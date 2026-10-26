using System;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The district's market as counted from its standing buildings: units and vacant units of each use, and their traffic.</summary>
    public struct DistrictMarket
    {
        public int[] Units;    // by use (Construction's BuildUse order: homes, shops, offices, warehouses)
        public int[] Vacant;   // for rent with nobody in, by use
        public int[] Filling;  // of those, in a building finished so lately its units are still being let (not oversupply)
        public int[] Normal;   // thousandths of each use's units the game keeps free to let in the district (its ideal share), or none: the target
        public int Traffic;    // the buildings' average traffic index, 0 to 100
    }

    /// <summary>
    /// The district's demand for each use, as Cities Skylines' demand bars (plans/build-mode.md section 10; the owner, 26 Oct
    /// 2026: "Is every factor taken into account? The economical sim of the game"): homes are wanted where there are jobs and
    /// few empty homes, shops where people live and walk, offices where there are people to work, warehouses where shops need
    /// goods; every use's demand falls as its settled vacancies rise past the share the game keeps free to let there (Uptown's
    /// third, MixRules.ToLetShare), a new building's units still being let left out. The demand sets the district's
    /// market level, which feeds the game's own rent and price arithmetic through Uptown's record (rentMultiplierPerSqmPrice,
    /// realEstateMultiplier), moved at most a step a day within caps; the developers and the council read it through
    /// build mode's bridges (Construction's BuildModeRules.DeveloperMay and CouncilLift). Game-free (EconomyCheck's BuildModeChecks), the same on every machine for the
    /// same counts. COST: nothing per frame; a few sums once a game day.
    /// </summary>
    public static class DemandRules
    {
        public const int Uses = 4;
        public const int TargetVacancyPermille = 80;   // the share of units kept empty a market moves round where the game gives none for the district (its ideal shares are 5 to 30 per cent)
        public const int NormalBand = 30;              // thousandths either side of the normal share that read as a balanced market: only a gentle lean
        public const int BandGainTenths = 1;           // demand points per ten thousandths inside that band
        public const int VacancyGainTenths = 3;        // demand points per ten thousandths past the band: 8% over it takes 24 points off
        public const int FillingDays = 30;             // days a finished building's empty units count as letting, not as oversupply
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

        /// <summary>The game's building type of a use (homes, shops, offices, warehouses), or null.</summary>
        public static string TypeOfUse(int use)
        {
            switch (use)
            {
                case 0: return "ba:buildingtype_residential";
                case 1: return "ba:buildingtype_retail";
                case 2: return "ba:buildingtype_office";
                case 3: return "ba:buildingtype_warehouse";
                default: return null;
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

        /// <summary>The share of a use's units kept free to let that reads as normal, thousandths: the game's own for the district, else the target.</summary>
        public static int NormalPermille(DistrictMarket m, int use)
        {
            int normal = m.Normal != null && use < m.Normal.Length ? m.Normal[use] : -1;
            return normal >= 0 && normal <= 1000 ? normal : TargetVacancyPermille;
        }

        /// <summary>Vacant units of a use settled in, thousandths: those of buildings still being let after their opening left out.</summary>
        public static int SettledVacancyPermille(DistrictMarket m, int use)
        {
            int units = m.Units != null && use < m.Units.Length ? m.Units[use] : 0;
            int vacant = m.Vacant != null && use < m.Vacant.Length ? m.Vacant[use] : 0;
            int filling = m.Filling != null && use < m.Filling.Length ? Math.Max(0, Math.Min(vacant, m.Filling[use])) : 0;
            return units <= 0 ? 0 : (int)(1000L * Math.Min(vacant - filling, units) / units);
        }

        /// <summary>Demand points the vacancies give a use: a gentle lean inside the band round the normal share, then three times that past it.</summary>
        public static int VacancyPull(DistrictMarket m, int use)
        {
            int off = NormalPermille(m, use) - SettledVacancyPermille(m, use);   // above 0: fewer empty than normal, wanted
            if (Math.Abs(off) <= NormalBand)
            {
                return off * BandGainTenths / 10;   // a balanced market: a gentle lean, enough that one more empty unit never wants more
            }
            int band = off > 0 ? NormalBand : -NormalBand;
            return (band * BandGainTenths + (off - band) * VacancyGainTenths) / 10;
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
            return Math.Max(0, Math.Min(100, Base[use] + pull + VacancyPull(m, use)));
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

        /// <summary>Yesterday's level moved toward today's wanted one by one step at most; none yet (0) starts even, at 1000, so even the first day moves one step.</summary>
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
    }
}
