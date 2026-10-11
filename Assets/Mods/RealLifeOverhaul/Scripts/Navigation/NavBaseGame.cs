using System;
using System.IO;
using Helpers;
using Streets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// Everything the minimap's base picture reads from the game, and the only place of it that touches the game.
    ///
    /// The game's own city map is not a picture: it is the city itself seen by a camera (CityMap.Toggle sets the main
    /// camera's culling mask and switches to the map's Cinemachine camera; CityMap.cs 221-284), on which every
    /// building is drawn by its simple "lodVersion" mesh (CityBuildingController.ToggleLodMode, 230-244). So there is
    /// no texture to show, and the base is drawn from what the game keeps:
    ///
    ///   buildings   CityManager.cityBuildingControllers; each one's outline is the outline of its lodVersion renderer
    ///               seen from above (the very mesh the game's map shows), or of all its renderers if it has none;
    ///   water       the game has no water object, layer or list. A place is asked the way Leisure's fishing asks:
    ///               one look straight down (Physics.Raycast), and whether people can walk there (the navigation mesh);
    ///   colours     the game's interface palette (GlobalReferences.colors), which the minimap's roads use already;
    ///   the game's build number (GameVersion), which names the file the measured city is kept in.
    ///
    /// Nothing here runs by itself or per frame: NavBase calls it while it measures a city (once per version of the
    /// game), and the director for its checks and tests.
    /// </summary>
    internal static class NavBaseGame
    {
        private const float WidestOutline = 600f;   // metres: an outline wider than this is not one building's (a renderer that reports a whole batch)
        private const float WalkReach = 1.2f;       // metres from the place looked at that still count as "people walk here" (the fishing rule's figure)
        private const string Folder = "RealLifeOverhaul";

        private static readonly Vector3[] Corner = new Vector3[8];
        private static int _solid;

        /// <summary>The game's build number; 0 if it cannot be read (the file is then kept under 0 and made again by the next version that can).</summary>
        public static int Build()
        {
            GameVersion version = GameVersion.GetCurrent();
            return version != null ? version.buildNumber : 0;
        }

        /// <summary>Where the measured city of a build is kept: in the mod's own folder beside the game's saves.</summary>
        public static string FilePath(int build)
        {
            return Path.Combine(Application.persistentDataPath, Folder, "minimap_base_" + build + ".bin");
        }

        /// <summary>The file's bytes, or null if there is none (or it cannot be read: it is then made again).</summary>
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

        /// <summary>
        /// The two colours, from the game's palette: buildings a faint shade of the roads' own light grey, water the
        /// palette's dark blue. False while the game's references are not there yet.
        /// </summary>
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
            building.a = 84;    // of 255: a third as strong as the roads, so that a road stays the first thing seen
            water = references.colors.darkBlue;
            if (water.r + water.g + water.b < 24)
            {
                // The palette's dark blue is not set in this version of the game: its blue, at six tenths.
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
            return city != null ? city.cityBuildingControllers : null;
        }

        /// <summary>
        /// The corners of a building's outline seen from above, into <paramref name="x"/> and <paramref name="z"/>
        /// (room for 8). Returns how many were written: 8 for a mesh of its own (its box, turned as the building is),
        /// 4 for the engine's upright box round it, 0 if the building has nothing to measure.
        /// </summary>
        public static int Outline(CityBuildingController building, float[] x, float[] z)
        {
            if (building == null)
            {
                return 0;
            }
            MeshRenderer simple = building.lodVersion;
            if (simple != null)
            {
                // A mesh that is not part of a static batch still has its own box, which keeps the building's turn.
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

        /// <summary>A building's front door (where the game's map points for it: CityMap.GetBuildingToFocusPos), or the building's own place. False for no building.</summary>
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

        // Everything that is not part of the city itself: people, cars, the interface, and the game's own invisible helpers.
        private static int Solid()
        {
            if (_solid == 0)
            {
                _solid = ~LayerMask.GetMask("Player", "Human", "Ignore Raycast", "Vehicles", "AiVehicles", "PlayerVehicles", "ParkedVehicles", "UI", "TransparentFX",
                                            "AutoParkSpots", "ParkingArea", "Guiders", "Trigger", "InteractiveItems", "InteractiveItemsOutlined");
            }
            return _solid;
        }

        /// <summary>
        /// What one square of the water grid is (NavBaseWater.Classify), from one look straight down at its middle
        /// from <paramref name="top"/>, <paramref name="depth"/> metres far. One ray; and, only where that leaves the
        /// answer open (something lies there, well below the street, that is neither road nor building), one
        /// question to the navigation mesh.
        /// </summary>
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

        // As Leisure's fishing reads a name (LeisureVenues.NamedWater): the game's water has no component to ask.
        private static bool NamedWater(string name)
        {
            return name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("river", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("sea", StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
