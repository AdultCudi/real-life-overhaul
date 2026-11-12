namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the game's tooltips on the RLO interface (Scripts/Ui/Tooltips): the builder's own templates, whose
    /// clones the game makes for each tooltip and the card reads after the game and other mods filled them, and what an employee's
    /// tooltip adds to its words. Each part and its members in this file alone; the fields are made in their own order and lean
    /// on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiTooltips = ContractBook.Part("ui_tooltips", "ui", "the game's tooltips drawn on the RLO interface");

        // The tooltip system (Tooltip.TooltipSystem): its canvas, which the card stands over, its content's parent, which is hidden under the
        // card, and the template each kind of line is cloned from (a clone's name is its template's with " Clone").
        private const string TipSystem = "Tooltip.TooltipSystem";
        public static readonly GameMember TipCanvas = Field(UiTooltips, Game, TipSystem, "canvas", "Canvas");
        public static readonly GameMember TipGroup = Field(UiTooltips, Game, TipSystem, "canvasGroup", "CanvasGroup");
        public static readonly GameMember TipData = Field(UiTooltips, Game, TipSystem, "dataTransform", "Transform");
        public static readonly GameMember TipSplitter = Field(UiTooltips, Game, TipSystem, "splitterTemplate", "Transform");
        public static readonly GameMember TipHeader = Field(UiTooltips, Game, TipSystem, "headerTemplate", "Transform");
        public static readonly GameMember TipCategory = Field(UiTooltips, Game, TipSystem, "categoryTemplate", "Transform");
        public static readonly GameMember TipLabel = Field(UiTooltips, Game, TipSystem, "labelTemplate", "Transform");
        public static readonly GameMember TipBar = Field(UiTooltips, Game, TipSystem, "progressBarTemplate", "Transform");
        public static readonly GameMember TipPairs = Field(UiTooltips, Game, TipSystem, "keyValueListTemplate", "Transform");
        public static readonly GameMember TipList = Field(UiTooltips, Game, TipSystem, "listTemplate", "Transform");
        public static readonly GameMember TipChecks = Field(UiTooltips, Game, TipSystem, "checkboxListTemplate", "Transform");
        public static readonly GameMember TipDemands = Field(UiTooltips, Game, TipSystem, "demandsListTemplate", "Transform");
        public static readonly GameMember TipImage = Field(UiTooltips, Game, TipSystem, "imageTemplate", "Image");
        public static readonly GameMember TipVisible = Prop(UiTooltips, Game, TipSystem, "IsVisible", "Boolean", true);

        // The tooltip target now showing (the game keeps it to hide its own tooltip), and an employee's tooltip's person.
        public static readonly GameMember TipCurrent = StaticField(UiTooltips, Game, "TooltipTarget", "CurrentTooltipTarget", "TooltipTarget");
        public static readonly GameMember TipEmployee = Field(UiTooltips, Game, "EmployeeTooltip", "employeeInstance", "EmployeeInstance");

        // What the employee's card adds: the person's shift now and today's, their business and mood.
        public static readonly GameMember TipShiftNow = Method(UiTooltips, Game, "Entities.EmployeeInstance", "GetWorkShiftAssignedInThisMoment", "WorkShift", "Boolean", "WorkShiftType");
        public static readonly GameMember TipSatisfaction = Field(UiTooltips, Game, "Entities.EmployeeInstance", "satisfaction", "Single");
        public static readonly GameMember TipAssigned = Field(UiTooltips, Game, "Entities.EmployeeInstance", "assignedAddress", "Address");
        public static readonly GameMember TipToday = StaticMethod(UiTooltips, Game, "Helpers.BuildingHelper", "GetTodaySchedule", "ScheduleDay", "BuildingRegistration");
    }
}
