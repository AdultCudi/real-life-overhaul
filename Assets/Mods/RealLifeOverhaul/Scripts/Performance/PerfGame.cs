using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Culling;
using GleyTrafficSystem;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>What the performance tools read of the game; private fields through Core/GameContract.</summary>
    internal static class PerfGame
    {
        private static readonly FieldInfo LampLights = GameContract.LampLights.Field;
        private static readonly FieldInfo LampData = GameContract.LampData.Field;
        private static readonly FieldInfo LampGlow = GameContract.LampGlow.Field;
        private static readonly FieldInfo LampIsOn = GameContract.LampIsOn.Field;

        // the game's list of what it culls by distance: every StreetLamp registers itself in its Start, off in OnDestroy
        private static readonly FieldInfo Cullables = GameContract.CullList.Field;

        /// <summary>False if this version of the game keeps a street lamp's lights, or its list of lamps, somewhere else.</summary>
        public static bool KnowsLamps => LampLights != null && LampData != null && LampGlow != null && LampIsOn != null && Cullables != null;

        /// <summary>The loaded city's active street lamps, from the game's culling list rather than a scene search; no allocation.</summary>
        public static void Lamps(List<StreetLamp> lamps)
        {
            lamps.Clear();
            CullingManager culling = InstanceBehavior<CullingManager>.Instance;
            if (culling == null || culling.generalCullingGroupController == null || !(Cullables.GetValue(culling.generalCullingGroupController) is List<ICullable> all))
            {
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is StreetLamp lamp && lamp != null && lamp.gameObject.activeInHierarchy)
                {
                    lamps.Add(lamp);
                }
            }
        }

        public static Light[] LightsOf(StreetLamp lamp) => LampLights.GetValue(lamp) as Light[];
        public static HDAdditionalLightData[] LightDataOf(StreetLamp lamp) => LampData.GetValue(lamp) as HDAdditionalLightData[];
        public static GameObject GlowOf(StreetLamp lamp) => LampGlow.GetValue(lamp) as GameObject;
        public static bool IsOn(StreetLamp lamp) => (bool)LampIsOn.GetValue(lamp);

        /// <summary>The pedestrians walking the streets right now, or null where there are none (menus, indoors before the city).</summary>
        public static List<Pedestrian> Pedestrians
        {
            get
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                PedestrianSpawner spawner = city != null ? city.pedestrianSpawner : null;
                return spawner != null ? spawner.activePedestrians : null;
            }
        }

        /// <summary>Traffic cars on the road right now (the pool's active ones); 0 while traffic is not simulating.</summary>
        public static int CarsDriving()
        {
            if (!TrafficTools.Street)
            {
                return 0;
            }
            int driving = 0;
            foreach (VehicleComponent car in Manager.GetVehicleList())
            {
                if (car != null && car.gameObject.activeSelf)
                {
                    driving++;
                }
            }
            return driving;
        }

        public static bool InVehicle()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            return game != null && game.selectedVehicle != null;
        }

        /// <summary>One line: where the player is and what the game is doing.</summary>
        public static string Where()
        {
            try
            {
                GameManager game = InstanceBehavior<GameManager>.Instance;
                PlayerController player = game != null ? game.playerController : null;
                string hour = TimeHelper.CurrentHour.ToString();
                return (BuildingManager.IsInsideBuilding ? "inside a building" : InVehicle() ? "in a vehicle" : "on foot in the street")
                       + ", neighbourhood '" + NeighborhoodHelper.CurrentNeighborhood + "', game hour " + hour
                       + ", outdoor lights " + (game != null && game.timeOfDayController != null && game.timeOfDayController.ShouldOutdoorLightsBeOn() ? "on" : "off")
                       + ", third-person camera " + (Settings.CameraEnabled ? "on" : "off")
                       + (player != null ? ", player at " + player.transform.position.ToString("F0") : string.Empty);
            }
            catch (Exception e)
            {
                return "unknown (" + e.Message + ")";
            }
        }

        /// <summary>The game's own populations, for the probe's report. Walks every object: report time only.</summary>
        public static void Describe(StringBuilder text)
        {
            if (TrafficTools.Street)
            {
                List<VehicleComponent> cars = Manager.GetVehicleList();
                int driving = 0, lit = 0;
                foreach (VehicleComponent car in cars)
                {
                    if (car != null && car.gameObject.activeSelf)
                    {
                        driving++;
                        Light lamp = car.GetComponentInChildren<Light>(false);
                        if (lamp != null && lamp.enabled)
                        {
                            lit++;
                        }
                    }
                }
                TrafficComponent traffic = InstanceBehavior<CityManager>.Instance != null ? InstanceBehavior<CityManager>.Instance.trafficComponent : null;
                text.AppendLine("  traffic: pool of " + cars.Count + " vehicles (the traffic system reads every pooled wheel each frame and each physics step, driving or not), driving now "
                                + driving + ", with a headlight on " + lit
                                + (traffic != null
                                    ? ", spawned beyond " + PerfText.F1(traffic.minDistanceToAdd) + " m, removed beyond " + PerfText.F1(traffic.distanceToRemove) + " m" : string.Empty));
            }
            else
            {
                text.AppendLine("  traffic: not simulating (inside a building, fast-forward, or not started)");
            }

            List<Pedestrian> walkers = Pedestrians;
            if (walkers != null)
            {
                int always = 0, offscreenSkin = 0, unseen = 0;
                foreach (Pedestrian walker in walkers)
                {
                    if (walker == null || walker.tpc == null)
                    {
                        continue;
                    }
                    ThirdPersonCharacter body = walker.tpc;
                    if (body.animator != null && body.animator.cullingMode == AnimatorCullingMode.AlwaysAnimate)
                    {
                        always++;
                    }
                    SkinnedMeshRenderer merged = body.appearanceSetter != null ? body.appearanceSetter.lastMergedMeshRenderer : null;
                    if ((merged != null && merged.updateWhenOffscreen) || (body.shadowCaster != null && body.shadowCaster.updateWhenOffscreen))
                    {
                        offscreenSkin++;
                    }
                    if (merged != null && !merged.isVisible && (body.shadowCaster == null || !body.shadowCaster.isVisible))
                    {
                        unseen++;
                    }
                }
                text.AppendLine("  pedestrians walking " + walkers.Count + "  not on screen " + unseen + "  animator set to AlwaysAnimate " + always + "  skinned even when off screen " + offscreenSkin
                                + "  (the game spawns them 50-90 m from the player in every direction and removes them at 100 m)");
            }

            AppearanceSetter[] characters = UnityEngine.Object.FindObjectsByType<AppearanceSetter>(FindObjectsSortMode.None);
            text.AppendLine("  characters with an appearance in the scene: " + characters.Length);

            if (KnowsLamps)
            {
                StreetLamp[] lamps = UnityEngine.Object.FindObjectsByType<StreetLamp>(FindObjectsSortMode.None);
                int on = 0, stray = 0, strayLights = 0, shadowed = 0;
                foreach (StreetLamp lamp in lamps)
                {
                    bool isOn = IsOn(lamp);
                    if (isOn)
                    {
                        on++;
                    }
                    Light[] lights = LightsOf(lamp);
                    if (lights == null)
                    {
                        continue;
                    }
                    int enabled = 0;
                    foreach (Light light in lights)
                    {
                        if (light != null && light.enabled)
                        {
                            enabled++;
                            if (isOn && light.shadows != LightShadows.None)
                            {
                                shadowed++;
                            }
                        }
                    }
                    if (!isOn && enabled > 0)
                    {
                        stray++;
                        strayLights += enabled;
                    }
                }
                text.AppendLine("  street lamps " + lamps.Length + "  switched on by the game (within 200 m of the player, at night) " + on
                                + "  of their lights casting shadows (within 60 m) " + shadowed);
                text.AppendLine("  street lamps the game has OFF whose Light components are nevertheless enabled: " + stray + " lamps, " + strayLights + " lights"
                                + (stray > 0 ? "  <- lamps never switched on since loading are never switched off either" : string.Empty));
            }
            else
            {
                text.AppendLine("  street lamps: this version of the game keeps their lights elsewhere; not counted");
            }

            text.AppendLine("  trees " + TreeController.AllTrees.Count + " (the game forces every tree to its most detailed model at any distance)  culling manager present "
                            + (InstanceBehavior<CullingManager>.Instance != null));
        }
    }
}
