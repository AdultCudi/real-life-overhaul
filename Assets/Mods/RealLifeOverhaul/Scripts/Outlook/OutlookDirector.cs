using System;
using System.Collections;
using BAModAPI;
using Entities;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app (docs/features/economy-app.md): the city's economy, its jobs, districts, markets and books as dashboards,
    /// with a small daily history of each figure (a line in the save, 120 days) to draw trends from, and advice worked out of the
    /// figures. This director owns the history and the timing: its own timer, once a second, puts the app on the phone, it
    /// writes a day's figures at the game's day event (after the city's economy has done its day), and has the open app written
    /// again each game hour. It only reads the game; nothing is sent in a Going Public session. No Update, so nothing per frame:
    /// once a second a few comparisons, once a day a pass over the figures, once an hour the open page.
    /// </summary>
    public sealed partial class OutlookDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeEconomy";

        public static IModLogger Logger;
        public static OutlookDirector Instance { get; private set; }

        private readonly OutlookReadings _readings = new OutlookReadings();
        private readonly int[] _scratch = new int[OutlookCatalogue.HistoryDays];
        private OutlookBook _book = new OutlookBook();
        private Action _onSave, _onHour, _onDay;
        private GameInstance _loadedFor;     // the save the book was read from
        private int _written = -1;           // the game's day the book was last written for
        private bool _broken;                // a fault: off for the session
        private bool _sample;                // a sample year is shown instead of the save's own history (a test; never saved)

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The history the app draws from: the save's own, or a sample year while the test shows one.</summary>
        internal OutlookBook Book => _sample ? _sampleBook : _book;

        /// <summary>The save's own history, always (what the save writes).</summary>
        internal OutlookBook Kept => _book;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            RemoveApp();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Once a second; its first look after a load waits for its seat on the start line (Core/StartLine).
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
                while (StartLine.Holds(StartSeats.Outlook))
                {
                    yield return null;
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
        }

        private static readonly int HotTick = HotSpots.Slot("Economy app: OutlookDirector.Tick");
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = Save;
            if (save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.EconomyApp)
            {
                RemoveApp();
                return;
            }
            if (!ReferenceEquals(_loadedFor, save))
            {
                Load(save);
            }
            SaveHook.Keep(_onSave);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            if (_written != save.Day && CityEconomy.Running)
            {
                Record();   // the first look of a save, or of a day the day event did not reach (a load in the middle of one)
            }
        }

        private void Load(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new System.Collections.Generic.Dictionary<string, string>();
            }
            OutlookBook book = OutlookStore.Load(save.modData, out int version);
            _book = book ?? new OutlookBook();
            _loadedFor = save;
            _written = -1;
            string found = book != null ? "history loaded (version " + version + ", " + _book.Lines + " figures to day " + _book.LastDay + ")"
                : "no history in this save: it starts today";
            Logger?.Info("Economy app: " + found + ".");
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = Save;
                if (save == null || !ReferenceEquals(_loadedFor, save) || !Settings.EconomyApp || _broken)
                {
                    return;
                }
                save.modData[OutlookCatalogue.Key] = OutlookStore.Write(_book);
            }
            catch (Exception e)
            {
                Fault("a save", e);
            }
        }

        private void OnDay()
        {
            if (_broken || !Settings.EconomyApp || _loadedFor == null || !ReferenceEquals(_loadedFor, Save))
            {
                return;
            }
            try
            {
                if (CityEconomy.Running)
                {
                    Record();
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private void OnHour()
        {
            if (_broken || !Settings.EconomyApp)
            {
                return;
            }
            try
            {
                _view?.Hour();
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        // One look at the game, written into the book as today's figures. Over the day's earlier reading, if there is one.
        private void Record()
        {
            if (!OutlookGame.Read(_readings))
            {
                return;
            }
            int day = _readings.Day;
            if (day <= 0)
            {
                return;
            }
            Write(_book, _readings, day);
            _written = day;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Economy check (app history, day " + day + "): " + _book.Lines + " figure(s) kept, the newest day " + _book.LastDay
                             + ", " + _readings.Districts.Count + " district(s): PASS");
            }
        }

        /// <summary>Writes a day's readings into a book, one figure each; a figure the mod does not keep now is left out.</summary>
        internal static void Write(OutlookBook book, OutlookReadings r, int day)
        {
            if (r.CityOn)
            {
                book.Set(OutlookCatalogue.Prices, day, r.Prices);
                book.Set(OutlookCatalogue.Inflation, day, r.Inflation);
                book.Set(OutlookCatalogue.Rate, day, r.Rate);
                book.Set(OutlookCatalogue.Cycle, day, r.Cycle);
                book.Set(OutlookCatalogue.Income, day, r.Income);
                book.Set(OutlookCatalogue.Spend, day, r.Spend);
                book.Set(OutlookCatalogue.Footfall, day, r.Footfall);
                book.Set(OutlookCatalogue.Property, day, r.Property);
                int growing = 0;
                bool any = false;
                for (int i = 0; i < r.Districts.Count; i++)
                {
                    OutlookDistrict d = r.Districts[i];
                    book.Set(OutlookCatalogue.OfDistrictIncome(d.Id), day, d.Income);
                    if (d.Level > 0)
                    {
                        book.Set(OutlookCatalogue.OfDistrictLevel(d.Id), day, d.Level);
                    }
                    for (int u = 0; u < d.Demand.Length; u++)
                    {
                        if (d.Demand[u] >= 0)
                        {
                            book.Set(OutlookCatalogue.OfDistrictDemand(d.Id, u), day, d.Demand[u]);
                        }
                    }
                    if (d.Growing >= 0)
                    {
                        growing += d.Growing;
                        any = true;
                    }
                }
                if (any)
                {
                    book.Set(OutlookCatalogue.Growing, day, growing);
                }
            }
            if (r.JobsOn)
            {
                book.Set(OutlookCatalogue.Unemployment, day, r.Unemployment);
                book.Set(OutlookCatalogue.Vacancies, day, r.Vacancies);
            }
            if (r.CouncilOn)
            {
                book.Set(OutlookCatalogue.Reserve, day, OutlookRules.ThousandsOf(r.Reserve));
                book.Set(OutlookCatalogue.Revenue, day, OutlookRules.ThousandsOf(r.Revenue));
                book.Set(OutlookCatalogue.Spending, day, OutlookRules.ThousandsOf(r.Spending));
            }
        }

        private readonly OutlookReadings _glance = new OutlookReadings();

        /// <summary>The verdict on the economy as the Finance app's line says it, in the game's language; null while the city's economy is off.</summary>
        public string VerdictWord()
        {
            if (_broken || !Settings.EconomyApp || !CityEconomy.Running)
            {
                return null;
            }
            try
            {
                if (!OutlookGame.Read(_glance) || !_glance.CityOn)
                {
                    return null;
                }
                if (_sample)
                {
                    ShowSample(_glance);
                }
                OutlookVerdict verdict = OutlookStudy.Run(Book, _glance, _scratch);
                return OutlookText.Words.VerdictWord(verdict);
            }
            catch (Exception e)
            {
                Fault("the verdict", e);
                return null;
            }
        }

        /// <summary>The figures of this moment, read now and written as today's, for the open page: false while there is nothing to read.</summary>
        internal bool Look(OutlookReadings into)
        {
            if (!OutlookGame.Read(into))
            {
                return false;
            }
            if (_sample)
            {
                ShowSample(into);   // a sample year stands in for the history, and for the city's figures it ends on
                return true;
            }
            if (ReferenceEquals(_loadedFor, Save) && into.Day > 0)
            {
                Write(_book, into, into.Day);
                _written = into.Day;
            }
            return true;
        }

        private void Fault(string where, Exception e)
        {
            if (_broken)
            {
                return;
            }
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Economy app: switched off for this session after an error in " + where + ".");
            try
            {
                RemoveApp();
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
        }
    }
}
