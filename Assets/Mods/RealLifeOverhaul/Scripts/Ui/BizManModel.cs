namespace RealLifeOverhaul.Ui
{
    /// <summary>Which of BizMan's two faces shows: the portfolio of the player's places, or one place's page.</summary>
    public enum BizManPage : byte
    {
        Portfolio = 0,
        Business
    }

    /// <summary>What a place's page shows under its head: each the shape the game's own tab or view needs.</summary>
    public enum BizManPart : byte
    {
        Building = 0,   // the building a running business stands in: its facts, its picture, the lease and an offer for the building
        Rent,           // a building to rent, or rented and empty: the lease's terms and what can be done with it
        Start,          // starting a business: the kinds as a catalogue, the name, Set up
        Rival,          // another's business: what it is, its hours, the offers to take it over or buy its building
        Villa,          // a house in the Hamptons to buy
        Insight,        // customers over time, promotion, satisfaction, capacity
        Pricing,        // the goods and their prices
        Deliveries,     // the wholesale contracts and their goods
        Marketing,      // the campaigns and the agencies
        Settings,       // the name, the kind, the logo, the uniforms, shutting down
        Estate,         // the building owned: its rent, the unit of its own, selling it
        Interior,       // the mod's own Interior tab: the scores beside the page chosen of what the interior does
        Classic         // a page only the game's own view shows, opened from its entry
    }

    /// <summary>A line of a page written as text (the Interior tab's): a title, a heading, a line in its tone or a quieter note.</summary>
    public enum BizManLineKind : byte
    {
        Title = 0,
        Heading,
        Line,
        Note
    }

    /// <summary>A line of such a page: its kind, its words and its tone.</summary>
    public struct BizManLine
    {
        public BizManLineKind Kind;
        public string Words;
        public UiRole Tone;     // a line's colour: None for plain, or the text tone the game's own box gives it
    }

    /// <summary>A fact of a page: its label and value, the value a link when it sets the destination or opens something.</summary>
    public struct BizManFact
    {
        public string Label;
        public string Value;
        public bool Link;
        public int Arg;          // what the link opens, the screen's own number
        public UiRole Tone;      // the value's tone where the game colours it (an occupancy, a gain); None for plain
    }

    /// <summary>A graded figure of a page, 0 to 100, as the game's own bars hold it.</summary>
    public struct BizManGrade
    {
        public string Label;
        public int Value;
    }

    /// <summary>An amount to offer or set, typed or stepped: its label, its figure, the field it types into and the button that sends it.</summary>
    public sealed class BizManAmount
    {
        public string Title;     // "Make an offer for the building"
        public string Line;      // what it is measured against ("Estimated valuation: $1.2M")
        public string Figure;    // the amount as typed
        public string Placeholder;
        public int Field;        // which field it types into (BizManPages.Field*)
        public bool CanLess, CanMore;
        public bool Disabled;    // the game holds it now
        public UiActionModel Send;   // the button that sends or applies it
    }

    /// <summary>A row of a page's list with what it does: a contract, a campaign's agency, an agency to call.</summary>
    public struct BizManItem
    {
        public string Picture;   // a logo (UiPictures key) or null for the glyph
        public UiIcon Glyph;
        public string Name;
        public string Line;      // under the name
        public string Value;     // at the right ("$120 a day")
        public string WarnWords; // what the game marks on it, as a chip ("Nothing ordered"), or null
        public bool Chosen;
        public string Link;      // a link at its right ("Call"), or null
    }

    /// <summary>One of a campaign's kinds of marketing, switched on or off: its words and its switch.</summary>
    public struct BizManToggle
    {
        public string Words;
        public bool On;
        public bool Disabled;
        public int Group;        // the agency it belongs to
    }

    /// <summary>A shelf or machine that caps how many customers come in an hour, against the building's limit.</summary>
    public struct BizManCapacity
    {
        public string Name;
        public string Figure;    // "18 / 40"
        public int Share;        // 0 to 100 of the building's limit
        public UiRole Tone;      // full, a third or less, between
    }

    /// <summary>
    /// What BizMan shows, as plain data: the portfolio's groups and table, or a place's head, its sections and the part its
    /// chosen section shows; no colour but a role, no position. The game's side fills it from the game's own widgets after
    /// the game (and other mods) ran; the samples fill it offline. Game-free.
    /// </summary>
    public sealed class BizManModel
    {
        public string Title;
        public BizManPage Page;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int NavSelected;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

        // The portfolio
        public UiIcon[] SummaryIcons = new UiIcon[0];   // the slim line over the table: each its glyph and its words
        public string[] Summary = new string[0];
        public UiDataTableModel Table = new UiDataTableModel();
        public string[] FilterOptions;                  // an open filter's choices, or null while none is open
        public int FilterOpen = -1;
        public int FilterChosen;
        public float FilterOffset;
        public string Note;                             // a quiet line under the table (another mod's part of the list, how it opens)
        public UiActionModel[] NoteActions = new UiActionModel[0];

        // A place's page
        public UiFirmHeadModel Head;
        public BizManPart Part;
        public string PartTitle;                        // the shown section's name, over its page where the page needs one
        public string Picture;                          // the building's photo, the game's own (UiPictures key), or null
        public float PictureAspect = 1.6f;
        public BizManFact[] Facts = new BizManFact[0];  // the building's facts, or the part's own
        public string FactsTitle;
        public BizManFact[] More = new BizManFact[0];   // a second block of facts (the lease's terms, the hours)
        public BizManFact[] BuildingFacts = new BizManFact[0];   // the building's own facts on a page whose first block is another's (real estate)
        public string MoreTitle;
        public string Lead;                             // a sentence over the part (who owns the building, a note on its state)
        public UiActionModel[] Actions = new UiActionModel[0];   // the part's actions, the main one last
        public BizManAmount Amount;                     // an amount to offer or set
        public BizManAmount Second;                     // a second (the building's offer beside the business's)

        // Start a business
        public UiCatalogueModel Catalogue;
        public string NameText;
        public string NamePlaceholder;
        public string NameLabel;
        public bool Typing;

        // Insight
        public UiChartModel Chart;
        public string[] Periods = new string[0];
        public int Period;
        public string GradesTitle;
        public BizManGrade[] Grades = new BizManGrade[0];
        public string SecondTitle;
        public BizManGrade[] SecondGrades = new BizManGrade[0];
        public string SecondEmpty;                      // why the second block has nothing yet
        public string CapacityTitle;
        public string CapacityLine;                     // "Up to 18 customers an hour of the building's 40"
        public BizManCapacity[] Capacity = new BizManCapacity[0];

        // Pricing, Deliveries
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public UiDataTableModel Goods;
        public BizManLine[] Lines = new BizManLine[0];  // the Interior tab's left column (its scores)
        public BizManLine[] SecondLines = new BizManLine[0];   // and its right (the page chosen)
        public string[] Pages = new string[0];          // the right column's pages, as tabs over it
        public int PageShown;
        public bool PagesOpen;                          // the pages' list open, where a language's names are too long for tabs
        public float LinesOffset, SecondOffset;         // px each column is scrolled; Lay writes back what it took
        public int LinesReveal = -1, SecondReveal = -1; // a heading stepped to, brought into view at the next layout
        public short LinesClip = -1, SecondClip = -1;   // each column's scroll region and headings' list, as Lay made them
        public short LinesList = -1, SecondList = -1;
        public string GoodsTitle;                       // the goods' table's title, over it as the left column's title is over its own
        public float ShareRest;                         // the share the legend does not name (the goods past the best sellers), the ring's grey
        public string[] ShareLabels = new string[0];    // the best sellers as a donut
        public string[] ShareValues = new string[0];
        public float[] Shares = new float[0];
        public string SharesTitle;

        // Deliveries, Marketing
        public string ItemsTitle;
        public BizManItem[] Items = new BizManItem[0];
        public string ItemsEmpty;
        public string ItemsEmptyBody;
        public BizManToggle[] Toggles = new BizManToggle[0];
        public bool HasRepeat;                          // a contract's repeating order
        public bool Repeat;
        public string RepeatWords;
        public string OthersTitle;
        public BizManItem[] Others = new BizManItem[0];

        // Settings
        public string[] Kinds = new string[0];
        public int KindChosen = -1;
        public bool KindOpen;
        public string KindLabel;
        public string[] Fonts = new string[0];
        public int FontChosen = -1;
        public bool FontOpen;
        public string FontLabel;
        public string LogoTitle;
        public string LogoLabel;
        public string Uniforms;                         // the link that opens the uniforms' editor, or null

        // Rent
        public string SliderLabel;
        public string SliderFigure;
        public float SliderShare;
        public bool SliderHeld;

        // Classic
        public UiIcon ClassicIcon;
        public string ClassicTitle;
        public string ClassicBody;
    }
}
