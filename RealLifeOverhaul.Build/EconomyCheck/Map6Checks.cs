using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The faults of the second look at the city map's dock, checked game-free: a fresh open reads the counts and the groups again though nothing looked while it was shut, the
// district page follows the district pointed at, and the game's building box is hidden by alpha and raycast alone and given back. Part of `--map5`.
internal static partial class Program
{
    private static void Map6Checks()
    {
        // The dock as the screen drives it: a layout reads the city's count when the look says it is stale. Nothing looks while the dock is hidden, so a close is never seen.
        var look = new MapLook();
        int city = 3, shown = -1;
        void Lay(float now)
        {
            look.Look(true, now);
            if (look.CountsStale)
            {
                shown = city;
                look.CountsStale = false;
            }
        }
        Lay(10f);
        Lay(10.25f);
        Check(shown == 3, "map dock: the first open counts the places on each line");
        city = 5;
        Lay(10.5f);
        Check(shown == 3, "map dock: while the map stays open the counts are not read again (the game is paused, nothing changes)");
        Lay(60f);
        Check(shown == 5, "map dock: opened again after a close (no look in between), the new count shows, not the first open's");
        look.GroupsStale = false;
        look.Look(true, 61f);
        Check(!look.GroupsStale, "map dock: a look a moment after the last one leaves the groups as they are");
        look.Look(true, 90f);
        Check(look.GroupsStale && look.CountsStale, "map dock: a look long after the last one finds the groups and counts stale, as a fresh open does");
        look.GroupsStale = look.CountsStale = false;
        look.Look(false, 91f);
        look.Look(true, 91.2f);
        Check(look.GroupsStale && look.CountsStale, "map dock: a close seen by a look and an open after it is a fresh open too");

        // The district page: the stamp it is drawn from moves with the district pointed at, and only on that page.
        int pointedA = 10, pointedB = 11;
        Check(MapLook.DistrictStamp(true, pointedA) != MapLook.DistrictStamp(true, pointedB), "map district page: pointing at a second district changes what the dock is drawn from");
        Check(MapLook.DistrictStamp(false, pointedA) == MapLook.DistrictStamp(false, pointedB), "map dock: the other pages do not relayout for a district pointed at");

        string live = Map5Source("Ui/MapDock/MapLive.cs");
        Check(live.Contains("DistrictOwner.KitStamp") && live.Contains("MapLook.DistrictStamp(true, DistrictOwner.KitStamp)"),
            "map district page: the dock's signature holds the district view's stamp while the district page shows");
        int at = live.IndexOf("DistrictOwner.KitPoint(", StringComparison.Ordinal);
        int dirty = at < 0 ? -1 : live.IndexOf("_dirty = true", at, StringComparison.Ordinal);
        Check(at >= 0 && dirty > at && dirty - at < 260 && live.Contains("DistrictOwner.KitStamp != stamp"),
            "map district page: pointing at a row sets the page dirty when the district pointed at changed");
        int lay = live.IndexOf("public void Lay(", StringComparison.Ordinal);
        int looked = lay < 0 ? -1 : live.IndexOf("_look.Look(", lay, StringComparison.Ordinal);
        int build = lay < 0 ? -1 : live.IndexOf("Build();", lay, StringComparison.Ordinal);
        Check(looked > lay && looked < build, "map dock: a layout looks at the map before it builds, so a fresh open is counted afresh");

        // The game's building box: hidden through the host's conceal (a canvas group's alpha and raycast), never switched off, and not for the subway list.
        string game = Map5Source("Ui/MapDock/MapFiltersGame.cs");
        string host = Map5Source("Ui/Host/UiConceal.cs");
        Check(game.Contains("conceal.Add(CardPanel())") && game.Contains("if (!_subway)") && !game.Contains("SetActive("),
            "map dock: the game's building box is hidden with the filters' dock, by the conceal alone, and never switched off");
        Check(host.Contains("Group.alpha = 0f") && host.Contains("blocksRaycasts = l.KeepBlocking && l.Blocks") && host.Contains("l.Group.alpha = l.Alpha") && !host.Contains("SetActive("),
            "map dock: the conceal gives the box its alpha and raycast back exactly as found when the host hides, switches off or is removed");

        // The minimap's destination text is joined only when its parts change.
        string dest = Map5Source("Navigation/NavHud.Destination.cs");
        Check(dest.Contains("minutes != _minutesShown || distance != _distanceShown"), "minimap: the minutes and distance are joined into one text only when a part changed");
        Map7Checks();
    }

