using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;

// A call's composer, the reading (Social/SocialComposerRules.Reading and .Words, SocialCatalogue's topics, PersonDialog.Composer):
// what an answer the player types on a call says, read by the game in every language the mod has, and what it gets back.
// A player typed "very good! holding all our money!" to "Be honest, is Firmway Capital holding up?" and was answered with
// the line for bad news, because every typed answer took the topic's first answer's reaction. Checked: a table of typed
// answers with what each reads as (intensifiers, turns, exclamation marks, emoji and smileys, casual spelling, short
// answers, every language); each topic's line for good news, bad news and anything unclear (never the line for bad news
// to good, never the glad line to bad, the neutral lines for anything unclear, never the cool answer's hurt reaction); the
// reported call end to end; RLO Texts' words that read the other way are not shown; the table's own phrases; the same words
// read the same, and the time a reading takes; the sources; the texts in all nine languages and What's new.
internal static partial class Program
{
    // Typed answers and what each must read as.
    private static readonly (string Text, SocialRead Want)[] TypedAnswers =
    {
        ("very good! holding all our money!", SocialRead.Good),   // the player's report
        ("good", SocialRead.Good), ("very good", SocialRead.Good), ("really well", SocialRead.Good), ("really well thanks", SocialRead.Good),
        ("doing really well, thanks", SocialRead.Good), ("great!!!", SocialRead.Good), ("pretty good", SocialRead.Good),
        ("super good", SocialRead.Good), ("gud", SocialRead.Good), ("gr8", SocialRead.Good), ("goooood", SocialRead.Good),
        ("fine", SocialRead.Good), ("ok", SocialRead.Good), ("all good", SocialRead.Good), ("not bad", SocialRead.Good),
        ("not bad at all", SocialRead.Good), ("not too bad", SocialRead.Good), ("could be worse", SocialRead.Good),
        ("can't complain", SocialRead.Good), ("never been better", SocialRead.Good), ("couldn't be better", SocialRead.Good),
        ("no problems at all", SocialRead.Good), ("no worries", SocialRead.Good), ("holding up", SocialRead.Good),
        ("making good money", SocialRead.Good), ("it's fine, really", SocialRead.Good), ("bruised, but I'm all right", SocialRead.Good),
        ("lost a lot but we'll be fine", SocialRead.Good), ("no it's fine", SocialRead.Good), ("yes, really good", SocialRead.Good),
        ("the shop is doing really well", SocialRead.Good), ("don't worry, all good", SocialRead.Good), ("good, you?", SocialRead.Good),
        ("\U0001F642", SocialRead.Good), ("\U0001F44D\U0001F3FD", SocialRead.Good), (":)", SocialRead.Good), ("GREAT", SocialRead.Good),
        ("not good", SocialRead.Bad), ("not very good", SocialRead.Bad), ("not super good", SocialRead.Bad),
        ("not great to be honest", SocialRead.Bad), ("not doing well", SocialRead.Bad), ("could be better", SocialRead.Bad),
        ("kinda rough", SocialRead.Bad), ("awful", SocialRead.Bad), ("terrible, we lost money", SocialRead.Bad),
        ("struggling a bit", SocialRead.Bad), ("losing money", SocialRead.Bad), ("barely holding up", SocialRead.Bad),
        ("good but slow", SocialRead.Bad), ("no, it's awful", SocialRead.Bad), ("oh no", SocialRead.Bad),
        ("sorry, it's been rough", SocialRead.Bad), ("\U0001F622", SocialRead.Bad), (":(", SocialRead.Bad), ("Well, it lost money", SocialRead.Bad),
        ("yes", SocialRead.Yes), ("yeah", SocialRead.Yes), ("yesss", SocialRead.Yes), ("of course", SocialRead.Yes),
        ("no", SocialRead.No), ("nah", SocialRead.No), ("nope", SocialRead.No), ("not really", SocialRead.No), ("not at all", SocialRead.No),
        ("meh", SocialRead.Unclear), ("so-so", SocialRead.Unclear), ("not sure", SocialRead.Unclear), ("I don't know", SocialRead.Unclear),
        ("it's ok I guess", SocialRead.Unclear), ("what happened?", SocialRead.Unclear), ("are you ok?", SocialRead.Unclear),
        ("asdfgh", SocialRead.Unclear), ("", SocialRead.Unclear), ("!!!", SocialRead.Unclear), ("yes and no", SocialRead.Unclear),
        ("good and bad", SocialRead.Unclear), ("\U0001F937", SocialRead.Unclear),
        // Portuguese
        ("muito bem!", SocialRead.Good), ("tudo bem", SocialRead.Good), ("nada mal", SocialRead.Good), ("não está bom", SocialRead.Bad),
        ("bem ruim", SocialRead.Bad), ("ruim", SocialRead.Bad), ("mais ou menos", SocialRead.Unclear), ("sim", SocialRead.Yes), ("não", SocialRead.No),
        ("nao", SocialRead.No),
        // Spanish
        ("muy bien", SocialRead.Good), ("no está mal", SocialRead.Good), ("no muy bien", SocialRead.Bad), ("fatal", SocialRead.Bad),
        ("más o menos", SocialRead.Unclear), ("sí", SocialRead.Yes),
        // French
        ("très bien", SocialRead.Good), ("pas mal", SocialRead.Good), ("ça va", SocialRead.Good), ("pas terrible", SocialRead.Bad),
        ("ça va pas", SocialRead.Bad), ("bof", SocialRead.Unclear), ("oui", SocialRead.Yes), ("non", SocialRead.No),
        // German
        ("sehr gut", SocialRead.Good), ("nicht schlecht", SocialRead.Good), ("nicht gut", SocialRead.Bad), ("schlecht", SocialRead.Bad),
        ("geht so", SocialRead.Unclear), ("ja", SocialRead.Yes), ("nein", SocialRead.No),
        // Dutch
        ("heel goed", SocialRead.Good), ("niet slecht", SocialRead.Good), ("niet goed", SocialRead.Bad), ("gaat wel", SocialRead.Unclear),
        ("nee", SocialRead.No),
        // Russian
        ("очень хорошо", SocialRead.Good), ("неплохо", SocialRead.Good), ("не плохо", SocialRead.Good), ("не очень", SocialRead.Bad),
        ("плохо", SocialRead.Bad), ("так себе", SocialRead.Unclear), ("да", SocialRead.Yes), ("нет", SocialRead.No),
        // Turkish
        ("çok iyi", SocialRead.Good), ("fena değil", SocialRead.Good), ("sorun yok", SocialRead.Good), ("iyi değil", SocialRead.Bad),
        ("kötü", SocialRead.Bad), ("idare eder", SocialRead.Unclear), ("evet", SocialRead.Yes), ("hayır", SocialRead.No),
        // Chinese
        ("很好", SocialRead.Good), ("不错", SocialRead.Good), ("还不错！", SocialRead.Good), ("不好", SocialRead.Bad), ("不太好", SocialRead.Bad),
        ("好累", SocialRead.Bad), ("亏钱了", SocialRead.Bad), ("一般", SocialRead.Unclear), ("是", SocialRead.Yes), ("不", SocialRead.No)
    };

