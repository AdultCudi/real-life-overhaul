using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the game's Options in the city shows, as plain data: the categories down the sidebar with their glyphs, the page
    /// of the chosen one as the kitgrids settings list reads it, the line at the head of the window and, while a drop-down
    /// is open, its options. The same model draws the custom difficulty panel, whose sidebar holds its own sections.
    /// </summary>
    public sealed class UiOptionsModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Gear;
        public string Build;                              // "Game build 3682, 4 mods" at the right of the strip
        public string[] Groups = new string[0];
        public UiIcon[] Icons = new UiIcon[0];
        public int Chosen;                                // the group on view; -1 while a search spans them
        public int[] Found;                               // a search's hits in each group, null when none is made
        public readonly UiSettingsModel List = new UiSettingsModel();
        public string[] Choices;                          // the options of the drop-down that is open; null when none is
        public int ChoiceCount, ChoiceValue, ChoiceRow;
        public float ChoiceOffset;
        public int ChoiceClip = -1;                       // the clip the open list scrolls in, set as it is laid out
    }

    /// <summary>
    /// The window of the game's Options and of its custom difficulty panel on the kit: an app window with the groups down its
    /// sidebar and the kitgrids settings list beside them, exactly as Mod settings draws its own. Game-free: the screen
    /// (OptionsScreen) fills the model from the game's controls and presses them back. Cost: a layout a change.
    /// </summary>
    public static class UiOptions
    {
        public const string SceneName = "options";

        /// <summary>The left of the controls' column in px of the layout, where a slider's pointer is measured from and a drop-down opens.</summary>
        public static float ControlLeft(UiFrame frame)
        {
            frame.Window(out float wx, out float _, out float ww, out float _);
            float x = wx + UiAppWindow.Sidebar + UiTokens.S10 + UiTokens.S5;   // the sidebar's band, as the list lays it
            float w = wx + ww - UiTokens.S5 - x;
            return x + w - UiTokens.S3 - UiSettingsList.ControlWidth;
        }

        /// <summary>The top of the first row in px of the layout: under the search, when the page has one, and the band.</summary>
        public static float RowsTop(UiFrame frame, UiOptionsModel m)
        {
            frame.Window(out float _, out float wy, out float _, out float _);
            float y = wy + UiFrame.Strip + UiTokens.S4;
            if (!m.List.NoHead)
            {
                y += UiTokens.Control + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(m.List.Preset))
            {
                y += UiTokens.Control + UiTokens.S2 * 2f + UiTokens.S3;
            }
            return y;
        }

        /// <summary>Where the open drop-down's list stands: under its row, at the controls' column.</summary>
        public static void ChoiceAt(UiFrame frame, UiOptionsModel m, out float x, out float y)
        {
            float top = 0f;
            for (int i = 0; i < m.ChoiceRow && i < m.List.RowCount; i++)
            {
                top += UiSettingsList.RowHeight(ref m.List.Rows[i]);
            }
            x = ControlLeft(frame);
            y = RowsTop(frame, m) + top - m.List.Offset + UiSettingsList.OptionHeight + UiTokens.S1;
        }

        public static void Lay(UiScene s, UiFrame frame, UiOptionsModel m, bool dark, IUiKeys keys, UiWords t)
        {
            s.Begin(SceneName, frame.Width, frame.Height, dark);
            frame.Icon = m.Icon;
            UiAt window = UiAppWindow.Shell(s, frame, m.Title, m.Icon, new UiNavEntry[0], 0, false, keys, out float _, out float _, out float _, out float _);
            if (!string.IsNullOrEmpty(m.Build))
            {
                // The build and the mods at the strip's right, under the close button's reach: quiet, as a game's version line.
                frame.Window(out float wx, out float wy, out float ww, out float _);
                float room = UiTokens.S3 + UiTokens.MinTarget + UiTokens.S2 * 2f;
                float bw = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Build, UiStyle.Small) + UiTokens.S1), ww / 3f);
                UiDraw.TextMiddle(s, window, wx + ww - room - bw, wy, UiFrame.Strip, bw, UiStyle.Small, UiRole.Muted, m.Build, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            UiSettingsList.LayWithGroups(s, window, m.List, frame, m.Groups, m.Icons, m.Chosen, keys, m.Found);
            if (m.Choices != null && m.ChoiceRow >= 0 && m.ChoiceRow < m.List.RowCount)
            {
                ChoiceAt(frame, m, out float cx, out float cy);
                UiDraw.ChoiceList(s, UiAt.Screen, cx, cy, UiSettingsList.ControlWidth, m.Choices, m.ChoiceCount, m.ChoiceValue, m.ChoiceOffset, UiSettingsList.ChoiceCommand);
                m.ChoiceClip = s.ClipCount - 1;
            }
            s.AddPrompt(UiAction.Move, t("reallife_ui_prompt_move", "Move"));
            s.AddPrompt(UiAction.Confirm, t("reallife_ui_prompt_select", "Select"));
            s.AddPrompt(UiAction.Back, t("reallife_ui_prompt_back", "Back"));
            UiDraw.PromptBar(s, frame, keys);
        }
    }
}
