using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Texts;

// Checks for RLO Texts in every language of the mod (data v6): the seven languages and the game's code for each, a pack's
// languages line, a line's length by its script, the tokens a reply may take; the checker in each language, each rule made
// to fail on a line that breaks it and a good line passing in all seven (capitals where the script has them, "a" and "an"
// in English only, "!", its full-width and Spanish's opening forms, the length by the language, the language itself, the
// must facts as each language can carry them, the name kept out); the tidier keeping each language's opening marks; the
// hub asking a data 6 pack in the game's language and counting the written line when the pack does not write it; and
// Going Public's shared text with its language (an older RLO passing over it, a guest keeping only its own language's).
internal static partial class Program
{
    private static void TextsLanguagesSix()
    {
        // the languages
        Check(TextLanguages.Of("pt") == "pt" && TextLanguages.Of("PT") == "pt" && TextLanguages.Of("zh-CN") == "zh-cn" && TextLanguages.Of("ja") == "en" && TextLanguages.Of(null) == "en"
              && TextLanguages.Of(string.Empty) == "en" && TextLanguages.All.Length == 7,
            "texts v6, languages: the card's language is the game's when it is one of the seven, English for any other or none");
        Check(string.Join(",", TextLanguages.Parse("en,pt, zh-cn")) == "en,pt,zh-cn" && string.Join(",", TextLanguages.Parse("ru,en")) == "en,ru"
              && string.Join(",", TextLanguages.Parse("de,xx,DE")) == "de" && string.Join(",", TextLanguages.Parse(null)) == "en" && string.Join(",", TextLanguages.Parse("ja")) == "en",
            "texts v6, languages: a pack's languages line read in the mod's order, unknown codes and repeats left out, English alone without one");
        Check(TextLanguages.Has(new[] { "en", "pt" }, "pt") && !TextLanguages.Has(new[] { "en" }, "pt") && !TextLanguages.Has(null, "en") && TextLanguages.Cased("ru") && !TextLanguages.Cased("zh-cn"),
            "texts v6, languages: a pack writes the languages it names; capitals in every language but Chinese");
        Check(TextLanguages.Units("abc") == 3 && TextLanguages.Units("中文") == 4 && TextLanguages.Units("Ｈｉ") == 4 && TextLanguages.Units("Привет") == 6 && TextLanguages.Units(null) == 0,
            "texts v6, length: a character one unit, a wide one (Chinese, the full-width forms) two");
        Check(TextLanguages.MostUnits(200, "en") == 200 && TextLanguages.MostUnits(200, "pt") == 240 && TextLanguages.MostUnits(200, "ru") == 240 && TextLanguages.MostUnits(200, "zh-cn") == 200
              && TextLanguages.MostUnits(140, "de") == 168,
            "texts v6, length: English's most in English, a fifth more in Portuguese, German, French, Spanish and Russian, the same units in Chinese");
        Check(TextLanguages.Tokens(200, "en") == 82 && TextLanguages.Tokens(240, "pt") == 108 && TextLanguages.Tokens(240, "ru") == 125 && TextLanguages.Tokens(200, "zh-cn") == 116,
            "texts v6, tokens: a reply's n_predict from its most units by the language, with room to end its sentence");
        Check(TextLanguages.OwnLetter('д', TextScript.Cyrillic) && !TextLanguages.OwnLetter('д', TextScript.Latin) && TextLanguages.OwnLetter('中', TextScript.Han)
              && TextLanguages.OwnLetter('S', TextScript.Han) && !TextLanguages.OwnLetter('中', TextScript.Cyrillic) && TextLanguages.HasOwnScript("Привет, Alex", TextScript.Cyrillic)
              && !TextLanguages.HasOwnScript("Hello, Alex", TextScript.Cyrillic) && TextLanguages.HasOwnScript("你好", TextScript.Han),
            "texts v6, scripts: each language's own letters, Latin names in any, the own script there for Russian and Chinese");

        // the checker in each language, on friend.crash of data v6 ("{first} heard you crashed your car on {street}"; must: the street)
        TextIntent crash = TextIntents.Find("friend.crash", null, TextIntents.DataV6);
        var who = new TextWho { Name = "Dana Reyes", First = "Dana", Age = 30, Job = "nurse", District = "Midtown", Role = "a friend, met at work", Player = "Alex", When = "Monday 10:00" };
        var slots = new TextSlots();
        slots.Set("street", "5th Ave");
        var sheet = new TextSheet();
        bool built = crash != null && crash.Data == TextIntents.DataV6 && TextIntents.Build(crash, who, slots, null, sheet);
        string card = built ? TextCard.Card(sheet) : string.Empty;
        TextVerdict J(string words, string lang, bool cut = false, string untold = null, bool lower = false, IReadOnlyList<string> recent = null)
        {
            return TextCheck.JudgeSix(words, crash, sheet, card, recent, lang, cut, untold, lower);
        }
        var good = new Dictionary<string, string>
        {
            { "en", "Heard about the crash on 5th Ave. Are you all right?" },
            { "pt", "Soube do acidente na 5th Ave. Você está bem?" },
            { "zh-cn", "听说你在5th Ave出了车祸。你还好吗？" },
            { "de", "Ich habe von dem Unfall auf der 5th Ave gehört. Geht es dir gut?" },
            { "fr", "J'ai appris pour l'accident sur 5th Ave. Tu vas bien ?" },
            { "es", "Me enteré del accidente en 5th Ave. ¿Estás bien?" },
            { "ru", "Слышала про аварию на 5th Ave. Ты в порядке?" }
        };
        var passed = new List<string>();
        foreach (KeyValuePair<string, string> g in good)
        {
            TextVerdict v = J(g.Value, g.Key);
            passed.Add(g.Key + " " + v);
        }
        Check(built && passed.TrueForAll(p => p.EndsWith(" Ok", StringComparison.Ordinal)), "texts v6, checker: a good reply passes in each of the seven languages (" + string.Join(", ", passed) + ")");
        Check(J("Heard about the crash on 5th Ave! Are you all right?", "en") == TextVerdict.Exclaim && J("听说你在5th Ave出了车祸！你还好吗", "zh-cn") == TextVerdict.Exclaim
              && J("Me enteré del accidente en 5th Ave. ¡Cuídate.", "es") == TextVerdict.Exclaim,
            "texts v6, checker: '!', its full-width form and Spanish's opening one refused in every language and for every voice");
        Check(J("soube do acidente na 5th Ave. Você está bem?", "pt") == TextVerdict.Capital && J("Soube do acidente na 5th Ave. você está bem?", "pt") == TextVerdict.Capital
              && J("soube do acidente na 5th Ave. você está bem?", "pt", false, null, true) == TextVerdict.Ok && J("听说你在5th Ave出了车祸。你还好吗？", "zh-cn", false, null, false) == TextVerdict.Ok
              && J("Me enteré del accidente en 5th Ave. ¿estás bien?", "es") == TextVerdict.Capital,
            "texts v6, checker: a sentence starting with a small letter refused where the script has capitals, unless the style is lower case; never in Chinese");
        Check(J("Heard about a accident on 5th Ave. Are you all right?", "en") == TextVerdict.Article && J("Soube do acidente na 5th Ave, a Ana contou. Está tudo bem?", "pt") == TextVerdict.Ok,
            "texts v6, checker: 'a' and 'an' checked in English only");
        Check(J("I heard about the crash on 5th Ave. Are you all right?", "pt") == TextVerdict.Language && J("Heard about the crash on 5th Ave, you ok?", "ru") == TextVerdict.Language
              && J("Слышала про аварию на 5th Ave. Ты в порядке?", "zh-cn") == TextVerdict.Language && J("J'ai appris pour l'accident sur 5th Ave. Tu vas bien ?", "de") == TextVerdict.Language,
            "texts v6, checker: a reply in another language or script than the card's refused");
        string longZh = "听说你在5th Ave出了车祸" + new string('好', 100);
        string longEn = "Heard about the crash on 5th Ave and I hope you are fine because that road is awful in the rain and everybody says the lights there are slow and drivers never look at all";
        string longPt = "Soube do acidente na 5th Ave e espero que você esteja bem porque essa rua é horrível na chuva e todo mundo diz que os sinais lá são lentos e os motoristas nunca olham direito quando passam por lá de manhã cedo";
        Check(J(longZh, "zh-cn") == TextVerdict.Long && J(longEn + " " + longEn, "en") == TextVerdict.Long && longPt.Length > 200 && longPt.Length <= 240 && J(longPt, "pt") == TextVerdict.Ok,
            "texts v6, checker: the length by the language (" + TextLanguages.Units(longZh) + " units of Chinese too many for 200; " + longPt.Length + " characters of Portuguese within 240)");
        Check(J("Soube do acidente. Você está bem?", "pt") == TextVerdict.Must && J("听说你出了车祸。你还好吗？", "zh-cn") == TextVerdict.Must,
            "texts v6, checker: a place the card must name, missing in another language, refused (a name is the same in every language)");
        Check(TextSlotKinds.Needed("Friday 19:30", TextSlotKind.Time, "pt") == "19:30" && TextSlotKinds.Needed("Friday 19:30", TextSlotKind.Time, "en") == "Friday 19:30"
              && TextSlotKinds.Needed("Monday", TextSlotKind.Day, "ru") == null && TextSlotKinds.Needed("Thanksgiving", TextSlotKind.Text, "fr") == null
              && TextSlotKinds.Needed("the park", TextSlotKind.Place, "pt") == null && TextSlotKinds.Needed("the park", TextSlotKind.Place, "en") == "the park"
              && TextSlotKinds.Needed("The Tipsy Fox", TextSlotKind.Place, "es") == "The Tipsy Fox" && TextSlotKinds.Needed("$400", TextSlotKind.Number, "zh-cn") == "$400",
            "texts v6, checker: a must fact as each language carries it: a time's clock, a day and English words said in the reply's own, a place of common words too, a name and a sum as written");
        Check(J("Hi Alex, heard about the crash on 5th Ave. Are you all right?", "en", false, "Alex") == TextVerdict.Name && J("Heard about the crash on 5th Ave. Alexandra said so.", "en", false, "Alex") == TextVerdict.Ok
              && J("Hi Alex, heard about the crash on 5th Ave. Are you all right?", "en") == TextVerdict.Ok,
            "texts v6, checker: the player's name refused from somebody the card says does not know it, as a whole word");
        Check(J("Sou uma IA, mas soube do acidente na 5th Ave.", "pt") == TextVerdict.Machine && J("Я бот, но слышал про аварию на 5th Ave.", "ru") == TextVerdict.Machine
              && J("我是人工智能，听说你在5th Ave出了车祸。", "zh-cn") == TextVerdict.Machine,
            "texts v6, checker: the speaker calling itself a machine refused in its own language");
        Check(J("Heard about the crash on 5th Ave. Are you all right?", "en", true) == TextVerdict.Cut && J("Heard about the crash on 5th Ave." + (char)0x2028 + "Are you all right?", "en") == TextVerdict.Lines
              && J("Soube do acidente na 5th Ave " + (char)0x2014 + " você está bem?", "pt") == TextVerdict.Dash,
            "texts v6, checker: a reply cut at its most tokens, a line break of any kind and a dash refused");
        Check(J("Soube do acidente na 5th Ave. Você está bem?", "pt", false, null, false, new[] { "Soube do acidente na 5th Ave. Você está bem?" }) == TextVerdict.Repeat
              && J("听说你在5th Ave出了车祸。你还好吗？", "zh-cn", false, null, false, new[] { "听说你在5th Ave出了车祸。你还好吗？" }) == TextVerdict.Repeat,
            "texts v6, checker: a repeat of the speaker's own words refused, in Chinese by its pairs of characters");
        Check(TextCheck.Tidy(": ¿Estás bien?", "es") == "¿Estás bien?" && TextCheck.Tidy("¿Estás bien?", null) == "Estás bien?" && TextCheck.Tidy(" « Bonjour »", "fr") == "« Bonjour »"
              && TextCheck.Tidy("„Hallo“", "de") == "„Hallo“" && TextCheck.Tidy("「你好」", "zh-cn") == "「你好」" && TextCheck.Tidy(". Hello", "en") == "Hello",
            "texts v6, tidy: each language's opening marks kept at a reply's start (a pack before data v6 tidied as ever)");
        Check(TextCheck.Capitals("¿Qué tal? Bien.") && !TextCheck.Capitals("¿qué tal?") && TextCheck.Capitals("« Bonjour. » Merci.") && !TextCheck.Capitals("Hola. ¡bueno."),
            "texts v6, capitals: a sentence's first letter found past an opening mark");
        Check(TextCheck.JudgeSharedSix("Soube do acidente na 5th Ave. Você está bem?", crash, "pt") == TextVerdict.Ok && TextCheck.JudgeSharedSix("Soube do acidente!", crash, "pt") == TextVerdict.Exclaim
              && TextCheck.JudgeSharedSix("I heard about the crash on 5th Ave. Are you all right?", crash, "pt") == TextVerdict.Language && TextCheck.JudgeSharedSix("x", null, "pt") == TextVerdict.Empty,
            "texts v6, a guest's check: the host's words checked again in their language");

        TextsLanguageHub();
        TextsLanguageShare();
    }

