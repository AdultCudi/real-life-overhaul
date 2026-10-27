using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>Part of the Economy app: the page of the body (what each tab writes), the Overview, and the shared empty states.</summary>
    internal sealed partial class OutlookApp
    {
        // ------------------------------------------------------------------ the body

        private void WriteBody(OutlookPalette p, float width, float height, bool read)
        {
            _body.Begin(p);
            _chartsDrawn = 0;
            _pointsDrawn = 0;
            float content = Mathf.Max(400f, width - Margin * 2f);
            float used;
            if (!read)
            {
                used = Notice(8f, content, "reallife_econapp_none_title", "reallife_econapp_none_body");
            }
            else
            {
                switch (_tab)
                {
                    case OutlookTab.Prices: used = Prices(content); break;
                    case OutlookTab.Jobs: used = Jobs(content); break;
                    case OutlookTab.Districts: used = Districts(content); break;
                    case OutlookTab.Markets: used = Markets(content); break;
                    case OutlookTab.Finances: used = Finances(content); break;
                    case OutlookTab.Insights: used = Insights(content); break;
                    default: used = Overview(content); break;
                }
            }
            _body.End(used + Margin);
            Last = new Drawn
            {
                Tab = _tab, Cards = _body.CardsDrawn, Words = _body.WordsDrawn, Pictures = _body.PicturesDrawn, Clicks = _body.ClicksDrawn,
                Charts = _chartsDrawn, Readings = _pointsDrawn
            };
        }

        // A card with a heading and a sentence, for a part that is off or has nothing yet. Returns where the next thing starts.
        private float Notice(float y, float width, string titleKey, string bodyKey)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            string title = OutlookText.T(titleKey), text = OutlookText.T(bodyKey);
            float textW = width - Pad * 2f - 120f;
            float headH = page.Fit(title, SizeCard, textW, true), bodyH = page.Fit(text, SizeBody, textW);
            float h = Pad * 2f + headH + 12f + bodyH;
            page.Card(Margin, y, width, h, p.Card, Radius);
            page.Pill(Margin + Pad, y + Pad, 56f, 56f, p.InfoSoft);
            OutlookMesh sign = page.Picture(Margin + Pad, y + Pad, 56f, 56f);
            Sign(sign, OutlookInsightKind.Info, 28f, 28f, p.Info, p.InfoSoft);
            sign.End();
            page.Text(title, Margin + Pad + 84f, y + Pad - 2f, textW, headH + 6f, SizeCard, p.Text, TextAlignmentOptions.TopLeft, true, true);
            page.Text(text, Margin + Pad + 84f, y + Pad + headH + 10f, textW, bodyH + 6f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            return y + h + Gap;
        }

        private int Columns(float width, float least, int most)
        {
            return Mathf.Clamp(Mathf.FloorToInt((width + Gap) / (least + Gap)), 1, most);
        }

        private static float CellWidth(float width, int columns)
        {
            return (width - Gap * (columns - 1)) / columns;
        }

        // A small quiet pill with a line of lettering. Returns its width.
        private float Chip(OutlookPage page, float x, float y, string text, int fill, int ink, float size = SizeSmall, bool bold = false)
        {
            float w = page.Measure(text, size, bold) + 44f;
            page.Pill(x, y, w, 46f, fill);
            page.Text(text, x, y, w, 46f, size, ink, TextAlignmentOptions.Midline, bold);
            return w;
        }

        // The ink and fill of a verdict: green growing, blue steady, amber slowing or overheating, red in recession.
        private static void Tint(OutlookPalette p, OutlookVerdict verdict, out int ink, out int fill)
        {
            switch (verdict)
            {
                case OutlookVerdict.Growing: Tint(p, OutlookTone.Good, out ink, out fill); break;
                case OutlookVerdict.Steady: Tint(p, OutlookTone.Neutral, out ink, out fill); break;
                case OutlookVerdict.Recession: Tint(p, OutlookTone.Bad, out ink, out fill); break;
                default:
                    ink = p.Warn;
                    fill = p.WarnSoft;
                    break;
            }
        }

        // ------------------------------------------------------------------ the Overview

        private float Overview(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            if (!_r.CityOn)
            {
                y = Notice(y, width, "reallife_econapp_city_off_title", "reallife_econapp_city_off_body");
            }
            else
            {
                y = Hero(y, width) + Gap;
            }
            int columns = Columns(width, 760f, 4);
            float cell = CellWidth(width, columns), tileHeight = 340f;
            int tiles = OutlookCatalogue.Tiles.Length;
            for (int i = 0; i < tiles; i++)
            {
                Tile(Margin + (i % columns) * (cell + Gap), y + (i / columns) * (tileHeight + Gap), cell, tileHeight, i);
            }
            y += ((tiles + columns - 1) / columns) * (tileHeight + Gap);

            // The top of the advice, and the way to the rest.
            int count = Math.Min(OutlookCatalogue.InsightsOnOverview, _advice.Count);
            string title = OutlookText.T("reallife_econapp_overview_advice");
            page.Text(title, Margin, y + 4f, width - 400f, 54f, 42f, p.Text, TextAlignmentOptions.TopLeft, true);
            if (_advice.Count > count)
            {
                string all = OutlookText.T("reallife_econapp_all_insights", "n", _advice.Count.ToString(Inv));
                float w = page.Measure(all, SizeBody, true) + 56f;
                page.Pill(Margin + width - w, y, w, 56f, p.AccentSoft);
                page.Text(all, Margin + width - w, y, w, 56f, SizeBody, p.Accent, TextAlignmentOptions.Midline, true);
                page.Click(Margin + width - w, y, w, 56f, () => PickTab(OutlookTab.Insights));
            }
            y += 78f;
            for (int i = 0; i < count; i++)
            {
                y += InsightCard(Margin, y, width, _advice[i], i + 1) + 24f;
            }
            if (count == 0)
            {
                y = Notice(y, width, "reallife_econapp_quiet_title", "reallife_econapp_quiet_body");
            }
            return y;
        }

        // The verdict card: the word, its line, and the last 120 days of the cycle as bands.
        private float Hero(float y, float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            OutlookWords words = OutlookText.Words;
            const float height = 410f;
            page.Card(Margin, y, width, height, p.Card, Radius + 4f);
            Tint(p, _verdict, out int ink, out int fill);
            float leftWidth = Mathf.Min(width * 0.5f, 1700f) - Pad;
            Caption(page, OutlookText.T("reallife_econapp_hero_caption"), Margin + Pad + 4f, y + Pad - 4f, leftWidth);
            page.Pill(Margin + Pad + 4f, y + Pad + 52f, 24f, 24f, ink);
            string word = words.VerdictWord(_verdict);
            page.Text(word, Margin + Pad + 44f, y + Pad + 18f, leftWidth - 44f, 118f, 100f, ink, TextAlignmentOptions.MidlineLeft, true);
            string line = words.VerdictLine(_verdict, _r);
            float lineHeight = page.Fit(line, 32f, leftWidth);
            page.Text(line, Margin + Pad + 4f, y + Pad + 150f, leftWidth, lineHeight + 6f, 32f, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            // Three facts as pills along the foot.
            float fx = Margin + Pad + 4f, fy = y + height - Pad - 46f;
            fx += Chip(page, fx, fy, OutlookText.T("reallife_econapp_fact_month", "n", _r.MonthsIn.ToString(Inv)), p.Raised, p.Muted) + 14f;
            string decision = _r.DaysToDecision == 1 ? "reallife_econapp_fact_decision_one" : "reallife_econapp_fact_decision";
            fx += Chip(page, fx, fy, OutlookText.T(decision, "days", _r.DaysToDecision.ToString(Inv)), p.Raised, p.Muted) + 14f;
            if (_r.Districts.Count > 0)
            {
                int best = -1;
                for (int i = 0; i < _r.Districts.Count; i++)
                {
                    if (_r.Districts[i].Rank == 1)
                    {
                        best = i;
                    }
                }
                if (best >= 0)
                {
                    Chip(page, fx, fy, OutlookText.T("reallife_econapp_fact_best", "district", OutlookText.Words.District(_r.Districts[best].Id)), p.AccentSoft, p.Accent);
                }
            }

            // The cycle through the last 120 days.
            float rx = Margin + width * 0.5f + 20f, rw = width * 0.5f - Pad - 20f;
            Caption(page, OutlookText.T("reallife_econapp_hero_history"), rx, y + Pad - 4f, rw);
            _director.Book.Window(OutlookCatalogue.Cycle, _r.Day, OutlookCatalogue.HistoryDays, _window);
            int[] colours = { p.Good, p.Info, p.Warn, p.Bad };
            OutlookDraw.Bands(page, rx, y + Pad + 44f, rw, 150f, _window, OutlookCatalogue.HistoryDays, colours);
            _chartsDrawn++;
            int known = 0;
            for (int i = 0; i < _window.Length; i++)
            {
                known += _window[i] != OutlookCatalogue.NoValue ? 1 : 0;
            }
            _pointsDrawn += known;
            // The legend under it, and the figures the state moves.
            float lx = rx, ly = y + Pad + 44f + 150f + 28f;
            string[] keys = { "reallife_macro_state_boom", "reallife_macro_state_steady", "reallife_macro_state_slowdown", "reallife_macro_state_recession" };
            for (int i = 0; i < 4; i++)
            {
                string name = OutlookText.T(keys[i]);
                page.Pill(lx, ly + 11f, 20f, 20f, colours[i]);
                float w = page.Measure(name, SizeSmall);
                page.Text(name, lx + 32f, ly, w + 10f, 42f, SizeSmall, p.Muted, TextAlignmentOptions.MidlineLeft);
                lx += 32f + w + 40f;
            }
            string effects = OutlookText.T("reallife_econapp_hero_effects", "footfall", OutlookWords.Whole(_r.Footfall), "spend", OutlookWords.Factor(_r.Spend));
            page.Text(effects, rx, y + height - Pad - 46f, rw, 46f, SizeSmall, p.Muted, TextAlignmentOptions.MidlineLeft);
            return y + height;
        }
    }
}
