using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul.Texts;

// Checks for the city's new talk of data v6 (Texts/TextsCityRules.cs, TextsCityStore.cs) as game-free rules: when one of
// the player's staff texts (a raise asked for, off sick, thanks for a raise, their hours) and never twice a day; the
// player's life events from one look to the next (the first look only notes); who texts about each, the same on every
// machine; gossip passed on once, from the day after, by somebody who knows who heard it; what a review of the player's
// business is mostly about; every written line any of it can pick in en, pt and zh-cn; the save's line read back; RLO
// Texts Large (its tier, where it may run, when it is too slow, when it is offered); and source checks: nothing of it per
// frame, every entry point guarded, nothing of it sent in a Going Public session.
internal static partial class Program
{
    private const string StaffStemSix = "reallife" + "_texts_staff_";   // TextsDirector.StaffStem (the director is the game's: its constant checked in its source)

    private static void TextsCitySix()
    {
        TextsCityStaff();
        TextsCityLife();
        TextsCityPeople();
        TextsCityKeys();
        TextsCityStoreSix();
        TextsLarge();
        TextsSixSources();
    }

    private static void TextsCityStaff()
    {
        var s = new TextStaff { Id = "e1", Name = "Dana Reyes", First = "Dana", Paid = 1500, Going = 1700, Raise = 100, MostHours = 40, WeekHours = 38 };
        var m = new TextStaffMark { Id = "e1" };
        Check(TextsCityRules.Staff(s, m, 10, 10) == TextStaffSay.None && m.RaiseSeen == 100, "texts v6, staff: a first sight says nothing of what came before (a raise from before, not thanked)");
        s.Sick = true;
        TextStaffSay sick = TextsCityRules.Staff(s, m, 11, 7);
        TextStaffSay again = TextsCityRules.Staff(s, m, 11, 8);
        s.Sick = false;
        Check(sick == TextStaffSay.Sick && again == TextStaffSay.None, "texts v6, staff: off sick texted in the morning, once; one message a day");
        s.AskDay = 12;
        s.AskTo = 1800;
        Check(TextsCityRules.Staff(s, m, 12, 7) == TextStaffSay.None && TextsCityRules.Staff(s, m, 12, 10) == TextStaffSay.RaiseAsk,
            "texts v6, staff: a raise asked for texted in business hours the day the company records it");
        s.Raise = 200;
        Check(TextsCityRules.Staff(s, m, 12, 11) == TextStaffSay.None, "texts v6, staff: one message a day at most (a raise agreed the day they asked is thanked for the next)");
        Check(TextsCityRules.Staff(s, m, 13, 21) == TextStaffSay.None && TextsCityRules.Staff(s, m, 13, 10) == TextStaffSay.RaiseThanks && TextsCityRules.Staff(s, m, 14, 10) == TextStaffSay.None,
            "texts v6, staff: thanks for a raise once it is seen to rise (in business hours), once");
        s.WeekHours = 46;
        s.Complaining = true;
        TextStaffSay complaining = TextsCityRules.Staff(s, m, 15, 10);
        s.Complaining = false;
        TextStaffSay hours = TextsCityRules.Staff(s, m, 16, 10);
        TextStaffSay sameWeek = TextsCityRules.Staff(s, m, 17, 10);
        TextStaffSay nextWeek = TextsCityRules.Staff(s, m, 23, 10);
        Check(complaining == TextStaffSay.None && hours == TextStaffSay.Hours && sameWeek == TextStaffSay.None && nextWeek == TextStaffSay.Hours && TextsCityRules.Week(16) == 2 && TextsCityRules.Week(23) == 3,
            "texts v6, staff: hours past what they agreed to texted once a game week, never while the game's own complaint is open");
        Check(TextsCityRules.Staff(null, m, 1, 10) == TextStaffSay.None && TextsCityRules.Staff(s, null, 1, 10) == TextStaffSay.None, "texts v6, staff: nobody, nothing");
    }

