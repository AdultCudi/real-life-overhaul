using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;

// Show on map and the construction objectives (Scripts/Construction: TrackRules, ConstructionDirector.Track, .Map, .MapTests,
// .TrackTests, the app's pages; Scripts/Core/GameUi/TaskGroup): what counts as under way, each line's words as a player
// reads them, the order (most urgent, then the soonest, then the newest), the cap and its "and N more", the claim kept in
// the store; then, over the sources, the one way to the game's destination, the game's own objectives window, nothing per
// frame, the option, the tests, and the texts in every language.
internal static partial class Program
{
    private static void Track()
    {
        TrackActiveChecks();
        TrackWordChecks();
        TrackOrderChecks();
        TrackStoreChecks();
        TrackSourceChecks();
        TrackTextChecks();
    }

    // A stand-in for the game: the clock, names, a site's share done, whether a project may start or be hired for, the court.
    private sealed class FakeTrack : ITrackFacts
    {
        public int Today = 30, Now = 9;
        public int Share = 45;
        public bool Start, HireOk;
        public readonly Dictionary<int, int> Claims = new Dictionary<int, int>();

        public int Day => Today;
        public int Hour => Now;
        public string PlaceOf(ProjectFile p) => p.Id == 1 ? "2 Maple Street" : p.Number + " Summit Avenue";
        public string TenderOf(TenderFile t) => "A rebuild at " + t.Number + " Crest Avenue";
        public string CrewOf(CrewFile c) => "Dan's crew";
        public string BuilderOf(int rival) => "Builder " + rival;
        public int Percent(ProjectFile p) => Share;
        public bool CanStart(ProjectFile p) => Start;
        public bool CanHire(ProjectFile p) => HireOk;
        public int ClaimDue(int claim) => Claims.TryGetValue(claim, out int day) ? day : -1;
    }

    private static Dictionary<string, string> _trackEn;

    private static Dictionary<string, string> TrackEnglish()
    {
        if (_trackEn == null)
        {
            Dictionary<string, string>[] words = LanguageWords(out _);
            _trackEn = words.Length > 0 && words[0] != null ? words[0] : new Dictionary<string, string>();
        }
        return _trackEn;
    }

