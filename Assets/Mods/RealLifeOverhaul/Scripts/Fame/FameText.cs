using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// The words of Standing. Every key is written out in full so that the locale check finds it. Called when the page
    /// shows something or a notification is made, never per frame.
    /// </summary>
    public static class FameText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static readonly string[] PartAboutKeys =
        {
            "reallife_fame_part_about_0", "reallife_fame_part_about_1", "reallife_fame_part_about_2", "reallife_fame_part_about_3"
        };
        private static readonly string[] PartKeys = { "reallife_fame_part_0", "reallife_fame_part_1", "reallife_fame_part_2", "reallife_fame_part_3" };
        private static readonly string[] TierKeys = { "reallife_fame_tier_0", "reallife_fame_tier_1", "reallife_fame_tier_2", "reallife_fame_tier_3", "reallife_fame_tier_4" };
        private static readonly string[] ProfileKeys =
        {
            "reallife_fame_profile_0", "reallife_fame_profile_1", "reallife_fame_profile_2", "reallife_fame_profile_3", "reallife_fame_profile_4", "reallife_fame_profile_5"
        };
        private static readonly string[] OpinionKeys =
        {
            "reallife_fame_opinion_0", "reallife_fame_opinion_1", "reallife_fame_opinion_2", "reallife_fame_opinion_3", "reallife_fame_opinion_4"
        };
        private static readonly string[] CauseKeys =
        {
            "reallife_fame_cause_0", "reallife_fame_cause_1", "reallife_fame_cause_2", "reallife_fame_cause_3", "reallife_fame_cause_4", "reallife_fame_cause_5",
            "reallife_fame_cause_6", "reallife_fame_cause_7", "reallife_fame_cause_8", "reallife_fame_cause_9", "reallife_fame_cause_10", "reallife_fame_cause_11",
            "reallife_fame_cause_12", "reallife_fame_cause_13", "reallife_fame_cause_14", "reallife_fame_cause_15", "reallife_fame_cause_16", "reallife_fame_cause_17",
            "reallife_fame_cause_18", "reallife_fame_cause_19", "reallife_fame_cause_20", "reallife_fame_cause_21", "reallife_fame_cause_22", "reallife_fame_cause_23",
            "reallife_fame_cause_24", "reallife_fame_cause_25", "reallife_fame_cause_26", "reallife_fame_cause_27", "reallife_fame_cause_28", "reallife_fame_cause_29",
            "reallife_fame_cause_30"
        };

        // By FameReaction; None and Look say nothing.
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

        // A question's keys: the question, then the first and second answers' buttons.
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

        // The sponsor's request has two wordings, picked by its seed.
        private static readonly string[] RequestTitleKeys =
        {
            "reallife_fame_request_0", "reallife_fame_request_0b", "reallife_fame_request_1", "reallife_fame_request_2", "reallife_fame_request_3", "reallife_fame_request_4"
        };
        private static readonly string[] RequestAboutKeys =
        {
            "reallife_fame_request_about_0", "reallife_fame_request_about_0b", "reallife_fame_request_about_1", "reallife_fame_request_about_2", "reallife_fame_request_about_3",
            "reallife_fame_request_about_4"
        };
        private static readonly string[] RequestStateKeys =
        {
            "reallife_fame_request_state_0", "reallife_fame_request_state_1", "reallife_fame_request_state_2", "reallife_fame_request_state_3", "reallife_fame_request_state_4"
        };
        private static readonly string[] LobbyStateKeys = { "reallife_fame_lobby_state_0", "reallife_fame_lobby_state_1", "reallife_fame_lobby_state_2" };

        public static string T(string key) => key.GetLocalization();
        public static string N(int value) => value.ToString("N0", Inv);
        public static string Signed(int value) => (value > 0 ? "+" : string.Empty) + N(value);
        public static string Money(float amount) => amount.ToCurrencyFormat();
        public static string Hour(int hour) => hour.GetFormattedTime(0f, true);

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

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2);   // a new one each time: the game keeps it with the notification
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        public static string Part(FamePart part) => Pick(PartKeys, (int)part);
        public static string Tier(FameTier tier) => Pick(TierKeys, (int)tier);
        public static string Profile(FameProfile profile) => Pick(ProfileKeys, (int)profile);
        public static string Cause(FameCause cause) => Pick(CauseKeys, (int)cause);
        public static string PromiseWords(FamePromiseKind kind) => Pick(PromiseKeys, (int)kind);
        public static string PromiseState(FamePromiseState state) => Pick(PromiseStateKeys, (int)state);
        public static string InviteName(FameInviteKind kind) => Pick(InviteKeys, (int)kind);
        public static string InviteState(FameInviteState state) => Pick(InviteStateKeys, (int)state);
        public static string RequestState(FameRequestState state) => Pick(RequestStateKeys, (int)state);
        public static string LobbyState(FameLobbyState state) => Pick(LobbyStateKeys, (int)state);

        /// <summary>A reputation in a word: poor, low, fair, good, excellent.</summary>
        public static string Opinion(int value)
        {
            int band = value < 25 ? 0 : value < 45 ? 1 : value < 60 ? 2 : value < 80 ? 3 : 4;
            return T(OpinionKeys[band]);
        }

        /// <summary>A reputation as the page writes it: "62, good".</summary>
        public static string Rep(int value) => Fill("reallife_fame_rep_value", "value", N(value), "word", Opinion(value));

        /// <summary>The key of what somebody in the street says, by a roll; null for a reaction without words.</summary>
        public static string StreetLine(FameReaction reaction, bool rich, int roll)
        {
            string[] keys = reaction == FameReaction.Greeting && rich ? GreetRichKeys : StreetKeys[FameRules.Clamp((int)reaction, 0, StreetKeys.Length - 1)];
            return keys != null ? keys[FameRules.Clamp(roll, 0, 999) % keys.Length] : null;
        }

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

        /// <summary>What the player says with each answer, shown under its button.</summary>
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

        /// <summary>The locale key of a piece for the City Ledger; null for none.</summary>
        public static string StoryKey(FameStory story)
        {
            int at = (int)story;
            return at > 0 && at < StoryKeys.Length ? StoryKeys[at] : null;
        }

        public static string RequestTitle(FameRequest r) => Fill(RequestTitleKeys[RequestAt(r)], "district", FameGame.DistrictName(r.District));

        public static string RequestAbout(FameRequest r) => Fill(RequestAboutKeys[RequestAt(r)], "district", FameGame.DistrictName(r.District), "amount", Money(r.Amount));

        private static int RequestAt(FameRequest r)
        {
            int kind = FameRules.Clamp((int)r.Kind, 0, 4);
            return kind == 0 ? (r.Seed & 1) : kind + 1;
        }

        private static string Pick(string[] keys, int index) => T(keys[FameRules.Clamp(index, 0, keys.Length - 1)]);
    }
}