    // A typed answer to a topic, and the lines the topic must answer it with.
    private static readonly (int Topic, string Text, string[] Want)[] TypedToTopics =
    {
        (SocialCatalogue.TopicShopBad, "very good! holding all our money!", SocialCatalogue.TypedGlad),
        (SocialCatalogue.TopicShopBad, "it lost money yesterday", new[] { "reallife_social_topic_shopbad_ra" }),
        (SocialCatalogue.TopicShopBad, "hmm", SocialCatalogue.TypedNeutral),
        (SocialCatalogue.TopicShopBad, "yes", SocialCatalogue.TypedGlad),
        (SocialCatalogue.TopicShopBad, "no", new[] { "reallife_social_topic_shopbad_ra" }),
        (SocialCatalogue.TopicShopGood, "really well thanks", new[] { "reallife_social_topic_shopgood_ra" }),
        (SocialCatalogue.TopicShopGood, "slow, to be honest", new[] { "reallife_social_topic_shopbad_ra" }),
        (SocialCatalogue.TopicCrash, "yeah I'm fine", new[] { "reallife_social_topic_crash_ra" }),
        (SocialCatalogue.TopicCrash, "not great, my neck hurts", SocialCatalogue.TypedSorry),
        (SocialCatalogue.TopicSick, "yes", SocialCatalogue.TypedSorry),
        (SocialCatalogue.TopicSick, "no, I'm fine", new[] { "reallife_social_topic_sick_rb" }),
        (SocialCatalogue.TopicArrest, "yes", new[] { "reallife_social_topic_arrest_ra" }),
        (SocialCatalogue.TopicKnocked, "no", new[] { "reallife_social_topic_knocked_ra" }),
        (SocialCatalogue.TopicBreakIn, "they took everything, awful", SocialCatalogue.TypedSorry),
        (SocialCatalogue.TopicGoodDay, "that's great!", new[] { "reallife_social_topic_goodday_ra" }),
        (SocialCatalogue.TopicBadDay, "oh no, I'm sorry", new[] { "reallife_social_topic_badday_ra" }),
        (SocialCatalogue.TopicFine, "nah, it's cold", new[] { "reallife_social_topic_fine_rb" }),
        (SocialCatalogue.TopicQuiet, "all good here", SocialCatalogue.TypedGlad),
        (SocialCatalogue.TopicAccident, "awful", SocialCatalogue.TypedNeutral)
    };

