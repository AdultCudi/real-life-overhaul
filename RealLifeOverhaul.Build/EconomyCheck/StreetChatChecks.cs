using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// Street talk that feels alive (not the same three options and questions again and again, and the player's own words
// one press away): the order of the choices comes from the moment, the person and what they just
// said, never from a fixed list, and nothing done comes round again; the follow-ups after the right answers; at least four
// turns for anybody who stops and a turn back for a good exchange; a stranger at worst a little cool unless there is a reason;
// the panel's three buttons with the player's own words one press away and Esc for goodbye; the turn's log line; the last
// three lines on the card (and the fixtures of that shape); written lines never said twice to the player in a day; the model's
// words tidied for speech; the queue's first expectation from the speed test.
internal static partial class Program
{
    private static void StreetConversation()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        StreetChatOrder();
        StreetChatNoRepeat();
        StreetChatFollowUps();
        StreetChatPatience();
        StreetChatWarmth();
        StreetChatLines();
        StreetChatSpoken();
        StreetChatQueue();
        StreetChatSources();
        Console.WriteLine("  street talk, the conversation: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static StreetChat StreetChatOf(int key, StreetFacts f)
    {
        StreetStranger s = StreetRules.Identity(321, f.Day, key, key % 2 == 0, 22 + key % 50, 6, -1);
        var memo = new StreetMemo { Key = key, Day = f.Day };
        return new StreetChat { Who = s, Memo = memo, Approach = StreetRules.Approach(s, memo, f, 0, 0) };
    }

    // ---------------------------------------------------------------- the order

    private static void StreetChatOrder()
    {
        StreetFacts f = StreetDay();
        var list = new List<StreetAsk>();
        var firsts = new Dictionary<StreetAsk, int>();
        var orders = new HashSet<string>();
        for (int k = 0; k < 300; k++)
        {
            StreetRules.Choices(StreetChatOf(8000 + k, f), f, list);
            firsts[list[0]] = firsts.TryGetValue(list[0], out int n) ? n + 1 : 1;
            orders.Add(string.Join(",", list.GetRange(0, 3)));
        }
        int most = 0;
        foreach (int v in firsts.Values)
        {
            most = Math.Max(most, v);
        }
        Check(firsts.Count >= 4 && most < 150 && orders.Count >= 20, "street talk, the order: 300 people do not open the same way: " + firsts.Count + " different first choices (the commonest "
                                                                        + most + " times), " + orders.Count + " different first three");
        var la = new List<StreetAsk>();
        var lb = new List<StreetAsk>();
        bool same = true;
        for (int k = 0; k < 50; k++)
        {
            StreetRules.Choices(StreetChatOf(8001 + k, f), f, la);
            StreetRules.Choices(StreetChatOf(8001 + k, f), f, lb);
            same &= string.Join(",", la) == string.Join(",", lb);
        }
        Check(same, "street talk, the order: the same person at the same moment is offered the same order (seeded, the same on every machine)");

        StreetFacts rain = StreetDay();
        rain.Raining = true;
        int rainFirst = 0, dryFirst = 0;
        bool rainFits = true;
        for (int k = 0; k < 300; k++)
        {
            StreetRules.Choices(StreetChatOf(8000 + k, rain), rain, list);
            rainFirst += list[0] == StreetAsk.SmallTalk ? 1 : 0;
            rainFits &= list[0] == StreetAsk.SmallTalk || list[0] == StreetAsk.Day;
            StreetRules.Choices(StreetChatOf(8000 + k, f), f, list);
            dryFirst += list[0] == StreetAsk.SmallTalk ? 1 : 0;
        }
        Check(rainFits && rainFirst > dryFirst + 50, "street talk, the order: in the rain the weather (or their mood, when it shows) comes first: the weather first for " + rainFirst
                                                     + " of 300, against " + dryFirst + " on a dry afternoon");
        StreetFacts fair = StreetDay();
        fair.EventToday = "Street market";
        int eventSeen = 0, eventFirst = 0;
        for (int k = 0; k < 300; k++)
        {
            StreetRules.Choices(StreetChatOf(8000 + k, fair), fair, list);
            eventSeen += list.Contains(StreetAsk.Event) ? 1 : 0;
            eventFirst += list[0] == StreetAsk.Event ? 1 : 0;
        }
        StreetRules.Choices(StreetChatOf(8000, f), f, list);
        Check(eventSeen == 300 && eventFirst > 40 && !list.Contains(StreetAsk.Event), "street talk, the order: the street event of the day is asked about while there is one (first for " + eventFirst
                                                                                        + " of 300), never without one");
        var topics = new HashSet<int>();
        StreetFacts morning = StreetDay(8);
        for (int k = 0; k < 60; k++)
        {
            topics.Add(StreetRules.SmallTopic(StreetChatOf(8000 + k, morning).Who, morning));
        }
        Check(topics.Contains(StreetRules.SmallMorning) && topics.Contains(StreetRules.SmallBusy) && topics.Contains(StreetRules.SmallAny) && !topics.Contains(StreetRules.SmallNight)
              && StreetRules.SmallTopic(StreetChatOf(1, rain).Who, rain) == StreetRules.SmallRain && StreetRules.SmallTopic(StreetChatOf(1, StreetDay(23)).Who, StreetDay(23)) == StreetRules.SmallNight
              && StreetLines.Small.Length == StreetRules.SmallBusy + 1,
            "street talk, the order: small talk is about the moment: a weekday morning's remark is the morning's, the rush hour's or any day's, by the person; the rain's in the rain, the night's at night");
    }

    // A conversation played by the rules alone, as the director plays it: always the blue button, until they go.
    private static List<StreetAsk> StreetChatPlay(StreetChat c, StreetFacts f, out bool repeated)
    {
        var said = new List<StreetAsk>();
        var list = new List<StreetAsk>();
        repeated = false;
        for (int guard = 0; guard < 30 && !StreetRules.Spent(c); guard++)
        {
            StreetRules.Choices(c, f, list);
            StreetAsk ask = StreetAsk.Count;
            foreach (StreetAsk x in list)
            {
                if (x != StreetAsk.Bye && x != StreetAsk.Rude && x != StreetAsk.Directions && x != StreetAsk.Number && x != StreetAsk.Invite && x != StreetAsk.Business && x != StreetAsk.Area
                    && x != StreetAsk.Election)
                {
                    ask = x;   // (the first that needs nothing of the game: what the rules alone decide)
                    break;
                }
            }
            if (ask == StreetAsk.Count)
            {
                break;
            }
            repeated |= ask != StreetAsk.Thanks && said.Contains(ask);
            said.Add(ask);
            c.Turns++;
            c.Done[(int)ask] = true;
            StreetOutcome o = StreetRules.Answer(c, ask, f, StreetRules.Seed(c.Who.Seed, c.Turns, (int)ask));
            c.Gain(o.Rapport);
            c.Last = o.Line;
            c.Shared |= o.Line == StreetLine.AboutJob || o.Line == StreetLine.AboutRetired;
            if (ask == StreetAsk.Name)
            {
                c.Memo.Named |= o.Line == StreetLine.NameGive;
            }
            if (ask == StreetAsk.Compliment)
            {
                c.Memo.Complimented = true;
            }
            if (ask == StreetAsk.Joke)
            {
                c.Memo.Joked = true;
            }
            if (ask == StreetAsk.Flirt)
            {
                c.Memo.Flirted = true;
            }
        }
        return said;
    }

    private static void StreetChatNoRepeat()
    {
        StreetFacts f = StreetDay();
        bool none = true;
        int longest = 0;
        var openings = new HashSet<string>();
        for (int k = 0; k < 300; k++)
        {
            List<StreetAsk> said = StreetChatPlay(StreetChatOf(9100 + k, f), f, out bool repeated);
            none &= !repeated;
            longest = Math.Max(longest, said.Count);
            openings.Add(string.Join(",", said.GetRange(0, Math.Min(3, said.Count))));
        }
        Check(none, "street talk, no repeats: in 300 conversations played to the end on the blue button, nothing done is offered again");
        Check(openings.Count >= 20, "street talk, no repeats: the first three things said differ from one conversation to the next (" + openings.Count + " different of 300)");
        StreetChat c = StreetChatOf(9100, f);
        c.Done[(int)StreetAsk.Day] = c.Done[(int)StreetAsk.SmallTalk] = c.Done[(int)StreetAsk.AboutThem] = true;
        c.Memo.Named = c.Memo.Complimented = c.Memo.Joked = true;
        var list = new List<StreetAsk>();
        StreetRules.Choices(c, f, list);
        Check(!list.Contains(StreetAsk.Day) && !list.Contains(StreetAsk.SmallTalk) && !list.Contains(StreetAsk.AboutThem) && !list.Contains(StreetAsk.Name) && !list.Contains(StreetAsk.Compliment)
              && !list.Contains(StreetAsk.Joke) && !list.Contains(StreetAsk.Work) && !list.Contains(StreetAsk.Thanks) && list.Contains(StreetAsk.Bye),
            "street talk, no repeats: what was asked this conversation, and their name, a compliment and a joke today, are not offered; whether they like their work only once they said it; thanks only after help; goodbye always");
    }

    // ---------------------------------------------------------------- follow-ups

    private static StreetAsk StreetChatFirstAfter(StreetChat c, StreetFacts f, StreetLine last)
    {
        var list = new List<StreetAsk>();
        c.Last = last;
        StreetRules.Choices(c, f, list);
        return list.Count > 0 ? list[0] : StreetAsk.Count;
    }

    private static void StreetChatFollowUps()
    {
        StreetFacts f = StreetDay();
        bool work = true, thanks = true, name = true, number = true, area = true;
        for (int k = 0; k < 100; k++)
        {
            StreetChat c = StreetChatOf(9300 + k, f);
            c.Turns = 2;
            c.Done[(int)StreetAsk.AboutThem] = true;
            c.Shared = true;
            work &= StreetChatFirstAfter(c, f, StreetLine.AboutJob) == StreetAsk.Work;
            thanks &= StreetChatFirstAfter(c, f, StreetLine.DirFound) == StreetAsk.Thanks && StreetChatFirstAfter(c, f, StreetLine.DirNone) == StreetAsk.Thanks;
            name &= StreetChatFirstAfter(c, f, StreetLine.ComplimentYes) == StreetAsk.Name;
            c.Memo.Named = true;
            number &= StreetChatFirstAfter(c, f, StreetLine.ComplimentYes) == StreetAsk.Number;
            StreetAsk cool = StreetChatFirstAfter(c, f, StreetLine.SmallCool);
            area &= cool == StreetAsk.Area || cool == StreetAsk.Directions || cool == StreetAsk.Day;
        }
        Check(work, "street talk, follow-ups: after what they do, whether they like it comes first");
        Check(thanks, "street talk, follow-ups: after the way somewhere (found or not), thanks comes first");
        Check(name && number, "street talk, follow-ups: after a compliment that landed, their name comes first, and their number once they gave it");
        Check(area, "street talk, follow-ups: after a cool answer, something practical (what is around, the way) or how their day is going comes first");
        StreetChat n = StreetChatOf(9301, f);
        Check(StreetChatFirstAfter(n, f, StreetLine.NumberYes) != StreetAsk.Invite && StreetRules.FollowUp(StreetLine.NumberYes, StreetAsk.Invite, 5) == 0,
            "street talk, follow-ups: a follow-up that cannot be said yet (an invitation before a first exchange) is not offered for being one");
        Check(StreetRules.Answer(n, StreetAsk.Thanks, f, 1).Line == StreetLine.Welcome && StreetRules.Answer(n, StreetAsk.Work, f, 1).Line != StreetLine.Welcome
              && StreetRules.Answer(StreetChatOf(9302, f), StreetAsk.Bye, f, 1).Ends,
            "street talk, follow-ups: thanks has its answer, whether they like their work its own, and goodbye ends it");
        var retired = new StreetStranger { Job = SocialCatalogue.Jobs.Length - 1, Seed = 5, First = "Ada", Last = "Ng" };
        Check(StreetRules.Answer(new StreetChat { Who = retired, Memo = new StreetMemo() }, StreetAsk.Work, f, 1).Line == StreetLine.WorkRetired,
            "street talk, follow-ups: somebody retired is asked whether they miss working, and answers so");
        Check(StreetSay.AskOf(StreetSay.Read("thanks so much", null).Kind) == StreetAsk.Thanks && StreetSay.AskOf(StreetSay.Read("how are you doing today", null).Kind) == StreetAsk.Day,
            "street talk, follow-ups: typed thanks and a typed how are you count as those choices");
    }

    // ---------------------------------------------------------------- patience and warmth

    private static void StreetChatPatience()
    {
        StreetFacts bad = StreetDay(2);
        bad.Unwashed = true;
        bad.Standing = 10;
        int least = int.MaxValue, most = 0;
        for (int k = 0; k < 2000; k++)
        {
            foreach (StreetFacts f in new[] { bad, StreetDay(), StreetDay(8) })
            {
                StreetChat c = StreetChatOf(10000 + k, f);
                least = Math.Min(least, c.Approach.Patience);
                most = Math.Max(most, c.Approach.Patience);
            }
        }
        Check(least >= 4 && least == StreetRules.PatienceLeast && most <= StreetRules.PatienceMost && most >= 8,
            "street talk, patience: anybody who stops gives at least four turns (night, unwashed, disliked included: " + least + "), up to " + most + " for somebody warm");
        StreetChat g = StreetChatOf(10001, StreetDay());
        int before = StreetRules.Turns(g);
        g.Gain(1);
        g.Gain(0);
        g.Gain(-2);
        Check(StreetRules.Turns(g) == Math.Min(StreetRules.PatienceMost, before + 1), "street talk, patience: a good exchange earns a turn back; a flat or bad one does not");
        for (int i = 0; i < 10; i++)
        {
            g.Gain(1);
        }
        Check(g.Earned == StreetRules.EarnedMost && StreetRules.Turns(g) <= StreetRules.PatienceMost, "street talk, patience: at most " + StreetRules.EarnedMost + " turns earned, " + StreetRules.PatienceMost + " in all");
        g.Turns = StreetRules.Turns(g);
        Check(StreetRules.Spent(g) && !StreetRules.Spent(StreetChatOf(10002, StreetDay())), "street talk, patience: once the turns are given they go");
    }

    private static void StreetChatWarmth()
    {
        StreetFacts day = StreetDay();
        day.Unwashed = true;
        day.Standing = 10;
        StreetFacts night = StreetDay(2);
        night.Unwashed = true;
        StreetFacts known = StreetDay();
        known.Unwashed = true;
        known.Record = true;
        known.Standing = 20;
        int dayLeast = 3, nightLeast = 3, knownLeast = 3, rudeLeast = 3, minusOne = 0;
        for (int k = 0; k < 2000; k++)
        {
            StreetStranger s = StreetRules.Identity(77, 12, 12000 + k, k % 2 == 0, 30, 6, -1);
            int w = StreetRules.Warmth(s, null, day, false);
            dayLeast = Math.Min(dayLeast, w);
            minusOne += w == -1 ? 1 : 0;
            nightLeast = Math.Min(nightLeast, StreetRules.Warmth(s, null, night, false));
            knownLeast = Math.Min(knownLeast, StreetRules.Warmth(s, null, known, true));
            rudeLeast = Math.Min(rudeLeast, StreetRules.Warmth(s, new StreetMemo { Rapport = -4 }, day, false));
        }
        Check(dayLeast == StreetRules.CoolestStranger && minusOne < 2000, "street talk, warmth: by day a stranger is at worst " + StreetRules.CoolestStranger
                                                                             + ", however unwashed and disliked the player is and however their day goes (" + minusOne + " of 2000 at it)");
        Check(nightLeast < -1 && knownLeast < -1 && rudeLeast < -1, "street talk, warmth: cooler than that only at night (" + nightLeast + "), when they know the player's face and record ("
                                                                    + knownLeast + ") or the talk went badly today (" + rudeLeast + ")");
        StreetFacts clean = StreetDay();
        int sumClean = 0, sumDirty = 0;
        StreetFacts dirty = StreetDay();
        dirty.Unwashed = true;
        for (int k = 0; k < 500; k++)
        {
            StreetStranger s = StreetRules.Identity(77, 12, 13000 + k, true, 30, 6, -1);
            sumClean += StreetRules.Warmth(s, null, clean, false);
            sumDirty += StreetRules.Warmth(s, null, dirty, false);
        }
        Check(sumDirty < sumClean, "street talk, warmth: not having washed still cools people (" + sumDirty + " against " + sumClean + " over 500)");
    }

    // ---------------------------------------------------------------- written lines

    private static void StreetChatLines()
    {
        bool several = true;
        string few = string.Empty;
        for (int i = 0; i < StreetLines.Lines.Length; i++)
        {
            if (StreetLines.Lines[i].Length < 3)
            {
                several = false;
                few += (StreetLine)i + " ";
            }
        }
        Check(several, "street talk, written lines: every kind has at least three lines (" + few + ")");
        var memo = new StreetMemo();
        var seen = new HashSet<string>();
        string last = null;
        bool fresh = true;
        string[] pool = StreetLines.Lines[(int)StreetLine.SmallWarm];
        for (int t = 0; t < pool.Length; t++)
        {
            string k = StreetLines.Pick(StreetLine.SmallWarm, 777, t % 2, last, memo.Lines);
            fresh &= seen.Add(k);
            memo.Said(k);
            last = k;
        }
        string again = StreetLines.Pick(StreetLine.SmallWarm, 777, 0, last, memo.Lines);
        Check(fresh && again == memo.Lines[0], "street talk, written lines: one person says every line of a kind before any comes round again that day, and then the one said longest ago ("
                                               + pool.Length + " lines)");
        var other = new StreetMemo();
        int same = 0;
        for (int k = 0; k < 50; k++)
        {
            same += StreetLines.Pick(StreetLine.Tired, 1000 + k, 3, null, other.Lines) == StreetLines.Pick(StreetLine.Tired, 2000 + k, 3, null, other.Lines) ? 1 : 0;
        }
        Check(same < 30, "street talk, written lines: two people do not all say the same line (" + same + " of 50 alike)");
        for (int k = 0; k < StreetLines.SaidMost + 5; k++)
        {
            memo.Said("x" + k);
        }
        Check(memo.Lines.Count == StreetLines.SaidMost, "street talk, written lines: a person remembers at most " + StreetLines.SaidMost + " lines a day");
    }

    // ---------------------------------------------------------------- the model's words

    private static void StreetChatSpoken()
    {
        Check(StreetCards.Spoken("What do you want, Sam?", "Sam") == "What do you want?" && StreetCards.Spoken("Hello, Sam. Nice to be stopped by a friendly face.", "Sam") == "Hello. Nice to be stopped by a friendly face."
              && StreetCards.Spoken("Sam, hello there.", "Sam") == "Hello there." && StreetCards.Spoken("I heard Sam runs a shop.", "Sam") == null
              && StreetCards.Spoken("What do you want, Sam?", null) == "What do you want, Sam?" && StreetCards.Spoken("Same here, really.", "Sam") == "Same here, really.",
            "street talk, the model's words: a stranger never calls the player by a name they were not told (said in passing it goes; otherwise the written line)");
        Check(StreetCards.Spoken("Sip & Savor, on 5th Avenue. off to your right, 2 minutes on foot.", null) == "Sip & Savor, on 5th Avenue. Off to your right, 2 minutes on foot."
              && StreetCards.Spoken("hm. not today.", null) == "Hm. Not today.", "street talk, the model's words: every sentence starts with a capital");
        var echoes = new StreetCards.Echoes();
        echoes.Add("Hello. Nice to be stopped by a friendly face.");
        bool heard = echoes.Heard("hello. nice to be stopped by a friendly face") && !echoes.Heard("Hello there.");
        for (int i = 0; i < StreetCards.Echoes.Most; i++)
        {
            echoes.Add("line " + i);
        }
        Check(heard && !echoes.Heard("Hello. Nice to be stopped by a friendly face."), "street talk, the model's words: a line another stranger said lately is not said again; after "
                                                                                     + StreetCards.Echoes.Most + " others it may be");
    }

    private static void StreetChatQueue()
    {
        var q = new TextQueue();
        bool refused = !q.Add(new TextJob { Key = "a", Priority = TextPriority.Now, Deadline = StreetRules.Hold }, 0);
        q.Expected(0.12);
        bool taken = q.Add(new TextJob { Key = "b", Priority = TextPriority.Now, Deadline = 10 + StreetRules.Hold }, 10);
        q.Expected(double.MaxValue);
        q.Expected(double.NaN);
        q.Expected(-1);
        Check(refused && taken && Math.Abs(q.Expect - 0.12) < 1e-9, "street talk, RLO Texts: a spoken line is refused for the queue's cautious first guess (" + TextQueue.StartGuess
                                                                    + " s) until the speed test's figure is set; then it is asked within the hold; a failed figure keeps the last");
        string texts = TextsSource(@"Texts\TextsDirector.cs");
        Check(texts.Contains("Hub.Queue.Expected(estimate);"), "street talk, RLO Texts: the speed test's figure is the queue's first expectation");
        string fixtures = File.ReadAllText(System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", "TextsCards.jsonl"), Encoding.UTF8);
        int three = 0;
        foreach (string line in fixtures.Split('\n'))
        {
            if (line.Contains("\"intent\": \"street.") && line.Contains("\"history\": [[") && line.Split(new[] { "], [" }, StringSplitOptions.None).Length >= 3)
            {
                three++;
            }
        }
        Check(three >= 10, "street talk, RLO Texts: the generator's own cards with the last three lines of a conversation are among the fixtures the builder matches byte for byte (" + three + ")");
    }

    // ---------------------------------------------------------------- by source

    private static void StreetChatSources()
    {
        string talk = TextsSource(@"Street\StreetDirector.Talk.cs"), game = TextsSource(@"Street\StreetGame.cs"), wiring = TextsSource(@"Wiring\StreetTexts.cs");
        Check(talk.Contains("entry.ConfirmTextOverride = Label(ask).Localize(data);") && talk.Contains("entry.SecondOptionTextOverride = \"reallife_social_btn_else\";")
              && talk.Contains("entry.CancelTextOverride = type ? StreetLines.Asks[(int)StreetAsk.Say] : StreetLines.Asks[(int)StreetAsk.Bye];")
              && talk.Contains("entry.OnCancel = type ? _onSayOrBye : _onGoodbye;") && StreetLines.Asks[(int)StreetAsk.Say] == "reallife_street_ask_say"
              && StreetLines.Asks[(int)StreetAsk.Bye] == "reallife_street_btn_goodbye",
            "street talk, the panel: the game's three buttons are the choice that fits, Something else, and Say something of your own (Goodbye where the game has no text box)");
        int say = talk.IndexOf("private void SayOrBye()", StringComparison.Ordinal);
        string body = say >= 0 ? talk.Substring(say, Math.Min(600, talk.Length - say)) : string.Empty;
        Check(body.Contains("if (StreetGame.CancelKey)") && body.Contains("Goodbye();") && body.Contains("StreetGame.AskWords(_onSaid, _onTyped)")
              && game.Contains("public static bool CancelKey => PlayerAction.Cancel.Pressed();"),
            "street talk, the panel: the game's cancel key (Esc) says goodbye; a click on the grey button opens the game's text box, one press away");
        Check(talk.Contains("if (Meta.Dev.Verbose)") && talk.Contains("TurnLine(kind, key, said, words != null);") && talk.Contains("Logger?.Info(\"Street talk turn \"")
              && talk.Contains("_turnSaid = \"chose \"") && talk.Contains("_turnSaid = \"typed \\\"\"") && wiring.Contains("c.Heard = said != null ? Card(r) + \", \" + Ms(r.Took)"),
            "street talk, the log: one line a turn with Developer diagnostics: what the player chose or typed, the line, where its words came from and why, warmth and rapport");
        Check(wiring.Contains("private const int HistoryMost = 3;") && wiring.Contains("History(c, r, HistoryMost, next);") && wiring.Contains("TextsDirector.Words.Expect < Street.StreetRules.Hold * AheadShare")
              && talk.Contains("string words = ahead && TakeAhead != null ? TakeAhead(_chat) : null;"),
            "street talk, RLO Texts: the card carries the last three lines; on a slow machine the blue button's answer is asked as it is shown and taken at the click");
        Check(talk.Contains("_chat.Menu = 0;") && talk.Contains("StreetRules.Choices(_chat, _facts, _choices);") && !talk.Contains("_chat.Rapport +="),
            "street talk: every round starts from what fits now; every turn's rapport goes through Gain (a good exchange earns a turn)");
    }
}
