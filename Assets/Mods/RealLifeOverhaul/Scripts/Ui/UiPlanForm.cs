using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a field of the Planner's form is.</summary>
    public enum UiPlanFieldKind : byte
    {
        Text = 0,      // words typed through the host's text box: the note's title
        Selector,      // a value stepped with a minus and a plus: the day, the hour, the reminder
        Choices,       // a few ready words as chips: the ready titles, once or every day or every week
        Switch,        // a kind shown or hidden
        Place,         // where it is, with its buttons
        Heading,       // a section's label
        Line           // a line of words
    }

    /// <summary>One field of the form.</summary>
    public struct UiPlanField
    {
        public UiPlanFieldKind Kind;
        public string Label;
        public string Value;          // a text's words, a selector's value, a switch's word ("On"), a place's name, a line's words
        public string Hint;           // what an empty text asks for
        public bool CanLess, CanMore; // a selector's ends
        public bool On;               // a switch
        public string[] Options;      // a choice's chips
        public int Chosen;            // the chip chosen, or -1
        public string Action, Action2;// a place's buttons: "This building", "Remove place"; null for none
    }

    /// <summary>What the form shows: its title, its fields, its actions, and how far it has scrolled.</summary>
    public sealed class UiPlanFormModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Info;
        public UiPlanField[] Fields = new UiPlanField[0];
        public UiActionModel[] Actions = new UiActionModel[0];
        public float Offset;                  // px the fields have scrolled
        public float[] Tops = new float[0];   // written by Lay: where each field stands in the scrolled column, for the keys' reveal
    }

    /// <summary>
    /// The Planner's form, a block of an app window's page: a note's title, day, hour, repeat, place and reminder, and the
    /// settings' switches. The labels stand at the left and the controls in a column at the right, as the game's own forms read;
    /// the fields scroll when they outnumber the room, every one a target the keys reach, and beside them (or under them in a
    /// narrow window) the actions. Commands: <see cref="UiPlanPage.BackCommand"/>, <see cref="FieldCommand"/> (a text's or a
    /// switch's index), <see cref="LessCommand"/> and <see cref="MoreCommand"/> (a selector's index), <see cref="OptionCommand"/>
    /// (a chip: <see cref="OptionArg"/>), <see cref="PlaceCommand"/> (a place's button: field * 2 + which) and
    /// <see cref="ActionCommand"/> (an action's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPlanForm
    {
        public const int FieldCommand = 1207, LessCommand = 1208, MoreCommand = 1209, OptionCommand = 1210, ActionCommand = 1211, PlaceCommand = 1212;
        public const float SelectorWidth = 300f;     // px: the controls' column at most
        private const float LabelMost = 180f;        // px: the labels' column at most
        private const float ChipGap = UiTokens.S2;

        /// <summary>A chip's argument: its field and its place among the field's chips.</summary>
        public static int OptionArg(int field, int option) => (field << 5) | (option & 31);

        public static int OptionField(int arg) => arg >> 5;

        public static int OptionIndex(int arg) => arg & 31;

        /// <summary>Lays the form into the room an app window's Shell gave it.</summary>
        public static void Lay(UiScene s, UiAt at, UiPlanFormModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            at = at.InRegion(3);
            float top = UiPlanPage.Head(s, at, m.Title, m.Icon, UiRole.None, null, UiRole.None, x, y, w);
            bool beside = UiPlanPage.Beside(w);
            float cw = beside ? UiPlanPage.Column : w;
            float actionsH = beside ? 0f : m.Actions.Length * (UiTokens.Control + UiTokens.S2);
            float vh = Math.Max(UiTokens.S16, y + h - top - actionsH);
            float labelW = Math.Min(LabelMost, UiDraw.SnapDown(cw * 0.32f));
            float controlX = labelW + UiTokens.S3, controlW = cw - controlX;
            int n = m.Fields.Length;
            if (m.Tops.Length != n)
            {
                m.Tops = new float[n];
            }
            float content = 0f;
            for (int i = 0; i < n; i++)
            {
                m.Tops[i] = content;
                content += Height(ref m.Fields[i], cw, controlW) + UiTokens.S2;
            }
            float offset = Math.Max(0f, Math.Min(m.Offset, Math.Max(0f, content - vh)));
            m.Offset = offset;
            short clip = s.AddClip(x - UiTokens.S1, top, cw + UiTokens.S3, vh, offset);
            s.Clips[clip].ContentHeight = content;
            UiDraw.ScrollThumb(s, at, x + cw + UiTokens.S2, top, vh, offset, content);
            short list = n > 0 ? s.AddList(n, 0, n, clip, -1) : (short)-1;
            var inside = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = at.Order, Region = at.Region };
            for (int i = 0; i < n; i++)
            {
                Field(s, inside, ref m.Fields[i], i, list, UiTokens.S1, m.Tops[i], cw, labelW, UiTokens.S1 + controlX, controlW);
            }
            float ax = beside ? x + cw + UiTokens.S8 : x;
            float ay = beside ? top : y + h - actionsH;
            UiPlanPage.Actions(s, at, m.Actions, ActionCommand, ax, ay, beside ? Math.Min(UiPlanPage.ActionsWidth, x + w - ax) : Math.Min(UiPlanPage.ActionsWidth, w), keys);
        }

        // A field's height.
        private static float Height(ref UiPlanField f, float cw, float controlW)
        {
            switch (f.Kind)
            {
                case UiPlanFieldKind.Heading:
                    return UiTokens.S4 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
                case UiPlanFieldKind.Line:
                    return UiDraw.Snap(UiTextRules.WrappedHeight(f.Value ?? string.Empty, UiStyle.Body, cw, 3));
                case UiPlanFieldKind.Choices:
                    return Math.Max(UiTokens.Control, Rows(f.Options, controlW) * (UiTokens.Chip + ChipGap) - ChipGap);
                case UiPlanFieldKind.Place:
                    return UiTokens.Control + (string.IsNullOrEmpty(f.Action) && string.IsNullOrEmpty(f.Action2) ? 0f : UiTokens.S2 + UiTokens.ControlSmall);
                default:
                    return UiTokens.Control;
            }
        }

        private static float ChipWidth(string label, float most) => UiDraw.Snap(Math.Min(most, UiTextRules.Width(label ?? string.Empty, UiStyle.Button) + UiTokens.S8));

        // The rows the chips wrap into in a width.
        private static int Rows(string[] options, float w)
        {
            int rows = 1;
            float cx = 0f;
            for (int i = 0; options != null && i < options.Length; i++)
            {
                float bw = ChipWidth(options[i], w);
                if (cx > 0f && cx + bw > w)
                {
                    rows++;
                    cx = 0f;
                }
                cx += bw + ChipGap;
            }
            return rows;
        }

        private static void Field(UiScene s, UiAt at, ref UiPlanField f, int index, short list, float x, float y, float cw, float labelW, float controlX, float controlW)
        {
            float h = UiTokens.Control;
            switch (f.Kind)
            {
                case UiPlanFieldKind.Heading:
                    UiDraw.Text(s, at, x, y + UiTokens.S4, cw, UiStyle.SectionHeader, UiRole.Muted, f.Label, UiFlags.Label | UiFlags.Truncate);
                    return;
                case UiPlanFieldKind.Line:
                    UiDraw.Text(s, at, x, y, cw, UiStyle.Body, UiRole.Text2, f.Value, UiFlags.Label | UiFlags.Wrap, 3);
                    return;
                case UiPlanFieldKind.Switch:
                {
                    float sw = Math.Min(cw * 0.4f, 140f);
                    UiDraw.TextMiddle(s, at, x, y, h, cw - sw - UiTokens.S3, UiStyle.Body, UiRole.Text, f.Label, UiFlags.Label | UiFlags.Truncate);
                    int node = UiDraw.Switch(s, at, x + cw - sw, y, sw, f.On, f.Value, FieldCommand, index, false);
                    s.Nodes[node].List = list;
                    s.Nodes[node].Index = index;
                    return;
                }
            }
            UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
            switch (f.Kind)
            {
                case UiPlanFieldKind.Text:
                {
                    bool typed = !string.IsNullOrEmpty(f.Value);
                    int face = UiDraw.Box(s, at, controlX, y, controlW, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
                    UiDraw.TextMiddle(s, at.In(face), controlX + UiTokens.S4, y, h, controlW - UiTokens.S4 * 2f, UiStyle.Body, typed ? UiRole.Text : UiRole.Muted,
                        typed ? f.Value : f.Hint, typed ? UiFlags.Truncate : UiFlags.Label | UiFlags.Truncate);
                    int node = UiDraw.Node(s, at, controlX, y, controlW, h, FieldCommand, index, face);
                    s.Nodes[node].List = list;
                    s.Nodes[node].Index = index;
                    break;
                }
                case UiPlanFieldKind.Selector:
                    Selector(s, at, controlX + Math.Max(0f, controlW - SelectorWidth), y, Math.Min(controlW, SelectorWidth), f.Value, f.CanLess, f.CanMore, LessCommand, MoreCommand, index, list);
                    break;
                case UiPlanFieldKind.Choices:
                {
                    float cx = 0f, cy = 0f;
                    for (int i = 0; f.Options != null && i < f.Options.Length && i < 32; i++)
                    {
                        float bw = ChipWidth(f.Options[i], controlW);
                        if (cx > 0f && cx + bw > controlW)
                        {
                            cx = 0f;
                            cy += UiTokens.Chip + ChipGap;
                        }
                        bool on = i == f.Chosen;
                        int face = UiDraw.Box(s, at, controlX + cx, y + cy, bw, UiTokens.Chip, on ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusMedium,
                            on ? UiFlags.Selected : UiFlags.None);
                        UiDraw.TextMiddle(s, at.In(face), controlX + cx, y + cy, UiTokens.Chip, bw, UiStyle.Button, on ? UiRole.OnAccent : UiRole.Text, f.Options[i],
                            UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                        int node = UiDraw.Node(s, at, controlX + cx, y + cy, bw, UiTokens.Chip, OptionCommand, OptionArg(index, i), face);
                        s.Nodes[node].List = list;
                        s.Nodes[node].Index = index;
                        cx += bw + ChipGap;
                    }
                    break;
                }
                case UiPlanFieldKind.Place:
                {
                    bool has = !string.IsNullOrEmpty(f.Value);
                    UiDraw.TextMiddle(s, at, controlX, y, h, controlW, UiStyle.Body, has ? UiRole.Text : UiRole.Muted, has ? f.Value : f.Hint,
                        has ? UiFlags.Truncate : UiFlags.Label | UiFlags.Truncate);
                    float bx = controlX, by = y + h + UiTokens.S2;
                    string[] words = { f.Action, f.Action2 };
                    for (int k = 0; k < 2; k++)
                    {
                        if (string.IsNullOrEmpty(words[k]))
                        {
                            continue;
                        }
                        float bw = UiDraw.Snap(Math.Min(controlW, UiTextRules.Width(words[k], UiStyle.Button) + UiTokens.S8));
                        int face = UiDraw.Box(s, at, bx, by, bw, UiTokens.ControlSmall, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
                        UiDraw.TextMiddle(s, at.In(face), bx, by, UiTokens.ControlSmall, bw, UiStyle.Button, k == 1 ? UiRole.DangerText : UiRole.AccentText, words[k],
                            UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                        int node = UiDraw.Node(s, at, bx, by, bw, UiTokens.ControlSmall, PlaceCommand, index * 2 + k, face);
                        s.Nodes[node].List = list;
                        s.Nodes[node].Index = index;
                        bx += bw + UiTokens.S2;
                    }
                    break;
                }
            }
        }

        /// <summary>A value between a minus and a plus: the value in a field, each button greyed at its end. Written for a row of its own, or a list's row.</summary>
        public static void Selector(UiScene s, UiAt at, float x, float y, float w, string value, bool canLess, bool canMore, int lessCommand, int moreCommand, int arg, short list)
        {
            float b = UiTokens.ControlSmall, h = UiTokens.Control, by = y + (h - b) / 2f;
            int drawn = s.ItemCount;
            int less = UiDraw.IconButton(s, at, x, by, b, UiIcon.Minus, UiRole.AccentText, lessCommand, arg);
            if (!canLess)
            {
                UiDraw.Dim(s, drawn, less);
            }
            float fx = x + b + UiTokens.S2, fw = w - (b + UiTokens.S2) * 2f;
            int face = UiDraw.Box(s, at, fx, y, fw, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, at.In(face), fx + UiTokens.S2, y, h, fw - UiTokens.S4, UiStyle.BodyStrong, UiRole.Text, value, UiFlags.Centre | UiFlags.Truncate);
            drawn = s.ItemCount;
            int more = UiDraw.IconButton(s, at, x + w - b, by, b, UiIcon.Plus, UiRole.AccentText, moreCommand, arg);
            if (!canMore)
            {
                UiDraw.Dim(s, drawn, more);
            }
            if (list >= 0)
            {
                s.Nodes[less].List = list;
                s.Nodes[less].Index = arg;
                s.Nodes[more].List = list;
                s.Nodes[more].Index = arg;
            }
        }
    }
}
