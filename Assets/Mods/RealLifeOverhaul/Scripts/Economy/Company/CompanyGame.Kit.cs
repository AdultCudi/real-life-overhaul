using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
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

        /// <summary>This business can be put up for sale now.</summary>
        public static bool CanSell(string place) => ServicesDirector.CanSellPlace(place, out _);

        /// <summary>
        /// Selling is offered for this business: true shows the button. With a reason (not null) it is shown off with the reason's words, as the sale itself would
        /// refuse it (the player stands inside, a vehicle or a gift is kept there, a dealership has cars, a Going Public session); with none it can be pressed.
        /// </summary>
        public static bool SaleOffered(string place, out string why)
        {
            bool can = ServicesDirector.CanSellPlace(place, out string key);
            why = key != null ? ServicesText.T(key) : null;
            return can || key != null;
        }

        public static void OpenCleaning() => ServicesDirector.OpenCleaning();

        public static void OpenBank() => OwnBankDirector.OpenApp();

        public static void OpenSale(string place) => ServicesDirector.OpenSale(place);

        /// <summary>
        /// Whether a registration is a rival's business a page or an outlet may read: owned by a rival of the game's that is still in the game, named, not behind a DLC the
        /// player lacks, not an address the game sets aside, and none of the player's (nor another player's, which looks rented by the player on this machine).
        /// </summary>
        internal static bool RivalShopUsable(BuildingRegistration r)
        {
            try
            {
                return r != null && !string.IsNullOrEmpty(r.businessOwnerRivalId) && !r.RentedByPlayer && !string.IsNullOrEmpty(r.BusinessName) && !RivalsHelper.IsRivalDefeated(r.businessOwnerRivalId)
                    && !GameDlc.Gated(r) && (r.Address == null || !RivalsHelper.IgnoredAddresses.Contains(r.Address)) && !GoingPublic.Mine(r);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The nearest business of the same kind in a shop's district that a rival owns, and the mean of the game's last seven daily takings for it: the game's own estimate
        /// (BuildingRegistration.dailyIncomes, which the game keeps twenty days of for every business a rival owns). False where there is none. Read when a business's page
        /// is read, never per frame.
        /// </summary>
        internal static bool RivalNear(CompanyShop shop, out string name, out int daily)
        {
            name = null;
            daily = 0;
            try
            {
                List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
                BuildingRegistration mine = shop != null ? shop.Reg : null;
                if (all == null || mine == null)
                {
                    return false;
                }
                float best = float.MaxValue;
                BuildingRegistration nearest = null;
                for (int i = 0; i < all.Count; i++)
                {
                    BuildingRegistration r = all[i];
                    if (!RivalShopUsable(r) || r.dailyIncomes == null || r.dailyIncomes.Count == 0
                        || !string.Equals(r.businessTypeName, mine.businessTypeName, StringComparison.Ordinal) || !string.Equals(GameRegistrations.District(r), shop.District,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    // Nearness by the address: the same street first, by how far apart the numbers are, then the rest of the district. A tie goes to the lower street name and number,
                    // so the answer is the save's and not the order the game lists its registrations in.
                    float away = string.Equals(r.StreetName, mine.StreetName, StringComparison.Ordinal) ? Mathf.Abs(r.StreetNumber - mine.StreetNumber) : 100000f;
                    if (away < best || (away == best && nearest != null && Before(r, nearest)))
                    {
                        best = away;
                        nearest = r;
                        name = r.BusinessName;
                        int from = Mathf.Max(0, r.dailyIncomes.Count - 7);
                        float sum = 0f;
                        for (int k = from; k < r.dailyIncomes.Count; k++)
                        {
                            sum += r.dailyIncomes[k];
                        }
                        daily = Mathf.RoundToInt(Mathf.Max(0f, sum / (r.dailyIncomes.Count - from)));
                    }
                }
                return name != null;
            }
            catch (Exception)
            {
                name = null;
                return false;
            }
        }

        private static bool Before(BuildingRegistration a, BuildingRegistration b)
        {
            int by = string.CompareOrdinal(a.StreetName, b.StreetName);
            return by != 0 ? by < 0 : a.StreetNumber < b.StreetNumber;
        }

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
