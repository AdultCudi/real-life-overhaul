using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a form field is: a setting turned on or off, a value on a track, a choice of a few, or a key and its value.</summary>
    public enum UiFieldKind : byte
    {
        Toggle = 0,
        Slider,
        Choice,
        Pair
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
        public float Offset;     // px the fields have scrolled
        public UiActionModel[] Actions = new UiActionModel[0];
        public int ActionCount;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The form template, for settings and choices made before an action (an order, a campaign): one framed card at the
    /// middle, its title and line, its fields in sections in a frame that scrolls when they outnumber its room (toggles,
    /// sliders, choices changed with left and right, key and value rows), its actions at the foot; the card as tall as
    /// what it holds. Commands:
    /// <see cref="FieldCommand"/> (the field's index) and <see cref="ActionCommand"/> (the action's). Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiForm
    {
        public const int FieldCommand = 1, ActionCommand = 2;
        public const float PanelWidth = 880f;   // px: a form reads down one column

        /// <summary>Lays a form out into a scene.</summary>
        public static void Lay(UiScene s, UiFormModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("form", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            float pw = Math.Min(cw, PanelWidth);
            float px = cx + UiDraw.SnapDown((cw - pw) / 2f);
            int items = s.ItemCount, nodes = s.NodeCount, clips = s.ClipCount;
            int panel = UiDraw.Box(s, at, px, cy, pw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = at.In(panel);
            float x = px + UiTokens.S8, w = pw - UiTokens.S8 * 2f, y = cy + UiTokens.S8;
            UiDraw.Text(s, inside, x, y, w, UiStyle.SectionTitle, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionTitle)) + UiTokens.S1;
            if (!string.IsNullOrEmpty(m.About))
            {
                UiDraw.Text(s, inside, x, y, w, UiStyle.Body, UiRole.Text2, m.About, UiFlags.Label | UiFlags.Wrap, 2);
                y += UiDraw.Snap(UiTextRules.WrappedHeight(m.About, UiStyle.Body, w, 2));
            }
            y += UiTokens.S4;
            float contentH = 0f;
            for (int i = 0; i < m.FieldCount; i++)
            {
                contentH += FieldHeight(ref m.Fields[i]);
            }
            float room = UiDraw.SnapDown(cy + ch - UiTokens.S8 - UiTokens.Control - UiTokens.S6 - y);
            float vh = Math.Min(room, UiDraw.Snap(contentH));
            float foot = y + vh + UiTokens.S6;
            float offset = Math.Max(0f, Math.Min(m.Offset, contentH - vh));
            short clip = s.AddClip(x - UiTokens.S3, y, w + UiTokens.S6, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            int targets = 0;
            for (int i = 0; i < m.FieldCount; i++)
            {
                targets += m.Fields[i].Kind != UiFieldKind.Pair ? 1 : 0;
            }
            short list = s.AddList(targets, 0, targets, clip, -1);
            int index = 0;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = 0, Order = -1, Region = at.Region };
            float fy = 0f, fx = UiTokens.S3;
            for (int i = 0; i < m.FieldCount; i++)
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
            float bx = px + pw - UiTokens.S8;
            for (int i = m.ActionCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                UiDraw.Button(s, inside, bx, foot, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                bx -= UiTokens.S3;
            }
            float ph = UiDraw.Snap(foot + UiTokens.Control + UiTokens.S8 - cy);
            s.Items[panel].H = ph;
            UiDraw.Lift(s, items, nodes, clips, UiDraw.SnapDown((ch - ph) / 2f));
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, 0f, frame.BandY, frame.Width, frame.Band, keys);
            UiDraw.Chrome(s, frame);
        }

        private static float FieldHeight(ref UiFieldModel f)
        {
            float h = f.Section != null ? UiDraw.Snap(UiTokens.S4 + UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 : 0f;
            switch (f.Kind)
            {
                case UiFieldKind.Slider: return h + UiTokens.Control + UiTokens.S4 + UiTokens.S1;
                case UiFieldKind.Pair: return h + UiTokens.PairRow + UiTokens.S1;
                default: return h + UiTokens.Control + UiTokens.S1;
            }
        }
    }
}
