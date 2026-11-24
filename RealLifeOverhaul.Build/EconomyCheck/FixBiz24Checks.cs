using System;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// Four players' reports mended together, each reproduced here:
//   1. buying a rival's business ended the game's takeover in an exception (it was given no BizMan page), which switched Services, Sell off for the session;
//   2. a purchasing agent's goods row cut its last figures (last week's sales) off the end of one long line;
//   3. the Property rent roll drew its empty message over the lists under it;
//   4. a job above the builder's licence said only that the licence did not cover it, its time limit left out the wait for its materials, and the managers stopped at 64.
internal static partial class Program
{
    private static bool _biz24Checked;

    private static void FixBiz24()
    {
        if (_biz24Checked)
        {
            return;
        }
        _biz24Checked = true;
        FixBiz24Takeover();
        FixBiz24Purchasing();
        FixBiz24Roll();
        FixBiz24Licence();
        FixBiz24Managers();
    }

    private static string ScriptText(string relative)
    {
        string path = System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", relative);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void FixBiz24Takeover()
    {
        string game = ScriptText(@"Economy\Services\ServicesGame.Rivals.cs");
        string overtake = MemberText(game, "public static void Overtake(");
        int pointed = overtake.IndexOf("page.SetAddress(reg.Address)", StringComparison.Ordinal);
        Check(pointed >= 0 && overtake.IndexOf("BizManPresentation.OvertakeBusiness(reg, page)", StringComparison.Ordinal) > pointed,
            "biz24: the game's takeover is given the BizMan page, pointed at the building (its last steps read the page's address and threw without one)");
        // No path of the mod hands the game's takeover no page: the one place that calls it is Overtake.
        int calls = 0, nulls = 0;
        foreach (string file in Directory.GetFiles(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            for (int at = text.IndexOf("OvertakeBusiness(", StringComparison.Ordinal); at >= 0; at = text.IndexOf("OvertakeBusiness(", at + 10, StringComparison.Ordinal))
            {
                calls++;
                nulls += text.Substring(at, Math.Min(40, text.Length - at)).Contains("null") ? 1 : 0;
            }
        }
        Check(calls == 1 && nulls == 0, "biz24: the game's takeover is called in one place and never with no page (" + calls + " call(s), " + nulls + " with null)");
        Check(MemberText(game, "public static bool Buy(").Contains("Overtake(reg);") && ScriptText(@"TestShops\TestShopsGame.cs").Contains("Economy.ServicesGame.Overtake(reg);"),
            "biz24: buying from a rival and the test shops both take a business over through it");
        string rivals = ScriptText(@"Economy\Services\ServicesDirector.Rivals.cs");
        string mend = MemberText(game, "public static bool Mend(");
        Check(MemberText(rivals, "private void RivalSalesDay(").Contains("ServicesGame.Mend(reg, v)") && mend.Contains("BizManSchedule.AutoFillSchedule(page)")
              && mend.Contains("reg.RentedByPlayer") && mend.Contains("IsNullOrEmpty(reg.businessOwnerRivalId)"),
            "biz24: a takeover that stopped half way in a save is finished on the next day's pass, only for a building the player rents with no rival owner");
        Check(rivals.Contains("SaleGuard(\"buying a rival's business\"") && rivals.Contains("SaleGuard(\"completing a purchase\""),
            "biz24: a fault in a purchase still stops only the sale part, with one log line");
    }

    private static void FixBiz24Purchasing()
    {
        string market = ScriptText(@"Ui\BizMan\BizManScreen.Market.cs"), managers = ScriptText(@"Ui\BizMan\Ops\BizManScreen.Managers.cs");
        string table = MemberText(managers, "private void BuyTable(");
        Check(table.Contains("ImportFigures(p)") && table.Contains("reallife_ui_bizops_col_limit") && table.Contains("reallife_ui_bizops_col_index")
              && table.Contains("reallife_ui_bizops_col_previous") && table.Contains("reallife_ui_bizops_col_sold") && !table.Contains("ImportNote"),
            "biz24: a purchasing agent's goods have a column each for the weekly limit, the index, last week's order and last week's sales");
        string figures = MemberText(market, "private static string[] ImportFigures(");
        Check(figures.Contains("ShouldLimitImporterMaxAmount") && figures.Contains("importPriceIndex") && figures.Contains("amountOrderedLastWeek") && figures.Contains("ProductsSoldLastWeek"),
            "biz24: the four figures are the game's own, read as its purchasing row reads them");
        int cells = table.IndexOf("Cells = new[]", StringComparison.Ordinal);
        string tail = cells >= 0 ? table.Substring(cells) : string.Empty;
        Check(tail.Contains("Values = new[] { (int)UiIcon.Package, 0, 0, 0, 0, 0, 0, 0, 0 }") && tail.Contains("fig[3]"), "biz24: every row carries a cell, a value and a tone for each of the nine columns");
    }

    private static void FixBiz24Roll()
    {
        // A window of the player's own size shows the page short of room: the frame's height is taken down from the full menu's.
        foreach (float shorter in new[] { 1f, 0.8f, 0.7f, 0.6f, 0.5f })
        {
            float aspect = 16f / 9f;
            var s = new UiScene();
            UiWords words = UiWords("en");
            EstateModel m = UiSamples.Estate(words, "roll_empty");
            m.Table.EmptyTitle = words("reallife_estate_row_nothing_owned", "You own no building. The Market tab lists what is for sale.");
            m.Table.EmptyBody = words("reallife_ui_estate_empty_roll_body", "The buildings you own appear here, a line for each unit, with its tenant and its rent.");
            UiFrame frame = UiFrame.FullMenu(aspect, "Property");
            frame.Height *= shorter;
            EstateFace.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
            int crossed = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem a = ref s.Items[i];
                if (a.Kind != UiKind.Text || string.IsNullOrEmpty(a.Text))
                {
                    continue;
                }
                for (int j = i + 1; j < s.ItemCount; j++)
                {
                    ref UiItem b = ref s.Items[j];
                    if (b.Kind != UiKind.Text || string.IsNullOrEmpty(b.Text) || a.Layer != b.Layer)
                    {
                        continue;
                    }
                    float w = Math.Min(a.X + a.W, b.X + b.W) - Math.Max(a.X, b.X), h = Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y);
                    if (w > 1f && h > 1f)
                    {
                        crossed++;
                    }
                }
            }
            Check(crossed == 0, "biz24: on the rent roll with no building no two texts cross, so the empty message stays off the lists under it (" + crossed + " crossed at " + shorter.ToString("0.0") + " of the height)");
        }
        Check(MemberText(ScriptText(@"Ui\UiDraw.EmptyState.cs"), "public static int EmptyState(").Contains("block > h && lh > 0f"),
            "biz24: an empty state in a room too short for it drops its sentence rather than spilling over what is under it");
    }

