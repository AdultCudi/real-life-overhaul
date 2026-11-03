using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The HUD's live pieces over the running city (Scripts/Ui/HudScreens), each over the street it would stand on: the side
    // panels at work by day and after the shift at night; the objectives with every group, and more than the screen holds at
    // night; the notice cards by day and the casino's and the bridge's at night; the bar's popups by day and indoors; the
    // game's control hints as key chips at night.
    static partial void AddHudScreens(List<Screen> screens)
    {
        screens.Add(Pieces("hudscreens_side", "13.jpg", (s, f, d, k) => UiSamples.PiecesSideScene(s, f, d, k, English, false), UiSidePanel.ActionCommand, 2 * 4 + 0, "DDDDDDLD"));
        screens.Add(Pieces("hudscreens_side_night", "11.jpg", (s, f, d, k) => UiSamples.PiecesSideScene(s, f, d, k, English, true), UiSidePanel.ToolCommand, 1, "DDDDDRD"));
        screens.Add(Pieces("hudscreens_objectives", "02.jpg", (s, f, d, k) => UiSamples.PiecesGoalsScene(s, f, d, k, English, false), UiObjectives.LocateCommand, 2 * 32 + 1, "DDDDDDDDDDDD"));
        screens.Add(Pieces("hudscreens_objectives_tight", "11.jpg", (s, f, d, k) => UiSamples.PiecesGoalsScene(s, f, d, k, English, true), UiObjectives.CloseCommand, 2 * 32 + 0, "DDDDDDDDDDDD"));
        screens.Add(Pieces("hudscreens_notices", "13.jpg", (s, f, d, k) => UiSamples.PiecesNotesScene(s, f, d, k, English, false), UiHudNotice.ActionCommand, 2, "RR"));
        screens.Add(Pieces("hudscreens_notices_night", "11.jpg", (s, f, d, k) => UiSamples.PiecesNotesScene(s, f, d, k, English, true), UiHudNotice.ActionCommand, 1, "RR"));
        screens.Add(Pieces("hudscreens_popups", "02.jpg", (s, f, d, k) => UiSamples.PiecesPopupsScene(s, f, d, k, English, false), UiHudPopup.RowCommand, 0 * 32 + 3, "RRRR"));
        screens.Add(Pieces("hudscreens_popups_indoors", "03.jpg", (s, f, d, k) => UiSamples.PiecesPopupsScene(s, f, d, k, English, true), UiDraw.ToolPillCommand, 1, "RRRR"));
        screens.Add(Pieces("hudscreens_hints", "11.jpg", (s, f, d, k) => UiSamples.PiecesHintsScene(s, f, d, k, English), UiDraw.MoneyPillCommand, 0, "RRRR"));
    }

    // A HUD scene over its street: the mouse resting on one of its targets, the pad's walk from its first.
    private static Screen Pieces(string name, string backdrop, LayOut lay, int hoverCommand, int hoverArg, string walk) =>
        new Screen { Name = name, AppName = "HUD", Backdrop = backdrop, Lay = lay, HoverCommand = hoverCommand, HoverArg = hoverArg, Walk = Moves(walk) };
}
