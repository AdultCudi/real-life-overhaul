using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Texts;

// RLO Texts with a shared history and more reasons to text (GitHub #12: Wiring/Wiring.TextsMemoryFacts, Wiring.TextsMemory,
// Social/SocialFollowRules.NextDay, SocialDirector.Contact, Texts/TextsDirector.City). Checked: a person's memories of the
// social life and what they last texted the player about reach a text's card (data 6) as memory lines of the kinds and codes
// the packs were trained on, the newest text with a topic, from every feature's texts; an older pack's card has none, a
// shared one none, and a text's card no thread lines (the shape the packs know); the largest card with its reply fits the
// server's context; the day after an outing they text about how it went, once, by the rules (the hour, asleep, texted or
// in touch today, let go after a day), through the social life's own texts and limits, with a card every pack has for a
// friend's good one; the player's news is texted about without a pack too, and the one who texted today is not asked
// again; the Guide's texts page, the option's tip and What's new say so.
internal static partial class Program
{
    private static void TextsHistory()
    {
        HistoryMemoryOnCard();
        HistoryThread();
        HistoryCardShape();
        HistoryBudget();
        HistoryNextDay();
        HistoryLifeWithoutPack();
        HistoryTexts();
    }

    // A friend of the social life with memories, as their facts and as the card of data 6 the hub sends.
    private static SocialPerson HistoryFriend()
    {
        var p = new SocialPerson { Index = 3, Name = "Dana Reyes", Met = true, MetDay = 20, Origin = SocialOrigin.Gym, Stage = SocialStage.Friend };
        p.Remember(MemoryCode.Met, 20);   // (how they met is their origin's: the memory itself has no line)
        p.Remember(MemoryCode.Birthday, 90);
        p.Remember(MemoryCode.Coffee, 95);
        p.Remember(MemoryCode.StoodUp, 97);
        return p;
    }

