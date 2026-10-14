using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// What the Stock tab shows of the workshop and the deliveries: the cars won at the trade auction on their way, as
    /// rows under the lot's (each with the showroom it goes to and the day it comes), and in a picked car's box what the
    /// workshop would do to it and charge, or when it is back. Written with the tab, never otherwise.
    /// </summary>
    public sealed partial class DealerApp
    {
        private int _now;   // the hour the Stock tab is written at, for the picked car's box

        // The cars on their way, under the lot's: not to be picked (nothing can be done to them before they come).
        private void WriteWays(DealerState state, int level)
        {
            for (int i = 0; i < state.Deliveries.Count; i++)
            {
                DealerDelivery q = state.Deliveries[i];
                DealerYard yard = state.Yard(q.Place);
                TableRow row = Row(DealerDirector.DeliveryRow(q));
                row.Pick = false;
                row.NoTotal = true;
                row.Kind = yard != null ? state.Yards.IndexOf(yard) : 0;
                row.Set(0, DealerGame.CarName(q.Type) + "  " + DealerGame.ColourName(q.Colour));
                row.Set(1, yard != null ? yard.Name : string.Empty);
                row.Set(2, DealerText.Km(q.Km), q.Km);
                row.Set(3, DealerText.Condition(q.Condition), q.Condition);   // (as the Stock tab shows a car once it has come)
                Money(row, 4, q.Cost);
                row.Set(5, string.Empty);
                Money(row, 6, DealerRules.Worth(DealerGame.ListPrice(q.Type), level, q.Km, q.Condition));
                row.Set(7, string.Empty);
                row.Set(8, DealerText.T(q.Told ? "reallife_dealer_state_way_waiting" : "reallife_dealer_state_way", "when", DealerText.When(q.Due)), q.Told ? TableRow.Red : TableRow.Yellow);
            }
        }

        // The workshop in a picked car's box: when the car is back, or what the work would do, cost and take, with its button.
        private void WriteWorkshop(InfoBox box, Transform buttons, DealerYard yard, StockCar car)
        {
            if (car.ShopUntil > _now)
            {
                box.Line(DealerText.Fill("reallife_dealer_workshop_in", "when", DealerText.When(car.ShopUntil), "condition", DealerText.Condition(DealerRules.Worked(car.Condition, car.Faults))));
                return;
            }
            if (!DealerRules.WorkshopWorth(car))
            {
                return;
            }
            int cost = _director.WorkshopCost(car);
            int hours = DealerRules.WorkshopHours(car.Condition + DealerRules.FaultCondition(car.Faults));
            int to = DealerRules.Worked(car.Condition, car.Faults);
            box.Line(DealerText.Fill("reallife_dealer_workshop_offer", "cost", DealerText.Money(cost), "hours", hours.ToString(), "condition", DealerText.Condition(to),
                "bays", (DealerCatalogue.WorkshopBays - yard.InWorkshop(_now)).ToString()));
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Positive, "reallife_dealer_button_workshop", () => Confirm("reallife_dealer_q_workshop_title",
                DealerText.Fill("reallife_dealer_q_workshop", "car", DealerGame.CarName(car.Type), "cost", DealerText.Money(cost), "hours", hours.ToString()), () => _director.Workshop(car.Id), false));
        }
    }
}
