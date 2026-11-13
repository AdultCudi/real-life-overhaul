using System;
using System.Linq;
using RealLifeOverhaul.Ui;

// The kit's second pass (13 Nov): a table cell's hover card, a row's blocks with their own buttons, sorting by any sortable column, a chart axis
// that keeps a least span, a search that shows the game's suggestion, the designer's catalogue that takes its rows from the frame, and what the
// screens that use them read. Run alone with --ui-kit2; the whole kit's checks (--ui) run it too.
internal static partial class Program
{
    private static void UiKit2Checks()
    {
        UiKit2Table();
        UiKit2Sort();
        UiKit2Chart();
        UiKit2Search();
        UiKit2Designer();
        UiKit2Sources();
    }

    private static UiDataTableModel UiKit2Warehouses(int rows, bool actions)
    {
        var t = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Warehouse", Kind = UiCellKind.Tile }, new UiDataColumn { Title = "Slots", Kind = UiCellKind.Figure, Width = 100f },
                new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = UiTokens.ControlSmall }
            },
            Rows = new UiDataRow[rows], RowCount = rows, BlockLines = 3, BlockActions = actions
        };
        for (int i = 0; i < rows; i++)
        {
            t.Rows[i] = new UiDataRow
            {
                Cells = new[] { "Warehouse " + i, "2", null }, Values = new[] { (int)UiIcon.Package, -1, (int)UiIcon.Locate }, Sub = "Street " + i,
                Blocks = new[]
                {
                    new UiDataBlock
                    {
                        Title = "Vehicle slots", Labels = new[] { "Slot 1, Van", "Slot 2" }, Values = new[] { "Sam", "No driver" }, Tones = new[] { UiRole.None, UiRole.DangerText },
                        Action = "Manage drivers", ActionIcon = UiIcon.Truck
                    },
                    new UiDataBlock
                    {
                        Title = "Inventory", Labels = new[] { "Flour (40)", "Eggs (12)", "Milk (8)" }, Values = new[] { "9 days", "3 days", "Run out" },
                        Tones = new[] { UiRole.None, UiRole.WarnText, UiRole.DangerText }, Action = "Show full inventory", ActionIcon = UiIcon.Package
                    }
                }
            };
        }
        return t;
    }

    private static void UiKit2Table()
    {
        // A row with blocks is taller by their head, their lines and their buttons' row; a table with none keeps its row.
        UiDataTableModel plain = UiKit2Warehouses(3, false);
        plain.BlockLines = 0;
        UiDataTableModel blocks = UiKit2Warehouses(3, true);
        float one = UiDataTable.RowHeight(plain), tall = UiDataTable.RowHeight(blocks);
        Check(one == 48f && tall >= one + UiTokens.PairRow * 4f + UiTokens.ControlSmall, "ui kit: a row with blocks takes the room of its head, its " + blocks.BlockLines + " lines and its buttons (" + one + " to " + tall + ")");

        var s = new UiScene();
        s.Begin("warehouses", 1920f, 1080f, true);
        UiDataTable.Lay(s, UiAt.Screen, blocks, 100f, 100f, 1200f, 800f, UiDefaultKeys.Keyboard);
        int acts = 0, rowActs = 0;
        bool inOrder = true;
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == UiDataTable.First + UiDataTable.RowAct)
            {
                rowActs++;
                inOrder &= s.Nodes[i].Arg / UiDataTable.ActsMost < 3 && s.Nodes[i].Arg % UiDataTable.ActsMost < 2;
            }
            acts += s.Nodes[i].Command == UiDataTable.First + UiDataTable.Act ? 1 : 0;
        }
        Check(rowActs == 6 && inOrder && acts == 3, "ui kit: each warehouse row has a button for each block, by row and block in its arg (" + rowActs + " buttons), and its place button still");
        bool lines = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            lines |= s.Items[i].Text == "Flour (40)" || s.Items[i].Text == "No driver";
        }
        Check(lines, "ui kit: a row's blocks write their lines");

        // A cell's hover card: a target over the card's cell where the row has none of its own, and the card on the layer over the page for the row hovered.
        var prices = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Product", Kind = UiCellKind.Text }, new UiDataColumn { Title = "Sold", Kind = UiCellKind.Figure, Width = 120f },
                new UiDataColumn { Title = "Price", Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth }
            },
            Rows = new UiDataRow[4], RowCount = 4
        };
        for (int i = 0; i < 4; i++)
        {
            prices.Rows[i] = new UiDataRow
            {
                Cells = new[] { "Good " + i, "30", "$2.00" }, Values = new[] { 0, -1, UiDataTable.CanLess | UiDataTable.CanMore },
                Card = i == 3 ? null : new UiDataCard { Column = 1, Title = "Sold last 7 days", Labels = new[] { "12 sold at $1.90", "18 sold at $2.00" } }
            };
        }
        s.Begin("prices", 1920f, 1080f, true);
        UiDataTable.Lay(s, UiAt.Screen, prices, 100f, 100f, 900f, 500f, UiDefaultKeys.Keyboard);
        int hovers = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            hovers += s.Nodes[i].Command == UiDataTable.First + UiDataTable.Hover ? 1 : 0;
        }
        bool cardShown = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            cardShown |= s.Items[i].Text == "12 sold at $1.90";
        }
        Check(hovers == 3 && !cardShown, "ui kit: a row with a card has a target over its cell and shows nothing until the pointer rests on it (" + hovers + " targets)");
        prices.Hover = 1;
        s.Begin("prices", 1920f, 1080f, true);
        UiDataTable.Lay(s, UiAt.Screen, prices, 100f, 100f, 900f, 500f, UiDefaultKeys.Keyboard);
        int over = -1;
        for (int i = 0; i < s.ItemCount; i++)
        {
            over = s.Items[i].Text == "18 sold at $2.00" ? i : over;
        }
        Check(over >= 0 && s.Items[over].Layer == 1 && s.Items[over].X >= 100f && s.Items[over].X + s.Items[over].W <= 1000f, "ui kit: the hovered row's card stands on the layer over the page, inside the table");
        prices.Hover = 3;
        s.Begin("prices", 1920f, 1080f, true);
        UiDataTable.Lay(s, UiAt.Screen, prices, 100f, 100f, 900f, 500f, UiDefaultKeys.Keyboard);
        bool none = true;
        for (int i = 0; i < s.ItemCount; i++)
        {
            none &= s.Items[i].Layer == 0;
        }
        Check(none, "ui kit: a hovered row that has no card draws none");
    }

    private static void UiKit2Sort()
    {
        var columns = new[]
        {
            new UiDataColumn { Title = "Name", Kind = UiCellKind.Text, Sortable = true }, new UiDataColumn { Title = "Price", Kind = UiCellKind.Figure, Sortable = true },
            new UiDataColumn { Title = "Chart", Kind = UiCellKind.Spark }
        };
        int column = -1;
        bool down = false;
        Check(UiDataSort.Press(columns, ref column, ref down, 1) && column == 1 && down, "ui kit: a first press on a figure's head sorts the highest first");
        Check(UiDataSort.Press(columns, ref column, ref down, 1) && column == 1 && !down, "ui kit: a second press on it turns the order round");
        Check(UiDataSort.Press(columns, ref column, ref down, 0) && column == 0 && !down, "ui kit: a first press on a name's head sorts from the start of the alphabet");
        Check(!UiDataSort.Press(columns, ref column, ref down, 2) && column == 0 && !UiDataSort.Press(columns, ref column, ref down, 9), "ui kit: a head that does not sort, or is not there, changes nothing");

        float none = UiDataSort.None;
        var t = new UiDataTableModel
        {
            Columns = columns, RowCount = 5, Selected = 2,
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { "pear", "3" }, Sorts = new[] { none, 3f }, Key = 1 },
                new UiDataRow { Cells = new[] { "Apple", string.Empty }, Sorts = new[] { none, none }, Key = 2 },
                new UiDataRow { Cells = new[] { "fig", "9" }, Sorts = new[] { none, 9f }, Key = 3 },
                new UiDataRow { Cells = new[] { "date", "3" }, Sorts = new[] { none, 3f }, Key = 4 },
                new UiDataRow { Cells = new[] { "plum", "1" }, Sorts = new[] { none, 1f }, Key = 5 }
            },
            SortColumn = 1, SortDown = true
        };
        UiDataSort.Sort(t);
        string order = string.Join(",", t.Rows.Select(r => r.Key));
        Check(order == "3,1,4,5,2", "ui kit: sorted by a figure the highest stands first, ties keep their order, a row with none stands last (" + order + ")");
        Check(t.Selected == 0, "ui kit: the chosen row keeps its row through a sort (" + t.Selected + ")");
        t.SortDown = false;
        UiDataSort.Sort(t);
        order = string.Join(",", t.Rows.Select(r => r.Key));
        Check(order == "5,1,4,3,2", "ui kit: turned round the lowest stands first and the row with none still last (" + order + ")");
        t.SortColumn = 0;
        UiDataSort.Sort(t);
        order = string.Join(",", t.Rows.Select(r => r.Key));
        Check(order == "2,4,3,1,5", "ui kit: a column of words sorts by its words, any case (" + order + ")");
        t.SortColumn = -1;
        UiDataSort.Sort(t);
        Check(string.Join(",", t.Rows.Select(r => r.Key)) == order, "ui kit: a table with no column chosen keeps the order it was given");
    }

    private static void UiKit2Chart()
    {
        var series = new UiSeries { Label = "Flour", Values = new[] { 100f, 100.4f, 99.7f, 100.2f } };
        var m = new UiChartModel { Kind = UiChartKind.Line, Points = new[] { "1", "2", "3", "4" }, Series = new[] { series }, Format = v => v.ToString("0") };
        UiChart.Scale(m, out float lo, out float hi, out float step);
        float tight = hi - lo;
        m.LeastSpan = 10f;
        UiChart.Scale(m, out lo, out hi, out step);
        Check(tight < 3f && hi - lo >= 10f && lo <= 95.5f && hi >= 104.5f, "ui kit: an index chart with a least span of 10 holds five points either side of 100 (" + lo + " to " + hi + ", without it " + tight + ")");
        series.Values = new[] { 100f, 80f, 130f };
        m.Series = new[] { series };
        UiChart.Scale(m, out float wlo, out float whi, out float wstep);
        Check(wlo <= 80f && whi >= 130f, "ui kit: a span that already holds the least is not widened (" + wlo + " to " + whi + ")");
        m.Kind = UiChartKind.Bars;
        series.Values = new[] { 3f };
        m.Series = new[] { series };
        m.LeastSpan = 100f;
        UiChart.Scale(m, out float blo, out float bhi, out float bstep);
        Check(blo == 0f, "ui kit: a bar chart keeps its foot at nothing whatever the least span says");
        string prices = ModSource(@"Ui\News\NewsLive.Prices.cs");
        Check(MemberText(prices, "private UiChartModel GoodsChart()").Contains("LeastSpan = GoodsLeastSpan"), "ui kit: the Economy goods chart, rebased to 100, sets a least span");
    }

    private static void UiKit2Search()
    {
        Check(UiDraw.GhostRest("sof", "sofa set") == "a set" && UiDraw.GhostRest("SOF", "sofa") == "a" && UiDraw.GhostRest("sofa", "sofa") == null && UiDraw.GhostRest("", "sofa") == null
              && UiDraw.GhostRest("sob", "sofa") == null && UiDraw.GhostRest("sof", null) == null, "ui kit: a suggestion adds what follows the typed words, and nothing when it does not begin with them");
        var s = new UiScene();
        s.Begin("search", 800f, 400f, true);
        UiDraw.Search(s, UiAt.Screen, 20f, 20f, 300f, "Search", "sof", null, 300, 0, "sofa set", "Tab");
        bool rest = false, key = false, typed = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            rest |= s.Items[i].Text == "a set";
            key |= s.Items[i].Text == "Tab";
            typed |= s.Items[i].Text == "sof";
        }
        Check(rest && key && typed, "ui kit: a search shows the suggestion after the typed words, and the key that takes it where the count stands");
        s.Begin("search", 800f, 400f, true);
        UiDraw.Search(s, UiAt.Screen, 20f, 20f, 300f, "Search", "sof", "12", 300);
        bool plain = true;
        for (int i = 0; i < s.ItemCount; i++)
        {
            plain &= s.Items[i].Text != "Tab" && s.Items[i].Text != "a set";
        }
        Check(plain, "ui kit: a search with no suggestion draws as it did");
        string host = ModSource(@"Ui\UiHost.cs"), typing = ModSource(@"Ui\IUiTyping.cs"), face = ModSource(@"Ui\Designer\DesignerFace.cs");
        Check(typing.Contains("interface IUiTypingGhost") && host.Contains("KeyCode.Tab") && host.Contains("TakeGhost()") && face.Contains("IUiTypingGhost")
              && face.Contains("DesignerGame.AcceptSuggestion()"), "ui kit: Tab in the designer's search takes the game's suggestion through the host");
        string game = ModSource(@"Ui\Designer\DesignerGame.Catalogue.cs");
        Check(MemberText(game, "public static string AcceptSuggestion()").Contains("onAutoCompleteConfirm?.Invoke"), "ui kit: the suggestion is taken as the game's own Tab takes it, through its confirm");
    }

    private static void UiKit2Designer()
    {
        Check(UiDesignerPanels.RowsFor(1080f) == UiDesignerPanels.Rows && UiDesignerPanels.RowsFor(720f) == UiDesignerPanels.Rows, "ui kit: the catalogue shows today's rows on the 1080 px frame and never fewer");
        Check(UiDesignerPanels.RowsFor(1440f) > UiDesignerPanels.RowsFor(1080f) && UiDesignerPanels.RowsFor(2160f) >= UiDesignerPanels.RowsFor(1440f), "ui kit: a taller frame shows more rows of tiles");
        int cols = UiDesignerPanels.Columns(UiDesignerPanels.CatalogueWidth), tall = UiDesignerPanels.RowsFor(1440f);
        UiDesignerPanels.Visible(200, cols, 0f, out int first, out int last, tall);
        float step = UiDesignerPanels.RowStep, most = ((200 + cols - 1) / cols - tall) * step;
        Check(first == 0 && last == cols * tall - 1 && UiDesignerPanels.Scrolled(200, cols, step * 1000f, tall) == most && UiDesignerPanels.Scrolled(3, cols, step * 5f, tall) == 0f,
            "ui kit: a tall frame's rows are in view from the top and the wheel stops at the last whole row");
        UiDesignerModel model = UiSamples.Designer(DesignerWords, "furniture");
        UiDesignerCatalogue c = model.Catalogue;
        c.CardCount = 120;
        c.Cards = new UiDesignerCard[120];
        for (int i = 0; i < 120; i++)
        {
            c.Cards[i] = new UiDesignerCard { Name = "Piece " + i, Price = "$1", Index = i };
        }
        UiFrame low = UiFrame.FullMenu(16f / 9f, "Designer"), high = UiFrame.FullMenu(4f / 3f, "Designer");
        UiDesignerPanels.PanelSize(model, low, out float lw, out float lh);
        UiDesignerPanels.PanelSize(model, high, out float hw, out float hh);
        Check(high.Height > low.Height && hh > lh, "ui kit: the catalogue's panel is taller in a taller frame (" + lh + " and " + hh + ")");
        var scene = new UiScene();
        UiDesignerPanels.Lay(scene, model, low, true, UiDefaultKeys.Keyboard);
        int lowShown = c.ShownLast - c.ShownFirst + 1;
        UiDesignerPanels.Lay(scene, model, high, true, UiDefaultKeys.Keyboard);
        int highShown = c.ShownLast - c.ShownFirst + 1;
        Check(lowShown == UiDesignerPanels.Columns(lw) * UiDesignerPanels.Rows && highShown > lowShown, "ui kit: the tall frame lays out more tiles (" + lowShown + " and " + highShown + ")");
    }

    private static void UiKit2Sources()
    {
        string pages = ModSource(@"Ui\Stocks\StocksBuild.Pages.cs"), build = ModSource(@"Ui\Stocks\StocksBuild.cs"), screen = ModSource(@"Ui\Stocks\StocksScreen.cs");
        string sheet = ModSource(@"Ui\Stocks\StocksScreen.Sheet.cs");
        Check(MemberText(build, "private static UiDataTableModel Head(UiWords words)").Split(new[] { "Sortable = true" }, StringSplitOptions.None).Length - 1 == 9
              && !MemberText(build, "private static UiDataTableModel Head(UiWords words)").Contains("Kind = UiCellKind.Spark, Width = 100f, Sortable"),
            "stocks: every column of the companies table but its spark sorts");
        Check(screen.Contains("UiDataSort.Press(shown.Columns, ref _column, ref _down, arg)") && MemberText(screen, "private int StockAt(int row)").Contains("laid.Rows[row].Key"),
            "stocks: a head sorts by its column and a pressed row opens the company the table shows in it");
        Check(MemberText(build, "private static UiDataTableModel Companies(").Contains("UiDataSort.Sort(table)"), "stocks: the companies are put in the chosen column's order");
        string futures = MemberText(pages, "public static void FuturesPage(");
        Check(futures.Contains("reallife_mkt_col_week") && futures.Contains("reallife_mkt_drv_col_held") && futures.Contains("reallife_mkt_col_gain") && !futures.Contains("m.Table2 =")
              && futures.Contains("FutureContract * FutureKeyBase") && MemberText(sheet, "private bool RowFuture(").Contains("laid.Rows[arg].Key"),
            "stocks: the futures page holds the held and the offered in one table with the week, held, worth and gain, each row opening what its key says");
        string bots = MemberText(pages, "public static void BotsPage(");
        Check(bots.Contains("b.DrawdownBp") && bots.Contains("b.Trades") && bots.Contains("reallife_mkt_bot_col_fall") && bots.Contains("reallife_mkt_bot_col_trades"),
            "stocks: the firms' table has each firm's deepest fall and its trades");
        string company = ModSource(@"Ui\Company\CompanyLive.ManagersSub.cs"), overview = ModSource(@"Ui\Company\CompanyLive.Overview.cs");
        Check(pages.Contains("UiClock.AtMinute(minute)") && !pages.Contains("ToString(\"00\") + \":\"") && company.Contains("UiClock.At(dec.Hour, 0)") && overview.Contains("UiClock.At(dec.Hour, 0)")
              && !company.Contains("dec.Hour.ToString(\"00\")") && !overview.Contains("dec.Hour.ToString(\"00\")"), "ui kit: the market's hours and a manager's decision's hour are written by the clock option");
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs"), port = ModSource(@"Ui\BizMan\BizManScreen.Portfolio.cs");
        Check(parts.Contains("Card = SoldCard(g)") && parts.Contains("inventory_pricing_sold_breakdown") && parts.Contains("SoldPrices = 8") && parts.Contains("t.Hover = _goodHover"),
            "bizman: a goods row's sold-per-price card lists up to eight prices as the game's tooltip does");
        Check(port.Contains("ManageDrivers/ManageDriversButton") && port.Contains("ShowFullInventory/ShowFullInventoryButton") && port.Contains("t.BlockActions = true")
              && port.Contains("UiDataTable.First + UiDataTable.RowAct"), "bizman: a warehouse row lists its slots and stock and presses the entry's own drivers and inventory buttons");
        string bizman = ModSource(@"Ui\BizManPages.cs");
        Check(bizman.Contains("Goods + UiDataTable.Hover") && bizman.Contains("UiDataTable.First + UiDataTable.RowAct"), "bizman: the new targets are among the page's known commands");
    }
}
