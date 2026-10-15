using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Permadeath;

// Permadeath (Scripts/Permadeath): the odds stay rare and stop well short of certain, a warning always comes first, nothing
// can happen while it is not allowed (off, dead already, a Going Public session, the game's tutorial or a mission, the game
// busy), the flu's three warnings in their turn before its fatal stretch, the switch kept per save and what a switch in the
// main menu applies to, going without food (three warnings a day apart from a day on an empty stomach, eating ends it,
// never fatal at a moment the game cannot take it, rare after), a dead save reads as dead and stays dead, the life's
// story and its line, and the obituary's
// sentences: each one there only when the record has something for it to say, and every key in every language.
internal static partial class Program
{
    private static readonly DeathCause[] Dangers = { DeathCause.Crash, DeathCause.Struck, DeathCause.Mugging, DeathCause.Assault };

    private static void Endings()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        EndingsAllowed();
        EndingsOdds();
        EndingsWarnFirst();
        EndingsRare();
        EndingsIllness();
        EndingsStarve();
        EndingsSwitch();
        EndingsSaved();
        EndingsStory();
        EndingsDraft();
        EndingsWords();
        Console.WriteLine("  Permadeath: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static Moment Open()
    {
        return new Moment { Armed = true };
    }

    private static void EndingsAllowed()
    {
        int allowed = 0;
        for (int bits = 0; bits < 32; bits++)
        {
            var m = new Moment
            {
                Armed = (bits & 1) != 0, Dead = (bits & 2) != 0, InSession = (bits & 4) != 0, MissionOrTutorial = (bits & 8) != 0, Busy = (bits & 16) != 0
            };
            bool yes = PermadeathRules.Allowed(m);
            allowed += yes ? 1 : 0;
            bool killed = false;
            foreach (DeathCause cause in Dangers)
            {
                killed |= PermadeathRules.Judge(cause, 100f, true, m, 0f) != Verdict.None || PermadeathRules.Judge(cause, 100f, false, m, 0f) != Verdict.None;
            }
            killed |= PermadeathRules.IllHour(500f, 3, m, 0f, out int warn) != Verdict.None || warn != 0;
            killed |= PermadeathRules.StarveHour(500f, 3, m, true, 0f, out int starveWarn) != Verdict.None || starveWarn != 0;
            killed |= PermadeathRules.StarveHour(500f, 0, m, true, 0f, out int starveFirst) != Verdict.None || starveFirst != 0;
            if (!yes)
            {
                Check(!killed, "permadeath: nothing at all happens while it is not allowed (moment " + bits + ")");
            }
        }
        Check(allowed == 1, "permadeath: only one moment of the 32 allows a death: on, alive, alone, the game's story not running, nothing under way (" + allowed + ")");
        Check(!PermadeathRules.Allowed(new Moment()), "permadeath: off by default: a moment of nothing set allows nothing");
        Check(PermadeathRules.Judge(DeathCause.Illness, 100f, true, Open(), 0f) == Verdict.None && PermadeathRules.Judge(DeathCause.Test, 100f, true, Open(), 0f) == Verdict.None
              && PermadeathRules.Judge(DeathCause.None, 100f, true, Open(), 0f) == Verdict.None,
            "permadeath: the illness has its own course, and a test or no cause kills nobody through a dangerous moment");
    }

    private static void EndingsOdds()
    {
        bool rare = true, monotone = true, belowNothing = true;
        foreach (DeathCause cause in Dangers)
        {
            float last = -1f;
            for (float speed = 0f; speed <= 120f; speed += 0.25f)
            {
                float odds = PermadeathRules.Odds(cause, speed);
                rare &= odds >= 0f && odds <= 0.35f;
                if (PermadeathRules.Fatal(cause, speed))
                {
                    monotone &= odds >= last - 1e-6f;
                    last = odds;
                }
                else
                {
                    belowNothing &= odds == 0f && PermadeathRules.Judge(cause, speed, true, Open(), 0f) == Verdict.None && PermadeathRules.Judge(cause, speed, false, Open(), 0f) == Verdict.None;
                }
            }
        }
        Check(rare, "permadeath: no danger is ever fatal more than 35% of the time, however bad");
        Check(monotone, "permadeath: a worse crash or a faster car is never less dangerous than a milder one");
        Check(belowNothing, "permadeath: below its threshold a danger is what it always was: no near miss, no death");
        Check(!PermadeathRules.Fatal(DeathCause.Crash, 24.9f) && PermadeathRules.Fatal(DeathCause.Crash, 25f) && Math.Abs(PermadeathRules.CrashFrom * 3.6f - 90f) < 0.01f,
            "permadeath: a crash can be fatal from an impact of 90 km/h, and not below");
        Check(PermadeathRules.CrashFrom > 17f, "permadeath: every crash bad enough to be fatal is one the hospital is called for already (from 61 km/h)");
        Check(Math.Abs(PermadeathRules.Odds(DeathCause.Crash, PermadeathRules.CrashFrom) - PermadeathRules.CrashLeast) < 1e-6f
              && Math.Abs(PermadeathRules.Odds(DeathCause.Crash, PermadeathRules.CrashWorst) - PermadeathRules.CrashMost) < 1e-6f
              && Math.Abs(PermadeathRules.Odds(DeathCause.Crash, 80f) - PermadeathRules.CrashMost) < 1e-6f,
            "permadeath: a crash's odds run from 5% at 90 km/h to 35% at 144 km/h, and no higher beyond");
        Check(PermadeathRules.StruckFrom > 35f / 3.6f && Math.Abs(PermadeathRules.StruckFrom * 3.6f - 55f) < 0.01f && PermadeathRules.Fatal(DeathCause.Struck, 55.01f / 3.6f) && !PermadeathRules.Fatal(DeathCause.Struck, 54.9f / 3.6f),
            "permadeath: on foot, a hit can be fatal from 55 km/h (the hospital is from 35), and not below");
        Check(Math.Abs(PermadeathRules.Odds(DeathCause.Struck, PermadeathRules.StruckWorst) - PermadeathRules.StruckMost) < 1e-6f && PermadeathRules.StruckMost <= 0.3f,
            "permadeath: a hit on foot is fatal at most 30% of the time, from 90 km/h");
        Check(PermadeathRules.Odds(DeathCause.Mugging, 0f) == 0.10f && PermadeathRules.Odds(DeathCause.Assault, 0f) == 0.08f,
            "permadeath: one hurt in ten from a mugger, about one beating in twelve from the lender's men, once warned");
        Check(PermadeathRules.Judge(DeathCause.Crash, 30f, true, Open(), 0.999f) == Verdict.None && PermadeathRules.Judge(DeathCause.Crash, 30f, true, Open(), 0f) == Verdict.Death,
            "permadeath: once warned, a death is the roll under the odds and nothing else");
        Check(PermadeathRules.Judge(DeathCause.Crash, 30f, true, Open(), 0.3f) == PermadeathRules.Judge(DeathCause.Crash, 30f, true, Open(), 0.3f),
            "permadeath: the same moment and the same roll always come to the same");
    }

    private static void EndingsWarnFirst()
    {
        bool first = true;
        foreach (DeathCause cause in Dangers)
        {
            for (float speed = 0f; speed <= 120f; speed += 1f)
            {
                Verdict v = PermadeathRules.Judge(cause, speed, false, Open(), 0f);
                first &= v != Verdict.Death && (v == Verdict.NearMiss) == PermadeathRules.Fatal(cause, speed);
            }
        }
        Check(first, "permadeath: the first dangerous moment of each kind is a near miss, never a death, even with the worst roll");

        // A save's whole life of crashes at 100 km/h, played by the rules with what the save remembers: the first is always the warning.
        var rng = new Random(20261013);
        int warnedAt = -1, diedAt = -1;
        bool warned = false;
        for (int crash = 1; crash <= 1000 && diedAt < 0; crash++)
        {
            Verdict v = PermadeathRules.Judge(DeathCause.Crash, 100f / 3.6f, warned, Open(), (float)rng.NextDouble());
            if (v == Verdict.NearMiss)
            {
                warnedAt = warnedAt < 0 ? crash : warnedAt;
                warned = true;
            }
            else if (v == Verdict.Death)
            {
                diedAt = crash;
            }
        }
        Check(warnedAt == 1 && diedAt > 1, "permadeath: the warning is the first crash and a death only ever comes after it (warned at " + warnedAt + ", died at " + diedAt + ")");
    }

    private static void EndingsRare()
    {
        var rng = new Random(7);
        const int Moments = 200000;
        int crash90 = 0, crash144 = 0, struck60 = 0, refusals = 0, beatings = 0;
        for (int i = 0; i < Moments; i++)
        {
            crash90 += PermadeathRules.Judge(DeathCause.Crash, 25f, true, Open(), (float)rng.NextDouble()) == Verdict.Death ? 1 : 0;
            crash144 += PermadeathRules.Judge(DeathCause.Crash, 40f, true, Open(), (float)rng.NextDouble()) == Verdict.Death ? 1 : 0;
            struck60 += PermadeathRules.Judge(DeathCause.Struck, 60f / 3.6f, true, Open(), (float)rng.NextDouble()) == Verdict.Death ? 1 : 0;
            // A refusal: half the time the mugger runs, half the time the player is hurt (Crime/Scenes/PlayerMuggingScene); only a hurt is judged.
            if (rng.NextDouble() >= 0.5)
            {
                refusals += PermadeathRules.Judge(DeathCause.Mugging, 0f, true, Open(), (float)rng.NextDouble()) == Verdict.Death ? 1 : 0;
            }
            else
            {
                rng.NextDouble();
            }
            beatings += PermadeathRules.Judge(DeathCause.Assault, 0f, true, Open(), (float)rng.NextDouble()) == Verdict.Death ? 1 : 0;
        }
        float a = crash90 / (float)Moments, b = crash144 / (float)Moments, c = struck60 / (float)Moments, d = refusals / (float)Moments, e = beatings / (float)Moments;
        Console.WriteLine("  Permadeath over " + Moments + " warned moments each: a crash at 90 km/h fatal " + a.ToString("P1") + ", at 144 km/h " + b.ToString("P1") + "; hit on foot at 60 km/h " + c.ToString("P1")
                          + "; a mugger refused " + d.ToString("P1") + " of refusals; the lender's men " + e.ToString("P1") + " of beatings.");
        Check(Math.Abs(a - 0.05f) < 0.006f && Math.Abs(b - 0.35f) < 0.01f, "permadeath: crashes are fatal at the rates the Guide gives (" + a.ToString("P1") + ", " + b.ToString("P1") + ")");
        Check(c < 0.10f, "permadeath: a hit on foot just over the threshold is fatal less than one time in ten (" + c.ToString("P1") + ")");
        Check(d < 0.06f && d > 0.04f, "permadeath: about one refusal in twenty ends a life once warned (" + d.ToString("P1") + ")");
        Check(e < 0.09f, "permadeath: under one beating in eleven ends a life once warned (" + e.ToString("P1") + ")");
    }

    private static void EndingsIllness()
    {
        Check(PermadeathRules.IllStage(0f) == 0 && PermadeathRules.IllStage(47.9f) == 0 && PermadeathRules.IllStage(48f) == 1 && PermadeathRules.IllStage(72f) == 2
              && PermadeathRules.IllStage(96f) == 3 && PermadeathRules.IllStage(119.9f) == 3 && PermadeathRules.IllStage(120f) == 4,
            "permadeath: a flu's stages: warnings after two, three and four days untreated, the fatal stretch from five");
        Check(!PermadeathRules.IllHeld(0) && PermadeathRules.IllHeld(1) && PermadeathRules.IllHeld(4), "permadeath: a flu no longer clears up by itself from its first warning");
        double day = 1.0 - Math.Pow(1.0 - PermadeathRules.IllPerHour, 24.0);
        Check(Math.Abs(day - PermadeathRules.IllPerDay) < 1e-4, "permadeath: the fatal stretch's hours come to a quarter over a whole day (" + day.ToString("F4") + ")");

        // Hour by hour from the flu's start, untreated, with the worst roll every hour: three warnings in order, a day apart, then the end.
        int told = 0;
        var warnings = new List<int>();
        int diedAtHour = -1;
        for (int hour = 0; hour <= 200 && diedAtHour < 0; hour++)
        {
            Verdict v = PermadeathRules.IllHour(hour, told, Open(), 0f, out int warn);
            if (warn > 0)
            {
                warnings.Add(hour);
                told = warn;
            }
            if (v == Verdict.Death)
            {
                diedAtHour = hour;
            }
        }
        Check(warnings.Count == 3 && warnings[0] == 48 && warnings[1] == 72 && warnings[2] == 96, "permadeath: the flu's three warnings come at 48, 72 and 96 hours (" + string.Join(", ", warnings) + ")");
        Check(diedAtHour == 120, "permadeath: even with the worst roll, the flu cannot end a life before five days untreated (" + diedAtHour + ")");

        // A long sleep jumps the course: the warnings still come one at a time, and none is skipped before the end can come.
        told = 0;
        Verdict jump = PermadeathRules.IllHour(150f, told, Open(), 0f, out int w1);
        told = w1;
        Verdict jump2 = PermadeathRules.IllHour(151f, told, Open(), 0f, out int w2);
        told = w2;
        Verdict jump3 = PermadeathRules.IllHour(152f, told, Open(), 0f, out int w3);
        told = w3;
        Verdict jump4 = PermadeathRules.IllHour(153f, told, Open(), 0f, out int w4);
        Check(w1 == 1 && w2 == 2 && w3 == 3 && w4 == 0 && jump != Verdict.Death && jump2 != Verdict.Death && jump3 != Verdict.Death && jump4 == Verdict.Death,
            "permadeath: after a long sleep the warnings still come in turn, an hour apart, before the flu can kill");
        var off = new Moment { Armed = false };
        Check(PermadeathRules.IllHour(150f, 0, off, 0f, out int none) == Verdict.None && none == 0, "permadeath: off, a flu warns of nothing and kills nobody");

        // Untreated from the start with honest rolls: how long a life lasts. The expected wait in the fatal stretch is about four days.
        var rng = new Random(11);
        double total = 0;
        const int Lives = 4000;
        for (int life = 0; life < Lives; life++)
        {
            told = 3;
            int hour = PermadeathRules.IllFatalFrom;
            while (PermadeathRules.IllHour(hour, told, Open(), (float)rng.NextDouble(), out _) != Verdict.Death && hour < 10000)
            {
                hour++;
            }
            total += hour - PermadeathRules.IllFatalFrom;
        }
        double mean = total / Lives / 24.0;
        Console.WriteLine("  Permadeath: a flu left untreated past its warnings lasts " + mean.ToString("F1") + " more days on average before it is fatal.");
        Check(mean > 2.8 && mean < 4.2, "permadeath: past all three warnings, a flu kills only after days more without a doctor (" + mean.ToString("F1") + " on average)");
    }

    private static void EndingsStarve()
    {
        Check(PermadeathRules.StarveStage(0f) == 0 && PermadeathRules.StarveStage(23.9f) == 0 && PermadeathRules.StarveStage(24f) == 1 && PermadeathRules.StarveStage(48f) == 2
              && PermadeathRules.StarveStage(72f) == 3 && PermadeathRules.StarveStage(95.9f) == 3 && PermadeathRules.StarveStage(96f) == 4,
            "permadeath: going without food's stages: warnings after one, two and three days with the bar at its bottom, the fatal stretch from four");
        Check(PermadeathRules.StarveWarnFirst >= 24 && PermadeathRules.StarveFatalFrom >= PermadeathRules.StarveWarnLast + 24,
            "permadeath: a whole day on an empty stomach before the first warning, and a whole day after the last before it can kill");
        double day = 1.0 - Math.Pow(1.0 - PermadeathRules.StarvePerHour, 24.0);
        Check(Math.Abs(day - PermadeathRules.StarvePerDay) < 1e-4 && PermadeathRules.StarvePerDay <= PermadeathRules.IllPerDay,
            "permadeath: the fatal stretch's hours come to a fifth over a whole day, no more than the flu (" + day.ToString("F4") + ")");
        Check(PermadeathRules.Empty(0f) && PermadeathRules.Empty(4.9f) && !PermadeathRules.Empty(5f) && !PermadeathRules.Empty(60f),
            "permadeath: the bar is at its bottom under 5, where the game shows its own face for too hungry");
        Check(PermadeathRules.Ate(0f, 1f) && PermadeathRules.Ate(2f, 30f) && !PermadeathRules.Ate(3f, 3f) && !PermadeathRules.Ate(3f, 2.9f) && !PermadeathRules.Ate(1f, 1.2f),
            "permadeath: anything eaten counts (a rise of half a point), and the bar falling or standing never does");
        Check(PermadeathRules.StarveDrain(0) == 0f && PermadeathRules.StarveDrain(1) == 0f && PermadeathRules.StarveDrain(2) > 0f && PermadeathRules.StarveDrain(3) > PermadeathRules.StarveDrain(2)
              && PermadeathRules.StarveDrain(3) <= 0.04f && PermadeathRules.StarveMood < 0,
            "permadeath: weak with hunger: a low mood from the first warning, energy draining faster from the second and more from the third, never more than just living costs again");

        // Hour by hour from the bar's bottom with the worst roll every hour: three warnings in order, a day apart, then the end.
        int told = 0;
        var warnings = new List<int>();
        int diedAtHour = -1;
        for (int hour = 0; hour <= 200 && diedAtHour < 0; hour++)
        {
            Verdict v = PermadeathRules.StarveHour(hour, told, Open(), true, 0f, out int warn);
            if (warn > 0)
            {
                warnings.Add(hour);
                told = warn;
            }
            if (v == Verdict.Death)
            {
                diedAtHour = hour;
            }
        }
        Check(warnings.Count == 3 && warnings[0] == 24 && warnings[1] == 48 && warnings[2] == 72, "permadeath: going without food warns at 24, 48 and 72 hours (" + string.Join(", ", warnings) + ")");
        Check(diedAtHour == 96, "permadeath: even with the worst roll, going without food cannot end a life before four days (" + diedAtHour + ")");

        // Never at a moment the game cannot take it: asleep, a dialog, a ride, in hospital. It still warns then.
        bool never = true;
        for (int hour = 0; hour <= 400; hour++)
        {
            never &= PermadeathRules.StarveHour(hour, 3, Open(), false, 0f, out _) != Verdict.Death;
        }
        Check(never, "permadeath: going without food never kills while asleep, in hospital or in the middle of something, whatever the roll");
        Check(PermadeathRules.StarveHour(30f, 0, Open(), false, 0f, out int busyWarn) == Verdict.NearMiss && busyWarn == 1, "permadeath: a warning still comes at a busy moment");

        // A long sleep jumps the course: the warnings still come one at a time before the end can come.
        Verdict j1 = PermadeathRules.StarveHour(150f, 0, Open(), true, 0f, out int w1);
        Verdict j2 = PermadeathRules.StarveHour(151f, w1, Open(), true, 0f, out int w2);
        Verdict j3 = PermadeathRules.StarveHour(152f, w2, Open(), true, 0f, out int w3);
        Verdict j4 = PermadeathRules.StarveHour(153f, w3, Open(), true, 0f, out int w4);
        Check(w1 == 1 && w2 == 2 && w3 == 3 && w4 == 0 && j1 != Verdict.Death && j2 != Verdict.Death && j3 != Verdict.Death && j4 == Verdict.Death,
            "permadeath: after a long sleep going without food's warnings still come in turn before it can kill");
        Check(PermadeathRules.StarveHour(150f, 0, new Moment { Armed = false }, true, 0f, out int none) == Verdict.None && none == 0
              && PermadeathRules.StarveHour(150f, 3, new Moment { Armed = true, MissionOrTutorial = true }, true, 0f, out int inMission) == Verdict.None && inMission == 0,
            "permadeath: off, or in the game's own story, an empty stomach warns of nothing and kills nobody");
        Check(PermadeathRules.Judge(DeathCause.Starvation, 100f, true, Open(), 0f) == Verdict.None, "permadeath: going without food has its own course, never a dangerous moment's");

        // Eating ends it: the state forgets the clock and the warnings.
        PermadeathState.Reset();
        PermadeathState.Empty(1000f);
        PermadeathState.StarveTold = 2;
        PermadeathState.Empty(5000f);
        bool kept = PermadeathState.EmptySince == 1000f && PermadeathState.StarveTold == 2;
        PermadeathState.Fed();
        Check(kept && PermadeathState.EmptySince < 0f && PermadeathState.StarveTold == 0, "permadeath: the empty stomach's clock keeps running until something is eaten, which ends it and its warnings");
        PermadeathState.Empty(7000f);
        Check(PermadeathState.EmptySince == 7000f && PermadeathState.StarveTold == 0, "permadeath: going without food again starts a new count from nothing");
        PermadeathState.Reset();

        // With honest rolls, past all three warnings: how long a life lasts on an empty stomach (expected about four and a half days).
        var rng = new Random(13);
        double total = 0;
        const int Lives = 4000;
        for (int life = 0; life < Lives; life++)
        {
            int hour = PermadeathRules.StarveFatalFrom;
            while (PermadeathRules.StarveHour(hour, 3, Open(), true, (float)rng.NextDouble(), out _) != Verdict.Death && hour < 10000)
            {
                hour++;
            }
            total += hour - PermadeathRules.StarveFatalFrom;
        }
        double mean = total / Lives / 24.0;
        Console.WriteLine("  Permadeath: going without food past its warnings lasts " + mean.ToString("F1") + " more days on average before it is fatal.");
        Check(mean > 3.6 && mean < 5.4, "permadeath: past all three warnings, going without food kills only after days more (" + mean.ToString("F1") + " on average)");

        var starved = new LifeStory { Name = "Ash", Age = 30, Day = 40, Cause = DeathCause.Starvation };
        var back = new LifeStory();
        PermadeathStory.Draft(starved, false, DraftScratch);
        Check(back.Read(starved.Write()) && back.Cause == DeathCause.Starvation && Has(DraftScratch, "_stand_starvation") && Has(DraftScratch, "_how_starvation"),
            "permadeath: a death from going without food is kept in the life's line and has its own sentences on the page");
    }

    private static void EndingsSwitch()
    {
        int armed = 0;
        for (int bits = 0; bits < 8; bits++)
        {
            armed += PermadeathRules.FirstArmed((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0) ? 1 : 0;
        }
        Check(armed == 1 && PermadeathRules.FirstArmed(true, true, true), "permadeath: a save starts with it on only when it is new, just begun and it was switched on in the main menu");
        Check(!PermadeathRules.FirstArmed(true, false, true), "permadeath: switched on in the main menu, it does not arm a save that was already going");
        Check(!PermadeathRules.Switches(false, true, true, false) && !PermadeathRules.Switches(true, true, true, false) && !PermadeathRules.Switches(false, false, true, false),
            "permadeath: once dead, the switch changes nothing: it cannot bring a character back");
        Check(!PermadeathRules.Switches(true, false, false, true) && PermadeathRules.Switches(false, true, false, true), "permadeath: in a session it cannot be switched on, and it can always be switched off");
        Check(PermadeathRules.Switches(true, false, false, false) && PermadeathRules.Switches(false, true, false, false) && !PermadeathRules.Switches(true, true, false, false),
            "permadeath: alive and alone, the switch does what it says, and moving it to what holds changes nothing");

        // Two saves, one switched on: the other is untouched by it.
        var one = new Dictionary<string, string>();
        var two = new Dictionary<string, string>();
        PermadeathState.Read(one);
        PermadeathState.Armed = true;
        PermadeathState.Write(one);
        PermadeathState.Read(two);
        bool twoArmed = PermadeathState.Armed;
        PermadeathState.Write(two);
        PermadeathState.Read(one);
        bool oneArmed = PermadeathState.Armed;
        Check(oneArmed && !twoArmed, "permadeath: switching it on in one save arms no other");
        PermadeathState.ArmNext = true;
        PermadeathState.Read(new Dictionary<string, string>());
        Check(PermadeathState.ArmNext && PermadeathState.Fresh && !PermadeathState.Armed, "permadeath: reading a save leaves the main menu's switch for the director to take, and a new save reads as off");
        PermadeathState.ArmNext = false;
        PermadeathState.Reset();
    }

    private static void EndingsSaved()
    {
        var data = new Dictionary<string, string>();
        PermadeathState.Read(data);
        Check(PermadeathState.Fresh && !PermadeathState.Armed && !PermadeathState.Dead && PermadeathState.Loaded, "permadeath: a save without the feature reads as fresh, off and alive");
        PermadeathState.Armed = true;
        PermadeathState.Warn(DeathCause.Crash);
        PermadeathState.Warn(DeathCause.Mugging);
        PermadeathState.Flu(1234.5f);
        PermadeathState.IllTold = 2;
        PermadeathState.ArmedDay = 17;
        PermadeathState.Empty(4321.5f);
        PermadeathState.StarveTold = 3;
        PermadeathState.See("5th Ave#12");
        PermadeathState.See("5th Ave#12");
        PermadeathState.Write(data);
        PermadeathState.Read(data);
        Check(!PermadeathState.Fresh && PermadeathState.Armed && !PermadeathState.Dead && PermadeathState.HasWarned(DeathCause.Crash) && PermadeathState.HasWarned(DeathCause.Mugging)
              && !PermadeathState.HasWarned(DeathCause.Struck) && PermadeathState.IllSince == 1234.5f && PermadeathState.IllTold == 2 && PermadeathState.ArmedDay == 17 && PermadeathState.Seen.Count == 1
              && PermadeathState.EmptySince == 4321.5f && PermadeathState.StarveTold == 3,
            "permadeath: the switch, the near misses had, the flu's and going without food's warnings and the businesses run come back from the save as they went in");
        var older = new Dictionary<string, string> { { PermadeathState.Key, "v1|1|0|2|-1|0|5" } };
        PermadeathState.Read(older);
        Check(!PermadeathState.Fresh && PermadeathState.Armed && PermadeathState.ArmedDay == 5 && PermadeathState.EmptySince < 0f && PermadeathState.StarveTold == 0,
            "permadeath: a line from before going without food reads as it was, and fed");
        PermadeathState.Read(data);
        Check(data[PermadeathState.Key].StartsWith("v1|1|0|", StringComparison.Ordinal) && !data.ContainsKey(PermadeathState.RecordKey), "permadeath: a living save has its line and no record");
        PermadeathState.Flu(1234.5f);
        Check(PermadeathState.IllTold == 2, "permadeath: the same flu keeps its warnings");
        PermadeathState.Flu(9999f);
        Check(PermadeathState.IllTold == 0 && PermadeathState.IllSince == 9999f, "permadeath: a new flu starts its warnings again");
        PermadeathState.NoFlu();
        Check(PermadeathState.IllSince < 0f && PermadeathState.IllTold == 0, "permadeath: a treated flu's warnings are over");

        // Dead: the save reads as dead, with its record, and stays so.
        PermadeathState.Dead = true;
        PermadeathState.Record.Clear();
        PermadeathState.Record.Name = "Sam Doe";
        PermadeathState.Record.Age = 34;
        PermadeathState.Record.Cause = DeathCause.Crash;
        PermadeathState.Record.Day = 300;
        PermadeathState.Write(data);
        PermadeathState.Read(data);
        Check(PermadeathState.Dead && PermadeathState.Record.Name == "Sam Doe" && PermadeathState.Record.Cause == DeathCause.Crash && PermadeathState.Record.Day == 300,
            "permadeath: a dead save reads as dead, with the life it ended on");
        Check(PermadeathRules.Ended(PermadeathState.Dead, PermadeathState.Armed, false), "permadeath: a dead save is read, never played");
        Check(PermadeathRules.Ended(false, true, true) && !PermadeathRules.Ended(false, false, true) && !PermadeathRules.Ended(false, true, false),
            "permadeath: an older save of a character who died since ends only if it was on in it");
        data[PermadeathState.RecordKey] = "garbage";
        PermadeathState.Read(data);
        Check(PermadeathState.Dead && PermadeathState.Record.Name.Length == 0, "permadeath: a dead save whose record cannot be read is still dead");
        data[PermadeathState.Key] = "v9|0|1|0|0|0|0";
        PermadeathState.Read(data);
        Check(PermadeathState.Fresh && !PermadeathState.Dead && !PermadeathState.Armed, "permadeath: a line of a version this does not know is never taken as a death");
        data.Clear();
        for (int i = 0; i < 300; i++)
        {
            PermadeathState.See("Street#" + i);
        }
        Check(PermadeathState.Seen.Count == PermadeathState.SeenMost, "permadeath: the businesses remembered are capped at " + PermadeathState.SeenMost);
        PermadeathState.Reset();
    }

    private static LifeStory Sample()
    {
        var s = new LifeStory { Name = "Sam Doe", Age = 41, Day = 245, DaysPerYear = 60, Cause = DeathCause.Struck, Place = "Fifth Avenue", Worth = 1250000, Founded = 4, Owned = 2,
            Employees = 23, Properties = 3, Friends = 5, Partner = "Alex Roe", Bond = 3, Arrests = 2, Convictions = 1, Followers = 1830, Elections = 3 };
        s.Businesses.Add("Doe's Deli");
        s.Businesses.Add("Corner Books");
        s.Close.Add("Jo Park");
        s.Close.Add("Lee Kim");
        s.Children.Add("Max");
        s.Family.Add("Mom");
        return s;
    }

    private static void EndingsStory()
    {
        LifeStory s = Sample();
        var back = new LifeStory();
        Check(back.Read(s.Write()) && back.Write() == s.Write() && back.Name == "Sam Doe" && back.Worth == 1250000 && back.Cause == DeathCause.Struck && back.Businesses.Count == 2
              && back.Close.Count == 2 && back.Children[0] == "Max" && back.Family[0] == "Mom" && back.Bond == 3 && back.Elections == 3 && back.Followers == 1830,
            "permadeath: the life's line reads back exactly as it was written");
        var odd = new LifeStory { Name = "A|B;C\nD", Partner = "x|y" };
        LifeStory.Keep(odd.Close, "Pat;Sam");
        Check(back.Read(odd.Write()) && back.Name == "A B C D" && back.Partner == "x y" && back.Close.Count == 1 && back.Close[0] == "Pat Sam", "permadeath: names with the line's dividers in them are kept, without the dividers");
        var debt = new LifeStory { Worth = -54000 };
        Check(back.Read(debt.Write()) && back.Worth == -54000, "permadeath: an estate below zero is kept as a debt");
        Check(!back.Read("") && !back.Read("1|x") && !back.Read("2|" + new string('|', 30)) && back.Name.Length == 0, "permadeath: a line that is short or of another version is not read");
        Check(s.Years == 4 && s.DaysOver == 5 && new LifeStory { Day = 1 }.Days == 1 && new LifeStory { Day = 1 }.Years == 0, "permadeath: the time in the city is whole years and days, the first day counting");
        var names = new List<string>();
        s.Mourners(names);
        Check(names.Count == 5 && names[0] == "Alex Roe" && names[1] == "Max" && names[2] == "Mom" && names[3] == "Jo Park" && names[4] == "Lee Kim",
            "permadeath: the mourners are the partner, the children, the family, then the close friends (" + string.Join(", ", names) + ")");
        var many = new LifeStory { Partner = "P" };
        for (int i = 0; i < 6; i++)
        {
            LifeStory.Keep(many.Children, "C" + i);
            LifeStory.Keep(many.Close, "F" + i);
        }
        LifeStory.Keep(many.Close, "P");
        many.Mourners(names);
        Check(names.Count == LifeStory.MournersMost && names.IndexOf("P") == 0 && names.LastIndexOf("P") == 0, "permadeath: at most eight mourners, nobody twice");
        LifeStory.Keep(many.Children, "C9");
        Check(many.Children.Count == LifeStory.NamesMost, "permadeath: the lists keep at most six names");
    }

    private static readonly List<ObitLine> DraftScratch = new List<ObitLine>(64);

    private static bool Has(List<ObitLine> lines, string ending)
    {
        foreach (ObitLine line in lines)
        {
            if (line.Key.EndsWith(ending, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void EndingsDraft()
    {
        List<ObitLine> d = DraftScratch;
        LifeStory s = Sample();
        PermadeathStory.Draft(s, false, d);
        Check(Has(d, "_stand_struck") && Has(d, "_how_struck") && Has(d, "_founded_many") && Has(d, "_owned_many") && Has(d, "_staff_many") && Has(d, "_property_many")
              && Has(d, "obit_worth") && Has(d, "_married") && Has(d, "_child_one") && Has(d, "_family") && Has(d, "_friends_close") && Has(d, "_record")
              && Has(d, "_followers_many") && Has(d, "_voted_many") && Has(d, "_mourners") && Has(d, "_notice") && !Has(d, "_notice_test"),
            "permadeath: a full life has a sentence for each part of its record");
        Check(d[0].Part == ObitPart.Kicker && d[1].Part == ObitPart.Head && d[2].Part == ObitPart.Stand && d[d.Count - 1].Part == ObitPart.Notice,
            "permadeath: the page reads kicker, headline, standfirst first and the notice last");
        int figures = 0;
        foreach (ObitLine line in d)
        {
            figures += line.Part == ObitPart.Figure ? 1 : 0;
        }
        Check(figures == 12, "permadeath: the box has twelve figures (" + figures + ")");

        var bare = new LifeStory { Name = "Ash", Age = 19, Day = 3, Cause = DeathCause.Illness };
        PermadeathStory.Draft(bare, false, d);
        Check(Has(d, "_no_business") && !Has(d, "_founded_one") && !Has(d, "_founded_many") && !Has(d, "_owned_one") && !Has(d, "_staff_one") && !Has(d, "_staff_many")
              && !Has(d, "_property_one") && !Has(d, "obit_worth") && !Has(d, "_debt") && !Has(d, "_married") && !Has(d, "_together") && !Has(d, "_child_one") && !Has(d, "_family")
              && Has(d, "_friends_none") && Has(d, "_clean") && !Has(d, "_followers_one") && !Has(d, "_voted_one") && Has(d, "_mourned_none") && Has(d, "_stand_illness"),
            "permadeath: a short life says nothing it has no record of");

        var ones = new LifeStory { Name = "Bo", Founded = 1, Owned = 1, Employees = 1, Properties = 1, Worth = -10, Partner = "Cy", Bond = 2, Friends = 1, Arrests = 1, Followers = 1, Elections = 1 };
        ones.Children.Add("Di");
        ones.Children.Add("Ed");
        PermadeathStory.Draft(ones, false, d);
        Check(Has(d, "_founded_one") && Has(d, "_owned_one") && Has(d, "_staff_one") && Has(d, "_property_one") && Has(d, "_debt") && Has(d, "_engaged") && Has(d, "_child_many")
              && Has(d, "_friends_count") && Has(d, "_arrested") && Has(d, "_followers_one") && Has(d, "_voted_one"),
            "permadeath: a count of one has its own sentence, and debts are said as debts");
        var founder = new LifeStory { Founded = 0, Owned = 3 };
        Check(PermadeathStory.Founded(founder) == 3, "permadeath: never fewer founded than are open at the end");
        PermadeathStory.Draft(founder, false, d);
        Check(Has(d, "_founded_many") && !Has(d, "_no_business"), "permadeath: businesses open at the end count as founded");
        var together = new LifeStory { Partner = "Fi", Bond = 1, Convictions = 2 };
        PermadeathStory.Draft(together, false, d);
        Check(Has(d, "_together") && Has(d, "_convicted"), "permadeath: a partner not married or engaged, convictions without arrests");

        PermadeathStory.Draft(s, true, d);
        Check(Has(d, "_stand_test") && !Has(d, "_how_struck") && !Has(d, "_how_test") && Has(d, "_notice_test") && Has(d, "_notice_head_test"),
            "permadeath: the rehearsal's page says it is a test and that nobody has died");
        var none = Sample();
        none.Cause = DeathCause.None;
        PermadeathStory.Draft(none, false, d);
        Check(Has(d, "_stand_none") && !Has(d, "_how_none"), "permadeath: a death with no cause of its own says only that they have died");

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 20000; i++)
        {
            PermadeathStory.Draft(s, i % 2 == 0, d);
        }
        Check(watch.ElapsedMilliseconds < 2000, "permadeath: twenty thousand pages are drafted in under two seconds (" + watch.ElapsedMilliseconds + " ms)");
    }

    // Every key a page can have is in every language, with no name in it that the text does not fill.
    private static void EndingsWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("permadeath", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        var filled = new HashSet<string>(new[] { "name", "age", "years", "place", "worth", "founded", "owned", "businesses", "employees", "properties", "friends", "close",
            "partner", "children", "count", "family", "arrests", "convictions", "followers", "elections", "mourners" });
        var keys = new HashSet<string>();
        var stories = new List<LifeStory> { Sample(), new LifeStory { Name = "Ash", Cause = DeathCause.Illness } };
        foreach (DeathCause cause in Enum.GetValues(typeof(DeathCause)))
        {
            LifeStory one = Sample();
            one.Cause = cause;
            stories.Add(one);
            stories.Add(new LifeStory { Name = "Bo", Cause = cause, Founded = 1, Owned = 1, Employees = 1, Properties = 1, Worth = -5, Partner = "Cy", Bond = 1, Friends = 1, Arrests = 1, Followers = 1, Elections = 1 });
            stories.Add(new LifeStory { Name = "Cy", Cause = cause, Partner = "Di", Bond = 2, Convictions = 3 });
        }
        foreach (LifeStory story in stories)
        {
            PermadeathStory.Draft(story, false, DraftScratch);
            foreach (ObitLine line in DraftScratch) keys.Add(line.Key);
            PermadeathStory.Draft(story, true, DraftScratch);
            foreach (ObitLine line in DraftScratch) keys.Add(line.Key);
        }
        bool present = true, names = true;
        var differ = new List<string>();
        var missing = new List<string>();
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string e) || e.Trim().Length == 0)
            {
                present = false;
                missing.Add(key);
                continue;
            }
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                differ.Add(key + " (" + lacking + ")");
            }
            foreach (Match m in Regex.Matches(e, @"\{([a-z_]+)\}"))
            {
                names &= filled.Contains(m.Groups[1].Value);
            }
        }
        Check(keys.Count > 60 && present, "permadeath: every sentence a page can have is in en.json (" + keys.Count + " keys" + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : string.Empty) + ")");
        Check(names, "permadeath: every name in the obituary's sentences is one the page fills in");
        Check(differ.Count == 0, "permadeath: every language has every sentence, with the same names as the English (" + string.Join(", ", differ) + ")");
        foreach (string key in new[] { "reallife_pd_n_year_many", "reallife_pd_n_day_many", "reallife_pd_n_friend_many", "reallife_pd_n_time_many" })
        {
            Check(en.TryGetValue(key, out string t) && t.Contains("{count}") && LanguagesLacking(langs, codes, key).Length == 0, "permadeath: " + key + " says its count, in every language");
        }
        foreach (string key in new[] { "reallife_pd_ill_1", "reallife_pd_ill_2", "reallife_pd_ill_3", "reallife_pd_near_crash", "reallife_pd_near_struck", "reallife_pd_near_mugging",
                     "reallife_pd_near_assault", "reallife_pd_mugged_body", "reallife_opt_tip_permadeath" })
        {
            Check(LanguagesLacking(langs, codes, key).Length == 0, "permadeath: the warning " + key + " is in every language");
        }
        Check(en.TryGetValue("reallife_pd_mugged_body", out string body) && en.TryGetValue("reallife_crime_mugged_body", out string plain) && body.StartsWith(plain, StringComparison.Ordinal) && body.Length > plain.Length,
            "permadeath: the mugger's demand with Permadeath on is the usual one and the warning after it");
    }

    private static Dictionary<string, string> LoadWords(string path)
    {
        return System.IO.File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(path).TrimStart('﻿'))
            : null;
    }
}
