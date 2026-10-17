using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The Stock tab: every car on the lots, and a picked car's price, preparing, sale to the trade and bay.</summary>
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
                free += state.Room(yard);
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
                    row.Set(8, State(yard, car, now), car.OutUntil > now || car.ShopUntil > now ? TableRow.Yellow : TableRow.Plain);
                    cars++;
                    worthAll += worth;
                    if (car.Id == pickedId)
                    {
                        picked = car;
                        pickedYard = yard;
                    }
                }
            }
            WriteWays(state, level);
            if (state.Yards.Count == 0)
            {
                Say(DealerText.T("reallife_dealer_row_no_dealership"));
            }
            if (picked != null)
            {
                WriteCar(box, pickedYard, picked, level, now);
                return spec;
            }
            _picked = null;
            box.Title(DealerText.T("reallife_dealer_app"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_dealer_sum_waiting", _director.Waiting().ToString(), _director.Waiting() > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_dealer_sum_cars", DealerText.T("reallife_dealer_sum_cars_value", "cars", cars.ToString(), "free", free.ToString()));
            box.Row("reallife_dealer_sum_worth", DealerText.Money(worthAll));
            box.Note(DealerText.T("reallife_dealer_about"));
            box.Note(DealerText.T("reallife_dealer_sum_note"));
            Meta.Guide.Point(box, GuideTitle);
            return spec;
        }

        private string State(DealerYard yard, StockCar car, int now)
        {
            if (car.ShopUntil > now)
            {
                return DealerText.T("reallife_dealer_state_workshop", "when", DealerText.When(car.ShopUntil));
            }
            if (car.OutUntil > now)
            {
                return DealerText.T("reallife_dealer_state_driving");
            }
            return DealerRules.HasDeal(yard, car.Id) ? DealerText.T("reallife_dealer_state_offer") : DealerText.T("reallife_dealer_state_on_show");
        }

        private void WriteCar(InfoBox box, DealerYard yard, StockCar car, int level, int now)
        {
            int worth = DealerDirector.Worth(car, level), day = now / 24;
            box.Title(DealerGame.CarName(car.Type));
            box.Subtitle(DealerGame.ColourName(car.Colour));
            box.Chip(State(yard, car, now), car.ShopUntil > now || car.OutUntil > now ? CellTone.Plain : DealerRules.HasDeal(yard, car.Id) ? CellTone.Green : CellTone.Plain);
            ShowroomPhotos.Show(box, car);
            box.Section("reallife_ibox_sec_money");
            box.RowPair("reallife_ibox_cost", DealerText.Money(car.Cost), "reallife_ibox_worth", DealerText.Money(worth));
            box.Row("reallife_ibox_trade_price", DealerText.Money(DealerRules.TradePrice(worth)));
            box.Note(DealerText.Fill("reallife_dealer_car_what", "source", DealerText.Source(car.Source), "name", yard.Name, "days", (day - car.Bought).ToString(),
                "km", DealerText.Km(car.Km), "condition", DealerText.Condition(car.Condition)));
            box.Row("reallife_dealer_car_faults", car.Known != 0 ? DealerText.Faults(car.Known) : DealerText.T("reallife_dealer_faults_none_known"),
                car.Known != 0 ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_asking");
            int step = Choice(DealerRules.AskStepOf(worth, car.Asking));
            Choose(box, DealerRules.AskSteps, step, i => DealerText.T("reallife_dealer_car_ask", "price", DealerText.Money(DealerRules.AskAt(worth, i)),
                "share", DealerText.Share(DealerCatalogue.AskLeast + i * DealerCatalogue.AskStep)), i =>
            {
                SetChoice(i);
                _director.Ask(car.Id, DealerRules.AskAt(worth, i));
            });
            if (yard == _director.YardInside() && car.Spot >= 0 && car.OutUntil <= ShowroomGame.Hour)
            {
                int id = car.Id;
                Transform floor = box.ButtonRow();
                InfoBox.Button(floor, InfoBox.Positive, "reallife_showroom_button_look", () => ShowroomView.Instance?.Look(id));
                InfoBox.Button(floor, InfoBox.Additional, "reallife_showroom_button_bay", () => ShowroomView.Instance?.Place(id));
            }
            else
            {
                box.Note(DealerText.T("reallife_showroom_car_away"));
            }
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
            WriteWorkshop(box, yard, car, now);
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
