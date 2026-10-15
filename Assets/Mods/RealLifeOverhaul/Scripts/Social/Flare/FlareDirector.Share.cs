using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in a Going Public session (Core/SharedCity, part F; the lines are FlareNetShare's): ONE network for the city.
    ///
    ///   the host   makes the city's posts and runs the City Office as alone; every shared post (the city's, its own under
    ///              its name, every guest's) goes to the guests: whole when one says hello, then what is new or changed
    ///              with each update. A guest's post comes in as a request and is put in the feed under that player's name;
    ///              its report is filed with the City Office here; a guest's message goes on to its one player alone.
    ///   a guest    makes none of the city's posts and runs no City Office while it follows: it takes the host's, beside
    ///              its own friends, influencers and brands. Its own post goes up at once on its screen and to the host
    ///              within the second, and comes back with the host's number for it (by its token: never twice).
    ///
    /// Pictures go as small copies (FlareSnap: 16 KB at most), made and read on a worker thread, a piece of 3,000 characters
    /// a second from a guest, four a day a player at most. What another player typed is shown only after a filter
    /// (Steam's own, else the mod's word list; both at Strict), and a player can be muted or hidden. Effects keep to "never
    /// change another player's things": a review of, or a complaint about, another player's business is applied by that
    /// player's own game when the post (or the City Office's case) comes to it.
    ///
    /// COST: nothing per frame. Once a second on a guest: the lines waiting (a few). An update every ten seconds or so on
    /// the host: the posts since the last (a few lines). A picture: one encode or decode of 16 KB on a worker thread.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const int PicturesKept = 24;       // the session's pictures kept in memory (Base64), the newest

        private readonly HashSet<int> _netChanged = new HashSet<int>();
        private readonly HashSet<int> _casesChanged = new HashSet<int>();
        private readonly List<KeyValuePair<string, string>> _netPics = new List<KeyValuePair<string, string>>(PicturesKept);
        private readonly List<string> _newPics = new List<string>(4);
        private readonly FlarePieceBook _pieces = new FlarePieceBook();
        private readonly HashSet<string> _likedBy = new HashSet<string>();
        private readonly HashSet<string> _sharedBy = new HashSet<string>();
        private readonly FlareOutbox _outbox = new FlareOutbox();
        private readonly FlareRateBook _rates = new FlareRateBook();
        private readonly List<string> _chunks = new List<string>(8);
        private readonly StringBuilder _lines = new StringBuilder(4096);
        private readonly int _session = Environment.TickCount & 0x7FFFFFFF;   // this run of the game as a host: in the whole part's head
        private Func<string, bool> _askF;
        private bool _watchesChanged;
        private bool _offToSend;                   // the host: Flare stopped in a session; the guests are sent an empty whole once
        private int _netFromId = int.MaxValue;     // the host: posts from this number on are new since the last update

        private bool Hosting => SharedCity.Leading && Running;

        // Once, in Awake: Flare's part of the shared city.
        private void ShareRegister()
        {
            _askF = line => SharedCity.Ask(FlareNetShare.Part, line);
            SharedCity.Register(new SharedCityPart
            {
                Id = FlareNetShare.Part, Name = "Flare",
                Ready = () => (Running && Save != null) || _offToSend,
                Write = HostWhole,
                Changes = HostChanges,
                Apply = GuestApply,
                Request = HostRequest,
                Left = HostGone
            });
        }

        // A guest: the host's feed is no longer followed (the host left, or stopped Flare): what was on its way is the player's own.
        private void HostGone()
        {
            _outbox.Clear();
            int kept = FlareNetShare.HostLeft(_file);
            if (kept > 0)
            {
                Logger?.Info("Flare: the host's feed is no longer followed; " + kept + " post(s) of this player's that were on their way stay as their own.");
                Changed();
            }
        }

        // The host: Flare stops (switched off, or the city unloads) in a session: its guests are sent an empty whole, so they run their own again.
        private void ShareStop()
        {
            if (GoingPublic.IsHost && _running)
            {
                _offToSend = true;
                SharedCity.Changed(FlareNetShare.Part);
            }
        }

        // The host: Flare starts in a session: its guests are sent its feed whole.
        private void ShareStart()
        {
            _offToSend = false;
            if (GoingPublic.IsHost)
            {
                SharedCity.Changed(FlareNetShare.Part);
            }
        }

        // Once a second: a guest's lines for the host, as many as a request may carry.
        private void ShareSecond()
        {
            if (_outbox.Count > 0 && Following)
            {
                _outbox.Send(_askF, FlareNetShare.LinesMost - SharedCity.Waiting(FlareNetShare.Part));
            }
            else if (_outbox.Count > 0 && !GoingPublic.InSession)
            {
                _outbox.Clear();
            }
        }

        private static string MyId => GoingPublicLink.MyId;

        // ---------------------------------------------------------------- what changed (the host)

        private void Shared_Changed(FlarePost p)
        {
            if (!GoingPublic.IsHost)
            {
                return;
            }
            if (_netFromId == int.MaxValue && _file != null)
            {
                _netFromId = _file.NextId;
            }
            if (p != null)
            {
                _netChanged.Add(p.Id);
            }
        }

        private void Shared_CaseChanged(FlareCase c)
        {
            if (GoingPublic.IsHost && c != null)
            {
                _casesChanged.Add(c.Id);
            }
        }

        private void Shared_WatchesChanged()
        {
            _watchesChanged = GoingPublic.IsHost;
        }

        // The host's whole part, for a guest that says hello.
        private string HostWhole()
        {
            if (!Running || Save == null)
            {
                _offToSend = false;   // (the empty whole that tells the guests Flare stopped here: sent once)
                return null;
            }
            if (_netFromId == int.MaxValue)
            {
                _netFromId = _file.NextId;
            }
            return FlareNetShare.WriteWhole(_file, Save.Day, MyId, MyName, Settings.FlareSharePosts, _netPics, _session);
        }

        // The host's update: the shared posts made or changed since the last, the cases that moved, the watches, new pictures.
        private string HostChanges()
        {
            if (!Running || _file == null)
            {
                return null;
            }
            if (_netFromId == int.MaxValue)
            {
                _netFromId = _file.NextId;
                return null;
            }
            _lines.Length = 0;
            bool shares = Settings.FlareSharePosts;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if ((p.Id >= _netFromId || _netChanged.Contains(p.Id)) && FlareNetShare.IsShared(_file, p, shares))
                {
                    _lines.Append(_lines.Length > 0 ? "\n" : string.Empty);
                    FlareNetShare.WritePost(_lines, _file, p, MyId, MyName);
                }
            }
            for (int i = 0; i < _file.Net.Cases.Count; i++)
            {
                if (_casesChanged.Contains(_file.Net.Cases[i].Id))
                {
                    _lines.Append(_lines.Length > 0 ? "\n" : string.Empty);
                    FlareNetShare.WriteCase(_lines, _file, _file.Net.Cases[i]);
                }
            }
            if (_watchesChanged)
            {
                _lines.Append(_lines.Length > 0 ? "\nW-" : "W-");
                FlareNetShare.WriteWatches(_lines, _file.Net);
            }
            for (int i = 0; i < _newPics.Count; i++)
            {
                string data = NetPicture(_newPics[i]);
                if (data != null)
                {
                    _lines.Append(_lines.Length > 0 ? "\n" : string.Empty);
                    FlareNetShare.WritePicture(_lines, _newPics[i], data);
                }
            }
            _netFromId = _file.NextId;
            _netChanged.Clear();
            _casesChanged.Clear();
            _newPics.Clear();
            _watchesChanged = false;
            return _lines.Length > 0 ? _lines.ToString() : null;
        }

        // ---------------------------------------------------------------- a guest's asks (the host)

        private void HostRequest(string text, string who)
        {
            if (!Running || Save == null || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(who))
            {
                return;
            }
            string[] lines = text.Split('\n');
            int taken = 0, refused = 0;
            for (int i = 0; i < lines.Length && i < FlareNetShare.LinesMost + 1; i++)
            {
                FlareAsk ask;
                if (lines[i].Length == 0 || !FlareNetShare.ReadAsk(lines[i], out ask))
                {
                    continue;
                }
                if (!_rates.Allow(who, ask.Kind, UnityEngine.Time.unscaledTime, Save.Day, ask.Kind == 'X' ? ask.Index : 0))
                {
                    refused++;
                    continue;
                }
                try
                {
                    Asked(ask, who);
                    taken++;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            if (Meta.Dev.Verbose && (taken > 0 || refused > 0))
            {
                Logger?.Info("Flare check (the host): " + taken + " line(s) of a guest's taken, " + refused + " over its bounds left out: PASS");
            }
            if (taken > 0)
            {
                Changed();
            }
        }

        private void Asked(FlareAsk ask, string who)
        {
            FlarePost p;
            switch (ask.Kind)
            {
                case 'P':
                    GuestPost(ask, who);
                    break;
                case 'L':
                case 'S':
                    p = ByNet(ask.NetId);
                    if (FlareNetShare.Count(p, ask, who, _likedBy, _sharedBy))   // (once a player: a like, its taking back, a share)
                    {
                        _netChanged.Add(p.Id);
                    }
                    break;
                case 'G':
                    p = ByNet(ask.NetId);
                    if (p != null)
                    {
                        GoingTo(p, who, who);
                    }
                    break;
                case 'D':
                    if (ask.To == MyId)
                    {
                        DmIn(who, who, Now, ask.Text);
                    }
                    else
                    {
                        SharedCity.ToPlayer(FlareNetShare.Part, ask.To, FlareNetShare.WriteDm(who, who, Now, ask.Text));
                    }
                    break;
                case 'X':
                    Piece(ask, who);
                    break;
            }
        }

        // A guest's post, put in the host's feed under that player's name: its report filed here, its review applied to what is the host's own or the city's.
        private void GuestPost(FlareAsk ask, string who)
        {
            FlarePost p = ask.Post;
            FlarePost parent = p.ReplyTo > 0 ? ByNet(p.ReplyTo) : null;
            if (p.Source == FlareSource.Reply && parent == null)
            {
                return;   // a reply to a post the host no longer has
            }
            int id = _file.NextId++;
            p.Id = id;
            p.NetId = id;
            p.Author = FlareAuthor.Network;
            p.AuthorId = who;
            p.AuthorName = ask.Name ?? who;
            FlareNetShare.CleanPost(p, text => Filter(text, who));   // its words, names and tags as the others will see them
            p.AuthorName = p.AuthorName ?? who;
            p.Token = ask.Token;
            p.ReplyTo = parent != null ? parent.Id : -1;
            p.Pic = p.Pic != null && p.Pic.StartsWith("n:" + who + "/", StringComparison.Ordinal) ? p.Pic : null;
            p.Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29);
            p.Likes = p.Reach * FlareRules.LikeShare / 1000;
            p.Replies = p.Reach / FlareRules.ReplyPer;
            FlareLiveRules.SetAt(p, Now);
            _file.Posts.Add(p);
            if (p.Source == FlareSource.Report)
            {
                FileCase(p, "p:" + who, 0f, 0f);
            }
            else if (p.Source == FlareSource.Review)
            {
                BusinessEffect(p, false);
            }
            if (p.Source != FlareSource.Reply)
            {
                FlareLiveRules.CityReplies(_file, p, null);
            }
            MentionAnswer(p);
            FlareRules.Prune(_file);
            Logger?.Info("Flare: a post of " + who + "'s (" + p.Source + ") is in the session's feed as number " + id + ".");
        }

        // A piece of a guest's picture: kept until the picture is whole, then it goes to everybody with the next update.
        private void Piece(FlareAsk ask, string who)
        {
            string whole = _pieces.Add(who, ask, UnityEngine.Time.unscaledTime);
            if (whole == null)
            {
                return;
            }
            if (whole.Length == 0)
            {
                Logger?.Warn("Flare: a picture of " + who + "'s came damaged and is left out.");
                return;
            }
            KeepPicture(ask.PicId, whole);
            _newPics.Add(ask.PicId);
        }

        // ---------------------------------------------------------------- the player's own (both)

        // A post of the player's: in a session, to the others (a guest's through the host; the host's goes with its next update).
        private void Shared_Own(FlarePost p)
        {
            if (!GoingPublic.InSession || !FlareLiveRules.ToSession(Settings.FlareSharePosts, p.Source))
            {
                return;   // (a report goes always: it is the City Office's, which on a guest is the host's)
            }
            bool picture = p.Pic != null && Settings.FlareSharePosts && Settings.FlareSharePictures && PictureAllowed(p);
            if (picture)
            {
                p.NetPic = "n:" + MyId + "/" + _file.Net.PicNext++;
                StartCoroutine(Guard.Routine(SendPicture(p.Id, p.Pic, p.NetPic.Substring(2))));
            }
            if (Following)
            {
                p.Pending = true;
                p.Token = p.Id;
                FlarePost parent = p.ReplyTo >= 0 ? _file.Post(p.ReplyTo) : null;
                _outbox.Add(FlareNetShare.AskPost(p, parent != null && parent.NetId > 0 ? parent.NetId : -1, MyName));
            }
            else
            {
                Shared_Changed(p);
            }
        }

        // One of the day's four pictures for the session; past them the post goes without its picture, and the player is told once that day.
        private bool PictureAllowed(FlarePost p)
        {
            FlareRateBook.PictureWay way = FlareRateBook.Picture(_file.Net, Save.Day);
            if (way == FlareRateBook.PictureWay.Send)
            {
                return true;
            }
            Logger?.Info("Flare: post " + p.Id + " goes to the session without its picture: " + FlareRateBook.PicturesDay + " pictures today already.");
            if (way == FlareRateBook.PictureWay.TellWithout && Settings.SocialMediaNotices)
            {
                Tell(NotificationType.Info, "reallife_flare_note_pictures_day", _openFeed, "count", FlareRateBook.PicturesDay.ToString());
            }
            return false;
        }

        // The small copy of a picture of the player's, made on a worker thread; then kept (the host) or sent in pieces (a guest).
        private IEnumerator SendPicture(int post, string pic, string id)
        {
            string path = FlarePictures.PathOf(pic);
            Task<byte[]> work = Task.Run(() =>
            {
                int w = 0, h = 0;
                bool undecoded;
                byte[] rows = path != null ? PhotoThumbs.Get(path, out w, out h, out undecoded) : null;
                return rows != null ? FlareSnap.Encode(rows, w, h) : null;
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            byte[] packed = work.Status == TaskStatus.RanToCompletion ? work.Result : null;
            if (packed == null || !Running)
            {
                Logger?.Info("Flare: the picture of post " + post + " could not be made small for the session; the post goes without it.");
                yield break;
            }
            if (Following)
            {
                FlareSnap.Chunks(packed, _chunks);
                for (int i = 0; i < _chunks.Count; i++)
                {
                    _outbox.Add(FlareNetShare.AskPiece(id, i, _chunks.Count, _chunks[i]));
                }
                KeepPicture(id, Convert.ToBase64String(packed));
                Logger?.Info("Flare: the picture of post " + post + " goes to the host in " + _chunks.Count + " piece(s), " + packed.Length + " bytes.");
            }
            else if (GoingPublic.IsHost)
            {
                KeepPicture(id, Convert.ToBase64String(packed));
                _newPics.Add(id);
                Shared_Changed(_file.Post(post));
            }
        }

        private void Shared_Like(FlarePost p, bool on)
        {
            if (!GoingPublic.InSession)
            {
                return;
            }
            if (Following && p.Shared && p.NetId > 0)
            {
                _outbox.Add(FlareNetShare.AskLike(p.NetId, on));
            }
            else if (GoingPublic.IsHost && FlareNetShare.IsShared(_file, p, true) && FlareNetShare.Count(p, new FlareAsk { Kind = 'L', On = on }, MyId, _likedBy, _sharedBy))
            {
                Shared_Changed(p);
            }
        }

        private void Shared_Repost(FlarePost p)
        {
            if (Following && p.Shared && p.NetId > 0)
            {
                _outbox.Add(FlareNetShare.AskShare(p.NetId));
            }
            else if (GoingPublic.IsHost)
            {
                _sharedBy.Add((p.NetId > 0 ? p.NetId : p.Id) + "|" + MyId);
                Shared_Changed(p);
            }
        }

        private void Shared_Going(FlarePost p)
        {
            if (Following && p.Shared && p.NetId > 0)
            {
                _outbox.Add(FlareNetShare.AskGoing(p.NetId));
            }
            else if (GoingPublic.IsHost && p.Author == FlareAuthor.Network)
            {
                GoingTo(p, MyId, MyName);
            }
        }

        // The host: a player goes to a player's invitation: its author is told (the host itself, or that guest alone).
        private void GoingTo(FlarePost invite, string who, string name)
        {
            if (invite.Author == FlareAuthor.Player)
            {
                FlareLiveRules.Dm(_file.Net, "p:" + who, name, Now, false, null, "reallife_flare_dm_going", "event=" + FlareText.InviteName(invite));
                Changed();
            }
            else if (invite.Author == FlareAuthor.Network && invite.AuthorId != null && invite.AuthorId != who)
            {
                SharedCity.ToPlayer(FlareNetShare.Part, invite.AuthorId, FlareNetShare.WriteGoing(who, name, Now, invite.NetId > 0 ? invite.NetId : invite.Id));
            }
        }

        private void Shared_Dm(string to, string text)
        {
            if (Following)
            {
                _outbox.Add(FlareNetShare.AskDm(to, text));
            }
            else if (GoingPublic.IsHost)
            {
                SharedCity.ToPlayer(FlareNetShare.Part, to, FlareNetShare.WriteDm(MyId, MyName, Now, text));
            }
        }

        // A message from another player of the session: filtered, and not at all from a player muted or hidden.
        private void DmIn(string from, string name, long at, string text)
        {
            if (_file.Net.IsMuted(from) || _file.Net.IsHidden(from))
            {
                return;
            }
            FlareLiveRules.Dm(_file.Net, "p:" + from, name != null ? Filter(name, from) : from, Math.Min(at, Now), false, Filter(text, from), null, null);
            if (Settings.SocialMediaNotices)
            {
                Tell(NotificationType.Info, "reallife_flare_note_dm", _openMessages, "name", name ?? from);
            }
            Changed();
        }

        // ---------------------------------------------------------------- the host's feed (a guest)

        // The host's part on a guest: whole (the shared posts as the host has them, its cases and watches) or what changed.
        private bool GuestApply(string text, bool whole)
        {
            if (!Running || Save == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] lines = text.Split('\n');
            int start = 0;
            HashSet<int> present = null;
            if (whole)
            {
                int hostDay, seed;
                string hostKey;
                if (!FlareNetShare.ReadHead(lines[0], out hostDay, out seed, out hostKey))
                {
                    return false;
                }
                if (FlareNetShare.NewHost(_file, hostKey))
                {
                    Logger?.Info("Flare: a host or session other than the last (" + hostKey + "): the posts of the last session go, and this player's own are theirs alone again.");
                }
                int dropped = FlareNetShare.DropOwnCity(_file, Save.Day);
                if (dropped > 0)
                {
                    Logger?.Info("Flare: following the host's city: " + dropped + " post(s) of this player's own city of today give way to the host's.");
                }
                if (seed != 0 && seed != _file.Net.CitySeed)
                {
                    _file.Net.CitySeed = seed;   // the host's city's people, from now on (their names stay theirs after the session)
                    EnsureCitizens(_file);
                }
                present = new HashSet<int>();
                _file.Net.Cases.Clear();
                start = 1;
            }
            int posts = 0, fresh = 0;
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }
                try
                {
                    string[] f = line[0] == 'X' ? line.Split(new[] { '|' }, 3) : line.Split('|');
                    switch (f[0])
                    {
                        case "P":
                            int net = HostPost(f, out bool added);
                            posts += net > 0 ? 1 : 0;
                            fresh += added ? 1 : 0;
                            if (net > 0)
                            {
                                present?.Add(net);
                            }
                            break;
                        case "C":
                            HostCase(f);
                            break;
                        case "W-":
                            _file.Net.Watches.Clear();
                            break;
                        case "W":
                            FlareWatch w;
                            if (FlareNetShare.ReadWatch(f, out w))
                            {
                                _file.Net.Watches.Add(w);
                            }
                            break;
                        case "X":
                            if (f.Length >= 3)
                            {
                                string id = FlareNetShare.Unesc(f[1]);
                                if (id != null && f[2].Length <= FlareSnap.MostBytes * 2)
                                {
                                    KeepPicture(id, f[2]);
                                }
                            }
                            break;
                        case "D":
                            string from, name, words;
                            long at;
                            if (FlareNetShare.ReadDm(f, out from, out name, out at, out words))
                            {
                                DmIn(from, name, at, words);
                            }
                            break;
                        case "V":
                            string who, goer;
                            long when;
                            int invite;
                            if (FlareNetShare.ReadGoing(f, out who, out goer, out when, out invite) && !_file.Net.IsHidden(who))
                            {
                                FlarePost p = ByNet(invite);
                                FlareLiveRules.Dm(_file.Net, "p:" + who, goer != null ? Filter(goer, who) : who, Math.Min(when, Now), false, null, "reallife_flare_dm_going", "event=" + (p != null ? FlareText.InviteName(p) : "?"));
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger?.Warn("Flare: a line of the host's feed could not be read and is left out (" + e.Message + ").");
                }
            }
            if (present != null)
            {
                for (int i = _file.Posts.Count - 1; i >= 0; i--)
                {
                    if (_file.Posts[i].Shared && !present.Contains(_file.Posts[i].NetId))
                    {
                        _file.Posts.RemoveAt(i);   // the host no longer has it
                    }
                }
            }
            FlareRules.Prune(_file);
            if (whole || fresh > 0)
            {
                Logger?.Info("Flare: the host's feed " + (whole ? "whole: " : "updated: ") + posts + " post(s), " + fresh + " new here, " + _file.Net.Cases.Count + " case(s), "
                             + _file.Net.Watches.Count + " watch(es).");
            }
            Recount();
            Changed();
            return true;
        }

        // One of the host's shared posts: this guest's own coming back (found by its token), one it has (its figures), or a new one. Its session number; 0 for none.
        private int HostPost(string[] f, out bool added)
        {
            added = false;
            FlarePost p;
            int replyNet;
            if (!FlareNetShare.ReadPost(f, out p, out replyNet))
            {
                return 0;
            }
            string me = MyId;
            if (p.Author == FlareAuthor.Network && p.AuthorId == me && me.Length > 0)
            {
                FlarePost own = p.Token > 0 ? _file.Post(p.Token) : null;
                if (own != null && own.Author == FlareAuthor.Player)
                {
                    own.NetId = p.NetId;
                    own.Pending = false;
                    own.PlayerLikes = p.PlayerLikes;
                    own.Reposts = Math.Max(own.Reposts, p.Reposts);
                    return p.NetId;
                }
                FlarePost mine = ByNet(p.NetId);
                if (mine != null)
                {
                    return p.NetId;   // (already matched)
                }
                p.Author = FlareAuthor.Player;   // a post of this player's from before this machine knew it (a reload): theirs
                p.Shared = false;
            }
            FlarePost known = ByNet(p.NetId);
            if (known != null)
            {
                known.Likes = p.Likes;
                known.Replies = p.Replies;
                known.Reposts = p.Reposts;
                known.PlayerLikes = p.PlayerLikes;
                return p.NetId;
            }
            FlarePost parent = replyNet > 0 ? ByNet(replyNet) : null;
            if (replyNet > 0 && parent == null)
            {
                return 0;   // a reply to a post this guest does not have
            }
            p.Id = _file.NextId++;
            p.ReplyTo = parent != null ? parent.Id : -1;
            if (p.Author == FlareAuthor.Network)
            {
                string author = p.AuthorId;
                FlareNetShare.CleanPost(p, text => Filter(text, author));   // another player's words, names and tags
                p.AuthorName = p.AuthorName ?? p.AuthorId;
            }
            if (p.Source == FlareSource.Answer && p.Case >= 0)
            {
                p.Case = -p.Case - 1;   // (a case of the host's: its number is the host's, never this save's own)
            }
            _file.Posts.Add(p);
            added = true;
            if (!_file.Net.Applied.Contains(p.NetId))
            {
                _file.Net.Applied.Add(p.NetId);
                while (_file.Net.Applied.Count > FlareNet.AppliedMost)
                {
                    _file.Net.Applied.RemoveAt(0);
                }
                if (FlareNetShare.AppliesOnArrival(p, Save.Day))
                {
                    SessionEffect(p);   // (today's: on joining, the earlier days' reviews have had their days)
                }
            }
            return p.NetId;
        }

        // Another player's review of a business that is this player's own (or the city's): its customers move here.
        private void SessionEffect(FlarePost p)
        {
            BuildingRegistration reg = p.Place != null ? FlareLiveGame.AnyBusiness(p.Place) : null;
            if (reg == null || GoingPublic.Foreign(reg) || p.Source != FlareSource.Review)
            {
                return;
            }
            BusinessEffect(p, false);
        }

        // One of the host's cases: kept as the host has it (its posts found by their session numbers); an inspection of this player's business is applied here.
        private void HostCase(string[] f)
        {
            FlareCase c;
            int postNet, answerNet;
            if (!FlareNetShare.ReadCase(f, out c, out postNet, out answerNet))
            {
                return;
            }
            FlarePost post = ByNet(postNet), answer = ByNet(answerNet);
            c.Post = post != null ? post.Id : -1;
            c.Answer = answer != null ? answer.Id : -1;
            c.Reporter = post != null && post.Author == FlareAuthor.Player ? "me" : "host";
            for (int i = 0; i < _file.Net.Cases.Count; i++)
            {
                if (_file.Net.Cases[i].Id == c.Id)
                {
                    _file.Net.Cases[i] = c;
                    c = null;
                    break;
                }
            }
            if (c != null)
            {
                _file.Net.Cases.Add(c);
                while (_file.Net.Cases.Count > FlareNet.CasesMost)
                {
                    _file.Net.Cases.RemoveAt(0);
                }
            }
            FlareCase now = c ?? _file.Net.Case(int.Parse(f[1]));
            if (now != null && now.Order == FlareOrder.Inspection && now.State == FlareCaseState.Acting && !_file.Net.Applied.Contains(-1000000 - now.Id))
            {
                BuildingRegistration reg = now.Place != null ? FlareLiveGame.AnyBusiness(now.Place) : null;
                if (reg != null && GoingPublic.Mine(reg))
                {
                    _file.Net.Applied.Add(-1000000 - now.Id);
                    Inspected(now, Save.Day);
                }
            }
        }

        // ---------------------------------------------------------------- the text and the pictures of others

        /// <summary>What another player typed, as this player is shown it: Steam's filter as its author's (else the mod's word list), and at Strict both and addresses. Also the City Ledger's pieces of guests (Wiring/LedgerLinks).</summary>
        public static string Filter(string text, string author)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            string steam = FlareLiveGame.SteamFilter(text, author);
            string clean = steam ?? FlareFilter.Clean(text, FlareFilter.Standard);
            return Settings.FlareFilter >= FlareFilter.Strict ? FlareFilter.Clean(clean, FlareFilter.Strict) : clean;
        }

        private void KeepPicture(string id, string base64)
        {
            for (int i = 0; i < _netPics.Count; i++)
            {
                if (_netPics[i].Key == id)
                {
                    return;
                }
            }
            _netPics.Add(new KeyValuePair<string, string>(id, base64));
            while (_netPics.Count > PicturesKept)
            {
                _netPics.RemoveAt(0);
            }
            if (_view != null)
            {
                _view.Changed();
            }
        }

        /// <summary>A picture of the session's by its id, as Base64 of its small copy; null if this machine does not have it (yet).</summary>
        public string NetPicture(string id)
        {
            for (int i = 0; i < _netPics.Count; i++)
            {
                if (_netPics[i].Key == id)
                {
                    return _netPics[i].Value;
                }
            }
            return null;
        }

        /// <summary>A post by its session number (the host's own posts by their own number), or null.</summary>
        private FlarePost ByNet(int net)
        {
            if (net <= 0 || _file == null)
            {
                return null;
            }
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.NetId == net || (GoingPublic.IsHost && p.Id == net && !p.Shared))
                {
                    return p;
                }
            }
            return null;
        }
    }
}
