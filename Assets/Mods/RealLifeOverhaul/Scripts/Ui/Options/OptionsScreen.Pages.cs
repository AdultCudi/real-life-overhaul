using System;
using System.Collections.Generic;
using RealLifeOverhaul.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class OptionsScreen
    {
        private enum Kind : byte
        {
            Heading = 0,
            Toggle,
            Slider,
            Choice,
            Scheme,
            Action,
            Key,
            Name,
            Group
        }

        /// <summary>What a row without a control of its own does when pressed.</summary>
        private enum Do : byte
        {
            None = 0,
            ResetWindows,
            OpenRadio,
            RefreshSongs,
            Unstuck,
            AskDeletion,
            Customize,
            SavePreset,
            CopyPreset,
            PastePreset,
            DeletePreset,
            OpenClassic
        }

        private sealed class Slot
        {
            public Kind Kind;
            public UiSettingRow Row;
            public string NameKey, TipKey, ValueKey;
            public Toggle T;
            public Slider S;
            public UI.Elements.Dropdown D;
            public Button B;
            public TMP_Text Fig;                // a figure the game writes for a slider (its percentage)
            public TMP_Text Text;               // the words a key binding shows
            public Component Opt;               // a custom difficulty option (the game's own slider or checkbox holder)
            public Do Act;
            public int Index;                   // a key's place, a group's number
            public float Min, Max;
            public bool Whole, Percent, Locked;
            public string[] Choices;
            public int Chosen = -1;
            public float Number;
            public string Applied;
        }

        private Slot Take(Kind kind)
        {
            if (_used >= _slots.Count)
            {
                _slots.Add(new Slot());
            }
            Slot s = _slots[_used++];
            s.Kind = kind;
            s.Row = new UiSettingRow();
            s.NameKey = s.TipKey = s.ValueKey = null;
            s.T = null;
            s.S = null;
            s.D = null;
            s.B = null;
            s.Fig = null;
            s.Text = null;
            s.Opt = null;
            s.Act = Do.None;
            s.Index = 0;
            s.Min = s.Max = 0f;
            s.Whole = s.Percent = s.Locked = false;
            s.Choices = null;
            s.Chosen = -1;
            s.Number = 0f;
            s.Applied = null;
            return s;
        }

        // ------------------------------------------------------------------------------------------------- one category

        private void FillCategory(int category)
        {
            if (category >= OptionsRules.Categories)
            {
                return;   // another mod's tab has no page here: it opens the game's own view
            }
            switch (category)
            {
                case OptionsRules.Graphics: Graphics(); break;
                case OptionsRules.Audio: Audio(); break;
                case OptionsRules.Controls: Controls(); break;
                case OptionsRules.Game: GamePage(); break;
                default: ModsPage(); break;
            }
        }

        // A search spans Graphics, Audio, Controls and Game: each page built in turn, only the rows that have the words kept.
        private void FillFound()
        {
            for (int i = 0; i < _found.Length; i++)
            {
                _found[i] = 0;
            }
            int kept = 0;
            for (int c = 0; c < OptionsRules.Categories - 1 && kept < SettingsRules.MostFound; c++)
            {
                int from = _used;
                FillCategory(c);
                int to = _used;
                _used = from;
                for (int i = from; i < to && kept < SettingsRules.MostFound; i++)
                {
                    Slot s = _slots[i];
                    if (s.Kind == Kind.Heading || s.Kind == Kind.Scheme)
                    {
                        continue;
                    }
                    Write(s, true);
                    if (!OptionsRules.Matches(_query, s.Row.Name, s.Row.Tip))
                    {
                        continue;
                    }
                    Slot into = _slots[_used];
                    if (_used != i)
                    {
                        _slots[_used] = s;
                        _slots[i] = into;
                    }
                    _used++;
                    kept++;
                    _found[c]++;
                }
            }
            for (int i = 0; i < _used; i++)
            {
                if (OptionsRules.MarkAt(_query, _slots[i].Row.Name, out int start, out int length))
                {
                    _slots[i].Row.MatchStart = start;
                    _slots[i].Row.MatchLength = length;
                }
            }
            _m.List.Found = kept.ToString(_inv);
            _m.List.EmptyTitle = T("reallife_ui_opt_found_none");
            _m.List.EmptyBody = T("reallife_ui_opt_found_none_body");
        }

        private void Heading(string nameKey, UiIcon icon)
        {
            Slot s = Take(Kind.Heading);
            s.NameKey = nameKey;
            s.Row = new UiSettingRow { Control = UiSettingControl.None, Name = T(nameKey), Icon = icon };
        }

        private void Heading(string words, UiIcon icon, bool plain)
        {
            Slot s = Take(Kind.Heading);
            s.Row = new UiSettingRow { Control = UiSettingControl.None, Name = words, Icon = icon };
        }

        private void AddToggle(string nameKey, Toggle t, string tipKey = null)
        {
            if (t == null || !t.gameObject.activeSelf)
            {
                return;
            }
            Slot s = Take(Kind.Toggle);
            s.NameKey = nameKey;
            s.TipKey = tipKey;
            s.T = t;
        }

        private void AddSlider(string nameKey, Slider slider, TMP_Text figure, bool percent, string tipKey = null)
        {
            if (slider == null || !slider.gameObject.activeSelf)
            {
                return;
            }
            Slot s = Take(Kind.Slider);
            s.NameKey = nameKey;
            s.TipKey = tipKey;
            s.S = slider;
            s.Fig = figure;
            s.Percent = percent;
        }

        private void AddChoice(string nameKey, UI.Elements.Dropdown d)
        {
            if (d == null || !d.gameObject.activeSelf)
            {
                return;
            }
            Slot s = Take(Kind.Choice);
            s.NameKey = nameKey;
            s.D = d;
        }

        private void AddAction(string nameKey, string valueKey, Do act, Button b, string tipKey = null)
        {
            Slot s = Take(Kind.Action);
            s.NameKey = nameKey;
            s.ValueKey = valueKey;
            s.TipKey = tipKey;
            s.Act = act;
            s.B = b;
        }

        private T Field<T>(GameMember member) where T : class => BuyKits.Get<T>(member, _o);

        private void Graphics()
        {
            Heading("reallife_ui_opt_sec_display", UiIcon.Monitor);
            AddToggle("menu_options_graphics_fullscreen", Field<Toggle>(GameContract.OptFullscreen));
            AddChoice("menu_options_graphics_resolution", Field<UI.Elements.Dropdown>(GameContract.OptResolution));
            AddChoice("menu_options_graphics_fps_limit", Field<UI.Elements.Dropdown>(GameContract.OptFps));
            AddSlider("menu_options_ui_zooming", Field<Slider>(GameContract.OptZoom), Field<TMP_Text>(GameContract.OptZoomText), true);
            AddSlider("menu_options_brightness", Field<Slider>(GameContract.OptGamma), Field<TMP_Text>(GameContract.OptGammaText), true);
            AddToggle("menu_options_graphics_lowdetailcitymap", Field<Toggle>(GameContract.OptLowMap));
            AddToggle("menu_options_graphics_showfps", Field<Toggle>(GameContract.OptShowFps));
            GameObject windows = Field<GameObject>(GameContract.OptWindows);
            if (windows != null && windows.activeSelf)
            {
                AddAction("menu_options_reset_windows", "menu_options_reset_windows_button", Do.ResetWindows, windows.GetComponentInChildren<Button>(true), "menu_options_reset_windows_tip");
            }
            Heading("reallife_ui_opt_sec_quality", UiIcon.Sliders);
            AddChoice("menu_options_graphics_gfx_quality", Field<UI.Elements.Dropdown>(GameContract.OptGfx));
            AddChoice("menu_options_graphics_particle_quality", Field<UI.Elements.Dropdown>(GameContract.OptParticles));
            AddChoice("menu_options_graphics_texture_quality", Field<UI.Elements.Dropdown>(GameContract.OptTextures));
            AddChoice("menu_options_graphics_aa", Field<UI.Elements.Dropdown>(GameContract.OptAa));
            AddChoice("menu_options_graphics_shadows", Field<UI.Elements.Dropdown>(GameContract.OptShade));
            AddChoice("menu_options_graphics_hbao", Field<UI.Elements.Dropdown>(GameContract.OptHbao));
        }

        private void Audio()
        {
            Heading("reallife_ui_opt_sec_volume", UiIcon.SpeakerHigh);
            AddSlider("menu_options_audio_global_volume", Field<Slider>(GameContract.OptGlobal), null, true);
            AddSlider("menu_options_audio_menumusic_volume", Field<Slider>(GameContract.OptMenuMusic), null, true);
            AddSlider("menu_options_audio_radio_volume", Field<Slider>(GameContract.OptRadio), null, true);
            AddSlider("menu_options_audio_sfx_volume", Field<Slider>(GameContract.OptSfx), null, true);
            AddSlider("menu_options_audio_ai_music_volume", Field<Slider>(GameContract.OptAiMusic), null, true);
            Heading("reallife_ui_opt_sec_radio", UiIcon.MusicNotes);
            AddAction("menu_options_local_radio_songs", "menu_options_open_folder", Do.OpenRadio, null);
            AddAction("reallife_ui_opt_songs_again", "common_refresh", Do.RefreshSongs, null);
        }

        private void GamePage()
        {
            Heading("reallife_ui_opt_sec_formats", UiIcon.Globe);
            AddChoice("menu_options_others_language", Field<UI.Elements.Dropdown>(GameContract.OptLanguage));
            AddChoice("menu_options_others_number_formatting", Field<UI.Elements.Dropdown>(GameContract.OptNumbers));
            AddToggle("menu_options_others_time_format", Field<Toggle>(GameContract.OptTime));
            AddToggle("menu_options_others_unit_system", Field<Toggle>(GameContract.OptUnits));
            AddToggle("menu_options_others_seasonal_decorations", Field<Toggle>(GameContract.OptSeasonal));
            AddToggle("ba:menu_options_others_control_hints", Field<Toggle>(GameContract.OptHints));
            GameObject box = Field<GameObject>(GameContract.OptDifficultyBox);
            if (box != null && box.activeSelf)
            {
                Heading("reallife_ui_opt_sec_difficulty", UiIcon.Trophy);
                GameObject custom = Field<GameObject>(GameContract.OptCustomize);
                if (custom != null && custom.activeSelf)
                {
                    AddAction("ba:menu_options_others_difficulty", "ba:difficulty_customize", Do.Customize, custom.GetComponentInChildren<Button>(true), "reallife_ui_opt_diff_tip");
                }
                else
                {
                    AddChoice("ba:menu_options_others_difficulty", Field<UI.Elements.Dropdown>(GameContract.OptDifficulty));
                }
            }
            Heading("reallife_ui_opt_sec_saving", UiIcon.FloppyDisk);
            AddSlider("menu_options_time_between_auto_saves", Field<Slider>(GameContract.OptSaveEvery), AmountOf(GameContract.OptSaveEveryText), false);
            AddSlider("menu_options_max_auto_saves_per_game", Field<Slider>(GameContract.OptSaveMost), AmountOf(GameContract.OptSaveMostText), false);
            AddSlider("menu_options_game_speed", Field<Slider>(GameContract.OptSpeed), Field<TMP_Text>(GameContract.OptSpeedText), false);
            GameObject stuck = Field<GameObject>(GameContract.OptUnstuckBox);
            if (stuck != null && stuck.activeSelf)
            {
                Heading("reallife_ui_opt_sec_help", UiIcon.Wrench);
                AddAction("options_unstuck_title", "options_unstuck_button", Do.Unstuck, Field<Button>(GameContract.OptUnstuck), "options_unstuck_description");
            }
            Heading("reallife_ui_opt_sec_privacy", UiIcon.ShieldCheck);
            AddToggle("menu_options_others_allow_tracking", Field<Toggle>(GameContract.OptTracking));
            AddAction("menu_options_others_request_data_deletion", "menu_options_others_request_data_deletion", Do.AskDeletion, Field<Button>(GameContract.OptDeletion));
        }

        // The words a game writes beside a slider (its amount of minutes or saves), read from its text.
        private TMP_Text AmountOf(GameMember member)
        {
            Localizor.LanguageChangeEvent.TextLocalizationComponent loc = Field<Localizor.LanguageChangeEvent.TextLocalizationComponent>(member);
            return loc != null ? loc.TextContainer : null;
        }

        // ------------------------------------------------------------------------------------------------- a row's words and state

        private void Write(Slot s, bool named)
        {
            UiSettingRow r = s.Row;
            if (named || string.IsNullOrEmpty(r.Name))
            {
                if (s.Kind != Kind.Group && s.Kind != Kind.Key && s.NameKey != null)
                {
                    r.Name = T(s.NameKey);
                }
                r.Tip = s.TipKey != null ? T(s.TipKey) : r.Tip;
            }
            switch (s.Kind)
            {
                case Kind.Toggle:
                {
                    bool on = s.T != null && s.T.isOn;
                    r.Control = UiSettingControl.Switch;
                    r.On = on;
                    r.Value = on ? T("reallife_meta_on") : T("reallife_meta_off");
                    r.Greyed = s.T == null || !s.T.interactable;
                    break;
                }
                case Kind.Slider:
                    WriteSlider(s, ref r);
                    break;
                case Kind.Choice:
                    WriteChoice(s, ref r);
                    break;
                case Kind.Action:
                    r.Control = UiSettingControl.Action;
                    r.Value = s.ValueKey != null ? T(s.ValueKey) : r.Value;
                    r.Greyed = s.Locked || (s.B != null && (!s.B.interactable || !Free(s.B)));
                    if (s.Act == Do.OpenRadio)
                    {
                        r.Tip = SongsLine();
                    }
                    break;
                case Kind.Scheme:
                    r.Control = UiSettingControl.Choice;
                    if (s.Choices == null || s.Applied != _locale)
                    {
                        s.Choices = new[] { T("menu_options_controls_keyboard"), T("menu_options_controls_controller") };
                        s.Applied = _locale;
                    }
                    s.Chosen = _scheme;
                    r.Value = s.Choices[_scheme == 1 ? 1 : 0];
                    break;
                case Kind.Key:
                    WriteKey(s, ref r);
                    break;
                case Kind.Name:
                    r.Control = UiSettingControl.Text;
                    r.Command = NameCommand;
                    r.Value = _presetName;
                    r.Tip = string.IsNullOrEmpty(_presetName) ? T("reallife_ui_opt_diff_name_hint") : null;
                    break;
            }
            if (_wasDiff)
            {
                WriteDifficulty(s, ref r);
            }
            if (r.Greyed && s.Kind != Kind.Key)
            {
                r.Tip = s.Locked ? T("reallife_ui_opt_diff_locked") : T("reallife_ui_opt_locked");
            }
            r.Held = _held >= 0 && _held < _used && _slots[_held] == s;
            s.Row = r;
        }

        private void WriteSlider(Slot s, ref UiSettingRow r)
        {
            Slider sl = s.S;
            r.Control = UiSettingControl.Slider;
            if (sl == null)
            {
                return;
            }
            s.Min = sl.minValue;
            s.Max = sl.maxValue;
            s.Whole = sl.wholeNumbers;
            r.Share = OptionsRules.ShareOf(sl.value, s.Min, s.Max);
            string figure = s.Fig != null ? UiWidgets.Text(s.Fig) : null;
            if (string.IsNullOrEmpty(figure))
            {
                figure = s.Percent ? Math.Round(r.Share * 100f).ToString(_inv) + "%" : Math.Round(sl.value).ToString(_inv);
            }
            r.Value = figure;
            r.Greyed = !sl.interactable;
        }

        private void WriteChoice(Slot s, ref UiSettingRow r)
        {
            r.Control = UiSettingControl.Choice;
            UI.Elements.Dropdown d = s.D;
            if (d == null)
            {
                return;
            }
            List<string> words = BuyKits.Get<List<string>>(GameContract.BizOptions, d);
            bool localize = GameContract.BizOptionsLocalized.Field != null && (bool)GameContract.BizOptionsLocalized.Field.GetValue(d);
            int n = words != null ? words.Count : 0;
            if (s.Choices == null || s.Choices.Length != n || s.Applied != _locale)
            {
                s.Choices = new string[n];
                s.Applied = _locale;
                for (int i = 0; i < n; i++)
                {
                    s.Choices[i] = localize ? T(words[i]) : words[i];
                }
            }
            s.Chosen = d.SelectedOptionIndex;
            r.Value = s.Chosen >= 0 && s.Chosen < n ? s.Choices[s.Chosen] : s.ValueKey != null ? T(s.ValueKey) : string.Empty;
            r.Greyed = Held(d);
        }

        private string SongsLine()
        {
            Localizor.LanguageChangeEvent.TextLocalizationComponent loc = Field<Localizor.LanguageChangeEvent.TextLocalizationComponent>(GameContract.OptSongs);
            return loc != null && loc.TextContainer != null ? UiWidgets.Text(loc.TextContainer) : string.Empty;
        }
    }
}
