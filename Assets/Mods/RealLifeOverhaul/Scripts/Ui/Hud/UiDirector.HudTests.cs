using System;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The HUD host's tests (Options, Tests): the status bar over the running city with every pill the game and the mod
    /// give it now, and a world prompt over the player that follows them as they walk and waits at the screen's edge when
    /// the camera turns away. Each press of a test shows its piece, the next press takes it away, and the notice says which
    /// and why it cannot run when it cannot (the interface off, the fonts loading, the HUD not built yet).
    /// </summary>
    public sealed partial class UiDirector
    {
        private const float PromptLift = 2.2f;   // metres over the player's feet: above their head, as a box over a person stands

        private HudFollower _testPrompt;
        private bool _testBar;   // the bar's test showed the HUD: the prompt's leaves it standing

        /// <summary>Options, Tests: the HUD's status bar over the city, or away again.</summary>
        public void TestHud()
        {
            try
            {
                HudHost host = TestHost();
                if (host == null)
                {
                    return;
                }
                if (HudLive.Live)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_hud_live", null, 8f);   // the HUD stands over the city on its own (Ui/HudScreens)
                    return;
                }
                if (_testBar)
                {
                    _testBar = false;
                    if (_testPrompt == null)
                    {
                        host.Hide();
                    }
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_hud_gone", null, 6f);
                    return;
                }
                _testBar = true;
                host.Show();
                Notifications.Show(NotificationType.Info, "reallife_ui_test_hud_shown", null, 8f);
                Logger?.Info("UI: the HUD test shows the status bar at a px scale of " + host.PxScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                             + " (2 unless the UI zoom made the canvas narrower than " + UiHudRules.LeastWidth + " px).");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: a world prompt over the player with the place the HUD names, following them; or away again.</summary>
        public void TestHudPrompt()
        {
            try
            {
                HudHost host = TestHost();
                if (host == null)
                {
                    return;
                }
                if (_testPrompt != null)
                {
                    EndTestPrompt(host);
                    return;
                }
                PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
                if (player == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
                    return;
                }
                UiStatusModel bar = HudStatusGame.Bar();
                Transform feet = player.transform;
                var model = new UiWorldPromptModel
                {
                    Title = bar != null && !string.IsNullOrEmpty(bar.Place) ? bar.Place : UiText.T("reallife_ui_test_hud_prompt"), Icon = UiIcon.MapPin,
                    Rows = bar != null && !string.IsNullOrEmpty(bar.District)
                        ? new[] { new UiInfoRow { Label = UiText.T("reallife_ui_test_hud_district"), Value = bar.District } }
                        : new UiInfoRow[0],
                    Actions = new[] { new UiPromptAction { Label = UiText.T("common_close"), Main = true } },
                };
                host.Show();
                _testPrompt = host.Follow(model, () => feet != null ? feet.position + Vector3.up * PromptLift : Vector3.zero, action => EndTestPrompt(host));
                Notifications.Show(NotificationType.Info, "reallife_ui_test_hud_prompt_shown", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void EndTestPrompt(HudHost host)
        {
            host.Unfollow(_testPrompt);
            _testPrompt = null;
            if (!_testBar)
            {
                host.Hide();
            }
        }

        // The HUD host for a test, or null with a notice saying exactly why there is none yet.
        private static HudHost TestHost()
        {
            if (!On)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                return null;
            }
            if (!UiFonts.Ready)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_fonts", null, 8f);
                return null;
            }
            if (!UiScreenSwitches.On("hud"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_switch", null, 8f);
                return null;
            }
            HudHost host = HudHost.Ensure();
            if (host == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
            }
            return host;
        }
    }
}