    private static string TrackWords(TrackLine line)
    {
        Dictionary<string, string> en = TrackEnglish();
        return TrackRules.Words(line, k => k != null && en.TryGetValue(k, out string t) ? t : k ?? string.Empty, d => "Day " + d, m => "$" + m.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static List<TrackLine> TrackOf(BuildState s, FakeTrack f)
    {
        var lines = new List<TrackLine>();
        TrackRules.Lines(s, f, lines);
        return lines;
    }

    private static ProjectFile TrackProject(int id, ProjectKind kind, ProjectStage stage)
    {
        return new ProjectFile { Id = id, Kind = kind, Stage = stage, Number = id, Storeys = 4, DueDay = 60, StageSeen = 2 };
    }

    private static void TrackActiveChecks()
    {
        var f = new FakeTrack();
        var s = new BuildState();
        Check(TrackOf(s, f).Count == 0 && TrackRules.Lines(null, f, new List<TrackLine>()) == 0, "objectives: nothing under way, no line (and no company, no licence line)");
        s.Projects.Add(TrackProject(1, ProjectKind.Plot, ProjectStage.Abandoned));
        ProjectFile done = TrackProject(2, ProjectKind.Plot, ProjectStage.Finished);
        done.Opened = true;
        done.Opening = 20;
        s.Projects.Add(done);
        ProjectFile past = TrackProject(3, ProjectKind.Rebuild, ProjectStage.Finished);
        past.Opening = 25;   // held without the player already, or its day gone
        s.Projects.Add(past);
        Check(TrackOf(s, f).Count == 0, "objectives: an abandoned project, a finished one opened and one whose opening day is gone are not under way");
        past.Opening = 34;
        List<TrackLine> lines = TrackOf(s, f);
        Check(lines.Count == 1 && lines[0].Key == "reallife_bld_track_opening" && lines[0].Need == TrackNeed.Urgent && lines[0].Day == 34, "objectives: an opening still to come is a line, urgent from the handover, on its day");
        past.Opening = 30;
        lines = TrackOf(s, f);
        Check(lines.Count == 1 && lines[0].Key == "reallife_bld_track_opening_today" && lines[0].Need == TrackNeed.Urgent, "objectives: the opening today is urgent: the ribbon to cut");
        s.Projects.Clear();

        // Each stage of a project, one line each, its next step and whose it is.
        ProjectFile p = TrackProject(1, ProjectKind.Plot, ProjectStage.Design);
        s.Projects.Add(p);
        Check(Only(s, f, "reallife_bld_track_plans", TrackNeed.Yours), "objectives: a new project's plans to have drawn are the player's");
        p.PlansDay = 33;
        Check(Only(s, f, "reallife_bld_track_plans_wait", TrackNeed.Waiting, 33), "objectives: plans being drawn by a practice: waiting, ready on their day");
        p.Architect = ArchitectKind.Player;
        Check(Only(s, f, "reallife_bld_track_plans_yours", TrackNeed.Yours), "objectives: plans the player draws: theirs, at the practice");
        p.PlansDone = true;
        p.Storeys = 20;
        Check(Only(s, f, "reallife_bld_track_engineer", TrackNeed.Yours), "objectives: a tall building's engineer's sign off to get");
        p.EngineerDay = 32;
        Check(Only(s, f, "reallife_bld_track_engineer_wait", TrackNeed.Waiting, 32), "objectives: the engineer's sign off coming, on its day");
        p.Engineered = true;
        Check(Only(s, f, "reallife_bld_track_apply", TrackNeed.Yours), "objectives: plans ready: apply at City Hall");
        p.Stage = ProjectStage.Planning;
        p.Planning = PlanningState.Submitted;
        p.DecideDay = 40;
        Check(Only(s, f, "reallife_bld_track_planning", TrackNeed.Waiting, 40), "objectives: planning permission waiting until its day");
        p.Planning = PlanningState.Refused;
        Check(Only(s, f, "reallife_bld_track_refused", TrackNeed.Yours), "objectives: permission refused: appeal or redraw, the player's");
        p.Planning = PlanningState.Appealed;
        p.AppealDay = 44;
        Check(Only(s, f, "reallife_bld_track_appeal", TrackNeed.Waiting, 44), "objectives: an appeal, heard on its day");
        p.Planning = PlanningState.AppealLost;
        Check(Only(s, f, "reallife_bld_track_appeal_lost", TrackNeed.Yours), "objectives: an appeal lost: redraw");
        p.Stage = ProjectStage.Ready;
        Check(Only(s, f, "reallife_bld_track_ready", TrackNeed.Yours), "objectives: ready but neither startable nor hirable: see what it needs");
        f.HireOk = true;
        Check(Only(s, f, "reallife_bld_track_hire", TrackNeed.Yours), "objectives: ready with no company: hire a builder (waiting for a builder)");
        f.Start = true;
        Check(Only(s, f, "reallife_bld_track_start_or_hire", TrackNeed.Yours), "objectives: ready with crews and builders to hire: either");
        f.HireOk = false;
        Check(Only(s, f, "reallife_bld_track_start", TrackNeed.Yours), "objectives: ready with crews only: start");
        p.Kind = ProjectKind.Contract;
        f.HireOk = true;
        Check(Only(s, f, "reallife_bld_track_start", TrackNeed.Yours), "objectives: works won in a tender are never put out to a builder");
        p.Kind = ProjectKind.Plot;

        // A site of the company's own.
        p.Stage = ProjectStage.Building;
        p.Need[0] = 10;
        Check(Only(s, f, "reallife_bld_track_order", TrackNeed.Yours), "objectives: a stage short of materials with none ordered: order them");
        p.OrderDay = 31;
        Check(Only(s, f, "reallife_bld_track_delivery", TrackNeed.Waiting, 31), "objectives: a delivery coming, on its day");
        p.Have[0] = 10;
        Check(Only(s, f, "reallife_bld_track_crew", TrackNeed.Yours), "objectives: supplied but no crew on it: put one on");
        s.Crews.Add(new CrewFile { Id = 9, Project = 1 });
        Check(Only(s, f, "reallife_bld_track_building", TrackNeed.Waiting, 60), "objectives: at work: its share done and the day it is due");
        p.Insp[(int)Inspection.Frame] = InspectionState.Booked;
        p.InspDay[(int)Inspection.Frame] = 35;
        Check(Only(s, f, "reallife_bld_track_booked", TrackNeed.Waiting, 35) && TrackOf(s, f)[0].What == "reallife_bld_insp_frame", "objectives: the next inspection booked, on its day");
        p.Insp[(int)Inspection.Frame] = InspectionState.Due;
        Check(Only(s, f, "reallife_bld_track_book", TrackNeed.Yours), "objectives: an inspection due and not booked: book it");
        p.Insp[(int)Inspection.Frame] = InspectionState.Failed;
        p.Rework = 40;
        Check(Only(s, f, "reallife_bld_track_failed", TrackNeed.Urgent), "objectives: a failed inspection with rework is urgent");
        p.Insp[(int)Inspection.Frame] = InspectionState.NotDue;
        p.Rework = 0;
        f.Share = 140;
        Check(TrackOf(s, f)[0].Percent == 100, "objectives: a share done never past 100%");
        f.Share = 45;

        // A builder of the city's hired: the project's line and the contract's line.
        p.Contractor = 3;
        p.ContractorPrice = 1000000;
        p.Insp[(int)Inspection.Foundation] = InspectionState.Booked;
        p.InspDay[(int)Inspection.Foundation] = 33;
        lines = TrackOf(s, f);
        TrackLine site = lines.Find(l => l.Key == "reallife_bld_track_hired_booked"), pay = lines.Find(l => l.Key == "reallife_bld_track_pay_on");
        Check(lines.Count == 2 && site.Key != null && site.Builder == "Builder 3" && site.Day == 33 && pay.Key != null && pay.Amount == CommissionRules.StagePay(1000000, 0) && pay.Day == 33,
            "objectives: a hired builder's site with its inspection booked, and its contract: the foundations' payment due on that day");
        p.Insp[(int)Inspection.Foundation] = InspectionState.Passed;
        p.HireStages = 1;
        lines = TrackOf(s, f);
        pay = lines.Find(l => l.Key == "reallife_bld_track_pay");
        Check(lines.Exists(l => l.Key == "reallife_bld_track_hired") && pay.Key != null && pay.Amount == CommissionRules.StagePay(1000000, 1) && pay.What == "reallife_bld_insp_frame",
            "objectives: the next payment is the frame's, when its inspection passes");
        f.Today = 63;
        pay = TrackOf(s, f).Find(l => l.Key == "reallife_bld_track_pay_late");
        Check(pay.Key != null && pay.Late == 3, "objectives: a builder past its day says how many days late");
        p.HireStages = 7;
        pay = TrackOf(s, f).Find(l => l.Key == "reallife_bld_track_retention_late");
        Check(pay.Key != null && pay.Amount == CommissionRules.Settle(1000000, p.HirePaid, CommissionRules.Penalty(1000000, 3), out _), "objectives: every stage paid: the retention at the handover, less the penalty");
        f.Today = 30;
        p.Contractor = 1;
        p.Kind = ProjectKind.Refit;
        Check(Only(s, f, "reallife_bld_track_contractor", TrackNeed.Waiting) && TrackOf(s, f)[0].Builder == "Builder 1", "objectives: a refit by a contractor: who, its share, no contract line");
        s.Projects.Clear();
        s.Crews.Clear();

        // The licence, once there is a company.
        s.Firm.Founded = 5;
        Check(Only(s, f, "reallife_bld_track_licence_none", TrackNeed.Yours), "objectives: a company with no licence: apply");
        s.Firm.Licence = LicenceState.Applied;
        s.Firm.LicenceDay = 29;
        Check(Only(s, f, "reallife_bld_track_licence_wait", TrackNeed.Waiting, 29 + BuildCatalogue.LicenceDays), "objectives: a licence asked for: City Hall decides on its day");
        s.Firm.Licence = LicenceState.Granted;
        s.Firm.LicenceUntil = 30 + BuildCatalogue.LicenceRemind + 1;
        Check(TrackOf(s, f).Count == 0, "objectives: a licence held, far from running out: no line");
        s.Firm.LicenceUntil = 30 + BuildCatalogue.LicenceRemind;
        Check(Only(s, f, "reallife_bld_track_licence_renew", TrackNeed.Yours, s.Firm.LicenceUntil), "objectives: a licence running out soon: renew");
        s.Firm.Licence = LicenceState.Refused;
        s.Firm.LicenceDay = 20;
        Check(Only(s, f, "reallife_bld_track_licence_refused", TrackNeed.Waiting, 20 + BuildCatalogue.LicenceRefusedRest), "objectives: a refusal's rest: ask again from its day");
        s.Firm.LicenceDay = 30 - BuildCatalogue.LicenceRefusedRest;
        Check(Only(s, f, "reallife_bld_track_licence_none", TrackNeed.Yours), "objectives: a refusal's rest over: apply");
        s.Firm.Licence = LicenceState.Lapsed;
        Check(Only(s, f, "reallife_bld_track_licence_lapsed", TrackNeed.Yours), "objectives: a lapsed licence: apply again");
        s.Firm.Founded = 0;

        // Tenders bid in, a ring's ask, a rival's offer to a crew, a claim over faults.
        var t = new TenderFile { Id = 50, Number = 12, State = TenderState.Open, Closes = 31 * 24 + 14 };
        s.Tenders.Add(t);
        Check(TrackOf(s, f).Count == 0, "objectives: an open tender with no bid of the player's is not theirs");
        t.BidPermille = 950;
        lines = TrackOf(s, f);
        Check(lines.Count == 1 && lines[0].Key == "reallife_bld_track_tender" && lines[0].Day == 31 && lines[0].Hour == 14 && lines[0].Opens == TrackOpens.Tender && lines[0].Tender == 50,
            "objectives: a tender bid in: when its bids close, opening the tender, its works on the map");
        t.State = TenderState.Won;
        Check(TrackOf(s, f).Count == 0, "objectives: a tender decided is no longer under way");
        t.State = TenderState.Open;
        t.BidPermille = 0;
        s.Rings.Add(new RingFile { Tender = 50, Asked = true });
        Check(Only(s, f, "reallife_bld_track_ring", TrackNeed.Yours), "objectives: builders asking the player to cover: answer before the bids close");
        s.Rings[0].Answer = RingFile.Refused;
        Check(TrackOf(s, f).Count == 0, "objectives: a ring answered and no bid: nothing");
        s.Tenders.Clear();
        s.Rings.Clear();
        s.Crews.Add(new CrewFile { Id = 4, PoachRival = 2, PoachUntil = 32 });
        Check(Only(s, f, "reallife_bld_track_poach", TrackNeed.Yours, 32), "objectives: a rival's offer to a crew: match it by its day");
        s.Crews[0].PoachUntil = 29;
        Check(TrackOf(s, f).Count == 0, "objectives: an offer past its day is gone");
        s.Crews.Clear();
        ProjectFile claimed = TrackProject(6, ProjectKind.Plot, ProjectStage.Finished);
        claimed.Opened = true;
        claimed.ClaimCase = 12;
        s.Projects.Add(claimed);
        Check(TrackOf(s, f).Count == 0, "objectives: a claim the court no longer has is not a line");
        f.Claims[12] = 35;
        Check(Only(s, f, "reallife_bld_track_claim", TrackNeed.Urgent, 35), "objectives: a claim over faults before the court: urgent, answer by its day");
    }

    // True when the state has one line, of that key and need (and day, when given).
    private static bool Only(BuildState s, FakeTrack f, string key, TrackNeed need, int day = -1)
    {
        List<TrackLine> lines = TrackOf(s, f);
        return lines.Count == 1 && lines[0].Key == key && lines[0].Need == need && (day < 0 || lines[0].Day == day);
    }

    private static void TrackWordChecks()
    {
        var f = new FakeTrack { Start = true };
        var s = new BuildState();
        ProjectFile p = TrackProject(1, ProjectKind.Plot, ProjectStage.Building);
        p.Have[0] = p.Need[0] = 10;
        p.Insp[(int)Inspection.Frame] = InspectionState.Booked;
        p.InspDay[(int)Inspection.Frame] = 35;
        s.Projects.Add(p);
        s.Crews.Add(new CrewFile { Id = 1, Project = 1 });
        Check(TrackWords(TrackOf(s, f)[0]) == "2 Maple Street: Frame 45%, the frame inspection on Day 35", "objectives words: a site's line reads its place, stage, share, inspection and day: '" + TrackWords(TrackOf(s, f)[0]) + "'");
        p.Stage = ProjectStage.Planning;
        p.Planning = PlanningState.Submitted;
        p.DecideDay = 40;
        Check(TrackWords(TrackOf(s, f)[0]) == "2 Maple Street: planning permission, decided on Day 40", "objectives words: planning permission waiting: '" + TrackWords(TrackOf(s, f)[0]) + "'");
        p.Stage = ProjectStage.Building;
        p.Contractor = 3;
        p.ContractorPrice = 1000000;
        p.HireStages = 1;   // the foundations paid: the frame's is next
        TrackLine pay = TrackOf(s, f).Find(l => l.Key == "reallife_bld_track_pay_on");
        Check(TrackWords(pay) == "Builder 3: $300,000 due on Day 35, if the frame inspection passes", "objectives words: a builder's next payment: '" + TrackWords(pay) + "'");
        var t = new TenderFile { Id = 8, Number = 12, State = TenderState.Open, BidPermille = 900, Closes = 31 * 24 + 9 };
        s.Projects.Clear();
        s.Tenders.Add(t);
        Check(TrackWords(TrackOf(s, f)[0]) == "Tender, A rebuild at 12 Crest Avenue: bids close on Day 31 at 09:00", "objectives words: a tender's close at the game's clock: '" + TrackWords(TrackOf(s, f)[0]) + "'");
        Check(TrackRules.Clock(0) == "00:00" && TrackRules.Clock(14) == "14:00" && TrackRules.Clock(24) == "00:00" && TrackRules.Clock(-1) == "23:00", "objectives words: an hour as the game's clock shows it");
        // Every key the rules write is in the English, with only the names a line fills, and leaves no name unfilled.
        string rules = ModSource(@"Construction\TrackRules.cs");
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "{name}", "{builder}", "{stage}", "{what}", "{day}", "{hour}", "{percent}", "{late}", "{amount}" };
        int keys = 0;
        string bad = null;
        TrackEnglish();
        foreach (Match m in Regex.Matches(rules, "\"(reallife_bld_track_[a-z_]+)\""))
        {
            keys++;
            string key = m.Groups[1].Value;
            if (!_trackEn.TryGetValue(key, out string text))
            {
                bad = bad ?? key + " (missing)";
                continue;
            }
            foreach (Match n in Regex.Matches(text, @"\{[a-z_]+\}"))
            {
                bad = allowed.Contains(n.Value) ? bad : bad ?? key + " (" + n.Value + ")";
            }
            var line = new TrackLine { Key = key, Name = "N", Builder = "B", Stage = "reallife_bld_stage_frame", What = "reallife_bld_insp_frame", Day = 3, Hour = 4, Percent = 5, Late = 6, Amount = 7 };
            bad = TrackWords(line).Contains("{") ? bad ?? key + " (a name left unfilled)" : bad;
        }
        Check(keys >= 40 && bad == null, "objectives words: the " + keys + " keys of the rules are in en.json with only the names a line fills (" + (bad ?? "all") + ")");
    }

    private static void TrackOrderChecks()
    {
        var f = new FakeTrack { Start = true };
        var s = new BuildState();
        for (int i = 1; i <= 9; i++)
        {
            ProjectFile p = TrackProject(i, ProjectKind.Plot, ProjectStage.Planning);
            p.Planning = PlanningState.Submitted;
            p.DecideDay = 40 + (i % 3);
            s.Projects.Add(p);
        }
        s.Projects[4].Planning = PlanningState.Refused;    // the player's
        s.Projects[7].Planning = PlanningState.Refused;
        ProjectFile urgent = TrackProject(10, ProjectKind.Plot, ProjectStage.Finished);
        urgent.Opening = 30;
        s.Projects.Add(urgent);
        List<TrackLine> lines = TrackOf(s, f);
        Check(lines.Count == 10 && lines[0].Pick == 10 && lines[0].Need == TrackNeed.Urgent && lines[1].Need == TrackNeed.Yours && lines[2].Need == TrackNeed.Yours
              && lines[1].Pick == 8 && lines[2].Pick == 5, "objectives order: the urgent first, then the player's, the newer of equals first: " + Picks(lines));
        bool ordered = true;
        for (int i = 4; i < lines.Count; i++)
        {
            ordered &= lines[i - 1].When < lines[i].When || lines[i - 1].When == lines[i].When && lines[i - 1].Newest > lines[i].Newest;
        }
        Check(ordered && lines[3].Day == 40 && lines[lines.Count - 1].Day == 42, "objectives order: of the waiting, the soonest first, then the newest: " + Picks(lines));
        // The same lines however the state lists them.
        var shuffled = new BuildState();
        for (int i = s.Projects.Count - 1; i >= 0; i--)
        {
            shuffled.Projects.Add(s.Projects[(i * 7) % s.Projects.Count]);
        }
        Check(Picks(TrackOf(shuffled, f)) == Picks(lines), "objectives order: the same order whatever the state's own (" + Picks(TrackOf(shuffled, f)) + ")");
        // The cap.
        int more;
        Check(TrackRules.ShownOf(0, out more) == 0 && more == 0 && TrackRules.ShownOf(1, out more) == 1 && more == 0 && TrackRules.ShownOf(5, out more) == 5 && more == 0
              && TrackRules.ShownOf(6, out more) == 5 && more == 1 && TrackRules.ShownOf(40, out more) == 5 && more == 35 && TrackRules.ShownOf(-3, out more) == 0 && more == 0,
            "objectives cap: up to " + TrackRules.Shown + " lines, the rest counted for \"and N more\", none of none");
        Check(TrackRules.Shown == 5, "objectives cap: five lines, as asked");
        // Cheap: a pass over a busy company.
        var busy = new BuildState();
        busy.Firm.Founded = 1;
        for (int i = 1; i <= 60; i++)
        {
            ProjectFile p = TrackProject(i, ProjectKind.Plot, (ProjectStage)(i % 4));
            p.Contractor = i % 5 == 0 ? 2 : -1;
            busy.Projects.Add(p);
            busy.Tenders.Add(new TenderFile { Id = 100 + i, State = TenderState.Open, BidPermille = i % 2 == 0 ? 900 : 0, Closes = 40 * 24 + i % 24 });
        }
        var into = new List<TrackLine>(128);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        int sum = 0;
        for (int n = 0; n < 2000; n++)
        {
            sum += TrackRules.Lines(busy, f, into);
        }
        timer.Stop();
        Check(sum > 0 && timer.Elapsed.TotalMilliseconds < 2000.0, "objectives cost: 2000 passes over 60 projects and 60 tenders in " + timer.Elapsed.TotalMilliseconds.ToString("0") + " ms (one is written after a change)");
    }

    private static string Picks(List<TrackLine> lines)
    {
        var picks = new List<string>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            picks.Add(lines[i].Pick.ToString());
        }
        return string.Join(",", picks);
    }

