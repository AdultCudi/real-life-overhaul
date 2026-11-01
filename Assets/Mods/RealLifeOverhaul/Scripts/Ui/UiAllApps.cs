using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A folder of All apps: its glyph and colour on a round tab, its name, the news of its apps added up.</summary>
    public struct UiAllAppsTab
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Label;
        public int Badge;
    }

    /// <summary>An app in a folder of All apps: its glyph, its name, one line where the app gives one, its news.</summary>
    public struct UiAllAppsRow
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Title;
        public string Status;     // "3 unread", "Next shift 09:00"; null when the app gives none
        public int Badge;
        public bool Open;         // the app the full menu shows now
    }

    /// <summary>What the All apps panel shows: the folders, the chosen one's apps, and a sentence for an empty folder.</summary>
    public sealed class UiAllAppsModel
    {
        public string Title;
        public UiAllAppsTab[] Tabs = new UiAllAppsTab[0];
        public int Tab;
        public UiAllAppsRow[] Rows = new UiAllAppsRow[0];
        public int RowCount;
        public int First;          // the first row in view, when a folder holds more than fit
        public string Empty;       // the chosen folder has no app: what will fill it
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// All apps, the full menu's contents in the info panel's shape: a panel about as wide, docked above the status bar at
    /// the left where its tile ends the app row, the phone's folders as round icon tabs down its left edge (the chosen one
    /// filled cyan), the title strip, the folder's name on its band, then its apps as rows: the glyph on its tile in its
    /// colour, the name, one line of what waits where the app says, the badge at the right. Pressing a row opens that
    /// app's own page. Commands: <see cref="TabCommand"/> (a folder's index) and <see cref="RowCommand"/> (an app's
    /// index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiAllApps
    {
        public const int TabCommand = 1, RowCommand = 2;
        public const float Width = UiInfoPanel.Width;
        private const float TabStrip = 56f;   // px: the round tabs' column, as the info panel's
        private const float TabSize = 40f;
        private const float RowTile = 40f;    // px: an app's tile
        private const int MostRows = 8;       // rows in view: the fullest folder of the phone's, the rest scroll

        private static float RowHeight => UiDraw.Snap(UiTokens.S2 * 2f + Math.Max(RowTile, UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTextRules.LineHeight(UiStyle.Small)));

        /// <summary>How many rows fit above the status bar.</summary>
        public static int RowsFit(UiFrame frame)
        {
            float room = frame.BandY - UiTokens.S16 * 2f - UiFrame.Strip - UiTokens.PairRow - UiTokens.S2 * 3f;
            return Math.Max(3, (int)(room / RowHeight));
        }

        public static void Lay(UiScene s, UiAllAppsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("all apps", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Panel(s, m, frame, UiAt.Screen);
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel over a window already laid out in the scene, on the overlay's layer, as it rises over an app. For the previews.</summary>
        public static void LayOver(UiScene s, UiAllAppsModel m, UiFrame frame, IUiKeys keys)
        {
            s.PromptCount = 0;
            s.Pointer = frame.Pointer;
            s.Held = false;   // the window under it was laid held; the layer over it has the keys
            Panel(s, m, frame, new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 });
            UiDraw.PromptBar(s, frame, keys);   // its own bar over the window's, as its canvas stands over the window's in the game
        }

        private static void Panel(UiScene s, UiAllAppsModel m, UiFrame frame, UiAt root)
        {
            // One height for every folder, so changing folder never moves the panel: its rows scroll, an empty one says so in the room.
            int room = Math.Min(MostRows, RowsFit(frame));
            int shown = Math.Min(room, Math.Max(0, m.RowCount - m.First));
            float bodyH = room * RowHeight;
            float h = UiDraw.Snap(UiFrame.Strip + UiTokens.S2 + UiTokens.PairRow + UiTokens.S2 + bodyH + UiTokens.S2);
            float bottom = frame.BandY - UiTokens.S4;
            float x = UiTokens.S2, px = x + TabStrip + UiTokens.S2;
            float y = UiDraw.SnapDown(bottom - h);
            // The window under it dimmed, as a question dims it: the panel reads as standing over the app, never as its rows cut.
            UiDraw.Box(s, root, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);
            int firstTab = s.NodeCount;
            Tabs(s, m, x, y, root);
            int chosenTab = m.Tab >= 0 && m.Tab < m.Tabs.Length ? firstTab + m.Tab : -1;
            int panel = UiDraw.Box(s, root, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = root.In(panel);
            float cy = UiDraw.TitleStrip(s, at, px, y, Width, UiIcon.Grid, m.Title, true);
            int close = s.NodeCount - 1;   // the strip's close, its last target
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            int band = UiDraw.Band(s, at, px, cy, Width, UiTokens.S2 * 2f + UiTokens.PairRow);
            string folder = m.Tab >= 0 && m.Tab < m.Tabs.Length ? m.Tabs[m.Tab].Label : null;
            UiDraw.TextMiddle(s, at.In(band), ix, cy + UiTokens.S2, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, folder, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.S2 * 2f + UiTokens.PairRow + UiTokens.S2;
            // The body's room is the list's own, filled or not: a short or empty folder keeps the panel's size without a hole.
            short room0 = s.AddClip(px, cy, Width, bodyH, 0f);
            s.Clips[room0].Fills = true;
            if (m.RowCount == 0)
            {
                s.AddList(0, 0, 0, room0, -1);
                float eh = UiDraw.Snap(UiTextRules.WrappedHeight(m.Empty, UiStyle.Body, iw, 3));
                UiDraw.Text(s, at, ix, cy + UiDraw.SnapDown((bodyH - eh) / 2f), iw, UiStyle.Body, UiRole.Text2, m.Empty, UiFlags.Wrap | UiFlags.Label | UiFlags.Middle | UiFlags.Centre, 3);
            }
            else
            {
                short clip = room0;
                s.Clips[clip].ContentHeight = bodyH;
                short list = s.AddList(m.RowCount, m.First, shown, clip, -1);
                var inClip = new UiAt { Parent = -1, Clip = clip, Layer = root.Layer, Order = -1 };
                int firstRow = s.NodeCount, entry = -1;
                for (int i = 0; i < shown; i++)
                {
                    int node = Row(s, inClip.Arriving(i), ref m.Rows[m.First + i], m.First + i, list, i * RowHeight);
                    if (entry < 0 || m.Rows[m.First + i].Open)
                    {
                        entry = node;
                    }
                }
                // Right from any folder enters its list at the open app's row, else its first; Left from a row goes back to the folder in view.
                for (int t = firstTab; t < firstRow && entry >= 0; t++)
                {
                    if (s.Nodes[t].Command == TabCommand)
                    {
                        s.Nodes[t].Right = entry;
                    }
                }
                for (int r = firstRow; r < s.NodeCount && chosenTab >= 0; r++)
                {
                    s.Nodes[r].Left = chosenTab;
                }
                if (entry >= 0 && close >= 0 && s.Nodes[close].Command == UiDraw.CloseCommand)
                {
                    s.Nodes[firstRow].Up = close;   // up from the list reaches the strip's close; down from the close comes back
                    s.Nodes[close].Down = entry;
                }
                if (m.RowCount > shown)
                {
                    UiDraw.ScrollThumb(s, at, px + Width - UiTokens.S2, cy, bodyH, m.First * RowHeight, m.RowCount * RowHeight);
                }
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
        }

        // The round tabs down the panel's left, the chosen folder filled cyan, its summed news on its corner.
        private static void Tabs(UiScene s, UiAllAppsModel m, float x, float y, UiAt root)
        {
            if (m.Tabs.Length == 0)
            {
                return;
            }
            float h = m.Tabs.Length * TabSize + (m.Tabs.Length - 1) * UiTokens.S2 + UiTokens.S2 * 2f;
            int strip = UiDraw.Box(s, root, x, y, TabStrip, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = root.In(strip);
            for (int i = 0; i < m.Tabs.Length; i++)
            {
                float tx = x + (TabStrip - TabSize) / 2f, ty = y + UiTokens.S2 + i * (TabSize + UiTokens.S2);
                int node = UiDraw.IconButton(s, at, tx, ty, TabSize, m.Tabs[i].Icon, m.Tabs[i].Tint, TabCommand, i, i == m.Tab, UiAction.Count, true);
                if (m.Tabs[i].Badge > 0)
                {
                    UiDraw.NewsDot(s, at.In(s.Nodes[node].Face), tx + TabSize, ty, UiRole.Panel);   // the folder's news, as the app row marks it
                }
                if (i == m.Tab)
                {
                    s.FocusFirst = node;   // the keys start on the folder in view, so one tab is lit, not two
                }
            }
        }

        // An app's row: its glyph on its tile, its name and the line it gives, its news at the right; the open app a fifth of the cyan.
        private static int Row(UiScene s, UiAt at, ref UiAllAppsRow r, int index, short list, float y)
        {
            float h = RowHeight;
            float x = UiTokens.S2, w = Width - UiTokens.S2 * 2f;
            int face = UiDraw.Box(s, at, x, y, w, h, r.Open ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, r.Open ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S2;
            int tile = UiDraw.Tile(s, inside, tx, y + (h - RowTile) / 2f, RowTile, r.Tint, false);
            float g = UiTokens.S6;
            UiDraw.Icon(s, inside.In(tile), tx + (RowTile - g) / 2f, y + (h - g) / 2f, g, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float bw = 0f;
            if (r.Badge > 0 && string.IsNullOrEmpty(r.Status))   // a line that already counts the news stands for the badge
            {
                bw = UiDraw.Badge(s, inside, x + w - UiTokens.S3, y + (h - UiTokens.Badge) / 2f, r.Badge, true) + UiTokens.S3;
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Middle;
            }
            float cx = tx + RowTile + UiTokens.S3, cw = x + w - UiTokens.S3 - bw - cx;
            bool status = !string.IsNullOrEmpty(r.Status);
            float lh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = UiTextRules.LineHeight(UiStyle.Small);
            float ty = y + (h - lh - (status ? sh : 0f)) / 2f;
            UiDraw.Text(s, inside, cx, ty, cw, UiStyle.BodyStrong, UiRole.Text, r.Title, UiFlags.Truncate | UiFlags.Middle);
            if (status)
            {
                // On the open app's row the quiet grey would not read on the cyan's fifth: its line takes the words' own colour.
                UiDraw.Text(s, inside, cx, ty + lh, cw, UiStyle.Small, r.Open ? UiRole.Text : UiRole.Text2, r.Status, UiFlags.Truncate | UiFlags.Middle);
            }
            return UiDraw.Node(s, at, x, y, w, h, RowCommand, index, face, false, list, index);
        }
    }
}
