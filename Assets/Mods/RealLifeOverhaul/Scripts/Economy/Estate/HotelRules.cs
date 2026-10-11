using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a night did at a hotel, for the director to pay, charge and tell.</summary>
    public struct HotelNight
    {
        public int Sold0, Sold1;    // rooms of each type sold, a group's among them
        public int Takings;
        public int Costs;           // staff, laundry, breakfast, utilities, wear
        public int Lease;           // the day's lease, if it is leased
        public bool Full;           // every room that could be sold was
        public bool Pipe;
        public int PipeCost;
        public bool BadReview;
        public bool GroupAsks;      // a group asks today (the file has its terms)
        public bool GroupLapsed;    // a group that asked was not answered
        public int Inspection;      // 0 none, 1 passed, 2 failed
        public int Fine;
        public int Review;          // the review written tonight; 0 for none
    }

    /// <summary>
    /// Owning a hotel by rule: how many rooms sell on a night, what the night takes and costs, what guests then
    /// think of the place, and what is asked for one. Game-free; every draw is <see cref="EstateRules.Roll"/>.
    /// The rooms and their going rates are Travel's own (two types to a hotel); the counts are this catalogue's.
    /// </summary>
    public static class HotelRules
    {
        private const int ForNoise = 51, ForPipe = 52, ForGroup = 53, ForGroupRooms = 54, ForGroupNights = 55, ForReview = 56;

        /// <summary>Review codes: 1 clean, 2 not clean, 3 good staff, 4 poor staff, 5 good value, 6 dear.</summary>
        public const int ReviewClean = 1, ReviewDirty = 2, ReviewStaff = 3, ReviewNoStaff = 4, ReviewValue = 5, ReviewDear = 6;

        public static int Rooms(int tier, int type)
        {
            return EstateCatalogue.Rooms[EstateCatalogue.Clamp(tier, 0, EstateCatalogue.HotelTiers - 1)][EstateCatalogue.Clamp(type, 0, EstateCatalogue.RoomTypes - 1)];
        }

        public static int Rooms(int tier)
        {
            return Rooms(tier, 0) + Rooms(tier, 1);
        }

        /// <summary>What a rate against the going rate does to the guests who come, thousandths.</summary>
        public static int RateDemand(int rate)
        {
            return EstateCatalogue.Clamp(1000 - (rate - 1000) * EstateCatalogue.RateSlope / 10, EstateCatalogue.RateDemandLeast, EstateCatalogue.RateDemandMost);
        }

        public static int RatingDemand(int rating)
        {
            return EstateCatalogue.RatingDemandLeast + EstateCatalogue.Clamp(rating, 0, 1000) * EstateCatalogue.RatingDemandSpan / 1000;
        }

        /// <summary>
        /// The share of a type's rooms that guests want on a night, thousandths (it can be over 1000: more want a
        /// room than there are). Each input moves it one way only: a better season, a weekend, an event, a better
        /// economy, a richer district, a lower rate, a better rating and a campaign never lose a guest.
        /// </summary>
        public static int Demand(int tier, int season, int weekday, int evt, int cycle, int income, int rate, int rating, bool campaign, int noise)
        {
            int[] seasons = tier == EstateCatalogue.HotelTiers - 1 ? EstateCatalogue.SeasonCottage : EstateCatalogue.SeasonCity;
            long d = EstateCatalogue.Occupancy;
            d = d * seasons[EstateCatalogue.Clamp(season, 0, 3)] / 1000;
            d = d * (weekday == 4 || weekday == 5 ? EstateCatalogue.Weekend : 1000) / 1000;
            d = d * EstateCatalogue.EventNight[EstateCatalogue.Clamp(evt, 0, EstateCatalogue.EventNight.Length - 1)] / 1000;
            d = d * EstateCatalogue.HotelCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000;
            d = d * (1000 + (EstateCatalogue.Clamp(income, EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost) - EstateCatalogue.IncomeMiddle) / EstateCatalogue.DistrictNightPerPoint) / 1000;
            d = d * RateDemand(rate) / 1000;
            d = d * RatingDemand(rating) / 1000;
            d = d * (campaign ? EstateCatalogue.CampaignDemand : 1000) / 1000;
            d = d * (1000 + EstateCatalogue.Clamp(noise, -EstateCatalogue.NightNoise, EstateCatalogue.NightNoise)) / 1000;
            return (int)Math.Max(0, d);
        }

        /// <summary>Rooms sold: what is wanted, never more than there are to sell.</summary>
        public static int Sold(int rooms, int demand)
        {
            return EstateCatalogue.Clamp((int)(((long)Math.Max(0, rooms) * Math.Max(0, demand) + 500) / 1000), 0, Math.Max(0, rooms));
        }

        /// <summary>The rate a guest pays for a type tonight.</summary>
        public static int Rate(int going, int rate)
        {
            return Math.Max(1, EstateCatalogue.Share(going, EstateCatalogue.Clamp(rate, EstateCatalogue.RateLeast, EstateCatalogue.RateMost)));
        }

        /// <summary>A night's costs: reception round the clock, housekeeping by rooms sold, maintenance, laundry, breakfast, utilities and wear, at today's prices.</summary>
        public static int Costs(int tier, int staff, int sold, int index)
        {
            int level = EstateCatalogue.Clamp(staff, 0, EstateCatalogue.StaffLevels - 1);
            int rooms = Rooms(tier);
            long cost = 24L * EstateCatalogue.ReceptionWage[level] + (long)sold * EstateCatalogue.HousekeepingPerRoom[level] + (long)rooms * EstateCatalogue.MaintenancePerRoom[level];
            cost = cost * EstateCatalogue.StaffTier[EstateCatalogue.Clamp(tier, 0, EstateCatalogue.HotelTiers - 1)] / 1000;
            cost += (long)sold * (EstateCatalogue.LaundryPerRoom + EstateCatalogue.BreakfastPerRoom[EstateCatalogue.Clamp(tier, 0, EstateCatalogue.HotelTiers - 1)] + EstateCatalogue.UtilitiesPerSold + EstateCatalogue.WearPerSold);
            cost += (long)rooms * EstateCatalogue.UtilitiesPerRoom;
            return (int)(cost * EstateCatalogue.Clamp(index, 500, 4000) / 1000);
        }

        public static int PriceScore(int rate)
        {
            return EstateCatalogue.Clamp(EstateCatalogue.PriceScoreAtGoing - (rate - 1000) * EstateCatalogue.PriceScoreSlope / 10, 0, 1000);
        }

        /// <summary>Where a rating heads: cleanliness, staffing and the price against the going rate.</summary>
        public static int RatingTarget(int clean, int staff, int rate)
        {
            long t = (long)EstateCatalogue.Clamp(clean, 0, 1000) * EstateCatalogue.RatingClean + (long)EstateCatalogue.StaffScore[EstateCatalogue.Clamp(staff, 0, EstateCatalogue.StaffLevels - 1)] * EstateCatalogue.RatingStaff
                     + (long)PriceScore(rate) * EstateCatalogue.RatingPrice;
            return (int)(t / 1000);
        }

        /// <summary>What a hotel is asked for as a going concern: so many years of its ordinary takings, as the property market stands.</summary>
        public static int Price(int tier, int going0, int going1, int real, int daysPerYear)
        {
            long night = (long)Rooms(tier, 0) * Math.Max(0, going0) + (long)Rooms(tier, 1) * Math.Max(0, going1);
            long price = night * EstateCatalogue.Occupancy / 1000 * Math.Max(8, daysPerYear) * EstateCatalogue.HotelMultiple / 1000;
            return (int)Math.Min(int.MaxValue / 4, price * EstateCatalogue.Clamp(real, EstateCatalogue.RealLeast, EstateCatalogue.RealMost) / 1000);
        }

        public static int LeaseDaily(int tier, int price, int daysPerYear)
        {
            return Math.Max(1, (int)((long)price * EstateCatalogue.LeaseShare[EstateCatalogue.Clamp(tier, 0, EstateCatalogue.HotelTiers - 1)] / 1000 / Math.Max(8, daysPerYear)));
        }

        public static int LeaseDeposit(int tier, int price, int daysPerYear)
        {
            return LeaseDaily(tier, price, daysPerYear) * EstateCatalogue.LeaseDepositDays;
        }

        public static int CampaignCost(int tier, int index)
        {
            return EstateCatalogue.Share((long)Rooms(tier) * EstateCatalogue.CampaignPerRoom, EstateCatalogue.Clamp(index, 500, 4000));
        }

        public static int Stars(int rating)
        {
            return 1 + EstateCatalogue.Clamp(rating, 0, 999) / 200;
        }

        public static bool AcceptGroup(HotelFile f)
        {
            if (f.AskRooms <= 0)
            {
                return false;
            }
            f.GroupRooms = f.AskRooms;
            f.GroupNights = f.AskNights;
            f.GroupRate = f.AskRate;
            f.AskRooms = f.AskNights = f.AskRate = f.AskLapse = 0;
            return true;
        }

        public static void RefuseGroup(HotelFile f)
        {
            f.AskRooms = f.AskNights = f.AskRate = f.AskLapse = 0;
        }

        /// <summary>A pipe bursts: a tenth of the larger type's rooms are out of use for two nights. Returns the bill.</summary>
        public static int Pipe(HotelFile f, int day, int index)
        {
            f.OutRooms = Math.Max(1, EstateCatalogue.Share(Rooms(f.Tier, 0), EstateCatalogue.PipeRoomsShare));
            f.OutEnd = day + EstateCatalogue.PipeNights;
            return EstateCatalogue.Share((long)Rooms(f.Tier) * EstateCatalogue.PipePerRoom, EstateCatalogue.Clamp(index, 500, 4000));
        }

        /// <summary>A group asks for rooms of the larger type at a discount. False while one is staying or another has asked.</summary>
        public static bool AskGroup(HotelFile f, int seed, int day)
        {
            if (f.AskRooms > 0 || f.GroupNights > 0)
            {
                return false;
            }
            int key = EstateRules.Hash(f.Slot);
            f.AskRooms = Math.Max(1, EstateCatalogue.Share(Rooms(f.Tier, 0), EstateRules.Between(EstateRules.Roll(seed, day, ForGroupRooms, key), EstateCatalogue.GroupLeast, EstateCatalogue.GroupMost)));
            f.AskNights = EstateRules.Between(EstateRules.Roll(seed, day, ForGroupNights, key), EstateCatalogue.GroupNightsLeast, EstateCatalogue.GroupNightsMost);
            f.AskRate = Math.Max(1, EstateCatalogue.Share(f.Going[0], 1000 - EstateCatalogue.GroupDiscount));
            f.AskLapse = day + EstateCatalogue.GroupAnswerDays;
            return true;
        }

        private static void AddReview(HotelFile f, int review)
        {
            for (int i = f.Reviews.Length - 1; i > 0; i--)
            {
                f.Reviews[i] = f.Reviews[i - 1];
            }
            f.Reviews[0] = review;
        }

        /// <summary>
        /// One night. A group that is staying has its rooms first, at its own rate, out of the larger type; rooms
        /// out of use are not sold; the rest sell by <see cref="Demand"/>. Then the costs, the lease, cleanliness
        /// and the rating, a review, and what may happen: a burst pipe, a group that asks, an inspection.
        /// </summary>
        public static HotelNight Night(HotelFile f, int seed, int day, EstateClimate c)
        {
            var n = new HotelNight();
            int key = EstateRules.Hash(f.Slot);
            int tier = EstateCatalogue.Clamp(f.Tier, 0, EstateCatalogue.HotelTiers - 1);
            bool campaign = f.PushEnd >= day;
            int noise = EstateRules.Between(EstateRules.Roll(seed, day, ForNoise, key), -EstateCatalogue.NightNoise, EstateCatalogue.NightNoise);
            int out0 = f.OutEnd >= day ? Math.Min(f.OutRooms, Rooms(tier, 0)) : 0;
            int group = f.GroupNights > 0 ? Math.Min(f.GroupRooms, Rooms(tier, 0) - out0) : 0;
            int total = 0, sellable = 0;
            for (int type = 0; type < EstateCatalogue.RoomTypes; type++)
            {
                int rooms = Rooms(tier, type) - (type == 0 ? out0 + group : 0);
                int sold = Sold(rooms, Demand(tier, c.Season, c.Weekday, c.Event, c.Cycle, f.Income, f.Rate[type], f.Rating, campaign, noise));
                n.Takings += sold * Rate(f.Going[type], f.Rate[type]);
                sold += type == 0 ? group : 0;
                f.Sold[type] = sold;
                total += sold;
                sellable += Rooms(tier, type) - (type == 0 ? out0 : 0);
            }
            n.Takings += group * f.GroupRate;
            n.Sold0 = f.Sold[0];
            n.Sold1 = f.Sold[1];
            n.Full = sellable > 0 && total >= sellable;
            n.Costs = Costs(tier, f.Staff, total, c.Index);
            n.Lease = f.Leased ? f.LeaseDaily : 0;
            if (f.GroupNights > 0 && --f.GroupNights == 0)
            {
                f.GroupRooms = f.GroupRate = 0;
            }
            if (f.OutEnd > 0 && f.OutEnd <= day)
            {
                f.OutRooms = f.OutEnd = 0;
            }

            // cleanliness goes to what the contract keeps up, less on a night that is all but full; the rating follows
            int cleanTarget = EstateCatalogue.CleanTarget[EstateCatalogue.Clamp(f.Staff, 0, EstateCatalogue.StaffLevels - 1)]
                              - (sellable > 0 && (long)total * 1000 >= (long)sellable * EstateCatalogue.FullHouseShare ? EstateCatalogue.FullHouseClean : 0);
            f.Clean = EstateCatalogue.Clamp(f.Clean + EstateCatalogue.Share(cleanTarget - f.Clean, 200), 0, 1000);
            int rate = (f.Rate[0] + f.Rate[1]) / 2;
            f.Rating = EstateCatalogue.Clamp(f.Rating + EstateCatalogue.Share(RatingTarget(f.Clean, f.Staff, rate) - f.Rating, EstateCatalogue.RatingPull), 0, 1000);
            if (total > 0)
            {
                int pick = EstateRules.Roll(seed, day, ForReview, key);
                int review;
                if (f.Clean < EstateCatalogue.BadReviewBelow && pick < EstateCatalogue.BadReviewChance)
                {
                    review = ReviewDirty;
                    n.BadReview = true;
                    f.Rating = Math.Max(0, f.Rating - EstateCatalogue.BadReviewRating);
                }
                else if (pick < 333)
                {
                    review = f.Clean >= 650 ? ReviewClean : ReviewDirty;
                }
                else if (pick < 666)
                {
                    review = f.Staff >= 1 ? ReviewStaff : ReviewNoStaff;
                }
                else
                {
                    review = rate <= 1050 ? ReviewValue : ReviewDear;
                }
                AddReview(f, review);
                n.Review = review;
            }

            // a burst pipe: a bill, and a tenth of the larger type out of use for two nights
            if (EstateRules.Roll(seed, day, ForPipe, key) < EstateCatalogue.PipeChance)
            {
                n.Pipe = true;
                n.PipeCost = Pipe(f, day, c.Index);
            }

            // a group that asks: so many rooms for so many nights at a discount, to be answered within two days
            if (f.AskRooms > 0 && day > f.AskLapse)
            {
                RefuseGroup(f);
                n.GroupLapsed = true;
            }
            if (EstateRules.Roll(seed, day, ForGroup, key) < EstateCatalogue.GroupChance)
            {
                n.GroupAsks = AskGroup(f, seed, day);
            }

            // the inspector, every thirty days
            if (f.InspectDay > 0 && day >= f.InspectDay)
            {
                f.InspectDay = day + EstateCatalogue.InspectionEvery;
                if (f.Clean >= EstateCatalogue.InspectionPass)
                {
                    n.Inspection = 1;
                }
                else
                {
                    n.Inspection = 2;
                    n.Fine = EstateCatalogue.Share((long)Rooms(tier) * EstateCatalogue.InspectionFinePerRoom, EstateCatalogue.Clamp(c.Index, 500, 4000));
                    f.Rating = Math.Max(0, f.Rating - EstateCatalogue.InspectionRating);
                }
            }
            f.Takings = n.Takings;
            f.Costs = n.Costs + n.Lease;
            f.TakingsYear += n.Takings;
            f.CostsYear += n.Costs + n.Lease + n.PipeCost + n.Fine;
            return n;
        }
    }
}
