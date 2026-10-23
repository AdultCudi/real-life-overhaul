using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// Fireworks over the skyline: bursts of small glowing cubes, one colour each, from four particle systems made once
    /// with the shared glowing paint, lighting nothing. Budget: 4 x 60 = 240 particles, 44 a burst, one pooled sound.
    /// Nothing runs by itself: the scene asks for each burst.
    /// </summary>
    internal sealed class Fireworks
    {
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
