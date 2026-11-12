using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Somebody who could make an offer for a business, read from the game and the mod by the director when an offer is due.</summary>
    public struct SaleBidder
    {
        public int Kind;                 // BuyerKind
        public string Id;                // a rival's id of the game's, an employee's id, a listed company's id, an investor's name
        public string Name;
        public int Purse;                // the most they can put up; 0: no bound of their own
        public int Count;                // a rival's businesses, a company's id, the staff's number
    }

    /// <summary>A business of a rival of the game's, as a rival's sale looks at them: read once on a day a sale may come.</summary>
    public struct RivalHolding
    {
        public string Place;
        public string Business;
        public string Rival;             // the owner's id of the game's
        public string RivalName;
        public int Daily;                // its mean takings a day over the game's last days of them
        public int Count;                // how many businesses that rival runs
    }

    /// <summary>
    /// Who buys and sells, game-free and deterministic from the save's seed: the named buyers of an offer (rivals of the game's
    /// within what they can put up, the business's own staff, a listed company in its trade, the save's investors), and a
    /// rival's sale of one of its own businesses, offered to the player first and then to another rival.
    /// </summary>
    public static partial class SaleRules
    {
        /// <summary>What a rival of the game's can put up: so many weeks of its weekly takings, a round figure.</summary>
        public static int RivalPurse(float weeklyIncome)
        {
            double purse = Math.Max(0.0, (double)weeklyIncome) * ServicesCatalogue.RivalPurseWeeks;
            return ServicesCatalogue.Round10((long)Math.Min(int.MaxValue / 4, purse));
        }

        /// <summary>True if a rival with this purse bids for a business of this value.</summary>
        public static bool RivalCanBid(int purse, int value)
        {
            return value > 0 && purse >= ServicesCatalogue.Pct(value, ServicesCatalogue.RivalLeastPct);
        }

        /// <summary>
        /// The listed companies that could buy a business as a chain: those in its own trade (CompanyDef.Trade), or with none
        /// there those of its Ventures sector's market sector. Never a company still to come to the market, nor a tycoon's holding.
        /// </summary>
        public static void Chains(string businessType, int sector, List<SaleBidder> into)
        {
            int trade = MarketInputs.TradeOf(businessType);
            string tradeId = trade >= 0 ? MarketInputs.Trades[trade] : null;
            int from = into.Count;
            for (int pass = 0; pass < 2 && into.Count == from; pass++)
            {
                RealLifeOverhaul.Markets.MarketSector want = ServicesCatalogue.ChainSectors[ServicesCatalogue.Clamp(sector, 0, ServicesCatalogue.ChainSectors.Length - 1)];
                for (int i = 0; i < MarketCatalogue.All.Length; i++)
                {
                    CompanyDef d = MarketCatalogue.All[i];
                    bool fits = pass == 0 ? tradeId != null && string.Equals(d.Trade, tradeId, StringComparison.Ordinal) : d.Sector == want;
                    if (fits && !d.Pipeline && !d.Holding)
                    {
                        into.Add(new SaleBidder { Kind = (int)BuyerKind.Chain, Id = d.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), Name = d.Name, Count = d.Id });
                    }
                }
            }
        }

        /// <summary>The save's investors as buyers, by name; with none (Ventures off) the investment firms of its catalogue.</summary>
        public static void Investors(IList<string> names, List<SaleBidder> into)
        {
            int from = into.Count;
            for (int i = 0; names != null && i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    into.Add(new SaleBidder { Kind = (int)BuyerKind.Investor, Id = names[i], Name = names[i] });
                }
            }
            bool none = into.Count == from;
            for (int i = 0; none && i < VenturesCatalogue.FirmNames.Length; i++)
            {
                into.Add(new SaleBidder { Kind = (int)BuyerKind.Investor, Id = VenturesCatalogue.FirmNames[i], Name = VenturesCatalogue.FirmNames[i] });
            }
        }

        /// <summary>
        /// One bidder of a kind for a listing's turn, by index into <paramref name="bidders"/>; -1 for nobody. Those of the kind are
        /// taken in the order of their ids, so the list's own order changes nothing.
        /// </summary>
        public static int PickBidder(ServicesState s, SaleListing l, IList<SaleBidder> bidders, int kind, int turn)
        {
            int count = 0;
            for (int i = 0; i < bidders.Count; i++)
            {
                count += Eligible(s, l, bidders[i], kind) ? 1 : 0;
            }
            if (count == 0)
            {
                return -1;
            }
            int rank = ServicesCatalogue.Roll(s.Seed, ServicesCatalogue.Number(l.Place), turn, PurposeWho + kind * 100, count);
            return NthById(bidders, rank, b => Eligible(s, l, b, kind));
        }

        private static bool Eligible(ServicesState s, SaleListing l, SaleBidder b, int kind)
        {
            return b.Kind == kind && !string.IsNullOrEmpty(b.Id) && !string.IsNullOrEmpty(b.Name) && !s.Offering(l.Place, ServicesStore.Clean(b.Id))
                   && (kind != (int)BuyerKind.Rival || RivalCanBid(b.Purse, l.Value));
        }

        // The index of the rank-th bidder that passes, counted in the order of their ids (ordinal): the same on every machine.
        private static int NthById(IList<SaleBidder> bidders, int rank, Func<SaleBidder, bool> passes)
        {
            string last = null;
            for (int r = 0; r <= rank; r++)
            {
                int best = -1;
                for (int i = 0; i < bidders.Count; i++)
                {
                    if (passes(bidders[i]) && (last == null || string.CompareOrdinal(bidders[i].Id, last) > 0)
                        && (best < 0 || string.CompareOrdinal(bidders[i].Id, bidders[best].Id) < 0))
                    {
                        best = i;
                    }
                }
                if (best < 0)
                {
                    return -1;
                }
                if (r == rank)
                {
                    return best;
                }
                last = bidders[best].Id;
            }
            return -1;
        }

        // ---------------------------------------------------------------- rivals selling their own

        /// <summary>True if today a rival may put one of its businesses up: room for one, the gap since the last kept, and the day's draw.</summary>
        public static bool RivalSaleDue(ServicesState s, int day)
        {
            return s.RivalSales.Count < ServicesCatalogue.RivalSalesMost && day - s.RivalSaleDay >= ServicesCatalogue.RivalSaleGapDays
                   && ServicesCatalogue.Roll(s.Seed, day, 0, PurposeRivalSale, 100) < ServicesCatalogue.RivalSaleChancePct;
        }

        /// <summary>
        /// Which rival's business goes up for sale today, by index into <paramref name="holdings"/>; -1 for none. One of the city's
        /// weakest few by takings, of a rival that runs more than one, never one already for sale; ties go by place.
        /// </summary>
        public static int PickHolding(ServicesState s, IList<RivalHolding> holdings, int day)
        {
            var weakest = new int[ServicesCatalogue.RivalSaleWeakest];
            int found = 0;
            for (int i = 0; i < holdings.Count; i++)
            {
                RivalHolding h = holdings[i];
                if (h.Count < ServicesCatalogue.RivalSaleKeep || string.IsNullOrEmpty(h.Place) || string.IsNullOrEmpty(h.Rival) || s.RivalSelling(h.Place))
                {
                    continue;
                }
                // Insert into the sorted few (weakest first), dropping the strongest of them when full.
                int at = found;
                while (at > 0 && Weaker(h, holdings[weakest[at - 1]]))
                {
                    at--;
                }
                if (at >= weakest.Length)
                {
                    continue;
                }
                for (int k = Math.Min(found, weakest.Length - 1); k > at; k--)
                {
                    weakest[k] = weakest[k - 1];
                }
                weakest[at] = i;
                found = Math.Min(found + 1, weakest.Length);
            }
            return found == 0 ? -1 : weakest[ServicesCatalogue.Roll(s.Seed, day, found, PurposeHolding, found)];
        }

        private static bool Weaker(RivalHolding a, RivalHolding b)
        {
            return a.Daily != b.Daily ? a.Daily < b.Daily : string.CompareOrdinal(a.Place, b.Place) < 0;
        }

        /// <summary>
        /// The rival who takes a business up for sale if the player does not, by index into <paramref name="rivals"/>; -1 for
        /// none. Never its seller, and only one who can put the price up.
        /// </summary>
        public static int PickRivalBuyer(ServicesState s, IList<SaleBidder> rivals, string seller, int price, int day)
        {
            Func<SaleBidder, bool> passes = b => b.Kind == (int)BuyerKind.Rival && !string.IsNullOrEmpty(b.Id) && !string.Equals(b.Id, seller, StringComparison.Ordinal)
                                                 && price > 0 && b.Purse >= price;
            int count = 0;
            for (int i = 0; i < rivals.Count; i++)
            {
                count += passes(rivals[i]) ? 1 : 0;
            }
            return count == 0 ? -1 : NthById(rivals, ServicesCatalogue.Roll(s.Seed, day, ServicesCatalogue.Number(seller), PurposeRivalBuyer, count), passes);
        }

        /// <summary>The asking price of a rival's sale: a share of the game's own takeover price, a round figure.</summary>
        public static int RivalAsking(float takeover) => ServicesCatalogue.Round10((long)Math.Min(int.MaxValue / 4, Math.Max(0.0, (double)takeover) * ServicesCatalogue.RivalSaleAskPct / 100.0));

        /// <summary>Puts a rival's business up for sale today; null if there is no room or no price.</summary>
        public static RivalSale OpenRivalSale(ServicesState s, RivalHolding h, int price, string buyer, string buyerName, int day)
        {
            if (s.RivalSales.Count >= ServicesCatalogue.RivalSalesMost || price <= 0 || string.IsNullOrEmpty(h.Place) || s.RivalSelling(h.Place))
            {
                return null;
            }
            var sale = new RivalSale
            {
                Id = s.NextId++, Place = ServicesStore.Clean(h.Place), Business = ServicesStore.Clean(h.Business), Seller = ServicesStore.Clean(h.Rival),
                SellerName = ServicesStore.Clean(h.RivalName), Buyer = ServicesStore.Clean(buyer), BuyerName = ServicesStore.Clean(buyerName), Price = price,
                Weekly = (int)Math.Min(int.MaxValue / 4, Math.Max(0L, (long)h.Daily * 7)), Day = day, Deadline = day + ServicesCatalogue.RivalSaleOpenDays
            };
            s.RivalSales.Add(sale);
            s.RivalSaleDay = day;
            return sale;
        }

        /// <summary>True once a rival's sale is past the player's days: it goes to its buyer, or lapses.</summary>
        public static bool RivalSaleOver(RivalSale sale, int day) => sale != null && day > sale.Deadline;
    }
}