    // The interface parity pass over the map, the places and the tables: the rules the tables of the Property and Services apps share, and the sources of what was dead or missing.
    private static void Map7Checks()
    {
        // A chip's or a layer's press on a line of a folded group: the game switches those lines off but they still answer, so the press goes through the switch that asks only
        // that its own object is on and not held.
        string widgets = Map5Source("Ui/Host/UiWidgets.cs");
        string hidden = MemberText(widgets, "public static bool SetHidden(");
        Check(hidden.Length > 0 && !hidden.Contains("activeInHierarchy") && hidden.Contains("activeSelf") && hidden.Contains("interactable") && hidden.Contains("t.isOn = on"),
            "map filters: a toggle of a folded group is set through itself when its own object is on and it is not held, as a click would");
        string actions = Map5Source("Ui/MapDock/MapLive.Actions.cs");
        Check(MemberText(actions, "public bool Chip(int index)").Contains("UiWidgets.SetHidden(line.Obj.Toggle, target)")
            && MemberText(actions, "public bool Layer(int index)").Contains("UiWidgets.SetHidden(layer.Line.Obj.Toggle"),
            "map filters: the quick chips and the layers panel's lines switch a line of a folded group");
        Check(!Enum.IsDefined(typeof(MapChipKind), "Parking") && (int)MapChipKind.Count == 6, "map filters: no Parking chip is kept, for the game has no line it could switch");
        Check(Map5Source("Ui/UiScreens.Map.cs").Contains("\"reallife_map_chip_fuel\""), "map filters: the Fuel chip's words are in the map screens' key list");

        // The place card and the route strip in taxi mode answer the taxi button, not Set destination.
        string place = Map5Source("Ui/MapDock/MapLive.Place.cs");
        Check(MemberText(place, "private bool BuildPlace()").Contains("InTaxiMode ? TaxiHeadline(card)") && MemberText(place, "private bool BuildPlace()").Contains("MayTravel(card)")
            && MemberText(place, "private void BuildRoute()").Contains("MayTravel(Card())") && place.Contains("InTaxiMode ? card.taxiTravelButton : card.setDestinationButton"),
            "map place card: in taxi mode its button and the route's Go are the taxi button's, enabled when the game shows it");

        // The subway list previews a station as the game's own row does, and gives the camera back.
        string live = Map5Source("Ui/MapDock/MapLive.cs");
        string point = MemberText(live, "private void PointStation(");
        Check(live.Contains("PointStation(command == UiMapFilters.StationCommand") && point.Contains("OnPointerEnter(null)") && point.Contains("OnPointerExit(null)") && point.Contains("HotSpots.Game()"),
            "map subway list: the pointer on a station runs the game's own row's preview and leaving runs its exit");

        // A building under the pointer on the open map is read into the dock's block without its buttons.
        string picked = Map5Source("Ui/MapDock/MapLive.Picked.cs");
        Check(picked.Contains("return ReadHover(ref p, card, target);") && MemberText(picked, "private bool ReadHover(").Contains("p.Hover = true") && picked.Contains("p.Hover = false;")
            && live.Contains("!picked && target != null ? target.GetInstanceID()"), "map dock: a building under the pointer shows its facts in the picked block, and the block follows the pointer");
        string filters = Map5Source("Ui/MapDock/UiMapFilters.cs");
        var hoverPanel = new MapPanelModel();
        hoverPanel.Place.DestinationLabel = "Set destination";
        hoverPanel.Place.CanBizMan = true;
        float pickedHeight = UiMapFilters.PickedHeight(hoverPanel, UiDefaultKeys.Keyboard);
        hoverPanel.Place.Hover = true;
        Check(UiMapFilters.PickedHeight(hoverPanel, UiDefaultKeys.Keyboard) < pickedHeight, "map dock: the block of a building only pointed at is shorter than a picked one's, with no row of buttons");
        Check(filters.Contains("if (!p.Hover)") && filters.Contains("if (p.Hover)"), "map dock: a pointed-at building's block draws no Clear link and no buttons");

        // The search lists the first five places and says how many more the words name.
        Check(MemberText(Map5Source("Ui/MapDock/MapLive.Lines.cs"), "private void BuildResults()").Contains("_resultsMore = matches - _results.Count") && filters.Contains("MoreHeight(m)")
            && filters.Contains("m.ResultsMore"), "map search: past five matches a line says how many more, in the dock's height and its layout");

        // The district block carries the figures the old card said, as sentences, as many as the panel has room for.
        var district = new MapDistrictModel { HasChosen = true, ChosenName = "Midtown", Chosen = new UiInfoRow[] { new UiInfoRow { Label = "Rent", Value = "$1" } } };
        float without = UiMapDistrict.Needed(district);
        district.ChosenNotes = new[] { "Shop rent about 30 a month for each square metre.", "Home rent about 20 a month for each square metre.", "Crime: 4 reports in the last 30 days." };
        Check(UiMapDistrict.Needed(district) > without, "map district block: its notes add to the height the panel asks for");
        Check(Map5Source("Districts/DistrictView.Kit.cs").Contains("CardLines(d, which)") && Map5Source("Districts/DistrictView.Card.cs").Contains("_notes.Add("),
            "map district block: the notes are the old card's own lines, written by the same code");

        // The private driver's cars past the panel's lines are paged, and a prompt with more buttons than the card holds stays the game's own.
        string driver = Map5Source("Ui/World/PrivateDriverKit.cs");
        Check(driver.Contains("_moreLine = M.LineCount") && driver.Contains("cars.Count > MostCars ? MostCars - 1 : MostCars") && driver.Contains("_page++;"),
            "private driver: a contract with more cars than the panel's lines pages them, so none is out of reach");
        string prompt = Map5Source("Ui/World/WorldPrompt.cs");
        Check(prompt.Contains("_overflow = _found.Count > UiPromptCardModel.MostActions") && MemberText(prompt, "public void Frame(UiFrame frame, bool on)").Contains("if (_overflow)"),
            "world prompt: more buttons than the card's places leaves the game's own box up with all of them");

        TableRuleChecks();
    }

