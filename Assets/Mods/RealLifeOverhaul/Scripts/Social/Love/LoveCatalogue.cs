using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The love life's words: the pools of lines every moment draws from, by a seeded roll (<see cref="LoveRules.Variant"/>) so the same
    /// moment does not read the same twice in a week. Every key is written whole, for the catalogue check. Game-free.
    /// </summary>
    public static class LoveCatalogue
    {
        // ---------------------------------------------------------------- asking out while taken

        public static readonly string[] Warn = { "reallife_love_warn_1", "reallife_love_warn_2", "reallife_love_warn_3" };

        // ---------------------------------------------------------------- signs

        /// <summary>A text from the one who minds: somebody saw the player somewhere with somebody.</summary>
        public static readonly string[] SignSeen =
        {
            "reallife_love_sign_seen_1", "reallife_love_sign_seen_2", "reallife_love_sign_seen_3", "reallife_love_sign_seen_4", "reallife_love_sign_seen_5", "reallife_love_sign_seen_6"
        };

        /// <summary>A text from a friend of both: not their business, but.</summary>
        public static readonly string[] SignFriend =
        {
            "reallife_love_sign_friend_1", "reallife_love_sign_friend_2", "reallife_love_sign_friend_3", "reallife_love_sign_friend_4"
        };

        /// <summary>A text from the one who minds about a picture on Flare.</summary>
        public static readonly string[] SignPost =
        {
            "reallife_love_sign_post_1", "reallife_love_sign_post_2", "reallife_love_sign_post_3", "reallife_love_sign_post_4"
        };

        /// <summary>The notice after two plans on one day close together: the comedy of it.</summary>
        public static readonly string[] SignClose =
        {
            "reallife_love_sign_close_1", "reallife_love_sign_close_2", "reallife_love_sign_close_3", "reallife_love_sign_close_4", "reallife_love_sign_close_5",
            "reallife_love_sign_close_6"
        };

        // ---------------------------------------------------------------- found out

        public static readonly string[][] CaughtText =
        {
            new[] { "reallife_love_caught_seen_1", "reallife_love_caught_seen_2", "reallife_love_caught_seen_3", "reallife_love_caught_seen_4" },
            new[] { "reallife_love_caught_friend_1", "reallife_love_caught_friend_2", "reallife_love_caught_friend_3" },
            new[] { "reallife_love_caught_post_1", "reallife_love_caught_post_2", "reallife_love_caught_post_3" },
            new[] { "reallife_love_caught_night_1", "reallife_love_caught_night_2", "reallife_love_caught_night_3" },
            new[] { "reallife_love_caught_child_1", "reallife_love_caught_child_2", "reallife_love_caught_child_3" }
        };

        /// <summary>The text when they share the player's home: they are waiting there.</summary>
        public static readonly string[] CaughtHome = { "reallife_love_caught_home_1", "reallife_love_caught_home_2", "reallife_love_caught_home_3" };

        public static readonly string[] Reminder = { "reallife_love_remind_1", "reallife_love_remind_2", "reallife_love_remind_3" };
        public static readonly string[] Lapsed = { "reallife_love_lapse_1", "reallife_love_lapse_2", "reallife_love_lapse_3" };

        // ---------------------------------------------------------------- the confrontation

        public static readonly string[][] ConfrontOpen =
        {
            new[] { "reallife_love_conf_seen_1", "reallife_love_conf_seen_2", "reallife_love_conf_seen_3" },
            new[] { "reallife_love_conf_friend_1", "reallife_love_conf_friend_2" },
            new[] { "reallife_love_conf_post_1", "reallife_love_conf_post_2" },
            new[] { "reallife_love_conf_night_1", "reallife_love_conf_night_2" },
            new[] { "reallife_love_conf_child_1", "reallife_love_conf_child_2" }
        };

        public static readonly string[] Forgave = { "reallife_love_forgive_1", "reallife_love_forgive_2", "reallife_love_forgive_3", "reallife_love_forgive_4" };
        public static readonly string[] Refused = { "reallife_love_refuse_1", "reallife_love_refuse_2", "reallife_love_refuse_3", "reallife_love_refuse_4" };
        public static readonly string[] DenyHolds = { "reallife_love_holds_1", "reallife_love_holds_2", "reallife_love_holds_3" };
        public static readonly string[] DenyFails = { "reallife_love_fails_1", "reallife_love_fails_2", "reallife_love_fails_3" };
        public static readonly string[] DenyDouble = { "reallife_love_fails_double_1", "reallife_love_fails_double_2" };
        public static readonly string[] EndedOther = { "reallife_love_endother_1", "reallife_love_endother_2", "reallife_love_endother_3" };
        public static readonly string[] EndedThis = { "reallife_love_endthis_1", "reallife_love_endthis_2", "reallife_love_endthis_3" };

        /// <summary>The other person, who hears of it: a text.</summary>
        public static readonly string[] OtherHears =
        {
            "reallife_love_other_1", "reallife_love_other_2", "reallife_love_other_3", "reallife_love_other_4"
        };

        /// <summary>The player ended it with somebody in the same breath: what that person says back.</summary>
        public static readonly string[] EndedByYou = { "reallife_love_byyou_1", "reallife_love_byyou_2", "reallife_love_byyou_3" };

        /// <summary>Mom hears of it, in her own ways.</summary>
        public static readonly string[] MomLeft = { "reallife_love_mom_left_1", "reallife_love_mom_left_2", "reallife_love_mom_left_3" };
        public static readonly string[] MomStayed = { "reallife_love_mom_stayed_1", "reallife_love_mom_stayed_2", "reallife_love_mom_stayed_3" };

        /// <summary>The in-laws, who hear of it from the partner's side: only where there are in-laws.</summary>
        public static readonly string[] InLaws = { "reallife_love_inlaws_1", "reallife_love_inlaws_2", "reallife_love_inlaws_3" };

        // ---------------------------------------------------------------- a night

        public static readonly string[] OfferDrinks =
        {
            "reallife_love_offer_drinks_1", "reallife_love_offer_drinks_2", "reallife_love_offer_drinks_3", "reallife_love_offer_drinks_4", "reallife_love_offer_drinks_5",
            "reallife_love_offer_drinks_6"
        };

        public static readonly string[] OfferTipsy = { "reallife_love_offer_tipsy_1", "reallife_love_offer_tipsy_2", "reallife_love_offer_tipsy_3" };

        public static readonly string[] OfferParty = { "reallife_love_offer_party_1", "reallife_love_offer_party_2", "reallife_love_offer_party_3", "reallife_love_offer_party_4" };

        public static readonly string[] SaysYes = { "reallife_love_yes_1", "reallife_love_yes_2", "reallife_love_yes_3" };
        public static readonly string[] SaysNo = { "reallife_love_no_1", "reallife_love_no_2", "reallife_love_no_3" };
        public static readonly string[] Declined = { "reallife_love_decline_1", "reallife_love_decline_2", "reallife_love_decline_3" };

        /// <summary>The morning's first lines: where the player wakes, by whose place it was.</summary>
        public static readonly string[] MorningTheirs = { "reallife_love_morning_theirs_1", "reallife_love_morning_theirs_2", "reallife_love_morning_theirs_3" };
        public static readonly string[] MorningMine = { "reallife_love_morning_mine_1", "reallife_love_morning_mine_2", "reallife_love_morning_mine_3" };

        /// <summary>A line after drinks: how the head is.</summary>
        public static readonly string[] MorningHead = { "reallife_love_head_1", "reallife_love_head_2", "reallife_love_head_3" };

        /// <summary>A line of guilt for the player who is with somebody.</summary>
        public static readonly string[] MorningGuilt = { "reallife_love_guilt_1", "reallife_love_guilt_2", "reallife_love_guilt_3" };

        public static readonly string[][] Outcome =
        {
            new[] { "reallife_love_out_warm_1", "reallife_love_out_warm_2", "reallife_love_out_warm_3", "reallife_love_out_warm_4", "reallife_love_out_warm_5" },
            new[] { "reallife_love_out_awkward_1", "reallife_love_out_awkward_2", "reallife_love_out_awkward_3", "reallife_love_out_awkward_4", "reallife_love_out_awkward_5" },
            new[] { "reallife_love_out_regret_1", "reallife_love_out_regret_2", "reallife_love_out_regret_3", "reallife_love_out_regret_4", "reallife_love_out_regret_5" },
            new[] { "reallife_love_out_more_1", "reallife_love_out_more_2", "reallife_love_out_more_3", "reallife_love_out_more_4", "reallife_love_out_more_5" },
            new[] { "reallife_love_out_around_1", "reallife_love_out_around_2", "reallife_love_out_around_3", "reallife_love_out_around_4" },
            new[] { "reallife_love_out_mishap_1", "reallife_love_out_mishap_2", "reallife_love_out_mishap_3", "reallife_love_out_mishap_4", "reallife_love_out_mishap_5", "reallife_love_out_mishap_6" }
        };

        /// <summary>The text they send the next day, by how it went; awkward and regret send none.</summary>
        public static readonly string[][] NextDay =
        {
            new[] { "reallife_love_text_warm_1", "reallife_love_text_warm_2", "reallife_love_text_warm_3", "reallife_love_text_warm_4" },
            null,
            null,
            new[] { "reallife_love_text_more_1", "reallife_love_text_more_2", "reallife_love_text_more_3" },
            null,
            new[] { "reallife_love_text_mishap_1", "reallife_love_text_mishap_2", "reallife_love_text_mishap_3" }
        };

        /// <summary>A friend's text when a night got around: for the player who is with somebody, and for the one who is not.</summary>
        public static readonly string[] AroundTaken = { "reallife_love_text_around_1", "reallife_love_text_around_2", "reallife_love_text_around_3" };
        public static readonly string[] AroundFree = { "reallife_love_text_around_free_1", "reallife_love_text_around_free_2" };

        public static readonly string[] DateOffer = { "reallife_love_dateoffer_1", "reallife_love_dateoffer_2", "reallife_love_dateoffer_3" };
        public static readonly string[] DateOfferOpen = { "reallife_love_dateoffer_open_1", "reallife_love_dateoffer_open_2" };

        // ---------------------------------------------------------------- a child of a night

        public static readonly string[] BabyNews = { "reallife_love_baby_news_1", "reallife_love_baby_news_2", "reallife_love_baby_news_3", "reallife_love_baby_news_4" };
        public static readonly string[] BabyMe = { "reallife_love_baby_me_1", "reallife_love_baby_me_2", "reallife_love_baby_me_3" };
        public static readonly string[] BabyOpen = { "reallife_love_baby_open_1", "reallife_love_baby_open_2" };
        public static readonly string[] BabyInvolved = { "reallife_love_baby_involved_1", "reallife_love_baby_involved_2" };
        public static readonly string[] BabySupport = { "reallife_love_baby_support_1", "reallife_love_baby_support_2" };
        public static readonly string[] BabyOut = { "reallife_love_baby_out_1", "reallife_love_baby_out_2" };
        public static readonly string[] BabyBorn = { "reallife_love_baby_born_1", "reallife_love_baby_born_2", "reallife_love_baby_born_3" };
        public static readonly string[] BabyBornOut = { "reallife_love_baby_born_out_1", "reallife_love_baby_born_out_2" };
        public static readonly string[] BabyMom = { "reallife_love_mom_baby_1", "reallife_love_mom_baby_2", "reallife_love_mom_baby_3" };
        public static readonly string[] BabyMomOut = { "reallife_love_mom_out_1", "reallife_love_mom_out_2" };
        public static readonly string[] SupportThanks = { "reallife_love_support_text_1", "reallife_love_support_text_2", "reallife_love_support_text_3" };
        public static readonly string[] SupportLate = { "reallife_love_support_late_1", "reallife_love_support_late_2" };

        // ---------------------------------------------------------------- a scandal

        public static readonly string[] StoryAffair =
        {
            "reallife_love_story_affair_1", "reallife_love_story_affair_2", "reallife_love_story_affair_3", "reallife_love_story_affair_4", "reallife_love_story_affair_5",
            "reallife_love_story_affair_6"
        };

        public static readonly string[] StoryChild = { "reallife_love_story_child_1", "reallife_love_story_child_2", "reallife_love_story_child_3", "reallife_love_story_child_4" };

        public static readonly string[] Gossip = { "reallife_love_gossip_1", "reallife_love_gossip_2", "reallife_love_gossip_3", "reallife_love_gossip_4" };

        public static readonly string[] FlareTalk = { "reallife_love_flare_1", "reallife_love_flare_2", "reallife_love_flare_3" };

        // ---------------------------------------------------------------- brought up in later talks

        public const int TopicNight = 90, TopicForgave = 91, TopicChild = 92;

        public static readonly SocialTopic[] Topics =
        {
            new SocialTopic
            {
                Id = TopicNight, Open = new[] { "reallife_love_topic_night_1", "reallife_love_topic_night_2", "reallife_love_topic_night_3" },
                ReplyA = "reallife_love_topic_night_a", ReplyB = "reallife_love_topic_night_b", ReactA = "reallife_love_topic_night_ra", ReactB = "reallife_love_topic_night_rb",
                BothFine = false, TypedGood = new[] { "reallife_love_topic_night_ra" }, TypedBad = new[] { "reallife_love_topic_night_rb" }, YesIs = SocialRead.Good, NoIs = SocialRead.Bad
            },
            new SocialTopic
            {
                Id = TopicForgave, Open = new[] { "reallife_love_topic_forgave_1", "reallife_love_topic_forgave_2" },
                ReplyA = "reallife_love_topic_forgave_a", ReplyB = "reallife_love_topic_forgave_b", ReactA = "reallife_love_topic_forgave_ra", ReactB = "reallife_love_topic_forgave_rb",
                BothFine = false, TypedGood = new[] { "reallife_love_topic_forgave_ra" }, TypedBad = new[] { "reallife_love_topic_forgave_rb" }, YesIs = SocialRead.Good, NoIs = SocialRead.Bad
            },
            new SocialTopic
            {
                Id = TopicChild, Open = new[] { "reallife_love_topic_child_1", "reallife_love_topic_child_2", "reallife_love_topic_child_3" },
                ReplyA = "reallife_love_topic_child_a", ReplyB = "reallife_love_topic_child_b", ReactA = "reallife_love_topic_child_ra", ReactB = "reallife_love_topic_child_rb",
                BothFine = true, TypedGood = new[] { "reallife_love_topic_child_ra" }, TypedBad = new[] { "reallife_love_topic_child_rb" }, YesIs = SocialRead.Good, NoIs = SocialRead.Unclear
            }
        };

        // ---------------------------------------------------------------- on the People page and the memories

        public static string MemoryKey(MemoryCode code)
        {
            switch (code)
            {
                case MemoryCode.Night: return "reallife_love_memory_night";
                case MemoryCode.NightAwkward: return "reallife_love_memory_awkward";
                case MemoryCode.NightRegret: return "reallife_love_memory_regret";
                case MemoryCode.CaughtOut: return "reallife_love_memory_caught";
                case MemoryCode.Forgave: return "reallife_love_memory_forgave";
                case MemoryCode.Denied: return "reallife_love_memory_denied";
                case MemoryCode.CoParent: return "reallife_love_memory_coparent";
                default: return null;
            }
        }

        // What comes next after living together, said on the partner's page in People (Family's state decides which).
        public static readonly string[] NextSteps =
        {
            "reallife_love_next_ring", "reallife_love_next_propose", "reallife_love_next_plan", "reallife_love_next_wedding", "reallife_love_next_married",
            "reallife_love_next_separated", "reallife_love_next_divorced", "reallife_love_next_hard", "reallife_love_next_apart"
        };

        public const string PeopleSecond = "reallife_love_people_second";
        public const string PeopleSecondFor = "reallife_love_people_second_for";
        public const string PeopleCoParent = "reallife_love_people_coparent";
        public const string PeopleDateOffer = "reallife_love_people_dateoffer";

        /// <summary>A pool's line for this moment, by the seeded variant.</summary>
        public static string Pick(string[] pool, int seed, int a, int b)
        {
            return pool[LoveRules.Variant(seed, a, b, pool.Length)];
        }

        public static string[] CaughtPool(CatchHow how)
        {
            int i = (int)how;
            return CaughtText[Math.Max(0, Math.Min(CaughtText.Length - 1, i))];
        }

        public static string[] ConfrontPool(CatchHow how)
        {
            int i = (int)how;
            return ConfrontOpen[Math.Max(0, Math.Min(ConfrontOpen.Length - 1, i))];
        }
    }
}
