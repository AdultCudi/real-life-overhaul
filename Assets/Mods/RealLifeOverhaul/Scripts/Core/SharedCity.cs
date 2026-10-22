using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One part of the city that is shared in a Going Public session: what the system it belongs to gives the layer
    /// (<see cref="SharedCity"/>). Each system registers its own when it starts.
    /// </summary>
    public sealed class SharedCityPart
    {
        public char Id;                              // its letter in a message (an update of only what changed has a small letter: Changes)
        public string Name;                          // for the log
        public Func<bool> Ready;                     // the system runs here and can be written or applied
        public Func<string> Write;                   // the host: the city's part as it stands, whole; null or empty while the host does not run it
        public Func<string> Changes;                 // the host, optional: what changed since the last call, for an update; null when nothing did
        public Func<string, bool, bool> Apply;       // a guest: the host's text, whole or (false) a change; true when applied
        public Action<string, string> Request;       // the host: a guest's request lines and Going Public's id of the guest
        public Action Left;                          // a guest: the host's city is no longer followed; the system goes on from where it is

        internal bool Dirty;                         // the host: changed since the last update
        internal bool Following;                     // a guest: the host's part is applied and this system does not step its own
        internal string Pending;                     // a guest: the host's whole part, waiting for the system to be ready
        internal StringBuilder PendingChanges;       // ...and the changes that came after it
    }

    /// <summary>
    /// The shared city: in a Going Public session the city's economy, government and stock market are one city, run by
    /// the host and followed by the guests (RealLifeOverhaul.Build/docs/other-mods.md, "The shared city"). A guest's
    /// systems do not step what <see cref="Follows"/> says is the host's; they apply the host's state to that player's
    /// game. A guest says Hello until answered with a Snapshot of every part; the host sends an Update of what changed at
    /// most every <see cref="UpdateEvery"/> seconds; a guest's doings go to the host as Requests; a part that came out
    /// different is asked for again (Resync). Every message carries this mod's version and the layout's number: another
    /// version is told once and never applied. Leaving undoes nothing: the guest steps on from the host's last state.
    /// Nothing per frame but a field read; once a second a few comparisons.
    /// </summary>
    public static class SharedCity
    {
        public const float UpdateEvery = 10f;        // real seconds between two updates from the host
        public const int ChangesMost = 256 * 1024;   // a change larger than this goes whole instead
        private const float HelloEvery = 5f;
        private const int HelloTries = 24;           // two minutes of a guest asking; then it plays its own city
        private const float ResyncRest = 120f;       // real seconds between two requests for a part again

        public static IModLogger Logger;

        private static readonly List<SharedCityPart> Parts = new List<SharedCityPart>(6);
        private static readonly List<object> Guests = new List<object>(4);       // the host's links to guests that run this mod in the same version
        private static readonly StringBuilder[] Asked = new StringBuilder[128];   // a guest's requests not yet sent, by part letter
        private static readonly Action<object, byte[]> OnMessage = Received;
        private static readonly Dictionary<string, StringBuilder> Private = new Dictionary<string, StringBuilder>(4);   // the host's lines for one player only, by Going Public's player id and the part's letter
        private static bool _anyPrivate;

        private static bool _following;            // a guest: the host's snapshot is applied
        private static bool _refused;              // a guest: the host runs another version; told once
        private static bool _toldShared;
        private static int _helloTries;
        private static float _nextHello, _nextUpdate, _nextResync;
        private static int _sequence, _lastSeen;
        private static bool _anyAsked;
        private static byte[] _recorded;           // the test's recorded snapshot
        private static bool _testFollow;           // the test applied a recording: a pretended guest follows it with no host

        /// <summary>The version this mod's messages carry.</summary>
        public static string Version => Meta.Changelog.Latest;

        /// <summary>True on a guest that follows the host's city.</summary>
        public static bool Following => _following;

        /// <summary>True on the host while at least one guest follows its city.</summary>
        public static bool Leading => GoingPublic.IsHost && Guests.Count > 0;

        /// <summary>True while this machine follows the host's part, which its system must not step. A search of five parts.</summary>
        public static bool Follows(char id)
        {
            if (!_following)
            {
                return false;
            }
            SharedCityPart part = Find(id);
            return part != null && part.Following;
        }

        /// <summary>A system's part of the city, given once when the system starts; a second one of the same letter takes its place.</summary>
        public static void Register(SharedCityPart part)
        {
            if (part == null)
            {
                return;
            }
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i].Id == part.Id)
                {
                    part.Following = Parts[i].Following;
                    Parts[i] = part;
                    return;
                }
            }
            Parts.Add(part);
        }

        /// <summary>The host: a part has changed and goes with the next update. Nothing while no guest follows.</summary>
        public static void Changed(char id)
        {
            if (Guests.Count == 0)
            {
                return;
            }
            SharedCityPart part = Find(id);
            if (part != null)
            {
                part.Dirty = true;
            }
        }

        /// <summary>
        /// The host: a line of a part for one player only (a direct message), sent to that guest alone within the second.
        /// False when that player is not a following guest (the caller keeps it, or applies it when it is the host).
        /// </summary>
        public static bool ToPlayer(char id, string player, string line)
        {
            if (!GoingPublic.IsHost || Guests.Count == 0 || string.IsNullOrEmpty(player) || string.IsNullOrEmpty(line))
            {
                return false;
            }
            bool there = false;
            for (int i = 0; i < Guests.Count && !there; i++)
            {
                there = GoingPublicLink.PlayerOf(Guests[i]) == player;
            }
            if (!there)
            {
                return false;
            }
            string key = id + player;
            StringBuilder b;
            if (!Private.TryGetValue(key, out b))
            {
                b = new StringBuilder(128);
                Private[key] = b;
            }
            if (b.Length < 16 * 1024)
            {
                b.Append(line.Replace('\n', ' ')).Append('\n');
                _anyPrivate = true;
            }
            return true;
        }

        public const int AskedMost = 16 * 1024;     // characters of one part's request waiting for the second

        /// <summary>
        /// A guest: one line of something the player did that changes the shared city, sent to the host within the
        /// second. False when not following or this second's request is full: a caller that must not lose it keeps it.
        /// </summary>
        public static bool Ask(char id, string line)
        {
            if (!_following || string.IsNullOrEmpty(line) || id >= Asked.Length)
            {
                return false;
            }
            StringBuilder b = Asked[id] ?? (Asked[id] = new StringBuilder(64));
            if (b.Length > 0 && b.Length + line.Length + 1 > AskedMost)
            {
                return false;
            }
            b.Append(line.Replace('\n', ' ')).Append('\n');
            _anyAsked = true;
            return true;
        }

        /// <summary>
        /// A guest: the lines a part has asked for that are not sent yet, for a part whose host reads only so many lines
        /// of one request. A count over a short text.
        /// </summary>
        public static int Waiting(char id)
        {
            StringBuilder b = id < Asked.Length ? Asked[id] : null;
            int lines = 0;
            for (int i = 0; b != null && i < b.Length; i++)
            {
                lines += b[i] == '\n' ? 1 : 0;
            }
            return lines;
        }

        private static SharedCityPart Find(char id)
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i].Id == id)
                {
                    return Parts[i];
                }
            }
            return null;
        }

        /// <summary>Once a frame, from <see cref="GoingPublicWatch"/>: a field read while nothing has come.</summary>
        public static void Frame()
        {
            if (GoingPublicLink.Waiting)
            {
                GoingPublicLink.Drain(OnMessage);
            }
        }

        /// <summary>Once a second of real time, from <see cref="GoingPublic.Tick"/>.</summary>
        public static void Second()
        {
            try
            {
                if (!ModState.Loaded)
                {
                    return;
                }
                float now = Time.unscaledTime;
                if (GoingPublic.IsGuest)
                {
                    GuestSecond(now);
                }
                else
                {
                    if (_following)
                    {
                        Stop(GoingPublic.IsHost ? "this machine hosts now" : "the session is over");
                    }
                    _refused = false;
                    _helloTries = 0;
                    if (GoingPublic.IsHost)
                    {
                        HostSecond(now);
                    }
                    else if (Guests.Count > 0)
                    {
                        Guests.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static bool LinkUp => _testFollow || (GoingPublicLink.Ready && GoingPublicLink.Connected);

        private static void GuestSecond(float now)
        {
            if (Guests.Count > 0)
            {
                Guests.Clear();
            }
            if (!LinkUp)
            {
                if (_following)
                {
                    Stop("the host is gone");
                }
                return;
            }
            if (!_following && !_refused && GoingPublicLink.Ready && now >= _nextHello && _helloTries < HelloTries)
            {
                _nextHello = now + HelloEvery;
                _helloTries++;
                if (GoingPublicLink.ToHost(Message(SharedKind.Hello)) && _helloTries == 1)
                {
                    Logger?.Info("Shared city: asked the host for its city (this mod " + Version + ").");
                }
                if (_helloTries == HelloTries)
                {
                    Logger?.Warn("Shared city: the host has not answered (it may not run this mod); this player's city stays their own in this session.");
                }
            }
            if (!_following)
            {
                return;
            }
            bool late = false;
            for (int i = 0; i < Parts.Count; i++)
            {
                SharedCityPart part = Parts[i];
                if (part.Pending != null && part.Ready != null && part.Ready())
                {
                    late |= ApplyWhole(part, part.Pending);
                }
            }
            if (late)
            {
                Logger?.Info("Shared city: a part that waited for its system here is now the host's.");
            }
            if (_anyAsked && !_testFollow)
            {
                _anyAsked = false;
                SharedMessage m = New(SharedKind.Request);
                for (int c = 0; c < Asked.Length; c++)
                {
                    if (Asked[c] != null && Asked[c].Length > 0)
                    {
                        m.Parts.Add(new SharedPart((char)c, Asked[c].ToString()));
                        Asked[c].Length = 0;
                    }
                }
                Send(null, m);
            }
            else if (_anyAsked)
            {
                _anyAsked = false;
                for (int c = 0; c < Asked.Length; c++)
                {
                    if (Asked[c] != null && Asked[c].Length > 0)
                    {
                        Logger?.Info("Shared city test: with no host, this request is not sent: " + (char)c + " " + Asked[c].ToString().Trim().Replace('\n', ';'));
                        Asked[c].Length = 0;
                    }
                }
            }
        }

        private static void HostSecond(float now)
        {
            for (int i = Guests.Count - 1; i >= 0; i--)
            {
                if (!GoingPublicLink.Alive(Guests[i]))
                {
                    Logger?.Info("Shared city: a guest has left (" + Guests.Count + " following before).");
                    Guests.RemoveAt(i);
                }
            }
            if (_anyPrivate)
            {
                SendPrivate();
            }
            if (Guests.Count == 0 || now < _nextUpdate)
            {
                return;
            }
            SharedMessage m = null;
            for (int i = 0; i < Parts.Count; i++)
            {
                SharedCityPart part = Parts[i];
                if (part.Ready == null || !part.Ready())
                {
                    continue;
                }
                string change = part.Changes != null ? part.Changes() : null;
                if (change != null && change.Length > ChangesMost)
                {
                    part.Dirty = true;   // too much at once (a long skip of the clock): the part goes whole instead
                    change = null;
                }
                if (part.Dirty)
                {
                    part.Dirty = false;
                    (m ?? (m = New(SharedKind.Update))).Parts.Add(new SharedPart(part.Id, part.Write() ?? string.Empty));
                }
                else if (!string.IsNullOrEmpty(change))
                {
                    (m ?? (m = New(SharedKind.Update))).Parts.Add(new SharedPart(char.ToLowerInvariant(part.Id), change));
                }
            }
            if (m == null)
            {
                return;
            }
            _nextUpdate = now + UpdateEvery;
            byte[] bytes = SharedCityWire.Write(m);
            if (bytes == null)
            {
                Logger?.Warn("Shared city: an update was too large to send; the guests are sent the whole city instead.");
                for (int g = 0; g < Guests.Count; g++)
                {
                    SendSnapshot(Guests[g]);
                }
                return;
            }
            for (int g = 0; g < Guests.Count; g++)
            {
                GoingPublicLink.ToGuest(Guests[g], bytes);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Shared city check (host): an update of " + Describe(m) + ", " + bytes.Length + " bytes, to " + Guests.Count + " guest(s): PASS");
            }
        }

        // The host, once a second when there are any: each guest's own lines, to that guest alone.
        private static void SendPrivate()
        {
            _anyPrivate = false;
            for (int g = 0; g < Guests.Count; g++)
            {
                string player = GoingPublicLink.PlayerOf(Guests[g]);
                SharedMessage m = null;
                foreach (KeyValuePair<string, StringBuilder> pair in Private)
                {
                    if (pair.Value.Length == 0 || pair.Key.Length < 2 || string.CompareOrdinal(pair.Key, 1, player, 0, Math.Max(player.Length, pair.Key.Length - 1)) != 0)
                    {
                        continue;
                    }
                    (m ?? (m = New(SharedKind.Update))).Parts.Add(new SharedPart(char.ToLowerInvariant(pair.Key[0]), pair.Value.ToString()));
                    pair.Value.Length = 0;
                }
                if (m != null)
                {
                    Send(Guests[g], m);
                }
            }
            Private.Clear();   // lines for a player who has left are not kept
        }

        private static SharedMessage New(SharedKind kind)
        {
            return new SharedMessage { Kind = kind, Version = Version, Sequence = ++_sequence };
        }

        private static byte[] Message(SharedKind kind)
        {
            return SharedCityWire.Write(New(kind));
        }

        private static void Send(object peer, SharedMessage m)
        {
            byte[] bytes = SharedCityWire.Write(m);
            if (bytes == null)
            {
                Logger?.Warn("Shared city: a message (" + m.Kind + ") was too large to send and was dropped.");
                return;
            }
            if (peer == null)
            {
                GoingPublicLink.ToHost(bytes);
            }
            else
            {
                GoingPublicLink.ToGuest(peer, bytes);
            }
        }

        // Every part the host runs, whole: what a guest is given when it says hello.
        private static SharedMessage Snapshot()
        {
            SharedMessage m = New(SharedKind.Snapshot);
            for (int i = 0; i < Parts.Count; i++)
            {
                SharedCityPart part = Parts[i];
                string text = part.Ready != null && part.Ready() ? part.Write() : null;
                if (!string.IsNullOrEmpty(text))
                {
                    m.Parts.Add(new SharedPart(part.Id, text));
                }
                // What changed and is not sent yet stays for the next update, which the following guests need.
            }
            return m;
        }

        private static void SendSnapshot(object peer)
        {
            SharedMessage m = Snapshot();
            byte[] bytes = SharedCityWire.Write(m);
            if (bytes == null)
            {
                Logger?.Warn("Shared city: the city is too large to send (over " + SharedCityWire.MostPacked / 1024 + " KB packed); " + GoingPublicLink.PlayerOf(peer) + " keeps their own.");
                return;
            }
            GoingPublicLink.ToGuest(peer, bytes);
            Logger?.Info("Shared city: the whole city (" + Describe(m) + ", " + bytes.Length / 1024 + " KB) sent to " + GoingPublicLink.PlayerOf(peer) + ".");
        }

        // A message has come (on the game's thread). A guest's come from the host with no peer; the host's from a guest's link.
        private static void Received(object peer, byte[] bytes)
        {
            SharedMessage m;
            SharedRead read = SharedCityWire.Read(bytes, Version, out m);
            if (read == SharedRead.Damaged)
            {
                Logger?.Warn("Shared city: a damaged message was dropped (" + (bytes != null ? bytes.Length : 0) + " bytes); nothing of it was applied.");
                return;
            }
            if (peer != null)
            {
                FromGuest(peer, read, m);
            }
            else
            {
                FromHost(read, m);
            }
        }

        private static void FromGuest(object peer, SharedRead read, SharedMessage m)
        {
            if (!GoingPublic.IsHost)
            {
                return;
            }
            string who = GoingPublicLink.PlayerOf(peer);
            if (who.Length == 0)
            {
                return;   // a link Going Public does not know yet: it drops what such a link sends, and so does this
            }
            if (read != SharedRead.Ok)
            {
                if (m != null && m.Kind == SharedKind.Hello || read == SharedRead.OtherFormat)
                {
                    Logger?.Warn("Shared city: " + who + " runs version " + m.Version + " of this mod and this machine " + Version + "; their city stays their own.");
                    Send(peer, New(SharedKind.Refused));
                }
                return;
            }
            switch (m.Kind)
            {
                case SharedKind.Hello:
                    Guests.Remove(peer);
                    Guests.Add(peer);
                    SendSnapshot(peer);
                    break;
                case SharedKind.Request:
                    if (!Guests.Contains(peer))
                    {
                        return;
                    }
                    for (int i = 0; i < m.Parts.Count; i++)
                    {
                        SharedCityPart part = Find(m.Parts[i].Id);
                        if (part != null && part.Request != null && part.Ready != null && part.Ready())
                        {
                            part.Request(m.Parts[i].Text, who);
                        }
                    }
                    break;
                case SharedKind.Resync:
                    if (!Guests.Contains(peer))
                    {
                        return;
                    }
                    SharedMessage again = New(SharedKind.Update);
                    for (int i = 0; i < m.Parts.Count; i++)
                    {
                        SharedCityPart part = Find(m.Parts[i].Id);
                        string text = part != null && part.Ready != null && part.Ready() ? part.Write() : null;
                        if (!string.IsNullOrEmpty(text))
                        {
                            again.Parts.Add(new SharedPart(part.Id, text));
                        }
                    }
                    Send(peer, again);
                    Logger?.Info("Shared city: " + who + " asked for " + Describe(again) + " again.");
                    break;
            }
        }

        private static void FromHost(SharedRead read, SharedMessage m)
        {
            if (!GoingPublic.IsGuest)
            {
                return;
            }
            if (read != SharedRead.Ok || m.Kind == SharedKind.Refused)
            {
                if (!_refused)
                {
                    _refused = true;
                    string host = m != null ? m.Version : "?";
                    Logger?.Warn("Shared city: the host runs version " + host + " of this mod and this machine " + Version + ". Nothing of the host's city is applied; this player's city stays their own in this session.");
                    Notifications.Show(NotificationType.Warning, "reallife_gp_city_versions", new Dictionary<string, string> { { "host", host }, { "mine", Version } }, 20f);
                }
                if (_following)
                {
                    Stop("the host runs another version");
                }
                return;
            }
            if (m.Sequence <= _lastSeen && m.Kind != SharedKind.Snapshot)
            {
                return;   // the channel is ordered: an older one can only be a repeat
            }
            _lastSeen = m.Sequence;
            if (m.Kind == SharedKind.Snapshot)
            {
                Follow(m, false);
            }
            else if (m.Kind == SharedKind.Update && _following)
            {
                Apply(m);
            }
        }

        // The host's snapshot: every part in it is the host's from now on; a part not in it the host does not run, and stays this player's.
        private static void Follow(SharedMessage m, bool test)
        {
            _following = true;
            _helloTries = 0;
            var names = new StringBuilder(64);
            for (int i = 0; i < Parts.Count; i++)
            {
                SharedCityPart part = Parts[i];
                string text = m.Part(part.Id);
                if (string.IsNullOrEmpty(text))
                {
                    Release(part);
                    continue;
                }
                part.PendingChanges = null;
                ApplyWhole(part, text);
                names.Append(names.Length > 0 ? ", " : string.Empty).Append(part.Name).Append(part.Following ? string.Empty : " (when it starts here)");
            }
            Logger?.Warn("Shared city: this guest follows the host's city" + (test ? " (the test's recording)" : string.Empty) + ": " + (names.Length > 0 ? names.ToString() : "nothing (the host runs none of it)")
                         + ". None of it is stepped here; the host's comes every few seconds.");
            if (!_toldShared)
            {
                _toldShared = true;
                Notifications.Show(NotificationType.Info, "reallife_gp_city_shared", null, 16f);
            }
            Meta.OptionsLayout.Refresh();
        }

        private static void Apply(SharedMessage m)
        {
            for (int i = 0; i < m.Parts.Count; i++)
            {
                char id = m.Parts[i].Id;
                bool change = char.IsLower(id);
                SharedCityPart part = Find(change ? char.ToUpperInvariant(id) : id);
                if (part == null)
                {
                    continue;
                }
                string text = m.Parts[i].Text;
                if (!change)
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        Release(part);   // the host has stopped running it
                    }
                    else
                    {
                        part.PendingChanges = null;
                        ApplyWhole(part, text);
                    }
                    continue;
                }
                if (part.Pending != null)
                {
                    StringBuilder later = part.PendingChanges ?? (part.PendingChanges = new StringBuilder(256));
                    if (later.Length + text.Length > ChangesMost)
                    {
                        part.PendingChanges = null;
                        AskAgain(part);   // too much has come while it waited: it is asked for again, whole, once it is ready
                    }
                    else
                    {
                        later.Append(text);
                    }
                    continue;
                }
                if (part.Following && !Safe(part, text, false))
                {
                    AskAgain(part);
                }
            }
        }

        // A part, whole: applied now if its system is ready here, else kept until it is. True when applied.
        private static bool ApplyWhole(SharedCityPart part, string text)
        {
            if (part.Ready == null || !part.Ready())
            {
                part.Pending = text;
                return false;
            }
            part.Pending = null;
            if (!Safe(part, text, true))
            {
                Logger?.Warn("Shared city: the host's " + part.Name + " could not be applied here; this player's own goes on.");
                part.Following = false;
                return false;
            }
            part.Following = true;
            if (part.PendingChanges != null)
            {
                string later = part.PendingChanges.ToString();
                part.PendingChanges = null;
                if (later.Length > 0 && !Safe(part, later, false))
                {
                    AskAgain(part);
                }
            }
            return true;
        }

        private static bool Safe(SharedCityPart part, string text, bool whole)
        {
            try
            {
                return part.Apply != null && part.Apply(text, whole);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>A guest: a part came out different from the host's, so it is asked for whole again, at most every two minutes.</summary>
        public static void AskAgain(char id)
        {
            SharedCityPart part = Find(id);
            if (part != null)
            {
                AskAgain(part);
            }
        }

        private static void AskAgain(SharedCityPart part)
        {
            float now = Time.unscaledTime;
            if (!_following || _testFollow || now < _nextResync)
            {
                return;
            }
            _nextResync = now + ResyncRest;
            SharedMessage m = New(SharedKind.Resync);
            m.Parts.Add(new SharedPart(part.Id, string.Empty));
            Send(null, m);
            Logger?.Info("Shared city: the host's " + part.Name + " is asked for again.");
        }

        private static void Release(SharedCityPart part)
        {
            bool was = part.Following;
            part.Following = false;
            part.Pending = null;
            part.PendingChanges = null;
            if (was)
            {
                try
                {
                    part.Left?.Invoke();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        // The host's city is not followed any more: each system goes on from the host's last state, which is its own now.
        private static void Stop(string why)
        {
            _following = false;
            _testFollow = false;
            _lastSeen = 0;
            for (int i = 0; i < Parts.Count; i++)
            {
                Release(Parts[i]);
            }
            for (int c = 0; c < Asked.Length; c++)
            {
                if (Asked[c] != null)
                {
                    Asked[c].Length = 0;
                }
            }
            _anyAsked = false;
            Logger?.Warn("Shared city: the host's city is no longer followed (" + why + "). This player's city goes on from the host's last state, which is now theirs.");
            Notifications.Show(NotificationType.Info, "reallife_gp_city_left", null, 14f);
            Meta.OptionsLayout.Refresh();
        }

        /// <summary>A new city is loading: nothing of the last one's following or guests is kept.</summary>
        public static void Reset()
        {
            if (_following)
            {
                _following = false;
                _testFollow = false;
                for (int i = 0; i < Parts.Count; i++)
                {
                    Parts[i].Following = false;
                }
            }
            for (int i = 0; i < Parts.Count; i++)
            {
                Parts[i].Pending = null;
                Parts[i].PendingChanges = null;
                Parts[i].Dirty = false;
            }
            Guests.Clear();
            Private.Clear();
            _anyPrivate = false;
            _refused = false;
            _helloTries = 0;
            _lastSeen = 0;
            _nextHello = 0f;
        }

        private static string Describe(SharedMessage m)
        {
            var t = new StringBuilder(48);
            for (int i = 0; i < m.Parts.Count; i++)
            {
                t.Append(i > 0 ? ", " : string.Empty).Append(m.Parts[i].Id).Append(' ').Append(m.Parts[i].Text.Length).Append(" chars");
            }
            return t.Length > 0 ? t.ToString() : "nothing";
        }

        /// <summary>The guide's and the options page's word for whose city this is now.</summary>
        public static string Words()
        {
            if (_following)
            {
                return "reallife_gp_city_words_following".GetLocalization();
            }
            if (Leading)
            {
                return "reallife_gp_city_words_leading".GetLocalization().Replace("{count}", Guests.Count.ToString());
            }
            if (GoingPublic.InSession && !GoingPublicLink.Ready)
            {
                return "reallife_gp_city_words_nolink".GetLocalization();
            }
            return "reallife_gp_city_words_own".GetLocalization();
        }

        /// <summary>The options page's button: whose city it is, and what of it is shared.</summary>
        public static void Tell()
        {
            try
            {
                Notifications.Show(NotificationType.Info, "reallife_gp_city_tell", new Dictionary<string, string> { { "city", Words() } }, 20f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: the city as a host would send it to a guest now, kept for "apply". Any role.</summary>
        public static void TestRecord()
        {
            try
            {
                if (!ModState.Loaded || SaveGameManager.Current == null)
                {
                    TestRefusals.Tell(Logger, "Record the city as a host", TestRefusal.NotLoaded);
                    return;
                }
                if (_following)
                {
                    TestRefusals.Tell(Logger, "Record the city as a host", TestRefusal.NothingFits, 0, "this machine follows a host's city; record as the host or alone");
                    return;
                }
                SharedMessage m = Snapshot();
                _recorded = SharedCityWire.Write(m);
                if (_recorded == null)
                {
                    Logger?.Warn("Shared city test: the city is too large to send.");
                    return;
                }
                SharedMessage back;
                SharedRead read = SharedCityWire.Read(_recorded, Version, out back);
                Logger?.Info("Shared city test: recorded " + Describe(m) + ", " + _recorded.Length + " bytes; read back: " + read + ": " + (read == SharedRead.Ok && back.Parts.Count == m.Parts.Count ? "PASS" : "FAIL"));
                Notifications.Show(NotificationType.Info, "reallife_gp_test_recorded", new Dictionary<string, string> { { "size", (_recorded.Length / 1024f).ToString("0.0") } }, 10f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Test: this machine pretends to be a guest (<see cref="GoingPublic.TestPretend"/>) and the recording is applied
        /// as a host's snapshot through a guest's own path; nothing of it steps until the pretence ends.
        /// </summary>
        public static void TestApply()
        {
            try
            {
                if (!ModState.Loaded || SaveGameManager.Current == null)
                {
                    TestRefusals.Tell(Logger, "Apply the recorded city as a guest", TestRefusal.NotLoaded);
                    return;
                }
                if (_recorded == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_gp_test_none", null, 9f);
                    return;
                }
                if (GoingPublic.InSession && !GoingPublic.Pretending)
                {
                    TestRefusals.Tell(Logger, "Apply the recorded city as a guest", TestRefusal.NothingFits, 0, "a real session is on");
                    return;
                }
                GoingPublic.Pretend(GoingPublicRole.Guest);
                _testFollow = true;
                _helloTries = HelloTries;   // no hello: there is no host to say it to
                SharedMessage m;
                SharedRead read = SharedCityWire.Read(_recorded, Version, out m);
                if (read != SharedRead.Ok || m.Kind != SharedKind.Snapshot)
                {
                    Logger?.Warn("Shared city test: the recording did not read (" + read + "): FAIL");
                    return;
                }
                Follow(m, true);
                Notifications.Show(NotificationType.Info, "reallife_gp_test_applied", null, 12f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
