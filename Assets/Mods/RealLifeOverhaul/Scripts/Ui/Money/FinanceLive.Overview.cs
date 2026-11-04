using System;
using System.Collections.Generic;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class FinanceLive
    {
        private readonly List<DebtLine> _debts = new List<DebtLine>(6);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private readonly float[] _values = new float[DaySeriesCatalogue.Days];
        private readonly int[] _days = new int[DaySeriesCatalogue.Days];

        private static string Label(string key) => FinanceText.T(key).TrimEnd(' ', ':');

        private static UiIcon KindIcon(int kind)
        {
            switch (kind)
            {
                case FinanceTables.FirstProduct + (int)ProductKind.Overdraft: return UiIcon.Wallet;
                case FinanceTables.FirstProduct + (int)ProductKind.CreditCard: return UiIcon.Coin;
                case FinanceTables.FirstProduct + (int)ProductKind.CarFinance: return UiIcon.Car;
                case FinanceTables.FirstProduct + (int)ProductKind.Mortgage: return UiIcon.Home;
                case FinanceTables.FirstProduct + (int)ProductKind.Arrears: return UiIcon.Warning;
                case FinanceTables.Private: return UiIcon.Handshake;
                default: return UiIcon.Bank;
            }
        }

        // ------------------------------------------------------------------------------------------------- Overview

        private void ReadOverview(FinanceFacts f, FinanceView v)
        {
            FinanceOverviewFacts o = f.Overview;
            GameInstance save = SaveGameManager.Current;
            CreditFile file = CreditLedger.File;
            UtilitiesWords w = W;
            string player = RivalGame.PlayerName();
            FaceSources.Player(player);   // the HUD's own picture of the player character, drawn under their name by the host
            o.Player = player;
            var rows = new List<FinanceRow>(8);
            var sums = new float[FinanceTables.Approval + 1];
            float owed = DebtRows(rows, sums, file, save);
            o.Debts = FinanceKitTables.Make(FinanceKitTables.DebtColumns(w), rows, -1, w.T("reallife_fin_no_debts", "You owe nothing"), string.Empty, UiIcon.Bank);
            o.Cash = Money(save.Money);
            o.Owed = Money(owed);
            o.Worth = Money(Books.NetWorth());
            Obligations due = Books.Tomorrow(file);
            o.Leaves = Money(due.Total);
            o.LeavesNote = due.Total > 0f ? FinanceText.T("reallife_money_leaves", "money", Money(due.Total)) : null;
            var labels = new List<string>(5);
            var values = new List<string>(5);
            Next(labels, values, "reallife_econ_over_loan_payments", due.Loans);
            Next(labels, values, "reallife_econ_over_accounts", due.Accounts);
            Next(labels, values, "reallife_econ_over_fines", due.Other);
            Next(labels, values, "reallife_econ_over_rent", due.Rent);
            Next(labels, values, "reallife_econ_over_wages", due.Wages);
            RateReview(labels, values, save.Day, w);
            o.RiseNote = file.RiseDay > 0 && file.RiseExtra > 0
                ? w.F("reallife_fin_rise", "Your variable debts cost {money} more a month after the last rise, on day {day}.",
                    "money", Money((float)file.RiseExtra * NewsRules.DaysMonth), "day", file.RiseDay.ToString(Inv)) : null;
            o.NextLabels = labels.ToArray();
            o.NextValues = values.ToArray();
            int kinds = 0;
            for (int i = 0; i < sums.Length; i++)
            {
                kinds += sums[i] > 0f ? 1 : 0;
            }
            o.Donut = new float[kinds];
            o.DonutLabels = new string[kinds];
            o.DonutValues = new string[kinds];
            int at = 0;
            for (int i = 0; i < sums.Length; i++)
            {
                if (sums[i] > 0f)
                {
                    o.Donut[at] = sums[i];
                    o.DonutLabels[at] = FinanceText.T(FinanceTables.DebtKinds[i]);
                    o.DonutValues[at] = FinanceRules.Share(sums[i], owed).ToString(Inv) + "%";
                    at++;
                }
            }
            City(o);
            o.Cash30 = CashChart(w, 30);
            CreditScore score = CreditLedger.Score;
            o.BestOn = BestOffer(score, out string name, out string line);
            o.BestName = name;
            o.BestLine = line;
        }

        private static void Next(List<string> labels, List<string> values, string key, float amount)
        {
            if (amount > 0f)
            {
                labels.Add(Label(key));
                values.Add(Money(amount));
            }
        }

        // The central bank's next rate review, a line of the list of what leaves next: the day it takes effect, and what is announced for it if it is.
        private static void RateReview(List<string> labels, List<string> values, int today, UtilitiesWords w)
        {
            CityEconomyDirector city = CityEconomyDirector.Instance;
            CityState state = city != null ? city.State : null;
            if (state == null)
            {
                return;
            }
            int day = today + CityModel.DaysToTurn(state, city.MonthDays);
            string value = FinanceText.Day(day);
            if (state.Pending)
            {
                value += ", " + (state.PendingMove == 0 ? w.T("reallife_fin_review_hold", "held") : (state.PendingMove > 0 ? "+" : "-") + CityText.Points(state.PendingMove));
            }
            labels.Add(w.T("reallife_fin_review", "Rate review"));
            values.Add(value);
        }

        // The loans the game keeps, the mod's own accounts, the private lender and what other features owe: a row each, and the total.
        private float DebtRows(List<FinanceRow> rows, float[] sums, CreditFile file, GameInstance save)
        {
            float total = 0f;
            UtilitiesWords w = W;
            List<Loan> loans = save.Loans;
            for (int i = 0; i < loans.Count; i++)
            {
                Loan loan = loans[i];
                int kind = FinanceTables.OfLoan(PurposeOf(file, loan));
                float perDay = GameLoans.Due(loan);
                int days = loan.dailyPayment > 0 ? (int)Math.Ceiling(loan.remainingAmount / loan.dailyPayment) : 0;
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T(FinanceTables.DebtKinds[kind]), Sub = GameLoans.BankName(loan), Icon = KindIcon(kind),
                    Cells = new[] { Money(loan.remainingAmount), YieldRate(FinanceApp.YearlyRate(loan, Math.Max(1, AccountRules.DaysPerYear))), perDay > 0f ? Money(perDay) : string.Empty,
                        days > 0 ? FinanceText.T("reallife_econ_row_days", "days", days.ToString(Inv)) : string.Empty }
                });
                total += loan.remainingAmount;
                sums[kind] += loan.remainingAmount;
            }
            int today = save.Day;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (!Books.Owing(a))
                {
                    continue;
                }
                int kind = FinanceTables.Of(a.Kind);
                string left = ProductCatalogue.Instalment(a.Kind) && a.Instalment > 0
                    ? FinanceText.T("reallife_econ_row_days", "days", ((int)Math.Ceiling(a.Owed / (float)a.Instalment)).ToString(Inv)) : FinanceText.State(a);
                float perDay = AccountRules.DueTomorrow(a, today);
                rows.Add(new FinanceRow
                {
                    Name = CreditDesk.Name(a), Sub = CreditDesk.LenderName(a), Icon = KindIcon(kind),
                    Cells = new[] { Money(a.Owed), a.RateBp > 0 ? Rate(a.RateNow) : string.Empty, perDay > 0f ? Money(perDay) : string.Empty, left }
                });
                total += a.Owed;
                sums[kind] += a.Owed;
            }
            if (file.Shark.Active)
            {
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T("reallife_econ_kind_private"), Sub = FinanceText.T("reallife-lender"), Icon = UiIcon.Handshake,
                    Cells = new[] { Money(file.Shark.Owed), string.Empty, string.Empty, FinanceText.T("reallife_econ_row_due_day", "day", file.Shark.DueDay.ToString(Inv)) }
                });
                total += file.Shark.Owed;
                sums[FinanceTables.Private] += file.Shark.Owed;
            }
            _debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(_debts);
            for (int i = 0; i < _debts.Count; i++)
            {
                DebtLine d = _debts[i];
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T(d.NameKey), Sub = FinanceText.T(FinanceTables.DebtKinds[FinanceTables.Other]), Icon = UiIcon.Scales,
                    Cells = new[] { Money(d.Owed), string.Empty, d.PerDay > 0 ? Money(Math.Min(d.PerDay, d.Owed)) : string.Empty,
                        d.LateDays > 0 ? FinanceText.T("reallife_econ_row_late", "days", d.LateDays.ToString(Inv)) : string.Empty }
                });
                total += d.Owed;
                sums[FinanceTables.Other] += d.Owed;
            }
            return total;
        }

        private static string YieldRate(float percent) => percent > 0f ? FinanceText.T("reallife_econ_row_rate", "rate", percent.ToString("0.#", Inv)) : string.Empty;

        private static int PurposeOf(CreditFile file, Loan loan)
        {
            for (int i = 0; i < file.Loans.Count; i++)
            {
                if (ReferenceEquals(file.Loans[i].Ref, loan))
                {
                    return file.Loans[i].Purpose;
                }
            }
            return LoanPurpose.Plain;
        }

        // The city's economy in three lines, from the Economy app's own words; none while it is not running.
        private static void City(FinanceOverviewFacts o)
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            CityState state = director != null ? director.State : null;
            o.EconomyApp = state != null && EconomyBridges.EconomyAppOn != null && EconomyBridges.EconomyAppOn();
            if (state == null)
            {
                o.CityLabels = new string[0];
                o.CityValues = new string[0];
                return;
            }
            string verdict = EconomyBridges.EconomyVerdict != null ? EconomyBridges.EconomyVerdict() : null;
            o.CityLabels = new[] { Label("reallife_macro_over_rate"), Label("reallife_macro_over_prices"), Label("reallife_macro_over_economy") };
            o.CityValues = new[]
            {
                CityText.Rate(state.Base), CityText.Prices(state),
                string.IsNullOrEmpty(verdict) ? CityText.Economy(state)
                    : FinanceText.T("reallife_econapp_finance_economy", "verdict", verdict, "state", CityText.State(state.Cycle), "month", state.MonthsIn.ToString(Inv))
            };
        }

        // The cash over the days the save has, never a zero before it began.
        private UiChartModel CashChart(UtilitiesWords w, int most)
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RivalGame.Day;
            int n = book != null ? FinanceRules.ExistingDays(book, DaySeriesCatalogue.Cash, day, most, false, _values, _days) : 0;
            string title = w.T("reallife_fin_cash_chart", "Cash");
            return FinanceRules.Chart(title, new[] { title }, new[] { Copy(_values, n) }, _days, n, Money, w.T("reallife_fin_chart_empty", "Not enough days yet"),
                w.T("reallife_fin_chart_empty_body", "The chart fills in as days pass."));
        }

        private static float[] Copy(float[] from, int n)
        {
            var copy = new float[n];
            Array.Copy(from, copy, n);
            return copy;
        }

        // The cheapest bank that lends you something now: "up to $400,000 at 5.2%".
        private static bool BestOffer(CreditScore score, out string name, out string line)
        {
            name = line = null;
            if (!score.Lends)
            {
                return false;
            }
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            float best = float.MaxValue;
            for (int i = 0; i < lenders.Count; i++)
            {
                float most = PhoneLending.MostNow(lenders[i], score.Value), rate = PhoneLending.RateOffered(lenders[i], score.Value);
                if (most >= CreditCatalogue.LoanLeast && rate < best)
                {
                    best = rate;
                    name = lenders[i].Name;
                    line = W.F("reallife_fin_best_line", "{bank}, up to {most} at {rate}%", "bank", lenders[i].Name, "most", Money(most), "rate", rate.ToString("0.#", Inv));
                }
            }
            return name != null;
        }

        // ------------------------------------------------------------------------------------------------- Credit

        private void ReadCredit(FinanceFacts f, FinanceView v)
        {
            FinanceCreditFacts c = f.Credit;
            CreditScore score = CreditLedger.Score;
            UtilitiesWords w = W;
            int n = (int)CreditFactor.Count;
            c.Factors = new FinanceFactor[n];
            for (int i = 0; i < n; i++)
            {
                var factor = (CreditFactor)i;
                float part = score.Factor[i];
                c.Factors[i] = new FinanceFactor
                {
                    Name = FinanceText.T(CreditCatalogue.FactorKeys[i]), Weight = CreditCatalogue.Weights[i] + "%", Part = (int)Math.Round(part * 100f), Word = FinanceText.Word(part),
                    Stands = FinanceText.Stands(score, factor), Tip = FinanceText.Tip(score, factor), Tone = part >= 0.85f ? UiRole.Ok : part >= 0.4f ? UiRole.Warn : UiRole.Danger
                };
            }
            int tips = CreditScoring.Tips(score, _tips);
            c.Tips = new string[tips];
            for (int i = 0; i < tips; i++)
            {
                c.Tips[i] = FinanceText.Tip(score, _tips[i]);
            }
            c.Worst = Worst(score);
            c.OffersTitle = w.T("reallife_fin_offers_title", "Offers at your score");
            var offers = new List<FinanceRow>(12);
            OfferRows(offers, score, out _);
            c.Offers = FinanceKitTables.Make(FinanceKitTables.OfferColumns(w, true), offers, FinanceRules.Clamp(v.CreditRow, offers.Count), w.T("reallife_fin_no_offers", "No offers"),
                FinanceText.T("reallife_econ_why_threshold", "threshold", CreditCatalogue.LendingThreshold.ToString(Inv)), UiIcon.Bank);
            var events = new List<FinanceRow>(CreditCatalogue.LedgerShown);
            List<CreditEvent> log = CreditLedger.File.Events;
            for (int i = log.Count - 1, shown = 0; i >= 0 && shown < CreditCatalogue.LedgerShown; i--, shown++)
            {
                CreditEvent e = log[i];
                events.Add(new FinanceRow { Name = FinanceText.Event(e), Cells = new[] { FinanceText.Day(e.Day), FinanceText.Effect(e.Effect) },
                    Values = new[] { 0, e.Effect < 0 ? -1 : e.Effect > 0 ? 1 : 0 } });
            }
            c.Events = FinanceKitTables.Make(FinanceKitTables.EventColumns(w), events, -1, w.T("reallife_fin_no_events", "Nothing yet"),
                FinanceText.T("reallife_econ_row_no_events"), UiIcon.Star);
        }

        // The debt that hurts the rating most now: the one furthest behind, else a limit more than half used.
        private static string Worst(CreditScore score)
        {
            CreditFile file = CreditLedger.File;
            string name = null;
            int misses = 0;
            float used = 0.5f;
            bool behind = false;
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                if (m.Ladder.Misses > misses && m.Ref is Loan)
                {
                    misses = m.Ladder.Misses;
                    name = new Debt(m).Name;
                    behind = true;
                }
            }
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (a.Closed || a.Test)
                {
                    continue;
                }
                if (a.Ladder.Misses > misses)
                {
                    misses = a.Ladder.Misses;
                    name = CreditDesk.Name(a);
                    behind = true;
                }
                else if (!behind && ProductCatalogue.Revolving(a.Kind) && a.Agreed > 0 && a.Owed / (float)a.Agreed > used)
                {
                    used = a.Owed / (float)a.Agreed;
                    name = CreditDesk.Name(a);
                }
            }
            if (name != null)
            {
                return behind ? FinanceText.T("reallife_econ_credit_worst_behind", "account", name, "count", misses.ToString(Inv))
                    : FinanceText.T("reallife_econ_credit_worst_used", "account", name, "share", ((int)Math.Round(used * 100f)).ToString(Inv) + "%");
            }
            return score.Held ? FinanceText.T("reallife_econ_credit_held", "score", score.Ceiling.ToString(Inv)) : null;
        }
    }
}
