using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

// Your own AI in each of the seven languages RLO Texts writes: the request carries the language (the card's lang line,
// the packs' system turn of data v6 and the language named as its natives write it, all the way into the body the
// service is sent); a reply in the language is shown as the service's; a reply in another language is refused by
// TextCheck and falls back to the pack when the pack writes that language, else to the written line. Dutch and Turkish,
// which TextCheck cannot judge, keep their written lines: neither the service nor the pack is asked. A language the mod
// has no texts in is asked in English, as the packs are. The pack and the service decide their languages each on its
// own; a spoken line still waits for the service, and shared text never goes to it.
internal static partial class Program
{
    // A reply of friend.crash in each language, its must fact (the street) kept as written.
    private static readonly Dictionary<string, string> ServiceGood = new Dictionary<string, string>
    {
        { "en", "Heard about the crash on 5th Ave. Are you all right?" },
        { "pt", "Soube do acidente na 5th Ave. Você está bem?" },
        { "zh-cn", "听说你在5th Ave出车祸了，你还好吗？" },
        { "de", "Ich habe von dem Unfall auf der 5th Ave gehört. Geht es dir gut?" },
        { "fr", "J'ai appris pour l'accident sur 5th Ave. Tu vas bien ?" },
        { "es", "Me he enterado del accidente en 5th Ave. ¿Estás bien?" },
        { "ru", "Слышал про аварию на 5th Ave. Ты в порядке?" }
    };

    private const string ServiceEnglish = "I heard about the crash on 5th Ave. Are you all right?";
    private const string ServiceGerman = "Ich habe von dem Unfall auf der 5th Ave gehört. Geht es dir gut?";
    private static readonly string[] LiteLanguages = { "en", "pt", "es" };   // the languages RLO Texts Lite declares (data 6)

    private static void ServiceEveryLanguage()
    {
        ServiceLanguageTurns();
        foreach (string lang in TextLanguages.All)
        {
            ServiceInLanguage(lang);
        }
        ServiceUnwritten();
        ServiceLanguageRoutes();
        ServiceLanguageBody();
    }

    // The system turns: English and an older card as the packs have them, each other language the packs' turn and one sentence naming it.
    private static void ServiceLanguageTurns()
    {
        var odd = new List<string>();
        for (int i = 1; i < TextLanguages.All.Length; i++)
        {
            string lang = TextLanguages.All[i], turn = TextCard.ServiceSystem(TextIntents.DataV6, lang);
            if (!turn.StartsWith(TextCard.SystemSix + " The lang line here is " + lang + ": write " + TextLanguages.Native(lang) + " as a native speaker", StringComparison.Ordinal)
                || !turn.EndsWith(TextLanguages.AddressOf(lang), StringComparison.Ordinal) || TextLanguages.AddressOf(lang).Length == 0
                || !ReferenceEquals(turn, TextCard.ServiceSystem(TextIntents.DataV6, lang)) || turn.IndexOf('!') >= 0 || turn.IndexOf('—') >= 0 || turn.IndexOf('–') >= 0
                || turn.Contains(" - "))
            {
                odd.Add(lang);
            }
        }
        Check(odd.Count == 0 && TextCard.ServiceSystem(TextIntents.DataV6, TextLanguages.English) == TextCard.SystemSix && TextCard.ServiceSystem(TextIntents.DataV5, "de") == TextCard.System
              && TextCard.ServiceSystem(TextIntents.DataV6, "nl") == TextCard.SystemSix && TextLanguages.Native("pt") == "Brazilian Portuguese" && TextLanguages.Native("nl") == null
              && TextLanguages.AddressOf("pt") == "Use você." && TextLanguages.AddressOf("en") == string.Empty,
            "your own AI, languages: the system turn is the packs' of the card's data; each of the six other languages adds the one sentence that names it as its natives write it "
            + "and how they address the player (the teacher's brief), made once, no dash or exclamation mark (" + (odd.Count == 0 ? "all six" : "not: " + string.Join(", ", odd)) + ")");
    }

