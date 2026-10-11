using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own furniture, as it is drawn and nothing more: a copy of one of the game's item prefabs (its office desks,
    /// chairs, computers, lockers, plants, lamps and the rest: Prefabs/&lt;name&gt;.prefab, loaded with the game's own
    /// PrefabHelper.LoadPrefabAssetByName) with everything that makes it an item taken off before it ever wakes.
    ///
    /// HOW. The copy is made under a holder that is switched off, so not one of its scripts runs (Unity calls no Awake on
    /// an object made inactive). Then, while it is still asleep: the children the game uses to place and sell an item are
    /// removed (the ground and direction indicators, the attachment points with their decals, the navigation targets, the
    /// "for sale" sign, a counter's waiting line and spots), and so is every script, collider, rigid body, obstacle, sound
    /// and sprite left on it, a light's own data excepted. What remains is its meshes, its LOD group and the game's own
    /// materials on them. A light keeps its light, with its shadows off. The copy is then handed over, still switched off.
    ///
    /// Every item prefab of the game stands on the floor at its pivot with its front towards +Z (its desk's seat point
    /// is 0.75 m out along +Z); a wall item hangs from its back face at z = 0; a ceiling lamp from its top at y = 0.
    ///
    /// COST. One copy per piece, made when it is first wanted and kept: the prefab is the game's own, loaded once per
    /// game session by the game's own cache (PrefabHelper). Nothing here runs by itself, and a copy has no script at all.
    /// </summary>
    internal static class GameFurniture
    {
        public static IModLogger Logger;

        // The children of an item prefab that are how the game places, sells or serves at it: never part of the look.
        private static readonly string[] HelperPrefixes =
        {
            "GroundIndicator", "DirectionIndicator", "NavMeshTarget", "ForSaleVisuals", "WaitingLine", "Spots", "SpotTemplate", "AnchorTemplate",
            "EmployeeSpot", "FirstCustomerSpot", "CustomerSpot"
        };

        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<Component> Components = new List<Component>(64);
        private static readonly List<Transform> Children = new List<Transform>(64);
        private static Transform _asleep;

        /// <summary>A copy of the game's item <paramref name="prefab"/> to look at, switched off, under <paramref name="parent"/>; null if the game has no such prefab (said once).</summary>
        public static Transform Make(string prefab, Transform parent)
        {
            GameObject source = null;
            try
            {
                source = PrefabHelper.LoadPrefabAssetByName(prefab);
            }
            catch (Exception e)
            {
                Logger?.Warn("Game furniture: '" + prefab + "' could not be loaded (" + e.Message + ").");
            }
            if (source == null)
            {
                if (Missing.Add(prefab))
                {
                    Logger?.Info("Game furniture: the game has no item prefab '" + prefab + "'; it is left out.");
                }
                return null;
            }
            if (_asleep == null)
            {
                var holder = new GameObject("RealLife Furniture (asleep)");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                _asleep = holder.transform;
            }
            GameObject copy = UnityEngine.Object.Instantiate(source, _asleep, false);
            copy.name = "Game " + prefab;
            Strip(copy.transform);
            copy.SetActive(false);
            copy.transform.SetParent(parent, false);
            return copy.transform;
        }

        private static void Strip(Transform root)
        {
            // The game's placing and selling helpers, by name, wherever they hang.
            Children.Clear();
            root.GetComponentsInChildren(true, Children);
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                Transform t = Children[i];
                if (t != null && t != root && IsHelper(t.name))
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }
            // Every script and every piece of physics: in passes, as one may be needed by another until that one is gone.
            for (int pass = 0; pass < 4; pass++)
            {
                Components.Clear();
                root.GetComponentsInChildren(true, Components);
                int removed = 0;
                for (int i = Components.Count - 1; i >= 0; i--)
                {
                    Component c = Components[i];
                    if (c == null || !Unwanted(c))
                    {
                        continue;
                    }
                    UnityEngine.Object.DestroyImmediate(c);
                    removed += c == null ? 1 : 0;
                }
                if (removed == 0)
                {
                    break;
                }
            }
            Components.Clear();
            root.GetComponentsInChildren(true, Components);
            for (int i = 0; i < Components.Count; i++)
            {
                if (Components[i] is Light light)
                {
                    light.shadows = LightShadows.None;   // a lamp of the room's: light, no shadow map of its own
                }
            }
            Components.Clear();
            Children.Clear();
        }

        private static bool IsHelper(string name)
        {
            if (name.IndexOf("AttachmentPoint", StringComparison.Ordinal) >= 0)
            {
                return true;
            }
            for (int i = 0; i < HelperPrefixes.Length; i++)
            {
                if (name.StartsWith(HelperPrefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Unwanted(Component c)
        {
            if (c is Transform || c is MeshFilter || c is Renderer && !(c is SpriteRenderer) || c is LODGroup || c is Light)
            {
                return false;
            }
            if (c is MonoBehaviour)
            {
                return c.GetType().Name != "HDAdditionalLightData";   // (a light of the game's pipeline is drawn by it)
            }
            return c is Collider || c is Rigidbody || c is NavMeshObstacle || c is AudioSource || c is SpriteRenderer || c is Animator || c is ReflectionProbe || c is Canvas;
        }

        /// <summary>The bounds of what a copy draws, in its own space (its pivot at the origin): the size the room is planned with.</summary>
        public static Bounds LocalBounds(Transform piece)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            Components.Clear();
            piece.GetComponentsInChildren(true, Components);
            Matrix4x4 toLocal = piece.worldToLocalMatrix;
            for (int i = 0; i < Components.Count; i++)
            {
                if (!(Components[i] is MeshFilter filter) || filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 m = toLocal * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = mesh.center + Vector3.Scale(mesh.extents, new Vector3((corner & 1) != 0 ? 1f : -1f, (corner & 2) != 0 ? 1f : -1f, (corner & 4) != 0 ? 1f : -1f));
                    Vector3 local = m.MultiplyPoint3x4(p);
                    if (!any)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }
            Components.Clear();
            return bounds;
        }
    }
}
