using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// Part of the Economy app: the People tab, the city's people as the kit's Economy window shows them (the same rules, NewsPeopleRules and
    /// PopulationPages, and the same path from the people to a district, the renters and a household, NewsPath), drawn in this app's cards: the
    /// headline figures with their links, the ages and the incomes as donuts, the districts as a table, a district's own page, who pays the most
    /// rent and a household. From the figures a link opens the city's people page (every district, building, household and person) in the same
    /// place, with its own way back. Written with the rest of the app on opening, a press and each game hour.
    /// </summary>
    internal sealed partial class OutlookApp
    {
        private const float RowHeight = 84f, HeadRow = 64f, ColumnWide = 220f, TwoColumns = 2000f;

        private readonly NewsPath _path = new NewsPath();
        private readonly Stack<BrowseStep> _way = new Stack<BrowseStep>();
        private int _home = -1;
        private int[] _districtSlots = new int[0], _homeSlots = new int[0];
        private bool _browse;
        private ViewTarget _at = ViewTarget.To(ViewKind.City, 0);
        private string _browseName;
        private PopulationView _view;
        private PopulationSource _source;
        private PeopleTally _tally;   // the records counted, kept for the day: a count is a pass over every person, so a press and an hour do not repeat it
        private PopulationState _tallyOf;
        private int _tallyDay = -1;
        private UiDataTableModel _list;   // the list the page showing was written with, for the name of the row pressed

        private struct BrowseStep
        {
            public ViewTarget At;
            public string Name;
        }

        /// <summary>Pages opened from the People tab are put away: the app opens on its tab, never on a page left from before.</summary>
        private void PutPeopleAway()
        {
            _path.Clear();
            _browse = false;
            _way.Clear();
            _at = ViewTarget.To(ViewKind.City, 0);
            _browseName = null;
            _home = -1;
            _tally = null;
        }

        /// <summary>Opens the city's people page on a person or place (a teacher's or a seller's page), in this tab.</summary>
        internal void WantPeople(ViewTarget at)
        {
            PutPeopleAway();
            _browse = true;
            _at = at;
            Want(OutlookTab.People);
        }

        // ------------------------------------------------------------------ the tab

        private PeopleTally Tally(PopulationState pop)
        {
            if (_tally == null || _tallyDay != _r.Day || !ReferenceEquals(_tallyOf, pop))
            {
                _tally = PeopleTally.Read(pop);
                _tallyOf = pop;
                _tallyDay = _r.Day;
            }
            return _tally;
        }

        private float People(float width)
        {
            float y = 8f;
            if (!_r.CityOn)
            {
                return Notice(y, width, "reallife_econapp_city_off_title", "reallife_econapp_city_off_body");
            }
            PopulationState pop = PopulationDirector.Instance != null ? PopulationDirector.Instance.State : null;
            if (pop == null)
            {
                return Notice(y, width, "reallife_news_empty_city", "reallife_news_empty_city_body");
            }
            if (_browse)
            {
                return Browse(y, width);
            }
            PeopleTally tally = Tally(pop);
            if (tally.People == 0)
            {
                return Notice(y, width, "reallife_news_empty_city", "reallife_news_empty_city_body");
            }
            INewsWords words = NewsLive.WordsFor(pop);
            switch (_path.Page(OutlookTab.People))
            {
                case NewsPage.Household:
                    if (_home >= 0 && _home < pop.Households.Count && pop.Households.Alive(_home))
                    {
                        return HomePage(y, width, pop, words);
                    }
                    _path.HomeGone();   // that household has gone from the city since its list was made: the list it came from shows again
                    return People(width);
                case NewsPage.Renters:
                    return RentersPage(y, width, pop, words);
                case NewsPage.District:
                    if (_path.District < tally.DistrictPeople.Length)
                    {
                        return DistrictPage(y, width, pop, tally, words);
                    }
                    _path.Back();
                    return People(width);
                default:
                    return CityPage(y, width, tally, words);
            }
        }

        private float CityPage(float y, float width, PeopleTally tally, INewsWords words)
        {
            NewsPeople people = NewsPeopleRules.City(tally, words, out _districtSlots);
            UiActionModel[] links = NewsPeopleRules.Links(tally, words);
            _path.Busy = tally.Busiest();
            int[] dots = DistrictDots(_districtSlots);
            return PeopleLayout(y, width, people, links, true, dots, row => DistrictRow(row));
        }

        private float DistrictPage(float y, float width, PopulationState pop, PeopleTally tally, INewsWords words)
        {
            int d = _path.District;
            y = BackRow(y, width, OutlookText.T("reallife_news_people_title"), null);
            NewsPeople people = NewsPeopleRules.District(tally, d, words);
            people.Table = NewsPeopleRules.Renters(pop, d, words, PopulationGame.NameOf, out _homeSlots);
            return PeopleLayout(y, width, people, new UiActionModel[0], false, null, row => HomeRow(row));
        }

        private float RentersPage(float y, float width, PopulationState pop, INewsWords words)
        {
            y = BackRow(y, width, OutlookText.T("reallife_news_people_title"), OutlookText.T("reallife_news_rent_link"));
            UiDataTableModel table = NewsPeopleRules.Renters(pop, -1, words, PopulationGame.NameOf, out _homeSlots);
            if (table.RowCount == 0)
            {
                return Notice(y, width, "reallife_news_empty_rent", "reallife_news_empty_rent_body");
            }
            return DataCard(Margin, y, width, OutlookText.T("reallife_news_rent_link"), table, null, row => HomeRow(row)) + Gap;
        }

        private float HomePage(float y, float width, PopulationState pop, INewsWords words)
        {
            string back = _path.RentersOpen ? OutlookText.T("reallife_news_rent_link") : NewsLive.DistrictNameOf(pop, _path.District);
            y = BackRow(y, width, back, null);
            UiInfoModel home = NewsPeopleRules.Household(pop, _home, words, PopulationGame.NameOf);
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float cw = Mathf.Min(width, 1100f);
            float rows = 0f;
            for (int s = 0; s < home.Sections.Length; s++)
            {
                rows += (home.Sections.Length > 1 ? 56f : 0f) + home.Sections[s].Rows.Length * 68f + 16f;
            }
            float h = Pad * 2f + 62f + rows;
            page.Card(Margin, y, cw, h, p.Card, Radius);
            Heading(page, home.Title, Margin + Pad, y + Pad - 6f, cw - Pad * 2f);
            float cy = y + Pad + 62f;
            for (int s = 0; s < home.Sections.Length; s++)
            {
                if (home.Sections.Length > 1)
                {
                    Caption(page, home.Sections[s].Label, Margin + Pad, cy, cw - Pad * 2f);
                    cy += 56f;
                }
                for (int r = 0; r < home.Sections[s].Rows.Length; r++)
                {
                    UiInfoRow row = home.Sections[s].Rows[r];
                    cy += Pair(page, Margin + Pad, cy, cw - Pad * 2f, row.Label, row.Value, row.Ink);
                }
                cy += 16f;
            }
            return y + h + Gap;
        }

        // ------------------------------------------------------------------ the people's figures, the donuts and the table

        // The figures and the donuts at the left, the table at the right; stacked where the page is narrow.
        private float PeopleLayout(float y, float width, NewsPeople p, UiActionModel[] links, bool browseLink, int[] dots, Action<int> onRow)
        {
            bool two = width >= TwoColumns && p.Table != null;
            float lw = two ? Mathf.Min(1040f, width * 0.4f) : width;
            float rx = Margin + lw + Gap, rw = width - lw - Gap;
            float left = FiguresCard(Margin, y, lw, p, links, browseLink);
            float donutY = y + left + Gap;
            float donuts = DonutCard(Margin, donutY, lw, p.ChartTitle, p.ChartLabels, p.ChartValues, p.ChartShares);
            donutY += donuts + Gap;
            donutY += DonutCard(Margin, donutY, lw, p.Chart2Title, p.Chart2Labels, p.Chart2Values, p.Chart2Shares) + Gap;
            if (p.Table == null)
            {
                return donutY;
            }
            if (two)
            {
                float table = DataCard(rx, y, rw, p.TableTitle, p.Table, dots, onRow);
                return Mathf.Max(donutY, y + table + Gap);
            }
            return DataCard(Margin, donutY, width, p.TableTitle, p.Table, dots, onRow) + Gap;
        }

        // The card of headline figures: the first big, the rest in rows, then the links to who pays the most rent, the busiest district and the people page.
        private float FiguresCard(float x, float y, float w, NewsPeople p, UiActionModel[] links, bool browseLink)
        {
            OutlookPage page = _body;
            OutlookPalette pal = page.Palette;
            int rows = p.Rows.Length;
            int linkCount = links.Length + (browseLink ? 1 : 0);
            float h = Pad * 2f + 62f + (string.IsNullOrEmpty(p.HeadLabel) ? 0f : 140f) + rows * 68f + (linkCount > 0 ? 24f + linkCount * 76f : 0f);
            page.Card(x, y, w, h, pal.Card, Radius);
            Heading(page, p.Title, x + Pad, y + Pad - 6f, w - Pad * 2f);
            float cy = y + Pad + 62f;
            if (!string.IsNullOrEmpty(p.HeadLabel))
            {
                Caption(page, p.HeadLabel, x + Pad, cy, w - Pad * 2f);
                Value(page, p.HeadValue, x + Pad, cy + 30f, w - Pad * 2f, pal.Text, 64f);
                cy += 140f;
            }
            for (int i = 0; i < rows; i++)
            {
                cy += Pair(page, x + Pad, cy, w - Pad * 2f, p.Rows[i].Label, p.Rows[i].Value, p.Rows[i].Ink);
            }
            if (linkCount == 0)
            {
                return h;
            }
            cy += 24f;
            for (int i = 0; i < links.Length; i++)
            {
                int command = links[i].Command;
                LinkPill(page, x + Pad, cy, w - Pad * 2f, links[i].Label, () => OpenLink(command));
                cy += 76f;
            }
            if (browseLink)
            {
                LinkPill(page, x + Pad, cy, w - Pad * 2f, OutlookText.T("reallife_ui_page_population"), OpenBrowse);
            }
            return h;
        }

        private void OpenLink(int command)
        {
            if (_path.Press(command))
            {
                _body.ScrollToTop();
                Refresh();
            }
        }

        private void OpenBrowse()
        {
            _browse = true;
            _way.Clear();
            _at = ViewTarget.To(ViewKind.City, 0);
            _browseName = null;
            _body.ScrollToTop();
            Refresh();
        }

        // A link as an accent pill with its words and an arrow, pressed as a whole.
        private void LinkPill(OutlookPage page, float x, float y, float w, string text, Action click)
        {
            OutlookPalette p = page.Palette;
            page.Pill(x, y, w, 64f, p.AccentSoft);
            page.Text(text, x + 28f, y, w - 84f, 64f, SizeBody, p.Accent, TextAlignmentOptions.MidlineLeft, true);
            page.Text("›", x + w - 56f, y, 36f, 64f, SizeBody, p.Accent, TextAlignmentOptions.Midline, true);
            page.Click(x, y, w, 64f, click);
        }

        // A label at the left in the quiet colour and its value at the right. Returns the row's height.
        private float Pair(OutlookPage page, float x, float y, float w, string label, string value, UiRole ink)
        {
            OutlookPalette p = page.Palette;
            page.Text(label, x, y, w * 0.58f, 60f, SizeBody, p.Muted, TextAlignmentOptions.MidlineLeft);
            page.Text(value, x + w * 0.42f, y, w * 0.58f, 60f, SizeBody, Ink(p, ink), TextAlignmentOptions.MidlineRight, true);
            return 68f;
        }

        private static int Ink(OutlookPalette p, UiRole role)
        {
            switch (role)
            {
                case UiRole.Muted: return p.Muted;
                case UiRole.OkText: return p.Good;
                case UiRole.WarnText: return p.Warn;
                case UiRole.DangerText: return p.Bad;
                default: return p.Text;
            }
        }

        // A donut of shares with what each slice is and how much of the whole it takes.
        private float DonutCard(float x, float y, float w, string title, string[] labels, string[] values, float[] shares)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            int n = labels == null ? 0 : labels.Length;
            float size = 240f, h = Pad * 2f + 62f + Mathf.Max(size, n * 64f);
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, title, x + Pad, y + Pad - 6f, w - Pad * 2f);
            if (shares == null || shares.Length == 0)
            {
                return h;
            }
            int[] colours = { p.Accent, p.Info, p.Warn };
            float top = y + Pad + 62f;
            OutlookMesh mesh = page.Picture(x + Pad, top, size, size);
            OutlookDraw.Donut(mesh, size * 0.5f, size * 0.5f, size * 0.5f, size * 0.32f, shares, shares.Length, colours, p.Raised);
            mesh.End();
            _chartsDrawn++;
            _pointsDrawn += shares.Length;
            float lx = x + Pad + size + 48f, lw = x + w - Pad - lx, ly = top + (size - n * 64f) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                page.Pill(lx, ly + i * 64f + 21f, 22f, 22f, colours[i % colours.Length]);
                page.Text(labels[i], lx + 40f, ly + i * 64f, lw - 40f - 150f, 64f, SizeBody, p.Text, TextAlignmentOptions.MidlineLeft);
                page.Text(values[i], lx + lw - 150f, ly + i * 64f, 150f, 64f, SizeBody, p.Text, TextAlignmentOptions.MidlineRight, true);
            }
            return h;
        }

        // The colour of a district's dot: the colour it has on the city map, by its name among the readings; the accent where there is none.
        private int[] DistrictDots(int[] places)
        {
            PopulationState pop = PopulationDirector.Instance != null ? PopulationDirector.Instance.State : null;
            OutlookPalette p = _body.Palette;
            var dots = new int[places.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = p.Accent;
                if (pop == null || places[i] < 0 || places[i] >= pop.Districts.Count)
                {
                    continue;
                }
                string id = pop.Districts[places[i]].Id;
                for (int d = 0; d < _r.DistrictCount; d++)
                {
                    if (_r.Districts[d].Id == id)
                    {
                        dots[i] = OutlookTheme.Visible(_r.Districts[d].Colour, p.Card, p.Dark);
                        break;
                    }
                }
            }
            return dots;
        }

        private void DistrictRow(int row)
        {
            if (row >= 0 && row < _districtSlots.Length && _path.OpenDistrict(_districtSlots[row]))
            {
                _body.ScrollToTop();
                Refresh();
            }
        }

        private void HomeRow(int row)
        {
            if (row >= 0 && row < _homeSlots.Length)
            {
                _home = _homeSlots[row];
                if (_path.OpenHome())
                {
                    _body.ScrollToTop();
                    Refresh();
                }
            }
        }

        // The way back from a page opened from another: a pill naming the page under it, then this page's own name. Returns where the next thing starts.
        private float BackRow(float y, float width, string back, string title)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float bw = page.Measure(back, SizeBody, true) + 100f;
            page.Pill(Margin, y, bw, 64f, p.AccentSoft);
            page.Text("‹", Margin + 22f, y, 36f, 64f, SizeBody, p.Accent, TextAlignmentOptions.Midline, true);
            page.Text(back, Margin + 60f, y, bw - 80f, 64f, SizeBody, p.Accent, TextAlignmentOptions.MidlineLeft, true);
            page.Click(Margin, y, bw, 64f, BackOnce);
            if (!string.IsNullOrEmpty(title))
            {
                page.Text(title, Margin + bw + 32f, y, width - bw - 32f, 64f, SizeCard, p.Text, TextAlignmentOptions.MidlineLeft, true);
            }
            return y + 64f + Gap;
        }

        private void BackOnce()
        {
            if (_browse)
            {
                if (_way.Count > 0)
                {
                    BrowseStep step = _way.Pop();
                    _at = step.At;
                    _browseName = step.Name;
                }
                else
                {
                    _browse = false;
                }
            }
            else
            {
                _path.Back();
            }
            _body.ScrollToTop();
            Refresh();
        }

        // ------------------------------------------------------------------ a table of the kit's rows

        // A card with a table in it: its heading, its column heads, and a row each, a press on a row when onRow is given. Returns the card's height.
        private float DataCard(float x, float y, float w, string title, UiDataTableModel t, int[] dots, Action<int> onRow)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            int columns = Mathf.Min(t.Columns.Length, 6);
            bool subs = false;
            for (int i = 0; i < t.RowCount; i++)
            {
                subs |= !string.IsNullOrEmpty(t.Rows[i].Sub);
            }
            float rowH = subs ? RowHeight + 32f : RowHeight;
            int empty = t.RowCount == 0 ? 1 : 0;
            float h = Pad * 2f + (string.IsNullOrEmpty(title) ? 0f : 62f) + HeadRow + t.RowCount * rowH + empty * 140f;
            page.Card(x, y, w, h, p.Card, Radius);
            float cy = y + Pad - 6f;
            if (!string.IsNullOrEmpty(title))
            {
                Heading(page, title, x + Pad, cy, w - Pad * 2f);
                cy += 62f;
            }
            float inner = w - Pad * 2f, fixedWidth = (columns - 1) * ColumnWide, firstWidth = Mathf.Max(260f, inner - fixedWidth);
            float[] at = new float[columns + 1];
            at[0] = x + Pad;
            at[1] = at[0] + firstWidth;
            for (int c = 2; c <= columns; c++)
            {
                at[c] = at[c - 1] + ColumnWide;
            }
            for (int c = 0; c < columns; c++)
            {
                float cw = c == 0 ? firstWidth : ColumnWide;
                page.Text(t.Columns[c].Title, at[c], cy, cw, HeadRow - 8f, SizeMicro, p.Muted, c == 0 ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight, true, false, true, 3f);
            }
            cy += HeadRow;
            if (t.RowCount == 0)
            {
                page.Text(t.EmptyTitle ?? string.Empty, x + Pad, cy + 24f, inner, 56f, SizeBody, p.Text, TextAlignmentOptions.TopLeft, true);
                page.Text(t.EmptyBody ?? string.Empty, x + Pad, cy + 72f, inner, 60f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft, false, true);
                return h;
            }
            for (int r = 0; r < t.RowCount; r++)
            {
                UiDataRow row = t.Rows[r];
                page.Box(x + Pad, cy, inner, 2f, p.Border, 1f);
                bool dot = dots != null && r < dots.Length;
                float lead = dot ? 36f : 0f;
                if (dot)
                {
                    page.Pill(at[0], cy + (rowH - 20f) * 0.5f, 20f, 20f, dots[r]);
                }
                float textY = subs && !string.IsNullOrEmpty(row.Sub) ? cy + 6f : cy, textH = subs && !string.IsNullOrEmpty(row.Sub) ? 52f : rowH;
                page.Text(Cell(row, 0), at[0] + lead, textY, firstWidth - lead - 12f, subs && !string.IsNullOrEmpty(row.Sub) ? textH : rowH, SizeBody, p.Text,
                    subs && !string.IsNullOrEmpty(row.Sub) ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.MidlineLeft, true);
                if (subs && !string.IsNullOrEmpty(row.Sub))
                {
                    page.Text(row.Sub, at[0] + lead, cy + 58f, firstWidth - lead - 12f, 34f, SizeMicro, p.Muted, TextAlignmentOptions.TopLeft);
                }
                for (int c = 1; c < columns; c++)
                {
                    page.Text(Cell(row, c), at[c], cy, ColumnWide - 12f, rowH, SizeBody, p.Text, TextAlignmentOptions.MidlineRight, false);
                }
                if (onRow != null)
                {
                    int pick = r;
                    page.Click(x + Pad / 2f, cy, w - Pad, rowH, () => onRow(pick));
                }
                cy += rowH;
            }
            return h;
        }

        private static string Cell(UiDataRow row, int column) => row.Cells != null && column < row.Cells.Length ? row.Cells[column] ?? string.Empty : string.Empty;

        // ------------------------------------------------------------------ the city's people page

        // A page of the people explorer: a district, a building, a household, a person, a business or who contributes, made by PopulationPages
        // from the same views the kit's page reads; a row or the one action opens the next, Back steps back the way it came.
        private float Browse(float y, float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            if (_source == null)
            {
                _source = PopulationLive.Source();
            }
            _view = _source.Open(_at) ?? _source.Open(ViewTarget.To(ViewKind.City, 0));
            string back = _way.Count > 0 ? OutlookText.T("reallife_ui_prompt_back") : OutlookText.T("reallife_news_people_title");
            y = BackRow(y, width, back, null);
            if (_view == null)
            {
                return Notice(y, width, "reallife_news_empty_city", "reallife_news_empty_city_body");
            }
            PopulationModel m = PopulationPages.Model(_view, UiText.Words, _source.Money, _source.NameOf, _source.Place, _browseName);
            if (m.Gone)
            {
                float tw = width - Pad * 2f - 120f;
                float bodyH = page.Fit(m.GoneBody ?? string.Empty, SizeBody, tw);
                float gh = Pad * 2f + 54f + bodyH + 12f;
                page.Card(Margin, y, width, gh, p.Card, Radius);
                page.Text(m.GoneTitle ?? string.Empty, Margin + Pad, y + Pad - 4f, tw, 54f, SizeCard, p.Text, TextAlignmentOptions.TopLeft, true);
                page.Text(m.GoneBody ?? string.Empty, Margin + Pad, y + Pad + 54f, tw, bodyH + 6f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft, false, true);
                return y + gh + Gap;
            }
            if (m.Figures.Length > 0)
            {
                y = FigureStrip(y, width, m.Figures) + Gap;
            }
            else if (m.Head != null)
            {
                y = HeadCard(y, width, m.Head) + Gap;
            }
            for (int s = 0; s < m.Sections.Length; s++)
            {
                if (m.Sections[s].Lines != null && m.Sections[s].Lines.Length > 0)
                {
                    y = PairsCard(y, width, m.Sections[s]) + Gap;
                }
            }
            if (m.HasAction)
            {
                float bw = page.Measure(m.Action.Label, SizeBody, true) + 96f;
                page.Pill(Margin, y, bw, 70f, p.Accent);
                page.Text(m.Action.Label, Margin, y, bw, 70f, SizeBody, p.AccentInk, TextAlignmentOptions.Midline, true);
                page.Click(Margin, y, bw, 70f, () => BrowseTo(_view.PrimaryTarget, null));
                y += 70f + Gap;
            }
            if (m.List.RowCount > 0 || m.List.Columns.Length > 0)
            {
                _list = m.List;
                y += DataCard(Margin, y, width, null, m.List, null, row => BrowseRow(row)) + Gap;
            }
            return y;
        }

        private void BrowseRow(int row)
        {
            if (_view != null && row >= 0 && row < _view.Rows.Count)
            {
                BrowseTo(_view.Rows[row].Target, PopulationRowName(row));
            }
        }

        // What the row called the place or person it opens: its first cell as written on the page.
        private string PopulationRowName(int row) => _list != null && row >= 0 && row < _list.RowCount ? Cell(_list.Rows[row], 0) : null;

        private void BrowseTo(ViewTarget to, string name)
        {
            if (!to.Valid)
            {
                return;
            }
            _way.Push(new BrowseStep { At = _at, Name = _browseName });
            _at = to;
            _browseName = name;
            _body.ScrollToTop();
            Refresh();
        }

        // The city's figures across, each a caption over its number.
        private float FigureStrip(float y, float width, UiFigureModel[] figures)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float h = Pad * 2f + 120f, cell = (width - Pad * 2f) / Mathf.Max(1, figures.Length);
            page.Card(Margin, y, width, h, p.Card, Radius);
            for (int i = 0; i < figures.Length; i++)
            {
                Caption(page, figures[i].Label, Margin + Pad + i * cell, y + Pad - 4f, cell - 20f);
                Value(page, figures[i].Value, Margin + Pad + i * cell, y + Pad + 30f, cell - 20f, p.Text, 56f);
            }
            return y + h;
        }

        // A place's or a person's head: its name, where it stands, and its key facts.
        private float HeadCard(float y, float width, UiProfileModel head)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            int facts = Mathf.Min(head.FactLabels.Length, head.FactValues.Length);
            bool role = !string.IsNullOrEmpty(head.Role);
            float h = Pad * 2f + 62f + (role ? 44f : 0f) + facts * 68f;
            page.Card(Margin, y, width, h, p.Card, Radius);
            Heading(page, head.Name ?? string.Empty, Margin + Pad, y + Pad - 6f, width - Pad * 2f);
            float cy = y + Pad + 62f;
            if (role)
            {
                page.Text(head.Role, Margin + Pad, cy - 6f, width - Pad * 2f, 44f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft);
                cy += 44f;
            }
            for (int i = 0; i < facts; i++)
            {
                cy += Pair(page, Margin + Pad, cy, width - Pad * 2f, head.FactLabels[i], head.FactValues[i], UiRole.None);
            }
            return y + h;
        }

        // A block of facts: its label in capitals over a key and a value to a row.
        private float PairsCard(float y, float width, PopulationSection section)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            float h = Pad * 2f + 56f + section.Lines.Length * 68f;
            page.Card(Margin, y, width, h, p.Card, Radius);
            Caption(page, section.Title, Margin + Pad, y + Pad - 4f, width - Pad * 2f);
            float cy = y + Pad + 52f;
            for (int i = 0; i < section.Lines.Length; i++)
            {
                cy += Pair(page, Margin + Pad, cy, width - Pad * 2f, section.Lines[i].Key, section.Lines[i].Value, UiRole.None);
            }
            return y + h;
        }
    }
}
