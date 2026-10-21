using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

// Your own AI beside RLO Texts' data v6: the service is given the newest card finished as a pack's would be, in the game's
// language (its lang line, the speaker's memory), and its words are checked in that language; after a miss of the
// service, a pack of data v6 that writes the language gets its own card, its system turn, grammar and language. And the
// mod's languages: Dutch and Turkish, whose texts are the mod's own and which no pack writes, keep their written lines
// (TextLanguages.Unwritten), and every language of the mod is one of the two kinds. The service in each of the seven
// languages is in TextsServiceLanguageChecks.cs.
internal static partial class Program
{
    private static void TextsServiceSix()
    {
        ServiceSixLanguages();
        ServiceSixRoute();
        ServiceEveryLanguage();
    }

    private static void ServiceSixLanguages()
    {
        string[] claimed = TextLanguages.Parse("en,pt,zh-cn,de,fr,es,ru,nl,tr");
        Check(TextLanguages.Of("nl") == "nl" && TextLanguages.Of("TR") == "tr" && TextLanguages.Of("ja") == TextLanguages.English && TextLanguages.Of("pt") == "pt"
              && !TextLanguages.Known("nl") && !TextLanguages.Known("tr") && claimed.Length == TextLanguages.All.Length
              && !TextLanguages.Has(claimed, TextLanguages.Of("nl")) && !TextLanguages.Has(claimed, TextLanguages.Of("tr")) && TextLanguages.Has(claimed, TextLanguages.Of("ja")),
            "texts v6, the languages: a game in Dutch or Turkish (the mod's own texts) is never a pack's language, not even for a pack that claims it, so its written lines stay; a language the mod has no texts in reads English, as the mod does");
        var odd = new List<string>();
        foreach (string code in RealLifeOverhaul.ModLanguages.All)
        {
            bool written = TextLanguages.Known(code), unwritten = Array.IndexOf(TextLanguages.Unwritten, code) >= 0;
            if (written == unwritten || TextLanguages.Of(code) != code)
            {
                odd.Add(code);
            }
        }
        foreach (string code in TextLanguages.All)
        {
            if (Array.IndexOf(RealLifeOverhaul.ModLanguages.All, code) < 0)
            {
                odd.Add(code + " (not a language of the mod)");
            }
        }
        foreach (string code in TextLanguages.Unwritten)
        {
            if (Array.IndexOf(RealLifeOverhaul.ModLanguages.All, code) < 0)
            {
                odd.Add(code + " (not a language of the mod)");
            }
        }
        Check(odd.Count == 0, "texts v6, the languages: every language of the mod (" + string.Join(", ", RealLifeOverhaul.ModLanguages.All)
                              + ") is one RLO Texts writes or one that keeps its written lines, never both or neither (" + (odd.Count == 0 ? "all are" : "not: " + string.Join(", ", odd)) + ")");
    }

    private static void ServiceSixRoute()
    {
        var packQ = new TextQueue();
        var serviceQ = new TextQueue();
        var service = new PretendService();
        var hub = new TextsHub(packQ, serviceQ) { Runner = new PretendRunner(), Service = service, Data = TextIntents.DataV6, Languages = new[] { "en", "de" }, Language = "en" };
        string got = "(none)";
        TextRequest r = TextsAskOf("six:1", "friend.crash", null, w => got = w);
        r.Memory.Add(new TextFact(TextMemoryKinds.Talked, "street", 2));
        bool asked = hub.Ask(r, 0);
        TextJob job = serviceQ.Take(0.1, true);
        TextIntent intent = TextIntents.Find("friend.crash", null, TextsHub.Newest);
        var sheet = new TextSheet();
        TextIntents.Build(intent, r.Who, r.Slots, r.History, sheet);
        TextsHub.Finish(intent, sheet, TextLanguages.English, r.Memory, r.Day, false);
        Check(asked && intent.Data == TextIntents.DataV6 && job != null && job.Card == TextCard.Card(sheet) && job.Card.EndsWith("lang: en", StringComparison.Ordinal)
              && r.Lang == TextLanguages.English && packQ.Count == 0,
            "your own AI with data v6: the service is given the newest card finished as a pack's would be in English, its lang line and the speaker's memory");
        if (job != null)
        {
            serviceQ.Done(job, "Heard about the crash on 5th Ave. Are you all right?", 0.8, null);
        }
        hub.Look(1.0);
        Check(got == "Heard about the crash on 5th Ave. Are you all right?" && r.ByService && hub.ServiceShown == 1,
            "your own AI with data v6: the service's words are checked in English and shown as the service's");

        hub.Language = "de";
        hub.English = false;
        TextRequest german = TextsAskOf("six:2", "friend.crash", null, w => { });
        bool germanAsked = hub.Ask(german, 2);
        TextJob germanJob = serviceQ.Take(2.1, true);
        string why = hub.ServiceWhy(TextUse.People, false, "friend.crash", null);
        Check(germanAsked && germanJob != null && packQ.Count == 0 && germanJob.Card.EndsWith("lang: de", StringComparison.Ordinal) && german.Lang == "de" && why == null,
            "your own AI with data v6: with the game in German the service is asked first, its card's lang line German");
        if (germanJob != null)
        {
            serviceQ.Done(germanJob, null, 0.1, "the check's");
        }
        hub.Look(2.2);
        TextJob packJob = packQ.Take(2.3, true);
        Check(packJob != null && packJob.Prompt.Contains(TextCard.SystemSix) && packJob.Prompt.Contains("lang: de") && packJob.Grammar != null && german.Lang == "de" && !german.ByService,
            "your own AI with data v6: when the service misses in German, the pack that writes German is asked in German, with its own system turn and grammar");
        if (packJob != null)
        {
            packQ.Done(packJob, null, 0.1, "the check's");
        }
        hub.Look(2.4);
        hub.Clear();
        hub.English = true;

        hub.Language = "en";
        got = "(none)";
        TextRequest refused = TextsAskOf("six:3", "friend.crash", null, w => got = w);
        hub.Ask(refused, 10);
        job = serviceQ.Take(10.1, true);
        if (job != null)
        {
            serviceQ.Done(job, "Heard about 5th Ave! Wow!", 0.5, null);
        }
        hub.Look(10.7);
        TextJob fallback = packQ.Take(10.8, true);
        Check(got == "(none)" && fallback != null && fallback.Prompt.Contains(TextCard.SystemSix) && fallback.Prompt.Contains("lang: en") && fallback.Grammar != null && fallback.Tokens > 0
              && refused.Lang == TextLanguages.English,
            "your own AI with data v6: words the checker refuses send the request to the pack of data v6 with its own card, system turn, grammar and length");
        if (fallback != null)
        {
            packQ.Done(fallback, "Heard about 5th Ave. Are you all right?", 0.4, null);
        }
        hub.Look(11.3);
        Check(got == "Heard about 5th Ave. Are you all right?" && !refused.ByService && refused.Source == TextSource.Pack,
            "your own AI with data v6: the pack's words after the service's miss are shown as the pack's");
        hub.Clear();
    }
}
