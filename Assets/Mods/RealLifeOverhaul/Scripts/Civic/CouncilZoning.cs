using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The council's planning business, game-free: the one council of the city (the mayor's five) hears a change of zoning
    /// for a lot (a builder's application) or for a whole class of the new district's lots (a motion of the player's in
    /// office), each member for it by how well the use suits the land, the building code in force, what the city wants now
    /// (growth against living), the mayor's line and the applicant's standing. Without the player on the council it sits
    /// for planning on a Thursday; with them, at their meetings. Every draw is seeded by the government's seed, so a vote is
    /// the same on every machine.
    /// </summary>
    public static class CouncilZoning
    {
        public const int SittingWeekday = 3;     // Thursday (day 1 is a Monday): the council's planning sitting while the player is not on it
        public const int SittingDaysLeast = 3;   // days: an application is heard at a sitting this many days on at least
        public const int AgendaMost = 8;         // zoning matters on the player's agenda at once, beside its policy motions
        public const float PermitsStep = 0.06f;  // a member's odds a step of the permits policy, towards building
        public const float CityWant = 0.15f;     // ...for the city wanting growth more than living (the issues' weights, 0 to 1 each)
        public const float MayorLine = 0.15f;    // ...for a member of the mayor's party, the mayor's line on permits
        public const float LeastOdds = 0.05f, MostOdds = 0.95f;

        /// <summary>The council's planning sitting that hears an application made on a day: a Thursday, at least a few days on.</summary>
        public static int SittingDay(int day)
        {
            int d = day + SittingDaysLeast;
            while (((d - 1) % 7 + 7) % 7 != SittingWeekday)
            {
                d++;
            }
            return d;
        }

        /// <summary>
        /// One member's chance of voting for a change: to allow a use (<paramref name="add"/>), the planners' view of how well it
        /// suits the land (thousandths), towards building by the permits policy and the city's wish for growth, the mayor's
        /// line for their own party's members, and the applicant's standing (0 to 100; 50 for none); to stop allowing it, the
        /// other way round, and standing counts for nothing.
        /// </summary>
        public static float MemberOdds(int suitPermille, bool add, int permits, float growth, float living, int mayorLine, bool mayorsParty, int reputation)
        {
            float suit = Math.Max(0, Math.Min(1000, suitPermille)) / 1000f;
            float lean = PermitsStep * Math.Sign(permits) + CityWant * (Clamp01(growth) - Clamp01(living)) + (mayorsParty ? MayorLine * Math.Sign(mayorLine) : 0f);
            float odds = add ? suit + lean + (Math.Max(0, Math.Min(100, reputation)) - 50) / 500f : 1f - suit - lean;
            return odds < LeastOdds ? LeastOdds : odds > MostOdds ? MostOdds : odds;
        }

        /// <summary>The draw for one seat's vote on a matter: the same for the same save, day, seat and matter.</summary>
        public static float Roll(int seed, int day, int seat, int matter)
        {
            var random = new CivicRandom(CivicRandom.Mix(seed, day * 31 + seat, matter + 1301));
            return random.Value;
        }

        /// <summary>A matter's own number for the draws: the lot's id or the class, and the use and the way asked.</summary>
        public static int Matter(string lot, int lotClass, int use, bool add)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; lot != null && i < lot.Length; i++)
                {
                    h = (h ^ lot[i]) * 16777619u;
                }
                h = (h ^ (uint)(lotClass + 17)) * 16777619u;
                h = (h ^ (uint)(use * 2 + (add ? 1 : 0))) * 16777619u;
                return (int)(h & 0x3FFFFFFF);
            }
        }

        /// <summary>The mayor's line on building: +1 for fast-track permits promised or in force, -1 for strict ones, else 0.</summary>
        public static int Line(Candidate mayor, int permitsNow)
        {
            int up = CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Permits, 1)), down = CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Permits, -1));
            for (int i = 0; mayor != null && i < mayor.Planks; i++)
            {
                if (mayor.Platform[i] == up)
                {
                    return 1;
                }
                if (mayor.Platform[i] == down)
                {
                    return -1;
                }
            }
            return Math.Sign(permitsNow);
        }

        /// <summary>
        /// How much a district's voters care about the building code against the city (0.7 to 1.3 as any issue): the new
        /// district, whose people came for what is being built there, leans to growth; another district takes its own lean.
        /// </summary>
        public static float BuildingLean(bool newDistrict, int issue, float lean)
        {
            if (!newDistrict)
            {
                return lean;
            }
            return issue == (int)Issue.Growth ? Math.Max(lean, NewDistrictGrowth) : issue == (int)Issue.Living ? Math.Min(lean, NewDistrictLiving) : lean;
        }

        public const float NewDistrictGrowth = 1.25f;   // the new district's least care for growth
        public const float NewDistrictLiving = 0.85f;   // ...and its most for the neighbours' quiet

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
