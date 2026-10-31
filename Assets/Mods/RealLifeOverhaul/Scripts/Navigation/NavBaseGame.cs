using System;
using System.IO;
using Helpers;
using Streets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The only place the minimap's base picture touches the game. The game's city map is the city under a camera, so
    /// the base is drawn from what the game keeps: each building's lodVersion mesh seen from above, water asked as
    /// fishing asks it (a ray down and the navigation mesh), and the game's palette. Run only while a city is measured
    /// and for the director's checks.
    /// </summary>
    internal static class NavBaseGame
    {
        private const float WidestOutline = 600f;   // metres: anything wider is a renderer reporting a whole batch
        private const float WalkReach = 1.2f;       // metres from the place looked at that still count as walked on, as fishing has it
        private const string Folder = "RealLifeOverhaul";

        private static readonly Vector3[] Corner = new Vector3[8];
        private static int _solid;

        /// <summary>The game's build number, which names the kept file; 0 if it cannot be read.</summary>
        public static int Build()
        {
            GameVersion version = GameVersion.GetCurrent();
            return version != null ? version.buildNumber : 0;
        }

        /// <summary>Where the measured city of a build is kept, in the mod's own folder beside the game's saves.</summary>
        public static string FilePath(int build)
        {
            return Path.Combine(Application.persistentDataPath, Folder, "minimap_base_" + build + ".bin");
        }

        /// <summary>The file's bytes, or null if there is none or it cannot be read.</summary>
        public static byte[] Read(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // it is written over when the city has been measured again
            }
        }

        /// <summary>Buildings in a faint light grey, water in the palette's dark blue; false while the game's references are not there.</summary>
        public static bool Colours(out Color32 building, out Color32 water)
        {
            building = default;
            water = default;
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.colors == null)
            {
                return false;
            }
            building = references.colors.lightGrey;
            building.a = 84;    // of 255: a third as strong as the roads, so a road stays the first thing seen
            water = references.colors.darkBlue;
            if (water.r + water.g + water.b < 24)
            {
                // the palette's dark blue is not set in this version of the game: its blue at six tenths
                Color32 blue = references.colors.blue;
                water = new Color32((byte)(blue.r * 6 / 10), (byte)(blue.g * 6 / 10), (byte)(blue.b * 6 / 10), 255);
            }
            water.a = 215;
            return true;
        }

        /// <summary>The game's list of the city's buildings; null while there is no city.</summary>
        public static CityBuildingController[] Buildings()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return GameDlc.Open(city);
        }

        /// <summary>A building's outline from above, into arrays of 8: 8 corners for its own mesh's box, 4 for an upright box, else 0.</summary>
        public static int Outline(CityBuildingController building, float[] x, float[] z)
        {
            if (building == null)
            {
                return 0;
            }
            int boxed = Outline(building.lodVersion, x, z);
            if (boxed > 0)
            {
                return boxed;
            }
            Renderer[] all = building.Renderers;
            if (all == null)
            {
                return 0;
            }
            bool any = false;
            Bounds whole = default;
            for (int i = 0; i < all.Length; i++)
            {
                Renderer renderer = all[i];
                if (renderer == null || renderer.bounds.size.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                if (!any)
                {
                    whole = renderer.bounds;
                    any = true;
                }
                else
                {
                    whole.Encapsulate(renderer.bounds);
                }
            }
            return any && Upright(whole, x, z) ? 4 : 0;
        }

        /// <summary>The outline of a building's box (the game's LOD box of it) from above, as <see cref="Outline(CityBuildingController, float[], float[])"/> reads it; 0 with none.</summary>
        public static int Outline(MeshRenderer simple, float[] x, float[] z)
        {
            if (simple != null)
            {
                // A mesh not in a static batch still has its own box, which keeps the building's turn.
                MeshFilter filter = simple.isPartOfStaticBatch ? null : simple.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh != null && mesh.bounds.size.sqrMagnitude > 0.01f)
                {
                    Bounds box = mesh.bounds;
                    Vector3 low = box.min, high = box.max;
                    Matrix4x4 toWorld = simple.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        Corner[i] = toWorld.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? low.x : high.x, (i & 2) == 0 ? low.y : high.y, (i & 4) == 0 ? low.z : high.z));
                    }
                    if (Fits(Corner, 8))
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            x[i] = Corner[i].x;
                            z[i] = Corner[i].z;
                        }
                        return 8;
                    }
                }
                if (Upright(simple.bounds, x, z))
                {
                    return 4;
                }
            }
            return 0;
        }

        private static bool Upright(Bounds box, float[] x, float[] z)
        {
            Vector3 size = box.size;
            if (size.x < 0.5f || size.z < 0.5f || size.x > WidestOutline || size.z > WidestOutline)
            {
                return false;
            }
            Vector3 low = box.min, high = box.max;
            x[0] = low.x;
            z[0] = low.z;
            x[1] = high.x;
            z[1] = low.z;
            x[2] = high.x;
            z[2] = high.z;
            x[3] = low.x;
            z[3] = high.z;
            return true;
        }

        private static bool Fits(Vector3[] corners, int count)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minZ = Mathf.Min(minZ, corners[i].z);
                maxZ = Mathf.Max(maxZ, corners[i].z);
            }
            return maxX - minX >= 0.5f && maxZ - minZ >= 0.5f && maxX - minX <= WidestOutline && maxZ - minZ <= WidestOutline;
        }

        /// <summary>A building's front door, where the game's map points for it, or its own place; false for no building.</summary>
        public static bool Door(CityBuildingController building, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            if (building == null)
            {
                return false;
            }
            Vector3 at = building.entranceDoors != null && building.entranceDoors.Length > 0 && building.entranceDoors[0] != null && building.entranceDoors[0].doorTransform != null
                ? building.entranceDoors[0].doorTransform.position
                : building.transform.position;
            x = at.x;
            z = at.z;
            return true;
        }

        /// <summary>A building's name in the kept file (NavBaseMath.BuildingKey): its address, which the game keeps; 0 with none.</summary>
        public static long KeyOf(CityBuildingController building)
        {
            try
            {
                Address address = building != null && building.building != null ? building.building.Address : null;
                return address != null && !address.IsUndefined() ? NavBaseMath.BuildingKey(address.streetName, address.streetNumber) : 0L;
            }
            catch (Exception)
            {
                return 0L;   // a building with no address the game can read
            }
        }

        /// <summary>
        /// Each kept outline's building in the game's list as it stands now, found by address and never by place in the list (the game
        /// fills it with FindObjectsByType(None) and the mod's own buildings join and leave it, so a place changes between loads and
        /// within one): <paramref name="found"/>[h] is the building of outline h, or null. A building the mod made is left out, as
        /// when the city was measured. Returns how many outlines found their building. A pass over the list: for a check or a test, never a frame.
        /// </summary>
        public static int Find(CityBuildingController[] buildings, NavBaseData data, CityBuildingController[] found)
        {
            var key = new long[buildings.Length];
            for (int i = 0; i < buildings.Length; i++)
            {
                Func<CityBuildingController, bool> made = NavBridges.NotMeasured;
                key[i] = buildings[i] != null && !(made != null && made(buildings[i])) ? KeyOf(buildings[i]) : 0L;
            }
            var place = new int[data.Hulls];
            int matched = NavBaseMath.Resolve(data.HullKey, data.Hulls, key, key.Length, place);
            for (int h = 0; h < data.Hulls; h++)
            {
                found[h] = place[h] >= 0 ? buildings[place[h]] : null;
            }
            return matched;
        }

        /// <summary>What the game calls a building: its address as the game writes it, or the object's name.</summary>
        public static string NameOf(CityBuildingController building)
        {
            try
            {
                Address address = building != null && building.building != null ? building.building.Address : null;
                if (address != null && !address.IsUndefined())
                {
                    return address.ToFormattedString();
                }
            }
            catch (Exception)
            {
                // a building with no address the game can write
            }
            return building != null ? building.name : string.Empty;
        }

        // Every layer but people, cars, the interface and the game's invisible helpers.
        private static int Solid()
        {
            if (_solid == 0)
            {
                _solid = ~LayerMask.GetMask("Player", "Human", "Ignore Raycast", "Vehicles", "AiVehicles", "PlayerVehicles", "ParkedVehicles", "UI", "TransparentFX",
                                            "AutoParkSpots", "ParkingArea", "Guiders", "Trigger", "InteractiveItems", "InteractiveItemsOutlined");
            }
            return _solid;
        }

        /// <summary>One square of the water grid from a ray down; the navigation mesh is asked only where that leaves it open.</summary>
        public static byte Ask(float x, float z, float top, float depth, float streetY)
        {
            if (!Physics.Raycast(new Vector3(x, top, z), Vector3.down, out RaycastHit hit, depth, Solid(), QueryTriggerInteraction.Ignore))
            {
                return NavBaseWater.Classify(false, false, false, false, 0f, streetY);
            }
            Collider collider = hit.collider;
            int layer = collider.gameObject.layer;
            bool built = layer == LayerHelper.RoadsLayerIndex || layer == LayerHelper.BuildingLayerIndex || layer == LayerHelper.BuildingOutlinedLayerIndex;
            bool named = !built && NamedWater(collider.name);
            bool walkable = !built && !named && !NavBaseWater.AtStreetLevel(hit.point.y, streetY)
                            && NavMesh.SamplePosition(hit.point, out _, WalkReach, NavMesh.AllAreas);
            return NavBaseWater.Classify(true, named, built, walkable, hit.point.y, streetY);
        }

        // As Leisure's fishing reads a name: the game's water has no component to ask.
        private static bool NamedWater(string name)
        {
            return name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("river", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("sea", StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
