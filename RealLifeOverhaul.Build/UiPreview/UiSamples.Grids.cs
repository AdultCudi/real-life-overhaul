using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The app window's page shapes, each in the app it is made for with the data a player would see there: the Calendar's
/// month as a diary, a café's schedule as a board, the Guide's longest page in the reader, the mod's settings as the
/// settings list (every section and option of its options page), a conversation in Contacts and docked over the world,
/// Flare's messages and feed, and the City Ledger on a busy and a quiet day. Plain English, as the owner reads the
/// previews; the labels the game or the mod already has come through their keys.
/// </summary>
internal static partial class UiSamples
{
    public const int AppCalendar = 9, AppFlare = 7;

    // An app window whose page is one of the page shapes: the window and its sections, the page's room given back.
    private static UiAt ShapeWindow(UiScene s, UiFrame f, bool dark, string name, string title, UiIcon icon, UiNavEntry[] nav, int selected, bool across, IUiKeys k,
        out float x, out float y, out float w, out float h)
    {
        s.Begin(name, f.Width, f.Height, dark);
        UiAt at = UiAppWindow.Shell(s, f, title, icon, nav, selected, across, k, out x, out y, out w, out float bottom);
        h = bottom - y;
        return at;
    }

    // The status bar's prompts for a page shape: move, select, the tab keys' own words where the page has them, back.
    private static void ShapePrompts(UiScene s, UiFrame f, IUiKeys k, UiWords t, string tabs)
    {
        s.AddPrompt(UiAction.Move, t("reallife_ui_prompt_move", "Move"));
        s.AddPrompt(UiAction.Confirm, t("reallife_ui_prompt_select", "Select"));
        if (tabs != null)
        {
            s.AddPrompt(UiAction.TabPrev, tabs);
        }
        s.AddPrompt(UiAction.Back, t("reallife_ui_prompt_back", "Back"));
        UiDraw.PromptBar(s, f, k);
    }

    // ------------------------------------------------------------------------------------------------- the diary

    /// <summary>The Calendar's month: a busy March with today the 14th, or the same month with nothing in it.</summary>
    public static UiDiaryModel Month(UiWords t, bool empty)
    {
        var m = new UiDiaryModel
        {
            Month = t("reallife_calendar_month_3", "March") + ", year 3",
            Weekdays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
            Lead = 2, Length = 30, Today = 14, Chosen = 14, PastBefore = 14,
            ChosenTitle = "Tuesday 14 " + t("reallife_calendar_month_3", "March"),
            ChosenEmpty = "Nothing is set for this day. Bills, wages, classes and birthdays show here as they come.", AheadTitle = "Coming up",
            CanBack = true, CanOn = true,
        };
        if (empty)
        {
            return m;
        }
        m.ChosenFigures = new[]
        {
            new UiPairModel { Key = t("reallife_calendar_kind_in", "Money in"), Value = "$3,480" },
            new UiPairModel { Key = t("reallife_calendar_kind_out", "Money out"), Value = "$2,286" },
        };
        m.Entries = new[]
        {
            new UiDiaryEntry { Day = 1, Icon = UiIcon.Home, Title = "Rent to Hudson Realty", Value = "$1,240", Action = "Open the bank" },
            new UiDiaryEntry { Day = 3, Icon = UiIcon.Coin, Title = "Wages, 6 staff", Value = "$2,910" },
            new UiDiaryEntry { Day = 3, Icon = UiIcon.Cap, Title = "Class: Business basics", Value = "18:00" },
            new UiDiaryEntry { Day = 5, Icon = UiIcon.Truck, Title = "Coffee beans delivered", Value = "08:00" },
            new UiDiaryEntry { Day = 7, Icon = UiIcon.ForkKnife, Title = "Dinner with Uncle Fred", Value = "19:30" },
            new UiDiaryEntry { Day = 9, Icon = UiIcon.Bank, Title = "Loan payment", Value = "$640" },
            new UiDiaryEntry { Day = 10, Icon = UiIcon.Car, Title = "Car service", Value = "11:00" },
            new UiDiaryEntry { Day = 12, Icon = UiIcon.Megaphone, Title = "Flare advert ends" },
            new UiDiaryEntry { Day = 14, Icon = UiIcon.Coin, Title = "Pay day", Value = "$3,480", Action = "Open the bank" },
            new UiDiaryEntry { Day = 14, Icon = UiIcon.Briefcase, Title = "Interview at Lantern Bay Games", Value = "10:00", Action = "Set as destination" },
            new UiDiaryEntry { Day = 14, Icon = UiIcon.Home, Title = "Rent for Bloom Cafe", Value = "$2,100", Action = "Open Business" },
            new UiDiaryEntry { Day = 14, Icon = UiIcon.Lightning, Title = "Electricity bill", Value = "$186", Action = "Open the bills" },
            new UiDiaryEntry { Day = 14, Icon = UiIcon.Baby, Title = "Maya's birthday" },
            new UiDiaryEntry { Day = 16, Icon = UiIcon.Scales, Title = "Court date, parking fine", Value = "09:00" },
            new UiDiaryEntry { Day = 18, Icon = UiIcon.ChartLineUp, Title = "Dividend from Gridline", Value = "$412" },
            new UiDiaryEntry { Day = 20, Icon = UiIcon.Hammer, Title = "Site inspection, Uptown", Value = "14:00" },
            new UiDiaryEntry { Day = 21, Icon = UiIcon.Coin, Title = "Wages, 6 staff", Value = "$2,910" },
            new UiDiaryEntry { Day = 21, Icon = UiIcon.Receipt, Title = "Water bill", Value = "$54" },
            new UiDiaryEntry { Day = 24, Icon = UiIcon.Building, Title = "Tender closes: Birch Row flats" },
            new UiDiaryEntry { Day = 25, Icon = UiIcon.MusicNotes, Title = "Concert at Broadway Hall", Value = "20:00" },
            new UiDiaryEntry { Day = 28, Icon = UiIcon.ShieldCheck, Title = "Insurance renews", Value = "$320" },
            new UiDiaryEntry { Day = 30, Icon = UiIcon.Receipt, Title = "Business taxes due", Value = "$4,050" },
        };
        return m;
    }

