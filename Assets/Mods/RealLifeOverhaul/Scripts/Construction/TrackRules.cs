using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>How much a line of the construction objectives waits on the player; the more, the higher it stands. Never saved.</summary>
    public enum TrackNeed : byte
    {
        Waiting = 0,   // under way: something comes on a day, nothing to press
        Yours = 1,     // the next step is the player's: book, order, apply, hire, answer
        Urgent = 2     // put it right now, or it is lost or costs: a failed inspection, a claim, the opening today
    }

    /// <summary>What a press on a line opens in the Construction app. Never saved.</summary>
    public enum TrackOpens : byte
    {
        Project = 0,
        Tender = 1,
        Company = 2,
        Crews = 3
    }

    /// <summary>One line of the construction objectives: what it is about, how much it waits on the player, and its words' parts.</summary>
    public struct TrackLine
    {
        public TrackNeed Need;
        public TrackOpens Opens;
        public int Pick;          // the project's or tender's id the press opens on; 0 for none
        public int Project;       // the project whose place Show on map leads to; 0 for none
        public int Tender;        // the tender whose works Show on map leads to; 0 for none
        public long When;         // the game hour it is about (day x 24 + hour); TrackRules.Never for none
        public int Newest;        // the larger, the newer: the id of what it is about
        public string Key;        // its words' text key
        public string Name;       // {name}: a place, a tender's works or a crew, as words
        public string Builder;    // {builder}, as words
        public string Stage;      // {stage}: a text key
        public string What;       // {what}: a text key (an inspection)
        public int Day, Hour, Percent, Late;   // {day}, {hour}, {percent}, {late}
        public long Amount;       // {amount}
    }

    /// <summary>What the objectives read of the game and the rest of the construction game: the clock, names, a site's share done.</summary>
    public interface ITrackFacts
    {
        int Day { get; }
        int Hour { get; }
        string PlaceOf(ProjectFile p);
        string TenderOf(TenderFile t);
        string CrewOf(CrewFile c);
        string BuilderOf(int rival);
        int Percent(ProjectFile p);       // 0 to 100: its site's work done
        bool CanStart(ProjectFile p);     // the company's crews may start it now
        bool CanHire(ProjectFile p);      // a builder of the city's may be hired for it now
        int ClaimDue(int claim);          // the day a claim must be answered by while it is before the court; -1 once it is not
    }

    /// <summary>
    /// The construction objectives (docs/features/construction.md, "Objectives"): the player's construction matters still
    /// under way, each as one line with its next step and when, most urgent first, then the soonest, then the newest; at
    /// most <see cref="Shown"/> lines and a line saying how many more. Active: a project from its plans to its handover, an
    /// opening still to come, a hired builder's contract, the licence asked for or running out, a tender bid in and still
    /// open, a ring's ask not yet answered, a rival's offer to a crew, a claim over faults before the court. Game-free
    /// (EconomyCheck's TrackChecks); the line list is the caller's. COST: nothing per frame; a pass over the state when it changes.
    /// </summary>
    public static class TrackRules
    {
        public const int Shown = 5;                  // lines before "and N more": the game's objectives stay a glance
        public const long Never = long.MaxValue;     // a line about no hour sorts after every dated one
        public const int MorningHour = 10;           // the inspector's visit and an opening's start, as the director holds them

        private static readonly Comparison<TrackLine> ByNeed = Compare;
        private static readonly StringBuilder Text = new StringBuilder(160);

        /// <summary>Every line active now into the caller's list, in order; the count.</summary>
        public static int Lines(BuildState s, ITrackFacts f, List<TrackLine> into)
        {
            into.Clear();
            if (s == null || f == null)
            {
                return 0;
            }
            int day = f.Day;
            for (int i = 0; i < s.Projects.Count; i++)
            {
                ProjectFile p = s.Projects[i];
                if (p == null)
                {
                    continue;
                }
                if (p.Live)
                {
                    into.Add(ProjectLine(s, p, f));
                    if (p.Hired && p.Stage == ProjectStage.Building)
                    {
                        into.Add(BuilderLine(p, f, day));
                    }
                }
                else if (p.Stage == ProjectStage.Finished && !p.Opened && p.Opening >= day)
                {
                    TrackLine line = About(p, f);
                    line.Need = TrackNeed.Urgent;   // from the handover: the opening is the day after, and nobody else will cut the ribbon
                    line.Key = p.Opening == day ? "reallife_bld_track_opening_today" : "reallife_bld_track_opening";
                    line.Day = p.Opening;
                    line.When = Hour(p.Opening, MorningHour);
                    into.Add(line);
                }
                if (p.ClaimCase > 0)
                {
                    int due = f.ClaimDue(p.ClaimCase);
                    if (due >= 0)
                    {
                        TrackLine line = About(p, f);
                        line.Need = TrackNeed.Urgent;
                        line.Key = "reallife_bld_track_claim";
                        line.Day = due;
                        line.When = Hour(due, 0);
                        into.Add(line);
                    }
                }
            }
            LicenceLine(s.Firm, day, into);
            for (int i = 0; i < s.Tenders.Count; i++)
            {
                TenderFile t = s.Tenders[i];
                if (t == null || t.State != TenderState.Open)
                {
                    continue;
                }
                RingFile ring = s.Ring(t.Id);
                bool asked = ring != null && ring.Asked && ring.Answer == RingFile.NoAnswer;
                if (t.BidPermille <= 0 && !asked)
                {
                    continue;
                }
                into.Add(new TrackLine
                {
                    Need = asked ? TrackNeed.Yours : TrackNeed.Waiting, Opens = TrackOpens.Tender, Pick = t.Id, Tender = t.Id, When = t.Closes, Newest = t.Id,
                    Key = asked ? "reallife_bld_track_ring" : "reallife_bld_track_tender", Name = f.TenderOf(t), Day = t.Closes / 24, Hour = t.Closes % 24
                });
            }
            for (int i = 0; i < s.Crews.Count; i++)
            {
                CrewFile c = s.Crews[i];
                if (c != null && c.PoachRival >= 0 && c.PoachUntil >= day)
                {
                    into.Add(new TrackLine
                    {
                        Need = TrackNeed.Yours, Opens = TrackOpens.Crews, Pick = c.Id, When = Hour(c.PoachUntil, 23), Newest = c.Id, Key = "reallife_bld_track_poach",
                        Name = f.CrewOf(c), Builder = f.BuilderOf(c.PoachRival), Day = c.PoachUntil
                    });
                }
            }
            into.Sort(ByNeed);
            return into.Count;
        }

        /// <summary>The order: more waiting on the player first, then the sooner, then the newer; then its words' key and its pick, so ties never depend on the list's order.</summary>
        public static int Compare(TrackLine a, TrackLine b)
        {
            int c = ((int)b.Need).CompareTo((int)a.Need);
            if (c == 0)
            {
                c = a.When.CompareTo(b.When);
            }
            if (c == 0)
            {
                c = b.Newest.CompareTo(a.Newest);
            }
            if (c == 0)
            {
                c = string.CompareOrdinal(a.Key, b.Key);
            }
            return c != 0 ? c : a.Pick.CompareTo(b.Pick);
        }

        /// <summary>How many lines show of so many, and how many the last line says are left; none past <see cref="Shown"/>.</summary>
        public static int ShownOf(int count, out int more)
        {
            int shown = Math.Min(Math.Max(0, count), Shown);
            more = Math.Max(0, count) - shown;
            return shown;
        }

        /// <summary>A line's words: its key's text with each of its names filled, a day and money in the game's own words.</summary>
        public static string Words(in TrackLine line, Func<string, string> text, Func<int, string> day, Func<long, string> money)
        {
            Text.Clear().Append(text(line.Key) ?? string.Empty);
            Put("{name}", line.Name);
            Put("{builder}", line.Builder);
            Put("{stage}", line.Stage != null ? text(line.Stage) : null);
            Put("{what}", line.What != null ? text(line.What) : null);
            Put("{day}", day(line.Day));
            Put("{hour}", Clock(line.Hour));
            Put("{percent}", line.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Put("{late}", line.Late.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Put("{amount}", money(line.Amount));
            return Text.ToString();
        }

        /// <summary>An hour of the day as the game's clock shows it: "14:00".</summary>
        public static string Clock(int hour)
        {
            int h = ((hour % 24) + 24) % 24;
            return (h < 10 ? "0" : string.Empty) + h.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":00";
        }

        private static void Put(string name, string value)
        {
            Text.Replace(name, value ?? string.Empty);
        }

        private static long Hour(int day, int hour) => (long)day * 24 + hour;

        private static TrackLine About(ProjectFile p, ITrackFacts f)
        {
            return new TrackLine { Opens = TrackOpens.Project, Pick = p.Id, Project = p.Id, When = Never, Newest = p.Id, Name = f.PlaceOf(p) };
        }

        // A project from its plans to its handover: where it stands, and its next step and when.
        private static TrackLine ProjectLine(BuildState s, ProjectFile p, ITrackFacts f)
        {
            TrackLine line = About(p, f);
            switch (p.Stage)
            {
                case ProjectStage.Design:
                    if (!p.PlansDone)
                    {
                        bool asked = p.PlansDay > 0;
                        Step(ref line, !asked ? "reallife_bld_track_plans" : p.Architect == ArchitectKind.Player ? "reallife_bld_track_plans_yours" : "reallife_bld_track_plans_wait",
                            asked && p.Architect != ArchitectKind.Player ? TrackNeed.Waiting : TrackNeed.Yours, asked ? p.PlansDay : -1);
                    }
                    else if (!p.Engineered && PlansRules.NeedsEngineer(p.Storeys, p.Kind))
                    {
                        bool asked = p.EngineerDay > 0;
                        Step(ref line, asked ? "reallife_bld_track_engineer_wait" : "reallife_bld_track_engineer", asked ? TrackNeed.Waiting : TrackNeed.Yours, asked ? p.EngineerDay : -1);
                    }
                    else
                    {
                        Step(ref line, "reallife_bld_track_apply", TrackNeed.Yours, -1);
                    }
                    break;
                case ProjectStage.Planning:
                    switch (p.Planning)
                    {
                        case PlanningState.Appealed:
                            Step(ref line, "reallife_bld_track_appeal", TrackNeed.Waiting, p.AppealDay);
                            break;
                        case PlanningState.Refused:
                            Step(ref line, "reallife_bld_track_refused", TrackNeed.Yours, -1);
                            break;
                        case PlanningState.AppealLost:
                            Step(ref line, "reallife_bld_track_appeal_lost", TrackNeed.Yours, -1);
                            break;
                        default:
                            Step(ref line, "reallife_bld_track_planning", TrackNeed.Waiting, p.DecideDay);
                            break;
                    }
                    break;
                case ProjectStage.Ready:
                {
                    bool start = f.CanStart(p), hire = ConstructionHire(p) && f.CanHire(p);
                    Step(ref line, start && hire ? "reallife_bld_track_start_or_hire" : start ? "reallife_bld_track_start" : hire ? "reallife_bld_track_hire" : "reallife_bld_track_ready",
                        TrackNeed.Yours, -1);
                    break;
                }
                default:
                    SiteLine(s, p, f, ref line);
                    break;
            }
            return line;
        }

        // A plot, a rebuild or an extension of the player's with no contractor yet: a builder of the city's may be hired for it (the director's HireKind).
        private static bool ConstructionHire(ProjectFile p)
        {
            return (p.Kind == ProjectKind.Plot || p.Kind == ProjectKind.Rebuild || p.Kind == ProjectKind.Extension) && p.Contractor < 0;
        }

        private static void Step(ref TrackLine line, string key, TrackNeed need, int onDay)
        {
            line.Key = key;
            line.Need = need;
            line.Day = Math.Max(0, onDay);
            line.When = onDay >= 0 ? Hour(onDay, 0) : Never;
        }

        // A site going up: its stage and share done, and what comes next on it.
        private static void SiteLine(BuildState s, ProjectFile p, ITrackFacts f, ref TrackLine line)
        {
            line.Percent = Math.Min(100, Math.Max(0, f.Percent(p)));
            line.Stage = p.Kind == ProjectKind.Refit || p.Kind == ProjectKind.Conversion   // a change of use's works are a refit's
                ? "reallife_bld_stage_refit" : BuildCatalogue.StageKeys[BuildCatalogue.Clamp(p.StageSeen, 0, BuildCatalogue.StageKeys.Length - 1)];
            int booked = Booked(p);
            if (p.Contractor >= 0 && !p.Hired)
            {
                line.Builder = f.BuilderOf(p.Contractor);
                Step(ref line, "reallife_bld_track_contractor", TrackNeed.Waiting, -1);
                return;
            }
            if (p.Hired)
            {
                line.Builder = f.BuilderOf(p.Contractor);
                if (booked >= 0)
                {
                    line.What = BuildCatalogue.InspectionKeys[booked];
                    Step(ref line, "reallife_bld_track_hired_booked", TrackNeed.Waiting, p.InspDay[booked]);
                    line.When = Hour(p.InspDay[booked], MorningHour);
                }
                else
                {
                    Step(ref line, "reallife_bld_track_hired", TrackNeed.Waiting, p.DueDay);
                }
                return;
            }
            int failed = Failed(p), due = Due(p);
            if (failed >= 0 && p.Rework > 0)
            {
                line.What = BuildCatalogue.InspectionKeys[failed];
                Step(ref line, "reallife_bld_track_failed", TrackNeed.Urgent, -1);
            }
            else if (due >= 0)
            {
                line.What = BuildCatalogue.InspectionKeys[due];
                Step(ref line, "reallife_bld_track_book", TrackNeed.Yours, -1);
            }
            else if (booked >= 0)
            {
                line.What = BuildCatalogue.InspectionKeys[booked];
                Step(ref line, "reallife_bld_track_booked", TrackNeed.Waiting, p.InspDay[booked]);
                line.When = Hour(p.InspDay[booked], MorningHour);
            }
            else if (!SiteRules.Supplied(p.Have, p.Need))
            {
                bool ordered = p.OrderDay > 0;
                Step(ref line, ordered ? "reallife_bld_track_delivery" : "reallife_bld_track_order", ordered ? TrackNeed.Waiting : TrackNeed.Yours, ordered ? p.OrderDay : -1);
                if (ordered)
                {
                    line.When = Hour(p.OrderDay, BuildCatalogue.DeliveryHour);
                }
            }
            else if (s.CrewsOn(p.Id) == 0)
            {
                Step(ref line, "reallife_bld_track_crew", TrackNeed.Yours, -1);
            }
            else
            {
                Step(ref line, "reallife_bld_track_building", TrackNeed.Waiting, p.DueDay);
            }
        }

        // A hired builder's contract: its next payment and on what, the retention once every stage is paid, and the days late.
        private static TrackLine BuilderLine(ProjectFile p, ITrackFacts f, int day)
        {
            TrackLine line = About(p, f);
            line.Builder = f.BuilderOf(p.Contractor);
            line.Late = Math.Max(0, day - p.DueDay);
            line.Newest = p.Id;
            line.Need = TrackNeed.Waiting;
            int next = -1;
            for (int k = 0; k < (int)Inspection.Count && next < 0; k++)
            {
                next = (p.HireStages & (1 << k)) == 0 ? k : -1;
            }
            long penalty = CommissionRules.Penalty(p.ContractorPrice, line.Late);
            if (next >= 0)
            {
                line.What = BuildCatalogue.InspectionKeys[next];
                line.Amount = CommissionRules.StagePay(p.ContractorPrice, next);
                bool booked = p.Insp[next] == InspectionState.Booked;
                line.Key = line.Late > 0 ? booked ? "reallife_bld_track_pay_on_late" : "reallife_bld_track_pay_late" : booked ? "reallife_bld_track_pay_on" : "reallife_bld_track_pay";
                line.Day = booked ? p.InspDay[next] : 0;
                line.When = booked ? Hour(p.InspDay[next], MorningHour) : Hour(p.DueDay, 0);
            }
            else
            {
                line.Amount = CommissionRules.Settle(p.ContractorPrice, p.HirePaid, penalty, out _);
                line.Key = line.Late > 0 ? "reallife_bld_track_retention_late" : "reallife_bld_track_retention";
                line.Day = p.DueDay;
                line.When = Hour(p.DueDay, 0);
            }
            return line;
        }

        // The contractor's licence: City Hall's decision to come, a licence running out or lapsed, a refusal's rest.
        private static void LicenceLine(FirmFile firm, int day, List<TrackLine> into)
        {
            if (firm == null || firm.Founded <= 0)
            {
                return;
            }
            var line = new TrackLine { Opens = TrackOpens.Company, When = Never };
            switch (firm.Licence)
            {
                case LicenceState.Applied:
                    Step(ref line, "reallife_bld_track_licence_wait", TrackNeed.Waiting, firm.LicenceDay + BuildCatalogue.LicenceDays);
                    break;
                case LicenceState.Granted when firm.LicenceUntil - day <= BuildCatalogue.LicenceRemind:
                    Step(ref line, "reallife_bld_track_licence_renew", TrackNeed.Yours, firm.LicenceUntil);
                    break;
                case LicenceState.Granted:
                    return;
                case LicenceState.Refused when day < firm.LicenceDay + BuildCatalogue.LicenceRefusedRest:
                    Step(ref line, "reallife_bld_track_licence_refused", TrackNeed.Waiting, firm.LicenceDay + BuildCatalogue.LicenceRefusedRest);
                    break;
                case LicenceState.Lapsed:
                    Step(ref line, "reallife_bld_track_licence_lapsed", TrackNeed.Yours, -1);
                    break;
                default:
                    Step(ref line, "reallife_bld_track_licence_none", TrackNeed.Yours, -1);
                    break;
            }
            into.Add(line);
        }

        private static int Booked(ProjectFile p) => First(p, InspectionState.Booked);

        private static int Due(ProjectFile p) => First(p, InspectionState.Due);

        private static int Failed(ProjectFile p) => First(p, InspectionState.Failed);

        private static int First(ProjectFile p, InspectionState state)
        {
            for (int i = 0; i < (int)Inspection.Count; i++)
            {
                if (p.Insp[i] == state)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
