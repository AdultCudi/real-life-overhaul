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
    /// Which trained intent each of Flare's worded posts is, for RLO Texts (Scripts/Texts): the game decides the post as
    /// before (its source, kind, author, reaction and time); this says which card the model is given for its words. A post
    /// with no intent here keeps its written line. Also the city's people as a card's speaker: their age and way of writing,
    /// stable for each person from the city's seed and their persona. And the comments under posts that are not the
    /// player's (FlareCommentRules): their intent and card; they have no written line, so a post with no intent for its
    /// comments shows only their count. Every map takes the running pack's data version (TextsHub.Data): a v2 pack is
    /// given only v2's intents; a v3 pack also the answers to what the player typed (a message, a post, a reply), the
    /// decided invitations, the rivals' stake, bid and suit, and comments under every kind of post.
    /// Game-free (EconomyCheck, TextsChecks.cs, FlareCommentChecks.cs, FlareDmTextsChecks.cs, FlareInviteChecks.cs).
    /// COST: a switch and a few strings a post, when it is made, and a comment, when its words are asked.
    /// </summary>
    public static class FlareTextsMap
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

        /// <summary>A city person's reply's reaction as the training names it, if the training has it for this kind of post; null otherwise.</summary>
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
                default: return null;   // (an angry pile-on: no such reaction in the training; the written line)
            }
            return Allowed(r, postKind);
        }

        /// <summary>A friend's reply's reaction (FlareRules.ReplyOf) as the training names it, where it has it.</summary>
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

        /// <summary>A city person's post about the day, by its topic (FlareTalk); null for a topic the training has no posts on.</summary>
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

        /// <summary>An official account's own post (FlareOfficialPost, and for the rate its way: 0 down, 1 up); null where the training has none.</summary>
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

        /// <summary>
        /// An account's answer to what the player typed (data v3: official.answer.said, business.answer.said,
        /// tycoon.answer.said; a city person's citizen.answer.reply) by who gives it: an answer to being called on in a post
        /// (FlareAnswer.Mention, Brand, Tycoon) or to the player's reply under the account's own post. Null for any other
        /// author, or a pack trained before data v3.
        /// </summary>
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

        /// <summary>True for the answers that answer what the player wrote in a post that called on the account (a mention, a brand, a rival on an announcement).</summary>
        public static bool AnswersMention(FlareAnswer answer) => answer == FlareAnswer.Mention || answer == FlareAnswer.Brand || answer == FlareAnswer.Tycoon;

        /// <summary>
        /// A rival tycoon's post: their move (Rivals.RivalMoveKind) or, with none, a boast; null for a move the training has no
        /// posts on. Data v3 adds the stake, the bid and the suit (RivalMoveKind 3, 4 and 6).
        /// </summary>
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

        // ---------------------------------------------------------------- the answers to the player's messages

        /// <summary>
        /// The intent of an answer to a message of the player's, by who answers (FlareDmSpeaker) and its line's key, for a
        /// pack of this data version. Data v3: every account the city answers for answers what was typed (citizen.dm.answer,
        /// citizen.dm.influencer, friend.dm.answer, partner.dm.answer, official.dm.answer, business.dm.answer,
        /// tycoon.dm.answer), and an invitation the game decided has its own (FlareInviteRules.TrainedIntent). Before v3 only
        /// a city person's chat and an influencer's (citizen.dm.chat, citizen.dm.collab). Null: the written line.
        /// </summary>
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
                return null;   // (the city's own first messages, a fan's or a job's, are not answers: their written lines)
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

        /// <summary>
        /// Who answers a message, by its account (FlareDm.With): one of the city's people as Flare keys them ("c12", no colon:
        /// FlareLiveRules.CityDm, the profiles, the conversations) or an influencer ("i:3"). False for any other (the City
        /// Office, a candidate "c:name", a player of the session): see <see cref="DmSpeakerOf"/> for every account.
        /// </summary>
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

        /// <summary>
        /// Who answers a message, for every account Flare keys (FlareDirector.AccountName): a city person "c12", an
        /// influencer "i:3", a friend "f3", the partner "p3", an official account "o:1", a brand "b:name" or a city business
        /// "k:name", a rival "t:name". None for the rest: a player of the session "p:id" (another human), the boss "b", a
        /// colleague "s:name", a candidate "c:name", the paper "l". 'number' is the citizen's, influencer's, person's or
        /// account's number, -1 for a named account.
        /// </summary>
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

        /// <summary>The key an answer's words are kept under: asked and drawn by the same.</summary>
        public static string DmKey(FlareDm m) => "fd:" + m.With + ":" + m.At;

        /// <summary>
        /// Whether an answer to the player's message may have its words asked now: the city's (not the player's, not typed),
        /// not yet shown, with an intent for this pack's data version and an account the model has a card for.
        /// </summary>
        public static bool DmWants(FlareDm m, long now, int data)
        {
            return m != null && !m.Mine && m.Text == null && m.At > now && Dm(m.Key, DmSpeakerOf(m.With, out _), data) != null;
        }

        /// <summary>The player's message an answer answers: the player's latest to the same account before it; null if it has gone (the oldest messages go).</summary>
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

        /// <summary>
        /// A city person's mood today, as a card says it (TextWords.Moods): stable for the day from the city's seed and their
        /// number; the grumpy are never better than fine.
        /// </summary>
        public static string Mood(int citySeed, int number, int day, bool grumpy)
        {
            int roll = FlareRules.Roll(citySeed * 31 + number, day, 1207);
            return TextWords.Moods[grumpy ? roll % 3 : roll % TextWords.Moods.Length];
        }

        // ---------------------------------------------------------------- the city's replies under the player's own posts

        /// <summary>
        /// The intent of a city person's reply under a post of the player's (FlareLiveRules.CityReplies), by the reply's kind
        /// the game chose: an angry pile-on keeps its written lines; with data v3 a post with typed words is answered on its
        /// words (citizen.reply.words), a built post of a kind with reactions by its kind (citizen.reply.*), and one of the
        /// other kinds (an arrest, a report, a review...) as a comment on it (citizen.comment.mine_*, <see cref="Mine"/>).
        /// Before v3: the kinds with reactions only. Null: the written line.
        /// </summary>
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

        /// <summary>
        /// The values of a comment on a post of the player's (citizen.comment.mine_*): the player, and by the post's kind its
        /// business, stars, company, issue, problem or announcement, and its district in words. A value the post lacks is
        /// left out (the card is then not built).
        /// </summary>
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

        /// <summary>The topic of a city person's post as the answer's card names it ("commute", "night out"); null for a post whose topic the training's answers lack.</summary>
        public static string AnswerTopic(FlarePost p)
        {
            string post = p != null && p.Author == FlareAuthor.Citizen && p.Source == FlareSource.CityTalk ? Talk(p.Arg / 10) : null;
            return post != null ? post.Substring("citizen.post.".Length).Replace('_', ' ') : null;
        }

        // ---------------------------------------------------------------- comments under posts that are not the player's

        /// <summary>
        /// The trained intent of a comment under a post that is not the player's (FlareCommentRules), by what the post is
        /// about: a resident answering another resident's post on its topic (citizen.thread.*, defs_flare.make_cit_thread),
        /// a resident reacting to an account's news (citizen.react.*, make_cit_react), a stranger answering another player's
        /// post (citizen.reply.*, make_cit_reply). 'niche' is an influencer's (FlareNiche), -1 for anybody else. With data v3
        /// every other kind of post has its own comments (citizen.comment.*, <see cref="CommentKind"/>), and a post of no
        /// kind is commented on its words (citizen.comment.words). Null where the training has nothing that fits: such a post
        /// shows only its count of replies.
        ///
        /// A pack of data v3 or later answers a post on a v2 topic (citizen.thread.*) on its words instead
        /// (citizen.comment.words, the post's words in history as before): v2's thread rows answer the topic, not the post,
        /// and the 2B v5 model wrote replies that missed the post more often than not ("They put the price up and made it
        /// smaller." under "Tried the new noodle place on 5th Avenue. Worth the queue."), where on the words it answered the
        /// post itself (in an offline run). Who may comment (a friend who cares about the topic) still goes by
        /// the topic: <see cref="CommentTaste"/>.
        /// </summary>
        public static string Comment(FlarePost p, int niche, int data = TextIntents.DataV2)
        {
            string v2 = CommentV2(p, niche);
            if (data < TextIntents.DataV3)
            {
                return v2;
            }
            if (v2 != null && !v2.StartsWith("citizen.thread.", System.StringComparison.Ordinal))
            {
                return v2;
            }
            string kind = CommentKind(p, niche);
            return kind != null ? "citizen.comment." + kind : p.Author != FlareAuthor.Player ? "citizen.comment.words" : null;
        }

        /// <summary>What a friend must care about to comment under a post (FlareTaste), by the post's topic whatever intent words the comment; None: no friend comments.</summary>
        public static FlareTaste CommentTaste(FlarePost p, int niche)
        {
            return Taste(CommentV2(p, niche));
        }

        private static string CommentV2(FlarePost p, int niche)
        {
            if (p.Author == FlareAuthor.Network && PostKind(p.Source) != null)
            {
                return "citizen.reply." + PostKind(p.Source);
            }
            switch (p.Source)
            {
                case FlareSource.CityTalk: return Thread(Talk(p.Arg / 10));
                case FlareSource.Report:
                    switch ((FlareReport)p.Arg)
                    {
                        case FlareReport.Road: return "citizen.thread.roadworks";
                        case FlareReport.Crime:
                        case FlareReport.Unsafe: return "citizen.thread.crime";
                        case FlareReport.Transit: return "citizen.thread.commute";
                        case FlareReport.Lamp:
                        case FlareReport.Litter:
                        case FlareReport.Utilities: return "citizen.thread.everyday";
                        default: return null;   // (a complaint about a business: nothing in the training fits)
                    }
                case FlareSource.Official:
                    switch ((FlareOfficialPost)(p.Arg / 10))
                    {
                        case FlareOfficialPost.Patrols: return "citizen.react.patrols";
                        case FlareOfficialPost.Outage: return "citizen.react.outage";
                        case FlareOfficialPost.Rate: return p.Arg % 10 == 1 ? "citizen.react.rate_up" : "citizen.react.rate_down";
                        case FlareOfficialPost.Clean: return "citizen.react.cleaning";
                        case FlareOfficialPost.Election: return "citizen.react.election";
                        default: return null;
                    }
                case FlareSource.BusinessPost:
                    if (p.Author == FlareAuthor.Tycoon)
                    {
                        switch (p.Arg - FlareLiveRules.TycoonMoveArg)
                        {
                            case 0: return "citizen.react.tycoon_buys";
                            case 1: return "citizen.react.tycoon_opens";
                            case 2: return "citizen.react.tycoon_hiring";
                            default: return null;
                        }
                    }
                    return p.Author == FlareAuthor.Business && (p.Arg == 0 || p.Arg == 1) ? "citizen.thread.prices" : null;   // (a sale, new stock: talk of prices)
                case FlareSource.Life:
                    switch ((FlareRules.LifeTopic)(p.Arg / 10))
                    {
                        case FlareRules.LifeTopic.Sport: return "citizen.thread.sport";
                        case FlareRules.LifeTopic.Food: return "citizen.thread.food";
                        case FlareRules.LifeTopic.Night: return "citizen.thread.night_out";
                        case FlareRules.LifeTopic.Shops: return "citizen.thread.prices";
                        default: return "citizen.thread.everyday";
                    }
                case FlareSource.Election: return "citizen.thread.election";
                case FlareSource.Niche:
                case FlareSource.Visit:
                case FlareSource.Hired:
                case FlareSource.Rival:
                    switch (niche)
                    {
                        case (int)FlareNiche.Food: return "citizen.thread.food";
                        case (int)FlareNiche.Fitness: return "citizen.thread.sport";
                        case (int)FlareNiche.Nightlife: return "citizen.thread.night_out";
                        case (int)FlareNiche.LocalNews: return "citizen.thread.everyday";
                        default: return null;   // (fashion, tech, business: data v3's niche_*)
                    }
                default: return null;
            }
        }

        /// <summary>
        /// The data v3 comment kind of a post the v2 training has no comments for (defs_v3.COMMENT_KINDS): a city person's
        /// accident, fire, market or policy; an influencer's fashion, tech or business; the City Ledger's headline; a city
        /// business's thanks, late opening or hiring (a rainy day's and a closed day's posts are not made by the game); a
        /// chain's news; a resident's review; a listed company's results; the City Office's notice and policy, the police's
        /// advice and transit's service post; a rival's boast, stake, bid, lobbying, suit and piece in the paper. Null otherwise.
        /// </summary>
        public static string CommentKind(FlarePost p, int niche)
        {
            switch (p.Source)
            {
                case FlareSource.CityTalk:
                    switch ((FlareTalk)(p.Arg / 10))
                    {
                        case FlareTalk.Accident: return "accident";
                        case FlareTalk.Fire: return "fire";
                        case FlareTalk.Market: return "market";
                        case FlareTalk.Policy: return "policy";
                        default: return null;
                    }
                case FlareSource.Niche:
                case FlareSource.Visit:
                case FlareSource.Hired:
                case FlareSource.Rival:
                    switch (niche)
                    {
                        case (int)FlareNiche.Fashion: return "niche_fashion";
                        case (int)FlareNiche.Tech: return "niche_tech";
                        case (int)FlareNiche.Business: return "niche_business";
                        default: return null;
                    }
                case FlareSource.Headline: return "ledger_headline";
                case FlareSource.BusinessPost:
                    if (p.Author == FlareAuthor.Tycoon)
                    {
                        if (p.Arg < FlareLiveRules.TycoonMoveArg)
                        {
                            return "tycoon_boast";
                        }
                        switch (p.Arg - FlareLiveRules.TycoonMoveArg)
                        {
                            case 3: return "tycoon_stake";
                            case 4: return "tycoon_bid";
                            case 5: return "tycoon_lobby";
                            case 6: return "tycoon_suit";
                            case 7: return "tycoon_press";
                            default: return null;
                        }
                    }
                    if (p.Author != FlareAuthor.Business)
                    {
                        return null;
                    }
                    switch (p.Arg)
                    {
                        case 2: return "biz_thanks";
                        case 3: return "biz_late";
                        case 4: return "biz_hiring";
                        default: return null;
                    }
                case FlareSource.BrandNews: return p.Author == FlareAuthor.Brand ? "brand_news" : null;
                case FlareSource.Review: return p.Author == FlareAuthor.Citizen ? (p.Arg >= 4 ? "review_good" : "review_bad") : null;
                case FlareSource.Results: return p.Arg == 1 ? "results_up" : "results_down";
                case FlareSource.Official:
                    switch ((FlareOfficialPost)(p.Arg / 10))
                    {
                        case FlareOfficialPost.Notice: return "official_notice";
                        case FlareOfficialPost.Advice: return "official_advice";
                        case FlareOfficialPost.Service: return "official_service";
                        case FlareOfficialPost.Policy: return "official_policy";
                        default: return null;
                    }
                default: return null;
            }
        }

        // A resident's post's intent as the reply to such a post.
        private static string Thread(string post)
        {
            return post != null ? "citizen.thread." + post.Substring("citizen.post.".Length) : null;
        }

        /// <summary>What a friend must care about to comment under a post of this intent (FlareTaste); None: no friend comments.</summary>
        public static FlareTaste Taste(string intent)
        {
            switch (intent)
            {
                case "citizen.thread.food": return FlareTaste.Food;
                case "citizen.thread.night_out": return FlareTaste.Night;
                case "citizen.thread.sport": return FlareTaste.Sport;
                case "citizen.thread.prices": return FlareTaste.Shops;
                case "citizen.thread.everyday": return FlareTaste.Cheers;
                default: return FlareTaste.None;
            }
        }

        /// <summary>The reaction of a stranger's comment under another player's post: how the city answers such a post (FlareLiveRules.ReplyKindOf), where the training has it; null otherwise.</summary>
        public static string CommentReaction(FlarePost p, int seed, int slot)
        {
            return Reaction(FlareLiveRules.ReplyKindOf(p, FlareRules.Roll(seed, slot, 847)), PostKind(p.Source));
        }

        /// <summary>True for a comment intent that can answer an earlier comment under its post (data v3's citizen.comment.*; v2's comments only answer the post).</summary>
        public static bool AnswersComments(string intent) => intent != null && intent.StartsWith("citizen.comment.", System.StringComparison.Ordinal);

        /// <summary>
        /// A comment's card: its intent, its use (Flare's comments and replies) and the values the intent's lines take from the
        /// post it answers. The speaker (r.Who) is filled by the caller. False if a value the card needs is missing (nothing
        /// is made up: that comment is not written). A data v3 comment with an earlier comment on the post (post.Earlier*)
        /// answers it: the earlier comment is history's last line, and a comment on a post's words becomes
        /// citizen.comment.reply.
        /// </summary>
        public static bool CommentCard(TextRequest r, string intent, FlareCommentPost post)
        {
            if (intent == null)
            {
                return false;
            }
            r.Intent = intent;
            r.Use = TextUse.Replies;
            r.Slots.Clear();
            r.History.Clear();
            if (AnswersComments(intent))
            {
                return CommentCardV3(r, intent.Substring("citizen.comment.".Length), post);
            }
            if (intent.StartsWith("citizen.thread.", System.StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(post.First) || string.IsNullOrEmpty(post.Handle) || string.IsNullOrEmpty(post.Words))
                {
                    return false;
                }
                r.Slots.Set("other", post.First);
                Optional(r.Slots, "district", post.District);
                r.History.Add(new TextSaid(post.Handle, post.Words));
                return true;
            }
            if (intent.StartsWith("citizen.react.", System.StringComparison.Ordinal))
            {
                Optional(r.Slots, "district", post.District);
                Optional(r.Slots, "home", post.Home);
                Optional(r.Slots, "tycoon", post.Tycoon);
                Optional(r.Slots, "company", post.Company);
                return true;
            }
            if (intent.StartsWith("citizen.reply.", System.StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(post.Reaction) || string.IsNullOrEmpty(post.First) || string.IsNullOrEmpty(post.Words))
                {
                    return false;
                }
                r.Slots.Set("reaction", post.Reaction);
                r.History.Add(new TextSaid(post.First, post.Words));
                return true;
            }
            return false;
        }

        // A data v3 comment (defs_v3.make_comment, make_comment_pair): a thread kind and a post's words with the post in
        // history, the paper's headline under "the City Ledger", a news kind told in news and facts; an earlier comment last.
        private static bool CommentCardV3(TextRequest r, string kind, FlareCommentPost post)
        {
            bool earlier = !string.IsNullOrEmpty(post.EarlierHandle) && !string.IsNullOrEmpty(post.EarlierWords);
            bool words = kind == "words" || kind == "reply";
            if (words || System.Array.IndexOf(TextIntents.ThreadKinds, kind) >= 0)
            {
                if (string.IsNullOrEmpty(post.First) || string.IsNullOrEmpty(post.Handle) || string.IsNullOrEmpty(post.Words))
                {
                    return false;
                }
                if (words)
                {
                    if (earlier && string.IsNullOrEmpty(post.EarlierFirst))
                    {
                        return false;
                    }
                    r.Intent = earlier ? "citizen.comment.reply" : "citizen.comment.words";
                    Optional(r.Slots, "other2", earlier ? post.EarlierFirst : null);
                }
                r.Slots.Set("other", post.First);
                r.History.Add(new TextSaid(post.Handle, post.Words));
            }
            else if (kind == "ledger_headline")
            {
                if (string.IsNullOrEmpty(post.Words))
                {
                    return false;
                }
                r.History.Add(new TextSaid("the City Ledger", post.Words));
            }
            else
            {
                Optional(r.Slots, "shop", post.Shop);
                Optional(r.Slots, "other", post.First);
                Optional(r.Slots, "stars", post.Stars);
                Optional(r.Slots, "target", post.Target);
                Optional(r.Slots, "tycoon", post.Tycoon);
                Optional(r.Slots, "company", post.Company);
                Optional(r.Slots, "player", post.Player);
                Optional(r.Slots, "district", post.District);
            }
            if (earlier)
            {
                r.History.Add(new TextSaid(post.EarlierHandle, post.EarlierWords));
            }
            return true;
        }

        private static void Optional(TextSlots slots, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                slots.Set(name, value);
            }
        }

        // ---------------------------------------------------------------- the city's people as speakers

        /// <summary>A city person's age: their own (FlareLiveRules.CitizenAge), the one their face is drawn at and an invitation meets them with.</summary>
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

        /// <summary>The speaker of a city person's card.</summary>
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

        // ---------------------------------------------------------------- for the checks

        /// <summary>Every intent Flare's map can give, for a pack of this data version.</summary>
        public static List<string> Mapped(int data = TextIntents.DataV2)
        {
            var list = new List<string>(140);
            foreach (KeyValuePair<string, string[]> pair in Reactions)
            {
                list.Add("citizen.reply." + pair.Key);
            }
            for (int t = 0; t <= (int)FlareTalk.Sport; t++)
            {
                Add(list, Talk(t));
            }
            for (int k = 0; k <= (int)FlareOfficialPost.Election; k++)
            {
                Add(list, Official(k, 0));
                Add(list, Official(k, 1));
            }
            for (int a = 0; a <= (int)FlareAnswer.Clean; a++)
            {
                Add(list, Answer((FlareAnswer)a, FlareAuthor.Official));
                Add(list, Answer((FlareAnswer)a, FlareAuthor.Business));
                Add(list, Answer((FlareAnswer)a, FlareAuthor.Tycoon));
            }
            for (int au = 0; au <= (int)FlareAuthor.Tycoon; au++)
            {
                Add(list, Said((FlareAuthor)au, data));
            }
            for (int m = -1; m < 8; m++)
            {
                Add(list, Tycoon(m, data));
            }
            for (int b = 0; b < 7; b++)
            {
                Add(list, Business(b));
            }
            string[] keys = { DmChat, DmCollab, DmOffice };
            List<string> invites = FlareInviteRules.Keys();
            for (int s = 1; s <= (int)FlareDmSpeaker.Tycoon; s++)
            {
                for (int k = 0; k < keys.Length; k++)
                {
                    Add(list, Dm(keys[k], (FlareDmSpeaker)s, data));
                }
                for (int k = 0; k < invites.Count; k++)
                {
                    Add(list, Dm(invites[k], (FlareDmSpeaker)s, data));
                }
            }
            var mine = new FlarePost { Author = FlareAuthor.Player };
            for (int s = 1; s <= (int)FlareSource.Answer; s++)
            {
                for (int arg = 1; arg <= 5; arg++)
                {
                    mine.Source = (FlareSource)s;
                    mine.Arg = arg;
                    mine.Text = null;
                    Add(list, Reply(mine, FlareReplyKind.Agree, data));
                    mine.Text = "typed";
                    Add(list, Reply(mine, FlareReplyKind.Agree, data));
                }
            }
            list.Add("citizen.review");
            CommentIntents(list, data);
            return list;
        }

        /// <summary>Every intent a comment can be asked with (every source, kind, author and niche swept), added to 'list' once each; with data v3 the answer to an earlier comment too.</summary>
        public static void CommentIntents(List<string> list, int data = TextIntents.DataV2)
        {
            FlareAuthor[] authors = { FlareAuthor.Citizen, FlareAuthor.Official, FlareAuthor.Business, FlareAuthor.Tycoon, FlareAuthor.Friend, FlareAuthor.Influencer, FlareAuthor.Network,
                FlareAuthor.Brand, FlareAuthor.Company, FlareAuthor.Ledger };
            var p = new FlarePost();
            for (int s = 1; s <= (int)FlareSource.Answer; s++)
            {
                for (int a = 0; a < authors.Length; a++)
                {
                    for (int arg = 0; arg < 200; arg++)   // (a city post's Arg is its topic times ten: FlareTalk runs to 15)
                    {
                        for (int niche = -1; niche <= (int)FlareNiche.Business; niche++)
                        {
                            p.Source = (FlareSource)s;
                            p.Author = authors[a];
                            p.Arg = arg;
                            Add(list, Comment(p, niche, data));
                        }
                    }
                }
            }
            if (list.Contains("citizen.comment.words"))
            {
                Add(list, "citizen.comment.reply");   // (a comment on a post's words that answers an earlier comment: CommentCard)
            }
        }

        private static void Add(List<string> list, string intent)
        {
            if (intent != null && !list.Contains(intent))
            {
                list.Add(intent);
            }
        }

        /// <summary>Where the map is wrong; empty when right.</summary>
        public static List<string> Problems()
        {
            var p = new List<string>();
            void Want(bool ok, string what)
            {
                if (!ok)
                {
                    p.Add(what);
                }
            }
            const int v2 = TextIntents.DataV2, v3 = TextIntents.DataV3;
            Want(PostKind(FlareSource.Opened) == "opened" && PostKind(FlareSource.RecordDay) == "record" && PostKind(FlareSource.Arrested) == null && PostKind(FlareSource.Report) == null,
                "the player's posts map to the training's kinds; an arrest or a report has no replies of its own");
            Want(Reaction(FlareReplyKind.Agree, "opened") == "cheer" && Reaction(FlareReplyKind.Angry, "opened") == null && Reaction(FlareReplyKind.Kind, "selfie") == "kind"
                 && Reaction(FlareReplyKind.Kind, "opened") == "cheer" && Reaction(FlareReplyKind.Doubt, "selfie") == null && Reaction(FlareReplyKind.Love, null) == null,
                "a reply's reaction is one the training has for that kind of post (an angry pile-on keeps its written lines)");
            Want(Reaction(FlareRules.ReplyKind.Plain, "photo") == "cheer" && Reaction(FlareRules.ReplyKind.Cool, "selfie") == "cool",
                "a friend's reply maps to the training's reactions");
            Want(Talk((int)FlareTalk.Night) == "citizen.post.night_out" && Talk((int)FlareTalk.Accident) == null && Talk((int)FlareTalk.EventTomorrow) == "citizen.post.events",
                "the city's talk maps by topic; an accident keeps its written lines");
            Want(Official((int)FlareOfficialPost.Rate, 1) == "official.post.rate_up" && Official((int)FlareOfficialPost.Rate, 0) == "official.post.rate_down"
                 && Official((int)FlareOfficialPost.Notice, 0) == null, "an official post maps by its kind, the rate by its way");
            Want(Answer(FlareAnswer.Works, FlareAuthor.Official) == "official.answer.crew" && Answer(FlareAnswer.Transit, FlareAuthor.Official) == "official.answer.sorry_wait"
                 && Answer(FlareAnswer.Thanks, FlareAuthor.Business) == "business.reply.good" && Answer(FlareAnswer.Tycoon, FlareAuthor.Tycoon) == "tycoon.answer"
                 && Answer(FlareAnswer.Nothing, FlareAuthor.Official) == null, "an answer maps by what it says and who says it; the faults' verdicts keep their written lines");
            Want(Said(FlareAuthor.Official, v3) == "official.answer.said" && Said(FlareAuthor.Brand, v3) == "business.answer.said" && Said(FlareAuthor.Tycoon, v3) == "tycoon.answer.said"
                 && Said(FlareAuthor.Citizen, v3) == "citizen.answer.reply" && Said(FlareAuthor.Official, v2) == null && Said(FlareAuthor.Friend, v3) == null
                 && AnswersMention(FlareAnswer.Mention) && AnswersMention(FlareAnswer.Brand) && !AnswersMention(FlareAnswer.Thanks),
                "an answer to what the player typed is the account's own (data v3), never asked of a v2 pack");
            Want(Tycoon(-1) == "tycoon.boast" && Tycoon(0) == "tycoon.property" && Tycoon(3) == null && Tycoon(3, v3) == "tycoon.stake" && Tycoon(4, v3) == "tycoon.bid"
                 && Tycoon(6, v3) == "tycoon.suit" && Tycoon(6, v2) == null && Business(0) == "business.post.sale" && Business(5) == null,
                "a tycoon's and a business's posts map by their kind; the stake, the bid and the suit only for a v3 pack");
            Want(Dm(DmChat, FlareDmSpeaker.Citizen, v2) == "citizen.dm.chat" && Dm(DmCollab, FlareDmSpeaker.Influencer, v2) == "citizen.dm.collab"
                 && Dm(DmOffice, FlareDmSpeaker.Official, v2) == null && Dm(DmChat, FlareDmSpeaker.Friend, v2) == null
                 && Dm(DmChat, FlareDmSpeaker.Citizen, v3) == "citizen.dm.answer" && Dm(DmCollab, FlareDmSpeaker.Influencer, v3) == "citizen.dm.influencer"
                 && Dm(DmChat, FlareDmSpeaker.Friend, v3) == "friend.dm.answer" && Dm(DmChat, FlareDmSpeaker.Partner, v3) == "partner.dm.answer"
                 && Dm(DmOffice, FlareDmSpeaker.Official, v3) == "official.dm.answer" && Dm(DmChat, FlareDmSpeaker.Business, v3) == "business.dm.answer"
                 && Dm(DmChat, FlareDmSpeaker.Tycoon, v3) == "tycoon.dm.answer" && Dm("reallife_flare_dm_fan", FlareDmSpeaker.Citizen, v3) == null
                 && Dm(DmChat, FlareDmSpeaker.None, v3) == null,
                "a message's answer maps by who answers and the pack's data version (v2: a city person's chat and an influencer's; v3: every account, answering what was typed)");
            Want(Dm(FlareInviteRules.KeyStart + "yes_coffee", FlareDmSpeaker.Citizen, v3) == "citizen.dm.invite_yes" && Dm(FlareInviteRules.KeyStart + "yes_coffee", FlareDmSpeaker.Citizen, v2) == null
                 && Dm(FlareInviteRules.KeyStart + "maybe_influencer", FlareDmSpeaker.Influencer, v3) == "citizen.dm.invite_maybe"
                 && Dm(FlareInviteRules.KeyStart + "maybe_plan", FlareDmSpeaker.Citizen, v3) == null,
                "an invitation's answer is the decided intent for a v3 pack (an influencer as a city person; the plan on already keeps its line), the written line for a v2 pack");
            Want(DmSpeaker("c6", out bool inf, out int n) && !inf && n == 6 && DmSpeaker("c27", out inf, out n) && !inf && n == 27
                 && DmSpeaker("i:3", out inf, out n) && inf && n == 3 && !DmSpeaker("c:Ashby", out _, out _) && !DmSpeaker("o:0", out _, out _)
                 && !DmSpeaker("p:host", out _, out _) && !DmSpeaker("c", out _, out _) && !DmSpeaker("c-1", out _, out _) && !DmSpeaker(null, out _, out _),
                "a message's answerer is read from its account as Flare keys it (a city person 'c6', an influencer 'i:3'); the rest keep the written line");
            Want(DmSpeakerOf("f4", out n) == FlareDmSpeaker.Friend && n == 4 && DmSpeakerOf("p2", out n) == FlareDmSpeaker.Partner && n == 2
                 && DmSpeakerOf("o:1", out n) == FlareDmSpeaker.Official && n == 1 && DmSpeakerOf("b:Daily Brew", out _) == FlareDmSpeaker.Business
                 && DmSpeakerOf("k:Sam's Deli", out _) == FlareDmSpeaker.Business && DmSpeakerOf("t:Space Karen", out _) == FlareDmSpeaker.Tycoon
                 && DmSpeakerOf("p:host", out _) == FlareDmSpeaker.None && DmSpeakerOf("b", out _) == FlareDmSpeaker.None && DmSpeakerOf("s:Ana", out _) == FlareDmSpeaker.None
                 && DmSpeakerOf("c:Ashby", out _) == FlareDmSpeaker.None && DmSpeakerOf("l", out _) == FlareDmSpeaker.None && DmSpeakerOf("c6", out n) == FlareDmSpeaker.Citizen && n == 6,
                "every account is read: a friend 'f4', the partner 'p2', an official 'o:1', a brand or a business, a rival; another player, the boss, a colleague, a candidate and the paper are not answered by the model");
            var post = new FlarePost { Author = FlareAuthor.Player, Source = FlareSource.Opened };
            Want(Reply(post, FlareReplyKind.Agree, v3) == "citizen.reply.opened" && Reply(post, FlareReplyKind.Angry, v3) == null && Reply(post, FlareReplyKind.Doubt, v2) == "citizen.reply.opened",
                "a city reply under a built post of the player's is by its kind and reaction; an angry pile-on keeps its written lines");
            post.Text = "Doors open at nine.";
            Want(Reply(post, FlareReplyKind.Agree, v3) == "citizen.reply.words" && Reply(post, FlareReplyKind.Agree, v2) == "citizen.reply.opened",
                "a post with typed words is answered on its words by a v3 pack, by its kind by a v2 pack");
            var report = new FlarePost { Author = FlareAuthor.Player, Source = FlareSource.Report, Arg = (int)FlareReport.Lamp };
            var slots = new TextSlots();
            MineSlots(slots, report, "Alex", "Midtown");
            Want(Reply(report, FlareReplyKind.Kind, v3) == "citizen.comment.mine_report" && Reply(report, FlareReplyKind.Kind, v2) == null
                 && slots.Get("problem") == "a street light out" && slots.Get("player") == "Alex" && slots.Get("district") == "Midtown"
                 && Mine(new FlarePost { Source = FlareSource.Review, Arg = 2 }) == "mine_review_bad" && Mine(new FlarePost { Source = FlareSource.Selfie }) == null,
                "a post of the player's with no reactions of its own is commented on as its kind (data v3 mine_*), with its values");
            Want(AnswerTopic(new FlarePost { Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Arg = (int)FlareTalk.Night * 10 }) == "night out"
                 && AnswerTopic(new FlarePost { Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Arg = (int)FlareTalk.Accident * 10 }) == null,
                "a resident answering under their own post names its topic as the training does ('night out'); a topic it lacks has no answer");
            FlarePost P(FlareAuthor a, FlareSource s, int arg) => new FlarePost { Author = a, Source = s, Arg = arg };
            Want(Comment(P(FlareAuthor.Citizen, FlareSource.CityTalk, (int)FlareTalk.Accident * 10), -1, v3) == "citizen.comment.accident"
                 && Comment(P(FlareAuthor.Citizen, FlareSource.CityTalk, (int)FlareTalk.Accident * 10), -1, v2) == null
                 && Comment(P(FlareAuthor.Citizen, FlareSource.CityTalk, (int)FlareTalk.Weather * 10), -1, v3) == "citizen.comment.words"
                 && Comment(P(FlareAuthor.Citizen, FlareSource.CityTalk, (int)FlareTalk.Weather * 10), -1, v2) == "citizen.thread.weather"
                 && Comment(P(FlareAuthor.Influencer, FlareSource.Niche, 0), (int)FlareNiche.Tech, v3) == "citizen.comment.niche_tech"
                 && Comment(P(FlareAuthor.Ledger, FlareSource.Headline, 0), -1, v3) == "citizen.comment.ledger_headline"
                 && Comment(P(FlareAuthor.Business, FlareSource.BusinessPost, 3), -1, v3) == "citizen.comment.biz_late"
                 && Comment(P(FlareAuthor.Tycoon, FlareSource.BusinessPost, 1), -1, v3) == "citizen.comment.tycoon_boast"
                 && Comment(P(FlareAuthor.Tycoon, FlareSource.BusinessPost, FlareLiveRules.TycoonMoveArg + 6), -1, v3) == "citizen.comment.tycoon_suit"
                 && Comment(P(FlareAuthor.Tycoon, FlareSource.BusinessPost, FlareLiveRules.TycoonMoveArg), -1, v3) == "citizen.react.tycoon_buys"
                 && Comment(P(FlareAuthor.Citizen, FlareSource.Review, 5), -1, v3) == "citizen.comment.review_good"
                 && Comment(P(FlareAuthor.Company, FlareSource.Results, 0), -1, v3) == "citizen.comment.results_down"
                 && Comment(P(FlareAuthor.Official, FlareSource.Official, (int)FlareOfficialPost.Advice * 10), -1, v3) == "citizen.comment.official_advice"
                 && Comment(P(FlareAuthor.Brand, FlareSource.BrandNews, 0), -1, v3) == "citizen.comment.brand_news"
                 && Comment(P(FlareAuthor.Influencer, FlareSource.Apology, 0), -1, v3) == "citizen.comment.words"
                 && Comment(P(FlareAuthor.Influencer, FlareSource.Apology, 0), -1, v2) == null,
                "a v3 pack's comments: every kind of post has its own (an accident, a niche, the paper, a shop's, a rival's move, a review, results, an official notice), a post on a v2 topic or of no kind on its words; v2's maps stay");
            var c = new FlareCitizen { Index = 3, First = "Dana", Last = "Reyes", Handle = "@dana_reyes12", Persona = FlarePersona.Student };
            int age = Age(c, 77);
            Want(age == FlareLiveRules.CitizenAge(77, 3) && Age(c, 77) == age, "a city person's card says their own age, the one their face has, the same every time");
            c.Persona = FlarePersona.Grumpy;
            Want(Style(c, 77).StartsWith("grumpy", System.StringComparison.Ordinal) && Style(c, 77) == Style(c, 77), "the grumpy write grumpily, the same every time");
            bool moods = true, grumpyLow = true;
            for (int d = 0; d < 40; d++)
            {
                moods &= Mood(77, 3, d, false) == Mood(77, 3, d, false) && System.Array.IndexOf(TextWords.Moods, Mood(77, 3, d, false)) >= 0;
                grumpyLow &= System.Array.IndexOf(TextWords.Moods, Mood(77, 3, d, true)) <= 2;
            }
            Want(moods && grumpyLow && Mood(77, 3, 1, false) != null, "a city person's mood is one of the five, the same all day; the grumpy never better than fine");
            return p;
        }
    }

    /// <summary>
    /// Which of Flare's items (a post by its number, an answer by its key) have their words asked, and how often. An item is
    /// asked once and then left while its request waits or its words are kept; a request handed back with no words (the model
    /// stopped or restarted on a change of device or threads, still loading past the deadline, the switch turned off, the
    /// checker refusing the words) frees it, so it is asked again on Flare's next event (the hour) while it is still to come,
    /// up to <see cref="Most"/> times in all. Game-free (EconomyCheck, FlareDmTextsChecks.cs).
    /// COST: a set and a count per item asked; nothing per frame.
    /// </summary>
    public sealed class FlareTextsAsked<T>
    {
        public const int Most = 3;   // asks of one item at most: a restart or a miss is asked again, a third failure keeps the written line

        private readonly HashSet<T> _waiting = new HashSet<T>();
        private readonly Dictionary<T, int> _asks = new Dictionary<T, int>();

        /// <summary>True if the item may be asked now: not waiting, not worded, and asked fewer than <see cref="Most"/> times.</summary>
        public bool May(T item)
        {
            return !_waiting.Contains(item) && (!_asks.TryGetValue(item, out int n) || n < Most);
        }

        /// <summary>The item was asked (the hub took the request).</summary>
        public void Asked(T item)
        {
            _waiting.Add(item);
            _asks[item] = _asks.TryGetValue(item, out int n) ? n + 1 : 1;
        }

        /// <summary>Its request came back: with words it stays asked (kept, drawn from the store); without, it may be asked again.</summary>
        public void Ended(T item, bool words)
        {
            if (!words)
            {
                _waiting.Remove(item);
            }
        }

        /// <summary>A new account or a stop: nothing asked.</summary>
        public void Clear()
        {
            _waiting.Clear();
            _asks.Clear();
        }
    }
}
