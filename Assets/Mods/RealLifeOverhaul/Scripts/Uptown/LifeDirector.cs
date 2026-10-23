using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// UPTOWN AS A DISTRICT OF THE GAME (docs/features/uptown.md, "The district and its buildings"): north of Manhattan, on
    /// the ground Scripts/Uptown's ground lays at every load, a district with an id of the mod's own whose buildings take
    /// part in the game's own simulation as its districts' do: the game's businesses open there, its rents and real estate,
    /// BizMan, the Market Insider, the map's rows and search see it, and the player rents and buys there. Everything that
    /// names it leaves every save for the mod's own section and comes back at a load (Scripts/DistrictSave), so a save loads
    /// without the mod at any time. A load in three steps:
    ///   1. when a save has been read, before its city's scenes are built (the district save's early step): the district
    ///      into the game's lists of districts (LifeGame.Know), so every list the game builds as the scenes load has it;
    ///   2. in the city's load: the lots' buildings declared to the made buildings' engine as the game's own (Awake), made
    ///      behind the loading screen, then those that stood put in the game's lists of buildings, cut into the walking
    ///      meshes and the district put on the city map (<see cref="Settle"/>, the step before the district save's, which
    ///      then puts back what the save kept of them);
    ///   3. in the game's late "game loaded" call: its first life where the save never had it (its stats, the game's own
    ///      generator for it), and the first load of each building the construction game finished.
    /// It lives only where the ground's own verdict says (GroundSession.Agrees, UptownLots.Ready: the option, the city, a
    /// Going Public session only when every machine runs this same mod), with its buildings or not at all, and stands down
    /// for the rest of the city when none stands at the load, when the ground's feature cleans up or when a session stops
    /// being one of the same mod (<see cref="StandDown"/>).
    /// COST: idle, one time comparison a frame; once a real second, a look at the session while one is on.
    /// </summary>
    public sealed partial class LifeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("uptown", "LifeDirector", typeof(LifeDirector));   // its Update, coroutines and handlers

        public const string PlacesOwner = "uptown";   // its name among the district save's features
        private const float SlowEvery = 1f;            // s of real time between two looks at the session and the ground
        private const float ExitWait = 15f;            // s of real time the game is given to take the player out of a building
        private const float OutsideDoor = 1.2f;        // m in front of a door: the spot a save made inside puts the player at

        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        public static IModLogger Logger;
        public static LifeDirector Instance { get; private set; }

        // One load's state, from the district save's early step to the city's end.
        private static bool _known;           // the district is among the game's districts at this load
        private static bool _lives;           // its buildings stand as the game's at this load
        private static bool _down;            // it stood down in this city
        private static bool _cityWasNew;      // the save's city was not set up yet: the game's own generator sets Uptown up with the rest
        private static string _notWhy;        // why it is not a district, or has no building, at this load (the census)
        private static int _stamp;
        private static LifeState _state = new LifeState();
        private static bool _stateUnread;     // the save's line of the district could not be read: kept as it is, nothing finished meanwhile
        private static readonly List<BuildingsLot> Plan = new List<BuildingsLot>(128);
        private static readonly List<int> DayOne = new List<int>(32);
        private static readonly List<BuildingsStructure> Options = new List<BuildingsStructure>(16);
        private static readonly List<MadeSpec> Declared = new List<MadeSpec>(64);
        private static readonly List<MadeBuilding> Standing = new List<MadeBuilding>(64);
        private static readonly HashSet<string> DayOneLots = new HashSet<string>(StringComparer.Ordinal);
        private static float _nearest;        // the plan's nearest reach from Manhattan: where the traffic steps count from

        private float _nextSlow;
        private bool _closingHooked;

        /// <summary>True while Uptown is a district of the game with its buildings standing at this load.</summary>
        public static bool Lives => _lives && !_down;

        /// <summary>Changes whenever a lot's state does: a reader that keeps something made from them compares it.</summary>
        public static int Stamp => _stamp;

        /// <summary>The buildings standing in the district at this load, into the caller's list (not cleared).</summary>
        public static void Buildings(List<MadeBuilding> into)
        {
            into.AddRange(Standing);
        }

        // ---------------------------------------------------------------- at game start and at a save's reading

        /// <summary>
        /// At game start (Core/RealLifeOverhaulMod, RealLifeOverhaulInit): Uptown's places named to the district save, with its
        /// early step; and its own line kept by the save tools with the district's records.
        /// </summary>
        public static void Register(IModLogger logger)
        {
            Logger = logger;
            DistrictPlaces.Add(PlacesOwner, Fill, Early);
            DistrictSaveDirector.KeepsAlso(LifeStore.Key);
        }

        // The district save's early step: a save has been read, its city's scenes not built.
        private static void Early()
        {
            Begin();
            _earlyFor = SaveGameManager.Current;
            Know("before the city's scenes are built");
        }

        // A new load: nothing of the one before carries over, its declarations withdrawn from the made buildings' engine. A city whose
        // director never started (its feature away there) left the district's record, its rows in the game's run-long tables and its
        // types' targets, which the game keeps for the whole run: given back first, as the director gives them back when its city goes.
        private static void Begin()
        {
            if (LifeGame.Record != null)
            {
                LifeGame.Unload();
            }
            _known = _lives = _down = false;
            _standingDown = _testing = false;   // a stand-down or a test the last city's end cut short never comes back to say it is done
            _notWhy = null;
            _earlyFor = null;
            for (int i = 0; i < Declared.Count; i++)
            {
                MadeBuildings.Withdraw(Declared[i].Id);
            }
            Standing.Clear();
            Declared.Clear();
            DayOneLots.Clear();
            _stamp++;
        }

        // The district among the game's districts, where the ground's verdict lets it be (the option, Going Public) and the save is New York's.
        private static void Know(string when)
        {
            if (_known)
            {
                return;
            }
            if (!GroundSession.Agrees(out string why) || !LifeGame.NewYorkSave(SaveGameManager.Current, out why))
            {
                _notWhy = why ?? "the ground's verdict";
                Logger?.Info("Uptown: not a district of the game at this load (" + _notWhy + ").");
                return;
            }
            if (!LifeGame.Know(out why))
            {
                _notWhy = why;
                Logger?.Warn("Uptown: could not be made a district of the game " + when + " (" + why + "); it stays out of this load, and nothing of its is lost.");
                return;
            }
            _known = true;
        }

        // ---------------------------------------------------------------- the city's load

        private void Awake()
        {
            Instance = this;
            if (SaveGameManager.Current == null || SaveGameManager.Current != _earlyFor)
            {
                Begin();   // a load that did not pass the main menu (a funeral's backup) had no early step
            }
            Know("at the city load");
            _cityWasNew = SaveGameManager.Current != null && !SaveGameManager.Current.CityInitialized;
            ReadState();
            LifeGame.Lots(Plan);   // the ground's plan, read for the construction game whether or not the district lives
            _nearest = BuildingsRules.Nearest(Plan, LifeGame.AwayX, LifeGame.AwayZ);
            if (_known)
            {
                Declare();
            }
            UptownLots.Closing += OnClosing;
            _closingHooked = true;
            ScenePrewarm.AddLate("uptown: the district's first life and the first load of its finished buildings", FirstLife);
        }

        private static GameInstance _earlyFor;   // the save the district save's early step was for
        private static GameInstance _stateFor;   // the save the district's line was read from

        // The district's own line of this save: the buildings the construction game finished.
        private static void ReadState()
        {
            GameInstance save = SaveGameManager.Current;
            _stateFor = save;
            LifeState read = LifeStore.Load(save != null ? save.modData : null, out int _);
            _stateUnread = read == null;
            _state = read ?? new LifeState();
        }

        // Every lot with a building declared to the made buildings' engine as the game's own: a quarter of the first block from
        // the first day, and every one the construction game finished. Made behind the loading screen while the ground stands.
        private static void Declare()
        {
            BuildingsRules.DayOne(Plan, LifeGame.AwayX, LifeGame.AwayZ, DayOne);
            for (int i = 0; i < DayOne.Count; i++)
            {
                DayOneLots.Add(Plan[DayOne[i]].Id);
            }
            int finished = 0, refused = 0;
            for (int i = 0; i < Plan.Count; i++)
            {
                BuildingsLot lot = Plan[i];
                FinishedBuilding f = _state.On(lot.Id);
                if (f == null && !DayOneLots.Contains(lot.Id))
                {
                    continue;   // empty, for the construction game
                }
                BuildingsStructure structure;
                if (f != null)
                {
                    structure = new BuildingsStructure { Size = f.Size, Version = f.Version, Type = f.Type };
                    finished++;
                }
                else
                {
                    LifeGame.Structures(lot, Options);
                    int pick = BuildingsRules.PickStructure(lot.Id, Options);
                    if (pick < 0)
                    {
                        refused++;
                        Logger?.Info("Uptown: lot " + lot.Id + " stays empty at this load: the game has no structure for its sizes.");
                        continue;
                    }
                    structure = Options[pick];
                }
                Lot plain = UptownLots.Get(lot.Id);
                MadeSpec spec = LifeGame.Spec(lot, structure, f != null ? f.Design : null, f != null ? f.Storeys : 0, BuildingsRules.Traffic(lot, _nearest, LifeGame.AwayX, LifeGame.AwayZ),
                    () => _known && !_down && UptownLots.Ready && plain != null && UptownLots.Open(plain));
                MadeBuildings.Declare(spec);
                Declared.Add(spec);
            }
            Logger?.Info("Uptown: " + Declared.Count + " building(s) declared for this load (" + DayOne.Count + " of the first block's lots from the first day, " + finished
                         + " finished by the construction game" + (refused > 0 ? ", " + refused + " left empty" : string.Empty) + ") of " + Plan.Count + " lots.");
        }

        /// <summary>
        /// The city's load step after the made buildings stand and before the district save puts back what the save kept
        /// (Core/RealLifeOverhaulMod): each of the district's buildings that stood into the game's lists of buildings and cut
        /// into the walking meshes, and the district on the city map. A district with no building at this load (its ground not
        /// laid, its feature away in another city or switched off by the guard) leaves the game's lists of districts here and
        /// what names it is kept in the mod's section, as when it stands down in play: it lives at a load with its buildings
        /// or not at all. A step of its own, not the director's, so it runs where the director never started.
        /// </summary>
        public static void Settle()
        {
            Standing.Clear();
            if (!_known)
            {
                return;
            }
            if (Instance == null || FrameGuard.Off)
            {
                for (int i = 0; i < Declared.Count; i++)
                {
                    MadeBuilding made = MadeBuildings.Get(Declared[i].Id);
                    if (made != null && made.Controller != null)
                    {
                        Standing.Add(made);   // made before the guard went off: named, kept out and hidden with the rest
                    }
                }
                _lives = Standing.Count > 0;
                Close(FrameGuard.Away ? "its feature stands on New York's data, and this city is another" : "its feature is not running at this load", false);
                return;
            }
            int cut = 0;
            for (int i = 0; i < Declared.Count; i++)
            {
                MadeBuilding made = MadeBuildings.Get(Declared[i].Id);
                if (made == null || made.Controller == null || made.Building == null)
                {
                    continue;   // it stood down: Player.log says why
                }
                LifeGame.Enter(made);
                cut += LifeGame.Cut(made) ? 1 : 0;
                Standing.Add(made);
            }
            _lives = Standing.Count > 0;
            _stamp++;
            if (!_lives)
            {
                _notWhy = !UptownLots.Ready ? UptownLots.Why ?? "its ground is not laid"
                          : Declared.Count == 0 ? "no lot of it has a building" : "none of its buildings could stand (Player.log says why)";
                Close("no building of it stands at this load: " + _notWhy, false);
                return;
            }
            Bounds(out float x1, out float z1, out float x2, out float z2);
            if (!LifeGame.Map(x1, z1, x2, z2, out string why))
            {
                Logger?.Warn("Uptown: its zone on the city map could not be made (" + why + "); the district has no row in the map's filters at this load.");
            }
            Logger?.Info("Uptown: " + Standing.Count + " of " + Declared.Count + " building(s) standing as the game's own, " + cut + " cut into the walking meshes.");
        }

        // The district's ground's bounds, for its zone on the city map; its lots' if the ground names none.
        private static void Bounds(out float x1, out float z1, out float x2, out float z2)
        {
            x1 = z1 = float.MaxValue;
            x2 = z2 = float.MinValue;
            IReadOnlyList<GroundRect> ground = UptownLots.Ground;
            for (int i = 0; ground != null && i < ground.Count; i++)
            {
                x1 = Mathf.Min(x1, Mathf.Min(ground[i].X1, ground[i].X2));
                x2 = Mathf.Max(x2, Mathf.Max(ground[i].X1, ground[i].X2));
                z1 = Mathf.Min(z1, Mathf.Min(ground[i].Z1, ground[i].Z2));
                z2 = Mathf.Max(z2, Mathf.Max(ground[i].Z1, ground[i].Z2));
            }
            if (x1 != float.MaxValue)
            {
                return;
            }
            for (int i = 0; i < Plan.Count; i++)
            {
                x1 = Mathf.Min(x1, Plan[i].X - Plan[i].Width * 0.5f);
                x2 = Mathf.Max(x2, Plan[i].X + Plan[i].Width * 0.5f);
                z1 = Mathf.Min(z1, Plan[i].Z - Plan[i].Depth * 0.5f);
                z2 = Mathf.Max(z2, Plan[i].Z + Plan[i].Depth * 0.5f);
            }
        }

        // ---------------------------------------------------------------- the district save's view of it

        // What of Uptown the district save keeps out of every save: its district from the moment it is among the game's; its
        // streets, ground, way out and every standing building's door once its buildings stand. From its own data alone.
        private static void Fill(PlaceSet set)
        {
            if (!_known)
            {
                return;
            }
            set.District(DistrictValues.Id);
            if (!_lives)
            {
                return;
            }
            for (int i = 0; i < Plan.Count; i++)
            {
                set.Street(Plan[i].Street);
            }
            IReadOnlyList<GroundRect> ground = UptownLots.Ground;
            for (int i = 0; ground != null && i < ground.Count; i++)
            {
                set.Ground(ground[i].X1, ground[i].Z1, ground[i].X2, ground[i].Z2);
            }
            UptownExit exit = UptownLots.Exit;
            set.Exit(exit.X, exit.Y, exit.Z, exit.Heading, exit.District);
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                float turn = made.DoorFacing * Mathf.Deg2Rad;   // the door's outward way, as Quaternion.Euler(0, facing, 0) turns forward
                set.Door(made.Spec.Street, made.Spec.Number, made.DoorX + Mathf.Sin(turn) * OutsideDoor, made.DoorY, made.DoorZ + Mathf.Cos(turn) * OutsideDoor,
                    made.DoorFacing, exit.District);   // a car inside one is parked in the way out's district of the game's
            }
        }

        // ---------------------------------------------------------------- the game's late "game loaded" call

        // Its first life where the save never had it, and each finished building's first load. The loading screen still up.
        private static bool FirstLife()
        {
            GameInstance save = SaveGameManager.Current;
            if (!Lives || save == null)
            {
                Census("the load");
                return true;
            }
            bool fresh = !LifeGame.HasStats(save);
            if (fresh && !_cityWasNew)
            {
                LifeGame.Populate(save);   // as the game sets up a district it adds to an older save
            }
            bool stats = LifeGame.Stats(save);
            int settled = 0, failed = 0;
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                FinishedBuilding f = _state.On(made.Spec.Id);
                if (f != null && !f.Settled)
                {
                    string why = LifeGame.Settle(made, f.Owner, f.Price, save);
                    if (why == null)
                    {
                        f.Settled = true;
                        settled++;
                    }
                    else
                    {
                        failed++;
                        Logger?.Warn("Uptown: the finished building at " + made.Address + " could not be set up at its first load (" + why + "); it is tried again at the next load.");
                    }
                }
                else if (f == null && made.NewRegistration && !fresh)
                {
                    LifeGame.Settle(made, FinishOwner.City, 0, save);   // a day-one building new to a district that lived before: free to let
                }
            }
            if (settled > 0)
            {
                WriteState();
            }
            Logger?.Info("Uptown: " + (fresh ? (_cityWasNew ? "set up with the rest of a new city by the game's own generator" : "set up for the first time in this save by the game's own generator")
                                             : "lives on in this save") + (stats ? ", its stats made" : string.Empty)
                         + (settled + failed > 0 ? "; " + settled + " finished building(s) at their first load" + (failed > 0 ? ", " + failed + " not yet" : string.Empty) : string.Empty) + ".");
            Census("the load");
            return true;
        }

        // The district's own line into the save in memory (the game writes it with the save).
        private static void WriteState()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || _stateUnread)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[LifeStore.Key] = LifeStore.Write(_state);
        }

        // ---------------------------------------------------------------- once a second

        private static readonly int HotUpdate = HotSpots.Slot("Uptown: LifeDirector.Update");
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
            float now = Time.unscaledTime;
            if (now < _nextSlow)
            {
                return;
            }
            _nextSlow = now + SlowEvery;
            if (!Lives || _standingDown)
            {
                return;
            }
            if (UptownLots.Closed)
            {
                StandDown("its ground's feature has cleaned up", false);
            }
            else if (GoingPublic.InSession && !GroundSession.Agrees(out string why))
            {
                SessionTurned(why);
            }
        }

        // The session stopped being one where every machine runs this mod: once a city, so its line is built here and never a frame.
        private static void SessionTurned(string why)
        {
            StandDown("the Going Public session is no longer one where every machine runs this mod in this version: " + (why ?? "the ground's verdict"), true);
        }

        // The ground's clean-up (another city, its faults, the save tools) raises Closing after the feature's guards are off, so
        // this is no handler of the guard's, which would skip it then: the ground runs each handler in its own try and logs its fault.
        private static void OnClosing(string why)
        {
            StandDown(why ?? "its ground's feature has cleaned up", false);
        }

        // ---------------------------------------------------------------- standing down in play

        private static bool _standingDown;

        /// <summary>
        /// Uptown out of the game for the rest of this city: the player taken out of a building of it first (the game's own
        /// way out, behind its fade), then every record naming it out of the save in memory, kept in the mod's section
        /// (DistrictSaveDirector.KeepOut), its buildings out of the game's lists and hidden (never destroyed), its district
        /// out of the game's list and array of districts and off the city map. Its records come back at a load where it
        /// lives again. Once a city. <paramref name="sessionChanged"/>: the Going Public session's verdict turned, which the
        /// player is told of.
        /// </summary>
        public static void StandDown(string why, bool sessionChanged)
        {
            if (!_known || _down || _standingDown)
            {
                return;
            }
            _standingDown = true;
            if (Instance != null && Instance.isActiveAndEnabled)
            {
                Instance.StartCoroutine(Guard.Routine(Instance.Down(why, sessionChanged)));
            }
            else
            {
                Close(why, sessionChanged);
            }
        }

        private IEnumerator Down(string why, bool sessionChanged)
        {
            if (LifeGame.PlayerInside(Standing))
            {
                LifeGame.WalkOut();
                float until = Time.unscaledTime + ExitWait;
                while (Time.unscaledTime < until && LifeGame.Indoors)
                {
                    yield return null;
                }
            }
            Close(why, sessionChanged);
        }

        private static void Close(string why, bool sessionChanged)
        {
            try
            {
                int kept = DistrictSaveDirector.KeepOut(PlacesOwner, why);
                LifeGame.LeaveLists();
                for (int i = 0; i < Standing.Count; i++)
                {
                    CopyGame.Show(Standing[i].Controller, false);
                    Standing[i].Shown = false;
                }
                MadeBuildings.Changed();
                LifeGame.Unlist();
                LifeGame.Unmap();
                _down = true;
                _stamp++;
                string line = "Uptown: out of the game's districts for the rest of this city (" + why + "): " + kept + " record(s) and stand-in(s) kept in the mod's section, "
                              + Standing.Count + " building(s) hidden; it comes back at the next load where it may live.";
                if (Standing.Count > 0)
                {
                    Logger?.Warn(line);
                }
                else
                {
                    Logger?.Info(line);
                }
                if (sessionChanged && GoingPublic.InSession)
                {
                    Notifications.Show(NotificationType.Warning, LifeText.ClosedSession, null, 14f);
                }
            }
            finally
            {
                _standingDown = false;
            }
        }

        // ---------------------------------------------------------------- the city goes

        private void OnDestroy()
        {
            if (_closingHooked)
            {
                UptownLots.Closing -= OnClosing;
                _closingHooked = false;
            }
            LifeGame.Unload();
            Standing.Clear();
            _known = _lives = _down = false;
            _stateFor = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
