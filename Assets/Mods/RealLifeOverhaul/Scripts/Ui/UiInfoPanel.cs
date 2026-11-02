using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A row of an info panel: its label and its value, or a link to another thing.</summary>
    public struct UiInfoRow
    {
        public string Label;
        public string Value;
        public bool Link;          // the value opens the thing or moves the camera to it: cyan with its magnifier
        public UiRole Ink;         // the value's colour when it says a state (good, bad); None for the words' own
        public bool Meter;         // a meter between the label and the value, filled to Fill in the accent (a building's customers against its room)
        public float Fill;         // the meter's share, 0 to 1
    }

    /// <summary>A section of an info panel: its label in capitals with a value of its own, and its rows.</summary>
    public struct UiInfoSection
    {
        public string Label;
        public string Value;
        public bool Link;
        public UiInfoRow[] Rows;
    }

    /// <summary>What an info panel shows: the thing's name and face, its state, its tabs, its sections and its footer's buttons.</summary>
    public sealed class UiInfoModel
    {
        public string Title;                       // the strip's title: the person's or the place's name
        public UiIcon Icon = UiIcon.Person;
        public UiIcon[] Tabs = new UiIcon[0];      // the strip at its left (household, person, home, work), each a thing of its own
        public UiRole[] TabTints = new UiRole[0];  // each tab's colour, on its icon and its soft tile
        public int Tab;
        public string Name;                        // the portrait's person; null for a place, which shows its icon instead
        public float Hue;
        public UiRole Ring = UiRole.Ok;            // the portrait's status ring
        public string Status;                      // "Looking for work"
        public UiIcon StatusIcon = UiIcon.SmileyMeh;
        public UiRole StatusTone = UiRole.Warn;
        public string Facts;                       // a line of the facts that matter most ("Age 27, office work")
        public string Lead;                        // the thing's own name over its facts and state in the head (a business under its address's strip); null for none
        public string Mark;                        // initials on the head's tile in place of the glyph (a business's own); null for the glyph
        public UiRole MarkTone = UiRole.Accent;    // the initials' tile: a chart hue, the initials in the dark ink on it
        public string FooterLabel;                 // the footer's wide button before its icon buttons ("Open in BizMan"), argument -1; null for none
        public bool FooterLabelOff;                // it is there but takes no press now
        public UiInfoSection[] Sections = new UiInfoSection[0];
        public UiIcon[] Footer = new UiIcon[0];    // locate, follow, call
        public UiRole[] FooterTints = new UiRole[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The info panel, after Cities: Skylines II's citizen panel (design/DIRECTION_CS2.md, section 4.1): a narrow slate
    /// panel docked at the top left with the world beside it, a strip of round icon tabs on its left edge for the things it
    /// joins (a household, a person, a home, a work), its title strip, the person's round portrait with a status ring beside
    /// their state, then sections parted by a darker band, each a capitals label over compact rows with cyan links, and a
    /// footer of icon buttons (locate, follow, call). Commands: <see cref="TabCommand"/> (a tab's index),
    /// <see cref="LinkCommand"/> (section * 16 + row, the section's own link at row 15) and <see cref="FooterCommand"/>
    /// (a button's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiInfoPanel
    {
        public const int TabCommand = 1, LinkCommand = 2, FooterCommand = 3;
        public const float Width = 440f;    // px: the panel, as a game's info panel is narrow
        private const float TabStrip = 56f; // px: the icon tabs' column, a round tab with two grid steps either side for its focus ring
        private const float TabSize = 40f;  // px: a round tab
        private const float Portrait = 64f; // px

        public static void Lay(UiScene s, UiInfoModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("info panel", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, m, UiTokens.S2, UiTokens.S4);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>
        /// The tabs and the panel alone at a top left (px of the layout), for a screen that stands it among other pieces (the
        /// HUD's left column over the running game). Returns the y under the panel.
        /// </summary>
        public static float Draw(UiScene s, UiInfoModel m, float x, float y)
        {
            float px = x;
            if (m.Tabs.Length > 0)
            {
                Tabs(s, m, x, y);
                px = x + TabStrip + UiTokens.S2;
            }
            float h = Height(m);
            int panel = UiDraw.Box(s, UiAt.Screen, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, px, y, Width, m.Icon, m.Title, true);
            cy = Head(s, at, m, px, cy);
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            for (int i = 0; i < m.Sections.Length; i++)
            {
                cy = Section(s, at, ref m.Sections[i], i, px, cy, ix, iw);
            }
            if (m.Footer.Length > 0 || !string.IsNullOrEmpty(m.FooterLabel))
            {
                UiDraw.Box(s, at, px, cy, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                float bx = ix - UiTokens.S1;
                int first = s.NodeCount;
                if (!string.IsNullOrEmpty(m.FooterLabel))
                {
                    // The panel's one main action, as wide as the icons leave it, before them.
                    float lw = UiDraw.SnapDown(iw - m.Footer.Length * (UiTokens.Control + UiTokens.S2));
                    UiDraw.Button(s, at, ix, cy + UiTokens.S2, lw, UiTokens.Control, UiButtonKind.Primary, m.FooterLabel, UiIcon.None,
                        UiAction.Count, UiDefaultKeys.Keyboard, FooterCommand, -1, m.FooterLabelOff);
                    bx = ix + lw + UiTokens.S2 - UiTokens.S1;
                }
                for (int i = 0; i < m.Footer.Length; i++)
                {
                    UiDraw.IconButton(s, at, bx, cy + UiTokens.S2, UiTokens.Control, m.Footer[i], i < m.FooterTints.Length ? m.FooterTints[i] : UiRole.None, FooterCommand, i);
                    bx += UiTokens.Control + UiTokens.S2;
                }
                s.Nodes[first].Left = first;   // the footer is a row: its ends go nowhere sideways, not across the panel
                s.Nodes[s.NodeCount - 1].Right = s.NodeCount - 1;
                // Down from the close goes into the panel, to its first link or its footer, never across to the tabs' strip.
                int close = s.FindNode(UiDraw.CloseCommand, 0), link = -1;
                for (int n = 0; n < s.NodeCount && link < 0; n++)
                {
                    link = s.Nodes[n].Command == LinkCommand ? n : -1;
                }
                if (close >= 0)
                {
                    s.Nodes[close].Down = link >= 0 ? link : first;
                }
            }
            return y + h;
        }

        /// <summary>The width the panel takes with its tabs' strip beside it.</summary>
        public static float OuterWidth(UiInfoModel m) => (m.Tabs.Length > 0 ? TabStrip + UiTokens.S2 : 0f) + Width;

        /// <summary>The panel's height: its strip, its head, its sections and its footer.</summary>
        public static float Height(UiInfoModel m)
        {
            float h = UiFrame.Strip + HeadHeight(m);
            for (int i = 0; i < m.Sections.Length; i++)
            {
                h += SectionHeight(ref m.Sections[i]) + UiTokens.S1;
            }
            return UiDraw.Snap(h + (m.Footer.Length > 0 || !string.IsNullOrEmpty(m.FooterLabel) ? UiTokens.Control + UiTokens.S4 : UiTokens.S2));
        }

        // The round tabs down the panel's left edge, the shown one filled cyan, as a game joins a person to their household.
        private static void Tabs(UiScene s, UiInfoModel m, float x, float y)
        {
            float h = m.Tabs.Length * TabSize + (m.Tabs.Length - 1) * UiTokens.S2 + UiTokens.S2 * 2f;
            int strip = UiDraw.Box(s, UiAt.Screen, x, y, TabStrip, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(strip);
            for (int i = 0; i < m.Tabs.Length; i++)
            {
                float ty = y + UiTokens.S2 + i * (TabSize + UiTokens.S2);
                UiRole tint = i < m.TabTints.Length ? m.TabTints[i] : UiRole.None;
                int node = UiDraw.IconButton(s, at, x + (TabStrip - TabSize) / 2f, ty, TabSize, m.Tabs[i], tint, TabCommand, i, i == m.Tab, UiAction.Count, true);
                if (i == m.Tab)
                {
                    s.FocusFirst = node;   // the keys start where the panel is, so one tab is lit, not two
                }
            }
        }

        private static float HeadHeight(UiInfoModel m) => string.IsNullOrEmpty(m.Status) && string.IsNullOrEmpty(m.Name) && string.IsNullOrEmpty(m.Lead) ? 0f
            : UiDraw.Snap(Math.Max(Portrait, LeadBlock(m))) + UiTokens.S4 * 2f;

        // The head's words with a lead name: the name, the facts, then the state, each a line.
        private static float LeadBlock(UiInfoModel m) => string.IsNullOrEmpty(m.Lead) ? 0f
            : UiTextRules.LineHeight(UiStyle.Hero) + (string.IsNullOrEmpty(m.Facts) ? 0f : UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Body))
              + (string.IsNullOrEmpty(m.Status) ? 0f : UiTokens.S1 + Math.Max(UiTokens.Icon, UiTextRules.LineHeight(UiStyle.BodyStrong)));

        // The person's round portrait in its status ring, their state with its face beside it and the facts that matter most.
        private static float Head(UiScene s, UiAt at, UiInfoModel m, float x, float y)
        {
            float h = HeadHeight(m);
            if (h <= 0f)
            {
                return y;
            }
            float fx = x + UiTokens.S4, fy = y + UiTokens.S4 + UiDraw.SnapDown((h - UiTokens.S4 * 2f - Portrait) / 2f);
            float tx = fx;
            if (!string.IsNullOrEmpty(m.Mark))
            {
                // A business's own initials on its tile, as its sign reads, in the dark ink on its colour.
                int tile = UiDraw.Box(s, at, fx, fy, Portrait, Portrait, m.MarkTone == UiRole.None ? UiRole.Accent : m.MarkTone, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.TextMiddle(s, at.In(tile), fx, fy, Portrait, Portrait, UiStyle.Hero, UiRole.OnAccent, m.Mark, UiFlags.Centre | UiFlags.Truncate);
                tx = fx + Portrait + UiTokens.S4;
            }
            else if (!string.IsNullOrEmpty(m.Name))
            {
                UiDraw.Portrait(s, at, fx, fy, Portrait, m.Name, m.Hue, m.Ring);
                tx = fx + Portrait + UiTokens.S4;
            }
            else if (m.Icon != UiIcon.None)
            {
                // A place has no face: its glyph on a tile of the portrait's size, as the model has always said.
                int tile = UiDraw.Tile(s, at, fx, fy, Portrait, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), fx + (Portrait - UiTokens.S8) / 2f, fy + (Portrait - UiTokens.S8) / 2f, UiTokens.S8, m.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                tx = fx + Portrait + UiTokens.S4;
            }
            float tw = x + Width - UiTokens.S4 - tx;
            if (!string.IsNullOrEmpty(m.Lead))
            {
                LeadWords(s, at, m, tx, y + (h - LeadBlock(m)) / 2f, tw);
                return y + h;
            }
            float lh = UiTextRules.LineHeight(UiStyle.BodyStrong), fh = UiTextRules.LineHeight(UiStyle.Body);
            bool facts = !string.IsNullOrEmpty(m.Facts);
            float block = Math.Max(UiTokens.Icon, lh) + (facts ? UiTokens.S1 + fh : 0f);
            float ty = fy + (Portrait - block) / 2f;
            if (!string.IsNullOrEmpty(m.Status))
            {
                UiDraw.Icon(s, at, tx, ty + (Math.Max(UiTokens.Icon, lh) - UiTokens.Icon) / 2f, UiTokens.Icon, m.StatusIcon, m.StatusTone, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, tx + UiTokens.Icon + UiTokens.S2, ty, Math.Max(UiTokens.Icon, lh), tw - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text,
                    m.Status, UiFlags.Truncate | UiFlags.Label);
            }
            if (facts)
            {
                UiDraw.Text(s, at, tx, ty + Math.Max(UiTokens.Icon, lh) + UiTokens.S1, tw, UiStyle.Body, UiRole.Text2, m.Facts, UiFlags.Truncate | UiFlags.Middle);
            }
            return y + h;
        }

        // The lead name in the head's large type, the facts in the quiet ink under it, then the state with its face in its tone.
        private static void LeadWords(UiScene s, UiAt at, UiInfoModel m, float x, float y, float w)
        {
            UiDraw.Text(s, at, x, y, w, UiStyle.Hero, UiRole.Text, m.Lead, UiFlags.Truncate | UiFlags.Middle);
            y += UiTextRules.LineHeight(UiStyle.Hero);
            if (!string.IsNullOrEmpty(m.Facts))
            {
                UiDraw.Text(s, at, x, y + UiTokens.S1, w, UiStyle.Body, UiRole.Text2, m.Facts, UiFlags.Truncate | UiFlags.Middle);
                y += UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Body);
            }
            if (string.IsNullOrEmpty(m.Status))
            {
                return;
            }
            float sh = Math.Max(UiTokens.Icon, UiTextRules.LineHeight(UiStyle.BodyStrong)), sy = y + UiTokens.S1;
            UiRole tone = m.StatusTone == UiRole.None ? UiRole.Text2 : m.StatusTone;
            UiDraw.Icon(s, at, x, sy + (sh - UiTokens.Icon) / 2f, UiTokens.Icon, m.StatusIcon, tone, UiFlags.Middle);
            UiDraw.TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, sy, sh, w - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, Words(tone), m.Status, UiFlags.Truncate);
        }

        // A state's words in its tone's lighter shade, which reads on the slate; a quiet tone in the quiet ink.
        private static UiRole Words(UiRole tone) => tone == UiRole.Ok ? UiRole.OkText : tone == UiRole.Danger ? UiRole.DangerText : tone == UiRole.Warn ? UiRole.WarnText : UiRole.Text2;

        private static float SectionHeight(ref UiInfoSection section) => UiTokens.S2 * 2f + UiTokens.PairRow * (1 + (section.Rows?.Length ?? 0));

        // A section on its darker band: its label in capitals with its own value, then its rows, a gap of the panel's tone under it.
        private static float Section(UiScene s, UiAt at, ref UiInfoSection section, int index, float x, float y, float ix, float iw)
        {
            float h = SectionHeight(ref section);
            UiDraw.Band(s, at, x, y, Width, h);
            float ry = y + UiTokens.S2;
            ry += UiDraw.SectionRow(s, at, ix, ry, iw, section.Label, section.Value, section.Link, LinkCommand, index * 16 + 15);
            for (int r = 0; section.Rows != null && r < section.Rows.Length; r++)
            {
                UiInfoRow row = section.Rows[r];
                if (row.Meter)
                {
                    ry += MeterRow(s, at, ix, ry, iw, ref row);
                    continue;
                }
                ry += UiDraw.InfoRow(s, at, ix, ry, iw, row.Label, row.Value, row.Link, LinkCommand, index * 16 + r, row.Ink == UiRole.None ? UiRole.Text : row.Ink);
            }
            return y + h + UiTokens.S1;
        }

        // A row with a meter: its label at the left, its figure at the right, the track between them filled to its share.
        private static float MeterRow(UiScene s, UiAt at, float x, float y, float w, ref UiInfoRow row)
        {
            float h = UiTokens.PairRow, track = UiTokens.S2;
            float lw = Math.Min(UiDraw.SnapDown(w * 0.45f), UiDraw.Snap(UiTextRules.Width(row.Label, UiStyle.Body) + UiTokens.S1));
            float vw = Math.Min(UiDraw.SnapDown(w * 0.3f), UiDraw.Snap(UiTextRules.Width(row.Value, UiStyle.BodyStrong) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x, y, h, lw, UiStyle.Body, UiRole.Text2, row.Label, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, UiStyle.BodyStrong, row.Ink == UiRole.None ? UiRole.Text : row.Ink, row.Value, UiFlags.Right | UiFlags.Truncate);
            float tx = x + lw + UiTokens.S3, tw = w - lw - vw - UiTokens.S3 * 2f;
            if (tw >= UiTokens.S8)
            {
                int bar = UiDraw.Box(s, at, tx, y + (h - track) / 2f, tw, track, UiRole.Track, UiRole.None, track / 2f, UiFlags.Circle | UiFlags.Middle);
                float fill = Math.Max(0f, Math.Min(1f, row.Fill)) * tw;
                if (fill >= 1f)
                {
                    UiDraw.Box(s, at.In(bar), tx, y + (h - track) / 2f, fill, track, UiRole.Accent, UiRole.None, track / 2f, UiFlags.Circle | UiFlags.Middle | UiFlags.Share);
                }
            }
            return h;
        }
    }
}
