using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// BizMan's Browse Store Inventory and Show employees: each reaches the game's own method (BrowseInventory fills and opens
// UIs.itemsList, the employees button opens UIs.rivalEmployeesUi on the business), both windows are drawn by the kit as boxes,
// and a box opened from inside an app of the full menu stands over the app and its frame, which is what the player did not see.
// Read from the sources, as the screens are game UI the checker cannot run.
internal static partial class Program
{
    private static string Lines(string code, string from, int count)
    {
        int at = code.IndexOf(from, StringComparison.Ordinal);
        int end = at;
        for (int i = 0; at >= 0 && i < count; i++)
        {
            end = code.IndexOf((char)10, end + 1);
        }
        return at < 0 || end < 0 ? string.Empty : code.Substring(at, end - at);
    }

    private static void BizManGoodsAll()
    {
        // The order: a box in the full menu goes over the app and All apps (one above the app); outside it keeps its own.
        Check(UiHostRules.OverMenu(5, 9, true) == 11 && UiHostRules.OverMenu(5, 9, false) == 5 && UiHostRules.OverMenu(30, 9, true) == 30,
            "bizman goods: a box opened inside the full menu stands over the app and its frame, a box outside it keeps its own order");

        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs");
        string browse = Lines(press, "case BizManPages.DoBrowse:", 1);
        string staff = Lines(press, "case BizManPages.DoEmployees:", 2);
        Check(press.Contains("case BizManPages.DoBrowse: return Click(Calling(pres, \"BrowseInventory\"), \"browse the store's goods\");")
              && press.Contains("Click(pres.showEmployeesButton.GetComponentInChildren<Button>(true), \"show the business's employees\")"),
            "bizman goods: both buttons press the game's own call (BrowseInventory, the employees button), the store chosen by the page they are on");
        Check(!browse.Contains("Classic") && !staff.Contains("Classic") && browse.Length > 0 && staff.Length > 0 && !press.Contains("itemsList.Toggle") && !press.Contains("rivalEmployeesUi"),
            "bizman goods: neither button hands the player to the game's classic view or opens a native window by itself");

        // The two windows are the kit's boxes.
        string items = ModSource(@"Ui\World\ItemsListKit.cs");
        string employees = ModSource(@"Ui\Employees\EmployeesKit.cs");
        Check(items.Contains("public UiGameKind Kind => UiGameKind.Box;") && items.Contains("return uis != null ? uis.itemsList : null;"),
            "bizman goods: the store's goods are the kit's items list, a box on the game's itemsList");
        Check(MemberText(employees, "private sealed class RivalGameScreen").Contains("UiGameKind Kind => UiGameKind.Box;")
              && employees.Contains("return uis != null ? uis.rivalEmployeesUi : null;") && employees.Contains("new RivalStaffScreen((RivalEmployeesUi)found)"),
            "bizman goods: the business's staff are the kit's rival staff screen, a box on the game's rivalEmployeesUi");

        // The host lifts a box over the app, at every show.
        string host = ModSource(@"Ui\Host\UiGameHost.cs");
        Check(MemberText(host, "private void Sort()").Contains("_canvas.sortingOrder = OrderNow();")
              && MemberText(host, "private int OrderNow()").Contains("UiHostRules.OverMenu(_order, AppOrder(), _screen.Kind == UiGameKind.Box && global::UI.Smartphone.FullMenu.IsOpen)"),
            "bizman goods: every show of a box sorts it by the full menu's state");

        // The shift card takes a component that is already on the copied panel.
        string card = ModSource(@"Jobs\ShiftCard.cs");
        Check(!card.Contains("= panel.AddComponent<CanvasGroup>()") && card.Contains("panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>()")
              && card.Contains("if (panel.GetComponent<Canvas>() == null)"),
            "shift card: the canvas group and canvas are taken when the copied panel has them, added only when not");

        // The What's new line, in every language.
        foreach (string code in new[] { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            string next = WhatsNewText(words, WhatsNewKey(LoadWords(System.IO.Path.Combine(DesignLocales, "en.json")), "the Browse Store Inventory and Show employees buttons now open"));   // the row that holds it
            Check(words != null && next.Contains("BizMan") && next.Contains("Browse Store Inventory") == (code == "en")
                  && !next.Contains('!') && !next.Contains((char)0x2014) && !next.Contains((char)0x2013),
                "bizman goods: What's new says so in " + code);
        }
    }
}
