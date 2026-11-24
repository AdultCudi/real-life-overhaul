using System;

// The sweep of 24 Nov: BizMan's page reads the game's alert rows by their words and not their number alone, the mark beside a price says what it means, and the Company app tells a business's
// campaign reports and claims apart by its place and never by its name. BizMan is the game's own UI and the Company's live source reads the game, so these are read from the sources, as the
// screens' other source checks are; the Company's scroll and store checks run the code itself (UiChecks.Company.cs, Checks5.cs).
internal static partial class Program
{
    private static void Ui27Checks()
    {
        string page = ModSource(@"Ui\BizMan\BizManScreen.Page.cs");
        string sig = Lines(page, "sig = sig * 31 + RowsHash(need)", 1);
        Check(sig.Contains("RowsHash(alert)") && !page.Contains("EntryCount(alert)") && !page.Contains("EntryCount(need)"),
            "bizman alerts: the page is laid again when the game's requirement or alert rows change their words, not only their number (a business with as many alerts as the one before it shows its own)");
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs");
        string pages = ModSource(@"Ui\BizManPages.Parts.cs");
        Check(parts.Contains("_m.MarkLine = marked ? UiText.T(\"reallife_ui_bizman_price_marks\") : null;") && pages.Contains("Lead(s, at, m.MarkLine, x, ly, half)"),
            "bizman prices: where a price carries the game's yellow or red mark the page says what it means in a line under the week's figures");
        string money = ModSource(@"Ui\Company\CompanyLive.Money.cs");
        Check(money.Contains("!OfBusiness(r, place, business)") && !money.Contains("r.Name != business") && !money.Contains("o.Name != c.Name") && !money.Contains("d.Shops[i].Name != c.Name"),
            "company marketing and cover: a business's earlier campaigns and claims are found by its place, never by its name (two businesses may share one or differ by a letter's case)");
        // Every kind of row the Planner raises (CompanyDirector.Alert's first figure) has a place for its Open button on the Overview's "also waiting", and the page offers no button to a kind it
        // does not know: an Open that led to the page the owner stood on did nothing, which is what the overtime offer's did.
        string overview = ModSource(@"Ui\Company\CompanyLive.Overview.cs");
        string handler = overview.Substring(Math.Max(0, overview.IndexOf("private static CompanyAct AlertOpen", StringComparison.Ordinal)));
        var raised = new System.Collections.Generic.SortedSet<int>();
        foreach (string file in new[] { "CompanyDirector.cs", "CompanyDirector.Cover.cs", "CompanyDirector.Licences.cs", "CompanyDirector.Marketing.cs", "CompanyDirector.Staff.cs", "CompanyDirector.Tax.cs" })
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ModSource(@"Economy\Company\" + file), @"\bAlert\((\d+),"))
            {
                raised.Add(int.Parse(m.Groups[1].Value));
            }
        }
        string rotaSource = ModSource(@"Economy\Company\CompanyDirector.Rota.cs");
        Check(rotaSource.Contains("Alert(RotaAlertKind,") && System.Text.RegularExpressions.Regex.IsMatch(rotaSource, @"RotaAlertKind\s*=\s*30\b") && overview.Contains("CompanyAlertRota = 30;") && handler.Contains("case CompanyAlertRota:"),
            "company waiting: the rota's row kind (30) is the one the Overview opens the Rota for");
        string missing = string.Empty;
        foreach (int kind in raised)
        {
            missing += handler.Contains("case " + kind + ":") ? string.Empty : " " + kind;
        }
        Check(raised.Count >= 14 && missing.Length == 0, "company waiting: every kind of Planner row (" + raised.Count + " found) has an Open target of its own" + (missing.Length > 0 ? ", none for:" + missing : string.Empty));
        Check(handler.Contains("return default(CompanyAct);") && overview.Contains("OpenLabel = open.Do != CompanyDo.None ?"),
            "company waiting: a kind with no target is offered no Open button");    }
}