    // The hub with a pack of data 6: the card in the game's language, and the written line counted when the pack does not write it.
    private static void TextsLanguageHub()
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner(), Data = TextIntents.DataV6, Language = "pt", Languages = new[] { "en", "pt" }, Pack = "RLO Texts" };
        string got = "(none)";
        TextRequest r = TextsAskOf("pt1", "friend.crash", null, w => got = w);
        bool asked = hub.Ask(r, 0);
        TextJob job = q.Take(0.1, true);
        Check(asked && job != null && job.Prompt.Contains("\nlang: pt<|im_end|>") && job.Prompt.StartsWith("<|im_start|>system\n" + TextCard.SystemSix, StringComparison.Ordinal) && r.Lang == "pt",
            "texts v6, the hub: a data 6 pack is asked in the game's language, its card's lang line that language, the system turn data 6's");
        q.Done(job, "Soube do acidente na 5th Ave. Você está bem?", 1.0, null);
        hub.Look(1.2);
        Check(got == "Soube do acidente na 5th Ave. Você está bem?", "texts v6, the hub: words in the card's language pass its checks and are shown");
        hub.Ask(TextsAskOf("pt2", "friend.crash", null, w => got = w), 2);
        job = q.Take(2.1, true);
        q.Done(job, "I heard about the crash on 5th Ave. Are you all right?", 1.0, null);
        hub.Look(3.2);
        Check(got == null && hub.Verdicts(TextVerdict.Language) == 1, "texts v6, the hub: words in another language than the card's refused (the written line)");
        hub.Language = "de";
        Check(!hub.Ask(TextsAskOf("de1", "friend.crash", null, w => { }), 4) && hub.Pending == 0, "texts v6, the hub: a pack that does not write the game's language is not asked (the written line)");
        var why = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
        var rule = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        hub.Tally.Totals("RLO Texts", why, rule);
        Check(why[(int)TextShownWhy.Model] == 1 && why[(int)TextShownWhy.Refused] == 1 && rule[(int)TextVerdict.Language] == 1 && why[(int)TextShownWhy.Language] == 1,
            "texts v6, the hub: each line counted: the model's words, a refusal by its rule, the language the pack does not write");
        var old = new TextsHub(new TextQueue()) { Runner = new PretendRunner(), Data = TextIntents.DataV5, Language = "pt" };
        TextQueue oq = old.Queue;
        TextRequest five = TextsAskOf("v5", "friend.crash", null, w => { });
        old.Ask(five, 0);
        TextJob oj = oq.Take(0.1, true);
        Check(oj != null && oj.Prompt.Contains("\nlang: en<|im_end|>") && oj.Prompt.StartsWith("<|im_start|>system\n" + TextCard.System, StringComparison.Ordinal) && five.Lang == null && oj.Grammar == null,
            "texts v6, the hub: a pack of data 5 keeps English cards, its trained system turn and no grammar, whatever the game's language");
    }

    // Going Public: shared text with its language.
    private static void TextsLanguageShare()
    {
        var b = new StringBuilder();
        TextsShare.Line(b, "fn:1", "citizen.post.weather", "Rain again.", "en");
        TextsShare.Line(b, "fn:2", "citizen.post.weather", "Chuva de novo.", "pt");
        TextsShare.Line(b, "fn:3", "citizen.post.weather", "Wieder Regen.", "de");
        TextsShare.Line(b, "fn:4", "citizen.post.weather", "Chuva de novo!", "pt");
        TextsShare.Line(b, "fn:5", "citizen.post.weather", "Rain again.", "xx");
        string text = b.ToString();
        Check(text.StartsWith("w|fn:1|citizen.post.weather|Rain again.\nx|fn:2|citizen.post.weather|pt|Chuva de novo.\n", StringComparison.Ordinal),
            "texts v6, Going Public: English words go as before ('w'), another language's with it ('x')");
        var older = new TextsStore();
        int keptOlder = TextsShare.Apply(text, older, 5, null, true);
        Check(keptOlder == 1 && older.Words("fn:1") == "Rain again." && older.Words("fn:2") == null,
            "texts v6, Going Public: a guest in English, as an older RLO, keeps the English words and passes over the other languages' lines");
        var pt = new TextsStore();
        var refused = new List<string>();
        int keptPt = TextsShare.Apply(text, pt, 5, refused, true, "pt");
        Check(keptPt == 1 && pt.Words("fn:2", "pt") == "Chuva de novo." && pt.Words("fn:1", "pt") == null && pt.Words("fn:3", "pt") == null && refused.Count == 1 && refused[0].StartsWith("fn:4", StringComparison.Ordinal),
            "texts v6, Going Public: a guest in Portuguese keeps only Portuguese words, checked in Portuguese ('!' refused), and shows its own written lines for the rest");
        Check(pt.Words("fn:2", "en") == null && pt.Words("fn:2", "pt") != null && new TextsStore().Words("none", "pt") == null,
            "texts v6, the save's words: words of a language shown only in it (the game's language switched: the written line)");
        var saved = new TextsStore();
        saved.Keep("fp:9", 3, "citizen.post.weather", "Chuva de novo.", true, false, "pt");
        saved.Keep("fp:10", 3, "citizen.post.weather", "Rain again.", true, false, null);
        string line = saved.Write();
        var back = new TextsStore();
        back.Read(line);
        Check(back.Words("fp:9", "pt") == "Chuva de novo." && back.Words("fp:9", "en") == null && back.Words("fp:10", "pt") == "Rain again." && line.Contains("|pt"),
            "texts v6, the save's words: a language kept at the record's end and read back; an older pack's words, with none, shown as ever");
    }
}
