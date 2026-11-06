using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Roommates;

// One face for one person (Scripts/Phone/Contacts/FixedPeople.cs, Social/SocialCatalogue.cs FixLook and SameAs, Home/Roommates/
// RoommateRules.cs Mend). A player's Alex was a woman at home, as the roommate, and a man at gatherings: the roommate kept a copy of
// the friend that an older save never had put right. Now Alex and Mom are two fixed characters (one table, the same for every
// player, new game or old save, like Uncle Fred), everybody else the mod keeps has the one look of the one record a copy follows,
// every spawn path reads that look, and a save with two looks for one person is mended on load, once, with a log line and a notice.
internal static partial class Program
{
    private static void OnePerson()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        OneTable();
        OneNewGame();
        OneOldSave();
        OneCopies();
        OneReport();
        OneSources();
        OneTexts();
        SocialState.Reset();
        Console.WriteLine("  one person, one look: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void OneDrain()
    {
        var lines = new List<string>();
        var names = new List<string>();
        FixedPeople.Take(lines, names);
    }

    // The table: two women, two different seeds, nobody else.
    private static void OneTable()
    {
        bool mom = FixedPeople.TryGet(0, out FixedPeople.Entry m), alex = FixedPeople.TryGet(1, out FixedPeople.Entry a);
        Check(mom && alex && m.Name == "Mom" && a.Name == "Alex" && m.Female && a.Female && m.Seed != a.Seed && m.Age == FixedPeople.MomAge && a.Age == FixedPeople.AlexAge
              && m.Seed == FixedPeople.MomSeed && a.Seed == FixedPeople.AlexSeed,
            "one person: the table has Mom (a woman of " + FixedPeople.MomAge + ") and Alex (a woman of " + FixedPeople.AlexAge + ") with seeds of their own, from the constants");
        Check(!FixedPeople.TryGet(2, out _) && !FixedPeople.TryGet(-1, out _) && !FixedPeople.TryGet(SocialRules.MaxCast - 1, out _), "one person: nobody else is a fixed character");
        Check(ContactCatalogue.TryGet("reallife-friend", out ContactLook f) && f.Female && f.Seed == FixedPeople.AlexSeed && f.Age == FixedPeople.AlexAge
              && ContactCatalogue.TryGet("reallife-mom", out ContactLook k) && k.Female && k.Seed == FixedPeople.MomSeed && k.Age == FixedPeople.MomAge,
            "one person: their contact pictures in the catalogue are the same two looks");
    }

    // A new game: whatever the seed, the same two; the gender is never rolled for them; the people to be met still are.
    private static void OneNewGame()
    {
        var people = new SocialPerson[SocialRules.BaseCast];
        bool same = true, women = true, genders = false;
        for (int seed = 1; seed <= 3000; seed += 7)
        {
            SocialCatalogue.MakeCast(people, seed, 0, 112, 9);
            for (int i = 0; i < 2; i++)
            {
                FixedPeople.TryGet(i, out FixedPeople.Entry want);
                SocialPerson p = people[i];
                same &= p.Name == want.Name && p.Seed == want.Seed && p.Age == want.Age && p.Female == want.Female && p.Look == null;
                women &= p.Female;
            }
            genders |= people[2].Female != people[3].Female;
        }
        Check(same && women && genders, "one person: for every new game's seed Mom and Alex are the same two women, and the people to be met are still dealt each game's own");
        // A person made by the cast's own rolls: not a fixed one, never touched.
        var other = new SocialPerson { Index = 4, Origin = SocialOrigin.Gym, Name = "Dana Reyes", Female = false, Age = 40, Seed = 5 };
        Check(!SocialCatalogue.FixLook(other) && !other.Female && other.Age == 40 && other.Seed == 5 && !SocialCatalogue.FixedFor(other, "a test"), "one person: nobody but the two is changed by the fixed look");
        OneDrain();
    }

    // The player's old save: Alex a man with a seed of his own in the social life, as the previous version left it; Mom with another.
    private static void OneOldSave()
    {
        OneDrain();
        var people = new SocialPerson[SocialRules.BaseCast];
        SocialCatalogue.MakeCast(people, 777, 0, 112, 9);
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 0, 112, 9, 777);
        SocialState.Save(data);
        string[] records = data["reallife:social_people"].Split('\n');
        records[0] = OneField(records[0], 2, "0");
        records[0] = OneField(records[0], 3, "63");
        records[0] = OneField(records[0], 4, "4455");
        records[1] = OneField(records[1], 2, "0");
        records[1] = OneField(records[1], 3, "27");
        records[1] = OneField(records[1], 4, "31337");
        data["reallife:social_people"] = string.Join("\n", records);
        string thirdName = SocialState.Get(2).Name;
        SocialState.Load(data, 9, 112, 9, 1);
        SocialPerson mom = SocialState.Get(0), alex = SocialState.Get(1);
        Check(mom.Female && mom.Age == FixedPeople.MomAge && mom.Seed == FixedPeople.MomSeed && alex.Female && alex.Age == FixedPeople.AlexAge && alex.Seed == FixedPeople.AlexSeed
              && alex.Name == "Alex" && mom.Name == "Mom" && SocialState.Get(2).Name == thirdName,
            "one person: an old save's Mom and Alex, a man of 27 with a seed of his own among them, take the fixed look as the save is read; names and the rest are as they were");
        var lines = new List<string>();
        var names = new List<string>();
        FixedPeople.Take(lines, names);
        Check(lines.Count == 2 && names.Count == 2 && names.Contains("Alex") && names.Contains("Mom") && lines[1].Contains("Alex") && lines[1].Contains("a woman of 30") && lines[1].Contains("seed " + FixedPeople.AlexSeed),
            "one person: one log line and one notice each for the two put right (" + lines.Count + " lines, " + names.Count + " names)");
        // Saved so, read again: nothing left to put right, nothing said.
        var again = new Dictionary<string, string>();
        SocialState.Save(again);
        SocialState.Load(again, 10, 112, 9, 1);
        Check(!FixedPeople.Waiting && SocialState.Get(1).Seed == FixedPeople.AlexSeed, "one person: and once saved they are not put right or told of again");
        Check(records[1].Split('|')[2] == "0" && SocialState.Get(1).Female, "one person: the repair turned the old save's man into the woman the player met");
        OneDrain();
    }

