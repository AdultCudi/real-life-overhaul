using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>Part of the Economy app: the City finances tab, the council's reserve and weekly books, its four departments, and the tax.</summary>
    internal sealed partial class OutlookApp
    {
        private static string AxisMoney(double thousands) => OutlookText.Money((long)Math.Round(thousands * 1000.0));

        private float Finances(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            if (!_r.CouncilOn)
            {
                y = Notice(y, width, "reallife_econapp_council_off_title", "reallife_econapp_council_off_body");
            }
            else
            {
                int split = Columns(width, 1500f, 2);
                float wide = split == 2 ? (width - Gap) * 0.64f : width, narrow = split == 2 ? width - Gap - wide : width;
                float height = 700f;
                ChartCard(Margin, y, wide, height, OutlookText.T("reallife_econapp_chart_reserve"), OutlookCatalogue.TileReserve, 1.0, AxisMoney, null, false, true);
                BooksCard(split == 2 ? Margin + wide + Gap : Margin, split == 2 ? y : y + height + Gap, narrow, height);
                y += (split == 2 ? height : height * 2f + Gap) + Gap;
                int second = Columns(width, 1500f, 2);
                float half = second == 2 ? (width - Gap) / 2f : width, rowHeight = 560f;
                DepartmentsCard(Margin, y, half, rowHeight);
                TaxCard(second == 2 ? Margin + half + Gap : Margin, second == 2 ? y : y + rowHeight + Gap, half, rowHeight);
                y += (second == 2 ? rowHeight : rowHeight * 2f + Gap) + Gap;
                return y;
            }
            TaxCard(Margin, y, Mathf.Min(width, 1500f), 560f);
            return y + 560f + Gap;
        }

        // A week of the city's books: what it takes in, what it spends and what is left.
        private void BooksCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_books_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            page.Text(OutlookText.T("reallife_econapp_books_note"), x + Pad, y + Pad + 46f, w - Pad * 2f, 40f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);
            long most = Math.Max(1L, Math.Max(_r.Revenue, _r.Spending));
            string[] captions = { "reallife_econapp_books_revenue", "reallife_econapp_books_spending", "reallife_econapp_books_balance" };
            long balance = (long)_r.Revenue - _r.Spending;
            long[] values = { _r.Revenue, _r.Spending, balance };
            int[] inks = { p.Good, p.Warn, balance >= 0 ? p.Good : p.Bad };
            float row = y + Pad + 120f;
            for (int i = 0; i < 3; i++)
            {
                Caption(page, OutlookText.T(captions[i]), x + Pad, row, w - Pad * 2f);
                page.Text(OutlookText.Money(values[i]), x + Pad, row + 32f, w - Pad * 2f, 60f, 48f, i == 2 ? inks[i] : p.Text, TextAlignmentOptions.TopLeft, true);
                if (i < 2)
                {
                    OutlookMesh meter = page.Picture(x + Pad, row + 100f, w - Pad * 2f, 22f);
                    OutlookDraw.Meter(meter, 0f, 3f, w - Pad * 2f, 16f, Mathf.Clamp01(values[i] / (float)most), p.Raised, inks[i]);
                    meter.End();
                    _chartsDrawn++;
                    _pointsDrawn++;
                }
                row += 164f;
            }
        }

        // The four departments, each from two steps down to two up.
        private void DepartmentsCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_departments_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            page.Text(OutlookText.T("reallife_econapp_departments_note"), x + Pad, y + Pad + 46f, w - Pad * 2f, 70f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            string[] keys = { "reallife_econapp_dept_safety", "reallife_econapp_dept_roads", "reallife_econapp_dept_services", "reallife_econapp_dept_living" };
            float row = y + Pad + 130f, nameW = Mathf.Min(420f, w * 0.34f), steps = w - Pad * 2f - nameW;
            OutlookMesh mesh = page.Picture(x + Pad + nameW, row, steps, 4 * 94f);
            for (int d = 0; d < 4; d++)
            {
                int level = Mathf.Clamp(_r.Departments[d], -2, 2);
                float cy = d * 94f + 38f;
                mesh.Round(0f, cy - 4f, steps - 8f, cy + 4f, 4f, 4f, OutlookKit.C32(p.Raised));
                for (int s = -2; s <= 2; s++)
                {
                    float cx = 14f + (steps - 36f) * (s + 2) / 4f;
                    bool on = s == level;
                    mesh.Disc(cx, cy, on ? 15f : 7f, OutlookKit.C32(on ? (s == 0 ? p.Info : s > 0 ? p.Good : p.Warn) : p.Muted, on ? 1f : 0.55f));
                }
            }
            mesh.End();
            _chartsDrawn++;
            _pointsDrawn += 4;
            for (int d = 0; d < 4; d++)
            {
                int level = Mathf.Clamp(_r.Departments[d], -2, 2);
                page.Text(OutlookText.T(keys[d]), x + Pad, row + d * 94f, nameW - 12f, 76f, SizeBody, p.Text, TextAlignmentOptions.MidlineLeft, true);
                page.Text(level > 0 ? "+" + level.ToString(Inv) : level.ToString(Inv), x + Pad + nameW, row + d * 94f + 50f, steps, 30f, SizeMicro, p.Muted, TextAlignmentOptions.TopRight);
            }
        }

        // The tax: the rate the game charges and the bill that is owing.
        private void TaxCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_tax_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            Caption(page, OutlookText.T("reallife_econapp_tax_rate"), x + Pad, y + Pad + 70f, w - Pad * 2f);
            Value(page, _r.TaxPercent.ToString(Inv) + "%", x + Pad, y + Pad + 102f, w - Pad * 2f, p.Text, 64f);
            Caption(page, OutlookText.T("reallife_econapp_tax_bill"), x + Pad, y + Pad + 210f, w - Pad * 2f);
            string bill = _r.TaxDue > 0 ? OutlookText.Money(_r.TaxDue) : OutlookText.T("reallife_econapp_tax_nothing");
            page.Text(bill, x + Pad, y + Pad + 242f, w - Pad * 2f, 60f, 48f, _r.TaxDue > 0 ? p.Warn : p.Text, TextAlignmentOptions.TopLeft, true);
            if (_r.TaxDue > 0)
            {
                page.Text(OutlookText.T("reallife_econapp_tax_due", "day", _r.TaxDay.ToString(Inv)), x + Pad, y + Pad + 306f, w - Pad * 2f, 40f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft);
            }
            page.Text(OutlookText.T("reallife_econapp_tax_note"), x + Pad, y + h - Pad - 90f, w - Pad * 2f, 90f, SizeMicro, p.Muted, TextAlignmentOptions.BottomLeft, false, true);
        }

        // ------------------------------------------------------------------ the Insights tab

        private float Insights(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            string note = OutlookText.T("reallife_econapp_insights_note");
            page.Text(note, Margin, y, width, 90f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            y += Mathf.Max(48f, page.Fit(note, SizeBody, width)) + 28f;
            if (!_r.CityOn || _advice.Count == 0)
            {
                return Notice(y, width, _r.CityOn ? "reallife_econapp_quiet_title" : "reallife_econapp_city_off_title", _r.CityOn ? "reallife_econapp_quiet_body" : "reallife_econapp_city_off_body");
            }
            for (int i = 0; i < _advice.Count; i++)
            {
                y += InsightCard(Margin, y, width, _advice[i], i + 1) + 24f;
            }
            return y + 8f;
        }
    }
}
