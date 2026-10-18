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
    /// <summary>The options page's own buttons: the presets, the reset, the settings file, the layout test and the set-up check.</summary>
    public static partial class OptionsLayout
    {
        // A preset is asked first, with the game's own question, which says how many options it will change.
        private static void AskPreset(Intensity intensity)
        {
            string about;
            switch (intensity)
            {
                case Intensity.Relaxed: about = "reallife_meta_preset_about_relaxed"; break;
                case Intensity.Realistic: about = "reallife_meta_preset_about_realistic"; break;
                case Intensity.Hardcore: about = "reallife_meta_preset_about_hardcore"; break;
                default: about = "reallife_meta_preset_about_standard"; break;
            }
            int switches;
            int changes = Presets.CountChanges(intensity, out switches);
            string body = "reallife_opt_ask_preset".GetLocalization()
                .Replace("{about}", about.GetLocalization())
                .Replace("{count}", changes.ToString())
                .Replace("{switches}", switches.ToString());
            OptionsNotice.Ask(Presets.NameKey(intensity), body, "reallife_meta_preset_yes", false, () => OptionsNotice.Tell(Presets.NameKey(intensity), Presets.Apply(intensity)));
        }

        private static void AskReset()
        {
            OptionsNotice.Ask("reallife_meta_reset_title", "reallife_meta_reset_explain".GetLocalization(), "reallife_meta_reset_yes", true,
                () => OptionsNotice.Tell("reallife_meta_reset_title", Presets.ResetAll()));
        }

        private static void AskImport()
        {
            OptionsNotice.Ask("reallife_meta_import_title",
                "reallife_meta_import_explain".GetLocalization().Replace("{file}", System.IO.Path.Combine(Presets.Folder, Presets.SettingsFile)),
                "reallife_meta_import_yes", false, () => OptionsNotice.Tell("reallife_meta_import_title", Presets.Import()));
        }

        private static void ExportNow()
        {
            try
            {
                string path = Presets.Export(Presets.SettingsFile);
                MetaLog.Info("settings written to " + path);
                OptionsNotice.Tell("reallife_meta_export_title", "reallife_meta_export_done".GetLocalization().Replace("{file}", path));
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                OptionsNotice.Tell("reallife_meta_export_title", "reallife_meta_export_failed".GetLocalization().Replace("{error}", e.Message));
            }
        }

        /// <summary>The whole page as it is arranged, one line per group, section and row, for Player.log.</summary>
        public static string Describe()
        {
            var text = new StringBuilder(8192);
            text.Append("the options page: ").Append(Top.Count).Append(" groups, ").Append(_plan != null ? _plan.Sections.Count : 0).Append(" sections, ")
                .Append(_plan != null ? _plan.Placed : 0).Append(" rows placed, ").Append(_otherRows).Append(" in Other, ").Append(_testRows).Append(" test tools\n");
            foreach (Section top in Top)
            {
                Describe(text, top, 0);
            }
            return text.ToString();
        }

        private static void Describe(StringBuilder text, Section section, int depth)
        {
            text.Append(' ', depth * 2).Append(depth == 0 ? "# " : "+ ").Append(section.TitleKey.GetLocalization()).Append("   [").Append(section.Id).Append(']');
            if (IsOpen(section))
            {
                text.Append(" (open)");
            }
            text.Append('\n');
            foreach (Row row in section.Infos)
            {
                text.Append(' ', depth * 2 + 2).Append("- ").Append(row.LabelKey.GetLocalization());
                if (row.Id != null)
                {
                    text.Append("   [").Append(row.Id).Append(row.IsSwitch ? (row.On ? ": on" : ": off") : string.Empty).Append(']');
                    string[] needs = OptionsPlan.NeedsOf(row.Id);
                    if (needs.Length > 0)
                    {
                        text.Append(" needs ").Append(string.Join(", ", needs));
                    }
                }
                text.Append('\n');
            }
            foreach (Section child in section.Children)
            {
                Describe(text, child, depth + 1);
            }
        }

        // The test button: the layout goes to Player.log whether or not "Developer diagnostics" is on, since it was asked for.
        private static void TestLayout()
        {
            try
            {
                UnityEngine.Debug.Log("[RealLifeOverhaul] Meta: " + Describe());
                OptionsNotice.Tell("reallife_opt_test_layout", "reallife_opt_test_layout_done".GetLocalization());
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        /// <summary>The "Check the mod's set-up" button: anything in the registry, the presets, the page or the Guide that does not fit together, for whoever merges a feature.</summary>
        public static void CheckSetup()
        {
            var problems = new List<string>();
            int rules = 0;
            try
            {
                foreach (Feature feature in Features.All)
                {
                    foreach (string key in new[] { feature.TitleKey, feature.AboutKey, feature.HelpKey })
                    {
                        if (key != null && key.GetLocalization() == key)
                        {
                            problems.Add(feature.Id + ": the text " + key + " is not in the locale file");
                        }
                    }
                    if (feature.Auto)
                    {
                        problems.Add(feature.Id + ": this options group is not registered (it works, but has no guide page, presets or clean-up)");
                    }
                    else if (feature.Id != OptionsTree.TestsId && feature.Id != OptionsTree.OtherId && OptionsTree.SectionOfFeature(feature.Id) == null)
                    {
                        problems.Add(feature.Id + ": no section of Meta/OptionsTree.cs stands for this feature (its test tools are under Other)");
                    }
                    foreach (PresetRule rule in feature.Presets)
                    {
                        rules++;
                        OptionInfo info = SettingsTable.Get(rule.OptionId);
                        if (info == null || !info.HasDefault)
                        {
                            problems.Add(feature.Id + ": the preset names the option " + rule.OptionId + ", which has no known default");
                            continue;
                        }
                        if (info.LabelKey == null)
                        {
                            problems.Add(feature.Id + ": the option " + rule.OptionId + " is in the presets but not on the options page");
                        }
                        foreach (int value in new[] { rule.Relaxed, rule.Realistic, rule.Hardcore })
                        {
                            if (SettingsTable.Clamp(info, value) != value)
                            {
                                problems.Add(feature.Id + ": the preset value " + value + " for " + rule.OptionId + " is outside " + info.Min + ".." + info.Max);
                            }
                        }
                    }
                    foreach (string id in feature.Options)
                    {
                        if (SettingsTable.Get(id) == null)
                        {
                            problems.Add(feature.Id + ": owns the option " + id + ", which does not exist");
                        }
                    }
                    foreach (string id in feature.ReloadOptions)
                    {
                        if (SettingsTable.Get(id) == null)
                        {
                            problems.Add(feature.Id + ": marks the option " + id + " as needing a reload, which does not exist");
                        }
                    }
                    foreach (TestAction test in feature.Tests)
                    {
                        if (test.Run == null)
                        {
                            problems.Add(feature.Id + ": names the test button " + test.LabelKey + ", which is not on the page and has no action");
                        }
                    }
                    foreach (string contact in feature.ContactIds)
                    {
                        if (contact.GetLocalization() == contact)
                        {
                            problems.Add(feature.Id + ": the contact " + contact + " has no name in the locale file");
                        }
                    }
                }
                foreach (OptionInfo info in SettingsTable.All)
                {
                    if (info.LabelKey != null && info.Owner != null && info.Owner.Auto)
                    {
                        continue;   // already said once for its group
                    }
                    if (info.Field != null && info.LabelKey == null)
                    {
                        problems.Add("the option " + info.Id + " is in Settings but on no row of the options page");
                    }
                }
                if (_plan != null)
                {
                    foreach (Section section in Sections)
                    {
                        foreach (Row row in section.Infos)
                        {
                            if (section.Id == "s:" + OptionsTree.OtherId)
                            {
                                problems.Add("the row " + (row.Id ?? row.LabelKey) + " is in no section of Meta/OptionsTree.cs");
                            }
                            if (row.TipKey != null && row.TipKey.GetLocalization() == row.TipKey)
                            {
                                problems.Add("the option " + row.Id + " has no sentence " + row.TipKey + " in the locale file");
                            }
                        }
                    }
                    foreach (string missing in _plan.Missing)
                    {
                        problems.Add("Meta/OptionsTree.cs names the row " + missing + ", which the page does not have");
                    }
                    string cycle = OptionsPlan.FindCycle();
                    if (cycle != null)
                    {
                        problems.Add("options that need each other in a circle: " + cycle);
                    }
                }
                Guide.Check(problems);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                problems.Add("the check itself failed: " + e.Message);
            }

            string summary = "reallife_meta_check_summary".GetLocalization()
                .Replace("{features}", Features.All.Count.ToString())
                .Replace("{options}", SettingsTable.All.Count.ToString())
                .Replace("{rules}", rules.ToString())
                .Replace("{problems}", problems.Count.ToString());
            foreach (string problem in problems)
            {
                MetaLog.Warn("set-up check: " + problem);
            }
            MetaLog.Info("set-up check: " + problems.Count + " problem(s).");
            OptionsNotice.Tell("reallife_meta_check_title", summary);
        }
    }
}
