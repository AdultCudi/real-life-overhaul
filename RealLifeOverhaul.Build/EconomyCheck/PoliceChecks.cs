using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Tech;
using Verdict = RealLifeOverhaul.Permadeath.Verdict;
using JusticeLaw = RealLifeOverhaul.JusticeLaw;
using ChargeDef = RealLifeOverhaul.ChargeDef;

// The police career (Scripts/Careers/Police): who may join (a clean record over a stated time, no conviction, a licence, health), the outcome table of every
// move across the ranks and the dangers (each row adding to a thousand, a routine incident never hurting anyone, a better rank or skill never worse, a
// worse danger never better), the roll from the save's seed, injuries only where the table allows and death only where Permadeath's own rules allow it,
// the rating and what it adds to a review, the pay on the public scale, the dismissal and the traffic stop's courtesy, the officer's file in the save, and
// the sources (no frame work, the uniform off on every way out, every bridge, the same door for the injury and the death) and the texts.
internal static partial class Program
{
    private static readonly DutyMove[] PoliceMoves = { DutyMove.TalkDown, DutyMove.Arrest, DutyMove.Backup, DutyMove.FirstAid, DutyMove.Ticket };

    private static void Police()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        PoliceEntry();
        PoliceOdds();
        PoliceRolls();
        PoliceHurt();
        PoliceDeath();
        PoliceRating();
        PoliceLadder();
        PoliceLeaving();
        PoliceFile();
        PoliceCases();
        PoliceIncidents();
        PoliceSources();
        PoliceWords();
        Console.WriteLine("  Police career: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static PoliceRules.Conduct Fine() => new PoliceRules.Conduct { SinceOffence = -1, Licence = true, Sound = true, Fit = true };

    private static void PoliceEntry()
    {
        int days;
        Check(PoliceRules.EntryWhy(Fine(), 365, out days) == null && days == PoliceRules.CleanDays, "police entry: a clean record, a licence and good health may join (" + days + " days in a 365-day year)");
        PoliceRules.EntryWhy(Fine(), 60, out int game);
        Check(game == TalentCatalogue.Scaled(PoliceRules.CleanDays, 60) && game >= 25 && game <= 35, "police entry: the stated time is scaled to the game's year of 60 days (" + game + ")");
        PoliceRules.Conduct c = Fine();
        c.SinceOffence = game - 1;
        Check(PoliceRules.EntryWhy(c, 60, out _) == "reallife_police_why_record", "police entry: an offence (a ticket counts) more recently than the stated time is refused");
        c.SinceOffence = game;
        Check(PoliceRules.EntryWhy(c, 60, out _) == null, "police entry: the day the time is up the record is clean enough");
        c.SinceOffence = 0;
        Check(PoliceRules.EntryWhy(c, 60, out _) == "reallife_police_why_record", "police entry: an offence today is refused");
        c = Fine();
        c.Serious = true;
        Check(PoliceRules.EntryWhy(c, 60, out _) == "reallife_police_why_record", "police entry: a stay in a cell or a warrant is refused whenever it was");
        c = Fine();
        c.Convicted = true;
        Check(PoliceRules.EntryWhy(c, 60, out _) == "reallife_police_why_conviction", "police entry: a conviction is refused, and said before the record");
        c = Fine();
        c.Licence = false;
        Check(PoliceRules.EntryWhy(c, 60, out _) == "reallife_police_why_licence", "police entry: a driving licence not in good standing is refused");
        c = Fine();
        c.Sound = false;
        Check(PoliceRules.EntryWhy(c, 60, out _) == "reallife_police_why_unfit", "police entry: someone seriously ill or hurt is refused");
        c = Fine();
        c.Fit = false;
        Check(PoliceRules.EntryWhy(c, 60, out _) == null, "police entry: a cold does not bar anyone from joining");
        Check(PoliceRules.ShiftWhy(c) == "reallife_police_why_shift_unfit" && PoliceRules.ShiftWhy(Fine()) == null, "police shift: anyone off sick is not sent out, and anyone well is");
        c = Fine();
        c.Licence = false;
        Check(PoliceRules.ShiftWhy(c) == "reallife_police_why_shift_licence", "police shift: no patrol without a licence in good standing");
        Check(PoliceRules.EntryRank == 0 && ProCatalogue.Find(ProCatalogue.Police).Ranks[0].Quals.Length == 1
              && ProCatalogue.Find(ProCatalogue.Police).Ranks[0].Quals[0] == StudyCatalogue.PoliceAcademy && StudyCatalogue.Find(StudyCatalogue.PoliceAcademy) != null,
            "police entry: the cadet rung's exam is the academy's course, an ordinary one of the table");
        CourseDef academy = StudyCatalogue.Find(StudyCatalogue.PoliceAcademy);
        Check(academy.Kind == CourseKind.Vocational && academy.Campus == Campus.School && academy.SkillA == Skills.Guarding && academy.SkillB == Skills.FirstAid && academy.Fee > 0,
            "police entry: the academy is a vocational course at the school that teaches guarding and first aid");
    }

    private static void PoliceOdds()
    {
        bool sums = true, nonNegative = true, routineSafe = true, rankHelps = true, skillHelps = true, dangerHurts = true, bounds = true, backOff = true;
        int rows = 0;
        foreach (DutyMove move in PoliceMoves)
        {
            for (int danger = 0; danger < PoliceRules.Dangers; danger++)
            {
                for (int rank = 0; rank < 5; rank++)
                {
                    for (int points = 0; points <= 100; points += 5)
                    {
                        PoliceRules.Odds(move, danger, rank, points, out int s, out int h, out int f);
                        rows++;
                        sums &= s + h + f == 1000;
                        nonNegative &= s >= 0 && h >= 0 && f >= 0;
                        routineSafe &= danger != PoliceRules.Routine || h == 0;
                        bounds &= s <= 1000 && (move == DutyMove.Backup || move == DutyMove.Ticket || s <= PoliceRules.OddsMost);
                        if (rank < 4)
                        {
                            PoliceRules.Odds(move, danger, rank + 1, points, out int s2, out int h2, out _);
                            rankHelps &= s2 >= s && h2 <= h;
                        }
                        if (points < 100)
                        {
                            PoliceRules.Odds(move, danger, rank, points + 5, out int s3, out int h3, out _);
                            skillHelps &= s3 >= s && h3 <= h;
                        }
                        if (danger < PoliceRules.Deadly)
                        {
                            PoliceRules.Odds(move, danger + 1, rank, points, out int s4, out int h4, out _);
                            dangerHurts &= s4 <= s && h4 >= h;
                        }
                    }
                }
            }
        }
        Check(rows == PoliceMoves.Length * 4 * 5 * 21, "police odds: every move at every danger, rank and skill is a row (" + rows + ")");
        Check(sums, "police odds: every row of the outcome table adds to exactly one (a thousand thousandths)");
        Check(nonNegative, "police odds: no outcome has a negative chance");
        Check(routineSafe, "police odds: a routine incident never hurts the officer, whatever the move");
        Check(rankHelps, "police odds: a higher rank is never worse, in the chance it works or the risk of injury");
        Check(skillHelps, "police odds: more points of the move's skill are never worse");
        Check(dangerHurts, "police odds: a worse danger is never better, in the chance it works or the risk of injury");
        Check(bounds, "police odds: nothing that is a fight is ever certain (at most " + PoliceRules.OddsMost / 10 + "%)");
        PoliceRules.Odds(DutyMove.BackOff, PoliceRules.Deadly, 4, 100, out int bs, out int bh, out int bf);
        backOff &= bs == 0 && bh == 0 && bf == 1000;
        Check(backOff, "police odds: backing off is no move: it settles nothing and hurts nobody");
        PoliceRules.Odds(DutyMove.Backup, PoliceRules.Deadly, 0, 0, out int ks, out int kh, out _);
        PoliceRules.Odds(DutyMove.Arrest, PoliceRules.Deadly, 0, 0, out int as_, out int ah, out _);
        Check(kh < ah && ks > as_, "police odds: calling for backup at a deadly incident is safer and surer than an arrest by a new cadet (" + kh + " against " + ah + ")");
        PoliceRules.Odds(DutyMove.Arrest, PoliceRules.Tense, 2, 60, out int s1, out int h1, out _);
        PoliceRules.Odds(DutyMove.TalkDown, PoliceRules.Tense, 2, 60, out int s2x, out int h2x, out _);
        Check(h1 > h2x && s1 < s2x, "police odds: an arrest is harder and likelier to hurt than a talk at the same skill");
        Check(PoliceRules.Percent(666) == 67 && PoliceRules.Percent(1000) == 100 && PoliceRules.Percent(0) == 0, "police odds: a chance is shown to the whole percent");
        Check(PoliceRules.CanHurt(DutyMove.Arrest, PoliceRules.Tense) && !PoliceRules.CanHurt(DutyMove.Arrest, PoliceRules.Routine) && !PoliceRules.CanHurt(DutyMove.Backup, PoliceRules.Tense)
              && PoliceRules.CanHurt(DutyMove.Backup, PoliceRules.Risky) && !PoliceRules.CanHurt(DutyMove.BackOff, PoliceRules.Deadly),
            "police odds: only a tense incident's arrest and a risky one's backup can hurt, never a routine one, never backing off");
    }

    private static void PoliceRolls()
    {
        int a = PoliceRules.Roll(7, 12, 3, 1), b = PoliceRules.Roll(7, 12, 3, 1);
        Check(a == b && a >= 0 && a < 1000, "police roll: the same seed, day, incident and rank give the same roll (" + a + "), within a thousand");
        bool spread = false;
        for (int n = 0; n < 50 && !spread; n++)
        {
            spread = PoliceRules.Roll(7, 12, n, 1) != a;
        }
        Check(spread && PoliceRules.Roll(8, 12, 3, 1) >= 0, "police roll: another incident number gives another roll");
        // The frequency of each outcome over many seeds is the table's, within a point or two.
        PoliceRules.Odds(DutyMove.Arrest, PoliceRules.Risky, 2, 50, out int s, out int h, out int f);
        int ok = 0, hurt = 0, fail = 0, n2 = 30000;
        for (int i = 0; i < n2; i++)
        {
            DutyResult r = PoliceRules.Result(DutyMove.Arrest, PoliceRules.Risky, 2, 50, PoliceRules.Roll(1 + i / 100, 1 + i % 50, i, 2));
            ok += r == DutyResult.Success ? 1 : 0;
            hurt += r == DutyResult.Hurt ? 1 : 0;
            fail += r == DutyResult.Failed ? 1 : 0;
        }
        Check(ok + hurt + fail == n2 && Math.Abs(ok * 1000 / n2 - s) <= 25 && Math.Abs(hurt * 1000 / n2 - h) <= 25 && Math.Abs(fail * 1000 / n2 - f) <= 25,
            "police roll: over thirty thousand rolls each outcome comes as often as the table says (" + ok * 1000 / n2 + "/" + s + ", " + hurt * 1000 / n2 + "/" + h + ", " + fail * 1000 / n2 + "/" + f + ")");
        Check(PoliceRules.Result(DutyMove.Arrest, PoliceRules.Tense, 2, 60, 0) == DutyResult.Success && PoliceRules.Result(DutyMove.Arrest, PoliceRules.Tense, 2, 60, 999) != DutyResult.Success,
            "police roll: the lowest roll is a success and the highest never is");
        PoliceRules.Odds(DutyMove.Arrest, PoliceRules.Tense, 2, 60, out int s5, out int h5, out _);
        Check(PoliceRules.Result(DutyMove.Arrest, PoliceRules.Tense, 2, 60, s5 - 1) == DutyResult.Success && PoliceRules.Result(DutyMove.Arrest, PoliceRules.Tense, 2, 60, s5) == DutyResult.Hurt
              && PoliceRules.Result(DutyMove.Arrest, PoliceRules.Tense, 2, 60, s5 + h5) == DutyResult.Failed, "police roll: the table's rows are read in order, success then injury then failure");
    }

    private static void PoliceHurt()
    {
        Check(PoliceRules.HurtHours(DutyResult.Success, PoliceRules.Deadly) == 0 && PoliceRules.HurtHours(DutyResult.Failed, PoliceRules.Deadly) == 0, "police injury: only an injury outcome leaves any hours of injury");
        int bruise = PoliceRules.HurtHours(DutyResult.Hurt, PoliceRules.Tense), sprain = PoliceRules.HurtHours(DutyResult.Hurt, PoliceRules.Risky), major = PoliceRules.HurtHours(DutyResult.Hurt, PoliceRules.Deadly);
        Check(bruise > 0 && bruise < RealLifeOverhaul.Health.HealthCatalogue.MinorHurtHours && sprain >= RealLifeOverhaul.Health.HealthCatalogue.MinorHurtHours && sprain < RealLifeOverhaul.Health.HealthCatalogue.MajorHurtHours
              && major >= RealLifeOverhaul.Health.HealthCatalogue.MajorHurtHours,
            "police injury: a tense incident leaves a bruise, a risky one a sprain and a deadly one a broken arm or a concussion, by Health's own hours (" + bruise + ", " + sprain + ", " + major + ")");
        Check(PoliceRules.HurtHours(DutyResult.Hurt, PoliceRules.Routine) == 0, "police injury: a routine incident leaves no hours, whatever came of it");
        Check(RealLifeOverhaul.Health.HealthRules.InjuryOf(5, bruise, 0) == RealLifeOverhaul.Health.Condition.None && RealLifeOverhaul.Health.HealthRules.InjuryOf(5, sprain, 0) == RealLifeOverhaul.Health.Condition.Sprain
              && (RealLifeOverhaul.Health.HealthRules.InjuryOf(5, major, 0) == RealLifeOverhaul.Health.Condition.BrokenArm || RealLifeOverhaul.Health.HealthRules.InjuryOf(5, major, 0) == RealLifeOverhaul.Health.Condition.Concussion),
            "police injury: Health's own rule names them: nothing for a bruise, a sprain, a broken arm or a concussion");
        for (int d = 1; d < PoliceRules.Dangers; d++)
        {
            Check(PoliceRules.InjuryHours[d] >= PoliceRules.InjuryHours[d - 1], "police injury: a worse danger never hurts for fewer hours (danger " + d + ")");
        }
        Check(PoliceRules.InjuryEnergy > 0f && PoliceRules.InjuryEnergy <= 15f, "police injury: the energy an injury costs is small");
    }

    private static void PoliceDeath()
    {
        bool onlyDeadly = true, warnsFirst = true, allowedOnly = true, neverOff = true, deterministic = true;
        for (int bits = 0; bits < 32; bits++)
        {
            var m = new Moment { Armed = (bits & 1) != 0, Dead = (bits & 2) != 0, InSession = (bits & 4) != 0, MissionOrTutorial = (bits & 8) != 0, Busy = (bits & 16) != 0 };
            bool allowed = PermadeathRules.Allowed(m);
            for (int danger = 0; danger < PoliceRules.Dangers; danger++)
            {
                float measure = PoliceRules.FatalMeasure(danger);
                foreach (bool warned in new[] { false, true })
                {
                    foreach (float roll in new[] { 0f, 0.03f, PermadeathRules.DutyOdds - 0.001f, PermadeathRules.DutyOdds, 0.5f, 0.999f })
                    {
                        Verdict v = PermadeathRules.Judge(DeathCause.Duty, measure, warned, m, roll);
                        onlyDeadly &= v == Verdict.None || danger >= PoliceRules.FatalFrom;
                        allowedOnly &= v == Verdict.None || allowed;
                        neverOff &= allowed || v == Verdict.None;
                        warnsFirst &= !(v == Verdict.Death && !warned);
                        deterministic &= v == PermadeathRules.Judge(DeathCause.Duty, measure, warned, m, roll);
                        if (allowed && danger >= PoliceRules.FatalFrom)
                        {
                            Verdict want = !warned ? Verdict.NearMiss : roll < PermadeathRules.DutyOdds ? Verdict.Death : Verdict.None;
                            warnsFirst &= v == want;
                        }
                    }
                }
            }
        }
        Check(onlyDeadly, "police death: only a deadly incident can end a life, never a routine, tense or risky one");
        Check(allowedOnly && neverOff, "police death: nothing happens where Permadeath is off, the character dead, a Going Public session, the tutorial or a mission, or the game busy: the same 32 moments as every danger");
        Check(warnsFirst, "police death: the first deadly injury is a near miss, never a death, and after it a death is the roll under the odds");
        Check(deterministic, "police death: the same moment and the same roll always come to the same");
        Check(PermadeathRules.DutyOdds > 0f && PermadeathRules.DutyOdds <= PermadeathRules.MuggingOdds && PermadeathRules.Odds(DeathCause.Duty, PoliceRules.Deadly) == PermadeathRules.DutyOdds
              && PermadeathRules.Odds(DeathCause.Duty, PoliceRules.Risky) == 0f, "police death: it is rarer than a mugging's and zero below a deadly incident (" + PermadeathRules.DutyOdds + ")");
        Check(PermadeathRules.DutyFrom == PoliceRules.FatalFrom, "police death: Permadeath's threshold and the rules' deadly danger are the same number");
        var story = new LifeStory { Name = "Ash", Age = 30, Day = 40, Cause = DeathCause.Duty };
        LifeStory back = new LifeStory();
        Check(back.Read(story.Write()) && back.Cause == DeathCause.Duty && PermadeathStory.Word(DeathCause.Duty) == "duty", "police death: a death on duty is kept in the life's line and read back as one");
    }

    private static void PoliceRating()
    {
        Check(PoliceRules.RatingStart == 60 && PoliceRules.Clamp(-5) == 0 && PoliceRules.Clamp(140) == 100, "police rating: a quiet shift starts at 60 and the rating stays between 0 and 100");
        bool backOffCosts = true, backOffEases = true;
        int last = int.MinValue;
        for (int d = 0; d < PoliceRules.Dangers; d++)
        {
            int c = PoliceRules.Change(DutyMove.BackOff, DutyResult.Failed, d);
            backOffCosts &= c < 0 || (d == PoliceRules.Deadly && c <= 0);
            backOffEases &= c >= last;
            last = c;
        }
        Check(backOffCosts && backOffEases && PoliceRules.Change(DutyMove.BackOff, DutyResult.Failed, PoliceRules.Routine) == -PoliceRules.LossBackOff,
            "police rating: backing off costs the rating, most at a routine incident and nearly nothing at a deadly one");
        Check(PoliceRules.Change(DutyMove.Arrest, DutyResult.Success, PoliceRules.Deadly) > PoliceRules.Change(DutyMove.Arrest, DutyResult.Success, PoliceRules.Routine)
              && PoliceRules.Change(DutyMove.Arrest, DutyResult.Failed, PoliceRules.Tense) < 0 && PoliceRules.Change(DutyMove.Arrest, DutyResult.Hurt, PoliceRules.Deadly) < 0
              && PoliceRules.Change(DutyMove.Arrest, DutyResult.Hurt, PoliceRules.Deadly) > PoliceRules.Change(DutyMove.Arrest, DutyResult.Failed, PoliceRules.Tense),
            "police rating: a harder success earns more, a failure costs and an injury costs less than a failure (it is not the officer's fault)");
        Check(PoliceRules.Change(DutyMove.Backup, DutyResult.Success, PoliceRules.Risky) > PoliceRules.Change(DutyMove.Backup, DutyResult.Success, PoliceRules.Routine)
              && PoliceRules.Change(DutyMove.Backup, DutyResult.Success, PoliceRules.Risky) < PoliceRules.Change(DutyMove.Arrest, DutyResult.Success, PoliceRules.Risky),
            "police rating: backup is sound at a dangerous call and earns less than settling it oneself");
        Check(PoliceRules.Commended(DutyMove.Arrest, DutyResult.Success, PoliceRules.Risky) && !PoliceRules.Commended(DutyMove.Arrest, DutyResult.Success, PoliceRules.Tense)
              && !PoliceRules.Commended(DutyMove.Backup, DutyResult.Success, PoliceRules.Deadly) && !PoliceRules.Commended(DutyMove.Arrest, DutyResult.Failed, PoliceRules.Deadly)
              && PoliceRules.ShiftCommended(PoliceRules.CommendRating, 1) && !PoliceRules.ShiftCommended(PoliceRules.CommendRating, 0) && !PoliceRules.ShiftCommended(PoliceRules.CommendRating - 1, 5),
            "police rating: a commendation for a risky or deadly settled incident (never backup) and for a shift ended at 90 with something answered");
        Check(PoliceRules.ReviewBonus(null, 0) == 0 && PoliceRules.ReviewBonus(new[] { 60, 60, 60 }, 3) == 0 && PoliceRules.ReviewBonus(new[] { 100, 100, 100 }, 3) > 0
              && PoliceRules.ReviewBonus(new[] { 0, 0, 0 }, 3) < 0 && Math.Abs(PoliceRules.ReviewBonus(new[] { 100, 100 }, 2)) <= PoliceRules.ReviewBonusMost
              && Math.Abs(PoliceRules.ReviewBonus(new[] { 0, 0 }, 2)) <= PoliceRules.ReviewBonusMost, "police rating: the last shifts' mean moves a review's score either way, and never by more than " + PoliceRules.ReviewBonusMost);
        Check(PoliceRules.Blend(80, 40) == 60 && PoliceRules.Blend(100, 100) == 100 && PoliceRules.Blend(0, 0) == 0, "police rating: a recorded shift's score is the job's own and the shift's rating, evenly");
        // A quiet shift: nothing answered leaves the rating where it began and the day counts as worked by the careers' own rule.
        var file = new DutyFile();
        file.Begin(10);
        Check(file.Rating == PoliceRules.RatingStart && file.Today == 0 && !PoliceRules.ShiftCommended(file.Rating, file.Today), "police rating: a quiet shift keeps the starting rating and earns nothing");
        Job quiet = PoliceJob(0);
        ProRules.Worked(quiet, 3, ProCatalogue.Find(ProCatalogue.Police).Hours * 60);
        Check(ProRules.Settle(quiet, 3) && quiet.Worked == 1 && quiet.Absent == 0, "police rating: a quiet shift worked for its whole length counts as a day worked");
        Job part = PoliceJob(0);
        ProRules.Worked(part, 3, ProCatalogue.Find(ProCatalogue.Police).Hours * 60 * 3 / 4);
        Check(ProRules.Settle(part, 3), "police rating: three quarters of a shift on patrol is a day worked");
        int s = file.Rating;
        file.Begin(10);
        file.Rating = PoliceRules.Clamp(file.Rating + 7);
        file.Begin(10);
        Check(file.Rating == s + 7, "police rating: the same day does not begin the shift's rating again");
        file.Begin(11);
        Check(file.Rating == PoliceRules.RatingStart, "police rating: a new day begins at the quiet shift's");
        file.Rating = 77;
        for (int i = 0; i < PoliceRules.RingSize + 3; i++)
        {
            file.Day = 100 + i;
            file.Rating = 50 + i;
            file.Close();
        }
        Check(file.Count == PoliceRules.RingSize && file.Ring[PoliceRules.RingSize - 1] == 50 + PoliceRules.RingSize + 2 && file.Ring[0] == 53, "police rating: the ring keeps the last " + PoliceRules.RingSize + " shifts, the oldest dropping off");
    }

    private static Job PoliceJob(int rank, int hired = 1)
    {
        var j = new Job { Hired = hired, SettledDay = 0, Work = (int)WorkMode.OnSite };
        new Opening { Id = 5, Prof = ProCatalogue.Police, Rank = rank, Kind = (int)EmployerKind.Public }.CopyTo(j.Post);
        return j;
    }

    private static void PoliceLadder()
    {
        Profession p = ProCatalogue.Find(ProCatalogue.Police);
        Profession nurse = ProCatalogue.Find(ProCatalogue.Nurse);
        Check(p != null && p.Id == ProCatalogue.Police && ProCatalogue.All[ProCatalogue.Police] == p && p.Public && p.Place == Workplace.Precinct && p.Types.Length == 0 && !p.Remote,
            "police ladder: police work is the public sector's, at a precinct, never from home");
        Check(p.Ranks.Length == 5 && p.Ranks[4].Exec && !p.Ranks[3].Exec, "police ladder: cadet, officer, detective, sergeant and lieutenant, the lieutenant at the top");
        bool pay = true, publicScale = true, climbs = true;
        for (int r = 0; r < p.Ranks.Length; r++)
        {
            pay &= p.Ranks[r].Pay > 0 && (r == 0 || p.Ranks[r].Pay > p.Ranks[r - 1].Pay);
            publicScale &= p.Ranks[r].Pay >= nurse.Ranks[Math.Min(r, nurse.Ranks.Length - 1)].Pay && p.Ranks[r].Pay <= nurse.Ranks[nurse.Ranks.Length - 1].Pay * 11 / 10;
            climbs &= r == 0 || (p.Ranks[r].Need > p.Ranks[r - 1].Need && p.Ranks[r].Days >= p.Ranks[r - 1].Days);
        }
        Check(pay, "police ladder: every rung pays more than the one below");
        Check(publicScale, "police ladder: the pay is on the nurses' public scale, never under a nurse's rung and never over the nurses' top by more than a tenth");
        Check(climbs, "police ladder: each rung asks more points and no fewer days than the one below");
        Check(p.Ranks[0].Pay == nurse.Ranks[0].Pay, "police ladder: a cadet starts on what a care worker starts on (" + p.Ranks[0].Pay / 100 + ")");
        Check(p.Hour + p.Hours <= 22 && p.Hours >= 6 && p.Hour == PoliceRules.Hour && p.Hours == PoliceRules.Hours && p.Days == (2 | 4 | 8 | 16 | 32),
            "police ladder: five days a week, Tuesday to Saturday, on the afternoon shift inside the day");
        Check(ProRules.Wage(p.Ranks[0].Pay, p.Hours * 60) == (long)p.Ranks[0].Pay * 8, "police pay: a whole shift pays the rank's hour eight times, by the careers' own wage");
        int paid = 0;
        for (int minutes = 0; minutes < p.Hours * 60; minutes += 60)
        {
            paid += (int)ProRules.Wage(p.Ranks[1].Pay, 60);
        }
        Check(paid == (int)ProRules.Wage(p.Ranks[1].Pay, p.Hours * 60), "police pay: banking the shift an hour at a time pays what banking it at the end would");
        Check(DeskRules.Kinds(p).Length == 0, "police desk: a patrol has no station of the game's to work at");
        Check(ProRules.PostPay(p, 0, EmployerKind.Public, 1000) < ProRules.PostPay(p, 0, EmployerKind.City, 1000), "police pay: a public post pays the public scale, a little under a business's");
        int cadet = ProCatalogue.RankOfLevel(p, TalentCatalogue.Junior), top = ProCatalogue.RankOfLevel(p, TalentCatalogue.Principal);
        Check(cadet == 0 && top == p.Ranks.Length - 1, "police ladder: the jobs board's levels map onto the ladder's ends");
        Check(!ProCatalogue.EmploysKept(p, "ba:businesstype_supermarket") && !ProCatalogue.EmploysKept(p, "ba:businesstype_bank") && !ProCatalogue.EmploysKept(p, string.Empty),
            "police ladder: no business hires police, only the precincts");
        Check(ProCatalogue.Count == ProCatalogue.All.Length && ProCatalogue.Count == 26, "police ladder: the professions are twenty-six");
        // Another week fitted to the precinct's door (which keeps no hours) is its own.
        Check(OpenHoursRules.Pack(p.Days, p.Hour, p.Hours) != 0, "police ladder: the week packs as any profession's does");
        // The state of a patrol by the careers' own panel rules, at a station (a desk's rules, the police's hours).
        Job j = PoliceJob(0);
        int m;
        bool late;
        Check(DeskRules.State(j, 1, 15 * 60, false, out m, out late) == DeskState.DayOff, "police shift: Monday is a day off");
        Check(DeskRules.State(j, 2, 12 * 60, false, out m, out late) == DeskState.NotYet && DeskRules.State(j, 2, 13 * 60 + 5, false, out m, out late) == DeskState.StartsSoon
              && DeskRules.State(j, 2, 14 * 60 + 30, false, out m, out late) == DeskState.Open && m == 450 && late, "police shift: it can be begun an hour before its start and is late after a quarter of an hour");
        Check(DeskRules.State(j, 2, 16 * 60 + 5, false, out m, out late) == DeskState.TooLate, "police shift: more than two hours late it cannot be begun");
    }

    private static void PoliceLeaving()
    {
        PoliceRules.Conduct c = Fine();
        Check(!PoliceRules.Dismissed(c), "police dismissal: a clean record since joining is kept on");
        c.SinceOffence = 2;
        Check(!PoliceRules.Dismissed(c), "police dismissal: a ticket is not a ground (an officer's minor stops end in a word)");
        c = Fine();
        c.Serious = true;
        Check(PoliceRules.Dismissed(c), "police dismissal: a stay in a cell or a warrant since joining ends the service");
        c = Fine();
        c.Convicted = true;
        Check(PoliceRules.Dismissed(c), "police dismissal: a conviction since joining ends the service");
        Check(ProRules.Reference(PoliceJob(0), 20, LeaveReason.Misconduct) == ProRules.Reference(PoliceJob(0), 20, LeaveReason.Dismissed),
            "police dismissal: it goes the careers' own way, a sacking's reference");
        Check(PoliceRules.Courtesy(true, 0, 0, 0, false) && !PoliceRules.Courtesy(false, 0, 0, 0, false) && !PoliceRules.Courtesy(true, 1, 0, 0, false)
              && !PoliceRules.Courtesy(true, 0, 1, 0, false) && !PoliceRules.Courtesy(true, 0, 0, 500, false) && !PoliceRules.Courtesy(true, 0, 0, 0, true),
            "police stop: an officer on a shift is let off with a word only for a minor stop: nobody hurt, nothing owed, no drink");
        Check(PoliceRules.Counted(1) == 1 && PoliceRules.Counted(0) == 0 && PoliceRules.Counted(PoliceRules.MostJumpMinutes) == PoliceRules.MostJumpMinutes && PoliceRules.Counted(PoliceRules.MostJumpMinutes + 1) == 0
              && PoliceRules.Counted(-5) == 0, "police clock: a look counts the minutes that passed, and none for a jump too long to have been watched");
        Check(!PoliceRules.Jumped(5) && !PoliceRules.Jumped(PoliceRules.MostJumpMinutes) && PoliceRules.Jumped(PoliceRules.MostJumpMinutes + 1) && PoliceRules.Jumped(-1),
            "police clock: a sleep, a skip or a time that went back ends the shift");
        Check(PoliceRules.MostJumpMinutes >= 10, "police clock: the game's own fastest speed still counts as patrolling (" + PoliceRules.MostJumpMinutes + " minutes a second)");
    }

    private static void PoliceFile()
    {
        var f = new DutyFile { Arrests = 4, Tickets = 3, Commendations = 2, Aid = 1, Talked = 5, Backups = 6, BackedOff = 7, Hurt = 1, Shifts = 12, Answered = 22, Day = 9, Rating = 71, Today = 3 };
        f.Rating = 71;
        f.Close();
        f.Rating = 60;
        f.Close();   // the same day again: the day's rating, not another shift's
        Check(f.Count == 1 && f.Ring[0] == 60, "police file: a shift begun again the same day is the same day's rating in the ring");
        f.Day = 10;
        f.Rating = 52;
        f.Close();
        DutyFile back = DutyStore.Read(DutyStore.Write(f), out int version);
        Check(version == DutyStore.Version && back.Arrests == 4 && back.Tickets == 3 && back.Commendations == 2 && back.Aid == 1 && back.Talked == 5 && back.Backups == 6 && back.BackedOff == 7
              && back.Hurt == 1 && back.Shifts == 12 && back.Answered == 22 && back.Day == 10 && back.RingDay == 10 && back.Rating == 52 && back.Today == 3 && back.Count == 2 && back.Ring[0] == 60 && back.Ring[1] == 52,
            "police file: every figure of the officer's file comes back from the save");
        var data = new Dictionary<string, string>();
        Check(DutyStore.Load(data, out _) == null, "police file: a save from before it has none, and reads as an officer with a clean slate");
        data[DutyStore.Key] = "RLO1\n";
        DutyFile junk = null;
        try
        {
            junk = DutyStore.Load(data, out _);
        }
        catch (Exception)
        {
            junk = null;
        }
        Check(junk == null || (junk.Arrests == 0 && junk.Count == 0), "police file: a line that says nothing reads as a clean slate, not as a fault");
        Check(DutyStore.Key == "reallife:work_duty", "police file: its own line of the save, apart from the careers' own (an older build keeps both)");
        string text = DutyStore.Write(new DutyFile());
        DutyFile blank = DutyStore.Read(text, out _);
        Check(blank.Day == -1 && blank.Rating == PoliceRules.RatingStart && blank.Count == 0, "police file: a new file reads back as new");
        // a record with more fields than this version knows, as a later version writes it, still reads
        DutyFile later = DutyStore.Read(text.Replace("\n", "|99|99\n"), out _);
        Check(later != null, "police file: fields a later version adds at the end of a record do not stop an older one reading it");
    }

    private static void PoliceIncidents()
    {
        // Every street crime of the catalogue is the officer's to answer, or is one of the three that are not (against the player, or the player's own shop).
        string catalogue = PoliceSource("Crime/CrimeCatalogue.cs");
        var ids = new List<string>();
        foreach (Match m in Regex.Matches(catalogue, "new CrimeEntry\\(\"([a-z_]+)\""))
        {
            ids.Add(m.Groups[1].Value);
        }
        Check(ids.Count >= 20, "police incidents: the catalogue's crimes are found (" + ids.Count + ")");
        string[] notTheirs = { "shop_theft_own", "player_pickpocket", "player_mugging" };
        bool all = true;
        string which = null;
        foreach (string id in ids)
        {
            bool theirs = PoliceRules.Crime(id, out DutyKind kind, out int danger);
            bool excluded = Array.IndexOf(notTheirs, id) >= 0;
            bool ok = theirs != excluded && (!theirs || (danger >= 0 && danger < PoliceRules.Dangers && PoliceRules.Moves(kind).Length >= 2));
            if (!ok)
            {
                all = false;
                which = id;
            }
        }
        Check(all, "police incidents: every crime is either answered with two or more moves at a danger, or is one against the player or in their shop" + (which != null ? " (" + which + ")" : string.Empty));
        Check(!PoliceRules.Crime("nonsense", out _, out _) && !PoliceRules.Crime(null, out _, out _), "police incidents: a crime the rules do not know is not offered");
        PoliceRules.Crime("holdup", out DutyKind hk, out int hd);
        PoliceRules.Crime("parking_ticket", out DutyKind pk, out int pd);
        Check(hd == PoliceRules.Deadly && hk == DutyKind.Fight && pd == PoliceRules.Routine && pk == DutyKind.Nuisance, "police incidents: a hold-up is deadly and a parked car routine");
        bool moves = true, backOff = true;
        foreach (DutyKind kind in Enum.GetValues(typeof(DutyKind)))
        {
            DutyMove[] list = PoliceRules.Moves(kind);
            moves &= list.Length >= 2 && list.Length <= 4 && Array.IndexOf(list, DutyMove.Backup) >= 0;
            backOff &= Array.IndexOf(list, DutyMove.BackOff) < 0;
            foreach (DutyMove move in list)
            {
                moves &= (int)move >= 0 && (int)move < PoliceRules.MoveKeys.Length;
            }
        }
        Check(moves, "police incidents: every kind offers two to four moves, backup always among them");
        Check(backOff, "police incidents: backing off is never one of the moves: the list adds it itself, always");
        Check(Array.IndexOf(PoliceRules.Moves(DutyKind.Casualty), DutyMove.FirstAid) == 0 && Array.IndexOf(PoliceRules.Moves(DutyKind.Casualty), DutyMove.Arrest) < 0
              && Array.IndexOf(PoliceRules.Moves(DutyKind.Fight), DutyMove.FirstAid) < 0 && Array.IndexOf(PoliceRules.Moves(DutyKind.Accident), DutyMove.Ticket) >= 0,
            "police incidents: first aid goes to a person down and to a crash, an arrest to a theft or a fight, a ticket to trouble and a crash");
        Check(PoliceRules.SkillOf(DutyMove.TalkDown) == TalentCatalogue.Relationships && PoliceRules.SkillOf(DutyMove.Arrest) == Skills.Guarding && PoliceRules.SkillOf(DutyMove.FirstAid) == Skills.FirstAid
              && PoliceRules.SkillOf(DutyMove.Backup) < 0 && PoliceRules.SkillOf(DutyMove.Ticket) < 0, "police incidents: talking rides on relationships, arresting on guarding and first aid on first aid");
        Check(PoliceRules.AccidentDanger(0) == PoliceRules.Routine && PoliceRules.AccidentDanger(2) == PoliceRules.Risky && PoliceRules.AccidentDanger(9) == PoliceRules.Risky
              && PoliceRules.CasualtyDanger == PoliceRules.Tense, "police incidents: a slight crash is routine, a severe one risky, a person down tense");
    }

    private static DutyFile CaseFile() => new DutyFile();

    private static void PoliceCases()
    {
        bool sums = true, nonNegative = true, strongerConvicts = true, witnessHelps = true;
        for (int w = -6; w <= 8; w += 2)
        {
            int lastConv = -1;
            for (int s = 0; s <= 100; s++)
            {
                CaseRules.Odds(s, w, out int a, out int p, out int c);
                sums &= a + p + c == 1000;
                nonNegative &= a >= 0 && p >= 0 && c >= 0 && a >= CaseRules.AcquitLeast && a <= CaseRules.AcquitMost;
                strongerConvicts &= a + p + c != 1000 || p + c >= lastConv;
                lastConv = p + c;
                if (w < 8)
                {
                    CaseRules.Odds(s, w + 2, out int a2, out _, out _);
                    witnessHelps &= a2 <= a;
                }
            }
        }
        Check(sums, "police cases: every hearing's odds (acquitted, pleaded, convicted) add to exactly one, at every strength and every weight of evidence");
        Check(nonNegative && strongerConvicts && witnessHelps, "police cases: no chance is negative, a stronger case never convicts less, and better evidence never acquits more");
        bool charge = true;
        int last = -1;
        for (int q = 0; q <= 100; q++)
        {
            int cc = CaseRules.ChargeChance(q);
            charge &= cc >= last && cc >= CaseRules.ChargeBase && cc <= 1000;
            last = cc;
        }
        Check(charge && CaseRules.ChargeChance(100) < 1000, "police cases: a better arrest is never less likely to be charged, and none is certain");
        Check(CaseRules.Quality(0, 0, 0) == CaseRules.QualityBase && CaseRules.Quality(900, 4, 100) > CaseRules.Quality(100, 1, 20) && CaseRules.Quality(9999, 9, 100) == 100
              && CaseRules.Good(CaseRules.GoodAt) && !CaseRules.Good(CaseRules.GoodAt - 1) && CaseRules.Poor(CaseRules.PoorAt - 1) && !CaseRules.Poor(CaseRules.PoorAt) && CaseRules.PoorAt < CaseRules.GoodAt,
            "police cases: the arrest's quality grows with the margin, the rank and the skill, and is good from " + CaseRules.GoodAt + " and poor under " + CaseRules.PoorAt);
        // the court's own timings and words
        Check(CaseRules.HearingDay(10, JusticeLaw.Theft) == 12 && CaseRules.HearingDay(10, JusticeLaw.Assault) == 13 && CaseRules.HearingDay(10, "nonsense") == 12,
            "police cases: a case is heard in the days Justice's court takes for the offence's class (two for a theft, three for an assault)");
        bool offences = true;
        foreach (string id in new[] { "mugging", "holdup", "assault", "disturbance", "graffiti", "foot_chase", "snatch", "shoplifter", "car_breakin", "car_theft", "drug_deal", "nonsense", null })
        {
            ChargeDef def = JusticeLaw.Find(CaseRules.Offence(id));
            offences &= def != null && def.NameKey.StartsWith("reallife_justice_charge_", StringComparison.Ordinal);
        }
        Check(offences, "police cases: every street crime's offence is a charge of Justice's own table, worded by its own text");
        Check(CaseRules.Sentence(JusticeLaw.Assault, CaseEnd.Acquitted) == 0 && CaseRules.Sentence(JusticeLaw.Assault, CaseEnd.Released) == 0
              && CaseRules.Sentence(JusticeLaw.Assault, CaseEnd.Convicted) == JusticeLaw.Find(JusticeLaw.Assault).PrisonDays
              && CaseRules.Sentence(JusticeLaw.Assault, CaseEnd.Pleaded) <= CaseRules.Sentence(JusticeLaw.Assault, CaseEnd.Convicted),
            "police cases: a sentence is Justice's prison days for the charge, less for a plea, and none for anyone not convicted");

        // a case ends exactly once, and only on a day after its hearing
        DutyFile f = CaseFile();
        OfficerCase charged = null;
        for (int id = 0; id < 400 && charged == null; id++)
        {
            OfficerCase c = CaseRules.Open(f, "Ash Lee", 7, JusticeLaw.Assault, "ba:neighborhood_x", 10, 80, 11);
            if (c.Open)
            {
                charged = c;
            }
        }
        Check(charged != null && charged.Hearing == 13 && charged.End == CaseEnd.Open && f.PendOpened >= 1, "police cases: a charged case waits for its hearing day");
        Check(CaseRules.Advance(f, 13, 11) == 0 && charged.Open, "police cases: on the hearing day itself it is not yet decided (the evidence is given that day)");
        int ended = CaseRules.Advance(f, 14, 11);
        CaseEnd first = charged.End;
        int convictedBefore = f.PendConvicted, acquittedBefore = f.PendAcquitted;
        Check(ended >= 1 && first != CaseEnd.Open && first != CaseEnd.Released && charged.EndDay == 14, "police cases: the day after the hearing the case ends, acquitted, convicted or pleaded");
        Check(CaseRules.Advance(f, 15, 11) == 0 && CaseRules.Advance(f, 30, 11) == 0 && charged.End == first && f.PendConvicted == convictedBefore && f.PendAcquitted == acquittedBefore,
            "police cases: a case ends exactly once: later days change nothing and count nothing");
        for (int i = 0; i < 30; i++)
        {
            CaseRules.Open(f, "Dee", 0, JusticeLaw.Theft, string.Empty, 10, 0, 100 + i);
        }
        bool released = false, hearingless = true;
        foreach (OfficerCase c in f.Cases)
        {
            if (c.End == CaseEnd.Released)
            {
                released = true;
                hearingless &= c.Hearing == 0 && c.EndDay == c.Day;
            }
        }
        Check(released && hearingless, "police cases: a case never charged is released the day of the arrest, with no hearing");
        // determinism and the frequency of the table
        var a1 = new OfficerCase { Id = 5, Hearing = 12, Strength = 55, Witness = 2 };
        Check(CaseRules.Decide(9, a1) == CaseRules.Decide(9, a1) && CaseRules.Decide(9, a1) != CaseEnd.Open && CaseRules.Decide(9, a1) != CaseEnd.Released, "police cases: the same case and seed always end the same way");
        int acq = 0, plea = 0, conv = 0, n = 20000;
        for (int i = 0; i < n; i++)
        {
            switch (CaseRules.Decide(3, new OfficerCase { Id = i, Hearing = 10 + i % 7, Strength = 50, Witness = 0 }))
            {
                case CaseEnd.Acquitted: acq++; break;
                case CaseEnd.Pleaded: plea++; break;
                default: conv++; break;
            }
        }
        CaseRules.Odds(50, 0, out int oa, out int op, out int oc);
        Check(Math.Abs(acq * 1000 / n - oa) <= 25 && Math.Abs(plea * 1000 / n - op) <= 25 && Math.Abs(conv * 1000 / n - oc) <= 25, "police cases: over twenty thousand hearings each end comes as often as the table says");

        // one notice a day, the news summed
        DutyFile g = CaseFile();
        for (int i = 0; i < 6; i++)
        {
            CaseRules.Open(g, "Bo " + i, 0, JusticeLaw.Theft, string.Empty, 20, 90, 5);
        }
        Check(CaseRules.NoticeDue(g, 20) && g.PendOpened == 6, "police cases: the day's news is due as one notice");
        CaseRules.TakeNotice(g, 20, out int op1, out int rel1, out int cv1, out int aq1, out int cm1);
        Check(op1 == 6 && rel1 + CaseRules.OpenCount(g) == 6 && g.PendOpened == 0 && !CaseRules.NoticeDue(g, 20), "police cases: the notice sums every case of the day and clears the count (" + op1 + " opened)");
        CaseRules.Open(g, "Cy", 0, JusticeLaw.Theft, string.Empty, 20, 90, 6);
        Check(!CaseRules.NoticeDue(g, 20) && CaseRules.NoticeDue(g, 21), "police cases: never a second case notice in a day: it waits for the next");
        CaseRules.Advance(g, 40, 5);
        CaseRules.TakeNotice(g, 40, out _, out _, out int cv2, out int aq2, out _);
        Check(CaseRules.OpenCount(g) == 0 && cv2 + aq2 == g.Cases.FindAll(c => c.End == CaseEnd.Convicted || c.End == CaseEnd.Pleaded || c.End == CaseEnd.Acquitted).Count,
            "police cases: every charged case that ended in the days is told, once, in the next notice");

        // commendations and the review's cost
        DutyFile h = CaseFile();
        h.Cases.Add(new OfficerCase { Id = 1, Offence = JusticeLaw.Theft, Hearing = 5, Quality = 80, Strength = 90 });
        h.Cases.Add(new OfficerCase { Id = 2, Offence = JusticeLaw.Theft, Hearing = 5, Quality = 20, Strength = 90 });
        CaseRules.Advance(h, 9, 1);
        int fromGood = 0;
        foreach (OfficerCase c in h.Cases)
        {
            fromGood += (c.End == CaseEnd.Convicted || c.End == CaseEnd.Pleaded) && CaseRules.Good(c.Quality) ? 1 : 0;
        }
        Check(h.Commendations == fromGood && h.PendCommended == fromGood && fromGood <= 1, "police cases: only a conviction after a good arrest earns a commendation (" + h.Commendations + ")");
        var thrown = new List<OfficerCase>
        {
            new OfficerCase { Id = 1, End = CaseEnd.Released, EndDay = 50, Quality = 10 }, new OfficerCase { Id = 2, End = CaseEnd.Acquitted, EndDay = 50, Quality = 30 },
            new OfficerCase { Id = 3, End = CaseEnd.Acquitted, EndDay = 50, Quality = 90 }, new OfficerCase { Id = 4, End = CaseEnd.Convicted, EndDay = 50, Quality = 10 },
            new OfficerCase { Id = 5, End = CaseEnd.Released, EndDay = 50, Quality = 5 }, new OfficerCase { Id = 6, End = CaseEnd.Released, EndDay = 50, Quality = 5 }
        };
        Check(CaseRules.ThrownPenalty(thrown, 55, 60) == CaseRules.ThrownMost && CaseRules.ThrownPenalty(thrown.GetRange(2, 2), 55, 60) == 0 && CaseRules.ThrownPenalty(thrown, 50 + TalentCatalogue.Scaled(CaseRules.ThrownDays, 60) + 1, 60) == 0
              && CaseRules.ThrownPenalty(null, 55, 60) == 0,
            "police cases: a case thrown out for a poor arrest counts against reviews for a while and at most " + CaseRules.ThrownMost + " points; a good arrest acquitted or a conviction never does");

        // the detective's work
        var open = new OfficerCase { Id = 9, Hearing = 30, Strength = 50, Offence = JusticeLaw.Theft };
        DutyFile k = CaseFile();
        Check(CaseRules.ActionWhy(open, CaseAction.Scene, 20, 0, 1) == "reallife_police_why_case_rank" && CaseRules.ActionWhy(open, CaseAction.Scene, 20, 0, CaseRules.DetectiveRank) == null,
            "police detective: only from the detective's rung are cases worked");
        CaseRules.Work(open, CaseAction.Scene, 20, 40, 2, k);
        Check(open.Strength > 50 && open.WorkedDay == 20 && k.ActionsToday == 1 && CaseRules.ActionWhy(open, CaseAction.Write, 20, k.ActionsToday, 2) == "reallife_police_why_case_worked"
              && CaseRules.ActionWhy(open, CaseAction.Write, 21, k.ActionsToday, 2) == null && CaseRules.ActionWhy(open, CaseAction.Scene, 21, k.ActionsToday, 2) == "reallife_police_why_case_done",
            "police detective: one action a shift on a case, and none done twice on it");
        Check(CaseRules.ActionWhy(open, CaseAction.Write, 22, CaseRules.ActionsMost, 2) == "reallife_police_why_case_actions", "police detective: the shift's actions are capped at " + CaseRules.ActionsMost);
        int worked = 0;
        for (int i = 0; i < 10; i++)
        {
            var one = new OfficerCase { Id = 20 + i, Hearing = 40, Strength = 10 };
            if (CaseRules.ActionWhy(one, CaseAction.Witness, 25, k.ActionsToday, 2) == null)
            {
                CaseRules.Work(one, CaseAction.Witness, 25, 50, 2, k);
                worked++;
            }
        }
        Check(worked == CaseRules.ActionsMost - 1 && k.ActionsToday == CaseRules.ActionsMost, "police detective: ten cases and a shift of three actions: only three are worked (" + k.ActionsToday + ")");
        bool gains = true;
        for (int pts = 0; pts < 100; pts += 10)
        {
            gains &= CaseRules.Gain(pts + 10, 2) >= CaseRules.Gain(pts, 2) && CaseRules.Gain(pts, 3) >= CaseRules.Gain(pts, 2) && CaseRules.Gain(pts, 4) <= CaseRules.GainMost && CaseRules.Gain(pts, 2) >= CaseRules.GainBase;
        }
        Check(gains, "police detective: an action adds more with more skill and rank, within the rule's bounds");
        var full = new OfficerCase { Id = 30, Hearing = 40, Strength = 98 };
        CaseRules.Work(full, CaseAction.Write, 26, 100, 4, CaseFile());
        Check(full.Strength == 100, "police detective: a case's strength never passes one hundred");
        CaseFile().Begin(1);
        DutyFile reset = CaseFile();
        reset.Begin(5);
        reset.ActionsToday = 3;
        reset.Begin(5);
        Check(reset.ActionsToday == 3, "police detective: the same day's shift keeps its count");
        reset.Begin(6);
        Check(reset.ActionsToday == 0, "police detective: a new day's shift has its actions again");
        Check(CaseRules.WitnessValue(WitnessChoice.Prepared, true) > CaseRules.WitnessValue(WitnessChoice.Prepared, false) && CaseRules.WitnessValue(WitnessChoice.Prepared, false) > CaseRules.WitnessValue(WitnessChoice.Plain, true)
              && CaseRules.WitnessValue(WitnessChoice.Hold, true) < 0 && CaseRules.WitnessValue(WitnessChoice.Plain, false) == 0,
            "police detective: evidence from a written file helps most, from one not written a little, saying little hurts");
        var heard = new OfficerCase { Id = 40, Hearing = 12, Strength = 40, Witness = CaseRules.WitnessPrepared };
        var unheard = new OfficerCase { Id = 40, Hearing = 12, Strength = 40, Witness = 0 };
        CaseRules.Odds(heard.Strength, heard.Witness, out int ha, out _, out _);
        CaseRules.Odds(unheard.Strength, unheard.Witness, out int ua, out _, out _);
        Check(ha < ua, "police detective: giving evidence from a file moves the hearing's odds towards a conviction");

        // the case book in the save
        DutyFile book = CaseFile();
        book.Cases.Add(new OfficerCase { Id = 3, Name = "Ash Lee", PersonId = 12, Offence = JusticeLaw.Assault, District = "ba:neighborhood_x", Day = 5, Hearing = 8, Quality = 71, Strength = 80, WorkedDay = 7, Done = 5, Witness = 8,
            Summoned = true, End = CaseEnd.Convicted, EndDay = 9, Sentence = 4 });
        book.NextCase = 4;
        book.ActionsToday = 2;
        book.NoticeDay = 9;
        book.PendOpened = 1;
        book.PendConvicted = 1;
        book.PendCommended = 1;
        DutyFile back = DutyStore.Read(DutyStore.Write(book), out _);
        OfficerCase b = back.Cases.Count == 1 ? back.Cases[0] : null;
        Check(b != null && b.Id == 3 && b.Name == "Ash Lee" && b.PersonId == 12 && b.Offence == JusticeLaw.Assault && b.District == "ba:neighborhood_x" && b.Day == 5 && b.Hearing == 8 && b.Quality == 71
              && b.Strength == 80 && b.WorkedDay == 7 && b.Done == 5 && b.Witness == 8 && b.Summoned && b.End == CaseEnd.Convicted && b.EndDay == 9 && b.Sentence == 4
              && back.NextCase == 4 && back.ActionsToday == 2 && back.NoticeDay == 9 && back.PendOpened == 1 && back.PendConvicted == 1 && back.PendCommended == 1,
            "police cases: the case book and the day's news come back from the save, every field");
        DutyFile older = DutyStore.Read(DutyStore.Write(new DutyFile { Arrests = 2 }), out _);
        Check(older.Arrests == 2 && older.Cases.Count == 0 && older.NextCase == 1, "police cases: a file with no case book reads as an empty one");
        DutyFile before = DutyStore.Read("v=1~h:3|1|0|0|0|0|0|0|1|4|9|60|1|2", out _);
        Check(before != null && before.Arrests == 3 && before.Cases.Count == 0 && before.NextCase == 1 && before.NoticeDay == -1, "police cases: a save from before the book (a short record) reads, with no cases");
        Check(back.Cases.Count <= CaseRules.Most, "police cases: the book keeps at most " + CaseRules.Most);
        DutyFile big = CaseFile();
        var opened = new List<OfficerCase>();
        for (int i = 0; i < CaseRules.Most + 10; i++)
        {
            OfficerCase c = CaseRules.Open(big, "X", 0, JusticeLaw.Theft, string.Empty, 1, 0, i);
            if (c.Open)
            {
                opened.Add(c);
            }
        }
        bool keptOpen = true;
        foreach (OfficerCase c in opened)
        {
            keptOpen &= big.Cases.Contains(c);
        }
        Check(big.Cases.Count <= CaseRules.Most && keptOpen, "police cases: past the limit only closed cases go, never an open one (" + big.Cases.Count + " kept)");
    }

    private static string PoliceSource(string relative)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
    }

