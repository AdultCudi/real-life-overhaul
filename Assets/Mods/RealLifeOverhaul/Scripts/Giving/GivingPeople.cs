using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Giving
{
    using Ledger = RealLifeOverhaul.Population.Ledger;

    /// <summary>What a person is short of, as the population holds it. Saved by nobody: it is read when asked.</summary>
    public enum GivingNeed
    {
        None = 0,
        Work = 1,    // out of work
        Debt = 2,    // owes more than a month's income
        Rent = 3     // rent takes more of the income than the population moves a household at
    }

    /// <summary>
    /// Money that reaches people, on the living population's own fields (a household's savings, debt and content; a person's skill): the same call for the player's gift, a
    /// charity's month and a neighbour's, so the money lands one way. Game-free. Nobody of the player's own household, nobody whose rent goes to the player and nobody another
    /// feature owns is helped, so what is given never finds its way back to the player. COST: a pass over the households or the people once a month, a few operations each, and
    /// nothing allocated (the best few are kept in two arrays).
    /// </summary>
    public static class GivingPeople
    {
        private static readonly int[] BestSlot = new int[GivingCatalogue.HelpedMost];
        private static readonly long[] BestScore = new long[GivingCatalogue.HelpedMost];
        private static int _best;

        // ---- who is who

        /// <summary>The resident the social cast's person is, or -1: the person met, the partner, a roommate (not asked here), looked for by the cast's index.</summary>
        public static int ResidentOf(PopulationState s, int socialIndex)
        {
            if (s == null)
            {
                return -1;
            }
            string key = "social:" + socialIndex;
            int p = PopulationLinks.Linked(s, LinkKind.Met, key);
            return p >= 0 ? p : PopulationLinks.Linked(s, LinkKind.Partner, key);
        }

        public static int HouseholdOf(PopulationState s, int p) => p >= 0 && p < s.People.Count && s.People.Id[p] != 0 ? s.People.Household[p] : -1;

        /// <summary>True for a household that may be helped: alive, not the player's own, not away, and not one the player lets a flat to.</summary>
        public static bool Helpable(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            return hh.Alive(h) && (hh.Flags[h] & (HouseholdFlags.Players | HouseholdFlags.Away)) == 0 && hh.Landlord[h] != Landlords.Player;
        }

        // ---- what a household has

        /// <summary>The last closed month's income after nothing is taken off, whole money: wages, benefits and pensions.</summary>
        public static long MonthIncome(PopulationState s, int h)
        {
            int row = h * RealLifeOverhaul.Population.Ledger.Lines;
            int[] last = s.Households.Last;
            return ((long)last[row + Ledger.Wages] + last[row + Ledger.Benefits] + last[row + Ledger.Pension]) / 100;
        }

        /// <summary>The last closed month's rent, whole money.</summary>
        public static long MonthRent(PopulationState s, int h) => s.Households.Last[h * RealLifeOverhaul.Population.Ledger.Lines + RealLifeOverhaul.Population.Ledger.Rent] / 100;

        /// <summary>What a household can spare to pay somebody back: its savings less a month's rent, never under nothing.</summary>
        public static long Spare(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            long keep = (long)hh.Rent[h] * Math.Max(3, s.YearDays / 12) / 100;
            return Math.Max(0L, hh.Savings[h] - keep);
        }

        /// <summary>What a person is short of: out of work first, then debt past a month's income, then a rent over the line. <paramref name="owes"/> is their household's debt.</summary>
        public static GivingNeed NeedOf(PopulationState s, int p, out long owes)
        {
            owes = 0;
            int h = HouseholdOf(s, p);
            if (h < 0 || !s.Households.Alive(h))
            {
                return GivingNeed.None;
            }
            HouseholdTable hh = s.Households;
            owes = hh.Debt[h];
            if (s.People.Status[p] == (byte)Status.Unemployed)
            {
                return GivingNeed.Work;
            }
            long income = MonthIncome(s, h);
            if (hh.Debt[h] > 0 && hh.Debt[h] >= income)
            {
                return GivingNeed.Debt;
            }
            return RentBurden(s, h) > PopulationCatalogue.RentBurdenMove ? GivingNeed.Rent : GivingNeed.None;
        }

        /// <summary>The share of last month's income the rent took, thousandths; 0 for none known or an owner's upkeep.</summary>
        public static int RentBurden(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            long income = MonthIncome(s, h), rent = MonthRent(s, h);
            if (income <= 0 || rent <= 0 || hh.Landlord[h] == Landlords.Own)
            {
                return 0;
            }
            return (int)Math.Min(100000L, rent * 1000 / income);
        }

        // ---- money that lands

        /// <summary>
        /// Money to a household: its debt first, then its savings, and its content lifted a little. Returns what went to the debt in <paramref name="toDebt"/> and what to the
        /// savings in <paramref name="toSavings"/>. Nothing is made: the caller has taken the money from the player, a pot or another household.
        /// </summary>
        public static void Help(PopulationState s, int h, long amount, out long toDebt, out long toSavings)
        {
            HouseholdTable hh = s.Households;
            long give = Math.Max(0L, amount);
            toDebt = Math.Min(give, hh.Debt[h]);
            hh.Debt[h] = (int)Math.Max(0L, hh.Debt[h] - toDebt);
            toSavings = give - toDebt;
            hh.Savings[h] = (int)Math.Min(int.MaxValue / 2, hh.Savings[h] + toSavings);
            hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.ContentLift, 0, 100);
        }

        /// <summary>A household pays money out of its savings to somebody: at most what it can spare. Returns what left them.</summary>
        public static long Take(PopulationState s, int h, long amount)
        {
            long take = Math.Min(Math.Max(0L, amount), Spare(s, h));
            s.Households.Savings[h] = (int)Math.Max(0L, s.Households.Savings[h] - take);
            return take;
        }

        // ---- a charity's month

        /// <summary>The food bank's month: households that owe more than a month's income, the worst off first. Returns how many it reached; <paramref name="spent"/> is what it paid.</summary>
        public static int PayFoodBank(PopulationState s, long budget, int level, out long spent)
        {
            spent = 0;
            if (budget <= 0)
            {
                return 0;
            }
            HouseholdTable hh = s.Households;
            _best = 0;
            for (int h = 0; h < hh.Count; h++)
            {
                if (!Helpable(s, h) || hh.Debt[h] <= 0)
                {
                    continue;
                }
                long income = MonthIncome(s, h);
                if (hh.Debt[h] >= income && (long)hh.Savings[h] * 4 < hh.Debt[h])
                {
                    Keep(h, hh.Debt[h] * 1000L / (income + 100));
                }
            }
            return Pay(s, budget, GivingRules.At(GivingCatalogue.HelpMost, level), level, 0, out spent);
        }

        /// <summary>The housing fund's month: tenants whose rent takes more than the line the population moves a household at, the part of the rent over the line paid.</summary>
        public static int PayHousing(PopulationState s, long budget, int level, out long spent)
        {
            spent = 0;
            if (budget <= 0)
            {
                return 0;
            }
            HouseholdTable hh = s.Households;
            _best = 0;
            for (int h = 0; h < hh.Count; h++)
            {
                if (!Helpable(s, h))
                {
                    continue;
                }
                int burden = RentBurden(s, h);
                if (burden > PopulationCatalogue.RentBurdenMove)
                {
                    Keep(h, burden);
                }
            }
            return Pay(s, budget, GivingRules.At(GivingCatalogue.HelpMost, level), level, 1, out spent);
        }

        /// <summary>
        /// The training fund's month: people out of work with the least skill take a course, a few points each; the pay a job pays follows their skill (LabourRules.PayFor).
        /// Returns how many took one; <paramref name="spent"/> is what the courses cost.
        /// </summary>
        public static int PayTraining(PopulationState s, long budget, int level, out long spent)
        {
            spent = 0;
            int cost = Math.Max(1, GivingRules.At(GivingCatalogue.TrainingCourse, level));
            if (budget < cost)
            {
                return 0;
            }
            PersonTable pt = s.People;
            _best = 0;
            for (int p = 0; p < pt.Count; p++)
            {
                if (pt.Id[p] == 0 || pt.Status[p] != (byte)Status.Unemployed || (pt.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) != 0 || pt.Skill[p] >= 100
                    || pt.Household[p] < 0 || !Helpable(s, pt.Household[p]))
                {
                    continue;
                }
                Keep(p, 100000L - pt.Skill[p] * 1000L - Math.Min(999, Math.Max(0, s.Day - pt.Since[p])));
            }
            int helped = 0;
            for (int i = 0; i < _best && budget - spent >= cost; i++)
            {
                int p = BestSlot[i];
                pt.Skill[p] = (byte)Math.Min(100, pt.Skill[p] + GivingCatalogue.TrainingSkill);
                spent += cost;
                helped++;
            }
            return helped;
        }

        // The worst-off households paid in order, each what the charity's help for that house is, until the money is gone.
        private static int Pay(PopulationState s, long budget, int most, int level, int kind, out long spent)
        {
            spent = 0;
            int helped = 0;
            for (int i = 0; i < _best && spent < budget; i++)
            {
                int h = BestSlot[i];
                long want = kind == 0 ? s.Households.Debt[h] : Math.Max(0L, MonthRent(s, h) - MonthIncome(s, h) * PopulationCatalogue.RentBurdenMove / 1000);
                long pay = Math.Min(Math.Min(want, most), budget - spent);
                if (pay <= 0)
                {
                    continue;
                }
                Help(s, h, pay, out _, out _);
                spent += pay;
                helped++;
            }
            return helped;
        }

        // Insertion into the few worst off: descending by score, the least dropped when full.
        private static void Keep(int slot, long score)
        {
            if (_best == BestSlot.Length && score <= BestScore[_best - 1])
            {
                return;
            }
            int at = Math.Min(_best, BestSlot.Length - 1);
            while (at > 0 && BestScore[at - 1] < score)
            {
                BestSlot[at] = BestSlot[at - 1];
                BestScore[at] = BestScore[at - 1];
                at--;
            }
            BestSlot[at] = slot;
            BestScore[at] = score;
            _best = Math.Min(BestSlot.Length, _best + 1);
        }

        // ---- neighbours

        /// <summary>
        /// The people helping one another: a household with plenty (savings past a year's income less a few months') gives a small share of them to a household in hardship, the
        /// worst off first, one gift for each <see cref="GivingCatalogue.NeighboursPer"/> households. Every dollar leaves one household and reaches another. Returns the households helped.
        /// </summary>
        public static int Neighbours(PopulationState s, int month, int level, out long moved)
        {
            moved = 0;
            HouseholdTable hh = s.Households;
            int quota = hh.Live / GivingCatalogue.NeighboursPer;
            if (quota <= 0 || hh.Count == 0)
            {
                return 0;
            }
            _best = 0;
            for (int h = 0; h < hh.Count; h++)
            {
                if (Helpable(s, h) && hh.Debt[h] > 0)
                {
                    long income = MonthIncome(s, h);
                    if (hh.Debt[h] >= income && (long)hh.Savings[h] * 4 < hh.Debt[h])
                    {
                        Keep(h, hh.Debt[h] * 1000L / (income + 100));
                    }
                }
            }
            int helped = 0;
            int most = Math.Max(1, GivingRules.At(GivingCatalogue.HelpMost, level) / 2);
            int start = (int)(GivingRules.Hash(s.Seed, 77, month, 0) % (uint)hh.Count);
            int next = 0;
            for (int n = 0; n < hh.Count && next < _best && helped < quota; n++)
            {
                int donor = (start + n) % hh.Count;
                if (!Helpable(s, donor))
                {
                    continue;
                }
                long income = MonthIncome(s, donor);
                if (income <= 0 || hh.Savings[donor] < income * GivingCatalogue.NeighbourRich)
                {
                    continue;
                }
                int receiver = BestSlot[next++];
                if (receiver == donor)
                {
                    continue;
                }
                long gift = Math.Min(Math.Min(hh.Savings[donor] * (long)GivingCatalogue.NeighbourShare / 1000, most), hh.Debt[receiver]);
                if (gift <= 0)
                {
                    continue;
                }
                hh.Savings[donor] = (int)Math.Max(0L, hh.Savings[donor] - gift);
                Help(s, receiver, gift, out _, out _);
                moved += gift;
                helped++;
            }
            return helped;
        }
    }
}
