using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Bonds by rule: their yields, payments, price before their end and what a payment does. Game-free and with no chance
    /// of its own: everything comes from the day's MarketEnv and the calendar, so every machine pays the same.
    /// </summary>
    public static class BondRules
    {
        /// <summary>A bond's yield a year in bp for a term in years; a deposit's at the bank of that rank.</summary>
        public static int YieldBp(BondKind kind, int bank, double years, in MarketEnv env)
        {
            years = Math.Max(0.0, years);
            double now = env.RateBp + env.RateNextBp * 0.5;
            double longRun = BondCatalogue.NeutralBp + Math.Max(-100, env.InflationBp - 200) * 0.5;
            double w = Math.Exp(-years / BondCatalogue.ShortYears);
            double y = w * now + (1.0 - w) * longRun + Math.Min(BondCatalogue.TermPremiumMostBp, BondCatalogue.TermPremiumBpYear * years);
            if (kind == BondKind.City)
            {
                y += BondCatalogue.CitySpreadBp;
            }
            else if (kind == BondKind.Deposit)
            {
                y += BankRules.Of(bank).RatePoints * BondCatalogue.DepositPerPointBp - BondCatalogue.DepositUnderBp;
            }
            return Math.Max(BondCatalogue.YieldLeastBp, (int)Math.Round(y));
        }

        /// <summary>A bond's whole term in days of a year of so many.</summary>
        public static int TermDays(BondDef d, int year)
        {
            return Math.Max(1, (d.TermQuarters * Math.Max(8, year) + 2) / 4);
        }

        /// <summary>The bonds on offer today: the Treasury's and the city's, and a deposit at each of the banks named (the largest first).</summary>
        public static void Offers(in MarketEnv env, int year, IList<string> banks, List<BondOffer> into)
        {
            into.Clear();
            for (int i = 0; i < BondCatalogue.All.Length; i++)
            {
                BondDef d = BondCatalogue.All[i];
                double years = TermDays(d, year) / (double)Math.Max(8, year);
                if (d.Kind != BondKind.Deposit)
                {
                    into.Add(new BondOffer { Def = d, Bank = -1, Issuer = string.Empty, YieldBp = YieldBp(d.Kind, -1, years, env) });
                    continue;
                }
                for (int b = 0; banks != null && b < banks.Count && b < BondCatalogue.BanksMost; b++)
                {
                    if (!string.IsNullOrEmpty(banks[b]))
                    {
                        into.Add(new BondOffer { Def = d, Bank = b, Issuer = banks[b], YieldBp = YieldBp(d.Kind, b, years, env) });
                    }
                }
            }
        }

        /// <summary>The least one purchase may be, in money, at the city's price level (1000 at the start).</summary>
        public static long Least(int priceLevel)
        {
            return Math.Max(1L, (long)BondCatalogue.LeastAtStart * Math.Max(100, priceLevel) / 1000L);
        }

        /// <summary>The flat most of one purchase, in money, at the city's price level.</summary>
        public static long Most(int priceLevel)
        {
            return Math.Max(1L, (long)BondCatalogue.MostAtStart * Math.Max(100, priceLevel) / 1000L);
        }

        /// <summary>
        /// The most one purchase may be for a player worth so much (money, the market's holdings included): the larger of
        /// the flat sum and a share of their worth, never over the cap.
        /// </summary>
        public static long Most(int priceLevel, long worth)
        {
            long flat = Most(priceLevel);
            long cap = Math.Max(flat, (long)BondCatalogue.MostCapAtStart * Math.Max(100, priceLevel) / 1000L);
            long share = worth > 0 ? worth / 100L * BondCatalogue.MostWorthPct : 0L;   // divided first: no sum a save holds overflows
            return Math.Max(flat, Math.Min(cap, share));
        }

        /// <summary>What <see cref="Most(int, long)"/> never passes, at the city's price level.</summary>
        public static long Cap(int priceLevel)
        {
            return Math.Max(Most(priceLevel), (long)BondCatalogue.MostCapAtStart * Math.Max(100, priceLevel) / 1000L);
        }

        /// <summary>Holdings allowed at once. One over it after switching the large option off keeps them and buys none.</summary>
        public static int HoldingsMost(bool large)
        {
            return large ? BondCatalogue.HoldingsMostLarge : BondCatalogue.HoldingsMost;
        }

        /// <summary>Whether a bond can be bought now for so much money (nothing is changed).</summary>
        public static BondWhy CanBuy(BondBook book, BondOffer offer, long money, long cash, int priceLevel, bool open, bool held, bool on)
        {
            return CanBuy(book, offer, money, cash, open, held, on, Least(priceLevel), Most(priceLevel), 1, BondCatalogue.HoldingsMost);
        }

        /// <summary>The same with the limits given; a ladder makes one holding for each of its terms.</summary>
        public static BondWhy CanBuy(BondBook book, BondOffer offer, long money, long cash, bool open, bool held, bool on, long least, long most, int holdings, int holdingsMost)
        {
            if (!on)
            {
                return BondWhy.Off;
            }
            if (held)
            {
                return BondWhy.Held;
            }
            if (!open)
            {
                return BondWhy.Closed;
            }
            if (offer.Def == null || (offer.Def.Kind == BondKind.Deposit && (offer.Bank < 0 || string.IsNullOrEmpty(offer.Issuer))))
            {
                return BondWhy.NoBank;
            }
            if (book.Holdings.Count + Math.Max(1, holdings) > holdingsMost)
            {
                return BondWhy.Full;
            }
            if (money < least)
            {
                return BondWhy.TooLittle;
            }
            if (money > most)
            {
                return BondWhy.TooMuch;
            }
            return money > cash ? BondWhy.NoCash : BondWhy.None;
        }

        /// <summary>The Treasury's bonds a ladder spreads a sum over, shortest first; how many.</summary>
        public static int LadderTerms(List<BondDef> into)
        {
            into.Clear();
            for (int i = 0; i < BondCatalogue.All.Length; i++)
            {
                if (BondCatalogue.All[i].Kind == BondKind.Treasury)
                {
                    into.Add(BondCatalogue.All[i]);
                }
            }
            into.Sort((a, b) => a.TermQuarters.CompareTo(b.TermQuarters));
            return into.Count;
        }

        /// <summary>A sum spread evenly over the pieces, the units left over one each to the first.</summary>
        public static void Ladder(long money, int pieces, long[] into)
        {
            int n = Math.Max(1, Math.Min(pieces, into.Length));
            long each = Math.Max(0L, money) / n, rest = Math.Max(0L, money) % n;
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = i < n ? each + (i < rest ? 1L : 0L) : 0L;
            }
        }

        /// <summary>
        /// A bond at its end bought again: same kind, term, bank and share put back, at the day's yield, for the face given
        /// in cents. Nothing changes hands, so no purchase limit and no commission.
        /// </summary>
        public static BondHolding Rolled(BondHolding h, BondDef d, int id, int yieldBp, long face, int day, int year)
        {
            return new BondHolding
            {
                Id = id, Def = d.Id, Bank = h.Bank, Issuer = h.Issuer ?? string.Empty, Face = Math.Max(0L, face), Cost = Math.Max(0L, face), RateBp = yieldBp, BoughtDay = day,
                EndDay = day + TermDays(d, year), Payments = d.Payments, Reinvest = ReinvestOf(h.Reinvest), Test = h.Test, Rollover = true
            };
        }

        /// <summary>A bond bought today for so much money, at its offer's yield; its payments from today.</summary>
        public static BondHolding Make(int id, BondOffer offer, long money, int day, int year, int reinvest, bool test)
        {
            BondDef d = offer.Def;
            return new BondHolding
            {
                Id = id, Def = d.Id, Bank = d.Kind == BondKind.Deposit ? offer.Bank : -1, Issuer = d.Kind == BondKind.Deposit ? offer.Issuer ?? string.Empty : string.Empty,
                Face = money * 100L, Cost = money * 100L, RateBp = offer.YieldBp, BoughtDay = day, EndDay = day + TermDays(d, year), Payments = d.Payments, Reinvest = ReinvestOf(reinvest),
                Test = test
            };
        }

        /// <summary>A share to put back held to one of the choices (BondCatalogue.Reinvest): the nearest at or under it.</summary>
        public static int ReinvestOf(int pct)
        {
            int chosen = BondCatalogue.Reinvest[0];
            for (int i = 0; i < BondCatalogue.Reinvest.Length; i++)
            {
                if (BondCatalogue.Reinvest[i] <= pct)
                {
                    chosen = BondCatalogue.Reinvest[i];
                }
            }
            return chosen;
        }

        /// <summary>The day of a bond's payment (1 to its count): evenly from the day it was bought, the last on its end.</summary>
        public static int PayDay(BondHolding h, int payment)
        {
            int count = Math.Max(1, h.Payments);
            long span = Math.Max(1, h.EndDay - h.BoughtDay);
            return h.BoughtDay + (int)((payment * span * 2L + count) / (2L * count));
        }

        /// <summary>The day of its next payment; 0 when every one is made.</summary>
        public static int NextPayDay(BondHolding h)
        {
            return h.Paid < h.Payments ? PayDay(h, h.Paid + 1) : 0;
        }

        /// <summary>One payment's interest in cents, before tax.</summary>
        public static long InterestCents(BondHolding h, BondDef d)
        {
            return Math.Max(0L, h.Face) * Math.Max(0, h.RateBp) / (10000L * Math.Max(1, d.PaysPerYear));
        }

        /// <summary>A payment of interest shared out in cents: the tax withheld, the share put back, the rest paid out.</summary>
        public static void Share(long interest, int taxStep, int reinvest, out long tax, out long back, out long paid)
        {
            interest = Math.Max(0L, interest);
            tax = Math.Min(interest, ListingVotes.DividendTax(interest, taxStep));
            long net = interest - tax;
            back = net * Math.Max(0, Math.Min(100, reinvest)) / 100L;
            paid = net - back;
        }

        /// <summary>A bond's worth today for each 100 it repays, its payments and face discounted at today's yield for what is left.</summary>
        public static double Price(BondHolding h, BondDef d, in MarketEnv env, int day, int year)
        {
            double y = Math.Max(8, year);
            double left = Math.Max(0, h.EndDay - day) / y;
            int m = Math.Max(1, d.PaysPerYear);
            double rate = YieldBp(d.Kind, h.Bank, left, env) / 10000.0 / m;
            double coupon = h.RateBp / 10000.0 / m * 100.0;
            double price = 0.0;
            for (int k = h.Paid + 1; k <= h.Payments; k++)
            {
                double t = Math.Max(0, PayDay(h, k) - day) / y;
                price += coupon * Math.Pow(1.0 + rate, -t * m);
            }
            return price + 100.0 * Math.Pow(1.0 + rate, -left * m);
        }

        /// <summary>What selling it now brings, in cents: its price less the spread; a deposit its face less a few months' interest.</summary>
        public static long SaleCents(BondHolding h, BondDef d, in MarketEnv env, int day, int year)
        {
            if (d.Kind == BondKind.Deposit)
            {
                long penalty = Math.Max(0L, h.Face) * Math.Max(0, h.RateBp) * BondCatalogue.DepositPenaltyMonths / (10000L * 12L);
                return Math.Max(0L, h.Face - penalty);
            }
            int spread = d.Kind == BondKind.City ? BondCatalogue.SellSpreadCityBp : BondCatalogue.SellSpreadTreasuryBp;
            double value = h.Face / 100.0 * Price(h, d, env, day, year) * (1.0 - spread / 10000.0);
            return value > 0.0 ? (long)Math.Floor(value) : 0L;
        }

        /// <summary>Cents of face that interest put back buys of the same bond at the day's price; what is left over.</summary>
        public static long Buys(long cents, double price, out long left)
        {
            left = Math.Max(0L, cents);
            if (cents <= 0 || !(price > 0.0))
            {
                return 0L;
            }
            long face = (long)Math.Floor(cents * 100.0 / price);
            long spent = Math.Min(cents, (long)Math.Ceiling(face * price / 100.0));
            left = cents - spent;
            return face;
        }

        /// <summary>Interest paid so far after tax, in cents, paid out and put back.</summary>
        public static long InterestSoFar(BondHolding h)
        {
            return h == null ? 0L : h.Received + h.Reinvested;
        }

        /// <summary>Whole money out of cents with what was carried before; the cents under a unit are carried on.</summary>
        public static long Money(long cents, ref long carry)
        {
            long all = Math.Max(0L, cents) + Math.Max(0L, carry);
            carry = all % 100L;
            return all / 100L;
        }
    }
}
