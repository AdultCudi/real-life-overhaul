using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Outlook;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The City Ledger on the kit: the morning's edition read into the kit's page, its front, its markets and its share prices, or a
    /// front page of its own (the obituary, the rich list). Every story is a piece the Ledger already wrote; nothing is made up here.
    /// </summary>
    public sealed partial class NewsLive
    {
        private const int StoriesMost = 4;           // stories a section of the front page prints
        private const int BriefsMost = 5;            // stories of the around town column
        private const int BandMovers = 2;            // risers and fallers in the markets band
        private static readonly List<LedgerPiece> Pieces = new List<LedgerPiece>(24);
        private static readonly List<LedgerQuote> Quotes = new List<LedgerQuote>(160);
        private static readonly List<LedgerQuote> Fallers = new List<LedgerQuote>(8);

        private static UiNavEntry[] PaperTabs()
        {
            return new[]
            {
                Item(UiIcon.Newspaper, "reallife_ledger_tab_front"),
                Item(UiIcon.ChartLineUp, "reallife_ledger_tab_markets"),
                Item(UiIcon.ListChecks, "reallife_ledger_tab_prices")
            };
        }

        // What the paper is in the game's own state now, so the page is laid again when it changes.
        private static int PaperStamp()
        {
            LedgerDirector director = LedgerDirector.Instance;
            LedgerEdition edition = director != null ? director.Latest : null;
            LedgerSpecial special = director != null ? director.ActiveSpecial : null;
            return (edition != null ? edition.Day * 7 + edition.Pieces.Count : 0) + (special != null ? special.Day * 13 + 5000 : 0);
        }

        private void FillPaper(NewsModel model)
        {
            model.Icon = UiIcon.Newspaper;
            model.Title = OutlookText.T("reallife_ledger_title");
            LedgerDirector director = LedgerDirector.Instance;
            LedgerSpecial special = director != null ? director.ActiveSpecial : null;
            if (special != null)
            {
                FillSpecial(model, special);
                return;
            }
            LedgerEdition edition = director != null ? director.Latest : null;
            if (edition == null)
            {
                model.Shape = NewsShape.Notice;
                model.EmptyTitle = OutlookText.T("reallife_news_paper_quiet");
                model.EmptyBody = OutlookText.T("reallife_news_paper_quiet_body");
                return;
            }
            OutlookBook book = OutlookDirector.Instance != null ? OutlookDirector.Instance.History : null;
            model.Across = true;
            model.Nav = PaperTabs();
            model.StripNote = Strip(edition.Day, director.CopyPrice);
            if (model.Tab == 1)
            {
                model.Shape = NewsShape.PaperMarkets;
                model.Edition = MarketsEdition(edition, book);
                return;
            }
            if (model.Tab == 2)
            {
                model.Shape = NewsShape.PaperPrices;
                model.Shares = SharesTable(edition);
                model.Note = edition.HasMarket ? LedgerText.T("reallife_ledger_prices_legend") : string.Empty;
                model.EmptyTitle = LedgerText.T("reallife_ledger_no_market_head");
                model.EmptyBody = LedgerText.T("reallife_ledger_no_market");
                return;
            }
            model.Shape = NewsShape.Paper;
            model.Edition = FrontEdition(edition, book);
        }

        // "Tuesday, day 289 · Year 3, number 1,102 · Price $2": the day, the year and the number of the edition, the price of a copy.
        private static string Strip(int day, int price)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            return NewsText.StripLine(new[]
            {
                LedgerText.Fill("reallife_ledger_dateline", "weekday", LedgerPage.Weekday(day), "day", Math.Max(1, day).ToString(culture)),
                LedgerText.Fill("reallife_ledger_dateline_number", "year", NewsText.YearOf(day, GameCalendar.DaysPerYear).ToString(culture),
                    "number", NewsText.Thousands(NewsRules.EditionOf(day), culture)),
                LedgerText.Fill("reallife_ledger_dateline_price", "price", LedgerText.Money(price))
            });
        }

        // The front page: the lead as the hero, the rates and the index beside it, the sections in columns, the markets band under them.
        private static UiEditionModel FrontEdition(LedgerEdition edition, OutlookBook book)
        {
            LedgerPiece lead = LedgerRules.Lead(edition);
            if (lead == null)
            {
                lead = Heaviest(edition);
            }
            var sections = new List<UiEditionSection>(6);
            Section(sections, edition, lead, UiIcon.MapPin, "reallife_ledger_sec_city", StoriesMost, LedgerSection.City);
            Section(sections, edition, lead, UiIcon.Chart, "reallife_ledger_sec_economy", StoriesMost, LedgerSection.Economy);
            Section(sections, edition, lead, UiIcon.Store, "reallife_ledger_sec_business", StoriesMost, LedgerSection.Business);
            Section(sections, edition, lead, UiIcon.People, "reallife_ledger_sec_around", BriefsMost, LedgerSection.People, LedgerSection.AroundTown);
            Section(sections, edition, lead, UiIcon.Cloud, "reallife_ledger_sec_weather", 2, LedgerSection.Weather);
            var model = new UiEditionModel
            {
                LeadIcon = lead != null ? SectionIcon(lead.Section) : UiIcon.Newspaper,
                LeadTint = UiRole.Accent,
                LeadKicker = lead != null ? SectionName(lead.Section) : LedgerText.T(LedgerCatalogue.SectionKeys[(int)LedgerSection.City]),
                LeadHeadline = lead != null ? PieceHead(lead) : LedgerText.T("reallife_ledger_quiet_head"),
                LeadStandfirst = lead != null ? PieceBody(lead) : LedgerText.T("reallife_ledger_quiet_body"),
                Markets = RatesBox(edition, book),
                Sections = sections.ToArray(),
                QuietLine = sections.Count < 3 ? OutlookText.T("reallife_news_paper_quiet_body") : null
            };
            MarketsBand(model, edition);
            return model;
        }

        private static LedgerPiece Heaviest(LedgerEdition edition)
        {
            LedgerPiece best = null;
            for (int i = 0; i < edition.Pieces.Count; i++)
            {
                if (edition.Pieces[i] != null && (best == null || edition.Pieces[i].Weight > best.Weight))
                {
                    best = edition.Pieces[i];
                }
            }
            return best;
        }

        // A section of the front page: its stories from one or two sections of the paper, the lead left to its hero.
        private static void Section(List<UiEditionSection> into, LedgerEdition edition, LedgerPiece lead, UiIcon icon, string labelKey, int most,
            LedgerSection first, LedgerSection second = LedgerSection.Count)
        {
            var stories = new List<UiEditionStory>(most);
            LedgerRules.Of(edition, first, lead, Pieces);
            for (int i = 0; i < Pieces.Count && stories.Count < most; i++)
            {
                stories.Add(Story(Pieces[i]));
            }
            if (second != LedgerSection.Count)
            {
                LedgerRules.Of(edition, second, lead, Pieces);
                for (int i = 0; i < Pieces.Count && stories.Count < most; i++)
                {
                    stories.Add(Story(Pieces[i]));
                }
            }
            if (stories.Count > 0)
            {
                into.Add(new UiEditionSection { Icon = icon, Label = LedgerText.T(labelKey), Stories = stories.ToArray() });
            }
        }

        // The box beside the hero: the city index with its line, the bank rate, inflation and the state of the economy.
        private static UiFigureModel[] RatesBox(LedgerEdition edition, OutlookBook book)
        {
            var figures = new List<UiFigureModel>(4)
            {
                IndexFigure(edition, OutlookText.T("reallife_news_series_index"))
            };
            if (edition.RateBp >= 0)
            {
                figures.Add(new UiFigureModel
                {
                    Label = OutlookText.T("reallife_news_series_rate"),
                    Value = LedgerText.Rate(edition.RateBp),
                    Trend = SparkOf(book, OutlookCatalogue.Rate, 12)
                });
            }
            if (edition.InflationBp != int.MinValue)
            {
                figures.Add(new UiFigureModel
                {
                    Label = OutlookText.T("reallife_news_series_inflation"),
                    Value = LedgerText.Rate(edition.InflationBp),
                    Trend = SparkOf(book, OutlookCatalogue.Inflation, 12)
                });
            }
            if (!string.IsNullOrEmpty(edition.CycleKey))
            {
                figures.Add(new UiFigureModel { Label = OutlookText.T("reallife_news_the_economy"), Value = LedgerText.T(edition.CycleKey) });
            }
            return figures.ToArray();
        }

        private static UiFigureModel IndexFigure(LedgerEdition edition, string label)
        {
            if (!edition.HasMarket)
            {
                return new UiFigureModel { Label = label, Value = "-" };
            }
            var line = new float[edition.IndexCloses.Count];
            for (int i = 0; i < line.Length; i++)
            {
                line[i] = edition.IndexCloses[i];
            }
            return new UiFigureModel
            {
                Label = label,
                Value = LedgerPage.Level(edition.IndexLevel),
                Change = Move(edition.IndexChangeBp),
                Rising = edition.IndexChangeBp > 0,
                Good = edition.IndexChangeBp >= 0,
                Trend = line.Length > 1 ? line : null
            };
        }

        // "0.80%", with the minus where it fell: the sign is the arrow's, the figure is the move.
        private static string Move(int bp)
        {
            return (Math.Abs(bp) / 100f).ToString("0.00", CultureInfo.CurrentCulture) + "%";
        }

        // The markets band under the columns: the city index, then the session's biggest risers and fallers with their lines.
        private static void MarketsBand(UiEditionModel model, LedgerEdition edition)
        {
            if (!edition.HasMarket)
            {
                return;
            }
            var band = new List<UiFigureModel>(5) { IndexFigure(edition, OutlookText.T("reallife_news_series_index")) };
            LedgerRules.Movers(edition, true, BandMovers, Quotes);
            for (int i = 0; i < Quotes.Count; i++)
            {
                band.Add(QuoteFigure(Quotes[i]));
            }
            LedgerRules.Movers(edition, false, BandMovers, Fallers);
            for (int i = 0; i < Fallers.Count; i++)
            {
                band.Add(QuoteFigure(Fallers[i]));
            }
            model.BandTitle = OutlookText.T("reallife_news_band_title");
            model.BandNote = OutlookText.T("reallife_news_band_note", "day", edition.MarketDay.ToString(Inv));
            model.Band = band.ToArray();
        }

        private static UiFigureModel QuoteFigure(LedgerQuote q)
        {
            return new UiFigureModel
            {
                Label = q.Name + " " + q.Ticker,
                Value = "$" + LedgerText.Figure(q.Close),
                Change = Move(q.ChangeBp),
                Rising = q.ChangeBp > 0,
                Good = q.ChangeBp >= 0,
                Trend = CloseLine(q.Ticker)
            };
        }

        // A company's last closes, from the market's own daily bars; null while it has too few.
        private static float[] CloseLine(string ticker)
        {
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            if (state == null || string.IsNullOrEmpty(ticker))
            {
                return null;
            }
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock stock = state.Stocks[i];
                if (stock == null || stock.Ticker != ticker || stock.Daily.Count < 2)
                {
                    continue;
                }
                int n = Math.Min(20, stock.Daily.Count);
                var line = new float[n];
                for (int d = 0; d < n; d++)
                {
                    line[d] = stock.Daily[stock.Daily.Count - n + d].C;
                }
                return line;
            }
            return null;
        }

        // The markets page: the market's lead story, the index beside it, the market's other stories, the session's movers and its sectors.
        private static UiEditionModel MarketsEdition(LedgerEdition edition, OutlookBook book)
        {
            LedgerRules.Of(edition, LedgerSection.Markets, null, Pieces);
            LedgerPiece lead = Pieces.Count > 0 ? Pieces[0] : null;
            var model = new UiEditionModel
            {
                LeadIcon = UiIcon.ChartLineUp,
                LeadTint = UiRole.Accent,
                LeadKicker = LedgerText.T(LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets]),
                LeadHeadline = lead != null ? PieceHead(lead) : LedgerText.T(edition.HasMarket ? "reallife_ledger_market_quiet_head" : "reallife_ledger_no_market_head"),
                LeadStandfirst = lead != null ? PieceBody(lead) : LedgerText.T(edition.HasMarket ? "reallife_ledger_market_quiet" : "reallife_ledger_no_market")
            };
            var figures = new List<UiFigureModel>(4) { IndexFigure(edition, OutlookText.T("reallife_news_series_index")) };
            if (edition.HasMarket)
            {
                figures.Add(new UiFigureModel
                {
                    Label = LedgerText.Fill("reallife_ledger_week", "change", string.Empty).Trim(),
                    Value = LedgerText.Change(edition.IndexWeekBp),
                    Good = edition.IndexWeekBp >= 0
                });
                SectorFigures(edition, figures);
            }
            model.Markets = figures.ToArray();
            var stories = new List<UiEditionStory>(8);
            for (int i = 1; i < Pieces.Count && stories.Count < 8; i++)
            {
                stories.Add(Story(Pieces[i]));
            }
            var sections = new List<UiEditionSection>(1);
            if (stories.Count > 0)
            {
                sections.Add(new UiEditionSection { Icon = UiIcon.ChartLineUp, Label = LedgerText.T("reallife_ledger_sec_markets"), Stories = stories.ToArray() });
            }
            model.Sections = sections.ToArray();
            if (edition.HasMarket)
            {
                model.NoticesTitle = OutlookText.T("reallife_news_movers");
                model.Notices = MoverNotices(edition);
                model.Band = SectorBand(edition);
                model.BandTitle = OutlookText.T("reallife_news_sectors");
                model.BandNote = OutlookText.T("reallife_news_band_note", "day", edition.MarketDay.ToString(Inv));
            }
            return model;
        }

        // The best and the worst sector of the session as two lines beside the index.
        private static void SectorFigures(LedgerEdition edition, List<UiFigureModel> into)
        {
            LedgerSector best = null;
            LedgerSector worst = null;
            for (int i = 0; i < edition.Sectors.Count; i++)
            {
                LedgerSector s = edition.Sectors[i];
                best = best == null || s.ChangeBp > best.ChangeBp ? s : best;
                worst = worst == null || s.ChangeBp < worst.ChangeBp ? s : worst;
            }
            if (best != null)
            {
                into.Add(new UiFigureModel { Label = LedgerText.T(best.Key), Value = LedgerText.Change(best.ChangeBp), Good = best.ChangeBp >= 0 });
            }
            if (worst != null && worst != best)
            {
                into.Add(new UiFigureModel { Label = LedgerText.T(worst.Key), Value = LedgerText.Change(worst.ChangeBp), Good = worst.ChangeBp >= 0 });
            }
        }

        private static UiEditionNotice[] MoverNotices(LedgerEdition edition)
        {
            var notices = new List<UiEditionNotice>(9);
            LedgerRules.Movers(edition, true, 3, Quotes);
            for (int i = 0; i < Quotes.Count; i++)
            {
                notices.Add(new UiEditionNotice { Icon = UiIcon.TrendUp, Text = Quotes[i].Name, Value = LedgerText.Change(Quotes[i].ChangeBp) });
            }
            LedgerRules.Movers(edition, false, 3, Fallers);
            for (int i = 0; i < Fallers.Count; i++)
            {
                notices.Add(new UiEditionNotice { Icon = UiIcon.TrendDown, Text = Fallers[i].Name, Value = LedgerText.Change(Fallers[i].ChangeBp) });
            }
            LedgerRules.MostActive(edition, 3, Quotes);
            for (int i = 0; i < Quotes.Count; i++)
            {
                notices.Add(new UiEditionNotice { Icon = UiIcon.Chart, Text = Quotes[i].Name, Value = Quotes[i].Volume.ToString("N0", CultureInfo.CurrentCulture) });
            }
            return notices.ToArray();
        }

        private static UiFigureModel[] SectorBand(LedgerEdition edition)
        {
            var band = new List<UiFigureModel>(5);
            for (int i = 0; i < edition.Sectors.Count && band.Count < 5; i++)
            {
                LedgerSector s = edition.Sectors[i];
                band.Add(new UiFigureModel
                {
                    Label = LedgerText.T(s.Key),
                    Value = LedgerPage.Level(s.Level),
                    Change = Move(s.ChangeBp),
                    Rising = s.ChangeBp > 0,
                    Good = s.ChangeBp >= 0
                });
            }
            return band.ToArray();
        }

        // Every listed company's close and move, by sector as the paper prints them.
        private static UiDataTableModel SharesTable(LedgerEdition edition)
        {
            var rows = new List<UiDataRow>(160);
            if (edition.HasMarket)
            {
                LedgerRules.Listing(edition, Quotes);
                for (int i = 0; i < Quotes.Count; i++)
                {
                    LedgerQuote q = Quotes[i];
                    int way = q.ChangeBp > 0 ? 1 : q.ChangeBp < 0 ? -1 : 0;
                    string sector = q.Sector >= 0 && q.Sector < edition.Sectors.Count ? LedgerText.T(edition.Sectors[q.Sector].Key) : string.Empty;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { q.Name, q.Ticker + Flags(q.Flags), sector, LedgerText.Figure(q.Close), LedgerText.Change(q.ChangeBp), q.Volume.ToString("N0", CultureInfo.CurrentCulture) },
                        Values = new[] { 0, 0, 0, 0, way, 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                    });
                }
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_company"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_code"), Kind = UiCellKind.Text, Width = 96f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_sector"), Kind = UiCellKind.Text, Width = 192f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_close"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_move"), Kind = UiCellKind.Trend, Width = 128f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_shares"), Kind = UiCellKind.Figure, Width = 144f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = LedgerText.T("reallife_ledger_no_market_head"),
                EmptyBody = LedgerText.T("reallife_ledger_no_market"),
                EmptyIcon = UiIcon.ListChecks
            };
        }

        private static string Flags(int flags)
        {
            if (flags == 0)
            {
                return string.Empty;
            }
            return " " + ((flags & LedgerQuote.Bid) != 0 ? "b" : string.Empty) + ((flags & LedgerQuote.ExDividend) != 0 ? "x" : string.Empty)
                   + ((flags & LedgerQuote.New) != 0 ? "n" : string.Empty) + ((flags & LedgerQuote.Split) != 0 ? "s" : string.Empty);
        }

        // A front page of its own, in another feature's words: its lead, the sections after it, the notes down the side.
        private static void FillSpecial(NewsModel model, LedgerSpecial special)
        {
            model.Shape = NewsShape.Paper;
            model.Across = false;
            model.Nav = new UiNavEntry[0];
            model.Tab = 0;
            model.StripNote = NewsText.StripLine(new[]
            {
                LedgerText.Fill("reallife_ledger_dateline", "weekday", LedgerPage.Weekday(special.Day), "day", Math.Max(1, special.Day).ToString(CultureInfo.CurrentCulture)),
                special.DateMiddle,
                special.DateRight
            });
            var sections = new List<UiEditionSection>(4);
            string body = special.Body ?? string.Empty;
            for (int i = 0; i < special.Labels.Count && i < special.Texts.Count; i++)
            {
                var stories = new List<UiEditionStory>(special.Texts[i].Count);
                for (int k = 0; k < special.Texts[i].Count; k++)
                {
                    string text = special.Texts[i][k];
                    LedgerPage.FirstSentence(ref text, out string head);
                    stories.Add(new UiEditionStory { Headline = head, Body = text });
                }
                if (stories.Count > 0)
                {
                    sections.Add(new UiEditionSection { Icon = UiIcon.Newspaper, Label = special.Labels[i], Stories = stories.ToArray() });
                }
            }
            if (!string.IsNullOrEmpty(special.Notice))
            {
                sections.Add(new UiEditionSection
                {
                    Icon = UiIcon.Bell,
                    Label = special.NoticeHead,
                    Stories = new[] { new UiEditionStory { Headline = special.Notice } }
                });
            }
            var notices = new List<UiEditionNotice>(8);
            for (int i = 0; i < special.BoxLabels.Count && i < special.BoxLines.Count; i++)
            {
                for (int k = 0; k < special.BoxLines[i].Count; k++)
                {
                    string line = special.BoxLines[i][k];
                    int cut = line.IndexOf(": ", StringComparison.Ordinal);
                    notices.Add(new UiEditionNotice
                    {
                        Icon = UiIcon.Info,
                        Text = cut > 0 ? line.Substring(0, cut) : line,
                        Value = cut > 0 ? line.Substring(cut + 2) : null
                    });
                }
            }
            model.Edition = new UiEditionModel
            {
                LeadIcon = UiIcon.Newspaper,
                LeadTint = UiRole.Accent,
                LeadKicker = special.Kicker,
                LeadHeadline = special.Headline,
                LeadStandfirst = string.IsNullOrEmpty(special.Stand) ? body : special.Stand + " " + body,
                Sections = sections.ToArray(),
                NoticesTitle = special.BoxLabels.Count > 0 ? special.BoxLabels[0] : special.Byline,
                Notices = notices.ToArray()
            };
        }

        private static UiEditionStory Story(LedgerPiece piece)
        {
            return new UiEditionStory { Headline = PieceHead(piece), Body = PieceBody(piece) };
        }

        // A piece's headline: its own, or the first sentence of its story, so a story without a headline is still read.
        private static string PieceHead(LedgerPiece piece)
        {
            LedgerText.Words(piece, out _, out string headline, out string body);
            if (string.IsNullOrEmpty(headline))
            {
                LedgerPage.FirstSentence(ref body, out headline);
            }
            return headline;
        }

        // The rest of a story once its headline is taken: the whole of it where the headline was its own.
        private static string PieceBody(LedgerPiece piece)
        {
            LedgerText.Words(piece, out _, out string headline, out string body);
            if (string.IsNullOrEmpty(headline))
            {
                LedgerPage.FirstSentence(ref body, out _);
            }
            return body ?? string.Empty;
        }

        private static string SectionName(LedgerSection section)
        {
            int i = (int)section;
            return i < 0 || i >= LedgerCatalogue.SectionKeys.Length ? LedgerText.T(LedgerCatalogue.SectionKeys[0]) : LedgerText.T(LedgerCatalogue.SectionKeys[i]);
        }

        private static UiIcon SectionIcon(LedgerSection section)
        {
            switch (section)
            {
                case LedgerSection.Economy: return UiIcon.Chart;
                case LedgerSection.Business: return UiIcon.Store;
                case LedgerSection.Markets: return UiIcon.ChartLineUp;
                case LedgerSection.People: return UiIcon.People;
                case LedgerSection.AroundTown: return UiIcon.MapPin;
                case LedgerSection.Weather: return UiIcon.Cloud;
                default: return UiIcon.Newspaper;
            }
        }

        private static float[] SparkOf(OutlookBook book, string key, int span)
        {
            if (book == null || book.LastDay <= 0)
            {
                return null;
            }
            var raw = new int[span];
            if (book.Window(key, book.LastDay, span, raw) < 2)
            {
                return null;
            }
            var line = new float[span];
            float carry = 0f;
            bool any = false;
            for (int i = 0; i < span; i++)
            {
                if (raw[i] != OutlookCatalogue.NoValue)
                {
                    carry = raw[i];
                    any = true;
                }
                line[i] = carry;
            }
            return any ? line : null;
        }

        // An edition as one paper prints it: the lead first, then the sections that title carries (the weather every title has), each piece a heading and its words.
        private static UiReaderModel ReaderOf(LedgerEdition edition, int title)
        {
            string name = LedgerText.T(NewsstandCatalogue.NameKeys[title]);
            string day = LedgerText.Fill("reallife_ledger_dateline", "weekday", LedgerPage.Weekday(edition.Day), "day", Math.Max(1, edition.Day).ToString(CultureInfo.CurrentCulture));
            var blocks = new List<UiReaderBlock>(edition.Pieces.Count * 2);
            LedgerPiece lead = NewsstandRules.Lead(edition, title);
            if (lead != null)
            {
                AddPiece(blocks, lead);
            }
            LedgerSection[] sections = NewsstandCatalogue.Sections[title];
            for (int s = 0; s <= sections.Length; s++)
            {
                LedgerRules.Of(edition, s < sections.Length ? sections[s] : LedgerSection.Weather, lead, Pieces);   // the weather comes last, in every title
                for (int i = 0; i < Pieces.Count; i++)
                {
                    AddPiece(blocks, Pieces[i]);
                }
            }
            if (blocks.Count == 0)
            {
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = OutlookText.T("reallife_news_paper_quiet_body") });
            }
            return new UiReaderModel { Title = name + ", " + day, Blocks = blocks.ToArray() };
        }

        private static void AddPiece(List<UiReaderBlock> blocks, LedgerPiece piece)
        {
            blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Heading, Text = PieceHead(piece) });
            blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = PieceBody(piece) });
        }
    }
}