    private static void PoliceSources()
    {
        string duty = PoliceSource("Careers/Police/CareersDirector.Duty.cs");
        string dutyFile = PoliceSource("Careers/Police/CareersDirector.DutyFile.cs");
        string director = PoliceSource("Careers/CareersDirector.cs");
        string work = PoliceSource("Careers/CareersDirector.Work.cs");
        string desk = PoliceSource("Careers/Police/PoliceDesk.cs");
        string links = PoliceSource("Wiring/Wiring.PoliceLinks.cs");
        string crime = PoliceSource("Crime/CrimeDirector.cs");
        string scene = PoliceSource("Crime/CrimeScene.Duty.cs");
        string stop = PoliceSource("Crime/PoliceAttention.Stop.cs");
        string law = PoliceSource("Law/StationDesk.cs");
        string wiring = PoliceSource("Wiring/Wiring.cs");
        string health = PoliceSource("Wiring/Wiring.HealthLinks.cs");
        string life = PoliceSource("Life/LifeEvents.cs");
        string work2 = PoliceSource("Careers/CareersDirector.Desk.cs");
        Check(duty.Length > 0 && links.Length > 0 && scene.Length > 0, "police sources: the files are found");
        // nothing per frame: no Update, no LateUpdate, no FixedUpdate, no Find in the career's own files; the look rides the one-second timer
        Check(!Regex.IsMatch(duty + dutyFile + desk, "\\b(Update|LateUpdate|FixedUpdate)\\s*\\(") && !duty.Contains("FindObjectsByType") && !duty.Contains("FindObjectOfType") && !desk.Contains("FindObjectsByType"),
            "police sources: nothing of the career runs in a frame, and nothing searches the scene");
        Check(Regex.IsMatch(director, "WorkDesks\\(\\);\\s*OwnDeskSecond\\(\\);\\s*DutyTick\\(\\);"), "police sources: the officer's look rides the careers' one-second timer, inside the working life");
        Check(duty.Contains("private void DutyTick()") && duty.Contains("if (!_dutyOn)") && duty.IndexOf("if (!_dutyOn)", StringComparison.Ordinal) < duty.IndexOf("CareersBridges.IncidentNow()", StringComparison.Ordinal),
            "police sources: off duty the timer costs one flag test, and no incident is looked for");
        // the uniform comes off on every way out
        string stopWork = PoliceBetween(work, "private void StopWork(bool unloading)", "private void WorkHour");
        string endJob = PoliceBetween(work, "private void EndJob(", "Spell s = ProRules.Spell");
        Check(stopWork.Contains("DutyDown(") && endJob.Contains("DutyDown(") && director.Contains("DutyDown(\"the save tools\")") && director.Contains("DutyDown(\"the save was left or changed\")"),
            "police sources: the uniform comes off when the working life stops, the job ends, the save tools run and the save is left or changed");
        Check(duty.Contains("DutyDress(false)") && PoliceBetween(duty, "private void DutyDown(", "private bool DutyDress").Contains("DutyDress(false)") && PoliceBetween(duty, "internal void DutyStop(", "private void DutyDown(").Contains("DutyDress(false)"),
            "police sources: ending a shift and stopping one both take the uniform off");
        Check(PoliceBetween(duty, "private void DutyTick()", "private bool DutyKept").Contains("PoliceRules.Jumped(moved)") && PoliceBetween(duty, "private void DutyTick()", "private bool DutyKept").Contains("DutyEnd.Skipped"),
            "police sources: a sleep or a skip ends the shift, and what was patrolled before it is kept");
        Check(director.Contains("DutyBank();   // the minutes patrolled since the hour are worked and paid before the save is taken"), "police sources: a save banks the minutes patrolled since the hour");
        Check(Regex.IsMatch(work, "DutyHour\\(j\\);\\s*if \\(DutyConduct\\(j, day\\)\\)"), "police sources: each hour banks the patrol and looks at the officer's conduct");
        Check(work.Contains("CareersBridges.ShiftWorked?.Invoke(IsPolice(j) ? -1 : j.Def.SkillA)"), "police sources: a patrol is never also a day's draw for an accident at work: its dangers are its incidents'");
        Check(work.Contains("WorkBanked(j, s.Kind == SittingKind.Overtime, minutes);") && duty.Contains("WorkBanked(j, false, minutes);"), "police sources: a patrol's minutes are worked and paid by the careers' own code, as a desk's are");
        Check(work.Contains("EndJob(LeaveReason.Misconduct") || duty.Contains("EndJob(LeaveReason.Misconduct, day, false)"), "police sources: the service lets an officer go by the careers' own dismissal");
        // the injury and the death go the one way every danger goes
        Check(links.Contains("Mortality.Judge(DeathCause.Duty") && links.Contains("Mortality.Die(DeathCause.Duty)") && links.Contains("Mortality.NearMiss(DeathCause.Duty)") && links.Contains("InjureOnDuty("),
            "police sources: an injury on duty is judged by Permadeath's door and hurts through the life events' own injury, as a mugging does");
        Check(life.Contains("public void InjureOnDuty(") && life.Contains("(HurtOnDuty ?? Hurt)?.Invoke(hours)") && health.Contains("LifeEvents.HurtOnDuty = ") && health.Contains("LifeEvents.HurtOnDuty = null;"),
            "police sources: Health counts it as an accident at work, and the bridge is taken down with the part");
        Check(PoliceSource("Health/HealthDirector.Care.cs").Contains("TakeInjury(injury, true)"), "police sources: an injury on duty is an accident at work: the employer's insurer and the days off paid");
        // the incident is the scene's own
        Check(links.Contains("CrimeDirector.Duty(out") && links.Contains("TrafficAccidents.Duty(out") && links.Contains("PedestrianIncidents.Duty(at") && links.Contains("CrimeDirector.DutySettle(move, result)"),
            "police sources: the incidents are read from the crime, accident and casualty scenes and settled in them");
        Check(scene.Contains("public bool Duty(") && scene.Contains("Arrest(First, now, ArrestEnding())") && scene.Contains("SendOfficer(") && scene.Contains("Flee(First, player)") && scene.Contains("Leave(now, player)"),
            "police sources: an arrest is the scene's own officer and car, a talk or a ticket sends them on their way, and a failure has the suspect run");
        Check(crime.Contains("CityWatch.DispatchScale") && crime.Contains("_serial++") && PoliceSource("Crime/CityWatch.cs").Contains("DutyCrime = 3f"), "police sources: an officer on a shift is sent where the street crime is, three times as much of it");
        Check(stop.Contains("Careers.PoliceRules.Courtesy(CityWatch.OnShift,") && stop.Contains("if (!courtesy)") && stop.Contains("reallife_police_courtesy"), "police sources: a minor traffic stop of an officer on a shift is a word, and the record, fine and points are as they were for anyone else");
        Check(law.Contains("Service.Duty") && law.Contains("Bridges.DutyPress") && law.Contains("Bridges.DutyDeskView"), "police sources: the front desk offers the patrol in its own list");
        Check(wiring.Contains("Step(\"the police career\", PoliceLinks.Connect)") && wiring.Contains("Step(\"the police career\", PoliceLinks.Disconnect)"), "police sources: the career's wiring is connected and disconnected with the others");
        Check(links.Contains("CareersBridges.GigOn = () => JobDirector.OnShift") && duty.Contains("CareersBridges.GigOnNow()"), "police sources: a gig shift and a patrol never share the player's work clothes");
        string cases = PoliceSource("Careers/Police/CareersDirector.Cases.cs");
        Check(Regex.IsMatch(work, "Guard\\.Step\\(\"the officer's cases' day\", \\(\\) => CaseDay\\(day\\)\\)") && cases.Contains("CaseRules.Advance(f, day, StudySeed)") && cases.Contains("CaseRules.TakeNotice("),
            "police sources: the day's turn decides the hearings behind and sends the one notice");
        Check(duty.Contains("CaseOpen(j, call, margin)") && duty.Contains("call.Source == DutySource.Crime") && cases.Contains("PeoplePool.For(") && cases.Contains("CareersBridges.DistrictNow()"),
            "police sources: an arrest opens a case named as the city's people are, in the district it was made in");
        Check(!Regex.IsMatch(cases, "\\b(Update|LateUpdate|FixedUpdate)\\s*\\(") && duty.Contains("if (CaseWitness(j, Save.Day))") && duty.Contains("now < _caseBusyUntil"),
            "police sources: the cases cost nothing per frame: the hearing-day look rides the one-second timer and the hour in court holds incidents off");
        Check(law.Contains("Service.Cases") && law.Contains("Bridges.CasePress") && links.Contains("Law.Bridges.CasePress = CasePress") && links.Contains("CaseOffered = cases"),
            "police sources: the front desk's list has the cases entry for a detective");
        Check(PoliceSource("Careers/CareerApp.Life.cs").Contains("_director.DutyCaseLines(_file)") && PoliceSource("Ui/Work/CareerLive.cs").Contains("CareersDirector.Instance?.DutyCaseLines(file)"),
            "police sources: the open cases are on the Career page on both interfaces");
        Check(PoliceSource("Careers/Police/CareersDirector.DutyFile.cs").Contains("CaseRules.ThrownPenalty("), "police sources: a case thrown out counts against the review");
        Check(PoliceSource("Careers/CareersDirector.Needs.cs").Contains("DutyEntryWhy(p)") && PoliceSource("Careers/CareersDirector.Applying.cs").Contains("DutyEntryWhy(a.Post.Def)")
              && PoliceSource("Careers/CareersDirector.Applying.cs").Contains("DutyEntryWhy(p) != null"), "police sources: the entry's asks are made where a post is applied for, listed as open, and taken");
        Check(work2.Contains("!IsPolice(j) && DeskGame.Inside"), "police sources: no desk of the player's is set up in a police station");
        Check(PoliceSource("CityLife/CityOutfits.cs").Contains("look == CityLook.Officer ? Officer"), "police sources: the player wears the officers' own garments, not a new model");
        Check(PoliceSource("Core/RealLifeOverhaulMod.cs").Contains("\"reallife_police_test_patrol\", () => Careers.CareersDirector.Instance?.TestDuty()")
              && PoliceSource("Core/RealLifeOverhaulMod.cs").Contains("\"reallife_police_test_hurt\", () => Careers.CareersDirector.Instance?.TestDutyHurt()"), "police sources: both tests are on the options page");
        Check(PoliceSource("Permadeath/PermadeathDirector.cs").Contains("case DeathCause.Duty: return \"reallife_pd_near_duty\""), "police sources: the near miss has its words");
    }

