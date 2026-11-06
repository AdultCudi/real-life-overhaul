using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank app's Branches tab: a row a branch with this month's customers, what they did and what the branch costs a
    /// day, and in the box the branches' reach and what it brings the bank, against their rent and wages. Written when the
    /// tab is shown; one pass over the save's buildings.
    /// </summary>
    public sealed partial class OwnBankApp
    {
        private readonly List<BuildingRegistration> _branchRegs = new List<BuildingRegistration>(4);

        private void WriteBranches(InfoBox box, OwnBank b, BankCity c, ref string selected)
        {
            BranchGame.Branches(_branchRegs);
            int today = OwnBankGame.Today, week = 0, picked = -1;
            long fees = 0, costs = 0;
            for (int i = 0; i < b.Branches.Count; i++)
            {
                BankBranch br = b.Branches[i];
                BuildingRegistration reg = Reg(br.Place);
                long cost = BranchGame.CostDay(reg);
                string id = "r|" + br.Place;
                TableRow row = Row(id);
                row.Pick = true;
                if (id == _pick)
                {
                    selected = id;
                    picked = i;
                }
                long[] m = br.Month;
                row.Set(0, br.Name);
                row.Set(1, Whole(m[BranchFigure.Visitors]), m[BranchFigure.Visitors]);
                row.Set(2, Whole(m[BranchFigure.Served]), m[BranchFigure.Served]);
                row.Set(3, Whole(m[BranchFigure.Lost]), m[BranchFigure.Lost], m[BranchFigure.Lost] * 2 > m[BranchFigure.Visitors] && m[BranchFigure.Lost] > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, Whole(m[BranchFigure.Opened]), m[BranchFigure.Opened]);
                row.Set(5, Whole(m[BranchFigure.Loans]), m[BranchFigure.Loans]);
                row.Set(6, OwnBankText.Money(m[BranchFigure.Fees]), m[BranchFigure.Fees] / 100.0);
                row.Set(7, OwnBankText.Money(cost), cost / 100.0);
                week += br.WeekServed(today);
                fees += m[BranchFigure.Fees];
                costs += cost;
            }
            if (picked >= 0)
            {
                WriteBranch(box, b, b.Branches[picked], today);
                return;
            }
            _pick = null;
            box.Title(OwnBankText.T("reallife_branch_tab"));
            box.Subtitle(b.Name);
            if (!Settings.EconomyBranch)
            {
                box.Line(OwnBankText.T("reallife_branch_box_off"), CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_branch_box_count", Whole(b.Branches.Count));
            box.Row("reallife_branch_box_week", Whole(week));
            int reach = Settings.EconomyBranch ? BranchRules.Reach(week) : 0;
            box.Row("reallife_branch_box_reach", OwnBankText.Fill("reallife_branch_box_reach_value", "reach", OwnBankText.Ratio(reach),
                "firms", OwnBankText.Ratio(reach * BranchCatalogue.FirmsReach / 1000)), reach > 0 ? CellTone.Green : CellTone.Plain);
            if (reach > 0)
            {
                int ratio = OwnBankRules.Ratio(b), trust = OwnBankRules.Trust(c, b, today, OwnBankRules.Confidence(c, b, ratio));
                BankCity with = c;
                with.Reach = reach;
                BranchWorth w = BranchRules.Worth(with, b, trust);
                box.Row("reallife_branch_box_brings", OwnBankText.Fill("reallife_branch_box_brings_value", "deposits", OwnBankText.Money(w.Deposits), "loans", OwnBankText.Money(w.Loans)));
                box.Row("reallife_branch_box_margin", OwnBankText.Signed(w.DayCents), w.DayCents >= costs ? CellTone.Green : CellTone.Yellow);
            }
            box.Row("reallife_branch_box_fees", OwnBankText.Money(fees));
            box.Row("reallife_branch_box_costs", OwnBankText.Money(costs));
            box.Note(OwnBankText.T(b.Branches.Count == 0 ? "reallife_branch_box_none" : "reallife_branch_box_pick"));
            box.Note(OwnBankText.Fill("reallife_branch_box_intro", "half", Whole(BranchCatalogue.ReachHalf), "most", OwnBankText.Ratio(BranchCatalogue.ReachMost)));
            // A branch's own statement in the game holds only its rent and wages: its fees are booked at the headquarters.
            box.Note(OwnBankText.T("reallife_branch_box_books"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteBranch(InfoBox box, OwnBank b, BankBranch br, int today)
        {
            BuildingRegistration reg = Reg(br.Place);
            box.Title(br.Name);
            box.Subtitle(b.Name);
            BranchHourIn h = BranchGame.Hour(reg, false);
            box.Chip(OwnBankText.T(h.Open ? "reallife_branch_chip_open" : "reallife_branch_chip_closed"), h.Open ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_branch_box_tellers", OwnBankText.Fill("reallife_branch_box_tellers_value", "now", Whole(h.Tellers), "all", Whole(BranchGame.Staff(reg)),
                "each", Whole(BranchCatalogue.ServePerTeller)), h.Open && h.Tellers == 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_branch_box_week", Whole(br.WeekServed(today)));
            box.Row("reallife_branch_box_costs", OwnBankText.Money(BranchGame.CostDay(reg)));
            Figures(box, "reallife_branch_box_month", br.Month);
            if (br.Last[BranchFigure.Visitors] > 0)
            {
                Figures(box, "reallife_branch_box_last", br.Last);
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T("reallife_branch_box_counter_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private static void Figures(InfoBox box, string section, long[] m)
        {
            box.Section(section);
            box.Row("reallife_branch_box_visitors", OwnBankText.Fill("reallife_branch_box_visitors_value", "visitors", Whole(m[BranchFigure.Visitors]),
                "served", Whole(m[BranchFigure.Served]), "lost", Whole(m[BranchFigure.Lost])),
                m[BranchFigure.Lost] * 2 > m[BranchFigure.Visitors] && m[BranchFigure.Lost] > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_branch_box_money", OwnBankText.Fill("reallife_branch_box_money_value", "in", OwnBankText.Money(m[BranchFigure.PaidIn]),
                "out", OwnBankText.Money(m[BranchFigure.TakenOut]), "opened", Whole(m[BranchFigure.Opened])));
            box.Row("reallife_branch_box_lending", OwnBankText.Fill("reallife_branch_box_lending_value", "loans", Whole(m[BranchFigure.Loans]),
                "lent", OwnBankText.Money(m[BranchFigure.Lent]), "refused", Whole(m[BranchFigure.Refused])));
            box.Row("reallife_branch_box_fees", OwnBankText.Money(m[BranchFigure.Fees]));
        }

        private BuildingRegistration Reg(string place)
        {
            for (int i = 0; i < _branchRegs.Count; i++)
            {
                if (BranchGame.Place(_branchRegs[i]) == place)
                {
                    return _branchRegs[i];
                }
            }
            return null;
        }

        private static string Whole(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
