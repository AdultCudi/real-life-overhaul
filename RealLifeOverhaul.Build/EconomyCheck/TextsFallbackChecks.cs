using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Texts;

// RLO Texts fell back to the written lines for the rest of a session (a report of 1 Nov 2026; the log of 30 Oct behind it: the
// model 103, written 194, slow 180, and not one word of the model from day 27 09:00 to the end, four game days, on a graphics card
// writing a reply in 46 ms). The queue's budget was a fixed count a minute set for two processor threads (12, then 8, 5, 3): a
// burst of the city's words used the minute, the player's own lines waited in the queue until their hold ran out, those waits and
// the guesses nobody claimed counted as the model being too slow, and the budget stepped down to its last step (3 a minute, only
// what the player looks at) with no way back but twenty requests in a row of a kind now seldom asked. Checked here: the budget a
// share of this machine's time, a share of each minute kept for spoken lines, a miss the queue caused itself or a guess never
// shown never stepping the budget down, a calm minute stepping it back up, the session replayed at a graphics card's speed, the
// reason "held" apart from "slow", the hourly line's reasons by intent and its verdict, the log line on a step's change, and the
// texts.
internal static partial class Program
{
    private static void TextsFallback()
    {
        TextsFallbackScaled();
        TextsFallbackSpoken();
        TextsFallbackMisses();
        TextsFallbackCalm();
        TextsFallbackSession();
        TextsFallbackLine();
        TextsFallbackSources();
        TextsFallbackTexts();
        TextsFallbackTiming();
    }

    private const double GpuReply = 0.046;   // seconds a reply took on the log's graphics card ("the pack, street.brush phone, 46 ms")

    // ---------------------------------------------------------------- the budget as a share of this machine's time

    private static void TextsFallbackScaled()
    {
        Check(TextBudget.Scaled(TextQueue.PerMinute, TextQueue.StartGuess) == TextQueue.PerMinute && TextBudget.Scaled(TextQueue.BackgroundPerMinute, TextQueue.StartGuess) == TextQueue.BackgroundPerMinute,
            "texts fallback, the budget: at the 1.5 s two processor threads take, the table's 12 a minute and 6 of the background, as before");
        Check(TextBudget.Scaled(12, 3.0) == 12 && TextBudget.Scaled(3, 9.0) == 3 && TextBudget.Scaled(5, 2.0) == 5,
            "texts fallback, the budget: a slower machine never gets less than the table");
        Check(TextBudget.Scaled(12, GpuReply) == TextQueue.PerMinuteMost && TextBudget.Scaled(3, GpuReply) == 97 && TextBudget.Scaled(6, GpuReply) == 195,
            "texts fallback, the budget: a graphics card at 46 ms a reply may start " + TextQueue.PerMinuteMost + " a minute (at most), 195 of the background, and 97 at the last step ("
            + TextBudget.Scaled(12, GpuReply) + ", " + TextBudget.Scaled(6, GpuReply) + ", " + TextBudget.Scaled(3, GpuReply) + ")");
        Check(TextBudget.Scaled(0, GpuReply) == 0 && TextBudget.Scaled(12, double.NaN) == 12 && TextBudget.Scaled(12, 0.0) == 12 && TextBudget.Scaled(12, -1.0) == 12
              && TextBudget.Scaled(12, double.PositiveInfinity) == 12 && TextBudget.Scaled(-4, 1.0) == 0,
            "texts fallback, the budget: no background stays none, and a reply time that is unknown, zero, negative or endless keeps the table");
        var q = new TextQueue();
        q.Expected(GpuReply);
        var b = new TextBudget();
        b.Apply(q);
        bool first = q.BudgetPerMinute == TextQueue.PerMinuteMost && q.BackgroundBudget == 195 && q.Floor == TextPriority.Background;
        for (int i = 0; i < 30; i++)
        {
            b.Ended(true);
        }
        b.Apply(q);
        Check(first && b.Step == 3 && q.BudgetPerMinute == 97 && q.BackgroundBudget == 0 && q.Floor == TextPriority.Now,
            "texts fallback, the budget: applied to the queue at the speed it expects, the steps keep their floor and their share (" + q.BudgetPerMinute + " a minute at step " + b.Step + ")");
        q.SetBudget(100000, 100000);
        Check(q.BudgetPerMinute == TextQueue.PerMinuteMost && q.BackgroundBudget == TextQueue.PerMinuteMost, "texts fallback, the queue: never more than " + TextQueue.PerMinuteMost + " starts a minute");
        Check(TextBudget.Describe(0, NewBudgetQueue(TextQueue.StartGuess, 0)) == "step 0 of 3 (12 a minute, 6 of them the background, every kind)"
              && TextBudget.Describe(2, NewBudgetQueue(TextQueue.StartGuess, 2)) == "step 2 of 3 (5 a minute, no background, nothing less urgent than what is due soon)"
              && TextBudget.Describe(9, NewBudgetQueue(TextQueue.StartGuess, 3)) == "step 3 of 3 (3 a minute, no background, only what the player looks at)",
            "texts fallback, the budget: the log names the step, the starts a minute, the background's and the floor (" + TextBudget.Describe(2, NewBudgetQueue(TextQueue.StartGuess, 2)) + ")");
    }

