using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Schools
{
    /// <summary>The schools' locale keys, written out whole so the text checks find them, and the words filled in.</summary>
    public static class SchoolsText
    {
        public const string Title = "reallife_meta_title_schools";              // the feature list, the options page
        public const string Enabled = "reallife_schools_enabled";
        public const string About = "reallife_guide_schools_about";
        public const string Help = "reallife_guide_schools";
        public const string Street = "reallife_newbuildings_street_birchwoodgreen";   // the campus's own way: a street name, fixed in every language
        public const string MapRow = "reallife_schools_map_row";
        public const string Campus = "reallife_schools_name_birchwood";
        public const string Council = "reallife_schools_name_council";            // "{district} Council School"
        public const string OwnerCouncil = "reallife_schools_owner_council";
        public const string OwnerPlayer = "reallife_schools_owner_player";
        public const string Transaction = "reallife_transaction_school_week";     // the player's school's week, in the game's books
        public const string TransactionBuy = "reallife_transaction_school_buy";
        public const string TransactionSell = "reallife_transaction_school_sell";
        public const string TransactionHead = "reallife_transaction_school_head";

        public const string FloorJuniors = "reallife_schools_floor_juniors";      // the main school's floors at its door and lift
        public const string FloorMiddle = "reallife_schools_floor_middle";
        public const string FloorSeniors = "reallife_schools_floor_seniors";
        public const string DoorLine = "reallife_schools_door_line";
        public const string CardType = "reallife_schools_card_type";              // the game's building card: what it is ...
        public const string CardRunPlayer = "reallife_schools_card_run_player";   // ... and who runs it
        public const string CardRunCouncil = "reallife_schools_card_run_council";
        public const string CardRunTrust = "reallife_schools_card_run_trust";
        public const string OwnerNameCouncil = "reallife_schools_owner_name_council";   // BizMan's "Building owned by ...": the council as a name
        public const string PagePlayer = "reallife_schools_page_player";                 // BizMan's line when the player owns the school: where to run it
        public const string CampusApp = "reallife_campus_app";

        public const string Tab = "reallife_schools_tab";                         // the Campus app's Schools tab
        public const string ColSchool = "reallife_schools_col_school";
        public const string ColDistrict = "reallife_schools_col_district";
        public const string ColOwner = "reallife_schools_col_owner";
        public const string ColFee = "reallife_schools_col_fee";
        public const string ColPupils = "reallife_schools_col_pupils";
        public const string ColResults = "reallife_schools_col_results";
        public const string ColGrade = "reallife_schools_col_grade";
        public const string ColRank = "reallife_schools_col_rank";
        public const string Empty = "reallife_schools_empty";
        public const string AllKinds = "reallife_schools_all_kinds";
        public const string KindPrivate = "reallife_schools_kind_private";
        public const string KindCouncil = "reallife_schools_kind_council";

        public const string BoxTitle = "reallife_schools_box_title";
        public const string BoxAbout = "reallife_schools_box_about";
        public const string BoxOff = "reallife_schools_box_off";
        public const string BoxNotYet = "reallife_schools_box_not_yet";
        public const string SecFigures = "reallife_schools_sec_figures";
        public const string SecStaff = "reallife_schools_sec_staff";
        public const string SecRunning = "reallife_schools_sec_running";
        public const string SecChildren = "reallife_schools_sec_children";
        public const string SecTrade = "reallife_schools_sec_trade";
        public const string RowOwner = "reallife_schools_row_owner";
        public const string RowDistrict = "reallife_schools_row_district";
        public const string RowPupils = "reallife_schools_row_pupils";
        public const string RowWaiting = "reallife_schools_row_waiting";
        public const string RowFee = "reallife_schools_row_fee";
        public const string RowTeachers = "reallife_schools_row_teachers";
        public const string RowPay = "reallife_schools_row_pay";
        public const string RowHead = "reallife_schools_row_head";
        public const string RowScholar = "reallife_schools_row_scholar";
        public const string RowName = "reallife_schools_row_name";
        public const string RowResults = "reallife_schools_row_results";
        public const string RowGrade = "reallife_schools_row_grade";
        public const string RowTeaching = "reallife_schools_row_teaching";
        public const string RowDay = "reallife_schools_row_day";
        public const string RowWorth = "reallife_schools_row_worth";
        public const string RowRank = "reallife_schools_row_rank";
        public const string LineYours = "reallife_schools_line_yours";
        public const string LineOffer = "reallife_schools_line_offer";
        public const string LineSession = "reallife_schools_line_session";
        public const string LineCandidate = "reallife_schools_line_candidate";
        public const string LineChildAt = "reallife_schools_line_child_at";
        public const string LineChildAway = "reallife_schools_line_child_away";
        public const string LineNoChildren = "reallife_schools_line_no_children";
        public const string LineFull = "reallife_schools_line_full";
        public const string LineNotYours = "reallife_schools_line_not_yours";
        public const string LineBuy = "reallife_schools_line_buy";
        public const string LineSell = "reallife_schools_line_sell";
        public const string ButtonBuy = "reallife_schools_button_buy";
        public const string ButtonSell = "reallife_schools_button_sell";
        public const string ButtonAccept = "reallife_schools_button_accept";
        public const string ButtonFeeUp = "reallife_schools_button_fee_up";
        public const string ButtonFeeDown = "reallife_schools_button_fee_down";
        public const string ButtonClassOpen = "reallife_schools_button_class_open";
        public const string ButtonClassClose = "reallife_schools_button_class_close";
        public const string ButtonPayUp = "reallife_schools_button_pay_up";
        public const string ButtonPayDown = "reallife_schools_button_pay_down";
        public const string ButtonScholarUp = "reallife_schools_button_scholar_up";
        public const string ButtonScholarDown = "reallife_schools_button_scholar_down";
        public const string ButtonHead = "reallife_schools_button_head";
        public const string ButtonEnrol = "reallife_schools_button_enrol";
        public const string ButtonLeave = "reallife_schools_button_leave";
        public const string ButtonGo = "reallife_schools_button_go";

        public const string NoticeBought = "reallife_schools_notice_bought";      // notices: 240 characters at most in en
        public const string NoticeSold = "reallife_schools_notice_sold";
        public const string NoticeOffer = "reallife_schools_notice_offer";
        public const string NoticeOfferGone = "reallife_schools_notice_offer_gone";
        public const string NoticeEnrolled = "reallife_schools_notice_enrolled";
        public const string NoticeLeft = "reallife_schools_notice_left";
        public const string NoticeResults = "reallife_schools_notice_results";
        public const string NoticeInspected = "reallife_schools_notice_inspected";
        public const string NoticePoachedFrom = "reallife_schools_notice_poached_from";
        public const string NoticePoachedBy = "reallife_schools_notice_poached_by";
        public const string NoticeWeek = "reallife_schools_notice_week";
        public const string NoticeHead = "reallife_schools_notice_head";
        public const string NoticeFull = "reallife_schools_notice_full";
        public const string NoticeSession = "reallife_schools_notice_session";
        public const string NoticeAge = "reallife_schools_notice_age";
        public const string NoticeNoCampus = "reallife_schools_notice_no_campus";

        public const string PaperOpened = "reallife_schools_paper_opened";        // the City Ledger
        public const string PaperSold = "reallife_schools_paper_sold";
        public const string PaperSoldPlayer = "reallife_schools_paper_sold_player";
        public const string PaperResults = "reallife_schools_paper_results";
        public const string PaperInspected = "reallife_schools_paper_inspected";
        public const string PaperPoached = "reallife_schools_paper_poached";
        public const string PaperOpenDay = "reallife_schools_paper_open_day";

        public const string TestCensus = "reallife_schools_test_census";          // the test buttons
        public const string TestTake = "reallife_schools_test_take";
        public const string TestRun = "reallife_schools_test_run";
        public const string TestResults = "reallife_schools_test_results";
        public const string TestInspect = "reallife_schools_test_inspect";
        public const string TestTerm = "reallife_schools_test_term";
        public const string TestMine = "reallife_schools_test_mine";
        public const string TestEnrol = "reallife_schools_test_enrol";
        public const string TestBack = "reallife_schools_test_back";
        public const string TestLook = "reallife_schools_test_look";
        public const string TestCompare = "reallife_schools_test_compare";
        public const string TestDone = "reallife_schools_test_done";              // the tests' notices
        public const string TestFailed = "reallife_schools_test_failed";
        public const string TestUnread = "reallife_schools_test_unread";
        public const string TestNoCampus = "reallife_schools_test_no_campus";
        public const string TestNoChild = "reallife_schools_test_no_child";

        private static readonly string[] GradeKeys =
        {
            "reallife_schools_grade_0", "reallife_schools_grade_1", "reallife_schools_grade_2", "reallife_schools_grade_3", "reallife_schools_grade_4"
        };

        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        public static string T(string key) => FinanceText.T(key);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Money(long amount) => FinanceText.Money(amount);

        public static string N(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

        public static string Grade(int grade) => T(GradeKeys[SchoolRules.Clamp(grade, 0, GradeKeys.Length - 1)]);

        public static string GradeKey(int grade) => GradeKeys[SchoolRules.Clamp(grade, 0, GradeKeys.Length - 1)];

        /// <summary>A trust's name in the player's language.</summary>
        public static string Trust(int trust) => T(SchoolRules.TrustKeys[SchoolRules.Clamp(trust, 0, SchoolRules.Trusts - 1)]);

        /// <summary>Who runs a school, in words.</summary>
        public static string Owner(int owner) => owner == SchoolOwner.Player ? T(OwnerPlayer) : owner == SchoolOwner.Council ? T(OwnerCouncil) : Trust(owner);

        /// <summary>The card's line for who runs the main school: "Run by you", "Run by the council", "Run by Meridian Schools".</summary>
        public static string CardRun(int owner) => owner == SchoolOwner.Player ? T(CardRunPlayer) : owner == SchoolOwner.Council ? T(CardRunCouncil) : Fill(CardRunTrust, "who", Trust(owner));

        /// <summary>The name of who owns the main school for BizMan's owner line: the council or a trust by name; null for the player (the page names the player itself).</summary>
        public static string PageOwner(int owner) => owner == SchoolOwner.Player ? null : owner == SchoolOwner.Council ? T(OwnerNameCouncil) : Trust(owner);

        /// <summary>BizMan's line in the place of "Taken by": who runs the main school, and for the player where they run it (the Campus app's Schools tab).</summary>
        public static string PageLine(int owner) => owner == SchoolOwner.Player ? Fill(PagePlayer, "app", T(CampusApp), "tab", T(Tab)) : CardRun(owner);

        /// <summary>A rank as words for the league table: "3 of 12".</summary>
        public static string Rank(int rank, int count) => Fill("reallife_schools_rank", "rank", rank.ToString(Plain), "count", count.ToString(Plain));

        /// <summary>A school's name key: a private school's own; the council's share one phrase their district fills.</summary>
        public static string NameKey(int school) => SchoolRules.Def(school).Key;
    }
}
