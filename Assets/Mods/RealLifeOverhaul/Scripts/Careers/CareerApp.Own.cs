using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Own work tab: freelance contracts (offers to take, work to do, what was delivered), the player's practice, the
    /// way out of a job to found a company or open a practice, former colleagues waiting to join a firm of the player's, and
    /// a restraint clause that still binds.
    /// </summary>
    public sealed partial class CareerApp
    {
        public const string ContractRow = "contract:";
        private readonly List<int> _practices = new List<int>(8);
        private int _practicePick;

        private TableSpec WriteOwn(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            if (!CareersDirector.OwnOn)
            {
                _picked = null;
                box.Title(CareersText.T("reallife_own_title"));
                box.Line(CareersText.T("reallife_own_off"), CellTone.Yellow);
                Meta.Guide.Point(box, OwnGuide);
                return CareerColumns.Own;
            }
            Contract picked = null;
            int id = PickedId(_picked, ContractRow);
            for (int i = w.Contracts.Count - 1; i >= 0; i--)
            {
                Contract c = w.Contracts[i];
                TableRow row = Row(ContractRow + c.Id);
                row.Set(0, c.Client);
                row.Set(1, CareersText.ProName(c.Prof));
                row.Set(2, CareersText.T("reallife_own_row_hours", "done", (c.Minutes / 60).ToString(), "of", c.Hours.ToString()), c.Hours);
                row.Set(3, CareersText.Money(Prices.At((int)c.Fee)), c.Fee);
                row.Set(4, CareersText.Day(c.Due), c.Due, c.Stage == (int)ContractStep.Taken && day > c.Due ? TableRow.Red : TableRow.Plain);
                row.Set(5, CareersText.ContractState(c.At),
                    c.Stage == (int)ContractStep.Offered ? TableRow.Green : c.Stage == (int)ContractStep.Taken ? TableRow.Plain : TableRow.Yellow);
                row.Kind = c.Stage <= (int)ContractStep.Taken ? CareerColumns.ContractOpen : CareerColumns.ContractDone;
                picked = c.Id == id ? c : picked;
            }
            if (picked != null)
            {
                WriteContract(box, picked, day);
                return CareerColumns.Own;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_own_title"));
            box.Note(CareersText.T("reallife_own_about"));
            Job j = w.Job;
            if (w.RestraintUntil >= day)
            {
                box.Line(CareersText.T(w.ClaimNumber > 0 ? "reallife_own_restraint_claimed" : "reallife_own_restraint_line", "employer", CareersText.Employer(w.RestraintEmployer),
                    "day", CareersText.Day(w.RestraintUntil)), w.ClaimNumber > 0 ? CellTone.Red : CellTone.Yellow);
            }
            if (j != null && j.NoticeEnd <= 0 && j.Exclusive)
            {
                box.Line(CareersText.T("reallife_own_exclusive"), CellTone.Yellow);
            }
            // The practice.
            box.Section("reallife_look_sec_practice");
            if (w.Practice.Open)
            {
                box.Row("reallife_career_col_profession", CareersText.ProName(w.Practice.Prof));
                box.Row("reallife_look_since", CareersText.Day(w.Practice.Opened));
                box.Meter("reallife_look_name", w.Practice.Reputation / 100f, CareersText.T("reallife_look_of", "value", w.Practice.Reputation.ToString(), "most", "100"), CellTone.Plain);
                box.RowPair("reallife_look_delivered", w.Practice.Served.ToString(), "reallife_look_late", w.Practice.Late.ToString());
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_own_button_close_practice", () => Do(_director.ClosePractice));
            }
            else
            {
                _practices.Clear();
                for (int i = 0; i < ProCatalogue.All.Length; i++)
                {
                    if (OwnRules.CanPractise(ProCatalogue.All[i], f))
                    {
                        _practices.Add(i);
                    }
                }
                if (_practices.Count == 0)
                {
                    box.Note(CareersText.T("reallife_own_practice_none"));
                }
                else
                {
                    _practicePick = Mathf.Clamp(_practicePick, 0, _practices.Count - 1);
                    if (_practices.Count > 1 && box.HasSelector)
                    {
                        box.Selector(_practices.Count, _practicePick, i => CareersText.ProName(_practices[i]), i =>
                        {
                            _practicePick = i;
                            Refresh();
                        });
                    }
                    int prof = _practices[_practicePick];
                    box.Line(CareersText.T("reallife_own_practice_open", "profession", CareersText.ProName(prof), "fee", CareersText.Money(Prices.At(OwnRules.RegisterFee(ProCatalogue.Find(prof))))));
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_own_button_open_practice", () => Do(() => _director.OpenPracticeNow(prof)));
                }
            }
            // A founder's record, colleagues waiting, a restraint clause.
            if (w.FounderDay > 0 || (w.ColleaguesCount > 0 && w.ColleaguesUntil >= day))
            {
                box.Section("reallife_look_sec_founding");
            }
            if (w.FounderDay > 0)
            {
                box.Line(CareersText.Fill("reallife_own_founder_line", "employer", CareersText.Employer(w.FounderFrom), "day", CareersText.Day(w.FounderDay),
                    "bonus", (_director.FounderBonus() / 10).ToString() + "%"));
            }
            if (w.ColleaguesCount > 0 && w.ColleaguesUntil >= day)
            {
                box.Line(CareersText.T("reallife_own_colleagues_line", "count", w.ColleaguesCount.ToString(), "day", CareersText.Day(w.ColleaguesUntil)));
                if (_director.ColleaguesFirm() != null)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_own_button_colleagues", () => Do(_director.BringColleagues));
                }
                else
                {
                    box.Note(CareersText.T("reallife_own_colleagues_no_firm"));
                }
            }
            // The way out of a job.
            if (j != null && j.NoticeEnd <= 0)
            {
                Transform leave = box.ButtonRow();
                if (_director.CanFoundLeave(j) == null)
                {
                    InfoBox.Button(leave, InfoBox.Additional, "reallife_own_button_found", () => Confirm("reallife_own_confirm_found_body", () => _director.FoundLeave(false)));
                    InfoBox.Button(leave, InfoBox.Additional, "reallife_own_button_found_colleagues", () => Confirm("reallife_own_confirm_colleagues_body", () => _director.FoundLeave(true)));
                }
                if (_director.CanPracticeLeave(j) == null)
                {
                    InfoBox.Button(leave, InfoBox.Additional, "reallife_own_button_practice_leave", () => Confirm("reallife_own_confirm_practice_body", _director.PracticeLeave));
                }
            }
            Meta.Guide.Point(box, OwnGuide);
            return CareerColumns.Own;
        }

        private void Confirm(string bodyKey, System.Func<string> act)
        {
            Job j = _director.Work != null ? _director.Work.Job : null;
            string employer = j != null ? CareersText.Employer(j.Post.Employer) : string.Empty;
            GameConfirm.Show("reallife_own_confirm_title".Localize(new { employer }), bodyKey.Localize(new { days = j != null ? ProRules.NoticeDays(j.Rank, j.Post.Rank).ToString() : "0" }),
                () => UiClone.Safe(() => Do(act)), null, null, null, true, true);
        }

        private void WriteContract(InfoBox box, Contract c, int day)
        {
            int id = c.Id;
            box.Title(c.Client);
            box.Subtitle(CareersText.ProName(c.Prof));
            bool late = c.Stage == (int)ContractStep.Taken && day > c.Due;
            box.Chip(CareersText.ContractState(c.At),
                c.Stage == (int)ContractStep.Offered ? CellTone.Green : c.Stage == (int)ContractStep.Taken ? (late ? CellTone.Red : CellTone.Plain) : CellTone.Yellow);
            box.Section("reallife_look_sec_terms");
            box.Row("reallife_own_col_hours", c.Hours.ToString());
            box.Row("reallife_own_col_fee", CareersText.Money(Prices.At((int)c.Fee)));
            box.Row("reallife_own_col_due", CareersText.Day(c.Due), late ? CellTone.Red : CellTone.Plain);
            switch (c.At)
            {
                case ContractStep.Offered:
                    {
                        box.Row("reallife_look_offer_stands", CareersText.Day(c.Lapse));
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_own_button_take", () => Do(() => _director.TakeContract(id)));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_career_button_decline", () => Do(() => _director.DropContract(id)));
                        break;
                    }
                case ContractStep.Taken:
                    {
                        box.Meter("reallife_look_work_done", c.Hours > 0 ? c.Minutes / (c.Hours * 60f) : 1f, CareersText.T("reallife_own_contract_left", "time", CareersText.Minutes(OwnRules.Left(c))), late ? CellTone.Red : CellTone.Plain);
                        string why = _director.CanWorkContract(c);
                        if (why != null)
                        {
                            box.Line(CareersText.T(why), CellTone.Yellow);
                        }
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_own_button_work", () => Do(() => _director.WorkContract(id)));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_own_button_drop", () => Do(() => _director.DropContract(id)));
                        break;
                    }
                default:
                    box.Note(CareersText.T("reallife_own_contract_over"));
                    break;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, OwnGuide);
        }
    }
}
