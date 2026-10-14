using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Paint on the road, laid the way the game lays its own. The game's road markings (its lines,
    /// arrows and crossings, the no-parking grid) are decals of the shader HDRP/Decal that colour
    /// the surface and nothing else: albedo only, from a colour map, at half strength. For each of
    /// the mod's pictures (PropAssets/scripts/decals.py, in the prop bundle: the words BUS STOP and
    /// one dash of a broken line) a copy of one of those materials is made with the picture put in,
    /// so the shader, its keywords and so its compiled variant are the game's own and certain to be
    /// in the build. The paint is laid by a DecalProjector pointing down at the road, which follows
    /// a road that is cambered or falls away where a flat model would sink into it or float.
    ///
    /// The game's road paint to copy is found behind the loading screen (<see cref="Prewarm"/>, Core/SceneFinds:
    /// one look through every material in memory, which was a frame of 5 to 30 ms in play the first time a bay
    /// was dressed after each load), and kept for the session: a city loaded again looks only if the one it had
    /// is gone. The two copies are made when a stop with a bay is first dressed, and go with the city.
    /// </summary>
    public static class RoadPaint
    {
        public const string Words = "decal_bus_stop";
        public const string Dash = "decal_dash";

        /// <summary>A dash of the broken line and its gap, in metres, and the dash's share of it (decals.py).</summary>
        public const float DashRepeat = 1.5f;
        public const float DashPaint = 2f / 3f;

        private const string DecalShader = "HDRP/Decal";
        private const string Template = "M_RoadPaintDecal";   // the game's plain road paint; another decal of the same kind will do
        private const float Lift = 0.08f;                      // the projector stands this far over the road and reaches as far under it: tyres on the paint take little of it
        private const float Strength = 0.75f;                  // the game's own lines are at 0.5, over pictures that are worn less than these
        private const float DrawDistance = 120f;

        public static IModLogger Logger;

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DecalBlendId = Shader.PropertyToID("_DecalBlend");
        private static readonly Dictionary<string, Material> Paints = new Dictionary<string, Material>(2);
        private static Material _template;
        private static bool _looked;
        private static bool _missingSaid;

        /// <summary>Why the last paint asked for could not be had, in a few words for the log; null when it could.</summary>
        public static string Missing { get; private set; }

        /// <summary>
        /// The paint for one of the mod's pictures (<see cref="Words"/>, <see cref="Dash"/>), made the first time it is
        /// asked for. Null if it cannot be made: the prop bundle is not loaded yet, or the city has none of the game's
        /// road paint to copy (the log says which, once).
        /// </summary>
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

        /// <summary>
        /// Behind the loading screen (a piece of Core/SceneFinds' ScenePrewarm): the game's road paint found, unless the one
        /// found at an earlier load is still there. Always done with: where none is found the bays are marked with strips,
        /// and nothing is looked for in play.
        /// </summary>
        public static bool Prewarm()
        {
            if (_template == null)
            {
                _looked = false;   // (the one kept went with its city, or none was found then: looked for again)
            }
            FindTemplate();
            return true;
        }

        // One of the game's own road paints: a decal that colours from a map and does nothing else, as its lines do. Looked for
        // once, behind the loading screen (Prewarm); a question in play before that, or after it found none, searches nothing.
        private static Material FindTemplate()
        {
            if (_template != null || _looked)
            {
                return _template;
            }
            _looked = true;
            if (!SceneFinds.Hidden)
            {
                return null;   // never a search of everything in memory in play: the bays are marked with strips until the next load
            }
            Material another = null;
            // The keywords first (no text made): only decals have these, so the names (each a new string) are read of a handful.
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

        /// <summary>
        /// A projector for one piece of paint, made switched off under <paramref name="parent"/>: <see cref="Pose"/> puts
        /// it down and switches it on.
        /// </summary>
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
        /// Lays a piece of paint flat on the road: its middle at <paramref name="middle"/> on the road's surface, the
        /// picture's height running along <paramref name="along"/>, <paramref name="width"/> by <paramref name="length"/>
        /// metres, the picture laid <paramref name="repeats"/> times along it, moved on by <paramref name="shift"/> of
        /// itself. <paramref name="up"/> is the road's own up there. Set while the projector is switched off and then
        /// switched on: the decal system takes a projector as it is when it comes on.
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

        /// <summary>The city is being left: the copies go. The game's paint found is kept (Prewarm looks again only if it has gone).</summary>
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
