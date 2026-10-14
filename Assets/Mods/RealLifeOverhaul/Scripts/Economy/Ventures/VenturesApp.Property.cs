using System.Collections.Generic;
using Entities;
using Extensions;
using RealLifeOverhaul.GameUi;
using Streets;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The Property tab. The game already lets every building the player owns: tenants come and go by the rent set in
    /// BizMan, the rent is paid in each night, and a building for sale is sold to a competitor
    /// (Helpers.RealEstateHelper). So nothing is added to that: this tab only brings the game's own figures together
    /// as an investment is read: what a building cost, what it is worth now, how much of it is let, what it has
    /// brought in over seven and thirty days, the yield on the price paid, and the mortgage on it if there is one.
    /// Everything is read from the game when the tab is written; nothing is kept and nothing is changed.
    /// </summary>
    public sealed partial class VenturesApp
    {
        private const string EstateRow = "est:";
        private const int Week = 7, Month = 30;

        private TableSpec WriteProperty(InfoBox box, VenturesFile file, int day)
        {
            GameInstance save = SaveGameManager.Current;
            List<RealEstate> owned = save.realEstate;
            int count = owned != null ? owned.Count : 0;
            int year = GameCalendar.DaysPerYear;
            double paid = 0;
            float week = 0f, month = 0f;
            RealEstate picked = null;
            int pick = Picked(_picked, EstateRow);
            List<string> districts = VenturesTables.Property.Filters[0].Names;   // the filter's kinds: the districts the buildings stand in
            districts.Clear();
            for (int i = 0; i < count; i++)
            {
                RealEstate e = owned[i];
                if (e == null || e.address == null)
                {
                    continue;
                }
                float rent7 = VenturesGame.Rent(e.address, Week);
                paid += e.purchasePrice;
                week += rent7;
                month += VenturesGame.Rent(e.address, Month);
                string district = CityGame.Name(CityGame.DistrictOf(e));
                float worth, daily = DailyRent(e);
                int let;
                Worth(e, out worth, out let);
                TableRow row = Row(EstateRow, i);
                row.Set(0, e.address.ToFormattedString());
                if (!string.IsNullOrEmpty(district))
                {
                    row.Kind = districts.IndexOf(district);
                    if (row.Kind < 0)
                    {
                        row.Kind = districts.Count;
                        districts.Add(district);
                    }
                    row.Set(1, district);
                }
                row.Set(2, let + "%", let);
                row.Set(3, Money((float)e.purchasePrice), e.purchasePrice);
                if (worth > 0f)
                {
                    row.Set(4, Money(worth), worth);
                }
                row.Set(5, Money(rent7), rent7);
                row.Set(6, Yield(daily, e.purchasePrice, year), e.purchasePrice > 0 ? daily * year / e.purchasePrice * 100.0 : double.NaN);
                picked = i == pick ? e : picked;
            }
            int buildings = _rows.Count;
            UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_head_property", "count", buildings.ToString()));
            if (buildings == 0)
            {
                Say(FinanceText.T("reallife_ven_row_no_property"));
            }
            if (picked != null)
            {
                WriteEstate(box, picked, day, year);
                return VenturesTables.Property;
            }
            _picked = null;
            box.Title(FinanceText.T("reallife_ven_tab_property"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ven_prop_week", Money(week));
            box.Row("reallife_ven_prop_month", Money(month));
            box.Row("reallife_ven_prop_count", buildings.ToString());
            box.Row("reallife_ven_prop_paid", Money((float)paid));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_prop_bizman", OpenBizMan);
            box.Note(FinanceText.T("reallife_ven_prop_what"));
            box.Note(FinanceText.T("reallife_ven_prop_yield_note", "days", year.ToString()));
            Meta.Guide.Point(box, GuideTitle);
            return VenturesTables.Property;
        }

        // What a building brings in a day now, as the game works it out (nothing for a house that is lived in, not let).
        private static float DailyRent(RealEstate e)
        {
            try
            {
                return e.DailyIncome;
            }
            catch (System.Exception)
            {
                return 0f;   // (the building is no longer in the city: no rent)
            }
        }

        // What the game says a building is worth now and how much of it is let; nothing for a figure the game cannot give.
        private static void Worth(RealEstate e, out float worth, out int let)
        {
            worth = 0f;
            let = 0;
            try
            {
                worth = e.Building != null ? e.Building.GetMarketValue() : 0f;
                let = e.OccupancyPercentage;
            }
            catch (System.Exception)
            {
                worth = 0f;   // (the building is no longer in the city)
            }
        }

        private static string Yield(float dailyRent, double price, int daysPerYear)
        {
            return price > 0 ? (dailyRent * daysPerYear / price * 100.0).ToString("0.0") + "%" : string.Empty;
        }

        private void WriteEstate(InfoBox box, RealEstate e, int day, int year)
        {
            float rent = DailyRent(e);
            box.Title(e.address.ToFormattedString());
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ven_prop_bought", FinanceText.T("reallife_ven_prop_bought_value", "day", e.purchaseDay.ToString(), "price", Money((float)e.purchasePrice)));
            float worth;
            int let;
            Worth(e, out worth, out let);
            if (worth > 0f)
            {
                box.Row("reallife_ven_prop_worth", Money(worth));
            }
            box.Row("reallife_ven_prop_let", FinanceText.T("reallife_ven_prop_let_value", "share", let.ToString(), "rent", Money(rent)));
            box.Row("reallife_ven_prop_week", Money(VenturesGame.Rent(e.address, Week)));
            box.Row("reallife_ven_prop_month", Money(VenturesGame.Rent(e.address, Month)));
            box.Row("reallife_ven_prop_yield", Yield(rent, e.purchasePrice, year));
            CreditFile credit = CreditLedger.Ready ? CreditLedger.File : null;
            string key = Books.Key(e.address);
            for (int i = 0; credit != null && i < credit.Accounts.Count; i++)
            {
                CreditAccount a = credit.Accounts[i];
                if (!a.Closed && a.Kind == ProductKind.Mortgage && a.Security == key)
                {
                    box.Row("reallife_ven_prop_mortgage", Money(a.Owed));
                }
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_prop_bizman", OpenBizMan);
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        // Rents are set, and buildings bought and sold, where the game has them: BizMan.
        private void OpenBizMan()
        {
            if (_menu != null)
            {
                _menu.ShowApp(AppName.BizMan);
            }
        }
    }
}
