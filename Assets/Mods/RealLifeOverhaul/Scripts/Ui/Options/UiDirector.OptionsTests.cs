using System;
using System.Collections;
using Scenes.MainMenu;
using UI.MiniMenu;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the game's Options in the city (Options, Tests): the pause menu opened and Options on top of it, said to
    /// be on the kit or the game's own; and the two options screens switched to the game's own and back. Nothing is saved: the switch test
    /// changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] OptionsScreens = { OptionsKits.CityId, OptionsKits.DifficultyId };

        /// <summary>Options, Tests: Options opened from the pause menu, as its Options row opens it; from inside Options it goes back to the menu and in again.</summary>
        public void TestOptions()
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
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_options_none", null, 8f);
                    return;
                }
                if (Options.IsVisible)
                {
                    menu.CloseOptions();   // the tests run inside Options: back to the pause menu, as Options' own Back
                }
                else if (!MiniMenu.IsOpen)
                {
                    menu.Toggle(true);
                }
                StartCoroutine(Guard.Routine(OptionsSoon(menu)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the options screens switched to the game's own, and back on the next press.</summary>
        public void TestOptionsSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(OptionsKits.CityId);
                for (int i = 0; i < OptionsScreens.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(OptionsScreens[i]), on);
                }
                Slow();
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_options_kit_on" : "reallife_ui_test_options_game_on", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The menu shows a frame or two after it is asked for, Options a frame or two after that: then says whether the kit draws it.
        private IEnumerator OptionsSoon(MiniMenu menu)
        {
            yield return null;
            yield return null;
            if (!MiniMenu.IsOpen)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_options_refused", null, 8f);
                yield break;
            }
            menu.OpenOptions();
            yield return null;
            yield return null;
            Notifications.Show(NotificationType.Info, OptionsKits.Showing ? "reallife_ui_test_options_kit" : "reallife_ui_test_options_game", null, 8f);
        }
    }
}
