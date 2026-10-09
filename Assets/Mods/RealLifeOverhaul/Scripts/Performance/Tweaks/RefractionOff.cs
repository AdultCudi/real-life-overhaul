using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// With refraction enabled in its frame settings the render pipeline builds, every frame, a blurred pyramid
    /// of the whole picture after the opaque objects are drawn, for see-through materials that bend what is
    /// behind them. The third benchmark measured it at 0.34 ms of the main thread's frame (both pairs agreed).
    /// Nothing outdoors uses it: of the shaders the game ships (181 in its data files, 57 in its asset bundles),
    /// none has a compiled variant that refracts or distorts (the pyramid's texture is referenced by none of
    /// them), and exactly one reads the pyramid at all: SH_PixelatedEffect, the censoring block the game puts on a character sitting on a toilet
    /// (PixelatedCube, created only by ToiletController). So this switches refraction and distortion off for the
    /// player's camera while the player is outdoors, and hands them back the moment a building is entered. As
    /// a second check it looks through the loaded materials when it comes on: if one has refraction or
    /// distortion switched on after all, it does nothing and says which.
    /// </summary>
    public sealed class RefractionOff : PerfTweak
    {
        private static readonly FrameSettingsField[] Fields = { FrameSettingsField.Refraction, FrameSettingsField.Distortion, FrameSettingsField.RoughDistortion };
        private static readonly string[] Keywords = { "_REFRACTION_PLANE", "_REFRACTION_SPHERE", "_REFRACTION_THIN" };

        private bool _hooked, _off;
        private int _materials;
        private string _users = string.Empty;

        public override string Id => "refraction";
        public override string LabelKey => "reallife_perf_refraction";
        public override bool DefaultOn => true;
        public override float Interval => 2f;

        protected override void Apply()
        {
            Scan();
            GlobalEvents.onEnterBuilding += OnBuilding;
            GlobalEvents.onExitBuilding += OnBuilding;
            _hooked = true;
            Reconcile();
        }

        internal override void WorldChanged()
        {
            if (_users.Length == 0)
            {
                Scan();   // a scene brought new materials: look once more
            }
        }

        // Every material in memory, used or not: once when the tweak comes on and once per scene loaded.
        private void Scan()
        {
            Material[] all = Resources.FindObjectsOfTypeAll<Material>();
            _materials = all.Length;
            var users = new List<string>(4);
            foreach (Material material in all)
            {
                if (material == null || material.shader == null)
                {
                    continue;
                }
                bool uses = false;
                foreach (string keyword in Keywords)
                {
                    uses |= material.IsKeywordEnabled(keyword);
                }
                uses |= material.HasProperty("_DistortionEnable") && material.GetFloat("_DistortionEnable") > 0.5f && material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType") > 0.5f;
                if (uses && users.Count < 6)
                {
                    users.Add(material.name + " [" + material.shader.name + "]");
                }
            }
            _users = string.Join(", ", users);
        }

        private void OnBuilding(Address address) => Reconcile();

        // On the timer too: the building flag is the game's, and this follows it whatever changed it.
        protected override void Tick(float now)
        {
            Reconcile();
            if (_off)
            {
                PerfFrameSettings.Apply();   // follows the game to a new main camera
            }
        }

        private void Reconcile()
        {
            bool want = _users.Length == 0 && !BuildingManager.IsInsideBuilding;
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
                GlobalEvents.onEnterBuilding -= OnBuilding;
                GlobalEvents.onExitBuilding -= OnBuilding;
            }
            if (_off)
            {
                _off = false;
                PerfFrameSettings.Release(Fields);
            }
        }

        public override string Describe() => _users.Length > 0
            ? "left alone: these materials refract or distort: " + _users
            : (_off ? "refraction and distortion off for the player's camera" : "handed back (inside a building)") + "; " + _materials + " materials looked through, none refracts or distorts";
    }

    /// <summary>
    /// A measurement, not an option: 3,134 obstacles cut holes in the navigation mesh, none of them moving, and
    /// the navigation steps take 0.9 to 1.2 ms a frame for 47 agents. If the engine spends part of that looking
    /// at every obstacle each frame to see whether it has moved, the ones nobody can walk near are pure cost.
    /// For the row this stops the obstacles more than 300 m from the player cutting the mesh (everyone on foot
    /// is within 100 m of the player) and puts them back; the navigation column says what that is worth.
    /// </summary>
    public sealed class FarObstacleTrial : PerfTweak
    {
        private const float Metres = 300f;

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
