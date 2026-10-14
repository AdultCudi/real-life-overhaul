using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>What a dealership's hour has to go on, as DealerGame reads it from the game.</summary>
    public struct YardHour
    {
        public bool Open;                   // open now by its opening hours, and not closed for anything
        public float ShopCustomers;         // the customers an hour the game's own count gives a shop of this building (its capacity)
        public int Promotion;               // the building's promotion as the game and the mod's features have written it (0 to 100)
        public float BaseMultiplier;        // the game's own base for the promotion (gameVariables.baseCustomerPromotionMultiplier)
        public int Satisfaction;            // the game's customer satisfaction of the building (0 to 100; 50 is even)
        public int Staff;                   // salespeople on shift there now
        public int Skill;                   // their average customer service skill (0 to 100)
    }

    /// <summary>What an hour of a dealership came to.</summary>
    public struct DealerHour
    {
        public int Visitors, Served, Lost, Offers;
    }

    public static partial class DealerRules
    {
        // ---------------------------------------------------------------- visitors

        /// <summary>
        /// Thousandths of a visitor in an hour: the customers the game's own count gives a shop of the building at its
        /// promotion (capacity x (base + 0.75 x promotion / 100), as CustomerEntriesCalculator counts them), a dealership's
        /// share of them, the hour of the day, how much the lot has to show, and the building's customer satisfaction.
        /// </summary>
        public static long VisitorThousandths(in YardHour h, int hour, int cars)
        {
            if (!h.Open || h.ShopCustomers <= 0f || cars <= 0)
            {
                return 0;
            }
            double shop = h.ShopCustomers * (h.BaseMultiplier + 0.75 * DealerCatalogue.Clamp(h.Promotion, 0, 100) / 100.0);
            int pull = Math.Min(1000, DealerCatalogue.StockPullLeast + (cars - 1) * DealerCatalogue.StockPullCar);
            int satisfaction = 1000 + (DealerCatalogue.Clamp(h.Satisfaction, 0, 100) - 50) * DealerCatalogue.SatisfactionWeight / 50;
            double v = shop * DealerCatalogue.VisitorShare * DealerCatalogue.HourWeight[DealerCatalogue.Clamp(hour, 0, 23)] / 1000.0 * pull / 1000.0 * satisfaction / 1000.0;
            return (long)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        /// <summary>Whole visitors this hour, with the thousandths left over carried to the next.</summary>
        public static int Visitors(in YardHour h, int hour, int cars, ref int carry)
        {
            long t = VisitorThousandths(h, hour, cars) + Math.Max(0, carry);
            int whole = (int)Math.Min(1000, t / 1000);
            carry = (int)(t - whole * 1000L);
            if (!h.Open)
            {
                carry = 0;
            }
            return whole;
        }

        /// <summary>How many of them are looked after: each salesperson on shift sees <see cref="DealerCatalogue.ServePerStaff"/> an hour; with nobody there, none.</summary>
        public static int Served(int visitors, int staff)
        {
            return Math.Max(0, Math.Min(visitors, staff * DealerCatalogue.ServePerStaff));
        }

        /// <summary>How a car's asking price against its worth draws buyers (thousandths).</summary>
        public static int PriceDraw(int asking, int worth)
        {
            if (worth <= 0)
            {
                return 0;
            }
            return Line(DealerCatalogue.PriceAt, DealerCatalogue.PriceDraw, (int)Math.Min(5000L, asking * 1000L / worth));
        }

        /// <summary>How a car's days on the lot draw buyers (thousandths): a new arrival is looked at, a car that has stood long less.</summary>
        public static int AgeDraw(int days)
        {
            return Line(DealerCatalogue.AgeAt, DealerCatalogue.AgeDraw, Math.Max(0, days));
        }

        /// <summary>The chance a visitor who is looked after makes an offer on a car (thousandths).</summary>
        public static int OfferChance(int asking, int worth, int days, int skill)
        {
            return OfferChance(asking, worth, days, skill, 1000, 1000);
        }

        /// <summary>
        /// The same for a car of this condition (a tired one draws fewer offers: <see cref="ConditionDraw"/>), with what an
        /// event, the featured kind and the showroom's reputation make of it (<paramref name="lift"/>, thousandths of itself).
        /// </summary>
        public static int OfferChance(int asking, int worth, int days, int skill, int condition, int lift)
        {
            int skillFactor = DealerCatalogue.SkillLeast + (DealerCatalogue.SkillMost - DealerCatalogue.SkillLeast) * DealerCatalogue.Clamp(skill, 0, 100) / 100;
            long c = (long)DealerCatalogue.OfferChance * PriceDraw(asking, worth) / 1000 * AgeDraw(days) / 1000 * skillFactor / 1000;
            c = c * ConditionDraw(condition) / 1000 * Math.Max(0, lift) / 1000;
            return (int)Math.Min(900, c);
        }

        /// <summary>
        /// The car a visitor looks at: one of those on the lot that is not out on a test drive or in the workshop and has no
        /// offer waiting, the more likely the better it draws (its price, its days on the lot, its condition, and the kind of
        /// car an event features: a bit for each such car in <paramref name="featured"/>). -1 if none can be looked at.
        /// </summary>
        public static int LookAt(DealerYard y, int now, int day, IList<int> worths, int featured, ref DealerDice dice)
        {
            long total = 0;
            for (int c = 0; c < y.Cars.Count; c++)
            {
                total += Draw(y, c, now, day, worths, featured);
            }
            if (total <= 0)
            {
                return -1;
            }
            long at = (long)(dice.Next() % (ulong)total);
            for (int c = 0; c < y.Cars.Count; c++)
            {
                at -= Draw(y, c, now, day, worths, featured);
                if (at < 0)
                {
                    return c;
                }
            }
            return -1;
        }

        private static long Draw(DealerYard y, int c, int now, int day, IList<int> worths, int featured)
        {
            StockCar car = y.Cars[c];
            if (car.OutUntil > now || car.ShopUntil > now || HasDeal(y, car.Id))
            {
                return 0;
            }
            long draw = 1L + (long)PriceDraw(car.Asking, worths[c]) * AgeDraw(day - car.Bought) / 1000 * ConditionDraw(car.Condition) / 1000;
            return IsFeatured(featured, c) ? draw * DealerCatalogue.FeaturedDraw / 1000 : draw;
        }

        private static bool IsFeatured(int featured, int c)
        {
            return c < 31 && (featured & (1 << c)) != 0;
        }

        public static bool HasDeal(DealerYard y, int car)
        {
            for (int d = 0; d < y.Deals.Count; d++)
            {
                if (y.Deals[d].Car == car)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- an offer

        /// <summary>
        /// A buyer's offer on a car: who they are, what they first offer and the most they would pay (shares of the asking
        /// price by the kind of buyer; a skilled salesperson lifts the first offer), whether they want a test drive first,
        /// to pay over time, a warranty, and whether they bring a car of their own in part exchange.
        /// </summary>
        public static DealerOffer Offer(int id, StockCar car, int now, int skill, bool warranty, int financeLeastAtPrices, IList<MarketModel> models, IList<string> colours, int level, ref DealerDice dice)
        {
            return Offer(id, car, now, skill, warranty, financeLeastAtPrices, models, colours, level, OfferMood.Plain, ref dice);
        }

        /// <summary>
        /// The same, with what the showroom's event and reputation make of the buyer (<see cref="Mood"/>): a higher first
        /// offer, more who ask for a test drive or bring a car in part exchange and a better allowance for it, a counter
        /// more often taken, more who take a warranty, and the extras they would take. The draws of a plain mood are the
        /// same as the rules made before any of it.
        /// </summary>
        public static DealerOffer Offer(int id, StockCar car, int now, int skill, bool warranty, int financeLeastAtPrices, IList<MarketModel> models, IList<string> colours, int level,
            in OfferMood mood, ref DealerDice dice)
        {
            int profile = dice.Pick(DealerCatalogue.ProfileShare);
            int offerShare = dice.Range(DealerCatalogue.OfferLeast[profile], DealerCatalogue.OfferMost[profile]) + DealerCatalogue.SkillOffer * DealerCatalogue.Clamp(skill, 0, 100) / 100 + mood.First;
            int reach = dice.Range(DealerCatalogue.ReachLeast[profile], DealerCatalogue.ReachMost[profile]);
            var d = new DealerOffer
            {
                Id = id, Car = car.Id, Customer = CustomerName(ref dice), Profile = profile, Made = now, Expires = now + DealerCatalogue.DealHours,
                Offer = DealerCatalogue.Named(DealerCatalogue.Share(car.Asking, Math.Min(1000, offerShare))),
                Ceiling = DealerCatalogue.Named(DealerCatalogue.Share(car.Asking, Math.Min(DealerCatalogue.CeilingMost, offerShare + reach))),
                WantsDrive = dice.Chance(DealerCatalogue.TestDriveChance + mood.Drive), Lift = Math.Max(0, mood.Accept)
            };
            d.Ceiling = Math.Max(d.Offer, d.Ceiling);
            if (car.Asking >= financeLeastAtPrices && dice.Chance(DealerCatalogue.FinanceWant))
            {
                d.WantsFinance = true;
                d.Score = dice.Range(DealerCatalogue.ScoreLeast, DealerCatalogue.ScoreMost);
                d.Deposit = DealerCatalogue.DepositLeast + dice.Range(0, (DealerCatalogue.DepositMost - DealerCatalogue.DepositLeast) / DealerCatalogue.DepositStep) * DealerCatalogue.DepositStep;
                d.Term = ProductCatalogue.CarTermLeast + dice.Range(0, (ProductCatalogue.CarTermMost - ProductCatalogue.CarTermLeast) / ProductCatalogue.CarTermStep) * ProductCatalogue.CarTermStep;
            }
            for (int t = DealerCatalogue.Tiers - 1; warranty && t >= 0 && d.Tier < 0; t--)
            {
                if (dice.Chance(DealerCatalogue.TierTakeUp[t] + (profile == 2 ? 50 : 0) + mood.TakeUp))
                {
                    d.Tier = t;
                }
            }
            if (models != null && models.Count > 0 && dice.Chance(DealerCatalogue.TradeInChance + mood.TradeIn))
            {
                MarketModel m = models[dice.Range(0, models.Count - 1)];
                DealerListing own = Used(0, m, colours, level, ref dice);
                int worth = Worth(m.Price, level, own.Km, own.Condition);
                int allowance = DealerCatalogue.Named(DealerCatalogue.Share(worth, dice.Range(DealerCatalogue.AllowanceLeast, DealerCatalogue.AllowanceMost) + mood.Allowance));
                if (worth > 0 && allowance <= DealerCatalogue.Share(d.Offer, DealerCatalogue.TradeInMost))
                {
                    d.TradeType = own.Type;
                    d.TradeColour = own.Colour;
                    d.TradeKm = own.Km;
                    d.TradeCondition = own.Condition;
                    d.TradeAllowance = allowance;
                }
            }
            d.Extras = DrawExtras(profile, mood, ref dice);
            return d;
        }

        /// <summary>True when the staff may close the deal on their own: the offer is at or above the dealership's floor for them, and the buyer needs no test drive first.</summary>
        public static bool StaffClose(DealerOffer d, StockCar car, int staffFloor)
        {
            return d.Stage == OfferStage.Open && (!d.WantsDrive || d.Drove) && d.Offer >= DealerCatalogue.Share(car.Asking, staffFloor);
        }

        /// <summary>
        /// The chance a counter is taken (thousandths): certain at or under the buyer's offer, falling to
        /// <see cref="DealerCatalogue.AcceptAtCeiling"/> at their ceiling, nothing above it; a test drive helps, and so does
        /// an event that ran when they came (the offer's lift).
        /// </summary>
        public static int AcceptChance(DealerOffer d, int counter)
        {
            if (counter <= d.Offer)
            {
                return 1000;
            }
            if (counter > d.Ceiling)
            {
                return 0;
            }
            int span = Math.Max(1, d.Ceiling - d.Offer);
            int chance = 1000 - (int)((1000L - DealerCatalogue.AcceptAtCeiling) * (counter - d.Offer) / span);
            return Math.Min(1000, chance + (d.Drove ? DealerCatalogue.TestDriveLift : 0) + Math.Max(0, d.Lift));
        }

        /// <summary>The prices the player may counter with: from the buyer's offer to the asking price, in up to eleven steps.</summary>
        public static int CounterSteps(DealerOffer d, int asking)
        {
            int gap = Math.Max(0, asking - d.Offer);
            return Math.Max(1, Math.Min(11, gap / DealerCatalogue.Round + 1));
        }

        public static int CounterAt(DealerOffer d, int asking, int step)
        {
            int steps = CounterSteps(d, asking);
            if (steps <= 1)
            {
                return asking;
            }
            int s = DealerCatalogue.Clamp(step, 0, steps - 1);
            return DealerCatalogue.Named(d.Offer + (int)((long)(asking - d.Offer) * s / (steps - 1)));
        }

        /// <summary>
        /// The player counters. True: the buyer takes it, and the offer becomes the counter. False: they do not; after
        /// <see cref="DealerCatalogue.CountersMost"/> counters they walk away (<paramref name="gone"/>). The answer to the
        /// same counter at the same point of the haggle is always the same.
        /// </summary>
        public static bool Counter(DealerOffer d, int counter, int seed, out bool gone)
        {
            var dice = new DealerDice(seed, d.Id, d.Counters, counter);
            d.Counters++;
            if (dice.Chance(AcceptChance(d, counter)))
            {
                d.Offer = counter;
                gone = false;
                return true;
            }
            gone = d.Counters >= DealerCatalogue.CountersMost;
            if (!gone)
            {
                // They come up a little towards the counter, never past their ceiling.
                d.Offer = DealerCatalogue.Named(Math.Min(d.Ceiling, d.Offer + (counter - d.Offer) / 3));
            }
            return false;
        }

        /// <summary>A test drive: the buyer takes the car out for an hour. Kilometres go on, and now and then a mark.</summary>
        public static void TestDrive(DealerOffer d, StockCar car, int now, int seed)
        {
            var dice = new DealerDice(seed, d.Id, 41);
            d.Stage = OfferStage.Driving;
            d.Drove = true;
            car.OutUntil = now + DealerCatalogue.TestDriveHours;
            car.Km += dice.Range(DealerCatalogue.TestDriveKmLeast, DealerCatalogue.TestDriveKmMost);
            if (dice.Chance(DealerCatalogue.TestDriveWearChance))
            {
                car.Condition = Math.Max(0, car.Condition - DealerCatalogue.TestDriveWear);
            }
            d.Expires = Math.Max(d.Expires, now + DealerCatalogue.TestDriveHours + DealerCatalogue.DealHours / 2);
        }

        // ---------------------------------------------------------------- an hour

        /// <summary>
        /// An hour of a dealership: visitors by the game's count for the building (with its event's points on the
        /// promotion, never past the game's top), those the staff look after, and the offers they make, added to the
        /// yard's offers (at most <see cref="DealerCatalogue.DealsMost"/> wait at once), the buyers as its event and
        /// reputation make them (<see cref="Mood"/>). Test drives that are over come back. The worths of the lot's cars are
        /// given in the cars' order. Offers that have lapsed are taken off, each a point off the reputation. The day's book
        /// counts it all.
        /// </summary>
        public static DealerHour Hour(DealerState s, DealerYard y, in YardHour h, int now, int day, IList<int> worths, int financeLeastAtPrices, IList<MarketModel> models, IList<string> colours, int level)
        {
            var r = new DealerHour();
            OfferMood mood = Mood(y, day);
            for (int i = y.Deals.Count - 1; i >= 0; i--)
            {
                DealerOffer d = y.Deals[i];
                StockCar car = y.Car(d.Car);
                if (car == null || now >= d.Expires)
                {
                    if (car != null)
                    {
                        Repute(y, -DealerCatalogue.RepLapsed);   // (a buyer left without an answer)
                    }
                    y.Deals.RemoveAt(i);
                    continue;
                }
                if (d.Stage == OfferStage.Driving && car.OutUntil <= now)
                {
                    d.Stage = OfferStage.Open;
                }
            }
            for (int c = 0; c < y.Cars.Count; c++)
            {
                if (y.Cars[c].OutUntil >= 0 && y.Cars[c].OutUntil <= now)
                {
                    y.Cars[c].OutUntil = -1;
                }
            }
            int carry = y.VisitorCarry;
            YardHour lifted = h;
            lifted.Promotion = Lifted(h.Promotion, mood.Promotion);
            r.Visitors = Visitors(lifted, now % 24, y.Cars.Count, ref carry);
            y.VisitorCarry = carry;
            r.Served = Served(r.Visitors, h.Staff);
            r.Lost = r.Visitors - r.Served;
            int featured = FeaturedCars(y, models, mood.Featured);
            var dice = new DealerDice(s.Seed, now, Hash(y.Place), 53);
            for (int v = 0; v < r.Served && y.Deals.Count < DealerCatalogue.DealsMost; v++)
            {
                int c = LookAt(y, now, day, worths, featured, ref dice);
                if (c < 0)
                {
                    break;
                }
                StockCar car = y.Cars[c];
                int lift = IsFeatured(featured, c) ? (int)((long)mood.Convert * DealerCatalogue.FeaturedOffer / 1000) : mood.Convert;
                if (!dice.Chance(OfferChance(car.Asking, worths[c], day - car.Bought, h.Skill, car.Condition, lift)))
                {
                    continue;
                }
                y.Deals.Add(Offer(s.Next(), car, now, h.Skill, y.Warranty, financeLeastAtPrices, models, colours, level, mood, ref dice));
                r.Offers++;
            }
            if (y.Today.Day != day)
            {
                y.Today.Clear(day);
            }
            y.Today.Visitors += r.Visitors;
            y.Today.Served += r.Served;
            y.Today.Lost += r.Lost;
            y.Today.Offers += r.Offers;
            return r;
        }

        /// <summary>A text's number that is the same in every run (a string's own hash is not).</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = h * 31 + text[i];
                }
                return h & 0x7FFFFFFF;
            }
        }

        /// <summary>
        /// The day is over for a yard: what stands at its close is read off (its lot, its arrears, its reputation:
        /// <see cref="Snapshot"/>), and its book goes first into the days kept, the oldest out.
        /// </summary>
        public static void CloseDay(DealerState s, DealerYard y, int day)
        {
            if (y.Today.Day < 0 || y.Today.Day >= day)
            {
                if (y.Today.Day != day)
                {
                    y.Today.Clear(day);
                }
                return;
            }
            Snapshot(s, y, y.Today.Day, y.Today);
            for (int i = y.Past.Length - 1; i > 0; i--)
            {
                y.Past[i].CopyFrom(y.Past[i - 1]);
            }
            y.Past[0].CopyFrom(y.Today);
            y.Today.Clear(day);
        }
    }
}
