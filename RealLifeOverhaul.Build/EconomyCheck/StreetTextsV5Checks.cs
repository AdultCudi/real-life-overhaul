using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// RLO Texts data v5 in street talk: a data 5 pack is asked v5's cards (v4's with the optional facts, and the eleven new
// branches of the lines written until then), a data 4 pack v4's exact cards and never a new branch; the card's moment from its
// weekday and hour as the generator makes it; the stranger knows the player's name only when told or when they know the face
// as famous or infamous; thanks warmly or coolly by the talk's score before the turn; and the packs' licence files.
internal static partial class Program
{
    private static void StreetTextsV5()
    {
        StreetV5Moment();
        StreetV5KnownName();
        StreetV5Lines();
        StreetV5Hubs();
        StreetV5Sources();
        StreetV5Tiers();
        StreetV5Packs();
    }

    // ---------------------------------------------------------------- three packs: the tiers, and RLO before 0.29.1

    private static void StreetV5Tiers()
    {
        Check(TextPacks.Tier(1, 0, true, false) == 1 && TextPacks.Tier(2, 0, true, false) == 3 && TextPacks.Tier(2, 2, true, true) == 2 && TextPacks.Tier(2, 3, true, true) == 3
              && TextPacks.Tier(1, 1, true, true) == 1 && TextPacks.Tier(0, 2, false, true) == 2 && TextPacks.Tier(3, 3, true, true) == 0 && TextPacks.Tier(2, 4, true, true) == 4
              && TextPacks.Tier(2, 5, true, true) == 0
              && TextPacks.Tier(0, 0, false, false) == 0 && TextPacks.Tier(2, 0, true, true) == 0,
            "texts, packs: a pack's tier is its tier line (Lite 1, Medium 2, RLO Texts 3, RLO Texts Large 4), else the first packs' rank (1 Lite, 2 the full pack, never Large); a rank above 2 or a tier above 4 is no pack");
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-texts-tiers-" + System.Diagnostics.Process.GetCurrentProcess().Id);
        try
        {
            var bodies = new[]
            {
                ("RLO Texts Lite", "name=RLO Texts Lite\nmodel=model/m.gguf\nexe=runtime/llama-server.exe\nrank=1\ntier=1\nversion=4\ndata=5\n"),
                ("RLO Texts Medium", "name=RLO Texts Medium\nmodel=model/m.gguf\nexe=runtime/llama-server.exe\nrank=2\ntier=2\nversion=1\ndata=5\n"),
                ("RLO Texts", "name=RLO Texts\nmodel=model/m.gguf\nexe=runtime/llama-server.exe\nrank=2\ntier=3\nversion=4\ndata=5\n"),
                ("Old full", "name=Old full\nmodel=model/m.gguf\nexe=runtime/llama-server.exe\nrank=2\nversion=3\ndata=4\n")
            };
            foreach (var (name, body) in bodies)
            {
                string f = System.IO.Path.Combine(root, name);
                Directory.CreateDirectory(System.IO.Path.Combine(f, "model"));
                Directory.CreateDirectory(System.IO.Path.Combine(f, "runtime"));
                File.WriteAllText(System.IO.Path.Combine(f, "model", "m.gguf"), "m");
                File.WriteAllText(System.IO.Path.Combine(f, "runtime", "llama-server.exe"), "x");
                File.WriteAllText(System.IO.Path.Combine(f, TextPacks.File), body);
            }
            List<TextPack> all = TextPacks.Find(new[] { root });
            Check(all.Count == 4 && all[0].Name == "RLO Texts" && all[1].Name == "Old full" && all[2].Name == "RLO Texts Medium" && all[3].Name == "RLO Texts Lite",
                "texts, packs: the best pack is chosen first: RLO Texts (tier 3, and an older full pack with only its rank 2 beside it), then Medium, then Lite ("
                + string.Join(", ", all.ConvertAll(x => x.Name + " " + x.Rank)) + ")");
            List<string> old = OldRloOrder(root);
            Check(old.Count == 4 && old[0] == "RLO Texts" && old[1] == "Old full" && old[2] == "RLO Texts Medium" && old[3] == "RLO Texts Lite",
                "texts, packs: RLO before 0.29.1 (ranks 1 and 2 only, then the newest version) takes every pack and orders them the same way: RLO Texts, Medium, Lite (" + string.Join(", ", old) + ")");
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
    }

    // How RLO 0.29.0 chose among packs (TextPacks.Read and Better of that release): a rank of 1 or 2 or no pack, the
    // highest rank first, then the newest version. The pack folders' names in that order.
    private static List<string> OldRloOrder(string root)
    {
        var packs = new List<(string name, int rank, int version)>();
        foreach (string f in Directory.GetDirectories(root))
        {
            string txt = System.IO.Path.Combine(f, TextPacks.File);
            if (!File.Exists(txt))
            {
                continue;
            }
            int rank = 0, version = 0;
            foreach (string line in File.ReadAllLines(txt))
            {
                if (line.StartsWith("rank=", StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring(5), out rank);
                }
                else if (line.StartsWith("version=", StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring(8), out version);
                }
            }
            if (rank >= 1 && rank <= 2)
            {
                packs.Add((System.IO.Path.GetFileName(f), rank, version));
            }
        }
        packs.Sort((a, b) => a.rank != b.rank ? b.rank.CompareTo(a.rank) : b.version.CompareTo(a.version));
        return packs.ConvertAll(p => p.name);
    }

    // ---------------------------------------------------------------- the moment

    private static readonly string[] WeekdayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    private static void StreetV5Moment()
    {
        // The edges of defs_v5.moment_of, hour by hour (0 Monday ... 6 Sunday).
        var wrong = new List<string>();
        void Is(int weekday, int hour, string want)
        {
            string got = StreetCards.Moment(weekday, hour);
            if (got != want)
            {
                wrong.Add(WeekdayNames[weekday] + " " + hour + ": " + (got ?? "none") + ", not " + (want ?? "none"));
            }
        }
        Is(0, 5, "night"); Is(0, 6, "morning"); Is(0, 7, "rush hour"); Is(0, 9, "rush hour"); Is(0, 10, "morning"); Is(0, 11, null);
        Is(2, 16, null); Is(2, 17, "rush hour"); Is(2, 18, "rush hour"); Is(2, 19, "evening"); Is(2, 20, "night"); Is(4, 23, "night"); Is(4, 0, "night");
        Is(5, 5, "night"); Is(5, 6, "morning"); Is(5, 7, "morning"); Is(5, 9, "morning"); Is(5, 10, "the weekend"); Is(5, 17, "the weekend");
        Is(6, 18, "evening"); Is(6, 19, "evening"); Is(6, 20, "night"); Is(6, 8, "morning"); Is(6, 12, "the weekend");
        Check(wrong.Count == 0, "street talk, data v5: the moment from the weekday and hour is the generator's (rush hours on weekdays, then night, a weekend's day, morning, evening; "
                                + string.Join("; ", wrong) + ")");

        // Every v5 fixture of a moment branch: the moment its card's when line gives is the one the generator put on it.
        int rows = 0, same = 0;
        var off = new List<string>();
        foreach (JsonElement r in StreetV5Fixtures())
        {
            string intent = r.GetProperty("intent").GetString(), branch = r.GetProperty("branch").GetString();
            if (!TextIntents.MomentBranch(intent, branch))
            {
                continue;
            }
            string when = r.GetProperty("who").GetProperty("When").GetString();
            int weekday = Array.IndexOf(WeekdayNames, when.Substring(0, when.IndexOf(' ')));
            int hour = int.Parse(when.Substring(when.IndexOf(' ') + 1, 2), System.Globalization.CultureInfo.InvariantCulture);
            string want = r.GetProperty("slots").TryGetProperty("moment", out JsonElement m) ? m.GetString() : null;
            rows++;
            if (StreetCards.Moment(weekday, hour) == want)
            {
                same++;
            }
            else if (off.Count < 4)
            {
                off.Add(when + ": " + (StreetCards.Moment(weekday, hour) ?? "none") + ", the generator " + (want ?? "none"));
            }
        }
        Check(rows >= 20 && same == rows, "street talk, data v5: the moment of every fixture card of a moment branch is the one its when line gives (" + same + " of " + rows + "; " + string.Join("; ", off) + ")");
    }

    // ---------------------------------------------------------------- the name

    private static void StreetV5KnownName()
    {
        Check(!StreetCards.KnowsName(false, false, false) && StreetCards.KnowsName(true, false, false) && StreetCards.KnowsName(false, true, false) && StreetCards.KnowsName(false, false, true),
            "street talk, data v5: a stranger knows the player's name only when told (in this talk or earlier today) or when they know the face as famous or infamous");

        // Famed: recognised, and the standing a famous (55 and over) or infamous (under 40) greeting's; never in between, never unrecognised.
        bool famous = false, infamous = false, between = true, unknown = true;
        for (int key = 1; key < 400; key++)
        {
            StreetStranger s = StreetRules.Identity(321, 12, key, key % 2 == 0, 22 + key % 50, 6, -1);
            StreetApproach high = StreetRules.Approach(s, null, new StreetFacts { Day = 12, Hour = 13, Standing = 70, Recognition = 100, Player = "Alex" }, 0, 0);
            StreetApproach low = StreetRules.Approach(s, null, new StreetFacts { Day = 12, Hour = 13, Standing = 20, Recognition = 100, Player = "Alex" }, 0, 0);
            StreetApproach mid = StreetRules.Approach(s, null, new StreetFacts { Day = 12, Hour = 13, Standing = 47, Recognition = 100, Player = "Alex" }, 0, 0);
            StreetApproach none = StreetRules.Approach(s, null, new StreetFacts { Day = 12, Hour = 13, Standing = 70, Recognition = 0, Player = "Alex" }, 0, 0);
            famous |= high.Famed && high.Opening == StreetLine.OpenFamous;
            infamous |= low.Famed && low.Opening == StreetLine.OpenInfamous;
            between &= !mid.Famed;
            unknown &= !none.Famed && !none.Recognised;
            if (high.Famed != high.Recognised || low.Famed != low.Recognised)
            {
                famous = false;
                break;
            }
        }
        Check(famous && infamous && between && unknown, "street talk, data v5: the face known as famous or infamous counts as knowing the name; a face known for neither, or not known, does not");

        // The words: a name never told goes, a told one stays (the guard if a model still slips).
        Check(StreetCards.Spoken("Nice to meet you, Alex.", "Alex") == "Nice to meet you." && StreetCards.Spoken("Nice to meet you, Alex.", null) == "Nice to meet you, Alex.",
            "street talk, data v5: a stranger who was not told the name does not say it; one who was may");
    }

    // ---------------------------------------------------------------- the lines

    private static readonly StreetLine[] V5Lines =
    {
        StreetLine.DayGood, StreetLine.DayFine, StreetLine.DayLow, StreetLine.EventGoing, StreetLine.EventNot,
        StreetLine.WorkLikes, StreetLine.WorkMixed, StreetLine.WorkRetired, StreetLine.Welcome, StreetLine.GreetBack
    };

    private static void StreetV5Lines()
    {
        var wrong = new List<string>();
        for (int i = 0; i < (int)StreetLine.Count; i++)
        {
            var line = (StreetLine)i;
            string four = StreetCards.V4(line, out string b4, out int l4);
            string byFour = StreetCards.ByData(line, TextIntents.DataV4, true, out string bb4, out int lb4);
            string five = StreetCards.ByData(line, TextIntents.DataV5, true, out string b5, out int l5);
            bool isNew = Array.IndexOf(V5Lines, line) >= 0;
            if (byFour != four || bb4 != b4 || lb4 != l4)
            {
                wrong.Add(line + ": a data 4 pack's card is not v4's");
            }
            if (isNew && (four != null || five == null))
            {
                wrong.Add(line + ": " + (four != null ? "has a v4 card" : "has no v5 card"));
            }
            if (!isNew && (five != four || b5 != b4 || l5 != l4))
            {
                wrong.Add(line + ": v5's card is not v4's");
            }
            if (five != null)
            {
                TextIntent at4 = TextIntents.Find(five, b5, TextIntents.DataV4), at5 = TextIntents.Find(five, b5, TextIntents.DataV5);
                if (at5 == null || at5.Data != TextIntents.DataV5 || (isNew ? at4 != null : at4 == null || at4.Data != TextIntents.DataV4))
                {
                    wrong.Add(line + ": " + five + "#" + b5 + " not registered as " + (isNew ? "data 5 only" : "data 4 and data 5"));
                }
            }
            bool written4 = StreetCards.Written(line, TextIntents.DataV4), written5 = StreetCards.Written(line, TextIntents.DataV5);
            if (written4 != (four == null) || written5 != (five == null) || written5 != (line == StreetLine.Waiting || line == StreetLine.DirAsk))
            {
                wrong.Add(line + ": written for data 4 " + written4 + ", for data 5 " + written5);
            }
        }
        StreetCards.V5(StreetLine.Welcome, false, out string cool, out _);
        StreetCards.V5(StreetLine.Welcome, true, out string warm, out _);
        StreetCards.V5(StreetLine.DayGood, true, out _, out int up);
        StreetCards.V5(StreetLine.DayLow, true, out _, out int down);
        StreetCards.V5(StreetLine.DayFine, true, out _, out int flat);
        Check(wrong.Count == 0 && cool == "cool" && warm == "warm" && up == 1 && down == -1 && flat == 0,
            "street talk, data v5: the lines written until now have their v5 card for a data 5 pack only (their day leaning with it, thanks warm or cool), the rest v4's; "
            + "written for data 5 only the \"go on\" and the question of where to (" + string.Join("; ", wrong) + ")");

        int v4 = 0, v5 = 0, moment = 0;
        foreach (TextIntent t in TextIntents.All)
        {
            if (!t.Key.StartsWith("street.", StringComparison.Ordinal) || t.Data > TextIntents.DataV5)
            {
                continue;   // (data v6's street cards are the contract's: TextsSixChecks.cs)
            }
            v4 += t.Data == TextIntents.DataV4 ? 1 : 0;
            v5 += t.Data == TextIntents.DataV5 ? 1 : 0;
            moment += t.Data == TextIntents.DataV5 && t.Optional.Length == 4 ? 1 : 0;
            bool optional = t.Data == TextIntents.DataV5 ? t.Optional.Length == (TextIntents.MomentBranch(t.Key, t.Branch) ? 4 : 1) && t.Optional[t.Optional.Length - 1] == "you know their name: {known}"
                : t.Optional.Length == 0;
            if (!optional)
            {
                v5 = -1000;
            }
        }
        Check(v4 == 70 && v5 == 81 && moment == 8, "street talk, data v5: v4's 70 cards again at data 5 and 11 new (" + v5 + "), the optional facts on every v5 card, the moment's on its 8 branches (" + moment + "), none on v4's");

        // Thanks: warm at a score of -1 and above, decided before the turn (the rapport the thanks earn comes after).
        StreetFacts f = new StreetFacts { Day = 12, Hour = 13, Standing = 55, Player = "Alex" };
        StreetChat c = StreetChatOf(17, f);
        int baseScore = StreetRules.Score(c);
        c.Rapport = StreetRules.WarmThanksLeast - baseScore;
        bool atEdge = StreetRules.WarmThanks(c) && StreetRules.Answer(c, StreetAsk.Thanks, f, 0).Rapport == 1;
        c.Rapport--;
        bool below = !StreetRules.WarmThanks(c) && StreetRules.Answer(c, StreetAsk.Thanks, f, 0).Rapport == 0;
        Check(atEdge && below && StreetRules.WarmThanksLeast == -1, "street talk, data v5: thanks are taken warmly at a talk's score of -1 and above, coolly below, as the rules count the point");
    }

    // ---------------------------------------------------------------- a real hub at data 4 and 5

    private static void StreetV5Hubs()
    {
        int rows = 0, five = 0, four = 0, refused = 0, newRows = 0;
        var wrong = new List<string>();
        foreach (JsonElement r in StreetV5Fixtures())
        {
            rows++;
            string intent = r.GetProperty("intent").GetString(), branch = r.GetProperty("branch").ValueKind == JsonValueKind.Null ? null : r.GetProperty("branch").GetString();
            string card = r.GetProperty("card").GetString();
            bool isNew = TextIntents.Find(intent, branch, TextIntents.DataV4) == null;
            newRows += isNew ? 1 : 0;

            string got5 = AskFixture(TextIntents.DataV5, r, "v5:" + rows);
            if (got5 == Prompt(card))
            {
                five++;
            }
            else if (wrong.Count < 4)
            {
                wrong.Add("data 5, " + intent + "#" + branch + ": " + (got5 ?? "not asked").Replace("\n", "\\n"));
            }

            string got4 = AskFixture(TextIntents.DataV4, r, "v4:" + rows);
            if (isNew)
            {
                refused += got4 == null ? 1 : 0;
                continue;
            }
            string v4Card = WithoutOptional(card, TextIntents.MomentBranch(intent, branch));
            if (got4 == Prompt(v4Card) && !v4Card.Contains("you know their name") && !v4Card.Contains("moment: ") && !v4Card.Contains("weather: "))
            {
                four++;
            }
            else if (wrong.Count < 4)
            {
                wrong.Add("data 4, " + intent + "#" + branch + ": " + (got4 ?? "not asked").Replace("\n", "\\n") + "\n    want " + v4Card.Replace("\n", "\\n"));
            }
        }
        Check(rows == 215 && five == rows, "street talk, data v5: a data 5 hub asks every v5 fixture card byte for byte (" + five + " of " + rows + ")" + (wrong.Count > 0 ? "\n  " + string.Join("\n  ", wrong) : ""));
        Check(newRows > 20 && refused == newRows && four == rows - newRows,
            "street talk, data v5: a data 4 hub asks v4's exact card for the same values, the optional facts left out (" + four + " of " + (rows - newRows) + "), and never a new branch (" + refused + " of " + newRows + " refused)");
    }

    // A fixture asked of a hub of a pack of this data version as the street asks it; the prompt the model would be sent, or null when not asked.
    private static string AskFixture(int data, JsonElement r, string key)
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner(), Data = data };
        var req = new TextRequest
        {
            Use = TextUse.People, Key = key, Intent = r.GetProperty("intent").GetString(), Branch = r.GetProperty("branch").ValueKind == JsonValueKind.Null ? null : r.GetProperty("branch").GetString(),
            Speaker = "st:" + key, Priority = TextPriority.Now, Seconds = 30, Done = w => { }, Day = 4
        };
        JsonElement w = r.GetProperty("who");
        req.Who.Name = w.GetProperty("Name").GetString();
        req.Who.First = w.GetProperty("First").GetString();
        req.Who.Age = w.GetProperty("Age").GetInt32();
        req.Who.Job = w.GetProperty("Job").GetString();
        req.Who.District = w.GetProperty("District").GetString();
        req.Who.Role = w.GetProperty("Role").GetString();
        req.Who.Mood = w.GetProperty("Mood").GetString();
        req.Who.Style = w.GetProperty("Style").GetString();
        req.Who.Player = w.GetProperty("Player").GetString();
        req.Who.When = w.GetProperty("When").GetString();
        foreach (JsonElement t in w.GetProperty("Traits").EnumerateArray())
        {
            req.Who.Traits.Add(t.GetString());
        }
        foreach (JsonProperty p in r.GetProperty("slots").EnumerateObject())
        {
            req.Slots.Set(p.Name, p.Value.GetString());
        }
        foreach (JsonElement h in r.GetProperty("history").EnumerateArray())
        {
            req.History.Add(new TextSaid(h[0].GetString(), h[1].GetString()));
        }
        if (!hub.Ask(req, 0))
        {
            return null;
        }
        TextJob job = q.Take(0, true);
        return job != null ? job.Prompt : "(nothing in the queue)";
    }

    private static string Prompt(string card) => "<|im_start|>system\n" + TextCard.System + "<|im_end|>\n<|im_start|>user\n" + card + "<|im_end|>\n<|im_start|>assistant\n";

    // A v5 card as v4 made it: the optional facts taken off its facts line (the line gone when nothing is left).
    private static string WithoutOptional(string card, bool moment)
    {
        var lines = new List<string>(card.Split('\n'));
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("facts: ", StringComparison.Ordinal))
            {
                continue;
            }
            var kept = new List<string>();
            foreach (string item in lines[i].Substring("facts: ".Length).Split(new[] { "; " }, StringSplitOptions.None))
            {
                bool optional = item.StartsWith("you know their name: ", StringComparison.Ordinal)
                                || (moment && (item.StartsWith("weather: ", StringComparison.Ordinal) || item.StartsWith("moment: ", StringComparison.Ordinal) || item.StartsWith("today: ", StringComparison.Ordinal)));
                if (!optional)
                {
                    kept.Add(item);
                }
            }
            if (kept.Count == 0)
            {
                lines.RemoveAt(i);
            }
            else
            {
                lines[i] = "facts: " + string.Join("; ", kept);
            }
            break;
        }
        return string.Join("\n", lines);
    }

    private static List<JsonElement> StreetV5Fixtures()
    {
        var rows = new List<JsonElement>();
        string path = System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", "TextsCards.jsonl");
        if (!File.Exists(path))
        {
            return rows;
        }
        foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (line.Length == 0)
            {
                continue;
            }
            JsonElement r = JsonDocument.Parse(line).RootElement;
            if (r.TryGetProperty("data", out JsonElement d) && d.ValueKind == JsonValueKind.Number && d.GetInt32() == TextIntents.DataV5)
            {
                rows.Add(r.Clone());
            }
        }
        return rows;
    }

    // ---------------------------------------------------------------- the sources

    private static void StreetV5Sources()
    {
        string texts = TextsSource(@"Wiring\StreetTexts.cs");
        string talk = TextsSource(@"Street\StreetDirector.Talk.cs");
        string hub = TextsSource(@"Texts\TextsHub.cs");
        string known = texts.Substring(Math.Max(0, texts.IndexOf("private static bool KnowsName(StreetChat c)", StringComparison.Ordinal)));
        known = known.Substring(0, Math.Max(0, known.IndexOf('\n')));
        Check(known.Contains("StreetCards.KnowsName(c.Approach.Famed, c.Done[(int)StreetAsk.Name], c.Memo != null && c.Memo.Told)") && !known.Contains("Named") && !known.Contains("Recognised")
              && texts.Contains("StreetCards.Spoken(words, KnowsName(c) ? null : TextsGame.PlayerName)") && texts.Contains("if (KnowsName(c))") && texts.Contains("r.Slots.Set(\"known\", \"yes\");"),
            "street talk, data v5: the card's \"you know their name\" and the words' name both come from the one rule (told, or a famous or infamous face; never their own name given)");
        Check(texts.Contains("StreetCards.ByData(line, TextsDirector.Words.Data, warm, out string branch, out int lean)") && texts.Contains("Copy(data, r.Slots, \"t_weather\", \"weather\");")
              && texts.Contains("StreetCards.Moment(weekday, hour)") && texts.Contains("TextsGame.Clock(out int weekday, out int hour)"),
            "street talk, data v5: the card is the pack's data's, with the rain, the moment from the card's own weekday and hour, and whether they know the name");
        Check(talk.Contains("{ \"t_thanks\", StreetRules.WarmThanks(c) ? \"warm\" : \"cool\" }") && talk.Contains("{ \"t_weather\", _facts.Raining ? \"raining\" : string.Empty }")
              && hub.Contains("TextIntents.Find(r.Intent, r.Branch, _packData)"),
            "street talk, data v5: thanks warm or cool and the rain are decided with the turn's values; the hub builds the card of the pack's own data");
    }

    // ---------------------------------------------------------------- the packs

    // The packs as they are built for release (not-in-repo/textpacks, beside the worktrees): each of the three trained on
    // the newest data the mod's cards have (TextsHub.Newest: data 6 since 0.42.0), with its model and its languages, English
    // among them; their licences; and the order an RLO before 0.29.1 gives them.
    private static void StreetV5Packs()
    {
        string up = System.IO.Path.GetFullPath(System.IO.Path.Combine(TextsBuildFolder(), "..", ".."));
        string packs = null;
        foreach (string beside in Directory.GetDirectories(up))
        {
            if (Directory.Exists(System.IO.Path.Combine(beside, "textpacks", "RLO Texts Lite")))
            {
                packs = System.IO.Path.Combine(beside, "textpacks");
                break;
            }
        }
        if (packs == null)
        {
            Console.WriteLine("SKIPPED: RLO Texts, the packs: no textpacks folder beside the worktree; the packs' files are checked where they are built.");
            return;
        }
        string full = System.IO.Path.Combine(packs, "RLO Texts"), lite = System.IO.Path.Combine(packs, "RLO Texts Lite");
        string medium = System.IO.Path.Combine(packs, "RLO Texts Medium");
        TextPack a = TextPacks.Read(full), b = TextPacks.Read(lite), c = TextPacks.Read(medium);
        bool newest = a != null && b != null && c != null && a.Data == TextsHub.Newest && b.Data == TextsHub.Newest && c.Data == TextsHub.Newest;
        bool english = a != null && b != null && c != null && TextLanguages.Has(a.Languages, TextLanguages.English) && TextLanguages.Has(b.Languages, TextLanguages.English)
                       && TextLanguages.Has(c.Languages, TextLanguages.English);
        Check(newest && english && File.Exists(a.Model) && File.Exists(b.Model) && File.Exists(c.Model) && a.Rank == 3 && c.Rank == 2 && b.Rank == 1,
            "RLO Texts, the packs: the three are trained on the newest data, data v" + TextsHub.Newest + ", each with its model and its languages, English among them ("
            + (a != null ? System.IO.Path.GetFileName(a.Model) + " data " + a.Data + " " + string.Join(",", a.Languages) : "no RLO Texts") + "; "
            + (c != null ? System.IO.Path.GetFileName(c.Model) + " data " + c.Data + " " + string.Join(",", c.Languages) : "no Medium") + "; "
            + (b != null ? System.IO.Path.GetFileName(b.Model) + " data " + b.Data + " " + string.Join(",", b.Languages) : "no Lite") + ")");
        string Read(string folder, string file)
        {
            string p = System.IO.Path.Combine(folder, "licences", file);
            return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : string.Empty;
        }
        string liteNotice = Read(lite, "NOTICE.txt"), lfm = Read(lite, "LICENSE-LFM-Open-License-v1.0.txt"), fullNotice = Read(full, "NOTICE.txt");
        Check(lfm.Contains("LFM Open License v1.0") && lfm.Contains("Liquid AI") && Read(lite, "LICENSE-Apache-2.0.txt").Length == 0
              && liteNotice.Contains("LFM2.5-350M") && liteNotice.Contains("Liquid AI") && liteNotice.Contains("modified by Real Life Overhaul") && !liteNotice.Contains("SmolLM2")
              && Read(lite, "LICENSE-llama.cpp.txt").Contains("MIT License"),
            "RLO Texts, the packs: the Lite pack carries the LFM Open License v1.0, a notice that its model is LFM2.5-350M by Liquid AI modified by Real Life Overhaul, and llama.cpp's MIT licence; no SmolLM2 Apache notice");
        Check(Read(full, "LICENSE-Apache-2.0.txt").Contains("Apache License") && fullNotice.Contains("Qwen") && !fullNotice.Contains("LFM") && Read(full, "LICENSE-llama.cpp.txt").Contains("MIT License")
              && Read(full, "LICENSE-LFM-Open-License-v1.0.txt").Length == 0,
            "RLO Texts, the packs: RLO Texts carries Apache 2.0 with Qwen's notice and llama.cpp's MIT licence, and no LFM licence");
        string mediumNotice = Read(medium, "NOTICE.txt");
        string mediumTxt = File.Exists(System.IO.Path.Combine(medium, TextPacks.File)) ? File.ReadAllText(System.IO.Path.Combine(medium, TextPacks.File)) : string.Empty;
        Check(mediumTxt.Contains("rank=2") && mediumTxt.Contains("tier=2") && mediumTxt.Contains("data=" + TextsHub.Newest) && Read(medium, "LICENSE-Apache-2.0.txt").Contains("Apache License")
              && mediumNotice.Contains("Qwen 3.5 0.8B") && !mediumNotice.Contains("LFM") && Read(medium, "LICENSE-llama.cpp.txt").Contains("MIT License")
              && File.Exists(System.IO.Path.Combine(medium, "README.txt")) && File.Exists(System.IO.Path.Combine(medium, "thumbnail.jpg")),
            "RLO Texts, the packs: RLO Texts Medium has pack.txt (rank 2 for older RLO, tier 2, data " + TextsHub.Newest + "), Qwen's Apache 2.0 and notice, llama.cpp's MIT licence, its README and thumbnail");
        List<string> order = OldRloOrder(packs);
        Check(order.IndexOf("RLO Texts") == 0 && order.IndexOf("RLO Texts Lite") == order.Count - 1,
            "RLO Texts, the packs: the packs' own pack.txt files order the same for RLO before 0.29.1 (" + string.Join(", ", order) + ")");
    }
}
