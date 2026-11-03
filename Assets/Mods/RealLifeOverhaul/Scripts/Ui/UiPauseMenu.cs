using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One line of the city's card beside the pause menu: its glyph, what it is and its figure.</summary>
    public struct UiPauseFact
    {
        public UiIcon Icon;
        public string Label;     // "Money"
        public string Value;     // "$48,210", in the game's own format
    }

    /// <summary>One of this city's saves in the recent saves card: its name, when, and whether it may be loaded or saved over.</summary>
    public struct UiPauseSave
    {
        public string Name;      // the save's name as the game lists it
        public string Line;      // "Day 412, 14 minutes ago"
        public bool Current;     // the name the game will save under now
        public bool Auto;        // the game's own autosave: loaded, never saved over by name
        public bool NoLoad;      // the game holds loading now (a shared session)
    }

    /// <summary>What the pause menu shows: the day and time, the save's name and when it was last written, the game's rows as they stand, the city's card and its recent saves.</summary>
    public sealed class UiPauseModel
    {
        public string Title;                     // "Paused"
        public string When;                      // "Day 12, 08:45": the game's day and time where the clock stopped
        public string NameLabel;                 // "Save name"
        public string Name;                      // the name the game will save under (its own field's words)
        public string NamePlaceholder;           // the game's own placeholder ("Name")
        public bool NameDisabled;                // the game holds its field (another player's session names the save)
        public bool Typing;                      // the name is being typed now: the caret shows
        public string Saved;                     // "Last saved 14 minutes ago, autosave every 10 minutes"; null for none
        public UiMenuRowModel[] Rows = new UiMenuRowModel[0];
        public string Back;                      // "Back to the city"
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        public string CityTitle;                 // "This city"
        public string CityNote;                  // the weekday, at the strip's right
        public UiPauseFact[] Facts = new UiPauseFact[0];
        public string SavesTitle;                // "Recent saves"
        public string SavesCount;                // "4 of 9"; null when all show
        public UiPauseSave[] Saves = new UiPauseSave[0];
        public string SavesEmpty;                // the sentence while this city has no save on disk
        public string Load, Over;                // "Load", "Save over"
    }

    /// <summary>
    /// The pause menu (the game's MiniMenu), as a game's pause screen: a slate panel over the world dimmed to half, its
    /// title strip saying it is paused with the day and time at its right, the save's name in a field to type in with when
    /// the game last saved under it, then the game's own rows (save game, the main one on the cyan with its F; options with
    /// its P; blueprints; then leaving: the main menu, saving and leaving, quitting in the danger's ink) large and full
    /// width with their glyphs, and the way back to the city with its key at its foot. Beside it, the city at a glance (its
    /// money, net worth, businesses, people, day and the time played) and its recent saves, each to load or to save over.
    /// Commands: <see cref="NameCommand"/> (typing the name), <see cref="RowCommand"/> (a row's index), <see cref="BackCommand"/>,
    /// <see cref="LoadCommand"/> and <see cref="OverCommand"/> (a save's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPauseMenu
    {
        public const int NameCommand = 1100, RowCommand = 1101, BackCommand = 1102, LoadCommand = 1103, OverCommand = 1104;
        public static readonly int[] Commands = { NameCommand, RowCommand, BackCommand, LoadCommand, OverCommand };

        public const float Width = 520f;                  // px: the menu, wide enough for the longest language's row in the large words
        public const float SideWidth = 480f;              // px: the city's card and the recent saves beside it
        public const int SavesMost = 4;                   // the recent saves listed: the latest few, as a game's quick list
        private const float SavedLines = 2;               // the last save's line wraps to two in a long language
        private const float Foot = UiTokens.S12;          // px: the way back at the panel's foot
        private const float FactRow = UiTokens.TableRowMost;   // px: a line of the city's card
        private const float SaveRow = UiTokens.Row;            // px: a save, its name over its day and age

        /// <summary>
        /// The line under the save's name: when the game last saved this city (its own saves and its autosave alike), in
        /// whole minutes or hours, then the game's autosave interval. Minutes -1 for no save yet. Game-free.
        /// </summary>
        public static string SavedLine(UiWords t, int savedMinutes, int autosaveMinutes)
        {
            string saved = savedMinutes < 0 ? t("reallife_ui_pause_unsaved", "Not saved since the city loaded")
                : savedMinutes == 0 ? t("reallife_ui_pause_saved_now", "Saved just now")
                : savedMinutes == 1 ? t("reallife_ui_pause_saved_minute", "Last saved a minute ago")
                : savedMinutes < 60 ? t("reallife_ui_pause_saved_minutes", "Last saved {minutes} minutes ago").Replace("{minutes}", savedMinutes.ToString(CultureInfo.InvariantCulture))
                : savedMinutes < 120 ? t("reallife_ui_pause_saved_hour", "Last saved an hour ago")
                : t("reallife_ui_pause_saved_hours", "Last saved {hours} hours ago").Replace("{hours}", (savedMinutes / 60).ToString(CultureInfo.InvariantCulture));
            if (autosaveMinutes <= 0)
            {
                return saved;
            }
            string auto = autosaveMinutes == 1 ? t("reallife_ui_pause_autosave_minute", "autosave every minute")
                : t("reallife_ui_pause_autosave", "autosave every {minutes} minutes").Replace("{minutes}", autosaveMinutes.ToString(CultureInfo.InvariantCulture));
            return t("reallife_ui_pause_saved_line", "{saved}, {autosave}").Replace("{saved}", saved).Replace("{autosave}", auto);
        }

        /// <summary>How long ago a save was written, in the largest whole unit that reads well; minutes -1 for not known. Game-free.</summary>
        public static string Ago(UiWords t, long minutes)
        {
            string n;
            if (minutes < 0)
            {
                return string.Empty;
            }
            if (minutes < 1)
            {
                return t("reallife_ui_pause_ago_now", "just now");
            }
            if (minutes < 2)
            {
                return t("reallife_ui_pause_ago_minute", "a minute ago");
            }
            if (minutes < 60)
            {
                n = minutes.ToString(CultureInfo.InvariantCulture);
                return t("reallife_ui_pause_ago_minutes", "{minutes} minutes ago").Replace("{minutes}", n);
            }
            if (minutes < 120)
            {
                return t("reallife_ui_pause_ago_hour", "an hour ago");
            }
            if (minutes < 24 * 60)
            {
                n = (minutes / 60).ToString(CultureInfo.InvariantCulture);
                return t("reallife_ui_pause_ago_hours", "{hours} hours ago").Replace("{hours}", n);
            }
            if (minutes < 48 * 60)
            {
                return t("reallife_ui_pause_ago_day", "yesterday");
            }
            n = (minutes / (24 * 60)).ToString(CultureInfo.InvariantCulture);
            return t("reallife_ui_pause_ago_days", "{days} days ago").Replace("{days}", n);
        }

        /// <summary>A save's second line: the game day it was made on and how long ago, either left out when not known. Game-free.</summary>
        public static string SaveLine(UiWords t, int day, long minutes)
        {
            string ago = Ago(t, minutes);
            if (day <= 0)
            {
                return ago;
            }
            string d = t("reallife_ui_pause_save_day", "Day {day}").Replace("{day}", day.ToString(CultureInfo.InvariantCulture));
            return ago.Length == 0 ? d : t("reallife_ui_pause_save_line", "{day}, {ago}").Replace("{day}", d).Replace("{ago}", ago);
        }

        /// <summary>The time played in a city: whole hours and minutes, or minutes alone under an hour. Game-free.</summary>
        public static string Played(UiWords t, long seconds)
        {
            long minutes = Math.Max(0L, seconds) / 60L;
            if (minutes < 1)
            {
                return t("reallife_ui_pause_played_none", "Under a minute");
            }
            string m = (minutes % 60L).ToString(CultureInfo.InvariantCulture);
            return minutes < 60
                ? t("reallife_ui_pause_played_minutes", "{minutes} min").Replace("{minutes}", m)
                : t("reallife_ui_pause_played_hours", "{hours} h {minutes} min").Replace("{hours}", (minutes / 60L).ToString(CultureInfo.InvariantCulture)).Replace("{minutes}", m);
        }

        /// <summary>The menu's height for its model.</summary>
        public static float Height(UiPauseModel m)
        {
            float inner = Width - UiTokens.S5 * 2f;
            float h = UiFrame.Strip + UiTokens.S4 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiTokens.Control;
            if (!string.IsNullOrEmpty(m.Saved))
            {
                h += UiTokens.S2 + UiDraw.Snap(UiTextRules.WrappedHeight(m.Saved, UiStyle.Small, inner, (int)SavedLines));
            }
            h += UiTokens.S4 + UiTokens.S2;   // the hairlines lie on the room's edges and take none of it
            for (int i = 0; i < m.Rows.Length; i++)
            {
                h += (m.Rows[i].Split && i > 0 ? UiTokens.S2 * 2f : 0f) + UiDraw.MenuRowLarge;
            }
            return UiDraw.Snap(h + UiTokens.S2 + Foot);
        }

        /// <summary>The city's card's height: its strip and a line a fact.</summary>
        public static float CityHeight(UiPauseModel m) => m.Facts.Length == 0 ? 0f : UiFrame.Strip + UiTokens.S2 * 2f + FactRow * m.Facts.Length;

        /// <summary>The recent saves card's height: its strip and a row a save, or its sentence while there is none.</summary>
        public static float SavesHeight(UiPauseModel m)
        {
            if (string.IsNullOrEmpty(m.SavesTitle))
            {
                return 0f;
            }
            float body = m.Saves.Length > 0 ? SaveRow * m.Saves.Length : UiDraw.Snap(UiDraw.EmptyStateHeight(SideWidth, null, m.SavesEmpty, null));
            return UiFrame.Strip + UiTokens.S2 * 2f + body;
        }

        /// <summary>The pause menu as a screen of its own: the world dimmed above the status bar, the menu and its side cards in its middle, the prompts on the bar.</summary>
        public static void Lay(UiScene s, UiPauseModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("pause_menu", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Held = frame.Held;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float h = Height(m), ch = CityHeight(m), sh = SavesHeight(m);
            bool side = ch + sh > 0f && frame.Width >= Width + UiTokens.S4 + SideWidth + UiTokens.S8 * 2f;
            float sideH = ch + (ch > 0f && sh > 0f ? UiTokens.S4 : 0f) + sh;
            float all = side ? Width + UiTokens.S4 + SideWidth : Width, tall = side ? Math.Max(h, sideH) : h;
            float x = UiDraw.SnapDown((frame.Width - all) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - tall) / 2f));
            Draw(s, m, x, y, keys, tall);   // the menu and the column beside it share one top and one bottom
            if (side)
            {
                // One gap between the cards, the columns' own; the last card stands to the menu's foot.
                float sx = x + Width + UiTokens.S4, sy = y;
                if (ch > 0f)
                {
                    City(s, m, sx, sy, sh > 0f ? ch : tall);
                    sy += ch + UiTokens.S4;
                }
                if (sh > 0f)
                {
                    Saves(s, m, sx, sy, keys, y + tall - sy);
                }
                UiDraw.JoinColumns(s, x, sx - UiTokens.S2, sx + SideWidth);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The menu at a place, at least <paramref name="height"/> tall (its foot at the bottom); returns its height.</summary>
        public static float Draw(UiScene s, UiPauseModel m, float x, float y, IUiKeys keys, float height = 0f)
        {
            float h = Math.Max(Height(m), height), inner = Width - UiTokens.S5 * 2f, ix = x + UiTokens.S5;
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, x, y, Width, UiIcon.Pause, m.Title, false);
            StripNote(s, at, x, y, Width, m.Title, m.When);
            cy += UiTokens.S4;
            UiDraw.Text(s, at, ix, cy, inner, UiStyle.SectionHeader, UiRole.Muted, m.NameLabel, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            Field(s, at, m, ix, cy, inner);
            cy += UiTokens.Control;
            if (!string.IsNullOrEmpty(m.Saved))
            {
                cy += UiTokens.S2;
                float sh = UiDraw.Snap(UiTextRules.WrappedHeight(m.Saved, UiStyle.Small, inner, (int)SavedLines));
                UiDraw.Text(s, at, ix, cy, inner, UiStyle.Small, UiRole.Muted, m.Saved, UiFlags.Label | UiFlags.Wrap, (int)SavedLines);
                cy += sh;
            }
            cy += UiTokens.S4;
            UiDraw.Box(s, at, x, cy, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            cy += UiTokens.S2;
            for (int i = 0; i < m.Rows.Length; i++)
            {
                if (m.Rows[i].Split && i > 0)
                {
                    cy += UiTokens.S2;
                    UiDraw.Box(s, at, ix, cy, inner, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
                    cy += UiTokens.S2;
                }
                UiDraw.MenuRow(s, at, x, cy, Width, ref m.Rows[i], RowCommand, i, keys, UiDraw.MenuRowLarge);   // edge to edge: the focus fills the panel's width
                cy += UiDraw.MenuRowLarge;
            }
            cy = y + h - Foot;   // the foot at the bottom, the rows' last gap above it (Height's S2) or more when the column beside is taller
            UiDraw.Box(s, at, x, cy, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            Back(s, at, m, x, cy, keys);
            if (s.FocusFirst < 0)
            {
                s.FocusFirst = s.FindNode(RowCommand, 0);   // focus starts on Save, never on the field or the way out
            }
            return h;
        }

        // Words at a strip's right, clear of its title centred in it.
        private static void StripNote(UiScene s, UiAt at, float x, float y, float w, string title, string note)
        {
            if (string.IsNullOrEmpty(note))
            {
                return;
            }
            float titleW = UiTextRules.Width(title, UiStyle.SectionTitle);
            float room = UiDraw.SnapDown((w - titleW) / 2f) - UiTokens.S3 * 2f;
            float ww = Math.Min(room, UiDraw.Snap(UiTextRules.Width(note, UiStyle.Small) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x + w - UiTokens.S4 - ww, y, UiFrame.Strip - UiTokens.Hairline, ww, UiStyle.Small, UiRole.Text2, note,
                UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
        }

        // The city at a glance: a line a figure, its glyph on a tile in its own colour, the figure at the right.
        private static void City(UiScene s, UiPauseModel m, float x, float y, float h)
        {
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, SideWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, x, y, SideWidth, UiIcon.Building, m.CityTitle, false) + UiTokens.S2;
            StripNote(s, at, x, y, SideWidth, m.CityTitle, m.CityNote);
            float ix = x + UiTokens.S5, inner = SideWidth - UiTokens.S5 * 2f, tile = FactRow - UiTokens.S1 * 2f;   // 36 in a 44 row: on the grid
            for (int i = 0; i < m.Facts.Length; i++, cy += FactRow)
            {
                ref UiPauseFact f = ref m.Facts[i];
                int t = UiDraw.Tile(s, at, ix, cy + (FactRow - tile) / 2f, tile, UiRole.None, false);
                UiDraw.Icon(s, at.In(t), ix + (tile - UiTokens.Icon) / 2f, cy + (FactRow - UiTokens.Icon) / 2f, UiTokens.Icon, f.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float lx = ix + tile + UiTokens.S3;
                float vw = Math.Min(inner / 2f, UiDraw.Snap(UiTextRules.Width(f.Value, UiStyle.Number) + UiTokens.S1));
                UiDraw.TextMiddle(s, at, ix + inner - vw, cy, FactRow, vw, UiStyle.Number, UiRole.Text, f.Value, UiFlags.Right | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, lx, cy, FactRow, ix + inner - vw - UiTokens.S3 - lx, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
            }
        }

        // This city's latest saves: the glyph (a save, or the game's autosave), the name over its day and age, Load and Save over at the right.
        private static void Saves(UiScene s, UiPauseModel m, float x, float y, IUiKeys keys, float h)
        {
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, SideWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, x, y, SideWidth, UiIcon.FloppyDisk, m.SavesTitle, false) + UiTokens.S2;
            StripNote(s, at, x, y, SideWidth, m.SavesTitle, m.SavesCount);
            float ix = x + UiTokens.S5, inner = SideWidth - UiTokens.S5 * 2f, tile = UiTokens.ControlSmall;
            if (m.Saves.Length == 0)
            {
                UiDraw.EmptyState(s, at, x, cy, SideWidth, y + h - cy, UiIcon.FloppyDisk, null, m.SavesEmpty, null, -1, keys);   // the card's room, down to the menu's foot
                return;
            }
            float overW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Over, UiIcon.None, UiAction.Count, keys);
            float loadW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Load, UiIcon.None, UiAction.Count, keys);
            // The card stands to the menu's foot: room to spare under the rows goes to the rows themselves, each taller by
            // the same whole step of 8 px so its tile and buttons stay on the grid.
            float room = y + h - UiTokens.S2 - cy, row = SaveRow, left = room - SaveRow * m.Saves.Length;
            if (left > UiCheckRules.MostDeadSpace)
            {
                row = (float)Math.Floor(room / m.Saves.Length / UiTokens.S2) * UiTokens.S2;
            }
            for (int i = 0; i < m.Saves.Length; i++, cy += row)
            {
                ref UiPauseSave sv = ref m.Saves[i];
                if (i > 0)
                {
                    UiDraw.Box(s, at, ix, cy, inner, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
                }
                int t = UiDraw.Tile(s, at, ix, cy + (row - tile) / 2f, tile, UiRole.None, false);
                UiIcon glyph = sv.Auto ? UiIcon.Reset : UiIcon.FloppyDisk;
                UiDraw.Icon(s, at.In(t), ix + (tile - UiTokens.Icon) / 2f, cy + (row - UiTokens.Icon) / 2f, UiTokens.Icon, glyph, sv.Auto ? UiRole.Text2 : UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                float bx = ix + inner - overW;
                UiDraw.Button(s, at, bx, cy + (row - UiTokens.ControlSmall) / 2f, overW, UiTokens.ControlSmall, UiButtonKind.Secondary, m.Over, UiIcon.None, UiAction.Count,
                    keys, OverCommand, i, sv.Auto || m.NameDisabled);   // never over an autosave, nor while the name is held (a shared session)
                bx -= UiTokens.S2 + loadW;
                UiDraw.Button(s, at, bx, cy + (row - UiTokens.ControlSmall) / 2f, loadW, UiTokens.ControlSmall, UiButtonKind.Secondary, m.Load, UiIcon.None, UiAction.Count,
                    keys, LoadCommand, i, sv.NoLoad);
                float lx = ix + tile + UiTokens.S3, lw = bx - UiTokens.S3 - lx;
                // The name over its line, each on the grid: the name's line rounded up to it, the pair centred and rounded down.
                float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), lh = UiTextRules.LineHeight(UiStyle.Small);
                float top = cy + UiDraw.SnapDown((row - nh - lh) / 2f);
                UiDraw.Text(s, at, lx, top, lw, UiStyle.Title, sv.Current ? UiRole.AccentText : UiRole.Text, sv.Name, UiFlags.Truncate);
                UiDraw.Text(s, at, lx, top + nh, lw, UiStyle.Small, UiRole.Muted, sv.Line, UiFlags.Truncate);
            }
        }

        // The save's name in the game's field: its words, or the placeholder, with the pencil at the right saying it is typed in.
        private static void Field(UiScene s, UiAt at, UiPauseModel m, float x, float y, float w)
        {
            float pen = UiTokens.Icon;
            bool typed = !string.IsNullOrEmpty(m.Name);
            // The words end clear of the pencil: cut here so the field's own room never runs under it.
            bool typing = m.Typing || UiDraw.Typing(s, NameCommand, 0);
            string words = typed && !typing ? UiTextRules.Cut(m.Name, UiStyle.Body, w - UiTokens.S4 * 2f - pen - UiTokens.S2) : m.Name;
            int field = UiDraw.TextField(s, at, x, y, w, words, m.NamePlaceholder, NameCommand, 0, m.NameDisabled, typing);
            if (!typing)
            {
                int face = s.Nodes[field].Face;
                UiDraw.Icon(s, at.In(face), x + w - UiTokens.S3 - pen, y + (UiTokens.Control - pen) / 2f, pen, UiIcon.Pencil, m.NameDisabled ? UiRole.Muted : UiRole.Text2,
                    UiFlags.Middle | UiFlags.Decor);   // over the field's own words, which are cut short of it
            }
        }

        // The way back to the city at the foot: the key that does it on its cap (the back glyph where no key shows), and the words.
        private static void Back(UiScene s, UiAt at, UiPauseModel m, float x, float y, IUiKeys keys)
        {
            int face = UiDraw.Box(s, at, x, y, Width, Foot, UiRole.None, UiRole.None, -UiTokens.RadiusPanel);
            UiAt on = at.In(face);
            bool cap = !s.Held && UiDraw.HasKey(UiAction.Back, keys);
            float lead = cap ? UiDraw.KeyCapWidth(UiAction.Back, keys) : UiTokens.IconSmall;
            float ww = Math.Min(Width - UiTokens.S8 * 2f - lead - UiTokens.S2, UiDraw.Snap(UiTextRules.Width(m.Back, UiStyle.Body) + UiTokens.S1));
            float bx = x + UiDraw.SnapDown((Width - lead - UiTokens.S2 - ww) / 2f);
            if (cap)
            {
                UiDraw.KeyCap(s, on, bx, y + (Foot - UiTokens.KeyCap) / 2f, UiAction.Back, keys, UiRole.Track, UiRole.Text);
            }
            else
            {
                UiDraw.Icon(s, on, bx, y + (Foot - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Back, UiRole.Text2, UiFlags.Middle);
            }
            UiDraw.TextMiddle(s, on, bx + lead + UiTokens.S2, y, Foot, ww, UiStyle.Body, UiRole.Text2, m.Back, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, Width, Foot, BackCommand, 0, face, false, -1, 0, UiAction.Back);
        }
    }
}
