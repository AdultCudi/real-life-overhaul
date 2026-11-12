using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] DiaryCommands = { UiAppWindow.NavCommand, UiDiary.DayCommand, UiDiary.MonthCommand, UiDiary.EntryCommand };
    private static readonly int[] BoardCommands = { UiAppWindow.NavCommand, UiBoard.ShiftCommand, UiBoard.LaneCommand, UiBoard.DayCommand, UiBoard.ActionCommand };
    private static readonly int[] ReaderCommands = { UiReader.AnchorCommand, UiReader.RelatedCommand, UiReader.ContentsCommand };
    private static readonly int[] SettingsCommands = { UiAppWindow.NavCommand, UiSettingsList.RowCommand, UiSettingsList.SearchCommand, UiSettingsList.PresetCommand, UiSettingsList.HistoryCommand, UiSettingsList.GroupCommand };
    private static readonly int[] ConversationCommands =
    {
        UiAppWindow.NavCommand, UiMessageList.RowCommand, UiMessageList.SearchCommand, UiConversation.ChoiceCommand, UiConversation.FormCommand, UiConversation.FormLess,
        UiConversation.FormMore, UiConversation.ComposeCommand, UiConversation.SendCommand
    };
    private static readonly int[] FeedCommands = { UiAppWindow.NavCommand, UiFeed.AuthorCommand, UiFeed.ReactCommand, UiFeed.ProfileCommand, UiFeed.ListCommand, UiFeed.PersonCommand };
    private static readonly int[] EditionCommands = { UiAppWindow.NavCommand, UiEdition.StoryCommand, UiEdition.LeadCommand };

    // The page shapes on their gallery pages, every state each has: laid out in every language, frame and input as any screen.
    static partial void UiCasesGrids(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "shape_diary":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a full month)", Title = "Calendar", Commands = DiaryCommands, Lay = (s, f, d, k, t) => UiSamples.Diary(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (an empty month)", Title = "Calendar", Commands = DiaryCommands, Lay = (s, f, d, k, t) => UiSamples.Diary(s, f, d, k, t, true) });
                break;
            case "shape_board":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a busy day)", Title = "Business", Commands = BoardCommands, Lay = (s, f, d, k, t) => UiSamples.Board(s, f, d, k, t, false, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (no workstations)", Title = "Business", Commands = BoardCommands, Lay = (s, f, d, k, t) => UiSamples.Board(s, f, d, k, t, true, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a shift moving)", Title = "Business", Commands = BoardCommands, Lay = (s, f, d, k, t) => UiSamples.Board(s, f, d, k, t, false, true) });
                break;
            case "shape_reader":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (the Guide's longest page)", Title = "Guide", Commands = ReaderCommands,
                    Lay = (s, f, d, k, t) => UiSamples.Reader(s, f, d, k, t, t("reallife_guide_construction", string.Empty), -1) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a section stepped to)", Title = "Guide", Commands = ReaderCommands,
                    Lay = (s, f, d, k, t) => UiSamples.Reader(s, f, d, k, t, t("reallife_guide_construction", string.Empty), 12) });
                break;
            case "shape_settings":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (every option, a section greyed)", Title = "Mod settings", Commands = SettingsCommands,
                    Lay = (s, f, d, k, t) => UiSamples.Settings(s, f, d, k, t, null, UiSamples.SettingSection("Family"), 1) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (the top)", Title = "Mod settings", Commands = SettingsCommands,
                    Lay = (s, f, d, k, t) => UiSamples.Settings(s, f, d, k, t, null, 0, 0) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a search)", Title = "Mod settings", Commands = SettingsCommands,
                    Lay = (s, f, d, k, t) => UiSamples.Settings(s, f, d, k, t, "police", 0, 0) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a search that finds nothing)", Title = "Mod settings", Commands = SettingsCommands,
                    Lay = (s, f, d, k, t) => UiSamples.Settings(s, f, d, k, t, "zzq", 0, 0) });
                break;
            case "shape_conversation":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (in the window)", Title = "Contacts", Commands = ConversationCommands, Lay = (s, f, d, k, t) => UiSamples.ConversationWindow(s, f, d, k, t) });
                break;
            case "shape_conversation_docked":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (docked over the world)", Title = "Talk", Commands = ConversationCommands, Lay = (s, f, d, k, t) => UiSamples.ConversationDocked(s, f, d, k, t) });
                break;
            case "shape_messages":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (twelve threads)", Title = "Flare", Commands = ConversationCommands, Lay = (s, f, d, k, t) => UiSamples.Messages(s, f, d, k, t) });
                break;
            case "shape_feed":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (posts with pictures)", Title = "Flare", Commands = FeedCommands, Lay = (s, f, d, k, t) => UiSamples.Feed(s, f, d, k, t) });
                break;
            case "shape_edition":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a busy day)", Title = "Newspaper", Commands = EditionCommands, Lay = (s, f, d, k, t) => UiSamples.Edition(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a quiet day)", Title = "Newspaper", Commands = EditionCommands, Lay = (s, f, d, k, t) => UiSamples.Edition(s, f, d, k, t, true) });
                break;
        }
    }
}
