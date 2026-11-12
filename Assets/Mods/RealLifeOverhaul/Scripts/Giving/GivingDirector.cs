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
            if (_state.MonthStep >= 0)
            {
                MonthStep();   // a pass the save, the load or the option off left in the middle goes on at its step
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
            _state.PeriodStart = GivingYear.FromSaved(_state.PeriodStart, GivingGame.DaysPerYear);
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

        // The option was switched off: the line stays as it is (a month's pass under way with its step), nothing runs, standing gifts stop, loans stay recorded and the app
        // leaves the phone.
        private void PutAway()
        {
            Write();
            _loaded = false;
            RemoveApp();
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
            RemoveApp();
            _revision++;
        }

        // ---- the day

        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            _state.LastDay = day;
            Roll(day);
            GivingMonth.Begin(_state, GivingRules.MonthOf(day, GivingGame.MonthDays));
            ClaimNow();
            CollectLoans(day);
            ExpireNotes(day);
            _revision++;
        }

        // The game's tax year turns over, on the day it clears its list: the year's sums start again. Asked by the day's pass and before every gift.
        private void Roll(int day)
        {
            if (GivingYear.Turn(_state, day, GivingGame.DaysPerYear) && Verbose)
            {
                Logger?.Info("Giving check (a new tax year from day " + _state.PeriodStart + "): the year's sums start again: PASS");
            }
        }

        // The income the game's tax counts so far this year: the sales of the days after the clearing day up to yesterday.
        private long YearIncome(int day)
        {
            return GivingBridges.Income != null && GivingYear.Window(_state, day, out int from, out int to) ? GivingBridges.Income(from, to) : 0L;
        }

        private static readonly Func<long> HoldsNow = GivingGame.Deducted;
        private static readonly Action<long> DeductNow = GivingGame.Deduct;

        /// <summary>
        /// The deduction (GivingYear.ClaimToday): the year's gifts to charities, held to the cap on the income so far, less what is claimed already (the game's list under giving's
        /// name, and giving's own record, whichever is more), given to the game's tax. Made at once for a gift, so one on the last day of the year is in the list before the game reads
        /// it, and again each day as the income grows.
        /// </summary>
        private void ClaimNow()
        {
            int day = GivingGame.Day;
            long due = GivingYear.ClaimToday(_state, day, GivingBridges.Income, HoldsNow, DeductNow);
            if (due > 0 && Verbose)
            {
                Logger?.Info("Giving check (the deduction on day " + day + "): gifts " + _state.TaxGifts + ", income so far " + YearIncome(day) + ", the cap "
                             + GivingRules.DeductionCap(YearIncome(day)) + ", " + due + " more claimed, " + _state.TaxClaimed + " in all: PASS");
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
