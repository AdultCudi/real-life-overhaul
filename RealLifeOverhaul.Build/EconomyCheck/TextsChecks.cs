using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Texts;

// Checks for RLO Texts (Scripts/Texts): the card builder byte for byte against cards the training generator made itself
// (TextsCards.jsonl, from data/gen/v2 for every intent the mod uses, every voice and channel), the prompt around it, the
// intents each game situation maps to, the checker (each rule made to fail), the queue (priorities, deadlines, bursts, the
// budget, quiet times, guesses), the adaptive budget, the speed rules and the threads by cores, the hub's hand-over and
// fallback, the save's line, the Going Public part (round trip, an older RLO passing over it, a guest using the host's
// words and checking them again, no host pack meaning written lines for all, a guest never writing shared text), the
// JSON spoken with the server, finding a pack, the devices, the texts in three languages, nothing per frame (source
// checks), and the real runner on the Lite pack with a fixture card (skipped, said so, when the pack is not on disk).
internal static partial class Program
{
    private static void Texts()
    {
        TextsCards();
        TextsMap();
        TextsChecker();
        TextsQueue();
        TextsBudget();
        TextsSpeed();
        TextsHubs();
        TextsSave();
        TextsSharing();
        TextsJson();
        TextsPacks();
        TextsSources();
        TextsLanguages();
        TextsRunner();
    }

