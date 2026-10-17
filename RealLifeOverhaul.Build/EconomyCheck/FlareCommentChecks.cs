using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Texts;

// The comments under posts that are not the player's (Flare/FlareCommentRules, FlareTextsMap.Comment and CommentCard,
// FlareDirector.Comments, the thread in FlareApp.Screens): a post that showed n comments
// was empty when opened, and only RLO Texts writes them. Held here: the
// comments written out never outnumber the card's count and the "more" line makes up the rest at every minute; each
// comes in only when the count reaches it; the same post gives the same comments every time, after a save and on a
// guest's copy in a Going Public session (only the city's people under a shared post; the host's key reaches the guest);
// the intent of every kind of post and the cards of the new requests byte for byte against the training generator's own;
// the texts in three languages; nothing per frame and nothing allocated drawing a plan (source and allocation checks).
internal static partial class Program
{
    private static void FlareComments()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        CommentsWritten();
        CommentsAgreeWithCount();
        CommentsSame();
        CommentsShared();
        CommentsWho();
        CommentsMap();
        CommentsCards();
        CommentsKept();
        CommentsTexts();
        CommentsSources();
        CommentsCost();
        Console.WriteLine("  flare comments: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A post of the city's people about the day, up at day 4, 12:30.
    private static FlarePost CommentPost(int id, int replies, int reach, FlareAuthor author = FlareAuthor.Citizen, FlareSource source = FlareSource.CityTalk, int arg = (int)RealLifeOverhaul.Flare.FlareTalk.Weather * 10)
    {
        var p = new FlarePost { Id = id, Author = author, Who = 3, Source = source, Arg = arg, Seed = 1000 + id * 7, Replies = replies, Reach = reach, Likes = reach / 8, District = "Midtown" };
        FlareLiveRules.SetAt(p, FlareLiveRules.Now(4, 12, 30));
        return p;
    }

    // ---------------------------------------------------------------- how many are written out

    private static void CommentsWritten()
    {
        bool every = true;
        for (int total = -3; total <= 3000; total++)
        {
            int w = FlareCommentRules.Written(total);
            every &= w == (total <= 0 ? 0 : Math.Min(total, FlareCommentRules.WrittenMost));
        }
        Check(every, "Flare comments: every reply the count promises is written out, up to " + FlareCommentRules.WrittenMost + " (what a phone's thread shows before \"more\")");
        Check(FlareCommentRules.Written(0) == 0 && FlareCommentRules.Written(1) == 1 && FlareCommentRules.Written(3) == 3 && FlareCommentRules.Written(16) == 16 && FlareCommentRules.Written(300) == 16
              && FlareCommentRules.WrittenMost <= 16, "Flare comments: 0 none, 1 one, 3 three (the screenshot's post), 16 sixteen, 300 sixteen and the rest the \"more\" line; a slot fits a like's key");
        Check(FlareCommentRules.Plan(CommentPost(1, 0, 600), 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, new List<FlareComment>()) == 0,
            "Flare comments: a post with no replies has none (the thread keeps its note)");
        var mine = CommentPost(2, 5, 600, FlareAuthor.Player);
        var reply = CommentPost(3, 5, 600);
        reply.ReplyTo = 1;
        Check(!FlareCommentRules.Has(mine) && !FlareCommentRules.Has(reply) && FlareCommentRules.Has(CommentPost(4, 5, 600)),
            "Flare comments: only a post that is not the player's and is not itself a reply has comments written out");
    }

    // ---------------------------------------------------------------- the thread and the card agree, minute by minute

    private static void CommentsAgreeWithCount()
    {
        var plan = new List<FlareComment>();
        bool agree = true, late = true, order = true, inDay = true, none = true;
        string first = null;
        for (int k = 0; k < 400; k++)
        {
            int total = 1 + FlareRules.Roll(5, k, 1) % (k % 4 == 0 ? 900 : 12);
            FlarePost p = CommentPost(100 + k, total, 60 + FlareRules.Roll(5, k, 2) * 9);
            FlareCommentRules.Plan(p, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
            none &= FlareCommentRules.Due(plan, p.At) == 0;
            for (int i = 0; i < plan.Count; i++)
            {
                FlareComment c = plan[i];
                int threshold = FlareCommentRules.Threshold(i, plan.Count, total);
                bool ok = FlareLiveRules.Shown(total, p, c.At) >= threshold && FlareLiveRules.Shown(total, p, c.At - 1) < threshold;
                late &= ok;
                order &= i == 0 || c.At >= plan[i - 1].At;
                inDay &= c.At > p.At && c.At <= p.At + 1440;
                if (!ok && first == null)
                {
                    first = "post " + p.Id + " slot " + i + " at +" + (c.At - p.At);
                }
            }
            for (long now = p.At - 5; now <= p.At + 1500; now += 7)
            {
                int shown = FlareLiveRules.Shown(total, p, now), due = FlareCommentRules.Due(plan, now);
                agree &= due <= shown && due + FlareCommentRules.More(shown, due) == shown && due <= FlareCommentRules.WrittenMost;
            }
        }
        Check(agree, "Flare comments: at every minute the comments written out never outnumber the card's count, and the \"more\" line makes up the rest exactly");
        Check(late && none, "Flare comments: each comes in only when the count reaches it (the minute before, the count is short of it), none the moment the post goes up"
                            + (first != null ? " (wrong: " + first + ")" : ""));
        Check(order && inDay, "Flare comments: they come in order, after the post and within its day, growing over the hours as the count does");
    }

    // ---------------------------------------------------------------- the same every time, and after a save

    private static void CommentsSame()
    {
        FlareFile f = FlareRules.Open(31, 1, 10, 50);
        var posts = new List<FlarePost>();
        for (int i = 0; i < 6; i++)
        {
            FlarePost p = CommentPost(f.NextId++, 3 + i * 40, 400 + i * 2000, i % 2 == 0 ? FlareAuthor.Citizen : FlareAuthor.Official, i % 2 == 0 ? FlareSource.CityTalk : FlareSource.Official,
                i % 2 == 0 ? (int)RealLifeOverhaul.Flare.FlareTalk.Food * 10 : (int)FlareOfficialPost.Patrols * 10);
            f.Posts.Add(p);
            posts.Add(p);
        }
        FlareFile back = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        bool same = back != null, again = true;
        var a = new List<FlareComment>();
        var b = new List<FlareComment>();
        for (int i = 0; i < posts.Count && same; i++)
        {
            FlarePost loaded = back.Post(posts[i].Id);
            FlareCommentRules.Plan(posts[i], 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, a);
            FlareCommentRules.Plan(loaded, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, b);
            same &= loaded != null && CommentsEqual(a, b) && a.Count > 0;
            FlareCommentRules.Plan(posts[i], 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, b);
            again &= CommentsEqual(a, b);
        }
        Check(again, "Flare comments: the same post gives the same comments every time its thread is opened (who, when, likes)");
        Check(same, "Flare comments: the same comments after a save and a load, with nothing of them saved (they come from the post)");
    }

    private static bool CommentsEqual(List<FlareComment> a, List<FlareComment> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Slot != b[i].Slot || a[i].At != b[i].At || a[i].Kind != b[i].Kind || a[i].Who != b[i].Who || a[i].Likes != b[i].Likes)
            {
                return false;
            }
        }
        return true;
    }

