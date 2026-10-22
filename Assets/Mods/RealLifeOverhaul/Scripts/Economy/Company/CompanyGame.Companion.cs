using System;
using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    internal static partial class CompanyGame
    {
        /// <summary>
        /// Ordinary hourly wages at capture time: base, city index, and an agreed company raise. No cached staff wage
        /// is read, and no daily pass runs. Overtime, sick pay and other fees are outside this capability.
        /// </summary>
        public static CompanionModule CompanionEmployment(CompanyDirector company, int index)
        {
            var rows = new List<object>();
            CompanyState state = company != null && company.On(CompanyPart.Staff) ? company.State : null;
            var staffById = new Dictionary<string, StaffFile>(StringComparer.Ordinal);
            if (state != null)
            {
                foreach (StaffFile file in state.Staff)
                {
                    if (!string.IsNullOrEmpty(file.Id) && !staffById.ContainsKey(file.Id))
                    {
                        staffById.Add(file.Id, file);
                    }
                }
            }
            EmployeeInstance self = Self();
            List<EmployeeInstance> all = SaveGameManager.Current.EmployeeInstances;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance employee = all[i];
                if (employee == null || ReferenceEquals(employee, self) || employee.IsCandidate || employee.isBeingReplaced
                    || employee.hourlyWage <= 0f || employee.satisfaction <= 0f || string.IsNullOrEmpty(employee.id))
                {
                    continue;
                }
                int baseCents = Mathf.RoundToInt(employee.hourlyWage * 100f);
                staffById.TryGetValue(employee.id, out StaffFile staff);
                int raiseCents = staff != null ? staff.Raise : 0;
                rows.Add(CompanyCompanion.Wage(employee.id, baseCents, index, raiseCents));
            }
            return new CompanionModule("ready", rows);
        }
    }
}
