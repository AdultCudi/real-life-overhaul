using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The realism presets, "reset everything" and the settings file: saved option values, as the features declare them (Feature.Preset).</summary>
    public static class Presets
    {
        public const string SettingsFile = "settings.txt";
        public const string BeforePresetFile = "settings_before_preset.txt";

        private static readonly string[] NameKeys =
        {
            "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore"
        };

        public static string NameKey(Intensity intensity)
        {
            return NameKeys[(int)intensity];
        }

        /// <summary>The mod's own folder under the game's data folder (next to Player.log): settings files and reports.</summary>
        public static string Folder
        {
            get
            {
                string folder = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        /// <summary>What a preset sets an option to, or false if the presets leave it alone.</summary>
        public static bool TryValue(OptionInfo info, Intensity intensity, out int value)
        {
            value = 0;
            PresetRule rule = Features.RuleFor(info.Id);
            if (rule == null || !info.HasDefault)
            {
                return false;
            }
            switch (intensity)
            {
                case Intensity.Relaxed: value = rule.Relaxed; break;
                case Intensity.Realistic: value = rule.Realistic; break;
                case Intensity.Hardcore: value = rule.Hardcore; break;
                default: value = info.Default; break;
            }
            value = SettingsTable.Clamp(info, value);
            return true;
        }

        /// <summary>The preset the options match now, or null when the player has changed something ("Custom").</summary>
        public static Intensity? Detect()
        {
            for (int i = 0; i < NameKeys.Length; i++)
            {
                bool all = true;
                bool any = false;
                foreach (OptionInfo info in SettingsTable.All)
                {
                    if (TryValue(info, (Intensity)i, out int value))
                    {
                        any = true;
                        if (SettingsTable.Current(info) != value)
                        {
                            all = false;
                            break;
                        }
                    }
                }
                if (all && any)
                {
                    return (Intensity)i;
                }
            }
            return null;
        }

        /// <summary>The name of what is set now: one of the four, or "Custom".</summary>
        public static string CurrentName()
        {
            Intensity? now = Detect();
            return (now.HasValue ? NameKeys[(int)now.Value] : "reallife_meta_preset_custom").GetLocalization();
        }

        /// <summary>How many options a preset would change now, and how many of those are switches: for the question before it is applied.</summary>
        public static int CountChanges(Intensity intensity, out int switches)
        {
            int changed = 0;
            switches = 0;
            if (Settings.ModId == null)
            {
                return 0;
            }
            foreach (OptionInfo info in SettingsTable.All)
            {
                if (TryValue(info, intensity, out int value) && SettingsTable.Current(info) != value)
                {
                    changed++;
                    switches += info.Type == OptionType.Toggle ? 1 : 0;
                }
            }
            return changed;
        }

        /// <summary>Sets every option the presets cover, first saving the settings to a file so that a press by mistake can be undone with Import. Returns the player's text.</summary>
        public static string Apply(Intensity intensity)
        {
            if (Settings.ModId == null)
            {
                return "reallife_meta_not_ready".GetLocalization();
            }
            string saved = SaveBefore();
            int changed = 0;
            var reload = new List<string>();
            foreach (OptionInfo info in SettingsTable.All)
            {
                if (!TryValue(info, intensity, out int value) || SettingsTable.Current(info) == value)
                {
                    continue;
                }
                SettingsTable.Write(info, value);
                changed++;
                if (Features.NeedsReload(info.Id))
                {
                    reload.Add(Label(info));
                }
            }
            UnityEngine.PlayerPrefs.Save();
            Settings.Reload();
            OptionsLayout.Refresh();
            MetaLog.Info("preset " + intensity + " applied, " + changed + " option(s) changed, " + reload.Count + " need a reload.");

            var text = new StringBuilder();
            text.Append("reallife_meta_preset_applied".GetLocalization()
                .Replace("{preset}", NameKeys[(int)intensity].GetLocalization())
                .Replace("{count}", changed.ToString(CultureInfo.InvariantCulture)));
            AppendReload(text, reload);
            if (saved != null && changed > 0)
            {
                text.Append("\n\n").Append("reallife_meta_preset_undo".GetLocalization().Replace("{file}", saved));
            }
            return text.ToString();
        }

        /// <summary>Every option back to the value the mod ships with, every key back on its own.</summary>
        public static string ResetAll()
        {
            if (Settings.ModId == null)
            {
                return "reallife_meta_not_ready".GetLocalization();
            }
            string saved = SaveBefore();
            int changed = 0;
            var reload = new List<string>();
            foreach (OptionInfo info in SettingsTable.All)
            {
                string key = Settings.PrefsKey(info.Id);
                if (!info.HasDefault)
                {
                    UnityEngine.PlayerPrefs.DeleteKey(key);   // kept elsewhere with a default of its own: no saved value means that default
                    continue;
                }
                if (SettingsTable.Current(info) != info.Default)
                {
                    changed++;
                    if (Features.NeedsReload(info.Id))
                    {
                        reload.Add(Label(info));
                    }
                }
                // Written, not deleted: Settings.Load falls back to the value in memory, which is not the default any more.
                SettingsTable.Write(info, info.Default);
            }
            Keys.ResetAll();
            UnityEngine.PlayerPrefs.Save();
            Settings.Reload();
            OptionsLayout.Refresh();
            MetaLog.Info("every option reset to its default, " + changed + " changed.");

            var text = new StringBuilder();
            text.Append("reallife_meta_reset_done".GetLocalization().Replace("{count}", changed.ToString(CultureInfo.InvariantCulture)));
            AppendReload(text, reload);
            if (saved != null)
            {
                text.Append("\n\n").Append("reallife_meta_preset_undo".GetLocalization().Replace("{file}", saved));
            }
            return text.ToString();
        }

        // The settings as they were, for Import to undo a preset or a reset with; null if they could not be written.
        private static string SaveBefore()
        {
            try
            {
                return Export(BeforePresetFile);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return null;
            }
        }

        private static void AppendReload(StringBuilder text, List<string> reload)
        {
            if (reload.Count == 0)
            {
                text.Append("\n\n").Append("reallife_meta_preset_live".GetLocalization());
                return;
            }
            text.Append("\n\n").Append("reallife_meta_preset_reload".GetLocalization());
            foreach (string label in reload)
            {
                text.Append("\n  - ").Append(label);
            }
        }

        internal static string Label(OptionInfo info)
        {
            return info.LabelKey != null ? info.LabelKey.GetLocalization() : info.Id;
        }

        /// <summary>Writes every option and every changed key as "id=value" lines and returns the path; Import reads settings.txt in the same folder.</summary>
        public static string Export(string fileName)
        {
            string path = Path.Combine(Folder, fileName);
            File.WriteAllText(path, ExportText(), new UTF8Encoding(false));
            return path;
        }

        /// <summary>What <see cref="Export"/> writes, as text: a bug report (BugReport.cs) puts the same file in its folder.</summary>
        public static string ExportText()
        {
            var text = new StringBuilder();
            text.Append("# Real Life Overhaul ").Append(Changelog.Version).Append(" settings. One option per line: id=value.\n");
            text.Append("# Toggles are 0 (off) or 1 (on). Lines starting with # are ignored, unknown ids are skipped.\n");
            text.Append("# To use someone else's set-up, put their file here as ").Append(SettingsFile).Append(" and press Import on the options page.\n");
            Intensity? preset = Detect();
            text.Append("# Preset when written: ").Append(preset.HasValue ? preset.Value.ToString() : "Custom").Append('\n');
            foreach (OptionInfo info in SettingsTable.All)
            {
                text.Append(info.Id).Append('=').Append(SettingsTable.Current(info).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            for (int i = 0; i < Keys.Count; i++)
            {
                string id = Keys.PrefsId((KeyAction)i);
                int saved = UnityEngine.PlayerPrefs.GetInt(Settings.PrefsKey(id), -1);
                if (saved > 0)
                {
                    text.Append(id).Append('=').Append(saved.ToString(CultureInfo.InvariantCulture)).Append('\n');   // only keys the player has changed
                }
            }
            return text.ToString();
        }

        /// <summary>Reads settings.txt from the mod's folder and applies what it knows. Returns the text to show the player.</summary>
        public static string Import()
        {
            if (Settings.ModId == null)
            {
                return "reallife_meta_not_ready".GetLocalization();
            }
            string path = Path.Combine(Folder, SettingsFile);
            if (!File.Exists(path))
            {
                return "reallife_meta_import_missing".GetLocalization().Replace("{file}", path);
            }
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
                Export(BeforePresetFile);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return "reallife_meta_import_failed".GetLocalization().Replace("{file}", path).Replace("{error}", e.Message);
            }

            int applied = 0, skipped = 0;
            var reload = new List<string>();
            var keyIds = new HashSet<string>();
            for (int i = 0; i < Keys.Count; i++)
            {
                keyIds.Add(Keys.PrefsId((KeyAction)i));
            }
            bool keysGiven = false;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals <= 0 || !int.TryParse(line.Substring(equals + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    skipped++;
                    continue;
                }
                string id = line.Substring(0, equals).Trim();
                if (keyIds.Contains(id))
                {
                    UnityEngine.PlayerPrefs.SetInt(Settings.PrefsKey(id), value);   // Keys.Load refuses a value that is no key
                    keysGiven = true;
                    applied++;
                    continue;
                }
                OptionInfo info = SettingsTable.Get(id);
                if (info == null)
                {
                    skipped++;
                    continue;
                }
                value = SettingsTable.Clamp(info, value);
                if (SettingsTable.Current(info) != value && Features.NeedsReload(info.Id))
                {
                    reload.Add(Label(info));
                }
                SettingsTable.Write(info, value);
                applied++;
            }
            UnityEngine.PlayerPrefs.Save();
            Settings.Reload();
            OptionsLayout.Refresh();
            MetaLog.Info("settings imported from " + path + ": " + applied + " applied, " + skipped + " skipped" + (keysGiven ? ", keys included." : "."));

            var text = new StringBuilder();
            text.Append("reallife_meta_import_done".GetLocalization()
                .Replace("{count}", applied.ToString(CultureInfo.InvariantCulture))
                .Replace("{skipped}", skipped.ToString(CultureInfo.InvariantCulture))
                .Replace("{file}", path));
            AppendReload(text, reload);
            return text.ToString();
        }

        /// <summary>The preset table as the guide prints it: one line per option the presets cover.</summary>
        public static string Table()
        {
            var text = new StringBuilder();
            foreach (Feature feature in Features.All)
            {
                bool titled = false;
                foreach (PresetRule rule in feature.Presets)
                {
                    OptionInfo info = SettingsTable.Get(rule.OptionId);
                    if (info == null || !info.HasDefault)
                    {
                        continue;
                    }
                    if (!titled)
                    {
                        titled = true;
                        text.Append("\n<b>").Append(feature.TitleKey.GetLocalization()).Append("</b>\n");
                    }
                    text.Append("  - ").Append(Label(info)).Append("\n        ");
                    for (int i = 0; i < NameKeys.Length; i++)
                    {
                        TryValue(info, (Intensity)i, out int value);
                        text.Append(i == 0 ? string.Empty : "   |   ").Append(NameKeys[i].GetLocalization()).Append(": ").Append(Show(info, value));
                    }
                    text.Append("   |   ").Append("reallife_meta_now".GetLocalization()).Append(": ").Append(Show(info, SettingsTable.Current(info))).Append('\n');
                }
            }
            return text.ToString();
        }

        internal static string Show(OptionInfo info, int value)
        {
            if (info.Type == OptionType.Toggle)
            {
                return (value != 0 ? "reallife_meta_on" : "reallife_meta_off").GetLocalization();
            }
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