    private static void TextsCityLife()
    {
        var mark = new TextLifeMark();
        var now = new TextLife { Day = 10, Children = 1, Married = true, MarriedDay = 5, Business = "Sam's Deli", BusinessDay = 3, Worth = 100000, HasWorth = true };
        var events = new List<TextLifeEvent>();
        Check(TextsCityRules.Life(now, mark, 25000, events) == 0 && mark.Day == 10 && mark.Children == 1 && mark.WorthDay == 10, "texts v6, life: the first look of a save only notes where things stand");
        now = new TextLife
        {
            Day = 11, Children = 2, ChildName = "Leila", Adopted = true, Partner = "Sam", Married = true, MarriedDay = 11, Degree = "LLB Law", CourtClosed = 1, CourtGuilty = false,
            Listed = "Hudson Logistics", ListedDay = 11, Business = "Bean There", BusinessDay = 11, Worth = 160000, HasWorth = true
        };
        int found = TextsCityRules.Life(now, mark, 25000, events);
        var kinds = new List<string>();
        foreach (TextLifeEvent e in events)
        {
            kinds.Add(e.Kind + (e.Value != null ? ":" + e.Value : string.Empty));
        }
        Check(found == 7 && string.Join(", ", kinds) == "baby:Sam, married:Sam, graduated:LLB Law, court_cleared, listed:Hudson Logistics, opened:Bean There, win:60000" && events[0].Child == "Leila" && events[0].Adopted,
            "texts v6, life: what changed since the last look: a child, a wedding, a graduation, a case closed, a listing, a business, a big win (" + string.Join(", ", kinds) + ")");
        events.Clear();
        now.Day = 11;
        now.Worth = 20000;
        Check(TextsCityRules.Life(now, mark, 25000, events) == 0, "texts v6, life: nothing twice: each change once, the worth once a day");
        now.Day = 12;
        now.Degree = null;
        now.Worth = 130000;
        int small = TextsCityRules.Life(now, mark, 25000, events);
        now.Day = 13;
        now.Worth = 60000;
        int loss = TextsCityRules.Life(now, mark, 25000, events);
        Check(small == 0 && loss == 1 && events[0].Kind == "loss" && events[0].Value == "70000", "texts v6, life: a big win or loss is a move of " + TextsCityRules.BigShare + "% of the worth, and at least the game's floor");
        now.Day = 14;
        now.Worth = 76000;
        events.Clear();
        Check(TextsCityRules.Life(now, mark, 25000, events) == 0, "texts v6, life: a move under the floor is no news, a quarter of the worth or not");
        var e1 = new TextLifeEvent { Kind = "court_guilty" };
        var e2 = new TextLifeEvent { Kind = "baby", Adopted = false };
        Check(TextsCityRules.Branch(e1) == "guilty" && TextsCityRules.Branch(e2) == "born" && TextsCityRules.Branch(new TextLifeEvent { Kind = "crash" }) == null
              && TextsCityRules.SenderOrder("arrest").Length == 0 && TextsCityRules.SenderOrder("crash")[0][0] == "partner",
            "texts v6, life: a card's branch by the event; an arrest is the social life's own text already");
        var prune = new List<TextLifeEvent>();
        for (int i = 0; i < 40; i++)
        {
            prune.Add(new TextLifeEvent { Kind = "win", Day = 100 + i });
        }
        prune.Insert(0, new TextLifeEvent { Kind = "crash", Day = 10 });
        int gone = TextsCityRules.Prune(prune, 140);
        Check(prune.Count == TextsCityRules.EventsMost && prune[0].Day == 108 && gone == 9, "texts v6, life: the newest " + TextsCityRules.EventsMost + " events kept, none older than " + TextsCityRules.EventDays + " days");
        Check(TextsCityRules.ReviewAbout(5, 80, 40, 60, 90) == "interior" && TextsCityRules.ReviewAbout(2, 80, 40, 60, 90) == "prices" && TextsCityRules.ReviewAbout(4, 70, 70, 10, 10) == "service"
              && TextsCityRules.ReviewAbout(1, 10, 10, 90, 90) == "service" && TextWords.Rating(75) == "very good" && TextWords.Rating(55) == "good" && TextWords.Rating(35) == "fair" && TextWords.Rating(34) == "poor",
            "texts v6, reviews: a good review is mostly about the business's best figure, any other about its worst, a tie the first; each figure in words");
    }

    private static TextPerson Person(int index, int stage, int closeness = 50, bool together = false, bool family = false, int district = 0, int origin = 0, int seed = 0)
    {
        return new TextPerson { Index = index, First = "P" + index, Seed = seed == 0 ? index * 7919 : seed, Stage = stage, Closeness = closeness, Together = together, Family = family, Met = true,
                                District = district, Origin = origin, LastTextDay = -1 };
    }