    // ---------------------------------------------------------------- Going Public: the same on every machine

    private static void CommentsShared()
    {
        FlareFile host = FlareRules.Open(31, 1, 10, 50);
        host.Net.CitySeed = 4242;
        FlarePost p = CommentPost(37, 60, 3000, FlareAuthor.Citizen, FlareSource.CityTalk, (int)RealLifeOverhaul.Flare.FlareTalk.Night * 10);
        host.Posts.Add(p);
        var line = new StringBuilder();
        FlareNetShare.WritePost(line, host, p, "host", "Host");
        bool read = FlareNetShare.ReadPost(line.ToString().Split('|'), out FlarePost copy, out int replyNet);
        copy.Id = 5;   // (the guest's own number for it)
        var people = new List<FlarePerson> { new FlarePerson { Index = 2, First = "Ana", Stage = 4, Taste = FlareTaste.Night | FlareTaste.Food | FlareTaste.Cheers } };
        var influencers = new List<FlareInfluencer> { new FlareInfluencer { Id = 1, Handle = "@nightowl" } };
        var onHost = new List<FlareComment>();
        var onGuest = new List<FlareComment>();
        FlareCommentRules.Plan(p, FlareLiveRules.CitySeed(host), FlareLiveRules.Citizens, null, FlareTaste.None, null, onHost);
        FlareCommentRules.Plan(copy, 4242, FlareLiveRules.Citizens, people, FlareTaste.Night, influencers, onGuest);
        Check(read && copy.Seed != p.Seed && CommentsEqual(onHost, onGuest) && onHost.Count > 0,
            "Flare comments Going Public: a guest's copy of a shared post has the host's comments (who, when, likes) from what the post line already carries; nothing new is sent");
        bool keys = true;
        for (int i = 0; i < onHost.Count; i++)
        {
            string hostKey = FlareCommentRules.Key(p, onHost[i]), guestKey = FlareCommentRules.Key(copy, onGuest[i]);
            keys &= hostKey.StartsWith("fc:37:", StringComparison.Ordinal) && TextsShare.NetKey(hostKey) == guestKey && TextsShare.Shareable(guestKey);
        }
        Check(keys, "Flare comments Going Public: the host's key for a comment's words goes to the guests as the key the guest's copy reads (fc:37:... as fn:37:...)");
        bool cityOnly = true;
        for (int k = 0; k < 300; k++)
        {
            FlarePost city = CommentPost(500 + k, 200, 9000, FlareAuthor.Network, FlareSource.Opened, 0);
            var under = new List<FlareComment>();
            FlareCommentRules.Plan(city, 4242, FlareLiveRules.Citizens, people, FlareTaste.Night, influencers, under);
            for (int i = 0; i < under.Count; i++)
            {
                cityOnly &= under[i].Kind == 'c';
            }
        }
        Check(cityOnly, "Flare comments Going Public: under a shared post only the city's people comment, never one machine's own friends or influencers");
        var store = new TextsStore();
        store.Keep(FlareCommentRules.Key(p, onHost[0]), 4, "citizen.thread.night_out", "Queue was round the block but worth it.", true);
        var guest = new TextsStore();
        var refused = new List<string>();
        int kept = TextsShare.Apply(TextsShare.Whole(store), guest, 4, refused, true);
        Check(kept == 1 && guest.Words(FlareCommentRules.Key(copy, onGuest[0])) == "Queue was round the block but worth it.",
            "Flare comments Going Public: the host's words for a comment reach a guest through the part RLO Texts already sends, under the guest's key (" + kept + " kept; refused: " + string.Join(", ", refused) + ")");
        Check(FlareCommentRules.Key(copy, onGuest[0]).StartsWith("fn:37:", StringComparison.Ordinal) && !FlareCommentRules.Key(p, onHost[0]).StartsWith("fn:", StringComparison.Ordinal),
            "Flare comments Going Public: a guest reads a shared post's comments by the session's number, the host by its own");
    }

