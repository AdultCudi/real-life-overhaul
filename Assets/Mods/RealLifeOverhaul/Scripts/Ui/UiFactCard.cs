using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of a fact card is.</summary>
    public enum UiFactKind : byte
    {
        Row = 0,        // a label at the left and its value at the right (Ink gives the value's colour)
        Sentence,       // a sentence on its own, wrapped (Ink gives its colour)
        Check,          // a sentence behind a tick (Met) or the warning's glyph
        Meter,          // a graded bar with its label and figure (Fill 0 to 100)
        Link,           // cyan words with an arrow that run Command: open a thing, move the camera
        Switch,         // a label and the game's toggle (On)
        Step,           // a figure to move between a minus and a plus (Value), with what it means under it (Hint)
        Heading,        // a section's label in capitals, with a hairline over it
        Gap,            // a step of room
        Person          // a face with a name and a line under it (Hint), and a figure at the right (Value, in Ink)
    }

    /// <summary>One line of a fact card. Commands: a link and a switch run <see cref="Command"/>; a step's minus runs
    /// it and its plus runs <see cref="Command2"/>; every one with <see cref="Arg"/>.</summary>
    public struct UiFactLine
    {
        public UiFactKind Kind;
        public string Label, Value, Hint;
        public UiRole Ink;
        public int Fill;
        public bool On, Met, CanLess, CanMore, Disabled;
        public int Command, Command2, Arg;

        /// <summary>Words the old apps wrote with the game's rich-text marks (bold, colour) as plain words: the kit sets its own emphasis.</summary>
        public static string Plain(string words)
        {
            if (string.IsNullOrEmpty(words) || words.IndexOf('<') < 0)
            {
                return words;
            }
            var sb = new System.Text.StringBuilder(words.Length);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i] == '<')
                {
                    int end = words.IndexOf('>', i);
                    if (end > i && end - i <= 24 && (words[i + 1] == '/' || char.IsLetter(words[i + 1])))
                    {
                        i = end;
                        continue;
                    }
                }
                sb.Append(words[i]);
            }
            return sb.ToString();
        }

        public static UiFactLine Row(string label, string value, UiRole ink = UiRole.None) => new UiFactLine { Kind = UiFactKind.Row, Label = Plain(label), Value = Plain(value), Ink = ink };

        public static UiFactLine Say(string words, UiRole ink = UiRole.None) => new UiFactLine { Kind = UiFactKind.Sentence, Value = Plain(words), Ink = ink };

        public static UiFactLine Tick(string words, bool met) => new UiFactLine { Kind = UiFactKind.Check, Value = Plain(words), Met = met };

        public static UiFactLine Bar(string label, int fill) => new UiFactLine { Kind = UiFactKind.Meter, Label = label, Fill = fill };

        public static UiFactLine Go(string words, int command, int arg = 0) => new UiFactLine { Kind = UiFactKind.Link, Value = words, Command = command, Arg = arg };

        public static UiFactLine Toggle(string label, bool on, int command, int arg = 0, bool disabled = false) =>
            new UiFactLine { Kind = UiFactKind.Switch, Label = label, On = on, Command = command, Arg = arg, Disabled = disabled };

        public static UiFactLine Amount(string label, string value, string hint, bool canLess, bool canMore, int less, int more, int arg = 0) =>
            new UiFactLine { Kind = UiFactKind.Step, Label = label, Value = value, Hint = hint, CanLess = canLess, CanMore = canMore, Command = less, Command2 = more, Arg = arg };

        public static UiFactLine Section(string label) => new UiFactLine { Kind = UiFactKind.Heading, Label = label };

        public static UiFactLine Space() => new UiFactLine { Kind = UiFactKind.Gap };

        public static UiFactLine Who(string name, string line, string value, UiRole ink = UiRole.None) => new UiFactLine { Kind = UiFactKind.Person,
            Label = name, Hint = line, Value = value, Ink = ink };
    }

    /// <summary>A panel of facts and what to do about them: a title with a state chip, a line under it, the lines and the buttons.</summary>
    public sealed class UiFactCardModel
    {
        public string Title;
        public bool Heading;            // the title is a section's label in capitals, not a name
        public string Sub;              // a line under the title
        public string Chip;
        public UiRole ChipTone = UiRole.Ok;
        public UiFactLine[] Lines = new UiFactLine[0];
        public UiActionModel[] Actions = new UiActionModel[0];
    }

    /// <summary>
    /// A card of facts as a game's panel holds them, on its band: the title and its chip, rows of a label and a value, sentences,
    /// graded meters, links, switches and steps (a figure between a minus and a plus), then the buttons at the foot with the main
    /// one last. The reusable block of the estate and the showroom's detail pages: any screen whose detail is "what it is, and what to
    /// do about it" lays one or two of these. Lines that would not fit the height given are left out, never spilled. Game-free.
    /// Cost: a layout when its model changes.
    /// </summary>
    public static class UiFactCard
    {
        private const float StepLabel = 0.36f;   // of a card's width, a step's name
        private const int SentenceLines = 8;

        /// <summary>Draws the card; returns the height it takes (never more than <paramref name="limit"/>).</summary>
        public static float Lay(UiScene s, UiAt at, UiFactCardModel c, float x, float y, float w, float limit, IUiKeys keys, bool band)
        {
            return Run(s, at, c, x, y, w, limit, keys, band, true);
        }

        /// <summary>The height the card takes at a width with everything it holds.</summary>
        public static float Height(UiFactCardModel c, float w, IUiKeys keys, bool band)
        {
            return Run(null, default(UiAt), c, 0f, 0f, w, float.MaxValue, keys, band, false);
        }

        private static float Run(UiScene s, UiAt at, UiFactCardModel c, float x, float y, float w, float limit, IUiKeys keys, bool band, bool draw)
        {
            float pad = band ? UiTokens.S4 : 0f;
            float x0 = x + pad, iw = w - pad * 2f;
            float total = pad;
            total += string.IsNullOrEmpty(c.Title) ? 0f : UiTokens.S8;
            total += string.IsNullOrEmpty(c.Sub) ? 0f : UiTokens.PairRow;
            total += UiTokens.S2;
            for (int i = 0; i < c.Lines.Length; i++)
            {
                total += LineHeight(c.Lines[i], iw);
            }
            float actionsH = ActionsHeight(c.Actions, iw, keys);
            total += c.Actions.Length > 0 ? UiTokens.S3 + actionsH : 0f;
            total = UiDraw.Snap(total + pad);
            float height = Math.Min(limit, total);
            if (!draw)
            {
                return total;
            }
            UiAt on = at;
            if (band)
            {
                int box = UiDraw.Box(s, at, x, y, w, UiDraw.SnapDown(height), UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                on = at.In(box);
            }
            float foot = y + height - pad - actionsH;
            float cy = y + pad;
            if (!string.IsNullOrEmpty(c.Title))
            {
                float chip = string.IsNullOrEmpty(c.Chip) ? 0f : ChipWidth(c.Chip);
                UiDraw.TextMiddle(s, on, x0, cy, UiTokens.S8, iw - (chip > 0f ? chip + UiTokens.S3 : 0f), c.Heading ? UiStyle.SectionHeader : UiStyle.Number, UiRole.Text, c.Title,
                    c.Heading ? UiFlags.Label | UiFlags.Truncate : UiFlags.Truncate);
                if (chip > 0f)
                {
                    UiDraw.Chip(s, on, x0 + iw - chip, cy + (UiTokens.S8 - UiTokens.Badge - UiTokens.S1) / 2f, c.Chip, c.ChipTone, chip);
                }
                cy += UiTokens.S8;
            }
            if (!string.IsNullOrEmpty(c.Sub))
            {
                UiDraw.TextMiddle(s, on, x0, cy, UiTokens.PairRow, iw, UiStyle.Body, UiRole.Text2, c.Sub, UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            cy += UiTokens.S2;
            for (int i = 0; i < c.Lines.Length; i++)
            {
                float h = LineHeight(c.Lines[i], iw);
                if (cy + h > foot + (c.Actions.Length > 0 ? 0f : pad))
                {
                    break;
                }
                Draw(s, on, c.Lines[i], x0, cy, iw);
                cy += h;
            }
            if (c.Actions.Length > 0)
            {
                Actions(s, on, c.Actions, x0, foot, iw, keys);
            }
            return height;
        }

        internal static float ChipWidth(string words) => UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1);

        private static float LineHeight(UiFactLine l, float w)
        {
            switch (l.Kind)
            {
                case UiFactKind.Sentence:
                case UiFactKind.Check:
                {
                    float room = w - (l.Kind == UiFactKind.Check ? UiTokens.Icon + UiTokens.S2 : 0f);
                    return UiDraw.Snap(UiTextRules.WrappedHeight(l.Value ?? string.Empty, UiStyle.Body, room, SentenceLines)) + UiTokens.S2;
                }
                case UiFactKind.Link:
                    return UiTokens.Control;
                case UiFactKind.Switch:
                    return UiTokens.Control;
                case UiFactKind.Step:
                {
                    float hint = string.IsNullOrEmpty(l.Hint) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(l.Hint, UiStyle.Small, w, 4)) + UiTokens.S1;
                    return UiTokens.Control + hint + UiTokens.S2;
                }
                case UiFactKind.Heading:
                    return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S4;
                case UiFactKind.Gap:
                    return UiTokens.S3;
                case UiFactKind.Person:
                    return UiTokens.S12;
                default:
                    return UiTokens.PairRow;
            }
        }

        private static void Draw(UiScene s, UiAt at, UiFactLine l, float x, float y, float w)
        {
            switch (l.Kind)
            {
                case UiFactKind.Row:
                    UiDraw.InfoRow(s, at, x, y, w, l.Label, l.Value, false, -1, 0, l.Ink == UiRole.None ? UiRole.Text : l.Ink);
                    break;
                case UiFactKind.Sentence:
                    UiDraw.Text(s, at, x, y + UiTokens.S1, w, UiStyle.Body, l.Ink == UiRole.None ? UiRole.Text2 : l.Ink, l.Value, UiFlags.Wrap | UiFlags.Label, SentenceLines);
                    break;
                case UiFactKind.Check:
                    UiDraw.Icon(s, at, x, y + UiTokens.S1, UiTokens.Icon, l.Met ? UiIcon.Check : UiIcon.Warning, UiRole.None, UiFlags.None, true);
                    UiDraw.Text(s, at, x + UiTokens.Icon + UiTokens.S2, y + UiTokens.S1, w - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2,
                        l.Value, UiFlags.Wrap | UiFlags.Label, SentenceLines);
                    break;
                case UiFactKind.Meter:
                    UiDraw.GradeBar(s, at, x, y, w, l.Label, l.Fill, Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(l.Label ?? string.Empty, UiStyle.Body) + UiTokens.S2)));
                    break;
                case UiFactKind.Link:
                    UiDraw.LinkRow(s, at, x, y, w, UiTokens.Control, l.Value, l.Command, l.Arg);
                    break;
                case UiFactKind.Switch:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w - UiTokens.S16 * 2f, UiStyle.Body, UiRole.Text2, l.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Switch(s, at, x, y, w, l.On, null, l.Command, l.Arg, l.Disabled);
                    break;
                case UiFactKind.Step:
                    Step(s, at, l, x, y, w);
                    break;
                case UiFactKind.Person:
                {
                    float face = UiTokens.AvatarSmall, figure = Math.Min(UiDraw.SnapDown(w * 0.4f), UiDraw.Snap(UiTextRules.Width(l.Value ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1));
                    UiDraw.Portrait(s, at, x, y + UiDraw.SnapDown((UiTokens.S12 - face) / 2f), face, l.Label, UiColour.HueOf(l.Label ?? string.Empty), UiRole.None);
                    float tx = x + face + UiTokens.S3, tw = w - face - UiTokens.S3 - figure - UiTokens.S3;
                    float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                    float top = y + UiDraw.SnapDown((UiTokens.S12 - lh - sh) / 2f);
                    UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, l.Label, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, at, tx, top + lh, tw, UiStyle.Small, UiRole.Text2, l.Hint, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.TextMiddle(s, at, x + w - figure, y, UiTokens.S12, figure, UiStyle.BodyStrong, l.Ink == UiRole.None ? UiRole.Text : l.Ink, l.Value, UiFlags.Right | UiFlags.Truncate);
                    break;
                }
                case UiFactKind.Heading:
                    UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                    UiDraw.Text(s, at, x, y + UiTokens.S3, w, UiStyle.SectionHeader, UiRole.Text2, l.Label, UiFlags.Label | UiFlags.Truncate);
                    break;
            }
        }

        // An amount: its name at the left, the minus, the figure and the plus at the right, what the figure means under them.
        private static void Step(UiScene s, UiAt at, UiFactLine l, float x, float y, float w)
        {
            float lw = UiDraw.SnapDown(w * StepLabel);
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw - UiTokens.S2, UiStyle.Body, UiRole.Text, l.Label, UiFlags.Label | UiFlags.Truncate);
            float cx = x + lw, cw = w - lw, b = UiDraw.StepButton, by = y + (UiTokens.Control - b) / 2f;
            Square(s, at, cx, by, b, UiIcon.Minus, l.Command, l.Arg, l.Disabled || !l.CanLess);
            Square(s, at, cx + cw - b, by, b, UiIcon.Plus, l.Command2, l.Arg, l.Disabled || !l.CanMore);
            float fx = cx + b + UiTokens.S2, fw = cw - (b + UiTokens.S2) * 2f;
            int field = UiDraw.Box(s, at, fx, y, fw, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, at.In(field), fx + UiTokens.S2, y, UiTokens.Control, fw - UiTokens.S4, UiStyle.BodyStrong, UiRole.Text, l.Value, UiFlags.Centre | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(l.Hint))
            {
                UiDraw.Text(s, at, x, y + UiTokens.Control + UiTokens.S1, w, UiStyle.Small, UiRole.Text2, l.Hint, UiFlags.Wrap | UiFlags.Label, 4);
            }
        }

        private static void Square(UiScene s, UiAt at, float x, float y, float size, UiIcon icon, int command, int arg, bool disabled)
        {
            int face = UiDraw.Box(s, at, x, y, size, size, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, UiFlags.Middle | (disabled ? UiFlags.Disabled : UiFlags.None));
            float g = UiTokens.Icon;
            UiDraw.Icon(s, at.In(face), x + (size - g) / 2f, y + (size - g) / 2f, g, icon, disabled ? UiRole.Muted : UiRole.AccentText,
                UiFlags.Middle | UiFlags.Centre | (disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.Node(s, at, x, y, size, size, command, arg, face, disabled);
        }

        /// <summary>The height the buttons take at a width: in rows from the right, a row that does not fit wrapping under.</summary>
        public static float ActionsHeight(UiActionModel[] actions, float w, IUiKeys keys)
        {
            if (actions == null || actions.Length == 0)
            {
                return 0f;
            }
            int rows = 1;
            float used = 0f;
            for (int i = 0; i < actions.Length; i++)
            {
                float bw = Math.Min(w, UiDraw.ButtonWidth(actions[i].Kind, actions[i].Label, actions[i].Icon, actions[i].Key, keys));
                if (used > 0f && used + bw > w)
                {
                    rows++;
                    used = 0f;
                }
                used += bw + UiTokens.S3;
            }
            return rows * UiTokens.Control + (rows - 1) * UiTokens.S2;
        }

        /// <summary>The buttons in rows ending at the right edge, the last the main one, the row's height from <paramref name="y"/>.</summary>
        public static void Actions(UiScene s, UiAt at, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
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
