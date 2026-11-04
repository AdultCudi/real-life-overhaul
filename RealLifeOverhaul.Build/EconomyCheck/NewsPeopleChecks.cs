using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // The Economy app's People page (the owner's report of 4 Nov 2026: the page opened as a panel over nothing, the links went nowhere, the figures read nought).

    // Plain words: a key's own name, money with a dollar, a district by its id. A figure's words are then the page's own.
    private sealed class NewsPlainWords : INewsWords
    {
        public PopulationState State;

        public string T(string key) => key;

        public string T(string key, string name, string value) => key + "|" + name + "=" + value;

        public string Money(int whole) => "$" + whole;

        public string District(int place) => State != null && place >= 0 && place < State.Districts.Count ? State.Districts[place].Id : "?";
    }

    // The page's source as the game's NewsLive keeps it, on the same path, the same rules and the same host: the page a tab shows, the people's page of a generated
    // city, a district's page, the renters and a household.
    private sealed class PeopleSource : INewsSource
    {
        public readonly NewsPath Path = new NewsPath();
        public NewsModel Last;
        public PopulationState State;
        public readonly NewsPlainWords Words = new NewsPlainWords();
        private int _local;
        private int[] _places = new int[0];
        private int[] _slots = new int[0];

        public int Generation => _local;

        public void Fill(NewsModel model)
        {
            Last = model;
            Words.State = State;
            model.App = NewsApp.Economy;
            model.Title = "Economy";
            model.Nav = new UiNavEntry[8];
            for (int i = 0; i < 8; i++)
            {
                model.Nav[i] = new UiNavEntry { Icon = UiIcon.Chart, Label = "tab " + i };
            }
            model.Prompts = new UiPrompt[0];
            model.BackLabel = null;
            model.PageTitle = null;
            PeopleTally t = PeopleTally.Read(State);
            Path.Busy = t.Busiest();
            switch (Path.Page(model.Tab))
            {
                case NewsPage.People:
                    model.Shape = NewsShape.People;
                    model.People = NewsPeopleRules.City(t, Words, out _places);
                    model.Actions = NewsPeopleRules.Links(t, Words);
                    break;
                case NewsPage.District:
                    model.Shape = NewsShape.People;
                    model.BackLabel = "people";
                    model.People = NewsPeopleRules.District(t, Path.District, Words);
                    model.People.Table = Renters(Path.District);
                    model.Actions = new UiActionModel[0];
                    break;
                case NewsPage.Renters:
                    model.Shape = NewsShape.Renters;
                    model.BackLabel = "people";
                    model.PageTitle = "renters";
                    model.Table = Renters(-1);
                    break;
                case NewsPage.Household:
                    model.Shape = NewsShape.Household;
                    model.BackLabel = Path.RentersOpen ? "renters" : "district";
                    model.Household = new UiInfoModel
                    {
                        Title = "household",
                        Sections = new[] { new UiInfoSection { Label = "household", Rows = new[] { new UiInfoRow { Label = "Rent a day", Value = "$1" } } } }
                    };
                    break;
                default:
                    model.Shape = NewsShape.Notice;
                    model.EmptyTitle = "tab " + model.Tab;
                    break;
            }
        }

        private UiDataTableModel Renters(int district)
        {
            var rows = new List<UiDataRow>();
            var slots = new List<int>();
            for (int h = 0; h < State.Households.Count && rows.Count < 12; h++)
            {
                if (State.Households.Alive(h) && (district < 0 || State.Households.District[h] == district) && State.Households.Landlord[h] != Landlords.Own)
                {
                    slots.Add(h);
                    rows.Add(new UiDataRow { Cells = new[] { "h" + h, "$1" }, Values = new int[2], Tones = new UiRole[2] });
                }
            }
            _slots = slots.ToArray();
            return new UiDataTableModel
            {
                Columns = new[] { new UiDataColumn { Title = "Name", Kind = UiCellKind.Text }, new UiDataColumn { Title = "Rent a day", Kind = UiCellKind.Text, Width = 120f } },
                Rows = rows.ToArray(),
                RowCount = rows.Count
            };
        }

        public bool Press(NewsModel model, int command, int arg)
        {
            bool row = command == UiDataTable.First + UiDataTable.Row || command == NewsCommands.District + UiDataTable.Row;
            if (command == NewsCommands.Back || command == NewsCommands.Rent || command == NewsCommands.Busy || command == UiAppWindow.NavCommand)
            {
                if (!Path.Press(command))
                {
                    return false;
                }
                _local++;
                return true;
            }
            if (row && (Path.RentersOpen || Path.DistrictOpen) && !Path.HomeOpen && arg >= 0 && arg < _slots.Length)
            {
                Path.OpenHome();
                _local++;
                return true;
            }
            if (row && model.Tab == NewsPath.PeopleTab && arg >= 0 && arg < _places.Length && Path.OpenDistrict(_places[arg]))
            {
                _local++;
                return true;
            }
            return false;
        }

        public bool Back()
        {
            if (!Path.Back())
            {
                return false;
            }
            _local++;
            return true;
        }
    }

    private static int NodesOf(UiScene s, int command)
    {
        int n = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            n += s.Nodes[i].Command == command ? 1 : 0;
        }
        return n;
    }

    private static void NewsPeopleChecks()
    {
        // The path of the window: its page, a step back, and the pages put away.
        var path = new NewsPath();
        Check(path.Page(0) == NewsPage.Tab && path.Page(NewsPath.PeopleTab) == NewsPage.People, "news people: the window shows its tab, and the people's page on the People tab");
        Check(!path.Press(NewsCommands.Back), "news people: Back with nothing open does nothing, so the window closes");
        Check(!path.Press(NewsCommands.Busy), "news people: the busiest district's link does nothing while nobody lives anywhere");
        Check(!path.OpenHome(), "news people: a household opens only from a list or a district");
        path.Busy = 3;
        Check(path.Press(NewsCommands.Rent) && path.Page(NewsPath.PeopleTab) == NewsPage.Renters, "news people: Who pays the most rent opens the renters");
        Check(path.OpenHome() && path.Page(NewsPath.PeopleTab) == NewsPage.Household, "news people: a renter opens its household");
        Check(path.Back() && path.Page(NewsPath.PeopleTab) == NewsPage.Renters && path.Back() && path.Page(NewsPath.PeopleTab) == NewsPage.People && !path.Back(),
            "news people: Back goes a step at a time, household, renters, the people's page");
        Check(path.Press(NewsCommands.Busy) && path.Page(NewsPath.PeopleTab) == NewsPage.District && path.District == 3, "news people: the busiest district's link opens that district");
        Check(!path.OpenDistrict(4) && !path.Press(NewsCommands.Busy), "news people: a district's page does not open another over it");
        Check(path.OpenHome() && path.Back() && path.Back() && path.Page(NewsPath.PeopleTab) == NewsPage.People, "news people: a household of a district's table goes back to the district, then to the page");
        path.OpenRenters();
        path.OpenHome();
        Check(path.Clear() && path.Page(0) == NewsPage.Tab && path.Page(NewsPath.PeopleTab) == NewsPage.People && !path.Clear(),
            "news people: closing the window puts every page away: it opens on its tab, never on a list, a household or a district left open");
        path.Press(NewsCommands.Rent);
        Check(path.Press(UiAppWindow.NavCommand) && path.Page(2) == NewsPage.Tab, "news people: a press on a tab puts the pages opened from another away");

        // The people's page of a generated city.
        PopulationMade(7001, out PopulationState city, out _);
        PeopleTally tally = PeopleTally.Read(city);
        var words = new NewsPlainWords { State = city };
        Check(tally.People == city.People.Live && tally.Households == city.Households.Live && tally.People > 10000 && tally.Jobs > 10000,
            "news people: the tally counts the city's records (" + tally.People + " people, " + tally.Households + " households, " + tally.Jobs + " jobs)");
        NewsPeople page = NewsPeopleRules.City(tally, words, out int[] places);
        var labels = new HashSet<string> { page.HeadLabel };
        bool once = true;
        foreach (UiInfoRow r in page.Rows)
        {
            once &= labels.Add(r.Label);
        }
        Check(once && page.Rows.Length == 6, "news people: no figure's label stands twice on the page (" + labels.Count + " labels)");
        Check(page.HeadValue != "0" && page.Rows[0].Value != "0" && page.Rows[1].Value != "0.0%" && page.Rows[2].Value != "$0" && page.Rows[3].Value != "0",
            "news people: the households, the work lost, the rent and the jobs are counted, never a nought");
        // A city just loaded: the district figures the day's turn works out are not in the save yet, and the page does not read them.
        foreach (DistrictFigures f in city.Figures)
        {
            f.Clear();
        }
        NewsPeople loaded = NewsPeopleRules.City(PeopleTally.Read(city), words, out _);
        bool same = loaded.HeadValue == page.HeadValue;
        for (int i = 0; i < page.Rows.Length; i++)
        {
            same &= loaded.Rows[i].Value == page.Rows[i].Value;
        }
        Check(same, "news people: a city just loaded, its day's figures not worked out yet, shows the same figures as a day on");
        // Who came and who left need a month closed.
        UiInfoRow came = page.Rows[4], left = page.Rows[5];
        Check(!tally.MonthClosed && came.Value.StartsWith("reallife_news_not_yet|day=") && left.Value.StartsWith("reallife_news_not_yet|day=") && came.Value.EndsWith("=" + tally.MonthClosesOnDay)
              && tally.MonthClosesOnDay > city.Day,
            "news people: before a month has closed, who came and who left say from which day (" + came.Value + "), never a nought");
        city.MonthNumber++;
        city.Region.MovedInLast = 41;
        city.Region.MovedOutLast = 0;
        NewsPeople closed = NewsPeopleRules.City(PeopleTally.Read(city), words, out _);
        Check(closed.Rows[4].Value == 41.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) && closed.Rows[5].Value == "0",
            "news people: after a month has closed they are that month's moves, a true nought included");
        city.MonthNumber--;
        city.Region.MovedInLast = 0;

        // The links of the page, through the host, the path and the layout.
        var source = new PeopleSource { State = city };
        var host = new NewsHost("economy", NewsApp.Economy, UiIcon.Chart, source);
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        host.Press(UiAppWindow.NavCommand, NewsPath.PeopleTab);
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        NewsModel m = source.Last;
        Check(m.Shape == NewsShape.People && m.Tab == NewsPath.PeopleTab && m.Nav.Length == 8, "news people: the People tab lays the people's page in the Economy window, with its tabs");
        Check(NodesOf(scene, NewsCommands.Rent) == 1 && NodesOf(scene, NewsCommands.Busy) == 1 && NodesOf(scene, NewsCommands.Back) == 0,
            "news people: the page has both links and no back button");
        Check(NodesOf(scene, UiAppWindow.NavCommand) == 8, "news people: the window's eight tabs are there to press");
        // The links stand inside the panel of figures, left of the table.
        float linkRight = 0f, tableLeft = float.MaxValue;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            scene.NodeBox(i, out float nx, out _, out float nw, out _);
            if (scene.Nodes[i].Command == NewsCommands.Rent || scene.Nodes[i].Command == NewsCommands.Busy)
            {
                linkRight = Math.Max(linkRight, nx + nw);
            }
            else if (scene.Nodes[i].Command == NewsCommands.District + UiDataTable.Row)
            {
                tableLeft = Math.Min(tableLeft, nx);
            }
        }
        Check(linkRight > 0f && linkRight < tableLeft, "news people: the links sit in the panel of figures, not in a box of their own beside the table");
        Check(NodesOf(scene, NewsCommands.District + UiDataTable.Row) == places.Length && places.Length > 0, "news people: each district that has people is a row to open (" + places.Length + ")");

        // Who pays the most rent.
        Check(host.Press(NewsCommands.Rent, 0), "news people: Who pays the most rent presses");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        m = source.Last;
        Check(m.Shape == NewsShape.Renters && m.Table.RowCount > 0 && m.BackLabel != null && NodesOf(scene, NewsCommands.Back) == 1 && NodesOf(scene, UiAppWindow.NavCommand) == 8,
            "news people: the renters list opens in the window with a back button and its rows");
        Check(host.Press(UiDataTable.First + UiDataTable.Row, 0), "news people: a renter presses");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.Shape == NewsShape.Household && NodesOf(scene, NewsCommands.Back) == 1, "news people: a renter opens its household with a back button");
        Check(host.Press(NewsCommands.Back, 0), "news people: the back button presses");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.Shape == NewsShape.Renters, "news people: Back from a household returns to the renters");
        Check(host.Back(), "news people: Escape from the renters is taken by the page");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.Shape == NewsShape.People && source.Last.Tab == NewsPath.PeopleTab && source.Last.BackLabel == null, "news people: Back from the renters returns to the people's page");

        // The busiest district.
        Check(host.Press(NewsCommands.Busy, 0), "news people: The busiest district presses");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        m = source.Last;
        int busy = tally.Busiest();
        Check(busy >= 0 && m.Shape == NewsShape.People && m.People.Title == city.Districts[busy].Id && m.BackLabel != null && NodesOf(scene, NewsCommands.Back) == 1,
            "news people: the busiest district opens that district's own page (" + m.People.Title + ") with a back button");
        Check(places.Length > 0 && places[0] == busy, "news people: the table of districts leads with the busiest, the one the link names");
        Check(host.Press(NewsCommands.Back, 0), "news people: the district's back button presses");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.Shape == NewsShape.People && source.Last.BackLabel == null, "news people: Back from a district returns to the people's page");
        // A row of the table opens its district.
        Check(host.Press(NewsCommands.District + UiDataTable.Row, 1), "news people: a district row presses");
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.People.Title == city.Districts[places[1]].Id, "news people: a district row opens that district (" + source.Last.People.Title + ")");
        host.Press(UiAppWindow.NavCommand, 2);
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.Shape == NewsShape.Notice && source.Last.Tab == 2 && source.Last.BackLabel == null, "news people: another tab leaves the district page for its own tab");
        host.Press(UiAppWindow.NavCommand, NewsPath.PeopleTab);
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(source.Last.Shape == NewsShape.People && source.Last.BackLabel == null, "news people: the People tab is the people's page, never a page left over");

        // Every page of it lays out, at the full menu's sizes, and opens quickly: the page is made and laid once when it opens or changes, never a frame.
        NewsOpenFits(host, source, scene, frame);
        host.Press(UiAppWindow.NavCommand, NewsPath.PeopleTab);
        host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        double lay = NewsOpen(scene, source.Last, frame);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        const int reads = 5;
        for (int i = 0; i < reads; i++)
        {
            NewsPeopleRules.City(PeopleTally.Read(city), words, out _);
        }
        double read = watch.Elapsed.TotalMilliseconds / reads;
        Check(lay < 8.0, "news people: the people's page lays out in " + lay.ToString("0.000") + " ms, under 8 ms");
        Check(read < 250.0, "news people: counting the city's records for the page takes " + read.ToString("0.0") + " ms, under 250 ms, once when it opens or the day changes");
        Console.WriteLine("    news people: " + tally.People + " people, " + tally.Households + " households; the page lays out in " + lay.ToString("0.000") + " ms, counted in " + read.ToString("0.0") + " ms");
    }

    // The people's page, a district's and the renters fit the window: nothing is laid below its bottom edge.
    private static void NewsOpenFits(NewsHost host, PeopleSource source, UiScene scene, UiFrame frame)
    {
        frame.Window(out _, out float wy, out _, out float wh);
        float bottom = wy + wh;
        string[] pages = { "people", "district", "renters" };
        for (int p = 0; p < pages.Length; p++)
        {
            source.Path.Clear();
            host.Press(UiAppWindow.NavCommand, NewsPath.PeopleTab);
            if (p == 1)
            {
                host.Press(NewsCommands.Busy, 0);
            }
            else if (p == 2)
            {
                host.Press(NewsCommands.Rent, 0);
            }
            host.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
            bool fits = true;
            for (int i = 0; i < scene.NodeCount; i++)
            {
                scene.NodeBox(i, out _, out float ny, out _, out float nh);
                fits &= scene.Nodes[i].Clip >= 0 || ny + nh <= bottom + 0.5f;
            }
            Check(fits, "news people: the " + pages[p] + " page's controls all stand inside the window");
        }
    }
}
