using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// The words of Standing. Every text is a key of en.json, written out in full so that the locale check finds each one;
    /// nothing here is kept. Called when the page shows something or a notification is made, never otherwise.
    /// </summary>
    public static class FameText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] PartKeys = { "reallife_fame_part_0", "reallife_fame_part_1", "reallife_fame_part_2", "reallife_fame_part_3" };
        public static readonly string[] PartAboutKeys = { "reallife_fame_part_about_0", "reallife_fame_part_about_1", "reallife_fame_part_about_2", "reallife_fame_part_about_3" };
        public static readonly string[] TierKeys = { "reallife_fame_tier_0", "reallife_fame_tier_1", "reallife_fame_tier_2", "reallife_fame_tier_3", "reallife_fame_tier_4" };
        public static readonly string[] ProfileKeys =
        {
            "reallife_fame_profile_0", "reallife_fame_profile_1", "reallife_fame_profile_2", "reallife_fame_profile_3", "reallife_fame_profile_4", "reallife_fame_profile_5"
        };
        private static readonly string[] OpinionKeys = { "reallife_fame_opinion_0", "reallife_fame_opinion_1", "reallife_fame_opinion_2", "reallife_fame_opinion_3", "reallife_fame_opinion_4" };

        // ---- what moved a part, by FameCause
        private static readonly string[] CauseKeys =
        {
            "reallife_fame_cause_0", "reallife_fame_cause_1", "reallife_fame_cause_2", "reallife_fame_cause_3", "reallife_fame_cause_4", "reallife_fame_cause_5",
            "reallife_fame_cause_6", "reallife_fame_cause_7", "reallife_fame_cause_8", "reallife_fame_cause_9", "reallife_fame_cause_10", "reallife_fame_cause_11",
            "reallife_fame_cause_12", "reallife_fame_cause_13", "reallife_fame_cause_14", "reallife_fame_cause_15", "reallife_fame_cause_16", "reallife_fame_cause_17",
            "reallife_fame_cause_18", "reallife_fame_cause_19", "reallife_fame_cause_20", "reallife_fame_cause_21", "reallife_fame_cause_22", "reallife_fame_cause_23",
            "reallife_fame_cause_24", "reallife_fame_cause_25", "reallife_fame_cause_26", "reallife_fame_cause_27", "reallife_fame_cause_28", "reallife_fame_cause_29"
        };

        // ---- what people in the street say, by FameReaction (Look says nothing)
        private static readonly string[][] StreetKeys =
        {
            null,
            null,
            new[] { "reallife_fame_street_greet_1", "reallife_fame_street_greet_2", "reallife_fame_street_greet_3" },
            new[] { "reallife_fame_street_praise_1", "reallife_fame_street_praise_2", "reallife_fame_street_praise_3" },
            new[] { "reallife_fame_street_remark_1", "reallife_fame_street_remark_2", "reallife_fame_street_remark_3" },
            new[] { "reallife_fame_street_rents_1", "reallife_fame_street_rents_2", "reallife_fame_street_rents_3" },
            new[] { "reallife_fame_street_complaint_1", "reallife_fame_street_complaint_2", "reallife_fame_street_complaint_3" }
        };
        private static readonly string[] GreetRichKeys = { "reallife_fame_street_greet_rich_1", "reallife_fame_street_greet_rich_2", "reallife_fame_street_greet_rich_3" };

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>"Label: value" with the value in bold, as the game's white box writes a line.</summary>
        public static string Pair(string labelKey, string value)
        {
            return T(labelKey) + " <b>" + value + "</b>";
        }

        public static string N(int value)
        {
            return value.ToString("N0", Inv);
        }

        public static string Signed(int value)
        {
            return (value > 0 ? "+" : string.Empty) + N(value);
        }

        /// <summary>An amount of money as the game writes it.</summary>
        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        public static string Part(FamePart part)
        {
            return T(PartKeys[FameRules.Clamp((int)part, 0, PartKeys.Length - 1)]);
        }

        public static string Tier(FameTier tier)
        {
            return T(TierKeys[FameRules.Clamp((int)tier, 0, TierKeys.Length - 1)]);
        }

        public static string Profile(FameProfile profile)
        {
            return T(ProfileKeys[FameRules.Clamp((int)profile, 0, ProfileKeys.Length - 1)]);
        }

        public static string Cause(FameCause cause)
        {
            return T(CauseKeys[FameRules.Clamp((int)cause, 0, CauseKeys.Length - 1)]);
        }

        /// <summary>A reputation in a word: poor, low, fair, good, excellent.</summary>
        public static string Opinion(int value)
        {
            int band = value < 25 ? 0 : value < 45 ? 1 : value < 60 ? 2 : value < 80 ? 3 : 4;
            return T(OpinionKeys[band]);
        }

        /// <summary>A reputation as the page writes it: "62, good".</summary>
        public static string Rep(int value)
        {
            return Fill("reallife_fame_rep_value", "value", N(value), "word", Opinion(value));
        }

        /// <summary>The key of what somebody in the street says, by a roll; null for a reaction without words.</summary>
        public static string StreetLine(FameReaction reaction, bool rich, int roll)
        {
            string[] keys = reaction == FameReaction.Greeting && rich ? GreetRichKeys : StreetKeys[FameRules.Clamp((int)reaction, 0, StreetKeys.Length - 1)];
            return keys != null ? keys[FameRules.Clamp(roll, 0, 999) % keys.Length] : null;
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2);   // (a dictionary each: the game keeps a notification's data)
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }
    }
}
