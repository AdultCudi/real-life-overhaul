using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One bank app's tables, declared as data (GameUi.TableSpec) as the Finance app declares its own. Each app has
    /// its own set, made once when the app is built, so an order or a filter chosen in one bank's app is that app's.
    /// The numbers beside each are the columns, for whoever writes the rows.
    /// </summary>
    internal sealed class BankTables
    {
        // ---- the kinds of thing the Apply tab lists, in its own order (the keys are the Finance app's)
        public const int Loan = 0, Overdraft = 1, Card = 2, Business = 3, Car = 4, Home = 5, Consolidate = 6, OfferCount = 7;

        public static readonly string[] OfferKinds =
        {
            "reallife_econ_kind_loan", "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_business", "reallife_econ_kind_car", "reallife_econ_kind_home",
            "reallife_econ_kind_consolidate"
        };

        // ---- the rows of the Rating tab
        public const int Band = 0, Yours = 1;
        public static readonly string[] RatingKinds = { "reallife_bank_kind_band", "reallife_bank_kind_yours" };

        /// <summary>While the credit file is not open: one empty column.</summary>
        public readonly TableSpec Off;

        /// <summary>Accounts: 0 day, 1 what, 2 category, 3 in, 4 out.</summary>
        public readonly TableSpec Statement;

        /// <summary>Loans: 0 debt, 1 kind, 2 owed, 3 rate, 4 next payment, 5 due, 6 paid off, 7 how it stands.</summary>
        public readonly TableSpec Loans;

        /// <summary>Card: 0 day, 1 what, 2 paid in, 3 drawn.</summary>
        public readonly TableSpec CardMoves;

        /// <summary>Apply: 0 what, 1 most, 2 rate, 3 each day, 4 fee, 5 offer.</summary>
        public readonly TableSpec Apply;

        /// <summary>Rating: 0 rating, 1 kind, 2 a loan up to, 3 its rate, 4 card, 5 overdraft, 6 car finance up to.</summary>
        public readonly TableSpec Rating;

        public BankTables(string app)
        {
            Off = new TableSpec(app, "Off").Column("reallife_econ_col_debt", 600f, TableSort.None, flexible: true);

            Statement = new TableSpec(app, "Accounts")
                .Column("reallife_econ_col_day", 200f, TableSort.Date)
                .Column("reallife_tab_col_what", 760f, TableSort.Text, flexible: true)
                .Column("reallife_tab_col_category", 420f, TableSort.Text)
                .Column("reallife_econ_col_in", 300f, TableSort.Money, sums: true)
                .Column("reallife_econ_col_out", 300f, TableSort.Money, sums: true, right: true)
                .Filter("reallife_tab_all_categories", CashFlow.CategoryKeys)
                .Order(0, false)
                .Empty("reallife_bank_empty_statement");

            Loans = new TableSpec(app, "Loans")
                .Column("reallife_econ_col_account", 440f, TableSort.Text, flexible: true)
                .Column("reallife_econ_col_kind", 280f, TableSort.Text)
                .Column("reallife_econ_col_owed", 230f, TableSort.Money, sums: true)
                .Column("reallife_econ_col_rate", 180f, TableSort.Number)
                .Column("reallife_tab_col_next", 230f, TableSort.Money, sums: true)
                .Column("reallife_tab_col_due", 170f, TableSort.Date)
                .Column("reallife_bank_col_ends", 180f, TableSort.Date)
                .Column("reallife_econ_col_state", 300f, TableSort.Custom, right: true)
                .Filter("reallife_tab_all_kinds", FinanceTables.DebtKinds)
                .Order(2, false)
                .Empty("reallife_bank_empty_loans");

            CardMoves = new TableSpec(app, "Card")
                .Column("reallife_econ_col_day", 200f, TableSort.Date)
                .Column("reallife_tab_col_what", 900f, TableSort.Text, flexible: true)
                .Column("reallife_bank_col_paid_in", 300f, TableSort.Money, sums: true)
                .Column("reallife_bank_col_drawn", 300f, TableSort.Money, sums: true, right: true)
                .Order(0, false)
                .Empty("reallife_bank_empty_card");

            Apply = new TableSpec(app, "Apply")
                .Column("reallife_tab_col_what", 520f, TableSort.Text, flexible: true)
                .Column("reallife_tab_col_most", 300f, TableSort.Money)
                .Column("reallife_econ_col_rate", 220f, TableSort.Number)
                .Column("reallife_econ_col_per_day", 240f, TableSort.Money)
                .Column("reallife_tab_col_fee", 200f, TableSort.Money)
                .Column("reallife_tab_col_offer", 300f, TableSort.Text, right: true)
                .Filter("reallife_tab_all_kinds", OfferKinds)
                .Empty("reallife_tab_empty_offers");

            Rating = new TableSpec(app, "Rating")
                .Column("reallife_bank_col_rating", 300f, TableSort.Number)
                .Column("reallife_econ_col_kind", 260f, TableSort.Text)
                .Column("reallife_bank_col_loan", 300f, TableSort.Money)
                .Column("reallife_econ_col_rate", 200f, TableSort.Number)
                .Column("reallife_econ_kind_card", 260f, TableSort.Money)
                .Column("reallife_econ_kind_overdraft", 260f, TableSort.Money)
                .Column("reallife_econ_kind_car", 280f, TableSort.Money, right: true)
                .Filter("reallife_tab_all_kinds", RatingKinds)
                .Order(0, true)
                .Empty("reallife_tab_empty_offers");
        }
    }
}
