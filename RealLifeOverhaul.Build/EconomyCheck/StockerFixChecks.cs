using System;
using System.IO;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;

internal static partial class Program
{
    private static void StockerFixes()
    {
        string jobs = IOPath.GetFullPath(IOPath.Combine(AppContext.BaseDirectory, "../../../../../Assets/Mods/RealLifeOverhaul/Scripts/Jobs"));
        string stocker = File.ReadAllText(IOPath.Combine(jobs, "Kinds/StockerJob.cs"));
        string shelf = MemberText(stocker, "private void ToShelf(float now, bool inside)");
        Check(InOrder(shelf, "_hasShelf = true;", "Mark(_shelfStand, _shelfLabel);",
                  "Up(_panelShelf, ButtonStock, JobSpots.Flat(Ctx.Player.transform.position, _shelfStand) < ShelfReach, false,")
              && !shelf.Contains("if (JobSpots.Flat("),
            "stocker: every chosen shelf has its panel at any distance, with stocking enabled only in reach");
        Check(InOrder(shelf, "if (!inside)", "Unmark();", "_step = Step.CarryIn;", "return;", "if (!_hasShelf)"),
            "stocker: stepping outside clears the indoor marker before carrying the box back in");

        string act = MemberText(stocker, "private void Act(string action)");
        Check(InOrder(act, "ReferenceEquals(action, ButtonStock) && _step == Step.ToShelf && _hasShelf",
                  "&& JobSites.Inside(_client) && JobSpots.Flat(Ctx.Player.transform.position, _shelfStand) < ShelfReach)", "_carry.Take();"),
            "stocker: a shelf action from a previous tick cannot stock from outside or beyond reach");

        string sites = MemberText(File.ReadAllText(IOPath.Combine(jobs, "JobSites.cs")), "public static bool ShelfSpot(");
        Check(sites.Contains("out string itemName)") && sites.Contains("itemName = null;")
              && InOrder(sites, "ShopFloor.RandomShelf(out Vector3 standAt, out Vector3 at, out _, out string name)",
                  "spot = standAt;", "shelf = at;", "itemName = name;", "return true;")
              && InOrder(shelf, "out _shelfStand, out _shelfAt, out string shelfName)", "_hasShelf = true;", "if (Meta.Dev.Verbose)",
                  "Ctx.Logger?.Info(", "shelfName", "JobSpots.Flat(Ctx.Player.transform.position, _shelfStand).ToString(\"0.0\", System.Globalization.CultureInfo.InvariantCulture)"),
            "stocker: choosing a shelf logs its accepted item name and standing-point distance only in verbose diagnostics");

        // The shelf had no prompt at all: its panel stood over the shelf itself, and the game's top-down indoor camera sees
        // about 5 m behind the player to 10 m ahead while indoors its guider is hidden, so a shelf further off had its panel
        // drawn off the screen. The panel is now kept in the picture, at the screen's edge toward the shelf (NavCheck,
        // PanelEdgeCheck.cs, has the geometry).
        string up = MemberText(stocker, "private void Up(");
        string panel = File.ReadAllText(IOPath.Combine(jobs, "JobPanel.cs"));
        string ui = IOPath.GetFullPath(IOPath.Combine(jobs, "../Core/GameUi"));
        string Ui(string name) => File.Exists(IOPath.Combine(ui, name)) ? File.ReadAllText(IOPath.Combine(ui, name)) : string.Empty;   // a file gone fails the check, never the checker
        Check(up.Contains("_panel.Show(over);") && Regex.IsMatch(panel, @"^\s*_panel\.KeepInView = true;", RegexOptions.Multiline)
              && MemberText(panel, "public bool Show(Vector3 over)").Contains("_panel.Position = over;") && !panel.Contains("Fit(")
              && Regex.IsMatch(Ui("WorldPanel.cs"), @"^\s*return KeepInView && InView\(at, out Vector3 shown\) \? shown : at;", RegexOptions.Multiline)
              && Regex.IsMatch(Ui("WorldPanel.View.cs"), @"^\s*if \(!PanelEdgeRules\.Edge\(", RegexOptions.Multiline),
            "stocker: the shelf's panel is the game's own, kept on the screen at its edge toward a shelf out of the picture");
        string tell = MemberText(stocker, "private void TellShelf()");
        Check(InOrder(shelf, "Up(_panelShelf, ButtonStock,", "if (!_shelfTold && Meta.Dev.Verbose)", "_shelfTold = true;", "TellShelf();")
              && InOrder(shelf, "_hasShelf = true;", "_shelfTold = false;") && tell.Contains("\"Jobs check (stocker shelf): the shelf to stock is \"")
              && tell.Contains("!_panel.Up ? \"not up: FAIL") && tell.Contains("_panel.AtEdge ?") && tell.Contains(": PASS.")
              && InOrder(shelf, "if (++_tries >= 5)", "\"Jobs check (stocker shelf): no shelf of \"", "Ctx.Say(\"reallife_job_stocker_no_shelf\", 8f);"),
            "stocker: one self-check line a shelf says whether its panel is up, over the shelf or at the screen's edge, and one says when no shelf was found");
        Check(FixLineEverywhere("now shows where the box goes as soon as you carry it in", "agora mostra onde vai a caixa assim que você a leva para dentro",
                                "一把箱子搬进店里就会显示该放到哪里", "zeigt jetzt, wohin die Kiste gehört, sobald du sie hineinträgst",
                                "montre maintenant où va le carton dès que vous l'apportez à l'intérieur", "ahora muestra dónde va la caja en cuanto la llevas dentro",
                                "теперь показывает, куда нести коробку, как только её внесли в магазин", "laat nu zien waar de doos heen moet zodra je hem naar binnen draagt",
                                "kutuyu içeri taşıdığınız anda nereye gideceğini gösteriyor"),
            "stocker: What's new has the line of the shelf's panel kept on the screen, in all nine languages");
    }
}
