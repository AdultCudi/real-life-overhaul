using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Company app on the kit reads from the game that the old app did not: the head office's name and address, the player's name, the day's result of the
    /// businesses, where yesterday's costs went by the game's own statements, and the entries of the other apps it opens. Reads only; run when the app opens or its
    /// state changes, never every frame.
    /// </summary>
    internal static partial class CompanyGame
    {
        /// <summary>A month of the game's days.</summary>
        public static int MonthDays => MacroCatalogue.MonthDays(DaysPerYear);

        /// <summary>The player's head office, or null.</summary>
        private static BuildingRegistration Head()
        {
            try
            {
                return OwnBankGame.Headquarters();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The head office's name as the player gave it; null with none.</summary>
        public static string HeadName()
        {
            BuildingRegistration hq = Head();
            return hq != null && !string.IsNullOrEmpty(hq.BusinessName) ? hq.BusinessName : null;
        }

        /// <summary>The head office's street address; null with none.</summary>
        public static string HeadAddress()
        {
            try
            {
                BuildingRegistration hq = Head();
                return hq != null && hq.Address != null ? hq.Address.ToFormattedString() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The player's own name; null before there is one.</summary>
        public static string OwnerName()
        {
            try
            {
                return PlayerHelper.CharacterData != null && !string.IsNullOrEmpty(PlayerHelper.CharacterData.name) ? PlayerHelper.CharacterData.name : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The day's result of the player's money: the game's profit with what the mod adds (GameBooks.Result), whole money. False when the game made no statement for the day.</summary>
        public static bool DayResult(int day, out int result)
        {
            result = 0;
            try
            {
                float parts;
                DayFigure figure;
                if (day < 1 || !GameBooks.Result(day, out figure, out parts))
                {
                    return false;
                }
                result = Mathf.RoundToInt(Mathf.Clamp(figure.Mod, -int.MaxValue / 4f, int.MaxValue / 4f));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Where a day's costs went at the businesses given, by the game's own statements: wages, stock and ingredients, rent, licensing fees and losses to theft together
        /// as rent and bills, and marketing. False when the game has no statement for the day or none for these businesses.
        /// </summary>
        public static bool StatementCosts(int day, IReadOnlyList<CompanyShop> shops, out int wages, out int stock, out int rent, out int marketing)
        {
            wages = stock = rent = marketing = 0;
            try
            {
                List<FinancialSummary> summaries = Save != null ? Save.financialSummaries : null;
                FinancialSummary summary = summaries != null ? summaries.FindLast(s => s != null && s.dayNumber == day) : null;
                if (summary == null || summary.businessIncomeStatements == null)
                {
                    return false;
                }
                bool any = false;
                float w = 0f, st = 0f, r = 0f, m = 0f;
                for (int i = 0; i < summary.businessIncomeStatements.Count; i++)
                {
                    FinancialSummary.BusinessIncomeStatement line = summary.businessIncomeStatements[i];
                    bool ours = false;
                    for (int k = 0; line != null && k < shops.Count && !ours; k++)
                    {
                        ours = shops[k].Address != null && line.Address == shops[k].Address;
                    }
                    if (!ours)
                    {
                        continue;
                    }
                    any = true;
                    w += line.SalaryExpenses;
                    st += line.TotalResources;
                    r += line.RentExpenses + line.LicensingFees + line.Theft;
                    m += line.MarketingExpenses;
                }
                wages = Mathf.RoundToInt(w);
                stock = Mathf.RoundToInt(st);
                rent = Mathf.RoundToInt(r);
                marketing = Mathf.RoundToInt(m);
                return any;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The cleaning company's app can be opened.</summary>
        public static bool CanClean => ServicesDirector.CanClean;

        /// <summary>The player's own bank app can be opened.</summary>
        public static bool CanOpenBank => OwnBankDirector.CanOpen;

        /// <summary>A business can be put up for sale.</summary>
        public static bool CanSell(string place) => ServicesDirector.CanSell;

        public static void OpenCleaning() => ServicesDirector.OpenCleaning();

        public static void OpenBank() => OwnBankDirector.OpenApp();

        public static void OpenSale(string place) => ServicesDirector.OpenSale(place);

        /// <summary>The company's line in the stock market (Markets/Listing); null where it is not listed.</summary>
        public static string ListingLine()
        {
            try
            {
                return CompanyBridges.ListingLine != null && CompanyBridges.OpenListing != null ? CompanyBridges.ListingLine() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void OpenListing()
        {
            try
            {
                CompanyBridges.OpenListing?.Invoke();
            }
            catch (Exception e)
            {
                CompanyApp.Logger?.Error(e);
            }
        }
    }
}
