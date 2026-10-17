using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the player's bank asks of the rest of the mod, filled in by Wiring/OwnBankLinks; each may be null.</summary>
    public static class OwnBankBridges
    {
        /// <summary>The player's integrity as an owner, 0 to 100 (Standing). Default: 50, a newcomer's.</summary>
        public static Func<int> Integrity;

        /// <summary>Where one of the council's levers stands: -1, 0 or 1 (Civic). Default: as the city came.</summary>
        public static Func<Lever, int> Policy;

        /// <summary>True while the justice system takes a lender's claims. Default: no courts.</summary>
        public static Func<bool> Courts;

        /// <summary>Fills the list with the Stocks app's companies as the fund sees them; false while the market is not running. Default: no market.</summary>
        public static Func<List<FundQuote>, bool> Quotes;

        /// <summary>The bank moved the player's integrity (Standing): by how much, and whether as a scandal. Default: nobody hears.</summary>
        public static Action<int, bool> Moved;

        /// <summary>True if the bank has the qualified person its licence asks for (Careers). Default: yes.</summary>
        public static Func<bool> Qualified;

        /// <summary>True while Standing keeps the player's reputation, so their integrity is their own. Default: no.</summary>
        public static Func<bool> StandingOn;

        public static void Reset()
        {
            Integrity = null;
            Policy = null;
            Courts = null;
            Quotes = null;
            Moved = null;
            Qualified = null;
            StandingOn = null;
        }
    }

    /// <summary>
    /// Everything the player's bank reads from or does to the game. Its money moves as the game's transactions at the
    /// headquarters, the day's profit then kept in the bank, so the player's cash nets to nothing. Nothing runs per frame.
    /// </summary>
    public static class OwnBankGame
    {
        private const string HeadquartersType = "ba:businesstype_headquarters";
        private const string DeductKey = "reallife_ob_deduct";

        private static GameInstance Save => SaveGameManager.Current;
        public static int Today => Save != null ? Save.Day : 0;

        /// <summary>The player's integrity as an owner now, 0 to 100; 50 with no bridge.</summary>
        public static int Integrity => OwnBankBridges.Integrity != null ? Mathf.Clamp(OwnBankBridges.Integrity(), 0, 100) : 50;

        public static bool StandingOn => OwnBankBridges.StandingOn != null && OwnBankBridges.StandingOn();

        /// <summary>Why a new licence may not be applied for yet after a closure (OwnBankRules.AgainWhy, today); null if it may.</summary>
        public static string AgainWhy(OwnBank b) => OwnBankRules.AgainWhy(b, Today, GameCalendar.DaysPerYear, StandingOn, Integrity);

        /// <summary>The player's cash in cents, floored.</summary>
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

        public static BankCity City()
        {
            GameInstance save = Save;
            float multiplier = save != null && save.gameVariables != null ? save.gameVariables.bankInterestMultiplier : 1f;   // as the game's banks charge it (LoanHelper)
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
                Integrity = OwnBankGame.Integrity,
                Tax = lever != null ? lever(Lever.BusinessTax) : 0,
                Grants = lever != null ? lever(Lever.Grants) : 0,
                Wages = lever != null ? lever(Lever.Wages) : 0,
                Rent = lever != null ? lever(Lever.Rent) : 0,
                Courts = OwnBankBridges.Courts != null && OwnBankBridges.Courts(),
                Scale = OwnBankDirector.Instance != null ? OwnBankDirector.Instance.ScaleNow() : 0
            };
        }

        /// <summary>A day booked at the headquarters on the day it is for: takings, costs, and the profit kept or the loss met.</summary>
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
            Book(-d.Systems, OwnBankCatalogue.SystemsKey, hq, null, day, true, true);
            Book(-d.Fine, OwnBankCatalogue.FineKey, hq, null, day, false, true);
            if (profit > 0)
            {
                Book(-profit, OwnBankCatalogue.KeptKey, hq, null, day, false, true);
            }
        }

        /// <summary>Money from the player's cash for the bank (the licence, capital put in), only if the cash covers it. False if not.</summary>
        public static bool Pay(long cents, string key, BuildingRegistration hq, string category)
        {
            return cents <= 0 || (cents <= Cash && Book(-cents, key, hq, category, null, false, false));
        }

        /// <summary>Money into the player's cash from the bank: a dividend, a sale, what a winding up left.</summary>
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
            var data = new Dictionary<string, string>(1) { { "place", Name(hq) } };   // one each: the game keeps a transaction's data while it lists it
            TransactionInfo info = category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
            if (deductible && cents < 0)
            {
                info.SetTaxDeductibleName(DeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(cents / 100f, info, day, hq != null ? hq.Address : null, force && cents < 0, false);
        }
    }
}
