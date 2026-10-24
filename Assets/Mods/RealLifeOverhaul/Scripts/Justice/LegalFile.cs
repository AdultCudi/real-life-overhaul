using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The Legal file: where the player stands with the courts, and what can be done about it today. Built when shown.</summary>
    public sealed class LegalFile
    {
        private sealed class Act
        {
            public string Label, Hint;
            public Func<string> Do;     // returns what to say afterwards; null if it closed the file itself
        }

        private readonly JusticeDirector _director;
        private readonly StringBuilder _text = new StringBuilder(900);
        private readonly List<Act> _acts = new List<Act>(8);

        public LegalFile(JusticeDirector director)
        {
            _director = director;
        }

        /// <summary>Where things stand in two or three sentences: what the Legal contact says when rung.</summary>
        public static string Brief()
        {
            int day = TimeHelper.CurrentDay;
            JusticeCase next = JusticeState.NextOpen;
            if (next == null && JusticeState.Owed == 0 && !JusticeState.OnProbation(day) && JusticeState.Suits.Count == 0 && JusticeState.CommunityHours == 0)
            {
                return JusticeText.T("reallife_justice_brief_nothing");
            }
            var text = new StringBuilder(240);
            if (next == null)
            {
                text.Append(JusticeText.T("reallife_justice_brief_no_case"));
            }
            else if (next.Stage == CaseStage.Warrant)
            {
                text.Append(JusticeText.T("reallife_justice_brief_warrant", "number", JusticeText.N(next.Number)));
            }
            else if (next.Stage == CaseStage.Bail)
            {
                text.Append(JusticeText.T("reallife_justice_brief_bail", "number", JusticeText.N(next.Number)));
            }
            else
            {
                text.Append(JusticeText.T(next.CourtDay == day ? "reallife_justice_brief_today" : "reallife_justice_brief_case",
                    "number", JusticeText.N(next.Number), "day", JusticeText.N(next.CourtDay), "charge", JusticeText.Charge(next.Main != null ? next.Main.Id : null)));
            }
            text.Append(' ').Append(JusticeText.T("reallife_justice_brief_counsel", "name", LegalCounsel.Name()));
            if (JusticeState.Owed > 0)
            {
                text.Append(' ').Append(JusticeText.T("reallife_justice_brief_owed", "amount", JusticeText.Money(JusticeState.Owed)));
            }
            if (JusticeState.Suits.Count > 0)
            {
                text.Append(' ').Append(JusticeText.T("reallife_justice_brief_suit"));
            }
            if (JusticeState.CommunityHours > 0)
            {
                text.Append(' ').Append(JusticeText.T("reallife_justice_brief_community", "hours", JusticeText.N(JusticeState.CommunityHours)));
            }
            return text.ToString();
        }

        private static JusticePage Page(string body)
        {
            return new JusticePage
            {
                Scene = JusticeScene.Office,
                Title = JusticeText.T("reallife_justice_file_title"),
                Subtitle = JusticeText.T("reallife_justice_file_subtitle", "day", JusticeText.N(TimeHelper.CurrentDay), "hour", JusticeText.N(TimeHelper.CurrentHour)),
                Body = body
            };
        }

        public void ShowMain()
        {
            int day = TimeHelper.CurrentDay;
            _text.Length = 0;
            bool any = false;
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (!c.Open)
                {
                    continue;
                }
                any = true;
                _text.Append(JusticeText.T("reallife_justice_file_case", "number", JusticeText.N(c.Number), "charges", ChargeList(c))).Append(' ');
                _text.Append(c.Stage == CaseStage.Warrant ? JusticeText.T("reallife_justice_file_case_warrant")
                    : c.Stage == CaseStage.Bail ? JusticeText.T("reallife_justice_file_case_bail")
                    : JusticeText.T(c.CourtDay == day ? "reallife_justice_file_case_today" : "reallife_justice_file_case_date", "day", JusticeText.N(c.CourtDay),
                        "place", _director.CourtPlaceName()));
                if (c.BailPaid > 0)
                {
                    _text.Append(' ').Append(JusticeText.T("reallife_justice_file_case_bail_paid", "amount", JusticeText.Money(c.BailPaid)));
                }
                if (c.DealReady)
                {
                    _text.Append(' ').Append(JusticeText.T("reallife_justice_file_case_deal"));
                }
                if (c.Damages > 0)
                {
                    _text.Append(' ').Append(JusticeText.T("reallife_justice_file_case_damages", "amount", JusticeText.Money(c.Damages)));
                }
                _text.Append('\n');
            }
            if (!any)
            {
                _text.Append(JusticeText.T("reallife_justice_file_no_case")).Append('\n');
            }
            _text.Append(JusticeText.T("reallife_justice_file_counsel", "name", LegalCounsel.Name()));
            if (JusticeState.Lawyer == LawyerKind.Firm)
            {
                _text.Append(" (").Append(JusticeText.Stars(JusticeState.LawyerStars)).Append(')');
            }
            _text.Append('\n');

            if (JusticeState.Debts.Count == 0)
            {
                _text.Append(JusticeText.T("reallife_justice_file_owed_none"));
            }
            else
            {
                _text.Append(JusticeText.T("reallife_justice_file_owed", "amount", JusticeText.Money(JusticeState.Owed)));
                for (int i = 0; i < JusticeState.Debts.Count; i++)
                {
                    JusticeDebt debt = JusticeState.Debts[i];
                    string key = debt.Kind == JusticeDebt.Fee ? "reallife_justice_file_debt_fee"
                        : debt.Kind == JusticeDebt.Civil ? "reallife_justice_file_debt_civil" : "reallife_justice_file_debt_fine";
                    _text.Append("\n  - ").Append(JusticeText.T(key, "left", JusticeText.Money(debt.Left), "perday", JusticeText.Money(Mathf.Min(debt.PerDay, debt.Left))));
                    if (debt.Missed > 0)
                    {
                        _text.Append(' ').Append(JusticeText.T("reallife_justice_file_debt_missed", "missed", JusticeText.N(debt.Missed),
                            "most", JusticeText.N(JusticeLaw.MissedInstalmentsMost)));
                    }
                }
            }
            _text.Append('\n');
            _text.Append(JusticeState.OnProbation(day)
                ? JusticeText.T("reallife_justice_file_probation", "day", JusticeText.N(JusticeState.ProbationUntil))
                : JusticeText.T("reallife_justice_file_probation_none"));
            _text.Append("  ");
            _text.Append(Justice.DrivingSuspended
                ? JusticeText.T("reallife_justice_file_licence_suspended", "day", JusticeText.N(JusticeState.SuspendedUntil))
                : JusticeText.T("reallife_justice_file_licence_ok"));
            if (JusticeState.CommunityHours > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_file_community", "hours", JusticeText.N(JusticeState.CommunityHours),
                    "day", JusticeText.N(JusticeState.CommunityDue)));
            }
            for (int i = 0; i < JusticeState.Suits.Count; i++)
            {
                JusticeSuit suit = JusticeState.Suits[i];
                _text.Append('\n').Append(JusticeText.T(suit.Contested ? "reallife_justice_file_suit_contested" : "reallife_justice_file_suit",
                    "amount", JusticeText.Money(suit.Claim), "day", JusticeText.N(suit.DueDay)));
            }
            JusticeCase appeal = JusticeSettings.Aftermath ? JusticeState.Appealable(day) : null;
            if (appeal != null)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_file_appealable", "number", JusticeText.N(appeal.Number),
                    "day", JusticeText.N(appeal.ClosedDay + JusticeLaw.AppealWindowDays)));
            }
            JusticePage page = Page(_text.ToString())
                .Add(JusticeText.T("reallife_justice_file_actions"), JusticeText.T("reallife_justice_file_actions_hint"))
                .Add(JusticeText.T("reallife_justice_file_record"), JusticeText.T("reallife_justice_file_record_hint", "count", JusticeText.N(JusticeState.Record.Count)))
                .Add(JusticeText.T("reallife_justice_file_close"));
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == 0)
                {
                    BuildActs();
                    ShowActs(0);
                }
                else if (choice == 1)
                {
                    ShowRecord();
                }
                else
                {
                    _director.CloseFile();
                }
            });
        }

        /// <summary>The charges still standing, as shown, comma separated.</summary>
        internal static string ChargeList(JusticeCase c)
        {
            var names = new List<string>(c.Charges.Count);
            for (int i = 0; i < c.Charges.Count; i++)
            {
                if (!c.Charges[i].Dropped)
                {
                    names.Add(JusticeText.Charge(c.Charges[i].Id));
                }
            }
            return string.Join(", ", names);
        }

        private void ShowRecord()
        {
            _text.Length = 0;
            if (JusticeState.Record.Count == 0)
            {
                _text.Append(JusticeText.T("reallife_justice_record_clean"));
            }
            else
            {
                _text.Append(JusticeText.T("reallife_justice_record_head", "count", JusticeText.N(JusticeState.Priors))).Append('\n');
                for (int i = Mathf.Max(0, JusticeState.Record.Count - 10); i < JusticeState.Record.Count; i++)
                {
                    JusticeRecord entry = JusticeState.Record[i];
                    _text.Append("\n  - ").Append(JusticeText.T(entry.Sealed ? "reallife_justice_record_line_sealed" : "reallife_justice_record_line",
                        "day", JusticeText.N(entry.Day), "charge", JusticeText.Charge(entry.ChargeId)));
                }
                if (JusticeSettings.Aftermath && JusticeState.Priors > 0)
                {
                    _text.Append("\n\n").Append(JusticeText.T("reallife_justice_record_seal_when",
                        "day", JusticeText.N(JusticeState.LastConvictionDay + JusticeLaw.SealAfterCleanDays), "days", JusticeText.N(JusticeLaw.SealAfterCleanDays)));
                }
            }
            for (int i = JusticeState.Cases.Count - 1, shown = 0; i >= 0 && shown < 4; i--)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (c.Open)
                {
                    continue;
                }
                if (shown++ == 0)
                {
                    _text.Append("\n\n").Append(JusticeText.T("reallife_justice_record_closed_head"));
                }
                _text.Append("\n  - ").Append(JusticeText.T("reallife_justice_record_closed", "number", JusticeText.N(c.Number), "day", JusticeText.N(c.ClosedDay),
                    "outcome", JusticeText.T(OutcomeKey(c.Outcome))));
            }
            if (JusticeState.Compensated > 0)
            {
                _text.Append("\n\n").Append(JusticeText.T("reallife_justice_record_compensated", "amount", JusticeText.Money(JusticeState.Compensated)));
            }
            JusticePanel.Instance.Show(Page(_text.ToString()).Add(JusticeText.T("reallife_justice_file_back")), _ => ShowMain());
        }

        public static string OutcomeKey(CaseOutcome outcome)
        {
            switch (outcome)
            {
                case CaseOutcome.Dismissed: return "reallife_justice_outcome_dismissed";
                case CaseOutcome.Acquitted: return "reallife_justice_outcome_acquitted";
                case CaseOutcome.Convicted: return "reallife_justice_outcome_convicted";
                case CaseOutcome.Guilty: return "reallife_justice_outcome_guilty";
                case CaseOutcome.Deal: return "reallife_justice_outcome_deal";
                case CaseOutcome.Quashed: return "reallife_justice_outcome_quashed";
                default: return "reallife_justice_outcome_none";
            }
        }

        private void BuildActs()
        {
            _acts.Clear();
            int day = TimeHelper.CurrentDay;
            float cash = Crime.Cash;

            JusticeCase due = _director.HearingDueRemotely();
            if (due != null)
            {
                _acts.Add(new Act
                {
                    Label = JusticeText.T(due.Stage == CaseStage.Warrant ? "reallife_justice_act_surrender" : "reallife_justice_act_video", "number", JusticeText.N(due.Number)),
                    Hint = JusticeText.T("reallife_justice_act_video_hint"),
                    Do = () =>
                    {
                        _director.CloseFile();
                        _director.RequestHearing(due, remote: true);
                        return null;
                    }
                });
            }
            int owed = JusticeState.Owed;
            if (owed > 0 && cash - owed >= JusticeLaw.CashFloor)
            {
                _acts.Add(new Act
                {
                    Label = JusticeText.T("reallife_justice_act_pay_all", "amount", JusticeText.Money(owed)),
                    Hint = JusticeText.T("reallife_justice_act_pay_all_hint"),
                    Do = () => _director.PayOffAll()
                });
            }
            for (int i = 0; i < JusticeState.Suits.Count; i++)
            {
                JusticeSuit suit = JusticeState.Suits[i];
                if (suit.Contested)
                {
                    continue;
                }
                int settle = JusticeLaw.Round10(suit.Claim * 0.6f);
                _acts.Add(new Act
                {
                    Label = JusticeText.T("reallife_justice_act_settle", "amount", JusticeText.Money(settle)),
                    Hint = JusticeText.T("reallife_justice_act_settle_hint", "claim", JusticeText.Money(suit.Claim)),
                    Do = () => _director.SettleSuit(suit)
                });
                _acts.Add(new Act
                {
                    Label = JusticeText.T("reallife_justice_act_contest"),
                    Hint = JusticeText.T("reallife_justice_act_contest_hint", "percent", JusticeText.Percent(_director.SuitWinChance(suit)), "day", JusticeText.N(suit.DueDay),
                        "costs", JusticeText.Money(JusticeLaw.Round10(suit.Claim * 1.15f))),
                    Do = () => _director.ContestSuit(suit)
                });
            }
            JusticeCase appeal = JusticeSettings.Aftermath ? JusticeState.Appealable(day) : null;
            if (appeal != null && !JusticeState.InPrison)
            {
                int fee = JusticeLaw.AppealFee(appeal.FineImposed, cash);
                float chance = _director.AppealChance(appeal);
                _acts.Add(new Act
                {
                    Label = JusticeText.T("reallife_justice_act_appeal", "number", JusticeText.N(appeal.Number)),
                    Hint = JusticeText.T("reallife_justice_act_appeal_hint", "fee", JusticeText.Money(fee), "percent", JusticeText.Percent(chance)),
                    Do = () => _director.Appeal(appeal)
                });
            }
            if (JusticeState.CommunityHours > 0 && JusticeSettings.Community)
            {
                _acts.Add(new Act
                {
                    Label = JusticeText.T("reallife_justice_act_community"),
                    Hint = JusticeText.T("reallife_justice_act_community_hint", "hours", JusticeText.N(JusticeState.CommunityHours),
                        "shift", JusticeText.N(JusticeLaw.CommunityShiftHours)),
                    Do = () =>
                    {
                        _director.CloseFile();
                        _director.StartCommunity(announceRefusal: true);
                        return null;
                    }
                });
            }
        }

        // Two acts a page, or one and "more"; the last answer always goes back.
        private void ShowActs(int from)
        {
            if (_acts.Count == 0)
            {
                JusticePanel.Instance.Show(Page(JusticeText.T("reallife_justice_file_no_actions")).Add(JusticeText.T("reallife_justice_file_back")), _ => ShowMain());
                return;
            }
            if (from >= _acts.Count)
            {
                from = 0;
            }
            Act first = _acts[from];
            bool more = _acts.Count - from > 2;
            Act second = !more && from + 1 < _acts.Count ? _acts[from + 1] : null;
            JusticePage page = Page(JusticeText.T("reallife_justice_file_actions_head")).Add(first.Label, first.Hint);
            if (more)
            {
                page.Add(JusticeText.T("reallife_justice_file_more"), JusticeText.T("reallife_justice_file_more_hint", "count", JusticeText.N(_acts.Count - from - 1)));
            }
            else if (second != null)
            {
                page.Add(second.Label, second.Hint);
            }
            page.Add(JusticeText.T("reallife_justice_file_back"));
            int last = page.Count - 1;
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == last)
                {
                    ShowMain();
                }
                else if (choice == 1 && more)
                {
                    ShowActs(from + 1);
                }
                else
                {
                    Run(choice == 0 ? first : second);
                }
            });
        }

        private void Run(Act act)
        {
            string said;
            try
            {
                said = act.Do();
            }
            catch (Exception e)
            {
                JusticeDirector.Logger?.Error(e);
                said = JusticeText.T("reallife_justice_file_failed");
            }
            if (said == null)
            {
                return;   // the act took the panel over, or closed it
            }
            JusticePanel.Instance.Show(Page(said).Add(JusticeText.T("reallife_justice_continue")), _ => ShowMain());
        }
    }
}
