using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The seasons' looks beside the snow: autumn's falling leaves (one particle system hung where the game's rain falls
    /// by the player, small flat pieces of the shared paint in autumn's colours) and a summer storm's lightning (one
    /// directional light, switched off but for the quarter of a second of a flash). Asked once a second by the director;
    /// a flash is a short routine of a few frames, so while idle nothing of the mod's runs a frame. Gone with the city.
    /// </summary>
    internal sealed class SeasonLook
    {
        private const float LeafFrom = 9f;            // metres over the player the leaves start
        private const float LeafSpan = 36f;           // metres: the square they start over
        private const float LeafLife = 14f;           // seconds: a slow, swaying fall
        private const float LeafRate = 26f;           // leaves a second at the height of autumn
        private const int MostLeaves = 420;
        private const float FlashLux = 16000f;        // a flash as bright as an overcast noon's sun, briefly
        private const float FlashSeconds = 0.22f;
        private const float FlickerSeconds = 0.07f;   // the dark between a flash's two strokes

        // One colour: the lit shader the flakes and props use takes no colour per particle, and a second system would
        // double the cost for a shade.
        private static readonly Color LeafColour = new Color(0.72f, 0.38f, 0.12f);

        private readonly Transform _root;
        private readonly MonoBehaviour _host;
        private ParticleSystem _leaves;
        private bool _leavesFailed, _leavesOn;
        private float _leafRate = -1f;
        private Light _flash;
        private HDAdditionalLightData _flashData;
        private bool _flashing;

        public string LeavesMissing { get; private set; }
        public bool Leafing => _leavesOn && _leaves != null && _leafRate > 0f;
        public int Leaves => _leaves != null ? _leaves.particleCount : 0;
        public int Flashes { get; private set; }

        public SeasonLook(Transform root, MonoBehaviour host)
        {
            _root = root;
            _host = host;
        }

        /// <summary>Autumn's leaves at <paramref name="strength"/> (0 to 1) round the player outdoors; none indoors or at 0.</summary>
        public void Leaf(float strength, bool outdoors)
        {
            if (!outdoors || strength <= 0f)
            {
                StopLeaves(!outdoors);
                return;
            }
            if (_leaves == null && !BuildLeaves())
            {
                return;
            }
            float rate = Mathf.Round(LeafRate * Mathf.Clamp01(strength));
            if (rate != _leafRate)
            {
                ParticleSystem.EmissionModule emission = _leaves.emission;
                emission.rateOverTime = rate;
                _leafRate = rate;
            }
            if (!_leavesOn)
            {
                _leaves.Play(true);
                _leavesOn = true;
            }
        }

        private void StopLeaves(bool clear)
        {
            if (_leaves == null || !_leavesOn)
            {
                return;
            }
            if (clear)
            {
                _leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // indoors: none left hanging in the street
                _leavesOn = false;
                _leafRate = -1f;
            }
            else if (_leafRate != 0f)
            {
                ParticleSystem.EmissionModule off = _leaves.emission;
                off.rateOverTime = 0f;   // those in the air come down; none start
                _leafRate = 0f;
            }
        }

        private bool BuildLeaves()
        {
            if (_leavesFailed)
            {
                return false;
            }
            Transform place = SeasonGame.RainPlace();
            Material paint = Shapes.Paint(LeafColour);
            if (place == null || paint == null)
            {
                _leavesFailed = true;
                LeavesMissing = place == null ? "the game's rain has no place by the player to read" : "the game has no lit shader for the leaves";
                SeasonDirector.Logger?.Warn("Seasons: no falling leaves this session: " + LeavesMissing + ".");
                return false;
            }
            var made = new GameObject("RealLifeOverhaul Falling Leaves");
            made.transform.SetParent(place, false);
            made.transform.localPosition = new Vector3(0f, LeafFrom, 0f);
            var leaves = made.AddComponent<ParticleSystem>();
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = leaves.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = LeafLife;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.006f, 0.01f);   // flat: a leaf, not a cube
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            main.gravityModifier = 0f;
            main.maxParticles = MostLeaves;
            ParticleSystem.EmissionModule emission = leaves.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = leaves.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(LeafSpan, 1f, LeafSpan);
            ParticleSystem.VelocityOverLifetimeModule drift = leaves.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            drift.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.55f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            ParticleSystem.NoiseModule noise = leaves.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.6f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            ParticleSystem.RotationOverLifetimeModule spin = leaves.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var renderer = made.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Shapes.Cube;
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _leaves = leaves;
            LeavesMissing = null;
            return true;
        }

        /// <summary>One flash of lightning: two quick strokes of a light over the city. Nothing if one is still showing.</summary>
        public void Flash()
        {
            if (_flashing || _host == null || !_host.isActiveAndEnabled)
            {
                return;
            }
            if (_flash == null)
            {
                var made = new GameObject("RealLifeOverhaul Lightning");
                made.transform.SetParent(_root, false);
                made.transform.rotation = Quaternion.Euler(62f, 30f, 0f);
                made.SetActive(false);
                _flash = made.AddComponent<Light>();
                _flash.type = LightType.Directional;
                _flash.shadows = LightShadows.None;
                _flash.color = new Color(0.82f, 0.88f, 1f);
                _flashData = made.GetComponent<HDAdditionalLightData>() ?? made.AddComponent<HDAdditionalLightData>();   // one a light
                _flashData.SetIntensity(FlashLux, LightUnit.Lux);
            }
            _flashing = true;
            Flashes++;
            _host.StartCoroutine(Guard.Routine(Strokes()));
        }

        private IEnumerator Strokes()
        {
            try
            {
                _flash.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(FlashSeconds * 0.4f);
                _flash.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(FlickerSeconds);
                _flash.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(FlashSeconds * 0.6f);
            }
            finally
            {
                if (_flash != null)
                {
                    _flash.gameObject.SetActive(false);
                }
                _flashing = false;
            }
        }

        public void Hide()
        {
            StopLeaves(true);
            if (_flash != null)
            {
                _flash.gameObject.SetActive(false);
            }
        }

        /// <summary>The city is unloading: everything made goes.</summary>
        public void Dispose()
        {
            if (_leaves != null)
            {
                Object.Destroy(_leaves.gameObject);
            }
            if (_flash != null)
            {
                Object.Destroy(_flash.gameObject);
            }
            _leaves = null;
            _flash = null;
            _flashData = null;
            _leavesFailed = _leavesOn = _flashing = false;
            _leafRate = -1f;
        }
    }
}
