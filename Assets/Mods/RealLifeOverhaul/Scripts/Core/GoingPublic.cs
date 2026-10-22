using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Where this machine stands with Going Public, the multiplayer mod.</summary>
    public enum GoingPublicRole
    {
        Absent,      // not switched on
        Alone,       // switched on, no session: the game as it is alone
        Host,        // hosting a session: the world others see is this machine's
        Guest        // joined someone's session: the clock, the weather, the market and the traffic are the host's
    }

    /// <summary>
    /// Living with Going Public (Workshop 3765662670), the mod that lets several players share one city; see
    /// RealLifeOverhaul.Build/docs/other-mods.md, "Going Public". In a session the clock is everyone's, so nothing of
    /// this mod's pauses, skips or speeds it (<see cref="ClockShared"/>); another player's buildings are on this machine
    /// as rented by the player (<see cref="Mine"/>); a guest's traffic is the host's (<see cref="TrafficMirrored"/>);
    /// the mod's city economy is the host's, through Core/SharedCity.
    /// It is known by its assembly (<see cref="OtherMods"/>), then through delegates made once by reflection over
    /// MPServer.IsRunning, MPClient.IsClientInWorld and GameStatePatcher.HideFromOwnAssetLists; a missing one means not
    /// there, a fault is logged once and the mod goes on as if alone.
    /// COST: one comparison a frame (<see cref="Tick"/>); two property reads of the other mod's a real second;
    /// <see cref="Role"/> and all built on it are field reads.
    /// </summary>
    public static class GoingPublic
    {
        private const float Every = 1f;   // real seconds between looks at the other mod's session
        private const string Name = "Going Public";

        public static IModLogger Logger;

        private static bool _resolved;
        private static Func<bool> _hosting;
        private static Func<bool> _inWorld;
        private static Func<BuildingRegistration, bool> _hidden;
        private static Func<List<Transform>> _remotes;   // the other players' figures on this machine
        private static bool _remotesFailed;
        private static bool _readFailed;
        private static bool _ownerFailed;
        private static GoingPublicRole _role = GoingPublicRole.Absent;
        private static GoingPublicRole _pretend = GoingPublicRole.Absent;   // the test's: Host or Guest, Absent while no test
        private static float _next;
        private static bool _told;   // the notice has been shown in this run of the game

        /// <summary>Where this machine stands now. A field read.</summary>
        public static GoingPublicRole Role => _role;

        /// <summary>True while a session is on, hosted here or joined.</summary>
        public static bool InSession => _role == GoingPublicRole.Host || _role == GoingPublicRole.Guest;

        public static bool IsHost => _role == GoingPublicRole.Host;

        public static bool IsGuest => _role == GoingPublicRole.Guest;

        /// <summary>
        /// True while the clock is the session's: nothing of this mod's pauses, skips or speeds it, and what would pass
        /// time alone passes none.
        /// </summary>
        public static bool ClockShared => InSession;

        /// <summary>True while the traffic here is the host's: nothing of it is taken, stopped or steered.</summary>
        public static bool TrafficMirrored => _role == GoingPublicRole.Guest;

        /// <summary>
        /// True for a building rented by the player that is not another player's (Going Public puts those on this machine
        /// as rented by the player). Outside a session exactly RentedByPlayer.
        /// </summary>
        public static bool Mine(BuildingRegistration reg)
        {
            return reg != null && reg.RentedByPlayer && !Foreign(reg);
        }

        /// <summary>True for another player's business or home in a session, by Going Public's own test where it can be read.</summary>
        public static bool Foreign(BuildingRegistration reg)
        {
            if (reg == null || !reg.RentedByPlayer || !InSession || _pretend != GoingPublicRole.Absent)
            {
                return false;
            }
            if (_hidden != null && !_ownerFailed)
            {
                try
                {
                    return _hidden(reg);
                }
                catch (Exception e)
                {
                    _ownerFailed = true;
                    Logger?.Warn("Other mods: Going Public's test of whose a building is failed (" + e.GetType().Name + ": " + e.Message
                                 + "); a building with another player's name on it is taken as theirs from now on.");
                }
            }
            return !string.IsNullOrEmpty(reg.businessOwnerRivalId);   // the game clears that name on whatever the player rents (RivalsHelper)
        }

        /// <summary>
        /// True for a vehicle of the player's own: in a session only if the save has it, as Going Public lists another
        /// player's car among the player's but not in the save. Decided by <see cref="WhoseCar.Mine"/>. Not per frame.
        /// </summary>
        public static bool MineVehicle(VehicleController vehicle)
        {
            bool has = vehicle != null && vehicle.vehicleInstance != null;
            bool sessionRules = InSession && _pretend == GoingPublicRole.Absent;
            if (!has || !sessionRules)
            {
                return WhoseCar.Mine(has, sessionRules, false, false, false);
            }
            GameInstance save = SaveGameManager.Current;
            bool own = save != null && save.VehicleInstances != null && save.VehicleInstances.Contains(vehicle.vehicleInstance);
            bool driver = !own && save != null && save.privateDriverVehicleInstances != null && save.privateDriverVehicleInstances.Contains(vehicle.vehicleInstance);
            return WhoseCar.Mine(true, true, save == null, own, driver);
        }

        /// <summary>A city is loading: the session is read at once, so every feature starts knowing it.</summary>
        public static void Look()
        {
            Resolve();
            _next = 0f;
            Read();
            Logger?.Info("Other mods check: " + Describe());
        }

        /// <summary>Once a frame from <see cref="GoingPublicWatch"/>: a look once a real second.</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + Every;
            Read();
            SharedCity.Second();
            if (!_told && InSession && ModState.Loaded)
            {
                _told = true;   // once a run of the game, not on every load
                Tell();
            }
        }

        // Nothing found leaves the delegates empty: the mod is then taken as not there.
        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }
            OtherMods.Look(Logger);
            if (!OtherMods.GoingPublic.Loaded)
            {
                return;   // looked for again on the next city: one walk
            }
            _resolved = true;
            try
            {
                Assembly assembly = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (OtherModName.Is(a.GetName().Name, OtherMods.GoingPublic.AssemblyPrefix) && a.GetType(OtherMods.GoingPublic.KnownType, false) != null)
                    {
                        assembly = a;
                        break;
                    }
                }
                Type server = assembly != null ? assembly.GetType("BigAmbitionsMP.MPServer", false) : null;
                Type client = assembly != null ? assembly.GetType("BigAmbitionsMP.MPClient", false) : null;
                Type patcher = assembly != null ? assembly.GetType("BigAmbitionsMP.GameStatePatcher", false) : null;
                const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                MethodInfo hosting = server?.GetProperty("IsRunning", Static)?.GetGetMethod(true);
                MethodInfo inWorld = client?.GetProperty("IsClientInWorld", Static)?.GetGetMethod(true);
                MethodInfo hidden = patcher?.GetMethod("HideFromOwnAssetLists", Static, null, new[] { typeof(BuildingRegistration) }, null);
                Type players = assembly != null ? assembly.GetType("BigAmbitionsMP.RemotePlayerManager", false) : null;
                MethodInfo remotes = players?.GetMethod("GetRemotePlayerTransforms", Static, null, Type.EmptyTypes, null);
                if (remotes != null && remotes.ReturnType == typeof(List<Transform>))
                {
                    _remotes = (Func<List<Transform>>)Delegate.CreateDelegate(typeof(Func<List<Transform>>), remotes);
                }
                if (hosting != null && hosting.ReturnType == typeof(bool) && inWorld != null && inWorld.ReturnType == typeof(bool))
                {
                    _hosting = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), hosting);
                    _inWorld = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), inWorld);
                }
                GoingPublicLink.Install(assembly);   // the shared city's messages
                if (hidden != null && hidden.ReturnType == typeof(bool))
                {
                    _hidden = (Func<BuildingRegistration, bool>)Delegate.CreateDelegate(typeof(Func<BuildingRegistration, bool>), hidden);
                }
                if (_hosting == null)
                {
                    Logger?.Warn("Other mods: Going Public is switched on but whether a session is on cannot be read (a newer version of it?); this mod acts as if alone.");
                }
                else if (_hidden == null)
                {
                    Logger?.Warn("Other mods: Going Public's test of whose a building is was not found; a building with another player's name on it is taken as theirs.");
                }
            }
            catch (Exception e)
            {
                _hosting = null;
                _inWorld = null;
                Logger?.Warn("Other mods: Going Public could not be read (" + e.GetType().Name + ": " + e.Message + "); this mod acts as if alone.");
            }
        }

        private static void Read()
        {
            GoingPublicRole now = GoingPublicRole.Absent;
            if (_pretend != GoingPublicRole.Absent)
            {
                now = _pretend;
            }
            else if (OtherMods.GoingPublic.Loaded)
            {
                now = GoingPublicRole.Alone;
                if (_hosting != null && !_readFailed)
                {
                    try
                    {
                        now = _hosting() ? GoingPublicRole.Host : _inWorld() ? GoingPublicRole.Guest : GoingPublicRole.Alone;
                    }
                    catch (Exception e)
                    {
                        _readFailed = true;
                        now = GoingPublicRole.Alone;
                        Logger?.Warn("Other mods: reading Going Public's session failed (" + e.GetType().Name + ": " + e.Message + "); this mod acts as if alone from now on.");
                    }
                }
            }
            if (now == _role)
            {
                return;
            }
            GoingPublicRole was = _role;
            _role = now;
            Changed(was, now);
        }

        private static void Changed(GoingPublicRole was, GoingPublicRole now)
        {
            try
            {
                string pretend = _pretend != GoingPublicRole.Absent ? " (the test's pretence)" : string.Empty;
                if (now == GoingPublicRole.Host)
                {
                    Logger?.Warn("Going Public: this machine hosts a session" + pretend + ". The clock is the session's: nothing of this mod pauses, skips or speeds it "
                                 + "(custody, prison, rides, outings and the family's occasions pass no time; photo mode does not pause; holidays and hotel nights "
                                 + "are not offered). Selling a business or a building and hiring out cars wait until the game is played alone. Other players' "
                                 + "businesses and homes are not this player's. Street life, scenes and the HUD are this machine's own. The city's economy, government and stock "
                                 + "market are this machine's, and every guest that runs this mod in the same version follows them (Core/SharedCity).");
                }
                else if (now == GoingPublicRole.Guest)
                {
                    Logger?.Warn("Going Public: this machine is a guest in a session" + pretend + ". As for the host, the clock is the session's and nothing of this "
                                 + "mod pauses, skips or speeds it; and the traffic is the host's: accidents, chases, patrols and everything else that takes cars "
                                 + "out of the traffic are left to the host, and franchised shops open in this player's books only. The city's economy, government and "
                                 + "stock market are the host's once it answers (Core/SharedCity).");
                    if (ModState.Loaded)
                    {
                        // Mid-play: whatever of the traffic this mod holds is given back before the host's figures arrive.
                        Meta.Features.Find("streets")?.CleanupAction?.Invoke();
                        Meta.Features.Find("traffic")?.CleanupAction?.Invoke();
                    }
                }
                else if (was == GoingPublicRole.Host || was == GoingPublicRole.Guest)
                {
                    Logger?.Warn("Going Public: the session is over" + pretend + "; this mod is as it is alone again.");
                }
                else if (now == GoingPublicRole.Alone)
                {
                    Logger?.Info("Going Public: switched on, no session; this mod is as it is alone.");
                }
                Meta.OptionsLayout.Refresh();   // the rows that do nothing in a session say so, or no longer
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The notice of what is different in this session: once a run by itself, again from the options page.</summary>
        public static void Tell()
        {
            try
            {
                string key = IsGuest ? "reallife_gp_toast_guest" : IsHost ? "reallife_gp_toast_host" : OtherMods.GoingPublic.Loaded ? "reallife_gp_toast_alone" : "reallife_gp_toast_absent";
                Notifications.Show(NotificationType.Info, key, null, 20f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>For the rows of what takes cars out of the traffic: why they do nothing just now, or null.</summary>
        public static string TrafficSentence()
        {
            return TrafficMirrored ? "reallife_gp_aside_traffic".GetLocalization() : null;
        }

        /// <summary>For the rows of what would pass time of its own: why they do nothing just now, or null.</summary>
        public static string ClockSentence()
        {
            return ClockShared ? "reallife_gp_aside_clock".GetLocalization() : null;
        }

        /// <summary>For the rows of what changes who owns what, past Going Public's own ways: why they do nothing just now, or null.</summary>
        public static string OwnershipSentence()
        {
            return InSession ? "reallife_gp_aside_ownership".GetLocalization() : null;
        }

        /// <summary>For Permadeath's row: why it does nothing just now (another player's world is never ended), or null.</summary>
        public static string PermadeathSentence()
        {
            return InSession ? "reallife_gp_aside_permadeath".GetLocalization() : null;
        }

        /// <summary>The guide's word for where this machine stands.</summary>
        public static string Words()
        {
            switch (_role)
            {
                case GoingPublicRole.Host: return "reallife_gp_role_host".GetLocalization();
                case GoingPublicRole.Guest: return "reallife_gp_role_guest".GetLocalization();
                case GoingPublicRole.Alone: return "reallife_gp_role_alone".GetLocalization();
                default: return "reallife_gp_role_absent".GetLocalization();
            }
        }

        /// <summary>
        /// "Test: pretend a Going Public session": each press steps through hosting, joined and none. No building is
        /// taken as another player's during the test.
        /// </summary>
        public static void TestPretend()
        {
            try
            {
                if (!ModState.Loaded || SaveGameManager.Current == null)
                {
                    TestRefusals.Tell(Logger, "Pretend a Going Public session", TestRefusal.NotLoaded);
                    return;
                }
                _pretend = _pretend == GoingPublicRole.Absent ? GoingPublicRole.Host : _pretend == GoingPublicRole.Host ? GoingPublicRole.Guest : GoingPublicRole.Absent;
                _told = false;   // the test shows the notice too
                Logger?.Info("Going Public test: " + (_pretend == GoingPublicRole.Absent ? "the pretence is over." : "this machine is taken to " + (_pretend == GoingPublicRole.Host ? "host" : "be a guest in") + " a session."));
                _next = 0f;
                Read();
                if (_pretend == GoingPublicRole.Absent)
                {
                    Notifications.Show(NotificationType.Info, "reallife_gp_test_off", null, 8f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Adds the other players' figures on this machine, from Going Public's own list: a local read, nothing sent or
        /// changed. Not per frame: the other mod makes a new list each call.
        /// </summary>
        public static void RemotePlayers(List<Transform> into)
        {
            if (into == null || !InSession || _pretend != GoingPublicRole.Absent || _remotes == null || _remotesFailed)
            {
                return;
            }
            try
            {
                List<Transform> found = _remotes();
                for (int i = 0; found != null && i < found.Count; i++)
                {
                    if (found[i] != null)
                    {
                        into.Add(found[i]);
                    }
                }
            }
            catch (Exception e)
            {
                _remotesFailed = true;
                Logger?.Warn("Other mods: Going Public's list of the other players could not be read (" + e.GetType().Name + ": " + e.Message
                             + "); their hair keeps the game's look until the next door or loading screen.");
            }
        }

        /// <summary>A new city: the test's pretence ends.</summary>
        public static void Reset()
        {
            _pretend = GoingPublicRole.Absent;
            SharedCity.Reset();
        }

        public static bool Pretending => _pretend != GoingPublicRole.Absent;

        /// <summary>For the shared city's test: sets the pretence at once (Absent ends it).</summary>
        public static void Pretend(GoingPublicRole role)
        {
            if (role == GoingPublicRole.Alone)
            {
                role = GoingPublicRole.Absent;
            }
            if (role == _pretend)
            {
                return;
            }
            _pretend = role;
            _told = false;
            _next = 0f;
            Read();
        }

        /// <summary>For the guide and the self-check: one line on where this machine stands.</summary>
        public static string Describe()
        {
            return "Going Public: " + (_role == GoingPublicRole.Absent ? "not switched on" : _role == GoingPublicRole.Alone ? "switched on, no session"
                       : _role == GoingPublicRole.Host ? "hosting a session" : "a guest in a session")
                   + (_pretend != GoingPublicRole.Absent ? " (pretended)" : string.Empty) + ".";
        }
    }

    /// <summary>The per-frame call of <see cref="GoingPublic"/>. COST: a clock comparison a frame, a look a second, a field read for the shared city.</summary>
    public sealed class GoingPublicWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("multiplayer", "GoingPublicWatch", typeof(GoingPublicWatch));

        private static readonly int HotUpdate = HotSpots.Slot("Core: GoingPublicWatch.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            GoingPublic.Tick();
            SharedCity.Frame();   // a field read while nothing has come
        }
    }
}
