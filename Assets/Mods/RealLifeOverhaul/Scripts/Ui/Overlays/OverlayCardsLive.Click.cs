using System;
using System.Collections.Generic;
using Items.SpecialItems;
using Localizor;
using Localizor.LanguageChangeEvent;
using Player.HUD.ItemInfoOverlays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The large card's read: each kind of the game's click box that stands on, by the buttons it made (pressed as a click presses
    /// them, the mod's Cook and Family buttons in the fridge among them), the choices of its drop-downs (made through the game's
    /// own drop-down), the radio's volume (through its own slider) and the words it wrote. A workstation's name is renamed
    /// through the game's own text field, which the card focuses and mirrors while it is typed in.
    /// </summary>
    internal sealed partial class OverlayCardsLive
    {
        private readonly List<IOverlay> _parts = new List<IOverlay>(16);
        private readonly List<Button> _found = new List<Button>(16);
        private readonly UI.Elements.Dropdown[] _drops = new UI.Elements.Dropdown[UiOverlayModel.MostChoices];
        private readonly string[][] _options = new string[UiOverlayModel.MostChoices][];
        private Slider _slider;
        private EditableWorkstationHeader _renameHeader;
        private TMP_InputField _rename;
        private EmployeeOverlay _person;
        private int _openChoice = -1, _page;

        private bool FindClick()
        {
            _parts.Clear();
            _detailed.GetComponentsInChildren(true, _parts);
            _renameHeader = _detailed.GetComponentInChildren<EditableWorkstationHeader>(true);
            _rename = _renameHeader != null && GameContract.OverlayRename.Field != null ? GameContract.OverlayRename.Field.GetValue(_renameHeader) as TMP_InputField : null;
            _person = _detailed.GetComponentInChildren<EmployeeOverlay>(true);
            return _parts.Count > 0;
        }

        /// <summary>True while the game's name field of a workstation is being typed in: the card mirrors its words each frame.</summary>
        private bool Typing() => _rename != null && _rename.gameObject.activeInHierarchy && _rename.isFocused;

        private void ReadClick(EntityController entity, bool renaming)
        {
            UiOverlayModel m = _model;
            m.Detail = true;
            m.Icon = IconOf(entity);
            string title = Plain(_headerOfDetailed.GetValue(_detailed) as TMP_Text);
            m.Title = renaming ? _rename.text + "|" : title;
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i] = null;
                _does[i] = null;
            }
            _slider = null;
            for (int i = 0; i < _drops.Length; i++)
            {
                _drops[i] = null;
            }
            for (int i = 0; i < _parts.Count; i++)
            {
                IOverlay part = _parts[i];
                if (part == null || !part.gameObject.activeSelf)
                {
                    continue;
                }
                switch (part)
                {
                    case EmployeeOverlay _:
                        m.Tag = m.Tag ?? UiText.T("reallife_ui_overlay_employee");
                        if (OverlayFactsLive.Employee(entity) == null)
                        {
                            Buttons(part, m);   // an empty station: the game's own buttons for the schedule stay on the card
                        }
                        break;
                    case DropdownOverlay d:
                        Holds(d, m);
                        break;
                    case MachineOverlay mo:
                        Workstation(mo, m);
                        break;
                    case MachineInfoOverlay mi:
                        var label = GameContract.OverlayMachineNote.Field?.GetValue(mi) as TextLocalizationComponent;
                        if (label != null && label.TextContainer != null)
                        {
                            m.Note = Plain(label.TextContainer.text);
                            m.NoteTone = WorldHost.Tone(label.TextContainer.color) == UiRole.DangerText ? UiRole.DangerText : UiRole.Text2;
                        }
                        break;
                    case RadioOverlay r:
                        Radio(r, m);
                        break;
                    default:
                        Buttons(part, m);
                        break;
                }
            }
            if (_rename != null && _rename.gameObject.activeInHierarchy)
            {
                Rename(m);
            }
            if (_openChoice >= 0 && _openChoice < m.ChoiceCount)
            {
                m.Choices[_openChoice].Open = true;
                m.Choices[_openChoice].Page = OverlayRules.ClampPage(_page, m.Choices[_openChoice].OptionCount, UiOverlayModel.PerPage);
                Options(ref m.Choices[_openChoice], _openChoice);
            }
            else
            {
                _openChoice = -1;
            }
            if (m.ActionCount > 0 && !m.Actions[0].Disabled)
            {
                m.Actions[0].Main = true;
            }
            _anchor = null;
            for (int i = 0; i < _parts.Count && _anchor == null; i++)
            {
                _anchor = _parts[i] != null && _parts[i].gameObject.activeSelf ? _parts[i].GetPrimaryInteractable() : null;
            }
        }

        // The game's buttons under a kind that are shown: each an action pressed through its own button; the buttons inside a
        // drop-down are its own and are left to it.
        private void Buttons(Component part, UiOverlayModel m)
        {
            _found.Clear();
            part.GetComponentsInChildren(false, _found);
            for (int i = 0; i < _found.Count && m.ActionCount < UiOverlayModel.MostActions; i++)
            {
                Button b = _found[i];
                if (b == null || !b.gameObject.activeInHierarchy || b.GetComponentInParent<UI.Elements.Dropdown>(false) != null)
                {
                    continue;
                }
                string label = Plain(UiWidgets.Label(b));
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }
                _buttons[m.ActionCount] = b;
                m.AddAction(new UiOverlayAction { Label = label, Disabled = !b.interactable });
            }
        }

        // What a shelf holds: the game's own drop-down as a choice, and the stock the game says under it.
        private void Holds(DropdownOverlay d, UiOverlayModel m)
        {
            var drop = GameContract.OverlayDropdown.Field?.GetValue(d) as UI.Elements.Dropdown;
            var stock = GameContract.OverlayStockLabel.Field?.GetValue(d) as TMP_Text;
            m.Tag = m.Tag ?? UiText.T("reallife_ui_overlay_stock");
            Choice(m, drop, UiText.T("reallife_ui_overlay_holds"));
            if (stock != null && stock.gameObject.activeSelf && !string.IsNullOrEmpty(stock.text))
            {
                m.AddRow(UiText.T("reallife_ui_overlay_stock"), After(stock.text));
            }
            Buttons(d, m);
        }

        // The machine: its two choices, the machines it needs (a tick once there) and the ingredients the pallets lack.
        private void Workstation(MachineOverlay mo, UiOverlayModel m)
        {
            m.Tag = m.Tag ?? UiText.T("reallife_ui_overlay_recipe");
            Choice(m, GameContract.OverlayWorkstation.Field?.GetValue(mo) as UI.Elements.Dropdown, UiText.T("reallife_ui_overlay_workstation"));
            Choice(m, GameContract.OverlayRecipe.Field?.GetValue(mo) as UI.Elements.Dropdown, UiText.T("reallife_ui_overlay_makes"));
            var template = GameContract.OverlayMachines.Field?.GetValue(mo) as Transform;
            Transform list = template != null ? template.parent : null;
            for (int i = 0; list != null && i < list.childCount && m.CheckCount < UiOverlayModel.MostChecks; i++)
            {
                Transform row = list.GetChild(i);
                if (row == template || !row.gameObject.activeSelf)
                {
                    continue;
                }
                var words = row.GetComponentInChildren<TextLocalizationComponent>(true);
                Transform active = row.Find("Active");
                string name = words != null && words.TextContainer != null ? Plain(words.TextContainer.text) : null;
                m.AddCheck(name, null, active != null && active.gameObject.activeSelf);
            }
            var label = GameContract.OverlayIngredients.Field?.GetValue(mo) as TMP_Text;
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                string[] names = new string[UiOverlayModel.MostChecks];
                bool[] lacking = new bool[UiOverlayModel.MostChecks];
                string red = ColorUtility.ToHtmlStringRGB(InstanceBehavior<GlobalReferences>.Instance.colors.red);
                int n = OverlayRules.Ingredients(label.text, red, names, lacking);
                for (int i = 0; i < n; i++)
                {
                    m.AddCheck(names[i], lacking[i] ? UiText.T("reallife_ui_overlay_short") : null, !lacking[i]);
                }
            }
        }

        // The radio: the station and the song in the game's words, the volume in steps through the slider, and the game's two buttons.
        private void Radio(RadioOverlay r, UiOverlayModel m)
        {
            m.Tag = m.Tag ?? UiText.T("reallife_ui_overlay_radio");
            m.Icon = UiIcon.MusicNotes;
            _slider = GameContract.OverlayRadioSlider.Field?.GetValue(r) as Slider;
            var station = GameContract.OverlayRadioStation.Field?.GetValue(r) as TMP_Text;
            var song = GameContract.OverlayRadioSong.Field?.GetValue(r) as TMP_Text;
            var controls = GameContract.OverlayRadioControls.Field?.GetValue(r) as GameObject;
            bool playing = controls != null && controls.activeSelf;
            if (playing)
            {
                m.AddRow(UiText.T("reallife_ui_overlay_station"), Plain(station));
                m.AddRow(UiText.T("reallife_ui_overlay_song"), Plain(song));
            }
            if (_slider != null)
            {
                m.Level = OverlayRules.Volume(_slider.value);
                m.LevelLabel = UiText.T("reallife_ui_overlay_volume");
            }
            Button next = playing ? UiWidgets.Calling(r, "NextStation") : null;
            Button toggle = UiWidgets.Calling(r, "ToggleRadio");
            if (next != null)
            {
                _buttons[m.ActionCount] = next;
                m.AddAction(new UiOverlayAction { Label = UiText.T("reallife_ui_overlay_next_station"), Icon = UiIcon.SkipForward, Disabled = !next.interactable });
            }
            if (toggle != null)
            {
                _buttons[m.ActionCount] = toggle;
                m.AddAction(new UiOverlayAction { Label = UiText.T(playing ? "reallife_ui_overlay_mute" : "reallife_ui_overlay_unmute"),
                    Icon = playing ? UiIcon.Pause : UiIcon.Play, Disabled = !toggle.interactable });
            }
        }

        // The workstation's name: renamed through the game's own text field, focused here and mirrored in the strip while it is typed in.
        private void Rename(UiOverlayModel m)
        {
            if (m.ActionCount >= UiOverlayModel.MostActions)
            {
                return;
            }
            _does[m.ActionCount] = FocusRename;
            m.AddAction(new UiOverlayAction { Label = UiText.T("reallife_ui_overlay_rename"), Icon = UiIcon.Pencil });
        }

        private void FocusRename()
        {
            if (_rename != null)
            {
                _rename.Select();
                _rename.ActivateInputField();
            }
        }

        // A drop-down of the game's as a choice: the words it shows now, how many options it holds and which is chosen.
        private void Choice(UiOverlayModel m, UI.Elements.Dropdown drop, string label)
        {
            if (drop == null || m.ChoiceCount >= UiOverlayModel.MostChoices)
            {
                return;
            }
            List<string> all = GameContract.OverlayOptions.Field?.GetValue(drop) as List<string>;
            int i = m.ChoiceCount++;
            _drops[i] = drop;
            int chosen = drop.SelectedOptionIndex;
            m.Choices[i] = new UiOverlayChoice
            {
                Label = label, Value = chosen >= 0 && all != null && chosen < all.Count ? OptionWords(drop, all[chosen]) : UiText.T("reallife_ui_overlay_choose"),
                Options = _options[i], OptionCount = all != null ? all.Count : 0, Chosen = chosen, Open = false, Page = 0,
            };
        }

        // An open choice's options as words, the page shown only: the list may run to a hundred, a page of them is all it needs.
        private void Options(ref UiOverlayChoice c, int index)
        {
            UI.Elements.Dropdown drop = _drops[index];
            List<string> all = drop != null ? GameContract.OverlayOptions.Field?.GetValue(drop) as List<string> : null;
            if (all == null)
            {
                return;
            }
            if (_options[index] == null || _options[index].Length < all.Count)
            {
                _options[index] = new string[all.Count];
            }
            OverlayRules.Window(all.Count, c.Page, UiOverlayModel.PerPage, out int first, out int end);
            for (int i = first; i < end; i++)
            {
                _options[index][i] = OptionWords(drop, all[i]);
            }
            c.Options = _options[index];
        }

        // An option as the game shows it: a key it translates, or the words as they are.
        private static string OptionWords(UI.Elements.Dropdown drop, string option)
        {
            bool localize = GameContract.OverlayOptionsLocalized.Field == null || !(GameContract.OverlayOptionsLocalized.Field.GetValue(drop) is bool b) || b;
            return Plain(localize ? option.GetLocalization() : option);
        }

        // The words after a label's own colon ("Stock: 12/40" is 12/40).
        private static string After(string words)
        {
            string plain = Plain(words);
            int colon = plain != null ? plain.IndexOf(':') : -1;
            return colon >= 0 ? plain.Substring(colon + 1).Trim() : plain;
        }

        // An option of the open choice chosen through the game's own drop-down: its listeners ask the game's questions.
        private bool Choose(int option)
        {
            UI.Elements.Dropdown drop = _openChoice >= 0 && _openChoice < _drops.Length ? _drops[_openChoice] : null;
            if (drop == null || option < 0)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                drop.SelectOption(option);
            }
            return true;
        }

        // The volume a tenth either way, through the game's own slider so its listener keeps the building's volume.
        private bool Step(int direction)
        {
            if (_slider == null)
            {
                return false;
            }
            int level = OverlayRules.Step(OverlayRules.Volume(_slider.value), direction);
            _relay = true;
            return UiWidgets.Slide(_slider, level / 100f);
        }
    }
}
