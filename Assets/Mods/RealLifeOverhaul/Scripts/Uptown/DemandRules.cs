using System;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The district's market as counted from its standing buildings: units and vacant units of each use, and their traffic.</summary>
    public struct DistrictMarket
    {
        public int[] Units;    // by use (Construction's BuildUse order: homes, shops, offices, warehouses)
        public int[] Vacant;   // for rent with nobody in, by use
        public int Traffic;    // the buildings' average traffic index, 0 to 100
    }

    /// <summary>
    /// The district's demand for each use, as Cities Skylines' demand bars (plans/build-mode.md section 10; the owner, 26 Oct
    /// 2026: "Is every factor taken into account? The economical sim of the game"): homes are wanted where there are jobs and
    /// few empty homes, shops where people live and walk, offices where there are people to work, warehouses where shops need
    /// goods; every use's demand falls as its vacancies rise past the game's own share kept empty. The demand sets the district's
    /// market level, which feeds the game's own rent and price arithmetic through Uptown's record (rentMultiplierPerSqmPrice,
    /// realEstateMultiplier), moved at most a step a day within caps; the developers and the council read it through
    /// build mode's bridges (Construction's BuildModeRules.DeveloperMay and CouncilLift). Game-free (EconomyCheck's BuildModeChecks), the same on every machine for the
    /// same counts. COST: nothing per frame; a few sums once a game day.
    /// </summary>
    public static class DemandRules
    {
        public const int Uses = 4;
        public const int TargetVacancyPermille = 80;   // the share of units kept empty a market moves round (the game's own ideal share is 5 to 30 per cent)
        public const int VacancyGainTenths = 3;        // demand points per ten thousandths: 8% under the target gives 24 points more
        public const int LevelLeast = 880;             // thousandths of the record's rents and values: oversupply lowers them by an eighth at most
        public const int LevelMost = 1120;             // ... shortage raises them as much
        public const int LevelStep = 10;               // thousandths a game day the level moves at most: no market runs away in a day
        public const int DensityLiftMost = 4;          // traffic points a lot gets from a packed block

        private static readonly int[] Base = { 50, 40, 45, 35 };   // each use's demand with a balanced district

        /// <summary>The use of one of the game's building types (homes, shops, offices, warehouses), or -1.</summary>
        public static int UseOfType(string type)
        {
            switch (type)
            {
                case "ba:buildingtype_residential": return 0;
                case "ba:buildingtype_retail": return 1;
                case "ba:buildingtype_office": return 2;
                case "ba:buildingtype_warehouse": return 3;
                default: return -1;
            }
        }

        private static int Occupied(DistrictMarket m, int use)
        {
            int units = m.Units != null && use < m.Units.Length ? m.Units[use] : 0;
            int vacant = m.Vacant != null && use < m.Vacant.Length ? m.Vacant[use] : 0;
            return Math.Max(0, units - vacant);
        }

        /// <summary>Vacant units of a use, thousandths of its units; none counted is 0 (nothing to let: no oversupply).</summary>
        public static int VacancyPermille(DistrictMarket m, int use)
        {
            int units = m.Units != null && use < m.Units.Length ? m.Units[use] : 0;
            int vacant = m.Vacant != null && use < m.Vacant.Length ? m.Vacant[use] : 0;
            return units <= 0 ? 0 : (int)(1000L * Math.Min(vacant, units) / units);
        }

        // -100 to 100: how far what a use serves outgrows it (each use's ratio the plan's mix of Manhattan: about 4 homes a shop, 4 shops an office's worth of people).
        private static int Pull(int serving, int served)
        {
            int all = serving + served;
            return all <= 0 ? 0 : (int)(100L * (serving - served) / all);
        }

        /// <summary>A use's demand, 0 to 100.</summary>
        public static int Demand(DistrictMarket m, int use)
        {
            if (use < 0 || use >= Uses)
            {
                return 0;
            }
            int homes = Occupied(m, 0), shops = Occupied(m, 1), offices = Occupied(m, 2), warehouses = Occupied(m, 3);
            int pull;
            switch (use)
            {
                case 0: pull = Pull((shops + offices * 3 + warehouses * 2) * 2, homes) * 4 / 10; break;   // jobs for the people of the homes
                case 1: pull = Pull(homes, shops * 4) * 3 / 10 + (Math.Max(0, Math.Min(100, m.Traffic)) - 50) / 5; break;   // people living and walking past
                case 2: pull = Pull(homes, offices * 12) * 35 / 100; break;   // people to work there
                default: pull = Pull(shops, warehouses * 8) * 35 / 100; break;   // shops needing goods
            }
            int vacancy = (TargetVacancyPermille - VacancyPermille(m, use)) * VacancyGainTenths / 10;
            return Math.Max(0, Math.Min(100, Base[use] + pull + vacancy));
        }

        /// <summary>The rents' and values' level a use's demand asks for, thousandths: 1000 at a demand of 50, within the caps.</summary>
        public static int Level(int demand)
        {
            int level = 1000 + (Math.Max(0, Math.Min(100, demand)) - 50) * (LevelMost - 1000) / 50;
            return Math.Max(LevelLeast, Math.Min(LevelMost, level));
        }

        /// <summary>The district's level: each use's level weighed by its units (an even weight with none counted).</summary>
        public static int DistrictLevel(DistrictMarket m)
        {
            long sum = 0, weight = 0;
            for (int use = 0; use < Uses; use++)
            {
                int units = m.Units != null && use < m.Units.Length ? Math.Max(0, m.Units[use]) : 0;
                int w = units > 0 ? units : 1;
                sum += (long)Level(Demand(m, use)) * w;
                weight += w;
            }
            return weight <= 0 ? 1000 : (int)(sum / weight);
        }

        /// <summary>Yesterday's level moved toward today's wanted one by one step at most.</summary>
        public static int Step(int current, int wanted)
        {
            current = current <= 0 ? 1000 : current;
            int moved = wanted > current ? Math.Min(wanted, current + LevelStep) : Math.Max(wanted, current - LevelStep);
            return Math.Max(LevelLeast, Math.Min(LevelMost, moved));
        }

        /// <summary>Traffic points a lot gets from its block's standing buildings: a packed block brings people past every door, up to the cap.</summary>
        public static int DensityLift(int built, int lots)
        {
            if (lots <= 0 || built <= 0)
            {
                return 0;
            }
            return Math.Min(DensityLiftMost, (int)((long)DensityLiftMost * Math.Min(built, lots) / lots));
        }

        /// <summary>A demand as the window's bar in the game's lettering: twenty strokes, those up to the demand in the game's green.</summary>
        public static string Bar(int demand)
        {
            int filled = Math.Max(0, Math.Min(20, (Math.Max(0, demand) + 2) / 5));
            return "<color=#5FD35F>" + new string('|', filled) + "</color><color=#5A5E64>" + new string('|', 20 - filled) + "</color>";
        }
    }
}
