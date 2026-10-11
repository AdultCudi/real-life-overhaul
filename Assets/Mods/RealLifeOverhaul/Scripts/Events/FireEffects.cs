using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The particle effects of a fire: flames, smoke, steam, the water jet, and one flickering
    /// light. All of them are copies of the game's own smoke effect (the one accidents use: the
    /// same search, the same material and texture, so they are drawn exactly as the game draws
    /// smoke), given their motion and colour here. They are made once, the first time there is a
    /// fire, kept switched off and reused; nothing is created or destroyed per fire.
    ///
    /// THE BUDGET (hard caps, set as each system's maxParticles):
    ///   3 flames x 36 + 2 smoke columns x 96 + steam 48 + water jet 70 = 418 particles at most,
    ///   seven particle systems, one point light without shadows. Nothing runs per frame here:
    ///   the light's flicker is a timer of the director's, and only while something burns.
    /// </summary>
    internal sealed class FireEffects
    {
        public const int Flames = 3;
        public const int Smokes = 2;
        public const int ParticleBudget = Flames * FlameMax + Smokes * SmokeMax + SteamMax + JetMax;

        private const int FlameMax = 36;
        private const int SmokeMax = 96;
        private const int SteamMax = 48;
        private const int JetMax = 70;
        private const string EngineSmokeName = "SmokeBrokenCar";

        private static readonly AnimationCurve Grow = AnimationCurve.Linear(0f, 0.32f, 1f, 1f);
        private static readonly AnimationCurve Shrink = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
        private static readonly Gradient SmokeFade = Fade(Color.white, Color.white, 0.12f, 0.6f, 0.75f);
        private static readonly Gradient FlameFade = Fade(new Color(1f, 0.92f, 0.45f), new Color(0.85f, 0.16f, 0.02f), 0.08f, 0.55f, 0.8f);
        private static readonly Gradient JetFade = Fade(Color.white, Color.white, 0.05f, 0.8f, 0.9f);

        private readonly Transform _root;
        private readonly ParticleSystem[] _flames = new ParticleSystem[Flames];
        private readonly float[] _flameSize = new float[Flames];
        private readonly ParticleSystem[] _smokes = new ParticleSystem[Smokes];
        private ParticleSystem _steam;
        private ParticleSystem _jet;
        private Light _light;
        private HDAdditionalLightData _lightData;
        private float _glow;
        private bool _built;
        private bool _unavailable;
        private float _retryAt;

        public FireEffects(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Fire Effects").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>True if the effects can be had (the game has a smoke effect loaded to copy). Builds them the first time.</summary>
        public bool Ready()
        {
            if (_built || _unavailable || Time.time < _retryAt)
            {
                return _built;
            }
            try
            {
                GameObject template = FindTemplate();
                if (template == null)
                {
                    // The game's smoke is only in memory once something has smoked: looked for again in two minutes, not before.
                    _retryAt = Time.time + 120f;
                    EventDirector.Logger?.Info("Fire: the game has no smoke effect loaded to copy yet; no fire can be staged for now.");
                    return false;
                }
                for (int i = 0; i < Flames; i++)
                {
                    _flames[i] = Make(template, "Flame " + i);
                    ShapeFlame(_flames[i]);
                }
                for (int i = 0; i < Smokes; i++)
                {
                    _smokes[i] = Make(template, "Smoke " + i);
                    ShapeSmoke(_smokes[i]);
                }
                _steam = Make(template, "Steam");
                ShapeSteam(_steam);
                _jet = Make(template, "Water Jet");
                ShapeJet(_jet);
                Object.Destroy(template);

                var lamp = new GameObject("Glow");
                lamp.transform.SetParent(_root, false);
                _light = lamp.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(1f, 0.45f, 0.1f);
                _light.range = 18f;
                _light.shadows = LightShadows.None;
                _lightData = lamp.AddComponent<HDAdditionalLightData>();
                _lightData.SetIntensity(12000f, LightUnit.Lumen);
                _lightData.range = 18f;
                lamp.SetActive(false);
                _built = true;
            }
            catch (Exception e)
            {
                _unavailable = true;
                EventDirector.Logger?.Error(e);
            }
            return _built;
        }

        // ---------------------------------------------------------------- what a fire asks for

        /// <summary>A flame at a place. Size is its width in metres (0.5 for a bin, 1.4 for a shopfront).</summary>
        public void Flame(int index, Vector3 at, float size)
        {
            ParticleSystem flame = _flames[index];
            _flameSize[index] = size;
            flame.transform.SetPositionAndRotation(at, Quaternion.identity);
            SetFlame(index, 1f);
            Begin(flame);
        }

        /// <summary>How hard a flame burns, 0 (out: it emits nothing more) to 1.</summary>
        public void SetFlame(int index, float level)
        {
            ParticleSystem flame = _flames[index];
            if (flame == null)
            {
                return;
            }
            float size = _flameSize[index] * Mathf.Lerp(0.45f, 1f, level);
            ParticleSystem.MainModule main = flame.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f + level, 1.6f + level * 1.4f);
            ParticleSystem.EmissionModule emission = flame.emission;
            emission.rateOverTime = level <= 0.01f ? 0f : Mathf.Lerp(10f, 38f, level);
            ParticleSystem.ShapeModule shape = flame.shape;
            shape.radius = Mathf.Max(0.08f, _flameSize[index] * 0.35f * Mathf.Lerp(0.5f, 1f, level));
        }

        /// <summary>A column of smoke from a place: black while it burns, white once water is on it.</summary>
        public void Smoke(int index, Vector3 at, bool white, float rate)
        {
            ParticleSystem smoke = _smokes[index];
            if (smoke == null || (rate <= 0f && !smoke.gameObject.activeSelf))
            {
                return;
            }
            smoke.transform.SetPositionAndRotation(at, Quaternion.identity);
            ParticleSystem.MainModule main = smoke.main;
            main.startColor = white ? new Color(0.86f, 0.86f, 0.86f, 0.5f) : new Color(0.05f, 0.05f, 0.05f, 0.95f);
            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = rate;
            if (!smoke.gameObject.activeSelf)
            {
                Begin(smoke);
            }
        }

        public void Steam(Vector3 at, bool on)
        {
            if (_steam == null)
            {
                return;
            }
            ParticleSystem.EmissionModule emission = _steam.emission;
            emission.rateOverTime = on ? 14f : 0f;
            if (on)
            {
                _steam.transform.SetPositionAndRotation(at, Quaternion.identity);
                if (!_steam.gameObject.activeSelf)
                {
                    Begin(_steam);
                }
            }
        }

        /// <summary>The water: an arc from the nozzle that comes down on a place. Off stops the flow; what is in the air lands.</summary>
        public void Jet(Vector3 from, Vector3 onto, bool on)
        {
            if (_jet == null)
            {
                return;
            }
            ParticleSystem.EmissionModule emission = _jet.emission;
            emission.rateOverTime = on ? 60f : 0f;
            if (!on)
            {
                return;
            }
            Vector3 flat = onto - from;
            float rise = flat.y;
            flat.y = 0f;
            float distance = Mathf.Max(0.5f, flat.magnitude);
            float time = Mathf.Clamp(distance / 11f, 0.45f, 1.15f);
            float gravity = Mathf.Abs(Physics.gravity.y);
            Vector3 velocity = flat / time + Vector3.up * ((rise + 0.5f * gravity * time * time) / time);
            _jet.transform.SetPositionAndRotation(from, Quaternion.LookRotation(velocity.normalized));
            ParticleSystem.MainModule main = _jet.main;
            main.startSpeed = velocity.magnitude;
            main.startLifetime = time;
            if (!_jet.gameObject.activeSelf)
            {
                Begin(_jet);
            }
        }

        /// <summary>The firelight: where, and how strong (0 puts it out).</summary>
        public void Glow(Vector3 at, float level)
        {
            if (_light == null)
            {
                return;
            }
            _glow = level;
            bool on = level > 0.02f;
            if (_light.gameObject.activeSelf != on)
            {
                _light.gameObject.SetActive(on);
            }
            if (on)
            {
                _light.transform.position = at;
            }
        }

        /// <summary>True while the firelight is on: the director then calls <see cref="Flicker"/> on a timer.</summary>
        public bool Burning => _glow > 0.02f;

        public void Flicker()
        {
            if (_lightData != null && _glow > 0.02f)
            {
                _lightData.SetIntensity(Random.Range(7000f, 16000f) * _glow, LightUnit.Lumen);
            }
        }

        /// <summary>Everything off, now. Safe at any time, built or not.</summary>
        public void StopAll()
        {
            if (!_built)
            {
                return;
            }
            for (int i = 0; i < Flames; i++)
            {
                End(_flames[i]);
            }
            for (int i = 0; i < Smokes; i++)
            {
                End(_smokes[i]);
            }
            End(_steam);
            End(_jet);
            Glow(default, 0f);
        }

        private static void Begin(ParticleSystem particles)
        {
            particles.gameObject.SetActive(true);
            particles.Clear(true);
            particles.Play(true);
        }

        private static void End(ParticleSystem particles)
        {
            if (particles != null && particles.gameObject.activeSelf)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- building them

        // The game's smoke, as Traffic/TrafficEffects finds it: the broken-engine smoke if it is in memory, else any smoke.
        private GameObject FindTemplate()
        {
            ParticleSystemRenderer source = null;
            foreach (ParticleSystemRenderer renderer in SceneFinds.InMemory<ParticleSystemRenderer>("events: the game's smoke effect being looked for (everything in memory gone through)"))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) < 0 || renderer.GetComponent<ParticleSystem>() == null
                    || renderer.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal) || renderer.transform.root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
                {
                    continue;
                }
                if (renderer.name.IndexOf(EngineSmokeName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    source = renderer;
                    break;
                }
                if (source == null)
                {
                    source = renderer;
                }
            }
            if (source == null)
            {
                return null;
            }
            var holder = new GameObject("RealLifeOverhaul Fire Template Holder");
            holder.SetActive(false);   // so that no game script on the copy ever wakes up
            holder.transform.SetParent(_root, false);
            GameObject copy = Object.Instantiate(source.gameObject, holder.transform);
            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(script);
            }
            EventDirector.Logger?.Info($"Fire: effects copied from the game's '{source.name}' (material {source.sharedMaterial.name}); budget {ParticleBudget} particles in {Flames + Smokes + 2} systems.");
            return holder;
        }

        private ParticleSystem Make(GameObject holder, string name)
        {
            GameObject made = Object.Instantiate(holder.transform.GetChild(0).gameObject, _root);
            made.SetActive(false);
            ParticleSystem[] systems = made.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 1; i < systems.Length; i++)
            {
                ParticleSystem.EmissionModule extra = systems[i].emission;
                extra.enabled = false;   // one system per effect, whatever it was copied from
            }
            // The system itself is what is placed and switched on and off: it stands directly under the effects' root,
            // at a scale of one (every size above is in metres), whatever hierarchy it was copied in.
            ParticleSystem first = systems[0];
            Transform own = first.transform;
            if (own != made.transform)
            {
                own.SetParent(_root, false);
                Object.Destroy(made);
            }
            own.gameObject.SetActive(false);
            own.name = "RealLifeOverhaul " + name;
            own.localScale = Vector3.one;
            own.localRotation = Quaternion.identity;
            return first;
        }

        // Everything that moves, sizes or emits particles is set outright; noise, spin and the texture animation stay the game's.
        private static void Common(ParticleSystem particles, int max, float coneAngle, float coneRadius, bool up)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = false;
            main.startDelay = 0f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            main.startSize3D = false;
            main.maxParticles = max;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverDistance = 0f;
            emission.burstCount = 0;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.position = Vector3.zero;
            shape.rotation = up ? new Vector3(-90f, 0f, 0f) : Vector3.zero;   // the cone's axis is the system's forward
            shape.scale = Vector3.one;
            shape.alignToDirection = false;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;
            shape.randomPositionAmount = 0f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = false;
            ParticleSystem.InheritVelocityModule inherit = particles.inheritVelocity;
            inherit.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
            limit.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = particles.forceOverLifetime;
            force.enabled = false;
            ParticleSystem.ExternalForcesModule wind = particles.externalForces;
            wind.enabled = false;
            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = false;
            ParticleSystem.TriggerModule trigger = particles.trigger;
            trigger.enabled = false;
            ParticleSystem.SubEmittersModule children = particles.subEmitters;
            children.enabled = false;
            ParticleSystem.LightsModule lights = particles.lights;
            lights.enabled = false;
            ParticleSystem.TrailModule trails = particles.trails;
            trails.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void Life(ParticleSystem particles, float sizeTo, AnimationCurve sizeCurve, Gradient fade)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(sizeTo, sizeCurve);
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(fade);
        }

        private static void ShapeFlame(ParticleSystem particles)
        {
            Common(particles, FlameMax, 14f, 0.3f, true);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startColor = new Color(1.6f, 0.9f, 0.3f, 0.9f);   // tinted by the gradient from yellow at the root to red at the tip
            main.gravityModifier = -0.06f;
            Life(particles, 1f, Shrink, FlameFade);
        }

        private static void ShapeSmoke(ParticleSystem particles)
        {
            Common(particles, SmokeMax, 7f, 0.3f, true);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.4f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.gravityModifier = -0.03f;
            Life(particles, 3.2f, Grow, SmokeFade);
        }

        private static void ShapeSteam(ParticleSystem particles)
        {
            Common(particles, SteamMax, 16f, 0.5f, true);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.4f);
            main.gravityModifier = -0.02f;
            Life(particles, 2.4f, Grow, SmokeFade);
        }

        private static void ShapeJet(ParticleSystem particles)
        {
            Common(particles, JetMax, 2.5f, 0.03f, false);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 12f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startColor = new Color(0.82f, 0.90f, 1f, 0.6f);
            main.gravityModifier = 1f;   // it falls as water does: Jet aims it so that it lands on the fire
            Life(particles, 2.2f, Grow, JetFade);
        }

        private static Gradient Fade(Color from, Color to, float fullAt, float holdTo, float holdAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fullAt), new GradientAlphaKey(holdAlpha, holdTo), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }

    /// <summary>
    /// Fireworks over the skyline: bursts of small glowing shapes, each burst one colour, from
    /// four particle systems made once (plain meshes with the shared glowing paint; nothing of
    /// the game's is copied, and nothing is lit by them). BUDGET: 4 systems x 60 = 240 particles
    /// at most, a burst of 44 at a time, one pooled sound. Nothing here runs by itself: the
    /// director asks for a burst on a timer.
    /// </summary>
    internal sealed class Fireworks
    {
        public const int ParticleBudget = Colours * PerSystem;
        private const int Colours = 4;
        private const int PerSystem = 60;
        private const int PerBurst = 44;

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.25f, 0.2f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.85f, 0.35f), new Color(0.45f, 1f, 0.5f)
        };

        private readonly Transform _root;
        private readonly ParticleSystem[] _bursts = new ParticleSystem[Colours];
        private AudioSource _bang;
        private AudioClip _clip;
        private bool _built;
        private int _next;

        public Fireworks(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Fireworks").transform;
            _root.SetParent(parent, false);
        }

        public bool Ready()
        {
            if (_built)
            {
                return true;
            }
            if (!Shapes.Available)
            {
                return false;
            }
            for (int i = 0; i < Colours; i++)
            {
                var made = new GameObject("Burst " + i);
                made.transform.SetParent(_root, false);
                var particles = made.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = particles.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 17f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
                main.startRotation3D = true;
                main.gravityModifier = 0.35f;
                main.maxParticles = PerSystem;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = false;   // only ever by Emit
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;
                ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.dampen = 0.08f;
                limit.limit = 3f;
                ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
                var renderer = made.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Shapes.Cube;
                renderer.sharedMaterial = Shapes.Paint(Palette[i], 30f);
                renderer.enableGPUInstancing = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                made.SetActive(false);
                _bursts[i] = particles;
            }
            var speaker = new GameObject("Bang");
            speaker.transform.SetParent(_root, false);
            _bang = speaker.AddComponent<AudioSource>();
            _bang.playOnAwake = false;
            _bang.spatialBlend = 1f;
            _bang.rolloffMode = AudioRolloffMode.Linear;
            _bang.minDistance = 40f;
            _bang.maxDistance = 400f;
            _bang.volume = 0.35f;
            _clip = BuildBang();
            _built = true;
            return true;
        }

        /// <summary>One burst in the sky at a place, in the next colour.</summary>
        public void Burst(Vector3 at)
        {
            if (!_built)
            {
                return;
            }
            ParticleSystem particles = _bursts[_next];
            _next = (_next + 1) % Colours;
            if (!particles.gameObject.activeSelf)
            {
                particles.gameObject.SetActive(true);
            }
            particles.transform.position = at;
            particles.Emit(PerBurst);
            _bang.transform.position = at;
            _bang.pitch = Random.Range(0.8f, 1.15f);
            _bang.PlayOneShot(_clip);
        }

        public void StopAll()
        {
            if (!_built)
            {
                return;
            }
            for (int i = 0; i < Colours; i++)
            {
                if (_bursts[i].gameObject.activeSelf)
                {
                    _bursts[i].Clear(true);
                    _bursts[i].gameObject.SetActive(false);
                }
            }
        }

        public void Dispose()
        {
            if (_clip != null)
            {
                Object.Destroy(_clip);
                _clip = null;
            }
        }

        // A distant thump: noise that dies away over half a second, made once.
        private static AudioClip BuildBang()
        {
            const int rate = 22050;
            int samples = rate * 7 / 10;
            var data = new float[samples];
            float low = 0f;
            var random = new System.Random(7);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * 0.12f;   // the high end is gone by the time it reaches the street
                data[i] = low * Mathf.Exp(-t * 7f) * 0.9f;
            }
            AudioClip clip = AudioClip.Create("RealLifeOverhaul Firework", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
