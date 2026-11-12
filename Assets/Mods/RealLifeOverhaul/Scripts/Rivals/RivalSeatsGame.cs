using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared;
using Helpers;
using RealLifeOverhaul.Economy;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The game's rival businesses as the tycoons' outlets stand on them (<see cref="IRivalSeats"/>): the rival's registration of the kind in the district, with its crew
    /// (BuildingRegistration.aiEmployees: the game's own id, name, main skill, the wage the game pays for that skill and the hours it expects) and its rent a day. Read on
    /// the rivals' day, an outlet at a time; reads only, nothing of the game's is changed.
    /// </summary>
    internal sealed class RivalSeatsGame : IRivalSeats
    {
        public static readonly RivalSeatsGame Instance = new RivalSeatsGame();

        private static readonly List<BuildingRegistration> Candidates = new List<BuildingRegistration>(16);

        public RivalSeat Find(string district, string type, IList<string> taken, int pick)
        {
            Candidates.Clear();
            try
            {
                List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    BuildingRegistration r = all[i];
                    if (r != null && CompanyGame.RivalShopUsable(r) && string.Equals(r.businessTypeName, type, StringComparison.Ordinal) && r.aiEmployees != null && r.aiEmployees.Count > 0
                        && string.Equals(GameRegistrations.District(r), district, StringComparison.Ordinal) && !taken.Contains(Key(r)))
                    {
                        Candidates.Add(r);
                    }
                }
                if (Candidates.Count == 0)
                {
                    return null;
                }
                Candidates.Sort((a, b) => string.CompareOrdinal(Key(a), Key(b)));   // (the save's order of registrations is nobody's rule)
                return Seat(Candidates[Math.Min(Candidates.Count - 1, Math.Max(0, pick) * Candidates.Count / 1000)]);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Candidates.Clear();
            }
        }

        public RivalSeat Read(string key)
        {
            try
            {
                int at = key.LastIndexOf('#');
                int number;
                if (at <= 0 || !int.TryParse(key.Substring(at + 1), out number))
                {
                    return null;
                }
                BuildingRegistration r = BuildingHelper.GetBuildingRegistration(new Address(key.Substring(0, at), number));
                return r != null && CompanyGame.RivalShopUsable(r) && r.aiEmployees != null ? Seat(r) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Key(BuildingRegistration r) => (r.StreetName ?? string.Empty) + "#" + r.StreetNumber;

        private static RivalSeat Seat(BuildingRegistration r)
        {
            var seat = new RivalSeat { Key = Key(r), Name = r.BusinessName ?? string.Empty, Rent = Mathf.Max(0, Mathf.RoundToInt(r.RentPerDay)) };
            for (int i = 0; i < r.aiEmployees.Count && seat.Hands.Count < RivalFirms.CrewMost; i++)
            {
                AiBusinessEmployeeData e = r.aiEmployees[i];
                if (e == null || e.isPoached || string.IsNullOrEmpty(e.id))
                {
                    continue;
                }
                string name;
                int hours;
                try
                {
                    name = e.GetFullName();
                    hours = e.GetExpectedHoursPerWeek();
                }
                catch (Exception)
                {
                    name = e.id;
                    hours = 40;
                }
                seat.Hands.Add(new RivalHand
                {
                    Id = e.id, Name = name ?? e.id, Skill = Mathf.RoundToInt(e.primarySkillValue), WeekHours = hours,
                    Hourly = PaidWage.Base(EmployeeHelper.CalculateHourlyWageForSkill(e.primarySkillName, e.primarySkillValue))
                });
            }
            return seat;
        }
    }
}
