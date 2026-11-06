using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>Part of the Economy app: the Markets tab, the city's stock index through the days and its sectors.</summary>
    internal sealed partial class OutlookApp
    {
        private readonly int[] _closes = new int[60];

        private static string AxisThousands(double v) => v.ToString("N0", CultureInfo.CurrentCulture);

        private float Markets(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            if (!_r.MarketOn)
            {
                return Notice(y, width, "reallife_econapp_markets_off_title", "reallife_econapp_markets_off_body");
            }
            const float heroHeight = 720f;
            ChartCard(Margin, y, width, heroHeight, OutlookText.T("reallife_econapp_chart_stocks"), OutlookCatalogue.TileStocks, 100.0, AxisThousands, null);
            // The market's state, in the corner of its card, and the day's move.
            string status = OutlookText.T(_r.MarketOpen ? "reallife_econapp_market_open" : "reallife_econapp_market_closed");
            float sw = page.Measure(status, SizeSmall, true) + 52f;
            int ink = _r.MarketOpen ? p.Good : p.Muted, fill = _r.MarketOpen ? p.GoodSoft : p.Flat;
            page.Pill(Margin + width - Pad - sw, y + Pad - 4f, sw, 46f, fill);
            page.Text(status, Margin + width - Pad - sw, y + Pad - 4f, sw, 46f, SizeSmall, ink, TextAlignmentOptions.Midline, true);
            OutlookDelta today = OutlookDelta.Of(_r.StocksDayBp);
            float dw = DeltaWidth(page, today, 1, null, true, 1);
            DeltaPill(page, Margin + width - Pad - sw - 14f - dw, y + Pad - 4f, today, 1, null, true, 1);
            y += heroHeight + Gap;

            // The sectors, the biggest movers of the day first.
            page.Text(OutlookText.T("reallife_econapp_sectors"), Margin, y + 4f, width, 54f, 42f, p.Text, TextAlignmentOptions.TopLeft, true);
            y += 78f;
            int count = _r.Sectors.Count;
            if (count == 0)
            {
                return y;
            }
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = 1; i < count; i++)
            {
                int held = order[i], j = i - 1;
                while (j >= 0 && Math.Abs(_r.Sectors[order[j]].DayBp) < Math.Abs(_r.Sectors[held].DayBp))
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = held;
            }
            int columns = Columns(width, 700f, 4);
            float cell = CellWidth(width, columns), height = 330f;
            int shown = Math.Min(count, OutlookReadings.MostSectors);
            for (int i = 0; i < shown; i++)
            {
                SectorCard(Margin + (i % columns) * (cell + Gap), y + (i / columns) * (height + Gap), cell, height, _r.Sectors[order[i]]);
            }
            y += ((shown + columns - 1) / columns) * (height + Gap);
            return y;
        }

        // The sector's name as the market writes it, or "Sector 7" where it gives none (OutlookWords.SectorLabel): never a bare number.
        private static string SectorLabel(int id)
        {
            return OutlookText.Words.SectorLabel(id, OutlookBridges.SectorName != null ? OutlookBridges.SectorName(id) : null);
        }

        private void SectorCard(float x, float y, float w, float h, OutlookSector sector)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            page.Text(SectorLabel(sector.Id), x + Pad, y + Pad - 6f, w - Pad * 2f, 50f, SizeCard, p.Text, TextAlignmentOptions.TopLeft, true);
            page.Text((sector.Last / 100.0).ToString("N2", CultureInfo.CurrentCulture), x + Pad, y + Pad + 48f, w * 0.5f, 56f, 44f, p.Text, TextAlignmentOptions.TopLeft, true);
            OutlookDelta move = OutlookDelta.Of(sector.DayBp);
            DeltaPill(page, x + w - Pad - DeltaWidth(page, move, 1, null, true, 1), y + Pad + 54f, move, 1, null, true, 1);
            for (int i = 0; i < _window.Length; i++)
            {
                _window[i] = OutlookCatalogue.NoValue;
            }
            OutlookBridges.SectorCloses closes = OutlookBridges.Sectors;
            int got = closes != null ? closes(sector.Id, _closes.Length, _closes) : 0;
            got = Math.Min(got, _closes.Length);
            for (int i = 0; i < got; i++)
            {
                _window[_closes.Length - got + i] = _closes[i];
            }
            int drawn = OutlookDraw.Spark(page, x + Pad - 8f, y + Pad + 112f, w - Pad * 2f + 16f, h - Pad * 2f - 96f, _window, _closes.Length, 100.0, p.Accent);
            _chartsDrawn++;
            _pointsDrawn += drawn;
        }
    }
}
