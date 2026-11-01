using System.Collections.Generic;
using System.IO;
using System.Text;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The app window's page shapes, each in its app over the street it would stand on; the docked conversation indoors,
    // where a face to face talk happens. Every state the shapes have: a full and an empty month, a busy and an empty board
    // and a shift being moved, the Guide's longest page, the settings list whole and searched, a conversation in the window
    // and docked, Flare's messages and feed, the paper on a busy and a quiet day.
    static partial void AddGrids(List<Screen> screens)
    {
        string guide = Locale("reallife_guide_construction");
        screens.Add(new Screen { Name = "shape_diary", AppName = "Calendar", Icon = UiIcon.Calendar, App = UiSamples.AppCalendar, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.Diary(s, f, d, k, English, false), HoverCommand = UiDiary.DayCommand, HoverArg = 18, Walk = Moves("RDRRDDRRUL") });
        screens.Add(new Screen { Name = "shape_diary_empty", AppName = "Calendar", Icon = UiIcon.Calendar, App = UiSamples.AppCalendar, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.Diary(s, f, d, k, English, true), HoverCommand = UiDiary.DayCommand, HoverArg = 9, Walk = Moves("DRRDDRR") });
        screens.Add(new Screen { Name = "shape_board", AppName = "Business", Icon = UiIcon.Store, App = UiSamples.AppBusiness, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Board(s, f, d, k, English, false, false), HoverCommand = UiBoard.ShiftCommand, HoverArg = 5, Walk = Moves("RDRRDRDLL") });
        screens.Add(new Screen { Name = "shape_board_empty", AppName = "Business", Icon = UiIcon.Store, App = UiSamples.AppBusiness, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Board(s, f, d, k, English, true, false), HoverCommand = UiBoard.DayCommand, HoverArg = 3, Walk = Moves("RRRRDD") });
        screens.Add(new Screen { Name = "shape_board_drag", AppName = "Business", Icon = UiIcon.Store, App = UiSamples.AppBusiness, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Board(s, f, d, k, English, false, true), FocusCommand = UiBoard.ShiftCommand, FocusArg = 2, HoverCommand = UiBoard.ShiftCommand, HoverArg = 2,
            Walk = Moves("LDRRUL") });
        screens.Add(new Screen { Name = "shape_reader", AppName = "Guide", Icon = UiIcon.Info, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.Reader(s, f, d, k, English, guide, -1), HoverCommand = UiReader.RelatedCommand, HoverArg = 1, Walk = Moves("RDDDRDDL") });
        screens.Add(new Screen { Name = "shape_settings", AppName = "Mod settings", Icon = UiIcon.Gear, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Settings(s, f, d, k, English, null, UiSamples.SettingSection("Family"), 1), HoverCommand = UiSettingsList.GroupCommand, HoverArg = 4,
            Walk = Moves("RDDDLDRD") });
        screens.Add(new Screen { Name = "shape_settings_search", AppName = "Mod settings", Icon = UiIcon.Gear, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Settings(s, f, d, k, English, "police", 0, 0), HoverCommand = UiSettingsList.SearchCommand, Walk = Moves("RRDDDDD") });
        screens.Add(new Screen { Name = "shape_conversation", AppName = "Contacts", Icon = UiIcon.AddressBook, App = UiSamples.AppContacts, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.ConversationWindow(s, f, d, k, English), HoverCommand = UiConversation.ChoiceCommand, HoverArg = 1, Walk = Moves("RRDDDLUU") });
        screens.Add(new Screen { Name = "shape_conversation_docked", AppName = "Talk", Icon = UiIcon.Message, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.ConversationDocked(s, f, d, k, English), HoverCommand = UiConversation.ChoiceCommand, HoverArg = 0, Walk = Moves("DDDUU") });
        screens.Add(new Screen { Name = "shape_messages", AppName = "Flare", Icon = UiIcon.Fire, App = UiSamples.AppFlare, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Messages(s, f, d, k, English), HoverCommand = UiMessageList.RowCommand, HoverArg = 3, Walk = Moves("RRDDDLDD") });
        screens.Add(new Screen { Name = "shape_feed", AppName = "Flare", Icon = UiIcon.Fire, App = UiSamples.AppFlare, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.Feed(s, f, d, k, English), HoverCommand = UiFeed.ReactCommand, HoverArg = 4, Walk = Moves("RDRRDDRDR") });
        screens.Add(new Screen { Name = "shape_edition", AppName = "Newspaper", Icon = UiIcon.Newspaper, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Edition(s, f, d, k, English, false), HoverCommand = UiEdition.StoryCommand, HoverArg = 16, Walk = Moves("DDDRDDLD") });
        screens.Add(new Screen { Name = "shape_edition_quiet", AppName = "Newspaper", Icon = UiIcon.Newspaper, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.Edition(s, f, d, k, English, true), HoverCommand = UiEdition.StoryCommand, HoverArg = 0, Walk = Moves("DDRD") });
    }

    // A text of the mod's English locale, read from the file as the game reads it, so the reader shows the Guide's real page.
    private static string Locale(string key)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", "en.json"));
        if (!File.Exists(path))
        {
            return string.Empty;
        }
        string json = File.ReadAllText(path, Encoding.UTF8);
        string head = "\"" + key + "\": \"";
        int at = json.IndexOf(head, System.StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        var sb = new StringBuilder(4096);
        for (int i = at + head.Length; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"')
            {
                break;
            }
            if (c == '\\' && i + 1 < json.Length)
            {
                char e = json[++i];
                if (e == 'u' && i + 4 < json.Length)
                {
                    sb.Append((char)System.Convert.ToInt32(json.Substring(i + 1, 4), 16));
                    i += 4;
                    continue;
                }
                sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
