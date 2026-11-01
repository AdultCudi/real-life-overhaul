using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>An app on the phone's home: its icon in its category's colour, its name and what waits in it.</summary>
    public struct UiPhoneApp
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Label;
        public int Badge;
    }

    /// <summary>What the phone's home shows: the time, the day, a page of apps and the page controls.</summary>
    public sealed class UiPhoneModel
    {
        public string Name = "BizPhone";
        public string Time;
        public string Day;
        public string Note;                          // a line under the day: the weather, the next meeting
        public UiPhoneApp[] Apps = new UiPhoneApp[0];
        public string NextTitle;                     // "Up next": the day's next things under the apps
        public UiInfoRow[] Next = new UiInfoRow[0];  // each a time and what it is
        public int Page;
        public int Pages = 1;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The phone's home, Big Ambitions' phone in the kit's tokens (design/DIRECTION_CS2.md, section 4.7): a slate phone at
    /// the bottom right as the game holds it, the time, the day and a close at its head, a grid of apps as chunky icons in
    /// their category's colour on slate tiles with their names, and the page controls fixed in a bar at its foot, so the next page is
    /// always in one place (the owner, 1 Nov: "The next button must be at the bottom"). Commands: <see cref="AppCommand"/>
    /// (an app's index on the page), <see cref="PageCommand"/> (-1 back, +1 on). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPhone
    {
        public const int AppCommand = 1, PageCommand = 2;
        public const float Width = 376f;      // px
        public const float Height = 752f;     // px
        public const int Across = 4;
        private const float Corner = 24f;     // px: a phone's rounded body, the one shape that is not a panel's
        private const float Tile = 60f;       // px: an app's tile, centred in its quarter of the phone on the grid
        private const float FootH = 64f;      // px: the page bar

        public static void Lay(UiScene s, UiPhoneModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("phone", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float x = frame.Width - Width - UiTokens.S8, y = frame.BandY - Height - UiTokens.S4;
            int body = UiDraw.Box(s, UiAt.Screen, x, y, Width, Height, UiRole.Panel, UiRole.PanelEdge, Corner, UiFlags.Panel | UiFlags.Chrome);   // a device of a fixed size
            UiAt at = UiAt.Screen.In(body);
            float ix = x + UiTokens.S5, iw = Width - UiTokens.S5 * 2f;
            float ty = y + UiTokens.S4;
            float head = UiTokens.MinTarget, third = UiDraw.SnapDown(iw / 3f);
            UiDraw.TextMiddle(s, at, ix, ty, head, third, UiStyle.SmallStrong, UiRole.Text, m.Time, UiFlags.None);
            UiDraw.TextMiddle(s, at, ix + third, ty, head, third, UiStyle.SectionHeader, UiRole.Text2, m.Name, UiFlags.Centre | UiFlags.Truncate);
            // The phone's own way back for the mouse, where a window has its close.
            UiDraw.IconButton(s, at, ix + iw - head, ty, head, UiIcon.Close, UiRole.Text, UiDraw.CloseCommand, 0, false, UiAction.Count, false, true);
            ty += head + UiTokens.S5;
            // The day and the weather are the status bar's: a phone that says them again under it only adds noise, so a
            // phone over the bar leaves them out and its apps lead.
            if (!string.IsNullOrEmpty(m.Day))
            {
                UiDraw.Text(s, at, ix, ty, iw, UiStyle.Display, UiRole.Text, m.Day, UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.Icon(s, at, ix, ty, UiTokens.Icon, UiIcon.CloudSun, UiRole.Warn, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, ix + UiTokens.Icon + UiTokens.S2, ty, UiTokens.Icon, iw - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2, m.Note, UiFlags.Truncate);
                ty += UiTokens.Icon;
            }
            ty += string.IsNullOrEmpty(m.Day) && string.IsNullOrEmpty(m.Note) ? 0f : UiTokens.S6;
            float cell = UiDraw.SnapDown(iw / Across);
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;
            float rowH = Tile + UiTokens.S2 + labelH + UiTokens.S3;
            float gridBottom = y + Height - FootH - UiTokens.S2;
            for (int i = 0; i < m.Apps.Length; i++)
            {
                float cx = ix + (i % Across) * cell, cy = ty + (i / Across) * rowH;
                if (cy + rowH > gridBottom + UiTokens.S3)
                {
                    break;
                }
                App(s, at, ref m.Apps[i], i, cx, cy, cell);
            }
            int rowsShown = Math.Min((m.Apps.Length + Across - 1) / Across, (int)((gridBottom + UiTokens.S3 - ty) / rowH));
            float ny = ty + rowsShown * rowH + UiTokens.S2;
            if (m.Next.Length > 0 && ny + UiTokens.S8 + m.Next.Length * UiTokens.PairRow <= gridBottom)
            {
                UiDraw.Box(s, at, ix, ny, iw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                ny += UiTokens.S3;
                UiDraw.Text(s, at, ix, ny, iw, UiStyle.SectionHeader, UiRole.Text, m.NextTitle, UiFlags.Label | UiFlags.Truncate);
                ny += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                for (int i = 0; i < m.Next.Length; i++)
                {
                    float tw = UiDraw.Snap(UiTextRules.Width("88:88", UiStyle.BodyStrong) + UiTokens.S3);
                    UiDraw.TextMiddle(s, at, ix, ny, UiTokens.PairRow, tw, UiStyle.BodyStrong, UiRole.Text, m.Next[i].Label, UiFlags.None);   // a time, not a link
                    UiDraw.TextMiddle(s, at, ix + tw, ny, UiTokens.PairRow, iw - tw, UiStyle.Body, UiRole.Text, m.Next[i].Value, UiFlags.Truncate);
                    ny += UiTokens.PairRow;
                }
            }
            Foot(s, at, m, x, y + Height - FootH);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // An app: its tile in its category's colour with the chunky glyph on it, its name under it, a badge for what waits.
        private static void App(UiScene s, UiAt at, ref UiPhoneApp app, int index, float x, float y, float w)
        {
            float tx = x + (w - Tile) / 2f;
            int face = UiDraw.Tile(s, at, tx, y, Tile, app.Tint, false);
            float g = UiTokens.S8;
            UiDraw.Icon(s, at.In(face), tx + (Tile - g) / 2f, y + (Tile - g) / 2f, g, app.Icon, app.Tint, UiFlags.Middle | UiFlags.Centre, true);
            if (app.Badge > 0)
            {
                UiDraw.Badge(s, at.In(face), tx + Tile + UiTokens.S1, y - UiTokens.S1, app.Badge, true);
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Overflow;
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Overflow;
            }
            UiDraw.Text(s, at, x, y + Tile + UiTokens.S2, w, UiStyle.Small, UiRole.Text, app.Label, UiFlags.Centre | UiFlags.Truncate | UiFlags.Label);
            UiDraw.Node(s, at, tx, y, Tile, Tile, AppCommand, index, face);
        }

        // An arrow with no page that way: it stays in its place, so the bar never moves, but dims and takes no press.
        private static void Rest(UiScene s, int node)
        {
            s.Nodes[node].Disabled = true;
            ref UiItem glyph = ref s.Items[s.ItemCount - 1];
            glyph.Flags |= UiFlags.Disabled;
            glyph.Alpha = 0.4f;
        }

        // The page bar fixed at the foot: back a page, the pages as dots, on a page; the same place on every page.
        private static void Foot(UiScene s, UiAt at, UiPhoneModel m, float x, float y)
        {
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float b = UiTokens.Control;
            float by = y + (FootH - b) / 2f;
            int back = UiDraw.IconButton(s, at, x + UiTokens.S5, by, b, UiIcon.Back, UiRole.Text2, PageCommand, -1, false, UiAction.TabPrev);
            if (m.Page <= 0)
            {
                Rest(s, back);
            }
            int on = UiDraw.IconButton(s, at, x + Width - UiTokens.S5 - b, by, b, UiIcon.Next, UiRole.Text2, PageCommand, 1, false, UiAction.TabNext);
            if (m.Page >= m.Pages - 1)
            {
                Rest(s, on);
            }
            float dot = UiTokens.S2, gap = UiTokens.S2;
            float total = m.Pages * dot + (m.Pages - 1) * gap;
            float dx = x + UiDraw.SnapDown((Width - total) / 2f);
            for (int i = 0; i < m.Pages; i++)
            {
                UiDraw.Circle(s, at, dx + i * (dot + gap), y + (FootH - dot) / 2f, dot, dot, i == m.Page ? UiRole.Accent : UiRole.Track, UiFlags.Middle);
            }
        }
    }
}
