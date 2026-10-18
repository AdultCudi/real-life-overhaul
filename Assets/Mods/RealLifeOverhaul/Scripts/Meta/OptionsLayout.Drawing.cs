using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The options page's part for each drawing: which object is which row, what is greyed and counted, and what is open.</summary>
    public static partial class OptionsLayout
    {
        private const float BlockedAlpha = 0.5f;   // a label beside a control that cannot be used, when the control itself does not say how faint

        private static readonly Dictionary<string, string> OpenChild = new Dictionary<string, string>(StringComparer.Ordinal)   // parent id: the one child that is open (until the game is closed)
        {
            { string.Empty, "g:" + OptionsTree.StartId }
        };

        private static readonly Dictionary<Feature, string> AsideNow = new Dictionary<Feature, string>();   // of this drawing
        private static readonly HashSet<GameObject> Absent = new HashSet<GameObject>();   // rows of this drawing that are not on the page (Feature.Present said no)
        private static readonly Func<string, bool> IsOnNow = IsOn;
        private static int _drawFrame = -1;        // the frame the page is being drawn in; -1 once it is drawn
        private static long _drawStarted;
        private static string _countFormat;
        private static string _needsFormat;

        private static bool Drawing => _drawFrame == Time.frameCount;

        // The first of the mod's rows is being made: the rows of the last drawing are on their way out, and nothing may switch them back on.
        private static void DrawBegin(Transform parent)
        {
            _drawFrame = Time.frameCount;
            _drawStarted = Stopwatch.GetTimestamp();
            foreach (Section section in Sections)
            {
                section.Header = null;
                section.HeaderText = null;
                section.Shown = null;
                section.Objects.Clear();
                section.Matched = false;
                section.Dressed = false;
                foreach (Row row in section.Infos)
                {
                    row.Reason = null;
                    row.Away = false;
                }
            }
            Absent.Clear();
            AsideNow.Clear();
            OtherMods.Look(MetaLog.Sink);   // a row may be there only with another author's mod: looked for when the page is drawn
            _countFormat = "reallife_opt_count".GetLocalization();
            _needsFormat = "reallife_opt_needs".GetLocalization();
            CaptionsBegin(parent);
            SearchDrawn(parent);
        }

        // A section's rows have been made: which object is which row, and which rows are not on the page this time.
        private static void RowsDrawn(Section section, Transform parent)
        {
            section.Objects.Clear();
            for (int i = section.Start; i < parent.childCount; i++)
            {
                section.Objects.Add(parent.GetChild(i).gameObject);
            }
            // One object per row is what the game makes. Should a row have made none or several, the section still opens and closes as a whole.
            section.Matched = section.Objects.Count == section.Rows.Count;
            if (!section.Matched)
            {
                return;
            }
            for (int i = 0; i < section.Infos.Count; i++)
            {
                Row row = section.Infos[i];
                if (row.Present != null && !row.Present())
                {
                    row.Away = true;
                    Absent.Add(section.Objects[i]);
                    section.Objects[i].SetActive(false);
                }
            }
        }

        // The last of the mod's rows has been made: every switch has reported, so what is greyed, the counts and what is open can be set.
        private static void DrawEnd(Transform parent)
        {
            CaptionsEnd(parent);
            _drawFrame = -1;
            try
            {
                foreach (Row row in Needy)
                {
                    Block(row);
                }
                foreach (Section top in Top)
                {
                    Count(top);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);   // the groups must still close what is not open: a page with every row shown is the wall this class is there to prevent
            }
            foreach (Section top in Top)
            {
                Apply(top);
            }
            if (Dev.Verbose)
            {
                double drawMs = (Stopwatch.GetTimestamp() - _drawStarted) * 1000.0 / Stopwatch.Frequency;
                int drawn = 0;
                int unmatched = 0;
                foreach (Section section in Sections)
                {
                    drawn += section.Objects.Count;
                    unmatched += section.Header != null && !section.Matched ? 1 : 0;
                }
                bool pass = _otherRows == 0 && unmatched == 0 && _plan != null && _plan.Missing.Count == 0;
                MetaLog.Info("Options check (page drawn): " + Top.Count + " groups, " + (_plan != null ? _plan.Sections.Count : 0) + " sections, "
                             + (_plan != null ? _plan.Placed : 0) + " rows placed, " + _otherRows + " in Other (wanted 0), " + drawn + " row objects, "
                             + unmatched + " sections whose rows did not match their objects (wanted 0), test tools " + (_testsShown ? "on the page" : "left off")
                             + ", put in order in " + _buildMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms, drawn in "
                             + drawMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms: " + (pass ? "PASS" : "FAIL") + ".");
            }
        }

        private static GameObject Last(Transform parent)
        {
            return parent != null && parent.childCount > 0 ? parent.GetChild(parent.childCount - 1).gameObject : null;
        }

        private static TextLocalizationComponent TextOf(GameObject row)
        {
            return row != null ? row.GetComponentInChildren<TextLocalizationComponent>(true) : null;
        }

        // The text of a row that shows its label (a slider has a second one for its number).
        private static TextLocalizationComponent LabelOf(GameObject row, string labelKey)
        {
            if (row == null)
            {
                return null;
            }
            TextLocalizationComponent first = null;
            foreach (TextLocalizationComponent text in row.GetComponentsInChildren<TextLocalizationComponent>(true))
            {
                if (text.Key == labelKey)
                {
                    return text;
                }
                first = first ?? text;
            }
            return first;
        }

        private static bool IsOn(string optionId)
        {
            if (On.TryGetValue(optionId, out bool on))
            {
                return on;
            }
            OptionInfo info = SettingsTable.Get(optionId);
            return info == null || SettingsTable.Current(info) != 0;   // an option the page does not know keeps nothing from working
        }

        // A switch reported: with its saved value while the page is drawn, or because it was clicked.
        private static void Switched(Row row, bool on)
        {
            try
            {
                row.On = on;
                if (row.Id != null)
                {
                    On[row.Id] = on;
                }
                if (Drawing || _countFormat == null)
                {
                    return;   // DrawEnd counts and greys everything once, when all have reported; or the page was never drawn (a change from the Settings app)
                }
                if (row.Needed)
                {
                    foreach (Row needy in Needy)
                    {
                        Block(needy);
                    }
                }
                for (Section section = row.Section; section != null; section = section.Parent)
                {
                    CountOwn(section);
                    Label(section);
                }
                if (Top.Count > 0 && Top[0].IsStart)
                {
                    CountOwn(Top[0]);   // the preset in use may be another one now
                    Label(Top[0]);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // Why a row does nothing just now, or null: another mod has what it steers, or something it needs is off.
        private static string ReasonFor(Row row)
        {
            if (row.AsideOf != null)
            {
                if (!AsideNow.TryGetValue(row.AsideOf, out string aside))
                {
                    aside = row.AsideOf.AsideWhy();
                    AsideNow[row.AsideOf] = aside;
                }
                if (aside != null)
                {
                    return aside;
                }
            }
            if (!row.HasNeeds)
            {
                return null;
            }
            string blocker = OptionsPlan.Blocker(row.Id, IsOnNow);
            if (blocker == null)
            {
                return null;
            }
            string label = ById.TryGetValue(blocker, out Row needed) ? needed.LabelKey.GetLocalization() : blocker;
            return _needsFormat.Replace("{option}", label);
        }

        // Draws a row as the game draws a control that cannot be used, with the reason after its label; or puts it back.
        private static void Block(Row row)
        {
            Section section = row.Section;
            if (!section.Matched || row.Away)
            {
                return;
            }
            string reason = ReasonFor(row);
            if (reason == row.Reason)
            {
                return;
            }
            row.Reason = reason;
            GameObject item = section.Objects[row.Index];
            if (item == null)
            {
                return;
            }
            float faint = BlockedAlpha;
            foreach (UnityEngine.UI.Selectable control in item.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
            {
                control.interactable = reason == null;
                faint = control.colors.disabledColor.a;
            }
            TextLocalizationComponent label = LabelOf(item, row.LabelKey);
            if (label != null)
            {
                label.Suffix = reason == null ? string.Empty : "   (" + reason + ")";
                if (label.TextContainer != null)
                {
                    label.TextContainer.alpha = reason == null ? 1f : Mathf.Clamp(faint, 0.35f, 0.8f);
                }
            }
        }

        // Counts a section's switches and those of what is in it, and says so after its name.
        private static void Count(Section section)
        {
            foreach (Section child in section.Children)
            {
                Count(child);
            }
            CountOwn(section);
        }

        private static void CountOwn(Section section)
        {
            int on = 0;
            int all = 0;
            foreach (Row row in section.Infos)
            {
                if (row.IsSwitch && !row.Away)
                {
                    all++;
                    on += row.On ? 1 : 0;
                }
            }
            int buttons = section.Infos.Count;
            foreach (Section child in section.Children)
            {
                if (child.IsTests)
                {
                    continue;   // the test tools are counted where they stand
                }
                on += child.SwitchesOn;
                all += child.Switches;
                buttons += child.Switches;   // (a group of test buttons keeps its number of buttons there)
            }
            section.SwitchesOn = on;
            section.Switches = section.InTests ? buttons : all;
            if (section.InTests)
            {
                section.Suffix = "   (" + section.Switches + ")";
            }
            else if (section.IsStart)
            {
                section.Suffix = ":  " + Presets.CurrentName() + (Guard.AnyOff ? "   (" + Guard.Note(null) + ")" : string.Empty);
            }
            else
            {
                section.Suffix = all == 0 ? string.Empty : _countFormat.Replace("{on}", on.ToString()).Replace("{total}", all.ToString());
                string note = GuardNote(section);
                if (note != null)
                {
                    section.Suffix += "   (" + note + ")";   // a part of it is off after a game update, or it was switched off after its faults
                }
            }
        }

        // Core/Guard's note for the first of a section's features that has a part off, or null.
        private static string GuardNote(Section section)
        {
            if (section.Features == null)
            {
                return null;
            }
            foreach (string feature in section.Features)
            {
                string note = Guard.Note(feature);
                if (note != null)
                {
                    return note;
                }
            }
            return null;
        }

        // The text after the name on a section's button, written only when it is another one.
        private static void Label(Section section)
        {
            if (section.HeaderText != null && section.Shown != section.Suffix)
            {
                section.Shown = section.Suffix;
                section.HeaderText.Suffix = section.Suffix;
            }
        }

        // The game's own tooltip on every row of a section that has a sentence: made the first time the section is shown in a drawing.
        private static void Dress(Section section)
        {
            if (section.Dressed || !section.Matched)
            {
                return;
            }
            section.Dressed = true;
            for (int i = 0; i < section.Infos.Count; i++)
            {
                Row row = section.Infos[i];
                GameObject item = section.Objects[i];
                if (row.TipKey == null || item == null || item.GetComponent<BasicTooltip>() != null)
                {
                    continue;
                }
                BasicTooltip tip = item.AddComponent<BasicTooltip>();
                tip.descriptionKey = row.TipKey;
            }
        }

        private static void Show(GameObject row, bool on)
        {
            on = on && !Absent.Contains(row);   // a row that is not on the page stays away, whatever opens or is found
            if (row != null && row.activeSelf != on)
            {
                row.SetActive(on);
            }
        }

        private static string ParentId(Section section)
        {
            return section.Parent != null ? section.Parent.Id : string.Empty;
        }

        private static bool IsOpen(Section section)
        {
            return OpenChild.TryGetValue(ParentId(section), out string open) && open == section.Id;
        }

        // One group is open at a time, and one section in it: opening one closes the one that was.
        private static void Toggle(Section section)
        {
            try
            {
                if (Searching)
                {
                    return;   // what is shown is what was found; what is open and closed stays as it was left
                }
                string parent = ParentId(section);
                bool open = !IsOpen(section);
                bool closedAbove = false;
                if (open && OpenChild.TryGetValue(parent, out string was))
                {
                    foreach (Section beside in section.Parent != null ? section.Parent.Children : Top)
                    {
                        if (beside == section)
                        {
                            break;
                        }
                        closedAbove |= beside.Id == was;
                    }
                }
                if (open)
                {
                    OpenChild[parent] = section.Id;
                }
                else
                {
                    OpenChild.Remove(parent);
                }
                foreach (Section top in Top)
                {
                    Apply(top);
                }
                if (closedAbove && _box != null)
                {
                    _box.ScrollTo(section.Header);   // what closed above it took its rows away: the one just opened goes to the top of the view
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // Is the section's own button to be seen: a group always, anything else while what it stands in is open.
        private static bool Visible(Section section)
        {
            return section.Parent == null || (Visible(section.Parent) && IsOpen(section.Parent));
        }

        // Shows or hides a section's rows, and what is inside it. The objects are the game's; one that has gone (the page was closed) is skipped.
        private static void Apply(Section section)
        {
            if (Searching)
            {
                return;   // the search decides until the box is empty again, and then calls this
            }
            bool visible = Visible(section);
            bool open = visible && IsOpen(section);
            Show(section.Header, visible);
            Label(section);
            CaptionState(section, open ? CaptionClose : CaptionOpen);
            if (open)
            {
                Dress(section);
            }
            foreach (GameObject row in section.Objects)
            {
                Show(row, open);
            }
            foreach (Section child in section.Children)
            {
                Apply(child);
            }
        }

        /// <summary>After "Show test tools" changed: the page with them, or without, is registered in place of the other on the next frame.</summary>
        public static void ApplyAll()
        {
            if (_plain == null || _modId == null || Settings.ShowTestTools == _testsShown)
            {
                return;
            }
            Refresh();
        }

        /// <summary>Option values changed from outside the page (a preset, an import, a reset): a registered page is drawn again next frame, the game's own way of showing saved values.</summary>
        public static void Refresh()
        {
            if (_plain == null || _modId == null || !OptionsService.RegisteredEntries.ContainsKey(_modId))
            {
                return;
            }
            MetaUi.Instance.NextFrame(() =>
            {
                if (_plain != null && OptionsService.RegisteredEntries.ContainsKey(_modId))
                {
                    _testsShown = Settings.ShowTestTools;
                    OptionsService.Register(_modId, Guard.Rows(_testsShown ? _withTests : _plain));
                }
            });
        }

        // The game's own "reset all options" clears the saved values and draws the page again, every row reporting the shipped
        // default (SettingsTable.WithTrueDefault); a frame later Settings is read again, so that Settings.Changed is heard.
        private static void GameReset()
        {
            try
            {
                MetaUi.Instance.NextFrame(() =>
                {
                    Settings.Reload();
                    ApplyAll();
                });
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
