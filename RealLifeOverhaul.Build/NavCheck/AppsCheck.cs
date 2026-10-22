using System;
using System.IO;

// Checks, over the mod's sources as they ship, that the phone apps' work stays where it costs nothing to see or is done once:
// an app's panel built when it is first opened, not
// as it installs in the first seconds after a load; the box's parts kept from page to page and set back to their template
// before they are used again; the Cash flow tab and a bank's statement written as they scroll; the City Ledger's pages kept
// for the paper they were laid out for, and its serif looked for through SceneFinds only when the game's is missing; Flare's
// pictures from the photo's small copy made off the main thread; the Stocks chart's pointer redrawing one bar; and every
// opening and click of an app timed under a name of its own. The game-free parts are checked by running them (EconomyCheck,
// AppsChecks.cs); what is here is that the game's side uses them.
//
// What is not here: how long anything takes in the game, which the "Session: a frame of ..." lines and the HotSpots names
// "Apps: (id) opened", "Apps: (id) built", "Click: ..." and "Table: ..." of Player.log show.
internal static partial class Program
{
    private static void AppsSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!Directory.Exists(scripts))
        {
            Check(false, "apps: the mod's sources are at " + scripts);
            return;
        }

        // An app's panel is built when it is first opened.
        string phone = Source(scripts, @"Core\GameUi\PhoneApps.cs");
        string install = Body(phone, "public static PhoneApp Install(");
        string ready = Body(phone, "public bool Ready()");
        string open = Body(phone, "public void Open()");
        Check(install.Length > 0 && !install.Contains(".Build(") && install.Contains("_pending = true"), "apps: an app's install puts its buttons on the phone and builds no panel");
        Check(ready.Contains("_def.Build(_menu, _dormant)") && ready.Contains("Remove()") && Count(phone, ".Build(") == 1, "apps: the panel is built in one place (Ready), and an app whose panel cannot be built is taken off the phone");
        int built = open.IndexOf("Ready()", StringComparison.Ordinal), selected = open.IndexOf("Select()", StringComparison.Ordinal);
        Check(built >= 0 && selected > built, "apps: opening an app builds its panel first");
        bool elsewhere = false;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = Code(File.ReadAllText(file));
            elsewhere |= !file.EndsWith("PhoneApps.cs", StringComparison.Ordinal) && (code.Contains("_def.Build(") || code.Contains("def.Build(menu"));
        }
        Check(!elsewhere, "apps: no other file builds an app's panel");
        string special = Body(Source(scripts, @"Ledger\LedgerPage.Special.cs"), "public bool ShowSpecial(");
        Check(special.Contains("_app.Ready()") && special.IndexOf("_app.Ready()", StringComparison.Ordinal) < special.IndexOf("Tabs(false)", StringComparison.Ordinal),
            "apps: the paper's special front page builds the page before it hides its tabs");

        // Openings and clicks are timed.
        Check(open.Contains("HotSpots.Time(_hotOpened)") && Body(phone, "internal void Shown()").Contains("HotSpots.Time(_hotOpened)") && Body(phone, "internal void Hidden()").Contains("HotSpots.Time(_hotOpened)")
              && ready.Contains("HotSpots.Time(_hotBuilt)"), "apps: an app's opening, closing and build are timed under its own names");
        Check(Body(Source(scripts, @"Core\GameUi\UiClone.cs"), "public static void Safe(Action action)").Contains("HotSpots.Time(Guard.HandlerSlot(action, \"Click\"))"),
            "apps: every click the kit rewires is timed under the class and method its code is written in");
        string view = Source(scripts, @"Core\GameUi\TableView.cs");
        Check(Body(view, "private void HeaderClicked(").Contains("HotSpots.Time(SlotOf(") && Body(view, "private void Picked(").Contains("HotSpots.Time(SlotOf("),
            "apps: a table's sort and filter are timed under the table's name");

        // The box's parts are kept, and set back to their template before they are used again.
        string box = Source(scripts, @"Core\GameUi\InfoBox.cs");
        string keep = Source(scripts, @"Core\GameUi\InfoBox.Keep.cs");
        string add = Body(box, "private GameObject Add(");
        Check(add.Contains("parent == null && Kept(template)") && add.Contains("Reuse(template)") && Body(box, "public void Clear()").Contains("Park(part, _liveFrom[i])"),
            "apps: a part is taken back only into the box, and the box's parts are laid aside when it is cleared");
        string kept = Body(keep, "private bool Kept(");
        string[] kinds = { "_bodyT", "_subT", "_noteT", "_headerT", "_headingT", "_pairT", "_meterT", "_checkT", "_footT", "_switch", "_toggleT", "_selector", "_buttons" };
        bool listed = true;
        foreach (string kind in kinds)
        {
            listed &= kept.Contains("template == " + kind);
        }
        Check(listed && !kept.Contains("_chipsT") && !kept.Contains("_chipT") && !kept.Contains("_title") && !kept.Contains("_line ") && !kept.Contains("_headline"),
            "apps: the box keeps its own kinds of part (rows, lines, notes, headers, sections, pairs, meters, ticks, the foot, switches, settings switches, selectors, buttons), and no chip");
        string restore = Body(keep, "private void Restore(");
        string[] setBack =
        {
            "color = _lineColour", "margin = _headerMargin", "Find(\"Chips\")", "color = _checkColour", "mark.sprite = _checkMarkSprite", "mark.color = _checkMarkColour",
            "sizeDelta = _checkMarkSize", "anchoredPosition = _checkMarkAt", "SetIsOnWithoutNotify(_switchOn)", "SetIsOnWithoutNotify(_toggleOn)", "template == _pairT || template == _selector || template == _buttons"
        };
        bool all = true;
        foreach (string piece in setBack)
        {
            all &= restore.Contains(piece);
        }
        Check(all && Count(restore, "Prune(") == 2, "apps: a part laid aside is set back to its template: a line's colour, a header's margin and chips, a tick's colour and mark, a switch's state, and what was put under a part made of others");
        string reuse = Body(keep, "private GameObject Reuse(");
        string park = Body(keep, "private void Park(");
        Check(reuse.Contains("SetParent(_box, false)") && park.IndexOf("Restore(part, template)", StringComparison.Ordinal) >= 0
              && park.IndexOf("Restore(part, template)", StringComparison.Ordinal) < park.IndexOf("SetParent(_parked", StringComparison.Ordinal),
            "apps: a part is set back while it is still awake, then laid aside, and taken back as the last of the box's children");

        // Tables of thousands of rows written as they scroll.
        string finance = Source(scripts, @"Economy\Finance\FinanceApp.cs"), cashFlow = Body(Source(scripts, @"Economy\Finance\FinanceApp.CashFlow.cs"), "private void WriteCashFlow(");
        Check(cashFlow.Contains("_flow.Read(") && !cashFlow.Contains("Row(\"cash:") && finance.Contains("_table.Show(_spec, _cash, selected)") && finance.Contains("Refresh(false)"),
            "apps: the Cash flow tab is written as it scrolls, and a filter picked writes the box again, not the table");
        string bank = Source(scripts, @"Economy\Banking\BankApps\BankApp.cs");
        Check(!Body(bank, "private void Statement(").Contains("Row(\"cash:") && bank.Contains("_table.Show(_spec, _cash, selected)") && Body(bank, "private void Statement(").Contains("_flow.Read(_period, today, IsHouse ? null : _mine)"),
            "apps: a bank's statement reads only its own movements and is written as it scrolls");
        string cell = Source(scripts, @"Core\GameUi\DataTable.cs");
        Check(Body(cell, "public override void SetData(TableRow row)").Contains("row.Ready()") && Body(cell, "protected override string GetDataId(").Contains("IdOf(model)"),
            "apps: a row of a table written as it scrolls is written when the scroller shows it");

        // The City Ledger.
        string ledger = Source(scripts, @"Ledger\LedgerPage.cs");
        string turn = Body(ledger, "private void Turn(int page)");
        int same = turn.IndexOf("now.Same(_laid[_page])", StringComparison.Ordinal), begin = turn.IndexOf("rules.Begin()", StringComparison.Ordinal);
        int end = turn.IndexOf("rules.End()", StringComparison.Ordinal), stamped = turn.IndexOf("_laid[_page] = now", StringComparison.Ordinal);
        Check(same >= 0 && begin > same && stamped > end && end > begin, "apps: a page of the paper is laid out only when what it is laid out for has changed, and stamped once it is written whole");
        string type = Source(scripts, @"Ledger\LedgerType.cs");
        Check(!type.Contains("Resources.FindObjectsOfTypeAll") && type.Contains("SceneFinds.InMemory<TMP_FontAsset>(")
              && type.IndexOf("GetFontByName", StringComparison.Ordinal) < type.IndexOf("SceneFinds.InMemory", StringComparison.Ordinal),
            "apps: the paper's serif is asked of the game by name, and the fonts in memory gone through only through SceneFinds when it has none");

        // Flare's pictures.
        string pictures = Source(scripts, @"Social\Flare\FlareApp.Pictures.cs");
        Check(pictures.Contains("PhotoThumbs.Get(path") && Count(pictures, "LoadImage(") == 1 && Body(pictures, "private static Texture2D Drawn(").Contains("LoadImage(")
              && !Body(pictures, "public void Closed()").Contains("Destroy"), "apps: Flare shows a photo's small copy, keeps it across openings, and lets the engine decode only a PNG the copy cannot be made from");
        Check(Body(Source(scripts, @"Social\Flare\FlareApp.cs"), "public void Closed()").Contains("_pictures.Closed()"), "apps: closing Flare keeps its pictures");
        Check(Source(scripts, @"Leisure\Photo\PhotoMode.cs").Contains("PhotoThumbs.MakeSoon(_shotPath)"), "apps: a photo's small copy is started when the photo is taken");

        // The Stocks app.
        string chart = Source(scripts, @"Markets\MarketChart.cs"), drawing = Source(scripts, @"Markets\MarketChart.Drawing.cs");
        string move = Body(chart, "public void OnPointerMove(");
        Check(!chart.Contains("Func<double, float>") && drawing.Contains("private void Draw()") && !drawing.Contains("Func<double, float>") && move.Contains("UiSafe(_showHover)")
              && !move.Contains("_draw") && Body(chart, "public void OnPointerExit(").Contains("UiSafe(_showHover)"),
            "apps: the pointer over the Stocks chart redraws the bar over one candle and the legends, and a drawing makes no function of its own");
        string controls = Body(Source(scripts, @"Markets\MarketPage.cs"), "private void Controls(");
        Check(controls.IndexOf("return;", StringComparison.Ordinal) >= 0 && controls.IndexOf("return;", StringComparison.Ordinal) < controls.IndexOf("Destroy(", StringComparison.Ordinal),
            "apps: the selectors under a company's page are kept while they are the ones it asks for");

        // App icons.
        string painter = Source(scripts, @"Core\GameUi\AppIconPainter.cs");
        Check(Body(painter, "private static Color32[] Read(").IndexOf("Reads.TryGetValue", StringComparison.Ordinal) < Body(painter, "private static Color32[] Read(").IndexOf("ReadPixels", StringComparison.Ordinal)
              && Body(painter, "public static Sprite Paint(").Contains("Made.TryGetValue"), "apps: an app's icon and the game's pictures it is painted from are read back once a session");
    }

    // How many times a text is in a source.
    private static int Count(string code, string what)
    {
        int n = 0;
        for (int at = code.IndexOf(what, StringComparison.Ordinal); at >= 0; at = code.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}
