using System;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Everything the people part reads from or does to the game, in one place.
    ///
    /// READ: the player's employees (SaveGameManager.Current.EmployeeInstances, the candidates and the player left out),
    /// their main skill (the game's skills[0], 0 to 100), whether they are working this hour (EmployeeInstance.IsWorking),
    /// their wage and hours this week, the business they are assigned to and its type; the game's own wage for a skill
    /// (EmployeeHelper.CalculateHourlyWageForSkill).
    ///
    /// DONE, each the game's own way: a rise is a higher EmployeeInstance.hourlyWage (never a lower one); morale moves the
    /// game's satisfaction a few points a day; a burnt-out employee is off sick through the game's own next sick day; one
    /// who resigns leaves as the game has an employee leave whose complaint went unanswered; a lay-off or a dismissal is
    /// the game's own firing (RemoveEmployee); a mentor's help is the game's IncreaseSkill. Money is the mod's own dated
    /// transactions (GameManager.ChangeMoneySafe), at the business, the cost lines marked deductible as the game marks its
    /// own. Nothing here runs by itself.
    /// </summary>
    internal static class TalentGame
    {
        public const string WagesKey = "reallife_transaction_tech_wages";
        public const string SeveranceKey = "reallife_transaction_tech_severance_wage";
        public const string TrainingKey = "reallife_transaction_tech_training";
        public const string ClauseKey = "reallife_transaction_tech_bonus_clause";
        private const string DeductKey = "reallife_tech_deduct";
        private const string Salaries = "ba:transactioncategory_salaryexpenses";
        private const string Office = "ba:buildingtype_office";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        // ---------------------------------------------------------------- who is a professional

        /// <summary>
        /// True for an employee of the player's whose main skill is a professional one: a programmer, a graphic designer,
        /// a lawyer, an HR manager or a headhunter anywhere, and customer service only at an office (a shop's till is
        /// not a professional's job and keeps the game's own hiring).
        /// </summary>
        public static bool Professional(EmployeeInstance e)
        {
            try
            {
                if (e == null || e.IsCandidate || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0 || e.satisfaction <= 0f)
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

        /// <summary>
        /// The player's professionals. One pass over the save's employees. In a Going Public session one assigned to
        /// another player's business is theirs, and is left out: nothing of the mod's counts, reads or changes them.
        /// </summary>
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

        public static EmployeeInstance Employee(string id)
        {
            try
            {
                return string.IsNullOrEmpty(id) ? null : EmployeeHelper.GetEmployeeById(id, false);
            }
            catch (Exception)
            {
                return null;
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

        /// <summary>
        /// The hours the game has counted the employee working today (EmployeeInstance.workedHoursToday, added on its hour
        /// after the mod's own count and emptied at midnight after the mod's first hour of the new day: there it is still
        /// the whole of the day before).
        /// </summary>
        public static int WorkedToday(EmployeeInstance e) => e != null ? Math.Max(0, e.workedHoursToday) : 0;

        /// <summary>True while a business is closed by the game's own switch (temporarily closed): nobody works there then.</summary>
        public static bool Shut(BuildingRegistration reg) => reg != null && reg.temporarilyClosed;

        public static int WeekHours(EmployeeInstance e) => Math.Max(0, e.workedHoursThisWeek);

        public static bool Absent(EmployeeInstance e) => e.isAbsent;

        public static int WageCents(EmployeeInstance e) => Mathf.RoundToInt(e.hourlyWage * 100f);

        /// <summary>The weekly hours the game has the employee down for (40 if none are set).</summary>
        public static int ContractHours(EmployeeInstance e) => e.assignedWeeklyHours > 0 ? e.assignedWeeklyHours : TalentCatalogue.WeekHours;

        // ---------------------------------------------------------------- places

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

        /// <summary>A business by its address, written as the company writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return reg == null ? string.Empty : Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>One of the player's buildings by its place key; null if it is no longer theirs.</summary>
        public static BuildingRegistration Find(string place)
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && !string.IsNullOrEmpty(place) && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && Place(reg) == place)
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
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName) && reg.businessTypeName != "ba:businesstype_empty")
                {
                    into.Add(reg);
                }
            }
        }

        // ---------------------------------------------------------------- the game's wage

        /// <summary>The game's own wage for a skill at a value, cents an hour.</summary>
        public static int GameRate(string skill, int points)
        {
            try
            {
                return Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(string.IsNullOrEmpty(skill) ? TalentCatalogue.RateSkill : skill, Mathf.Clamp(points, 0, 100)) * 100f);
            }
            catch (Exception)
            {
                return TalentCatalogue.RateLeast;
            }
        }

        // ---------------------------------------------------------------- doing

        /// <summary>A rise in basis points on the game's wage. Never lowers it.</summary>
        public static void Rise(EmployeeInstance e, int bp)
        {
            float before = e.hourlyWage;
            e.hourlyWage = Mathf.Max(before, before * (10000 + Mathf.Max(0, bp)) / 10000f);
            Logger?.Info("Tech: " + NameOf(e) + "'s wage " + before.ToString("F2") + " -> " + e.hourlyWage.ToString("F2") + ".");
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

        /// <summary>The employee leaves of their own accord, as the game has one leave whose complaint went unanswered.</summary>
        public static void Resign(EmployeeInstance e)
        {
            Logger?.Info("Tech: " + NameOf(e) + " leaves (the game's own resignation).");
            e.poached = false;
            e.complaintData.isComplaining = false;
            e.satisfaction = 0f;
            if (!e.ReplaceAutomaticallyOnResign())
            {
                e.Resign();
            }
        }

        /// <summary>The employee is let go: the game's own firing.</summary>
        public static void Fire(EmployeeInstance e)
        {
            Logger?.Info("Tech: " + NameOf(e) + " is let go (the game's own firing).");
            e.RemoveEmployee();
        }

        // ---------------------------------------------------------------- money

        /// <summary>
        /// A cost of the business, cents, as a dated transaction of the mod's own at its address; wages in the game's
        /// salaries line. Deductible. Forced: a wage or severance is owed whatever the cash.
        /// </summary>
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

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;
    }
}
