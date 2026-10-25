using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the people part reads from or does to the game, each the game's own way: the player's professionals, their skill,
    /// hours and wage, rises within WageRules' cap, sickness, leaving, and money as dated transactions at the business.
    /// </summary>
    internal static class TalentGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { DeductKey.GetLocalization() };

        public const string WagesKey = "reallife_transaction_tech_wages";
        public const string SeveranceKey = "reallife_transaction_tech_severance_wage";
        public const string TrainingKey = "reallife_transaction_tech_training";
        public const string ClauseKey = "reallife_transaction_tech_bonus_clause";
        private const string DeductKey = "reallife_tech_deduct";
        private const string Salaries = "ba:transactioncategory_salaryexpenses";
        private const string Office = "ba:buildingtype_office";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>True for an employee whose main skill is a professional one; customer service only at an office, not at a shop's till.</summary>
        public static bool Professional(EmployeeInstance e)
        {
            try
            {
                if (e == null || e.IsCandidate || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0
                    || e.satisfaction <= 0f)
                {
                    return false;
                }
                string skill = e.characterData.skills[0].name;
                if (!TalentCatalogue.Professional(skill))
                {
                    return false;
                }
                if (skill != TalentCatalogue.CustomerService)
                {
                    return true;
                }
                BuildingRegistration reg = e.assignedAddress != null ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
                return reg != null && reg.GetBuildingType() == Office;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The player's professionals, but another player's in a Going Public session. One pass over the save's employees.</summary>
        public static void Employees(List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            EmployeeInstance self = null;
            try
            {
                self = PlayerHelper.GetPlayerEmployeeInstance();
            }
            catch (Exception)
            {
            }
            bool session = GoingPublic.InSession;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e != null && e != self && !e.isBeingReplaced && Professional(e) && !(session && GoingPublic.Foreign(Business(e))))
                {
                    into.Add(e);
                }
            }
        }

        public static string Skill(EmployeeInstance e) => e.characterData.skills[0].name;

        public static int MainPoints(EmployeeInstance e) => Mathf.Clamp(Mathf.FloorToInt(e.characterData.skills[0].value), 0, 100);

        public static string NameOf(EmployeeInstance e)
        {
            return e != null && e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?";
        }

        public static bool Working(EmployeeInstance e)
        {
            try
            {
                return e.IsWorking();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's count of hours worked today; at midnight it still holds the day before.</summary>
        public static int WorkedToday(EmployeeInstance e) => e != null ? Math.Max(0, e.workedHoursToday) : 0;

        /// <summary>True while a business is closed by the game's own switch (temporarily closed): nobody works there then.</summary>
        public static bool Shut(BuildingRegistration reg) => reg != null && reg.temporarilyClosed;

        public static int WeekHours(EmployeeInstance e) => Math.Max(0, e.workedHoursThisWeek);

        public static int WageCents(EmployeeInstance e) => Mathf.RoundToInt(e.hourlyWage * 100f);

        public static BuildingRegistration Business(EmployeeInstance e)
        {
            try
            {
                return e != null && e.assignedAddress != null ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A building's place key, kept per building so after the first ask it is a dictionary hit with no string made.</summary>
        public static string Place(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return string.Empty;
            }
            if (!ReferenceEquals(_keysOf, Save) || Keys.Count >= KeysMost)
            {
                Keys.Clear();
                _keysOf = Save;
            }
            if (Keys.TryGetValue(reg, out PlaceKey kept) && ReferenceEquals(kept.Street, reg.StreetName) && kept.Number == reg.StreetNumber)
            {
                return kept.Text;
            }
            string text = PlaceRules.Key(reg.StreetName, reg.StreetNumber);
            Keys[reg] = new PlaceKey { Street = reg.StreetName, Number = reg.StreetNumber, Text = text };
            return text;
        }

        private sealed class PlaceKey
        {
            public string Street;
            public int Number;
            public string Text;
        }

        private const int KeysMost = 8192;   // buildings whose keys are kept; the city has about 2,000, so more means another city's are in it
        private static readonly Dictionary<BuildingRegistration, PlaceKey> Keys = new Dictionary<BuildingRegistration, PlaceKey>(256);
        private static GameInstance _keysOf;

        /// <summary>The player's building with a place key, null if none; other numbers are passed over on an int compare.</summary>
        public static BuildingRegistration Find(string place)
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            if (all == null || string.IsNullOrEmpty(place))
            {
                return null;
            }
            bool numbered = PlaceRules.Number(place, out int number);
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && (!numbered || reg.StreetNumber == number) && GoingPublic.Mine(reg) && reg.HasValidAddress && Place(reg) == place)
                {
                    return reg;
                }
            }
            return null;
        }

        public static string NameOf(BuildingRegistration reg)
        {
            return reg == null ? "?" : !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : reg.GetDisplayName();
        }

        public static string TypeOf(BuildingRegistration reg) => reg != null ? reg.businessTypeName ?? string.Empty : string.Empty;

        /// <summary>The player's businesses that can employ professionals: every named business but an empty one. One pass over the buildings.</summary>
        public static void Businesses(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName)
                    && reg.businessTypeName != "ba:businesstype_empty")
                {
                    into.Add(reg);
                }
            }
        }

        /// <summary>The game's own wage for a skill at a value, cents an hour.</summary>
        public static int GameRate(string skill, int points)
        {
            try
            {
                string rated = string.IsNullOrEmpty(skill) ? TalentCatalogue.RateSkill : skill;
                return Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(rated, Mathf.Clamp(points, 0, 100)) * 100f);
            }
            catch (Exception)
            {
                return TalentCatalogue.RateLeast;
            }
        }

        /// <summary>The game's own wage for the employee's main skill, cents an hour, which bounds every rise of the mod's. 0 if it cannot be read.</summary>
        public static int FormulaCents(EmployeeInstance e)
        {
            try
            {
                return e != null && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null
                    ? Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(e.characterData.skills[0]) * 100f)
                    : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>A rise in basis points on the game's wage: never lowers it, never past WageRules' cap for the skill. True if it rose.</summary>
        public static bool Rise(EmployeeInstance e, int bp)
        {
            int formula = FormulaCents(e), before = WageCents(e), after = WageRules.Rise(before, bp, formula);
            if (after <= before)
            {
                Logger?.Info("Tech: " + NameOf(e) + "'s wage stays at " + (before / 100f).ToString("F2", CultureInfo.InvariantCulture)
                             + " (the most a raise of the mod's takes it to is " + (WageRules.Cap(formula) / 100f).ToString("F2", CultureInfo.InvariantCulture) + ").");
                return false;
            }
            e.hourlyWage = after / 100f;
            Logger?.Info("Tech: " + NameOf(e) + "'s wage " + (before / 100f).ToString("F2", CultureInfo.InvariantCulture) + " -> "
                         + e.hourlyWage.ToString("F2", CultureInfo.InvariantCulture) + ".");
            return true;
        }

        /// <summary>A wage over WageRules' cap brought back to it as a save loads, satisfaction left as it is. True if it was lowered.</summary>
        public static bool Mend(EmployeeInstance e)
        {
            int formula = FormulaCents(e), before = WageCents(e), after = WageRules.Mended(before, formula);
            if (after >= before)
            {
                return false;
            }
            e.hourlyWage = after / 100f;
            Logger?.Info("Tech: " + NameOf(e) + "'s wage, raised by the mod past " + (WageRules.CapBp / 10000) + " times the game's own for the skill ("
                         + (formula / 100f).ToString("F2", CultureInfo.InvariantCulture)
                         + "), brought back from " + (before / 100f).ToString("F2", CultureInfo.InvariantCulture) + " to "
                         + e.hourlyWage.ToString("F2", CultureInfo.InvariantCulture) + ".");
            return true;
        }

        /// <summary>The game's satisfaction moved by a few points, kept within 1 and 100 (the game's own leaving at 0 is left to the game).</summary>
        public static void Mood(EmployeeInstance e, float change)
        {
            e.satisfaction = Mathf.Clamp(e.satisfaction + change, Mathf.Min(e.satisfaction, 1f), 100f);
        }

        /// <summary>Off sick tomorrow, through the game's own sick day (it calls the business and finds cover as it always does).</summary>
        public static void SickTomorrow(EmployeeInstance e)
        {
            if (Save != null)
            {
                e.nextSickDay = Math.Min(e.nextSickDay, Save.Day + 1);
            }
        }

        /// <summary>The help of a mentor: thousandths of a point on the game's own skill.</summary>
        public static void Mentored(EmployeeInstance e, int thousandths)
        {
            if (thousandths > 0)
            {
                e.IncreaseSkill(e.characterData.skills[0], thousandths / 1000f);
            }
        }

        /// <summary>
        /// The employee leaves of their own accord, as the game has one leave whose complaint went unanswered: once, with
        /// one wage and one notice, off the game's list at once (Core/StaffLeave). A rival's offer to them in the game's
        /// messages loses its buttons at once (Core/RivalOfferGuard). False, with nothing changed, for somebody who is not
        /// the player's to let go now.
        /// </summary>
        public static bool Resign(EmployeeInstance e)
        {
            return StaffLeave.Resign(e, "the tech sector");
        }

        /// <summary>True while the game's own rival offer to the employee is open (Core/RivalOfferGuard): the sector's own offer waits for it, or gives way.</summary>
        public static bool GameOffer(EmployeeInstance e) => RivalOfferGuard.OfferOpen(e);

        /// <summary>
        /// The employee is let go: the game's own firing (MyEmployees: RemoveEmployee, which takes them off the list at once
        /// and pays nothing, as the game's firing does; the day before was paid by the game's night, when the sector's day
        /// lets them go). A rival's offer to them loses its buttons.
        /// </summary>
        public static void Fire(EmployeeInstance e)
        {
            Logger?.Info("Tech: " + NameOf(e) + " is let go (the game's own firing).");
            e.RemoveEmployee();
            RivalOfferGuard.Look("a tech employee let go");
        }

        /// <summary>A deductible cost of the business at its address, cents; wages in the salaries line; <paramref name="force"/> for what is owed whatever the cash.</summary>
        public static bool Cost(int cents, string key, BuildingRegistration at, string name, bool wages, bool force)
        {
            if (cents <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            TransactionInfo info = wages
                ? new TransactionInfo(key, Salaries, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } })
                : new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            return GameManager.ChangeMoneySafe(-cents / 100f, info, null, at != null ? at.Address : null, force);
        }
    }
}
