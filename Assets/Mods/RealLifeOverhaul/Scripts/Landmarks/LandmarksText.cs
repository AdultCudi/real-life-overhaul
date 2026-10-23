using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>The landmarks' locale keys, written out whole so the text checks find them, and the words filled in.</summary>
    public static class LandmarksText
    {
        public const string Title = "reallife_meta_title_landmarks";              // the feature list, the options page
        public const string Enabled = "reallife_lmk_enabled";
        public const string About = "reallife_guide_landmarks_about";
        public const string Help = "reallife_guide_landmarks";

        public const string Tower = "reallife_lmk_name_tower";                    // the three names: the district's own, none of the game's
        public const string Mall = "reallife_lmk_name_mall";
        public const string Stadium = "reallife_lmk_name_stadium";
        public const string Club = "reallife_lmk_club_home";                      // the stadium's home side

        private static readonly string[] Clubs =
        {
            "reallife_lmk_club_0", "reallife_lmk_club_1", "reallife_lmk_club_2", "reallife_lmk_club_3",
            "reallife_lmk_club_4", "reallife_lmk_club_5", "reallife_lmk_club_6", "reallife_lmk_club_7"
        };

        public const string MapRow = "reallife_lmk_map_row";

        public const string BuilderPlayer = "reallife_lmk_builder_player";        // who builds it
        public const string BuilderRival = "reallife_lmk_builder_rival";
        public const string BuilderCity = "reallife_lmk_builder_city";

        public const string WordsPlanned = "reallife_lmk_words_planned";          // where it stands, for the construction game's Land tab
        public const string WordsAnnounced = "reallife_lmk_words_announced";
        public const string WordsTendered = "reallife_lmk_words_tendered";
        public const string WordsBuilding = "reallife_lmk_words_building";
        public const string WordsBuildingDue = "reallife_lmk_words_building_due";
        public const string WordsOpening = "reallife_lmk_words_opening";
        public const string WordsOpen = "reallife_lmk_words_open";

        private static readonly string[] Announced = { "reallife_lmk_paper_announced_tower", "reallife_lmk_paper_announced_mall", "reallife_lmk_paper_announced_stadium" };
        private static readonly string[] Phases = { "reallife_lmk_paper_phase_1", "reallife_lmk_paper_phase_2", "reallife_lmk_paper_phase_3" };
        public const string PaperAwarded = "reallife_lmk_paper_awarded";          // the City Ledger
        public const string PaperCity = "reallife_lmk_paper_city";
        public const string PaperOpening = "reallife_lmk_paper_opening";
        public const string PaperOpened = "reallife_lmk_paper_opened";
        public const string PaperMatchDay = "reallife_lmk_paper_match_day";
        public const string PaperWon = "reallife_lmk_paper_won";
        public const string PaperLost = "reallife_lmk_paper_lost";
        public const string PaperDrew = "reallife_lmk_paper_drew";

        public const string PanelOpeningTitle = "reallife_lmk_panel_opening_title";   // the game's panel at a landmark's front
        public const string PanelOpeningLine = "reallife_lmk_panel_opening_line";
        public const string PanelJoin = "reallife_lmk_panel_join";
        public const string PanelJoined = "reallife_lmk_panel_joined";
        public const string PanelStadiumTitle = "reallife_lmk_panel_stadium_title";
        public const string PanelMatchToday = "reallife_lmk_panel_match_today";
        public const string PanelMatchNext = "reallife_lmk_panel_match_next";
        public const string PanelLast = "reallife_lmk_panel_last";
        public const string NoticeJoined = "reallife_lmk_notice_joined";

        public const string TestCensus = "reallife_lmk_test_census";             // the test buttons
        public const string TestAnnounceButton = "reallife_lmk_test_announce";
        public const string TestAwardButton = "reallife_lmk_test_award";
        public const string TestTower = "reallife_lmk_test_tower";
        public const string TestMall = "reallife_lmk_test_mall";
        public const string TestStadium = "reallife_lmk_test_stadium";
        public const string TestOpeningButton = "reallife_lmk_test_opening";
        public const string TestMatchButton = "reallife_lmk_test_match";
        public const string TestTakeStadium = "reallife_lmk_test_take_stadium";
        public const string TestTakeTower = "reallife_lmk_test_take_tower";
        public const string TestTakeMall = "reallife_lmk_test_take_mall";
        public const string TestBackButton = "reallife_lmk_test_back";

        public const string TestUnread = "reallife_lmk_test_unread";             // the tests' notices
        public const string TestNoDistrict = "reallife_lmk_test_no_district";
        public const string TestNoSite = "reallife_lmk_test_no_site";
        public const string TestNothing = "reallife_lmk_test_nothing";
        public const string TestCensusDone = "reallife_lmk_test_census_done";
        public const string TestCensusOdd = "reallife_lmk_test_census_odd";
        public const string TestAnnounced = "reallife_lmk_test_announced";
        public const string TestAwarded = "reallife_lmk_test_awarded";
        public const string TestFinished = "reallife_lmk_test_finished";
        public const string TestOpening = "reallife_lmk_test_opening_done";
        public const string TestMatch = "reallife_lmk_test_match_done";
        public const string TestTaken = "reallife_lmk_test_taken";
        public const string TestBack = "reallife_lmk_test_back_done";

        public static string T(string key) => FinanceText.T(key);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string NameKey(LandmarkKind kind) => kind == LandmarkKind.Tower ? Tower : kind == LandmarkKind.Mall ? Mall : Stadium;

        public static string ClubKey(int club) => Clubs[(club % Clubs.Length + Clubs.Length) % Clubs.Length];

        public static string PaperAnnounced(LandmarkKind kind) => Announced[(int)kind % Announced.Length];

        public static string PaperPhase(int phase) => Phases[System.Math.Max(1, System.Math.Min(Phases.Length, phase)) - 1];
    }
}
