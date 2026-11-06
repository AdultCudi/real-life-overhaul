using System;
using System.Collections;
using BlueprintsUI;
using Scenes.MainMenu;
using UI.MiniMenu;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the blueprints window and the game's help (Options, Tests): the blueprints opened from the pause menu as its
    /// row opens them and the help opened as F1 opens it, each said to be on the kit or the game's own; and the two screens switched to the
    /// game's own and back. Nothing is saved: the switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] BlueprintScreens = { BlueprintsKits.BlueprintsId, HelpKits.HelpId };

        /// <summary>Options, Tests: the blueprints window, opened from the pause menu as its Blueprints row opens it.</summary>
        public void TestBlueprints()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                MiniMenu menu = PauseGame.Menu();
                if (menu == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_pause_none", null, 8f);
                    return;
                }
                if (Options.IsVisible)
                {
                    menu.CloseOptions();   // the tests run inside Options, which the pause menu opened: back to it, as Options' own Back
                }
                else if (!MiniMenu.IsOpen && !BlueprintsPanel.IsOpen)
                {
                    menu.Toggle(true);
                }
                if (!BlueprintsPanel.IsOpen && MiniMenu.IsOpen)
                {
                    menu.OpenBlueprints();
                }
                StartCoroutine(Guard.Routine(PanelSoon(() => BlueprintsPanel.IsOpen, () => BlueprintsKits.Showing)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the game's help, opened as F1 opens it.</summary>
        public void TestHelp()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
                if (help == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_pause_none", null, 8f);
                    return;
                }
                MiniMenu menu = PauseGame.Menu();
                if (Options.IsVisible && menu != null)
                {
                    menu.CloseOptions();
                }
                if (!HelpSystem.IsVisible)
                {
                    help.Toggle(true);
                }
                StartCoroutine(Guard.Routine(PanelSoon(() => HelpSystem.IsVisible, () => HelpKits.Showing)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the blueprints and help screens switched to the game's own, and back on the next press.</summary>
        public void TestBlueprintsSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(BlueprintsKits.BlueprintsId);
                for (int i = 0; i < BlueprintScreens.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(BlueprintScreens[i]), on);
                }
                Slow();
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_bp_kit" : "reallife_ui_test_bp_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // A window shows a frame or two after it is asked for: then says whether the kit draws it, or why it is not open.
        private IEnumerator PanelSoon(Func<bool> open, Func<bool> onKit)
        {
            yield return null;
            yield return null;
            if (!open())
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_pause_refused", null, 8f);
                yield break;
            }
            Notifications.Show(NotificationType.Info, onKit() ? "reallife_ui_test_buy_on_kit" : "reallife_ui_test_buy_on_game", null, 8f);
        }
    }
}
