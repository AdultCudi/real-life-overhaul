using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The ways a firm's sale can be paid. Saved as a number.</summary>
    public static class TermKind
    {
        public const int Cash = 0, Shares = 1, Slice = 2, Earn = 3, Boss = 4, Count = 5;
    }

    /// <summary>Where a deal stands. Saved as a number. Everything after Open is over for good.</summary>
    public static class TermState
    {
        public const int Open = 0, Done = 1, Lapsed = 2, Resigned = 3, Dismissed = 4, Gone = 5;
    }

    /// <summary>What an earn-out's written target counts. Saved as a number.</summary>
    public static class TermMeasure
    {
        public const int Takings = 0, Customers = 1;
    }

    /// <summary>The figures of the terms of a sale. Cents at the start's prices; a month is 30 days and a year 360 (the way exits count their lock-up and their quarters).</summary>
    public static class SaleTermCatalogue
    {
        public const int MonthDays = FirmsCatalogue.MonthDays;   // a pay day, a dividend and a step of the firm's modelled growth come this often
        public const int YearDays = 360;                         // twelve months: the unit of a contract's term and of its vesting
        public const int DealsMost = 24;                         // deals kept in a save, the oldest finished one dropped first (the exit jobs keep forty leads; a sale is rarer)
        public const int OpenMost = 6;                           // deals open at once: a seventh sale waits until one has ended, so the daily pass stays a handful of lines

        // The share of the price paid in the buyer's shares, thousandths. A quarter and a half are the player's own examples; three quarters is the most a buyer's stock is trusted with.
        public static readonly int[] ShareSteps = { 250, 500, 750 };
        public const int ShareLockDays = ExitCatalogue.LockUpDays;   // the lock-up a firm's own listing gives its founder: the market knows the number already

        // Keep a slice: the size of the stake kept (thousandths of the firm), its life, what it is worth when the owner buys it out, and what it earns.
        public static readonly int[] SliceSteps = { 100, 200, 300 };   // a minority stake: never over a third, so the buyer always runs the firm
        public const int SliceTermDays = 3 * YearDays;                  // three years, the shortest term a contract here has
        public const int MinorityDiscount = 200;                        // thousandths off a minority stake at its buyout: a stake that cannot decide anything is worth less
        public const int SliceMargin = 250;                             // thousandths of a month's takings the firm keeps as profit: a software firm's, between the games' and the consultancies'
        public const int SliceCapPercent = 300;                         // the stake never pays more than three times what it cost in the price, dividends and buyout together

        // Earn-out: the part of the price held back, the written targets (thousandths of the takings or the customers at the sale), what each pays on the held-back sum, and when it is judged.
        public const int EarnShare = 300;                                          // thousandths of the price held back
        public static readonly int[] EarnTargets = { 1100, 1250, 1350 };           // the firm must reach this much of its figure at the sale: a tenth, a quarter, a third more
        public static readonly int[] EarnFurther = { 1100, 1800, 3000 };           // thousandths of the held-back sum paid if it does: the likelier the target the smaller the prize
        public const int EarnDays = YearDays;                                      // a year from the sale

        // The modelled growth of a firm the player no longer runs: one seeded draw a month, bounded so no firm doubles or dies in a season.
        public const int DriftLeast = -40, DriftMost = 80;                         // thousandths a month: a year's growth averages a quarter more, with room either side
        public const int IndexLeast = 300, IndexMost = 3000;                       // the index never leaves a third to three times the sale's figures

        // Stay as the boss: the terms of the contract. The share of the price still paid now is what is left of it.
        public static readonly int[] BossYears = { 3, 4 };                         // the player's own "three to four years"
        public const int BossGrant = 250;                                          // thousandths of the price paid in shares or a bonus that vest a year at a time
        public const int BossEarn = 150;                                           // thousandths of the price held back as the contract's earn-out
        public const int SalaryBp = 800;                                           // basis points of the month's takings at the sale paid a month
        public static long SalaryLeast => MoneyCatalogue.SalaryBand[0];            // the founder's salary band of the firm account bounds a boss's pay too
        public static long SalaryMost => MoneyCatalogue.SalaryBand[MoneyCatalogue.SalaryBand.Length - 1];
        public const int ReviewDays = 90;                                          // the owner reads the firm's takings this often
        public const int KeepMille = 700;                                          // takings under this much of the sale's month are a strike; two in a row dismiss the boss
        public const int StrikesMost = 2;
        public const int AbsentMost = 3;                                           // day passes a contract's business may be missing before the contract ends as gone
        public const int CoverMille = 300;                                         // thousandths of the sale's month of takings a buyer carries of one month's loss; the boss bears the rest
    }

    /// <summary>One sale's terms, as written when it was accepted, and how they stand. Cents at the start's prices unless said. Saved (<see cref="SaleTermStore"/>).</summary>
    public sealed class TermDeal
    {
        public int Id, Kind, State, Day, EndDay;
        public string Place = string.Empty, Firm = string.Empty, Buyer = string.Empty;
        public int BuyerId, BuyerKind;
        public long Price, Now, Deferred, Mrr0, Cust0;   // the whole price, what was paid at once, what was held back or kept, and the firm's figures at the sale
        public long Paid;                                // everything the deal has paid since the sale, cents
        public long Most;                                // the most the deal's written rules can ever pay after the sale: Paid never passes it
        public int Seen;                                 // the day its monthly step last ran
        public int Index = 1000, CIndex = 1000;         // the firm's modelled takings and customers against the sale's, thousandths (a firm the player no longer runs)
        // shares taken
        public int Stock;
        public long Shares;                              // shares paid (Shares) or the shares of each vesting year (Boss)
        public int ShareCents;                           // the price of one at the day, real cents
        public int LockTo;
        // slice kept
        public int Mille;
        // earn-out
        public int Measure, Target, EarnState, Due;     // EarnState: 0 open, 1 paid, 2 lapsed
        public long Further;
        // contract
        public long Salary, Grant;                       // a month's pay; the grant's value, cents
        public int Years, Vested, Strikes, Review;
        public bool Handed;                              // the business has left the player's hands
        public int Synced;                               // the day the firm's profit was last settled with the owner
        public int Absent;                               // day passes in a row that the contract's business was not found among the player's: a poor read of the firms is no ending

        public bool Open => State == TermState.Open;

        /// <summary>The deal still has something the daily pass must do: it is open, or a contract's business waits to be handed over.</summary>
        public bool Live => State == TermState.Open || (Kind == TermKind.Boss && !Handed);
    }

    /// <summary>What the terms keep in a save: the deals. A save from before has none.</summary>
    public sealed class TermFile
    {
        public int Seed, Next = 1;
        public readonly List<TermDeal> Deals = new List<TermDeal>(4);

        public TermDeal Of(int id)
        {
            for (int i = 0; i < Deals.Count; i++)
            {
                if (Deals[i].Id == id)
                {
                    return Deals[i];
                }
            }
            return null;
        }

        /// <summary>The contract that holds a business, or null.</summary>
        public TermDeal BossOf(string place)
        {
            for (int i = 0; i < Deals.Count; i++)
            {
                if (Deals[i].Kind == TermKind.Boss && Deals[i].Live && Deals[i].Place == place)
                {
                    return Deals[i];
                }
            }
            return null;
        }

        public int OpenCount()
        {
            int n = 0;
            for (int i = 0; i < Deals.Count; i++)
            {
                n += Deals[i].Live ? 1 : 0;
            }
            return n;
        }
    }

    /// <summary>What the player picked on the page: the way to be paid, its size, and for an earn-out its target and what it counts.</summary>
    public struct TermChoice
    {
        public int Kind, Step, Target, Measure;
    }

    /// <summary>What a choice comes to for one offer: all of it worked out before the confirm box, and what the sale will do if it is accepted. Cents at the start's prices.</summary>
    public struct TermPlan
    {
        public bool Ok;
        public string Why;          // the key of the text that says why not, when not Ok
        public int Kind;
        public long Price, Net, Now, Deferred;
        public long Shares;         // shares taken at once, or the shares of each of a contract's vesting years
        public int ShareCents, Stock;
        public int Mille;           // the stake kept, thousandths of the firm
        public long Cap;            // a stake's most
        public long Further;
        public int Target, Measure;
        public long Figure;         // the figure the target is a share of
        public long Salary, Grant;
        public int Years, LockDays;
        public long Most;           // the most the deal can ever pay after the sale
    }

    /// <summary>What the day sees of the world around a deal: the firm if it is still the player's (a contract), and the market's price of the buyer's stock.</summary>
    public struct TermWorld
    {
        public bool FirmHere;
        public long Takings, Customers;   // the firm's real figures, cents at the start's prices
        public bool StockLive;
        public int StockCents;            // real cents
    }

    /// <summary>What a deal's day did: each is something the director carries out, a payment or a notice.</summary>
    public enum TermEventKind
    {
        SliceDividend, SliceBought, EarnPaid, EarnLapsed, SharesFree, Salary, VestShares, VestCash, Strike, Dismissed, Ended, Remit, Gone
    }

    /// <summary>One thing a deal's monthly step did, for the director to carry out in the game.</summary>
    public struct TermEvent
    {
        public TermDeal Deal;
        public TermEventKind Kind;
        public long Cents;      // a payment, cents at the start's prices; a figure for a notice
        public long Shares;
        public int Count;       // the vesting year, or the strikes
    }

    /// <summary>
    /// The rules of the terms of a sale. Game-free. A plan is worked out from the offer, what the player has left of it after the investors, and the firm's figures; a deal is
    /// begun from a plan; and the day moves it by its monthly steps, each of which says what it pays as an event for the director to carry out. Nothing here allocates once built, and
    /// every draw is <see cref="TechRandom"/>'s, so every machine reaches the same deal.
    /// </summary>
    public static class SaleTermRules
    {
        public const string WhyNone = "reallife_terms_why_none", WhyShares = "reallife_terms_why_no_shares", WhySmall = "reallife_terms_why_small", WhyMeasure = "reallife_terms_why_no_customers",
            WhyOpen = "reallife_terms_why_many", WhyBoss = "reallife_terms_why_boss";

        /// <summary>What a choice is for an offer. <paramref name="net"/> is what the player has of the price after the investors, <paramref name="stockCents"/> the market's price of
        /// the buyer's stock in real cents (0 where the buyer is not a listed company or the market cannot say), and <paramref name="level"/> today's price level.</summary>
        public static TermPlan Plan(in TermChoice c, long price, long net, long mrr, long customers, int stock, int stockCents, int level, int open)
        {
            var p = new TermPlan { Kind = c.Kind, Price = Math.Max(0L, price), Net = Math.Max(0L, net), Stock = stock, ShareCents = stockCents };
            p.Now = p.Price;
            if (c.Kind <= TermKind.Cash || c.Kind >= TermKind.Count)
            {
                p.Kind = TermKind.Cash;
                p.Ok = p.Price > 0;
                p.Why = p.Ok ? null : WhyNone;
                return p;
            }
            if (p.Price <= 0 || p.Net <= 0)
            {
                p.Why = WhyNone;   // nothing of the price is the player's after the investors: nothing can be held back or kept
                return p;
            }
            if (open >= SaleTermCatalogue.OpenMost)
            {
                p.Why = WhyOpen;
                return p;
            }
            switch (c.Kind)
            {
                case TermKind.Shares:
                {
                    if (stock <= 0 || stockCents <= 0)
                    {
                        p.Why = WhyShares;
                        return p;
                    }
                    long want = Math.Min(p.Price * Pick(SaleTermCatalogue.ShareSteps, c.Step) / 1000, p.Net);
                    p.Shares = ExitGains.Level(want, level) / stockCents;
                    if (p.Shares <= 0)
                    {
                        p.Why = WhySmall;
                        return p;
                    }
                    p.Deferred = ExitGains.Unlevel(p.Shares * stockCents, level);
                    p.LockDays = SaleTermCatalogue.ShareLockDays;
                    p.Most = 0;
                    break;
                }
                case TermKind.Slice:
                {
                    p.Deferred = Math.Min(p.Price * Pick(SaleTermCatalogue.SliceSteps, c.Step) / 1000, p.Net);
                    p.Mille = (int)(p.Deferred * 1000 / p.Price);
                    p.Cap = p.Deferred * SaleTermCatalogue.SliceCapPercent / 100;
                    p.Most = p.Cap;
                    break;
                }
                case TermKind.Earn:
                {
                    if (!Target(ref p, c, mrr, customers))
                    {
                        return p;
                    }
                    p.Deferred = Math.Min(p.Price * SaleTermCatalogue.EarnShare / 1000, p.Net);
                    p.Further = p.Deferred * Pick(SaleTermCatalogue.EarnFurther, c.Target) / 1000;
                    p.Most = p.Further;
                    break;
                }
                default:
                {
                    if (!Target(ref p, c, mrr, customers))
                    {
                        return p;
                    }
                    p.Years = Pick(SaleTermCatalogue.BossYears, c.Step);
                    p.Grant = Math.Min(p.Price * SaleTermCatalogue.BossGrant / 1000, p.Net);
                    long earn = Math.Min(p.Price * SaleTermCatalogue.BossEarn / 1000, p.Net - p.Grant);
                    p.Deferred = p.Grant + earn;
                    p.Further = earn * Pick(SaleTermCatalogue.EarnFurther, c.Target) / 1000;
                    p.Salary = Math.Max(SaleTermCatalogue.SalaryLeast, Math.Min(SaleTermCatalogue.SalaryMost, mrr * SaleTermCatalogue.SalaryBp / 10000));
                    if (stock > 0 && stockCents > 0)
                    {
                        p.Shares = ExitGains.Level(p.Grant / p.Years, level) / stockCents;   // each vesting year's shares, counted now at the day's price
                    }
                    p.Most = p.Salary * (p.Years * SaleTermCatalogue.YearDays / SaleTermCatalogue.MonthDays) + p.Grant + p.Further;
                    break;
                }
            }
            p.Now = p.Price - p.Deferred;
            p.Ok = true;
            return p;
        }

        private static bool Target(ref TermPlan p, in TermChoice c, long mrr, long customers)
        {
            p.Measure = c.Measure == TermMeasure.Customers ? TermMeasure.Customers : TermMeasure.Takings;
            p.Figure = p.Measure == TermMeasure.Customers ? customers : mrr;
            if (p.Figure <= 0)
            {
                p.Why = p.Measure == TermMeasure.Customers ? WhyMeasure : WhyNone;
                return false;
            }
            p.Target = Pick(SaleTermCatalogue.EarnTargets, c.Target);
            return true;
        }

        private static int Pick(int[] steps, int at) => steps[TalentCatalogue.Clamp(at, 0, steps.Length - 1)];

        /// <summary>The deal a plan becomes on the day it is accepted: scheduled from that day, with the firm's figures at the sale kept as the written ones.</summary>
        public static TermDeal Begin(TermFile f, in TermPlan p, in TermChoice c, string place, string firm, string buyer, int buyerId, int buyerKind, int day, long mrr, long customers)
        {
            var d = new TermDeal
            {
                Id = f.Next++, Kind = p.Kind, State = TermState.Open, Day = day, Place = place ?? string.Empty, Firm = firm ?? string.Empty, Buyer = buyer ?? string.Empty,
                BuyerId = buyerId, BuyerKind = buyerKind, Price = p.Price, Now = p.Now, Deferred = p.Deferred, Mrr0 = mrr, Cust0 = customers, Most = p.Most, Seen = day,
                Stock = p.Stock, Shares = p.Shares, ShareCents = p.ShareCents, Mille = p.Mille, Measure = p.Measure, Target = p.Target, Further = p.Further, Synced = day
            };
            switch (p.Kind)
            {
                case TermKind.Shares:
                    d.LockTo = day + p.LockDays;
                    break;
                case TermKind.Slice:
                    d.EndDay = day + SaleTermCatalogue.SliceTermDays;
                    break;
                case TermKind.Earn:
                    d.Due = day + SaleTermCatalogue.EarnDays;
                    break;
                case TermKind.Boss:
                    d.Due = day + SaleTermCatalogue.EarnDays;
                    d.Years = p.Years;
                    d.EndDay = day + p.Years * SaleTermCatalogue.YearDays;
                    d.Salary = p.Salary;
                    d.Grant = p.Grant;
                    d.Review = day + SaleTermCatalogue.ReviewDays;
                    break;
            }
            f.Deals.Add(d);
            for (int i = 0; i < f.Deals.Count && f.Deals.Count > SaleTermCatalogue.DealsMost; i++)
            {
                if (!f.Deals[i].Live)
                {
                    f.Deals.RemoveAt(i--);   // the oldest finished one goes
                }
            }
            return d;
        }

        // ---------------------------------------------------------------- the firm the player no longer runs

        /// <summary>One month of the modelled growth: a seeded draw for the takings and another for the customers, thousandths, bounded.</summary>
        public static int Drift(int seed, int deal, int month, int which)
        {
            return TechRandom.Range(seed, deal, month, 81 + which, SaleTermCatalogue.DriftLeast, SaleTermCatalogue.DriftMost);
        }

        private static int Move(int index, int drift) => TalentCatalogue.Clamp((int)((long)index * (1000 + drift) / 1000), SaleTermCatalogue.IndexLeast, SaleTermCatalogue.IndexMost);

        /// <summary>The firm's takings a month and customers now: the modelled ones of a firm gone to its buyer, the real ones of a contract's firm.</summary>
        public static long Takings(TermDeal d, in TermWorld w) => d.Kind == TermKind.Boss && w.FirmHere ? w.Takings : d.Mrr0 * d.Index / 1000;

        public static long Customers(TermDeal d, in TermWorld w) => d.Kind == TermKind.Boss && w.FirmHere ? w.Customers : d.Cust0 * d.CIndex / 1000;

        /// <summary>The figure an earn-out is judged on, and the figure it must reach.</summary>
        public static long Progress(TermDeal d, in TermWorld w) => d.Measure == TermMeasure.Customers ? Customers(d, w) : Takings(d, w);

        public static long Aim(TermDeal d) => (d.Measure == TermMeasure.Customers ? d.Cust0 : d.Mrr0) * d.Target / 1000;

        /// <summary>What a slice pays for a month: its share of the profit the firm's takings make.</summary>
        public static long SliceMonth(TermDeal d, in TermWorld w) => Takings(d, w) * SaleTermCatalogue.SliceMargin / 1000 * d.Mille / 1000;

        /// <summary>What the owner pays for the stake at its end: its share of what the price was, moved with the firm and less the minority discount.</summary>
        public static long SliceBuyout(TermDeal d) => d.Price * d.Mille / 1000 * d.Index / 1000 * (1000 - SaleTermCatalogue.MinorityDiscount) / 1000;

        /// <summary>What a stake is worth now, for the list of holdings: the buyout it would get today.</summary>
        public static long SliceWorth(TermDeal d) => d.State == TermState.Open ? Math.Min(SliceBuyout(d), Math.Max(0L, d.Most - d.Paid)) : 0L;

        // ---------------------------------------------------------------- the day

        /// <summary>
        /// A deal's monthly steps up to <paramref name="day"/>: at most one a month, each dated by its own month, so a long sleep pays every month it passed and none twice. Events
        /// are put in <paramref name="into"/>; the director carries them out. Nothing is paid for a deal not open, bar a contract's business still to be handed over. Cheap: a deal
        /// with no month to run does one comparison.
        /// </summary>
        public static void Day(TermFile f, TermDeal d, int day, in TermWorld w, List<TermEvent> into)
        {
            if (d == null || !d.Live)
            {
                return;
            }
            if (d.Kind == TermKind.Shares)
            {
                if (day >= d.LockTo)
                {
                    d.State = TermState.Done;
                    into.Add(new TermEvent { Deal = d, Kind = TermEventKind.SharesFree, Shares = d.Shares });
                }
                return;
            }
            for (int steps = 0; d.Live && d.Seen + SaleTermCatalogue.MonthDays <= day && steps < 240; steps++)
            {
                d.Seen += SaleTermCatalogue.MonthDays;
                int month = (d.Seen - d.Day) / SaleTermCatalogue.MonthDays;
                if (d.Kind == TermKind.Boss)
                {
                    Boss(d, d.Seen, w, into);
                    continue;
                }
                d.Index = Move(d.Index, Drift(f.Seed, d.Id, month, 0));
                d.CIndex = Move(d.CIndex, Drift(f.Seed, d.Id, month, 1));
                if (d.Kind == TermKind.Slice)
                {
                    Slice(d, d.Seen, w, into);
                }
                else if (d.Kind == TermKind.Earn && d.EarnState == 0 && d.Seen >= d.Due)
                {
                    Earn(d, w, into);
                    d.State = TermState.Done;
                }
            }
        }

        private static void Slice(TermDeal d, int on, in TermWorld w, List<TermEvent> into)
        {
            long room = Math.Max(0L, d.Most - d.Paid);
            if (on >= d.EndDay)
            {
                long buy = Math.Min(room, SliceBuyout(d));
                d.Paid += buy;
                d.State = TermState.Done;
                into.Add(new TermEvent { Deal = d, Kind = TermEventKind.SliceBought, Cents = buy });
                return;
            }
            long pay = Math.Min(room, SliceMonth(d, w));
            if (pay > 0)
            {
                d.Paid += pay;
                into.Add(new TermEvent { Deal = d, Kind = TermEventKind.SliceDividend, Cents = pay });
            }
        }

        private static void Earn(TermDeal d, in TermWorld w, List<TermEvent> into)
        {
            bool reached = Progress(d, w) >= Aim(d);
            d.EarnState = reached ? 1 : 2;
            long pay = reached ? Math.Min(d.Further, Math.Max(0L, d.Most - d.Paid)) : 0L;
            d.Paid += pay;
            into.Add(new TermEvent { Deal = d, Kind = reached ? TermEventKind.EarnPaid : TermEventKind.EarnLapsed, Cents = pay });
        }

        private static void Boss(TermDeal d, int on, in TermWorld w, List<TermEvent> into)
        {
            if (!w.FirmHere)
            {
                End(d, TermState.Gone, TermEventKind.Gone, into);   // the business is no longer the player's: closed or sold by other means
                d.Handed = true;
                return;
            }
            if (d.State == TermState.Open)
            {
                if (on <= d.EndDay)
                {
                    d.Paid += d.Salary;
                    into.Add(new TermEvent { Deal = d, Kind = TermEventKind.Salary, Cents = d.Salary });
                }
                while (d.Vested < d.Years && on >= d.Day + (d.Vested + 1) * SaleTermCatalogue.YearDays)
                {
                    d.Vested++;
                    long each = d.Grant / Math.Max(1, d.Years);
                    d.Paid += d.Shares > 0 && w.StockLive && w.StockCents > 0 ? 0L : each;   // shares are not money paid by the deal; a bonus is
                    into.Add(new TermEvent
                    {
                        Deal = d, Kind = d.Shares > 0 && w.StockLive && w.StockCents > 0 ? TermEventKind.VestShares : TermEventKind.VestCash, Cents = each, Shares = d.Shares, Count = d.Vested
                    });
                }
                if (d.EarnState == 0 && on >= d.Due)
                {
                    Earn(d, w, into);
                }
                if (on >= d.Review)
                {
                    d.Review += SaleTermCatalogue.ReviewDays;
                    if (w.Takings * 1000 >= d.Mrr0 * SaleTermCatalogue.KeepMille)
                    {
                        d.Strikes = 0;
                    }
                    else if (++d.Strikes >= SaleTermCatalogue.StrikesMost)
                    {
                        End(d, TermState.Dismissed, TermEventKind.Dismissed, into);
                        into.Add(new TermEvent { Deal = d, Kind = TermEventKind.Remit });   // the month's profit is the owner's all the same
                        return;
                    }
                    else
                    {
                        into.Add(new TermEvent { Deal = d, Kind = TermEventKind.Strike, Cents = w.Takings, Count = d.Strikes });
                    }
                }
                if (on >= d.EndDay)
                {
                    End(d, TermState.Done, TermEventKind.Ended, into);
                }
            }
            into.Add(new TermEvent { Deal = d, Kind = TermEventKind.Remit });
        }

        /// <summary>
        /// The contract ends otherwise than at its term: the pay stops, the years not yet vested and an earn-out not yet judged are lost, and the business is to be handed over.
        /// What was already paid stays the player's.
        /// </summary>
        public static void End(TermDeal d, int state, TermEventKind why, List<TermEvent> into)
        {
            if (!d.Open)
            {
                return;
            }
            d.State = state;
            d.EndDay = Math.Min(d.EndDay, d.Seen);
            if (d.EarnState == 0)
            {
                d.EarnState = 2;
            }
            into.Add(new TermEvent { Deal = d, Kind = why });
        }

        /// <summary>The player resigns a contract: the same ending as a dismissal, by the player's own word.</summary>
        public static bool Resign(TermDeal d, List<TermEvent> into)
        {
            if (d == null || d.Kind != TermKind.Boss || !d.Open)
            {
                return false;
            }
            End(d, TermState.Resigned, TermEventKind.Ended, into);
            return true;
        }

        /// <summary>The most a finished or running deal may ever have paid: its written rules' most. Used by the checks and the director's log.</summary>
        public static bool WithinRules(TermDeal d) => d.Paid <= d.Most;

        /// <summary>The days of lock-up left on shares taken at a day; 0 when they are free.</summary>
        public static int LockLeft(TermDeal d, int day) => d != null && d.Kind == TermKind.Shares && d.Open && d.LockTo > day ? d.LockTo - day : 0;
    }
}
