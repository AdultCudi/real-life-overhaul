using System;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// The rules of giving, with no game in them: steps, the month and the tax year, the deduction and its cap, what a month's gifts earn in standing and the press, what a person's
    /// gifts earn in a window, a loan's parts, who asks and for how much, and a rival's gift. Everything is a whole number worked out from its inputs and a seed, so the same
    /// inputs give the same answer on every machine. COST: a few operations a call, nothing allocated.
    /// </summary>
    public static class GivingRules
    {
        // ---- money at today's prices

        /// <summary>
        /// The cash the investors' floor takes out of what may be given, whole money: the floor is held in cents, and a gift of exactly the spare amount must not leave the wallet a
        /// cent under it, so the reserve is rounded up.
        /// </summary>
        public static long Reserve(long floorCents) => floorCents <= 0 ? 0L : (floorCents + 99) / 100;

        /// <summary>An amount written at the start's prices, at the price level (tenths of a point), in whole money.</summary>
        public static int At(int amount, int level) => (int)Math.Round(PriceRules.At((float)amount, level));

        /// <summary>An amount at today's prices written back at the start's, in whole money.</summary>
        public static int Back(long amount, int level) => (int)Math.Min(int.MaxValue / 2, amount * PriceRules.Start / Math.Max(PriceRules.Least, level));

        /// <summary>A step of the list at today's prices; the index is held inside the list.</summary>
        public static int StepNow(int index, int level) => At(GivingCatalogue.Steps[GivingCatalogue.Clamp(index, 0, GivingCatalogue.Steps.Length - 1)], level);

        /// <summary>The nearest step to an amount at the start's prices.</summary>
        public static int StepOf(int amount)
        {
            int best = 0;
            for (int i = 1; i < GivingCatalogue.Steps.Length; i++)
            {
                best = Math.Abs(GivingCatalogue.Steps[i] - amount) < Math.Abs(GivingCatalogue.Steps[best] - amount) ? i : best;
            }
            return best;
        }

        // ---- the calendar

        /// <summary>The month a day is in, from 0: a twelfth of the save's year, as the economy's months are.</summary>
        public static int MonthOf(int day, int monthDays) => (Math.Max(1, day) - 1) / Math.Max(1, monthDays);

        // (The tax year is GivingYear's: when it turns, and what is claimed.)

        // ---- the tax return

        /// <summary>The most a year's income lets be deducted for gifts.</summary>
        public static long DeductionCap(long income) => Math.Max(0L, income) * GivingCatalogue.DeductionShare / 1000;

        /// <summary>
        /// What is still to be deducted: the year's gifts to charities, held to the cap on the income so far, less what is claimed already (<see cref="GivingYear.Claimed"/>). Income
        /// only grows, so a claim made on the income so far is never over the cap on the whole year's; and if it ever fell, the answer would be nothing, never a claim taken back.
        /// Never negative.
        /// </summary>
        public static long DeductionDue(long gifts, long deducted, long income)
        {
            long allowed = Math.Min(Math.Max(0L, gifts), DeductionCap(income));
            return Math.Max(0L, allowed - Math.Max(0L, deducted));
        }

        // ---- standing and the press

        /// <summary>most x total / (total + half): the more is given, the less each gift earns, and ten gifts of a tenth earn what one gift of the whole does.</summary>
        public static long Curve(long total, long most, long half)
        {
            if (total <= 0 || most <= 0)
            {
                return 0;
            }
            return most * total / (total + Math.Max(1, half));
        }

        /// <summary>Points of integrity a month's gifts (at the start's prices) have earned so far.</summary>
        public static int IntegrityOf(long given) => (int)Curve(given, GivingCatalogue.IntegrityMost, GivingCatalogue.IntegrityHalf);

        /// <summary>Tenths of a point of recognition a month's gifts have earned so far; a gift is no news under a tenth of a point.</summary>
        public static int PressOf(long given) => (int)Curve(given * 10, GivingCatalogue.PressMost * 10, GivingCatalogue.PressHalf * 10) / 10;

        // ---- people

        /// <summary>
        /// A gift to a person counted in their window: the window begins at the first gift and ends after <see cref="GivingCatalogue.WindowDays"/> days. The gift counts in full
        /// to someone in need and by <see cref="GivingCatalogue.NoNeedShare"/> otherwise. Returns the tenths of a point of affection and of trust still to be given for it.
        /// </summary>
        public static void PersonGain(PersonGiving g, int giftAtStart, int day, bool inNeed, out int affection, out int trust)
        {
            if (g.Start <= 0 || day - g.Start >= GivingCatalogue.WindowDays || day < g.Start)
            {
                g.Start = day;
                g.Given = 0;
                g.AffectionDone = 0;
                g.TrustDone = 0;
            }
            long counted = (long)Math.Max(0, giftAtStart) * (inNeed ? 1000 : GivingCatalogue.NoNeedShare) / 1000;
            g.Given = (int)Math.Min(int.MaxValue / 2, g.Given + counted);
            int a = (int)Curve(g.Given, GivingCatalogue.AffectionMost * 10, GivingCatalogue.GiftHalf);
            int t = (int)Curve(g.Given, GivingCatalogue.TrustMost * 10, GivingCatalogue.GiftHalf);
            affection = Math.Max(0, a - g.AffectionDone);
            trust = Math.Max(0, t - g.TrustDone);
            g.AffectionDone += affection;
            g.TrustDone += trust;
        }

        /// <summary>What a loan counts for in a person's window, at the start's prices: a share of a gift of the same size, because it comes back.</summary>
        public static int LoanCounts(int amountAtStart) => (int)((long)Math.Max(0, amountAtStart) * GivingCatalogue.LoanShare / 1000);

        /// <summary>A loan's most a month asks: an equal part of it over the months it is paid back in, never under one.</summary>
        public static int LoanPart(int principal) => Math.Max(1, (principal + GivingCatalogue.LoanParts - 1) / GivingCatalogue.LoanParts);

        /// <summary>What a household pays back of a part: what it can spare, never more than the part or what is owed.</summary>
        public static int Repayment(int remaining, int part, long spare) => (int)Math.Max(0L, Math.Min(Math.Min(remaining, part), spare));

        // ---- who asks

        /// <summary>
        /// The chance an hour, out of ten thousand, that somebody in trouble who may ask does: the base chance, more as the player is richer and better known, to the boost's
        /// most. <paramref name="wealthTier"/> is the number of the wealth steps the player has passed (0 to 5) and <paramref name="recognition"/> the city's knowledge of them (0 to 100).
        /// </summary>
        public static int AskPerTenThousand(int wealthTier, int recognition)
        {
            int boost = GivingCatalogue.Clamp(wealthTier * 100 + Math.Max(0, recognition) * 5, 0, GivingCatalogue.AskBoostMost);
            return GivingCatalogue.AskChance * 10 * (1000 + boost) / 1000;
        }

        /// <summary>Whether somebody who may ask does in this hour: a seeded draw of the save's seed, the person and the hour, against the chance a ten thousand.</summary>
        public static bool Asks(int seed, int person, int day, int hour, int chance) => Hash(seed, 91, person, day * 24 + hour) % 10000 < (uint)Math.Max(0, chance);

        /// <summary>Whether it may be asked now: the global gap and the person's own gap have both passed, and the person is close enough to ask.</summary>
        public static bool MayAsk(int day, int lastAsk, int personAsk, int affection, int trust)
        {
            return (lastAsk <= 0 || day - lastAsk >= GivingCatalogue.AskGapDays) && (personAsk <= 0 || day - personAsk >= GivingCatalogue.AskPersonGapDays)
                   && affection + trust >= GivingCatalogue.AskLeast;
        }

        /// <summary>What a person in trouble asks for, whole money at today's prices: their debt, held to the most and rounded to fifty.</summary>
        public static int AskAmount(long debt, int level)
        {
            long most = At(GivingCatalogue.AskMost, level);
            long ask = Math.Min(Math.Max(0L, debt), most);
            return (int)Math.Max(0L, ask / 50 * 50);
        }

        // ---- rivals

        /// <summary>
        /// A tycoon's gift this month: their worth times the average yearly share, moved by a seeded rate of their own (each gives a little more or less than the
        /// average, the same for a save and every machine), over the months of a year. Zero for no worth.
        /// </summary>
        public static long RivalGift(int seed, int tycoon, long worth, int monthsPerYear, out int charity)
        {
            uint h = Hash(seed, tycoon, 1, 0);
            charity = (int)(Hash(seed, tycoon, 2, 0) % (uint)Charity.Count);
            if (worth <= 0)
            {
                return 0;
            }
            long spread = (long)(h % (uint)(2 * GivingCatalogue.RivalSpread + 1)) - GivingCatalogue.RivalSpread;
            long yearly = worth * GivingCatalogue.RivalYearShare / 1000 * (1000 + spread) / 1000;
            return Math.Max(0L, yearly / Math.Max(1, monthsPerYear));
        }

        /// <summary>
        /// What of a gift a tycoon really gives: only from worth above the floor their worth grows back to (the rivals raise a worth to at least their start's, each day and
        /// when they come forward), so what is given is never made up the next day. Nothing at or under the floor.
        /// </summary>
        public static long RivalPaid(long gift, long worth, long floor) => Math.Max(0L, Math.Min(gift, worth - Math.Max(0L, floor)));

        /// <summary>The charity a tycoon's gift goes to in a month: their own favourite most months, another now and then (seeded).</summary>
        public static int RivalCharity(int seed, int tycoon, int month)
        {
            uint favourite = Hash(seed, tycoon, 2, 0) % (uint)Charity.Count;
            return Hash(seed, tycoon, 3, month) % 4 == 0 ? (int)(Hash(seed, tycoon, 4, month) % (uint)Charity.Count) : (int)favourite;
        }

        /// <summary>A hash of four numbers, the same on every machine.</summary>
        public static uint Hash(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ (uint)c * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        // ---- a charity's month

        /// <summary>What a charity may pay out in a month: its cap for each household of the city, at today's prices; at least what is in the pot is not paid past it.</summary>
        public static long MonthCap(Charity c, int households, int level) => (long)Math.Max(0, households) * At(GivingCatalogue.CapPerHousehold[(int)c], level);

        /// <summary>What it pays now: the pot, held to the cap less what it has paid this month already.</summary>
        public static long PayNow(long pot, long cap, long paidThisMonth) => Math.Max(0L, Math.Min(pot, cap - paidThisMonth));
    }
}
