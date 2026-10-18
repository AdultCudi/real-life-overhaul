using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in a Going Public session (Core/SharedCity, part F; the lines are FlareNetShare's): one network for the city.
    /// The host makes the city's posts and runs the City Office, and sends every shared post to the guests; a guest's post
    /// comes in as a request and goes in the feed under its player's name. A following guest keeps its own friends,
    /// influencers and brands. Pictures go as small copies; another player's text is filtered. Cost: nothing per frame;
    /// a guest's waiting lines once a second, the host's update every few seconds, a picture's encode on a worker.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const int PicturesKept = 24;       // the session's pictures kept in memory, the newest

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
        private readonly int _session = Environment.TickCount & 0x7FFFFFFF;   // this run of the game as a host, in the whole part's head
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

        // A guest no longer follows the host's feed: what was on its way is the player's own.
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

        // The host: Flare stops in a session: its guests are sent an empty whole, so they run their own again.
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

        private void ShareChanged(FlarePost p)
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

        private void ShareCaseChanged(FlareCase c)
        {
            if (GoingPublic.IsHost && c != null)
            {
                _casesChanged.Add(c.Id);
            }
        }

        private void ShareWatchesChanged()
        {
            _watchesChanged = GoingPublic.IsHost;
        }

        private string HostWhole()
        {
            if (!Running || Save == null)
            {
                _offToSend = false;   // the empty whole that tells the guests Flare stopped: sent once
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
                    if (FlareNetShare.Count(p, ask, who, _likedBy, _sharedBy))   // once a player: a like, its taking back, a share
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

        // A guest's post, in the host's feed under that player's name: its report filed here, its review applied here.
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

        // A piece of a guest's picture, kept until the picture is whole; then it goes to everybody with the next update.
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

        // A post of the player's goes to the others: a guest's through the host, the host's with its next update.
        private void ShareOwn(FlarePost p)
        {
            if (!GoingPublic.InSession || !FlareLiveRules.ToSession(Settings.FlareSharePosts, p.Source))
            {
                return;   // a report goes always: it is the City Office's, which on a guest is the host's
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
                ShareChanged(p);
            }
        }

        // One of the day's four pictures for the session; past them the post goes without, and the player is told once a day.
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
                ShareChanged(_file.Post(post));
            }
        }

        private void ShareLike(FlarePost p, bool on)
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
                ShareChanged(p);
            }
        }

        private void ShareRepost(FlarePost p)
        {
            if (Following && p.Shared && p.NetId > 0)
            {
                _outbox.Add(FlareNetShare.AskShare(p.NetId));
            }
            else if (GoingPublic.IsHost)
            {
                _sharedBy.Add((p.NetId > 0 ? p.NetId : p.Id) + "|" + MyId);
                ShareChanged(p);
            }
        }

        private void ShareGoing(FlarePost p)
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

        private void ShareDm(string to, string text)
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

        /// <summary>What another player typed as this player is shown it: Steam's filter, else the mod's word list; both at Strict.</summary>
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

        /// <summary>A picture of the session's by its id, as Base64 of its small copy; null if this machine does not have it yet.</summary>
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