    public static void Diary(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool empty)
    {
        var nav = new[]
        {
            new UiNavEntry { Icon = UiIcon.Clock, Label = t("reallife_calendar_tab_day", "Day") }, new UiNavEntry { Icon = UiIcon.Calendar, Label = t("reallife_calendar_tab_month", "Month") },
        };
        UiAt at = ShapeWindow(s, f, dark, "diary", t("reallife_calendar_app", "Calendar"), UiIcon.Calendar, nav, 1, true, k, out float x, out float y, out float w, out float h);
        UiDiary.Lay(s, at, Month(t, empty), x, y, w, h, k);
        ShapePrompts(s, f, k, t, t("reallife_ui_prompt_page", "Page"));
    }

    // ------------------------------------------------------------------------------------------------- the board

    /// <summary>A café's Tuesday: its workstations, the shifts on them, the now line at half past two; or no workstations yet.</summary>
    public static UiBoardModel Schedule(UiWords t, bool empty, bool dragging)
    {
        var m = new UiBoardModel
        {
            Days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, Day = 1, FirstHour = 6, LastHour = 24, OpenFrom = 7 * 60, OpenTo = 22 * 60, Now = 14 * 60 + 30,
            Actions = new[]
            {
                new UiActionModel { Label = "Copy day", Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                new UiActionModel { Label = "Paste", Kind = UiButtonKind.Secondary },
                new UiActionModel { Label = "Clear day", Kind = UiButtonKind.Destructive },
                new UiActionModel { Label = "Fill gaps", Kind = UiButtonKind.Primary, Key = UiAction.Alt },
            },
            EmptyTitle = "No workstations yet",
            EmptyBody = "Place a register, a coffee machine or a desk in Build mode, then plan who works it and when.",
        };
        var hours = new string[18];
        for (int i = 0; i < hours.Length; i++)
        {
            hours[i] = (6 + i).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":00";
        }
        m.Hours = hours;
        if (empty)
        {
            m.Actions = new UiActionModel[0];   // nothing to copy, clear or fill until a workstation stands
            return m;
        }
        m.Lanes = new[]
        {
            new UiBoardLane { Name = "Register 1", Sub = "Cashier", Icon = UiIcon.Receipt, Tint = UiRole.Chart1 },
            new UiBoardLane { Name = "Register 2", Sub = "Cashier", Icon = UiIcon.Receipt, Tint = UiRole.Chart5, Warning = "No one after 18:00" },
            new UiBoardLane { Name = "Coffee machine", Sub = "Barista", Icon = UiIcon.Coffee, Tint = UiRole.Chart2, Warning = "Priya: 52 hours this week" },
            new UiBoardLane { Name = "Kitchen", Sub = "Cook", Icon = UiIcon.ForkKnife, Tint = UiRole.Chart3 },
            new UiBoardLane { Name = "Cleaning station", Sub = "Cleaner", Icon = UiIcon.Wrench, Tint = UiRole.Chart7 },
            new UiBoardLane { Name = "Storage", Sub = "Stocker", Icon = UiIcon.Package, Tint = UiRole.Chart4 },
            new UiBoardLane { Name = "Office desk", Sub = "Manager", Icon = UiIcon.Briefcase, Tint = UiRole.Chart6 },
            new UiBoardLane { Name = "Delivery bay", Sub = "Driver", Icon = UiIcon.Truck, Tint = UiRole.Chart1 },
            new UiBoardLane { Name = "Terrace", Sub = "Waiter", Icon = UiIcon.Sun, Tint = UiRole.Chart5 },
            new UiBoardLane { Name = "Pastry counter", Sub = "Baker", Icon = UiIcon.Store, Tint = UiRole.Chart2 },
            new UiBoardLane { Name = "Drive-through", Sub = "Cashier", Icon = UiIcon.Car, Tint = UiRole.Chart3 },
            new UiBoardLane { Name = "Back office", Sub = "Accountant", Icon = UiIcon.Receipt, Tint = UiRole.Chart7 },
            new UiBoardLane { Name = "Loading dock", Sub = "Stocker", Icon = UiIcon.Package, Tint = UiRole.Chart4 },
            new UiBoardLane { Name = "Security desk", Sub = "Guard", Icon = UiIcon.ShieldCheck, Tint = UiRole.Chart6 },
        };
        m.Shifts = new[]
        {
            new UiBoardShift { Lane = 0, Start = 7 * 60, End = 15 * 60, Name = "Maya Okafor" },
            new UiBoardShift { Lane = 0, Start = 15 * 60, End = 22 * 60, Name = "Tom Becker" },
            new UiBoardShift { Lane = 1, Start = 10 * 60, End = 18 * 60, Name = "Hana Sato" },
            new UiBoardShift { Lane = 2, Start = 7 * 60, End = 13 * 60, Name = "Luis Romero" },
            new UiBoardShift { Lane = 2, Start = 13 * 60, End = 22 * 60, Name = "Priya Raman", Warn = true },
            new UiBoardShift { Lane = 3, Start = 9 * 60, End = 17 * 60, Name = "Jonah Reyes" },
            new UiBoardShift { Lane = 4, Start = 6 * 60, End = 9 * 60, Name = "Elena Sokolova" },
            new UiBoardShift { Lane = 4, Start = 20 * 60, End = 23 * 60, Name = "Elena Sokolova" },
            new UiBoardShift { Lane = 5, Start = 8 * 60, End = 12 * 60, Name = "Kenji Mori" },
            new UiBoardShift { Lane = 6, Start = 9 * 60, End = 17 * 60, Name = "Ava Lindqvist" },
            new UiBoardShift { Lane = 7, Start = 6 * 60, End = 10 * 60, Name = "Marcus Hale" },
            new UiBoardShift { Lane = 8, Start = 11 * 60, End = 19 * 60, Name = "Rosa Stanley" },
            new UiBoardShift { Lane = 9, Start = 6 * 60, End = 12 * 60, Name = "Omar Haddad" },
            new UiBoardShift { Lane = 10, Start = 12 * 60, End = 20 * 60, Name = "Ines Duarte" },
        };
        if (dragging)
        {
            m.Shifts[2].Start = 9 * 60;
            m.Shifts[2].End = 18 * 60;
            m.Chosen = 2;
            m.Moving = true;
            m.MovingSpan = "09:00 to 18:00";
        }
        return m;
    }

    public static void Board(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool empty, bool dragging)
    {
        var nav = new[]
        {
            new UiNavEntry { Icon = UiIcon.Grid, Label = "Overview" }, new UiNavEntry { Icon = UiIcon.Calendar, Label = "Schedule" },
            new UiNavEntry { Icon = UiIcon.People, Label = "Employees", Count = empty ? null : "14" }, new UiNavEntry { Icon = UiIcon.Package, Label = "Items" },
            new UiNavEntry { Icon = UiIcon.Megaphone, Label = "Marketing" }, new UiNavEntry { Icon = UiIcon.Coin, Label = "Finance" },
        };
        UiAt at = ShapeWindow(s, f, dark, "board", "Bloom Cafe", UiIcon.Store, nav, 1, false, k, out float x, out float y, out float w, out float h);
        UiBoard.Lay(s, at, Schedule(t, empty, dragging), x, y, w, h, k);
        if (dragging)
        {
            s.AddPrompt(UiAction.Move, "Move");
            s.AddPrompt(UiAction.TabPrev, "Resize");
            s.AddPrompt(UiAction.Confirm, "Place");
            s.AddPrompt(UiAction.Back, "Cancel");
            UiDraw.PromptBar(s, f, k);
            return;
        }
        ShapePrompts(s, f, k, t, t("reallife_ui_prompt_page", "Page"));
    }

    // ------------------------------------------------------------------------------------------------- the reader

    /// <summary>The Guide's longest page in the reader, its facts filled as the Guide fills them in the game, scrolled to a section.</summary>
    public static UiReaderModel GuidePage(string text, int reveal)
    {
        var m = new UiReaderModel
        {
            Title = "Construction company",
            Blocks = UiReaderRules.Parse(Facts(Illustrated(text))),
            Picture = UiIcon.Hammer, PictureCaption = "Build on land you buy in Uptown and Cedar Heights, with your own crews or a hired builder.",
            FactsTitle = "In short",
            Facts = new[]
            {
                new UiPairModel { Key = "Licence fee", Value = "$12,000" }, new UiPairModel { Key = "Licence decided in", Value = "7 days" },
                new UiPairModel { Key = "Lots at most", Value = "6" }, new UiPairModel { Key = "Projects at once", Value = "4" },
                new UiPairModel { Key = "Land tax", Value = "1.2% a year" }, new UiPairModel { Key = "Crews at most", Value = "5" },
            },
            RelatedTitle = "See also",
            Related = new[] { "Hiring a builder", "Uptown and Cedar Heights", "Builders and hedging", "Land and lots", "The council", "Banks and loans" },
            Reveal = reveal,
        };
        return m;
    }

    // The page with the blocks a Guide page can carry beyond its lines: a picture under a section's head, a note and a link
    // to the page it names, so each of the reader's kinds is seen on its longest page.
    private static string Illustrated(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        int plans = text.IndexOf("# The plans", StringComparison.Ordinal);
        int building = plans < 0 ? -1 : text.IndexOf("\n# ", plans + 1, StringComparison.Ordinal);
        if (building > 0)
        {
            int line = text.IndexOf('\n', building + 1);
            text = text.Insert(line + 1, "![The frame stage at Birch Row, with its tower crane](Hammer)\n");
        }
        int licence = text.IndexOf("# The licence", StringComparison.Ordinal);
        if (licence > 0)
        {
            text = text.Insert(licence, "> No company of your own? One of the city's builders builds for you: see the page on hiring one.\n[Hiring a builder](8)\n\n");
        }
        return text;
    }

    // The Guide's facts as the game fills them: each {name} a figure of its kind, so no name of a fact reaches the page.
    private static string Facts(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            int close = text[i] == '{' ? text.IndexOf('}', i + 1) : -1;
            if (close < 0)
            {
                sb.Append(text[i]);
                continue;
            }
            string name = text.Substring(i + 1, close - i - 1);
            int seed = 0;
            for (int c = 0; c < name.Length; c++)
            {
                seed = seed * 31 + name[c];
            }
            seed = Math.Abs(seed % 9);
            sb.Append(name.EndsWith("_days", StringComparison.Ordinal) ? (3 + seed).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : name.EndsWith("_hours", StringComparison.Ordinal) ? (2 + seed).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : name.EndsWith("_weeks", StringComparison.Ordinal) || name.EndsWith("_years", StringComparison.Ordinal) ? (1 + seed % 4).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : name.Contains("fee") || name.Contains("cost") || name.Contains("price") || name.Contains("deposit") || name.Contains("rent") || name.Contains("rate")
                  || name.Contains("upkeep") || name.Contains("excess") || name.Contains("least") && name.Contains("investor") ? "$" + (1200 + seed * 850).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
                : name.Contains("share") || name.Contains("tax") || name.Contains("off") || name.Contains("cut") || name.Contains("spread") || name.Contains("premium") || name.Contains("penalty")
                  || name.Contains("retention") || name.Contains("stage") || name.Contains("points") || name.Contains("raises") || name.Contains("back") ? (2 + seed) + "%"
                : (2 + seed).ToString(System.Globalization.CultureInfo.InvariantCulture));
            i = close;
        }
        return sb.ToString();
    }

