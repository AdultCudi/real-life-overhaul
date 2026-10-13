using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What "running a business" asks of the rest of the mod. Each is optional and has a default, so Economy/Company
    /// works alone; Scripts/Wiring/CompanyLinks.cs fills them in. It never calls another feature directly.
    /// </summary>
    public static class CompanyBridges
    {
        /// <summary>Has a business's promotion figure written again as it should stand now (business pressure's own writer). Default: nothing is written, so footfall is not changed.</summary>
        public static CityBridges.FootfallWriter Footfall;

        /// <summary>True where the game keeps a promotion figure for a business, so that its footfall can be changed at all. Default: nowhere.</summary>
        public static Func<BuildingRegistration, bool> Promoted;

        /// <summary>Shuts a business for some hours by business pressure's own closure, which reopens it itself. False if it would not. Default: never.</summary>
        public static Func<BuildingRegistration, int, bool> Close;

        /// <summary>Starts business pressure's own tax audit at a business, by its own rules. Default: never.</summary>
        public static Func<BuildingRegistration, bool> Audit;

        /// <summary>Starts one of business pressure's own inspections at a business, by its own rules. Default: never.</summary>
        public static Func<BuildingRegistration, bool> Inspect;

        /// <summary>True while business pressure has a staff dispute of its own open at a business: the mod's strike waits. Default: never.</summary>
        public static Func<BuildingRegistration, bool> Dispute;

        /// <summary>A rival's name to put to an offer; null if there is none to name. Default: none.</summary>
        public static Func<BuildingRegistration, string> Rival;

        /// <summary>Has an employee leave by the game's own resignation, as business pressure does. Default: nobody leaves.</summary>
        public static Action<EmployeeInstance> Leave;

        /// <summary>Files a criminal case for tax kept back, for a sum. Returns the court's number; 0 with no court to file with. Default: 0.</summary>
        public static Func<int, int> TaxCase;

        /// <summary>The footfall change, per cent, that one of business pressure's own issues has running at a business on a day; 0 with none then. Default: none.</summary>
        public static Func<BuildingRegistration, int, int> IssueFootfall;

        /// <summary>Income of the days from one to another (both counted) that is in no business's statement and belongs in the quarter's return: the gain on a business sold. Default: none.</summary>
        public static Func<int, int, int> OtherIncome;

        /// <summary>True for a business whose cleaning the cleaning service does on its round, so that the staff rota planner asks for no cleaning shift there. Default: none is.</summary>
        public static Func<Address, bool> Cleaned;

        /// <summary>
        /// A campaign of a kind that is not the company's own (CompanyCatalogue.CampaignOwnKinds and on: a sponsored post on
        /// Flare): whether it can be booked at a business now, and its cost and its customers as thousandths of the
        /// catalogue's figures. Default: none of them is offered.
        /// </summary>
        public static OutsideQuote Outside;

        public delegate bool OutsideQuote(int kind, string place, out int cost, out int reach);

        /// <summary>A campaign of a kind that is not the company's own has been booked and paid: its business, kind, first and last day, and customers. Default: nobody is told.</summary>
        public static Action<string, int, int, int, int> Booked;

        /// <summary>
        /// What it is like to work for the player as the city tells it (Standing's workers' reputation, through
        /// Wiring/StandingLinks), -10 to 10: somebody new starts that many points of standing above or below the usual, and
        /// a rival approaches the staff a quarter as much more or less often for every four points under or over. Default: 0.
        /// </summary>
        public static Func<int> Esteem;

        /// <summary>The esteem, inside its bounds; 0 without it.</summary>
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
        }
    }

    /// <summary>One business of the player's as the company sees it today: its file and what was read from the game.</summary>
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
        public int Average;             // an average day's takings by the game's statements of the last seven days
        public int Takings, Profit;     // yesterday, by the game's statement
        public bool HasBooks;           // the game has a statement for yesterday
        public int Staff, Low, Standing, Sick;   // its staff: how many, how many stand low, their standing added up, how many are off sick today
        public int Asked;               // per cent of customers the company asks for here now
        public bool Applied;            // something of the company's is in the game's figure
    }

    /// <summary>
    /// Everything "running a business" reads from or does to the game, in one place.
    ///
    /// READ: the player's businesses (the same test the Finance app uses), each one's district, type, opening hours,
    /// daily statement (GameInstance.financialSummaries) and customers (BuildingRegistration.orderHistory); every
    /// employee's wage, main skill, satisfaction, hours and absence (EmployeeInstance); the game's tax rate and
    /// whether the year's sales pass its threshold (TaxHelper, TaxCalculationHelper).
    ///
    /// DONE: money only, as dated transactions of the mod's own with their own keys and labels
    /// (GameManager.ChangeMoneySafe). A business cost is marked deductible the way the game marks its own
    /// (TransactionInfo.SetTaxDeductibleName), so the game's yearly tax deducts it itself. No wage, no satisfaction,
    /// no schedule and no figure of a business is written here. Footfall, closures, inspections, audits, a
    /// resignation and a court case are asked for through <see cref="CompanyBridges"/>.
    ///
    /// Nothing here runs on a timer: once when the feature starts, on the game's hour that opens a day, once a game
    /// day, and when the app is opened.
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

        // The label a deductible cost goes under in the game's tax notice, by the kind of cost. Written as words, as
        // the game writes a business's name there, so the notice reads the same in a save the mod has left.
        private static readonly string[] DeductibleKeys =
        {
            "reallife_co_deduct_wages", "reallife_co_deduct_premiums", "reallife_co_deduct_marketing", "reallife_co_deduct_licences", "reallife_co_deduct_accountant"
        };

        // The game's business types by what they sell (their names as the game writes them, "ba:businesstype_...").
        private static readonly string[] FoodTypes = { "fastfoodrestaurant", "coffeeshop", "supermarket", "fruitandvegetablestore" };
        private static readonly string[] AlcoholTypes = { "liquorstore", "nightclub" };
        private static readonly string[] WellOffTypes = { "jewelrystore", "electronicsstore", "cardealership", "furniturestore", "clothingstore", "nightclub" };
        private static readonly string[] PriceTypes = { "supermarket", "fastfoodrestaurant", "fruitandvegetablestore", "liquorstore", "gasstation", "giftshop" };
        private const string DeliveryDriver = "ba:skill_deliverydriver";
        private const int DriverHours = 570;    // what the game pays a delivery driver for a day (EmployeeInstance.PayWage), hundredths

        public static IModLogger Logger;

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

        // ---------------------------------------------------------------- the businesses

        /// <summary>A business by its address, written as business pressure writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The player's businesses as they stand today, each with its file. A business seen for the first time gets
        /// one: with <paramref name="ranBefore"/> (the feature has just appeared in this save) every licence it needs
        /// for a first term, free; otherwise a few days' grace. A file whose business is no longer the player's is
        /// dropped. One pass over the save's buildings and each business's week of opening hours.
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
                    shop.Address = null;    // a business whose building cannot be read is in no district
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

        // Open past ten at night or before five in the morning on any day of its week (the game's own opening hours).
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

        public static CompanyShop ShopAt(List<CompanyShop> shops, string place)
        {
            for (int i = 0; i < shops.Count; i++)
            {
                if (string.Equals(shops[i].File.Place, place, StringComparison.Ordinal))
                {
                    return shops[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the game's books

        /// <summary>
        /// Yesterday's takings and profit of each business and its average day over the last seven, from the game's
        /// own daily statements. One walk back from the newest statement, at most <see cref="CompanyCatalogue.BooksDays"/> of them.
        /// </summary>
        public static void ReadBooks(List<CompanyShop> shops, int today)
        {
            GameInstance save = Save;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            if (summaries == null || shops.Count == 0)
            {
                return;
            }
            int from = today - 7, looked = 0;
            for (int i = summaries.Count - 1; i >= 0 && looked < CompanyCatalogue.BooksDays; i--, looked++)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber >= today)
                {
                    continue;
                }
                if (summary.dayNumber < from)
                {
                    break;   // (the game keeps them oldest first)
                }
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    CompanyShop shop = statement != null ? ShopOf(shops, statement.Address) : null;
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

        private static CompanyShop ShopOf(List<CompanyShop> shops, Address address)
        {
            for (int i = 0; address != null && i < shops.Count; i++)
            {
                if (shops[i].Address != null && shops[i].Address == address)
                {
                    return shops[i];
                }
            }
            return null;
        }

        /// <summary>One day of a business in the game's books: its customers (the order history), its takings and its profit (the daily statement). False if the game has no statement for that day.</summary>
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
                return Save != null && Save.financialSummaries != null ? Mathf.RoundToInt(Mathf.Min(TaxCalculationHelper.GetBusinessSales(fromDay, toDay), int.MaxValue / 4)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Whether the game's yearly tax falls due on the last year's income as it stands: its own threshold, asked of the game.</summary>
        public static bool Taxed(int today)
        {
            try
            {
                return Save != null && TaxHelper.MeetsIncomeTaxThreshold(TaxCalculationHelper.GetBusinessSales(today - DaysPerYear + 1, today), TaxCalculationHelper.GetCurrentTaxableGamblingWinnings());
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The books are in order for a return: the game's own tax bill is not overdue and no back-tax is owed to it.</summary>
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

        // ---------------------------------------------------------------- the staff

        /// <summary>
        /// Every employee who works at one of the player's businesses, read into their file: the game's wage, the
        /// going rate for their main skill there, their satisfaction, a day's hours and whether the game has them off
        /// sick today. A file whose employee has gone is dropped. One pass over the game's employees.
        /// </summary>
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
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (!Counts(e, self))
                {
                    continue;
                }
                CompanyShop shop = ShopOf(shops, e.assignedAddress);
                if (shop == null)
                {
                    continue;   // not at a business that takes money (a headquarters, a warehouse): outside the wage market
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
                f.Wage = CompanyCatalogue.Share(Mathf.RoundToInt(e.hourlyWage * 100f), index);   // at today's prices: the city's cost of living pays it so
                f.Skill = Mathf.RoundToInt(skill.value);
                f.Mood = Mathf.RoundToInt(Mathf.Clamp(e.satisfaction, 0f, 100f));
                f.Sick = e.isAbsent;
                int days = e.assignedWeeklyDays != null ? e.assignedWeeklyDays.Count : 0;
                f.DayHours = days > 0 && e.assignedWeeklyHours > 0 ? e.assignedWeeklyHours * 100 / days : 0;
                f.Going = StaffRules.Going(Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(skill) * 100f), StaffRules.Market(index, cycle, shop.Income));
                shop.Staff++;
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

        /// <summary>
        /// The hour that opens a new day, before the game's daily run pays the wages and clears the hours: what each
        /// employee with an agreed raise worked on the day that is ending, by the game's own count.
        /// </summary>
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
                    // (the game pays a delivery driver for a fixed day whatever the count says: so does the raise)
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

        // (somebody whose satisfaction is at nothing has resigned: the game takes them off its list within the hour)
        private static bool Counts(EmployeeInstance e, EmployeeInstance self)
        {
            return e != null && !ReferenceEquals(e, self) && !string.IsNullOrEmpty(e.id) && !e.IsCandidate && !e.isBeingReplaced && e.hourlyWage > 0f && e.satisfaction > 0f && e.characterData != null
                   && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null && e.assignedAddress != null;
        }

        /// <summary>The name of somebody's main skill as the game writes it; empty if they are not found.</summary>
        public static string SkillName(string id)
        {
            EmployeeInstance e = Employee(id);
            string skill = e != null && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null ? e.characterData.skills[0].name : null;
            return string.IsNullOrEmpty(skill) ? string.Empty : LocalizorManager.IsLocalizedKey(skill) ? skill.GetLocalization() : skill;
        }

        // ---------------------------------------------------------------- money

        // The game's category of each kind of cost that has a line of the game's daily statement (Economy/GameBooks); null for none.
        private static readonly string[] StatementLines = { GameBooks.Salaries, null, GameBooks.Marketing, GameBooks.Licensing, null };

        /// <summary>
        /// Takes an amount as a transaction of the mod's own at a business (or at none). With a kind of cost it is
        /// marked deductible as the game marks its own, and a kind that has a line of the game's daily statement (wages,
        /// marketing, licences) is booked under it at the business, so that the statement counts it. Unless
        /// <paramref name="force"/>, the game's call refuses a charge the cash does not cover: false, and nothing is taken.
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
            if (deductible >= 0 && deductible < DeductibleKeys.Length)
            {
                info.SetTaxDeductibleName(DeductibleKeys[deductible].GetLocalization());
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

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }
    }
}
