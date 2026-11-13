using System;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // What the kit has and the old interface lacked, given its equal on the game's own widgets: the city's people in the Economy app and a firm's whole page
    // in the Tech app, both through the rules the kit's pages use, and the Mods page's switch that reads a fault.

    private static void UiOldSide()
    {
        // The Economy app has the People tab the kit's window has, in the same place, so a notice's tab number means one thing on both.
        Check(OutlookTab.People == NewsPath.PeopleTab && OutlookTab.Count == 8 && OutlookTab.Keys.Length == OutlookTab.Count && OutlookTab.Clamp(OutlookTab.People) == OutlookTab.People,
            "old side people: the Economy app has eight tabs and People is the kit's own seventh (" + OutlookTab.People + ")");

        // The renters and a household, from the one rules file both sides call.
        PopulationMade(7003, out PopulationState city, out _);
        var words = new NewsPlainWords { State = city };
        var slots = new int[NewsPeopleRules.RentersMost];
        var rents = new int[NewsPeopleRules.RentersMost];
        int n = NewsPeopleRules.TopRenters(city, -1, slots, rents);
        bool ordered = n > 0 && n <= NewsPeopleRules.RentersMost;
        for (int i = 0; i < n; i++)
        {
            ordered &= city.Households.Alive(slots[i]) && city.Households.Landlord[slots[i]] != Landlords.Own && rents[i] > 0 && (i == 0 || rents[i] <= rents[i - 1]);
        }
        Check(ordered, "old side people: the renters are the highest rents first, none an owner's upkeep (" + n + " of at most " + NewsPeopleRules.RentersMost + ")");
        UiDataTableModel table = NewsPeopleRules.Renters(city, -1, words, (seed, female) => "n" + seed, out int[] rows);
        Check(table.RowCount == n && rows.Length == n && table.Columns.Length == 2 && table.Rows[0].Cells[0].StartsWith("n"), "old side people: the renters' table has a row, a name and a rent for each");
        int district = city.Households.District[slots[0]];
        UiDataTableModel inDistrict = NewsPeopleRules.Renters(city, district, words, (seed, female) => "n" + seed, out int[] inRows);
        bool same = inRows.Length > 0;
        for (int i = 0; i < inRows.Length; i++)
        {
            same &= city.Households.District[inRows[i]] == district;
        }
        Check(same && inDistrict.RowCount == inRows.Length, "old side people: a district's renters are that district's households alone");
        UiInfoModel home = NewsPeopleRules.Household(city, slots[0], words, (seed, female) => "n" + seed);
        Check(home != null && home.Sections.Length == 1 && home.Sections[0].Rows.Length == 3 && home.Sections[0].Rows[2].Value == "$" + rents[0] / 100,
            "old side people: a household has its people, its district and its rent");
        Check(NewsPeopleRules.Household(city, -1, words, (seed, female) => "n") == null && NewsPeopleRules.Household(city, city.Households.Count, words, (seed, female) => "n") == null,
            "old side people: a household that is not there gives no page");

        // The sources: the Economy app's tab asks the same rules, keeps no selection of its own, and is put away as the kit's is.
        string app = ModSource(@"Outlook\OutlookApp.People.cs"), shell = ModSource(@"Outlook\OutlookApp.cs"), body = ModSource(@"Outlook\OutlookApp.Overview.cs"),
            live = ModSource(@"Ui\News\NewsLive.cs"), pool = ModSource(@"Careers\PeoplePoolGame.cs"), director = ModSource(@"Outlook\OutlookDirector.App.cs");
        Check(Has(app, "NewsPeopleRules.City(", "NewsPeopleRules.District(", "NewsPeopleRules.Links(", "NewsPeopleRules.Renters(", "NewsPeopleRules.Household(", "PeopleTally.Read(",
                "PopulationPages.Model(", "new NewsPath()") && !Has(app, "Landlords.Own") && !Has(app, "Households.Rent["),
            "old side people: the Economy app's tab reads the people through the kit's rules and keeps no rule of its own");
        Check(Has(live, "NewsPeopleRules.Renters(", "NewsPeopleRules.Household(") && !Has(live, "Landlords.Own"), "old side people: the kit's window calls the same rules for the renters and a household");
        Check(Has(body, "case OutlookTab.People: used = People(content); break;") && Has(MemberText(shell, "public void Closed()"), "PutPeopleAway();")
              && Has(MemberText(shell, "private void PickTab("), "PutPeopleAway();"),
            "old side people: the tab is written with the others, and a tab pressed or the app closed puts its pages away");
        Check(Has(MemberText(pool, "internal static bool Open(int id)"), "OutlookDirector.Instance;", "economy.OpenPeople(") && Has(director, "internal bool OpenPeople(", "_view.WantPeople(at);"),
            "old side people: a person's page opens on the Economy app's People tab where the kit's page is not offered");

        // The firm's whole page on the game's widgets.
        string page = ModSource(@"Tech\Studio\StudioPageApp.cs"), pageScreen = ModSource(@"Ui\Pages\StudioPageScreen.cs"), firmPage = ModSource(@"Tech\TechDirector.FirmPage.cs"),
            techApp = ModSource(@"Tech\TechDirector.FirmsApp.cs"), sector = ModSource(@"Tech\Sector\TechApp.Sector.cs"), firms = ModSource(@"Tech\Firms\TechApp.Pages.cs");
        Check(Has(page, "StudioPages.Page(", "TechLive.Extras(", "_director.PageAct(", "StudioFirmPage(", "SectorFirmPage(") && Has(firmPage, "internal bool PageAct(") && Has(pageScreen, "d.PageAct(_place, act, value)"),
            "old side studio: the page on the game's widgets and the kit's page are made from the same page and press through the same director call");
        Check(Has(sector, "reallife_firmpage_open", "OpenFirmPage(place)") && Has(firms, "reallife_firmpage_open", "OpenFirmPage(place)"),
            "old side studio: a firm's page in the Tech app has the button to its whole page, for a studio and for another firm");
        Check(Has(techApp, "_firmPage.Install();", "_firmPage?.Changed();") && Has(ModSource(@"Tech\TechDirector.Firms.cs"), "_firmPage?.Remove();"),
            "old side studio: the page is put among the apps with the Tech app, written again when the firms change, and taken off with it");

        // The Mods page's switch reads a faulted screen as off, a second after the fault, and a press on it tries the screen again.
        string options = ModSource(@"Ui\Shell\UiSwitchOptions.cs"), ui = ModSource(@"Ui\UiDirector.cs");
        Check(Has(options, "public static void Look()", "UiBreaker.Tripped(def.Id)", "SetIsOnWithoutNotify(shown)", "UiScreenSwitches.Retry(", "if (UiBreaker.Count == 0 && !_held)")
              && Has(MemberText(ui, "private void Slow()"), "UiSwitchOptions.Look();"),
            "old side options: a faulted screen's switch on the Mods page reads off once a second at most, nothing runs while no screen has faulted");
    }
}
