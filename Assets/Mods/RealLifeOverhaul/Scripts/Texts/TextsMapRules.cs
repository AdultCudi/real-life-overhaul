using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// Which trained intent each of the phone's written messages is, by the line's own key; a key with none keeps its
    /// written line. Game-free. COST: a few string comparisons a message.
    /// </summary>
    public static class TextsMapRules
    {
        /// <summary>Who a phone message is from, as the asker says it.</summary>
        public enum From { Friend, Partner, Mom, InLaws, Roommate, Boss, Lender }

        // The written lines' pools, without their number, built so the catalogue's check does not read them as keys.
        private const string Social = "reallife" + "_social_text_";
        private const string Family = "reallife" + "_family_";
        private const string CityStem = "reallife" + "_texts_";   // data v6's life texts and gossip

        /// <summary>A phone message's intent and branch from its written line's key; null for a line with none.</summary>
        public static string Phone(string key, From from, out string branch)
        {
            branch = null;
            if (key == null)
            {
                return null;
            }
            string pool = Pool(key);
            string city = City(pool, out string cityBranch);
            if (city != null)
            {
                branch = cityBranch;
                return city;   // data v6: a text after the player's own event, or gossip
            }
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
                case Social + "nextday_good": return from == From.Friend ? "friend.thanks" : null;   // the day after a good outing: thanks for it, a card every pack has
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

        /// <summary>The written lines of data v6's life events and gossip by their pool, one row each: "pool|intent|branch".</summary>
        public static readonly string[] CityKeys =
        {
            "reallife_texts_life_partner_crash|partner.crash|", "reallife_texts_life_mom_news_crash|mom.news.crash|",
            "reallife_texts_life_partner_court_guilty|partner.court|guilty", "reallife_texts_life_partner_court_cleared|partner.court|cleared",
            "reallife_texts_life_mom_news_court_guilty|mom.news.court|guilty", "reallife_texts_life_mom_news_court_cleared|mom.news.court|cleared",
            "reallife_texts_life_mom_news_graduated|mom.news.graduated|", "reallife_texts_life_friend_graduated|friend.graduated|",
            "reallife_texts_life_friend_listed|friend.listed|", "reallife_texts_life_mom_news_listed|mom.news.listed|",
            "reallife_texts_life_friend_congrats_open|friend.congrats_open|", "reallife_texts_life_mom_news_opened|mom.news.opened|",
            "reallife_texts_life_friend_married|friend.married|", "reallife_texts_life_mom_news_baby_born|mom.news.baby|born",
            "reallife_texts_life_mom_news_baby_adopted|mom.news.baby|adopted", "reallife_texts_life_friend_baby_born|friend.baby|born",
            "reallife_texts_life_friend_baby_adopted|friend.baby|adopted", "reallife_texts_life_friend_win|friend.win|",
            "reallife_texts_life_partner_loss|partner.loss|", "reallife_texts_life_friend_loss|friend.loss|",
            "reallife_texts_gossip_crash|friend.gossip|crash", "reallife_texts_gossip_arrest|friend.gossip|arrest", "reallife_texts_gossip_court|friend.gossip|court",
            "reallife_texts_gossip_opened|friend.gossip|opened", "reallife_texts_gossip_graduated|friend.gossip|graduated", "reallife_texts_gossip_listed|friend.gossip|listed",
            "reallife_texts_gossip_married|friend.gossip|married", "reallife_texts_gossip_baby|friend.gossip|baby"
        };

        // A pool of data v6's life texts or gossip: its intent and branch; null for any other pool.
        private static string City(string pool, out string branch)
        {
            branch = null;
            if (!pool.StartsWith(CityStem, StringComparison.Ordinal))
            {
                return null;
            }
            for (int i = 0; i < CityKeys.Length; i++)
            {
                string row = CityKeys[i];
                int a = row.IndexOf('|'), b = row.IndexOf('|', a + 1);
                if (string.CompareOrdinal(row, 0, pool, 0, Math.Max(a, pool.Length)) == 0 && pool.Length == a)
                {
                    branch = b + 1 < row.Length ? row.Substring(b + 1) : null;
                    return row.Substring(a + 1, b - a - 1);
                }
            }
            return null;
        }

        /// <summary>The card that answers the player's typed words on a call: data v3's, for a friend or the partner; null for anybody else.</summary>
        public static string Answering(From from)
        {
            switch (from)
            {
                case From.Friend: return "friend.dm.answer";
                case From.Partner: return "partner.dm.answer";
                default: return null;
            }
        }

        /// <summary>A City Ledger piece's intent by its key, and the names its values go by; null for a piece with none.</summary>
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

        /// <summary>The key a piece's words are kept under: its day, and its key and values hashed alike on every machine.</summary>
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

        /// <summary>A key without its number at the end: "..._crash_2" is of "..._crash".</summary>
        public static string Pool(string key)
        {
            int end = key.Length;
            while (end > 0 && char.IsDigit(key[end - 1]))
            {
                end--;
            }
            return end < key.Length && end > 0 && key[end - 1] == '_' ? key.Substring(0, end - 1) : key;
        }

        /// <summary>What a friend's small-talk line is about, in the training's words; null for one of the day's.</summary>
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

        /// <summary>The holiday of Mom's invitation in the training's words where it is the same; else the game's own English name.</summary>
        public static string Holiday(string english) => english;

        // For the checks.

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

        // What each key's moment is, said plainly, against the intent the map gives.
        private static readonly string[][] Expect =
        {
            new[] { "reallife_social_text_crash_1", "friend.crash" }, new[] { "reallife_social_text_news_2", "friend.news.bad" },
            new[] { "reallife_social_text_news_1", "friend.news.good" }, new[] { "reallife_social_text_cancel_1", "friend.cancel.work" },
            new[] { "reallife_social_text_cancel_rain_2", "friend.cancel.rain" }, new[] { "reallife_social_text_arrest_mom_1", "mom.news.arrest" },
            new[] { "reallife_social_invite_meal", "friend.invite.meal" }, new[] { "reallife_social_text_love_2", "partner.affection" },
            new[] { "reallife_family_arrest_3", "partner.arrest" }, new[] { "reallife_family_mom_birthday", "mom.grandchild_birthday" },
            new[] { "reallife_family_inlaws_grandchild", "inlaws.baby" }, new[] { "reallife_text_roommate_notice", "roommate.notice" },
            new[] { "reallife_career_text_noshow", "boss.noshow" }, new[] { "reallife_text_shark_warn", "lender.warn" },
            new[] { "reallife_social_invite_drive", null }, new[] { "reallife_social_text_breakup_1", null }, new[] { "reallife_social_text_where", null },
            new[] { "reallife_social_text_nextday_good_2", "friend.thanks" }, new[] { "reallife_social_text_nextday_bad_1", null },
            new[] { "reallife_social_text_nextday_date_good_1", null }, new[] { "reallife_social_text_nextday_mom_bad_2", null }
        };

        /// <summary>The cases checked, for the checks' line.</summary>
        public static int Cases => Expect.Length + PhoneKeys.Length + 3;

        /// <summary>Every intent the phone's map can give; the checks find each in TextIntents.</summary>
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
