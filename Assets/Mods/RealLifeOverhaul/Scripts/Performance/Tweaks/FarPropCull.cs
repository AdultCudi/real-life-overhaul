using System.Collections.Generic;
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
