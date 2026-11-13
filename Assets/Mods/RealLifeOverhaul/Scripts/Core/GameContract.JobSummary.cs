namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the result popups after a delivery shift and a food delivery, drawn as a notice card on
    /// the RLO interface's HUD (Scripts/Ui/HudScreens/HudJobSummary.cs): its part and members in this file alone. Both members
    /// are optional: a game without them leaves the popups as the game draws them. A part file's fields are made in their own
    /// order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart JobSummaryPart = ContractBook.Part("hud_job_summary", "ui", "the result popups after a delivery shift and a food delivery, drawn as a notice card");

        // The popup the game has up now (it makes one under its UI root and destroys it on Close), and the rows it fades in one by one.
        public static readonly GameMember JobSummaryActive = Optional(Field(JobSummaryPart, Game, "UI.DailySummary.DailySummary", "_activeJobSummary", "JobSummary"));
        public static readonly GameMember JobSummaryRows = Optional(Field(JobSummaryPart, Game, "UI.DailySummary.JobSummary", "canvasGroups", "CanvasGroup[]"));
    }
}