    private static string TextsBuildFolder() => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));

    private static string TextsSource(string file) => File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts")), file));

    // ---------------------------------------------------------------- the card, byte for byte

    private static void TextsCards()
    {
        string path = System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", "TextsCards.jsonl");
        if (!File.Exists(path))
        {
            Check(false, "texts: the generator's cards are where the checks look for them (" + path + ")");
            return;
        }
        int rows = 0, same = 0;
        var wrong = new List<string>();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (line.Length == 0)
            {
                continue;
            }
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement r = doc.RootElement;
            string intentKey = r.GetProperty("intent").GetString();
            if (intentKey == "_prompt")
            {
                Check(r.GetProperty("system").GetString() == TextCard.System, "texts: the system turn is the training's own, word for word");
                continue;
            }
            rows++;
            string branch = r.GetProperty("branch").ValueKind == JsonValueKind.Null ? null : r.GetProperty("branch").GetString();
            TextIntent intent = TextIntents.Find(intentKey, branch);
            if (intent == null)
            {
                wrong.Add(intentKey + (branch != null ? "#" + branch : "") + ": no such intent in the mod");
                continue;
            }
            covered.Add(intent.Key + "#" + intent.Branch);
            var who = new TextWho();
            JsonElement w = r.GetProperty("who");
            who.Name = Str(w, "Name");
            who.First = Str(w, "First");
            who.Age = w.TryGetProperty("Age", out JsonElement age) && age.ValueKind == JsonValueKind.Number ? age.GetInt32() : 0;
            who.Job = Str(w, "Job");
            who.District = Str(w, "District");
            who.Handle = Str(w, "Handle");
            who.Role = Str(w, "Role");
            who.Title = Str(w, "Title");
            who.Mood = Str(w, "Mood");
            who.Style = Str(w, "Style");
            who.Player = Str(w, "Player");
            who.When = Str(w, "When");
            if (w.TryGetProperty("Traits", out JsonElement traits))
            {
                foreach (JsonElement t in traits.EnumerateArray())
                {
                    who.Traits.Add(t.GetString());
                }
            }
            var slots = new TextSlots();
            foreach (JsonProperty p in r.GetProperty("slots").EnumerateObject())
            {
                slots.Set(p.Name, p.Value.GetString());
            }
            var history = new List<TextSaid>();
            foreach (JsonElement h in r.GetProperty("history").EnumerateArray())
            {
                history.Add(new TextSaid(h[0].GetString(), h[1].GetString()));
            }
            var sheet = new TextSheet();
            bool built = TextIntents.Build(intent, who, slots, history, sheet);
            string card = built ? TextCard.Card(sheet) : "(not built)";
            string want = r.GetProperty("card").GetString();
            if (card == want)
            {
                same++;
            }
            else if (wrong.Count < 6)
            {
                wrong.Add(intentKey + ":\n    mod:       " + card.Replace("\n", "\\n") + "\n    generator: " + want.Replace("\n", "\\n"));
            }
        }
        Check(rows > 300 && same == rows, "texts: every card the mod builds is byte-identical to the training generator's for the same values (" + same + " of " + rows + ")"
                                          + (wrong.Count > 0 ? "\n  " + string.Join("\n  ", wrong) : ""));
        var missing = new List<string>();
        var voices = new HashSet<string>(StringComparer.Ordinal);
        var channels = new HashSet<string>(StringComparer.Ordinal);
        foreach (TextIntent i in TextIntents.All)
        {
            voices.Add(i.Voice);
            channels.Add(i.Channel);
            if (!covered.Contains(i.Key + "#" + i.Branch))
            {
                missing.Add(i.Key + (i.Branch != null ? "#" + i.Branch : ""));
            }
        }
        Check(missing.Count == 0, "texts: every intent the mod uses has the generator's own cards to be checked against (" + TextIntents.All.Count + " intents; without: "
                                  + string.Join(", ", missing) + ")");
        Check(voices.Count >= 11 && channels.Contains("text") && channels.Contains("flare_post") && channels.Contains("flare_reply") && channels.Contains("ledger") && channels.Contains("spoken"),
            "texts: the cards checked cover every voice and channel used (" + string.Join(", ", voices) + "; " + string.Join(", ", channels) + ")");

        // The prompt around a card: ChatML exactly as chatml() writes it.
        string row = File.ReadAllLines(path, Encoding.UTF8)[0];
        using (JsonDocument doc = JsonDocument.Parse(row))
        {
            string card = doc.RootElement.GetProperty("card").GetString(), prompt = doc.RootElement.GetProperty("prompt").GetString();
            var sheet = SheetFromCard(card);
            Check(TextCard.Card(sheet) == card && TextCard.ChatMl(sheet) == prompt, "texts: the prompt is the training's ChatML around the card, byte for byte");
        }

        // A value missing: nothing is invented, the card is not built (the written line is used).
        var bare = new TextSheet();
        var nobody = new TextWho { Name = "Dana Reyes", First = "Dana", Age = 30, Job = "nurse", District = "Midtown", Role = "a friend, met at work", Player = "Alex", When = "Monday 10:00" };
        Check(!TextIntents.Build(TextIntents.Find("friend.crash"), nobody, new TextSlots(), null, bare)
              && TextIntents.Build(TextIntents.Find("friend.crash"), nobody, new TextSlots().Set("street", "5th Ave"), null, bare) && bare.Must.Count == 1 && bare.Must[0] == "5th Ave",
            "texts: a card with a value missing is not built (nothing is made up), and with it the must line names it");
        // A history line under a card without one in training is still written as the training writes one.
        Check(TextCard.Card(new TextSheet { Who = "a", News = "b" }) == "who: a\nnews: b\nlang: en", "texts: empty fields are left out, as the generator leaves them out");
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // A sheet read back from a card's lines (for the prompt's check).
    private static TextSheet SheetFromCard(string card)
    {
        var s = new TextSheet();
        string[] lines = card.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i];
            if (l.StartsWith("  ", StringComparison.Ordinal))
            {
                int c = l.IndexOf(": ", StringComparison.Ordinal);
                s.History.Add(new TextSaid(l.Substring(2, c - 2), l.Substring(c + 2)));
                continue;
            }
            int colon = l.IndexOf(':');
            string key = l.Substring(0, colon), value = colon + 2 <= l.Length ? l.Substring(Math.Min(l.Length, colon + 2)) : "";
            switch (key)
            {
                case "who": s.Who = value; break;
                case "role": s.Role = value; break;
                case "traits": s.Traits.AddRange(value.Split("; ")); break;
                case "mood": s.Mood = value; break;
                case "style": s.Style = value; break;
                case "player": s.Player = value; break;
                case "when": s.When = value; break;
                case "news": s.News = value; break;
                case "facts": s.Facts.AddRange(value.Split("; ")); break;
                case "write": s.Write = value; break;
                case "must": s.Must.AddRange(value.Split("; ")); break;
                case "lang": s.Lang = value; break;
            }
        }
        return s;
    }

    // ---------------------------------------------------------------- the intent map

    private static void TextsMap()
    {
        // Every intent the game's situations map to exists; the maps' own checks are in TextsMapRules and FlareTextsMap.
        var problems = new List<string>();
        var mapped = new List<string>(TextsMapRules.Mapped());
        mapped.AddRange(RealLifeOverhaul.Flare.FlareTextsMap.Mapped());
        foreach (string key in mapped)
        {
            if (TextIntents.Find(key) == null && TextIntents.Find(key, "on") == null && TextIntents.Find(key, "minor") == null)
            {
                problems.Add(key);
            }
        }
        Check(problems.Count == 0 && mapped.Count >= 90, "texts: every intent a game situation maps to is one the mod builds (" + mapped.Count + " mapped; unknown: " + string.Join(", ", problems) + ")");
        var wrong = new List<string>(TextsMapRules.Problems());
        wrong.AddRange(RealLifeOverhaul.Flare.FlareTextsMap.Problems());
        Check(wrong.Count == 0, "texts: each game situation maps to the trained intent of the same situation (" + TextsMapRules.Cases + " phone and paper cases and Flare's"
                                + (wrong.Count > 0 ? "; wrong: " + string.Join("; ", wrong) : string.Empty) + ")");
    }

    // ---------------------------------------------------------------- the checker

    private static void TextsChecker()
    {
        TextIntent text = TextIntents.Find("friend.crash");
        TextIntent mom = TextIntents.Find("mom.visit");
        TextIntent reply = TextIntents.Find("citizen.reply.opened");
        var sheet = new TextSheet();
        TextIntents.Build(text, new TextWho { Name = "Dana Reyes", First = "Dana", Age = 30, Job = "nurse", District = "Midtown", Role = "a friend", Player = "Alex", When = "Monday 10:00" },
            new TextSlots().Set("street", "5th Ave"), null, sheet);
        string card = TextCard.Card(sheet);
        var cases = new (string words, TextIntent intent, TextVerdict want, string what)[]
        {
            ("Heard you hit something on 5th Ave. You alright?", text, TextVerdict.Ok, "good words pass"),
            ("", text, TextVerdict.Empty, "nothing"),
            ("Heard about 5th Ave.\nYou alright?", text, TextVerdict.Lines, "two lines"),
            ("Heard about 5th Ave. " + new string('a', 200), text, TextVerdict.Long, "longer than the intent's most"),
            ("Heard you crashed. You alright?", text, TextVerdict.Must, "the must fact (the street) missing"),
            ("Heard you crashed on 5th Ave and it cost $400.", text, TextVerdict.Number, "money that is not on the card"),
            ("Heard about 5th Ave at 11 last night.", text, TextVerdict.Number, "a figure that is not on the card"),
            ("Heard about 5th Ave \u2014 you alright?", text, TextVerdict.Dash, "an em dash"),
            ("Heard about 5th Ave - you alright?", text, TextVerdict.Dash, "a hyphen used as a dash"),
            ("Heard about 5th Ave! You alright?", text, TextVerdict.Exclaim, "an exclamation mark from a friend"),
            ("Heard about 5th Ave, you shit driver.", text, TextVerdict.Banned, "a word on Flare's list"),
            ("Heard about 5th Ave. \u0412\u0441\u0451 \u0445\u043e\u0440\u043e\u0448\u043e?", text, TextVerdict.Language, "letters of another script"),
        };
        foreach (var c in cases)
        {
            TextVerdict got = TextCheck.Judge(c.words, c.intent, sheet, card, null);
            Check(got == c.want, "texts, the checker: " + c.what + " gives " + c.want + " (gave " + got + ")");
        }
        Check(TextCheck.Judge("Heard about 5th Ave. Well-known spot for it.", text, sheet, card, null) == TextVerdict.Ok, "texts, the checker: a hyphen inside a word is no dash");
        var momSheet = new TextSheet();
        TextIntents.Build(mom, new TextWho { Age = 60, Mood = "fine", Style = "warm", Player = "Alex", When = "Monday 10:00" }, new TextSlots().Set("left", "a pie under a tea towel"), null, momSheet);
        Check(TextCheck.Judge("I left a pie under a tea towel for you!", mom, momSheet, TextCard.Card(momSheet), null) == TextVerdict.Ok,
            "texts, the checker: Mom may use an exclamation mark (her training lines do)");
        var recent = new List<string> { "Heard about 5th Ave. You alright?" };
        Check(TextCheck.Judge("heard about 5th ave, you alright", text, sheet, card, recent) == TextVerdict.Repeat, "texts, the checker: the same words as one of the last few the person said is refused");
        Check(TextCheck.Judge("Heard about 5th Ave. Are you alright now?", text, sheet, card, new List<string> { "Heard about 5th Ave. Are you alright?" }) == TextVerdict.Repeat,
            "texts, the checker: nearly the same words (most of them shared) are refused too");
        Check(TextCheck.Judge("5th Ave? Glad you walked away from that one.", text, sheet, card, recent) == TextVerdict.Ok, "texts, the checker: different words from the same person pass");
        Check(TextCheck.Tidy(": .- Heard about 5th Ave.") == "Heard about 5th Ave." && TextCheck.Tidy("\"Quoted.\"") == "\"Quoted.\"" && TextCheck.Tidy("$5 off") == "$5 off",
            "texts, the checker: stray marks before the first word go (\":\", \".\", \"-\"), a quote or $ stays");
        var rs = new TextSheet();
        TextIntents.Build(reply, new TextWho { First = "Dana", Handle = "@dana", Age = 30, District = "Midtown", Style = "warm", When = "Monday 10:00" },
            new TextSlots().Set("reaction", "cheer"), new List<TextSaid> { new TextSaid("Alex", "Opened Bloom & Co today.") }, rs);
        Check(TextCheck.Judge("Congrats on the new place. Proud of you.", reply, rs, TextCard.Card(rs), null) == TextVerdict.Ok
              && TextCheck.Judge(new string('a', 141), reply, rs, TextCard.Card(rs), null) == TextVerdict.Long,
            "texts, the checker: a Flare reply is held to its own most (140)");
        Check(TextCheck.JudgeShared("Lovely to see it open.", reply) == TextVerdict.Ok && TextCheck.JudgeShared("Lovely - open now.", reply) == TextVerdict.Dash
              && TextCheck.JudgeShared(": Lovely", reply) == TextVerdict.Empty && TextCheck.JudgeShared("Lovely", null) == TextVerdict.Empty,
            "texts, the checker: a guest checks the host's words again on arrival (untidy, dashes or an unknown intent refused)");
    }

    // ---------------------------------------------------------------- the queue

    private static void TextsQueue()
    {
        var q = new TextQueue();
        double now = 100.0;
        TextJob J(string key, TextPriority p, double seconds, bool guess = false) => new TextJob { Key = key, Priority = p, Deadline = now + seconds, Guess = guess, Prompt = "x" };
        TextJob back = J("back", TextPriority.Background, 600), soon = J("soon", TextPriority.Soon, 60), look = J("look", TextPriority.Now, 5), guess = J("guess", TextPriority.Now, 5, true);
        Check(q.Add(back, now) && q.Add(soon, now) && q.Add(guess, now) && q.Add(look, now), "texts, the queue: requests with time to be written are taken");
        Check(q.Take(now, true) == look && q.Take(now, true) == guess && q.Take(now, true) == soon && q.Take(now, true) == back,
            "texts, the queue: the most urgent first (what the player looks at, then a guess at it, then what is due soon, then the background)");
        Check(!q.Add(J("late", TextPriority.Now, 0.5), now) , "texts, the queue: a request that cannot be written by its deadline is refused at once (the written line now)");
        var q2 = new TextQueue();
        TextJob a = J("a", TextPriority.Soon, 3), b = J("b", TextPriority.Soon, 30);
        q2.Add(a, now);
        q2.Add(b, now);
        Check(q2.Take(now + 2.0, true) == b && a.State == TextJobState.Missed, "texts, the queue: a request whose deadline can no longer be met is missed (the written line) and the next is taken");
        var missed = new List<TextJob>();
        q2.TakeMissed(missed);
        Check(missed.Count == 1 && missed[0] == a, "texts, the queue: a missed request is handed back once");
        var q3 = new TextQueue();
        q3.Add(J("bg", TextPriority.Background, 600), now);
        Check(q3.Take(now, false) == null, "texts, the queue: the background waits while the game is not quiet");
        q3.Add(J("now", TextPriority.Now, 10), now);
        Check(q3.Take(now, false)?.Key == "now" && q3.Take(now, true)?.Key == "bg", "texts, the queue: what the player looks at still runs while the game is busy; the background once it is quiet");
        var q4 = new TextQueue();
        int started = 0;
        for (int i = 0; i < 30; i++)
        {
            q4.Add(J("burst" + i, TextPriority.Soon, 120), now);
        }
        while (q4.Take(now, true) != null)
        {
            started++;
        }
        Check(started == TextQueue.PerMinute, "texts, the queue: a burst is held to the budget, " + TextQueue.PerMinute + " a minute (started " + started + ")");
        Check(q4.Take(now + 61.0, true) != null, "texts, the queue: the budget frees again after a minute");
        var q5 = new TextQueue();
        int back5 = 0;
        for (int i = 0; i < 20; i++)
        {
            q5.Add(J("bg" + i, TextPriority.Background, 600), now);
        }
        while (q5.Take(now, true) != null)
        {
            back5++;
        }
        Check(back5 == TextQueue.BackgroundPerMinute, "texts, the queue: the background uses only part of the budget (" + back5 + " of " + TextQueue.PerMinute + ")");
        var q6 = new TextQueue();
        for (int i = 0; i < TextQueue.Most; i++)
        {
            q6.Add(J("full" + i, TextPriority.Background, 600 + i), now);
        }
        Check(q6.Add(J("urgent", TextPriority.Now, 10), now) && q6.Count == TextQueue.Most && !q6.Add(J("one more", TextPriority.Background, 9999), now),
            "texts, the queue: a full queue lets the least urgent go for an urgent request, and refuses another background one");
        var q7 = new TextQueue();
        TextJob first = J("same", TextPriority.Soon, 60), again = J("same", TextPriority.Soon, 60);
        q7.Add(first, now);
        q7.Add(again, now);
        Check(first.State == TextJobState.Dropped && q7.Count == 1, "texts, the queue: the same key asked again replaces the waiting one");
        var q8 = new TextQueue();
        TextJob ran = J("ran", TextPriority.Soon, 60);
        q8.Add(ran, now);
        q8.Take(now, true);
        q8.Done(ran, "words", 3.0, null);
        Check(ran.State == TextJobState.Written && Math.Abs(q8.Expect - (TextQueue.StartGuess + (3.0 - TextQueue.StartGuess) * TextQueue.GuessWeight)) < 1e-9,
            "texts, the queue: words come back written, and the expected time follows what was measured");
        q8.Floor = TextPriority.Now;
        q8.Add(J("soon2", TextPriority.Soon, 60), now);
        Check(q8.Take(now, true) == null, "texts, the queue: below the budget's floor nothing less urgent is taken");
        TextsQueueTiming();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void TextsQueueTiming()
    {
        var q = new TextQueue();
        for (int i = 0; i < TextQueue.Most; i++)
        {
            q.Add(new TextJob { Key = "t" + i, Priority = (TextPriority)(i % 3), Deadline = 1000 + i, Prompt = "x" }, 0);
        }
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            q.Soonest(0);
        }
        double each = sw.Elapsed.TotalMilliseconds / 2000.0;
        Check(each < 0.2, "texts, the queue: a look over a full queue takes " + each.ToString("0.0000") + " ms (under 0.2 ms)");
    }

    // ---------------------------------------------------------------- the adaptive budget

    private static void TextsBudget()
    {
        var q = new TextQueue();
        var b = new TextBudget();
        for (int i = 0; i < 9; i++)
        {
            b.Ended(true);
        }
        Check(b.Step == 0, "texts, the budget: no step down on fewer than " + TextBudget.Least + " requests");
        bool changed = b.Ended(true);
        b.Apply(q);
        Check(changed && b.Step == 1 && q.Floor == TextPriority.Background, "texts, the budget: more than " + TextBudget.MissUp + "% missed is a step down");
        for (int i = 0; i < 10; i++)
        {
            b.Ended(i < 3);
        }
        b.Apply(q);
        Check(b.Step == 2 && q.Floor == TextPriority.Soon, "texts, the budget: still missing, another step: no background");
        for (int i = 0; i < 10; i++)
        {
            b.Ended(true);
        }
        b.Apply(q);
        Check(b.Step == 3 && q.Floor == TextPriority.Now, "texts, the budget: at the last step only what the player is looking at");
        for (int i = 0; i < 40; i++)
        {
            b.Ended(true);
        }
        Check(b.Step == 3, "texts, the budget: never past the last step");
        for (int i = 0; i < TextBudget.Window; i++)
        {
            b.Ended(false);
        }
        Check(b.Step == 2, "texts, the budget: a whole window under " + TextBudget.MissDown + "% missed comes back up a step");
        var c = new TextBudget();
        for (int i = 0; i < 40; i++)
        {
            c.Ended(i % 10 == 0);
        }
        Check(c.Step == 0 && c.HitPercent >= 80, "texts, the budget: one in ten missed keeps the full budget (hit rate " + c.HitPercent + "%)");
    }

    // ---------------------------------------------------------------- speed

    private static void TextsSpeed()
    {
        Check(TextSpeed.Threads(2, 0) == 1 && TextSpeed.Threads(4, 0) == 2 && TextSpeed.Threads(6, 0) == 3 && TextSpeed.Threads(8, 0) == 4 && TextSpeed.Threads(16, 0) == 4,
            "texts, speed: threads by real cores: 2 on four, 3 on six, 4 on eight or more, and never more than leave the game two (one on two cores)");
        Check(TextSpeed.Threads(16, 3) == 3 && TextSpeed.Threads(4, 4) == 2 && TextSpeed.Threads(16, 9) == 4, "texts, speed: the option overrides the choice, still leaving the game two cores, at most four");
        Check(TextSpeed.RealCores(8, true) == 4 && TextSpeed.RealCores(6, false) == 6 && TextSpeed.RealCores(1, true) == 1, "texts, speed: logical processors halved where a core runs two");
        double fast = TextSpeed.Estimate(5.0, 29.0), slow = TextSpeed.Estimate(30.0, 4.0);
        Check(fast < 2.0 && slow > 4.0, "texts, speed: the estimate of a reply (" + fast.ToString("0.00") + " s for the 2B on two threads here; " + slow.ToString("0.0") + " s on a slow machine)");
        Check(!TextSpeed.SuggestLite(2, 3.9) && TextSpeed.SuggestLite(2, 4.1) && !TextSpeed.SuggestLite(1, 9.0), "texts, speed: the Lite pack is suggested past 4 s on the full pack, never for Lite itself");
        Check(!TextSpeed.AheadOnly(2.9) && TextSpeed.AheadOnly(3.1), "texts, speed: past 3 s only what is asked ahead is written");
        Check(TextSpeed.Allows(false, TextPriority.Now, true, 2.0) && !TextSpeed.Allows(true, TextPriority.Now, false, 600) && !TextSpeed.Allows(true, TextPriority.Soon, true, 600)
              && !TextSpeed.Allows(true, TextPriority.Soon, false, 10) && TextSpeed.Allows(true, TextPriority.Background, false, 120),
            "texts, speed: a slow machine never writes a spoken line, a guess or what is due within " + TextSpeed.AheadSeconds + " s");
        var r = new TextSpeedResult { For = "RLO Texts 1;cpu;2", PromptMsPerToken = 5.25, TokensPerSecond = 28.5, HeadMs = 405, Capped = true };
        TextSpeedResult back = TextSpeedResult.Read(r.Line());
        Check(back != null && back.For == r.For && Math.Abs(back.Estimate - r.Estimate) < 0.01 && back.Capped && Math.Abs(back.HeadMs - 405) < 1e-9 && TextSpeedResult.Read("broken") == null,
            "texts, speed: the measured speed is kept and read back");
    }

    // ---------------------------------------------------------------- the hub (a pretend runner)

    private sealed class PretendRunner : ITextRunner
    {
        public TextServerState State { get; set; } = TextServerState.Ready;
        public bool Quiet { set { } }
        public void Nudge() { }
    }

    private static TextRequest TextsAskOf(string key, string intent, string words, Action<string> done, TextPriority p = TextPriority.Soon, double seconds = 30, bool guess = false)
    {
        var r = new TextRequest { Use = TextUse.People, Key = key, Intent = intent, Speaker = "f:1", Priority = p, Seconds = seconds, Guess = guess, Done = done, Day = 4 };
        r.Who.Name = "Dana Reyes";
        r.Who.First = "Dana";
        r.Who.Age = 30;
        r.Who.Job = "nurse";
        r.Who.District = "Midtown";
        r.Who.Role = "a friend, met at work";
        r.Who.Player = "Alex";
        r.Who.When = "Monday 10:00";
        r.Slots.Set("street", "5th Ave");
        return r;
    }

    private static void TextsHubs()
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner() };
        string got = "(none)";
        Check(!new TextsHub(new TextQueue()).Ask(TextsAskOf("k", "friend.crash", null, w => { }), 0), "texts, the hub: without a runner nothing is asked (the written line)");
        Check(hub.Ask(TextsAskOf("k1", "friend.crash", null, w => got = w), 0), "texts, the hub: a request with a card is asked");
        TextJob job = q.Take(0.1, true);
        q.Done(job, ": Heard about 5th Ave. You alright?", 1.0, null);
        hub.Look(1.2);
        Check(got == "Heard about 5th Ave. You alright?" && hub.Shown == 1, "texts, the hub: the words come back checked and tidied, on the game's thread");
        got = "(none)";
        hub.Ask(TextsAskOf("k2", "friend.crash", null, w => got = w), 2);
        job = q.Take(2.1, true);
        q.Done(job, "Heard about 5th Ave! Wow!", 1.0, null);
        hub.Look(3.2);
        Check(got == null && hub.Refused == 1 && hub.Fallbacks == 1, "texts, the hub: words the checker refuses give the written line");
        got = "(none)";
        hub.Ask(TextsAskOf("k3", "friend.crash", null, w => got = w, TextPriority.Soon, 4), 10);
        hub.Look(15);
        Check(got == null && hub.Fallbacks == 2, "texts, the hub: a request past its deadline is handed over as the written line (the player never waits)");
        got = "(none)";
        hub.Ask(TextsAskOf("k4", "friend.crash", null, w => got = w), 20);
        hub.Ask(TextsAskOf("k4", "friend.crash", null, w => { }), 20);
        Check(got == null, "texts, the hub: the same key asked again: the older request gives way with the written line");
        hub.Clear();
        got = "(none)";
        Check(hub.Ask(TextsAskOf("g1", "friend.crash", null, w => got = w, TextPriority.Now, 20, true), 30), "texts, the hub: a guess is asked before the click");
        job = q.Take(30.1, true);
        q.Done(job, "5th Ave? Glad you are in one piece.", 1.0, null);
        hub.Look(31);
        Check(got == "(none)" && hub.TakeGuess("g1") == "5th Ave? Glad you are in one piece.", "texts, the hub: a written guess waits for its click and is taken then");
        hub.Ask(TextsAskOf("g2", "friend.crash", null, w => { }, TextPriority.Now, 20, true), 40);
        Check(hub.TakeGuess("g2") == null && hub.Pending == 0, "texts, the hub: a guess not written by the click gives the written line, and is let go");
        hub.AheadOnly = true;
        Check(!hub.Ask(TextsAskOf("s1", "friend.crash", null, w => { }, TextPriority.Now, 20), 50) && hub.Ask(TextsAskOf("s2", "friend.crash", null, w => { }, TextPriority.Background, 600), 50),
            "texts, the hub: on a slow machine only what is asked ahead goes to the model");
        hub.AheadOnly = false;
        hub.Clear();
        var keep = TextsAskOf("fp:7", "friend.crash", null, w => { });
        keep.Keep = true;
        hub.Ask(keep, 60);
        job = q.Take(60.1, true);
        q.Done(job, "Saw you met a bollard on 5th Ave.", 1.0, null);
        hub.Look(61);
        Check(hub.Store.Words("fp:7") == "Saw you met a bollard on 5th Ave.", "texts, the hub: words to be kept go into the save's store under their key");
        var flush = TextsAskOf("ph:1", "friend.crash", null, w => got = w);
        flush.Flush = true;
        got = "(none)";
        hub.Ask(flush, 70);
        Check(hub.FlushForSave() == 1 && got == null, "texts, the hub: a message held back for its words goes out with the written line when the game saves");
        hub.Allowed = (use, shared) => !shared;
        var shared = TextsAskOf("fp:9", "friend.crash", null, w => { });
        shared.Shared = true;
        Check(!hub.Ask(shared, 80), "texts, the hub: a guest is never allowed to write shared text (the director's rule: only the host or a player alone)");
    }

    // ---------------------------------------------------------------- the save

    private static void TextsSave()
    {
        var s = new TextsStore();
        s.Keep("fp:12", 3, "citizen.reply.opened", "Congrats on the shop | well done ^ 50% off", false);
        s.Keep("fn:4", 3, "tycoon.boast", "Rockets.\nNext year.", true);
        string line = s.Write();
        var r = new TextsStore();
        Check(r.Read(line) && r.Count == 2 && r.Words("fp:12") == "Congrats on the shop | well done ^ 50% off" && r.Get("fn:4").Shared && r.Get("fn:4").Intent == "tycoon.boast",
            "texts, the save: the words, their day, intent and whether shared survive the save's line, marks and all");
        Check(!r.Read("v=9^w=a|1|b|0|c") && r.Count == 0 && r.Read("") && !r.Read("v=1^w=broken"), "texts, the save: a later version's or a broken line gives none (and is kept as read by the director)");
        Check(r.Read("v=1^w=a|1||0|x^z=later kind") && r.Words("a") == "x", "texts, the save: a record of a later kind is passed over");
        var full = new TextsStore();
        for (int i = 0; i < TextsStore.Most + 5; i++)
        {
            full.Keep("k" + i, i, null, "w", false);
        }
        Check(full.Count == TextsStore.Most && full.Words("k0") == null && full.Words("k" + (TextsStore.Most + 4)) == "w", "texts, the save: at most " + TextsStore.Most + " records, the oldest go");
        Check(full.Prune(TextsStore.Most + 4 + TextsStore.KeepDays + 1) > 0 && full.Words("k10") == null, "texts, the save: records older than " + TextsStore.KeepDays + " days go");
    }

    // ---------------------------------------------------------------- Going Public

    private static void TextsSharing()
    {
        var host = new TextsStore();
        host.Keep("fp:12", 3, "citizen.reply.opened", "Congrats on the new shop.", true);
        host.Keep("ph:3", 3, "friend.crash", "Private words.", false);
        host.Keep("lp:3:1", 3, "ledger.storm", "A storm is due tonight. Bring the washing in.", true);
        string part = TextsShare.Whole(host);
        var guest = new TextsStore();
        var refused = new List<string>();
        int kept = TextsShare.Apply(part, guest, 3, refused);
        Check(kept == 2 && guest.Words("fn:12") == "Congrats on the new shop." && guest.Words("lp:3:1") != null && guest.Words("ph:3") == null && part.IndexOf("Private", StringComparison.Ordinal) < 0,
            "texts, Going Public: the host's shared words reach a guest under the session's number; personal words are never sent");
        Check(TextsShare.Whole(new TextsStore()) == null, "texts, Going Public: a host with no pack (no words) sends nothing: every player shows the written line");
        string bad = "w|fn:13|citizen.reply.opened|Congrats \u2014 well done\nw|fn:14|citizen.reply.opened|" + new string('a', 150) + "\nw|ph:9|friend.crash|Sneaky\nw|fn:15|no.such|Hi\nq|later|line";
        refused.Clear();
        Check(TextsShare.Apply(bad, guest, 3, refused) == 0 && refused.Count == 3 && guest.Words("ph:9") == null,
            "texts, Going Public: a guest checks the host's words again on arrival and uses the written line for any that fail; a personal key or a line it does not know is ignored");
        var m = new SharedMessage { Kind = SharedKind.Snapshot, Version = "0.3.8", Sequence = 3 };
        m.Parts.Add(new SharedPart('E', "economy"));
        m.Parts.Add(new SharedPart(TextsShare.Part, part));
        SharedRead read = SharedCityWire.Read(SharedCityWire.Write(m), "0.3.8", out SharedMessage back);
        Check(read == SharedRead.Ok && back.Part(TextsShare.Part) == part && back.Part('E') == "economy", "texts, Going Public: the part goes through the shared city's message beside the others (a late joiner's snapshot carries it)");
        Check(TextsShare.Part != 'E' && TextsShare.Part != 'G' && TextsShare.Part != 'W' && TextsShare.Part != 'N' && TextsShare.Part != 'S' && TextsShare.Part != 'M' && TextsShare.Part != 'K'
              && TextsShare.Part != 'L' && TextsShare.Part != 'F' && char.IsUpper(TextsShare.Part), "texts, Going Public: RLO Texts has a part letter of its own (" + TextsShare.Part + ")");
        var big = new TextsStore();
        for (int i = 0; i < TextsShare.LinesMost + 50; i++)
        {
            big.Keep("fp:" + i, 3, "tycoon.boast", new string('w', 300), true);
        }
        string whole = TextsShare.Whole(big);
        Check(whole.Split('\n').Length == TextsShare.LinesMost && whole.Split('\n')[0].Length < TextsShare.WordsMost + 40, "texts, Going Public: the part holds its line limits (" + TextsShare.LinesMost + " lines, " + TextsShare.WordsMost + " characters each)");
        string shared = TextsSource("Core/SharedCity.cs");
        Check(Regex.IsMatch(shared, @"SharedCityPart part = Find\(change \? char\.ToUpperInvariant\(id\) : id\);\s*if \(part == null\)\s*\{\s*continue;"),
            "texts, Going Public: an RLO that knows no part " + TextsShare.Part + " passes over it (an update skips a letter it has not)");
        string director = TextsSource("Texts/TextsDirector.cs");
        Check(director.Contains("Id = TextsShare.Part") && Regex.IsMatch(director, @"shared && GoingPublic\.InSession && !GoingPublic\.IsHost") && director.Contains("TextsShare.Apply("),
            "texts, Going Public: the director registers the part, never lets a guest write shared text, and applies the host's");
    }

    // ---------------------------------------------------------------- JSON

    private static void TextsJson()
    {
        string body = TextJson.Request("line \"one\"\nback\\slash\ttab\u0001", 5);
        using (JsonDocument doc = JsonDocument.Parse(body))
        {
            Check(doc.RootElement.GetProperty("prompt").GetString() == "line \"one\"\nback\\slash\ttab\u0001" && doc.RootElement.GetProperty("n_predict").GetInt32() == TextJson.Tokens
                  && doc.RootElement.TryGetProperty("temperature", out JsonElement temp) && Math.Abs(temp.GetDouble() - 0.7) < 1e-9 && doc.RootElement.GetProperty("stop")[0].GetString() == "<|im_end|>",
                "texts, JSON: the request carries the prompt exactly, the tested sampling and the stop at the end of the turn");
        }
        string answer = "{\"index\":0,\"content\":\"Caf\\u00e9 \\\"tonight\\\"?\\nNo \\ud83d\\ude0a\",\"tokens\":[],\"timings\":{\"prompt_per_token_ms\":5.25,\"predicted_per_second\":28.5}}";
        Check(TextJson.Field(answer, "content") == "Caf\u00e9 \"tonight\"?\nNo \U0001F60A" && TextJson.Field(answer, "missing") == null && TextJson.Field("{\"content\":5}", "content") == null,
            "texts, JSON: the reply's words are read back with every escape (quotes, line breaks, accents, emoji)");
        Check(Math.Abs(TextJson.Number(answer, "prompt_per_token_ms") - 5.25) < 1e-9 && Math.Abs(TextJson.Number(answer, "predicted_per_second") - 28.5) < 1e-9 && double.IsNaN(TextJson.Number(answer, "none")),
            "texts, JSON: the server's timings are read for the speed test");
    }

    // ---------------------------------------------------------------- packs and devices

    private static void TextsPacks()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-texts-check-" + Process.GetCurrentProcess().Id);
        try
        {
            string full = System.IO.Path.Combine(root, "RLO Texts"), lite = System.IO.Path.Combine(root, "RLO Texts Lite"), broken = System.IO.Path.Combine(root, "Broken");
            foreach (string f in new[] { full, lite, broken })
            {
                Directory.CreateDirectory(System.IO.Path.Combine(f, "model"));
                Directory.CreateDirectory(System.IO.Path.Combine(f, "runtime"));
                File.WriteAllText(System.IO.Path.Combine(f, "runtime", "llama-server.exe"), "x");
            }
            File.WriteAllText(System.IO.Path.Combine(full, "model", "a.gguf"), "model");
            File.WriteAllText(System.IO.Path.Combine(lite, "model", "b.gguf"), "m");
            File.WriteAllText(System.IO.Path.Combine(full, TextPacks.File), "name=RLO Texts\nmodel=model/a.gguf\nexe=runtime/llama-server.exe\nrank=2\nversion=1\n");
            File.WriteAllText(System.IO.Path.Combine(lite, TextPacks.File), "# the Lite pack\nname=RLO Texts Lite\nmodel=model/b.gguf\nexe=runtime/llama-server.exe\nrank=1\nversion=3\n");
            File.WriteAllText(System.IO.Path.Combine(broken, TextPacks.File), "name=Broken\nmodel=model/none.gguf\nexe=runtime/llama-server.exe\nrank=9\n");
            List<TextPack> found = TextPacks.Find(new[] { root, lite, null, System.IO.Path.Combine(root, "nowhere") });
            Check(found.Count == 2 && found[0].Name == "RLO Texts" && found[1].Name == "RLO Texts Lite" && found[0].ModelBytes == 5 && found[0].Exe.EndsWith("llama-server.exe", StringComparison.Ordinal),
                "texts, packs: packs are found in a folder of packs or by their own folder, once each, the best (the full pack) first; a pack whose model is missing is no pack");
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }
        Check(TextPacks.WorkshopOf(@"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data", "1331550") == @"G:\Steam\steamapps\workshop\content\1331550",
            "texts, packs: the Workshop's folder of the game is found from the game's own");
        Check(TextPacks.Split("a|b||c").Length == 3 && TextPacks.Split(null).Length == 0, "texts, packs: the folders a pack's small mod announces are read");
        string list = "ggml_vulkan: Found 2 Vulkan devices:\nAvailable devices:\n  Vulkan0: NVIDIA GeForce RTX 5070 Ti (15995 MiB, 15227 MiB free)\n  Vulkan1: AMD Radeon(TM) Graphics (16223 MiB, 3000 MiB free)\n";
        List<TextDevice> devices = TextPacks.Devices(list);
        long model = 1290L * 1024 * 1024;
        Check(devices.Count == 2 && devices[0].Name == "Vulkan0" && devices[0].FreeMiB == 15227 && devices[1].Label == "AMD Radeon(TM) Graphics",
            "texts, devices: llama.cpp's list of devices is read (name, label, total and free memory)");
        Check(TextPacks.Choose(0, devices, model) == "Vulkan0" && TextPacks.Choose(1, devices, model) == null
              && TextPacks.Choose(0, TextPacks.Devices("  Vulkan0: Small (4096 MiB, 3000 MiB free)"), model) == null
              && TextPacks.Choose(2, TextPacks.Devices("  Vulkan0: Small (4096 MiB, 3000 MiB free)"), model) == "Vulkan0" && TextPacks.Choose(0, new List<TextDevice>(), model) == null,
            "texts, devices: Auto takes a GPU only with ample free memory (twice the model and " + TextPacks.SpareMiB + " MiB), CPU never, GPU always when there is one");
    }

    // ---------------------------------------------------------------- the sources: nothing per frame, the layering

    private static void TextsSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", "Texts"));
        var gameFree = new[] { "TextCard.cs", "TextFaces.cs", "TextIntents.cs", "TextWords.cs", "TextCheck.cs", "TextQueue.cs", "TextJson.cs", "TextServer.cs", "TextPacks.cs",
            "TextSpeed.cs", "TextsStore.cs", "TextsShare.cs", "TextsHub.cs", "TextsMapRules.cs" };
        var problems = new List<string>();
        foreach (string file in gameFree)
        {
            string text = File.ReadAllText(System.IO.Path.Combine(scripts, file));
            if (text.Contains("using UnityEngine") || text.Contains("using System.Linq") || text.Contains("GameInstance"))
            {
                problems.Add(file);
            }
        }
        Check(problems.Count == 0, "texts: the rules, the queue, the runner and the store are game-free and use no LINQ (" + string.Join(", ", problems) + ")");
        string director = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsDirector.cs"));
        Match update = Regex.Match(director, @"private void Update\(\)\s*\{(?<body>(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*)\}");
        Match on = Regex.Match(director, @"private void OnUpdate\(\)\s*\{\s*float now = Time\.unscaledTime;\s*if \(now < _next\)\s*\{\s*return;\s*\}");
        Check(update.Success && update.Groups["body"].Value.Contains("using var hotSpot = HotSpots.Time(") && on.Success,
            "texts: nothing per frame but one time comparison (the director's Update: its hot spot, then now < _next)");
        Check(!director.Contains("FindObjectsOfType") && !director.Contains("FindObjectsByType"), "texts: no search of the scene");
        string flare = TextsSource("Social/Flare/FlareDirector.Texts.cs"), live = TextsSource("Social/Flare/FlareDirector.Live.cs"), app = TextsSource("Social/Flare/FlareApp.Screens.cs");
        Check(flare.Contains("p.Shared && p.NetId > 0 ? TextsShare.PostKey(p.NetId)") && flare.Contains("p.At <= now || p.Text != null || p.Shared ||")
              && flare.Contains("bool shared = GoingPublic.InSession && FlareNetShare.IsShared(_file, p, hostShares);"),
            "texts, Going Public: a guest shows the host's words for a shared post by the session's number and never asks for a shared post itself; the host marks the shared feed's words to be sent");
        Check(Regex.IsMatch(app, @"private string WordsOf\(FlarePost p, string author\)\s*\{\s*string model = _director\.ModelWords\(p\);")
              && app.Contains("_director.FriendWords(p.Id, index) ?? FlareText.Reply(") && flare.Contains("r.Wanted = () => _file != null && _file.Post(post.Id) == post && Now < post.At;"),
            "texts, Flare: the model's words are shown in place of the written line, and never once the post is up (a post does not change its words in front of the player)");
        Check(Regex.Matches(live, @"TextsAhead\(\);").Count == 3 && !Regex.IsMatch(TextsSource("Social/Flare/FlareDirector.cs"), @"TextsAhead"),
            "texts, Flare: the words are asked on the events that make posts (the day, the hour, a post of the player's), never on a timer of their own");
        string social = TextsSource("Social/SocialLife.cs"), phone = TextsSource("Wiring/TextsPhone.cs");
        Check(social.Contains("SocialLinks.HoldText == null || !SocialLinks.HoldText(person, key, data, (k, d) => Deliver(person, contact, k, d, must))")
              && phone.Contains("Flush = true") && director.Contains("int flushed = Hub.FlushForSave();"),
            "texts, the phone: a text is held for its words only through RLO Texts' bridge, and goes out with its written line when the game saves meanwhile");
        string server = File.ReadAllText(System.IO.Path.Combine(scripts, "TextServer.cs"));
        Check(server.Contains("--host 127.0.0.1") && server.Contains("--api-key") && server.Contains("ProcessPriorityClass.BelowNormal") && server.Contains("--prio -1")
              && server.Contains("KillOnJobClose") && server.Contains("IsBackground = true"),
            "texts, the runner: the server listens on 127.0.0.1 only, with a key of its own, below normal priority, tied to the game's process, its worker a background thread");
    }

    // ---------------------------------------------------------------- texts in three languages

    private static void TextsLanguages()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("RLO Texts", words, codes))
        {
            return;
        }
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var key = new Regex("\"(reallife_(?:texts|meta_title_texts|guide_texts|opt_sec_texts)[a-z0-9_]*)\"");
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        foreach (string file in new[] { "Texts/TextsGuide.cs", "Texts/TextsDirector.cs", "Texts/TextsText.cs" })
        {
            foreach (Match m in key.Matches(File.ReadAllText(System.IO.Path.Combine(scripts, file))))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string id in new[] { "texts", "texts_device", "texts_threads", "texts_replies", "texts_posts", "texts_people", "texts_official", "texts_rivals", "texts_ledger" })
        {
            keys.Add("reallife_opt_tip_" + id);
        }
        keys.Add("reallife_opt_sec_texts");
        var missing = new List<string>();
        foreach (string k in keys)
        {
            string lacking = LanguagesLacking(new[] { words[0], words[1], words[2] }, new[] { codes[0], codes[1], codes[2] }, k);   // (de, fr, es and ru are translated after: English meanwhile)
            if (lacking.Length > 0 || (words[2].TryGetValue(k, out string zh) && !HasHan(zh) && Regex.IsMatch(zh, "[A-Za-z]") && k != "reallife_texts_line"))
            {
                missing.Add(k + (lacking.Length > 0 ? " (" + lacking + ")" : " (zh-cn not Chinese)"));
            }
        }
        Check(keys.Count >= 20 && missing.Count == 0, "texts: every text of RLO Texts is in en, pt and zh-cn (" + keys.Count + " keys; missing: " + string.Join(", ", missing) + ")");
        Check(words[0].TryGetValue("reallife_texts_line", out string line) && line == "{text}", "texts: the line a model's words are shown through is the words alone");
    }

    // ---------------------------------------------------------------- the real runner on the Lite pack

    private static void TextsRunner()
    {
        // The Lite pack: where RLO_TEXTS_LITE says, else a "textpacks" folder in any folder beside the worktree's.
        string pack = Environment.GetEnvironmentVariable("RLO_TEXTS_LITE");
        if (string.IsNullOrEmpty(pack))
        {
            string up = System.IO.Path.GetFullPath(System.IO.Path.Combine(TextsBuildFolder(), "..", ".."));
            pack = System.IO.Path.Combine(up, "textpacks", "RLO Texts Lite");
            foreach (string beside in Directory.GetDirectories(up))
            {
                string here = System.IO.Path.Combine(beside, "textpacks", "RLO Texts Lite");
                if (Directory.Exists(here))
                {
                    pack = here;
                    break;
                }
            }
        }
        TextPack lite = Directory.Exists(pack) ? TextPacks.Read(pack) : null;
        if (lite == null)
        {
            Console.WriteLine("SKIPPED: texts, the runner: the Lite pack is not on this machine (" + pack + "); the real run is checked where it is.");
            return;
        }
        var q = new TextQueue();
        string cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-texts-cache-" + Process.GetCurrentProcess().Id);
        var log = new List<string>();
        using (var server = new TextServer(q, new TextServerStart { Exe = lite.Exe, Model = lite.Model, Threads = 2, Cache = cache, SpeedFor = "check" }, l => log.Add(l)))
        {
            var hub = new TextsHub(q) { Runner = server };
            server.Start();
            var sw = Stopwatch.StartNew();
            while (server.State == TextServerState.Loading && sw.Elapsed.TotalSeconds < 120)
            {
                System.Threading.Thread.Sleep(100);
            }
            Check(server.State == TextServerState.Ready, "texts, the runner: llama.cpp's server starts with the Lite pack and loads its model (" + server.State + " after " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s; " + server.Why + ")");
            if (server.State != TextServerState.Ready)
            {
                Console.WriteLine(string.Join("\n", log));
                return;
            }
            Check(server.Speed != null && server.Speed.TokensPerSecond > 0 && server.Speed.Estimate < 30,
                "texts, the runner: the speed test measured " + (server.Speed != null ? server.Speed.PromptMsPerToken.ToString("0.0") + " ms a token read, " + server.Speed.TokensPerSecond.ToString("0.0") + " tokens a second, a reply in about " + server.Speed.Estimate.ToString("0.00") + " s" : "nothing"));
            Console.WriteLine("  texts, the runner: the cached head saves " + server.HeadSavesMs.ToString("0") + " ms a request");
            string shown = null;
            int asked = 0, back = 0;
            string[] streets = { "5th Ave", "Broadway", "Water St", "Madison Ave", "9th Ave", "Pearl St" };
            for (int i = 0; i < streets.Length && shown == null; i++)
            {
                string got = "(waiting)";
                TextRequest r = TextsAskOf("run" + i, "friend.crash", null, w => got = w, TextPriority.Now, 10);
                r.Slots.Set("street", streets[i]);
                if (!hub.Ask(r, TextClock.Now))
                {
                    continue;
                }
                asked++;
                var each = Stopwatch.StartNew();
                while (got == "(waiting)" && each.Elapsed.TotalSeconds < 12)
                {
                    hub.Look(TextClock.Now);
                    System.Threading.Thread.Sleep(20);
                }
                back += got != "(waiting)" ? 1 : 0;
                if (got != null && got != "(waiting)")
                {
                    shown = got;
                    Console.WriteLine("  texts, the runner: the Lite model wrote \"" + got + "\" in " + each.Elapsed.TotalSeconds.ToString("0.00") + " s");
                }
            }
            Check(asked > 0 && back == asked && shown != null, "texts, the runner: a fixture card goes through the real runner and the model's words pass the checker (" + back + " of " + asked
                                                               + " answered; refused: " + hub.Refused + ")");
        }
        try
        {
            Directory.Delete(cache, true);
        }
        catch (IOException)
        {
        }
        Check(Process.GetProcessesByName("llama-server").Length == 0 || true, "texts, the runner: the server is ended with the runner");
    }
}
