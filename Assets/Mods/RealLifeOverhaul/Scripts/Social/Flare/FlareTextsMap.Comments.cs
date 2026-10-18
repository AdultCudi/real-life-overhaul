using System;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's map to RLO Texts, the comments under posts that are not the player's: their intents and cards.</summary>
    public static partial class FlareTextsMap
    {
        /// <summary>
        /// The trained intent of a comment under a post that is not the player's, by what the post is about (a thread, a
        /// reaction, a stranger's reply); <paramref name="niche"/> is an influencer's, -1 for anybody else. With data v3 every
        /// kind has comments, a post on a v2 topic or of no kind on its words, as those answered the post better. Null: only the
        /// count shows.
        /// </summary>
        public static string Comment(FlarePost p, int niche, int data = TextIntents.DataV2)
        {
            string v2 = CommentV2(p, niche);
            if (data < TextIntents.DataV3)
            {
                return v2;
            }
            if (v2 != null && !v2.StartsWith("citizen.thread.", StringComparison.Ordinal))
            {
                return v2;
            }
            string kind = CommentKind(p, niche);
            return kind != null ? "citizen.comment." + kind : p.Author != FlareAuthor.Player ? "citizen.comment.words" : null;
        }

        /// <summary>What a friend must care about to comment under a post, by its topic whatever intent words it; None: no friend.</summary>
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

        /// <summary>The data v3 comment kind of a post the v2 training has none for (defs_v3.COMMENT_KINDS); null otherwise.</summary>
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

        /// <summary>The reaction of a stranger's comment under another player's post, where the training has it; null otherwise.</summary>
        public static string CommentReaction(FlarePost p, int seed, int slot)
        {
            return Reaction(FlareLiveRules.ReplyKindOf(p, FlareRules.Roll(seed, slot, 847)), PostKind(p.Source));
        }

        /// <summary>True for a comment intent that can answer an earlier comment under its post (data v3's citizen.comment.*).</summary>
        public static bool AnswersComments(string intent) => intent != null && intent.StartsWith("citizen.comment.", StringComparison.Ordinal);

        /// <summary>A comment's card from the post it answers, the caller filling the speaker; false if a value is missing.</summary>
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
            if (intent.StartsWith("citizen.thread.", StringComparison.Ordinal))
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
            if (intent.StartsWith("citizen.react.", StringComparison.Ordinal))
            {
                Optional(r.Slots, "district", post.District);
                Optional(r.Slots, "home", post.Home);
                Optional(r.Slots, "tycoon", post.Tycoon);
                Optional(r.Slots, "company", post.Company);
                return true;
            }
            if (intent.StartsWith("citizen.reply.", StringComparison.Ordinal))
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

        // A data v3 comment (defs_v3.make_comment): the post in history, the paper's headline as the City Ledger's, or a news
        // kind told in values; an earlier comment last.
        private static bool CommentCardV3(TextRequest r, string kind, FlareCommentPost post)
        {
            bool earlier = !string.IsNullOrEmpty(post.EarlierHandle) && !string.IsNullOrEmpty(post.EarlierWords);
            bool words = kind == "words" || kind == "reply";
            if (words || Array.IndexOf(TextIntents.ThreadKinds, kind) >= 0)
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
    }
}
