using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Family;

// A player and contributor (19 Oct 2026): "difficult moments dont use triggers but are random". They were:
// HardTimes.Daily rolled UnityEngine.Random once a game day, 4 % in a warm home, 10 % in a so so one and 20 % in a
// tense one, halved for two who live apart, two days apart at the least, and Argue dealt the ten moments from an
// unseeded shuffle. So "it is past ten, I ate by myself again" came to a couple who are home every evening, and "I see
// the bank statement" to a player who never spent. Now HardTimesWatch reads what is going on once a game hour and day
// (the evening at ten, the night, the household, home life, careers, the social life, travel and the money) and
// HardTimesRules decides: a moment comes of its own causes once they have lasted, at its hour, the days since the last
// allowing; a cause gone takes its moments with it; with nothing wrong only the towel comes, rarely. First each
// moment's causes, then nothing when all is well, a cause gone, the options and presets, the save's line, determinism
// and variety, the sources, the cost and the texts.
internal static partial class Program
{
    private const int HardSeed = 424242;

    private static void HardTimesTriggers()
    {
        HardMoments();
        HardWhen();
        HardCalm();
        HardGone();
        HardOptions();
        HardSaveLine();
        HardSameState();
        HardSources();
        HardPace();
        HardWords();
    }

    // ---------------------------------------------------------------- the scenes

    // Two who live together (or not) on a day with nothing wrong: harmony 60, home life known and fine, a word today,
    // money in hand.
    private static HardSigns HardHome(int day, int hour, bool livesIn = true)
    {
        HardSigns g = HardSigns.Unknown(day, hour);
        g.LivesIn = livesIn;
        g.Committed = livesIn;
        g.Harmony = 60f;
        g.TalkDay = day;
        g.SharedDay = day - 1;
        g.YearDays = 60;
        g.SinceDay = day - 20;
        g.MomThere = true;
        g.Places = 1;
        g.WorkHours = 10f;
        g.Stress = 30f;
        g.SleepDebt = 2f;
        g.Skipped = 0;
        g.Diet = 60f;
        g.Tidy = 70f;
        g.ContactDay = day;
        g.OutingDay = day - 3;
        g.Cash = 20000f;
        g.WeekHigh = 21000f;
        g.NetKnown = true;
        g.NetWorth = 50000L;
        return g;
    }

    // A memory based on a day, with a cause that began so many days before it.
    private static HardMemory HardMem(int day, Strain lasting = Strain.None, int days = 0)
    {
        var m = new HardMemory();
        HardTimesRules.Roll(m, day);
        if (lasting != Strain.None)
        {
            m.Since[(int)lasting] = day - days;
        }
        return m;
    }

    // What comes now: the last moment ten days ago unless said (every gap allows it, the everyday towel's three weeks
    // do not).
    private static int HardPick(in HardSigns g, HardMemory m, out Strain why, int lastDay = -1, int last = -1, List<int> seen = null, int seed = HardSeed)
    {
        return HardTimesRules.Choose(g, m, seed, seen ?? new List<int>(), last, lastDay < 0 ? g.Day - 10 : lastDay, new int[HardTimesRules.Strains],
                                     new int[HardTimesRules.Strains], out why);
    }

    private static string HardSaid(int pick, Strain why)
    {
        return pick < 0 ? "none" : "moment " + (pick + 1) + " of " + why;
    }

    // ---------------------------------------------------------------- each moment's causes

