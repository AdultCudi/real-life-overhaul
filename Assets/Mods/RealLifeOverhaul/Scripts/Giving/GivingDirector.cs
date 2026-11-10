using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// Giving: donations to the city's charities and money for the people the player knows (docs/features/giving.md). It owns the state and its timing. A second at a time it looks
    /// whether a save is ready (and loads or lets go of its line); a game day it goes through once (the tax year, the deduction, the loans' parts) and a game month in steps of
    /// a second each (standing gifts, the rivals' gifts, the three charities' pay-outs, the neighbours). Money moves only through <see cref="GivingGame"/>, which books it through
    /// the mod's one call. No Update. COST: nothing per frame; a few comparisons a second, a pass over the households or people once a month.
    /// </summary>
    public sealed partial class GivingDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static GivingDirector Instance { get; private set; }
        public const string TestKey = "reallife:giving_test";

        private static readonly int HotDay = HotSpots.Slot("Giving: the day's pass");

        private GivingState _state;
        private bool _loaded, _broken;
        private Action _onSave;
        private int _monthStep = -1;     // the step of the month's pass under way; -1 for none
        private int _monthNumber;
        private int _revision;
        private string _beforeTests;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            GivingGame.Logger = logger;
            root.AddComponent<GivingDirector>();
        }

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Settings.Giving;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public GivingState State => _state;

        /// <summary>Moves whenever something a page shows may have changed.</summary>
        public int Revision => _revision;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Look();
                }
                catch (Exception e)
                {
                    _broken = true;
                    Logger?.Error(e);
                    Logger?.Warn("Giving: switched off for this session after the error above. The save keeps what was given and lent as it was.");
                }
            }
        }

        private void Look()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.Giving)
            {
                if (_loaded)
                {
                    PutAway();
                }
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            SaveHook.Keep(_onSave);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            if (_monthStep >= 0)
            {
                MonthStep();
                return;
            }
            int day = GivingGame.Day;
            if (day != _state.LastDay)
            {
                Pass(day);
            }
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = GivingStore.Load(save.modData, out int version) ?? new GivingState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: every rival's gift and every ask comes from it
            }
            int day = GivingGame.Day;
            for (int i = 0; i < _state.Loans.Count; i++)
            {
                if (_state.Loans[i].State == LoanState.Open && _state.Loans[i].Next < day)
                {
                    _state.Loans[i].Next = day;   // a load or the option back on: no month of the time away is held against the person
                }
            }
            _state.LastDay = 0;
            _monthStep = -1;
            _beforeTests = save.modData.TryGetValue(TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _loaded = true;
            _revision++;
            Logger?.Info("Giving: " + (version > 0 ? "read the save's line (version " + version + "): " + _state.Loans.Count + " loan(s), " + _state.Standing.Count
                         + " standing gift(s)" : "nothing given yet") + ".");
        }

        private void OnSave()
        {
            try
            {
                if (_loaded)
                {
                    Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[GivingStore.Key] = GivingStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(TestKey);
            }
        }

        // The option was switched off: the line stays as it is, nothing runs, standing gifts stop and loans stay recorded.
        private void PutAway()
        {
            Write();
            _loaded = false;
            _monthStep = -1;
            _revision++;
            Logger?.Info("Giving: switched off; the gifts, standing gifts and loans are kept in the save and go on when it is on again.");
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(GivingStore.Key);
                save.modData.Remove(TestKey);
            }
            _state = new GivingState();
            _beforeTests = null;
            _loaded = false;
            _monthStep = -1;
            _revision++;
        }

        // ---- the day

        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            _state.LastDay = day;
            Roll(day);
            int month = GivingRules.MonthOf(day, GivingGame.MonthDays);
            if (_state.Month < 0)
            {
                _state.Month = month;
                _state.StandingMonth = month;
            }
            else if (month > _state.Month)
            {
                _monthNumber = month;
                _monthStep = 0;
            }
            Claim(day);
            CollectLoans(day);
            ExpireNotes(day);
            _revision++;
        }

        // The game's tax year turns over: the year's sums start again.
        private void Roll(int day)
        {
            int start = GivingRules.PeriodStart(day, GivingGame.DaysPerYear);
            if (start == _state.PeriodStart)
            {
                return;
            }
            bool first = _state.PeriodStart == 0;
            _state.PeriodStart = start;
            if (first)
            {
                return;
            }
            _state.TaxGifts = 0;
            _state.TaxClaimed = 0;
            _state.NeighbourYear = 0;
            _state.NeighbourHelped = 0;
            for (int i = 0; i < _state.Books.Length; i++)
            {
                CharityBook b = _state.Books[i];
                b.PlayerYear = 0;
                b.OthersYear = 0;
                b.PaidYear = 0;
                b.PeopleYear = 0;
            }
            for (int i = 0; i < _state.RivalBooks.Length; i++)
            {
                _state.RivalBooks[i].Year = 0;
            }
            if (Verbose)
            {
                Logger?.Info("Giving check (a new tax year from day " + start + "): the year's sums start again: PASS");
            }
        }

        /// <summary>
        /// The deduction: the year's gifts to charities, held to the cap on the income so far, less what the game's tax already deducts under giving's name (read from its own
        /// list, so a claim is never made twice), given to the game's tax. Never on the day the game closes its year and clears its list.
        /// </summary>
        private void Claim(int day)
        {
            int dpy = GivingGame.DaysPerYear;
            if (GivingRules.Boundary(day, dpy) || _state.TaxGifts <= 0)
            {
                return;
            }
            long income = GivingBridges.Income != null && day - 1 >= _state.PeriodStart ? GivingBridges.Income(_state.PeriodStart, day - 1) : 0L;
            long deducted = GivingGame.Deducted();
            long due = GivingRules.DeductionDue(_state.TaxGifts, deducted, income);
            _state.TaxClaimed = deducted;
            if (due <= 0)
            {
                return;
            }
            GivingGame.Deduct(due);
            _state.TaxClaimed = deducted + due;
            if (Verbose)
            {
                Logger?.Info("Giving check (the deduction on day " + day + "): gifts " + _state.TaxGifts + ", income so far " + income + ", the cap " + GivingRules.DeductionCap(income)
                             + ", deducted before " + deducted + ", now " + due + " more: PASS");
            }
        }

        private void ExpireNotes(int day)
        {
            for (int i = _state.Notes.Count - 1; i >= 0; i--)
            {
                if (day - _state.Notes[i].Day > GivingCatalogue.PaperKept)
                {
                    _state.Notes.RemoveAt(i);
                }
            }
        }

        /// <summary>A result said in the game's way: its notice.</summary>
        internal static void Say(GiveResult r, Dictionary<string, string> data = null)
        {
            GivingGame.Tell(GivingResults.Moved(r) ? NotificationType.Info : NotificationType.Warning, GivingResults.KeyOf(r), data);
        }
    }
}
