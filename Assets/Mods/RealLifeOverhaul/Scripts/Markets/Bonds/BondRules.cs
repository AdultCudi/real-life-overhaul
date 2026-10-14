using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Bonds by rule: their yields, their payments, their price before their end and what a payment does. Game-free and
    /// without a chance of its own: everything is worked out from the market's city of the day (MarketEnv, the same on
    /// every machine) and the calendar, so the same bond pays the same on the same day wherever it is played.
    ///
    ///     the yield for a term   w x (the bank rate with half its next decision) + (1 - w) x the long run (the neutral
    ///                            rate and half of inflation over its target) + a premium for the term, w = e^(-term / 2 years);
    ///                            a city bond more, a deposit less (BondCatalogue)
    ///     a bond bought          at 100 for each 100 it repays, at the yield of its whole term, for good; no commission
    ///     its payments           on fixed days from the day it was bought, the last on the day it ends: its rate for the
    ///                            part of the year on what it repays, less the tax withheld as on a dividend (Listing's
    ///                            ListingVotes.DividendTax); of what is left, the share the player chose is put back into it
    ///                            at the day's price, the rest is paid into their cash
    ///     its end                what it repays comes back with the last payment
    ///     sold before its end    every payment still to come and what it repays, discounted at the yield for what is left
    ///                            of its term: so its price falls when rates rise and rises when they fall; less a small
    ///                            spread. A deposit is closed instead, for what it repays less a few months' interest.
    /// Over a year a bond pays its yield, near the bank rate: less than shares are expected to return, and with no fall
    /// of more than its price can move when rates move.
    /// </summary>
    public static class BondRules
    {
        // ---------------------------------------------------------------- the yields

        /// <summary>The yield a year, in bp, of a bond of a kind (at a bank's rank, for a deposit) for so many years, on the city of the day.</summary>
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

        /// <summary>The least and the most one purchase may be, in money, at the city's price level (tenths of a point, 1000 at the start).</summary>
        public static long Least(int priceLevel)
        {
            return Math.Max(1L, (long)BondCatalogue.LeastAtStart * Math.Max(100, priceLevel) / 1000L);
        }

        public static long Most(int priceLevel)
        {
            return Math.Max(1L, (long)BondCatalogue.MostAtStart * Math.Max(100, priceLevel) / 1000L);
        }

        /// <summary>Whether a bond can be bought now for so much money (nothing is changed).</summary>
        public static BondWhy CanBuy(BondBook book, BondOffer offer, long money, long cash, int priceLevel, bool open, bool held, bool on)
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
            if (book.Holdings.Count >= BondCatalogue.HoldingsMost)
            {
                return BondWhy.Full;
            }
            if (money < Least(priceLevel))
            {
                return BondWhy.TooLittle;
            }
            if (money > Most(priceLevel))
            {
                return BondWhy.TooMuch;
            }
            return money > cash ? BondWhy.NoCash : BondWhy.None;
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

        // ---------------------------------------------------------------- its payments

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

        /// <summary>One payment's interest, in cents, before tax: its rate on what it repays for a part of the year.</summary>
        public static long InterestCents(BondHolding h, BondDef d)
        {
            return Math.Max(0L, h.Face) * Math.Max(0, h.RateBp) / (10000L * Math.Max(1, d.PaysPerYear));
        }

        /// <summary>A payment of interest shared out: the tax withheld, then of what is left the share put back, and the rest paid out (cents).</summary>
        public static void Share(long interest, int taxStep, int reinvest, out long tax, out long back, out long paid)
        {
            interest = Math.Max(0L, interest);
            tax = Math.Min(interest, ListingVotes.DividendTax(interest, taxStep));
            long net = interest - tax;
            back = net * Math.Max(0, Math.Min(100, reinvest)) / 100L;
            paid = net - back;
        }

        // ---------------------------------------------------------------- its price

        /// <summary>
        /// What a bond is worth today for each 100 it repays: its payments still to come and what it repays, each
        /// discounted at today's yield for what is left of its term. At its rate, on the day it is bought, it is 100.
        /// </summary>
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

        /// <summary>What selling (or closing) it now brings, in cents: its price less the spread; a deposit what it repays less a few months' interest.</summary>
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

        /// <summary>Interest put back into a bond: what it buys of the same bond at the day's price (cents of face), and what is left over (cents).</summary>
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

        /// <summary>
        /// The interest a bond has paid so far, after tax, in cents: what was paid out to the player and what was put back
        /// into it (<see cref="BondHolding.Received"/> and <see cref="BondHolding.Reinvested"/>, the running totals each
        /// payment adds to and the save keeps). For "N paid on this bond so far" in the notice of each payment.
        /// </summary>
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
