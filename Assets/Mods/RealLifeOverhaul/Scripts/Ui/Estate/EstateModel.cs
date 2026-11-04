using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Property app's page is: nothing yet, a table with its head, a unit's page or a page of cards about one thing.</summary>
    public enum EstateShape : byte
    {
        Off = 0,
        Roll,       // the rent roll: its head with the figures, the table, the two short lists under it
        Table,      // a tab's table, with a card of what the tab is beside it
        Unit,       // one unit: its picture, its tenant, its record, its works
        Detail,     // one thing's page: cards of facts and what to do
        Rents,      // the info view of the city's rents, over the world with no window
        Catalogue,  // the showroom's cars as cards with the chosen one's detail beside them
        Chart,      // a page of one line chart with its choices under it and its sums beside it
        Auction     // a lot of the trade auction as a card over the world: its facts, its bids and the one to place
    }

    /// <summary>A short row under the roll or on a unit's page: a glyph on its tile, a title and a line under it, a
    /// state chip and an amount. A press runs <see cref="Run"/> (an index of the model's runs).</summary>
    public struct EstateRow
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Title, Sub;
        public string Chip;
        public UiRole ChipTone;
        public string Amount;
        public UiRole AmountInk;
        public int Run;         // an index into the model's runs, or -1 for a row that only says something
        public string Face;     // a name to paint a portrait of in the glyph's place, or null
        public float Hue;
    }

    /// <summary>A row of the unit page's rent record: the month's due day, how it ended, its state and its sum.</summary>
    public struct EstateRecordRow
    {
        public string Due, What, Chip, Amount;
        public UiRole ChipTone;
    }

    /// <summary>One flat's page, after the mockup: the building's picture, its tenant and what the tenant feels, the rent record, the works and the other flats of the building.</summary>
    public sealed class EstateUnitPage
    {
        public string Title, District, Locate;       // the address, the district and the locate link's words
        public int LocateRun = -1;
        public UiIcon Icon = UiIcon.Home;
        public string Rent, RentPer;                 // "$2,600", "a month"
        public string NextLabel, NextValue;          // "Next inspection", "in 9 days"
        public int NextFill = -1;                    // 0 to 100, or -1 for no bar
        public string[] FactLabels = new string[0], FactValues = new string[0];
        public string TenantTitle, TenantName, TenantLine, TenantChip, TenantOpen;
        public UiRole TenantChipTone = UiRole.Warn;
        public float TenantHue;
        public UiRole TenantRing = UiRole.Ok;
        public int TenantOpenRun = -1;
        public string[] MeterLabels = new string[0];
        public int[] MeterValues = new int[0];
        public string YearTitle, YearKept, YearKeptLabel;
        public string[] YearLabels = new string[0], YearValues = new string[0];
        public string NeedKicker, NeedHead, NeedBody;    // "NEEDS YOU, RENT LATE", the headline, what to know
        public UiActionModel[] NeedActions = new UiActionModel[0];
        public string NeedLink;                          // "Give notice": a quiet link at the right
        public int NeedLinkRun = -1;
        public string RecordTitle, RecordNote;
        public EstateRecordRow[] Record = new EstateRecordRow[0];
        public string WorksTitle, WorksCount;
        public EstateRow[] Works = new EstateRow[0];
        public string OthersTitle, OthersCount;
        public EstateRow[] Others = new EstateRow[0];
        public string MarketNote;
        public string BuildingLink;                      // "The building" opens the building's own page
        public int BuildingRun = -1;
        public string EmptyTitle, EmptyBody;             // a unit with nobody in: what it asks and what to do
        public UiFactCardModel Empty;                    // the asking rent step, the to-let switch and the market line, when nobody is in
    }

    /// <summary>A lot of the trade auction as a card: the car's picture and facts at the left, the bidding at the right.</summary>
    public sealed class EstateAuction
    {
        public string Kicker, Title;
        public UiIcon Icon = UiIcon.Car;
        public string Picture;          // the game's picture of the car by its UiPictures key, or null for the glyph
        public float Aspect = 1.6f;
        public UiFactCardModel Left, Right;
        public string Back;
    }

    /// <summary>
    /// What the Property app shows, after the kit's app window: the sections down its sidebar, and a page of the shape given. Plain words
    /// and figures with the actions as runs (what a press does is the source's), so a state can be drawn and walked offline.
    /// </summary>
    public sealed class EstateModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Home;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public EstateShape Shape;
        public string OffTitle, OffBody;
        public UiIcon OffIcon = UiIcon.Home;   // the glyph of the page that says the feature is off: the app's own
        public UiPrompt[] Prompts = new UiPrompt[0];
        public string DayWords;                      // "Mon, Day 214" at the strip's right is the host's; kept for the head's sub line

        // The head of a page with a table: a glyph, a title, a line and up to four figures at the right.
        public UiIcon HeadIcon = UiIcon.Home;
        public string HeadTitle, HeadSub;
        public string[] FigLabels = new string[0], FigValues = new string[0];
        public UiRole[] FigInks = new UiRole[0];

        public UiDataTableModel Table;
        public string[] RowKeys = new string[0];     // by row of the table: what a press picks
        public UiFactCardModel Side;                 // beside the table: what the tab is, or its summary
        public string TableLink;                     // a link over the table's right ("View on the map")
        public int TableLinkRun = -1;

        // The roll's two lists under the table.
        public string ListATitle, ListALink, ListAEmpty;
        public int ListALinkRun = -1;
        public EstateRow[] ListA = new EstateRow[0];
        public string ListBTitle, ListBLink, ListBEmpty;
        public int ListBLinkRun = -1;
        public EstateRow[] ListB = new EstateRow[0];

        public EstateUnitPage Unit;
        public UiInfoViewModel Rents;
        public bool TabsAcross;                      // the sections as icon tabs across the top (the showroom), not down a sidebar
        public UiCatalogueModel Catalogue;
        public int[] CatalogueRuns = new int[0];    // by the chosen card's action: the run it presses
        public UiChartModel Chart;
        public UiActionModel[] ChartKinds = new UiActionModel[0], ChartPlaces = new UiActionModel[0];   // the choices under a chart: what it plots, and where
        public int ChartKind, ChartPlace;
        public UiActionModel[] FilterChips = new UiActionModel[0], SortChips = new UiActionModel[0];   // the choices over a catalogue: what shows, and in what order
        public int FilterAt, SortAt;
        public string DetailFace;   // a person the page is about: their portrait stands where the tile would
        public UiChartModel Lower;   // the chart that fills what a short page leaves under its table or its cards (real figures of the page), or null
        public string SearchHint, SearchCount, Query;   // a search over a catalogue: its placeholder, how many it holds, and the words typed (null SearchHint: no field)
        public EstateAuction Auction;

        // A detail page: its head, and two columns of cards.
        public string DetailTitle, DetailSub, DetailChip;
        public UiIcon DetailIcon = UiIcon.Home;
        public UiRole DetailChipTone = UiRole.Ok;
        public string DetailLocate;
        public int DetailLocateRun = -1;
        public UiActionModel[] DetailActions = new UiActionModel[0];
        public UiFactCardModel[] Left = new UiFactCardModel[0];
        public UiFactCardModel[] Right = new UiFactCardModel[0];

        public string Back;                          // the way back's words on a sub page; null on a tab's own page
        public Func<bool>[] Runs = new Func<bool>[0];

        // Written back by the layout, so a wheel and a focus reach the table they belong to.
        public short TableClip = -1, TableList = -1;
    }

    /// <summary>Where the screen stands: which section, and what it has open (a unit, a building, an applicant), the tab's own filters and what the player has chosen on it.</summary>
    public sealed class EstateView
    {
        public int Tab;
        public string Pick;             // a row's id, its kind before the colon: "unt:" a unit, "own:" a building, "app:" an applicant, "call:", "mkt:", "htl:", "ins:", "hot:", "car:", "add:", "lot:"
        public string From;             // the page a unit was opened from, so Back returns there
        public int ListShare = 2;       // the asking price showing on a building's page, a place in the shares
        public int ToUse = -1;          // the use showing for a change of use
        public int Unit;                // the unit showing on a building's page
        public int Office;              // the office showing for the agency
        public int Lot;                 // the building showing for the car hire lot
        public float Offset;            // px the table is scrolled
        public int Reveal = -1;
        public int Selected = -1;
        public string Query;
        public int Bid;                 // the amount the player means to bid on the lot shown, 0 until chosen
        public int BidFor;              // ...and the lot it is for
        public int Choice = -1;         // a step a page of the picked thing stands at (an asking price, an offer, a counter); -1 for its own default
        public string ChoiceFor;        // ...and what it is for
        public int Report;              // the figure the reports plot, a ReportKind
        public string ReportPlace;      // the showroom the reports are of; null for all of them
        public int Filter;              // the stock shown: all, new, used, in the workshop
        public int Sort;                // ...and its order: by bay, by price, by days on the lot
    }
}
