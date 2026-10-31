using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Texts;

// Flare with a model present (1 Nov 2026: Flare is written only by a model when one is present, else by its written lines;
// Social/Flare/FlareWordsHold.cs, FlareDirector.Hold.cs). An item whose words are asked appears only with them: held back
// past the look ahead until they come, then up the minute they came. Its written line only when the model is gone or truly
// failed it: refused or failed again after one retry, or waiting past its kind's cap. Checked here: each step and reason,
// the retry, the caps, the bound that lets only everyday posts go, the order of asking, the hub counting only the model's
// words for a held line and the asker's own written lines, a session replayed with and without a model, a message's words
// following it to its new minute, the sources and the texts.
internal static partial class Program
{
    private static void FlareWordsHolds()
    {
        FlareHoldSteps();
        FlareHoldBound();
        FlareHoldOrder();
        FlareHoldHub();
        FlareHoldSession();
        FlareHoldStore();
        FlareHoldSources();
        FlareHoldTexts();
    }

    // ---------------------------------------------------------------- each step, its reason and the retry

    private static void FlareHoldSteps()
    {
        var h = new FlareHeld { Key = "fp:1", Kind = FlareHoldKind.Post, Intent = "citizen.post.events", Planned = 600, Since = 10.0 };
        bool steps = FlareWordsHold.Step(h, true, false, 10.0) == FlareHoldStep.Show
                     && FlareWordsHold.Step(h, false, false, 10.0) == FlareHoldStep.Written
                     && FlareWordsHold.Step(h, false, true, 10.0) == FlareHoldStep.Ask;
        h.Asking = true;
        steps &= FlareWordsHold.Step(h, false, true, 11.0) == FlareHoldStep.Wait && FlareWordsHold.Step(h, false, true, 10.0 + FlareWordsHold.PostCap) == FlareHoldStep.Written;
        Check(steps, "flare hold: its words came, it shows; no model, its written line; else it is asked, then waits, and past its cap its written line");
        Check(FlareWordsHold.Why(h, TextShownWhy.NoPack, 11.0) == TextShownWhy.NoPack && FlareWordsHold.Why(h, TextShownWhy.Off, 11.0) == TextShownWhy.Off
              && FlareWordsHold.Why(h, TextShownWhy.Model, 10.0 + FlareWordsHold.PostCap) == TextShownWhy.Slow,
            "flare hold: a written line is counted as the model's absence names it, or as slow past the cap");

        var r = new FlareHeld { Key = "dm:3", Kind = FlareHoldKind.Message, Since = 0.0 };
        FlareWordsHold.Asked(r, true, "fd:c:1:600", TextShownWhy.Model);
        FlareWordsHold.Ended(r, false, TextShownWhy.Slow);
        bool timeOnly = r.Failures == 0 && !r.Asking && FlareWordsHold.Step(r, false, true, 5.0) == FlareHoldStep.Ask;
        FlareWordsHold.Ended(r, false, TextShownWhy.Held);
        FlareWordsHold.Ended(r, false, TextShownWhy.Busy);
        FlareWordsHold.Ended(r, false, TextShownWhy.Loading);
        FlareWordsHold.Ended(r, false, TextShownWhy.Unwanted);
        timeOnly &= r.Failures == 0 && !r.Final;
        FlareWordsHold.Ended(r, false, TextShownWhy.Refused);
        bool retry = r.Failures == 1 && FlareWordsHold.Step(r, false, true, 5.0) == FlareHoldStep.Ask;
        FlareWordsHold.Ended(r, false, TextShownWhy.Failed);
        bool gives = r.Failures == 2 && FlareWordsHold.Step(r, false, true, 5.0) == FlareHoldStep.Written && FlareWordsHold.Why(r, TextShownWhy.Model, 5.0) == TextShownWhy.Failed;
        Check(timeOnly && retry && gives && FlareWordsHold.RetryMost == 1,
            "flare hold: a time running out, a full queue, the model loading or a request given way only ask again; refused or failed is retried once, then the written line with that reason");
        var none = new FlareHeld { Key = "fp:9", Since = 0.0 };
        FlareWordsHold.Asked(none, false, "fp:9", TextShownWhy.NoCard);
        bool final = none.Final && FlareWordsHold.Step(none, false, true, 1.0) == FlareHoldStep.Written && FlareWordsHold.Why(none, TextShownWhy.Model, 1.0) == TextShownWhy.NoCard;
        var lang = new FlareHeld { Key = "fp:8", Since = 0.0 };
        FlareWordsHold.Ended(lang, false, TextShownWhy.Language);
        final &= lang.Final;
        Check(final, "flare hold: no card, another language, switched off or no pack is final at once: its written line, counted so");
        var worded = new FlareHeld { Key = "fp:7", Since = 0.0, Asking = true };
        FlareWordsHold.Ended(worded, true, TextShownWhy.Model);
        Check(!worded.Asking && worded.Failures == 0 && FlareWordsHold.Step(worded, true, true, 1.0) == FlareHoldStep.Show, "flare hold: words that came show it");
        Check(FlareWordsHold.MessageCap < FlareWordsHold.ReplyCap && FlareWordsHold.ReplyCap < FlareWordsHold.PostCap && FlareWordsHold.PostCap == 900.0
              && FlareWordsHold.Cap(FlareHoldKind.Message) == 90.0 && FlareWordsHold.Cap(FlareHoldKind.Reply) == 180.0 && FlareWordsHold.Cap(FlareHoldKind.Post) == 900.0,
            "flare hold: the caps are real seconds by how near the player is: an answer in a thread 90, under their post 180, the city's feed 900 as a comment's");
        Check(FlareWordsHold.ShownAt(h, 500) == 600 && FlareWordsHold.ShownAt(h, 720) == 720
              && FlareWordsHold.PushedAt(600, 590) == 590 + FlareWordsHold.AheadMinutes + 1 && FlareWordsHold.PushedAt(5000, 590) == 5000,
            "flare hold: an item shows at its own time if its words came first, else the minute they came; while held it is pushed past the look ahead, never brought nearer");
    }

