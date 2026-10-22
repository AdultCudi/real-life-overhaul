using System;
using System.Collections.Generic;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The part of TextsGame for the city's new talk: the game's employees, their threads, a business's figures.</summary>
    public static partial class TextsGame
    {
        private static readonly Dictionary<string, EmployeeInstance> Employees = new Dictionary<string, EmployeeInstance>(64, StringComparer.Ordinal);

        /// <summary>Indexes the game's employees by id for one look over the staff: a pass over the save's list.</summary>
        public static void IndexEmployees()
        {
            Employees.Clear();
            List<EmployeeInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.EmployeeInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id != null)
                {
                    Employees[all[i].id] = all[i];
                }
            }
        }

        /// <summary>Reads the game's own facts of one of the staff, after <see cref="IndexEmployees"/>; false for no such employee.</summary>
        public static bool ReadEmployee(TextStaff s)
        {
            if (s == null || s.Id == null || !Employees.TryGetValue(s.Id, out EmployeeInstance e))
            {
                return false;
            }
            s.Sick = e.isAbsent;
            s.WeekHours = e.assignedWeeklyHours;
            s.MostHours = MostHours(e);
            s.Complaining = e.complaintData != null && e.complaintData.isComplaining;
            s.Job = TextWords.StaffJob(e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 ? e.characterData.skills[0].name : null);
            return true;
        }

        // The most hours a week an employee agreed to, by their demand of the game's; 0 with none.
        private static int MostHours(EmployeeInstance e)
        {
            for (int i = 0; e.demands != null && i < e.demands.Count; i++)
            {
                if (JobDemandHelper.GetByName(e.demands[i]) is HoursWorkingPerWeek hours)
                {
                    return hours.MaxHours;
                }
            }
            return 0;
        }

        /// <summary>Sends a message from one of the player's staff into the game's own thread with them; the game adds their name, business and skill.</summary>
        public static bool EmployeeSay(string id, string key, Dictionary<string, string> data)
        {
            if (id == null || key == null)
            {
                return false;
            }
            if (!Employees.TryGetValue(id, out EmployeeInstance e))
            {
                IndexEmployees();
                if (!Employees.TryGetValue(id, out e))
                {
                    return false;   // gone since: nothing is sent
                }
            }
            e.SendMessage(key, data != null ? new Dictionary<string, string>(data) : null);   // a copy: the game keeps a message's values while it is listed
            return true;
        }

        /// <summary>A business's review figures, 0 to 100, and yesterday's crowd (1 busy, -1 quiet); false with too few days of figures.</summary>
        public static bool Figures(BuildingRegistration reg, out int service, out int pricing, out int cleanliness, out int interior, out int crowd)
        {
            service = pricing = cleanliness = interior = crowd = 0;
            if (reg == null || reg.satisfaction == null || reg.orderHistory == null || SaveGameManager.Current == null)
            {
                return false;
            }
            int today = SaveGameManager.Current.Day, yesterday = -1, sum = 0, days = 0;
            bool any = false;
            for (int i = 0; i < reg.orderHistory.Count; i++)
            {
                OrderHistoryEntry h = reg.orderHistory[i];
                if (h == null || h.dayNumber >= today || h.totalCustomers <= 0)
                {
                    continue;
                }
                any = true;
                if (h.dayNumber == today - 1)
                {
                    yesterday = h.totalCustomers;
                }
                else if (today - h.dayNumber <= 8)
                {
                    sum += h.totalCustomers;
                    days++;
                }
            }
            if (!any)
            {
                return false;   // the game's own page says "not enough data" then
            }
            service = reg.satisfaction.customerService;
            pricing = reg.satisfaction.pricing;
            cleanliness = reg.satisfaction.cleanliness;
            interior = reg.satisfaction.facility;
            if (yesterday > 0 && days > 0)
            {
                int week = sum / days;
                crowd = yesterday * 4 >= week * 5 ? 1 : yesterday * 4 <= week * 3 ? -1 : 0;
            }
            return true;
        }
    }
}
