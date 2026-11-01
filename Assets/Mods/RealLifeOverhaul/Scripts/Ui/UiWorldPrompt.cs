using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One action of a world prompt: its words, the game's key that does it, and whether it can be done now.</summary>
    public struct UiPromptAction
    {
        public string Label;       // "Enter", "Buy a coffee"
        public UiGameKey Key;      // the game's key (Interact, Secondary, Special, Confirm), which the game reads
        public bool Main;          // the one the game's Interact does: on the cyan
        public bool Disabled;      // the game greys it (ButtonInfo.interactable): seen, but a press does nothing
    }

    /// <summary>
    /// What a box over a place in the world says: the thing's glyph and name, up to three rows, and up to four actions each
    /// with its key, or (the hover form, for an item under the pointer) one figure and no actions. One model for the game's
    /// OverlayUI (IOverlay: its first line, its second line's left and right, its ButtonInfo) and the mod's WorldPanel and
    /// DoorPanel.
    /// </summary>
    public sealed class UiWorldPromptModel
    {
        public const int MostRows = 3, MostActions = 4;

        public string Title;                                       // "70 Fifth Avenue"
        public UiIcon Icon = UiIcon.Building;
        public UiInfoRow[] Rows = new UiInfoRow[0];
        public UiPromptAction[] Actions = new UiPromptAction[0];
        public bool Hover;                                         // the hover form: Figure and FigureLabel, no actions
        public string FigureLabel;                                 // "Price"
        public string Figure;                                      // "$4.50"
    }

    /// <summary>
    /// The world prompt: the small slate box at a door, a desk, a person or an item, standing beside its point of the world
    /// (UiHudRules.Beside clamps it inside the screen above the status bar and off a docked panel). Its title strip (the
    /// thing's glyph, its name), its rows on a band, and its actions as rows each led by the game's key on its cap, the
    /// one Interact does on the cyan; the game reads the keys, the mouse presses a row. The hover form is the strip and one
    /// figure. Commands: <see cref="ActionCommand"/> (an action's index). Game-free. Cost: a layout a change; following its
    /// point moves the box, never lays it out again.
    /// </summary>
    public static class UiWorldPrompt
    {
        public const int ActionCommand = 200;
        public const float Width = 320f;                     // px: as narrow as a game's world box, a long address cut at a word
        private const float ActionRow = UiTokens.ControlSmall;   // px: an action, its key and its words

        /// <summary>The box's height for its model.</summary>
        public static float Height(UiWorldPromptModel m)
        {
            float h = UiFrame.Strip;
            int rows = m.Hover ? (string.IsNullOrEmpty(m.Figure) ? 0 : 1) : Math.Min(UiWorldPromptModel.MostRows, m.Rows.Length);
            if (rows > 0)
            {
                h += UiTokens.S2 * 2f + rows * UiTokens.PairRow;
            }
            int actions = m.Hover ? 0 : Math.Min(UiWorldPromptModel.MostActions, m.Actions.Length);
            if (actions > 0)
            {
                h += UiTokens.S2 + actions * ActionRow + (actions - 1) * UiTokens.S1 + UiTokens.S2;
            }
            return UiDraw.Snap(h);
        }

        /// <summary>Lays the box at a place (its top left, px of the layout); returns its panel.</summary>
        public static int Lay(UiScene s, UiAt at, UiWorldPromptModel m, float x, float y)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, false);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            int rows = m.Hover ? (string.IsNullOrEmpty(m.Figure) ? 0 : 1) : Math.Min(UiWorldPromptModel.MostRows, m.Rows.Length);
            if (rows > 0)
            {
                float bh = UiTokens.S2 * 2f + rows * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, Width, bh);
                float ry = cy + UiTokens.S2;
                if (m.Hover)
                {
                    UiDraw.InfoRow(s, on, ix, ry, iw, m.FigureLabel, m.Figure);
                }
                for (int i = 0; !m.Hover && i < rows; i++)
                {
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, m.Rows[i].Label, m.Rows[i].Value, false, -1, 0, m.Rows[i].Ink == UiRole.None ? UiRole.Text : m.Rows[i].Ink);
                }
                cy += bh;
            }
            int actions = m.Hover ? 0 : Math.Min(UiWorldPromptModel.MostActions, m.Actions.Length);
            float ay = cy + UiTokens.S2;
            for (int i = 0; i < actions; i++)
            {
                Action(s, on, ref m.Actions[i], i, x + UiTokens.S2, ay, Width - UiTokens.S2 * 2f);
                ay += ActionRow + UiTokens.S1;
            }
            return panel;
        }

        // An action's row: the game's key on its cap, its words; the main one on the cyan, a greyed one dimmed and inert.
        private static void Action(UiScene s, UiAt at, ref UiPromptAction a, int index, float x, float y, float w)
        {
            bool main = a.Main && !a.Disabled;
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, w, ActionRow, main ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, a.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2;
            float kw = UiDraw.GameKey(s, inside, cx, y + (ActionRow - UiTokens.KeyCap) / 2f, a.Key, main);
            cx += kw > 0f ? kw + UiTokens.S2 : 0f;
            UiDraw.TextMiddle(s, inside, cx, y, ActionRow, x + w - UiTokens.S3 - cx, UiStyle.Body, main ? UiRole.OnAccent : UiRole.Text, a.Label, UiFlags.Label | UiFlags.Truncate);
            int node = UiDraw.Node(s, at, x, y, w, ActionRow, ActionCommand, index, face, a.Disabled);
            if (a.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }
    }
}
