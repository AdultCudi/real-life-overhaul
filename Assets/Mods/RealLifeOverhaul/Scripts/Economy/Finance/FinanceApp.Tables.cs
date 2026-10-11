using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Finance app's tables, declared as data (GameUi.TableSpec): for each tab its columns, how each sorts, which
    /// are summed, the kinds its rows can be filtered by, the order it opens in, and the sentence shown when a filter
    /// leaves nothing. The numbers beside each are the columns, for whoever writes the rows.
    /// </summary>
    internal static class FinanceTables
    {
        // ---- the kinds of debt (the filter of the Overview and of Accounts)
        public const int Loan = 0, Business = 1, Consolidated = 2, FirstProduct = 3, Private = 8, Other = 9, Approval = 10;

        public static readonly string[] DebtKinds =
        {
            "reallife_econ_kind_loan", "reallife_econ_kind_business", "reallife_econ_kind_consolidated",
            "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_car", "reallife_econ_kind_home", "reallife_econ_kind_arrears",
            "reallife_econ_kind_private", "reallife_tab_kind_other_debt", "reallife_tab_kind_approval"
        };

        /// <summary>The kind of one of the mod's accounts.</summary>
        public static int Of(ProductKind kind)
        {
            return FirstProduct + (int)kind;
        }

        /// <summary>The kind of one of the game's loans, by what the mod lent it for.</summary>
        public static int OfLoan(int purpose)
        {
            return purpose == LoanPurpose.Business ? Business : purpose == LoanPurpose.Consolidation ? Consolidated : Loan;
        }

        // ---- the kinds of offer (Borrow): a bank's loan, then the six other kinds of credit in the app's own order
        public static readonly string[] OfferKinds =
        {
            "reallife_econ_kind_loan", "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_business", "reallife_econ_kind_car", "reallife_econ_kind_home",
            "reallife_econ_kind_consolidate"
        };

        // ---- what the rating's table lists
        public const int Factor = 0, Event = 1;
        public static readonly string[] CreditKinds = { "reallife_tab_kind_factor", "reallife_tab_kind_event" };

        // ---- the city's page
        public const int District = 0, Supply = 1, Shop = 2, Estate = 3;
        public static readonly string[] CityKinds = { "reallife_tab_city_districts", "reallife_tab_city_supply", "reallife_tab_city_shops", "reallife_tab_city_property" };

        /// <summary>While the credit file is not open: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Finance", "Off").Column("reallife_econ_col_debt", 600f, TableSort.None, flexible: true);

        /// <summary>Overview: 0 owed to, 1 kind, 2 owed, 3 each day, 4 time left.</summary>
        public static readonly TableSpec Overview = new TableSpec("Finance", "Overview")
            .Column("reallife_econ_col_debt", 560f, TableSort.Text, flexible: true)
            .Column("reallife_econ_col_kind", 380f, TableSort.Text)
            .Column("reallife_econ_col_owed", 300f, TableSort.Money, sums: true)
            .Column("reallife_econ_col_per_day", 300f, TableSort.Money, sums: true)
            .Column("reallife_econ_col_left", 400f, TableSort.Custom, right: true)
            .Filter("reallife_tab_all_kinds", DebtKinds)
            .Empty("reallife_tab_empty_debts");

        /// <summary>Credit: 0 what, 1 kind, 2 weight, 3 day, 4 amount, 5 rating.</summary>
        public static readonly TableSpec Credit = new TableSpec("Finance", "Credit")
            .Column("reallife_econ_col_event", 700f, TableSort.Text, flexible: true)
            .Column("reallife_econ_col_kind", 300f, TableSort.Text)
            .Column("reallife_tab_col_weight", 200f, TableSort.Number)
            .Column("reallife_econ_col_day", 200f, TableSort.Date)
            .Column("reallife_econ_col_amount", 260f, TableSort.Money)
            .Column("reallife_econ_col_effect", 300f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_kinds", CreditKinds)
            .Empty("reallife_tab_empty_credit");

        /// <summary>Borrow: 0 lender, 1 kind, 2 most, 3 rate, 4 each day, 5 fee, 6 term, 7 offer.</summary>
        public static readonly TableSpec Borrow = new TableSpec("Finance", "Borrow")
            .Column("reallife_econ_col_lender", 460f, TableSort.Text, flexible: true)
            .Column("reallife_econ_col_kind", 300f, TableSort.Text)
            .Column("reallife_tab_col_most", 280f, TableSort.Money)
            .Column("reallife_econ_col_rate", 200f, TableSort.Number)
            .Column("reallife_econ_col_per_day", 200f, TableSort.Money)
            .Column("reallife_tab_col_fee", 160f, TableSort.Money)
            .Column("reallife_tab_col_term", 180f, TableSort.Number)
            .Column("reallife_tab_col_offer", 260f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", OfferKinds)
            .Order(3, true)
            .Empty("reallife_tab_empty_offers");

        /// <summary>Cash flow: 0 day, 1 what, 2 category, 3 in, 4 out.</summary>
        public static readonly TableSpec Cash = new TableSpec("Finance", "Cash flow")
            .Column("reallife_econ_col_day", 200f, TableSort.Date)
            .Column("reallife_tab_col_what", 760f, TableSort.Text, flexible: true)
            .Column("reallife_tab_col_category", 420f, TableSort.Text)
            .Column("reallife_econ_col_in", 300f, TableSort.Money, sums: true)
            .Column("reallife_econ_col_out", 300f, TableSort.Money, sums: true, right: true)
            .Filter("reallife_tab_all_categories", CashFlow.CategoryKeys)
            .Order(0, false)
            .Empty("reallife_tab_empty_cash");

        /// <summary>Accounts: 0 account, 1 kind, 2 owed, 3 rate, 4 next payment, 5 due, 6 how it stands.</summary>
        public static readonly TableSpec Accounts = new TableSpec("Finance", "Accounts")
            .Column("reallife_econ_col_account", 480f, TableSort.Text, flexible: true)
            .Column("reallife_econ_col_kind", 290f, TableSort.Text)
            .Column("reallife_econ_col_owed", 230f, TableSort.Money, sums: true)
            .Column("reallife_econ_col_rate", 200f, TableSort.Number)
            .Column("reallife_tab_col_next", 250f, TableSort.Money, sums: true)
            .Column("reallife_tab_col_due", 180f, TableSort.Date)
            .Column("reallife_econ_col_state", 330f, TableSort.Custom, right: true)
            .Filter("reallife_tab_all_kinds", DebtKinds)
            .Order(5, true)
            .Empty("reallife_tab_empty_debts");

        /// <summary>The city's page: 0 name, 1 kind, 2 now, 3 change, 4 note.</summary>
        public static readonly TableSpec City = new TableSpec("Finance", "The city")
            .Column("reallife_macro_col_name", 560f, TableSort.Text, flexible: true)
            .Column("reallife_econ_col_kind", 380f, TableSort.Text)
            .Column("reallife_macro_col_now", 300f, TableSort.Custom)
            .Column("reallife_macro_col_change", 500f, TableSort.Custom)
            .Column("reallife_macro_col_note", 400f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", CityKinds)
            .Empty("reallife_tab_empty_city");

        // ---- what a missed payment would do next

        private static readonly string[] StepKeys =
        {
            "reallife_tab_step_reminder", "reallife_tab_step_fee", "reallife_tab_step_penalty", "reallife_tab_step_collections", "reallife_tab_step_repossession", "reallife_tab_step_claim"
        };

        /// <summary>
        /// The next step a lender takes if payments go on being missed, read from the same counts the ladder itself
        /// uses (ProductCatalogue.Counts): the step, and how many more misses it is away. False if there is none left
        /// at this level for this debt.
        /// </summary>
        public static bool NextStep(LadderState s, int level, bool secured, out string stepKey, out int missesAway)
        {
            level = ProductCatalogue.Level(level);
            int[] counts = ProductCatalogue.Counts(level);
            for (int i = 0; i < counts.Length && i < StepKeys.Length; i++)
            {
                if (counts[i] > s.Misses && LadderRules.Takes((LadderStep)(i + 1), level, secured))
                {
                    stepKey = StepKeys[i];
                    missesAway = counts[i] - s.Misses;
                    return true;
                }
            }
            stepKey = null;
            missesAway = 0;
            return false;
        }
    }
}
