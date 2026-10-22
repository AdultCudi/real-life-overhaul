using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// Faults found in Scripts/Texts and Street: a raise first seen on a look that texted something else (off sick, a raise
// asked for) was marked seen and never thanked; "what's good around here" could name the same chain twice ("X and X");
// each web request's cancellation source was never let go; the packs found off the game's thread were said found with no
// barrier; and a request's Done that asked again at once (the street's next button asked ahead) could end others in the
// list the hub's look was walking, so the walk skipped one or ran past the list's end.
internal static partial class Program
{
    private static void TextsStreetFixes()
    {
        TextsRaiseThanks();
        StreetNearestNames();
        TextsThreads();
        TextsHubAskAgain();
        Check(FixLineEverywhere("staff thank you for a raise even when", "os funcionários agradecem um aumento mesmo", "他们之后也会为加薪道谢", "Angestellte bedanken sich für eine Gehaltserhöhung",
                                "vos employés vous remercient pour une augmentation", "tus empleados te agradecen un aumento", "сотрудники благодарят вас за прибавку",
                                "je personeel bedankt je voor een loonsverhoging", "size teşekkür ediyor"),
            "texts fix: What's new has the raise's thanks line, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("a stranger never names the same business twice", "um desconhecido nunca cita a mesma empresa duas vezes", "陌生人不会把同一家店说两次",
                                "nennt ein Fremder nie zweimal dasselbe Geschäft", "un inconnu ne cite jamais deux fois la même entreprise",
                                "un desconocido nunca nombra dos veces el mismo negocio", "незнакомец никогда не называет одно и то же заведение дважды",
                                "noemt een vreemde nooit twee keer hetzelfde bedrijf", "aynı işletmeyi asla iki kez söylemiyor"),
            "street fix: What's new has the line of the two names, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("RLO Texts keeps going when a street conversation's next answer", "o RLO Texts continua funcionando quando a próxima resposta",
                                "RLO Texts 会照常运行", "RLO Texts läuft weiter, wenn die nächste Antwort", "RLO Texts continue de fonctionner quand la réponse suivante",
                                "RLO Texts sigue funcionando cuando la siguiente respuesta", "RLO Texts продолжает работать, если следующий ответ",
                                "RLO Texts blijft werken als het volgende antwoord", "RLO Texts çalışmaya devam ediyor"),
            "texts fix: What's new has the hub's line, with its fix mark, in some row of every language");
    }

    private static void TextsRaiseThanks()
    {
        var s = new TextStaff { Id = "e1", Name = "Dana Reyes", First = "Dana", Paid = 1500, Going = 1700, Raise = 100, MostHours = 40, WeekHours = 38 };
        var m = new TextStaffMark { Id = "e1" };
        TextsCityRules.Staff(s, m, 10, 10);   // first sight: notes the raise from before
        s.Raise = 200;
        s.Sick = true;
        TextStaffSay sick = TextsCityRules.Staff(s, m, 11, 7);
        s.Sick = false;
        TextStaffSay thanks = TextsCityRules.Staff(s, m, 12, 10);
        TextStaffSay once = TextsCityRules.Staff(s, m, 13, 10);
        Check(sick == TextStaffSay.Sick && thanks == TextStaffSay.RaiseThanks && once == TextStaffSay.None && m.RaiseSeen == 200,
            "texts fix: a raise first seen the morning they are off sick is thanked for on a later look, once (" + sick + ", " + thanks + ", " + once + ")");
        s.Raise = 300;
        s.AskDay = 14;
        s.AskTo = 1900;
        TextStaffSay ask = TextsCityRules.Staff(s, m, 14, 10);
        TextStaffSay later = TextsCityRules.Staff(s, m, 15, 10);
        Check(ask == TextStaffSay.RaiseAsk && later == TextStaffSay.RaiseThanks && TextsCityRules.Staff(s, m, 16, 10) == TextStaffSay.None,
            "texts fix: a raise first seen on the day they ask for one is thanked for the next day, once (" + ask + ", " + later + ")");
        s.Raise = 150;
        TextsCityRules.Staff(s, m, 17, 10);
        s.Raise = 250;
        Check(m.RaiseSeen == 150 && TextsCityRules.Staff(s, m, 18, 10) == TextStaffSay.RaiseThanks,
            "texts fix: a raise taken back owes nothing, and the next one is thanked from there");
        s.Raise = 400;
        TextStaffSay night = TextsCityRules.Staff(s, m, 19, 22);
        Check(night == TextStaffSay.None && m.RaiseSeen == 250 && TextsCityRules.Staff(s, m, 19, 10) == TextStaffSay.RaiseThanks,
            "texts fix: a raise seen out of business hours waits for them, as before");
    }

    private static void StreetNearestNames()
    {
        string first = null, second = null;
        float firstSqr = float.MaxValue, secondSqr = float.MaxValue;
        StreetRules.NearestTwo("Bean There", 900f, ref first, ref firstSqr, ref second, ref secondSqr);
        StreetRules.NearestTwo("Bean There", 100f, ref first, ref firstSqr, ref second, ref secondSqr);   // a nearer branch
        Check(first == "Bean There" && second == null && firstSqr == 100f, "street fix: two branches of a chain are one name, never \"X and X\"");
        StreetRules.NearestTwo("Sam's Deli", 400f, ref first, ref firstSqr, ref second, ref secondSqr);
        StreetRules.NearestTwo("Rocket Burger", 2500f, ref first, ref firstSqr, ref second, ref secondSqr);
        Check(first == "Bean There" && second == "Sam's Deli" && secondSqr == 400f, "street fix: the next nearest of a different name is the second");
        StreetRules.NearestTwo("Sam's Deli", 25f, ref first, ref firstSqr, ref second, ref secondSqr);   // a nearer Deli than the cafe
        Check(first == "Sam's Deli" && second == "Bean There" && firstSqr == 25f && secondSqr == 100f, "street fix: a nearer branch of the second name puts it first, the other second");
        StreetRules.NearestTwo("Bean There", 10f, ref first, ref firstSqr, ref second, ref secondSqr);
        StreetRules.NearestTwo(null, 1f, ref first, ref firstSqr, ref second, ref secondSqr);
        StreetRules.NearestTwo(string.Empty, 1f, ref first, ref firstSqr, ref second, ref secondSqr);
        Check(first == "Bean There" && second == "Sam's Deli" && first != second, "street fix: the nearest first whichever branch came first; a business with no name is passed over");
        string game = ModSource(@"Street\StreetGame.cs");
        string around = MemberText(game, "public static int Around(Vector3 from, string[] into)");
        Check(around.Contains("StreetRules.NearestTwo(reg.BusinessName, ") && !around.Contains("into[1] = into[0]"),
            "street fix: \"what's good around here\" keeps its two names by the one rule");
    }

    private static void TextsThreads()
    {
        string web = ModSource(@"Texts\TextWeb.cs");
        string send = MemberText(web, "public TextWebAnswer Send(TextWebRequest request, int timeoutMs)");
        Check(send.Contains("finally") && send.Contains("cancel.Dispose();") && send.Contains("task.ContinueWith(LetGo, cancel, ")
              && web.Contains("private static readonly Action<Task, object> LetGo = (task, cancel) => ((CancellationTokenSource)cancel).Dispose();"),
            "texts fix: every web request's cancellation source is let go when the request ends, a request given up on once it stops");
        string director = ModSource(@"Texts\TextsDirector.cs");
        string find = MemberText(director, "private static void Find()");
        int found = find.IndexOf("_found = true;", StringComparison.Ordinal);
        Check(director.Contains("private static volatile bool _found;") && found > find.IndexOf("_devices = new List<TextDevice>();", StringComparison.Ordinal)
              && found > find.IndexOf("Verified.AddRange(verified);", StringComparison.Ordinal) && !Regex.IsMatch(director, @"private static bool [^;]*\b_found\b"),
            "texts fix: the packs found off the game's thread are said found last, by a volatile write the game's thread reads first");
    }

    private static void TextsHubAskAgain()
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner() };
        int dones = 0;
        bool askedAgain = false;
        hub.Ask(TextsAskOf("ahead", "friend.crash", null, w => dones++), 0);
        hub.Ask(TextsAskOf("k2", "friend.crash", null, w => dones++), 0);
        // Its Done asks again at once with the first one's key and is refused (no time left): the first gives way, the list
        // loses two while the look walks it from the end.
        hub.Ask(TextsAskOf("k3", "friend.crash", null, w =>
        {
            dones++;
            askedAgain = !hub.Ask(TextsAskOf("ahead", "friend.crash", null, x => { }, TextPriority.Soon, -1), 0.6);
        }), 0);
        for (int n = 0; n < 3; n++)
        {
            TextJob job = q.Take(0.1, true);
            if (job != null && job.Key == "k3")
            {
                q.Done(job, ": Heard about 5th Ave. You alright?", 1.0, null);
            }
        }
        string fault = null;
        try
        {
            hub.Look(0.5);
        }
        catch (Exception e)
        {
            fault = e.GetType().Name;
        }
        Check(fault == null && askedAgain && dones == 2 && hub.Pending == 1,
            "texts fix: a Done that asks again at once during the look neither breaks the look nor loses a request (" + (fault ?? "no fault") + ", " + dones + " Done, "
            + hub.Pending + " waiting)");
        string look = ModSource(@"Texts\TextsHub.Look.cs"), core = ModSource(@"Texts\TextsHub.cs");
        Check(Regex.Matches(look + core, @"\bPass\(\);").Count == 4 && Regex.Matches(look + core, @"\bPassed\(\);").Count == 4
              && MemberText(look, "private void End(int i, string words, bool counted, TextShownWhy why, bool unseen = false)").Contains("_held.Add(r);"),
            "texts fix: every walk over the waiting requests (the look, giving way, the flush, the clear) holds their Dones until it is over");
    }
}