    // The tables of the Property and Services apps: the order a head sets, the filters, the sums and the empty cell last, as the old tables did.
    private static void TableRuleChecks()
    {
        const long N = TableRules.None;
        UiDataRow Row(string name, long price, params int[] kinds) => new UiDataRow { Cells = new[] { name, price.ToString() }, SortKeys = new[] { N, price }, Kinds = kinds };
        var rows = new[] { Row("Cedar", 300, 0, 1), Row("alder", 100, 1, 0), Row("Birch", N, 0, 0), Row("Elm", 200, 1, 1), Row("Ash", 100, 0, 1) };
        int[] asWritten = TableRules.Order(rows, -1, false, null);
        Check(asWritten.Length == 5 && asWritten[0] == 0 && asWritten[4] == 4, "tables: with no column chosen the rows stay as written");
        int[] byName = TableRules.Order(rows, 0, false, null);
        Check(rows[byName[0]].Cells[0] == "alder" && rows[byName[1]].Cells[0] == "Ash" && rows[byName[4]].Cells[0] == "Elm", "tables: a text column orders by its words, capitals and small letters alike");
        int[] byPrice = TableRules.Order(rows, 1, false, null);
        Check(rows[byPrice[0]].Cells[0] == "alder" && rows[byPrice[1]].Cells[0] == "Ash" && rows[byPrice[3]].Cells[0] == "Cedar" && rows[byPrice[4]].Cells[0] == "Birch",
            "tables: a figure column orders by its keys, equal ones as written, and a row with none is last");
        int[] byPriceDown = TableRules.Order(rows, 1, true, null);
        Check(rows[byPriceDown[0]].Cells[0] == "Cedar" && rows[byPriceDown[3]].Cells[0] == "Ash" && rows[byPriceDown[4]].Cells[0] == "Birch", "tables: the other way round the row with none is still last");
        int[] kind = TableRules.Order(rows, 1, false, new[] { 1, -1 });
        Check(kind.Length == 2 && rows[kind[0]].Cells[0] == "alder" && rows[kind[1]].Cells[0] == "Elm", "tables: a filter keeps the rows of its kind, and the sort works on them");
        int[] both = TableRules.Order(rows, -1, false, new[] { 0, 1 });
        Check(both.Length == 2 && both[0] == 0 && both[1] == 4, "tables: two filters keep the rows both pass");
        Check(TableRules.Sum(rows, TableRules.Order(rows, -1, false, null), 1) == 700 && TableRules.Sum(rows, kind, 1) == 300, "tables: a column's sum is of the rows shown, and a row with none adds nothing");
        Check(TableRules.Next(-1, 3) == 0 && TableRules.Next(2, 3) == -1 && TableRules.Held(5, 3) == -1 && TableRules.Held(1, 3) == 1, "tables: a filter steps through each kind and back to all, and a choice for a kind that is gone falls back to all");
        string[] keysTaken = TableRules.Take(new[] { "a", "b", "c" }, new[] { 2, 0 });
        Check(keysTaken.Length == 2 && keysTaken[0] == "c" && keysTaken[1] == "a", "tables: the keys follow the rows through the order");

        // The Property screen's own presses: a head sorts by its column and a second press turns it, a filter steps, and a tab opens as written.
        var source = new SortableEstate();
        var screen = new EstateScreen(source, "estate");
        var scene = new UiScene();
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.Keyboard);
        Check(screen.Press(EstateFace.TableBase + UiDataTable.Head, 1) && screen.View.SortColumn == 1 && !screen.View.SortDown, "Property: a press on a sortable head sorts by it, lowest first");
        Check(screen.Press(EstateFace.TableBase + UiDataTable.Head, 1) && screen.View.SortDown, "Property: a second press on the same head turns the order");
        Check(!screen.Press(EstateFace.TableBase + UiDataTable.Head, 0), "Property: a head that does not sort is no target");
        Check(screen.Press(EstateFace.TableBase + UiDataTable.Filter, 0) && screen.View.Chosen[0] == 0 && screen.Press(EstateFace.TableBase + UiDataTable.Filter, 0) && screen.View.Chosen[0] == 1
            && screen.Press(EstateFace.TableBase + UiDataTable.Filter, 0) && screen.View.Chosen[0] == -1, "Property: a filter's press steps each kind in turn and then all again");
        screen.Press(EstateFace.TableBase + UiDataTable.Head, 1);
        screen.Press(UiAppWindow.NavCommand, 0);
        Check(screen.View.SortColumn == -1 && !screen.View.SortDown && screen.View.Chosen[0] == -1, "Property: a tab opened shows its table as written, with no filter");

