using System;
using System.Collections.Generic;
using BigAmbitions.Characters.Skills;
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

        /// <summary>An employee leaves by the game's own resignation, as business pressure has them leave.</summary>
        public static Action<EmployeeInstance> Leave;

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

        public static void Reset()
        {
            Comfort = null;
            Footfall = null;
            Promoted = null;
            Close = null;
            Audit = null;
            Inspect = null;
            Dispute = null;
            Rival = null;
            Leave = null;
            TaxCase = null;
            IssueFootfall = null;
            OtherIncome = null;
            Cleaned = null;
            Outside = null;
            Booked = null;
            Esteem = null;
            ListingLine = null;
            OpenListing = null;
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
        public int Taste;               // +1 sells to the well-off, -1 sells on price, 0 neither
        public int Average;             // an average day's takings over the last seven statements
        public int Takings, Profit;     // yesterday's, by the game's statement
        public bool HasBooks;           // the game has a statement for yesterday
        public int Staff, Low, Standing, Sick;   // staff, those standing low, their standing summed, those off sick
        public int Asked;               // per cent of customers the company asks for here now
        public bool Applied;            // something of the company's is in the game's figure
    }

    /// <summary>
    /// Everything running a business reads from or does to the game. It writes money only, as transactions, never a
    /// wage, a schedule or a business's figure. Runs at the start, the hour that opens a day, the day and the app's opening.
    /// </summary>
    internal static class CompanyGame
    {
        public const string RaiseKey = "reallife_transaction_co_raise";
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

        // The game's business types by what they sell, as the end of their names ("ba:businesstype_...").
        private static readonly string[] FoodTypes = { "fastfoodrestaurant", "coffeeshop", "supermarket", "fruitandvegetablestore" };
        private static readonly string[] AlcoholTypes = { "liquorstore", "nightclub" };
        private static readonly string[] WellOffTypes = { "jewelrystore", "electronicsstore", "cardealership", "furniturestore", "clothingstore", "nightclub" };
        private static readonly string[] PriceTypes = { "supermarket", "fastfoodrestaurant", "fruitandvegetablestore", "liquorstore", "gasstation", "giftshop" };
        private const string DeliveryDriver = "ba:skill_deliverydriver";
        private const int DriverHours = 570;    // hundredths: the fixed day the game pays a delivery driver (EmployeeInstance.PayWage)

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly Dictionary<string, StaffFile> ById = new Dictionary<string, StaffFile>(64);

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

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
                try
                {
                    shop.Address = reg.Address;
                    shop.District = reg.Neighborhood;
                }
                catch (Exception)
                {
                    shop.Address = null;    // a building that cannot be read is in no district
                    shop.District = null;
                }
                shop.Income = Mathf.RoundToInt(CityEconomy.DistrictIncome(shop.District));
                shop.Needs = LicenceRules.Needs(Is(type, FoodTypes), Is(type, AlcoholTypes), OpensLate(reg));
                shop.Taste = Is(type, WellOffTypes) ? 1 : Is(type, PriceTypes) ? -1 : 0;
                shop.Average = shop.Takings = shop.Profit = 0;
                shop.HasBooks = false;
                shop.Staff = shop.Low = shop.Standing = shop.Sick = 0;
                shop.Asked = 0;
                shop.Applied = false;
                if (firstSight)
                {
                    LicenceRules.First(file, shop.Needs, day, ranBefore);
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

        /// <summary>Yesterday's takings and profit of each business, and its average day of the last seven statements.</summary>
        public static void ReadBooks(List<CompanyShop> shops, int today)
        {
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
                shops[i].Average /= 7;
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
                StaffFile f;
                if (!ById.TryGetValue(e.id, out f))
                {
                    if (staff.Count >= CompanyCatalogue.StaffMost)
                    {
                        continue;
                    }
                    f = new StaffFile { Id = e.id, Standing = CompanyCatalogue.Clamp(CompanyCatalogue.StandingStart + CompanyBridges.EsteemNow(), 0, CompanyCatalogue.StandingMost) };
                    staff.Add(f);
                    ById[e.id] = f;
                }
                Skill skill = e.characterData.skills[0];
                f.Seen = true;
                f.Place = shop.File.Place;
                f.Name = e.characterData.name ?? "?";
                f.Wage = CompanyCatalogue.Share(Mathf.RoundToInt(e.hourlyWage * 100f), index);   // at today's prices, as the city's cost of living pays it
                f.Skill = Mathf.RoundToInt(skill.value);
                f.Mood = Mathf.RoundToInt(Mathf.Clamp(e.satisfaction, 0f, 100f));
                f.Sick = e.isAbsent;
                int days = e.assignedWeeklyDays != null ? e.assignedWeeklyDays.Count : 0;
                f.DayHours = days > 0 && e.assignedWeeklyHours > 0 ? e.assignedWeeklyHours * 100 / days : 0;
                f.Going = StaffRules.Going(Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(skill) * 100f), StaffRules.Market(index, cycle, shop.Income));
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
            ById.Clear();
        }

        /// <summary>The hours each employee with an agreed raise worked on the ending day, before the game's run clears them.</summary>
        public static void NoteHours(CompanyState state)
        {
            List<StaffFile> staff = state.Staff;
            bool any = false;
            for (int i = 0; i < staff.Count; i++)
            {
                staff[i].Hours = 0;
                any |= staff[i].Raise > 0;
            }
            if (!any)
            {
                return;
            }
            for (int i = 0; i < staff.Count; i++)
            {
                StaffFile f = staff[i];
                EmployeeInstance e = f.Raise > 0 ? Employee(f.Id) : null;
                if (e != null)
                {
                    // The game pays a delivery driver a fixed day whatever the count says, and so does the raise.
                    f.Hours = e.IsAssignedToAnyBusiness() && e.HasSkill(DeliveryDriver) ? DriverHours : Mathf.Max(0, e.workedHoursToday) * 100;
                }
            }
        }

        public static EmployeeInstance Employee(string id)
        {
            return string.IsNullOrEmpty(id) ? null : EmployeeHelper.GetEmployeeById(id, false);
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
            return GameManager.ChangeMoneySafe(-amount, info, day, shop != null ? shop.Address : null, force);
        }

        public static void Give(int amount, string key, string name, CompanyShop shop, int? day)
        {
            if (amount > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(key, name), day, shop != null ? shop.Address : null, true);
            }
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }
    }
}
