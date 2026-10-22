using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Schedule;
using Entities;
using Helpers;
using Localizor;
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

    /// <summary>One place of the player's on the Rota tab's list, with a glance at its week.</summary>
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

    /// <summary>A business's week as it was before the planner wrote over it.</summary>
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
        public BuildingRegistration Reg;
        public long Stamp;                  // what it was worked out from (RotaGame.Stamp): a change means working it out again
        public readonly RotaShop Shop = new RotaShop();
        public readonly RotaProblem Problem = new RotaProblem();
        public readonly RotaOutlook Outlook = new RotaOutlook();
        public readonly RotaPlan Now = new RotaPlan();          // the rota it has, rated
        public readonly RotaLadderResult Ladder = new RotaLadderResult();
        public RotaPlan Shown;                                  // the rota the page speaks of: the one just written, else the advised one
        public readonly List<ItemInstance> Stations = new List<ItemInstance>(16);
        public readonly List<EmployeeInstance> Workers = new List<EmployeeInstance>(16);
        public readonly List<string> GroupNames = new List<string>(8);
        public readonly List<string> Broken = new List<string>(2);  // stations out of order, by name
        public bool FromBooks;              // a customer's worth is from the last days' books; false: from the prices
        public bool RequirementsMet = true; // the game sends customers only to a business that meets its requirements
        public bool Late;                   // it opens late or early now without the late opening permit
        public bool Permit;                 // it holds the late opening permit, or licences are off: no hour is barred
        public bool TooLarge;               // more stations or employees than the planner keeps: nothing read, nothing written
        public bool[] Fixed = new bool[0];  // [worker] left as they are: a headquarters desk holder, or on a station not read
        public readonly List<string> Products = new List<string>(8);    // the game's names of the goods in Shop.Stock, in order
        public int UrgentPercent;           // the surcharge of the game's Urgent Order, per cent
        public int FixedCount;
    }

    /// <summary>
    /// Everything the staff rota planner reads from or writes to the game; this part has the places and the stamp.
    /// The game's own customer forecast already holds the mod's footfall changes, written into the promotion figure,
    /// so nothing of the mod's is counted twice. Nothing runs on a timer: only on the Rota tab and its buttons.
    /// </summary>
    internal static partial class RotaGame
    {
        private const string Headquarters = "ba:businesstype_headquarters", Warehouse = "ba:businesstype_warehouse", Empty = "ba:businesstype_empty";
        private const string Cleaning = "ba:skill_cleaning", CinemaScreen = "ba:itemname_screencinema";
        private static readonly string[] Drivers = { "ba:skill_deliverydriver" };   // left out by the game's own auto fill (ScheduleAutoFillerHelper)

        // BuildingCleanliness: dirt a dirtying adds, the share of an item's spots it reaches, cleaners a square metre.
        private const float DirtPerTime = 1.2f, DirtShare = 0.66f, CleanersPerSquareMetre = 0.005f;
        private const byte NotRead = 255;       // an assignable item that is no station of the planner's

        public static IModLogger Logger;

        private static readonly List<ItemInstance> Items = new List<ItemInstance>(32), One = new List<ItemInstance>(1);
        private static readonly List<EmployeeInstance> People = new List<EmployeeInstance>(32);
        private static readonly List<int> GroupOf = new List<int>(32), CapOf = new List<int>(32), ItemCaps = new List<int>(32);
        private static readonly List<byte> ItemKinds = new List<byte>(32);
        private static readonly List<string> ItemKeys = new List<string>(32), ItemLimits = new List<string>(32);
        private static readonly List<RotaShift> Present = new List<RotaShift>(64), Counted = new List<RotaShift>(64);
        private static readonly List<string> GroupKeys = new List<string>(8), GroupLimits = new List<string>(8);
        private static readonly CustomerEntriesCalculator Plain = new CustomerEntriesCalculator();   // a shop's customers before the cap at what its stations serve
        private static readonly List<byte> GroupKinds = new List<byte>(8);
        private static readonly Dictionary<string, int> StationAt = new Dictionary<string, int>(32), WorkerAt = new Dictionary<string, int>(32);
        private static readonly Dictionary<(string, int), RotaPlace> PlaceAt = new Dictionary<(string, int), RotaPlace>(32);
        private static readonly Dictionary<string, RotaDemand> Known = new Dictionary<string, RotaDemand>(32);
        private static readonly HashSet<string> Unknown = new HashSet<string>();
        private static readonly List<RotaDemand> Demands = new List<RotaDemand>(8);
        private static readonly WorkStationInfo Info = new WorkStationInfo();
        private static readonly Probe Calculator = new Probe();
        private static EmployeeInstance _made;                  // nobody: the employee a demand is asked about
        private static List<ScheduleDay> _madeWeek;
        private static WorkShift _madeShift;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The places of the player's past the most read (CompanyCatalogue.ShopsMost) on the last read: not listed.</summary>
        public static int LeftOut { get; private set; }

        /// <summary>Every place the player runs: one pass over the save's buildings and one over its staff.</summary>
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
            LeftOut = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress
                    || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty || reg.scheduleDays == null)
                {
                    continue;
                }
                if (into.Count >= CompanyCatalogue.ShopsMost)
                {
                    LeftOut++;   // counted, so the Managers tab can say so; the rest of the pass is a few comparisons each
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
            // An employee counts at the first place with their street and number.
            PlaceAt.Clear();
            for (int k = 0; k < into.Count; k++)
            {
                (string, int) key = (into[k].Reg.StreetName, into[k].Reg.StreetNumber);
                if (!PlaceAt.ContainsKey(key))
                {
                    PlaceAt.Add(key, into[k]);
                }
            }
            List<EmployeeInstance> staff = save != null ? save.EmployeeInstances : null;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.assignedAddress == null || e.IsCandidate)
                {
                    continue;
                }
                if (PlaceAt.TryGetValue((e.assignedAddress.streetName, e.assignedAddress.streetNumber), out RotaPlace place))
                {
                    place.Staff++;
                }
            }
            PlaceAt.Clear();
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
            if (Tech.TechTypes.Is(reg.businessTypeName))
            {
                // The game sends a tech firm no customers; planned for them, its people would be idle all week.
                return RotaType.Work;
            }
            BusinessType type = BusinessTypeHelper.GetData(reg);
            return type != null && type.HasTag(TagRef.Businesstag.generatesrevenue) ? RotaType.Customers : RotaType.Work;
        }

        // Open hours in the week, and those with nobody on a work shift.
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
                open += RotaSolver.Bits(hours);
                gaps += RotaSolver.Bits(hours & ~staffed);
            }
        }

        private static uint Mask(int from, int to)
        {
            from = Mathf.Clamp(from, 0, RotaCatalogue.Hours);
            to = Mathf.Clamp(to, from, RotaCatalogue.Hours);
            return to > from ? (uint)(((1UL << (to - from)) - 1UL) << from) : 0u;
        }

        /// <summary>
        /// A number that changes when anything a sheet is worked out from does: customers, stations, prices, goods,
        /// hours, shifts, and everybody assigned with wage, satisfaction, skills and demands. A few thousand additions.
        /// </summary>
        public static long Stamp(RotaPlace place, int extra)
        {
            BuildingRegistration reg = place.Reg;
            long h = RotaLedger.StampStart;
            h = RotaLedger.Mix(h, place.Place);
            h = RotaLedger.Mix(h, Save != null ? Save.Day : 0);
            h = RotaLedger.Mix(h, extra);
            h = RotaLedger.Mix(h, reg.businessTypeName);
            h = RotaLedger.Mix(h, reg.promotion != null ? reg.promotion.total : 0);
            h = RotaLedger.Mix(h, reg.satisfaction != null ? reg.satisfaction.overall : 0);
            h = RotaLedger.Mix(h, reg.customerCapacity);
            h = RotaLedger.Mix(h, reg.temporarilyClosed ? 1 : 0);
            h = RotaLedger.Mix(h, reg.orderHistory != null ? reg.orderHistory.Count : 0);
            h = RotaLedger.Mix(h, reg.dirtSpots != null ? reg.dirtSpots.Count : 0);
            h = RotaLedger.Mix(h, Save != null && Save.disabledLicensingFees != null ? Save.disabledLicensingFees.Count : 0);
            h = StockStamp(h, reg);
            foreach (KeyValuePair<string, ItemInstance> item in reg.itemInstances)
            {
                Item data = item.Value != null ? item.Value.ItemCached : null;
                if (data != null && data.assignable)
                {
                    h = RotaLedger.Mix(h, item.Key);
                    h = RotaLedger.Mix(h, ItemHelper.HasAnyMissingRequirements(item.Value) ? 1 : 2);
                }
            }
            for (int i = 0; reg.retailPrices != null && i < reg.retailPrices.Count; i++)
            {
                h = RotaLedger.Mix(h, reg.retailPrices[i].itemName);
                h = RotaLedger.Mix(h, Mathf.RoundToInt(reg.retailPrices[i].price * 100f));
            }
            for (int i = 0; reg.cachedAvailableProducts != null && i < reg.cachedAvailableProducts.Count; i++)
            {
                h = RotaLedger.Mix(h, reg.cachedAvailableProducts[i]);
            }
            List<ScheduleDay> week = reg.scheduleDays;
            for (int i = 0; week != null && i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                h = RotaLedger.Mix(h, day.isOpen ? 7 : 3);
                for (int k = 0; day.openingHourSlots != null && k < day.openingHourSlots.Count; k++)
                {
                    h = RotaLedger.Mix(h, day.openingHourSlots[k].startingHour * 32 + day.openingHourSlots[k].endingHour);
                }
                for (int k = 0; day.workShifts != null && k < day.workShifts.Count; k++)
                {
                    WorkShift shift = day.workShifts[k];
                    h = RotaLedger.Mix(h, shift.startingHour * 32 + shift.endingHour);
                    h = RotaLedger.Mix(h, shift.employeeId);
                    h = RotaLedger.Mix(h, shift.itemInstanceId);
                }
            }
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.assignedAddress == null || e.assignedAddress.streetNumber != reg.StreetNumber
                    || e.assignedAddress.streetName != reg.StreetName)
                {
                    continue;
                }
                h = RotaLedger.Mix(h, e.id);
                h = RotaLedger.Mix(h, Mathf.RoundToInt(e.hourlyWage * 100f));
                h = RotaLedger.Mix(h, Mathf.RoundToInt(e.satisfaction));
                for (int k = 0; e.demands != null && k < e.demands.Count; k++)
                {
                    h = RotaLedger.Mix(h, e.demands[k]);
                }
                for (int k = 0; e.characterData != null && e.characterData.skills != null && k < e.characterData.skills.Count; k++)
                {
                    h = RotaLedger.Mix(h, e.characterData.skills[k].name);
                    h = RotaLedger.Mix(h, Mathf.RoundToInt(e.characterData.skills[k].value));
                }
            }
            return h;
        }

        public static string Name(ItemInstance item)
        {
            return item == null ? string.Empty : string.IsNullOrEmpty(item.alias) ? Text(item.itemName) : item.alias;   // as the game's schedule screen names a station
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
