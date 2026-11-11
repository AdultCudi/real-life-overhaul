using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Street;

// Who the player can fall for (Social/RomanceRules). Players found the people they could ask out were often twice their age
// (a stranger in the street, or somebody met on Flare, kept a "could be more than a friend" roll made for another face), that
// more men than women came up whatever they were, and that asking out worked for one character and not for another. The roll
// ignored the person's sex and age and the player's own, and a stranger's answer to a flirt never reached their record. Now
// whoever is open to a romance is judged as they are today: of the sex the player is interested in, near the player's age (the
// gap the city's own couples keep), in the cast, the street, Flare and the phone alike.
internal static partial class Program
{
    private const int RomanceYear = 28;        // days in a year for these checks; any length works
    private const int RomanceDistricts = 9;

    private static void Romance()
    {
        int before = _run, failedBefore = _failed;
        RomanceRuleTable();
        RomanceCastFits();
        RomanceCastMix();
        RomanceVerdict();
        RomanceStreet();
        RomanceCouples();
        RomanceSources();
        RomanceTexts();
        Console.WriteLine("  romance: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // ---------------------------------------------------------------- the rule

    private static void RomanceRuleTable()
    {
        bool sex = true;
        foreach (int interest in new[] { RomanceRules.Anyone, RomanceRules.Women, RomanceRules.Men, 3, -1, 99 })
        {
            foreach (bool female in new[] { true, false })
            {
                bool want = interest == RomanceRules.Women ? female : interest == RomanceRules.Men ? !female : true;
                sex &= RomanceRules.SexFits(interest, female) == want;
            }
        }
        Check(sex && RomanceRules.Anyone == 0 && RomanceRules.Women == 1 && RomanceRules.Men == 2,
            "romance: a woman fits 'women' and 'anyone', a man 'men' and 'anyone', and a saved value that is none of them reads as anyone (0, 1 and 2 as the option saves them)");

        int gap = RomanceRules.AgeGap;
        bool ages = true, edges = true;
        foreach (int player in new[] { 18, 19, 25, 40, 60, 70 })
        {
            for (int age = 0; age <= 95; age++)
            {
                ages &= RomanceRules.AgeFits(age, player) == (age >= RomanceRules.AgeLeast && Math.Abs(age - player) <= gap);
            }
            edges &= RomanceRules.AgeFits(player + gap, player) && !RomanceRules.AgeFits(player + gap + 1, player)
                     && RomanceRules.AgeFits(Math.Max(RomanceRules.AgeLeast, player - gap), player) && !RomanceRules.AgeFits(player - gap - 1, player);
        }
        Check(ages && edges, "romance: somebody fits a player's age when they are an adult and no more than " + gap + " years from it, on either side, edges included");
        Check(!RomanceRules.AgeFits(17, 18) && !RomanceRules.AgeFits(12, 20) && RomanceRules.AgeFits(18, 18) && RomanceRules.AgeFits(18 + gap, 18) && !RomanceRules.AgeFits(19 + gap, 18),
            "romance: a player of eighteen can fall for somebody of eighteen to " + (18 + gap) + ", and nobody under eighteen is ever somebody to fall for");
        Check(RomanceRules.AgeFits(0, 0) && RomanceRules.AgeFits(90, 0) && RomanceRules.AgeFits(5, -3) && RomanceRules.Fits(RomanceRules.Women, true, 0, 0),
            "romance: when the game does not say the player's age (0) nobody is ruled out for their age");
        Check(gap == LifeRules.AgeGapMost && gap > 0, "romance: the player's age gap is the gap the city's own couples keep (" + gap + " years, Population/LifeRules.AgeGapMost)");

        bool near = true, covered = true, same = true;
        for (int player = 18; player <= 93; player++)
        {
            var seen = new HashSet<int>();
            for (int roll = 0; roll < 200; roll++)
            {
                int age = RomanceRules.AgeNear(player, roll);
                near &= RomanceRules.AgeFits(age, player) && age >= RomanceRules.AgeLeast && age <= RomanceRules.AgeMost;
                same &= age == RomanceRules.AgeNear(player, roll);
                seen.Add(age);
            }
            foreach (int roll in new[] { -1, int.MinValue, int.MaxValue, 123456789 })
            {
                near &= RomanceRules.AgeFits(RomanceRules.AgeNear(player, roll), player);
            }
            covered &= seen.Count == Math.Min(RomanceRules.AgeMost, player + gap) - Math.Max(RomanceRules.AgeLeast, player - gap) + 1;
        }
        Check(near && same, "romance: an age made to fit a player always fits, whatever the roll (even a negative one), between 18 and " + RomanceRules.AgeMost + ", the same every time");
        Check(covered, "romance: the ages made to fit a player cover the whole window, not one age (for every player of 18 to 93)");
        Check(RomanceRules.AgeNear(120, 5) == RomanceRules.AgeMost && RomanceRules.AgeNear(3, 5) >= RomanceRules.AgeLeast,
            "romance: a player older than the cast keeps anybody gets the nearest age there is, and nobody is made younger than eighteen");
    }

    // ---------------------------------------------------------------- the cast made to fit

    private static void RomanceCastFits()
    {
        int[] players = { 18, 24, 31, 45, 63 };
        int[] interests = { RomanceRules.Anyone, RomanceRules.Women, RomanceRules.Men };
        var plain = new SocialPerson[SocialRules.BaseCast];
        var cast = new SocialPerson[SocialRules.BaseCast];
        var again = new SocialPerson[SocialRules.BaseCast];
        bool fit = true, two = true, named = true, rest = true, adultsOnly = true;
        string fault = string.Empty;
        for (int seed = 1; seed <= 400; seed++)
        {
            SocialCatalogue.MakeCast(plain, seed, 3, RomanceYear, RomanceDistricts);
            foreach (int player in players)
            {
                foreach (int interest in interests)
                {
                    SocialCatalogue.MakeCast(cast, seed, 3, RomanceYear, RomanceDistricts, player, interest);
                    SocialCatalogue.MakeCast(again, seed, 3, RomanceYear, RomanceDistricts, player, interest);
                    int open = 0;
                    for (int i = 0; i < cast.Length; i++)
                    {
                        SocialPerson p = cast[i], q = plain[i], r = again[i];
                        open += p.Compatible ? 1 : 0;
                        if (p.Compatible)
                        {
                            bool ok = RomanceRules.Fits(interest, p.Female, p.Age, player);
                            fit &= ok;
                            adultsOnly &= p.Age >= RomanceRules.AgeLeast;
                            if (!ok && fault.Length == 0)
                            {
                                fault = "seed " + seed + ", player " + player + ", interest " + interest + ": " + p.Name + " (" + (p.Female ? "a woman" : "a man") + " of " + p.Age + ")";
                            }
                            int space = p.Name.IndexOf(' ');
                            named &= space > 0 && Array.IndexOf(p.Female ? SocialCatalogue.Women : SocialCatalogue.Men, p.Name.Substring(0, space)) >= 0;
                        }
                        // Nothing else of anybody moves: the same seed deals the same people, and whoever is not open is exactly as before.
                        rest &= p.Seed == q.Seed && p.Traits == q.Traits && p.Job == q.Job && p.District == q.District && p.Birthday == q.Birthday && p.Origin == q.Origin
                                && p.Compatible == q.Compatible && p.FreeFrom == q.FreeFrom && p.FreeTo == q.FreeTo && p.Wants == q.Wants
                                && (p.Compatible || (p.Name == q.Name && p.Female == q.Female && p.Age == q.Age))
                                && p.Name == r.Name && p.Female == r.Female && p.Age == r.Age && p.Compatible == r.Compatible;   // (and the same again for the same player and wish)
                    }
                    two &= open >= 2;
                }
            }
        }
        Check(fit && adultsOnly, "romance: everybody of a new cast who is open to a romance fits the player (their sex for what the player is interested in, their age near the player's, an adult): "
                                 + "400 seeds, five ages, three wishes" + (fault.Length > 0 ? "; " + fault : string.Empty));
        Check(two, "romance: at least two of a new cast's first eight are still open to a romance, whatever the player's age and wish");
        Check(named, "romance: whoever is made to fit keeps a first name of the sex they are");
        Check(rest, "romance: nothing else of the cast moves: the same seed deals the same people, and whoever is not open is the same person with or without the player's age and wish");

        // Somebody new, to be met: the same, and a sex asked for by the caller (a figure in the street, a face on Flare) is kept.
        bool newFit = true, kept = true, deterministic = true;
        string faultNew = string.Empty;
        var nobody = new SocialPerson[1];
        for (int castSeed = 1; castSeed <= 12; castSeed++)
        {
            for (int index = 8; index < 60; index++)
            {
                foreach (int player in players)
                {
                    foreach (int interest in interests)
                    {
                        SocialPerson p = SocialCatalogue.MakeNew(index, SocialOrigin.Street, -1, castSeed, 40, RomanceYear, RomanceDistricts, nobody, 0, player, interest);
                        bool ok = !p.Compatible || RomanceRules.Fits(interest, p.Female, p.Age, player);
                        newFit &= ok;
                        if (!ok && faultNew.Length == 0)
                        {
                            faultNew = "slot " + index + ", player " + player + ", interest " + interest + ": " + p.Name;
                        }
                        SocialPerson twice = SocialCatalogue.MakeNew(index, SocialOrigin.Street, -1, castSeed, 40, RomanceYear, RomanceDistricts, nobody, 0, player, interest);
                        deterministic &= twice.Name == p.Name && twice.Age == p.Age && twice.Female == p.Female && twice.Compatible == p.Compatible;
                        foreach (int wanted in new[] { 0, 1 })
                        {
                            SocialPerson w = SocialCatalogue.MakeNew(index, SocialOrigin.Online, wanted, castSeed, 40, RomanceYear, RomanceDistricts, nobody, 0, player, interest);
                            kept &= w.Female == (wanted == 1) && (!w.Compatible || RomanceRules.AgeFits(w.Age, player));
                        }
                    }
                }
            }
        }
        Check(newFit && deterministic, "romance: somebody new who is open to a romance fits the player as the first eight do, the same every time" + (faultNew.Length > 0 ? "; " + faultNew : string.Empty));
        Check(kept, "romance: when the caller asks for a man or a woman (a figure met in the street, a face on Flare) that is who they are, whatever the player is interested in");
    }

    // The sex mix of the people dealt: a coin flip for everybody, so no sex is dealt more often; only those open to a romance follow the wish.
    private static void RomanceCastMix()
    {
        const int seeds = 3000;
        var cast = new SocialPerson[SocialRules.BaseCast];
        int[] interests = { RomanceRules.Anyone, RomanceRules.Women, RomanceRules.Men };
        bool openOk = true, restOk = true, allOk = true;
        string said = string.Empty;
        foreach (int interest in interests)
        {
            int all = 0, allWomen = 0, open = 0, openWomen = 0, rest = 0, restWomen = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                SocialCatalogue.MakeCast(cast, seed, 3, RomanceYear, RomanceDistricts, 25, interest);
                for (int i = (int)SocialOrigin.Gym; i < cast.Length; i++)
                {
                    all++;
                    allWomen += cast[i].Female ? 1 : 0;
                    if (cast[i].Compatible)
                    {
                        open++;
                        openWomen += cast[i].Female ? 1 : 0;
                    }
                    else
                    {
                        rest++;
                        restWomen += cast[i].Female ? 1 : 0;
                    }
                }
            }
            double shareOpen = openWomen / (double)Math.Max(1, open), shareRest = restWomen / (double)Math.Max(1, rest), shareAll = allWomen / (double)Math.Max(1, all);
            openOk &= interest == RomanceRules.Women ? openWomen == open : interest == RomanceRules.Men ? openWomen == 0 : shareOpen > 0.45 && shareOpen < 0.55;
            restOk &= shareRest > 0.45 && shareRest < 0.55;
            allOk &= interest != RomanceRules.Anyone || (shareAll > 0.47 && shareAll < 0.53);
            said += " [" + interest + ": all " + shareAll.ToString("F3") + ", open " + shareOpen.ToString("F3") + ", the rest " + shareRest.ToString("F3") + "]";
        }
        Check(allOk, "romance: nobody is dealt more often as a man: of " + seeds * 6 + " people dealt about half are women" + said);
        Check(openOk, "romance: those open to a romance are of the sex the player asked for, and with 'anyone' about half are women, not mostly men" + said);
        Check(restOk, "romance: everybody who is not open to one is a man or a woman as the roll fell, whatever the player is interested in: the world is not all one sex" + said);

        // The same for the people who come after the first eight.
        var nobody = new SocialPerson[1];
        bool newOpen = true, newRest = true, newAll = true;
        string saidNew = string.Empty;
        foreach (int interest in interests)
        {
            int all = 0, allWomen = 0, open = 0, openWomen = 0, rest = 0, restWomen = 0;
            for (int castSeed = 1; castSeed <= 40; castSeed++)
            {
                for (int index = 8; index < 408; index++)
                {
                    SocialPerson p = SocialCatalogue.MakeNew(index, SocialOrigin.Street, -1, castSeed, 40, RomanceYear, RomanceDistricts, nobody, 0, 25, interest);
                    all++;
                    allWomen += p.Female ? 1 : 0;
                    if (p.Compatible)
                    {
                        open++;
                        openWomen += p.Female ? 1 : 0;
                    }
                    else
                    {
                        rest++;
                        restWomen += p.Female ? 1 : 0;
                    }
                }
            }
            double shareOpen = openWomen / (double)Math.Max(1, open), shareRest = restWomen / (double)Math.Max(1, rest), shareAll = allWomen / (double)Math.Max(1, all);
            newOpen &= interest == RomanceRules.Women ? openWomen == open : interest == RomanceRules.Men ? openWomen == 0 : shareOpen > 0.45 && shareOpen < 0.55;
            newRest &= shareRest > 0.45 && shareRest < 0.55;
            newAll &= interest != RomanceRules.Anyone || (shareAll > 0.47 && shareAll < 0.53);
            saidNew += " [" + interest + ": all " + shareAll.ToString("F3") + ", open " + shareOpen.ToString("F3") + ", the rest " + shareRest.ToString("F3") + "]";
        }
        Check(newAll && newOpen && newRest, "romance: the people who come after the first eight are rolled as a man or a woman about half and half, and those open to a romance follow the wish" + saidNew);
    }

    // ---------------------------------------------------------------- the verdict on a person

    private static void RomanceVerdict()
    {
        const int day = 600, year = 60;
        // The report: somebody rolled open at thirty meets the player as a street figure of sixty-three, and the phone went on offering
        // to ask them out because the roll was kept.
        var street = new SocialPerson { Compatible = true, Female = true, Age = 63, AgeDay = day, Birthday = 10 };
        var peer = new SocialPerson { Compatible = true, Female = true, Age = 27, AgeDay = day, Birthday = 10 };
        Check(!SocialRules.Romantic(street, RomanceRules.Anyone, 28, day, year) && SocialRules.Romantic(peer, RomanceRules.Anyone, 28, day, year),
            "romance: somebody of sixty-three who was rolled open at thirty is no longer somebody a player of 28 can ask out; somebody of 27 is");
        var closed = new SocialPerson { Compatible = false, Female = true, Age = 27, AgeDay = day, Birthday = 10 };
        Check(!SocialRules.Romantic(closed, RomanceRules.Anyone, 28, day, year) && !SocialRules.Romantic(null, RomanceRules.Anyone, 28, day, year),
            "romance: somebody not open to one is never somebody to fall for, whatever their age and sex");
        var man = new SocialPerson { Compatible = true, Female = false, Age = 27, AgeDay = day, Birthday = 10 };
        Check(SocialRules.Romantic(man, RomanceRules.Men, 28, day, year) && SocialRules.Romantic(man, RomanceRules.Anyone, 28, day, year) && !SocialRules.Romantic(man, RomanceRules.Women, 28, day, year)
              && SocialRules.Romantic(peer, RomanceRules.Women, 28, day, year) && !SocialRules.Romantic(peer, RomanceRules.Men, 28, day, year),
            "romance: a man is somebody to ask out for a player interested in men or anyone, a woman for one interested in women or anyone, whatever the player is");
        // The player's own age counts, today's: a person who fitted a player of one age no longer fits a player who is older by more than the gap.
        var mid = new SocialPerson { Compatible = true, Female = true, Age = 40, AgeDay = day, Birthday = 10 };
        Check(SocialRules.Romantic(mid, RomanceRules.Anyone, 40 - RomanceRules.AgeGap, day, year) && SocialRules.Romantic(mid, RomanceRules.Anyone, 40 + RomanceRules.AgeGap, day, year)
              && !SocialRules.Romantic(mid, RomanceRules.Anyone, 41 + RomanceRules.AgeGap, day, year) && !SocialRules.Romantic(mid, RomanceRules.Anyone, 39 - RomanceRules.AgeGap, day, year),
            "romance: the player's own age counts, as it is today: somebody of 40 fits a player of " + (40 - RomanceRules.AgeGap) + " to " + (40 + RomanceRules.AgeGap) + ", not one a year beyond");
        // Their age is today's: a birthday adds a year, so somebody at the edge of the window leaves it the next birthday.
        var edge = new SocialPerson { Compatible = true, Female = true, Age = 28 + RomanceRules.AgeGap, AgeDay = day, Birthday = (day % year) + 3 };
        Check(SocialRules.Romantic(edge, RomanceRules.Anyone, 28, day, year) && !SocialRules.Romantic(edge, RomanceRules.Anyone, 28, day + 10, year),
            "romance: their age is the age they have today, a year more at each birthday");

        // Asking out: only somebody the player could fall for says yes, and then at the figures it always took.
        var friend = new SocialPerson { Compatible = true, Stage = SocialStage.Friend, Affection = 70f, Trust = 60f };
        bool asks = SocialRules.WouldDate(friend, true) && !SocialRules.WouldDate(friend, false);
        friend.Stage = SocialStage.Dating;
        asks &= !SocialRules.WouldDate(friend, true);
        friend.Stage = SocialStage.Friend;
        friend.Affection = SocialRules.DateAffection - 1f;
        asks &= !SocialRules.WouldDate(friend, true);
        friend.Affection = SocialRules.DateAffection;
        friend.Trust = SocialRules.DateTrust - 1f;
        asks &= !SocialRules.WouldDate(friend, true);
        friend.Trust = SocialRules.DateTrust;
        asks &= SocialRules.WouldDate(friend, true);
        Check(asks, "romance: asked out, they say yes from affection " + SocialRules.DateAffection + " and trust " + SocialRules.DateTrust + " only when a romance is possible with this player, and not when they are together already");

        // Nothing ends because of it: a partner whom the player's age or wish would now rule out stays a partner.
        var partner = new SocialPerson { Met = true, Compatible = true, Female = true, Age = 70, AgeDay = day, Birthday = 10, Stage = SocialStage.Partner, Affection = 90f, Trust = 90f };
        Check(!SocialRules.Romantic(partner, RomanceRules.Men, 25, day, year) && !SocialRules.BrokenUp(partner) && SocialRules.Earned(partner, day) == SocialStage.Partner,
            "romance: somebody the player is already with is not parted from them because their sex or age would no longer fit: only affection and trust end it");
    }

    // ---------------------------------------------------------------- the street

    private static void RomanceStreet()
    {
        StreetFacts f = StreetDay();
        f.Interest = RomanceRules.Women;
        f.PlayerAge = 30;
        StreetStranger Open(bool female, int age, bool taken = false) =>
            new StreetStranger { First = "Dana", Last = "Reyes", Female = female, Age = age, Compatible = true, Taken = taken, Seed = 5 };
        StreetChat Warm(StreetStranger s) => new StreetChat { Who = s, Memo = new StreetMemo { Rapport = 6 } };
        StreetLine Flirt(StreetStranger s, StreetFacts facts) => StreetRules.Answer(Warm(s), StreetAsk.Flirt, facts, 3).Line;

        Check(Flirt(Open(true, 27), f) == StreetLine.FlirtYes && Flirt(Open(false, 27), f) == StreetLine.FlirtNo,
            "romance, street: for a player of 30 interested in women a flirt works with a woman of 27 who is open, and not with a man of 27");
        int gap = RomanceRules.AgeGap;
        Check(Flirt(Open(true, 62), f) == StreetLine.FlirtNo && Flirt(Open(true, 30 + gap + 2), f) == StreetLine.FlirtNo && Flirt(Open(true, 30 + gap), f) == StreetLine.FlirtYes
              && Flirt(Open(true, 30 + gap + 1), f) == StreetLine.FlirtNo && Flirt(Open(true, 30 - gap), f) == StreetLine.FlirtYes && Flirt(Open(true, 30 - gap - 1), f) == StreetLine.FlirtNo,
            "romance, street: a flirt does not work with somebody more than " + gap + " years from the player's age, on either side (against 30: it works at " + (30 - gap) + " and " + (30 + gap)
            + ", not a year past, and not with a woman of 62)");
        Check(Flirt(Open(true, 27, true), f) == StreetLine.FlirtTaken, "romance, street: somebody who is seeing someone says so, as before");
        var notOpen = Open(true, 27);
        notOpen.Compatible = false;
        Check(Flirt(notOpen, f) == StreetLine.FlirtNo, "romance, street: somebody not open to a romance says no, as before");
        StreetFacts anyone = StreetDay();
        Check(Flirt(Open(false, 27), anyone) == StreetLine.FlirtYes && Flirt(Open(true, 62), anyone) == StreetLine.FlirtYes,
            "romance, street: with the player's age not known and anyone wanted a flirt works with anybody open, as it did");

        // What is offered: no flirt with those the player is not interested in.
        var list = new List<StreetAsk>();
        StreetRules.Choices(Warm(Open(false, 27)), f, list);
        bool hiddenForMen = !list.Contains(StreetAsk.Flirt);
        StreetRules.Choices(Warm(Open(true, 27)), f, list);
        bool shownForWomen = list.Contains(StreetAsk.Flirt);
        f.Interest = RomanceRules.Men;
        StreetRules.Choices(Warm(Open(true, 27)), f, list);
        bool hiddenForWomen = !list.Contains(StreetAsk.Flirt);
        StreetRules.Choices(Warm(Open(false, 27)), f, list);
        bool shownForMen = list.Contains(StreetAsk.Flirt);
        f.Interest = RomanceRules.Anyone;
        StreetRules.Choices(Warm(Open(false, 27)), f, list);
        bool shownForAnyone = list.Contains(StreetAsk.Flirt);
        StreetRules.Choices(Warm(Open(true, 27)), f, list);
        Check(hiddenForMen && shownForWomen && hiddenForWomen && shownForMen && shownForAnyone && list.Contains(StreetAsk.Flirt),
            "romance, street: the flirt is on offer only for the people the player is interested in (women, men or anyone)");

        // Their roll is a roll of everybody: the age and the sex are weighed against the player's when it counts, not when they are rolled.
        int young = 0, old = 0, total = 4000;
        for (int key = 1; key <= total; key++)
        {
            young += StreetRules.Identity(77, 5, key, true, 19, RomanceDistricts, -1).Compatible ? 1 : 0;
            old += StreetRules.Identity(77, 5, key, false, 63, RomanceDistricts, -1).Compatible ? 1 : 0;
        }
        Check(young > total * 40 / 100 && young < total * 50 / 100 && old > total * 40 / 100 && old < total * 50 / 100,
            "romance, street: " + StreetRules.CompatiblePercent + " in a hundred of strangers are open to a romance at any age (a stranger of 19: " + young + " of " + total + ", of 63: " + old
            + "); whether it is with the player is Romantic");
        StreetStranger a = StreetRules.Identity(77, 5, 99, true, 30, RomanceDistricts, -1), b = StreetRules.Identity(77, 5, 99, true, 30, RomanceDistricts, -1);
        Check(a.Compatible == b.Compatible && StreetRules.Romantic(a, f) == StreetRules.Romantic(b, f), "romance, street: the same figure is open or not every time");

        // What the talk showed reaches the phone: the stranger who said yes to a flirt is somebody who can be asked out, not a coin flip made for another face.
        string bridges = ModSource(@"Street\StreetBridges.cs"), outcomes = ModSource(@"Street\StreetDirector.Outcomes.cs"), links = ModSource(@"Wiring\StreetLinks.cs");
        Check(bridges.Contains("public bool Compatible;") && outcomes.Contains("Compatible = s.Compatible") && links.Contains("p.Compatible = m.Compatible;"),
            "romance, street: a stranger met in the street is as open to a romance on the phone as the talk showed (StreetMeet carries it, MeetInStreet sets it)");
        Check(links.Contains("f.Interest = SocialLife.Interest;") && links.Contains("f.PlayerAge = SocialLife.PlayerYears();"),
            "romance, street: the talk is told what the player is interested in and how old they are");
        string rules = ModSource(@"Street\StreetRules.Choices.cs");
        Check(rules.Contains("Romantic(s, f) && score + luck >= 1") && !rules.Contains("s.Compatible && score + luck"), "romance, street: a flirt is answered by the one rule, never by the roll alone");
    }

    // ---------------------------------------------------------------- the city's own couples

    private static Dictionary<int, int> RomanceCouplesOf(PopulationState s)
    {
        var map = new Dictionary<int, int>(4096);
        PersonTable pt = s.People;
        for (int p = 0; p < pt.Count; p++)
        {
            int q = pt.Alive(p) ? pt.Partner[p] : -1;
            if (q >= 0 && pt.Alive(q))
            {
                map[pt.Id[p]] = pt.Id[q];
            }
        }
        return map;
    }

    // The city's people pair up by the same gap, and in both mixes: what the player is held to, they are held to.
    private static void RomanceCouples()
    {
        string rules = ModSource(@"Social\RomanceRules.cs"), life = ModSource(@"Population\LifeRules.cs");
        string pair = MemberText(life, "private static void PairAll(");
        Check(rules.Contains("public const int AgeGap = RealLifeOverhaul.Population.LifeRules.AgeGapMost;") && pair.Contains("gap > AgeGapMost") && pair.Contains("(pt.Female[a] == pt.Female[b]) != same"),
            "romance, the city's couples: the player's age gap is read from the one the city's singles pair up by, which turns down a partner further away and pairs both mixes");

        PopulationSim sim = PopulationMade(1234, out PopulationState s, out _);
        Dictionary<int, int> start = RomanceCouplesOf(s);
        var there = new HashSet<int>();   // who is in the city at the start: two of them who were not a couple then were paired by the city's own pairing, not brought by the region
        for (int z = 0; z < s.People.Count; z++)
        {
            if (s.People.Alive(z))
            {
                there.Add(s.People.Id[z]);
            }
        }
        for (int day = 2; day <= s.YearDays * 2 + 1; day++)
        {
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
        }
        PersonTable pt = s.People;
        int couples = 0, formed = 0, worst = 0, same = 0, sameFormed = 0;
        for (int p = 0; p < pt.Count; p++)
        {
            int q = pt.Alive(p) ? pt.Partner[p] : -1;
            if (q < p || !pt.Alive(q))
            {
                continue;
            }
            couples++;
            worst = Math.Max(worst, Math.Abs(pt.Birth[p] - pt.Birth[q]) / Math.Max(1, s.YearDays));
            same += pt.Female[p] == pt.Female[q] ? 1 : 0;
            bool paired = there.Contains(pt.Id[p]) && there.Contains(pt.Id[q]) && !(start.TryGetValue(pt.Id[p], out int was) && was == pt.Id[q]);
            formed += paired ? 1 : 0;
            sameFormed += paired && pt.Female[p] == pt.Female[q] ? 1 : 0;
        }
        Check(couples >= 500 && formed >= 300, "romance, the city's couples: after two years of the city without the player there are couples (" + couples + "), and many of them were paired in the city since the start (" + formed + ")");
        Check(worst <= RomanceRules.AgeGap, "romance, the city's couples: no couple is more than " + RomanceRules.AgeGap + " years apart, the gap the player's romances keep (the widest is " + worst + ")");
        Check(same * 100 >= couples && same * 100 <= couples * 12 && formed >= 300 && sameFormed * 100 >= formed * 2 && sameFormed * 100 <= formed * 15,
            "romance, the city's couples: some couples are of the same sex, a few in a hundred (" + same + " of " + couples + "; of the " + formed + " paired in the two years, " + sameFormed
            + "), as 'anyone' allows the player");
    }

    // ---------------------------------------------------------------- the wiring, read in the sources

    private static void RomanceSources()
    {
        string person = ModSource(@"Social\PersonDialog.cs"), people = ModSource(@"Social\PeopleDialog.cs"), flare = ModSource(@"Wiring\FlareLinks.cs");
        string tests = ModSource(@"Social\SocialDirector.Tests.cs"), life = ModSource(@"Social\SocialLife.cs");
        Check(person.Contains("SocialLife.Romantic(_p)") && person.Contains("SocialRules.WouldDate(_p, SocialLife.Romantic(_p))") && !person.Contains("_p.Compatible"),
            "romance: the phone offers Ask them out, and asking decides, by the one rule (SocialLife.Romantic), never by the roll alone");
        Check(people.Contains("SocialLife.Romantic(p)") && people.Contains("reallife_social_people_romance"), "romance: a person's page in People says when they could be more than a friend");
        Check(flare.Contains("Compatible = SocialRules.Romantic(p, interest, years,") && !flare.Contains("Compatible = p.Compatible"), "romance: Flare is told who the player could fall for by the one rule too");
        Check(tests.Contains("SocialLife.Romantic(p)") && !tests.Contains("p.Compatible"), "romance: the test that advances a relationship follows the one rule");
        Check(life.Contains("public static int Interest => Settings.FamilyInterest;") && life.Contains("TimeHelper.GetYearsByDays(data.ageInDays)")
              && life.Contains("SocialRules.Romantic(person, Interest, PlayerYears(), Day, SocialPlaces.DaysPerYear)"),
            "romance: the rule is asked with the option and the played character's age of today");

        string director = ModSource(@"Social\SocialDirector.cs"), meetings = ModSource(@"Social\SocialDirector.Meetings.cs"), lose = ModSource(@"Social\SocialDirector.Lose.cs");
        Check(director.Contains("SocialLife.PlayerYears(), SocialLife.Interest);") && meetings.Contains("SocialState.People, SocialState.Count, years, interest);")
              && lose.Contains("SocialLife.PlayerYears(), SocialLife.Interest);"),
            "romance: a new cast, somebody new to meet and whoever takes the place of someone lost touch with are all made for this player's age and wish");

        // Nobody decides on the roll alone: every place that reads a person's or a stranger's .Compatible is one that rolls it, keeps it, carries it or asks the rule.
        string[] allowed =
        {
            @"Social\SocialRules.cs", @"Social\SocialLife.cs", @"Social\SocialCatalogue.cs", @"Social\SocialState.cs", @"Social\SocialDirector.Meetings.cs",
            @"Social\Flare\FlareDirector.City.cs", @"Street\StreetRules.cs", @"Street\StreetBridges.cs", @"Street\StreetDirector.Outcomes.cs",
            @"Wiring\StreetLinks.cs", @"Wiring\Wiring.DynastyLinks.cs"
        };
        var stray = new List<string>();
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        foreach (string folder in new[] { "Social", "Street", "Wiring", "Family", "Dynasty", "Home", "People", "Texts" })
        {
            string dir = System.IO.Path.Combine(root, folder);
            if (!System.IO.Directory.Exists(dir))
            {
                continue;
            }
            foreach (string file in System.IO.Directory.GetFiles(dir, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string relative = file.Substring(root.Length).TrimStart('\\', '/').Replace('/', '\\');
                string text = System.IO.File.ReadAllText(file);
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\.Compatible\b") && Array.IndexOf(allowed, relative) < 0)
                {
                    stray.Add(relative);
                }
            }
        }
        Check(stray.Count == 0, "romance: nothing but the rule's own places reads whether somebody is open to a romance (" + string.Join(", ", stray) + ")");

        // The option: one row, under Friends and social life, owned by the social life, with its label and its sentence.
        string tree = ModSource(@"Meta\OptionsTree.cs"), features = ModSource(@"Meta\FeatureList.Life.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), settings = ModSource(@"Core\Settings.cs");
        int social = tree.IndexOf("S(\"social\")", StringComparison.Ordinal), networking = tree.IndexOf("S(\"networking\")", StringComparison.Ordinal);
        int family = tree.IndexOf("S(\"family\")", StringComparison.Ordinal), partner = tree.IndexOf("S(\"partner\")", StringComparison.Ordinal);
        Check(social >= 0 && networking > social && tree.Substring(social, networking - social).Contains("FreeOpts(\"family_interest\")")
              && family >= 0 && partner > family && !tree.Substring(family, partner - family).Contains("family_interest"),
            "romance: the option stands under Friends and social life and no longer under Family");
        int socialFeature = features.IndexOf("Features.Add(\"social\"", StringComparison.Ordinal), ledgerFeature = features.IndexOf("Features.Add(\"ledger\"", StringComparison.Ordinal);
        int familyFeature = features.IndexOf("Features.Add(\"family\"", StringComparison.Ordinal);
        Check(socialFeature >= 0 && ledgerFeature > socialFeature && features.Substring(socialFeature, ledgerFeature - socialFeature).Contains("Owns(Settings.FamilyInterestId)")
              && familyFeature >= 0 && !features.Substring(familyFeature, Math.Min(900, features.Length - familyFeature)).Contains("Settings.FamilyInterestId"),
            "romance: the option belongs to the social life, and Family does not claim it too");
        Check(mod.Contains("AddDropdown(Settings.FamilyInterestId, \"reallife_family_interest\"") && mod.Contains("\"reallife_family_interest_anyone\", \"reallife_family_interest_women\", \"reallife_family_interest_men\"")
              && settings.Contains("public static int FamilyInterest = 0;") && settings.Contains("FamilyInterest = Int(modId, FamilyInterestId, FamilyInterest);"),
            "romance: the option is a choice of anyone, women or men, anyone by default, saved as it always was");
        Check(features.Contains("Fact(\"social_age_gap\", () => RomanceRules.AgeGap."), "romance: the Guide's age gap is filled from the rule");
    }

    // ---------------------------------------------------------------- the words

    private static void RomanceTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        if (en == null)
        {
            Check(false, "romance: en.json could not be read");
            return;
        }
        string label = en.TryGetValue("reallife_family_interest", out string l) ? l : string.Empty, tip = en.TryGetValue("reallife_opt_tip_family_interest", out string t) ? t : string.Empty;
        Check(label.Length > 0 && label.Length <= 60 && label.Split(' ').Length <= 9 && !label.EndsWith(".") && label.IndexOf("Someone special", StringComparison.Ordinal) < 0,
            "romance: the option's label says what it is for in a few words, who the player is interested in, not only who 'Someone special' introduces (" + label + ")");
        Check(tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && tip.EndsWith(".") && tip.Contains("women, men or anyone") && tip.Contains("ask out") && tip.Contains("flirt"),
            "romance: the option's sentence says who it decides, in one sentence of at most 32 words and 190 characters (" + tip + ")");
        Check(en.ContainsKey("reallife_family_interest_anyone") && en.ContainsKey("reallife_family_interest_women") && en.ContainsKey("reallife_family_interest_men"),
            "romance: the three choices are in en.json");
        Check(en.TryGetValue("reallife_social_people_romance", out string line) && line.Length > 0 && !line.Contains("{") && !line.Contains("!") && line.IndexOf('\u2013') < 0 && line.IndexOf('\u2014') < 0,
            "romance: a person's page has its line for somebody who could be more than a friend");
        Check(en.TryGetValue("reallife_guide_social", out string guide) && guide.Contains("{social_age_gap}") && guide.Contains("'Who you are interested in'") && guide.Contains("a flirt in the street works the same way"),
            "romance: the Guide's People page says who Ask them out is for, with the age gap filled from the code");

        string within = "they are now within " + RomanceRules.AgeGap + " years of it";
        Check(AnyChangesRow(en, " Fixed: the people you can ask out or flirt with could be 60 or older whatever your own age; " + within + ".")
              && AnyChangesRow(en, " Fixed: whether somebody could be more than a friend was settled by a coin flip that ignored their sex, their age and how they answered your flirt in the street;")
              && AnyChangesRow(en, " The new option Who you are interested in, under Friends and social life, decides whether the people who can fall for you,")
              && !AnyChangesRow(en, "Fixed: The new option"),
            "romance: What's new says both fixes (each with its mark) and the new option (with none), in some row");
    }
}
