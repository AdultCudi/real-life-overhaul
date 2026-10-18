using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>A siren's sound.</summary>
    public enum SirenTone
    {
        Wail,   // slow rise and fall: ambulance
        Yelp    // fast two-tone: police
    }

    /// <summary>
    /// Builds the things an accident or a response vehicle wears: flashing lights, sirens, the
    /// police body, fire, smoke and debris. Everything is created on an event and
    /// returned to the caller, who owns it and must destroy it (traffic cars are pooled).
    /// Templates and generated sounds are made once and kept until the city unloads.
    /// </summary>
    public static partial class TrafficEffects
    {
        private static readonly List<MeshRenderer> MeshScratch = new List<MeshRenderer>();
        private static readonly List<Component> ComponentScratch = new List<Component>();

        public static IModLogger Logger;

        private static Transform _holder;
        private static GameObject _smokeTemplate;
        private static bool _smokeGenuine;   // the template is the game's engine smoke, not a stand-in

        private static GameObject _policeTemplate;
        private static bool _policeSearched;
        private static AudioClip _wailClip;
        private static AudioClip _yelpClip;

        /// <summary>Called when the city unloads: drops what was generated and forgets what was borrowed.</summary>
        public static void Dispose()
        {
            if (_wailClip != null)
            {
                Object.Destroy(_wailClip);
            }
            if (_yelpClip != null)
            {
                Object.Destroy(_yelpClip);
            }
            if (_holder != null)
            {
                Object.Destroy(_holder.gameObject);   // takes the templates with it
            }
            _wailClip = null;
            _yelpClip = null;
            _holder = null;
            _smokeTemplate = null;
            _smokeGenuine = false;
            _smokeTries = 0;
            _policeTemplate = null;
            _policeSearched = false;
        }

        private const int SmokeTries = 3;   // searches for the game's own engine smoke per city, each made where it is not seen
        private static int _smokeTries;

        /// <summary>Looks for the game's engine smoke out of sight, three times a city at most. True once there is nothing more to try.</summary>
        public static bool Prewarm()
        {
            if ((!Settings.TrafficAccidents && !Settings.CityFires) || _smokeGenuine || _smokeTries >= SmokeTries)
            {
                return true;
            }
            _smokeTries++;
            FindSmoke();
            return _smokeGenuine || _smokeTries >= SmokeTries;
        }

        /// <summary>Behind the loading screen (ScenePrewarm): both sirens' sounds, some 100,000 samples each, not made as the first unit is sent.</summary>
        public static bool PrewarmSirens()
        {
            if (Settings.Siren && (Settings.TrafficAccidents || Settings.PolicePursuit || Settings.CityFires))
            {
                GetSirenClip(SirenTone.Wail);
                GetSirenClip(SirenTone.Yelp);
            }
            return true;
        }

        // Templates live under an inactive object, so no game script on a copy ever wakes up.
        private static Transform Holder
        {
            get
            {
                if (_holder == null)
                {
                    var go = new GameObject("RealLifeOverhaul Templates");
                    go.SetActive(false);
                    _holder = go.transform;
                }
                return _holder;
            }
        }

        private static bool IsUpright(VehicleComponent car)
        {
            return car.transform.up.y > 0.7f;
        }

        // Effects sit at a point on the car; on a car that has rolled over they go just above it instead.
        private static void Seat(Transform effect, VehicleComponent car, Vector3 local)
        {
            if (IsUpright(car))
            {
                effect.localPosition = local;
            }
            else
            {
                effect.SetPositionAndRotation(car.transform.position + Vector3.up * Mathf.Min(local.y, 1.1f), Quaternion.identity);
            }
        }

        /// <summary>Two alternating point lights above a car: hazard lights, or an emergency beacon.</summary>
        public static GameObject CreateFlasher(VehicleComponent car, string objectName, float height, Color first, Color second, float interval)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(car.transform, false);
            Seat(root.transform, car, new Vector3(0f, height, 0f));

            var flasher = root.AddComponent<Flasher>();
            flasher.Interval = interval;
            flasher.A = CreateLight(root.transform, new Vector3(-0.5f, 0f, 0f), first, 9000f, 28f);
            flasher.B = CreateLight(root.transform, new Vector3(0.5f, 0f, 0f), second, 9000f, 28f);
            return root;
        }

        /// <summary>The same two lights on an object of the mod's own (the tow truck's light bar), on the same clock as the other.</summary>
        public static GameObject CreateFlasher(Transform parent, string objectName, Vector3 localPosition, Color first, Color second, float interval, float lumens, float range)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            var flasher = root.AddComponent<Flasher>();
            flasher.Interval = interval;
            flasher.A = CreateLight(root.transform, new Vector3(-0.4f, 0f, 0f), first, lumens, range);
            flasher.B = CreateLight(root.transform, new Vector3(0.4f, 0f, 0f), second, lumens, range);
            return root;
        }

        /// <summary>The height the existing beacons and hazard lights have always used.</summary>
        public static float BeaconHeight(VehicleComponent car)
        {
            return Mathf.Max(1.8f, car.coliderHeight + 0.3f);
        }

        /// <summary>A quiet, low, fully positional siren on the given object. Starts playing.</summary>
        public static AudioSource AddSiren(GameObject root, VehicleComponent car, SirenTone tone)
        {
            var audio = root.AddComponent<AudioSource>();
            audio.clip = GetSirenClip(tone);
            audio.loop = true;
            audio.volume = 0.16f;
            audio.dopplerLevel = 0.5f;

            // Fully positional, on the mixer channel of the car's own sounds, so the game's effects volume applies.
            audio.spatialBlend = 1f;
            audio.spread = 0f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 4f;
            audio.maxDistance = 95f;
            var engine = car.GetComponentInChildren<AudioSource>();
            if (engine != null && engine != audio)
            {
                audio.outputAudioMixerGroup = engine.outputAudioMixerGroup;
            }
            audio.Play();
            return audio;
        }

        private static Light CreateLight(Transform parent, Vector3 localPosition, Color color, float lumens, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.shadows = LightShadows.None;
            var hd = go.AddComponent<HDAdditionalLightData>();
            hd.SetIntensity(lumens, LightUnit.Lumen);
            hd.range = range;
            return light;
        }

        // Sirens are generated in code and pitched low, like a real one heard from the street.
        private static AudioClip GetSirenClip(SirenTone tone)
        {
            if (tone == SirenTone.Yelp)
            {
                if (_yelpClip == null)
                {
                    // Four quick cycles, so the loop is long enough not to sound mechanical.
                    _yelpClip = BuildSiren("RealLifeOverhaul Siren Yelp", tone, 1.44f, 4);
                }
                return _yelpClip;
            }
            if (_wailClip == null)
            {
                _wailClip = BuildSiren("RealLifeOverhaul Siren", tone, 3.2f, 1);
            }
            return _wailClip;
        }

        private static readonly int HotBuildSiren = HotSpots.Slot("Traffic: TrafficEffects.BuildSiren");
        private static AudioClip BuildSiren(string clipName, SirenTone tone, float seconds, int cycles)
        {
            using var hotSpot = HotSpots.Time(HotBuildSiren);
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];

            // Nudge the pitch (well under 1%) so the wave ends where it began and the loop doesn't click.
            double total = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                total += 2.0 * Math.PI * SirenFrequency(tone, i / (float)samples.Length * cycles) / rate;
            }
            double whole = Math.Round(total / (2.0 * Math.PI)) * 2.0 * Math.PI;
            double scale = total > 0.0 ? whole / total : 1.0;

            double phase = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                phase += 2.0 * Math.PI * SirenFrequency(tone, i / (float)samples.Length * cycles) * scale / rate;
                samples[i] = (float)(Math.Sin(phase) * 0.55 + Math.Sin(phase * 2.0) * 0.12 + Math.Sin(phase * 3.0) * 0.05);
            }
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Frequency in Hz at a point in the clip, where each whole number is one cycle.
        private static float SirenFrequency(SirenTone tone, float cyclePosition)
        {
            float t = cyclePosition - Mathf.Floor(cyclePosition);
            if (tone == SirenTone.Yelp)
            {
                // Sharp climb, quicker drop.
                float sweep = t < 0.62f ? t / 0.62f : (1f - t) / 0.38f;
                return Mathf.Lerp(540f, 980f, Mathf.SmoothStep(0f, 1f, sweep));
            }
            float wail = t < 0.5f ? t * 2f : (1f - t) * 2f;
            return Mathf.Lerp(430f, 780f, Mathf.SmoothStep(0f, 1f, wail));
        }

        /// <summary>On a police body: as it is destroyed, or its car returns to the pool, the car gets its own looks back. No frame cost.</summary>
        private sealed class PoliceBody : MonoBehaviour
        {
            public readonly List<Renderer> Hidden = new List<Renderer>();

            private void OnDisable()
            {
                for (int i = 0; i < Hidden.Count; i++)
                {
                    if (Hidden[i] != null)
                    {
                        Hidden[i].forceRenderingOff = false;
                    }
                }
                Hidden.Clear();
                Destroy(gameObject);
            }
        }

        /// <summary>Two lights flashing in turn on the unscaled clock. Cost: a comparison a frame; the lights change only as they flip.</summary>
        private sealed class Flasher : MonoBehaviour
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "TrafficEffects.Flasher", typeof(TrafficEffects.Flasher));

            public Light A;
            public Light B;
            public float Interval;

            private bool _on;

            private static readonly int HotUpdate = HotSpots.Slot("Traffic: Flasher.Update");
            private void Update()
            {
                using var hotSpot = HotSpots.Time(HotUpdate);
                if (FrameGuard.Off)
                {
                    enabled = false;
                    return;
                }
                try
                {
                    OnUpdate();
                }
                catch (Exception e)
                {
                    Guard.Fail(FrameGuard, e, this);
                }
            }

            private void OnUpdate()
            {
                if (Interval <= 0f)
                {
                    return;
                }
                bool on = Mathf.FloorToInt(Time.unscaledTime / Interval) % 2 == 0;
                if (on == _on)
                {
                    return;
                }
                _on = on;
                if (A != null)
                {
                    A.enabled = on;
                }
                if (B != null)
                {
                    B.enabled = !on;
                }
            }
        }

        /// <summary>A fire's light flickering. Cost: a time comparison a frame, a new brightness every 0.05 to 0.14 s.</summary>
        private sealed class Flicker : MonoBehaviour
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "TrafficEffects.Flicker", typeof(TrafficEffects.Flicker));

            public HDAdditionalLightData Light;

            private float _next;

            private static readonly int HotUpdate = HotSpots.Slot("Traffic: Flicker.Update");
            private void Update()
            {
                using var hotSpot = HotSpots.Time(HotUpdate);
                if (FrameGuard.Off)
                {
                    enabled = false;
                    return;
                }
                try
                {
                    OnUpdate();
                }
                catch (Exception e)
                {
                    Guard.Fail(FrameGuard, e, this);
                }
            }

            private void OnUpdate()
            {
                if (Light == null || Time.time < _next)
                {
                    return;
                }
                _next = Time.time + Random.Range(0.05f, 0.14f);
                Light.SetIntensity(Random.Range(8000f, 17000f), LightUnit.Lumen);
            }
        }
    }
}
