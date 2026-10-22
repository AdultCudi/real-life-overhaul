using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the bank's level lets it hold now: cents at today's prices (BankTiers.NoLimit: no limit), and its branches.</summary>
    public struct BankCaps
    {
        public long Loans, Deposits;
        public int Branches;
    }

    /// <summary>
    /// The bank's levels (BankTiers) in its rules: what a level may hold and where that binds (its day lends no further
    /// than its book and takes no saver's money past its deposits, a counter does the same, and a branch past its count is
    /// not taken in), what a bank over a limit keeps (all it holds: a limit only stops it taking more), moving up a level,
    /// an older save's bank fitted to the level its capital meets, and the owner's pay out of each day's profit. Game-free;
    /// each is a few sums, and nothing is allocated.
    /// </summary>
    public static partial class OwnBankRules
    {
        public const string UpTopKey = "reallife_ob_why_up_top";               // a national bank has no level above it
        public const string UpRegulatorKey = "reallife_ob_why_up_regulator";   // watched, in breach or in a run
        public const string UpRecordKey = "reallife_ob_why_up_record";         // not yet the record's months at its level
        public const string UpCapitalKey = "reallife_ob_why_up_capital";       // its capital short of the next level's least

        /// <summary>The bank's level, within the table (an unfitted older line counts as a community bank until it is fitted).</summary>
        public static int TierOf(OwnBank b) => b == null ? BankTiers.Community : Clamp(b.Tier, BankTiers.Community, BankTiers.National);

        /// <summary>A level's licence at today's prices and the end game's share.</summary>
        public static long TierLicence(int tier, int level, int share)
        {
            int t = Clamp(tier, BankTiers.Community, BankTiers.National);
            return At(EndScaleRules.Mix(BankTiers.LicenceCents[t], BankTiers.LicenceCentsScale[t], share), level);
        }

        /// <summary>What moving up to a level costs: its licence less the licence of the level below, at today's prices.</summary>
        public static long MoveUpFee(int tier, int level, int share)
        {
            int t = Clamp(tier, BankTiers.Regional, BankTiers.National);
            return Math.Max(0, TierLicence(t, level, share) - TierLicence(t - 1, level, share));
        }

        /// <summary>A level's least capital at the start's prices, at a share of the scale, AgainCapital more for each closure before.</summary>
        public static long TierLeastAt(int tier, int share, int closures)
        {
            int t = Clamp(tier, BankTiers.Community, BankTiers.National);
            return EndScaleRules.Mix(BankTiers.CapitalCents[t], BankTiers.CapitalCentsScale[t], share) * (1000 + Math.Max(0, closures) * OwnBankCatalogue.AgainCapital) / 1000;
        }

        /// <summary>A level's least capital at today's prices.</summary>
        public static long TierCapital(int tier, int level, int closures, int share) => At(TierLeastAt(tier, share, closures), level);

        /// <summary>What the bank's level may hold now, at today's prices and the share of the end game's scale the bank runs at.</summary>
        public static BankCaps Caps(OwnBank b, int level, int share) => CapsOf(TierOf(b), level, share);

        /// <summary>What a level may hold, at today's prices and a share of the end game's scale.</summary>
        public static BankCaps CapsOf(int tier, int level, int share)
        {
            int t = Clamp(tier, BankTiers.Community, BankTiers.National);
            return new BankCaps
            {
                Loans = Limit(BankTiers.LoansCents[t], BankTiers.LoansCentsScale[t], level, share),
                Deposits = Limit(BankTiers.DepositsCents[t], BankTiers.DepositsCentsScale[t], level, share),
                Branches = Math.Min(BankTiers.BranchesMost[t], BranchCatalogue.BranchesMost)
            };
        }

        private static long Limit(long before, long scale, int level, int share)
        {
            return before == BankTiers.NoLimit || scale == BankTiers.NoLimit ? BankTiers.NoLimit : At(EndScaleRules.Mix(before, scale, share), level);
        }

        /// <summary>The loans the level still lets the bank make: nothing while its book (performing and in arrears) is at its most or over it.</summary>
        public static long LoanRoom(OwnBank b, in BankCaps caps)
        {
            return caps.Loans == BankTiers.NoLimit ? BankTiers.NoLimit : Math.Max(0, caps.Loans - b.Lent - b.InArrears);
        }

        /// <summary>The savers' money the level still lets the bank take: nothing while its deposits are at its most or over it.</summary>
        public static long DepositRoom(OwnBank b, in BankCaps caps)
        {
            return caps.Deposits == BankTiers.NoLimit ? BankTiers.NoLimit : Math.Max(0, caps.Deposits - b.Savings);
        }

        /// <summary>True while one more branch may open as the bank's: fewer than its level's count are taken in.</summary>
        public static bool BranchRoom(OwnBank b) => b.Branches.Count < Math.Min(BankTiers.BranchesMost[TierOf(b)], BranchCatalogue.BranchesMost);

        /// <summary>Whole game months the bank has held its level without a breach: from founding, its last move up or its last day in breach.</summary>
        public static int MonthsAtTier(OwnBank b, int today, int monthDays)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(0L, ((long)today - b.TierFrom) / Math.Max(1, monthDays)));
        }

        /// <summary>The level a bank moves up to next; BankTiers.National for one at the top.</summary>
        public static int NextTier(OwnBank b) => Math.Min(BankTiers.National, TierOf(b) + 1);

        /// <summary>
        /// Why the bank may not move up a level now, as a reason key; null if it may. It must run, be below the top, be
        /// clear of the regulator and of any run, have held its level for the next level's record of months without a breach,
        /// and hold the next level's least capital (at the price of what is bought now: <paramref name="share"/>). The licence
        /// is the director's to take.
        /// </summary>
        public static string MoveUpWhy(OwnBank b, int today, int monthDays, int level, int share)
        {
            if (!b.Running)
            {
                return "reallife_ob_why_none";
            }
            int t = TierOf(b);
            if (t >= BankTiers.National)
            {
                return UpTopKey;
            }
            if (b.Stage != Regulator.Clear || b.Run)
            {
                return UpRegulatorKey;
            }
            if (MonthsAtTier(b, today, monthDays) < BankTiers.RecordMonths[t + 1])
            {
                return UpRecordKey;
            }
            return CapitalNow(b) < TierCapital(t + 1, level, b.Closures, share) ? UpCapitalKey : null;
        }

        /// <summary>The bank moved up a level: its record begins again, and from now it keeps the new level's least capital.</summary>
        public static void MoveUp(OwnBank b, int today, int share)
        {
            int t = NextTier(b);
            b.Tier = t;
            b.TierFrom = today;
            b.FoundedLeast = TierLeastAt(t, share, b.Closures);
        }

        /// <summary>
        /// An older save's bank, founded before there were levels, put at the level its capital meets on the terms it was
        /// founded on (its least capital tells the end game's share it was founded at, and its closures): a bank whose capital
        /// still meets the least it was founded under is a national bank, which is what it paid for; one whose capital has
        /// fallen below is put at the highest level it meets, and from then keeps that level's least. Either way it keeps
        /// every saver, loan and branch it has, even past its level's limits: they only stop it taking more. Its record
        /// counts from its founding. Returns the level.
        /// </summary>
        public static int FitTier(OwnBank b, int level)
        {
            b.TierFrom = b.FoundedDay;
            if (!b.Running)
            {
                b.Tier = BankTiers.Community;   // a bank sold or closed: the next one is founded at the first level anyway
                return b.Tier;
            }
            int closures = Math.Max(0, b.Closures);
            long founded = b.FoundedLeast > 0 ? b.FoundedLeast : OwnBankCatalogue.CapitalLeastCents;
            long capital = CapitalNow(b);
            if (capital >= At(founded, level))
            {
                b.Tier = BankTiers.National;
                return b.Tier;
            }
            int share = FoundedShare(founded, closures);
            int t = BankTiers.Community;
            for (int k = BankTiers.Regional; k < BankTiers.National; k++)
            {
                if (capital >= At(TierLeastAt(k, share, closures), level))
                {
                    t = k;
                }
            }
            b.Tier = t;
            b.FoundedLeast = TierLeastAt(t, share, closures);
            return t;
        }

        /// <summary>The end game's share a bank founded under this least capital (start's prices, closures counted) was founded at, thousandths.</summary>
        public static int FoundedShare(long foundedLeast, int closures)
        {
            long before = OwnBankCatalogue.CapitalLeastCents, scale = OwnBankCatalogue.CapitalLeastCentsScale;
            long least = foundedLeast * 1000 / (1000 + Math.Max(0, closures) * OwnBankCatalogue.AgainCapital);
            if (least <= before)
            {
                return 0;
            }
            return (int)Math.Min(EndScaleRules.Full, (least - before) * EndScaleRules.Full / (scale - before));
        }

        /// <summary>The capital the bank keeps whatever it pays out: its level's least, and its loans' and fund's weight at the lending line.</summary>
        public static long Keep(OwnBank b, int level)
        {
            return Math.Max(At(b.FoundedLeast > 0 ? b.FoundedLeast : OwnBankCatalogue.CapitalLeastCents, level), RiskWeighted(b) * OwnBankCatalogue.LendRatio / 1000);
        }

        /// <summary>
        /// The owner's pay out of a day's profit: the share chosen of it, or the fixed sum but never more than it; nothing on a
        /// day without profit, in a breach or a run, or for a test's bank; never past what the bank can spare (its cash over
        /// the reserve, its capital over what it keeps). The rest of the profit stays in the capital.
        /// </summary>
        public static long OwnerPay(OwnBank b, long profit, int level)
        {
            if (!b.Running || b.Test || b.Run || b.Stage == Regulator.Breach || profit <= 0)
            {
                return 0;
            }
            long wanted = b.PayMode == BankTiers.PayFixedMode
                ? Math.Min(Math.Max(0, b.PayFixed), profit)
                : profit * Clamp(b.PayShare, 0, BankTiers.PayShareMost) / BankTiers.PayShareMost;
            long room = Math.Min(b.Reserves - ReserveNeed(b), CapitalNow(b) - Keep(b, level));
            return Math.Max(0, Math.Min(wanted, room));
        }

        /// <summary>A share of the profit for the owner's pay, on its step and within its bounds.</summary>
        public static int PayShareStep(int share)
        {
            int s = Clamp(share, 0, BankTiers.PayShareMost);
            return s / BankTiers.PayStep * BankTiers.PayStep;
        }
    }
}
