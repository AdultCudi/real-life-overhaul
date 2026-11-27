using System;
using System.Collections.Generic;

// A player, Logistics Managers tab of the game's own BizMan: "I cannot find where to set a second run". The departures (first run, runs a day, hours between
// runs) and Send now of delivery trips were on the RLO interface's route page only, so the game's own page, the old interface, had no way to set them.
// Now TripsPage puts a Departures button on the game's page that lists them; the checks read the sources, since the page is the game's own.
internal static partial class Program
{
    private static void LogisticsPage()
    {
        string page = ModSource(@"Economy\Logistics\TripsPage.cs"), director = ModSource(@"Economy\Logistics\LogisticsDirector.cs"), kit = ModSource(@"Ui\BizMan\Ops\BizManScreen.Trips.cs");
        string contract = ModSource(@"Core\GameContract.Logistics.cs"), managers = ModSource(@"Ui\BizMan\Ops\BizManScreen.Managers.cs");
        Check(OldPageBuildsDepartures(page, director, contract),
            "trips page: the game's own Logistics Managers page gets a Departures button that lists the first run, the runs a day, the hours between runs and Send now, set through the director's own calls");
        Check(!OldPageBuildsDepartures(page.Replace("\"reallife_trips_first\"", "\"x\""), director, contract), "trips page: a page that lacks the first run's row is found");
        Check(!OldPageBuildsDepartures(page.Replace("trips.SetTimes(plan, first, runs, gap)", "trips.Nothing()"), director, contract), "trips page: a page that does not set the times through the director is found");
        Check(!OldPageBuildsDepartures(page, director.Replace("TripsPage.Keep(trips)", "TripsPage.Nothing()"), contract), "trips page: a director that never puts the button on the game's page is found");
        Check(!OldPageBuildsDepartures(page.Replace("LogisticsDirector.Instance.SendNowOnce(id)", "0"), director, contract), "trips page: a Send now that does not run the director's send is found");
        Check(!OldPageBuildsDepartures(page, director, contract.Replace("showWarehouseButton", "x")), "trips page: a contract without the game's Show warehouse button is found");
        // Both interfaces send and test the same way.
        Check(kit.Contains("SendNowOnce(id)") && managers.Contains("Economy.LogisticsGame.CanSendNow(route)") && page.Contains("LogisticsGame.CanSendNow(plan)"),
            "trips page: both interfaces run Send now through the director's SendNowOnce and test it with LogisticsGame.CanSendNow");
        // Shown only while the routes run as trips, as the kit shows its rows.
        Check(page.Contains("bool show = wanted && Chosen() != null") && director.Contains("TripsPage.Keep(false)"), "trips page: the button stands only while the routes run as delivery trips");
        string docs = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\delivery-trips.md")));
        Dictionary<string, string> en = LocaleFile("en");
        string guide = en.TryGetValue("reallife_guide_trips", out string g) ? g : string.Empty;
        Check(docs.Contains("TripsPage") && docs.Contains("game's own interface") && guide.Contains("<b>Departures</b>") && guide.Contains("RLO interface") && guide.Contains("game's own interface"),
            "trips page: the Guide and the feature's page say where the departures are in both interfaces");
        Check(WhatsNewHas(en, "the departures and Send now of delivery trips are on the game's own Logistics Managers page too")
              && WhatsNewHas(en, "every stop of a route in BizMan has its Remove button again in the RLO interface")
              && WhatsNewHas(en, "the mouse wheel moved the list of goods at a supplier the wrong way in the RLO interface"),
            "trips page: What's new says the three fixes in English");
    }

    private static bool OldPageBuildsDepartures(string page, string director, string contract) =>
        page.Contains("\"reallife_trips_first\"") && page.Contains("\"reallife_trips_runs\"") && page.Contains("\"reallife_trips_gap\"") && page.Contains("RowSend")
        && page.Contains("trips.SetTimes(plan, first, runs, gap)") && page.Contains("trips.Times(plan)") && page.Contains("LogisticsDirector.Instance.SendNowOnce(id)")
        && page.Contains("GameConfirm.Show(\"reallife_ui_bizops_send_title\"") && page.Contains("UnityEngine.Object.Instantiate(show.gameObject")
        && director.Contains("TripsPage.Keep(trips)") && contract.Contains("showWarehouseButton") && contract.Contains("addDestinationButton") && contract.Contains("_currentPlan");
}
