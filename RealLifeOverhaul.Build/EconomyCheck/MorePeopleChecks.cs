using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Roommates;

// More people to meet: a save starts with the same eight people for a seed, and every place keeps bringing somebody new up
// to SocialRules.MaxCast. The first eight stay in the record every version reads; the rest are in a record of their own that
// an older version leaves alone. A save of eight loads as it was, never as a new cast. With a big cast only the circle
// (the closest, family, a partner, anyone new) gets in touch of its own accord. A roommate who is one of the later people is
// saved so that an older version reads no friend rather than the wrong one.
internal static partial class Program
{
    private const int MoreYear = 28;        // days in a year for these checks; any length works
    private const int MoreDistricts = 9;

    private const string MorePeopleKey = "reallife:social_people";
    private const string MoreMoreKey = "reallife:social_more";
    private const string MorePlanKey = "reallife:social_plan";
    private const string MoreOfferKey = "reallife:social_offer";

    private const double MoreSaveMs = 10.0;   // a save or a load of the whole cast of 400: behind the game's own save or loading screen
    private const double MoreBarMs = 0.05;    // the circle's bar over 400 people: once an hour of play
    private const double MoreNewMs = 2.0;     // somebody new made with 400 names to keep clear of: once a meeting

    private static readonly SocialOrigin[] MorePlaces =
    {
        SocialOrigin.Gym, SocialOrigin.Work, SocialOrigin.CoffeeCart, SocialOrigin.Neighbour, SocialOrigin.Helped, SocialOrigin.Customer,
        SocialOrigin.Online, SocialOrigin.Event, SocialOrigin.Street
    };

    private static void MorePeople()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        MoreFirstEight();
        MoreOldSave();
        MoreGrowing();
        MoreRoundTrip();
        MoreOlderVersion();
        MoreKeptLines();
        MoreCircle();
        MoreRoommate();
        MoreSources();
        MoreTexts();
        MoreTimings();
        SocialState.Reset();
        Console.WriteLine("  more people to meet: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // Everything of a person a save keeps, as one line to compare.
    private static string MoreText(SocialPerson p)
    {
        if (p == null)
        {
            return "nobody";
        }
        CultureInfo inv = CultureInfo.InvariantCulture;
        string memories = string.Empty;
        for (int m = 0; m < p.Memories.Count; m++)
        {
            memories += (int)p.Memories[m].Code + ":" + p.Memories[m].Day + ",";
        }
        return string.Join("|", p.Index, p.Name, p.Female, p.Age, p.Seed, (int)p.Origin, (int)p.Traits, p.Job, p.District, p.FreeFrom, p.FreeTo, p.Birthday,
            p.Compatible, p.Met, p.MetDay, p.Familiarity.ToString("F1", inv), p.Affection.ToString("F1", inv), p.Trust.ToString("F1", inv), (int)p.Stage, p.StageDay,
            p.MovedIn, p.Drifted, p.LastContactDay, p.Shared, p.Dates, (int)p.Wants, p.Debt, p.Loan, p.BirthdayYear, p.AgeDay, p.Look ?? string.Empty, memories);
    }

    private static List<string> MoreCast()
    {
        var all = new List<string>(SocialState.Count);
        for (int i = 0; i < SocialState.Count; i++)
        {
            all.Add(MoreText(SocialState.Get(i)));
        }
        return all;
    }

    private static bool MoreSame(List<string> a, List<string> b, out string first)
    {
        first = string.Empty;
        if (a.Count != b.Count)
        {
            first = "counts " + a.Count + " and " + b.Count;
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                first = "slot " + i + ": " + a[i] + " against " + b[i];
                return false;
            }
        }
        return true;
    }

    // A whole cast of 400: the first eight rolled from the seed, then somebody new from each place in turn, four in five met.
    private static void MoreFill(int seed)
    {
        SocialState.Load(new Dictionary<string, string>(), 10, MoreYear, MoreDistricts, seed);
        for (int i = 0; SocialState.Count < SocialRules.MaxCast; i++)
        {
            SocialPerson p = SocialCatalogue.MakeNew(SocialState.Count, MorePlaces[i % MorePlaces.Length], i % 3 - 1, SocialState.CastSeed, 10 + i, MoreYear,
                MoreDistricts, SocialState.People, SocialState.Count);
            if (!SocialState.Add(p))
            {
                return;
            }
            if (i % 5 != 4)
            {
                p.Met = true;
                p.MetDay = 10 + i;
                p.Stage = SocialStage.Acquaintance;
                p.Familiarity = 10 + i % 50;
                p.Affection = 12 + i % 40;
                p.Trust = 9 + i % 30;
                p.Remember(MemoryCode.Met, 10 + i);
            }
        }
    }

