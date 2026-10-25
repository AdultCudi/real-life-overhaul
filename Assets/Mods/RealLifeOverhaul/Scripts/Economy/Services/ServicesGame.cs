using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the services ask of the rest of the mod, set by Wiring/ServicesLinks; each is optional, with the default named on it.</summary>
    public static class ServicesBridges
    {
        /// <summary>What buying every investor out of a business costs today (Ventures' own price). Default: nothing.</summary>
        public static Func<BuildingRegistration, int> InvestorsOwed;

        /// <summary>Buys every investor out through Ventures' own buy-out; false if one could not be paid. Default: true.</summary>
        public static Func<BuildingRegistration, bool> PayInvestors;

        /// <summary>What is owed on business loans secured on a business (the credit side's marks). Default: nothing.</summary>
        public static Func<Address, int> LoansOwed;

        /// <summary>Pays those loans off as the game's own pay-off does; false if one could not be paid. Default: true.</summary>
        public static Func<Address, bool> SettleLoans;

        /// <summary>Tells "running a business" that a business, by its place, is no longer the player's. Default: nobody.</summary>
        public static Action<string> BusinessGone;

        /// <summary>A business sold, by its place, and its whole price, for the player's listed company (Markets/Listing). Default: nobody.</summary>
        public static Action<string, int> Sold;

        public static void Reset()
        {
            InvestorsOwed = null;
            PayInvestors = null;
            LoansOwed = null;
            SettleLoans = null;
            BusinessGone = null;
            Sold = null;
        }
    }

    /// <summary>One cleaner of the crew as the game has them now.</summary>
    public sealed class CrewMember
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string BaseName = string.Empty;
        public EmployeeInstance Employee;
        public BuildingRegistration Base;    // the building they are assigned to; null if none
        public int X, Z;                     // its door
        public int Skill, Mood;              // 0 to 100 each
        public int Wage;                     // hundredths an hour
        public int WeekHours;                // the hours a week the game has them down for
        public int TodayFrom = -1, TodayTo;  // their cleaning shifts today, first to last minute; -1: none
        public bool Present;                 // not off sick today (or replaced)
    }

    /// <summary>
    /// What the cleaning service reads from and does to the game: dirt on the game's own tiles by the game's own sum, and
    /// money as the mod's own transactions; never a wage, a schedule or an employee. Nothing runs per frame.
    /// </summary>
    internal static partial class ServicesGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { DeductKey.GetLocalization() };

        public const string VanKey = "reallife_transaction_svc_van";
        public const string FeeKey = "reallife_transaction_svc_fee";
        public const string PenaltyKey = "reallife_transaction_svc_penalty";
        public const string SaleKey = "reallife_transaction_svc_sale";
        public const string PartKey = "reallife_transaction_svc_part";
        public const string ClauseKey = "reallife_transaction_svc_clause";
        private const string DeductKey = "reallife_svc_deduct_van";
        private const string EmptyType = "ba:businesstype_empty";
        private const int MinutesMost = 120;   // minutes of one visit cleaned at one hour event: this hour's, and the last hour's of a visit planned during it

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        /// <summary>The minute of the day by the game's clock.</summary>
        public static int Now => Save != null ? Save.Hour * 60 + Mathf.Clamp(Mathf.FloorToInt(Save.Minute), 0, 59) : 0;

        /// <summary>The price index in tenths of a point, 1000 at the start.</summary>
        public static int Index => Prices.Level;

        /// <summary>The economy as a business's value feels it: 1 a boom, 0 steady, -1 a slowdown or a recession.</summary>
        public static int Phase
        {
            get
            {
                CycleState state = CityEconomy.State;
                return state == CycleState.Boom ? 1 : state == CycleState.Steady ? 0 : -1;
            }
        }

        /// <summary>A building by its address, written as business pressure and "running a business" write a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The building at a place; null if the place is no address or the city has no such building.</summary>
        public static BuildingRegistration Find(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            if (at <= 0 || Save == null
                || !int.TryParse(place.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }
            try
            {
                var address = new Address(Uri.UnescapeDataString(place.Substring(0, at)), number);
                return BuildingHelper.GetBuilding(address) != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Name(BuildingRegistration reg)
        {
            try
            {
                return reg == null ? "?" : reg.GetDisplayName();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>A building's door in whole metres; false while the city is not there to ask.</summary>
        public static bool Door(BuildingRegistration reg, out int x, out int z)
        {
            x = z = 0;
            try
            {
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null && reg != null && reg.Address != null ? city.FindCityBuildingController(reg.Address) : null;
                if (controller == null || controller.entranceDoors == null || controller.entranceDoors.Length == 0 || controller.entranceDoors[0] == null
                    || controller.entranceDoors[0].doorTransform == null)
                {
                    return false;
                }
                Vector3 door = controller.entranceDoors[0].doorTransform.position;
                x = Mathf.RoundToInt(door.x);
                z = Mathf.RoundToInt(door.z);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True for a building of the player's that the game cleans (BuildingCleanlinessHelper.RunHourly) and that has a floor.</summary>
        public static bool Cleaned(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasValidAddress || reg.dirtSpots == null || reg.dirtSpots.Count == 0)
                {
                    return false;
                }
                Building building = reg.BuildingCached;
                return building != null && BuildingTypeHelper.GetData(building).NeedsCleaning;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static int Cleanliness(BuildingRegistration reg)
        {
            try
            {
                return reg != null ? Mathf.RoundToInt(reg.GetCleanliness()) : 100;
            }
            catch (Exception)
            {
                return 100;
            }
        }

        /// <summary>A kind of business by the name the game gives it (its type's id is also its locale key).</summary>
        public static string TypeName(string type)
        {
            return string.IsNullOrEmpty(type) ? string.Empty : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : type;
        }

        /// <summary>The player's buildings the service could look after, in the save's own order.</summary>
        public static void Shops(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (Cleaned(all[i]) && all[i].HasEstablishedBusiness)
                {
                    into.Add(all[i]);
                }
            }
        }

        /// <summary>A client as it stands now: a shop's worst tile, cleanliness, force and whether the game cleans it today; a contract's whether it is still there.</summary>
        public static void Read(CleaningClient c)
        {
            BuildingRegistration reg = Find(c.Place);
            if (c.Outside)
            {
                c.Gone = reg == null || reg.RentedByPlayer || !reg.HasEstablishedBusiness || reg.businessTypeName == EmptyType;
                c.Force = CleaningRules.Force(c.Sqm);
                c.Open = !c.Gone;
                c.Clean = CleaningRules.Even(c.Dirt);
                c.Worst = c.Dirt;
                return;
            }
            c.Gone = !Cleaned(reg);
            if (c.Gone)
            {
                c.Open = false;
                return;
            }
            c.Name = Name(reg);
            c.District = reg.Neighborhood ?? string.Empty;
            if (Door(reg, out int x, out int z))
            {
                c.X = x;
                c.Z = z;
            }
            c.Force = CleaningRules.Force(BuildingHelper.GetBuildingSquareMeters(reg.Address));
            float worst = 0f;
            List<DirtSpot> spots = reg.dirtSpots;
            for (int i = 0; i < spots.Count; i++)
            {
                worst = Mathf.Max(worst, spots[i].dirtiness);
            }
            c.Worst = Mathf.RoundToInt(worst * 100f);
            c.Clean = Mathf.RoundToInt(reg.GetCleanliness());
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            c.Open = !reg.temporarilyClosed && today != null && today.isOpen;   // what the game's hourly cleaning asks of a building and its day
        }

        /// <summary>A visit's minutes of cleaning at a shop, by the game's sum; returns hundredths taken off the worst tile, 0 if the game would not clean it now.</summary>
        public static int Clean(CleaningClient c, EmployeeInstance e, int minutes)
        {
            BuildingRegistration reg = Find(c.Place);
            if (!Cleaned(reg) || reg.temporarilyClosed || e == null || minutes <= 0)
            {
                return 0;
            }
            float off = Points(e, reg) * Mathf.Min(MinutesMost, minutes) / 60f;
            if (off <= 0f)
            {
                return 0;
            }
            BuildingCleanliness floor = reg.Cleanliness;
            List<DirtSpot> spots = reg.dirtSpots;
            float worst = 0f;
            for (int i = 0; i < spots.Count; i++)
            {
                worst = Mathf.Max(worst, spots[i].dirtiness);
                floor.SetDirtiness(i, Mathf.Max(0f, spots[i].dirtiness - off));
            }
            Shown(reg);
            return CleaningRules.Taken(Mathf.RoundToInt(worst * 100f), Mathf.RoundToInt(off * 100f));
        }

        /// <summary>True if a crew member is at work and on a cleaning shift at their base in an hour of today (the game's IsWorking asks of the hour now).</summary>
        public static bool Working(CrewMember member, int hour)
        {
            EmployeeInstance e = member.Employee;
            return e != null && member.Base != null && e.IsEmployeeAvailable() && OnCleaningShift(member.Base, e.id, hour);
        }

        /// <summary>
        /// Puts back on a cleaner's base the minutes of the starting hour they were out, since the game's hourly cleaning
        /// after this event counts them there a whole hour. Only where the game will; returns hundredths put on a tile.
        /// </summary>
        public static int Hold(CrewMember member, int awayMinutes)
        {
            BuildingRegistration reg = member.Base;
            EmployeeInstance e = member.Employee;
            if (awayMinutes <= 0 || e == null || !Cleaned(reg) || reg.temporarilyClosed || !e.IsEmployeeAvailable() || !e.HasSkill(ServicesCatalogue.CleaningSkill)
                || !OnCleaningShift(reg, e.id, Save.Hour))
            {
                return 0;
            }
            float add = Points(e, reg) * Mathf.Min(60, awayMinutes) / 60f;
            if (add <= 0f)
            {
                return 0;
            }
            BuildingCleanliness floor = reg.Cleanliness;
            List<DirtSpot> spots = reg.dirtSpots;
            for (int i = 0; i < spots.Count; i++)
            {
                floor.SetDirtiness(i, Mathf.Min(BuildingCleanliness.MaxDirtiness, spots[i].dirtiness + add));
            }
            return Mathf.RoundToInt(add * 100f);
        }

        // What one cleaner takes off every tile of a building in an hour, in the game's points (BuildingCleanliness.SimulateCleaning).
        private static float Points(EmployeeInstance e, BuildingRegistration reg)
        {
            float skill = e.GetSkillValue(ServicesCatalogue.CleaningSkill) * (e.satisfaction / 100f);
            return skill / CleaningRules.Force(BuildingHelper.GetBuildingSquareMeters(reg.Address));
        }

        // True if today's schedule, on an open day, has this employee on a cleaning shift in this hour.
        private static bool OnCleaningShift(BuildingRegistration reg, string id, int hour)
        {
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            List<WorkShift> shifts = today != null && today.isOpen ? today.workShifts : null;
            for (int i = 0; shifts != null && i < shifts.Count; i++)
            {
                WorkShift shift = shifts[i];
                if (shift != null && shift.type == WorkShiftType.Cleaning && shift.startingHour <= hour && shift.endingHour > hour && shift.employeeId == id)
                {
                    return true;
                }
            }
            return false;
        }

        // The floor of the building the player stands in is drawn again, as after the game's own cleaning.
        private static void Shown(BuildingRegistration reg)
        {
            if (BuildingManager.IsInsideBuilding && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.buildingRegistration, reg))
            {
                InstanceBehavior<BuildingManager>.Instance.UpdateDirtinessInCurrentBuilding();
            }
        }

        /// <summary>Everybody the player employs with the cleaning skill, in the game's order; candidates and the player left out.</summary>
        public static void Cleaners(List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            EmployeeInstance self = Self();
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (Counts(e) && !ReferenceEquals(e, self))
                {
                    into.Add(e);
                }
            }
        }

        private static bool Counts(EmployeeInstance e)
        {
            return e != null && !string.IsNullOrEmpty(e.id) && !e.IsCandidate && !e.isBeingReplaced && e.characterData != null && e.characterData.skills != null
                   && e.HasSkill(ServicesCatalogue.CleaningSkill);
        }

        private static EmployeeInstance Self()
        {
            try
            {
                return PlayerHelper.GetPlayerEmployeeInstance();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The crew as the game has them now; anyone who has left, or is no longer a cleaner, is taken off the crew.</summary>
        public static void ReadCrew(ServicesState state, List<CrewMember> into, List<CrewMember> pool, out int dropped)
        {
            dropped = 0;
            for (int i = 0; i < into.Count; i++)
            {
                into[i].Employee = null;
                into[i].Base = null;
                pool.Add(into[i]);
            }
            into.Clear();
            for (int i = state.Crew.Count - 1; i >= 0; i--)
            {
                EmployeeInstance e = EmployeeHelper.GetEmployeeById(state.Crew[i], false);
                if (!Counts(e))
                {
                    state.Crew.RemoveAt(i);
                    dropped++;
                }
            }
            for (int i = 0; i < state.Crew.Count; i++)
            {
                CrewMember m;
                if (pool.Count > 0)
                {
                    m = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                else
                {
                    m = new CrewMember();
                }
                Fill(m, EmployeeHelper.GetEmployeeById(state.Crew[i], false));
                into.Add(m);
            }
        }

        /// <summary>One cleaner's figures, whether or not they are on the crew.</summary>
        public static void Fill(CrewMember m, EmployeeInstance e)
        {
            m.Employee = e;
            m.Id = e.id;
            m.Name = e.characterData.name ?? "?";
            m.Skill = Mathf.RoundToInt(Mathf.Clamp(e.GetSkillValue(ServicesCatalogue.CleaningSkill), 0f, 100f));
            m.Mood = Mathf.RoundToInt(Mathf.Clamp(e.satisfaction, 0f, 100f));
            m.Wage = Mathf.RoundToInt(e.hourlyWage * 100f);
            m.WeekHours = Mathf.Max(0, e.assignedWeeklyHours);
            m.Present = e.IsEmployeeAvailable();
            m.Base = e.IsAssignedToAnyBusiness() ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            m.BaseName = m.Base != null ? Name(m.Base) : string.Empty;
            m.TodayFrom = -1;
            m.TodayTo = 0;
            if (m.Base != null && Door(m.Base, out int x, out int z))
            {
                m.X = x;
                m.Z = z;
            }
            ScheduleDay today = m.Base != null ? BuildingHelper.GetTodaySchedule(m.Base) : null;
            List<WorkShift> shifts = today != null && today.isOpen ? today.workShifts : null;
            for (int i = 0; shifts != null && i < shifts.Count; i++)
            {
                WorkShift shift = shifts[i];
                if (shift != null && shift.type == WorkShiftType.Cleaning && shift.employeeId == e.id && shift.endingHour > shift.startingHour)
                {
                    m.TodayFrom = m.TodayFrom < 0 ? shift.startingHour * 60 : Mathf.Min(m.TodayFrom, shift.startingHour * 60);
                    m.TodayTo = Mathf.Max(m.TodayTo, shift.endingHour * 60);
                }
            }
        }

        /// <summary>Today's cleaning shifts of the crew who are at work, one entry each, at the building they are assigned to.</summary>
        public static void Shifts(List<CrewMember> crew, List<RoundCleaner> into)
        {
            into.Clear();
            for (int m = 0; m < crew.Count; m++)
            {
                CrewMember member = crew[m];
                ScheduleDay today = member.Base != null && member.Present ? BuildingHelper.GetTodaySchedule(member.Base) : null;
                List<WorkShift> shifts = today != null && today.isOpen ? today.workShifts : null;
                for (int i = 0; shifts != null && i < shifts.Count; i++)
                {
                    WorkShift shift = shifts[i];
                    if (shift != null && shift.type == WorkShiftType.Cleaning && shift.employeeId == member.Id && shift.endingHour > shift.startingHour)
                    {
                        into.Add(new RoundCleaner
                        {
                            Id = member.Id, Name = member.Name, From = shift.startingHour * 60, To = shift.endingHour * 60, X = member.X, Z = member.Z, Skill = member.Skill, Mood = member.Mood
                        });
                    }
                }
            }
        }

        /// <summary>The hourly wage the game would offer a cleaner of a skill, in hundredths: what a shop's own cleaner is priced at.</summary>
        public static int GoingWage(int skill)
        {
            try
            {
                return Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(ServicesCatalogue.CleaningSkill, Mathf.Clamp(skill, 1, 100)) * 100f);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The cleaning hours a week, with somebody on them, in a shop's own schedule.</summary>
        public static int OwnCleaningHours(BuildingRegistration reg)
        {
            int hours = 0;
            List<ScheduleDay> week = reg != null ? reg.scheduleDays : null;
            for (int d = 0; week != null && d < week.Count; d++)
            {
                ScheduleDay day = week[d];
                for (int i = 0; day != null && day.isOpen && day.workShifts != null && i < day.workShifts.Count; i++)
                {
                    WorkShift shift = day.workShifts[i];
                    hours += shift != null && shift.type == WorkShiftType.Cleaning && !string.IsNullOrEmpty(shift.employeeId) ? Mathf.Max(0, shift.endingHour - shift.startingHour) : 0;
                }
            }
            return hours;
        }

        /// <summary>Businesses that could want a contract: not the player's, no special service, of a type the game cleans. One pass over the buildings.</summary>
        public static void Candidates(List<ContractCandidate> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                try
                {
                    if (reg == null || reg.RentedByPlayer || !reg.HasValidAddress || !reg.HasEstablishedBusiness || reg.businessTypeName == EmptyType || GameDlc.Gated(reg))
                    {
                        continue;
                    }
                    Building building = reg.BuildingCached;
                    if (building == null || building.SpecialService != null || !BuildingTypeHelper.GetData(building).NeedsCleaning)
                    {
                        continue;
                    }
                    if (!Door(reg, out int x, out int z))
                    {
                        continue;
                    }
                    into.Add(new ContractCandidate
                    {
                        Place = Place(reg), Name = ServicesStore.Clean(Name(reg)), District = building.Neighbourhood ?? string.Empty, Sqm = BuildingHelper.GetBuildingSquareMeters(reg.Address),
                        Capacity = Mathf.Max(0, reg.customerCapacity), X = x, Z = z, Income = Mathf.RoundToInt(CityEconomy.DistrictIncome(building.Neighbourhood))
                    });
                }
                catch (Exception)
                {
                    // a building that cannot be read is no candidate
                }
            }
        }

        /// <summary>Charges the mod's own transaction, 'deductible' as a business cost; without 'force' the game refuses what the cash does not cover.</summary>
        public static bool Take(int amount, string key, string name, Address address, int? day, bool deductible, bool force)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            TransactionInfo info = Info(key, name);
            if (deductible)
            {
                info.SetTaxDeductibleName(DeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(-amount, info, day, address, force);
        }

        public static void Give(int amount, string key, string name, Address address, int? day)
        {
            if (amount > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(key, name), day, address, true);
            }
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }
    }
}
