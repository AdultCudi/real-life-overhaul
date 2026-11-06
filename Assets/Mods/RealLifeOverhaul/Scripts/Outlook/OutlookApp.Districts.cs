using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>Part of the Economy app: the Districts tab, each district as a card in its own colour from the city map, ranked by its pull on customers.</summary>
    internal sealed partial class OutlookApp
    {
        private float Districts(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            if (!_r.CityOn)
            {
                return Notice(y, width, "reallife_econapp_city_off_title", "reallife_econapp_city_off_body");
            }
            int n = OutlookRules.Order(_r, _order);
            if (n == 0)
            {
                return Notice(y, width, "reallife_econapp_districts_none_title", "reallife_econapp_districts_none_body");
            }
            page.Text(OutlookText.T("reallife_econapp_districts_note"), Margin, y, width, 90f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            y += Mathf.Max(48f, page.Fit(OutlookText.T("reallife_econapp_districts_note"), SizeBody, width)) + 28f;
            int columns = Columns(width, 1040f, 3);
            float cell = CellWidth(width, columns);
            for (int start = 0; start < n; start += columns)
            {
                float rowHeight = 0f;
                for (int c = 0; c < columns && start + c < n; c++)
                {
                    rowHeight = Mathf.Max(rowHeight, DistrictHeight(_r.Districts[_order[start + c]]));
                }
                for (int c = 0; c < columns && start + c < n; c++)
                {
                    DistrictCard(Margin + c * (cell + Gap), y, cell, rowHeight, _r.Districts[_order[start + c]]);
                }
                y += rowHeight + Gap;
            }
            return Places(y, width);
        }

        private static float DistrictHeight(OutlookDistrict d)
        {
            return d.HasDemand ? 1140f : 800f;
        }

        private void DistrictCard(float x, float y, float w, float h, OutlookDistrict d)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            int colour = OutlookTheme.Visible(d.Colour, p.Card, p.Dark);
            page.Card(x, y, w, h, p.Card, Radius);
            // The colour of the district on the city map, down the card's edge.
            OutlookMesh edge = page.Picture(x, y, 16f, h);
            edge.Round(0f, 30f, 9f, h - 30f, 4.5f, 4.5f, OutlookKit.C32(colour));
            edge.End();

            // The rank, the name and the pull on customers.
            int badge = OutlookTheme.Legible(colour, out int badgeInk);
            page.Pill(x + Pad + 8f, y + Pad - 4f, 74f, 74f, badge);
            page.Text(d.Rank.ToString(Inv), x + Pad + 8f, y + Pad - 4f, 74f, 74f, 38f, badgeInk, TextAlignmentOptions.Midline, true);
            string name = OutlookText.Words.District(d.Id);
            page.Text(name, x + Pad + 102f, y + Pad - 6f, w - Pad * 2f - 102f - 230f, 80f, 40f, p.Text, TextAlignmentOptions.MidlineLeft, true);
            Caption(page, OutlookText.T("reallife_econapp_district_pull"), x + w - Pad - 220f, y + Pad - 6f, 220f);
            Value(page, d.Pull.ToString(Inv), x + w - Pad - 220f, y + Pad + 24f, 220f, p.Accent, 44f);

            // Income, how it moved, and its line.
            float row = y + Pad + 110f;
            Caption(page, OutlookText.T("reallife_econapp_district_income"), x + Pad + 8f, row, w * 0.5f);
            Value(page, OutlookWords.Tenths(d.Income), x + Pad + 8f, row + 30f, w * 0.4f, p.Text, 64f);
            ChangePoints points = n => OutlookText.Pts((n / 10.0).ToString("0.0", Inv));
            DeltaPill(page, x + Pad + 8f + Mathf.Max(190f, page.Measure(OutlookWords.Tenths(d.Income), 64f, true) + 36f), row + 52f, d.IncomeLong, 1, points, false, OutlookCatalogue.LongDays);
            _director.Book.Window(OutlookCatalogue.OfDistrictIncome(d.Id), _r.Day, OutlookCatalogue.HistoryDays, _window);
            int drawn = OutlookDraw.Spark(page, x + Pad - 4f, row + 124f, w - Pad * 2f + 8f, 128f, _window, _window.Length, 10.0, colour);
            _chartsDrawn++;
            _pointsDrawn += drawn;

            // Four figures of the district.
            float figures = row + 276f, half = (w - Pad * 2f - 8f) / 2f;
            string[] captions = { "reallife_econapp_district_worth", "reallife_econapp_district_land", "reallife_econapp_district_foot", "reallife_econapp_district_bears" };
            string[] values =
            {
                OutlookWords.Factor(d.Property), d.Land > 0 ? OutlookText.T("reallife_econapp_district_land_value", "amount", OutlookText.Money(d.Land)) : "-",
                OutlookWords.Whole(d.Footfall + _r.Footfall), OutlookWords.Factor(d.Bears)
            };
            for (int i = 0; i < 4; i++)
            {
                float fx = x + Pad + 8f + (i % 2) * half, fy = figures + (i / 2) * 104f;
                Caption(page, OutlookText.T(captions[i]), fx, fy, half - 20f);
                page.Text(values[i], fx, fy + 32f, half - 20f, 52f, 36f, p.Text, TextAlignmentOptions.TopLeft, true);
            }

            // The people: working, middle and upper class.
            float mix = figures + 224f;
            Caption(page, OutlookText.T("reallife_econapp_district_people"), x + Pad + 8f, mix, w - Pad * 2f);
            int total = Math.Max(1, d.Working + d.Middle + d.Upper);
            OutlookMesh bar = page.Picture(x + Pad + 8f, mix + 40f, w - Pad * 2f - 8f, 26f);
            float bw = w - Pad * 2f - 8f, at = 0f;
            int[] shares = { d.Working, d.Middle, d.Upper };
            int[] inks = { p.Info, p.Accent, p.Warn };
            for (int i = 0; i < 3; i++)
            {
                float seg = bw * shares[i] / total;
                if (seg > 3f)
                {
                    bar.Round(at, 0f, at + seg - 3f, 26f, 13f, 13f, OutlookKit.C32(inks[i]));
                }
                at += seg;
            }
            bar.End();
            string legend = OutlookText.T("reallife_econapp_district_mix", "working", d.Working.ToString(Inv), "middle", d.Middle.ToString(Inv), "upper", d.Upper.ToString(Inv));
            page.Text(legend, x + Pad + 8f, mix + 74f, w - Pad * 2f, 40f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);

            if (d.HasDemand)
            {
                WriteDemand(x + Pad + 8f, mix + 138f, w - Pad * 2f - 8f, d);
            }
        }

        // What the mod counts of the districts it adds: demand for each use, and the level of the market.
        private void WriteDemand(float x, float y, float w, OutlookDistrict d)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            Caption(page, OutlookText.T("reallife_econapp_district_demand"), x, y, w);
            const float top = 52f, barH = 170f;
            OutlookMesh mesh = page.Picture(x, y + top, w, barH + 4f);
            float part = w / 4f, barW = Mathf.Min(70f, part * 0.4f);
            mesh.Rect(0f, barH, w, barH + 3f, OutlookKit.C32(p.Grid));
            for (int u = 0; u < 4; u++)
            {
                int demand = d.Demand[u];
                float cx = part * u + part * 0.5f;
                float height = demand < 0 ? 0f : Mathf.Max(8f, barH * Mathf.Clamp01(demand / 100f));
                int colour = demand >= OutlookCatalogue.DemandHigh ? p.Good : demand >= 0 && demand <= OutlookCatalogue.DemandLow ? p.Bad : p.Accent;
                if (height > 0f)
                {
                    mesh.Bar(cx - barW * 0.5f, barH - height, cx + barW * 0.5f, barH, 10f, OutlookKit.C32(colour));
                }
            }
            mesh.End();
            _chartsDrawn++;
            _pointsDrawn += 4;
            for (int u = 0; u < 4; u++)
            {
                int demand = d.Demand[u];
                float cx = x + part * u;
                float height = demand < 0 ? 0f : Mathf.Max(8f, barH * Mathf.Clamp01(demand / 100f));
                page.Text(demand < 0 ? "-" : demand.ToString(Inv), cx, y + top + barH - height - 40f, part, 36f, SizeSmall, p.Text, TextAlignmentOptions.Midline, true);
                page.Text(OutlookText.T(OutlookWords.UseKeys[u]), cx, y + top + barH + 12f, part, 36f, SizeMicro, p.Muted, TextAlignmentOptions.Top);
            }
            string line = d.Level > 0 ? OutlookText.T("reallife_econapp_district_level", "level", OutlookWords.Factor(d.Level)) : string.Empty;
            if (d.Growing >= 0)
            {
                string going = d.Growing == 1 ? "reallife_econapp_district_going_one" : "reallife_econapp_district_going";
                line += (line.Length > 0 ? "   " : string.Empty) + OutlookText.T(going, "n", d.Growing.ToString(Inv));
            }
            if (line.Length > 0)
            {
                page.Text(line, x, y + top + barH + 56f, w, 40f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);
            }
        }
    }
}
