namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// The names of the places people work for and the locale keys of every line, by position and by favour. Names are the
    /// city's own firms, the same in every language; an official's office is a locale key. Game-free.
    /// </summary>
    public static class NetworkCatalogue
    {
        // By NetRole's number; a buyer's and a manager's are the game's own importer or wholesaler when the city has one.
        private static readonly string[][] Orgs =
        {
            null,
            new[] { "Harbour Line Imports", "Eastgate Trading", "Pier Nine Imports", "Atlas Freight Trading" },
            new[] { "Union Wholesale", "Crossway Wholesale", "Metro Cash Supply", "Riverside Wholesale" },
            new[] { "Keane & Moreau", "Hartley Walsh LLP", "Osei Park Partners", "Lowell & Costa", "Ramsey Thorne Law", "Varga Ellison" },
            new[] { "Northline Staffing", "Brightpath Recruitment", "Harbour Talent", "Keystone People", "Metro Careers Agency" },
            new[] { "Ridgeway Capital", "Copperleaf Ventures", "Bluestone Partners", "Halcyon Fund", "Westbridge Equity" },
            new[] { "reallife_social_net_org_planning", "reallife_social_net_org_licensing", "reallife_social_net_org_buildings", "reallife_social_net_org_development" },
            new[] { "Greystone Properties", "Parkview Estates", "Ironbridge Holdings", "Linden Realty", "Cornerstone Lettings" }
        };

        public static string[] OrgsOf(NetRole role)
        {
            int r = (int)role;
            return r > 0 && r < Orgs.Length ? Orgs[r] : null;
        }

        // "a recruiter at {org}": how their position is said, after their name.
        private static readonly string[] Roles =
        {
            null, "reallife_social_net_role_buyer", "reallife_social_net_role_wholesale", "reallife_social_net_role_lawyer",
            "reallife_social_net_role_recruiter", "reallife_social_net_role_investor", "reallife_social_net_role_official", "reallife_social_net_role_landlord"
        };

        // What they say about their work when they are met, in the conversation.
        private static readonly string[] Meets =
        {
            null, "reallife_social_net_meet_buyer", "reallife_social_net_meet_wholesale", "reallife_social_net_meet_lawyer",
            "reallife_social_net_meet_recruiter", "reallife_social_net_meet_investor", "reallife_social_net_meet_official", "reallife_social_net_meet_landlord"
        };

        // What the friend or Mom who brings them says about them.
        private static readonly string[] Intros =
        {
            null, "reallife_social_net_intro_buyer", "reallife_social_net_intro_wholesale", "reallife_social_net_intro_lawyer",
            "reallife_social_net_intro_recruiter", "reallife_social_net_intro_investor", "reallife_social_net_intro_official", "reallife_social_net_intro_landlord"
        };

        // Their first text after numbers are swapped: what they do, and that they could help one day.
        private static readonly string[] Texts =
        {
            null, "reallife_social_net_text_buyer", "reallife_social_net_text_wholesale", "reallife_social_net_text_lawyer",
            "reallife_social_net_text_recruiter", "reallife_social_net_text_investor", "reallife_social_net_text_official", "reallife_social_net_text_landlord"
        };

        // Their text when a favour is ready: call me.
        private static readonly string[] Readies =
        {
            null, "reallife_social_net_ready_joblead", "reallife_social_net_ready_freelance", "reallife_social_net_ready_importer",
            "reallife_social_net_ready_wholesaler", "reallife_social_net_ready_terms", "reallife_social_net_ready_investor", "reallife_social_net_ready_property"
        };

        // The call's button that asks for it.
        private static readonly string[] Buttons =
        {
            null, "reallife_social_net_btn_joblead", "reallife_social_net_btn_freelance", "reallife_social_net_btn_importer",
            "reallife_social_net_btn_wholesaler", "reallife_social_net_btn_terms", "reallife_social_net_btn_investor", "reallife_social_net_btn_property"
        };

        // What they say when it is done, with {a} and {b}.
        private static readonly string[] Dones =
        {
            null, "reallife_social_net_done_joblead", "reallife_social_net_done_freelance", "reallife_social_net_done_importer",
            "reallife_social_net_done_wholesaler", "reallife_social_net_done_terms", "reallife_social_net_done_investor", "reallife_social_net_done_property"
        };

        // The favour's name, for the People page.
        private static readonly string[] Names =
        {
            null, "reallife_social_net_name_joblead", "reallife_social_net_name_freelance", "reallife_social_net_name_importer",
            "reallife_social_net_name_wholesaler", "reallife_social_net_name_terms", "reallife_social_net_name_investor", "reallife_social_net_name_property"
        };

        /// <summary>How their position is said.</summary>
        public static string RoleKey(NetRole value) => Pick(Roles, (int)value);

        /// <summary>What they say of their work when met.</summary>
        public static string MeetKey(NetRole value) => Pick(Meets, (int)value);

        /// <summary>What the one who brings them says of them.</summary>
        public static string IntroKey(NetRole value) => Pick(Intros, (int)value);

        /// <summary>Their first text about their work.</summary>
        public static string TextKey(NetRole value) => Pick(Texts, (int)value);

        /// <summary>Their text when the favour is ready.</summary>
        public static string ReadyKey(NetFavour value) => Pick(Readies, (int)value);

        /// <summary>The call's button that asks for the favour.</summary>
        public static string ButtonKey(NetFavour value) => Pick(Buttons, (int)value);

        /// <summary>What they say when the favour is done.</summary>
        public static string DoneKey(NetFavour value) => Pick(Dones, (int)value);

        /// <summary>The favour's name.</summary>
        public static string NameKey(NetFavour value) => Pick(Names, (int)value);

        private static string Pick(string[] keys, int at)
        {
            return at > 0 && at < keys.Length ? keys[at] : null;
        }

        public const string Met = "reallife_social_net_met";               // a notification: who they are, and that they may help
        public const string PeopleLine = "reallife_social_net_people";     // the People page: their position
        public const string PeopleNext = "reallife_social_net_people_next";   // ...and what they could do next
        public const string PeopleDone = "reallife_social_net_people_done";   // ...or that they have done all they can
        public const string Option = "reallife_social_networking";
        public const string OptionTip = "reallife_opt_tip_social_networking";
        public const string TestIntro = "reallife_social_net_test_intro";
        public const string TestFavour = "reallife_social_net_test_favour";
        public const string TestEach = "reallife_social_net_test_each";
        public const string TestNobody = "reallife_social_net_test_nobody";   // a test's refusal: nobody known holds a position
    }
}