    // The first eight are dealt as they always were: the same for a seed whatever the cast can hold, and nobody after them.
    private static void MoreFirstEight()
    {
        bool same = true, empty = true, romance = true;
        string fault = string.Empty;
        for (int seed = 1; seed <= 300; seed++)
        {
            var eight = new SocialPerson[SocialRules.BaseCast];
            var all = new SocialPerson[SocialRules.MaxCast];
            SocialCatalogue.MakeCast(eight, seed, 3, MoreYear, MoreDistricts);
            SocialCatalogue.MakeCast(all, seed, 3, MoreYear, MoreDistricts);
            int compatible = 0;
            for (int i = 0; i < SocialRules.BaseCast; i++)
            {
                if (MoreText(eight[i]) != MoreText(all[i]) && fault.Length == 0)
                {
                    fault = "seed " + seed + ", slot " + i;
                }
                same &= MoreText(eight[i]) == MoreText(all[i]);
                compatible += all[i] != null && all[i].Compatible ? 1 : 0;
            }
            for (int i = SocialRules.BaseCast; i < all.Length; i++)
            {
                empty &= all[i] == null;
            }
            romance &= compatible >= 2;
        }
        Check(same, "more people: the first eight a seed deals are the same, field by field, in a cast of 8 and a cast of 400 (300 seeds" + (fault.Length > 0 ? "; " + fault : string.Empty) + ")");
        Check(empty, "more people: a new save's cast is the first eight and nobody else; later people come only when met");
        Check(romance, "more people: at least two of the first eight could still become more than friends, as before");
        Check(SocialRules.BaseCast == 8 && SocialRules.MaxCast == 400 && SocialRules.MeetGapDays == 1,
            "more people: a save starts with 8 people, can know 400, and meets somebody new at most once a game day");
    }

    // A save of eight, as every earlier version wrote it, loads with everybody and everything as it was: not a new cast.
    private static void MoreOldSave()
    {
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 50, MoreYear, MoreDistricts, 777);
        SocialPerson friend = SocialState.Get(3), partner = SocialState.Get(5);
        friend.Met = true;
        friend.MetDay = 5;
        friend.Stage = SocialStage.Friend;
        friend.Familiarity = 41.5f;
        friend.Affection = 38f;
        friend.Trust = 30f;
        friend.Debt = 50;
        friend.BirthdayYear = 1;
        friend.Remember(MemoryCode.Coffee, 7);
        friend.Remember(MemoryCode.Birthday, 9);
        partner.Met = true;
        partner.Stage = SocialStage.Partner;
        partner.MovedIn = true;
        partner.Affection = 85f;
        partner.Trust = 80f;
        partner.Dates = 6;
        SocialState.Plan.Person = 5;
        SocialState.Plan.Kind = PlanKind.Meal;
        SocialState.Plan.Time = 1234f;
        SocialState.Offer.Person = 3;
        SocialState.Offer.Kind = SocialOffer.Loan;
        SocialState.Offer.Amount = 100;
        SocialState.Save(data);
        Check(!data.ContainsKey(MoreMoreKey) && data[MorePeopleKey].Split('\n').Length == SocialRules.BaseCast,
            "more people: a cast of eight is saved in the one record every version reads, as before, and nothing else");
        List<string> saved = MoreCast();

