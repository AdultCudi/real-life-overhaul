using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The Dealerships tab: each showroom's lot, staff and figures, and a picked one's week and settings.</summary>
    public sealed partial class DealerApp
    {
        private const string LotRow = "lot:";

        private TableSpec WriteLots(InfoBox box, DealerState state)
        {
            string pickedPlace = PickedText(_picked, LotRow);
            DealerYard picked = null;
            BuildingRegistration pickedReg = null;
            for (int y = 0; y < state.Yards.Count; y++)
            {
                DealerYard yard = state.Yards[y];
                BuildingRegistration reg = _director.RegOf(yard);
                TableRow row = Row(LotRow + yard.Place);
                row.Set(0, yard.Name);
                row.Set(1, yard.Laid
                        ? DealerText.T("reallife_dealer_lot_count", "cars", yard.Cars.Count.ToString(), "places", yard.Spots.Count.ToString())
                        : DealerText.T("reallife_dealer_lot_unlaid"),
                    yard.Cars.Count, yard.Laid ? TableRow.Plain : TableRow.Yellow);
                int staff = reg != null ? DealerGame.Salespeople(reg) : 0;
                Count(row, 2, staff, staff == 0 ? TableRow.Red : TableRow.Plain);
                Count(row, 3, yard.Today.Visitors);
                Count(row, 4, yard.Today.Sold);
                Money(row, 5, yard.Today.Takings);
                Money(row, 6, Week(yard).Margin + yard.Today.Margin);
                Count(row, 7, yard.Deals.Count, yard.Deals.Count > 0 ? TableRow.Yellow : TableRow.Plain);
                if (yard.Place == pickedPlace)
                {
                    picked = yard;
                    pickedReg = reg;
                }
            }
            if (state.Yards.Count == 0)
            {
                Say(DealerText.T("reallife_dealer_row_no_dealership"));
            }
            if (picked != null)
            {
                WriteYard(box, picked, pickedReg);
                return DealerColumns.Lots;
            }
            _picked = null;
            box.Title(DealerText.T("reallife_dealer_lots_title"));
            box.Note(DealerText.T("reallife_dealer_lots_about"));
            box.Note(DealerText.T("reallife_dealer_lots_staff"));
            Meta.Guide.Point(box, GuideTitle);
            return DealerColumns.Lots;
        }

        private readonly DealerBook _weekSum = new DealerBook(), _weekDay = new DealerBook();

        // The six days before today of a showroom's kept days (today's book is added by whoever shows the week).
        private DealerBook Week(DealerYard y)
        {
            DealerState state = _director.State;
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            if (state == null)
            {
                _weekSum.Clear(-1);
                return _weekSum;
            }
            DealerReport.Sum(state, y.Place, today - DealerCatalogue.WeekBooks + 1, today - 1, today, _weekSum, _weekDay);
            return _weekSum;
        }

        private void WriteYard(InfoBox box, DealerYard y, BuildingRegistration reg)
        {
            DealerBook w = Week(y);
            int day = SaveGameManager.Current.Day;
            box.Title(y.Name);
            int step = DealerRules.Step(y.Rep);
            box.Chip(DealerText.Step(step));
            if (y.EventOn(day))
            {
                box.Chip(DealerText.Event(y.Event, y.Feature), CellTone.Green);
            }
            if (reg != null && DealerGame.Salespeople(reg) == 0)
            {
                box.Line(DealerText.T("reallife_dealer_yard_no_staff"), CellTone.Red);
            }
            box.Section("reallife_ibox_sec_now");
            box.Line(DealerText.Fill("reallife_dealer_yard_rep", "rep", y.Rep.ToString(), "step", DealerText.Step(step), "next", NextStep(step)));
            if (y.EventOn(day))
            {
                box.Line(DealerText.Fill("reallife_dealer_yard_event", "event", DealerText.Event(y.Event, y.Feature), "day", FinanceText.Day(y.EventUntil)));
            }
            if (y.Laid)
            {
                box.Meter("reallife_ibox_lot", LookRules.Fill(y.Cars.Count, y.Spots.Count),
                    DealerText.T("reallife_ibox_of", "value", y.Cars.Count.ToString(), "most", y.Spots.Count.ToString()), CellTone.Plain);
            }
            else
            {
                box.Line(DealerText.T("reallife_dealer_yard_unlaid"), CellTone.Yellow);
            }
            box.Section("reallife_ibox_today");
            box.Line(DealerText.Fill("reallife_dealer_yard_today", "visitors", y.Today.Visitors.ToString(), "served", y.Today.Served.ToString(), "lost", y.Today.Lost.ToString(),
                "offers", y.Today.Offers.ToString(), "sold", y.Today.Sold.ToString()));
            box.Section("reallife_ibox_sec_this_week");
            box.Line(DealerText.Fill("reallife_dealer_yard_week", "sold", (w.Sold + y.Today.Sold).ToString(), "takings", DealerText.Money(w.Takings + y.Today.Takings),
                "bought", DealerText.Money(w.Bought + y.Today.Bought), "margin", DealerText.Money(w.Margin + y.Today.Margin), "claims", DealerText.Money(w.Claims + y.Today.Claims)));
            int steps = (DealerCatalogue.StaffFloorMost - DealerCatalogue.StaffFloorLeast) / DealerCatalogue.StaffFloorStep + 1;
            int at = (y.StaffFloor - DealerCatalogue.StaffFloorLeast) / DealerCatalogue.StaffFloorStep;
            Choose(box, steps, at, i => DealerText.T("reallife_dealer_yard_floor", "share", DealerText.Share(DealerCatalogue.StaffFloorLeast + i * DealerCatalogue.StaffFloorStep)),
                i => _director.Floor(y.Place, DealerCatalogue.StaffFloorLeast + i * DealerCatalogue.StaffFloorStep));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, y.Finance ? "reallife_dealer_button_finance_off" : "reallife_dealer_button_finance_on", () => _director.OfferFinance(y.Place, !y.Finance));
            InfoBox.Button(buttons, InfoBox.Negative, y.Warranty ? "reallife_dealer_button_warranty_off" : "reallife_dealer_button_warranty_on", () => _director.OfferWarranty(y.Place, !y.Warranty));
            InfoBox.Button(buttons, InfoBox.Additional, y.Extras ? "reallife_dealer_button_extras_off" : "reallife_dealer_button_extras_on", () => _director.OfferExtras(y.Place, !y.Extras));
            Transform more = box.ButtonRow();
            if (reg != null && DealerGame.Inside(reg))
            {
                InfoBox.Button(more, InfoBox.Positive, "reallife_dealer_button_relay",
                    () => Confirm("reallife_dealer_q_relay_title", DealerText.T("reallife_dealer_q_relay"), () => _director.Relay(y.Place), false));
            }
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        private static string NextStep(int step)
        {
            return step + 1 < DealerCatalogue.RepSteps.Length
                ? DealerText.T("reallife_dealer_rep_next", "points", DealerCatalogue.RepSteps[step + 1].ToString())
                : DealerText.T("reallife_dealer_rep_top");
        }
    }
}
