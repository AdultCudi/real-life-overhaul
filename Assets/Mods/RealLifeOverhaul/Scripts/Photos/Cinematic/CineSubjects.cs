using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.Economy;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>A subject looked for: whether it is there, why not, its place and size for the camera's sums, and its name for the list.</summary>
    internal struct CineFound
    {
        public bool There;
        public string Why;        // for a subject not there
        public CineBox Box;
        public string Name;       // what it is, in plain words, for shots.txt
        public float Roof;        // the skyline's roof: its height, and where it is behind and beside the subject
        public float Back, Sideways;
    }

    /// <summary>
    /// What a cinematic shot is of, found in the city as it stands when the shot comes up, never at a place written down:
    /// the player and their car, the walkers and the trees the game lists, the mod's places on the map rows, the bus, a
    /// stall, a scene that is on, the nearest shopfront, and the street and the roofs by rays cast from where the player
    /// stands. One look is a pass over a list of the game's or some thirty rays, on a press with the game paused; nothing per frame.
    /// </summary>
    internal static class CineSubjects
    {
        private const float FrontageReach = 55f;      // metres: a shop's sign is lit within the game's first band, 60 m of the player
        private const float FrontHalf = 7f;           // metres of a front either side of its door
        private const float LookOver = 3f;            // metres over the street the runs are measured at: over the cars, under the signs
        private const float SideMost = 45f;           // metres a street's wall is looked for to either side
        private const float RoofFrom = 260f;          // metres over the street a roof is looked for from
        private const int RenderersMost = 240;        // of a building, to measure it by

        private static readonly string[] Landmarks = { "landmarks", "sights", "courthouse", "university", "tech_hq", "schools", "utilities_office" };
        private static readonly string[] Stations = { "police", "hotels" };

        private static readonly float[] Xs = new float[CineRules.ClusterMost], Zs = new float[CineRules.ClusterMost];
        private static readonly Vector3[] Points = new Vector3[CineRules.ClusterMost];
        private static readonly Vector3[] Ways = new Vector3[CineRules.ClusterMost];
        private static readonly float[] Runs = new float[CineRules.Bearings];
        private static readonly List<Renderer> Renderers = new List<Renderer>(RenderersMost);

        /// <summary>Looks for a subject now. A fault in one look is that subject not found, said in the log; it never ends the run.</summary>
        public static CineFound Find(CineSubject subject)
        {
            var found = default(CineFound);
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (player == null)
                {
                    found.Why = "the player is not there";
                    return found;
                }
                Vector3 from = player.transform.position;
                switch (subject)
                {
                    case CineSubject.Player: Player(ref found); break;
                    case CineSubject.Car: Car(from, ref found); break;
                    case CineSubject.Walkers: Walkers(from, ref found); break;
                    case CineSubject.Trees: Trees(from, ref found); break;
                    case CineSubject.Avenue: Avenue(from, ref found); break;
                    case CineSubject.Skyline: Skyline(from, ref found); break;
                    case CineSubject.Landmark: OnMap(from, Landmarks, "no landmark or sight of the mod's map rows within " + Metres(CineRules.Reach), ref found); break;
                    case CineSubject.Bus: Bus(from, ref found); break;
                    case CineSubject.Stall: Stall(from, ref found); break;
                    case CineSubject.Incident: Incident(from, ref found); break;
                    case CineSubject.Frontage: Frontage(from, ref found); break;
                    case CineSubject.Station: OnMap(from, Stations, "no police station or hotel of the mod's within " + Metres(CineRules.Reach), ref found); break;
                    default: found.Why = "not a subject"; break;
                }
            }
            catch (Exception e)
            {
                CineGame.Logger?.Error(e);
                found.There = false;
                found.Why = "a fault while looking (" + e.GetType().Name + ")";
            }
            return found;
        }

        private static string Metres(float metres)
        {
            return metres.ToString("0") + " m";
        }

        private static Vector3 Flat(Vector3 way)
        {
            way.y = 0f;
            return way.sqrMagnitude > 0.0001f ? way.normalized : Vector3.forward;
        }

        private static bool Near(Vector3 from, Vector3 at, float within)
        {
            float dx = at.x - from.x, dz = at.z - from.z;
            return dx * dx + dz * dz <= within * within;
        }

        private static void Set(ref CineFound found, Vector3 centre, Vector3 facing, float halfLength, float halfWidth, float height, string name)
        {
            Vector3 way = Flat(facing);
            found.There = true;
            found.Why = null;
            found.Name = name;
            found.Box = new CineBox { X = centre.x, Y = centre.y, Z = centre.z, Fx = way.x, Fz = way.z, HalfLength = halfLength, HalfWidth = halfWidth, Height = height };
        }

        // ------------------------------------------------------------------ the player and their car

        private static void Player(ref CineFound found)
        {
            if (PlayerHelper.IsUsingVehicle)
            {
                found.Why = "the player is in a vehicle";
                return;
            }
            if (!PhotosGame.Body(out Transform root, out Transform head))
            {
                found.Why = "the player's body or head was not found";
                return;
            }
            Set(ref found, root.position, root.forward, 0.3f, 0.3f, Mathf.Clamp(head.position.y - root.position.y + 0.14f, 1.2f, 2.2f), "the player");
        }

        private static void Car(Vector3 from, ref CineFound found)
        {
            GameManager manager = InstanceBehavior<GameManager>.Instance;
            CarController car = manager != null ? manager.selectedVehicle as CarController : null;
            if (car == null)
            {
                float best = CineRules.Reach * CineRules.Reach;
                List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; own != null && i < own.Count; i++)
                {
                    if (!(own[i] is CarController candidate) || !candidate.isActiveAndEnabled || !GoingPublic.MineVehicle(candidate))
                    {
                        continue;
                    }
                    float apart = (candidate.transform.position - from).sqrMagnitude;
                    if (apart < best)
                    {
                        best = apart;
                        car = candidate;
                    }
                }
            }
            if (car == null)
            {
                found.Why = "no car of the player's within " + Metres(CineRules.Reach);
                return;
            }
            Transform body = car.transform;
            Vector3 centre = body.position;
            float halfLength = 2.2f, halfWidth = 0.9f, top = 1.45f;   // a saloon, where the game's body collider cannot be read
            if (VehicleHelper.TryGetBodyColliderBounds(body, out Bounds local, out Collider shell) && shell != null)
            {
                Vector3 scale = shell.transform.lossyScale;
                halfLength = Mathf.Clamp(local.extents.z * Mathf.Abs(scale.z), 1.2f, 5f);
                halfWidth = Mathf.Clamp(local.extents.x * Mathf.Abs(scale.x), 0.6f, 1.6f);
                centre = shell.transform.TransformPoint(local.center);
                top = shell.transform.TransformPoint(local.center + Vector3.up * local.extents.y).y;
            }
            float ground = CineGame.Ground(centre, out float height) ? height : body.position.y;
            top = top > ground + 0.5f ? Mathf.Clamp(top - ground, 1f, 3.2f) : 1.45f;
            centre.y = ground;
            Set(ref found, centre, body.forward, halfLength, halfWidth, top, car == (manager != null ? manager.selectedVehicle : null) ? "the car the player drives" : "a car of the player's, parked");
        }

        // ------------------------------------------------------------------ what the game lists: walkers and trees

        private static void Walkers(Vector3 from, ref CineFound found)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            int count = 0;
            for (int i = 0; walkers != null && i < walkers.Count && count < CineRules.ClusterMost; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.gameObject.activeInHierarchy || !Near(from, walker.transform.position, CineRules.Reach))
                {
                    continue;
                }
                Points[count] = walker.transform.position;
                Ways[count] = walker.transform.forward;
                Xs[count] = Points[count].x;
                Zs[count] = Points[count].z;
                count++;
            }
            if (!CineRules.Densest(Xs, Zs, count, CineRules.WalkerRadius, CineRules.WalkersLeast, out int centre, out int size))
            {
                found.Why = "fewer than " + CineRules.WalkersLeast + " walkers within " + Metres(CineRules.WalkerRadius) + " of each other (" + count + " walkers within "
                            + Metres(CineRules.Reach) + ")";
                return;
            }
            Set(ref found, Points[centre], Ways[centre], 0.3f, 0.3f, 1.8f, size + " walkers on one stretch of pavement");
        }

        private static void Trees(Vector3 from, ref CineFound found)
        {
            List<TreeController> trees = TreeController.AllTrees;
            int count = 0;
            for (int i = 0; trees != null && i < trees.Count && count < CineRules.ClusterMost; i++)
            {
                TreeController tree = trees[i];
                if (tree == null || !tree.gameObject.activeInHierarchy || !Near(from, tree.transform.position, CineRules.Reach))
                {
                    continue;
                }
                Points[count] = tree.transform.position;
                Xs[count] = Points[count].x;
                Zs[count] = Points[count].z;
                count++;
            }
            if (!CineRules.Densest(Xs, Zs, count, CineRules.TreeRadius, CineRules.TreesLeast, out int centre, out int size))
            {
                found.Why = "fewer than " + CineRules.TreesLeast + " trees within " + Metres(CineRules.TreeRadius) + " of each other (" + count + " trees within "
                            + Metres(CineRules.Reach) + ")";
                return;
            }
            Vector3 middle = Points[centre];
            if (CineGame.Ground(middle, out float ground))
            {
                middle.y = ground;
            }
            // The camera stands on the player's side of the trees, where there is a way in.
            Set(ref found, middle, Near(from, middle, 3f) ? Vector3.forward : from - middle, 9f, 9f, 9f, size + " trees together");
        }

        // ------------------------------------------------------------------ the street and the roofs, by rays

        private static Vector3 Bearing(int index)
        {
            float turn = index * 2f * Mathf.PI / CineRules.Bearings;
            return new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn));
        }

        private static void Avenue(Vector3 from, ref CineFound found)
        {
            Vector3 over = from + Vector3.up * LookOver;
            float longest = 0f;
            for (int b = 0; b < CineRules.Bearings; b++)
            {
                Runs[b] = Physics.Raycast(over, Bearing(b), out RaycastHit hit, CineRules.AvenueMost, CineGame.Walls, QueryTriggerInteraction.Ignore) ? hit.distance : CineRules.AvenueMost;
                longest = Mathf.Max(longest, Runs[b]);
            }
            if (!CineRules.Avenue(Runs, CineRules.Bearings, CineRules.AvenueLeast, out int best))
            {
                found.Why = "no straight street of " + Metres(CineRules.AvenueLeast) + " from where the player stands (the longest run is " + Metres(longest) + ")";
                return;
            }
            Vector3 along = Bearing(best);
            var right = new Vector3(along.z, 0f, -along.x);
            bool wallLeft = Physics.Raycast(over, -right, out RaycastHit left, SideMost, CineGame.Walls, QueryTriggerInteraction.Ignore);
            bool wallRight = Physics.Raycast(over, right, out RaycastHit rightHit, SideMost, CineGame.Walls, QueryTriggerInteraction.Ignore);
            bool both = wallLeft && wallRight;
            Vector3 middle = both ? from + right * ((rightHit.distance - left.distance) * 0.5f) : from;   // between the two walls, where the street's lines meet
            if (CineGame.Ground(middle, out float ground))
            {
                middle.y = ground;
            }
            float half = both ? (left.distance + rightHit.distance) * 0.5f : 8f;
            Set(ref found, middle, along, 0f, half, 0f, "a street " + Metres(Runs[best]) + " long and " + Metres(half * 2f) + " wide between its walls");
        }

        private static void Skyline(Vector3 from, ref CineFound found)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            Vector3 sum = Vector3.zero;
            int counted = 0;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    sum += all[i].transform.position;
                    counted++;
                }
            }
            // The city is looked at toward where most of it stands.
            Vector3 toward = counted > 0 && !Near(from, sum / counted, 30f) ? Flat(sum / counted - from) : Vector3.forward;
            var right = new Vector3(toward.z, 0f, -toward.x);
            float street = CineGame.Ground(from, out float ground) ? ground : from.y;
            float bestWorth = 0f;
            for (int i = 0; i < CineRules.Stands; i++)
            {
                CineRules.Stand(i, out float back, out float sideways);
                Vector3 stand = from - toward * back + right * sideways;
                stand.y = street + RoofFrom;
                if (!Physics.Raycast(stand, Vector3.down, out RaycastHit roof, RoofFrom + 40f, CineGame.Walls | CineGame.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
                float worth = CineRules.RoofWorth(roof.point.y - street);
                Vector3 tripod = roof.point + Vector3.up * CineRules.EyeOverRoof;
                if (worth > bestWorth && !Physics.Raycast(tripod, toward, CineRules.RoofView, CineGame.Walls, QueryTriggerInteraction.Ignore))
                {
                    bestWorth = worth;
                    found.Roof = roof.point.y;
                    found.Back = back;
                    found.Sideways = sideways;
                }
            }
            if (bestWorth <= 0f)
            {
                found.Why = "no roof between " + Metres(CineRules.RoofLeast) + " and " + Metres(CineRules.RoofMost) + " high behind the player with " + Metres(CineRules.RoofView)
                            + " of open air toward the city";
                return;
            }
            Set(ref found, new Vector3(from.x, street, from.z), toward, 0f, 0f, 0f, "the city from a roof " + Metres(found.Roof - street) + " over the street");
        }

        // ------------------------------------------------------------------ the mod's places and the city's buildings

        private static void OnMap(Vector3 from, string[] rows, string none, ref CineFound found)
        {
            IReadOnlyList<MapPlaces.Category> all = MapPlaces.All;
            MapPlace best = null;
            int bestRow = int.MaxValue;
            float bestApart = float.MaxValue;
            for (int c = 0; all != null && c < all.Count; c++)
            {
                MapPlaces.Category category = all[c];
                int row = category != null ? Array.IndexOf(rows, category.Id) : -1;
                if (row < 0 || category.Places == null)
                {
                    continue;
                }
                for (int p = 0; p < category.Places.Count; p++)
                {
                    MapPlace place = category.Places[p];
                    if (place == null || !Near(from, place.Position, CineRules.Reach))
                    {
                        continue;
                    }
                    float apart = (place.Position - from).sqrMagnitude;
                    if (row < bestRow || (row == bestRow && apart < bestApart))   // the rows are in the order a photographer would pick them
                    {
                        best = place;
                        bestRow = row;
                        bestApart = apart;
                    }
                }
            }
            if (best == null)
            {
                found.Why = none;
                return;
            }
            string name = string.IsNullOrEmpty(best.Name) ? rows[bestRow] : best.Name;
            if (best.Building != null && Measure(best.Building, best.Position, name, ref found))
            {
                return;
            }
            Vector3 at = best.Position;
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            Set(ref found, at, from - at, 8f, 8f, 12f, name);
        }

        // A building as a box: its renderers' bounds, facing out through the side its door (or its place on the map) is on.
        private static bool Measure(CityBuildingController building, Vector3 face, string name, ref CineFound found)
        {
            Renderers.Clear();
            building.GetComponentsInChildren(false, Renderers);
            bool any = false;
            var bounds = default(Bounds);
            for (int i = 0; i < Renderers.Count && i < RenderersMost; i++)
            {
                Renderer part = Renderers[i];
                if (part == null || !part.enabled || part is ParticleSystemRenderer || part.bounds.size.sqrMagnitude > 600f * 600f)
                {
                    continue;
                }
                if (any)
                {
                    bounds.Encapsulate(part.bounds);
                }
                else
                {
                    bounds = part.bounds;
                    any = true;
                }
            }
            Renderers.Clear();
            if (!any)
            {
                return false;
            }
            Vector3 centre = bounds.center;
            Vector3 out0 = face - centre;
            Transform frame = building.transform;
            Vector3 along = Flat(frame.forward), across = Flat(frame.right);
            float byAlong = Vector3.Dot(out0, along), byAcross = Vector3.Dot(out0, across);
            Vector3 outward = Mathf.Abs(byAlong) >= Mathf.Abs(byAcross) ? along * (byAlong < 0f ? -1f : 1f) : across * (byAcross < 0f ? -1f : 1f);
            float deep = Mathf.Abs(outward.x) * bounds.extents.x + Mathf.Abs(outward.z) * bounds.extents.z;
            float wide = Mathf.Abs(outward.z) * bounds.extents.x + Mathf.Abs(outward.x) * bounds.extents.z;
            float ground = CineGame.Ground(face, out float height) ? height : bounds.min.y;
            centre.y = ground;
            Set(ref found, centre, outward, Mathf.Max(1f, deep), Mathf.Max(1f, wide), Mathf.Clamp(bounds.max.y - ground, 3f, 400f), name);
            return true;
        }

        private static void Frontage(Vector3 from, ref CineFound found)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            CityBuildingController best = null;
            Vector3 bestDoor = default(Vector3);
            float bestApart = FrontageReach * FrontageReach;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController building = all[i];
                BuildingRegistration registration = building != null ? building.buildingRegistration : null;
                if (registration == null || string.IsNullOrEmpty(registration.BusinessName) || !Books.IsBusiness(registration) || !JobSpots.DoorOf(building, out Vector3 door))
                {
                    continue;
                }
                float apart = (door - from).sqrMagnitude;
                if (apart < bestApart)
                {
                    bestApart = apart;
                    best = building;
                    bestDoor = door;
                }
            }
            if (best == null)
            {
                found.Why = "no business with a door within " + Metres(FrontageReach);
                return;
            }
            string name = best.buildingRegistration.BusinessName;
            if (!Measure(best, bestDoor, name, ref found))
            {
                found.Why = "the nearest business's building could not be measured";
                found.There = false;
                return;
            }
            // The front, not the block: the door on the ground, a few metres of the wall either side, the sign's height.
            if (CineGame.Ground(bestDoor, out float ground))
            {
                bestDoor.y = ground;
            }
            found.Box.X = bestDoor.x;
            found.Box.Y = bestDoor.y;
            found.Box.Z = bestDoor.z;
            found.Box.HalfLength = 0f;
            found.Box.HalfWidth = FrontHalf;
            found.Box.Height = Mathf.Min(found.Box.Height, 6f);
        }

        // ------------------------------------------------------------------ the mod's own in the street

        private static void Bus(Vector3 from, ref CineFound found)
        {
            if (!TransitDirector.BusOut(out Vector3 at, out Vector3 facing, out bool standing) || !Near(from, at, CineRules.Reach))
            {
                found.Why = "no bus on the street within " + Metres(CineRules.Reach) + " (one comes when the player waits at a stop)";
                return;
            }
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            Set(ref found, at, facing, BusVehicle.Length * 0.5f, BusVehicle.HalfWidth, 3f, standing ? "a bus standing at its stop" : "a bus on its way");
        }

        private static void Stall(Vector3 from, ref CineFound found)
        {
            CityLifeDirector life = CityLifeDirector.Instance;
            Vector3 at = default(Vector3);
            bool stall = life != null && life.SiteOut(CityFamily.Stalls, out at);
            if (!stall && !(life != null && life.SiteOut(CityFamily.Everyday, out at)) || !Near(from, at, CineRules.Reach))
            {
                found.Why = "no stall, cart or street cafe of the living city is out near the player";
                return;
            }
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            // A person at a stall is the subject, seen from the player's side of it.
            Set(ref found, at, Near(from, at, 2f) ? Vector3.forward : from - at, 0.5f, 0.5f, 1.8f, stall ? "a stall of the living city" : "people of the living city about their day");
        }

        private static void Incident(Vector3 from, ref CineFound found)
        {
            Vector3 at = default(Vector3);
            string what = null;
            if (TrafficAccidents.IsActive)
            {
                TrafficAccidents.Needing(out at);   // where the scene is, whoever it needs
                what = "a traffic accident";
            }
            else if (Events.EventDirector.Burning(out at))
            {
                what = "a fire";
            }
            else if (Events.EventDirector.StreetEvent(out at, out _))
            {
                what = "a street event";
            }
            else if (CrimeDirector.IsActive)
            {
                CrimeDirector.Victim(out at);   // where the scene is, whatever its kind
                what = "a crime scene";
            }
            if (what == null || at == Vector3.zero || !Near(from, at, CineRules.Reach))
            {
                found.Why = what == null ? "no accident, fire, crime or street event is on" : what + " is on, but not within " + Metres(CineRules.Reach);
                return;
            }
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            Set(ref found, at, Near(from, at, 2f) ? Vector3.forward : from - at, 7f, 4.5f, 2.4f, what);
        }
    }
}