    private static void TextsCityPeople()
    {
        var people = new List<TextPerson> { Person(0, 5, 90, true), Person(1, 0, 60, false, true), Person(2, 3, 70, district: 1), Person(3, 2, 95, district: 1), Person(4, 3, 70, district: 2) };
        var crash = new TextLifeEvent { Kind = "crash", Day = 20 };
        bool partner = TextsCityRules.Sender(crash, people, 20, out TextLifeSender s1) && s1.Index == 0 && s1.Intent == "partner.crash";
        people[0].Asleep = true;
        bool mom = TextsCityRules.Sender(crash, people, 20, out TextLifeSender s2) && s2.Index == 1 && s2.Intent == "mom.news.crash";
        people[0].Asleep = false;
        Check(partner && mom, "texts v6, who texts: about a crash the partner first, Mom while the partner is asleep");
        var opened = new TextLifeEvent { Kind = "opened", Day = 20, Value = "Bean There" };
        TextsCityRules.Sender(opened, people, 20, out TextLifeSender close);
        people[2].LastTextDay = 20;
        people[4].LastTextDay = 20;
        TextsCityRules.Sender(opened, people, 20, out TextLifeSender friend);
        people[3].Drifted = true;
        TextsCityRules.Sender(opened, people, 20, out TextLifeSender momOpened);
        people[2].LastTextDay = people[4].LastTextDay = -1;
        people[3].Drifted = false;
        Check((close.Index == 2 || close.Index == 4) && close.Intent == "friend.congrats_open" && friend.Index == 3 && momOpened.Index == 1 && momOpened.Intent == "mom.news.opened",
            "texts v6, who texts: a new business, the closest friend (a close friend before a friend, ties by the hash), one who texted today or drifted passed over, then Mom");
        TextsCityRules.Sender(opened, people, 20, out TextLifeSender again);
        Check(again.Index == close.Index && again.Key == close.Key && close.Key.StartsWith("reallife_texts_life_friend_congrats_open_", StringComparison.Ordinal),
            "texts v6, who texts: the same answer for the same day and people on every machine, and its written line's key");
        var nobody = new List<TextPerson> { Person(9, 1) };
        Check(!TextsCityRules.Sender(opened, nobody, 20, out TextLifeSender none) && none.Index == -1, "texts v6, who texts: nobody who may, nobody");
        // gossip
        Check(TextsCityRules.Knows(people[2], people[3]) && !TextsCityRules.Knows(people[2], people[4]) && TextsCityRules.Knows(people[0], people[2]) && !TextsCityRules.Knows(people[2], people[2])
              && TextsCityRules.Knows(Person(10, 2, origin: 3), Person(11, 2, district: 5, origin: 3)) && !TextsCityRules.Knows(Person(10, 2, origin: 0), Person(11, 2, district: 5, origin: 0)),
            "texts v6, gossip: two people know each other by the social life's own data: one district, one place they met the player, the partner and a close friend");
        var heard = new TextLifeEvent { Kind = "opened", Day = 20, Value = "Bean There", Texted = 3 };
        int hour = TextsCityRules.GossipHour(heard);
        bool sameDay = TextsCityRules.Gossip(heard, people, 20, hour, out _, out _);
        bool wrongHour = TextsCityRules.Gossip(heard, people, 21, hour == 23 ? 0 : hour + 1, out _, out _);
        bool passed = TextsCityRules.Gossip(heard, people, 21, hour, out int teller, out int hearer);
        heard.Gossiped = true;
        bool twice = TextsCityRules.Gossip(heard, people, 21, hour, out _, out _);
        bool late = TextsCityRules.Gossip(new TextLifeEvent { Kind = "opened", Day = 20, Texted = 3 }, people, 24, hour, out _, out _);
        bool secret = TextsCityRules.Gossip(new TextLifeEvent { Kind = "win", Day = 20, Texted = 3 }, people, 21, TextsCityRules.GossipHour(new TextLifeEvent { Kind = "win", Day = 20 }), out _, out _);
        Check(!sameDay && !wrongHour && passed && teller == 3 && hearer == 2 && !twice && !late && !secret && hour >= TextsCityRules.GossipHourFrom && hour < TextsCityRules.GossipHourFrom + TextsCityRules.GossipHours,
            "texts v6, gossip: a public event passed on once, from the day after and for " + TextsCityRules.GossipTo + " days, at its hour, by somebody who knows the one who texted (" + teller + " to " + hearer + "); money is nobody's business");
        Check(TextsCityRules.GossipKind("court_guilty") == "court" && TextsCityRules.Public("arrest") && !TextsCityRules.Public("loss") && !TextsCityRules.Public("knocked"),
            "texts v6, gossip: what is public: a crash, an arrest, a court case, a business, a graduation, a listing, a wedding, a baby");
    }

