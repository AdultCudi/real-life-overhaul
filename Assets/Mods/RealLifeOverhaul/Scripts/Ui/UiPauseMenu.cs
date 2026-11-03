using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the pause menu shows: the day and time, the save's name and when it was last written, the game's rows as they stand.</summary>
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
    }

    /// <summary>
    /// The pause menu (the game's MiniMenu), as Cities: Skylines II pauses: a narrow slate panel centred over the world
    /// dimmed to half, its title strip saying it is paused with the day and time at its right, the save's name in a field to
    /// type in with when the game last saved under it, then the game's own rows (save, options, blueprints; then leaving:
    /// the main menu, saving and leaving, quitting) full width with their glyphs, the one that loses unsaved play in the
    /// danger's ink, and at its foot the way back to the city. Commands: <see cref="NameCommand"/> (typing the name),
    /// <see cref="RowCommand"/> (a row's index) and <see cref="BackCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPauseMenu
    {
        public const int NameCommand = 1100, RowCommand = 1101, BackCommand = 1102;
        public static readonly int[] Commands = { NameCommand, RowCommand, BackCommand };

        public const float Width = 424f;                  // px: narrow, as a game's pause menu, wide enough for the longest row's words
        private const float SavedLines = 2;               // the last save's line wraps to two in a long language
        private const float Foot = UiTokens.Control;      // px: the way back at the panel's foot

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

        /// <summary>The panel's height for its model.</summary>
        public static float Height(UiPauseModel m)
        {
            float inner = Width - UiTokens.S4 * 2f;
            float h = UiFrame.Strip + UiTokens.S4 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiTokens.Control;
            if (!string.IsNullOrEmpty(m.Saved))
            {
                h += UiTokens.S2 + UiDraw.Snap(UiTextRules.WrappedHeight(m.Saved, UiStyle.Small, inner, (int)SavedLines));
            }
            h += UiTokens.S4 + UiTokens.S2;   // the hairlines lie on the room's edges and take none of it
            for (int i = 0; i < m.Rows.Length; i++)
            {
                h += (m.Rows[i].Split && i > 0 ? UiTokens.S2 * 2f : 0f) + UiDraw.MenuRowHeight;
            }
            return UiDraw.Snap(h + UiTokens.S2 + Foot);
        }

        /// <summary>The pause menu as a screen of its own: the world dimmed above the status bar, the panel in its middle, the prompts on the bar.</summary>
        public static void Lay(UiScene s, UiPauseModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("pause_menu", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Held = frame.Held;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, m, x, y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel at a place; returns its height.</summary>
        public static float Draw(UiScene s, UiPauseModel m, float x, float y, IUiKeys keys)
        {
            float h = Height(m), inner = Width - UiTokens.S4 * 2f, ix = x + UiTokens.S4;
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, x, y, Width, UiIcon.Pause, m.Title, false);
            if (!string.IsNullOrEmpty(m.When))
            {
                // The day and time at the strip's right, clear of the title centred in it.
                float titleW = UiTextRules.Width(m.Title, UiStyle.SectionTitle);
                float room = UiDraw.SnapDown((Width - titleW) / 2f) - UiTokens.S3 * 2f;
                float ww = Math.Min(room, UiDraw.Snap(UiTextRules.Width(m.When, UiStyle.Small) + UiTokens.S1));
                UiDraw.TextMiddle(s, at, x + Width - UiTokens.S3 - ww, y, UiFrame.Strip - UiTokens.Hairline, ww, UiStyle.Small, UiRole.Text2, m.When,
                    UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
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
                UiDraw.MenuRow(s, at, x, cy, Width, ref m.Rows[i], RowCommand, i, keys);   // edge to edge: the focus fills the panel's width
                cy += UiDraw.MenuRowHeight;
            }
            cy += UiTokens.S2;
            UiDraw.Box(s, at, x, cy, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            Back(s, at, m, x, cy, keys);
            if (s.FocusFirst < 0)
            {
                s.FocusFirst = s.FindNode(RowCommand, 0);   // focus starts on Save, never on the field or the way out
            }
            return h;
        }

        // The save's name in the game's field: its words, or the placeholder, with the pencil at the right saying it is typed in.
        private static void Field(UiScene s, UiAt at, UiPauseModel m, float x, float y, float w)
        {
            float pen = UiTokens.Icon;
            bool typed = !string.IsNullOrEmpty(m.Name);
            // The words end clear of the pencil: cut here so the field's own room never runs under it.
            string words = typed && !m.Typing ? UiTextRules.Cut(m.Name, UiStyle.Body, w - UiTokens.S4 * 2f - pen - UiTokens.S2) : m.Name;
            int field = UiDraw.TextField(s, at, x, y, w, words, m.NamePlaceholder, NameCommand, 0, m.NameDisabled, m.Typing);
            if (!m.Typing)
            {
                int face = s.Nodes[field].Face;
                UiDraw.Icon(s, at.In(face), x + w - UiTokens.S3 - pen, y + (UiTokens.Control - pen) / 2f, pen, UiIcon.Pencil, m.NameDisabled ? UiRole.Muted : UiRole.Text2,
                    UiFlags.Middle | UiFlags.Decor);   // over the field's own words, which are cut short of it
            }
        }

        // The way back to the city at the foot: the key that does it while the keys lead, else the back glyph, and the words.
        private static void Back(UiScene s, UiAt at, UiPauseModel m, float x, float y, IUiKeys keys)
        {
            int face = UiDraw.Box(s, at, x, y, Width, Foot, UiRole.None, UiRole.None, -UiTokens.RadiusPanel);
            UiAt on = at.In(face);
            bool cap = !s.Pointer && !s.Held && UiDraw.HasKey(UiAction.Back, keys);
            float lead = cap ? UiDraw.KeyCapWidth(UiAction.Back, keys) : UiTokens.IconSmall;
            float ww = Math.Min(Width - UiTokens.S8 * 2f - lead - UiTokens.S2, UiDraw.Snap(UiTextRules.Width(m.Back, UiStyle.Small) + UiTokens.S1));
            float bx = x + UiDraw.SnapDown((Width - lead - UiTokens.S2 - ww) / 2f);
            if (cap)
            {
                UiDraw.KeyCap(s, on, bx, y + (Foot - UiTokens.KeyCap) / 2f, UiAction.Back, keys, UiRole.Track, UiRole.Text);
            }
            else
            {
                UiDraw.Icon(s, on, bx, y + (Foot - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Back, UiRole.Text2, UiFlags.Middle);
            }
            UiDraw.TextMiddle(s, on, bx + lead + UiTokens.S2, y, Foot, ww, UiStyle.Small, UiRole.Text2, m.Back, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, Width, Foot, BackCommand, 0, face, false, -1, 0, UiAction.Back);
        }
    }
}
