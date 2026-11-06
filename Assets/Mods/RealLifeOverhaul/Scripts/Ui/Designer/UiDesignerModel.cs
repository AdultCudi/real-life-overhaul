namespace RealLifeOverhaul.Ui
{
    /// <summary>The designer's tools, in the order the status bar's tool row shows them: the index is the row's argument.</summary>
    public enum UiDesignerTool : byte
    {
        Hand = 0, Furniture, Duplicate, Package, Wall, Floor, Palette, Eyedropper, Producer, Queue, Security, Sell, TimeOfDay, SaveBlueprint,
        Count
    }

    /// <summary>The buttons after the tools on the bar: undo, redo, one floor down, one floor up and the walls' look.</summary>
    public enum UiDesignerStep : byte
    {
        Undo = 0, Redo, FloorDown, FloorUp, Walls,
        Count
    }

    /// <summary>The panel that rises over the chosen tool's tile.</summary>
    public enum UiDesignerPanelKind : byte
    {
        None = 0, Furniture, Materials, Items, Time, Blueprint, Note, BusinessType
    }

    /// <summary>One line of what this edit costs: its words, its figure and the figure's ink.</summary>
    public struct UiDesignerCostLine
    {
        public string Label;
        public string Value;
        public UiRole Ink;
        public bool Total;     // the foot line, in the stronger type
    }

    /// <summary>One requirement of the business: a check line with its count and the two links.</summary>
    public struct UiDesignerRequirement
    {
        public string Title;
        public bool Met;
        public string Count;       // "6", "0 of 1", or null for a line with no count
        public bool CanShow;       // some item would meet it: "Show" filters the catalogue to those
        public bool Showing;       // the catalogue is filtered to it now
        public bool CanHelp;       // the game has a help page for it: "How"
    }

    /// <summary>The info panel docked at the top left: the business, what this edit costs, its customers, its score and what it needs to open.</summary>
    public sealed class UiDesignerInfo
    {
        public const int CostMost = 6, RequireMost = 12;

        public string Title;                 // "Interior designer"
        public string FloorWords;            // "Floor 1" for a building with floors; null for none
        public string Mark;                  // the business's initials on its tile
        public UiRole MarkTone = UiRole.Accent;
        public string Name;                  // the business's own name
        public string Facts;                 // "Florist, 214 5th Avenue, Garment District"
        public bool Folded;                  // only its head and a line of each reading
        public bool CanFold = true;

        public string EditTitle;             // "This edit"
        public UiDesignerCostLine[] Cost = new UiDesignerCostLine[CostMost];
        public int CostCount;

        public bool CapacityShown;
        public string CapacityTitle;         // "Customers an hour"
        public string CapacityFigure, CapacityOf;   // "56", "of 80"
        public int CapacityFill;             // 0 to 100
        public string CapacityChange;        // "+8" since the designer opened; null for none
        public UiRole CapacityChangeInk = UiRole.OkText;
        public UiRole CapacityTone = UiRole.Accent;

        public bool ScoreShown;
        public string ScoreTitle;            // "Interior score"
        public string ScoreFigure, ScoreWants;   // "74%", "district wants 70%"
        public int ScoreFill;
        public string ScoreChange;
        public UiRole ScoreChangeInk = UiRole.OkText;
        public UiRole ScoreTone = UiRole.Ok;

        public bool RequireShown;
        public string RequireTitle;          // "To open as a florist"
        public string RequireFigure;         // "4 of 6"
        public UiRole RequireInk = UiRole.WarnText;
        public string ShowLabel, HowLabel;   // "Show", "How"
        public UiDesignerRequirement[] Require = new UiDesignerRequirement[RequireMost];
        public int RequireCount;
    }

    /// <summary>A tile of the furniture catalogue.</summary>
    public struct UiDesignerCard
    {
        public int Index;            // the catalogue's own order, the argument of a press
        public string Name;
        public string Price;
        public string Kind;          // "wall", "ceiling", "holds 24" or null
        public string Picture;       // the game's picture of the item (UiPictures), resolved for the tiles in view
        public bool Needed;          // it fills a requirement the business has not met
        public bool Favourite;
    }

    /// <summary>The furniture catalogue: its categories, its search, its tiles and the chosen item's detail.</summary>
    public sealed class UiDesignerCatalogue
    {
        public const int TabsMost = 12, RowsMost = 10;

        public string[] Tabs = new string[TabsMost];          // "Decoration", each with its count in TabCounts
        public string[] TabCounts = new string[TabsMost];
        public float[] TabLeft = new float[TabsMost], TabRight = new float[TabsMost];   // where the layout stood each tab, px: the face tells which one the pointer is over
        public int TabCount;
        public int Tab;
        public int TabHover = -1;                              // the tab the pointer rests on, or -1
        public string SearchPlaceholder, Search = string.Empty;
        public string FavouritesLabel;
        public bool FavouritesOnly;
        public UiDesignerCard[] Cards = new UiDesignerCard[0];
        public int CardCount;
        public int Chosen = -1;                                // an index of Cards
        public float Scroll;
        public string NeededLabel;                             // "NEEDED"
        public string NoneTitle, NoneBody;

        public string DetailName, DetailKind;                  // "Flower stand", "Shop fittings, holds 24 bunches"
        public string[] DetailLabels = new string[RowsMost];
        public string[] DetailValues = new string[RowsMost];
        public UiRole[] DetailInks = new UiRole[RowsMost];
        public int DetailCount;
        public string ColoursLabel;
        public string[] Swatches = new string[8];              // the item's colours as picture keys (UiDraw.SwatchKey)
        public int SwatchCount;
        public string PlaceLabel, FavouriteLabel, FilterNote;  // FilterNote: "Showing what the Storage shelf needs" with a way off (Clear)
        public string ClearLabel;
        public bool PlaceOff;
        public UiGameKey PlaceKey;
        public bool Filtered;                                  // the catalogue is narrowed to a requirement
    }

    /// <summary>A tile of the wall and floor materials.</summary>
    public struct UiDesignerMaterial
    {
        public int Index;
        public string Name;
        public string Price;
        public string Picture;
        public bool HasShades;
    }

    /// <summary>The wall and floor materials: Floors and Walls with their counts, a search, the tiles and the chosen one's detail.</summary>
    public sealed class UiDesignerMaterials
    {
        public const int RowsMost = 6;

        public string[] Tabs = new string[2];
        public string[] TabCounts = new string[2];
        public int Tab;
        public string SearchPlaceholder, Search = string.Empty;
        public UiDesignerMaterial[] Cards = new UiDesignerMaterial[0];
        public int CardCount;
        public int Chosen = -1;
        public float Scroll;
        public string NoneTitle, NoneBody, PickBody;

        public string DetailName, DetailKind;
        public string[] DetailLabels = new string[RowsMost];
        public string[] DetailValues = new string[RowsMost];
        public UiRole[] DetailInks = new UiRole[RowsMost];
        public int DetailCount;
        public string ShadeLabel;
        public string[] Shades = new string[16];               // each shade of the chosen material as a picture key
        public int ShadeCount;
        public int ShadeChosen = -1;
        public string LayLabel, LayFigure;                     // "Lay the whole floor", "$1,638"
        public bool LayOff;
        public string LayWhy;                                  // why it cannot be laid now, or null
    }

    /// <summary>A thing of the item lists: a shelf, a counter or a camera, with where it stands.</summary>
    public struct UiDesignerItem
    {
        public int Index;
        public string Name;
        public string Line;
        public string Picture;
        public UiIcon Glyph;       // the state's glyph over its picture (a camera on or off), or None
        public UiRole GlyphTone;
        public bool Chosen;
    }

    /// <summary>The Producer, Queue and Security tools' panel: the building's items that tool is about.</summary>
    public sealed class UiDesignerItems
    {
        public string Title;
        public UiIcon Icon = UiIcon.Info;
        public string Note;                  // under the title: what the tool does, or the security rating
        public UiRole NoteInk = UiRole.Text2;
        public string EmptyTitle, EmptyBody;
        public UiDesignerItem[] Items = new UiDesignerItem[0];
        public int Count;
        public float Scroll;
        public int Meter = -1;               // the security rating, 0 to 100, or -1 for none
        public string MeterWords;
        public UiRole MeterTone = UiRole.Ok;
    }

    /// <summary>The time of day tool: the light in the room, never the game's clock.</summary>
    public sealed class UiDesignerTime
    {
        public string Title, Sub;            // "Time of day", "The light in the room"
        public string Time;                  // "14:30"
        public string Clock;                 // "The game clock stays at 08:45"
        public float Share;                  // 0 to 1 along the slider
        public string Figure;                // the slider's figure
        public string[] Marks = new string[0];   // the hours under the slider: "06:00" and on
        public float[] MarkShares = new float[0];   // each one's place along the slider, 0 to 1
        public string LessLabel, MoreLabel;  // "-10", "+10"
        public bool LessOff, MoreOff;
        public string Note;
    }

    /// <summary>The blueprint panel: the name to save under, what is saved and whether it can be saved.</summary>
    public sealed class UiDesignerBlueprint
    {
        public const int FactsMost = 4, ChecksMost = 3;

        public string Title, Sub;            // "Save blueprint", "Bloom & Co, 214 5th Avenue"
        public string NameLabel, Name, NamePlaceholder, NameHint;
        public bool NameBad;
        public string FactsTitle;            // "What is saved"
        public string[] FactLabels = new string[FactsMost], FactValues = new string[FactsMost];
        public int FactCount;
        public string[] CheckWords = new string[ChecksMost], CheckSide = new string[ChecksMost];
        public bool[] CheckMet = new bool[ChecksMost];
        public int CheckCount;
        public string Foot;                  // "Saved to your library, under Blueprints in the pause menu"
        public string SaveLabel;
        public bool SaveOff;
    }

    /// <summary>The game's question of what kind of business a blueprint is for, when the unit has none: its options and Continue.</summary>
    public sealed class UiDesignerBusinessType
    {
        public string Title, Body;
        public string[] Options = new string[0];
        public int Count;
        public int Chosen = -1;
        public float Offset;
        public string ContinueLabel, CancelLabel;
        public bool Open;                    // the options list is open under its field
    }

    /// <summary>A tool without a panel of its own in the game: its name and what the mouse does with it, in a card over its tile.</summary>
    public sealed class UiDesignerNote
    {
        public string Title, Body;
        public UiIcon Icon = UiIcon.Info;
    }

    /// <summary>
    /// What the interior designer shows over the game's room: the info panel at the top left, the key chips at the top right, the status bar with
    /// the tool row, the steps after it and the apply and close buttons at the right, and the panel that rises over the chosen tool. Written by
    /// DesignerFace from what the game says; every press comes back as a command and an argument. Game-free.
    /// </summary>
    public sealed class UiDesignerModel
    {
        public UiDesignerInfo Info = new UiDesignerInfo();

        public UiToolRowModel Tools = new UiToolRowModel();    // the tools' glyphs and names; Tool is the one in hand, or -1
        public UiIcon[] StepIcons = new UiIcon[(int)UiDesignerStep.Count];
        public string[] StepNames = new string[(int)UiDesignerStep.Count];
        public bool[] StepOff = new bool[(int)UiDesignerStep.Count];
        public bool[] StepShown = new bool[(int)UiDesignerStep.Count];
        public bool[] ToolOff = new bool[(int)UiDesignerTool.Count];   // a tool the game takes off this business

        public UiStatusModel Bar = new UiStatusModel();        // the time on the left; no money, no apps
        public string Balance;                                 // the money in the bank after this edit
        public string OnApply;                                 // "-$15,140 on apply", or null while nothing has changed
        public int OnApplyTrend;                               // -1 spends, +1 earns
        public string ApplyLabel, CloseLabel;
        public bool ApplyOff;

        public UiPrompt[] Prompts = new UiPrompt[0];           // the game's own control hints, as key chips
        public int PromptCount;

        public UiDesignerPanelKind Panel;
        public UiDesignerCatalogue Catalogue = new UiDesignerCatalogue();
        public UiDesignerMaterials Materials = new UiDesignerMaterials();
        public UiDesignerItems Items = new UiDesignerItems();
        public UiDesignerTime Time = new UiDesignerTime();
        public UiDesignerBlueprint Blueprint = new UiDesignerBlueprint();
        public UiDesignerBusinessType BusinessType = new UiDesignerBusinessType();
        public UiDesignerNote Note = new UiDesignerNote();
    }
}
