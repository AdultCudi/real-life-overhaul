using System;
using System.Collections.Generic;
using BigAmbitions.Characters.Skills;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What running a business asks of the rest of the mod (Wiring/CompanyLinks); null does nothing.</summary>
    public static class CompanyBridges
    {
        /// <summary>Writes a business's promotion figure again as it should stand, by business pressure's writer.</summary>
        public static CityBridges.FootfallWriter Footfall;

        /// <summary>True where the game keeps a promotion figure for a business.</summary>
        public static Func<BuildingRegistration, bool> Promoted;

        /// <summary>Shuts a business for some hours by business pressure's closure, which reopens it; false if it would not.</summary>
        public static Func<BuildingRegistration, int, bool> Close;

        /// <summary>Starts business pressure's tax audit at a business, by its own rules.</summary>
        public static Func<BuildingRegistration, bool> Audit;

        /// <summary>Starts one of business pressure's inspections at a business, by its own rules.</summary>
        public static Func<BuildingRegistration, bool> Inspect;

        /// <summary>True while business pressure has a staff dispute open at a business: the company's strike waits.</summary>
        public static Func<BuildingRegistration, bool> Dispute;

        /// <summary>A rival's name to put to an offer; null for none.</summary>
        public static Func<BuildingRegistration, string> Rival;

        /// <summary>An employee leaves by the game's own resignation, once, as business pressure has them leave; false if they could not be let go.</summary>
        public static Func<EmployeeInstance, bool> Leave;

        /// <summary>True while the tech sector has its own rival's offer or a departure in hand for an employee (the game's id): the company makes no offer then.</summary>
        public static Func<string, bool> Held;

        internal static bool HeldAt(string id)
        {
            try
            {
                return Held != null && !string.IsNullOrEmpty(id) && Held(id);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Files a criminal case for tax kept back; the court's number, or 0 with no court.</summary>
        public static Func<int, int> TaxCase;

        /// <summary>Per cent of footfall one of business pressure's issues changes at a business on a day; 0 with none.</summary>
        public static Func<BuildingRegistration, int, int> IssueFootfall;

        /// <summary>Income of the days from one to another, both counted, that no statement holds: the gain on a business sold.</summary>
        public static Func<int, int, int> OtherIncome;

        /// <summary>True where the cleaning service cleans a business, so the rota planner wants no cleaning shift there.</summary>
        public static Func<Address, bool> Cleaned;

        /// <summary>Whether a kind not the company's own can be booked at a place, with cost and reach in thousandths.</summary>
        public static OutsideQuote Outside;

        public delegate bool OutsideQuote(int kind, string place, out int cost, out int reach);

        /// <summary>A campaign not of the company's own was booked and paid: place, kind, first and last day, per cent.</summary>
        public static Action<string, int, int, int, int> Booked;

        /// <summary>The city's view of the player as an employer, -10 to 10: a newcomer's standing, and rivals' approaches.</summary>
        public static Func<int> Esteem;

        /// <summary>Points the workplace's comfort raises the level staff paid fairly settle at, 0 to 10.</summary>
        public static Func<string, int> Comfort;

        internal static int ComfortAt(string place)
        {
            try
            {
                return Comfort != null ? CompanyCatalogue.Clamp(Comfort(place), 0, 10) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The Overview's line on the company and the market (Markets/Listing); null: no line and no button.</summary>
        public static Func<string> ListingLine;

        public static Action OpenListing;

        /// <summary>Writes a shop's price of a product as the game's price cell does (place, product, price, district); false if it did not change.</summary>
        public static Func<BuildingRegistration, string, float, string, bool> WritePrice;

        /// <summary>True where a chain or a brand keeps a shop's prices (by its place): a manager leaves them alone.</summary>
        public static Func<string, bool> PricesKept;

        internal static bool PricesKeptAt(string place)
        {
            try
            {
                return PricesKept != null && !string.IsNullOrEmpty(place) && PricesKept(place);
            }
            catch (Exception)
            {
                return true;   // cannot tell: left alone
            }
        }

        internal static int EsteemNow()
        {
            try
            {
                return Esteem != null ? CompanyCatalogue.Clamp(Esteem(), -CompanyCatalogue.EsteemMost, CompanyCatalogue.EsteemMost) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The player's professionals into a list, from the professions; false while they are off (the seats then wait).</summary>
        public static Func<List<ProfessionalHand>, bool> Professionals;

        /// <summary>For a test: a professional of a role and level joins the professions' own payroll at a place; false if they could not.</summary>
        public static Func<string, int, int, bool> HireProfessional;

        /// <summary>What a rival buying up a district adds to the rises landlords ask at a lease's renewal there, per mille; 0 where none does.</summary>
        public static Func<string, int> Pressure;

        /// <summary>
        /// The one function the property feature feeds: the market rent a day for a lease's premises (the district, the place and the game's rent a day now), whole
        /// money; 0 or null where it knows none. A landlord's offer moves toward it (LeaseRules.Offer); the city's rise ring stays the lease rules'.
        /// </summary>
        public static Func<string, string, int, int> MarketRent;

        public static void Reset()
        {
            Pressure = null;
            MarketRent = null;
            Professionals = null;
            HireProfessional = null;
            Comfort = null;
            Footfall = null;
            Promoted = null;
            Close = null;
            Audit = null;
            Inspect = null;
            Dispute = null;
            Rival = null;
            Leave = null;
            Held = null;
            TaxCase = null;
            IssueFootfall = null;
            OtherIncome = null;
            Cleaned = null;
            Outside = null;
            Booked = null;
            Esteem = null;
            ListingLine = null;
            OpenListing = null;
            WritePrice = null;
            PricesKept = null;
        }
    }

    /// <summary>One of the player's businesses as the company sees it today: its file and what was read from the game.</summary>
    public sealed class CompanyShop
    {
        public ShopFile File;
        public BuildingRegistration Reg;
        public Address Address;
        public string Name = string.Empty;
        public string District;
        public int Income = CompanyCatalogue.IncomeMiddle;
        public int Needs;               // the licences it needs, a bit each
        public int Drivers;             // delivery drivers on its staff, as last read: a delivery vehicle permit
        public int Taste;               // +1 sells to the well-off, -1 sells on price, 0 neither
        public int Average;             // an average day's takings over the statements of the last seven days
        public int Statements;          // of those days, the statements the game has
        public int Takings, Profit;     // yesterday's, by the game's statement
        public bool HasBooks;           // the game has a statement for yesterday
        public int Rent;                // the game's rent a day at the premises, whole money; 0 where the player owns the building
        public int Staff, Low, Standing, Sick;   // staff, those standing low, their standing summed, those off sick
        public int Asked;               // per cent of customers the company asks for here now
        public bool Applied;            // something of the company's is in the game's figure
    }

    /// <summary>
    /// Everything running a business reads from or does to the game. It writes money only, as transactions, never a
    /// wage, a schedule or a business's figure. Runs at the start, the hour that opens a day, the day and the app's opening.
    /// </summary>
    internal static partial class CompanyGame
    {
        public const string RaiseKey = "reallife_transaction_co_raise";
        public const string ReserveKey = "reallife_transaction_co_reserve";       // money set aside for the year's tax bill: out of the cash, no cost, no deduction
        public const string ReserveBackKey = "reallife_transaction_co_reserve_back";   // the reserve back in the cash
        public const string LeaseKey = "reallife_transaction_co_lease";   // the rise a lease's renewal added to the game's rent: a business cost, deductible
        public const string BonusKey = "reallife_transaction_co_bonus";   // a one-off bonus in place of a raise: a wage cost, deductible
        public const string SickKey = "reallife_transaction_co_sick";
        public const string OvertimeKey = "reallife_transaction_co_overtime";
        public const string StrikeKey = "reallife_transaction_co_strike";
        public const string PremiumKey = "reallife_transaction_co_premium";
        public const string ClaimKey = "reallife_transaction_co_claim";
        public const string CampaignKey = "reallife_transaction_co_campaign";
        public const string DiscountKey = "reallife_transaction_co_discount";
        public const string PenaltyKey = "reallife_transaction_co_penalty";
        public const string ErrorKey = "reallife_transaction_co_error";
        public const string AccountantKey = "reallife_transaction_co_accountant";
        public const string RebateKey = "reallife_transaction_co_rebate";
        public const string HiddenKey = "reallife_transaction_co_hidden";
        public const string BackTaxKey = "reallife_transaction_co_backtax";
        public const string LicenceKey = "reallife_transaction_co_licence";
        public const string LicenceFineKey = "reallife_transaction_co_licence_fine";
        public const string ManagerKey = "reallife_transaction_co_manager";
        public const string ExecutiveKey = "reallife_transaction_co_executive";   // an executive's allowance on top of their professional's pay

        // The game's business types by what they sell, as the end of their names ("ba:businesstype_...").
        private static readonly string[] FoodTypes = { "fastfoodrestaurant", "coffeeshop", "supermarket", "fruitandvegetablestore", "icecreamparlour", "teabar" };   // the last two: Scripts/Shops
        private static readonly string[] AlcoholTypes = { "liquorstore", "nightclub" };
        private static readonly string[] CrowdTypes = { "nightclub", "cinema", "theater", "casino" };                                  // many people in one room: a fire safety certificate
        // tables: an outdoor seating permit and a fire safety certificate; the last two: Scripts/Shops
        private static readonly string[] SeatingTypes = { "coffeeshop", "fastfoodrestaurant", "teabar", "icecreamparlour" };
        private static readonly string[] CellarTypes = { "warehouse" };                                                                // goods held in bulk: bonded storage
        private static readonly string[] WellOffTypes = { "jewelrystore", "electronicsstore", "cardealership", "furniturestore", "clothingstore", "nightclub" };
        private static readonly string[] PriceTypes = { "supermarket", "fastfoodrestaurant", "fruitandvegetablestore", "liquorstore", "gasstation", "giftshop", "tobacconist" };
        private const string DeliveryDriver = "ba:skill_deliverydriver";
        private const int DriverHours = 570;    // hundredths: the fixed day the game pays a delivery driver (EmployeeInstance.PayWage)

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly Dictionary<string, StaffFile> ById = new Dictionary<string, StaffFile>(64);

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? WholeMoney.Floor(Save.Money) : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        public static int TaxPercent => Save != null && Save.gameVariables != null ? Mathf.Max(0, Save.gameVariables.taxPercentage) : 0;

        /// <summary>A business by its address, written as business pressure writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The player's businesses today, each with its file; a file whose business is gone is dropped. A new file holds a
        /// first term of the licences free when <paramref name="ranBefore"/> (the feature is new to the save), else a grace.
        /// </summary>
        public static void ReadShops(CompanyState state, List<CompanyShop> into, List<CompanyShop> pool, int day, bool ranBefore, out int added, out int dropped)
        {
            added = dropped = 0;
            for (int i = 0; i < into.Count; i++)
            {
                into[i].Reg = null;
                into[i].File = null;
                pool.Add(into[i]);
            }
            into.Clear();
            for (int i = 0; i < state.Shops.Count; i++)
            {
                state.Shops[i].Seen = false;
            }
            Books.Businesses(Regs);
            for (int i = 0; i < Regs.Count; i++)
            {
                BuildingRegistration reg = Regs[i];
                string place = Place(reg);
                ShopFile file = state.Shop(place);
                bool firstSight = file == null;
                if (firstSight)
                {
                    if (state.Shops.Count >= CompanyCatalogue.ShopsMost)
                    {
                        continue;
                    }
                    file = new ShopFile { Place = place, FirstDay = day };
                    state.Shops.Add(file);
                    added++;
                }
                file.Seen = true;
                CompanyShop shop;
                if (pool.Count > 0)
                {
                    shop = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                else
                {
                    shop = new CompanyShop();
                }
                string type = reg.businessTypeName ?? string.Empty;
                shop.File = file;
                shop.Reg = reg;
                shop.Name = string.IsNullOrEmpty(reg.BusinessName) ? "?" : reg.BusinessName;
                shop.Address = reg.Address;
                shop.District = GameRegistrations.District(reg);
                shop.Rent = reg.BuildingOwnedByPlayer ? 0 : Mathf.Max(0, Mathf.RoundToInt(reg.RentPerDay));
                shop.Income = Mathf.RoundToInt(CityEconomy.DistrictIncome(shop.District));
                shop.Drivers = 0;   // counted again when the staff are read, which follows
                shop.Needs = LicenceRules.Needs(new LicenceSite
                {
                    Food = Is(type, FoodTypes), Alcohol = Is(type, AlcoholTypes), Late = OpensLate(reg), Crowd = Is(type, CrowdTypes), Seating = Is(type, SeatingTypes),
                    Cellar = Is(type, CellarTypes)
                });
                shop.Taste = Is(type, WellOffTypes) ? 1 : Is(type, PriceTypes) ? -1 : 0;
                shop.Staff = shop.Low = shop.Standing = shop.Sick = 0;
                shop.Asked = 0;
                shop.Applied = false;
                if (firstSight)
                {
                    LicenceRules.First(file, shop.Needs, day, ranBefore);
                }
                else
                {
                    LicenceRules.Newly(file, shop.Needs, day);
                }
                into.Add(shop);
            }
            Regs.Clear();
            for (int i = state.Shops.Count - 1; i >= 0; i--)
            {
                if (!state.Shops[i].Seen)
                {
                    state.Shops.RemoveAt(i);
                    dropped++;
                }
            }
        }

        private static bool Is(string type, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (type.EndsWith(names[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool OpensLate(BuildingRegistration reg)
        {
            List<ScheduleDay> week = reg.scheduleDays;
            for (int d = 0; week != null && d < week.Count; d++)
            {
                ScheduleDay day = week[d];
                for (int s = 0; day != null && day.isOpen && day.openingHourSlots != null && s < day.openingHourSlots.Count; s++)
                {
                    OpeningHourSlot slot = day.openingHourSlots[s];
                    if (slot != null && LicenceRules.IsLate(slot.startingHour, slot.endingHour))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Yesterday's takings and profit of each business, and its average day over the last seven days' statements: over
        /// as many as there are, so that a business younger than a week is not taken to have taken nothing before it opened.
        /// Read afresh each time.
        /// </summary>
        public static void ReadBooks(List<CompanyShop> shops, int today)
        {
            for (int i = 0; i < shops.Count; i++)
            {
                shops[i].Average = shops[i].Takings = shops[i].Profit = shops[i].Statements = 0;
                shops[i].HasBooks = false;
            }
            GameInstance save = Save;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            if (summaries == null || shops.Count == 0)
            {
                return;
            }
            int from = today - 7, looked = 0;
            IndexShops(shops);
            for (int i = summaries.Count - 1; i >= 0 && looked < CompanyCatalogue.BooksDays; i--, looked++)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber >= today)
                {
                    continue;
                }
                if (summary.dayNumber < from)
                {
                    break;   // the game keeps them oldest first
                }
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    CompanyShop shop = statement != null ? ShopOf(statement.Address) : null;
                    if (shop == null)
                    {
                        continue;
                    }
                    shop.Average += Mathf.RoundToInt(Mathf.Max(0f, statement.TotalSales));
                    shop.Statements++;
                    if (summary.dayNumber == today - 1)
                    {
                        shop.Takings = Mathf.RoundToInt(Mathf.Max(0f, statement.TotalSales));
                        shop.Profit = Mathf.RoundToInt(statement.TotalProfit);
                        shop.HasBooks = true;
                    }
                }
            }
            for (int i = 0; i < shops.Count; i++)
            {
                shops[i].Average = CompanyCatalogue.DayAverage(shops[i].Average, shops[i].Statements);
            }
        }

        // The businesses of a read by address, the first at each, so a statement or an employee is a look-up.
        private static readonly Dictionary<Address, CompanyShop> ByAddress = new Dictionary<Address, CompanyShop>(64);

        private static void IndexShops(List<CompanyShop> shops)
        {
            ByAddress.Clear();
            for (int i = 0; i < shops.Count; i++)
            {
                Address a = shops[i].Address;
                if (a != null && !ByAddress.ContainsKey(a))   // Address's != also refuses the game's "no address"
                {
                    ByAddress.Add(a, shops[i]);
                }
            }
        }

        private static CompanyShop ShopOf(Address address)
        {
            return !ReferenceEquals(address, null) && ByAddress.TryGetValue(address, out CompanyShop shop) ? shop : null;
        }

        /// <summary>A business's customers, takings and profit on a day; false if the game has no statement for it.</summary>
        public static bool DayBook(CompanyShop shop, int day, out int customers, out int takings, out int profit)
        {
            customers = takings = profit = 0;
            GameInstance save = Save;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            bool found = false;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0 && !found; i--)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber > day)
                {
                    continue;
                }
                if (summary.dayNumber < day)
                {
                    break;
                }
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    if (statement != null && shop.Address != null && statement.Address == shop.Address)
                    {
                        takings = Mathf.RoundToInt(statement.TotalSales);
                        profit = Mathf.RoundToInt(statement.TotalProfit);
                        found = true;
                        break;
                    }
                }
            }
            List<OrderHistoryEntry> history = found && shop.Reg != null ? shop.Reg.orderHistory : null;
            for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
            {
                if (history[i] != null && history[i].dayNumber == day)
                {
                    customers = history[i].totalCustomers;
                    break;
                }
            }
            return found;
        }

        /// <summary>What every business took between two days, by the game's own sum (the one its tax uses).</summary>
        public static int Sales(int fromDay, int toDay)
        {
            try
            {
                return Save != null && Save.financialSummaries != null
                    ? Mathf.RoundToInt(Mathf.Min(TaxCalculationHelper.GetBusinessSales(fromDay, toDay), int.MaxValue / 4))
                    : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Whether the last year's income passes the game's own threshold, so its yearly tax falls due.</summary>
        public static bool Taxed(int today)
        {
            try
            {
                return Save != null && TaxHelper.MeetsIncomeTaxThreshold(TaxCalculationHelper.GetBusinessSales(today - DaysPerYear + 1, today),
                    TaxCalculationHelper.GetCurrentTaxableGamblingWinnings());
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own tax bill is not overdue and no back-tax is owed to it.</summary>
        public static bool BooksInOrder()
        {
            try
            {
                GameInstance save = Save;
                return save != null && (save.currentUnpaidTaxes == null || !save.currentUnpaidTaxes.lateFeeApplied) && !TaxHelper.HasBackTaxesToPay();
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>Every employee at one of the player's businesses read into their file; files of those gone are dropped.</summary>
        public static void ReadStaff(CompanyState state, List<CompanyShop> shops, int index, CycleState cycle, out int dropped)
        {
            dropped = 0;
            List<StaffFile> staff = state.Staff;
            ById.Clear();
            for (int i = 0; i < staff.Count; i++)
            {
                staff[i].Seen = false;
                ById[staff[i].Id] = staff[i];
            }
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            EmployeeInstance self = Self();
            IndexShops(shops);
            for (int i = 0; i < shops.Count; i++)
            {
                shops[i].Drivers = 0;
                shops[i].Needs &= ~(1 << (int)LicenceKind.DeliveryVehicle);   // asked of a business with a delivery driver on its staff, read again here
            }
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (!Counts(e, self))
                {
                    continue;
                }
                CompanyShop shop = ShopOf(e.assignedAddress);
                if (shop == null)
                {
                    continue;   // a headquarters or a warehouse takes no money: outside the wage market
                }
                if (e.HasSkill(DeliveryDriver))
                {
                    shop.Drivers++;
                }
                StaffFile f;
                if (!ById.TryGetValue(e.id, out f))
                {
                    if (staff.Count >= CompanyCatalogue.StaffMost)
                    {
                        continue;
                    }
                    f = StaffRules.Hire(e.id, CompanyBridges.EsteemNow());
                    staff.Add(f);
                    ById[e.id] = f;
                }
                Skill skill = e.characterData.skills[0];
                f.Seen = true;
                f.Place = shop.File.Place;
                f.Name = e.characterData.name ?? "?";
                f.Base = PaidWage.Base(e.hourlyWage);
                f.Wage = PaidWage.Cents(f.Base, index, 0);   // at today's prices, as the city's cost of living pays it
                f.Skill = Mathf.RoundToInt(skill.value);
                f.Mood = Mathf.RoundToInt(Mathf.Clamp(e.satisfaction, 0f, 100f));
                f.Sick = e.isAbsent;
                int days = e.assignedWeeklyDays != null ? e.assignedWeeklyDays.Count : 0;
                f.DayHours = days > 0 && e.assignedWeeklyHours > 0 ? e.assignedWeeklyHours * 100 / days : 0;
                f.Going = StaffRules.Going(Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(skill) * 100f), StaffRules.Market(index, cycle, shop.Income));
                f.GameOffer = RivalOfferGuard.OfferOpen(e);
                f.Held = CompanyBridges.HeldAt(e.id);
                shop.Sick += f.Sick ? 1 : 0;
            }
            for (int i = staff.Count - 1; i >= 0; i--)
            {
                if (!staff[i].Seen)
                {
                    staff.RemoveAt(i);
                    dropped++;
                }
            }
            for (int i = 0; i < shops.Count; i++)
            {
                if (shops[i].Drivers > 0 && shops[i].File != null)
                {
                    shops[i].Needs |= 1 << (int)LicenceKind.DeliveryVehicle;
                    LicenceRules.Newly(shops[i].File, shops[i].Needs, Save != null ? Save.Day : 0);   // a first term free where it is new to the business
                }
            }
            ById.Clear();
        }

        /// <summary>The game's yearly tax bill has come and waits to be paid.</summary>
        public static bool BillWaiting()
        {
            try
            {
                return Save != null && Save.currentUnpaidTaxes != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The bill waiting, whole money; 0 with none.</summary>
        public static int BillAmount()
        {
            try
            {
                return Save != null && Save.currentUnpaidTaxes != null ? Mathf.RoundToInt(Mathf.Min(Save.currentUnpaidTaxes.totalToPay, int.MaxValue / 4)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>One building the player owns, with the property tax the game charges on it (TaxHelper's sum): so far this tax year, and for the whole year.</summary>
        public struct PropertyTaxLine
        {
            public string Name;
            public int ToDate, Year;
        }

        /// <summary>
        /// The property tax of each building the player owns, from the game's own sum (TaxCalculationHelper.GetRealEstateTaxes for the tax year to yesterday, and
        /// each building's yearly figure). A rented building has none: the tax is inside its rent. Read when the day turns and when the app opens, never per frame.
        /// </summary>
        public static void PropertyTaxes(int today, List<PropertyTaxLine> into)
        {
            into.Clear();
            try
            {
                GameInstance save = Save;
                if (save == null || save.realEstate == null)
                {
                    return;
                }
                int days = DaysPerYear, from = TaxRules.YearStart(today, days);
                List<(string, float)> sofar = TaxCalculationHelper.GetRealEstateTaxes(from, Mathf.Max(from, today - 1), days);
                for (int i = 0; i < sofar.Count && i < save.realEstate.Count && into.Count < CompanyCatalogue.PropertyLinesMost; i++)
                {
                    into.Add(new PropertyTaxLine
                    {
                        Name = sofar[i].Item1 ?? string.Empty, ToDate = Mathf.RoundToInt(Mathf.Min(sofar[i].Item2, int.MaxValue / 4)),
                        Year = Mathf.RoundToInt(Mathf.Min(save.realEstate[i].TaxesAmount, int.MaxValue / 4))
                    });
                }
            }
            catch (Exception)
            {
                into.Clear();
            }
        }

        /// <summary>
        /// The hours each employee with an agreed raise worked on the ending day, before the game's run clears them, and, for everybody, whether the day
        /// was worked short of the day planned (<see cref="StaffRules.LateDay"/>): one pass a day over the staff, a look-up each.
        /// </summary>
        public static void NoteHours(CompanyState state, int endedDay)
        {
            List<StaffFile> staff = state.Staff;
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(endedDay);
            for (int i = 0; i < staff.Count; i++)
            {
                StaffFile f = staff[i];
                f.Hours = 0;
                EmployeeInstance e = Employee(f.Id);
                if (e == null)
                {
                    continue;
                }
                bool driver = e.IsAssignedToAnyBusiness() && e.HasSkill(DeliveryDriver);
                if (f.Raise > 0)
                {
                    // The game pays a delivery driver a fixed day whatever the count says, and so does the raise.
                    f.Hours = driver ? DriverHours : Mathf.Max(0, e.workedHoursToday) * 100;
                }
                if (!driver)
                {
                    StaffRules.LateDay(f, PlannedHours(e, weekday), e.workedHoursToday, e.isAbsent || e.isTrainingDay);
                }
            }
        }

        // The hours the business's schedule has an employee work on a day of the week; 0 for a day off or an employee with no business.
        private static int PlannedHours(EmployeeInstance e, DayOfWeekOrdered weekday)
        {
            BuildingRegistration reg = e.assignedAddress != null && !e.assignedAddress.IsUndefined() ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            List<ScheduleDay> week = reg != null ? reg.scheduleDays : null;
            for (int d = 0; week != null && d < week.Count; d++)
            {
                if (week[d] != null && week[d].day == weekday)
                {
                    return e.GetWorkingHoursOnDay(week[d]);
                }
            }
            return 0;
        }

        public static EmployeeInstance Employee(string id)
        {
            return string.IsNullOrEmpty(id) ? null : EmployeeHelper.GetEmployeeById(id, false);
        }

        /// <summary>
        /// One employee's other offers read again, as they stand now: whether the game's own rival offer to them is open
        /// (Core/RivalOfferGuard) and whether the tech sector has an offer or a departure in hand for them. The game's offer
        /// can come at any hour (its special rivals' planned moves), so a decision on an offer reads them first.
        /// </summary>
        public static void Offers(StaffFile f)
        {
            EmployeeInstance e = Employee(f.Id);
            f.GameOffer = e != null && RivalOfferGuard.OfferOpen(e);
            f.Held = CompanyBridges.HeldAt(f.Id);
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

        // Somebody whose satisfaction is at nothing has resigned: the game takes them off its list within the hour.
        private static bool Counts(EmployeeInstance e, EmployeeInstance self)
        {
            return e != null && !ReferenceEquals(e, self) && !string.IsNullOrEmpty(e.id) && !e.IsCandidate && !e.isBeingReplaced && e.hourlyWage > 0f && e.satisfaction > 0f
                   && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null
                   && e.assignedAddress != null;
        }

        /// <summary>The name of somebody's main skill as the game writes it; empty if they are not found.</summary>
        public static string SkillName(string id)
        {
            EmployeeInstance e = Employee(id);
            string skill = e != null && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null
                ? e.characterData.skills[0].name
                : null;
            return string.IsNullOrEmpty(skill) ? string.Empty : LocalizorManager.IsLocalizedKey(skill) ? skill.GetLocalization() : skill;
        }

        // The game's statement line of each kind of cost (Economy/GameBooks); null where the statement has none.
        private static readonly string[] StatementLines = { GameBooks.Salaries, null, GameBooks.Marketing, GameBooks.Licensing, null };

        /// <summary>
        /// Takes an amount as a transaction. A <see cref="CostKind"/> (else -1) is marked deductible and booked on the
        /// business's statement line where it has one. Unless forced, false and nothing taken when the cash is short.
        /// </summary>
        public static bool Take(int amount, string key, string name, CompanyShop shop, int? day, int deductible, bool force)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            string line = shop != null && shop.Address != null && deductible >= 0 && deductible < StatementLines.Length ? StatementLines[deductible] : null;
            TransactionInfo info = line != null
                ? new TransactionInfo(key, line, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } })
                : Info(key, name);
            if (deductible >= 0 && deductible < CompanyText.CostKeys.Length)
            {
                // Words, not a key, as the game names its own: the tax notice reads the same in a save the mod has left.
                info.SetTaxDeductibleName(CompanyText.CostKeys[deductible].GetLocalization());
            }
            return RealLifeOverhaul.Economy.BizBooks.Book(-amount, info, day, shop != null ? shop.Address : null, force);
        }

        public static void Give(int amount, string key, string name, CompanyShop shop, int? day)
        {
            if (amount > 0 && Save != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(amount, Info(key, name), day, shop != null ? shop.Address : null, true);
            }
        }

        /// <summary>
        /// Opens the game's BizMan on a business's own page, as the game's own buttons do (MyEmployees.ManageSchedule,
        /// UniformLockerController: BizMan.Open with the business's address, BizMan picking its first tab). Only a business of
        /// the player's own (GoingPublic.Mine); nothing is sent or written. False, with nothing opened, if it cannot be.
        /// Runs on a click only.
        /// </summary>
        public static bool OpenBizMan(BuildingRegistration reg)
        {
            try
            {
                global::UI.UIs ui = InstanceBehavior<global::UI.UIs>.Instance;
                if (reg == null || !reg.RentedByPlayer || reg.Address == null || ui == null || ui.fullMenu == null || ui.fullMenu.bizMan == null || !GoingPublic.Mine(reg))
                {
                    return false;
                }
                ui.fullMenu.bizMan.Open(reg.Address);
                return true;
            }
            catch (Exception e)
            {
                CompanyApp.Logger?.Error(e);
                return false;
            }
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }
    }
}
