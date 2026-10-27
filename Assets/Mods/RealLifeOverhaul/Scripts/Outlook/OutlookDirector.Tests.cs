using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Economy;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// Part of the Economy app's director: the tests on the options page (Options > Tests). Each says in Player.log what it measured
    /// and PASS or FAIL, and in a notice what it found.
    /// </summary>
    public sealed partial class OutlookDirector
    {
        private const int SampleDay = 200;   // the day the sample year ends on: the game's own day may be too early for 120 days behind it
        private OutlookBook _sampleBook;
        private bool _testing;

        internal bool SampleShown => _sample;
        internal bool CityRuns => CityEconomy.Running;

        /// <summary>The day the app draws as today: the sample's last day while a sample year is shown, else the game's.</summary>
        internal int Today(int gameDay) => _sample ? SampleDay : gameDay;

        private static void Tell(bool good, string key, string a = null, string b = null)
        {
            var data = new Dictionary<string, string>(3);
            if (a != null)
            {
                data["a"] = a;
            }
            if (b != null)
            {
                data["b"] = b;
            }
            Notifications.Show(good ? NotificationType.Success : NotificationType.Error, key, data, 10f, null, null, true, false);
        }

        // ------------------------------------------------------------------ every tab

        /// <summary>Opens the app and goes through every tab: each must draw, its charts must have points where the figures are kept, and the advice must read the game's own figures.</summary>
        public void TestTabs()
        {
            if (_testing)
            {
                return;
            }
            if (_broken || !Settings.EconomyApp || !Installed || _view == null)
            {
                Logger?.Info("Economy app check (tabs): the app is not on the phone, so there is nothing to open: FAIL");
                Tell(false, "reallife_econapp_test_not_on");
                return;
            }
            _testing = true;
            StartCoroutine(Guard.Routine(TabsRoutine()));
        }

        private IEnumerator TabsRoutine()
        {
            var said = new StringBuilder(512);
            int faults = 0, tabs = 0, charts = 0, withPoints = 0;
            try
            {
                for (int tab = 0; tab < OutlookTab.Count; tab++)
                {
                    if (!Open(tab))
                    {
                        faults++;
                        said.Append(" tab ").Append(tab).Append(" did not open;");
                        continue;
                    }
                    yield return null;
                    yield return null;
                    OutlookApp.Drawn d = _view != null ? _view.Last : default(OutlookApp.Drawn);
                    OutlookReadings r = _view != null ? _view.Readings : null;
                    tabs++;
                    bool drew = d.Tab == tab && d.Cards > 0 && d.Words > 0;
                    // A tab whose figures the mod keeps must show a line of them.
                    bool wants = r != null && FiguresKept(tab, r);
                    bool lines = !wants || (d.Charts > 0 && d.Readings > 0);
                    charts += d.Charts;
                    withPoints += d.Readings > 0 ? 1 : 0;
                    if (!drew || !lines)
                    {
                        faults++;
                        said.Append(" tab ").Append(tab).Append(!drew ? " drew nothing" : " has no line").Append(" (").Append(d.Cards).Append(" cards, ").Append(d.Words).Append(" texts, ")
                            .Append(d.Charts).Append(" charts, ").Append(d.Readings).Append(" readings);");
                    }
                }
                List<string> wrong = AdviceMistakes();
                if (wrong.Count > 0)
                {
                    faults += wrong.Count;
                    said.Append(" advice that does not read the game's figures: ").Append(string.Join("; ", wrong)).Append(';');
                }
                bool pass = faults == 0 && tabs == OutlookTab.Count;
                Logger?.Info("Economy app check (tabs): " + tabs + " of " + OutlookTab.Count + " tab(s) drew, " + charts + " chart(s), " + withPoints + " tab(s) with points, "
                             + (_view != null ? _view.Advice.Count : 0) + " piece(s) of advice checked against the game's figures: " + (pass ? "PASS" : "FAIL," + said));
                Tell(pass, pass ? "reallife_econapp_test_tabs_pass" : "reallife_econapp_test_tabs_fail", tabs.ToString(CultureInfo.InvariantCulture), faults.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                _testing = false;
            }
        }

        // Whether the mod keeps figures that a tab draws as a line, now.
        private static bool FiguresKept(int tab, OutlookReadings r)
        {
            switch (tab)
            {
                case OutlookTab.Overview:
                case OutlookTab.Prices:
                case OutlookTab.Districts:
                    return r.CityOn;
                case OutlookTab.Jobs: return r.CityOn && r.JobsOn;
                case OutlookTab.Markets: return r.MarketOn;
                case OutlookTab.Finances: return r.CouncilOn;
                default: return false;
            }
        }

        // ------------------------------------------------------------------ the advice against the game

        /// <summary>
        /// Each piece of the app's advice checked against the game's own figures, read afresh and apart from the app's: the figures in the
        /// advice are the city economy's, the market's and the table's. The names of the ones that are not.
        /// </summary>
        internal List<string> AdviceMistakes()
        {
            var wrong = new List<string>(4);
            CityEconomyDirector city = CityEconomyDirector.Instance;
            CityState s = city != null ? city.State : null;
            if (_view == null || s == null)
            {
                return wrong;
            }
            OutlookWords words = OutlookText.Words;
            foreach (OutlookInsight i in _view.Advice)
            {
                string head = words.Head(i);
                bool ok = head.IndexOf('{') < 0;
                switch (i.Rule)
                {
                    case OutlookInsights.RateUp:
                    case OutlookInsights.RateDown:
                        ok &= Math.Abs(s.LastMove) == i.A && OwnBankCatalogue.CityRateStart + s.Base * 25 == i.B;
                        break;
                    case OutlookInsights.RateAheadUp:
                    case OutlookInsights.RateAheadDown:
                        ok &= s.Pending && Math.Abs(s.PendingMove) == i.A && CityModel.DaysToTurn(s, city.MonthDays) == i.B;
                        break;
                    case OutlookInsights.PricesHigh:
                    case OutlookInsights.PricesLow:
                        ok &= s.Inflation == i.A;
                        break;
                    case OutlookInsights.Boom:
                    case OutlookInsights.Slump:
                        ok &= CityModel.CycleFootfall(s.Cycle, city.Level) == i.A && (CityModel.Spend(s.Cycle, city.Level) - 1000) / 10 == i.B;
                        break;
                    case OutlookInsights.JobsTight:
                    case OutlookInsights.JobsSlack:
                        ok &= OutlookBridges.Jobs != null && OutlookBridges.Jobs((int)s.Cycle, out int unemployment, out _, out _, out _) && unemployment == i.A;
                        break;
                    case OutlookInsights.IncomeUp:
                    case OutlookInsights.IncomeDown:
                    case OutlookInsights.WorthUp:
                    case OutlookInsights.WorthDown:
                    case OutlookInsights.DemandHigh:
                    case OutlookInsights.DemandLow:
                    case OutlookInsights.Building:
                        ok &= s.District(i.Name) != null;
                        if (i.Rule == OutlookInsights.IncomeUp || i.Rule == OutlookInsights.IncomeDown)
                        {
                            ok &= s.District(i.Name) != null && s.District(i.Name).Income == i.B;
                        }
                        break;
                    case OutlookInsights.Shortage:
                    case OutlookInsights.Glut:
                        ok &= i.A >= 0 && i.A < s.Supply.Length && s.Supply[i.A].Shock != SupplyShock.None && Math.Abs(CityModel.ShockPercent(s.Supply[i.A], city.Level)) == i.B;
                        break;
                    default:
                        break;
                }
                if (!ok)
                {
                    wrong.Add(OutlookInsights.Names[i.Rule]);
                }
            }
            return wrong;
        }

        /// <summary>Writes the readings and the advice in the log, and says how many pieces of advice there are.</summary>
        public void TestAdvice()
        {
            try
            {
                var r = new OutlookReadings();
                if (!Look(r))
                {
                    Tell(false, "reallife_econapp_test_not_on");
                    return;
                }
                OutlookVerdict verdict = OutlookStudy.Run(Book, r, _scratch);
                var advice = new List<OutlookInsight>();
                OutlookInsights.Build(r, advice);
                var text = new StringBuilder(1024);
                text.Append("Economy app: day ").Append(r.Day).Append(", the verdict ").Append(verdict).Append(" (cycle ").Append(r.Cycle).Append(", inflation ").Append(r.Inflation / 100.0)
                    .Append("%, prices ").Append(r.Prices / 1000.0).Append(", bank rate ").Append(r.RateBp / 100.0).Append("%, income ").Append(r.Income / 10.0).Append("), ")
                    .Append(advice.Count).Append(" piece(s) of advice, the history ").Append(Kept.Lines).Append(" figure(s) to day ").Append(Kept.LastDay).Append(':');
                OutlookWords words = OutlookText.Words;
                for (int i = 0; i < advice.Count; i++)
                {
                    OutlookInsight a = advice[i];
                    text.Append("\n  ").Append(i + 1).Append(". [").Append(a.Kind).Append(' ').Append(a.Score).Append("] ").Append(words.Head(a)).Append(' ').Append(words.Mean(a));
                }
                Logger?.Info(text.ToString());
                Tell(true, "reallife_econapp_test_advice_done", advice.Count.ToString(CultureInfo.InvariantCulture), Kept.LastDay.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ------------------------------------------------------------------ a sample year

        /// <summary>Shows a sample year of the city's economy in the app instead of the save's own history (never saved); pressed again, the save's own.</summary>
        public void TestSample()
        {
            try
            {
                if (_broken || !Installed || _view == null)
                {
                    Tell(false, "reallife_econapp_test_not_on");
                    return;
                }
                if (_sample)
                {
                    TestBack();
                    return;
                }
                var r = new OutlookReadings();
                if (!OutlookGame.Read(r) || !r.CityOn)
                {
                    Tell(false, "reallife_econapp_test_no_city");
                    return;
                }
                _sampleBook = MakeSample(r);
                _sample = true;
                Logger?.Info("Economy app: a sample year is shown (" + _sampleBook.Lines + " figures to day " + _sampleBook.LastDay
                             + "); it is never saved. Press the test again to put the save's own back.");
                Open(OutlookTab.Overview);
                Tell(true, "reallife_econapp_test_sample_on");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The save's own history is shown again.</summary>
        public void TestBack()
        {
            if (!_sample)
            {
                return;
            }
            _sample = false;
            _sampleBook = null;
            _view?.Refresh();
            Tell(true, "reallife_econapp_test_back");
        }

        // The city's own model run for 130 days from a fixed seed over the districts the game has, recorded as a day's figures are.
        private OutlookBook MakeSample(OutlookReadings now)
        {
            CityState state = CityModel.Open(20261027, 1);
            for (int i = 0; i < now.Districts.Count; i++)
            {
                OutlookDistrict d = now.Districts[i];
                CityModel.Know(state, d.Id, d.Start);
            }
            var book = new OutlookBook();
            var r = new OutlookReadings();
            int level = MacroCatalogue.LevelDefault;
            for (int day = 2; day <= SampleDay; day++)
            {
                CityModel.Day(state, day, new CityDay(), level, 5);
                if (day < SampleDay - OutlookCatalogue.HistoryDays + 1)
                {
                    continue;
                }
                r.Begin();
                r.CityOn = true;
                r.Day = day;
                r.Level = level;
                r.Cycle = (int)state.Cycle;
                r.Prices = state.Index;
                r.Inflation = state.Inflation;
                r.Rate = state.Base;
                r.Footfall = CityModel.CycleFootfall(state.Cycle, level);
                r.Spend = CityModel.Spend(state.Cycle, level);
                long sum = 0, worth = 0;
                for (int i = 0; i < state.Districts.Count; i++)
                {
                    CityDistrict cd = state.Districts[i];
                    OutlookDistrict d = r.NextDistrict();
                    d.Id = cd.Name;
                    d.Income = cd.Income;
                    d.Start = cd.Start;
                    d.Property = CityModel.Property(state, cd, level);
                    sum += cd.Income;
                    worth += d.Property;
                }
                r.Done();
                int n = Math.Max(1, state.Districts.Count);
                r.Income = (int)(sum / n);
                r.Property = (int)(worth / n);
                OutlookBridges.JobsReader jobs = OutlookBridges.Jobs;
                if (jobs != null && jobs(r.Cycle, out int unemployment, out int vacancies, out int seekers, out int layoffs))
                {
                    r.JobsOn = true;
                    r.Unemployment = unemployment;
                    r.Vacancies = vacancies;
                }
                Write(book, r, day);
            }
            _sampleState = state;
            return book;
        }

        private CityState _sampleState;

        /// <summary>Puts the sample's last day over the readings of the game, so a tile's figure and its line end together.</summary>
        internal void ShowSample(OutlookReadings into)
        {
            CityState s = _sampleState;
            if (!_sample || s == null)
            {
                return;
            }
            int level = MacroCatalogue.LevelDefault;
            into.Day = SampleDay;
            into.Cycle = (int)s.Cycle;
            into.MonthsIn = s.MonthsIn;
            into.Prices = s.Index;
            into.Inflation = s.Inflation;
            into.Rate = s.Base;
            into.RateBp = OwnBankCatalogue.CityRateStart + s.Base * 25;
            into.MonthChange = s.MonthChange;
            into.LastMove = s.LastMove;
            into.Pending = s.Pending;
            into.PendingMove = s.PendingMove;
            into.Footfall = CityModel.CycleFootfall(s.Cycle, level);
            into.Spend = CityModel.Spend(s.Cycle, level);
            into.Takings = CityModel.Takings(s, level);
            into.Costs = CityModel.Costs(s);
            long sum = 0, worth = 0;
            for (int i = 0; i < into.Districts.Count; i++)
            {
                OutlookDistrict d = into.Districts[i];
                CityDistrict cd = s.District(d.Id);
                if (cd == null)
                {
                    continue;
                }
                d.Income = cd.Income;
                d.Before = cd.Before;
                d.Footfall = CityModel.DistrictFootfall(cd);
                d.Property = OutlookRules.Worth((int)s.Cycle, s.Index, cd.Income, cd.Start, level, d.Level);
                sum += d.Income;
                worth += d.Property;
            }
            int n = Math.Max(1, into.Districts.Count);
            into.Income = (int)(sum / n);
            into.Property = (int)(worth / n);
            OutlookRules.Rank(into);
            OutlookBridges.JobsReader jobs = OutlookBridges.Jobs;
            if (jobs != null && jobs(into.Cycle, out int unemployment, out int vacancies, out int seekers, out int layoffs))
            {
                into.JobsOn = true;
                into.Unemployment = unemployment;
                into.Vacancies = vacancies;
                into.Seekers = seekers;
                into.Layoffs = layoffs;
            }
        }
    }
}