    // A queue at a reply time, its budget as at a step.
    private static TextQueue NewBudgetQueue(double expect, int step)
    {
        var q = new TextQueue();
        q.Expected(expect);
        var b = new TextBudget();
        for (int s = 0; s < step; s++)
        {
            for (int i = 0; i < TextBudget.Least; i++)
            {
                b.Ended(true);
            }
        }
        b.Apply(q);
        return q;
    }

    // ---------------------------------------------------------------- a share of each minute for the spoken lines

    private static void TextsFallbackSpoken()
    {
        double now = 50.0;
        TextJob J(string key, TextPriority p, double seconds, bool guess = false) => new TextJob { Key = key, Priority = p, Deadline = now + seconds, Guess = guess, Prompt = "x" };
        Check(TextQueue.Kept(12) == 3 && TextQueue.Kept(TextService.PerMinute) == 5 && TextQueue.Kept(3) == 1 && TextQueue.Kept(2) == 1 && TextQueue.Kept(1) == 0 && TextQueue.Kept(240) == 60,
            "texts fallback, the queue: a quarter of each minute's starts is kept for spoken lines, at least one once there are two");
        var q = new TextQueue();
        var burst = new List<TextJob>();
        for (int i = 0; i < 30; i++)
        {
            TextJob j = J("flare" + i, TextPriority.Soon, 60);
            burst.Add(j);
            q.Add(j, now);
        }
        int started = 0;
        while (q.Take(now, true) != null)
        {
            started++;
        }
        TextJob guess = J("guess", TextPriority.Now, 12, true);
        q.Add(guess, now);
        bool guessHeld = q.Take(now, true) == null && guess.Held && !guess.Ran;
        TextJob spoken = J("spoken", TextPriority.Now, 5);   // (the queue's first guess of a reply is 1.5 s: a 1.1 s hold is refused before any speed is measured)
        q.Add(spoken, now);
        TextJob taken = q.Take(now, true);
        int heldCount = 0;
        for (int i = 0; i < burst.Count; i++)
        {
            heldCount += burst[i].Held && !burst[i].Ran ? 1 : 0;
        }
        bool heldMarked = heldCount == burst.Count - started;
        Check(started == 9 && guessHeld && taken == spoken && spoken.Ran && !spoken.Held && heldMarked,
            "texts fallback, the queue: a burst of the city's words starts 9 of the minute's 12, a guess waits with it, and the line the player waits for face to face is taken at once (started "
            + started + ", the spoken line " + (taken == spoken ? "taken" : "waiting") + ")");
        var full = new TextQueue();
        for (int i = 0; i < 3; i++)
        {
            full.Add(J("talk" + i, TextPriority.Now, 5), now);
        }
        full.SetBudget(3, 0);
        int spokenStarts = 0;
        while (full.Take(now, true) != null)
        {
            spokenStarts++;
        }
        Check(spokenStarts == 3, "texts fallback, the queue: spoken lines may use the whole minute, the kept share as well (" + spokenStarts + " of 3)");
        var gpu = new TextQueue();
        gpu.Expected(GpuReply);
        new TextBudget().Apply(gpu);
        for (int i = 0; i < TextQueue.Most; i++)
        {
            gpu.Add(J("city" + i, i % 2 == 0 ? TextPriority.Soon : TextPriority.Background, 120), now);
        }
        int gpuStarts = 0;
        while (gpu.Take(now, true) != null)
        {
            gpuStarts++;
        }
        Check(gpuStarts == TextQueue.Most, "texts fallback, the queue: at a graphics card's speed a full queue of the city's words is started within the minute (" + gpuStarts + " of " + TextQueue.Most + ")");
    }