    // One language: the request in it, its reply shown, a reply in another language refused and handed to the pack that writes it or to the written line.
    private static void ServiceInLanguage(string lang)
    {
        var packQ = new TextQueue();
        var serviceQ = new TextQueue();
        var hub = new TextsHub(packQ, serviceQ)
        {
            Runner = new PretendRunner(), Service = new PretendService(), Data = TextIntents.DataV6, Languages = LiteLanguages, Language = lang, English = lang == TextLanguages.English,
            Pack = "RLO Texts Lite"
        };
        bool packWrites = TextLanguages.Has(LiteLanguages, lang);
        string got = "(none)";
        TextRequest r = TextsAskOf("lang:" + lang + ":1", "friend.crash", null, w => got = w);
        r.Speaker = "f:" + lang + ":1";
        bool asked = hub.Ask(r, 0);
        TextJob job = serviceQ.Take(0.1, true);
        TextWebRequest body = job != null ? TextServiceRules.Build(TextProviders.Of(TextProvider.Groq), "https://api.groq.com/openai/v1", "a-model", job.System, job.Card, null, false, false) : null;
        Check(asked && job != null && packQ.Count == 0 && job.Card.EndsWith("\nlang: " + lang, StringComparison.Ordinal) && job.System == TextCard.ServiceSystem(TextIntents.DataV6, lang)
              && r.Lang == lang && hub.ServiceWrites && hub.Wants(TextUse.People, false) && body != null && body.Body.Contains("lang: " + lang)
              && (lang == TextLanguages.English || body.Body.Contains(TextLanguages.Native(lang) + " as a native speaker")),
            "your own AI in " + lang + ": the service is asked first, the card's lang line " + lang + ", the system turn of " + lang + ", both in the body it is sent");
        if (job != null)
        {
            serviceQ.Done(job, ServiceGood[lang], 0.8, null);
        }
        hub.Look(0.5);
        Check(got == ServiceGood[lang] && r.ByService && r.Source == TextSource.Service && hub.ServiceShown == 1,
            "your own AI in " + lang + ": a reply in " + lang + " passes TextCheck in " + lang + " and is shown as the service's (" + got + ")");

        got = "(none)";
        TextRequest wrong = TextsAskOf("lang:" + lang + ":2", "friend.crash", null, w => got = w);
        wrong.Speaker = "f:" + lang + ":2";
        hub.Ask(wrong, 2);
        job = serviceQ.Take(2.1, true);
        if (job != null)
        {
            serviceQ.Done(job, lang == TextLanguages.English ? ServiceGerman : ServiceEnglish, 0.8, null);
        }
        hub.Look(2.5);
        TextJob packJob = packQ.Take(2.6, true);
        string said = wrong.ServiceMiss ?? wrong.Why;   // (why the service's words were not used: the pack's words come with it, the written line with Why)
        bool refused = hub.Verdicts(TextVerdict.Language) == 1 && said != null && said.Contains("(Language)");
        if (packWrites)
        {
            Check(refused && got == "(none)" && packJob != null && packJob.Prompt.Contains("\nlang: " + lang + "<|im_end|>") && packJob.Prompt.Contains(TextCard.SystemSix) && wrong.Lang == lang,
                "your own AI in " + lang + ": a reply in another language is refused (Language) and the pack, which writes " + lang + ", is asked in " + lang);
            if (packJob != null)
            {
                packQ.Done(packJob, ServiceGood[lang], 0.4, null);
            }
            hub.Look(3.0);
            Check(got == ServiceGood[lang] && wrong.Source == TextSource.Pack && !wrong.ByService && hub.ServiceFell == 1,
                "your own AI in " + lang + ": the pack's words after the refusal are shown as the pack's");
        }
        else
        {
            Check(refused && got == null && packJob == null && packQ.Count == 0 && wrong.Source == TextSource.Written,
                "your own AI in " + lang + ": a reply in another language is refused (Language), and with a pack that does not write " + lang + " the written line is used");
        }
        hub.Clear();
    }

