using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
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
    public static class TrafficEffects
    {
        private const string PoliceCarPrefab = "Vehicles/ParkedVehicles/PoliceCar";   // the game's parked police car prop

        private static readonly List<MeshRenderer> MeshScratch = new List<MeshRenderer>();
        private static readonly List<Component> ComponentScratch = new List<Component>();

        public static IModLogger Logger;

        // Borrowed or generated once and reused.
        private static Transform _holder;
        private static GameObject _smokeTemplate;
        private static bool _smokeSearched;
        private static bool _smokeGenuine;   // the template is the game's engine smoke, not a stand-in

        private const string EngineSmokeName = "SmokeBrokenCar";
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
            _smokeSearched = false;
            _smokeGenuine = false;
            _smokeTries = 0;
            _policeTemplate = null;
            _policeSearched = false;
        }

        private const int SmokeTries = 3;   // searches for the game's own engine smoke per city, each made where it is not seen
        private static int _smokeTries;

        /// <summary>
        /// The smoke is looked for where the search is not seen: behind the loading screen, and (while only a stand-in
        /// was to be had) behind the game's black screen at a door, three times at most. True once there is nothing
        /// more to try.
        /// </summary>
        public static bool Prewarm()
        {
            if ((!Settings.TrafficAccidents && !Settings.CityFires) || _smokeGenuine || _smokeTries >= SmokeTries)
            {
                return true;
            }
            _smokeTries++;
            GetSmokeTemplate();
            if (_smokeTemplate == null)
            {
                _smokeSearched = false;   // no smoke of any kind in memory yet: the first accident looks once more, as it always did
            }
            return _smokeGenuine || _smokeTries >= SmokeTries;
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

        // ---------------------------------------------------------------- lights and sirens

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

            // Fully positional, with the same mixer channel as the car's own sounds so the
            // game's sound-effects volume applies to it.
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

        private static AudioClip BuildSiren(string clipName, SirenTone tone, float seconds, int cycles)
        {
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

        // ---------------------------------------------------------------- police look

        /// <summary>
        /// Dresses a traffic car as a police car using the game's parked police car prop: a
        /// copy of the prop (visuals only) is fitted over the car and the car's own renderers
        /// are hidden. Returns the copy, or null if the prop is unavailable. The copy undoes
        /// itself: destroying it shows the car again, and it destroys itself when the car goes
        /// back to the pool, so a patrol car can drive off still looking like one.
        /// </summary>
        public static GameObject CreatePoliceBody(VehicleComponent car)
        {
            GameObject template = GetPoliceTemplate();
            if (template == null || !TryGetBounds(car.gameObject, out Bounds host))
            {
                return null;
            }

            // Collected before the copy exists, so only the car's own renderers are hidden.
            car.GetComponentsInChildren(true, ComponentScratch);

            GameObject body = Object.Instantiate(template, car.transform);
            body.name = "RealLifeOverhaul Police Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.SetActive(true);
            List<Renderer> hidden = body.AddComponent<PoliceBody>().Hidden;

            // The prop must be car-sized, or something about it is not what we expect.
            if (!TryGetBounds(body, out Bounds own) || Mathf.Max(own.size.x, own.size.z) < 2.5f || Mathf.Max(own.size.x, own.size.z) > 9f)
            {
                Object.Destroy(body);
                ComponentScratch.Clear();
                return null;
            }

            // Line the prop up with the car: centred on it, wheels on the same ground.
            var shift = new Vector3(host.center.x - own.center.x, host.min.y - own.min.y, host.center.z - own.center.z);
            if (shift.sqrMagnitude < 4f)
            {
                body.transform.position += shift;
            }

            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                if (ComponentScratch[i] is Renderer renderer && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    hidden.Add(renderer);
                }
            }
            ComponentScratch.Clear();
            if (hidden.Count == 0)
            {
                Object.Destroy(body);
                return null;
            }
            return body;
        }

        /// <summary>
        /// A copy of the game's parked police car, meshes only, standing on its parent's origin and
        /// facing the way the parent faces: a patrol car parked at the kerb (People/PolicePresence).
        /// The parent must be unturned when this is called. Null if the game has no such prop.
        /// </summary>
        /// <param name="size">Its width, height and length in metres.</param>
        public static GameObject CreateParkedPatrol(Transform parent, out Vector3 size)
        {
            size = default;
            GameObject template = GetPoliceTemplate();
            if (template == null || parent == null)
            {
                return null;
            }
            GameObject body = Object.Instantiate(template, parent);
            body.name = "RealLifeOverhaul Parked Patrol Car";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.SetActive(true);
            if (!TryGetBounds(body, out Bounds own) || Mathf.Max(own.size.x, own.size.z) < 2.5f || Mathf.Max(own.size.x, own.size.z) > 9f)
            {
                Object.Destroy(body);
                return null;
            }
            Vector3 origin = parent.position;
            body.transform.position += new Vector3(origin.x - own.center.x, origin.y - own.min.y, origin.z - own.center.z);
            size = own.size;
            return body;
        }

        // The game's police car is a parked prop, not a traffic car. Loaded once; the copy kept
        // here has everything but its meshes taken off.
        private static GameObject GetPoliceTemplate()
        {
            if (_policeSearched)
            {
                return _policeTemplate;
            }
            _policeSearched = true;
            try
            {
                GameObject prefab = PrefabHelper.LoadPrefabAssetByName(PoliceCarPrefab);
                if (prefab == null)
                {
                    Logger?.Info("Police: the game's police car prop was not found; no patrol cars can be sent.");
                    return null;
                }
                _policeTemplate = Object.Instantiate(prefab, Holder);
                _policeTemplate.name = "RealLifeOverhaul Police Template";
                StripToVisuals(_policeTemplate);
                Logger?.Info("Police: using the game's police car prop for patrol cars.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                if (_policeTemplate != null)
                {
                    Object.Destroy(_policeTemplate);
                }
                _policeTemplate = null;
            }
            return _policeTemplate;
        }

        // Removes colliders, rigidbodies, scripts and anything else that is not part of drawing the model.
        private static void StripToVisuals(GameObject root)
        {
            // Scripts first, since they may require the components removed after them.
            root.GetComponentsInChildren(true, ComponentScratch);
            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                if (ComponentScratch[i] is MonoBehaviour script)
                {
                    Object.DestroyImmediate(script);
                }
            }
            // Twice, in case one component could not go before another that depended on it.
            for (int pass = 0; pass < 2; pass++)
            {
                root.GetComponentsInChildren(true, ComponentScratch);
                for (int i = 0; i < ComponentScratch.Count; i++)
                {
                    Component component = ComponentScratch[i];
                    if (component != null && !(component is Transform) && !(component is MeshFilter)
                        && !(component is Renderer) && !(component is LODGroup))
                    {
                        Object.DestroyImmediate(component);
                    }
                }
            }
            ComponentScratch.Clear();
        }

        /// <summary>Height of the car's roof above its origin, measured from its meshes while it stands upright.</summary>
        public static float RoofHeight(VehicleComponent car)
        {
            float height = TryGetBounds(car.gameObject, out Bounds bounds) ? bounds.max.y - car.transform.position.y : 0f;
            return height > 1f && height < 3.5f ? height : Mathf.Max(1.4f, car.coliderHeight);
        }

        // World bounds of the visible meshes under an object.
        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            root.GetComponentsInChildren(false, MeshScratch);
            for (int i = 0; i < MeshScratch.Count; i++)
            {
                MeshRenderer renderer = MeshScratch[i];
                if (!renderer.enabled || renderer.forceRenderingOff)
                {
                    continue;
                }
                if (any)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    any = true;
                }
            }
            MeshScratch.Clear();
            return any;
        }

        // Built-in default materials may not render in this HDRP game, so shapes made here use a
        // material from the car itself, tinted per renderer without creating a new material.
        private static Material BorrowMaterial(VehicleComponent car)
        {
            var paint = car.GetComponentInChildren<MeshRenderer>();
            return paint != null ? paint.sharedMaterial : null;
        }

        // ---------------------------------------------------------------- wreck dressing

        /// <summary>A flickering orange light over the engine bay.</summary>
        public static GameObject CreateFire(VehicleComponent car)
        {
            var fire = new GameObject("RealLifeOverhaul Fire");
            fire.transform.SetParent(car.transform, false);
            Seat(fire.transform, car, new Vector3(0f, 1.1f, car.length * 0.3f));
            Light light = CreateLight(fire.transform, Vector3.zero, new Color(1f, 0.45f, 0.1f), 14000f, 16f);
            var flicker = fire.AddComponent<Flicker>();
            flicker.Light = light.GetComponent<HDAdditionalLightData>();
            return fire;
        }

        /// <summary>
        /// A column of smoke rising straight up from a car's engine bay, wherever the car has ended up
        /// (on its wheels, its side or its roof); null if the game has no smoke effect loaded. Heavy is
        /// a dark column that shows from across the street, light a thinner grey one.
        /// </summary>
        public static GameObject CreateSmoke(VehicleComponent car, bool heavy)
        {
            GameObject template = GetSmokeTemplate();
            if (template == null)
            {
                return null;
            }

            Transform body = car.transform;
            GameObject smoke = Object.Instantiate(template, body);
            smoke.name = "RealLifeOverhaul Smoke";

            // Over the engine, in the world: on a car that is not on its wheels that is wherever its nose now is,
            // never below the road. The effect itself is always upright, so the column rises whatever the car does.
            bool upright = IsUpright(car);
            Vector3 bay = body.TransformPoint(new Vector3(0f, upright ? 1f : 0.5f, car.length * 0.33f));
            if (!upright && StreetSpots.Ground(bay, out float ground))
            {
                bay.y = Mathf.Max(bay.y, ground + 0.6f);
            }
            Transform place = smoke.transform;
            place.SetPositionAndRotation(bay, Quaternion.identity);
            // Its sizes are set below in metres: whatever scale the car carries is taken out again.
            Vector3 scale = body.lossyScale;
            place.localScale = new Vector3(Inverse(scale.x), Inverse(scale.y), Inverse(scale.z));
            smoke.SetActive(true);

            // Whichever of the game's effects it was copied from (the engine smoke is a metre-high puff, tyre smoke
            // a scatter of short ones), the material and texture are the game's and the motion is set here.
            ParticleSystem[] systems = smoke.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].gameObject.SetActive(true);
                if (systems[i].transform != place)
                {
                    systems[i].transform.localRotation = Quaternion.identity;
                }
                if (i == 0)
                {
                    ShapeAsColumn(systems[i], heavy);
                }
                else
                {
                    // One column, not one per part of the effect it was copied from: the rest emit nothing.
                    ParticleSystem.EmissionModule extra = systems[i].emission;
                    extra.enabled = false;
                }
            }
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(false);
                systems[i].Play(false);
            }
            return smoke;
        }

        private static float Inverse(float scale)
        {
            return Mathf.Abs(scale) > 0.001f ? 1f / scale : 1f;
        }

        // One particle system made into smoke rising from a point. Every module that moves, sizes or
        // emits particles is set outright, so nothing of the effect it was copied from is left to chance;
        // noise, spin and the texture animation are kept as the game has them. The systems are simulated in
        // the world (the smoke already in the air stays where it is) and scaled by their own hierarchy,
        // which CreateSmoke has brought to one: every size here is in metres.
        private static void ShapeAsColumn(ParticleSystem particles, bool heavy)
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
            main.startLifetime = heavy ? new ParticleSystem.MinMaxCurve(3.4f, 4.6f) : new ParticleSystem.MinMaxCurve(2.4f, 3.2f);
            main.startSpeed = heavy ? new ParticleSystem.MinMaxCurve(1.5f, 2.3f) : new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
            main.startSize = heavy ? new ParticleSystem.MinMaxCurve(0.9f, 1.5f) : new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startColor = heavy ? new Color(0.05f, 0.05f, 0.05f, 0.95f) : new Color(0.55f, 0.55f, 0.55f, 0.6f);
            main.gravityModifier = -0.03f;   // the world's gravity, turned round: it keeps rising as it thins
            main.maxParticles = heavy ? 96 : 48;   // more than the rate and the lifetime below can ever have alive

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = heavy ? 16f : 9f;
            emission.rateOverDistance = 0f;
            emission.burstCount = 0;

            // A narrow cone, opening straight up: the cone's own axis is the system's forward.
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = heavy ? 7f : 10f;
            shape.radius = heavy ? 0.3f : 0.2f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.position = Vector3.zero;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = Vector3.one;
            shape.alignToDirection = false;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;
            shape.randomPositionAmount = 0f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(heavy ? 3.2f : 2.6f, GrowCurve);

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(FadeGradient);

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

        // A puff grows to its full size over its life, from a third of it (the curve runs 0 to 1; the size is its multiplier)...
        private static readonly AnimationCurve GrowCurve = AnimationCurve.Linear(0f, 0.32f, 1f, 1f);

        // ...comes in quickly, and thins out to nothing at the top of the column.
        private static readonly Gradient FadeGradient = BuildFade();

        private static Gradient BuildFade()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        // The game's own smoke effect, copied whole (shape, texture animation, material) so it renders
        // exactly as the game's smoke does. Found once, kept as a disabled template.
        private static GameObject GetSmokeTemplate()
        {
            // The engine smoke is only in memory while a player car is; until it turns up, a stand-in is used. The
            // search goes through everything the engine has in memory (a long frame), so it is made behind the
            // loading screen (Prewarm) and never again while the game is being played: an accident takes what there is.
            if (_smokeSearched && (_smokeGenuine || _smokeTemplate == null || !SceneFinds.Hidden))
            {
                return _smokeTemplate;
            }
            _smokeSearched = true;

            ParticleSystemRenderer source = null;
            bool genuine = false;
            foreach (ParticleSystemRenderer renderer in SceneFinds.InMemory<ParticleSystemRenderer>("traffic: the game's smoke effect being looked for (everything in memory gone through)"))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) < 0
                    || renderer.GetComponent<ParticleSystem>() == null || renderer.transform.IsChildOf(Holder))
                {
                    continue;
                }
                // The game's broken-engine smoke is the one whose material and texture were made for this;
                // any other smoke stands in until it is in memory. Either is given its motion by CreateSmoke.
                if (renderer.name.IndexOf(EngineSmokeName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    source = renderer;
                    genuine = true;
                    break;
                }
                if (source == null)
                {
                    source = renderer;
                }
            }
            if (source == null)
            {
                Logger?.Info("Accident: the game has no smoke effect loaded; accidents will have no smoke.");
                return null;
            }
            if (_smokeTemplate != null)
            {
                if (!genuine)
                {
                    return _smokeTemplate;   // still only the stand-in
                }
                Object.Destroy(_smokeTemplate);
            }
            _smokeGenuine = genuine;

            _smokeTemplate = Object.Instantiate(source.gameObject, Holder);
            _smokeTemplate.name = "RealLifeOverhaul Smoke Template";
            _smokeTemplate.SetActive(false);
            // Any game scripts on the copy would expect to be on a car; only the particles are wanted.
            foreach (MonoBehaviour script in _smokeTemplate.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.Destroy(script);
            }

            ParticleSystem.MainModule main = source.GetComponent<ParticleSystem>().main;
            Logger?.Info($"Accident: smoke copied from '{source.name}' (parent '{(source.transform.parent != null ? source.transform.parent.name : "none")}'), "
                         + $"material {source.sharedMaterial.name}, mode {source.renderMode}, size {main.startSize.constantMax:F2}, life {main.startLifetime.constantMax:F2}, "
                         + $"space {main.simulationSpace}, scale {source.transform.lossyScale}");
            return _smokeTemplate;
        }

        /// <summary>
        /// Broken bodywork: flat shards in the car's own paint, scattered on the road around a
        /// point. No colliders, no shadows. Null if the car has no material to borrow.
        /// </summary>
        public static GameObject CreateDebris(VehicleComponent car, Vector3 centre, int pieces)
        {
            Material paint = BorrowMaterial(car);
            if (paint == null)
            {
                return null;
            }

            var root = new GameObject("RealLifeOverhaul Debris");
            root.transform.position = centre;
            for (int i = 0; i < pieces; i++)
            {
                var piece = new GameObject("Shard");
                piece.transform.SetParent(root.transform, false);
                Vector2 scatter = Random.insideUnitCircle * Random.Range(1.6f, 4.2f);
                piece.transform.position = new Vector3(centre.x + scatter.x, centre.y + 0.04f, centre.z + scatter.y);
                piece.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                piece.transform.localScale = new Vector3(Random.Range(0.12f, 0.55f), Random.Range(0.02f, 0.06f), Random.Range(0.08f, 0.35f));
                piece.AddComponent<MeshFilter>().sharedMesh = Shapes.Cube;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = paint;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        // ---------------------------------------------------------------- components

        // Sits on a police body. Runs only when the body is destroyed or the car under it is
        // switched off (returned to the traffic pool): either way the car gets its own looks back.
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

        private sealed class Flasher : MonoBehaviour
        {
            public Light A;
            public Light B;
            public float Interval;

            private bool _on;

            // Only touches the lights on the frames where the state flips.
            private void Update()
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

        private sealed class Flicker : MonoBehaviour
        {
            public HDAdditionalLightData Light;

            private float _next;

            private void Update()
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
