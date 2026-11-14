using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.Mods;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class OptionsScreen
    {
        private SettingsScreen _settings;
        private bool _modsOpen;
        private readonly Dictionary<string, string> _modNames = new Dictionary<string, string>(StringComparer.Ordinal);

        // The Mods category: the mod's own groups, each opening Mod settings' list at that group (the same list, in this window), then the
        // options other mods registered, each on the game's own control for it.
        private void ModsPage()
        {
            _settings = SettingsScreen.Make() as SettingsScreen;
            if (_settings != null && OptionsLayout.AppReady)
            {
                int groups = _settings.GroupCount;
                Heading(T("reallife_ui_opt_mods_rlo").Replace("{count}", groups.ToString(_inv)), UiIcon.Cube, true);
                for (int g = 0; g < groups; g++)
                {
                    Slot s = Take(Kind.Group);
                    s.Index = g;
                    int options = _settings.GroupOptions(g);
                    s.Row = new UiSettingRow
                    {
                        Control = UiSettingControl.Action,
                        Name = _settings.GroupName(g),
                        Tip = options > 0 ? T("reallife_ui_opt_mods_options").Replace("{count}", options.ToString(_inv)) : null,
                        Value = T("reallife_ui_opt_open"),
                        Icon = _settings.GroupIcon(g)
                    };
                }
            }
            OtherMods();
        }

        private void OtherMods()
        {
            Component controller = ModsController();
            Transform root = BuyKits.Get<Transform>(GameContract.ModsRoot, controller);
            string own = OptionKeysRules.FixedId;
            _modNames.Clear();
            ModNames();
            foreach (KeyValuePair<string, ModOptions> entry in OptionsService.RegisteredEntries)
            {
                if (entry.Key == own || entry.Value == null || entry.Value.Options.Count == 0)
                {
                    continue;
                }
                Heading(ModName(entry.Key), UiIcon.Code, true);
                for (int i = 0; i < entry.Value.Options.Count; i++)
                {
                    ModRow(entry.Value.Options[i], root);
                }
            }
        }

        private void ModRow(ModOption option, Transform root)
        {
            if (option is SplitterOption)
            {
                return;
            }
            if (option is HeaderOption)
            {
                Heading(T(option.Label), UiIcon.Info, true);
                return;
            }
            Component control = Control(root, option);
            Toggle toggle = option is ToggleOption ? BuyKits.Get<Toggle>(GameContract.ModsToggle, control) : null;
            Slider slider = option is SliderOption ? BuyKits.Get<Slider>(GameContract.ModsSlider, control) : null;
            UI.Elements.Dropdown dropdown = option is DropdownOption ? BuyKits.Get<UI.Elements.Dropdown>(GameContract.ModsDropdown, control) : null;
            Button button = option is ButtonOption ? BuyKits.Get<Button>(GameContract.ModsButton, control) : null;
            if (toggle != null)
            {
                AddToggle(option.Label, toggle);
            }
            else if (slider != null)
            {
                Localizor.LanguageChangeEvent.TextLocalizationComponent figure = BuyKits.Get<Localizor.LanguageChangeEvent.TextLocalizationComponent>(GameContract.ModsSliderFigure, control);
                AddSlider(option.Label, slider, figure != null && figure.gameObject.activeSelf ? figure.TextContainer : null, false);
            }
            else if (dropdown != null)
            {
                AddChoice(option.Label, dropdown);
            }
            else if (button != null)
            {
                AddAction(option.Label, "reallife_ui_opt_press", Do.None, button);   // the game's own button, pressed as a click presses it
            }
            else
            {
                AddAction(option.Label, "reallife_ui_opt_open_classic", Do.OpenClassic, null);   // a row only the game's own list can draw
            }
        }

        // The game's own control for an option, by the label it was made with: the view makes one per option, in the order of its list.
        private static Component Control(Transform root, ModOption option)
        {
            if (root == null)
            {
                return null;
            }
            GameMember[] types = { GameContract.ModsToggleLabel, GameContract.ModsSliderLabel, GameContract.ModsDropdownLabel, GameContract.ModsButtonLabel };
            Type want = option is ToggleOption ? types[0].Field?.DeclaringType : option is SliderOption ? types[1].Field?.DeclaringType
                : option is DropdownOption ? types[2].Field?.DeclaringType : option is ButtonOption ? types[3].Field?.DeclaringType : null;
            GameMember label = option is ToggleOption ? types[0] : option is SliderOption ? types[1] : option is DropdownOption ? types[2] : types[3];
            if (want == null)
            {
                return null;
            }
            Component[] all = root.GetComponentsInChildren(want, true);
            for (int i = 0; i < all.Length; i++)
            {
                TextLocalizationComponent words = BuyKits.Get<TextLocalizationComponent>(label, all[i]);
                if (words != null && string.Equals(words.Key, option.Label, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }
            return null;
        }

        private Component ModsController()
        {
            System.Reflection.FieldInfo root = GameContract.ModsRoot.Field;
            return root != null && root.DeclaringType != null ? _o.GetComponentInChildren(root.DeclaringType, true) : null;
        }

        // The loaded mods' own names by their ids, from the game's list of active mods.
        private void ModNames()
        {
            try
            {
                System.Reflection.MethodInfo list = GameContract.ModsActive.Method;
                if (list == null || !(list.Invoke(null, null) is IEnumerable mods))
                {
                    return;
                }
                foreach (object mod in mods)
                {
                    Type t = mod.GetType();
                    string id = t.GetField("Item1")?.GetValue(mod) as string;
                    string name = t.GetField("Item2")?.GetValue(mod) as string;
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                    {
                        _modNames[id] = name;
                    }
                }
            }
            catch (Exception)
            {
                // the ids stand in for names the game does not give
            }
        }

        private string ModName(string id) => _modNames.TryGetValue(id, out string name) ? name : id;

        // ------------------------------------------------------------------------------------------------- a group of the mod's own

        private bool OpenMods(int group)
        {
            if (_settings == null || !OptionsLayout.AppReady)
            {
                return false;
            }
            _modsOpen = true;
            _settings.ShowGroup(group);
            _choice = -1;
            _held = -1;
            _dirty = true;
            return true;
        }

        // Back out of the mod's own list: Mod settings steps back its own pages first, then this window shows its Mods page again.
        private bool BackMods()
        {
            if (_settings.Back())
            {
                _dirty = true;
                return true;
            }
            _modsOpen = false;
            _stamp++;
            _dirty = true;
            return true;
        }
    }
}
