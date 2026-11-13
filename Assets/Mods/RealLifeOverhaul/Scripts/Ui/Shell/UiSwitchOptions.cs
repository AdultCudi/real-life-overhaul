using System.Collections.Generic;
using BigAmbitions.Mods;
using BigAmbitions.ModsInternal;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's switch for each screen on the options page, one row each from the screens' entries
    /// (UiScreenSwitches), so a job that converts a screen adds its switch with the one word in its entry. The page puts
    /// them in order by Meta/OptionsTree, which places them the same way. Called once when the page is built.
    /// The game gives a switch its value from the saved option each time it builds the page, so a screen the kit faulted on
    /// since would still read on there while the game's own screen shows. <see cref="Look"/> sets such a switch to off, as the
    /// kit's Settings page reads it, and puts it back when the screen is tried again; pressing it on tries the screen again,
    /// as it does there.
    /// </summary>
    public static class UiSwitchOptions
    {
        private static readonly Dictionary<string, UnityAction<bool>> Presses = new Dictionary<string, UnityAction<bool>>(32);
        private static bool _held;   // a switch was set off for a fault: one look more, with the page showing, puts it right once the screen is tried again

        public static ModOptions Add(ModOptions options)
        {
            foreach (UiScreenDef def in UiScreenSwitches.All)
            {
                string option = UiScreenSwitches.OptionOf(def.Id);
                options = options.AddToggle(option, UiScreenSwitches.LabelOf(def.Id), UiScreenSwitches.Value(option), on => UiScreenSwitches.Set(option, on));
            }
            return options;
        }

        /// <summary>
        /// Once a second: while a screen's breaker is open (or was, when the page last showed) and the game's Options menu shows, each
        /// screen's switch on its Mods page reads as the kit's Settings page reads it. Nothing at all while no breaker is open.
        /// </summary>
        public static void Look()
        {
            if (UiBreaker.Count == 0 && !_held)
            {
                return;
            }
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            Component menu = uis != null ? uis.options : null;
            if (menu == null || !menu.gameObject.activeInHierarchy)
            {
                return;   // asked again when the page shows
            }
            bool held = false;
            ModOptionsToggleControl[] all = menu.GetComponentsInChildren<ModOptionsToggleControl>(true);
            foreach (UiScreenDef def in UiScreenSwitches.All)
            {
                string label = UiScreenSwitches.LabelOf(def.Id);
                for (int i = 0; i < all.Length; i++)
                {
                    TextLocalizationComponent words = all[i].GetComponentInChildren<TextLocalizationComponent>(true);
                    Toggle toggle = all[i].GetComponentInChildren<Toggle>(true);
                    if (words == null || toggle == null || words.Key != label)
                    {
                        continue;
                    }
                    bool faulted = UiBreaker.Tripped(def.Id);
                    held |= faulted;
                    bool shown = UiScreenSwitches.Value(UiScreenSwitches.OptionOf(def.Id)) && !faulted;
                    if (toggle.isOn != shown)
                    {
                        toggle.SetIsOnWithoutNotify(shown);
                    }
                    if (faulted)
                    {
                        Hook(toggle, def.Id);
                    }
                    break;
                }
            }
            _held = held;
        }

        // A faulted switch pressed on tries the screen again, as the Settings page's does. One listener a screen, put on once.
        private static void Hook(Toggle toggle, string screen)
        {
            if (!Presses.TryGetValue(screen, out UnityAction<bool> press))
            {
                press = on =>
                {
                    if (on)
                    {
                        UiScreenSwitches.Retry(UiScreenSwitches.OptionOf(screen));
                    }
                };
                Presses[screen] = press;
            }
            toggle.onValueChanged.RemoveListener(press);
            toggle.onValueChanged.AddListener(press);
        }
    }
}
