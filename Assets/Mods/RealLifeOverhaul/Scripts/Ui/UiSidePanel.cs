using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a side panel shows: its title in capitals with a quiet word at the strip's right (the employer, "Your business"),
    /// a thing's head (its glyph on a tile, its name, what and where, a state chip), up to five rows (a link moves the camera
    /// or opens the thing), a switch row, a meter or a timing gauge, and one or two actions, each a row of its own, the last
    /// with a square tool beside it; collapsed, only its title strip. For the game's current job widget, the mod's ShiftCard
    /// and the current building.
    /// </summary>
    public sealed class UiSidePanelModel
    {
        public const int MostRows = 5, MostActions = 2;

        public string Title;                               // "Shift at Bloom Café"
        public UiIcon Icon = UiIcon.Briefcase;             // the head's glyph, on its tile
        public UiInfoRow[] Rows = new UiInfoRow[0];
        public bool HasProgress;
        public UiProgressModel Progress;                   // the shift's hours, a delivery's way
        public bool HasTiming;
        public UiTimingModel Timing;                       // a trade's timing
        public UiActionModel[] Actions = new UiActionModel[0];   // their Command is ignored: the panel's own commands carry which
        public bool Collapsed;

        // Appended with the HUD's screens (3 Nov 2026); left empty a panel reads as it did.
        public string Meta;                                // the strip's quiet word at its right: the employer, "Your business"
        public string HeadName;                            // a thing's head under the strip: its name...
        public string HeadSub;                             // ...what and where ("Florist, Midtown")...
        public string HeadChip;                            // ...and its state as a chip ("Open"); null for none
        public UiRole HeadTone = UiRole.Ok;                // the chip's tone: Ok, Warn, Danger or None for the quiet grey
        public string SwitchLabel;                         // a switch row under the rows ("Temporarily closed"); null for none
        public bool SwitchOn;
        public bool SwitchDisabled;
        public UiIcon Tool;                                // a square icon button beside the last action (the walls); None for none
        public bool ToolOn;                                // the tool in force (the walls lowered)
        public string ProgressUnder;                       // the meter's words under it at the right ("5 h 15 min left"); its Label stands at the left
    }

    /// <summary>
    /// Side panels: compact slate panels docked at the top right over the running game, stacked a grid step apart, each with
    /// its title strip (its title in capitals in the cyan at the left, a quiet word at the right, a chevron that folds it to
    /// the strip alone), a thing's head, up to five label and value rows, a switch row, a meter with its words under it or a
    /// timing gauge, and its actions, a row each. Commands: <see cref="FoldCommand"/> (a panel's index), <see cref="LinkCommand"/>
    /// (panel * 8 + row), <see cref="ActionCommand"/> (panel * 4 + action), <see cref="ToolCommand"/> and
    /// <see cref="SwitchCommand"/> (a panel's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiSidePanel
    {
        public const int FoldCommand = 210, LinkCommand = 211, ActionCommand = 212, ToolCommand = 213, SwitchCommand = 214;
        public const float Width = 360f;   // px: narrower than an info panel, as it stays up while the player walks
        private const byte Region = 210;   // the panels' own column for the d-pad
        private const float Mark = UiTokens.Control;   // px: the head's tile

        /// <summary>One panel's height, folded or open.</summary>
        public static float Height(UiSidePanelModel m)
        {
            if (m.Collapsed)
            {
                return UiFrame.Strip;
            }
            float h = UiFrame.Strip + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.HeadName))
            {
                h += Mark + UiTokens.S3;
            }
            h += Math.Min(UiSidePanelModel.MostRows, m.Rows.Length) * UiTokens.PairRow;
            if (!string.IsNullOrEmpty(m.SwitchLabel))
            {
                h += UiTokens.Control;
            }
            if (m.HasProgress)
            {
                h += UiTokens.S3 + MeterHeight(m);
            }
            if (m.HasTiming)
            {
                h += UiTokens.S3 + UiDraw.TimingHeight;
            }
            int actions = Math.Min(UiSidePanelModel.MostActions, m.Actions.Length);
            if (actions > 0)
            {
                h += UiTokens.S3 + actions * UiTokens.ControlSmall + (actions - 1) * UiTokens.S2;
            }
            return UiDraw.Snap(h + UiTokens.S4);
        }

        // The meter: its bar, then its words under it, the share at the left and what is left at the right.
        private static float MeterHeight(UiSidePanelModel m) =>
            UiTokens.S2 + (string.IsNullOrEmpty(m.Progress.Label) && string.IsNullOrEmpty(m.ProgressUnder) && string.IsNullOrEmpty(m.Progress.Value) ? 0f
                : UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)));

        /// <summary>Lays the panels down from a top right corner (px of the layout); returns the y under the last.</summary>
        public static float LayStack(UiScene s, UiAt at, UiSidePanelModel[] panels, int count, float right, float top, IUiKeys keys)
        {
            float y = top;
            UiAt column = at.InRegion(Region);
            for (int i = 0; i < count && i < panels.Length; i++)
            {
                y = Lay(s, column, panels[i], i, right - Width, y, keys) + UiTokens.S2;
            }
            return y - (count > 0 ? UiTokens.S2 : 0f);
        }

        /// <summary>The stack's height, the steps between included, for whatever stands under it (the game's notices).</summary>
        public static float StackHeight(UiSidePanelModel[] panels, int count)
        {
            float h = 0f;
            for (int i = 0; i < count && i < panels.Length; i++)
            {
                h += Height(panels[i]) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return h;
        }

        /// <summary>
        /// A HUD panel's title strip: its title in capitals in the cyan at the left, a quiet word and the fold's chevron at the
        /// right; the chevron's target spans the strip's right end so the word and the chevron press as one. Returns the fold's node.
        /// </summary>
        public static int Strip(UiScene s, UiAt at, float x, float y, float w, string title, string meta, bool collapsed, int command, int arg)
        {
            float h = UiFrame.Strip;
            UiDraw.Box(s, at, x, y, w, h, UiRole.Header, UiRole.None, collapsed ? UiTokens.RadiusPanel : -UiTokens.RadiusPanel, UiFlags.Decor | UiFlags.Chrome);
            float chevron = UiTokens.IconSmall;
            float mw = string.IsNullOrEmpty(meta) ? 0f : Math.Min(w * 0.5f, UiDraw.Snap(UiTextRules.Width(meta, UiStyle.Small) + UiTokens.S1));
            float fw = UiTokens.S2 + (mw > 0f ? mw + UiTokens.S1 : 0f) + chevron + UiTokens.S2;
            float fx = x + w - UiTokens.S2 - fw;
            int face = UiDraw.Box(s, at, fx, y + (h - UiTokens.ControlSmall) / 2f, fw, UiTokens.ControlSmall, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            if (mw > 0f)
            {
                UiDraw.TextMiddle(s, inside, fx + UiTokens.S2, y, h, mw, UiStyle.Small, UiRole.Text2, meta, UiFlags.Truncate);
            }
            UiDraw.Icon(s, inside, fx + fw - UiTokens.S2 - chevron, y + (h - chevron) / 2f, chevron, collapsed ? UiIcon.Down : UiIcon.Up, UiRole.Text2, UiFlags.Middle);
            float tw = fx - x - UiTokens.S4 - UiTokens.S2;
            UiDraw.TextMiddle(s, at, x + UiTokens.S4, y, h, tw, UiStyle.SectionHeader, UiRole.AccentText, title, UiFlags.Label | UiFlags.Truncate);
            return UiDraw.Node(s, at, fx, y + (h - UiTokens.ControlSmall) / 2f, fw, UiTokens.ControlSmall, command, arg, face);
        }

        /// <summary>
        /// A small chip in a tone: its words on the tone's soft fill, as a state reads at a glance ("Open", "Closed"); Accent,
        /// what is going on right now ("Dancing"), on the solid cyan with dark words.
        /// </summary>
        public static float ToneChip(UiScene s, UiAt at, float x, float y, string text, UiRole tone, float most = 0f)
        {
            UiRole fill, ink;
            switch (tone)
            {
                case UiRole.Ok: fill = UiRole.OkSoft; ink = UiRole.OkText; break;
                case UiRole.Warn: fill = UiRole.WarnSoft; ink = UiRole.WarnText; break;
                case UiRole.Danger: fill = UiRole.DangerSoft; ink = UiRole.DangerText; break;
                case UiRole.Accent: fill = UiRole.Accent; ink = UiRole.OnAccent; break;
                default: fill = UiRole.Pill; ink = UiRole.Text2; break;
            }
            float w = most > 0f ? Math.Min(ToneChipWidth(text), most) : ToneChipWidth(text);   // at most "most" wide, its words cut
            float h = UiTokens.Badge + UiTokens.S1;
            int face = UiDraw.Box(s, at, x, y, w, h, fill, UiRole.None, h / 2f);
            UiDraw.TextMiddle(s, at.In(face), x + UiTokens.S2, y, h, w - UiTokens.S2 * 2f, UiStyle.SmallStrong, ink, text, UiFlags.Truncate);
            return w;
        }

        /// <summary>A tone chip's width before it is drawn.</summary>
        public static float ToneChipWidth(string text) => string.IsNullOrEmpty(text) ? 0f : UiDraw.Snap(UiTextRules.Width(text, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.S2 * 2f);

        // One panel: its strip with the fold, then its head, rows, switch, meter or gauge and its actions. Returns the y under it.
        private static float Lay(UiScene s, UiAt at, UiSidePanelModel m, int index, float x, float y, IUiKeys keys)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            int fold = Strip(s, on, x, y, Width, m.Title, m.Meta, m.Collapsed, FoldCommand, index);
            s.Nodes[fold].Left = s.Nodes[fold].Right = fold;
            if (m.Collapsed)
            {
                return y + h;
            }
            float cy = y + UiFrame.Strip + UiTokens.S2;
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            if (!string.IsNullOrEmpty(m.HeadName))
            {
                cy = Head(s, on, m, ix, cy, iw) + UiTokens.S3;
            }
            int rows = Math.Min(UiSidePanelModel.MostRows, m.Rows.Length);
            for (int r = 0; r < rows; r++)
            {
                ref UiInfoRow row = ref m.Rows[r];
                if (string.IsNullOrEmpty(row.Value))
                {
                    // A line of words alone (a job's objective): the row's whole width, in the value's ink.
                    UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.Body, row.Ink == UiRole.None ? UiRole.Text : row.Ink, row.Label, UiFlags.Truncate);
                    cy += UiTokens.PairRow;
                    continue;
                }
                cy += UiDraw.InfoRow(s, on, ix, cy, iw, row.Label, row.Value, row.Link, LinkCommand, index * 8 + r, row.Ink == UiRole.None ? UiRole.Text : row.Ink);
            }
            if (!string.IsNullOrEmpty(m.SwitchLabel))
            {
                float sw = UiDraw.Snap(iw * 0.3f);
                UiRole ink = m.SwitchDisabled ? UiRole.Muted : UiRole.Text2;
                UiDraw.TextMiddle(s, on, ix, cy, UiTokens.Control, iw - sw - UiTokens.S3, UiStyle.Body, ink, m.SwitchLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Switch(s, on, ix + iw - sw, cy, sw, m.SwitchOn, null, SwitchCommand, index, m.SwitchDisabled);
                cy += UiTokens.Control;
            }
            if (m.HasProgress)
            {
                cy += UiTokens.S3;
                cy += Meter(s, on, m, ix, cy, iw);
            }
            if (m.HasTiming)
            {
                cy += UiTokens.S3;
                cy += UiDraw.Timing(s, on, ix, cy, iw, ref m.Timing);
            }
            int actions = Math.Min(UiSidePanelModel.MostActions, m.Actions.Length);
            if (actions > 0)
            {
                cy += UiTokens.S3;
                for (int a = 0; a < actions; a++)
                {
                    ref UiActionModel act = ref m.Actions[a];
                    bool tool = a == actions - 1 && m.Tool != UiIcon.None;
                    float room = tool ? UiTokens.ControlSmall + UiTokens.S2 : 0f;
                    UiButtonKind kind = act.Kind == UiButtonKind.Destructive || act.Kind == UiButtonKind.Primary ? act.Kind : UiButtonKind.Secondary;
                    int button = UiDraw.Button(s, on, ix, cy, iw - room, UiTokens.ControlSmall, kind, act.Label, act.Icon, UiAction.Count, keys, ActionCommand, index * 4 + a, act.Disabled);
                    // A row each: Up and Down walk them, sideways leads nowhere but to the tool beside the last.
                    s.Nodes[button].Left = s.Nodes[button].Right = button;
                    if (tool)
                    {
                        int t = UiDraw.IconButton(s, on, ix + iw - UiTokens.ControlSmall, cy, UiTokens.ControlSmall, m.Tool, UiRole.Text, ToolCommand, index, m.ToolOn);
                        s.Nodes[button].Right = t;
                        s.Nodes[t].Left = button;
                        s.Nodes[t].Right = t;
                    }
                    cy += UiTokens.ControlSmall + UiTokens.S2;
                }
            }
            return y + h;
        }

        // A thing's head: its glyph on a tile, its name over what and where, its state as a chip at the right.
        private static float Head(UiScene s, UiAt at, UiSidePanelModel m, float x, float y, float w)
        {
            int tile = UiDraw.Tile(s, at, x, y, Mark, UiRole.Tile, false);
            UiDraw.Icon(s, at.In(tile), x + (Mark - UiTokens.Icon) / 2f, y + (Mark - UiTokens.Icon) / 2f, UiTokens.Icon, m.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre);
            float chip = Math.Min(ToneChipWidth(m.HeadChip), UiDraw.SnapDown((w - Mark - UiTokens.S3) * 0.5f));   // a long state ("Closing in 3 hours") cut, the name kept
            float tx = x + Mark + UiTokens.S3;
            float tw = x + w - tx - (chip > 0f ? chip + UiTokens.S2 : 0f);
            // The name over what and where, the pair centred on the tile, each line on its own line's middle.
            float line = UiTextRules.LineHeight(UiStyle.BodyStrong);
            bool sub = !string.IsNullOrEmpty(m.HeadSub);
            float small = sub ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
            float ty = y + Math.Max(0f, (Mark - line - small) / 2f);
            UiDraw.TextMiddle(s, at, tx, ty, line, tw, UiStyle.BodyStrong, UiRole.Text, m.HeadName, UiFlags.Truncate);
            if (sub)
            {
                UiDraw.TextMiddle(s, at, tx, ty + line, small, tw, UiStyle.Small, UiRole.Text2, m.HeadSub, UiFlags.Truncate);
            }
            if (chip > 0f)
            {
                ToneChip(s, at, x + w - chip, y + (Mark - UiTokens.Badge - UiTokens.S1) / 2f, m.HeadChip, m.HeadTone, chip);
            }
            return y + Mark;
        }

        // The meter: its bar filled to its share in its tone, the share's words under it at the left and what is left at the right.
        private static float Meter(UiScene s, UiAt at, UiSidePanelModel m, float x, float y, float w)
        {
            float share = Math.Max(0f, Math.Min(1f, m.Progress.Share));
            int track = UiDraw.Box(s, at, x, y, w, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1, UiFlags.Middle);
            if (share > 0f)
            {
                UiDraw.Box(s, at.In(track), x, y, Math.Max(UiTokens.S2, w * share), UiTokens.S2, m.Progress.Tone == UiRole.None ? UiRole.Accent : m.Progress.Tone, UiRole.None,
                    UiTokens.S1, UiFlags.Middle | UiFlags.Share);
            }
            float h = MeterHeight(m);
            if (h > UiTokens.S2)
            {
                float ly = y + UiTokens.S2 + UiTokens.S1, lh = UiTextRules.LineHeight(UiStyle.Small);
                string right = !string.IsNullOrEmpty(m.ProgressUnder) ? m.ProgressUnder : m.Progress.Value;
                float rw = string.IsNullOrEmpty(right) ? 0f : Math.Min(w * 0.5f, UiDraw.Snap(UiTextRules.Width(right, UiStyle.Small) + UiTokens.S1));
                if (!string.IsNullOrEmpty(m.Progress.Label))
                {
                    UiDraw.TextMiddle(s, at, x, ly, lh, w - rw - UiTokens.S3, UiStyle.Small, UiRole.Text2, m.Progress.Label, UiFlags.Truncate);
                }
                if (rw > 0f)
                {
                    UiDraw.TextMiddle(s, at, x + w - rw, ly, lh, rw, UiStyle.Small, UiRole.Text2, right, UiFlags.Right | UiFlags.Truncate);
                }
            }
            return h;
        }
    }
}
