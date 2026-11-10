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

        /// <summary>A business sold on dressed books: the part of the price the lift made goes on the trail, and the books are plain again.</summary>
        public static SchemeAct Sold(SchemeState s, string place, long price, int day)
        {
            SchemeShop shop = s.Shop(place, false);
            int lift = shop != null ? shop.Lift : 0;
            if (lift <= 0)
            {
                return null;
            }
            shop.Lift = 0;
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
        /// Money goes back to the households the clubs took from, oldest club first; never more than they are still out.
        /// Returns what went back: the caller gives exactly that to the households.
        /// </summary>
        public static long Recover(SchemeState s, long amount)
        {
            long left = Math.Min(Math.Max(0L, amount), Math.Max(0L, s.ClubTaken - s.ClubBack));
            long done = 0;
            for (int i = 0; i < s.Acts.Count && left > 0; i++)
            {
                SchemeAct a = s.Acts[i];
                if (a.Kind != SchemeKind.Club || a.Owed <= 0)
                {
                    continue;
                }
                long part = Math.Min(left, a.Owed);
                a.Back += part;
                left -= part;
                done += part;
            }
            s.ClubBack += done;
            return done;
        }

        /// <summary>What a till washes on a day: what the business could plausibly sell more, as far as the cash in hand goes.</summary>
        public static long WashRoom(long averageTakings, int rate, long cash)
        {
            return Math.Max(0L, Math.Min(SchemeCatalogue.Share(Math.Max(0L, averageTakings), WashStep(rate)), cash));
        }

        /// <summary>What reaches the player of a sum washed: the cut is lost on the way, and the game's tax rate (per cent) is taken off the rest as on any sale.</summary>
        public static long WashNet(long amount, int taxPercent)
        {
            long after = Math.Max(0L, amount) - SchemeCatalogue.Share(amount, SchemeCatalogue.WashCut);
            return after - after * SchemeCatalogue.Clamp(taxPercent, 0, 100) / 100L;
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
            return sum;
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
                fromCash = s.Watched ? 0L : Math.Min(s.Dirty, cost);
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
