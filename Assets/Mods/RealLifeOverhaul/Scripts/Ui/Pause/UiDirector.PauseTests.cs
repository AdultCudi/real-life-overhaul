using System;
using System.Collections;
using Scenes.MainMenu;
using UI.MiniMenu;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the pause menu (Options, Tests): the pause menu opened, as Esc opens it, and said to be
    /// on the kit or the game's own; and the pause screens switched to the game's own and back. Nothing is saved: the
    /// switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] PauseScreens = { PauseKits.PauseId };

        /// <summary>Options, Tests: the pause menu, as Esc opens it; from Options it closes Options back to the menu, as the game's own Back does.</summary>
        public void TestPauseMenu()
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
                else if (!MiniMenu.IsOpen)
                {
                    menu.Toggle(true);
                }
                StartCoroutine(Guard.Routine(PauseSoon()));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the pause screens switched to the game's own, and back on the next press.</summary>
        public void TestPauseSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(PauseKits.PauseId);
                for (int i = 0; i < PauseScreens.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(PauseScreens[i]), on);
                }
                Slow();
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_pause_kit" : "reallife_ui_test_pause_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The menu shows a frame or two after it is asked for: then says whether the kit draws it, or why it is not open.
        private IEnumerator PauseSoon()
        {
            yield return null;
            yield return null;
            if (!MiniMenu.IsOpen)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_pause_refused", null, 8f);
                yield break;
            }
            Notifications.Show(NotificationType.Info, PauseKits.PauseShowing ? "reallife_ui_test_buy_on_kit" : "reallife_ui_test_buy_on_game", null, 8f);
        }
    }
}
