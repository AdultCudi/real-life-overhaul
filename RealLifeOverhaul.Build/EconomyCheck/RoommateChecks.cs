using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Roommates;

// The roommate's checks (Scripts/Home/Roommates/RoommateRules.cs, RoommateState.cs, RoommateStore.cs): who answers a
// notice from a seed, the hours they are in, the rent share and when it is late, how the relationship moves and where
// it stops, what a day brings by what they are like, that food taken is bounded, the room a home has from its
// furniture, every ending, the save line there and back, and how long the rules take.
internal static partial class Program
{
    private static void Roommates()
    {
        RoommateCandidates();
        RoommateFriends();
        RoommateHours();
        RoommateRent();
        RoommateRelationship();
        RoommateDays();
        RoommatePantry();
        RoommateRoom();
        RoommateEndings();
        RoommateStateChecks();
        RoommateSave();
        RoommateTiming();
    }

    private static SocialPerson RoommatePerson(float familiarity, float affection, float trust)
    {
        return new SocialPerson { Name = "Test Person", Met = true, Familiarity = familiarity, Affection = affection, Trust = trust };
    }

    // ---------------------------------------------------------------- who answers a notice

    private static void RoommateCandidates()
    {
        Check(RoommateRules.JobCount == SocialCatalogue.Jobs.Length - 1, "roommate, jobs: every job of the social cast but the last (retired) can be a roommate's");
        var list = new List<Flatmate>();
        var again = new List<Flatmate>();
        bool counts = true, names = true, traits = true, known = true, shares = true, answers = true, first = true, people = true, same = true, figures = true;
        int two = 0, three = 0, four = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            int stamp = 50 * 24 + 9;
            RoommateRules.Candidates(seed, 1, stamp, RoomKind.Bed, list);
            RoommateRules.Candidates(seed, 1, stamp, RoomKind.Bed, again);
            counts &= list.Count >= RoommateRules.CandidatesLeast && list.Count <= RoommateRules.CandidatesMost;
            two += list.Count == 2 ? 1 : 0;
            three += list.Count == 3 ? 1 : 0;
            four += list.Count == 4 ? 1 : 0;
            same &= again.Count == list.Count;
            first &= list.Count > 0 && list[0].Answers > stamp && list[0].Answers <= stamp + 4;
            for (int i = 0; i < list.Count; i++)
            {
                Flatmate c = list[i];
                same &= i < again.Count && again[i].Person.Name == c.Person.Name && again[i].Traits == c.Traits && again[i].SharePct == c.SharePct && again[i].Answers == c.Answers
                        && again[i].Person.Seed == c.Person.Seed;
                int count = RoommateRules.Count(c.Traits);
                traits &= (count == 2 || count == 3) && RoommateRules.Consistent(c.Traits);
                known &= RoommateRules.Count(c.Known) == 1 && (c.Known & c.Traits) == c.Known;
                shares &= c.SharePct >= RoommateRules.ShareLeast - 5 && c.SharePct <= RoommateRules.ShareMost + 5;
                answers &= c.Answers > stamp && c.Answers <= stamp + RoommateRules.AnswerHours && !c.Told && c.Friend == -1;
                people &= c.Person.Seed > 0 && c.Person.Age >= 20 && c.Person.Age <= 41 && c.Person.Job >= 0 && c.Person.Job < RoommateRules.JobCount
                          && !string.IsNullOrEmpty(c.Person.Name) && c.Person.Name.IndexOf(' ') > 0 && c.Person.Met;
                figures &= c.Person.Familiarity == RoommateRules.StartFamiliarity && c.Person.Affection == RoommateRules.StartAffection && c.Person.Trust == RoommateRules.StartTrust;
                for (int k = 0; k < i; k++)
                {
                    string a = c.Person.Name, b = list[k].Person.Name;
                    names &= a != b && a.Substring(a.IndexOf(' ')) != b.Substring(b.IndexOf(' '));
                }
            }
        }
        Check(counts, "roommate, a notice: between two and four people answer, for 400 seeds");
        Check(two > 60 && three > 60 && four > 60, "roommate, a notice: two, three and four answers all happen (" + two + ", " + three + ", " + four + ")");
        Check(same, "roommate, a notice: the same seed and notice give the same people, traits, shares and hours");
        Check(names, "roommate, a notice: no two applicants share a name or a surname");
        Check(traits, "roommate, a notice: everybody has two or three traits and never both sides of a pair");
        Check(known, "roommate, a notice: exactly one trait shows at once, and it is one they have");
        Check(shares, "roommate, a notice: the share offered for a bed is within " + (RoommateRules.ShareLeast - 5) + " to " + (RoommateRules.ShareMost + 5) + " per cent");
        Check(answers, "roommate, a notice: every answer comes after the notice and within " + RoommateRules.AnswerHours + " hours");
        Check(first, "roommate, a notice: the first answer comes within four hours");
        Check(people, "roommate, a notice: everybody has a look, an age of 20 to 41, a job and a first name and surname");
        Check(figures, "roommate, a notice: everybody starts at the same figures");

        RoommateRules.Candidates(7, 1, 1200, RoomKind.Bed, list);
        RoommateRules.Candidates(7, 2, 1200, RoomKind.Bed, again);
        Check(list[0].Person.Name != again[0].Person.Name || list[0].Person.Seed != again[0].Person.Seed, "roommate, a notice: the next notice brings different people");
        RoommateRules.Candidates(7, 1, 1200, RoomKind.Sofa, again);
        bool sofa = again.Count == list.Count;
        for (int i = 0; sofa && i < list.Count; i++)
        {
            sofa &= again[i].SharePct < list[i].SharePct && again[i].Person.Name == list[i].Person.Name;
        }
        Check(sofa, "roommate, a notice: the same people offer less for a sofa than for a bed");
        Check(RoommateRules.SharePct(50, RoomKind.Sofa) == 30 && RoommateRules.SharePct(50, RoomKind.Bed) == 50, "roommate, the share: a sofa is 60 per cent of the share for a bed");
        Check(RoommateRules.SharePct(0, RoomKind.Bed) == 5 && RoommateRules.SharePct(500, RoomKind.Bed) == 70, "roommate, the share: never under 5 or over 70 per cent");