    // ---------------------------------------------------------------- the bound lets only everyday posts go

    private static void FlareHoldBound()
    {
        var hold = new FlareWordsHold();
        var dropped = new List<FlareHeld>();
        for (int i = 0; i < FlareWordsHold.Most; i++)
        {
            hold.Hold("fp:" + i, FlareHoldKind.Post, "citizen.post.everyday", 1000 + i, 0.0, true, null, dropped);
        }
        FlareHeld newer = hold.Hold("fp:new", FlareHoldKind.Post, "citizen.post.everyday", 5000, 0.0, true, null, dropped);
        bool oldestGone = newer != null && dropped.Count == 1 && dropped[0].Key == "fp:0" && hold.Count == FlareWordsHold.Most && !hold.Holds("fp:0") && hold.Dropped == 1;
        dropped.Clear();
        FlareHeld message = hold.Hold("dm:1", FlareHoldKind.Message, "citizen.dm.answer", 900, 0.0, false, null, dropped);
        bool messageIn = message != null && dropped.Count == 1 && dropped[0].Key == "fp:1";
        Check(oldestGone && messageIn, "flare hold: past " + FlareWordsHold.Most + " held, the oldest everyday post is let go for a newer item, never shown");
        var full = new FlareWordsHold();
        dropped.Clear();
        for (int i = 0; i < FlareWordsHold.Most; i++)
        {
            full.Hold("dm:" + i, FlareHoldKind.Message, "citizen.dm.answer", 100 + i, 0.0, false, null, dropped);
        }
        FlareHeld more = full.Hold("dm:more", FlareHoldKind.Message, "citizen.dm.answer", 300, 0.0, false, null, dropped);
        FlareHeld chatter = full.Hold("fp:talk", FlareHoldKind.Post, "citizen.post.everyday", 300, 0.0, true, null, dropped);
        Check(more != null && chatter == null && dropped.Count == 0 && full.Count == FlareWordsHold.Most + 1,
            "flare hold: an answer to the player is never dropped for the bound; an everyday post that finds no room is the one let go");
        FlareHeld again = full.Hold("dm:3", FlareHoldKind.Message, "x", 1, 0.0, false, null, dropped);
        Check(again != null && again.Planned == 103 && full.Of("dm:3") == again && full.Of("none") == null, "flare hold: an item held already is the same item");
        full.Let(again, true);
        full.Let(more, false);
        Check(full.Worded == 1 && full.Written == 1 && !full.Holds("dm:3"), "flare hold: an item let go is counted as worded or written");
    }

