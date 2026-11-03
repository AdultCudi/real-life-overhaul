using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, the attention (DRAFT2.md section 4.11: "Flare's effect leaves the shared plus 15% cap and becomes its own channel,
    /// through the residents"): what residents heard of a business becomes the share of its customers who heard (each district weighed by the money
    /// its residents bring the business's district for its kind), and that share draws people to it from its rivals in the same pools
    /// (PopulationAttention: a pool is only ever shared out, so nobody is made from nothing). Measured the day after: the takings and visits the
    /// attention brought, worked out from the pools' own weights with and without Flare's part, and shared among the posts and ads that made it.
    /// Game-free. COST: a few dozen operations a business, once a day.
    /// </summary>
    public static partial class FlareCityRules
    {
        /// <summary>
        /// The money each district's residents bring each district for each kind, from the day's pools (home lines: the residents as their homes
        /// are counted), by district of the business, kind and district of origin; with no pools yet, a district's own residents alone.
        /// </summary>
        public static void OriginWeights(PopulationState s, PoolRules.DemandDay dd, int districts, long[] into)
        {
            int n = Math.Max(1, districts);
            Array.Clear(into, 0, Math.Min(into.Length, n * Kinds * n));
            bool pools = dd != null && dd.Day >= 0 && dd.Districts == n && dd.RouteCum.Length >= PoolRules.LineKinds * n * Kinds * (n + 1);
            for (int db = 0; db < n; db++)
            {
                for (int k = 0; k < Kinds; k++)
                {
                    int row = (db * Kinds + k) * n;
                    if (!pools)
                    {
                        into[row + db] = 1;
                        continue;
                    }
                    for (int o = 0; o < n; o++)
                    {
                        int r = dd.RouteIndex(PoolRules.Home, o, k);
                        long total = dd.RouteCum[r + n];
                        long part = dd.RouteCum[r + db + 1] - dd.RouteCum[r + db];
                        if (total <= 0 || part <= 0)
                        {
                            continue;
                        }
                        long money = 0;
                        for (int c = 0; c < PopulationCatalogue.Classes; c++)
                        {
                            money += dd.LineMoney[dd.LineIndex(PoolRules.Home, o, c, k)];
                        }
                        into[row + o] = money / 1000 * part / total + (money > 0 ? 1 : 0);
                    }
                    long any = 0;
                    for (int o = 0; o < n; o++)
                    {
                        any += into[row + o];
                    }
                    if (any == 0)
                    {
                        into[row + db] = 1;   // nobody's money reached it yesterday: its own district's residents stand for its customers
                    }
                }
            }
        }

        /// <summary>
        /// A business's attention from what residents heard, thousandths: the share of its customers' districts who heard good things times
        /// <see cref="FlareCityCatalogue.PositiveLift"/>, less the share who heard bad things times <see cref="FlareCityCatalogue.NegativeLift"/>.
        /// </summary>
        public static int AttentionOf(FlareCityState c, PopulationState s, int b, long[] weights)
        {
            if (b < 0 || b >= c.BusinessCount || b >= s.Businesses.Count || !Sells(s.Businesses, b))
            {
                return 0;
            }
            int n = c.Districts, db = s.Businesses.District[b], k = s.Businesses.Kind[b];
            if (db >= n)
            {
                return 0;
            }
            int row = (db * Kinds + k) * n;
            long weight = 0, good = 0, bad = 0;
            for (int o = 0; o < n && o < c.Adults.Length; o++)
            {
                long w = weights[row + o];
                int adults = c.Adults[o];
                if (w <= 0 || adults <= 0)
                {
                    continue;
                }
                weight += w;
                good += w * c.Heard[b * n + o] / adults;   // thousandths of the district's adults who heard
                bad += w * c.Sour[b * n + o] / adults;
            }
            if (weight <= 0)
            {
                return 0;
            }
            long heard = good / weight, sour = bad / weight;   // per mille of its customers' residents
            long attention = (heard * FlareCityCatalogue.PositiveLift - sour * FlareCityCatalogue.NegativeLift) / 1000;
            int at = (int)Math.Max(FlareCityCatalogue.AttentionLeast, Math.Min(FlareCityCatalogue.AttentionMost, attention));
            int pull = FlareUseRules.StarPull(FlareUseRules.StarsOf(c.StarRows, s.Businesses.Key[b]));
            if (pull != 0)
            {
                at += pull;
                if (at < FlareCityCatalogue.AttentionLeast)
                {
                    at = FlareCityCatalogue.AttentionLeast;
                }
                if (at > FlareCityCatalogue.AttentionMost)
                {
                    at = FlareCityCatalogue.AttentionMost;
                }
            }
            return at;
        }

        /// <summary>Every business's attention for the population's next day.</summary>
        public static void AttentionAll(FlareCityState c, PopulationState s, long[] weights)
        {
            int n = Math.Min(c.BusinessCount, s.Businesses.Count);
            for (int b = 0; b < n; b++)
            {
                c.Attention[b] = AttentionOf(c, s, b, weights);
            }
        }

        /// <summary>
        /// What Flare's attention did on the day the population last lived, for each business: the takings it took with the attention given that day
        /// against what its share of the same pools would have been without Flare's part (the day's own share; yesterday's habit, already in the
        /// weights, stays), in cents, and the visits that is at a customer's basket.
        /// </summary>
        public static void MeasureLift(FlareCityState c, PopulationState s, PoolRules.DemandDay dd)
        {
            int n = Math.Min(c.BusinessCount, s.Businesses.Count);
            BusinessTable bt = s.Businesses;
            bool exact = c.HabitFor >= 0 && c.HabitBefore.Length >= n * PopulationCatalogue.Classes;
            var moneyOut = new long[PopulationCatalogue.Classes];
            var scratch = new long[(dd != null ? dd.Districts : 1) + 1];
            for (int b = 0; b < n; b++)
            {
                c.Lift[b] = 0;
                c.LiftVisits[b] = 0;
                int given = c.Given[b];
                if (given == 0 || dd == null || dd.Day < 0 || !Sells(bt, b) || bt.District[b] >= dd.Districts)
                {
                    continue;
                }
                CounterMoney(s, dd, b, given, moneyOut, scratch);
                long lift = exact ? LiftExact(c, bt, dd, b, given, moneyOut) : LiftOf(bt, dd, b, given, moneyOut);
                c.Lift[b] = lift;
                long basket = (long)PopulationCatalogue.BasketOfType(bt.Type[b]) * 100;
                c.LiftVisits[b] = (int)(lift / Math.Max(1, basket));
            }
            c.HabitFor = -1;   // the snapshot is the day's: the next is taken as the population asks again
        }

        /// <summary>The pools' habit shares as they stand before the population's day (the attention source takes it as the population asks).</summary>
        public static void TakeHabit(FlareCityState c, BusinessTable bt, int lastLived)
        {
            int n = Math.Min(c.BusinessCount, bt.Count) * PopulationCatalogue.Classes;
            if (c.HabitBefore.Length < n || bt.Habit.Length < n)
            {
                return;
            }
            Array.Copy(bt.Habit, c.HabitBefore, n);
            c.HabitFor = lastLived;
        }

        /// <summary>
        /// One business's lift, cents, worked out exactly from the pools: each business's own share of its pool recovered from its weight and the
        /// habit share it came in with (the population blends the two by its kind's habit), the business's own share taken back to what it is
        /// without Flare's attention and the pool's shares made whole again, then its share of each class's money with the attention, less without.
        /// </summary>
        public static long LiftExact(FlareCityState c, BusinessTable bt, PoolRules.DemandDay dd, int b, int given, long[] moneyOut)
        {
            int db = bt.District[b], k = bt.Kind[b];
            System.Collections.Generic.List<int> pool = dd.PoolBusinesses[dd.PoolIndex(db, k)];
            int all = bt.Attention[b];
            long with = 1000 + PopulationCatalogue.Clamp(all, PopulationCatalogue.AttentionLeast, PopulationCatalogue.AttentionMost);
            long without = 1000 + PopulationCatalogue.Clamp(all - given, PopulationCatalogue.AttentionLeast, PopulationCatalogue.AttentionMost);
            long lift = 0;
            const int C = PopulationCatalogue.Classes;
            for (int cls = 0; cls < C; cls++)
            {
                long total = dd.WeightTotal[dd.TotalIndex(db, cls, k)];
                long w = dd.Weight[b * C + cls];
                if (total <= 0 || w <= 0)
                {
                    continue;
                }
                long money = 0;
                for (int line = 0; line < PoolRules.LineKinds; line++)
                {
                    money += dd.SubPool[dd.LineIndex(line, db, cls, k)];
                }
                if (money <= 0)
                {
                    continue;
                }
                long past = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    past += c.HabitBefore[pool[i] * C + cls];
                }
                int h = past > 0 ? PopulationCatalogue.Habit[k] : 0;
                // each business's own share (millionths of the pool) from its blended weight: w = (own * (1000 - h) + before * h) / 1000
                long ownSum = 0, ownB = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    int j = pool[i];
                    long own = Own(dd.Weight[j * C + cls], c.HabitBefore[j * C + cls], past, h);
                    ownSum += own;
                    if (j == b)
                    {
                        ownB = own;
                    }
                }
                long ownBOut = ownB * without / Math.Max(1, with);
                long norm = Math.Max(1, ownSum - ownB + ownBOut);
                long totalOut = 0, wOut = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    int j = pool[i];
                    long own = j == b ? ownBOut : Own(dd.Weight[j * C + cls], c.HabitBefore[j * C + cls], past, h);
                    long share = own * ownSum / norm;
                    long before = past > 0 ? (long)c.HabitBefore[j * C + cls] * PoolRules.WeightScale / past : 0;
                    long wj = (share * (1000 - h) + before * h) / 1000;
                    totalOut += wj;
                    if (j == b)
                    {
                        wOut = wj;
                    }
                }
                lift += money * w / total - (totalOut > 0 ? moneyOut[cls] * wOut / totalOut : 0);
            }
            return lift;
        }

        /// <summary>
        /// The money each class would have brought the business's district for its kind without Flare's attention on the business: a district's pull
        /// for a kind counts its businesses' attention (PoolRules.Prepare), so the day's lines are routed again with the business's part taken out.
        /// </summary>
        public static void CounterMoney(PopulationState s, PoolRules.DemandDay dd, int b, int given, long[] moneyOut, long[] scratch)
        {
            BusinessTable bt = s.Businesses;
            int db = bt.District[b], k = bt.Kind[b], n = dd.Districts;
            Array.Clear(moneyOut, 0, moneyOut.Length);
            int all = bt.Attention[b];
            long room = (long)bt.Room[b] * PoolRules.HoursFactor(bt.Hours[b]) / 1000;
            long withPull = room * PoolRules.AttentionTerm(all) / 1000;
            long withoutPull = room * PoolRules.AttentionTerm(all - given) / 1000;
            int pi = dd.PoolIndex(db, k);
            long kept = dd.Pull[pi];
            dd.Pull[pi] = Math.Max(0, kept - withPull + withoutPull);
            try
            {
                for (int line = 0; line < PoolRules.LineKinds; line++)
                {
                    for (int o = 0; o < n; o++)
                    {
                        long total = PoolRules.RouteWeights(s, dd, line, o, k, scratch);
                        long part = scratch[db];
                        if (total <= 0 || part <= 0)
                        {
                            continue;
                        }
                        for (int cls = 0; cls < moneyOut.Length; cls++)
                        {
                            long money = dd.LineMoney[dd.LineIndex(line, o, cls, k)];
                            if (money > 0)
                            {
                                moneyOut[cls] += PoolRules.CumShare(money, 0, part, total);
                            }
                        }
                    }
                }
            }
            finally
            {
                dd.Pull[pi] = kept;   // the day's pools are left as they were
            }
        }

        // A business's own share of its pool from its blended weight and the habit share it came in with.
        private static long Own(long weight, int habit, long past, int h)
        {
            if (h <= 0)
            {
                return weight;
            }
            long before = past > 0 ? (long)habit * PoolRules.WeightScale / past : 0;
            return Math.Max(0, (weight * 1000 - before * h) / Math.Max(1, 1000 - h));
        }

        /// <summary>One business's lift, cents, when the habit shares it came in with are not known (the first day after a load): the business's own share alone moved.</summary>
        public static long LiftOf(BusinessTable bt, PoolRules.DemandDay dd, int b, int given, long[] moneyOut)
        {
            int db = bt.District[b], k = bt.Kind[b];
            int all = bt.Attention[b];
            int habit = PopulationCatalogue.Habit[k];
            long with = 1000 + PopulationCatalogue.Clamp(all, PopulationCatalogue.AttentionLeast, PopulationCatalogue.AttentionMost);
            long without = 1000 + PopulationCatalogue.Clamp(all - given, PopulationCatalogue.AttentionLeast, PopulationCatalogue.AttentionMost);
            long lift = 0;
            for (int cls = 0; cls < PopulationCatalogue.Classes; cls++)
            {
                long total = dd.WeightTotal[dd.TotalIndex(db, cls, k)];
                long w = dd.Weight[b * PopulationCatalogue.Classes + cls];
                if (total <= 0 || w <= 0)
                {
                    continue;
                }
                long money = 0;
                for (int line = 0; line < PoolRules.LineKinds; line++)
                {
                    money += dd.SubPool[dd.LineIndex(line, db, cls, k)];
                }
                if (money <= 0)
                {
                    continue;
                }
                long own = w * (1000 - habit) / 1000;                 // the day's own share in the weight; the habit part is yesterday's
                long wOut = w - own + own * without / Math.Max(1, with);
                long withShare = money * w / total;
                long withoutShare = moneyOut[cls] * wOut / Math.Max(1, total - w + wOut);
                lift += withShare - withoutShare;
            }
            return lift;
        }

        /// <summary>
        /// A day's lift shared among the player's posts and ads that made the talk about each business: each its live impressions' share of what the
        /// residents heard of the business (the rest is the city's own talk: reviews, others' posts). A lift that kept people away is not a post's.
        /// </summary>
        public static void Apportion(FlareCityState c, PopulationState s, Func<string, int> businessOf)
        {
            for (int i = 0; i < c.Funnels.Count; i++)
            {
                FlareFunnel f = c.Funnels[i];
                if (f.Place == null || f.Live <= 0)
                {
                    continue;
                }
                int b = businessOf(f.Place);
                if (b < 0 || b >= c.BusinessCount || c.Lift[b] <= 0)
                {
                    continue;
                }
                long heard = 0, live = 0;
                for (int d = 0; d < c.Districts; d++)
                {
                    heard += c.Heard[b * c.Districts + d];
                }
                for (int j = 0; j < c.Funnels.Count; j++)
                {
                    if (c.Funnels[j].Live > 0 && c.Funnels[j].Place == f.Place)
                    {
                        live += c.Funnels[j].Live;
                    }
                }
                long of = Math.Max(1, Math.Max(heard, live));
                f.Spent += c.Lift[b] * f.Live / of;
                f.Came += (int)((long)c.LiftVisits[b] * f.Live / of);
            }
        }

        /// <summary>The talk fades: what residents heard of each business, and each post's live impressions with it.</summary>
        public static void Decay(FlareCityState c)
        {
            int n = c.Districts;
            for (int b = 0; b < c.BusinessCount; b++)
            {
                int keepSour = c.Answered[b] ? FlareCityCatalogue.SourAnswered : FlareCityCatalogue.SourKeep;
                bool any = false;
                for (int d = 0; d < n; d++)
                {
                    int at = b * n + d;
                    c.Heard[at] = (int)((long)c.Heard[at] * FlareCityCatalogue.HeardKeep / 1000);
                    c.Sour[at] = (int)((long)c.Sour[at] * keepSour / 1000);
                    any |= c.Sour[at] > 0;
                }
                if (!any)
                {
                    c.Answered[b] = false;
                }
                c.RatingSum[b] = c.RatingSum[b] * FlareCityCatalogue.RatingKeep / 1000;
                c.RatingCount[b] = c.RatingCount[b] * FlareCityCatalogue.RatingKeep / 1000;
            }
            for (int i = 0; i < c.Funnels.Count; i++)
            {
                c.Funnels[i].Live = (int)((long)c.Funnels[i].Live * FlareCityCatalogue.HeardKeep / 1000);
            }
        }

        /// <summary>Residents who heard good things of a business, and bad, in all (thousandths of a resident: the page's "people talking").</summary>
        public static void Talk(FlareCityState c, int b, out long heard, out long sour)
        {
            heard = sour = 0;
            if (b < 0 || b >= c.BusinessCount)
            {
                return;
            }
            for (int d = 0; d < c.Districts; d++)
            {
                heard += c.Heard[b * c.Districts + d];
                sour += c.Sour[b * c.Districts + d];
            }
        }

        /// <summary>A crisis answered (an honest reply, an apology): what put people off fades faster from now on.</summary>
        public static bool Answer(FlareCityState c, int b)
        {
            if (b < 0 || b >= c.BusinessCount)
            {
                return false;
            }
            Talk(c, b, out _, out long sour);
            if (sour <= 0)
            {
                return false;
            }
            c.Answered[b] = true;
            return true;
        }
    }
}
