using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// The whole-building check of the district (Scripts/Uptown: CompleteRules, LifeDirector.Complete, LifeGame.Complete) is a developer's
// diagnostic: its result is a line in Player.log and never a card on the player's screen. A player on 0.63.8 saw "new building(s) north
// of Manhattan are not yet whole buildings of the game" at every load with a number that grew as the game's market let out more homes
// and warehouses; there is nothing a player can do about such a card.
internal static partial class Program
{
    private static void UptownNoticeChecks()
    {
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Uptown"));
        string[] sources = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.cs") : new string[0];
        Check(sources.Length > 40, "uptown notice: the district's sources are found (" + sources.Length + ")");

        // No notice for the diagnostic: the text, its constant and the call that showed it are gone, and nothing in the district names the key.
        Check(sources.All(s => !File.ReadAllText(s).Contains("reallife_uptown_incomplete") && !File.ReadAllText(s).Contains("SayIncomplete") && !File.ReadAllText(s).Contains("LifeText.Incomplete")),
            "uptown notice: no source of the district shows the 'not yet whole buildings' card");
        Check(codes.All(c => LocaleText(c, "reallife_uptown_incomplete") == null), "uptown notice: the card's text is in no language any more");

        // The files that run the check do not talk to the player at all, at a load or at a stand.
        string[] runs = { "LifeDirector.Complete.cs", "LifeGame.Complete.cs", "CompleteRules.cs" };
        Check(runs.All(r => !Regex.IsMatch(File.ReadAllText(System.IO.Path.Combine(dir, r)), @"Notifications\.|NotificationType|HudConfirm|NoticeDue")), "uptown notice: the whole-building check only writes to Player.log");
        string stand = File.ReadAllText(System.IO.Path.Combine(dir, "LifeDirector.Stand.cs"));
        Check(stand.Contains("CompleteOne(made, f, \"its stand\");") && !stand.Contains("StandIncomplete"), "uptown notice: a building that stands is checked and logged, nothing more");

        // The line of the check still names the building, its lot and what is missing, whatever happens to the card.
        var f = new CompleteFacts { Street = "reallife:street_uptown_1", Number = 3, PlanNumber = 3, DistrictStreet = true, Type = "ba:buildingtype_office", RecordType = "ba:buildingtype_office" };
        int passed = CompleteRules.Passed(f);
        string line = CompleteRules.Line("the load", "3 Test Street", f, passed, "uptown_1_01_0001");
        Check(!CompleteRules.All(passed) && line.Contains("lot uptown_1_01_0001") && line.Contains("; missing ") && line.EndsWith(": FAIL"), "uptown notice: a building not whole still gets its line in Player.log");

        // A district line written by 0.63.8 or 0.63.9 carries the old count (^w:); it loads, the rest of it is kept, and the count is not written again.
        string old = LifeStore.Write(new LifeState { Split = 2 }) + "^w:7";
        LifeState read = LifeStore.Read(old, out _);
        string again = read != null ? LifeStore.Write(read) : null;
        Check(read != null && read.Split == 2 && again != null && !again.Contains("^w:"), "uptown notice: a line with the old count still loads, keeps its other fields and drops the count");
        LifeState bad = LifeStore.Read("v=1^f:^g:2^w:abc", out _);
        Check(bad != null && !LifeStore.Write(bad).Contains("^w:"), "uptown notice: a bad old count is dropped the same way");
    }
}
