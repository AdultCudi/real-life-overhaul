using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The rival tycoons on the stock market beside their holdings, by the player's own rules: units of the fund up to a
    /// share of their worth, blocks of other companies (another tycoon's holding among them) within what a holder may
    /// have, a block sold now and then, and a bank or a lab of their own put on the market. Every choice is drawn from the
    /// market's id, the tycoon and the day, so the same save plays the same and nothing is decided ahead. Their blocks move
    /// prices as a trade does (the engine's tape). Game-free; once a day for a tycoon whose day it is.
    /// </summary>
    public static class RivalMarket
    {
        private const int DrawSell = 1, DrawPick = 2, DrawHolding = 3, DrawArm = 4, DrawArmKind = 5;

        private static double Draw(MarketState s, int tycoon, int what, int day)
        {
            return MarketRandom.Unit(s.Id, MarketRandom.Extras, tycoon * 16 + what, day);
        }

        /// <summary>
        /// The next day of a tycoon's own, on their day of the month, that they have not yet looked at the market on and that has come: the first one after their
        /// last look (for a tycoon who has never looked, the latest one within a month), or 0 when none is due. Asked again until 0, so every day that passed
        /// between two looks of the director (a load, a long sleep, a game left idle) is played, none is skipped, and each look is drawn from the day it is for.
        /// </summary>
        public static int NextLook(int tycoon, int day, RivalBook b)
        {
            int every = ExtrasCatalogue.RivalEveryDays, offset = (tycoon * 11 + 5) % every;
            int from = b.LookDay >= 0 ? b.LookDay + 1 : Math.Max(1, day - every + 1);
            int due = from + ((offset - from % every) % every + every) % every;
            return due > 0 && due <= day ? due : 0;
        }

        /// <summary>The units of the fund a tycoon buys at a look: a step of their worth, until what they hold is their share of it. Nothing with no fund.</summary>
        public static long FundUnits(MarketState s, RivalBook b, long worth)
        {
            double nav = FundRules.Nav(s);
            if (nav < 1.0 || worth <= 0)
            {
                return 0L;
            }
            double held = b.Units * nav / 100.0, aim = worth * (double)ExtrasCatalogue.RivalFundBp / 10000.0;
            if (held >= aim)
            {
                return 0L;
            }
            double step = Math.Min(aim - held, worth * (double)ExtrasCatalogue.RivalFundStepBp / 10000.0);
            return Math.Max(0L, (long)Math.Floor(step * 100.0 / nav));
        }

        // A company a tycoon may take a block of: a city company or another tycoon's holding, traded long enough, with no bid for it, and never a player's own.
        private static bool Open(MarketState s, Stock k, int ownStock)
        {
            return k.Listed && k.Own == null && k.Id != ownStock && k.BidCents <= 0 && s.Day - k.ListedDay >= ExtrasCatalogue.BlockListedDays * 7 / 5;
        }

        /// <summary>The company a tycoon takes a block of today, 0 for none: one time in three another tycoon's holding if one is listed, else one of the city's.</summary>
        public static int Pick(MarketState s, int tycoon, int ownStock, int day, RivalBook b)
        {
            if (b.Stakes.Count >= ExtrasCatalogue.RivalStakesMost)
            {
                return 0;
            }
            bool holding = Draw(s, tycoon, DrawHolding, day) < 1.0 / 3.0;
            for (int pass = 0; pass < 2; pass++)
            {
                int count = 0;
                for (int i = 0; i < s.Stocks.Count; i++)
                {
                    Stock k = s.Stocks[i];
                    count += Open(s, k, ownStock) && (k.Def != null && k.Def.Holding) == holding && !Holds(b, k.Id) ? 1 : 0;
                }
                if (count > 0)
                {
                    int pick = (int)Math.Min(count - 1, Math.Floor(Draw(s, tycoon, DrawPick, day) * count));
                    for (int i = 0; i < s.Stocks.Count; i++)
                    {
                        Stock k = s.Stocks[i];
                        if (Open(s, k, ownStock) && (k.Def != null && k.Def.Holding) == holding && !Holds(b, k.Id) && pick-- == 0)
                        {
                            return k.Id;
                        }
                    }
                }
                holding = !holding;   // none of that kind: the other
            }
            return 0;
        }

        private static bool Holds(RivalBook b, int stock)
        {
            for (int i = 0; i < b.Stakes.Count; i++)
            {
                if (b.Stakes[i].Stock == stock)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The shares of a block so much money buys at the offer moved by its size, within what a tycoon may hold of one company and what any holder may.</summary>
        public static long BlockShares(MarketState s, Stock k, long money, out int cents)
        {
            cents = 0;
            if (k == null || money <= 0)
            {
                return 0L;
            }
            long most = Math.Min(MarketTrading.MostHeld(k), k.Shares * ExtrasCatalogue.RivalStakeMostBp / 10000L);
            long qty = Math.Min(most, (long)(money * 100.0 / Math.Max(1, k.MidCents)));
            if (qty <= 0)
            {
                return 0L;
            }
            cents = Bought(s, k, qty);
            while (qty > 1 && MarketTrading.Value(qty, cents, true) > money)
            {
                qty = Math.Max(1L, (long)(qty * 0.9));
                cents = Bought(s, k, qty);
            }
            return qty;
        }

        // What somebody who is not the player pays or is paid a share: the market's own depth rule on its size alone, nothing of the player's recent trading in it.
        private static int Bought(MarketState s, Stock k, long qty)
        {
            Quote q = MarketTrading.QuoteOf(s, k, MarketCatalogue.PhaseOpen + 1);
            double impact = MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(s, k) * Math.Sqrt(qty / Math.Max(1.0, k.Adv));
            return (int)Math.Min(int.MaxValue, Math.Ceiling(q.Ask * (1.0 + impact)));
        }

        /// <summary>What a tycoon is paid a share for a block sold: the bid moved against it by its size.</summary>
        public static int Sold(MarketState s, Stock k, long qty)
        {
            Quote q = MarketTrading.QuoteOf(s, k, MarketCatalogue.PhaseOpen + 1);
            double impact = MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(s, k) * Math.Sqrt(qty / Math.Max(1.0, k.Adv));
            return Math.Max(1, (int)Math.Floor(q.Bid * (1.0 - Math.Min(0.9, impact))));
        }

        /// <summary>Whether a tycoon sells a block today, and which: the oldest. A block of a company that left the market is gone already.</summary>
        public static bool Sells(MarketState s, int tycoon, int day, RivalBook b, out int at)
        {
            at = -1;
            if (b.Stakes.Count == 0 || Draw(s, tycoon, DrawSell, day) * 1000.0 >= ExtrasCatalogue.RivalSellChance)
            {
                return false;
            }
            at = 0;
            for (int i = 1; i < b.Stakes.Count; i++)
            {
                at = b.Stakes[i].Day < b.Stakes[at].Day ? i : at;
            }
            return true;
        }

        /// <summary>Blocks of companies that have left the market paid out at what each share was paid; returns the gain or loss of them all, in money.</summary>
        public static long Sweep(MarketState s, RivalBook b)
        {
            long gain = 0;
            for (int i = b.Stakes.Count - 1; i >= 0; i--)
            {
                Stock k = s.Stock(b.Stakes[i].Stock);
                if (k != null && k.Listed)
                {
                    continue;
                }
                long paid = k != null ? MarketTrading.Value(b.Stakes[i].Shares, k.GoneCents, false) : 0L;
                gain += paid - b.Stakes[i].Cost;
                b.Stakes.RemoveAt(i);
            }
            return gain;
        }

        /// <summary>Whether a tycoon whose holding is listed puts an arm of it on the market today, and which: 0 a bank, 1 a lab.</summary>
        public static bool ListsArm(MarketState s, int tycoon, int day, RivalBook b, bool listed, out int kind)
        {
            kind = Draw(s, tycoon, DrawArmKind, day) < 0.5 ? 0 : 1;
            return listed && b.ArmStock == 0 && Draw(s, tycoon, DrawArm, day) * 1000.0 < ExtrasCatalogue.RivalArmChance;
        }

        /// <summary>The arm's name: the holding's first word and what it is ("Karen Bank", "Luthor Labs").</summary>
        public static string ArmName(string holding, int kind)
        {
            string text = (holding ?? string.Empty).Trim();
            int space = text.IndexOf(' ');
            string first = space > 0 ? text.Substring(0, space) : text;
            return (first.Length > 0 ? first + " " : string.Empty) + ExtrasCatalogue.ArmWords[ExtrasCatalogue.Clamp(kind, 0, ExtrasCatalogue.ArmWords.Length - 1)];
        }

        /// <summary>The arm as a company of the market's own: worth a share of its tycoon's fortune, a bank among the banks or a lab among the software companies.</summary>
        public static StartupListing Arm(string name, int kind, long worth, int priceLevel)
        {
            double level = Math.Max(100, priceLevel) / 1000.0;
            double value = Math.Max(1000000.0, worth * (double)ExtrasCatalogue.RivalArmWorthBp / 10000.0);
            int cents = (int)Math.Max(100.0, Math.Round(2500.0 * level));
            return new StartupListing
            {
                Name = name, Sector = (int)(kind == 0 ? MarketSector.Banks : MarketSector.Software), RevenueM = (int)Math.Max(1.0, Math.Min(100000.0, value / level / 2000000.0)),
                Shares = Math.Max(100000L, (long)(value * 100.0 / cents)), PriceCents = cents
            };
        }

        /// <summary>What a tycoon holds on the market beside their holding, in money: units at what they hold, blocks at the last price.</summary>
        public static long Worth(MarketState s, RivalBook b)
        {
            double worth = b.Units * FundRules.Nav(s) / 100.0;
            for (int i = 0; i < b.Stakes.Count; i++)
            {
                Stock k = s.Stock(b.Stakes[i].Stock);
                worth += k != null && k.Listed ? b.Stakes[i].Shares * (double)k.Last / 100.0 : 0.0;
            }
            return (long)worth;
        }
    }
}
