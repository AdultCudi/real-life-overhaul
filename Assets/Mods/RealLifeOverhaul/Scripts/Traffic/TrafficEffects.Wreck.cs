using System;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What an accident or a unit wears: a wreck's fire, smoke and debris.</summary>
    public static partial class TrafficEffects
    {
        private const string EngineSmokeName = "SmokeBrokenCar";

        // Built-in materials may not render in this HDRP game: shapes take a material from the car itself.
        private static Material BorrowMaterial(VehicleComponent car)
        {
            var paint = car.GetComponentInChildren<MeshRenderer>();
            return paint != null ? paint.sharedMaterial : null;
        }

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

        private static readonly int HotCreateSmoke = HotSpots.Slot("Traffic: TrafficEffects.CreateSmoke");
        /// <summary>A column of smoke rising from a car's engine bay however the car lies (heavy: dark). Null if the game has no smoke loaded.</summary>
        public static GameObject CreateSmoke(VehicleComponent car, bool heavy)
        {
            using var hotSpot = HotSpots.Time(HotCreateSmoke);
            GameObject template = GetSmokeTemplate();
            if (template == null)
            {
                return null;
            }

            Transform body = car.transform;
            GameObject smoke = Object.Instantiate(template, body);
            smoke.name = "RealLifeOverhaul Smoke";

            // Over the engine, wherever the nose is now, never below the road; the effect is upright, so the column rises.
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

            // Whichever of the game's effects it was copied from, the material and texture are the game's and the motion set here.
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

        // One particle system made into a column of smoke: every module that moves, sizes or emits is set outright; noise,
        // spin and the texture animation stay the game's. Simulated in the world and scaled by its hierarchy, which
        // CreateSmoke made one, so every size here is in metres.
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

        // The game's own smoke effect copied whole, so it renders as the game's does; found once, kept as a disabled template.
        private static GameObject GetSmokeTemplate()
        {
            // The engine smoke is in memory only while a player car is; until then a stand-in is used. The search goes through
            // everything in memory (a long frame), so it is made only behind a screen: in play an accident takes what there is.
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
                // The broken-engine smoke is the one made for this; any other smoke stands in until it is in memory.
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
                         + FormattableString.Invariant(
                             $"material {source.sharedMaterial.name}, mode {source.renderMode}, size {main.startSize.constantMax:F2}, life {main.startLifetime.constantMax:F2}, ")
                         + $"space {main.simulationSpace}, scale {source.transform.lossyScale}");
            return _smokeTemplate;
        }

        private static readonly int HotCreateDebris = HotSpots.Slot("Traffic: TrafficEffects.CreateDebris");
        /// <summary>Broken bodywork: flat shards in the car's own paint scattered round a point, no colliders or shadows. Null if no paint.</summary>
        public static GameObject CreateDebris(VehicleComponent car, Vector3 centre, int pieces)
        {
            using var hotSpot = HotSpots.Time(HotCreateDebris);
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
    }
}
