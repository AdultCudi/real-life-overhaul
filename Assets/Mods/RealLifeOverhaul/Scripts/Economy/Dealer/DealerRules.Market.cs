using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>A vehicle type the market can offer, as DealerGame reads it from the game: its name, its list price and its kind of car (the mod's car catalogue's, Driving/CarCatalogue).</summary>
    public struct MarketModel
    {
        public string Type;
        public float Price;
        public int Kind;

        public MarketModel(string type, float price, int kind = 0)
        {
            Type = type;
            Price = price;
            Kind = kind;
        }
    }

    /// <summary>What the player's offer to a private owner came to.</summary>
    public enum HaggleAnswer { Taken, Countered, WalkedAway }

    public static partial class DealerRules
    {
        // ---------------------------------------------------------------- the morning's market

        /// <summary>
        /// The day's market, drawn afresh each morning from the seed and the day: <see cref="DealerCatalogue.WholesaleADay"/>
        /// new cars, <see cref="DealerCatalogue.UsedADay"/> used ones offered by their owners. A sale of the trade auction is
        /// opened apart (<see cref="OpenSale"/>). The colours are drawn by the caller's list. What the day's market was,
        /// lots of an auction still running and bid on, stays: only the listings of the day before are replaced.
        /// </summary>
        public static void Morning(DealerState s, int day, IList<MarketModel> models, IList<string> colours, int level)
        {
            for (int i = s.Market.Count - 1; i >= 0; i--)
            {
                if (s.Market[i].Kind != ListingKind.Auction)
                {
                    s.Market.RemoveAt(i);
                }
            }
            s.MarketDay = day;
            if (models == null || models.Count == 0)
            {
                return;
            }
            var dice = new DealerDice(s.Seed, day, 11);
            for (int n = 0; n < DealerCatalogue.WholesaleADay; n++)
            {
                MarketModel m = models[dice.Range(0, models.Count - 1)];
                int km = dice.Range(5, DealerCatalogue.WholesaleKmMost);
                int worth = Worth(m.Price, level, 0, 1000);
                s.Market.Add(new DealerListing
                {
                    Id = s.Next(), Kind = ListingKind.Wholesale, Type = m.Type, Colour = Colour(colours, ref dice), Km = km, Condition = 1000, Shown = 1000,
                    Inspected = true, Asking = DealerCatalogue.Named(DealerCatalogue.Share(worth, dice.Range(DealerCatalogue.WholesaleLeast, DealerCatalogue.WholesaleMost)))
                });
            }
            for (int n = 0; n < DealerCatalogue.UsedADay; n++)
            {
                s.Market.Add(Used(s.Next(), models[dice.Range(0, models.Count - 1)], colours, level, ref dice));
            }
        }

        private static string Colour(IList<string> colours, ref DealerDice dice)
        {
            return colours != null && colours.Count > 0 ? colours[dice.Range(0, colours.Count - 1)] : null;
        }

        /// <summary>The condition a car of these kilometres usually has, before chance.</summary>
        public static int ConditionFor(int km)
        {
            return DealerCatalogue.Clamp(1000 - km / 320, DealerCatalogue.UsedConditionLeast, DealerCatalogue.UsedConditionMost);
        }

        /// <summary>The faults a used car hides: more with more kilometres and less condition.</summary>
        public static int DrawFaults(int km, int condition, ref DealerDice dice)
        {
            int faults = 0;
            int wear = 1000 + (km - 100000) / 200 + (700 - condition) * 2;   // thousandths of the catalogue's chance
            wear = DealerCatalogue.Clamp(wear, 250, 2500);
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                if (dice.Chance(DealerCatalogue.FaultChance[f] * wear / 1000))
                {
                    faults |= 1 << f;
                }
            }
            return faults;
        }

        /// <summary>A used car as its owner offers it: the truth, what they say of it, their asking price and their least.</summary>
        public static DealerListing Used(int id, MarketModel m, IList<string> colours, int level, ref DealerDice dice)
        {
            int band = dice.Pick(DealerCatalogue.UsedKmShare);
            int low = band == 0 ? 1000 : DealerCatalogue.UsedKmBand[band - 1];
            int km = dice.Range(low, DealerCatalogue.UsedKmBand[band]);
            int condition = DealerCatalogue.Clamp(ConditionFor(km) + dice.Range(-DealerCatalogue.UsedConditionSpread, DealerCatalogue.UsedConditionSpread),
                DealerCatalogue.UsedConditionLeast, DealerCatalogue.UsedConditionMost);
            int faults = DrawFaults(km, condition, ref dice);
            condition = Math.Max(DealerCatalogue.UsedConditionLeast - 100, condition - FaultCondition(faults));
            int worth = Worth(m.Price, level, km, condition);
            int shown = faults != 0 ? Math.Min(1000, condition + dice.Range(DealerCatalogue.OverstateMost / 3, DealerCatalogue.OverstateMost)) : condition;
            int asking = DealerCatalogue.Named(DealerCatalogue.Share(worth, dice.Range(DealerCatalogue.AskingLeast, DealerCatalogue.AskingMost)));
            return new DealerListing
            {
                Id = id, Kind = ListingKind.Used, Type = m.Type, Colour = Colour(colours, ref dice), Km = km, Condition = condition, Shown = shown, Faults = faults,
                Asking = asking, Floor = DealerCatalogue.Named(DealerCatalogue.Share(asking, dice.Range(DealerCatalogue.FloorLeast, DealerCatalogue.FloorMost)))
            };
        }

        /// <summary>What an inspection costs for a car of this worth, from the least and most at today's prices.</summary>
        public static int InspectionFee(int worth, int leastAtPrices, int mostAtPrices)
        {
            return DealerCatalogue.Clamp(DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.InspectionShare)), leastAtPrices, Math.Max(leastAtPrices, mostAtPrices));
        }

        /// <summary>An inspection of a used car: its faults are known, and the owner's least comes down by what was found.</summary>
        public static void Inspect(DealerListing l)
        {
            if (l.Inspected)
            {
                return;
            }
            l.Inspected = true;
            int found = FaultCount(l.Faults);
            if (found > 0 && l.Kind == ListingKind.Used)
            {
                int lever = Math.Min(4, found) * DealerCatalogue.InspectionLever;
                l.Floor = DealerCatalogue.Named(Math.Max(DealerCatalogue.Share(l.Asking, DealerCatalogue.HaggleLeast), l.Floor - DealerCatalogue.Share(l.Asking, lever)));
            }
        }

        /// <summary>The offers the player may make an owner, of their asking price: from <see cref="DealerCatalogue.HaggleLeast"/> to all of it.</summary>
        public static int HaggleSteps => (1000 - DealerCatalogue.HaggleLeast) / DealerCatalogue.HaggleStep + 1;

        public static int HaggleAt(int asking, int step)
        {
            return DealerCatalogue.Named(DealerCatalogue.Share(asking, DealerCatalogue.HaggleLeast + DealerCatalogue.Clamp(step, 0, HaggleSteps - 1) * DealerCatalogue.HaggleStep));
        }

        /// <summary>
        /// The player offers an owner a price. At or above their least it is taken; under it they counter with their least
        /// and a share of the gap above it, until the player has made <see cref="DealerCatalogue.HagglesMost"/> offers, when
        /// they sell elsewhere. A counter is never under one they made before.
        /// </summary>
        public static HaggleAnswer Haggle(DealerListing l, int price, out int counter)
        {
            counter = 0;
            if (price >= l.Floor || (l.Countered > 0 && price >= l.Countered))
            {
                return HaggleAnswer.Taken;
            }
            l.Haggles++;
            if (l.Haggles >= DealerCatalogue.HagglesMost)
            {
                l.Gone = true;
                return HaggleAnswer.WalkedAway;
            }
            int gap = Math.Max(0, l.Asking - l.Floor);
            counter = DealerCatalogue.Named(l.Floor + DealerCatalogue.Share(gap, DealerCatalogue.CounterShare));
            counter = l.Countered > 0 ? Math.Min(l.Countered, counter) : counter;
            l.Countered = counter;
            return HaggleAnswer.Countered;
        }

        // ---------------------------------------------------------------- the trade auction

        /// <summary>A step of the bidding on a car of this worth.</summary>
        public static int Increment(int worth)
        {
            return DealerCatalogue.Clamp(DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.IncrementShare)), DealerCatalogue.IncrementLeast, DealerCatalogue.IncrementMost);
        }

        /// <summary>The auction house's fee on a hammer price.</summary>
        public static int BuyersFee(int hammer, int leastAtPrices)
        {
            return Math.Max(leastAtPrices, DealerCatalogue.Named(DealerCatalogue.Share(hammer, DealerCatalogue.BuyersFee)));
        }

        /// <summary>True when a sale is to be opened now: none running, and within the hours sales are held.</summary>
        public static bool SaleDue(DealerState s, int hour)
        {
            if (hour < DealerCatalogue.AuctionFrom || hour > DealerCatalogue.AuctionTo)
            {
                return false;
            }
            for (int i = 0; i < s.Market.Count; i++)
            {
                if (s.Market[i].Kind == ListingKind.Auction && !s.Market[i].Gone)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A sale of <see cref="DealerCatalogue.AuctionLots"/> lots, each knocked down a few hours from now; the lots of the last sale are cleared.</summary>
        public static void OpenSale(DealerState s, int now, IList<MarketModel> models, IList<string> colours, int level)
        {
            for (int i = s.Market.Count - 1; i >= 0; i--)
            {
                if (s.Market[i].Kind == ListingKind.Auction)
                {
                    s.Market.RemoveAt(i);
                }
            }
            if (models == null || models.Count == 0)
            {
                return;
            }
            var dice = new DealerDice(s.Seed, now, 23);
            for (int n = 0; n < DealerCatalogue.AuctionLots; n++)
            {
                DealerListing l = Used(s.Next(), models[dice.Range(0, models.Count - 1)], colours, level, ref dice);
                int worth = WorthOf(l, models, level);
                l.Kind = ListingKind.Auction;
                l.Shown = DealerCatalogue.Clamp(l.Condition + dice.Range(-DealerCatalogue.AuctionShownSpread, DealerCatalogue.AuctionShownSpread), 300, 1000);
                l.Asking = DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.OpeningShare));
                l.Floor = 0;
                l.Ends = now + DealerCatalogue.LotHours[Math.Min(n, DealerCatalogue.LotHours.Length - 1)];
                l.Ceiling = DealerCatalogue.Named(DealerCatalogue.Share(worth, dice.Range(DealerCatalogue.RivalCeilingLeast, DealerCatalogue.RivalCeilingMost)));
                s.Market.Add(l);
            }
        }

        /// <summary>A listing's worth by the truth of it.</summary>
        public static int WorthOf(DealerListing l, IList<MarketModel> models, int level)
        {
            for (int i = 0; models != null && i < models.Count; i++)
            {
                if (models[i].Type == l.Type)
                {
                    return Worth(models[i].Price, level, l.Km, l.Condition);
                }
            }
            return 0;
        }

        /// <summary>The next bid the player may make on a lot: the opening one if nobody has bid, else a step over the current one.</summary>
        public static int NextBid(DealerListing l, int worth)
        {
            return l.Leader < 0 ? l.Asking : l.Asking + Increment(worth);
        }

        /// <summary>The player bids. False if they already lead or the lot is knocked down.</summary>
        public static bool Bid(DealerListing l, int worth, int now)
        {
            if (l.Gone || l.Leader == 0 || now >= l.Ends)
            {
                return false;
            }
            l.Asking = NextBid(l, worth);
            l.Leader = 0;
            l.Bids++;
            return true;
        }

        /// <summary>
        /// An hour of the room: on each lot still running another dealer may bid (more often while the player leads, as
        /// they answer a bid), one or two steps up and never past the room's ceiling. Returns how many lots another dealer
        /// took the lead on from the player.
        /// </summary>
        public static int RoomHour(DealerState s, int now, IList<MarketModel> models, int level)
        {
            int outbid = 0;
            for (int i = 0; i < s.Market.Count; i++)
            {
                DealerListing l = s.Market[i];
                if (l.Kind != ListingKind.Auction || l.Gone || now >= l.Ends)
                {
                    continue;
                }
                var dice = new DealerDice(s.Seed, l.Id, now, 31);
                if (!dice.Chance(l.Leader == 0 ? DealerCatalogue.RivalChanceLeading : DealerCatalogue.RivalChanceBehind))
                {
                    continue;
                }
                int worth = WorthOf(l, models, level);
                int step = Increment(worth) * dice.Range(1, 2);
                int bid = l.Leader < 0 ? l.Asking : l.Asking + step;
                if (bid > l.Ceiling)
                {
                    continue;
                }
                outbid += l.Leader == 0 ? 1 : 0;
                l.Asking = bid;
                l.Leader = 1 + dice.Range(0, DealerCatalogue.RivalNames.Length - 1);
                l.Bids++;
            }
            return outbid;
        }

        /// <summary>The lots knocked down by this hour, into the list given (none of them taken off the market: the caller settles each).</summary>
        public static void KnockedDown(DealerState s, int now, List<DealerListing> into)
        {
            into.Clear();
            for (int i = 0; i < s.Market.Count; i++)
            {
                DealerListing l = s.Market[i];
                if (l.Kind == ListingKind.Auction && !l.Gone && now >= l.Ends)
                {
                    into.Add(l);
                }
            }
        }
    }
}
