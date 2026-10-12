using System.Collections.Generic;
using System.Globalization;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    public sealed partial class LedgerPage
    {
        // ---- the type, in fractions of the sheet's height (on a 1080p screen the sheet is some 1400 canvas units tall)
        private const float MarginX = 0.034f, MarginY = 0.02f;
        private const float BodyStart = 0.0205f, BodyLeast = 0.019f, BodyMost = 0.0255f;          // the front page's columns
        private const float MarketStart = 0.0195f, MarketLeast = 0.0175f, MarketMost = 0.0245f;   // the markets page's
        private const float HeadStart = 0.058f, HeadMost = 0.08f, HeadLeast = 0.046f;             // the lead's headline
        private const float BoxSize = 0.0168f, RightSize = 0.019f, PricesMost = 0.022f;
        private const float BandLeast = 0.13f, BandMost = 0.3f, BandGap = 0.02f;                   // the index's chart under the columns
        // ---- spacing, in em/100 as TextMeshPro takes it: lines a little closer than the face's own, items a paragraph apart
        private const float BodySpacing = -14f, HeadSpacing = -24f, ItemSpacing = 85f, TableSpacing = -18f;
        private const float Spread = 0.15f;     // the part of a justified line's slack that goes between letters, the rest between words
        private const float MarkSize = 0.6f;    // the up and down marks, at this much of the type they stand in
        private const int FrontColumns = 4, MarketColumns = 3, PriceColumns = 5;
        private const int StoriesMost = 3;      // stories under a headline in a section of the front page; the rest are briefs
        private const int MarketStories = 9;
        private const int BoxMoversMost = 6, BoxMoversLeast = 3, MoversLeast = 5, MoversMost = 12;
        private const char VT = '\v';           // a line break inside an item: no paragraph spacing before the next line

        private const string Up = "\u25B2", Down = "\u25BC";

        // ================================================================ the front page

        private void WriteFront(LedgerEdition e, Rules rules)
        {
            float u = _height;
            Margins(_pages[Front], u, out float W, out float H);

            // ---- the masthead between its ears, the double rule, the dateline
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
            Dateline(_fDateMiddle, LedgerText.Fill("reallife_ledger_dateline_number", "year", Year(e.Day).ToString(CultureInfo.CurrentCulture), "number", e.Day.ToString(CultureInfo.CurrentCulture)), 0f, dy, W, dh, ds);
            Dateline(_fDateRight, LedgerText.Fill("reallife_ledger_dateline_price", "price", LedgerText.Money(_director.CopyPrice)), 0f, dy, W, dh, ds);
            rules.Across(0f, dy + dh, W, _px);
            float bt = dy + dh + 0.018f * u;

            // ---- the main area, and the box down the side
            float mw = 0.755f * W, bx = mw + 0.017f * W, bw = W - bx;
            bool chart = e.HasMarket && e.IndexCloses.Count > 1;
            bool aroundInColumns = e.HasMarket;   // without the market the box has the room for around town
            SideBox(e, bx, bt, bw, H - bt, u, aroundInColumns, rules);

            // ---- the lead: its words, its head's sizes and the columns' type chosen together
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
            // a paper too full for its columns even in the least type: around town (the lightest news, last) is cut short
            // until the rest fits, rather than a story stopping in the middle
            int briefs = aroundInColumns ? _also.Count : 0;
            while (!_fColumns.Fitted && briefs > 0)
            {
                briefs = briefs > 3 ? briefs / 2 : briefs - 1;
                flow = FrontFlow(e, lead, lw, aroundInColumns, briefs);
                _fColumns.Lay(flow, 0f, cw, gut, top, most, lw.Cap, lw.CapLines, lw.EndsBeside);
            }
            _fColumns.Marks(rules, _px);
            _fColumns.Gutters(rules, gut, _px);

            // ---- under the columns, what a short paper leaves: the index's last sessions, or the paper's own notice
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

        private void Dateline(TMP_Text t, string words, float x, float y, float w, float h, float size)
        {
            t.fontSize = size;
            Set(t, "<smallcaps><cspace=0.06em>" + Plain(words) + "</cspace></smallcaps>");
            Put(t.rectTransform, x, y + TopFor(h, size), w, h);
        }

        // The lead's words in the columns: the standfirst (its first sentence, when what is left still fills two lines
        // beside the drop cap), the text that follows and the cap.
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

        // The lead's head across the main area (kicker, headline in two lines at most, standfirst, byline): placed, or only
        // measured. Returns where the columns start.
        private float Header(string kicker, string headline, string stand, string byline, float bt, float mw, float hs, float u, bool place)
        {
            return Header(_fKicker, _fHead, _fStand, _fByline, kicker, headline, stand, byline, bt, mw, hs, u, place);
        }

        private float Header(TMP_Text kickerText, TMP_Text headText, TMP_Text standText, TMP_Text bylineText, string kicker, string headline, string stand, string byline,
            float bt, float mw, float hs, float u, bool place)
        {
            float y = bt - 0.003f * u;
            float ks = 0.0148f * u;
            kickerText.fontSize = ks;
            if (place)
            {
                Set(kickerText, "<b><smallcaps><cspace=0.16em>" + Plain(kicker) + "</cspace></smallcaps></b>");
                Put(kickerText.rectTransform, 0f, y, mw, ks * 1.6f);
            }
            y += ks * (_asc + _desc) - 0.004f * u;
            string head = Plain(headline);
            float size = hs, least = Mathf.Min(HeadLeast * u, hs * 0.78f);
            while (size > least && Lines(headText, head, mw, size, HeadSpacing) > 2)
            {
                size -= 0.002f * u;
            }
            headText.fontSize = size;
            float h = Tall(headText, head, mw);
            if (place)
            {
                Set(headText, head);
                Put(headText.rectTransform, 0f, y, mw, h + 2f);
            }
            y += h + 0.005f * u;
            if (standText != null)
            {
                bool any = !string.IsNullOrEmpty(stand);
                if (any)
                {
                    standText.fontSize = 0.0232f * u * Mathf.Sqrt(size / (HeadStart * u));
                    string words = "<i angle=12>" + Plain(stand) + "</i>";
                    float sh = Tall(standText, words, mw * 0.7f);
                    if (place)
                    {
                        Set(standText, words);
                        Put(standText.rectTransform, 0f, y, mw * 0.7f, sh + 2f);
                    }
                    y += sh + 0.003f * u;
                }
                if (place)
                {
                    Show(standText, any);
                }
            }
            float bs = 0.0136f * u;
            bylineText.fontSize = bs;
            if (place)
            {
                Set(bylineText, "<smallcaps><cspace=0.12em>" + Plain(byline) + "</cspace></smallcaps>");
                Put(bylineText.rectTransform, 0f, y, mw, bs * 1.6f);
            }
            return y + bs * (_asc + _desc) + 0.011f * u;
        }

        // The front page's columns: the lead's text after its drop cap, the rest of its section, the other news sections
        // under their labels, around town last. An empty section is left out: the next takes its room.
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

        // A section in columns: its label over a rule (none when it goes on from the lead), its stories (a kicker, a
        // headline, the text; at most so many) and its briefs (a bold lead into the text).
        private void Section(string labelKey, List<LedgerPiece> pieces, int most, bool label)
        {
            if (pieces.Count == 0)
            {
                return;
            }
            if (label)
            {
                _b.Append(MarkRule).Append(MarkKeep).Append("<align=left>");
                Label(LedgerText.T(labelKey), "84%");
                _b.Append("</align>").Append(VT);
            }
            int told = 0;
            string name = LedgerText.T(labelKey);
            for (int i = 0; i < pieces.Count; i++)
            {
                LedgerText.Words(pieces[i], out string kicker, out string headline, out string body);
                if (kicker != null && string.Equals(Sentence(kicker), name, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    kicker = null;   // "BUSINESS:" under the business label says it twice
                }
                if (headline != null && told < most)
                {
                    told++;
                    if (i > 0 || !label)
                    {
                        _b.Append(MarkCut);
                    }
                    if (!(label && i == 0))
                    {
                        _b.Append(MarkKeep);
                    }
                    if (kicker != null)
                    {
                        _b.Append("<align=left><size=78%><smallcaps><cspace=0.1em>").Append(Plain(Sentence(kicker))).Append("</cspace></smallcaps></size></align>").Append(VT);
                    }
                    _b.Append("<align=left><size=120%><b><uppercase>").Append(Plain(headline)).Append("</uppercase></b></size></align>").Append(VT);
                    _b.Append(MarkEnd).Append(Plain(body)).Append('\n');
                }
                else
                {
                    if (label && i == 0)
                    {
                        _b.Append(MarkEnd);
                    }
                    string lead = kicker != null ? Sentence(kicker) : headline;
                    if (lead != null)
                    {
                        _b.Append("<b>").Append(Plain(lead)).Append(kicker != null ? ":" : ".").Append("</b> ");
                    }
                    _b.Append(Plain(body)).Append('\n');
                }
            }
        }

        // The box down the side: the markets at a glance (with as many risers and fallers as it has room for), the
        // weather, then what is inside (or, without the market, around town).
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
                      ? LedgerText.Fill("reallife_ledger_inside_prices_body", "companies", e.Quotes.Count.ToString(CultureInfo.CurrentCulture), "sectors", e.Sectors.Count.ToString(CultureInfo.CurrentCulture))
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

        // A label in the box, sitting on a rule.
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

        // Without the market's chart, what a short paper leaves holds the paper's own notice, as papers fill a gap: a
        // framed box as deep as the room, its words in the middle.
        private void HouseAdvert(float x, float y, float w, float h, float u, Rules rules)
        {
            float bw = 0.7f * w, size = 0.026f * u;
            _fAd.fontSize = size;
            string words = "<size=170%><b><smallcaps><cspace=0.1em>" + Plain(LedgerText.T("reallife_ledger_ad_head")) + "</cspace></smallcaps></b></size>" + VT
                           + Plain(LedgerText.Fill("reallife_ledger_ad_body", "price", LedgerText.Money(_director.WeekPrice)));
            float th = Tall(_fAd, words, bw - 0.08f * u);
            float bh = Mathf.Max(th + 0.05f * u, h - 0.03f * u);
            float bx = x + (w - bw) * 0.5f, by = y + Mathf.Max(0f, h - bh) * 0.5f;
            Set(_fAd, words);
            Put(_fAd.rectTransform, bx + 0.04f * u, by + (bh - th) * 0.5f, bw - 0.08f * u, th + 2f);
            rules.Frame(bx, by, bw, bh, 2f * _px);
            rules.Frame(bx + 0.006f * u, by + 0.006f * u, bw - 0.012f * u, bh - 0.012f * u, _px);
        }

        // ================================================================ the markets page

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

        // The markets page's columns: the lead's text after its drop cap, the market's other stories, then the movers
        // (the risers, the fallers, the most traded: as many as fill the columns) and the bank's figures.
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
                    _b.Append("<align=left><b>").Append(Plain(LedgerText.T(g == 0 ? "reallife_ledger_risers" : g == 1 ? "reallife_ledger_fallers" : "reallife_ledger_active"))).Append("</b></align>").Append(VT);
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

        // ================================================================ the share prices

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

        private readonly int[] _starts = new int[PriceColumns], _ends = new int[PriceColumns];

        // Which quotes each column has (as indexes into the listing), so many rows to a column, counting a sector's name as one.
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

        // A column of the share prices: each sector's name in small capitals on a rule, then its companies: the name in full
        // (smaller only if it would run into the figures), the close and the move right-aligned in even figures.
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

        // ---------------------------------------------------------------- the parts of a page

        // The page's margins on the sheet, and the room inside them.
        private void Margins(RectTransform page, float u, out float W, out float H)
        {
            page.offsetMin = new Vector2(MarginX * u, MarginY * u);
            page.offsetMax = new Vector2(-MarginX * u, -MarginY * u);
            Rect r = page.rect;
            W = r.width;
            H = r.height;
        }

        // The head of an inside page: the paper's name, the page's in small capitals, the double rule and the dateline
        // between rules. Returns where the page's own words start.
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

        // Where a one-line text's box starts for its capitals to sit in the middle of a band so deep.
        private float TopFor(float band, float size)
        {
            return (band - _cap * size) * 0.5f - (_asc - _cap) * size;
        }

        // A label: small capitals in bold, a little apart, at so much of the type.
        private void Label(string words, string size)
        {
            _b.Append("<size=").Append(size).Append("><b><smallcaps><cspace=0.14em>").Append(Plain(words)).Append("</cspace></smallcaps></b></size>");
        }

        // A brief: its kicker (or headline) in bold, then its words.
        private void Brief(LedgerPiece piece)
        {
            LedgerText.Words(piece, out string kicker, out string headline, out string body);
            string lead = kicker != null ? Sentence(kicker) : headline;
            if (lead != null)
            {
                _b.Append("<b>").Append(Plain(lead)).Append(kicker != null ? ":" : ".").Append("</b> ");
            }
            _b.Append(Plain(body)).Append('\n');
        }

        // A row of a table: the name on the left (smaller if it would run into the figures), then the figure and the move
        // each right-aligned at its stop, in even figures: the move's up or down mark before it, its per cent sign after.
        // The stops are the same in every row: the move's field is as wide as a two-figure move with its mark.
        private void Row(string name, string marks, float width, float size, TMP_Text measure, string figure, int moveBp, bool move)
        {
            float mono = _figure * size;
            float right = width - 2f * _px;
            float percent = _percent * size;
            float mark = _marks ? (_mark + 0.18f) * size : 0f;
            float moveField = move ? mark + Even("+00.00", mono) + percent : 0f;
            float figureLeft = figure != null ? right - (move ? moveField + 0.55f * size : 0f) - Even(figure, mono) : right - moveField;
            float room = figureLeft - 0.5f * size;
            string named = Plain(name) + marks;
            float wide = 0f;
            if (measure != null)
            {
                measure.fontSize = size;
                wide = measure.GetPreferredValues(named).x;
            }
            _b.Append("<align=left>");
            if (wide > room && wide > 1f)
            {
                _b.Append("<size=").Append(Mathf.Max(70, Mathf.FloorToInt(100f * room / wide)).ToString(CultureInfo.InvariantCulture)).Append("%>").Append(named).Append("</size>");
            }
            else
            {
                _b.Append(named);
            }
            if (figure != null)
            {
                Pos(figureLeft);
                EvenFigures(figure, mono);
            }
            if (move)
            {
                string change = LedgerText.Change(moveBp);
                bool sign = change.EndsWith("%", System.StringComparison.Ordinal);
                if (sign)
                {
                    change = change.Substring(0, change.Length - 1);
                }
                float at = right - (sign ? percent : 0f) - Even(change, mono);
                if (_marks && moveBp != 0)
                {
                    Pos(at - mark);
                    _b.Append("<size=").Append(Pct(MarkSize)).Append('>').Append(moveBp > 0 ? Up : Down).Append("</size>");
                }
                Pos(at);
                EvenFigures(change, mono);
                if (sign)
                {
                    _b.Append('%');
                }
            }
            _b.Append("</align>");
        }

        // The width of figures set even: every figure and sign one width, a stop, comma or colon half of it.
        private static float Even(string figure, float mono)
        {
            float w = 0f;
            for (int i = 0; i < figure.Length; i++)
            {
                char c = figure[i];
                w += c == '.' || c == ',' || c == ':' ? mono * 0.5f : mono;
            }
            return w;
        }

        private void EvenFigures(string figure, float mono)
        {
            _b.Append("<mspace=").Append(F(mono)).Append(" duospace=1>").Append(Plain(figure)).Append("</mspace>");
        }

        private void Pos(float x)
        {
            _b.Append("<pos=").Append(F(x)).Append('>');
        }

        // A move in ink: its mark, up or down, and the figure with its sign.
        private string Move(int bp)
        {
            string figure = LedgerText.Change(bp);
            return bp != 0 && _marks ? "<size=" + Pct(MarkSize) + ">" + (bp > 0 ? Up : Down) + "</size> " + figure : figure;
        }

        private static string Pct(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static string F(float v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // What has been written, without a last line break (which would be one more, empty, line).
        private string Words()
        {
            int end = _b.Length;
            while (end > 0 && (_b[end - 1] == '\n' || _b[end - 1] == VT))
            {
                end--;
            }
            return _b.ToString(0, end);
        }

        // The lead's text after its drop cap: its first letter is the cap, the rest of its first word or two in small capitals.
        private static string AfterCap(string text)
        {
            string rest = text.Substring(1);
            int one = rest.IndexOf(' ');
            int two = one >= 0 ? rest.IndexOf(' ', one + 1) : -1;
            int end = two > 0 && two <= 22 ? two : one > 0 && one <= 22 ? one : Mathf.Min(rest.Length, 22);
            return "<smallcaps>" + Plain(rest.Substring(0, end)) + "</smallcaps>" + Plain(rest.Substring(end));
        }

        // A kicker as the page prints it, in small capitals: "COURT" becomes "Court".
        private static string Sentence(string kicker)
        {
            if (string.IsNullOrEmpty(kicker))
            {
                return kicker;
            }
            string lower = CultureInfo.CurrentCulture.TextInfo.ToLower(kicker);
            return char.ToUpper(lower[0], CultureInfo.CurrentCulture) + lower.Substring(1);
        }

        private static string BylineKey(LedgerSection section)
        {
            switch (section)
            {
                case LedgerSection.Economy: return "reallife_ledger_byline_economy";
                case LedgerSection.Business: return "reallife_ledger_byline_business";
                case LedgerSection.Markets: return "reallife_ledger_byline_markets";
                default: return "reallife_ledger_byline_city";
            }
        }

        // A market story's headline as the inside page will print it.
        private static string MarketHead(LedgerPiece piece)
        {
            LedgerText.Words(piece, out _, out string headline, out string body);
            if (headline == null)
            {
                FirstSentence(ref body, out headline);
            }
            return headline;
        }

        // The bank rate and inflation, and the state of the economy, a line each. False if the edition has neither.
        private bool Rates(LedgerEdition e)
        {
            bool any = false;
            if (e.RateBp >= 0)
            {
                _b.Append(Plain(LedgerText.Fill("reallife_ledger_rates", "rate", LedgerText.Rate(e.RateBp), "inflation", e.InflationBp != int.MinValue ? LedgerText.Rate(e.InflationBp) : "?")));
                any = true;
            }
            if (!string.IsNullOrEmpty(e.CycleKey))
            {
                if (any)
                {
                    _b.Append(VT);
                }
                _b.Append(Plain(LedgerText.Fill("reallife_ledger_cycle", "state", LedgerText.T(e.CycleKey))));
                any = true;
            }
            return any;
        }

        // ---------------------------------------------------------------- the words

        // A piece's words without its kicker or headline: what a box that has its own name prints.
        private static string Body(LedgerPiece p)
        {
            LedgerText.Words(p, out _, out string headline, out string body);
            return headline != null && p.Head == null ? headline + ". " + body : body;
        }

        // A lead without a headline of its own: its first sentence is the headline.
        private static void FirstSentence(ref string body, out string headline)
        {
            body = body ?? string.Empty;
            int stop = body.IndexOf(". ", System.StringComparison.Ordinal);
            if (stop > 0 && stop < 110)
            {
                headline = body.Substring(0, stop);
                body = body.Substring(stop + 2);
            }
            else
            {
                headline = body.TrimEnd('.');
                body = string.Empty;
            }
        }

        private string Dateline(LedgerEdition e)
        {
            return e.HasMarket
                ? LedgerText.Fill("reallife_ledger_dateline_market", "weekday", Weekday(e.Day), "day", e.Day.ToString(CultureInfo.CurrentCulture), "session", e.MarketDay.ToString(CultureInfo.CurrentCulture))
                : LedgerText.Fill("reallife_ledger_dateline", "weekday", Weekday(e.Day), "day", e.Day.ToString(CultureInfo.CurrentCulture));
        }

        private static string Weekday(int day)
        {
            return TimeHelper.GetDayOfWeek(Mathf.Max(1, day)).GetLocalization();
        }

        private static int Year(int day)
        {
            int perYear = Mathf.Max(1, GameCalendar.DaysPerYear);
            return (Mathf.Max(1, day) - 1) / perYear + 1;
        }

        private static string Level(int hundredths)
        {
            return (hundredths / 100.0).ToString("N2", CultureInfo.CurrentCulture);
        }

        // A line the paper has instead of news: in italic, in the same ink.
        private static string Soft(string words)
        {
            return "<i angle=12>" + Plain(words) + "</i>";
        }

        private static string Marks(int flags)
        {
            if (flags == 0)
            {
                return string.Empty;
            }
            string marks = ((flags & LedgerQuote.Bid) != 0 ? "b" : string.Empty) + ((flags & LedgerQuote.ExDividend) != 0 ? "x" : string.Empty)
                           + ((flags & LedgerQuote.New) != 0 ? "n" : string.Empty) + ((flags & LedgerQuote.Split) != 0 ? "s" : string.Empty);
            return " <size=80%><i angle=12>" + marks + "</i></size>";
        }

        // Words that came from a name in the game (a shop the player named) printed as they are, never read as mark-up.
        private static string Plain(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return string.Empty;
            }
            return words.IndexOf('<') < 0 ? words : "<noparse>" + words.Replace("</noparse>", string.Empty) + "</noparse>";
        }

        private static void Set(TMP_Text text, string words)
        {
            if (text != null)
            {
                UiClone.Words(text, words ?? string.Empty);
            }
        }

        private static void Show(Component part, bool on)
        {
            if (part != null && part.gameObject.activeSelf != on)
            {
                part.gameObject.SetActive(on);
            }
        }
    }
}
