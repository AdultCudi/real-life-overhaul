namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for My Employees and a rival's staff drawn on the RLO interface (Scripts/Ui/Employees):
    /// the pieces of the game's own screens the kit reads after the game (and other mods) filled them, and presses. Each
    /// part and its members are in this file alone; a part file's fields are made in their own order and lean on nothing
    /// in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiEmployees = ContractBook.Part("ui_employees", "ui", "the game's My Employees drawn on the RLO interface");
        public static readonly GamePart UiRivalStaff = ContractBook.Part("ui_rival_staff", "ui", "a rival's staff drawn on the RLO interface");

        // My Employees' filter panels: their toggles and search field are read and set, then the game's table filters itself again.
        public static readonly GameMember StaffEmployeeFilters = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "employeeFilterPanel", "TogglePanel");
        public static readonly GameMember StaffCandidateFilters = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "candidateFilterPanel", "TogglePanel");

        // The employee box as the game's ShowEmployee filled it: its skills and demands (copies of a template), the choice of
        // business, the training under way, the bonus's cooldown and the button only a candidate shows.
        public static readonly GameMember StaffSkillTemplate = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "skillTemplate", "Transform");
        public static readonly GameMember StaffDemandTemplate = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "demandTemplate", "Transform");
        public static readonly GameMember StaffAssign = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "assignedAddressDropdown", "Dropdown");
        public static readonly GameMember StaffTraining = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "trainingOverlay", "Transform");
        public static readonly GameMember StaffCooldown = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "payBonusCooldownLabel", "TextLocalizationComponent");
        public static readonly GameMember StaffAdditional = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployees", "additionalActionButton", "GameObject");

        // The candidates' table's own toggle for the job board's candidates.
        public static readonly GameMember StaffHideJobBoard = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.CandidateScrollerController", "hideJobBoardCandidatesToggle", "Toggle");

        // The actions for several picked: the drop-down a mass action is chosen in, its list of kinds, and the panel of choices
        // a mass action asks one thing more in (its title, its drop-down).
        public static readonly GameMember StaffMassDropdown = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployeesMassActionsUI", "massActionDropdown", "Dropdown");
        public static readonly GameMember StaffMassTypes = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployeesMassActionsUI", "_massActionsTypes", "String[]");
        public static readonly GameMember StaffMassPanel = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployeesMassActionsUI", "massActionOptionsPanel", "Transform");
        public static readonly GameMember StaffMassTitle = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployeesMassActionsUI", "massActionPanelTitle", "TextLocalizationComponent");
        public static readonly GameMember StaffMassOptions = Field(UiEmployees, Game, "UI.Smartphone.Apps.MyEmployees.MyEmployeesMassActionsUI", "massActionOptionsDropdown", "Dropdown");

        // The game's drop-down keeps its options to itself: read as it shows them (words, or keys it puts into words).
        public static readonly GameMember StaffDropdownOptions = Field(UiEmployees, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember StaffDropdownWords = Field(UiEmployees, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean");

        // A rival's staff (UIs.rivalEmployeesUi, opened from BizMan): its title as the game wrote it, its table, and a row's
        // own Negotiate, pressed so the game's diploma check and its poaching run as a click runs them.
        public static readonly GameMember RivalStaffTitle = Field(UiRivalStaff, Game, "RivalEmployeesUi", "titleLocalization", "TextLocalizationComponent");
        public static readonly GameMember RivalStaffTable = Field(UiRivalStaff, Game, "RivalEmployeesUi", "employeesTable", "RivalEmployeeScrollerController");
        public static readonly GameMember RivalStaffNegotiate = Field(UiRivalStaff, Game, "UI.Smartphone.Apps.BizMan.RivalEmployeeCellView", "negotiateButton", "Button");
    }
}