    private static void TypedReadingChecks()
    {
        TypedReadingTable();
        TypedReadingTopics();
        TypedReadingReported();
        TypedReadingModelWords();
        TypedReadingWords();
        TypedReadingSources();
        TypedReadingTexts();
    }

    // Every typed answer of the table reads as it must.
    private static void TypedReadingTable()
    {
        foreach ((string text, SocialRead want) in TypedAnswers)
        {
            SocialRead got = SocialComposerRules.Read(text);
            Check(got == want, "typed reading: \"" + text + "\" reads as " + want + " (read as " + got + ")");
        }
        Check(TypedAnswers.Length >= 40, "typed reading: the table holds at least 40 typed answers (" + TypedAnswers.Length + ")");
        Check(SocialComposerRules.Read(null) == SocialRead.Unclear && SocialComposerRules.Read(new string('x', 5000)) == SocialRead.Unclear
              && SocialComposerRules.Read(new string('!', 5000)) == SocialRead.Unclear,
            "typed reading: nothing, a very long word and only signs are not clear, and never bad");
    }

    // Each topic answers good news, bad news and anything unclear with lines that fit: never a sorry to good news.
    private static void TypedReadingTopics()
    {
        var sorry = new HashSet<string>(SocialCatalogue.TypedSorry, StringComparer.Ordinal) { "reallife_social_topic_shopbad_ra" };
        var glad = new HashSet<string>(SocialCatalogue.TypedGlad, StringComparer.Ordinal) { "reallife_social_topic_shopgood_ra" };
        var hurt = new HashSet<string>(StringComparer.Ordinal);
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (SocialTopic topic in SocialCatalogue.Topics)
        {
            own.Add(topic.ReactA);
            own.Add(topic.ReactB);
            if (!topic.BothFine)
            {
                hurt.Add(topic.ReactB);   // the cool answer's reaction: typed words, worth the warm answer, never get it
            }
        }
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")) ?? new Dictionary<string, string>();
        var faults = new List<string>();
        foreach (SocialTopic topic in SocialCatalogue.Topics)
        {
            string[] good = SocialComposerRules.Reaction(topic, SocialRead.Good), bad = SocialComposerRules.Reaction(topic, SocialRead.Bad);
            if (topic.TypedGood == null || topic.TypedGood.Length == 0 || topic.TypedBad == null || topic.TypedBad.Length == 0)
            {
                faults.Add(topic.Id + ": no line of its own for good or bad news");
            }
            if (!ReferenceEquals(SocialComposerRules.Reaction(topic, SocialRead.Unclear), SocialCatalogue.TypedNeutral))
            {
                faults.Add(topic.Id + ": anything unclear is not answered with the neutral lines");
            }
            if (Holds(good, sorry) || Holds(good, hurt))
            {
                faults.Add(topic.Id + ": good news is answered with a sorry or the cool answer's reaction");
            }
            if (Holds(bad, glad) || Holds(bad, hurt))
            {
                faults.Add(topic.Id + ": bad news is answered with a glad line or the cool answer's reaction");
            }
            foreach (SocialRead means in new[] { topic.YesIs, topic.NoIs })
            {
                if (means != SocialRead.Good && means != SocialRead.Bad && means != SocialRead.Unclear)
                {
                    faults.Add(topic.Id + ": a bare yes or no is taken as " + means);
                }
            }
            if (SocialComposerRules.Heard(topic, SocialRead.Yes) != topic.YesIs || SocialComposerRules.Heard(topic, SocialRead.No) != topic.NoIs
                || SocialComposerRules.Heard(topic, SocialRead.Good) != SocialRead.Good || SocialComposerRules.Heard(topic, SocialRead.Unclear) != SocialRead.Unclear)
            {
                faults.Add(topic.Id + ": a yes or no is not heard as the topic takes it");
            }
            foreach (string key in Joined(good, bad))
            {
                if (!en.ContainsKey(key))
                {
                    faults.Add(topic.Id + ": " + key + " is not a text");
                }
            }
        }
        Check(faults.Count == 0 && SocialCatalogue.Topics.Length == 20,
            "typed reading: every topic answers good news, bad news and anything unclear with its own lines, the glad, sorry or neutral ones, "
            + "never a sorry to good news nor the cool answer's reaction (" + (faults.Count > 0 ? string.Join("; ", faults) : "20 topics") + ")");
        Check(!Holds(SocialCatalogue.TypedNeutral, own) && !Holds(SocialCatalogue.TypedNeutral, sorry) && !Holds(SocialCatalogue.TypedNeutral, glad)
              && SocialCatalogue.TypedNeutral.Length >= 2 && SocialCatalogue.TypedGlad.Length >= 2 && SocialCatalogue.TypedSorry.Length >= 2,
            "typed reading: the neutral lines are none of the topics' own, the glad or the sorry lines, and each pool has two or more");
        Check(SocialComposerRules.Reaction(null, SocialRead.Good) == SocialCatalogue.TypedNeutral && SocialComposerRules.Heard(null, SocialRead.Yes) == SocialRead.Unclear,
            "typed reading: without a topic, anything is not clear and gets a neutral line");
        foreach ((int id, string text, string[] want) in TypedToTopics)
        {
            SocialTopic topic = SocialCatalogue.Topics[id];
            string[] got = SocialComposerRules.Reaction(topic, SocialComposerRules.Heard(topic, SocialComposerRules.Read(text)));
            Check(string.Join(",", got) == string.Join(",", want),
                "typed reading: topic " + id + " answers \"" + text + "\" with " + string.Join(", ", want) + " (" + string.Join(", ", got) + ")");
        }
    }

