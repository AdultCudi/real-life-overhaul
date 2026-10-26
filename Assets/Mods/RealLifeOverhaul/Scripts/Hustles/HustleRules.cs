using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>Something the hour brought that the player is told of.</summary>
    public enum HustleEventKind
    {
        Delivered,      // a thing came home
        Offer,          // a buyer wrote in
        Lapsed,         // a buyer's offer ran out
        Missed,         // nobody came to a booked visit
        HelperDone,     // a helper finished a visit
        Expired         // a thing waiting at a seller's door was not collected in time: it leaves, the money returns
    }

    /// <summary>One thing the hour brought, for the notice the director shows: whose, how much and how many stars.</summary>
    public struct HustleEvent
    {
        public HustleEventKind Kind;
        public int Id;          // the thing's or the visit's
        public int Amount;
        public int Stars;
        public string Who;
        public string Thing;    // an expired thing's, which the list no longer holds
        public bool Car;
    }

    /// <summary>
    /// The Marketplace's and freelance work's rules, game-free and checked offline (EconomyCheck HustleChecks): the day's
    /// listings and requests, worth, haggling, repairs and cleaning, buyers and their counters, ratings and helpers. Every
    /// draw is from the save's seed and the day or hour, so the same save gives the same city on every machine.
    /// </summary>
    public static class HustleRules
    {
        private const int SaltListings = 31, SaltGigs = 37, SaltBuyers = 41, SaltRepair = 43, SaltRating = 47, SaltCounter = 53;

        /// <summary>A seed from the save's own name for the playthrough: the same text, the same seed, on every machine.</summary>
        public static int SeedOf(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                int seed = (int)(h & 0x7fffffff);
                return seed == 0 ? 1 : seed;
            }
        }

        // ---------------------------------------------------------------- the morning

        /// <summary>
        /// The day's listings and requests in place of yesterday's: what was not bought goes, a visit nobody said yes to
        /// goes; booked visits, the player's things and the buyers' offers stay. <paramref name="homes"/> are the city's
        /// homes in a fixed order; <paramref name="cars"/> the cars the market deals in.
        /// </summary>
        public static void Morning(HustleState s, int day, IList<HustlePlace> homes, IList<MarketModel> cars, IList<string> colours, int level)
        {
            s.Listings.Clear();
            for (int i = s.Gigs.Count - 1; i >= 0; i--)
            {
                GigStage stage = s.Gigs[i].Stage;
                if (stage == GigStage.Offered || stage == GigStage.Done || stage == GigStage.Missed || stage == GigStage.Cancelled)
                {
                    s.Gigs.RemoveAt(i);
                }
            }
            s.Day = day;
            if (homes == null || homes.Count == 0)
            {
                return;
            }
            var dice = new DealerDice(s.Seed, day, SaltListings);
            for (int n = 0; n < HustleCatalogue.GoodsADay; n++)
            {
                s.Listings.Add(Goods(s.Next(), homes, level, ref dice));
            }
            for (int n = 0; cars != null && cars.Count > 0 && n < HustleCatalogue.CarsADay; n++)
            {
                s.Listings.Add(CarListing(s.Next(), homes, cars, colours, level, ref dice));
            }
            var gigs = new DealerDice(s.Seed, day, SaltGigs);
            int count = GigsToday(s.Rating, s.Ratings.Count);
            for (int n = 0; n < count && s.Gigs.Count < HustleCatalogue.GigsMost; n++)
            {
                s.Gigs.Add(Request(s.Next(), day, homes, level, s.Rating, ref gigs));
            }
        }

        private static Listing Goods(int id, IList<HustlePlace> homes, int level, ref DealerDice dice)
        {
            ThingDef def = HustleCatalogue.Things[dice.Range(0, HustleCatalogue.Things.Length - 1)];
            int condition = dice.Range(HustleCatalogue.ListingConditionLeast, HustleCatalogue.ListingConditionMost);
            int shown = condition;
            if (def.Kind == ThingKind.Electronics && dice.Chance(HustleCatalogue.OverstateChance))
            {
                shown = Math.Min(100, condition + dice.Range(HustleCatalogue.OverstateLeast, HustleCatalogue.OverstateMost));   // a fault they keep to themselves
            }
            int worth = GoodsWorth(def, condition, level);
            int asking = HustleCatalogue.Named(HustleCatalogue.Share(worth, dice.Range(HustleCatalogue.AskingLeast, HustleCatalogue.AskingMost)));
            HustlePlace home = homes[dice.Range(0, homes.Count - 1)];
            return new Listing
            {
                Id = id, Thing = def.Id, Condition = condition, Shown = shown, Asking = asking,
                Floor = HustleCatalogue.Named(HustleCatalogue.Share(asking, dice.Range(HustleCatalogue.FloorLeast, HustleCatalogue.FloorMost))),
                Seller = DealerRules.CustomerName(ref dice), Street = home.Street, Number = home.Number
            };
        }

        // A car from its owner, drawn as the showroom's used cars are, at the door of a home of the city.
        private static Listing CarListing(int id, IList<HustlePlace> homes, IList<MarketModel> cars, IList<string> colours, int level, ref DealerDice dice)
        {
            MarketModel model = cars[dice.Range(0, cars.Count - 1)];
            DealerListing used = DealerRules.Used(id, model, colours, level, ref dice);
            HustlePlace home = homes[dice.Range(0, homes.Count - 1)];
            return new Listing
            {
                Id = id, Thing = model.Type, Car = true, Colour = used.Colour, Km = used.Km, Price = (int)Math.Round(model.Price),
                Condition = HustleCatalogue.Clamp(used.Condition / 10, 1, 100), Shown = HustleCatalogue.Clamp(used.Shown / 10, 1, 100),
                Asking = used.Asking, Floor = used.Floor, Seller = DealerRules.CustomerName(ref dice), Street = home.Street, Number = home.Number
            };
        }

        /// <summary>How many visits clients ask for in a day, by the player's rating (thousandths of a star) and how many rated them.</summary>
        public static int GigsToday(int rating, int rated)
        {
            int n = HustleCatalogue.GigsADay;
            if (rated > 0 && rating >= HustleCatalogue.GoodRating)
            {
                n++;
            }
            else if (rated > 0 && rating < HustleCatalogue.PoorRating)
            {
                n--;
            }
            return n;
        }

        private static Gig Request(int id, int day, IList<HustlePlace> homes, int level, int rating, ref DealerDice dice)
        {
            int total = 0;
            for (int i = 0; i < HustleCatalogue.Gigs.Length; i++)
            {
                total += HustleCatalogue.Gigs[i].Weight;
            }
            int at = dice.Range(0, total - 1);
            GigDef def = HustleCatalogue.Gigs[0];
            for (int i = 0; i < HustleCatalogue.Gigs.Length; i++)
            {
                at -= HustleCatalogue.Gigs[i].Weight;
                if (at < 0)
                {
                    def = HustleCatalogue.Gigs[i];
                    break;
                }
            }
            int from = day * 24 + dice.Range(HustleCatalogue.GigFromLeast, HustleCatalogue.GigFromMost);
            int first = dice.Range(0, homes.Count - 1);
            int second = homes.Count > 1 ? (first + 1 + dice.Range(0, homes.Count - 2)) % homes.Count : first;   // never the same door twice
            return new Gig
            {
                Id = id, Kind = def.Kind, Client = DealerRules.CustomerName(ref dice), Street = homes[first].Street, Number = homes[first].Number,
                Street2 = def.TwoStops ? homes[second].Street : null, Number2 = def.TwoStops ? homes[second].Number : 0,
                Pay = GigPay(def, level, rating, dice.Range(HustleCatalogue.ClientLeast, HustleCatalogue.ClientMost)),
                From = from, Until = from + HustleCatalogue.GigWindow, Stage = GigStage.Offered, Day = day
            };
        }

        /// <summary>What a visit pays at these prices, for this rating, with the client's own figure (thousandths).</summary>
        public static int GigPay(GigDef def, int level, int rating, int client)
        {
            int pay = AtPrices(def.Pay, level);
            pay = HustleCatalogue.Share(pay, client);
            if (rating >= HustleCatalogue.GoodRating)
            {
                pay = HustleCatalogue.Share(pay, HustleCatalogue.GoodRatingPay);
            }
            return HustleCatalogue.Named(pay);
        }

        /// <summary>A sum at the start's prices at today's price level (1000 at the start).</summary>
        public static int AtPrices(int amount, int level)
        {
            return (int)Math.Round(amount * (double)Math.Max(1, level) / 1000.0, MidpointRounding.AwayFromZero);
        }

        // ---------------------------------------------------------------- worth

        /// <summary>A straight line through points (at, value), held at both ends.</summary>
        public static int Line(int[] at, int[] value, int x)
        {
            return DealerRules.Line(at, value, x);
        }

        /// <summary>What goods of this kind are worth in this condition, at these prices.</summary>
        public static int GoodsWorth(ThingDef def, int condition, int level)
        {
            if (def == null)
            {
                return 0;
            }
            int share = Line(HustleCatalogue.WorthAt, HustleCatalogue.WorthShare, HustleCatalogue.Clamp(condition, 0, 100));
            return Math.Max(1, HustleCatalogue.Share(AtPrices(def.NewPrice, level), share));
        }

        /// <summary>What a car is worth, as the showroom values one, less for dirt (percent, 0 clean).</summary>
        public static int CarWorth(int price, int level, int km, int condition, int dirt)
        {
            int worth = DealerRules.Worth(price, level, km, HustleCatalogue.Clamp(condition, 0, 100) * 10);
            return HustleCatalogue.Share(worth, 1000 - HustleCatalogue.CarDirtWorth * HustleCatalogue.Clamp(dirt, 0, 100) / 100);
        }

        public static int Worth(Owned o, int level, int dirt)
        {
            return o.Car ? CarWorth(o.Price, level, o.Km, o.Condition, dirt) : GoodsWorth(HustleCatalogue.Thing(o.Thing), o.Condition, level);
        }

        /// <summary>A listing's worth in the condition the seller gives it: what the player can see.</summary>
        public static int SaidWorth(Listing l, int level)
        {
            return l.Car ? CarWorth(l.Price, level, l.Km, l.Shown, 0) : GoodsWorth(HustleCatalogue.Thing(l.Thing), l.Shown, level);
        }

        // ---------------------------------------------------------------- buying

        public static int HaggleSteps => (1000 - HustleCatalogue.HaggleLeast) / HustleCatalogue.HaggleStep + 1;

        public static int HaggleAt(int asking, int step)
        {
            return HustleCatalogue.Named(HustleCatalogue.Share(asking, HustleCatalogue.HaggleLeast + HustleCatalogue.Clamp(step, 0, HaggleSteps - 1) * HustleCatalogue.HaggleStep));
        }

        /// <summary>The least a seller takes from a player of this negotiation skill (0 to 100).</summary>
        public static int FloorFor(Listing l, int skill)
        {
            int lever = HustleCatalogue.NegotiationLever * HustleCatalogue.Clamp(skill, 0, 100) / 100;
            return Math.Max(HustleCatalogue.Share(l.Asking, HustleCatalogue.HaggleLeast), l.Floor - HustleCatalogue.Share(l.Asking, lever));
        }

        /// <summary>An offer to a seller: taken at their least or their last counter, else countered (never above the last) until they go.</summary>
        public static HaggleAnswer Haggle(Listing l, int price, int skill, out int counter)
        {
            counter = 0;
            int floor = FloorFor(l, skill);
            if (price >= floor || (l.Countered > 0 && price >= l.Countered))
            {
                return HaggleAnswer.Taken;
            }
            l.Haggles++;
            if (l.Haggles >= HustleCatalogue.HagglesMost)
            {
                l.Gone = true;
                return HaggleAnswer.WalkedAway;
            }
            counter = HustleCatalogue.Named(floor + HustleCatalogue.Share(Math.Max(0, l.Asking - floor), HustleCatalogue.CounterShare));
            counter = l.Countered > 0 ? Math.Min(l.Countered, counter) : counter;
            l.Countered = counter;
            return HaggleAnswer.Countered;
        }

        /// <summary>What it costs to have goods brought home the next morning.</summary>
        public static int DeliveryFee(int price, int level)
        {
            return HustleCatalogue.Named(AtPrices(HustleCatalogue.DeliveryLeast, level) + HustleCatalogue.Share(price, HustleCatalogue.DeliveryShare));
        }

        /// <summary>A listing bought: it leaves the day's list and is the player's, to be collected or on its way home.</summary>
        public static Owned Buy(HustleState s, Listing l, int price, bool deliver, int fee, int day)
        {
            s.Listings.Remove(l);
            var o = new Owned
            {
                Id = s.Next(), Thing = l.Thing, Car = l.Car, Colour = l.Colour, Km = l.Km, Price = l.Price, Condition = l.Condition, Paid = price,
                Spent = deliver ? fee : 0, State = deliver && !l.Car ? OwnedState.Delivering : OwnedState.Waiting, Street = l.Street, Number = l.Number,
                Seller = l.Seller, Due = day + 1, Bought = day, Until = PickupRules.UntilFor(day)
            };
            s.Items.Add(o);
            return o;
        }

        // ---------------------------------------------------------------- fixing and cleaning

        /// <summary>The chance a repair by hand works (thousandths), by experience (0 to 100) and the condition it is in.</summary>
        public static int FixChance(int xp, int condition)
        {
            int c = HustleCatalogue.FixChanceBase + HustleCatalogue.FixChancePerXp * HustleCatalogue.Clamp(xp, 0, HustleCatalogue.XpMost)
                    - (condition < HustleCatalogue.WreckBelow ? HustleCatalogue.FixWreckCut : 0);
            return HustleCatalogue.Clamp(c, HustleCatalogue.FixChanceLeast, HustleCatalogue.FixChanceMost);
        }

        /// <summary>True if a repair by hand could still do something for it.</summary>
        public static bool CanFix(Owned o)
        {
            ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
            return def != null && def.FixMinutes > 0 && o.Condition < def.FixMost;
        }

        public static bool CanClean(Owned o)
        {
            ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
            return def != null && def.CleanGain > 0 && !o.Cleaned && o.Condition < HustleCatalogue.CleanMost;
        }

        public static bool CanShop(Owned o)
        {
            ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
            return def != null && def.ShopFix && o.Condition < HustleCatalogue.ShopCondition;
        }

        public static int PartsCost(ThingDef def, int level)
        {
            return HustleCatalogue.Named(HustleCatalogue.Share(AtPrices(def.NewPrice, level), HustleCatalogue.PartsShare));
        }

        /// <summary>
        /// A repair by hand, drawn from the save's seed, the thing and how many times it has been tried: the same attempt
        /// always comes out the same. Returns true if it worked; the condition and the experience are changed.
        /// </summary>
        public static bool FixByHand(HustleState s, Owned o, int attempt)
        {
            ThingDef def = HustleCatalogue.Thing(o.Thing);
            if (def == null)
            {
                return false;
            }
            var dice = new DealerDice(s.Seed, o.Id, attempt, SaltRepair);
            bool worked = dice.Chance(FixChance(s.RepairXp, o.Condition));
            if (worked)
            {
                o.Condition = Math.Max(o.Condition, def.FixMost - dice.Range(0, 8));
                s.RepairXp = Math.Min(HustleCatalogue.XpMost, s.RepairXp + HustleCatalogue.XpWorked);
            }
            else
            {
                o.Condition = Math.Max(1, o.Condition - dice.Range(1, HustleCatalogue.FixMiss));
                s.RepairXp = Math.Min(HustleCatalogue.XpMost, s.RepairXp + HustleCatalogue.XpFailed);
            }
            return worked;
        }

        /// <summary>What a repair shop charges for a thing in this condition.</summary>
        public static int ShopFee(ThingDef def, int condition, int level)
        {
            int share = HustleCatalogue.ShopShare * (100 - HustleCatalogue.Clamp(condition, 0, 100)) / 100;
            return HustleCatalogue.Named(Math.Max(AtPrices(HustleCatalogue.ShopLeast, level), HustleCatalogue.Share(AtPrices(def.NewPrice, level), share)));
        }

        public static void ShopFixed(Owned o)
        {
            o.Condition = Math.Max(o.Condition, HustleCatalogue.ShopCondition);
        }

        /// <summary>A clean, once: the kind's gain, never past <see cref="HustleCatalogue.CleanMost"/>.</summary>
        public static void Clean(Owned o)
        {
            ThingDef def = HustleCatalogue.Thing(o.Thing);
            if (def == null || o.Cleaned)
            {
                return;
            }
            o.Cleaned = true;
            o.Condition = Math.Max(o.Condition, Math.Min(HustleCatalogue.CleanMost, o.Condition + def.CleanGain));
        }

        // ---------------------------------------------------------------- selling

        public static int AskSteps => (HustleCatalogue.AskMostShare - HustleCatalogue.AskLeastShare) / HustleCatalogue.AskStepShare + 1;

        public static int AskAt(int worth, int step)
        {
            return HustleCatalogue.Named(HustleCatalogue.Share(worth, HustleCatalogue.AskLeastShare + HustleCatalogue.Clamp(step, 0, AskSteps - 1) * HustleCatalogue.AskStepShare));
        }

        /// <summary>The step nearest an asking price.</summary>
        public static int AskStepOf(int worth, int asking)
        {
            int best = 0, gap = int.MaxValue;
            for (int s = 0; s < AskSteps; s++)
            {
                int d = Math.Abs(AskAt(worth, s) - asking);
                if (d < gap)
                {
                    gap = d;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>The step the app starts on: the worth itself.</summary>
        public static int AskStepAtWorth => (1000 - HustleCatalogue.AskLeastShare) / HustleCatalogue.AskStepShare;

        public static void List(Owned o, int asking, int day)
        {
            o.State = OwnedState.Listed;
            o.Asking = Math.Max(1, asking);
            o.ListedDay = day;
            o.Plan = OwnedPlan.None;
        }

        /// <summary>The chance a listed thing draws a buyer this hour (thousandths): by its price against its worth and its condition.</summary>
        public static int OfferChance(int asking, int worth, int condition, bool car, int hour)
        {
            if (hour < HustleCatalogue.BuyersFrom || hour >= HustleCatalogue.BuyersUntil || worth <= 0)
            {
                return 0;
            }
            int price = Line(HustleCatalogue.PriceAt, HustleCatalogue.PriceDraw, (int)Math.Min(5000L, asking * 1000L / worth));
            int shape = Line(HustleCatalogue.ConditionAt, HustleCatalogue.ConditionDraw, HustleCatalogue.Clamp(condition, 0, 100));
            long c = (long)(car ? HustleCatalogue.OfferChanceCar : HustleCatalogue.OfferChanceGoods) * price / 1000 * shape / 1000;
            return (int)Math.Min(800, c);
        }

        /// <summary>A buyer's offer for a listed thing of this worth: under the asking price, with a ceiling they never pass.</summary>
        public static BuyerOffer MakeOffer(int id, Owned o, int worth, int expires, ref DealerDice dice)
        {
            int offer = HustleCatalogue.Named(HustleCatalogue.Share(o.Asking, dice.Range(HustleCatalogue.BuyerOfferLeast, HustleCatalogue.BuyerOfferMost)));
            int ceiling = Math.Min(o.Asking, HustleCatalogue.Named(HustleCatalogue.Share(worth, dice.Range(HustleCatalogue.CeilingLeast, HustleCatalogue.CeilingMost))));
            int first = Math.Min(offer, o.Asking);
            return new BuyerOffer { Id = id, Item = o.Id, Buyer = DealerRules.CustomerName(ref dice), Offer = first, Ceiling = Math.Max(first, ceiling), Expires = expires };
        }

        /// <summary>The chance a counter is taken: certain at their offer, falling to AcceptAtCeiling at their ceiling, none above it.</summary>
        public static int AcceptChance(BuyerOffer d, int counter)
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
            return 1000 - (int)((1000L - HustleCatalogue.AcceptAtCeiling) * (counter - d.Offer) / span);
        }

        /// <summary>The prices the player may counter with: from the buyer's offer to the asking price, in up to eleven steps.</summary>
        public static int CounterSteps(BuyerOffer d, int asking)
        {
            int gap = Math.Max(0, asking - d.Offer);
            return Math.Max(1, Math.Min(HustleCatalogue.CounterStepsMost, gap / 5 + 1));
        }

        public static int CounterAt(BuyerOffer d, int asking, int step)
        {
            int steps = CounterSteps(d, asking);
            if (steps <= 1)
            {
                return Math.Max(d.Offer, asking);
            }
            int s = HustleCatalogue.Clamp(step, 0, steps - 1);
            return HustleCatalogue.Named(d.Offer + (int)((long)(asking - d.Offer) * s / (steps - 1)));
        }

        /// <summary>The player counters: true if taken; after CountersMost refusals the buyer goes. The same counter always gets the same answer.</summary>
        public static bool Counter(BuyerOffer d, int counter, int seed, out bool gone)
        {
            var dice = new DealerDice(seed, d.Id, d.Counters, counter + SaltCounter);
            d.Counters++;
            if (dice.Chance(AcceptChance(d, counter)))
            {
                d.Offer = counter;
                gone = false;
                return true;
            }
            gone = d.Counters >= HustleCatalogue.CountersMost;
            if (!gone)
            {
                d.Offer = HustleCatalogue.Named(Math.Min(d.Ceiling, d.Offer + (counter - d.Offer) / 3));   // they come up a little, never past their ceiling
            }
            return false;
        }

        /// <summary>A thing sold: it leaves the player's things with its offers, and the sale is kept for the record. Returns the margin.</summary>
        public static int Sell(HustleState s, Owned o, string buyer, int price, int day)
        {
            s.Items.Remove(o);
            for (int i = s.Offers.Count - 1; i >= 0; i--)
            {
                if (s.Offers[i].Item == o.Id)
                {
                    s.Offers.RemoveAt(i);
                }
            }
            int cost = o.Paid + o.Spent;
            s.Sold(new Sale { Thing = o.Thing, Car = o.Car, Buyer = buyer, Price = price, Cost = cost, Day = day });
            if (!o.Test)
            {
                s.Profit += price - cost;
            }
            return price - cost;
        }

        // ---------------------------------------------------------------- the hour

        /// <summary>
        /// One game hour: deliveries come, buyers write in and their offers lapse, booked visits nobody came to are missed,
        /// helpers finish, things nobody collected from a seller in time go back and the money returns. <paramref name="worths"/> holds the worth of each of the player's things in the order of
        /// <see cref="HustleState.Items"/>. What happened goes into <paramref name="events"/>, for the player to be told.
        /// </summary>
        public static void Hour(HustleState s, int stamp, IList<int> worths, List<HustleEvent> events)
        {
            int day = stamp / 24, hour = stamp % 24;
            for (int i = 0; i < s.Items.Count; i++)
            {
                Owned o = s.Items[i];
                if (o.State == OwnedState.Delivering && (day > o.Due || (day == o.Due && hour >= HustleCatalogue.DeliveryHour)))
                {
                    o.State = OwnedState.Kept;
                    events.Add(new HustleEvent { Kind = HustleEventKind.Delivered, Id = o.Id });
                }
            }
            for (int i = s.Offers.Count - 1; i >= 0; i--)
            {
                BuyerOffer d = s.Offers[i];
                Owned o = s.Item(d.Item);
                if (o == null || o.State != OwnedState.Listed || stamp >= d.Expires)
                {
                    s.Offers.RemoveAt(i);
                    if (o != null && o.State == OwnedState.Listed)
                    {
                        events.Add(new HustleEvent { Kind = HustleEventKind.Lapsed, Id = o.Id, Who = d.Buyer });
                    }
                }
            }
            for (int i = 0; i < s.Items.Count && worths != null && i < worths.Count; i++)
            {
                Owned o = s.Items[i];
                if (o.State != OwnedState.Listed || s.OfferFor(o.Id) != null || s.Offers.Count >= HustleCatalogue.OffersMost)
                {
                    continue;
                }
                var dice = new DealerDice(s.Seed, stamp, o.Id, SaltBuyers);
                if (!dice.Chance(OfferChance(o.Asking, worths[i], o.Condition, o.Car, hour)))
                {
                    continue;
                }
                BuyerOffer d = MakeOffer(s.Next(), o, worths[i], stamp + HustleCatalogue.OfferHours, ref dice);
                s.Offers.Add(d);
                events.Add(new HustleEvent { Kind = HustleEventKind.Offer, Id = o.Id, Amount = d.Offer, Who = d.Buyer });
            }
            for (int i = 0; i < s.Gigs.Count; i++)
            {
                Gig g = s.Gigs[i];
                if ((g.Stage == GigStage.Booked && stamp >= g.Until) || (g.Stage == GigStage.Loaded && stamp >= g.Until + HustleCatalogue.GigWindow))
                {
                    g.Stage = GigStage.Missed;
                    g.Rating = HustleCatalogue.MissedRating;
                    Rated(s, g);
                    events.Add(new HustleEvent { Kind = HustleEventKind.Missed, Id = g.Id, Stars = g.Rating, Who = g.Client });
                }
                else if (g.Stage == GigStage.Helper && stamp >= g.HelperDone)
                {
                    int earned = HelperDone(s, g);
                    events.Add(new HustleEvent { Kind = HustleEventKind.HelperDone, Id = g.Id, Amount = earned, Stars = g.Rating, Who = g.Client });
                }
            }
            PickupRules.Lapse(s, day, events);   // last: it takes things out of the list the rules above read in order
        }

        // ---------------------------------------------------------------- freelance work

        public static bool Open(Gig g, int stamp)
        {
            return (g.Stage == GigStage.Booked && stamp >= g.From - HustleCatalogue.GigEarly && stamp < g.Until)
                   || (g.Stage == GigStage.Loaded && stamp < g.Until + HustleCatalogue.GigWindow);
        }

        public static void Book(Gig g)
        {
            if (g.Stage == GigStage.Offered)
            {
                g.Stage = GigStage.Booked;
            }
        }

        /// <summary>A booking called off: the client remembers it.</summary>
        public static void Cancel(HustleState s, Gig g)
        {
            if (g.Stage != GigStage.Booked && g.Stage != GigStage.Loaded)
            {
                return;
            }
            g.Stage = GigStage.Cancelled;
            g.Rating = HustleCatalogue.CancelRating;
            Rated(s, g);
        }

        public static int HelperFee(int pay)
        {
            return HustleCatalogue.Named(HustleCatalogue.Share(pay, HustleCatalogue.HelperShare));
        }

        /// <summary>Game hours a visit takes, whole: both stops for a move.</summary>
        public static int Hours(GigDef def)
        {
            int minutes = def.Minutes * (def.TwoStops ? 2 : 1);
            return (minutes + 59) / 60;
        }

        /// <summary>A helper sent in the player's place: they start when the client wants them and are done after the visit's hours.</summary>
        public static void SendHelper(HustleState s, Gig g, int stamp)
        {
            if (g.Stage != GigStage.Booked && g.Stage != GigStage.Offered)
            {
                return;
            }
            GigDef def = HustleCatalogue.Gig(g.Kind);
            g.Stage = GigStage.Helper;
            g.HelperFee = HelperFee(g.Pay);
            g.HelperDone = Math.Max(stamp, g.From) + Hours(def);
            var dice = new DealerDice(s.Seed, g.Id, SaltRating, 1);
            int roll = dice.Range(0, 99);
            g.HelperRating = roll < 25 ? 3 : roll < 75 ? 4 : 5;   // a helper of the city's: a steady hand, now and then a star better or worse
        }

        /// <summary>A helper's visit done: the client pays, the helper takes their share, the rating counts. Returns what the player keeps.</summary>
        public static int HelperDone(HustleState s, Gig g)
        {
            g.Stage = GigStage.Done;
            g.Rating = g.HelperRating;
            Rated(s, g);
            int kept = Math.Max(0, g.Pay - g.HelperFee) + (g.Rating >= 5 ? Tip(g.Pay) : 0);
            if (!g.Test)
            {
                s.Earned += kept;
            }
            return kept;
        }

        // A rating counts towards the player's, and a visit done towards the count; a test's leaves both as they were.
        private static void Rated(HustleState s, Gig g)
        {
            if (g.Test)
            {
                return;
            }
            s.Rate(g.Rating);
            s.JobsDone += g.Stage == GigStage.Done ? 1 : 0;
        }

        public static int Tip(int pay)
        {
            return HustleCatalogue.Named(HustleCatalogue.Share(pay, HustleCatalogue.TipShare));
        }

        /// <summary>
        /// The stars a client gives for a visit the player did: 3 for turning up, up to 1.5 more by the skill the work draws on
        /// (0 to 100), half a star either way by chance, one off for coming late and two for leaving before the end.
        /// </summary>
        public static int Stars(HustleState s, Gig g, int skill, bool whole)
        {
            var dice = new DealerDice(s.Seed, g.Id, SaltRating, 2);
            int r = 3000 + HustleCatalogue.Clamp(skill, 0, 100) * 15 + dice.Range(-500, 500) - (g.Late ? 1000 : 0) - (whole ? 0 : HustleCatalogue.StoppedEarlyCut);
            return HustleCatalogue.Clamp((r + 500) / 1000, 1, 5);
        }

        /// <summary>The work at an address is over (whole, or stopped after <paramref name="minutes"/>): what the client pays, and the rating once it is all done.</summary>
        public static int Worked(HustleState s, Gig g, int minutes, bool whole, int skill)
        {
            GigDef def = HustleCatalogue.Gig(g.Kind);
            if (def.TwoStops && g.Stage == GigStage.Booked && whole)
            {
                g.Stage = GigStage.Loaded;   // the first half: the client pays at the other end
                return 0;
            }
            int total = def.Minutes * (def.TwoStops ? 2 : 1);
            int done = def.TwoStops && g.Stage == GigStage.Loaded ? def.Minutes + minutes : minutes;
            int pay = whole ? g.Pay : HustleCatalogue.Share(g.Pay, 1000 * HustleCatalogue.Clamp(done, 0, total) / Math.Max(1, total));
            g.Stage = GigStage.Done;
            g.Rating = Stars(s, g, skill, whole);
            Rated(s, g);
            pay += whole && g.Rating >= 5 ? Tip(g.Pay) : 0;
            if (!g.Test)
            {
                s.Earned += pay;
            }
            return pay;
        }

        /// <summary>The address the next part of a visit is at.</summary>
        public static HustlePlace Where(Gig g)
        {
            return g.Stage == GigStage.Loaded && !string.IsNullOrEmpty(g.Street2) ? new HustlePlace(g.Street2, g.Number2) : new HustlePlace(g.Street, g.Number);
        }
    }
}