        // The sources: every table of the Property app takes the old app's filters, sorts and sums; the Services tables sort and sum.
        string works = Map5Source("Ui/Estate/EstateLive.Works.cs");
        string market = Map5Source("Ui/Estate/EstateLive.Market.cs");
        string trade = Map5Source("Ui/Estate/EstateLive.Trade.cs");
        string mine = Map5Source("Ui/Estate/EstateLive.Mine.cs");
        Check(works.Contains("Present(_m.Table, keys, choices, sums, filteredBody)") && mine.Contains("Present(t, keys, new[] { Choice(\"reallife_tab_all_kinds\"")
            && market.Contains("Districts(rows, where, \"reallife_tab_all_districts\")") && market.Contains("\"reallife_tab_all_stages\"") && trade.Contains("\"reallife_estate_filter_all_classes\"")
            && works.Contains("\"reallife_estate_filter_all_states\"") && trade.Contains("new[] { 5, 6 }") && market.Contains("new[] { 1, 2 }"),
            "Property: the roll, market, tenants, works, agency, hotel and hire tables are given the old app's filters and their money columns' totals");
        Check(!market.Contains("rows.Count < 400") && works.Contains("_m.RowRuns = Ordered(locate)") && market.Contains("_m.RowRuns = Ordered(locate)") && trade.Contains("_m.RowRuns = Ordered(locate)"),
            "Property: the market is not cut at 400 rows before the sort, and the Locate buttons follow their rows through it");
        string services = Map5Source("Ui/Services/ServicesLive.Round.cs");
        Check(services.Contains("TableRules.Order(written, sort, down, null)") && services.Contains("new[] { 5 }") && services.Contains("new[] { 1 }") && Map5Source("Ui/Services/ServicesLive.Sell.cs").Contains("Arranged(columns, rows, keys, null"),
            "Services: the tables sort by every head and the crew's wages and the contracts' fees are summed");
        string svcScreen = Map5Source("Ui/Services/ServicesScreen.cs");
        Check(svcScreen.Contains("ServicesFace.TableBase + UiDataTable.Head") && svcScreen.Contains("_view.SortColumn = -1;"), "Services: a head's press sorts the table shown, and a tab opens it as written");

