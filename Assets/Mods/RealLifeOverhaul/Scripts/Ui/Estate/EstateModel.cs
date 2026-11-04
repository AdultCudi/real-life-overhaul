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
        Auction,    // a lot of the trade auction as a card over the world: its facts, its bids and the one to place
        Empty,      // a section with nothing in it yet: what it is, and the card that starts it, together in the middle of the page
        Decide      // a decision, as the desk session's card: one question, the facts that decide it and its choices, over the dimmed world
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
        public string Button;   // a button of its own at the row's right (Send a reminder, Add to the fleet), or null; it takes the place of the amount
        public int ButtonRun;   // the run it presses
        public bool ButtonOff;  // drawn disabled: its reason is the row's chip
    }

    /// <summary>
    /// A thing offered to the player that waits for an answer, as a band across a page (an instruction the agency is offered): what it is, the few figures that decide it
    /// and the two answers to give. It sits over the page's table, so the answer is made where the offer is seen.
    /// </summary>
    public sealed class EstateOffer
    {
        public UiIcon Icon = UiIcon.Key;
        public string Title, Sub, Chip;
        public UiRole ChipTone = UiRole.Warn;
        public string[] FigLabels = new string[0], FigValues = new string[0];
        public UiActionModel[] Actions = new UiActionModel[0];   // the answers, the main one last
    }

    /// <summary>One answer of a decision: its name, what it does as lines of consequences, and the run it presses. The first two that can be taken carry F and P.</summary>
    public sealed class EstateChoice
    {
        public string Name, Note;
        public UiConsequence[] Lines = new UiConsequence[0];
        public int Run = -1;
        public bool Disabled;
    }

    /// <summary>
    /// A decision as the approved desk session's card: who or what it is about at the left, the kicker and the title, one question, the few facts that decide it,
    /// the choices with what each does, and the way out at the foot. Focus starts on <see cref="Start"/>, the safe answer, never the way out.
    /// </summary>
    public sealed class EstateDecision
    {
        public string Kicker, Title, Body;
        public string Portrait;                                  // the person it is about, large, with their name under it; null for the emblem
        public string[] PortraitLines = new string[0];
        public UiIcon Emblem = UiIcon.Home;
        public UiRole EmblemTint = UiRole.Accent;
        public string EmblemLabel;
        public string[] FactLabels = new string[0], FactValues = new string[0];
        public UiRole[] FactInks = new UiRole[0];
        public UiDecisionStep[] Steps = new UiDecisionStep[0];   // an amount to set before choosing (a counter-offer)
        public EstateChoice[] Choices = new EstateChoice[0];
        public int Start = -1;                                   // the choice focus starts on
        public UiActionModel[] Buttons = new UiActionModel[0];   // the way out ("Not now", Esc), after the choices
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
        public string BuildingLink;                      // "The building" opens the building's own page
        public int BuildingRun = -1;
        public string EmptyTitle, EmptyBody;             // a unit with nobody in: what it asks and what to do
        public UiFactCardModel Empty;                    // the asking rent step, the to-let switch and the market line, when nobody is in
        public UiFigureModel[] Strip = new UiFigureModel[0];   // the flat's figures when nobody is in: asking, market, days empty, applicants
        public string ShareTitle;
        public string[] ShareLabels = new string[0];     // what a tenant would weigh in the building, as meters with their percent
        public int[] ShareValues = new int[0];
        public string ApplicantsTitle, ApplicantsCount;  // the people who asked for the empty unit, as rows with their faces
        public EstateRow[] Applicants = new EstateRow[0];
    }

    /// <summary>A lot of the trade auction as a card: the car's picture and facts at the left, the bidding at the right.</summary>
    public sealed class EstateAuction
    {
        public string Kicker, Title;
        public UiIcon Icon = UiIcon.Car;
        public string Picture;          // the game's picture of the car by its UiPictures key, or null for the glyph
        public float Aspect = 1.6f;
        public UiFactCardModel Left, Right;
        public string Face;             // a name for the person the page is about, or null
        public string[] ShareLabels = new string[0];   // the lot's condition and the like as meters with their percent
        public int[] ShareValues = new int[0];
        public string ShareTitle;
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
        public int HeadStars = -1;                   // a rating in halves of a star, drawn before the sub line (the fleet's rating), or -1 for none
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
        public string ChartKindsTitle, ChartPlacesTitle;   // the labels over the two groups of choices, in the column beside the chart
        public UiActionModel[] FilterChips = new UiActionModel[0], SortChips = new UiActionModel[0];   // the choices over a catalogue: what shows, and in what order
        public int FilterAt, SortAt;
        public string DetailFace;   // a person the page is about: their portrait stands where the tile would
        public UiChartModel Lower;   // the chart that fills what a short page leaves under its table or its cards (real figures of the page), or null
        public string SearchHint, SearchCount, Query;   // a search over a catalogue: its placeholder, how many it holds, and the words typed (null SearchHint: no field)
        public EstateAuction Auction;

        // What fills a page that is short, from the page's own figures, in place of a chart that would only repeat them: a band of figures under the head,
        // the shares as graded meters with their unit, and a list of the related things (the people, the units, the works) with the same row as the roll's.
        public UiFigureModel[] Strip = new UiFigureModel[0];
        public bool SharePlain;                      // the shares are parts of a whole, in one colour: a rating mix is no grade
        public string ShareTitle;
        public string[] ShareLabels = new string[0];
        public int[] ShareValues = new int[0];       // 0 to 100: each shows with its percent
        public string RelatedTitle, RelatedCount;
        public EstateRow[] Related = new EstateRow[0];
        public bool FootFirst;                       // the lists under a table stand over it instead: what needs the player first, where the pad reaches it from the sidebar
        public int RelatedRows = 4;                  // the most rows the related list shows (a building's flats show more)
        public string GridTitle;                     // a block of equal buttons under the right column (the visits that can be booked)
        public UiActionModel[] Grid = new UiActionModel[0];
        public int[] RowRuns = new int[0];           // by row of the table: the run its action button presses (Locate), or -1

        // A detail page: its head, and two columns of cards.
        public string DetailTitle, DetailSub, DetailChip;
        public UiIcon DetailIcon = UiIcon.Home;
        public UiRole DetailChipTone = UiRole.Ok;
        public string DetailLocate;
        public int DetailLocateRun = -1;
        public UiActionModel[] DetailActions = new UiActionModel[0];
        public UiFactCardModel[] Left = new UiFactCardModel[0];
        public UiFactCardModel[] Right = new UiFactCardModel[0];

        public bool Card;                            // a decision about one thing (a call, an applicant, an offer, a bid): its page is a card over the dimmed world, as tall as its words, not a window
        public EstateUnitPage Person;                // the person a card is about: their block (name, line, chip, their meters), as the unit page shows its tenant
        public string Related2Title, Related2Count;  // a second list of related things, under the first column's or the shares
        public EstateRow[] Related2 = new EstateRow[0];
        public string[] ShareTexts = new string[0];  // what each share's figure says in place of its percent (a rent per m2), by share; empty for the percents

        public EstateOffer Offer;                    // an offer waiting for an answer, as a band between the figures and the table (the agency's offered instruction)
        public EstateDecision Decision;              // with the Decide shape
        public string FootBack;                      // a card's way out at its foot (Esc): "Not now"; the card has no back button of its own nor a title strip
        public string Kicker;                        // a card's line over its title, as the desk session's: what kind of decision this is
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
        public int RentUse;             // the rents view's kind of rent: 0 flats, 1 shops, 2 offices
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
