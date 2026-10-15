using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a guest asks of the host, one line of a request, as read by the host (<see cref="FlareNetShare.ReadAsk"/>).</summary>
    public struct FlareAsk
    {
        public char Kind;            // 'P' a post, 'L' a like, 'S' a share, 'D' a message, 'X' a piece of a picture, 'G' going to an invitation
        public int Token;            // a post: the guest's own number for it
        public FlarePost Post;       // a post: what it is (its ReplyTo is the session's number of the post it answers, or -1)
        public string Name;          // a post: the guest's name, as they gave it
        public int NetId;            // a like, a share, going: the session's number of the post
        public bool On;              // a like: true to like, false to take it back
        public string To;            // a message: the player it is for
        public string Text;          // a message: its words; a piece of a picture: its Base64
        public string PicId;         // a piece of a picture: the picture's own id ("player/n")
        public int Index, Count;     // ...which piece, of how many
    }

    /// <summary>
    /// Flare in a Going Public session (Core/SharedCity, part F): ONE network for the city, the host's. Game-free
    /// (EconomyCheck, FlareLiveChecks.cs).
    ///
    /// The host's feed is the city's: its people, the official accounts, the businesses, the paper's and the market's
    /// accounts, and every player's posts, the host's own among them under its name. A guest does not make the city's
    /// posts while it follows (FlareLiveRules.CityDay); it takes the host's, and keeps its own friends, influencers and
    /// brands beside them, which are its own. The City Office is the host's: a guest's report is filed and acted on there,
    /// and its answer comes back to everybody.
    ///
    ///   the whole part   "FN1|day|seed of the city's people", then a P line for each shared post, a C line for each case, the W lines of the
    ///                    watches and an X line for each of the newest pictures
    ///   an update        the P lines of the posts made or changed since the last, the C lines of the cases that moved,
    ///                    the W lines (all of them, after a "W-" line) when a watch changed, the X lines of new pictures
    ///   to one player    "D|from|name|at|text": a message (Core/SharedCity.ToPlayer, never to anybody else)
    ///   a guest's ask    P (a post), L (a like), S (a share), D (a message), X (a piece of a picture), G (going): at most
    ///                    <see cref="LinesMost"/> a request; the rest waits for the next second (<see cref="FlareOutbox"/>)
    ///
    /// P|net|day|hour|minute|author|who|source|tone|arg|place|name|district|topic|text|pic|reply|authorId|authorName|likes|
    ///   replies|reposts|playerLikes|reach|when|mention|flags|token|case
    ///
    /// Typed text is escaped (% | and line breaks), at most <see cref="FlareLiveRules.TextMost"/> characters; every number
    /// is read with its bounds; a line this version does not understand is left out. A newer RLO's lines would not reach an
    /// older one at all: the shared city refuses another version whole (SharedCity), and this part's letter is new.
    /// </summary>
    public static class FlareNetShare
    {
        public const char Part = 'F';
        public const string Version = "FN1";
        public const int LinesMost = 16;          // lines of one guest's request the host reads
        public const int AskChars = 1600;         // the longest line of a request a host reads (a picture's piece is 3,000 and has its own bound)
        public const int NameMost = 32;           // characters of a player's name
        public const int PicturesWhole = 6;       // the newest pictures in the whole part
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- what is shared

        /// <summary>
        /// Whether a post of the host's is the city's, to be shared: the city's people's, the official accounts', the
        /// businesses', the paper's, the market's and the candidates' posts, the players' own (the host's only if it shares
        /// its posts), and a reply to any of those. A friend's, an influencer's or a brand's post is that player's own.
        /// </summary>
        public static bool IsShared(FlareFile f, FlarePost p, bool hostShares)
        {
            if (p == null || p.Pending)
            {
                return false;
            }
            switch (p.Author)
            {
                case FlareAuthor.Player: return hostShares && p.Source != FlareSource.Sponsored;
                case FlareAuthor.Citizen:
                case FlareAuthor.Official:
                case FlareAuthor.Business:
                case FlareAuthor.Ledger:
                case FlareAuthor.Company:
                case FlareAuthor.Candidate:
                case FlareAuthor.Network:
                case FlareAuthor.Tycoon:
                    if (p.ReplyTo >= 0)
                    {
                        FlarePost parent = f.Post(p.ReplyTo);
                        return parent != null && IsShared(f, parent, hostShares);
                    }
                    return true;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- writing

        /// <summary>The host's whole part: every shared post, case and watch, and the newest pictures.</summary>
        public static string WriteWhole(FlareFile f, int day, string hostId, string hostName, bool hostShares, IList<KeyValuePair<string, string>> pictures)
        {
            var b = new StringBuilder(32 * 1024);
            b.Append(Version).Append('|').Append(day.ToString(Inv)).Append('|').Append(f.Net.CitySeed.ToString(Inv));
            for (int i = 0; i < f.Posts.Count; i++)
            {
                if (IsShared(f, f.Posts[i], hostShares))
                {
                    b.Append('\n');
                    WritePost(b, f, f.Posts[i], hostId, hostName);
                }
            }
            for (int i = 0; i < f.Net.Cases.Count; i++)
            {
                b.Append('\n');
                WriteCase(b, f, f.Net.Cases[i]);
            }
            b.Append("\nW-");
            WriteWatches(b, f.Net);
            for (int i = Math.Max(0, (pictures?.Count ?? 0) - PicturesWhole); pictures != null && i < pictures.Count; i++)
            {
                b.Append('\n');
                WritePicture(b, pictures[i].Key, pictures[i].Value);
            }
            return b.ToString();
        }

        /// <summary>One shared post as a line. The host's own posts go under its name; a reply names the session's number of its post.</summary>
        public static void WritePost(StringBuilder b, FlareFile f, FlarePost p, string hostId, string hostName)
        {
            bool own = p.Author == FlareAuthor.Player;
            FlarePost parent = p.ReplyTo >= 0 ? f.Post(p.ReplyTo) : null;
            int net = p.NetId > 0 ? p.NetId : p.Id;
            int reply = parent != null ? (parent.NetId > 0 ? parent.NetId : parent.Id) : -1;
            b.Append("P|").Append(net.ToString(Inv));
            Ints(b, p.Day, p.Hour, p.Minute, own ? (int)FlareAuthor.Network : (int)p.Author, p.Who, (int)p.Source, (int)p.Tone, p.Arg);
            Texts(b, p.Place, p.Name, p.District, p.Topic, p.Text, own ? p.NetPic : p.Pic != null && p.Pic.StartsWith("n:", StringComparison.Ordinal) ? p.Pic : null);
            Ints(b, reply);
            Texts(b, own ? hostId : p.AuthorId, own ? hostName : p.AuthorName);
            Ints(b, p.Likes, p.Replies, p.Reposts, p.PlayerLikes, p.Reach, p.When);
            Texts(b, p.Mention);
            Ints(b, (p.Viral ? 1 : 0) | (p.Bad ? 2 : 0), p.Token, p.Case);
        }

        public static void WriteCase(StringBuilder b, FlareFile f, FlareCase c)
        {
            FlarePost post = c.Post >= 0 ? f.Post(c.Post) : null, answer = c.Answer >= 0 ? f.Post(c.Answer) : null;
            b.Append("C|").Append(c.Id.ToString(Inv));
            Ints(b, (int)c.Kind);
            Texts(b, c.District, c.Place, c.Name);
            Ints(b, (int)c.State, (int)c.Order, c.Until, c.Ref, c.Repeats);
            b.Append('|').Append(c.Filed.ToString(Inv)).Append('|').Append(c.ReplyAt.ToString(Inv));
            Ints(b, post != null ? (post.NetId > 0 ? post.NetId : post.Id) : -1, answer != null ? (answer.NetId > 0 ? answer.NetId : answer.Id) : -1);
        }

        /// <summary>Every watch, each as a W line (after the "W-" line that clears a guest's).</summary>
        public static void WriteWatches(StringBuilder b, FlareNet net)
        {
            for (int i = 0; i < net.Watches.Count; i++)
            {
                FlareWatch w = net.Watches[i];
                b.Append("\nW");
                Texts(b, w.District);
                Ints(b, (int)w.Kind, w.Until);
            }
        }

        public static void WritePicture(StringBuilder b, string id, string base64)
        {
            b.Append("X");
            Texts(b, id);
            b.Append('|').Append(base64 ?? string.Empty);
        }

        /// <summary>A message for one player, from another (the host's own, or a guest's it passes on).</summary>
        public static string WriteDm(string from, string name, long at, string text)
        {
            var b = new StringBuilder(64 + (text?.Length ?? 0));
            b.Append('D');
            Texts(b, from, Clip(name, NameMost));
            b.Append('|').Append(at.ToString(Inv));
            Texts(b, FlareLiveRules.Clip(text, FlareLiveRules.DmTextMost));
            return b.ToString();
        }

        // ---------------------------------------------------------------- a guest's asks

        /// <summary>A guest's post as a line of its request: what it is, the built part and the typed text; the host stamps the time and the author.</summary>
        public static string AskPost(FlarePost p, int replyNet, string name)
        {
            var b = new StringBuilder(256 + (p.Text?.Length ?? 0) * 2);
            b.Append("P|").Append(p.Token.ToString(Inv));
            Ints(b, (int)p.Source, (int)p.Tone, p.Arg);
            Texts(b, p.Place, p.Name, p.District, p.Topic, p.Text, p.NetPic);
            Ints(b, replyNet, p.Reach, p.When);
            Texts(b, p.Mention, Clip(name, NameMost));
            Ints(b, (p.Viral ? 1 : 0) | (p.Bad ? 2 : 0));
            return b.ToString();
        }

        /// <summary>The host to a player whose invitation another is going to: who, their name, when, the post's session number.</summary>
        public static string WriteGoing(string who, string name, long at, int net)
        {
            var b = new StringBuilder(64);
            b.Append('V');
            Texts(b, who, Clip(name, NameMost));
            b.Append('|').Append(at.ToString(Inv)).Append('|').Append(net.ToString(Inv));
            return b.ToString();
        }

        public static bool ReadGoing(string[] f, out string who, out string name, out long at, out int net)
        {
            who = name = null;
            at = 0;
            net = -1;
            if (f.Length < 5 || f[0] != "V")
            {
                return false;
            }
            who = Clip(U(f, 1), 64);
            name = Clip(U(f, 2), NameMost);
            long.TryParse(f[3], NumberStyles.Integer, Inv, out at);
            net = I(f, 4, -1);
            return who != null && net > 0;
        }

        public static string AskLike(int net, bool on)
        {
            return "L|" + net.ToString(Inv) + "|" + (on ? "1" : "0");
        }

        public static string AskShare(int net)
        {
            return "S|" + net.ToString(Inv);
        }

        public static string AskGoing(int net)
        {
            return "G|" + net.ToString(Inv);
        }

        public static string AskDm(string to, string text)
        {
            var b = new StringBuilder(64);
            b.Append('D');
            Texts(b, to, FlareLiveRules.Clip(text, FlareLiveRules.DmTextMost));
            return b.ToString();
        }

        public static string AskPiece(string picId, int index, int count, string data)
        {
            var b = new StringBuilder(data.Length + 48);
            b.Append('X');
            Texts(b, picId);
            Ints(b, index, count);
            b.Append('|').Append(data);
            return b.ToString();
        }

        /// <summary>
        /// One line of a guest's request, as the host reads it; false for one it does not take (another kind, damaged, a
        /// built part out of its bounds). The typed text is clipped to its length here; what decides an effect is the
        /// built part, read with its bounds.
        /// </summary>
        public static bool ReadAsk(string line, out FlareAsk ask)
        {
            ask = default(FlareAsk);
            if (string.IsNullOrEmpty(line) || line.Length > (line[0] == 'X' ? FlareSnap.ChunkChars + 200 : AskChars))
            {
                return false;
            }
            string[] f = line.TrimEnd('\r').Split('|');
            ask.Kind = f[0].Length == 1 ? f[0][0] : '?';
            try
            {
                switch (ask.Kind)
                {
                    case 'P':
                        if (f.Length < 17)
                        {
                            return false;
                        }
                        int source = I(f, 2, -1), tone = I(f, 3, 0);
                        if (!GuestSource(source) || tone < 0 || tone > (int)FlareTone.Cheeky)
                        {
                            return false;
                        }
                        var p = new FlarePost
                        {
                            Source = (FlareSource)source, Tone = (FlareTone)tone, Arg = I(f, 4), Place = U(f, 5), Name = Clip(U(f, 6), 64), District = Clip(U(f, 7), 64),
                            Topic = Clip(U(f, 8), FlareLiveRules.TagsMost * (FlareLiveRules.TagLength + 1)), Text = FlareLiveRules.Clip(U(f, 9)), Pic = Clip(U(f, 10), 96),
                            ReplyTo = I(f, 11, -1), Reach = FlareRules.Clamp(I(f, 12), 0, 10000000), When = I(f, 13, -1), Mention = Clip(U(f, 14), 64)
                        };
                        int flags = I(f, 16);
                        p.Viral = (flags & 1) != 0;
                        p.Bad = (flags & 2) != 0;
                        if (!BuiltOk(p))
                        {
                            return false;
                        }
                        ask.Token = I(f, 1);
                        ask.Post = p;
                        ask.Name = Clip(U(f, 15), NameMost);
                        return true;
                    case 'L':
                        ask.NetId = I(f, 1, -1);
                        ask.On = f.Length > 2 && f[2] == "1";
                        return f.Length >= 3 && ask.NetId > 0;
                    case 'S':
                    case 'G':
                        ask.NetId = I(f, 1, -1);
                        return f.Length >= 2 && ask.NetId > 0;
                    case 'D':
                        if (f.Length < 3)
                        {
                            return false;
                        }
                        ask.To = Clip(U(f, 1), 64);
                        ask.Text = FlareLiveRules.Clip(U(f, 2), FlareLiveRules.DmTextMost);
                        return ask.To != null && ask.Text != null;
                    case 'X':
                        if (f.Length < 5)
                        {
                            return false;
                        }
                        ask.PicId = Clip(U(f, 1), 96);
                        ask.Index = I(f, 2, -1);
                        ask.Count = I(f, 3, 0);
                        ask.Text = f[4];
                        return ask.PicId != null && ask.Count >= 1 && ask.Count <= FlareSnap.PartsMost && ask.Index >= 0 && ask.Index < ask.Count && ask.Text.Length <= FlareSnap.ChunkChars;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The kinds of post a guest may send: a moment of now, its moments of the day, a report, a review, an announcement, a reply.</summary>
        public static bool GuestSource(int source)
        {
            if (source <= 0 || source > (int)FlareSource.Answer)
            {
                return false;
            }
            var s = (FlareSource)source;
            return s == FlareSource.Report || s == FlareSource.Review || s == FlareSource.Announce || s == FlareSource.Reply || s == FlareSource.Life
                   || (s >= FlareSource.Photo && s <= FlareSource.Arrested) || s == FlareSource.Shares || s == FlareSource.Issue || s == FlareSource.Campaign
                   || s == FlareSource.Graduated || s == FlareSource.Interior;
        }

        /// <summary>The built part of a post within its bounds: a report's kind, a review's stars, an announcement's kind.</summary>
        public static bool BuiltOk(FlarePost p)
        {
            switch (p.Source)
            {
                case FlareSource.Report: return p.Arg >= 0 && p.Arg <= (int)FlareReport.Utilities && p.District != null;
                case FlareSource.Review: return p.Arg >= 1 && p.Arg <= 5 && p.Place != null;
                case FlareSource.Announce: return p.Arg >= 0 && p.Arg <= (int)FlareAnnounce.New && p.Place != null;
                case FlareSource.Reply: return p.ReplyTo > 0 && p.Text != null;
                default: return true;
            }
        }

        // ---------------------------------------------------------------- reading the host's

        /// <summary>
        /// One shared post of the host's as a post (its ReplyTo is still the session's number, for the caller to find);
        /// false for a line that does not read. The typed text is as it came: the caller filters it before it keeps it.
        /// </summary>
        public static bool ReadPost(string[] f, out FlarePost p, out int replyNet)
        {
            p = null;
            replyNet = -1;
            if (f.Length < 29 || f[0] != "P")
            {
                return false;
            }
            int author = I(f, 5, -1), source = I(f, 7, -1), tone = I(f, 8, 0);
            if (author < 0 || author > (int)FlareAuthor.Tycoon || source <= 0 || source > (int)FlareSource.Answer || tone < 0 || tone > (int)FlareTone.Cheeky)
            {
                return false;
            }
            p = new FlarePost
            {
                NetId = I(f, 1), Day = I(f, 2), Hour = FlareRules.Clamp(I(f, 3), 0, 23), Minute = FlareRules.Clamp(I(f, 4), 0, 59), Author = (FlareAuthor)author, Who = I(f, 6, -1),
                Source = (FlareSource)source, Tone = (FlareTone)tone, Arg = I(f, 9), Place = U(f, 10), Name = U(f, 11), District = U(f, 12), Topic = U(f, 13),
                Text = FlareLiveRules.Clip(U(f, 14)), Pic = U(f, 15), AuthorId = Clip(U(f, 17), 64), AuthorName = Clip(U(f, 18), NameMost),
                Likes = Math.Max(0, I(f, 19)), Replies = Math.Max(0, I(f, 20)), Reposts = Math.Max(0, I(f, 21)), PlayerLikes = Math.Max(0, I(f, 22)), Reach = Math.Max(0, I(f, 23)),
                When = I(f, 24, -1), Mention = U(f, 25), Token = I(f, 27), Case = I(f, 28, -1), Shared = true
            };
            int flags = I(f, 26);
            p.Viral = (flags & 1) != 0;
            p.Bad = (flags & 2) != 0;
            replyNet = I(f, 16, -1);
            return p.NetId > 0;
        }

        public static bool ReadCase(string[] f, out FlareCase c, out int postNet, out int answerNet)
        {
            c = null;
            postNet = answerNet = -1;
            if (f.Length < 15 || f[0] != "C")
            {
                return false;
            }
            int kind = I(f, 2, -1), state = I(f, 6, -1), order = I(f, 7, -1);
            if (kind < 0 || kind > (int)FlareReport.Utilities || state < 0 || state > (int)FlareCaseState.Queued || order < 0 || order > (int)FlareOrder.SiteCheck)
            {
                return false;
            }
            long filed, reply;
            long.TryParse(f[11], NumberStyles.Integer, Inv, out filed);
            long.TryParse(f[12], NumberStyles.Integer, Inv, out reply);
            c = new FlareCase
            {
                Id = I(f, 1), Kind = (FlareReport)kind, District = U(f, 3), Place = U(f, 4), Name = U(f, 5), State = (FlareCaseState)state, Order = (FlareOrder)order,
                Until = I(f, 8, -1), Ref = I(f, 9, -1), Repeats = Math.Max(0, I(f, 10)), Filed = filed, ReplyAt = reply
            };
            postNet = I(f, 13, -1);
            answerNet = I(f, 14, -1);
            return c.Id > 0;
        }

        public static bool ReadWatch(string[] f, out FlareWatch w)
        {
            w = null;
            if (f.Length < 4 || f[0] != "W")
            {
                return false;
            }
            int kind = I(f, 2, -1);
            if (kind != (int)FlareOrder.Patrol && kind != (int)FlareOrder.Cleanup)
            {
                return false;
            }
            w = new FlareWatch { District = U(f, 1), Kind = (FlareOrder)kind, Until = I(f, 3) };
            return w.District != null;
        }

        public static bool ReadDm(string[] f, out string from, out string name, out long at, out string text)
        {
            from = name = text = null;
            at = 0;
            if (f.Length < 5 || f[0] != "D")
            {
                return false;
            }
            from = Clip(U(f, 1), 64);
            name = Clip(U(f, 2), NameMost);
            long.TryParse(f[3], NumberStyles.Integer, Inv, out at);
            text = FlareLiveRules.Clip(U(f, 4), FlareLiveRules.DmTextMost);
            return from != null && text != null;
        }

        // ---------------------------------------------------------------- the text of a line

        /// <summary>A text as a field of a line: '%', '|' and line breaks written as %25, %7C, %0A and %0D.</summary>
        public static string Esc(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.IndexOf('%') < 0 && text.IndexOf('|') < 0 && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
            {
                return text;
            }
            var b = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '%': b.Append("%25"); break;
                    case '|': b.Append("%7C"); break;
                    case '\n': b.Append("%0A"); break;
                    case '\r': b.Append("%0D"); break;
                    default: b.Append(c); break;
                }
            }
            return b.ToString();
        }

        /// <summary>A field read back; null for an empty one.</summary>
        public static string Unesc(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return null;
            }
            if (field.IndexOf('%') < 0)
            {
                return field;
            }
            var b = new StringBuilder(field.Length);
            for (int i = 0; i < field.Length; i++)
            {
                if (field[i] == '%' && i + 2 < field.Length)
                {
                    string code = field.Substring(i + 1, 2);
                    char c = code == "25" ? '%' : code == "7C" ? '|' : code == "0A" ? ' ' : code == "0D" ? ' ' : '\0';
                    if (c != '\0')
                    {
                        b.Append(c);
                        i += 2;
                        continue;
                    }
                }
                b.Append(field[i]);
            }
            return b.ToString();
        }

        private static string U(string[] f, int at)
        {
            return at < f.Length ? Unesc(f[at]) : null;
        }

        public static string Clip(string text, int most)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            return text.Length <= most ? text : text.Substring(0, most);
        }

        private static void Ints(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void Texts(StringBuilder b, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(Esc(values[i]));
            }
        }

        private static int I(string[] c, int at, int otherwise = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : otherwise;
        }
    }

    /// <summary>
    /// A guest's lines for the host, waiting for the next second: each second takes as many as a request may carry (and
    /// fewer while the shared city still holds some of this part's), a picture's pieces one a second, in the order they
    /// were asked. Game-free.
    /// </summary>
    public sealed class FlareOutbox
    {
        public const int Most = 400;             // lines kept waiting at most: the oldest asks go first (a guest offline for long)
        private readonly List<string> _lines = new List<string>(16);

        public int Count => _lines.Count;

        public void Add(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            _lines.Add(line);
            while (_lines.Count > Most)
            {
                _lines.RemoveAt(0);
            }
        }

        /// <summary>The lines for this second: at most 'room', and only one piece of a picture. Returns how many went.</summary>
        public int Send(Action<string> ask, int room)
        {
            int sent = 0;
            bool piece = false;
            for (int i = 0; i < _lines.Count && sent < room;)
            {
                string line = _lines[i];
                if (line.Length > 0 && line[0] == 'X')
                {
                    if (piece)
                    {
                        i++;
                        continue;   // one piece a second: the rest wait, in order
                    }
                    piece = true;
                }
                ask(line);
                _lines.RemoveAt(i);
                sent++;
            }
            return sent;
        }

        public void Clear()
        {
            _lines.Clear();
        }
    }

    /// <summary>
    /// The host's bounds on what one player may ask, over the last ten minutes of real time and the game's day: posts,
    /// messages, likes and pictures. Game-free: the caller gives the time.
    /// </summary>
    public sealed class FlareRateBook
    {
        public const float Window = 600f;        // real seconds
        public const int PostsWindow = 6, PostsDay = 40, DmsWindow = 20, LikesWindow = 80, PicturesDay = 4;

        private readonly Dictionary<string, List<float>> _times = new Dictionary<string, List<float>>(16);
        private readonly Dictionary<string, int> _day = new Dictionary<string, int>(16);
        private int _dayOf = -1;

        /// <summary>True, and counted, if the player may ask this now.</summary>
        public bool Allow(string player, char kind, float now, int day)
        {
            if (string.IsNullOrEmpty(player))
            {
                return false;
            }
            if (day != _dayOf)
            {
                _day.Clear();
                _dayOf = day;
            }
            int most = kind == 'P' ? PostsWindow : kind == 'D' ? DmsWindow : kind == 'X' ? int.MaxValue : LikesWindow;
            string key = player + "|" + kind;
            List<float> times;
            if (!_times.TryGetValue(key, out times))
            {
                times = new List<float>(8);
                _times[key] = times;
            }
            for (int i = times.Count - 1; i >= 0; i--)
            {
                if (now - times[i] > Window)
                {
                    times.RemoveAt(i);
                }
            }
            int today;
            _day.TryGetValue(key, out today);
            int dayMost = kind == 'P' ? PostsDay : kind == 'X' ? PicturesDay * FlareSnap.PartsMost : int.MaxValue;
            if (times.Count >= most || today >= dayMost)
            {
                return false;
            }
            times.Add(now);
            _day[key] = today + 1;
            return true;
        }

        public void Clear()
        {
            _times.Clear();
            _day.Clear();
        }
    }
}