    private static string PoliceBetween(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        if (a < 0)
        {
            return string.Empty;
        }
        int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
    }

    private static void PoliceWords()
    {
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(english != null, "police texts: en.json is read");
        if (english == null)
        {
            return;
        }
        var keys = new List<string>();
        keys.AddRange(PoliceRules.DangerKeys);
        keys.AddRange(PoliceRules.MoveKeys);
        keys.AddRange(PoliceRules.KindKeys);
        string all = PoliceSource("Careers/Police/CareersDirector.Duty.cs") + PoliceSource("Careers/Police/PoliceRules.cs") + PoliceSource("Wiring/Wiring.PoliceLinks.cs") + PoliceSource("Careers/CareersDirector.Work.cs")
                     + PoliceSource("Careers/CareersDirector.Life.cs") + PoliceSource("Careers/CareersDirector.DeskDay.cs") + PoliceSource("Careers/CareersDirector.Desk.cs") + PoliceSource("Crime/PoliceAttention.Stop.cs")
                     + PoliceSource("Careers/Police/PoliceDesk.cs") + PoliceSource("Careers/Police/CareersDirector.Cases.cs") + PoliceSource("Ui/Work/CareerLive.cs") + PoliceSource("Core/RealLifeOverhaulMod.cs") + PoliceSource("Permadeath/PermadeathDirector.cs");
        foreach (Match m in Regex.Matches(all, "\"(reallife_(?:police|pd_near_duty)[a-z_]*)\""))
        {
            if (!keys.Contains(m.Groups[1].Value))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        for (int i = 0; i < 5; i++)
        {
            keys.Add("reallife_pro_rank_police_" + i);
        }
        keys.AddRange(new[] { "reallife_pro_name_police", "reallife_work_employer_police", "reallife_study_course_police_academy", "reallife_study_about_police_academy", "reallife_pd_obit_stand_duty", "reallife_pd_obit_how_duty",
            "reallife_police_courtesy", "reallife_police_why_record", "reallife_police_why_conviction", "reallife_police_why_licence", "reallife_police_why_unfit", "reallife_police_why_shift_unfit",
            "reallife_police_why_shift_licence", "reallife_police_why_gig", "reallife_police_why_no_late" });
        Check(keys.Count >= 70, "police texts: its keys are found: " + keys.Count);
        bool there = true, dashes = true, bangs = true, notices = true;
        string missing = null;
        foreach (string key in keys)
        {
            if (!english.TryGetValue(key, out string value) || value.Length == 0)
            {
                there = false;
                missing = key;
                continue;
            }
            dashes &= value.IndexOf('—') < 0 && value.IndexOf('–') < 0 && !value.Contains(" - ");
            bangs &= !value.Contains("!");
            notices &= !key.StartsWith("reallife_police_note_", StringComparison.Ordinal) || value.Length <= 240;
        }
        Check(there, "police texts: every key is in en.json" + (missing != null ? " (" + missing + ")" : string.Empty));
        Check(dashes && bangs, "police texts: no dash and no exclamation mark");
        Check(notices, "police texts: a notice is at most 240 characters");
        Check(english["reallife_police_list_odds"].Contains("{chance}") && english["reallife_police_list_odds"].Contains("{risk}") && english["reallife_police_list_back_off"].Contains("{points}")
              && english["reallife_police_note_call"].Contains("{kind}") && english["reallife_police_note_call"].Contains("{danger}") && english["reallife_police_note_call"].Contains("{metres}")
              && english["reallife_police_why_record"].Contains("{days}"), "police texts: the figures a line is told are in it");
        string guide = english.TryGetValue("reallife_guide_careers_jobs", out string g) ? g : string.Empty;
        string[] facts = { "{police_clean_days}", "{police_from}", "{police_to}", "{police_crime_times}", "{police_back_off}", "{police_start}", "{police_commend}", "{police_fatal_one_in}", "{police_thrown_days}", "{police_detective}", "{police_actions}" };
        bool filled = true;
        foreach (string fact in facts)
        {
            filled &= guide.Contains(fact) && PoliceSource("Careers/CareersGuide.cs").Contains("\"" + fact.Trim('{', '}') + "\"");
        }
        Check(filled && guide.Contains("# Police work"), "police texts: the Guide's police section is in the careers page and every figure in it is filled from the rules");
        Check(PoliceSource("Careers/CareersGuide.cs").Contains("Permadeath.PermadeathRules.DutyOdds"), "police texts: the Guide's odds of a fatal injury are Permadeath's own constant");
        Check(english.TryGetValue("reallife_changes_next", out string next) && next.Contains("police") && !next.Contains("Fixed: The police"), "police texts: the What's new line says the police career is in and does not start with a fix mark");
        // the kit lists the keys its screen reads
        string screens = PoliceSource("Ui/UiScreens.Work.cs");
        Check(screens.Contains("\"reallife_pro_name_police\"") && screens.Contains("\"reallife_pro_rank_police_0\"") && screens.Contains("\"reallife_pro_rank_police_4\"") && screens.Contains("\"reallife_police_file_arrests\"")
              && screens.Contains("\"reallife_police_button_start\"") && screens.Contains("\"reallife_study_course_police_academy\""), "police texts: the new interface's Work screen lists the police keys it reads");
    }
}
