using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Texts and toasts for civic life: every string comes from en.json, {names} are filled here.</summary>
    public static class CivicText
    {
        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return key.GetLocalization().Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return key.GetLocalization().Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2).Replace("{" + name3 + "}", value3);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>What a policy does, in words, with its amounts at today's prices.</summary>
        public static string Effect(PolicyDef policy)
        {
            if (policy == null)
            {
                return string.Empty;
            }
            string text = T(policy.Effect);
            if (text.IndexOf('{') < 0)
            {
                return text;
            }
            return text.Replace("{levy}", Money(Math.Abs(CivicCatalogue.LevyNow(policy))))
                .Replace("{grant}", Money(Math.Abs(CivicCatalogue.GrantNow(policy.Dir))))
                .Replace("{ticket}", Money(Math.Abs(CivicCatalogue.TicketExtraNow(policy.Dir))))
                .Replace("{fare}", Money(Math.Abs(CivicCatalogue.FareExtraNow(policy.Dir))))
                .Replace("{floor}", Money(CivicCatalogue.WageFloorNow(policy.Dir)));
        }

        public static string Name(Candidate candidate)
        {
            return CivicCatalogue.FirstNames[candidate.First].GetLocalization() + " " + CivicCatalogue.LastNames[candidate.Last].GetLocalization();
        }

        public static string Party(Candidate candidate)
        {
            return CivicCatalogue.Parties[candidate.Party].GetLocalization();
        }

        /// <summary>A toast with no names in it. Not kept in the save: the key would mean nothing without the mod.</summary>
        public static void Toast(NotificationType type, string key, float seconds = 9f)
        {
            Notifications.Show(type, key, null, seconds, null, null, true, false);
        }

        public static void Toast(NotificationType type, string key, string name, string value, float seconds = 9f)
        {
            // A new dictionary each time: the game keeps hold of it for as long as the toast is listed.
            var data = new Dictionary<string, string>(1) { { name, value } };
            Notifications.Show(type, key, data, seconds, null, null, true, false);
        }

        public static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, float seconds = 9f)
        {
            var data = new Dictionary<string, string>(2) { { name, value }, { name2, value2 } };
            Notifications.Show(type, key, data, seconds, null, null, true, false);
        }

        public static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, string name3, string value3, float seconds = 9f)
        {
            var data = new Dictionary<string, string>(3) { { name, value }, { name2, value2 }, { name3, value3 } };
            Notifications.Show(type, key, data, seconds, null, null, true, false);
        }
    }

    /// <summary>
    /// Makes the policies real. Every effect is one of three kinds, and all of them are undone
    /// by <see cref="Revert"/> (called when the option is switched off, when the city unloads
    /// and before anything is applied afresh):
    ///
    /// 1. A multiplier on a value the mod itself owns, or on one the game keeps only in memory:
    ///    the police presence, patrol, street-crime and traffic-accident options of this mod
    ///    (Core/Settings fields, never written to the options file from here), the weight of
    ///    the street sweeper in city life's catalogue, and each neighbourhood's street-parking
    ///    price (a field of the game's neighbourhood data that is not part of a save). The
    ///    value found is remembered and put back exactly.
    /// 2. A labelled transaction worked out from the game's own figures, because the figure
    ///    itself is part of the save and must not be changed: income tax (the game's rate x the
    ///    day's sales), the trade levy (sales), rent (each lease's daily rent), wages (each
    ///    employee's hourly wage and rota), grants, the city levy; settled once a week. Fines,
    ///    parking tickets and subway rides are topped up or part-refunded as they are charged
    ///    (found by reading the day's transactions once an hour).
    /// 3. A mood modifier of the mod's own (Core/ModState keeps those out of the save).
    ///
    /// Nothing of the game's that is saved is ever written: not the tax rate, not a rent, not a
    /// wage. A save made under any policy loads without the mod exactly as the game left it.
    /// </summary>
    public sealed class CivicEffects
    {
        private const string FineType = "reallife_transaction_fine";
        private const string RideType = "ba:transaction_subwayride";
        private const string SweeperId = "sweeper";

        /// <summary>One of the mod's own integer options, scaled. What the player set is remembered and put back.</summary>
        private struct Knob
        {
            public bool Held;      // we have changed it
            public int Base;       // what the player's option says
            public int Applied;    // what we last wrote
        }

        private Knob _presence, _patrol, _crime, _accidents;
        private float[] _parkingBase;          // each neighbourhood's own street-parking price, while ours is in force
        private NeighborhoodData[] _parkingOf;
        private CityEntry _sweeper;
        private int _sweeperBase = -1;
        private bool _applied;

        /// <summary>
        /// Applies where the levers stand. Called when they change, on load, and once an hour (the
        /// integer options are dithered: 2 scaled by 1.25 is 2 three hours in four and 3 the fourth).
        /// </summary>
        public void Apply(int[] levers)
        {
            _applied = true;
            int policing = levers[(int)Lever.Policing];
            int nightLife = levers[(int)Lever.NightLife];
            int traffic = levers[(int)Lever.Traffic];
            Scale(ref _presence, ref Settings.PolicePresence, CivicCatalogue.PoliceScale(policing), 0, 10);
            Scale(ref _patrol, ref Settings.PatrolFrequency, CivicCatalogue.PoliceScale(policing), 1, 10);
            Scale(ref _crime, ref Settings.CrimeFrequency, CivicCatalogue.CrimeScale(policing, nightLife), 1, 10);
            Scale(ref _accidents, ref Settings.AccidentFrequency, CivicCatalogue.AccidentScale(traffic), 1, 10);
            Parking(CivicCatalogue.ParkingScale(levers[(int)Lever.Parking]));
            Sweepers(CivicCatalogue.SweeperScale(levers[(int)Lever.Cleaning]));
        }

        /// <summary>Everything back as it was found. Safe at any time, any number of times.</summary>
        public void Revert()
        {
            if (!_applied)
            {
                return;
            }
            _applied = false;
            Restore(ref _presence, ref Settings.PolicePresence);
            Restore(ref _patrol, ref Settings.PatrolFrequency);
            Restore(ref _crime, ref Settings.CrimeFrequency);
            Restore(ref _accidents, ref Settings.AccidentFrequency);
            Parking(1f);
            Sweepers(1f);
            ModState.RemoveModifier(ModState.CivicClean);
            ModState.RemoveModifier(ModState.CivicLitter);
            ModState.RemoveModifier(ModState.CivicFestive);
        }

        private static void Scale(ref Knob knob, ref int setting, float scale, int min, int max)
        {
            if (knob.Held && setting != knob.Applied)
            {
                knob.Base = setting;   // the player moved the option meanwhile: that is the new base
            }
            else if (!knob.Held)
            {
                knob.Base = setting;
            }
            if (Mathf.Approximately(scale, 1f))
            {
                if (knob.Held)
                {
                    setting = knob.Base;
                    knob.Held = false;
                }
                return;
            }
            float wanted = knob.Base * scale;
            int whole = Mathf.FloorToInt(wanted);
            int value = Mathf.Clamp(whole + (Random.value < wanted - whole ? 1 : 0), min, max);
            if (knob.Base <= 0)
            {
                value = knob.Base;   // switched off by the player stays off
            }
            setting = value;
            knob.Applied = value;
            knob.Held = true;
        }

        private static void Restore(ref Knob knob, ref int setting)
        {
            if (knob.Held)
            {
                if (setting == knob.Applied)
                {
                    setting = knob.Base;
                }
                knob.Held = false;
            }
        }

        // Street-parking prices: the game reads each neighbourhood's price once an hour for every legally parked car.
        private void Parking(float scale)
        {
            try
            {
                if (_parkingBase != null)
                {
                    for (int i = 0; i < _parkingOf.Length; i++)
                    {
                        if (_parkingOf[i] != null)
                        {
                            _parkingOf[i].parkingPrice = _parkingBase[i];
                        }
                    }
                    _parkingBase = null;
                    _parkingOf = null;
                }
                NeighborhoodData[] all = NeighborhoodHelper.NeighborhoodsData;
                if (Mathf.Approximately(scale, 1f) || all == null || all.Length == 0)
                {
                    return;
                }
                _parkingOf = all;
                _parkingBase = new float[all.Length];
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                    {
                        _parkingBase[i] = all[i].parkingPrice;
                        all[i].parkingPrice = Mathf.Round(all[i].parkingPrice * scale * 100f) / 100f;
                    }
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        private void Sweepers(float scale)
        {
            if (_sweeper == null)
            {
                CityEntry[] all = CityCatalogue.All;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].Id == SweeperId)
                    {
                        _sweeper = all[i];
                    }
                }
                if (_sweeper == null)
                {
                    return;
                }
            }
            if (_sweeperBase >= 0)
            {
                _sweeper.Weight = _sweeperBase;
                _sweeperBase = -1;
            }
            if (!Mathf.Approximately(scale, 1f))
            {
                _sweeperBase = _sweeper.Weight;
                _sweeper.Weight = Mathf.Max(1, Mathf.RoundToInt(_sweeper.Weight * scale));
            }
        }

        // ---------------------------------------------------------------- once an hour

        /// <summary>The moods a policy keeps up, and the top-ups on what was charged this hour.</summary>
        public void Hourly(GovState state, int day)
        {
            int cleaning = state.Levers[(int)Lever.Cleaning];
            if (cleaning > 0)
            {
                Keep(ModState.CivicClean, 3);
            }
            else if (cleaning < 0)
            {
                Keep(ModState.CivicLitter, 3);
            }
            if (state.Levers[(int)Lever.Events] > 0)
            {
                Keep(ModState.CivicFestive, 3);
            }
            Charges(state, day);
        }

        /// <summary>
        /// Keeps one of the mod's moods up for some hours more. If the player already has it only its hours are
        /// topped up: adding it again through the game would play the "happier" face over the player every hour.
        /// </summary>
        public static void Keep(string type, int hours)
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.happinessModifiers != null)
            {
                for (int i = 0; i < save.happinessModifiers.Count; i++)
                {
                    HappinessModifierData modifier = save.happinessModifiers[i];
                    if (modifier != null && modifier.type == type)
                    {
                        if (modifier.hoursLeft < hours)
                        {
                            modifier.hoursLeft = hours;
                        }
                        return;
                    }
                }
            }
            ModState.AddModifier(type, hours);
        }

        // Fines, subway rides and parking tickets charged since the last look: each is topped up or part-refunded.
        private static void Charges(GovState state, int day)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            bool first = state.SeenDay <= 0;   // the first look in this save: what was charged before is not ours to touch
            if (state.SeenDay != day)
            {
                if (state.SeenDay > 0 && state.SeenDay == day - 1)
                {
                    Settle(state, save, state.SeenDay, false, true);
                }
                state.SeenDay = day;
                state.SeenFines = 0f;
                state.SeenRides = 0f;
            }
            Settle(state, save, day, first, false);

            int tickets = save.achievementsData != null ? save.achievementsData.parkingTickets : 0;
            if (state.SeenTickets < 0 || tickets < state.SeenTickets)
            {
                state.SeenTickets = tickets;
            }
            int extra = CivicCatalogue.TicketExtraNow(state.Levers[(int)Lever.Parking]);
            if (tickets > state.SeenTickets && extra != 0)
            {
                int count = Mathf.Min(tickets - state.SeenTickets, 5);
                Pay(-extra * count, "reallife_transaction_civic_ticket");
                CivicText.Toast(extra > 0 ? NotificationType.Warning : NotificationType.Info,
                    extra > 0 ? "reallife_civic_charge_ticket_up" : "reallife_civic_charge_ticket_down", "amount", CivicText.Money(Mathf.Abs(extra * count)));
            }
            state.SeenTickets = tickets;
        }

        // dayDone: the day is over (it is settled one last time on the next): what is in its books is all there will be.
        private static void Settle(GovState state, GameInstance save, int day, bool lookOnly, bool dayDone)
        {
            float fines = 0f, rides = 0f;
            Queue<Transaction> all = save.Transactions;
            if (all != null)
            {
                foreach (Transaction entry in all)
                {
                    if (entry == null || entry.timestamp == null || entry.timestamp.Day != day)
                    {
                        continue;
                    }
                    if (entry.amount < 0f && entry.transactionType == FineType)
                    {
                        fines -= entry.amount;
                    }
                    else if (entry.amount < 0f && entry.transactionType == RideType)
                    {
                        rides -= entry.amount;
                    }
                    else if (entry.amount > 0f && entry.transactionType == CivicBridges.FareGivenBack)
                    {
                        rides -= entry.amount;   // a ride the day pass covered: the game's fare was given back, so it was not paid
                    }
                }
            }
            float newFines = fines - state.SeenFines;
            float fineScale = CivicCatalogue.FineScale(state.Levers[(int)Lever.Traffic]);
            if (!lookOnly && newFines > 0.5f && !CivicBridges.FinesScaledAtSource && !Mathf.Approximately(fineScale, 1f))
            {
                int change = Mathf.RoundToInt(newFines * (fineScale - 1f));
                Pay(-change, "reallife_transaction_civic_fine");
                CivicText.Toast(change > 0 ? NotificationType.Warning : NotificationType.Info,
                    change > 0 ? "reallife_civic_charge_fine_up" : "reallife_civic_charge_fine_down", "amount", CivicText.Money(Mathf.Abs(change)));
            }
            state.SeenFines = Mathf.Max(state.SeenFines, fines);

            // A ride is counted once its train is in: a day pass gives the game's fare back a moment after the game takes it,
            // and the game takes it in the frame its ride begins, so by then both are in the books.
            if (SubwaySystem.IsRiding && !dayDone)
            {
                return;
            }
            float newRides = rides - state.SeenRides;
            float fareExtra = CivicCatalogue.FareExtraNow(state.Levers[(int)Lever.Transit]);
            if (!lookOnly && newRides > 0.5f && !CivicBridges.FaresScaledAtSource && fareExtra != 0)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(newRides / 3f), 1, 20);   // the game's subway ride is 3
                Pay(-fareExtra * count, "reallife_transaction_civic_fare");
            }
            state.SeenRides = Mathf.Max(state.SeenRides, rides);
        }

        // ---------------------------------------------------------------- once a day, and the weekly settlement

        /// <summary>Yesterday's sales run up tax and levy; on the seventh day everything is settled.</summary>
        public void Daily(GovState state, int day)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            float sales = SalesOn(save, day - 1);
            float rate = save.gameVariables != null ? save.gameVariables.taxPercentage / 100f : 0.1f;
            if (IncomeTaxed(day - 1))
            {
                state.OwedTax += sales * rate * CivicCatalogue.TaxShare(state.Levers[(int)Lever.BusinessTax]);
            }
            state.OwedTrade += sales * CivicCatalogue.TradeShare(state.Levers[(int)Lever.TradeLevy]);
            if (state.SettleDay <= 0)
            {
                state.SettleDay = day + 7;
            }
            if (day >= state.SettleDay)
            {
                state.SettleDay = day + 7;
                SettleWeek(state, save);
            }
        }

        private static float SalesOn(GameInstance save, int day)
        {
            float sales = 0f;
            List<FinancialSummary> summaries = save.financialSummaries;
            if (summaries == null)
            {
                return 0f;
            }
            for (int i = summaries.Count - 1; i >= 0; i--)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.dayNumber != day || summary.businessIncomeStatements == null)
                {
                    continue;
                }
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    if (summary.businessIncomeStatements[b] != null)
                    {
                        sales += Mathf.Max(0f, summary.businessIncomeStatements[b].TotalSales);
                    }
                }
                break;
            }
            return sales;
        }

        /// <summary>The lines of the weekly account, as they stand now: what a settlement today would charge (positive) or pay (negative).</summary>
        public struct Account
        {
            public float Levy, Tax, Trade, Grants, Rent, Landlord, Wages;

            public float Total => Levy + Tax + Trade + Grants + Rent + Landlord + Wages;
        }

        /// <summary>Works the account out from the save as it is now. A handful of list walks: for a settlement or a phone page, never a tick.</summary>
        public static Account Reckon(GovState state)
        {
            var account = new Account();
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return account;
            }
            for (int i = 0; i < CivicCatalogue.Levers; i++)
            {
                PolicyDef policy = CivicCatalogue.Find((Lever)i, state.Levers[i]);
                if (policy != null)
                {
                    account.Levy += CivicCatalogue.LevyNow(policy);
                }
            }
            account.Tax = state.OwedTax;
            account.Trade = state.OwedTrade;

            int businesses = 0;
            float rents = 0f;
            List<BuildingRegistration> registrations = save.BuildingRegistrations;
            if (registrations != null)
            {
                for (int i = 0; i < registrations.Count; i++)
                {
                    BuildingRegistration registration = registrations[i];
                    if (registration == null || !GoingPublic.Mine(registration))
                    {
                        continue;
                    }
                    if (registration.HasEstablishedBusiness)
                    {
                        businesses++;
                    }
                    if (registration.RentPerDay > 0f && !Owned(registration))
                    {
                        rents += registration.RentPerDay;
                    }
                }
            }
            account.Grants = -CivicCatalogue.GrantNow(state.Levers[(int)Lever.Grants]) * Mathf.Min(businesses, CivicCatalogue.GrantBusinesses);
            account.Rent = rents * 7f * CivicCatalogue.RentShare(state.Levers[(int)Lever.Rent]);

            float collected = 0f;
            if (save.realEstate != null)
            {
                for (int i = 0; i < save.realEstate.Count; i++)
                {
                    if (save.realEstate[i] != null)
                    {
                        collected += Mathf.Max(0f, save.realEstate[i].pricePerSqm * save.realEstate[i].totalSqm * Mathf.Clamp01(save.realEstate[i].occupancy / 100f));
                    }
                }
            }
            account.Landlord = -collected * 7f * CivicCatalogue.LandlordShare(state.Levers[(int)Lever.Rent]);

            float floor = CivicCatalogue.WageFloorNow(state.Levers[(int)Lever.Wages]);
            float subsidy = CivicCatalogue.WageSubsidy(state.Levers[(int)Lever.Wages]);
            if ((floor > 0f || subsidy > 0f) && save.EmployeeInstances != null)
            {
                float level = Prices.Level / 1000f;   // the city's cost of living pays every wage at today's prices (Economy/City)
                Func<string, float> raise = CivicBridges.Raise;
                for (int i = 0; i < save.EmployeeInstances.Count; i++)
                {
                    EmployeeInstance employee = save.EmployeeInstances[i];
                    if (employee == null || employee.assignedWeeklyHours <= 0 || employee.hourlyWage <= 0f)
                    {
                        continue;
                    }
                    // What they are paid an hour now: the game's wage at today's prices, and any raise agreed on top (Economy/Company).
                    float paid = employee.hourlyWage * level + (raise != null ? raise(employee.id) : 0f);
                    if (floor > paid)
                    {
                        account.Wages += (floor - paid) * employee.assignedWeeklyHours;
                    }
                    account.Wages -= paid * employee.assignedWeeklyHours * subsidy;
                }
            }
            return account;
        }

        // The game charges income tax at the end of its tax year only on a year of business sales (and gambling winnings) of
        // 150,000 or more (TaxHelper.MeetsIncomeTaxThreshold): the city's share of it follows, on the tax year so far
        // projected to a whole one, as the game's own EconoView projects it.
        private static bool IncomeTaxed(int day)
        {
            int year = GameCalendar.DaysPerYear;
            day = Math.Max(1, day);
            int start = (day - 1) / year * year + 1;
            int days = Math.Max(1, day - start + 1);
            float projected = TaxCalculationHelper.GetBusinessSales(start, day) / days * year;
            return TaxHelper.MeetsIncomeTaxThreshold(projected, TaxCalculationHelper.GetCurrentTaxableGamblingWinnings());
        }

        private static bool Owned(BuildingRegistration registration)
        {
            try
            {
                return registration.BuildingOwnedByPlayer;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SettleWeek(GovState state, GameInstance save)
        {
            Account account = Reckon(state);
            state.OwedTax = 0f;
            state.OwedTrade = 0f;
            Pay(-account.Levy, "reallife_transaction_civic_levy");
            Pay(-account.Tax, "reallife_transaction_civic_tax");
            Pay(-account.Trade, "reallife_transaction_civic_trade");
            Pay(-account.Grants, account.Grants <= 0f ? "reallife_transaction_civic_grant" : "reallife_transaction_civic_licence");
            Pay(-(account.Rent + account.Landlord), "reallife_transaction_civic_rent");
            Pay(-account.Wages, "reallife_transaction_civic_wages");
            float total = Mathf.Round(account.Total);
            if (Mathf.Abs(total) >= 1f)
            {
                CivicText.Toast(total > 0f ? NotificationType.Warning : NotificationType.Success,
                    total > 0f ? "reallife_civic_week_charged" : "reallife_civic_week_paid", "amount", CivicText.Money(Mathf.Abs(total)), 12f);
            }
            CivicDirector.Logger?.Info("Civic: weekly account settled: levy " + account.Levy.ToString("F0") + ", tax " + account.Tax.ToString("F0") + ", trade " + account.Trade.ToString("F0")
                                       + ", grants " + account.Grants.ToString("F0") + ", rent " + (account.Rent + account.Landlord).ToString("F0") + ", wages " + account.Wages.ToString("F0") + " (positive is charged).");
        }

        /// <summary>Money to the player (positive) or from them (negative), under a label of the mod's. Whole dollars; nothing under one.</summary>
        public static void Pay(float amount, string transactionKey)
        {
            amount = Mathf.Round(amount);
            if (Mathf.Abs(amount) < 1f || SaveGameManager.Current == null)
            {
                return;
            }
            GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, null, force: true);
        }
    }
}