        // Hidden traits show one at a time.
        RoomTrait all = RoomTrait.Messy | RoomTrait.Noisy | RoomTrait.Cooks;
        RoomTrait seen = RoomTrait.Noisy;
        Check(RoommateRules.Reveal(all, seen, 0) == RoomTrait.None && RoommateRules.Reveal(all, seen, RoommateRules.RevealDays - 1) == RoomTrait.None, "roommate, traits: nothing more shows before the days are up");
        RoomTrait next = RoommateRules.Reveal(all, seen, RoommateRules.RevealDays);
        Check(next == RoomTrait.Messy, "roommate, traits: the next hidden one shows after " + RoommateRules.RevealDays + " days");
        Check(RoommateRules.Reveal(all, all, 1000) == RoomTrait.None && RoommateRules.Reveal(RoomTrait.None, RoomTrait.None, 1000) == RoomTrait.None, "roommate, traits: with all known, or none to know, nothing shows");
        int steps = 0;
        for (int day = 0; day < 100 && seen != all; day++)
        {
            RoomTrait t = RoommateRules.Reveal(all, seen, day);
            seen |= t;
            steps += t != RoomTrait.None ? 1 : 0;
        }
        Check(seen == all && steps == 2, "roommate, traits: living together shows every trait in the end, one at a time");
    }

    private static void RoommateFriends()
    {
        var friend = new SocialPerson
        {
            Index = 3, Name = "Old Friend", Origin = SocialOrigin.Gym, Met = true, Stage = SocialStage.Friend, Trust = 50f, Affection = 55f, Familiarity = 60f, Job = 2, Seed = 99,
            Traits = SocialTrait.NightOwl | SocialTrait.Punctual | SocialTrait.Generous | SocialTrait.Foodie
        };
        Check(RoommateRules.FriendWould(friend), "roommate, a friend: a friend who trusts the player would move in");
        friend.Trust = RoommateRules.FriendAskTrust - 1f;
        Check(!RoommateRules.FriendWould(friend), "roommate, a friend: not with too little trust");
        friend.Trust = 50f;
        friend.Stage = SocialStage.Acquaintance;
        Check(!RoommateRules.FriendWould(friend), "roommate, a friend: not an acquaintance");
        friend.Stage = SocialStage.Partner;
        Check(!RoommateRules.FriendWould(friend), "roommate, a friend: not the player's partner");
        friend.Stage = SocialStage.Friend;
        friend.Origin = SocialOrigin.Family;
        Check(!RoommateRules.FriendWould(friend) && !RoommateRules.FriendWould(null), "roommate, a friend: not family, and nobody is not a friend");
        friend.Origin = SocialOrigin.Gym;
        friend.Met = false;
        Check(!RoommateRules.FriendWould(friend), "roommate, a friend: not somebody the player has not met");
        friend.Met = true;
        Flatmate c = RoommateRules.Friend(friend, RoomKind.Bed);
        Check(c.Known == c.Traits && RoommateRules.Consistent(c.Traits) && c.Traits != RoomTrait.None, "roommate, a friend: what they are like is known from the start, and never both sides of a pair");
        Check(RoommateRules.Has(c.Traits, RoomTrait.NightOwl) && !RoommateRules.Has(c.Traits, RoomTrait.EarlyBird) && RoommateRules.Has(c.Traits, RoomTrait.Cooks),
            "roommate, a friend: a night owl who likes food is a night owl who cooks");
        Check(c.Friend == 3 && c.Person.Name == "Old Friend" && c.Person.Seed == 99 && c.Person.Affection == 55f && c.Person.Trust == 50f,
            "roommate, a friend: the same person, the same look, the figures they already have");
        Check(c.SharePct == RoommateRules.FriendSharePct + 5 && RoommateRules.Friend(friend, RoomKind.Sofa).SharePct < c.SharePct, "roommate, a friend: a generous friend pays five more, and less for a sofa");
        friend.Job = 15;
        Check(RoommateRules.Friend(friend, RoomKind.Bed).Person.Job == RoommateRules.JobCount - 1, "roommate, a friend: a job outside the list is put inside it");
    }

    // ---------------------------------------------------------------- the hours they are in

    private static void RoommateHours()
    {
        // Day 2 is a Tuesday (weekday 1), day 7 a Sunday (weekday 6): PlannerTime.Weekday.
        Check(RoommateRules.AtWork(5, 2, 1, 10) && !RoommateRules.AtWork(5, 2, 1, 8) && !RoommateRules.AtWork(5, 2, 1, 17), "roommate, hours: an accountant works nine to five");
        Check(!RoommateRules.AtWork(5, 7, 6, 10) && !RoommateRules.AtWork(5, 6, 5, 10), "roommate, hours: and not at the weekend");
        Check(RoommateRules.AtWork(6, 3, 2, 23) && RoommateRules.AtWork(6, 4, 3, 1) && !RoommateRules.AtWork(6, 4, 3, 2), "roommate, hours: a bartender's Wednesday shift runs until two on Thursday morning");
        Check(!RoommateRules.AtWork(6, 2, 1, 20) && !RoommateRules.AtWork(6, 2, 1, 1), "roommate, hours: no shift on a Tuesday, and none left over from a Monday");
        Check(RoommateRules.AtWork(6, 8, 0, 1), "roommate, hours: Sunday's shift runs into Monday morning");
        Check(RoommateRules.AtWork(1, 3, 2, 7) && !RoommateRules.AtWork(1, 3, 2, 15) && RoommateRules.AtWork(1, 4, 3, 15) && !RoommateRules.AtWork(1, 4, 3, 7) && !RoommateRules.AtWork(1, 5, 4, 7) && !RoommateRules.AtWork(1, 5, 4, 15),
            "roommate, hours: a nurse works an early, a late, and has a day off");
        Check(!RoommateRules.AtWork(-1, 2, 1, 10) && !RoommateRules.AtWork(99, 2, 1, 10), "roommate, hours: a job outside the list is never at work");
        bool every = true, home = true;
        for (int job = 0; job < RoommateRules.JobCount; job++)
        {
            int worked = 0, inFlat = 0;
            for (int day = 1; day <= 21; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    int weekday = (day - 1) % 7;
                    worked += RoommateRules.AtWork(job, day, weekday, hour) ? 1 : 0;
                    inFlat += RoommateRules.Where(job, RoomTrait.None, 5, day, weekday, hour) == RoomWhere.In ? 1 : 0;
                }
            }
            every &= worked >= 21 * 4 && worked <= 21 * 10;
            home &= inFlat >= 21 * 3;
        }
        Check(every, "roommate, hours: every job has between four and ten hours of work a day on average");
        Check(home, "roommate, hours: whatever the job, they are in and awake at least three hours a day on average");

        RoommateRules.Sleep(5, RoomTrait.None, out int bed, out int rise);
        Check(bed == 23 && rise == 7, "roommate, sleep: eleven to seven by default");
        RoommateRules.Sleep(5, RoomTrait.EarlyBird, out bed, out rise);
        Check(bed == 22 && rise == 6, "roommate, sleep: an early bird, ten to six");
        RoommateRules.Sleep(12, RoomTrait.NightOwl, out bed, out rise);
        Check(bed == 2 && rise == 10, "roommate, sleep: a night owl with a late start, two to ten");
        RoommateRules.Sleep(0, RoomTrait.NightOwl, out bed, out rise);
        Check(rise == 5 && bed == 21, "roommate, sleep: a barista is up at five whatever they are like");
        RoommateRules.Sleep(6, RoomTrait.None, out bed, out rise);
        Check(bed == 2 && rise == 10, "roommate, sleep: a bartender sleeps after the shift");
        Check(RoommateRules.Where(5, RoomTrait.None, 5, 2, 1, 3) == RoomWhere.Asleep && RoommateRules.Where(5, RoomTrait.None, 5, 2, 1, 12) == RoomWhere.Work, "roommate, where: asleep at three, at work at noon");
        Check(RoommateRules.Where(6, RoomTrait.None, 5, 4, 3, 1) == RoomWhere.Work, "roommate, where: work comes before sleep");
        Check(RoommateRules.Where(5, RoomTrait.Private, 5, 2, 1, 21) == RoomWhere.InRoom && RoommateRules.Where(5, RoomTrait.Private, 5, 2, 1, 19) == RoomWhere.In,
            "roommate, where: somebody who keeps to themselves is in their room from nine");
        int outSociable = 0, outPlain = 0, outPrivate = 0;
        for (int day = 1; day <= 60; day++)
        {
            outSociable += RoommateRules.Where(5, RoomTrait.Sociable, 5, day, (day - 1) % 7, 19) == RoomWhere.Out ? 1 : 0;
            outPlain += RoommateRules.Where(5, RoomTrait.None, 5, day, (day - 1) % 7, 19) == RoomWhere.Out ? 1 : 0;
            outPrivate += RoommateRules.Where(5, RoomTrait.Private, 5, day, (day - 1) % 7, 19) == RoomWhere.Out ? 1 : 0;
        }
        Check(outSociable > outPlain && outPlain > 0 && outPrivate == 0, "roommate, where: the sociable are out more evenings than most, the private never (" + outSociable + ", " + outPlain + ", " + outPrivate + " of 60)");
        Check(RoommateRules.Hidden(RoomWhere.Asleep) && RoommateRules.Hidden(RoomWhere.InRoom) && !RoommateRules.Hidden(RoomWhere.In) && !RoommateRules.Hidden(RoomWhere.Work) && !RoommateRules.Hidden(RoomWhere.Out),
            "roommate, where: asleep and in their room are the two that are in the flat and not seen");
        Check(RoommateRules.Where(5, RoomTrait.None, 5, 2, 1, 19) == RoommateRules.Where(5, RoomTrait.None, 5, 2, 1, 19), "roommate, where: the same hour of the same day is always the same");
    }

    // ---------------------------------------------------------------- rent

    private static void RoommateRent()
    {
        Check(RoommateRules.Weekly(100f, 50) == 350 && RoommateRules.Weekly(33.3f, 45) == 105, "roommate, rent: a week's share is the daily rent times seven times the share");
        Check(RoommateRules.Weekly(0f, 50) == 0 && RoommateRules.Weekly(-5f, 50) == 0 && RoommateRules.Weekly(100f, 0) == 0, "roommate, rent: no rent or no share is nothing, never less");
        Check(RoommateRules.NextRentDay(10, 10) == 17 && RoommateRules.NextRentDay(10, 17) == 17 && RoommateRules.NextRentDay(10, 18) == 24 && RoommateRules.NextRentDay(10, 5) == 17,
            "roommate, rent: a rent day every seven days from the day they moved in");
        bool reliable = true, adds = true, shortRange = true, days = true;
        int late = 0, shorts = 0, plainLate = 0, plainShort = 0, lowLate = 0;
        for (int day = 1; day <= 2000; day++)
        {
            RentResult r = RoommateRules.Rent(11, day, RoomTrait.Reliable, 10, 315);
            reliable &= r.Outcome == RentOutcome.Paid && r.Paid == 315 && r.Owed == 0 && r.DueInDays == 0;
            r = RoommateRules.Rent(11, day, RoomTrait.LateWithRent, 50, 315);
            adds &= r.Paid + r.Owed == 315 && r.Paid >= 0 && r.Owed >= 0;
            late += r.Outcome == RentOutcome.Late ? 1 : 0;
            shorts += r.Outcome == RentOutcome.Short ? 1 : 0;
            shortRange &= r.Outcome != RentOutcome.Short || (r.Paid >= 315 * 50 / 100 && r.Paid <= 315 * 80 / 100 && r.Owed > 0);
            days &= r.Outcome == RentOutcome.Paid ? r.DueInDays == 0 : r.DueInDays >= 1 && r.DueInDays <= 4;
            r = RoommateRules.Rent(11, day, RoomTrait.None, 50, 315);
            plainLate += r.Outcome == RentOutcome.Late ? 1 : 0;
            plainShort += r.Outcome == RentOutcome.Short ? 1 : 0;
            adds &= r.Paid + r.Owed == 315;
            lowLate += RoommateRules.Rent(11, day, RoomTrait.None, RoommateRules.LowLevel - 1, 315).Outcome == RentOutcome.Late ? 1 : 0;
        }
        Check(reliable, "roommate, rent: somebody reliable pays in full on the day, 2000 rent days running");
        Check(adds, "roommate, rent: what is paid and what is owed always add up to the share");
        Check(late > 480 && late < 720, "roommate, rent: somebody late with rent pays nothing on about three rent days in ten (" + late + " of 2000)");
        Check(shorts > 380 && shorts < 620, "roommate, rent: and part of it on about one in four (" + shorts + " of 2000)");
        Check(shortRange, "roommate, rent: short is between half and four fifths paid");
        Check(days, "roommate, rent: the rest is promised within one to four days");
        Check(plainLate > 60 && plainLate < 200 && plainShort == 0, "roommate, rent: most people are late about one time in sixteen and never short (" + plainLate + " of 2000)");
        Check(lowLate > plainLate, "roommate, rent: lateness is commoner when the two do not get on (" + lowLate + " against " + plainLate + ")");
        RentResult none = RoommateRules.Rent(11, 5, RoomTrait.LateWithRent, 50, 0);
        Check(none.Paid == 0 && none.Owed == 0 && none.Outcome == RentOutcome.Paid, "roommate, rent: with nothing due nothing is paid or owed");
        Check(RoommateRules.Rent(11, 5, RoomTrait.LateWithRent, 50, 315).Outcome == RoommateRules.Rent(11, 5, RoomTrait.LateWithRent, 50, 315).Outcome, "roommate, rent: the same rent day always goes the same way");

        bool always = true, capped = true;
        int slipped = 0, chased = 0, chasedLow = 0;
        for (int day = 1; day <= 1000; day++)
        {
            always &= RoommateRules.PaysArrears(3, day, RoomTrait.None, 0) && RoommateRules.PaysWhenChased(3, day, RoomTrait.Tight, 0);
            capped &= RoommateRules.PaysArrears(3, day, RoomTrait.LateWithRent, RoommateRules.SlipsMost);
            slipped += RoommateRules.PaysArrears(3, day, RoomTrait.LateWithRent, 0) ? 0 : 1;
            chased += RoommateRules.PaysWhenChased(3, day, RoomTrait.LateWithRent, 80) ? 1 : 0;
            chasedLow += RoommateRules.PaysWhenChased(3, day, RoomTrait.LateWithRent, 0) ? 1 : 0;
        }
        Check(always, "roommate, arrears: anybody not late by nature pays on the promised day, and when asked");
        Check(capped, "roommate, arrears: a promise slips " + RoommateRules.SlipsMost + " times at most");
        Check(slipped > 250 && slipped < 450, "roommate, arrears: a late payer's promise slips about a third of the time (" + slipped + " of 1000)");
        Check(chased > chasedLow && chasedLow > 400 && chased < 1000, "roommate, arrears: chasing works more often when the two get on (" + chased + " against " + chasedLow + ")");
        Check(RoommateRules.BorrowAmount(0) == 20 && RoommateRules.BorrowAmount(315) == 30 && RoommateRules.BorrowAmount(1250) == 120 && RoommateRules.BorrowAmount(99999) == 200,
            "roommate, a loan: a tenth of a week's share in tens, between 20 and 200");
        Check(RoommateRules.Repays(3, 5, RoomTrait.Reliable) && RoommateRules.Repays(3, 5, RoomTrait.None), "roommate, a loan: anybody not late by nature repays on the day");
        Check(RoommateRules.SettledOnLeaving(200, RoomTrait.Reliable) == 200 && RoommateRules.SettledOnLeaving(200, RoomTrait.Generous) == 200 && RoommateRules.SettledOnLeaving(200, RoomTrait.Messy) == 100
              && RoommateRules.SettledOnLeaving(0, RoomTrait.None) == 0 && RoommateRules.SettledOnLeaving(-50, RoomTrait.Reliable) == 0,
            "roommate, leaving: the reliable and the generous settle in full, the rest pay half, nothing owed is nothing");
        Check(RoommateRules.FinalRent(350, 0) == 0 && RoommateRules.FinalRent(350, 3) == 150 && RoommateRules.FinalRent(350, 7) == 350 && RoommateRules.FinalRent(350, 30) == 350
              && RoommateRules.FinalRent(350, -4) == 0 && RoommateRules.FinalRent(0, 3) == 0,
            "roommate, leaving: the last days are paid by the day, never more than a week and never less than nothing");
    }

    // ---------------------------------------------------------------- the relationship

    private static void RoommateRelationship()
    {
        bool signs = true;
        RoomMoment[] good = { RoomMoment.DayTogether, RoomMoment.Talk, RoomMoment.WarmReply, RoomMoment.SharedMeal, RoomMoment.Lent, RoomMoment.Repaid, RoomMoment.CoveredRent, RoomMoment.Waited,
            RoomMoment.NoiseForgiven, RoomMoment.AskedQuietly, RoomMoment.RaidForgiven, RoomMoment.Thanked, RoomMoment.GoodLuck };
        RoomMoment[] bad = { RoomMoment.ShortReply, RoomMoment.RefusedLoan, RoomMoment.NotRepaid, RoomMoment.Chased, RoomMoment.ChaseRefused, RoomMoment.Row, RoomMoment.OverdueDay, RoomMoment.NoisyNight, RoomMoment.ToldToGo };
        for (int i = 0; i < good.Length; i++)
        {
            RoommateRules.Effect(good[i], out float f, out float a, out float t);
            signs &= f >= 0f && a >= 0f && t >= 0f && f + a + t > 0f;
        }
        Check(signs, "roommate, getting on: talks, meals, favours and patience never cost anything and always give something");
        signs = true;
        for (int i = 0; i < bad.Length; i++)
        {
            RoommateRules.Effect(bad[i], out float f, out float a, out float t);
            signs &= f <= 0f && a <= 0f && t <= 0f && f + a + t < 0f;
        }
        Check(signs, "roommate, getting on: short answers, rows and unpaid money never give anything and always cost something");
        Check(good.Length + bad.Length + 1 == Enum.GetValues(typeof(RoomMoment)).Length, "roommate, getting on: every moment is on one side or the other (asking them to stop is the one with both)");
        RoommateRules.Effect(RoomMoment.CoveredRent, out _, out float covered, out _);
        RoommateRules.Effect(RoomMoment.WarmReply, out _, out float warm, out _);
        RoommateRules.Effect(RoomMoment.Row, out _, out float row, out _);
        Check(covered > warm && -row > warm, "roommate, getting on: letting a week's rent go is worth more than a kind word, and a row costs more than one");

        SocialPerson p = RoommatePerson(RoommateRules.StartFamiliarity, RoommateRules.StartAffection, RoommateRules.StartTrust);
        for (int i = 0; i < 400; i++)
        {
            RoommateRules.Apply(p, RoomMoment.DayTogether);
        }
        Check(p.Familiarity == RoommateRules.FamiliarityByLiving && p.Affection == RoommateRules.StartAffection, "roommate, getting on: living together alone makes people familiar up to a point and no fonder");
        Check(RoommateRules.Level(p) < RoommateRules.FriendLevel, "roommate, getting on: time alone never makes a friend");
        for (int i = 0; i < 400; i++)
        {
            RoommateRules.Apply(p, RoomMoment.SharedMeal);
            RoommateRules.Apply(p, RoomMoment.CoveredRent);
        }
        Check(p.Familiarity == 100f && p.Affection == 100f && p.Trust == 100f && RoommateRules.Level(p) == 100, "roommate, getting on: nothing goes above a hundred");
        for (int i = 0; i < 400; i++)
        {
            RoommateRules.Apply(p, RoomMoment.Row);
            RoommateRules.Apply(p, RoomMoment.ChaseRefused);
        }
        Check(p.Affection == 0f && p.Trust == 0f && p.Familiarity == 100f && RoommateRules.Level(p) == 33, "roommate, getting on: nothing goes below nothing, and a row does not make strangers of them");
        Check(RoommateRules.Level(null) == 0, "roommate, getting on: nobody is at level nothing");
        Check(RoommateRules.Broken(RoommateRules.RowAffection - 0.1f, 50f) && RoommateRules.Broken(50f, RoommateRules.RowTrust - 0.1f) && !RoommateRules.Broken(RoommateRules.RowAffection, RoommateRules.RowTrust),
            "roommate, getting on: it is broken under either line and not on them");
        Check(!RoommateRules.Broken(RoommateRules.StartAffection, RoommateRules.StartTrust), "roommate, getting on: nobody starts broken");
        // The road to being friends is walkable in a few weeks of being decent.
        p = RoommatePerson(RoommateRules.StartFamiliarity, RoommateRules.StartAffection, RoommateRules.StartTrust);
        int daysToFriend = 0;
        while (RoommateRules.Level(p) < RoommateRules.FriendLevel && daysToFriend < 400)
        {
            daysToFriend++;
            RoommateRules.Apply(p, RoomMoment.DayTogether);
            RoommateRules.Apply(p, RoomMoment.Talk);
            RoommateRules.Apply(p, RoomMoment.WarmReply);
            if (daysToFriend % 3 == 0) RoommateRules.Apply(p, RoomMoment.SharedMeal);
        }
        Check(daysToFriend >= 14 && daysToFriend <= 60, "roommate, getting on: a talk a day and a meal every third makes a friend in two weeks to two months (" + daysToFriend + " days)");
    }

    // ---------------------------------------------------------------- what a day brings

    private static void RoommateDays()
    {
        var c = new DayContext { InAtNight = true, DaysIn = 5, Affection = 50f, Trust = 50f, Level = 50 };
        int noisy = 0, asked = 0, owl = 0, quiet = 0, away = 0, cooks = 0, cooksNot = 0, raids = 0, raidsStopped = 0, raidsFull = 0, borrow = 0, borrowLate = 0, borrowNo = 0, notice = 0, noticeEarly = 0, noticeLow = 0;
        bool oneOrOther = true, same = true, rows = true;
        for (int day = 1; day <= 2000; day++)
        {
            noisy += (RoommateRules.Day(9, day, RoomTrait.Noisy, c) & DayEvents.Noise) != 0 ? 1 : 0;
            owl += (RoommateRules.Day(9, day, RoomTrait.NightOwl, c) & DayEvents.Noise) != 0 ? 1 : 0;
            quiet += (RoommateRules.Day(9, day, RoomTrait.Quiet | RoomTrait.NightOwl, c) & DayEvents.Noise) != 0 ? 1 : 0;
            quiet += (RoommateRules.Day(9, day, RoomTrait.None, c) & DayEvents.Noise) != 0 ? 1 : 0;
            cooks += (RoommateRules.Day(9, day, RoomTrait.Cooks, c) & DayEvents.Cooked) != 0 ? 1 : 0;
            cooksNot += (RoommateRules.Day(9, day, RoomTrait.RaidsFridge, c) & DayEvents.Cooked) != 0 ? 1 : 0;
            raids += (RoommateRules.Day(9, day, RoomTrait.RaidsFridge, c) & DayEvents.Raid) != 0 ? 1 : 0;
            cooksNot += (RoommateRules.Day(9, day, RoomTrait.Cooks, c) & DayEvents.Raid) != 0 ? 1 : 0;
            borrow += (RoommateRules.Day(9, day, RoomTrait.None, c) & DayEvents.Borrow) != 0 ? 1 : 0;
            borrowLate += (RoommateRules.Day(9, day, RoomTrait.LateWithRent, c) & DayEvents.Borrow) != 0 ? 1 : 0;
            borrowNo += (RoommateRules.Day(9, day, RoomTrait.Reliable, c) & DayEvents.Borrow) != 0 ? 1 : 0;
            DayEvents tidy = RoommateRules.Day(9, day, RoomTrait.Tidy, c), messy = RoommateRules.Day(9, day, RoomTrait.Messy, c);
            oneOrOther &= (tidy & DayEvents.Tidied) != 0 && (tidy & DayEvents.Mess) == 0 && (messy & DayEvents.Mess) != 0 && (messy & DayEvents.Tidied) == 0;
            same &= RoommateRules.Day(9, day, RoomTrait.Noisy | RoomTrait.Cooks, c) == RoommateRules.Day(9, day, RoomTrait.Noisy | RoomTrait.Cooks, c);

            DayContext other = c;
            other.QuietAsked = true;
            asked += (RoommateRules.Day(9, day, RoomTrait.Noisy, other) & DayEvents.Noise) != 0 ? 1 : 0;
            quiet += (RoommateRules.Day(9, day, RoomTrait.NightOwl, other) & DayEvents.Noise) != 0 ? 1 : 0;
            other = c;
            other.InAtNight = false;
            away += (RoommateRules.Day(9, day, RoomTrait.Noisy, other) & DayEvents.Noise) != 0 ? 1 : 0;
            other = c;
            other.RaidStopped = true;
            raidsStopped += (RoommateRules.Day(9, day, RoomTrait.RaidsFridge, other) & DayEvents.Raid) != 0 ? 1 : 0;
            other = c;
            other.RaidsThisWeek = RoommateRules.RaidsPerWeek;
            raidsFull += (RoommateRules.Day(9, day, RoomTrait.RaidsFridge, other) & DayEvents.Raid) != 0 ? 1 : 0;
            other = c;
            other.Owes = true;
            borrowNo += (RoommateRules.Day(9, day, RoomTrait.LateWithRent, other) & DayEvents.Borrow) != 0 ? 1 : 0;
            other = c;
            other.HasLoan = true;
            borrowNo += (RoommateRules.Day(9, day, RoomTrait.LateWithRent, other) & DayEvents.Borrow) != 0 ? 1 : 0;
            other = c;
            other.OnNotice = true;
            borrowNo += (RoommateRules.Day(9, day, RoomTrait.LateWithRent, other) & (DayEvents.Borrow | DayEvents.Notice)) != 0 ? 1 : 0;
            other = c;
            other.DaysIn = 2;
            borrowNo += (RoommateRules.Day(9, day, RoomTrait.LateWithRent, other) & DayEvents.Borrow) != 0 ? 1 : 0;
            noticeEarly += (RoommateRules.Day(9, day, RoomTrait.None, other) & DayEvents.Notice) != 0 ? 1 : 0;
            other = c;
            other.DaysIn = 30;
            notice += (RoommateRules.Day(9, day, RoomTrait.None, other) & DayEvents.Notice) != 0 ? 1 : 0;
            other.Level = RoommateRules.LowLevel - 1;
            noticeLow += (RoommateRules.Day(9, day, RoomTrait.None, other) & DayEvents.Notice) != 0 ? 1 : 0;
            other = c;
            other.DaysIn = 1;
            other.Affection = RoommateRules.RowAffection - 1f;
            rows &= (RoommateRules.Day(9, day, RoomTrait.None, other) & DayEvents.Notice) != 0;
        }
        Check(noisy > 480 && noisy < 720, "roommate, a day: a noisy roommate has a loud night about three nights in ten (" + noisy + " of 2000)");
        Check(asked > 120 && asked < 290 && asked < noisy, "roommate, a day: asked to keep it down, about one in ten (" + asked + ")");
        Check(owl > 120 && owl < 290, "roommate, a day: a night owl who is not noisy by nature, about one in ten (" + owl + ")");
        Check(quiet == 0, "roommate, a day: the quiet, the ordinary and a night owl who was asked are never loud");
        Check(away == 0, "roommate, a day: nobody is loud at home on a night they were at work");
        Check(cooks > 580 && cooks < 820 && cooksNot == 0, "roommate, a day: only somebody who cooks leaves a meal, about one evening in three (" + cooks + "), and they never raid");
        Check(raids > 480 && raids < 720, "roommate, a day: somebody who raids the fridge tries about three days in ten (" + raids + ")");
        Check(raidsStopped == 0 && raidsFull == 0, "roommate, a day: never once asked to stop, never past the week's limit");
        Check(oneOrOther, "roommate, a day: every day the flat is either tidied or made messier, never both");
        Check(borrow > 40 && borrow < 130 && borrowLate > borrow, "roommate, a day: people ask to borrow now and then, late payers twice as often (" + borrow + ", " + borrowLate + ")");
        Check(borrowNo == 0, "roommate, a day: nobody asks to borrow while they owe, on notice, in the first days, or if they are reliable");
        Check(noticeEarly == 0, "roommate, a day: nobody leaves of their own accord in the first " + RoommateRules.MinStayDays + " days");
        Check(notice > 5 && notice < 60 && noticeLow > notice * 3, "roommate, a day: people move on now and then, much sooner when the two do not get on (" + notice + ", " + noticeLow + " of 2000)");
        Check(rows, "roommate, a day: once it is broken they give notice that very day, however new they are");
        Check(same, "roommate, a day: the same day always brings the same things");
        Check(RoommateRules.TidyChange(RoomTrait.Tidy) > 0f && RoommateRules.TidyChange(RoomTrait.Messy) < RoommateRules.TidyChange(RoomTrait.None) && RoommateRules.TidyChange(RoomTrait.None) < 0f,
            "roommate, the flat: a tidy one cleans, a messy one dirties more than one more person does");
        Check(RoommateRules.Roll(1, 2, 3) == RoommateRules.Roll(1, 2, 3) && RoommateRules.Roll(1, 2, 3) != RoommateRules.Roll(1, 2, 4), "roommate, chance: the same three numbers give the same figure, another purpose another");
        bool inRange = true;
        var buckets = new int[10];
        for (int i = 0; i < 20000; i++)
        {
            int roll = RoommateRules.Roll(i * 7919, i, 5);
            inRange &= roll >= 0 && roll < 10000;
            buckets[roll / 1000]++;
        }
        bool even = true;
        for (int i = 0; i < 10; i++)
        {
            even &= buckets[i] > 1800 && buckets[i] < 2200;
        }
        Check(inRange && even, "roommate, chance: figures run from 0 to 9999 and are spread evenly");
    }

    // ---------------------------------------------------------------- food taken is bounded

    private static void RoommatePantry()
    {
        Check(RoommateRules.RaidTake(10, 0, false) == 1, "roommate, the fridge: one unit at a time");
        Check(RoommateRules.RaidTake(RoommateRules.RaidLeaves, 0, false) == 0 && RoommateRules.RaidTake(0, 0, false) == 0 && RoommateRules.RaidTake(-3, 0, false) == 0,
            "roommate, the fridge: nothing when " + RoommateRules.RaidLeaves + " or fewer are left, or none at all");
        Check(RoommateRules.RaidTake(RoommateRules.RaidLeaves + 1, 0, false) == 1, "roommate, the fridge: one when there is one more than that");
        Check(RoommateRules.RaidTake(10, RoommateRules.RaidsPerWeek, false) == 0 && RoommateRules.RaidTake(10, RoommateRules.RaidsPerWeek + 5, false) == 0, "roommate, the fridge: nothing past the week's limit");
        Check(RoommateRules.RaidTake(1000, 0, true) == 0, "roommate, the fridge: nothing once asked to stop");
        // A year with a full fridge that is never refilled: what goes is bounded by the week and by what is left.
        int units = 40, taken = 0, raidsThisWeek = 0, mostInWeek = 0;
        var c = new DayContext { InAtNight = true, DaysIn = 5, Affection = 50f, Trust = 50f, Level = 50 };
        for (int day = 1; day <= 365; day++)
        {
            if (day % 7 == 0)
            {
                raidsThisWeek = 0;
            }
            c.RaidsThisWeek = raidsThisWeek;
            if ((RoommateRules.Day(21, day, RoomTrait.RaidsFridge, c) & DayEvents.Raid) != 0)
            {
                int take = RoommateRules.RaidTake(units, raidsThisWeek, false);
                units -= take;
                taken += take;
                raidsThisWeek += take;
                mostInWeek = Math.Max(mostInWeek, raidsThisWeek);
            }
        }
        Check(units == RoommateRules.RaidLeaves && taken == 40 - RoommateRules.RaidLeaves, "roommate, the fridge: a year empties a fridge of 40 down to " + RoommateRules.RaidLeaves + " and no further (" + units + " left)");
        Check(mostInWeek <= RoommateRules.RaidsPerWeek && mostInWeek > 0, "roommate, the fridge: never more than " + RoommateRules.RaidsPerWeek + " in a week (" + mostInWeek + ")");
    }

    // ---------------------------------------------------------------- the room a home has

    private static void RoommateRoom()
    {
        string[] beds = { "itemname_bed1", "itemname_beddoubleiron", "itemname_beddoublemoderncanopy", "itemname_bedindividualwooden", "itemname_fullsizebedmodern", "itemname_kingsizebed" };
        string[] notBeds = { "itemname_bedsidetable", "itemname_bedsidetablelampclassic", "itemname_flatbed", "itemname_flatbedspawnerstacked", "itemname_sofaluxury", "itemname_fridge", "", null };
        string[] sofas = { "itemname_cornersofa01", "itemname_modularsofa1l", "itemname_sofaluxury", "itemname_sofaluxurymodularcornerleftpillow", "itemname_couch" };
        bool ok = true;
        for (int i = 0; i < beds.Length; i++) ok &= RoommateRules.IsBed(beds[i]) && !RoommateRules.IsSofa(beds[i]);
        Check(ok, "roommate, furniture: every bed the game has is a bed, single or double");
        ok = true;
        for (int i = 0; i < notBeds.Length; i++) ok &= !RoommateRules.IsBed(notBeds[i]);
        Check(ok, "roommate, furniture: a bedside table, a flatbed trolley, a sofa and nothing are not beds");
        ok = true;
        for (int i = 0; i < sofas.Length; i++) ok &= RoommateRules.IsSofa(sofas[i]) && !RoommateRules.IsBed(sofas[i]);
        Check(ok && !RoommateRules.IsSofa(null) && !RoommateRules.IsSofa("itemname_armchair"), "roommate, furniture: every sofa is a sofa, an armchair is not");
        Check(RoommateRules.Room(2, 0, false) == RoomKind.Bed && RoommateRules.Room(5, 3, false) == RoomKind.Bed, "roommate, room: a second bed is a room of their own");
        Check(RoommateRules.Room(1, 1, false) == RoomKind.Sofa && RoommateRules.Room(0, 1, false) == RoomKind.Sofa, "roommate, room: with one bed or none, a sofa will do");
        Check(RoommateRules.Room(1, 0, false) == RoomKind.None && RoommateRules.Room(0, 0, false) == RoomKind.None, "roommate, room: one bed and no sofa is no room");
        Check(RoommateRules.Room(1, 2, true) == RoomKind.None && RoommateRules.Room(2, 0, true) == RoomKind.Bed, "roommate, room: with a partner living in it has to be a second bed");
    }

    // ---------------------------------------------------------------- every ending

    private static void RoommateEndings()
    {
        Check(RoommateRules.Leaving(1, 5, 2, 50, RoommateRules.RowAffection - 1f, 50f) == EndWhy.Row && RoommateRules.Leaving(1, 5, 200, 50, 50f, RoommateRules.RowTrust - 1f) == EndWhy.Row,
            "roommate, ending: broken affection or trust ends in a row, on any day");
        bool none = true;
        int job = 0, partner = 0, row = 0;
        for (int day = 1; day <= 20000; day++)
        {
            none &= RoommateRules.Leaving(4, day, RoommateRules.MinStayDays - 1, 50, 50f, 50f) == EndWhy.None;
            EndWhy why = RoommateRules.Leaving(4, day, 40, 50, 50f, 50f);
            job += why == EndWhy.NewJob ? 1 : 0;
            partner += why == EndWhy.NewPartner ? 1 : 0;
            row += why == EndWhy.Row ? 1 : 0;
        }
        Check(none, "roommate, ending: nobody who gets on leaves before " + RoommateRules.MinStayDays + " days");
        Check(job > 50 && partner > 50 && row == 0, "roommate, ending: those who get on leave for a new job or a partner, never in a row (" + job + ", " + partner + ")");
        Check(RoommateRules.Leaving(4, 77, 40, RoommateRules.LowLevel - 1, 50f, 50f) == EndWhy.None || RoommateRules.Leaving(4, 77, 40, RoommateRules.LowLevel - 1, 50f, 50f) == EndWhy.Row,
            "roommate, ending: those who do not get on leave in a row or not at all");
        Check(RoommateRules.OnMove(false, RoomKind.Bed, 100) == EndWhy.HomeGivenUp, "roommate, ending: a home given up with no other ends it");
        Check(RoommateRules.OnMove(true, RoomKind.None, 100) == EndWhy.NoRoomAfterMove, "roommate, ending: a new home without room ends it, however close");
        Check(RoommateRules.OnMove(true, RoomKind.Bed, RoommateRules.GoodLevel - 1) == EndWhy.NotCloseEnoughToMove, "roommate, ending: they do not move with somebody they are not close to");
        Check(RoommateRules.OnMove(true, RoomKind.Bed, RoommateRules.GoodLevel) == EndWhy.None && RoommateRules.OnMove(true, RoomKind.Sofa, 100) == EndWhy.None, "roommate, moving: with room and a good level they come along, to a sofa too");
        Check(RoommateRules.OnPartner(2) == EndWhy.None && RoommateRules.OnPartner(1) == EndWhy.PartnerMovedIn && RoommateRules.OnPartner(0) == EndWhy.PartnerMovedIn,
            "roommate, ending: when the partner moves in they stay only with a bed of their own");
        bool never = true;
        int stays = 0;
        for (int day = 1; day <= 1000; day++)
        {
            never &= !RoommateRules.StaysWhenAsked(2, day, 100, EndWhy.Row) && !RoommateRules.StaysWhenAsked(2, day, 100, EndWhy.PlayerNotice) && !RoommateRules.StaysWhenAsked(2, day, RoommateRules.FriendLevel - 1, EndWhy.NewJob);
            stays += RoommateRules.StaysWhenAsked(2, day, RoommateRules.FriendLevel, EndWhy.NewJob) ? 1 : 0;
        }
        Check(never, "roommate, ending: asking does not keep somebody who leaves in a row, was told to go, or is not close");
        Check(stays > 400 && stays < 600, "roommate, ending: somebody close stays when asked about half the time (" + stays + " of 1000)");
        Check(!RoommateRules.Gone(0, 50) && !RoommateRules.Gone(51, 50) && RoommateRules.Gone(50, 50) && RoommateRules.Gone(40, 50), "roommate, ending: gone on and after the day the notice runs out, never without one");
    }

    // ---------------------------------------------------------------- the state

    private static RoommateState RoommateLiving()
    {
        var list = new List<Flatmate>();
        RoommateRules.Candidates(5, 1, 20 * 24 + 8, RoomKind.Bed, list);
        var s = new RoommateState { Seed = 5, LastDay = 20, LastStamp = 20 * 24 + 8, AdUp = true, AdStamp = 20 * 24 + 8, Ads = 1 };
        s.Candidates.AddRange(list);
        s.MoveIn(list[0], 20, "Main St & 5th | odd^name;", 12, false, false, 315);
        return s;
    }

    private static void RoommateStateChecks()
    {
        RoommateState s = RoommateLiving();
        Check(s.Living && !s.AdUp && s.Candidates.Count == 0 && s.MovedIn == 20 && s.RentDay == 27 && s.LastRentDay == 20 && s.Weekly == 315 && !s.OnNotice,
            "roommate, moving in: the notice comes down, the first rent day is a week on");
        s.Topics = Topic.Cooked | Topic.Rent | Topic.Noise;
        Check(s.NextTopic() == Topic.Rent, "roommate, talks: rent comes before noise and a meal");
        s.Topics |= Topic.Leaving | Topic.PartnerIn;
        Check(s.NextTopic() == Topic.Leaving, "roommate, talks: their notice comes first of all");
        s.Topics = Topic.None;
        Check(s.NextTopic() == Topic.None, "roommate, talks: with nothing waiting there is no topic");
        s.GiveNotice(EndWhy.None, 22);
        Check(!s.OnNotice, "roommate, notice: no reason is no notice");
        s.GiveNotice(EndWhy.NewJob, 22);
        Check(s.OnNotice && s.LeaveDay == 22 + RoommateRules.NoticeDays && !RoommateRules.Gone(s.LeaveDay, 24) && RoommateRules.Gone(s.LeaveDay, 25), "roommate, notice: they are gone " + RoommateRules.NoticeDays + " days later");
        s.Topics = Topic.Leaving | Topic.Rent;
        s.TakeBackNotice();
        Check(!s.OnNotice && s.LeaveDay == 0 && s.Topics == Topic.Rent, "roommate, notice: taken back, it and its topic are gone and the rest stays");
        s.Owed = 100;
        s.OwedDue = 29;
        s.Loan = 40;
        s.LoanDue = 30;
        s.GiveNotice(EndWhy.PlayerNotice, 24);
        s.Shift(10);
        Check(s.MovedIn == 30 && s.RentDay == 37 && s.LastRentDay == 30 && s.OwedDue == 39 && s.LoanDue == 40 && s.LeaveDay == 37, "roommate, switched off and on: every date moves on by the days it was off");
        s.Shift(0);
        s.Shift(-5);
        Check(s.MovedIn == 30 && s.RentDay == 37, "roommate, switched off and on: no days, or fewer, move nothing");
        s.ClearMate();
        Check(!s.Living && s.Owed == 0 && s.Loan == 0 && s.Topics == Topic.None && s.HomeStreet == null && !s.OnNotice && s.Seed == 5 && s.Ads == 1, "roommate, gone: nothing of them is left, the seed and the notice count are kept");
        s.GiveNotice(EndWhy.NewJob, 30);
        s.Shift(3);
        Check(!s.OnNotice && s.LeaveDay == 0, "roommate, gone: nobody can be given notice, and nothing moves");
        var ad = new RoommateState { AdUp = true, AdStamp = 100 };
        RoommateRules.Candidates(5, 1, 100, RoomKind.Bed, ad.Candidates);
        int answers = ad.Candidates[0].Answers;
        ad.Shift(2);
        Check(ad.AdStamp == 148 && ad.Candidates[0].Answers == answers + 48, "roommate, switched off and on: a notice and its answers move on too");
    }

    // ---------------------------------------------------------------- the save

    private static void RoommateSave()
    {
        RoommateState s = RoommateLiving();
        s.Mate.Known = s.Mate.Traits;
        s.Mate.Person.Affection = 61.5f;
        s.Owed = 126;
        s.OwedDue = 29;
        s.OwedSlips = 1;
        s.Loan = 40;
        s.LoanDue = 31;
        s.BorrowAsk = 30;
        s.Topics = Topic.Rent | Topic.Raid;
        s.RaidItem = "itemname_pizza";
        s.RaidWeek = 1;
        s.Raids = 2;
        s.RaidStopped = true;
        s.QuietAsked = true;
        s.Covered = 1;
        s.Meals = 3;
        s.LastTalkDay = 21;
        s.LastMealDay = 20;
        s.LastCompanyDay = 21;
        s.LastGreetStamp = 500;
        s.MadeFriend = true;
        s.Mate.Friend = 5;
        s.Introduced = true;
        s.PartnerSeen = true;
        s.Lodger = true;
        s.OnSofa = true;
        s.GiveNotice(EndWhy.NewPartner, 22);
        string line = RoommateStore.Write(s);
        RoommateState r = RoommateStore.Read(line, out int version);
        Check(r != null && version == RoommateStore.Version, "roommate, the save: a line written is read back at its version");
        Check(r != null && RoommateStore.Write(r) == line, "roommate, the save: read and written again it is the same line, character for character");
        Check(r != null && r.Living && r.Mate.Person.Name == s.Mate.Person.Name && r.Mate.Person.Seed == s.Mate.Person.Seed && r.Mate.Person.Female == s.Mate.Person.Female && r.Mate.Person.Age == s.Mate.Person.Age
              && r.Mate.Person.Job == s.Mate.Person.Job && r.Mate.Traits == s.Mate.Traits && r.Mate.Known == s.Mate.Known && r.Mate.SharePct == s.Mate.SharePct && r.Mate.Friend == 5
              && r.Mate.Person.Affection == 61.5f && r.Mate.Person.Trust == s.Mate.Person.Trust, "roommate, the save: the person comes back whole");
        Check(r != null && r.HomeStreet == "Main St & 5th | odd^name;" && r.HomeNumber == 12, "roommate, the save: an address with every dividing character in it comes back as it was");
        Check(r != null && r.Owed == 126 && r.OwedDue == 29 && r.OwedSlips == 1 && r.Loan == 40 && r.LoanDue == 31 && r.BorrowAsk == 30 && r.Weekly == 315 && r.RentDay == 27 && r.LastRentDay == 20 && r.MovedIn == 20,
            "roommate, the save: the money and the dates come back");
        Check(r != null && r.Topics == (Topic.Rent | Topic.Raid) && r.RaidItem == "itemname_pizza" && r.Raids == 2 && r.RaidWeek == 1 && r.RaidStopped && r.QuietAsked && r.Covered == 1 && r.Meals == 3
              && r.LastTalkDay == 21 && r.LastMealDay == 20 && r.LastCompanyDay == 21 && r.LastGreetStamp == 500, "roommate, the save: what waits to be talked about and what was agreed come back");
        Check(r != null && r.MadeFriend && r.Introduced && r.PartnerSeen && r.Lodger && r.OnSofa && r.Notice == EndWhy.NewPartner && r.LeaveDay == 25 && r.Seed == 5 && r.LastDay == 20 && r.Ads == 1,
            "roommate, the save: the flags, the notice and the header come back");

        var ad = new RoommateState { Seed = 9, LastDay = 4, LastStamp = 100, AdUp = true, AdStamp = 100, Ads = 3 };
        RoommateRules.Candidates(9, 3, 100, RoomKind.Sofa, ad.Candidates);
        ad.Candidates[0].Told = true;
        RoommateState back = RoommateStore.Read(RoommateStore.Write(ad), out version);
        bool same = back != null && !back.Living && back.AdUp && back.AdStamp == 100 && back.Ads == 3 && back.Candidates.Count == ad.Candidates.Count && back.Candidates[0].Told;
        for (int i = 0; same && i < ad.Candidates.Count; i++)
        {
            same &= back.Candidates[i].Person.Name == ad.Candidates[i].Person.Name && back.Candidates[i].Traits == ad.Candidates[i].Traits && back.Candidates[i].Known == ad.Candidates[i].Known
                    && back.Candidates[i].Answers == ad.Candidates[i].Answers && back.Candidates[i].SharePct == ad.Candidates[i].SharePct && back.Candidates[i].Friend == -1;
        }
        Check(same, "roommate, the save: a notice and everybody who answered come back");

        var empty = new RoommateState { Seed = 77 };
        back = RoommateStore.Read(RoommateStore.Write(empty), out version);
        Check(back != null && back.Seed == 77 && !back.Living && !back.AdUp && back.Candidates.Count == 0, "roommate, the save: with nobody and no notice, only the seed");

        Check(RoommateStore.Read(null, out version) == null && RoommateStore.Read(string.Empty, out version) == null && RoommateStore.Read("hello", out version) == null
              && RoommateStore.Read("v=x^h:1", out version) == null && RoommateStore.Read("v=0^h:1", out version) == null && RoommateStore.Read("v=1", out version) == null,
            "roommate, the save: nothing, another feature's line, a bad version and a line without a header are not read");
        Check(RoommateStore.Load(null, out version) == null && RoommateStore.Load(new Dictionary<string, string>(), out version) == null, "roommate, the save: a save made without the feature has none, and loads");
        var data = new Dictionary<string, string> { { RoommateStore.Key, line } };
        Check(RoommateStore.Load(data, out version) != null && RoommateStore.Key == "reallife:home_roommate", "roommate, the save: found under its own key");

        back = RoommateStore.Read("v=7^h:5|20|488|0|0|1|9|9|9^z:something new^m:" + line.Substring(line.IndexOf("^m:", StringComparison.Ordinal) + 3) + "|99|99", out version);
        Check(back != null && version == 7 && back.Living && back.Mate.Person.Name == s.Mate.Person.Name && back.Notice == EndWhy.NewPartner,
            "roommate, the save: a line from a later version loads, with its new part and its extra fields passed over");
        back = RoommateStore.Read("v=1^h:5|20^m:Sam%20Test|1|30|44|2|0|150|250|250|3|1|45", out version);
        Check(back != null && !back.Living, "roommate, the save: a person without an arrangement is nobody living in");
        back = RoommateStore.Read("v=1^h:5|20^m:Sam%20Test|1|30|44|2|0|150|250|250|3|1|45^a:Elm%20St|3|0|0|18|25", out version);
        Check(back != null && back.Living && back.Mate.Person.Name == "Sam Test" && back.HomeStreet == "Elm St" && back.RentDay == 25 && back.Owed == 0 && back.Topics == Topic.None && back.Mate.Friend == -1 && !back.OnNotice,
            "roommate, the save: a short line from an older version loads with everything missing at nothing");
        back = RoommateStore.Read("v=1^h:5|20^m:Sam%20Test|1|900|0|77|0|99999|-50|250|3|16383|999^a:Elm%20St|3|0|0|18|2|2|-9|-9|5|77|-1|-1|9999|255|x|1|99|0|0|0|0|0|0|0|0|0|0|0|42|1", out version);
        Check(back != null && back.Mate.Person.Age == 99 && back.Mate.Person.Seed == 1 && back.Mate.Person.Job == RoommateRules.JobCount - 1 && back.Mate.Person.Familiarity == 100f && back.Mate.Person.Affection == 0f
              && back.Mate.SharePct == 70 && back.Mate.Known == back.Mate.Traits, "roommate, the save: a person's figures out of their bounds are put inside them");
        Check(back != null && back.RentDay == 18 && back.LastRentDay == 18 && back.Weekly == 0 && back.Owed == 0 && back.OwedDue == 0 && back.OwedSlips == 0 && back.Loan == 0 && back.BorrowAsk == 200
              && back.Raids == RoommateRules.RaidsPerWeek && back.Notice == EndWhy.None && back.LeaveDay == 0 && (back.Topics & Topic.Rent) == 0,
            "roommate, the save: an arrangement's figures out of their bounds are put inside them, and rent that is not owed is not a topic");
        back = RoommateStore.Read("v=1^h:5|20^m:Sam%20Test|1|30|44|2|0|150|250|250|3|3|45^a:Elm%20St|3", out version);
        Check(back != null && back.Mate.Traits == RoomTrait.None, "roommate, the save: both sides of a pair cannot be, and are dropped");
        back = RoommateStore.Read("v=1^h:5|20|0|0|100|2^c:Sam%20Test|1|30|44|2|0|150|250|250|4|4|45|120|1", out version);
        Check(back != null && back.Candidates.Count == 0, "roommate, the save: applicants without a notice up are dropped");
    }

    // ---------------------------------------------------------------- how long it takes

    private static void RoommateTiming()
    {
        var c = new DayContext { InAtNight = true, DaysIn = 30, Affection = 50f, Trust = 50f, Level = 50 };
        int sink = 0;
        for (int i = 0; i < 20000; i++)   // (the runtime compiles it before the clock starts)
        {
            sink += (int)RoommateRules.Where(i % RoommateRules.JobCount, RoomTrait.Sociable, i, i, i % 7, i % 24) + (int)RoommateRules.Day(i, i, RoomTrait.Noisy | RoomTrait.Cooks, c)
                    + RoommateRules.Rent(i, i, RoomTrait.LateWithRent, 50, 315).Paid;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        const int Runs = 200000;
        for (int i = 0; i < Runs; i++)
        {
            sink += (int)RoommateRules.Where(i % RoommateRules.JobCount, RoomTrait.Sociable, i, i, i % 7, i % 24) + (int)RoommateRules.Day(i, i, RoomTrait.Noisy | RoomTrait.Cooks, c)
                    + RoommateRules.Rent(i, i, RoomTrait.LateWithRent, 50, 315).Paid;
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = watch.Elapsed.TotalMilliseconds * 1000.0 / Runs;
        Check(allocated == 0, "roommate, cost: where they are, a day's events and a rent day allocate nothing (" + allocated + " bytes over " + Runs + " of each)");
        Check(each < 5.0, "roommate, cost: all three together take under 5 microseconds (" + each.ToString("F3") + ", checksum " + (sink & 1) + ")");
        RoommateState s = RoommateLiving();
        watch.Restart();
        for (int i = 0; i < 2000; i++)
        {
            sink += RoommateStore.Read(RoommateStore.Write(s), out _).Weekly;
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds / 2000.0 < 0.5, "roommate, cost: writing the save line and reading it back takes under half a millisecond (" + (watch.Elapsed.TotalMilliseconds / 2000.0).ToString("F4") + " ms)");
    }
}