    // Every written line the city's talk may pick is in en, pt and zh-cn.
    private static void TextsCityKeys()
    {
        var keys = new List<string>();
        foreach (string kind in new[] { "crash", "knocked", "court_guilty", "court_cleared", "graduated", "listed", "opened", "married", "baby", "win", "loss" })
        {
            foreach (string[] who in TextsCityRules.SenderOrder(kind))
            {
                foreach (bool adopted in new[] { false, true })
                {
                    string branch = TextsCityRules.Branch(new TextLifeEvent { Kind = kind, Adopted = adopted });
                    for (uint h = 0; h < 2; h++)
                    {
                        keys.Add(TextsCityRules.LifeKey(who[1], branch, h));
                    }
                }
            }
        }
        foreach (string kind in new[] { "crash", "arrest", "court_guilty", "court_cleared", "opened", "graduated", "listed", "married", "baby" })
        {
            for (uint h = 0; h < 2; h++)
            {
                keys.Add(TextsCityRules.GossipKey(kind, h));
            }
        }
        foreach (string word in new[] { "raise_ask", "sick", "raise_thanks", "hours" })
        {
            keys.Add(StaffStemSix + word + "_1");
            keys.Add(StaffStemSix + word + "_2");
        }
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), lang + ".json")));
            foreach (string key in keys)
            {
                if (!doc.RootElement.TryGetProperty(key, out _))
                {
                    missing.Add(lang + ":" + key);
                }
            }
        }
        var mapped = new List<string>();
        foreach (string key in keys)
        {
            if (key.StartsWith(TextsCityRules.LifeStem, StringComparison.Ordinal) || key.StartsWith(TextsCityRules.GossipStem, StringComparison.Ordinal))
            {
                string intent = TextsMapRules.Phone(key, TextsMapRules.From.Friend, out string branch);
                if (intent == null || TextIntents.Find(intent, branch, TextIntents.DataV6) == null)
                {
                    mapped.Add(key);
                }
            }
        }
        Check(missing.Count == 0 && keys.Count > 80, "texts v6, the city's talk: every written line it may pick is in en, pt and zh-cn (" + keys.Count + " keys; missing: " + (missing.Count == 0 ? "none" : string.Join(", ", missing)) + ")");
        Check(mapped.Count == 0, "texts v6, the city's talk: every life text and gossip line has its card of data v6 (without: " + (mapped.Count == 0 ? "none" : string.Join(", ", mapped)) + ")");
    }

    private static void TextsCityStoreSix()
    {
        var store = new TextsCityStore();
        store.Mark.Day = 30;
        store.Mark.Children = 1;
        store.Mark.Worth = 123456789012;
        store.Mark.WorthDay = 30;
        store.Events.Add(new TextLifeEvent { Kind = "opened", Day = 29, Value = "Sam's|Deli^%", Texted = 3, Done = true });
        store.Events.Add(new TextLifeEvent { Kind = "baby", Day = 30, Value = "Sam", Child = "Leila", Adopted = true });
        TextStaffMark m = store.StaffMark("e:1|2");
        m.RaiseSeen = 150;
        m.HoursWeek = 4;
        m.SeenDay = 30;
        string line = store.Write();
        var back = new TextsCityStore();
        bool read = back.Read(line);
        Check(read && back.Write() == line && back.Events[0].Value == "Sam's|Deli^%" && back.Events[1].Adopted && back.Events[1].Child == "Leila" && back.Staff[0].Id == "e:1|2" && back.Mark.Worth == 123456789012,
            "texts v6, the city's save line read back as written (" + line + ")");
        Check(!back.Read("v=2^n=1|0|0|0|0|0|0|0|-1") && back.Mark.Day == -1 && !back.Read("v=1^e=x") && back.Events.Count == 0 && back.Read("v=1^z=later^n=5|0|0|0|0|0|0|0|-1|later") && back.Mark.Day == 5
              && back.Read(null) && back.Mark.Day == -1,
            "texts v6, the city's save line: a later version's or a damaged line gives none (kept in the save as read); a later record or field passed over");
        var many = new TextsCityStore();
        for (int i = 0; i < TextsCityStore.StaffMost + 5; i++)
        {
            many.StaffMark("e" + i).SeenDay = 100;
        }
        many.StaffMark("e400").SeenDay = 90;
        int gone = many.Prune(100);
        Check(many.Staff.Count == TextsCityStore.StaffMost - 1 && gone == 1 && many.StaffMark(null) == null, "texts v6, the city's save line: at most " + TextsCityStore.StaffMost + " staff marks, those not seen for a week let go");
        Check(TextsCityStore.SaveKey == "reallife:texts_city" && TextsCityStore.Version == 1, "texts v6, the city's save line: its own key in the save");
    }

    // RLO Texts Large: its tier, where it may run, when it is too slow, when it is offered.
    private static void TextsLarge()
    {
        Check(TextPacks.Tier(0, 4, false, true) == TextPacks.LargeTier && TextPacks.Tier(2, 4, true, true) == TextPacks.LargeTier && TextPacks.Tier(2, 0, true, false) == TextPacks.FullTier
              && TextPacks.Tier(0, 5, false, true) == 0 && TextPacks.Tier(3, 4, true, true) == 0,
            "texts v6, RLO Texts Large: its tier 4 read (with no rank line, as an RLO before the tier line, or one before data v6, reads no pack at all); a rank alone is never Large");
        var large = new TextPack { Name = "RLO Texts Large", Rank = TextPacks.LargeTier, ModelBytes = TextSpeed.LargeBytes, Data = TextIntents.DataV6 };
        var full = new TextPack { Name = "RLO Texts", Rank = TextPacks.FullTier, ModelBytes = 1L << 30, Data = TextIntents.DataV6 };
        var both = new List<TextPack> { large, full };
        var card = new List<TextDevice> { new TextDevice { Name = "Vulkan0", Label = "NVIDIA GeForce RTX 4080", TotalMiB = 16384, FreeMiB = 14000 } };
        var small = new List<TextDevice> { new TextDevice { Name = "Vulkan0", Label = "NVIDIA GeForce RTX 3060", TotalMiB = 8192, FreeMiB = 6000 } };
        var builtIn = new List<TextDevice> { new TextDevice { Name = "Vulkan0", Label = "Intel(R) UHD Graphics 770", TotalMiB = 16384, FreeMiB = 15000 } };
        bool runs = TextPacks.Pick(both, card, 0, null, out string why1) == large && why1 == null;
        bool processor = TextPacks.Pick(both, card, 1, null, out string why2) == full && why2 != null;
        bool memory = TextPacks.Pick(both, small, 0, null, out string why3) == full && why3.Contains("MB");
        bool igpu = TextPacks.Pick(both, builtIn, 0, null, out _) == full;
        bool slow = TextPacks.Pick(both, card, 0, "NVIDIA GeForce RTX 4080", out string why4) == full && why4.Contains("too slow");
        bool alone = TextPacks.Pick(new List<TextPack> { large }, small, 0, null, out _) == null;
        Check(runs && processor && memory && igpu && slow && alone, "texts v6, RLO Texts Large: run only on a graphics card with the memory for it (never the processor or built-in graphics), and not where it was too slow before; else the next pack");
        Check(TextSpeed.LargeTooSlow(1.21) && !TextSpeed.LargeTooSlow(TextSpeed.LargeSlow) && TextSpeed.LargeTooSlow(double.NaN) && !TextSpeed.LargeTooSlow(0.4),
            "texts v6, RLO Texts Large: too slow past " + TextSpeed.LargeSlow + " s a reply on its card (the next pack runs, and Large is not tried on that card again)");
        Check(TextSpeed.OfferLarge(TextPacks.FullTier, 0.3, true, true) && !TextSpeed.OfferLarge(TextPacks.FullTier, 0.3, true, false) && !TextSpeed.OfferLarge(TextPacks.FullTier, 0.6, true, true)
              && !TextSpeed.OfferLarge(2, 0.3, true, true) && !TextSpeed.OfferLarge(TextPacks.FullTier, 0.3, false, true) && TextPacks.LargePublished == (TextPacks.WorkshopItems[3].Length > 0),
            "texts v6, RLO Texts Large: offered once where the full pack is fast on a card with the memory, and only once it is published (its Workshop id set)");
        // (the offer reckons Large at its published model, 2,708,804,096 bytes: twice that and the spare, 6,190 MB, free on the card,
        // as Pick asks once it is installed. A 12 GB card beside the game has it; an 8 GB card with 6,000 MB free does not)
        var fits = new TextDevice { Name = "Vulkan0", Label = "NVIDIA GeForce RTX 4070", TotalMiB = 12282, FreeMiB = 6190 };
        var under = new TextDevice { Name = "Vulkan0", Label = "NVIDIA GeForce RTX 4070", TotalMiB = 12282, FreeMiB = 6189 };
        Check(TextSpeed.LargeBytes == 2708804096L && TextSpeed.LargeFits(fits, TextSpeed.LargeBytes) && !TextSpeed.LargeFits(under, TextSpeed.LargeBytes)
              && TextSpeed.LargeFits(card[0], TextSpeed.LargeBytes) && !TextSpeed.LargeFits(small[0], TextSpeed.LargeBytes),
            "texts v6, RLO Texts Large: the offer reckons its published model (" + TextSpeed.LargeBytes + " bytes), so it asks "
            + (TextSpeed.LargeBytes / (1024 * 1024) * 2 + TextPacks.SpareMiB) + " MB free on the card, as Pick does once the pack is installed");
        Check(TextSpeed.SuggestLite(2, 9) && TextSpeed.SuggestLite(TextPacks.FullTier, 9) && !TextSpeed.SuggestLite(TextPacks.LargeTier, 9) && !TextSpeed.SuggestLite(1, 9),
            "texts v6, RLO Texts Large: never told to try the Lite pack (too slow, it gives way by itself)");
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-large-" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(System.IO.Path.Combine(folder, "model"));
        Directory.CreateDirectory(System.IO.Path.Combine(folder, "runtime"));
        File.WriteAllText(System.IO.Path.Combine(folder, "model", "large.gguf"), "x");
        File.WriteAllText(System.IO.Path.Combine(folder, "runtime", "llama-server.exe"), "x");
        File.WriteAllText(System.IO.Path.Combine(folder, "pack.txt"), "name=RLO Texts Large\nmodel=model/large.gguf\nexe=runtime/llama-server.exe\ntier=4\nversion=1\ndata=6\nlanguages=en,pt,zh-cn,de,fr,es,ru\n");
        TextPack read = TextPacks.Read(folder);
        try
        {
            Directory.Delete(folder, true);
        }
        catch (IOException)
        {
        }
        Check(read != null && read.Rank == TextPacks.LargeTier && read.Data == TextIntents.DataV6 && read.Languages.Length == 7, "texts v6, RLO Texts Large: its pack.txt (tier 4, data 6, seven languages, no rank line) read");
    }

    // Source checks of what data v6 added: nothing per frame, every entry point guarded, nothing sent, no Harmony.
    private static void TextsSixSources()
    {
        string city = TextsSource("Texts/TextsDirector.City.cs"), memory = TextsSource("Texts/TextsDirector.Memory.cs"), cityGame = TextsSource("Texts/TextsGame.City.cs");
        string wiring = TextsSource("Wiring/Wiring.TextsCity.cs"), links = TextsSource("Wiring/Wiring.TextsMemory.cs"), street = TextsSource("Wiring/StreetTexts.cs"), share = TextsSource("Texts/TextsShare.cs");
        string director = TextsSource("Texts/TextsDirector.cs"), hub = TextsSource("Texts/TextsHub.cs"), textsLinks = TextsSource("Wiring/TextsLinks.cs");
        Check(city.Contains("(save.Hour == _hour && save.Day == _hourDay)") && !city.Contains("void Update(") && !memory.Contains("void Update(") && !cityGame.Contains("void Update(")
              && !wiring.Contains("void Update(") && !links.Contains("void Update("),
            "texts v6, sources: the city's talk runs once a game hour (one comparison in each look), nothing of it per frame");
        int guarded = 0;
        foreach (string bridge in new[] { "Fault(\"RLO Texts, the staff\"", "Fault(\"RLO Texts, the player's life\"", "Fault(\"RLO Texts, the people\"", "Fault(\"RLO Texts, a person's text\"" })
        {
            guarded += wiring.Contains(bridge) ? 1 : 0;
        }
        Check(guarded == 4 && street.Contains("Fault(\"RLO Texts, what a stranger remembers\"") && textsLinks.Contains("Fault(\"RLO Texts, a review's figures\"") && System.Text.RegularExpressions.Regex.IsMatch(city, @"City\(save\.Day, save\.Hour, Six\);\s*\}\s*catch \(Exception e\)\s*\{\s*Logger\?\.Error\(e\);"),
            "texts v6, sources: every entry point of the city's talk, a stranger's memory and a review's figures is guarded (a fault is logged, the game goes on)");
        Check(!share.Contains("TextMemoryStore") && !share.Contains("TextsCityStore") && !share.Contains("Memory") && hub.Contains("if (!shared && memory != null && memory.Count > 0)"),
            "texts v6, sources: what people remember and the city's talk are each player's own: never in the shared part, never on a shared card");
        int tally = city.IndexOf("Hub.Tally.Line(", StringComparison.Ordinal);
        Check(tally > 0 && city.LastIndexOf("if (Meta.Dev.Verbose)", tally, StringComparison.Ordinal) > tally - 160, "texts v6, sources: the hourly line of what was shown only with Developer diagnostics on");
        var harmony = new List<string>();
        foreach (string file in new[] { "Texts/TextLanguages.cs", "Texts/TextSlotKinds.cs", "Texts/TextMemoryRules.cs", "Texts/TextMemoryStore.cs", "Texts/TextMemoryTopics.cs", "Texts/TextIntents.Six.cs",
                     "Texts/TextCheck.Six.cs", "Texts/TextDecoding.cs", "Texts/TextShown.cs", "Texts/TextCityViews.cs", "Texts/TextsCityRules.cs", "Texts/TextsCityStore.cs", "Texts/TextsBridges.cs",
                     "Texts/TextsDirector.Memory.cs", "Texts/TextsDirector.City.cs", "Texts/TextsGame.City.cs", "Wiring/Wiring.TextsCity.cs", "Wiring/Wiring.TextsMemory.cs" })
        {
            if (TextsSource(file).Contains("Harmony"))
            {
                harmony.Add(file);
            }
        }
        string tests = TextsSource("Texts/TextsDirector.Tests.cs");
        var whyKeys = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(tests, "\"reallife_texts_why_([a-z]+)\""))
        {
            whyKeys.Add(m.Groups[1].Value);
        }
        var ruleKeys = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(tests, "\"reallife_texts_rule_([a-z]+)\""))
        {
            ruleKeys.Add(m.Groups[1].Value);
        }
        var whyNames = new List<string>(Enum.GetNames(typeof(TextShownWhy)));
        whyNames.RemoveAt(0);
        var ruleNames = new List<string>(Enum.GetNames(typeof(TextVerdict)));
        ruleNames.RemoveAt(0);
        Check(string.Join(",", whyKeys) == string.Join(",", whyNames).ToLowerInvariant() && string.Join(",", ruleKeys) == string.Join(",", ruleNames).ToLowerInvariant(),
            "texts v6, sources: the readout's reasons and rules are in the order of TextShownWhy and TextVerdict (" + string.Join(",", whyKeys) + "; " + string.Join(",", ruleKeys) + ")");
        Check(harmony.Count == 0, "texts v6, sources: no Harmony in anything data v6 added (" + (harmony.Count == 0 ? "none" : string.Join(", ", harmony)) + ")");
        Check(TextsSource("Core/Settings.cs").Contains("public static bool TextsCity = true;") && TextsSource("Meta/OptionsTree.cs").Contains(".Opt(\"texts_city\", \"texts\", \"texts_people\")")
              && city.Contains("StaffStem = \"reallife\" + \"_texts_staff_\";"),
            "texts v6, sources: the city's talk has its own option, on by default");
    }
}
