using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's overlays: the prompt bar, a question, the call, and the toast.</summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// The frame's own bars, flat: the masthead (the app, the place the page shows, the money and time, the rule) and
        /// the band's hairline. Never a target. The place stands after the app's name in the masthead rather than as a
        /// heading of its own, so no page repeats the masthead's words under it ("The city's people" twice), and it is left
        /// out when it is the app's own name.
        /// </summary>
        public static void Chrome(UiScene s, UiFrame frame, string place = null)
        {
            if (frame.Top > 0f)
            {
                int bar = Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.Top, UiRole.Header, UiRole.None, 0f, UiFlags.Decor);
                UiAt on = UiAt.Screen.In(bar);
                Box(s, on, 0f, frame.Top - UiTokens.FrameRule, frame.Width, UiTokens.FrameRule, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
                frame.Content(out float cx, out float _, out float cw, out float _);
                string status = UiChrome.Status != null ? UiChrome.Status() : null;
                float sw = string.IsNullOrEmpty(status) ? 0f : UiTextRules.Width(status, UiStyle.Title) + UiTokens.S1;
                float tw = cw - sw - UiTokens.S8;
                float bandH = frame.Top - UiTokens.FrameRule;
                string app = frame.Title ?? string.Empty;
                if (Crumb(app, place))
                {
                    float aw = Math.Min(tw, Snap(UiTextRules.Width(app, UiStyle.AppTitle) + UiTokens.S1));   // widths on the grid, so the mark and the place stand on it
                    TextMiddle(s, on, cx, 0f, bandH, aw, UiStyle.AppTitle, UiRole.Text, app, UiFlags.Truncate);
                    float gw = Snap(UiTextRules.Width(CrumbMark, UiStyle.AppTitle) + UiTokens.S1);
                    float gx = cx + aw + UiTokens.S3;
                    float pw = cx + tw - (gx + gw + UiTokens.S3);
                    if (pw > UiTokens.S16)
                    {
                        TextMiddle(s, on, gx, 0f, bandH, gw, UiStyle.AppTitle, UiRole.Muted, CrumbMark, UiFlags.None);
                        TextMiddle(s, on, gx + gw + UiTokens.S3, 0f, bandH, pw, UiStyle.AppTitle, UiRole.Text2, place, UiFlags.Truncate);
                    }
                }
                else
                {
                    TextMiddle(s, on, cx, 0f, bandH, tw, UiStyle.AppTitle, UiRole.Text, app, UiFlags.Truncate);
                }
                if (sw > 0f)
                {
                    TextMiddle(s, on, cx + cw - sw, 0f, frame.Top - UiTokens.FrameRule, sw, UiStyle.Title, UiRole.Text2, status, UiFlags.Right);
                }
            }
            if (frame.Band > 0f)
            {
                Box(s, UiAt.Screen, 0f, frame.BandY, frame.Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            }
        }

        /// <summary>The mark between the app's name and the place in the masthead.</summary>
        public const string CrumbMark = "\u203A";

        /// <summary>Whether a place is shown after the app's name: only one with words of its own.</summary>
        public static bool Crumb(string app, string place)
        {
            return !string.IsNullOrEmpty(place) && !string.Equals(place.Trim(), (app ?? string.Empty).Trim(), StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// The prompt bar: each of the scene's prompts as its key's cap (or the pad's glyph) and its words in the game's
        /// own capitals, centred in a band. A TabPrev prompt shows both tab keys. Ink and caps are the band's own (the
        /// game's bottom bar), the same in both themes.
        /// </summary>

        /// <summary>
        /// The band of a frame. With the mouse leading, Move and Select go, since the mouse does those by pointing and
        /// clicking. The shortcuts stay (a target's key, the tab keys, Back), as they work with the mouse in hand and are
        /// quicker than travelling the cursor.
        /// </summary>
        public static void PromptBar(UiScene s, UiFrame frame, IUiKeys keys)
        {
            string owner = BackOwner(s);
            for (int i = 0; owner != null && i < s.PromptCount; i++)
            {
                if (s.Prompts[i].Action == UiAction.Back)
                {
                    s.Prompts[i].Label = owner;
                }
            }
            if (frame.Pointer)
            {
                int kept = 0;
                for (int i = 0; i < s.PromptCount; i++)
                {
                    if (s.Prompts[i].Action != UiAction.Move && s.Prompts[i].Action != UiAction.Confirm)
                    {
                        s.Prompts[kept++] = s.Prompts[i];
                    }
                }
                s.PromptCount = kept;
            }
            PromptBar(s, 0f, frame.BandY, frame.Width, frame.Band, keys);
        }

        // The words of the button that answers Escape on the top layer (a form's Cancel, a question's Keep), or null.
        private static string BackOwner(UiScene s)
        {
            int node = UiFocusRules.Keyed(s, UiAction.Back);
            int face = node >= 0 ? s.Nodes[node].Face : -1;
            for (int i = face + 1; face >= 0 && i < s.ItemCount; i++)
            {
                if (s.Items[i].Kind == UiKind.Text && (s.Items[i].Flags & UiFlags.Label) != 0 && Inside(s, i, face) && !string.IsNullOrEmpty(s.Items[i].Text))
                {
                    return s.Items[i].Text;
                }
            }
            return null;
        }

        public static void PromptBar(UiScene s, float x, float y, float w, float h, IUiKeys keys)
        {
            s.PromptX = x;
            s.PromptY = y;
            s.PromptW = w;
            s.PromptH = h;
            float total = 0f;
            for (int i = 0; i < s.PromptCount; i++)
            {
                total += PromptWidth(ref s.Prompts[i], keys) + (i > 0 ? UiTokens.S8 : 0f);
            }
            float cx = x + Math.Max(0f, (w - total) / 2f);
            float capY = y + (h - UiTokens.KeyCap) / 2f;
            UiAt at = UiAt.Screen;
            for (int i = 0; i < s.PromptCount; i++)
            {
                ref UiPrompt p = ref s.Prompts[i];
                int first = s.ItemCount;
                cx += KeyCap(s, at, cx, capY, p.Action, keys, UiRole.PromptCap, UiRole.PromptInk);
                if (p.Action == UiAction.TabPrev)
                {
                    cx += UiTokens.S1;
                    cx += KeyCap(s, at, cx, capY, UiAction.TabNext, keys, UiRole.PromptCap, UiRole.PromptInk);
                }
                cx += UiTokens.S2;
                float lw = UiTextRules.Width(p.Label, UiStyle.Prompt) + UiTokens.S1;
                TextMiddle(s, at, cx, y, h, lw, UiStyle.Prompt, UiRole.PromptInk, p.Label, UiFlags.Label);
                cx += lw + UiTokens.S8;
                for (int k = first; k < s.ItemCount; k++)
                {
                    s.Items[k].Flags |= UiFlags.Centre | UiFlags.Middle | UiFlags.Prompt;
                }
            }
        }

        private static float PromptWidth(ref UiPrompt p, IUiKeys keys)
        {
            float w = KeyCapWidth(p.Action, keys) + UiTokens.S2 + UiTextRules.Width(p.Label, UiStyle.Prompt) + UiTokens.S1;
            return p.Action == UiAction.TabPrev ? w + UiTokens.S1 + KeyCapWidth(UiAction.TabNext, keys) : w;
        }

        /// <summary>The scrim behind an overlay: the screen beneath darkened, no blur.</summary>
        public static int Scrim(UiScene s, float x, float y, float w, float h, byte layer)
        {
            var at = new UiAt { Parent = -1, Clip = -1, Layer = layer, Order = -1 };
            return Box(s, at, x, y, w, h, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);
        }

        /// <summary>
        /// A question over the screen: its title, what it means, and its answers at the bottom right, the confirming one
        /// last. Focus stays in it until it is answered or Back closes it. Returns the card's height.
        /// </summary>
        public static float Question(UiScene s, float areaX, float areaY, float areaW, float areaH, string title, string body, UiActionModel[] answers, int answerCount,
            IUiKeys keys)
        {
            Scrim(s, areaX, areaY, areaW, areaH, 1);
            float cw = Math.Min(areaW - UiTokens.S16 * 2f, UiTokens.S16 * 10f);
            float inner = cw - UiTokens.S8 * 2f;
            float th = UiTextRules.LineHeight(UiStyle.SectionTitle);
            float bh = UiTextRules.WrappedHeight(body, UiStyle.Body, inner, 6);
            float ch = Snap(UiTokens.S8 + th + UiTokens.S3 + bh + UiTokens.S6 + UiTokens.Control + UiTokens.S8);
            float cx = areaX + SnapDown((areaW - cw) / 2f), cy = areaY + SnapDown((areaH - ch) / 2f);
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 };
            int card = Box(s, at, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);   // its edge parts it from the dimmed page
            UiAt inside = at.In(card);
            Text(s, inside, cx + UiTokens.S8, cy + UiTokens.S8, inner, UiStyle.SectionTitle, UiRole.Text, title, UiFlags.Label);
            Text(s, inside, cx + UiTokens.S8, cy + UiTokens.S8 + th + UiTokens.S3, inner, UiStyle.Body, UiRole.Text2, body, UiFlags.Wrap | UiFlags.Label | UiFlags.Middle, 6);
            float bx = cx + cw - UiTokens.S8, by = cy + ch - UiTokens.S8 - UiTokens.Control;
            for (int i = answerCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref answers[i];
                float bw = ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                Button(s, inside, bx, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                bx -= UiTokens.S3;
            }
            return ch;
        }

        /// <summary>
        /// The call: the contact's avatar in its ring, the name, the line's state in capitals, what the contact offers on
        /// the call as rows with their prices, and the red End, on a solid card of its own over the dimmed screen (as a
        /// question stands), so nothing of the page shows through its words. Focus is held in it.
        /// </summary>
        public static void Call(UiScene s, float areaX, float areaY, float areaW, float areaH, string name, float hue, string state, bool connected,
            UiServiceModel[] options, int optionCount, string end, int optionCommand, int endCommand, float dimX, float dimY, float dimW, float dimH)
        {
            Scrim(s, dimX, dimY, dimW, dimH, 1);
            var on = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 };
            const float size = UiTokens.S16 * 2f;
            float colW = Math.Min(areaW - UiTokens.S12 * 2f - UiTokens.S8 * 2f, UiTokens.S16 * 7f);
            float nameH = UiTextRules.LineHeight(UiStyle.Hero), stateH = UiTextRules.LineHeight(UiStyle.Prompt);
            float rowsH = optionCount > 0 ? optionCount * (UiTokens.Control + UiTokens.S2) + UiTokens.S4 : 0f;
            float total = size + UiTokens.S6 + Snap(nameH + UiTokens.S2 + stateH) + UiTokens.S8 + rowsH + UiTokens.S16;
            float cardW = colW + UiTokens.S8 * 2f, cardH = Snap(total + UiTokens.S8 * 2f);
            float cardX = areaX + SnapDown((areaW - cardW) / 2f), cardY = areaY + SnapDown(Math.Max(0f, (areaH - cardH) / 2f));
            int card = Box(s, on, cardX, cardY, cardW, cardH, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);   // its edge parts it from the dimmed page
            UiAt at = on.In(card);
            float top = cardY + UiTokens.S8 + UiTokens.S1;
            float cx = cardX + SnapDown((cardW - size) / 2f);
            Circle(s, at, cx - UiTokens.S1, top - UiTokens.S1, size + UiTokens.S2, size + UiTokens.S2, connected ? UiRole.Ok : UiRole.Accent, UiFlags.Decor);
            Circle(s, at, cx - UiTokens.Hairline * 2f, top - UiTokens.Hairline * 2f, size + UiTokens.S1, size + UiTokens.S1, UiRole.Panel, UiFlags.Decor);
            Avatar(s, at, cx, top, size, name, hue);
            float y = top + size + UiTokens.S6;
            float colX = cardX + UiTokens.S8;
            Text(s, at, colX, y, colW, UiStyle.Hero, UiRole.Text, name, UiFlags.Centre | UiFlags.Truncate);
            y += nameH + UiTokens.S2;
            Text(s, at, colX, y, colW, UiStyle.Prompt, connected ? UiRole.OkText : UiRole.AccentText, state, UiFlags.Centre | UiFlags.Label | UiFlags.Middle);
            y = Snap(y + stateH + UiTokens.S8 - cardY) + cardY;
            for (int i = 0; i < optionCount; i++)
            {
                ref UiServiceModel o = ref options[i];
                int face = Box(s, at, colX, y, colW, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, o.Disabled ? UiFlags.Disabled : UiFlags.None);
                float pw = UiTextRules.Width(o.Price, UiStyle.SmallStrong) + UiTokens.S1;
                TextMiddle(s, at.In(face), colX + UiTokens.S5, y, UiTokens.Control, colW - pw - UiTokens.S5 * 2f - UiTokens.S3, UiStyle.Button, UiRole.Text, o.Title, UiFlags.Truncate);
                TextMiddle(s, at.In(face), colX + colW - UiTokens.S5 - pw, y, UiTokens.Control, pw, UiStyle.SmallStrong, UiRole.AccentText, o.Price, UiFlags.Right);
                Node(s, at, colX, y, colW, UiTokens.Control, optionCommand, i, face, o.Disabled);
                y += UiTokens.Control + UiTokens.S2;
            }
            y += optionCount > 0 ? UiTokens.S4 : 0f;
            float ex = cardX + SnapDown((cardW - UiTokens.S16) / 2f);
            int endFace = Circle(s, at, ex, y, UiTokens.S16, UiTokens.S16, UiRole.Danger);
            TextMiddle(s, at.In(endFace), ex, y, UiTokens.S16, UiTokens.S16, UiStyle.Button, UiRole.OnAccent, end, UiFlags.Centre | UiFlags.Label);   // a button's words, as every button has
            int n = Node(s, at, ex, y, UiTokens.S16, UiTokens.S16, endCommand, 0, endFace);
            if (optionCount == 0)
            {
                s.FocusFirst = n;
            }
        }

        /// <summary>A toast at the bottom centre of an area: the accent disc with a tick, what happened and to whom; above every layer.</summary>
        public static void Toast(UiScene s, float areaX, float areaY, float areaW, float areaH, string title, string sub)
        {
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 2, Order = -1 };
            float th = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
            float tw = Math.Max(UiTextRules.Width(title, UiStyle.Title), UiTextRules.Width(sub, UiStyle.Small)) + UiTokens.S1;
            const float disc = UiTokens.S10;
            float w = Snap(UiTokens.S2 + disc + UiTokens.S4 + tw + UiTokens.S6);
            float h = UiTokens.S16 - UiTokens.S2;
            float x = areaX + SnapDown((areaW - w) / 2f), y = areaY + areaH - UiTokens.S8 - h;
            int face = Box(s, at, x, y, w, h, UiRole.Field, UiRole.RowSelEdge, UiTokens.RadiusLarge, UiFlags.Middle);
            UiAt inside = at.In(face);
            int d = Circle(s, inside, x + UiTokens.S2, y + (h - disc) / 2f, disc, disc, UiRole.Accent, UiFlags.Middle);
            Icon(s, at.In(d), x + UiTokens.S2 + (disc - UiTokens.Icon) / 2f, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Check, UiRole.OnAccent, UiFlags.Middle);
            float ty = y + (h - th - sh) / 2f;
            Text(s, inside, x + UiTokens.S2 + disc + UiTokens.S4, ty, tw, UiStyle.Title, UiRole.Text, title, UiFlags.Middle | UiFlags.Truncate);
            Text(s, inside, x + UiTokens.S2 + disc + UiTokens.S4, ty + th, tw, UiStyle.Small, UiRole.Text2, sub, UiFlags.Middle | UiFlags.Truncate);
        }
    }
}
