using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The words of the living network: the city's people's posts, the official accounts', the businesses', the reports,
    /// reviews, announcements, invitations and answers, the replies, the notifications, the messages and how long ago.
    /// Every key written out in full (the locale check finds each one). Made when shown, never kept.
    /// </summary>
    public static partial class FlareText
    {
        private static readonly StringBuilder Live = new StringBuilder(256);

        private static readonly string[][] TalkKeys =
        {
            new[] { "reallife_flare_talk_weather_0", "reallife_flare_talk_weather_1", "reallife_flare_talk_weather_2" },
            new[] { "reallife_flare_talk_prices_0", "reallife_flare_talk_prices_1", "reallife_flare_talk_prices_2" },
            new[] { "reallife_flare_talk_crime_0", "reallife_flare_talk_crime_1", "reallife_flare_talk_crime_2" },
            new[] { "reallife_flare_talk_accident_0", "reallife_flare_talk_accident_1", "reallife_flare_talk_accident_2" },
            new[] { "reallife_flare_talk_event_0", "reallife_flare_talk_event_1", "reallife_flare_talk_event_2" },
            new[] { "reallife_flare_talk_tomorrow_0", "reallife_flare_talk_tomorrow_1", "reallife_flare_talk_tomorrow_2" },
            new[] { "reallife_flare_talk_election_0", "reallife_flare_talk_election_1", "reallife_flare_talk_election_2" },
            new[] { "reallife_flare_talk_roadworks_0", "reallife_flare_talk_roadworks_1", "reallife_flare_talk_roadworks_2" },
            new[] { "reallife_flare_talk_market_0", "reallife_flare_talk_market_1", "reallife_flare_talk_market_2" },
            new[] { "reallife_flare_talk_commute_0", "reallife_flare_talk_commute_1", "reallife_flare_talk_commute_2" },
            new[] { "reallife_flare_talk_policy_0", "reallife_flare_talk_policy_1", "reallife_flare_talk_policy_2" },
            new[] { "reallife_flare_talk_fire_0", "reallife_flare_talk_fire_1", "reallife_flare_talk_fire_2" },
            new[] { "reallife_flare_talk_everyday_0", "reallife_flare_talk_everyday_1", "reallife_flare_talk_everyday_2" },
            new[] { "reallife_flare_talk_night_0", "reallife_flare_talk_night_1", "reallife_flare_talk_night_2" },
            new[] { "reallife_flare_talk_food_0", "reallife_flare_talk_food_1", "reallife_flare_talk_food_2" },
            new[] { "reallife_flare_talk_sport_0", "reallife_flare_talk_sport_1", "reallife_flare_talk_sport_2" }
        };

        private static readonly string[] ReportPostKeys =
        {
            "reallife_flare_report_post_0", "reallife_flare_report_post_1", "reallife_flare_report_post_2", "reallife_flare_report_post_3",
            "reallife_flare_report_post_4", "reallife_flare_report_post_5", "reallife_flare_report_post_6", "reallife_flare_report_post_7"
        };

        /// <summary>A report's kind in a word or two, for its chip.</summary>
        public static readonly string[] ReportKeys =
        {
            "reallife_flare_report_0", "reallife_flare_report_1", "reallife_flare_report_2", "reallife_flare_report_3",
            "reallife_flare_report_4", "reallife_flare_report_5", "reallife_flare_report_6", "reallife_flare_report_7"
        };

        private static readonly string[] ReportWordKeys =
        {
            "reallife_flare_report_word_0", "reallife_flare_report_word_1", "reallife_flare_report_word_2", "reallife_flare_report_word_3",
            "reallife_flare_report_word_4", "reallife_flare_report_word_5", "reallife_flare_report_word_6", "reallife_flare_report_word_7"
        };

        private static readonly string[] ReviewKeys =
        {
            "reallife_flare_review_post_1", "reallife_flare_review_post_2", "reallife_flare_review_post_3", "reallife_flare_review_post_4", "reallife_flare_review_post_5"
        };

        /// <summary>An announcement's kind, for its chip.</summary>
        public static readonly string[] AnnounceKeys =
        {
            "reallife_flare_announce_0", "reallife_flare_announce_1", "reallife_flare_announce_2", "reallife_flare_announce_3", "reallife_flare_announce_4"
        };

        private static readonly string[] AnnouncePostKeys =
        {
            "reallife_flare_announce_post_0", "reallife_flare_announce_post_1", "reallife_flare_announce_post_2", "reallife_flare_announce_post_3", "reallife_flare_announce_post_4"
        };

        private static readonly string[][] CitizenReplyKeys =
        {
            new[] { "reallife_flare_cit_reply_agree_0", "reallife_flare_cit_reply_agree_1" },
            new[] { "reallife_flare_cit_reply_kind_0", "reallife_flare_cit_reply_kind_1" },
            new[] { "reallife_flare_cit_reply_funny_0", "reallife_flare_cit_reply_funny_1" },
            new[] { "reallife_flare_cit_reply_doubt_0", "reallife_flare_cit_reply_doubt_1" },
            new[] { "reallife_flare_cit_reply_angry_0", "reallife_flare_cit_reply_angry_1" },
            new[] { "reallife_flare_cit_reply_love_0", "reallife_flare_cit_reply_love_1" },
            new[] { "reallife_flare_cit_reply_question_0", "reallife_flare_cit_reply_question_1" }
        };

        private static readonly string[][] OfficialKeys =
        {
            new[] { "reallife_flare_official_notice_0", "reallife_flare_official_notice_1" },
            new[] { "reallife_flare_official_patrols_0", "reallife_flare_official_patrols_1" },
            new[] { "reallife_flare_official_advice_0", "reallife_flare_official_advice_1" },
            new[] { "reallife_flare_official_service_0", "reallife_flare_official_service_1" },
            new[] { "reallife_flare_official_outage_0", "reallife_flare_official_outage_1" },
            new[] { "reallife_flare_official_rate_0", "reallife_flare_official_rate_1" },
            new[] { "reallife_flare_official_policy_0", "reallife_flare_official_policy_1" },
            new[] { "reallife_flare_official_clean_0", "reallife_flare_official_clean_1" },
            new[] { "reallife_flare_official_election_0", "reallife_flare_official_election_1" }
        };

        private static readonly string[] OfficialNameKeys =
        {
            "reallife_flare_official_name_0", "reallife_flare_official_name_1", "reallife_flare_official_name_2", "reallife_flare_official_name_3", "reallife_flare_official_name_4"
        };

        /// <summary>The official accounts' handles: the same in every language.</summary>
        public static readonly string[] OfficialHandles = { "@cityoffice", "@citypolice", "@citytransit", "@cityutilities", "@citybank" };

        private static readonly string[] BusinessPostKeys =
        {
            "reallife_flare_biz_post_0", "reallife_flare_biz_post_1", "reallife_flare_biz_post_2", "reallife_flare_biz_post_3", "reallife_flare_biz_post_4",
            "reallife_flare_biz_post_5", "reallife_flare_biz_post_6"
        };

        private static readonly string[] AnswerKeys =
        {
            "reallife_flare_answer_0", "reallife_flare_answer_1", "reallife_flare_answer_2", "reallife_flare_answer_3", "reallife_flare_answer_4",
            "reallife_flare_answer_5", "reallife_flare_answer_6", "reallife_flare_answer_7", "reallife_flare_answer_8", "reallife_flare_answer_9",
            "reallife_flare_answer_10", "reallife_flare_answer_11", "reallife_flare_answer_12", "reallife_flare_answer_13", "reallife_flare_answer_14",
            "reallife_flare_answer_15"
        };

        private static readonly string[] InviteKeys = { "reallife_flare_invite_0", "reallife_flare_invite_1" };

        private static readonly string[] NoticeKeys =
        {
            "reallife_flare_notice_like", "reallife_flare_notice_reply", "reallife_flare_notice_repost", "reallife_flare_notice_mention",
            "reallife_flare_notice_case", "reallife_flare_notice_dm", "reallife_flare_notice_going", "reallife_flare_notice_review"
        };

        public static string OfficialName(int who)
        {
            return T(Of(OfficialNameKeys, who));
        }

        public static string OfficialHandle(int who)
        {
            return OfficialHandles[FlareRules.Clamp(who, 0, OfficialHandles.Length - 1)];
        }

        public static string ReportWord(FlareReport kind)
        {
            return T(Of(ReportWordKeys, (int)kind));
        }

        /// <summary>A name as a handle: "@" and its letters and figures, lower case.</summary>
        public static string HandleOf(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "@you";
            }
            Live.Length = 0;
            Live.Append('@');
            for (int i = 0; i < name.Length && Live.Length < 24; i++)
            {
                char c = char.ToLowerInvariant(name[i]);
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    Live.Append(c);
                }
            }
            return Live.Length > 1 ? Live.ToString() : "@you";
        }

        /// <summary>A district by the game's own id, in words; "the city" for none.</summary>
        public static string District(string id)
        {
            return id != null ? FlareGame.DistrictName(id) : T("reallife_flare_the_city");
        }

        /// <summary>An invitation's event in words: a street event by its name, or the event at a business.</summary>
        public static string InviteName(FlarePost p)
        {
            if (p.Source == FlareSource.Announce)
            {
                return Fill("reallife_flare_event_at", "business", p.Name);
            }
            return string.IsNullOrEmpty(p.Name) ? T("reallife_flare_event_city") : Localizor.LocalizorManager.IsLocalizedKey(p.Name) ? T(p.Name) : p.Name;
        }

        /// <summary>An hour of a day as the app writes a time: "Day 12, 19:00".</summary>
        public static string Hour(int when)
        {
            return when < 0 ? string.Empty : When(when / 24, when % 24);
        }

        /// <summary>How long ago, as a feed writes it: "now", "12m", "3h", or the day.</summary>
        public static string Ago(long at, long now)
        {
            long since = now - at;
            if (since < 1)
            {
                return T("reallife_flare_ago_now");
            }
            if (since < 60)
            {
                return Fill("reallife_flare_ago_min", "n", since.ToString());
            }
            if (since < 1440)
            {
                return Fill("reallife_flare_ago_hour", "n", (since / 60).ToString());
            }
            return Fill("reallife_flare_day", "day", (at / 1440).ToString());
        }

        /// <summary>
        /// The built part of a post of the living network, in words: what the city's people say, an official account's
        /// post, a business's, a report, a review, an announcement, an invitation, an answer, a reply of the city's people.
        /// Null for a post of another kind (FlareText.Post words it).
        /// </summary>
        public static string Built(FlarePost p, string author)
        {
            string district = District(p.District);
            switch (p.Source)
            {
                case FlareSource.CityTalk:
                    string[] talk = TalkKeys[FlareRules.Clamp(p.Arg / 10, 0, TalkKeys.Length - 1)];
                    return Fill(talk[FlareRules.Clamp(p.Arg % 10, 0, talk.Length - 1)], "district", district, "name", Named(p.Name), "event", Named(p.Name));
                case FlareSource.Report:
                    return Fill(Of(ReportPostKeys, p.Arg), "district", district, "name", p.Name ?? district);
                case FlareSource.Review:
                    return Fill(Of(ReviewKeys, p.Arg - 1), "name", p.Name);
                case FlareSource.Announce:
                    return Fill(Of(AnnouncePostKeys, p.Arg), "name", p.Name, "when", Hour(p.When));
                case FlareSource.Official:
                    string[] off = OfficialKeys[FlareRules.Clamp(p.Arg / 10, 0, OfficialKeys.Length - 1)];
                    int v = p.Arg / 10 == (int)FlareOfficialPost.Rate ? p.Arg % 10 : Pick(p.Seed, 1, off.Length) - 1;
                    return Fill(off[FlareRules.Clamp(v, 0, off.Length - 1)], "district", district, "name", p.Name);
                case FlareSource.BusinessPost:
                    return Fill(Of(BusinessPostKeys, p.Arg), "name", p.Name, "district", district);
                case FlareSource.Invite:
                    return Fill(InviteKeys[Pick(p.Seed, 1, InviteKeys.Length) - 1], "event", InviteName(p), "time", Hour(p.When));
                case FlareSource.Answer:
                    return Fill(Of(AnswerKeys, p.Arg), "district", district, "address", p.Name ?? district, "days", p.When > 0 ? p.When.ToString() : "1", "name", p.Name, "author", author);
                case FlareSource.Reply:
                    if (p.Author == FlareAuthor.Citizen)
                    {
                        string[] reply = CitizenReplyKeys[FlareRules.Clamp(p.Arg, 0, CitizenReplyKeys.Length - 1)];
                        return T(reply[Pick(p.Seed, 2, reply.Length) - 1]);
                    }
                    return null;
                case FlareSource.Life:
                    return p.Author == FlareAuthor.Player || p.Author == FlareAuthor.Network ? Fill("reallife_flare_post_now", "district", district) : null;
                default:
                    return null;
            }
        }

        // A name that may be a key (a street event's) or plain words.
        private static string Named(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return T("reallife_flare_something");
            }
            return Localizor.LocalizorManager.IsLocalizedKey(name) ? T(name) : name;
        }

        /// <summary>A notification's line: "{name} liked your post" and the like.</summary>
        public static string Notice(FlareNoticeKind kind, string name)
        {
            return Fill(Of(NoticeKeys, (int)kind), "name", name);
        }

        /// <summary>A message of the city's people, by its key and its names ("name=value~name=value").</summary>
        public static string DmWords(FlareDm m, string name)
        {
            if (m.Text != null)
            {
                return m.Text;
            }
            if (m.Key == null)
            {
                return string.Empty;
            }
            string text = Fill(m.Key, "name", name);
            if (!string.IsNullOrEmpty(m.Data))
            {
                string[] pairs = m.Data.Split('~');
                for (int i = 0; i < pairs.Length; i++)
                {
                    int eq = pairs[i].IndexOf('=');
                    if (eq > 0)
                    {
                        text = text.Replace("{" + pairs[i].Substring(0, eq) + "}", pairs[i].Substring(eq + 1));
                    }
                }
            }
            return text;
        }

        /// <summary>The key of a message of the city's people by what it is about (FlareLiveRules.CityDm), and of their answers to the player's.</summary>
        public static string DmKey(string what)
        {
            switch (what)
            {
                case "fan": return "reallife_flare_dm_fan";
                case "job": return "reallife_flare_dm_job";
                case "collab": return "reallife_flare_dm_collab";
                case "answer_collab": return "reallife_flare_dm_answer_collab";
                case "answer_office": return "reallife_flare_dm_answer_office";
                case "answer_chat": return "reallife_flare_dm_answer_chat";
                default: return "reallife_flare_dm_chat";
            }
        }

        /// <summary>A tag as shown: "#rain".</summary>
        public static string Tag(string tag)
        {
            return "#" + tag;
        }
    }
}
