using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the old app's pages say, offered to the kit's live source (Ui/Company/CompanyLive) so that both write the same words from the same figures: a decision in a line,
    /// a kind of escalation, a manager's task and what it does, a limit's value. Static, read from the model, nothing of the old page's widgets.
    /// </summary>
    public sealed partial class CompanyApp
    {
        /// <summary>A decision in one line, as the log says it.</summary>
        internal static string DecisionWords(Decision d) => Summary(d);

        // The person, the station they came from and the station they were moved to, where a written week moved somebody (Decision.Detail, "person~from~to"); null for none.
        internal static string[] RotaMoved(Decision d)
        {
            string[] parts = d != null && d.Task == (int)ManagerTask.Rota && !string.IsNullOrEmpty(d.Detail) ? d.Detail.Split('~') : null;
            return parts != null && parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0 && parts[2].Length > 0 ? parts : null;
        }

        /// <summary>A kind of escalation, named.</summary>
        internal static string EscalationName(int kind) => CompanyText.T(EscalationKeys[CompanyCatalogue.Clamp(kind, 0, EscalationKeys.Length - 1)]);

        /// <summary>Where an escalation stands, named.</summary>
        internal static string EscalationStateName(int state) => CompanyText.T(EscalationStateKeys[CompanyCatalogue.Clamp(state, 0, EscalationStateKeys.Length - 1)]);

        /// <summary>A manager's kind of decision, named.</summary>
        internal static string TaskTitle(int task) => TaskName(task);

        /// <summary>What a kind of decision does, in a sentence.</summary>
        internal static string TaskWhat(int task) => CompanyText.T(TaskWhatKeys[CompanyCatalogue.Clamp(task, 0, TaskWhatKeys.Length - 1)]);

        /// <summary>Why a kind given waits, or null where nothing else stops it.</summary>
        internal static string TaskWaits(int task)
        {
            string key = TaskWaitKeys[CompanyCatalogue.Clamp(task, 0, TaskWaitKeys.Length - 1)];
            return key == null ? null : CompanyText.T(key);
        }

        /// <summary>The line under a kind's switch: its limit, or when the rota is written.</summary>
        internal static string TaskLine(ManagerFile m, ManagerTask task) => TaskNote(m, task);

        /// <summary>A place's kind (shop, works, warehouse), named.</summary>
        internal static string PlaceKindName(ManagedKind kind) => KindName(kind);

        /// <summary>A decision's state, named.</summary>
        internal static string DecisionStateName(int state) => CompanyText.T(StateKeys[CompanyCatalogue.Clamp(state, 0, StateKeys.Length - 1)]);

        /// <summary>An escalation in words, from its figures.</summary>
        internal static string EscalationSays(CompanyDirector d, Escalation e)
        {
            switch ((EscalationKind)e.Kind)
            {
                case EscalationKind.Gap:
                    return CompanyText.T("reallife_hier_say_gap", "staff", e.A.ToString(), "keep", e.B.ToString(), "day", e.C.ToString());
                case EscalationKind.Budget:
                    return CompanyText.T("reallife_hier_say_budget", "spent", CompanyText.Money(e.A), "budget", CompanyText.Money(e.B));
                case EscalationKind.Demand:
                    return CompanyText.T(e.C == 1 ? "reallife_hier_say_demand_strike" : "reallife_hier_say_demand", "count", e.A.ToString(), "day", e.B.ToString());
                case EscalationKind.Mood:
                    return CompanyText.T("reallife_hier_say_mood", "standing", e.A.ToString(), "floor", e.B.ToString(), "count", e.C.ToString());
                case EscalationKind.Wave:
                    return CompanyText.T("reallife_hier_say_wave", "count", e.A.ToString(), "gone", e.B.ToString(), "leaving", e.C.ToString());
                case EscalationKind.Kpi:
                    return CompanyText.T("reallife_hier_say_kpi", "profit", CompanyText.Money(e.A), "target", CompanyText.Money(e.B), "days", e.C.ToString());
                case EscalationKind.Exception:
                    return CompanyText.T(e.C == 1 ? "reallife_hier_say_exception_offer" : "reallife_hier_say_exception", "name", e.Detail, "asked", CompanyText.Wage(e.A),
                        "ceiling", CompanyText.Wage(e.B));
                case EscalationKind.Proposal:
                {
                    int field = CompanyCatalogue.Clamp(e.A, 0, (int)PolicyField.Count - 1);
                    return CompanyText.Fill("reallife_hier_say_proposal", "who", e.Detail, "rule", CompanyText.T(PolicyCatalogue.Def((PolicyField)field).TextKey),
                        "from", PolicyStepName((PolicyField)field, e.C), "to", PolicyStepName((PolicyField)field, e.B));
                }
                case EscalationKind.Lease:
                    return CompanyText.Fill("reallife_hier_say_lease", "business", e.Detail, "days", e.C.ToString(), "rent", CompanyText.Money(d.Monthly(e.B)),
                        "share", CompanyText.Share(e.A > 0 ? (int)((e.B - e.A) * 1000L / e.A) : 0));
                default:
                    return string.Empty;
            }
        }

        /// <summary>A rung of the rota planner's ladder, named.</summary>
        internal static string RotaPlanWords(RotaSheet sheet, RotaRung r) => RotaPlanName(sheet, r);

        /// <summary>What a rung needs, in a sentence.</summary>
        internal static string RotaNeedsWords(RotaSheet sheet, RotaRung r) => RotaNeeds(sheet, r);

        /// <summary>A day of the week, as the game names it.</summary>
        internal static string RotaDayWords(int d) => RotaDayName(d);

        /// <summary>An hour as the game writes the time of day.</summary>
        internal static string RotaHourWords(int hour) => RotaHour(hour);

        /// <summary>The hours a day is open, as runs of time.</summary>
        internal static string RotaOpenWords(bool[] open, int d) => RotaHours(open, d);

        /// <summary>Hundredths of money, whole.</summary>
        internal static string RotaMoneyWords(long hundredths) => RotaMoney(hundredths);

        /// <summary>A station group's name.</summary>
        internal static string RotaGroupWords(RotaSheet sheet, int group) => RotaGroup(sheet, group);

        /// <summary>Why there is no rota to plan, from the refusal's key.</summary>
        internal static string RotaRefusedWords(string key) => RotaRefused(key);

        /// <summary>What somebody asks for about their time.</summary>
        internal static string RotaAsksWords(RotaWorker worker) => RotaAsks(worker);

        /// <summary>The hours somebody asks for against what a rung gives: the words, and how it stands (0 plain, 1 inside the range, 2 outside it); null for somebody who asks for none.</summary>
        internal static string RotaRangeWords(RotaSheet sheet, RotaRung r, int worker, out int standing)
        {
            GameUi.CellTone tone;
            string words = RotaRange(sheet, r, worker, out tone);
            standing = tone == GameUi.TableRow.Green ? 1 : tone == GameUi.TableRow.Red ? 2 : 0;
            return words;
        }

        /// <summary>The first demand of somebody that a rota leaves unmet; null if it meets them all.</summary>
        internal static string RotaUnmetWords(RotaSheet sheet, RotaPlan plan, int worker) => RotaUnmetOf(sheet, plan, worker);

        /// <summary>Hours a week a rung gives somebody.</summary>
        internal static int RotaWorkerHours(RotaRung r, int worker) => RotaHoursOf(r, worker);

        /// <summary>A product's name, as the game writes it.</summary>
        internal static string ProductWords(string item) => ProductName(item);

        /// <summary>A rule's step in words.</summary>
        internal static string PolicyStepName(PolicyField field, int step) => StepWords(field, step);
    }
}
