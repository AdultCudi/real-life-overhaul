using System;
using System.Collections.Generic;
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