    // ---------------------------------------------------------------- who comments

    private static void CommentsWho()
    {
        var people = new List<FlarePerson>
        {
            new FlarePerson { Index = 2, First = "Ana", Stage = 4, Taste = FlareTaste.Food },
            new FlarePerson { Index = 5, First = "Ben", Stage = 0, Taste = FlareTaste.Food },        // (a stranger: does not follow)
            new FlarePerson { Index = 7, First = "Cy", Stage = 3, Taste = FlareTaste.Sport },        // (cares about something else)
            new FlarePerson { Index = 9, First = "Di", Stage = 3, Taste = FlareTaste.Food }          // (the author of the posts below)
        };
        var influencers = new List<FlareInfluencer> { new FlareInfluencer { Id = 1, Handle = "@a" }, new FlareInfluencer { Id = 4, Handle = "@b" } };
        var plan = new List<FlareComment>();
        int friends = 0, infs = 0, wrongFriend = 0, dupes = 0, self = 0;
        for (int k = 0; k < 600; k++)
        {
            var p = CommentPost(900 + k, 40, 4000, FlareAuthor.Friend, FlareSource.Life, (int)FlareRules.LifeTopic.Food * 10);
            p.Who = 9;
            p.Seed = FlareRules.Roll(9, k, 3) * 1000 + k;
            FlareCommentRules.Plan(p, 77, FlareLiveRules.Citizens, people, FlareTaste.Food, influencers, plan);
            for (int i = 0; i < plan.Count; i++)
            {
                FlareComment c = plan[i];
                friends += c.Kind == 'f' ? 1 : 0;
                infs += c.Kind == 'i' ? 1 : 0;
                wrongFriend += c.Kind == 'f' && c.Who != 2 ? 1 : 0;
                self += c.Kind == 'f' && c.Who == 9 ? 1 : 0;
                for (int j = 0; j < i; j++)
                {
                    dupes += plan[j].Kind == c.Kind && plan[j].Who == c.Who ? 1 : 0;
                }
            }
        }
        Check(friends > 0 && infs > 0 && wrongFriend == 0 && self == 0,
            "Flare comments: under a friend's post a friend comments only if they follow and care about its topic, never the author; now and then an influencer ("
            + friends + " friends, " + infs + " influencers)");
        Check(dupes == 0, "Flare comments: nobody comments twice under one post");
        var citizen = CommentPost(77, 400, 9000);
        FlareCommentRules.Plan(citizen, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
        bool notAuthor = true;
        for (int i = 0; i < plan.Count; i++)
        {
            notAuthor &= !(plan[i].Kind == 'c' && plan[i].Who == citizen.Who);
        }
        Check(notAuthor && plan.Count == FlareCommentRules.WrittenMost, "Flare comments: one of the city's people never comments under their own post");
    }

    // ---------------------------------------------------------------- the intent of each kind of post

    private static void CommentsMap()
    {
        string Of(FlareAuthor author, FlareSource source, int arg, int niche = -1) => FlareTextsMap.Comment(new FlarePost { Author = author, Source = source, Arg = arg }, niche);
        Check(Of(FlareAuthor.Citizen, FlareSource.CityTalk, (int)RealLifeOverhaul.Flare.FlareTalk.Weather * 10 + 2) == "citizen.thread.weather"
              && Of(FlareAuthor.Citizen, FlareSource.CityTalk, (int)RealLifeOverhaul.Flare.FlareTalk.Night * 10) == "citizen.thread.night_out"
              && Of(FlareAuthor.Citizen, FlareSource.CityTalk, (int)RealLifeOverhaul.Flare.FlareTalk.EventTomorrow * 10) == "citizen.thread.events"
              && Of(FlareAuthor.Citizen, FlareSource.CityTalk, (int)RealLifeOverhaul.Flare.FlareTalk.Accident * 10) == null,
            "Flare comments map: a resident's post on a topic draws residents answering on that topic (citizen.thread.*); an accident, which the training lacks, none");
        Check(Of(FlareAuthor.Official, FlareSource.Official, (int)FlareOfficialPost.Patrols * 10) == "citizen.react.patrols"
              && Of(FlareAuthor.Official, FlareSource.Official, (int)FlareOfficialPost.Rate * 10 + 1) == "citizen.react.rate_up"
              && Of(FlareAuthor.Official, FlareSource.Official, (int)FlareOfficialPost.Rate * 10) == "citizen.react.rate_down"
              && Of(FlareAuthor.Official, FlareSource.Official, (int)FlareOfficialPost.Notice * 10) == null,
            "Flare comments map: an account's news draws residents reacting to it (citizen.react.*)");
        Check(Of(FlareAuthor.Tycoon, FlareSource.BusinessPost, FlareLiveRules.TycoonMoveArg) == "citizen.react.tycoon_buys"
              && Of(FlareAuthor.Tycoon, FlareSource.BusinessPost, 3) == null && Of(FlareAuthor.Business, FlareSource.BusinessPost, 0) == "citizen.thread.prices"
              && Of(FlareAuthor.Business, FlareSource.BusinessPost, 4) == null, "Flare comments map: a rival's move and a shop's offer map by their kind");
        Check(Of(FlareAuthor.Network, FlareSource.Opened, 0) == "citizen.reply.opened" && Of(FlareAuthor.Network, FlareSource.Arrested, 0) == null
              && Of(FlareAuthor.Influencer, FlareSource.Niche, 0, (int)FlareNiche.Fitness) == "citizen.thread.sport"
              && Of(FlareAuthor.Influencer, FlareSource.Niche, 0, (int)FlareNiche.Fashion) == null
              && Of(FlareAuthor.Friend, FlareSource.Life, (int)FlareRules.LifeTopic.Night * 10 + 1) == "citizen.thread.night_out",
            "Flare comments map: another player's post draws strangers' replies (citizen.reply.*); an influencer's by their niche; a friend's by its topic");
        var all = new List<string>();
        FlareTextsMap.CommentIntents(all);
        FlareTextsMap.CommentIntents(all, TextIntents.DataV3);
        var unknown = new List<string>();
        foreach (string key in all)
        {
            TextIntent t = TextIntents.Find(key);
            if (t == null || t.Voice != "citizen")
            {
                unknown.Add(key);
            }
        }
        Check(unknown.Count == 0 && all.Count >= 25, "Flare comments map: every intent a comment is asked with is one of the citizens' the mod builds (" + all.Count + "; unknown: "
                                                     + string.Join(", ", unknown) + ")");
        Check(FlareTextsMap.Taste("citizen.thread.food") == FlareTaste.Food && FlareTextsMap.Taste("citizen.react.patrols") == FlareTaste.None,
            "Flare comments map: a friend comments on a topic they care about, never on the city's news");
    }

    // ---------------------------------------------------------------- the cards, byte for byte against the generator's

    private static void CommentsCards()
    {
        string path = System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", "TextsCards.jsonl");
        var wanted = new List<string>();
        FlareTextsMap.CommentIntents(wanted);
        FlareTextsMap.CommentIntents(wanted, TextIntents.DataV3);   // (data v3's comments: every kind of post, and the answers to an earlier comment)
        var covered = new HashSet<string>(StringComparer.Ordinal);
        int rows = 0, same = 0;
        var wrong = new List<string>();
        foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (line.Length == 0)
            {
                continue;
            }
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement r = doc.RootElement;
            string intent = r.GetProperty("intent").GetString();
            if (!wanted.Contains(intent))
            {
                continue;
            }
            rows++;
            var request = new TextRequest();
            JsonElement w = r.GetProperty("who");
            request.Who.First = Str(w, "First");
            request.Who.Handle = Str(w, "Handle");
            request.Who.Age = w.GetProperty("Age").GetInt32();
            request.Who.District = Str(w, "District");
            request.Who.Style = Str(w, "Style");
            request.Who.When = Str(w, "When");
            JsonElement slots = r.GetProperty("slots");
            JsonElement history = r.GetProperty("history");
            var post = new FlareCommentPost
            {
                First = Slot(slots, "other") ?? (history.GetArrayLength() > 0 ? history[0][0].GetString() : null),
                Handle = history.GetArrayLength() > 0 ? history[0][0].GetString() : null,
                Words = history.GetArrayLength() > 0 ? history[0][1].GetString() : null,
                District = Slot(slots, "district"), Home = Slot(slots, "home"), Tycoon = Slot(slots, "tycoon"), Company = Slot(slots, "company"), Reaction = Slot(slots, "reaction")
            };
            if (FlareTextsMap.AnswersComments(intent))
            {
                // data v3: a news kind has the post in news and facts and only an earlier comment in history; the rest the post first
                string kind = intent.Substring("citizen.comment.".Length);
                bool newsKind = kind != "words" && kind != "reply" && kind != "ledger_headline" && Array.IndexOf(TextIntents.ThreadKinds, kind) < 0;
                int earlierAt = newsKind ? 0 : 1;
                post.Shop = Slot(slots, "shop");
                post.Stars = Slot(slots, "stars");
                post.Target = Slot(slots, "target");
                post.Player = Slot(slots, "player");
                if (newsKind)
                {
                    post.Handle = post.Words = null;
                }
                if (history.GetArrayLength() > earlierAt)
                {
                    post.EarlierFirst = Slot(slots, "other2");
                    post.EarlierHandle = history[earlierAt][0].GetString();
                    post.EarlierWords = history[earlierAt][1].GetString();
                }
            }
            var sheet = new TextSheet();
            bool built = FlareTextsMap.CommentCard(request, intent, post) && request.Use == TextUse.Replies
                         && TextIntents.Build(TextIntents.Find(request.Intent), request.Who, request.Slots, request.History, sheet);
            string card = built ? TextCard.Card(sheet) : "(not built)";
            if (card == r.GetProperty("card").GetString())
            {
                same++;
                covered.Add(intent);
            }
            else if (wrong.Count < 4)
            {
                wrong.Add(intent + ":\n    mod:       " + card.Replace("\n", "\\n") + "\n    generator: " + r.GetProperty("card").GetString().Replace("\n", "\\n"));
            }
        }
        var uncovered = new List<string>();
        foreach (string key in wanted)
        {
            if (!covered.Contains(key))
            {
                uncovered.Add(key);
            }
        }
        Check(rows >= 50 && same == rows, "Flare comments: every comment's card is byte-identical to the training generator's for the same values (" + same + " of " + rows + ")"
                                         + (wrong.Count > 0 ? "\n  " + string.Join("\n  ", wrong) : ""));
        Check(uncovered.Count == 0, "Flare comments: every intent a comment can be asked with is checked against the generator's cards (without: " + string.Join(", ", uncovered) + ")");
        var none = new TextRequest();
        Check(!FlareTextsMap.CommentCard(none, "citizen.thread.food", new FlareCommentPost { First = "Ana", Handle = "@ana" })
              && !FlareTextsMap.CommentCard(none, "citizen.reply.opened", new FlareCommentPost { First = "Ana", Words = "Opened." })
              && !FlareTextsMap.CommentCard(none, null, new FlareCommentPost()),
            "Flare comments: a card missing the post's words or the reaction is not built (that comment is simply not written)");
    }