    // ---------------------------------------------------------------- the order of asking

    private static void FlareHoldOrder()
    {
        var hold = new FlareWordsHold();
        hold.Hold("fp:late", FlareHoldKind.Post, "a", 900, 0.0, true, null, null);
        hold.Hold("fp:early", FlareHoldKind.Post, "a", 600, 0.0, false, null, null);
        hold.Hold("fr:1:2", FlareHoldKind.Reply, "b", 950, 0.0, false, null, null);
        hold.Hold("dm:5", FlareHoldKind.Message, "c", 990, 0.0, false, null, null);
        FlareHeld asking = hold.Hold("dm:6", FlareHoldKind.Message, "c", 100, 0.0, false, null, null);
        asking.Asking = true;
        var next = new List<FlareHeld>();
        hold.Next(next);
        Check(next.Count == 4 && next[0].Key == "dm:5" && next[1].Key == "fr:1:2" && next[2].Key == "fp:early" && next[3].Key == "fp:late",
            "flare hold: asked messages first, then answers under the player's posts, then the feed by its time; one asking already not asked again (" + string.Join(", ", Keys(next)) + ")");
        var many = new FlareWordsHold();
        for (int i = 0; i < 40; i++)
        {
            FlareHeld h = many.Hold("fp:" + i, FlareHoldKind.Post, "a", i, 0.0, false, null, null);
            h.Asking = i < 5;
        }
        many.Next(next);
        Check(next.Count == FlareWordsHold.AskingMost - 5 && next[0].Key == "fp:5", "flare hold: at most " + FlareWordsHold.AskingMost + " held items asking at once, a quarter of the queue (" + next.Count + " more)");
        var spent = new FlareWordsHold();
        FlareHeld failed = spent.Hold("fp:x", FlareHoldKind.Post, "a", 1, 0.0, false, null, null);
        failed.Failures = 2;
        FlareHeld nocard = spent.Hold("fp:y", FlareHoldKind.Post, "a", 1, 0.0, false, null, null);
        nocard.Final = true;
        spent.Next(next);
        Check(next.Count == 0, "flare hold: an item past its retry or with no card is never asked again");
    }

    private static List<string> Keys(List<FlareHeld> list)
    {
        var keys = new List<string>(list.Count);
        foreach (FlareHeld h in list)
        {
            keys.Add(h.Key);
        }
        return keys;
    }

    // ---------------------------------------------------------------- the hub counts only the model's words for a held line

    private static void FlareHoldHub()
    {
        var hub = new TextsHub(new TextQueue()) { Runner = new PretendRunner(), Pack = "RLO Texts" };
        TextRequest held = TextsAskOf("fp:1", "friend.crash", null, w => { }, TextPriority.Background, 5);
        held.HoldsLine = true;
        hub.Ask(held, 0);
        hub.Look(10);                                                 // its time ran out in the queue: nothing shown yet
        var why = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
        var rule = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        int lines = hub.Tally.Totals("RLO Texts", why, rule);
        bool quiet = lines == 0 && held.Shown == TextShownWhy.Slow;
        TextRequest none = TextsAskOf("fp:2", "street.day", null, w => { });   // no card for the hub's data 2
        none.HoldsLine = true;
        quiet &= !hub.Ask(none, 11) && none.Shown == TextShownWhy.NoCard && hub.Tally.Totals("RLO Texts", why, rule) == 0;
        hub.CountWritten("friend.crash", TextShownWhy.Refused);
        lines = hub.Tally.Totals("RLO Texts", why, rule);
        bool counted = lines == 1 && why[(int)TextShownWhy.Refused] == 1;
        TextRequest worded = TextsAskOf("fp:3", "friend.crash", null, w => { }, TextPriority.Soon, 30);
        worded.HoldsLine = true;
        worded.Speaker = "c:9";
        hub.Ask(worded, 20);
        TextJob job = hub.Queue.Take(20.1, true);
        hub.Queue.Done(job, "Heard about 5th Ave. You alright?", 1.0, null);
        hub.Look(21);
        lines = hub.Tally.Totals("RLO Texts", why, rule);
        Check(quiet && counted && lines == 2 && why[0] == 1 && worded.Shown == TextShownWhy.Model,
            "flare hold, the hub: a held line's miss or refusal to ask is counted nowhere (its asker asks again), the asker's written line is counted with its reason, the model's words as ever");
        Check(hub.WhyNotWanted(TextUse.Posts, false) == TextShownWhy.Model && new TextsHub(new TextQueue()).WhyNotWanted(TextUse.Posts, false) == TextShownWhy.NoPack,
            "flare hold, the hub: whether a model is there for a kind of message, and if not why");
    }

