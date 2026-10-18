using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UI.Guiders;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The law's one component: the save, the precincts, the station's door, room and desk, the reputation's daily step and
    /// the watch over a stay in the cell. Cost: a tick a second on the real clock and a look four times a second at the door
    /// or desk; between them two time comparisons, three flags and a reference a frame.
    /// </summary>
    public sealed partial class LawDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("law", "LawDirector", typeof(LawDirector));

        private const float TickInterval = 1f;       // seconds, real time
        private const float OwedEvery = 0.25f;        // seconds between looks for a dark screen while the station is owed its furniture
        private const float ThanksChance = 1f / 75f;   // chance a second on foot near people, for somebody well liked; once a day at most
        private const float ThanksWithin = 6f;       // metres

        private static readonly string[] ThanksLines = { "reallife_law_thanks_1", "reallife_law_thanks_2", "reallife_law_thanks_3", "reallife_law_thanks_4" };

        public static IModLogger Logger;
        public static LawDirector Instance { get; private set; }

        private enum Use { None, Desk, Door, Window }

        private Precincts _precincts;
        private Custody _custody;
        private StationInterior _interior;
        private StationDesk _desk;
        private DoorPanel _door;
        private Action _onSpeak;
        private Action _onGo;
        private Action _onQuickReport;
        private Action _onQuickFines;
        private Action _onQuickCar;
        private Action _onQuickPaper;
        private Func<string> _doorWhat;
        private int _doorKey = -1;
        private Action<Address> _onEnter;
        private Action<Address> _onExit;
        private bool _entered;               // the game's own event; acted on in Update
        private bool _exited;
        private Use _use;                    // what the interaction key does where the player stands
        private Precinct _usePrecinct;
        private float _nextLook;
        private bool _mapOffered = true;
        private Precinct _asked;             // the precinct whose door the game was asked to take the player through
        private float _askedAt;
        private float _nextTick;
        private Precinct _roomOwed;          // the precinct the player is in, its station not furnished: done behind the next dark screen
        private float _nextOwed;
        private int _faults;
        private int _streetFaults;
        private int _day = -1;
        private int _level = -1;
        private bool _resumed;
        private int _lastThanks = -1;
        private Action _onSave;
        private Action<Address> _enterSeen;   // the game's two building events as last hooked
        private Action<Address> _exitSeen;

        /// <summary>True once the precincts are chosen for this city; features that take a building choose after.</summary>
        public static bool PrecinctsChosen => Instance != null && Instance._precincts != null && Instance._precincts.Chosen;

        /// <summary>The precinct nearest a point, or null before the city is ready: where patrols come from, where somebody is taken.</summary>
        public static Precinct NearestPrecinct(Vector3 at)
        {
            return Instance != null && Instance._precincts != null ? Instance._precincts.Nearest(at) : null;
        }

        /// <summary>True while the station's prompt is on screen: World/NearbyInteraction leaves the key alone.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._door.TakesKey || Instance._desk.IsOpen);

        /// <summary>The precinct the player is standing inside, or null.</summary>
        public static Precinct Inside => Instance != null && Instance._interior != null && Instance._interior.Dressed ? Instance._interior.Precinct : null;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSaveGame;
            _onEnter = _ => _entered = true;
            _onExit = _ => _exited = true;
            LawState.Logger = Logger;
            Custody.Logger = Logger;
            LawState.Reset();
            _precincts = new Precincts(transform, Logger);
            _interior = new StationInterior(transform, Logger);
            _desk = new StationDesk(Logger);
            WorldPanel.Logger = Logger;
            _door = new DoorPanel(Logger, "Law check (desk panel)");
            _onSpeak = _door.Press(Speak);
            _onGo = _door.Press(GoIn);
            _onQuickReport = _door.Press(() => Quick(StationDesk.Service.Report));
            _onQuickFines = _door.Press(() => Quick(StationDesk.Service.Fines));
            _onQuickCar = _door.Press(() => Quick(StationDesk.Service.Impound));
            _onQuickPaper = _door.Press(() => Quick(StationDesk.Service.Paper));
            _doorWhat = () => _usePrecinct != null ? _usePrecinct.Name + (_use == Use.Door ? " (door)" : _use == Use.Desk ? " (desk)" : " (front desk at the door)") : "a station";
            _custody = gameObject.AddComponent<Custody>();
            _custody.Bind(_precincts, _interior);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onEnterBuilding -= _onEnter;
            GlobalEvents.onExitBuilding -= _onExit;
            try
            {
                _door.Hide("the director went with the city");
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

        private static readonly int HotUpdate = HotSpots.Slot("Law: LawDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            try
            {
                if (_entered || _exited)
                {
                    Moved();
                }
                if (_roomOwed != null && Time.unscaledTime >= _nextOwed)
                {
                    _nextOwed = Time.unscaledTime + OwedEvery;
                    FurnishOwed();
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
                bool lacking = Guard.StandsDown(e, "the police precincts, custody and the record");   // the fault would come back every tick
                if (lacking || ++_faults >= 3)
                {
                    if (!lacking)
                    {
                        Logger?.Warn("Law: switched off for this session after repeated errors (see above). Fines and stops go on as before; nobody is held.");
                    }
                    _custody.enabled = false;   // Custody.Ready is then false: every route does what it did before
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

        // The game has just taken the player into or out of a building (its own events, raised with the screen dark).
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
                    Furnish(precinct);
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
                _precincts.DressNow(left);   // the screen is still dark: the frontage is up when it clears
            }
            _desk.OnStreet();
        }

        // The station furnished, in one pass (StationInterior.Begin): a long frame, so only where it is not seen. With
        // Developer diagnostics on, its time and where it was paid.
        private void Furnish(Precinct precinct)
        {
            long began = Stopwatch.GetTimestamp();
            bool hidden = SceneFinds.Hidden;
            _roomOwed = null;
            _precincts.PeopleIn();
            _interior.Begin(precinct);
            if (Meta.Dev.Verbose && _interior.Dressed)
            {
                double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                Logger?.Info("Law check (furnish): " + precinct.Name + " furnished in " + ms.ToString("F0", CultureInfo.InvariantCulture) + " ms "
                             + (hidden ? "behind a dark screen or a pause: PASS" : "with the game in view: FAIL, a long frame the player sees"));
            }
        }

        // The door's event was missed (a save loaded inside) or the floor was not there yet: the station is furnished once a
        // long frame is not seen, behind the loading screen, the game's black screen or a pause; never in play.
        private void FurnishOwed()
        {
            Precinct owed = _roomOwed;
            if (!SceneFinds.Hidden)
            {
                return;
            }
            if (_interior.Dressed || !BuildingManager.IsInsideBuilding)
            {
                _roomOwed = null;
                return;
            }
            Furnish(owed);
        }

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
            if (_precincts.Count > 0 && Settings.LawPrecincts && player != null && !_desk.IsOpen && !Custody.Active && !PlayerHelper.IsUsingVehicle
                && !player.NavigationDisabled && !JobDirector.TakesInteractKey && !JobDirector.OnShift && !ThirdPersonCamera.IsUiOpen())
            {
                Vector3 at = player.transform.position;
                if (BuildingManager.IsInsideBuilding)
                {
                    if (_interior.Dressed && StationReach.AtDesk(JobSpots.Flat(at, _interior.ServiceSpot)))
                    {
                        use = Use.Desk;
                        precinct = _interior.Precinct;
                    }
                }
                else
                {
                    Precinct near = _precincts.Nearest(at);
                    if (near != null && AtDoor(at, near))
                    {
                        precinct = near;
                        use = Precincts.CanEnter(near) ? Use.Door : Use.Window;
                    }
                }
            }
            _use = use;
            _usePrecinct = precinct;
            _desk.Watch();
            if (use == Use.None)
            {
                _door.Hide(_desk.IsOpen ? "the desk's list is open" : "the player walked away or another screen is open");
                _doorKey = -1;
            }
            else
            {
                bool plain = use != Use.Door && _desk.Plain;
                int key = ((int)use) | (plain ? 8 : 0) | (precinct.Index << 4) | (Wanted() ? 1 << 12 : 0);
                if (key != _doorKey)
                {
                    _doorKey = key;
                    WriteDoor(use, precinct, plain);
                }
                Vector3 place = use == Use.Desk ? _interior.ServiceSpot : precinct.Door;
                _door.Show(place, player.transform.position, use == Use.Desk ? 2.2f : 2.4f, _doorWhat);
            }
        }

        private static bool Wanted()
        {
            return LawState.Loaded && LawState.WantedCharges != 0;
        }

        private static bool AtDoor(Vector3 at, Precinct precinct)
        {
            return StationReach.AtDoor(at.x - precinct.Door.x, at.y - precinct.Door.y, at.z - precinct.Door.z);
        }

        // The panel at the desk or the door; without the game's list the desk's four commonest services go on its keys.
        private void WriteDoor(Use use, Precinct precinct, bool plain)
        {
            WorldPanel panel = _door.Panel;
            panel.Lines(precinct.Name, (use == Use.Door ? "reallife_law_door_station" : "reallife_law_door_desk").Tr(), use == Use.Door ? null : "reallife_law_door_open".Tr(), WorldPanel.Green());
            if (use == Use.Door)
            {
                panel.Buttons("reallife_law_button_go", _onGo, true, null, null, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
            }
            else if (plain)
            {
                panel.Button(0, "reallife_law_button_report", _onQuickReport, true, true);
                panel.Button(1, "reallife_law_button_fines", _onQuickFines, ModState.PendingFine > 0, false);
                panel.Button(2, "reallife_law_button_car", _onQuickCar, LawState.ImpoundVehicleId != null, false);
                panel.Button(3, "reallife_law_button_paper", _onQuickPaper, Bridges.HasIncident, false);
            }
            else
            {
                panel.Buttons("reallife_law_button_speak", _onSpeak, true, null, null, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
            }
        }

        private void Idle()
        {
            _use = Use.None;
            _usePrecinct = null;
            _door.Hide("the player moved on");
            _doorKey = -1;
        }

        private bool Where(out Precinct precinct, out Use use)
        {
            precinct = _usePrecinct;
            use = _use;
            return precinct != null && use != Use.None && !_desk.IsOpen;
        }

        private void Speak()
        {
            try
            {
                if (Where(out Precinct precinct, out Use use) && use != Use.Door)
                {
                    Idle();
                    _desk.Open(precinct, use == Use.Desk);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void GoIn()
        {
            try
            {
                if (Where(out Precinct precinct, out Use use) && use == Use.Door)
                {
                    Idle();
                    // Measured again: the look four times a second may be a step behind the player.
                    Vector3 at = PlayerHelper.GetPosition();
                    bool there = AtDoor(at, precinct);
                    if (Meta.Dev.Verbose)
                    {
                        float metres = StationReach.Metres(at.x - precinct.Door.x, at.z - precinct.Door.z);
                        Logger?.Info("Law check (station door): the player was " + metres.ToString("F1", CultureInfo.InvariantCulture) + " m from "
                                     + precinct.Name + "'s door on the flat and " + (at.y - precinct.Door.y).ToString("F1", CultureInfo.InvariantCulture)
                                     + " m up at the press (wanted under " + StationReach.Door.ToString("F1", CultureInfo.InvariantCulture)
                                     + " m, the game's own reach at a door, and under " + StationReach.Rise.ToString("F1", CultureInfo.InvariantCulture)
                                     + " m up or down): " + (there ? "PASS" : "not let in from there, the panel goes: PASS") + ".");
                    }
                    if (!there)
                    {
                        return;
                    }
                    if (!EnterStation(precinct))
                    {
                        Notifications.Show(NotificationType.Warning, "reallife_law_station_no_entry", null, 6f);
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Quick(StationDesk.Service service)
        {
            try
            {
                if (Where(out Precinct precinct, out Use use) && use != Use.Door)
                {
                    _desk.Quick(precinct, use == Use.Desk, service);
                    _doorKey = -1;   // what the buttons say may have changed: a fine paid, a car brought
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game was asked seven seconds ago to take the player into a station and has not: its way in does not work for
        // this unit. What it left half done is put right, and the precinct's desk answers at the door for the session.
        private void NotLetIn(BuildingManager buildings, Precinct within)
        {
            Precinct asked = _asked;
            _asked = null;
            if (within == asked && buildings != null && !buildings.enteringBuilding)
            {
                return;   // they are in; the door's own event was missed
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

        /// <summary>Takes the player into a walk-in precinct by the game's own way into a building. False if that cannot be done now.</summary>
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

            // The game clears its event hub on every load: the hooks are put back when the event is not the one last seen.
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _onExit, ref _exitSeen);

            bool outdoors = !BuildingManager.IsInsideBuilding;

            // The room: owed its furniture if the door's event was missed (a save loaded inside), cleared once the player has left.
            if (_precincts.Chosen)
            {
                BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
                Precinct within = !outdoors && buildings != null && Settings.LawPrecincts && Settings.LawStation ? _precincts.Of(buildings.building) : null;
                if (_interior.Dressed && within != _interior.Precinct)
                {
                    _interior.End();
                }
                _roomOwed = within != null && !_interior.Dressed && !buildings.enteringBuilding && !buildings.exitingBuilding ? within : null;
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
            if (!_precincts.Chosen && outdoors)
            {
                if (!_precincts.Choose())
                {
                    return;
                }
                StationInterior.SearchDesigns(_precincts);
            }

            // A stay in the cell: resumed after a load; ended if it stalls or the save and the routine disagree.
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

            // The street: a fault here takes the frontage in, and after three leaves it off; it never ends a stay in the cell.
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

        // Somebody walking by close enough to speak: a smile and a line; for somebody the city loves, a few dollars too.
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

        /// <summary>Points the game's destination marker at the precinct nearest the player. Null if there is none.</summary>
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
    }
}
