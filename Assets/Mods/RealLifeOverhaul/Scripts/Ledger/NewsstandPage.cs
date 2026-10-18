using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>One block of a page on the phone: its words (mark-up) and how it is set (NewsstandPage's kinds).</summary>
    public struct NewsBlock
    {
        public string Text;
        public byte Kind;
    }

    /// <summary>
    /// A title's edition written for the phone as one column of blocks, top to bottom: the masthead, the dateline, the
    /// lead, each section the title prints, its markets, the weather and, for two titles, every company's close.
    /// Written from the same pieces as the printed paper (LedgerText), in the language of the moment; the app keeps
    /// what it wrote.
    /// </summary>
    public sealed class NewsstandPage
    {
        public const byte Masthead = 0, Centre = 1, Body = 2, Label = 3, Rule = 4, Double = 5, Table = 6, Foot = 7;

        private const int StoriesMost = 4;      // stories under a headline in a section; the rest are briefs
        private const int MarketStories = 6;
        private const int MoversMost = 6, ActiveMost = 5;
        private const int NameMost = 26;        // a company's name in a table, at most so many letters
        private const char VT = '\v';           // a line break inside an item: no paragraph spacing before it

        private readonly StringBuilder _b = new StringBuilder(4096);
        private readonly List<LedgerPiece> _pieces = new List<LedgerPiece>(24);
        private readonly List<LedgerQuote> _quotes = new List<LedgerQuote>(160);

        /// <summary>A title's edition as the blocks of its page.</summary>
        public void Write(LedgerEdition e, int title, List<NewsBlock> into)
        {
            into.Clear();
            _b.Clear();
            _b.Append("<b>").Append(Plain(LedgerText.T(NewsstandCatalogue.NameKeys[title]))).Append("</b>\n<size=38%><i>")
              .Append(Plain(LedgerText.T(NewsstandCatalogue.TaglineKeys[title]))).Append("</i></size>");
            Add(into, Masthead);
            Add(into, Double, null);
            _b.Append("<smallcaps><cspace=0.06em>").Append(Plain(LedgerText.Fill("reallife_ledger_online_dateline", "weekday", LedgerPage.Weekday(e.Day),
                "day", e.Day.ToString(CultureInfo.CurrentCulture)))).Append("</cspace></smallcaps>");
            Add(into, Centre);
            Add(into, Rule, null);

            LedgerPiece lead = NewsstandRules.Lead(e, title);
            Lead(e, lead);
            Add(into, Body);

            LedgerSection[] sections = NewsstandCatalogue.Sections[title];
            int markets = NewsstandCatalogue.Markets[title];
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] == LedgerSection.Markets)
                {
                    Markets(e, lead, markets, into);
                }
                else
                {
                    Section(e, sections[i], lead, into);
                }
            }
            if (!NewsstandRules.Prints(title, LedgerSection.Markets))
            {
                IndexLine(e, into);
            }
            Weather(e, into);
            if (NewsstandCatalogue.SharePrices[title] && e.HasMarket && e.Quotes.Count > 0)
            {
                SharePrices(e, into);
            }
            _b.Append("<i>").Append(Plain(LedgerText.Fill("reallife_ledger_online_foot", "name", LedgerText.T(NewsstandCatalogue.NameKeys[title]),
                "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)))).Append("</i>");
            Add(into, Foot);
        }

        /// <summary>A page with nothing on it but a line: no edition to read.</summary>
        public void Empty(string words, List<NewsBlock> into)
        {
            into.Clear();
            _b.Clear();
            _b.Append("<b>").Append(Plain(LedgerText.T("reallife_ledger_online_app"))).Append("</b>");
            Add(into, Masthead);
            Add(into, Double, null);
            _b.Append("<i>").Append(Plain(words)).Append("</i>");
            Add(into, Centre);
        }

        // The lead: kicker, headline in capitals, byline and story; a quiet morning's words without one.
        private void Lead(LedgerEdition e, LedgerPiece lead)
        {
            string kicker, headline, body;
            if (lead != null)
            {
                LedgerText.Words(lead, out kicker, out headline, out body);
                if (headline == null)
                {
                    LedgerPage.FirstSentence(ref body, out headline);
                }
                kicker = kicker != null ? LedgerPage.Sentence(kicker) : LedgerText.T(LedgerCatalogue.SectionKeys[(int)lead.Section]);
            }
            else
            {
                kicker = LedgerText.T(LedgerCatalogue.SectionKeys[(int)LedgerSection.City]);
                headline = LedgerText.T("reallife_ledger_quiet_head");
                body = LedgerText.T("reallife_ledger_quiet_body");
            }
            _b.Append("<size=78%><smallcaps><cspace=0.1em>").Append(Plain(kicker)).Append("</cspace></smallcaps></size>").Append(VT);
            _b.Append("<size=165%><b><uppercase>").Append(Plain(headline)).Append("</uppercase></b></size>").Append(VT);
            _b.Append("<size=78%><i>").Append(Plain(LedgerText.T(LedgerPage.BylineKey(lead != null ? lead.Section : LedgerSection.City)))).Append("</i></size>");
            if (!string.IsNullOrEmpty(body))
            {
                _b.Append('\n').Append(Plain(body));
            }
        }

        // A section: a rule and its name, then its stories, the heaviest first.
        private void Section(LedgerEdition e, LedgerSection section, LedgerPiece lead, List<NewsBlock> into)
        {
            LedgerRules.Of(e, section, lead, _pieces);
            if (_pieces.Count == 0)
            {
                return;
            }
            Heading(LedgerCatalogue.SectionKeys[(int)section], into);
            for (int i = 0; i < _pieces.Count; i++)
            {
                Story(_pieces[i], i < StoriesMost);
            }
            Add(into, Body);
        }

        // One story: its headline in bold capitals over its words; or, as a brief, its kicker in bold before them.
        private void Story(LedgerPiece p, bool story)
        {
            LedgerText.Words(p, out string kicker, out string headline, out string body);
            if (story && headline != null)
            {
                _b.Append("<b><uppercase>").Append(Plain(headline)).Append("</uppercase></b>");
                if (!string.IsNullOrEmpty(body))
                {
                    _b.Append(VT).Append(Plain(body));
                }
            }
            else
            {
                if (kicker != null)
                {
                    _b.Append("<b>").Append(Plain(LedgerPage.Sentence(kicker))).Append(":</b> ");
                }
                _b.Append(Plain(headline != null && p.Head == null ? headline + ". " + body : body));
            }
            _b.Append('\n');
        }

        // The markets: their stories, then the figures as the title has them.
        private void Markets(LedgerEdition e, LedgerPiece lead, int how, List<NewsBlock> into)
        {
            Heading(LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets], into);
            LedgerRules.Of(e, LedgerSection.Markets, lead, _pieces);
            for (int i = 0; i < _pieces.Count && i < MarketStories; i++)
            {
                Story(_pieces[i], i < StoriesMost);
            }
            if (_pieces.Count == 0)
            {
                _b.Append("<i>").Append(Plain(LedgerText.T(e.HasMarket ? "reallife_ledger_online_market_quiet" : "reallife_ledger_no_market"))).Append("</i>\n");
            }
            Add(into, Body);
            if (!e.HasMarket)
            {
                return;
            }
            // the index, the week, the rates and the cycle
            _b.Append("<size=80%><smallcaps><cspace=0.1em>").Append(Plain(LedgerText.T("reallife_ledger_ear_index"))).Append("</cspace></smallcaps></size>").Append(VT);
            _b.Append("<size=160%><b>").Append(LedgerPage.Level(e.IndexLevel)).Append("</b></size>   <size=110%>").Append(LedgerText.Change(e.IndexChangeBp)).Append("</size>").Append(VT);
            _b.Append("<i>").Append(Plain(LedgerText.Fill("reallife_ledger_week", "change", LedgerText.Change(e.IndexWeekBp)))).Append("</i>");
            if (e.RateBp >= 0)
            {
                _b.Append('\n').Append(Plain(LedgerText.Fill("reallife_ledger_rates", "rate", LedgerText.Rate(e.RateBp), "inflation", e.InflationBp != int.MinValue ? LedgerText.Rate(e.InflationBp) : "?")));
            }
            if (!string.IsNullOrEmpty(e.CycleKey))
            {
                _b.Append(e.RateBp >= 0 ? VT : '\n').Append(Plain(LedgerText.Fill("reallife_ledger_cycle", "state", LedgerText.T(e.CycleKey))));
            }
            Add(into, Body);
            if (how < NewsstandCatalogue.MarketsFull)
            {
                return;
            }
            Movers(e, true, "reallife_ledger_risers", into);
            Movers(e, false, "reallife_ledger_fallers", into);
            LedgerRules.MostActive(e, ActiveMost, _quotes);
            if (_quotes.Count > 0)
            {
                TableHead("reallife_ledger_active");
                for (int i = 0; i < _quotes.Count; i++)
                {
                    Quote(_quotes[i]);
                }
                Add(into, Table);
            }
            if (e.Sectors.Count > 0)
            {
                TableHead("reallife_ledger_sectors");
                for (int i = 0; i < e.Sectors.Count; i++)
                {
                    LedgerSector x = e.Sectors[i];
                    Row(LedgerText.T(x.Key), (x.Level / 100.0).ToString("N2", CultureInfo.CurrentCulture), x.ChangeBp);
                }
                Add(into, Table);
            }
        }

        private void Movers(LedgerEdition e, bool up, string titleKey, List<NewsBlock> into)
        {
            LedgerRules.Movers(e, up, MoversMost, _quotes);
            if (_quotes.Count == 0)
            {
                return;
            }
            TableHead(titleKey);
            for (int i = 0; i < _quotes.Count; i++)
            {
                Quote(_quotes[i]);
            }
            Add(into, Table);
        }

        // The markets in one line, for a title that has no markets pages.
        private void IndexLine(LedgerEdition e, List<NewsBlock> into)
        {
            Heading(LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets], into);
            if (e.HasMarket)
            {
                _b.Append(Plain(LedgerText.Fill("reallife_ledger_online_index_line", "level", LedgerPage.Level(e.IndexLevel), "change", LedgerText.Change(e.IndexChangeBp),
                    "week", LedgerText.Change(e.IndexWeekBp))));
            }
            else
            {
                _b.Append("<i>").Append(Plain(LedgerText.T("reallife_ledger_no_market"))).Append("</i>");
            }
            Add(into, Body);
        }

        private void Weather(LedgerEdition e, List<NewsBlock> into)
        {
            Heading(LedgerCatalogue.SectionKeys[(int)LedgerSection.Weather], into);
            LedgerRules.Of(e, LedgerSection.Weather, null, _pieces);
            if (_pieces.Count == 0)
            {
                _b.Append("<i>").Append(Plain(LedgerText.T("reallife_ledger_no_weather"))).Append("</i>");
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                LedgerText.Words(_pieces[i], out string kicker, out _, out _);
                if (kicker != null && !string.Equals(LedgerPage.Sentence(kicker), LedgerText.T("reallife_ledger_ear_weather"), System.StringComparison.OrdinalIgnoreCase))
                {
                    _b.Append("<b>").Append(Plain(LedgerPage.Sentence(kicker))).Append(":</b> ");   // an outlook, not the day's weather
                }
                _b.Append(Plain(LedgerPage.Body(_pieces[i]))).Append(i + 1 < _pieces.Count ? "\n" : string.Empty);
            }
            Add(into, Body);
        }

        // Every listed company's close and its move, by sector: one block a sector.
        private void SharePrices(LedgerEdition e, List<NewsBlock> into)
        {
            Heading("reallife_ledger_tab_prices", into);
            _b.Append("<size=85%><i>").Append(Plain(LedgerText.T("reallife_ledger_prices_legend"))).Append("</i></size>");
            Add(into, Body);
            LedgerRules.Listing(e, _quotes);
            int sector = -1;
            for (int i = 0; i < _quotes.Count; i++)
            {
                LedgerQuote q = _quotes[i];
                if (q.Sector != sector)
                {
                    if (sector >= 0)
                    {
                        Add(into, Table);
                    }
                    sector = q.Sector;
                    string name = sector >= 0 && sector < e.Sectors.Count ? LedgerText.T(e.Sectors[sector].Key) : string.Empty;
                    _b.Append("<b>").Append(Plain(name)).Append("</b>\n");
                }
                Quote(q);
            }
            if (sector >= 0)
            {
                Add(into, Table);
            }
        }

        private void TableHead(string key)
        {
            _b.Append("<b>").Append(Plain(LedgerText.T(key))).Append("</b>\n");
        }

        private void Quote(LedgerQuote q)
        {
            string name = q.Name ?? string.Empty;
            if (name.Length > NameMost)
            {
                name = name.Substring(0, NameMost - 1).TrimEnd() + ".";
            }
            Row(Plain(name) + LedgerPage.Marks(q.Flags), LedgerText.Figure(q.Close), q.ChangeBp, true);
        }

        // A line of a table: the name, the figure and the move at their places across the column.
        private void Row(string name, string figure, int moveBp, bool marked = false)
        {
            _b.Append(marked ? name : Plain(name)).Append("<pos=62%>").Append(figure).Append("<pos=82%>").Append(LedgerText.Change(moveBp)).Append('\n');
        }

        private void Heading(string key, List<NewsBlock> into)
        {
            Add(into, Rule, null);
            _b.Append("<smallcaps><cspace=0.12em><b>").Append(Plain(LedgerText.T(key))).Append("</b></cspace></smallcaps>");
            Add(into, Label);
        }

        // What has been written becomes a block (without a last line break, which would be one more empty line).
        private void Add(List<NewsBlock> into, byte kind)
        {
            int end = _b.Length;
            while (end > 0 && (_b[end - 1] == '\n' || _b[end - 1] == VT))
            {
                end--;
            }
            if (end > 0)
            {
                into.Add(new NewsBlock { Text = _b.ToString(0, end), Kind = kind });
            }
            _b.Clear();
        }

        private static void Add(List<NewsBlock> into, byte kind, string text)
        {
            into.Add(new NewsBlock { Text = text, Kind = kind });
        }

        private static string Plain(string words)
        {
            return LedgerPage.Plain(words);
        }
    }
}
