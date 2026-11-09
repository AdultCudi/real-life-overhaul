using System;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A builder of the city's hired by the player, in the app: a ready project's quotes page (every builder's price, days,
    /// standing and speciality, the contract's money, and Hire for the one picked, the cheapest or the fastest), the site the
    /// builder runs, and the contract's lines once it is handed over. Written on a page's opening, a pick or a press.
    /// </summary>
    public sealed partial class ConstructionLive
    {
        private readonly TenderBid[] _shownQuotes = new TenderBid[BuildCatalogue.RivalCount];
        private int _quoteFor, _quotePick = -1;

        /// <summary>Opens the app on a ready project's quotes.</summary>
        public void WantQuotes(int project)
        {
            _wantPage = 3;
            Want(ConstructionDirector.TabProjects, project);
        }

        // The quotes page: every builder's quote this week as a table to compare (price, days, the quality the inspector sees, the risk and the jobs it has done for the player),
        // the one picked set out with its contract's money and what it means against the others, and Hire.
        private void WriteQuotes(KitBox box, ProjectFile p)
        {
            int count = Director.Quotes(p, _shownQuotes);
            if (_quoteFor != p.Id || _quotePick < 0 || _quotePick >= count)
            {
                _quoteFor = p.Id;
                _quotePick = Math.Max(0, Licensed(p, count));   // the builder a client would pick on price, time and standing, among those licensed for the floors
            }
            box.Note(ConstructionText.T("reallife_bld_quotes_note", "day", ConstructionText.Day(CommissionRules.StandUntil(ConstructionGame.Day))));
            QuotesTable(p, count);
            if (count == 0)
            {
                BackButton(box);
                return;
            }
            int r = _quotePick;
            TenderBid bid = _shownQuotes[r];
            long price = CommissionRules.Price(bid);
            bool licensed = LevelRules.Allows(Director.RivalLevel(r), p.Storeys);
            box.Section("reallife_ui_cons_sec_quote");
            box.RowPair("reallife_bld_row_quote", ConstructionText.Money(price), "reallife_bld_row_quote_days", ConstructionText.N(bid.Days));
            box.RowPair("reallife_bld_row_reputation", ConstructionText.N(Director.State.Rivals[r].Reputation), "reallife_bld_col_speciality",
                ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[r]]));
            box.RowPair("reallife_bld_row_deposit", ConstructionText.Money(CommissionRules.Deposit(price)), "reallife_bld_row_retention",
                ConstructionText.Money(CommissionRules.Retention(price)));
            box.Row("reallife_bld_row_stages", ConstructionText.T("reallife_bld_value_stages", "first", ConstructionText.Money(CommissionRules.StagePay(price, 0)), "second",
                ConstructionText.Money(CommissionRules.StagePay(price, 1)), "third", ConstructionText.Money(CommissionRules.StagePay(price, 2))));
            box.Row("reallife_bld_row_penalty", ConstructionText.T("reallife_bld_value_penalty", "amount", ConstructionText.Money(CommissionRules.Penalty(price, 1)), "most",
                ConstructionText.Money(CommissionRules.Penalty(price, 1000))));
            QuoteMeaning(box, p, count);
            string block = Director.HireBlock(p);
            if (block != null)
            {
                box.Line(Director.BlockWords(p, block), CellTone.Yellow);
            }
            else if (!licensed)
            {
                box.Line(ConstructionText.T("reallife_ui_cons_answer_builder_floors"), CellTone.Yellow);
            }
            if (block == null && licensed)
            {
                KitBox.Button(box.ButtonRow(), KitBox.Positive, "reallife_bld_button_hire_this", () =>
                    Confirm("reallife_bld_confirm_hire", "reallife_bld_confirm_hire_body", RivalName(r), CommissionRules.Deposit(price),
                        () => Hired(Director.HireBuilder(p, r, false)), false));
            }
            KitRow picks = box.ButtonRow();
            int cheapest = CommissionRules.Cheapest(_shownQuotes, count), fastest = CommissionRules.Fastest(_shownQuotes, count);
            KitBox.Button(picks, KitBox.Negative, "reallife_bld_button_cheapest", () => { _quotePick = cheapest; Refresh(); });
            KitBox.Button(picks, KitBox.Negative, "reallife_bld_button_fastest", () => { _quotePick = fastest; Refresh(); });
            KitBox.Button(picks, KitBox.Additional, "reallife_bld_button_site", () => { _page = 0; Refresh(); });
        }

        // The best quote among the builders licensed for the project's floors, by the client's own score; -1 where none is.
        private int Licensed(ProjectFile p, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (!LevelRules.Allows(Director.RivalLevel(i), p.Storeys))
                {
                    continue;
                }
                TenderBid a = _shownQuotes[i];
                if (best < 0 || a.Score < _shownQuotes[best].Score)
                {
                    best = i;
                }
            }
            return best;
        }

        // The quotes as a table: the builder with its speciality and the jobs it has done for the player, its price and days, the quality its work shows and the risk of its quote.
        private void QuotesTable(ProjectFile p, int count)
        {
            long days = 0;
            for (int i = 0; i < count; i++)
            {
                days += _shownQuotes[i].Days;
            }
            long mean = count > 0 ? Math.Max(1L, days / count) : 1L;
            var rows = new UiDataRow[count];
            var keys = new string[count];
            for (int i = 0; i < count; i++)
            {
                TenderBid q = _shownQuotes[i];
                RivalFile rival = Director.State.Rivals[i];
                bool licensed = LevelRules.Allows(Director.RivalLevel(i), p.Storeys);
                int risk = QuoteRules.Risk(Director.BuilderBusy(i), rival.Reputation, (int)(mean * 1000L / Math.Max(1, q.Days)));
                int jobs = Math.Max(rival.Jobs, Director.HiredBy(i));
                string speciality = ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[i]]);
                rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        RivalName(i), ConstructionText.Money(CommissionRules.Price(q)), ConstructionText.T("reallife_ui_cons_days_n", "count", q.Days.ToString()), string.Empty,
                        licensed ? ConstructionText.T(risk == QuoteRules.Low ? "reallife_ui_cons_risk_low" : risk == QuoteRules.Medium ? "reallife_ui_cons_risk_medium" : "reallife_ui_cons_risk_high")
                            : ConstructionText.T("reallife_ui_cons_risk_unlicensed")
                    },
                    Values = new[] { (int)UiIcon.Flag, 0, 0, CommissionRules.Quality(rival.Reputation), 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, !licensed || risk == QuoteRules.High ? UiRole.Danger : risk == QuoteRules.Medium ? UiRole.Warn : UiRole.Ok },
                    Sub = jobs > 0 ? speciality + ", " + ConstructionText.T("reallife_ui_cons_jobs_for_you", "count", jobs.ToString()) : speciality
                };
                keys[i] = "q:" + i;
            }
            _detailKeys = keys;
            _detailTable = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = ConstructionText.T("reallife_bld_col_builder"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = ConstructionText.T("reallife_bld_row_quote"), Kind = UiCellKind.Money, Width = 144f },
                    new UiDataColumn { Title = ConstructionText.T("reallife_ui_cons_col_time"), Kind = UiCellKind.Text, Width = 120f },
                    new UiDataColumn { Title = ConstructionText.T("reallife_ui_cons_col_quality"), Kind = UiCellKind.Meter, Width = 168f },
                    new UiDataColumn { Title = ConstructionText.T("reallife_ui_cons_col_risk"), Kind = UiCellKind.Chip, Width = 200f }
                },
                Rows = rows, RowCount = rows.Length, Selected = _quotePick
            };
        }

        // What the picked quote means against the cheapest and the fastest: the money and the days between them, and what a building waiting those days loses in rent.
        private void QuoteMeaning(KitBox box, ProjectFile p, int count)
        {
            int r = _quotePick, cheapest = CommissionRules.Cheapest(_shownQuotes, count), fastest = CommissionRules.Fastest(_shownQuotes, count);
            long month = RentMonth(p);
            bool said = false;
            for (int k = 0; k < 2; k++)
            {
                int other = k == 0 ? cheapest : fastest;
                if (other == r || other < 0 || (k == 1 && other == cheapest))
                {
                    continue;
                }
                long gap = CommissionRules.Price(_shownQuotes[r]) - CommissionRules.Price(_shownQuotes[other]);
                int sooner = _shownQuotes[other].Days - _shownQuotes[r].Days;
                string key = k == 0 ? (sooner > 0 ? "reallife_ui_cons_means_cheap_sooner" : "reallife_ui_cons_means_cheap_later")
                    : (gap > 0L ? "reallife_ui_cons_means_fast_dearer" : "reallife_ui_cons_means_fast_cheaper");
                if (!said)
                {
                    box.Section("reallife_ui_cons_sec_means");
                    said = true;
                }
                box.Note(ConstructionText.T(key, "builder", RivalName(r), "other", RivalName(other), "amount", ConstructionText.Money(Math.Abs(gap))) + (sooner != 0
                    ? " " + ConstructionText.T("reallife_ui_cons_means_days", "count", Math.Abs(sooner).ToString()) : string.Empty));
                long lost = QuoteRules.RentLost(month * 7L / 30L, Math.Abs(sooner));
                if (lost > 0L && sooner != 0)
                {
                    box.Note(ConstructionText.T("reallife_ui_cons_means_rent", "count", Math.Abs(sooner).ToString(), "amount", ConstructionText.Money(lost)));
                }
            }
        }

        // The rent a project's building would let for in a month, from the rents a square metre in its district for its use; 0 where the rents are not read yet.
        private static long RentMonth(ProjectFile p)
        {
            int use = BuildCatalogue.Clamp((int)p.Use, 0, RentMapData.Uses - 1);
            int cents = ConstructionDirector.RentPerSqm(p.District, use);
            return cents > 0 ? (long)cents * Math.Max(0, p.Area) / 100L : 0L;
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
        private void WriteHired(KitBox box, ProjectFile p)
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
                    box.Note(ConstructionText.T("reallife_bld_inspection_booked", "what", ConstructionText.T(BuildCatalogue.InspectionKeys[i]),
                        "day", ConstructionText.Day(p.InspDay[i])));
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
            KitRow buttons = box.ButtonRow();
            KitBox.Button(buttons, KitBox.Negative, "reallife_bld_button_money", () => { _page = 1; Refresh(); });
            KitBox.Button(buttons, KitBox.Additional, "reallife_bld_back", () => Back());
            AbandonRow(box, p);
            Guide(box);
        }

        // A finished building a builder put up: who, the contract and what it paid back for being late.
        private void HiredRows(KitBox box, ProjectFile p)
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
