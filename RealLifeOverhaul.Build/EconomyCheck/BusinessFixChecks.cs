using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Interiors;

// Faults found in Scripts/Business and Interiors: a smear tested for room with the undercut's dip (-10) but applied its own
// (-6), so a business with room for one but not the other never had one; the stations of an office read the game's item
// list with no test that it was there, though the interior's own read allows for none; a refresh of the finishes that
// could not be done said nothing; and the wear test aged the finishes at full strength, whatever the wear option and the
// cleaning service.
internal static partial class Program
{
    private static void BusinessInteriorFixes()
    {
        BusinessFootfallRoom();
        InteriorStations();
        InteriorRefreshTold();
        InteriorWearTest();
    }

    private static void BusinessFootfallRoom()
    {
        Check(BusinessCatalogue.IssueFootfall(BusinessCatalogue.Smear) == BusinessCatalogue.SmearDip
              && BusinessCatalogue.IssueFootfall(BusinessCatalogue.Undercut) == BusinessCatalogue.UndercutDip
              && BusinessCatalogue.IssueFootfall(BusinessCatalogue.Complaint) == BusinessCatalogue.ComplaintDip
              && BusinessCatalogue.IssueFootfall(BusinessCatalogue.Praise) == BusinessCatalogue.PraiseBoost
              && BusinessCatalogue.IssueFootfall(BusinessCatalogue.Feature) == BusinessCatalogue.FeatureBoost
              && BusinessCatalogue.IssueFootfall(BusinessCatalogue.Racket) == 0 && BusinessCatalogue.IssueFootfall(null) == 0,
            "business footfall: each kind's own figure, the smear's -6 and not the undercut's -10; none for a kind that moves no customers");
        string issues = ModSource(@"Business\BusinessIssues.cs"), start = ModSource(@"Business\BusinessIssues.Start.cs");
        string why = MemberText(issues, "public static string Why(IssueDef def, BuildingRegistration reg, bool test)");
        int rooms = Regex.Matches(why, @"FootfallRoom\(reg, BusinessCatalogue\.IssueFootfall\(def\.Id\)\)").Count;
        int asked = Regex.Matches(why, @"FootfallRoom\(").Count;
        int started = Regex.Matches(start, @"issue\.A = BusinessCatalogue\.IssueFootfall\(issue\.Kind\);").Count;
        Check(rooms == 4 && asked == rooms && started == 4 && !Regex.IsMatch(start, @"issue\.A = BusinessCatalogue\.\w+(Dip|Boost);")
              && issues.Contains("int boost = BusinessCatalogue.IssueFootfall(issue.Kind);"),
            "business footfall: the test for room and the start use the same figure for every kind that moves customers (" + rooms + " tests, " + started + " starts)");
    }

    private static void InteriorStations()
    {
        string basket = ModSource(@"Interiors\InteriorGame.Basket.cs");
        string stations = MemberText(basket, "private static int Stations(BuildingRegistration reg, BusinessType type, int hour)");
        Check(stations.Contains("Dictionary<string, ItemInstance> placed = reg.itemInstances;") && stations.Contains("|| placed == null)")
              && stations.Contains("string.IsNullOrEmpty(shift.itemInstanceId) || !placed.TryGetValue(shift.itemInstanceId, out station) || station == null")
              && !stations.Contains("reg.itemInstances.TryGetValue"),
            "interiors fix: an office's stations allow for no item list and for a shift with no station, as the interior's own read does");
        var bare = new List<string>();
        foreach (string file in new[] { "InteriorGame.cs", "InteriorGame.Basket.cs", "InteriorGame.Requirements.cs" })
        {
            string text = ModSource(@"Interiors\" + file);
            foreach (Match m in Regex.Matches(text, @"reg\.itemInstances\.\w+"))
            {
                bare.Add(file + ": " + m.Value);
            }
        }
        string requirements = ModSource(@"Interiors\InteriorGame.Requirements.cs");
        Check(bare.Count == 1 && requirements.Contains("if (reg.itemInstances != null)") && bare[0] == "InteriorGame.Requirements.cs: reg.itemInstances.Values",
            "interiors fix: the item list is read in one place without a local, behind its own test (" + string.Join(", ", bare) + ")");
    }

    private static void InteriorRefreshTold()
    {
        Check(InteriorRules.RefreshRefused(0, true, true) == "reallife_interior_wear_off" && InteriorRules.RefreshRefused(0, false, false) == "reallife_interior_wear_off"
              && InteriorRules.RefreshRefused(1000, false, false) == "reallife_interior_refresh_unknown" && InteriorRules.RefreshRefused(1000, true, false) == "reallife_interior_refresh_unknown"
              && InteriorRules.RefreshRefused(1000, true, true) == null && InteriorRules.RefreshRefused(-5, true, true) == "reallife_interior_wear_off",
            "interiors refresh: wear off is said first, then a business with no record; none when it can be done");
        string director = ModSource(@"Interiors\InteriorDirector.cs");
        string refresh = MemberText(director, "public bool Refresh(BuildingRegistration reg)");
        int falses = Regex.Matches(refresh, @"return false;").Count;
        int told = Regex.Matches(refresh, @"Notifications\.Show\(NotificationType\.Warning, ").Count;
        Check(refresh.Contains("InteriorRules.RefreshRefused(Parts.WearScale, s != null, f != null)") && falses == 2 && told == 2
              && refresh.IndexOf("Notifications.Show(NotificationType.Warning, refused", StringComparison.Ordinal) < refresh.IndexOf("return false;", StringComparison.Ordinal),
            "interiors refresh: every way it does nothing tells the player why in a notice, as the money short does (" + told + " notices for " + falses + " refusals)");
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), language + ".json"));
            Check(words != null && words.TryGetValue("reallife_interior_wear_off", out string off) && !off.Contains("{")
                  && words.TryGetValue("reallife_interior_refresh_unknown", out string unknown) && unknown.Contains("{business}") && (language != "en" || unknown.Length <= 240 && off.Length <= 240),
                "interiors refresh (" + language + "): both refusals are written, the record's naming the business, within 240 characters");
        }
    }

    private static void InteriorWearTest()
    {
        string director = ModSource(@"Interiors\InteriorDirector.cs"), tests = ModSource(@"Interiors\InteriorDirector.Tests.cs");
        string age = MemberText(director, "private void AgeFinishes(InteriorFile f, InteriorShop s, int days, int wearScale)");
        Check(age.Contains("InteriorBridges.Cleaned(s.Place)") && age.Contains("InteriorRules.Age(f.Wear, s.Clean, cleaned, wearScale)")
              && Regex.Matches(director, @"InteriorRules\.Age\(").Count == 1 && director.Contains("AgeFinishes(f, s, days, p.WearScale);"),
            "interiors wear: the finishes age in one place, at the wear option's strength, less with the cleaning service");
        string test = MemberText(tests, "public void TestWear()");
        Check(test.Contains("AgeFinishes(f, s, 100, Parts.WearScale);") && test.Contains("\"reallife_interior_wear_off\"") && !test.Contains("InteriorRules.Age("),
            "interiors wear: the test ages a hundred days as play would, and with wear off says it ages nothing");
    }
}
