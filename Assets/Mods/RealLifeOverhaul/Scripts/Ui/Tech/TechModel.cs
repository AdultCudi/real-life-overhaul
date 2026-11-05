using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a page of the Tech app is: nothing yet, a tab's table, a firm's overview, one thing's page of cards, or a game's page.</summary>
    public enum TechShape : byte
    {
        Off = 0,
        Table,      // a tab's or a firm's list: figures over a big table, a side card and a chart beside or under it
        Overview,   // a firm's overview as the approved studio window: the next step, a hero with the takings, tables, the team
        Sheet,      // one thing's page: its head, figures, two columns of cards, a table under them
        Game        // one game's page: its hero, its milestones, its reviews and its sales
    }

    /// <summary>A review of a game as a row: the outlet, the day, its score and the line it wrote.</summary>
    public struct TechQuote
    {
        public string Outlet, Day, Line;
        public int Score;
    }

    /// <summary>A capacity meter of a hall: what it measures, how full it is (0 to 100), its figures as written, and its tone by how near it is to full.</summary>
    public struct TechMeter
    {
        public string Label, Figure;
        public int Fill;
        public UiRole Tone;
    }

    /// <summary>One thing the player may press on the page: what it does is the source's, by its index in <see cref="TechModel.Runs"/>.</summary>
    public delegate bool TechRun();

    /// <summary>
    /// What the Tech app shows: its sections down the sidebar (the tabs, or a firm's own sections behind an All firms entry) and a page of the shape
    /// given. Plain words and figures with the actions as runs, so a state can be drawn and walked offline. Game-free.
    /// </summary>
    public sealed class TechModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Grid;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int NavSelected;
        public TechShape Shape;
        public string OffTitle, OffBody, OffAction;
        public UiPrompt[] Prompts = new UiPrompt[0];

        public string Next;                                       // what to do next, one sentence in the lead's place
        public UiActionModel[] Actions = new UiActionModel[0];    // beside it, the main one last
        public string Back;                                       // a sub page's way back ("Firms"); null on a tab's own page
        public UiFigureModel[] Figures = new UiFigureModel[0];

        // The table shape.
        public string TableTitle;
        public UiDataTableModel Table;
        public string[] RowKeys = new string[0];                  // by row: what a press opens
        public UiFactCardModel Side;                              // beside the table
        public UiChartModel Chart;                                // under it, where the page has room

        // The overview shape: the approved window's own model, whose rows and people are the page's.
        public UiAppModel App;
        public string[] RowKeysMore = new string[0];              // by row of the second table
        public string[] PersonKeys = new string[0];               // by person
        public TechMeter[] Meters = new TechMeter[0];             // capacity meters across the page under its hero
        public string MetersTitle;
        public string[] NavKeys = new string[0];                  // by section: where it goes (TechView.Open's keys)
        public int NoteRun = -1;                                  // the note's link under the team

        // The sheet shape.
        public UiHeadModel Head;
        public UiActionModel[] HeadActions = new UiActionModel[0];
        public UiFactCardModel[] Left = new UiFactCardModel[0];
        public UiFactCardModel[] Right = new UiFactCardModel[0];

        // The game shape.
        public UiHeroModel Hero;
        public UiTrackModel Track;
        public string QuotesTitle, QuotesCount, QuotesAverage, QuotesAverageLabel;
        public TechQuote[] Quotes = new TechQuote[0];
        public string QuotesEmpty;
        public string ChartNotes;                                 // the events on the sales line, one line under the chart ("Day 361 launch, Day 369 a review")

        public TechRun[] Runs = new TechRun[0];

        // Written back by the layout, so a wheel and a focus reach the table they belong to.
        public short TableClip = -1, TableList = -1;
    }

    /// <summary>Where the app stands: which section, which firm is open, what page of it, and what the player has chosen on it.</summary>
    public sealed class TechView
    {
        public int Tab;              // 0 to 5 as the sidebar lists them: firms, products, contracts, infra, startups, funding
        public string Firm;          // the open firm's place, or null for the tabs
        public int Section;          // the open firm's section, TechSection
        public string Pick;          // a sub page: its kind before the colon (prod:, work:, dc:, cloud, fit:, start:, cap:, newprod:, rival:, rivals:) and its id
        public string From;          // where Back returns to, the pick it was opened from
        public int Page;             // a funding page's sub page (the cap table or the raise)
        public float Offset;         // px the table is scrolled
        public int Reveal = -1;
        public int Sort = -1;        // a table's sorted column and its way
        public bool SortDown;
        public bool Opened;          // the app has been opened: the first read takes a notice's want

        /// <summary>The sidebar's place of a director tab number (firms, products, contracts, startups, funding, infra).</summary>
        public static int KitTab(int directorTab)
        {
            switch (directorTab)
            {
                case 1: return 1;
                case 2: return 2;
                case 3: return 4;
                case 4: return 5;
                case 5: return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// Where a notice's director tab and row land: a firm's own page for a firm or a rival, a firm's products for a new product, else the tab and its row's page. Every notice and
        /// link of the game that opens the Tech app goes through here, so none opens the old window.
        /// </summary>
        public void Land(int directorTab, string pick)
        {
            Offset = 0f;
            Page = 0;
            From = null;
            Pick = null;
            Firm = null;
            Section = TechSection.Overview;
            Tab = KitTab(directorTab);
            if (string.IsNullOrEmpty(pick))
            {
                return;
            }
            if (Rest(pick, "firm:", out string place))
            {
                Firm = place;
                Tab = 0;
            }
            else if (Rest(pick, "rivals:", out place))
            {
                Firm = place;
                Section = TechSection.Rivals;
                Tab = 0;
            }
            else if (Rest(pick, "rival:", out string both))
            {
                int bar = both.IndexOf('|');
                Firm = bar > 0 ? both.Substring(0, bar) : null;
                Section = TechSection.Rivals;
                Pick = pick;
                Tab = 0;
            }
            else if (Rest(pick, "newprod:", out place))
            {
                Firm = place;
                Section = TechSection.Products;
                Pick = pick;
                Tab = 0;
            }
            else
            {
                Pick = pick;
            }
        }

        private static bool Rest(string pick, string kind, out string id)
        {
            id = null;
            if (pick == null || !pick.StartsWith(kind, StringComparison.Ordinal))
            {
                return false;
            }
            id = pick.Substring(kind.Length);
            return true;
        }

        /// <summary>Goes where a key says: a tab ("tab:2"), a firm ("firm:place"), one of the open firm's sections ("sec:3"), or a page of a thing (its kind before the colon).</summary>
        public bool Open(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            Offset = 0f;
            Page = 0;
            if (key.StartsWith("tab:", StringComparison.Ordinal) && int.TryParse(key.Substring(4), out int tab))
            {
                Tab = tab;
                Firm = null;
                Pick = null;
                From = null;
                return true;
            }
            if (key.StartsWith("sec:", StringComparison.Ordinal) && int.TryParse(key.Substring(4), out int section))
            {
                Section = section;
                Pick = null;
                From = null;
                return true;
            }
            if (key.StartsWith("firm:", StringComparison.Ordinal))
            {
                Tab = 0;
                Firm = key.Substring(5);
                Section = TechSection.Overview;
                Pick = null;
                From = null;
                return true;
            }
            From = Pick;
            Pick = key;
            return true;
        }
    }

    /// <summary>A firm's sections down the sidebar while it is open.</summary>
    public static class TechSection
    {
        public const int Overview = 0, Products = 1, Team = 2, Market = 3, Rivals = 4, Money = 5, Awards = 6, Count = 7;
    }
}
