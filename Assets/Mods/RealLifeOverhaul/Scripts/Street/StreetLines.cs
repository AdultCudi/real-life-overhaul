using System.Collections.Generic;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The written lines of street talk, shown whenever the model's words are not there in time. Every key is written out
    /// in full: the catalogue check reads them. Game-free. COST: none.
    /// </summary>
    public static class StreetLines
    {
        public static readonly string[][] Lines =
        {
            new[] { "reallife_street_open_warm_1", "reallife_street_open_warm_2", "reallife_street_open_warm_3", "reallife_street_open_warm_4", "reallife_street_open_warm_5" },   // OpenWarm
            new[] { "reallife_street_open_fine_1", "reallife_street_open_fine_2", "reallife_street_open_fine_3", "reallife_street_open_fine_4", "reallife_street_open_fine_5" },   // OpenFine
            new[] { "reallife_street_open_cool_1", "reallife_street_open_cool_2", "reallife_street_open_cool_3", "reallife_street_open_cool_4", "reallife_street_open_cool_5" },   // OpenCool
            new[] { "reallife_street_open_again_1", "reallife_street_open_again_2", "reallife_street_open_again_3", "reallife_street_open_again_4", "reallife_street_open_again_5" },   // OpenAgain
            new[] { "reallife_street_open_famous_1", "reallife_street_open_famous_2", "reallife_street_open_famous_3", "reallife_street_open_famous_4",
                "reallife_street_open_famous_5" },   // OpenFamous
            new[] { "reallife_street_open_infamous_1", "reallife_street_open_infamous_2", "reallife_street_open_infamous_3", "reallife_street_open_infamous_4",
                "reallife_street_open_infamous_5" },   // OpenInfamous
            new[] { "reallife_street_brush_hurry_1", "reallife_street_brush_hurry_2", "reallife_street_brush_hurry_3" },   // BrushHurry
            new[] { "reallife_street_brush_work_1", "reallife_street_brush_work_2", "reallife_street_brush_work_3" },   // BrushWork
            new[] { "reallife_street_brush_night_1", "reallife_street_brush_night_2", "reallife_street_brush_night_3" },   // BrushNight
            new[] { "reallife_street_brush_rain_1", "reallife_street_brush_rain_2", "reallife_street_brush_rain_3" },   // BrushRain
            new[] { "reallife_street_brush_mood_1", "reallife_street_brush_mood_2", "reallife_street_brush_mood_3" },   // BrushMood
            new[] { "reallife_street_brush_rude_1", "reallife_street_brush_rude_2", "reallife_street_brush_rude_3" },   // BrushRude
            new[] { "reallife_street_brush_unwashed_1", "reallife_street_brush_unwashed_2", "reallife_street_brush_unwashed_3" },   // BrushUnwashed
            new[] { "reallife_street_brush_record_1", "reallife_street_brush_record_2", "reallife_street_brush_record_3" },   // BrushRecord
            new[] { "reallife_street_brush_wanted_1", "reallife_street_brush_wanted_2", "reallife_street_brush_wanted_3" },   // BrushWanted
            new[] { "reallife_street_brush_phone_1", "reallife_street_brush_phone_2", "reallife_street_brush_phone_3" },   // BrushPhone
            new[] { "reallife_street_waiting_1", "reallife_street_waiting_2", "reallife_street_waiting_3", "reallife_street_waiting_4",
                "reallife_street_waiting_5", "reallife_street_waiting_6" },   // Waiting
            new[] { "reallife_street_small_warm_1", "reallife_street_small_warm_2", "reallife_street_small_warm_3", "reallife_street_small_warm_4",
                "reallife_street_small_warm_5", "reallife_street_small_warm_6", "reallife_street_small_warm_7" },   // SmallWarm
            new[] { "reallife_street_small_cool_1", "reallife_street_small_cool_2", "reallife_street_small_cool_3", "reallife_street_small_cool_4",
                "reallife_street_small_cool_5", "reallife_street_small_cool_6", "reallife_street_small_cool_7" },   // SmallCool
            new[] { "reallife_street_about_job_1", "reallife_street_about_job_2", "reallife_street_about_job_3", "reallife_street_about_job_4" },   // AboutJob
            new[] { "reallife_street_about_retired_1", "reallife_street_about_retired_2", "reallife_street_about_retired_3" },   // AboutRetired
            new[] { "reallife_street_about_cool_1", "reallife_street_about_cool_2", "reallife_street_about_cool_3" },   // AboutCool
            new[] { "reallife_street_name_give_1", "reallife_street_name_give_2", "reallife_street_name_give_3", "reallife_street_name_give_4" },   // NameGive
            new[] { "reallife_street_name_withhold_1", "reallife_street_name_withhold_2", "reallife_street_name_withhold_3" },   // NameWithhold
            new[] { "reallife_street_name_known_1", "reallife_street_name_known_2", "reallife_street_name_known_3" },   // NameKnown
            new[] { "reallife_street_area_places_1", "reallife_street_area_places_2", "reallife_street_area_places_3" },   // AreaPlaces
            new[] { "reallife_street_area_event_1", "reallife_street_area_event_2", "reallife_street_area_event_3" },   // AreaEvent
            new[] { "reallife_street_area_news_1", "reallife_street_area_news_2", "reallife_street_area_news_3" },   // AreaNews
            new[] { "reallife_street_area_nothing_1", "reallife_street_area_nothing_2", "reallife_street_area_nothing_3" },   // AreaNothing
            new[] { "reallife_street_dir_ask_1", "reallife_street_dir_ask_2", "reallife_street_dir_ask_3" },   // DirAsk
            new[] { "reallife_street_dir_found_1", "reallife_street_dir_found_2", "reallife_street_dir_found_3", "reallife_street_dir_found_4" },   // DirFound
            new[] { "reallife_street_dir_none_1", "reallife_street_dir_none_2", "reallife_street_dir_none_3" },   // DirNone
            new[] { "reallife_street_dir_far_1", "reallife_street_dir_far_2", "reallife_street_dir_far_3" },   // DirFar
            new[] { "reallife_street_compliment_yes_1", "reallife_street_compliment_yes_2", "reallife_street_compliment_yes_3", "reallife_street_compliment_yes_4" },   // ComplimentYes
            new[] { "reallife_street_compliment_no_1", "reallife_street_compliment_no_2", "reallife_street_compliment_no_3" },   // ComplimentNo
            new[] { "reallife_street_joke_yes_1", "reallife_street_joke_yes_2", "reallife_street_joke_yes_3", "reallife_street_joke_yes_4" },   // JokeYes
            new[] { "reallife_street_joke_no_1", "reallife_street_joke_no_2", "reallife_street_joke_no_3" },   // JokeNo
            new[] { "reallife_street_flirt_yes_1", "reallife_street_flirt_yes_2", "reallife_street_flirt_yes_3", "reallife_street_flirt_yes_4" },   // FlirtYes
            new[] { "reallife_street_flirt_no_1", "reallife_street_flirt_no_2", "reallife_street_flirt_no_3", "reallife_street_flirt_no_4" },   // FlirtNo
            new[] { "reallife_street_flirt_taken_1", "reallife_street_flirt_taken_2", "reallife_street_flirt_taken_3" },   // FlirtTaken
            new[] { "reallife_street_number_yes_1", "reallife_street_number_yes_2", "reallife_street_number_yes_3", "reallife_street_number_yes_4" },   // NumberYes
            new[] { "reallife_street_number_no_1", "reallife_street_number_no_2", "reallife_street_number_no_3" },   // NumberNo
            new[] { "reallife_street_number_taken_1", "reallife_street_number_taken_2", "reallife_street_number_taken_3" },   // NumberTaken
            new[] { "reallife_street_number_already_1", "reallife_street_number_already_2", "reallife_street_number_already_3" },   // NumberAlready
            new[] { "reallife_street_number_later_1", "reallife_street_number_later_2", "reallife_street_number_later_3" },   // NumberLater
            new[] { "reallife_street_invite_yes_1", "reallife_street_invite_yes_2", "reallife_street_invite_yes_3", "reallife_street_invite_yes_4" },   // InviteYes
            new[] { "reallife_street_invite_maybe_stranger_1", "reallife_street_invite_maybe_stranger_2", "reallife_street_invite_maybe_stranger_3" },   // InviteMaybeStranger
            new[] { "reallife_street_invite_maybe_busy_1", "reallife_street_invite_maybe_busy_2", "reallife_street_invite_maybe_busy_3" },   // InviteMaybeBusy
            new[] { "reallife_street_invite_maybe_night_1", "reallife_street_invite_maybe_night_2", "reallife_street_invite_maybe_night_3" },   // InviteMaybeNight
            new[] { "reallife_street_invite_maybe_later_1", "reallife_street_invite_maybe_later_2", "reallife_street_invite_maybe_later_3" },   // InviteMaybeLater
            new[] { "reallife_street_invite_maybe_nowhere_1", "reallife_street_invite_maybe_nowhere_2", "reallife_street_invite_maybe_nowhere_3" },   // InviteMaybeNowhere
            new[] { "reallife_street_invite_maybe_plan_1", "reallife_street_invite_maybe_plan_2", "reallife_street_invite_maybe_plan_3" },   // InviteMaybePlan
            new[] { "reallife_street_invite_no_taken_1", "reallife_street_invite_no_taken_2", "reallife_street_invite_no_taken_3" },   // InviteNoTaken
            new[] { "reallife_street_invite_no_dislikes_1", "reallife_street_invite_no_dislikes_2", "reallife_street_invite_no_dislikes_3" },   // InviteNoDislikes
            new[] { "reallife_street_invite_no_1", "reallife_street_invite_no_2", "reallife_street_invite_no_3" },   // InviteNo
            new[] { "reallife_street_election_for_1", "reallife_street_election_for_2", "reallife_street_election_for_3" },   // ElectionFor
            new[] { "reallife_street_election_unsure_1", "reallife_street_election_unsure_2", "reallife_street_election_unsure_3" },   // ElectionUnsure
            new[] { "reallife_street_election_none_1", "reallife_street_election_none_2", "reallife_street_election_none_3" },   // ElectionNone
            new[] { "reallife_street_economy_up_1", "reallife_street_economy_up_2", "reallife_street_economy_up_3" },   // EconomyUp
            new[] { "reallife_street_economy_down_1", "reallife_street_economy_down_2", "reallife_street_economy_down_3" },   // EconomyDown
            new[] { "reallife_street_economy_flat_1", "reallife_street_economy_flat_2", "reallife_street_economy_flat_3" },   // EconomyFlat
            new[] { "reallife_street_business_heard_1", "reallife_street_business_heard_2", "reallife_street_business_heard_3" },   // BusinessHeard
            new[] { "reallife_street_business_will_1", "reallife_street_business_will_2", "reallife_street_business_will_3" },   // BusinessWill
            new[] { "reallife_street_business_no_1", "reallife_street_business_no_2", "reallife_street_business_no_3" },   // BusinessNo
            new[] { "reallife_street_business_again_1", "reallife_street_business_again_2", "reallife_street_business_again_3" },   // BusinessAgain
            new[] { "reallife_street_said_warm_1", "reallife_street_said_warm_2", "reallife_street_said_warm_3", "reallife_street_said_warm_4",
                "reallife_street_said_warm_5", "reallife_street_said_warm_6", "reallife_street_said_warm_7", "reallife_street_said_warm_8" },   // SaidWarm
            new[] { "reallife_street_said_cool_1", "reallife_street_said_cool_2", "reallife_street_said_cool_3", "reallife_street_said_cool_4",
                "reallife_street_said_cool_5", "reallife_street_said_cool_6", "reallife_street_said_cool_7", "reallife_street_said_cool_8" },   // SaidCool
            new[] { "reallife_street_said_odd_1", "reallife_street_said_odd_2", "reallife_street_said_odd_3" },   // SaidOdd
            new[] { "reallife_street_rude_cold_1", "reallife_street_rude_cold_2", "reallife_street_rude_cold_3", "reallife_street_rude_cold_4" },   // RudeCold
            new[] { "reallife_street_walk_off_1", "reallife_street_walk_off_2", "reallife_street_walk_off_3", "reallife_street_walk_off_4" },   // WalkOff
            new[] { "reallife_street_threat_leave_1", "reallife_street_threat_leave_2", "reallife_street_threat_leave_3" },   // ThreatLeave
            new[] { "reallife_street_tired_1", "reallife_street_tired_2", "reallife_street_tired_3", "reallife_street_tired_4", "reallife_street_tired_5", "reallife_street_tired_6" },   // Tired
            new[] { "reallife_street_bye_1", "reallife_street_bye_2", "reallife_street_bye_3", "reallife_street_bye_4", "reallife_street_bye_5", "reallife_street_bye_6" },   // Bye
            new[] { "reallife_street_bye_cool_1", "reallife_street_bye_cool_2", "reallife_street_bye_cool_3", "reallife_street_bye_cool_4", "reallife_street_bye_cool_5" },   // ByeCool
            new[] { "reallife_street_day_good_1", "reallife_street_day_good_2", "reallife_street_day_good_3", "reallife_street_day_good_4", "reallife_street_day_good_5" },   // DayGood
            new[] { "reallife_street_day_fine_1", "reallife_street_day_fine_2", "reallife_street_day_fine_3", "reallife_street_day_fine_4", "reallife_street_day_fine_5" },   // DayFine
            new[] { "reallife_street_day_low_1", "reallife_street_day_low_2", "reallife_street_day_low_3", "reallife_street_day_low_4", "reallife_street_day_low_5" },   // DayLow
            new[] { "reallife_street_event_going_1", "reallife_street_event_going_2", "reallife_street_event_going_3", "reallife_street_event_going_4" },   // EventGoing
            new[] { "reallife_street_event_not_1", "reallife_street_event_not_2", "reallife_street_event_not_3", "reallife_street_event_not_4" },   // EventNot
            new[] { "reallife_street_work_likes_1", "reallife_street_work_likes_2", "reallife_street_work_likes_3", "reallife_street_work_likes_4", "reallife_street_work_likes_5" },   // WorkLikes
            new[] { "reallife_street_work_mixed_1", "reallife_street_work_mixed_2", "reallife_street_work_mixed_3", "reallife_street_work_mixed_4", "reallife_street_work_mixed_5" },   // WorkMixed
            new[] { "reallife_street_work_retired_1", "reallife_street_work_retired_2", "reallife_street_work_retired_3", "reallife_street_work_retired_4" },   // WorkRetired
            new[] { "reallife_street_welcome_1", "reallife_street_welcome_2", "reallife_street_welcome_3", "reallife_street_welcome_4", "reallife_street_welcome_5" },   // Welcome
            new[] { "reallife_street_greet_back_1", "reallife_street_greet_back_2", "reallife_street_greet_back_3", "reallife_street_greet_back_4" },   // GreetBack
        };

        /// <summary>What the player says for each choice, by StreetAsk: the button's words and the player's line on the card.</summary>
        public static readonly string[] Asks =
        {
            "reallife_street_ask_smalltalk", "reallife_street_ask_about", "reallife_street_ask_name", "reallife_street_ask_area", "reallife_street_ask_directions",
            "reallife_street_ask_compliment", "reallife_street_ask_joke", "reallife_street_ask_flirt", "reallife_street_ask_number", "reallife_street_ask_invite",
            "reallife_street_ask_election", "reallife_street_ask_economy", "reallife_street_ask_business", "reallife_street_ask_say", "reallife_street_ask_rude",
            "reallife_street_ask_day", "reallife_street_ask_event", "reallife_street_ask_work", "reallife_street_ask_thanks", "reallife_street_btn_goodbye"
        };

        /// <summary>The remarks of small talk, by StreetRules.SmallTopic.</summary>
        public static readonly string[] Small =
        {
            "reallife_street_ask_smalltalk", "reallife_street_ask_smalltalk_rain", "reallife_street_ask_smalltalk_night", "reallife_street_ask_smalltalk_morning",
            "reallife_street_ask_smalltalk_evening", "reallife_street_ask_smalltalk_weekend", "reallife_street_ask_smalltalk_busy"
        };

        // Other wordings of a choice, picked by the person's seed, and those of the moment.
        public static readonly string[] DayAsks = { "reallife_street_ask_day", "reallife_street_ask_day_2" };
        public static readonly string[] WorkAsks = { "reallife_street_ask_work", "reallife_street_ask_work_2" };
        public const string AskWorkRetired = "reallife_street_ask_work_retired";
        public const string AskThanksAnyway = "reallife_street_ask_thanks_anyway";
        public const string AskRain = "reallife_street_ask_smalltalk_rain";
        public const string AskNight = "reallife_street_ask_smalltalk_night";
        public const string AskDinner = "reallife_street_ask_invite_dinner";
        public const string Opener = "reallife_street_said_open";   // the player's first words, on the card only

        /// <summary>Place buttons, by StreetPlace.</summary>
        public static readonly string[] Places =
        {
            "reallife_street_place_coffee", "reallife_street_place_food", "reallife_street_place_drinks", "reallife_street_place_grocery",
            "reallife_street_place_clothes", "reallife_street_place_gym", "reallife_street_place_cinema", "reallife_street_place_bank"
        };

        /// <summary>A place as a line names it ("a cafe"), by StreetPlace.</summary>
        public static readonly string[] Whats =
        {
            "reallife_street_what_coffee", "reallife_street_what_food", "reallife_street_what_drinks", "reallife_street_what_grocery",
            "reallife_street_what_clothes", "reallife_street_what_gym", "reallife_street_what_cinema", "reallife_street_what_bank"
        };

        /// <summary>Which way, by StreetRules.Way.</summary>
        public static readonly string[] Ways = { "reallife_street_way_ahead", "reallife_street_way_right", "reallife_street_way_behind", "reallife_street_way_left" };

        public const int SaidMost = 32;   // written lines a person remembers having said today

        /// <summary>
        /// One line of a kind, by the person's seed and the turn: never the one said last, nor one said today while the kind
        /// has one unsaid; else the one said longest ago. COST: a pass over a few lines; nothing allocated.
        /// </summary>
        public static string Pick(StreetLine line, int seed, int turn, string last, List<string> said)
        {
            string[] pool = Lines[(int)line];
            int start = StreetRules.Roll(seed, 100 + turn, pool.Length);
            int best = -1, bestAge = -1;
            for (int k = 0; k < pool.Length; k++)
            {
                int at = (start + k) % pool.Length;
                if (pool.Length > 1 && pool[at] == last)
                {
                    continue;
                }
                int when = said != null ? said.LastIndexOf(pool[at]) : -1;
                if (when < 0)
                {
                    return pool[at];   // not said today
                }
                int age = said.Count - when;
                if (age > bestAge)
                {
                    best = at;
                    bestAge = age;
                }
            }
            return pool[best >= 0 ? best : start];
        }
    }
}
