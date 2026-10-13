using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Law;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Standing, a city that reacts to the player's success (Scripts/Fame/FameBridges), with the rest of the mod.
        /// Standing calls no other feature and none calls it: what they have to do with each other is here, one line each.
        ///
        /// What Standing reads:
        ///   Followers     Flare's followers, a part of the player's recognition (FlareDirector's file)
        ///   Staff         each business's staff as the company knows them: their standing, those who stand low, a strike
        ///                 now or in the last week (CompanyDirector.ShopAt), for the workers' reputation
        ///   Holdings      the buildings the player lets and the rent asked against the market's (EstateDirector's state),
        ///                 for the neighbourhoods' opinion of rents raised
        ///   Brand         the player's brands' recognition in each district (BrandsDirector's state)
        ///   the page      the Standing page is a page of the Finance app (EconomyBridges.Standing, Finance.OpenStanding)
        ///
        /// What moves Standing, heard from the features that keep it:
        ///   Deed          a good deed that counted (Reputation.Deed): integrity
        ///   Closed        one of business pressure's issues closed (BusinessEvents.Closed): a complaint answered or
        ///                 left, praise shared, an inspection passed or failed (the customers), a strike settled or
        ///                 refused, an argument left to fester, somebody poached (the workers)
        ///
        /// What Standing does elsewhere:
        ///   Visited       a visit to a business whose staff are proud or nervous: their standing a point either way,
        ///                 through the company (CompanyDirector.Visited)
        ///
        /// Everything is read from what those features already keep in public or heard through an event they already
        /// raise; nothing of theirs is changed but through an entry of theirs. COST: nothing per frame. The reads are made
        /// once on Standing's daily round (a walk over the player's businesses and buildings) and the page when it is shown.
        /// </summary>
        public static class StandingLinks
        {
            private const int DeedIntegrity = 2;
            private const int StrikeDays = 7;          // a strike this recent still counts against the workers' reputation

            internal static void Connect()
            {
                FameBridges.Followers = Followers;
                FameBridges.Staff = Staff;
                FameBridges.Holdings = Holdings;
                FameBridges.Brand = Brand;
                FameBridges.Visited = (place, points) => CompanyDirector.Instance != null ? CompanyDirector.Instance.Visited(place, points) : 0;
                FameBridges.Open = Finance.OpenStanding;
                EconomyBridges.Standing = new StandingPage { On = FamePage.On, Spec = FamePage.Spec, Write = FamePage.Write };
                Reputation.Deed += Deed;
                BusinessEvents.Closed += IssueClosed;
            }

            internal static void Disconnect()
            {
                FameBridges.Reset();
                EconomyBridges.Standing = null;
                Reputation.Deed -= Deed;
                BusinessEvents.Closed -= IssueClosed;
            }

            private static int Followers()
            {
                FlareFile file = FlareDirector.Instance != null ? FlareDirector.Instance.File : null;
                return file != null ? file.Followers : 0;
            }

            // A business's staff as the company counts them this morning.
            private static bool Staff(string place, out int count, out int standing, out int low, out bool strike)
            {
                count = standing = low = 0;
                strike = false;
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    CompanyShop shop = company != null && company.On(CompanyPart.Staff) ? company.ShopAt(place) : null;
                    if (shop == null || shop.Staff <= 0)
                    {
                        return false;
                    }
                    int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
                    count = shop.Staff;
                    standing = shop.Standing / shop.Staff;
                    low = shop.Low;
                    strike = shop.File.Striking(day) || (shop.File.StrikeEnd > 0 && day - shop.File.StrikeEnd < StrikeDays);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Staff", e);
                    return false;
                }
            }

            private static void Holdings(List<FameHolding> into)
            {
                EstateState s = EstateDirector.Instance != null ? EstateDirector.Instance.State : null;
                for (int i = 0; s != null && i < s.Holdings.Count; i++)
                {
                    EstateHolding h = s.Holdings[i];
                    into.Add(new FameHolding { District = h.District, Let = h.Let, Ask = h.Managed ? h.Ask : FameRules.AskMarket, Rent = h.Managed && h.Let ? h.Rent : 0 });
                }
            }

            // The best of the player's brands' recognition in each district, points (the brands keep hundredths).
            private static void Brand(Dictionary<string, int> into)
            {
                BrandsState s = BrandsDirector.Instance != null ? BrandsDirector.Instance.State : null;
                for (int b = 0; s != null && b < s.Brands.Count; b++)
                {
                    List<DistrictFile> districts = s.Brands[b].Districts;
                    for (int i = 0; i < districts.Count; i++)
                    {
                        int points = districts[i].Rec / 100, best;
                        if (!string.IsNullOrEmpty(districts[i].Name) && (!into.TryGetValue(districts[i].Name, out best) || points > best))
                        {
                            into[districts[i].Name] = points;
                        }
                    }
                }
            }

            private static void Deed()
            {
                FameDirector.Instance?.Moved(FamePart.Integrity, FameCause.GoodDeed, DeedIntegrity);
            }

            // One of business pressure's issues has closed: what it says about the customers' and the workers' view of the player.
            private static void IssueClosed(string kind, string place, string outcome)
            {
                FameDirector fame = FameDirector.Instance;
                if (fame == null || fame.File == null || outcome == null)
                {
                    return;
                }
                switch (outcome)
                {
                    case "reallife_biz_complaint_voucher_done": fame.Moved(FamePart.Customers, FameCause.Complaint, 2); break;
                    case "reallife_biz_complaint_ignored": fame.Moved(FamePart.Customers, FameCause.Complaint, -2); break;
                    case "reallife_biz_praise_shared": fame.Moved(FamePart.Customers, FameCause.Praise, 1); break;
                    case "reallife_biz_health_pass":
                    case "reallife_biz_health_pass_second":
                    case "reallife_biz_fire_pass": fame.Moved(FamePart.Customers, FameCause.Inspection, 1); break;
                    case "reallife_biz_health_fail":
                    case "reallife_biz_health_fail_closed":
                    case "reallife_biz_fire_fail":
                    case "reallife_biz_fire_fail_closed": fame.Moved(FamePart.Customers, FameCause.Inspection, -2); break;
                    case "reallife_biz_strike_raised":
                    case "reallife_biz_strike_bonus_done": fame.Moved(FamePart.Workers, FameCause.Strike, 2); break;
                    case "reallife_biz_strike_walkout":
                    case "reallife_biz_strike_backed_down": fame.Moved(FamePart.Workers, FameCause.Strike, -3); break;
                    case "reallife_biz_argument_festered":
                    case "reallife_biz_poach_lost": fame.Moved(FamePart.Workers, FameCause.Staff, -1); break;
                }
            }
        }
    }
}
