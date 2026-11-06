using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// This mod's own messages through Going Public's connection, the way its add-ons send theirs (Friend Trade,
    /// message 249; Extras, 250). Going Public has no call to register a message, so a Harmony prefix on its receivers
    /// (MPServer.OnReceive on the host, MPClient.OnReceive on a guest) takes this mod's frames and returns false for
    /// them; sends go in Going Public's own envelope (MPClient.SendEnvelope to the host, MPLink.Send to a guest), on its
    /// reliable, ordered channel. A frame is an envelope of type <see cref="SharedCityWire.MessageType"/> with the message
    /// as its attachment (the binary frame "BZA"), told apart by its first six bytes.
    /// Going Public and its Harmony are found by reflection, never referenced; a missing piece or a taken type number
    /// leaves the link off with one log line, and each player's city goes on alone.
    /// THREADS: the receivers run on the transport's thread and only queue the bytes under a lock; <see cref="Drain"/>
    /// hands them out on the game's thread. COST: one field read a frame while nothing has come.
    /// </summary>
    public static class GoingPublicLink
    {
        private const string HarmonyId = "reallifeoverhaul.sharedcity";
        private const int QueueMost = 64;   // messages kept waiting; more are dropped and counted

        public static IModLogger Logger;

        private struct Inbound
        {
            public object Peer;    // the host's MPLink of the guest it came from; null on a guest
            public byte[] Bytes;
        }

        private static readonly object Gate = new object();
        private static readonly Queue<Inbound> Inbox = new Queue<Inbound>(8);
        private static volatile bool _waiting;
        private static bool _tried;

        private static Type _envelope, _typeEnum;
        private static PropertyInfo _envType, _envSender, _envData, _envAttachment;
        private static MethodInfo _toHost, _linkSend;
        private static PropertyInfo _linkId, _linkAlive;
        private static Func<bool> _connected;
        private static PropertyInfo _playerId;
        private static FieldInfo _peerNames;
        private static int _dropped;

        public static bool Ready { get; private set; }

        /// <summary>Why the link is not in place, for the log and the self-check; null while it is.</summary>
        public static string Why { get; private set; } = "Going Public is not switched on";

        /// <summary>
        /// This machine's player id in the session, also the owner Going Public writes on a player's business
        /// (businessOwnerRivalId); empty without the link. A reflected read: never per frame.
        /// </summary>
        public static string MyId
        {
            get
            {
                try
                {
                    return Ready && _playerId != null ? _playerId.GetValue(null) as string ?? string.Empty : string.Empty;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>True on a guest while its connection to the host is up.</summary>
        public static bool Connected
        {
            get
            {
                try
                {
                    return Ready && _connected != null && _connected();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>True while something waits for <see cref="Drain"/>. A field read.</summary>
        public static bool Waiting => _waiting;

        /// <summary>Puts the link in place, once, with Going Public's assembly. Logs why not if it cannot.</summary>
        public static void Install(Assembly gp)
        {
            if (_tried || gp == null)
            {
                return;
            }
            _tried = true;
            try
            {
                const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                Type server = gp.GetType("BigAmbitionsMP.MPServer", false);
                Type client = gp.GetType("BigAmbitionsMP.MPClient", false);
                Type link = gp.GetType("BigAmbitionsMP.MPLink", false);
                Type config = gp.GetType("BigAmbitionsMP.MPConfig", false);
                _envelope = gp.GetType("BigAmbitionsMP.MessageEnvelope", false);
                _typeEnum = gp.GetType("BigAmbitionsMP.MessageType", false);
                if (server == null || client == null || link == null || config == null || _envelope == null || _typeEnum == null || !_typeEnum.IsEnum)
                {
                    Off("Going Public's network classes were not found (a newer version of it?)");
                    return;
                }
                foreach (object value in Enum.GetValues(_typeEnum))
                {
                    if (Convert.ToInt32(value) == SharedCityWire.MessageType)
                    {
                        Off("Going Public uses message type " + SharedCityWire.MessageType + " for its own " + value + " now");
                        return;
                    }
                }
                _envType = _envelope.GetProperty("Type", Instance);
                _envSender = _envelope.GetProperty("SenderId", Instance);
                _envData = _envelope.GetProperty("Data", Instance);
                _envAttachment = _envelope.GetProperty("Attachment", Instance);
                _toHost = client.GetMethod("SendEnvelope", Static, null, new[] { _envelope }, null);
                _linkSend = link.GetMethod("Send", Instance, null, new[] { _envelope }, null);
                _linkId = link.GetProperty("Id", Instance);
                _linkAlive = link.GetProperty("IsAlive", Instance);
                _playerId = config.GetProperty("PlayerId", Static);
                _peerNames = server.GetField("_peerNames", Static);
                MethodInfo connected = client.GetProperty("IsConnected", Static)?.GetGetMethod(true);
                MethodInfo hostReceive = server.GetMethod("OnReceive", Static, null, new[] { link, typeof(byte[]) }, null);
                MethodInfo guestReceive = client.GetMethod("OnReceive", Static, null, new[] { typeof(byte[]) }, null);
                if (_envType == null || _envSender == null || _envData == null || _envAttachment == null || _envAttachment.PropertyType != typeof(byte[]) || _toHost == null
                    || _linkSend == null || _linkId == null || _linkAlive == null || _playerId == null || _peerNames == null || !typeof(IDictionary).IsAssignableFrom(_peerNames.FieldType)
                    || connected == null || connected.ReturnType != typeof(bool) || hostReceive == null || guestReceive == null)
                {
                    Off("a part of Going Public's messaging was not found (a newer version of it?)");
                    return;
                }
                ParameterInfo[] hostArgs = hostReceive.GetParameters(), guestArgs = guestReceive.GetParameters();
                if (hostArgs[0].Name != "peer" || hostArgs[1].Name != "bytes" || guestArgs[0].Name != "bytes")
                {
                    Off("Going Public's receivers have other parameters than this mod knows");
                    return;
                }
                _connected = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), connected);

                // Harmony: Going Public's own copy, found by name.
                Type harmony = null, harmonyMethod = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type h = a.GetType("HarmonyLib.Harmony", false);
                    if (h != null)
                    {
                        harmony = h;
                        harmonyMethod = a.GetType("HarmonyLib.HarmonyMethod", false);
                        break;
                    }
                }
                ConstructorInfo wrap = harmonyMethod?.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo patch = harmony?.GetMethod("Patch", new[] { typeof(MethodBase), harmonyMethod, harmonyMethod, harmonyMethod, harmonyMethod });
                if (harmony == null || wrap == null || patch == null)
                {
                    Off("Harmony, which Going Public brings, was not found");
                    return;
                }
                object instance = Activator.CreateInstance(harmony, HarmonyId);
                MethodInfo hostPrefix = typeof(GoingPublicLink).GetMethod(nameof(HostReceive), BindingFlags.Public | BindingFlags.Static);
                MethodInfo guestPrefix = typeof(GoingPublicLink).GetMethod(nameof(GuestReceive), BindingFlags.Public | BindingFlags.Static);
                patch.Invoke(instance, new[] { hostReceive, wrap.Invoke(new object[] { hostPrefix }), null, null, null });
                patch.Invoke(instance, new[] { guestReceive, wrap.Invoke(new object[] { guestPrefix }), null, null, null });
                Ready = true;
                Why = null;
                Logger?.Info("Other mods: Going Public's messaging is in place for the shared city (message type " + SharedCityWire.MessageType + ", on its reliable channel).");
            }
            catch (Exception e)
            {
                Off("putting it in place failed (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private static void Off(string why)
        {
            Ready = false;
            Why = why;
            Logger?.Warn("Other mods: the shared city cannot use Going Public's connection: " + why + ". In a session each player's city goes on as their own.");
        }

        /// <summary>Harmony prefix on MPServer.OnReceive, on Going Public's thread: takes this mod's frames, passes the rest on.</summary>
        public static bool HostReceive(object peer, byte[] bytes)
        {
            if (!Ours(bytes))
            {
                return true;
            }
            Take(peer, bytes);
            return false;
        }

        /// <summary>Harmony prefix on MPClient.OnReceive.</summary>
        public static bool GuestReceive(byte[] bytes)
        {
            if (!Ours(bytes))
            {
                return true;
            }
            Take(null, bytes);
            return false;
        }

        // Going Public's attachment frame ("BZA"), with this mod's type in its two type bytes.
        private static bool Ours(byte[] b)
        {
            return b != null && b.Length > 10 && b[0] == 2 && b[1] == 66 && b[2] == 90 && b[3] == 65 && b[4] == SharedCityWire.MessageType && b[5] == 0;
        }

        private static void Take(object peer, byte[] frame)
        {
            try
            {
                int json = frame[6] | (frame[7] << 8) | (frame[8] << 16) | (frame[9] << 24);
                int length = frame.Length - 10 - json;
                if (json <= 0 || length <= 0 || length > SharedCityWire.MostPacked)
                {
                    return;
                }
                var bytes = new byte[length];
                Buffer.BlockCopy(frame, 10 + json, bytes, 0, length);
                lock (Gate)
                {
                    if (Inbox.Count >= QueueMost)
                    {
                        _dropped++;
                        return;
                    }
                    Inbox.Enqueue(new Inbound { Peer = peer, Bytes = bytes });
                    _waiting = true;
                }
            }
            catch (Exception)
            {
                // a frame that is not what it says is dropped, never troubling Going Public's thread
            }
        }

        /// <summary>On the game's thread: hands everything that has come to the handler, in order.</summary>
        public static void Drain(Action<object, byte[]> handle)
        {
            while (true)
            {
                Inbound next;
                lock (Gate)
                {
                    if (Inbox.Count == 0)
                    {
                        _waiting = false;
                        if (_dropped > 0)
                        {
                            Logger?.Warn("Shared city: " + _dropped + " message(s) dropped: more came at once than are kept waiting.");
                            _dropped = 0;
                        }
                        return;
                    }
                    next = Inbox.Dequeue();
                }
                try
                {
                    handle(next.Peer, next.Bytes);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private static object Envelope(byte[] message)
        {
            object env = Activator.CreateInstance(_envelope);
            _envType.SetValue(env, Enum.ToObject(_typeEnum, SharedCityWire.MessageType));
            _envSender.SetValue(env, _playerId.GetValue(null) as string ?? string.Empty);
            _envData.SetValue(env, string.Empty);
            _envAttachment.SetValue(env, message);
            return env;
        }

        /// <summary>A guest's message to the host; false if it could not go.</summary>
        public static bool ToHost(byte[] message)
        {
            if (!Ready || message == null)
            {
                return false;
            }
            try
            {
                return _toHost.Invoke(null, new[] { Envelope(message) }) is bool sent && sent;
            }
            catch (Exception e)
            {
                Logger?.Warn("Shared city: a message to the host could not be sent (" + e.GetType().Name + ": " + e.Message + ").");
                return false;
            }
        }

        /// <summary>The host's message to one guest, by Going Public's link to it.</summary>
        public static bool ToGuest(object peer, byte[] message)
        {
            if (!Ready || peer == null || message == null || !Alive(peer))
            {
                return false;
            }
            try
            {
                _linkSend.Invoke(peer, new[] { Envelope(message) });
                return true;
            }
            catch (Exception e)
            {
                Logger?.Warn("Shared city: a message to " + PlayerOf(peer) + " could not be sent (" + e.GetType().Name + ": " + e.Message + ").");
                return false;
            }
        }

        public static bool Alive(object peer)
        {
            try
            {
                return peer != null && _linkAlive != null && _linkAlive.GetValue(peer) is bool alive && alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The player id a host's link belongs to; empty for a link that has not said hello to Going Public.</summary>
        public static string PlayerOf(object peer)
        {
            try
            {
                if (peer == null || !(_peerNames?.GetValue(null) is IDictionary names))
                {
                    return string.Empty;
                }
                object id = _linkId.GetValue(peer);
                return id != null && names.Contains(id) ? names[id] as string ?? string.Empty : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
