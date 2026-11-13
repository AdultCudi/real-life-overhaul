using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Ui;
using IoPath = System.IO.Path;

// The Economy app on the kit carries what its old pages did (the interface parity pass, 13 Nov 2026): a goods line only over the days that have a price, the
// cards and tiles of every tab, a table that is as tall as its rows, and nothing cut without a word.
internal static partial class Program
{
    private static void NewsParityChecks()
    {
        NewsParityRules();
        NewsParityLayout();
        NewsParitySources();
    }

    private static void NewsParityRules()
    {
        int none = DaySeriesCatalogue.NoValue;
        // A young save: nine days of prices in a window of 120.
        var window = new int[120];
        for (int i = 0; i < window.Length; i++)
        {
            window[i] = i < 111 ? none : 500 + i;
        }
        float[] line = NewsCardRules.Kept(window, 120, none, v => v / 100f, out int first);
        Check(first == 111 && line != null && line.Length == 9 && Math.Abs(line[0] - 6.11f) < 0.001f,
            "news parity: a goods line begins on the first day that has a price, so a nine day old save draws nine points, not 120 (" + (line == null ? -1 : line.Length) + ")");
        window[115] = none;
        line = NewsCardRules.Kept(window, 120, none, v => v / 100f, out first);
        Check(line.Length == 9 && Math.Abs(line[4] - line[3]) < 0.0001f, "news parity: a day without a reading after the first carries the reading before it");
        Check(NewsCardRules.Kept(new[] { none, none, none }, 3, none, v => v, out first) == null && first == -1, "news parity: a window with no reading has no line");
        Check(NewsCardRules.FirstKept(new[] { none, 5 }, 2, none) == 1 && NewsCardRules.FirstKept(null, 0, none) == -1, "news parity: the first kept day of a window");

        string up = NewsCardRules.Pill(true, 120, true, n => n + "bp", out int way);
        Check(up == "120bp" && way == 1, "news parity: a rise of 1.2 per cent is a rise in the pill's words");
        string small = NewsCardRules.Pill(true, 3, true, n => n + "bp", out way);
        Check(small == "0bp" && way == 0, "news parity: under 0.05 per cent is no change, as the old pill read it");
        string fall = NewsCardRules.Pill(true, -40, false, n => n.ToString(), out way);
        Check(fall == "40" && way == -1, "news parity: a fall in points gives its size and the way down");
        string unknown = NewsCardRules.Pill(false, 0, true, n => "x", out way);
        Check(unknown == "-" && way == 0, "news parity: a change the book has not kept so many days for is a dash");

        Check(Math.Abs(NewsCardRules.Share(1000f, 800f, 1200f) - 0.5f) < 0.0001f && NewsCardRules.Share(2000f, 800f, 1200f) == 1f && NewsCardRules.Share(100f, 800f, 1200f) == 0f
            && NewsCardRules.Share(5f, 5f, 5f) == 0f, "news parity: a meter's share runs from nought to one on its scale and never past it");
        Check(Math.Abs(NewsCardRules.Share(1000f, 600f, 1600f) - 0.4f) < 0.0001f, "news parity: the wholesale usual stands at two fifths of its 60 to 160 scale");
        Check(NewsCardRules.Step(-2) == 0f && NewsCardRules.Step(0) == 0.5f && NewsCardRules.Step(2) == 1f && NewsCardRules.Step(9) == 1f, "news parity: a department's five steps run left to right");
        Check(NewsCardRules.Capital("fresh produce") == "Fresh produce" && NewsCardRules.Capital(null) == null, "news parity: a kind of goods is written with its capital");

        var heights = new[] { 30f, 30f, 30f, 30f };
        int fit = NewsCardRules.RowsThatFit(heights, 70f, out int left);
        Check(fit == 2 && left == 2, "news parity: a room for two rows holds two and counts the two left");
        fit = NewsCardRules.RowsThatFit(heights, 10f, out left);
        Check(fit == 1 && left == 3, "news parity: a room under one row still shows the first");
        fit = NewsCardRules.RowsThatFit(heights, 500f, out left);
        Check(fit == 4 && left == 0, "news parity: a room for all shows all and counts none left");
    }

