using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Buildings;
using Entities;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's buildings on the game's side: the ground's plan read as the rules read it, the game's own structures for a
    /// lot, the declarations the made buildings' engine stands as buildings of the game (MadeSpec.Life), each standing one
    /// put in the game's lists (BuildingHelper.allBuildings: the city's generator, the rivals, the walkers; the district's
    /// list: the game's daily pass), a carving box over each on the walking meshes, and the district's first life (its
    /// stats and the game's own generator for it, seeded; for buildings new to a save it lived in, for those alone) and a
    /// finished building's first load. Nothing here runs a frame.
    /// </summary>
    internal static partial class LifeGame
    {
        public const float AwayX = 0f;   // the way Uptown runs from Manhattan, a unit direction on the ground: north
        public const float AwayZ = 1f;
        private const float CutInset = 0.5f;   // m inside a design's footprint on each side: a carve reaches the walker's radius beyond its box, so its edge lands at the walls

        private static readonly List<Building> Entered = new List<Building>(512);   // the records this load put in the game's lists of buildings
        private static readonly List<NavMeshObstacle> Cuts = new List<NavMeshObstacle>(512);
        private static readonly List<Building> AllWas = new List<Building>(2048);   // the game's list of all buildings as it was, while its generator sees the new ones alone
        private static readonly HashSet<Building> Fresh = new HashSet<Building>();

        /// <summary>The plan's lots as the rules read them, into the caller's list (cleared), in the plan's own order of their ids.</summary>
        public static void Lots(List<BuildingsLot> into)
        {
            into.Clear();
            Add(UptownLots.All, into);
            Add(UptownLots.Places, into);   // the big places' ground, after the lots: BigPlacesRules.Holds keeps it from every ordinary building
        }

        private static void Add(IReadOnlyList<Lot> all, List<BuildingsLot> into)
        {
            for (int i = 0; all != null && i < all.Count; i++)
            {
                Lot lot = all[i];
                if (lot == null || string.IsNullOrEmpty(lot.Id))
                {
                    continue;
                }
                into.Add(new BuildingsLot
                {
                    Id = lot.Id, Street = lot.Street, Number = lot.Number, Block = lot.Block, First = lot.First, Avenue = BuildingsRules.FrontsAvenue(lot.Facing),
                    X = lot.X, Z = lot.Z, Facing = lot.Facing, Width = lot.Width, Depth = lot.Depth, Class = ClassOf(lot.Class), Sizes = lot.Sizes, Version = lot.Version,
                    Spaces = lot.Spaces, Strip = lot.Strip, Index = lot.Index, Open = lot.Open
                });
            }
        }

        /// <summary>
        /// True when the save holds the records of a building at the lot's address from an earlier load (the district save
        /// keeps its registration): a building a save has stands on, whatever a later plan leaves open there.
        /// </summary>
        public static bool Kept(BuildingsLot lot)
        {
            return DistrictSave.DistrictSaveDirector.KeptRegistration(lot.Street, lot.Number) != null;
        }

        /// <summary>
        /// True when the registration the district save keeps for a lot's address is someone's: the player's, rented or
        /// owned, or another player's in a Going Public session. A big special place never takes such a lot.
        /// </summary>
        public static bool Someones(BuildingsLot lot)
        {
            BuildingRegistration reg = DistrictSave.DistrictSaveDirector.KeptRegistration(lot.Street, lot.Number);
            return reg != null && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer || GoingPublic.Foreign(reg));
        }

        private static BuildingsClass ClassOf(LotClass kind)
        {
            switch (kind)
            {
                case LotClass.Office:
                    return BuildingsClass.Office;
                case LotClass.Big:
                    return BuildingsClass.Big;
                case LotClass.Warehouse:
                    return BuildingsClass.Warehouse;
                case LotClass.Theatre:
                    return BuildingsClass.Theatre;
                case LotClass.Cinema:
                    return BuildingsClass.Cinema;
                default:
                    return BuildingsClass.Small;
            }
        }

        /// <summary>
        /// Every structure the game has for a lot's sizes, read at this load (BuildingSizeHelper, never assumed): each size's
        /// versions that are not kept for the game's special buildings (only the plan's version where the lot has one: its
        /// spaces are that version's), and the types each takes but the special one; in the rules' order (size, type,
        /// version), into the caller's list (cleared).
        /// </summary>
        public static void Structures(BuildingsLot lot, List<BuildingsStructure> into)
        {
            into.Clear();
            List<BuildingSizeData> sizes = BuildingSizeHelper.GetAllBuildingSizes();
            for (int s = 0; lot.Sizes != null && s < lot.Sizes.Count; s++)
            {
                for (int i = 0; sizes != null && i < sizes.Count; i++)
                {
                    BuildingSizeData data = sizes[i];
                    if (data == null || data.buildingSize != lot.Sizes[s] || data.buildingVersions == null)
                    {
                        continue;
                    }
                    for (int v = 0; v < data.buildingVersions.Length; v++)
                    {
                        BuildingVersion each = data.buildingVersions[v];
                        if (each == null || each.specialBuildingOnly || each.supportedBuildingTypes == null || (lot.Version > 0 && each.number != lot.Version))
                        {
                            continue;
                        }
                        for (int t = 0; t < each.supportedBuildingTypes.Count; t++)
                        {
                            string type = each.supportedBuildingTypes[t];
                            if (!string.IsNullOrEmpty(type) && type != BuildingsRules.SpecialType)
                            {
                                into.Add(new BuildingsStructure { Size = data.buildingSize, Version = each.number, Type = type });
                            }
                        }
                    }
                }
            }
            into.Sort(BuildingsRules.CompareStructures);
        }

        /// <summary>
        /// A lot's building declared to the made buildings' engine as one of the game's own (MadeSpec.Life): at its lot's
        /// address, the game's structure given (or, where no design of its size fits the lot, the first of <paramref name="others"/>
        /// with one: BuildingsRules.Others), filed under Uptown, its traffic index the rules', its floor area its storeys' or (0)
        /// that of the game's building its design copies; made behind the loading screen while <paramref name="wanted"/> says so.
        /// </summary>
        public static MadeSpec Spec(BuildingsLot lot, BuildingsStructure structure, IList<BuildingsStructure> others, string design, int storeys, int traffic, Func<bool> wanted)
        {
            MadeStructure[] instead = null;
            if (others != null && others.Count > 0)
            {
                instead = new MadeStructure[others.Count];
                for (int i = 0; i < others.Count; i++)
                {
                    instead[i] = new MadeStructure { Size = others[i].Size, Version = others[i].Version, Type = others[i].Type };
                }
            }
            return new MadeSpec
            {
                Id = lot.Id, Street = lot.Street, Number = lot.Number, Size = structure.Size, Version = structure.Version, Type = structure.Type,
                District = DistrictValues.Id, Design = string.IsNullOrEmpty(design) ? null : design,
                Lots = new[] { new MadeLot(lot.X, lot.Z, lot.Facing, lot.Width, lot.Depth) }, Preferred = 0,
                Traffic = traffic, Floors = Math.Max(1, storeys), DesignArea = storeys <= 0, Life = true, Wanted = wanted, Others = instead
            };
        }

        /// <summary>
        /// One of the district's big special places declared to the made buildings' engine as one of the game's own
        /// (MadeSpec.Life): at its own lot's address, the game's special structure of the building it copies whole, filed
        /// under Uptown, on its ground (its lot and the lots it spans), standing where BigPlacesRules puts it on that ground;
        /// its floor area that of the game's building it copies, the game's service copied under the place's own name (with no
        /// ordinary type to fall back to: where a player holds the lot it stands down). Made behind the loading screen while <paramref name="wanted"/> says so.
        /// </summary>
        public static MadeSpec BigSpec(BigPlace place, BuildingsLot ground, int traffic, Func<bool> wanted)
        {
            var lot = new MadeLot(ground.X, ground.Z, ground.Facing, ground.Width, ground.Depth) { Inset = place.Inset, Side = place.Side, SideGap = place.SideGap };
            return new MadeSpec
            {
                Id = place.Lot, Street = ground.Street, Number = ground.Number, Size = place.Size, Version = place.Version, Type = BigPlacesRules.SpecialType,
                District = DistrictValues.Id, Design = place.Design, Whole = true, Lots = new[] { lot }, Preferred = 0, Service = place.Service, ServiceName = place.Name, PlainType = null,
                Traffic = traffic, Floors = 1, DesignArea = true, Life = true, Wanted = wanted
            };
        }

        /// <summary>
        /// A district building that stands, into the game's lists of buildings: the city's (its generator, its rivals, its
        /// citizens' wishes) and the district's (its daily pass of businesses). Its record and registration are in the game's
        /// lookups already (the made buildings' engine put them there, the registration first).
        /// </summary>
        public static void Enter(MadeBuilding made)
        {
            Building record = made != null ? made.Building : null;
            if (record == null)
            {
                return;
            }
            if (!BuildingHelper.allBuildings.Contains(record))
            {
                BuildingHelper.allBuildings.Add(record);
            }
            if (!BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(DistrictValues.Id, out List<Building> district) || district == null)
            {
                district = new List<Building>();
                BuildingHelper.AllNeighbourhoodBuildings[DistrictValues.Id] = district;
            }
            if (!district.Contains(record))
            {
                district.Add(record);
            }
            if (!Entered.Contains(record))
            {
                Entered.Add(record);
            }
        }

        /// <summary>How many of the district's records are in the game's list of all buildings, and in the district's own list.</summary>
        public static void Counted(out int inCity, out int inDistrict)
        {
            inCity = 0;
            for (int i = 0; i < Entered.Count; i++)
            {
                inCity += Entered[i] != null && BuildingHelper.allBuildings.Contains(Entered[i]) ? 1 : 0;
            }
            inDistrict = BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(DistrictValues.Id, out List<Building> district) && district != null ? district.Count : 0;
        }

        /// <summary>
        /// Uptown stood down in play: its records out of the game's lists of buildings (nothing of the game's walks them from
        /// now on), kept in its lookup by address, where anything still holding one of its addresses finds it.
        /// </summary>
        public static void LeaveLists()
        {
            for (int i = 0; i < Entered.Count; i++)
            {
                BuildingHelper.allBuildings.Remove(Entered[i]);
            }
            if (BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(DistrictValues.Id, out List<Building> district) && district != null)
            {
                district.Clear();
            }
            Entered.Clear();
        }

        /// <summary>
        /// One district building knocked down in play: its record out of the game's lists of buildings (nothing of the game's
        /// walks it from now on), its carving box off so its ground is walked on again, and every filler standing on its lots'
        /// ground hidden. Its record stays in the game's lookup by address until the next load. Returns the fillers hidden.
        /// </summary>
        public static int Leave(MadeBuilding made, List<BuildingsLot> lots)
        {
            Building record = made != null ? made.Building : null;
            if (record == null)
            {
                return 0;
            }
            BuildingHelper.allBuildings.Remove(record);
            if (BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(DistrictValues.Id, out List<Building> district) && district != null)
            {
                district.Remove(record);
            }
            Entered.Remove(record);
            if (made.Controller != null && made.Controller.TryGetComponent(out NavMeshObstacle cut))
            {
                cut.carving = false;
                cut.enabled = false;
                Cuts.Remove(cut);
            }
            return HideFillersOn(lots);
        }

        /// <summary>
        /// Uptown stood down in play: its special places out of the game's lists of them too (the nearest clinic, the
        /// importers and the banks are found there), each with the registration it stood with.
        /// </summary>
        public static void LeaveSpecials(List<MadeBuilding> standing)
        {
            for (int i = 0; i < standing.Count; i++)
            {
                MadeBuilding made = standing[i];
                if (made != null && made.Building != null && made.Building.SpecialService != null)
                {
                    MadeGame.ImporterListed(made.Building, made.Registration, false);
                    MadeGame.ServiceListed(made.Building, false);
                }
            }
        }

        /// <summary>
        /// A carving box over a district building's footprint on the walking meshes, so nobody walks through its walls: the
        /// game's own buildings stand in holes baked into its meshes, and Uptown's ground is grown with none. Stationary, so it
        /// carves once.
        /// </summary>
        public static bool Cut(MadeBuilding made)
        {
            IList<DesignEntry> designs = CopyGame.Catalogue;
            if (made == null || made.Controller == null || made.DesignIndex < 0 || made.DesignIndex >= designs.Count)
            {
                return false;
            }
            DesignEntry d = designs[made.DesignIndex];
            Transform root = made.Controller.transform;
            if (d.Whole)
            {
                return CutWalls(root);
            }
            if (!root.TryGetComponent(out NavMeshObstacle cut))
            {
                cut = root.gameObject.AddComponent<NavMeshObstacle>();
            }
            Vector3 scale = root.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(scale.x), 1e-4f), sy = Mathf.Max(Mathf.Abs(scale.y), 1e-4f), sz = Mathf.Max(Mathf.Abs(scale.z), 1e-4f);
            float width = Mathf.Max(0.5f, d.MaxX - d.MinX - 2f * CutInset), depth = Mathf.Max(0.5f, d.MaxZ - d.MinZ - 2f * CutInset), height = Mathf.Max(2f, d.Height);
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3((d.MinX + d.MaxX) * 0.5f / sx, height * 0.5f / sy, (d.MinZ + d.MaxZ) * 0.5f / sz);
            cut.size = new Vector3(width / sx, height / sy, depth / sz);
            cut.carveOnlyStationary = true;
            cut.carving = true;
            cut.enabled = true;
            if (!Cuts.Contains(cut))
            {
                Cuts.Add(cut);
            }
            return true;
        }

        private static readonly List<Collider> Walls = new List<Collider>(8);
        private const string WallCut = "RealLife walls cut";   // a whole copy's carving boxes stand under it, one a wall
        private const float WallLeast = 1f;                    // m: a collider lower than this is ground (a forecourt, a car park), never carved

        // A building copied whole carves its solid parts alone, the walls the game bakes its holes for: every collider under
        // it that is no trigger and stands at least WallLeast high (a shop's shell, a garage's walls and doors, a pump
        // island). Its car park, forecourt and yard are flat ground its people and cars use, and stay walkable. One carving
        // box a part, under one holder made once a copy; the copy is never destroyed in a session.
        private static bool CutWalls(Transform root)
        {
            Transform holder = root.Find(WallCut);
            if (holder != null)
            {
                return true;
            }
            Walls.Clear();
            root.GetComponentsInChildren(true, Walls);
            var made = new GameObject(WallCut);
            made.transform.SetParent(root, false);
            int boxes = 0;
            for (int i = 0; i < Walls.Count; i++)
            {
                Collider wall = Walls[i];
                Bounds local;
                if (wall == null || wall.isTrigger || !Local(wall, root, out local) || local.size.y < WallLeast)
                {
                    continue;
                }
                var piece = new GameObject(WallCut + " " + boxes);
                piece.transform.SetParent(made.transform, false);
                NavMeshObstacle cut = piece.AddComponent<NavMeshObstacle>();
                cut.shape = NavMeshObstacleShape.Box;
                cut.center = local.center;
                cut.size = new Vector3(Mathf.Max(0.5f, local.size.x - 2f * CutInset), Mathf.Max(2f, local.size.y), Mathf.Max(0.5f, local.size.z - 2f * CutInset));
                cut.carveOnlyStationary = true;
                cut.carving = true;
                cut.enabled = true;
                if (boxes == 0)
                {
                    Cuts.Add(cut);   // one a building, as Carved counts buildings
                }
                boxes++;
            }
            Walls.Clear();
            return boxes > 0;
        }

        // A collider's box in the copy's root's space: a box's own or a mesh's bounds, its eight corners carried over from
        // the collider's object (an upright box round them where that object is turned).
        private static bool Local(Collider wall, Transform root, out Bounds local)
        {
            local = default;
            Bounds own;
            if (wall is BoxCollider box)
            {
                own = new Bounds(box.center, box.size);
            }
            else if (wall is MeshCollider mesh && mesh.sharedMesh != null)
            {
                own = mesh.sharedMesh.bounds;
            }
            else
            {
                return false;
            }
            Transform at = wall.transform;
            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = new Vector3((k & 1) == 0 ? own.min.x : own.max.x, (k & 2) == 0 ? own.min.y : own.max.y, (k & 4) == 0 ? own.min.z : own.max.z);
                Vector3 p = root.InverseTransformPoint(at.TransformPoint(corner));
                if (k == 0)
                {
                    local = new Bounds(p, Vector3.zero);
                }
                else
                {
                    local.Encapsulate(p);
                }
            }
            return true;
        }

        /// <summary>How many district buildings carve the walking meshes at this load.</summary>
        public static int Carved => Cuts.Count;

        /// <summary>
        /// Uptown's stats among the game's districts' (its daily pass of new businesses and swaps runs only for a district
        /// with them): made when the save has none, its first days spread as the game's own AddMissingNeighborhoodStats spreads
        /// them, from the save's seed rather than chance. True when made now.
        /// </summary>
        public static bool Stats(GameInstance save)
        {
            List<NeighbourhoodStats> all = save != null ? save.NeighbourhoodStats : null;
            if (all == null)
            {
                return false;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].name == DistrictValues.Id)
                {
                    return false;
                }
            }
            uint h = NewBuildingsRules.Hash(DistrictValues.Id + "|stats|" + save.seed);
            all.Add(new NeighbourhoodStats
            {
                name = DistrictValues.Id,
                nextNewBusinessDay = save.Day + 2 + (int)(h % 8u),                 // the game's day + 2 to + 9
                nextResidentialSwapDay = save.Day + 5 + (int)((h >> 8) % 10u),     // + 5 to + 14
                nextWarehouseSwapDay = save.Day + 5 + (int)((h >> 16) % 15u)       // + 5 to + 19
            });
            return true;
        }

        /// <summary>
        /// False when the save's own data says its city is not New York, read before its scenes exist: a city set up whose
        /// districts' rows of stats (the game keeps one for each district of its city) name none of New York's. A new city
        /// cannot tell yet; the city check at its load does, and the district then leaves at the city's step.
        /// </summary>
        public static bool NewYorkSave(GameInstance save, out string why)
        {
            why = null;
            List<NeighbourhoodStats> all = save != null && save.CityInitialized ? save.NeighbourhoodStats : null;
            if (all == null || all.Count == 0)
            {
                return true;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && CityDistricts.Known(all[i].name))
                {
                    return true;
                }
            }
            why = "the save's city is not New York: none of its " + all.Count + " districts' rows of stats is one of New York's";
            return false;
        }

        /// <summary>True when the save has Uptown's stats: its district has lived in this save before (its buildings were set up then).</summary>
        public static bool HasStats(GameInstance save)
        {
            List<NeighbourhoodStats> all = save != null ? save.NeighbourhoodStats : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].name == DistrictValues.Id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The game's own generator for Uptown alone, as the game sets up a district it adds to an older save
        /// (SetUpIndustryCityNeighborhood): its units let, kept empty to let or given the rivals' businesses, then given to
        /// the rivals as their landlords. The engine's chance is seeded from the save's seed and Uptown's id for the call and
        /// put back after, so the same save sets Uptown up the same way on every machine.
        /// </summary>
        public static void Populate(GameInstance save)
        {
            UnityEngine.Random.State was = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)NewBuildingsRules.Hash(DistrictValues.Id + "|first|" + (save != null ? save.seed : string.Empty)));
            try
            {
                CityGenerator.InitializeCity(DistrictValues.Id);
                CityGenerator.DistributeBuildingsToRivals(DistrictValues.Id);
            }
            finally
            {
                UnityEngine.Random.state = was;
            }
        }

        /// <summary>
        /// The game's own generator for Uptown's buildings new to a save it lived in before, those alone (a lot a newer plan
        /// builds, or one that could not stand at an earlier load): the district's other buildings are out of the game's list
        /// of all buildings for the call, which the generator reads (CityGenerator walks allBuildings by district), and the list
        /// is put back exactly as it was after; so their businesses, tenants and landlords are untouched, and the new ones are
        /// let, kept empty to let or given the rivals' businesses and landlords as the game sets up a district. Seeded from the
        /// save and the new buildings' ids, so every machine sets them up alike. How many it was run for.
        /// </summary>
        public static int PopulateOnly(GameInstance save, List<MadeBuilding> fresh, List<MadeBuilding> standing)
        {
            List<Building> all = BuildingHelper.allBuildings;
            if (all == null || fresh == null || fresh.Count == 0)
            {
                return 0;
            }
            Fresh.Clear();
            uint seed = NewBuildingsRules.Hash(DistrictValues.Id + "|more|" + (save != null ? save.seed : string.Empty));
            for (int i = 0; i < fresh.Count; i++)
            {
                if (fresh[i] != null && fresh[i].Building != null)
                {
                    Fresh.Add(fresh[i].Building);
                    seed = unchecked(seed * 16777619u ^ NewBuildingsRules.Hash(fresh[i].Spec.Id));
                }
            }
            AllWas.Clear();
            AllWas.AddRange(all);
            for (int i = 0; i < standing.Count; i++)
            {
                Building record = standing[i] != null ? standing[i].Building : null;
                if (record != null && !Fresh.Contains(record))
                {
                    all.Remove(record);
                }
            }
            UnityEngine.Random.State was = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)seed);
            try
            {
                CityGenerator.InitializeCity(DistrictValues.Id);
                CityGenerator.DistributeBuildingsToRivals(DistrictValues.Id);
            }
            finally
            {
                UnityEngine.Random.state = was;
                all.Clear();
                all.AddRange(AllWas);
                AllWas.Clear();
                Fresh.Clear();
            }
            return fresh.Count;
        }

        /// <summary>
        /// A building's first load in a district that has lived before (one finished by the construction game, or a day-one
        /// one whose registration was made now): the player's own gets the game's deed, written as BizMan's purchase writes
        /// one (BizManPresentation: its address, the price paid, the day, its floor area, nobody in it yet, the district's
        /// rent), off the market and with no rival as its landlord; the city's is put up for rent as the game leaves a unit it
        /// sets free. Never another player's (Going Public mirrors theirs). Null when done, else why not.
        /// </summary>
        public static string Settle(MadeBuilding made, FinishOwner owner, double price, GameInstance save)
        {
            BuildingRegistration reg = made != null ? made.Registration : null;
            Building record = made != null ? made.Building : null;
            if (reg == null || record == null || save == null)
            {
                return "its records are not there";
            }
            if (GoingPublic.Foreign(reg))
            {
                return "it is another player's in this session";
            }
            Address address = record.Address;
            if (owner == FinishOwner.Player)
            {
                if (save.realEstate == null)
                {
                    save.realEstate = new List<RealEstate>();
                }
                if (!Deeded(save, address))
                {
                    save.realEstate.Add(new RealEstate
                    {
                        address = address, purchasePrice = price, purchaseDay = save.Day, totalSqm = record.totalSqm,
                        occupancy = BuildingsRules.NewDeedOccupancy, pricePerSqm = record.GetBuildingDailyMarketRentPerSqm()
                    });
                }
                for (int i = save.buildingsForSale != null ? save.buildingsForSale.Count - 1 : -1; i >= 0; i--)
                {
                    if (save.buildingsForSale[i] != null && save.buildingsForSale[i].address == address)
                    {
                        save.buildingsForSale.RemoveAt(i);
                    }
                }
                reg.buildingOwnerRivalId = string.Empty;
                return null;
            }
            if (!reg.RentedByPlayer && reg.businessTypeName == "ba:businesstype_empty")
            {
                BusinessHelper.SetBuildingForRent(reg, false, false);   // empty: nothing of the market or the rivals to bring up to date
            }
            return null;
        }

        /// <summary>The player's deed of a district building (the game's real estate at its address), or null.</summary>
        public static RealEstate Deed(MadeBuilding made)
        {
            GameInstance save = SaveGameManager.Current;
            Address address = made != null && made.Building != null ? made.Building.Address : null;
            for (int i = 0; address != null && save != null && save.realEstate != null && i < save.realEstate.Count; i++)
            {
                if (save.realEstate[i] != null && save.realEstate[i].address == address)
                {
                    return save.realEstate[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The player's deed of an extended building given its new floor area, as BizMan's purchase writes the building's own
        /// into it: the owner's rent from the city's tenants and the property tax follow (RealEstate.DailyIncome, TaxesAmount).
        /// False when the player holds no deed of it.
        /// </summary>
        public static bool Resize(MadeBuilding made, int sqm)
        {
            RealEstate deed = Deed(made);
            if (deed == null || sqm <= 0)
            {
                return false;
            }
            deed.totalSqm = sqm;
            return true;
        }

        /// <summary>True when the game's structure of a size and version takes a building type (BuildingVersion.supportedBuildingTypes).</summary>
        public static bool Takes(string size, int version, string type)
        {
            BuildingSizeData data = string.IsNullOrEmpty(size) ? null : BuildingSizeHelper.GetData(size);
            BuildingVersion[] versions = data != null ? data.buildingVersions : null;
            for (int i = 0; versions != null && i < versions.Length; i++)
            {
                if (versions[i] != null && versions[i].number == version && versions[i].supportedBuildingTypes != null)
                {
                    return versions[i].supportedBuildingTypes.Contains(type);
                }
            }
            return false;
        }

        private static bool Deeded(GameInstance save, Address address)
        {
            for (int i = 0; save.realEstate != null && i < save.realEstate.Count; i++)
            {
                if (save.realEstate[i] != null && save.realEstate[i].address == address)
                {
                    return true;
                }
            }
            return false;
        }

        // The city goes: the district's records go with the game's own lists, which the game makes again at the next load.
        private static void UnloadBuildings()
        {
            Entered.Clear();
            Cuts.Clear();
        }
    }
}
