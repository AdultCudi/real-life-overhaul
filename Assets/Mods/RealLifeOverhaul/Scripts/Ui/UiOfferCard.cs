using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A fact of an offer on its own row: its glyph on a tile, what it is, its figure and a quieter line under the figure.</summary>
    public struct UiOfferFact
    {
        public UiIcon Icon;
        public string Label;       // "Hours"
        public string Value;       // "09:00 to 17:00"
        public string Sub;         // "about $1,520 a week"; null for none
    }

    /// <summary>
    /// What an offer card shows (the game's job offer): who offers it at its left (their emblem, their name and where they
    /// are, the person who asks), the offer at its right (a kicker, its title in capitals, a paragraph, its facts each on a
    /// row) and the two answers with the game's keys.
    /// </summary>
    public sealed class UiOfferModel
    {
        public string Kicker;                              // "Job offer"
        public string Title;                               // "Barista"
        public string Body;                                // what is offered, in a sentence or three; null for none
        public UiIcon Emblem = UiIcon.Briefcase;
        public UiRole EmblemTint = UiRole.Accent;
        public string Side;                                // the employer: "Bloom Café"
        public string SideSub;                             // where: "70 Fifth Avenue, Midtown"; null for none
        public string Face;                                // who offers it: their face; null for none
        public string FaceLine;                            // "Dana Ruiz, the manager"
        public UiOfferFact[] Facts = new UiOfferFact[0];
        public string BackLabel;                           // "Not now"
        public UiGameKey BackKey;                          // the game's Cancel, which the game reads
        public string GoLabel;                             // "Accept the job"
        public UiGameKey GoKey;                            // the game's Interact
        public bool GoOff;                                 // the game's accept button takes no press now
        public UiPrompt[] Prompts = new UiPrompt[0];       // the status bar's, while the keys lead: the game's keys for the answers
        public int PromptCount;
    }

    /// <summary>
    /// The offer card, as the world's mockup draws the job offer (previews/ui/mockups/world/job_offer_card): a slate card
    /// centred over the running game, the employer's emblem on a large tile with their name, their address and the person
    /// who offers it in a column at its left; at its right the kicker, the title in capitals, a paragraph, then each fact on
    /// a darker row of its own (a glyph on its tile, its words, its figure with a quieter line under it), and the two answers
    /// at its foot, each led by the game's key on its cap, Accept on the cyan. The game reads the keys; the mouse presses the
    /// buttons; the status bar under it names the same keys while the keys lead. Commands: <see cref="BackCommand"/> and
    /// <see cref="GoCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiOfferCard
    {
        public const int BackCommand = 1, GoCommand = 2;
        public const float Width = 960f;            // px: the card
        private const float Side = 280f;            // px: the employer's column
        private const float Emblem = 112f;          // px: the employer's emblem tile
        private const float FaceSize = 40f;         // px: the person who offers it
        private const float FactRow = 56f;          // px: a fact, its tile with room round it
        private const float FactTile = UiTokens.IconTile;   // px: on the grid inside its row
        private const int BodyLines = 5, SubLines = 2;

        private static float MainWidth => Width - Side - UiTokens.S6 * 2f;

        /// <summary>The card's height for its model: the taller of its two columns, with its padding.</summary>
        public static float Height(UiOfferModel m) => UiDraw.Snap(Math.Max(SideHeight(m), MainHeight(m))) + UiTokens.S6 * 2f;

        private static float SideHeight(UiOfferModel m)
        {
            float w = Side - UiTokens.S6 * 2f;
            float h = Emblem + UiTokens.S4 + UiTextRules.LineHeight(UiStyle.BodyStrong);
            if (!string.IsNullOrEmpty(m.SideSub))
            {
                h += UiTokens.S1 + UiTextRules.WrappedHeight(m.SideSub, UiStyle.Small, w, SubLines);
            }
            if (!string.IsNullOrEmpty(m.FaceLine))
            {
                h += UiTokens.S5 + FaceSize;
            }
            return h;
        }

        private static float MainHeight(UiOfferModel m)
        {
            float h = UiTokens.PairRow + UiTextRules.LineHeight(UiStyle.AppTitle) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Body))
            {
                h += UiTextRules.WrappedHeight(m.Body, UiStyle.Body, MainWidth, BodyLines) + UiTokens.S4;
            }
            h += m.Facts.Length * (FactRow + UiTokens.S2);
            return h + UiTokens.S4 + UiTokens.Control;
        }

        /// <summary>The card as a screen of its own: centred above the status bar, which it draws with its prompts.</summary>
        public static void Lay(UiScene s, UiOfferModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("offer card", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, UiAt.Screen, m, x, y);
            for (int i = 0; i < m.PromptCount && i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The card alone at a place (its top left); returns its panel.</summary>
        public static int Draw(UiScene s, UiAt at, UiOfferModel m, float x, float y)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            SideColumn(s, on, m, x + UiTokens.S6, y + UiTokens.S6, Side - UiTokens.S6 * 2f);
            float mx = x + Side + UiTokens.S6, mw = MainWidth, my = y + UiTokens.S6;
            UiDraw.TextMiddle(s, on, mx, my, UiTokens.PairRow, mw, UiStyle.SectionHeader, UiRole.AccentText, m.Kicker, UiFlags.Label | UiFlags.Truncate);
            my += UiTokens.PairRow;
            UiDraw.Text(s, on, mx, my, mw, UiStyle.AppTitle, UiRole.Text, m.Title, UiFlags.Truncate);
            my += UiTextRules.LineHeight(UiStyle.AppTitle) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Body))
            {
                float bh = UiTextRules.WrappedHeight(m.Body, UiStyle.Body, mw, BodyLines);
                UiDraw.Text(s, on, mx, my, mw, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Wrap | UiFlags.Middle, BodyLines);
                my += bh + UiTokens.S4;
            }
            for (int i = 0; i < m.Facts.Length; i++)
            {
                Fact(s, on, ref m.Facts[i], mx, UiDraw.Snap(my), mw);
                my += FactRow + UiTokens.S2;
            }
            Answers(s, on, m, mx, y + h - UiTokens.S6 - UiTokens.Control, mw);
            return panel;
        }

        // Who offers it: their emblem on a large tile, their name, where they are, and the person who asks with their face.
        private static void SideColumn(UiScene s, UiAt at, UiOfferModel m, float x, float y, float w)
        {
            float ex = x + UiDraw.SnapDown((w - Emblem) / 2f);
            int tile = UiDraw.Tile(s, at, ex, y, Emblem, m.EmblemTint, false);
            float g = UiDraw.Snap(Emblem * 0.56f);
            UiDraw.Icon(s, at.In(tile), ex + (Emblem - g) / 2f, y + (Emblem - g) / 2f, g, m.Emblem, m.EmblemTint == UiRole.None ? UiRole.Accent : m.EmblemTint, UiFlags.Middle | UiFlags.Centre, true);
            float ty = y + Emblem + UiTokens.S4;
            UiDraw.Text(s, at, x, ty, w, UiStyle.BodyStrong, UiRole.Text, m.Side, UiFlags.Truncate | UiFlags.Centre);
            ty += UiTextRules.LineHeight(UiStyle.BodyStrong);
            if (!string.IsNullOrEmpty(m.SideSub))
            {
                float sh = UiTextRules.WrappedHeight(m.SideSub, UiStyle.Small, w, SubLines);
                UiDraw.Text(s, at, x, ty + UiTokens.S1, w, UiStyle.Small, UiRole.Text2, m.SideSub, UiFlags.Wrap | UiFlags.Centre | UiFlags.Middle, SubLines);
                ty += UiTokens.S1 + sh;
            }
            if (string.IsNullOrEmpty(m.FaceLine))
            {
                return;
            }
            float fy = UiDraw.Snap(ty + UiTokens.S5), lw = Math.Min(w - FaceSize - UiTokens.S2, UiDraw.Snap(UiTextRules.Width(m.FaceLine, UiStyle.Small) + UiTokens.S1));
            float fx = x + UiDraw.SnapDown((w - FaceSize - UiTokens.S2 - lw) / 2f);
            if (!string.IsNullOrEmpty(m.Face))
            {
                UiDraw.Avatar(s, at, fx, fy, FaceSize, m.Face, UiColour.HueOf(m.Face));
            }
            UiDraw.TextMiddle(s, at, fx + FaceSize + UiTokens.S2, fy, FaceSize, lw, UiStyle.Small, UiRole.Text2, m.FaceLine, UiFlags.Truncate);
        }

        // A fact on its darker row: its glyph on a tile, its words, its figure at the right with its quieter line under it.
        private static void Fact(UiScene s, UiAt at, ref UiOfferFact f, float x, float y, float w)
        {
            int row = UiDraw.Box(s, at, x, y, w, FactRow, UiRole.Row, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(row);
            float tx = x + UiTokens.S3, ty = y + (FactRow - FactTile) / 2f;
            int tile = UiDraw.Tile(s, inside, tx, ty, FactTile, UiRole.None, false);
            UiDraw.Icon(s, inside.In(tile), tx + (FactTile - UiTokens.Icon) / 2f, ty + (FactTile - UiTokens.Icon) / 2f, UiTokens.Icon, f.Icon, UiRole.Accent, UiFlags.Middle | UiFlags.Centre, true);
            bool sub = !string.IsNullOrEmpty(f.Sub);
            float vh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = sub ? UiTextRules.LineHeight(UiStyle.SmallStrong) : 0f;
            float vw = UiDraw.Snap(Math.Max(UiTextRules.Width(f.Value, UiStyle.BodyStrong), sub ? UiTextRules.Width(f.Sub, UiStyle.SmallStrong) : 0f) + UiTokens.S1);
            vw = Math.Min(vw, UiDraw.SnapDown(w * 0.5f));
            float vx = x + w - UiTokens.S3 - vw, block = vh + (sub ? UiTokens.S1 / 2f + sh : 0f), vy = y + (FactRow - block) / 2f;
            UiDraw.Text(s, inside, vx, vy, vw, UiStyle.BodyStrong, UiRole.Text, f.Value, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            if (sub)
            {
                UiDraw.Text(s, inside, vx, vy + vh + UiTokens.S1 / 2f, vw, UiStyle.SmallStrong, UiRole.Text2, f.Sub, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            }
            float lx = tx + FactTile + UiTokens.S3;
            UiDraw.TextMiddle(s, inside, lx, y, FactRow, vx - UiTokens.S3 - lx, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
        }

        // The two answers at the card's foot, right-aligned: Not now on the field, Accept on the cyan, each led by the game's key.
        private static void Answers(UiScene s, UiAt at, UiOfferModel m, float x, float y, float w)
        {
            float gw = AnswerWidth(m.GoLabel, m.GoKey), bw = AnswerWidth(m.BackLabel, m.BackKey);
            float gx = x + w - gw, bx = gx - UiTokens.S3 - bw;
            Answer(s, at, bx, y, bw, m.BackLabel, m.BackKey, false, false, BackCommand);
            Answer(s, at, gx, y, gw, m.GoLabel, m.GoKey, true, m.GoOff, GoCommand);
        }

        private static float AnswerWidth(string label, UiGameKey key) =>
            UiDraw.Snap(UiTokens.S5 * 2f + UiTextRules.Width(label, UiStyle.Button) + (key.Shown ? UiDraw.GameKeyWidth(key) + UiTokens.S2 : 0f) + UiTokens.S1);

        private static void Answer(UiScene s, UiAt at, float x, float y, float w, string label, UiGameKey key, bool main, bool off, int command)
        {
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, w, UiTokens.Control, main ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S5, kw = key.Shown ? UiDraw.GameKeyWidth(key) + UiTokens.S2 : 0f;
            UiDraw.TextMiddle(s, inside, cx + kw, y, UiTokens.Control, w - UiTokens.S5 * 2f - kw, UiStyle.Button, main ? UiRole.OnAccent : UiRole.Text, label, UiFlags.Label | UiFlags.Truncate);
            int node = UiDraw.Node(s, at, x, y, w, UiTokens.Control, command, 0, face, off);
            // After the target: the game, not the button, answers the key.
            UiDraw.GameKey(s, inside, cx, y + (UiTokens.Control - UiTokens.KeyCap) / 2f, key, main && !off, off);
            if (off)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }
    }
}
