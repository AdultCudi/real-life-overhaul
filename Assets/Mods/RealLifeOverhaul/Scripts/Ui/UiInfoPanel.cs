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
            float x = UiTokens.S2, y = UiTokens.S4;
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
            if (m.Footer.Length > 0)
            {
                UiDraw.Box(s, at, px, cy, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                float bx = ix - UiTokens.S1;
                for (int i = 0; i < m.Footer.Length; i++)
                {
                    UiDraw.IconButton(s, at, bx, cy + UiTokens.S2, UiTokens.Control, m.Footer[i], i < m.FooterTints.Length ? m.FooterTints[i] : UiRole.None, FooterCommand, i);
                    bx += UiTokens.Control + UiTokens.S2;
                }
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel's height: its strip, its head, its sections and its footer.</summary>
        public static float Height(UiInfoModel m)
        {
            float h = UiFrame.Strip + HeadHeight(m);
            for (int i = 0; i < m.Sections.Length; i++)
            {
                h += SectionHeight(ref m.Sections[i]) + UiTokens.S1;
            }
            return UiDraw.Snap(h + (m.Footer.Length > 0 ? UiTokens.Control + UiTokens.S4 : UiTokens.S2));
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

        private static float HeadHeight(UiInfoModel m) => string.IsNullOrEmpty(m.Status) && string.IsNullOrEmpty(m.Name) ? 0f : Portrait + UiTokens.S4 * 2f;

        // The person's round portrait in its status ring, their state with its face beside it and the facts that matter most.
        private static float Head(UiScene s, UiAt at, UiInfoModel m, float x, float y)
        {
            float h = HeadHeight(m);
            if (h <= 0f)
            {
                return y;
            }
            float fx = x + UiTokens.S4, fy = y + UiTokens.S4;
            float tx = fx;
            if (!string.IsNullOrEmpty(m.Name))
            {
                UiDraw.Portrait(s, at, fx, fy, Portrait, m.Name, m.Hue, m.Ring);
                tx = fx + Portrait + UiTokens.S4;
            }
            float tw = x + Width - UiTokens.S4 - tx;
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
                ry += UiDraw.InfoRow(s, at, ix, ry, iw, row.Label, row.Value, row.Link, LinkCommand, index * 16 + r, row.Ink == UiRole.None ? UiRole.Text : row.Ink);
            }
            return y + h + UiTokens.S1;
        }
    }
}
