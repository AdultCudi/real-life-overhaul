using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What Flare asks of the rest of the mod, filled in by Wiring/FlareLinks; each has a default so Flare works alone.</summary>
    public static partial class FlareBridges
    {
        /// <summary>The people the player has met in the social life, as Flare reads them. Default: nobody.</summary>
        public static Action<List<FlarePerson>> People;

        /// <summary>The people met as a save holds them, read while the loading screen is up. Default: nobody.</summary>
        public static Action<Dictionary<string, string>, List<FlarePerson>> SavedPeople;

        /// <summary>What the city thinks of the player, 0 to 100 (the law's public reputation). Default: 50.</summary>
        public static Func<int> PublicStanding;

        /// <summary>The election on now: bit 1 voted in it, bit 2 endorsed in it, with the candidates' names. Default: no election.</summary>
        public static CivicAsk Civic;

        public delegate int CivicAsk(out string voted, out string endorsed);

        /// <summary>The player's employer and rank (employer * 100 + rank, -1 out of work) and the employer's name. Default: out of work.</summary>
        public static CareerAsk Career;

        public delegate int CareerAsk(out string employer);

        // The influences: without them a post changes nothing outside Flare.

        /// <summary>Has a business's customers figure written again after Flare's talk about it changed, and whether it is a new day. Default: nothing.</summary>
        public static Func<BuildingRegistration, bool, bool> Footfall;

        /// <summary>A friend was tagged (the social life's number), whether in a selfie with them, and whether the post went well. Default: nothing.</summary>
        public static Action<int, bool, bool> Tagged;

        /// <summary>An honest answer to a scandal at a brand's shop: the recognition to give back (hundredths); what came back. Default: nothing.</summary>
        public static Func<string, int, int> Answered;

        /// <summary>A finished piece for the morning paper (one locale key). Default: nothing is printed.</summary>
        public static Action<string> Story;

        /// <summary>True while the player is wanted by the police. Default: never.</summary>
        public static Func<bool> Wanted;

        /// <summary>The police take notice of the player for the rest of the day. Default: nothing.</summary>
        public static Action Noticed;

        /// <summary>The employer and rank of a rota shift the player is clocked on to (employer * 100 + rank), or -1. Default: never.</summary>
        public static Func<int> OnShift;

        /// <summary>A complaint at work: the boss saw the player posting on shift. Default: nothing.</summary>
        public static Action Complaint;

        /// <summary>True while the council lets the night go on later than the city came with. Default: no.</summary>
        public static Func<bool> NightLate;

        /// <summary>The candidates of the election on now, into the list given (none between elections). Default: none.</summary>
        public static Action<List<string>> Candidates;

        /// <summary>The player's brands and the city's chains, into the list given. Default: none.</summary>
        public static Action<List<string>> Brands;

        /// <summary>What the company would charge and bring for a campaign of a kind at a business; false with the reason's key. Default: none can.</summary>
        public static QuoteAsk Quote;

        public delegate bool QuoteAsk(string place, int kind, out int cost, out int percent, out string why);

        /// <summary>Has the company book and pay a campaign of a kind at a business. Default: nothing is booked.</summary>
        public static Func<string, int, bool> Book;

        /// <summary>The company's campaigns at the player's businesses, running or booked (the list filled again). Default: none.</summary>
        public static Action<List<FlareCampaign>> Campaigns;

        /// <summary>The player ended a deal: the company's campaign that is the deal (place, kind, first day) ends with it. Default: nothing.</summary>
        public static Action<string, int, int> EndCampaign;

        /// <summary>A business that hired an influencer is hurt by their scandal: its brand hears of it. Default: nothing.</summary>
        public static Action<string> BrandScandal;

        /// <summary>A star influencer turns out to be somebody the player can still meet, introduced by a friend: their number, or -1. Default: nobody.</summary>
        public static Func<string, int> MeetStar;

        /// <summary>Somebody the player has still to meet answers a post and becomes a contact: their name, or null. Default: nobody.</summary>
        public static Func<string> MeetOnline;

        /// <summary>The partner (the social life's number) sees a post with somebody they could fall for, and how they take it. Default: nothing.</summary>
        public static Action<int, int> PartnerSees;

        /// <summary>The partner minds never being posted. Default: nothing.</summary>
        public static Action PartnerMinds;

        /// <summary>A friend (the social life's number) whose texts wait minds the player posting all day. Default: nothing.</summary>
        public static Action<int> FriendMinds;

        /// <summary>The stock market's news of the day, into the list given: results and the biggest move. Default: none.</summary>
        public static Action<List<FlareMarketNote>> Market;

        /// <summary>True if the player has sold shares of a company (its number) since a day. Default: never.</summary>
        public static Func<int, int, bool> Sold;

        /// <summary>A case of market abuse is opened (the company). Default: nothing.</summary>
        public static Action<string> MarketAbuse;

        /// <summary>A buzz on the stock market for the trade of a business (its place), one way or the other. Default: nothing.</summary>
        public static Action<string, int> MarketBuzz;

        /// <summary>The morning paper's headlines, into the list given. Default: none.</summary>
        public static Action<List<string>> Headlines;

        /// <summary>A post raised an issue (FlareIssue) in the council, by so many thousandths of a point. Default: nothing.</summary>
        public static Action<int, int> Issue;

        /// <summary>A post about the player's own run for office: what it is worth to the campaign, thousandths of a point. Default: nothing.</summary>
        public static Action<int> Campaign;

        /// <summary>The degree the player graduated with today at the university's ceremony, in words, or null. Default: never.</summary>
        public static Func<string> Graduated;

        public static void Reset()
        {
            People = null;
            SavedPeople = null;
            PublicStanding = null;
            Civic = null;
            Career = null;
            Footfall = null;
            Tagged = null;
            Answered = null;
            Story = null;
            Wanted = null;
            Noticed = null;
            OnShift = null;
            Complaint = null;
            NightLate = null;
            Candidates = null;
            Brands = null;
            Quote = null;
            Book = null;
            Campaigns = null;
            EndCampaign = null;
            BrandScandal = null;
            MeetStar = null;
            MeetOnline = null;
            PartnerSees = null;
            PartnerMinds = null;
            FriendMinds = null;
            Market = null;
            Sold = null;
            MarketAbuse = null;
            MarketBuzz = null;
            Headlines = null;
            Issue = null;
            Campaign = null;
            Graduated = null;
            ResetLive();
        }

        internal static T Ask<T>(Func<T> bridge, T otherwise)
        {
            try
            {
                return bridge != null ? bridge() : otherwise;
            }
            catch (Exception)
            {
                return otherwise;
            }
        }

        internal static int Standing()
        {
            try
            {
                return PublicStanding != null ? PublicStanding() : FlareRules.ReputationStart;
            }
            catch (Exception)
            {
                return FlareRules.ReputationStart;
            }
        }
    }

    /// <summary>The one place Flare touches the game: the clock, the player's businesses and takings. Asked on the hour, the day and a click.</summary>
    public static class FlareGame
    {
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static int Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;
        public static int MinuteOfDay => SaveGameManager.Current != null ? SaveGameManager.Current.Hour * 60 + Mathf.FloorToInt(SaveGameManager.Current.Minute) : 0;

        /// <summary>A business's place as the company and the brands write it ("street~number"), as Tech/People/TalentGame.Place has it.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return reg == null ? null : Tech.TalentGame.Place(reg);
        }

        public static string Name(BuildingRegistration reg)
        {
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : "?";
        }

        /// <summary>The player's businesses that take money, into a list of Flare's own (filled again on each call).</summary>
        public static List<BuildingRegistration> Businesses()
        {
            Books.Businesses(Regs);
            return Regs;
        }

        /// <summary>The player's business at a place, or null.</summary>
        public static BuildingRegistration Business(string place)
        {
            if (string.IsNullOrEmpty(place))
            {
                return null;
            }
            List<BuildingRegistration> all = Businesses();
            for (int i = 0; i < all.Count; i++)
            {
                if (Place(all[i]) == place)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>What a business took yesterday, from the game's statement of that day; nothing if it has none.</summary>
        public static int Takings(BuildingRegistration reg)
        {
            return reg != null ? Mathf.RoundToInt(Books.SalesYesterday(reg.Address)) : 0;
        }

        /// <summary>The game's satisfaction figures of a business (0 to 100): overall, then service, pricing, cleanliness, facility. False with none.</summary>
        public static bool Satisfaction(BuildingRegistration reg, out int overall, int[] parts)
        {
            overall = 0;
            Satisfaction s = reg != null ? reg.satisfaction : null;
            if (s == null)
            {
                return false;
            }
            overall = s.overall;
            parts[0] = s.customerService;
            parts[1] = s.pricing;
            parts[2] = s.cleanliness;
            parts[3] = s.facility;
            return true;
        }

        /// <summary>A viral post's game hype for one retail product the business sells and not yet hyped there, as ProductMarketHelper makes one; null for none.</summary>
        public static string Hype(BuildingRegistration reg)
        {
            List<string> products = reg != null ? reg.cachedAvailableProducts : null;
            string district = GameRegistrations.District(reg);
            if (products == null || string.IsNullOrEmpty(district) || SaveGameManager.Current == null || SaveGameManager.Current.marketEvents == null)
            {
                return null;
            }
            for (int i = 0; i < products.Count; i++)
            {
                BigAmbitions.Items.Item item = BigAmbitions.Items.ItemsGetter.GetByName(products[i], true);
                if (item == null || (item.type & BigAmbitions.Items.ItemType.RetailProduct) == 0 || item.GetWholesalePrice() <= 0f
                    || ProductMarketHelper.IsProductInMarketEvent(products[i], MarketEventType.Hype, district))
                {
                    continue;
                }
                ProductMarketHelper.CreateItemRelatedMarketEvent(MarketEventType.Hype, products[i], district);
                return products[i];
            }
            return null;
        }

        private static readonly List<EmployeeInstance> Staffed = new List<EmployeeInstance>(16);

        /// <summary>The city's districts by the game's own names (not its "everywhere"), into the list given.</summary>
        public static void Districts(List<string> into)
        {
            into.Clear();
            try
            {
                CityCheck.NewYorkDistricts(NeighborhoodHelper.Neighborhoods, into);   // Flare's people are New York's, wherever the player is
            }
            catch (Exception)
            {
                into.Clear();   // the game's list is not there: influencers have no home district
            }
        }

        /// <summary>The names of the game's own rivals, into the list given (none while the game's rivals are off).</summary>
        public static void Rivals(List<string> into)
        {
            into.Clear();
            try
            {
                if (SaveGameManager.Current == null || !RivalsHelper.IsFeatureEnabled)
                {
                    return;
                }
                IReadOnlyCollection<SpecialRival> rivals = RivalsHelper.GetSpecialRivals();
                if (rivals == null)
                {
                    return;
                }
                foreach (SpecialRival rival in rivals)
                {
                    string name = rival != null && rival.rivalData != null ? rival.rivalData.rivalName : null;
                    if (!string.IsNullOrEmpty(name) && !into.Contains(name) && into.Count < 8)
                    {
                        into.Add(name);
                    }
                }
            }
            catch (Exception)
            {
                into.Clear();
            }
        }

        /// <summary>True if any business of the player's has staff, one of whom may take up posting. Asked once a month.</summary>
        public static bool HasStaff()
        {
            List<BuildingRegistration> all = Businesses();
            for (int i = 0; i < all.Count; i++)
            {
                Staffed.Clear();
                try
                {
                    EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = all[i].Address, excludeBeingReplaced = true }, Staffed);
                }
                catch (Exception)
                {
                    Staffed.Clear();
                }
                if (Staffed.Count > 0)
                {
                    Staffed.Clear();
                    return true;
                }
            }
            return false;
        }

        /// <summary>The player's businesses as influencers see them, into the list given.</summary>
        public static void Shops(FlareFile f, List<FlareShop> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Businesses();
            for (int i = 0; i < all.Count; i++)
            {
                string place = Place(all[i]);
                FlareShopSeen seen = f.Shop(place);
                into.Add(new FlareShop
                {
                    Place = place, Name = Name(all[i]), Niche = FlareCast.NicheOf(all[i].businessTypeName), District = GameRegistrations.District(all[i]),
                    OpenedDay = FlareRules.OpenedOn(FoundingGame.Of(all[i]), seen != null ? seen.FirstDay : f.StartDay)
                });
            }
        }

        /// <summary>A district by the game's own name, in words.</summary>
        public static string DistrictName(string district)
        {
            return string.IsNullOrEmpty(district) ? FlareText.T("reallife_flare_district_none") : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>The business of the player's they are standing in, or null.</summary>
        public static BuildingRegistration Inside()
        {
            if (!BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance == null)
            {
                return null;
            }
            BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
            return here != null && Books.IsBusiness(here) ? here : null;
        }
    }
}
