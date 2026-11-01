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
            Panel(s, m, frame, new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 });
            UiDraw.PromptBar(s, frame, keys);   // its own bar over the window's, as its canvas stands over the window's in the game
        }

        private static void Panel(UiScene s, UiAllAppsModel m, UiFrame frame, UiAt root)
        {
            int shown = Math.Min(RowsFit(frame), Math.Max(0, m.RowCount - m.First));
            float bodyH = m.RowCount == 0 ? UiTokens.S16 + UiTokens.S8 : shown * RowHeight;
            float h = UiDraw.Snap(UiFrame.Strip + UiTokens.S2 + UiTokens.PairRow + UiTokens.S2 + bodyH + UiTokens.S2);
            float tabsH = m.Tabs.Length * TabSize + (m.Tabs.Length - 1) * UiTokens.S2 + UiTokens.S2 * 2f;
            float bottom = frame.BandY - UiTokens.S4;
            float x = UiTokens.S2, px = x + TabStrip + UiTokens.S2;
            float y = UiDraw.SnapDown(bottom - h);
            Tabs(s, m, x, UiDraw.SnapDown(bottom - tabsH), root);
            int panel = UiDraw.Box(s, root, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = root.In(panel);
            float cy = UiDraw.TitleStrip(s, at, px, y, Width, UiIcon.Grid, m.Title, true);
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            int band = UiDraw.Band(s, at, px, cy, Width, UiTokens.S2 * 2f + UiTokens.PairRow);
            string folder = m.Tab >= 0 && m.Tab < m.Tabs.Length ? m.Tabs[m.Tab].Label : null;
            UiDraw.TextMiddle(s, at.In(band), ix, cy + UiTokens.S2, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, folder, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.S2 * 2f + UiTokens.PairRow + UiTokens.S2;
            if (m.RowCount == 0)
            {
                UiDraw.Text(s, at, ix, cy + UiTokens.S4, iw, UiStyle.Body, UiRole.Text2, m.Empty, UiFlags.Wrap | UiFlags.Label | UiFlags.Middle | UiFlags.Centre, 3);
            }
            else
            {
                short clip = s.AddClip(px, cy, Width, bodyH, 0f);
                s.Clips[clip].ContentHeight = bodyH;
                short list = s.AddList(m.RowCount, m.First, shown, clip, -1);
                var inClip = new UiAt { Parent = -1, Clip = clip, Layer = root.Layer, Order = -1 };
                for (int i = 0; i < shown; i++)
                {
                    Row(s, inClip.Arriving(i), ref m.Rows[m.First + i], m.First + i, list, i * RowHeight);
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
                    int face = s.Nodes[node].Face;
                    UiDraw.Badge(s, at.In(face), tx + TabSize + UiTokens.S1, ty - UiTokens.S1, m.Tabs[i].Badge, true);
                    s.Items[s.ItemCount - 1].Flags |= UiFlags.Overflow;
                    s.Items[s.ItemCount - 2].Flags |= UiFlags.Overflow;
                }
                if (i == m.Tab)
                {
                    s.FocusFirst = node;   // the keys start on the folder in view, so one tab is lit, not two
                }
            }
        }

        // An app's row: its glyph on its tile, its name and the line it gives, its news at the right; the open app a fifth of the cyan.
        private static void Row(UiScene s, UiAt at, ref UiAllAppsRow r, int index, short list, float y)
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
            if (r.Badge > 0)
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
            UiDraw.Node(s, at, x, y, w, h, RowCommand, index, face, false, list, index);
        }
    }
}