    // The reported call: the shop's loss, a happy answer; before, the line for bad news; now, a glad one.
    private static void TypedReadingReported()
    {
        SocialTopic shopBad = SocialCatalogue.Topics[SocialCatalogue.TopicShopBad];
        const string typed = "very good! holding all our money!";
        SocialRead read = SocialComposerRules.Read(typed);
        string[] now = SocialComposerRules.Reaction(shopBad, SocialComposerRules.Heard(shopBad, read));
        bool before = SocialComposerRules.TypedWarm && shopBad.ReactA == "reallife_social_topic_shopbad_ra";   // the old written answer: the warm one's
        Check(shopBad.Id == SocialCatalogue.TopicShopBad && Array.IndexOf(shopBad.Open, "reallife_social_topic_shopbad_2") >= 0 && before
              && read == SocialRead.Good && Array.IndexOf(now, "reallife_social_topic_shopbad_ra") < 0 && ReferenceEquals(now, SocialCatalogue.TypedGlad),
            "typed reading: \"" + typed + "\" to \"is it holding up?\" reads as good news and is answered with a glad line, not the line for bad news "
            + "the warm answer gave it (" + read + ", " + string.Join(", ", now) + ")");
        string[] honest = SocialComposerRules.Reaction(shopBad, SocialComposerRules.Heard(shopBad, SocialComposerRules.Read("not great, it lost money yesterday")));
        Check(honest.Length == 1 && honest[0] == shopBad.ReactA,
            "typed reading: the same topic still answers bad news with its own line for it (" + string.Join(", ", honest) + ")");
        SocialWorth warm = SocialRules.Chat(true, false, false, true, shopBad.BothFine), typedWorth = SocialRules.Chat(true, false, false, SocialComposerRules.TypedWarm, shopBad.BothFine);
        Check(warm.Affection == typedWorth.Affection && warm.Familiarity == typedWorth.Familiarity && warm.Trust == typedWorth.Trust,
            "typed reading: what the words say changes only what they say back; typed words are still worth the warm answer");
    }

