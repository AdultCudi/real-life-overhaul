using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using BigAmbitions.Rivals;
using BigAmbitions.Tags;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything business pressure reads from or does to the game. Each change goes through the field or method the game
    /// uses for the same thing, bounded by the catalogue, with one log line: money by ChangeMoneySafe (never forced), footfall
    /// by the promotion figure the game recomputes daily, a closure by the business's own switch, theft by stolenItemsCost
    /// (cleared each night), wages as the game's counter-offer writes them, leaving through Core/StaffLeave. No timer of its own.
    /// </summary>
    internal static partial class BusinessGame
    {
        public const string Marketing = "ba:transactioncategory_marketing";
        public const string Licensing = "ba:transactioncategory_licensingfees";
        public const string Salaries = "ba:transactioncategory_salaryexpenses";
        private const string EmptyType = "ba:businesstype_empty";
        private const string FactoryType = "ba:businesstype_factory";
        private const string NegotiationSkill = "ba:skill_negotiation";

        public static IModLogger Logger;

        /// <summary>What the city's economy adds to a business's footfall, in percent, asked at each write (set by Wiring/CityLinks).</summary>
        public static Func<BuildingRegistration, int> Ambient;

        /// <summary>What the business's interior adds, in percent, a part with bounds of its own (set by Wiring/InteriorLinks).</summary>
        public static Func<BuildingRegistration, int> Interior;

        /// <summary>The last business <see cref="Footfall"/> wrote: its own promotion figure and what was written.</summary>
        public static int LastOwn, LastWritten;

        private static readonly List<EmployeeInstance> StaffScratch = new List<EmployeeInstance>(16);

        /// <summary>A business of the player's that takes money: rented, named, of a type that generates revenue.</summary>
        public static bool IsBusiness(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || string.IsNullOrEmpty(reg.businessTypeName)
                    || reg.businessTypeName == EmptyType || !reg.HasValidAddress || !GameRegistrations.Found(reg))
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

        /// <summary>A business's place as the save keeps it, made once a registration and kept while its address stands.</summary>
        public static string Place(BuildingRegistration reg)
        {
            string street = reg.StreetName;
            int number = reg.StreetNumber;
            Fresh();
            if (Places.TryGetValue(reg, out PlaceKey known) && known.Number == number && (ReferenceEquals(known.Street, street) || string.Equals(known.Street, street, StringComparison.Ordinal)))
            {
                return known.Key;
            }
            string key = BusinessState.PlaceKey(street, number);
            Places[reg] = new PlaceKey { Street = street, Number = number, Key = key };
            return key;
        }

        private sealed class PlaceKey
        {
            public string Street, Key;
            public int Number;
        }

        private static readonly Dictionary<BuildingRegistration, PlaceKey> Places = new Dictionary<BuildingRegistration, PlaceKey>(64);

        /// <summary>The player's business at a remembered place, or null if it is no longer theirs (sold, closed down).</summary>
        public static BuildingRegistration Find(string place)
        {
            BuildingRegistration reg = At(place);
            return IsBusiness(reg) ? reg : null;
        }

        /// <summary>The player's building at a place whatever is in it, or null: closures use it, as the utilities close a headquarters too.</summary>
        public static BuildingRegistration Held(string place)
        {
            BuildingRegistration reg = At(place);
            return reg != null && GoingPublic.Mine(reg) ? reg : null;
        }

        // The registration at a place, whoever has it.
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
                   || type.Contains("liquorstore") || type.Contains("nightclub") || type.Contains("icecreamparlour") || type.Contains("teabar");   // the last two: Scripts/Shops
        }

        public static bool AllowsTheft(BuildingRegistration reg)
        {
            BusinessType type = BusinessTypeHelper.GetData(reg);
            return type != null && type.HasTag(TagRef.Businesstag.allowtheft);
        }

        /// <summary>Promoted, open and with no footfall change of the mod's running: only there may the figure be touched.</summary>
        public static bool HasFootfall(BuildingRegistration reg)
        {
            return Promoted(reg) && !reg.temporarilyClosed && BusinessState.EffectAt(Place(reg)) < 0;
        }

        /// <summary>True if the game recomputes the business's promotion figure and its customers follow it (not an Empire Motors dealership).</summary>
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

        /// <summary>True if the figure (0 to 100) leaves room for a change of so many percent to show in full.</summary>
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

        public static float Cash => SaveGameManager.Current != null ? Mathf.Max(0f, SaveGameManager.Current.Money) : 0f;

        /// <summary>What the business took a day on average over the last seven days it traded, from the game's order history; zero if none.</summary>
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

        /// <summary>Takes a charge from the player, booked at the business. False (and nothing taken) if they cannot pay it.</summary>
        public static bool Charge(BuildingRegistration reg, int amount, string transactionKey, string category = null)
        {
            if (amount <= 0)
            {
                return true;
            }
            TransactionInfo info = category != null ? new TransactionInfo(transactionKey, category) : new TransactionInfo(transactionKey);
            bool paid = RealLifeOverhaul.Economy.BizBooks.Book(-amount, info, null, reg != null ? reg.Address : null, force: false);
            Logger?.Info("Business: " + (paid ? "charged " : "could not charge ") + amount + " (" + transactionKey + ") at " + Name(reg) + ".");
            return paid;
        }

        public static void Pay(BuildingRegistration reg, int amount, string transactionKey)
        {
            if (amount <= 0)
            {
                return;
            }
            RealLifeOverhaul.Economy.BizBooks.Book(amount, new TransactionInfo(transactionKey), null, reg != null ? reg.Address : null, force: true);
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

        // What was last written into a business's figure, and from what. The game makes a new Promotion each time it
        // recomputes, so while that object and every part are the same, writing again would give the same figure.
        private sealed class Wrote
        {
            public Promotion Promotion;
            public int Total, Own, Percent, Ambient, Interior;
            public float Multiplier;
            /// <summary>
            /// The figures of active play, kept while a save puts the game's own back; a few copies each time a figure
            /// is written.
            /// </summary>
            public readonly CompanionPromotion Play = new CompanionPromotion();
        }

        private static readonly Dictionary<BuildingRegistration, Wrote> Written = new Dictionary<BuildingRegistration, Wrote>(64);
        private static GameInstance _writtenSave;   // the save those are of: another one starts afresh

        /// <summary>
        /// Puts a business's promotion at its own figure changed by so many percent, plus <see cref="Ambient"/> and
        /// <see cref="Interior"/>. The game's figure is recomputed first so calls never add up, unless nothing has moved
        /// since the last write. With <paramref name="regenerate"/> today's customers still to come are drawn again
        /// (Core/CustomerDraw).
        /// </summary>
        public static bool Footfall(BuildingRegistration reg, int percent, bool regenerate)
        {
            try
            {
                if (!Promoted(reg))
                {
                    return false;   // the game would never recompute it: left alone
                }
                int ambient = Ambient != null ? Ambient(reg) : 0;
                int interior = Interior != null ? Interior(reg) : 0;
                float multiplier = SaveGameManager.Current.gameVariables.baseCustomerPromotionMultiplier;
                Wrote w = Last(reg);
                if (w.Promotion == null || !ReferenceEquals(w.Promotion, reg.promotion) || reg.promotion.total != w.Total || w.Percent != percent || w.Ambient != ambient
                    || w.Interior != interior || w.Multiplier != multiplier)
                {
                    CustomerDraw.Writing(reg);
                    BusinessHelper.UpdatePromotion(reg);
                    if (reg.promotion == null)
                    {
                        w.Promotion = null;
                        return false;
                    }
                    w.Own = reg.promotion.total;
                    reg.promotion.total = BusinessCatalogue.Written(w.Own, percent, ambient, interior, multiplier);
                    w.Promotion = reg.promotion;
                    w.Total = reg.promotion.total;
                    w.Percent = percent;
                    w.Ambient = ambient;
                    w.Interior = interior;
                    w.Multiplier = multiplier;
                }
                w.Play.Remember(SaveHook.Saving, w.Own, w.Total, w.Percent, w.Ambient, w.Interior, w.Multiplier);
                LastOwn = w.Own;
                LastWritten = w.Total;
                if (regenerate)
                {
                    CustomerDraw.Rewritten(reg);
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // A load drops what was kept of the last save's registrations.
        private static void Fresh()
        {
            GameInstance save = SaveGameManager.Current;
            if (!ReferenceEquals(save, _writtenSave))
            {
                Written.Clear();
                Places.Clear();
                _writtenSave = save;
            }
        }

        private static Wrote Last(BuildingRegistration reg)
        {
            Fresh();
            if (!Written.TryGetValue(reg, out Wrote w))
            {
                w = new Wrote();
                Written[reg] = w;
            }
            return w;
        }

        /// <summary>Logs an issue's own footfall change: what was written, from what.</summary>
        public static void Said(BuildingRegistration reg, int percent)
        {
            Wrote w = reg != null && Written.TryGetValue(reg, out Wrote known) ? known : null;
            if (w == null || w.Promotion == null)
            {
                return;
            }
            Logger?.Info("Business: footfall at " + Name(reg) + " " + (percent >= 0 ? "+" : string.Empty) + percent + " %"
                         + (w.Ambient != 0 ? " and " + (w.Ambient > 0 ? "+" : string.Empty) + w.Ambient + " % from the city's economy" : string.Empty)
                         + (w.Interior != 0 ? " and " + (w.Interior > 0 ? "+" : string.Empty) + w.Interior + " % from its interior" : string.Empty) + ": promotion " + w.Own + " -> "
                         + w.Total + ".");
        }

        /// <summary>Closes a business with the game's own switch. False if it was closed already (not ours to reopen) or is not the player's.</summary>
        public static bool Close(BuildingRegistration reg)
        {
            if (!GoingPublic.Mine(reg) || reg.temporarilyClosed || InstanceBehavior<UI.UIs>.Instance == null)
            {
                return false;
            }
            reg.TemporarilyClose(true);
            Logger?.Info("Business: " + Name(reg) + " temporarily closed.");
            return true;
        }

        /// <summary>
        /// Clears the mod's switch on a building let go while nobody rents it (another player's is rented here), set directly
        /// as a sale does: TemporarilyClose would charge licensing fees. True if cleared.
        /// </summary>
        public static bool Freed(string place)
        {
            BuildingRegistration reg = At(place);
            if (reg == null || reg.RentedByPlayer || !reg.temporarilyClosed)
            {
                return false;
            }
            reg.temporarilyClosed = false;
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

        /// <summary>True if the game gives the business a "temporarily closed" switch: a revenue type or a factory, as BizMan tests.</summary>
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

        /// <summary>A wage brought up to a figure: never down, never past WageRules' cap.</summary>
        public static void SetWage(EmployeeInstance e, float wage)
        {
            float before = e.hourlyWage;
            int formula = Mathf.RoundToInt(ExpectedWage(e) * 100f);
            e.hourlyWage = WageRules.UpTo(Mathf.RoundToInt(before * 100f), Mathf.RoundToInt(wage * 100f), formula) / 100f;
            Logger?.Info("Business: " + NameOf(e) + "'s wage " + before.ToString("F2", CultureInfo.InvariantCulture) + " -> " + e.hourlyWage.ToString("F2", CultureInfo.InvariantCulture) + ".");
        }

        public static void Mood(EmployeeInstance e, float change)
        {
            float before = e.satisfaction;
            e.satisfaction = Mathf.Clamp(before + change, Mathf.Min(before, BusinessCatalogue.SatisfactionFloor), 100f);
            Logger?.Info("Business: " + NameOf(e) + "'s satisfaction " + before.ToString("F0") + " -> " + e.satisfaction.ToString("F0") + ".");
        }

        /// <summary>The employee resigns the game's way, through Core/StaffLeave. False, with nothing changed, if not the player's to let go.</summary>
        public static bool Leave(EmployeeInstance e)
        {
            return StaffLeave.Resign(e, "business pressure");
        }

        /// <summary>Whether the employee an offer was made to is still the player's, by the rival offer's own rule.</summary>
        public static RivalOfferRules.State Standing(EmployeeInstance e, string name)
        {
            return RivalOfferGuard.Standing(e, name);
        }

        public static bool GameRivalActive(BuildingRegistration reg)
        {
            try
            {
                if (!RivalsHelper.IsFeatureEnabled)
                {
                    return false;
                }
                string district = GameRegistrations.District(reg);
                if (district == null)
                {
                    return true;   // as when it cannot be told: the game's own rule decides
                }
                SpecialRival rival = RivalsHelper.GetSpecialRivalByNeighborhood(district);
                SpecialRivalState state = rival != null && rival.rivalData != null ? RivalsHelper.GetSpecialRivalState(rival.rivalData.id) : null;
                return state != null && state.isActive && !state.isDefeated;
            }
            catch (Exception)
            {
                return true;   // if it cannot be told, leave the neighbourhood to the game
            }
        }

        /// <summary>A rival of the game's with businesses to its name, to name a move after; null if none.</summary>
        public static RivalData AnyRival(BuildingRegistration reg)
        {
            try
            {
                if (!RivalsHelper.IsFeatureEnabled)
                {
                    return null;
                }
                string district = GameRegistrations.District(reg);
                SpecialRival local = district != null ? RivalsHelper.GetSpecialRivalByNeighborhood(district) : null;
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
