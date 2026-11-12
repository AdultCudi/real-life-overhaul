using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// The kit's pages from the features' own views, as the owner met them in beta 3: no record's key on a page (an address
// as the game writes it), every target a press answers with the game's own action (View on map and Call reached
// nothing), the pages shaped to their jobs (a household's ledger, a person's profile, Flare's feed), and the Contacts
// buttons each routed to the game. Every page is laid out from a real view (the offline city, Flare's views, the firms'
// acts) and held to the kit's layout rules.
internal static partial class Program
{
    private static void UiPageChecks()
    {
        UiPlaceChecks();
        UiPopulationPageChecks();
        UiStudioPageChecks();
    }

    private static void UiPlaceChecks()
    {
        Check(UiPlaces.IsKey("ba:street_fifthavenue|70") && UiPlaces.IsKey("ba:street_seventhstreet|17|t") && UiPlaces.IsKey("ba:businesstype_coffeeshop")
              && UiPlaces.IsKey("reallife_pop_fig_people") && !UiPlaces.IsKey("70 Fifth Avenue") && !UiPlaces.IsKey("Midtown") && !UiPlaces.IsKey("$1,112,059"),
            "ui places: a record's key is told from words");
        string fifth = UiPlaces.Address("ba:street_fifthavenue|70", null), seventh = UiPlaces.Address("ba:street_seventhstreet|17|t", null);
        Check(fifth == "70 Fifth Avenue" && seventh == "17 Seventh Street" && UiPlaces.Address("ba:street_broadwaystreet|12|t", k => k == "ba:street_broadwaystreet" ? "Broadway" : null) == "12 Broadway",
            "ui places: an address key reads as the game writes an address, the street by the game's own words where it has them (" + fifth + ", " + seventh + ")");
        Check(UiPlaces.Words("ba:businesstype_coffeeshop") == "Coffeeshop" && !UiPlaces.IsKey(UiPlaces.Words("reallife_pop_fig_people")), "ui places: a key with no words of its own reads as plain words, never as itself");
    }

    // Flare's views as feeds: the entries that lead somewhere are targets, a tab says where its page is, and no action opens its own page.
    // The firms' pages offer only what the page can run: the acts the director carries out itself. The Tech app's own
    // screens (a new product, the contracts, the concept) open only inside that app, so a button for them would do nothing.
    private static void UiStudioPageChecks()
    {
        string tech = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Tech"));
        string sector = File.ReadAllText(System.IO.Path.Combine(tech, "TechDirector.SectorDesk.cs"));
        sector = sector.Substring(Math.Max(0, sector.IndexOf("internal bool SectorDo(", StringComparison.Ordinal)));
        string studio = Between(File.ReadAllText(System.IO.Path.Combine(tech, "TechDirector.StudioDesk.cs")), "internal void StudioDo(", "private void ToDesk(");
        bool same = sector.Length > 0 && studio.Length > 0;
        string odd = "none";
        foreach ((Type type, string body) in new[] { (typeof(SectorAct), sector), (typeof(StudioAct), studio) })
        {
            var handled = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(body, @"case " + type.Name + @"\.(\w+):"))
            {
                handled.Add(match.Groups[1].Value);
            }
            foreach (System.Reflection.FieldInfo f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (f.IsLiteral && f.FieldType == typeof(int) && StudioPages.Direct((int)f.GetValue(null)) != handled.Contains(f.Name))
                {
                    same = false;
                    odd = type.Name + "." + f.Name;
                }
            }
        }
        Check(same, "ui pages: a firm's page offers exactly the acts its director carries out, so no button of the Tech app's own screens does nothing on it (" + odd + ")");
        StudioFaceModel sample = StudioPages.Sample(UiWords("en"));
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Your firm");
        var scene = new UiScene();
        StudioFace.Lay(scene, sample, frame, true, UiDefaultKeys.Keyboard);
        string said = "none";
        int broken = UiCheckRules.Overlaps(scene, l => said = l) + UiCheckRules.Spills(scene, l => said = l) + UiCheckRules.Unfit(scene, l => said = l) + UiCheckRules.EmptyPanels(scene, l => said = l)
                     + UiCheckRules.NoPrompts(scene, l => said = l) + UiCheckRules.TypeLevels(scene, l => said = l);
        // Its rows are targets only so the keys and the pad can scroll them: the close and the rows are all there is to press, and no action stands on it.
        bool onlyRows = true;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            int c = scene.Nodes[i].Command;
            onlyRows &= c == UiDraw.CloseCommand || (System.Array.IndexOf(StudioFace.Commands, c) >= 0 && c != StudioFace.ActionCommand);
        }
        Check(onlyRows && sample.Actions.Length == 0 && broken == 0 && sample.Title == UiWords("en")("reallife_ui_sample_firm", "A sample studio"),
            "ui pages: on a save with no tech firm the firm's test opens a sample studio named as one, with nothing on it to act on (" + said + ")");
    }
}