    // ---------------------------------------------------------------- a session replayed, with a model and without

    private static void FlareHoldSession()
    {
        // With a model: an item comes due each second for ten minutes, a third of them under the player's posts or answers
        // to messages; the pretend worker answers each in time, but every tenth item's first two answers are refused (an
        // exclamation mark) and every seventh's first answer is. Nothing may show a written line but those refused twice.
        var q = new TextQueue();
        q.Expected(0.05);
        var hub = new TextsHub(q) { Runner = new PretendRunner(), Pack = "RLO Texts" };
        hub.Budget.Apply(q);
        var hold = new FlareWordsHold();
        var kept = new Dictionary<string, string>();
        var refusals = new Dictionary<string, int>();
        var askedTimes = new Dictionary<string, int>();
        var shownWritten = new List<string>();
        int shownWorded = 0, n = 0;
        var next = new List<FlareHeld>();
        for (double t = 0; t <= 700; t += 1.0)
        {
            if (t < 600)
            {
                n++;
                FlareHoldKind kind = n % 3 == 0 ? FlareHoldKind.Message : n % 3 == 1 ? FlareHoldKind.Post : FlareHoldKind.Reply;
                FlareHeld h = hold.Hold("k:" + n, kind, "friend.crash", (long)t, t, kind == FlareHoldKind.Post, null, null);
                h.Use = TextUse.People;
                refusals["k:" + n] = n % 10 == 0 ? 2 : n % 7 == 0 ? 1 : 0;
            }
            IReadOnlyList<FlareHeld> items = hold.Items;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                FlareHeld h = items[i];
                FlareHoldStep step = FlareWordsHold.Step(h, kept.ContainsKey(h.Key), hub.WhyNotWanted(h.Use, false) == TextShownWhy.Model, t);
                if (step == FlareHoldStep.Show)
                {
                    shownWorded++;
                    hold.Let(h, true);
                }
                else if (step == FlareHoldStep.Written)
                {
                    hub.CountWritten(h.Intent, FlareWordsHold.Why(h, hub.WhyNotWanted(h.Use, false), t));
                    shownWritten.Add(h.Key);
                    hold.Let(h, false);
                }
            }
            hold.Next(next);
            foreach (FlareHeld h in next)
            {
                string key = h.Key;
                FlareHeld held = h;
                TextRequest r = TextsAskOf(key, "friend.crash", null, null, h.Kind == FlareHoldKind.Post ? TextPriority.Background : TextPriority.Soon, 60);
                r.Speaker = "s:" + key + ":" + (askedTimes.TryGetValue(key, out int a) ? a : 0);
                askedTimes[key] = a + 1;
                r.HoldsLine = true;
                r.Done = words =>
                {
                    if (words != null)
                    {
                        kept[key] = words;
                    }
                    FlareWordsHold.Ended(held, words != null, r.Shown);
                };
                FlareWordsHold.Asked(h, hub.Ask(r, t), key, r.Shown);
            }
            TextJob job;
            while ((job = q.Take(t + 0.5, true)) != null)
            {
                int left = refusals.TryGetValue(job.Key, out int x) ? x : 0;
                refusals[job.Key] = Math.Max(0, left - 1);
                q.Done(job, left > 0 ? "Heard about 5th Ave! Wow!" : "Heard about 5th Ave. You alright?", 0.05, null);
            }
            hub.Look(t + 0.6);
        }
        var why = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
        var rule = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        int lines = hub.Tally.Totals("RLO Texts", why, rule);
        int twice = 600 / 10;
        bool onlyFailures = shownWritten.Count == twice && why[(int)TextShownWhy.Refused] == twice && lines - why[0] == twice;
        foreach (string key in shownWritten)
        {
            onlyFailures &= int.Parse(key.Substring(2)) % 10 == 0;
        }
        Check(onlyFailures && shownWorded == 600 - twice && why[0] == shownWorded && hold.Count == 0,
            "flare hold, a session with a model: " + shownWorded + " items shown with the model's words, and a written line only for the " + shownWritten.Count
            + " the checks refused again after a retry, counted as refused (" + why[(int)TextShownWhy.Refused] + "; written in all " + (lines - why[0]) + ")");

