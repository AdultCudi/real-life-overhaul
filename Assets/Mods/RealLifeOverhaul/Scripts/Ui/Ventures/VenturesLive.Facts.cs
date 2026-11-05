using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Extensions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using Streets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The Ventures facts, read from the investors' file and the game.</summary>
    public sealed partial class VenturesLive
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int WeekDays = 7, MonthRentDays = 30;

        private VenFacts Facts(VenturesDirector d, VentView v)
        {
            VenturesFile file = d.File;
            VenturesExtra extra = d.Extra;
            int day = SaveGameManager.Current.Day;
            int monthDays = VenturesDirector.MonthLength;
            var f = new VenFacts
            {
                Day = day, InvestorsOn = Settings.VenturesInvestors, Reputation = file.Reputation, Invested = file.InvestedTotal, PaidOutTotal = file.PaidOutTotal,
                    TargetsMet = file.TargetsMet,
                RaiseNow = Settings.VenturesInvestors,
                HowText = FinanceText.T("reallife_ven_inv_how", "days", VenturesCatalogue.ProfitDaysNeeded.ToString(Inv), "of", VenturesCatalogue.ProfitWindowDays.ToString(Inv),
                    "profit", FinanceText.Money(VenturesCatalogue.OfferProfitFloor))
            };
            int firms = Businesses();
            var cache = new Dictionary<int, VenInvestorFact>(8);
            VenInvestorFact Who(int id, bool forNew = false)
            {
                Investor inv = file.Investor(id);
                if (inv == null)
                {
                    return new VenInvestorFact { Name = string.Empty, Kind = string.Empty, Temper = string.Empty, Likes = string.Empty, OpenText = string.Empty };
                }
                int key = forNew ? id + 1000 : id;
                VenInvestorFact have;
                if (!cache.TryGetValue(key, out have))
                {
                    have = MakeInvestor(file, inv, day, forNew);
                    cache[key] = have;
                }
                return have;
            }

            var offers = new List<VenOfferFact>(file.Offers.Count);
            for (int i = 0; i < file.Offers.Count; i++)
            {
                Offer o = file.Offers[i];
                Investor inv = file.Investor(o.InvestorId);
                if (inv == null)
                {
                    continue;
                }
                var kinds = new PromiseKind[2];
                int n = VenturesExtraRules.PlannedPromises(file, inv, o.Id, kinds);
                var promises = new VenPromiseFact[n];
                for (int p = 0; p < n; p++)
                {
                    promises[p] = new VenPromiseFact { Text = PlannedText(kinds[p], firms), State = 0 };
                }
                DealTerm term = VenturesExtraRules.TermFor(file, inv, o.Id, VenturesDirector.YearLength);
                offers.Add(new VenOfferFact
                {
                    Id = o.Id, Who = Who(o.InvestorId), ForNew = o.ForNew, Amount = o.Amount, ShareBp = o.ShareBp, Implied = VenturesExtraRules.Implied(o.Amount, o.ShareBp),
                    For = o.ForNew ? _words.F("reallife_ven_row_new", "A new {type}", "type", VenturesGame.TypeName(o.TypeName)) : VenturesText.Business(o.Place, o.Business),
                    TargetPct = o.TargetPct, TargetDays = o.TargetDays, DaysLeft = o.LapseDay - day, Countered = o.Countered, CanMoreMoney = OfferRules.MoreMoney(o, inv),
                    BuyBack = term != null ? TermText(term) : null, SaleLine = _words.T("reallife_ui_ven_sale_offer", "They are paid out first, from the price."), Promises = promises,
                    TargetLine = o.ForNew ? _words.F("reallife_ui_ven_target_new", "{target} a day within {days} days of opening", "target",
                        FinanceText.Money(OfferRules.YoungTarget), "days", o.TargetDays.ToString(Inv))
                        : _words.F("reallife_ui_ven_target_line", "Profit up {growth}% within {days} days", "growth", o.TargetPct.ToString(Inv), "days", o.TargetDays.ToString(Inv))
                });
            }
            f.Offers = offers.ToArray();

            var pledges = new List<VenPledgeFact>(file.Pledges.Count);
            for (int i = 0; i < file.Pledges.Count; i++)
            {
                Pledge p = file.Pledges[i];
                pledges.Add(new VenPledgeFact { Id = p.Id, Who = Who(p.InvestorId), Type = VenturesGame.TypeName(p.TypeName), Amount = p.Amount, ShareBp = p.ShareBp,
                    OpenBy = p.OpenBy, DaysLeft = p.OpenBy - day });
            }
            f.Pledges = pledges.ToArray();

            var deals = new List<VenDealFact>(file.Deals.Count);
            int keptAll = 0, totalAll = 0;
            for (int i = 0; i < file.Deals.Count; i++)
            {
                VenDealFact fact = MakeDeal(d, file, extra, file.Deals[i], Who(file.Deals[i].InvestorId), day, monthDays);
                keptAll += fact.Kept;
                totalAll += fact.Total;
                deals.Add(fact);
            }
            f.Deals = deals.ToArray();
            f.PromisesKept = keptAll;
            f.PromisesTotal = totalAll;

            var investors = new VenInvestorFact[file.Investors.Count];
            bool forNew = v.Page == VenPages.PageWho && v.S1 == null;
            for (int i = 0; i < investors.Length; i++)
            {
                investors[i] = Who(file.Investors[i].Id, forNew);
            }
            f.Investors = investors;
            Ring(file, extra, day, monthDays, f);
            Raise(file, day, f);
            Property(f);
            return f;
        }

        private static int Businesses()
        {
            var list = new List<BuildingRegistration>(16);
            Books.Businesses(list);
            return list.Count;
        }

        private VenInvestorFact MakeInvestor(VenturesFile file, Investor inv, int day, bool forNew)
        {
            var names = new string[3];
            var stakes = new int[3];
            int n = VenturesExtraRules.AlsoBacks(file, inv, names, stakes);
            var backs = new VenBackFact[n];
            for (int i = 0; i < n; i++)
            {
                backs[i] = new VenBackFact { Name = names[i], Pct = stakes[i] };
            }
            bool takes = !forNew || inv.Kind != InvestorKind.Tycoon;   // a tycoon backs no business not yet open
            bool open = takes && inv.Open(day);
            bool never = VenturesText.Never(inv, day);
            return new VenInvestorFact
            {
                Id = inv.Id, Name = inv.Name, Kind = VenturesText.Kind(inv), Temper = VenturesText.Temper(inv), Likes = VenturesText.Likes(inv),
                Portfolio = VenturesExtraRules.Portfolio(file, inv), Backs = backs, Open = open, Never = never && takes, Least = inv.Least, Most = inv.Most, Committed = inv.Committed,
                OpenText = !takes ? FinanceText.T("reallife_ven_row_open_only") : open ? FinanceText.T("reallife_ven_row_open_now")
                    : never ? FinanceText.T("reallife_ven_row_never") : FinanceText.Day(inv.OpenDay)
            };
        }

        // The buy-back term in words: from the offer on it is "after so many years", on a deal it is from its own day.
        private string TermText(DealTerm term, int fromDay = 0)
        {
            string times = (term.Times10 / 10.0).ToString("0.#", Inv);
            if (fromDay > 0)
            {
                return term.Mode == BuyBackMode.Callable
                    ? _words.F("reallife_ui_ven_term_call_from", "They may call it from day {day} at {times} times the money", "day", fromDay.ToString(Inv), "times", times)
                    : _words.F("reallife_ui_ven_term_opt_from", "You may buy back from day {day} at {times} times the money", "day", fromDay.ToString(Inv), "times", times);
            }
            string years = (term.Day / Math.Max(1, VenturesDirector.YearLength)).ToString(Inv);
            return term.Mode == BuyBackMode.Callable
                ? _words.F("reallife_ui_ven_term_call", "They may call it after {years} years at {times} times the money", "years", years, "times", times)
                : _words.F("reallife_ui_ven_term_opt", "You may buy back after {years} years at {times} times the money", "years", years, "times", times);
        }

        private string PlannedText(PromiseKind kind, int firms)
        {
            int months = VenturesExtraRules.MonthsOf(kind);
            switch (kind)
            {
                case PromiseKind.Firms:
                    return _words.F("reallife_ui_ven_pr_firms", "Run {count} businesses within {months} months", "count", (firms + 2).ToString(Inv), "months", months.ToString(Inv));
                case PromiseKind.Morale:
                    return _words.F("reallife_ui_ven_pr_morale", "Keep team morale at {floor} or more for {months} months", "floor", "60", "months", months.ToString(Inv));
                default:
                    return _words.T("reallife_ui_ven_pr_report", "Send a report every quarter");
            }
        }

        private VenPromiseFact PromiseOf(Promise p)
        {
            switch (p.Kind)
            {
                case PromiseKind.Firms:
                    return new VenPromiseFact { Text = _words.F("reallife_ui_ven_pr_firms_day", "Run {count} businesses by day {day}", "count", p.Target.ToString(Inv), "day",
                        p.Due.ToString(Inv)), State = p.State };
                case PromiseKind.Morale:
                    return new VenPromiseFact { Text = _words.F("reallife_ui_ven_pr_morale_day", "Keep team morale at {floor} or more until day {day}", "floor",
                        p.Target.ToString(Inv), "day", p.Due.ToString(Inv)), State = p.State };
                default:
                    return new VenPromiseFact
                    {
                        Text = _words.F("reallife_ui_ven_pr_report_day", "Send a report every quarter, the next by day {day}", "day", p.Due.ToString(Inv)),
                            State = p.Missed > 0 ? 2 : p.Kept > 0 ? 1 : 0
                    };
            }
        }

        private static string DealState(Deal d)
        {
            switch (d.Stage)
            {
                case DealStage.Called: return FinanceText.T("reallife_ven_state_called");
                case DealStage.Demand: return FinanceText.T("reallife_ven_state_demand");
                case DealStage.Cold: return FinanceText.T("reallife_ven_state_cold");
                case DealStage.Quiet: return FinanceText.T("reallife_ven_state_quiet");
                default: return d.Misses > 0 ? FinanceText.T("reallife_ven_state_warned") : VenturesText.Mood(d);
            }
        }

        private VenDealFact MakeDeal(VenturesDirector d, VenturesFile file, VenturesExtra extra, Deal deal, VenInvestorFact who, int day, int monthDays)
        {
            Investor inv = file.Investor(deal.InvestorId);
            BuildingRegistration reg = VenturesGame.Business(deal.Place);
            BusinessFigures b = VenturesGame.Figures(reg);
            DealTerm term = extra.TermOf(deal.Id);
            int price = d.BuyOutPrice(deal);
            var promises = new List<VenPromiseFact>(3);
            bool reportDue = false;
            for (int i = 0; i < extra.Promises.Count; i++)
            {
                Promise p = extra.Promises[i];
                if (p.DealId != deal.Id)
                {
                    continue;
                }
                promises.Add(PromiseOf(p));
                reportDue |= p.Kind == PromiseKind.Report && p.State == 0 && p.ReportDay <= p.Due - 3 * monthDays;
            }
            int kept, total;
            bool broken;
            VenturesExtraRules.Kept(extra, deal.Id, out kept, out total, out broken);
            var fact = new VenDealFact
            {
                Id = deal.Id, Who = who, Business = VenturesText.Business(deal.Place, deal.Business), Amount = deal.Amount, PaidOut = deal.PaidOut, Arrears = deal.Arrears,
                    ShareBp = deal.ShareBp,
                ShareNowBp = deal.Stage == DealStage.Called ? 0 : deal.ShareNow(day), StartDay = deal.StartDay, Mood = deal.Mood,
                    Implied = VenturesExtraRules.Implied(deal.Amount, deal.ShareBp),
                MoodText = VenturesText.Mood(deal), StateText = DealState(deal), Stage = (int)deal.Stage, CanSeat = inv == null || inv.Kind != InvestorKind.Firm, BuyOut = price,
                SaleLine = _words.F("reallife_ui_ven_sale_deal", "They are bought out first, for {amount}.", "amount", FinanceText.Money(price)),
                    Promises = promises.ToArray(), Kept = kept, Total = total,
                ReportDue = reportDue, Day = FinanceText.Day(deal.StartDay)
            };
            if (term != null)
            {
                fact.BuyBack = TermText(term, term.Day);
                fact.BuyBackNow = day >= term.Day;
                fact.BuyBackCalls = term.Mode == BuyBackMode.Callable;
                fact.BuyBackPrice = VenturesExtraRules.BuyBackPrice(deal, term);
            }
            if (deal.Stage == DealStage.Called)
            {
                fact.CalledLine = FinanceText.T("reallife_ven_deal_called", "amount", FinanceText.Money(deal.CallOwed), "per_day", FinanceText.Money(deal.CallPerDay));
                return fact;
            }
            if (deal.BoardUntil > day)
            {
                fact.SeatLine = FinanceText.T("reallife_ven_deal_share_seat", "share", VenturesText.Share(deal.ShareNow(day)), "day", deal.BoardUntil.ToString(Inv));
            }
            if (deal.TargetDay > 0)
            {
                fact.TargetLine = _words.F("reallife_ui_ven_deal_target_line", "{target} a day by day {day}, now {now}", "target", FinanceText.Money(deal.TargetProfit), "day",
                    deal.TargetDay.ToString(Inv),
                    "now", FinanceText.Money(b.AvgProfit7));
            }
            if (deal.CostDay > 0)
            {
                fact.CostLine = _words.F("reallife_ui_ven_deal_cost_line", "Costs under {target} a day by day {day}, now {now}", "target", FinanceText.Money(deal.CostTarget),
                    "day", deal.CostDay.ToString(Inv),
                    "now", FinanceText.Money(b.AvgCost14));
            }
            if (deal.Stage == DealStage.Demand)
            {
                fact.DemandLine = FinanceText.Fill(inv != null && inv.Kind == InvestorKind.Firm ? "reallife_ven_deal_demand_firm" : "reallife_ven_deal_demand", "day",
                    deal.DemandDay.ToString(Inv),
                    "extra", VenturesText.Share(VenturesCatalogue.BoardExtraBp), "seat_days", VenturesCatalogue.BoardDays.ToString(Inv), "cut", VenturesCatalogue.CostCutPct.ToString(Inv),
                    "cut_days", VenturesCatalogue.CostCutDays.ToString(Inv));
            }
            return fact;
        }

        // The valuation of the player's firm by month beside a rival's, from the extras' ring and the rival's own rule, and what a rival raised lately.
        private void Ring(VenturesFile file, VenturesExtra extra, int day, int monthDays, VenFacts f)
        {
            int month = VenturesExtraRules.MonthOf(day, monthDays);
            var names = new string[3];
            int rivals = VenturesExtraRules.Rivals(file, names);
            f.RivalName = rivals > 0 ? names[0] : null;
            int n = Math.Min(extra.ValuationCount, VenturesExtra.RingMonths);
            int last = extra.MonthDone >= 0 ? extra.MonthDone : month;
            f.Mine = new long[n];
            f.Rival = new long[n];
            f.MonthNames = new string[n];
            for (int i = 0; i < n; i++)
            {
                int m = last - (n - 1 - i);
                f.Mine[i] = extra.Valuation[i];
                f.Rival[i] = rivals > 0 ? VenturesExtraRules.RivalValue(file, 0, m) : 0L;
                f.MonthNames[i] = _words.F("reallife_ui_ven_month_short", "M{n}", "n", m.ToString(Inv));
            }
            var rival = new int[3];
            var investor = new int[3];
            var sum = new int[3];
            for (int back = 0; back < 2 && f.RivalLine == null; back++)
            {
                int count = VenturesExtraRules.RoundsOf(file, month - back, rival, investor, sum);
                if (count > 0)
                {
                    string rivalName = names[rival[0]], sumText = FinanceText.Money(sum[0]), investorName = file.Investors[investor[0]].Name;
                    string value = _words.Short(VenturesExtraRules.RivalValue(file, rival[0], month - back));
                    f.RivalLine = back == 0
                        ? _words.F("reallife_ui_ven_rival_raised", "{rival} raised {sum} from {investor} this month and is valued at {value}. Investors are comparing the two of you.",
                            "rival", rivalName, "sum", sumText, "investor", investorName, "value", value)
                        : _words.F("reallife_ui_ven_rival_raised_before", "{rival} raised {sum} from {investor} last month and is valued at {value}. Investors are comparing the two of you.",
                            "rival", rivalName, "sum", sumText, "investor", investorName, "value", value);
                }
            }
            if (extra.Heards.Count > 0)
            {
                Heard h = extra.Heards[extra.Heards.Count - 1];
                Investor who = file.Investor(h.InvestorId);
                string name = who != null ? who.Name : string.Empty;
                f.HeardLine = h.Kind == 0
                    ? _words.F("reallife_ui_ven_heard_cold", "Word got round on day {day} that {name} was left unpaid. Careful investors keep away until day {until}.", "day",
                        h.Day.ToString(Inv), "name", name,
                        "until", (h.Day + VenturesExtraRules.WaryDays).ToString(Inv))
                    : _words.F("reallife_ui_ven_heard_broken", "Word got round on day {day} that a promise to {name} was broken.", "day", h.Day.ToString(Inv), "name", name);
            }
        }

        private void Raise(VenturesFile file, int day, VenFacts f)
        {
            var regs = new List<BuildingRegistration>(16);
            Books.Businesses(regs);
            var biz = new VenRaiseFact[regs.Count];
            for (int i = 0; i < biz.Length; i++)
            {
                BuildingRegistration r = regs[i];
                BusinessFigures b = VenturesGame.Figures(r);
                string key = Books.Key(r.Address);
                int deals;
                int promised = file.ShareOn(key, day, out deals);
                biz[i] = new VenRaiseFact
                {
                    Key = key, Name = VenturesGame.Name(r), AvgProfit = (long)b.AvgProfit7, AgeDays = b.AgeDays, Promised = promised,
                    Line = FinanceText.T("reallife_ven_row_days", "days", b.AgeDays.ToString(Inv))
                };
            }
            f.RaiseBusinesses = biz;
            var types = new List<string>(40);
            VenturesGame.StartableTypes(types);
            var list = new VenRaiseFact[types.Count];
            for (int i = 0; i < list.Length; i++)
            {
                Sector sector = VenturesCatalogue.SectorOf(types[i]);
                int liked = 0;
                for (int v = 0; v < file.Investors.Count; v++)
                {
                    liked += file.Investors[v].LikesSector(sector) ? 1 : 0;
                }
                list[i] = new VenRaiseFact { Key = types[i], Name = VenturesGame.TypeName(types[i]), Sector = VenturesText.SectorName(sector), Liked = liked,
                    Running = VenturesGame.CountOf(types[i]) };
            }
            f.RaiseTypes = list;
            f.Budgets = new long[VenturesCatalogue.BudgetSteps.Length];
            for (int i = 0; i < f.Budgets.Length; i++)
            {
                f.Budgets[i] = VenturesCatalogue.BudgetAt(i);
            }
        }

        // The Property tab: the game's own figures of the player's buildings, read as an investment. Nothing is kept or changed.
        private void Property(VenFacts f)
        {
            GameInstance save = SaveGameManager.Current;
            List<RealEstate> owned = save.realEstate;
            int count = owned != null ? owned.Count : 0;
            int year = GameCalendar.DaysPerYear;
            double paid = 0;
            float week = 0f, month = 0f;
            var list = new List<VenPropertyFact>(count);
            for (int i = 0; i < count; i++)
            {
                RealEstate e = owned[i];
                if (e == null || e.address == null)
                {
                    continue;
                }
                float rent7 = VenturesGame.Rent(e.address, WeekDays), rent30 = VenturesGame.Rent(e.address, MonthRentDays);
                paid += e.purchasePrice;
                week += rent7;
                month += rent30;
                float worth, daily = DailyRent(e);
                int let;
                Worth(e, out worth, out let);
                double yield = e.purchasePrice > 0 ? daily * year / e.purchasePrice * 100.0 : double.NaN;
                string mortgage = null;
                long owedMortgage = 0;
                CreditFile credit = CreditLedger.Ready ? CreditLedger.File : null;
                string key = Books.Key(e.address);
                for (int a = 0; credit != null && a < credit.Accounts.Count; a++)
                {
                    CreditAccount acc = credit.Accounts[a];
                    if (!acc.Closed && acc.Kind == ProductKind.Mortgage && acc.Security == key)
                    {
                        owedMortgage += (long)acc.Owed;
                        mortgage = key;
                    }
                }
                list.Add(new VenPropertyFact
                {
                    Key = key, Address = e.address.ToFormattedString(), District = CityGame.Name(CityGame.DistrictOf(e)), Let = let, Paid = (long)e.purchasePrice, Worth = (long)worth,
                    Week = (long)rent7, Month = (long)rent30, Mortgage = owedMortgage, Yield = double.IsNaN(yield) ? null : yield.ToString("0.0", Inv) + "%",
                    YieldGrade = double.IsNaN(yield) ? -1 : (int)Math.Max(0, Math.Min(100, yield * 10)),
                    Bought = _words.F("reallife_ui_ven_prop_bought_value", "Day {day} for {price}", "day", e.purchaseDay.ToString(Inv), "price", FinanceText.Money((float)e.purchasePrice))
                });
            }
            f.Property = list.ToArray();
            f.PropWeek = (long)week;
            f.PropMonth = (long)month;
            f.PropPaid = (long)paid;
        }

        // What a building brings in a day now, as the game works it out (nothing for a home lived in).
        private static float DailyRent(RealEstate e)
        {
            try
            {
                return e.DailyIncome;
            }
            catch (Exception)
            {
                return 0f;   // the building is no longer in the city
            }
        }

        // What the game says a building is worth and how much of it is let; 0 for a figure it cannot give.
        private static void Worth(RealEstate e, out float worth, out int let)
        {
            worth = 0f;
            let = 0;
            try
            {
                worth = e.Building != null ? e.Building.GetMarketValue() : 0f;
                let = e.OccupancyPercentage;
            }
            catch (Exception)
            {
                worth = 0f;
            }
        }
    }
}