    // ---------------------------------------------------------------- what counts as the model being too slow

    private static void TextsFallbackMisses()
    {
        var hub = new TextsHub(new TextQueue()) { Runner = new PretendRunner() };
        var guess = new TextJob { Priority = TextPriority.Now, Guess = true };
        bool counts = !hub.BudgetMiss(guess);
        guess.Ran = true;
        counts &= hub.BudgetMiss(guess);
        var held = new TextJob { Priority = TextPriority.Soon, Held = true };
        counts &= !hub.BudgetMiss(held);
        held.Ran = true;
        counts &= hub.BudgetMiss(held);
        var spoken = new TextJob { Priority = TextPriority.Now };
        counts &= hub.BudgetMiss(spoken);
        Check(counts, "texts fallback, the budget: a guess nobody waited for and a request the queue held back never count as the model too slow; once the model ran either, a miss counts; a spoken line that waited behind other work counts");
        Check(TextsHub.Late(new TextJob { Held = true }) == TextShownWhy.Held && TextsHub.Late(new TextJob { Held = true, Ran = true }) == TextShownWhy.Slow
              && TextsHub.Late(new TextJob()) == TextShownWhy.Slow,
            "texts fallback, the reasons: a request whose time ran out is held when the budget held it and the model never ran it, else slow");

        // A guess that runs out unclaimed was never shown: no written line counted, no step down.
        var q = new TextQueue();
        var g = new TextsHub(q) { Runner = new PretendRunner(), Pack = "RLO Texts" };
        for (int i = 0; i < 30; i++)
        {
            TextRequest r = TextsAskOf("stg:" + i, "friend.crash", null, w => { }, TextPriority.Now, 12, true);
            r.Speaker = "st:" + i;
            g.Ask(r, i);
        }
        for (int t = 0; t <= 60; t++)
        {
            g.Look(t);
        }
        var why = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
        var rule = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        int lines = g.Tally.Totals("RLO Texts", why, rule);
        Check(lines == 0 && g.Fallbacks == 30 && g.Budget.Step == 0 && g.Pending == 0,
            "texts fallback, guesses: thirty guesses at faces the player never stopped, run out in the queue, are no written line shown and no step down (counted " + lines + ", step " + g.Budget.Step + ")");

        // A request held by the minute's starts and run out: counted as held, the budget untouched.
        var hq = new TextQueue();
        var h = new TextsHub(hq) { Runner = new PretendRunner(), Pack = "RLO Texts" };
        for (int i = 0; i < 9; i++)
        {
            TextRequest r = TextsAskOf("ph:" + i, "friend.crash", null, w => { }, TextPriority.Soon, 30);
            r.Speaker = "p:" + i;
            h.Ask(r, 0);
            TextJob job = hq.Take(0.1, true);
            hq.Done(job, "Heard about 5th Ave. You alright?", TextQueue.StartGuess, null);
        }
        h.Look(0.2);
        TextRequest late = TextsAskOf("ph:late", "friend.crash", null, w => { }, TextPriority.Soon, 4);
        late.Speaker = "p:late";
        h.Ask(late, 1);
        bool waits = hq.Take(1.2, true) == null;
        h.Look(6);
        h.Tally.Totals("RLO Texts", why, rule);
        Check(waits && why[(int)TextShownWhy.Held] == 1 && why[(int)TextShownWhy.Slow] == 0 && h.Budget.Step == 0 && h.Budget.MissPercent == 0,
            "texts fallback, the reasons: a message held back by the minute's starts and run out is counted as held, not slow, and never as a miss of the budget (held "
            + why[(int)TextShownWhy.Held] + ", slow " + why[(int)TextShownWhy.Slow] + ", missed " + h.Budget.MissPercent + "%)");
    }

    // ---------------------------------------------------------------- a calm minute steps the budget back up