    private static string Slot(JsonElement slots, string name) => slots.TryGetProperty(name, out JsonElement v) ? v.GetString() : null;

    // ---------------------------------------------------------------- the words kept by their key

    private static void CommentsKept()
    {
        FlarePost p = CommentPost(12, 9, 600);
        var plan = new List<FlareComment>();
        FlareCommentRules.Plan(p, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
        string key = FlareCommentRules.Key(p, plan[1]);
        var store = new TextsStore();
        store.Keep(key, 4, "citizen.thread.weather", "Brought a coat for nothing again.", false);
        var back = new TextsStore();
        bool read = back.Read(store.Write());
        Check(read && back.Words(key) == "Brought a coat for nothing again." && key == "fc:12:1:c" + plan[1].Who,
            "Flare comments: a comment's words are kept by the post, the slot and who writes them, and are the same after a save and a load");
        FlareComment other = plan[1];
        other.Who = (other.Who + 1) % FlareLiveRules.Citizens;
        Check(FlareCommentRules.Key(p, other) != key, "Flare comments: words written for one person are never shown under another (who is part of the key)");
        Check(FlareCommentRules.DisplayId(12, 1) < -1000 - 1000 * 32 && FlareCommentRules.DisplayId(12, 1) != FlareCommentRules.DisplayId(12, 2)
              && FlareCommentRules.DisplayId(12, 1) != FlareCommentRules.DisplayId(13, 1), "Flare comments: each is shown under a number of its own, clear of the posts' and the friends' replies'");
    }

    // ---------------------------------------------------------------- the texts

    private static void CommentsTexts()
    {
        string folder = LocalesFolder();
        string[] codes = { "en", "pt", "zh-cn" };
        string[] keys = { "reallife_flare_comments_count_one", "reallife_flare_comments_count", "reallife_flare_comments_more_one", "reallife_flare_comments_more" };
        bool all = true, names = true, style = true;
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, codes[l] + ".json"));
            for (int k = 0; k < keys.Length; k++)
            {
                string v = words != null && words.TryGetValue(keys[k], out string s) ? s : null;
                all &= !string.IsNullOrEmpty(v);
                names &= v == null || (v.Contains("{count}") == !keys[k].EndsWith("_one", StringComparison.Ordinal));
                style &= v == null || (v.IndexOf('!') < 0 && v.IndexOf('—') < 0 && v.IndexOf('–') < 0 && v.IndexOf(" - ", StringComparison.Ordinal) < 0);
            }
        }
        Check(all, "Flare comments: the \"more\" line and the count line are in en, pt and zh-cn");
        Check(names && style, "Flare comments: the count lines name {count} (the one-person lines do not), with no dashes or exclamation marks");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Check(AnyChangesRow(en, "comments under them, written by RLO Texts"), "Flare comments: What's new says so");
        Check(en != null && en["reallife_guide_flare"].Contains("comments come in over the hours") && en["reallife_guide_texts"].Contains("written only by the model"),
            "Flare comments: the Guide's Flare and RLO Texts pages say how comments come");
        bool none = true;
        foreach (string key in en.Keys)
        {
            none &= !key.StartsWith("reallife_flare_comment_line", StringComparison.Ordinal) && !key.StartsWith("reallife_flare_comment_text", StringComparison.Ordinal);
        }
        Check(none, "Flare comments: no written comment lines (only RLO Texts writes them)");
    }

    // ---------------------------------------------------------------- nothing per frame (the source)

    private static void CommentsSources()
    {
        string rules = TextsSource("Social/Flare/FlareCommentRules.cs"), director = TextsSource("Social/Flare/FlareDirector.Comments.cs"),
            screens = TextsSource("Social/Flare/FlareApp.Screens.cs"), texts = TextsSource("Social/Flare/FlareDirector.Texts.cs");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("System.Linq") && !director.Contains("System.Linq"),
            "Flare comments sources: the rules are game-free and nothing uses LINQ");
        Check(!director.Contains("void Update") && !director.Contains("LateUpdate")
              && CountOf(screens, "_director.CommentsAhead(") == 2 && CountOf(texts, "CommentsAhead(null)") == 1 && CountOf(screens, "CommentsOf(") == 1,
            "Flare comments sources: nothing per frame: a thread's comments are made only when it is drawn; their words are asked on the feed, a thread opened and Flare's own events");
        Check(director.Contains("Priority = urgent ? TextPriority.Soon : TextPriority.Background") && director.Contains("CommentsWaitingMost") && director.Contains("if (now == _commentsLooked && !_commentsFreed)"),
            "Flare comments sources: their words are asked at the lowest priority (the open thread's ahead of it), a few waiting at once, the walk once a game minute at most or once a request came back");
        Check(!director.Contains("FlareText.T(") && !director.Contains("FlareText.Fill(") && director.Contains("FlareCommentRules.Key(p, c)"),
            "Flare comments sources: a comment's words are only the model's, read by its key (no written line stands in)");
        Check(director.Contains("p.Shared") && director.Contains("TextWants(TextUse.Replies, shared)"),
            "Flare comments sources: a guest never asks for a shared post's comments; the host asks as the shared feed's writer");
    }

    private static int CountOf(string text, string what)
    {
        int n = 0;
        for (int i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + what.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- the cost

    private static void CommentsCost()
    {
        var plan = new List<FlareComment>(FlareCommentRules.WrittenMost);
        FlarePost p = CommentPost(44, 900, 9000);
        CommentsPlanMany(p, plan, 10);
        long bytes = CommentsPlanBytes(p, plan, 2000);
        double ms = CommentsPlanTime(p, plan, 2000);
        Check(bytes < 1024, "Flare comments: drawing a thread's plan allocates nothing (" + bytes + " bytes for 2,000 plans)");
        Check(ms < 200.0, "Flare comments: 2,000 plans of sixteen comments take " + ms.ToString("0.0") + " ms (well under a frame for a thread drawn once)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void CommentsPlanMany(FlarePost p, List<FlareComment> plan, int n)
    {
        for (int i = 0; i < n; i++)
        {
            FlareCommentRules.Plan(p, 77 + i, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long CommentsPlanBytes(FlarePost p, List<FlareComment> plan, int n)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        CommentsPlanMany(p, plan, n);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double CommentsPlanTime(FlarePost p, List<FlareComment> plan, int n)
    {
        var watch = Stopwatch.StartNew();
        CommentsPlanMany(p, plan, n);
        return watch.Elapsed.TotalMilliseconds;
    }

    // Any What's new row has the words: the next row until a release numbers it into its own.
    internal static bool AnyChangesRow(Dictionary<string, string> words, string part)
    {
        if (words == null)
        {
            return false;
        }
        foreach (var r in words)
        {
            if (r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !r.Key.EndsWith("_title", StringComparison.Ordinal) && r.Value.Contains(part))
            {
                return true;
            }
        }
        return false;
    }
}
