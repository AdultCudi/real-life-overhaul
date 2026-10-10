using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Mods;

namespace RealLifeOverhaul.Meta
{
    /// <summary>Meta's own log lines, through the mod's logger once there is one.</summary>
    internal static class MetaLog
    {
        public static IModLogger Sink;

        public static void Info(string message)
        {
            if (Sink != null)
            {
                Sink.Info("Meta: " + message);
            }
        }

        public static void Warn(string message)
        {
            if (Sink != null)
            {
                Sink.Warn("Meta: " + message);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[RealLifeOverhaul] Meta: " + message);
            }
        }

        public static void Error(Exception e)
        {
            if (Sink != null)
            {
                Sink.Error(e);
            }
            else
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }

    public enum OptionType
    {
        Toggle,
        Slider,
        Choice
    }

    /// <summary>One saved option of the mod: what the presets, the reset, the export and the guide know about it.</summary>
    public sealed class OptionInfo
    {
        public string Id;
        public OptionType Type;
        public int Min;
        public int Max = 1;
        public bool HasRange;          // min and max are known (from the options page)
        public bool HasDefault;
        public int Default;
        public string LabelKey;        // from the options page; null until it was seen there
        public FieldInfo Field;        // its static field in Settings, when it has one
        public Feature Owner;
    }

    /// <summary>
    /// Every saved option the mod has, found without a list of its own to keep up to date: the fields of
    /// <see cref="Settings"/> (a const string "XId" beside a static bool or int "X": id, kind and shipped value),
    /// the rows of the options page (label and range), and what a feature declares for options kept elsewhere.
    /// </summary>
    public static class SettingsTable
    {
        /// <summary>What a slider shows beside itself: the number, or the number as a per cent. The game shows nothing for a slider without one.</summary>
        public const string SliderValue = "reallife_opt_value";
        public const string SliderPercent = "reallife_opt_value_percent";

        private static readonly Dictionary<string, OptionInfo> ById = new Dictionary<string, OptionInfo>();
        private static readonly List<OptionInfo> Ordered = new List<OptionInfo>();
        private static bool _captured;

        public static IReadOnlyList<OptionInfo> All => Ordered;

        public static OptionInfo Get(string id)
        {
            return id != null && ById.TryGetValue(id, out OptionInfo info) ? info : null;
        }

        /// <summary>
        /// Called at the top of Settings.Load. The first time, the static fields still hold the values the mod
        /// ships with, and they are taken down as the defaults; later calls do nothing.
        /// </summary>
        public static void CaptureDefaults()
        {
            if (_captured)
            {
                return;
            }
            _captured = true;
            try
            {
                Type settings = typeof(Settings);
                foreach (FieldInfo idField in settings.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!idField.IsLiteral || idField.FieldType != typeof(string) || !idField.Name.EndsWith("Id", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    FieldInfo value = settings.GetField(idField.Name.Substring(0, idField.Name.Length - 2), BindingFlags.Public | BindingFlags.Static);
                    if (value == null || value.IsLiteral || (value.FieldType != typeof(bool) && value.FieldType != typeof(int)))
                    {
                        continue;   // an id with no value of its own (the dropped car_speed)
                    }
                    OptionInfo info = Ensure((string)idField.GetRawConstantValue());
                    info.Field = value;
                    info.Type = value.FieldType == typeof(bool) ? OptionType.Toggle : OptionType.Slider;
                    info.HasDefault = true;
                    info.Default = value.FieldType == typeof(bool) ? ((bool)value.GetValue(null) ? 1 : 0) : (int)value.GetValue(null);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        /// <summary>The default of an option that is not a field of Settings (a performance tweak, a look).</summary>
        public static void Declare(string id, OptionType type, int defaultValue)
        {
            OptionInfo info = Ensure(id);
            if (info.Field == null)
            {
                info.Type = type;
                info.HasDefault = true;
                info.Default = defaultValue;
            }
        }

        /// <summary>Takes label and range from a row of the options page.</summary>
        public static void Harvest(ModOption option, Feature owner)
        {
            if (!(option is IPersistableOption persistable) || string.IsNullOrEmpty(persistable.Id))
            {
                return;
            }
            OptionInfo info = Ensure(persistable.Id);
            info.LabelKey = option.Label;
            info.Owner = owner ?? info.Owner;
            if (option is SliderOption slider)
            {
                info.Type = OptionType.Slider;
                info.Min = slider.Min;
                info.Max = slider.Max;
                info.HasRange = true;
            }
            else if (option is DropdownOption dropdown)
            {
                info.Type = OptionType.Choice;
                info.Min = 0;
                info.Max = dropdown.ChoiceKeys.Length - 1;
                info.HasRange = true;
            }
            else if (option is ToggleOption)
            {
                info.Type = OptionType.Toggle;
                info.Min = 0;
                info.Max = 1;
                info.HasRange = true;
            }
        }

        private static OptionInfo Ensure(string id)
        {
            if (!ById.TryGetValue(id, out OptionInfo info))
            {
                info = new OptionInfo { Id = id };
                ById[id] = info;
                Ordered.Add(info);
            }
            return info;
        }

        /// <summary>What the option is now: its Settings field when it has one, otherwise what is saved.</summary>
        public static int Current(OptionInfo info)
        {
            if (info.Field != null)
            {
                object value = info.Field.GetValue(null);
                return value is bool on ? (on ? 1 : 0) : (int)value;
            }
            string key = Settings.PrefsKey(info.Id);
            return key == null ? info.Default : UnityEngine.PlayerPrefs.GetInt(key, info.Default);
        }

        /// <summary>Keeps a value inside what the option allows.</summary>
        public static int Clamp(OptionInfo info, int value)
        {
            if (info.Type == OptionType.Toggle)
            {
                return value != 0 ? 1 : 0;
            }
            return info.HasRange ? Math.Max(info.Min, Math.Min(info.Max, value)) : value;
        }

        /// <summary>Saves a value the way the options page does. Settings.Reload() afterwards makes it live.</summary>
        public static void Write(OptionInfo info, int value)
        {
            string key = Settings.PrefsKey(info.Id);
            if (key != null)
            {
                UnityEngine.PlayerPrefs.SetInt(key, Clamp(info, value));
            }
        }

        /// <summary>The same option row with the shipped value as its default, so the game's own "reset" really resets it.</summary>
        public static ModOption WithTrueDefault(ModOption option)
        {
            OptionInfo info = option is IPersistableOption persistable ? Get(persistable.Id) : null;
            if (info == null || !info.HasDefault)
            {
                return option;
            }
            if (option is ToggleOption toggle)
            {
                return new ToggleOption(toggle.Id, toggle.Label, info.Default != 0, toggle.OnValueChanged);
            }
            if (option is SliderOption slider)
            {
                return new SliderOption(slider.Id, slider.Label, slider.Min, slider.Max, Math.Max(slider.Min, Math.Min(slider.Max, info.Default)),
                    slider.OnValueChanged, string.IsNullOrEmpty(slider.ValueLabelKey) ? SliderValue : slider.ValueLabelKey);
            }
            if (option is DropdownOption dropdown)
            {
                return new DropdownOption(dropdown.Id, dropdown.Label, dropdown.ChoiceKeys, info.Default, dropdown.OnValueChanged);
            }
            return option;
        }
    }
}
