using System.Globalization;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The paper's page: the markets page and the share prices.</summary>
    public sealed partial class LedgerPage
    {
        private readonly int[] _starts = new int[PriceColumns], _ends = new int[PriceColumns];

        private void WriteMarkets(LedgerEdition e, Rules rules)
        {
            float u = _height;
            Margins(_pages[Markets], u, out float W, out float H);
            float top = FolioHead(_mTitle, _mName, _mDate, "reallife_ledger_tab_markets", Dateline(e), W, u, rules) + 0.02f * u;
            float lw = 0.53f * W, rx = lw + 0.022f * W, rw = W - rx;
            float gut = 0.025f * u;
            float cw = (lw - (MarketColumns - 1) * gut) / MarketColumns;

            // the left: the market's stories and the movers, in three columns under the lead's headline
            LedgerRules.Of(e, LedgerSection.Markets, null, _pieces);
            string headline, body;
            if (_pieces.Count > 0)
            {
                LedgerText.Words(_pieces[0], out _, out headline, out body);
                if (headline == null)
                {
                    FirstSentence(ref body, out headline);
                }
                _pieces.RemoveAt(0);
            }
            else
            {
                headline = LedgerText.T(e.HasMarket ? "reallife_ledger_market_quiet_head" : "reallife_ledger_no_market_head");
                body = LedgerText.T(e.HasMarket ? "reallife_ledger_market_quiet" : "reallife_ledger_no_market");
            }
            _also.Clear();
            _also.AddRange(_pieces);
            float colTop = Header(_mKicker, _mHead, null, _mByline, LedgerText.T(LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets]), headline, null,
                LedgerText.T(BylineKey(LedgerSection.Markets)), top, lw, 0.044f * u, u, true);
            float room = H - colTop;
            float s = MarketStart * u;
            _mColumns.Size(s);
            int capLines = _mColumns.CapLines(body, cw, out bool beside);
            string flow = MarketFlow(e, body, capLines, MoversLeast, cw, s);
            float need = Tall(_mColumns.Measure, flow, cw);
            s = Mathf.Clamp(s * Mathf.Sqrt(0.92f * MarketColumns * room / Mathf.Max(1f, need)), MarketLeast * u, MarketMost * u);
            _mColumns.Size(s);
            capLines = _mColumns.CapLines(body, cw, out beside);
            int most = MoversLeast;
            while (e.HasMarket && most < MoversMost)
            {
                string more = MarketFlow(e, body, capLines, most + 1, cw, s);
                if (Tall(_mColumns.Measure, more, cw) / MarketColumns > room * 0.9f)
                {
                    break;
                }
                most++;
            }
            flow = MarketFlow(e, body, capLines, most, cw, s);
            _mColumns.Lay(flow, 0f, cw, gut, colTop, room, capLines > 0 ? body.Substring(0, 1) : null, capLines, beside);
            _mColumns.Marks(rules, _px);
            _mColumns.Gutters(rules, gut, _px);
            rules.Down(lw + 0.011f * W, top, H - top, _px);

            // the right: the index, its chart and every sector
            float size = RightSize * u;
            _mIndex.fontSize = size;
            _b.Clear();
            _b.Append(MarkUnder).Append("<align=left>");
            Label(LedgerText.T("reallife_ledger_index"), "86%");
            _b.Append("</align>\n");
            if (e.HasMarket)
            {
                _b.Append("<size=200%><b>").Append(Level(e.IndexLevel)).Append("</b></size>   <size=120%>").Append(Move(e.IndexChangeBp)).Append("</size>").Append(VT);
                _b.Append("<i angle=12>").Append(Plain(LedgerText.Fill("reallife_ledger_week", "change", LedgerText.Change(e.IndexWeekBp)))).Append("</i>");
            }
            else
            {
                _b.Append(Soft(LedgerText.T("reallife_ledger_no_market")));
            }
            string index = Words();
            float ih = Tall(_mIndex, index, rw);
            Set(_mIndex, index);
            Put(_mIndex.rectTransform, rx, top - 0.003f * u, rw, ih + 2f);
            _mIndex.ForceMeshUpdate();
            Under(_mIndex, rules);
            float y = top - 0.003f * u + ih + 0.012f * u;
            bool sectors = e.HasMarket && e.Sectors.Count > 0;
            bool chart = e.HasMarket && e.IndexCloses.Count > 1;
            Show(_mChart, chart);
            Show(_mCaption, chart);
            Show(_mSectorsLabel, sectors);
            int per = (e.Sectors.Count + 1) / 2;
            float ts = size * 0.92f;
            float tableH = sectors ? per * Advance(ts, TableSpacing) + (_asc + _desc) * ts : 0f;
            float labelH = size * 1.9f;
            if (chart)
            {
                float caption = size * 1.6f;
                float ch = Mathf.Max(0.16f * u, H - y - caption - 0.012f * u - (sectors ? labelH + tableH + 0.01f * u : 0f));
                Put(_mChart.rectTransform, rx, y, rw, ch);
                _mChart.Show(e.IndexCloses, Ink, FaintInk, Mathf.Max(1.5f, _height * 0.0026f));
                y += ch + 0.003f * u;
                _mCaption.fontSize = size * 0.86f;
                Set(_mCaption, "<i angle=12>" + Plain(LedgerText.Fill("reallife_ledger_chart_caption", "days", e.IndexCloses.Count.ToString(CultureInfo.CurrentCulture))) + "</i>");
                Put(_mCaption.rectTransform, rx, y, rw, caption);
                y += caption + 0.012f * u;
            }
            for (int c = 0; c < _mSectors.Length; c++)
            {
                Show(_mSectors[c], sectors);
            }
            if (!sectors)
            {
                return;
            }
            _mSectorsLabel.fontSize = size;
            _b.Clear();
            _b.Append(MarkUnder).Append("<align=left>");
            Label(LedgerText.T("reallife_ledger_sectors"), "86%");
            _b.Append("</align>");
            Set(_mSectorsLabel, Words());
            Put(_mSectorsLabel.rectTransform, rx, y, rw, labelH);
            _mSectorsLabel.ForceMeshUpdate();
            Under(_mSectorsLabel, rules);
            y += labelH;
            float half = (rw - gut) * 0.5f;
            float rowsH = Mathf.Max(1f, H - y);
            ts = Mathf.Min(ts, rowsH / Mathf.Max(1f, per * Advance(1f, TableSpacing) + _asc + _desc));
            for (int c = 0; c < _mSectors.Length; c++)
            {
                TMP_Text t = _mSectors[c];
                t.fontSize = ts;
                _b.Clear();
                for (int i = c * per; i < Mathf.Min(e.Sectors.Count, (c + 1) * per); i++)
                {
                    LedgerSector sector = e.Sectors[i];
                    if (i > c * per)
                    {
                        _b.Append(VT);
                    }
                    Row(LedgerText.T(sector.Key), string.Empty, half, ts, t, Level(sector.Level), sector.ChangeBp, true);
                }
                Set(t, Words());
                Put(t.rectTransform, rx + c * (half + gut), y, half, rowsH);
            }
            rules.Down(rx + half + gut * 0.5f, y, rowsH, _px);
        }

        // The markets page's columns: the lead's text, the market's other stories, the movers and the bank's figures.
        private string MarketFlow(LedgerEdition e, string body, int capLines, int movers, float width, float size)
        {
            _b.Clear();
            if (!string.IsNullOrEmpty(body))
            {
                _b.Append(capLines > 0 ? AfterCap(body) : Plain(body)).Append('\n');
            }
            Section(LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets], _also, MarketStories, false);
            if (e.HasMarket)
            {
                _b.Append(MarkRule).Append(MarkKeep).Append("<align=left>");
                Label(LedgerText.T("reallife_ledger_movers"), "84%");
                _b.Append("</align>").Append(VT);
                bool first = true;
                for (int g = 0; g < 3; g++)
                {
                    if (g < 2)
                    {
                        LedgerRules.Movers(e, g == 0, movers, _quotes);
                    }
                    else
                    {
                        LedgerRules.MostActive(e, movers, _quotes);
                    }
                    if (_quotes.Count == 0)
                    {
                        continue;
                    }
                    if (!first)
                    {
                        _b.Append('\n');
                    }
                    string group = g == 0 ? "reallife_ledger_risers" : g == 1 ? "reallife_ledger_fallers" : "reallife_ledger_active";
                    _b.Append("<align=left><b>").Append(Plain(LedgerText.T(group))).Append("</b></align>").Append(VT);
                    if (first)
                    {
                        _b.Append(MarkEnd);
                        first = false;
                    }
                    for (int i = 0; i < _quotes.Count; i++)
                    {
                        if (i > 0)
                        {
                            _b.Append(VT);
                        }
                        LedgerQuote q = _quotes[i];
                        if (g < 2)
                        {
                            Row(q.Name, string.Empty, width, size, _mColumns.Measure, null, q.ChangeBp, true);
                        }
                        else
                        {
                            Row(q.Name, string.Empty, width, size, _mColumns.Measure, LedgerText.Figure(q.Close), 0, false);
                        }
                    }
                }
                _b.Append('\n');
                Rates(e);
            }
            return Words();
        }

        // The head of an inside page: the paper's name, the page's, the rules and the dateline; returns where it ends.
        private float FolioHead(TMP_Text title, TMP_Text name, TMP_Text date, string nameKey, string dateline, float W, float u, Rules rules)
        {
            float fh = 0.072f * u;
            title.fontSize = 0.046f * u;
            title.characterSpacing = 5f;
            Set(title, Plain(LedgerText.T("reallife_ledger_masthead")));
            Put(title.rectTransform, 0f, TopFor(fh, title.fontSize), 0.5f * W, fh);
            name.fontSize = 0.03f * u;
            Set(name, "<b><smallcaps><cspace=0.12em>" + Plain(LedgerText.T(nameKey)) + "</cspace></smallcaps></b>");
            Put(name.rectTransform, 0.5f * W, TopFor(fh, name.fontSize), 0.5f * W, fh);
            rules.Across(0f, fh + 0.0015f * u, W, 3f * _px);
            rules.Across(0f, fh + 0.007f * u, W, _px);
            float dy = fh + 0.007f * u, dh = 0.031f * u;
            Dateline(date, dateline, 0f, dy, W, dh, 0.0148f * u);
            rules.Across(0f, dy + dh, W, _px);
            return dy + dh;
        }

        private string Dateline(LedgerEdition e)
        {
            return e.HasMarket
                ? LedgerText.Fill("reallife_ledger_dateline_market", "weekday", Weekday(e.Day), "day", e.Day.ToString(CultureInfo.CurrentCulture),
                    "session", e.MarketDay.ToString(CultureInfo.CurrentCulture))
                : LedgerText.Fill("reallife_ledger_dateline", "weekday", Weekday(e.Day), "day", e.Day.ToString(CultureInfo.CurrentCulture));
        }

        private void WritePrices(LedgerEdition e, Rules rules)
        {
            float u = _height;
            Margins(_pages[Prices], u, out float W, out float H);
            float top = FolioHead(_pTitle, _pName, _pDate, "reallife_ledger_tab_prices", Dateline(e), W, u, rules);
            float ls = 0.0142f * u;
            _pLegend.fontSize = ls;
            Set(_pLegend, "<i angle=12>" + Plain(LedgerText.T("reallife_ledger_prices_legend")) + "</i>");
            Put(_pLegend.rectTransform, 0f, top + 0.006f * u, W, ls * 1.7f);
            top += ls * 1.7f + 0.012f * u;
            rules.Across(0f, top, W, _px);
            top += 0.012f * u;
            float gut = 0.022f * u;
            float cw = (W - (PriceColumns - 1) * gut) / PriceColumns;
            float hgt = H - top;

            int count = 0;
            if (e.HasMarket)
            {
                LedgerRules.Listing(e, _quotes);
                count = _quotes.Count;
            }
            if (count == 0)
            {
                _pColumns[0].fontSize = 0.018f * u;
                Set(_pColumns[0], Soft(LedgerText.T("reallife_ledger_no_market")));
                Put(_pColumns[0].rectTransform, 0f, top, W, hgt);
                for (int i = 1; i < _pColumns.Length; i++)
                {
                    Set(_pColumns[i], string.Empty);
                }
                return;
            }
            // The rows: a sector's name over each sector's companies; five columns of as many rows each, a sector's
            // name never left at the foot of a column without its first company.
            int rows = 0, sector = -1;
            for (int i = 0; i < count; i++)
            {
                if (_quotes[i].Sector != sector)
                {
                    sector = _quotes[i].Sector;
                    rows++;
                }
                rows++;
            }
            int per = (rows + PriceColumns - 1) / PriceColumns;
            Split(per, _starts, _ends);
            // one size for every column: the deepest column's rows fill the height
            float trial = 0.02f * u, deepest = 1f;
            for (int c = 0; c < PriceColumns; c++)
            {
                string words = PriceColumn(e, _starts[c], _ends[c], cw, trial, null);   // the depth only: the names are not measured yet
                _pColumns[c].fontSize = trial;
                deepest = Mathf.Max(deepest, Tall(_pColumns[c], words, cw));
            }
            float s = Mathf.Min(PricesMost * u, trial * hgt / deepest * 0.99f);
            for (int c = 0; c < PriceColumns; c++)
            {
                TMP_Text t = _pColumns[c];
                t.fontSize = s;
                string words = PriceColumn(e, _starts[c], _ends[c], cw, s, t);
                Set(t, words);
                float x = c * (cw + gut);
                Put(t.rectTransform, x, top, cw, hgt);
                t.ForceMeshUpdate();
                Under(t, rules);
                if (c > 0)
                {
                    rules.Down(x - gut * 0.5f, top, hgt, _px);
                }
            }
        }

        // Which quotes each column has, so many rows to a column, a sector's name counting as one.
        private void Split(int per, int[] starts, int[] ends)
        {
            int q = 0, sector = -1;
            for (int c = 0; c < PriceColumns; c++)
            {
                starts[c] = q;
                int used = 0;
                while (q < _quotes.Count)
                {
                    bool head = _quotes[q].Sector != sector;
                    int cost = head ? 2 : 1;
                    if (c < PriceColumns - 1 && used + cost > per && used > 0)
                    {
                        break;
                    }
                    used += cost;
                    sector = _quotes[q].Sector;
                    q++;
                }
                ends[c] = q;
                if (c < PriceColumns - 1 && q < _quotes.Count)
                {
                    sector = -1;   // a column that starts in a sector's middle repeats its name
                }
            }
        }

        // A column of the share prices: each sector's name on a rule, then its companies, the figures right-aligned.
        private string PriceColumn(LedgerEdition e, int from, int to, float width, float size, TMP_Text measure)
        {
            _b.Clear();
            int sector = -1;
            for (int i = from; i < to; i++)
            {
                LedgerQuote q = _quotes[i];
                if (q.Sector != sector)
                {
                    if (i > from)
                    {
                        _b.Append('\n');
                    }
                    sector = q.Sector;
                    _b.Append(MarkUnder).Append("<align=left><b><size=92%><smallcaps><cspace=0.1em>")
                      .Append(Plain(LedgerText.T(sector < e.Sectors.Count ? e.Sectors[sector].Key : string.Empty))).Append("</cspace></smallcaps></size></b></align>");
                }
                _b.Append(VT);
                Row(q.Name, Marks(q.Flags), width, size, measure, LedgerText.Figure(q.Close), q.ChangeBp, true);
            }
            return Words();
        }
    }
}
