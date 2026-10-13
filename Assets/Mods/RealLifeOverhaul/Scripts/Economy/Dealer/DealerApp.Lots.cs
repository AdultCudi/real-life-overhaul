using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Dealerships tab: each dealership with its lot, its salespeople and today's figures. One that is picked shows its
    /// week, and its settings: the share of the asking price the staff close a deal at on their own, whether it offers its
    /// own finance and warranties, and, while the player stands in it, laying its lot out again after the room has changed.
    /// </summary>
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
                row.Set(1, yard.Laid ? DealerText.T("reallife_dealer_lot_count", "cars", yard.Cars.Count.ToString(), "places", yard.Spots.Count.ToString()) : DealerText.T("reallife_dealer_lot_unlaid"),
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
            box.Line(DealerText.T("reallife_dealer_lots_about"));
            box.Line(DealerText.T("reallife_dealer_lots_staff"));
            return DealerColumns.Lots;
        }

        private readonly DealerBook _weekSum = new DealerBook();

        private DealerBook Week(DealerYard y)
        {
            _weekSum.Clear(-1);
            for (int i = 0; i < y.Week.Length; i++)
            {
                DealerBook b = y.Week[i];
                if (b.Day < 0)
                {
                    continue;
                }
                _weekSum.Visitors += b.Visitors;
                _weekSum.Served += b.Served;
                _weekSum.Lost += b.Lost;
                _weekSum.Offers += b.Offers;
                _weekSum.Sold += b.Sold;
                _weekSum.Takings += b.Takings;
                _weekSum.Bought += b.Bought;
                _weekSum.Claims += b.Claims;
                _weekSum.Margin += b.Margin;
            }
            return _weekSum;
        }

        private void WriteYard(InfoBox box, DealerYard y, BuildingRegistration reg)
        {
            DealerBook w = Week(y);
            box.Title(y.Name);
            box.Line(y.Laid ? DealerText.T("reallife_dealer_yard_lot", "cars", y.Cars.Count.ToString(), "places", y.Spots.Count.ToString()) : DealerText.T("reallife_dealer_yard_unlaid"));
            box.Line(DealerText.Fill("reallife_dealer_yard_today", "visitors", y.Today.Visitors.ToString(), "served", y.Today.Served.ToString(), "lost", y.Today.Lost.ToString(),
                "offers", y.Today.Offers.ToString(), "sold", y.Today.Sold.ToString()));
            box.Line(DealerText.Fill("reallife_dealer_yard_week", "sold", (w.Sold + y.Today.Sold).ToString(), "takings", DealerText.Money(w.Takings + y.Today.Takings),
                "bought", DealerText.Money(w.Bought + y.Today.Bought), "margin", DealerText.Money(w.Margin + y.Today.Margin), "claims", DealerText.Money(w.Claims + y.Today.Claims)));
            if (reg != null && DealerGame.Salespeople(reg) == 0)
            {
                box.Line(DealerText.T("reallife_dealer_yard_no_staff"));
            }
            int steps = (DealerCatalogue.StaffFloorMost - DealerCatalogue.StaffFloorLeast) / DealerCatalogue.StaffFloorStep + 1;
            int at = (y.StaffFloor - DealerCatalogue.StaffFloorLeast) / DealerCatalogue.StaffFloorStep;
            Choose(box, steps, at, i => DealerText.T("reallife_dealer_yard_floor", "share", DealerText.Share(DealerCatalogue.StaffFloorLeast + i * DealerCatalogue.StaffFloorStep)),
                i => _director.Floor(y.Place, DealerCatalogue.StaffFloorLeast + i * DealerCatalogue.StaffFloorStep));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, y.Finance ? "reallife_dealer_button_finance_off" : "reallife_dealer_button_finance_on", () => _director.OfferFinance(y.Place, !y.Finance));
            InfoBox.Button(buttons, InfoBox.Negative, y.Warranty ? "reallife_dealer_button_warranty_off" : "reallife_dealer_button_warranty_on", () => _director.OfferWarranty(y.Place, !y.Warranty));
            Transform more = box.ButtonRow();
            if (reg != null && DealerGame.Inside(reg))
            {
                InfoBox.Button(more, InfoBox.Positive, "reallife_dealer_button_relay", () => Confirm("reallife_dealer_q_relay_title", DealerText.T("reallife_dealer_q_relay"), () => _director.Relay(y.Place), false));
            }
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
        }
    }
}