    private static void HardMoments()
    {
        const int day = 100, late = 23;   // at eleven every moment's hour of the day has come

        // 1. "You said you would be home by seven. It is past ten. I ate by myself, again."
        HardSigns g = HardHome(day, late);
        HardMemory m = HardMem(day, Strain.LateHome, 2);
        HardTimesRules.Mark(ref m.Late, m.Base, day);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 1);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 3);
        int pick = HardPick(g, m, out Strain why);
        Check(pick == 0 && why == Strain.LateHome, "hard times: three evenings out after ten in a week, tonight one of them, bring \"it is past ten\" (" + HardSaid(pick, why) + ")");
        m = HardMem(day, Strain.LateHome, 2);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 1);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 2);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 3);
        pick = HardPick(g, m, out why);
        Check(pick < 0, "hard times: late evenings, but home on time tonight: not \"it is past ten\" tonight (" + HardSaid(pick, why) + ")");
        m = HardMem(day, Strain.NightsAway, 3);
        HardTimesRules.Mark(ref m.Late, m.Base, day);
        HardTimesRules.Mark(ref m.Away, m.Base, day - 1);
        HardTimesRules.Mark(ref m.Away, m.Base, day - 2);
        HardTimesRules.Mark(ref m.Away, m.Base, day - 4);
        pick = HardPick(g, m, out why);
        Check(pick == 0 && why == Strain.NightsAway, "hard times: three nights away this week and out again tonight bring \"it is past ten\" (" + HardSaid(pick, why) + ")");
        HardSigns apart = HardHome(day, late, false);
        m = HardMem(day, Strain.LateHome, 2);
        HardTimesRules.Mark(ref m.Late, m.Base, day);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 1);
        HardTimesRules.Mark(ref m.Late, m.Base, day - 2);
        pick = HardPick(apart, m, out why);
        Check(pick < 0, "hard times: two who live apart never hear \"I ate by myself, again\" (" + HardSaid(pick, why) + ")");

        // 2. "I have told you about my day three times this week and you have not heard a word of it. Where do you go?"
        apart = HardHome(day, late, false);
        apart.TalkDay = apart.ContactDay = day - 6;
        pick = HardPick(apart, HardMem(day, Strain.Unseen, 2), out why);
        Check(pick == 1 && why == Strain.Unseen, "hard times: six days without a call, a text or a visit bring \"where do you go?\" (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.Stress = 95f;
        pick = HardPick(g, HardMem(day, Strain.Worn, 1), out why);
        Check(pick == 1 && why == Strain.Worn, "hard times: stress near the top brings \"you have not heard a word of it\" before Mom's call or the towel (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.SleepDebt = 15f;
        pick = HardPick(g, HardMem(day, Strain.Worn, 1), out why);
        Check(pick == 1 && why == Strain.Worn, "hard times: fifteen hours of sleep owed bring \"where do you go?\" (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.WorkHours = 26f;
        pick = HardPick(g, HardMem(day, Strain.LongHours, 1), out why);
        Check(pick == 1 && why == Strain.LongHours, "hard times: thirteen hours a day at work bring \"where do you go?\" (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.Burnout = 900;
        pick = HardPick(g, HardMem(day, Strain.LongHours, 1), out why);
        Check(pick == 1 && why == Strain.LongHours, "hard times: a professional job's burnout of 900 counts as long hours (" + HardSaid(pick, why) + ")");

        // 3. "You forgot. You actually forgot what today was."
        g = HardHome(day, late);
        g.BirthdayToday = true;
        g.TalkDay = g.ContactDay = day - 1;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 2 && why == Strain.Forgot, "hard times: the partner's birthday with no word all day brings \"you forgot what today was\" in the evening (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.SinceDay = day - g.YearDays;
        g.TalkDay = g.ContactDay = day - 1;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 2 && why == Strain.Forgot, "hard times: the anniversary (a year of the game's together) with no word brings \"you forgot\" (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.LetDownDay = day - 1;
        g.LetDowns = 1;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 2 && why == Strain.LetDown, "hard times: a date or an appointment missed yesterday, not made up, brings \"you forgot\" (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.StoodUpDay = day - 2;
        g.SharedDay = day - 5;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 2 && why == Strain.LetDown, "hard times: an outing of the social life the player stood them up for brings \"you forgot\" (" + HardSaid(pick, why) + ")");

        // 4. "We agreed to talk about big purchases first. And then I see the bank statement."
        g = HardHome(day, late);
        g.Cash = 4000f;
        g.WeekHigh = 10000f;
        g.BigBuy = 6000f;
        g.BigBuyBefore = 10000f;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 3 && why == Strain.Spend, "hard times: six tenths of the money on one purchase bring \"I see the bank statement\" (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.Cash = -500f;
        g.WeekHigh = 0f;
        g.RedMidnights = 2;
        pick = HardPick(g, HardMem(day, Strain.Money, 2), out why);
        Check(pick == 3 && why == Strain.Money, "hard times: two midnights in the red and still below nothing bring the statement (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.NetWorth = -20000L;
        pick = HardPick(g, HardMem(day, Strain.Money, 2), out why);
        Check(pick == 3 && why == Strain.Money, "hard times: owing more than is owned brings the statement (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.Borrowed = 8000f;
        pick = HardPick(g, HardMem(day, Strain.Money, 3), out why);
        Check(pick == 3 && why == Strain.Money, "hard times: a loan of 8,000 taken this week brings the statement (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.WeekHigh = 20000f;
        g.Cash = 5000f;
        pick = HardPick(g, HardMem(day, Strain.Loss, 3), out why);
        Check(pick == 3 && why == Strain.Loss, "hard times: three quarters of the week's money gone, no purchase to show for it, bring the statement (" + HardSaid(pick, why) + ")");
        apart = HardHome(day, late, false);
        apart.Cash = 4000f;
        apart.WeekHigh = 10000f;
        apart.BigBuy = 6000f;
        apart.BigBuyBefore = 10000f;
        pick = HardPick(apart, HardMem(day), out why);
        Check(pick < 0, "hard times: two who neither live together nor are engaged share no statement (" + HardSaid(pick, why) + ")");
        apart.Committed = true;
        pick = HardPick(apart, HardMem(day), out why);
        Check(pick == 3, "hard times: engaged and living apart, a big purchase still reaches the statement (" + HardSaid(pick, why) + ")");

        // 5. "Your mother rang me to ask if I was feeding you. I am not your housekeeper."
        g = HardHome(day, late);
        g.Skipped = 5;
        pick = HardPick(g, HardMem(day, Strain.Skipping, 2), out why);
        Check(pick == 4 && why == Strain.Skipping, "hard times: five meals skipped in three days bring Mom's call (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.Diet = 20f;
        pick = HardPick(g, HardMem(day, Strain.Skipping, 3), out why);
        Check(pick == 4 && why == Strain.Skipping, "hard times: a poor diet brings Mom's call (" + HardSaid(pick, why) + ")");
        g.MomThere = false;
        pick = HardPick(g, HardMem(day, Strain.Skipping, 3), out why);
        Check(pick < 0, "hard times: without Mom in the phone, or relatives off, nobody rings about the meals (" + HardSaid(pick, why) + ")");

        // 6. "There is a wet towel on the bed. There is always a wet towel on the bed."
        g = HardHome(day, late);
        g.Tidy = 8f;
        pick = HardPick(g, HardMem(day, Strain.Mess, 2), out why);
        Check(pick == 5 && why == Strain.Mess, "hard times: a home that is a tip brings the towel (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        g.ChildDay = day - 3;
        pick = HardPick(g, HardMem(day, Strain.NewChild, 2), out why);
        Check(pick == 5 && why == Strain.NewChild, "hard times: a child three days old brings the towel (" + HardSaid(pick, why) + ")");

        // 7. "You always say sorry. You say it so quickly."
        g = HardHome(day, late);
        g.WorkHours = 26f;
        m = HardMem(day, Strain.LongHours, 2);
        m.Sorry = 2;
        m.SorryDay = day - 3;
        pick = HardPick(g, m, out why);
        Check(pick == 6 && why == Strain.Sorry, "hard times: two quick sorries in a row and the long hours still there bring \"you always say sorry\" first (" + HardSaid(pick, why) + ")");
        g = HardHome(day, late);
        m = HardMem(day);
        m.Sorry = 3;
        m.SorryDay = day - 1;
        pick = HardPick(g, m, out why, day - 1);
        Check(pick < 0, "hard times: quick sorries, but nothing wrong now: nothing to be upset about (" + HardSaid(pick, why) + ")");

        // 8. "I feel like the shop gets the best of you and I get what is left over."
        g = HardHome(day, late);
        m = HardMem(day, Strain.Business, 2);
        foreach (int d in new[] { day - 1, day - 2, day - 4 })
        {
            HardTimesRules.Mark(ref m.Late, m.Base, d);
            HardTimesRules.Mark(ref m.LateWork, m.Base, d);
        }
        pick = HardPick(g, m, out why);
        Check(pick == 7 && why == Strain.Business, "hard times: three evenings at the player's own business this week bring \"the shop gets the best of you\" (" + HardSaid(pick, why) + ")");
        m = HardMem(day, Strain.Business, 2);
        foreach (int d in new[] { day - 1, day - 2, day - 3, day - 5 })
        {
            HardTimesRules.Mark(ref m.Away, m.Base, d);
            HardTimesRules.Mark(ref m.AwayWork, m.Base, d);
        }
        pick = HardPick(g, m, out why);
        Check(pick == 7 && why == Strain.Business, "hard times: living at the business (four nights there this week) brings \"the shop gets the best of you\" (" + HardSaid(pick, why) + ")");
        int shop = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            HardSigns owner = HardHome(day, late, false);
            owner.OwnsBusiness = true;
            owner.TalkDay = owner.ContactDay = day - 6;
            if (HardPick(owner, HardMem(day, Strain.Unseen, 2), out _, 0, 1, null, seed) == 7)
            {
                shop++;
            }
        }
        Check(shop > 0 && shop < 200, "hard times: a business owner who lives apart and is never seen hears \"the shop gets the best of you\" now and then in place of \"where do you go?\" (" + shop + " of 200)");

        // 9. "We were supposed to leave half an hour ago. They are all waiting."
        g = HardHome(day, 18);
        g.OutingLate = 60;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 8 && why == Strain.RunningLate, "hard times: an hour late for a child's play or dinner at Mom's brings \"they are all waiting\" at once (" + HardSaid(pick, why) + ")");
        g.OutingLate = 0;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick < 0, "hard times: on time for the outing: nobody is waiting (" + HardSaid(pick, why) + ")");

        // 10. "I found the plan for the new place. Another one. When were you going to tell me?"
        g = HardHome(day, late);
        g.PlaceDay = day - 1;
        g.Places = 2;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 9 && why == Strain.NewPlace, "hard times: a new place taken yesterday besides the home brings \"another one\" (" + HardSaid(pick, why) + ")");
        g.Places = 1;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick < 0, "hard times: a first place of their own is no \"another one\" (" + HardSaid(pick, why) + ")");
        g.MovedDay = day - 2;
        pick = HardPick(g, HardMem(day), out why);
        Check(pick == 9 && why == Strain.NewPlace, "hard times: the home moved two days ago, one place still, brings \"the plan for the new place\" (" + HardSaid(pick, why) + ")");

        // Every cause the watch reads leads to some moment: none is read for nothing.
        string idle = null;
        for (int i = 1; i < HardTimesRules.Strains; i++)
        {
            var raw = new int[HardTimesRules.Strains];
            var usable = new int[HardTimesRules.Strains];
            usable[i] = raw[i] = 100;
            usable[(int)Strain.LongHours] = Math.Max(usable[(int)Strain.LongHours], i == (int)Strain.Sorry ? 60 : 0);   // a sorry is always for something
            HardSigns all = HardHome(day, late);
            all.OwnsBusiness = true;
            var lateTonight = HardMem(day);
            HardTimesRules.Mark(ref lateTonight.Late, lateTonight.Base, day);
            bool fits = false;
            for (int k = 0; k < HardTimesRules.Moments; k++)
            {
                fits |= HardTimesRules.Fit(k, all, lateTonight, raw, usable, out Strain cause) >= HardTimesRules.MinFit && cause == (Strain)i;
            }
            if (!fits)
            {
                idle = (idle == null ? string.Empty : idle + ", ") + (Strain)i;
            }
        }
        Check(idle == null, "hard times: every cause leads to at least one moment whose words are about it" + (idle != null ? " (not: " + idle + ")" : string.Empty));
    }

    // ---------------------------------------------------------------- when: the hour, the gap, how long a cause has
    // lasted, the rest

    private static void HardWhen()
    {
        const int day = 200;
        // The hour: before a moment's hour of the day it waits, from it it comes; nobody is woken for one.
        string early = null;
        for (int k = 0; k < HardTimesRules.Moments; k++)
        {
            int hour = HardTimesRules.HourOf(k, day, HardSeed);
            if (hour < HardTimesRules.DayFrom || hour > 23)
            {
                early = (early == null ? string.Empty : early + ", ") + (k + 1);
            }
        }
        Check(early == null, "hard times: every moment's hour of the day is between eight and eleven at night" + (early != null ? " (not: " + early + ")" : string.Empty));
        HardSigns g = HardHome(day, 0);
        g.Tidy = 8f;
        int due = HardTimesRules.HourOf(HardTimesRules.Towel, day, HardSeed);
        int before = -1, at = -1;
        for (int hour = 0; hour < 24; hour++)
        {
            g.Hour = hour;
            int pick = HardPick(g, HardMem(day, Strain.Mess, 3), out _);
            if (pick == HardTimesRules.Towel && at < 0)
            {
                at = hour;
            }
            else if (pick >= 0 && at < 0)
            {
                before = hour;
            }
        }
        Check(at == due && before < 0 && at >= HardTimesRules.DayFrom, "hard times: a moment comes at its own hour of the day (" + due + ":00), not before (" + at + ")");
        g = HardHome(day, 1);
        g.OutingLate = 120;
        Check(HardPick(g, HardMem(day), out _) < 0, "hard times: no moment in the small hours, however late anybody is");

        // The gap: two days at the least; a mild cause waits longer in a warm home; a strong one only the two days.
        g = HardHome(day, 23);
        g.Tidy = 20f;   // a messy home: 50
        Check(HardPick(g, HardMem(day, Strain.Mess, 4), out _, day - 1) < 0, "hard times: never a day after the last moment");
        g.Harmony = 75f;
        Check(HardPick(g, HardMem(day, Strain.Mess, 4), out _, day - 3) < 0 && HardPick(g, HardMem(day, Strain.Mess, 4), out _, day - 5) == HardTimesRules.Towel,
            "hard times: in a warm home a mild cause waits five days after the last moment");
        g.Harmony = 30f;
        Check(HardPick(g, HardMem(day, Strain.Mess, 4), out _, day - 2) == HardTimesRules.Towel, "hard times: in a tense home two days are enough");
        g = HardHome(day, 18);
        g.Harmony = 90f;
        g.OutingLate = 60;
        Check(HardPick(g, HardMem(day), out _, day - 2) == 8 && HardPick(g, HardMem(day), out _, day - 1) < 0,
            "hard times: a strong cause (late for an outing, 90) needs only the two days, however warm the home");

        // How long a lasting cause has lasted: a mild one two or three days, a middling one one or two, a strong one
        // none or one; a tense home a day sooner, a warm one a day later.
        int mild = HardLasted(40, 60f), middling = HardLasted(60, 60f), strong = HardLasted(90, 60f);
        Check(mild >= 2 && mild <= 3 && middling >= 1 && middling <= 2 && strong >= 0 && strong <= 1 && mild > strong,
            "hard times: a mild cause comes to a moment after two or three days, a middling one after one or two, a strong one at once or a day later (" + mild + ", " + middling + ", " + strong + ")");
        Check(HardLasted(40, 30f) == mild - 1 && HardLasted(40, 75f) == mild + 1 && HardLasted(90, 30f) == Math.Max(0, strong - 1),
            "hard times: a tense home comes to it a day sooner, a warm one a day later");
        HardSigns sudden = HardHome(day, 23);
        Check(HardTimesRules.Ripe(Strain.Spend, 45, sudden, HardMem(day), HardSeed) && HardTimesRules.Ripe(Strain.RunningLate, 70, sudden, HardMem(day), HardSeed),
            "hard times: an event (a purchase, an outing kept waiting) can come to a moment the day it happens");
        HardMemory m = HardMem(day);
        HardSigns started = HardHome(day, 23);
        started.Tidy = 20f;
        HardPick(started, m, out _);
        Check(m.Since[(int)Strain.Mess] == day, "hard times: the day a cause begins is noted when it is first weighed");
        started.Tidy = 70f;
        HardPick(started, m, out _);
        Check(m.Since[(int)Strain.Mess] == 0, "hard times: a cause that has gone is forgotten, so coming back it starts again");

        // The rest: a week (to nine days) after a moment came of a cause, that cause brings no other.
        g = HardHome(day, 23);
        g.WorkHours = 26f;
        int quiet = 0;
        for (int d = 1; d <= 12; d++)
        {
            HardSigns later = g;
            later.Day = day + d;
            later.TalkDay = later.ContactDay = later.Day;
            HardMemory rested = HardMem(day + d, Strain.LongHours, d + 2);
            HardTimesRules.Spoke(rested, Strain.LongHours, HardFull(Strain.LongHours), day);
            if (HardPick(later, rested, out _, day) < 0)
            {
                quiet = d;
            }
        }
        Check(quiet >= HardTimesRules.RestDays - 1 && quiet <= HardTimesRules.RestDays + 1, "hard times: the long hours bring no other moment for a week to nine days after one (quiet to day " + quiet + ")");

        // A moment airs everything wrong then: a cause that was there too rests as long; something new still comes up.
        HardSigns two = HardHome(day, 23);
        two.WorkHours = 26f;   // long hours, the moment's own cause
        two.Tidy = 20f;        // a messy home besides
        var weighed = new int[HardTimesRules.Strains];
        HardMemory aired = HardMem(day, Strain.LongHours, 3);
        aired.Since[(int)Strain.Mess] = day - 3;
        HardTimesRules.Choose(two, aired, HardSeed, new List<int>(), -1, day - 10, weighed, new int[HardTimesRules.Strains], out Strain first);
        HardTimesRules.Spoke(aired, first, weighed, day);
        HardSigns after = two;
        after.Day = day + 4;
        after.TalkDay = after.ContactDay = after.Day;
        HardSigns news = after;
        news.BigBuy = 6000f;
        news.BigBuyBefore = 9000f;
        news.Cash = 3000f;
        news.WeekHigh = 9000f;
        Check(aired.Spoke[(int)Strain.LongHours] == day && aired.Spoke[(int)Strain.Mess] == day && HardPick(after, HardMemCopy(aired), out _, day) < 0
              && HardPick(news, HardMemCopy(aired), out Strain fresh, day) == 3 && fresh == Strain.Spend,
            "hard times: a moment airs everything wrong then (the long hours and the mess both rest), and something new still comes up four days on");
        int back = -1;
        for (int d = 5; d <= 12 && back < 0; d++)
        {
            HardSigns later = two;
            later.Day = day + d;
            later.TalkDay = later.ContactDay = later.Day;
            if (HardPick(later, HardMemCopy(aired), out _, day) >= 0)
            {
                back = d;
            }
        }
        Check(back >= HardTimesRules.RestDays && back <= HardTimesRules.RestDays + 2, "hard times: the troubles aired together come back together a week to nine days on (" + back + ")");

        // Never the same one twice running while another fits about as well; one had lately after one that has not.
        g = HardHome(day, 23);
        g.Tidy = 5f;
        g.Skipped = 5;
        HardMemory both = HardMem(day, Strain.Mess, 3);
        both.Since[(int)Strain.Skipping] = day - 3;
        int towelLast = HardPick(g, both, out _, day - 6, HardTimesRules.Towel);
        int momLast = HardPick(g, HardMemCopy(both), out _, day - 6, 4);
        Check(towelLast == 4 && momLast == HardTimesRules.Towel, "hard times: of two that fit as well, the one not had last comes (" + towelLast + ", " + momLast + ")");
    }

    // Days a lasting cause (the mess) of a level must have lasted, from a fixed first day, before it is ripe in a home
    // of this harmony.
    private static int HardLasted(int level, float harmony)
    {
        const int since = 250;
        HardMemory m = HardMem(since);
        m.Since[(int)Strain.Mess] = since;
        for (int d = 0; d <= 6; d++)
        {
            HardSigns g = HardHome(since + d, 23);
            g.Harmony = harmony;
            if (HardTimesRules.Ripe(Strain.Mess, level, g, m, HardSeed))
            {
                return d;
            }
        }
        return 99;
    }

    private static HardMemory HardMemCopy(HardMemory m)
    {
        return HardMemory.Read(m.Write());
    }

    // ---------------------------------------------------------------- nothing when all is well

    private static void HardCalm()
    {
        // A year of days with nothing wrong, every hour of each: only the towel, now and then, three weeks apart at the
        // least.
        List<string> together = HardRun(360, HardSeed, (day, hour, m) => HardHome(day, hour));
        bool towels = together.Count > 0;
        int lastDay = -100;
        bool apart = true, waking = true;
        foreach (string came in together)
        {
            string[] f = came.Split(':');
            int day = int.Parse(f[0], CultureInfo.InvariantCulture), hour = int.Parse(f[1], CultureInfo.InvariantCulture);
            towels &= f[2] == HardTimesRules.Towel.ToString(CultureInfo.InvariantCulture) && f[3] == Strain.None.ToString();
            apart &= day - lastDay >= HardTimesRules.EverydayGapDays;
            waking &= hour >= HardTimesRules.DayFrom;
            lastDay = day;
        }
        Check(towels && apart && waking && together.Count <= 360 / HardTimesRules.EverydayGapDays,
            "hard times: a year with nothing wrong brings only the towel, " + together.Count + " times, three weeks apart at the least, in the day");
        List<string> livingApart = HardRun(360, HardSeed, (day, hour, m) => HardHome(day, hour, false));
        Check(livingApart.Count == 0, "hard times: a year with nothing wrong for two who live apart brings no moment at all (" + livingApart.Count + ")");
        List<string> unknown = HardRun(360, HardSeed, (day, hour, m) =>
        {
            HardSigns g = HardSigns.Unknown(day, hour);
            g.LivesIn = true;
            g.Harmony = 20f;   // tense, but with nothing found wrong
            g.TalkDay = day;
            return g;
        });
        bool onlyTowel = true;
        foreach (string came in unknown)
        {
            onlyTowel &= came.EndsWith(":" + HardTimesRules.Towel + ":" + Strain.None, StringComparison.Ordinal);
        }
        Check(onlyTowel, "hard times: with nothing known of the other features, no moment comes of what is not known (" + unknown.Count + " towels)");
        int many = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            many += HardRun(400, seed, (day, hour, m) => HardHome(day, hour)).Count;
        }
        Check(many >= 40 * 3 && many <= 40 * 16, "hard times: the towel on days with nothing wrong is rare: about one day in forty, three weeks apart (" + many + " in 40 runs of 400 days)");
    }

    // A run of days at every hour, the signs a script gives, a moment answered as it comes: what came, as
    // "day:hour:moment:cause".
    private static List<string> HardRun(int days, int seed, Func<int, int, HardMemory, HardSigns> signs, Action<int, int, HardMemory> each = null)
    {
        var m = new HardMemory();
        var seen = new List<int>();
        var raw = new int[HardTimesRules.Strains];
        var usable = new int[HardTimesRules.Strains];
        var came = new List<string>();
        int last = -1, lastDay = 0;
        for (int day = 1; day <= days; day++)
        {
            HardTimesRules.Roll(m, day);
            for (int hour = 0; hour < 24; hour++)
            {
                each?.Invoke(day, hour, m);
                HardSigns g = signs(day, hour, m);
                int pick = HardTimesRules.Choose(g, m, seed, seen, last, lastDay, raw, usable, out Strain why);
                if (pick < 0)
                {
                    continue;
                }
                HardTimesRules.Spoke(m, why, raw, day);
                if (seen.Count >= HardTimesRules.Moments)
                {
                    seen.Clear();
                }
                seen.Add(pick);
                last = pick;
                lastDay = day;
                came.Add(day.ToString(CultureInfo.InvariantCulture) + ":" + hour.ToString(CultureInfo.InvariantCulture) + ":" + pick.ToString(CultureInfo.InvariantCulture) + ":" + why);
            }
        }
        return came;
    }

    // ---------------------------------------------------------------- a cause gone

    private static void HardGone()
    {
        const int day = 300;
        HardSigns g = HardHome(day, 12);
        g.Cash = -500f;
        g.WeekHigh = 0f;
        g.RedMidnights = 2;
        HardMemory m = HardMem(day, Strain.Money, 2);
        int due = HardTimesRules.HourOf(3, day, HardSeed);
        Check(HardPick(g, m, out _) < 0, "hard times: in the red at noon, the statement waits for its hour (" + due + ":00)");
        g.Hour = due;
        HardSigns red = g;
        Check(HardPick(red, HardMemCopy(m), out Strain why) == 3 && why == Strain.Money, "hard times: still in the red at its hour: the statement comes");
        g.Cash = 3000f;
        Check(HardPick(g, HardMemCopy(m), out _) < 0, "hard times: the money back above nothing by its hour: no moment");
        g = HardHome(day, 23);
        g.NetWorth = -100L;
        Check(HardPick(g, HardMem(day, Strain.Money, 3), out _) == 3, "hard times: owing a little more than is owned is money trouble");
        g.NetWorth = 100L;
        Check(HardPick(g, HardMem(day, Strain.Money, 3), out _) < 0, "hard times: worth a little more than is owed again: no moment");
        g = HardHome(day, 23);
        g.WeekHigh = 20000f;
        g.Cash = 5000f;
        HardSigns loss = g;
        loss.Cash = 15000f;
        Check(HardPick(g, HardMem(day, Strain.Loss, 3), out _) == 3 && HardPick(loss, HardMem(day, Strain.Loss, 3), out _) < 0,
            "hard times: the money that came back after a loss ends it");

        g = HardHome(day, 23);
        g.LetDownDay = day - 1;
        g.LetDowns = 1;
        HardSigns madeUp = g;
        madeUp.SharedDay = day;
        Check(HardPick(g, HardMem(day), out _) == 2 && HardPick(madeUp, HardMem(day), out _) < 0, "hard times: a let-down made up with time together since: no moment");
        madeUp = g;
        madeUp.LetDownDay = day - HardTimesRules.EventDays - 1;
        Check(HardPick(madeUp, HardMem(day), out _) < 0, "hard times: a let-down of a week ago is over");
        g = HardHome(day, 23);
        g.StoodUpDay = day - 2;
        g.SharedDay = day - 5;
        Check(HardPick(g, HardMem(day), out _) == 2, "hard times: stood up two days ago, nothing together since: \"you forgot\"");
        madeUp = g;
        madeUp.OutingDay = day - 1;
        Check(HardPick(madeUp, HardMem(day), out _) < 0, "hard times: an outing together after the stand-up makes up for it");

        HardSigns apart = HardHome(day, 23, false);
        apart.TalkDay = apart.ContactDay = day - 6;
        HardSigns called = apart;
        called.ContactDay = day;
        Check(HardPick(apart, HardMem(day, Strain.Unseen, 2), out _) == 1 && HardPick(called, HardMem(day, Strain.Unseen, 2), out _) < 0,
            "hard times: a call of the social life today ends \"where do you go?\"");
        called = apart;
        called.TalkDay = day;
        called.ContactDay = -1;   // the social life off: family life's own word counts
        Check(HardPick(called, HardMem(day, Strain.Unseen, 2), out _) < 0, "hard times: with the social life off, a word through the Family contact ends it");

        g = HardHome(day, 23);
        g.BirthdayToday = true;
        g.TalkDay = g.ContactDay = day - 1;
        HardSigns wished = g;
        wished.BirthdayWished = true;
        HardSigns rang = g;
        rang.ContactDay = day;
        Check(HardPick(g, HardMem(day), out _) == 2 && HardPick(wished, HardMem(day), out _) < 0 && HardPick(rang, HardMem(day), out _) < 0,
            "hard times: a birthday wished, or a call that day, is not forgotten");
        HardSigns morning = g;
        morning.Hour = HardTimesRules.ForgotFrom - 1;
        Check(HardTimesRules.Level(Strain.Forgot, morning, HardMem(day)) == 0, "hard times: a birthday is not forgotten before the evening");

        m = HardMem(day, Strain.LateHome, 2);
        foreach (int d in new[] { day, day - 1, day - 2 })
        {
            HardTimesRules.Mark(ref m.Late, m.Base, d);
        }
        g = HardHome(day, 23);
        Check(HardPick(g, HardMemCopy(m), out _) == 0, "hard times: the late evenings bring \"it is past ten\"");
        HardTimesRules.Roll(m, day + HardTimesRules.Week);
        g = HardHome(day + HardTimesRules.Week, 23);
        HardTimesRules.Mark(ref m.Late, m.Base, day + HardTimesRules.Week);
        Check(HardTimesRules.Level(Strain.LateHome, g, m) == 0 && HardPick(g, m, out _) < 0, "hard times: a week home on time, and one late evening is no pattern: no moment");

        g = HardHome(day, 23);
        g.PlaceDay = day - HardTimesRules.EventDays - 1;
        g.Places = 3;
        Check(HardPick(g, HardMem(day), out _) < 0, "hard times: a new place of a week ago is no news");
        g = HardHome(day, 23);
        g.ChildDay = day - HardTimesRules.ChildDays - 1;
        Check(HardPick(g, HardMem(day, Strain.NewChild, 20), out _) < 0, "hard times: a child three weeks old is no longer new");
    }

    // ---------------------------------------------------------------- the options and presets

    private static void HardOptions()
    {
        string watch = ModSource(@"Family\HardTimesWatch.cs");
        string hard = ModSource(@"Family\HardTimes.cs");
        string calendar = ModSource(@"Family\FamilyCalendar.cs");
        string links = ModSource(@"Wiring\Wiring.HardTimesLinks.cs");
        string hourly = MemberText(watch, "public static void Hourly(int day, int hour)");
        Check(hourly.Contains("if (!FamilyState.Together || !Settings.FamilyFriction || director == null)") && hourly.IndexOf("return;", StringComparison.Ordinal) > 0
              && hourly.IndexOf("Settings.FamilyFriction", StringComparison.Ordinal) < hourly.IndexOf("HardTimesRules.Choose", StringComparison.Ordinal),
            "hard times: with \"Arguments, time apart and divorce\" off (the Relaxed preset) nothing is read and no moment comes");
        string daily = MemberText(hard, "public static void Daily(int day)");
        int friction = daily.IndexOf("!Settings.FamilyFriction", StringComparison.Ordinal);
        int separate = daily.IndexOf("Separate(true);", StringComparison.Ordinal);
        int custody = daily.IndexOf("FamilyBridges.Law.InCustody", StringComparison.Ordinal);
        int watched = daily.IndexOf("HardTimesWatch.Daily(day);", StringComparison.Ordinal);
        Check(friction > 0 && separate > friction && custody > separate && watched > custody && !daily.Contains("Random") && !daily.Contains("chance"),
            "hard times: the day keeps its order: hard times off, then a low home moving out, then the cells, and only then what is going on; no chance is rolled");
        string hourlyCalendar = MemberText(calendar, "public static void Hourly(int day, int hour)");
        int held = hourlyCalendar.IndexOf("if (held)", StringComparison.Ordinal);
        Check(held > 0 && hourlyCalendar.IndexOf("HardTimesWatch.Hourly(day, hour);", StringComparison.Ordinal) > held,
            "hard times: nobody argues through the bars: the hour's moment is weighed only after the cells' return");
        string life = ModSource(@"Meta\FeatureList.Life.cs");
        Check(life.Contains(".Preset(Settings.FamilyFrictionId, 0, 1, 1)"), "hard times: the presets still set hard times off for Relaxed and on for Realistic and Hardcore (Standard is its default, on)");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains(".AddToggle(Settings.FamilyFrictionId, \"reallife_family_friction\", Settings.FamilyFriction,")
              && mod.Contains(".AddButton(\"reallife_family_test_argue\", () => Family.FamilyDirector.Instance?.TestArgue())"),
            "hard times: the switch is on the options page as before, and the test of a moment is on the Tests page");
        string test = MemberText(ModSource(@"Family\FamilyDirector.HardTimes.cs"), "public void TestArgue()");
        Check(test.Contains("if (!Settings.FamilyFriction)") && test.Contains("TestRefusal.FeatureOff") && test.Contains("\"reallife_family_no_argue_cause\""),
            "hard times: the test says hard times are off when they are, and that nothing is wrong when nothing is");
        string signs = MemberText(watch, "private static HardSigns Signs(int day, int hour, HardMemory m)");
        Check(signs.Contains("g.ChildDay = Settings.FamilyChildren ? NewestChild(s) : 0;") && signs.Contains("g.MomThere = Settings.FamilyRelatives && PhoneContacts.Mom != null;"),
            "hard times: children off, no new child to come of; relatives off, no call from Mom");
        string home = MemberText(links, "private static void Home(ref HardSigns signs)");
        Check(home.Contains("if (!HomeState.Loaded || !Settings.HomeLife)") && Regex.IsMatch(home, @"if \(Settings\.HomeNeeds\)\s*\{\s*signs\.Stress = HomeState\.Stress;\s*signs\.Tidy")
              && Regex.IsMatch(home, @"if \(Settings\.HomeSleep\)\s*\{\s*signs\.SleepDebt") && Regex.IsMatch(home, @"if \(Settings\.HomeFitness\)\s*\{"),
            "hard times: home life's figures are read only while their part is on (needs: stress and the home; sleep; fitness: the meals)");
        Check(MemberText(links, "private static void Social(ref HardSigns signs, string partner)").Contains("SocialState.Loaded && Settings.Social ? SocialState.Partner : null")
              && MemberText(links, "private static void Social(ref HardSigns signs, string partner)").Contains("p.Name != partner"),
            "hard times: the social life's figures only while it is on, and only about the partner family life has");

        // Each part switched off leaves its causes unknown, and unknown brings nothing.
        const int day = 400;
        HardSigns off = HardHome(day, 23);
        off.WorkHours = off.Stress = off.SleepDebt = off.Diet = off.Tidy = -1f;
        off.Skipped = off.Burnout = -1;
        bool none = true;
        foreach (Strain s in new[] { Strain.LongHours, Strain.Worn, Strain.Skipping, Strain.Mess })
        {
            none &= HardTimesRules.Level(s, off, HardMem(day)) == 0;
        }
        Check(none, "hard times: with home life off, no long hours, stress, sleep, meals or mess");
        HardSigns noMom = HardHome(day, 23);
        noMom.Skipped = 6;
        noMom.MomThere = false;
        Check(HardTimesRules.Fit(4, noMom, HardMem(day), HardFull(Strain.Skipping), HardFull(Strain.Skipping), out _) == 0, "hard times: relatives off: Mom's call never comes");
        HardSigns noSocial = HardHome(day, 23, false);
        noSocial.ContactDay = -1;
        noSocial.TalkDay = day - 6;
        Check(HardTimesRules.Level(Strain.Unseen, noSocial, HardMem(day)) > 0, "hard times: with the social life off, family life's own last word measures the quiet");
        noSocial.BirthdayToday = false;
        noSocial.Hour = 20;
        Check(HardTimesRules.Level(Strain.Forgot, noSocial, HardMem(day)) == 0, "hard times: with the social life off, no birthday to forget (the anniversary still counts)");
    }

    private static int[] HardFull(Strain s)
    {
        var levels = new int[HardTimesRules.Strains];
        levels[(int)s] = 100;
        return levels;
    }

    // ---------------------------------------------------------------- the save's line

    private static void HardSaveLine()
    {
        var m = new HardMemory
        {
            Base = 412, Late = 0x55, LateWork = 0x5, Away = 0x2A, AwayWork = 0x2, Places = 4, PlaceDay = 410, HomeKey = HardTimesRules.Key("5th Avenue", 12),
            Promises = 7, PromiseDay = 409, PromiseRun = 2, Sorry = 2, SorryDay = 405, MovedDay = 402
        };
        for (int i = 0; i < HardTimesRules.Strains; i++)
        {
            m.Since[i] = i == 0 ? 0 : 400 + i;
            m.Spoke[i] = i % 3 == 0 ? 0 : 380 + i;
        }
        string line = m.Write();
        HardMemory back = HardMemory.Read(line);
        Check(HardSame(m, back) && back.Write() == line, "hard times: the save's line gives back every field as it was written (" + line.Length + " characters)");
        Check(HardFresh(HardMemory.Read(null)) && HardFresh(HardMemory.Read(string.Empty)), "hard times: a save from before the field (none, or empty) reads as nothing remembered");
        Check(HardFresh(HardMemory.Read("x,y,z")) && HardFresh(HardMemory.Read("1,2,abc")) && HardFresh(HardMemory.Read("0,5,5")) && HardFresh(HardMemory.Read("-3")),
            "hard times: a line that cannot be read, or of no version, reads as nothing remembered");
        HardMemory older = HardMemory.Read("1,412,85");
        Check(older.Base == 412 && older.Late == 85 && older.Away == 0 && older.Places == -1 && older.Promises == -1 && older.Since[3] == 0,
            "hard times: an earlier, shorter form of the line gives what it has, the rest as nothing");
        string later = "2,412,85,5,42,2,20";
        for (int i = 0; i < 20; i++)
        {
            later += "," + (i < HardTimesRules.Strains ? m.Since[i] : 999);
        }
        later += ",20";
        for (int i = 0; i < 20; i++)
        {
            later += "," + (i < HardTimesRules.Strains ? m.Spoke[i] : 888);
        }
        later += ",4,410," + m.HomeKey + ",7,409,2,2,405,402,77,66";
        HardMemory newer = HardMemory.Read(later);
        Check(HardSame(m, newer), "hard times: a later version's line (two causes more, two fields more) gives this version all of its own");
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var negative = new HardMemory { Places = -1, Promises = -1, HomeKey = -5 };
            string swedish = negative.Write();
            HardMemory read = HardMemory.Read(swedish);
            Check(swedish.Contains(",-1,") && read.Places == -1 && read.Promises == -1 && read.HomeKey == -5, "hard times: the line is the same in every language of the machine (" + swedish.Substring(0, Math.Min(12, swedish.Length)) + "...)");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
        // Nothing remembered never brings a moment by itself: an old save's first day is a day with nothing wrong.
        HardMemory first = HardMemory.Read(null);
        HardTimesRules.Roll(first, 500);
        Check(HardPick(HardHome(500, 23), first, out _, 499) < 0, "hard times: an old save's first day with the new field brings no moment of its own");
        // The first count of places and promises in a save is taken as it is: what was before is no news.
        HardMemory counts = HardMemory.Read(null);
        HardTimesRules.NotePlaces(counts, 3, HardTimesRules.Key("Main Street", 4), 500);
        HardTimesRules.NotePromises(counts, 6, 500);
        Check(counts.PlaceDay == 0 && counts.Places == 3 && counts.PromiseDay == 0 && counts.Promises == 6,
            "hard times: an old save's places and broken promises, first counted, are no news");
        HardTimesRules.NotePlaces(counts, 4, HardTimesRules.Key("Main Street", 4), 503);
        Check(counts.PlaceDay == 503 && counts.Places == 4, "hard times: one more place is a new place that day");
        HardTimesRules.NotePlaces(counts, 4, HardTimesRules.Key("Park Lane", 9), 506);
        Check(counts.MovedDay == 506 && counts.PlaceDay == 503 && counts.HomeKey == HardTimesRules.Key("Park Lane", 9), "hard times: the home somewhere else is a move that day");
        HardTimesRules.NotePlaces(counts, 2, HardTimesRules.Key("Park Lane", 9), 507);
        Check(counts.PlaceDay == 503 && counts.MovedDay == 506 && counts.Places == 2, "hard times: a place given up is no news");
        HardTimesRules.NotePromises(counts, 7, 510);
        HardTimesRules.NotePromises(counts, 9, 512);
        Check(counts.PromiseDay == 512 && counts.PromiseRun == 3, "hard times: three broken promises in a week are a run of three");
        HardTimesRules.NotePromises(counts, 10, 530);
        Check(counts.PromiseDay == 530 && counts.PromiseRun == 1, "hard times: a broken promise weeks later starts a new run");
        HardTimesRules.Said(counts, true, 531);
        HardTimesRules.Said(counts, true, 535);
        int two = counts.Sorry;
        HardTimesRules.Said(counts, false, 536);
        HardTimesRules.Said(counts, true, 537);
        HardTimesRules.Said(counts, true, 537 + HardTimesRules.SorryDays + 1);
        Check(two == 2 && counts.Sorry == 1, "hard times: quick sorries count in a row, anything else or a fortnight between ends the run");
        string state = ModSource(@"Family\FamilyState.cs");
        Check(Regex.IsMatch(state, @"public string Hard;\s+//"), "hard times: the family's save has the line as an optional field, null in a save from before it");
    }

    private static bool HardFresh(HardMemory m)
    {
        bool fresh = m.Base == 0 && m.Late == 0 && m.LateWork == 0 && m.Away == 0 && m.AwayWork == 0 && m.Places == -1 && m.PlaceDay == 0 && m.HomeKey == 0
                     && m.Promises == -1 && m.PromiseDay == 0 && m.PromiseRun == 0 && m.Sorry == 0 && m.SorryDay == 0 && m.MovedDay == 0;
        for (int i = 0; i < HardTimesRules.Strains; i++)
        {
            fresh &= m.Since[i] == 0 && m.Spoke[i] == 0;
        }
        return fresh;
    }

    private static bool HardSame(HardMemory a, HardMemory b)
    {
        bool same = a.Base == b.Base && a.Late == b.Late && a.LateWork == b.LateWork && a.Away == b.Away && a.AwayWork == b.AwayWork && a.Places == b.Places
                    && a.PlaceDay == b.PlaceDay && a.HomeKey == b.HomeKey && a.Promises == b.Promises && a.PromiseDay == b.PromiseDay && a.PromiseRun == b.PromiseRun
                    && a.Sorry == b.Sorry && a.SorryDay == b.SorryDay && a.MovedDay == b.MovedDay;
        for (int i = 0; i < HardTimesRules.Strains; i++)
        {
            same &= a.Since[i] == b.Since[i] && a.Spoke[i] == b.Spoke[i];
        }
        return same;
    }

    // ---------------------------------------------------------------- the same state, the same moment; and variety

    private static void HardSameState()
    {
        // A life of 140 days: calm, then two weeks of long hours, a big purchase, a fortnight of late evenings, a
        // stand-up, a move.
        Func<int, int, HardMemory, HardSigns> script = (day, hour, m) =>
        {
            HardSigns g = HardHome(day, hour);
            if (day >= 31 && day <= 44)
            {
                g.WorkHours = 26f;
            }
            if (day >= 50 && day <= 52)
            {
                g.BigBuy = 7000f;
                g.BigBuyBefore = 10000f;
                g.Cash = 3000f;
                g.WeekHigh = 10000f;
            }
            if (day == 81)
            {
                g.LetDownDay = 81;
                g.LetDowns = 1;
            }
            if (day >= 100 && day <= 102)
            {
                g.PlaceDay = 100;
                g.Places = 2;
            }
            return g;
        };
        Action<int, int, HardMemory> evenings = (day, hour, m) =>
        {
            if (hour == HardTimesRules.LateHour && day >= 60 && day <= 73)
            {
                HardTimesRules.Mark(ref m.Late, m.Base, day);
            }
        };
        List<string> one = HardRun(140, HardSeed, script, evenings);
        List<string> two = HardRun(140, HardSeed, script, evenings);
        Check(string.Join(" ", one) == string.Join(" ", two) && one.Count > 0, "hard times: the same life and the same partner give the same moments at the same hours (" + one.Count + ": " + string.Join(" ", one) + ")");
        bool caused = true;
        foreach (string came in one)
        {
            string[] f = came.Split(':');
            int day = int.Parse(f[0], CultureInfo.InvariantCulture);
            bool calm = day < 31 || (day > 47 && day < 50) || (day > 56 && day < 60) || (day > 76 && day < 81) || (day > 86 && day < 100) || day > 106;
            caused &= calm ? f[3] == Strain.None.ToString() : f[3] != Strain.None.ToString();
        }
        Check(caused, "hard times: every moment of that life came of what was going on then; in the calm days only the towel");
        Func<int, int, HardMemory, HardSigns> hard = (day, hour, m) =>
        {
            HardSigns g = HardHome(day, hour);
            g.WorkHours = 24f;
            g.Stress = 85f;
            g.SleepDebt = 10f;
            g.Tidy = 20f;
            g.OwnsBusiness = true;
            return g;
        };
        Action<int, int, HardMemory> out4 = (day, hour, m) =>
        {
            if (hour == HardTimesRules.LateHour && day % 7 < 4)
            {
                HardTimesRules.Mark(ref m.Late, m.Base, day);
                HardTimesRules.Mark(ref m.LateWork, m.Base, day);
            }
        };
        bool weekly = true;
        int fewest = int.MaxValue, most = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            List<string> run = HardRun(140, seed, hard, out4);
            fewest = Math.Min(fewest, run.Count);
            most = Math.Max(most, run.Count);
            int previous = -100;
            foreach (string came in run)
            {
                int d = int.Parse(came.Substring(0, came.IndexOf(':')), CultureInfo.InvariantCulture);
                weekly &= d - previous >= HardTimesRules.MinGapDays;
                previous = d;
            }
        }
        Check(weekly && fewest >= 12 && most <= 24, "hard times: a life with everything wrong at once (long hours, stress, sleep, a mess, four evenings a week at the shop) brings about one moment a week, never two in two days ("
                                                    + fewest + " to " + most + " in 140 days)");
        var kinds = new HashSet<string>();
        var lines = new HashSet<string>();
        for (int seed = 1; seed <= 30; seed++)
        {
            List<string> run = HardRun(140, seed, script, evenings);
            lines.Add(string.Join(" ", run));
            foreach (string came in run)
            {
                kinds.Add(came.Substring(came.IndexOf(':', came.IndexOf(':') + 1) + 1));
            }
        }
        Check(lines.Count >= 10, "hard times: other partners live the same life a little differently: which moment and when vary (" + lines.Count + " different of 30)");
        Check(kinds.Contains("1:LongHours") && kinds.Contains("3:Spend") && kinds.Contains("0:LateHome") && kinds.Contains("2:LetDown") && kinds.Contains("9:NewPlace"),
            "hard times: that life's long hours, purchase, late evenings, stand-up and move each brought their own moment (" + string.Join(", ", kinds) + ")");
        var hours = new HashSet<int>();
        bool inside = true;
        for (int seed = 1; seed <= 60; seed++)
        {
            int h = HardTimesRules.HourOf(1, 77, seed);
            hours.Add(h);
            inside &= h >= 18 && h <= 22;
        }
        Check(inside && hours.Count >= 4, "hard times: \"where do you go?\" comes at an hour of the evening that varies with the partner and the day (" + hours.Count + " hours)");
        Check(HardTimesRules.Mix(1, 2, 3) == HardTimesRules.Mix(1, 2, 3) && HardTimesRules.Mix(1, 2, 3) != HardTimesRules.Mix(1, 2, 4) && HardTimesRules.Mix(-5, 0, 9) >= 0,
            "hard times: the mixing of the seed and the day is the same each time, and never below nothing");
        Check(HardTimesRules.Key("5th Avenue", 12) == HardTimesRules.Key("5th Avenue", 12) && HardTimesRules.Key("5th Avenue", 12) != HardTimesRules.Key("5th Avenue", 14)
              && HardTimesRules.Key(null, 0) != 0, "hard times: a home's key is the same for the same address, another for another, never 0");
        HardSigns g2 = HardHome(600, 23);
        g2.WorkHours = 26f;
        bool same = true;
        for (int i = 0; i < 50; i++)
        {
            int pick = HardPick(g2, HardMem(600, Strain.LongHours, 2), out Strain why);
            same &= pick == 1 && why == Strain.LongHours;
        }
        Check(same, "hard times: weighed fifty times, the same state gives the same moment fifty times");
    }

    // ---------------------------------------------------------------- the sources

    private static void HardSources()
    {
        string hard = ModSource(@"Family\HardTimes.cs");
        string watch = ModSource(@"Family\HardTimesWatch.cs");
        string rules = ModSource(@"Family\HardTimesRules.cs");
        string game = ModSource(@"Family\HardTimesGame.cs");
        string links = ModSource(@"Wiring\Wiring.HardTimesLinks.cs");
        string wiring = ModSource(@"Wiring\Wiring.cs");
        Check(!hard.Contains("Random") && !watch.Contains("Random") && !rules.Contains("Random"),
            "hard times: no unseeded chance anywhere in when or which moment comes (Going Public: the same state, the same moment)");
        Check(hard.Contains("internal static void Argue(int day, int pick)") && !hard.Contains("Random.Range") && watch.Contains("HardTimes.Argue(day, pick);"),
            "hard times: the moment HardTimesRules chose is the one asked");
        Check(MemberText(hard, "private static void Answered(float gain, string line)").Contains("HardTimesWatch.Answered(SaidSorry(line));"),
            "hard times: each answer tells the watch whether it was a quick sorry, and does what it always did");
        Check(!Regex.IsMatch(rules, @"\busing (UnityEngine|Helpers|Entities|BAModAPI)\b") && !rules.Contains("SaveGameManager"), "hard times: the rules are game-free (and compiled here)");
        Check(game.Contains("GoingPublic.Mine(r)") && game.Contains("GoingPublic.Mine(manager.buildingRegistration)") && !watch.Contains("SaveGameManager")
              && !watch.Contains("BuildingManager") && !watch.Contains("SharedCity") && !links.Contains("SharedCity") && !game.Contains("SharedCity"),
            "hard times: the game is read in HardTimesGame alone, only the player's own buildings count, and nothing is sent to anybody");
        string signs = MemberText(watch, "private static HardSigns Signs(int day, int hour, HardMemory m)");
        Check(MemberText(watch, "public static void Hourly(int day, int hour)").Contains("if (director.HasQuestions || FamilyApi.WeddingToday)")
              && signs.IndexOf("if (g.AwayTogether)", StringComparison.Ordinal) > signs.IndexOf("life.Days(ref g, Partner.Name);", StringComparison.Ordinal)
              && signs.Contains("g.TalkDay = g.SharedDay = day;"),
            "hard times: no moment on the wedding day, and a trip the partner came along on is time together, so \"where do you go?\" never comes of it");
        Check(!watch.Contains("void Update(") && !links.Contains("void Update(") && !game.Contains("void Update(") && Regex.Matches(ModSource(@"Family\FamilyCalendar.cs"), @"HardTimesWatch\.Hourly\(").Count == 1
              && Regex.Matches(hard, @"HardTimesWatch\.Daily\(").Count == 1,
            "hard times: nothing per frame: the watch is called once a game hour (FamilyCalendar) and once a game day (HardTimes.Daily)");
        Check(wiring.Contains("Step(\"family life's difficult moments\", HardTimesLinks.Connect);") && wiring.Contains("Step(\"family life's difficult moments\", HardTimesLinks.Disconnect);"),
            "hard times: the wiring connects the door and takes it off again");
        string statement = MemberText(links, "private static void Statement(ref HardSigns signs)");
        Check(statement.Contains("CashCategory.Investments || category == CashCategory.Other") && statement.Contains("type.StartsWith(FamilyOwn, StringComparison.Ordinal)")
              && statement.Contains("category == CashCategory.Loans && Borrowing(type)"),
            "hard times: a purchase is one the economy calls an investment or other spending (not wages, rent, stock, loans, tax, fees, marketing), never family life's own; a loan is money borrowed, not interest or a loan paid back");
        string balance = ModSource(@"Home\HomeBalance.cs");
        Check(balance.Contains("public const float StressHigh = " + HardTimesRules.StressFrom.ToString("0", CultureInfo.InvariantCulture) + "f;")
              && balance.Contains("public const float DebtHigh = " + HardTimesRules.SleepFrom.ToString("0", CultureInfo.InvariantCulture) + "f;")
              && balance.Contains("public const float DietPoor = " + HardTimesRules.DietPoor.ToString("0", CultureInfo.InvariantCulture) + "f;")
              && balance.Contains("public const float MessLow = " + HardTimesRules.MessFrom.ToString("0", CultureInfo.InvariantCulture) + "f;"),
            "hard times: stressed, short of sleep, a poor diet and a messy home are where home life itself says so");
    }

    // ---------------------------------------------------------------- the cost

    private static void HardPace()
    {
        var m = new HardMemory();
        var seen = new List<int> { 1, 3 };
        var raw = new int[HardTimesRules.Strains];
        var usable = new int[HardTimesRules.Strains];
        HardSigns g = HardHome(10, 20);
        g.WorkHours = 22f;
        g.Tidy = 20f;
        int n = 0;
        HardTimesRules.Choose(g, m, HardSeed, seen, 2, 3, raw, usable, out _);
        Stopwatch watch = Stopwatch.StartNew();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        for (int i = 0; i < 24 * 365; i++)
        {
            g.Day = 10 + i / 24;
            g.Hour = i % 24;
            n += HardTimesRules.Choose(g, m, HardSeed, seen, 2, 3, raw, usable, out _) + 1;
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(watch.Elapsed.TotalMilliseconds < 200.0, "hard times: a year of game hours weighed takes " + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms (under 200; the game asks once an hour)");
        Check(allocated == 0 && n >= 0, "hard times: weighing an hour makes nothing new (" + allocated + " bytes over a year of hours)");
        watch.Restart();
        string line = null;
        for (int i = 0; i < 10000; i++)
        {
            line = m.Write();
            HardMemory.Read(line);
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 300.0, "hard times: the save's line written and read ten thousand times takes " + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms (once a game hour in play)");
    }

    // ---------------------------------------------------------------- the texts

    private static void HardWords()
    {
        Check(FixLineEverywhere("a difficult moment at home now comes from what is really going on", "um momento difícil em casa agora vem do que está acontecendo de verdade",
                                "家里的艰难时刻现在源于真实发生的事情", "Ein schwieriger Moment zu Hause entsteht jetzt aus dem, was wirklich los ist",
                                "un moment difficile à la maison vient désormais de ce qui se passe vraiment", "un momento difícil en casa ahora surge de lo que de verdad está pasando",
                                "трудный момент дома теперь возникает из того, что действительно происходит", "een moeilijk moment thuis komt nu voort uit wat er echt speelt",
                                "evde zor bir an artık rastgele gelmiyor"),
            "hard times: What's new has the fix's line, with its fix mark, in some row of every language");
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("hard times", langs, codes))
        {
            return;
        }
        foreach (string key in new[] { "reallife_family_test_argue", "reallife_family_no_argue_cause" })
        {
            bool all = LanguagesLacking(langs, codes, key).Length == 0;
            for (int l = 0; all && l < langs.Length; l++)
            {
                string text = langs[l][key];
                all &= text.Length > 10 && (l == 0 || text != langs[0][key]) && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ")
                       && (key != "reallife_family_no_argue_cause" || text.Contains("{name}"));
            }
            Check(all && codes.Length == ModLanguages.All.Length, "hard times: " + key + " is in every language, each its own, the partner's name kept, no dash and no exclamation mark");
        }
        Check(langs[0]["reallife_family_no_argue_cause"].Length <= 240, "hard times: the test's notice is within 240 characters");
    }
}
