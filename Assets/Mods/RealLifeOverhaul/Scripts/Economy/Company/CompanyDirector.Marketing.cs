using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The marketing part: campaigns that add customers through business pressure's footfall figure.</summary>
    public sealed partial class CompanyDirector
    {
        partial void MarketingDay(int day)
        {
            int yesterday = day - 1;
            bool booked = false;   // yesterday's statement brought up to date for the reports
            for (int i = 0; i < _shops.Count; i++)
            {
                CompanyShop shop = _shops[i];
                ShopFile file = shop.File;
                if (file.Push < 0)
                {
                    continue;
                }
                if ((file.Push == (int)CampaignKind.DiscountDay || file.Push == (int)CampaignKind.Giveaway) && file.Pushing(yesterday) && shop.HasBooks)
                {
                    // The takings given away never came in: taken whatever the cash is, as part of the campaign's cost.
                    int given = CampaignRules.Discount(shop.Takings);
                    Cost(given, CompanyGame.DiscountKey, shop, shop.Name, CostKind.Marketing, yesterday,
                        CompanyText.Share(CompanyCatalogue.DiscountShare) + " of the " + shop.Takings + " the game's statement shows for day " + yesterday, true);
                    CampaignRules.Gave(file, given);
                }
                if (!CampaignRules.Over(file, day))
                {
                    continue;
                }
                if (!booked)
                {
                    Refresh(yesterday);   // its last day's statement with what was booked for it this morning: a give-away, wages
                    booked = true;
                }
                CompanyShop measured = shop;
                int gave = file.PushGiven;
                CampaignReport report = CampaignRules.Measure(shop.Name, file.Push, file.PushFrom, file.PushEnd - file.PushFrom, file.PushCost, gave,
                    (int d, out int customers, out int takings, out int profit) => CompanyGame.DayBook(measured, d, out customers, out takings, out profit));
                CampaignRules.Keep(_state.Reports, report);
                CampaignRules.Close(file);
                Tell(report.Extra >= 0 ? NotificationType.Success : NotificationType.Info, "reallife_co_note_report", new Dictionary<string, string>
                {
                    { "campaign", CompanyText.Campaign(report.Kind) }, { "business", shop.Name }, { "extra", CompanyText.Money(report.Extra) },
                    { "footfall", CompanyText.Percent(report.Footfall) }
                }, TabMarketing, "rep:" + (_state.Reports.Count - 1));
                if (Verbose)
                {
                    Logger?.Info("Company check (marketing, the report for " + shop.Name + "): " + (CampaignKind)report.Kind + " from day " + report.From + " for "
                                 + report.Days + " day(s); the game had figures for " + report.DaysBefore + " day(s) before and " + report.DaysDuring
                                 + " during; customers " + report.CustomersBefore + " to " + report.CustomersDuring + ", takings " + report.TakingsBefore + " to "
                                 + report.TakingsDuring + ", profit " + report.ProfitBefore + " to " + report.ProfitDuring + " (the price put back on the day it was booked, "
                                 + gave + " given away put back, the days before scaled to the days during), cost " + report.Cost
                                 + ": extra profit " + report.Extra + ", return " + report.Return + "%; the footfall figure is the game's own again from today: PASS");
                }
            }
        }

        /// <summary>What a campaign would cost today and the customers it would add from tomorrow.</summary>
        public void CampaignQuote(CompanyShop shop, int kind, out int cost, out int percent, out int suit, out int repeat)
        {
            int costShare, reach;
            Offered(shop, kind, out costShare, out reach);
            cost = (int)((long)CampaignRules.Cost(kind, _index) * costShare / 1000);
            suit = CampaignRules.Suit(kind, shop.Taste, shop.Income);
            repeat = CampaignRules.Repeat(shop.File.PushEnded[kind], Save.Day + 1);
            percent = CampaignRules.Effect(kind, suit, _cycle, repeat, reach);
        }

        /// <summary>Whether a kind can be booked: the company's own always, others when offered from outside.</summary>
        public bool Offered(CompanyShop shop, int kind, out int costShare, out int reach)
        {
            costShare = reach = 1000;
            if (kind < CompanyCatalogue.CampaignOwnKinds)
            {
                return kind >= 0;
            }
            try
            {
                return kind < CompanyCatalogue.CampaignKinds && shop != null && CompanyBridges.Outside != null
                       && CompanyBridges.Outside(kind, shop.File.Place, out costShare, out reach);
            }
            catch (System.Exception e)
            {
                Logger?.Error(e);
                costShare = reach = 1000;
                return false;
            }
        }

        /// <summary>The kinds that can be booked at a business now, the company's own first.</summary>
        public int OfferedKinds(CompanyShop shop, List<int> into)
        {
            into.Clear();
            for (int k = 0; k < CompanyCatalogue.CampaignKinds; k++)
            {
                int costShare, reach;
                if (Offered(shop, k, out costShare, out reach))
                {
                    into.Add(k);
                }
            }
            return into.Count;
        }

        public void BookCampaign(string place, int kind)
        {
            Book(place, kind, false);
        }

        // A campaign booked at a business: by the player (quiet false: a refusal is told) or by a manager (quiet: they book only what the cash covers).
        private bool Book(string place, int kind, bool quiet)
        {
            CompanyShop shop = On(CompanyPart.Marketing) ? ShopAt(place) : null;
            int share, reach;
            if (shop != null && kind >= 0 && kind < CompanyCatalogue.CampaignKinds && !CampaignRules.CanBook(shop.File))
            {
                Logger?.Info("Company: " + (CampaignKind)kind + " not booked for " + shop.Name + ": " + BusyLine(shop) + ". One campaign at a time at a business.");
                return false;
            }
            if (shop == null || kind < 0 || kind >= CompanyCatalogue.CampaignKinds || !Offered(shop, kind, out share, out reach) || !Promoted(shop))
            {
                return false;
            }
            int cost, percent, suit, repeat;
            CampaignQuote(shop, kind, out cost, out percent, out suit, out repeat);
            if (quiet && CompanyGame.Cash < cost)
            {
                return false;
            }
            if (!Cost(cost, CompanyGame.CampaignKey, shop, shop.Name, CostKind.Marketing, null, (CampaignKind)kind + " at today's prices (" + _index + ")"))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            CampaignRules.Book(shop.File, kind, cost, percent, Save.Day);
            Logger?.Info("Company: " + (CampaignKind)kind + " booked for " + shop.Name + ": cost " + cost + ", +" + percent + "% of customers (its own "
                         + CompanyCatalogue.CampaignFootfall[kind] + ", the district " + suit + ", the economy " + _cycle + ", repeats " + repeat
                         + ", thousandths), days " + shop.File.PushFrom + " to " + (shop.File.PushEnd - 1) + "; the report comes on day " + shop.File.PushEnd + ".");
            Notify(NotificationType.Success, "reallife_co_note_campaign", new Dictionary<string, string>
            {
                { "campaign", CompanyText.Campaign(kind) }, { "business", shop.Name }, { "from", shop.File.PushFrom.ToString() },
                { "to", (shop.File.PushEnd - 1).ToString() }, { "percent", percent.ToString() }
            }, 9f);
            if (kind >= CompanyCatalogue.CampaignOwnKinds && CompanyBridges.Booked != null)
            {
                try
                {
                    CompanyBridges.Booked(place, kind, shop.File.PushFrom, shop.File.PushEnd - 1, percent);
                }
                catch (System.Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Changed();
            return true;
        }

        // A manager with marketing, at their hour: a business with no campaign gets the best of the company's own kinds (the cost and the customers
        // each adds are the player's own quote) that fits what is left of the month's limit and adds enough to be worth it. The money is the company's, as
        // when the player books it, and counts against the limit; a mistake books the cheapest that fits.
        private void DecideMarketing(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!On(CompanyPart.Marketing) || mp.Shop == null || !CampaignRules.CanBook(mp.Shop.File) || !Promoted(mp.Shop))
            {
                return;
            }
            ManagerFile m = mp.Own;
            int left = ManagerRules.Left(CompanyCatalogue.AtPrices(ManagerCatalogue.Step(ManagerCatalogue.MarketingSteps, m.MarketingStep), _index), m.MarketingSpent);
            if (left <= 0)
            {
                return;
            }
            var cost = new int[CompanyCatalogue.CampaignOwnKinds];
            var effect = new int[CompanyCatalogue.CampaignOwnKinds];
            for (int k = 0; k < CompanyCatalogue.CampaignOwnKinds; k++)
            {
                int suit, repeat;
                CampaignQuote(mp.Shop, k, out cost[k], out effect[k], out suit, out repeat);
                if (k == (int)CampaignKind.DiscountDay)
                {
                    cost[k] = 0;   // a discount day costs takings, not a sum to book: it is the owner's to choose
                }
            }
            int roll;
            bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Marketing, string.Empty, out roll);
            int fair = ManagerRules.Campaign(cost, effect, cost.Length, left, ManagerCatalogue.CampaignLeast, false);
            int pick = ManagerRules.Campaign(cost, effect, cost.Length, left, ManagerCatalogue.CampaignLeast, slip);
            if (pick < 0 || !Book(mp.Place.Place, pick, true))
            {
                return;
            }
            m.MarketingSpent += cost[pick];
            Decision d = Record(mp, ManagerTask.Marketing, string.Empty, who, day, hour, pick != fair);
            d.A = pick;
            d.B = cost[pick];
            d.C = effect[pick];
            d.D = CompanyCatalogue.CampaignDays[pick];
            d.State = DecisionState.Closed;   // booked and paid: the campaign's own report says how it did
            Logger?.Info("Company: " + who + " (manager, skill " + skill + ") booked " + (CampaignKind)pick + " at " + mp.Place.Name + " for " + cost[pick] + ", +" + effect[pick]
                         + "% of customers, " + left + " left of the month's marketing limit" + (pick != fair ? ", a mistake: the cheapest that fitted" : string.Empty) + ".");
        }

        // For the log: what is booked at a business, how, and its days.
        private string BusyLine(CompanyShop shop)
        {
            int kind, from, to;
            bool started;
            if (shop == null || Save == null || !CampaignRules.Busy(shop.File, Save.Day, out kind, out from, out to, out started))
            {
                return "nothing is booked there";
            }
            return (CampaignKind)kind + " is " + (started ? "running" : "booked") + " there, " + (CampaignRules.HiredOnFlare(kind) ? "hired on Flare" : "booked from the Company app")
                   + ", days " + from + " to " + to + " (today is day " + Save.Day + ")";
        }

        /// <summary>An influencer's deal ended on Flare: its campaign is taken off, or ends today if running.</summary>
        public void EndCampaign(string place, int kind, int from)
        {
            CompanyShop shop = _running && Save != null ? ShopAt(place) : null;
            if (shop == null || shop.File.Push != kind || shop.File.PushFrom != from || !CampaignRules.HiredOnFlare(kind))
            {
                return;
            }
            int end = shop.File.PushEnd;
            bool gone = CampaignRules.EndEarly(shop.File, Save.Day);
            Logger?.Info("Company: " + (CampaignKind)kind + " for " + shop.Name + " ended on Flare by the player on day " + Save.Day
                         + (gone ? " before it began: taken off, with no report" : ": its last day is today instead of day " + (end - 1) + ", the report comes tomorrow") + ".");
            Refoot(shop);
            Changed();
        }

        partial void MarketingAlerts(int day)
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                ShopFile file = _shops[i].File;
                if (file.Push >= 0 && file.PushEnd == day + 1)
                {
                    Alert(12, PlaceNumber(file.Place), file.PushEnd, CompanyText.T("reallife_co_plan_report_title", "business", _shops[i].Name),
                        CompanyText.T("reallife_co_plan_report_what", "campaign", CompanyText.Campaign(file.Push)), null, null);
                }
            }
        }
    }
}