    private static string OneField(string record, int index, string value)
    {
        string[] f = record.Split('|');
        f[index] = value;
        return string.Join("|", f);
    }

    private static Flatmate OneMate(int friend, string name, bool female, int age, int seed)
    {
        // As the roommate's store gives one back: a person of the neighbour's origin, with a place in the cast.
        return new Flatmate { Person = new SocialPerson { Index = friend, Origin = SocialOrigin.Neighbour, Name = name, Female = female, Age = age, Seed = seed, Met = true }, Friend = friend };
    }

    // The roommate's copy: it follows the one record, and a fixed person's copy is the fixed look with or without the record.
    private static void OneCopies()
    {
        OneDrain();
        // The report as the player gave it: Alex, the start's friend, a woman as the roommate at home, a man in the social life.
        var data = new Dictionary<string, string>();
        var people = new SocialPerson[SocialRules.BaseCast];
        SocialCatalogue.MakeCast(people, 55, 0, 112, 9);
        SocialState.Load(data, 0, 112, 9, 55);
        SocialState.Save(data);
        string[] records = data["reallife:social_people"].Split('\n');
        records[1] = OneField(OneField(records[1], 2, "0"), 4, "52817");   // the social life's Alex: a man of the previous version's fixed seed
        data["reallife:social_people"] = string.Join("\n", records);
        SocialState.Load(data, 3, 112, 9, 1);
        Flatmate home = OneMate(1, "Alex", true, 33, 424242);   // the roommate's copy, made before: a woman with a look of her own
        bool changedHome = RoommateRules.Mend(home, SocialState.Get(1));
        SocialPerson social = SocialState.Get(1);
        Check(changedHome && home.Person.Female == social.Female && home.Person.Age == social.Age && home.Person.Seed == social.Seed && social.Female && social.Seed == FixedPeople.AlexSeed,
            "one person: Alex at home and Alex at a gathering, as the player's old save had them apart, are one woman with one look after the repair");
        Check(!RoommateRules.Mend(home, social), "one person: and the roommate's copy is not changed twice");
        Check(RoommateRules.Mend(OneMate(0, "Mom", false, 40, 11), null) && !RoommateRules.Mend(OneMate(1, "Alex", true, FixedPeople.AlexAge, FixedPeople.AlexSeed), null),
            "one person: a fixed person's copy is put right with or without the social life's record, and left alone when it is right");
        // Anybody else the roommate is: the record they were first met as wins, the record itself is never changed.
        SocialPerson met = SocialState.Get(3);
        met.Met = true;
        bool wasFemale = met.Female;
        int wasAge = met.Age, wasSeed = met.Seed;
        Flatmate friend = OneMate(3, met.Name, !wasFemale, wasAge + 5, wasSeed + 17);
        Check(RoommateRules.Mend(friend, met) && friend.Person.Female == wasFemale && friend.Person.Age == wasAge && friend.Person.Seed == wasSeed
              && met.Female == wasFemale && met.Age == wasAge && met.Seed == wasSeed,
            "one person: a friend who is the roommate follows the record the player met first, and the record is never the one that changes");
        Check(!RoommateRules.Mend(OneMate(3, "Somebody Else", !wasFemale, 50, 9), met) && !RoommateRules.Mend(OneMate(3, met.Name, wasFemale, wasAge, wasSeed), null)
              && !RoommateRules.Mend(OneMate(-1, met.Name, !wasFemale, 50, 9), met) && !RoommateRules.Mend(null, met) && !RoommateRules.Mend(new Flatmate { Friend = 3 }, met),
            "one person: a roommate who is not that friend, whose record is not known yet, who is no friend or who is nobody is left as they are");
        // Through the roommate's own save line, as an old save holds them.
        var state = new RoommateState { Seed = 5, LastDay = 3 };
        state.MoveIn(OneMate(1, "Alex", true, 33, 424242), 1, "Test Street", 4, false, false, 100);
        int version;
        RoommateState back = RoommateStore.Read(RoommateStore.Write(state), out version);
        bool changedBack = back != null && back.Mate != null && RoommateRules.Mend(back.Mate, SocialState.Get(back.Mate.Friend));
        Check(back != null && back.Mate != null && changedBack && back.Mate.Person.Female && back.Mate.Person.Seed == FixedPeople.AlexSeed && back.Mate.Person.Name == "Alex",
            "one person: the roommate read from an old save's line is put right to the fixed look (Friend " + (back != null && back.Mate != null ? back.Mate.Friend : -9) + ")");
        OneDrain();
    }