        // The tenant's page, the listing's repair cost and the guide lines.
        string unit = Map5Source("Ui/Estate/EstateLive.Unit.cs");
        string feeling = MemberText(unit, "private void Feeling(");
        Check(feeling.Contains("reallife_ui_estate_ten_happy_up") && feeling.Contains("ten_moans") && feeling.Contains("ten_quitting") && feeling.Contains("ten_plan") && feeling.Contains("ten_reminded") && unit.Contains("Feeling(h, n, u, p);"),
            "Property: a let unit's page says where the mood is heading, the complaints so far, the day they leave and the payment plan or reminder");
        Check(market.Contains("EstateRules.RepairCost(b.Base, cond, _c.Index)") && market.Contains("reallife_ui_estate_listing_how"), "Property: a listing says what bringing it to new costs and what its button does");
        Check(Map5Source("Ui/Estate/EstateLive.cs").Contains("m.Guide = GuideNote(tab, v.Pick)") && Map5Source("Ui/Services/ServicesLive.cs").Contains("m.Guide = label"), "Property and Services: each page names the Guide's page that explains it");
    }

    // A source that fills one sortable table and honours the view's sort and filter as the live page does, to press the screen against.
    private sealed class SortableEstate : IEstateSource
    {
        public int Generation => 0;

        public void Opened(EstateView view)
        {
        }

        public void Fill(EstateModel m, EstateView v)
        {
            m.Title = "Property";
            m.Nav = new[] { new UiNavEntry { Label = "Mine", Icon = UiIcon.Home }, new UiNavEntry { Label = "Tenants", Icon = UiIcon.People } };
            m.Tab = v.Tab;
            m.Shape = EstateShape.Table;
            m.HeadTitle = "Table";
            var cols = new[]
            {
                new UiDataColumn { Title = "Name", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Price", Kind = UiCellKind.Money, Width = 120f, Sortable = true }
            };
            var rows = new List<UiDataRow>();
            for (int i = 0; i < 4; i++)
            {
                rows.Add(new UiDataRow { Cells = new[] { "Row " + i, "$" + (100 * (4 - i)) }, SortKeys = new long[] { TableRules.None, 100 * (4 - i) }, Kinds = new[] { i % 2 } });
            }
            m.Choices = new[] { new TableChoice { All = "All", Options = new[] { "Even", "Odd" } } };
            m.Table = new UiDataTableModel { Columns = cols, Rows = rows.ToArray(), RowCount = rows.Count, Filters = new[] { m.Choices[0].Words(v.Chosen[0]) } };
            m.RowKeys = new[] { "a", "b", "c", "d" };
        }
    }
}
