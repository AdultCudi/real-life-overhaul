using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// catalogue, and writes one line to the log saying what was changed from what to what (the
    /// footfall of every feature's writes: one line a round, Core/CustomerDraw, and one for an
    /// issue's own change, <see cref="Said"/>):
    ///   money        GameManager.ChangeMoneySafe with the business's address, never forced;
    ///   footfall     the business's promotion figure (recomputed by the game daily and whenever
    ///                marketing changes, so it heals itself), then the game's own regeneration
    ///                of the customers still to come today (Core/CustomerDraw: once, spread over
    ///                frames within the hour, and not where they are drawn with the figure already);
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

        /// <summary>
        /// What the business's interior adds to its footfall, in per cent of customers (set by Wiring/InteriorLinks; nothing
        /// while it is not set): a part of its own beside the ambient one, kept inside <see cref="BusinessCatalogue.InteriorMostDip"/>
        /// and <see cref="BusinessCatalogue.InteriorMostBoost"/>, so that the decor's strongest choice is not lost under what
        /// the city's economy and the rest of the mod add.
        /// </summary>
        public static Func<BuildingRegistration, int> Interior;

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

        /// <summary>A business's place, as the save keeps it (BusinessState.PlaceKey). Made once a registration and kept while its street and number are the same: asked on every footfall write and look-up, it builds nothing then.</summary>
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

        // What was last written into a business's figure, and from what: while the game has not recomputed the figure (it
        // makes a new Promotion each time, BusinessHelper.UpdatePromotion) and nothing it was worked out from has moved,
        // writing it again would give the very same figure.
        private sealed class Wrote
        {
            public Promotion Promotion;
            public int Total, Own, Percent, Ambient, Interior;
            public float Multiplier;
        }

        private static readonly Dictionary<BuildingRegistration, Wrote> Written = new Dictionary<BuildingRegistration, Wrote>(64);
        private static GameInstance _writtenSave;   // the save those are of: another one starts afresh

        /// <summary>
        /// Puts a business's promotion at what its own figure would be, changed by so many percent of customers (zero:
        /// exactly its own), with whatever <see cref="Ambient"/> and <see cref="Interior"/> add. The game's figure is
        /// recomputed first, so repeated calls never add up, unless the figure is still the one this wrote last time, the
        /// game having recomputed nothing since, and every part is as it was then: then it is left as it stands (the hourly
        /// round over every business, which only has to put the figure back where the game wrote its own over it, costs a
        /// few look-ups a business). With <paramref name="regenerate"/> the customers still to come today are drawn again
        /// with the figure (Core/CustomerDraw: once, spread, within the hour; not at all if they are drawn with it already).
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
                LastOwn = w.Own;
                LastWritten = w.Total;
                if (regenerate)
                {
                    CustomerDraw.Rewritten(reg);   // the game's own redraw (Core/CustomerDraw keeps the hour under way while Meaningful Ambience is on)
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // Another save (a load): what was kept of the last one's registrations is dropped.
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

        /// <summary>One line for a change of business pressure's own at a business (an issue's footfall begun or ended): what was written, from what.</summary>
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

        // ---------------------------------------------------------------- closure

        /// <summary>
        /// Closes a business of the player's with the game's own switch. False if it was closed already (it is then not
        /// ours to reopen), or if the building is not the player's (sold with its lease, handed back, another player's).
        /// </summary>
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
        /// A building the mod had closed, let go by the player since (its lease ended, it was sold with it): the game's
        /// switch the mod set is cleared, as the game's own hand-over clears a tenant's fields, so the closure does not
        /// pass to whoever has it next. Only while nobody rents it: in a Going Public session another player's building is
        /// rented on this machine, and is never touched. The field is set directly, as selling a business does
        /// (ServicesGame.Sale): TemporarilyClose would charge the player the licensing fees of a building not theirs.
        /// True if it was cleared.
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

        /// <summary>A wage brought up to a figure (a strike settled with a raise): never down, never past WageRules' cap of the game's own wage for the skill.</summary>
        public static void SetWage(EmployeeInstance e, float wage)
        {
            float before = e.hourlyWage;
            int formula = Mathf.RoundToInt(ExpectedWage(e) * 100f);
            e.hourlyWage = WageRules.UpTo(Mathf.RoundToInt(before * 100f), Mathf.RoundToInt(wage * 100f), formula) / 100f;   // a wage only ever goes up here, and never past the cap
            Logger?.Info("Business: " + NameOf(e) + "'s wage " + before.ToString("F2", CultureInfo.InvariantCulture) + " -> " + e.hourlyWage.ToString("F2", CultureInfo.InvariantCulture) + ".");
        }

        public static void Mood(EmployeeInstance e, float change)
        {
            float before = e.satisfaction;
            e.satisfaction = Mathf.Clamp(before + change, Mathf.Min(before, BusinessCatalogue.SatisfactionFloor), 100f);
            Logger?.Info("Business: " + NameOf(e) + "'s satisfaction " + before.ToString("F0") + " -> " + e.satisfaction.ToString("F0") + ".");
        }

        /// <summary>
        /// The employee leaves the way the game has an employee leave whose complaint went unanswered. A rival's offer
        /// to them loses its buttons at once (Core/RivalOfferGuard): the game sends nothing to their chat until their
        /// next hour.
        /// </summary>
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
            RivalOfferGuard.Look("an employee let go by the game's own resignation");
        }

        /// <summary>
        /// Whether the employee an offer was made to is still the player's to keep, by the rule that takes a rival
        /// offer's buttons (Core/RivalOfferRules.Standing): gone, replaced by the headhunter, resigned, or theirs.
        /// </summary>
        public static RivalOfferRules.State Standing(EmployeeInstance e, string name)
        {
            return RivalOfferGuard.Standing(e, name);
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
