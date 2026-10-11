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
            box.Line(FinanceText.Pair("reallife_macro_over_rate", CityText.Rate(state.Base)));
            box.Line(FinanceText.Pair("reallife_macro_over_prices", CityText.Prices(state)));
            box.Line(FinanceText.Pair("reallife_macro_over_economy", CityText.Economy(state)));
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
            box.Line(FinanceText.Pair("reallife_macro_over_rate", CityText.Rate(state.Base)));
            int toTurn = CityModel.DaysToTurn(state, director.MonthDays);
            box.Line(state.Pending && toTurn == 1
                ? CityText.Decision(state.PendingMove, state.PendingReason, MacroCatalogue.Clamp(state.Base + state.PendingMove, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost))
                : FinanceText.T("reallife_macro_page_decision", "days", toTurn.ToString()));
            box.Line(FinanceText.Pair("reallife_macro_over_prices", CityText.Prices(state)));
            box.Line(FinanceText.Pair("reallife_macro_page_inflation", FinanceText.T("reallife_macro_page_inflation_value", "rate", (state.Inflation / 100f).ToString("0.0"))));
            box.Line(FinanceText.Pair("reallife_macro_over_economy", CityText.Economy(state)));
            box.Headline("reallife_macro_page_money");
            int costs = CityModel.Costs(state), takings = CityModel.Takings(state, level);
            box.Line(FinanceText.Pair("reallife_macro_page_costs", Adjusted(costs)));
            box.Line(FinanceText.Pair("reallife_macro_page_takings", Adjusted(takings)));
            int carried = state.CarryRent + state.CarryWages + state.CarryTakings;
            if (carried > 0)
            {
                box.Line(FinanceText.Pair("reallife_macro_page_carried", FinanceText.Money(carried)));
            }
            box.Line(FinanceText.T("reallife_macro_page_note"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_macro_back", HideCity);

            // ---- districts
            Say("city:districts", FinanceText.T("reallife_macro_rows_districts"));
            List<CityDistrict> districts = state.Districts;
            for (int i = 0; i < districts.Count; i++)
            {
                CityDistrict d = districts[i];
                int moved = d.Income - d.Before;
                FinanceRow row = Row("district:" + i);
                row.A = CityGame.Name(d.Name);
                row.B = CityText.Income(d.Income);
                row.C = moved == 0 ? FinanceText.T("reallife_macro_trend_flat") : FinanceText.T(moved > 0 ? "reallife_macro_trend_up" : "reallife_macro_trend_down", "points", CityText.Income(Mathf.Abs(moved)));
                row.D = FinanceText.T(d.Income < IncomeLow ? "reallife_macro_income_low" : d.Income > IncomeHigh ? "reallife_macro_income_high" : "reallife_macro_income_middle");
            }

            // ---- wholesale prices
            Say("city:supply", FinanceText.T("reallife_macro_rows_supply"));
            for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
            {
                SupplyLine line = state.Supply[k];
                FinanceRow row = Row("supply:" + k);
                row.A = CityText.KindTitle(k);
                row.B = CityText.Change(CityModel.Wholesale(state, k));
                if (line.Announced(state.Age))
                {
                    row.C = FinanceText.T(line.Shock == SupplyShock.Shortage ? "reallife_macro_supply_shortage_tomorrow" : "reallife_macro_supply_glut_tomorrow",
                        "percent", Mathf.Abs(CityModel.ShockPercent(line, level)).ToString(), "days", line.Days.ToString());
                    row.Tone = line.Shock == SupplyShock.Shortage ? 2 : 1;
                }
                else if (line.Running(state.Age))
                {
                    row.C = FinanceText.T(line.Shock == SupplyShock.Shortage ? "reallife_macro_supply_shortage_now" : "reallife_macro_supply_glut_now",
                        "days", (line.From + line.Days - state.Age).ToString());
                    row.Tone = line.Shock == SupplyShock.Shortage ? 2 : 1;
                }
                row.D = (state.Buys & (1 << k)) != 0 ? FinanceText.T("reallife_macro_supply_stocked") : string.Empty;
            }

            // ---- the player's businesses, as they were read this morning
            IReadOnlyList<CityShop> shops = director.Shops;
            if (shops.Count > 0)
            {
                Say("city:shops", FinanceText.T("reallife_macro_rows_shops"));
            }
            float multiplier = save.gameVariables != null ? save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            for (int i = 0; i < shops.Count; i++)
            {
                CityShop shop = shops[i];
                if (shop.Reg == null)
                {
                    continue;
                }
                FinanceRow row = Row("shop:" + i);
                row.A = string.IsNullOrEmpty(shop.Reg.BusinessName) ? shop.Reg.Address.ToFormattedString() : shop.Reg.BusinessName;
                row.B = shop.District != null ? CityGame.Name(shop.District.Name) : string.Empty;
                int achieved = shop.Applied ? shop.Achieved(multiplier) : 0;
                row.C = !shop.HasFigure && shop.Wanted != 0 ? FinanceText.T("reallife_macro_shop_no_figure")
                    : achieved == 0 ? FinanceText.T("reallife_macro_shop_none") : FinanceText.T("reallife_macro_shop_customers", "percent", CityText.Percent(achieved));
                row.D = shop.Against <= 0 ? string.Empty : FinanceText.T(shop.Over ? "reallife_macro_shop_over" : "reallife_macro_shop_suit");
                row.Tone = shop.Over ? 2 : 0;
            }

            // ---- the buildings the player owns: what the game says each is worth, and what that comes to today
            List<RealEstate> owned = save.realEstate;
            bool head = false;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                RealEstate estate = owned[i];
                float value = CityGame.MarketValue(estate);
                if (estate == null || estate.address == null || value <= 0f)
                {
                    continue;
                }
                if (!head)
                {
                    Say("city:property", FinanceText.T("reallife_macro_rows_property"));
                    head = true;
                }
                int factor = CityModel.Property(state, state.District(CityGame.DistrictOf(estate)), level);
                FinanceRow row = Row("estate:" + i);
                row.A = estate.address.ToFormattedString();
                row.B = FinanceText.Money(value);
                row.C = FinanceText.T("reallife_macro_property_today", "amount", FinanceText.Money(value * (factor / 1000f)));
                row.D = CityText.Change(factor);
                row.Tone = factor < 1000 ? 2 : 0;
            }
        }

        // A factor on the game's own figures in words: "as the game has them" inside the band where nothing is adjusted.
        private static string Adjusted(int factor)
        {
            return Mathf.Abs(factor - 1000) < MacroCatalogue.AdjustQuiet ? FinanceText.T("reallife_macro_page_as_game") : CityText.Change(factor);
        }
    }
}
