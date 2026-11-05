using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class EstateFace
    {
        private const byte RegionOffer = 13;

        // An offer waiting for an answer, as one band: its tile, what it is and the line under it, the figures that decide it, and the answers at the right (the main one
        // last). Returns the y under it, or the y given where nothing is offered.
        private static float OfferBand(UiScene s, UiAt at, EstateOffer o, float x, float y, float w, IUiKeys keys)
        {
            if (o == null)
            {
                return y;
            }
            UiAt on = at.InRegion(RegionOffer);
            float pad = UiTokens.S4, h = TileBig + pad * 2f;
            int band = UiDraw.Box(s, on, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = on.In(band);
            int tile = UiDraw.Tile(s, inside, x + pad, y + pad, TileBig, UiRole.None, false);
            UiDraw.Icon(s, inside.In(tile), x + pad + (TileBig - UiTokens.S8) / 2f, y + pad + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, o.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            // The answers at the right edge, the main one last.
            float right = x + w - pad;
            for (int i = o.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref o.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, inside, right - bw, y + (h - UiTokens.Control) / 2f, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= bw + UiTokens.S3;
            }
            // The figures between the words and the answers, each a small label over its figure, sharing what room the words leave.
            int n = Math.Min(o.FigLabels.Length, o.FigValues.Length);
            float tx = x + pad + TileBig + UiTokens.S4, wordsW = UiDraw.SnapDown((right - tx) * 0.34f), fx = tx + wordsW + UiTokens.S4;
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), valueH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            string chipText = string.IsNullOrEmpty(o.Chip) ? o.Chip : char.ToUpperInvariant(o.Chip[0]) + o.Chip.Substring(1);
            float chip = string.IsNullOrEmpty(o.Chip) ? 0f : Math.Min(UiDraw.SnapDown(wordsW * 0.6f), UiFactCard.ChipWidth(o.Chip));
            float titleH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), subH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((h - titleH - subH) / 2f);
            UiDraw.Text(s, inside, tx, top, wordsW, UiStyle.Title, UiRole.Text, o.Title, UiFlags.Truncate | UiFlags.Middle);
            if (chip > 0f)
            {
                UiDraw.Chip(s, inside, tx, top + titleH + UiTokens.S1, chipText, o.ChipTone, chip);
                UiDraw.Text(s, inside, tx + chip + UiTokens.S2, top + titleH, wordsW - chip - UiTokens.S2, UiStyle.Small, UiRole.Text2, o.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            else
            {
                UiDraw.Text(s, inside, tx, top + titleH, wordsW, UiStyle.Small, UiRole.Text2, o.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            if (n > 0)
            {
                // Each figure takes the width of its words, the rest of the room shared out, and cuts its label short only when the language leaves no more.
                float room = right - fx, fy = y + UiDraw.SnapDown((h - labelH - UiTokens.S1 - valueH) / 2f), sum = 0f;
                var wide = new float[n];
                for (int i = 0; i < n; i++)
                {
                    wide[i] = UiDraw.Snap(Math.Max(UiTextRules.Width(o.FigLabels[i], UiStyle.SectionHeader) * 1.2f, UiTextRules.Width(o.FigValues[i], UiStyle.Title)) + UiTokens.S3);
                    sum += wide[i];
                }
                float scale = sum > room ? room / sum : 1f, gap = n > 1 && sum < room ? UiDraw.SnapDown((room - sum) / n) : 0f, cx = UiDraw.Snap(fx);
                for (int i = 0; i < n; i++)
                {
                    float fw = UiDraw.SnapDown(wide[i] * scale);
                    UiDraw.Text(s, inside, cx, fy, fw - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text2, o.FigLabels[i], UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, inside, cx, fy + labelH + UiTokens.S1, fw - UiTokens.S2, UiStyle.Title, UiRole.Text, o.FigValues[i], UiFlags.Truncate | UiFlags.Middle);
                    cx += fw + gap;
                }
            }
            return y + h + UiTokens.S3;
        }
    }
}
