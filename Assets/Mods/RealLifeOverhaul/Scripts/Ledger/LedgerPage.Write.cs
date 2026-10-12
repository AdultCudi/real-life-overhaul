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
        private const int ColumnStories = 2;      // stories under a headline in one of the front page's columns; the rest are briefs
        private const int MarketStories = 6;
        private const int SideMovers = 3, PageMovers = 5;

        private readonly List<string> _rows = new List<string>(200);
        private readonly List<bool> _headings = new List<bool>(200);

        // ---------------------------------------------------------------- the front page

        private void WriteFront(LedgerEdition e)
        {
            Set(_fTitle, LedgerText.T("reallife_ledger_masthead"));
            Set(_fDateLeft, LedgerText.Fill("reallife_ledger_dateline", "weekday", Weekday(e.Day), "day", e.Day.ToString(CultureInfo.CurrentCulture)));
            Set(_fDateMiddle, LedgerText.Fill("reallife_ledger_dateline_number", "year", Year(e.Day).ToString(CultureInfo.CurrentCulture), "number", e.Day.ToString(CultureInfo.CurrentCulture)));
            Set(_fDateRight, LedgerText.Fill("reallife_ledger_dateline_price", "price", LedgerText.Money(_director.CopyPrice)));

            LedgerRules.Of(e, LedgerSection.Weather, null, _pieces);
            string weather = _pieces.Count > 0 ? Body(_pieces[0]) : LedgerText.T("reallife_ledger_no_weather");
            int stop = weather.IndexOf(". ", System.StringComparison.Ordinal);
            if (stop > 0)
            {
                weather = weather.Substring(0, stop + 1);   // the ear has the first sentence; the weather box has it all
            }
            Set(_fEarLeft, "<b><uppercase>" + LedgerText.T("reallife_ledger_ear_weather") + "</uppercase></b>\n" + Plain(weather));
            Set(_fEarRight, "<b><uppercase>" + LedgerText.T("reallife_ledger_ear_index") + "</uppercase></b>\n"
                            + (e.HasMarket ? "<b>" + Level(e.IndexLevel) + "</b> " + Coloured(e.IndexChangeBp, LedgerText.Change(e.IndexChangeBp)) : LedgerText.T("reallife_ledger_no_session")));

            // The lead: the heaviest piece of the news, under its headline across the page, in two columns.
            LedgerPiece lead = LedgerRules.Lead(e);
            if (lead != null)
            {
                LedgerText.Words(lead, out string kicker, out string headline, out string body);
                if (headline == null)
                {
                    FirstSentence(ref body, out headline);
                }
                Set(_fKicker, Plain(kicker ?? LedgerText.T(LedgerCatalogue.SectionKeys[(int)lead.Section])));
                Set(_fHead, Plain(headline));
                Set(_fLead1, Plain(body));
            }
            else
            {
                Set(_fKicker, LedgerText.T(LedgerCatalogue.SectionKeys[(int)LedgerSection.City]));
                Set(_fHead, LedgerText.T("reallife_ledger_quiet_head"));
                Set(_fLead1, LedgerText.T("reallife_ledger_quiet_body"));
            }

            LedgerSection[] columns = { LedgerSection.City, LedgerSection.Economy, LedgerSection.Business };
            for (int i = 0; i < columns.Length; i++)
            {
                LedgerRules.Of(e, columns[i], lead, _pieces);
                Set(_fColumns[i], Column(_pieces, ColumnStories));
            }

            Set(_fMarkets, SideMarkets(e));

            LedgerRules.Of(e, LedgerSection.Weather, null, _pieces);
            _b.Clear();
            for (int i = 0; i < _pieces.Count; i++)
            {
                Gap();
                _b.Append(Plain(Body(_pieces[i])));
            }
            Set(_fWeather, _b.Length > 0 ? _b.ToString() : Soft(LedgerText.T("reallife_ledger_no_weather")));

            _b.Clear();
            LedgerRules.Of(e, LedgerSection.People, null, _pieces);
            Briefs(_pieces);
            LedgerRules.Of(e, LedgerSection.AroundTown, null, _pieces);
            Briefs(_pieces);
            Set(_fAround, _b.Length > 0 ? _b.ToString() : Soft(LedgerText.T("reallife_ledger_nothing")));
        }

        // The markets down the side of the front page: the index, its week, the risers and fallers, the bank rate.
        private string SideMarkets(LedgerEdition e)
        {
            _b.Clear();
            if (!e.HasMarket)
            {
                _b.Append(Soft(LedgerText.T("reallife_ledger_no_market")));
            }
            else
            {
                _b.Append("<size=78%><b><uppercase>").Append(LedgerText.T("reallife_ledger_ear_index")).Append("</uppercase></b></size>\n");
                _b.Append("<size=160%><b>").Append(Level(e.IndexLevel)).Append("</b></size>  ").Append(Coloured(e.IndexChangeBp, LedgerText.Change(e.IndexChangeBp))).Append('\n');
                _b.Append("<size=86%>").Append(LedgerText.Fill("reallife_ledger_week", "change", LedgerText.Change(e.IndexWeekBp))).Append("</size>\n");
                LedgerRules.Movers(e, true, SideMovers, _quotes);
                if (_quotes.Count > 0)
                {
                    _b.Append("\n<b>").Append(LedgerText.T("reallife_ledger_risers")).Append("</b>\n");
                    Movers(_quotes, false);
                }
                LedgerRules.Movers(e, false, SideMovers, _quotes);
                if (_quotes.Count > 0)
                {
                    _b.Append("\n<b>").Append(LedgerText.T("reallife_ledger_fallers")).Append("</b>\n");
                    Movers(_quotes, false);
                }
            }
            Rates(e);
            return _b.ToString();
        }

        private void Rates(LedgerEdition e)
        {
            if (e.RateBp >= 0)
            {
                _b.Append("\n<size=88%>").Append(LedgerText.Fill("reallife_ledger_rates", "rate", LedgerText.Rate(e.RateBp),
                    "inflation", e.InflationBp != int.MinValue ? LedgerText.Rate(e.InflationBp) : "?")).Append("</size>");
            }
            if (!string.IsNullOrEmpty(e.CycleKey))
            {
                _b.Append("\n<size=88%>").Append(LedgerText.Fill("reallife_ledger_cycle", "state", LedgerText.T(e.CycleKey))).Append("</size>");
            }
        }

        // ---------------------------------------------------------------- the markets page

        private void WriteMarkets(LedgerEdition e)
        {
            Set(_mTitle, LedgerText.T("reallife_ledger_masthead"));
            Set(_mName, LedgerText.T("reallife_ledger_tab_markets"));
            Set(_mDate, Dateline(e));

            LedgerRules.Of(e, LedgerSection.Markets, null, _pieces);
            if (_pieces.Count > 0)
            {
                LedgerPiece lead = _pieces[0];
                LedgerText.Words(lead, out string kicker, out string headline, out string body);
                if (headline == null)
                {
                    FirstSentence(ref body, out headline);
                }
                Set(_mHead, Plain(headline));
                Set(_mLead1, Plain(body));
                _pieces.RemoveAt(0);
                Set(_mNews1, _pieces.Count > 0 ? Column(_pieces, MarketStories) : Soft(LedgerText.T("reallife_ledger_nothing")));
            }
            else
            {
                Set(_mHead, LedgerText.T(e.HasMarket ? "reallife_ledger_market_quiet_head" : "reallife_ledger_no_market_head"));
                Set(_mLead1, LedgerText.T(e.HasMarket ? "reallife_ledger_market_quiet" : "reallife_ledger_no_market"));
                Set(_mNews1, string.Empty);
            }

            _b.Clear();
            if (e.HasMarket)
            {
                _b.Append("<size=150%><b>").Append(Level(e.IndexLevel)).Append("</b></size>  ").Append(Coloured(e.IndexChangeBp, LedgerText.Change(e.IndexChangeBp))).Append('\n');
                _b.Append("<size=88%>").Append(LedgerText.Fill("reallife_ledger_week", "change", LedgerText.Change(e.IndexWeekBp))).Append("</size>");
            }
            else
            {
                _b.Append(Soft(LedgerText.T("reallife_ledger_no_market")));
            }
            Set(_mIndex, _b.ToString());
            _mChart.Show(e.HasMarket ? e.IndexCloses : null, Ink, new Color(Ink.r, Ink.g, Ink.b, 0.16f), Mathf.Max(1.5f, _height * 0.0026f));
            Set(_mCaption, e.HasMarket && e.IndexCloses.Count > 1 ? LedgerText.Fill("reallife_ledger_chart_caption", "days", e.IndexCloses.Count.ToString(CultureInfo.CurrentCulture)) : string.Empty);

            _b.Clear();
            for (int i = 0; e.HasMarket && i < e.Sectors.Count; i++)
            {
                LedgerSector s = e.Sectors[i];
                _b.Append(i > 0 ? "\n" : string.Empty).Append(Plain(Short(LedgerText.T(s.Key), 20))).Append("<pos=60%>").Append(Mono(Level(s.Level), 8))
                    .Append("<pos=84%>").Append(Coloured(s.ChangeBp, Mono(LedgerText.Change(s.ChangeBp), 7)));
            }
            Set(_mSectors, _b.Length > 0 ? _b.ToString() : Soft(LedgerText.T("reallife_ledger_no_market")));

            _b.Clear();
            if (e.HasMarket)
            {
                LedgerRules.Movers(e, true, PageMovers, _quotes);
                _b.Append("<b>").Append(LedgerText.T("reallife_ledger_risers")).Append("</b>\n");
                Movers(_quotes, true);
                LedgerRules.Movers(e, false, PageMovers, _quotes);
                _b.Append("\n<b>").Append(LedgerText.T("reallife_ledger_fallers")).Append("</b>\n");
                Movers(_quotes, true);
                LedgerRules.MostActive(e, PageMovers, _quotes);
                _b.Append("\n<b>").Append(LedgerText.T("reallife_ledger_active")).Append("</b>\n");
                for (int i = 0; i < _quotes.Count; i++)
                {
                    LedgerQuote q = _quotes[i];
                    _b.Append(Plain(Short(q.Name, 16))).Append("<pos=68%>").Append(Mono(LedgerText.Figure(q.Close), 8)).Append('\n');
                }
                Rates(e);
            }
            Set(_mMovers, _b.Length > 0 ? _b.ToString() : Soft(LedgerText.T("reallife_ledger_no_market")));
        }

        // ---------------------------------------------------------------- the share prices

        private void WritePrices(LedgerEdition e)
        {
            Set(_pTitle, LedgerText.T("reallife_ledger_masthead"));
            Set(_pName, LedgerText.T("reallife_ledger_tab_prices"));
            Set(_pDate, Dateline(e));
            Set(_pLegend, LedgerText.T("reallife_ledger_prices_legend"));

            _rows.Clear();
            _headings.Clear();
            if (e.HasMarket)
            {
                LedgerRules.Listing(e, _quotes);
                int sector = -1;
                for (int i = 0; i < _quotes.Count; i++)
                {
                    LedgerQuote q = _quotes[i];
                    if (q.Sector != sector)
                    {
                        sector = q.Sector;
                        _rows.Add("<b><uppercase>" + Plain(LedgerText.T(sector < e.Sectors.Count ? e.Sectors[sector].Key : string.Empty)) + "</uppercase></b>");
                        _headings.Add(true);
                    }
                    _rows.Add(Plain(Short(q.Name, 22)) + Marks(q.Flags) + "<pos=57%>" + Mono(LedgerText.Figure(q.Close), 8) + "<pos=80%>"
                              + Coloured(q.ChangeBp, Mono(LedgerText.Change(q.ChangeBp), 7)));
                    _headings.Add(false);
                }
            }
            if (_rows.Count == 0)
            {
                Set(_pColumns[0], Soft(LedgerText.T("reallife_ledger_no_market")));
                for (int i = 1; i < _pColumns.Length; i++)
                {
                    Set(_pColumns[i], string.Empty);
                }
                return;
            }
            // Four columns of as many lines each, a sector's name never left at the foot of a column without its first company.
            int columns = _pColumns.Length;
            int per = (_rows.Count + columns - 1) / columns;
            int at = 0;
            for (int c = 0; c < columns; c++)
            {
                int end = c == columns - 1 ? _rows.Count : Mathf.Min(_rows.Count, at + per);
                if (end < _rows.Count && end > at + 1 && _headings[end - 1])
                {
                    end--;
                }
                _b.Clear();
                for (int i = at; i < end; i++)
                {
                    _b.Append(i > at ? "\n" : string.Empty).Append(_rows[i]);
                }
                Set(_pColumns[c], _b.ToString());
                at = end;
            }
            FitColumns();
        }

        // The share prices' type, sized so that the longest column fits its height: measured once, the same size in all four.
        private void FitColumns()
        {
            float wanted = Mathf.Max(10f, 0.016f * _height);
            float size = wanted;
            for (int c = 0; c < _pColumns.Length; c++)
            {
                TMP_Text t = _pColumns[c];
                if (t == null || string.IsNullOrEmpty(t.text))
                {
                    continue;
                }
                t.fontSize = wanted;
                Rect box = t.rectTransform.rect;
                Vector2 needs = t.GetPreferredValues(t.text, box.width, 0f);
                if (needs.y > box.height && needs.y > 1f)
                {
                    size = Mathf.Min(size, wanted * box.height / needs.y * 0.98f);
                }
            }
            size = Mathf.Max(9f, size);
            for (int c = 0; c < _pColumns.Length; c++)
            {
                if (_pColumns[c] != null)
                {
                    _pColumns[c].fontSize = size;
                }
            }
        }

        // ---------------------------------------------------------------- the words

        // A column of a section: its heaviest pieces as stories under their headlines, the rest as briefs.
        private string Column(List<LedgerPiece> pieces, int stories)
        {
            _b.Clear();
            int told = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                LedgerText.Words(pieces[i], out string kicker, out string headline, out string body);
                Gap();
                if (headline != null && told < stories)
                {
                    told++;
                    if (kicker != null)
                    {
                        _b.Append("<color=#9A2418><size=80%><b><uppercase>").Append(Plain(kicker)).Append("</uppercase></b></size></color>\n");
                    }
                    _b.Append("<b><uppercase><size=118%>").Append(Plain(headline)).Append("</size></uppercase></b>\n").Append(Plain(body));
                }
                else
                {
                    string lead = kicker ?? headline;
                    if (lead != null)
                    {
                        _b.Append("<b>").Append(Plain(lead)).Append(kicker != null ? ":" : ".").Append("</b> ");
                    }
                    _b.Append(Plain(body));
                }
            }
            return _b.Length > 0 ? _b.ToString() : Soft(LedgerText.T("reallife_ledger_nothing"));
        }

        // Pieces as briefs, each with its kicker in bold.
        private void Briefs(List<LedgerPiece> pieces)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                LedgerText.Words(pieces[i], out string kicker, out string headline, out string body);
                Gap();
                string lead = kicker ?? headline;
                if (lead != null)
                {
                    _b.Append("<b>").Append(Plain(lead)).Append(kicker != null ? ":" : ".").Append("</b> ");
                }
                _b.Append(Plain(body));
            }
        }

        private void Movers(List<LedgerQuote> quotes, bool wide)
        {
            for (int i = 0; i < quotes.Count; i++)
            {
                LedgerQuote q = quotes[i];
                _b.Append(Plain(Short(q.Name, wide ? 16 : 18))).Append(wide ? "<pos=68%>" : "<pos=74%>").Append(Coloured(q.ChangeBp, Mono(LedgerText.Change(q.ChangeBp), 7))).Append('\n');
            }
        }

        private void Gap()
        {
            if (_b.Length > 0)
            {
                _b.Append("\n<size=40%>\n</size>");
            }
        }

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

        private static string Coloured(int bp, string words)
        {
            return bp > 0 ? "<color=" + UpInk + ">" + words + "</color>" : bp < 0 ? "<color=" + DownInk + ">" + words + "</color>" : words;
        }

        private static string Soft(string words)
        {
            return "<color=" + SoftInk + "><i>" + Plain(words) + "</i></color>";
        }

        // Figures in a fixed width, right-aligned in their field, so that a column of them lines up.
        private static string Mono(string figure, int width)
        {
            string padded = figure.Length < width ? new string(' ', width - figure.Length) + figure : figure;
            return "<mspace=0.56em>" + padded + "</mspace>";
        }

        private static string Marks(int flags)
        {
            if (flags == 0)
            {
                return string.Empty;
            }
            string marks = ((flags & LedgerQuote.Bid) != 0 ? "b" : string.Empty) + ((flags & LedgerQuote.ExDividend) != 0 ? "x" : string.Empty)
                           + ((flags & LedgerQuote.New) != 0 ? "n" : string.Empty) + ((flags & LedgerQuote.Split) != 0 ? "s" : string.Empty);
            return " <color=" + SoftInk + "><size=80%><i>" + marks + "</i></size></color>";
        }

        private static string Short(string name, int most)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= most)
            {
                return name ?? string.Empty;
            }
            return name.Substring(0, most - 1).TrimEnd() + ".";
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
    }
}
