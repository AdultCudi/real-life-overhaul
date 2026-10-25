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
    ///   2. in the city's load: the lots' buildings declared to the made buildings' engine as the game's own (Awake: most of
    ///      the band from the first day, every one the construction game finished, every one a save holds the records of),
    ///      made behind the loading screen, then those that stood put in the game's lists of buildings, cut into the walking
    ///      meshes and the district put on the city map (<see cref="Settle"/>, the step before the district save's, which
    ///      then puts back what the save kept of them);
    ///   3. in the game's late "game loaded" call: its first life where the save never had it (its stats, the game's own
    ///      generator for it), the game's own generator for its buildings new to a save it lived in before, the first load
    ///      of each building the construction game finished, and the leased offices' businesses (the city's rival builders).
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
        private static readonly List<BuildingsLot> Plan = new List<BuildingsLot>(512);
        private static readonly List<int> Built = new List<int>(512);
        private static readonly List<BuildingsStructure> Options = new List<BuildingsStructure>(16);
        private static readonly List<BuildingsStructure> Others = new List<BuildingsStructure>(8);
        private static readonly List<MadeSpec> Declared = new List<MadeSpec>(512);
        private static readonly List<MadeBuilding> Standing = new List<MadeBuilding>(512);
        private static readonly HashSet<string> BuiltLots = new HashSet<string>(StringComparer.Ordinal);   // a building declared on it at this load, from the first day or kept
        private static readonly HashSet<string> KeptLots = new HashSet<string>(StringComparer.Ordinal);    // of those, built because the save holds their records from an earlier load
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
            MadeBuildings.Arranging += Arrange;
            DistrictSaveDirector.KeepsAlso(LifeStore.Key);
        }

        // The district save's early step: a save has been read, its city's scenes not built.
        private static void Early()
        {
            Begin();
            RetireEarlierPlan(SaveGameManager.Current);
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
            StandingLots.Clear();
            Declared.Clear();
            BuiltLots.Clear();
            KeptLots.Clear();
            ForgetLeases();
            ForgetSpecials();
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
                RetireEarlierPlan(SaveGameManager.Current);   // before the district save's city step reads what it keeps
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
            if (_state.Plan < LayoutRules.PlanGeneration)
            {
                // Its finished buildings and opened lots name the first plan's lots, which are no more: the district starts afresh.
                Logger?.Info("Uptown: this save's lots are of the district's plan " + _state.Plan + "; they were planned again, so its " + _state.Finished.Count
                             + " finished building(s) and its opening pass are retired with the district's records, and the district is set up afresh.");
                _state = new LifeState();
            }
            WriteState();   // the line says the plan's generation from this load on, whatever else happens
        }

        // A save of an earlier plan of Uptown's lots: its district's records name addresses that are now other buildings, of
        // other sizes, so the district save lets them go before they go back (DistrictSaveDirector.Retire).
        private static void RetireEarlierPlan(GameInstance save)
        {
            int plan = save != null ? LifeStore.PlanOf(save.modData) : LayoutRules.PlanGeneration;
            if (plan < LayoutRules.PlanGeneration)
            {
                DistrictSaveDirector.Retire(save, LayoutStreets.Prefix, "Uptown's lots were planned again; this save's are of plan " + plan);
            }
        }

        // Every lot with a building declared to the made buildings' engine as the game's own: the band's lots built from the
        // first day (all but those left open for the construction game), every one the construction game finished, and every
        // one whose records the save holds from an earlier load, so no building of a save is ever lost to a change of the
        // plan. Made behind the loading screen while the ground stands.
        private static void Declare()
        {
            DecideBig();
            BuildingsRules.Built(Plan, DistrictBuildings.KeptOpen, LifeGame.AwayX, LifeGame.AwayZ, Built);
            for (int i = 0; i < Built.Count; i++)
            {
                BuiltLots.Add(Plan[Built[i]].Id);
            }
            int finished = 0, refused = 0, leased = 0, special = 0;
            for (int i = 0; i < Plan.Count; i++)
            {
                BuildingsLot lot = Plan[i];
                if (!BuildingsRules.Clear(lot.Id) && !BigDown.Contains(lot.Id))
                {
                    continue;   // taken from the start for every feature, so no save holds a building there (a big place's whose place stands)
                }
                FinishedBuilding f = _state.On(lot.Id);
                if (f == null && !BuiltLots.Contains(lot.Id))
                {
                    if (!LifeGame.Kept(lot))
                    {
                        continue;   // open, for the construction game
                    }
                    BuiltLots.Add(lot.Id);
                    KeptLots.Add(lot.Id);
                }
                BuildingsStructure structure;
                SpecialPlace place = null;
                Others.Clear();   // a finished building keeps the structure and design the construction game gave it
                if (f != null)
                {
                    structure = new BuildingsStructure { Size = f.Size, Version = f.Version, Type = f.Type };
                    finished++;
                }
                else if (SpecialStructure(lot, out structure, out place))
                {
                    special++;
                }
                else
                {
                    LifeGame.Structures(lot, Options);
                    bool lease = LeaseOf(lot.Id) >= 0;
                    int pick = lease ? BuildingsRules.PickOffice(lot.Id, Options) : BuildingsRules.PickStructure(lot.Id, Options);
                    if (pick < 0)
                    {
                        refused++;
                        BuiltLots.Remove(lot.Id);
                        Logger?.Info("Uptown: lot " + lot.Id + " stays empty at this load: the game has no " + (lease ? "office " : string.Empty) + "structure for its sizes.");
                        continue;
                    }
                    leased += lease ? 1 : 0;
                    structure = Options[pick];
                    if (lot.Version <= 0)
                    {
                        BuildingsRules.Others(Options, pick, lease, Others);   // a lot of the plan's spaces keeps its size: they are that size's
                    }
                }
                Lot plain = UptownLots.Get(lot.Id);
                MadeSpec spec = LifeGame.Spec(GroundOf(lot, f), structure, Others, f != null ? f.Design : BuildingsRules.LookOf(lot.Class), f != null ? f.Storeys : 0, TrafficOf(lot.Id),
                    () => _known && !_down && UptownLots.Ready && plain != null && UptownLots.Open(plain));
                if (place != null)
                {
                    LifeGame.Serve(spec, place);
                }
                MadeBuildings.Declare(spec);
                Declared.Add(spec);
            }
            int big = DeclareBig();
            int open = 0, notClear = 0, held = 0, ring = 0, ringBuilt = 0;
            for (int i = 0; i < Plan.Count; i++)
            {
                bool clear = BuildingsRules.Clear(Plan[i].Id), place = BigPlacesRules.Holds(Plan[i].Id);
                open += clear && !BuiltLots.Contains(Plan[i].Id) && _state.Covering(Plan[i].Id) == null ? 1 : 0;
                notClear += clear || place ? 0 : 1;
                held += place ? 1 : 0;
                bool onRing = LayoutRules.IsRing(Plan[i].Block / 100, Plan[i].Block % 100);
                ring += onRing ? 1 : 0;
                ringBuilt += onRing && BuiltLots.Contains(Plan[i].Id) ? 1 : 0;
            }
            Logger?.Info("Uptown: " + Declared.Count + " building(s) declared for this load (" + Built.Count + " of the band's lots from the first day, " + leased
                         + " of them offices leased to the city's builders, " + special + " the district's special places; " + KeptLots.Count + " kept from an earlier load; "
                         + finished + " finished by the construction game; " + big + " of the game's special services copied whole over " + held + " lots"
                         + (refused > 0 ? "; " + refused + " left empty" : string.Empty) + ") of " + Plan.Count + " lots, " + open + " open for the construction game"
                         + (notClear > 0 ? ", " + notClear + " kept empty as something of the game's stands into it" : string.Empty) + "; on the ring past its outer streets "
                         + ringBuilt + " of " + ring + " lots built.");
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
            StandingLots.Clear();
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
                MarkStanding();
                Close(FrameGuard.Away ? "its feature stands on New York's data, and this city is another" : "its feature is not running at this load", false);
                return;
            }
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
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
            int fillers = LifeGame.StandFillers(Fills);
            Fills.Clear();
            _lives = Standing.Count > 0;
            MarkStanding();
            _stamp++;
            ListBig(true);
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
            Logger?.Info("Uptown: " + Standing.Count + " of " + Declared.Count + " building(s) standing as the game's own, " + cut + " cut into the walking meshes, "
                         + fillers + " filler(s) standing beside and behind them, in "
                         + ((System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F0", Plain) + " ms.");
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

        private static readonly List<MadeBuilding> NewOnes = new List<MadeBuilding>(512);
        private static readonly List<MadeBuilding> LaterOnes = new List<MadeBuilding>(64);   // buildings new to a save the district lived in, for the opening pass

        // Its first life where the save never had it, the game's own generator for its buildings new to a save it lived in
        // before (as for a district the game adds to an older save, those alone), each finished building's first load, and the
        // leased offices' businesses. The loading screen still up.
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
            int populated = 0;
            if (!fresh)
            {
                NewOnes.Clear();
                for (int i = 0; i < Standing.Count; i++)
                {
                    if (Standing[i].NewRegistration && _state.On(Standing[i].Spec.Id) == null)
                    {
                        NewOnes.Add(Standing[i]);   // nothing of it was in the save: a lot built by a newer plan, or one that could not stand before
                    }
                }
                populated = NewOnes.Count > 0 ? LifeGame.PopulateOnly(save, NewOnes, Standing) : 0;
                LaterOnes.Clear();
                LaterOnes.AddRange(NewOnes);
                NewOnes.Clear();
            }
            bool stats = LifeGame.Stats(save);
            int settled = 0, failed = 0;
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                FinishedBuilding f = _state.On(made.Spec.Id);
                if (f == null || f.Settled)
                {
                    continue;
                }
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
            if (settled > 0)
            {
                WriteState();
            }
            int leased = Leases(save, out int renamed);
            int specials = Specials();
            if (!_state.Mixed && !_stateUnread)
            {
                int changed = LifeGame.Mix(Standing, Standing, Another, save, out string mixSaid);
                _state.Mixed = _state.Opened = true;   // once a save: the units the game's own days change later are the game's to let or fill
                WriteState();
                Logger?.Info("Uptown: the mix pass after the game's generator changed " + changed + " unit(s) (" + mixSaid + ").");
            }
            else if (!_stateUnread)
            {
                OpenLater(save);
            }
            LaterOnes.Clear();
            Logger?.Info("Uptown: " + (fresh ? (_cityWasNew ? "set up with the rest of a new city by the game's own generator" : "set up for the first time in this save by the game's own generator")
                                             : "lives on in this save" + (populated > 0 ? ", the game's own generator for its " + populated + " building(s) new to it" : string.Empty))
                         + (stats ? ", its stats made" : string.Empty)
                         + (settled + failed > 0 ? "; " + settled + " finished building(s) at their first load" + (failed > 0 ? ", " + failed + " not yet" : string.Empty) : string.Empty)
                         + "; " + leased + " of " + BuildingsRules.LeasedOffices + " leased offices with their businesses"
                         + (renamed > 0 ? ", " + renamed + " of them given their lessees' names again" : string.Empty)
                         + "; " + specials + " of " + SpecialsRules.All.Length + " special places with the game's services.");
            Census("the load");
            SpecialsCensus("the load", false, out int _);
            return true;
        }

        // The mix pass for the buildings new to a save the district lived in, those alone (the game's generator ran for
        // them alone), each lot's share of units kept to let worked out among them, every one marked so it is filled once;
        // the businesses already in the district stay as they are.
        private static void OpenLater(GameInstance save)
        {
            NewOnes.Clear();
            for (int i = 0; i < LaterOnes.Count; i++)
            {
                if (!_state.OpenedLot(LaterOnes[i].Spec.Id))
                {
                    NewOnes.Add(LaterOnes[i]);
                }
            }
            if (NewOnes.Count == 0)
            {
                return;
            }
            int changed = LifeGame.Mix(NewOnes, Standing, Another, save, out string mixSaid);
            for (int i = 0; i < NewOnes.Count; i++)
            {
                _state.MarkOpened(NewOnes[i].Spec.Id);
            }
            Logger?.Info("Uptown: the mix pass for its " + NewOnes.Count + " building(s) new to this save changed " + changed + " unit(s) (" + mixSaid + ").");
            NewOnes.Clear();
            WriteState();
        }

        // A building the mix pass leaves alone: a lease's lot (its business moves in by the lease), or one the mod holds
        // in the special buildings' book for a place of its own.
        private static bool Another(MadeBuilding made)
        {
            return made == null || LeaseOf(made.Spec.Id) >= 0 || Array.IndexOf(LeasedUp, made) >= 0 || SpecialBuildings.TakenBy(made.Registration) != null;
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
                ListBig(false);
                LifeGame.LeaveLists();
                LifeGame.LeaveSpecials(Standing);
                for (int i = 0; i < Standing.Count; i++)
                {
                    CopyGame.Show(Standing[i].Controller, false);
                    Standing[i].Shown = false;
                }
                LifeGame.ShowFillers(false);
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
            ShowHidden();
            LifeGame.Unload();
            Standing.Clear();
            StandingLots.Clear();
            ForgetLeases();
            ForgetSpecials();
            _known = _lives = _down = false;
            _stateFor = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
