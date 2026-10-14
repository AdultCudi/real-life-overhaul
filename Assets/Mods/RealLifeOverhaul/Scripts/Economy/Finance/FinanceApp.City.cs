using System.Collections.Generic;
using Entities;
using Helpers;
using Streets;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    // The city's economy in the Finance app (Economy/City): three lines on the Overview, and a page of its own that
    // the Overview opens with a button, in the same frame. The page's lists (districts, wholesale prices, the
    // player's businesses, the buildings they own) go in the table, which scrolls; the white box, which does not,
    // has the few figures that are the city's as a whole. Nothing is worked out here but the words: the figures are
    // the model's, and each business's are what was read this morning.
    public sealed partial class FinanceApp
    {
        private const int IncomeLow = 850, IncomeHigh = 1150;   // tenths of a point: what the page calls a low or a high income

        private bool _city;        // the Overview shows the city's page
        private bool _wantCity;    // it is to show the next time the app opens

        private static CityState CityState()
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            return director != null ? director.State : null;
        }

        // The Overview's three lines. False (and nothing written) while the city's economy is not running.
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

        private void WriteCity(InfoBox box)
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            CityState state = director.State;
            int level = director.Level;
            GameInstance save = SaveGameManager.Current;

            box.Title(FinanceText.T("reallife_macro_title"));
            box.Row("reallife_macro_over_rate", CityText.Rate(state.Base));
            int toTurn = CityModel.DaysToTurn(state, director.MonthDays);
            box.Line(state.Pending && toTurn == 1
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
            box.Line(FinanceText.T("reallife_macro_page_note"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_city");

            // One table of four kinds of row, which the filter over it tells apart: each row's third and fourth cells
            // carry the figure they are ordered by (an income level, a price against the usual, a share, a value).
            _spec = FinanceTables.City;

            // ---- districts: income level, and how it moved
            string kind = FinanceText.T(FinanceTables.CityKinds[FinanceTables.District]);
            List<CityDistrict> districts = state.Districts;
            for (int i = 0; i < districts.Count; i++)
            {
                CityDistrict d = districts[i];
                int moved = d.Income - d.Before;
                TableRow row = CityRow("district:" + i, FinanceTables.District, CityGame.Name(d.Name), kind);
                row.Set(2, CityText.Income(d.Income), d.Income);
                row.Set(3, moved == 0 ? FinanceText.T("reallife_macro_trend_flat") : FinanceText.T(moved > 0 ? "reallife_macro_trend_up" : "reallife_macro_trend_down", "points", CityText.Income(Mathf.Abs(moved))), moved);
                row.Set(4, FinanceText.T(d.Income < IncomeLow ? "reallife_macro_income_low" : d.Income > IncomeHigh ? "reallife_macro_income_high" : "reallife_macro_income_middle"));
            }

            // ---- wholesale prices against the usual, and a shortage or a glut
            kind = FinanceText.T(FinanceTables.CityKinds[FinanceTables.Supply]);
            for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
            {
                SupplyLine line = state.Supply[k];
                int wholesale = CityModel.Wholesale(state, k);
                TableRow row = CityRow("supply:" + k, FinanceTables.Supply, CityText.KindTitle(k), kind);
                row.Set(2, CityText.Change(wholesale), wholesale);
                bool shortage = line.Shock == SupplyShock.Shortage;
                if (line.Announced(state.Age))
                {
                    int percent = Mathf.Abs(CityModel.ShockPercent(line, level));
                    row.Set(3, FinanceText.T(shortage ? "reallife_macro_supply_shortage_tomorrow" : "reallife_macro_supply_glut_tomorrow", "percent", percent.ToString(), "days", line.Days.ToString()),
                        shortage ? percent : -percent, shortage ? TableRow.Red : TableRow.Yellow);
                }
                else if (line.Running(state.Age))
                {
                    row.Set(3, FinanceText.T(shortage ? "reallife_macro_supply_shortage_now" : "reallife_macro_supply_glut_now", "days", (line.From + line.Days - state.Age).ToString()),
                        shortage ? Mathf.Abs(CityModel.ShockPercent(line, level)) : -Mathf.Abs(CityModel.ShockPercent(line, level)), shortage ? TableRow.Red : TableRow.Yellow);
                }
                row.Set(4, (state.Buys & (1 << k)) != 0 ? FinanceText.T("reallife_macro_supply_stocked") : string.Empty);
            }

            // ---- the player's businesses, as they were read this morning: customers, district, prices
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
                TableRow row = CityRow("shop:" + i, FinanceTables.Shop, string.IsNullOrEmpty(shop.Reg.BusinessName) ? shop.Reg.Address.ToFormattedString() : shop.Reg.BusinessName, kind);
                int achieved = shop.Applied ? shop.Achieved(multiplier) : 0;
                row.Set(2, !shop.HasFigure && shop.Wanted != 0 ? FinanceText.T("reallife_macro_shop_no_figure")
                    : achieved == 0 ? FinanceText.T("reallife_macro_shop_none") : FinanceText.T("reallife_macro_shop_customers", "percent", CityText.Percent(achieved)), achieved);
                row.Set(3, shop.District != null ? CityGame.Name(shop.District.Name) : string.Empty);
                row.Set(4, shop.Against <= 0 ? string.Empty : FinanceText.T(shop.Over ? "reallife_macro_shop_over" : "reallife_macro_shop_suit"), shop.Over ? TableRow.Red : TableRow.Plain);
            }

            // ---- the buildings the player owns: what the game says each is worth, and what that comes to today
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

        // A factor on the game's own figures in words: "as the game has them" inside the band where nothing is adjusted.
        private static string Adjusted(int factor)
        {
            return Mathf.Abs(factor - 1000) < MacroCatalogue.AdjustQuiet ? FinanceText.T("reallife_macro_page_as_game") : CityText.Change(factor);
        }
    }
}
