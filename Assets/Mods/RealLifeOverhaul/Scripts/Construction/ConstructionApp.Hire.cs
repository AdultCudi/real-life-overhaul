using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A builder of the city's hired by the player, in the app: a ready project's quotes page (every builder's price, days,
    /// standing and speciality, the contract's money, and Hire for the one picked, the cheapest or the fastest), the site the
    /// builder runs, and the contract's lines once it is handed over. Written on a page's opening, a pick or a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly TenderBid[] _shownQuotes = new TenderBid[BuildCatalogue.RivalCount];
        private int _quoteFor, _quotePick = -1;

        /// <summary>Opens the app on a ready project's quotes.</summary>
        public void WantQuotes(int project)
        {
            _wantPage = 3;
            Want(ConstructionDirector.TabProjects, project);
        }

        // The quotes page: every builder's quote this week, the one picked set out with its contract's money, and Hire.
        private void WriteQuotes(InfoBox box, ProjectFile p)
        {
            box.Section("reallife_bld_sec_quotes");
            int count = _director.Quotes(p, _shownQuotes);
            if (_quoteFor != p.Id || _quotePick < 0 || _quotePick >= count)
            {
                _quoteFor = p.Id;
                _quotePick = Math.Max(0, TenderRules.Winner(_shownQuotes, count));   // the builder a client would pick on price, time and standing
            }
            box.Note(ConstructionText.T("reallife_bld_quotes_note", "day", ConstructionText.Day(CommissionRules.StandUntil(ConstructionGame.Day))));
            for (int i = 0; i < count; i++)
            {
                box.Line(ConstructionText.T("reallife_bld_quote_line", "builder", RivalName(i), "amount", ConstructionText.Money(CommissionRules.Price(_shownQuotes[i])), "days",
                    ConstructionText.N(_shownQuotes[i].Days)), i == _quotePick ? CellTone.Green : CellTone.Plain);
            }
            if (count == 0)
            {
                BackButton(box);
                return;
            }
            box.Selector(count, _quotePick, i => ConstructionText.T("reallife_bld_sel_builder", "builder", RivalName(i)), i => { _quotePick = i; Refresh(); });
            int r = _quotePick;
            TenderBid bid = _shownQuotes[r];
            long price = CommissionRules.Price(bid);
            box.RowPair("reallife_bld_row_quote", ConstructionText.Money(price), "reallife_bld_row_quote_days", ConstructionText.N(bid.Days));
            box.RowPair("reallife_bld_row_reputation", ConstructionText.N(_director.State.Rivals[r].Reputation), "reallife_bld_col_speciality",
                ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[r]]));
            box.RowPair("reallife_bld_row_deposit", ConstructionText.Money(CommissionRules.Deposit(price)), "reallife_bld_row_retention", ConstructionText.Money(CommissionRules.Retention(price)));
            box.Row("reallife_bld_row_stages", ConstructionText.T("reallife_bld_value_stages", "first", ConstructionText.Money(CommissionRules.StagePay(price, 0)), "second",
                ConstructionText.Money(CommissionRules.StagePay(price, 1)), "third", ConstructionText.Money(CommissionRules.StagePay(price, 2))));
            box.Row("reallife_bld_row_penalty", ConstructionText.T("reallife_bld_value_penalty", "amount", ConstructionText.Money(CommissionRules.Penalty(price, 1)), "most",
                ConstructionText.Money(CommissionRules.Penalty(price, 1000))));
            string block = _director.HireBlock(p);
            if (block != null)
            {
                box.Line(_director.BlockWords(p, block), CellTone.Yellow);
            }
            if (block == null)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_hire_this", () =>
                    Confirm("reallife_bld_confirm_hire", "reallife_bld_confirm_hire_body", RivalName(r), CommissionRules.Deposit(price), () => Hired(_director.HireBuilder(p, r, false)), false));
            }
            Transform picks = box.ButtonRow();
            int cheapest = CommissionRules.Cheapest(_shownQuotes, count), fastest = CommissionRules.Fastest(_shownQuotes, count);
            InfoBox.Button(picks, InfoBox.Negative, "reallife_bld_button_cheapest", () => { _quotePick = cheapest; Refresh(); });
            InfoBox.Button(picks, InfoBox.Negative, "reallife_bld_button_fastest", () => { _quotePick = fastest; Refresh(); });
            InfoBox.Button(picks, InfoBox.Additional, "reallife_bld_button_site", () => { _page = 0; Refresh(); });
            Guide(box);
        }

        // The answer to a hire: the project's own page again once the builder is at work.
        private string Hired(string answer)
        {
            if (answer == "reallife_bld_answer_hired_builder")
            {
                _page = 0;
            }
            return answer;
        }

        // The site a hired builder runs: its progress, the contract's money, the inspections and whether it is running late.
        private void WriteHired(InfoBox box, ProjectFile p)
        {
            box.Section("reallife_bld_sec_builder");
            box.Row("reallife_bld_row_builder", RivalName(p.Contractor));
            double progress = Progress(p);
            box.Meter("reallife_bld_row_progress", (float)progress, Mathf.RoundToInt((float)progress * 100f) + "%", CellTone.Plain);
            box.RowPair("reallife_bld_row_started", ConstructionText.Day(p.StartDay), "reallife_bld_row_due", ConstructionText.Day(p.DueDay));
            box.RowPair("reallife_bld_row_quote", ConstructionText.Money(p.ContractorPrice), "reallife_bld_row_paid", ConstructionText.Money(p.HirePaid));
            for (int i = 0; i < (int)Inspection.Count; i++)
            {
                if (p.Insp[i] == InspectionState.Booked)
                {
                    box.Note(ConstructionText.T("reallife_bld_inspection_booked", "what", ConstructionText.T(BuildCatalogue.InspectionKeys[i]), "day", ConstructionText.Day(p.InspDay[i])));
                }
                else if (p.Insp[i] == InspectionState.Passed)
                {
                    box.Check(ConstructionText.T("reallife_bld_hire_passed", "what", ConstructionText.T(BuildCatalogue.InspectionKeys[i])), true);
                }
            }
            if (p.Rework > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_hire_rework"), CellTone.Yellow);
            }
            int late = ConstructionGame.Day - p.DueDay;
            if (late > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_hire_late_now", "amount", ConstructionText.Money(CommissionRules.Penalty(p.ContractorPrice, late))), CellTone.Yellow);
            }
            box.Note(ConstructionText.T("reallife_bld_hire_site_note"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_money", () => { _page = 1; Refresh(); });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            AbandonRow(box, p);
            Guide(box);
        }

        // A finished building a builder put up: who, the contract and what it paid back for being late.
        private void HiredRows(InfoBox box, ProjectFile p)
        {
            box.Row("reallife_bld_row_builder", RivalName(p.Contractor));
            box.RowPair("reallife_bld_row_quote", ConstructionText.Money(p.ContractorPrice), "reallife_bld_row_paid", ConstructionText.Money(p.HirePaid));
            if (p.HirePenalty > 0)
            {
                box.Row("reallife_bld_row_penalty_paid", ConstructionText.Money(p.HirePenalty), CellTone.Yellow);
            }
        }
    }
}
