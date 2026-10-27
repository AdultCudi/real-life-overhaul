using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Rivals;
using Buildings;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game's reads of the game for car parks: the district's street rate an hour (what the game charges a car left in
    /// its streets), the businesses and rivals round a place (who may ask a car park for bays), and the player's own cars standing in a
    /// lot (which the game charged its street rate for). Nothing here keeps state or changes the game. COST: nothing per frame; a walk
    /// over the city's buildings once a day for each car park and on a press.
    /// </summary>
    internal static partial class ConstructionGame
    {
        /// <summary>The street rate an hour in dollars the game charges a car left legally in a district's streets; 0 for a district it lacks.</summary>
        internal static double ParkingRate(string district)
        {
            if (string.IsNullOrEmpty(district))
            {
                return 0.0;
            }
            try
            {
                NeighborhoodData data = NeighborhoodHelper.GetData(district);
                return data != null ? data.parkingPrice : 0.0;
            }
            catch (Exception)
            {
                return 0.0;   // a district the game's data does not know: the rules' least rate stands
            }
        }

        /// <summary>
        /// The businesses and rivals within <paramref name="radius"/> metres of a place that could ask a car park for bays, the nearest
        /// first, into <paramref name="into"/> (cleared): a business of the game's own with a building of its own round there (not an
        /// empty unit, not the player's), and a rival that owns a building there (named once, by the biggest). Its weight is its building's
        /// size, 1 to 10.
        /// </summary>
        internal static void ParkNeighbours(Vector3 at, int radius, List<ParkNeighbour> into)
        {
            into.Clear();
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (all == null)
            {
                return;
            }
            float reach = (float)radius * radius;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                Building b = c != null ? c.building : null;
                if (b == null || !CivicCity.HasDoor(c) || b.SpecialService != null || b.IsHamptonsHouse() || SpecialBuildings.Holds(c))
                {
                    continue;
                }
                Vector3 d = c.transform.position - at;
                d.y = 0f;
                if (d.sqrMagnitude > reach)
                {
                    continue;
                }
                BuildingRegistration reg = b.GetRegistration();
                if (reg == null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty"
                    || (b.BuildingType != "ba:buildingtype_retail" && b.BuildingType != "ba:buildingtype_office" && b.BuildingType != "ba:buildingtype_warehouse"))
                {
                    continue;
                }
                bool rival = !string.IsNullOrEmpty(reg.buildingOwnerRivalId) && (b.StreetNumber & 1) == 0;   // half the buildings a rival owns ask in the rival's name
                string name = rival ? reg.buildingOwnerRivalId.GetRivalName() : Words(reg);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                AddNeighbour(into, name, rival ? CarParkLease.Rival : CarParkLease.Business, WeightOf(b.totalSqm), (int)Mathf.Sqrt(d.sqrMagnitude));
            }
        }

        // One neighbour put in by distance; a name already there keeps the bigger weight and the nearer place.
        private static void AddNeighbour(List<ParkNeighbour> into, string name, int who, int weight, int metres)
        {
            for (int i = 0; i < into.Count; i++)
            {
                if (string.Equals(into[i].Name, name, StringComparison.Ordinal))
                {
                    ParkNeighbour kept = into[i];
                    kept.Weight = Math.Max(kept.Weight, weight);
                    if (metres < kept.Metres)
                    {
                        kept.Metres = metres;
                        into.RemoveAt(i);
                        InsertNear(into, kept);
                    }
                    else
                    {
                        into[i] = kept;
                    }
                    return;
                }
            }
            InsertNear(into, new ParkNeighbour { Name = name, Who = who, Weight = weight, Metres = metres });
        }

        private static void InsertNear(List<ParkNeighbour> into, ParkNeighbour n)
        {
            int at = into.Count;
            while (at > 0 && into[at - 1].Metres > n.Metres)
            {
                at--;
            }
            into.Insert(at, n);
        }

        // A building's weight 1 to 10 by its floor area: a small shop one, a big office or warehouse nine.
        private static int WeightOf(int sqm)
        {
            return sqm <= 100 ? 1 : sqm <= 200 ? 2 : sqm <= 350 ? 3 : sqm <= 600 ? 4 : sqm <= 900 ? 5 : sqm <= 1300 ? 6 : sqm <= 1800 ? 7 : sqm <= 2500 ? 8 : 9;
        }

        /// <summary>
        /// The player's own cars standing legally in a rectangle of the ground (the lot's middle, its turn and its size), not the one they
        /// are driving and not one in an underground car park: the cars the game charged its street rate an hour for.
        /// </summary>
        internal static int PlayerCarsIn(float x, float z, int yaw, int width, int depth)
        {
            GameInstance save = SaveGameManager.Current;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            if (all == null)
            {
                return 0;
            }
            double a = yaw * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a), halfX = width * 0.5f, halfZ = depth * 0.5f;
            int cars = 0;
            for (int i = 0; i < all.Count; i++)
            {
                VehicleInstance v = all[i];
                if (v == null || v.parkingState != ParkingState.Legal || v.streetName == "ba:street_parking" || v.id == save.ActiveVehicleId)
                {
                    continue;
                }
                Vector3 p = v.position;
                float dx = p.x - x, dz = p.z - z;
                float lx = cos * dx - sin * dz, lz = sin * dx + cos * dz;   // the lot's own axes: the inverse of the turn that puts a point of the lot in the world
                if (Mathf.Abs(lx) <= halfX && Mathf.Abs(lz) <= halfZ)
                {
                    cars++;
                }
            }
            return cars;
        }
    }
}
