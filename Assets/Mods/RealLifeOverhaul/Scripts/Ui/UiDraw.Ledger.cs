using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of a ledger is: an item with its figure, a section's heading, a subtotal, the total, or a gap.</summary>
    public enum UiLedgerKind : byte
    {
        Line = 0,
        Heading,
        Subtotal,
        Total,
        Gap
    }

    /// <summary>A line of a ledger: its kind, its words and its figure as the game wrote them (null for none).</summary>
    public struct UiLedgerLine
    {
        public UiLedgerKind Kind;
        public string Label;
        public string Value;
    }

    /// <summary>
    /// A ledger inside a message, as the tax office's notice carries its return: the words over it, each section's heading
    /// in capitals, its items with their figures on one right edge, a subtotal under a hairline, the total due last in the
    /// strong weight, and the words under it. A table on paper becomes rows a game draws: no dashes of characters, a rule
    /// where the paper had one. Game-free. Cost: a few items a line, a layout a change.
    /// </summary>
    public static partial class UiDraw
    {
        private const float LedgerShare = 0.86f;   // of the thread's width: a table needs its figures' column beside its words
        private const int FootLines = 8;           // lines the words under a ledger take: a closing, an address and a signature

        /// <summary>A ledger's height at a width: a pair row a line, a step over a heading, a gap's step.</summary>
        public static float LedgerHeight(UiLedgerLine[] lines)
        {
            float h = 0f;
            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                h += LedgerLineHeight(lines[i].Kind, i == 0);
            }
            return h;
        }

        private static float LedgerLineHeight(UiLedgerKind kind, bool first) =>
            kind == UiLedgerKind.Gap ? UiTokens.S2 : kind == UiLedgerKind.Heading && !first ? UiTokens.S3 + UiTokens.PairRow : UiTokens.PairRow;

        /// <summary>Lays a ledger out from its top: its lines, the figures at the right edge. Returns its height.</summary>
        public static float Ledger(UiScene s, UiAt at, float x, float y, float w, UiLedgerLine[] lines)
        {
            float ly = y;
            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                ref UiLedgerLine l = ref lines[i];
                float h = LedgerLineHeight(l.Kind, i == 0);
                float row = ly + h - UiTokens.PairRow;
                switch (l.Kind)
                {
                    case UiLedgerKind.Gap:
                        break;
                    case UiLedgerKind.Heading:
                        TextMiddle(s, at, x, row, UiTokens.PairRow, w, UiStyle.SectionHeader, UiRole.Text2, l.Label, UiFlags.Label | UiFlags.Truncate);
                        break;
                    default:
                        bool strong = l.Kind != UiLedgerKind.Line;
                        if (strong)
                        {
                            Box(s, at, x, row, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);   // the paper's rule over a sum
                        }
                        UiStyle figure = l.Kind == UiLedgerKind.Total ? UiStyle.BodyStrong : UiStyle.Body;
                        float vw = string.IsNullOrEmpty(l.Value) ? 0f : Snap(UiTextRules.Width(l.Value, figure) + UiTokens.S1);
                        TextMiddle(s, at, x, row, UiTokens.PairRow, w - vw - (vw > 0f ? UiTokens.S3 : 0f), l.Kind == UiLedgerKind.Line ? UiStyle.Body : UiStyle.BodyStrong,
                            l.Kind == UiLedgerKind.Line ? UiRole.Text2 : UiRole.Text, l.Label, UiFlags.Label | UiFlags.Truncate);
                        if (vw > 0f)
                        {
                            TextMiddle(s, at, x + w - vw, row, UiTokens.PairRow, vw, figure, UiRole.Text, l.Value, UiFlags.Right);
                        }
                        break;
                }
                ly += h;
            }
            return ly - y;
        }

        // A message that carries a ledger: theirs at the left, wider than a line of talk so the figures stand clear of the words.
        private static float TableBubbleHeight(ref UiMessageModel m, float w)
        {
            float inner = Snap(LedgerShare * w) - UiTokens.S4 * 2f;
            float words = string.IsNullOrEmpty(m.Text) ? 0f : Snap(UiTextRules.WrappedHeight(m.Text, UiStyle.Body, inner, 12)) + UiTokens.S3;
            float foot = string.IsNullOrEmpty(m.TableFoot) ? 0f : UiTokens.S3 + Snap(UiTextRules.WrappedHeight(m.TableFoot, UiStyle.Small, inner, FootLines));
            float timeH = string.IsNullOrEmpty(m.Time) ? 0f : UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small);
            return Snap(UiTokens.S3 * 2f + words + LedgerHeight(m.Table) + foot + timeH);
        }

        private static float TableBubble(UiScene s, UiAt at, float x, float y, float w, ref UiMessageModel m)
        {
            float bw = Snap(LedgerShare * w), bh = TableBubbleHeight(ref m, w);
            int face = Box(s, at, x, y, bw, bh, UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
            UiAt inside = at.In(face);
            float ix = x + UiTokens.S4, iw = bw - UiTokens.S4 * 2f, iy = y + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Text))
            {
                int words = Text(s, inside, ix, iy, iw, UiStyle.Body, UiRole.Text, m.Text, UiFlags.Wrap | UiFlags.Truncate, 12);
                iy = Snap(s.Items[words].Y + s.Items[words].H) + UiTokens.S3;
            }
            iy += Ledger(s, inside, ix, iy, iw, m.Table);
            if (!string.IsNullOrEmpty(m.TableFoot))
            {
                int foot = Text(s, inside, ix, iy + UiTokens.S3, iw, UiStyle.Small, UiRole.Text2, m.TableFoot, UiFlags.Wrap | UiFlags.Truncate, FootLines);
                iy = s.Items[foot].Y + s.Items[foot].H;
            }
            if (!string.IsNullOrEmpty(m.Time))
            {
                int t = Text(s, inside, ix, iy + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, m.Time, UiFlags.Middle);
                s.Items[t].Alpha = 1f;
            }
            return bh;
        }
    }
}