    private static void FixBiz24Licence()
    {
        Check(LevelRules.LevelFor(1) == 0 && LevelRules.LevelFor(6) == 0 && LevelRules.LevelFor(7) == 1 && LevelRules.LevelFor(8) == 1 && LevelRules.LevelFor(9) == 2 && LevelRules.LevelFor(12) == 3
              && LevelRules.LevelFor(99) == LevelRules.Levels - 1,
            "biz24: the licence level a building's floors need is the lowest that covers them, the top for any taller");
        for (int floors = 1; floors <= 12; floors++)
        {
            int level = LevelRules.LevelFor(floors);
            Check(LevelRules.Allows(level, floors) && (level == 0 || !LevelRules.Allows(level - 1, floors)), "biz24: " + floors + " floors: the level named covers them and the one under it does not");
        }
        // A job with materials waits for each stage's delivery; one with none (a demolition) waits for nothing.
        var roads = new ProjectFile { Kind = ProjectKind.Contract, CivicKind = 0, Hours = 50 };
        var demolition = new ProjectFile { Kind = ProjectKind.Contract, CivicKind = RedevelopRules.DemolitionWorks, Hours = 50 };
        int wait = SiteRules.DeliveryDays(roads);
        Check(wait > BuildCatalogue.ShortFollowDays && SiteRules.DeliveryDays(demolition) == 0 && SiteRules.DeliveryDays(null) == 0,
            "biz24: the time limit allows the roadworks' materials' wait (" + wait + " working days) and a job with no materials none");
        var rebuild = new ProjectFile { Kind = ProjectKind.Rebuild, Use = BuildUse.Offices, Area = 1200, Storeys = 4, Hours = 160, CostPermille = 1000 };
        int quickest = Math.Min(BuildCatalogue.MerchantDays[0], Math.Min(BuildCatalogue.MerchantDays[1], BuildCatalogue.MerchantDays[2]));
        Check(wait == 4 * quickest + BuildCatalogue.ShortFollowDays && SiteRules.DeliveryDays(rebuild) == 3 * quickest + BuildCatalogue.ShortFollowDays,
            "biz24: each stage that takes materials waits for its delivery: the roadworks' four, a rebuild's three (the ground work takes none), and a short delivery's rest behind them");
        string market = ScriptText(@"Construction\ConstructionDirector.Market.cs"), projects = ScriptText(@"Construction\ConstructionDirector.Projects.cs");
        Check(market.Contains("Math.Max(1, t.WinningDays) + SiteRules.DeliveryDays(p)") && projects.Contains("TenderRules.StandardDays(p.Hours) + 2 + SiteRules.DeliveryDays(p)"),
            "biz24: both places that set a job's due day add the materials' wait");
        Check(market.Contains("floors > FloorsLicensed") && market.Contains("Licensed && TenderFloors(t) <= FloorsLicensed"),
            "biz24: a bid above the licence is refused with the floors named, and a stored one is no bid when the tender is decided");
        Check(ScriptText(@"Construction\ConstructionDirector.Fit.cs").Contains("FloorsWords(key, p.Storeys)") && ScriptText(@"Construction\ConstructionApp.Tenders.cs").Contains("FloorsWords(")
              && ScriptText(@"Construction\Live\ConstructionLive.Tenders.cs").Contains("FloorsWords(") && ScriptText(@"Construction\Live\ConstructionLive.Redevelop.cs").Contains("BlockWords(p, block)")
              && ScriptText(@"Construction\ConstructionApp.Redevelop.cs").Contains("BlockWords(p, block)"),
            "biz24: the floors the job has and what the licence for them asks are written on the job's page and the start's refusal, on both interfaces");
        string en = File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        foreach (string key in new[] { "reallife_ui_cons_answer_floors", "reallife_bld_answer_tender_floors" })
        {
            int at = en.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            string line = at >= 0 ? en.Substring(at, en.IndexOf('\n', at) - at) : string.Empty;
            Check(line.Contains("{floors}") && line.Contains("{have}") && line.Contains("{builds}") && line.Contains("{done}") && line.Contains("{standing}") && line.Contains("{rep}"),
                "biz24: " + key + " names the floors, the licence's, what it asks and what the company has");
        }
    }

    private static void FixBiz24Managers()
    {
        Check(ManagerCatalogue.ManagersMost >= 256 && PolicyCatalogue.StoresMost >= ManagerCatalogue.ManagersMost,
            "biz24: a company may have 256 managers, and a store's policy for each (" + ManagerCatalogue.ManagersMost + ", " + PolicyCatalogue.StoresMost + ")");
        var state = new ManagerState();
        for (int i = 0; i < 200; i++)
        {
            state.Managers.Add(new ManagerFile { Place = "P" + i, Role = ManagerCatalogue.RolePlace, Id = "e" + i, Name = "N" + i });
        }
        string written = ManagerStore.Write(state);
        Check(Occurs(written, "|P") == 200, "biz24: 200 managers are all written to the save (" + Occurs(written, "|P") + ")");
    }
}
