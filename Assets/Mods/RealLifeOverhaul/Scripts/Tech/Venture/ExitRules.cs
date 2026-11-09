using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One who may buy a firm of the player's: a listed company of the market, or a startup of the city with the cash. Game-free.</summary>
    public struct ExitBuyer
    {
        public int Buyer;       // 0 a listed company, 1 a startup of the city
        public int Id;          // the stock's id, or the startup's
        public string Name;
        public long Cash;       // a startup's cash, cents at the start's prices; a listed company's is not tracked (the largest value)
    }

    /// <summary>What a sale of a firm of the player's came to: the pot, who is owed what, and what the wallet does. Cents at the start's prices.</summary>
    public struct SaleResult
    {
        public long Price;     // the buyer's money
        public long Cash;      // what the firm's account holds, which goes into the pot with the price
        public long Loan;      // a director's loan still out, which the player returns to the pot
        public long Bridge;    // the bridge loan, paid first
        public long Pot;       // what the waterfall shares: price, cash and loan, less the bridge
        public long Owed;      // to the investors in all: the bridge and their lines of the waterfall
        public long Paid;      // out of the price and the wallet
        public long Gap;       // owed and not paid: the investors' claim
        public long Taken;     // what was counted as taken at the sale
        public long Player;    // what the wallet gains by the sale: the price less what was paid to the investors (negative: their money comes out of the wallet)
        public bool Fraud;     // a gap with the clear pattern of misuse
    }

    /// <summary>The rules of exits: offers for a firm, a sale through the firm account, the listing path, the player's own shares sold later.
    /// Game-free; every roll is <see cref="TechRandom"/>'s.</summary>
    public static class ExitRules
    {
        public const int KindBuy = 0, KindHire = 1;
        public const int CounterRaised = 0, CounterHeld = 1, CounterWalked = 2;

        // ---------------------------------------------------------------- offers

        /// <summary>The chance, per mille a day, of an offer for a firm: more as it grows, scaled by the market's mood.</summary>
        public static int OfferChance(int growthBp, int mood)
        {
            int growth = Math.Max(0, growthBp) / ExitCatalogue.OfferGrowthBp;
            int chance = Math.Min(ExitCatalogue.OfferMost, ExitCatalogue.OfferBase + growth);
            return (int)((long)chance * Math.Max(0, mood) / 1000);
        }

        /// <summary>
        /// What a buyer pays: the firm's worth by its books (FirmsRules.Value, whose multiple moves with growth), scaled by the market's mood, and a premium
        /// that a listed company pays more of than a startup. Never under nothing.
        /// </summary>
        public static long OfferPrice(long value, int mood, int buyer, int roll)
        {
            if (value <= 0)
            {
                return 0;
            }
            long moody = value * (500 + Math.Max(0, mood) / 2) / 1000;
            int premium = buyer == 0 ? ExitCatalogue.PremiumListedLeast + Math.Abs(roll) % ExitCatalogue.PremiumListedSpan : Math.Abs(roll) % ExitCatalogue.PremiumRivalSpan;
            return moody * (1000 + premium) / 1000;
        }

        /// <summary>The offer a firm draws today, or null: seeded, more often when it grows, from a buyer that can pay. Never backdated: it is made on the day.</summary>
        public static ExitOffer Offer(ExitFile f, FirmExit e, int day, int mood, long mrr, long value, int growthBp, IList<ExitBuyer> buyers, bool onTheMarket = false)
        {
            // A firm whose shares trade on the market is bought at what the market makes of it (the caller gives that as the value);
            // one that listed and has no stock draws none, as before.
            if (mrr < ExitCatalogue.OfferMrrLeast || value <= 0 || e.Offers.Count >= ExitCatalogue.OffersMost
                || (e.Stage == ListStage.Listed && !onTheMarket) || e.OnTheWay || buyers == null || buyers.Count == 0)
            {
                return null;
            }
            int salt = StartupMoney.Hash(e.Place);
            if (TechRandom.Mille(f.Seed, day, salt, 71) >= OfferChance(growthBp, mood))
            {
                return null;
            }
            ExitBuyer b = PickBuyer(f.Seed, day, salt, buyers, 0, out int roll);
            if (b.Name == null)
            {
                return null;
            }
            long price = OfferPrice(value, mood, b.Buyer, roll);
            if (b.Buyer == 1 && price > b.Cash)
            {
                price = b.Cash;   // a startup offers what it has
                if (price <= 0)
                {
                    return null;
                }
            }
            return Made(f, e, KindBuy, b, price, day);
        }

        /// <summary>The soft landing: a failing firm's team taken for a small price. Null while the firm is not failing, or by the day's seeded roll.</summary>
        public static ExitOffer Hire(ExitFile f, FirmExit e, Cap c, long burnPerDay, int day, IList<ExitBuyer> buyers)
        {
            if (!Failing(c, burnPerDay) || e.Offers.Count >= ExitCatalogue.OffersMost || e.Stage == ListStage.Listed || buyers == null || buyers.Count == 0 || HasKind(e, KindHire))
            {
                return null;
            }
            int salt = StartupMoney.Hash(e.Place);
            if (TechRandom.Mille(f.Seed, day, salt, 75) >= ExitCatalogue.AcquihireChance)
            {
                return null;
            }
            long price = HirePrice(c);
            ExitBuyer b = PickBuyer(f.Seed, day, salt, buyers, price, out _);
            return b.Name == null ? null : Made(f, e, KindHire, b, price, day);
        }

        private static ExitBuyer PickBuyer(int seed, int day, int salt, IList<ExitBuyer> buyers, long needs, out int roll)
        {
            roll = TechRandom.Range(seed, day, salt, 76, 0, 100000);
            int start = roll % buyers.Count;
            for (int k = 0; k < buyers.Count; k++)
            {
                ExitBuyer b = buyers[(start + k) % buyers.Count];
                if (b.Buyer == 0 || b.Cash > needs)
                {
                    return b;
                }
            }
            return default(ExitBuyer);
        }

        private static bool HasKind(FirmExit e, int kind)
        {
            for (int i = 0; i < e.Offers.Count; i++)
            {
                if (e.Offers[i].Kind == kind)
                {
                    return true;
                }
            }
            return false;
        }

        private static ExitOffer Made(ExitFile f, FirmExit e, int kind, ExitBuyer b, long price, int day)
        {
            var o = new ExitOffer { Id = f.Next(), Kind = kind, Buyer = b.Buyer, BuyerId = b.Id, Name = b.Name, Price = price, Made = day, Lapse = day + ExitCatalogue.OfferDays };
            e.Offers.Add(o);
            return o;
        }

        /// <summary>Offers past their last day go. Returns how many.</summary>
        public static int Lapse(FirmExit e, int day)
        {
            int n = 0;
            for (int i = e.Offers.Count - 1; i >= 0; i--)
            {
                if (day > e.Offers[i].Lapse)
                {
                    e.Offers.RemoveAt(i);
                    n++;
                }
            }
            return n;
        }

        /// <summary>A firm whose account has little left at its burn: investors' money in it, burning, and days of it left under the limit.</summary>
        public static bool Failing(Cap c, long burnPerDay)
        {
            return c != null && StartupMoney.Has(c) && burnPerDay > 0 && StartupMoney.Balance(c) / burnPerDay < ExitCatalogue.FailingDays;
        }

        /// <summary>What a buyer pays for a failing firm's team: the share of what was put in that the city's own acqui-hires pay.</summary>
        public static long HirePrice(Cap c)
        {
            return c != null ? Math.Max(0L, c.Acc.Raised) * VentureCatalogue.AcquihireShare / 1000 : 0;
        }

        /// <summary>The chance a counter is met, thousandths: the player's Selling, Analysis and Relationships against how much more is asked.</summary>
        public static int CounterChance(int step, int selling, int analysis, int relationships)
        {
            int s = TalentCatalogue.Clamp(step, 0, ExitCatalogue.CounterSteps.Length - 1);
            int chance = ExitCatalogue.CounterBase + Skill(selling) * ExitCatalogue.CounterSelling + Skill(analysis) * ExitCatalogue.CounterAnalysis
                         + Skill(relationships) * ExitCatalogue.CounterRelationships - ExitCatalogue.CounterSteps[s] * ExitCatalogue.CounterStep;
            return TalentCatalogue.Clamp(chance, ExitCatalogue.CounterLeast, ExitCatalogue.CounterMost);
        }

        private static int Skill(int points) => TalentCatalogue.Clamp(points, 0, 100);

        /// <summary>A counter: the buyer pays more (the offer's price rises), holds at its price, or walks. One counter an offer.</summary>
        public static int Counter(ExitFile f, ExitOffer o, int step, int selling, int analysis, int relationships, int day)
        {
            if (o == null || o.Countered)
            {
                return CounterHeld;
            }
            o.Countered = true;
            int s = TalentCatalogue.Clamp(step, 0, ExitCatalogue.CounterSteps.Length - 1);
            if (TechRandom.Mille(f.Seed, o.Id, day, 72) < CounterChance(s, selling, analysis, relationships))
            {
                o.Price = o.Price * (1000 + ExitCatalogue.CounterSteps[s]) / 1000;
                return CounterRaised;
            }
            return TechRandom.Mille(f.Seed, o.Id, day, 73) < ExitCatalogue.HoldChance ? CounterHeld : CounterWalked;
        }

        // ---------------------------------------------------------------- a sale through the firm account

        /// <summary>Who is owed from a sale or a wind down: the venture firms and the public, which put money into the firm.</summary>
        public static bool Investor(Holder h) => StartupMoney.IsInvestor(h.Kind);

        /// <summary>
        /// A firm sold for <paramref name="price"/> while the player's wallet holds <paramref name="wallet"/>. The price joins what the firm's account holds
        /// and a director's loan still out; the bridge loan is paid first and the rest goes through the cap table's waterfall (VentureRules.Waterfall, whatever
        /// terms it knows). The investors' lines are paid before the player gets a cent, first out of the price, then out of the wallet; what neither
        /// covers is their claim. Money counted as taken is therefore never the player's: it sits in the account's cash the investors share, and is paid
        /// out of the player's proceeds first. A firm with no cap table has no one to pay. Game-free.
        /// </summary>
        public static SaleResult Settle(Cap c, long price, long wallet, int day)
        {
            var r = new SaleResult { Price = Math.Max(0L, price) };
            if (c == null)
            {
                r.Player = r.Price;
                return r;
            }
            bool funded = c.Acc.Open;   // an account that is open, wound or not: a firm being sold has its floor lifted by the sale itself
            r.Cash = funded ? Math.Max(0L, StartupMoney.Balance(c)) : 0;
            r.Loan = funded ? StartupMoney.LoanOwed(c, day) : 0;
            r.Taken = funded ? c.Acc.Taken : 0;
            long pot = r.Cash + r.Loan + r.Price;
            r.Bridge = Math.Min(Math.Max(0L, c.Bridge), pot);
            r.Pot = pot - r.Bridge;
            long[] lines = VentureRules.Waterfall(c.Holders, r.Pot);
            r.Owed = r.Bridge;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                r.Owed += Investor(c.Holders[i]) ? lines[i] : 0L;
            }
            r.Paid = Math.Min(r.Owed, Math.Max(0L, wallet) + r.Price);
            r.Gap = r.Owed - r.Paid;
            r.Player = r.Price - r.Paid;
            r.Fraud = r.Gap > 0 && StartupMoney.Clear(c, MoneyCatalogue.ClearPercent);
            return r;
        }

        // ---------------------------------------------------------------- the way to the market

        /// <summary>True while the firm may start the way to the market: takings of a Series B's, and not on the way or listed already.</summary>
        public static bool ListReady(long mrr, FirmExit e) => mrr >= ExitCatalogue.ListMrrLeast && !e.OnTheWay && e.Stage != ListStage.Listed;

        public static long Retainer(long fair) => Math.Max(ExitCatalogue.RetainerLeast, Math.Min(ExitCatalogue.RetainerMost, fair * ExitCatalogue.RetainerBp / 10000));

        /// <summary>How far a bank reaches, thousandths: the largest first reaches furthest, each a little off by its name.</summary>
        public static int BankReach(int index, string name)
        {
            int off = Math.Abs(TechRandom.Of(name ?? string.Empty) % 100) - 50;
            return TalentCatalogue.Clamp(900 - index * 250 + off, 100, 1000);
        }

        /// <summary>What a bank keeps of the money an offer raises, basis points: a wider reach costs more.</summary>
        public static int BankSpread(int reach) => ExitCatalogue.SpreadBase + TalentCatalogue.Clamp(reach, 0, 1000) * ExitCatalogue.SpreadReach / 1000;

        /// <summary>The bankers engaged: their retainer is paid, the firm's worth is taken from its books. False while another way is under way or the firm is too small.</summary>
        public static bool Engage(FirmExit e, long mrr, int bank, string name, int index, long fair, int day)
        {
            if (!ListReady(mrr, e) || fair <= 0)
            {
                return false;
            }
            e.Stage = ListStage.Bankers;
            e.Bank = bank;
            e.BankName = name ?? string.Empty;
            e.Reach = BankReach(index, name);
            e.Spread = BankSpread(e.Reach);
            e.Fee = Retainer(fair);
            e.Fair = fair;
            e.Demand = e.Choice = 0;
            e.Low = e.High = e.Price = 0;
            e.Gross = e.Net = 0;
            e.PriceDay = 0;
            e.FloatBp = ExitCatalogue.FloatBp;
            return true;
        }

        /// <summary>The roadshow's demand, thousandths: the market's mood, the player's Selling and Relationships, the bank's reach and the way it was held.</summary>
        public static int Demand(int mood, int selling, int relationships, int reach, int approach)
        {
            int a = TalentCatalogue.Clamp(approach, 0, ExitCatalogue.ApproachDemand.Length - 1);
            int d = ExitCatalogue.DemandBase + Math.Max(0, mood) * ExitCatalogue.DemandMood / 1000 + (Skill(selling) + Skill(relationships)) * ExitCatalogue.DemandSkill
                    + Math.Max(0, reach) * ExitCatalogue.DemandReach / 1000 + ExitCatalogue.ApproachDemand[a];
            return TalentCatalogue.Clamp(d, 0, 1000);
        }

        /// <summary>The share of fair value the book centres on, thousandths.</summary>
        public static int Centre(int demand) => ExitCatalogue.PriceLeast + TalentCatalogue.Clamp(demand, 0, 1000) * ExitCatalogue.PriceSpan / 1000;

        /// <summary>The roadshow held: the demand is set, the prospectus's range drawn round it, and the pricing day fixed. False unless the bankers are engaged.</summary>
        public static bool Hold(FirmExit e, int mood, int selling, int relationships, int approach, int day)
        {
            if (e.Stage != ListStage.Bankers)
            {
                return false;
            }
            int a = TalentCatalogue.Clamp(approach, 0, ExitCatalogue.PricingDays.Length - 1);
            e.Choice = a;
            e.Demand = Demand(mood, selling, relationships, e.Reach, a);
            int centre = Centre(e.Demand);
            e.Low = e.Fair * (centre - ExitCatalogue.RangeSpread) / 1000;
            e.High = e.Fair * (centre + ExitCatalogue.RangeSpread) / 1000;
            e.PriceDay = day + ExitCatalogue.PricingDays[a];
            e.Stage = ListStage.Roadshow;
            return true;
        }

        public enum Pricing { Waiting, Priced, CalledOff }

        /// <summary>
        /// The pricing day: on it the book is priced inside the range by the day's own noise, seeded; under the least the player takes it is called off
        /// (the retainer is not given back). Priced, the value before the money is kept in <see cref="FirmExit.Price"/>.
        /// </summary>
        public static Pricing Price(ExitFile f, FirmExit e, int day)
        {
            if (e.Stage != ListStage.Roadshow || day < e.PriceDay)
            {
                return Pricing.Waiting;
            }
            int centre = Centre(e.Demand);
            int noise = TechRandom.Range(f.Seed, StartupMoney.Hash(e.Place), e.PriceDay, 74, -ExitCatalogue.NoiseSpread, ExitCatalogue.NoiseSpread);
            int factor = TalentCatalogue.Clamp(centre + noise, centre - ExitCatalogue.RangeSpread, centre + ExitCatalogue.RangeSpread);
            e.Price = e.Fair * factor / 1000;
            if (e.Price < e.Fair * ExitCatalogue.WalkMille / 1000)
            {
                e.Stage = ListStage.CalledOff;
                e.Called = day;
                return Pricing.CalledOff;
            }
            return Pricing.Priced;
        }

        /// <summary>
        /// The firm lists at its price: new shares for the public are added to the cap table, the money after the bank's cut goes into the firm account as
        /// money raised (the wallet must keep it, as for any round), the player's shares are locked up for 180 days. Returns the cents that reach the firm.
        /// </summary>
        public static long List(Cap c, FirmExit e, int day)
        {
            long total = Math.Max(1L, c.Total);
            int f = TalentCatalogue.Clamp(e.FloatBp, 100, 9000);
            long shares = total * f / (10000 - f);
            e.Gross = e.Price * f / (10000 - f);
            e.Net = e.Gross - e.Gross * e.Spread / 10000;
            c.Holders.Add(new Holder { Kind = (int)HolderKind.Public, Shares = shares, Invested = e.Gross, Round = Math.Max(0, c.Stage + 1) });
            c.Post = e.Price + e.Gross;
            StartupMoney.Inflow(c, e.Net, day);
            e.Stage = ListStage.Listed;
            e.ListDay = day;
            e.LockTo = day + ExitCatalogue.LockUpDays;
            e.RoundDay = e.LockTo;
            e.Sold = 0;
            return e.Net;
        }

        /// <summary>The player's shares cannot be sold: the lock-up after a listing runs to its day.</summary>
        public static bool Locked(FirmExit e, int day) => e != null && e.Stage == ListStage.Listed && day < e.LockTo;

        /// <summary>A listed firm's worth now by its books, per share of those in issue, cents at the start's prices; 0 with nothing to say.</summary>
        public static long Quote(Cap c, long value)
        {
            long total = c != null ? c.Total : 0;
            return total > 0 && value > 0 ? value / total : 0;
        }

        /// <summary>A withdrawn way: the bankers are let go before the book is priced, the retainer stays paid.</summary>
        public static bool Withdraw(FirmExit e)
        {
            if (!e.OnTheWay)
            {
                return false;
            }
            e.Stage = ListStage.None;
            e.Demand = e.Choice = 0;
            e.PriceDay = 0;
            return true;
        }

        // ---------------------------------------------------------------- the player's own shares, sold later

        public static long PlayerShares(Cap c)
        {
            long mine = 0;
            for (int i = 0; c != null && i < c.Holders.Count; i++)
            {
                mine += c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i].Shares : 0;
            }
            return mine;
        }

        /// <summary>
        /// The day looks at a funded firm's last round: a round that closed since it last looked opens the window for the round's investors to buy from the
        /// player. A firm seen for the first time opens none (nothing is backdated); a listed firm's window opens when the lock-up ends and every quarter.
        /// </summary>
        public static void Round(Cap c, FirmExit e, int day)
        {
            if (c == null)
            {
                return;
            }
            if (e.StageSeen == -2)
            {
                e.StageSeen = c.Stage;
                return;
            }
            if (c.Stage > e.StageSeen && StartupMoney.Has(c))
            {
                e.RoundDay = day;
                e.Sold = 0;
            }
            e.StageSeen = c.Stage;
            if (e.Listed && day >= e.LockTo && (e.RoundDay < e.LockTo || day - e.RoundDay >= ExitCatalogue.QuarterDays))
            {
                e.RoundDay = day;
                e.Sold = 0;
            }
        }

        public static bool Window(FirmExit e, int day)
        {
            int days = e.Listed ? ExitCatalogue.QuarterDays : ExitCatalogue.SecondaryDays;
            return e.RoundDay >= 0 && day >= e.RoundDay && day - e.RoundDay <= days;
        }

        /// <summary>How many of the player's shares may be sold now: a share of the vested ones, once a round, never in a lock-up, never outside the window.</summary>
        public static long Room(Cap c, FirmExit e, int day, int year)
        {
            if (c == null || e == null || Locked(e, day) || !Window(e, day) || !StartupMoney.Has(c))
            {
                return 0;
            }
            int vested = VentureRules.Vested(c.Acc.FounderDay, day, year);
            long most = PlayerShares(c) * vested / 1000 * ExitCatalogue.SecondaryShare / 1000;
            return Math.Max(0L, most - e.Sold);
        }

        /// <summary>The discount under the round's price, thousandths: the player's Selling and Relationships take it down; a listed firm gives only the market's spread.</summary>
        public static int Discount(int selling, int relationships, bool listed)
        {
            if (listed)
            {
                return ExitCatalogue.QuoteSpread;
            }
            return TalentCatalogue.Clamp(ExitCatalogue.DiscountFirst - (Skill(selling) + Skill(relationships)) * 3 / 4, ExitCatalogue.DiscountLeast, ExitCatalogue.DiscountFirst);
        }

        /// <summary>What a number of shares fetch at the last round's price less the discount, cents.</summary>
        public static long SharePrice(Cap c, long shares, int discount)
        {
            long total = Math.Max(1L, c.Total);
            return (long)((double)Math.Max(0L, c.Post) * shares / total * (1000 - TalentCatalogue.Clamp(discount, 0, 1000)) / 1000);
        }

        /// <summary>
        /// Some of the player's shares sold to the round's investors (the lead), or to the public once listed. The money is the player's own: the firm's
        /// account is not touched, nothing is raised, so the floor stays as it was. Returns the cents paid; 0 when not allowed.
        /// </summary>
        public static long Sell(Cap c, FirmExit e, long shares, int discount, int lead, int day, int year)
        {
            if (shares <= 0 || shares > Room(c, e, day, year))
            {
                return 0;
            }
            Holder me = null;
            for (int i = 0; i < c.Holders.Count && me == null; i++)
            {
                me = c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i] : null;
            }
            if (me == null || shares > me.Shares)
            {
                return 0;
            }
            long paid = SharePrice(c, shares, discount);
            me.Shares -= shares;
            e.Sold += shares;
            int kind = e.Listed ? (int)HolderKind.Public : (int)HolderKind.Vc;
            int who = e.Listed ? 0 : TalentCatalogue.Clamp(lead, 0, VentureCatalogue.FirmCount - 1);
            for (int i = 0; i < c.Holders.Count; i++)
            {
                Holder h = c.Holders[i];
                if (h.Kind == kind && h.Who == who && h.Preference == 0 && h.Invested > 0)
                {
                    h.Shares += shares;
                    h.Invested += paid;
                    return paid;
                }
            }
            c.Holders.Add(new Holder { Kind = kind, Who = who, Shares = shares, Invested = paid, Round = Math.Max(0, c.Stage) });
            return paid;
        }
    }
}
