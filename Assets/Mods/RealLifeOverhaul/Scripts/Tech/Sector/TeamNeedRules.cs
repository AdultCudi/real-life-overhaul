using System;
using RealLifeOverhaul.Staffing;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The roles a firm's team lines say a need for (<see cref="TeamNeedRules"/>).</summary>
    public enum NeedRole
    {
        Designers = 0, Product = 1, Sellers = 2, Reliability = 3, Security = 4, Count = 5
    }

    /// <summary>
    /// What one team line's role can give and how many people reach it: the figure now, the most it can ever be, the figures at half
    /// and at nine tenths of the way to it with the people each takes, and what one more person adds now.
    /// </summary>
    public struct TeamNeed
    {
        public int Role;                    // NeedRole
        public int Have;                    // people of the role now
        public long Amount;                 // what they bring now: work points a day, or the posture's thousandths
        public long Each;                   // what one more brings: the people's average, or a middle-level person's with none (never 0)
        public int Now;                     // the figure now, thousandths (a lift, or a posture)
        public int Top;                     // the most it can be, thousandths: never quite reached
        public int HalfFigure, FullFigure;  // the figure at half and at nine tenths of the way from nothing (or the base) to the top
        public int HalfPeople, FullPeople;  // people in all for each (never under the people now)
        public int More;                    // people to add for the nine tenths; 0 once there
        public int EachFigure;              // thousandths one more person adds now
        public bool Past;                   // the nine tenths is reached
    }

    /// <summary>The support people a firm's customers need against those it has: a ceiling, so an exact number.</summary>
    public struct SupportNeed
    {
        public int Have;
        public long Need;
        public long More, Spare;
    }

    /// <summary>The testers that keep up with the bugs the builders make.</summary>
    public struct TesterNeed
    {
        public int Have;
        public long Made, Fixed;            // bugs a day the builders make, and the share they fix as they go
        public long Points;                 // tester points a day that fix the rest
        public int People;                  // testers in all that give those points
        public long More;                   // testers to add; 0 when the testers keep up
    }

    /// <summary>
    /// The team lines' "how many for the most" (a player, 25 Nov 2026: "it would be helpful if the game explicitly stated the exact
    /// amount of manpower required to reach 100%"). Every figure a team gives grows by a curve that never quite reaches its top
    /// (<see cref="SectorRules.Lift"/>: half the gain at the amount that is its half, nearer the gain the more there is), so there is
    /// no head count for exactly 100%. The line says the two it can say exactly: the people for half of the way to the top and for nine
    /// tenths of it (<see cref="SectorCatalogue.NeedHalf"/>, <see cref="SectorCatalogue.NeedFull"/>), each worked out as the smallest
    /// amount whose figure is at least that, by the curve turned round (<see cref="AmountFor"/>), and what one more person adds now.
    /// Support is a true ceiling (the people its tickets need), and builders have none. Game-free; EconomyCheck's TechPlanChecks hold each
    /// figure to the rule that gives it.
    /// </summary>
    public static class TeamNeedRules
    {
        /// <summary>
        /// The least amount whose <see cref="SectorRules.Lift"/> is at least <paramref name="target"/>: ceil(target x half / (gain - target)).
        /// -1 where the target is the gain or more (a curve never reaches it); 0 for a target of nothing.
        /// </summary>
        public static long AmountFor(int target, long half, int gain)
        {
            if (target <= 0)
            {
                return 0;
            }
            if (target >= gain)
            {
                return -1;
            }
            long h = Math.Max(1L, half);
            return ((long)target * h + (gain - target) - 1) / (gain - target);
        }

        /// <summary>
        /// The product people's points that give a share of the build (their points over everybody's) of at least <paramref name="share"/> thousandths:
        /// ceil(share x engineering / (1000 - share)), one at the least.
        /// </summary>
        public static long ProductPointsFor(int share, long engineering)
        {
            if (share <= 0)
            {
                return 0;
            }
            if (share >= 1000)
            {
                return -1;
            }
            return Math.Max(1L, ((long)share * Math.Max(0L, engineering) + (1000 - share) - 1) / (1000 - share));
        }

        /// <summary>People to add so that their amount reaches a need, at what each brings: ceil(gap / each), 0 when there already.</summary>
        public static int PeopleToAdd(long have, long need, long each)
        {
            return need <= have ? 0 : (int)Math.Min(100000L, (need - have + Math.Max(1L, each) - 1) / Math.Max(1L, each));
        }

        /// <summary>What one person brings by default: a middle-level person's work points a day in good heart, for the roles whose figure is in points.</summary>
        public static long DefaultPoints => FirmsCatalogue.PointsPerDay;

        /// <summary>A security analyst's thousandths of posture at the middle level, before any certificate: what a newcomer brings.</summary>
        public static long DefaultSecurity => (long)StaffEffects.MidSkill(TalentCatalogue.Mid) * FirmsCatalogue.SecurityPerPoint;

        /// <summary>A site reliability engineer's thousandths of reliability at the middle level.</summary>
        public static long DefaultReliability => (long)StaffEffects.MidSkill(TalentCatalogue.Mid) * FirmsCatalogue.ReliabilityPerPoint;

        private static long EachOf(long amount, int people, long otherwise) => people > 0 && amount > 0 ? Math.Max(1L, (amount + people / 2) / people) : Math.Max(1L, otherwise);

        // A curve with a base: the figure is baseline + Lift(amount, half, top - baseline).
        private static TeamNeed Curve(NeedRole role, int have, long amount, long each, int baseline, int top, long half)
        {
            int gain = top - baseline;
            var n = new TeamNeed { Role = (int)role, Have = have, Amount = amount, Each = Math.Max(1L, each), Top = top };
            n.Now = baseline + SectorRules.Lift(amount, half, gain);
            long forHalf = AmountFor(gain * SectorCatalogue.NeedHalf / 1000, half, gain), forFull = AmountFor(gain * SectorCatalogue.NeedFull / 1000, half, gain);
            n.HalfFigure = baseline + gain * SectorCatalogue.NeedHalf / 1000;
            n.FullFigure = baseline + gain * SectorCatalogue.NeedFull / 1000;
            n.HalfPeople = have + PeopleToAdd(amount, forHalf, n.Each);
            n.FullPeople = have + PeopleToAdd(amount, forFull, n.Each);
            n.More = n.FullPeople - have;
            n.Past = n.More == 0;
            n.EachFigure = baseline + SectorRules.Lift(amount + n.Each, half, gain) - n.Now;
            return n;
        }

        /// <summary>The designers' lift of a product's appeal: their points over the engineers' (and one), up to <see cref="SectorCatalogue.DesignGain"/>.</summary>
        public static TeamNeed Designers(in Crew c, in SectorTeam t)
        {
            return Curve(NeedRole.Designers, t.Designers, c.Design, EachOf(c.Design, t.Designers, DefaultPoints), 0, SectorCatalogue.DesignGain, (long)c.Engineering + 1);
        }

        /// <summary>The sellers' speed of business buyers, up to <see cref="SectorCatalogue.SellingGain"/>.</summary>
        public static TeamNeed Sellers(in Crew c, in SectorTeam t)
        {
            return Curve(NeedRole.Sellers, t.Sellers, c.Selling, EachOf(c.Selling, t.Sellers, DefaultPoints), 0, SectorCatalogue.SellingGain, SectorCatalogue.SellingHalf);
        }

        /// <summary>The reliability engineers' posture: the base with nobody, towards all of it.</summary>
        public static TeamNeed Reliability(in Crew c, in SectorTeam t)
        {
            return Curve(NeedRole.Reliability, t.Reliability, c.ReliabilityBrought, EachOf(c.ReliabilityBrought, c.ReliabilityWorking, DefaultReliability), FirmsCatalogue.ReliabilityBase, 1000,
                SectorCatalogue.ReliabilityHalf);
        }

        /// <summary>The security people's posture: the base with nobody, towards all of it.</summary>
        public static TeamNeed Security(in Crew c, in SectorTeam t)
        {
            return Curve(NeedRole.Security, t.Security, c.SecurityBrought, EachOf(c.SecurityBrought, c.SecurityWorking, DefaultSecurity), FirmsCatalogue.SecurityBase, 1000,
                SectorCatalogue.SecurityHalf);
        }

        /// <summary>
        /// The product people's extra feature points: a curve of their share of the build (<see cref="SectorRules.ProductBoost"/>). The share moves with
        /// their own points, so the people for a share are worked out on the share (<see cref="ProductPointsFor"/>), not on a fixed half.
        /// </summary>
        public static TeamNeed Product(in Crew c, in SectorTeam t)
        {
            int gain = SectorCatalogue.ProductGain;
            long each = EachOf(c.Product, t.Product, DefaultPoints), build = Math.Max(0, c.Engineering);
            var n = new TeamNeed { Role = (int)NeedRole.Product, Have = t.Product, Amount = c.Product, Each = each, Top = gain };
            n.Now = SectorRules.ProductBoost(c);
            int gainHalf = gain * SectorCatalogue.NeedHalf / 1000, gainFull = gain * SectorCatalogue.NeedFull / 1000;
            long shareHalf = AmountFor(gainHalf, 100, gain), shareFull = AmountFor(gainFull, 100, gain);
            n.HalfFigure = gainHalf;
            n.FullFigure = gainFull;
            n.HalfPeople = t.Product + PeopleToAdd(c.Product, ProductPointsFor((int)shareHalf, build), each);
            n.FullPeople = t.Product + PeopleToAdd(c.Product, ProductPointsFor((int)shareFull, build), each);
            n.More = n.FullPeople - t.Product;
            n.Past = n.More == 0;
            var more = c;
            more.Product = (int)Math.Min(int.MaxValue / 4, c.Product + each);
            n.EachFigure = SectorRules.ProductBoost(more) - n.Now;
            return n;
        }

        /// <summary>The support people the customers' tickets need: exact, with no curve.</summary>
        public static SupportNeed Support(long need, int have)
        {
            long n = Math.Max(0L, need);
            return new SupportNeed { Have = have, Need = n, More = Math.Max(0L, n - have), Spare = Math.Max(0L, have - n) };
        }

        /// <summary>
        /// The testers that fix what the builders break: the builders make about their feature points x faults in bugs a day (the focus's share to
        /// features, the product people's boost on it), fix the focus's share as they go at <see cref="FirmsCatalogue.PointsPerFix"/> points a bug, and a
        /// tester's point fixes a bug. Before the firm's debt slows features (so a little over what is made).
        /// </summary>
        public static TesterNeed Testers(in Crew c, in SectorTeam t, int focus)
        {
            int[] split = FirmsCatalogue.FocusSplit[TalentCatalogue.Clamp(focus, 0, (int)Focus.Count - 1)];
            long feature = (long)Math.Max(0, c.Engineering) * split[0] / 1000 * (1000 + SectorRules.ProductBoost(c)) / 1000;
            long made = feature * Math.Max(30, c.Faults) / 1000, self = (long)Math.Max(0, c.Engineering) * split[1] / 1000 / FirmsCatalogue.PointsPerFix;
            long points = Math.Max(0L, made - self);
            long each = EachOf(c.Testing, t.Testers, DefaultPoints);
            int more = PeopleToAdd(c.Testing, points, each);
            return new TesterNeed { Have = t.Testers, Made = made, Fixed = self, Points = points, People = t.Testers + more, More = more };
        }

        /// <summary>What one more builder gives a day, points: the builders' average, or a middle-level builder's with none. Builders have no ceiling.</summary>
        public static long BuilderEach(long points, int people) => EachOf(points, people, DefaultPoints);
    }
}
