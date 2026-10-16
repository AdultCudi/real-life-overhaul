using System.Collections.Generic;
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
    }

    /// <summary>
    /// Which trained intent each of Flare's worded posts is, for RLO Texts (Scripts/Texts): the game decides the post as
    /// before (its source, kind, author, reaction and time); this says which card the model is given for its words. A post
    /// with no intent here keeps its written line. Also the city's people as a card's speaker: their age and way of writing,
    /// stable for each person from the city's seed and their persona. And the comments under posts that are not the
    /// player's (FlareCommentRules): their intent and card; they have no written line, so a post with no intent for its
    /// comments shows only their count. Game-free (EconomyCheck, TextsChecks.cs, FlareCommentChecks.cs).
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

        /// <summary>A rival tycoon's post: their move (Rivals.RivalMoveKind) or, with none, a boast; null for a move the training has no posts on.</summary>
        public static string Tycoon(int move)
        {
            switch (move)
            {
                case -1: return "tycoon.boast";
                case 0: return "tycoon.property";
                case 1: return "tycoon.outlet";
                case 2: return "tycoon.poach";
                case 5: return "tycoon.lobby";
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

        /// <summary>An answer of the city's to the player's message, by its line's key; null for the City Office's (no such card).</summary>
        public static string Dm(string key)
        {
            switch (key)
            {
                case "reallife_flare_dm_answer_chat": return "citizen.dm.chat";
                case "reallife_flare_dm_answer_collab": return "citizen.dm.collab";
                default: return null;
            }
        }

        // ---------------------------------------------------------------- comments under posts that are not the player's

        /// <summary>
        /// The trained intent of a comment under a post that is not the player's (FlareCommentRules), by what the post is
        /// about: a resident answering another resident's post on its topic (citizen.thread.*, defs_flare.make_cit_thread),
        /// a resident reacting to an account's news (citizen.react.*, make_cit_react), a stranger answering another player's
        /// post (citizen.reply.*, make_cit_reply). 'niche' is an influencer's (FlareNiche), -1 for anybody else. Null where
        /// the training has nothing that fits: such a post shows only its count of replies.
        /// </summary>
        public static string Comment(FlarePost p, int niche)
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
                        default: return null;   // (fashion, tech, business: no topic of the training's)
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

        /// <summary>
        /// A comment's card: its intent, its use (Flare's comments and replies) and the values the intent's lines take from the
        /// post it answers. The speaker (r.Who) is filled by the caller. False if a value the card needs is missing (nothing
        /// is made up: that comment is not written).
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
            if (intent.StartsWith("citizen.thread.", System.StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(post.First) || string.IsNullOrEmpty(post.Handle) || string.IsNullOrEmpty(post.Words))
                {
                    return false;
                }
                r.Slots.Set("other", post.First);
                Optional(r, "district", post.District);
                r.History.Add(new TextSaid(post.Handle, post.Words));
                return true;
            }
            if (intent.StartsWith("citizen.react.", System.StringComparison.Ordinal))
            {
                Optional(r, "district", post.District);
                Optional(r, "home", post.Home);
                Optional(r, "tycoon", post.Tycoon);
                Optional(r, "company", post.Company);
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

        private static void Optional(TextRequest r, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                r.Slots.Set(name, value);
            }
        }

        // ---------------------------------------------------------------- the city's people as speakers

        /// <summary>A city person's age: stable, in the range of their persona (students young, retirees old).</summary>
        public static int Age(FlareCitizen c, int citySeed)
        {
            int seed = citySeed * 31 + c.Index;
            switch (c.Persona)
            {
                case FlarePersona.Student: return TextWords.Age(seed, 18, 24);
                case FlarePersona.Retiree: return TextWords.Age(seed, 62, 75);
                case FlarePersona.Parent: return TextWords.Age(seed, 29, 48);
                default: return TextWords.Age(seed, 21, 60);
            }
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

        /// <summary>Every intent Flare's map can give.</summary>
        public static List<string> Mapped()
        {
            var list = new List<string>(48);
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
            for (int m = -1; m < 8; m++)
            {
                Add(list, Tycoon(m));
            }
            for (int b = 0; b < 7; b++)
            {
                Add(list, Business(b));
            }
            Add(list, Dm("reallife_flare_dm_answer_chat"));
            Add(list, Dm("reallife_flare_dm_answer_collab"));
            list.Add("citizen.review");
            CommentIntents(list);
            return list;
        }

        /// <summary>Every intent a comment can be asked with (every source, kind, author and niche swept), added to 'list' once each.</summary>
        public static void CommentIntents(List<string> list)
        {
            FlareAuthor[] authors = { FlareAuthor.Citizen, FlareAuthor.Official, FlareAuthor.Business, FlareAuthor.Tycoon, FlareAuthor.Friend, FlareAuthor.Influencer, FlareAuthor.Network };
            var p = new FlarePost();
            for (int s = 1; s <= (int)FlareSource.Answer; s++)
            {
                for (int a = 0; a < authors.Length; a++)
                {
                    for (int arg = 0; arg < 100; arg++)
                    {
                        for (int niche = -1; niche <= (int)FlareNiche.Business; niche++)
                        {
                            p.Source = (FlareSource)s;
                            p.Author = authors[a];
                            p.Arg = arg;
                            Add(list, Comment(p, niche));
                        }
                    }
                }
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
            Want(Tycoon(-1) == "tycoon.boast" && Tycoon(0) == "tycoon.property" && Tycoon(3) == null && Business(0) == "business.post.sale" && Business(5) == null,
                "a tycoon's and a business's posts map by their kind");
            Want(Dm("reallife_flare_dm_answer_chat") == "citizen.dm.chat" && Dm("reallife_flare_dm_answer_office") == null, "a message's answer maps by its line");
            var c = new FlareCitizen { Index = 3, First = "Dana", Last = "Reyes", Handle = "@dana_reyes12", Persona = FlarePersona.Student };
            int age = Age(c, 77);
            Want(age >= 18 && age <= 24 && Age(c, 77) == age, "a student's age is young and the same every time");
            c.Persona = FlarePersona.Grumpy;
            Want(Style(c, 77).StartsWith("grumpy", System.StringComparison.Ordinal) && Style(c, 77) == Style(c, 77), "the grumpy write grumpily, the same every time");
            return p;
        }
    }
}
