using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Paint on the road as the game lays its markings: a copy of one of its HDRP/Decal materials with the mod's picture put
    /// in, laid by a DecalProjector. The game's paint is found behind the loading screen (in play the search took 5 to 30 ms)
    /// and kept for the session; the copies go with the city.
    /// </summary>
    public static class RoadPaint
    {
        public const string Words = "decal_bus_stop";
        public const string Dash = "decal_dash";

        public const float DashRepeat = 1.5f;   // metres: a dash of the broken line and its gap (decals.py)
        public const float DashPaint = 2f / 3f;  // the dash's share of it

        private const string DecalShader = "HDRP/Decal";
        private const string Template = "M_RoadPaintDecal";   // the game's plain road paint; another decal of the same kind will do
        private const float Lift = 0.08f;                      // metres over the road the projector stands, and as far under it it reaches
        private const float Strength = 0.75f;                  // the game's own lines are at 0.5, over pictures that are worn less than these
        private const float DrawDistance = 120f;               // metres

        public static IModLogger Logger;

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DecalBlendId = Shader.PropertyToID("_DecalBlend");
        private static readonly Dictionary<string, Material> Paints = new Dictionary<string, Material>(2);
        private static Material _template;
        private static bool _looked;
        private static bool _missingSaid;

        /// <summary>Why the last paint asked for could not be had, for the log; null when it could.</summary>
        public static string Missing { get; private set; }

        /// <summary>The paint for one of the mod's pictures, made when first asked for; null if it cannot be made yet.</summary>
        public static Material Paint(string picture)
        {
            if (Paints.TryGetValue(picture, out Material made) && made != null)
            {
                return made;
            }
            Texture2D texture = PropLibrary.Texture(picture);
            if (texture == null)
            {
                Missing = PropLibrary.Ready ? "the prop bundle has no picture '" + picture + "': it was built before the road paint" : "the prop bundle is not loaded yet";
                if (PropLibrary.Ready && !_missingSaid)
                {
                    _missingSaid = true;
                    Logger?.Warn("Road paint: " + Missing + "; bus zones are marked with plain strips.");
                }
                return null;
            }
            Material template = FindTemplate();
            if (template == null)
            {
                Missing = "the city has none of the game's road paint to copy";
                return null;
            }
            Missing = null;
            made = new Material(template) { name = "RealLifeOverhaul Road Paint (" + picture + ")" };
            made.SetTexture(BaseColorMapId, texture);
            made.SetColor(BaseColorId, Color.white);
            made.SetFloat(DecalBlendId, Strength);
            Paints[picture] = made;
            return made;
        }

        /// <summary>Behind the loading screen: finds the game's road paint unless the one from an earlier load is still there.</summary>
        public static bool Prewarm()
        {
            if (_template == null)
            {
                _looked = false;
            }
            FindTemplate();
            return true;
        }

        // A decal of the game's that colours from a map and nothing else. Searched for only behind a screen.
        private static Material FindTemplate()
        {
            if (_template != null || _looked)
            {
                return _template;
            }
            _looked = true;
            if (!SceneFinds.Hidden)
            {
                return null;   // the bays are marked with strips until the next load
            }
            Material another = null;
            // The keywords first: only decals have these, so a name (a new string each) is read of a handful.
            foreach (Material material in SceneFinds.InMemory<Material>("transit: the game's road paint being looked for (everything in memory gone through)"))
            {
                if (material == null || material.shader == null
                    || !material.IsKeywordEnabled("_COLORMAP") || !material.IsKeywordEnabled("_MATERIAL_AFFECTS_ALBEDO")
                    || material.IsKeywordEnabled("_NORMALMAP") || material.IsKeywordEnabled("_MASKMAP") || material.shader.name != DecalShader)
                {
                    continue;
                }
                if (material.name == Template)
                {
                    _template = material;
                    break;
                }
                another = another ?? material;
            }
            _template = _template != null ? _template : another;
            Logger?.Info(_template != null
                ? "Road paint: laid with copies of the game's '" + _template.name + "' (" + DecalShader + ", " + string.Join(" ", _template.shaderKeywords) + ")."
                : "Road paint: the city has none of the game's road paint to copy (no " + DecalShader + " material that colours from a map only): bus zones are marked with plain strips.");
            return _template;
        }

        /// <summary>A projector for one piece of paint, made switched off.</summary>
        public static DecalProjector Make(Transform parent, string name, Material paint)
        {
            var piece = new GameObject(name);
            piece.SetActive(false);
            piece.transform.SetParent(parent, false);
            var decal = piece.AddComponent<DecalProjector>();
            decal.material = paint;
            decal.drawDistance = DrawDistance;
            decal.fadeScale = 0.9f;
            decal.fadeFactor = 1f;
            decal.pivot = new Vector3(0f, 0f, Lift);
            return decal;
        }

        /// <summary>
        /// Lays a piece of paint flat on the road, the picture repeated and shifted along it. Done with the projector switched
        /// off, as the decal system takes one as it is when it comes on.
        /// </summary>
        public static void Pose(DecalProjector decal, Vector3 middle, Vector3 along, Vector3 up, float width, float length, float repeats, float shift)
        {
            GameObject piece = decal.gameObject;
            bool on = piece.activeSelf;
            if (on)
            {
                piece.SetActive(false);
            }
            Vector3 forward = Vector3.ProjectOnPlane(along, up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = along;
                up = Vector3.up;
            }
            piece.transform.SetPositionAndRotation(middle + up * Lift, Quaternion.LookRotation(-up, forward.normalized));
            decal.size = new Vector3(width, length, Lift * 2f);
            decal.uvScale = new Vector2(1f, repeats);
            decal.uvBias = new Vector2(0f, shift);
            piece.SetActive(true);
        }

        /// <summary>The city is being left: the copies go; the game's paint is kept.</summary>
        public static void Dispose()
        {
            foreach (Material made in Paints.Values)
            {
                if (made != null)
                {
                    Object.Destroy(made);
                }
            }
            Paints.Clear();
            _missingSaid = false;
            Missing = null;
        }
    }
}
