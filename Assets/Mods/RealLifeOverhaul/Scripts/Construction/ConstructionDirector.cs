using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game (docs/features/construction.md, "The construction game"): the player's company, its land and
    /// projects from the plans to the keys, tenders, the rival builders and the city's developers. This owns the state and
    /// its timing; every site goes up in the city's construction programme (Scripts/Civic), which asks this how fast. The
    /// state is read at a load whatever the option says (the programme's sites must be known as the builders'), and runs
    /// only while the option is on. Cost: one time comparison a frame; once a second, a look at the clock, the save hook,
    /// the panels at the player's sites and the building the player is in (its front desk); an hour's and a day's passes
    /// over the projects, crews and tenders when they come.
    /// </summary>
    public sealed partial class ConstructionDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("construction", "ConstructionDirector", typeof(ConstructionDirector));

        public const string AppId = "RealLifeBuilders";
        private const int CatchUpHours = 48;   // hours missed that are still worked one by one; a longer gap moves the clock on

        public static IModLogger Logger;
        public static ConstructionDirector Instance { get; private set; }

        private BuildState _state;
        private bool _loaded, _broken;
        private bool _running;
        private long _lastHour = -1;           // day x 24 + hour of the last hour worked
        private float _next;
        private string _beforeTests;
        private object _save;                  // the save the state was read from
        private Action _onSave;

        /// <summary>True once this save's state is read and nothing has stopped the feature.</summary>
        public bool Loaded => _loaded && !_broken;

        /// <summary>True while the construction game runs: its option on, its state read, no fault.</summary>
        public bool Running => _running && _loaded && !_broken;

        /// <summary>The state in use; null before it is read.</summary>
        public BuildState State => _loaded ? _state : null;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                StandDown("the city is unloading");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Construction: ConstructionDirector.Update");
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
            if (ConstructionGame.Loading)
            {
                return;
            }
            if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                Load();
            }
            SaveHook.Keep(_onSave);   // the game clears its event hub on every load: kept attached, whatever the option
            bool on = Settings.Construction && Settings.Civic && Settings.CivicConstruction;
            if (!on)
            {
                if (_running)
                {
                    StandDown(!Settings.Construction ? "its option is off" : "the city's construction sites are off");
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            Clock();
            AppSecond();
            SitesSecond();
            ShowSecond();
            DeskSecond();
        }

        // ------------------------------------------------------------------ the state

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            BuildState state = BuildStore.Load(save.modData, BuildStore.Key, out int version);
            if (state == null)
            {
                // A save new to the construction game: seeded once from the day (the player's company is theirs alone; nothing
                // shared hangs on this seed: the city's developers are drawn from the lot plan and the day).
                state = new BuildState { Seed = BuildRandom.Mix(save.Day * 7919, Environment.TickCount) | 1, FirstDay = save.Day, LastDay = save.Day - 1 };
                state.NextClientDay = save.Day + TenderRules.NextIn(state.Seed, save.Day, TenderSource.Client);
                state.NextPublicDay = save.Day + TenderRules.NextIn(state.Seed, save.Day, TenderSource.Public);
            }
            Retire(state);
            if (state.NextRefitDay <= 0)
            {
                state.NextRefitDay = save.Day + TenderRules.NextIn(state.Seed, save.Day, TenderSource.Refit);   // a line from before the refits' tenders too
            }
            _beforeTests = save.modData.TryGetValue(BuildStore.TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _testLots.Clear();
            _testOpening = false;
            _state = state;
            _save = save;
            _loaded = true;
            _lastHour = -1;
            _lotsStamp = int.MinValue;
            Logger?.Info("Construction: " + (version > 0 ? "read (line version " + version + ")" : "a new save for the construction game") + ": " + state.Projects.Count + " project(s), "
                         + state.Crews.Count + " crew(s), " + state.Lots.Count + " lot(s) owned, " + state.Developments.Count + " development(s) known; the company "
                         + (state.Firm.Founded > 0 ? "founded on day " + state.Firm.Founded : "not founded") + ".");
        }

        // Uptown's lots were planned again (the owner's "Like Manhattan", 24 Oct 2026): land on a lot of the first plan, which
        // is no more, is given back at the price paid, and the developments and open tenders there let go. A project there
        // stays to its end, as a project's site is the city's own.
        private void Retire(BuildState state)
        {
            Func<string, bool> retired = ConstructionBridges.LotRetired;
            if (retired == null)
            {
                return;
            }
            long back = 0;
            int lots = 0, developments = 0, tenders = 0;
            for (int i = state.Lots.Count - 1; i >= 0; i--)
            {
                if (retired(state.Lots[i].Id))
                {
                    back += Math.Max(0L, state.Lots[i].Paid);
                    state.Lots.RemoveAt(i);
                    lots++;
                }
            }
            for (int i = state.Developments.Count - 1; i >= 0; i--)
            {
                if (retired(state.Developments[i].Lot))
                {
                    state.Developments.RemoveAt(i);
                    developments++;
                }
            }
            for (int i = state.Tenders.Count - 1; i >= 0; i--)
            {
                if (state.Tenders[i].State == TenderState.Open && retired(state.Tenders[i].Lot))
                {
                    state.Tenders.RemoveAt(i);
                    tenders++;
                }
            }
            if (lots + developments + tenders == 0)
            {
                return;
            }
            ConstructionGame.Give(back, "reallife_transaction_bld_land_back", ConstructionText.T("reallife_neighborhood_uptown"));
            Logger?.Info("Construction: Uptown's lots were planned again: " + lots + " lot(s) of the first plan given back for " + back + ", " + developments
                         + " development(s) and " + tenders + " open tender(s) there let go.");
        }

        // The state read now if the city's programme asks before the second's look has read it: a site's first hour after a
        // load is worked at its builder's pace, never the city's. False while the game is loading or after a fault.
        private bool Ready()
        {
            if (_broken)
            {
                return false;
            }
            if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                if (ConstructionGame.Loading)
                {
                    return false;
                }
                Load();
            }
            return true;
        }

        /// <summary>This save's state forgotten (its modData has just been removed): the next look reads it afresh, a new save's.</summary>
        public void Forget()
        {
            _loaded = false;
            _beforeTests = null;
            _testLots.Clear();
            _testOpening = false;
            HidePanels();
            DropShowPlace("the save's state is forgotten");
        }

        private void OnSave()
        {
            try
            {
                if (!_loaded || SaveGameManager.Current == null || !ReferenceEquals(_save, SaveGameManager.Current))
                {
                    return;
                }
                if (SaveGameManager.Current.modData == null)
                {
                    SaveGameManager.Current.modData = new Dictionary<string, string>();
                }
                if (SaveLines.IsKept(SaveGameManager.Current.modData, BuildStore.Key))
                {
                    return;   // a line this version could not read stays as it was read, for the version that can
                }
                SaveGameManager.Current.modData[BuildStore.Key] = BuildStore.Write(_state);
                if (_beforeTests != null)
                {
                    SaveGameManager.Current.modData[BuildStore.TestKey] = _beforeTests;   // "put back" still works after a load
                }
                else
                {
                    SaveGameManager.Current.modData.Remove(BuildStore.TestKey);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void StartUp()
        {
            _running = true;
            _lastHour = -1;
            ConstructionType.Register();   // furnished as the game's studio once its type is known
            if (Verbose)
            {
                bool trades = ConstructionBridges.Qualified != null && ConstructionBridges.Commission != null && ConstructionBridges.DesignOf != null;
                Logger?.Info("Construction check (trades): the player's own qualification, the brief at the practice and the design from it are " + (trades ? "wired: PASS" : "not wired: FAIL")
                             + "; the district's lots " + (ConstructionBridges.Lots != null ? "wired" : "not wired (no land for sale)") + ".");
            }
            Logger?.Info("Construction: running. " + (GoingPublic.InSession ? "A Going Public session: " + (GoingPublic.IsGuest ? "a guest's"
                : "the host's") + " company; no land is sold in a session." : "Solo."));
        }

        /// <summary>Everything the construction game shows put away; the state stays (the sites wait, put away, while it is off).</summary>
        public void StandDown(string why)
        {
            bool was = _running;
            _running = false;
            RemoveApp();
            HidePanels();
            DesksDown();
            DropShowPlace("the construction game stood down");
            if (was)
            {
                Logger?.Info("Construction: stood down, " + why + ". Its sites in the city's programme wait, put away, until it is on again.");
            }
        }

        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Construction: switched off for this session after an error in " + where + "; its state is kept as it was.");
            try
            {
                StandDown("after a fault");
            }
            catch (Exception inner)
            {
                Logger?.Error(inner);
            }
        }

        // ------------------------------------------------------------------ the clock

        // Every game hour that has gone by is worked, a day's pass on the hour that opens a day. A gap longer than two days
        // (a long sleep or skip) moves the clock on and works the day it lands on.
        private void Clock()
        {
            int day = ConstructionGame.Day, hour = ConstructionGame.Hour;
            long now = (long)day * 24 + hour;
            if (_lastHour < 0 || now < _lastHour || now - _lastHour > CatchUpHours)
            {
                bool fresh = _lastHour < 0, back = now < _lastHour;
                _lastHour = now;
                if (!fresh)
                {
                    Logger?.Info("Construction: the clock moved " + (back ? "back" : "on more than two days") + "; the day's pass runs once for day " + day + ".");
                }
                Daily(day);
                Hourly(day, hour);
                return;
            }
            while (_lastHour < now)
            {
                _lastHour++;
                int d = (int)(_lastHour / 24), h = (int)(_lastHour % 24);
                if (h == 0)
                {
                    Daily(d);
                }
                Hourly(d, h);
            }
        }

        private void Hourly(int day, int hour)
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

        private void Daily(int day)
        {
            try
            {
                if (day <= _state.LastDay)
                {
                    return;   // a day's pass runs once a day, whatever the loads
                }
                _state.LastDay = day;
                DayWork(day);
                Changed();
            }
            catch (Exception e)
            {
                Fault("the day's pass", e);
            }
        }

        // ------------------------------------------------------------------ the company

        /// <summary>The player's office of the construction company's type, the first in the save's order; false for none.</summary>
        public bool Office(out PlaceView office)
        {
            ConstructionGame.Offices(_offices);
            for (int i = 0; i < _offices.Count; i++)
            {
                if (_state != null && _offices[i].Place == _state.Firm.Office)
                {
                    office = _offices[i];
                    return true;
                }
            }
            office = _offices.Count > 0 ? _offices[0] : default(PlaceView);
            return _offices.Count > 0;
        }

        private readonly List<PlaceView> _offices = new List<PlaceView>(2);

        /// <summary>True when the company may start works and bid: founded, its office there and its licence held.</summary>
        public bool Licensed => _state != null && _state.Firm.Founded > 0 && _state.Firm.Licence == LicenceState.Granted;

        /// <summary>Something changed: the open app is written again and the badge counted.</summary>
        internal void Changed()
        {
            AppChanged();
        }
    }
}
