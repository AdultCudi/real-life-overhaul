using System;
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
    /// Living with Going Public (Steam Workshop 3765662670), the mod that lets several players share one city. See
    /// RealLifeOverhaul.Build/docs/other-mods.md, "Going Public", for what it does and why each feature of this mod
    /// does what it does in a session.
    ///
    /// What matters here, read in its decompiled code:
    ///  - every player plays their own save (their money, their businesses, this mod's state in it), loaded and kept
    ///    on the host's machine, and the shared world is laid over it: the clock, the weather, the market, who rents
    ///    and owns what, and near the host the traffic, all as the host has them;
    ///  - the clock is everyone's. In a session it stops the game's time skip as soon as anything starts it
    ///    (TimeMachine.StartTimeMachine, and TimeMachine.Update never runs), refuses the game's pause and its lock on
    ///    the speed controls (GameSpeedController.SetPause, SetPauseState, DisableTimeControl), and puts back any jump
    ///    of the clock it did not make. Time passes faster only by a vote of all the players, from an activity the
    ///    game's own panel shows (a bed, a bench, the mod's own shift skip). So nothing of this mod's may pause, skip or
    ///    speed the clock in a session: <see cref="ClockShared"/>;
    ///  - another player's business or home is on this machine as rented by the player (RentedByPlayer) with that
    ///    player's id as its owner (businessOwnerRivalId), and Going Public keeps it out of the game's own lists of
    ///    the player's assets. This mod does the same through <see cref="Mine"/>, with Going Public's own test
    ///    (GameStatePatcher.HideFromOwnAssetLists) where it can be read;
    ///  - a guest's traffic near the host is the host's cars, placed from the host's figures (TrafficManager.Update is
    ///    not run there): nothing may be taken out of it, stopped or steered. <see cref="TrafficMirrored"/>.
    ///
    /// How it is known, without a reference to it: by its assembly and a type of its own (<see cref="OtherMods"/>),
    /// then by three of its statics, read by reflection once and called through delegates made then:
    /// MPServer.IsRunning (hosting), MPClient.IsClientInWorld (in someone's world) and
    /// GameStatePatcher.HideFromOwnAssetLists. A missing one means it is taken as not there; a fault in one is
    /// logged once and the mod goes on as if alone.
    ///
    /// The cost: nothing per frame but one comparison with the real clock (<see cref="Tick"/>); once a second of real
    /// time, two property reads of the other mod's. <see cref="Role"/> and everything built on it are a field read.
    /// The notice and the log lines are made on a change only.
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
        /// True while the clock belongs to the session: nothing of this mod's pauses it, skips it or speeds it. What
        /// would have passed time alone passes none (Going Public does the same with a taxi ride).
        /// </summary>
        public static bool ClockShared => InSession;

        /// <summary>True while the traffic here is the host's: nothing of it is taken, stopped or steered (Traffic/TrafficTools.Ready).</summary>
        public static bool TrafficMirrored => _role == GoingPublicRole.Guest;

        /// <summary>
        /// True for a building rented by the player that is the player's own: not another player's business or home,
        /// which Going Public puts on this machine as rented by the player. Outside a session exactly RentedByPlayer.
        /// </summary>
        public static bool Mine(BuildingRegistration reg)
        {
            return reg != null && reg.RentedByPlayer && !Foreign(reg);
        }

        /// <summary>True for another player's business or home, in a session. Going Public's own test where it can be read.</summary>
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
        /// True for a vehicle of the player's own. Going Public shows another player's car on this machine as a car of the
        /// game's player's list (VehicleHelper.AllPlayerVehicles) but takes it off the save's own list
        /// (GameInstance.VehicleInstances), so in a session a car is the player's only if the save has it. For loops over a
        /// handful of cars, not per frame. Outside a session: any car of the list with an entry.
        /// </summary>
        public static bool MineVehicle(VehicleController vehicle)
        {
            if (vehicle == null || vehicle.vehicleInstance == null)
            {
                return false;
            }
            if (!InSession || _pretend != GoingPublicRole.Absent)
            {
                return true;
            }
            GameInstance save = SaveGameManager.Current;
            return save == null || (save.VehicleInstances != null && save.VehicleInstances.Contains(vehicle.vehicleInstance))
                   || (save.privateDriverVehicleInstances != null && save.privateDriverVehicleInstances.Contains(vehicle.vehicleInstance));
        }

        /// <summary>A city is loading: the session is read at once, so that every feature starts knowing it.</summary>
        public static void Look()
        {
            Resolve();
            _next = 0f;
            Read();
            Logger?.Info("Other mods check: " + Describe());   // the self-check's line, with Developer diagnostics on
        }

        /// <summary>Once a frame, from <see cref="GoingPublicWatch"/>: a look once a second of real time.</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + Every;
            Read();
            SharedCity.Second();   // the shared city's second: hello, updates, what a guest asked for (Core/SharedCity)
            if (!_told && InSession && ModState.Loaded)
            {
                _told = true;   // once in a run of the game, not on every load: the first look after the save is in
                Tell();
            }
        }

        // The three statics, found once. Nothing found leaves the delegates empty: the mod is then taken as not there.
        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }
            OtherMods.Look(Logger);
            if (!OtherMods.GoingPublic.Loaded)
            {
                return;   // looked for again on the next city: it cannot be switched on without the game starting again, but this costs one walk
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
                if (hosting != null && hosting.ReturnType == typeof(bool) && inWorld != null && inWorld.ReturnType == typeof(bool))
                {
                    _hosting = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), hosting);
                    _inWorld = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), inWorld);
                }
                GoingPublicLink.Install(assembly);   // this mod's own messages through Going Public's connection, for the shared city
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
                                 + "businesses and homes are not this player's. Street life, scenes and the HUD are this machine's own.");
                }
                else if (now == GoingPublicRole.Guest)
                {
                    Logger?.Warn("Going Public: this machine is a guest in a session" + pretend + ". As for the host, the clock is the session's and nothing of this "
                                 + "mod pauses, skips or speeds it; and the traffic is the host's: accidents, chases, patrols and everything else that takes cars "
                                 + "out of the traffic are left to the host, and franchised shops open in this player's books only.");
                    if (ModState.Loaded)
                    {
                        // In the middle of play: whatever of the traffic's this mod holds is given back now, before the next figures of
                        // the host's arrive. (At a city's load nothing is held yet.)
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

        /// <summary>The notice that says what is different in this session. Shown by itself once a run; again from the options page.</summary>
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

        // ---------------------------------------------------------------- the options page

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
        /// "Test: pretend a Going Public session": each press goes one step on, a session hosted here, a session joined,
        /// none, so that everything a session changes can be seen without the other mod or a second player. Another
        /// player's buildings cannot be pretended: none is taken as theirs during the test.
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

        /// <summary>A new city: the test's pretence ends with it. What the player was told this run stays told.</summary>
        public static void Reset()
        {
            _pretend = GoingPublicRole.Absent;
            SharedCity.Reset();
        }

        /// <summary>True while the test pretends a session.</summary>
        public static bool Pretending => _pretend != GoingPublicRole.Absent;

        /// <summary>For the shared city's test: the pretence is set to a role at once (Absent ends it).</summary>
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
            return "Going Public: " + (_role == GoingPublicRole.Absent ? "not switched on" : _role == GoingPublicRole.Alone ? "switched on, no session" : _role == GoingPublicRole.Host ? "hosting a session" : "a guest in a session")
                   + (_pretend != GoingPublicRole.Absent ? " (pretended)" : string.Empty) + ".";
        }
    }

    /// <summary>The one per-frame call of <see cref="GoingPublic"/>: a comparison with the real clock and a look once a second, and the shared city's inbox (a field read).</summary>
    public sealed class GoingPublicWatch : MonoBehaviour
    {
        private void Update()
        {
            GoingPublic.Tick();
            SharedCity.Frame();   // (a field read while nothing of the shared city's has come)
        }
    }
}