    // What is said: one line each, one notice for a person even if two records were put right, nothing for a rolled look; nothing allocated when nothing is.
    private static void OneReport()
    {
        OneDrain();
        var lines = new List<string>();
        var names = new List<string>();
        FixedPeople.Forget();
        FixedPeople.Mended("Alex", "the social life's people", true, 30, 52817);
        FixedPeople.Mended("Alex", "the roommate's record", true, 30, 52817);
        FixedPeople.Take(lines, names);
        Check(lines.Count == 2 && names.Count == 1 && names[0] == "Alex" && !FixedPeople.Waiting, "one person: Alex put right in two records is two log lines and one notice");
        FixedPeople.Mended("Alex", "the roommate's record", true, 30, 52817);
        FixedPeople.Take(lines, names);
        Check(lines.Count == 1 && names.Count == 0, "one person: and told of once more, a later repair is logged but not told again in the same save");
        FixedPeople.Forget();
        FixedPeople.Mended("Mom", "a scene", true, 58, 77171);
        FixedPeople.Take(lines, names);
        Check(lines.Count == 1 && names.Count == 1, "one person: a new save is told of again");
        var right = new SocialPerson { Index = 1, Origin = SocialOrigin.OldFriend, Name = "Alex", Female = FixedPeople.AlexFemale, Age = FixedPeople.AlexAge, Seed = FixedPeople.AlexSeed };
        long bytes = OneFixedForBytes(right);
        Check(bytes < 2048 && !FixedPeople.Waiting, "one person: standing somebody who is right already allocates nothing and says nothing (" + bytes + " bytes in 20000 calls)");
        OneDrain();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long OneFixedForBytes(SocialPerson right)
    {
        SocialCatalogue.FixedFor(right, "a test");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20000; i++)
        {
            SocialCatalogue.FixedFor(right, "a test");
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Every place that stands a person in the world or draws them takes the look from the one record, or for Mom and Alex from the table.
    private static void OneSources()
    {
        string table = ModSource(@"Phone\Contacts\FixedPeople.cs");
        string catalogue = ModSource(@"Social\SocialCatalogue.cs");
        string cast = ModSource(@"Social\SocialCast.cs");
        string mates = ModSource(@"Home\Roommates\RoommateCast.cs");
        string family = ModSource(@"Family\FamilyCast.cs");
        string familyPhone = ModSource(@"Family\FamilyPhone.cs");
        string flat = ModSource(@"Home\Roommates\RoommateFlat.cs");
        string phone = ModSource(@"Home\Roommates\RoommatePhone.cs");
        string director = ModSource(@"Home\Roommates\RoommateDirector.cs");
        string state = ModSource(@"Social\SocialState.cs");
        Check(table.Contains("AlexFemale = true") && table.Contains("MomFemale = true") && table.Contains("THE ONE PLACE"), "one person: the table of the two fixed people is in one place, FixedPeople.cs, and says it");
        // The seeds are written once in the whole mod.
        int outside = 0;
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (!file.EndsWith("FixedPeople.cs", StringComparison.Ordinal))
            {
                string text = File.ReadAllText(file);
                outside += text.Contains(FixedPeople.AlexSeed.ToString()) || text.Contains(FixedPeople.MomSeed.ToString()) ? 1 : 0;
            }
        }
        Check(outside == 0, "one person: Mom's and Alex's seeds are written only in the table (" + outside + " other files hold them)");
        string fix = MemberText(catalogue, "public static bool FixLook(SocialPerson p)");
        Check(fix.Contains("FixedPeople.TryGet((int)p.Origin") && fix.Contains("p.Female = fixedLook.Female") && fix.Contains("p.Seed = fixedLook.Seed") && fix.Contains("p.Look = null"),
            "one person: the social life's Mom and Alex are set from the table alone, their street look dropped");
        string make = catalogue.Substring(catalogue.IndexOf("public static void MakeCast", StringComparison.Ordinal));
        make = make.Substring(0, make.IndexOf("OneTrainer(people, jobs);", StringComparison.Ordinal));
        Check(make.IndexOf("FixLook(p);", StringComparison.Ordinal) > make.IndexOf("p.Female = origin == SocialOrigin.Family || random.Next(2) == 0", StringComparison.Ordinal)
              && make.IndexOf("FixLook(p);", StringComparison.Ordinal) > make.IndexOf("Seed = random.Next(1, int.MaxValue)", StringComparison.Ordinal),
            "one person: a new cast sets their look after every roll, so a rolled gender or seed never stays");
        Check(state.Contains("SocialCatalogue.FixedFor(p, \"the social life's people\")") && ModSource(@"Social\SocialDirector.cs").Contains("TellFixedPeople();"),
            "one person: an old save's people are put right as they are read, and the director logs and tells it");
        Check(MemberText(cast, "private static void Dress(CityPerson person, SocialPerson who)").Contains("SocialCatalogue.FixedFor(who"),
            "one person: a gathering, an outing or a meeting dresses them from the fixed look first");
        Check(mates.Contains("SocialCatalogue.FixedFor(who") && mates.IndexOf("SocialCatalogue.FixedFor(who", StringComparison.Ordinal) < mates.IndexOf("long wanted", StringComparison.Ordinal)
              && mates.Contains("Mathf.Clamp(who.Age, 18, 85)"), "one person: the roommate at home is dressed from the fixed look, at the age the social life draws");
        int mendAt = flat.IndexOf("RoommateDirector.Instance?.Mend();", StringComparison.Ordinal);
        int loadAt = director.IndexOf("            Mend();", StringComparison.Ordinal);
        Check(mendAt > 0 && mendAt < flat.IndexOf("_cast.Put(s.Mate.Person", StringComparison.Ordinal) && phone.Contains("d?.Mend();") && loadAt > 0 && loadAt < director.IndexOf("RoommatePhone.Setup();", loadAt, StringComparison.Ordinal),
            "one person: the roommate's copy follows the record when the save is read, before they are shown at home and before their picture is drawn");
        Check(ModSource(@"Wiring\RoommateLinks.cs").Contains("RoommateBridges.Record = SocialState.Get;") && ModSource(@"Home\Roommates\RoommateSettings.cs").Contains("Record = null;"),
            "one person: the roommate reads the social life's record through a bridge that is put up and taken down with the feature");
        // The family: Mom at the wedding is the table's; the partner's age is the one record's, at home, on the phone and out.
        string mother = family.Substring(family.IndexOf("seed = MotherSeed;", StringComparison.Ordinal));
        mother = mother.Substring(0, mother.IndexOf("case FamilyLook.Officiant:", StringComparison.Ordinal));
        Check(mother.Contains("seed = MotherSeed;") && mother.Contains("female = FixedPeople.MomFemale;") && mother.Contains("age = FixedPeople.MomAge;") && !mother.Contains("Random"),
            "one person: Mom at the wedding is the table's look and rolls nothing");
        Check(family.Contains("age = Partner.LookAge;") && familyPhone.Contains("Partner.LookAge") && !familyPhone.Contains("PartnerAge(") && ModSource(@"Wiring\FamilyLinks.cs").Contains("Age = p.Age,")
              && ModSource(@"Family\Partner.cs").Contains("public static int LookAge => Kept(out PersonInfo p) && p.Age >= 18"),
            "one person: the partner is drawn at home, on the phone and out at the age of the one record of them");
        // No gender is rolled where a named person is dressed: each of these takes it from the person.
        Check(CountOf(cast, "Gender.Female") == CountOf(cast, "who.Female ? Gender.Female : Gender.Male") && CountOf(mates, "Gender.Female") == CountOf(mates, "who.Female ? Gender.Female : Gender.Male"),
            "one person: the social and roommate characters take their gender from the person, never a roll");
        string test = ModSource(@"Social\SocialDirector.Fixed.cs");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_social_test_looks\", () => SocialDirector.Instance?.TestOneLook())")
              && test.Contains("Social check (one look per person)") && test.Contains("PASS") && test.Contains("FAIL"),
            "one person: Options, Tests has a button that stands them in each way they are shown and writes PASS or FAIL to the log");
    }

    // The texts: three languages by hand, the fix line in all nine, once each, no double space.
    private static void OneTexts()
    {
        string folder = LocalesFolder();
        string[] keys = { "reallife_social_looks_again", "reallife_social_test_looks", "reallife_social_test_looks_none", "reallife_social_test_looks_done", "reallife_social_test_looks_pass", "reallife_social_test_looks_fail" };
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] fixes =
        {
            "Alex and Mom now have one fixed look", "Alex e Mom agora têm um único rosto fixo", "Alex 和 Mom 现在对所有玩家都只有一个固定的样子",
            "Alex und Mom haben jetzt für alle Spieler ein festes Aussehen", "Alex et Mom ont maintenant un seul visage fixe", "Alex y Mom ahora tienen un solo aspecto fijo",
            "у Alex и Mom теперь один неизменный облик", "Alex en Mom hebben nu voor alle spelers één vast uiterlijk", "Alex ile Mom'un artık tüm oyuncular için tek, sabit bir görünüşü var"
        };
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, codes[l] + ".json"));
            string row = words != null ? NewsRow(words, fixes[l]) : null;
            Check(row != null && row.IndexOf(fixes[l], StringComparison.Ordinal) == row.LastIndexOf(fixes[l], StringComparison.Ordinal) && !row.Contains("  ") && row.IndexOf(fixes[l], StringComparison.Ordinal) > FixAt(row),
                "one person: What's new has the fix line once, among the fixes, in " + codes[l]);
            if (l >= 3)
            {
                continue;
            }
            var bad = new List<string>();
            foreach (string key in keys)
            {
                if (!words.TryGetValue(key, out string text) || text.Length == 0 || text.IndexOf('!') >= 0 || text.Contains(" - ") || text.Contains("—") || text.Contains("–") || text.Contains("  "))
                {
                    bad.Add(key);
                }
            }
            Check(bad.Count == 0 && words["reallife_social_looks_again"].Contains("{name}") && words["reallife_social_looks_again"].Length <= 240
                  && words["reallife_social_test_looks_done"].Contains("{name}") && words["reallife_social_test_looks_done"].Contains("{result}"),
                "one person: the notice and the test's texts are in " + codes[l] + " (" + string.Join(", ", bad) + ")");
        }
    }
}
