using System;
using System.IO;
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

        string up = MemberText(stocker, "private void Up(");
        string panel = File.ReadAllText(IOPath.Combine(jobs, "JobPanel.cs"));
        Check(up.Contains("_panel.Show(over, Ctx.Player);")
              && MemberText(panel, "public bool Show(Vector3 over, PlayerController player)").Contains("_panel.Position = Fit(over, player);")
              && InOrder(MemberText(panel, "private static Vector3 Fit("), "Vector3.Dot(", "< 0.5f)",
                  "return player.transform.position + Vector3.up * 2.1f;"),
            "stocker: the shelf uses the native job panel with its behind-camera fallback");
    }
}
