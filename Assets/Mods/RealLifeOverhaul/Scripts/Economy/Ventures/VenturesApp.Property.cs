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

        private string[] WriteProperty(InfoBox box, VenturesFile file, int day)
        {
            GameInstance save = SaveGameManager.Current;
            List<RealEstate> owned = save.realEstate;
            int count = owned != null ? owned.Count : 0;
            int year = Mathf.Max(1, save.gameVariables.daysPerYear);
            double paid = 0;
            float week = 0f, month = 0f;
            RealEstate picked = null;
            int pick = Picked(_picked, EstateRow);
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
                FinanceRow row = Row(EstateRow, i);
                row.A = e.address.ToFormattedString();
                row.B = Money((float)e.purchasePrice);
                row.C = Money(rent7);
                row.D = Yield(DailyRent(e), e.purchasePrice, year);
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
                return PropertyColumns;
            }
            _picked = null;
            box.Title(FinanceText.T("reallife_ven_tab_property"));
            box.Line(FinanceText.T("reallife_ven_prop_what"));
            box.Line(Pair("reallife_ven_prop_count", buildings.ToString()));
            box.Line(Pair("reallife_ven_prop_paid", Money((float)paid)));
            box.Line(Pair("reallife_ven_prop_week", Money(week)));
            box.Line(Pair("reallife_ven_prop_month", Money(month)));
            box.Line(FinanceText.T("reallife_ven_prop_yield_note", "days", year.ToString()));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_prop_bizman", OpenBizMan);
            return PropertyColumns;
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

        private static string Yield(float dailyRent, double price, int daysPerYear)
        {
            return price > 0 ? (dailyRent * daysPerYear / price * 100.0).ToString("0.0") + "%" : string.Empty;
        }

        private void WriteEstate(InfoBox box, RealEstate e, int day, int year)
        {
            float rent = DailyRent(e);
            box.Title(e.address.ToFormattedString());
            box.Line(Pair("reallife_ven_prop_bought", FinanceText.T("reallife_ven_prop_bought_value", "day", e.purchaseDay.ToString(), "price", Money((float)e.purchasePrice))));
            float worth = 0f;
            int let = 0;
            try
            {
                worth = e.Building != null ? e.Building.GetMarketValue() : 0f;
                let = e.OccupancyPercentage;
            }
            catch (System.Exception)
            {
                worth = 0f;   // (a figure the game cannot give for this building is left out)
            }
            if (worth > 0f)
            {
                box.Line(Pair("reallife_ven_prop_worth", Money(worth)));
            }
            box.Line(Pair("reallife_ven_prop_let", FinanceText.T("reallife_ven_prop_let_value", "share", let.ToString(), "rent", Money(rent))));
            box.Line(Pair("reallife_ven_prop_week", Money(VenturesGame.Rent(e.address, Week))));
            box.Line(Pair("reallife_ven_prop_month", Money(VenturesGame.Rent(e.address, Month))));
            box.Line(Pair("reallife_ven_prop_yield", Yield(rent, e.purchasePrice, year)));
            CreditFile credit = CreditLedger.Ready ? CreditLedger.File : null;
            string key = Books.Key(e.address);
            for (int i = 0; credit != null && i < credit.Accounts.Count; i++)
            {
                CreditAccount a = credit.Accounts[i];
                if (!a.Closed && a.Kind == ProductKind.Mortgage && a.Security == key)
                {
                    box.Line(Pair("reallife_ven_prop_mortgage", Money(a.Owed)));
                }
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_prop_bizman", OpenBizMan);
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
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
