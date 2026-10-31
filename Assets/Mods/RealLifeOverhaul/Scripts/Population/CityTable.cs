using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The city's table laid out for the population: the buildings in the order of their addresses (the same on every machine), each
    /// building's flats with their size and the game's own rent, a free or taken mark for every flat, and the districts' running
    /// figures. Game-free. COST: nothing per frame; a pass over the buildings when a city is put together.
    /// </summary>
    public static class CityTable
    {
        /// <summary>Buildings sorted by street and number, in the ordinal order of the street's id: the same order on every machine.</summary>
        public static void Sort(List<CityBuilding> buildings)
        {
            buildings.Sort(CompareAddress);
        }

        private static int CompareAddress(CityBuilding a, CityBuilding b)
        {
            int c = string.CompareOrdinal(a.Street, b.Street);
            return c != 0 ? c : a.Number.CompareTo(b.Number);
        }

        /// <summary>
        /// The flats of every building laid out: their first place in the occupants' table, their count, size and rent; every flat free.
        /// The districts' rents start at the game's own (1000), their figures empty, each field's pay at its start.
        /// </summary>
        public static void Lay(PopulationState s)
        {
            int n = s.Buildings.Count;
            s.FlatFirst = new int[n];
            s.FlatCount = new int[n];
            s.FlatSqm = new int[n];
            s.FlatRent = new int[n];
            int total = 0;
            for (int i = 0; i < n; i++)
            {
                CityBuilding b = s.Buildings[i];
                s.FlatFirst[i] = total;
                s.FlatCount[i] = MeasureRules.Flats(b);
                s.FlatSqm[i] = MeasureRules.FlatSqm(b);
                s.FlatRent[i] = b.Use == Use.Homes ? (b.Rent > 0 ? b.Rent : Rent(b, s.Districts)) : 0;
                total += s.FlatCount[i];
            }
            s.Occupant = new int[total];
            for (int i = 0; i < total; i++)
            {
                s.Occupant[i] = -1;
            }
            int d = s.Districts.Count;
            if (s.RentIndex.Length != d)
            {
                var index = new int[d];
                for (int i = 0; i < d; i++)
                {
                    index[i] = i < s.RentIndex.Length && s.RentIndex[i] > 0 ? s.RentIndex[i] : 1000;
                }
                s.RentIndex = index;
            }
            if (s.Figures.Length != d)
            {
                var figures = new DistrictFigures[d];
                for (int i = 0; i < d; i++)
                {
                    figures[i] = i < s.Figures.Length && s.Figures[i] != null ? s.Figures[i] : new DistrictFigures();
                }
                s.Figures = figures;
            }
            if (s.WantFlats.Length != d)
            {
                Array.Resize(ref s.WantFlats, d);
            }
            if (s.FieldWage.Length != PopulationCatalogue.Fields)
            {
                var wage = new int[PopulationCatalogue.Fields];
                for (int f = 0; f < wage.Length; f++)
                {
                    wage[f] = f < s.FieldWage.Length && s.FieldWage[f] > 0 ? s.FieldWage[f] : 1000;
                }
                s.FieldWage = wage;
            }
        }

        /// <summary>
        /// The game's market rent of one unit of a residential building, cents a day, by the game's own arithmetic (Building.GetMarketPricePerSqm and
        /// GetBuildingDailyMarketRent: the base price times residential's multiplier times the district's real estate multiplier, dearer for a quiet
        /// street, times the rent a square metre and residential's rent multiplier, for the unit's floor).
        /// </summary>
        public static int Rent(in CityBuilding b, IList<PopDistrict> districts)
        {
            int estate = b.District >= 0 && districts != null && b.District < districts.Count ? districts[b.District].RealEstate : 1000;
            double price = PopulationCatalogue.BasePricePerSqm * (PopulationCatalogue.ResidentialPriceMultiplier / 1000.0) * (estate / 1000.0);
            int quiet = Math.Min(b.Traffic - 30, 0);   // lowerrentforhightraffic: a street under 30 costs more
            price -= price * quiet / 100.0;
            int sqm = b.Villa ? Math.Max(b.UnitSqm, 1) : (b.UnitSqm > 0 ? b.UnitSqm : PopulationCatalogue.FlatSqmDefault);
            double rent = Math.Ceiling(sqm * price * (PopulationCatalogue.RentPerPrice / 1000000.0) * (PopulationCatalogue.RentResidential / 1000.0));
            return (int)Math.Min(int.MaxValue / 4, rent * 100.0);
        }

        /// <summary>A flat's rent today, cents a day: the game's own times its district's index.</summary>
        public static int FlatRentNow(PopulationState s, int building)
        {
            int d = building >= 0 && building < s.Buildings.Count ? s.Buildings[building].District : -1;
            int index = d >= 0 && d < s.RentIndex.Length ? s.RentIndex[d] : 1000;
            return building >= 0 && building < s.FlatRent.Length ? (int)Math.Min(int.MaxValue / 4, (long)s.FlatRent[building] * index / 1000) : 0;
        }

        /// <summary>The metres between two districts' middles.</summary>
        public static int Distance(PopulationState s, int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= s.Districts.Count || b >= s.Districts.Count)
            {
                return a == b ? 0 : 4000;
            }
            double dx = s.Districts[a].X - s.Districts[b].X, dz = s.Districts[a].Z - s.Districts[b].Z;
            return (int)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Each district's middle: the mean place of its buildings (a district with none keeps the middle it was given).</summary>
        public static void Middles(PopulationState s)
        {
            int d = s.Districts.Count;
            var x = new double[d];
            var z = new double[d];
            var count = new int[d];
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                CityBuilding b = s.Buildings[i];
                if (b.District >= 0 && b.District < d && (b.X != 0f || b.Z != 0f))   // a building with no place given says nothing of its district's middle
                {
                    x[b.District] += b.X;
                    z[b.District] += b.Z;
                    count[b.District]++;
                }
            }
            for (int i = 0; i < d; i++)
            {
                if (count[i] > 0)
                {
                    s.Districts[i].X = (float)(x[i] / count[i]);
                    s.Districts[i].Z = (float)(z[i] / count[i]);
                }
            }
        }
    }
}
