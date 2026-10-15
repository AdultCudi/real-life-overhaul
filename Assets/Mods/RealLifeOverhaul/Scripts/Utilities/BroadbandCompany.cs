using System;
using BAModAPI;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The player's broadband company in the game (its rules: BroadbandRules). It is founded with the city's operator's
    /// licence at the player's headquarters, its network dug district by district from the Utilities app's "Your
    /// network" tab. Each morning the networks dug by then open and the day that has ended is booked at the
    /// headquarters: the customers' bills as its takings, its engineers and call centre as wages, its network as the
    /// cost of what it sells, its marketing as marketing (so its statement, the game's tax and its bank count them all:
    /// Wiring/EconomyLinks, EconomyBridges.Takings and CostRow), its running costs deducted by the game's tax. Its
    /// capital, the licence and the networks dug, is not deducted, as the price it is sold for is not taxed. At the turn of the
    /// month the city's market moves (the rivals' answer already in their prices) and the new connections' installations
    /// are paid. A company without a headquarters cannot bill anybody; a company sold is its buyer's. The
    /// UtilitiesDirector runs it with the rest of its books. At the end game's scale (Core/EndScale, its option on) each
    /// day also hires its crews towards what they need and pays them, and a district dug before the scale may be
    /// extended to the whole district; a company that existed before is brought to the scale over four game weeks, and
    /// what the scale adds to a day's costs is never charged past the player's cash. Cost: nothing per frame.
    /// </summary>
    public sealed class BroadbandCompany
    {
        public static IModLogger Logger;

        private const string Salaries = "ba:transactioncategory_salaryexpenses", Marketing = "ba:transactioncategory_marketing", Licensing = "ba:transactioncategory_licensingfees";

        private readonly UtilitiesDirector _director;
        private int _warnedDay = -1;

        public BroadbandCompany(UtilitiesDirector director)
        {
            _director = director;
        }

        private UtilitiesState State => _director.State;

        /// <summary>The company of this save; null while no save is running.</summary>
        public Network Network => State != null ? State.Network : null;

        /// <summary>
        /// The end game's share for the company today (Core/EndScale), also set on the network for its rules: 0 with the
        /// option off; a company that ran before the scale comes to it over four game weeks, with a notice on the first day.
        /// </summary>
        public int ScaleNow()
        {
            Network n = Network;
            if (n == null)
            {
                return 0;
            }
            int today = UtilitiesGame.Today, before = n.ScaleFrom;
            n.ScaleFrom = EndScaleRules.From(Settings.EndScale, before, today, n.Running);
            if (EndScaleRules.Begins(before, n.ScaleFrom, today))
            {
                UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_endscale_note_phase", "what", UtilitiesText.T("reallife_endscale_what_broadband"), "days", EndScaleRules.PhaseDays.ToString()), 14f);
                Logger?.Info("Utilities: the end game's scale begins to come in for the broadband company on day " + today + ", over " + EndScaleRules.PhaseDays + " days.");
            }
            n.Scale = EndScaleRules.Share(Settings.EndScale, n.ScaleFrom, today);
            return n.Scale;
        }

        // ---------------------------------------------------------------- the day and the month

        /// <summary>The morning: the networks dug by today open, and the company as a provider is brought up to date.</summary>
        public void Morning(int today)
        {
            Network n = Network;
            ScaleNow();
            if (n != null && n.Exists)
            {
                for (int d = 0; d < Network.Districts; d++)
                {
                    if (n.Digging(d) && n.ReadyDay(d) <= today)
                    {
                        UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_bb_toast_open", "district", District(d)), 12f);
                        Logger?.Info("Utilities: the broadband network in " + BroadbandCatalogue.Districts[d] + " opened (ready on day " + n.ReadyDay(d) + ").");
                    }
                }
                BroadbandRules.Finish(n, today);
            }
            BroadbandRules.Apply(n, today);
        }

        /// <summary>A day that has ended, booked at the headquarters: the customers' bills (none on a day the network failed, nor without a headquarters), its staff, its network and its marketing.</summary>
        public void Day(int day, int level, int monthDays)
        {
            Network n = Network;
            if (n == null || !n.Running)
            {
                return;
            }
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            int share = ScaleNow();
            BroadbandRules.Hire(n, day);
            bool outage = InternetRules.Outage(State.Seed, day, InternetCatalogue.OwnIndex, false, out _, out _);
            int takings = BroadbandRules.DayTakings(n, level, monthDays, outage, hq != null);
            BroadbandRules.DayCosts(n, level, day, monthDays, out int staff, out int network, out int marketing);
            if (share > 0 || n.Owed > 0)
            {
                // what the scale adds to the day (its crews and upkeep over the figures before it), and what it owed from
                // before, after the day's takings: never past the cash; what is left is owed, and its crews leave in that share
                BroadbandRules.DayCosts(n, level, day, monthDays, out int staff0, out int network0, out _, 0);
                long room = UtilitiesGame.Cash + takings - staff0 - network0 - marketing;
                long staffDue = Math.Max(0, staff - staff0) + n.Owed, networkDue = Math.Max(0, network - network0);
                long staffPaid = EndScaleRules.Take(ref room, staffDue), networkPaid = EndScaleRules.Take(ref room, networkDue);
                staff = (int)Math.Min(int.MaxValue / 4, staff0 + staffPaid);
                network = (int)Math.Min(int.MaxValue / 4, network0 + networkPaid);
                bool was = n.Owed > 0;
                n.Owed = staffDue + networkDue - staffPaid - networkPaid;
                int unpaid = EndScaleRules.Unpaid(staffDue + networkDue, staffPaid + networkPaid);
                if (unpaid > 0)
                {
                    BroadbandRules.Leave(n, unpaid);
                    if (!was)
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_endscale_note_owed", "what", UtilitiesText.T("reallife_endscale_what_broadband"), "owed", UtilitiesText.Money(n.Owed)), 14f);
                    }
                }
            }
            UtilitiesGame.Book(takings, BroadbandCatalogue.TakingsKey, hq, null);
            UtilitiesGame.Book(-staff, BroadbandCatalogue.WagesKey, hq, Salaries);
            UtilitiesGame.Book(-network, BroadbandCatalogue.NetworkKey, hq, null);
            UtilitiesGame.Book(-marketing, BroadbandCatalogue.MarketingKey, hq, Marketing);
            n.MonthTakings = Sum(n.MonthTakings, takings);
            n.MonthCosts = Sum(n.MonthCosts, staff + network + marketing);
            if (hq == null && BroadbandRules.Connections(n) > 0 && _warnedDay != UtilitiesGame.Today)
            {
                _warnedDay = UtilitiesGame.Today;
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_bb_toast_no_hq"), 12f);
            }
        }

        /// <summary>
        /// The turn of the month: the city's market moves, the new connections' installations are paid, and the month's
        /// books are closed. answered: the rivals whose prices are held down this month where the company took customers.
        /// </summary>
        public void Month(int day, int level, int monthDays, int answered)
        {
            Network n = Network;
            if (n == null || !n.Exists)
            {
                return;
            }
            int before = BroadbandRules.Connections(n);
            BroadbandRules.Month(n, State.Promo, day, monthDays);
            int after = BroadbandRules.Connections(n);
            if (!n.Running)
            {
                return;
            }
            int installs = BroadbandRules.Installs(before, after, level, ScaleNow());
            UtilitiesGame.Book(-installs, BroadbandCatalogue.InstallsKey, UtilitiesGame.Headquarters(), null);
            n.MonthCosts = Sum(n.MonthCosts, installs);
            bool any = n.MonthTakings > 0 || n.MonthCosts > 0;
            BroadbandRules.CloseMonth(n, after, after - before);
            if (any)
            {
                UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T(answered > 0 ? "reallife_bb_toast_month_answer" : "reallife_bb_toast_month", "count", after.ToString("N0", UtilitiesText.Culture),
                    "gained", Signed(after - before), "takings", UtilitiesText.Money(n.LastTakings), "costs", UtilitiesText.Money(n.LastCosts), "profit", UtilitiesText.Money(n.LastTakings - n.LastCosts),
                    "rivals", answered.ToString()), 14f);
            }
            Logger?.Info("Utilities: the broadband month turned on day " + day + ": " + after + " connection(s) (" + (after - before) + "), takings " + n.LastTakings + ", costs " + n.LastCosts
                         + " (installations " + installs + "), the last year's profit " + BroadbandRules.YearProfit(n) + ", a buyer would pay " + BroadbandRules.SaleValue(n)
                         + "; " + answered + " rival(s) holding their prices down.");
        }

        // ---------------------------------------------------------------- what the player does (the app)

        /// <summary>The company founded: the licence paid at the headquarters. False with a reason key if it cannot be.</summary>
        public bool Found(out string why)
        {
            Network n = Network;
            why = n == null ? "reallife_util_off_title" : n.State == CompanyState.Sold ? "reallife_bb_sold_out" : n.Running ? "reallife_bb_already" : null;
            if (why != null)
            {
                return false;
            }
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            int fee = BroadbandRules.LicenceFee(Prices.Level, EndScaleRules.Buying(Settings.EndScale));
            if (hq == null)
            {
                why = "reallife_bb_no_hq";
                return false;
            }
            if (fee > UtilitiesGame.Cash || !UtilitiesGame.Book(-fee, BroadbandCatalogue.LicenceKey, hq, Licensing, false))
            {
                why = "reallife_net_no_cash";
                return false;
            }
            BroadbandRules.Found(n, UtilitiesGame.Today);
            BroadbandRules.Apply(n, UtilitiesGame.Today);
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_bb_toast_founded"), 14f);
            Logger?.Info("Utilities: a broadband company founded on day " + n.FoundedDay + " at " + UtilitiesGame.Place(hq) + ", the licence " + fee + ".");
            return true;
        }

        /// <summary>A district's network ordered: paid now, open in BuildMonths of the game's months. False with a reason key if it cannot be.</summary>
        public bool Build(int district, out string why)
        {
            Network n = Network;
            why = n == null || !n.Running ? "reallife_bb_not_running" : district < 0 || district >= Network.Districts || n.Digging(district) || n.Built[district] > 0 ? "reallife_bb_already" : null;
            if (why != null)
            {
                return false;
            }
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            int buy = EndScaleRules.Buying(Settings.EndScale);
            int cost = BroadbandRules.BuildCost(district, Prices.Level, buy);
            if (hq == null)
            {
                why = "reallife_bb_no_hq";
                return false;
            }
            if (cost > UtilitiesGame.Cash || !UtilitiesGame.Book(-cost, BroadbandCatalogue.BuildKey, hq, null, false))
            {
                why = "reallife_net_no_cash";
                return false;
            }
            int today = UtilitiesGame.Today;
            BroadbandRules.StartBuild(n, district, today, GameCalendar.MonthDays, cost, buy > 0);
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_bb_toast_build", "district", District(district), "day", UtilitiesText.Day(n.ReadyDay(district))), 12f);
            Logger?.Info("Utilities: the broadband network in " + BroadbandCatalogue.Districts[district] + " ordered on day " + today + " for " + cost + ", open on day " + n.ReadyDay(district) + ".");
            return true;
        }

        /// <summary>
        /// A district's network dug before the scale extended to the whole district at it: paid now (what the scale's
        /// network costs less what it cost), at scale from today. False with a reason key if it cannot be.
        /// </summary>
        public bool Extend(int district, out string why)
        {
            Network n = Network;
            ScaleNow();
            int cost = BroadbandRules.ExtendCost(n, district, Prices.Level, UtilitiesGame.Today, EndScaleRules.Buying(Settings.EndScale));
            why = n == null || !n.Running ? "reallife_bb_not_running" : cost <= 0 ? "reallife_bb_already" : null;
            if (why != null)
            {
                return false;
            }
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            if (hq == null)
            {
                why = "reallife_bb_no_hq";
                return false;
            }
            if (cost > UtilitiesGame.Cash || !UtilitiesGame.Book(-cost, BroadbandCatalogue.BuildKey, hq, null, false))
            {
                why = "reallife_net_no_cash";
                return false;
            }
            BroadbandRules.Extend(n, district, cost);
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_endscale_bb_extended", "district", District(district)), 12f);
            Logger?.Info("Utilities: the broadband network in " + BroadbandCatalogue.Districts[district] + " extended to the whole district on day " + UtilitiesGame.Today + " for " + cost + ".");
            return true;
        }

        public void SetPrices(int share)
        {
            Network n = Network;
            if (n != null && n.Running)
            {
                n.PriceShare = Math.Max(BroadbandCatalogue.PriceLeast, Math.Min(BroadbandCatalogue.PriceMost, share));
            }
        }

        public void SetMaintenance(int share)
        {
            Network n = Network;
            if (n != null && n.Running)
            {
                n.Maintenance = Math.Max(BroadbandCatalogue.MaintenanceLeast, Math.Min(BroadbandCatalogue.MaintenanceMost, share));
                BroadbandRules.Apply(n, UtilitiesGame.Today);   // (its reliability as a provider of the player's own places)
            }
        }

        public void SetMarketing(int budget)
        {
            Network n = Network;
            if (n != null && n.Running)
            {
                n.Marketing = Math.Max(0, Math.Min(BroadbandCatalogue.Marketing.Length - 1, budget));
            }
        }

        /// <summary>
        /// The company sold for what a buyer pays (BroadbandRules.SaleValue): the network is the buyer's from now, and the
        /// player's own places on it are its customers like anybody's, month by month at its prices.
        /// </summary>
        public bool Sell(out string why)
        {
            Network n = Network;
            why = n == null || !n.Running ? "reallife_bb_not_running" : null;
            if (why != null)
            {
                return false;
            }
            long price = Math.Max(0L, BroadbandRules.SaleValue(n) - n.Owed);   // (what it owes is settled out of the price)
            int today = UtilitiesGame.Today;
            n.Owed = 0;
            UtilitiesGame.Book(price, BroadbandCatalogue.SaleKey, UtilitiesGame.Headquarters(), null);
            n.State = CompanyState.Sold;
            n.SoldDay = today;
            n.SalePrice = price;
            n.MonthTakings = n.MonthCosts = 0;
            BroadbandRules.Apply(n, today);
            int level = Prices.Level, moved = 0;
            for (int i = 0; i < State.Accounts.Count; i++)
            {
                Account a = State.Accounts[i];
                if (a.Service == Service.Internet && a.Provider == InternetCatalogue.OwnIndex && a.State != SupplyState.Closed)
                {
                    a.ContractUntil = 0;
                    a.PriceCents = InternetRules.MonthCents(a.Provider, a.Plan, a.BusinessPlan, false, level, State.PromoOf(a.Provider));
                    moved++;
                }
            }
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_bb_toast_sold", "price", UtilitiesText.Money(price)), 14f);
            Logger?.Info("Utilities: the broadband company sold on day " + today + " for " + price + "; " + moved + " of the player's place(s) on its network now billed by the buyer.");
            return true;
        }

        // ---------------------------------------------------------------- tests

        /// <summary>Test (Core/EndScale): the company starts coming to scale today, or is at it at once. False with no company running.</summary>
        public bool TestScale(bool full)
        {
            Network n = Network;
            if (n == null || !n.Running)
            {
                return false;
            }
            n.ScaleFrom = full ? UtilitiesGame.Today - EndScaleRules.PhaseDays : EndScaleRules.Unset;
            ScaleNow();
            BroadbandRules.Apply(n, UtilitiesGame.Today);
            return true;
        }

        /// <summary>Test: every network being dug is ready this morning.</summary>
        public int TestBuilt(int today)
        {
            Network n = Network;
            int count = 0;
            for (int d = 0; n != null && d < Network.Districts; d++)
            {
                if (n.Digging(d))
                {
                    n.Built[d] = -today;
                    count++;
                }
            }
            Morning(today);
            return count;
        }

        public static string District(int district)
        {
            return district >= 0 && district < Network.Districts ? BroadbandCatalogue.Districts[district].GetLocalization() : string.Empty;
        }

        private static string Signed(int value)
        {
            return (value >= 0 ? "+" : string.Empty) + value.ToString("N0", UtilitiesText.Culture);
        }

        private static int Sum(int a, int b)
        {
            return (int)Math.Max(0L, Math.Min(int.MaxValue / 4, (long)a + b));
        }
    }
}
