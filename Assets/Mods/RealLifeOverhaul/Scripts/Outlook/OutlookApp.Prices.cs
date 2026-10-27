using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// Part of the Economy app: the Prices and rates tab, the price index, inflation and the bank rate as charts, what rents, wages and
    /// takings come to, and the wholesale prices by kind of goods.
    /// </summary>
    internal sealed partial class OutlookApp
    {
        private static string Axis0(double v) => v.ToString("0", Inv);
        private static string Axis1(double v) => v.ToString("0.0", Inv);
        private static string Axis2(double v) => v.ToString("0.##", Inv);
        private static string AxisPercent(double v) => v.ToString("0.#", Inv) + "%";
        private static string AxisPercent2(double v) => v.ToString("0.##", Inv) + "%";

        // A card for one figure: its name, its value, the two changes and its line over the days. Returns nothing; the card stands at the box.
        private void ChartCard(float x, float y, float w, float h, string title, int tile, double unit, Func<double, string> axis, string value,
            bool stepped = false, bool zero = false, double offset = 0.0)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            OutlookTile def = OutlookCatalogue.Tiles[tile];
            bool kept = true;
            string text = value ?? TileValue(tile, out kept);
            page.Card(x, y, w, h, p.Card, Radius);
            Caption(page, title, x + Pad, y + Pad - 6f, w - Pad * 2f);
            Value(page, kept ? text : "-", x + Pad, y + Pad + 24f, w * 0.6f, kept ? p.Text : p.Muted, 64f);
            float py = y + Pad + 24f + 64f * 1.25f + 6f;
            bool percent = def.Kind == OutlookChange.Percent;
            ChangePoints points = n => PointsOfTile(tile, n);
            if (kept)
            {
                float used = DeltaPill(page, x + Pad, py, _r.Short[tile], def.Good, points, percent, OutlookCatalogue.ShortDays);
                DeltaPill(page, x + Pad + used + 14f, py, _r.Long[tile], def.Good, points, percent, OutlookCatalogue.LongDays);
            }
            TileWindow(tile, _window);
            float cy = py + 46f + 12f;
            int drawn = OutlookDraw.Chart(page, x + Pad - 10f, cy, w - Pad * 2f + 20f, Mathf.Max(220f, y + h - Pad + 8f - cy), _window, _window.Length, unit, p.Accent, axis, _r.Day, stepped, zero);
            _chartsDrawn++;
            _pointsDrawn += drawn;
        }

        private float Prices(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            if (!_r.CityOn)
            {
                return Notice(y, width, "reallife_econapp_city_off_title", "reallife_econapp_city_off_body");
            }
            int columns = Columns(width, 1040f, 3);
            float cell = CellWidth(width, columns), height = 640f;
            int[] tiles = { OutlookCatalogue.TilePrices, OutlookCatalogue.TileInflation, OutlookCatalogue.TileRate };
            string[] titles = { "reallife_econapp_chart_prices", "reallife_econapp_chart_inflation", "reallife_econapp_chart_rate" };
            for (int i = 0; i < 3; i++)
            {
                float cx = Margin + (i % columns) * (cell + Gap), cyy = y + (i / columns) * (height + Gap);
                switch (tiles[i])
                {
                    case OutlookCatalogue.TilePrices:
                        ChartCard(cx, cyy, cell, height, OutlookText.T(titles[i]), tiles[i], 1000.0, Axis1, null);
                        break;
                    case OutlookCatalogue.TileInflation:
                        ChartCard(cx, cyy, cell, height, OutlookText.T(titles[i]), tiles[i], 100.0, AxisPercent, null);
                        break;
                    default:
                        ChartCard(cx, cyy, cell, height, OutlookText.T(titles[i]), tiles[i], 4.0, AxisPercent2, null, true);
                        break;
                }
            }
            y += ((3 + columns - 1) / columns) * (height + Gap);

            // What rents, wages and takings come to against the game's own figures, and the next decision.
            int second = Columns(width, 1500f, 2);
            float wide = second == 2 ? (width - Gap) * 0.62f : width, narrow = second == 2 ? width - Gap - wide : width;
            float rowHeight = 400f;
            CostsCard(Margin, y, wide, rowHeight);
            BankCard(second == 2 ? Margin + wide + Gap : Margin, second == 2 ? y : y + rowHeight + Gap, narrow, rowHeight);
            y += (second == 2 ? rowHeight : rowHeight * 2f + Gap) + Gap;

            y = WholesaleCard(Margin, y, width) + Gap;
            return y;
        }

        // Rents, wages, takings and what customers spend, each on the game's own figure.
        private void CostsCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_costs_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            page.Text(OutlookText.T("reallife_econapp_costs_note"), x + Pad, y + Pad + 46f, w - Pad * 2f, 70f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            string[] keys = { "reallife_econapp_costs_rents", "reallife_econapp_costs_takings", "reallife_econapp_costs_spend" };
            int[] values = { _r.Costs, _r.Takings, _r.Spend };
            float inner = w - Pad * 2f, part = inner / 3f;
            for (int i = 0; i < 3; i++)
            {
                float px = x + Pad + part * i;
                Caption(page, OutlookText.T(keys[i]), px, y + 160f, part - 20f);
                int ink = values[i] > 1000 ? (i == 0 ? p.Warn : p.Good) : values[i] < 1000 ? (i == 0 ? p.Good : p.Warn) : p.Text;
                string usual = Math.Abs(values[i] - 1000) < Economy.MacroCatalogue.AdjustQuiet
                    ? OutlookText.T("reallife_econapp_costs_usual") : OutlookWords.Factor(values[i]);
                Value(page, usual, px, y + 194f, part - 20f, ink, 60f);
                // A meter from 80% to 120% of the usual, the usual in its middle.
                OutlookMesh meter = page.Picture(px, y + 288f, part - 36f, 22f);
                float fraction = Mathf.Clamp01((values[i] - 800f) / 400f);
                OutlookDraw.Meter(meter, 0f, 4f, part - 36f, 14f, fraction, p.Raised, ink == p.Text ? p.Muted : ink);
                meter.Rect((part - 36f) * 0.5f - 1.5f, 0f, (part - 36f) * 0.5f + 1.5f, 22f, OutlookKit.C32(p.Text, 0.7f));
                meter.End();
            }
            string you = OutlookText.T("reallife_econapp_costs_you", "wages", OutlookText.Money(_r.WagesTomorrow), "rent", OutlookText.Money(_r.RentTomorrow));
            if (_r.Carried > 0)
            {
                you += "   " + OutlookText.T("reallife_macro_page_carried") + " " + OutlookText.Money(_r.Carried);
            }
            page.Text(you, x + Pad, y + h - Pad - 40f, w - Pad * 2f, 40f, SizeSmall, p.Muted, TextAlignmentOptions.MidlineLeft);
        }

        // The central bank: its rate, its last decision and the next.
        private void BankCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            OutlookWords words = OutlookText.Words;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_bank_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            string last = _r.LastMove > 0 ? OutlookText.T("reallife_econapp_bank_raised", "points", OutlookWords.Quarters(_r.LastMove))
                : _r.LastMove < 0 ? OutlookText.T("reallife_econapp_bank_cut", "points", OutlookWords.Quarters(_r.LastMove)) : OutlookText.T("reallife_econapp_bank_held");
            string next = _r.Pending && _r.PendingMove != 0
                ? OutlookText.T(_r.PendingMove > 0 ? "reallife_econapp_bank_next_up" : "reallife_econapp_bank_next_down",
                    "points", OutlookWords.Quarters(_r.PendingMove), "days", _r.DaysToDecision.ToString(Inv))
                : OutlookText.T("reallife_econapp_bank_next", "days", _r.DaysToDecision.ToString(Inv));
            float row = y + Pad + 60f;
            string[] captions = { "reallife_econapp_bank_rate", "reallife_econapp_bank_last", "reallife_econapp_bank_upcoming" };
            string[] values = { OutlookWords.Rate(_r.RateBp) + "  " + Economy.CityText.Rate(_r.Rate), last, next };
            for (int i = 0; i < 3; i++)
            {
                Caption(page, OutlookText.T(captions[i]), x + Pad, row, w - Pad * 2f);
                float vh = page.Fit(values[i], SizeBody, w - Pad * 2f, i == 0);
                page.Text(values[i], x + Pad, row + 34f, w - Pad * 2f, vh + 6f, i == 0 ? 36f : SizeBody, p.Text, TextAlignmentOptions.TopLeft, i == 0, true);
                row += 34f + Mathf.Max(48f, vh) + 22f;
            }
        }

        // The six kinds of goods: today's wholesale price against the usual as bars up or down from the usual line, with a shortage or a glut marked.
        private float WholesaleCard(float x, float y, float w)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            const float height = 660f, low = 0.6f, high = 1.6f;   // the axis runs from 60% to 160% of the usual
            page.Card(x, y, w, height, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_wholesale_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            page.Text(OutlookText.T("reallife_econapp_wholesale_note"), x + Pad, y + Pad + 46f, w - Pad * 2f, 40f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);
            float meshTop = y + 150f, plotH = 300f, inner = w - Pad * 2f, part = inner / OutlookReadings.Kinds, barW = Mathf.Min(120f, part * 0.42f);
            OutlookMesh mesh = page.Picture(x + Pad, meshTop, inner, plotH + 80f);
            float usual = 40f + plotH * (1f - (1f - low) / (high - low));
            mesh.Rect(0f, usual - 1f, inner, usual + 1f, OutlookKit.C32(p.Muted, 0.55f));
            var ends = new float[OutlookReadings.Kinds];
            for (int k = 0; k < OutlookReadings.Kinds; k++)
            {
                OutlookSupply s = _r.Supply[k];
                float factor = Mathf.Clamp(s.Factor / 1000f, low, high);
                float yv = 40f + plotH * (1f - (factor - low) / (high - low));
                if (Mathf.Abs(yv - usual) < 8f)
                {
                    yv = usual - 8f;
                }
                ends[k] = yv;
                int colour = s.Shock == 1 ? p.Bad : s.Shock == 2 ? p.Info : p.Accent;
                float cx = part * k + part * 0.5f;
                Color32 c = OutlookKit.C32(colour);
                if (yv < usual)
                {
                    mesh.Round(cx - barW * 0.5f, yv, cx + barW * 0.5f, usual, 12f, 0f, c);
                }
                else
                {
                    mesh.Round(cx - barW * 0.5f, usual, cx + barW * 0.5f, yv, 0f, 12f, c);
                }
            }
            mesh.End();
            _chartsDrawn++;
            _pointsDrawn += OutlookReadings.Kinds;
            for (int k = 0; k < OutlookReadings.Kinds; k++)
            {
                OutlookSupply s = _r.Supply[k];
                float cx = x + Pad + part * k;
                float labelY = meshTop + (ends[k] < usual ? ends[k] - 44f : ends[k] + 8f);
                page.Text(OutlookWords.Factor(s.Factor), cx, labelY, part, 38f, SizeSmall, p.Text, TextAlignmentOptions.Midline, true);
                float nameY = meshTop + plotH + 100f;
                page.Text(CityTextKind(k), cx + 8f, nameY, part - 16f, 40f, SizeSmall, p.Text, TextAlignmentOptions.Top, true);
                string state = s.Shock == 0 ? string.Empty : OutlookText.T(s.Shock == 1 ? (s.Announced ? "reallife_macro_supply_shortage_tomorrow" : "reallife_macro_supply_shortage_now")
                    : (s.Announced ? "reallife_macro_supply_glut_tomorrow" : "reallife_macro_supply_glut_now"), "percent", Math.Abs(s.Percent).ToString(Inv), "days", s.Days.ToString(Inv));
                if (state.Length > 0)
                {
                    page.Text(state, cx + 8f, nameY + 42f, part - 16f, 96f, SizeMicro, s.Shock == 1 ? p.Bad : p.Info, TextAlignmentOptions.Top, false, true);
                }
                else if (s.Stocked)
                {
                    page.Text(OutlookText.T("reallife_macro_supply_stocked"), cx + 8f, nameY + 42f, part - 16f, 40f, SizeMicro, p.Accent, TextAlignmentOptions.Top);
                }
            }
            return y + height;
        }

        private static string CityTextKind(int kind)
        {
            string name = OutlookText.T(OutlookWords.KindKeys[Mathf.Clamp(kind, 0, OutlookWords.KindKeys.Length - 1)]);
            return string.IsNullOrEmpty(name) ? name : char.ToUpper(name[0], Inv) + name.Substring(1);
        }
    }
}
