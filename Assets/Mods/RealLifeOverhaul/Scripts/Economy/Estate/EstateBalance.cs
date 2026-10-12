using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What each part of the property business returns in a year, worked out by running the rules themselves over
    /// some years of days from a seed: nothing here is a second set of figures. The offline checks hold the results
    /// to the table in <see cref="EstateCatalogue"/>, and the Guide prints them. Game-free; never called in play
    /// except when the Guide's page is opened.
    /// </summary>
    public static class EstateBalance
    {
        private const int Value = 1000000;      // the building the letting figures are worked out on
        private const int Years = 6;

        /// <summary>
        /// A let building's net return a year, hundredths of a per cent of its value: the rent that came in, less
        /// landlord's cover, the court's fees and what it costs to put the wear right, with a landlord who takes
        /// the first applicant with a fair record (or anybody after two days), serves notice when he may and goes
        /// to court when he may.
        /// </summary>
        public static int LetYearlyBp(int use, int income, int cycle, int seed)
        {
            EstateClimate c = EstateClimate.Steady;
            c.Cycle = cycle;
            var h = new EstateHolding { Place = "Balance~" + use, Base = Value, Use = use, AsBuilt = use, Income = income, Cond = 900, Cover = true, Managed = true };
            int days = Years * c.DaysPerYear;
            long net = 0;
            bool pays = false;
            for (int day = 1; day <= days; day++)
            {
                net += pays ? h.Rent : 0;
                EstateRules.Lapse(h, day);
                EstateRules.Apply(h, seed, day, c);
                for (int i = 0; !h.Let && i < h.Applicants.Count; i++)
                {
                    if (h.Applicants[i].Record >= 50 || h.VoidDays >= 2)
                    {
                        EstateRules.Accept(h, h.Applicants[i].Id, day);
                    }
                }
                TenancyDay d = EstateRules.Day(h, seed, day, c);
                pays = d.Pays;
                net += d.Lump;
                net += EstateRules.Hazard(h, seed, day, c).Payout;
                net -= EstateRules.Premium(h.Base, c);
                if (EstateRules.ServeNotice(h, day, c.RentLever))
                {
                    continue;
                }
                if (EstateRules.GoToCourt(h, day))
                {
                    net -= EstateRules.CourtFee(c.Index);
                }
            }
            net -= EstateRules.RepairCost(h.Base, h.Cond, c.Index) - EstateRules.RepairCost(h.Base, 900, c.Index);
            long worth = EstateRules.Value(Value, EstateCatalogue.RealStart, c.Index, income, 900, 0);
            return (int)(net * 10000 / worth / Years);
        }

        /// <summary>
        /// A hotel's net return a year, hundredths of a per cent of its price, bought or leased (a lease ties up only
        /// its deposit): takings less the night's costs, the lease, burst pipes and fines, at the going rate.
        /// </summary>
        public static int HotelYearlyBp(int tier, int going0, int going1, int staff, bool leased, int cycle, int seed)
        {
            EstateClimate c = EstateClimate.Steady;
            c.Cycle = cycle;
            int price = HotelRules.Price(tier, going0, going1, EstateCatalogue.RealStart, c.DaysPerYear);
            var f = new HotelFile { Slot = "balance" + tier, Tier = tier, Staff = staff, Leased = leased, LeaseDaily = leased ? HotelRules.LeaseDaily(tier, price, c.DaysPerYear) : 0, InspectDay = EstateCatalogue.InspectionEvery };
            f.Going[0] = going0;
            f.Going[1] = going1;
            int days = Years * c.DaysPerYear;
            long net = 0;
            for (int day = 1; day <= days; day++)
            {
                c.Season = CalendarRules.SeasonFromSpring(day, c.DaysPerYear);   // the weather's seasons, counted from spring
                c.Weekday = (day - 1) % 7;
                HotelNight n = HotelRules.Night(f, seed, day, c);
                net += n.Takings - n.Costs - n.Lease - n.PipeCost - n.Fine;
                if (f.AskRooms > 0)
                {
                    HotelRules.RefuseGroup(f);   // (the figures are of a hotel that takes no groups)
                }
            }
            return (int)(net * 10000 / Math.Max(1, price) / Years);
        }

        /// <summary>
        /// A hire car's net return a year, hundredths of a per cent of its list price: hire money and deposits
        /// kept, less cleaning, fleet cover, repairs the cover did not meet, and the car itself if it never came back.
        /// </summary>
        public static int HireYearlyBp(int price, int cycle, int seed)
        {
            EstateClimate c = EstateClimate.Steady;
            c.Cycle = cycle;
            var car = new HireCar { Id = "balance" + price, Price = price };
            int days = Years * c.DaysPerYear, rating = EstateCatalogue.HireRatingStart;
            long net = 0;
            for (int day = 1; day <= days; day++)
            {
                c.Season = CalendarRules.SeasonFromSpring(day, c.DaysPerYear);   // the weather's seasons, counted from spring
                net -= HireRules.Premium(price, c);
                if (car.State == (int)HireState.Out && day >= car.Day)
                {
                    HireReturn r = HireRules.End(car, rating, true, c);
                    net += r.Rent + r.Kept + r.Payout - r.Repair - r.Cleaning;
                    net -= r.Stolen ? EstateCatalogue.Share(price, EstateCatalogue.HireResidual) : 0;
                    rating = HireRules.Rated(rating, true);
                    HireRules.Settle(car, r);
                    car.State = (int)HireState.Idle;
                    continue;   // (the day it comes back it is cleaned)
                }
                HireRules.TryHire(car, seed, day, c, rating);
            }
            return (int)(net * 10000 / Math.Max(1, price) / Years);
        }

        /// <summary>
        /// What the agency takes in a year before wages and the office's rent, in dollars: commissions less the
        /// marketing of everything taken on, with a day's capacity in tenths of an agent's hour and every
        /// instruction taken on.
        /// </summary>
        public static int AgencyYearly(int capacity, int sqmPrice, int cycle, int seed)
        {
            EstateClimate c = EstateClimate.Steady;
            c.Cycle = cycle;
            var agency = new AgencyFile { Office = "Balance~1", Capacity = capacity };
            agency.Districts.Add(new DistrictRep { Name = "a", SqmPrice = sqmPrice });
            agency.Districts.Add(new DistrictRep { Name = "b", SqmPrice = sqmPrice });
            var events = new List<AgencyEvent>(8);
            int days = Years * c.DaysPerYear;
            long net = 0;
            for (int day = 1; day <= days; day++)
            {
                Instruction i = AgencyRules.Arrive(agency, seed, day, c, EstateCatalogue.RealStart);
                if (i != null)
                {
                    net -= AgencyRules.TakeCost(i);
                    AgencyRules.Take(i, day);
                }
                events.Clear();
                AgencyRules.Day(agency, seed, day, c, events);
                for (int e = 0; e < events.Count; e++)
                {
                    net += events[e].Kind == AgencyEvent.Completed ? events[e].Amount : 0;
                }
            }
            return (int)(net / Years);
        }

        /// <summary>What a shop set up with the money of a point in the game (0 early, 1 mid, 2 late) is taken to return a year, hundredths of a per cent.</summary>
        public static int ShopYearlyBp(int point)
        {
            return EstateCatalogue.ShopYearlyBp[EstateCatalogue.Clamp(point, 0, EstateCatalogue.ShopYearlyBp.Length - 1)];
        }
    }
}