        SocialState.Load(data, 60, MoreYear, MoreDistricts, 1);
        bool same = MoreSame(saved, MoreCast(), out string fault);
        Check(SocialState.CastSeed == 777 && SocialState.Count == SocialRules.BaseCast && same,
            "more people: a save of eight loads with every one of them as saved, the friend, the partner who moved in, memories, debts and birthdays, not a new cast (" + fault + ")");
        Check(SocialState.Plan.Person == 5 && SocialState.Plan.Kind == PlanKind.Meal && SocialState.Offer.Person == 3 && SocialState.Offer.Amount == 100,
            "more people: and its plan and the loan asked are kept, with the person they are with");
    }

    // Every place keeps bringing somebody new, up to 400: names nobody else has, rolled the same for a seed, by the rules of the first eight.
    private static void MoreGrowing()
    {
        MoreFill(4242);
        Check(SocialState.Count == SocialRules.MaxCast, "more people: a long run of meetings fills the cast to exactly " + SocialRules.MaxCast + " (" + SocialState.Count + ")");
        var extra = new SocialPerson { Name = "One Too Many" };
        Check(!SocialState.Add(extra) && SocialState.Count == SocialRules.MaxCast && SocialState.Get(SocialRules.MaxCast) == null,
            "more people: past " + SocialRules.MaxCast + " nobody new joins, and nothing else changes");

        var names = new HashSet<string>(StringComparer.Ordinal);
        bool unique = true, fair = true, sport = true, sexes = true, indexed = true;
        int trainer = Array.IndexOf(SocialCatalogue.Jobs, "reallife_social_job_trainer"), retired = SocialCatalogue.Jobs.Length - 1;
        for (int i = 0; i < SocialState.Count; i++)
        {
            SocialPerson p = SocialState.Get(i);
            unique &= p != null && names.Add(p.Name);
            indexed &= p != null && p.Index == i;
            if (p == null || i < SocialRules.BaseCast)
            {
                continue;
            }
            int n = i - SocialRules.BaseCast;
            fair &= p.Job >= 0 && p.Job != trainer && p.Job != retired && p.Age >= 22 && p.Age <= 55 && p.Birthday >= 1 && p.Birthday <= MoreYear
                    && p.District >= 0 && p.District < MoreDistricts && p.Name.IndexOf(' ') > 0 && p.Name != "Mom" && !p.Name.StartsWith("Alex ", StringComparison.Ordinal)
                    && p.Traits != SocialTrait.None && p.FreeFrom < p.FreeTo;
            sport &= p.Origin != SocialOrigin.Gym || p.Has(SocialTrait.Sporty);
            sexes &= n % 3 == 0 || p.Female == (n % 3 == 2);
        }
        Check(unique, "more people: every one of the 400 has a full name nobody else has: each name is one contact on the phone");
        Check(indexed, "more people: everybody's number is their slot");
        Check(fair, "more people: the later people are 22 to 55, with a job (never the one trainer, never retired), two traits, free hours, a birthday and a district in range");
        Check(sport, "more people: somebody met at the gym is sporty, as the gym's first person is");
        Check(sexes, "more people: somebody new is a woman or a man when the meeting asks for one");

        List<string> first = MoreCast();
        MoreFill(4242);
        Check(MoreSame(first, MoreCast(), out string fault), "more people: the same save meets the same people in the same slots (" + fault + ")");
        MoreFill(4243);
        int differ = 0;
        List<string> other = MoreCast();
        for (int i = SocialRules.BaseCast; i < other.Count; i++)
        {
            differ += other[i] != first[i] ? 1 : 0;
        }
        Check(differ > 380, "more people: another save meets other people (" + differ + " of 392 differ)");
        Check(SocialRules.SlotSeed(4242, 8) == SocialRules.SlotSeed(4242, 8) && SocialRules.SlotSeed(4242, 8) != SocialRules.SlotSeed(4242, 9)
              && SocialRules.SlotSeed(4242, 8) != SocialRules.SlotSeed(4243, 8) && SocialRules.SlotSeed(0, 0) > 0 && SocialRules.SlotSeed(-5, 399) > 0,
            "more people: each later slot has a seed of its own, the same every time, never zero");
    }

    // A cast of 400 saved and loaded: the first eight in their own record, the rest after, everything kept, written again the same.
    private static void MoreRoundTrip()
    {
        MoreFill(5150);
        SocialState.Plan.Person = 250;
        SocialState.Plan.Kind = PlanKind.Walk;
        SocialState.Plan.Time = 4321f;
        SocialState.Offer.Person = 300;
        SocialState.Offer.Kind = (int)PlanKind.Lift;
        SocialState.Get(377).Debt = 40;
        SocialState.Get(377).Remember(MemoryCode.Borrowed, 70);
        var data = new Dictionary<string, string>();
        SocialState.Save(data);
        List<string> saved = MoreCast();

        string[] main = data[MorePeopleKey].Split('\n');
        string[] more = data.TryGetValue(MoreMoreKey, out string text) ? text.Split('\n') : new string[0];
        bool mainShape = main.Length == SocialRules.BaseCast, moreShape = more.Length == SocialRules.MaxCast - SocialRules.BaseCast;
        for (int i = 0; mainShape && i < main.Length; i++)
        {
            mainShape &= main[i].Split('|')[0] == i.ToString(CultureInfo.InvariantCulture) && main[i].Split('|').Length >= 48;
        }
        for (int i = 0; moreShape && i < more.Length; i++)
        {
            moreShape &= more[i].Split('|')[0] == (i + SocialRules.BaseCast).ToString(CultureInfo.InvariantCulture) && more[i].Split('|').Length >= 48;
        }
        Check(mainShape, "more people: the record every version reads holds exactly the first eight, slots 0 to 7, each with all its fields");
        Check(moreShape, "more people: the later 392 are in a record of their own, slot by slot, with the same fields");

        SocialState.Load(data, 500, MoreYear, MoreDistricts, 9);
        bool same = MoreSame(saved, MoreCast(), out string fault);
        Check(SocialState.Count == SocialRules.MaxCast && same,
            "more people: all 400 load as saved, every field and memory (" + fault + ")");
        Check(SocialState.Plan.Person == 250 && SocialState.Plan.Kind == PlanKind.Walk && SocialState.Offer.Person == 300,
            "more people: a plan with person 251 and a favour asked by person 301 are kept");
        var again = new Dictionary<string, string>();
        SocialState.Save(again);
        Check(again[MorePeopleKey] == data[MorePeopleKey] && again[MoreMoreKey] == data[MoreMoreKey] && again[MorePlanKey] == data[MorePlanKey]
              && again[MoreOfferKey] == data[MoreOfferKey], "more people: loaded and saved again, every record is the same, character for character");

        int size = data[MorePeopleKey].Length + data[MoreMoreKey].Length;
        Check(size < 160000, "more people: the whole cast of 400 takes " + size.ToString("N0", CultureInfo.InvariantCulture) + " characters of the save (under 160,000)");
    }

    // How the version before reads a save's people: the first eight from the one record, all of them or a new cast.
    private static int MoreOlderReads(string people)
    {
        var found = new bool[SocialRules.BaseCast];
        int count = 0;
        foreach (string record in people.Split('\n'))
        {
            string[] f = record.Split('|');
            int index = f.Length >= 41 && int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : -1;
            if (index < 0 || index >= SocialRules.BaseCast || found[index] || string.IsNullOrEmpty(f[1]))
            {
                continue;
            }
            found[index] = true;
            count++;
        }
        return count;
    }

    // An older version given a save of 400 reads the first eight and keeps them; what it changes in them and saves is read
    // back, and the later people it never touched are there again.
    private static void MoreOlderVersion()
    {
        MoreFill(6060);
        var data = new Dictionary<string, string>();
        SocialState.Save(data);
        List<string> saved = MoreCast();
        Check(MoreOlderReads(data[MorePeopleKey]) == SocialRules.BaseCast,
            "more people: the version before reads all of the first eight from a save of 400, so it keeps the cast instead of making a new one");

        // The older version plays on: person 4 grows closer, and it writes its own records (no plan with person 251, which it cannot know).
        string[] main = data[MorePeopleKey].Split('\n');
        string[] f = main[4].Split('|');
        f[15] = "77.0";
        main[4] = string.Join("|", f);
        data[MorePeopleKey] = string.Join("\n", main);
        data[MorePlanKey] = string.Empty;
        SocialState.Load(data, 600, MoreYear, MoreDistricts, 1);
        bool later = SocialState.Count == SocialRules.MaxCast;
        for (int i = SocialRules.BaseCast; later && i < SocialRules.MaxCast; i++)
        {
            later &= MoreText(SocialState.Get(i)) == saved[i];
        }
        Check(SocialState.Get(4) != null && Math.Abs(SocialState.Get(4).Familiarity - 77f) < 0.01f && later,
            "more people: back on this version, what the older one did with the first eight stands, and the 392 later people are all there as they were");
    }

    // A line of the later people this version cannot read is kept as it was read, and its slot is never given to anybody else.
    private static void MoreKeptLines()
    {
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 20, MoreYear, MoreDistricts, 31);
        for (int i = 0; i < 2; i++)
        {
            SocialState.Add(SocialCatalogue.MakeNew(SocialState.Count, SocialOrigin.Gym, -1, SocialState.CastSeed, 20, MoreYear, MoreDistricts, SocialState.People, SocialState.Count));
        }
        SocialState.Save(data);
        string firstLine = data[MorePeopleKey].Split('\n')[3];
        data[MoreMoreKey] += "\n12|half a line\n" + firstLine;
        SocialState.Load(data, 21, MoreYear, MoreDistricts, 1);
        Check(SocialState.CastSeed == 31 && SocialState.Count == 13 && SocialState.Get(9) != null && SocialState.Get(12) == null
              && MoreText(SocialState.Get(3)).StartsWith("3|", StringComparison.Ordinal),
            "more people: a later line that cannot be read keeps its slot taken, and a later line claiming one of the first eight changes nobody");
        SocialPerson next = SocialCatalogue.MakeNew(SocialState.Count, SocialOrigin.Street, -1, SocialState.CastSeed, 21, MoreYear, MoreDistricts, SocialState.People, SocialState.Count);
        Check(SocialState.Add(next) && next.Index == 13, "more people: somebody new after that takes the next free slot, never the kept one's");
        var again = new Dictionary<string, string>();
        SocialState.Save(again);
        string kept = again.TryGetValue(MoreMoreKey, out string text) ? text : string.Empty;
        Check(kept.Contains("\n12|half a line") && kept.Contains(firstLine) && kept.StartsWith("8|", StringComparison.Ordinal) && kept.Contains("\n13|"),
            "more people: and both lines are saved again as they were read, beside everybody else");
    }

    // With a big cast only the circle gets in touch: the closest twelve, family, a partner and anyone new. A small cast is all circle.
    private static void MoreCircle()
    {
        var scratch = new int[SocialRules.CircleSize];
        var people = new SocialPerson[40];
        for (int i = 0; i < people.Length; i++)
        {
            people[i] = new SocialPerson { Index = i, Origin = SocialOrigin.Street, Met = i < 20, MetDay = 1, Stage = SocialStage.Acquaintance };
            people[i].Familiarity = people[i].Affection = people[i].Trust = i * 5f;
        }
        int bar = SocialRules.CircleBar(people, people.Length, scratch);
        Check(bar == 40, "more people: with 20 met, a person needs the closeness of the twelfth closest, 40, to be in the circle (" + bar + ")");
        Check(SocialRules.CircleBar(people, 12, scratch) == 0 && SocialRules.CircleBar(people, 0, scratch) == 0,
            "more people: with twelve or fewer met, everybody met is in the circle, as everybody was before");
        people[3].Origin = SocialOrigin.Family;
        people[4].Stage = SocialStage.Dating;
        people[5].MetDay = 95;
        Check(!SocialRules.InCircle(people[6], 100, bar) && SocialRules.InCircle(people[19], 100, bar) && SocialRules.InCircle(people[3], 100, bar)
              && SocialRules.InCircle(people[4], 100, bar) && SocialRules.InCircle(people[5], 100, bar) && !SocialRules.InCircle(people[5], 110, bar)
              && !SocialRules.InCircle(people[25], 100, 0),
            "more people: family, a partner and anyone met in the last " + SocialRules.CircleNewDays + " days are in the circle whoever is closer; others by closeness; nobody unmet");
        people[39].Met = true;
        Check(SocialRules.CircleBar(people, people.Length, scratch) == 45, "more people: somebody closer joining the circle raises its bar");
        Check(SocialRules.TextChance(8) == 0.09f + 0.02f * 8 && SocialRules.TextChance(0) == 0.09f && SocialRules.TextChance(400) == SocialRules.TextChance(SocialRules.CircleSize)
              && SocialRules.TextChance(-3) == 0.09f,
            "more people: the chance an hour that somebody gets in touch is as it was for eight people, and stops growing past the circle's twelve");
        for (int i = 0; i < SocialRules.BaseCast; i++)
        {
            Check(SocialRules.DialogTypeName(i) == "reallife_socialp" + i + "_calldialogtype", "more people: person " + (i + 1) + "'s call opens the dialog type every version registered for them");
        }
        Check(SocialRules.DialogTypeName(399) == "reallife_socialp399_calldialogtype", "more people: the later people have call dialog types of their own");
    }

    // A roommate who is one of the later people: their number in full in field 15, and field 14 says no friend, which is what an older version reads.
    private static void MoreRoommate()
    {
        foreach (int friend in new[] { 37, 5, 399, -1 })
        {
            RoommateState s = RoommateLiving();
            s.Mate.Friend = friend;
            string line = RoommateStore.Write(s);
            RoommateState back = RoommateStore.Read(line, out _);
            string[] f = MoreMateFields(line);
            string first = friend >= 0 && friend < SocialRules.BaseCast ? friend.ToString(CultureInfo.InvariantCulture) : "-1";
            Check(back != null && back.Mate.Friend == friend && f.Length == 16 && f[14] == first && f[15] == friend.ToString(CultureInfo.InvariantCulture),
                "more people: a roommate who is person " + friend + " is saved and read back as them");
            string older = line.Replace(string.Join("|", f), string.Join("|", f, 0, 15));
            RoommateState old = RoommateStore.Read(older, out _);
            Check(old != null && old.Mate.Friend == (friend < SocialRules.BaseCast ? friend : -1),
                "more people: read as the version before reads it, a roommate who is person " + friend + " is "
                + (friend >= 0 && friend < SocialRules.BaseCast ? "the same friend" : "no friend, never the wrong one"));
        }
    }

    // The roommate's own record in a roommate line.
    private static string[] MoreMateFields(string line)
    {
        int from = line.IndexOf("^m:", StringComparison.Ordinal);
        if (from < 0)
        {
            return new string[0];
        }
        int to = line.IndexOf('^', from + 3);
        return line.Substring(from + 3, (to < 0 ? line.Length : to) - from - 3).Split('|');
    }

    // Nothing in the mod still takes the cast to be eight, and every way of meeting can bring somebody new.
    private static void MoreSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var eights = new List<string>();
        var lengths = new List<string>();
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(path);
            string name = System.IO.Path.GetFileName(path);
            if (code.Contains("SocialRules.CastSize") || code.Contains("SocialPerson[8]"))
            {
                eights.Add(name);
            }
            if (name != "SocialState.cs" && code.Contains("< SocialState.People.Length;"))
            {
                lengths.Add(name);
            }
        }
        Check(eights.Count == 0, "more people: no part of the mod sizes the cast as eight (" + string.Join(", ", eights) + ")");
        Check(lengths.Count == 0, "more people: loops over the cast stop at the last slot taken, not at the 400 it can hold (" + string.Join(", ", lengths) + ")");

        string meetings = ModSource(@"Social\SocialDirector.Meetings.cs");
        string pend = MemberText(meetings, "private void Pend(");
        string next = MemberText(meetings, "public static SocialPerson NextStranger()");
        Check(pend.Contains("SocialState.Unmet(origin)") && pend.Contains("person = Newcomer(origin, -1);") && pend.Contains("SocialRules.MeetGapDays")
              && !pend.Contains("ByOrigin"),
            "more people: leaving the gym, a shift, home or your shop brings whoever of that place is still to meet, or somebody new, once a day at most");
        Check(next.Contains("return Newcomer(SocialOrigin.Street, -1);") && MemberText(meetings, "internal static SocialPerson Newcomer(").Contains("SocialPhone.Picture(p);"),
            "more people: meetings online, at an evening, through a friend or a roommate bring somebody new once the first eight are met, with their face on the contact");
        Check(ModSource(@"Wiring\StreetLinks.cs").Contains("return SocialDirector.Newcomer(SocialOrigin.Street, sex) ?? either;")
              && ModSource(@"Wiring\FlareInviteLinks.cs").Contains("return SocialDirector.Newcomer(SocialOrigin.Online, sex) ?? either;"),
            "more people: swapping numbers in the street and a yes on Flare bring somebody new of the right sex too");
        Check(ModSource(@"Wiring\StreetLinks.cs").Contains("!SocialState.NameTaken(m.First + \" \" + m.Last, p)")
              && ModSource(@"Wiring\FlareInviteLinks.cs").Contains("!SocialState.NameTaken(b.First + \" \" + b.Last, p)"),
            "more people: somebody met under a name the cast already has keeps a name of their own, so no two share a contact");

        string phone = ModSource(@"Social\SocialPhone.cs");
        Check(phone.Contains("ModEnumHash.GetSafeHash(SocialRules.DialogTypeName(index))") && phone.Contains("new int[SocialRules.MaxCast]"),
            "more people: every person's contact opens their own call dialog, hashed once a slot");
        string overview = MemberText(ModSource(@"Social\PeopleDialog.cs"), "private DialogEntry Overview()");
        Check(overview.Contains("Hints[o]") && overview.Contains("SocialState.Unmet((SocialOrigin)o)") && !overview.Contains("Hints[i]"),
            "more people: the People page lists the places by place, every one while somebody new can come from it");

        string contact = ModSource(@"Social\SocialDirector.Contact.cs");
        string daily = MemberText(contact, "private void Daily(int day)");
        Check(contact.Contains("SocialRules.InCircle(p, day, bar)") && MemberText(contact, "private static SocialPerson Contactor(int hour)").Contains("Reachable(p, hour, day, bar)"),
            "more people: who texts, rings or asks of their own accord is one of the circle");
        Check(daily.Contains("if (told)") && daily.Contains("p.BirthdayYear != (day - 1) / perYear && SocialRules.InCircle(p, day, bar)"),
            "more people: somebody outside the circle drifts away and has a birthday without a notice or a text to the player");
        Check(ModSource(@"Home\Roommates\RoommateStore.cs").Contains("Bound(Int(f, 15), -1, SocialRules.MaxCast - 1)"),
            "more people: a roommate can be any of the 400");
    }

    // What's new says so in every language, and the Guide's People page in English, Portuguese and Chinese.
    private static void MoreTexts()
    {
        string folder = LocalesFolder();
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] lines =
        {
            "More people to meet", "Mais pessoas para conhecer", "更多可以认识的人", "Mehr Leute kennenlernen", "Plus de gens à rencontrer", "Más gente que conocer",
            "Больше людей для знакомства", "Meer mensen om te ontmoeten", "Tanışılacak daha çok insan"
        };
        string[] guide = { "New faces keep coming, one a day at most", "Rostos novos continuam aparecendo, no máximo um por dia", "新面孔会不断出现，每天最多一个" };
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, codes[l] + ".json"));
            Check(AnyChangesRow(words, lines[l]), "more people: What's new says so in " + codes[l]);
            if (l < guide.Length)
            {
                string page = words != null && words.TryGetValue("reallife_guide_social", out string p) ? p : string.Empty;
                Check(page.Contains(guide[l]) && page.Contains("{social_cast}") && page.IndexOf('!') < 0,
                    "more people: the Guide's People page in " + codes[l] + " says new faces keep coming and only the closest get in touch on their own");
            }
        }
    }

    // The save, the load, the circle's bar and somebody new, each at the largest cast, against their budgets.
    private static void MoreTimings()
    {
        MoreFill(7070);
        var data = new Dictionary<string, string>();
        MoreSaveLoad(data, 2);   // warmed
        var watch = Stopwatch.StartNew();
        MoreSaveLoad(data, 10);
        double each = watch.Elapsed.TotalMilliseconds / 10.0;
        Check(each < MoreSaveMs, "more people: a save and a load of all 400 take " + each.ToString("0.00", CultureInfo.InvariantCulture) + " ms (under " + MoreSaveMs
                                 + " ms, behind the game's save and loading screens)");

        var scratch = new int[SocialRules.CircleSize];
        MoreBars(scratch, 100);   // warmed
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        MoreBars(scratch, 1000);
        double bar = watch.Elapsed.TotalMilliseconds / 1000.0;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(bar < MoreBarMs && allocated == 0, "more people: the circle's bar over 400 people takes " + bar.ToString("0.0000", CultureInfo.InvariantCulture) + " ms and allocates "
                                                 + allocated + " bytes (under " + MoreBarMs + " ms, nothing)");

        watch.Restart();
        SocialPerson last = null;
        for (int i = 0; i < 20; i++)
        {
            last = SocialCatalogue.MakeNew(SocialRules.MaxCast - 1, SocialOrigin.Street, -1, 7070 + i, 10, MoreYear, MoreDistricts, SocialState.People, SocialRules.MaxCast);
        }
        double made = watch.Elapsed.TotalMilliseconds / 20.0;
        Check(last != null && !SocialState.NameTaken(last.Name, null) && made < MoreNewMs,
            "more people: somebody new, kept clear of 400 names, is made in " + made.ToString("0.000", CultureInfo.InvariantCulture) + " ms (under " + MoreNewMs + " ms, once a meeting)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void MoreSaveLoad(Dictionary<string, string> data, int runs)
    {
        for (int i = 0; i < runs; i++)
        {
            SocialState.Save(data);
            SocialState.Load(data, 100, MoreYear, MoreDistricts, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void MoreBars(int[] scratch, int runs)
    {
        for (int i = 0; i < runs; i++)
        {
            SocialRules.CircleBar(SocialState.People, SocialState.Count, scratch);
        }
    }
}
