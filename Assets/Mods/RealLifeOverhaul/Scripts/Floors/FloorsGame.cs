using System;
using System.Collections.Generic;
using Blueprints;
using Buildings;
using Buildings.Outdoors;
using Entities;
using Helpers;
using PlayerActivity;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// Everything the floors read from or do to the game. A building's interior is the game's shared structure for its size and
    /// version (BuildingManager.EnterBuilding loads it by BuildingSizeInfo), so a floor on another structure has the building's
    /// size and version read as that one's while the game loads it, and put back the moment it has (FloorLoan says when). Written,
    /// and given back on every way out: the building's size and version, its registration's dirt spots (the game rewrites them for
    /// the structure it loads), the manager's registration while on a floor, the entrance doors' "always open". Never per frame.
    /// </summary>
    internal static partial class FloorsGame
    {
        public const string Office = "ba:buildingtype_office";
        public const string Retail = "ba:buildingtype_retail";
        private const string Empty = "ba:businesstype_empty";
        private const string SizeT = "ba:buildingsize_t";             // the Hamptons' houses: entered another way (EnterHamptonsBuilding)
        private const string SizeParking = "ba:buildingsize_parking";

        public static BuildingManager Manager => InstanceBehavior<BuildingManager>.Instance;

        /// <summary>True while the game is going into a building (its fade, the exit first from another, the load).</summary>
        public static bool Entering
        {
            get
            {
                BuildingManager m = Manager;
                return m != null && m.enteringBuilding;
            }
        }

        public static bool Exiting
        {
            get
            {
                BuildingManager m = Manager;
                return m != null && m.exitingBuilding;
            }
        }

        /// <summary>The building the player is inside, as the floors key it (street#number); null outdoors or in a car park.</summary>
        public static string InsideKey()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            BuildingManager m = Manager;
            Building b = m != null ? m.building : null;
            return b != null ? FloorsRules.Key(b.StreetName, b.StreetNumber) : null;
        }

        public static string KeyOf(CityBuildingController b) => b != null && b.building != null ? FloorsRules.Key(b.building.StreetName, b.building.StreetNumber) : null;

        public static string KeyOf(Address a) => a != null ? FloorsRules.Key(a.streetName, a.streetNumber) : null;

        /// <summary>A building that can be a host: its asset, an entrance door, a size the game enters by its own door (no Hamptons house, no car park).</summary>
        public static bool Usable(CityBuildingController b)
        {
            Building asset = b != null ? b.building : null;
            return asset != null && !string.IsNullOrEmpty(asset.StreetName) && !string.IsNullOrEmpty(asset.BuildingSize) && asset.BuildingSize != SizeT
                   && asset.BuildingSize != SizeParking && !asset.IsHamptonsHouse() && b.entranceDoors != null && b.entranceDoors.Length > 0 && b.entranceDoors[0] != null
                   && b.entranceDoors[0].doorTransform != null;
        }

        /// <summary>The building's address as the game writes it.</summary>
        public static string Address(CityBuildingController b)
        {
            return b != null && b.building != null && b.building.Address != null ? b.building.Address.ToFormattedString() : "?";
        }

        public static int Sqm(CityBuildingController b) => b != null && b.building != null ? b.building.totalSqm : 0;

        // ---------------------------------------------------------------- structures

        public static string SizeOf(CityBuildingController b) => b != null && b.building != null ? b.building.BuildingSize : null;

        public static int VersionOf(CityBuildingController b) => b != null && b.building != null ? b.building.BuildingVersion : 0;

        /// <summary>A structure as the game writes it ("D1").</summary>
        public static string Words(string size, int version)
        {
            try
            {
                return string.IsNullOrEmpty(size) ? "?" : new BuildingSizeInfo(size, version).ToString();
            }
            catch (Exception)
            {
                return size + " " + version;
            }
        }

        /// <summary>One storey's floor area of a size: the game's interior for it, in square metres; 0 when the game does not say.</summary>
        public static int StoreySqm(string size)
        {
            try
            {
                BuildingSizeData data = string.IsNullOrEmpty(size) ? null : BuildingSizeHelper.GetData(size);
                return data != null ? Mathf.Max(0, Mathf.RoundToInt(data.squareMeters)) : 0;
            }
            catch (Exception)
            {
                return 0;   // a size the game's data has not loaded: the building holds its holder's own floors
            }
        }

        /// <summary>True if the game has an interior for a size and version: one of its building types lists it.</summary>
        public static bool Has(string size, int version)
        {
            return Listed(Office, size, version) || Listed(Retail, size, version) || Listed("ba:buildingtype_warehouse", size, version)
                   || Listed("ba:buildingtype_residential", size, version) || Listed("ba:buildingtype_special", size, version);
        }

        private static bool Listed(string type, string size, int version)
        {
            List<BuildingSizeInfo> list = BuildingSizeHelper.GetBuildingVersionsByBuildingType(type);
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] != null && list[i].buildingVersion == version && string.Equals(list[i].buildingSize, size, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The game's office structure (else a shop's) with the largest storey that is neither <paramref name="notSize"/>/<paramref name="notVersion"/>
        /// nor another unwanted one, ties by name: the same on every machine. Null when the game lists none.
        /// </summary>
        public static FloorStructure OtherStructure(string notSize, int notVersion, string alsoNotSize, int alsoNotVersion)
        {
            FloorStructure best = Largest(Office, notSize, notVersion, alsoNotSize, alsoNotVersion);
            return best ?? Largest(Retail, notSize, notVersion, alsoNotSize, alsoNotVersion);
        }

        private static FloorStructure Largest(string type, string notSize, int notVersion, string alsoNotSize, int alsoNotVersion)
        {
            List<BuildingSizeInfo> list = BuildingSizeHelper.GetBuildingVersionsByBuildingType(type);
            BuildingSizeInfo best = null;
            int bestSqm = -1;
            string bestName = null;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                BuildingSizeInfo s = list[i];
                if (s == null || string.IsNullOrEmpty(s.buildingSize) || s.buildingSize == SizeT || s.buildingSize == SizeParking
                    || (s.buildingVersion == notVersion && s.buildingSize == notSize) || (s.buildingVersion == alsoNotVersion && s.buildingSize == alsoNotSize))
                {
                    continue;
                }
                int sqm = StoreySqm(s.buildingSize);
                string name = s.ToString();
                if (sqm > bestSqm || (sqm == bestSqm && string.CompareOrdinal(name, bestName) < 0))
                {
                    best = s;
                    bestSqm = sqm;
                    bestName = name;
                }
            }
            return best != null ? new FloorStructure(best.buildingSize, best.buildingVersion) : null;
        }

        /// <summary>The building's own size and version read as another structure's, until put back (FloorsDirector's loan).</summary>
        public static void ReadAs(Building asset, string size, int version)
        {
            if (asset == null || string.IsNullOrEmpty(size))
            {
                return;
            }
            asset.BuildingSize = size;
            asset.BuildingVersion = version;
        }

        // ---------------------------------------------------------------- registrations

        /// <summary>A building's registration, as the game finds it by address (its private lookup first): a made building's too.</summary>
        public static BuildingRegistration RegistrationOf(CityBuildingController b)
        {
            if (b == null || b.building == null || b.building.Address == null)
            {
                return null;
            }
            try
            {
                return BuildingHelper.GetBuildingRegistration(b.building.Address);
            }
            catch (Exception)
            {
                return b.buildingRegistration;   // the address unknown to the game's lookup: the controller's own
            }
        }

        public static BuildingRegistration ManagerRegistration
        {
            get
            {
                BuildingManager m = Manager;
                return m != null ? m.buildingRegistration : null;
            }
            set
            {
                BuildingManager m = Manager;
                if (m != null)
                {
                    m.buildingRegistration = value;
                }
            }
        }

        public static List<DirtSpot> DirtOf(BuildingRegistration r) => r != null ? r.dirtSpots : null;

        public static void PutDirt(BuildingRegistration r, List<DirtSpot> kept)
        {
            if (r != null)
            {
                r.dirtSpots = kept;
            }
        }

        /// <summary>
        /// The manager's registration for a floor on a borrowed structure: the mod's own, in no list of the save. Its dirt spots are
        /// the ones the game just made for the structure on show; its address, designs and landlord the building's.
        /// </summary>
        public static BuildingRegistration PlainShadow(BuildingRegistration real, CityBuildingController b, string name)
        {
            var r = new BuildingRegistration
            {
                StreetName = b != null && b.building != null ? b.building.StreetName : real != null ? real.StreetName : null,
                StreetNumber = b != null && b.building != null ? b.building.StreetNumber : real != null ? real.StreetNumber : 0,
                BusinessName = name,
                businessTypeName = Empty,
                AvailableForRent = false,
                RentedByPlayer = false
            };
            if (real != null)
            {
                r.dirtSpots = real.dirtSpots;
                r.interiorDesigns = real.interiorDesigns;
                r.buildingOwnerRivalId = real.buildingOwnerRivalId;
                r.scheduleDays = real.scheduleDays;
            }
            return r;
        }

        /// <summary>The tag Going Public tells players apart indoors by, from the manager's registration ("26 ba:street_secondavenue": number and street).</summary>
        public static string TagOf(BuildingRegistration r)
        {
            return r != null ? r.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + r.StreetName : string.Empty;
        }

        /// <summary>True if a registration is in the save's list (what the game writes): a search of it, at a save only.</summary>
        public static bool InSaveList(BuildingRegistration r)
        {
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (ReferenceEquals(all[i], r))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Standing empty: nobody rents or owns it (the player, another player, a rival), nothing of the game's in it, no layout left.</summary>
        public static bool Vacant(BuildingRegistration reg)
        {
            return reg != null && reg.AvailableForRent && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && string.IsNullOrEmpty(reg.businessOwnerRivalId)
                   && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty) && reg.Layout == null
                   && (reg.itemInstances == null || reg.itemInstances.Count == 0);
        }

        // ---------------------------------------------------------------- going in, the lift, out

        /// <summary>The door of a building nearest the player (the game's door id).</summary>
        public static int NearestDoor(CityBuildingController building)
        {
            Vector3 at = PlayerHelper.GetPosition();
            int best = building.entranceDoors[0].doorId;
            float near = float.MaxValue;
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                Transform t = building.entranceDoors[i] != null ? building.entranceDoors[i].doorTransform : null;
                if (t != null && (t.position - at).sqrMagnitude < near)
                {
                    near = (t.position - at).sqrMagnitude;
                    best = building.entranceDoors[i].doorId;
                }
            }
            return best;
        }

        /// <summary>The game goes into the building by a door: from inside another it leaves that one first, behind its own fade.</summary>
        public static void Enter(CityBuildingController building, int door)
        {
            Manager.EnterBuilding(building.building, false, false, 0, door);
        }

        /// <summary>The game takes the player out by a door to the street.</summary>
        public static void Exit(int door)
        {
            Manager?.ExitFromBuilding(door);
        }

        /// <summary>Where the game puts the player who comes in by a door: its exit zone's spawn point, else the first, as BuildingManager.GetFirstExitZone.</summary>
        public static bool Spawn(int door, out Vector3 at, out Quaternion turn)
        {
            at = Vector3.zero;
            turn = Quaternion.identity;
            BuildingManager m = Manager;
            List<ExitZone> zones = m != null ? m.exitZones : null;
            ExitZone found = null;
            for (int i = 0; door >= 0 && zones != null && i < zones.Count && found == null; i++)
            {
                if (zones[i] != null && zones[i].despawner != null && zones[i].despawner.exitToZoneId == door && zones[i].playerSpawnPoint != null)
                {
                    found = zones[i];
                }
            }
            for (int i = 0; zones != null && i < zones.Count && found == null; i++)
            {
                if (zones[i] != null && zones[i].isPrimarySpawnPoint && zones[i].playerSpawnPoint != null)
                {
                    found = zones[i];
                }
            }
            for (int i = 0; zones != null && i < zones.Count && found == null; i++)
            {
                if (zones[i] != null && zones[i].playerSpawnPoint != null && !zones[i].isDriveInBay)
                {
                    found = zones[i];
                }
            }
            if (found == null)
            {
                return false;
            }
            at = found.playerSpawnPoint.position;
            turn = found.playerSpawnPoint.rotation;
            return true;
        }

        /// <summary>The player put at a place indoors, as the game moves them (BuildingManager.MovePlayerToPoint), turned when asked.</summary>
        public static void Warp(Vector3 at, Quaternion turn, bool turned)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            player.ResetNavigation();
            player.Character.navmeshAgent.Warp(at);
            if (turned)
            {
                player.transform.rotation = turn;
            }
        }

        public static bool PlayerAt(out Vector3 at, out Quaternion turn)
        {
            PlayerController player = PlayerHelper.PlayerController;
            at = player != null ? player.transform.position : Vector3.zero;
            turn = player != null ? player.transform.rotation : Quaternion.identity;
            return player != null;
        }

        public static bool InVehicle => PlayerHelper.IsUsingVehicle;

        /// <summary>True once a save is in play and the mod's part of it read, with no load under way.</summary>
        public static bool Loaded => SaveGameManager.Current != null && !UI.Load.LoadScene.isLoading && ModState.Loaded;

        /// <summary>True while the player is inside a building (a car park is none).</summary>
        public static bool InsideBuilding => BuildingManager.IsInsideBuilding;

        /// <summary>True while the game's placement mode is on (the floors' furniture hides for it).</summary>
        public static bool Placing => BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode;

        /// <summary>The game's black screen coming down, and going again, over a lift (UI.UiFader, as the game's doors use it).</summary>
        public static System.Collections.IEnumerator Fade(float seconds) => UI.UiFader.Fade(seconds);

        public static System.Collections.IEnumerator UnFade(float seconds) => UI.UiFader.UnFade(seconds);

        /// <summary>The place key of a place's floor for a desk there (FitOut/FitDesks): the building's, and the place's own beside it.</summary>
        public static string DeskPlace(string placeId)
        {
            BuildingManager m = Manager;
            return Tech.HqTowers.FloorPlace(m != null ? Tech.TalentGame.Place(m.buildingRegistration) : null, placeId);
        }

        /// <summary>True while the city's scene is going, or a load has begun: nothing is asked of the game's building manager then.</summary>
        public static bool CityGoing => GameManager.isCitySceneBeingUnloaded || UI.Load.LoadScene.isLoading || Manager == null;

        /// <summary>True while a screen of the game's, its activity panel or a job's prompt has the player: no panel of the floors' then.</summary>
        public static bool Busy()
        {
            return ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen || JobDirector.TakesInteractKey;
        }

        /// <summary>True when the game is not in the middle of a screen that keeps the player: a lift's or a test's going in may begin.</summary>
        public static bool Free()
        {
            return !ThirdPersonCamera.IsUiOpen() && !PlayerActivityUI.IsPanelOpen;
        }

        // ---------------------------------------------------------------- doors

        /// <summary>
        /// The building's entrances let the player in whatever the registration says (the game's "always open" on each door) while
        /// its holder is open, and are put back as they were found when it closes or is let go.
        /// </summary>
        public static void Doors(FloorHost host, bool open)
        {
            CityBuildingController b = host.Building;
            if (!ReferenceEquals(host.DoorsOf, b))
            {
                PutDoorsBack(host);
                host.DoorsOf = b;
                host.DoorWas = null;
            }
            BuildingEntranceDoor[] doors = b != null ? b.entranceDoors : null;
            if (doors == null || open == host.DoorsOpen)
            {
                return;
            }
            if (host.DoorWas == null || host.DoorWas.Length != doors.Length)
            {
                host.DoorWas = new bool[doors.Length];
                for (int i = 0; i < doors.Length; i++)
                {
                    host.DoorWas[i] = doors[i] != null && doors[i].alwaysOpen;
                }
            }
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                {
                    doors[i].alwaysOpen = open || (i < host.DoorWas.Length && host.DoorWas[i]);
                }
            }
            host.DoorsOpen = open;
            host.DoorsChanged(open);
        }

        /// <summary>Every entrance of the host's building as it was found.</summary>
        public static void PutDoorsBack(FloorHost host)
        {
            CityBuildingController b = host.DoorsOf;
            BuildingEntranceDoor[] doors = b != null ? b.entranceDoors : null;
            if (doors != null && host.DoorsOpen)
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        doors[i].alwaysOpen = host.DoorWas != null && i < host.DoorWas.Length && host.DoorWas[i];
                    }
                }
                host.DoorsChanged(false);
            }
            host.DoorsOpen = false;
            host.DoorWas = null;
        }

        // ---------------------------------------------------------------- a building taken for places

        /// <summary>
        /// The largest office building that stands empty and that nobody holds (no feature of the mod's, its reservations included,
        /// no conversion, not the player's or another player's), ties by address: the same on every machine. Null for none.
        /// </summary>
        public static CityBuildingController PickClaim(Func<string, int, bool> taken)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            if (all == null || all.Length == 0)
            {
                return null;
            }
            int pick = SpecialNameBook.PickLargest(all.Length, i => Claimable(all[i]), i => all[i].building.totalSqm, i => all[i].building.StreetName,
                i => all[i].building.StreetNumber, taken);
            return pick >= 0 ? all[pick] : null;
        }

        /// <summary>An office of the city's own that stands empty and can be walked into.</summary>
        public static bool Claimable(CityBuildingController b)
        {
            return Usable(b) && b.building.SpecialService == null && b.building.BuildingType == Office && !GameMembers.Fenced(b.building) && (int)b.building.requiredDLC == 0
                   && b.building.totalSqm > 0 && !Offices.Conversions.Holds(b.building) && !SpecialBuildings.Players(b) && Vacant(RegistrationOf(b));
        }

        /// <summary>Where the player stands outside a building to choose a floor: its pavement in front of the door, else the door.</summary>
        public static Vector3 Front(CityBuildingController b)
        {
            if (JobSpots.Front(b, out Vector3 front))
            {
                return front;
            }
            for (int i = 0; b.entranceDoors != null && i < b.entranceDoors.Length; i++)
            {
                if (b.entranceDoors[i] != null && b.entranceDoors[i].doorTransform != null)
                {
                    return b.entranceDoors[i].doorTransform.position;
                }
            }
            return b.transform.position;
        }
    }
}
