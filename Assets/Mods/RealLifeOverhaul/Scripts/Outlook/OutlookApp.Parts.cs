using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>Part of the Economy app: the parts every tab is made of, a caption, a change as a pill, a headline tile, a card for a chart, a piece of advice.</summary>
    internal sealed partial class OutlookApp
    {
        private const float SizeBody = 30f, SizeSmall = 26f, SizeMicro = 24f, SizeCard = 38f, SizeValue = 76f, Pad = 36f, Radius = 40f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private int _chartsDrawn, _pointsDrawn;

        // A small heading in capitals, quiet.
        private void Caption(OutlookPage page, string text, float x, float y, float w)
        {
            page.Text(text, x, y, w, 32f, SizeMicro, page.Palette.Muted, TextAlignmentOptions.TopLeft, true, false, true, 3f);
        }

        // A heading for a card.
        private void Heading(OutlookPage page, string text, float x, float y, float w)
        {
            page.Text(text, x, y, w, 50f, SizeCard, page.Palette.Text, TextAlignmentOptions.TopLeft, true);
        }

        // A card's number: large, and the text beside it.
        private float Value(OutlookPage page, string text, float x, float y, float w, int colour, float size = SizeValue)
        {
            page.Text(text, x, y, w, size * 1.25f, size, colour, TextAlignmentOptions.TopLeft, true);
            return size * 1.25f;
        }

        // ------------------------------------------------------------------ a change as a pill

        // What a change shows as a pill: its words, its colours, and whether it has an arrow (up or down).
        private void DeltaLook(OutlookPage page, OutlookDelta change, int good, ChangePoints points, bool percent, int days, out string text, out int arrow, out int ink, out int fill)
        {
            OutlookPalette p = page.Palette;
            int v = change.Value;
            if (change.Known && percent && Math.Abs(v) < 5)
            {
                v = 0;   // under 0.05% is no change
            }
            OutlookDelta shown = change.Known ? OutlookDelta.Of(v) : change;
            Tint(p, OutlookRules.OutcomeOf(shown, good), out ink, out fill);
            string size = !shown.Known ? "-" : percent ? OutlookWords.Percent(Math.Abs(v)) : points(Math.Abs(v));
            text = size + "  " + OutlookText.Days(days);
            arrow = shown.Known && v != 0 ? (v > 0 ? 1 : -1) : 0;
        }

        /// <summary>How wide a change's pill is.</summary>
        private float DeltaWidth(OutlookPage page, OutlookDelta change, int good, ChangePoints points, bool percent, int days)
        {
            DeltaLook(page, change, good, points, percent, days, out string text, out int arrow, out _, out _);
            return page.Measure(text, SizeSmall, true) + (arrow != 0 ? 54f : 20f) + 22f;
        }

        // A change over some days as a pill: an arrow, its size and the days. Returns the pill's width.
        private float DeltaPill(OutlookPage page, float x, float y, OutlookDelta change, int good, ChangePoints points, bool percent, int days)
        {
            DeltaLook(page, change, good, points, percent, days, out string text, out int arrow, out int ink, out int fill);
            float textWidth = page.Measure(text, SizeSmall, true);
            float lead = arrow != 0 ? 54f : 20f;
            float w = textWidth + lead + 22f;
            page.Pill(x, y, w, 46f, fill);
            if (arrow != 0)
            {
                OutlookMesh mesh = page.Picture(x + 18f, y + 12f, 24f, 24f);
                OutlookDraw.Arrow(mesh, 2f, 3f, 18f, arrow > 0, ink);
                mesh.End();
            }
            page.Text(text, x + lead, y, textWidth + 8f, 46f, SizeSmall, ink, TextAlignmentOptions.MidlineLeft, true);
            return w;
        }

        private delegate string ChangePoints(int absolute);

        private static string PointsOfTile(int tile, int absolute)
        {
            double n;
            string format = "0.0";
            switch (tile)
            {
                case OutlookCatalogue.TileInflation: n = absolute / 100.0; format = "0.0#"; break;
                case OutlookCatalogue.TileRate: n = absolute / (double)Economy.MacroCatalogue.QuartersPerPoint; format = "0.##"; break;
                case OutlookCatalogue.TileUnemployment: n = absolute / 10.0; break;
                case OutlookCatalogue.TileIncome: n = absolute / 10.0; break;
                default: n = absolute; format = "0"; break;
            }
            return OutlookText.Pts(n.ToString(format, Inv));
        }

        // ------------------------------------------------------------------ the history of a figure

        // The last 120 days of a tile's figure into the shared window, and what one unit of it is.
        private double TileWindow(int tile, int[] into)
        {
            OutlookTile def = OutlookCatalogue.Tiles[tile];
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = OutlookCatalogue.NoValue;
            }
            if (tile == OutlookCatalogue.TileStocks)
            {
                OutlookStudy.StocksWindow(_r, into);
                return 100.0;
            }
            if (def.Series != null)
            {
                _director.Book.Window(def.Series, _r.Day, into.Length, into);
            }
            return Unit(def.Series);
        }

        // One unit of a figure as it is plotted: the index in points, inflation in per cent, and so on.
        private static double Unit(string series)
        {
            switch (series)
            {
                case OutlookCatalogue.Prices: return 1000.0;
                case OutlookCatalogue.Inflation: return 100.0;
                case OutlookCatalogue.Rate: return 4.0;
                case OutlookCatalogue.Unemployment: return 10.0;
                case OutlookCatalogue.Income: return 10.0;
                case OutlookCatalogue.Property: return 10.0;
                case OutlookCatalogue.Reserve: return 1.0;
                default: return 1.0;
            }
        }

        // The figure of a tile as the tile writes it, and whether the mod keeps it.
        private string TileValue(int tile, out bool kept)
        {
            kept = true;
            OutlookWords w = OutlookText.Words;
            switch (tile)
            {
                case OutlookCatalogue.TilePrices: kept = _r.CityOn; return OutlookWords.Index(_r.Prices);
                case OutlookCatalogue.TileInflation: kept = _r.CityOn; return OutlookWords.Hundredths(_r.Inflation);
                case OutlookCatalogue.TileRate: kept = _r.CityOn; return OutlookWords.Rate(_r.RateBp);
                case OutlookCatalogue.TileUnemployment: kept = _r.JobsOn; return OutlookWords.Permille(_r.Unemployment);
                case OutlookCatalogue.TileIncome: kept = _r.CityOn; return OutlookWords.Tenths(_r.Income);
                case OutlookCatalogue.TileProperty: kept = _r.CityOn; return OutlookWords.Factor(_r.Property);
                case OutlookCatalogue.TileStocks: kept = _r.MarketOn; return (_r.Stocks / 100.0).ToString("N2", CultureInfo.CurrentCulture);
                default: kept = _r.CouncilOn; return OutlookText.Money(_r.Reserve);
            }
        }

        // ------------------------------------------------------------------ the headline tile

        // One of the Overview's tiles: its name, its figure, how it moved over a week and a month, and its line.
        private void Tile(float x, float y, float w, float h, int tile)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            OutlookTile def = OutlookCatalogue.Tiles[tile];
            string value = TileValue(tile, out bool kept);
            page.Card(x, y, w, h, p.Card, Radius);
            Caption(page, OutlookText.T(def.LabelKey), x + Pad, y + Pad - 6f, w - Pad * 2f);
            Value(page, kept ? value : "-", x + Pad, y + Pad + 28f, w - Pad * 2f, kept ? p.Text : p.Muted);
            float py = y + Pad + 28f + SizeValue * 1.25f + 8f;
            bool percent = def.Kind == OutlookChange.Percent;
            ChangePoints points = n => PointsOfTile(tile, n);
            if (kept)
            {
                float used = DeltaPill(page, x + Pad, py, _r.Short[tile], def.Good, points, percent, OutlookCatalogue.ShortDays);
                DeltaPill(page, x + Pad + used + 14f, py, _r.Long[tile], def.Good, points, percent, OutlookCatalogue.LongDays);
            }
            else
            {
                page.Text(OutlookText.T("reallife_econapp_not_kept"), x + Pad, py, w - Pad * 2f, 46f, SizeSmall, p.Muted, TextAlignmentOptions.MidlineLeft);
            }
            double unit = TileWindow(tile, _window);
            float sy = py + 46f + 10f;
            int drawn = OutlookDraw.Spark(page, x + Pad - 8f, sy, w - Pad * 2f + 16f, Mathf.Max(60f, y + h - Pad + 6f - sy), _window, _window.Length, unit, p.Accent);
            _chartsDrawn++;
            _pointsDrawn += drawn;
            int target = def.Tab;
            page.Click(x, y, w, h, () => PickTab(target));
        }

        // ------------------------------------------------------------------ advice

        private OutlookTone ToneOfInsight(OutlookInsightKind kind)
        {
            return kind == OutlookInsightKind.Opportunity ? OutlookTone.Good : kind == OutlookInsightKind.Warning ? OutlookTone.Bad : OutlookTone.Neutral;
        }

        // A piece of advice as a card: a stripe and a mark in the colour of its kind, what has happened, and what it means. Returns its height.
        private float InsightCard(float x, float y, float w, in OutlookInsight insight, int rank)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            OutlookWords words = OutlookText.Words;
            Tint(p, ToneOfInsight(insight.Kind), out int ink, out int fill);
            string head = words.Head(insight), mean = words.Mean(insight);
            float textX = x + 150f, textW = w - 150f - Pad - 40f;
            float headH = page.Fit(head, 34f, textW, true), meanH = page.Fit(mean, SizeBody, textW);
            float tagH = 44f;
            float h = Mathf.Max(170f, Pad + headH + 12f + meanH + 20f + tagH + Pad - 8f);
            page.Card(x, y, w, h, p.Card, Radius);
            // The stripe down the left edge, rounded with the card.
            OutlookMesh stripe = page.Picture(x, y, 14f, h);
            stripe.Round(0f, 26f, 8f, h - 26f, 4f, 4f, OutlookKit.C32(ink));
            stripe.End();
            // The mark: a round badge with the kind's sign.
            page.Pill(x + 40f, y + Pad, 72f, 72f, fill);
            OutlookMesh sign = page.Picture(x + 40f, y + Pad, 72f, 72f);
            Sign(sign, insight.Kind, 36f, 36f, ink, fill);
            sign.End();
            page.Text(head, textX, y + Pad - 4f, textW, headH + 6f, 34f, p.Text, TextAlignmentOptions.TopLeft, true, true);
            page.Text(mean, textX, y + Pad - 4f + headH + 12f, textW, meanH + 6f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            // Where it leads: a pill with the tab's name.
            string tab = OutlookText.T(OutlookTab.Keys[OutlookTab.Clamp(insight.Tab)]);
            float tw = page.Measure(tab, SizeSmall, true) + 52f;
            float ty = y + h - Pad - tagH + 8f;
            page.Pill(textX, ty, tw, tagH, p.Raised);
            page.Text(tab, textX, ty, tw, tagH, SizeSmall, p.Muted, TextAlignmentOptions.Midline, true);
            string kind = OutlookText.T(KindKeys[(int)insight.Kind]);
            float kw = page.Measure(kind, SizeSmall, true) + 40f;
            page.Pill(textX + tw + 14f, ty, kw, tagH, fill);
            page.Text(kind, textX + tw + 14f, ty, kw, tagH, SizeSmall, ink, TextAlignmentOptions.Midline, true);
            int target = insight.Tab;
            if (target != _tab)
            {
                page.Click(x, y, w, h, () => PickTab(target));
            }
            return h;
        }

        private static readonly string[] KindKeys = { "reallife_econapp_kind_opportunity", "reallife_econapp_kind_warning", "reallife_econapp_kind_info" };

        // The sign on a piece of advice: an arrow up for a chance, an exclamation mark in a triangle for a risk, an i for news.
        private static void Sign(OutlookMesh mesh, OutlookInsightKind kind, float cx, float cy, int ink, int fill)
        {
            Color32 c = OutlookKit.C32(ink), cut = OutlookKit.C32(fill);
            switch (kind)
            {
                case OutlookInsightKind.Opportunity:
                    mesh.Triangle(cx, cy - 17f, cx - 18f, cy + 2f, cx + 18f, cy + 2f, c);
                    mesh.Round(cx - 6.5f, cy, cx + 6.5f, cy + 17f, 2.5f, 2.5f, c);
                    break;
                case OutlookInsightKind.Warning:
                    mesh.Triangle(cx, cy - 20f, cx - 21f, cy + 16f, cx + 21f, cy + 16f, c);
                    mesh.Round(cx - 2.8f, cy - 8f, cx + 2.8f, cy + 4f, 2.6f, 2.6f, cut);
                    mesh.Disc(cx, cy + 10f, 3.1f, cut);
                    break;
                default:
                    mesh.Disc(cx, cy - 12f, 4.6f, c);
                    mesh.Round(cx - 4.2f, cy - 3f, cx + 4.2f, cy + 17f, 3f, 3f, c);
                    break;
            }
        }
    }
}