        // Without a model: nothing is held; each item shows its written line at once, counted as no pack, as before.
        var bare = new TextsHub(new TextQueue()) { Pack = TextsHub.NoPack };
        var nohold = new FlareWordsHold();
        int written = 0;
        for (int i = 0; i < 20; i++)
        {
            FlareHeld h = nohold.Hold("k:" + i, FlareHoldKind.Post, "friend.crash", i, i, false, null, null);
            h.Use = TextUse.Posts;
            TextShownWhy away = bare.WhyNotWanted(h.Use, false);
            if (FlareWordsHold.Step(h, false, away == TextShownWhy.Model, i) == FlareHoldStep.Written)
            {
                bare.CountWritten(h.Intent, FlareWordsHold.Why(h, away, i));
                nohold.Let(h, false);
                written++;
            }
        }
        bare.Tally.Totals(TextsHub.NoPack, why, rule);
        Check(written == 20 && why[(int)TextShownWhy.NoPack] == 20 && nohold.Count == 0,
            "flare hold, a session without a model: every item shows its written line at once, counted as no pack");
    }

    // ---------------------------------------------------------------- a message's words follow it to its new minute

    private static void FlareHoldStore()
    {
        var store = new TextsStore();
        store.Keep("fd:c:4:600", 1, "citizen.dm.answer", "Sounds good.", false, false, "en");
        bool moved = store.Move("fd:c:4:600", "fd:c:4:645") && store.Words("fd:c:4:600") == null && store.Words("fd:c:4:645", "en") == "Sounds good."
                     && store.Get("fd:c:4:645").Intent == "citizen.dm.answer" && store.Get("fd:c:4:645").Day == 1 && store.Count == 1;
        Check(moved && !store.Move("none", "x") && store.Move("fd:c:4:645", "fd:c:4:645") && store.Count == 1,
            "flare hold: a held message's words move to the key of the minute it shows, as they were kept");
    }

    // ---------------------------------------------------------------- the sources: wired, the shared feed untouched, nothing per frame

    private static void FlareHoldSources()
    {
        string director = ModSource(@"Social\Flare\FlareDirector.cs");
        string hold = ModSource(@"Social\Flare\FlareDirector.Hold.cs");
        string texts = ModSource(@"Social\Flare\FlareDirector.Texts.cs");
        string screens = ModSource(@"Social\Flare\FlareApp.Screens.cs");
        string comments = ModSource(@"Social\Flare\FlareDirector.Comments.cs");
        string answers = ModSource(@"Social\Flare\FlareDirector.Answers.cs");
        string links = ModSource(@"Wiring\TextsLinks.cs");
        Check(director.Contains("WordsHoldSecond();") && director.Contains("HoldForget();") && !hold.Contains("void Update") && !hold.Contains("LateUpdate"),
            "flare hold sources: the hold runs on Flare's one-second timer and is forgotten with the account; nothing per frame");
        Check(hold.Contains("FlareNetShare.IsShared(_file, p, hostShares)") && hold.Contains("p.Shared") && texts.Contains("r.HoldsLine = !shared;"),
            "flare hold sources: the session's shared feed is never held, and its lines are counted by the hub as before");
        Check(texts.Contains("_wordsHold.Holds(PostKey(p))") && texts.Contains("_wordsHold.Holds(DmHoldKey(answer))") && texts.Contains("r.Wanted = () => Now < answer.At;")
              && texts.Contains("HoldEnded(key, words != null, r.Shown);") && texts.Contains("HoldFriendReply(p, index, at, r);"),
            "flare hold sources: a held post or answer is the hold's to ask, a held answer's words stay wanted while its minute moves, and each request tells the hold how it ended");
        Check(screens.Contains("_director.FriendHeld(p.Id, index)") && comments.Contains("HoldsLine = true") && answers.Contains("r.HoldsLine = true;"),
            "flare hold sources: a friend's reply held for its words is left out of the thread; comments and answers, never shown without words, count no written line");
        Check(links.Contains("FlareBridges.TextMove = ") && links.Contains("FlareBridges.TextWritten = ") && links.Contains("FlareBridges.TextWhyNot = ")
              && links.Contains("FlareBridges.TextMove = null;") && links.Contains("FlareBridges.TextWritten = null;") && links.Contains("FlareBridges.TextWhyNot = null;"),
            "flare hold sources: the bridges are wired and let go");
        Check(hold.Contains("FlareBridges.TextWritten?.Invoke(h.Intent, why);") && hold.Contains("Flare check (held words)") && hold.Contains("FlareBridges.TextMove?.Invoke(h.AskKey, key);"),
            "flare hold sources: each written line after all is counted with its reason and told with Developer diagnostics; a message's words follow it");
    }

    // ---------------------------------------------------------------- the texts

    private static void FlareHoldTexts()
    {
        var guide = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "On Flare it is the other way round while a model is there" }, { "pt", "No Flare é o contrário enquanto houver um modelo" },
            { "zh-cn", "在 Flare 上，只要有模型就正好相反" }, { "de", "Auf Flare ist es umgekehrt, solange ein Modell da ist" },
            { "fr", "Sur Flare, c'est l'inverse tant qu'un modèle est là" }, { "es", "En Flare es al revés mientras haya un modelo" },
            { "ru", "Во Flare, пока есть модель, всё наоборот" }, { "nl", "Op Flare is het andersom zolang er een model is" },
            { "tr", "Flare'de bir model varken durum tersinedir" }, { "pl", "Na Flare jest odwrotnie, dopóki jest model" }, { "ko", "Flare에서는 모델이 있는 동안 반대입니다" }
        };
        var news = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "On Flare, while a model is there, posts, replies and answers to your messages now wait for its words" },
            { "pt", "No Flare, enquanto houver um modelo, posts, respostas e respostas às suas mensagens agora esperam" },
            { "zh-cn", "在 Flare 上，只要有模型，帖子、回复和对你消息的回答现在都会等待它的文字" },
            { "de", "Auf Flare warten Beiträge, Antworten und Antworten auf deine Nachrichten jetzt" },
            { "fr", "Sur Flare, tant qu'un modèle est là, les publications, les réponses et les réponses à vos messages attendent" },
            { "es", "En Flare, mientras haya un modelo, las publicaciones, las respuestas y las respuestas a tus mensajes" },
            { "ru", "Во Flare, пока есть модель, посты, ответы и ответы на ваши сообщения теперь ждут" },
            { "nl", "Op Flare wachten berichten, reacties en antwoorden op je berichten nu" },
            { "tr", "Flare'de bir model varken gönderiler, yanıtlar ve mesajlarınıza gelen cevaplar" },
            { "pl", "Na Flare, dopóki jest model, posty, odpowiedzi i odpowiedzi na twoje wiadomości" },
            { "ko", "이제 Flare에서는 모델이 있는 동안 게시물, 답글" }
        };
        bool pages = true, rows = true, clean = true;
        string lacking = string.Empty;
        foreach (KeyValuePair<string, string> pair in guide)
        {
            Dictionary<string, string> texts = LocaleFile(pair.Key);
            string page = texts.TryGetValue("reallife_guide_texts", out string g) ? g : string.Empty;
            bool has = page.Contains(pair.Value);
            bool row = WhatsNewHas(texts, news[pair.Key]);
            pages &= has;
            rows &= row;
            lacking += has && row ? string.Empty : pair.Key + " ";
            clean &= !page.Contains("!") && !page.Contains("—") && !page.Contains("–") && Occurs(page, pair.Value) == 1;
        }
        Check(guide.Count == 11 && pages && rows, "flare hold, texts: the Guide's RLO Texts page and What's new tell of Flare waiting for the model's words, in all eleven languages ("
                                                  + (lacking.Length > 0 ? "lacking " + lacking : "none lacking") + ")");
        Check(clean, "flare hold, texts: said once on the page, with no exclamation marks or dashes");
    }
}
