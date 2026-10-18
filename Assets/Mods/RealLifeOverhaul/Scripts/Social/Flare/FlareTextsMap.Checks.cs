using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's map to RLO Texts, its self-checks: every intent it can give, and where it is wrong.</summary>
    public static partial class FlareTextsMap
    {
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

        /// <summary>Adds every intent a comment can be asked with to <paramref name="list"/>, once each, every source, kind, author and niche swept.</summary>
        public static void CommentIntents(List<string> list, int data = TextIntents.DataV2)
        {
            FlareAuthor[] authors = { FlareAuthor.Citizen, FlareAuthor.Official, FlareAuthor.Business, FlareAuthor.Tycoon, FlareAuthor.Friend, FlareAuthor.Influencer, FlareAuthor.Network,
                FlareAuthor.Brand, FlareAuthor.Company, FlareAuthor.Ledger };
            var p = new FlarePost();
            for (int s = 1; s <= (int)FlareSource.Answer; s++)
            {
                for (int a = 0; a < authors.Length; a++)
                {
                    for (int arg = 0; arg < 200; arg++)   // a city post's Arg is its topic times ten
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
                Add(list, "citizen.comment.reply");   // a comment on a post's words that answers an earlier one
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
                "every account is read: a friend 'f4', the partner 'p2', an official 'o:1', a brand or a business, a rival; another player, "
                + "the boss, a colleague, a candidate and the paper are not answered by the model");
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
                "a v3 pack's comments: every kind of post has its own (an accident, a niche, the paper, a shop's, a rival's move, a review, results, "
                + "an official notice), a post on a v2 topic or of no kind on its words; v2's maps stay");
            var c = new FlareCitizen { Index = 3, First = "Dana", Last = "Reyes", Handle = "@dana_reyes12", Persona = FlarePersona.Student };
            int age = Age(c, 77);
            Want(age == FlareLiveRules.CitizenAge(77, 3) && Age(c, 77) == age, "a city person's card says their own age, the one their face has, the same every time");
            c.Persona = FlarePersona.Grumpy;
            // Against the person's own seed, and another card for the same person: the style is theirs, whatever else the card says.
            string own = TextWords.CitizenStyle(77 * 31 + c.Index);
            int comma = own.IndexOf(',');
            var same = new FlareCitizen { Index = 3, First = "Test", Last = "Person", Handle = "@test_person", Persona = FlarePersona.Grumpy };
            Want(Style(c, 77) == "grumpy" + (comma > 0 ? own.Substring(comma) : string.Empty) && Style(same, 77) == Style(c, 77)
                 && Style(new FlareCitizen { Index = 3, Persona = FlarePersona.Student }, 77) == own,
                "the grumpy write grumpily, the rest of their style their own seed's, the same every time");
            bool moods = true, grumpyLow = true, seeded = true;
            for (int d = 0; d < 40; d++)
            {
                int roll = FlareRules.Roll(77 * 31 + 3, d, 1207);
                seeded &= Mood(77, 3, d, false) == TextWords.Moods[roll % TextWords.Moods.Length] && Mood(77, 3, d, true) == TextWords.Moods[roll % 3];
                moods &= Array.IndexOf(TextWords.Moods, Mood(77, 3, d, false)) >= 0;
                grumpyLow &= Array.IndexOf(TextWords.Moods, Mood(77, 3, d, true)) <= 2;
            }
            Want(seeded, "a city person's mood is their own seed's for the day, the grumpy's from the lower three");
            Want(moods && grumpyLow && Mood(77, 3, 1, false) != null, "a city person's mood is one of the five, the same all day; the grumpy never better than fine");
            return p;
        }
    }
}
