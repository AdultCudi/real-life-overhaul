using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What running a batch came to: how many were done and which ones were not, each with its reason.</summary>
    public sealed class BatchOutcome
    {
        public int Done;
        public readonly List<int> Failed = new List<int>(4);              // the places of those not done, in the order given
        public readonly List<BatchState> Why = new List<BatchState>(4);   // and why, one for each
    }

    /// <summary>
    /// The rules of a batch on My Employees (a raise for several, hiring several), game-free: the review sheet's lines and
    /// totals, who a hire may reach, the run that goes on past a failure, and the words of what was left out. The raise
    /// itself is StaffRules' (one raise, as the Staff tab gives it); the hire is the game's (EmployeeHelper.HireCandidate).
    /// This only decides, shows and counts. Cost: a pass over the picked, on the player's click.
    /// </summary>
    public static class StaffBatchRules
    {
        /// <summary>Characters of the list of those left out that a notice may carry, so the whole stays under 240.</summary>
        public const int ListBudget = 120;

        /// <summary>The text key of why somebody is left out; null for Ready.</summary>
        public static string ReasonKey(BatchState state)
        {
            switch (state)
            {
                case BatchState.Rested: return "reallife_staff_batch_why_rested";
                case BatchState.Waiting: return "reallife_staff_batch_why_waiting";
                case BatchState.AtRate: return "reallife_staff_batch_why_at_rate";
                case BatchState.Unknown: return "reallife_staff_batch_why_unknown";
                case BatchState.NoFit: return "reallife_staff_batch_why_no_fit";
                case BatchState.NotYours: return "reallife_staff_batch_why_not_yours";
                case BatchState.Gone: return "reallife_staff_batch_why_gone";
                case BatchState.Failed: return "reallife_staff_batch_why_failed";
                default: return null;
            }
        }

        /// <summary>
        /// Whether one picked candidate may be hired to a business, by the game's own order of questions: still a candidate, the
        /// business and the candidate the player's (Going Public holds another player's), and one of their skills a skill the
        /// business needs (the game's "assign to business and hire" refuses a candidate without one).
        /// </summary>
        public static BatchState HireVerdict(bool candidate, bool yours, bool fits)
        {
            return !candidate ? BatchState.Gone : !yours ? BatchState.NotYours : !fits ? BatchState.NoFit : BatchState.Ready;
        }

        /// <summary>
        /// Runs a batch one person at a time. <paramref name="one"/> does the person at that place and says Ready when it was done,
        /// or why not; one that throws is Failed. Nobody stops the others: every place is tried.
        /// </summary>
        public static BatchOutcome Run(int count, Func<int, BatchState> one, Action<int, Exception> fault = null)
        {
            var outcome = new BatchOutcome();
            for (int i = 0; i < count; i++)
            {
                BatchState state;
                try
                {
                    state = one(i);
                }
                catch (Exception e)
                {
                    state = BatchState.Failed;
                    fault?.Invoke(i, e);
                }
                if (state == BatchState.Ready)
                {
                    outcome.Done++;
                }
                else
                {
                    outcome.Failed.Add(i);
                    outcome.Why.Add(state);
                }
            }
            return outcome;
        }

        /// <summary>
        /// The items one after another, as many as fit the budget in characters, then "and N more" (<paramref name="more"/> has
        /// {count}). At least one is always named.
        /// </summary>
        public static string Listed(IList<string> items, string more, int budget)
        {
            int n = items?.Count ?? 0;
            if (n == 0)
            {
                return string.Empty;
            }
            string tailWords = more ?? "and {count} more";
            var text = new System.Text.StringBuilder(budget + 32);
            int shown = 0;
            for (int i = 0; i < n; i++)
            {
                string item = items[i] ?? string.Empty;
                int tail = i + 1 < n ? tailWords.Length + 1 : 0;   // the room "and N more" would need after this one
                if (shown > 0 && text.Length + 2 + item.Length + tail > budget)
                {
                    break;
                }
                if (shown > 0)
                {
                    text.Append(", ");
                }
                text.Append(item);
                shown++;
            }
            if (shown < n)
            {
                text.Append(' ').Append(tailWords.Replace("{count}", (n - shown).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            return text.ToString();
        }

        /// <summary>
        /// The review of a raise for several picked: a line for each, in the order they were picked, from the plan's own lines, and
        /// the plan's totals. The four lists are the picked employees' ids, names, main skills (the game's key) and businesses, in
        /// the same order as the plan's lines.
        /// </summary>
        public static StaffSheet RaiseSheet(IList<string> ids, IList<string> names, IList<string> roles, IList<string> places, IList<PickedLine> lines, PickedPlan plan, string subject)
        {
            int n = lines != null ? lines.Count : 0;
            var sheet = new StaffSheet
            {
                Hire = false, Subject = subject, Picked = plan.Picked, Ready = plan.Raised, Left = plan.Left,
                Now = plan.Before, Next = plan.After, DayNow = plan.DayBefore, DayNext = plan.DayAfter, Lines = new StaffSheetLine[n]
            };
            for (int i = 0; i < n; i++)
            {
                PickedLine l = lines[i];
                int next = l.Now + l.Raise;
                sheet.Lines[i] = new StaffSheetLine
                {
                    Id = At(ids, i), Name = At(names, i), RoleKey = At(roles, i), Business = At(places, i), Now = l.Now, Next = next,
                    DayNow = Day(l.Now, l.Hours), DayNext = Day(next, l.Hours), State = l.State
                };
            }
            return sheet;
        }

        /// <summary>A working day's pay, hundredths, for a wage an hour (hundredths) and the day's hours (hundredths of an hour): the plan's own sum.</summary>
        public static int Day(int wage, int hours) => (int)Math.Min(int.MaxValue, (long)wage * Math.Max(0, hours) / 100);

        /// <summary>
        /// The review of hiring several picked candidates to one business: their asking wage unchanged, the business named, and the
        /// verdict of each. The totals count those who will be hired.
        /// </summary>
        public static StaffSheet HireSheet(IList<string> ids, IList<string> names, IList<string> roles, IList<int> wages, IList<BatchState> verdicts, string business)
        {
            int n = verdicts != null ? verdicts.Count : 0;
            var sheet = new StaffSheet { Hire = true, Subject = business, Picked = n, Lines = new StaffSheetLine[n] };
            for (int i = 0; i < n; i++)
            {
                int wage = wages != null && i < wages.Count ? wages[i] : 0;
                BatchState state = verdicts[i];
                sheet.Lines[i] = new StaffSheetLine { Id = At(ids, i), Name = At(names, i), RoleKey = At(roles, i), Business = business, Now = wage, Next = wage, State = state };
                if (state == BatchState.Ready)
                {
                    sheet.Ready++;
                    sheet.Now += wage;
                    sheet.Next += wage;
                }
                else
                {
                    sheet.Left++;
                }
            }
            return sheet;
        }

        private static string At(IList<string> list, int i) => list != null && i >= 0 && i < list.Count ? list[i] : null;
    }
}
