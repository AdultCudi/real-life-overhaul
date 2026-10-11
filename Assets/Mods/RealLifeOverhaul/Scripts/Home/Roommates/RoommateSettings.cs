using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The roommate's one option ("Roommates", on unless switched off), read from PlayerPrefs at game start like every
    /// other option of the mod, and its test buttons. Off, nobody answers, pays or is shown and nothing of the home is
    /// touched; a roommate who lives in is put away with the arrangement kept in the save, and comes back when it is
    /// switched on again.
    /// </summary>
    public static class RoommateSettings
    {
        public const string EnabledId = "home_roommates";

        public static bool Enabled = true;

        public static void Load(string modId)
        {
            Enabled = UnityEngine.PlayerPrefs.GetInt("m:" + modId + ":" + EnabledId, Enabled ? 1 : 0) != 0;   // (the key format the game saves mod options under, as Core/Settings)
        }
    }

    /// <summary>
    /// The doors between the roommate and the features it leans on. Each is null until Wiring/RoommateLinks fills it
    /// in; with a door left null the roommate does without (no friends to ask, no partner to make room for, nobody
    /// introduced). The roommate never reaches into those features itself.
    /// </summary>
    public static class RoommateBridges
    {
        /// <summary>True while the player's partner lives in their home (family life).</summary>
        public static Func<bool> PartnerLivesIn;
        public static Func<string> PartnerName;

        /// <summary>Fills the list with the friends who could be asked to move in (social life); returns how many.</summary>
        public static Func<List<SocialPerson>, int> Friends;
        /// <summary>The person of the social cast at a place in it; null if there is none (or social life is off).</summary>
        public static Func<int, SocialPerson> Friend;
        /// <summary>Tells social life that things moved between the player and one of its people: place, affection, trust.</summary>
        public static Action<int, float, float> FriendMoved;
        /// <summary>A roommate becomes one of the people of social life; returns their place in the cast, or -1 if there is none free.</summary>
        public static Func<Flatmate, int> MakeFriend;
        /// <summary>They introduce somebody the player has not met; returns that person's name, or null if nobody could be.</summary>
        public static Func<int, string> Introduce;
        /// <summary>True while the person at a place in the social cast is the player's partner.</summary>
        public static Func<int, bool> IsPartner;

        /// <summary>The partner's body while they are shown in the home, or null: the roommate keeps off where they are.</summary>
        public static Func<UnityEngine.Transform> PartnerBody;

        public static void Reset()
        {
            PartnerBody = null;
            PartnerLivesIn = null;
            PartnerName = null;
            Friends = null;
            Friend = null;
            FriendMoved = null;
            MakeFriend = null;
            Introduce = null;
            IsPartner = null;
        }
    }

    /// <summary>
    /// The feature's entry in the mod's feature list, and the figures the Guide's page "Roommates" prints: every
    /// {name} in its text is filled from <see cref="RoommateRules"/> here, so the page can never say anything the
    /// rules do not do. Called once, when the feature list is built (Meta/FeatureList); each figure is worked out
    /// when the page is opened.
    /// </summary>
    public static class RoommateGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("home_roommates", "reallife_meta_title_roommates")
                .Group("reallife_roommate_header")
                .About("reallife_guide_roommates_about").Help("reallife_guide_roommates")
                .Owns(RoommateSettings.EnabledId)
                .Data(RoommateStore.Key, RoommateDirector.TestKey)
                .Contact(RoommatePhone.ContactId)
                .Test("reallife_roommate_test_candidates", "reallife_roommate_test_move_in", "reallife_roommate_test_rent_short", "reallife_roommate_test_noise",
                    "reallife_roommate_test_cooked", "reallife_roommate_test_notice", "reallife_roommate_test_back")
                .Fact("rm_fee", () => Guide.Money(RoommateRules.AdFee))
                .Fact("rm_least", () => Whole(RoommateRules.CandidatesLeast))
                .Fact("rm_most", () => Whole(RoommateRules.CandidatesMost))
                .Fact("rm_ad_days", () => Whole(RoommateRules.AdDays))
                .Fact("rm_share_least", () => Whole(RoommateRules.ShareLeast - 5) + "%")
                .Fact("rm_share_most", () => Whole(RoommateRules.ShareMost + 5) + "%")
                .Fact("rm_sofa", () => Whole(RoommateRules.SofaPct) + "%")
                .Fact("rm_rent_days", () => Whole(RoommateRules.RentEveryDays))
                .Fact("rm_notice_days", () => Whole(RoommateRules.NoticeDays))
                .Fact("rm_raids", () => Whole(RoommateRules.RaidsPerWeek))
                .Fact("rm_raid_leaves", () => Whole(RoommateRules.RaidLeaves))
                .Fact("rm_good", () => Whole(RoommateRules.GoodLevel))
                .Fact("rm_friend", () => Whole(RoommateRules.FriendLevel))
                .Fact("rm_reveal", () => Whole(RoommateRules.RevealDays))
                .Fact("rm_stay", () => Whole(RoommateRules.MinStayDays))
                .Cleanup(() => RoommateDirector.Instance?.StopAll())
                .ResetData(() => RoommateDirector.Instance?.Forget())
                .Repair(RoommatePhone.Setup);
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}

namespace RealLifeOverhaul
{
    /// <summary>The roommate's place on the options page and in the mod's start-up, under the mod's own namespace so that each is one line there.</summary>
    public static class RoommateOptions
    {
        public static void Load(string modId)
        {
            Roommates.RoommateSettings.Load(modId);
        }

        /// <summary>The "Roommates" block of the options page: the option and the test buttons.</summary>
        public static ModOptions AddRoommates(this ModOptions options)
        {
            return options
                .AddHeader("reallife_roommate_header")
                .AddToggle(Roommates.RoommateSettings.EnabledId, "reallife_roommate_enabled", Roommates.RoommateSettings.Enabled, v => Roommates.RoommateSettings.Enabled = v)
                .AddButton("reallife_roommate_test_candidates", () => Roommates.RoommateDirector.Instance?.TestCandidates())
                .AddButton("reallife_roommate_test_move_in", () => Roommates.RoommateDirector.Instance?.TestMoveIn())
                .AddButton("reallife_roommate_test_rent_short", () => Roommates.RoommateDirector.Instance?.TestRentShort())
                .AddButton("reallife_roommate_test_noise", () => Roommates.RoommateDirector.Instance?.TestNoise())
                .AddButton("reallife_roommate_test_cooked", () => Roommates.RoommateDirector.Instance?.TestCooked())
                .AddButton("reallife_roommate_test_notice", () => Roommates.RoommateDirector.Instance?.TestNotice())
                .AddButton("reallife_roommate_test_back", () => Roommates.RoommateDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
