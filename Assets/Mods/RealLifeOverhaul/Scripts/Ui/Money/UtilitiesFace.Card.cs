using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class UtilitiesFace
    {
        private const float StepBox = 160f;      // px: the figure between a stepper's minus and plus, at least
        private const float StepLabel = 0.34f;   // of a card's width, a step's name

        /// <summary>
        /// A card of facts and what to do about them, on its band: a title with its state chip, a line under it, the lines
        /// (a label and its value, or a sentence with its tick), the steps (an amount between a minus and a plus, with what
        /// it means under it), a switch, and the buttons at the foot, the main one last. Lines that would not fit the height
        /// given are left out, never spilled. Returns the height the card takes; draws nothing when <paramref name="draw"/> is false.
        /// </summary>
        private static float Card(UiScene s, UiAt at, UtilitiesCard c, float x, float y, float w, float limit, int switchCommand, int lessCommand, int moreCommand,
            IUiKeys keys, bool band, bool draw)
        {
            float pad = band ? UiTokens.S4 : 0f;
            float x0 = x + pad, iw = w - pad * 2f;
            UiAt on = at;
            int box = -1;
            if (draw && band)
            {
                box = UiDraw.Box(s, at, x, y, w, UiDraw.SnapDown(Math.Min(limit, CardHeight(c, w, keys, band))), UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                on = at.In(box);
            }
            float actionsH = ActionsHeight(c.Actions, iw, keys);
            float foot = y + Math.Max(0f, limit) - pad - actionsH;
            float cy = y + pad;
            if (!string.IsNullOrEmpty(c.Title))
            {
                float chip = string.IsNullOrEmpty(c.Chip) ? 0f : ChipWidth(c.Chip);
                if (draw)
                {
                    UiDraw.TextMiddle(s, on, x0, cy, UiTokens.S8, iw - (chip > 0f ? chip + UiTokens.S3 : 0f), c.Heading ? UiStyle.SectionHeader : UiStyle.Number, UiRole.Text, c.Title,
                        c.Heading ? UiFlags.Label | UiFlags.Truncate : UiFlags.Truncate);
                    if (chip > 0f)
                    {
                        UiDraw.Chip(s, on, x0 + iw - chip, cy + (UiTokens.S8 - UiTokens.Badge - UiTokens.S1) / 2f, c.Chip, c.ChipTone, chip);
                    }
                }
                cy += UiTokens.S8;
            }
            if (!string.IsNullOrEmpty(c.Sub))
            {
                if (draw)
                {
                    UiDraw.TextMiddle(s, on, x0, cy, UiTokens.PairRow, iw, UiStyle.Body, UiRole.Text2, c.Sub, UiFlags.Truncate);
                }
                cy += UiTokens.PairRow;
            }
            cy += UiTokens.S2;
            for (int i = 0; i < c.Lines.Length; i++)
            {
                float h = LineHeight(c.Lines[i], iw);
                if (cy + h > foot)
                {
                    break;
                }
                if (draw)
                {
                    Line(s, on, c.Lines[i], x0, cy, iw);
                }
                cy += h + UiTokens.S1;
            }
            for (int i = 0; i < c.Steps.Length; i++)
            {
                float h = StepHeight(c.Steps[i], iw);
                if (cy + h > foot)
                {
                    break;
                }
                if (draw)
                {
                    Step(s, on, c.Steps[i], i, x0, cy, iw, lessCommand, moreCommand);
                }
                cy += h;
            }
            if (!string.IsNullOrEmpty(c.SwitchLabel) && cy + UiTokens.Control <= foot)
            {
                if (draw)
                {
                    UiDraw.TextMiddle(s, on, x0, cy, UiTokens.Control, iw - UiTokens.S16 * 2f, UiStyle.Body, UiRole.Text2, c.SwitchLabel, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Switch(s, on, x0, cy, iw, c.SwitchOn, null, switchCommand, 0, c.SwitchDisabled);
                }
                cy += UiTokens.Control;
            }
            if (c.Actions.Length > 0)
            {
                float ay = cy + UiTokens.S3;
                if (draw)
                {
                    Actions(s, on, c.Actions, x0, ay, iw, keys);
                }
                cy = ay + actionsH;
            }
            return Math.Min(limit, UiDraw.Snap(cy - y + pad));
        }

        /// <summary>The height a card takes at a width with everything it holds.</summary>
        private static float CardHeight(UtilitiesCard c, float w, IUiKeys keys, bool band)
        {
            float pad = band ? UiTokens.S4 : 0f, iw = w - pad * 2f;
            float cy = pad;
            cy += string.IsNullOrEmpty(c.Title) ? 0f : UiTokens.S8;
            cy += string.IsNullOrEmpty(c.Sub) ? 0f : UiTokens.PairRow;
            cy += UiTokens.S2;
            for (int i = 0; i < c.Lines.Length; i++)
            {
                cy += LineHeight(c.Lines[i], iw) + UiTokens.S1;
            }
            for (int i = 0; i < c.Steps.Length; i++)
            {
                cy += StepHeight(c.Steps[i], iw);
            }
            cy += string.IsNullOrEmpty(c.SwitchLabel) ? 0f : UiTokens.Control;
            cy += c.Actions.Length > 0 ? UiTokens.S3 + ActionsHeight(c.Actions, iw, keys) : 0f;
            return UiDraw.Snap(cy + pad);
        }

        private static float ChipWidth(string words) => UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1);

        // A line's height: a row of a label and its value, or a sentence wrapped to four lines at most.
        private static float LineHeight(UtilitiesLine l, float w)
        {
            if (l.Label != null)
            {
                return UiTokens.PairRow;
            }
            float room = w - (l.Check ? UiTokens.Icon + UiTokens.S2 : 0f);
            return UiDraw.Snap(UiTextRules.WrappedHeight(l.Value ?? string.Empty, UiStyle.Body, room, 4)) + UiTokens.S1;
        }

        private static void Line(UiScene s, UiAt at, UtilitiesLine l, float x, float y, float w)
        {
            if (l.Label != null)
            {
                UiDraw.InfoRow(s, at, x, y, w, l.Label, l.Value, false, -1, 0, l.Ink == UiRole.None ? UiRole.Text : l.Ink);
                return;
            }
            float tx = x;
            if (l.Check)
            {
                UiDraw.Icon(s, at, x, y + UiTokens.S1, UiTokens.Icon, l.Met ? UiIcon.Check : UiIcon.Warning, UiRole.None, UiFlags.None, true);
                tx = x + UiTokens.Icon + UiTokens.S2;
            }
            UiDraw.Text(s, at, tx, y + UiTokens.S1, w - (tx - x), UiStyle.Body, l.Ink == UiRole.None ? UiRole.Text2 : l.Ink, l.Value, UiFlags.Wrap | UiFlags.Label, 4);
        }

        private static float StepHeight(UtilitiesStep step, float w)
        {
            float hint = string.IsNullOrEmpty(step.Hint) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(step.Hint, UiStyle.Small, w, 2)) + UiTokens.S1;
            return UiTokens.Control + hint + UiTokens.S3;
        }

        // An amount: its name at the left, the minus, the figure and the plus at the right, what the figure means under them.
        private static void Step(UiScene s, UiAt at, UtilitiesStep step, int index, float x, float y, float w, int lessCommand, int moreCommand)
        {
            float lw = UiDraw.SnapDown(w * StepLabel);
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw - UiTokens.S2, UiStyle.Body, UiRole.Text, step.Label, UiFlags.Label | UiFlags.Truncate);
            float cx = x + lw, cw = w - lw, b = UiDraw.StepButton, by = y + (UiTokens.Control - b) / 2f;
            Square(s, at, cx, by, b, UiIcon.Minus, lessCommand, index, !step.CanLess);
            Square(s, at, cx + cw - b, by, b, UiIcon.Plus, moreCommand, index, !step.CanMore);
            float fx = cx + b + UiTokens.S2, fw = cw - (b + UiTokens.S2) * 2f;
            int field = UiDraw.Box(s, at, fx, y, fw, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, at.In(field), fx + UiTokens.S2, y, UiTokens.Control, fw - UiTokens.S4, UiStyle.BodyStrong, UiRole.Text, step.Value, UiFlags.Centre | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(step.Hint))
            {
                UiDraw.Text(s, at, x, y + UiTokens.Control + UiTokens.S1, w, UiStyle.Small, UiRole.Text2, step.Hint, UiFlags.Wrap | UiFlags.Label, 2);
            }
        }

        // A square button with a glyph on its slate tile, greyed and never pressable at the end of its range.
        private static void Square(UiScene s, UiAt at, float x, float y, float size, UiIcon icon, int command, int arg, bool disabled)
        {
            int face = UiDraw.Box(s, at, x, y, size, size, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, UiFlags.Middle | (disabled ? UiFlags.Disabled : UiFlags.None));
            float g = UiTokens.Icon;
            UiDraw.Icon(s, at.In(face), x + (size - g) / 2f, y + (size - g) / 2f, g, icon, disabled ? UiRole.Muted : UiRole.AccentText,
                UiFlags.Middle | UiFlags.Centre | (disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.Node(s, at, x, y, size, size, command, arg, face, disabled);
        }

        // The buttons in rows from the right, the last the main one; a row that does not fit the width wraps under.
        private static float ActionsHeight(UiActionModel[] actions, float w, IUiKeys keys)
        {
            if (actions.Length == 0)
            {
                return 0f;
            }
            int rows = 1;
            float used = 0f;
            for (int i = 0; i < actions.Length; i++)
            {
                float bw = UiDraw.ButtonWidth(actions[i].Kind, actions[i].Label, actions[i].Icon, actions[i].Key, keys);
                if (used > 0f && used + bw > w)
                {
                    rows++;
                    used = 0f;
                }
                used += bw + UiTokens.S3;
            }
            return rows * UiTokens.Control + (rows - 1) * UiTokens.S2;
        }

        private static void Actions(UiScene s, UiAt at, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float right = x + w, ry = y;
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = Math.Min(w, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                if (right - bw < x)
                {
                    right = x + w;
                    ry += UiTokens.Control + UiTokens.S2;
                }
                right -= bw;
                UiDraw.Button(s, at, right, ry, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
        }
    }
}