    // Dutch and Turkish: TextCheck cannot judge a reply in them, so neither the service nor any pack is asked; their written lines stay.
    private static void ServiceUnwritten()
    {
        Check(TextCheck.InLanguage(ServiceEnglish, "nl") && TextCheck.InLanguage(ServiceGerman, "tr") && TextCheck.InLanguage(ServiceEnglish, "tr")
              && !TextCheck.InLanguage(ServiceEnglish, "de") && !TextCheck.MachineIn("Ik ben een taalmodel en geen mens.", null, "nl")
              && !TextCheck.MachineIn("Ben bir yapay zekâyım, insan değilim.", null, "tr"),
            "your own AI, Dutch and Turkish: TextCheck cannot judge them (an English or German line passes as theirs, a speaker calling itself a machine in them is not caught), "
            + "which is why neither is asked; give TextCheck their rules before letting either in");
        foreach (string lang in TextLanguages.Unwritten)
        {
            var packQ = new TextQueue();
            var serviceQ = new TextQueue();
            var hub = new TextsHub(packQ, serviceQ)
            {
                Runner = new PretendRunner(), Service = new PretendService(), Data = TextIntents.DataV6, Languages = TextLanguages.All, Language = TextLanguages.Of(lang), English = false,
                Pack = "RLO Texts"
            };
            bool called = false;
            TextRequest r = TextsAskOf("un:" + lang, "friend.crash", null, w => called = true);
            r.Spoken = true;
            bool asked = hub.Ask(r, 0);
            string why = hub.ServiceWhy(TextUse.People, false, "friend.crash", null);
            var shown = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
            hub.Tally.Totals("RLO Texts", shown, new int[Enum.GetValues(typeof(TextVerdict)).Length]);
            Check(!asked && !called && serviceQ.Count == 0 && packQ.Count == 0 && !hub.ServiceWrites && !hub.WritesLanguage && !hub.Wants(TextUse.People, false)
                  && why != null && why.Contains("not asked in " + lang) && r.Why != null && r.Why.Contains("cannot check") && shown[(int)TextShownWhy.Language] == 1
                  && hub.WhyNot(TextUse.People, false, "friend.crash", null, TextPriority.Soon, false, 30) != null,
                "your own AI in " + lang + ": neither the service nor a pack (even one that claims all seven) is asked, the written line is used and counted as the language (" + why + ")");
        }
    }

