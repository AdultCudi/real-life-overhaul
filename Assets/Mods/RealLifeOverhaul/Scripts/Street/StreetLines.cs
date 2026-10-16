namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The written lines of street talk: for every kind of line (StreetLine) a few in each language (en, pt, zh-cn; the
    /// other languages read English until they are translated), what each choice of the player's says, the places asked
    /// the way to. They are what is shown whenever the model's words are not there in time, or the model is off, missing,
    /// or the game is in another language (StreetTexts). Every key is written out in full: the catalogue check reads them.
    /// Game-free. COST: none.
    /// </summary>
    public static class StreetLines
    {
        /// <summary>The lines of each kind, by StreetLine.</summary>
        public static readonly string[][] Lines =
        {
            new[] { "reallife_street_open_warm_1", "reallife_street_open_warm_2", "reallife_street_open_warm_3", "reallife_street_open_warm_4" },   // OpenWarm
            new[] { "reallife_street_open_fine_1", "reallife_street_open_fine_2", "reallife_street_open_fine_3", "reallife_street_open_fine_4" },   // OpenFine
            new[] { "reallife_street_open_cool_1", "reallife_street_open_cool_2", "reallife_street_open_cool_3" },   // OpenCool
            new[] { "reallife_street_open_again_1", "reallife_street_open_again_2", "reallife_street_open_again_3" },   // OpenAgain
            new[] { "reallife_street_open_famous_1", "reallife_street_open_famous_2", "reallife_street_open_famous_3" },   // OpenFamous
            new[] { "reallife_street_open_infamous_1", "reallife_street_open_infamous_2", "reallife_street_open_infamous_3" },   // OpenInfamous
            new[] { "reallife_street_brush_hurry_1", "reallife_street_brush_hurry_2", "reallife_street_brush_hurry_3" },   // BrushHurry
            new[] { "reallife_street_brush_work_1", "reallife_street_brush_work_2", "reallife_street_brush_work_3" },   // BrushWork
            new[] { "reallife_street_brush_night_1", "reallife_street_brush_night_2", "reallife_street_brush_night_3" },   // BrushNight
            new[] { "reallife_street_brush_rain_1", "reallife_street_brush_rain_2", "reallife_street_brush_rain_3" },   // BrushRain
            new[] { "reallife_street_brush_mood_1", "reallife_street_brush_mood_2", "reallife_street_brush_mood_3" },   // BrushMood
            new[] { "reallife_street_brush_rude_1", "reallife_street_brush_rude_2", "reallife_street_brush_rude_3" },   // BrushRude
            new[] { "reallife_street_brush_unwashed_1", "reallife_street_brush_unwashed_2", "reallife_street_brush_unwashed_3" },   // BrushUnwashed
            new[] { "reallife_street_brush_record_1", "reallife_street_brush_record_2" },   // BrushRecord
            new[] { "reallife_street_brush_wanted_1", "reallife_street_brush_wanted_2" },   // BrushWanted
            new[] { "reallife_street_brush_phone_1", "reallife_street_brush_phone_2" },   // BrushPhone
            new[] { "reallife_street_waiting_1", "reallife_street_waiting_2", "reallife_street_waiting_3", "reallife_street_waiting_4" },   // Waiting
            new[] { "reallife_street_small_warm_1", "reallife_street_small_warm_2", "reallife_street_small_warm_3", "reallife_street_small_warm_4", "reallife_street_small_warm_5" },   // SmallWarm
            new[] { "reallife_street_small_cool_1", "reallife_street_small_cool_2", "reallife_street_small_cool_3", "reallife_street_small_cool_4" },   // SmallCool
            new[] { "reallife_street_about_job_1", "reallife_street_about_job_2", "reallife_street_about_job_3", "reallife_street_about_job_4" },   // AboutJob
            new[] { "reallife_street_about_retired_1", "reallife_street_about_retired_2" },   // AboutRetired
            new[] { "reallife_street_about_cool_1", "reallife_street_about_cool_2", "reallife_street_about_cool_3" },   // AboutCool
            new[] { "reallife_street_name_give_1", "reallife_street_name_give_2", "reallife_street_name_give_3", "reallife_street_name_give_4" },   // NameGive
            new[] { "reallife_street_name_withhold_1", "reallife_street_name_withhold_2", "reallife_street_name_withhold_3" },   // NameWithhold
            new[] { "reallife_street_name_known_1", "reallife_street_name_known_2" },   // NameKnown
            new[] { "reallife_street_area_places_1", "reallife_street_area_places_2", "reallife_street_area_places_3" },   // AreaPlaces
            new[] { "reallife_street_area_event_1", "reallife_street_area_event_2" },   // AreaEvent
            new[] { "reallife_street_area_news_1", "reallife_street_area_news_2" },   // AreaNews
            new[] { "reallife_street_area_nothing_1", "reallife_street_area_nothing_2" },   // AreaNothing
            new[] { "reallife_street_dir_ask_1", "reallife_street_dir_ask_2" },   // DirAsk
            new[] { "reallife_street_dir_found_1", "reallife_street_dir_found_2", "reallife_street_dir_found_3", "reallife_street_dir_found_4" },   // DirFound
            new[] { "reallife_street_dir_none_1", "reallife_street_dir_none_2", "reallife_street_dir_none_3" },   // DirNone
            new[] { "reallife_street_dir_far_1", "reallife_street_dir_far_2" },   // DirFar
            new[] { "reallife_street_compliment_yes_1", "reallife_street_compliment_yes_2", "reallife_street_compliment_yes_3", "reallife_street_compliment_yes_4" },   // ComplimentYes
            new[] { "reallife_street_compliment_no_1", "reallife_street_compliment_no_2", "reallife_street_compliment_no_3" },   // ComplimentNo
            new[] { "reallife_street_joke_yes_1", "reallife_street_joke_yes_2", "reallife_street_joke_yes_3", "reallife_street_joke_yes_4" },   // JokeYes
            new[] { "reallife_street_joke_no_1", "reallife_street_joke_no_2", "reallife_street_joke_no_3" },   // JokeNo
            new[] { "reallife_street_flirt_yes_1", "reallife_street_flirt_yes_2", "reallife_street_flirt_yes_3", "reallife_street_flirt_yes_4" },   // FlirtYes
            new[] { "reallife_street_flirt_no_1", "reallife_street_flirt_no_2", "reallife_street_flirt_no_3", "reallife_street_flirt_no_4" },   // FlirtNo
            new[] { "reallife_street_flirt_taken_1", "reallife_street_flirt_taken_2", "reallife_street_flirt_taken_3" },   // FlirtTaken
            new[] { "reallife_street_number_yes_1", "reallife_street_number_yes_2", "reallife_street_number_yes_3", "reallife_street_number_yes_4" },   // NumberYes
            new[] { "reallife_street_number_no_1", "reallife_street_number_no_2", "reallife_street_number_no_3" },   // NumberNo
            new[] { "reallife_street_number_taken_1", "reallife_street_number_taken_2" },   // NumberTaken
            new[] { "reallife_street_number_already_1", "reallife_street_number_already_2" },   // NumberAlready
            new[] { "reallife_street_number_later_1", "reallife_street_number_later_2" },   // NumberLater
            new[] { "reallife_street_invite_yes_1", "reallife_street_invite_yes_2", "reallife_street_invite_yes_3", "reallife_street_invite_yes_4" },   // InviteYes
            new[] { "reallife_street_invite_maybe_stranger_1", "reallife_street_invite_maybe_stranger_2", "reallife_street_invite_maybe_stranger_3" },   // InviteMaybeStranger
            new[] { "reallife_street_invite_maybe_busy_1", "reallife_street_invite_maybe_busy_2" },   // InviteMaybeBusy
            new[] { "reallife_street_invite_maybe_night_1", "reallife_street_invite_maybe_night_2" },   // InviteMaybeNight
            new[] { "reallife_street_invite_maybe_later_1", "reallife_street_invite_maybe_later_2" },   // InviteMaybeLater
            new[] { "reallife_street_invite_maybe_nowhere_1", "reallife_street_invite_maybe_nowhere_2" },   // InviteMaybeNowhere
            new[] { "reallife_street_invite_maybe_plan_1" },   // InviteMaybePlan
            new[] { "reallife_street_invite_no_taken_1", "reallife_street_invite_no_taken_2" },   // InviteNoTaken
            new[] { "reallife_street_invite_no_dislikes_1", "reallife_street_invite_no_dislikes_2" },   // InviteNoDislikes
            new[] { "reallife_street_invite_no_1", "reallife_street_invite_no_2", "reallife_street_invite_no_3" },   // InviteNo
            new[] { "reallife_street_election_for_1", "reallife_street_election_for_2", "reallife_street_election_for_3" },   // ElectionFor
            new[] { "reallife_street_election_unsure_1", "reallife_street_election_unsure_2", "reallife_street_election_unsure_3" },   // ElectionUnsure
            new[] { "reallife_street_election_none_1", "reallife_street_election_none_2" },   // ElectionNone
            new[] { "reallife_street_economy_up_1", "reallife_street_economy_up_2", "reallife_street_economy_up_3" },   // EconomyUp
            new[] { "reallife_street_economy_down_1", "reallife_street_economy_down_2" },   // EconomyDown
            new[] { "reallife_street_economy_flat_1", "reallife_street_economy_flat_2" },   // EconomyFlat
            new[] { "reallife_street_business_heard_1", "reallife_street_business_heard_2", "reallife_street_business_heard_3" },   // BusinessHeard
            new[] { "reallife_street_business_will_1", "reallife_street_business_will_2", "reallife_street_business_will_3" },   // BusinessWill
            new[] { "reallife_street_business_no_1", "reallife_street_business_no_2", "reallife_street_business_no_3" },   // BusinessNo
            new[] { "reallife_street_business_again_1", "reallife_street_business_again_2" },   // BusinessAgain
            new[] { "reallife_street_said_warm_1", "reallife_street_said_warm_2", "reallife_street_said_warm_3", "reallife_street_said_warm_4", "reallife_street_said_warm_5" },   // SaidWarm
            new[] { "reallife_street_said_cool_1", "reallife_street_said_cool_2", "reallife_street_said_cool_3", "reallife_street_said_cool_4" },   // SaidCool
            new[] { "reallife_street_said_odd_1", "reallife_street_said_odd_2", "reallife_street_said_odd_3" },   // SaidOdd
            new[] { "reallife_street_rude_cold_1", "reallife_street_rude_cold_2", "reallife_street_rude_cold_3", "reallife_street_rude_cold_4" },   // RudeCold
            new[] { "reallife_street_walk_off_1", "reallife_street_walk_off_2", "reallife_street_walk_off_3", "reallife_street_walk_off_4" },   // WalkOff
            new[] { "reallife_street_threat_leave_1", "reallife_street_threat_leave_2", "reallife_street_threat_leave_3" },   // ThreatLeave
            new[] { "reallife_street_tired_1", "reallife_street_tired_2", "reallife_street_tired_3", "reallife_street_tired_4" },   // Tired
            new[] { "reallife_street_bye_1", "reallife_street_bye_2", "reallife_street_bye_3", "reallife_street_bye_4" },   // Bye
            new[] { "reallife_street_bye_cool_1", "reallife_street_bye_cool_2", "reallife_street_bye_cool_3" },   // ByeCool
        };

        /// <summary>What the player says for each choice (the button's words, and the player's line on the card), by StreetAsk.</summary>
        public static readonly string[] Asks =
        {
            "reallife_street_ask_smalltalk", "reallife_street_ask_about", "reallife_street_ask_name", "reallife_street_ask_area", "reallife_street_ask_directions",
            "reallife_street_ask_compliment", "reallife_street_ask_joke", "reallife_street_ask_flirt", "reallife_street_ask_number", "reallife_street_ask_invite",
            "reallife_street_ask_election", "reallife_street_ask_economy", "reallife_street_ask_business", "reallife_street_ask_say", "reallife_street_ask_rude"
        };

        public const string AskRain = "reallife_street_ask_smalltalk_rain";
        public const string AskNight = "reallife_street_ask_smalltalk_night";
        public const string AskDinner = "reallife_street_ask_invite_dinner";
        public const string Opener = "reallife_street_said_open";   // the player's first words, on the card only

        /// <summary>A place's button, by StreetPlace.</summary>
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

        /// <summary>Which way, by StreetRules.Way: ahead, right, behind, left.</summary>
        public static readonly string[] Ways = { "reallife_street_way_ahead", "reallife_street_way_right", "reallife_street_way_behind", "reallife_street_way_left" };

        /// <summary>
        /// One line of a kind, by the person's seed and the turn, never the one this person said last (when the kind has more
        /// than one): the same person does not say the same thing twice running, and two people do not all say the first.
        /// </summary>
        public static string Pick(StreetLine line, int seed, int turn, string last)
        {
            string[] pool = Lines[(int)line];
            int at = StreetRules.Roll(seed, 100 + turn, pool.Length);
            if (pool.Length > 1 && pool[at] == last)
            {
                at = (at + 1) % pool.Length;
            }
            return pool[at];
        }
    }
}
