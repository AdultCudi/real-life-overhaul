using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Staffing
{
    /// <summary>
    /// What a role's number and skill do, worked out by the game's own rules (TalentRules, FirmsRules) on small made-up firms, so
    /// the reference's "a sensible number" is what the rules give and cannot drift from them. Game-free; each call is a few
    /// hundred steps of arithmetic, made once when the table is.
    /// </summary>
    public static class StaffEffects
    {
        public const int Day = 100;                   // a day number that means nothing to the rules used here
        private const int Cap = 3000;                 // days a run gives up after

        /// <summary>The skill, 0 to 100, in the middle of a level's range.</summary>
        public static int MidSkill(int level)
        {
            int l = TalentCatalogue.Clamp(level, 0, TalentCatalogue.Levels - 1);
            return (TalentCatalogue.LevelSkillLeast[l] + TalentCatalogue.LevelSkillMost[l]) / 2;
        }

        /// <summary>A professional of a role at a level and a skill, in good heart (the morale a newcomer starts with, no burnout, not sick).</summary>
        public static Profile Person(int role, int level, int skill)
        {
            return new Profile { Role = role, Level = level, A = skill * 1000, B = skill * 1000, Main = skill * 1000 };
        }

        /// <summary>The work points a professional gives in a full day, as the firm's day counts them (TechDirector.Crews): whole points.</summary>
        public static int Points(int role, int level, int skill)
        {
            Profile p = Person(role, level, skill);
            return TalentRules.Output(p, skill, Day) * FirmsCatalogue.PointsPerDay * TalentCatalogue.DayHours / TalentCatalogue.DayHours / 1000;
        }

        /// <summary>A builder's whole points a day at the middle of a level.</summary>
        public static int BuilderPoints(int level) => Points(TalentCatalogue.SoftwareEngineer, level, MidSkill(level));

        /// <summary>The bugs a builder of a skill makes for every 100 points of new work, before the focus takes its share (TalentRules.Faults).</summary>
        public static int FaultsPer100(int skill)
        {
            return TalentRules.Faults(Person(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, skill), skill) / 10;
        }

        private static Crew CrewOf(int builders, int level, int testers, int product)
        {
            int pts = BuilderPoints(level);
            int skill = MidSkill(level);
            return new Crew
            {
                Engineering = builders * pts, Testing = testers * Points(TalentCatalogue.QaTester, TalentCatalogue.Mid, MidSkill(TalentCatalogue.Mid)), Product = product,
                Faults = TalentRules.Faults(Person(TalentCatalogue.SoftwareEngineer, level, skill), skill), SeniorShare = level >= TalentCatalogue.Senior ? 1000 : 0
            };
        }

        /// <summary>The result of a run of the firm's building: days taken (Cap when never) and the product's quality and bugs at the end.</summary>
        public readonly struct Run
        {
            public readonly int Days, Quality, Bugs;

            public Run(int days, int quality, int bugs)
            {
                Days = days;
                Quality = quality;
                Bugs = bugs;
            }
        }

        /// <summary>A game of a size built by so many programmers of a level and testers, on a focus, to the day it can launch.</summary>
        public static Run GameRun(int scope, int builders, int level, int testers, int focus)
        {
            var firm = new Firm { Focus = focus };
            var game = new Product { Kind = (int)ProductKind.Game, Scope = scope };
            Crew c = CrewOf(builders, level, testers, 0);
            int days = 0;
            while (!FirmsRules.GameReady(game) && days < Cap)
            {
                FirmsRules.Build(firm, game, c, c.Engineering, c.Testing);
                days++;
            }
            return new Run(days, FirmsRules.Quality(game), game.Bugs);
        }

        /// <summary>A service's next feature (the one after so many built) by so many builders of a level, on the balanced focus: the days until it is done.</summary>
        public static int FeatureDays(int built, int builders, int level)
        {
            var firm = new Firm { Focus = (int)Focus.Balanced };
            var product = new Product { Kind = (int)ProductKind.Saas, Features = built };
            Crew c = CrewOf(builders, level, 0, 0);
            int days = 0;
            while (product.Features == built && days < Cap)
            {
                FirmsRules.Build(firm, product, c, c.Engineering, 0);
                days++;
            }
            return days;
        }

        /// <summary>Builders per product person at which product people's boost reaches its cap, in thousandths.</summary>
        public static int BuildersPerProduct => (FirmsCatalogue.ProductBoostGain - FirmsCatalogue.ProductBoostMost) * 1000 / FirmsCatalogue.ProductBoostMost;

        /// <summary>Builders per designer at which designers' lift reaches its cap (their points are this share of the builders').</summary>
        public static int BuildersPerDesigner => 1000 / FirmsCatalogue.DesignAppealMost;

        /// <summary>The bugs a product of so many features can hold before its quality has fallen by half.</summary>
        public static int BugsToHalve(int features)
        {
            var product = new Product { Kind = (int)ProductKind.Saas, Features = features };
            int bugs = 0;
            while (bugs < 100000)
            {
                product.Bugs = bugs;
                if (FirmsRules.Quality(product) <= 500)
                {
                    return bugs;
                }
                bugs++;
            }
            return bugs;
        }

        /// <summary>
        /// Builders one tester keeps up with on a focus, at the middle of a level: the bugs a builder makes beyond what their own
        /// fixing clears, over a tester's day. 0 when builders clear what they make themselves.
        /// </summary>
        public static int BuildersPerTester(int focus, int level)
        {
            int[] split = FirmsCatalogue.FocusSplit[TalentCatalogue.Clamp(focus, 0, (int)Focus.Count - 1)];
            int pts = BuilderPoints(level);
            int skill = MidSkill(level);
            int faults = Math.Max(30, TalentRules.Faults(Person(TalentCatalogue.SoftwareEngineer, level, skill), skill));
            long made = (long)pts * split[0] * faults;                      // bugs a day x 1,000,000
            long fixedBy = (long)pts * split[1] * 1000 / FirmsCatalogue.PointsPerFix;
            long gap = made - fixedBy;
            if (gap <= 0)
            {
                return 0;
            }
            long tester = (long)Points(TalentCatalogue.QaTester, TalentCatalogue.Mid, MidSkill(TalentCatalogue.Mid)) * 1000000;
            return (int)Math.Max(1, tester / gap);
        }

        /// <summary>Customers one support person answers for, a segment's tickets a day over what one person answers.</summary>
        public static long CustomersPerSupport(int segment)
        {
            return (long)FirmsCatalogue.TicketsPerPerson * 1000 / Math.Max(1, FirmsCatalogue.Tickets[TalentCatalogue.Clamp(segment, 0, 2)]);
        }

        /// <summary>People of a role at a level that reach a total of work points, rounded up.</summary>
        public static int PeopleFor(int totalPoints, int pointsEach) => pointsEach <= 0 ? 0 : (totalPoints + pointsEach - 1) / pointsEach;

        /// <summary>Site reliability or DevOps engineers of a skill it takes to reach the cap of reliability.</summary>
        public static int ReliabilityPeople(int skill)
        {
            int each = Math.Max(1, Math.Min(FirmsCatalogue.ReliabilityEach, skill * FirmsCatalogue.ReliabilityPerPoint));
            return PeopleFor(FirmsCatalogue.ReliabilityMost - FirmsCatalogue.ReliabilityBase, each);
        }

        /// <summary>Security analysts of a skill it takes to reach the cap of the security posture, with a data protection officer counted first.</summary>
        public static int SecurityPeople(int skill)
        {
            int each = Math.Max(1, Math.Min(FirmsCatalogue.SecurityEach, skill * FirmsCatalogue.SecurityPerPoint));
            return PeopleFor(FirmsCatalogue.SecurityMost - FirmsCatalogue.SecurityBase - FirmsCatalogue.SecurityOfficer, each);
        }

        /// <summary>Whole points of selling work a day at which the sellers' boost reaches its cap, for a boost of so many thousandths a point.</summary>
        public static int SellingToCap(int most, int perPoint) => (most + perPoint - 1) / Math.Max(1, perPoint);
    }
}
