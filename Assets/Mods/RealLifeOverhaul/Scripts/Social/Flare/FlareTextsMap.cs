using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The post a comment answers, as the comment's card needs it (FlareTextsMap.CommentCard).</summary>
    public struct FlareCommentPost
    {
        public string First;         // the poster's first name (another player: their name)
        public string Handle;        // ...and handle, as the thread's line names them
        public string Words;         // the post's words as shown
        public string District;      // the district the post is about, in words
        public string Home;          // the commenter's own district, in words
        public string Tycoon;        // a rival's post: the rival
        public string Company;       // ...and their company
        public string Reaction;      // another player's post: the reaction the city's rules chose
        public string Shop;          // data v3's news kinds: the business or chain that posted, or the one reviewed
        public string Stars;         // ...a review's stars
        public string Target;        // ...the listed company (its results; a rival's stake or bid: the player's own)
        public string Player;        // ...the player's name (a rival's suit)
        public string EarlierFirst;  // an earlier comment the comment answers (data v3): its writer's first name,
        public string EarlierHandle; // ...handle,
        public string EarlierWords;  // ...and words
    }

    /// <summary>Who answers a message of the player's on Flare, by the account (FlareTextsMap.DmSpeakerOf).</summary>
    public enum FlareDmSpeaker { None, Citizen, Influencer, Friend, Partner, Official, Business, Tycoon }

    /// <summary>
    /// Which trained RLO Texts intent each of Flare's worded posts is: the game decides the post, this picks the card the
    /// model words it from; a post with no intent keeps its written line. Also the city's people as speakers. Every map
    /// takes the running pack's data version. Game-free. Cost: a switch and a few strings a post or comment.
    /// </summary>
    public static partial class FlareTextsMap
    {
        // The reactions the training data has for each kind of the player's post (defs_flare.REACTIONS).
        private static readonly Dictionary<string, string[]> Reactions = new Dictionary<string, string[]>
        {
            { "opened", new[] { "cheer", "proud", "question", "doubt", "funny", "love" } },
            { "record", new[] { "cheer", "proud", "doubt", "funny", "question" } },
            { "promotion", new[] { "cheer", "proud", "cool", "funny", "doubt" } },
            { "selfie", new[] { "love", "cheer", "funny", "cool", "kind" } },
            { "outing", new[] { "love", "cheer", "funny", "cool", "question" } },
            { "moved", new[] { "cheer", "kind", "question", "funny", "cool" } },
            { "voted", new[] { "cheer", "cool", "funny", "question", "doubt" } },
            { "photo", new[] { "love", "cool", "question", "funny", "cheer" } }
        };

        /// <summary>The kind of the player's post as the training names it; null for a kind it has no replies for.</summary>
        public static string PostKind(FlareSource source)
        {
            switch (source)
            {
                case FlareSource.Opened: return "opened";
                case FlareSource.RecordDay: return "record";
                case FlareSource.Promotion: return "promotion";
                case FlareSource.Selfie: return "selfie";
                case FlareSource.Outing: return "outing";
                case FlareSource.Voted: return "voted";
                case FlareSource.Photo:
                case FlareSource.Interior: return "photo";
                default: return null;
            }
        }

        /// <summary>A city person's reply's reaction as the training names it, if it has it for this kind of post; null otherwise.</summary>
        public static string Reaction(FlareReplyKind kind, string postKind)
        {
            string r;
            switch (kind)
            {
                case FlareReplyKind.Agree: r = "cheer"; break;
                case FlareReplyKind.Kind: r = "kind"; break;
                case FlareReplyKind.Funny: r = "funny"; break;
                case FlareReplyKind.Doubt: r = "doubt"; break;
                case FlareReplyKind.Love: r = "love"; break;
                case FlareReplyKind.Question: r = "question"; break;
                default: return null;   // an angry pile-on: not in the training, the written line
            }
            return Allowed(r, postKind);
        }

        /// <summary>A friend's reply's reaction as the training names it, where it has it.</summary>
        public static string Reaction(FlareRules.ReplyKind kind, string postKind)
        {
            string r;
            switch (kind)
            {
                case FlareRules.ReplyKind.Cheer:
                case FlareRules.ReplyKind.Plain: r = "cheer"; break;
                case FlareRules.ReplyKind.Proud: r = "proud"; break;
                case FlareRules.ReplyKind.Cool: r = "cool"; break;
                case FlareRules.ReplyKind.Kind: r = "kind"; break;
                case FlareRules.ReplyKind.Love: r = "love"; break;
                case FlareRules.ReplyKind.Funny: r = "funny"; break;
                default: return null;
            }
            return Allowed(r, postKind);
        }

        private static string Allowed(string reaction, string postKind)
        {
            if (postKind == null || !Reactions.TryGetValue(postKind, out string[] list))
            {
                return null;
            }
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == reaction)
                {
                    return reaction;
                }
            }
            return reaction == "kind" || reaction == "proud" ? Allowed("cheer", postKind) : null;
        }

        /// <summary>A city person's post about the day by its topic (FlareTalk); null for a topic the training has no posts on.</summary>
        public static string Talk(int topic)
        {
            switch ((FlareTalk)topic)
            {
                case FlareTalk.Weather: return "citizen.post.weather";
                case FlareTalk.Prices: return "citizen.post.prices";
                case FlareTalk.Crime: return "citizen.post.crime";
                case FlareTalk.Event:
                case FlareTalk.EventTomorrow: return "citizen.post.events";
                case FlareTalk.Election: return "citizen.post.election";
                case FlareTalk.Roadworks: return "citizen.post.roadworks";
                case FlareTalk.Commute: return "citizen.post.commute";
                case FlareTalk.Everyday: return "citizen.post.everyday";
                case FlareTalk.Night: return "citizen.post.night_out";
                case FlareTalk.Food: return "citizen.post.food";
                case FlareTalk.Sport: return "citizen.post.sport";
                default: return null;   // an accident, the market, a policy, a fire: the written lines
            }
        }

        /// <summary>An official account's own post (FlareOfficialPost; for the rate, 0 down, 1 up); null where the training has none.</summary>
        public static string Official(int kind, int variant)
        {
            switch ((FlareOfficialPost)kind)
            {
                case FlareOfficialPost.Patrols: return "official.post.patrols";
                case FlareOfficialPost.Outage: return "official.post.outage";
                case FlareOfficialPost.Rate: return variant == 1 ? "official.post.rate_up" : "official.post.rate_down";
                case FlareOfficialPost.Clean: return "official.post.clean";
                case FlareOfficialPost.Election: return "official.post.election";
                default: return null;
            }
        }

        /// <summary>An account's answer (FlareAnswer) by who gives it; null where the training has none.</summary>
        public static string Answer(FlareAnswer answer, FlareAuthor author)
        {
            if (author == FlareAuthor.Tycoon)
            {
                return answer == FlareAnswer.Tycoon ? "tycoon.answer" : null;
            }
            if (author == FlareAuthor.Business)
            {
                return answer == FlareAnswer.Thanks ? "business.reply.good" : answer == FlareAnswer.Sorry ? "business.reply.bad" : null;
            }
            switch (answer)
            {
                case FlareAnswer.Received: return "official.answer.noted";
                case FlareAnswer.Works: return "official.answer.crew";
                case FlareAnswer.Patrol: return "official.answer.patrols";
                case FlareAnswer.Cleanup: return "official.answer.cleaning";
                case FlareAnswer.Busy: return "official.answer.booked";
                case FlareAnswer.Already: return "official.answer.already";
                case FlareAnswer.Inspect: return "official.answer.inspector";
                case FlareAnswer.Transit: return "official.answer.sorry_wait";
                default: return null;
            }
        }

        /// <summary>An account's answer to what the player typed (data v3), to a mention or a reply under its post; null otherwise.</summary>
        public static string Said(FlareAuthor author, int data)
        {
            if (data < TextIntents.DataV3)
            {
                return null;
            }
            switch (author)
            {
                case FlareAuthor.Official: return "official.answer.said";
                case FlareAuthor.Business:
                case FlareAuthor.Brand: return "business.answer.said";
                case FlareAuthor.Tycoon: return "tycoon.answer.said";
                case FlareAuthor.Citizen: return "citizen.answer.reply";
                default: return null;
            }
        }

        /// <summary>True for the answers to a post that called on the account: a mention, a brand, a rival on an announcement.</summary>
        public static bool AnswersMention(FlareAnswer answer) => answer == FlareAnswer.Mention || answer == FlareAnswer.Brand || answer == FlareAnswer.Tycoon;

        /// <summary>A rival's post: their move (Rivals.RivalMoveKind) or, with none (-1), a boast; data v3 adds the stake, bid and suit.</summary>
        public static string Tycoon(int move, int data = TextIntents.DataV2)
        {
            switch (move)
            {
                case -1: return "tycoon.boast";
                case 0: return "tycoon.property";
                case 1: return "tycoon.outlet";
                case 2: return "tycoon.poach";
                case 3: return data >= TextIntents.DataV3 ? "tycoon.stake" : null;
                case 4: return data >= TextIntents.DataV3 ? "tycoon.bid" : null;
                case 5: return "tycoon.lobby";
                case 6: return data >= TextIntents.DataV3 ? "tycoon.suit" : null;
                case 7: return "tycoon.press";
                default: return null;
            }
        }

        /// <summary>A business account's own post by its kind (FlareText's business lines); null for the company lines.</summary>
        public static string Business(int arg)
        {
            switch (arg)
            {
                case 0: return "business.post.sale";
                case 1: return "business.post.new";
                case 2: return "business.post.thanks";
                case 3: return "business.post.late";
                case 4: return "business.post.hiring";
                default: return null;
            }
        }

        /// <summary>The intent of an answer to a message of the player's by who answers; v3 answers every account, before only some. Null: the written line.</summary>
        public static string Dm(string key, FlareDmSpeaker speaker, int data)
        {
            if (key == null || speaker == FlareDmSpeaker.None)
            {
                return null;
            }
            if (FlareInviteRules.AnswerOf(key, out _))
            {
                return speaker == FlareDmSpeaker.Citizen || speaker == FlareDmSpeaker.Influencer ? FlareInviteRules.TrainedIntent(key, data) : null;
            }
            if (key != DmChat && key != DmCollab && key != DmOffice)
            {
                return null;   // the city's own first messages (a fan's, a job's) are not answers
            }
            if (data < TextIntents.DataV3)
            {
                return speaker == FlareDmSpeaker.Citizen && key == DmChat ? "citizen.dm.chat" : speaker == FlareDmSpeaker.Influencer && key == DmCollab ? "citizen.dm.collab" : null;
            }
            switch (speaker)
            {
                case FlareDmSpeaker.Citizen: return "citizen.dm.answer";
                case FlareDmSpeaker.Influencer: return "citizen.dm.influencer";
                case FlareDmSpeaker.Friend: return "friend.dm.answer";
                case FlareDmSpeaker.Partner: return "partner.dm.answer";
                case FlareDmSpeaker.Official: return "official.dm.answer";
                case FlareDmSpeaker.Business: return "business.dm.answer";
                default: return "tycoon.dm.answer";
            }
        }

        public const string DmChat = "reallife_flare_dm_answer_chat", DmCollab = "reallife_flare_dm_answer_collab", DmOffice = "reallife_flare_dm_answer_office";

        /// <summary>Who answers a message by its account: a city person ("c12") or an influencer ("i:3"); false for any other.</summary>
        public static bool DmSpeaker(string with, out bool influencer, out int number)
        {
            influencer = false;
            number = -1;
            if (with == null || with.Length < 2)
            {
                return false;
            }
            if (with[0] == 'c' && with[1] != ':')
            {
                return int.TryParse(with.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out number);
            }
            if (with.Length > 2 && with[0] == 'i' && with[1] == ':')
            {
                influencer = true;
                return int.TryParse(with.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out number);
            }
            return false;
        }

        /// <summary>Who answers a message, for every account Flare keys; None for another player, the boss, a colleague, a candidate or the paper.</summary>
        public static FlareDmSpeaker DmSpeakerOf(string with, out int number)
        {
            if (DmSpeaker(with, out bool influencer, out number))
            {
                return influencer ? FlareDmSpeaker.Influencer : FlareDmSpeaker.Citizen;
            }
            number = -1;
            if (with == null || with.Length < 2)
            {
                return FlareDmSpeaker.None;
            }
            switch (with[0])
            {
                case 'f':
                    return int.TryParse(with.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out number) ? FlareDmSpeaker.Friend : FlareDmSpeaker.None;
                case 'p':
                    return with[1] != ':' && int.TryParse(with.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out number) ? FlareDmSpeaker.Partner : FlareDmSpeaker.None;
                case 'o':
                    return with.Length > 2 && with[1] == ':' && int.TryParse(with.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out number)
                        ? FlareDmSpeaker.Official : FlareDmSpeaker.None;
                case 'b':
                case 'k':
                    return with.Length > 2 && with[1] == ':' ? FlareDmSpeaker.Business : FlareDmSpeaker.None;
                case 't':
                    return with.Length > 2 && with[1] == ':' ? FlareDmSpeaker.Tycoon : FlareDmSpeaker.None;
                default:
                    return FlareDmSpeaker.None;
            }
        }

        /// <summary>The name of a named account ("b:Daily Brew", "k:Sam's Deli", "t:Space Karen"); null for any other.</summary>
        public static string DmName(string with)
        {
            return with != null && with.Length > 2 && with[1] == ':' && (with[0] == 'b' || with[0] == 'k' || with[0] == 't') ? with.Substring(2) : null;
        }

        /// <summary>The key an answer's words are kept under.</summary>
        public static string DmKey(FlareDm m) => "fd:" + m.With + ":" + m.At;

        /// <summary>Whether an answer to the player's message may have its words asked now: the city's, still to come, with a card.</summary>
        public static bool DmWants(FlareDm m, long now, int data)
        {
            return m != null && !m.Mine && m.Text == null && m.At > now && Dm(m.Key, DmSpeakerOf(m.With, out _), data) != null;
        }

        /// <summary>The player's latest message to the same account before an answer; null if it has gone.</summary>
        public static FlareDm DmAsked(List<FlareDm> dms, FlareDm answer)
        {
            FlareDm asked = null;
            for (int i = 0; dms != null && answer != null && i < dms.Count; i++)
            {
                FlareDm m = dms[i];
                if (m == answer)
                {
                    return asked;
                }
                if (m.Mine && m.With == answer.With)
                {
                    asked = m;
                }
            }
            return null;
        }

        /// <summary>A city person's mood today as a card says it, stable for the day; the grumpy are never better than fine.</summary>
        public static string Mood(int citySeed, int number, int day, bool grumpy)
        {
            int roll = FlareRules.Roll(citySeed * 31 + number, day, 1207);
            return TextWords.Moods[grumpy ? roll % 3 : roll % TextWords.Moods.Length];
        }

        /// <summary>The intent of a city person's reply under a post of the player's; an angry pile-on keeps its written lines.</summary>
        public static string Reply(FlarePost parent, FlareReplyKind kind, int data)
        {
            if (parent == null || parent.Author != FlareAuthor.Player || kind == FlareReplyKind.Angry)
            {
                return null;
            }
            bool v3 = data >= TextIntents.DataV3;
            if (v3 && !string.IsNullOrEmpty(parent.Text))
            {
                return "citizen.reply.words";
            }
            string postKind = PostKind(parent.Source);
            if (postKind != null)
            {
                return Reaction(kind, postKind) != null ? "citizen.reply." + postKind : null;
            }
            string mine = v3 ? Mine(parent) : null;
            return mine != null ? "citizen.comment." + mine : null;
        }

        /// <summary>The comment kind of a post of the player's with no reactions of its own (data v3 mine_*); null for none.</summary>
        public static string Mine(FlarePost p)
        {
            switch (p.Source)
            {
                case FlareSource.Endorsed: return "mine_endorsed";
                case FlareSource.GoodDeed: return "mine_good_deed";
                case FlareSource.Arrested: return "mine_arrested";
                case FlareSource.Sponsored: return "mine_sponsored";
                case FlareSource.Shares: return "mine_shares";
                case FlareSource.Issue: return "mine_issue";
                case FlareSource.Campaign: return "mine_campaign";
                case FlareSource.Graduated: return "mine_graduated";
                case FlareSource.Report: return "mine_report";
                case FlareSource.Review: return p.Arg >= 4 ? "mine_review_good" : "mine_review_bad";
                case FlareSource.Announce: return "mine_announce";
                default: return null;
            }
        }

        // What the player reported and announced, and the issue they posted about, as the training's cards say them (defs_v3).
        private static readonly string[] Reported = { "a pothole", "a street light out", "crime", "litter", "an unsafe building site", "a business", "trouble with the buses", "a power or water cut" };
        private static readonly string[] Announced = { "a sale", "new opening hours", "an event", "that it is hiring", "new stock" };
        private static readonly string[] Issues = { "safety after a crime", "the roads after a crash" };

        /// <summary>The values of a comment on a post of the player's; a value the post lacks is left out, and the card is not built.</summary>
        public static void MineSlots(TextSlots slots, FlarePost p, string player, string district)
        {
            slots.Set("player", player);
            Optional(slots, "district", district);
            switch (p.Source)
            {
                case FlareSource.Sponsored:
                case FlareSource.Announce:
                case FlareSource.Review:
                    Optional(slots, "shop", p.Name);
                    break;
                case FlareSource.Shares:
                    Optional(slots, "target", p.Name);
                    break;
            }
            if (p.Source == FlareSource.Review)
            {
                slots.Set("stars", FlareRules.Clamp(p.Arg, 1, 5).ToString(CultureInfo.InvariantCulture));
            }
            else if (p.Source == FlareSource.Report)
            {
                slots.Set("problem", Reported[FlareRules.Clamp(p.Arg, 0, Reported.Length - 1)]);
            }
            else if (p.Source == FlareSource.Announce)
            {
                slots.Set("what", Announced[FlareRules.Clamp(p.Arg, 0, Announced.Length - 1)]);
            }
            else if (p.Source == FlareSource.Issue)
            {
                slots.Set("issue", Issues[FlareRules.Clamp(p.Arg, 0, Issues.Length - 1)]);
            }
        }

        /// <summary>The topic of a city person's post as the answer's card names it ("night out"); null for one the answers lack.</summary>
        public static string AnswerTopic(FlarePost p)
        {
            string post = p != null && p.Author == FlareAuthor.Citizen && p.Source == FlareSource.CityTalk ? Talk(p.Arg / 10) : null;
            return post != null ? post.Substring("citizen.post.".Length).Replace('_', ' ') : null;
        }

        /// <summary>A city person's age: their own, the one their face and an invitation have.</summary>
        public static int Age(FlareCitizen c, int citySeed)
        {
            return FlareLiveRules.CitizenAge(citySeed, c.Index);
        }

        /// <summary>A city person's way of writing: the grumpy write grumpily; the rest by their own seed (world.citizen).</summary>
        public static string Style(FlareCitizen c, int citySeed)
        {
            string style = TextWords.CitizenStyle(citySeed * 31 + c.Index);
            if (c.Persona != FlarePersona.Grumpy)
            {
                return style;
            }
            int comma = style.IndexOf(',');
            return "grumpy" + (comma > 0 ? style.Substring(comma) : string.Empty);
        }

        public static void Fill(TextWho who, FlareCitizen c, string district, int citySeed, string when)
        {
            who.Clear();
            who.First = c.First;
            who.Name = c.First + " " + c.Last;
            who.Handle = c.Handle;
            who.Age = Age(c, citySeed);
            who.District = district;
            who.Style = Style(c, citySeed);
            who.When = when;
        }

        /// <summary>The training's companies of the rival tycoons, by their place in the rivals' catalogue.</summary>
        public static readonly string[] TycoonCompanies = { "Karen Aerospace & Motors", "Luthor Everything Co.", "Zucc Social Holdings" };
    }
}
