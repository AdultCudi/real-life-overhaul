using System;
using System.Collections.Generic;
using BigAmbitions.SaveSystem;
using BusinessLayoutSets;
using Extensions;
using RealLifeOverhaul.Tech;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>A fitted floor's pieces: the design's items read as pieces of the plan, their sizes, and each stood in the room.</summary>
    internal sealed partial class FitView
    {
        private static string Id(Item item) => item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName.GetIdWithoutType() : null;

        // Most of a design's floor pieces on this room's floor: a design for this room.
        private static bool StandsHere(BusinessLayoutSet set, Request r)
        {
            int floorItems = 0, onFloor = 0;
            for (int i = 0; i < set.Items.Count; i++)
            {
                Item item = set.Items[i];
                Vector3 at = item.position;
                if (item.itemName == null || Mathf.Abs(at.y - r.Floor) > 0.15f)
                {
                    continue;
                }
                floorItems++;
                onFloor += r.Grid.NearestOpen(at.x, at.z, null) >= 0 ? 1 : 0;
            }
            return floorItems >= 4 && onFloor >= floorItems * 0.7f;
        }

        // How far the walkable floor stops short of a wall: the walkers' own width.
        private static float Erosion()
        {
            try
            {
                return Mathf.Clamp(NavMesh.GetSettingsByID(NavMeshHelper.NpcNavMeshFilter.agentTypeID).agentRadius, 0.15f, 0.6f);
            }
            catch (Exception)
            {
                return 0.3f;
            }
        }

        // The unit's other ways out kept clear, and its drive-in bays, with room for a car.
        private static void Exits(Request r, FitSite site)
        {
            site.Exits.Clear();
            List<ExitZone> exits = r.Building.exitZones;
            for (int i = 0; exits != null && i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.playerSpawnPoint == null)
                {
                    continue;
                }
                Vector3 at = exit.playerSpawnPoint.position;
                if (!exit.isDriveInBay && CitySpots.Flat(at - r.Door) < 0.5f * 0.5f)
                {
                    continue;   // the door the player came in by: the plan keeps its way clear itself
                }
                site.Exits.Add(new FitSpot(at.x, at.z, exit.isDriveInBay ? 4.5f : FitPlanner.ExitClear));
            }
        }

        // A piece's drawn size: its template's, or the size the mod's model stands by.
        private static bool Size(string prefab, out FitSize size)
        {
            if (prefab.StartsWith(ModProp, StringComparison.Ordinal))
            {
                if (ModSpec(prefab, out SiteProps.Spec spec))
                {
                    size = new FitSize(spec.Size.x, spec.Size.z, spec.Size.y);
                    return true;
                }
                size = default;
                return false;
            }
            if (GameFurniture.Template(prefab, out Bounds b) == null || b.size.x < 0.02f || b.size.z < 0.02f)
            {
                size = default;
                return false;
            }
            size = new FitSize(b.size.x, b.size.z, b.size.y, b.center.x, b.center.z, b.max.y);
            return true;
        }

        private static bool ModSpec(string prefab, out SiteProps.Spec spec)
        {
            switch (prefab)
            {
                case "rlo:closetrack": spec = SiteProps.ClosetRack; return true;
                case "rlo:bench": spec = SiteProps.Bench; return true;
                default: spec = default; return false;
            }
        }

        // The design's items as pieces of the plan, each with the floor its drawn bounds take as it stands.
        private void DesignPieces(Request r, List<FitPiece> into)
        {
            for (int i = 0; i < _set.Items.Count; i++)
            {
                Item item = _set.Items[i];
                string id = Id(item);
                if (id == null || !FitRoles.Fitted(id) || !r.Site.Sizes.TryGetValue(id, out FitSize s))
                {
                    continue;
                }
                Vector3 at = item.position;
                Quaternion facing = item.rotation;
                float height = at.y - r.Floor;
                FitRole role = FitRoles.IsLamp(id) ? FitRole.Lamp : FitRoles.IsFlat(id) ? FitRole.Flat : height >= 0.15f ? FitRole.OnTop : FitRoles.IsChair(id) ? FitRole.Seat : FitRole.Floor;
                float yaw = facing.eulerAngles.y;
                float sin = Mathf.Sin(yaw * Mathf.Deg2Rad), cos = Mathf.Cos(yaw * Mathf.Deg2Rad);
                float cx = at.x + s.CX * cos + s.CZ * sin, cz = at.z - s.CX * sin + s.CZ * cos;
                into.Add(new FitPiece
                {
                    Prefab = id, Role = role, X = at.x, Z = at.z, Y = height, Yaw = yaw, CX = cx, CZ = cz,
                    HalfX = Mathf.Abs(cos) * s.W * 0.5f + Mathf.Abs(sin) * s.D * 0.5f, HalfZ = Mathf.Abs(sin) * s.W * 0.5f + Mathf.Abs(cos) * s.D * 0.5f,
                    Height = s.H, Anchor = -1, Tall = s.H > 1.3f
                });
            }
        }

        // Null if it cannot stand: no such prefab, or no wall or ceiling where it hangs.
        private Transform Place(FitPiece p, Transform under)
        {
            Request r = _request;
            float floor = r.Floor;
            Quaternion facing = Quaternion.Euler(0f, p.Yaw, 0f);
            Vector3 at = new Vector3(p.X, floor + p.Y, p.Z);
            if (p.Role == FitRole.Hung && !p.Design)
            {
                // its back on the real surface of the wall behind where the plan put it
                Vector3 front = facing * Vector3.forward;
                if (!RoomMeasure.Wall(new Vector3(p.X, floor + p.Y + 0.4f, p.Z), -front, 1.4f, out Vector3 surface, out Vector3 normal))
                {
                    return null;
                }
                at = new Vector3(surface.x, floor + p.Y, surface.z) + normal * 0.004f;
                facing = Quaternion.LookRotation(normal, Vector3.up);
            }
            else if (p.Role == FitRole.Lamp && !p.Design)
            {
                if (!RoomMeasure.Ceiling(new Vector3(p.X, floor + 1.2f, p.Z), out Vector3 ceiling))
                {
                    return null;
                }
                at = ceiling - Vector3.up * 0.002f;
            }
            else if (p.Role == FitRole.Flat)
            {
                at.y += 0.003f;
            }
            Transform piece;
            Vector3 boxCentre, boxSize;
            if (p.Prefab.StartsWith(ModProp, StringComparison.Ordinal))
            {
                if (!ModSpec(p.Prefab, out SiteProps.Spec spec))
                {
                    return null;
                }
                piece = SiteProps.Make(spec, _root, out _);
                boxCentre = new Vector3(0f, spec.Size.y * 0.5f, 0f);
                boxSize = spec.Size;
            }
            else
            {
                piece = GameFurniture.Copy(p.Prefab, _root);
                if (piece == null)
                {
                    return null;
                }
                GameFurniture.Template(p.Prefab, out Bounds b);
                boxCentre = new Vector3(b.center.x, b.size.y * 0.5f, b.center.z);
                boxSize = new Vector3(b.size.x, Mathf.Max(0.5f, b.size.y), b.size.z);
            }
            piece.SetPositionAndRotation(at, facing);
            if (under != null && p.Role == FitRole.OnTop)
            {
                piece.SetParent(under, true);
            }
            if (r.Carve && p.Role == FitRole.Floor && p.HalfX * p.HalfZ * 4f >= 0.06f)   // thin partitions too: they make the rooms
            {
                var cut = piece.gameObject.AddComponent<NavMeshObstacle>();
                cut.shape = NavMeshObstacleShape.Box;
                cut.center = boxCentre;
                cut.size = boxSize;
                cut.carving = true;
                cut.carveOnlyStationary = true;
                cut.carvingTimeToStationary = 0f;   // it never moves: cut at the navigation system's next update
                cut.enabled = false;   // switched on once the room stands, a few a frame
                _cuts.Add(cut);
            }
            piece.gameObject.SetActive(true);
            return piece;
        }
    }
}
