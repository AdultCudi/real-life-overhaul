using RealLifeOverhaul.GameUi;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The app's Agency tab: the instructions, and a page for the office or the instruction picked.</summary>
    public sealed partial class EstateApp
    {
        private int _office;            // the office showing for the agency

        private void WriteAgency(InfoBox box, EstateState state, ref string selected)
        {
            EstateClimate c = _director.Climate;
            AgencyFile agency = state.Agency;
            Instruction picked = null;
            for (int i = 0; i < agency.Open.Count; i++)
            {
                Instruction n = agency.Open[i];
                TableRow row;
                int pays = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
                if (Pick("ins:" + n.Id, ref selected, out row))
                {
                    picked = n;
                }
                row.Kind = n.Sale ? EstateTables.IsSale : EstateTables.IsLetting;
                row.Kind2 = n.Stage;
                row.Set(0, EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)));
                row.Set(1, EstateText.T(n.Sale ? "reallife_estate_filter_sales" : "reallife_estate_filter_lettings"));
                row.Set(2, EstateText.Money(n.Value), n.Value);
                row.Set(3, EstateText.Money(pays), pays);
                row.Set(4, ((n.Hours - n.Done) / 10f).ToString("0.#"), (n.Hours - n.Done) / 10.0);
                row.Set(5, EstateText.Stage(n.Stage), n.Stage == (int)InstructionStage.Offered ? TableRow.Yellow : TableRow.Plain);
                row.Set(6, EstateText.T("reallife_estate_row_until", "day", n.Day.ToString()), n.Day);
            }
            ManagedBlock pickedBlock = WriteBlockRows(agency, c, ref selected);
            if (agency.Open.Count == 0 && agency.Book.Blocks.Count == 0)
            {
                Say("none", EstateText.T(agency.Running ? "reallife_estate_row_no_instructions" : "reallife_estate_row_no_agency"));
            }
            if (picked != null)
            {
                WriteInstruction(box, agency, picked, c);
                return;
            }
            if (pickedBlock != null)
            {
                WriteBlock(box, agency, pickedBlock, c);
                return;
            }
            box.Title(EstateText.T("reallife_estate_tab_agency"));
            EstateGame.Offices(_places);
            if (agency.Running)
            {
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_estate_agency_office", EstateGame.NameOf(agency.Office));
                box.Line(EstateText.T(agency.Negotiators > 0 ? "reallife_estate_agency_staff" : "reallife_estate_agency_no_staff", "count", agency.Negotiators.ToString(),
                    "hours", (agency.Capacity / 10f).ToString("0.#")), agency.Negotiators > 0 ? CellTone.Plain : CellTone.Red);
                box.Line(EstateText.T("reallife_estate_agency_totals", "deals", agency.Deals.ToString(), "lost", agency.Lost.ToString(), "amount", EstateText.Money(agency.Commission)));
                WriteBook(box, agency);
                box.Headline("reallife_estate_agency_reputation");
                for (int i = 0; i < agency.Districts.Count; i++)
                {
                    DistrictRep d = agency.Districts[i];
                    if (d.SqmPrice > 0)
                    {
                        box.RowWords(EstateText.District(d.Name),
                            EstateText.Level(d.Rep) + ", " + EstateText.T("reallife_estate_agency_chance", "chance", EstateText.Share(AgencyRules.InstructionChance(d.Rep, c.Cycle))));
                    }
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_estate_btn_close_agency", _director.CloseAgency);
                box.Note(EstateText.T("reallife_estate_agency_what"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            if (_places.Count == 0)
            {
                box.Line(EstateText.T("reallife_estate_agency_no_office"), CellTone.Yellow);
                box.Note(EstateText.T("reallife_estate_agency_what"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            _office = EstateCatalogue.Clamp(_office, 0, _places.Count - 1);
            if (!box.HasSelector || !box.Selector(_places.Count, _office, i => _places[i].Address.ToFormattedString(), i => _office = i))
            {
                box.Line(_places[_office].Address.ToFormattedString());
            }
            box.Note(EstateText.T("reallife_estate_agency_open_what"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_open_agency", () =>
            {
                EstateGame.Offices(_places);
                if (_office >= 0 && _office < _places.Count)
                {
                    _director.OpenAgency(EstateGame.Place(_places[_office].StreetName, _places[_office].StreetNumber));
                }
            });
            box.Note(EstateText.T("reallife_estate_agency_what"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // The blocks of other owners' flats as rows beside the instructions: offers to pitch for, the blocks held, and the few lost. Returns the one picked, or null.
        private ManagedBlock WriteBlockRows(AgencyFile agency, EstateClimate c, ref string selected)
        {
            ManagedBlock picked = null;
            int monthDays = MacroCatalogue.MonthDays(c.DaysPerYear);
            for (int i = 0; i < agency.Book.Blocks.Count; i++)
            {
                ManagedBlock b = agency.Book.Blocks[i];
                TableRow row;
                if (Pick("blk:" + b.Id, ref selected, out row))
                {
                    picked = b;
                }
                int code = AgencyText.StageCode(b);
                int roll = b.Held ? AgencyBookRules.RentRoll(b, monthDays) : b.Units * b.RentDay * monthDays;
                int fee = AgencyBookRules.MonthFee(roll, b.FeeAsk, b.Pitched || b.Held ? b.FeePct : 100);
                row.Kind = EstateTables.IsManaging;
                row.Kind2 = code;
                row.Set(0, AgencyText.Block(b));
                row.Set(1, EstateText.T("reallife_estate_filter_managing"));
                row.Set(2, EstateText.Money(roll), roll);
                row.Set(3, EstateText.Money(fee), fee);
                row.Set(4, (AgencyBookRules.Hours(b.Units) / 10f).ToString("0.#"), AgencyBookRules.Hours(b.Units) / 10.0);
                row.Set(5, EstateText.Stage(code), b.Stage == (int)BlockStage.Offered ? TableRow.Yellow : b.Held ? TableRow.Green : TableRow.Red);
                row.Set(6, b.Stage == (int)BlockStage.Offered ? EstateText.T("reallife_estate_row_until", "day", b.Until.ToString())
                    : b.Held ? EstateText.T("reallife_estate_row_since", "day", b.Until.ToString()) : string.Empty, b.Until);
            }
            return picked;
        }

        // The agency's own book on its summary: what it holds, the hours, the month's fees and the pitches it has won and lost.
        private void WriteBook(InfoBox box, AgencyFile agency)
        {
            AgencyBook book = agency.Book;
            box.Section("reallife_estate_sec_book");
            box.Row("reallife_estate_book_blocks", EstateText.T("reallife_estate_book_blocks_of", "held", book.Count(BlockStage.Held).ToString(), "most", AgencyBookCatalogue.BlocksMost.ToString()));
            box.Row("reallife_estate_book_hours", EstateText.T("reallife_estate_book_hours_of", "taken", (agency.Reserved / 10f).ToString("0.#"), "all", (agency.Capacity / 10f).ToString("0.#")),
                book.Served < 1000 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_estate_book_skill", agency.Skill.ToString());
            box.Row("reallife_estate_book_own", EstateText.T("reallife_estate_book_own_of", "units", book.OwnUnits.ToString()), book.OwnPaused ? CellTone.Red : CellTone.Plain);
            box.Row("reallife_estate_book_fees_coming", EstateText.Money(AgencyBookRules.FeesToCome(book)));
            box.Row("reallife_estate_book_fees_last", EstateText.Money(book.LastMonthFees));
            box.Row("reallife_estate_book_letting", EstateText.Money(book.MonthLettings));
            box.Row("reallife_estate_book_pitches", EstateText.T("reallife_estate_book_pitches_of", "won", book.Won.ToString(), "lost", book.Lost.ToString()));
        }

        // One block: an offer to pitch for, a block held, or one lost.
        private void WriteBlock(InfoBox box, AgencyFile agency, ManagedBlock b, EstateClimate c)
        {
            int id = b.Id, monthDays = MacroCatalogue.MonthDays(c.DaysPerYear);
            DistrictRep d = agency.District(b.District);
            int rep = d != null ? d.Rep : 0;
            box.Title(AgencyText.Block(b));
            int code = AgencyText.StageCode(b);
            box.Chip(EstateText.Stage(code), b.Stage == (int)BlockStage.Offered ? CellTone.Yellow : b.Held ? CellTone.Green : CellTone.Red);
            box.Row("reallife_estate_block_owner", AgencyText.Owner(b.Owner));
            box.Row("reallife_estate_block_flats", EstateText.T("reallife_estate_block_flats_of", "units", b.Units.ToString(), "sqm", b.Sqm.ToString(),
                "rent", EstateText.Money(b.RentDay * monthDays)));
            box.Row("reallife_estate_block_expects", AgencyText.Standard(b.Standard) + ", " + EstateText.Level(b.Standard));
            switch ((BlockStage)b.Stage)
            {
                case BlockStage.Offered:
                    WriteOffered(box, agency, b, rep, monthDays);
                    break;
                case BlockStage.Held:
                    WriteHeld(box, b);
                    break;
                default:
                    box.Line(AgencyText.Why(b), CellTone.Red);
                    box.Row("reallife_estate_block_earned", EstateText.Money(b.Earned));
                    break;
            }
            Transform buttons = box.ButtonRow();
            if (b.Stage == (int)BlockStage.Offered)
            {
                if (b.Pitched)
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_withdraw", () => _director.WithdrawPitch(id));
                }
                else
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_pitch", () => _director.PitchBlock(id, false));
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_undercut", () => _director.PitchBlock(id, true));
                }
            }
            else if (b.Held)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_resign", () => _director.ResignBlock(id));
            }
            BackButton(buttons);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteOffered(InfoBox box, AgencyFile agency, ManagedBlock b, int rep, int monthDays)
        {
            int roll = b.Units * b.RentDay * monthDays;
            box.Section("reallife_estate_sec_pitch");
            box.Row("reallife_estate_block_fee_ask", EstateText.Share(b.FeeAsk) + ", " + EstateText.Money(AgencyBookRules.MonthFee(roll, b.FeeAsk, 100)));
            box.Row("reallife_estate_block_fee_under", EstateText.Share(b.FeeAsk * AgencyBookCatalogue.UndercutPct / 100) + ", "
                + EstateText.Money(AgencyBookRules.MonthFee(roll, b.FeeAsk, AgencyBookCatalogue.UndercutPct)));
            box.Row("reallife_estate_block_hours", EstateText.T("reallife_estate_block_hours_of", "hours", (AgencyBookRules.Hours(b.Units) / 10f).ToString("0.#"),
                "spare", (AgencyBookRules.Spare(agency) / 10f).ToString("0.#")));
            box.Row("reallife_estate_block_rivals", EstateText.T("reallife_estate_block_rivals_of", "count", b.Bids.Count.ToString()));
            box.Row("reallife_estate_block_score", EstateText.T("reallife_estate_block_score_of", "skill", agency.Skill.ToString(), "rep", EstateText.Level(rep)));
            box.Line(EstateText.T(b.Pitched ? "reallife_estate_block_pitched" : "reallife_estate_block_open", "day", b.Until.ToString(), "fee", EstateText.Share(b.FeeAsk * b.FeePct / 100)),
                b.Pitched ? CellTone.Green : CellTone.Plain);
            box.Note(EstateText.T("reallife_estate_block_pitch_what"));
        }

        private void WriteHeld(InfoBox box, ManagedBlock b)
        {
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_estate_block_let", EstateText.T("reallife_estate_block_let_of", "let", b.Let.ToString(), "units", b.Units.ToString()));
            if (b.Happy >= 0)
            {
                box.Meter("reallife_estate_duty_happy", LookRules.Fill(b.Happy, 1000), EstateText.Happy(b.Happy), b.Happy >= b.Standard ? CellTone.Green : CellTone.Red);
            }
            box.Row("reallife_estate_block_cond", EstateText.Level(b.Cond), EstateRules.RunDown(b.Cond) ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_estate_block_calls", b.Calls.Count.ToString(), b.Calls.Count > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_estate_block_inspect", EstateText.T("reallife_estate_block_inspect_of", "day", b.NextInspect.ToString(), "strikes", b.Strikes.ToString(),
                "most", AgencyBookCatalogue.StrikesMost.ToString()), b.Strikes > 0 ? CellTone.Red : CellTone.Plain);
            box.Row("reallife_estate_block_served", EstateText.Share(b.Served), b.Served < AgencyBookCatalogue.ServedGood ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_estate_sec_fees");
            box.Row("reallife_estate_block_month", EstateText.Money(b.Collected));
            box.Row("reallife_estate_block_fee_coming", EstateText.Money(AgencyBookRules.MonthFee(b.Collected, b.FeeAsk, b.FeePct)));
            box.Row("reallife_estate_block_fee_rate", EstateText.Share(b.FeeAsk * b.FeePct / 100));
            box.Row("reallife_estate_block_earned", EstateText.Money(b.Earned));
            if (b.Missed > 0)
            {
                box.Line(EstateText.T("reallife_estate_block_missed", "count", b.Missed.ToString(), "most", AgencyBookCatalogue.MissesMost.ToString()), CellTone.Red);
            }
            box.Note(EstateText.T("reallife_estate_block_held_what", "share", EstateText.Share(AgencyBookCatalogue.PenaltyPct * 10), "days", AgencyBookCatalogue.LettingFeeDays.ToString()));
        }

        private void WriteInstruction(InfoBox box, AgencyFile agency, Instruction n, EstateClimate c)
        {
            int id = n.Id;
            DistrictRep d = agency.District(n.District);
            int rep = d != null ? d.Rep : 0;
            box.Title(EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)));
            box.Chip(EstateText.Stage(n.Stage), n.Stage == (int)InstructionStage.Offered ? CellTone.Yellow : n.Stage == (int)InstructionStage.Completing ? CellTone.Green : CellTone.Plain);
            switch ((InstructionStage)n.Stage)
            {
                case InstructionStage.Offered:
                    box.Line(EstateText.T("reallife_estate_ins_offered", "day", n.Day.ToString(), "amount", EstateText.Money(AgencyRules.TakeCost(n)),
                        "days", EstateCatalogue.InstructionLapseDays.ToString()));
                    break;
                case InstructionStage.Marketing:
                    box.Line(EstateText.T("reallife_estate_ins_marketing", "day", n.Day.ToString()));
                    break;
                default:
                    box.Line(EstateText.T("reallife_estate_ins_completing", "amount", EstateText.Money(n.Price), "day", n.Day.ToString()));
                    break;
            }
            box.Note(EstateText.T("reallife_estate_ins_what", "sqm", n.Sqm.ToString(), "amount", EstateText.Money(n.Value), "ask", EstateText.Share(n.Ask)));
            box.Section("reallife_ibox_sec_work");
            box.Meter("reallife_ibox_done", n.Hours > 0 ? n.Done / (float)n.Hours : 1f, ((n.Hours - n.Done) / 10f).ToString("0.#"), CellTone.Plain);
            box.Line(EstateText.T("reallife_estate_ins_hours", "hours", (n.Hours / 10).ToString(), "done", (n.Done / 10f).ToString("0.#"), "capacity", (agency.Capacity / 10f).ToString("0.#")));
            box.Line(EstateText.T("reallife_estate_ins_chance", "chance", EstateText.Share(AgencyRules.DealChance(n.Ask, rep, c.Cycle)),
                "fall", EstateText.Share(EstateCatalogue.FallThrough[EstateCatalogue.Clamp(c.Cycle, 0, 3)]), "reputation", EstateText.Level(rep)));
            int commission = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
            box.Line(EstateText.T(n.Sale ? "reallife_estate_ins_pays_sale" : "reallife_estate_ins_pays_let", "amount", EstateText.Money(commission),
                "share", EstateText.Share(n.Sale ? EstateCatalogue.CommissionSale : EstateCatalogue.CommissionLet)));
            Transform buttons = box.ButtonRow();
            if ((InstructionStage)n.Stage == InstructionStage.Offered)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_take", () => _director.TakeInstruction(id));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_decline", () => _director.DeclineInstruction(id));
            }
            BackButton(buttons);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
