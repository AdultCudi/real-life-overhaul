using System;
using System.Collections.Generic;
using BigAmbitions.Mods;
using BigAmbitions.ModsInternal;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The word on each button of the options page. The game's button row has two texts: the label beside the
    /// button, which the game fills from the row, and one on the button itself, which it never fills, so the
    /// button shows whatever its template was saved with. This part of the class writes that second text: a
    /// section's button says "Open" or "Close", a button that does something says what ("Run", "Apply", "Reset").
    ///
    /// Done once per drawing of the page (the game destroys and makes every row each time), from a row that draws
    /// nothing at the end of the mod's rows, and when a section opens or closes. Nothing runs per frame. A button
    /// whose text cannot be found is left as it is, and one line goes to the log.
    /// </summary>
    public static partial class OptionsLayout
    {
        private const string CaptionRun = "reallife_opt_caption_run";
        private const string CaptionOpen = "reallife_opt_caption_open";
        private const string CaptionClose = "reallife_opt_caption_close";
        private const string CaptionFound = "reallife_opt_caption_found";
        private const string CaptionChange = "reallife_opt_caption_change";

        // Buttons whose word is not "Run": the row's label key, then the word's key.
        private static readonly Dictionary<string, string> CaptionByLabel = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { SettingsButtonKey, CaptionOpen },
            { "reallife_meta_guide_button", CaptionOpen },
            { AboutKey, CaptionOpen },
            { "reallife_meta_welcome_button", CaptionOpen },
            { "reallife_meta_preset_button_relaxed", "reallife_opt_caption_apply" },
            { "reallife_meta_preset_button_standard", "reallife_opt_caption_apply" },
            { "reallife_meta_preset_button_realistic", "reallife_opt_caption_apply" },
            { "reallife_meta_preset_button_hardcore", "reallife_opt_caption_apply" },
            { "reallife_meta_reset_button", "reallife_opt_caption_reset" },
            { "reallife_meta_resetdata_button", "reallife_opt_caption_reset" },
            { "reallife_keys_reset", "reallife_opt_caption_reset" },
            { "reallife_meta_export_button", "reallife_opt_caption_export" },
            { "reallife_meta_import_button", "reallife_opt_caption_import" },
            { "reallife_meta_bug_button", "reallife_opt_caption_report" },
            { "reallife_meta_remove_button", "reallife_opt_caption_prepare" },
            { "reallife_meta_repair_button", "reallife_opt_caption_repair" },
            { "reallife_meta_check_button", "reallife_opt_caption_check" },
            { "reallife_car_measure", "reallife_opt_caption_measure" },
        };

        /// <summary>The text on one button: a text the game keeps translated, or a plain one that is written here.</summary>
        private sealed class Caption
        {
            public TextLocalizationComponent Localised;
            public TMP_Text Plain;
            public string Key;

            public void Set(string key)
            {
                if (key == null || key == Key)
                {
                    return;
                }
                Key = key;
                Write();
            }

            public void Write()
            {
                if (Localised != null)
                {
                    Localised.Key = Key;
                }
                else if (Plain != null)
                {
                    Plain.text = Key.GetLocalization();
                }
            }
        }

        private static readonly List<Caption> PlainCaptions = new List<Caption>();   // of this drawing: written again when the language changes
        private static readonly HashSet<GameObject> OwnCaption = new HashSet<GameObject>();   // rows whose word follows a state (sections)
        private static int _captionsFrom;
        private static bool _captionsWarned;
        private static bool _captionsFailed;

        // The first of the mod's rows is being made: the rows of the last drawing are on their way out.
        private static void CaptionsBegin(Transform parent)
        {
            PlainCaptions.Clear();
            OwnCaption.Clear();
            _captionsFrom = parent != null ? parent.childCount : 0;
            foreach (Section section in Sections)
            {
                section.Caption = null;
            }
        }

        // The last of the mod's rows has been made: every button among them that has no word yet gets one.
        private static void CaptionsEnd(Transform parent)
        {
            if (_captionsFailed || parent == null)
            {
                return;
            }
            try
            {
                for (int i = _captionsFrom; i < parent.childCount; i++)
                {
                    GameObject row = parent.GetChild(i).gameObject;
                    if (OwnCaption.Contains(row) || row.GetComponent<ModOptionsButtonControl>() == null)
                    {
                        continue;
                    }
                    TextLocalizationComponent label = TextOf(row);
                    string labelKey = label != null ? label.Key : null;
                    string key;
                    if (labelKey == null || !CaptionByLabel.TryGetValue(labelKey, out key))
                    {
                        key = Keys.IsActionLabel(labelKey) ? CaptionChange : CaptionRun;
                    }
                    Caption caption = CaptionOf(row);
                    if (caption != null)
                    {
                        caption.Set(key);
                    }
                }
            }
            catch (Exception e)
            {
                CaptionsFailed(e);
            }
        }

        // The text on a row's button: the first text under the button that is not the row's label.
        private static Caption CaptionOf(GameObject row)
        {
            if (row == null || _captionsFailed)
            {
                return null;
            }
            TextLocalizationComponent label = TextOf(row);
            TMP_Text labelText = label != null ? label.TextContainer : null;
            Button button = row.GetComponentInChildren<Button>(true);
            TMP_Text found = null;
            if (button != null)
            {
                foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text != labelText)
                    {
                        found = text;
                        break;
                    }
                }
            }
            if (found == null)
            {
                if (!_captionsWarned)
                {
                    _captionsWarned = true;
                    MetaLog.Warn("a button on the options page has no text of its own to write on; the buttons are left as the game draws them.");
                }
                return null;
            }
            var caption = new Caption { Plain = found, Localised = found.GetComponent<TextLocalizationComponent>() };
            if (caption.Localised == null)
            {
                PlainCaptions.Add(caption);
            }
            return caption;
        }

        // A section's button, as it is made: its word follows whether the section is open.
        private static void CaptionSection(Section section)
        {
            try
            {
                section.Caption = CaptionOf(section.Header);
                if (section.Header != null)
                {
                    OwnCaption.Add(section.Header);
                }
            }
            catch (Exception e)
            {
                CaptionsFailed(e);
            }
        }

        private static void CaptionState(Section section, string key)
        {
            if (section.Caption == null || _captionsFailed)
            {
                return;
            }
            try
            {
                section.Caption.Set(key);
            }
            catch (Exception e)
            {
                CaptionsFailed(e);
            }
        }

        // The language was changed while the page is open: the game sees to its own texts, the plain ones are written again here.
        private static void CaptionsLanguage()
        {
            if (_captionsFailed)
            {
                return;
            }
            try
            {
                foreach (Caption caption in PlainCaptions)
                {
                    if (caption.Plain != null && caption.Key != null)
                    {
                        caption.Write();
                    }
                }
            }
            catch (Exception e)
            {
                CaptionsFailed(e);
            }
        }

        private static void CaptionsFailed(Exception e)
        {
            if (!_captionsFailed)
            {
                _captionsFailed = true;
                MetaLog.Error(e);
                MetaLog.Warn("the words on the options page's buttons could not be written; the buttons work as before.");
            }
        }

        /// <summary>
        /// The page as it was built, for when it could not be put in order: the same rows, with a number beside
        /// every slider and a word on every button.
        /// </summary>
        private static ModOptions AsBuilt(ModOptions original)
        {
            try
            {
                var page = new ModOptions();
                bool first = true;
                foreach (ModOption option in original.Options)
                {
                    ModOption row = option;
                    if (option is SliderOption slider && string.IsNullOrEmpty(slider.ValueLabelKey))
                    {
                        row = new SliderOption(slider.Id, slider.Label, slider.Min, slider.Max, slider.DefaultValue, slider.OnValueChanged, SettingsTable.SliderValue);
                    }
                    page.AddCustom(row);
                    if (first)
                    {
                        // After the mod's first row (its first header), so that it counts the rows of this drawing only.
                        first = false;
                        page.AddCustom(new RowTap(CaptionsBegin));
                    }
                }
                page.AddCustom(new RowTap(CaptionsEnd));
                return page;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return original;
            }
        }
    }
}