    private static void TrackStoreChecks()
    {
        var s = new BuildState { Seed = 3, NextId = 9 };
        s.Projects.Add(new ProjectFile { Id = 4, Kind = ProjectKind.Plot, Stage = ProjectStage.Finished, ClaimCase = 17 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out int version);
        Check(back != null && back.Projects.Count == 1 && back.Projects[0].ClaimCase == 17, "objectives store: a claim's number comes back from the save");
        int cut = line.IndexOf("|17^", StringComparison.Ordinal);
        BuildState old = cut > 0 ? BuildStore.Read(line.Remove(cut, 3), out version) : null;
        Check(old != null && old.Projects.Count == 1 && old.Projects[0].ClaimCase == 0 && old.Projects[0].Id == 4, "objectives store: a line from before the claim's field reads, with none");
    }

    private static void TrackSourceChecks()
    {
        string rules = ModSource(@"Construction\TrackRules.cs"), track = ModSource(@"Construction\ConstructionDirector.Track.cs"), map = ModSource(@"Construction\ConstructionDirector.Map.cs");
        string group = ModSource(@"Core\GameUi\TaskGroup.cs"), main = ModSource(@"Construction\ConstructionDirector.cs"), game = ModSource(@"Construction\ConstructionGame.cs");
        string guide = ModSource(@"Construction\ConstructionGuide.cs"), mapTests = ModSource(@"Construction\ConstructionDirector.MapTests.cs"), trackTests = ModSource(@"Construction\ConstructionDirector.TrackTests.cs");
        string settings = ModSource(@"Core\Settings.cs"), store = ModSource(@"Construction\BuildStore.cs"), day = ModSource(@"Construction\ConstructionDirector.Day.cs");
        Check(rules.Length > 0 && track.Length > 0 && map.Length > 0 && group.Length > 0 && mapTests.Length > 0 && trackTests.Length > 0, "objectives sources: the rules, the director's parts, the group and the tests are there");
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode"), "objectives sources: TrackRules is game-free and draws nothing");
        Check(!Regex.IsMatch(track + map + group + mapTests + trackTests + rules, @"using System\.Linq|\.Where\(|\.Select\(|void Update\(|void LateUpdate\(|FindObjectsOfType|FindObjectsByType|FindObjectOfType"),
            "objectives sources: no LINQ, no frame method, no scene search");
        // Written only when something changed: the director's Changed, each hour's pass and a load mark it; the second looks at the mark.
        string trackSecond = MethodText(track, "private void TrackSecond(");
        Check(MethodText(main, "private void Slow(").Contains("TrackSecond();") && trackSecond.Contains("if (_trackDirty)") && trackSecond.Contains("TrackWrite();")
              && MethodText(main, "internal void Changed(").Contains("TrackChanged();") && MethodText(main, "private void Hourly(").Contains("TrackChanged();")
              && MethodText(main, "private void Load(").Contains("_trackDirty = true;") && MethodText(main, "public void StandDown(").Contains("TrackDown();"),
            "objectives sources: written again only after a change, an hour's pass or a load; put away with the construction game");
        Check(trackSecond.Contains("!Settings.ConstructionTrack") && trackSecond.Contains("_track.Drop();"), "objectives sources: the option off takes the group out of the game's window");
        // The game's own objectives window, by its own call, with its own entries, sized and shown by the game.
        Check(group.Contains("tasks.SetUpTasksGroup(_headKey)") && group.Contains("UnityEngine.Object.Instantiate(tasks.taskEntryTemplate, _group)") && group.Contains("tasks.ScheduleUpdateObjectivesHeight();")
              && group.Contains("root.Find(\"DestinationButton\")") && !Regex.IsMatch(group, @"new GameObject\(|AddComponent<(Image|Text|TextMeshProUGUI|Canvas)>"),
            "objectives sources: a group made by the game's own call in its objectives window, its lines the game's own task entries, nothing drawn of the mod's");
        Check(MethodText(group, "private bool Write(").Contains("if (words != e.Shown)"), "objectives sources: a line whose words did not change is not written again");
        // One way to the game's destination: the construction game's Show on map goes through ConstructionGame.Destination alone.
        string destination = MethodText(game, "internal static bool Destination(");
        Check(destination.Contains("save.customDestination = address;") && destination.Contains("GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);")
              && destination.Contains("save.customDestination = null;") && destination.Contains("look.poiICon, look.poiBackgroundColor, DirectionGuiderType.Destination") && destination.Contains("uis.fullMenu.Toggle(false);"),
            "show on map sources: the game's own Set destination: an address kept in the save, or a spot with the guider's own icon, the menu closed");
        int guiders = 0;
        foreach (string name in new[] { "ConstructionDirector.Map.cs", "ConstructionDirector.Track.cs", "ConstructionApp.cs", "ConstructionApp.Land.cs", "ConstructionApp.Projects.cs", "ConstructionApp.Tenders.cs", "ConstructionApp.Refit.cs" })
        {
            guiders += Regex.Matches(ModSource(@"Construction\" + name), @"SetGuiderTarget|customDestination").Count;
        }
        string show = MethodText(map, "private string Show(");
        Check(guiders == 0 && show.Contains("ConstructionGame.Destination(place.Street, place.Number, place.Spot, words, place.Building)") && show.Contains("\"reallife_bld_map_set\""),
            "show on map sources: every press goes the one way, ConstructionGame.Destination, and a notice of the game's says where");
        Check(MethodText(map, "internal static Vector3 LotFront(").Contains("lot.Depth * 0.5f + FrontOut") && ModSource(@"Construction\ConstructionDirector.App.cs").Contains("place = LotFront(lot);"),
            "show on map sources: an empty lot is marked at its front on its street, where its ribbon is cut");
        string projects = ModSource(@"Construction\ConstructionApp.Projects.cs"), land = ModSource(@"Construction\ConstructionApp.Land.cs");
        Check(MethodText(projects, "private void WriteProject(").Contains("MapRow(box, () => _director.ShowProject(p));") && Regex.Matches(land, @"MapRow\(box, \(\) => _director\.Show(Lot|Building)\(").Count == 4
              && ModSource(@"Construction\ConstructionApp.Tenders.cs").Contains("MapRow(box, () => _director.ShowTender(t));") && ModSource(@"Construction\ConstructionApp.Refit.cs").Contains("MapRow(box, () => _director.ShowBuilding(where, at));"),
            "show on map sources: on every project's pages, a lot for sale or owned, a run of spaces, a landmark's site, a building, a business and a tender");
        // The option, on by default, read, in the tree and the feature's own.
        Check(settings.Contains("public static bool ConstructionTrack = true;") && settings.Contains("ConstructionTrack = Bool(modId, ConstructionTrackId, ConstructionTrack);")
              && ModSource(@"Meta\OptionsTree.cs").Contains(".Opt(\"construction_track\", \"construction\")") && guide.Contains(".Owns(Settings.ConstructionTrackId)")
              && guide.Contains("AddToggle(Settings.ConstructionTrackId, \"reallife_bld_track_option\""), "objectives sources: the option is on by default, read, in the tree and the feature's own");
        // The claim's number: kept after every field an older reader knows, set when a claim is filed.
        Check(store.Contains("t.Append('|').Append(p.ClaimCase.ToString(Inv));") && store.IndexOf("p.ClaimCase.ToString(Inv)", StringComparison.Ordinal) > store.IndexOf("p.HirePenalty.ToString(Inv)", StringComparison.Ordinal)
              && MethodText(day, "private void Defect(").Contains("p.ClaimCase = Math.Max(0, number);"), "objectives sources: a claim's number is written last in a project's record and set as the claim is filed");
        // The tests: public static methods, buttons of the feature, checks of the run, each line PASS or FAIL.
        Check(guide.Contains(".Check(\"reallife_bld_test_map\", () => ConstructionDirector.TestMapNow(), \"Construction check (show on map)\", RunPlace.Here)")
              && guide.Contains(".Check(\"reallife_bld_test_track\", () => ConstructionDirector.TestTrackNow(), \"Construction check (objectives)\", RunPlace.Here)")
              && mapTests.Contains("TrackTest(\"show on map\"") && trackTests.Contains("TrackTest(\"objectives\""), "objectives sources: the two tests are buttons and checks of the run, writing the lines the run reads");
        Check(Regex.Matches(mapTests + trackTests, @"\(set \? "": PASS\."" : "": FAIL\.""\)|\(pass \? "": PASS\."" : "": FAIL\.""\)").Count == 2, "objectives sources: each test ends its line PASS or FAIL");
        Check(!Regex.IsMatch(track + map + group, @"GoingPublic\.Send|Network|Rpc"), "objectives sources: nothing sent: the lines are the local player's own, on this machine");
    }

    private static void TrackTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in new[] { @"Construction\TrackRules.cs", @"Construction\ConstructionDirector.Track.cs", @"Construction\ConstructionDirector.Map.cs", @"Construction\ConstructionDirector.MapTests.cs",
                     @"Construction\ConstructionDirector.TrackTests.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_bld_button_map");
        keys.Add("reallife_bld_track_option");
        keys.Add("reallife_opt_tip_construction_track");
        keys.Add("reallife_bld_test_map");
        keys.Add("reallife_bld_test_track");
        Check(keys.Contains("reallife_bld_map_set") && keys.Contains("reallife_bld_track_claim") && keys.Count > 55, "objectives texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("objectives texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "objectives texts: every key of Show on map and the objectives is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in new[] { "reallife_bld_map_set", "reallife_bld_map_set_plain", "reallife_bld_map_none", "reallife_bld_map_refused", "reallife_bld_test_map_none", "reallife_bld_test_track_shown",
                     "reallife_bld_test_track_none" })
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            Check(text.Length > 0 && text.Length + 60 <= 240, "objectives texts: the notice " + key + " stays under 240 characters with a long address and district (" + text.Length + ")");
        }
        var guideLines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "- <b>Show on map</b>, under the title of a lot" }, { "pt", "- <b>Mostrar no mapa</b>, abaixo do título" }, { "zh-cn", "点<b>在地图上显示</b>" }
        };
        var objectiveLines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "- What is under way shows in the game's objectives at the top left" }, { "pt", "- O que está em andamento aparece nos objetivos do jogo" }, { "zh-cn", "- 正在进行的事务显示在左上角的游戏目标里" }
        };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            int line = guideLines.TryGetValue(codes[l], out string start) ? page.IndexOf(start, StringComparison.Ordinal) : -1;
            int box = objectiveLines.TryGetValue(codes[l], out string second) ? page.IndexOf(second, StringComparison.Ordinal) : -1;
            Check(line > 0 && box > line && page.IndexOf("\n# ", StringComparison.Ordinal) < line, "objectives texts: the Guide's construction page in " + codes[l] + " has Show on map and the objectives, after its first section");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "The Construction app now shows where things are" }, { "pt", "O app Construção agora mostra onde ficam as coisas" }, { "zh-cn", "建筑公司应用现在能显示东西在哪里" },
            { "de", "Die Bau-App zeigt jetzt, wo alles ist" }, { "fr", "L'application Construction montre maintenant où sont les choses" }, { "es", "La aplicación Construcción ahora muestra dónde está cada cosa" },
            { "ru", "Приложение «Строительство» теперь показывает, где что находится" }, { "nl", "De Bouw-app laat nu zien waar alles is" }, { "tr", "İnşaat uygulaması artık her şeyin nerede olduğunu gösteriyor" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            bool found = false;
            foreach (KeyValuePair<string, string> row in words[l])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && starts.TryGetValue(codes[l], out string start) && row.Value.Contains(start);
            }
            Check(found, "objectives texts: What's new has Show on map and the objectives in some reallife_changes_ row in " + codes[l]);
        }
    }
}