    // ------------------------------------------------------------------ the pages, laid

    private static NewsModel ParityModel(NewsShape shape, int tab)
    {
        var m = new NewsModel
        {
            App = NewsApp.Economy,
            Shape = shape,
            Tab = tab,
            Title = "Economy",
            Across = false,
            Nav = new UiNavEntry[8],
            Lead = "Prices are steady",
            Chip = "Steady",
            Body = "A line.",
            AdviceTitle = "What it means for you",
            StripNote = "Day 9"
        };
        for (int i = 0; i < 8; i++)
        {
            m.Nav[i] = new UiNavEntry { Icon = UiIcon.Chart, Label = "tab " + i };
        }
        m.Prompts = new UiPrompt[0];
        return m;
    }

    private static UiDataTableModel ParityTable(int rows, int columns)
    {
        var cols = new UiDataColumn[columns];
        for (int c = 0; c < columns; c++)
        {
            cols[c] = new UiDataColumn { Title = "col" + c, Kind = c == 0 ? UiCellKind.Text : UiCellKind.Figure, Width = c == 0 ? 0f : 96f };
        }
        var list = new UiDataRow[rows];
        for (int r = 0; r < rows; r++)
        {
            var cells = new string[columns];
            for (int c = 0; c < columns; c++)
            {
                cells[c] = c == 0 ? "good " + r : "1." + r;
            }
            list[r] = new UiDataRow { Cells = cells, Values = new int[columns], Tones = new UiRole[columns] };
        }
        return new UiDataTableModel { Columns = cols, Rows = list, RowCount = rows };
    }

    private static UiChartModel ParityChart(string title)
    {
        return new UiChartModel
        {
            Kind = UiChartKind.Line,
            Title = title,
            Points = new[] { "1", "2", "3" },
            Series = new[] { new UiSeries { Label = "a", Values = new[] { 1f, 2f, 3f } } },
            Format = v => v.ToString("0")
        };
    }

    private static NewsPanel ParityPanel(string title, int rows, bool bars)
    {
        var list = new NewsRow[rows];
        for (int i = 0; i < rows; i++)
        {
            list[i] = new NewsRow { Label = title + " row " + i, Value = "+" + i + "%", Bar = bars, Share = 0.4f, MidAt = bars ? 0.5f : -1f, Detail = i == 1 ? "a quiet line" : null };
        }
        return new NewsPanel { Title = title, Note = "A quiet sentence about " + title + ".", Rows = list, Foot = "A foot line", MoreText = "and {n} more" };
    }

