using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Marketing that is measured: what the director does with <see cref="CampaignRules"/>.
    ///
    /// WHAT THE GAME ALREADY HAS, AND IS LEFT TO IT: a standing marketing budget for a business, placed through a
    /// marketing agency (BuildingRegistration.marketingCampaigns). It costs a sum every day for as long as it is
    /// switched on, raises the business's "marketing" figure and with it the promotion figure the customers follow
    /// (BusinessHelper.UpdatePromotion), has no end and gives no account of itself.
    ///
    /// WHAT IS ADDED BESIDE IT: a campaign with a price, a length and an end, and a report afterwards. It is bought
    /// once (a transaction of the mod's own, deductible: way c), adds customers for its days through business
    /// pressure's footfall figure, on top of the game's own marketing and inside business pressure's bounds (way a),
    /// and a discount day gives away a share of that day's takings (a transaction: way c). The report compares the
    /// campaign's days with the same number before them from the game's own figures and is kept, the last eight.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        partial void MarketingDay(int day)
        {
            int yesterday = day - 1;
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
                    // The takings a discount day gave away never came in: taken whatever the cash is, and part of the campaign's cost.
                    int given = CampaignRules.Discount(shop.Takings);
                    Cost(given, CompanyGame.DiscountKey, shop, shop.Name, CostKind.Marketing, yesterday,
                        CompanyText.Share(CompanyCatalogue.DiscountShare) + " of the " + shop.Takings + " the game's statement shows for day " + yesterday, true);
                    file.PushCost += given;
                }
                if (!CampaignRules.Over(file, day))
                {
                    continue;
                }
                CompanyShop measured = shop;
                CampaignReport report = CampaignRules.Measure(shop.Name, file.Push, file.PushFrom, file.PushEnd - file.PushFrom, file.PushCost,
                    (int d, out int customers, out int takings, out int profit) => CompanyGame.DayBook(measured, d, out customers, out takings, out profit));
                CampaignRules.Keep(_state.Reports, report);
                CampaignRules.Close(file);
                Tell(report.Extra >= 0 ? NotificationType.Success : NotificationType.Info, "reallife_co_note_report", new Dictionary<string, string>
                {
                    { "campaign", CompanyText.Campaign(report.Kind) }, { "business", shop.Name }, { "extra", CompanyText.Money(report.Extra) }, { "footfall", CompanyText.Percent(report.Footfall) }
                }, TabMarketing, "rep:" + (_state.Reports.Count - 1));
                if (Verbose)
                {
                    Logger?.Info("Company check (marketing, the report for " + shop.Name + "): " + (CampaignKind)report.Kind + " from day " + report.From + " for " + report.Days + " day(s); the game had figures for "
                                 + report.DaysBefore + " day(s) before and " + report.DaysDuring + " during; customers " + report.CustomersBefore + " to " + report.CustomersDuring + ", takings " + report.TakingsBefore + " to "
                                 + report.TakingsDuring + ", profit " + report.ProfitBefore + " to " + report.ProfitDuring + ", cost " + report.Cost + ": extra profit " + report.Extra + ", return " + report.Return
                                 + "%; the footfall figure is the game's own again from today: PASS");
                }
            }
        }

        // ---------------------------------------------------------------- what the app asks

        /// <summary>What a campaign would cost at a business today and the customers it would add from tomorrow, with the parts of that.</summary>
        public void CampaignQuote(CompanyShop shop, int kind, out int cost, out int percent, out int suit, out int repeat)
        {
            int costShare = 1000, reach = 1000;
            Offered(shop, kind, out costShare, out reach);
            cost = (int)((long)CampaignRules.Cost(kind, _index) * costShare / 1000);
            suit = CampaignRules.Suit(kind, shop.Taste, shop.Income);
            repeat = CampaignRules.Repeat(shop.File.PushEnded[kind], Save.Day + 1);
            percent = CampaignRules.Effect(kind, suit, _cycle, repeat, reach);
        }

        /// <summary>Whether a kind of campaign can be booked at a business: the company's own always; another only when it is offered from outside, with its figures.</summary>
        public bool Offered(CompanyShop shop, int kind, out int costShare, out int reach)
        {
            costShare = reach = 1000;
            if (kind < CompanyCatalogue.CampaignOwnKinds)
            {
                return kind >= 0;
            }
            try
            {
                return kind < CompanyCatalogue.CampaignKinds && shop != null && CompanyBridges.Outside != null && CompanyBridges.Outside(kind, shop.File.Place, out costShare, out reach);
            }
            catch (System.Exception e)
            {
                Logger?.Error(e);
                costShare = reach = 1000;
                return false;
            }
        }

        /// <summary>The kinds of campaign that can be booked at a business now, into the list given: the company's own first.</summary>
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
            CompanyShop shop = On(CompanyPart.Marketing) ? ShopAt(place) : null;
            int share, reach;
            if (shop == null || kind < 0 || kind >= CompanyCatalogue.CampaignKinds || !Offered(shop, kind, out share, out reach) || !CampaignRules.CanBook(shop.File) || !Promoted(shop))
            {
                return;
            }
            int cost, percent, suit, repeat;
            CampaignQuote(shop, kind, out cost, out percent, out suit, out repeat);
            if (!Cost(cost, CompanyGame.CampaignKey, shop, shop.Name, CostKind.Marketing, null, (CampaignKind)kind + " at today's prices (" + _index + ")"))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            CampaignRules.Book(shop.File, kind, cost, percent, Save.Day);
            Logger?.Info("Company: " + (CampaignKind)kind + " booked for " + shop.Name + ": cost " + cost + ", +" + percent + "% of customers (its own " + CompanyCatalogue.CampaignFootfall[kind] + ", the district "
                         + suit + ", the economy " + _cycle + ", repeats " + repeat + ", thousandths), days " + shop.File.PushFrom + " to " + (shop.File.PushEnd - 1) + "; the report comes on day " + shop.File.PushEnd + ".");
            Notifications.Show(NotificationType.Success, "reallife_co_note_campaign", new Dictionary<string, string>
            {
                { "campaign", CompanyText.Campaign(kind) }, { "business", shop.Name }, { "from", shop.File.PushFrom.ToString() }, { "to", (shop.File.PushEnd - 1).ToString() }, { "percent", percent.ToString() }
            }, 9f, null, null, true, false);
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
