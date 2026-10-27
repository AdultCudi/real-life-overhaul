using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// Part of the Economy app: the Jobs and people tab, unemployment and vacancies through the days, what the jobs market asks of a
    /// player who hires, and how well off the districts' people are.
    /// </summary>
    internal sealed partial class OutlookApp
    {
        private float Jobs(float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float y = 8f;
            if (!_r.CityOn)
            {
                return Notice(y, width, "reallife_econapp_city_off_title", "reallife_econapp_city_off_body");
            }
            if (!_r.JobsOn)
            {
                y = Notice(y, width, "reallife_econapp_jobs_off_title", "reallife_econapp_jobs_off_body");
            }
            else
            {
                int columns = Columns(width, 1040f, 3);
                float cell = CellWidth(width, columns), height = 640f;
                ChartCard(Margin, y, cell, height, OutlookText.T("reallife_econapp_chart_unemployment"), OutlookCatalogue.TileUnemployment, 10.0, AxisPercent, null);
                VacanciesCard(Margin + (1 % columns) * (cell + Gap), y + (1 / columns) * (height + Gap), cell, height);
                PeopleCard(Margin + (2 % columns) * (cell + Gap), y + (2 / columns) * (height + Gap), cell, height);
                y += ((3 + columns - 1) / columns) * (height + Gap);
            }
            y = IncomeCard(Margin, y, width) + Gap;
            return y;
        }

        // The vacancies the city has open at once, through the days.
        private void VacanciesCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            Caption(page, OutlookText.T("reallife_econapp_chart_vacancies"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            Value(page, _r.Vacancies.ToString(Inv), x + Pad, y + Pad + 24f, w * 0.6f, p.Text, 64f);
            _director.Book.Window(OutlookCatalogue.Vacancies, _r.Day, OutlookCatalogue.HistoryDays, _window);
            OutlookDelta shortChange = OutlookRules.Over(_window, _window.Length, OutlookCatalogue.ShortDays, _r.Vacancies, OutlookChange.Percent);
            OutlookDelta longChange = OutlookRules.Over(_window, _window.Length, OutlookCatalogue.LongDays, _r.Vacancies, OutlookChange.Percent);
            float py = y + Pad + 24f + 64f * 1.25f + 6f;
            float used = DeltaPill(page, x + Pad, py, shortChange, 1, null, true, OutlookCatalogue.ShortDays);
            DeltaPill(page, x + Pad + used + 14f, py, longChange, 1, null, true, OutlookCatalogue.LongDays);
            float cy = py + 46f + 12f;
            int drawn = OutlookDraw.Chart(page, x + Pad - 10f, cy, w - Pad * 2f + 20f, Mathf.Max(220f, y + h - Pad + 8f - cy), _window, _window.Length, 1.0, p.Accent, Axis0, _r.Day, true, true);
            _chartsDrawn++;
            _pointsDrawn += drawn;
        }

        // What the jobs market means for someone who hires: seekers, the chance a job is lost, and how that reads.
        private void PeopleCard(float x, float y, float w, float h)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_people_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            float row = y + Pad + 62f;
            string[] captions = { "reallife_econapp_people_seekers", "reallife_econapp_people_layoffs", "reallife_econapp_people_hiring" };
            string layoffs = _r.Layoffs <= 0
                ? OutlookText.T("reallife_econapp_people_layoffs_none")
                : OutlookText.T("reallife_econapp_people_layoffs_value", "rate", (_r.Layoffs / 10.0).ToString("0.#", Inv));
            string hiring = OutlookText.T(_r.Unemployment <= OutlookCatalogue.JobsTight ? "reallife_econapp_people_hiring_hard"
                : _r.Unemployment >= OutlookCatalogue.JobsSlack ? "reallife_econapp_people_hiring_easy" : "reallife_econapp_people_hiring_usual");
            string[] values = { _r.Seekers.ToString(Inv), layoffs, hiring };
            for (int i = 0; i < 3; i++)
            {
                Caption(page, OutlookText.T(captions[i]), x + Pad, row, w - Pad * 2f);
                float vh = page.Fit(values[i], i == 0 ? 56f : SizeBody, w - Pad * 2f, i == 0);
                page.Text(values[i], x + Pad, row + 34f, w - Pad * 2f, vh + 6f, i == 0 ? 56f : SizeBody, p.Text, TextAlignmentOptions.TopLeft, i == 0, true);
                row += 34f + Mathf.Max(64f, vh) + 26f;
            }
            page.Text(OutlookText.T("reallife_econapp_people_note"), x + Pad, y + h - Pad - 92f, w - Pad * 2f, 92f, SizeMicro, p.Muted, TextAlignmentOptions.BottomLeft, false, true);
        }

        // How well off the districts' people are: one bar each, longest first, in the colour of the district on the city map.
        private float IncomeCard(float x, float y, float w)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            int n = OutlookRules.Order(_r, _order);
            float rowHeight = 84f, height = Pad * 2f + 96f + n * rowHeight + 20f;
            page.Card(x, y, w, height, p.Card, Radius);
            Heading(page, OutlookText.T("reallife_econapp_income_title"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            page.Text(OutlookText.T("reallife_econapp_income_note"), x + Pad, y + Pad + 46f, w - Pad * 2f, 40f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);
            if (n == 0)
            {
                return y + height;
            }
            // Longest first by income, the bars drawn on a scale from the lowest income a district can have to the highest.
            var byIncome = new int[n];
            for (int i = 0; i < n; i++)
            {
                byIncome[i] = i;
            }
            for (int i = 1; i < n; i++)
            {
                int held = byIncome[i], j = i - 1;
                while (j >= 0 && _r.Districts[byIncome[j]].Income < _r.Districts[held].Income)
                {
                    byIncome[j + 1] = byIncome[j];
                    j--;
                }
                byIncome[j + 1] = held;
            }
            float top = y + Pad + 108f, nameW = 360f, valueW = 150f, barX = x + Pad + nameW, barW = w - Pad * 2f - nameW - valueW - 20f;
            OutlookMesh mesh = page.Picture(barX, top, barW, n * rowHeight);
            for (int i = 0; i < n; i++)
            {
                OutlookDistrict d = _r.Districts[byIncome[i]];
                float share = Mathf.Clamp01((d.Income - Economy.MacroCatalogue.IncomeLeast) / (float)(Economy.MacroCatalogue.IncomeMost - Economy.MacroCatalogue.IncomeLeast));
                int colour = OutlookTheme.Visible(d.Colour, p.Card, p.Dark);
                OutlookDraw.Meter(mesh, 0f, i * rowHeight + 26f, barW, 28f, share, p.Raised, colour);
                float yy = top + i * rowHeight;
                page.Pill(x + Pad, yy + 33f, 18f, 18f, colour);
                page.Text(OutlookText.Words.District(d.Id), x + Pad + 34f, yy, nameW - 40f, rowHeight, SizeBody, p.Text, TextAlignmentOptions.MidlineLeft, false);
                page.Text(OutlookWords.Tenths(d.Income), barX + barW + 16f, yy, valueW, rowHeight, SizeBody, p.Text, TextAlignmentOptions.MidlineRight, true);
            }
            mesh.End();
            _chartsDrawn++;
            _pointsDrawn += n;
            return y + height;
        }
    }
}
