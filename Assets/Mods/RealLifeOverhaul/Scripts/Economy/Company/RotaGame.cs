using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings;
using Buildings.Office.Headquarters;
using Buildings.Retail.Businesses.CinemaTheater;
using Buildings.Schedule;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Helpers;
using Localizor;
using PlayerActivity;
using UI;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.Schedule;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How a place of the player's is planned.</summary>
    internal enum RotaType
    {
        Customers = 0,      // people walk in: the forecast, the hours worth opening, the rota
        Work = 1,           // nobody walks in (a factory): the rota for the hours it keeps
        Headquarters = 2,   // the game's office hours, one desk a person for the whole week
        Warehouse = 3       // no rota: the game gives the drivers their vehicles itself
    }

    /// <summary>One place of the player's on the Rota tab's list: what a look at its week costs nothing to say.</summary>
    internal sealed class RotaPlace
    {
        public BuildingRegistration Reg;
        public string Place = string.Empty, Name = string.Empty;
        public RotaType Type;
        public int Staff;           // employees assigned there
        public int OpenHours;       // in the week
        public int GapHours;        // open hours with nobody on a work shift
        public bool Shut;           // the game has it temporarily closed
    }

    /// <summary>A week's rota and opening hours of a business as they were before the planner wrote over them.</summary>
    internal sealed class RotaUndo
    {
        public string Place;
        public bool Kept;           // made by a test tool: kept when the app closes
        public readonly bool[] Open = new bool[RotaCatalogue.Days];
        public readonly List<OpeningHourSlot>[] Slots = new List<OpeningHourSlot>[RotaCatalogue.Days];
        public readonly List<WorkShift>[] Shifts = new List<WorkShift>[RotaCatalogue.Days];
    }

    /// <summary>Everything worked out for the one business whose rota is looked at.</summary>
    internal sealed class RotaSheet
    {
        public RotaPlace Place;
        public BuildingRegistration Reg;    // the business it is of
        public long Stamp;                  // what it was worked out from: a change of it means working it out again
        public readonly RotaShop Shop = new RotaShop();
        public readonly RotaProblem Problem = new RotaProblem();
        public readonly RotaOutlook Outlook = new RotaOutlook();
        public readonly RotaPlan Now = new RotaPlan();          // the rota it has, rated
        public RotaPlan Shown;                                  // the rota the page speaks of: the one just written, else the advised one
        public readonly List<ItemInstance> Stations = new List<ItemInstance>(16);
        public readonly List<EmployeeInstance> Workers = new List<EmployeeInstance>(16);
        public readonly List<string> GroupNames = new List<string>(8);
        public readonly List<string> Broken = new List<string>(2);  // stations out of order, by name
        public bool FromBooks;              // what a customer is worth comes from the game's books of the last days; false: from its prices
        public bool RequirementsMet = true; // the game sends customers only to a business that meets its requirements
        public bool Late;                   // the advised hours need the mod's late opening permit and the present ones do not
    }

    /// <summary>
    /// Everything the staff rota planner reads from or writes to the game, in one place.
    ///
    /// READ
    ///   customers    the game's own calculator for the business (CustomerEntriesCalculatorFactory, GetInitialCustomers,
    ///                GetCustomersByHour): capacity, promotion, the type's day and hour factors, and for an office the
    ///                neighbourhood's demand and the business's satisfaction. An office's figure is capped by the game
    ///                at the desks staffed now; the uncapped figure is asked through a class of the planner's derived
    ///                from the game's (<see cref="Probe"/>), by the game's own factors. A shop's figure stops at what
    ///                its stations serve; who would come beyond that (for "one more till would serve") is asked of the
    ///                game's plain calculator, the same arithmetic without that stop.
    ///                THE MOD'S OWN CHANGES ARE ALREADY IN IT: the city's economy, a campaign, overtime and a strike
    ///                change customers by writing the business's promotion figure (Business/BusinessGame.Footfall,
    ///                see Wiring/CompanyLinks), which GetPromotionMultiplier reads. So nothing of the mod's is added
    ///                here a second time; a day on which a campaign or a strike will have ended or begun is scaled by
    ///                the director from the company's own rules (CompanyDirector.Rota.cs)
    ///   worth        a customer's takings and the cost of the goods from the business's own order history of the
    ///                last days (BuildingRegistration.orderHistory); with no history, from what it sells and its
    ///                prices by the game's own product arithmetic (CustomerEntriesCalculatorRetail)
    ///   stations     BuildingRegistration.GetAssignableItems, as the game's schedule screen and auto fill;
    ///                what a station serves an hour by ItemHelper.GetItemsSortedByCapacity, the rule the game's day
    ///                simulation uses (RetailBusinessSimulator.GetMaxCustomerCapacityThisHour)
    ///   skills       an employee may stand at a station if one of their skills is among the item's suitable skills
    ///                (ScheduleHelper.HasSkillForWorkstation); how well, by skill and satisfaction
    ///                (EmployeeInstance.GetCustomerSatisfaction)
    ///   demands      each of the game's rota demands, asked of the demand itself: its public members where it has
    ///                them, otherwise its own Fulfilled answer for a made-up week (see <see cref="Demand"/>). No
    ///                private member of the game is read
    ///   fees         the cinema's and the theatre's licensing fee for a day it opens (LicensingFeesHelper)
    ///   cleaning     the game's dirt and cleaning figures (BuildingCleanliness)
    ///
    /// WRITTEN, and only on the player's button: ScheduleDay.workShifts, and with "use these hours" ScheduleDay.isOpen
    /// and openingHourSlots, followed by exactly what the game's own auto fill and schedule screen do afterwards
    /// (<see cref="Write"/>). Shifts of people the planner does not manage are left as they are.
    ///
    /// Nothing here runs on a timer: when the Rota tab is opened, when a business is picked, and on a button.
    /// Reading a business is in RotaGame.Read.cs, its employees' demands in RotaGame.Demands.cs, writing a rota and
    /// putting one back in RotaGame.Write.cs.
    /// </summary>
    internal static partial class RotaGame
    {
        private const string Headquarters = "ba:businesstype_headquarters", Warehouse = "ba:businesstype_warehouse", Empty = "ba:businesstype_empty";
        private const string Cleaning = "ba:skill_cleaning", CinemaScreen = "ba:itemname_screencinema";
        private static readonly string[] Drivers = { "ba:skill_deliverydriver" };   // the game's own auto fill leaves them out (ScheduleAutoFillerHelper)

        // The game's dirt figures (BuildingCleanliness: AmountOfDirtToIncreasePerTime, PercentageOfCellsToAffectInItem, CleanersNeededPerSquareMeter).
        private const float DirtPerTime = 1.2f, DirtShare = 0.66f, CleanersPerSquareMetre = 0.005f;

        public static IModLogger Logger;

        private static readonly List<ItemInstance> Items = new List<ItemInstance>(32), One = new List<ItemInstance>(1);
        private static readonly List<EmployeeInstance> People = new List<EmployeeInstance>(32);
        private static readonly List<int> GroupOf = new List<int>(32), CapOf = new List<int>(32);
        private static readonly List<string> GroupKeys = new List<string>(8), GroupLimits = new List<string>(8);
        private static readonly CustomerEntriesCalculator Plain = new CustomerEntriesCalculator();   // the game's plain calculator: a shop's figure before the cap at what its stations serve
        private static readonly List<byte> GroupKinds = new List<byte>(8);
        private static readonly Dictionary<string, int> StationAt = new Dictionary<string, int>(32), WorkerAt = new Dictionary<string, int>(32);
        private static readonly Dictionary<string, RotaDemand> Known = new Dictionary<string, RotaDemand>(32);
        private static readonly HashSet<string> Unknown = new HashSet<string>();
        private static readonly List<RotaDemand> Demands = new List<RotaDemand>(8);
        private static readonly WorkStationInfo Info = new WorkStationInfo();
        private static readonly Probe Calculator = new Probe();
        private static EmployeeInstance _made;                  // nobody: the employee a demand is asked about
        private static List<ScheduleDay> _madeWeek;
        private static WorkShift _madeShift;

        private static GameInstance Save => SaveGameManager.Current;

        // ---------------------------------------------------------------- the places

        /// <summary>
        /// Every place the player runs, of any type, with what a look at its week says. One pass over the save's
        /// buildings and one over its employees.
        /// </summary>
        public static void ReadPlaces(List<RotaPlace> into, List<RotaPlace> pool)
        {
            for (int i = 0; i < into.Count; i++)
            {
                into[i].Reg = null;
                pool.Add(into[i]);
            }
            into.Clear();
            GameInstance save = Save;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count && into.Count < CompanyCatalogue.ShopsMost; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !reg.RentedByPlayer || !reg.HasEstablishedBusiness || !reg.HasValidAddress || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty || reg.scheduleDays == null)
                {
                    continue;
                }
                RotaPlace place;
                if (pool.Count > 0)
                {
                    place = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                else
                {
                    place = new RotaPlace();
                }
                place.Reg = reg;
                place.Place = CompanyGame.Place(reg);
                place.Name = reg.BusinessName;
                place.Type = TypeOf(reg);
                place.Shut = reg.temporarilyClosed;
                place.Staff = 0;
                Week(reg, out place.OpenHours, out place.GapHours);
                into.Add(place);
            }
            List<EmployeeInstance> staff = save != null ? save.EmployeeInstances : null;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.assignedAddress == null || e.IsCandidate)
                {
                    continue;
                }
                for (int k = 0; k < into.Count; k++)
                {
                    if (into[k].Reg.StreetNumber == e.assignedAddress.streetNumber && into[k].Reg.StreetName == e.assignedAddress.streetName)
                    {
                        into[k].Staff++;
                        break;
                    }
                }
            }
        }

        private static RotaType TypeOf(BuildingRegistration reg)
        {
            if (reg.businessTypeName == Warehouse)
            {
                return RotaType.Warehouse;
            }
            if (reg.businessTypeName == Headquarters)
            {
                return RotaType.Headquarters;
            }
            BusinessType type = BusinessTypeHelper.GetData(reg);
            return type != null && type.HasTag(TagRef.Businesstag.generatesrevenue) ? RotaType.Customers : RotaType.Work;
        }

        // The open hours of a business's week, and how many of them have nobody on a work shift.
        private static void Week(BuildingRegistration reg, out int open, out int gaps)
        {
            open = gaps = 0;
            List<ScheduleDay> week = reg.scheduleDays;
            for (int i = 0; i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                if (day == null || !day.isOpen || day.openingHourSlots == null)
                {
                    continue;
                }
                uint hours = 0, staffed = 0;
                for (int k = 0; k < day.openingHourSlots.Count; k++)
                {
                    hours |= Mask(day.openingHourSlots[k].startingHour, day.openingHourSlots[k].endingHour);
                }
                for (int k = 0; day.workShifts != null && k < day.workShifts.Count; k++)
                {
                    WorkShift shift = day.workShifts[k];
                    if (shift != null && shift.type == WorkShiftType.Default && !string.IsNullOrEmpty(shift.employeeId))
                    {
                        staffed |= Mask(shift.startingHour, shift.endingHour);
                    }
                }
                open += Bits(hours);
                gaps += Bits(hours & ~staffed);
            }
        }

        private static uint Mask(int from, int to)
        {
            from = Mathf.Clamp(from, 0, RotaCatalogue.Hours);
            to = Mathf.Clamp(to, from, RotaCatalogue.Hours);
            return to > from ? (uint)(((1UL << (to - from)) - 1UL) << from) : 0u;
        }

        private static int Bits(uint mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- what a sheet was worked out from

        /// <summary>
        /// A number that changes when anything the advice is worked out from does: the day, the business's customer
        /// figures, its hours and shifts, its stations, and who works there for what and how content.
        /// </summary>
        public static long Stamp(RotaPlace place, int extra)
        {
            BuildingRegistration reg = place.Reg;
            unchecked
            {
                long h = 1469598103934665603L;
                h = Mix(h, Hash(place.Place));
                h = Mix(h, Save != null ? Save.Day : 0);
                h = Mix(h, extra);
                h = Mix(h, reg.promotion != null ? reg.promotion.total : 0);
                h = Mix(h, reg.satisfaction != null ? reg.satisfaction.overall : 0);
                h = Mix(h, reg.customerCapacity);
                h = Mix(h, reg.temporarilyClosed ? 1 : 0);
                h = Mix(h, reg.itemInstances != null ? reg.itemInstances.Count : 0);
                h = Mix(h, reg.retailPrices != null ? reg.retailPrices.Count : 0);
                h = Mix(h, reg.orderHistory != null ? reg.orderHistory.Count : 0);
                List<ScheduleDay> week = reg.scheduleDays;
                for (int i = 0; week != null && i < week.Count; i++)
                {
                    ScheduleDay day = week[i];
                    h = Mix(h, day.isOpen ? 7 : 3);
                    for (int k = 0; day.openingHourSlots != null && k < day.openingHourSlots.Count; k++)
                    {
                        h = Mix(h, day.openingHourSlots[k].startingHour * 32 + day.openingHourSlots[k].endingHour);
                    }
                    for (int k = 0; day.workShifts != null && k < day.workShifts.Count; k++)
                    {
                        WorkShift shift = day.workShifts[k];
                        h = Mix(h, shift.startingHour * 32 + shift.endingHour);
                        h = Mix(h, Hash(shift.employeeId));
                        h = Mix(h, Hash(shift.itemInstanceId));
                    }
                }
                List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
                for (int i = 0; staff != null && i < staff.Count; i++)
                {
                    EmployeeInstance e = staff[i];
                    if (e == null || e.assignedAddress == null || e.assignedAddress.streetNumber != reg.StreetNumber || e.assignedAddress.streetName != reg.StreetName)
                    {
                        continue;
                    }
                    h = Mix(h, Hash(e.id));
                    h = Mix(h, Mathf.RoundToInt(e.hourlyWage * 100f));
                    h = Mix(h, Mathf.RoundToInt(e.satisfaction));
                    h = Mix(h, e.demands != null ? e.demands.Count : 0);
                    h = Mix(h, e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 ? Mathf.RoundToInt(e.characterData.skills[0].value) : 0);
                }
                return h;
            }
        }

        private static long Mix(long h, int value)
        {
            unchecked
            {
                return (h ^ value) * 1099511628211L;
            }
        }

        private static int Hash(string text)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = h * 31 + text[i];
                }
                return h;
            }
        }

        // ---------------------------------------------------------------- names

        public static string Name(ItemInstance item)
        {
            return item == null ? string.Empty : string.IsNullOrEmpty(item.alias) ? Text(item.itemName) : item.alias;   // (as the game's schedule screen names a station)
        }

        public static string Text(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : LocalizorManager.IsLocalizedKey(key) ? key.GetLocalization() : key;
        }

        public static string TypeName(BuildingRegistration reg)
        {
            return reg != null ? Text(reg.businessTypeName) : string.Empty;
        }

        public static string WorkerName(EmployeeInstance e)
        {
            return e != null && e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?";
        }

        /// <summary>The skill a group of stations takes, as the game names it: what to look for when hiring.</summary>
        public static string GroupSkill(RotaSheet sheet, int group)
        {
            for (int s = 0; s < sheet.Stations.Count; s++)
            {
                string[] skills = sheet.Shop.Group[s] == group ? sheet.Stations[s].ItemCached.suitableSkills : null;
                if (skills != null && skills.Length > 0)
                {
                    return Text(skills[0]);
                }
            }
            return string.Empty;
        }
    }
}
