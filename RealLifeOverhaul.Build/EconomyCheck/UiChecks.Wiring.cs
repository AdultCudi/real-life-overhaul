using System;
using System.IO;
using System.Text.RegularExpressions;

internal static partial class Program
{
    // The presses that find the game's own buttons: where the game has a public method, field or value for the action the
    // press uses it, never a hierarchy path; where only a path works the screen's load-time check writes one line (found or
    // MISSING) and a press that finds nothing gives the game's own view, never a silent no-op.
    private static void WiringChecks()
    {
        string ui = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string market = File.ReadAllText(System.IO.Path.Combine(ui, "Market", "MarketScreen.cs")), marketKit = File.ReadAllText(System.IO.Path.Combine(ui, "Market", "MarketKit.cs"));
        string rivals = File.ReadAllText(System.IO.Path.Combine(ui, "Rivals", "RivalsScreen.cs")), rivalsKit = File.ReadAllText(System.IO.Path.Combine(ui, "Rivals", "RivalsKit.cs"));
        string persona = File.ReadAllText(System.IO.Path.Combine(ui, "Persona", "PersonaScreen.cs")), personaKit = File.ReadAllText(System.IO.Path.Combine(ui, "Persona", "PersonaKit.cs"));
        string press = File.ReadAllText(System.IO.Path.Combine(ui, "BizMan", "BizManScreen.Press.cs"));
        string probe = File.ReadAllText(System.IO.Path.Combine(ui, "Host", "UiProbe.cs"));

        // No Find<T>("path") press remains in these three screens, and no path of the old ones is left anywhere.
        Check(!Regex.IsMatch(market + rivals + persona, @"Find<\w+>\(") && !market.Contains("ViewOnMapButton") && !market.Contains("TableTabs") && !rivals.Contains("SelectedRival/TabMenu")
              && !persona.Contains("CurrentGoalsLabel") && !persona.Contains("SteamAchievementsLabel"),
            "ui wiring: Market Insider, Rivals and Persona press the game's buttons by no hierarchy path (their old View on map, table tab, TabMenu and goal sort paths are gone)");
        Check(market.Contains("_app.ChangeTab(estate ? RealEstateTab : DemandTab)") && market.Contains("\"MarketDemands\"") && market.Contains("\"RealEstate\"")
              && market.Contains("GameContract.MarketViewOnMap.Field?.GetValue(_app.neighborhoodData)")
              && persona.Contains("_goals.ToggleCurrentGoalTierSorting()") && persona.Contains("_goals.ToggleSteamAchievementTierSorting()")
              && rivals.Contains("GetComponentInParent<TabMenu>(true)") && rivals.Contains("menu.SelectTab(at.name)"),
            "ui wiring: the table tabs go to MarketInsider.ChangeTab, View on map to the district data's own button field, the goal sorts to the game's two public toggle methods, and the Rivals tab to its TabMenu found above the table");
        // The Insight period tabs name the game's filter values, not the place in its list.
        Check(press.Contains("PeriodValues = { \"1\", \"7\" }") && press.Contains("chart.SetFilter(PeriodValues[arg])") && !press.Contains("filterOptions[arg]"),
            "ui wiring: BizMan Insight's period tabs press the game's chart filter by its value (1 yesterday, 7 last week), not by index");
        // A press that finds nothing gives the game's own view, with one line.
        Check(market.Contains("MarketKit.Classic()") && market.Contains("is MISSING") && marketKit.Contains("public static void Classic() => Host.Classic()")
              && rivals.Contains("RivalsKit.Classic()") && rivals.Contains("is MISSING") && rivalsKit.Contains("public static void Classic() => Host.Classic()"),
            "ui wiring: a View on map or a Rivals tab the game has no object for gives the game's own view, with a MISSING line in Player.log");
        // The load-time self-check writes one line per object: found or MISSING.
        Check(probe.Contains("\"MISSING \"") && probe.Contains("\"found \"") && marketKit.Contains("UiProbe.Need(\"Market Insider\"") && marketKit.Contains("UiProbe.Path(\"Market Insider\"")
              && rivalsKit.Contains("UiProbe.Need(\"Rivals\"") && personaKit.Contains("UiProbe.Need(\"Persona\""),
            "ui wiring: Market Insider, Rivals and Persona check at load what their presses need and write found or MISSING per object");
        // The dead pieces stay removed.
        string pages = File.ReadAllText(System.IO.Path.Combine(ui, "BizManPages.cs")), model = File.ReadAllText(System.IO.Path.Combine(ui, "BizManModel.cs")), staff = File.ReadAllText(System.IO.Path.Combine(ui, "Employees", "EmployeesKit.cs"));
        Check(!pages.Contains("DoSetUp") && !pages.Contains("NoteAction") && !model.Contains("NoteActions") && !staff.Contains("Classic()"),
            "ui wiring: the unused BizMan DoSetUp, the portfolio note and its action, and EmployeesKit.Classic are gone");
    }
}
