using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a form field is: a setting turned on or off, a value on a track, a choice of a few, or a key and its value.</summary>
    public enum UiFieldKind : byte
    {
        Toggle = 0,
        Slider,
        Choice,
        Pair,
        Note       // a line of words (what a choice gives, what to do next): no target
    }

    /// <summary>One field of a form, under the section header it opens (or null to continue the section above).</summary>
    public struct UiFieldModel
    {
        public UiFieldKind Kind;
        public string Section;
        public string Label;
        public string Value;     // a slider's figure, a choice's name, a pair's value
        public bool On;          // a toggle
        public float Share;      // a slider, 0 to 1
    }

    /// <summary>What a form shows: its title and line, its fields in sections, and its actions, the primary last.</summary>
    public sealed class UiFormModel
    {
        public string Title;
        public string About;
        public UiFieldModel[] Fields = new UiFieldModel[0];
        public int FieldCount;
        public float Offset;       // px the column with the actions has scrolled (the right one, or the only one)
        public float LeftOffset;   // px the left column has scrolled, when the form runs in two
        public UiActionModel[] Actions = new UiActionModel[0];
        public int ActionCount;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The form template, for settings and choices made before an action (an order, a campaign) and for a page of lines
    /// with its actions (a tech firm's). The title and its line make the page's head. The lines it opens with before any
    /// section (what to do next) stand in a band across the page with the actions beside them. Then the fields run in
    /// sections in two framed columns across the page, split where a section starts so the columns stand as level as
    /// they can, in the view's own order: what it says first reads first, at the left, and without a band the actions
    /// stand at the foot of the right one. A form of one section is one column as wide as the page. Each column is as
    /// tall as what it holds and scrolls when its fields outnumber the page (toggles, sliders, choices changed with left
    /// and right, key and value rows, lines).
    /// Commands: <see cref="FieldCommand"/> (the field's index) and <see cref="ActionCommand"/> (the action's).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiForm
    {
        public const int FieldCommand = 1, ActionCommand = 2;
        public const byte RegionLeft = 1, RegionRight = 2, RegionLead = 3;   // the d-pad's columns, and the band over them

        /// <summary>Lays a form out into a scene.</summary>
        public static void Lay(UiScene s, UiFormModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("form", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Window(s, frame, null, keys);   // the app's window over the world: the page is drawn inside it
            UiAt at = screen.In(bg);
            // The form's title stands in the masthead after the app's name, so the page opens on what it is about.
            float y = cy;
            if (!string.IsNullOrEmpty(m.About))
            {
                UiDraw.Text(s, at, cx, y, cw, UiStyle.Body, UiRole.Text2, m.About, UiFlags.Label | UiFlags.Wrap, 2);
                y += UiDraw.Snap(UiTextRules.WrappedHeight(m.About, UiStyle.Body, cw, 2)) + UiTokens.S6;
            }
            int lead = Lead(m);
            if (lead > 0 || (m.FieldCount == 0 && m.ActionCount > 0))
            {
                y += Band(s, at.InRegion(RegionLead), m, lead, cx, y, cw, keys) + UiTokens.S4;
            }
            if (lead < m.FieldCount)
            {
                float bodyH = UiDraw.SnapDown(cy + ch - y);
                float colW = UiDraw.SnapDown((cw - UiTokens.S4) / 2f);
                float footH = lead == 0 && m.ActionCount > 0 ? UiTokens.S6 + UiTokens.Control : 0f;
                int split = Split(m, lead, colW - UiTokens.S5 * 2f, footH);
                int panel;
                if (split <= lead)
                {
                    panel = Column(s, at.InRegion(RegionRight), m, lead, m.FieldCount, cx, y, cw, bodyH, footH, m.Offset);
                }
                else
                {
                    int left = Column(s, at.InRegion(RegionLeft), m, lead, split, cx, y, colW, bodyH, 0f, m.LeftOffset);
                    panel = Column(s, at.InRegion(RegionRight), m, split, m.FieldCount, cx + cw - colW, y, colW, bodyH, footH, m.Offset);
                    float lh = s.Items[left].H, rh = s.Items[panel].H;
                    if (Math.Abs(lh - rh) <= UiTokens.MostSlack - UiTokens.S6)
                    {
                        // Nearly level: one foot reads tidier than a step of a few px, while the shorter column's padding and
                        // the step together stay within the slack a frame may hold.
                        s.Items[left].H = s.Items[panel].H = Math.Max(lh, rh);
                    }
                }
                if (footH > 0f)
                {
                    Foot(s, at.InRegion(RegionRight), m, panel, keys);
                }
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
            UiDraw.Chrome(s, frame, m.Title);
        }

        // How many lines open the form before its first section (what to do next), or 0 when a choice comes first or a
        // section opens it: those lines and the form's actions make the band across the page.
        private static int Lead(UiFormModel m)
        {
            int lead = 0;
            while (lead < m.FieldCount && m.Fields[lead].Section == null && m.Fields[lead].Kind == UiFieldKind.Note)
            {
                lead++;
            }
            return lead < m.FieldCount && m.Fields[lead].Section == null ? 0 : lead;
        }

        // The lead band across the page: the opening lines at the left and the actions beside them at the right, as the
        // Tech app puts a page's next step and the button that takes it together. Returns the band's height.
        private static float Band(UiScene s, UiAt at, UiFormModel m, int lead, float x, float y, float w, IUiKeys keys)
        {
            float actionsW = 0f;
            for (int i = 0; i < m.ActionCount; i++)
            {
                actionsW += UiDraw.ButtonWidth(m.Actions[i].Kind, m.Actions[i].Label, m.Actions[i].Icon, m.Actions[i].Key, keys) + (i > 0 ? UiTokens.S3 : 0f);
            }
            float tx = x + UiTokens.S5 + UiTokens.S3, tw = w - UiTokens.S5 * 2f - UiTokens.S6 - (actionsW > 0f ? actionsW + UiTokens.S6 : 0f);
            float linesH = 0f;
            for (int i = 0; i < lead; i++)
            {
                linesH += UiTextRules.WrappedHeight(m.Fields[i].Label, UiStyle.Body, tw, 2) + (i > 0 ? UiTokens.S2 : 0f);
            }
            float h = UiDraw.Snap(UiTokens.S5 * 2f + Math.Max(linesH, m.ActionCount > 0 ? UiTokens.Control : 0f));
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = at.In(band);
            float ty = y + (h - linesH) / 2f;
            for (int i = 0; i < lead; i++)
            {
                ref UiFieldModel f = ref m.Fields[i];
                UiDraw.Text(s, inside, tx, ty, tw, UiStyle.Body, f.On ? UiRole.Text : UiRole.Text2, f.Label, UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, 2);
                ty += UiTextRules.WrappedHeight(f.Label, UiStyle.Body, tw, 2) + UiTokens.S2;
            }
            float bx = x + w - UiTokens.S5, by = y + UiDraw.SnapDown((h - UiTokens.Control) / 2f);
            for (int i = m.ActionCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                UiDraw.Button(s, inside, bx, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                bx -= UiTokens.S3;
            }
            return h;
        }

        // Where the right column begins: the section start after the lead that leaves the two columns most level (the
        // right one holding the actions too when there is no band), or the lead itself for one column (a single section
        // has nothing to split).
        private static int Split(UiFormModel m, int lead, float w, float footH)
        {
            float total = 0f;
            for (int i = lead; i < m.FieldCount; i++)
            {
                total += FieldHeight(ref m.Fields[i], w);
            }
            int best = lead;
            float bestTaller = float.MaxValue, left = 0f;
            for (int i = lead; i < m.FieldCount; i++)
            {
                if (i > lead && m.Fields[i].Section != null)
                {
                    float taller = Math.Max(left, total - left + footH);
                    if (taller < bestTaller)
                    {
                        bestTaller = taller;
                        best = i;
                    }
                }
                left += FieldHeight(ref m.Fields[i], w);
            }
            return best;
        }

        // One framed column of the fields from one index to another, scrolling when they outnumber its room, with room at
        // its foot for the actions when it holds them. Returns the column's frame.
        private static int Column(UiScene s, UiAt at, UiFormModel m, int from, int to, float px, float py, float pw, float most, float footH, float offset)
        {
            int panel = UiDraw.Box(s, at, px, py, pw, most, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            float x = px + UiTokens.S5, w = pw - UiTokens.S5 * 2f;
            // A section's header brings its own step of air, so only a column that opens on a line takes the panel's padding.
            float top = from < to && m.Fields[from].Section != null ? UiTokens.S1 : UiTokens.S5;
            float contentH = 0f;
            int targets = 0;
            for (int i = from; i < to; i++)
            {
                contentH += FieldHeight(ref m.Fields[i], w);
                targets += m.Fields[i].Kind != UiFieldKind.Pair && m.Fields[i].Kind != UiFieldKind.Note ? 1 : 0;
            }
            float room = UiDraw.SnapDown(most - top - UiTokens.S5 - footH);
            float vh = Math.Min(room, UiDraw.Snap(contentH));
            offset = Math.Max(0f, Math.Min(offset, contentH - vh));
            short clip = s.AddClip(x - UiTokens.S3, py + top, w + UiTokens.S6, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            short list = targets > 0 ? s.AddList(targets, 0, targets, clip, -1) : (short)-1;
            int index = 0;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = 0, Order = -1, Region = at.Region };
            float fy = 0f, fx = UiTokens.S3;
            for (int i = from; i < to; i++)
            {
                ref UiFieldModel f = ref m.Fields[i];
                UiAt arriving = inClip.Arriving(i);
                if (f.Section != null)
                {
                    fy += UiDraw.SectionHeader(s, arriving, fx - UiTokens.S3, fy, w, f.Section) + UiTokens.S2;
                }
                int node = -1;
                switch (f.Kind)
                {
                    case UiFieldKind.Toggle:
                        node = UiDraw.Toggle(s, arriving, fx, fy, w, f.Label, f.On, FieldCommand, i);
                        fy += UiTokens.Control;
                        break;
                    case UiFieldKind.Slider:
                        node = UiDraw.Slider(s, arriving, fx, fy, w, f.Label, f.Value, f.Share, FieldCommand, i);
                        fy += UiTokens.Control + UiTokens.S4;
                        break;
                    case UiFieldKind.Choice:
                        node = UiDraw.Selector(s, arriving, fx, fy, w, f.Label, f.Value, FieldCommand, i);
                        fy += UiTokens.Control;
                        break;
                    case UiFieldKind.Note:
                        float nh = UiTextRules.WrappedHeight(f.Label, UiStyle.Body, w - UiTokens.S6, 2);
                        UiDraw.Text(s, arriving, fx + UiTokens.S3, fy, w - UiTokens.S6, UiStyle.Body, f.On ? UiRole.Text : UiRole.Text2, f.Label, UiFlags.Wrap | UiFlags.Truncate, 2);
                        fy += UiDraw.Snap(nh) + UiTokens.S2;
                        break;
                    default:
                        fy += UiDraw.KeyValue(s, arriving, fx + UiTokens.S3, fy, w - UiTokens.S6, f.Label, f.Value);
                        break;
                }
                if (node >= 0)
                {
                    s.Nodes[node].List = list;
                    s.Nodes[node].Index = index++;
                }
                fy += UiTokens.S1;
            }
            s.Items[panel].H = UiDraw.Snap(top + vh + footH + UiTokens.S5);
            return panel;
        }

        // The actions at the foot of the column that holds them, the primary at the right. They stand on its floor, so a
        // column made level with its neighbour keeps the air over its buttons rather than under them.
        private static void Foot(UiScene s, UiAt at, UiFormModel m, int panel, IUiKeys keys)
        {
            ref UiItem p = ref s.Items[panel];
            float by = p.Y + p.H - UiTokens.S5 - UiTokens.Control;
            float bx = p.X + p.W - UiTokens.S5;
            UiAt inside = at.In(panel);
            for (int i = m.ActionCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                UiDraw.Button(s, inside, bx, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                bx -= UiTokens.S3;
            }
        }

        private static float FieldHeight(ref UiFieldModel f, float w)
        {
            float h = f.Section != null ? UiDraw.Snap(UiTokens.S4 + UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 : 0f;
            switch (f.Kind)
            {
                case UiFieldKind.Slider: return h + UiTokens.Control + UiTokens.S4 + UiTokens.S1;
                case UiFieldKind.Pair: return h + UiTokens.PairRow + UiTokens.S1;
                case UiFieldKind.Note: return h + UiDraw.Snap(UiTextRules.WrappedHeight(f.Label, UiStyle.Body, w - UiTokens.S6, 2)) + UiTokens.S2 + UiTokens.S1;
                default: return h + UiTokens.Control + UiTokens.S1;
            }
        }
    }
}