    public static void Reader(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string guide, int reveal)
    {
        UiAt at = ShapeWindow(s, f, dark, "reader", "Guide", UiIcon.Info, new UiNavEntry[0], 0, false, k, out float _, out float _, out float _, out float _);
        UiReader.LayWithContents(s, at, GuidePage(guide, reveal), f, k);
        ShapePrompts(s, f, k, t, "Section");
    }

    // ------------------------------------------------------------------------------------------------- the settings list

    private static readonly UiIcon[] GroupIcons = { UiIcon.Info, UiIcon.Person, UiIcon.Building, UiIcon.Car, UiIcon.Briefcase, UiIcon.Coin, UiIcon.Store, UiIcon.Gear, UiIcon.Lightning, UiIcon.Wrench };

    // The four sections of "Start here", which hold buttons rather than options.
    private static readonly string[][] StartButtons =
    {
        new[] { "Open the Guide" }, new[] { "Relaxed", "Standard", "Realistic", "Hardcore" }, new[] { "Back to the defaults", "Save my choices to a file", "Load choices from a file" },
        new[] { "Report a problem" },
    };

    /// <summary>
    /// The mod's settings as one list: every section of the options page and its options, about four hundred rows; a
    /// section whose main switch is off greys the rest; or what a search for some letters finds, marked.
    /// </summary>
    public static UiSettingsModel SettingsList(UiWords t, string query, int scrollToSection)
    {
        var rows = new List<UiSettingRow>(420);
        int buttons = 0;
        float scrollTo = 0f, at = 0f;
        for (int sct = 0; sct < SettingSections.Length; sct++)
        {
            string[] sec = SettingSections[sct];
            int group = int.Parse(sec[0], System.Globalization.CultureInfo.InvariantCulture);
            bool off = sec[1] == "Family";   // its main switch off: the rest of its rows wait, greyed
            var head = new UiSettingRow { Control = UiSettingControl.None, Name = sec[1], Icon = GroupIcons[group % GroupIcons.Length] };
            var found = new List<UiSettingRow>(8);
            if (sec.Length == 2 && buttons < StartButtons.Length)
            {
                foreach (string label in StartButtons[buttons])
                {
                    found.Add(new UiSettingRow { Control = UiSettingControl.Action, Name = label, Value = label == "Standard" ? "In use" : "Open", Tip = null });
                }
                buttons++;
            }
            for (int o = 2; o + 2 < sec.Length; o += 3)
            {
                string kind = sec[o];
                bool main = kind.Length > 1 && kind[1] == 'M';
                int n = o / 3;
                var row = new UiSettingRow { Name = sec[o + 1], Tip = sec[o + 2], Greyed = off && !main, Differs = n % 7 == 3 };
                switch (kind[0])
                {
                    case 'R':
                        row.Control = UiSettingControl.Slider;
                        row.Share = 0.2f + 0.6f * (n % 5) / 4f;
                        row.Value = SliderFigure(row.Name, row.Share);
                        break;
                    case 'C':
                        row.Control = UiSettingControl.Choice;
                        row.Value = n % 2 == 0 ? "Standard" : "As the game has it";
                        break;
                    default:
                        row.Control = UiSettingControl.Switch;
                        row.On = main ? !off : n % 4 != 1;
                        row.Value = row.On ? "On" : "Off";
                        break;
                }
                found.Add(row);
            }
            if (!string.IsNullOrEmpty(query))
            {
                for (int i = found.Count - 1; i >= 0; i--)
                {
                    int match = found[i].Name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (match < 0)
                    {
                        found.RemoveAt(i);
                        continue;
                    }
                    UiSettingRow r = found[i];
                    r.MatchStart = match;
                    r.MatchLength = query.Length;
                    found[i] = r;
                }
                if (found.Count == 0)
                {
                    continue;
                }
            }
            if (sct == scrollToSection)
            {
                scrollTo = at;
            }
            rows.Add(head);
            at += UiSettingsList.SectionHeight;
            foreach (UiSettingRow r in found)
            {
                rows.Add(r);
                at += UiSettingsList.OptionHeight;
            }
        }
        int options = 0;
        foreach (UiSettingRow r in rows)
        {
            options += r.Control != UiSettingControl.None ? 1 : 0;
        }
        return new UiSettingsModel
        {
            Rows = rows.ToArray(), RowCount = rows.Count, Offset = scrollTo, Query = query, SearchPlaceholder = "Search the options",
            Found = options.ToString(System.Globalization.CultureInfo.InvariantCulture) + " found", Preset = "Nearest preset: Standard", Differs = "4 choices differ",
            PresetActions = new[]
            {
                new UiActionModel { Label = "Show them", Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                new UiActionModel { Label = "Back to Standard", Kind = UiButtonKind.Secondary, Key = UiAction.Alt },
            },
            CanBack = true, CanForward = false, EmptyTitle = "No option has those words",
            EmptyBody = "Try a shorter word, or the name of a feature: police, bank, staff.",
        };
    }

    public static void Settings(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string query, int scrollToSection, int group)
    {
        UiAt at = ShapeWindow(s, f, dark, "settings", "Mod settings", UiIcon.Gear, new UiNavEntry[0], 0, false, k, out float _, out float _, out float _, out float _);
        UiSettingsList.LayWithGroups(s, at, SettingsList(t, query, scrollToSection), f, SettingGroups, GroupIcons, string.IsNullOrEmpty(query) ? group : -1, k, Found(query));
        ShapePrompts(s, f, k, t, null);
    }

    // A slider's figure at its place on its track, inside the range its name gives ("(0 to 10)") or as a share of a hundred.
    private static string SliderFigure(string name, float share)
    {
        int lo = 0, hi = 100;
        int open = name.LastIndexOf('(');
        int to = open >= 0 ? name.IndexOf(" to ", open, StringComparison.Ordinal) : -1;
        int close = to >= 0 ? name.IndexOf(')', to) : -1;
        if (close > 0)
        {
            int.TryParse(name.Substring(open + 1, to - open - 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out lo);
            int.TryParse(name.Substring(to + 4, close - to - 4), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hi);
        }
        return ((int)Math.Round(lo + share * (hi - lo))).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // How many options of each group a search finds, for the groups beside it; null with no search.
    private static int[] Found(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }
        var found = new int[SettingGroups.Length];
        for (int sct = 0; sct < SettingSections.Length; sct++)
        {
            string[] sec = SettingSections[sct];
            int group = int.Parse(sec[0], System.Globalization.CultureInfo.InvariantCulture);
            for (int o = 3; o < sec.Length; o += 3)
            {
                found[group] += sec[o].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
            }
        }
        return found;
    }

    /// <summary>A section of the settings by its title, for a sample scrolled to it.</summary>
    public static int SettingSection(string title)
    {
        for (int i = 0; i < SettingSections.Length; i++)
        {
            if (SettingSections[i][1] == title)
            {
                return i;
            }
        }
        return 0;
    }

    // ------------------------------------------------------------------------------------------------- the conversation

    /// <summary>Maya asks for Sundays off: ten lines, the choices with what each does, and an offer to make.</summary>
    public static UiConversationModel Talk(UiWords t, bool docked)
    {
        if (docked)
        {
            return new UiConversationModel
            {
                Title = "Lease renewal", Name = "Victor Hale", Hue = 210f, Ring = UiRole.Warn, Role = "Landlord, Hudson Realty", Status = "Firm", StatusTone = UiRole.Warn,
                ThreadStart = "At 70 Fifth Avenue",
                Messages = new[]
                {
                    new UiMessageModel { From = UiFrom.Them, Text = "Good to see you. The lease on the flat ends next month." },
                    new UiMessageModel { From = UiFrom.Them, Text = "Rents in Midtown went up again. I'm asking $1,320 a month from April." },
                    new UiMessageModel { From = UiFrom.Me, Text = "I've paid on time for two years. That has to count for something." },
                    new UiMessageModel { From = UiFrom.Them, Text = "It does. I could do $1,240 if you sign for a year." },
                    new UiMessageModel { From = UiFrom.System, Text = "Victor will wait for your answer until Friday." },
                },
                MessageCount = 5,
                Form = UiFormKind.Amount, FormLabel = "Your offer a month", FormValue = "$1,180", FormHint = "Similar flats on Fifth Avenue let for $1,150 to $1,350.",
                Choices = new[]
                {
                    new UiDecisionChoice { Name = "Offer $1,180 a month", Lines = new[] { new UiConsequence { Text = "He may say no", Tone = -1 }, new UiConsequence { Text = "Rent $60 a month under his", Tone = 1, Change = -1 } } },
                    new UiDecisionChoice { Name = "Sign for a year at $1,240", Lines = new[] { new UiConsequence { Text = "The rent is fixed until next April", Tone = 1 } } },
                    new UiDecisionChoice { Name = "Leave it for now", Note = "Until Friday", Lines = new[] { new UiConsequence { Text = "The lease ends if nothing is signed", Tone = -1 } } },
                },
                Prompts = new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
                    new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
                },
                PromptCount = 3,
            };
        }
        return new UiConversationModel
        {
            Name = "Maya Okafor", Hue = 24f, Ring = UiRole.Ok, Role = "Customer Service, Bloom Cafe", Status = "On shift", StatusTone = UiRole.Ok,
            ThreadStart = "Start of your messages with Maya Okafor",
            Messages = new[]
            {
                new UiMessageModel { From = UiFrom.Them, Text = "Morning. The cafe is busy today.", Time = "07:58" },
                new UiMessageModel { From = UiFrom.Me, Text = "Good. Is the new grinder working?", Time = "08:02" },
                new UiMessageModel { From = UiFrom.Them, Text = "It is, much faster than the old one.", Time = "08:05" },
                new UiMessageModel { From = UiFrom.Them, Text = "Could I drop the Sunday shift? I've picked up a class on weekends.", Time = "09:14" },
                new UiMessageModel { From = UiFrom.Me, Text = "Which class?", Time = "09:16" },
                new UiMessageModel { From = UiFrom.Them, Text = "Accounting at the college. It runs until June.", Time = "09:18" },
                new UiMessageModel { From = UiFrom.Them, Text = "Happy to cover Tuesday evenings instead.", Time = "09:19" },
                new UiMessageModel { From = UiFrom.System, Text = "Maya has worked 46 hours this week." },
                new UiMessageModel { From = UiFrom.Me, Text = "Let me look at the rota tonight.", Time = "09:20" },
                new UiMessageModel { From = UiFrom.Them, Text = "Thanks. It would mean a lot.", Time = "09:21" },
            },
            MessageCount = 10,
            Form = UiFormKind.Amount, FormLabel = "Hourly wage", FormValue = "$24", FormHint = "She earns $22 an hour now.",
            Choices = new[]
            {
                new UiDecisionChoice
                {
                    Name = "Give her Sundays off", Lines = new[] { new UiConsequence { Text = "Satisfaction +8", Tone = 1, Change = 1 }, new UiConsequence { Text = "Sunday needs another barista", Tone = -1 } }
                },
                new UiDecisionChoice { Name = "Swap her with Tom on Tuesdays", Lines = new[] { new UiConsequence { Text = "Satisfaction +4", Tone = 1, Change = 1 } } },
                new UiDecisionChoice { Name = "Say no for now", Lines = new[] { new UiConsequence { Text = "Satisfaction -6", Tone = -1, Change = -1 } } },
            },
        };
    }

    // Contacts' people, twelve, the first four new.
    private static UiMessageListModel ContactsList(int selected)
    {
        var rows = new[]
        {
            new UiMessageRow { Name = "Luis Romero", Hue = 30f, Preview = "Van needs fuel before the afternoon run, and the left tyre looks low", Time = "10:02", Unread = 1, Online = true, Section = "New messages" },
            new UiMessageRow { Name = "Uncle Fred", Hue = 280f, Preview = "Proud of you, kid. Dinner on Sunday? Your aunt is cooking", Time = "09:02", Unread = 1, Online = true },
            new UiMessageRow { Name = "Anderson Recruitment Corp.", Icon = UiIcon.IdBadge, Preview = "Shall I send the shortlist over?", Time = "08:52", Unread = 2 },
            new UiMessageRow { Name = "City Office", Icon = UiIcon.Bank, Preview = "Fine of $125 deducted from your bank account for parking", Time = "07:40", Unread = 1 },
            new UiMessageRow { Name = "Maya Okafor", Hue = 24f, Preview = "Thanks. It would mean a lot.", Time = "09:21", Online = true, Section = "Earlier" },
            new UiMessageRow { Name = "McCain's eMarketing", Icon = UiIcon.Megaphone, Preview = "Click-through on your last campaign was up 18%", Time = "Mon" },
            new UiMessageRow { Name = "Hana Sato", Hue = 330f, Preview = "Requesting a better desk chair. My back is not happy", Time = "Mon", Online = true },
            new UiMessageRow { Name = "NY AutoTow Service", Icon = UiIcon.Truck, Preview = "Stuck somewhere? One call and we'll haul you home", Time = "Sun" },
            new UiMessageRow { Name = "Harbor Line Wholesale", Icon = UiIcon.Package, Preview = "Container of coffee beans landed. Your order ships tomorrow", Time = "Sat" },
            new UiMessageRow { Name = "Lumen and Oak Interiors", Icon = UiIcon.Sofa, Preview = "The walnut counters you asked about are back in stock", Time = "Fri" },
            new UiMessageRow { Name = "City Workforce Inc.", Icon = UiIcon.Briefcase, Preview = "Your HQ application is approved. Let us know a start date", Time = "Thu" },
            new UiMessageRow { Name = "Tom Becker", Hue = 200f, Preview = "Can do Tuesday evenings if Maya takes Sunday", Time = "Wed" },
        };
        if (selected >= 0)
        {
            rows[selected].Selected = true;
        }
        return new UiMessageListModel { Rows = rows, RowCount = rows.Length, SearchPlaceholder = "Search people, shops, agencies" };
    }

    public const float ListColumn = 400f;   // px: the message list beside a conversation, as Contacts' list stood

    public static void ConversationWindow(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var nav = new[]
        {
            new UiNavEntry { Icon = UiIcon.AddressBook, Label = "All contacts", Count = "13" }, new UiNavEntry { Icon = UiIcon.Person, Label = "General", Count = "3" },
            new UiNavEntry { Icon = UiIcon.Briefcase, Label = "Business", Count = "4" }, new UiNavEntry { Icon = UiIcon.IdBadge, Label = "Employees", Count = "3" },
            new UiNavEntry { Icon = UiIcon.Package, Label = "Imports and goods", Count = "1" }, new UiNavEntry { Icon = UiIcon.Sofa, Label = "Furniture", Count = "1" },
            new UiNavEntry { Icon = UiIcon.Flag, Label = t("contactcategoryname_rivals", "Rivals"), Count = "1" },
        };
        UiAt at = ShapeWindow(s, f, dark, "conversation", "Contacts", UiIcon.AddressBook, nav, 0, false, k, out float x, out float y, out float w, out float h);
        UiMessageList.Lay(s, at.InRegion(4), ContactsList(4), x, y, ListColumn, h);
        float cx = x + ListColumn + UiTokens.S6;
        UiDraw.Box(s, at, cx - UiTokens.S3, y, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        UiConversation.Lay(s, at.InRegion(5), Talk(t, false), cx, y, x + w - cx, h, k);
        UiDraw.JoinColumns(s, x, cx - UiTokens.S3, x + w);
        ShapePrompts(s, f, k, t, null);
    }

    public static void ConversationDocked(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t) => UiConversation.LayDocked(s, Talk(t, true), f, dark, k);

    // ------------------------------------------------------------------------------------------------- Flare

    private static UiNavEntry[] FlareNav(UiWords t) => new[]
    {
        new UiNavEntry { Icon = UiIcon.Fire, Label = "Feed" }, new UiNavEntry { Icon = UiIcon.Message, Label = "Messages", Count = "12" },
        new UiNavEntry { Icon = UiIcon.ChartLineUp, Label = "Trends" }, new UiNavEntry { Icon = UiIcon.Person, Label = "Profile" },
        new UiNavEntry { Icon = UiIcon.Megaphone, Label = "Adverts" },
    };

    /// <summary>Flare's messages: twelve threads, one open, with the player's own words to send.</summary>
    public static void Messages(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        UiAt at = ShapeWindow(s, f, dark, "messages", "Flare", UiIcon.Fire, FlareNav(t), 1, false, k, out float x, out float y, out float w, out float h);
        var list = new UiMessageListModel
        {
            SearchPlaceholder = "Search messages",
            Rows = new[]
            {
                new UiMessageRow { Name = "Bloom Cafe fans", Icon = UiIcon.Coffee, Preview = "Is the oat latte back on the menu this week?", Time = "11:40", Unread = 3, Section = "New messages" },
                new UiMessageRow { Name = "Dana Whitfield", Hue = 140f, Preview = "Can I tag you in my review?", Time = "11:12", Online = true, Selected = true },
                new UiMessageRow { Name = "Midtown Eats", Icon = UiIcon.ForkKnife, Preview = "We'd like to feature your pastries in Friday's post", Time = "10:30", Unread = 1 },
                new UiMessageRow { Name = "Gridline Studio", Icon = UiIcon.GameController, Preview = "Collab idea for the launch of Neon Laps", Time = "09:05", Unread = 2 },
                new UiMessageRow { Name = "Sam Carver", Hue = 60f, Preview = "Saw your post about hiring. Still looking for a barista?", Time = "Mon", Online = true, Section = "Earlier" },
                new UiMessageRow { Name = "Harbor Days Market", Icon = UiIcon.Store, Preview = "Stall fees for the spring market are now open", Time = "Mon" },
                new UiMessageRow { Name = "Ivy Moreno", Hue = 300f, Preview = "Thank you for the free coffee card", Time = "Sun" },
                new UiMessageRow { Name = "Uptown Builders Guild", Icon = UiIcon.Hammer, Preview = "Members' night at the Birch Row site, Thursday", Time = "Sat" },
                new UiMessageRow { Name = "Noah Brandt", Hue = 220f, Preview = "Your follower count jumped this week", Time = "Fri" },
                new UiMessageRow { Name = "Lantern Bay Games", Icon = UiIcon.GameController, Preview = "Playtest invitations go out next week", Time = "Thu" },
                new UiMessageRow { Name = "Cedar Heights Living", Icon = UiIcon.Home, Preview = "Three new flats near your cafe", Time = "Wed" },
                new UiMessageRow { Name = "Priya Raman", Hue = 15f, Preview = "Shared your post with the design group", Time = "Tue" },
            },
            RowCount = 12,
        };
        UiMessageList.Lay(s, at.InRegion(4), list, x, y, ListColumn, h);
        float cx = x + ListColumn + UiTokens.S6;
        UiDraw.Box(s, at, cx - UiTokens.S3, y, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        var talk = new UiConversationModel
        {
            Name = "Dana Whitfield", Hue = 140f, Ring = UiRole.Ok, Role = "@danaeats, 4.2k followers", Status = "Online", StatusTone = UiRole.Ok,
            ThreadStart = "You follow each other",
            Messages = new[]
            {
                new UiMessageModel { From = UiFrom.Them, Text = "Hi, I write @danaeats. Are you open on Saturday morning?", Time = "Mon" },
                new UiMessageModel { From = UiFrom.Me, Text = "From seven. The new menu starts that day.", Time = "Mon" },
                new UiMessageModel { From = UiFrom.Them, Text = "Perfect, I'll come early with a friend.", Time = "Mon" },
                new UiMessageModel { From = UiFrom.System, Text = "Dana posted about Bloom Cafe on Saturday. 512 people liked it." },
                new UiMessageModel { From = UiFrom.Them, Text = "Loved the opening. The cinnamon buns were gone by ten.", Time = "11:10" },
                new UiMessageModel { From = UiFrom.Them, Text = "Can I tag you in my review?", Time = "11:12" },
            },
            MessageCount = 6, Typing = true, Composer = true, ComposerPlaceholder = "Write a message", Draft = "Of course, thank you for coming", SendLabel = t("reallife_ui_contacts_send", "Send"),
        };
        UiConversation.Lay(s, at.InRegion(5), talk, cx, y, x + w - cx, h, k);
        UiDraw.JoinColumns(s, x, cx - UiTokens.S3, x + w);
        ShapePrompts(s, f, k, t, null);
    }

    /// <summary>Flare's feed: posts with pictures, the player's own account beside them.</summary>
    public static void Feed(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        UiAt at = ShapeWindow(s, f, dark, "feed", "Flare", UiIcon.Fire, FlareNav(t), 0, false, k, out float x, out float y, out float w, out float h);
        var m = new UiFeedModel
        {
            Posts = new[]
            {
                new UiFeedPost
                {
                    Author = "Bloom Cafe", Icon = UiIcon.Coffee, Handle = "@bloomcafe", Time = "2h", Text = "Our spring menu is here: oat lattes, rhubarb buns and a new house blend roasted in Industry City.",
                    Picture = UiIcon.Coffee, Caption = "The new house blend, on the counter at 14 Bleecker Street", Likes = "1,284", Comments = "96", Shares = "41", Liked = true
                },
                new UiFeedPost
                {
                    Author = "Dana Whitfield", Hue = 140f, Handle = "@danaeats", Time = "3h", Text = "Best cinnamon buns in Midtown, and I have tried them all. Get there before ten.",
                    Likes = "512", Comments = "38", Shares = "12"
                },
                new UiFeedPost
                {
                    Author = "City Ledger", Icon = UiIcon.Newspaper, Handle = "@cityledger", Time = "5h", Text = "Land prices in Uptown rose 12% this quarter as the Birch Row towers took shape.",
                    Picture = UiIcon.Building, Caption = "Birch Row from the north, Tuesday morning", Likes = "2,940", Comments = "310", Shares = "402"
                },
                new UiFeedPost
                {
                    Author = "Gridline Studio", Icon = UiIcon.GameController, Handle = "@gridline", Time = "8h", Text = "Neon Laps hits the stores on Friday. Thank you to everyone who played the test.",
                    Picture = UiIcon.GameController, Likes = "3,108", Comments = "244", Shares = "190"
                },
            },
            PostCount = 4,
            Profile = new UiFeedProfile
            {
                Name = "Alex Carter", Hue = 200f, Handle = "@alexcarter", About = "Owner of Bloom Cafe on Bleecker Street. Coffee, early mornings and the odd tower in Uptown.",
                FactLabels = new[] { "Followers", "Following", "Posts" }, FactValues = new[] { "12.4k", "312", "86" },
                Actions = new[]
                {
                    new UiActionModel { Label = "New post", Kind = UiButtonKind.Primary, Icon = UiIcon.Plus, Key = UiAction.Alt },
                    new UiActionModel { Label = "Edit profile", Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                },
                ListTitle = "Trending in Midtown", List = new[] { "#SpringMenu", "#MidtownEats", "#BirchRow", "#NeonLaps", "#BleeckerStreet", "#CoffeeCrawl" },
                PeopleTitle = "People to follow",
                People = new[]
                {
                    new UiPersonRow { Name = "Sam Carver", Hue = 60f, Role = "@samcarver, barista in Hell's Kitchen" },
                    new UiPersonRow { Name = "Ivy Moreno", Hue = 300f, Role = "@ivymoreno, 8.1k followers" },
                    new UiPersonRow { Name = "Noah Brandt", Hue = 220f, Role = "@noahb, photos of Midtown" },
                    new UiPersonRow { Name = "Priya Raman", Hue = 15f, Role = "@priyadesigns, game designer" },
                    new UiPersonRow { Name = "Omar Haddad", Hue = 100f, Role = "@omarbakes, pastry chef" },
                },
            },
        };
        UiFeed.Lay(s, at, m, x, y, w, h, k);
        ShapePrompts(s, f, k, t, null);
    }

    // ------------------------------------------------------------------------------------------------- the edition

    /// <summary>The City Ledger on a busy Tuesday, or on a quiet one.</summary>
    public static UiEditionModel Paper(bool quiet)
    {
        if (quiet)
        {
            return new UiEditionModel
            {
                LeadIcon = UiIcon.CloudSun, LeadKicker = "Weather", LeadHeadline = "A mild week ahead, with rain on Thursday",
                LeadStandfirst = "Temperatures stay near 14 degrees until the weekend.",
                Markets = new[]
                {
                    new UiFigureModel { Label = "City index", Value = "1,268.2", Change = "+0.1%", Good = true, Trend = new[] { 1266f, 1267f, 1266.5f, 1268.2f } },
                    new UiFigureModel { Label = "Bank rate", Value = "4.25%" },
                },
                Sections = new[]
                {
                    new UiEditionSection
                    {
                        Icon = UiIcon.Building, Label = "City",
                        Stories = new[] { new UiEditionStory { Headline = "Road works on Fifth Avenue end early", Body = "The lanes north of 42nd Street open again on Wednesday.", Byline = "Midtown" } }
                    },
                    new UiEditionSection
                    {
                        Icon = UiIcon.Store, Label = "Business",
                        Stories = new[] { new UiEditionStory { Headline = "A bakery opens in Murray Hill", Body = "Its owner worked ten years in Lower Manhattan kitchens.", Byline = "By the business desk" } }
                    },
                },
                NoticesTitle = "Notices",
                Notices = new[] { new UiEditionNotice { Icon = UiIcon.Lightning, Text = "Power work in Garment District", Value = "02:00" } },
                QuietLine = "A quiet day. The paper fills as the city's businesses, markets and people make news.",
            };
        }
        return new UiEditionModel
        {
            LeadIcon = UiIcon.Building, LeadKicker = "City", LeadHeadline = "Uptown towers rise as land prices climb 12%",
            LeadStandfirst = "Birch Row's first tower tops out as buyers queue for the last lots in Cedar Heights.",
            Markets = new[]
            {
                new UiFigureModel { Label = "City index", Value = "1,284.6", Change = "+1.2%", Good = true, Trend = new[] { 1250f, 1262f, 1258f, 1271f, 1284.6f } },
                new UiFigureModel { Label = "Rent index", Value = "112.4", Change = "+2.1%", Good = false, Trend = new[] { 108f, 109.5f, 110f, 112.4f } },
                new UiFigureModel { Label = "Jobs this week", Value = "110,800", Change = "+312", Good = true, Trend = new[] { 109900f, 110200f, 110500f, 110800f } },
                new UiFigureModel { Label = "Bank rate", Value = "4.25%" },
            },
            Sections = new[]
            {
                new UiEditionSection
                {
                    Icon = UiIcon.Building, Label = "City",
                    Stories = new[]
                    {
                        new UiEditionStory { Headline = "Council backs new bus line to Cedar Heights", Body = "The route runs every twelve minutes from May and stops at the new school.", Byline = "Cedar Heights" },
                        new UiEditionStory { Headline = "Fifth Avenue road works end a week early", Body = "Crews finished the resurfacing north of 42nd Street on Monday night.", Byline = "Midtown" },
                        new UiEditionStory { Headline = "Snow is gone, the parks reopen", Body = "Central lawns are open again after three weeks behind fences.", Byline = "Around the city" },
                        new UiEditionStory { Headline = "Subway trains run late on the A line", Body = "Signal works north of Canal Street slow trains until Friday.", Byline = "Lower Manhattan" },
                    }
                },
                new UiEditionSection
                {
                    Icon = UiIcon.Store, Label = "Business",
                    Stories = new[]
                    {
                        new UiEditionStory { Headline = "Gridline Studio's Neon Laps sells 40,000 in a day", Body = "The racing game topped the store's chart within hours of its launch.", Byline = "By the business desk" },
                        new UiEditionStory { Headline = "Harbor Line Wholesale cuts its coffee prices", Body = "Cafes across Midtown should see cheaper beans from next week.", Byline = "Industry City" },
                        new UiEditionStory { Headline = "Three new shops sign for Bleecker Street", Body = "A florist, a bookshop and a tailor take the empty fronts by the cafe.", Byline = "Lower Manhattan" },
                        new UiEditionStory { Headline = "Office rents in Midtown hold for a third month", Body = "Landlords keep prices steady as two new towers open their floors.", Byline = "Midtown" },
                    }
                },
                new UiEditionSection
                {
                    Icon = UiIcon.People, Label = "People",
                    Stories = new[]
                    {
                        new UiEditionStory { Headline = "Birchwood School names its first head", Body = "A teacher of twenty years takes the post as pupils start in September.", Byline = "Cedar Heights" },
                        new UiEditionStory { Headline = "A tycoon gives $2 million to the city's parks", Body = "The gift pays for new paths and lights in Garment District's square.", Byline = "Garment District" },
                    }
                },
                new UiEditionSection
                {
                    Icon = UiIcon.MusicNotes, Label = "Around town",
                    Stories = new[]
                    {
                        new UiEditionStory { Headline = "Broadway Hall sells out its spring season", Body = "Late shows run until midnight on Fridays and Saturdays.", Byline = "Hell's Kitchen" },
                    }
                },
            },
            NoticesTitle = "Notices",
            Notices = new[]
            {
                new UiEditionNotice { Icon = UiIcon.Wrench, Text = "Water off in Murray Hill", Value = "02:00" },
                new UiEditionNotice { Icon = UiIcon.Car, Text = "Lane closed on 7th Avenue", Value = "All day" },
                new UiEditionNotice { Icon = UiIcon.Bank, Text = "Bank rate held", Value = "4.25%" },
                new UiEditionNotice { Icon = UiIcon.Calendar, Text = "Council sits on Thursday", Value = "10:00" },
            },
        };
    }

    public static void Edition(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool quiet)
    {
        var nav = new[]
        {
            new UiNavEntry { Icon = UiIcon.Newspaper, Label = "Today" }, new UiNavEntry { Icon = UiIcon.ChartLineUp, Label = "Markets" },
            new UiNavEntry { Icon = UiIcon.Calendar, Label = "Past editions" },
        };
        UiAt at = ShapeWindow(s, f, dark, "edition", quiet ? "The City Ledger, Monday, day 411" : "The City Ledger, Tuesday, day 412", UiIcon.Newspaper, nav, 0, true, k,
            out float x, out float y, out float w, out float h);
        UiEdition.Lay(s, at, Paper(quiet), x, y, w, h, k);
        ShapePrompts(s, f, k, t, null);
    }
}
