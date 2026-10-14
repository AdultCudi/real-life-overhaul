using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Stock tab: every car on the lots, filtered by dealership. A car that is picked shows what it is, what it cost,
    /// its worth and its asking price with a selector to set it, and two things to do with it: prepare it (a valet, a check
    /// and the faults known put right, which lifts its condition and so its worth) or sell it to the trade at once.
    /// </summary>
    public sealed partial class DealerApp
    {
        private const string CarRow = "car:";

        private TableSpec WriteStock(InfoBox box, DealerState state, int now)
        {
            TableSpec spec = DealerColumns.Stock;
            TableFilter byYard = spec.Filters[0];
            byYard.Names.Clear();
            int level = Prices.Level, day = now / 24;
            int pickedId = Picked(_picked, CarRow);
            StockCar picked = null;
            DealerYard pickedYard = null;
            int cars = 0, free = 0, worthAll = 0;
            for (int y = 0; y < state.Yards.Count; y++)
            {
                DealerYard yard = state.Yards[y];
                byYard.Names.Add(yard.Name);
                free += yard.FreeSpots();
                for (int c = 0; c < yard.Cars.Count; c++)
                {
                    StockCar car = yard.Cars[c];
                    int worth = DealerDirector.Worth(car, level);
                    TableRow row = Row(CarRow + car.Id);
                    row.Kind = y;
                    row.Set(0, DealerGame.CarName(car.Type) + "  " + DealerGame.ColourName(car.Colour));
                    row.Set(1, yard.Name);
                    row.Set(2, DealerText.Km(car.Km), car.Km);
                    row.Set(3, DealerText.Condition(car.Condition), car.Condition, car.Condition < 700 ? TableRow.Yellow : TableRow.Plain);
                    Money(row, 4, car.Cost);
                    Money(row, 5, car.Asking, car.Asking < car.Cost ? TableRow.Red : TableRow.Plain);
                    Money(row, 6, worth);
                    Count(row, 7, day - car.Bought, day - car.Bought >= 30 ? TableRow.Yellow : TableRow.Plain);
                    row.Set(8, State(yard, car, now), car.OutUntil > now ? TableRow.Yellow : TableRow.Plain);
                    cars++;
                    worthAll += worth;
                    if (car.Id == pickedId)
                    {
                        picked = car;
                        pickedYard = yard;
                    }
                }
            }
            if (state.Yards.Count == 0)
            {
                Say(DealerText.T("reallife_dealer_row_no_dealership"));
            }
            if (picked != null)
            {
                WriteCar(box, pickedYard, picked, level, day);
                return spec;
            }
            _picked = null;
            box.Title(DealerText.T("reallife_dealer_app"));
            box.Line(DealerText.Pair("reallife_dealer_sum_waiting", _director.Waiting().ToString()));
            box.Line(DealerText.Pair("reallife_dealer_sum_cars", DealerText.T("reallife_dealer_sum_cars_value", "cars", cars.ToString(), "free", free.ToString())));
            box.Line(DealerText.Pair("reallife_dealer_sum_worth", DealerText.Money(worthAll)));
            box.Line(DealerText.T("reallife_dealer_about"));
            box.Line(DealerText.T("reallife_dealer_sum_note"));
            Meta.Guide.Point(box, GuideTitle);
            return spec;
        }

        private string State(DealerYard yard, StockCar car, int now)
        {
            if (car.OutUntil > now)
            {
                return DealerText.T("reallife_dealer_state_driving");
            }
            return DealerRules.HasDeal(yard, car.Id) ? DealerText.T("reallife_dealer_state_offer") : DealerText.T("reallife_dealer_state_on_show");
        }

        private void WriteCar(InfoBox box, DealerYard yard, StockCar car, int level, int day)
        {
            int worth = DealerDirector.Worth(car, level);
            box.Title(DealerGame.CarName(car.Type) + "  " + DealerGame.ColourName(car.Colour));
            box.Line(DealerText.Fill("reallife_dealer_car_money", "cost", DealerText.Money(car.Cost), "worth", DealerText.Money(worth), "trade", DealerText.Money(DealerRules.TradePrice(worth))));
            box.Line(DealerText.Fill("reallife_dealer_car_what", "source", DealerText.Source(car.Source), "name", yard.Name, "days", (day - car.Bought).ToString(),
                "km", DealerText.Km(car.Km), "condition", DealerText.Condition(car.Condition)));
            box.Line(DealerText.Pair("reallife_dealer_car_faults", car.Known != 0 ? DealerText.Faults(car.Known) : DealerText.T("reallife_dealer_faults_none_known")));
            int step = Choice(DealerRules.AskStepOf(worth, car.Asking));
            Choose(box, DealerRules.AskSteps, step, i => DealerText.T("reallife_dealer_car_ask", "price", DealerText.Money(DealerRules.AskAt(worth, i)),
                "share", DealerText.Share(DealerCatalogue.AskLeast + i * DealerCatalogue.AskStep)), i =>
            {
                SetChoice(i);
                _director.Ask(car.Id, DealerRules.AskAt(worth, i));
            });
            int prepare = _director.PrepareCost(car);
            int trade = DealerRules.TradePrice(worth);
            Transform buttons = box.ButtonRow();
            if (car.Condition < 1000 || car.Known != 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dealer_button_prepare", () => Confirm("reallife_dealer_q_prepare_title",
                    DealerText.T("reallife_dealer_q_prepare", "car", DealerGame.CarName(car.Type), "cost", DealerText.Money(prepare)), () => _director.Prepare(car.Id), false));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_dealer_button_trade", () => Confirm("reallife_dealer_q_trade_title",
                DealerText.T("reallife_dealer_q_trade", "car", DealerGame.CarName(car.Type), "price", DealerText.Money(trade)), () =>
                {
                    if (_director.SellToTrade(car.Id))
                    {
                        Pick(null);
                    }
                }, true));
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
