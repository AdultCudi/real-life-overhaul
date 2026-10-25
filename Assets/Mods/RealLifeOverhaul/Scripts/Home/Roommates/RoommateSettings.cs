using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>The roommate's one option ("Roommates", on by default), read from PlayerPrefs at game start like every option of the mod.</summary>
    public static class RoommateSettings
    {
        public const string EnabledId = "home_roommates";

        public static bool Enabled = true;

        public static void Load()
        {
            Enabled = UnityEngine.PlayerPrefs.GetInt(OptionKeysRules.Key(EnabledId), Enabled ? 1 : 0) != 0;
        }
    }

    /// <summary>The doors to the features the roommate leans on, filled by Wiring/RoommateLinks; with a door left null it does without.</summary>
    public static class RoommateBridges
    {
        public static Func<bool> PartnerLivesIn;
        public static Func<string> PartnerName;

        /// <summary>Fills the list with the friends who could be asked to move in (social life); returns how many.</summary>
        public static Func<List<SocialPerson>, int> Friends;
        /// <summary>Tells social life that things moved between the player and one of its people: place, affection, trust.</summary>
        public static Action<int, float, float> FriendMoved;
        /// <summary>A roommate becomes one of the people of social life; returns their place in the cast, or -1 if there is none free.</summary>
        public static Func<Flatmate, int> MakeFriend;
        /// <summary>They introduce somebody the player has not met; returns that person's name, or null if nobody could be.</summary>
        public static Func<int, string> Introduce;
        public static Func<int, bool> IsPartner;

        /// <summary>The partner's body while they are shown in the home, or null: the roommate keeps off where they are.</summary>
        public static Func<UnityEngine.Transform> PartnerBody;

        public static void Reset()
        {
            PartnerBody = null;
            PartnerLivesIn = null;
            PartnerName = null;
            Friends = null;
            FriendMoved = null;
            MakeFriend = null;
            Introduce = null;
            IsPartner = null;
        }
    }

    /// <summary>The feature's entry in the feature list, with the Guide's figures read from <see cref="RoommateRules"/> when the page opens.</summary>
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
        public static void Load()
        {
            Roommates.RoommateSettings.Load();
        }

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
