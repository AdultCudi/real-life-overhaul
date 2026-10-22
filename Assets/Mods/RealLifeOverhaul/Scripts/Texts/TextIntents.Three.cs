using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The intents of data v3 (defs_v3.INTENTS): answers to what the player wrote, and the comments under every kind of post.</summary>
    public static partial class TextIntents
    {
        /// <summary>The comment kinds whose post is in history and its topic on the card (defs_v3.THREAD_TOPIC).</summary>
        public static readonly string[] ThreadKinds = { "accident", "fire", "market", "policy", "niche_fashion", "niche_tech", "niche_business" };
        public static readonly string[] ThreadTopics = { "an accident", "a fire", "the stock market", "a new council policy", "fashion", "tech", "business" };

        /// <summary>The comment kinds whose post is told in the news and facts (defs_v3.NEWS_KINDS): the kind, its news, its facts.</summary>
        public static readonly string[][] NewsKinds =
        {
            new[] { "biz_late", "{shop} posted that it is open late tonight", "business: {shop}" },
            new[] { "biz_hiring", "{shop} posted that it is hiring", "business: {shop}" },
            new[] { "biz_thanks", "{shop} thanked its customers for a good week", "business: {shop}" },
            new[] { "biz_rain", "{shop} posted that it is raining and the shop is warm and dry", "business: {shop}" },
            new[] { "biz_closed", "{shop} posted that it is closed today for a staff day", "business: {shop}" },
            new[] { "brand_news", "{shop}, a city chain, posted news about its shops", "business: {shop}" },
            new[] { "review_good", "{other} reviewed {shop}: {stars} stars", "business: {shop}", "stars: {stars}" },
            new[] { "review_bad", "{other} reviewed {shop}: {stars} stars", "business: {shop}", "stars: {stars}" },
            new[] { "results_up", "{target}, a company on the stock market, posted results better than expected", "company: {target}" },
            new[] { "results_down", "{target}, a company on the stock market, posted results worse than expected", "company: {target}" },
            new[] { "official_notice", "City Office posted a notice about the week's works and a council meeting", "source: City Office", "district: {district}" },
            new[] { "official_advice", "City Police posted advice on keeping homes and shops safe after a break-in in {district}", "source: City Police", "district: {district}" },
            new[] { "official_service", "City Transit posted that the buses are slowed by the weather and the timetable is changing", "source: City Transit" },
            new[] { "official_policy", "City Office posted that a new council policy has come in", "source: City Office" },
            new[] { "tycoon_stake", "{company}, owned by {tycoon}, bought a block of shares in {target} and is pressing its board", "who: {tycoon}", "company: {target}" },
            new[] { "tycoon_bid", "{company}, owned by {tycoon}, made a bid to buy {target}", "who: {tycoon}", "company: {target}" },
            new[] { "tycoon_lobby", "{tycoon} met the council about inspections", "who: {tycoon}" },
            new[] { "tycoon_suit", "{tycoon} is suing {player} in the civil court", "who: {tycoon}", "sued: {player}" },
            new[] { "tycoon_press", "{tycoon} has a piece in the City Ledger attacking a rival's business", "who: {tycoon}" },
            new[] { "tycoon_boast", "{tycoon} posted a boast", "who: {tycoon}" },
            new[] { "mine_endorsed", "{player} endorsed a candidate in the city election", "poster: {player}" },
            new[] { "mine_good_deed", "{player} posted about helping somebody out", "poster: {player}" },
            new[] { "mine_arrested", "{player} posted about being arrested", "poster: {player}" },
            new[] { "mine_sponsored", "{player} posted a sponsored post for their business {shop}", "poster: {player}", "business: {shop}" },
            new[] { "mine_shares", "{player} posted about their shares in {target} after its results", "poster: {player}", "company: {target}" },
            new[] { "mine_issue", "{player} posted about {issue} in {district}", "poster: {player}", "district: {district}" },
            new[] { "mine_campaign", "{player} posted about their run for office", "poster: {player}" },
            new[] { "mine_graduated", "{player} posted that they graduated from the university", "poster: {player}" },
            new[] { "mine_report", "{player} reported {problem} in {district} to the City Office", "poster: {player}", "district: {district}" },
            new[] { "mine_review_good", "{player} reviewed {shop}: {stars} stars", "poster: {player}", "business: {shop}", "stars: {stars}" },
            new[] { "mine_review_bad", "{player} reviewed {shop}: {stars} stars", "poster: {player}", "business: {shop}", "stars: {stars}" },
            new[] { "mine_announce", "{player} announced {what} at their business {shop}", "poster: {player}", "business: {shop}" }
        };

        private static void V3()
        {
            // Answers to a message of the player's (defs_v3.make_dm): the typed message is history's last line.
            R3("citizen.dm.answer", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} answers your message", None, "flare dm reply");
            R3("citizen.dm.influencer", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People,
                "{first} is an influencer and answers your message; hiring them is done from your Business tab", None, "flare dm reply");
            R3("friend.dm.answer", "friend", "flare_reply", 160, TextFace.Friend, TextUse.People, "{first} answers your message on Flare", None, "flare dm reply");
            R3("partner.dm.answer", "partner", "flare_reply", 160, TextFace.Friend, TextUse.People, "{first} answers your message on Flare", None, "flare dm reply");
            R3("official.dm.answer", "official", "flare_reply", 160, TextFace.Official, TextUse.Official, "{self} answers your message", None, "flare dm reply");
            R3("business.dm.answer", "business", "flare_reply", 160, TextFace.Business, TextUse.People, "{self} answers your message", None, "flare dm reply");
            R3("tycoon.dm.answer", "tycoon", "flare_reply", 160, TextFace.Tycoon, TextUse.Rivals, "{self} answers your message", None, "flare dm reply");

            // Invitations the game decided (defs_v3.make_invite_decided), with the training's own reasons.
            R3("citizen.dm.invite_yes", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} says yes to your invitation; the plan is booked",
                L("outing: {outing}", "place: {place}", "time: {time}"), "flare dm reply", L("{place}", "{time}"));
            R3("citizen.dm.invite_maybe", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} is not sure about your invitation yet",
                L("outing: {outing}", "reason: {reason}"), "flare dm reply");
            R3("citizen.dm.invite_no", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} says no to your invitation",
                L("outing: {outing}", "reason: {reason}"), "flare dm reply");

            // Replies and answers to what the player typed in a post or a reply (make_reply_words, make_answer_reply, make_account_answer).
            R3("citizen.reply.words", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} replies to what you wrote in your post", None, "flare reply");
            R3("citizen.answer.reply", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} answers your reply under their post about {topic}", None,
                "flare reply");
            R3("official.answer.said", "official", "flare_reply", 140, TextFace.Official, TextUse.Official, "{self} answers what you wrote to it", None,
                "flare reply: official answer");
            R3("business.answer.said", "business", "flare_reply", 140, TextFace.Business, TextUse.Posts, "{self} answers what you wrote to it", None, "flare reply: business");
            R3("tycoon.answer.said", "tycoon", "flare_reply", 160, TextFace.Tycoon, TextUse.Rivals, "{self} answers what you wrote to them", None, "flare reply: tycoon");

            // Comments on a post's words, and answers to an earlier comment under it (make_comment_pair).
            R3("citizen.comment.words", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} comments on a post by {other}", None, "flare reply");
            R3("citizen.comment.reply", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} answers {other2}'s comment under a post by {other}", None,
                "flare reply");

            // The rivals' moves that had no post (make_typost): RivalMoveKind Stake, Bid and Suit.
            R3("tycoon.stake", "tycoon", "flare_post", 200, TextFace.Tycoon, TextUse.Rivals,
                "{company} has bought a block of shares in {target}, your listed company, and is pressing its board", L("company: {target}"), "flare post: tycoon", L("{target}"));
            R3("tycoon.bid", "tycoon", "flare_post", 200, TextFace.Tycoon, TextUse.Rivals, "{company} has made a bid to buy {target}, your listed company",
                L("company: {target}"), "flare post: tycoon", L("{target}"));
            R3("tycoon.suit", "tycoon", "flare_post", 200, TextFace.Tycoon, TextUse.Rivals, "{self} is suing {player} in the civil court", L("sued: {player}"),
                "flare post: tycoon");

            // The comments under each kind of post (make_comment): a thread kind has the post in history, a news kind in news and facts.
            for (int k = 0; k < ThreadKinds.Length; k++)
            {
                R3("citizen.comment." + ThreadKinds[k], "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies,
                    "{first} comments on a post by {other} about " + ThreadTopics[k], L("topic: " + ThreadTopics[k]), "flare reply");
            }
            R3("citizen.comment.ledger_headline", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} comments on the City Ledger's headline", None,
                "flare reply");
            for (int k = 0; k < NewsKinds.Length; k++)
            {
                string[] n = NewsKinds[k];
                var facts = new string[n.Length - 2];
                Array.Copy(n, 2, facts, 0, facts.Length);
                R3("citizen.comment." + n[0], "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} comments on a post: " + n[1], facts, "flare reply");
            }
        }
    }
}
