using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The City tab: the building code the elected government has in force and what is to change when, the council's
    /// zoning matters, the bills owed to the company and by it (chase, sue, pay), and the City Ledger's building of the year.
    /// Written on opening, a pick or a change, like every tab.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private static readonly string[] RuleRows = { "height", "parking", "fee", "vacant", "council" };

        private static readonly string[] RuleKeys =
        {
            "reallife_bld_rule_height", "reallife_bld_rule_parking", "reallife_bld_rule_fee", "reallife_bld_rule_vacant", "reallife_bld_rule_council"
        };

        partial void WriteCity(InfoBox box, BuildState state, ref string selected)
        {
            int code = ConstructionDirector.Code;
            bool ahead = ConstructionDirector.CodeAhead(out int position, out int changeDay, out int election);
            string picked = null;
            for (int i = 0; i < RuleRows.Length; i++)
            {
                TableRow row = Pick("rule:" + RuleRows[i], ref selected);
                row.Kind = ConstructionTables.CityRule;
                row.Set(0, ConstructionText.T(RuleKeys[i]));
                row.Set(1, RuleValue(RuleRows[i], code));
                row.Set(3, ahead && i < 4 ? ConstructionText.T("reallife_bld_rule_changes", "day", ConstructionText.Day(changeDay), "value", RuleValue(RuleRows[i], position)) : "-");
                picked = row.Id == selected ? row.Id : picked;
            }
            for (int i = 0; i < state.Lots.Count; i++)
            {
                LotFile l = state.Lots[i];
                if (l.ZoneAsk == 0)
                {
                    continue;
                }
                TableRow row = Pick("zone:" + l.Id, ref selected);
                row.Kind = ConstructionTables.CityCouncil;
                row.Set(0, ConstructionText.T("reallife_bld_city_zoning", "use", ConstructionText.ZonesWords(l.ZoneAsk), "name", _director.LotWords(l.Id)));
                row.Set(1, ConstructionText.T("reallife_bld_city_before_council"));
                row.Set(3, ConstructionText.Day(l.ZoneDay), l.ZoneDay);
                picked = row.Id == selected ? row.Id : picked;
            }
            for (int c = 0; c < BuildCatalogue.ClassCount; c++)
            {
                if (state.ClassAdded[c] == 0)
                {
                    continue;
                }
                TableRow row = Pick("class:" + c, ref selected);
                row.Kind = ConstructionTables.CityCouncil;
                row.Set(0, ConstructionText.T(BuildCatalogue.ClassKeys[c]));
                row.Set(1, ConstructionText.T("reallife_bld_city_class_allowed", "uses", ConstructionText.ZonesWords(state.ClassAdded[c])));
                picked = row.Id == selected ? row.Id : picked;
            }
            for (int i = state.Bills.Count - 1; i >= 0; i--)
            {
                BillFile b = state.Bills[i];
                TableRow row = Pick("bill:" + b.Id, ref selected);
                row.Kind = ConstructionTables.CityBill;
                row.Set(0, BillName(b));
                row.Set(1, BillState(b), b.Live ? (b.Owed ? CellTone.Yellow : CellTone.Red) : b.Outcome == BillFile.Lost || b.Outcome == BillFile.Sued ? CellTone.Red : CellTone.Green);
                row.Set(2, ConstructionText.Money(b.Amount), b.Owed ? b.Amount : -b.Amount);
                row.Set(3, ConstructionText.Day(b.Due), b.Due);
                picked = row.Id == selected ? row.Id : picked;
            }
            if (state.Grows.Count > 0 || state.CityLand > 0L)
            {
                // The city's growth (plans/growables.md): what goes up and stands, and what the city had for its land.
                TableRow row = Pick("grow", ref selected);
                row.Kind = ConstructionTables.CityCouncil;
                row.Set(0, ConstructionText.T("reallife_grow_city_title"));
                row.Set(1, ConstructionText.T("reallife_grow_city_value", "going", ConstructionText.N(state.GrowingNow()), "standing", ConstructionText.N(GrownStanding(state))));
                row.Set(2, ConstructionText.Money(state.CityLand + state.CityLandTax), state.CityLand + state.CityLandTax);
                picked = row.Id == selected ? row.Id : picked;
            }
            AwardFile a = state.Award;
            if (a.Year >= 0 || a.LastYear >= 0)
            {
                TableRow row = Pick("award", ref selected);
                row.Kind = ConstructionTables.CityAward;
                row.Set(0, ConstructionText.T("reallife_bld_award_title", "year", ConstructionText.N(Mathf.Max(0, a.Year) + 1)));
                row.Set(1, ConstructionText.T(a.Voting ? "reallife_bld_award_voting" : "reallife_bld_award_gathering", "count", ConstructionText.N(a.Entries.Count)));
                int day = AwardRules.AwardDay(Mathf.Max(0, a.Year), ConstructionGame.DaysPerYear);
                row.Set(3, ConstructionText.Day(day), day);
                picked = row.Id == selected ? row.Id : picked;
            }
            if (picked == null)
            {
                box.Title(ConstructionText.T("reallife_bld_tab_city"));
                box.Note(ConstructionText.T("reallife_bld_city_note", "code", ConstructionDirector.CodeWords(code)));
                if (election > 0)
                {
                    box.Row("reallife_bld_row_election", ConstructionText.Day(election));
                }
                Guide(box);
                return;
            }
            if (picked.StartsWith("rule:", System.StringComparison.Ordinal))
            {
                WriteCode(box, code, ahead, position, changeDay, election);
            }
            else if (picked.StartsWith("zone:", System.StringComparison.Ordinal))
            {
                WriteZoning(box, state.Lot(picked.Substring(5)));
            }
            else if (picked.StartsWith("class:", System.StringComparison.Ordinal))
            {
                int c = BuildCatalogue.Clamp(PickedNumber("class:"), 0, BuildCatalogue.ClassCount - 1);
                box.Title(ConstructionText.T(BuildCatalogue.ClassKeys[c]));
                box.Row("reallife_bld_row_zoned", ConstructionText.ZonesWords(_director.ClassZones(c)));
                box.Note(ConstructionText.T("reallife_bld_city_class_note", "uses", ConstructionText.ZonesWords(state.ClassAdded[c])));
                BackButton(box);
            }
            else if (picked.StartsWith("bill:", System.StringComparison.Ordinal))
            {
                WriteBill(box, state.Bill(PickedNumber("bill:")));
            }
            else if (picked == "grow")
            {
                WriteGrowth(box, state);
            }
            else
            {
                WriteAward(box, a);
            }
        }

        private static string RuleValue(string rule, int code)
        {
            switch (rule)
            {
                case "height":
                    return ConstructionText.T("reallife_bld_rule_storeys", "storeys", ConstructionText.N(PlansRules.HeightLimit(null, true, code)));
                case "parking":
                    return ConstructionText.Share(CivicCatalogue.ParkingPermille(code));
                case "fee":
                    return ConstructionText.Share(CivicCatalogue.PlanningFeePermille(code));
                case "vacant":
                    return ConstructionText.T("reallife_bld_value_a_year", "amount", ConstructionText.Share(LandRules.VacantTax(code)));
                default:
                    return ConstructionText.Day(ConstructionDirector.CouncilDay(ConstructionGame.Day));
            }
        }

        private void WriteCode(InfoBox box, int code, bool ahead, int position, int changeDay, int election)
        {
            box.Title(ConstructionText.T("reallife_bld_code_title"));
            box.Subtitle(ConstructionText.T(code > 0 ? "reallife_bld_code_fast" : code < 0 ? "reallife_bld_code_strict" : "reallife_bld_code_as_came"));
            box.Line(ConstructionDirector.CodeWords(code), CellTone.Plain);
            if (ahead)
            {
                box.Section("reallife_bld_sec_code_ahead");
                box.Line(ConstructionText.T("reallife_bld_code_ahead", "day", ConstructionText.Day(changeDay), "code", ConstructionDirector.CodeWords(position)), CellTone.Yellow);
            }
            if (election > 0)
            {
                box.Row("reallife_bld_row_election", ConstructionText.Day(election));
            }
            box.Row("reallife_bld_row_council_sits", ConstructionText.Day(ConstructionDirector.CouncilDay(ConstructionGame.Day)));
            box.Note(ConstructionText.T("reallife_bld_code_note"));
            BackButton(box);
        }

        // The city's growth: its sites going up and their stage, the buildings standing and run down, the money the city had for its land.
        private void WriteGrowth(InfoBox box, BuildState state)
        {
            box.Title(ConstructionText.T("reallife_grow_city_title"));
            box.RowPair("reallife_grow_row_land", ConstructionText.Money(state.CityLand), "reallife_grow_row_land_tax", ConstructionText.Money(state.CityLandTax));
            int down = 0;
            for (int i = 0; i < state.Grows.Count; i++)
            {
                GrowFile g = state.Grows[i];
                down += g.Standing && g.RunDown ? 1 : 0;
                if (g.UnderWay)
                {
                    box.Line(ConstructionText.T("reallife_grow_city_line", "name", _director.GrowName(g), "stage", ConstructionDirector.GrowStageWords(g)), CellTone.Plain);
                }
            }
            box.Row("reallife_grow_row_rundown", ConstructionText.N(down));
            box.Note(ConstructionText.T("reallife_grow_city_note"));
            BackButton(box);
        }

        private static int GrownStanding(BuildState state)
        {
            int n = 0;
            for (int i = 0; i < state.Grows.Count; i++)
            {
                n += state.Grows[i].Standing ? 1 : 0;
            }
            return n;
        }

        private void WriteZoning(InfoBox box, LotFile l)
        {
            if (l == null)
            {
                Back();
                return;
            }
            box.Title(_director.LotWords(l.Id));
            box.Row("reallife_bld_row_zoned", ConstructionText.ZonesWords(_director.ZonesNow(l)));
            box.Line(ConstructionText.T("reallife_bld_land_zone_heard", "use", ConstructionText.ZonesWords(l.ZoneAsk), "day", ConstructionText.Day(l.ZoneDay)), CellTone.Plain);
            box.Note(ConstructionText.T("reallife_bld_city_zoning_note"));
            BackButton(box);
        }

        private string BillName(BillFile b)
        {
            switch (b.Kind)
            {
                case BillKind.Client:
                    return ConstructionText.T("reallife_bld_bill_client", "name", _director.DebtorWords(b));
                case BillKind.Buyers:
                    return _director.DebtorWords(b);
                case BillKind.Wages:
                    return ConstructionText.T("reallife_bld_bill_wages", "name", _director.CreditorWords(b));
                default:
                    return ConstructionText.T("reallife_bld_bill_merchant", "name", _director.CreditorWords(b));
            }
        }

        private static string BillState(BillFile b)
        {
            if (!b.Live)
            {
                switch (b.Outcome)
                {
                    case BillFile.Paid: return ConstructionText.T("reallife_bld_bill_paid");
                    case BillFile.Won: return ConstructionText.T("reallife_bld_bill_won");
                    case BillFile.Lost: return ConstructionText.T("reallife_bld_bill_lost");
                    case BillFile.Sued: return ConstructionText.T("reallife_bld_bill_sued");
                    default: return ConstructionText.T("reallife_bld_bill_settled");
                }
            }
            if (b.Case > 0)
            {
                return ConstructionText.T("reallife_bld_bill_in_court");
            }
            if (!b.Owed)
            {
                return ConstructionText.T(ConstructionGame.Day > b.Due ? "reallife_bld_bill_owing_late" : "reallife_bld_bill_owing");
            }
            return ConstructionText.T(b.Chased > 0 ? "reallife_bld_bill_chased" : "reallife_bld_bill_unpaid", "count", ConstructionText.N(b.Chased));
        }

        private void WriteBill(InfoBox box, BillFile b)
        {
            if (b == null)
            {
                Back();
                return;
            }
            box.Title(BillName(b));
            box.Subtitle(BillState(b));
            box.RowPair("reallife_bld_row_amount", ConstructionText.Money(b.Amount), "reallife_bld_row_due", ConstructionText.Day(b.Due));
            Transform buttons = box.ButtonRow();
            int id = b.Id;
            BuildState state = _director.State;
            if (b.Live && b.Owed && b.Case == 0)
            {
                box.Row("reallife_bld_row_letters", ConstructionText.T("reallife_bld_value_letters", "count", ConstructionText.N(b.Chased), "most", ConstructionText.N(BillRules.ChaseMost)));
                box.Note(ConstructionText.T("reallife_bld_bill_owed_note", "days", ConstructionText.N(BillRules.SueAfterDays)));
                if (BillRules.MayChase(b.Chased, b.ChasedDay, b.Due, ConstructionGame.Day))
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_chase", () => Answer(_director.Chase(state.Bill(id))));
                }
                if (BillRules.MaySue(b.Chased, b.Due, ConstructionGame.Day))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_sue", () => Answer(_director.SueFor(state.Bill(id))));
                }
            }
            else if (b.Live && b.Case > 0)
            {
                box.Note(ConstructionText.T("reallife_bld_bill_court_note"));
            }
            else if (b.Live && !b.Owed)
            {
                box.Note(ConstructionText.T(b.Kind == BillKind.Wages ? "reallife_bld_bill_wages_note" : "reallife_bld_bill_merchant_note", "days",
                    ConstructionText.N(b.Kind == BillKind.Wages ? BillRules.WagesSueDays : BillRules.MerchantSueDays)));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_settle", () => Answer(_director.Settle(state.Bill(id))));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteAward(InfoBox box, AwardFile a)
        {
            box.Title(ConstructionText.T("reallife_bld_award_title", "year", ConstructionText.N(Mathf.Max(0, a.Year) + 1)));
            box.Subtitle(ConstructionText.T(a.Voting ? "reallife_bld_award_voting" : "reallife_bld_award_gathering", "count", ConstructionText.N(a.Entries.Count)));
            int dpy = ConstructionGame.DaysPerYear;
            box.RowPair("reallife_bld_row_shortlist_day", ConstructionText.Day(AwardRules.ShortlistDay(Mathf.Max(0, a.Year), dpy)), "reallife_bld_row_award_day",
                ConstructionText.Day(AwardRules.AwardDay(Mathf.Max(0, a.Year), dpy)));
            for (int i = 0; i < a.Entries.Count; i++)
            {
                AwardEntry e = a.Entries[i];
                if (!a.Voting || e.Short)
                {
                    string words = ConstructionText.Fill(a.Voting ? "reallife_bld_award_entry_votes" : "reallife_bld_award_entry", "name", _director.AwardName(e),
                        "builder", _director.BuilderName(e.Builder), "stars", ConstructionText.N(AwardRules.Stars(e.Score)), "votes", ConstructionText.N(e.Votes));
                    box.Line(words, e.Builder == -1 ? CellTone.Green : CellTone.Plain);
                }
            }
            if (a.LastYear >= 0)
            {
                box.Section("reallife_bld_sec_award_last");
                box.Line(ConstructionText.Fill("reallife_bld_award_last", "year", ConstructionText.N(a.LastYear + 1), "name", _director.AwardName(a.Last), "builder",
                    _director.BuilderName(a.Last.Builder), "votes", ConstructionText.N(a.Last.Votes)), a.Last.Builder == -1 ? CellTone.Green : CellTone.Plain);
            }
            box.Note(ConstructionText.T("reallife_bld_award_note"));
            BackButton(box);
        }
    }
}
