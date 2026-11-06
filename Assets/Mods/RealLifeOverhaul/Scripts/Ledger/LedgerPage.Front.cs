using System.Globalization;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The paper's page: the front page.</summary>
    public sealed partial class LedgerPage
    {
        private void WriteFront(LedgerEdition e, Rules rules)
        {
            float u = _height;
            Margins(_pages[Front], u, out float W, out float H);

            float mh = 0.122f * u;
            _fTitle.fontSize = 0.083f * u;
            _fTitle.characterSpacing = 6f;
            Set(_fTitle, Plain(LedgerText.T("reallife_ledger_masthead")));
            Put(_fTitle.rectTransform, 0.2f * W, TopFor(mh, _fTitle.fontSize), 0.6f * W, mh);

            LedgerRules.Of(e, LedgerSection.Weather, null, _pieces);
            string weather = _pieces.Count > 0 ? Body(_pieces[0]) : LedgerText.T("reallife_ledger_no_weather");
            int stop = weather.IndexOf(". ", System.StringComparison.Ordinal);
            if (stop > 0)
            {
                weather = weather.Substring(0, stop + 1);   // the ear has the first sentence; the box has it all
            }
            _b.Clear();
            Label(LedgerText.T("reallife_ledger_ear_weather"), "80%");
            _b.Append(VT).Append("<i angle=12>").Append(Plain(weather)).Append("</i>");
            Ear(_fEarLeft, _b.ToString(), 0f, W, mh, u, rules);
            _b.Clear();
            Label(LedgerText.T("reallife_ledger_ear_index"), "80%");
            _b.Append(VT);
            if (e.HasMarket)
            {
                _b.Append("<b>").Append(Level(e.IndexLevel)).Append("</b>  ").Append(Move(e.IndexChangeBp));
            }
            else
            {
                _b.Append("<i angle=12>").Append(Plain(LedgerText.T("reallife_ledger_no_session"))).Append("</i>");
            }
            Ear(_fEarRight, _b.ToString(), W - 0.148f * W, W, mh, u, rules);

            rules.Across(0f, mh + 0.0015f * u, W, 3f * _px);
            rules.Across(0f, mh + 0.007f * u, W, _px);
            float dy = mh + 0.007f * u, dh = 0.031f * u, ds = 0.0148f * u;
            Dateline(_fDateLeft, LedgerText.Fill("reallife_ledger_dateline", "weekday", Weekday(e.Day), "day", e.Day.ToString(CultureInfo.CurrentCulture)), 0f, dy, W, dh, ds);
            Dateline(_fDateMiddle, LedgerText.Fill("reallife_ledger_dateline_number", "year", Year(e.Day).ToString(CultureInfo.CurrentCulture),
                "number", e.Day.ToString(CultureInfo.CurrentCulture)), 0f, dy, W, dh, ds);
            Dateline(_fDateRight, LedgerText.Fill("reallife_ledger_dateline_price", "price", LedgerText.Money(_director.CopyPrice)), 0f, dy, W, dh, ds);
            rules.Across(0f, dy + dh, W, _px);
            float bt = dy + dh + 0.018f * u;

            float mw = 0.755f * W, bx = mw + 0.017f * W, bw = W - bx;
            bool chart = e.HasMarket && e.IndexCloses.Count > 1;
            bool aroundInColumns = e.HasMarket;   // without the market the box has the room for around town
            SideBox(e, bx, bt, bw, H - bt, u, aroundInColumns, rules);

            LedgerPiece lead = LedgerRules.Lead(e);
            string kicker, headline, body;
            if (lead != null)
            {
                LedgerText.Words(lead, out kicker, out headline, out body);
                if (headline == null)
                {
                    FirstSentence(ref body, out headline);
                }
                kicker = kicker != null ? Sentence(kicker) : LedgerText.T(LedgerCatalogue.SectionKeys[(int)lead.Section]);
            }
            else
            {
                kicker = LedgerText.T(LedgerCatalogue.SectionKeys[(int)LedgerSection.City]);
                headline = LedgerText.T("reallife_ledger_quiet_head");
                body = LedgerText.T("reallife_ledger_quiet_body");
            }
            string byline = LedgerText.T(BylineKey(lead != null ? lead.Section : LedgerSection.City));
            float gut = 0.025f * u;
            float cw = (mw - (FrontColumns - 1) * gut) / FrontColumns;

            float s = BodyStart * u, hs = HeadStart * u;
            Lead lw = LeadWords(_fColumns, body, s, cw);
            string flow = FrontFlow(e, lead, lw, aroundInColumns);
            float top = Header(kicker, headline, lw.Stand, byline, bt, mw, hs, u, false);
            float need = Tall(_fColumns.Measure, flow, cw);
            s = Mathf.Clamp(s * Mathf.Sqrt(0.94f * FrontColumns * (H - top) / Mathf.Max(1f, need)), BodyLeast * u, BodyMost * u);
            bool band = false;
            for (int pass = 0; pass < 6; pass++)
            {
                lw = LeadWords(_fColumns, body, s, cw);
                flow = FrontFlow(e, lead, lw, aroundInColumns);
                top = Header(kicker, headline, lw.Stand, byline, bt, mw, hs, u, false);
                float adv = Advance(s, BodySpacing);
                need = Tall(_fColumns.Measure, flow, cw);
                float depth = Mathf.Ceil(need / FrontColumns / adv + 0.5f) * adv;
                float left = H - top - depth;
                band = left >= (BandLeast + BandGap) * u;
                float spare = left - (band ? (BandGap + BandMost) * u : 0f);
                if (spare <= 0.01f * u || hs >= HeadMost * u)
                {
                    break;
                }
                hs = Mathf.Min(HeadMost * u, hs + spare * 0.5f);   // a short paper: a larger headline
            }
            top = Header(kicker, headline, lw.Stand, byline, bt, mw, hs, u, true);
            float most = H - top - (band ? (BandLeast + BandGap) * u : 0f);
            _fColumns.Lay(flow, 0f, cw, gut, top, most, lw.Cap, lw.CapLines, lw.EndsBeside);
            // a paper too full for its columns even in the least type: around town (the lightest news, last) is cut
            // short until the rest fits, rather than a story stopping in the middle
            int briefs = aroundInColumns ? _also.Count : 0;
            while (!_fColumns.Fitted && briefs > 0)
            {
                briefs = briefs > 3 ? briefs / 2 : briefs - 1;
                flow = FrontFlow(e, lead, lw, aroundInColumns, briefs);
                _fColumns.Lay(flow, 0f, cw, gut, top, most, lw.Cap, lw.CapLines, lw.EndsBeside);
            }
            _fColumns.Marks(rules, _px);
            _fColumns.Gutters(rules, gut, _px);

            float below = top + _fColumns.Depth + BandGap * u * 0.6f;
            bool drawn = band && chart;
            Show(_fBand, drawn);
            Show(_fCaption, drawn);
            Show(_fChart, drawn);
            Show(_fAd, band && !chart);
            if (drawn)
            {
                IndexBand(e, 0f, below, mw, H - below, u, rules);
            }
            else if (band)
            {
                HouseAdvert(0f, below, mw, H - below, u, rules);
            }
        }

        // An ear of the masthead: a little framed box, its words in the middle.
        private void Ear(TMP_Text t, string words, float x, float W, float mh, float u, Rules rules)
        {
            float w = 0.148f * W, pad = 0.014f * u;
            t.fontSize = 0.0172f * u;
            t.lineSpacing = -6f;
            float h = Tall(t, words, w - 2f * pad);
            float y = (mh - h) * 0.5f;
            Set(t, words);
            Put(t.rectTransform, x + pad, y, w - 2f * pad, h + 4f);
            rules.Frame(x, y - 0.01f * u, w, h + 0.0185f * u, _px);
        }

        // The lead's words: its standfirst (the first sentence, if the rest fills two lines by the cap), then the rest.
        private struct Lead
        {
            public string Stand, Text, Cap;
            public int CapLines;
            public bool EndsBeside;
        }

        private Lead LeadWords(Columns columns, string body, float s, float cw)
        {
            var lw = new Lead { Text = body ?? string.Empty };
            columns.Size(s);
            int stop = lw.Text.IndexOf(". ", System.StringComparison.Ordinal);
            if (stop > 0 && columns.CapLines(lw.Text.Substring(stop + 2), cw, out _) >= 2)
            {
                lw.Stand = lw.Text.Substring(0, stop + 1);
                lw.Text = lw.Text.Substring(stop + 2);
            }
            lw.CapLines = columns.CapLines(lw.Text, cw, out lw.EndsBeside);
            lw.Cap = lw.CapLines > 0 ? lw.Text.Substring(0, 1) : null;
            return lw;
        }

        private float Header(string kicker, string headline, string stand, string byline, float bt, float mw, float hs, float u, bool place)
        {
            return Header(_fKicker, _fHead, _fStand, _fByline, kicker, headline, stand, byline, bt, mw, hs, u, place);
        }

        // The front page's columns: the lead's text, the rest of its section, the other news, around town last.
        private string FrontFlow(LedgerEdition e, LedgerPiece lead, Lead lw, bool around, int aroundMost = int.MaxValue)
        {
            _b.Clear();
            if (lw.CapLines > 0)
            {
                _b.Append(AfterCap(lw.Text)).Append('\n');
            }
            else if (lw.Text.Length > 0)
            {
                _b.Append(Plain(lw.Text)).Append('\n');
            }
            LedgerSection own = lead != null ? lead.Section : LedgerSection.City;
            LedgerSection[] news = { LedgerSection.City, LedgerSection.Economy, LedgerSection.Business };
            if (own > LedgerSection.Business)
            {
                own = LedgerSection.Count;   // a markets lead: every section of the front page has its label
            }
            for (int i = 0; i < news.Length; i++)
            {
                LedgerRules.Of(e, news[i], lead, _pieces);
                Section(LedgerCatalogue.SectionKeys[(int)news[i]], _pieces, StoriesMost, news[i] != own);
            }
            if (around)
            {
                Around(e);
                if (_also.Count > aroundMost)
                {
                    _also.RemoveRange(aroundMost, _also.Count - aroundMost);
                }
                Section(LedgerCatalogue.SectionKeys[(int)LedgerSection.AroundTown], _also, 0, true);
            }
            return Words();
        }

        // People and around town, as one section's briefs (in _also; _pieces is used on the way).
        private void Around(LedgerEdition e)
        {
            LedgerRules.Of(e, LedgerSection.People, null, _also);
            LedgerRules.Of(e, LedgerSection.AroundTown, null, _pieces);
            _also.AddRange(_pieces);
        }

        // The box down the side: the markets, the weather, then what is inside (or, without the market, around town).
        private void SideBox(LedgerEdition e, float x, float y, float w, float h, float u, bool around, Rules rules)
        {
            float pad = 0.014f * u, size = BoxSize * u;
            float iw = w - 2f * pad, ih = h - 1.4f * pad;
            _fBox.fontSize = size;
            string words = null;
            for (int movers = BoxMoversMost; movers >= BoxMoversLeast; movers--)
            {
                words = BoxWords(e, movers, iw, size, around);
                if (Tall(_fBox, words, iw) <= ih)
                {
                    break;
                }
            }
            Set(_fBox, words);
            Put(_fBox.rectTransform, x + pad, y + 0.5f * pad, iw, h - pad);
            _fBox.ForceMeshUpdate();
            Under(_fBox, rules);
            rules.Frame(x, y - 0.003f * u, w, h + 0.003f * u, _px);
        }

        private string BoxWords(LedgerEdition e, int movers, float width, float size, bool around)
        {
            _b.Clear();
            BoxLabel(LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets]);
            if (e.HasMarket)
            {
                _b.Append("<size=80%><smallcaps><cspace=0.12em>").Append(Plain(LedgerText.T("reallife_ledger_ear_index"))).Append("</cspace></smallcaps></size>").Append(VT);
                _b.Append("<size=170%><b>").Append(Level(e.IndexLevel)).Append("</b></size>   <size=105%>").Append(Move(e.IndexChangeBp)).Append("</size>").Append(VT);
                _b.Append("<i angle=12>").Append(Plain(LedgerText.Fill("reallife_ledger_week", "change", LedgerText.Change(e.IndexWeekBp)))).Append("</i>\n");
                Movers(e, true, movers, "reallife_ledger_risers", width, size, _fBox);
                Movers(e, false, movers, "reallife_ledger_fallers", width, size, _fBox);
            }
            else
            {
                _b.Append(Soft(LedgerText.T("reallife_ledger_no_market"))).Append('\n');
            }
            if (Rates(e))
            {
                _b.Append('\n');
            }
            BoxLabel(LedgerCatalogue.SectionKeys[(int)LedgerSection.Weather]);
            LedgerRules.Of(e, LedgerSection.Weather, null, _pieces);
            if (_pieces.Count == 0)
            {
                _b.Append(Soft(LedgerText.T("reallife_ledger_no_weather"))).Append('\n');
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                LedgerText.Words(_pieces[i], out string kicker, out _, out _);
                if (kicker != null && !string.Equals(Sentence(kicker), LedgerText.T("reallife_ledger_ear_weather"), System.StringComparison.OrdinalIgnoreCase))
                {
                    _b.Append("<b>").Append(Plain(Sentence(kicker))).Append(":</b> ");   // an outlook, not the day's weather
                }
                _b.Append(Plain(Body(_pieces[i]))).Append('\n');
            }
            if (around)
            {
                BoxLabel("reallife_ledger_inside");
                LedgerRules.Of(e, LedgerSection.Markets, null, _pieces);
                string markets = e.HasMarket
                    ? (_pieces.Count > 0 ? MarketHead(_pieces[0]) : LedgerText.T("reallife_ledger_market_quiet_head"))
                    : LedgerText.T("reallife_ledger_no_market_head");
                _b.Append("<b>").Append(Plain(LedgerText.T("reallife_ledger_inside_markets"))).Append("</b> <i angle=12>").Append(Plain(markets)).Append("</i>\n");
                _b.Append("<b>").Append(Plain(LedgerText.T("reallife_ledger_inside_prices"))).Append("</b> ")
                  .Append(Plain(e.HasMarket
                      ? LedgerText.Fill("reallife_ledger_inside_prices_body", "companies", e.Quotes.Count.ToString(CultureInfo.CurrentCulture),
                          "sectors", e.Sectors.Count.ToString(CultureInfo.CurrentCulture))
                      : LedgerText.T("reallife_ledger_no_market"))).Append('\n');
            }
            else
            {
                Around(e);
                if (_also.Count > 0)
                {
                    BoxLabel(LedgerCatalogue.SectionKeys[(int)LedgerSection.AroundTown]);
                    for (int i = 0; i < _also.Count; i++)
                    {
                        Brief(_also[i]);
                    }
                }
            }
            return Words();
        }

        private void BoxLabel(string key)
        {
            _b.Append(MarkUnder).Append("<align=left>");
            Label(LedgerText.T(key), "86%");
            _b.Append("</align>\n");
        }

        // The risers or the fallers as a little table: the name, and the move right-aligned with its mark.
        private void Movers(LedgerEdition e, bool up, int most, string titleKey, float width, float size, TMP_Text measure)
        {
            LedgerRules.Movers(e, up, most, _quotes);
            if (_quotes.Count == 0)
            {
                return;
            }
            _b.Append("<align=left><b>").Append(Plain(LedgerText.T(titleKey))).Append("</b></align>");
            for (int i = 0; i < _quotes.Count; i++)
            {
                _b.Append(VT);
                Row(_quotes[i].Name, string.Empty, width, size, measure, null, _quotes[i].ChangeBp, true);
            }
            _b.Append('\n');
        }

        // The index's last sessions under the columns, with its level over them and a caption under.
        private void IndexBand(LedgerEdition e, float x, float y, float w, float h, float u, Rules rules)
        {
            rules.Across(x, y, w, _px);
            float size = 0.0168f * u;
            _fBand.fontSize = size;
            _b.Clear();
            Label(LedgerText.T("reallife_ledger_index"), "86%");
            _b.Append("    <size=130%><b>").Append(Level(e.IndexLevel)).Append("</b></size>  ").Append(Move(e.IndexChangeBp))
              .Append("    <i angle=12>").Append(Plain(LedgerText.Fill("reallife_ledger_week", "change", LedgerText.Change(e.IndexWeekBp)))).Append("</i>");
            Set(_fBand, Words());
            Put(_fBand.rectTransform, x, y + 0.009f * u, w, size * 2.2f);
            float cy = y + 0.009f * u + size * 2.1f, caption = size * 1.7f;
            float ch = Mathf.Max(0.05f * u, h - (cy - y) - caption);
            Put(_fChart.rectTransform, x, cy, w, ch);
            _fChart.Show(e.IndexCloses, Ink, FaintInk, Mathf.Max(1.5f, _height * 0.0024f));
            _fCaption.fontSize = size * 0.86f;
            Set(_fCaption, "<i angle=12>" + Plain(LedgerText.Fill("reallife_ledger_chart_caption", "days", e.IndexCloses.Count.ToString(CultureInfo.CurrentCulture))) + "</i>");
            Put(_fCaption.rectTransform, x, cy + ch + 0.002f * u, w, caption);
        }

        // Without the market's chart, the room a short paper leaves holds the paper's own notice, framed.
        private void HouseAdvert(float x, float y, float w, float h, float u, Rules rules)
        {
            float bw = 0.7f * w, size = 0.026f * u;
            _fAd.fontSize = size;
            string words = "<size=170%><b><smallcaps><cspace=0.1em>" + Plain(LedgerText.T("reallife_ledger_ad_head")) + "</cspace></smallcaps></b></size>" + VT
                           + Plain(LedgerText.Fill(LedgerCatalogue.AdBodyKey(Settings.LedgerKiosks), "price", LedgerText.Money(_director.WeekPrice)));
            float th = Tall(_fAd, words, bw - 0.08f * u);
            float bh = Mathf.Max(th + 0.05f * u, h - 0.03f * u);
            float bx = x + (w - bw) * 0.5f, by = y + Mathf.Max(0f, h - bh) * 0.5f;
            Set(_fAd, words);
            Put(_fAd.rectTransform, bx + 0.04f * u, by + (bh - th) * 0.5f, bw - 0.08f * u, th + 2f);
            rules.Frame(bx, by, bw, bh, 2f * _px);
            rules.Frame(bx + 0.006f * u, by + 0.006f * u, bw - 0.012f * u, bh - 0.012f * u, _px);
        }

        private static string MarketHead(LedgerPiece piece)
        {
            LedgerText.Words(piece, out _, out string headline, out string body);
            if (headline == null)
            {
                FirstSentence(ref body, out headline);
            }
            return headline;
        }

        private static int Year(int day)
        {
            int perYear = Mathf.Max(1, GameCalendar.DaysPerYear);
            return (Mathf.Max(1, day) - 1) / perYear + 1;
        }
    }
}
