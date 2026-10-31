using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// Housing (POPULATION.md section 8; the brief's eighth step): every flat is a unit with a rent, the game's own times its district's index;
    /// a district's vacancy is real and its rents move with it month by month; households choose flats by what they can pay, the room they need,
    /// their work and their class; and each district's pressure for homes, offices, shops and warehouses comes out of the people (households
    /// forming and coming that find no flat, jobs growing, spending per shop floor, goods per warehouse floor), which Uptown's market reads instead
    /// of its fixed ratios. The base districts answer with rents and vacancy only (the owner, 1 Nov 2026: no new geometry there). Game-free.
    /// COST: nothing per frame; a household's rent a few operations, a flat looked for a few dozen.
    /// </summary>
    public static class HousingRules
    {
        public const int FlatLooks = 12;          // free flats a household looks at when it moves
        public const int PullScale = 100;         // a use's pull runs -100 to 100, as Uptown's DemandRules' own

        /// <summary>A household's rent a day, cents: the flat's rent today for a tenant, the upkeep and tax (and a mortgage) for an owner.</summary>
        public static void SetRent(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            int b = hh.Building[h];
            if (b < 0)
            {
                hh.Rent[h] = 0;
                return;
            }
            long market = CityTable.FlatRentNow(s, b);
            if (hh.Landlord[h] == Landlords.Own)
            {
                long cost = market * PopulationCatalogue.OwnerCostShare / 1000;
                if ((hh.Flags[h] & HouseholdFlags.Mortgage) != 0)
                {
                    cost += market * PopulationCatalogue.MortgageShare / 1000;
                }
                hh.Rent[h] = (int)Math.Min(int.MaxValue / 4, cost);
                return;
            }
            if (hh.Landlord[h] == Landlords.Player)
            {
                return;   // the player sets that rent (the estate's let unit); the population leaves it as told
            }
            if ((hh.Flags[h] & HouseholdFlags.Regulated) != 0 && hh.Rent[h] > 0)
            {
                hh.Rent[h] = (int)Math.Min(hh.Rent[h], market);   // a regulated tenancy keeps its rent, and never pays past the market's
                return;
            }
            hh.Rent[h] = (int)Math.Min(int.MaxValue / 4, market);
        }

        /// <summary>
        /// The month's rents: each district's index moves by how far its vacancy is from the target (fewer free flats, dearer), within its bounds,
        /// and every tenant's rent follows it.
        /// </summary>
        public static void Month(PopulationState s)
        {
            int districts = s.Districts.Count;
            var flats = new int[districts];
            var vacant = new int[districts];
            for (int b = 0; b < s.Buildings.Count; b++)
            {
                int d = s.Buildings[b].District;
                if (d < 0 || d >= districts)
                {
                    continue;
                }
                int first = s.FlatFirst[b];
                for (int u = 0; u < s.FlatCount[b]; u++)
                {
                    flats[d]++;
                    vacant[d] += s.Occupant[first + u] == -1 ? 1 : 0;
                }
            }
            for (int d = 0; d < districts && d < s.RentIndex.Length; d++)
            {
                if (flats[d] <= 0 || s.Districts[d].Villas)
                {
                    continue;   // a district of villas has no rental market to move
                }
                int vacancy = (int)(1000L * vacant[d] / flats[d]);
                int step = (PopulationCatalogue.TargetVacancy - vacancy) * PopulationCatalogue.RentStep / 10;
                s.RentIndex[d] = PopulationCatalogue.Clamp(s.RentIndex[d] + s.RentIndex[d] * step / 1000, PopulationCatalogue.RentLeast, PopulationCatalogue.RentMost);
            }
            for (int h = 0; h < s.Households.Count; h++)
            {
                if (s.Households.Alive(h))
                {
                    SetRent(s, h);
                }
            }
        }

        /// <summary>True when a household's rent takes more of its income than it will bear, or its flat is too small for its people.</summary>
        public static bool WantsMove(PopulationState s, int h, long disposable)
        {
            HouseholdTable hh = s.Households;
            if (hh.Landlord[h] == Landlords.Own || (hh.Flags[h] & HouseholdFlags.Players) != 0)
            {
                return false;   // owners stay; the player's household is the player's to move
            }
            int b = hh.Building[h];
            bool crowded = b >= 0 && s.FlatSqm[b] < hh.Size[h] * PopulationCatalogue.PersonsPerRoom;
            bool dear = disposable > 0 && hh.Rent[h] * 1000L / disposable > PopulationCatalogue.RentBurdenMove;
            return crowded || dear;
        }

        /// <summary>
        /// A free flat for a household of so many people and so much income after tax, by its class and work: a few free flats looked at in
        /// districts in reach (the home district first, then where the work is, then any), the best that it can pay for and has room; -1 for none.
        /// <paramref name="free"/> holds the free flats' places by district (FreeFlats), and loses the one chosen.
        /// </summary>
        public static int FindFlat(PopulationState s, List<int>[] free, int seed, int who, int day, int size, long disposable, int cls, int home, int work, out int building)
        {
            building = -1;
            int bestFlat = -1, bestScore = int.MinValue, bestAt = -1, bestDistrict = -1;
            int districts = s.Districts.Count;
            for (int look = 0; look < FlatLooks; look++)
            {
                int d = look < FlatLooks / 2 && home >= 0 ? home : look < FlatLooks * 3 / 4 && work >= 0 ? work : PopDraw.Range(seed, PopDraw.Flat, who, day * 31 + look, districts);
                if (d < 0 || d >= free.Length || free[d] == null || free[d].Count == 0)
                {
                    continue;
                }
                int at = PopDraw.Range(seed, PopDraw.Flat, who, day * 37 + look, free[d].Count);
                int flat = free[d][at];
                int b = BuildingOfFlat(s, flat);
                if (b < 0)
                {
                    continue;
                }
                long rent = CityTable.FlatRentNow(s, b);
                if (disposable > 0 && rent * 1000 / disposable > PopulationCatalogue.AffordableShare + 150)
                {
                    continue;   // beyond what it would ever pay
                }
                int score = Score(s, b, size, disposable, rent, cls, work);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFlat = flat;
                    bestAt = at;
                    bestDistrict = d;
                }
            }
            if (bestFlat < 0)
            {
                return -1;
            }
            free[bestDistrict][bestAt] = free[bestDistrict][free[bestDistrict].Count - 1];
            free[bestDistrict].RemoveAt(free[bestDistrict].Count - 1);
            building = BuildingOfFlat(s, bestFlat);
            return bestFlat;
        }

        // How well a flat suits a household: room for its people, rent against what it would pay, a district of its class, near its work.
        private static int Score(PopulationState s, int b, int size, long disposable, long rent, int cls, int work)
        {
            int score = 0;
            int room = s.FlatSqm[b] / Math.Max(1, size);
            score += room >= PopulationCatalogue.PersonsPerRoom ? 300 : room * 300 / PopulationCatalogue.PersonsPerRoom;
            if (disposable > 0)
            {
                long share = rent * 1000 / disposable;
                score += (int)Math.Max(-400, 300 - Math.Abs(share - 280));
            }
            PopDistrict d = s.Districts[s.Buildings[b].District];
            score += (cls == 0 ? d.Working : cls == 1 ? d.Middle : d.Upper) * 3;
            if (work >= 0)
            {
                score -= CityTable.Distance(s, s.Buildings[b].District, work) / 20;
            }
            return score;
        }

        /// <summary>The building a flat's place in the occupants' table belongs to (a search of the buildings' first places).</summary>
        public static int BuildingOfFlat(PopulationState s, int flat)
        {
            int lo = 0, hi = s.FlatFirst.Length - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (s.FlatFirst[mid] <= flat)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            while (found >= 0 && s.FlatCount[found] == 0)
            {
                found--;   // a building with no flats shares its first place with the next one's
            }
            return found >= 0 && flat < s.FlatFirst[found] + s.FlatCount[found] ? found : -1;
        }

        /// <summary>Each district's free flats, their places in the occupants' table, in the order of the table.</summary>
        public static void FreeFlats(PopulationState s, List<int>[] free)
        {
            for (int d = 0; d < free.Length; d++)
            {
                if (free[d] == null)
                {
                    free[d] = new List<int>(64);
                }
                free[d].Clear();
            }
            for (int b = 0; b < s.Buildings.Count; b++)
            {
                int d = s.Buildings[b].District;
                if (d < 0 || d >= free.Length)
                {
                    continue;
                }
                int first = s.FlatFirst[b];
                for (int u = 0; u < s.FlatCount[b]; u++)
                {
                    if (s.Occupant[first + u] == -1)
                    {
                        free[d].Add(first + u);
                    }
                }
            }
        }

        /// <summary>A household moves into a flat: the old one is freed, the new one taken, its district, rent and day of moving set.</summary>
        public static void MoveInto(PopulationState s, int h, int flat, int building, int day)
        {
            HouseholdTable hh = s.Households;
            Vacate(s, h);
            hh.Building[h] = building;
            hh.Unit[h] = flat - s.FlatFirst[building];
            hh.District[h] = (byte)s.Buildings[building].District;
            hh.Since[h] = day;
            hh.Flags[h] = (byte)(hh.Flags[h] & ~HouseholdFlags.WantsMove & ~HouseholdFlags.Away & ~HouseholdFlags.Regulated);   // a new tenancy is at the market's rent
            if (hh.Landlord[h] == Landlords.Own)
            {
                hh.Landlord[h] = Landlords.City;   // an owner who moves sells and rents here
                hh.Flags[h] = (byte)(hh.Flags[h] & ~HouseholdFlags.Mortgage);
            }
            s.Occupant[flat] = h;
            SetRent(s, h);
        }

        /// <summary>A household's flat freed (it moves or is gone).</summary>
        public static void Vacate(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            int b = hh.Building[h];
            if (b >= 0 && b < s.FlatFirst.Length && hh.Unit[h] >= 0 && hh.Unit[h] < s.FlatCount[b])
            {
                int flat = s.FlatFirst[b] + hh.Unit[h];
                if (s.Occupant[flat] == h)
                {
                    s.Occupant[flat] = -1;
                }
            }
            hh.Building[h] = -1;
            hh.Unit[h] = -1;
        }

        /// <summary>
        /// Each district's pull for homes, shops, offices and warehouses, -100 to 100, from the people: homes by households that looked for a flat
        /// there and found none and the region's households that wanted to come, against its free flats; shops by spending a square metre of shop
        /// floor against the city's; offices by the city's takings growing against last month (firms take on people and desks); warehouses by the
        /// goods sold a square metre of warehouse floor against the city's. -1000 (none counted) for a use the district has no floor of.
        /// </summary>
        public static void Pulls(PopulationState s, DistrictMeasure[] measure, long takingsNow, long takingsBefore, int[] into)
        {
            int districts = s.Districts.Count;
            long citySpend = 0, cityShop = 0, cityGoods = 0, cityWarehouse = 0;
            var goods = new long[districts];
            BusinessTable bt = s.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                int k = bt.Kind[b];
                if (k == (int)Spend.Groceries || k == (int)Spend.Clothing || k == (int)Spend.Electronics || k == (int)Spend.Furniture || k == (int)Spend.BooksGifts
                    || k == (int)Spend.Flowers || k == (int)Spend.Jewellery)
                {
                    if (bt.District[b] < districts)
                    {
                        goods[bt.District[b]] += bt.LastTakings[b];
                        cityGoods += bt.LastTakings[b];
                    }
                }
            }
            for (int d = 0; d < districts; d++)
            {
                citySpend += s.Figures[d].Takings;
                cityShop += measure != null && d < measure.Length ? measure[d].ShopSqm : 0;
                cityWarehouse += measure != null && d < measure.Length ? measure[d].WarehouseSqm : 0;
            }
            int growth = takingsBefore > 0 ? (int)Math.Max(-1000, Math.Min(1000, (takingsNow - takingsBefore) * 1000 / takingsBefore)) : 0;
            for (int d = 0; d < districts; d++)
            {
                DistrictFigures f = s.Figures[d];
                DistrictMeasure m = measure != null && d < measure.Length ? measure[d] : null;
                int row = d * 4;
                int unmet = s.WantFlats.Length > d ? s.WantFlats[d] : 0;
                int pressure = unmet + f.MigrantsWanting - f.Vacant;
                int scale = Math.Max(20, f.Flats / 20);   // a twentieth of its flats wanted reads as full pull
                into[row] = f.Flats > 0 ? PopulationCatalogue.Clamp(pressure * PullScale / scale, -PullScale, PullScale) : -1000;
                into[row + 1] = m != null && m.ShopSqm > 0 && cityShop > 0 && citySpend > 0
                    ? PopulationCatalogue.Clamp((int)((f.Takings * 1000 / m.ShopSqm * cityShop / citySpend - 1000) / 10), -PullScale, PullScale) : -1000;
                into[row + 2] = m != null && m.OfficeSqm > 0 ? PopulationCatalogue.Clamp(growth * 2 / 10 + (f.Vacancies > f.Unemployed ? 10 : -10), -PullScale, PullScale) : -1000;
                into[row + 3] = m != null && m.WarehouseSqm > 0 && cityWarehouse > 0 && cityGoods > 0
                    ? PopulationCatalogue.Clamp((int)((goods[d] * 1000 / m.WarehouseSqm * cityWarehouse / cityGoods - 1000) / 10), -PullScale, PullScale) : -1000;
            }
        }
    }
}
