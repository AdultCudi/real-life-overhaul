using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The test tools of the hair finish (Options > Tests), so that hair under a strong light can be judged in a
    /// second instead of waiting for a car's lamp at night:
    ///
    ///  - "say what my character's hair is made of": the line <see cref="HairLook.Describe(AppearanceSetter)"/> for the
    ///    player's detailed character, written to the log whether or not diagnostics are on, and a short notice;
    ///  - "a strong lamp beyond my head for 20 seconds": one warm point light of the mod's own, 3 m beyond the head
    ///    as the camera sees it at a car lamp's height, removed by the engine's own timer;
    ///  - "a street lamp over my head for 20 seconds": a copy of the nearest street lamp's light, all but over the head;
    ///  - "switch the hair finish off and on": for this session only; the option itself is not touched.
    ///
    /// Per frame: nothing. Each entry does its work once when pressed; the lamp is one light for 20 seconds.
    /// </summary>
    internal static class HairTests
    {
        private const string Test = "Hair";
        private const string LampName = "RealLifeOverhaul Hair Test Lamp";
        private const float LampSeconds = 20f;
        private const float LampDistance = 3f;      // metres beyond the head, as in the report this was made for
        private const float LampHeight = 0.8f;      // metres above the ground: a car's headlamp
        private const float HeadHeight = 1.6f;      // metres above the player's feet
        private const float LampLumens = 40000f;    // about 3 200 candela: 350 lux at the head, a headlamp at that distance
        private const float LampRange = 12f;
        private const float NoticeSeconds = 9f;

        private static readonly Color LampColour = new Color(1f, 0.82f, 0.63f);   // warm: a halogen headlamp beside an amber indicator
        private static readonly Color StreetLampColour = new Color(1f, 0.86f, 0.7f);
        private const float StreetLampHeight = 5.5f;     // metres above the feet, when no lamp of the game's is near to copy
        private const float StreetLampLumens = 40000f;
        private const float StreetLampOffset = 1f;       // metres beyond the head: the lamp all but over it, as at the stop of the report
        private static readonly System.Reflection.FieldInfo LampMaxValue = GameContract.LampMaxValue.Field;   // (the game's private piece, resolved once: Core/GameContract)

        private static GameObject _lamp;

        /// <summary>Set by "switch the hair finish off and on": the finish stays off until pressed again or the scene ends.</summary>
        public static bool FinishOff { get; private set; }

        /// <summary>"Test: say what my character's hair is made of".</summary>
        public static void Say()
        {
            try
            {
                AppearanceSetter character = Player(out TestRefusal why);
                if (character == null)
                {
                    Refuse(why);
                    return;
                }
                HairLook.Logger?.Warn(HairLook.Describe(character));   // asked for by a button: written whether or not diagnostics are on
                int finished = HairLook.FinishedLayers(character, out int layers, out string style);
                var data = new Dictionary<string, string>(3)
                {
                    { "style", style ?? "-" }, { "finished", finished.ToString() }, { "layers", layers.ToString() }
                };
                Notifications.Show(NotificationType.Info, layers == 0 ? "reallife_hair_test_none" : "reallife_hair_test_said", data, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                HairLook.Logger?.Error(e);
            }
        }

        /// <summary>"Test: a strong lamp beyond my head for 20 seconds".</summary>
        public static void Lamp()
        {
            try
            {
                AppearanceSetter character = Player(out TestRefusal why);
                PlayerController player = PlayerHelper.PlayerController;
                if (character == null || player == null)
                {
                    Refuse(character == null ? why : TestRefusal.NotLoaded);
                    return;
                }
                RemoveLamp();
                Vector3 feet = player.transform.position;
                Vector3 head = feet + Vector3.up * HeadHeight;
                Camera camera = Camera.main;
                // "Beyond the head": on the far side of it from the camera, so the camera sees the hair against the lamp.
                Vector3 away = camera != null ? head - camera.transform.position : player.transform.forward;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-4f)
                {
                    away = player.transform.forward;
                    away.y = 0f;
                }
                if (away.sqrMagnitude < 1e-4f)
                {
                    away = Vector3.forward;
                }
                away.Normalize();

                _lamp = new GameObject(LampName);
                _lamp.transform.position = new Vector3(head.x, feet.y + LampHeight, head.z) + away * LampDistance;
                var light = _lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LampColour;
                light.range = LampRange;
                light.shadows = LightShadows.None;   // a car's indicator casts none either: the worst case for hair
                var data = _lamp.AddComponent<HDAdditionalLightData>();
                data.SetIntensity(LampLumens, LightUnit.Lumen);
                data.range = LampRange;
                data.affectsVolumetric = false;
                UnityEngine.Object.Destroy(_lamp, LampSeconds);   // the engine's own timer: nothing of ours runs meanwhile

                HairLook.Logger?.Warn("Hair test: a lamp of " + LampLumens.ToString("F0") + " lm stands " + LampDistance.ToString("F0") + " m beyond the player's head for "
                                      + LampSeconds.ToString("F0") + " s. " + HairLook.Describe(character));
                Notifications.Show(NotificationType.Info, "reallife_hair_test_lamp", null, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                HairLook.Logger?.Error(e);
                RemoveLamp();
            }
        }

        /// <summary>
        /// "Test: a street lamp over my head for 20 seconds": a copy of the light of the nearest of the game's street lamps
        /// (its kind, colour, cone, range, brightness and shadows, as the game's settings have them), as high above the
        /// player's feet as it stands above its own post's foot and a metre to the camera's far side of the head, pointing as
        /// it points. The case of the report: at night under a lamp at a stop, the head seen from above looked bald.
        /// </summary>
        public static void StreetLamp()
        {
            try
            {
                AppearanceSetter character = Player(out TestRefusal why);
                PlayerController player = PlayerHelper.PlayerController;
                if (character == null || player == null)
                {
                    Refuse(character == null ? why : TestRefusal.NotLoaded);
                    return;
                }
                RemoveLamp();
                Vector3 feet = player.transform.position;
                Vector3 head = feet + Vector3.up * HeadHeight;
                Light model = NearestStreetLight(feet, out float height, out float distance);
                Camera camera = Camera.main;
                Vector3 away = camera != null ? head - camera.transform.position : player.transform.forward;
                away.y = 0f;
                away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;

                _lamp = new GameObject(LampName);
                var light = _lamp.AddComponent<Light>();
                var data = _lamp.AddComponent<HDAdditionalLightData>();
                string said;
                if (model != null)
                {
                    light.type = model.type;
                    light.color = model.color;
                    light.range = model.range;
                    light.spotAngle = model.spotAngle;
                    light.innerSpotAngle = model.innerSpotAngle;
                    light.shadows = model.shadows;
                    light.renderingLayerMask = model.renderingLayerMask;
                    if (model.TryGetComponent(out HDAdditionalLightData modelData))
                    {
                        modelData.CopyTo(data);
                    }
                    light.intensity = Mathf.Max(model.intensity, LampFullOf(model));
                    _lamp.transform.rotation = model.transform.rotation;
                    _lamp.transform.position = new Vector3(head.x, feet.y + height, head.z) + away * StreetLampOffset;
                    said = "a copy of the street lamp light '" + model.name + "' " + distance.ToString("F0") + " m away (" + model.type + ", intensity "
                           + light.intensity.ToString("F0") + ", range " + light.range.ToString("F0") + " m, shadows " + light.shadows + ")";
                }
                else
                {
                    light.type = LightType.Point;
                    light.color = StreetLampColour;
                    light.range = LampRange * 2f;
                    light.shadows = LightShadows.None;
                    data.SetIntensity(StreetLampLumens, LightUnit.Lumen);
                    data.range = LampRange * 2f;
                    height = StreetLampHeight;
                    _lamp.transform.position = new Vector3(head.x, feet.y + height, head.z) + away * StreetLampOffset;
                    said = "no street lamp of the game's is near, so a point light of " + StreetLampLumens.ToString("F0") + " lm";
                }
                data.affectsVolumetric = false;
                UnityEngine.Object.Destroy(_lamp, LampSeconds);
                HairLook.Logger?.Warn("Hair test: " + said + " stands " + height.ToString("F1", CultureInfo.InvariantCulture) + " m above the player's feet, " + StreetLampOffset.ToString("F0")
                                      + " m beyond the head, for " + LampSeconds.ToString("F0") + " s. " + HairLook.Describe(character));
                Notifications.Show(NotificationType.Info, "reallife_hair_test_streetlamp", null, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                HairLook.Logger?.Error(e);
                RemoveLamp();
            }
        }

        // The nearest light of the game's street lamps, how high it hangs above its post's foot and how far it is. Once, on a press.
        private static Light NearestStreetLight(Vector3 from, out float height, out float distance)
        {
            height = StreetLampHeight;
            distance = 0f;
            if (!PerfGame.KnowsLamps)
            {
                return null;
            }
            Light best = null;
            float nearest = float.MaxValue;
            foreach (global::StreetLamp lamp in UnityEngine.Object.FindObjectsByType<global::StreetLamp>(FindObjectsSortMode.None))
            {
                Light[] lights = lamp != null ? PerfGame.LightsOf(lamp) : null;
                if (lights == null)
                {
                    continue;
                }
                foreach (Light light in lights)
                {
                    float d = light != null ? (light.transform.position - from).sqrMagnitude : float.MaxValue;
                    if (d < nearest)
                    {
                        nearest = d;
                        best = light;
                        height = Mathf.Clamp(light.transform.position.y - lamp.transform.position.y, 2f, 12f);
                    }
                }
            }
            distance = best != null ? Mathf.Sqrt(nearest) : 0f;
            return best;
        }

        // The brightness the game gives a lit street lamp (StreetLamp.maxValue): by day its lights stand at 0.
        private static float LampFullOf(Light light)
        {
            global::StreetLamp lamp = light.GetComponentInParent<global::StreetLamp>();
            return lamp != null && LampMaxValue != null && LampMaxValue.GetValue(lamp) is float full ? full : 0f;
        }

        /// <summary>"Test: switch the hair finish off and on".</summary>
        public static void Switch()
        {
            try
            {
                if (!Settings.CharacterHair)
                {
                    TestRefusals.Tell(HairLook.Logger, Test, TestRefusal.FeatureOff);
                    return;
                }
                if (Player(out TestRefusal why) == null)
                {
                    Refuse(why);
                    return;
                }
                FinishOff = !FinishOff;   // CharacterQuality compares it on its next frame and finishes or restores once
                HairLook.Logger?.Warn("Hair test: the finish is " + (FinishOff ? "off for this session (the game's own hair materials are back)" : "on again") + ".");
                Notifications.Show(NotificationType.Info, FinishOff ? "reallife_hair_test_off" : "reallife_hair_test_on", null, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                HairLook.Logger?.Error(e);
            }
        }

        /// <summary>End of the scene, or the save prepared for removal: the lamp goes and the finish is the option's again.</summary>
        public static void Forget()
        {
            RemoveLamp();
            FinishOff = false;
        }

        private static void RemoveLamp()
        {
            if (_lamp != null)
            {
                UnityEngine.Object.Destroy(_lamp);
            }
            _lamp = null;
        }

        // The player's detailed character: the only one of the player's that wears strand hair.
        private static AppearanceSetter Player(out TestRefusal why)
        {
            why = TestRefusal.None;
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                why = TestRefusal.NotLoaded;
                return null;
            }
            PlayerDouble double_ = PlayerDouble.Instance;
            AppearanceSetter character = double_ != null ? double_.DoubleLook : null;
            if (character == null)
            {
                why = TestRefusal.FeatureOff;   // "the detailed player" is off, failed, or not built yet
            }
            return character;
        }

        private static void Refuse(TestRefusal why)
        {
            if (why == TestRefusal.FeatureOff)
            {
                TestRefusals.TellKey(HairLook.Logger, Test, "reallife_hair_test_no_double");
            }
            else
            {
                TestRefusals.Tell(HairLook.Logger, Test, why);
            }
        }
    }
}
