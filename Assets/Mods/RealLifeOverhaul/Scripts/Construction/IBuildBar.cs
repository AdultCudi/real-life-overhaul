using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What Build mode's logic says to whoever draws it. Two faces answer: the old panels (BuildModeBar and its parts, copies of the
    /// game's interior designer) and the kit's (Ui/BuildMode/BuildLive). The state machine, the facts and every action stay in
    /// BuildMode; a face only draws what it is told and reports what was pressed, by the same row ids the old panels used.
    /// </summary>
    internal interface IBuildSheet
    {
        void Begin(string title, string count);
        void Line(string text, Color colour);
        void Fact(string name, string value, Color colour);
        void Swatch(int id, Color colour, string name, string status, Color statusColour, bool chosen, bool usable);
        void Buttons(int mainId, string main, bool mainUsable, int sideId, string side, bool sideUsable);
        void Choices(int firstId, string[] names, int chosen, bool usable);
        /// <summary>A plot or lot of "You could build here": its name, what fits it, pressed to go there.</summary>
        void Site(int id, string name, string fits, bool mine);
        void End();
    }

    /// <summary>The view's legend: its entries, then the title and the words for the entries past those shown.</summary>
    internal interface IBuildLegend
    {
        void Begin();
        void Add(Color fill, Color edge, bool edgeOnly, string text);
        void End(string title, string more);
    }

    /// <summary>The tour at the first opening: four tips, remembered once seen.</summary>
    internal interface IBuildTour
    {
        bool Running { get; }
        TourStep Step { get; }
        void Start();
        void Next();
        void Skip();
        void Hide();
        string Check();
    }

    /// <summary>One design as "Choose a building" shows it. The extra fields are the kit's; the old window draws the first eight only.</summary>
    internal struct CatalogueCard
    {
        public int Index;
        public Sprite Picture;
        public string Name;
        public string Kind;        // its use and where it is built, or why it cannot go there
        public string Size;        // storeys and floor area
        public string Cost;
        public bool Usable;
        public bool Chosen;
        public int Use;            // BuildUse
        public bool Wanted;        // the district wants its use most
        public bool Favourite;
        public int Floors;
        public int Width, Depth;   // m
        public int Weeks;          // build time
        public long RentMonth;     // what its units rent for when let, a month; 0 when not known
        public int Demand;         // its use's demand in the district, 0 to 100, -1 for none known
        public string Planning;    // the council's word on it ("permit granted"), null for none
        public bool PlanningOk;
        public string WhyNot;      // why it cannot be built here, null when it can
    }

    /// <summary>"Choose a building": the designs that fit, as the player picks one for the squares picked.</summary>
    internal interface IBuildCatalogue
    {
        bool Alive { get; }
        bool Shown { get; }
        void Show(bool shown);
        void Show(bool shown, float toolbarTop);
        /// <summary>How many designs fit for each use (BuildUse), and the use shown: the kit's tabs; the old window sorts by filter and ignores it.</summary>
        void Uses(int[] counts, int use);
        void Begin(string context, string[] filterNames, int chosen);
        void Add(ref CatalogueCard card);
        void End(string summary, bool placeUsable, bool top);
        void Destroy();
        string Heading { get; }
        int CardsShown { get; }
        float Scale { get; }
        string Overlap(float toolbarTop);
    }

    /// <summary>What the kit's face asks of Build mode: each press comes back as the same row ids the old panels used.</summary>
    internal sealed class BuildFaceCalls
    {
        public System.Action<int> Tool, View, Row, Card, Use, Filter, Favourite;
        public System.Action Leave, Back, Place;
    }

    /// <summary>The kit's face is all of it at once.</summary>
    internal interface IBuildFace : IBuildBar, IBuildCatalogue, IBuildTour
    {
        /// <summary>Fixed text the face keeps for the tests: what its panels hold now.</summary>
        string Describe();
    }

    /// <summary>The tool row, the views, the demand, the hint line, the tool panel and the legend: everything Build mode shows besides the designs.</summary>
    internal interface IBuildBar
    {
        bool Alive { get; }
        bool Shown { get; }
        IBuildSheet Sheet { get; }
        IBuildLegend Legend { get; }
        void Show(bool shown);
        void Destroy();
        /// <summary>The game's HUD as it stands now, read again before the parts are written (the old hint bar keeps clear of it).</summary>
        void ReadHud();
        void Tools(BuildTool inUse, string[] names, string[] lines, string[] why);
        void Views(BuildView shown, string[] names, string[] lines);
        void Demand(int use, int value, Color colour, string title, string line);
        void Hint(int step, string next, string more);
        void Place();
        string Overlap();
        bool CoversCentre(out string what);
        bool ToolGreyed(BuildTool tool, out string why);
        float ToolbarTop();
        string HintWords { get; }
        string HintMoreWords { get; }
    }
}
