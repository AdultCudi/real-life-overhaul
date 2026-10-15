using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Neighborhoods;
using Buildings;
using Helpers;
using RealLifeOverhaul.Events;
using UnityEngine;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The one place the city's faults read the game (and the parts of the mod that stand for it): the city's buildings
    /// with a street door by district, each district's upkeep, traffic and night life, the street lamps, the weather and
    /// the council's cleaning, and where in the street a fault of each kind can be put. The city is read once, behind the
    /// loading screen (<see cref="Prewarm"/>), or failing that the first time the faults need it while the screen is black
    /// at a door; after that a roll is a look into those lists.
    ///
    /// COST: nothing per frame. Behind the loading screen, one pass over the city's buildings and one over the game's
    /// list of the lamps it culls. A placement, once when a fault is rolled: a few look-ups of the navigation mesh and
    /// the road map.
    /// </summary>
    public static class FaultGame
    {
        private const float LampFromDoor = 50f;      // metres: the lamp that goes dark is the nearest this near the door picked
        private const float LampTaken = 2f;          // metres: a lamp this near a dark one is that one
        private const float LaneFromDoor = 25f;      // metres: the lane a pothole opens in is this near the door picked
        private const float WheelTrack = 0.9f;       // metres from the lane's middle: where a wheel runs
        private const float HeapAside = 4f;          // metres along the pavement from the door: never in front of it
        private const float HeapFromDoor = 2.5f;     // metres: never nearer a door than this
        private const float TowardKerb = 1f;         // metres from the pavement's middle towards the road: an unsafe place by the kerb
        private const int Tries = 8;                 // doors tried for one fault
        private const string Residential = "ba:buildingtype_residential";   // the game's type of a home
        private const string Global = "ba:neighborhood_global";             // the game's district of nowhere in particular

        public static IModLogger Logger;

        private struct Door
        {
            public Vector3 At;
            public int District;
            public string Street;
            public int Number;
            public CityBuildingController Building;
        }

        private static readonly List<Door> Doors = new List<Door>(1024);
        private static readonly List<FaultDistrict> Districts = new List<FaultDistrict>(12);
        private static readonly List<int>[] ByDistrict = new List<int>[32];
        private static readonly List<StreetLamp> Lamps = new List<StreetLamp>(4096);
        private static readonly List<Vector3> LampFeet = new List<Vector3>(4096);
        private static CityManager _city;
        private static bool _lampsRead;

        /// <summary>True once the city's doors and districts are read for the city now loaded.</summary>
        public static bool Ready => _city != null && ReferenceEquals(_city, InstanceBehavior<CityManager>.Instance) && Doors.Count > 0;

        /// <summary>True once the doors, the districts and the lamps are all read for the city now loaded.</summary>
        public static bool Complete => Ready && _lampsRead;

        /// <summary>The street lamps of the city now loaded (from the game's list of what it culls), and their feet, in step.</summary>
        public static IReadOnlyList<StreetLamp> CityLamps => Lamps;

        /// <summary>
        /// Behind the loading screen (Core/ScenePrewarm, late: the game's buildings are listed by then): the city's doors and
        /// districts, and its lamps. True when done; false to be tried again behind the next black screen.
        /// </summary>
        public static bool Prewarm()
        {
            return ReadCity() && ReadLamps();
        }

        /// <summary>The city read now if it has not been for the city loaded, but only while the screen is black (a door, a load, a pause). True when it is there.</summary>
        public static bool EnsureHidden()
        {
            if (Ready && _lampsRead)
            {
                return true;
            }
            return SceneFinds.Hidden && Prewarm();
        }

        /// <summary>What was read of the last city: let go when it unloads.</summary>
        public static void Forget()
        {
            Doors.Clear();
            Districts.Clear();
            Lamps.Clear();
            LampFeet.Clear();
            for (int i = 0; i < ByDistrict.Length; i++)
            {
                ByDistrict[i]?.Clear();
            }
            _city = null;
            _lampsRead = false;
        }

        private static bool ReadCity()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            if (all == null || all.Length == 0)
            {
                return false;
            }
            Doors.Clear();
            Districts.Clear();
            for (int i = 0; i < ByDistrict.Length; i++)
            {
                ByDistrict[i]?.Clear();
            }
            var traffic = new long[ByDistrict.Length];
            var counted = new int[ByDistrict.Length];
            var night = new int[ByDistrict.Length];
            for (int b = 0; b < all.Length; b++)
            {
                CityBuildingController c = all[b];
                if (!CivicCity.HasDoor(c) || string.IsNullOrEmpty(c.building.Neighbourhood) || c.building.Neighbourhood == Global || GameMembers.Fenced(c.building))
                {
                    continue;
                }
                int d = DistrictIndex(c.building.Neighbourhood);
                if (d < 0)
                {
                    continue;
                }
                Doors.Add(new Door { At = c.entranceDoors[0].doorTransform.position, District = d, Street = c.building.StreetName, Number = c.building.StreetNumber, Building = c });
                (ByDistrict[d] ?? (ByDistrict[d] = new List<int>(128))).Add(Doors.Count - 1);
                if (c.building.BuildingType != Residential)
                {
                    traffic[d] += c.building.trafficIndex;
                    counted[d]++;
                }
                string type = c.buildingRegistration != null ? c.buildingRegistration.businessTypeName : null;
                if (type != null && (type.Contains("nightclub") || type.Contains("bar") || type.Contains("restaurant") || type.Contains("liquorstore")))
                {
                    night[d]++;
                }
            }
            for (int d = 0; d < Districts.Count; d++)
            {
                FaultDistrict fd = Districts[d];
                fd.Spending = ClassUpkeep(fd.Id);
                fd.Busy = Rank(traffic, counted, d);
                fd.Night = RankCount(night, d);
            }
            _city = city;
            Logger?.Info("City faults: the city read for its faults: " + Doors.Count + " street doors in " + Districts.Count + " districts.");
            return Doors.Count > 0;
        }

        private static int DistrictIndex(string id)
        {
            for (int i = 0; i < Districts.Count; i++)
            {
                if (Districts[i].Id == id)
                {
                    return i;
                }
            }
            if (Districts.Count >= ByDistrict.Length)
            {
                return -1;
            }
            Districts.Add(new FaultDistrict { Id = id });
            return Districts.Count - 1;
        }

        // A district's own upkeep by who lives there: a working-class district is kept up less, a well-off one more (the game's neighbourhood data).
        private static int ClassUpkeep(string id)
        {
            try
            {
                NeighborhoodData data = NeighborhoodHelper.GetData(id);
                if (data == null)
                {
                    return 0;
                }
                return data.workingClassPercentage >= 50 ? -1 : data.upperClassClassPercentage >= 40 ? 1 : 0;
            }
            catch (Exception)
            {
                return 0;   // (a district the game's data does not know: ordinary upkeep)
            }
        }

        // A district's place among the others by average traffic, as 0 to 3 (quarters).
        private static int Rank(long[] sum, int[] count, int d)
        {
            float mine = count[d] > 0 ? (float)sum[d] / count[d] : 0f;
            int below = 0;
            for (int i = 0; i < Districts.Count; i++)
            {
                float other = count[i] > 0 ? (float)sum[i] / count[i] : 0f;
                below += other < mine ? 1 : 0;
            }
            return Districts.Count > 1 ? FaultRules.Clamp(below * 4 / Districts.Count, 0, 3) : 1;
        }

        private static int RankCount(int[] count, int d)
        {
            int below = 0;
            for (int i = 0; i < Districts.Count; i++)
            {
                below += count[i] < count[d] ? 1 : 0;
            }
            return Districts.Count > 1 ? FaultRules.Clamp(below * 4 / Districts.Count, 0, 3) : 1;
        }

        private static bool ReadLamps()
        {
            if (!PerfGame.KnowsLamps)
            {
                _lampsRead = true;   // (this game keeps its lamps elsewhere: no lamp goes dark, the other faults go on)
                return true;
            }
            PerfGame.Lamps(Lamps);
            LampFeet.Clear();
            for (int i = 0; i < Lamps.Count; i++)
            {
                LampFeet.Add(Lamps[i].transform.position);
            }
            _lampsRead = Lamps.Count > 0;
            return _lampsRead;
        }

        // ---------------------------------------------------------------- the city now

        /// <summary>The city as this hour's roll reads it: the time, the weather, the council's cleaning, the districts.</summary>
        public static void Read(FaultFacts facts, long now, int option)
        {
            facts.Now = now;
            facts.Option = option;
            facts.Rain = Weather.Sky == Sky.Rain || Weather.Sky == Sky.Storm;
            facts.Storm = Weather.Sky == Sky.Storm;
            facts.Frost = Weather.Spell == Spell.ColdSnap || Weather.Spell == Spell.Ice;
            int cleaning = Settings.Civic ? CivicPolicy.Position(Lever.Cleaning) : 0;
            facts.Litter = cleaning;
            facts.Districts.Clear();
            for (int i = 0; i < Districts.Count; i++)
            {
                FaultDistrict d = Districts[i];
                facts.Districts.Add(new FaultDistrict { Id = d.Id, Spending = FaultRules.Clamp(d.Spending + cleaning, -1, 1), Busy = d.Busy, Night = d.Night });
            }
        }

        /// <summary>Game minutes since day 0 now.</summary>
        public static long Now()
        {
            GameInstance save = SaveGameManager.Current;
            return save != null ? (long)save.Day * 1440 + save.Hour * 60 + Mathf.Clamp(Mathf.FloorToInt(save.Minute), 0, 59) : 0;
        }

        /// <summary>Where the player stands in the city (outdoors, or the door of the building they are in); false without a player.</summary>
        public static bool Player(out Vector3 at)
        {
            at = default;
            try
            {
                if (PlayerHelper.PlayerController == null)
                {
                    return false;
                }
                at = PlayerHelper.GetCityPosition();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string PlayerDistrict()
        {
            return Districts.Count > 0 ? RealLifeOverhaul.Districts.DistrictGame.PlayerDistrict() : null;
        }

        public static bool Outdoors => !BuildingManager.IsInsideBuilding;

        // ---------------------------------------------------------------- where a fault goes

        /// <summary>
        /// A place for a fault the roll wants, in its district: a door picked at random (never a door of a player's own
        /// place), and from it the lamp nearest, the lane in front, or a stretch of pavement beside it. False if no place was
        /// found in a few tries (the fault is then not made this hour).
        /// </summary>
        public static bool Place(FaultWant want, IFaultDice dice, FaultFile file, out Vector3 at, out string address)
        {
            at = default;
            address = null;
            List<int> doors = want.District >= 0 && want.District < Districts.Count ? ByDistrict[want.District] : null;
            if (doors == null || doors.Count == 0)
            {
                return false;
            }
            for (int t = 0; t < Tries; t++)
            {
                Door door = Doors[doors[dice.Next(doors.Count)]];
                if (door.Building == null || Players(door.Building))
                {
                    continue;
                }
                bool found;
                switch (want.Kind)
                {
                    case FaultKind.Lamp: found = LampNear(door.At, file, out at); break;
                    case FaultKind.Pothole: found = InLane(door.At, out at); break;
                    case FaultKind.Litter: found = Beside(door.At, want.Variant, false, out at); break;
                    case FaultKind.Unsafe: found = Beside(door.At, want.Variant, true, out at); break;
                    default: found = false; break;
                }
                if (found)
                {
                    address = CivicCity.AddressText(door.Street, door.Number);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The test's place for a fault of a kind near the player: the doors nearest them (within <see cref="TestNear"/>
        /// metres) tried in turn. False if none will do.
        /// </summary>
        public static bool PlaceNear(FaultKind kind, Vector3 player, int variant, FaultFile file, out Vector3 at, out string address, out string district)
        {
            at = default;
            address = district = null;
            float floor = -1f;
            for (int t = 0; t < Tries; t++)
            {
                int best = -1;
                float bestSqr = TestNear * TestNear;
                for (int i = 0; i < Doors.Count; i++)
                {
                    Vector3 d = Doors[i].At;
                    float sqr = (d.x - player.x) * (d.x - player.x) + (d.z - player.z) * (d.z - player.z);
                    if (sqr > floor && sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    return false;
                }
                floor = bestSqr;
                Door door = Doors[best];
                if (door.Building == null || Players(door.Building))
                {
                    continue;
                }
                bool found = kind == FaultKind.Lamp ? LampNear(door.At, file, out at) : kind == FaultKind.Pothole ? InLane(door.At, out at)
                    : kind == FaultKind.Hotspot ? Beside(door.At, variant, false, out at) : Beside(door.At, variant, kind == FaultKind.Unsafe, out at);
                if (found)
                {
                    address = CivicCity.AddressText(door.Street, door.Number);
                    district = Districts[door.District].Id;
                    return true;
                }
            }
            return false;
        }

        private const float TestNear = 150f;   // metres: the test's faults go at doors this near the player

        // A place a player rents or owns (this one or, in a session, another): nothing is put at its door.
        private static bool Players(CityBuildingController building)
        {
            BuildingRegistration reg = building.buildingRegistration;
            return reg != null && (GoingPublic.Mine(reg) || GoingPublic.Foreign(reg) || reg.RentedByPlayer || reg.BuildingOwnedByPlayer);
        }

        private static bool LampNear(Vector3 door, FaultFile file, out Vector3 at)
        {
            at = default;
            int best = -1;
            float bestSqr = LampFromDoor * LampFromDoor;
            for (int i = 0; i < LampFeet.Count; i++)
            {
                Vector3 foot = LampFeet[i];
                float sqr = (foot.x - door.x) * (foot.x - door.x) + (foot.z - door.z) * (foot.z - door.z);
                if (sqr < bestSqr && Lamps[i] != null && !FaultRules.DarkAt(file, foot.x, foot.z, LampTaken))
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            if (best < 0)
            {
                return false;
            }
            at = LampFeet[best];
            return true;
        }

        private static bool InLane(Vector3 door, out Vector3 at)
        {
            at = default;
            if (!TrafficTools.Street || !RoadMap.Ensure() || !RoadMap.NearestLane(door, out float distance, out Vector3 runs, out _, out Vector3 lane) || distance > LaneFromDoor)
            {
                return false;
            }
            Vector3 right = Vector3.Cross(Vector3.up, runs).normalized;
            at = lane + right * WheelTrack;
            if (CitySpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            return true;
        }

        // A stretch of pavement to one side of a door (the side by the variant), clear of the door; for an unsafe place, by the kerb.
        private static bool Beside(Vector3 door, int variant, bool kerb, out Vector3 at)
        {
            at = default;
            if (!CitySpots.Pavement(door, 8f, out Vector3 front) || !CitySpots.Along(front, out Vector3 run))
            {
                return false;
            }
            float side = variant % 2 == 0 ? 1f : -1f;
            Vector3 aside = front + run * side * (HeapAside + variant % 3);
            if (kerb && TrafficTools.Street && RoadMap.Ensure() && RoadMap.NearestLane(aside, out float distance, out _, out _, out Vector3 lane) && distance < 14f)
            {
                Vector3 out_ = lane - aside;
                out_.y = 0f;
                aside += out_.normalized * TowardKerb;
            }
            if (!CitySpots.Pavement(aside, 2f, out Vector3 spot))
            {
                return false;
            }
            Vector3 fromDoor = spot - door;
            fromDoor.y = 0f;
            if (fromDoor.sqrMagnitude < HeapFromDoor * HeapFromDoor)
            {
                return false;
            }
            at = spot;
            return true;
        }

        /// <summary>The lamp at a fault's place: the one whose foot is within a step of it; null if none (the lamps not read, or another city).</summary>
        public static StreetLamp LampAt(Vector3 foot)
        {
            for (int i = 0; i < LampFeet.Count; i++)
            {
                Vector3 f = LampFeet[i];
                if ((f.x - foot.x) * (f.x - foot.x) + (f.z - foot.z) * (f.z - foot.z) < 0.25f && Lamps[i] != null)
                {
                    return Lamps[i];
                }
            }
            return null;
        }

        /// <summary>The district of a point: the neighbourhood of the nearest building with a door within so many metres (a pass over the doors read); null for none.</summary>
        public static string DistrictAt(Vector3 point, float within, out string address)
        {
            address = null;
            int best = -1;
            float bestSqr = within * within;
            for (int i = 0; i < Doors.Count; i++)
            {
                Vector3 d = Doors[i].At;
                float sqr = (d.x - point.x) * (d.x - point.x) + (d.z - point.z) * (d.z - point.z);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            if (best < 0)
            {
                return null;
            }
            address = CivicCity.AddressText(Doors[best].Street, Doors[best].Number);
            return Districts[Doors[best].District].Id;
        }

        /// <summary>The doors of the player's own businesses, with their registrations, into the lists (cleared first).</summary>
        public static void PlayerBusinesses(List<BuildingRegistration> regs, List<Vector3> doors)
        {
            regs.Clear();
            doors.Clear();
            GameInstance save = SaveGameManager.Current;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (save == null || save.BuildingRegistrations == null || city == null)
            {
                return;
            }
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration reg = save.BuildingRegistrations[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness)
                {
                    continue;
                }
                CityBuildingController c = city.FindCityBuildingController(reg.Address);
                if (CivicCity.HasDoor(c))
                {
                    regs.Add(reg);
                    doors.Add(c.entranceDoors[0].doorTransform.position);
                }
            }
        }
    }
}
