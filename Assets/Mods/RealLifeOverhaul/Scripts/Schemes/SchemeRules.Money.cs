using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schemes
{
    // The money: what each scheme brings, what a business could plausibly hide, the cash in hand and the way back.
    public static partial class SchemeRules
    {
        /// <summary>Whether an invented claim may be put in at a business today: not within the wait since the last one there.</summary>
        public static bool MayClaim(SchemeShop shop, int day)
        {
            return shop == null || day - shop.ClaimDay >= SchemeCatalogue.ClaimGapDays;
        }

        /// <summary>Whether an invented expense may go on the books today.</summary>
        public static bool MayExpense(SchemeState s, int day)
        {
            return day - s.ExpenseDay >= SchemeCatalogue.ExpenseGapDays;
        }

        /// <summary>Whether a club may be sold from a business today.</summary>
        public static bool MayClub(SchemeShop shop, int day)
        {
            return shop == null || day - shop.ClubDay >= SchemeCatalogue.ClubGapDays;
        }

        /// <summary>An invented claim was put in at a business: on the trail with what the insurer will pay, and the wait begins.</summary>
        public static SchemeAct Claimed(SchemeState s, string place, int day, long payout, int size, int claim)
        {
            SchemeShop shop = s.Shop(place, true);
            if (shop != null)
            {
                shop.ClaimDay = day;
            }
            return Record(s, SchemeKind.Claim, day, payout, size, 0, true, place, claim);
        }

        /// <summary>An invented expense went on the books: on the trail with the tax it saved, and the wait begins.</summary>
        public static SchemeAct Expensed(SchemeState s, int day, long saved, int size)
        {
            s.ExpenseDay = day;
            return Record(s, SchemeKind.Expense, day, saved, size, 0, true, string.Empty, 0);
        }

        /// <summary>The loss an invented claim names: a share of the most the cover counts.</summary>
        public static int ClaimLoss(int coverMost, int size)
        {
            return (int)Math.Min(int.MaxValue / 4, SchemeCatalogue.Share(Math.Max(0, coverMost), SchemeCatalogue.Clamp(size, 0, SchemeCatalogue.Large)));
        }

        /// <summary>An invented expense of a size: so many days of the businesses' average takings.</summary>
        public static long ExpenseAmount(long averageTakings, int size)
        {
            return Round(SchemeCatalogue.Share(Math.Max(0L, averageTakings) * SchemeCatalogue.ExpenseDays, SchemeCatalogue.Clamp(size, 0, SchemeCatalogue.Large)));
        }

        /// <summary>The tax an invented expense saves at the game's rate: what it brings, and what is owed again if it is found.</summary>
        public static long ExpenseGain(long amount, int taxPercent)
        {
            return Math.Max(0L, amount) * SchemeCatalogue.Clamp(taxPercent, 0, 100) / 100L;
        }

        /// <summary>A lift brought to a step the books can show.</summary>
        public static int LiftStep(int permille)
        {
            int step = 0;
            for (int i = 0; i < SchemeCatalogue.LiftSteps.Length; i++)
            {
                step = SchemeCatalogue.LiftSteps[i] <= permille ? SchemeCatalogue.LiftSteps[i] : step;
            }
            return step;
        }

        /// <summary>A washing rate brought to a step a till could plausibly take.</summary>
        public static int WashStep(int permille)
        {
            int step = 0;
            for (int i = 0; i < SchemeCatalogue.WashSteps.Length; i++)
            {
                step = SchemeCatalogue.WashSteps[i] <= permille ? SchemeCatalogue.WashSteps[i] : step;
            }
            return step;
        }

        /// <summary>A figure as the dressed books show it: the truth and the lift on top. A loss is not dressed up.</summary>
        public static float Lifted(float truth, int lift)
        {
            return truth > 0f && lift > 0 ? truth * (1000 + Math.Min(lift, SchemeCatalogue.LiftMost)) / 1000f : truth;
        }

        /// <summary>The part of a price that the lift made: what the buyer paid for sales that never happened.</summary>
        public static long Over(long price, int lift)
        {
            lift = SchemeCatalogue.Clamp(lift, 0, SchemeCatalogue.LiftMost);
            return lift <= 0 ? 0L : Math.Max(0L, price) * lift / (1000L + lift);
        }

        /// <summary>
        /// Whether a buyer sees through dressed books before paying: how hard their kind looks, by how far the books are
        /// lifted. Fixed by the seed, the place and the offer, so the same offer is always seen through or never.
        /// </summary>
        public static bool SeenThrough(int seed, string place, int buyerKind, int lift, int offer)
        {
            if (lift <= 0)
            {
                return false;
            }
            int look = SchemeCatalogue.Diligence[SchemeCatalogue.Clamp(buyerKind, 0, SchemeCatalogue.Diligence.Length - 1)];
            int chance = look * Math.Min(lift, SchemeCatalogue.LiftMost) / SchemeCatalogue.LiftMost;
            return Roll(seed, DrawDiligence, Text(place), offer) < chance;
        }

        /// <summary>
        /// A buyer looks at the books a price was made from, before paying: <paramref name="lift"/> is the lift stamped on the offer or the
        /// quick sale when its price was made, never what the books show now. Seen through, the buyer walks, the books are plain again and
        /// the walk heats the trail. True if they walked.
        /// </summary>
        public static bool Looked(SchemeState s, string place, int buyerKind, int lift, int offer)
        {
            if (!SeenThrough(s.Seed, place, buyerKind, lift, offer))
            {
                return false;
            }
            Walked(s, place);
            return true;
        }

        /// <summary>
        /// A business sold at a price made from books lifted by <paramref name="lift"/> (the stamp on the offer, whatever the books show
        /// now): the part of the price the lift made goes on the trail, and the business's books are plain from here. Null for a price made
        /// from plain books.
        /// </summary>
        public static SchemeAct Sold(SchemeState s, string place, long price, int lift, int day)
        {
            SchemeShop shop = s.Shop(place, false);
            if (shop != null)
            {
                shop.Lift = 0;
            }
            lift = SchemeCatalogue.Clamp(lift, 0, SchemeCatalogue.LiftMost);
            if (lift <= 0)
            {
                return null;
            }
            return Record(s, SchemeKind.Books, day, Over(price, lift), lift * 1000 / SchemeCatalogue.LiftMost, 0, true, place, day);
        }

        /// <summary>A buyer saw through the books and walked: said aloud, and the books are plain again.</summary>
        public static void Walked(SchemeState s, string place)
        {
            SchemeShop shop = s.Shop(place, false);
            if (shop != null)
            {
                shop.Lift = 0;
            }
            AddHeat(s, SchemeKind.Books, SchemeCatalogue.HeatWalked);
        }

        /// <summary>How many households a club of a size approaches.</summary>
        public static int ClubHouseholds(int size)
        {
            return size >= SchemeCatalogue.Large ? SchemeCatalogue.ClubLarge : SchemeCatalogue.ClubSmall;
        }

        /// <summary>A household's stake in the club: a share of its savings, never more than the most, nothing from one with little.</summary>
        public static int ClubStake(long savings, int level)
        {
            if (savings < SchemeCatalogue.AtPrices(SchemeCatalogue.ClubSavingsLeast, level))
            {
                return 0;
            }
            return (int)Math.Min(SchemeCatalogue.Share(savings, SchemeCatalogue.ClubStakePermille), SchemeCatalogue.AtPrices(SchemeCatalogue.ClubStakeMost, level));
        }

        /// <summary>The draw a household is picked for a club by: the lowest are approached, the same households for the same club.</summary>
        public static int ClubDraw(int seed, int club, int household)
        {
            return Roll(seed, DrawClub, club, household);
        }

        /// <summary>A club was sold: its takings are cash in hand, owed to the households, and the act goes on the trail with those who will complain.</summary>
        public static SchemeAct ClubSold(SchemeState s, string place, long taken, int households, int size, int day)
        {
            taken = Math.Max(0L, taken);
            s.Dirty += taken;
            s.ClubTaken += taken;
            SchemeShop shop = s.Shop(place, true);
            if (shop != null)
            {
                shop.ClubDay = day;
            }
            return Record(s, SchemeKind.Club, day, taken, size, Math.Max(1, households / SchemeCatalogue.ClubKnowsEvery), false, place, households);
        }

        /// <summary>
        /// Money goes back to the households the clubs took from; never more than they are still out. The ledger of what they are out
        /// (<see cref="SchemeState.ClubTaken"/> less <see cref="SchemeState.ClubBack"/>) is all it touches: another kind's finding
        /// (a till that washed their money) makes no club's act look repaid. Returns what went back: the caller gives exactly that to
        /// the households.
        /// </summary>
        public static long Recover(SchemeState s, long amount)
        {
            long done = Math.Min(Math.Max(0L, amount), Math.Max(0L, s.ClubTaken - s.ClubBack));
            s.ClubBack += done;
            return done;
        }

        /// <summary>What the households are still out of the clubs' takings.</summary>
        public static long ClubOwed(SchemeState s)
        {
            return Math.Max(0L, s.ClubTaken - s.ClubBack);
        }

        /// <summary>Why a press is refused before anything moves; the director says it in a notice. None: the press may go on.</summary>
        public enum Refusal
        {
            None = 0,
            NotNow = 1,     // not the player's, nothing there for it, or the option off
            TooSoon = 2,    // its wait is not over
            NoPeople = 3,   // the city's people are not this machine's own: a Going Public session, or the population off or followed
            NoTax = 4,      // the game's tax does not fall due
            Short = 5       // the wallet cannot pay it and still keep what belongs to the investors of the player's firms
        }

        /// <summary>A club: refused in a Going Public session and wherever the city's people are not this machine's own, before any household is asked.</summary>
        public static Refusal ClubRefusal(bool on, bool mine, bool cash, bool inSession, bool peopleMine, bool door, SchemeShop kept, int day)
        {
            if (!on || !mine || !cash)
            {
                return Refusal.NotNow;
            }
            if (inSession || !peopleMine || !door)
            {
                return Refusal.NoPeople;
            }
            return MayClub(kept, day) ? Refusal.None : Refusal.TooSoon;
        }

        /// <summary>An invented claim: a cover in force, the insurer's door, and its wait.</summary>
        public static Refusal ClaimRefusal(bool on, bool mine, bool covered, bool door, SchemeShop kept, int day)
        {
            if (!on || !mine || !covered || !door)
            {
                return Refusal.NotNow;
            }
            return MayClaim(kept, day) ? Refusal.None : Refusal.TooSoon;
        }

        /// <summary>An invented expense: the game's tax must fall due at all, and its wait.</summary>
        public static Refusal ExpenseRefusal(bool on, int taxPercent, bool taxed, SchemeState s, int day)
        {
            if (!on || taxPercent <= 0 || !taxed)
            {
                return Refusal.NoTax;
            }
            return MayExpense(s, day) ? Refusal.None : Refusal.TooSoon;
        }

        /// <summary>The money in a rival's fund: not in a session, only while the offer stands and its rival can pay the return, and never under the investors' floor.</summary>
        public static Refusal FundPutRefusal(SchemeState s, int day, bool inSession, long rivalWorth, long cash, long floor)
        {
            if (inSession || !FundOpenFor(s, day, rivalWorth))
            {
                return Refusal.NotNow;
            }
            return Affords(cash, s.Fund.Amount, floor) ? Refusal.None : Refusal.Short;
        }

        /// <summary>Having a fund looked at: not in a session, while the offer stands and was not looked at yet, and the fee above the investors' floor.</summary>
        public static Refusal FundLookRefusal(SchemeState s, int day, bool inSession, long fee, long cash, long floor)
        {
            if (inSession || !FundStands(s, day) || s.Fund.Checked)
            {
                return Refusal.NotNow;
            }
            return Affords(cash, fee, floor) ? Refusal.None : Refusal.Short;
        }

        /// <summary>
        /// Paying a kind's file back: something to pay, and the wallet's part (the cost less the cash in hand that covers it) above the
        /// investors' floor, refused whole otherwise. <paramref name="wallet"/> is what the wallet would pay.
        /// </summary>
        public static Refusal RepayRefusal(SchemeState s, SchemeKind kind, int day, List<int> unpaid, long cash, long floor, out long cost, out long wallet)
        {
            cost = RepayCost(s, kind, day, unpaid);
            wallet = Math.Max(0L, cost - RepayCash(s, kind, cost));
            if (cost <= 0)
            {
                return Refusal.NotNow;
            }
            return Affords(cash, wallet, floor) ? Refusal.None : Refusal.Short;
        }

        /// <summary>
        /// Money offered to somebody: refused before anything moves for one already cold to the player, a favour not priced, or a price the
        /// wallet cannot pay above the investors' floor.
        /// </summary>
        public static Refusal FavourRefusal(SchemeState s, string contact, long price, bool cash, long wallet, long floor)
        {
            if (IsCold(s, contact) || price <= 0)
            {
                return Refusal.NotNow;
            }
            return cash || Affords(wallet, price, floor) ? Refusal.None : Refusal.Short;
        }

        /// <summary>Nobody to take the money now: no one to ask, or a council vote in a Going Public session. Nothing is offered and no money moves.</summary>
        public static bool NobodyToAsk(Favour favour, string who, bool inSession)
        {
            return who == null || (favour == Favour.Vote && inSession);
        }

        /// <summary>
        /// A file goes to the court on a summons: the charge, the evidence the inquiry gathered, the sum it is about, the case's own seed
        /// and whether the player told the investigators everything (<see cref="SchemeInquiry.Coop"/>, never how much evidence there is).
        /// <paramref name="charge"/> is the courts' door; its number, or 0 with no court.
        /// </summary>
        public static int FileCase(SchemeState s, SchemeInquiry q, string chargeId, long amount, Func<string, int, long, int, bool, int> charge)
        {
            return charge != null ? charge(chargeId, q.Evidence, amount, s.Seed ^ q.Id * 7919, q.Coop) : 0;
        }

        /// <summary>What a settlement moves: out of the wallet by force, the penalty on top, and the part that goes back into the households' savings.</summary>
        public struct Due
        {
            public long Wallet, Surcharge, Households;
        }

        /// <summary>
        /// What an inquiry's findings move: what was found less what the cash in hand covered comes out of the wallet whatever it holds, the
        /// penalty with it, and what a club or a till took from the households goes back to them. The director carries it out and the
        /// simulations count it, from this one rule.
        /// </summary>
        public static Due FindingsDue(SchemeKind kind, long found, long fromCash, long surcharge)
        {
            return new Due
            {
                Wallet = Math.Max(0L, found - fromCash), Surcharge = Math.Max(0L, surcharge),
                Households = (kind == SchemeKind.Club || kind == SchemeKind.Wash) && found > 0 ? found : 0L
            };
        }

        /// <summary>What a file paid back moves: the wallet pays what the cash in hand did not cover, and a club's stakes go back to its households.</summary>
        public static Due RepayDue(SchemeKind kind, long paid, long fromCash)
        {
            return new Due { Wallet = Math.Max(0L, paid - fromCash), Households = kind == SchemeKind.Club && paid > 0 ? paid : 0L };
        }

        /// <summary>The ids of the invented claims an inquiry's findings took in: the insurer refuses one not yet paid and will not write that cover again.</summary>
        public static void ClaimsFound(SchemeState s, SchemeInquiry q, List<int> into)
        {
            into.Clear();
            for (int i = 0; q.Kind == SchemeKind.Claim && i < s.Acts.Count; i++)
            {
                if (s.Acts[i].Kind == SchemeKind.Claim && s.Acts[i].Inquiry == q.Id)
                {
                    into.Add(s.Acts[i].Ref);
                }
            }
        }

        /// <summary>The investors' floor as the schemes count it: the firms keep it in cents; whole money, rounded up, so it is never under theirs.</summary>
        public static long FloorOf(long cents)
        {
            return cents <= 0 ? 0L : (cents + 99L) / 100L;
        }

        /// <summary>Whether a wallet can pay a sum and still keep what belongs to the investors of the player's firms.</summary>
        public static bool Affords(long cash, long amount, long floor)
        {
            return cash - Math.Max(0L, amount) >= Math.Max(0L, floor);
        }

        /// <summary>What a till washes on a day: what the business could plausibly sell more, as far as the cash in hand goes.</summary>
        public static long WashRoom(long averageTakings, int rate, long cash)
        {
            return Math.Max(0L, Math.Min(SchemeCatalogue.Share(Math.Max(0L, averageTakings), WashStep(rate)), cash));
        }

        /// <summary>What reaches the player of a sum washed: the cut is lost on the way, and the game's tax rate (per cent) is taken off the rest as on any sale.</summary>
        public static long WashNet(long amount, int taxPercent)
        {
            long after = SchemeCatalogue.Share(amount, 1000 - SchemeCatalogue.WashCut);   // what is kept is rounded down, so no day's rounding is the player's
            return after * (100 - SchemeCatalogue.Clamp(taxPercent, 0, 100)) / 100L;
        }

        // An invented claim on file that its insurer has not paid yet: there is nothing of it to pay back, and it is withdrawn instead.
        private static bool Unpaid(SchemeAct a, List<int> unpaid)
        {
            return a.Kind == SchemeKind.Claim && unpaid != null && unpaid.Contains(a.Ref);
        }

        /// <summary>
        /// What paying a kind's file back costs today: what its acts still owe, less the claims their insurer has not paid
        /// yet (<paramref name="unpaid"/>, by id). Cash sales and payments to people cannot be paid back.
        /// </summary>
        public static long RepayCost(SchemeState s, SchemeKind kind, int day, List<int> unpaid)
        {
            if (kind == SchemeKind.Wash || kind == SchemeKind.Graft)
            {
                return 0;
            }
            long sum = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                sum += OnFile(a, kind, day) && !Unpaid(a, unpaid) ? a.Owed : 0L;
            }
            return kind == SchemeKind.Club ? Math.Min(sum, ClubOwed(s)) : sum;   // a till's finding may have given some of it back already
        }

        /// <summary>
        /// The part of paying a kind's file back that comes out of the cash in hand: a club's stakes are the cash in hand, as far as it goes,
        /// whether it is watched or not (the investigators take what is handed over). The rest is the wallet's.
        /// </summary>
        public static long RepayCash(SchemeState s, SchemeKind kind, long cost)
        {
            return kind == SchemeKind.Club ? Math.Max(0L, Math.Min(s.Dirty, cost)) : 0L;
        }

        /// <summary>
        /// A kind's file is paid back: every act on file is settled, a claim not yet paid is withdrawn (the caller has its
        /// insurer refuse it), a club's money goes back to its households, and the open inquiry notes what was paid: it
        /// carries half the surcharge. Returns what was paid; <paramref name="fromCash"/> is the part of it out of the cash
        /// in hand (a club's first), the rest is the wallet's.
        /// </summary>
        public static long Repay(SchemeState s, SchemeKind kind, int day, List<int> unpaid, out long fromCash)
        {
            fromCash = 0;
            long cost = RepayCost(s, kind, day, unpaid);
            if (cost <= 0)
            {
                return 0;
            }
            if (kind == SchemeKind.Club)
            {
                fromCash = RepayCash(s, kind, cost);
                s.Dirty -= fromCash;
                s.ClubBack += cost;
            }
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                if (!OnFile(a, kind, day))
                {
                    continue;
                }
                if (Unpaid(a, unpaid))
                {
                    a.Gain = a.Back;   // withdrawn before it paid: nothing was had of it
                }
                else
                {
                    a.Back = a.Gain;
                }
            }
            SchemeInquiry q = s.OpenOf(kind);
            if (q != null)
            {
                q.Repaid += cost;
            }
            return cost;
        }
    }
}
