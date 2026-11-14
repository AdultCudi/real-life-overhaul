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
        public int Picture;       // another mod's own picture (UiDraw.PictureKey), drawn instead of the glyph; 0 for none
        public bool Fav;          // the player's favourite: its star filled
        public bool OnBar;        // on the status bar's row: its pin filled
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
        public int Mode;           // UiAllApps.Folder, Favourites or Bar: which controls a row carries
        public string EditLabel;   // a folder's or the favourites' button to the bar's page
        public string ResetLabel;  // the bar's page's button: the default row again
        public int FocusCommand = -1, FocusArg;   // after a move the keys follow the app to its new place: this target takes focus once
        public float ListTop;      // laid out: where the first row of the list stands, for a drag's drop (px of the layout)
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

        /// <summary>
        /// A row's own controls: arg is the row's index. Star marks a favourite, Pin puts the app on the bar or takes it off,
        /// Earlier and Later move it one place in the list the player orders.
        /// </summary>
        public const int StarCommand = 3, EarlierCommand = 4, LaterCommand = 5, PinCommand = 6;

        /// <summary>The band's button: Edit opens the bar's page, Reset puts the default row back.</summary>
        public const int EditCommand = 7, ResetCommand = 8;

        /// <summary>What the list is: a folder's apps (a star each), the favourites (a star, and the order), or the bar's page (every app with its pin, the bar's apps first in their order).</summary>
        public const int Folder = 0, Favourites = 1, Bar = 2;

        /// <summary>px: a row's height, for the drag that drops an app on another's place.</summary>
        public static float RowStep => RowHeight;

        private const float BandH = UiTokens.S2 * 2f + UiTokens.Control;   // px: the folder's band, tall enough for its button
        private const float Control = UiTokens.Control;
        public const float Width = UiInfoPanel.Width;
        private const float TabStrip = 56f;   // px: the round tabs' column, as the info panel's
        private const float TabSize = 40f;
        private const float RowTile = 40f;    // px: an app's tile
        private const int MostRows = 8;       // rows in view: the fullest folder of the phone's, the rest scroll

        private static float RowHeight => UiDraw.Snap(UiTokens.S2 * 2f + Math.Max(RowTile, UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTextRules.LineHeight(UiStyle.Small)));

        /// <summary>How many rows fit above the status bar.</summary>
        public static int RowsFit(UiFrame frame)
        {
            float room = frame.BandY - UiTokens.S16 * 2f - UiFrame.Strip - BandH - UiTokens.S2;
            return Math.Max(3, (int)(room / RowHeight));
        }

        public static void Lay(UiScene s, UiAllAppsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("all apps", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Panel(s, m, frame, UiAt.Screen, keys);
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel over a window already laid out in the scene, on the overlay's layer, as it rises over an app. For the previews.</summary>
        public static void LayOver(UiScene s, UiAllAppsModel m, UiFrame frame, IUiKeys keys)
        {
            s.PromptCount = 0;
            s.Pointer = frame.Pointer;
            s.Held = false;   // the window under it was laid held; the layer over it has the keys
            Panel(s, m, frame, new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 }, keys);
            UiDraw.PromptBar(s, frame, keys);   // its own bar over the window's, as its canvas stands over the window's in the game
        }

        private static void Panel(UiScene s, UiAllAppsModel m, UiFrame frame, UiAt root, IUiKeys keys)
        {
            // One height for every folder, so changing folder never moves the panel: its rows scroll, an empty one says so in the room.
            int room = Math.Min(MostRows, RowsFit(frame));
            int shown = Math.Min(room, Math.Max(0, m.RowCount - m.First));
            float bodyH = room * RowHeight;
            float h = UiDraw.Snap(UiFrame.Strip + BandH + UiTokens.S2 + bodyH);
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
            int band = UiDraw.Band(s, at, px, cy, Width, BandH);
            string folder = m.Tab >= 0 && m.Tab < m.Tabs.Length ? m.Tabs[m.Tab].Label : null;
            // The band's button: a folder or the favourites lead to the bar's page, the bar's page has its reset.
            bool reset = m.Mode == Bar;
            string words = reset ? m.ResetLabel : m.EditLabel;
            int bandButton = -1;
            float bw = 0f;
            if (!string.IsNullOrEmpty(words))
            {
                UiIcon icon = reset ? UiIcon.Reset : UiIcon.Pencil;
                bw = Math.Min(iw - UiTokens.S3 - UiTokens.S10, UiDraw.ButtonWidth(UiButtonKind.Secondary, words, icon, UiAction.Count, keys));
                bandButton = UiDraw.Button(s, at.In(band), ix + iw - bw, cy + UiTokens.S2, bw, Control, UiButtonKind.Secondary, words, icon, UiAction.Count, keys,
                    reset ? ResetCommand : EditCommand, 0);
            }
            UiDraw.TextMiddle(s, at.In(band), ix, cy + (BandH - UiTokens.PairRow) / 2f, UiTokens.PairRow, iw - (bw > 0f ? bw + UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text,
                folder, UiFlags.Label | UiFlags.Truncate);
            cy += BandH + UiTokens.S2;
            m.ListTop = cy;
            // The body's room is the list's own, filled or not: a short or empty folder keeps the panel's size without a hole.
            short room0 = s.AddClip(px, cy, Width, bodyH, 0f);
            s.Clips[room0].Fills = true;
            bool closeOk = close >= 0 && s.Nodes[close].Command == UiDraw.CloseCommand;
            if (bandButton >= 0 && closeOk)
            {
                s.Nodes[close].Down = bandButton;   // down from the close reaches the band's button; up from it comes back
                s.Nodes[bandButton].Up = close;
            }
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
                    int node = Row(s, inClip.Arriving(i), ref m.Rows[m.First + i], m.First + i, list, i * RowHeight, m.Mode);
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
                    if (s.Nodes[r].Command == RowCommand)
                    {
                        s.Nodes[r].Left = chosenTab;
                    }
                }
                int up = bandButton >= 0 ? bandButton : closeOk ? close : -1;
                if (entry >= 0 && up >= 0)
                {
                    s.Nodes[entry].Up = up;   // up from the list reaches the band's button (else the strip's close); down from it comes back
                    s.Nodes[up].Down = entry;
                }
                if (m.RowCount > shown)
                {
                    UiDraw.ScrollThumb(s, at, px + Width - UiTokens.S2, cy, bodyH, m.First * RowHeight, m.RowCount * RowHeight);
                }
                if (m.FocusCommand >= 0)
                {
                    int again = s.FindNode(m.FocusCommand, m.FocusArg);
                    if (again >= 0)
                    {
                        s.FocusFirst = again;   // the app moved: the keys stay on it
                        s.FocusReset = true;
                    }
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
        // Then the row's own controls: the star in a folder; the favourites' page adds the two arrows that move the app, the bar's page has
        // the arrows and a pin in the star's place.
        private static int Row(UiScene s, UiAt at, ref UiAllAppsRow r, int index, short list, float y, int mode)
        {
            float h = RowHeight;
            float x = UiTokens.S2, w = Width - UiTokens.S2 * 2f;
            int face = UiDraw.Box(s, at, x, y, w, h, r.Open ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, r.Open ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S2;
            int tile = UiDraw.Tile(s, inside, tx, y + (h - RowTile) / 2f, RowTile, r.Tint, false);
            float g = UiTokens.S6;
            if (r.Picture != 0)
            {
                UiDraw.AppMark(s, inside.In(tile), tx + (RowTile - g) / 2f, y + (h - g) / 2f, g, r.Picture);   // another mod's app: its own picture or the kit's mark, as on the phone
            }
            else
            {
                UiDraw.Icon(s, inside.In(tile), tx + (RowTile - g) / 2f, y + (h - g) / 2f, g, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            int controls = mode == Folder ? 1 : 3;
            float controlsW = controls * Control + (controls - 1) * UiTokens.S1;
            float controlsX = x + w - UiTokens.S2 - controlsW, right = controlsX - UiTokens.S3;
            float bw = 0f;
            if (r.Badge > 0 && string.IsNullOrEmpty(r.Status))   // a line that already counts the news stands for the badge
            {
                bw = UiDraw.Badge(s, inside, right, y + (h - UiTokens.Badge) / 2f, r.Badge, true) + UiTokens.S3;
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Middle;
            }
            float cx = tx + RowTile + UiTokens.S3, cw = right - bw - cx;
            bool status = !string.IsNullOrEmpty(r.Status);
            float lh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = UiTextRules.LineHeight(UiStyle.Small);
            float ty = y + (h - lh - (status ? sh : 0f)) / 2f;
            UiDraw.Text(s, inside, cx, ty, cw, UiStyle.BodyStrong, UiRole.Text, r.Title, UiFlags.Truncate | UiFlags.Middle);
            if (status)
            {
                // On the open app's row the quiet grey would not read on the cyan's fifth: its line takes the words' own colour.
                UiDraw.Text(s, inside, cx, ty + lh, cw, UiStyle.Small, r.Open ? UiRole.Text : UiRole.Text2, r.Status, UiFlags.Truncate | UiFlags.Middle);
            }
            int row = UiDraw.Node(s, at, x, y, w, h, RowCommand, index, face, false, list, index);
            float by = y + (h - Control) / 2f, bx = controlsX;
            int left = row;
            if (mode != Folder)
            {
                left = Chain(s, row, left, UiDraw.IconButton(s, inside, bx, by, Control, UiIcon.Up, UiRole.Text2, EarlierCommand, index));
                bx += Control + UiTokens.S1;
                left = Chain(s, row, left, UiDraw.IconButton(s, inside, bx, by, Control, UiIcon.Down, UiRole.Text2, LaterCommand, index));
                bx += Control + UiTokens.S1;
            }
            Chain(s, row, left, mode == Bar
                ? UiDraw.IconButton(s, inside, bx, by, Control, UiIcon.Pin, UiRole.Text2, PinCommand, index, r.OnBar)
                : UiDraw.IconButton(s, inside, bx, by, Control, UiIcon.Star, UiRole.Text2, StarCommand, index, r.Fav));
            return row;
        }

        // Links a control to the one before it (the row for the first), left and right, so the arrows walk along a row's controls.
        private static int Chain(UiScene s, int row, int before, int node)
        {
            s.Nodes[before].Right = node;
            s.Nodes[node].Left = before;
            return node;
        }
    }
}
