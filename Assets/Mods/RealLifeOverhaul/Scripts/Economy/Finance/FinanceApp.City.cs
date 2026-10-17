using System.Collections.Generic;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy (Economy/City): three lines on the Overview, and its own page.</summary>
    public sealed partial class FinanceApp
    {
        private const int IncomeLow = 850, IncomeHigh = 1150;   // tenths of a point: a district's income the page calls low or high

        private bool _city;       // the Overview shows the city's page
        private bool _wantCity;   // it is to show the next time the app opens

        /// <summary>The city's page shows the next time the app opens (its notification was clicked).</summary>
        public void WantCity()
        {
            _wantCity = true;
            Want(Finance.Overview);
        }

        private static CityState CityState()
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            return director != null ? director.State : null;
        }

        // The Overview's three lines; false, with nothing written, while the city's economy is not running.
        private static bool CityLines(InfoBox box)
        {
            CityState state = CityState();
            if (state == null)
            {
                return false;
            }
            box.Row("reallife_macro_over_rate", CityText.Rate(state.Base));
            box.Row("reallife_macro_over_prices", CityText.Prices(state));
            box.Row("reallife_macro_over_economy", CityText.Economy(state));
            return true;
        }

        private void ShowCity()
        {
            _city = true;
            Refresh();
        }

        private void HideCity()
        {
            _city = false;
            Refresh();
        }

        // The table's four kinds of row are told apart by the filter, and ordered by the figures in their third and fourth cells.
        private void WriteCity(InfoBox box)
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            CityState state = director.State;
            int level = director.Level;
            GameInstance save = SaveGameManager.Current;

            box.Title(FinanceText.T("reallife_macro_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_macro_over_rate", CityText.Rate(state.Base));
            int toTurn = CityModel.DaysToTurn(state, director.MonthDays);
            box.Note(state.Pending && toTurn == 1
                ? CityText.Decision(state.PendingMove, state.PendingReason, MacroCatalogue.Clamp(state.Base + state.PendingMove, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost))
                : FinanceText.T("reallife_macro_page_decision", "days", toTurn.ToString()));
            box.Row("reallife_macro_over_prices", CityText.Prices(state));
            box.Row("reallife_macro_page_inflation", FinanceText.T("reallife_macro_page_inflation_value", "rate", (state.Inflation / 100f).ToString("0.0")));
            box.Row("reallife_macro_over_economy", CityText.Economy(state));
            box.Headline("reallife_macro_page_money");
            int costs = CityModel.Costs(state), takings = CityModel.Takings(state, level);
            box.Row("reallife_macro_page_costs", Adjusted(costs));
            box.Row("reallife_macro_page_takings", Adjusted(takings));
            int carried = state.CarryRent + state.CarryWages + state.CarryTakings + state.CarryLiving;
            if (carried > 0)
            {
                box.Row("reallife_macro_page_carried", FinanceText.Money(carried));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_macro_back", HideCity);
            box.Note(FinanceText.T("reallife_macro_page_note"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_city");

            _spec = FinanceTables.City;
            string kind = FinanceText.T(FinanceTables.CityKinds[FinanceTables.District]);
            List<CityDistrict> districts = state.Districts;
            for (int i = 0; i < districts.Count; i++)
            {
                CityDistrict d = districts[i];
                int moved = d.Income - d.Before;
                TableRow row = CityRow("district:" + i, FinanceTables.District, CityGame.Name(d.Name), kind);
                row.Set(2, CityText.Income(d.Income), d.Income);
                row.Set(3, moved == 0 ? FinanceText.T("reallife_macro_trend_flat")
                    : FinanceText.T(moved > 0 ? "reallife_macro_trend_up" : "reallife_macro_trend_down", "points", CityText.Income(Mathf.Abs(moved))), moved);
                row.Set(4, FinanceText.T(d.Income < IncomeLow ? "reallife_macro_income_low" : d.Income > IncomeHigh ? "reallife_macro_income_high" : "reallife_macro_income_middle"));
            }

            kind = FinanceText.T(FinanceTables.CityKinds[FinanceTables.Supply]);
            for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
            {
                SupplyLine line = state.Supply[k];
                int wholesale = CityModel.Wholesale(state, k);
                TableRow row = CityRow("supply:" + k, FinanceTables.Supply, CityText.KindTitle(k), kind);
                row.Set(2, CityText.Change(wholesale), wholesale);
                bool shortage = line.Shock == SupplyShock.Shortage;
                CellTone tone = shortage ? TableRow.Red : TableRow.Yellow;
                if (line.Announced(state.Age))
                {
                    int percent = Mathf.Abs(CityModel.ShockPercent(line, level));
                    string words = FinanceText.T(shortage ? "reallife_macro_supply_shortage_tomorrow" : "reallife_macro_supply_glut_tomorrow",
                        "percent", percent.ToString(), "days", line.Days.ToString());
                    row.Set(3, words, shortage ? percent : -percent, tone);
                }
                else if (line.Running(state.Age))
                {
                    int percent = Mathf.Abs(CityModel.ShockPercent(line, level));
                    string words = FinanceText.T(shortage ? "reallife_macro_supply_shortage_now" : "reallife_macro_supply_glut_now", "days", (line.From + line.Days - state.Age).ToString());
                    row.Set(3, words, shortage ? percent : -percent, tone);
                }
                row.Set(4, (state.Buys & (1 << k)) != 0 ? FinanceText.T("reallife_macro_supply_stocked") : string.Empty);
            }

            kind = FinanceText.T(FinanceTables.CityKinds[FinanceTables.Shop]);
            IReadOnlyList<CityShop> shops = director.Shops;
            float multiplier = save.gameVariables != null ? save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            for (int i = 0; i < shops.Count; i++)
            {
                CityShop shop = shops[i];
                if (shop.Reg == null)
                {
                    continue;
                }
                string name = string.IsNullOrEmpty(shop.Reg.BusinessName) ? shop.Reg.Address.ToFormattedString() : shop.Reg.BusinessName;
                TableRow row = CityRow("shop:" + i, FinanceTables.Shop, name, kind);
                int achieved = shop.Applied ? shop.Achieved(multiplier) : 0;
                row.Set(2, !shop.HasFigure && shop.Wanted != 0 ? FinanceText.T("reallife_macro_shop_no_figure")
                    : achieved == 0 ? FinanceText.T("reallife_macro_shop_none") : FinanceText.T("reallife_macro_shop_customers", "percent", CityText.Percent(achieved)), achieved);
                row.Set(3, shop.District != null ? CityGame.Name(shop.District.Name) : string.Empty);
                row.Set(4, shop.Against <= 0 ? string.Empty : FinanceText.T(shop.Over ? "reallife_macro_shop_over" : "reallife_macro_shop_suit"), shop.Over ? TableRow.Red : TableRow.Plain);
            }

            kind = FinanceText.T(FinanceTables.CityKinds[FinanceTables.Estate]);
            List<RealEstate> owned = save.realEstate;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                RealEstate estate = owned[i];
                float value = CityGame.MarketValue(estate);
                if (estate == null || estate.address == null || value <= 0f)
                {
                    continue;
                }
                int factor = CityModel.Property(state, state.District(CityGame.DistrictOf(estate)), level);
                float today = value * (factor / 1000f);
                TableRow row = CityRow("estate:" + i, FinanceTables.Estate, estate.address.ToFormattedString(), kind);
                row.Set(2, FinanceText.Money(value), value);
                row.Set(3, FinanceText.T("reallife_macro_property_today", "amount", FinanceText.Money(today)), today);
                row.Set(4, CityText.Change(factor), factor < 1000 ? TableRow.Red : TableRow.Plain);
            }
        }

        private TableRow CityRow(string id, int kind, string name, string kindName)
        {
            TableRow row = Row(id);
            row.Kind = kind;
            row.Set(0, name);
            row.Set(1, kindName);
            return row;
        }

        // A factor on the game's own figures in words: "as the game has them" within the band where nothing is adjusted.
        private static string Adjusted(int factor)
        {
            return Mathf.Abs(factor - 1000) < MacroCatalogue.AdjustQuiet ? FinanceText.T("reallife_macro_page_as_game") : CityText.Change(factor);
        }
    }
}
