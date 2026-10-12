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
    /// <summary>
    /// What the cleaning service and selling a business ask of the rest of the mod. Each is optional and has a default,
    /// so Economy/Services works alone; Scripts/Wiring/ServicesLinks.cs fills them in. It never calls another feature
    /// of the mod directly, with one exception that is a reading and no more: the investors' valuation
    /// (Ventures' own figures of a business), which the sale reuses so that a business has one value in the mod.
    /// </summary>
    public static class ServicesBridges
    {
        /// <summary>What buying every investor out of a business costs today (Ventures' own price). Default: nothing, nobody has a share.</summary>
        public static Func<BuildingRegistration, int> InvestorsOwed;

        /// <summary>Buys every investor out of a business through Ventures' own buy-out. False if one could not be paid. Default: true, there is none.</summary>
        public static Func<BuildingRegistration, bool> PayInvestors;

        /// <summary>What is owed on business loans secured on a business (the credit side's own marks). Default: nothing.</summary>
        public static Func<Address, int> LoansOwed;

        /// <summary>Pays those loans off as the game's own pay-off does. False if one could not be paid. Default: true, there is none.</summary>
        public static Func<Address, bool> SettleLoans;

        /// <summary>Tells "running a business" that a business is no longer the player's, by its place. Default: nobody to tell.</summary>
        public static Action<string> BusinessGone;

        public static void Reset()
        {
            InvestorsOwed = null;
            PayInvestors = null;
            LoansOwed = null;
            SettleLoans = null;
            BusinessGone = null;
        }
    }

    /// <summary>One cleaner of the crew as the game has them now.</summary>
    public sealed class CrewMember
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string BaseName = string.Empty;
        public EmployeeInstance Employee;
        public BuildingRegistration Base;    // the building they are assigned to; null if to none
        public int X, Z;                     // its door
        public int Skill, Mood;              // 0 to 100 each
        public int Wage;                     // an hour, hundredths
        public int WeekHours;                // the hours a week the game has them down for
        public int TodayFrom = -1, TodayTo;  // their cleaning shifts today, first to last minute; -1: none
        public bool Present;                 // not off sick today (or replaced)
    }

    /// <summary>
    /// Everything the cleaning service reads from or does to the game, in one place (the sale's part is in
    /// ServicesGame.Sale.cs, a visiting cleaner seen in a shop in CleaningPresence.cs).
    ///
    /// READ: the player's buildings the game cleans (BuildingRegistration: dirtSpots, GetCleanliness, the day's
    /// schedule), each one's door (CityBuildingController.entranceDoors) and size; the crew (EmployeeInstance: skill,
    /// satisfaction, wage, absence, the cleaning shifts at the building they are assigned to); businesses that are
    /// not the player's, for contracts.
    ///
    /// DONE: dirt on the game's own tiles, through the game's own setter (BuildingCleanliness.SetDirtiness), by the
    /// game's own arithmetic: taken off at a shop that is visited, and at the visiting cleaner's base put on for the
    /// minutes they were out, just before the game takes a whole hour of their work off it. Money as dated
    /// transactions of the mod's own. No wage, no schedule, no employee and no satisfaction is written.
    ///
    /// Nothing here runs per frame: on the game's hour, once a game day, and when the page is opened.
    /// </summary>
    internal static partial class ServicesGame
    {
        public const string VanKey = "reallife_transaction_svc_van";
        public const string FeeKey = "reallife_transaction_svc_fee";
        public const string PenaltyKey = "reallife_transaction_svc_penalty";
        public const string SaleKey = "reallife_transaction_svc_sale";
        public const string PartKey = "reallife_transaction_svc_part";
        public const string ClauseKey = "reallife_transaction_svc_clause";
        private const string DeductKey = "reallife_svc_deduct_van";
        private const string EmptyType = "ba:businesstype_empty";
        private const int MinutesMost = 120;   // the most minutes of one visit cleaned at one hour event: this hour's, and the last hour's of a visit planned in the middle of it

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        /// <summary>The minute of the day, by the game's clock.</summary>
        public static int Now => Save != null ? Save.Hour * 60 + Mathf.Clamp(Mathf.FloorToInt(Save.Minute), 0, 59) : 0;

        /// <summary>The price index as these rules count it: tenths of a point, 1000 at the start (the city's economy's own figure; 1000 while that is off).</summary>
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

        // ---------------------------------------------------------------- places

        /// <summary>A building by its address, written as business pressure and "running a business" write a place, so that a place means the same in all three.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The building at a place; null if the place is no address or the city has no such building.</summary>
        public static BuildingRegistration Find(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            int number;
            if (at <= 0 || Save == null || !int.TryParse(place.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number))
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

        /// <summary>A building's door in whole metres; false while the city is not there to ask (the last door known is kept).</summary>
        public static bool Door(BuildingRegistration reg, out int x, out int z)
        {
            x = z = 0;
            try
            {
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null && reg != null && reg.Address != null ? city.FindCityBuildingController(reg.Address) : null;
                if (controller == null || controller.entranceDoors == null || controller.entranceDoors.Length == 0 || controller.entranceDoors[0] == null || controller.entranceDoors[0].doorTransform == null)
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

        // ---------------------------------------------------------------- the player's shops

        /// <summary>True for a building of the player's that the game cleans (BuildingCleanlinessHelper.RunHourly: rented by the player, of a type that needs cleaning) and that has a floor to clean.</summary>
        public static bool Cleaned(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !reg.RentedByPlayer || !reg.HasValidAddress || reg.dirtSpots == null || reg.dirtSpots.Count == 0)
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

        /// <summary>A building's cleanliness by the game's own figure (BuildingCleanliness.GetCleanliness), 0 to 100.</summary>
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

        /// <summary>
        /// A client as it stands now. A shop of the player's: its dirtiest tile, the game's own cleanliness figure,
        /// the cleaners its floor needs and whether the game would clean it today. A client under contract: whether
        /// the business is still there. One walk over the shop's tiles.
        /// </summary>
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
            int x, z;
            if (Door(reg, out x, out z))
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
            c.Open = !reg.temporarilyClosed && today != null && today.isOpen;   // (the two things the game's own hourly cleaning asks of a building and its day)
        }

        /// <summary>
        /// A visit's cleaning at a shop of the player's, for some minutes of an hour: every tile loses what the game
        /// would take off it for that cleaner's work (their skill by their satisfaction over the cleaners the floor
        /// needs), in proportion to the minutes. Returns what came off a tile, in hundredths; 0 if the game would not
        /// clean the shop now.
        /// </summary>
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
            for (int i = 0; i < spots.Count; i++)
            {
                floor.SetDirtiness(i, Mathf.Max(0f, spots[i].dirtiness - off));
            }
            Shown(reg);
            return Mathf.RoundToInt(off * 100f);
        }

        /// <summary>
        /// The base of a cleaner who was out for some minutes of the hour that is beginning. The game is about to
        /// take a whole hour of their work off every tile there (this runs on the game's hour event, which comes
        /// before its hourly cleaning: GameManager.RunMainGameTick), so the part of the hour they were elsewhere is
        /// put on first, and the two cancel. Done only where the game will in fact count them: the same tests as
        /// BuildingCleanlinessHelper.RunHourly, BuildingCleanliness.SimulateCleaning and GetCleaningSkill.
        /// Returns what was put on a tile, in hundredths.
        /// </summary>
        public static int Hold(CrewMember member, int awayMinutes)
        {
            BuildingRegistration reg = member.Base;
            EmployeeInstance e = member.Employee;
            if (awayMinutes <= 0 || e == null || !Cleaned(reg) || reg.temporarilyClosed || !e.IsEmployeeAvailable() || !e.HasSkill(ServicesCatalogue.CleaningSkill) || !OnCleaningShift(reg, e.id, Save.Hour))
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

        // What one cleaner takes off every tile of a building in an hour, in the game's points and by its own sum (BuildingCleanliness.SimulateCleaning).
        private static float Points(EmployeeInstance e, BuildingRegistration reg)
        {
            float skill = e.GetSkillValue(ServicesCatalogue.CleaningSkill) * (e.satisfaction / 100f);
            return skill / CleaningRules.Force(BuildingHelper.GetBuildingSquareMeters(reg.Address));
        }

        // True if the building's schedule has this employee on a cleaning shift in this hour of today, and the day is open.
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

        // The floor of the building the player stands in is drawn again, as the game draws it after its own cleaning.
        private static void Shown(BuildingRegistration reg)
        {
            if (BuildingManager.IsInsideBuilding && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.buildingRegistration, reg))
            {
                InstanceBehavior<BuildingManager>.Instance.UpdateDirtinessInCurrentBuilding();
            }
        }

        // ---------------------------------------------------------------- the crew

        /// <summary>Everybody the player employs who has the cleaning skill, the game's order kept. Candidates and the player are left out.</summary>
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
            return e != null && !string.IsNullOrEmpty(e.id) && !e.IsCandidate && !e.isBeingReplaced && e.characterData != null && e.characterData.skills != null && e.HasSkill(ServicesCatalogue.CleaningSkill);
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

        /// <summary>
        /// The crew as the game has them now. Somebody who has left, or is no cleaner any more, is taken off the crew.
        /// One look-up by id each, and a walk over their base's shifts of today.
        /// </summary>
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
            int x, z;
            if (m.Base != null && Door(m.Base, out x, out z))
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

        /// <summary>
        /// Today's cleaning shifts of the crew as the round takes them: one entry for each shift of somebody who is
        /// at work today, at the building they are assigned to. The entries are the caller's and are made again.
        /// </summary>
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

        /// <summary>A number that changes when anything the round depends on does: who is on a cleaning shift when, who is off sick, which shops the game would clean today. Worked out once an hour.</summary>
        public static int Signature(List<CrewMember> crew, List<CleaningClient> clients)
        {
            unchecked
            {
                int h = 17;
                for (int m = 0; m < crew.Count; m++)
                {
                    EmployeeInstance e = crew[m].Employee;
                    BuildingRegistration reg = e != null && e.IsAssignedToAnyBusiness() ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
                    h = h * 31 + (e != null && e.IsEmployeeAvailable() ? 1 : 0);
                    h = h * 31 + (reg != null ? reg.StreetNumber * 7 + (reg.StreetName != null ? reg.StreetName.Length : 0) : -1);
                    ScheduleDay today = reg != null ? BuildingHelper.GetTodaySchedule(reg) : null;
                    List<WorkShift> shifts = today != null && today.isOpen ? today.workShifts : null;
                    for (int i = 0; e != null && shifts != null && i < shifts.Count; i++)
                    {
                        WorkShift shift = shifts[i];
                        if (shift != null && shift.type == WorkShiftType.Cleaning && shift.employeeId == e.id)
                        {
                            h = h * 31 + shift.startingHour * 24 + shift.endingHour;
                        }
                    }
                }
                for (int i = 0; i < clients.Count; i++)
                {
                    h = h * 31 + (clients[i].Open ? 1 : 0) + (clients[i].Gone ? 2 : 0);
                }
                return h;
            }
        }

        /// <summary>The wage the game itself would offer a cleaner of a skill, an hour, in hundredths: what "one cleaner per shop" is priced at.</summary>
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

        /// <summary>How many cleaning hours a week a shop's own schedule has, with somebody on them: what its own cleaners do there beside the service.</summary>
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

        // ---------------------------------------------------------------- businesses that could want a contract

        /// <summary>
        /// The city's businesses that are not the player's, are no special service (a bank, a hospital) and are of a
        /// building type the game cleans: each with its size, the customers it has room for and its door. One pass
        /// over the save's buildings; asked for when an offer is drawn, every few days.
        /// </summary>
        public static void Candidates(List<ContractCandidate> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                try
                {
                    if (reg == null || reg.RentedByPlayer || !reg.HasValidAddress || !reg.HasEstablishedBusiness || reg.businessTypeName == EmptyType)
                    {
                        continue;
                    }
                    Building building = reg.BuildingCached;
                    if (building == null || building.SpecialService != null || !BuildingTypeHelper.GetData(building).NeedsCleaning)
                    {
                        continue;
                    }
                    int x, z;
                    if (!Door(reg, out x, out z))
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

        // ---------------------------------------------------------------- money

        /// <summary>Takes an amount as a transaction of the mod's own. With 'deductible' it is marked as the game marks a business cost. Unless 'force', the game's call refuses a charge the cash does not cover.</summary>
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

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }
    }
}