    // RLO Texts' words are shown only when they do not read the other way from the typed words.
    private static void TypedReadingModelWords()
    {
        Check(SocialComposerRules.Against(SocialRead.Good, "Oh no, I'm sorry to hear that.")
              && SocialComposerRules.Against(SocialRead.Good, "Ouch. Thanks for telling me. Every business has days like that. It'll turn.")
              && SocialComposerRules.Against(SocialRead.Bad, "That's brilliant news, well done."),
            "typed reading: the model's words that answer good news as bad (the reported line among them), or bad as good, are not shown");
        Check(!SocialComposerRules.Against(SocialRead.Good, "That's great to hear, I'm proud of you.")
              && !SocialComposerRules.Against(SocialRead.Bad, "Ouch, that sounds rough. Hang in there.")
              && !SocialComposerRules.Against(SocialRead.Bad, "Sorry to hear it. It'll get better, you'll see.")
              && !SocialComposerRules.Against(SocialRead.Good, "Really? What happened?")
              && !SocialComposerRules.Against(SocialRead.Unclear, "Oh no, I'm sorry.") && !SocialComposerRules.Against(SocialRead.Good, null),
            "typed reading: the model's words that agree, say both, ask, or answer something unclear are shown");
    }

    // The table's own phrases, the same words read the same, and what a reading costs.
    private static void TypedReadingWords()
    {
        IReadOnlyList<string> faults = SocialComposerRules.TableFaults;
        Check(faults.Count == 0 && SocialComposerRules.TableSize > 1000,
            "typed reading: every phrase of the table is one phrase and says one thing in every language (" + SocialComposerRules.TableSize + " phrases"
            + (faults.Count > 0 ? "; " + string.Join("; ", faults) : string.Empty) + ")");
        bool same = true;
        foreach ((string text, SocialRead _) in TypedAnswers)
        {
            same &= SocialComposerRules.Read(text) == SocialComposerRules.Read(text) && SocialComposerRules.Read(text) == SocialComposerRules.Read(text + " ");
        }
        Check(same, "typed reading: the same words always read the same, a space more or less");
        List<string> plain = SocialComposerRules.Plain("Ça VA trèèès bien!! N'est-ce pas? I'll be fine 好累");
        Check(string.Join(" ", plain) == "ca va treees bien | nest ce pas ? i be fine 好 累",
            "typed reading: words are made plain: lower case, accents off, apostrophes out and an English ending left out, clauses marked, a Chinese character a word ("
            + string.Join(" ", plain) + ")");
        Check(SocialComposerRules.Read("trèèès bien") == SocialRead.Good && SocialComposerRules.Read("I've seen better days") == SocialRead.Bad
              && SocialComposerRules.Read("ik ook") == SocialRead.Unclear && SocialComposerRules.Read("it'll pass, it's fine") == SocialRead.Good
              && SocialComposerRules.Read("I'll be fine") == SocialRead.Good && SocialComposerRules.Read("he'll manage") == SocialRead.Unclear,
            "typed reading: a stretched word is read without its stretch, an ordinary double letter never (\"seen\" is not Turkish \"sen\", Dutch \"ook\" is not \"ok\"), "
            + "and \"I'll\" and \"he'll\" are never \"ill\" and \"hell\"");
        SocialComposerRules.Read("warm");   // (the table, made once, is not timed)
        var watch = Stopwatch.StartNew();
        int readings = 0;
        for (int round = 0; round < 20; round++)
        {
            foreach ((string text, SocialRead _) in TypedAnswers)
            {
                SocialComposerRules.Read(text);
                readings++;
            }
        }
        watch.Stop();
        double each = watch.Elapsed.TotalMilliseconds / Math.Max(1, readings);
        Check(each < 0.5, "typed reading: a reading takes under 0.5 ms (" + each.ToString("0.000") + " ms on average over " + readings + ")");
    }