    // The job a hub of this data version makes of a request (its prompt is the card the pack reads), or null when not asked.
    private static TextJob HistoryJob(int data, TextRequest r)
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner(), Data = data, Language = "en", English = true, Languages = new[] { "en" } };
        return hub.Ask(r, 0) ? q.Take(0.1, true) : null;
    }

    private static void HistoryMemoryOnCard()
    {
        SocialPerson p = HistoryFriend();
        var facts = new List<TextFact>();
        Wiring.TextsMemoryFacts.People(p, facts);
        bool kinds = facts.Count == 4 && facts.Exists(f => f.Kind == TextMemoryKinds.Met && f.Code == "gym" && f.Day == 20)
                     && facts.Exists(f => f.Kind == TextMemoryKinds.Talked && f.Code == "outing" && f.Value == "coffee" && f.Day == 95)
                     && facts.Exists(f => f.Kind == TextMemoryKinds.Tone && f.Code == "stood_up" && f.Day == 97)
                     && facts.Exists(f => f.Kind == TextMemoryKinds.Favour && f.Code == "birthday" && f.Day == 90);
        bool lines = true;
        foreach (TextFact f in facts)
        {
            lines &= TextMemoryRules.Line(f, 100) != null;
        }
        Check(kinds && lines, "texts history: a person's memories of the social life (how they met, an outing, a favour, a birthday remembered, being stood up) are memory facts with lines on a card");

        var thread = new List<SocialThreadLine> { CbLine("reallife_social_welcome_gym", 20, 18), CbLine("reallife_social_text_news_4", 98, 14) };
        Wiring.TextsMemoryFacts.Texted(thread, false, false, 100, facts);
        TextRequest r = TextsAskOf("h1", "friend.news.good", null, w => { });
        r.Day = 100;
        r.Person = "s:3";
        r.Slots.Set("happening", "the bakery on my street had fresh bread");
        r.Memory.AddRange(facts);
        TextJob six = HistoryJob(TextIntents.DataV6, r);
        string memory = six != null ? HistoryCardLine(six.Prompt, "memory: ") : null;
        int count = memory != null ? memory.Split(new[] { "; " }, StringSplitOptions.None).Length : 0;
        Check(memory != null && count == TextMemoryRules.CardMost && memory.Contains("texted you about their day, 2 days ago") && memory.Contains("you remembered their birthday, 10 days ago")
              && memory.Contains("met up with you for coffee, 5 days ago"),
            "texts history: a text's card of data 6 carries what that friend remembers and what they last texted about, at most " + TextMemoryRules.CardMost + " lines (" + memory + ")");

        r.Memory.Clear();
        r.Memory.AddRange(facts);
        TextJob five = HistoryJob(TextIntents.DataV5, r);
        r.Shared = true;
        TextJob shared = HistoryJob(TextIntents.DataV6, r);
        Check(five != null && !five.Prompt.Contains("memory:") && (shared == null || !shared.Prompt.Contains("memory:")),
            "texts history: a pack of data 5 or older is never sent a memory line (its cards as it was trained), and a shared card none");
    }

    // What a person last texted the player about, from their thread: the newest with a topic, of any feature, worded or not.
    private static void HistoryThread()
    {
        var into = new List<TextFact>();
        string Texted(List<SocialThreadLine> t, bool family, bool together, int today)
        {
            into.Clear();
            Wiring.TextsMemoryFacts.Texted(t, family, together, today, into);
            return into.Count == 1 && into[0].Kind == TextMemoryKinds.Talked && into[0].Code == "text" ? into[0].Value + "@" + into[0].Day : into.Count == 0 ? "none" : "wrong";
        }
        var life = new List<SocialThreadLine> { CbLine("reallife_ev_fm02", 50, 11) };
        var mapped = new List<SocialThreadLine> { CbLine("reallife_social_text_crash_1", 50, 11) };
        var held = new Dictionary<string, string> { { "text", "x" }, { SocialFollowRules.KeyField, "reallife_family_neglect_2" } };
        var worded = new List<SocialThreadLine> { CbLine(SocialFollowRules.WordsKey, 50, 11, false, held) };
        var mine = new List<SocialThreadLine> { CbLine("reallife_ev_fr02", 49, 10), CbLine("reallife_family_sms_new_home_sent", 50, 9, true), CbLine("reallife_social_welcome_gym", 50, 10) };
        Check(Texted(life, true, false, 51) == "the garage door@50" && Texted(mapped, false, false, 51) == "your crash@50" && Texted(worded, false, true, 51) == "keeping in touch@50"
              && Texted(mine, false, false, 51) == "a dog in sunglasses@49",
            "texts history: what they last texted about is the newest of their texts with a topic: a little moment's own words, RLO Texts' map's topic for its lines, by the key RLO Texts' words "
            + "stand for, past the player's own message and a text with no topic");
        int talk = TextMemoryRules.Lifetime(TextMemoryKinds.Talked);
        Check(Texted(life, true, false, 50 + talk) == "the garage door@50" && Texted(life, true, false, 51 + talk) == "none"
              && Texted(life, true, false, 49) == "none" && Texted(new List<SocialThreadLine>(), false, false, 50) == "none" && Texted(null, false, false, 50) == "none",
            "texts history: a text is remembered for as long as a talk is, and never one dated after today");
        Check(Wiring.TextsMemoryFacts.TopicOf("reallife_social_text_arrest_kind_1", false, true) == "your arrest"
              && Wiring.TextsMemoryFacts.TopicOf("reallife_social_text_arrest_kind_1", false, false) == "your arrest"
              && Wiring.TextsMemoryFacts.TopicOf("reallife_family_mom_married", true, false) == TextMemoryTopics.Text("mom.news.married")
              && Wiring.TextsMemoryFacts.TopicOf("reallife_social_welcome_gym", false, false) == null && Wiring.TextsMemoryFacts.TopicOf(null, false, false) == null,
            "texts history: a text's topic is the contract's where RLO Texts' map knows its card, else the call back's subject's, and none for a welcome");

        // A call back with a pack: the composer's card has their line about it and the typed words, and remembers the text.
        var facts = new List<TextFact>();
        Wiring.TextsMemoryFacts.Texted(new List<SocialThreadLine> { CbLine("reallife_social_text_news_1", 100, 9) }, false, false, 100, facts);
        TextRequest r = TextsAskOf("call:3", "friend.dm.answer", null, w => { }, TextPriority.Now, 6);
        r.Day = 100;
        r.Person = "s:3";
        r.History.Add(new TextSaid("Dana", "You saw my text, then. I've been smiling about it all day."));
        r.History.Add(new TextSaid("Alex", "haha the dog in the raincoat made my day"));
        r.Memory.AddRange(facts);
        TextJob job = HistoryJob(TextIntents.DataV6, r);
        string memory = job != null ? HistoryCardLine(job.Prompt, "memory: ") : null;
        Check(job != null && memory != null && memory.Contains("texted you about their day, today") && job.Prompt.Contains("history:\n  Dana: You saw my text, then.")
              && job.Prompt.Contains("\n  Alex: haha the dog in the raincoat made my day"),
            "texts history: a call back's typed answer is asked with their line about the text and the typed words as the card's thread, and the text in its memory (" + memory + ")");
    }

    // The cards keep the shape the packs were trained on: no thread lines on a text's card, the contract's memory codes, no new intents.
    private static void HistoryCardShape()
    {
        string phone = ModSource(@"Wiring\TextsPhone.cs");
        Check(!MemberText(phone, "private static TextRequest Request(").Contains("History") && !MemberText(phone, "private static bool Social(").Contains("History")
              && !MemberText(phone, "private static bool Hold(").Contains("History"),
            "texts history: a text's card carries no thread lines (the packs answer a thread's last line, so its lines would change what a text says): its history is its memory");
        Check(TextMemoryRules.Template(TextMemoryKinds.Talked, "text") == "texted you about {value}" && TextsHub.Newest == TextIntents.DataV6,
            "texts history: what they last texted about is the contract's own memory line, and no data version past 6 is asked of any pack");
        bool none = true;
        foreach (TextIntent t in TextIntents.All)
        {
            none &= t.Key.IndexOf("nextday", StringComparison.Ordinal) < 0 && t.Key.IndexOf("follow", StringComparison.Ordinal) < 0 && t.Key.IndexOf("callback", StringComparison.Ordinal) < 0;
        }
        string memory = ModSource(@"Wiring\Wiring.TextsMemory.cs");
        Check(none && MemberText(memory, "internal static void Fill(").Contains("Texted(person, r.Day, r.Memory);") && MemberText(memory, "internal static void Fill(").Contains("!TextsDirector.Six"),
            "texts history: no new card is asked of the packs; the thread's memory is added only with a pack of data 6, as all memory is");
    }

    // The largest card a person of the social life's text or answer makes, with its reply, inside the server's context.
    private static void HistoryBudget()
    {
        string args = TextServer.Arguments(new TextServerStart { Exe = "x", Model = "m" }, 1, "k");
        int at = args.IndexOf(" -c ", StringComparison.Ordinal);
        int context = at >= 0 && int.TryParse(args.Substring(at + 4).Split(' ')[0], out int c) ? c : 0;
        string longest = "the corner shop past the old station on Madison";   // (a slot's value, as long as the game's longest names and places)
        var who = new TextWho
        {
            Name = "Bartholomew Castellano-Whitlock", First = "Bartholomew", Age = 65, Job = "personal trainer", District = "Garment District",
            Role = "a close friend, a customer at your shop", Mood = "having a great day", Style = "warm, chatty, signs off with a kiss and a joke about the weather",
            Player = "Maximiliana Fitzgerald", When = "Wednesday 23:59"
        };
        who.Traits.Add("a little envious");
        who.Traits.Add("careful with money");
        var memory = new List<TextFact>
        {
            new TextFact(TextMemoryKinds.Event, "listed", 99, "Castellano Whitlock Holdings International"),
            new TextFact(TextMemoryKinds.Favour, "directions", 99, longest),
            new TextFact(TextMemoryKinds.Talked, "text", 99, "a cousin's wedding gift"),
            new TextFact(TextMemoryKinds.Knows, "business", 1, longest)
        };
        int worst = 0, cards = 0;
        string worstKey = null;
        foreach (TextIntent t in TextIntents.All)
        {
            if (t.Data != TextIntents.DataV6 || t.Use != TextUse.People || (t.Shape != "text" && t.Shape != "dm"))
            {
                continue;
            }
            var slots = new TextSlots();
            foreach (string name in SixSlotNames(t))
            {
                slots.Set(name, longest);
            }
            var history = new List<TextSaid>();
            if (t.Key.IndexOf(".dm.", StringComparison.Ordinal) >= 0)
            {
                for (int i = 0; i < SocialComposerRules.HistoryMost; i++)
                {
                    history.Add(new TextSaid(i % 2 == 0 ? who.First : who.Player, HistoryLongLine()));
                }
            }
            var sheet = new TextSheet();
            if (!TextIntents.Build(t, who, slots, history, sheet))
            {
                continue;
            }
            TextsHub.Finish(t, sheet, "en", memory, 100, false);
            sheet.Lang = "zh-cn";   // (the card's words stay English in every language: only its lang line and the reply's length change)
            string prompt = TextCard.ChatMl(sheet, TextCard.SystemSix);
            int tokens = Encoding.UTF8.GetByteCount(prompt) / BytesAToken + TextDecoding.Tokens(t, "zh-cn");
            cards++;
            if (tokens > worst)
            {
                worst = tokens;
                worstKey = t.Key + " " + t.Branch;
            }
        }
        Check(context >= 1024 && cards > 60 && worst > 0 && worst <= context,
            "texts history: the largest card of a person's text or answer, with three memory lines, the longest thread and the longest reply in any language, fits the server's context (" + cards
            + " cards; at most " + worst + " tokens at " + BytesAToken + " bytes a token, " + worstKey + "; the context " + context + "; TextsRunnerHistory sends the largest to the pack itself)");
    }

    // Bytes of a card's English a token stands for at the least: the packs' tokenizers take three to five (names and figures
    // the fewest), so three overstates a card's tokens; the runner below sends the largest card to a real pack as well.
    private const int BytesAToken = 3;

    // A typed line as long as the composer lets the player type (SocialComposerRules.TypedMost), in plain words.
    private static string HistoryLongLine()
    {
        var b = new StringBuilder(SocialComposerRules.TypedMost);
        string[] words = { "honestly", "I", "think", "we", "should", "go", "back", "to", "that", "place", "near", "the", "river", "next", "week" };
        for (int i = 0; b.Length < SocialComposerRules.TypedMost - 10; i++)
        {
            b.Append(i > 0 ? " " : string.Empty).Append(words[i % words.Length]);
        }
        return b.ToString();
    }

    // The day after an outing: the rules, the lines, the outing kept and let go, through the social life's own texts.
    private static void HistoryNextDay()
    {
        SocialFollowRules.NextDayStep S(int afterDay, int today, int hour, bool asleep = false, int lastText = 0, int contact = 0) =>
            SocialFollowRules.NextDay(afterDay, today, hour, asleep, lastText, contact);
        const SocialFollowRules.NextDayStep Text = SocialFollowRules.NextDayStep.Text, None = SocialFollowRules.NextDayStep.None, Go = SocialFollowRules.NextDayStep.LetGo;
        Check(S(40, 41, SocialFollowRules.NextDayFrom) == Text && S(40, 41, SocialFollowRules.NextDayFrom - 1) == None && S(40, 40, 20) == None && S(40, 42, 12) == Go
              && S(40, 39, 12) == Go && S(0, 41, 12) == None && S(40, 41, 12, true) == None && S(40, 41, 12, false, 41) == None && S(40, 41, 12, false, 40, 41) == None
              && S(40, 41, 12, false, 40, 40) == Text,
            "texts history: they text about an outing the day after it, from " + SocialFollowRules.NextDayFrom + ":00, awake, not having texted or been in touch that day; a day later it is let go");
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        bool pools = true;
        string lacking = string.Empty;
        foreach (bool family in new[] { false, true })
        {
            foreach (bool together in new[] { false, true })
            {
                foreach (bool well in new[] { false, true })
                {
                    string[] pool = SocialFollowRules.NextDayLines(family, together, well);
                    bool whose = pool != null && (family ? pool[0].Contains("_mom_") : together ? pool[0].Contains("_date_") : !pool[0].Contains("_mom_") && !pool[0].Contains("_date_"));
                    pools &= pool != null && pool.Length == 2 && whose
                             && pool[0].Contains(well ? "_good_" : "_bad_") && SocialFollowCatalogue.Of(pool[0]) != null && SocialFollowCatalogue.Of(pool[1]) == SocialFollowCatalogue.Of(pool[0]);
                    foreach (string key in pool ?? new string[0])
                    {
                        string l = words[0] != null ? LanguagesLacking(new[] { words[0], words[1], words[2] }, new[] { codes[0], codes[1], codes[2] }, key) : "en";
                        lacking += l.Length > 0 ? key + " (" + l + ") " : string.Empty;
                    }
                }
            }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int day = 1; day <= 40; day++)
        {
            seen.Add(SocialFollowRules.Line(SocialCatalogue.TextNextDayGood, day, 77));
        }
        Check(pools && lacking.Length == 0 && seen.Count == 2 && SocialFollowRules.Line(SocialCatalogue.TextNextDayGood, 12, 77) == SocialFollowRules.Line(SocialCatalogue.TextNextDayGood, 12, 77),
            "texts history: Mom's, the partner's (a date) and a friend's lines for a good outing and a bad one, in en, pt and zh-cn, picked by the day and the person the same on every machine, "
            + "each a call back's subject (" + lacking + ")");

        string after = ModSource(@"Social\OutingScene.After.cs");
        string settle = MemberText(after, "private void Settle(bool wentWell)");
        int test = settle.IndexOf("if (!_test)", StringComparison.Ordinal);
        int kept = settle.IndexOf("_who.AfterDay = SocialLife.Day;", StringComparison.Ordinal);
        string director = ModSource(@"Social\SocialDirector.cs");
        string contact = ModSource(@"Social\SocialDirector.Contact.cs");
        string hourly = MemberText(contact, "private void Hourly(");
        string next = MemberText(contact, "private static bool NextDayText(");
        Check(test >= 0 && kept > test && settle.Contains("_who.AfterWell = wentWell;") && settle.Contains("_who.AfterKind = _kind;")
              && director.Contains("if (SocialLife.SendText(_thanks, SocialLines.Pick(SocialCatalogue.TextThanks), data))") && director.Contains("_thanks.AfterDay = 0;"),
            "texts history: an outing that ends (not a test's) is kept for the day after, how it went and where; a good one's thanks sent the same day lets it go");
        int capped = hourly.IndexOf("!SocialLife.MayText()", StringComparison.Ordinal);
        int news = hourly.IndexOf("_reactedBreakInDay != day", StringComparison.Ordinal);
        int asked = hourly.IndexOf("if (NextDayText(day, hour))", StringComparison.Ordinal);
        int roll = hourly.IndexOf("if (Random.value > 0.09f + 0.02f * SocialState.MetCount)", StringComparison.Ordinal);
        Check(capped >= 0 && news > capped && asked > news && roll > asked && next.Contains("bool sent = SocialLife.SendText(p, key, data);") && !next.Contains("SendMessage")
              && next.Contains("Mathf.Max(p.LastTextDay, SocialPhone.LastTextDay(p))") && next.Contains("p.AfterDay = 0;") && next.Contains("return true;")
              && contact.Contains("if (p.AfterDay > 0 && day > p.AfterDay + 1)"),
            "texts history: the day after's text comes after the player's own news, one an hour, through the social life's own texts (their switch, the day's cap, the quiet hours, one a person "
            + "a day, a text from any feature counted), and is let go at the day's round past its day");

        // A pack: a friend's good one is thanks for the outing, the card every pack has; the rest keep their written lines.
        Check(TextsMapRules.Phone("reallife_social_text_nextday_good_1", TextsMapRules.From.Friend, out _) == "friend.thanks"
              && TextsMapRules.Phone("reallife_social_text_nextday_good_1", TextsMapRules.From.Partner, out _) == null
              && TextsMapRules.Phone("reallife_social_text_nextday_bad_2", TextsMapRules.From.Friend, out _) == null
              && TextsMapRules.Phone("reallife_social_text_nextday_date_good_1", TextsMapRules.From.Partner, out _) == null
              && TextsMapRules.Phone("reallife_social_text_nextday_mom_good_2", TextsMapRules.From.Mom, out _) == null
              && TextIntents.Find("friend.thanks", null, TextIntents.DataV2) != null && contact.Contains("data[\"t_place\"] = p.AfterPlace ?? string.Empty;")
              && contact.Contains("data[\"t_kind\"] = p.AfterKind.ToString();") && phone(@"Copy(data, r.Slots, ""t_place"", ""place"");"),
            "texts history: with a pack, a friend's text the day after a good outing is worded as their thanks for it (every pack's card, with the outing and the place); "
            + "the rest keep their written lines");
        bool phone(string what) => ModSource(@"Wiring\TextsPhone.cs").Contains(what);

        TextRequest r = TextsAskOf("ph:s:3:next", "friend.thanks", null, w => { });
        r.Day = 100;
        r.Slots.Set("what", "coffee").Set("place", "Five Cups");
        var facts = new List<TextFact>();
        Wiring.TextsMemoryFacts.People(HistoryFriend(), facts);
        r.Memory.AddRange(facts);
        TextJob job = HistoryJob(TextIntents.DataV6, r);
        string memory = job != null ? HistoryCardLine(job.Prompt, "memory: ") : null;
        Check(job != null && job.Prompt.Contains("news: you and Dana just had coffee at Five Cups; it went well") && memory != null && memory.Contains("met up with you for coffee"),
            "texts history: the thanks card for the day after a good coffee names the outing and the place, and remembers it in its memory (" + memory + ")");
    }

    // The player's news is texted about without a pack too; the one who texted today is not asked again about the same news.
    private static void HistoryLifeWithoutPack()
    {
        string city = ModSource(@"Texts\TextsDirector.City.cs");
        string hour = MemberText(city, "private void Hour()");
        string talk = MemberText(city, "private static void City(int day, int hour, bool six)");
        Check(hour.Contains("if (Settings.TextsCity)") && !hour.Contains("Six && Settings.TextsCity") && hour.Contains("City(save.Day, save.Hour, Six);")
              && talk.Contains("if (six && TextsBridges.Staff != null)") && talk.Contains("LifeTexts(day);")
              && talk.IndexOf("if (six)", StringComparison.Ordinal) < talk.IndexOf("Gossip(day, hour);", StringComparison.Ordinal)
              && talk.IndexOf("if (six)", StringComparison.Ordinal) > talk.IndexOf("LifeTexts(day);", StringComparison.Ordinal),
            "texts history: the people of the player's life text after the player's news with a pack or without one; the staff and gossip only with a pack of data 6");
        string memory = ModSource(@"Texts\TextsDirector.Memory.cs");
        string prune = MemberText(memory, "private static void PruneMemory(");
        Check(MemberText(memory, "public static void Heard(").Contains("if (!Settings.Texts || kind == null)") && prune.Contains("int city = CityStore.Prune(day);")
              && prune.Contains("int gone = Six ? MemoryStore.Prune(day) : 0;") && MemberText(memory, "public static bool Remember(").Contains("if (!Six || person == null)"),
            "texts history: the player's events are noted and let go after their days without a pack too; what people remember is still kept only with one");
        string tests = ModSource(@"Texts\TextsDirector.Tests.cs");
        Check(MemberText(tests, "public static void TestLife()").Contains("if (!CityReady(false))") && MemberText(tests, "public static void TestGossip()").Contains("if (!CityReady())"),
            "texts history: the test of a text about the player's news needs no pack; gossip's still does");

        // Mom texted today about the baby (the family's own text): the news of the baby is texted by a close friend, not by her again.
        var people = new List<TextPerson>
        {
            new TextPerson { Index = 0, First = "Mom", Seed = 11, Stage = 2, Family = true, Met = true, LastTextDay = 60, Closeness = 80 },
            new TextPerson { Index = 4, First = "Dana", Seed = 12, Stage = 3, Met = true, LastTextDay = 55, Closeness = 70 }
        };
        var baby = new TextLifeEvent { Kind = "baby", Day = 60 };
        bool told = TextsCityRules.Sender(baby, people, 60, out TextLifeSender sender);
        people[0].LastTextDay = 59;
        bool mom = TextsCityRules.Sender(baby, people, 60, out TextLifeSender first);
        string links = ModSource(@"Wiring\Wiring.TextsCity.cs");
        Check(told && sender.Index == 4 && sender.Intent == "friend.baby" && mom && first.Index == 0 && first.Intent == "mom.news.baby"
              && links.Contains("int lastText = p.Met ? Math.Max(p.LastTextDay, SocialPhone.LastTextDay(p)) : p.LastTextDay;") && links.Contains("LastTextDay = lastText"),
            "texts history: whoever texted the player today, by any feature (Mom's own text of the baby from the family), is not the one who texts about the same news; the next in order is");
    }

    // The Guide's texts page, the option's tip and What's new say so.
    private static void HistoryTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("texts history", words, codes))
        {
            return;
        }
        var page = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "- A text also remembers what that person last texted you about" }, { "pt", "- Uma mensagem também lembra sobre o que aquela pessoa te escreveu" },
            { "zh-cn", "- 短信也会记得那个人上次给你发短信说的是什么" }, { "de", "- Eine Nachricht erinnert sich auch daran, worüber dir diese Person" },
            { "fr", "- Un message se souvient aussi de ce dont cette personne vous a écrit" }, { "es", "- Un mensaje también recuerda de qué te escribió esa persona" },
            { "ru", "- Сообщение также помнит, о чём этот человек писал вам" }, { "nl", "- Een bericht onthoudt ook waar die persoon je het laatst over schreef" },
            { "tr", "- Bir mesaj ayrıca o kişinin size en son ne hakkında yazdığını" }
        };
        var line = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "People text the day after an outing or a date" }, { "pt", "As pessoas escrevem no dia seguinte a um passeio ou a um encontro" },
            { "zh-cn", "外出或约会的第二天" }, { "de", "Am Tag nach einem Ausflug oder einem Date" }, { "fr", "Le lendemain d'une sortie ou d'un rendez-vous amoureux" },
            { "es", "La gente escribe al día siguiente de una salida o de una cita" }, { "ru", "На следующий день после прогулки или свидания" },
            { "nl", "Mensen sturen de dag na een uitje of een date" }, { "tr", "İnsanlar bir gezinin ya da randevunun ertesi günü" }
        };
        var wrong = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!page.TryGetValue(codes[l], out string bullet) || !words[l].TryGetValue("reallife_guide_texts", out string texts) || !texts.Contains(bullet) || !texts.Contains("{texts_big_share}"))
            {
                wrong.Add(codes[l] + " Guide");
            }
            if (!line.TryGetValue(codes[l], out string part) || !AnyChangesRow(words[l], part))
            {
                wrong.Add(codes[l] + " What's new");
            }
        }
        bool tip = words[0].TryGetValue("reallife_opt_tip_texts_city", out string en) && en.StartsWith("The people in your life text after your big news.", StringComparison.Ordinal)
                   && !en.StartsWith("With a pack", StringComparison.Ordinal)
                   && LanguagesLacking(new[] { words[0], words[1], words[2] }, new[] { codes[0], codes[1], codes[2] }, "reallife_opt_tip_texts_city").Length == 0;
        Check(codes.Length == 9 && wrong.Count == 0 && tip,
            "texts history: the Guide's RLO Texts page, the option's tip and What's new say people text about the player's news without a pack, the day after an outing, and remember their last text "
            + "(wrong: " + string.Join(", ", wrong) + (tip ? string.Empty : ", the tip") + ")");
    }

    // A card's line that starts so, without its name; null if the card has none.
    private static string HistoryCardLine(string card, string name)
    {
        int at = card.IndexOf("\n" + name, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        int end = card.IndexOf('\n', at + 1);
        return card.Substring(at + 1 + name.Length, (end < 0 ? card.Length : end) - at - 1 - name.Length);
    }

    // ---------------------------------------------------------------- the real runner on the Lite pack (called by TextsRunner)

    // The cards this work sends, through the pinned llama.cpp and the Lite pack, worded and passed by the checker of data v6: the
    // thanks the day after a good outing with what they remember, a call back's typed answer with its thread and memory, and a
    // friend's small talk that remembers their last text. Each asked up to RunnerTries times (the 350M model is off now and then).
    private static void TextsRunnerHistory(TextPack lite, TextQueue q, TextServer server)
    {
        if (lite.Data < TextIntents.DataV6)
        {
            Console.WriteLine("SKIPPED: texts history, the runner: the Lite pack on this machine is data v" + lite.Data + "; these cards are of data 6.");
            return;
        }
        var hub = new TextsHub(q) { Runner = server, Data = lite.Data, Language = "en", English = true, Languages = lite.Languages ?? new[] { "en" } };
        q.SetBudget(120, TextQueue.BackgroundPerMinute);
        var friend = new List<TextFact>();
        Wiring.TextsMemoryFacts.People(HistoryFriend(), friend);
        var cases = new List<(string what, Func<TextRequest> make)>
        {
            ("the day after a good coffee", () =>
            {
                TextRequest r = TextsAskOf("hx1", "friend.thanks", null, null, TextPriority.Now, 12);
                r.Slots.Set("what", "coffee").Set("place", "Five Cups");
                r.Memory.AddRange(friend);
                return r;
            }),
            ("a call back's typed answer", () =>
            {
                TextRequest r = TextsAskOf("hx2", "friend.dm.answer", null, null, TextPriority.Now, 12);
                r.History.Add(new TextSaid("Dana", "You saw my text, then. I've been smiling about it all day."));
                r.History.Add(new TextSaid("Alex", "haha the dog in the raincoat made my day"));
                Wiring.TextsMemoryFacts.Texted(new List<SocialThreadLine> { CbLine("reallife_social_text_news_1", 100, 9) }, false, false, 100, r.Memory);
                return r;
            }),
            ("small talk that remembers their last text", () =>
            {
                TextRequest r = TextsAskOf("hx3", "friend.news.good", null, null, TextPriority.Now, 12);
                r.Slots.Set("happening", "the bakery on my street had fresh bread");
                r.Memory.AddRange(friend);
                Wiring.TextsMemoryFacts.Texted(new List<SocialThreadLine> { CbLine("reallife_ev_fr15", 99, 18) }, false, false, 100, r.Memory);
                return r;
            })
        };
        int passed = 0;
        var said = new List<string>();
        foreach (var c in cases)
        {
            for (int t = 0; t < RunnerTries; t++)
            {
                string got = "(waiting)";
                TextRequest r = c.make();
                r.Key += ":" + t;
                r.Day = 100;
                r.Person = "s:3";
                r.Done = w => got = w;
                if (!hub.Ask(r, TextClock.Now))
                {
                    said.Add(c.what + ": not asked (" + r.Why + ")");
                    break;
                }
                var each = Stopwatch.StartNew();
                while (got == "(waiting)" && each.Elapsed.TotalSeconds < 20)
                {
                    hub.Look(TextClock.Now);
                    System.Threading.Thread.Sleep(20);
                }
                if (got != null && got != "(waiting)")
                {
                    passed++;
                    said.Add(c.what + ": \"" + got + "\" in " + each.Elapsed.TotalSeconds.ToString("0.00") + " s");
                    break;
                }
                said.Add(c.what + ": refused (" + r.Why + ")");
            }
        }
        Console.WriteLine("  texts history, the runner: " + string.Join("; ", said));
        Check(passed == cases.Count, "texts history, the runner: the cards of a call back and the day after, with memory and a thread, go through the Lite pack and pass the checker of data 6 ("
                                     + passed + " of " + cases.Count + " within " + RunnerTries + " tries)");

        // The largest card a person's answer makes (three memory lines with long names, the longest thread the composer keeps),
        // sent to the pack itself: the server takes it whole and writes a reply, so it is inside the pack's context.
        TextIntent dm = TextIntents.Find("friend.dm.answer", null, lite.Data);
        var who = new TextWho
        {
            Name = "Bartholomew Castellano-Whitlock", First = "Bartholomew", Age = 65, Job = "personal trainer", District = "Garment District",
            Role = "a close friend, a customer at your shop", Mood = "having a great day", Style = "warm, chatty", Player = "Maximiliana Fitzgerald", When = "Wednesday 23:59"
        };
        var thread = new List<TextSaid>();
        for (int i = 0; i < SocialComposerRules.HistoryMost; i++)
        {
            thread.Add(new TextSaid(i % 2 == 0 ? who.First : who.Player, HistoryLongLine()));
        }
        var memory = new List<TextFact>
        {
            new TextFact(TextMemoryKinds.Event, "listed", 99, "Castellano Whitlock Holdings International"),
            new TextFact(TextMemoryKinds.Favour, "directions", 99, "the corner shop past the old station on Madison"),
            new TextFact(TextMemoryKinds.Talked, "text", 99, "a cousin's wedding gift")
        };
        var sheet = new TextSheet();
        bool built = dm != null && TextIntents.Build(dm, who, new TextSlots(), thread, sheet);
        TextJob largest = null;
        if (built)
        {
            TextsHub.Finish(dm, sheet, "en", memory, 100, false);
            largest = new TextJob
            {
                Key = "hx:largest", Prompt = TextCard.ChatMl(sheet, TextCard.SystemSix), Priority = TextPriority.Now, Deadline = TextClock.Now + 30,
                Tokens = TextDecoding.Tokens(dm, "en"), Grammar = TextDecoding.Grammar(TextDecoding.GrammarName("en", false))
            };
            q.SetBudget(120, TextQueue.BackgroundPerMinute);   // (the hub above put its own budget back on the queue: the check asks more a minute than the game)
            if (q.Add(largest, TextClock.Now))
            {
                server.Nudge();
                var sw = Stopwatch.StartNew();
                while ((largest.State == TextJobState.Waiting || largest.State == TextJobState.Running) && sw.Elapsed.TotalSeconds < 35)
                {
                    System.Threading.Thread.Sleep(20);
                }
            }
        }
        Check(built && largest != null && largest.State == TextJobState.Written && largest.Raw != null && sheet.Memory.Count == TextMemoryRules.CardMost,
            "texts history, the runner: the largest card of an answer (" + (largest != null ? Encoding.UTF8.GetByteCount(largest.Prompt) + " bytes" : "not built")
            + ", three memory lines and the longest thread) is taken whole by the pack's server and answered ("
            + (largest != null ? largest.State + (largest.Why != null ? ", " + largest.Why : string.Empty) : "none") + ")");
    }
}
