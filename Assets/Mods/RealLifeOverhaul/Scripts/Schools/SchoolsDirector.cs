using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// BIRCHWOOD SCHOOL (plans/campus.md, docs/features/schools.md): a campus of the city's on Uptown's west square (block 202,
    /// Birch to Elm Street, Uptown to Summit Avenue), the main school of three floors of classrooms, the hall, the junior block,
    /// the court, the green, the playing field and the fence; and the city's schools as one market the player may buy into,
    /// whose trusts and council run theirs by the same rules. This owns the state and its timing; the game's scene is
    /// SchoolsGame's alone, the rules SchoolRules', the family and the council reach it through Wiring/Wiring.SchoolLinks.
    /// COST: one time comparison a frame; once a real second, a look at the clock, the save hook, the campus's copies and the
    /// families at the gates; a day's pass over twelve schools when a day comes; the campus stood once a load, behind the
    /// loading screen.
    /// </summary>
    public sealed partial class SchoolsDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("schools", "SchoolsDirector", typeof(SchoolsDirector));

        public const string PlacesOwner = "schools";   // its name among the district save's features
        private const int CatchUpDays = 3;             // game days missed that are still gone through one by one

        public static IModLogger Logger;
        public static SchoolsDirector Instance { get; private set; }

        private static SchoolState _state = new SchoolState();
        private static bool _loaded;          // this save's line is read
        private static bool _broken;          // a fault switched the feature off for the session
        private static bool _unread;          // the save's line could not be read: kept as it was, nothing moves
        private static object _save;          // the save the state was read from
        private static string _beforeTests;   // the line before the first test changed it, for "put back"
        private static string _liftText;      // the save's line the lift was last read from while the city loads
        private static SchoolState _liftState;

        private bool _running;
        private float _next;
        private Action _onSave;

        /// <summary>True while the schools run: their option on, this save's line read, no fault.</summary>
        public static bool Running => Instance != null && Instance._running && _loaded && !_broken && !_unread;

        /// <summary>The state in use; null before it is read.</summary>
        public static SchoolState State => _loaded ? _state : null;

        private static bool Verbose => Meta.Dev.Verbose;

        // ---------------------------------------------------------------- at game start

        /// <summary>
        /// At game start (Core/RealLifeOverhaulMod), before any save is read and after the landmarks: the campus's street named
        /// to the district save, and the lift its name gives the district's lots, chained after the landmarks' (Uptown asks it
        /// while the city loads, from this save's line alone).
        /// </summary>
        public static void Register(IModLogger logger)
        {
            Logger = logger;
            DistrictPlaces.Add(PlacesOwner, Fill);
            Func<string, int> before = DistrictBuildings.TrafficLift;
            DistrictBuildings.TrafficLift = lot => (before != null ? before(lot) : 0) + LiftOf(lot);
        }

        // ---------------------------------------------------------------- the city

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _loaded = false;
            SchoolsGame.Declare(Wanted);
            Ask();
            ScenePrewarm.AddLate("schools: Birchwood School's campus stood, its classrooms held", AtLoad);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                LeaveCampus("the city is unloading");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _loaded = false;
            _save = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Schools: SchoolsDirector.Update");
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
            if (now < _next)
            {
                return;
            }
            _next = now + 1f;
            if (_broken)
            {
                return;
            }
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                Fault("the timer", e);
            }
        }

        // The campus is wanted at this load: its option, the district laid, New York's.
        private static bool Wanted() => Settings.Schools && UptownLots.Ready;

        private void Slow()
        {
            if (SchoolsGame.Loading)
            {
                return;
            }
            if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                Load();
            }
            SaveHook.Keep(_onSave);   // the game clears its event hub on every load: kept attached, whatever the option
            TestTick();
            if (!Settings.Schools || _unread || !UptownLots.Ready)
            {
                if (_running)
                {
                    StandDown(_unread ? "its line in this save cannot be read" : !Settings.Schools ? "its option is off" : "Uptown does not stand at this load");
                }
                return;
            }
            if (!_running)
            {
                _running = true;
                Logger?.Info("Schools: running. " + (SchoolsGame.InSession
                    ? "A Going Public session: the city's schools are run by their owners alone; nothing is bought or sold."
                    : "Solo: Birchwood School may be bought and run."));
            }
            Clock();
            Campus();
        }

        // ---------------------------------------------------------------- the state

        private static void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            SchoolState state = SchoolStore.Load(save.modData, SchoolStore.Key, out int version);
            _unread = state == null;
            _state = state ?? new SchoolState();
            _beforeTests = save.modData.TryGetValue(SchoolStore.TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _save = save;
            _loaded = true;
            Logger?.Info("Schools: " + (_unread ? "this save's line cannot be read (a later version's, or damaged): kept as it is, and nothing moves"
                : version > 0 ? "read (line version " + version + "): " + Describe() : "a new save for the schools: Birchwood School opens on the next day's pass") + ".");
        }

        /// <summary>This save's line forgotten (its modData has just been removed): the next look reads it afresh, a new save's.</summary>
        public static void Forget()
        {
            _loaded = false;
            _beforeTests = null;
            Instance?.LeaveCampus("its line was removed");
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (!_loaded || save == null || !ReferenceEquals(_save, save))
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                if (SaveLines.IsKept(save.modData, SchoolStore.Key))
                {
                    return;   // a line this version could not read stays as it was read, for the version that can
                }
                save.modData[SchoolStore.Key] = SchoolStore.Write(_state);
                if (_beforeTests != null)
                {
                    save.modData[SchoolStore.TestKey] = _beforeTests;   // "put back" still works after a load
                }
                else
                {
                    save.modData.Remove(SchoolStore.TestKey);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The line into the save in memory now, so what the city loads next (the district's lift) reads it.
        private static void Written()
        {
            GameInstance save = SaveGameManager.Current;
            if (!_loaded || _unread || save == null || !ReferenceEquals(_save, save))
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[SchoolStore.Key] = SchoolStore.Write(_state);
        }

        /// <summary>Everything the campus stands in the street put away; the state stays, and the market carries on when it is on again.</summary>
        public void StandDown(string why)
        {
            bool was = _running;
            _running = false;
            LeaveCampus(why);
            if (was)
            {
                Logger?.Info("Schools: stood down, " + why + ".");
            }
        }

        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Schools: switched off for this session after an error in " + where + "; the state is kept as it was.");
            try
            {
                StandDown("after a fault");
            }
            catch (Exception inner)
            {
                Logger?.Error(inner);
            }
        }

        // ---------------------------------------------------------------- the clock

        // Every game day gone by is gone through, three at most after a long sleep or a time skip; a day's pass runs once a day.
        private void Clock()
        {
            int day = SchoolsGame.Day;
            if (day <= 0)
            {
                return;
            }
            int from = Math.Max(_state.LastDay > 0 ? _state.LastDay : day - 1, day - CatchUpDays);
            for (int d = from + 1; d <= day; d++)
            {
                DayPass(d);
            }
        }

        private void DayPass(int day)
        {
            try
            {
                if (day <= _state.LastDay)
                {
                    return;   // a day's pass runs once a day, whatever the loads
                }
                DayWork(day);
                Written();
            }
            catch (Exception e)
            {
                Fault("the day's pass", e);
            }
        }

        // ---------------------------------------------------------------- the district save's view of it

        // What of the schools the district save keeps out of every save: the campus's own street (the main school's door the
        // made buildings name themselves; the campus's ground is Uptown's).
        private static void Fill(PlaceSet set)
        {
            if (!Settings.Schools)
            {
                return;
            }
            set.Street(SchoolGrounds.Street);
        }

        // ---------------------------------------------------------------- the lift of the district's lots

        /// <summary>
        /// The traffic points a lot of the district gets from the campus's name (DistrictBuildings.TrafficLift, after the
        /// landmarks'), from this save's line alone: asked by Uptown while the city loads, before this director has read it.
        /// None with the option off or in a Going Public session (the game's prices stay the same on every machine).
        /// </summary>
        public static int LiftOf(string lotId)
        {
            if (!Settings.Schools || SchoolsGame.InSession)
            {
                return 0;
            }
            Lot lot = UptownLots.Get(lotId);
            SchoolState state = LiftState();
            if (lot == null || state == null)
            {
                return 0;
            }
            SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
            return SchoolRules.Lift(lot.X, lot.Z, state.Campus.Reputation, state.Opened > 0, x0, z0, x1, z1);
        }

        // The state the lift reads: the director's once read, else this save's line, parsed again only when it changes.
        private static SchoolState LiftState()
        {
            GameInstance save = SaveGameManager.Current;
            if (_loaded && ReferenceEquals(_save, save))
            {
                return _unread ? null : _state;
            }
            string text = save != null && save.modData != null && save.modData.TryGetValue(SchoolStore.Key, out string line) ? line : null;
            if (!ReferenceEquals(text, _liftText) || _liftState == null)
            {
                _liftText = text;
                _liftState = string.IsNullOrEmpty(text) ? new SchoolState() : SchoolStore.Read(text, out _) ?? new SchoolState();
            }
            return _liftState;
        }

        private static string Describe()
        {
            SchoolFile c = _state.Campus;
            return "Birchwood School opened on day " + _state.Opened + ", run by " + OwnerWords(c.Owner) + ", " + c.Pupils + " of " + c.Places + " places, fee " + c.Fee
                   + ", " + c.Teachers + " teachers, name " + c.Reputation + "; " + _state.Children.Count + " of the player's children there";
        }

        private static string OwnerWords(int owner) => owner == SchoolOwner.Player ? "the player" : owner == SchoolOwner.Council ? "the council" : "trust " + owner;
    }
}
