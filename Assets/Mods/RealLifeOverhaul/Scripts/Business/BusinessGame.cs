using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using BigAmbitions.Rivals;
using BigAmbitions.Tags;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything business pressure reads from or does to the game, in one place. Each change is to
    /// a field or through a method the game itself uses for the same thing, is bounded by the
    /// catalogue, and writes one line to the log saying what was changed from what to what:
    ///   money        GameManager.ChangeMoneySafe with the business's address, never forced;
    ///   footfall     the business's promotion figure (recomputed by the game daily and whenever
    ///                marketing changes, so it heals itself), then the game's own regeneration
    ///                of the customers still to come today;
    ///   closure      BuildingRegistration.TemporarilyClose, the switch on the business's own page;
    ///   theft        stolenItemsCost, which the game books as Theft and clears each night;
    ///   wages        EmployeeInstance.hourlyWage, as the game's own poach counter-offer writes it;
    ///   leaving      the game's own resignation path (what a complaint that is ignored does);
    ///   deliveries   DeliveryContract.nextDeliveryDay.
    /// Nothing here runs on a timer of its own.
    /// </summary>
    internal static class BusinessGame
    {
        public const string Marketing = "ba:transactioncategory_marketing";
        public const string Licensing = "ba:transactioncategory_licensingfees";
        public const string Salaries = "ba:transactioncategory_salaryexpenses";
        private const string EmptyType = "ba:businesstype_empty";
        private const string FactoryType = "ba:businesstype_factory";
        private const string NegotiationSkill = "ba:skill_negotiation";

        public static IModLogger Logger;

        /// <summary>
        /// What the city's economy adds to a business's footfall, in per cent of customers, whatever else is running
        /// there (set by Wiring/CityLinks; nothing while it is not set). It is asked each time the figure is written,
        /// kept inside <see cref="BusinessCatalogue.AmbientMostDip"/> and <see cref="BusinessCatalogue.AmbientMostBoost"/>.
        /// </summary>
        public static Func<BuildingRegistration, int> Ambient;

        /// <summary>The last business <see cref="Footfall"/> wrote: its own promotion figure and what was written.</summary>
        public static int LastOwn, LastWritten;

        private static readonly List<EmployeeInstance> StaffScratch = new List<EmployeeInstance>(16);

        // ---------------------------------------------------------------- which businesses

        /// <summary>A business of the player's that takes money: rented, named, of a type that generates revenue.</summary>
        public static bool IsBusiness(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || string.IsNullOrEmpty(reg.businessTypeName)
                    || reg.businessTypeName == EmptyType || !reg.HasValidAddress)
                {
                    return false;
                }
                BusinessType type = BusinessTypeHelper.GetData(reg);
                return type != null && type.HasTag(TagRef.Businesstag.generatesrevenue);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Businesses(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            List<BuildingRegistration> all = save.BuildingRegistrations;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsBusiness(all[i]))
                {
                    into.Add(all[i]);
                }
            }
        }

        /// <summary>How many revenue businesses the player runs. Asked when a job starts, never per frame.</summary>
        public static int RevenueBusinesses()
        {
            int count = 0;
            try
            {
                GameInstance save = SaveGameManager.Current;
                List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    if (IsBusiness(all[i]))
                    {
                        count++;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return count;
        }

        public static string Place(BuildingRegistration reg)
        {
            return BusinessState.PlaceKey(reg.StreetName, reg.StreetNumber);
        }

        /// <summary>The player's business at a remembered place, or null if it is no longer theirs (sold, closed down).</summary>
        public static BuildingRegistration Find(string place)
        {
            BuildingRegistration reg = At(place);
            return IsBusiness(reg) ? reg : null;
        }

        /// <summary>
        /// The player's building at a remembered place whatever business is in it, or null if it is no longer theirs.
        /// A closure is looked up by this and not by <see cref="Find"/>: the utilities close any business of the
        /// player's for want of power, water or a connection (a headquarters, a warehouse or a factory as well as a
        /// shop), and whatever was closed has to be found again to be opened.
        /// </summary>
        public static BuildingRegistration Held(string place)
        {
            BuildingRegistration reg = At(place);
            return reg != null && GoingPublic.Mine(reg) ? reg : null;
        }

        // The building registered at a remembered place, whoever has it; null if there is none.
        private static BuildingRegistration At(string place)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null || !BusinessState.SplitPlace(place, out string street, out int number))
            {
                return null;
            }
            List<BuildingRegistration> all = save.BuildingRegistrations;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.StreetNumber == number && reg.StreetName == street)
                {
                    return reg;
                }
            }
            return null;
        }

        public static string Name(BuildingRegistration reg)
        {
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : "?";
        }

        public static bool PlayerInside(BuildingRegistration reg)
        {
            return reg != null && BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null
                   && InstanceBehavior<BuildingManager>.Instance.buildingRegistration == reg;
        }

        public static bool IsFood(BuildingRegistration reg)
        {
            string type = reg.businessTypeName ?? string.Empty;
            return type.Contains("fastfood") || type.Contains("coffeeshop") || type.Contains("supermarket") || type.Contains("fruitandvegetable")
                   || type.Contains("liquorstore") || type.Contains("nightclub");
        }

        public static bool AllowsTheft(BuildingRegistration reg)
        {
            BusinessType type = BusinessTypeHelper.GetData(reg);
            return type != null && type.HasTag(TagRef.Businesstag.allowtheft);
        }

        /// <summary>
        /// A business whose customers follow a promotion figure the game recomputes itself (the building type has
        /// marketing promotion), open, and with no footfall change of the mod's already running. Only on these
        /// may the figure be touched: anywhere else the game would never put it back.
        /// </summary>
        public static bool HasFootfall(BuildingRegistration reg)
        {
            return Promoted(reg) && !reg.temporarilyClosed && BusinessState.EffectAt(Place(reg)) < 0;
        }

        /// <summary>
        /// True if the game itself recomputes this business's promotion figure (BusinessHelper.UpdatePromotion's own test)
        /// and its customers follow it. An Empire Motors dealership's do not: that mod makes up its customers itself
        /// (Core/EmpireMotors), so a campaign or a change of footfall would cost or say something and change nothing.
        /// </summary>
        public static bool Promoted(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || reg.promotion == null || !reg.HasValidAddress || EmpireMotors.IsDealership(reg))
                {
                    return false;
                }
                Buildings.Building building = BuildingHelper.GetBuilding(reg.Address);
                return building != null && Buildings.BuildingTypeHelper.GetData(building).HasTag(TagRef.Buildingtypetag.hasmarketingpromotion);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True if the business's own figure leaves room for a change of so many percent to show in full (the figure runs 0 to 100).</summary>
        public static bool FootfallRoom(BuildingRegistration reg, int percent)
        {
            if (!HasFootfall(reg))
            {
                return false;
            }
            float baseMultiplier = SaveGameManager.Current.gameVariables.baseCustomerPromotionMultiplier;
            int own = reg.promotion.total;
            int wanted = BusinessCatalogue.PromotionFor(own, percent, baseMultiplier);
            return percent < 0 ? wanted > 0 && wanted < own : wanted < 100 && wanted > own;
        }

        // ---------------------------------------------------------------- the figures

        public static float Cash => SaveGameManager.Current != null ? Mathf.Max(0f, SaveGameManager.Current.Money) : 0f;

        /// <summary>What the business took a day, on average over the last seven days it traded (the game's own order history). Zero if it has none.</summary>
        public static float DailyRevenue(BuildingRegistration reg)
        {
            List<OrderHistoryEntry> history = reg.orderHistory;
            if (history == null)
            {
                return 0f;
            }
            int from = TimeHelper.CurrentDay - 7;
            float total = 0f;
            int days = 0;
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i] != null && history[i].dayNumber >= from)
                {
                    total += Mathf.Max(0f, history[i].totalRevenue);
                    days++;
                }
            }
            return days > 0 ? total / days : 0f;
        }

        /// <summary>A charge for this business by the catalogue's rule: a share of its day, floor to ceiling, capped by the cash in hand.</summary>
        public static int Fee(BuildingRegistration reg, float share, int floor, int ceiling)
        {
            return BusinessCatalogue.Fee(DailyRevenue(reg), share, floor, ceiling, Cash);
        }

        public static int Cleanliness(BuildingRegistration reg)
        {
            try
            {
                return Mathf.RoundToInt(reg.GetCleanliness());
            }
            catch (Exception)
            {
                return 100;   // a business with no floor to dirty is clean
            }
        }

        public static int Satisfaction(BuildingRegistration reg)
        {
            return reg.satisfaction != null ? reg.satisfaction.overall : 50;
        }

        public static int Facility(BuildingRegistration reg)
        {
            return reg.satisfaction != null ? reg.satisfaction.facility : 50;
        }

        /// <summary>Which of the game's four satisfaction figures is lowest: 0 service, 1 pricing, 2 cleanliness, 3 facility.</summary>
        public static int Weakest(BuildingRegistration reg)
        {
            Satisfaction s = reg.satisfaction;
            if (s == null)
            {
                return 2;
            }
            int which = 0;
            int low = s.customerService;
            if (s.pricing < low) { low = s.pricing; which = 1; }
            if (s.cleanliness < low) { low = s.cleanliness; which = 2; }
            if (s.facility < low) { which = 3; }
            return which;
        }

        public static int Security(BuildingRegistration reg)
        {
            return Mathf.RoundToInt(Mathf.Clamp(reg.securityLevelPercentage, 0f, 100f));
        }

        public static bool IsOpenNow(BuildingRegistration reg)
        {
            try
            {
                return !reg.temporarilyClosed && BusinessHelper.IsBusinessOpen(reg);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>Takes a charge from the player, booked at the business. False (and nothing taken) if they cannot pay it.</summary>
        public static bool Charge(BuildingRegistration reg, int amount, string transactionKey, string category = null)
        {
            if (amount <= 0)
            {
                return true;
            }
            TransactionInfo info = category != null ? new TransactionInfo(transactionKey, category) : new TransactionInfo(transactionKey);
            bool paid = GameManager.ChangeMoneySafe(-amount, info, null, reg != null ? reg.Address : null, force: false);
            Logger?.Info("Business: " + (paid ? "charged " : "could not charge ") + amount + " (" + transactionKey + ") at " + Name(reg) + ".");
            return paid;
        }

        public static void Pay(BuildingRegistration reg, int amount, string transactionKey)
        {
            if (amount <= 0)
            {
                return;
            }
            GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, reg != null ? reg.Address : null, force: true);
            Logger?.Info("Business: paid " + amount + " (" + transactionKey + ") to " + Name(reg) + ".");
        }

        /// <summary>A loss booked as theft in the business's own books (the game clears the figure each night). No cash moves.</summary>
        public static void BookTheft(BuildingRegistration reg, int amount)
        {
            if (amount > 0)
            {
                reg.stolenItemsCost += amount;
                Logger?.Info("Business: " + amount + " booked as theft at " + Name(reg) + " (stolenItemsCost now " + reg.stolenItemsCost.ToString("F0") + ").");
            }
        }

        public static void UnbookTheft(BuildingRegistration reg, int amount)
        {
            if (amount > 0 && reg.stolenItemsCost >= amount)
            {
                reg.stolenItemsCost -= amount;
                Logger?.Info("Business: " + amount + " of booked theft taken back at " + Name(reg) + ".");
            }
        }

        // ---------------------------------------------------------------- footfall

        /// <summary>
        /// Puts a business's promotion at what its own figure would be, changed by so many percent of
        /// customers (zero: exactly its own). The game's figure is recomputed first, so repeated calls never
        /// add up. With <paramref name="regenerate"/> the customers still to come today are drawn again.
        /// </summary>
        public static bool Footfall(BuildingRegistration reg, int percent, bool regenerate)
        {
            try
            {
                if (!Promoted(reg))
                {
                    return false;   // the game would never recompute it: left alone
                }
                BusinessHelper.UpdatePromotion(reg);
                if (reg.promotion == null)
                {
                    return false;
                }
                int own = reg.promotion.total;
                int ambient = Ambient != null ? Ambient(reg) : 0;
                reg.promotion.total = BusinessCatalogue.Written(own, percent, ambient, SaveGameManager.Current.gameVariables.baseCustomerPromotionMultiplier);
                LastOwn = own;
                LastWritten = reg.promotion.total;
                if (regenerate)
                {
                    CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
                    Logger?.Info("Business: footfall at " + Name(reg) + " " + (percent >= 0 ? "+" : string.Empty) + percent + " %"
                                 + (ambient != 0 ? " and " + (ambient > 0 ? "+" : string.Empty) + ambient + " % from the city's economy" : string.Empty) + ": promotion " + own + " -> "
                                 + reg.promotion.total + ".");
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // ---------------------------------------------------------------- closure

        /// <summary>Closes a business with the game's own switch. False if it was closed already (it is then not ours to reopen).</summary>
        public static bool Close(BuildingRegistration reg)
        {
            if (reg.temporarilyClosed || InstanceBehavior<UI.UIs>.Instance == null)
            {
                return false;
            }
            reg.TemporarilyClose(true);
            Logger?.Info("Business: " + Name(reg) + " temporarily closed.");
            return true;
        }

        public static void Reopen(BuildingRegistration reg)
        {
            if (reg != null && reg.temporarilyClosed && InstanceBehavior<UI.UIs>.Instance != null)
            {
                reg.TemporarilyClose(false);
                Logger?.Info("Business: " + Name(reg) + " reopened.");
            }
        }

        /// <summary>
        /// True if the game gives a business its own "temporarily closed" switch, so that the player may have closed it
        /// by hand (the game's own test, BizManBusiness.ShouldShowTemporarilyClosedToggle: a type that generates
        /// revenue, or a factory). A headquarters or a warehouse has none: the game never closes either, and the player
        /// cannot open one.
        /// </summary>
        public static bool HasSwitch(BuildingRegistration reg)
        {
            try
            {
                BusinessType type = BusinessTypeHelper.GetData(reg);
                return type == null || type.HasTag(TagRef.Businesstag.generatesrevenue) || reg.businessTypeName == FactoryType;
            }
            catch (Exception)
            {
                return true;   // if it cannot be told, a closure is left as it is
            }
        }

        // ---------------------------------------------------------------- cleanliness

        /// <summary>A contract clean: every dirty spot down to a third of what it was.</summary>
        public static void DeepClean(BuildingRegistration reg)
        {
            List<DirtSpot> spots = reg.dirtSpots;
            if (spots == null)
            {
                return;
            }
            int before = Cleanliness(reg);
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i] != null && spots[i].dirtiness > 0f)
                {
                    spots[i].dirtiness *= 0.33f;
                }
            }
            if (PlayerInside(reg))
            {
                InstanceBehavior<BuildingManager>.Instance.UpdateDirtinessInCurrentBuilding();
            }
            Logger?.Info("Business: contract clean at " + Name(reg) + ": cleanliness " + before + " -> " + Cleanliness(reg) + ".");
        }

        // ---------------------------------------------------------------- staff

        /// <summary>The employees assigned to a business. A shared list: use it before asking again.</summary>
        public static List<EmployeeInstance> Staff(BuildingRegistration reg)
        {
            StaffScratch.Clear();
            try
            {
                EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address, excludeBeingReplaced = true }, StaffScratch);
                for (int i = StaffScratch.Count - 1; i >= 0; i--)
                {
                    EmployeeInstance e = StaffScratch[i];
                    if (e == null || e.IsCandidate || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0
                        || e.satisfaction <= 0f)
                    {
                        StaffScratch.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                StaffScratch.Clear();
            }
            return StaffScratch;
        }

        public static EmployeeInstance Employee(string id)
        {
            return string.IsNullOrEmpty(id) ? null : EmployeeHelper.GetEmployeeById(id, false);
        }

        public static string NameOf(EmployeeInstance e)
        {
            return e != null && e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?";
        }

        /// <summary>What the game's own wage formula gives for this employee's main skill.</summary>
        public static float ExpectedWage(EmployeeInstance e)
        {
            return EmployeeHelper.CalculateHourlyWageForSkill(e.characterData.skills[0]);
        }

        /// <summary>How far under the formula an employee is paid: 0.12 is twelve percent under; negative is over.</summary>
        public static float WageGap(EmployeeInstance e)
        {
            float expected = ExpectedWage(e);
            return expected > 0.01f ? 1f - e.hourlyWage / expected : 0f;
        }

        public static void SetWage(EmployeeInstance e, float wage)
        {
            float before = e.hourlyWage;
            e.hourlyWage = Mathf.Max(before, wage);   // a wage only ever goes up here
            Logger?.Info("Business: " + NameOf(e) + "'s wage " + before.ToString("F2") + " -> " + e.hourlyWage.ToString("F2") + ".");
        }

        public static void Mood(EmployeeInstance e, float change)
        {
            float before = e.satisfaction;
            e.satisfaction = Mathf.Clamp(before + change, Mathf.Min(before, BusinessCatalogue.SatisfactionFloor), 100f);
            Logger?.Info("Business: " + NameOf(e) + "'s satisfaction " + before.ToString("F0") + " -> " + e.satisfaction.ToString("F0") + ".");
        }

        /// <summary>The employee leaves the way the game has an employee leave whose complaint went unanswered.</summary>
        public static void Leave(EmployeeInstance e)
        {
            Logger?.Info("Business: " + NameOf(e) + " leaves the company (the game's own resignation).");
            e.poached = false;
            e.complaintData.isComplaining = false;
            e.satisfaction = 0f;
            if (!e.ReplaceAutomaticallyOnResign())
            {
                e.Resign();
            }
        }

        // ---------------------------------------------------------------- rivals

        /// <summary>True where one of the game's own special rivals is already active: the game is applying its own pressure there.</summary>
        public static bool GameRivalActive(BuildingRegistration reg)
        {
            try
            {
                if (!RivalsHelper.IsFeatureEnabled)
                {
                    return false;
                }
                SpecialRival rival = RivalsHelper.GetSpecialRivalByNeighborhood(reg.Neighborhood);
                SpecialRivalState state = rival != null && rival.rivalData != null ? RivalsHelper.GetSpecialRivalState(rival.rivalData.id) : null;
                return state != null && state.isActive && !state.isDefeated;
            }
            catch (Exception)
            {
                return true;   // if it cannot be told, leave the neighbourhood to the game
            }
        }

        /// <summary>A rival of the game's own with businesses to its name, to put a name to a move; null if there is none.</summary>
        public static RivalData AnyRival(BuildingRegistration reg)
        {
            try
            {
                if (!RivalsHelper.IsFeatureEnabled)
                {
                    return null;
                }
                SpecialRival local = RivalsHelper.GetSpecialRivalByNeighborhood(reg.Neighborhood);
                if (local != null && local.rivalData != null && local.rivalData.ownedBusinesses != null && local.rivalData.ownedBusinesses.Count > 0)
                {
                    return RivalsHelper.GetRivalData(local.rivalData.id);
                }
                List<RivalData> all = RivalsHelper.GetAllRivalData();
                int start = all.Count > 0 ? TimeHelper.CurrentDay % all.Count : 0;
                for (int i = 0; i < all.Count; i++)
                {
                    RivalData rival = all[(start + i) % all.Count];
                    if (rival != null && !string.IsNullOrEmpty(rival.id) && rival.ownedBusinesses != null && rival.ownedBusinesses.Count > 0)
                    {
                        return rival;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return null;
        }

        // ---------------------------------------------------------------- deliveries

        /// <summary>The business's next wholesale delivery that is still to come, or null.</summary>
        public static DeliveryContract NextDelivery(BuildingRegistration reg)
        {
            GameInstance save = SaveGameManager.Current;
            List<DeliveryContract> contracts = save != null ? save.DeliveryContracts : null;
            if (contracts == null)
            {
                return null;
            }
            int day = TimeHelper.CurrentDay;
            DeliveryContract best = null;
            for (int i = 0; i < contracts.Count; i++)
            {
                DeliveryContract c = contracts[i];
                if (c == null || !c.enabled || c.businessAddress == null || c.businessAddress != reg.Address || c.items == null || c.items.Count == 0)
                {
                    continue;
                }
                bool coming = c.nextDeliveryDay > day || (c.nextDeliveryDay == day && TimeHelper.CurrentHour < 7);
                if (coming && c.nextDeliveryDay <= day + 2 && (best == null || c.nextDeliveryDay < best.nextDeliveryDay))
                {
                    best = c;
                }
            }
            return best;
        }

        public static float DeliveryPrice(DeliveryContract contract)
        {
            try
            {
                return Mathf.Max(0f, contract.TotalPricePerDelivery);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        public static void Delay(BuildingRegistration reg, DeliveryContract contract)
        {
            if (contract != null)
            {
                contract.nextDeliveryDay += 1;
                Logger?.Info("Business: the delivery to " + Name(reg) + " now comes on day " + contract.nextDeliveryDay + ".");
            }
        }

        public static float NegotiationSkillValue()
        {
            try
            {
                EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
                return self != null && self.HasSkill(NegotiationSkill) ? self.GetSkillValue(NegotiationSkill) : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        public static void TrainNegotiation(float amount)
        {
            try
            {
                PlayerHelper.GetPlayerEmployeeInstance()?.IncreaseSkill(NegotiationSkill, amount);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