    private static void TextsFallbackCalm()
    {
        var b = new TextBudget();
        for (int i = 0; i < 40; i++)
        {
            b.Ended(true);
        }
        bool bottom = b.Step == 3 && b.Downs == 3;
        bool first = !b.Calm(0.0);                       // the calm is counted from the first look
        bool early = !b.Calm(TextBudget.CalmSeconds - 1);
        bool up = b.Calm(TextBudget.CalmSeconds) && b.Step == 2;
        b.Ended(true);                                   // a miss: the calm starts again
        bool reset = !b.Calm(TextBudget.CalmSeconds + 1) && !b.Calm(TextBudget.CalmSeconds * 2) && b.Step == 2;
        bool next = b.Calm(TextBudget.CalmSeconds * 2 + 1) && b.Step == 1 && b.Calm(TextBudget.CalmSeconds * 3 + 1) && b.Step == 0;
        bool stays = !b.Calm(TextBudget.CalmSeconds * 10) && b.Step == 0 && b.Ups == 3;
        Check(bottom && first && early && up && reset && next && stays,
            "texts fallback, the budget: at its last step, a minute with no miss brings it up a step, a miss starts the minute again, and it comes back to the first step and stays (step "
            + b.Step + ", " + b.Downs + " down, " + b.Ups + " up)");

        // The hub's own look does it, with nothing asked: the stuck session of the log comes back by itself.
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner() };
        for (int i = 0; i < 40; i++)
        {
            hub.Budget.Ended(true);
        }
        hub.Budget.Apply(q);
        bool stuck = q.Floor == TextPriority.Now && q.BackgroundBudget == 0;
        for (double t = 0; t <= TextBudget.CalmSeconds * 3 + 1; t += 0.5)
        {
            hub.Look(t);
        }
        Check(stuck && hub.Budget.Step == 0 && q.Floor == TextPriority.Background && q.BackgroundBudget == TextQueue.BackgroundPerMinute,
            "texts fallback, the budget: the hub's look brings a budget left at its last step back to the first within three calm minutes, the background with it (step " + hub.Budget.Step + ")");
    }

    // ---------------------------------------------------------------- the session replayed at a graphics card's speed

    private static void TextsFallbackSession()
    {
        // Ten real minutes as in the log: the city's words in bursts, a guess at each face the player passes, posts in the
        // background, and two conversations, each line a request the player waits 1.1 s for. The pretend worker answers
        // what the queue gives it in 46 ms, as the log's card did.
        var q = new TextQueue();
        q.Expected(GpuReply);
        var hub = new TextsHub(q) { Runner = new PretendRunner(), Pack = "RLO Texts Lite" };
        hub.Budget.Apply(q);
        int n = 0, spokenWritten = 0, spokenAsked = 0;
        var running = new List<(TextJob Job, double Due)>();
        for (int tick = 0; tick <= 6000; tick++)
        {
            double now = tick * 0.1;
            if (tick % 300 == 0)
            {
                for (int i = 0; i < 20; i++)
                {
                    FallbackAsk(hub, TextPriority.Soon, 60, false, now, ref n, null);   // a burst of replies and comments under a post
                }
            }
            if (tick % 50 == 0)
            {
                FallbackAsk(hub, TextPriority.Background, 120, false, now, ref n, null);   // a post going up later
            }
            if (tick % 20 == 0)
            {
                FallbackAsk(hub, TextPriority.Now, 12, true, now, ref n, null);   // a guess at a face
            }
            if ((tick >= 1200 && tick < 1500 || tick >= 4200 && tick < 4500) && tick % 30 == 0)
            {
                spokenAsked++;
                FallbackAsk(hub, TextPriority.Now, 1.1, false, now, ref n, w => spokenWritten += w == null ? 1 : 0);   // a conversation's line
            }
            for (int i = running.Count - 1; i >= 0; i--)
            {
                if (running[i].Due <= now)
                {
                    q.Done(running[i].Job, "Heard about 5th Ave. You alright?", GpuReply, null);
                    running.RemoveAt(i);
                }
            }
            if (running.Count == 0)
            {
                TextJob job = q.Take(now, true);
                if (job != null)
                {
                    running.Add((job, now + GpuReply));
                }
            }
            hub.Look(now);
        }
        var why = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
        var rule = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        int lines = hub.Tally.Totals("RLO Texts Lite", why, rule);
        int share = lines > 0 ? why[0] * 100 / lines : 0;
        Check(share >= 95 && spokenWritten == 0 && hub.Budget.Step == 0 && hub.Budget.Downs == 0,
            "texts fallback, the session at 46 ms a reply: the model words " + share + "% of " + lines + " lines shown (wanted 95% or more), every one of the " + spokenAsked
            + " spoken lines, and the budget never steps down (" + hub.Budget.Downs + " down; written " + (lines - why[0]) + ", slow " + why[(int)TextShownWhy.Slow] + ", held " + why[(int)TextShownWhy.Held] + ")");
    }

    // A request of the replay: a speaker of its own, so the repeat check never refuses the same words from two people.
    private static void FallbackAsk(TextsHub hub, TextPriority p, double seconds, bool guess, double now, ref int n, Action<string> done)
    {
        n++;
        TextRequest r = TextsAskOf((guess ? "stg:" : "k:") + n, "friend.crash", null, done ?? (w => { }), p, seconds, guess);
        r.Speaker = "s:" + n;
        hub.Ask(r, now);
    }

    // ---------------------------------------------------------------- the hourly line: reasons by intent, and its verdict

    private static void TextsFallbackLine()
    {
        var t = new TextShown();
        for (int i = 0; i < 25; i++)
        {
            t.Count("RLO Texts Lite 7 (data v6)", "citizen.comment.words", TextShownWhy.Model);
        }
        for (int i = 0; i < 5; i++)
        {
            t.Count("RLO Texts Lite 7 (data v6)", "street.greet", TextShownWhy.Slow);
        }
        t.Count("RLO Texts Lite 7 (data v6)", "street.greet", TextShownWhy.NoCard);
        string pass = t.Line("day 4, 10:00");
        Check(pass == "RLO Texts check (what was shown, day 4, 10:00): RLO Texts Lite 7 (data v6): the model 25, written 6 (slow 5; no card 1), by intent: citizen.comment.words 25/0, street.greet 0/6 (slow 5; no card 1). "
                      + "The model worded 83% of the 30 line(s) it could have (wanted 80% or more): PASS.",
            "texts fallback, the hourly line: each intent's written lines with their reasons, and the share the model worded of the lines it could have, PASS at 80% or more (" + pass + ")");
        var stuck = new TextShown();
        for (int i = 0; i < 103; i++)
        {
            stuck.Count("RLO Texts Lite 7 (data v6)", "citizen.comment.words", TextShownWhy.Model);
        }
        for (int i = 0; i < 180; i++)
        {
            stuck.Count("RLO Texts Lite 7 (data v6)", "citizen.post.events", TextShownWhy.Held);
        }
        for (int i = 0; i < 14; i++)
        {
            stuck.Count("RLO Texts Lite 7 (data v6)", "citizen.post.events", TextShownWhy.Unwanted);
        }
        string fail = stuck.Line(null);
        Check(fail.EndsWith("The model worded 34% of the 297 line(s) it could have (wanted 80% or more): most written lines were held (180).", StringComparison.Ordinal)
              && fail.Contains("citizen.post.events 0/194 (unwanted 14; held 180)"),
            "texts fallback, the hourly line: the log's session would say it fell short and why: most written lines held back (" + fail.Substring(Math.Max(0, fail.Length - 140)) + ")");
        var off = new TextShown();
        for (int i = 0; i < 40; i++)
        {
            off.Count(TextsHub.NoPack, "friend.crash", i % 2 == 0 ? TextShownWhy.NoPack : TextShownWhy.Off);
        }
        Check(off.Line(null).EndsWith("The model worded 100% of the 0 line(s) it could have (wanted 80% or more): too few to judge yet.", StringComparison.Ordinal)
              && TextShown.Could(new int[Enum.GetValues(typeof(TextShownWhy)).Length]) == 0,
            "texts fallback, the hourly line: lines with no pack, switched off, in another language or with no card are never held against the model");
    }

    // ---------------------------------------------------------------- the sources: wired, told, nothing per frame

    private static void TextsFallbackSources()
    {
        string look = ModSource(@"Texts\TextsHub.Look.cs");
        string director = ModSource(@"Texts\TextsDirector.cs");
        string city = ModSource(@"Texts\TextsDirector.City.cs");
        string tests = ModSource(@"Texts\TextsDirector.Tests.cs");
        string queue = ModSource(@"Texts\TextQueue.cs");
        Check(look.Contains("if (_budget.Calm(now))") && look.Contains("_budget.Apply(_queue);   // a minute with no miss: a step back up")
              && look.Contains("End(i, null, false, why, _waiting[i].Request.Guess);") && look.Contains("Over(i, !w.ViaService && BudgetMiss(w.Job), Late(w.Job));"),
            "texts fallback sources: the hub's look calms the budget, a guess handed back is never a line shown, and a run-out request names held or slow");
        Check(director.Contains("if (Hub.Budget.Step != _budgetStep)") && director.Contains("TextBudget.Describe(_budgetStep, Hub.Queue)")
              && director.Contains("Hub.Budget.Apply(Hub.Queue);    // ...and its budget a minute from this machine's time"),
            "texts fallback sources: the director writes one line when the budget changes its step, and sets the budget from the speed measured");
        Check(city.Contains("if (Meta.Dev.Verbose)") && city.Contains("TextBudget.Describe(Hub.Budget.Step, Hub.Queue)") && city.Contains("Hub.Budget.Downs") && city.Contains("Hub.Budget.Ups"),
            "texts fallback sources: the hourly line (Developer diagnostics) says the budget's step and its steps this session");
        int keys = tests.Split(new[] { "\"reallife_texts_why_" }, StringSplitOptions.None).Length - 1;
        Check(keys == Enum.GetValues(typeof(TextShownWhy)).Length - 1 && tests.Contains("\"reallife_texts_why_held\""),
            "texts fallback sources: the notice of what was shown has a text for every reason, held included (" + keys + ")");
        Check(queue.Contains("j.Held = true;") && queue.Contains("(Spoken(j) ? spokenFull : full)") && queue.Contains("_started.Count >= _perMinute - Kept(_perMinute)"),
            "texts fallback sources: the queue marks what it holds back, and keeps a share of the minute for spoken lines");
    }

    // ---------------------------------------------------------------- the texts

    private static void TextsFallbackTexts()
    {
        var held = new Dictionary<string, string> { { "en", "held back by the budget" }, { "pt", "retidas pelo limite de pedidos" }, { "zh-cn", "因请求上限而被搁置" } };
        bool keys = true;
        foreach (KeyValuePair<string, string> pair in held)
        {
            Dictionary<string, string> texts = LocaleFile(pair.Key);
            keys &= texts.TryGetValue("reallife_texts_why_held", out string v) && v.StartsWith("{n} ", StringComparison.Ordinal) && v.Contains(pair.Value);
        }
        Check(keys, "texts fallback, texts: the reason held has its words in en, pt and zh-cn, with its count");
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: after a busy moment RLO Texts could stop wording messages and conversations for the rest of the session" },
            { "pt", "Corrigido: depois de um momento movimentado, o RLO Texts podia parar de escrever mensagens e conversas" },
            { "zh-cn", "已修复：繁忙一阵之后，RLO Texts 可能在本次游戏剩余时间里不再撰写消息和对话" },
            { "de", "Behoben: Nach einem geschäftigen Moment konnte RLO Texts für den Rest der Sitzung aufhören" },
            { "fr", "Corrigé : après un moment chargé, RLO Texts pouvait cesser de rédiger les messages" },
            { "es", "Corregido: tras un momento de mucha actividad, RLO Texts podía dejar de redactar mensajes" },
            { "ru", "Исправлено: после напряжённого момента RLO Texts мог перестать писать сообщения" },
            { "nl", "Opgelost: na een druk moment kon RLO Texts voor de rest van de sessie stoppen" },
            { "tr", "Düzeltildi: Yoğun bir andan sonra RLO Texts oturumun geri kalanında" },
            { "pl", "Naprawiono: po intensywnej chwili RLO Texts mógł do końca sesji przestać" },
            { "ko", "수정: 바쁜 순간이 지나면 RLO Texts가 남은 세션 동안" }
        };
        bool every = true, clean = true;
        string lacking = string.Empty;
        foreach (KeyValuePair<string, string> pair in lines)
        {
            Dictionary<string, string> texts = LocaleFile(pair.Key);
            bool has = WhatsNewHas(texts, pair.Value);
            every &= has;
            lacking += has ? string.Empty : pair.Key + " ";
            foreach (KeyValuePair<string, string> row in texts)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains(pair.Value))
                {
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–") && Occurs(row.Value, pair.Value) == 1;
                }
            }
        }
        Check(lines.Count == 11 && every, "texts fallback, texts: What's new tells of the fix, with its fix mark, in all eleven languages (" + (lacking.Length > 0 ? "lacking " + lacking : "none lacking") + ")");
        Check(clean, "texts fallback, texts: the fix line is said once in its row, with no double spaces, exclamation marks or dashes");
    }

    // ---------------------------------------------------------------- the look's new work costs next to nothing

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void TextsFallbackTiming()
    {
        var b = new TextBudget();
        var q = new TextQueue();
        b.Calm(0.0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100000; i++)
        {
            if (b.Calm(i * 0.001))
            {
                b.Apply(q);
            }
        }
        double each = sw.Elapsed.TotalMilliseconds / 100000.0;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(each < 0.002 && bytes == 0, "texts fallback, timing: the budget's calm look takes " + each.ToString("0.00000") + " ms (under 0.002 ms) and allocates " + bytes + " bytes (none)");
    }
}
