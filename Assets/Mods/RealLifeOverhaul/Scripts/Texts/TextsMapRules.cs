using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// Which trained intent each of the game's written messages is (the map for the phone: friends, Mom, the partner, the
    /// in-laws, roommates, the boss and the lender), by the written line's own key: the game picks the line as before, and
    /// the line's key says what the moment is. A key with no intent here keeps its written line. Flare's map is its own
    /// (Social/Flare/FlareTextsMap). Game-free (EconomyCheck, TextsChecks.cs: every key maps to the intent of the same
    /// moment, every intent exists). COST: a few string comparisons a message.
    /// </summary>
    public static class TextsMapRules
    {
        /// <summary>Who a phone message is from, as the asker says it.</summary>
        public enum From { Friend, Partner, Mom, InLaws, Roommate, Boss, Lender }

        // The written lines' pools, without their number (built, so the catalogue's check does not read them as keys).
        private const string Social = "reallife" + "_social_text_";
        private const string Family = "reallife" + "_family_";

        /// <summary>A phone message's intent and branch from its written line's key; null for a line with none.</summary>
        public static string Phone(string key, From from, out string branch)
        {
            branch = null;
            if (key == null)
            {
                return null;
            }
            string pool = Pool(key);
            switch (pool)
            {
                // friends (Social/SocialCatalogue's pools)
                case Social + "news": return key == "reallife_social_text_news_2" || key == "reallife_social_text_news_5" || key == "reallife_social_text_news_8"
                                                         || key == "reallife_social_text_news_9" ? "friend.news.bad" : "friend.news.good";
                case Social + "badday": return "friend.news.bad";
                case Social + "cancel": return key == "reallife_social_text_cancel_1" ? "friend.cancel.work" : "friend.cancel.home";
                case Social + "cancel_rain": return "friend.cancel.rain";
                case Social + "stoodup": return "friend.stood_up";
                case Social + "thanks": return "friend.thanks";
                case Social + "birthday_own": return "friend.birthday_own";
                case Social + "birthday_missed": return "friend.birthday_missed";
                case Social + "rich_kind":
                case Social + "rich_sour": return "friend.rich";
                case Social + "arrest_kind":
                case Social + "arrest_stern": return from == From.Partner ? null : "friend.arrest";
                case Social + "arrest_mom": return "mom.news.arrest";
                case Social + "crash": return "friend.crash";
                case Social + "breakin": return "friend.breakin";
                case Social + "missed": return "friend.missed_call";
                case Social + "drift": return "friend.drift";
                case Social + "loan_back": return "friend.loan_back";
                case Social + "loan_excuse": return "friend.loan_excuse";
                case Social + "debt": return "friend.debt";
                case Social + "love": return "partner.affection";
                case Social + "intro": return "friend.introduce";
                case Social + "lift": return "friend.lift";
                case Social + "loan": return "friend.ask_loan";

                // the family (Family/: HardTimes, HomeLife, Children, Relatives, Courtship)
                case Family + "arrest":
                    branch = key == "reallife_family_arrest_1" ? "minor" : "serious";
                    return "partner.arrest";
                case Family + "stoodup": return "partner.stood_up";
                case Family + "neglect": return "partner.missing";
                case Family + "mom_visit": return "mom.visit";
                case Family + "holiday_invite": return "mom.holiday";
                case Family + "holiday_missed": return "mom.holiday_missed";
            }
            switch (key)
            {
                case "reallife_social_invite_coffee": return "friend.invite.coffee";
                case "reallife_social_invite_meal": return "friend.invite.meal";
                case "reallife_social_invite_drinks": return "friend.invite.drinks";
                case "reallife_social_invite_cinema": return "friend.invite.cinema";
                case "reallife_social_invite_walk": return "friend.invite.walk";
                case "reallife_social_invite_workout": return "friend.invite.workout";
                case "reallife_family_appointment_missed": return "partner.appointment_missed";
                case "reallife_family_mom_moved_in": return "mom.news.moved_in";
                case "reallife_family_mom_engaged": return "mom.news.engaged";
                case "reallife_family_mom_married": return "mom.news.married";
                case "reallife_family_mom_expecting": return "mom.news.expecting";
                case "reallife_family_mom_separated": return "mom.news.separated";
                case "reallife_family_mom_arrest": return "mom.news.arrest";
                case "reallife_family_mom_birthday": return "mom.grandchild_birthday";
                case "reallife_family_inlaws_welcome": return "inlaws.dinner";
                case "reallife_family_inlaws_married": return "inlaws.married";
                case "reallife_family_inlaws_grandchild": return "inlaws.baby";
                case "reallife_text_roommate_rent_late": return "roommate.rent_late";
                case "reallife_text_roommate_rent_short": return "roommate.rent_short";
                case "reallife_text_roommate_cooked": return "roommate.cooked";
                case "reallife_text_roommate_borrow": return "roommate.borrow";
                case "reallife_text_roommate_notice":
                case "reallife_text_roommate_notice_row":
                case "reallife_text_roommate_partner_go":
                case "reallife_text_roommate_no_bed": return "roommate.notice";
                case "reallife_text_roommate_rent_slips": return "roommate.rent_late";
                case "reallife_career_text_noshow": return "boss.noshow";
                case "reallife_career_text_review_due": return "boss.review_due";
                case "reallife_career_text_dismissed": return "boss.dismissed";
                case "reallife_text_shark_intro": return "lender.intro";
                case "reallife_text_shark_due": return "lender.due";
                case "reallife_text_shark_warn": return "lender.warn";
                default: return null;
            }
        }

        /// <summary>A piece of the City Ledger's intent by its key (the city's economy's briefs), and the names its values go by; null for a piece with none.</summary>
        public static string Ledger(string key)
        {
            switch (key)
            {
                case "reallife_macro_news_prices_up": return "ledger.prices_up";
                case "reallife_macro_news_prices_down": return "ledger.prices_down";
                case "reallife_macro_news_rate_up": return "ledger.rate_up";
                case "reallife_macro_news_rate_down": return "ledger.rate_down";
                case "reallife_macro_news_district_up": return "ledger.district_up";
                case "reallife_macro_news_district_down": return "ledger.district_down";
                default: return null;
            }
        }

        /// <summary>
        /// The key a piece's words are kept under: its edition's day, and its key and values hashed (FNV-1a, the same on every
        /// machine, so a guest of a session finds the host's words for the same piece; the day keeps the same news of two
        /// editions apart).
        /// </summary>
        public static string LedgerKey(int day, string key, string[] data)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Fnv(h, key);
                for (int i = 0; data != null && i < data.Length; i++)
                {
                    h = Fnv(h, "|");
                    h = Fnv(h, data[i]);
                }
                return "lp:" + day.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + h.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static uint Fnv(uint h, string s)
        {
            unchecked
            {
                for (int i = 0; s != null && i < s.Length; i++)
                {
                    h = (h ^ s[i]) * 16777619u;
                }
                return h;
            }
        }

        /// <summary>A key without its number at the end (a crash text's "..._crash_2" is of "..._crash").</summary>
        public static string Pool(string key)
        {
            int end = key.Length;
            while (end > 0 && char.IsDigit(key[end - 1]))
            {
                end--;
            }
            return end < key.Length && end > 0 && key[end - 1] == '_' ? key.Substring(0, end - 1) : key;
        }

        /// <summary>What a friend's small-talk line is about (the written line's own happening, in the training's words), or null for one of the day's.</summary>
        public static string Happening(string key)
        {
            switch (key)
            {
                case "reallife_social_text_news_1": return "I saw a dog on the subway wearing a raincoat";
                case "reallife_social_text_news_2": return "the coffee machine at work broke";
                case "reallife_social_text_news_3": return "my landlord finally fixed the heating";
                case "reallife_social_text_news_4": return "the bakery on my street had fresh bread";
                case "reallife_social_text_news_5": return "I got a parking ticket";
                case "reallife_social_text_news_7": return "work let us go early";
                case "reallife_social_text_news_8": return "the lift in my building is stuck again";
                case "reallife_social_text_news_9": return "somebody on my street is learning the trumpet";
                case "reallife_social_text_news_10": return "I found twenty dollars in an old coat";
                case "reallife_social_text_news_12": return "my boss said good job today";
                default: return null;
            }
        }

        /// <summary>The rival tycoons' own manner (common.TYCOONS' style), by their place in the rivals' catalogue.</summary>
        public static readonly string[] TycoonStyles =
        {
            "boastful, rockets and cars, promises next year, asks for the manager",
            "smooth, smug, everything is for sale",
            "stiff, oddly polite, data and privacy, insists he is human"
        };

        /// <summary>The holidays of Mom's invitations in the training's words where the game's are the same (Thanksgiving); else the game's own English name.</summary>
        public static string Holiday(string english) => english;

        // ---------------------------------------------------------------- for the checks

        private static readonly string[] PhoneKeys =
        {
            "reallife_social_text_news_1", "reallife_social_text_news_2", "reallife_social_text_news_11", "reallife_social_text_badday_2", "reallife_social_text_cancel_1",
            "reallife_social_text_cancel_3", "reallife_social_text_cancel_rain_2", "reallife_social_text_stoodup_4", "reallife_social_text_thanks_1",
            "reallife_social_text_birthday_own_2", "reallife_social_text_birthday_missed_1", "reallife_social_text_rich_kind_1", "reallife_social_text_rich_sour_2",
            "reallife_social_text_arrest_kind_1", "reallife_social_text_arrest_stern_2", "reallife_social_text_arrest_mom_1", "reallife_social_text_crash_1",
            "reallife_social_text_breakin_2", "reallife_social_text_missed_1", "reallife_social_text_drift_3", "reallife_social_text_loan_back_1",
            "reallife_social_text_loan_excuse_2", "reallife_social_text_debt_1", "reallife_social_text_love_2", "reallife_social_text_intro_1", "reallife_social_text_lift_1",
            "reallife_social_text_loan_2", "reallife_social_invite_coffee", "reallife_social_invite_meal", "reallife_social_invite_drinks", "reallife_social_invite_cinema",
            "reallife_social_invite_walk", "reallife_social_invite_workout", "reallife_family_arrest_1", "reallife_family_arrest_3", "reallife_family_stoodup_1",
            "reallife_family_neglect_2", "reallife_family_appointment_missed", "reallife_family_mom_moved_in", "reallife_family_mom_engaged", "reallife_family_mom_married",
            "reallife_family_mom_expecting", "reallife_family_mom_separated", "reallife_family_mom_arrest", "reallife_family_mom_birthday", "reallife_family_mom_visit_2",
            "reallife_family_holiday_invite_1", "reallife_family_holiday_missed_2", "reallife_family_inlaws_welcome", "reallife_family_inlaws_married",
            "reallife_family_inlaws_grandchild", "reallife_text_roommate_rent_late", "reallife_text_roommate_rent_short", "reallife_text_roommate_cooked",
            "reallife_text_roommate_borrow", "reallife_text_roommate_notice", "reallife_career_text_noshow", "reallife_career_text_review_due", "reallife_career_text_dismissed",
            "reallife_text_shark_intro", "reallife_text_shark_due", "reallife_text_shark_warn"
        };

        // What each key's moment is, said plainly, against the intent the map gives (a wrong map shows here).
        private static readonly string[][] Expect =
        {
            new[] { "reallife_social_text_crash_1", "friend.crash" }, new[] { "reallife_social_text_news_2", "friend.news.bad" },
            new[] { "reallife_social_text_news_1", "friend.news.good" }, new[] { "reallife_social_text_cancel_1", "friend.cancel.work" },
            new[] { "reallife_social_text_cancel_rain_2", "friend.cancel.rain" }, new[] { "reallife_social_text_arrest_mom_1", "mom.news.arrest" },
            new[] { "reallife_social_invite_meal", "friend.invite.meal" }, new[] { "reallife_social_text_love_2", "partner.affection" },
            new[] { "reallife_family_arrest_3", "partner.arrest" }, new[] { "reallife_family_mom_birthday", "mom.grandchild_birthday" },
            new[] { "reallife_family_inlaws_grandchild", "inlaws.baby" }, new[] { "reallife_text_roommate_notice", "roommate.notice" },
            new[] { "reallife_career_text_noshow", "boss.noshow" }, new[] { "reallife_text_shark_warn", "lender.warn" },
            new[] { "reallife_social_invite_drive", null }, new[] { "reallife_social_text_breakup_1", null }, new[] { "reallife_social_text_where", null }
        };

        /// <summary>The cases checked (for the checks' line).</summary>
        public static int Cases => Expect.Length + PhoneKeys.Length + 3;

        /// <summary>Every intent the phone's map can give (the checks find each in TextIntents).</summary>
        public static List<string> Mapped()
        {
            var list = new List<string>(64);
            for (int i = 0; i < PhoneKeys.Length; i++)
            {
                string intent = Phone(PhoneKeys[i], From.Friend, out string _);
                if (intent != null && !list.Contains(intent))
                {
                    list.Add(intent);
                }
            }
            foreach (string key in new[] { "reallife_macro_news_prices_up", "reallife_macro_news_prices_down", "reallife_macro_news_rate_up", "reallife_macro_news_rate_down",
                "reallife_macro_news_district_up", "reallife_macro_news_district_down" })
            {
                list.Add(Ledger(key));
            }
            return list;
        }

        /// <summary>Where the map is wrong: a key that maps to nothing it should, or to the wrong moment. Empty when it is right.</summary>
        public static List<string> Problems()
        {
            var problems = new List<string>();
            for (int i = 0; i < PhoneKeys.Length; i++)
            {
                if (Phone(PhoneKeys[i], From.Friend, out string _) == null)
                {
                    problems.Add(PhoneKeys[i] + " maps to nothing");
                }
            }
            for (int i = 0; i < Expect.Length; i++)
            {
                string got = Phone(Expect[i][0], From.Friend, out string _);
                if (got != Expect[i][1])
                {
                    problems.Add(Expect[i][0] + " maps to " + (got ?? "nothing") + ", not " + (Expect[i][1] ?? "nothing"));
                }
            }
            if (Phone("reallife_family_arrest_1", From.Partner, out string minor) != "partner.arrest" || minor != "minor"
                || Phone("reallife_family_arrest_2", From.Partner, out string serious) != "partner.arrest" || serious != "serious")
            {
                problems.Add("the partner's arrest lines map to the minor and serious cards");
            }
            if (Ledger("reallife_macro_news_rate_up") != "ledger.rate_up" || Ledger("reallife_macro_news_rate_hold") != null || Ledger("reallife_macro_news_cycle_boom") != null
                || LedgerKey(4, "k", new[] { "a", "1" }) != LedgerKey(4, "k", new[] { "a", "1" }) || LedgerKey(4, "k", new[] { "a", "1" }) == LedgerKey(4, "k", new[] { "a", "2" })
                || LedgerKey(4, "k", null) != "lp:4:" + "ee0c38ea" || LedgerKey(4, "k", null) == LedgerKey(5, "k", null))
            {
                problems.Add("the paper's briefs map by their key, a rate held or the economy's state keep their written lines, and a piece's key is the same on every machine");
            }
            if (Pool(Social + "crash_12") != Social + "crash" || Pool("reallife_social_invite_coffee") != "reallife_social_invite_coffee")
            {
                problems.Add("a key's pool is its key without the number at the end");
            }
            if (Happening("reallife_social_text_news_3") != "my landlord finally fixed the heating" || Happening("reallife_social_text_news_11") != null)
            {
                problems.Add("a small-talk line's own happening");
            }
            return problems;
        }
    }
}
