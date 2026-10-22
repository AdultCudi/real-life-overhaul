using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a call back opens on (SocialFollowRules): the person's line about their text, its two answers and what each gets
    /// back, as one of the call's topics, and what the text was about in RLO Texts' card words.
    /// </summary>
    public sealed class SocialFollowSubject
    {
        public string Id;          // "news_good", "fm02": the text's moment
        public string Topic;       // what it was about, in the cards' English words ("the garage door"): a memory's "texted you about ..."
        public SocialTopic Talk;   // the call's topic: Open is their line about it; the answers, reactions and typed lines are its kind's
    }

    /// <summary>
    /// The texts a call back can open on, by their written line's key, and what is said then: one line of theirs about each
    /// (reallife_social_follow_*) and the answers of its kind, a kind's own (reallife_social_follow_laugh_a and the rest) or one
    /// of the twenty topics' where they fit. Every source that texts the people of the social life is here, by the keys it
    /// sends: the social life's own texts (and RLO Texts' news and gossip, which it sends), the family's (the partner, Mom and
    /// the old friend's wedding invitation), life's little moments (the old friend's and Mom's, Life/LifeEventCatalogue.People)
    /// and travel's (a nudge to take a break, a souvenir's thanks). What has no subject keeps the ordinary call: a welcome, an
    /// invitation or a favour asked (answered on the call already), plans and where somebody is waiting, a break-up, their own
    /// birthday (the call wishes it), the waters breaking, the partner's asks and wishes (talked over at home), their answer
    /// to the player's own text about a new home, and the family's own contact's and the in-laws'. Game-free (EconomyCheck,
    /// CallBackChecks.cs). COST: the tables are made once; a key is looked up in a dictionary.
    /// </summary>
    public static class SocialFollowCatalogue
    {
        public const int TopicBase = 100;   // a call back's topic ids: from here, so none is one of the twenty topics (0 to 19)

        // The keys' stems: the table's keys and pools are written from them, as the catalogue's check reads whole keys only (CallBackChecks finds
        // every key and pool of the table in the locales, and every line of a subject in en, pt and zh-cn).
        private const string Follow = "reallife" + "_social_follow_";
        private const string SocialText = "reallife" + "_social_text_";
        private const string Texts = "reallife" + "_texts_";
        private const string Family = "reallife" + "_family_";
        private const string Travel = "reallife" + "_travel_";

        // ---------------------------------------------------------------- the kinds of answer

        // A kind of answer: its two answers (the first the warm one) and what each gets back, and what typed words get back.
        private static SocialTopic Kind(string kind, bool bothFine, string[] typedGood, string[] typedBad, SocialRead yes, SocialRead no)
        {
            string stem = Follow + kind;
            return new SocialTopic
            {
                ReplyA = stem + "_a", ReplyB = stem + "_b", ReactA = stem + "_ra", ReactB = stem + "_rb", BothFine = bothFine,
                TypedGood = typedGood ?? new[] { stem + "_ra" }, TypedBad = typedBad ?? new[] { stem + "_rb" }, YesIs = yes, NoIs = no
            };
        }

        private static readonly SocialTopic Laugh = Kind("laugh", true, null, SocialCatalogue.TypedNeutral, SocialRead.Good, SocialRead.Unclear);
        private static readonly SocialTopic Sorry = Kind("sorry", false, null, new[] { Follow + "sorry_ra" }, SocialRead.Unclear, SocialRead.Unclear);
        private static readonly SocialTopic NextTime = Kind("next_time", true, null, null, SocialRead.Good, SocialRead.Bad);
        private static readonly SocialTopic Thanks = Kind("thanks", true, null, SocialCatalogue.TypedNeutral, SocialRead.Good, SocialRead.Unclear);
        private static readonly SocialTopic Gift = Kind("gift", true, null, SocialCatalogue.TypedNeutral, SocialRead.Good, SocialRead.Unclear);
        private static readonly SocialTopic Patience = Kind("patience", true, null, null, SocialRead.Unclear, SocialRead.Unclear);
        private static readonly SocialTopic Owe = Kind("owe", true, null, null, SocialRead.Good, SocialRead.Unclear);
        private static readonly SocialTopic Love = Kind("love", true, null, SocialCatalogue.TypedSorry, SocialRead.Good, SocialRead.Unclear);
        private static readonly SocialTopic Proud = Kind("proud", true, null, SocialCatalogue.TypedNeutral, SocialRead.Good, SocialRead.Unclear);
        private static readonly SocialTopic Comfort = Kind("comfort", true, SocialCatalogue.TypedGlad, new[] { Follow + "comfort_ra" }, SocialRead.Good, SocialRead.Bad);
        private static readonly SocialTopic Reassure = Kind("reassure", true, null, SocialCatalogue.TypedSorry, SocialRead.Good, SocialRead.Bad);
        private static readonly SocialTopic Plain = Kind("plain", true, SocialCatalogue.TypedGlad, SocialCatalogue.TypedSorry, SocialRead.Unclear, SocialRead.Unclear);
        private static readonly SocialTopic Rest = Kind("rest", true, null, null, SocialRead.Good, SocialRead.Bad);
        private static readonly SocialTopic Rearrange = Kind("rearrange", true, null, null, SocialRead.Unclear, SocialRead.Unclear);
        private static readonly SocialTopic Repaid = Kind("repaid", false, null, null, SocialRead.Good, SocialRead.Bad);
        private static readonly SocialTopic Come = Kind("come", true, null, null, SocialRead.Good, SocialRead.Bad);

        // The twenty topics' own answers, where they fit a call back as they are.
        private static SocialTopic Topic(int id) => SocialCatalogue.Topics[id];

        // ---------------------------------------------------------------- the subjects

        /// <summary>Every subject, in order; a subject's topic id is <see cref="TopicBase"/> and its place here.</summary>
        public static readonly SocialFollowSubject[] All;

        // The written lines' keys (or their pools, without the number at the end) each subject is the moment of.
        private static readonly Dictionary<string, SocialFollowSubject> ByKey = new Dictionary<string, SocialFollowSubject>(256, StringComparer.Ordinal);

        private static readonly List<SocialFollowSubject> Made = new List<SocialFollowSubject>(160);

        // A subject: its id, its card words, its kind of answer, and the keys or pools whose texts it is.
        private static void S(string id, string topic, SocialTopic kind, params string[] keys)
        {
            var talk = new SocialTopic
            {
                Id = TopicBase + Made.Count, Open = new[] { Follow + id }, ReplyA = kind.ReplyA, ReplyB = kind.ReplyB, ReactA = kind.ReactA,
                ReactB = kind.ReactB, BothFine = kind.BothFine, TypedGood = kind.TypedGood, TypedBad = kind.TypedBad, YesIs = kind.YesIs, NoIs = kind.NoIs
            };
            var subject = new SocialFollowSubject { Id = id, Topic = topic, Talk = talk };
            Made.Add(subject);
            for (int i = 0; i < keys.Length; i++)
            {
                ByKey[keys[i]] = subject;
            }
        }

        // A little moment of life's (Life/LifeEventCatalogue.People): its key is its subject's id.
        private static void L(string id, string topic, SocialTopic kind)
        {
            S(id, topic, kind, "reallife_ev_" + id);
        }

        static SocialFollowCatalogue()
        {
            // ---- the social life's own texts (Social/SocialCatalogue's pools); the news pool's bad days by their key, as RLO Texts' map has them
            S("news_good", "their day", Laugh, SocialText + "news");
            S("news_bad", "their day", Topic(SocialCatalogue.TopicBadDay), SocialText + "news_2", SocialText + "news_5", SocialText + "news_8",
                SocialText + "news_9", SocialText + "badday");
            S("cancel", "calling off a plan", Rearrange, SocialText + "cancel", SocialText + "cancel_rain");
            S("stoodup", "being stood up", Sorry, SocialText + "stoodup");
            S("thanks", "your time together", Topic(SocialCatalogue.TopicMemory), SocialText + "thanks");
            S("birthday_reminder", "a friend's birthday", Topic(SocialCatalogue.TopicBirthday), SocialText + "birthday_mom");
            S("birthday_missed", "their birthday", Sorry, SocialText + "birthday_missed");
            S("rich_kind", "your business", Topic(SocialCatalogue.TopicRich), SocialText + "rich_kind");
            S("rich_sour", "your business", Topic(SocialCatalogue.TopicRich), SocialText + "rich_sour");
            S("arrest", "your arrest", Topic(SocialCatalogue.TopicArrest), SocialText + "arrest_kind", SocialText + "arrest_stern", Texts + "gossip_arrest");
            S("arrest_mom", "your arrest", Topic(SocialCatalogue.TopicArrest), SocialText + "arrest_mom", Family + "mom_arrest");
            S("crash", "your crash", Topic(SocialCatalogue.TopicCrash), SocialText + "crash", Texts + "life_partner_crash", Texts + "life_mom_news_crash",
                Texts + "gossip_crash");
            S("breakin", "the break-in", Topic(SocialCatalogue.TopicBreakIn), SocialText + "breakin");
            S("missed_call", "a missed call", Plain, SocialText + "missed");
            S("drift", "keeping in touch", Sorry, SocialText + "drift");
            S("loan_back", "money", Repaid, SocialText + "loan_back");
            S("loan_excuse", "money", Patience, SocialText + "loan_excuse");
            S("debt", "money", Owe, SocialText + "debt");
            S("love", "you two", Love, SocialText + "love");
            S("home", "coming home", Love, SocialText + "home");
            S("intro", "a friend of theirs", Topic(SocialCatalogue.TopicTheirJob), SocialText + "intro");
            S("nextday_good", "your time together", Topic(SocialCatalogue.TopicMemory), SocialText + "nextday_good");
            S("nextday_bad", "your time together", Sorry, SocialText + "nextday_bad");
            S("nextday_date_good", "you two", Love, SocialText + "nextday_date_good");
            S("nextday_date_bad", "you two", Sorry, SocialText + "nextday_date_bad");
            S("nextday_mom_good", "your time together", Topic(SocialCatalogue.TopicMemory), SocialText + "nextday_mom_good");
            S("nextday_mom_bad", "your time together", Sorry, SocialText + "nextday_mom_bad");

            // ---- RLO Texts' news of the player's life and its gossip, sent by the social life (Texts/TextsCityRules.LifeKey, GossipKey)
            S("graduated", "your graduation", Proud, Texts + "life_mom_news_graduated", Texts + "life_friend_graduated", Texts + "gossip_graduated");
            S("listed", "your company", Proud, Texts + "life_friend_listed", Texts + "life_mom_news_listed", Texts + "gossip_listed");
            S("opened", "your business", Proud, Texts + "life_friend_congrats_open", Texts + "life_mom_news_opened", Texts + "gossip_opened");
            S("married", "your wedding", Proud, Texts + "life_friend_married", Texts + "gossip_married");
            S("baby", "your baby", Proud, Texts + "life_mom_news_baby_born", Texts + "life_mom_news_baby_adopted", Texts + "life_friend_baby_born",
                Texts + "life_friend_baby_adopted", Texts + "gossip_baby");
            S("win", "your big win", Proud, Texts + "life_friend_win");
            S("court_cleared", "your court case", Comfort, Texts + "life_partner_court_cleared", Texts + "life_mom_news_court_cleared");
            S("court_guilty", "your court case", Comfort, Texts + "life_partner_court_guilty", Texts + "life_mom_news_court_guilty", Texts + "gossip_court");
            S("loss", "your big loss", Comfort, Texts + "life_partner_loss", Texts + "life_friend_loss");

            // ---- the family: the partner (Family/: Children, Chores, HardTimes, HomeLife, WeddingDay, WeddingPlanner, PartnerVoice's thanks)
            S("appointment_missed", "the missed appointment", Sorry, Family + "appointment_missed");
            S("checkup_missed", "the check-up", Sorry, Family + "checkup_missed");
            S("birth_missed", "the birth", Sorry, Family + "birth_missed");
            S("chores", "the chores", Sorry, Family + "chore_missed", Family + "chore_groceries", Family + "chore_clean", Family + "chore_shower",
                Family + "chore_carwash");
            S("partner_arrest", "your arrest", Topic(SocialCatalogue.TopicArrest), Family + "arrest");
            S("counselling_missed", "counselling", Sorry, Family + "counselling_missed");
            S("apart", "time apart", Sorry, Family + "apart_theirs_text");
            S("watch_waited", "a night in", Sorry, Family + "watch_waited");
            S("dinner_stoodup", "being stood up", Sorry, Family + "stoodup");
            S("neglect", "keeping in touch", Love, Family + "neglect");
            S("wedding_missed", "the wedding", Sorry, Family + "wedding_missed_once", Family + "wedding_missed_twice");
            S("wedding_nerves", "the wedding", Love, Family + "coldfeet_theirs");
            S("wedding_eve", "the wedding", Love, Family + "eve_message");
            S("expecting", "the baby on the way", Laugh, Family + "expecting");
            S("partner_thanks", "what you did for them", Gift, Family + "expect_home_thanks", Family + "expect_car_thanks", Family + "expect_second_home_thanks");
            S("partner_wealth_kind", "your money", Proud, Family + "wealth_plain", Family + "wealth_proud");
            S("partner_wealth_sour", "your money", Topic(SocialCatalogue.TopicRich), Family + "wealth_envious");
            S("partner_glad", "life at home", Topic(SocialCatalogue.TopicGoodDay), Family + "glad_work", Family + "glad_stop", Family + "glad_retrain");
            S("partner_trained", "their new job", Topic(SocialCatalogue.TopicGoodDay), Family + "wish_trained");

            // ---- the family: Mom (Family/Relatives, Children, Courtship, HardTimes, HomeLife, WeddingDay, WeddingPlanner) and the old friend
            S("mom_moved_in", "living together", Proud, Family + "mom_moved_in");
            S("mom_engaged", "your engagement", Proud, Family + "mom_engaged");
            S("mom_date_set", "the wedding date", Proud, Family + "mom_date_set");
            S("mom_married", "your wedding", Proud, Family + "mom_married");
            S("mom_expecting", "your baby", Proud, Family + "mom_expecting");
            S("mom_adopting", "the adoption", Proud, Family + "mom_adopting");
            S("mom_grandchild", "your baby", Proud, Family + "mom_born", Family + "mom_adopted");
            S("mom_reconciled", "you two making up", Proud, Family + "mom_reconciled");
            S("mom_separated", "your separation", Comfort, Family + "mom_separated");
            S("mom_divorced", "your divorce", Comfort, Family + "mom_divorced");
            S("mom_grandchild_birthday", "a grandchild's birthday", Thanks, Family + "mom_birthday");
            S("mom_wedding_invite", "the wedding", Laugh, Family + "mom_invited");
            S("mom_holiday", "a holiday", Come, Family + "holiday_invite", Family + "holiday_at_moms");
            S("mom_holiday_missed", "a holiday", Sorry, Family + "holiday_missed");
            S("mom_visit", "her visit", Thanks, Family + "mom_visit");
            S("friend_wedding_invite", "the wedding", Laugh, Family + "friend_invited");

            // ---- life's little moments: the old friend's texts, and the texted outcomes of their choices
            L("fr01", "keeping in touch", Plain);
            L("fr02", "a dog in sunglasses", Laugh);
            L("fr03", "their stories", Topic(SocialCatalogue.TopicTheirJob));
            L("fr04", "money", Repaid);
            L("fr05_y1", "the rent", Repaid);
            L("fr05_y2", "the rent", Patience);
            L("fr05_n1", "the rent", Sorry);
            L("fr06", "your business", Proud);
            L("fr07_n1", "tacos", NextTime);
            L("fr08", "a man in a hat", Laugh);
            L("fr09", "your business", Proud);
            L("fr10_n1", "moving house", NextTime);
            L("fr11", "hot dogs", Laugh);
            L("fr12", "a lucky scratch card", Thanks);
            L("fr13", "their landlord", Topic(SocialCatalogue.TopicBadDay));
            L("fr15", "a rat on the subway", Laugh);
            L("fr16", "being proud of you", Proud);
            L("fr17_n1", "asking for a raise", Laugh);
            L("fr18", "a cooking disaster", Laugh);
            L("fr19", "you being ill", Topic(SocialCatalogue.TopicSick));
            L("fr20", "a sleepless night", Laugh);
            L("fr21", "the rain", Laugh);
            L("fr22", "Mondays", Laugh);
            L("fr23_n1", "a stuffed owl", Laugh);
            L("fr24", "money", Thanks);
            L("fr25_y1", "a bet on the game", Laugh);
            L("fr25_y2", "a bet on the game", Laugh);

            // ---- life's little moments: Mom's
            L("fm01", "money", Thanks);
            L("fm02", "the garage door", Laugh);
            L("fm03", "eating vegetables", Reassure);
            L("fm04", "your business", Proud);
            L("fm05_n1", "a parcel from home", Sorry);
            L("fm06", "her phone", Laugh);
            L("fm07_y1", "a cousin's wedding gift", Thanks);
            L("fm07_n1", "a cousin's wedding gift", Sorry);
            L("fm08", "the neighbours' dog", Laugh);
            L("fm09", "your old school report", Proud);
            L("fm10", "you being ill", Topic(SocialCatalogue.TopicSick));
            L("fm11", "an early good morning", Laugh);
            L("fm12_n1", "the internet at home", Laugh);
            L("fm13", "Sunday dinner", Plain);
            L("fm14", "money from Grandma", Thanks);
            L("fm15", "a warm coat", Reassure);
            L("fm16", "an umbrella", Reassure);
            L("fm17_y1", "your father's birthday", Laugh);
            L("fm18", "your sister", Plain);
            L("fm19", "the ladies at church", Laugh);
            L("fm20", "getting home safe", Reassure);
            L("fm21_y1", "her visit", Laugh);
            L("fm21_n1", "her visit", Sorry);

            // ---- travel (Travel/Holidays): the old friend's nudge to take a break, and a souvenir's thanks
            S("burnout", "taking a break", Rest, Travel + "burnout_friend");
            S("souvenir_friend", "a souvenir", Gift, Travel + "text_friend_souvenir");
            S("souvenir_mom", "a souvenir", Gift, Travel + "text_mom_souvenir");

            All = Made.ToArray();
            Made.Clear();
        }

        /// <summary>
        /// The subject of a text by its written line's key: the key itself first (a bad day among the news, a little moment's
        /// outcome), then its pool (the key without the number at the end); null for a text with none.
        /// </summary>
        public static SocialFollowSubject Of(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            if (ByKey.TryGetValue(key, out SocialFollowSubject subject))
            {
                return subject;
            }
            string pool = Pool(key);
            return !ReferenceEquals(pool, key) && ByKey.TryGetValue(pool, out subject) ? subject : null;
        }

        /// <summary>The subject of this id; null for none.</summary>
        public static SocialFollowSubject ById(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>Every key and pool the table names, for the checks (each is a key in the locales, or a pool of some).</summary>
        public static IEnumerable<string> Keys => ByKey.Keys;

        /// <summary>Every key a subject's call says: its line, its two answers and their reactions.</summary>
        public static IEnumerable<string> Lines()
        {
            for (int i = 0; i < All.Length; i++)
            {
                SocialTopic t = All[i].Talk;
                yield return t.Open[0];
                yield return t.ReplyA;
                yield return t.ReplyB;
                yield return t.ReactA;
                yield return t.ReactB;
            }
        }

        // A key without its number at the end ("..._news_12" is of "..._news"); the key itself when it has none.
        private static string Pool(string key)
        {
            int end = key.Length;
            while (end > 0 && char.IsDigit(key[end - 1]))
            {
                end--;
            }
            return end < key.Length && end > 0 && key[end - 1] == '_' ? key.Substring(0, end - 1) : key;
        }
    }
}
