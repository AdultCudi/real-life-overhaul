using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using Streets;
using Streets.Pedestrians;
using UI.Guiders;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The law's one component: loads and saves its part of the savegame, designates the
    /// precincts and keeps the one the player is near dressed, lets the player into a station
    /// and furnishes it while they are inside, runs the front desk, runs the daily step of the
    /// reputation, lets a passer-by say thanks now and then, watches over a stay in the cell,
    /// and owns the test buttons. Everything is on one tick a second (real time, so it also
    /// runs while the game is paused for a time skip) and a look four times a second at whether
    /// the player is at a station's door or desk; between those it is two time comparisons and
    /// three flags per frame, and the interaction key only while its prompt is on screen.
    /// </summary>
    public sealed class LawDirector : MonoBehaviour
    {
        private const float TickInterval = 1f;
        private const float ThanksChance = 1f / 75f;     // per second on foot near people, for somebody well liked, once a day at most
        private const float ThanksWithin = 6f;

        private static readonly string[] ThanksLines = { "reallife_law_thanks_1", "reallife_law_thanks_2", "reallife_law_thanks_3", "reallife_law_thanks_4" };

        public static IModLogger Logger;
        public static LawDirector Instance { get; private set; }

        private const float DoorWithin = 5f;         // metres from a precinct's door at which the interaction key is the station's
        private const float DeskWithin = 2.3f;

        private enum Use { None, Desk, Door, Window }

        private Precincts _precincts;
        private Custody _custody;
        private StationInterior _interior;
        private StationDesk _desk;
        private Action<Address> _onEnter;
        private Action<Address> _onExit;
        private bool _entered;               // the game has just put the player inside a building (its event; acted on in Update)
        private bool _exited;
        private Use _use;                    // what the interaction key does where the player stands
        private Precinct _usePrecinct;
        private float _nextLook;
        private bool _mapOffered = true;
        private Precinct _asked;             // the precinct whose door the game was asked to take the player through
        private float _askedAt;
        private float _nextTick;
        private int _faults;
        private int _streetFaults;
        private int _day = -1;
        private int _level = -1;
        private bool _resumed;
        private int _lastThanks = -1;
        private Action _onSave;
        private int _hookIn;

        /// <summary>How many precincts the city has (0 until they have been designated).</summary>
        public static int PrecinctCount => Instance != null && Instance._precincts != null ? Instance._precincts.Count : 0;

        /// <summary>The precinct nearest a point, or null. For other features: where patrols come from, where somebody is taken.</summary>
        public static Precinct NearestPrecinct(Vector3 at)
        {
            return Instance != null && Instance._precincts != null ? Instance._precincts.Nearest(at) : null;
        }

        /// <summary>True while the interaction key is the station's (its prompt is on screen): World/NearbyInteraction leaves the key alone.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._use != Use.None || DeskPanel.IsOpen);

        /// <summary>The precinct the player is standing inside, or null.</summary>
        public static Precinct Inside => Instance != null && Instance._interior != null && Instance._interior.Dressed ? Instance._interior.Precinct : null;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSaveGame;   // one delegate for the session: it is taken off and put back on the game's event every second
            _onEnter = _ => _entered = true;
            _onExit = _ => _exited = true;
            LawState.Logger = Logger;
            Custody.Logger = Logger;
            LawState.Reset();
            _precincts = new Precincts(transform, Logger);
            _interior = new StationInterior(transform, Logger);
            _desk = new StationDesk(transform, _precincts, Logger);
            _custody = gameObject.AddComponent<Custody>();
            _custody.Bind(_precincts, _interior);
        }

        private void OnDestroy()
        {
            GlobalEvents.onSaveGame -= _onSave;
            GlobalEvents.onEnterBuilding -= _onEnter;
            GlobalEvents.onExitBuilding -= _onExit;
            try
            {
                _desk.Dispose();
                _interior.Dispose();
                _precincts.Dispose();
                StationProps.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            LawState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            try
            {
                if (_entered || _exited)
                {
                    Moved();
                }
                if (DeskPanel.IsOpen)
                {
                    _desk.Frame();
                }
                else if (_use != Use.None && JobKeys.InteractDown())
                {
                    Act();
                }
                if (Time.unscaledTime >= _nextLook)
                {
                    _nextLook = Time.unscaledTime + 0.25f;
                    Look();
                }
                if (Time.unscaledTime < _nextTick)
                {
                    return;
                }
                _nextTick = Time.unscaledTime + TickInterval;
                Tick();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                try
                {
                    Idle();
                    _desk.Close();
                    _interior.End();
                    _precincts.Undress();
                    _custody.ForceRelease("a fault in the law's tick");
                }
                catch (Exception inner)
                {
                    Logger?.Error(inner);
                }
                if (++_faults >= 3)
                {
                    Logger?.Warn("Law: switched off for this session after repeated errors (see above). Fines and stops go on as before; nobody is held.");
                    _custody.enabled = false;   // (Custody.Ready is then false: every route falls back to what it did before)
                    enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            // Switched off after faults: nothing of ours stays on the street, in a room or on the screen.
            if (_precincts != null)
            {
                _precincts.Undress();
            }
            if (_desk != null)
            {
                Idle();
                _desk.Close();
            }
            if (_interior != null)
            {
                _interior.End();
            }
        }

        // ---------------------------------------------------------------- the station: the door, the room, the desk

        // The game has just taken the player into a building or out of one (its own events, raised with the screen dark).
        private void Moved()
        {
            bool entered = _entered;
            _entered = false;
            _exited = false;
            Idle();
            _desk.Close();
            if (!_precincts.Chosen || !LawState.Loaded)
            {
                return;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                Precinct precinct = Settings.LawPrecincts ? _precincts.Of(InstanceBehavior<BuildingManager>.Instance.building) : null;
                if (entered && precinct != null && Settings.LawStation)
                {
                    if (precinct == _asked)
                    {
                        _asked = null;
                    }
                    _precincts.PeopleIn();
                    _interior.Begin(precinct);
                }
                else if (_interior.Dressed && precinct != _interior.Precinct)
                {
                    _interior.End();
                }
                return;
            }
            Precinct left = _interior.Precinct;
            _interior.End();
            if (left != null && !Custody.Active)
            {
                _precincts.DressNow(left);   // (the screen is still dark: the frontage is all there when it clears)
            }
            _desk.OnStreet();
        }

        // Four times a second: what the interaction key would do where the player stands, and the line that says so.
        private void Look()
        {
            if (!LawState.Loaded || !ModState.Loaded || SaveGameManager.Current == null)
            {
                if (_use != Use.None)
                {
                    Idle();
                }
                return;
            }
            Use use = Use.None;
            Precinct precinct = null;
            PlayerController player = PlayerHelper.PlayerController;
            if (LawState.Loaded && _precincts.Count > 0 && Settings.LawPrecincts && player != null && !DeskPanel.IsOpen && !Custody.Active && !PlayerHelper.IsUsingVehicle
                && !player.NavigationDisabled && !JobDirector.TakesInteractKey && !JobDirector.OnShift && !ThirdPersonCamera.IsUiOpen() && SaveGameManager.Current != null)
            {
                Vector3 at = player.transform.position;
                if (BuildingManager.IsInsideBuilding)
                {
                    if (_interior.Dressed && JobSpots.Flat(at, _interior.ServiceSpot) < DeskWithin * DeskWithin)
                    {
                        use = Use.Desk;
                        precinct = _interior.Precinct;
                    }
                }
                else
                {
                    Precinct near = _precincts.Nearest(at);
                    if (near != null && JobSpots.Flat(at, near.Door) < DoorWithin * DoorWithin && Mathf.Abs(at.y - near.Door.y) < 4f)
                    {
                        precinct = near;
                        use = Precincts.CanEnter(near) ? Use.Door : Use.Window;
                    }
                }
            }
            _use = use;
            _usePrecinct = precinct;
            if (use == Use.None)
            {
                _desk.Prompt(null, null);
            }
            else
            {
                _desk.Prompt(use == Use.Desk ? "reallife_law_station_prompt_desk" : use == Use.Door ? "reallife_law_station_prompt_door" : "reallife_law_station_prompt_window", precinct.Name);
            }
        }

        private void Idle()
        {
            _use = Use.None;
            _usePrecinct = null;
            _desk.Prompt(null, null);
        }

        // The interaction key, with the prompt on screen.
        private void Act()
        {
            Use use = _use;
            Precinct precinct = _usePrecinct;
            Idle();
            if (precinct == null)
            {
                return;
            }
            if (use == Use.Door)
            {
                if (!EnterStation(precinct))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_law_station_no_entry", null, 6f);
                }
            }
            else
            {
                _desk.Open(precinct, use == Use.Desk);
            }
        }

        // The game was asked to take the player into a station seven seconds ago and has not: its way in does not work for
        // this unit. Whatever it left half done is put right (its "entering" flag, the dark screen), and the precinct is
        // one whose front desk answers at the door for the rest of the session.
        private void NotLetIn(BuildingManager buildings, Precinct within)
        {
            Precinct asked = _asked;
            _asked = null;
            if (within == asked && buildings != null && !buildings.enteringBuilding)
            {
                return;   // they are in (the door's own event was missed)
            }
            asked.WalkIn = false;
            Logger?.Warn("Law: the game did not take the player into " + asked.Name + " (see any error above); its front desk answers at the door from now on.");
            if (buildings != null && buildings.enteringBuilding)
            {
                buildings.enteringBuilding = false;
                StartCoroutine(UI.UiFader.UnFade(0.2f));
                if (BuildingManager.IsInsideBuilding)
                {
                    buildings.ExitFromBuilding(0, false);
                }
            }
            if (SaveGameManager.Current != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_station_no_entry", null, 8f);
            }
        }

        /// <summary>
        /// Takes the player into a precinct that can be walked into, through its own door, by the game's own way into a
        /// building (its fade, its interior for that unit, its camera and light). False if that cannot be done now.
        /// </summary>
        internal bool EnterStation(Precinct precinct)
        {
            BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
            if (!Precincts.CanEnter(precinct) || buildings == null || buildings.enteringBuilding || buildings.exitingBuilding || BuildingManager.IsInsideBuilding
                || PlayerHelper.PlayerController == null || PlayerHelper.IsUsingVehicle)
            {
                return false;
            }
            Buildings.Building building = precinct.Building.building;
            if (BuildingManager.IsBuildingBlockedByAnyService(building.Address))
            {
                return false;
            }
            // By the door nearest the player, as the game's own entrance does.
            int doorId = -1;
            float best = float.MaxValue;
            Vector3 at = PlayerHelper.GetPosition();
            foreach (Buildings.Outdoors.BuildingEntranceDoor door in precinct.Building.entranceDoors)
            {
                if (door == null || door.doorTransform == null)
                {
                    continue;
                }
                float sqr = (door.doorTransform.position - at).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    doorId = door.doorId;
                }
            }
            _asked = precinct;
            _askedAt = Time.unscaledTime;
            Logger?.Info("Law: going into " + precinct.Name + ".");
            return buildings.EnterBuilding(building, false, false, 0, doorId);
        }

        private void Tick()
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                return;
            }
            if (!LawState.Loaded)
            {
                LawState.Load();
                _day = TimeHelper.CurrentDay;
                Reputation.NewDay(_day);
                _level = Reputation.ScrutinyLevel;
                _resumed = false;
                RecordPhone.Setup(_precincts.Icon);
            }

            // The game clears its event hub on every load, so the save hook is kept attached (as Life/LifeEvents does): every few seconds.
            if (--_hookIn <= 0)
            {
                _hookIn = 3;
                GlobalEvents.onSaveGame -= _onSave;
                GlobalEvents.onSaveGame += _onSave;
                GlobalEvents.onEnterBuilding -= _onEnter;
                GlobalEvents.onEnterBuilding += _onEnter;
                GlobalEvents.onExitBuilding -= _onExit;
                GlobalEvents.onExitBuilding += _onExit;
            }

            bool outdoors = !BuildingManager.IsInsideBuilding;

            // The room: furnished here too if the door's own event was missed (a save loaded inside one), cleared if the
            // player is no longer in it, and its people kept at their places.
            if (_precincts.Chosen)
            {
                BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
                Precinct within = !outdoors && buildings != null && Settings.LawPrecincts && Settings.LawStation ? _precincts.Of(buildings.building) : null;
                if (_interior.Dressed && within != _interior.Precinct)
                {
                    _interior.End();
                }
                if (within != null && !_interior.Dressed && !buildings.enteringBuilding && !buildings.exitingBuilding)
                {
                    _precincts.PeopleIn();
                    _interior.Begin(within);
                }
                _interior.Tick(Time.time);
                _precincts.Keep(within);
                if (_asked != null && Time.unscaledTime - _askedAt > 7f)
                {
                    NotLetIn(buildings, within);
                }
                bool offered = Settings.LawPrecincts && Settings.LawMapMarkers;
                if (offered != _mapOffered)
                {
                    _mapOffered = offered;
                    MapPlaces.Refresh();
                }
            }
            if (!_precincts.Chosen && outdoors && !_precincts.Choose())
            {
                return;
            }

            // A stay in the cell: resumed after a load; ended if it has stopped making progress or the save and the routine disagree.
            if (_precincts.Chosen && LawState.Custody.Active && !Custody.Active)
            {
                if (!_resumed)
                {
                    _resumed = true;
                    _custody.Resume();
                }
                else
                {
                    _custody.ForceRelease("the save says held, nothing is running");
                }
            }
            else if (_custody.Stalled)
            {
                _custody.ForceRelease("no progress");
            }

            int day = TimeHelper.CurrentDay;
            if (day != _day)
            {
                _day = day;
                Reputation.NewDay(day);
            }
            if (Reputation.ScrutinyLevel != _level)
            {
                if (Settings.LawReputation && !Custody.Active)
                {
                    if (Reputation.ScrutinyLevel > _level)
                    {
                        Notifications.Show(NotificationType.Warning, Reputation.NoWarnings ? "reallife_law_scrutiny_high" : "reallife_law_scrutiny_up", null, 10f);
                    }
                    else if (Reputation.ScrutinyLevel == 0)
                    {
                        Notifications.Show(NotificationType.Success, "reallife_law_scrutiny_off", null, 9f);
                    }
                    _level = Reputation.ScrutinyLevel;
                }
                else if (!Settings.LawReputation)
                {
                    _level = Reputation.ScrutinyLevel;
                }
            }

            // The street: a fault out here takes the frontage in, and after three leaves it in; it never ends a stay in the cell.
            if (_streetFaults >= 3)
            {
                return;
            }
            try
            {
                if (!Settings.LawPrecincts)
                {
                    _precincts.Undress();
                }
                else if (outdoors)
                {
                    _precincts.Tick(Time.time, PlayerHelper.GetPosition());
                }

                if (Settings.LawReputation && outdoors && Reputation.Liked && Reputation.ThanksDay != day && !Custody.Active
                    && !PlayerHelper.IsUsingVehicle && Random.value < ThanksChance && LifeEvents.IsSafeForEvent())
                {
                    Thanks(day);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _precincts.Undress();
                if (++_streetFaults >= 3)
                {
                    Logger?.Warn("Law: the precincts' frontage is off for this session after repeated errors (see above). Custody and the record go on.");
                }
            }
        }

        private void OnSaveGame()
        {
            try
            {
                LawState.Save();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Somebody walking by, close enough to be the one who speaks: a smile over their head and a line; for
        // somebody the city really likes, a few dollars pressed into the hand as well. Nobody near: nothing is said.
        private void Thanks(int day)
        {
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            ActorPool pool = ActorPool.Instance;
            if (walkers == null || pool == null)
            {
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            ThirdPersonCharacter who = null;
            float best = ThanksWithin * ThanksWithin;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || walker.tpc == null || !walker.tpc.visible || !walker.gameObject.activeInHierarchy || RagdollSystem.Holds(walker.tpc))
                {
                    continue;
                }
                Vector3 offset = walker.transform.position - player;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < best && Mathf.Abs(offset.y) < 2f)
                {
                    best = sqr;
                    who = walker.tpc;
                }
            }
            if (who == null)
            {
                return;
            }
            Reputation.ThanksDay = day;
            pool.ShowEmoji(who, CharacterEmojiName.CustomerGoodService, 3f);
            int line = Random.Range(0, ThanksLines.Length);
            if (line == _lastThanks)
            {
                line = (line + 1) % ThanksLines.Length;
            }
            _lastThanks = line;
            if (Reputation.Public >= Reputation.LovedFrom)
            {
                int tip = Random.Range(1, 5) * 5;
                Crime.Give(tip, "reallife_transaction_thanks");
                Crime.ShowAmount(NotificationType.Success, "reallife_law_thanks_tip", tip, 8f);
            }
            else
            {
                Notifications.Show(NotificationType.Success, ThanksLines[line], null, 7f);
            }
            Logger?.Info($"Law: a passer-by thanked the player (public reputation {Reputation.Public}).");
        }

        // ---------------------------------------------------------------- the nearest precinct, shown

        /// <summary>
        /// Points the game's own destination marker at the precinct nearest the player (its name and
        /// the distance show on it, and it can be dismissed like any destination). Null if there is none.
        /// </summary>
        internal Precinct GuideToNearest()
        {
            Precinct precinct = _precincts.Nearest(PlayerHelper.GetCityPosition());
            if (precinct == null || InstanceBehavior<GuidersManager>.Instance == null)
            {
                return precinct;
            }
            GuidersManager.SetGuiderTarget(precinct.Door + Vector3.up * 4.5f, precinct.Name, _precincts.Icon, new Color(0.10f, 0.28f, 0.78f), DirectionGuiderType.Destination);
            return precinct;
        }

        // ---------------------------------------------------------------- test buttons

        private TestRefusal Refusal(bool needsCustody)
        {
            if (!ModState.Loaded || !LawState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (!enabled || (needsCustody ? !Settings.LawCustody : !Settings.LawPrecincts))
            {
                return TestRefusal.FeatureOff;
            }
            if (_precincts.Count == 0)
            {
                return TestRefusal.NoPrecinct;
            }
            if (!needsCustody)
            {
                return TestRefusal.None;
            }
            if (Custody.Active || PoliceAttention.IsBusy)
            {
                return TestRefusal.PoliceBusy;
            }
            // Not over a time skip or a conversation. (A menu is no reason: the button is in one. The arrest waits for it to close.)
            var uis = InstanceBehavior<UI.UIs>.Instance;
            return (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning) || DialogController.current != null
                ? TestRefusal.ScreenBusy : TestRefusal.None;
        }

        /// <summary>Options-menu test button: the whole arrest from where the player stands. 0 minor, 1 serious, 2 fatal.</summary>
        public void TestArrest(int kind)
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: true);
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Arrest", why, 0, _precincts.Count + " precinct(s)");
                    return;
                }
                int charges = kind <= 0 ? LawBook.Bit(Charge.FledStop)
                    : kind == 1 ? LawBook.Bit(Charge.HitAndRun) | LawBook.Bit(Charge.InjuryFled)
                    : LawBook.Bit(Charge.DeathFled);
                // No money is taken by the test: in play the fines are charged by whoever stops the player, before this is called.
                if (!Custody.Arrest(charges, 0, null))
                {
                    TestRefusals.Tell(Logger, "Arrest", TestRefusal.PoliceBusy);
                    return;
                }
                Notifications.Show(NotificationType.Info, "reallife_law_test_fine", null, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Options-menu test button: marks the nearest precinct and says how far it is.</summary>
        public void TestNearest()
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: false);
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Nearest precinct", why);
                    return;
                }
                Precinct precinct = GuideToNearest();
                if (precinct == null)
                {
                    TestRefusals.Tell(Logger, "Nearest precinct", TestRefusal.NoPrecinct);
                    return;
                }
                Vector3 offset = precinct.Door - PlayerHelper.GetCityPosition();
                offset.y = 0f;
                var data = new Dictionary<string, string>(3)
                {
                    { "precinct", precinct.Name },
                    { "metres", Mathf.RoundToInt(offset.magnitude).ToString() },
                    { "address", precinct.Building.building.Address.ToFormattedString() }
                };
                Notifications.Show(NotificationType.Info, "reallife_law_nearest", data, 12f);
                Logger?.Info($"Law: nearest precinct is {precinct.Name} at {precinct.Building.building.Address.ToFormattedString()}, {offset.magnitude:F0} m.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Options-menu test button: opens the front desk of the nearest precinct wherever the player stands.</summary>
        public void TestDesk()
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: false);
                if (why == TestRefusal.None && (Custody.Active || DialogController.current != null))
                {
                    why = Custody.Active ? TestRefusal.PoliceBusy : TestRefusal.ScreenBusy;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Front desk", why);
                    return;
                }
                Precinct precinct = Inside ?? _precincts.Nearest(PlayerHelper.GetCityPosition());
                if (precinct == null)
                {
                    TestRefusals.Tell(Logger, "Front desk", TestRefusal.NoPrecinct);
                    return;
                }
                Idle();
                _desk.Open(precinct, Inside == precinct);
                Notifications.Show(NotificationType.Info, "reallife_law_test_desk_done", null, 6f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Options-menu test button: takes the nearest precinct's frontage in and puts it out again at once, and says in the log what fitted and what did not.</summary>
        public void TestDress()
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: false);
                if (why == TestRefusal.None && BuildingManager.IsInsideBuilding)
                {
                    why = TestRefusal.Indoors;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Dress the precinct", why);
                    return;
                }
                Vector3 at = PlayerHelper.GetPosition();
                Precinct precinct = _precincts.Nearest(at);
                if (precinct == null)
                {
                    TestRefusals.Tell(Logger, "Dress the precinct", TestRefusal.NoPrecinct);
                    return;
                }
                float metres = Mathf.Sqrt(JobSpots.Flat(at, precinct.Door));
                if (metres > 250f)
                {
                    TestRefusals.TellKey(Logger, "Dress the precinct", "reallife_law_test_dress_far", $"{precinct.Name} is {metres:F0} m away");
                    return;
                }
                _precincts.Undress();
                _precincts.DressNow(precinct);
                string report = _precincts.Report();
                Logger?.Info("Law: dressed again by the test button: " + report + ". " + _interior.Report() + ".");
                var data = new Dictionary<string, string>(1) { { "precinct", precinct.Name } };
                Notifications.Show(NotificationType.Info, "reallife_law_test_dress_done", data, 10f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Options-menu test button: wipes the record and puts both reputations back where a new save starts.</summary>
        public void TestClearRecord()
        {
            if (!LawState.Loaded)
            {
                TestRefusals.Tell(Logger, "Clear record", TestRefusal.NotLoaded);
                return;
            }
            if (Custody.Active)
            {
                TestRefusals.Tell(Logger, "Clear record", TestRefusal.PoliceBusy);
                return;
            }
            Record.Clear();
            Reputation.Set(Reputation.Start, Reputation.Start);
            _level = Reputation.ScrutinyLevel;
            Notifications.Show(NotificationType.Success, "reallife_law_record_cleared", null, 7f);
            Logger?.Info("Law: record cleared by the test button.");
        }
    }

    /// <summary>
    /// The ways into a cell, one call each for whoever catches the player. Each returns true if
    /// the law has taken it from there (the caller then keeps its patrol car where it is while
    /// <see cref="Custody.OnScene"/> is true, and does nothing more), false if the caller should
    /// do what it did before. The fines are the caller's: it charges them once and passes what
    /// they came to.
    /// </summary>
    public static class Arrests
    {
        /// <summary>Caught after driving on from a traffic stop (Crime/PoliceAttention).</summary>
        /// <param name="crashed">They hit a car on the way.</param>
        public static bool FledStop(bool crashed, int hurt, int killed, int paid, VehicleComponent patrol)
        {
            int charges = LawBook.Bit(Charge.FledStop)
                          | (crashed ? LawBook.Bit(Charge.HitAndRun) : 0)
                          | (hurt > 0 ? LawBook.Bit(Charge.InjuryFled) : 0)
                          | (killed > 0 ? LawBook.Bit(Charge.DeathFled) : 0);
            return Custody.Arrest(charges, paid, patrol);
        }

        /// <summary>Caught after driving off from a crash (Traffic/PolicePursuit).</summary>
        /// <param name="crashed">A car was hit (with nobody hurt and no car hit there is nothing to hold them for but this).</param>
        public static bool HitAndRun(bool crashed, int hurt, int killed, int paid, VehicleComponent patrol)
        {
            int charges = (killed > 0 ? LawBook.Bit(Charge.DeathFled) : 0) | (hurt > 0 ? LawBook.Bit(Charge.InjuryFled) : 0);
            if (charges == 0 || crashed)
            {
                charges |= LawBook.Bit(Charge.HitAndRun);
            }
            return Custody.Arrest(charges, paid, patrol);
        }

        /// <summary>
        /// They stopped and waited after hitting somebody. An injury is the fine it always was; a
        /// death is an arrest on the spot.
        /// </summary>
        public static bool StayedAtScene(int killed, int paid, VehicleComponent patrol)
        {
            return killed > 0 && Settings.LawArrests && Custody.Arrest(LawBook.Bit(Charge.DeathStayed), paid, patrol);
        }

        /// <summary>One more 911 call about nothing today (Crime/CrimeDirector): from the fifth, a few hours.</summary>
        public static bool WastedCall(int callsToday, int paid)
        {
            return callsToday >= LawBook.WastedCallsForCustody && Settings.LawArrests && Custody.Arrest(LawBook.Bit(Charge.WastedCalls), paid, null);
        }

        /// <summary>
        /// They drove off after a crash and were not caught. No cell; but their car is known for the
        /// rest of the day (the flag Crime/PoliceAttention already keeps), and if somebody was hurt
        /// it goes on the record as wanted.
        /// </summary>
        public static void Escaped(int hurt, int killed)
        {
            if (!Settings.LawArrests || !LawState.Loaded)
            {
                return;
            }
            PoliceAttention.KnowCar();
            if (hurt + killed > 0)
            {
                Record.Add(LawBook.Bit(Charge.Wanted), 0, 0, Outcome.Escaped);
                // What they ran from, for the day they walk into a station and hand themselves in (StationDesk).
                LawState.WantedCharges = LawBook.Bit(killed > 0 ? Charge.DeathFled : Charge.InjuryFled);
                LawState.WantedDay = TimeHelper.CurrentDay;
            }
            if (SaveGameManager.Current != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_wanted", null, 10f);
            }
        }

        /// <summary>A ticket at the roadside (Crime/PoliceAttention): no cell, a line on the record.</summary>
        public static void Cited(int fine)
        {
            if (LawState.Loaded)
            {
                Record.Add(LawBook.Bit(Charge.Citation), 0, fine, Outcome.Cited);
            }
        }
    }
}
