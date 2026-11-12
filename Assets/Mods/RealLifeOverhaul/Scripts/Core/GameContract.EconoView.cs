namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for EconoView drawn on the RLO interface (Scripts/Ui/EconoView): the pieces of the game's
    /// money app the kit reads after the game (and other mods) filled them, and presses. Each part and its members are in
    /// this file alone; a part file's fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiEconoView = ContractBook.Part("ui_econoview", "ui", "the game's EconoView drawn on the RLO interface");

        // The app itself: its loans, investments and taxes, and the businesses its choice lists (after Going Public took its
        // partners' out in OnEnable).
        public static readonly GameMember EconoBanking = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoView", "banking", "EconoViewBanking");
        public static readonly GameMember EconoBusinesses = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoView", "_businesses", "List<BuildingRegistration>");

        // The overview: the income statement's rows as its scroller holds them (a heading's fold pressed through the scroller's
        // own toggle), the last transactions' table and the days its heads name.
        public static readonly GameMember EconoStatement = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewOverview", "incomeStatementScrollerController",
            "EconoViewIncomeStatementScrollerController");
        public static readonly GameMember EconoLatest = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewOverview", "lastTransactionsScrollerController",
            "EconoViewLastTransactionsScrollerController");
        public static readonly GameMember EconoDays = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewOverview", "daysText", "TextLocalizationComponent[]");
        public static readonly GameMember EconoRows = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewIncomeStatementScrollerController", "_data",
            "List<EconoViewIncomeStatementModel>");
        public static readonly GameMember EconoToggle = Method(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewIncomeStatementScrollerController", "ToggleRow", "Void",
            "EconoViewIncomeStatementModel");

        // Every transaction: its three filters and its table.
        public static readonly GameMember EconoTypes = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewFullTransactions", "typesDropdown", "Dropdown");
        public static readonly GameMember EconoAmounts = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewFullTransactions", "amountDropdown", "Dropdown");
        public static readonly GameMember EconoDayFilter = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewFullTransactions", "daysDropdown", "Dropdown");
        public static readonly GameMember EconoAll = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewFullTransactions", "scrollerController",
            "EconoViewFullTransactionsScrollerController");

        // The game's drop-down keeps its options to itself: read as it shows them (words, or keys it puts into words).
        public static readonly GameMember EconoDropdownOptions = Field(UiEconoView, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember EconoDropdownWords = Field(UiEconoView, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean");

        // The loans: the row the game copies for each loan and its "no loans" line, and each row's loan, words, fields and button.
        public static readonly GameMember EconoLoanTemplate = Field(UiEconoView, Game, "EconoViewLoans", "loanEntry", "Transform");
        public static readonly GameMember EconoNoLoans = Field(UiEconoView, Game, "EconoViewLoans", "noLoansLabel", "GameObject");
        public static readonly GameMember EconoLoan = Field(UiEconoView, Game, "LoanEntryUi", "_loan", "Loan");
        public static readonly GameMember EconoLoanBank = Field(UiEconoView, Game, "LoanEntryUi", "bankLabel", "TMP_Text");
        public static readonly GameMember EconoLoanPaid = Field(UiEconoView, Game, "LoanEntryUi", "paidTotalLabel", "TMP_Text");
        public static readonly GameMember EconoLoanInterest = Field(UiEconoView, Game, "LoanEntryUi", "dailyInterestRateLabel", "TMP_Text");
        public static readonly GameMember EconoLoanTime = Field(UiEconoView, Game, "LoanEntryUi", "timeLeftLabel", "TextLocalizationComponent");
        public static readonly GameMember EconoLoanDaily = Field(UiEconoView, Game, "LoanEntryUi", "dailyPaymentInput", "InputField");
        public static readonly GameMember EconoLoanPayOff = Field(UiEconoView, Game, "LoanEntryUi", "payOffInput", "InputField");
        public static readonly GameMember EconoLoanPay = Field(UiEconoView, Game, "LoanEntryUi", "payOffButton", "Button");

        // The investments: the funds' buttons, the one shown, and its withdraw and auto-invest controls.
        public static readonly GameMember EconoFundButtons = Field(UiEconoView, Game, "EconoViewInvestments", "_fundButtons", "List<InvestmentFundButton>");
        public static readonly GameMember EconoFund = Field(UiEconoView, Game, "EconoViewInvestments", "_currentInvestment", "InvestmentFund");
        public static readonly GameMember EconoWithdraw = Field(UiEconoView, Game, "EconoViewInvestments", "withdrawField", "InputField");
        public static readonly GameMember EconoAutoField = Field(UiEconoView, Game, "EconoViewInvestments", "autoInvestField", "InputField");
        public static readonly GameMember EconoAutoToggle = Field(UiEconoView, Game, "EconoViewInvestments", "autoInvestToggle", "Toggle");

        // The taxes as the game's Init (and Going Public's postfix on it) wrote them.
        public static readonly GameMember EconoTaxRate = Field(UiEconoView, Game, "EconoViewTaxes", "taxRateAmountLabel", "TMP_Text");
        public static readonly GameMember EconoBackArea = Field(UiEconoView, Game, "EconoViewTaxes", "backTaxesArea", "GameObject");
        public static readonly GameMember EconoBackAmount = Field(UiEconoView, Game, "EconoViewTaxes", "backTaxesAmountLabel", "TMP_Text");
        public static readonly GameMember EconoOwed = Field(UiEconoView, Game, "EconoViewTaxes", "taxesOwedAmountLabel", "TMP_Text");
        public static readonly GameMember EconoOwedWhen = Field(UiEconoView, Game, "EconoViewTaxes", "taxesOwedDaysLeftLabel", "TextLocalizationComponent");
        public static readonly GameMember EconoOwedBar = Field(UiEconoView, Game, "EconoViewTaxes", "taxesOwedProgressBar", "ProgressBar");
        public static readonly GameMember EconoProjection = Field(UiEconoView, Game, "EconoViewTaxes", "upcomingTaxProjectionAmountLabel", "TMP_Text");
        public static readonly GameMember EconoProjectionWhen = Field(UiEconoView, Game, "EconoViewTaxes", "upcomingTaxProjectionDaysLeftLabel", "TextLocalizationComponent");
        public static readonly GameMember EconoProjectionBar = Field(UiEconoView, Game, "EconoViewTaxes", "upcomingTaxProjectionProgressBar", "ProgressBar");
        public static readonly GameMember EconoDeductions = Field(UiEconoView, Game, "EconoViewTaxes", "taxDeductionAmountLabel", "TMP_Text");
        public static readonly GameMember EconoIrsArea = Field(UiEconoView, Game, "EconoViewTaxes", "contactArea", "GameObject");
        public static readonly GameMember EconoIrs = Field(UiEconoView, Game, "EconoViewTaxes", "irsContactButton", "Button");

        // One business: the days its heads name, the four days' statements its page was made from, and which of its income
        // statement's rows fold under which.
        public static readonly GameMember EconoBusinessDays = Field(UiEconoView, Game, "EconoViewBusinessDetails", "daysText", "TextLocalizationComponent[]");
        public static readonly GameMember EconoBusinessData = Field(UiEconoView, Game, "EconoViewBusinessDetails", "_selectedBusinessIncomeStatements",
            "List<FinancialSummary.BusinessIncomeStatement>");
        public static readonly GameMember EconoBusinessFolds = Field(UiEconoView, Game, "UI.Smartphone.Apps.EconoView.EconoViewIncomeStatement", "_rowRelationships",
            "List<EconoViewIncomeStatement.RowRelationship>");
    }
}