    // The composer reads the words for what they get back and for nothing else; the buttons are as before.
    private static void TypedReadingSources()
    {
        string composer = ModSource(@"Social\PersonDialog.Composer.cs"), dialog = ModSource(@"Social\PersonDialog.cs");
        string typed = MemberText(composer, "private void Typed(string text)"), came = MemberText(composer, "private void AnswerCame(string words, string how)");
        int worth = typed.IndexOf("Answered(topic, SocialComposerRules.TypedWarm);", StringComparison.Ordinal);
        int read = typed.IndexOf("SocialComposerRules.Read(words)", StringComparison.Ordinal);
        int heard = typed.IndexOf("_heard = SocialComposerRules.Heard(topic, read);", StringComparison.Ordinal);
        int react = typed.IndexOf("SocialLines.Pick(SocialComposerRules.Reaction(topic, _heard))", StringComparison.Ordinal);
        Check(worth >= 0 && read > worth && heard > read && react > heard && typed.Contains("_react = react;") && !typed.Contains("= Answered("),
            "typed reading: the composer settles the warm answer's worth, reads the words, hears them as the topic takes them and answers with that reading's lines, "
            + "never the warm answer's reaction");
        int against = came.IndexOf("SocialComposerRules.Against(_heard, words)", StringComparison.Ordinal);
        int shown = came.IndexOf("Show(Onward(", StringComparison.Ordinal);
        Check(against >= 0 && shown > against && came.Contains("words = null;"),
            "typed reading: the model's words are weighed against the reading before they are shown, and give way to the written answer when they read the other way");
        Check(MemberText(dialog, "private DialogEntry Reply(").Contains("Onward(Line(Answered(topic, warm), data))"),
            "typed reading: the two answers' buttons keep their own reactions");
        var users = new List<string>();
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (System.IO.Directory.Exists(root))
        {
            foreach (string file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string text = System.IO.File.ReadAllText(file);
                if (text.Contains("SocialComposerRules.Read(") || text.Contains("SocialComposerRules.Against(") || text.Contains("SocialComposerRules.Reaction("))
                {
                    users.Add(System.IO.Path.GetFileName(file));
                }
            }
        }
        Check(string.Join(",", users) == "PersonDialog.Composer.cs",
            "typed reading: only a call's composer reads typed words, for what they get back (" + string.Join(", ", users) + ")");
    }

    // The new lines in every language, each its own, and What's new.
    private static void TypedReadingTexts()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("typed reading", langs, codes))
        {
            return;
        }
        var keys = new List<string>();
        keys.AddRange(SocialCatalogue.TypedGlad);
        keys.AddRange(SocialCatalogue.TypedSorry);
        keys.AddRange(SocialCatalogue.TypedNeutral);
        var faults = new List<string>();
        foreach (string key in keys)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                string text = langs[l].TryGetValue(key, out string t) ? t : null;
                if (string.IsNullOrWhiteSpace(text) || (l > 0 && text == langs[0][key]) || text.Contains("!") || text.Contains("—") || text.Contains("–")
                    || text.Contains(" - ") || text.Contains("{") || text.Length > 80)
                {
                    faults.Add(key + " (" + codes[l] + ")");
                }
            }
        }
        Check(faults.Count == 0 && codes.Length == ModLanguages.PerFeature.Length,
            "typed reading: the glad, sorry and neutral lines are in every language, each its own, short, with no dash, exclamation mark or name in curly brackets ("
            + (faults.Count > 0 ? string.Join(", ", faults) : keys.Count + " keys in " + codes.Length + " languages") + ")");
        Check(FixLineEverywhere("an answer you type on a call is read the right way round", "uma resposta que você digita numa ligação agora é entendida",
                                "通话中你输入的回答现在会被正确理解", "Eine Antwort, die du in einem Anruf tippst, wird jetzt richtig verstanden",
                                "une réponse que vous tapez pendant un appel est maintenant comprise", "una respuesta que escribes en una llamada ahora se entiende",
                                "ответ, который вы печатаете во время звонка, теперь понимается правильно", "een antwoord dat je tijdens een oproep typt",
                                "bir çağrıda yazdığınız yanıt artık doğru anlaşılıyor"),
            "typed reading: What's new has the fix's line, with its fix mark, in some row of every language");
    }

    private static bool Holds(string[] lines, HashSet<string> any)
    {
        foreach (string line in lines)
        {
            if (any.Contains(line))
            {
                return true;
            }
        }
        return false;
    }

    private static List<string> Joined(string[] a, string[] b)
    {
        var all = new List<string>(a);
        all.AddRange(b);
        return all;
    }
}
