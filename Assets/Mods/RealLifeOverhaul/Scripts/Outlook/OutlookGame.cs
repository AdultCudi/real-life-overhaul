using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Entities;
using Helpers;
using RealLifeOverhaul.Economy;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// Everything the Economy app reads of the game, in one file: the city's economy and what the game keeps of each
    /// district, the player's debts, rent, wages and shops, and the tax. What other features keep comes through
    /// <see cref="OutlookBridges"/>. It only reads and writes nothing; each call is a pass over a dozen districts and a few
    /// lists, made when the app is written (once an hour while it is open, and once a day for the history), never per frame.
    /// </summary>
    internal static class OutlookGame
    {
        /// <summary>One look at the game into <paramref name="r"/>; false while there is no save to look at. A fault in one part leaves that part unread.</summary>
        public static bool Read(OutlookReadings r)
        {
            GameInstance save = SaveGameManager.Current;
            r.Begin();
            if (save == null)
            {
                r.Done();
                return false;
            }
            r.Day = save.Day;
            r.Hour = save.Hour;
            Part("the city", () => City(r, save));
            Part("the player", () => Player(r, save));
            Part("the player's places", () => Places(r, save));
            Part("the tax", () => Tax(r, save));
            Part("the jobs", () => Jobs(r));
            Part("the market", () => Market(r));
            Part("the council", () => Council(r));
            r.Done();
            if (r.CityOn)
            {
                OutlookRules.Rank(r);
            }
            return true;
        }

        private static void Part(string what, Action read)
        {
            try
            {
                read();
            }
            catch (Exception e)
            {
                OutlookDirector.Logger?.Warn("Economy app: " + what + " could not be read, so that part shows as not kept: " + e.GetType().Name + ": " + e.Message);
            }
        }

        // ------------------------------------------------------------------ the city

        private static void City(OutlookReadings r, GameInstance save)
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            CityState state = director != null ? director.State : null;
            if (state == null)
            {
                return;
            }
            int level = director.Level;
            r.CityOn = true;
            r.Cycle = (int)state.Cycle;
            r.MonthsIn = state.MonthsIn;
            r.Level = level;
            r.Prices = state.Index;
            r.Inflation = state.Inflation;
            r.Rate = state.Base;
            r.RateBp = OwnBankCatalogue.CityRateStart + state.Base * (100 / MacroCatalogue.QuartersPerPoint);
            r.MonthChange = state.MonthChange;
            r.LastMove = state.LastMove;
            r.Pending = state.Pending;
            r.PendingMove = state.PendingMove;
            r.DaysToDecision = CityModel.DaysToTurn(state, director.MonthDays);
            r.Footfall = CityModel.CycleFootfall(state.Cycle, level);
            r.Spend = CityModel.Spend(state.Cycle, level);
            r.Takings = CityModel.Takings(state, level);
            r.Costs = CityModel.Costs(state);

            for (int k = 0; k < OutlookReadings.Kinds && k < state.Supply.Length; k++)
            {
                SupplyLine line = state.Supply[k];
                OutlookSupply s = r.Supply[k];
                s.Factor = CityModel.Wholesale(state, k);
                s.Stocked = (state.Buys & (1 << k)) != 0;
                if (line.Announced(state.Age))
                {
                    s.Shock = (int)line.Shock;
                    s.Announced = true;
                    s.Days = line.Days;
                    s.Percent = CityModel.ShockPercent(line, level);
                }
                else if (line.Running(state.Age))
                {
                    s.Shock = (int)line.Shock;
                    s.Days = Math.Max(1, line.From + line.Days - state.Age);
                    s.Percent = CityModel.ShockPercent(line, level);
                }
            }

            HashSet<string> mine = Mine(director, save);
            for (int i = 0; i < state.Districts.Count && i < OutlookReadings.MostDistricts; i++)
            {
                CityDistrict district = state.Districts[i];
                OutlookDistrict d = r.NextDistrict();
                Fill(r, d, district, state, level, mine);
            }
            r.Income = OutlookRules.MeanIncome(r);
            r.Property = OutlookRules.MeanWorth(r);
        }

        // The districts the player has a business or a building in.
        private static HashSet<string> Mine(CityEconomyDirector director, GameInstance save)
        {
            var mine = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<CityShop> shops = director.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                if (shops[i].District != null)
                {
                    mine.Add(shops[i].District.Name);
                }
            }
            List<RealEstate> owned = save.realEstate;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                string district = owned[i] != null && owned[i].address != null ? CityGame.DistrictOf(owned[i]) : null;
                if (!string.IsNullOrEmpty(district))
                {
                    mine.Add(district);
                }
            }
            return mine;
        }

        private static void Fill(OutlookReadings r, OutlookDistrict d, CityDistrict district, CityState state, int level, HashSet<string> mine)
        {
            d.Id = district.Name;
            d.Start = district.Start;
            d.Income = district.Income;
            d.Before = district.Before;
            d.Footfall = CityModel.DistrictFootfall(district);
            d.Bears = CityModel.Tolerance(state, district, level);
            d.Mine = mine.Contains(district.Name);
            NeighborhoodData data = null;
            try
            {
                data = NeighborhoodHelper.GetData(district.Name);
            }
            catch (Exception)
            {
                data = null;   // a district the game's data lacks: shown with the figures the city's economy keeps
            }
            if (data != null)
            {
                d.Colour = Rgb(data.cityMapZoneColor);
                d.Working = data.workingClassPercentage;
                d.Middle = data.middleClassPercentage;
                d.Upper = data.upperClassClassPercentage;
                d.Weight = Mathf.RoundToInt(Mathf.Clamp(data.customerDemandsWeight, 0f, 20f) * 1000f);
                d.Traffic = data.vehicleTrafficDensityPercentage;
            }
            else
            {
                d.Colour = Fallback(district.Name);
            }
            int[] demand = d.Demand;
            int market = 0, growing = -1;
            OutlookBridges.DistrictReader reader = OutlookBridges.District;
            if (reader != null && reader(district.Name, demand, out market, out growing))
            {
                d.Level = market;
                d.Growing = growing;
            }
            int worth = CityModel.Property(state, district, level);   // the city's part: the cycle, the district against its start and the prices
            d.Property = OutlookRules.Worth((int)state.Cycle, state.Index, district.Income, district.Start, level, d.Level);
            d.Land = data != null ? (int)Mathf.Clamp(data.baseBuildingPricePerSqm * data.realEstateMultiplier * worth / 1000f, 0f, 1e9f) : 0;
        }

        /// <summary>A colour as 0xRRGGBB.</summary>
        public static int Rgb(Color c)
        {
            return (Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255) << 16) | (Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255) << 8) | Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        }

        // A district the game gives no map colour: one from its name, always the same.
        private static int Fallback(string name)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; name != null && i < name.Length; i++)
                {
                    h = h * 31 + name[i];
                }
                var hue = (h & 0x7FFFFFFF) % 360 / 360f;
                return Rgb(Color.HSVToRGB(hue, 0.55f, 0.85f));
            }
        }

        // ------------------------------------------------------------------ the player

        private static void Player(OutlookReadings r, GameInstance save)
        {
            CreditFile file = CreditLedger.File;
            long owed = 0;
            List<Loan> loans = save.Loans;
            for (int i = 0; loans != null && i < loans.Count; i++)
            {
                owed += (long)loans[i].remainingAmount;
            }
            if (file != null)
            {
                for (int i = 0; i < file.Accounts.Count; i++)
                {
                    CreditAccount a = file.Accounts[i];
                    if (Books.Owing(a))
                    {
                        owed += (long)a.Owed;
                    }
                }
                if (file.Shark.Active)
                {
                    owed += (long)file.Shark.Owed;
                }
                Obligations due = Books.Tomorrow(file);
                r.WagesTomorrow = Mathf.RoundToInt(due.Wages);
                r.RentTomorrow = Mathf.RoundToInt(due.Rent);
            }
            r.Debt = owed;
            CityEconomyDirector director = CityEconomyDirector.Instance;
            r.Shops = director != null ? director.Shops.Count : 0;
            r.Buildings = save.realEstate != null ? save.realEstate.Count : 0;
        }

        // The player's businesses against what their districts bear, and their buildings at today's value: what the Finance app's page
        // for the city used to list. A dozen of each at most; the rest are counted.
        private static void Places(OutlookReadings r, GameInstance save)
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            CityState state = director != null ? director.State : null;
            if (state == null)
            {
                return;
            }
            int level = director.Level;
            r.Carried = state.CarryRent + state.CarryWages + state.CarryTakings + state.CarryLiving;
            IReadOnlyList<CityShop> shops = director.Shops;
            float multiplier = save.gameVariables != null ? save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            int listed = 0;
            for (int i = 0; i < shops.Count; i++)
            {
                CityShop shop = shops[i];
                if (shop.Reg == null)
                {
                    continue;
                }
                if (listed >= OutlookReadings.MostPlaces)
                {
                    r.MoreBusinesses++;
                    continue;
                }
                listed++;
                OutlookPlace place = r.NextBusiness();
                place.Name = string.IsNullOrEmpty(shop.Reg.BusinessName) ? shop.Reg.Address.ToFormattedString() : shop.Reg.BusinessName;
                place.Customers = shop.Applied ? shop.Achieved(multiplier) : 0;
                place.Known = shop.HasFigure || shop.Wanted == 0;
                place.Prices = shop.Against <= 0 ? 0 : shop.Over ? 2 : 1;
                if (shop.District != null)
                {
                    place.District = shop.District.Name;
                    place.Colour = ColourOf(r, place.District);
                }
            }
            listed = 0;
            List<RealEstate> owned = save.realEstate;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                RealEstate estate = owned[i];
                if (estate == null || estate.address == null)
                {
                    continue;
                }
                float value = CityGame.MarketValue(estate);
                if (value <= 0f)
                {
                    continue;
                }
                if (listed >= OutlookReadings.MostPlaces)
                {
                    r.MoreEstates++;
                    continue;
                }
                listed++;
                string district = CityGame.DistrictOf(estate);
                int factor = CityModel.Property(state, state.District(district), level);
                OutlookPlace place = r.NextEstate();
                place.Name = estate.address.ToFormattedString();
                place.District = district;
                place.Colour = string.IsNullOrEmpty(district) ? 0 : ColourOf(r, district);
                place.Value = (long)value;
                place.Today = (long)(value * (factor / 1000f));
                place.Factor = factor;
            }
        }

        // A district's colour on the city map, from the districts already read; one made from its name where the game has none.
        private static int ColourOf(OutlookReadings r, string district)
        {
            for (int i = 0; i < r.DistrictCount && i < r.Districts.Count; i++)
            {
                if (r.Districts[i].Id == district)
                {
                    return r.Districts[i].Colour;
                }
            }
            return Fallback(district);
        }

        private static void Tax(OutlookReadings r, GameInstance save)
        {
            r.TaxPercent = save.gameVariables != null ? save.gameVariables.taxPercentage : 0;
            if (TaxHelper.HasCurrentTaxesToPay())
            {
                r.TaxDue = Mathf.RoundToInt(TaxHelper.GetCurrentTaxesToPay());
                r.TaxDay = TaxHelper.GetCurrentTaxesDueDay();
            }
        }

        // ------------------------------------------------------------------ what other features keep

        private static void Jobs(OutlookReadings r)
        {
            OutlookBridges.JobsReader reader = OutlookBridges.Jobs;
            if (reader != null && r.CityOn && reader(r.Cycle, out int unemployment, out int vacancies, out int seekers, out int layoffs))
            {
                r.JobsOn = true;
                r.Unemployment = unemployment;
                r.Vacancies = vacancies;
                r.Seekers = seekers;
                r.Layoffs = layoffs;
            }
        }

        private static void Market(OutlookReadings r)
        {
            OutlookBridges.MarketReader reader = OutlookBridges.Market;
            if (reader != null)
            {
                reader(r);
            }
        }

        private static void Council(OutlookReadings r)
        {
            OutlookBridges.CouncilReader reader = OutlookBridges.Council;
            if (reader != null)
            {
                reader(r);
            }
        }
    }
}
