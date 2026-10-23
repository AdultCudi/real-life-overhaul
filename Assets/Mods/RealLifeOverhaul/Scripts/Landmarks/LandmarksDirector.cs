using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// UPTOWN'S LANDMARKS (docs/features/landmarks.md): Summit Tower, Uptown Galleria and Crest Field, the city's three
    /// megaprojects on blocks of the new district's first block. Each is announced in the City Ledger on its day, put out
    /// to tender in the construction game (the player's company may bid against the city's eight builders) or built by the
    /// city's own works when nobody can bid, goes up as a site of the city's construction programme, is handed over and
    /// opens with a ceremony; then it lifts the traffic and the land of the lots round it, and the stadium has its home
    /// matches. The tower and the mall become the district's own buildings (DistrictBuildings.FinishOver), the mall's shops
    /// let to the city's businesses; the stadium is the mod's own pieces. This owns the state and its timing; the
    /// construction game and the programme reach it through Wiring/Wiring.LandmarkLinks and ConstructionLinks.
    /// COST: one time comparison a frame; once a real second, a look at the clock, the save hook and the player's distance
    /// to three fronts; a day's pass and an hour's when they come; pieces stood once a city, out of view.
    /// </summary>
    public sealed partial class LandmarksDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("landmarks", "LandmarksDirector", typeof(LandmarksDirector));

        public const string PlacesOwner = "landmarks";   // its name among the district save's features
        private const int CatchUpHours = 48;             // game hours missed that are still gone through one by one

        public static IModLogger Logger;
        public static LandmarksDirector Instance { get; private set; }

        private static LandmarkState _state = new LandmarkState();
        private static bool _loaded;          // this save's line is read
        private static bool _broken;          // a fault switched the feature off for the session
        private static bool _unread;          // the save's line could not be read: kept as it was, nothing moves
        private static object _save;          // the save the state was read from
        private static string _beforeTests;   // the line before the first test changed it, for "put back"
        private static int _builtStamp;       // changes whenever a landmark is built or the state is read: the lift's cache follows it

        private bool _running;
        private float _next;
        private long _lastHour = -1;
        private Action _onSave;

        /// <summary>True while the landmarks run: their option on, this save's line read, no fault.</summary>
        public static bool Running => Instance != null && Instance._running && _loaded && !_broken && !_unread;

        /// <summary>The state in use; null before it is read.</summary>
        public static LandmarkState State => _loaded ? _state : null;

        private static bool Verbose => Meta.Dev.Verbose;

        // ---------------------------------------------------------------- at game start

        /// <summary>
        /// At game start (Core/RealLifeOverhaulMod), before any save is read: the landmarks' places named to the district save,
        /// and the lift their crowds give the district's lots' traffic, which Uptown asks while the city loads.
        /// </summary>
        public static void Register(IModLogger logger)
        {
            Logger = logger;
            DistrictPlaces.Add(PlacesOwner, Fill);
            DistrictBuildings.TrafficLift = LiftOf;
        }

        // ---------------------------------------------------------------- the city

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _loaded = false;
            ScenePrewarm.AddLate("landmarks: the stadium, the plaza and the forecourt stood, and the lots' traffic", AtLoad);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                LeaveStreet("the city is unloading");
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

        private static readonly int HotUpdate = HotSpots.Slot("Landmarks: LandmarksDirector.Update");
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

        private void Slow()
        {
            if (LandmarksGame.Loading)
            {
                return;
            }
            if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                Load();
            }
            SaveHook.Keep(_onSave);   // the game clears its event hub on every load: kept attached, whatever the option
            if (!Settings.Landmarks || _unread)
            {
                if (_running)
                {
                    StandDown(_unread ? "its line in this save cannot be read" : "its option is off");
                }
                return;
            }
            if (!_running)
            {
                _running = true;
                _lastHour = -1;
                Logger?.Info("Landmarks: running. " + (GoingPublic.InSession ? "A Going Public session: each landmark keeps the city's own timetable on every machine; no tender."
                    : "Solo: each goes out to tender in the construction game while it runs."));
            }
            Clock();
            Street();
        }

        // ---------------------------------------------------------------- the state

        private static void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            LandmarkState state = LandmarkStore.Load(save.modData, LandmarkStore.Key, out int version);
            _unread = state == null;
            _state = state ?? new LandmarkState();
            _beforeTests = save.modData.TryGetValue(LandmarkStore.TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _save = save;
            _loaded = true;
            _builtStamp++;
            _sitesFor = -1;
            Logger?.Info("Landmarks: " + (_unread ? "this save's line cannot be read (a later version's, or damaged): kept as it is, and nothing moves"
                : version > 0 ? "read (line version " + version + "): " + Describe() : "a new save for the landmarks") + ".");
        }

        /// <summary>This save's line forgotten (its modData has just been removed): the next look reads it afresh, a new save's.</summary>
        public static void Forget()
        {
            _loaded = false;
            _beforeTests = null;
            Instance?.LeaveStreet("its line was removed");
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
                if (SaveLines.IsKept(save.modData, LandmarkStore.Key))
                {
                    return;   // a line this version could not read stays as it was read, for the version that can
                }
                save.modData[LandmarkStore.Key] = LandmarkStore.Write(_state);
                if (_beforeTests != null)
                {
                    save.modData[LandmarkStore.TestKey] = _beforeTests;   // "put back" still works after a load
                }
                else
                {
                    save.modData.Remove(LandmarkStore.TestKey);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The line into the save in memory now, so what the city loads next (the district's lift, the district save) reads it.
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
            save.modData[LandmarkStore.Key] = LandmarkStore.Write(_state);
        }

        /// <summary>Everything the landmarks stand in the street put away; the state stays, and the works the timetable runs carry on when it is on again.</summary>
        public void StandDown(string why)
        {
            bool was = _running;
            _running = false;
            LeaveStreet(why);
            if (was)
            {
                Logger?.Info("Landmarks: stood down, " + why + ".");
            }
        }

        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Landmarks: switched off for this session after an error in " + where + "; the state is kept as it was.");
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

        // Every game hour gone by is gone through, a day's pass on the hour that opens a day. A gap longer than two days moves the clock on.
        private void Clock()
        {
            int day = LandmarksGame.Day, hour = LandmarksGame.Hour;
            long now = (long)day * 24 + hour;
            if (_lastHour < 0 || now < _lastHour || now - _lastHour > CatchUpHours)
            {
                _lastHour = now;
                DayPass(day);
                HourPass(day, hour);
                return;
            }
            while (_lastHour < now)
            {
                _lastHour++;
                int d = (int)(_lastHour / 24), h = (int)(_lastHour % 24);
                if (h == 0)
                {
                    DayPass(d);
                }
                HourPass(d, h);
            }
        }

        private void DayPass(int day)
        {
            try
            {
                if (day <= _state.LastDay)
                {
                    RefreshSites();
                    return;   // a day's pass runs once a day, whatever the loads
                }
                _state.LastDay = day;
                DayWork(day);
                Written();
            }
            catch (Exception e)
            {
                Fault("the day's pass", e);
            }
        }

        private void HourPass(int day, int hour)
        {
            try
            {
                HourWork(day, hour);
            }
            catch (Exception e)
            {
                Fault("the hour's pass", e);
            }
        }

        // ---------------------------------------------------------------- the district save's view of it

        // What of the landmarks the district save keeps out of every save: their sites' ground, and the doors of their
        // buildings that stand as the district's (the tower, the mall and its shops), with the district's way out.
        private static void Fill(PlaceSet set)
        {
            if (!Settings.Landmarks || !UptownLots.Ready)
            {
                return;
            }
            RefreshSites();
            UptownExit exit = UptownLots.Exit;
            for (int k = 0; k < Sites.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                if (!SiteKnown[k] || f.Block <= 0)
                {
                    continue;
                }
                SiteRect g = Sites[k].Ground;
                set.Ground(g.X0, g.Z0, g.X1, g.Z1);
            }
            Scratch.Clear();
            DistrictBuildings.Standing(Scratch);
            for (int i = 0; i < Scratch.Count; i++)
            {
                NewBuildings.MadeBuilding made = Scratch[i];
                if (made == null || made.Spec == null || !HoldsBuilding(made.Spec.Id))
                {
                    continue;
                }
                float turn = made.DoorFacing * Mathf.Deg2Rad;
                set.Door(made.Spec.Street, made.Spec.Number, made.DoorX + Mathf.Sin(turn) * 1.2f, made.DoorY, made.DoorZ + Mathf.Cos(turn) * 1.2f, made.DoorFacing, exit.District);
            }
            Scratch.Clear();
        }

        private static readonly List<NewBuildings.MadeBuilding> Scratch = new List<NewBuildings.MadeBuilding>(64);

        // True for a district building of a landmark's: the tower, the mall's main building or one of its shops.
        private static bool HoldsBuilding(string lot)
        {
            for (int k = 0; k < Sites.Length; k++)
            {
                if (SiteKnown[k] && _state.Files[k].Built && Sites[k].Kind != LandmarkKind.Stadium && Sites[k].Lots.Contains(lot))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Describe()
        {
            var t = new System.Text.StringBuilder(160);
            for (int k = 0; k < _state.Files.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                t.Append(k > 0 ? ", " : string.Empty).Append(LandmarkRules.Name(f.Kind)).Append(' ').Append(f.Stage);
                if (f.Block > 0)
                {
                    t.Append(" on block ").Append(f.Block);
                }
            }
            return t.ToString();
        }
    }
}
