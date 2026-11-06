using System;
using System.Globalization;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// A value of a saved option in words, as the options page shows it: a slider's number in its own format ("75%"), a drop-down's
    /// choice by its own text. One place for the page, the Settings app, the presets' table and the Guide. Game-free.
    /// </summary>
    public static class OptionValueRules
    {
        /// <summary>What a slider shows beside itself: the number, or the number as a per cent.</summary>
        public const string SliderValue = "reallife_opt_value";
        public const string SliderPercent = "reallife_opt_value_percent";

        /// <summary>A slider's value in its format (a text with {value}); the plain number when it has none.</summary>
        public static string Slider(string formatKey, int value, Func<string, string> text)
        {
            return text(string.IsNullOrEmpty(formatKey) ? SliderValue : formatKey).Replace("{value}", value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>A drop-down's choice by its own text; empty for a value it has no choice for (the page's own answer).</summary>
        public static string Choice(string[] choiceKeys, int value, Func<string, string> text)
        {
            return choiceKeys != null && value >= 0 && value < choiceKeys.Length ? text(choiceKeys[value]) : string.Empty;
        }
    }
}
