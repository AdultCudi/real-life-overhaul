using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a call shows: who is on the line, how long it has run, what they say now, the replies and the way to hang up.</summary>
    public sealed class UiCallModel
    {
        public string Name;
        public string Face;                         // the key the face is drawn by (two of one name); null: the name
        public float Hue;
        public UiRole Ring = UiRole.None;           // the mood the game shows by the line; None where it shows none
        public UiIcon Icon = UiIcon.None;           // a firm or a service: its picture on a tile in place of a face
        public UiRole Tint = UiRole.None;
        public string Picture;                      // the firm's own picture (its logo, a UiPictures key) on the tile in place of the icon; null for the icon
        public string Role;                         // over the name: "Customer Service, Bloom Cafe"
        public string Length;                       // the call's length, "1:24"
        public string Line;                         // what they say now
        public UiIcon Mood = UiIcon.None;           // the mood the game shows by the line, as a small glyph before it; None where it shows none
        public bool Waiting;                        // their next line is coming: the typing dots under this one
        public UiDecisionChoice[] Choices = new UiDecisionChoice[0];
        public int Selected = -1;
        public UiActionModel[] Buttons = new UiActionModel[0];   // the game's way out of the line (Hang up, Decline), the danger kind
        public UiFormRow[] Rows = new UiFormRow[0];               // a form the line asks for (a campaign, a delivery), under the line (UiFormRows)
        public int RowCount;
        public UiFormPick Pick = new UiFormPick();
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// A phone call as a moment, the decision card's call variant (DIRECTION_CS2, section 4.5), drawn as the decision card
    /// is: centred over the dimmed world, the caller's face in the ring of their mood (or a firm's picture on its tile) on
    /// the card's band at the left with the call's length under it behind the phone; what they do in capitals and their
    /// name as the title; what they say now as the narrative; the replies numbered with their consequences where the game
    /// gives one, each on its number's key; and the way out of the line as the danger button centred at the foot. The
    /// game's conversations on the phone draw the same way. Commands: <see cref="ChoiceCommand"/> (a reply's index) and
    /// <see cref="ButtonCommand"/> (a button's index). Game-free. Cost: a layout a change, and one a second while the
    /// length shows.
    /// </summary>
    public static class UiCallCard
    {
        public const int ChoiceCommand = 170, ButtonCommand = 171;   // its own, so a press meant for a call never lands on the page under it
        private const float Side = 296f;      // px: the face's column, as the decision card's
        private const float Face = 144f;      // px: the caller's face, a step under the card's emblem with its ring
        private const float Dots = 40f;       // px: the typing dots' bubble

        public static void Lay(UiScene s, UiCallModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("call", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float width = UiDecision.Width, inner = width - Side - UiTokens.S8 * 2f;
            float h = Height(m, inner);
            float x = UiDraw.SnapDown((frame.Width - width) / 2f), y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            int card = UiDraw.Box(s, UiAt.Screen, x, y, width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            UiDraw.Box(s, at, x, y, Side, h, UiRole.Band, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor);
            Caller(s, at, m, x, y, h);
            float cx = x + Side + UiTokens.S8, cy = y + UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Role))
            {
                UiDraw.Text(s, at, cx, cy, inner, UiStyle.SectionHeader, UiRole.Text2, m.Role, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            UiDraw.Text(s, at, cx, cy, inner, UiStyle.AppTitle, UiRole.AccentText, m.Name, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle)) + UiTokens.S4;
            if (!string.IsNullOrEmpty(m.Line))
            {
                float lead = MoodLead(m);
                float lh = UiTextRules.WrappedHeight(m.Line, UiStyle.Body, inner - lead, 8);
                if (lead > 0f)
                {
                    UiDraw.Icon(s, at, cx, cy + (UiTextRules.LineHeight(UiStyle.Body) - UiTokens.Icon) / 2f, UiTokens.Icon, m.Mood, UiRole.None, UiFlags.Middle, true);
                }
                UiDraw.Text(s, at, cx + lead, cy, inner - lead, UiStyle.Body, UiRole.Text, m.Line, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 8);
                cy += UiDraw.Snap(lh) + UiTokens.S5;
            }
            if (m.Waiting)
            {
                int bubble = UiDraw.Box(s, at, cx, cy, UiTokens.S16 + UiTokens.S4, Dots, UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge);
                var dots = new UiItem
                {
                    Kind = UiKind.Busy, Fill = UiRole.AccentText, X = cx + UiTokens.S4, Y = cy + UiTokens.S4 + UiTokens.S1, W = UiTokens.S12, H = UiTokens.S2,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = bubble, Alpha = 1f
                };
                s.Add(ref dots);
                cy += Dots + UiTokens.S5;
            }
            if (m.RowCount > 0)
            {
                float fh = UiFormRows.Height(m.Rows, m.RowCount, inner, m.Pick);
                UiFormRows.Lay(s, at, m.Rows, m.RowCount, m.Pick, cx, cy, inner);
                cy += fh + UiTokens.S5;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                cy += UiConversation.ChoiceRow(s, at, ref m.Choices[i], i, i == m.Selected, cx, cy, inner, ChoiceCommand) + UiTokens.S2;
            }
            if (m.Buttons.Length > 0)
            {
                cy += UiTokens.S4;
                float total = 0f;
                for (int i = 0; i < m.Buttons.Length; i++)
                {
                    total += UiDraw.ButtonWidth(m.Buttons[i].Kind, m.Buttons[i].Label, m.Buttons[i].Icon, m.Buttons[i].Key, keys) + (i > 0 ? UiTokens.S3 : 0f);
                }
                float bx = cx + UiDraw.SnapDown((inner - total) / 2f);
                for (int i = 0; i < m.Buttons.Length; i++)
                {
                    ref UiActionModel b = ref m.Buttons[i];
                    float bw = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                    UiDraw.Button(s, at, bx, cy, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, b.Key, keys, ButtonCommand, i, b.Disabled);
                    bx += bw + UiTokens.S3;
                }
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            Bar(s, frame, keys);
        }

        // The status bar under a call: its apps and All apps wait until the call ends, as opening one would leave it, so the
        // keys and the pad stay on the card and the bar offers no way to them.
        private static void Bar(UiScene s, UiFrame frame, IUiKeys keys)
        {
            Func<UiStatusModel> before = UiChrome.Bar;
            UiStatusModel bar = before != null ? before() : null;
            bool press = bar != null && bar.AppsPress;
            if (bar != null)
            {
                bar.AppsPress = false;
                UiChrome.Bar = () => bar;
            }
            try
            {
                UiDraw.PromptBar(s, frame, keys);
            }
            finally
            {
                UiChrome.Bar = before;
                if (bar != null)
                {
                    bar.AppsPress = press;
                }
            }
        }

        /// <summary>The card's height at its content's width: what they do, the name, the line, the replies, the buttons; never shorter than the caller's side.</summary>
        public static float Height(UiCallModel m, float inner)
        {
            float h = UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Role))
            {
                h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle)) + UiTokens.S4;
            if (!string.IsNullOrEmpty(m.Line))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(m.Line, UiStyle.Body, inner - MoodLead(m), 8)) + UiTokens.S5;
            }
            h += m.Waiting ? Dots + UiTokens.S5 : 0f;
            h += m.RowCount > 0 ? UiFormRows.Height(m.Rows, m.RowCount, inner, m.Pick) + UiTokens.S5 : 0f;
            for (int i = 0; i < m.Choices.Length; i++)
            {
                h += UiConversation.ChoiceHeight(ref m.Choices[i]) + UiTokens.S2;
            }
            h += (m.Buttons.Length > 0 ? UiTokens.S4 + UiTokens.Control : 0f) + UiTokens.S8;
            return Math.Max(UiDraw.Snap(h), SideHeight(m) + UiTokens.S12 * 2f);
        }

        // The room the mood's glyph takes before the line, when the game shows one.
        private static float MoodLead(UiCallModel m) => m.Mood != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f;

        private static float SideHeight(UiCallModel m) =>
            Face + UiTokens.S2 + (string.IsNullOrEmpty(m.Length) ? 0f : UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)));

        // The caller on the card's band: their face in the ring of their mood or the firm's tile, the call's length under it
        // behind the phone, held in the middle of the card's height.
        private static void Caller(UiScene s, UiAt at, UiCallModel m, float x, float y, float h)
        {
            float top = y + UiDraw.SnapDown((h - SideHeight(m)) / 2f);
            float fx = x + (Side - Face) / 2f, fy = top + UiTokens.S1;
            if (!string.IsNullOrEmpty(m.Picture))
            {
                UiDraw.PictureFace(s, at, fx, fy, Face, m.Picture);
            }
            else if (m.Icon != UiIcon.None)
            {
                int tile = UiDraw.Tile(s, at, fx, fy, Face, m.Tint, false);
                float g = UiDraw.Snap(Face * 0.5f);
                UiDraw.Icon(s, at.In(tile), fx + (Face - g) / 2f, fy + (Face - g) / 2f, g, m.Icon, m.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                UiDraw.Portrait(s, at, fx, fy, Face, m.Face ?? m.Name, m.Hue, m.Ring);
            }
            if (!string.IsNullOrEmpty(m.Length))
            {
                float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
                float lw = UiDraw.Snap(UiTextRules.Width(m.Length, UiStyle.Title) + UiTokens.S1);
                float block = UiTokens.Icon + UiTokens.S2 + lw, bx = x + UiDraw.SnapDown((Side - block) / 2f), ly = fy + Face + UiTokens.S1 + UiTokens.S5;
                UiDraw.Icon(s, at, bx, ly + (lh - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Phone, UiRole.None, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, bx + UiTokens.Icon + UiTokens.S2, ly, lh, lw, UiStyle.Title, UiRole.Text, m.Length, UiFlags.None);
            }
        }
    }
}
