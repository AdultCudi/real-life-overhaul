using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Per-layer draw distances on the player's camera: the shortest asked, its own back once nobody asks.</summary>
    internal static class PerfCullDistances
    {
        private static readonly List<LayerCull> Users = new List<LayerCull>(4);
        private static readonly float[] Mine = new float[32];
        private static Camera _camera;
        private static float[] _was;
        private static bool _wasSpherical;
        private static bool _hooked;

        public static bool Applied => _camera != null;
        public static float FarPlane => _camera != null ? _camera.farClipPlane : 0f;

        public static void Add(LayerCull user)
        {
            if (!Users.Contains(user))
            {
                Users.Add(user);
            }
            MapParts.Add("draw distances", MapLookCall);   // asked after the map shut, with Developer diagnostics on (Diagnostics/SessionWatch.Map)
            Hook(true);
            Reconcile();
        }

        public static void Remove(LayerCull user)
        {
            Users.Remove(user);
            if (Users.Count == 0)
            {
                Hook(false);
            }
            Reconcile();
        }

        private static void Hook(bool on)
        {
            if (on == _hooked)
            {
                return;
            }
            _hooked = on;
            if (on)
            {
                GlobalEvents.onCityMapToggle += MapToggled;
                GlobalEvents.onCityMapClosed += MapClosed;
            }
            else
            {
                GlobalEvents.onCityMapToggle -= MapToggled;
                GlobalEvents.onCityMapClosed -= MapClosed;
            }
        }

        private static void OnMapToggled(bool open) => Reconcile();

        // through the guard: a fault never reaches the game's own map handlers
        private static readonly System.Action<bool> MapToggled = Guard.Wrap<bool>(OnMapToggled);
        private static readonly System.Action MapClosed = Guard.Wrap(Reconcile);
        private static readonly MapParts.Look MapLookCall = MapLook;   // made once

        // The map check after the map shut (Diagnostics/SessionWatch.Map): the options' draw distances on the player's camera
        // again (the map draws with the camera's own). Asked with no line: references only; with a line its 32 values are
        // compared too (the engine hands out a copy: once a map).
        private static int MapLook(StringBuilder line)
        {
            if (Users.Count == 0)
            {
                line?.Append("draw distances: no option asks for any");
                return 0;
            }
            Camera camera = GameManager.GetMainCamera();
            bool applied = _camera != null && camera == _camera && camera.layerCullSpherical;
            if (line == null)
            {
                return applied ? 0 : 1;
            }
            line.Append("draw distances: ");
            if (!applied)
            {
                line.Append("not the options' again (").Append(_camera == null ? "none applied" : camera != _camera ? "another camera" : "flat").Append(')');
                return 1;
            }
            float[] now = camera.layerCullDistances;
            int differ = 0;
            for (int layer = 0; layer < now.Length && layer < Mine.Length; layer++)
            {
                differ += Mathf.Approximately(now[layer], Mine[layer]) ? 0 : 1;
            }
            line.Append(differ == 0 ? "the options' again on the player's camera" : "the options' only in part, layers that differ: ").Append(differ == 0 ? string.Empty : differ.ToString());
            return differ == 0 ? 0 : 1;
        }

        /// <summary>Brings the camera in line: also follows the game to a new main camera and a changed far plane.</summary>
        public static void Reconcile()
        {
            Camera camera = GameManager.GetMainCamera();
            bool want = Users.Count > 0 && camera != null && !CityMap.IsOpen;
            if (_camera != null && (!want || camera != _camera))
            {
                _camera.layerCullDistances = _was;
                _camera.layerCullSpherical = _wasSpherical;
                _camera = null;
            }
            if (!want)
            {
                return;
            }
            if (_camera == null)
            {
                _camera = camera;
                _was = camera.layerCullDistances;   // a copy of the camera's own 32 values
                _wasSpherical = camera.layerCullSpherical;
            }
            float far = camera.farClipPlane;
            bool changed = false;
            for (int layer = 0; layer < 32; layer++)
            {
                float metres = _was[layer];
                foreach (LayerCull user in Users)
                {
                    float asked = user.DistanceFor(layer, far);
                    if (asked > 0f && (metres <= 0f || asked < metres))
                    {
                        metres = asked;
                    }
                }
                changed |= !Mathf.Approximately(Mine[layer], metres);
                Mine[layer] = metres;
            }
            if (changed || !camera.layerCullSpherical)
            {
                camera.layerCullDistances = Mine;
                camera.layerCullSpherical = true;   // by distance from the camera: turning the view brings nothing in or out at its edges
            }
        }
    }

    /// <summary>
    /// A draw distance for some layers. view_radius: the flat far plane lets a wide picture draw things 470 m away at its
    /// sides, so things (not buildings, roads or ground) are held to 300 m all round. far_props: street furniture, items
    /// and parked cars within 200 m, a visible trade.
    /// </summary>
    public sealed class LayerCull : PerfTweak
    {
        private readonly string _id, _label;
        private readonly string[] _layers;
        private readonly float _metres;   // metres; 0 = the camera's far plane
        private readonly int[] _indices;

        public LayerCull(string id, string label, float metres, params string[] layers)
        {
            _id = id;
            _label = label;
            _metres = metres;
            _layers = layers;
            _indices = new int[layers.Length];
        }

        public override string Id => _id;
        public override string LabelKey => _label;
        public override bool DefaultOn => false;
        public override float Interval => 2f;

        internal float DistanceFor(int layer, float farPlane)
        {
            foreach (int index in _indices)
            {
                if (index == layer)
                {
                    return _metres > 0f ? _metres : farPlane;
                }
            }
            return 0f;
        }

        protected override void Apply()
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                _indices[i] = LayerMask.NameToLayer(_layers[i]);   // -1 for a layer this game lacks: matches nothing
            }
            PerfCullDistances.Add(this);
        }

        protected override void Tick(float now) => PerfCullDistances.Reconcile();

        protected override void Restore() => PerfCullDistances.Remove(this);

        public override string Describe() => PerfCullDistances.Applied
            ? _layers.Length + " layers drawn only within " + (_metres > 0f ? _metres : PerfCullDistances.FarPlane) + " m of the camera in every direction (far plane "
              + PerfCullDistances.FarPlane + " m)"
            : "waiting (no camera, or the city map is open)";
    }
}