    // The routes around the language: a language the mod has no texts in, the pack and the service each on its own, a spoken line, shared text.
    private static void ServiceLanguageRoutes()
    {
        var packQ = new TextQueue();
        var serviceQ = new TextQueue();
        var hub = new TextsHub(packQ, serviceQ)
        {
            Runner = new PretendRunner(), Service = new PretendService(), Data = TextIntents.DataV6, Languages = LiteLanguages, Language = TextLanguages.Of("ja"), English = false,
            Pack = "RLO Texts Lite"
        };
        TextRequest other = TextsAskOf("ja:1", "friend.crash", null, w => { });
        bool asked = hub.Ask(other, 0);
        TextJob job = serviceQ.Take(0.1, true);
        Check(asked && job != null && job.Card.EndsWith("\nlang: en", StringComparison.Ordinal) && job.System == TextCard.SystemSix && other.Lang == TextLanguages.English,
            "your own AI, languages: a game in a language the mod has no texts in (it reads English) is asked in English, as the packs are");
        hub.Clear();

        var old = new TextsHub(new TextQueue()) { Runner = new PretendRunner(), Data = TextIntents.DataV5, Language = TextLanguages.Of("ja"), English = false, Pack = "RLO Texts" };
        Check(!old.Ask(TextsAskOf("ja:2", "friend.crash", null, w => { }), 0) && !old.WritesLanguage && old.Queue.Count == 0,
            "your own AI, languages: a pack of data 5 or older writes only while the game is in English, as before");

        hub.Language = "ru";
        hub.Service = null;
        TextRequest alone = TextsAskOf("ru:1", "friend.crash", null, w => { });
        Check(!hub.Wants(TextUse.People, false) && !hub.Ask(alone, 1) && packQ.Count == 0 && alone.Why.Contains("does not write the game's language"),
            "your own AI, languages: without the service, a pack that does not write Russian is not asked in a Russian game (the written line)");
        hub.Service = new PretendService();
        serviceQ.Expected(1.5);
        TextRequest spoken = TextsAskOf("ru:2", "friend.crash", null, w => { }, TextPriority.Now, 1.0);
        spoken.Spoken = true;
        Check(hub.Wants(TextUse.People, false) && hub.Ask(spoken, 2) && serviceQ.Count == 1 && packQ.Count == 0 && spoken.Lang == "ru"
              && Math.Abs(spoken.Waits - TextsHub.SpokenWait(1.0, serviceQ.Expect, 0.0)) < 1e-9 && spoken.Waits > spoken.Seconds,
            "your own AI, languages: beside a pack that writes no Russian the service words a Russian game's lines, and a spoken line waits for it ("
            + spoken.Waits.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s), nothing kept for a pack that cannot take it");
        hub.Clear();

        hub.Language = "es";
        TextRequest shared = TextsAskOf("fp:es", "friend.crash", null, w => { });
        shared.Shared = true;
        Check(hub.Ask(shared, 3) && serviceQ.Count == 0 && packQ.Count == 1 && shared.Lang == "es" && hub.ServiceWhy(TextUse.People, true, "friend.crash", null).Contains("shared text never goes"),
            "your own AI, languages: shared text in Spanish goes to the pack that writes Spanish, never the service");
        hub.Clear();
    }

    // The runner sends the job's own system turn with its card; a job without one keeps the trained turn of data 5 and before.
    private static void ServiceLanguageBody()
    {
        var logs = new List<string>();
        Action<string> log = s => { lock (logs) { logs.Add(s); } };
        TextProviderInfo groq = TextProviders.Of(TextProvider.Groq);
        var web = new PretendWeb { Answer = (rq, ms) => Ans(200, "{\"choices\":[{\"message\":{\"content\":\"Geht es dir gut?\"}}]}") };
        var q = new TextQueue();
        var run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey, Workers = 1 }, web, log);
        run.Start();
        var german = new TextJob { Key = "de", Card = "who: Dana\nlang: de", System = TextCard.ServiceSystem(TextIntents.DataV6, "de"), Priority = TextPriority.Soon, Deadline = TextClock.Now + 20 };
        var plain = new TextJob { Key = "v5", Card = "who: Dana", Priority = TextPriority.Soon, Deadline = TextClock.Now + 20 };
        q.Add(german, TextClock.Now);
        q.Add(plain, TextClock.Now);
        run.Nudge();
        bool both = Until(() => german.State == TextJobState.Written && plain.State == TextJobState.Written, 5000);
        string germanBody = null, plainBody = null;
        lock (web.Sent)
        {
            foreach (TextWebRequest sent in web.Sent)
            {
                if (sent.Body.Contains("lang: de"))
                {
                    germanBody = sent.Body;
                }
                else
                {
                    plainBody = sent.Body;
                }
            }
        }
        run.Dispose();
        Check(both && germanBody != null && germanBody.Contains("German as a native speaker") && germanBody.Contains("Use Sie with a stranger")
              && germanBody.Contains("Write in the language the lang line names") && plainBody != null && plainBody.Contains("Plain British English, no dashes.") && !plainBody.Contains("lang line"),
            "your own AI, the runner: the service is sent each job's own system turn (German's for a German card), and a job without one the trained turn as before");
    }
}
