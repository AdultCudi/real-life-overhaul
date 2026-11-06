using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's templates: every screen is drawn by one of them.</summary>
    public enum UiTemplate : byte
    {
        ListDetail = 0,   // groups, list and detail (Contacts)
        Dashboard,        // figures over time (the Economy app's views)
        Session,          // a desk session's steps and choices
        Form,             // settings and choices before an action
        Gallery,          // the kit's own gallery
        InfoPanel,        // a person, a household, a business or a building, docked left with its icon tabs (DIRECTION_CS2, 4.1)
        InfoView,         // the city coloured by one figure, its rows, donut and map legend at the left (4.2)
        AppWindow,        // an app's window over the city: sections down a sidebar or across as tabs, a page of blocks (4.3, 4.4)
        Decision,         // a moment's decision over the dimmed world: faces or an emblem, numbered choices (4.5)
        Phone,            // the phone's home: apps on tiles, what comes next, the pages' bar at its foot (4.7)
        Hud               // the HUD over the running game: the status bar and the pieces at its edges or over a point of the world (4.6)
    }

    /// <summary>One registered screen: its id, its template, the class that runs it, and the words its labels use.</summary>
    public sealed class UiScreenDef
    {
        public readonly string Id;
        public readonly UiTemplate Template;
        public readonly string Runner;      // the IUiScreen class that runs it in the game, or null for a template's sample alone
        public readonly string[] Keys;      // the locale keys of its labels: the mod's own (reallife_) and the game's

        /// <summary>The options section its own switch stands in (UiScreenSwitches), or null: a template's sample, the gallery.</summary>
        public string Section { get; private set; }

        public UiScreenDef(string id, UiTemplate template, string runner, string[] keys)
        {
            Id = id;
            Template = template;
            Runner = runner;
            Keys = keys;
        }

        /// <summary>
        /// Gives the screen its own switch in Mod settings, in a section of the RLO interface's (UiScreenSwitches.Apps and
        /// the rest): with it off, this one screen is the game's own (or the mod's page as it was) while the rest stay on
        /// the kit. Its id, name and tip follow from the screen's id (UiScreenSwitches).
        /// </summary>
        public UiScreenDef Switch(string section)
        {
            Section = section;
            return this;
        }
    }

    /// <summary>
    /// The one list of the kit's screens (docs/standards/ui.md, RLO-UI-005): every screen the mod draws on the kit is here,
    /// in its area's file, with its template and the keys of its labels, and every class that runs a kit screen is named
    /// by one entry (StandardCheck holds the two together). EconomyCheck lays each one out at every frame the game can give
    /// it, in both looks and all eleven languages, and checks the layout; the templates' own samples stand here too, so
    /// the shape the next screens take (the Economy app's population view, the studio's pages, the desk sessions) is
    /// checked before they are built. Game-free. Cost: one array, gathered once.
    /// </summary>
    public static partial class UiScreens
    {
        public static readonly UiScreenDef[] All = Gather();

        // Each area keeps its screens in a file of its own (UiScreens.<Area>.cs), so jobs on different areas never edit one
        // list. An area with no file yet is a partial method nobody wrote, and the compiler drops its call. A new area adds
        // its line to both lists below. The order is fixed, so the registry reads the same on every machine.
        private static UiScreenDef[] Gather()
        {
            var screens = new List<UiScreenDef>(64);
            AddContacts(screens);
            AddKit(screens);
            AddPages(screens);
            AddShell(screens);
            AddPhone(screens);
            AddGrids(screens);
            AddHud(screens);
            AddBizMan(screens);
            AddEconoView(screens);
            AddEmployees(screens);
            AddPersona(screens);
            AddActivity(screens);
            AddBizOps(screens);
            AddBuy(screens);
            AddDialog(screens);
            AddHudScreens(screens);
            AddWorld(screens);
            AddFlare(screens);
            AddLife(screens);
            AddMoney(screens);
            AddNews(screens);
            AddSettings(screens);
            AddStocks(screens);
            AddCompany(screens);
            AddConstruction(screens);
            AddEstate(screens);
            AddServices(screens);
            AddTech(screens);
            AddVentures(screens);
            AddWork(screens);
            AddDesigner(screens);
            AddMap(screens);
            AddOverlays(screens);
            AddPause(screens);
            AddTooltips(screens);
            AddBlueprints(screens);
            AddOptions(screens);
            AddSweep(screens);
            return screens.ToArray();
        }

        static partial void AddContacts(List<UiScreenDef> screens);
        static partial void AddKit(List<UiScreenDef> screens);
        static partial void AddPages(List<UiScreenDef> screens);
        static partial void AddShell(List<UiScreenDef> screens);
        static partial void AddPhone(List<UiScreenDef> screens);
        static partial void AddGrids(List<UiScreenDef> screens);
        static partial void AddHud(List<UiScreenDef> screens);
        static partial void AddBizMan(List<UiScreenDef> screens);
        static partial void AddEconoView(List<UiScreenDef> screens);
        static partial void AddEmployees(List<UiScreenDef> screens);
        static partial void AddPersona(List<UiScreenDef> screens);
        static partial void AddActivity(List<UiScreenDef> screens);
        static partial void AddBizOps(List<UiScreenDef> screens);
        static partial void AddBuy(List<UiScreenDef> screens);
        static partial void AddDialog(List<UiScreenDef> screens);
        static partial void AddHudScreens(List<UiScreenDef> screens);
        static partial void AddWorld(List<UiScreenDef> screens);
        static partial void AddFlare(List<UiScreenDef> screens);
        static partial void AddLife(List<UiScreenDef> screens);
        static partial void AddMoney(List<UiScreenDef> screens);
        static partial void AddNews(List<UiScreenDef> screens);
        static partial void AddSettings(List<UiScreenDef> screens);
        static partial void AddStocks(List<UiScreenDef> screens);
        static partial void AddCompany(List<UiScreenDef> screens);
        static partial void AddConstruction(List<UiScreenDef> screens);
        static partial void AddEstate(List<UiScreenDef> screens);
        static partial void AddServices(List<UiScreenDef> screens);
        static partial void AddTech(List<UiScreenDef> screens);
        static partial void AddVentures(List<UiScreenDef> screens);
        static partial void AddWork(List<UiScreenDef> screens);
        static partial void AddDesigner(List<UiScreenDef> screens);
        static partial void AddMap(List<UiScreenDef> screens);
        static partial void AddOverlays(List<UiScreenDef> screens);
        static partial void AddPause(List<UiScreenDef> screens);
        static partial void AddTooltips(List<UiScreenDef> screens);
        static partial void AddBlueprints(List<UiScreenDef> screens);
        static partial void AddOptions(List<UiScreenDef> screens);
        static partial void AddSweep(List<UiScreenDef> screens);

        /// <summary>A registered screen by its id, or null.</summary>
        public static UiScreenDef Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }
    }
}
