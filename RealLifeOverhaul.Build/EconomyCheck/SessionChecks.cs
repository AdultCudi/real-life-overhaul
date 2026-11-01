using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

// The desk sessions (Scripts/Sessions; plans/tech-overhaul DRAFT2 section 3, "Desks"): the one registry and what it refuses,
// the schedule, the furniture a desk needs, a session's minutes, a manager's strength and seeded choices with their
// mistakes, when a manager holds a session and the one reminder a day, the save's line with places of every character, the
// effect read back while it lasts; the weekly team meeting's rules and its week through the people part's own rules (morale
// against burnout); the sources: the panel's buttons for Going Public, the guards, the option off, the click and the tests;
// the texts in every language; and what an hour's turn and the save cost.
internal static partial class Program
{
    private static void Sessions()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        SessionRegistryRules();
        SessionSchedule();
        SessionManagers();
        SessionSaveLine();
        SessionMeeting();
        SessionMeetingWeek();
        SessionSources();
        SessionSharedDesk();
        SessionTexts();
        SessionTimings();
        Console.WriteLine("  Desk sessions: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A kind of the checks' own, two steps of two and three choices.
    private static SessionDef SessionKind(string id, params string[] types)
    {
        return new SessionDef
        {
            Id = id,
            TitleKey = "t",
            AboutKey = "a",
            Types = types,
            EveryDays = 7,
            Hour = 10,
            BaseMinutes = 15,
            DesksLeast = 2,
            Steps = new[]
            {
                new SessionStep { TitleKey = "s0", Choices = new[] { new SessionChoice { NameKey = "a", Minutes = 0 }, new SessionChoice { NameKey = "b", Minutes = 0 } } },
                new SessionStep { TitleKey = "s1", Choices = new[] { new SessionChoice { NameKey = "c", Minutes = 30 }, new SessionChoice { NameKey = "d", Minutes = 60 }, new SessionChoice { NameKey = "e", Minutes = 90 } } }
            },
            Apply = (ask, choices) => new SessionOutcome { Done = true, Key = "k", A = choices[0], B = choices[1], Days = 7 },
            Pick = (ask, step) => step == 0 ? 1 : 2
        };
    }

    // ---------------------------------------------------------------- the registry

    private static void SessionRegistryRules()
    {
        SessionDef good = SessionKind("check_kind", "type_a", "type_b");
        Check(SessionRules.Whole(good), "sessions: a whole kind is fit to be held");
        SessionDef noApply = SessionKind("check_x", "type_a");
        noApply.Apply = null;
        SessionDef noTypes = SessionKind("check_y");
        SessionDef badId = SessionKind("check#z", "type_a");
        SessionDef four = SessionKind("check_w", "type_a");
        four.Steps[1].Choices = new[] { new SessionChoice(), new SessionChoice(), new SessionChoice(), new SessionChoice() };
        SessionDef empty = SessionKind("check_v", "type_a");
        empty.Steps[0].Choices = new SessionChoice[0];
        SessionDef never = SessionKind("check_u", "type_a");
        never.EveryDays = 0;
        Check(!SessionRules.Whole(null) && !SessionRules.Whole(noApply) && !SessionRules.Whole(noTypes) && !SessionRules.Whole(badId) && !SessionRules.Whole(four)
              && !SessionRules.Whole(empty) && !SessionRules.Whole(never) && !SessionRegistry.Add(noApply) && SessionRegistry.Find("check_x") == null,
            "sessions: a kind without its callback, its types or a step's choices, with more than the panel's three choices, a reserved character in its id or no schedule is refused");
        int count = SessionRegistry.All.Count;
        bool added = SessionRegistry.Add(good);
        SessionDef replaced = SessionKind("check_kind", "type_c");
        bool again = SessionRegistry.Add(replaced);
        var at = new List<SessionDef>();
        SessionRegistry.For("type_c", at);
        bool listed = at.Count == 1 && ReferenceEquals(at[0], replaced);
        SessionRegistry.For("type_a", at);
        Check(added && again && SessionRegistry.All.Count == count + 1 && ReferenceEquals(SessionRegistry.Find("check_kind"), replaced) && listed && at.Count == 0
              && SessionRegistry.Any("type_c") && !SessionRegistry.Any("type_a") && !SessionRegistry.Any(null),
            "sessions: one registry: a kind is added once, the same id takes the old one's place, and each business type lists its kinds");
        SessionRegistry.Remove("check_kind");
        Check(SessionRegistry.All.Count == count && SessionRegistry.Find("check_kind") == null, "sessions: a kind taken out is gone and the others are as they were");
        Check(good.HeldAt("type_b") && !good.HeldAt("TYPE_B") && !good.HeldAt(null), "sessions: a kind is held at its own types only, by the exact name");
    }

    // ---------------------------------------------------------------- the schedule, the desk and the minutes

    private static void SessionSchedule()
    {
        Check(SessionRules.Due(SessionRules.Never, 1, 7) && !SessionRules.Due(10, 10, 7) && !SessionRules.Due(10, 16, 7) && SessionRules.Due(10, 17, 7) && SessionRules.Due(10, 40, 7)
              && SessionRules.Due(10, 11, 0) && SessionRules.DaysToDue(10, 13, 7) == 4 && SessionRules.DaysToDue(10, 17, 7) == 0 && SessionRules.DaysToDue(SessionRules.Never, 3, 7) == 0,
            "sessions: a kind never held is due; held, it is due again its days later; the days to wait are said");
        SessionDef k = SessionKind("check_desk", "type_a");
        Check(SessionRules.Missing(k, 0, 0, out int m0) == SessionNeed.Desks && m0 == 2 && SessionRules.Missing(k, 1, 1, out int m1) == SessionNeed.Desks && m1 == 1
              && SessionRules.Missing(k, 3, 0, out int m2) == SessionNeed.OwnDesk && m2 == 0 && SessionRules.Missing(k, 2, 1, out _) == SessionNeed.None && SessionRules.Missing(k, -4, 0, out int m3) == SessionNeed.Desks && m3 == 2,
            "sessions: a desk lacks workstations first, then one of the owner's own nobody is rostered at; with both, nothing");
        k.OwnDesk = false;
        k.DesksLeast = 0;
        Check(SessionRules.Missing(k, 0, 0, out _) == SessionNeed.None, "sessions: a kind that asks for no furniture lacks nothing");
        SessionDef t = SessionKind("check_minutes", "type_a");
        Check(SessionRules.Minutes(t, new[] { 0, 0 }) == 45 && SessionRules.Minutes(t, new[] { 1, 2 }) == 105 && SessionRules.Minutes(t, new[] { 0, 9 }) == 45 && SessionRules.Minutes(t, null) == 45
              && SessionRules.Minutes(t, new[] { 1 }) == 45,
            "sessions: a session takes its base minutes and its choices', a choice out of range or missing as the first");
        t.BaseMinutes = 0;
        t.Steps[1].Choices[0].Minutes = 0;
        Check(SessionRules.Minutes(t, new[] { 0, 0 }) == 1, "sessions: a session takes a minute at least, so the game's panel always has time to pass");
    }

    // ---------------------------------------------------------------- managers

    private static void SessionManagers()
    {
        Check(SessionRules.Strength(false, 0) == 1000 && SessionRules.Strength(true, 0) == SessionRules.ManagerLeast && SessionRules.Strength(true, 100) == 1000
              && SessionRules.Strength(true, 50) == 750 && SessionRules.Strength(true, 400) == 1000 && SessionRules.Strength(true, -9) == SessionRules.ManagerLeast,
            "sessions: the owner brings all their strength; a manager half at no skill, all at full, within bounds");
        SessionDef k = SessionKind("check_mgr", "type_a");
        var a = new SessionAsk { Place = "Main%20Street~12", Name = "Firm", Day = 40, Skill = 100 };
        var picks = new int[2];
        int none = SessionRules.ManagerChoices(k, a, 77, 0, picks);
        Check(none == 0 && picks[0] == 1 && picks[1] == 2, "sessions: a manager of full skill makes the kind's sound choices, with no mistake");
        int steps = 0, mistakes = 0, steps50 = 0, mistakes50 = 0;
        bool same = true;
        var again = new int[2];
        for (int day = 1; day <= 2000; day++)
        {
            a.Day = day;
            a.Skill = 0;
            mistakes += SessionRules.ManagerChoices(k, a, 77, 1, picks);
            steps += 2;
            int once = SessionRules.ManagerChoices(k, a, 77, 1, again);
            same &= picks[0] == again[0] && picks[1] == again[1];
            bool wrong0 = picks[0] != 1, wrong1 = picks[1] != 2;
            same &= (wrong0 ? 1 : 0) + (wrong1 ? 1 : 0) == once && (!wrong0 || picks[0] == 0) && (!wrong1 || picks[1] == 0);
            a.Skill = 50;
            mistakes50 += SessionRules.ManagerChoices(k, a, 77, 1, picks);
            steps50 += 2;
        }
        int rate = mistakes * 1000 / steps, rate50 = mistakes50 * 1000 / steps50, want = ManagerRules.MistakeChance(0, 0), want50 = ManagerRules.MistakeChance(50, 0);
        Check(same && Math.Abs(rate - want) < 40 && Math.Abs(rate50 - want50) < 40 && rate50 < rate,
            "sessions: a manager's mistakes come at the managers' own chance for their skill (" + rate + " against " + want + ", at 50 " + rate50 + " against " + want50 + "), the same on every machine, a mistake the next choice round");
        var sound = new int[2];
        SessionRules.SoundChoices(k, a, sound);
        k.Pick = (ask, step) => 9;
        var bounded = new int[2];
        SessionRules.SoundChoices(k, a, bounded);
        k.Pick = null;
        SessionRules.ManagerChoices(k, a, 77, 1, picks);
        Check(sound[0] == 1 && sound[1] == 2 && bounded[0] == 0 && bounded[1] == 0, "sessions: unseen, the owner takes the sound choices; a pick out of range reads as the first");
        Check(SessionRules.ManagerHolds(true, true, true, 10, 10) && SessionRules.ManagerHolds(true, true, true, 23, 10) && !SessionRules.ManagerHolds(true, true, true, 9, 10)
              && !SessionRules.ManagerHolds(false, true, true, 12, 10) && !SessionRules.ManagerHolds(true, false, true, 12, 10) && !SessionRules.ManagerHolds(true, true, false, 12, 10),
            "sessions: a manager holds a session handed to them, due, while at work, from its hour on (a sleep past the hour holds it at the next look)");
        Check(SessionRules.Remind(1, SessionRules.Never, 5, SessionRules.RemindHour) && !SessionRules.Remind(1, 5, 5, 12) && !SessionRules.Remind(0, 4, 5, 12) && !SessionRules.Remind(3, 4, 5, SessionRules.RemindHour - 1)
              && SessionRules.Remind(3, 4, 5, 23),
            "sessions: what waits is said once a day at the most, from the reminder's hour, and only when something waits");
        Check(SessionRules.Until(10, 7) == 17 && SessionRules.Until(10, 0) == 10, "sessions: an effect lasts its days from the day held, the day held the first");
    }

    // ---------------------------------------------------------------- the save

    private static void SessionSaveLine()
    {
        var f = new SessionFile { Seed = 123456, Reminded = 44 };
        SessionRecord r = f.Get("Main%20Street#1^2|3~12", MeetingRules.Id);
        r.Held = 40;
        r.Delegated = true;
        r.By = 1;
        r.A = -30;
        r.B = 12;
        r.Until = 47;
        r.Times = 6;
        f.Get("Elm%25Road~3", "check_other");
        string line = SessionStore.Write(f);
        SessionFile back = SessionStore.Read(line, out int version);
        SessionRecord b = back != null ? back.Find("Main%20Street#1^2|3~12", MeetingRules.Id) : null;
        Check(version == SessionStore.Version && back.Seed == 123456 && back.Reminded == 44 && back.Records.Count == 2 && b != null && b.Held == 40 && b.Delegated && b.By == 1 && b.A == -30
              && b.B == 12 && b.Until == 47 && b.Times == 6 && back.Find("Elm%25Road~3", "check_other").Held == SessionRules.Never && SessionStore.Write(back) == line,
            "sessions save: every field of every record comes back, places holding '%', '#', '^' and '|', and the line is the same written again");
        SessionFile shortRec = SessionStore.Read("v=1|5|-1^Main~1#" + MeetingRules.Id + "#20", out _);
        Check(shortRec != null && shortRec.Find("Main~1", MeetingRules.Id).Held == 20 && shortRec.Find("Main~1", MeetingRules.Id).Until == SessionRules.Never && !shortRec.Find("Main~1", MeetingRules.Id).Delegated,
            "sessions save: a record written with fewer fields reads the rest as they start (new fields go at the end)");
        Check(SessionStore.Read("v=2|5|-1", out int v2) == null && v2 == 0 && SessionStore.Read("garbage", out _) == null && SessionStore.Read("v=1|x|1", out _) == null
              && SessionStore.Read("v=1|5|1^#", out _) == null && SessionStore.Read(null, out _) == null && SessionStore.Load(new Dictionary<string, string>(), out int none) == null && none == 0,
            "sessions save: a later version, a damaged line or none gives nothing");
        var data = new Dictionary<string, string> { { SessionStore.Key, "v=9|1|1" } };
        Check(SessionStore.Load(data, out int bad) == null && bad == -1 && SessionStore.Key == "reallife:sessions",
            "sessions save: a line that cannot be read says so (-1), so the director keeps it as read; the key is the mod's own");
        Check(f.Effect("Main%20Street#1^2|3~12", MeetingRules.Id, 40, out int ea, out int eb) && ea == -30 && eb == 12 && f.Effect("Main%20Street#1^2|3~12", MeetingRules.Id, 46, out _, out _)
              && !f.Effect("Main%20Street#1^2|3~12", MeetingRules.Id, 47, out int ga, out int gb) && ga == 0 && gb == 0 && !f.Effect("Main%20Street#1^2|3~12", MeetingRules.Id, 39, out _, out _)
              && !f.Effect("Elm%25Road~3", "check_other", 40, out _, out _) && !f.Effect("Nowhere~1", MeetingRules.Id, 40, out _, out _),
            "sessions: an effect is read from the day held until its last day, and never before, after or where nothing was held");
        int pruned = f.Prune(null, s => s == MeetingRules.Id);
        Check(pruned == 1 && f.Records.Count == 1 && f.Prune(p => false, null) == 1 && f.Records.Count == 0, "sessions: records of kinds no longer held are let go, and of places when asked");
        var big = new SessionFile();
        for (int i = 0; i < 700; i++)
        {
            big.Get("Street~" + i, MeetingRules.Id).Held = i;
        }
        SessionFile cut = SessionStore.Read(SessionStore.Write(big), out _);
        Check(cut != null && cut.Records.Count == 512, "sessions save: a save keeps 512 records at the most, so no line grows without end");
    }

    // ---------------------------------------------------------------- the weekly team meeting

    private static void SessionMeeting()
    {
        Check(MeetingRules.Strength(MeetingRules.Full, 1000) == 1000 && MeetingRules.Strength(MeetingRules.Short, 1000) == MeetingRules.ShortStrength
              && MeetingRules.Strength(MeetingRules.Full, SessionRules.Strength(true, 0)) == 500 && MeetingRules.Strength(MeetingRules.Short, 500) == MeetingRules.ShortStrength / 2
              && MeetingRules.Strength(MeetingRules.Full, 5000) == 1000 && MeetingRules.Strength(MeetingRules.Full, -1) == 0,
            "meeting: a full hour has all its strength, half an hour two thirds; a manager of no skill half; within bounds");
        MeetingRules.Effect(MeetingRules.Morale, 1000, out int morale, out int ease, out int output, out int burnout);
        MeetingRules.Effect(MeetingRules.Push, 1000, out int pm, out int pe, out int po, out int pb);
        MeetingRules.Effect(MeetingRules.Push, 0, out _, out _, out int po0, out int pb0);
        Check(morale == MeetingRules.MoraleLift && ease == MeetingRules.BurnoutEase && output == MeetingRules.MoraleWeekOutput && output < 0 && burnout == 0
              && pm == 0 && pe == 0 && po == MeetingRules.PushOutput && pb == MeetingRules.PushBurnout && po0 == 0 && pb0 == 1,
            "meeting: lifting morale raises it and eases burnout at once, for a little less output that week; pushing raises the week's output and burnout a day worked, never none at all");
        var p = new Profile { Id = "x", Morale = 960, Burnout = 30 };
        MeetingRules.Rally(p, 90, 60);
        Check(p.Morale == 1000 && p.Burnout == 0, "meeting: morale and burnout stay within the people part's bounds");
        Check(MeetingRules.Day(500, 8, 12) == 512 && MeetingRules.Day(500, 0, 12) == 500 && MeetingRules.Day(995, 8, 12) == 1000 && MeetingRules.Day(500, 8, 0) == 500,
            "meeting: a pushed week's burnout comes only on a day worked, and never past the most");
        Check(MeetingRules.Points(100, 120) == 112 && MeetingRules.Points(100, -30) == 97 && MeetingRules.Points(100, 0) == 100 && MeetingRules.Points(100, -2000) == 0 && MeetingRules.Points(int.MaxValue / 2, 120) > 0,
            "meeting: a day's work points take the week's output, never below nothing, in longs");
        Check(MeetingRules.Pick(MeetingRules.BurnoutHigh) == MeetingRules.Morale && MeetingRules.Pick(MeetingRules.BurnoutHigh - 1) == MeetingRules.Push && MeetingRules.Pick(900) == MeetingRules.Morale,
            "meeting: a manager lifts a tired team's morale and pushes a fresh one");
        Check(MeetingRules.WeekDays == 7 && MeetingRules.ShortMinutes < MeetingRules.FullMinutes && MeetingRules.Id == "tech_team_meeting", "meeting: a week long, half an hour or an hour, its id kept");
    }

    // The meeting's week through the people part's own day: a team of four, a meeting each Monday of an aim, or none.
    private static void SessionMeetingWeek()
    {
        TeamWeek(-1, 1, 450, 9, out long base1, out int baseBurn1, out _, out _);
        TeamWeek(MeetingRules.Push, 1, 450, 9, out long push1, out int burn1, out _, out _);
        Check(push1 >= base1 * 108 / 100 && burn1 > baseBurn1 + 40,
            "meeting week: a pushed week gets more work done than an ordinary one (" + push1 + " against " + base1 + ") and leaves the team more burnt out (" + burn1 + " against " + baseBurn1 + ")");
        TeamWeek(-1, 4, 760, 10, out long basePoints, out int baseBurn, out int baseMorale, out int baseSick);
        TeamWeek(MeetingRules.Push, 4, 760, 10, out long pushPoints, out int pushBurn, out int pushMorale, out int pushSick);
        TeamWeek(MeetingRules.Morale, 4, 760, 10, out long liftPoints, out int liftBurn, out int liftMorale, out _);
        Check(pushBurn > baseBurn + 150 && pushSick > baseSick && pushMorale < baseMorale && (pushPoints - basePoints) * 100 / basePoints < (push1 - base1) * 100 / base1,
            "meeting week: pushed week after week, a tired team's burnout bites through the people part's own rules: more burnout (" + pushBurn + " against " + baseBurn + "), a chance of falling ill ("
            + pushSick + " against " + baseSick + "), lower morale (" + pushMorale + " against " + baseMorale + ") and a smaller gain than the first week's (" + pushPoints + " against " + basePoints + ")");
        Check(liftMorale > baseMorale && liftBurn < baseBurn && liftPoints > basePoints * 97 / 100,
            "meeting week: lifting morale week after week leaves the team happier (" + liftMorale + " against " + baseMorale + ") and less burnt out (" + liftBurn + " against " + baseBurn + "), costing little work ("
            + liftPoints + " against " + basePoints + ")");
    }

    // Weeks of a team of four seniors working so many hours a weekday, a meeting each Monday of the aim given (-1 none), as the
    // people part's day and the firms' work read it: work points summed; burnout, morale and the sick chance averaged at the end.
    private static void TeamWeek(int aim, int weeks, int startBurnout, int dayHours, out long points, out int burnout, out int morale, out int sick)
    {
        var team = new Profile[4];
        for (int i = 0; i < team.Length; i++)
        {
            team[i] = new Profile { Id = "pro:" + i, Role = 0, Level = 2, A = 60000, B = 55000, Morale = 600, Burnout = startBurnout };
        }
        points = 0;
        int output = 0, pushBurn = 0, until = -1;
        for (int day = 1; day <= weeks * 7; day++)
        {
            int weekday = (day - 1) % 7;
            if (weekday == 0 && aim >= 0)
            {
                MeetingRules.Effect(aim, 1000, out int m, out int e, out output, out pushBurn);
                foreach (Profile p in team)
                {
                    MeetingRules.Rally(p, m, e);
                }
                until = day + MeetingRules.WeekDays;
            }
            bool week = day < until;
            int hours = weekday < 5 ? dayHours : 0;
            foreach (Profile p in team)
            {
                points += MeetingRules.Points(TalentRules.Output(p, 60, day) * FirmsCatalogue.PointsPerDay * hours / TalentCatalogue.DayHours / 1000, week ? output : 0);
                var d = new WorkDay { Day = day, Year = 60, Hours = hours, WeekHours = hours * Math.Min(5, weekday + 1), MainPoints = 60, Paid = 4000, Going = 4000, Esteem = 0, Wanted = true, Comfort = 30 };
                TalentRules.Mood(p, d);
                p.Burnout = MeetingRules.Day(p.Burnout, hours, week ? pushBurn : 0);
            }
        }
        burnout = morale = sick = 0;
        foreach (Profile p in team)
        {
            burnout += p.Burnout / team.Length;
            morale += p.Morale / team.Length;
            sick += TalentRules.SickChance(p) / team.Length;
        }
    }

    // ---------------------------------------------------------------- the sources

    private static void SessionSources()
    {
        string panel = ModSource(@"Sessions\SessionPanel.cs");
        string director = ModSource(@"Sessions\SessionDirector.cs");
        string day = ModSource(@"Sessions\SessionDirector.Day.cs");
        string desk = ModSource(@"Sessions\SessionDirector.Desk.cs");
        string tests = ModSource(@"Sessions\SessionDirector.Tests.cs");
        string game = ModSource(@"Sessions\SessionGame.cs");
        string links = ModSource(@"Wiring\Wiring.SessionLinks.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string guide = ModSource(@"Sessions\SessionGuide.cs");
        string bridges = ModSource(@"Tech\TechBridges.cs");
        string firms = ModSource(@"Tech\TechDirector.Firms.cs");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        Check(panel.Contains("\"StartRealLifeSession\"") && panel.Contains("\"CancelRealLifeSession\"") && panel.Contains("\"StopRealLifeSession\"") && panel.Contains("GoingPublic.InSession")
              && panel.Contains("_director.Sound(Def, _choices)") && panel.Contains("\"StartRealLifeSessionChoice\" + i") && !panel.Contains("\"NextRealLifeSessionStart"),
            "sessions sources: in a Going Public session, which hides the panel and presses the button named Start, the session begins with the sound choices; a choice is named for the press too, Next and Hand are not");
        Check(panel.Contains("private static readonly FeatureGuard FrameGuard = Guard.For(\"living\", \"SessionPanel\", typeof(SessionPanel));") && panel.Contains("return Guard.Ended(this);")
              && game.Contains("Guard.For(\"living\", \"SessionGame.Behaviour\", typeof(Behaviour))") && game.Contains("GameContract.CtaBehaviours.Field") && game.Contains("_list.Insert(0, _behaviour);"),
            "sessions sources: every call of the game's into the panel and the click is behind the guard; the click is an entry at the front of the game's calls to action, as the careers' desk");
        Check(panel.Contains("DeskGame.WalkTo(_station, Arrived)") && panel.Contains("if (!DeskGame.SitOn(seat, _station, out _stood))") && panel.Contains("DeskGame.StandUp(_stood, _station);")
              && panel.Contains("if (_done >= _length)") && panel.Contains("_director.Held(Def, _choices);") && panel.Contains("_director.Stopped(Def, _done, _length);") && panel.Contains("HasTimeMachine() => _state == PlayerActivityState.Running"),
            "sessions sources: the session is sat at the owner's desk with the game's own skip and fast forward; held only when its time is sat whole, stopped early it holds nothing");
        Check(director.Contains("public static bool On => Settings.LivingPopulation && Settings.LivingDesks") && director.Contains("return On && d._file != null && d._file.Effect(")
              && director.Contains("DeskRelease(\"the option is off\");") && bridges.Contains("public static void TeamWeekAt(string place, int day, out int output, out int burnout)")
              && links.Contains("return SessionDirector.Effect(place, MeetingRules.Id, day, out output, out burnout);"),
            "sessions sources: under Living population and its own switch; off, no desk is clicked and no effect is read, so the firms work as before");
        Check(firms.Contains("TechBridges.TeamWeekAt(firm.File.Place, Save.Day, out int week, out _);") && firms.Contains("MeetingRules.Points(TalentRules.Output(p, person.MainPoints, Save.Day)")
              && people.Contains("p.Burnout = MeetingRules.Day(p.Burnout, hours, pushed);") && people.IndexOf("TalentRules.Mood(p, d);", StringComparison.Ordinal) < people.IndexOf("MeetingRules.Day(", StringComparison.Ordinal),
            "sessions sources: the meeting's week reaches the firms' work points and the professionals' day after the people part's own mood");
        Check(day.Contains("SessionRules.ManagerHolds(") && day.Contains("SessionRules.ManagerChoices(def, ask, _file.Seed, KindIndex(def), _picks)") && day.Contains("SessionRules.Remind(waiting, _file.Reminded, day, hour)")
              && day.Contains("\"reallife_session_note_manager\"") && day.Contains("Sessions check (manager): "),
            "sessions sources: an hour's turn holds what managers take by the rules, the seeded choices, one reminder a day, a notice of the result, and a self-check line");
        Check(desk.Contains("SessionGame.OwnDesk(manager, reg, out _desks, out _free)") && desk.Contains("SessionRules.Missing(def, _desks, _free, out int missing)") && desk.Contains("\"reallife_session_status_own_desk\"")
              && game.Contains("Rostered.Add(shifts[s].itemInstanceId);") && game.Contains("GoingPublic.Mine(reg)") && desk.Contains("Sessions check (desk): "),
            "sessions sources: the owner's desk is a workstation nobody is rostered at in the player's own office; what is missing is said on the panel");
        Check(tests.Contains("public void TestMeeting()") && tests.Contains("public void TestManagers()") && tests.Contains("\"reallife_session_test_go\"") && tests.Contains("SessionGame.Route(nearest);")
              && tests.Contains("\"reallife_session_test_living_off\"") && mod.Contains("\"reallife_session_test_meeting\", () => Sessions.SessionDirector.Instance?.TestMeeting()")
              && mod.Contains("\"reallife_session_test_managers\", () => Sessions.SessionDirector.Instance?.TestManagers()") && guide.Contains(".Test(\"reallife_session_test_meeting\", \"reallife_session_test_managers\")"),
            "sessions sources: the tests open the meeting at the desk or mark the nearest firm and say why, and hold the managers' sessions; both in Options and on the feature's list");
        Check(mod.Contains("Settings.LivingPopulationId, \"reallife_living_enabled\"") && mod.Contains("Settings.LivingDesksId, \"reallife_session_enabled\"")
              && tree.Contains("S(\"living\").For(\"living\", \"population\").Main(\"living_population\").Opt(\"living_desks\", \"living_population\")") && links.Contains("SessionRegistry.Add(Meeting());")
              && links.Contains("SessionRegistry.Remove(MeetingRules.Id);") && links.Contains("DesksLeast = 2") && links.Contains("TechTypes.GameStudio") && links.Contains("FirmsGame.WebAgency"),
            "sessions sources: the master option and the sessions' switch in Options, the meeting added by the wiring at every tech firm and the game's office businesses, and taken out again");
    }

    // The owner's desk under Living population is also a free desk of the player's own tech firm, where main's own work
    // (Tech/TechDirector.OwnDesk) offers a click of its own. The game shows only the first click that answers for a thing
    // (CtaManager.UpdateCta), so two features on one desk would hide one of them. One click per desk: the own work takes another
    // free desk first, and where the sessions' desk is the only free one it leaves the click to them and their panel offers it.
    private static void SessionSharedDesk()
    {
        string own = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string offer = MethodText(own, "private void OfferDesk(BuildingRegistration reg)");
        string free = MethodText(own, "private EmployeeStationController FreeDesk(EmployeeStationController session)");
        Check(offer.Contains("session = TechBridges.SessionDeskNow();") && offer.Contains("free = FreeDesk(session);")
              && offer.Contains("_sharedDesk = free != null && ReferenceEquals(free, session) ? free : null;") && offer.Contains("if (free == null || _sharedDesk != null)")
              && ComesBefore(offer, "if (free == null || _sharedDesk != null)", "DeskGame.OtherTarget = free;"),
            "sessions shared desk: the own desk's work never offers a click on the sessions' desk; there it is offered through their panel");
        Check(free.Contains("if (!ReferenceEquals(_desks[i], session))") && free.Contains("return sessionFree ? session : null;"),
            "sessions shared desk: the own desk's work takes a free desk other than the sessions' first, theirs only when no other is free");
        Check(MethodText(own, "public bool WorksAt(EmployeeStationController desk)").Contains("ReferenceEquals(desk, _sharedDesk)")
              && MethodText(own, "public void WorkAt(EmployeeStationController desk)").Contains("DeskClicked(desk);")
              && MethodText(own, "private void DeskDown()").Contains("_sharedDesk = null;"),
            "sessions shared desk: the work chosen there goes the way a click on a free desk goes, refusals said, and is forgotten when the firms stand down");

        string panel = ModSource(@"Sessions\SessionPanel.cs");
        string page = MethodText(panel, "private void Page()");
        string work = MethodText(panel, "private void Work()");
        Check(page.Contains("bool work = SessionBridges.WorkHereNow(_station);") && page.Contains("(work ? 1 : 0)") && page.Contains("\"WorkRealLifeSessionDesk\", \"reallife_session_button_work\"")
              && work.Contains("if (_state != PlayerActivityState.NotStarted || _pressed || _step >= 0)") && ComesBefore(work, "End(false, false);", "_director.WorkAfter(_station);"),
            "sessions shared desk: the panel's first page offers Work here while the work is offered at its desk; pressed, nothing of a session is spent and the work follows");
        string desk = ModSource(@"Sessions\SessionDirector.Desk.cs");
        string wait = MethodText(desk, "private IEnumerator WorkWhenFree(EmployeeStationController desk)");
        Check(wait.Contains("while (SessionGame.PanelBusy && UnityEngine.Time.unscaledTime < until)") && wait.Contains("if (!SessionGame.PanelBusy && On && ReferenceEquals(desk, _desk))")
              && wait.Contains("SessionBridges.WorkNow(desk);") && desk.Contains("private const float WorkWaitSeconds = 2f;"),
            "sessions shared desk: the work is offered once the game's panel has let the session go, two real seconds at most, and only at the desk still held");
        string links = ModSource(@"Wiring\Wiring.SessionLinks.cs");
        Check(links.Contains("TechBridges.SessionDesk = SessionDesk;") && links.Contains("SessionBridges.WorkHere = WorkHere;") && links.Contains("SessionBridges.Work = Work;")
              && links.Contains("TechBridges.SessionDesk = null;") && links.Contains("SessionBridges.WorkHere = null;") && links.Contains("SessionBridges.Work = null;")
              && links.Contains("return SessionDirector.On ? SessionGame.Target : null;") && ModSource(@"Tech\TechBridges.cs").Contains("SessionDesk = null;"),
            "sessions shared desk: wired both ways and taken off again; with Living population or its sessions off there is no sessions' desk and the own work is as before");
        Check(AllIn(new[] { "reallife_session_button_work" }, out string missing), "sessions shared desk: the button's text in en, pt and zh-cn (" + missing + ")");
    }

    // ---------------------------------------------------------------- the texts

    private static void SessionTexts()
    {
        string[] keys =
        {
            "reallife_living_enabled", "reallife_session_enabled", "reallife_opt_tip_living_population", "reallife_opt_tip_living_desks", "reallife_opt_sec_living", "reallife_meta_title_living",
            "reallife_guide_living_about", "reallife_guide_living", "reallife_session_test_meeting", "reallife_session_test_managers", "reallife_session_cta", "reallife_session_label_of",
            "reallife_session_label_choice", "reallife_session_button_begin", "reallife_session_button_next", "reallife_session_button_hand", "reallife_session_button_take",
            "reallife_session_status_desks", "reallife_session_status_own_desk", "reallife_session_status_next", "reallife_session_status_manager_next", "reallife_session_status_manager_due",
            "reallife_session_status_manager_away", "reallife_session_status_due", "reallife_session_refused_fault", "reallife_session_refused_off", "reallife_session_refused_not_due",
            "reallife_session_note_waiting", "reallife_session_note_waiting_many", "reallife_session_note_manager", "reallife_session_note_taken", "reallife_session_note_no_manager",
            "reallife_session_note_handed", "reallife_session_note_unseen", "reallife_session_note_stopped", "reallife_session_test_no_kind", "reallife_session_test_no_firm", "reallife_session_test_go",
            "reallife_session_test_desks", "reallife_session_test_busy", "reallife_session_test_none_handed", "reallife_session_test_none_due", "reallife_session_test_held",
            "reallife_session_test_living_off", "reallife_session_test_off", "reallife_session_test_not_ready", "reallife_session_meeting_title", "reallife_session_meeting_about",
            "reallife_session_meeting_step_aim", "reallife_session_meeting_morale", "reallife_session_meeting_morale_trade", "reallife_session_meeting_push", "reallife_session_meeting_push_trade",
            "reallife_session_meeting_step_length", "reallife_session_meeting_short", "reallife_session_meeting_short_trade", "reallife_session_meeting_full", "reallife_session_meeting_full_trade",
            "reallife_session_meeting_no_people_part", "reallife_session_meeting_nobody", "reallife_session_meeting_done_morale", "reallife_session_meeting_done_push"
        };
        Check(AllIn(keys, out string missing), "sessions texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool fits = true;
        string over = "none";
        foreach (string key in keys)
        {
            if ((key.Contains("_note_") || key.Contains("_test_") || key.Contains("_refused_") || key.Contains("_done_")) && !Fits(en, key))
            {
                fits = false;
                over = key;
            }
        }
        // a manager's notice holds the result's own words: the longest result with the longest names stays a notice
        string worst = en["reallife_session_note_manager"].Replace("{manager}", new string('m', 24)).Replace("{result}", en["reallife_session_meeting_done_push"])
                       .Replace("{session}", en["reallife_session_meeting_title"]).Replace("{business}", new string('b', 30)).Replace("{output}", "+12%").Replace("{people}", "40");
        Check(fits && worst.Length <= 240, "sessions texts: every notice is 240 characters or fewer, a manager's with the result in it too (" + over + ", " + worst.Length + ")");
        bool buttons = true;
        foreach (string key in new[] { "reallife_session_button_begin", "reallife_session_button_next", "reallife_session_button_hand", "reallife_session_button_take", "reallife_session_meeting_morale",
                     "reallife_session_meeting_push", "reallife_session_meeting_short", "reallife_session_meeting_full" })
        {
            buttons &= !en[key].Contains("{");
        }
        Check(buttons, "sessions texts: no button's text has a placeholder, as the game's panel shows a button's key alone");
        Check(en["reallife_guide_living"].Contains("<b>Living population</b>") && en["reallife_guide_living"].Contains("{meeting_push}") && en["reallife_guide_living"].Contains("{session_remind}")
              && en["reallife_guide_living"].Contains("30 days"), "sessions texts: the Guide's page tells of the master option, the desk, managers, the meeting and the one month");
        Check(WhatsNewHas(en, "click your own desk to hold a weekly team meeting") && WhatsNewHas(en, "Broadband and internet bills now use the same 30-day month as tech")
              && WhatsNewHas(LocaleFile("pt"), "reunião semanal da equipe") && WhatsNewHas(LocaleFile("zh-cn"), "每周团队会议"),
            "sessions texts: What's new tells of the desk sessions and the one month, in each of the three languages (any of its rows)");
    }

    // ---------------------------------------------------------------- what it costs

    private static void SessionTimings()
    {
        var file = new SessionFile();
        var kinds = new List<SessionDef>();
        for (int i = 0; i < 4; i++)
        {
            kinds.Add(SessionKind("check_cost_" + i, "type_cost"));
        }
        for (int f = 0; f < 64; f++)
        {
            for (int i = 0; i < 4; i++)
            {
                SessionRecord r = file.Get("Street~" + f, kinds[i].Id);
                r.Held = f % 9;
                r.Delegated = (f & 1) == 0;
            }
        }
        SessionTurn(file, kinds, 10);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int held = SessionTurn(file, kinds, 200);
        double turn = watch.Elapsed.TotalMilliseconds / 200;
        Check(turn < 0.5 && held > 0, "sessions timing: an hour's turn over 64 firms with 4 kinds each costs " + turn.ToString("0.000") + " ms, under 0.5 (it comes once a game hour)");
        SessionStore.Write(file);
        watch.Restart();
        string line = null;
        for (int i = 0; i < 50; i++)
        {
            line = SessionStore.Write(file);
        }
        double write = watch.Elapsed.TotalMilliseconds / 50;
        watch.Restart();
        for (int i = 0; i < 50; i++)
        {
            SessionStore.Read(line, out _);
        }
        double read = watch.Elapsed.TotalMilliseconds / 50;
        Check(write < 2.0 && read < 2.0, "sessions timing: the save's line of 256 records is written in " + write.ToString("0.000") + " ms and read in " + read.ToString("0.000") + " ms, each under 2");
    }

    // An hour's turn as the director takes it, without the game: for each firm and kind, due, then the manager's seeded choices and the callback.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int SessionTurn(SessionFile file, List<SessionDef> kinds, int turns)
    {
        int held = 0;
        var picks = new int[2];
        for (int t = 0; t < turns; t++)
        {
            for (int f = 0; f < 64; f++)
            {
                for (int k = 0; k < kinds.Count; k++)
                {
                    SessionRecord r = file.Find("Street~" + f, kinds[k].Id);
                    if (r == null || !SessionRules.Due(r.Held, 20, kinds[k].EveryDays) || !SessionRules.ManagerHolds(r.Delegated, true, true, 12, kinds[k].Hour))
                    {
                        continue;
                    }
                    var ask = new SessionAsk { Place = r.Place, Day = 20, Hour = 12, ByManager = true, Skill = 60 };
                    SessionRules.ManagerChoices(kinds[k], ask, 5, k, picks);
                    held += kinds[k].Apply(ask, picks).Done ? 1 : 0;
                }
            }
        }
        return held;
    }
}
