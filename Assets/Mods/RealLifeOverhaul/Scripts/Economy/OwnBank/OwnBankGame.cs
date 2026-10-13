using System;
using System.Collections.Generic;
using Helpers;
using Streets;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the player's bank asks of the rest of the mod. Each has a default, so Economy/OwnBank works alone;
    /// Scripts/Wiring/OwnBankLinks.cs fills them in on every city load and <see cref="Reset"/> puts them back. The bank
    /// never calls another feature itself.
    /// </summary>
    public static class OwnBankBridges
    {
        /// <summary>The player's integrity as an owner, 0 to 100 (Standing). Default: 50, a newcomer's.</summary>
        public static Func<int> Integrity;

        /// <summary>Where one of the council's levers stands: -1, 0 or 1 (Civic). Default: as the city came.</summary>
        public static Func<Lever, int> Policy;

        /// <summary>True while the justice system takes a lender's claims (Justice: DebtClaims.Available). Default: no courts.</summary>
        public static Func<bool> Courts;

        /// <summary>Fills the list with the Stocks app's listed companies as the fund sees them; false while the market is not running. Default: no market.</summary>
        public static Func<List<FundQuote>, bool> Quotes;

        /// <summary>Something the bank did moves the player's integrity (Standing): by how much, and whether it was a scandal. Default: nobody hears.</summary>
        public static Action<int, bool> Moved;

        /// <summary>True if the bank has the qualified person the licence asks for (Careers: a finance qualification of the player's, or a senior professional of theirs in finance). Default: yes.</summary>
        public static Func<bool> Qualified;

        public static void Reset()
        {
            Integrity = null;
            Policy = null;
            Courts = null;
            Quotes = null;
            Moved = null;
            Qualified = null;
        }
    }

    /// <summary>
    /// Everything the player's bank reads from or does to the game, in one place.
    ///
    /// READ: the player's headquarters (BuildingRegistration: RentedByPlayer, businessTypeName, and Going Public's
    /// Mine, so that another player's office in a session is never taken for it); the game's year
    /// (gameVariables.daysPerYear, GameCalendar) and its interest multiplier (gameVariables.bankInterestMultiplier,
    /// as the game's banks charge it: LoanHelper.CalculateEffectiveAnnualInterestRate); the largest rival bank's own
    /// rate (BankTerms.House, its BankSettings.annualInterestRate as the game ships it); the city's economy
    /// (CityEconomy: the cycle, the central bank's points, inflation; CityEconomyDirector.TroubleScale, business
    /// pressure's chance in it) and the price level (Core/Prices).
    ///
    /// DONE: money, as the game's own transactions (GameManager.ChangeMoneySafe) under the bank's keys at the
    /// headquarters' address, dated on the day they are for: its takings, what its savers are paid and its losses (rows
    /// among the headquarters' goods), its staff (under the game's salaries, so in the statement's salary line), all
    /// deducted by the game's tax as a business's costs are; then the day's profit kept in the bank's capital, or its
    /// loss met from it, so that the player's cash is what it was (the bank's money is the bank's: OwnBankRules). The
    /// licence and the capital put in come out of the player's cash, a dividend and a sale go into it. Nothing else of
    /// the game's is changed.
    ///
    /// Nothing here runs per frame: once a game day, and when the player does something in the app.
    /// </summary>
    public static class OwnBankGame
    {
        private const string HeadquartersType = "ba:businesstype_headquarters";
        private const string DeductKey = "reallife_ob_deduct";

        public static GameInstance Save => SaveGameManager.Current;
        public static int Today => Save != null ? Save.Day : 0;

        /// <summary>The player's cash in cents (what the game holds as money, floored).</summary>
        public static long Cash => Save != null ? (long)Math.Floor(Math.Max(0.0, Save.Money) * 100.0) : 0;

        /// <summary>The player's headquarters (the game has one at most), or null.</summary>
        public static BuildingRegistration Headquarters()
        {
            GameInstance save = Save;
            for (int i = 0; save != null && save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration reg = save.BuildingRegistrations[i];
                if (reg != null && reg.HasValidAddress && reg.businessTypeName == HeadquartersType && GoingPublic.Mine(reg))
                {
                    return reg;
                }
            }
            return null;
        }

        /// <summary>A headquarters by the name the player gave it, else its address.</summary>
        public static string Name(BuildingRegistration reg)
        {
            return reg == null ? string.Empty : string.IsNullOrEmpty(reg.BusinessName) ? reg.Address.ToFormattedString() : reg.BusinessName;
        }

        /// <summary>The city as the bank's day reads it now (see the summary for where each figure comes from).</summary>
        public static BankCity City()
        {
            GameInstance save = Save;
            float multiplier = save != null && save.gameVariables != null ? save.gameVariables.bankInterestMultiplier : 1f;
            Lender house = BankTerms.House;
            int houseRate = house != null ? BankTerms.OwnRate(house.Settings) : 0;
            Func<Lever, int> lever = OwnBankBridges.Policy;
            return new BankCity
            {
                YearDays = GameCalendar.DaysPerYear,
                MonthDays = GameCalendar.MonthDays,
                Level = Prices.Level,
                CityRateBp = OwnBankCatalogue.CityRateStart + Mathf.RoundToInt(CityEconomy.BasePoints * 100f),
                BasePointsQuarter = Mathf.RoundToInt(CityEconomy.BasePoints * MacroCatalogue.QuartersPerPoint),
                HouseRate = houseRate > 0 ? houseRate : OwnBankCatalogue.HouseRateDefault,
                Multiplier = Mathf.RoundToInt(Mathf.Max(0f, multiplier) * 1000f),
                Cycle = CityEconomy.State,
                InflationBp = Mathf.RoundToInt(CityEconomy.InflationPercent * 100f),
                Trouble = Mathf.RoundToInt(CityEconomyDirector.TroubleScale() * 1000f),
                Integrity = OwnBankBridges.Integrity != null ? Mathf.Clamp(OwnBankBridges.Integrity(), 0, 100) : 50,
                Tax = lever != null ? lever(Lever.BusinessTax) : 0,
                Grants = lever != null ? lever(Lever.Grants) : 0,
                Wages = lever != null ? lever(Lever.Wages) : 0,
                Rent = lever != null ? lever(Lever.Rent) : 0,
                Courts = OwnBankBridges.Courts != null && OwnBankBridges.Courts()
            };
        }

        /// <summary>
        /// A day of the bank booked at the headquarters (the day it is for; the address of none if the player has
        /// none): its takings; what it lost being met from its capital; what its savers were paid, its losses and fees,
        /// its staff and any fine; and what it made kept in its capital. The player's cash comes out where it was.
        /// </summary>
        public static void BookDay(BankDay d, BuildingRegistration hq, int day)
        {
            long profit = d.Profit;
            Book(d.Income, OwnBankCatalogue.TakingsKey, hq, null, day, false, false);
            if (profit < 0)
            {
                Book(-profit, OwnBankCatalogue.CoveredKey, hq, null, day, false, false);
            }
            Book(-d.Savers, OwnBankCatalogue.SaversKey, hq, null, day, true, true);
            Book(-(d.Lost + d.Court + d.FundLost + d.Fees), OwnBankCatalogue.LossesKey, hq, null, day, true, true);
            Book(-d.Staff, OwnBankCatalogue.WagesKey, hq, GameBooks.Salaries, day, true, true);
            Book(-d.Fine, OwnBankCatalogue.FineKey, hq, null, day, false, true);
            if (profit > 0)
            {
                Book(-profit, OwnBankCatalogue.KeptKey, hq, null, day, false, true);
            }
        }

        /// <summary>Money out of the player's cash for the bank (the licence, capital put in): taken only if the cash covers it. False if not.</summary>
        public static bool Pay(long cents, string key, BuildingRegistration hq, string category)
        {
            return cents <= 0 || (cents <= Cash && Book(-cents, key, hq, category, null, false, false));
        }

        /// <summary>Money into the player's cash from the bank (a dividend, a sale, what a winding up left).</summary>
        public static void Give(long cents, string key, BuildingRegistration hq)
        {
            Book(cents, key, hq, null, null, false, false);
        }

        private static bool Book(long cents, string key, BuildingRegistration hq, string category, int? day, bool deductible, bool force)
        {
            if (cents == 0 || Save == null)
            {
                return true;
            }
            var data = new Dictionary<string, string>(1) { { "place", Name(hq) } };   // (one each: the game keeps a transaction's data for as long as it lists it)
            TransactionInfo info = category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
            if (deductible && cents < 0)
            {
                info.SetTaxDeductibleName(DeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(cents / 100f, info, day, hq != null ? hq.Address : null, force && cents < 0, false);
        }
    }
}
