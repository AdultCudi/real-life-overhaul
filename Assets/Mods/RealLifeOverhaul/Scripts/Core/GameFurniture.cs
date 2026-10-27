using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own furniture as it is drawn and nothing more: a copy of one of its item prefabs (loaded with
    /// PrefabHelper.LoadPrefabAssetByName) with everything that makes it an item taken off before it wakes. The copy is
    /// made under a holder that is switched off, so none of its scripts runs; the placing and selling helpers and every
    /// script, collider, body and sound go, leaving meshes, LODs and the game's materials (a light keeps its light,
    /// without shadows). Item prefabs stand at their pivot facing +Z; wall items hang from z = 0, ceiling lamps from y = 0.
    /// COST: nothing runs by itself; a copy is made when first wanted. FitOut copies from a kept stripped template, one
    /// instantiation per further copy.
    /// </summary>
    internal static class GameFurniture
    {
        public static IModLogger Logger;

        // The children by which the game places, sells or serves at an item: never part of the look.
        private static readonly string[] HelperPrefixes =
        {
            "GroundIndicator", "DirectionIndicator", "NavMeshTarget", "ForSaleVisuals", "WaitingLine", "Spots", "SpotTemplate", "AnchorTemplate",
            "EmployeeSpot", "FirstCustomerSpot", "CustomerSpot"
        };

        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<Component> Components = new List<Component>(64);
        private static readonly List<Transform> Children = new List<Transform>(64);
        private static Transform _asleep;

        /// <summary>A stripped copy of the game's item, switched off, under <paramref name="parent"/>; null if the game has no such prefab.</summary>
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
            int seats = ReadSeats(copy);
            Strip(copy.transform);
            for (int i = 0; i < seats; i++)
            {
                var seat = new GameObject(SeatName).transform;
                seat.SetParent(copy.transform, false);
                seat.localPosition = SeatPoses[i].Key;
                seat.localRotation = SeatPoses[i].Value;
            }
            copy.SetActive(false);
            copy.transform.SetParent(parent, false);
            return copy.transform;
        }

        private static readonly Dictionary<string, Transform> Templates = new Dictionary<string, Transform>(64, StringComparer.Ordinal);
        private static readonly Dictionary<string, Bounds> TemplateBounds = new Dictionary<string, Bounds>(64, StringComparer.Ordinal);

        /// <summary>True once a template of the prefab was made or found missing: <see cref="Copy"/> is then cheap.</summary>
        public static bool HasTemplate(string prefab) => Templates.ContainsKey(prefab);

        /// <summary>
        /// The kept stripped copy of a prefab, made on first asking (the costly part); null if the game has none. Kept
        /// asleep with its drawn bounds until <see cref="Forget"/>.
        /// </summary>
        public static Transform Template(string prefab, out Bounds bounds)
        {
            if (Templates.TryGetValue(prefab, out Transform made))
            {
                TemplateBounds.TryGetValue(prefab, out bounds);
                return made;
            }
            made = Make(prefab, null);
            bounds = default;
            if (made != null)
            {
                made.SetParent(_asleep, false);
                made.name = "Template " + prefab;
                bounds = LocalBounds(made);
                TemplateBounds[prefab] = bounds;
            }
            Templates[prefab] = made;   // a missing prefab is remembered as missing
            return made;
        }

        /// <summary>As <see cref="Make"/>, from the template: one instantiation, no load or stripping.</summary>
        public static Transform Copy(string prefab, Transform parent)
        {
            Transform template = Template(prefab, out _);
            if (template == null)
            {
                return null;
            }
            Transform copy = UnityEngine.Object.Instantiate(template, parent, false);
            copy.name = "Game " + prefab;
            copy.gameObject.SetActive(false);
            return copy;
        }

        /// <summary>The city is unloading: the templates go, so the game's assets they hold can be unloaded.</summary>
        public static void Forget()
        {
            foreach (KeyValuePair<string, Transform> pair in Templates)
            {
                if (pair.Value != null)
                {
                    SceneryTrees.Destroy(pair.Value.gameObject);
                }
            }
            Templates.Clear();
            TemplateBounds.Clear();
        }

        /// <summary>The name of the empty children a copy is given at the item's sitting positions.</summary>
        public const string SeatName = "RealLife Seat";

        private static readonly List<KeyValuePair<Vector3, Quaternion>> SeatPoses = new List<KeyValuePair<Vector3, Quaternion>>(8);
        private static FieldInfo _sittingPositions;
        private static bool _sittingLooked;

        // ItemController.sittingPositions, kept as poses in the copy's own space before the script is taken off.
        private static int ReadSeats(GameObject copy)
        {
            SeatPoses.Clear();
            if (!_sittingLooked)
            {
                _sittingLooked = true;
                _sittingPositions = GameContract.SittingPositions.Field;
                if (_sittingPositions == null)
                {
                    Logger?.Info("Game furniture: the game's items have no list of sitting positions any more; nobody is seated on a copy.");
                }
            }
            ItemController item = copy.GetComponent<ItemController>();
            if (_sittingPositions == null || item == null)
            {
                return 0;
            }
            try
            {
                if (_sittingPositions.GetValue(item) is Transform[] places)
                {
                    Transform root = copy.transform;
                    for (int i = 0; i < places.Length && SeatPoses.Count < 8; i++)
                    {
                        Transform place = places[i];
                        if (place != null && place.IsChildOf(root))
                        {
                            SeatPoses.Add(new KeyValuePair<Vector3, Quaternion>(root.InverseTransformPoint(place.position), Quaternion.Inverse(root.rotation) * place.rotation));
                        }
                    }
                }
            }
            catch (Exception)
            {
                SeatPoses.Clear();
            }
            return SeatPoses.Count;
        }

        /// <summary>The places to sit on a copy, in order; none for a piece that is not a seat.</summary>
        public static void Seats(Transform piece, List<Transform> into)
        {
            into.Clear();
            if (piece == null)
            {
                return;
            }
            for (int i = 0; i < piece.childCount; i++)
            {
                Transform child = piece.GetChild(i);
                if (child.name == SeatName)
                {
                    into.Add(child);
                }
            }
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
                    SceneryTrees.DestroyNow(t.gameObject);
                }
            }
            // In passes: a component may be required by another until that one is gone.
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
                    light.shadows = LightShadows.None;   // no shadow map of its own
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
                return c.GetType().Name != "HDAdditionalLightData";   // the pipeline draws a light by it
            }
            return c is Collider || c is Rigidbody || c is NavMeshObstacle || c is AudioSource || c is SpriteRenderer || c is Animator || c is ReflectionProbe || c is Canvas;
        }

        /// <summary>The bounds of what a copy draws, in its own space: the size the room is planned with.</summary>
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
