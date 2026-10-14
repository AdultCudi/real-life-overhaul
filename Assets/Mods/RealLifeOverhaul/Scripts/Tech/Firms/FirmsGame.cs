using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using Helpers;
using Localizor;
using Streets;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Everything the firms part reads from or does to the game.
    ///
    /// READ: the player's businesses of the tech sector's types, and their web development agencies and graphic designers
    /// (the game's own office businesses, which join the same economy); what a game office sold yesterday and its hour's
    /// price (from the game's statement and ItemHelper.GetPrice), so that only the hours its staff had spare go to the
    /// tech sector's work; the player's other businesses, as clients; for the competitors' rule, every tech firm and
    /// academy the player or another player runs, with its address and district.
    ///
    /// DONE: money as dated transactions of the mod's own at the firm's address (takings counted among the business's
    /// takings in the game's statement through the economy's books; hosting and software as a row of its costs; marketing
    /// in the game's marketing line), the costs marked deductible as the game marks its own; the research and development
    /// relief given to the game's tax as an extra deductible expense with no money moving (TaxHelper.TrackTransaction, as
    /// the economy's books give Empire Motors' costs). A fine is charged at once and is not deductible.
    /// A competitor's tower or business set as the destination, as the game's own Set destination does it.
    /// </summary>
    internal static class FirmsGame
    {
        public const string TakingsKey = "reallife_transaction_tech_takings";
        public const string HostingKey = "reallife_transaction_tech_hosting";
        public const string SoftwareKey = "reallife_transaction_tech_software";
        public const string MarketingKey = "reallife_transaction_tech_marketing";
        public const string FineKey = "reallife_transaction_tech_data_fine";
        public const string RegisterKey = "reallife_transaction_tech_register_fee";
        public const string ServicesKey = "reallife_transaction_tech_it_services";
        public const string ReliefKey = "reallife_transaction_tech_relief";
        public const string CostRow = "reallife_books_tech_hosting";
        public const string PowerRow = "reallife_books_tech_power";
        private const string DeductKey = "reallife_tech_deduct";
        private const string ReliefDeductKey = "reallife_tech_relief_deduct";
        private const string Marketing = "ba:transactioncategory_marketing";
        public const string WebAgency = "ba:businesstype_webdevelopmentagency";
        public const string GraphicDesigner = "ba:businesstype_graphicdesigner";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>True for a transaction type that is the tech sector's takings (for the economy's books).</summary>
        public static bool IsTakings(string type) => type == TakingsKey || type == InfraGame.TakingsKey;

        /// <summary>The row of a business's costs a transaction type of the tech sector's belongs to; null for none.</summary>
        public static string RowOf(string type) => type == HostingKey || type == SoftwareKey || type == InfraGame.ComputeKey ? CostRow
            : type == InfraGame.PowerKey || type == InfraGame.WaterKey ? PowerRow : null;

        /// <summary>The firm kind of a business's type, or -1 for a business that is no tech firm.</summary>
        public static int KindOf(string type)
        {
            int k = TechTypes.KindOf(type);
            if (k >= 0)
            {
                return k;
            }
            return type == WebAgency ? (int)FirmKind.WebAgency : type == GraphicDesigner ? (int)FirmKind.Design : -1;
        }

        /// <summary>The player's tech firms: their businesses of the sector's types and their game office businesses. One pass over the buildings.</summary>
        public static void Firms(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && KindOf(reg.businessTypeName) >= 0)
                {
                    into.Add(reg);
                }
            }
        }

        /// <summary>The player's businesses that are not tech firms, as clients: place and name.</summary>
        public static void Clients(List<KeyValuePair<string, string>> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count && into.Count < 24; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName)
                    && reg.businessTypeName != "ba:businesstype_empty" && KindOf(reg.businessTypeName) < 0)
                {
                    into.Add(new KeyValuePair<string, string>(TalentGame.Place(reg), TalentGame.NameOf(reg)));
                }
            }
        }

        /// <summary>
        /// The share of a game office's staff hours that went unsold yesterday, thousandths: its hours worked against what
        /// it sold at its hour's price. All of it for a business of the tech sector's own types, which the game sells nothing for.
        /// </summary>
        public static int Spare(BuildingRegistration reg, int hoursWorked)
        {
            if (reg == null || TechTypes.Is(reg.businessTypeName))
            {
                return 1000;
            }
            try
            {
                float sold = Economy.Books.SalesYesterday(reg.Address);
                string item = reg.cachedAvailableProducts != null && reg.cachedAvailableProducts.Count > 0 ? reg.cachedAvailableProducts[0] : null;
                float price = string.IsNullOrEmpty(item) ? 0f : ItemHelper.GetPrice(item, reg);
                if (price <= 0.01f || hoursWorked <= 0)
                {
                    return hoursWorked > 0 ? 1000 : 0;
                }
                float used = sold / price / hoursWorked;
                return Mathf.Clamp(Mathf.RoundToInt((1f - used) * 1000f), 0, 1000);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ---------------------------------------------------------------- whether its people can work

        private static readonly List<ItemInstance> Stations = new List<ItemInstance>(32);
        private static readonly HashSet<string> WorkerIds = new HashSet<string>();
        private static readonly HashSet<string> WorkerSkills = new HashSet<string>();

        /// <summary>
        /// What decides whether a firm's people can work on a kind of contract (-1: any of the firm's work), read from the
        /// game as its own schedule screen reads it: who is at the firm and whose role does that work (and how long they
        /// worked yesterday), the game's switch and the utilities behind it (the internet the firm needs, the power), the
        /// days of the week it opens (ScheduleDay.isOpen with opening hours), the workstations they can use
        /// (GetAssignableItems whose suitable skills hold one of theirs, as ScheduleHelper.HasSkillForWorkstation asks) and
        /// their shifts on the days it opens (as EmployeeInstance.IsWorking counts them). A professional on the mod's own
        /// payroll works a plain week with no shift. Read only: nothing of the game's is changed.
        /// </summary>
        public static FirmFacts Facts(BuildingRegistration reg, FirmKind firm, int kind, IReadOnlyList<Person> people, string place, int day)
        {
            var f = new FirmFacts();
            WorkerIds.Clear();
            WorkerSkills.Clear();
            for (int i = 0; people != null && i < people.Count; i++)
            {
                Person person = people[i];
                if (person.File.Place != place)
                {
                    continue;
                }
                f.Staff++;
                RoleDef r = person.File.Def;
                if (kind >= 0 ? !FirmsRules.CanDo(r, (WorkKind)kind) : !FirmsRules.WorksFor(r, firm))
                {
                    continue;
                }
                f.Workers++;
                f.Worked += person.HoursYesterday;
                if (person.Game == null)
                {
                    f.Stations++;
                    f.ShiftHours += TalentCatalogue.WeekHours;
                    f.ShiftsYesterday += person.HoursYesterday;
                    continue;
                }
                WorkerIds.Add(person.Game.id);
                List<Skill> skills = person.Game.characterData != null ? person.Game.characterData.skills : null;
                for (int s = 0; skills != null && s < skills.Count; s++)
                {
                    WorkerSkills.Add(skills[s].name);
                }
            }
            if (reg == null)
            {
                return f;
            }
            try
            {
                f.Closed = reg.temporarilyClosed;
                if (f.Closed)
                {
                    f.Net = TechBridges.NetOf(reg, out f.NetWhen);
                    f.Dark = TechBridges.Supplied != null && !TechBridges.Supplied(reg, 0);
                }
                DayOfWeekOrdered yesterday = TimeHelper.GetDayOfWeek(day - 1);
                List<ScheduleDay> week = reg.scheduleDays;
                for (int d = 0; week != null && d < week.Count; d++)
                {
                    ScheduleDay sd = week[d];
                    if (sd == null || !sd.isOpen || sd.openingHourSlots == null || sd.openingHourSlots.Count == 0)
                    {
                        continue;
                    }
                    f.OpenDays++;
                    for (int s = 0; sd.workShifts != null && s < sd.workShifts.Count; s++)
                    {
                        WorkShift shift = sd.workShifts[s];
                        if (shift == null || string.IsNullOrEmpty(shift.employeeId) || !WorkerIds.Contains(shift.employeeId))
                        {
                            continue;
                        }
                        int hours = Mathf.Max(0, shift.endingHour - shift.startingHour);
                        f.ShiftHours += hours;
                        f.ShiftsYesterday += sd.day == yesterday ? hours : 0;
                    }
                }
                if (WorkerSkills.Count > 0)
                {
                    reg.GetAssignableItems(Stations);
                    for (int i = 0; i < Stations.Count; i++)
                    {
                        string[] suits = Stations[i] != null && Stations[i].ItemCached != null ? Stations[i].ItemCached.suitableSkills : null;
                        for (int s = 0; suits != null && s < suits.Length; s++)
                        {
                            if (WorkerSkills.Contains(suits[s]))
                            {
                                f.Stations++;
                                break;
                            }
                        }
                    }
                    Stations.Clear();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return f;
        }

        /// <summary>The game's BizMan opened on a business's schedule, where shifts are given (and its own auto fill is). False if it cannot be.</summary>
        public static bool OpenSchedule(BuildingRegistration reg)
        {
            try
            {
                global::UI.UIs ui = InstanceBehavior<global::UI.UIs>.Instance;
                if (reg == null || reg.Address == null || ui == null || ui.fullMenu == null || ui.fullMenu.bizMan == null || !GoingPublic.Mine(reg))
                {
                    return false;
                }
                ui.fullMenu.bizMan.Open(reg.Address, "Schedule");
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // ---------------------------------------------------------------- competitors

        /// <summary>
        /// Every business the player runs, or another player in a Going Public session (which Going Public puts on this
        /// machine as rented by the player), that is a tech firm or a training academy (its type: null while the academy is
        /// off), for the competitors' rule (CompetitorRules.Gather). One pass over the buildings. Read only.
        /// </summary>
        public static void Seen(List<FirmSeen> into, string academy)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !reg.RentedByPlayer || !reg.HasValidAddress || !reg.HasEstablishedBusiness)
                {
                    continue;
                }
                int kind = KindOf(reg.businessTypeName);
                if (kind < 0 && academy != null && reg.businessTypeName == academy)
                {
                    kind = CompetitorRules.Academy;
                }
                if (kind >= 0)
                {
                    into.Add(new FirmSeen { Kind = kind, Place = TalentGame.Place(reg), Name = TalentGame.NameOf(reg), Theirs = GoingPublic.Foreign(reg) });
                }
            }
        }

        /// <summary>Where a business of the player's or another player's is, for its competitors' page: its address and its district in words. False if it is not in the city's list.</summary>
        public static bool Where(string place, out string street, out int number, out string district)
        {
            street = null;
            number = 0;
            district = null;
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !reg.RentedByPlayer || !reg.HasValidAddress || TalentGame.Place(reg) != place)
                {
                    continue;
                }
                street = reg.StreetName;
                number = reg.StreetNumber;
                try
                {
                    string id = reg.Neighborhood;
                    district = string.IsNullOrEmpty(id) ? null : id.GetLocalization();
                }
                catch (Exception)
                {
                    district = null;
                }
                return true;
            }
            return false;
        }

        /// <summary>An address as the game writes it.</summary>
        public static string AddressWords(string street, int number)
        {
            try
            {
                return string.IsNullOrEmpty(street) ? string.Empty : new Address(street, number).ToFormattedString();
            }
            catch (Exception)
            {
                return street + " " + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// The destination set at a building, as the game's own Set destination does it (BizManBusiness.SetDestination): the
        /// address kept in the save, the game's destination marker on it (a special building's own name on the marker, as
        /// the city map's double click puts it: Navigation), the game's notification naming it, and the phone closed. Only
        /// the player's own destination is changed. False if it cannot be.
        /// </summary>
        public static bool Destination(string street, int number)
        {
            try
            {
                GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
                if (string.IsNullOrEmpty(street) || Save == null || guiders == null)
                {
                    return false;
                }
                var address = new Address(street, number);
                Save.customDestination = address;
                GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
                string name = address.ToFormattedString();
                string special = SpecialBuildings.NameFor(address);
                if (special != null)
                {
                    if (guiders.destinationGuider != null && guiders.destinationGuider.poi != null)
                    {
                        guiders.destinationGuider.poi.SetText(special);
                    }
                    name = TechText.T("reallife_map_name_at").Replace("{what}", special).Replace("{place}", name);
                }
                Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string>(1) { { "address", name } });
                global::UI.UIs ui = InstanceBehavior<global::UI.UIs>.Instance;
                if (ui != null && ui.fullMenu != null)
                {
                    ui.fullMenu.Toggle(false);
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>Takings at the firm, cents, as a dated transaction of the mod's own (counted among its takings in the game's statement).</summary>
        public static void Takings(long cents, BuildingRegistration at, string name, int? day)
        {
            if (cents > 0 && Save != null && at != null)
            {
                GameManager.ChangeMoneySafe(cents / 100f, Info(TakingsKey, name, null), day, at.Address, true);
            }
        }

        /// <summary>A cost of the firm, cents: deductible; marketing in the game's marketing line. Forced (it was used).</summary>
        public static void Cost(long cents, string key, BuildingRegistration at, string name, int? day)
        {
            if (cents <= 0 || Save == null)
            {
                return;
            }
            TransactionInfo info = Info(key, name, key == MarketingKey ? Marketing : null);
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            GameManager.ChangeMoneySafe(-cents / 100f, info, day, at != null ? at.Address : null, true);
        }

        /// <summary>A charge the player chose to pay now (the register's fee), cents; refused if the cash does not cover it.</summary>
        public static bool Pay(long cents, string key, BuildingRegistration at, string name)
        {
            if (cents <= 0)
            {
                return true;
            }
            TransactionInfo info = Info(key, name, null);
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            return Save != null && GameManager.ChangeMoneySafe(-cents / 100f, info, null, at != null ? at.Address : null, false);
        }

        /// <summary>A fine, cents: charged at once, not deductible.</summary>
        public static void Fine(long cents, BuildingRegistration at, string name)
        {
            if (cents > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(-cents / 100f, Info(FineKey, name, null), null, at != null ? at.Address : null, true);
            }
        }

        /// <summary>The research and development relief: an extra deductible expense for the game's tax, with no money moving. Returns what was given, whole money.</summary>
        public static float Relief(long cents)
        {
            if (cents <= 0)
            {
                return 0f;
            }
            float amount = Mathf.Round(cents) / 100f;
            var info = new TransactionInfo(ReliefKey);
            info.SetTaxDeductibleName(ReliefDeductKey.GetLocalization());
            TaxHelper.TrackTransaction(new Transaction(info) { amount = -amount });
            return amount;
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, string name, string category)
        {
            var data = new Dictionary<string, string>(2) { { "name", name ?? string.Empty } };
            return category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
        }
    }
}