    private static UiScene LayParity(NewsModel m, UiFrame frame)
    {
        var scene = new UiScene();
        NewsPages.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard);
        return scene;
    }

    // The height of the card whose title is on the page: the band box that starts a step above the title's text and holds it.
    private static float ParityCardHeight(UiScene scene, string title)
    {
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if (scene.Items[i].Kind != UiKind.Text || scene.Items[i].Text != title)
            {
                continue;
            }
            float best = -1f;
            for (int j = 0; j < scene.ItemCount; j++)
            {
                ref UiItem b = ref scene.Items[j];
                if (b.Kind == UiKind.Box && b.Fill == UiRole.Band && b.X <= scene.Items[i].X && b.Y <= scene.Items[i].Y && b.X + b.W >= scene.Items[i].X && b.Y + b.H >= scene.Items[i].Y
                    && scene.Items[i].Y - b.Y < UiTokens.S6 && (best < 0f || b.H < best))
                {
                    best = b.H;
                }
            }
            return best;
        }
        return -1f;
    }

    private static bool ParityHasText(UiScene scene, string part)
    {
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if (scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text != null && scene.Items[i].Text.Contains(part))
            {
                return true;
            }
        }
        return false;
    }

    private static int ParityCommands(UiScene scene, int command)
    {
        int n = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            n += scene.Nodes[i].Command == command ? 1 : 0;
        }
        return n;
    }

    private static void NewsParityLayout()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f);

        // Prices: a table of three goods stands as tall as its rows, and the page carries the charts, the three cards and the moves.
        NewsModel prices = ParityModel(NewsShape.Prices, 1);
        prices.Table = ParityTable(3, 5);
        prices.TableTitle = "Shops goods";
        prices.Charts = new[] { ParityChart("Price index"), ParityChart("Inflation"), ParityChart("Bank rate") };
        prices.SideChart = ParityChart("Goods that moved");
        prices.Panels = new[] { ParityPanel("Costs card", 3, true), ParityPanel("Bank card", 3, false), ParityPanel("Wholesale card", 6, true) };
        prices.Moves = new[] { new NewsMove { Mark = "AB", Head = "Moved", Detail = "a line" } };
        prices.MovesTitle = "Moved this week";
        UiScene scene = LayParity(prices, frame);
        float table = ParityCardHeight(scene, "Shops goods");
        float wholesale = ParityCardHeight(scene, "Wholesale card");
        Check(table > 0f && wholesale > 0f && table < wholesale, "news parity: a table of three goods stands in a card as tall as its rows (" + table + "), not as tall as the page (" + wholesale + ")");
        Check(ParityHasText(scene, "Costs card") && ParityHasText(scene, "Bank card") && ParityHasText(scene, "Wholesale card") && ParityHasText(scene, "Price index")
            && ParityHasText(scene, "Inflation") && ParityHasText(scene, "Bank rate") && ParityHasText(scene, "Moved this week"),
            "news parity: the prices page carries the index, inflation and rate charts and the costs, bank and wholesale cards");
        prices.Panels = new NewsPanel[0];
        prices.Charts = new UiChartModel[0];
        prices.SideChart = null;
        scene = LayParity(prices, frame);
        Check(ParityHasText(scene, "Shops goods") && !ParityHasText(scene, "Costs card"), "news parity: a prices page with no card or chart still lays its table, and leaves them out, never empty");

        // Overview: the tiles are there with their changes, a press on one opens its tab, the facts and the effects line stand over them.
        NewsModel overview = ParityModel(NewsShape.Overview, 0);
        overview.Tiles = new NewsTile[5];
        for (int i = 0; i < 5; i++)
        {
            overview.Tiles[i] = new NewsTile
            {
                Label = "tile" + i, Value = "10" + i, Short = "+1%  7d", Long = "-2%  30d", ShortWay = 1, LongWay = -1, ShortTone = UiRole.OkText, LongTone = UiRole.DangerText,
                Line = new[] { 1f, 2f, 3f }, Tab = i + 1
            };
        }
        overview.Facts = new[] { "Month 2 of this state", "Rate decision in 3 days", "Busiest district: Downtown" };
        overview.FactsNote = "Footfall +2%, spending +1%";
        overview.Chart = ParityChart("Chart");
        overview.Advice = new[] { new NewsAdvice { Icon = UiIcon.Lightbulb, Title = "advice", Body = "body" }, new NewsAdvice { Icon = UiIcon.Lightbulb, Title = "advice two", Body = "body" } };
        overview.AdviceMore = "All 24 insights";
        scene = LayParity(overview, frame);
        Check(ParityCommands(scene, NewsCommands.Tab) == 5 && ParityHasText(scene, "tile4") && ParityHasText(scene, "Month 2 of this state") && ParityHasText(scene, "Footfall +2%"),
            "news parity: all eight tiles of the overview are there, the five beside the band each a way into its tab, with the facts over them");
        Check(!ParityHasText(scene, "All 24 insights"), "news parity: the link to every insight is there only when the card holds fewer than there are");
        var many = new NewsAdvice[24];
        for (int i = 0; i < many.Length; i++)
        {
            many[i] = new NewsAdvice { Icon = UiIcon.Lightbulb, Title = "advice " + i, Body = "body of the advice that takes a line or two to say", Link = "Open" };
        }
        overview.Advice = many;
        scene = LayParity(overview, frame);
        Check(ParityHasText(scene, "All 24 insights") && ParityCommands(scene, NewsCommands.Tab) == 6, "news parity: with more advice than the card holds the overview links to the Insights tab");

        // Insights: more findings than the room holds are counted, in columns, never dropped without a word.
        NewsModel insights = ParityModel(NewsShape.Insights, 6);
        insights.Advice = many;
        insights.AdviceMore = "and {n} more";
        insights.Note = "The findings";
        scene = LayParity(insights, frame);
        int shown = 0;
        for (int i = 0; i < many.Length; i++)
        {
            shown += ParityHasText(scene, "advice " + i) ? 1 : 0;
        }
        bool counted = shown == many.Length;
        for (int i = 1; i < many.Length && !counted; i++)
        {
            counted = ParityHasText(scene, "and " + i + " more") && shown + i == many.Length;
        }
        Check(shown > 6 && counted, "news parity: the insights stand in columns and what does not fit is counted (" + shown + " of " + many.Length + ")");

        // Jobs, finances and districts lay their panels, charts and places.
        NewsModel jobs = ParityModel(NewsShape.Jobs, 2);
        jobs.Chart = ParityChart("Unemployment");
        jobs.Charts = new[] { ParityChart("Vacancies") };
        jobs.Panels = new[] { ParityPanel("Hiring card", 3, false) };
        jobs.Table = ParityTable(6, 3);
        jobs.TableTitle = "Incomes";
        scene = LayParity(jobs, frame);
        Check(ParityHasText(scene, "Unemployment") && ParityHasText(scene, "Vacancies") && ParityHasText(scene, "Hiring card") && ParityHasText(scene, "Incomes"),
            "news parity: the jobs page carries the unemployment and vacancies charts, what it means to hire and the districts' incomes");
        NewsModel off = ParityModel(NewsShape.Jobs, 2);
        off.Table = ParityTable(4, 3);
        off.TableTitle = "Incomes";
        off.EmptyTitle = "No figures for jobs";
        off.EmptyBody = "They come from the careers.";
        scene = LayParity(off, frame);
        Check(ParityHasText(scene, "No figures for jobs") && ParityHasText(scene, "Incomes"), "news parity: with the jobs not counted the notice says so over the incomes, which are");

        NewsModel fin = ParityModel(NewsShape.Finances, 5);
        fin.Chart = ParityChart("Reserve");
        fin.Panels = new[] { ParityPanel("Books card", 3, true), ParityPanel("Departments card", 4, true), ParityPanel("Tax card", 2, false) };
        scene = LayParity(fin, frame);
        Check(ParityHasText(scene, "Books card") && ParityHasText(scene, "Departments card") && ParityHasText(scene, "Tax card") && ParityHasText(scene, "Reserve"),
            "news parity: finance carries the council's week, its departments and the tax beside the reserve");
        NewsModel council = ParityModel(NewsShape.Finances, 5);
        council.Panels = new[] { ParityPanel("Tax card", 2, false) };
        council.EmptyTitle = "The council's books have not started";
        council.EmptyBody = "They start soon.";
        scene = LayParity(council, frame);
        Check(ParityHasText(scene, "The council's books have not started") && ParityHasText(scene, "Tax card"), "news parity: with no council the tax stands beside what to do");

        NewsModel districts = ParityModel(NewsShape.Districts, 3);
        districts.Table = ParityTable(6, 8);
        districts.TableTitle = "Districts by pull";
        districts.Boards = new[] { ParityTable(3, 4), ParityTable(2, 5) };
        districts.BoardTitles = new[] { "Your businesses", "Your buildings" };
        scene = LayParity(districts, frame);
        Check(ParityHasText(scene, "Districts by pull") && ParityHasText(scene, "Your businesses") && ParityHasText(scene, "Your buildings"),
            "news parity: the districts' table stands over your businesses and your buildings");
        districts.Boards = new UiDataTableModel[0];
        districts.BoardTitles = new string[0];
        scene = LayParity(districts, frame);
        float alone = ParityCardHeight(scene, "Districts by pull");
        Check(alone > 0f && alone < frame.Height * 0.8f, "news parity: with no place of yours the districts' table is as tall as its rows (" + alone + ")");

        // The press on a tile or a link opens the tab it names, on a host with no source too.
        var host = new NewsHost("economy", NewsApp.Economy, UiIcon.Chart, null);
        Check(host.Press(NewsCommands.Tab, 4), "news parity: a tile's press is answered by the Economy window");
    }

    // ------------------------------------------------------------------ the sources

    private static void NewsParitySources()
    {
        string fill = File.ReadAllText(NewsRoot("Ui/News/NewsLive.cs"));
        string cards = File.ReadAllText(NewsRoot("Ui/News/NewsLive.Cards.cs"));
        string prices = File.ReadAllText(NewsRoot("Ui/News/NewsLive.Prices.cs"));
        string chart = MemberText(fill, "private UiSeries[] SeriesFor(OutlookBook book, NewsShape shape, int end, int span)");
        string retail = MemberText(prices, "private UiDataTableModel RetailTable(bool full)");
        Check(retail.Contains("NewsCardRules.Kept(") && !retail.Contains("float carry = today / 100f"),
            "news parity: a goods line is cut to the days that have a price, never today's price carried back over the days before the save");
        Check(retail.Contains("Move(today, month, out int monthWay)") && !retail.Contains("month == DaySeriesCatalogue.NoValue ? week : month"),
            "news parity: a month the book does not hold is blank, never the week's change under the month's head");
        Check(MemberText(fill, "private static UiDataTableModel GoodsColumns(List<UiDataRow> rows, bool full)").Contains("full && !yours"),
            "news parity: your price column is there only when you sell one of the goods");
        Check(!chart.Contains("OutlookCatalogue.Vacancies") && cards.Contains("OutlookCatalogue.Vacancies") && cards.Contains("reallife_econapp_chart_vacancies"),
            "news parity: the vacancies are read once, for a chart of their own, not read into a line and thrown away");
        string tile = MemberText(cards, "private NewsTile Tile(OutlookBook book, int tile)");
        Check(tile.Contains("Look.Short[tile]") && tile.Contains("Look.Long[tile]"), "news parity: a tile gives the change over a week and over a month, read as the old tile read them");
        Check(cards.Contains("reallife_econapp_costs_title") && cards.Contains("reallife_econapp_bank_title") && cards.Contains("reallife_econapp_wholesale_title")
            && cards.Contains("reallife_econapp_books_title") && cards.Contains("reallife_econapp_departments_title") && cards.Contains("reallife_econapp_tax_title")
            && cards.Contains("reallife_econapp_people_title") && cards.Contains("reallife_econapp_places_shops") && cards.Contains("reallife_econapp_places_estates"),
            "news parity: every card of the old Economy app has its kit card");
        Check(fill.Contains("model.Table = DistrictsFull(book);") && fill.Contains("_districtRows[row]"),
            "news parity: the districts' table is the full one and a press on a row opens the district it names, in the order the table lists them");
        Check(!fill.Contains("list.Count < 5"), "news parity: the advice is not cut at five, the Insights tab holds every finding");
        Check(!fill.Contains("private UiDataTableModel JobsTable()"), "news parity: the jobs page's two row table is gone for the districts' incomes");

        string register = File.ReadAllText(NewsRoot("Ui/UiScreens.News.cs"));
        bool all = true;
        foreach (string key in new[] { "reallife_econapp_costs_note", "reallife_econapp_bank_title", "reallife_econapp_wholesale_title", "reallife_econapp_people_hiring_easy",
            "reallife_econapp_tax_due", "reallife_news_col_level", "reallife_news_col_going", "reallife_news_col_customers", "reallife_news_col_game_value", "reallife_news_col_worth_today" })
        {
            all &= register.Contains("\"" + key + "\"");
        }
        Check(all, "news parity: the words of the new cards are on the Economy screens' key lists");
    }
}
