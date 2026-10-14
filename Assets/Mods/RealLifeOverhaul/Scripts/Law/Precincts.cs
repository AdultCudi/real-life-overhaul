using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings.Outdoors;
using Extensions;
using Localizor;
using Streets;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>One police station: a building of the city's own, designated and dressed by the mod.</summary>
    public sealed class Precinct
    {
        public int Index;
        public CityBuildingController Building;
        public string District;         // the game's neighbourhood id
        public string NameKey;
        public string Name;             // as shown
        public Vector3 Door;
        public string Address;          // "street|number", as kept in the save
        /// <summary>True if the player can walk in: an empty unit nobody rents, entered through its own door into the game's own interior for it.</summary>
        public bool WalkIn;
        /// <summary>How well its frontage takes the station's dressing, as measured when it was chosen (higher is better).</summary>
        public int Score;
        internal MapPlace Place;        // on the city map (Core/MapPlaces)
        internal bool[] DoorWas;        // the doors' own "always open" flags, to be put back
        internal bool DoorsOpen;
        internal bool Stays;            // on the player's building with nowhere else in its district to go: not looked for again this session
    }

    /// <summary>
    /// The city's police stations. The game has none (no building, no model: only the parked
    /// police car prop), so one existing building in each district is designated. Since players
    /// walk in, it has to be a unit that stands empty and to let (the game loads the bare
    /// interior of such a unit through its own door, and nothing of the game's lives in it): an
    /// office where the district has one, otherwise a large shop unit, a warehouse, any shop.
    /// Of those, the one whose frontage takes the station's dressing best, measured once per
    /// city load: a sign band of its own over the shopfront, flat wall either side of the door
    /// and above it, a deep pavement, a kerb. Ties go to the one nearest the middle of the
    /// district, then by address. The choice is written into the save by address and stays,
    /// with two exceptions that are both said to the player: a save from before stations could
    /// be entered is looked over once, and a precinct whose building has been let (to the
    /// player or a rival) moves to another.
    ///
    /// A precinct's doors are flagged "always open" (the game's own flag: its entrance then
    /// lets the player in whatever the opening hours say), it has a marker in the game's
    /// point-of-interest list, and its frontage is dressed while the player is near
    /// (<see cref="PrecinctFrontage"/>). Nothing here runs unless LawDirector calls it.
    /// </summary>
    internal sealed class Precincts
    {
        public const int Most = 6;
        public const int Rules = 2;                  // LawState.PrecinctRules once the walk-in rules have chosen

        private const float DressWithin = 260f;      // the player this near: the frontage is dressed
        private const float UndressBeyond = 330f;
        private const int Measured = 14;             // candidates per district whose frontage is measured
        private const int MuchBetter = 30;           // an old save's precinct gives way to one that scores this much more
        private const string Hamptons = "ba:neighborhood_thehamptons";
        private const string Empty = "ba:businesstype_empty";

        private static readonly Color MarkerBlue = new Color(0.10f, 0.28f, 0.78f);
        private static int _wallMask;

        private struct Candidate
        {
            public CityBuildingController Building;
            public Vector3 Door;
            public bool WalkIn;
            public int Rank;
            public float Sqr;
            public string Address;
            public int Score;
        }

        private readonly List<Precinct> _all = new List<Precinct>(Most);
        private readonly List<Candidate> _candidates = new List<Candidate>(64);
        private readonly IModLogger _logger;
        private readonly Transform _root;
        private readonly PrecinctFrontage _frontage;
        private Sprite _icon;
        private MapPlaces.Category _map;
        private bool _chosen;
        private int _nextLook;
        private int _attempts;

        public Precincts(Transform root, IModLogger logger)
        {
            _root = root;
            _logger = logger;
            _frontage = new PrecinctFrontage(root, logger);
        }

        public int Count => _all.Count;

        public bool Chosen => _chosen;

        public Precinct At(int index) => index >= 0 && index < _all.Count ? _all[index] : null;

        /// <summary>The precinct whose frontage is dressed just now, or null.</summary>
        public Precinct Dressed => _frontage.Dressed;

        /// <summary>The badge the precincts carry on the map (and the records desk on the phone).</summary>
        public Sprite Icon => _icon != null ? _icon : _icon = MakeIcon();

        /// <summary>The precinct whose door is nearest a point, or null if there are none.</summary>
        public Precinct Nearest(Vector3 at)
        {
            Precinct best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _all.Count; i++)
            {
                Vector3 offset = _all[i].Door - at;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = _all[i];
                }
            }
            return best;
        }

        /// <summary>The precinct that is this building, or null.</summary>
        public Precinct Of(Buildings.Building building)
        {
            if (building == null)
            {
                return null;
            }
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Building != null && ReferenceEquals(_all[i].Building.building, building))
                {
                    return _all[i];
                }
            }
            return null;
        }

        /// <summary>The precinct that is this building on the street, or null.</summary>
        public Precinct Of(CityBuildingController building)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (ReferenceEquals(_all[i].Building, building))
                {
                    return _all[i];
                }
            }
            return null;
        }

        /// <summary>True if the player can walk into this precinct now: it is an empty unit still, and walk-in stations are switched on.</summary>
        public static bool CanEnter(Precinct precinct)
        {
            return precinct != null && precinct.WalkIn && Settings.LawStation && Settings.LawPrecincts && precinct.Building != null && Vacant(precinct.Building);
        }

        // ---------------------------------------------------------------- which buildings

        /// <summary>Once per city load: designates the precincts. False if the city's buildings are not there yet (asked again).</summary>
        public bool Choose()
        {
            if (_chosen)
            {
                return true;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null || buildings.Length == 0)
            {
                return false;
            }
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] != null && buildings[i].building != null && buildings[i].buildingRegistration == null && _attempts < 12)
                {
                    _attempts++;
                    return false;   // the game has not tied its buildings to their registrations yet (asked again, a dozen times at most)
                }
            }
            _chosen = true;

            // The middle of each district: the mean of its buildings' doors.
            var districts = new SortedDictionary<string, Vector4>(StringComparer.Ordinal);
            for (int i = 0; i < buildings.Length; i++)
            {
                if (!Usable(buildings[i], out Vector3 door))
                {
                    continue;
                }
                string district = buildings[i].building.Neighbourhood;
                districts.TryGetValue(district, out Vector4 sum);
                districts[district] = sum + new Vector4(door.x, door.y, door.z, 1f);
            }

            // What the save says first: the same buildings as last time, by address.
            var saved = new Dictionary<string, CityBuildingController>(Most, StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(LawState.PrecinctAddresses))
            {
                foreach (string address in LawState.PrecinctAddresses.Split(';'))
                {
                    CityBuildingController found = Find(buildings, address);
                    if (found != null && Usable(found, out _) && !SpecialBuildings.Players(found) && !saved.ContainsKey(found.building.Neighbourhood))   // (one the player has since rented or bought gives way)
                    {
                        saved[found.building.Neighbourhood] = found;
                    }
                }
            }
            bool oldSave = saved.Count > 0 && LawState.PrecinctRules < Rules;
            string moved = null;
            int walkIn = 0;

            foreach (KeyValuePair<string, Vector4> district in districts)
            {
                if (_all.Count >= Most || district.Value.w < 8f)
                {
                    continue;   // a handful of buildings is not a district
                }
                Vector3 middle = new Vector3(district.Value.x, district.Value.y, district.Value.z) / district.Value.w;
                bool found = Best(buildings, district.Key, middle, null, out Candidate best);
                Candidate pick = best;
                bool had = saved.TryGetValue(district.Key, out CityBuildingController kept);
                if (had)
                {
                    Usable(kept, out Vector3 keptDoor);
                    var same = new Candidate
                    {
                        Building = kept, Door = keptDoor, WalkIn = Vacant(kept), Address = AddressOf(kept),
                        Score = Measure(kept, keptDoor)
                    };
                    // The saved building stands, unless it cannot be walked into and another can, or (an old save, once)
                    // another's frontage is far better.
                    bool gives = found && !ReferenceEquals(best.Building, kept)
                                 && ((best.WalkIn && !same.WalkIn) || (oldSave && best.WalkIn == same.WalkIn && best.Score >= same.Score + MuchBetter));
                    if (!gives)
                    {
                        pick = same;
                        found = true;
                    }
                }
                if (!found || pick.Building == null)
                {
                    continue;
                }
                var precinct = new Precinct
                {
                    Index = _all.Count,
                    Building = pick.Building,
                    District = district.Key,
                    NameKey = NameKey(district.Key),
                    Door = pick.Door,
                    Address = pick.Address,
                    WalkIn = pick.WalkIn,
                    Score = pick.Score
                };
                precinct.Name = precinct.NameKey.GetLocalization();
                _all.Add(precinct);
                walkIn += precinct.WalkIn ? 1 : 0;
                if (had && !ReferenceEquals(pick.Building, kept))
                {
                    moved = (moved != null ? moved + ", " : string.Empty) + precinct.Name;
                }
            }

            var names = new string[_all.Count];
            for (int i = 0; i < _all.Count; i++)
            {
                Precinct precinct = _all[i];
                names[i] = precinct.Name + " (" + precinct.Building.building.Address.ToFormattedString() + ", " + precinct.Building.building.BuildingType
                           + ", frontage " + precinct.Score + (precinct.WalkIn ? ", walk-in" : ", desk at the door: no empty unit in the district") + ")";
                Mark(precinct);
            }
            Remember();
            LawState.PrecinctRules = Rules;
            _logger?.Info("Law: " + _all.Count + " precinct(s), " + walkIn + " that can be walked into: " + string.Join("; ", names) + ".");
            if (moved != null && SaveGameManager.Current != null)
            {
                var data = new Dictionary<string, string>(1) { { "precincts", moved } };
                Notifications.Show(NotificationType.Info, "reallife_law_precincts_moved", data, 14f);
                _logger?.Info("Law: moved since the save was made: " + moved + ".");
            }
            return true;
        }

        private void Remember()
        {
            var addresses = new string[_all.Count];
            for (int i = 0; i < _all.Count; i++)
            {
                addresses[i] = _all[i].Address;
            }
            LawState.PrecinctAddresses = string.Join(";", addresses);
        }

        // A building that can be a precinct at all: in the city proper, with a door on the street, and not one of the game's own services.
        private static bool Usable(CityBuildingController building, out Vector3 door)
        {
            door = default;
            if (building == null || building.building == null || building.entranceDoors == null || building.building.SpecialService != null
                || GameMembers.Fenced(building.building) || string.IsNullOrEmpty(building.building.Neighbourhood)
                || building.building.Neighbourhood == Hamptons || building.building.IsHamptonsHouse())
            {
                return false;
            }
            Transform at = DoorOf(building);
            if (at == null)
            {
                return false;
            }
            door = at.position;
            return true;
        }

        /// <summary>The building's first door on the street, or null.</summary>
        public static Transform DoorOf(CityBuildingController building)
        {
            if (building == null || building.entranceDoors == null)
            {
                return null;
            }
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    return building.entranceDoors[i].doorTransform;
                }
            }
            return null;
        }

        // Standing empty and to let, with nothing of the game's inside and nothing that would keep the player out of it:
        // the one kind of unit whose interior can be a station's.
        private static bool Vacant(CityBuildingController building)
        {
            BuildingRegistration registration = building != null ? building.buildingRegistration : null;
            if (registration == null || !registration.AvailableForRent || registration.RentedByPlayer || registration.BuildingOwnedByPlayer
                || registration.businessTypeName != Empty || registration.Layout != null
                || (registration.itemInstances != null && registration.itemInstances.Count > 0))
            {
                return false;
            }
            try
            {
                return building.building.requiredDLC.DlcIsOwned();
            }
            catch (Exception)
            {
                return false;
            }
        }

        // An office first, then a large shop unit, then a warehouse, then any shop; never a home, a cinema or a theatre.
        private static int Rank(Buildings.Building building)
        {
            switch (building.BuildingType)
            {
                case "ba:buildingtype_office": return 0;
                case "ba:buildingtype_retail":
                    return building.BuildingSize == "ba:buildingsize_d" || building.BuildingSize == "ba:buildingsize_m" ? 1 : 3;
                case "ba:buildingtype_warehouse": return 2;
                default: return -1;
            }
        }

        private static string AddressOf(CityBuildingController building)
        {
            return building.building.StreetName + "|" + building.building.StreetNumber;
        }

        // The district's best building for a station: every unit of a kind that will do is listed; the likeliest few
        // (empty ones first, then by kind, then nearest the middle) have their frontage measured; the best score wins.
        private bool Best(CityBuildingController[] buildings, string district, Vector3 middle, CityBuildingController not, out Candidate best)
        {
            best = default;
            _candidates.Clear();
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (ReferenceEquals(building, not) || !Usable(building, out Vector3 door) || building.building.Neighbourhood != district)
                {
                    continue;
                }
                int rank = Rank(building.building);
                BuildingRegistration registration = building.buildingRegistration;
                string holder = SpecialBuildings.HolderOf(building);
                if (holder != null && !holder.StartsWith("police_", StringComparison.Ordinal))
                {
                    continue;   // (a building another feature has made its own: a tech company's headquarters, the university)
                }
                // Never the player's own premises, and somewhere with a pavement at the door.
                if (rank < 0 || registration == null || registration.RentedByPlayer || registration.BuildingOwnedByPlayer || !StreetSpots.OnPavement(door, 6f, out _))
                {
                    continue;
                }
                Vector3 offset = door - middle;
                offset.y = 0f;
                _candidates.Add(new Candidate
                {
                    Building = building, Door = door, WalkIn = Vacant(building), Rank = rank,
                    Sqr = Mathf.Round(offset.sqrMagnitude),   // whole square metres: two runs never disagree over a rounding error
                    Address = AddressOf(building)
                });
            }
            if (_candidates.Count == 0)
            {
                return false;
            }
            _candidates.Sort(Likelier);
            int measured = Mathf.Min(Measured, _candidates.Count);
            int pick = 0;
            for (int i = 0; i < measured; i++)
            {
                Candidate candidate = _candidates[i];
                candidate.Score = Measure(candidate.Building, candidate.Door);
                _candidates[i] = candidate;
                // An empty unit always before one that is not; then the score; then the order they were listed in.
                Candidate held = _candidates[pick];
                if (i > 0 && candidate.WalkIn == held.WalkIn && candidate.Score > held.Score)
                {
                    pick = i;
                }
            }
            best = _candidates[pick];
            _candidates.Clear();
            return true;
        }

        private static int Likelier(Candidate a, Candidate b)
        {
            if (a.WalkIn != b.WalkIn)
            {
                return a.WalkIn ? -1 : 1;
            }
            if (a.Rank != b.Rank)
            {
                return a.Rank.CompareTo(b.Rank);
            }
            return a.Sqr != b.Sqr ? a.Sqr.CompareTo(b.Sqr) : string.CompareOrdinal(a.Address, b.Address);
        }

        // How well a frontage takes the dressing, from 0 to about 100: a sign band of its own (25), flat wall either side
        // of the door at chest height (3 a place, eight places) and above head height (2 a place, four), the pavement's
        // depth (5 a metre step, three) and a kerb with the road beyond it (10); a little for the kind of building.
        private static int Measure(CityBuildingController building, Vector3 door)
        {
            try
            {
                if (_wallMask == 0)
                {
                    _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
                }
                int rank = Rank(building.building);
                int score = rank == 0 ? 8 : rank == 1 ? 6 : rank == 3 ? 4 : 2;
                score += building.HasHorizontalSigns ? 25 : 0;
                if (!StreetSpots.OnPavement(door, 6f, out Vector3 front))
                {
                    return score;
                }
                Transform at = DoorOf(building);
                Vector3 toFront = front - door;
                toFront.y = 0f;
                Vector3 outward = at != null ? at.forward : toFront;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = toFront;
                }
                else if (Vector3.Dot(outward, toFront) < 0f)
                {
                    outward = -outward;
                }
                if (outward.sqrMagnitude < 0.01f)
                {
                    return score;
                }
                outward.Normalize();
                Vector3 across = Vector3.Cross(Vector3.up, outward);
                Vector3 foot = new Vector3(door.x, front.y, door.z);
                if (_wallMask != 0)
                {
                    float first = float.NaN;
                    for (int i = 0; i < 8; i++)
                    {
                        float aside = (1.4f + (i / 2) * 0.8f) * ((i & 1) == 0 ? -1f : 1f);
                        for (int level = 0; level < (i < 4 ? 2 : 1); level++)
                        {
                            Vector3 from = foot + across * aside + outward * 1.4f + Vector3.up * (level == 0 ? 1.5f : 2.6f);
                            if (!Physics.Raycast(from, -outward, out RaycastHit hit, 3f, _wallMask, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, outward) < 0.88f)
                            {
                                continue;
                            }
                            if (float.IsNaN(first))
                            {
                                first = hit.distance;
                            }
                            if (Mathf.Abs(hit.distance - first) < 0.2f)
                            {
                                score += level == 0 ? 3 : 2;
                            }
                        }
                    }
                }
                for (int step = 0; step < 3; step++)
                {
                    score += NavMesh.SamplePosition(foot + outward * (1.5f + step), out _, 0.4f, StreetSpots.Pavement) ? 5 : 0;
                }
                if (NavMesh.Raycast(front, front + outward * 9f, out NavMeshHit edge, StreetSpots.Pavement)
                    && !NavMesh.SamplePosition(edge.position + outward * 1.1f, out _, 0.35f, StreetSpots.Pavement)
                    && StreetSpots.OnRoad(edge.position + outward * 1.1f, 0.6f, out _))
                {
                    score += 10;
                }
                return score;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static CityBuildingController Find(CityBuildingController[] buildings, string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return null;
            }
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (building != null && building.building != null && address == AddressOf(building))
                {
                    return building;
                }
            }
            return null;
        }

        private static string NameKey(string district)
        {
            switch (district)
            {
                case "ba:neighborhood_garmentdistrict": return "reallife_law_precinct_garmentdistrict";
                case "ba:neighborhood_hellskitchen": return "reallife_law_precinct_hellskitchen";
                case "ba:neighborhood_industrycity": return "reallife_law_precinct_industrycity";
                case "ba:neighborhood_lowermanhattan": return "reallife_law_precinct_lowermanhattan";
                case "ba:neighborhood_midtown": return "reallife_law_precinct_midtown";
                case "ba:neighborhood_murrayhill": return "reallife_law_precinct_murrayhill";
                default: return "reallife_law_precinct_city";
            }
        }

        // ---------------------------------------------------------------- staying a station

        /// <summary>
        /// Every few seconds: a precinct whose unit has been let (to the player, to a rival) moves to another empty one in
        /// its district and says so, and one with its desk at the door moves off a building that has become the player's; the doors of the ones that can be walked into are kept flagged open, and put back as
        /// the game had them when walk-in stations are switched off.
        /// </summary>
        /// <param name="inside">The precinct the player is standing in, which is left alone until they have gone.</param>
        public void Keep(Precinct inside)
        {
            if (++_nextLook < 5)
            {
                return;
            }
            _nextLook = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                Precinct precinct = _all[i];
                if (precinct != inside && !Custody.Active && (precinct.WalkIn ? !Vacant(precinct.Building) : !precinct.Stays && SpecialBuildings.Players(precinct.Building)))
                {
                    Move(precinct);
                }
                Doors(precinct, CanEnter(precinct));
            }
        }

        private void Move(Precinct precinct)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null)
            {
                return;
            }
            Vector4 sum = Vector4.zero;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (Usable(buildings[i], out Vector3 door) && buildings[i].building.Neighbourhood == precinct.District)
                {
                    sum += new Vector4(door.x, door.y, door.z, 1f);
                }
            }
            Vector3 middle = sum.w > 0f ? new Vector3(sum.x, sum.y, sum.z) / sum.w : precinct.Door;
            Doors(precinct, false);
            bool players = SpecialBuildings.Players(precinct.Building);   // (off the player's building to any other that will do, empty or not)
            if (!Best(buildings, precinct.District, middle, precinct.Building, out Candidate best) || (!best.WalkIn && !players))
            {
                // Nowhere empty to go: it stays where it is, with its desk at the door (and is not looked at again for it).
                if (precinct.WalkIn)
                {
                    _logger?.Info("Law: " + precinct.Name + ": its unit has been let and the district has no other empty one; it stays, with the front desk at its door.");
                }
                precinct.WalkIn = false;
                precinct.Stays = players;
                return;
            }
            if (_frontage.Dressed == precinct)
            {
                _frontage.Undress();
            }
            precinct.Building = best.Building;
            precinct.Door = best.Door;
            precinct.Address = best.Address;
            precinct.WalkIn = best.WalkIn;
            precinct.Score = best.Score;
            Mark(precinct);
            Remember();
            string address = best.Building.building.Address.ToFormattedString();
            _logger?.Info("Law: " + precinct.Name + " has moved to " + address + " (its old unit was let); frontage " + precinct.Score + ".");
            if (SaveGameManager.Current != null)
            {
                var data = new Dictionary<string, string>(2) { { "precinct", precinct.Name }, { "address", address } };
                Notifications.Show(NotificationType.Info, "reallife_law_precinct_moved", data, 12f);
            }
        }

        // The game's own flag on an entrance: with it set, the building's own door lets the player in whatever its
        // registration says. Set on a precinct's doors only, remembered, and put back.
        private static void Doors(Precinct precinct, bool open)
        {
            if (precinct.DoorsOpen == open || precinct.Building == null || precinct.Building.entranceDoors == null)
            {
                return;
            }
            BuildingEntranceDoor[] doors = precinct.Building.entranceDoors;
            if (open)
            {
                precinct.DoorWas = new bool[doors.Length];
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        precinct.DoorWas[i] = doors[i].alwaysOpen;
                        doors[i].alwaysOpen = true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        doors[i].alwaysOpen = precinct.DoorWas != null && i < precinct.DoorWas.Length && precinct.DoorWas[i];
                    }
                }
                precinct.DoorWas = null;
            }
            precinct.DoorsOpen = open;
        }

        // ---------------------------------------------------------------- the map

        // On the city map: a place of the mod's "Police stations" category (Core/MapPlaces), which gives it a marker with its
        // name, a row in the map's filters, a click for a destination, and its name on the game's card for the building.
        private void Mark(Precinct precinct)
        {
            // Its own name wherever the game names the building (Core/SpecialBuildings); a precinct that moves is set again.
            SpecialBuildings.Set("police_" + precinct.Index, precinct.Building, SpecialKind.Police, precinct.NameKey, "reallife_law_card_type", () => Settings.LawPrecincts);
            try
            {
                if (_map == null)
                {
                    _map = MapPlaces.Register("police", "reallife_law_map_stations", MarkerBlue, MapIcon.Shield, onByDefault: true, inStreet: true);
                    _map.Available = () => Settings.LawPrecincts && Settings.LawMapMarkers;
                }
                if (precinct.Place != null)
                {
                    _map.Remove(precinct.Place);
                }
                precinct.Place = new MapPlace(precinct.Door, precinct.Name, precinct.Building)
                {
                    CardTypeKey = "reallife_law_card_type",
                    CardStateKey = "reallife_law_card_open"
                };
                _map.Add(precinct.Place);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Law: the precinct could not be put on the city map; it is still there in the street.");
            }
        }

        // A white five-pointed badge star on nothing: the marker's own blob is the blue behind it. Made once.
        private static Sprite MakeIcon()
        {
            const int size = 64;
            var corners = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i & 1) == 0 ? 29f : 12.5f;
                corners[i] = new Vector2(size * 0.5f + Mathf.Cos(angle) * radius, size * 0.5f + Mathf.Sin(angle) * radius);
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Four samples a pixel: a soft edge without a filter.
                    int inside = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        var point = new Vector2(x + 0.25f + (s & 1) * 0.5f, y + 0.25f + (s >> 1) * 0.5f);
                        bool odd = false;
                        for (int i = 0, j = 9; i < 10; j = i++)
                        {
                            if ((corners[i].y > point.y) != (corners[j].y > point.y)
                                && point.x < (corners[j].x - corners[i].x) * (point.y - corners[i].y) / (corners[j].y - corners[i].y) + corners[i].x)
                            {
                                odd = !odd;
                            }
                        }
                        inside += odd ? 1 : 0;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 4));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Precinct Badge" };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = "RealLifeOverhaul Precinct Badge";
            return sprite;
        }

        // ---------------------------------------------------------------- the frontage

        /// <summary>
        /// Where somebody let out of a precinct stands: on the pavement in front of its door, a
        /// step out, clear of vehicles, and the way from the door to the street. False if no such
        /// place could be found (the caller then uses the game's own spot for the building).
        /// </summary>
        public static bool Front(Precinct precinct, out Vector3 spot, out Vector3 outward)
        {
            spot = default;
            outward = Vector3.forward;
            if (precinct == null || precinct.Building == null || !JobSpots.Front(precinct.Building, out spot))
            {
                return false;
            }
            outward = spot - precinct.Door;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.04f)
            {
                outward = -precinct.Building.transform.forward;
                outward.y = 0f;
            }
            outward = outward.sqrMagnitude < 0.01f ? Vector3.forward : outward.normalized;
            return true;
        }

        /// <summary>Once a second while the player is out of doors: dresses the precinct they are near, undresses the one they left.</summary>
        public void Tick(float now, Vector3 player)
        {
            Precinct near = Settings.LawPrecincts ? Nearest(player) : null;
            float sqr = near != null ? Flat(near.Door - player) : float.MaxValue;
            Precinct dressed = _frontage.Dressed;
            if (dressed != null && (near != dressed || sqr > UndressBeyond * UndressBeyond))
            {
                // Only ever taken in from far off: the nearest precinct changes half way between two of them.
                _frontage.Undress();
            }
            if (near == null || sqr > DressWithin * DressWithin)
            {
                return;
            }
            _frontage.Tick(now, player, near);
        }

        /// <summary>Dresses a precinct at once, in view or not: called with the screen dark, just before the player is put outside it.</summary>
        public void DressNow(Precinct precinct)
        {
            if (precinct != null && Settings.LawPrecincts)
            {
                _frontage.DressNow(precinct);
            }
        }

        /// <summary>The frontage's people off the street: the player has gone indoors, and the same few characters are wanted inside.</summary>
        public void PeopleIn()
        {
            _frontage.PeopleIn();
        }

        /// <summary>Everything off the street. Safe at any time, any number of times.</summary>
        public void Undress()
        {
            _frontage.Undress();
        }

        /// <summary>What is up at the dressed precinct and what was left off, for the log.</summary>
        public string Report() => _frontage.Report();

        public void Dispose()
        {
            _frontage.Dispose();
            for (int i = 0; i < _all.Count; i++)
            {
                try
                {
                    Doors(_all[i], false);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
                if (_all[i].Place != null && _map != null)
                {
                    _map.Remove(_all[i].Place);
                    _all[i].Place = null;
                }
                SpecialBuildings.Remove("police_" + _all[i].Index);
            }
            _all.Clear();
        }

        private static float Flat(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }
    }
}
