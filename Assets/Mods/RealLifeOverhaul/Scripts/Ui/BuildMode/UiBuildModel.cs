namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of Build mode's tool panel is: the same kinds the old panel's sheet wrote (words, a fact, a zone row, buttons, choices) and the sites of "You could build here".</summary>
    public enum UiBuildLineKind : byte
    {
        Text = 0,     // a paragraph in a tone
        Fact,         // a name and its figure
        Zone,         // a row of a colour, a name and a state, pressed to choose it
        Buttons,      // one or two buttons across the panel
        Choices,      // a row of choices, one chosen
        Site          // a plot or lot: its name, what fits it, pressed to go there
    }

    /// <summary>One line of the tool panel.</summary>
    public struct UiBuildLine
    {
        public UiBuildLineKind Kind;
        public string Label;       // Text: the words; Fact and Site: the name; Zone: the name; Buttons: the main button
        public string Value;       // Fact: the figure; Zone: its state; Site: what fits; Buttons: the second button
        public UiRole Tone;        // the words' tone (Text2 quiet, Text plain, DangerText, WarnText, OkText); a Fact's figure
        public int Id;             // Zone, Site: the row's id; Buttons: the main button's; Choices: the first choice's
        public int Id2;            // Buttons: the second button's
        public int Rgb;            // Zone: its colour; -1 for none (Remove zone)
        public bool On;            // Zone: chosen; Site: the player's own
        public bool Usable;        // the row or the main button takes a press
        public bool Usable2;       // the second button takes a press
        public string[] Names;     // Choices
        public int Chosen;         // Choices
    }

    /// <summary>A colour of the view's legend: its fill (or an edge alone) and its words.</summary>
    public struct UiBuildKey
    {
        public int FillRgb;        // -1 for an edge alone
        public int EdgeRgb;
        public string Words;
    }

    /// <summary>One use's demand in the district.</summary>
    public struct UiBuildDemand
    {
        public string Name;        // "Homes"
        public int Value;          // 0 to 100; -1 for none known
        public string Words;       // "wants more"
        public int Rgb;            // the use's own colour on the map
        public UiRole Tone;        // the meter's tone: Warn while little is wanted, Accent between, Ok while a lot is
    }

    /// <summary>One design of "Choose a building".</summary>
    public struct UiBuildCard
    {
        public int Index;          // BuildMode's own
        public string Name;
        public string Kind;        // "City design, Homes"; or why it cannot go here
        public string Size;        // "4 storeys, 600 m2"
        public string Cost;
        public bool Usable;
        public bool Chosen;
        public bool Wanted;
        public bool Favourite;
        public string Picture;     // the game's picture of the use (UiPictures), or null
        public string Planning;
        public bool PlanningOk;
        public int Floors, Width, Depth, Weeks;
        public string Rent;        // "$31,200 a month" when the city's rents are read; null for none
        public int Demand;
        public string WhyNot;
    }

    /// <summary>
    /// "Choose a building": category tabs with their counts, a search, favourites, the designs as cards and the chosen one's detail. Tabs are the
    /// uses (Homes, Shops, Offices, Warehouses); the search and the favourites switch narrow the cards the face holds.
    /// </summary>
    public sealed class UiBuildCatalogueModel
    {
        public const int CardsMost = 40;

        public string Context;                 // for which squares
        public string[] Tabs = new string[0];  // each with its count
        public int Tab;                        // the use shown
        public string[] Filters = new string[0];   // Fits here, Corner buildings, City designs, each with its count: BuildModeCatalogue's filters
        public int Filter;                     // the filter shown
        public string PickLine;                // what to do while no card is chosen or the chosen one is not among those shown
        public string SearchPlaceholder;
        public string Search = string.Empty;
        public string FavouritesLabel;
        public bool FavouritesOnly;
        public string SummaryLine;             // what is picked, or what to do
        public UiBuildCard[] Cards = new UiBuildCard[CardsMost];
        public int CardCount;
        public string FavouriteLabel, WantedLabel, WeeksLabel, PriceLabel, SizeLabel, BuildTimeLabel, RentLabel, PlanningLabel, DemandLabel, PlaceLabel, BackLabel, NoneLabel, WeeksWords;
        public bool PlaceUsable;
        public float Scroll;                   // px the cards are scrolled
    }

    /// <summary>One tip of the tour at the first opening.</summary>
    public sealed class UiBuildTourModel
    {
        public int Step;                       // 0 to 3: the hint line, the tools, the views, the demand
        public string Count, Title, Body, Next, Skip;
    }

    /// <summary>
    /// What Build mode's kit face shows: the hint line, the tool row, the views with the legend and the demand, the tool panel, the design window and the tour. Written by
    /// BuildFace from BuildMode's own calls; every press comes back as a command and an argument. Game-free.
    /// </summary>
    public sealed class UiBuildModel
    {
        public const int KeysMost = 12, DemandMost = 4, LinesMost = 24, ViewsMost = 8;

        public string Title;                   // "Build mode"
        public string District;                // "Uptown" or the city; at the strip's right
        public int Step;                       // the hint line's step number; 0 for none
        public string Hint, HintMore;
        public UiIcon[] ViewIcons = new UiIcon[ViewsMost];
        public string[] ViewNames = new string[ViewsMost];
        public int ViewCount, View;
        public string ViewsTitle;              // "View"
        public string ViewTip;                 // the shown view's one line
        public UiBuildKey[] Keys = new UiBuildKey[KeysMost];
        public int KeyCount;
        public string LegendTitle, LegendMore;
        public UiBuildDemand[] Demand = new UiBuildDemand[DemandMost];
        public int DemandCount;
        public string DemandTitle;             // "What Uptown wants"
        public UiToolRowModel Tools = new UiToolRowModel();
        public string SheetTitle, SheetCount;
        public UiIcon SheetIcon = UiIcon.Info;
        public UiBuildLine[] Lines = new UiBuildLine[LinesMost];
        public int LineCount;
        public float SheetScroll;              // px the tool panel's lines are scrolled while they are taller than the room
        public string LeaveLabel;
        public UiBuildCatalogueModel Catalogue;   // null while the window is shut
        public UiBuildTourModel Tour;             // null while no tip shows
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }
}
