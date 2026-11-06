using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Refraction and distortion off for the player's camera outdoors (the blur pyramid costs 0.34 ms a frame, and only the
    /// censoring block at a toilet reads it), handed back at a door, once a sliced look through every loaded material finds
    /// none that refracts. No cost a frame: the director's timer, about 0.3 ms a tick while looking.
    /// </summary>
    public sealed class RefractionOff : PerfTweak
    {
        private static readonly FrameSettingsField[] Fields = { FrameSettingsField.Refraction, FrameSettingsField.Distortion, FrameSettingsField.RoughDistortion };
        private static readonly string[] Keywords = { "_REFRACTION_PLANE", "_REFRACTION_SPHERE", "_REFRACTION_THIN" };

        private static readonly int DistortionEnable = Shader.PropertyToID("_DistortionEnable");
        private static readonly int SurfaceType = Shader.PropertyToID("_SurfaceType");

        private const double SliceMs = 0.3;   // milliseconds of a tick a look may take (some 300 materials)
        private const int LookEvery = 16;   // materials between two looks at the clock
        private const float SliceEvery = 0.05f;   // seconds between slices
        private const float LookAfter = 4f;   // seconds after the last scene arrived before the materials are looked through again
        private const float HiddenEvery = 0.25f;   // seconds between looks at whether the screen is black, while a look waits
        private const string Listing = "performance: listing the loaded materials (refraction check)";

        private readonly List<string> _found = new List<string>(6);
        private Material[] _looking;   // null when no look is under way
        private int _cursor;
        private bool _looked;   // a whole look finished since the tweak came on
        private bool _again;   // a scene loaded since the last look began
        private float _againAt;
        private bool _hooked, _off;
        private int _materials;
        private string _users = string.Empty;

        // the list taken behind the loading screen, good for its city while no scene has arrived since
        private Material[] _kept;
        private PerfDirector _keptFor;
        private int _keptArrivals;

        public override string Id => "refraction";
        public override string LabelKey => "reallife_perf_refraction";
        public override bool DefaultOn => true;
        public override float Interval => _looking != null ? SliceEvery : _again && _users.Length == 0 ? HiddenEvery : 2f;
        public override bool Busy => _looking != null || !_looked;

        protected override void Apply()
        {
            _looked = false;
            _again = false;
            _users = string.Empty;
            if (Kept || SceneFinds.Hidden)
            {
                Begin();
            }
            else
            {
                _again = true;   // on in play with no list taken behind the loading screen: taken behind the next black screen
                _againAt = Time.unscaledTime;
            }
            _onBuilding = _onBuilding ?? Guard.Wrap<Address>(OnBuilding);   // through the guard: a fault never reaches the game's handlers
            GlobalEvents.onEnterBuilding += _onBuilding;
            GlobalEvents.onExitBuilding += _onBuilding;
            _hooked = true;
            Reconcile();
        }

        internal override void WorldChanged(bool loaded)
        {
            if (loaded && _users.Length == 0)
            {
                _again = true;   // a scene brought new materials: one more look once scenes stop arriving
                _againAt = Time.unscaledTime + LookAfter;
            }
        }

        private bool Kept => _kept != null && _keptFor != null && ReferenceEquals(_keptFor, PerfDirector.Instance) && _keptArrivals == PerfTweaks.Arrivals;

        protected override void Prewarm()
        {
            _kept = SceneFinds.InMemory<Material>(Listing);
            _keptFor = PerfDirector.Instance;
            _keptArrivals = PerfTweaks.Arrivals;
        }

        // every material in memory, used or not: one engine call, behind a screen where it can be, gone through in slices
        private void Begin()
        {
            _looking = Kept ? _kept : SceneFinds.InMemory<Material>(Listing);
            _kept = null;   // once: a scene that arrives later is looked at again behind a black screen
            _keptFor = null;
            _materials = _looking.Length;
            _cursor = 0;
            _found.Clear();
            _again = false;
        }

        private void Look()
        {
            WorkSlice slice = WorkSlice.Of(SliceMs, LookEvery);
            for (; _cursor < _looking.Length && slice.More(); _cursor++)
            {
                Material material = _looking[_cursor];
                if (material == null || material.shader == null)
                {
                    continue;
                }
                bool uses = false;
                for (int k = 0; k < Keywords.Length; k++)
                {
                    uses |= material.IsKeywordEnabled(Keywords[k]);
                }
                uses |= material.HasProperty(DistortionEnable) && material.GetFloat(DistortionEnable) > 0.5f
                        && material.HasProperty(SurfaceType) && material.GetFloat(SurfaceType) > 0.5f;
                if (uses && _found.Count < 6)
                {
                    _found.Add(material.name + " [" + material.shader.name + "]");
                }
            }
            if (_cursor >= _looking.Length)
            {
                _looking = null;
                _looked = true;
                _users = string.Join(", ", _found);
                _found.Clear();
            }
        }

        private void OnBuilding(Address address) => Reconcile();

        private System.Action<Address> _onBuilding;

        // on the timer too: the building flag is the game's, followed whatever changed it
        protected override void Tick(float now)
        {
            if (_looking != null)
            {
                Look();
                if (_looking != null)
                {
                    return;   // more on the next slice
                }
            }
            else if (_again && now >= _againAt && _users.Length == 0)
            {
                if (SceneFinds.Hidden)
                {
                    Begin();   // everything in memory listed only where a long frame is not seen
                    return;
                }
            }
            Reconcile();
            if (_off)
            {
                PerfFrameSettings.Apply();   // follows the game to a new main camera
            }
        }

        private void Reconcile()
        {
            // not before the first look is finished: until then nobody has said nothing refracts
            bool want = _looked && _users.Length == 0 && !BuildingManager.IsInsideBuilding;
            if (want == _off)
            {
                return;
            }
            _off = want;
            if (want)
            {
                PerfFrameSettings.Disable(Fields);
            }
            else
            {
                PerfFrameSettings.Release(Fields);
            }
        }

        protected override void Restore()
        {
            if (_hooked)
            {
                _hooked = false;
                GlobalEvents.onEnterBuilding -= _onBuilding;
                GlobalEvents.onExitBuilding -= _onBuilding;
            }
            if (_off)
            {
                _off = false;
                PerfFrameSettings.Release(Fields);
            }
            _looking = null;
            _looked = false;
            _again = false;
            _found.Clear();
        }

        public override string Describe() => _users.Length > 0
            ? "left alone: these materials refract or distort: " + _users
            : !_looked ? "looking through the loaded materials (" + _cursor + " of " + _materials + ")"
            : (_off ? "refraction and distortion off for the player's camera" : "handed back (inside a building)") + "; " + _materials
              + " materials looked through, none refracts or distorts";
    }

    /// <summary>A benchmark row: navigation obstacles beyond 300 m stop cutting the mesh (everyone on foot is within 100 m).</summary>
    public sealed class FarObstacleTrial : PerfTweak
    {
        private const float Metres = 300f;   // metres from the player

        private readonly List<UnityEngine.AI.NavMeshObstacle> _stopped = new List<UnityEngine.AI.NavMeshObstacle>(4096);

        public override string Id => "trial_far_obstacles";
        public override string LabelKey => null;
        public override bool DefaultOn => false;
        public override bool Trial => true;

        protected override void Apply()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null)
            {
                return;
            }
            Vector3 player = game.playerController.transform.position;
            foreach (UnityEngine.AI.NavMeshObstacle obstacle in Object.FindObjectsByType<UnityEngine.AI.NavMeshObstacle>(FindObjectsSortMode.None))
            {
                if (obstacle.enabled && obstacle.carving && (obstacle.transform.position - player).sqrMagnitude > Metres * Metres)
                {
                    obstacle.carving = false;
                    _stopped.Add(obstacle);
                }
            }
        }

        protected override void Restore()
        {
            foreach (UnityEngine.AI.NavMeshObstacle obstacle in _stopped)
            {
                if (obstacle != null && !obstacle.carving)
                {
                    obstacle.carving = true;
                }
            }
            _stopped.Clear();
        }

        public override string Describe() => "obstacles beyond " + Metres + " m not cutting the navigation mesh: " + _stopped.Count;
    }
}
