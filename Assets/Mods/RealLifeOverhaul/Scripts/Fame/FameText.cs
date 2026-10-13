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

        // ---- the press: questions (the question, the first answer's button, the second's), what the player says, promises, invitations, pieces
        private static readonly string[] RiseKeys = { "reallife_fame_q_rise", "reallife_fame_a_rise_0", "reallife_fame_a_rise_1" };
        private static readonly string[] PlansKeys = { "reallife_fame_q_plans", "reallife_fame_a_plans_0", "reallife_fame_a_plans_1" };
        private static readonly string[] PromiseAskKeys = { "reallife_fame_q_promise", "reallife_fame_a_promise_0", "reallife_fame_a_promise_1" };
        private static readonly string[][] HardKeys =
        {
            new[] { "reallife_fame_q_hard_0", "reallife_fame_a_honest", "reallife_fame_a_deflect" },
            new[] { "reallife_fame_q_hard_1", "reallife_fame_a_honest", "reallife_fame_a_deflect" },
            new[] { "reallife_fame_q_hard_2", "reallife_fame_a_honest", "reallife_fame_a_deflect" },
            new[] { "reallife_fame_q_hard_3", "reallife_fame_a_honest", "reallife_fame_a_deflect" }
        };
        private static readonly string[] RiseSays = { "reallife_fame_say_rise_0", "reallife_fame_say_rise_1" };
        private static readonly string[] PlansSays = { "reallife_fame_say_plans_0", "reallife_fame_say_plans_1" };
        private static readonly string[] PromiseSays = { "reallife_fame_say_promise_0", "reallife_fame_say_promise_1" };
        private static readonly string[][] HardSays =
        {
            new[] { "reallife_fame_say_honest_0", "reallife_fame_say_deflect_0" },
            new[] { "reallife_fame_say_honest_1", "reallife_fame_say_deflect_1" },
            new[] { "reallife_fame_say_honest_2", "reallife_fame_say_deflect_2" },
            new[] { "reallife_fame_say_honest_3", "reallife_fame_say_deflect_3" }
        };
        private static readonly string[] PromiseKeys = { "reallife_fame_promise_0", "reallife_fame_promise_1", "reallife_fame_promise_2", "reallife_fame_promise_3" };
        private static readonly string[] InviteKeys = { "reallife_fame_invite_0", "reallife_fame_invite_1", "reallife_fame_invite_2", "reallife_fame_invite_3" };
        private static readonly string[] InviteStateKeys =
        {
            "reallife_fame_invite_state_0", "reallife_fame_invite_state_1", "reallife_fame_invite_state_2", "reallife_fame_invite_state_3", "reallife_fame_invite_state_4",
            "reallife_fame_invite_state_5"
        };
        private static readonly string[] PromiseStateKeys = { "reallife_fame_promise_state_0", "reallife_fame_promise_state_1", "reallife_fame_promise_state_2" };
        private static readonly string[] StoryKeys =
        {
            null, "reallife_fame_news_shops_2", "reallife_fame_news_shops_5", "reallife_fame_news_shops_10", "reallife_fame_news_shops_20", "reallife_fame_news_property_1",
            "reallife_fame_news_property_5", "reallife_fame_news_conditions", "reallife_fame_news_trust", "reallife_fame_news_jobs", "reallife_fame_news_rents",
            "reallife_fame_news_interview_good", "reallife_fame_news_interview_hard", "reallife_fame_news_show", "reallife_fame_news_show_bad", "reallife_fame_news_magazine",
            "reallife_fame_news_promise_kept", "reallife_fame_news_promise_broken", "reallife_fame_news_refused", "reallife_fame_news_sponsor", "reallife_fame_news_charity",
            "reallife_fame_news_rescue", "reallife_fame_news_renovation", "reallife_fame_news_lobby_won", "reallife_fame_news_lobby_lost", "reallife_fame_news_investigation",
            "reallife_fame_news_partnership", "reallife_fame_news_event"
        };

        /// <summary>A question's keys: the question, the first answer's button, the second's.</summary>
        public static string[] QuestionKeys(FameAsk ask)
        {
            switch (ask.Question)
            {
                case FameQuestion.Rise: return RiseKeys;
                case FameQuestion.Plans: return PlansKeys;
                case FameQuestion.Hard: return HardKeys[FameRules.Clamp((int)ask.Topic, 0, HardKeys.Length - 1)];
                default: return PromiseAskKeys;
            }
        }

        /// <summary>What the player says with each of the two answers, as the scene's page shows it under the button.</summary>
        public static string[] SayKeys(FameAsk ask)
        {
            switch (ask.Question)
            {
                case FameQuestion.Rise: return RiseSays;
                case FameQuestion.Plans: return PlansSays;
                case FameQuestion.Hard: return HardSays[FameRules.Clamp((int)ask.Topic, 0, HardSays.Length - 1)];
                default: return PromiseSays;
            }
        }

        public static string PromiseWords(FamePromiseKind kind)
        {
            return T(PromiseKeys[FameRules.Clamp((int)kind, 0, PromiseKeys.Length - 1)]);
        }

        public static string InviteName(FameInviteKind kind)
        {
            return T(InviteKeys[FameRules.Clamp((int)kind, 0, InviteKeys.Length - 1)]);
        }

        public static string InviteState(FameInviteState state)
        {
            return T(InviteStateKeys[FameRules.Clamp((int)state, 0, InviteStateKeys.Length - 1)]);
        }

        public static string PromiseState(FamePromiseState state)
        {
            return T(PromiseStateKeys[FameRules.Clamp((int)state, 0, PromiseStateKeys.Length - 1)]);
        }

        /// <summary>The locale key of a piece for the City Ledger; null for none.</summary>
        public static string StoryKey(FameStory story)
        {
            int at = (int)story;
            return at > 0 && at < StoryKeys.Length ? StoryKeys[at] : null;
        }

        /// <summary>An hour as the game writes the time.</summary>
        public static string Hour(int hour)
        {
            return hour.GetFormattedTime(0f, true);
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
