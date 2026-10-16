using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in a save: two entries of the save's data, short lines each.
    ///
    ///   reallife:flare_account
    ///     FL1|seed|start|settled|followers|reputation|last post day|last post minute|posts day|posts today|last viral|
    ///         posts|likes|next id|unseen|following|civic seen|career|history,history,...|notice day|hype day|
    ///         complaint day|noticed day|story day|arrest day|online day|partner tag day|others tag day|never day|neglect day|
    ///         ledger day|mayor mood|issue day
    ///     D|id|source|day|hour|person|place|name|arg          a draft
    ///     S|place|first day|best|days                         a business as Flare has seen it
    ///     B|place|percent|until                               talk about a business, in its customers
    ///     V|stock|name|day                                    a viral post about a company the player holds
    ///     R|key                                               a friend's reply the player liked (key: post number * 32 + the reply's place); an older version skips the line
    ///     C|post|slot|kind|who                                a comment under another's post the player liked (kind 0 the city's, 1 a friend, 2 an influencer); an older version skips it
    ///   reallife:flare_posts
    ///     P|id|day|hour|author|who|source|tone|tag|place|name|arg|seed|quality|reach|likes|replies|likers|repliers|gained|flags|
    ///         minute|reply to|text|picture|net id|token|author id|author name|district|topic|reactors|mention|reposts|player likes|
    ///         case|flags2|when|picture in the session                          (the living network's fields, appended: an older save's lines lack them)
    ///   reallife:flare_cast
    ///     FC1|cast seed|month day|follow notice day|news today|news day|viral niche day|viral niche
    ///     I|id|handle|niche|district|followers|engagement|reliability|style|reputation|start|quit|star|person|scandal day|seed|staff
    ///     H|id|kind|place|name|from|to|percent|who,who|late|off message|backfired|ended
    ///     F|account                                           an account the player follows
    ///   reallife:flare_net                                    (FlareLiveStore: the City Office's cases, the watches, the messages)
    ///
    /// Every number is written in the invariant culture. What is not understood is left out a line at a time: a post or
    /// a draft of a kind this version does not know (from a later one) is dropped, never read as something else, so an
    /// entry from a later version or a damaged one never stops a load. An account line that is not this version's is
    /// not read at all (a new account). Game-free.
    /// </summary>
    public static class FlareStore
    {
        public const string AccountKey = "reallife:flare_account";
        public const string PostsKey = "reallife:flare_posts";
        public const string CastKey = "reallife:flare_cast";
        private const string CastVersion = "FC1";
        public const string Keys = "reallife:flare_*";
        private const string Version = "FL1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The kinds this version knows: anything past them is from a later one.
        private const int Sources = (int)FlareSource.Answer + 1;     // (Graduated was once left out of it, so a graduation's post was dropped when the save was read)
        private const int Tones = (int)FlareTone.Cheeky + 1;
        private const int Authors = (int)FlareAuthor.Tycoon + 1;
        private const int Niches = (int)FlareNiche.Business + 1;
        private const int Styles = (int)FlareStyle.Wholesome + 1;

        public static void Save(Dictionary<string, string> data, FlareFile f)
        {
            if (data == null || f == null)
            {
                return;
            }
            data[AccountKey] = WriteAccount(f);
            data[PostsKey] = WritePosts(f);
            data[CastKey] = WriteCast(f);
            data[FlareLiveStore.Key] = FlareLiveStore.Write(f.Net);
        }

        /// <summary>The save's file, or null if it has none this version can read (a new account is opened then).</summary>
        public static FlareFile Load(Dictionary<string, string> data)
        {
            if (data == null || !data.ContainsKey(AccountKey))
            {
                return null;
            }
            FlareFile f = Peek(data);
            if (f == null)
            {
                // An account that cannot be read (damaged, or of a later version) is kept as it is, with its posts and cast (Core/SaveLines).
                SaveLines.Unreadable(data, AccountKey, "it could not be read");
                if (SaveLines.IsKept(data, AccountKey))
                {
                    SaveLines.Unreadable(data, PostsKey, "its account could not be read");
                    SaveLines.Unreadable(data, CastKey, "its account could not be read");
                    SaveLines.Unreadable(data, FlareLiveStore.Key, "its account could not be read");
                }
            }
            return f;
        }

        /// <summary>
        /// The save's file as <see cref="Load"/> reads it, the save's data left exactly as it is (an account that does not read
        /// is not marked to be kept here: Load does that when Flare starts). For Flare's faces drawn behind the loading screen.
        /// </summary>
        public static FlareFile Peek(Dictionary<string, string> data)
        {
            string account, posts;
            if (data == null || !data.TryGetValue(AccountKey, out account))
            {
                return null;
            }
            data.TryGetValue(PostsKey, out posts);
            FlareFile f = Read(account, posts);
            string cast;
            if (f != null && data.TryGetValue(CastKey, out cast))
            {
                ReadCast(f, cast);
            }
            string net;
            if (f != null && data.TryGetValue(FlareLiveStore.Key, out net))
            {
                FlareLiveStore.Read(f.Net, net);
            }
            return f;
        }

        public static string WriteCast(FlareFile f)
        {
            var b = new StringBuilder(256 + f.Influencers.Count * 90 + f.Hires.Count * 70 + f.Follows.Count * 20);
            b.Append(CastVersion);
            Join(b, f.CastSeed, f.MonthDay, f.FollowNoticeDay, f.NewsToday, f.NewsDay, f.ViralNicheDay, f.ViralNiche);
            for (int i = 0; i < f.Influencers.Count; i++)
            {
                FlareInfluencer n = f.Influencers[i];
                b.Append("\nI");
                Join(b, n.Id);
                b.Append('|').Append(Clean(n.Handle));
                Join(b, (int)n.Niche);
                b.Append('|').Append(Clean(n.District));
                Join(b, n.Followers, n.Engagement, n.Reliability, (int)n.Style, n.Reputation, n.StartDay, n.Quit ? 1 : 0, n.Star ? 1 : 0, n.Person, n.ScandalDay, n.Seed, n.Staff ? 1 : 0);
            }
            for (int i = 0; i < f.Hires.Count; i++)
            {
                FlareHire h = f.Hires[i];
                b.Append("\nH");
                Join(b, h.Id, h.Kind);
                b.Append('|').Append(Clean(h.Place)).Append('|').Append(Clean(h.Name));
                Join(b, h.From, h.To, h.Percent);
                b.Append('|');
                for (int k = 0; k < h.Who.Count; k++)
                {
                    b.Append(k > 0 ? "," : string.Empty).Append(h.Who[k].ToString(Inv));
                }
                Join(b, h.Late ? 1 : 0, h.OffMessage ? 1 : 0, h.Backfired ? 1 : 0, h.Ended ? 1 : 0, h.Posted, h.LastPost, h.Counted ? 1 : 0);   // (the last three appended: a save from before has none)
            }
            for (int i = 0; i < f.Follows.Count; i++)
            {
                b.Append("\nF|").Append(Clean(f.Follows[i]));
            }
            return b.ToString();
        }

        /// <summary>The cast, the hires and the follows into a file. What is not understood is left out a line at a time; another version's entry leaves the file without them (a new cast is made).</summary>
        public static void ReadCast(FlareFile f, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 8 || h[0] != CastVersion)
                {
                    return;
                }
                f.CastSeed = I(h, 1);
                f.MonthDay = I(h, 2, -1);
                f.FollowNoticeDay = I(h, 3, -1);
                f.NewsToday = I(h, 4);
                f.NewsDay = I(h, 5, -1);
                f.ViralNicheDay = I(h, 6, -100);
                f.ViralNiche = I(h, 7, -1);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "I" && c.Length >= 17 && f.Influencers.Count < FlareCast.KeptMost)
                    {
                        int niche = I(c, 3, -1), style = I(c, 8, -1);
                        if (niche < 0 || niche >= Niches || style < 0 || style >= Styles)
                        {
                            continue;   // a kind of a later version's
                        }
                        f.Influencers.Add(new FlareInfluencer
                        {
                            Id = I(c, 1), Handle = Text(c[2]) ?? "@?", Niche = (FlareNiche)niche, District = Text(c[4]), Followers = Math.Max(0, I(c, 5)), Engagement = FlareRules.Clamp(I(c, 6), 0, 10000),
                            Reliability = FlareRules.Clamp(I(c, 7), 0, 100), Style = (FlareStyle)style, Reputation = FlareRules.Clamp(I(c, 9), 0, 100), StartDay = I(c, 10), Quit = I(c, 11) != 0,
                            Star = I(c, 12) != 0, Person = I(c, 13, -1), ScandalDay = I(c, 14, -100), Seed = I(c, 15), Staff = I(c, 16) != 0
                        });
                    }
                    else if (c[0] == "H" && c.Length >= 13 && f.Hires.Count < 32)
                    {
                        var hire = new FlareHire
                        {
                            Id = I(c, 1), Kind = I(c, 2), Place = Text(c[3]), Name = Text(c[4]), From = I(c, 5), To = I(c, 6), Percent = I(c, 7),
                            Late = I(c, 9) != 0, OffMessage = I(c, 10) != 0, Backfired = I(c, 11) != 0, Ended = I(c, 12) != 0, Posted = Math.Max(0, I(c, 13)), LastPost = I(c, 14, -1),
                            Counted = c.Length >= 15 && I(c, 15, 1) != 0   // (a deal saved before the posts were counted: those it had are not known)
                        };
                        string[] who = c[8].Split(',');
                        for (int k = 0; k < who.Length && hire.Who.Count < 3; k++)
                        {
                            int id;
                            if (int.TryParse(who[k], NumberStyles.Integer, Inv, out id))
                            {
                                hire.Who.Add(id);
                            }
                        }
                        if (hire.Place != null && hire.Who.Count > 0)
                        {
                            f.Hires.Add(hire);
                        }
                    }
                    else if (c[0] == "F" && c.Length >= 2 && c[1].Length > 0 && f.Follows.Count < 64 && !f.Follows.Contains(c[1]))
                    {
                        f.Follows.Add(c[1]);
                    }
                }
            }
            catch (Exception)
            {
                f.Influencers.Clear();
                f.Hires.Clear();
                f.Follows.Clear();
            }
        }

        public static string WriteAccount(FlareFile f)
        {
            var b = new StringBuilder(512 + f.Drafts.Count * 60 + f.Shops.Count * 40);
            b.Append(Version);
            Join(b, f.Seed, f.StartDay, f.SettledDay, f.Followers, f.Reputation, f.LastPostDay, f.LastPostMinute, f.PostsDay, f.PostsToday, f.LastViralDay,
                f.TotalPosts, f.TotalLikes, f.NextId, f.Unseen, f.Following, f.CivicSeen, f.CareerKey);
            b.Append('|');
            for (int i = 0; i < f.History.Length; i++)
            {
                b.Append(i > 0 ? "," : string.Empty).Append(f.History[i].ToString(Inv));
            }
            Join(b, f.NoticeDay, f.HypeDay, f.ComplaintDay, f.NoticedDay, f.StoryDay, f.ArrestDay, f.OnlineDay, f.PartnerTagDay, f.OthersTagDay, f.NeverDay, f.NeglectDay,
                f.LedgerDay, f.MayorMood, f.IssueDay);
            for (int i = 0; i < f.Drafts.Count; i++)
            {
                FlareDraft d = f.Drafts[i];
                b.Append("\nD");
                Join(b, d.Id, (int)d.Source, d.Day, d.Hour, d.Person);
                b.Append('|').Append(Clean(d.Place)).Append('|').Append(Clean(d.Name)).Append('|').Append(d.Arg.ToString(Inv));
            }
            for (int i = 0; i < f.Shops.Count; i++)
            {
                FlareShopSeen s = f.Shops[i];
                b.Append("\nS|").Append(Clean(s.Place));
                Join(b, s.FirstDay, s.Best, s.Days);
            }
            for (int i = 0; i < f.Buzz.Count; i++)
            {
                FlareBuzz z = f.Buzz[i];
                b.Append("\nB|").Append(Clean(z.Place));
                Join(b, z.Percent, z.Until);
            }
            for (int i = 0; i < f.ViralShares.Count; i++)
            {
                FlareViralShares v = f.ViralShares[i];
                b.Append("\nV");
                Join(b, v.Stock);
                b.Append('|').Append(Clean(v.Name));
                Join(b, v.Day);
            }
            for (int i = 0; i < f.ReplyLikes.Count; i++)
            {
                if (f.Post(f.ReplyLikes[i] / 32) != null)   // (one whose post is gone is not kept)
                {
                    b.Append("\nR");
                    Join(b, f.ReplyLikes[i]);
                }
            }
            for (int i = 0; i < f.CommentLikes.Count; i++)
            {
                int post, slot, kind, who;
                FlareCommentRules.Parts(f.CommentLikes[i], out post, out slot, out kind, out who);
                if (f.Post(post) != null)   // (one whose post is gone is not kept)
                {
                    b.Append("\nC");
                    Join(b, post, slot, kind, who);
                }
            }
            return b.ToString();
        }

        public static string WritePosts(FlareFile f)
        {
            var b = new StringBuilder(f.Posts.Count * 90);
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                b.Append(i > 0 ? "\nP" : "P");
                Join(b, p.Id, p.Day, p.Hour, (int)p.Author, p.Who, (int)p.Source, (int)p.Tone, p.Tag);
                b.Append('|').Append(Clean(p.Place)).Append('|').Append(Clean(p.Name));
                Join(b, p.Arg, p.Seed, p.Quality, p.Reach, p.Likes, p.Replies, p.Likers, p.Repliers, p.Gained, (p.Viral ? 1 : 0) | (p.Bad ? 2 : 0) | (p.Disclosed ? 4 : 0));
                Join(b, p.Minute, p.ReplyTo);
                Esc(b, p.Text, p.Pic);
                Join(b, p.NetId, p.Token);
                Esc(b, p.AuthorId, p.AuthorName, p.District, p.Topic, p.Reactors, p.Mention);
                Join(b, p.Reposts, p.PlayerLikes, p.Case, (p.Liked ? 1 : 0) | (p.Reposted ? 2 : 0) | (p.Shared ? 4 : 0) | (p.Pending ? 8 : 0), p.When);
                Esc(b, p.NetPic);
            }
            return b.ToString();
        }

        public static FlareFile Read(string account, string posts)
        {
            if (string.IsNullOrEmpty(account))
            {
                return null;
            }
            try
            {
                string[] lines = account.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 19 || h[0] != Version)
                {
                    return null;
                }
                var f = new FlareFile
                {
                    Seed = I(h, 1), StartDay = I(h, 2), SettledDay = I(h, 3), Followers = Math.Max(0, I(h, 4)), Reputation = FlareRules.Clamp(I(h, 5, FlareRules.ReputationStart), 0, 100),
                    LastPostDay = I(h, 6, -1), LastPostMinute = I(h, 7, -1), PostsDay = I(h, 8, -1), PostsToday = Math.Max(0, I(h, 9)), LastViralDay = I(h, 10, -100),
                    TotalPosts = Math.Max(0, I(h, 11)), TotalLikes = Math.Max(0, I(h, 12)), NextId = Math.Max(1, I(h, 13, 1)), Unseen = Math.Max(0, I(h, 14)),
                    Following = I(h, 15), CivicSeen = I(h, 16), CareerKey = I(h, 17, -1), NoticeDay = I(h, 19, -1),
                    HypeDay = I(h, 20, -100), ComplaintDay = I(h, 21, -1), NoticedDay = I(h, 22, -1), StoryDay = I(h, 23, -1), ArrestDay = I(h, 24, -1),
                    OnlineDay = I(h, 25, -100), PartnerTagDay = I(h, 26, -1), OthersTagDay = I(h, 27, -1), NeverDay = I(h, 28, -100), NeglectDay = I(h, 29, -100), LedgerDay = I(h, 30, -1),
                    MayorMood = FlareRules.Clamp(I(h, 31), -100, 100), IssueDay = I(h, 32, -1)
                };
                string[] history = h[18].Split(',');
                for (int i = 0; i < f.History.Length; i++)
                {
                    f.History[i] = i < history.Length ? Math.Max(0, I(history, i, f.Followers)) : f.Followers;
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "D" && c.Length >= 9 && f.Drafts.Count < FlareRules.DraftsMost)
                    {
                        int source = I(c, 2, -1);
                        if (source > 0 && source < Sources)
                        {
                            f.Drafts.Add(new FlareDraft
                            {
                                Id = I(c, 1), Source = (FlareSource)source, Day = I(c, 3), Hour = I(c, 4), Person = I(c, 5, -1), Place = Text(c[6]), Name = Text(c[7]), Arg = I(c, 8)
                            });
                        }
                    }
                    else if (c[0] == "S" && c.Length >= 5 && c[1].Length > 0 && f.Shops.Count < 64)
                    {
                        f.Shops.Add(new FlareShopSeen { Place = c[1], FirstDay = I(c, 2), Best = Math.Max(0, I(c, 3)), Days = Math.Max(0, I(c, 4)) });
                    }
                    else if (c[0] == "V" && c.Length >= 4 && f.ViralShares.Count < 16)
                    {
                        f.ViralShares.Add(new FlareViralShares { Stock = I(c, 1, -1), Name = Text(c[2]), Day = I(c, 3) });
                    }
                    else if (c[0] == "B" && c.Length >= 4 && c[1].Length > 0 && f.Buzz.Count < 64)
                    {
                        f.Buzz.Add(new FlareBuzz { Place = c[1], Percent = FlareRules.Clamp(I(c, 2), -FlareRules.BuzzMost, FlareRules.BuzzMost), Until = I(c, 3) });
                    }
                }
                ReadPosts(f, posts);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    int key = c.Length >= 2 && c[0] == "R" ? I(c, 1, -1) : -1;
                    if (key >= 0 && f.ReplyLikes.Count < FlareRules.ReplyLikesMost && !f.ReplyLikes.Contains(key))
                    {
                        f.ReplyLikes.Add(key);
                    }
                    long like = c.Length >= 5 && c[0] == "C" ? FlareCommentRules.FromParts(I(c, 1, -1), I(c, 2, -1), I(c, 3, -1), I(c, 4, -1)) : -1;
                    if (like >= 0 && f.CommentLikes.Count < FlareCommentRules.LikesKept && !f.CommentLikes.Contains(like))
                    {
                        f.CommentLikes.Add(like);
                    }
                }
                FlareRules.DropLikesOfGone(f);   // (the posts are read now: a like of a reply or a comment whose post is gone is let go)
                return f;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReadPosts(FlareFile f, string posts)
        {
            if (string.IsNullOrEmpty(posts))
            {
                return;
            }
            string[] lines = posts.Split('\n');
            for (int i = 0; i < lines.Length && f.Posts.Count < FlareRules.PostsMost; i++)
            {
                string[] c = lines[i].TrimEnd('\r').Split('|');
                if (c[0] != "P" || c.Length < 21)
                {
                    continue;
                }
                int author = I(c, 4, -1), source = I(c, 6, -1), tone = I(c, 7, -1);
                if (author < 0 || author >= Authors || source <= 0 || source >= Sources || tone < 0 || tone >= Tones)
                {
                    continue;   // a kind of a later version's
                }
                int flags = I(c, 20), flags2 = I(c, 36);
                f.Posts.Add(new FlarePost
                {
                    Id = I(c, 1), Day = I(c, 2), Hour = I(c, 3), Author = (FlareAuthor)author, Who = I(c, 5, -1), Source = (FlareSource)source, Tone = (FlareTone)tone, Tag = I(c, 8, -1),
                    Place = Text(c[9]), Name = Text(c[10]), Arg = I(c, 11), Seed = I(c, 12), Quality = I(c, 13), Reach = Math.Max(0, I(c, 14)), Likes = Math.Max(0, I(c, 15)),
                    Replies = Math.Max(0, I(c, 16)), Likers = I(c, 17), Repliers = I(c, 18), Gained = I(c, 19), Viral = (flags & 1) != 0, Bad = (flags & 2) != 0, Disclosed = (flags & 4) != 0,
                    Minute = FlareRules.Clamp(I(c, 21), 0, 59), ReplyTo = I(c, 22, -1), Text = U(c, 23), Pic = U(c, 24), NetId = I(c, 25), Token = I(c, 26), AuthorId = U(c, 27),
                    AuthorName = U(c, 28), District = U(c, 29), Topic = U(c, 30), Reactors = U(c, 31), Mention = U(c, 32), Reposts = Math.Max(0, I(c, 33)),
                    PlayerLikes = Math.Max(0, I(c, 34)), Case = I(c, 35, -1), Liked = (flags2 & 1) != 0, Reposted = (flags2 & 2) != 0, Shared = (flags2 & 4) != 0,
                    Pending = (flags2 & 8) != 0, When = I(c, 37, -1), NetPic = U(c, 38)
                });
            }
        }

        private static void Join(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        // Typed text and the network's fields: escaped, so that what the player typed is kept as typed ('|' and all).
        private static void Esc(StringBuilder b, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(FlareNetShare.Esc(values[i]));
            }
        }

        private static string U(string[] c, int at)
        {
            return at < c.Length ? FlareNetShare.Unesc(c[at]) : null;
        }

        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Text(string field)
        {
            return string.IsNullOrEmpty(field) ? null : field;
        }

        private static int I(string[] c, int at, int otherwise = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : otherwise;
        }
    }
}
