using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// The kit's lists that ran past their room (a player could not reach the rest of the licences, the businesses or what waits), the amount typed into a delivery order, the lowest market
// price on the pricing page and the hire button of an applicant whose offer is already made. The Company face is laid out here, the BizMan and Open Roles screens are read from source.
internal static partial class Program
{
    private static readonly Regex AndMore = new Regex(@"^and \d+ more$", RegexOptions.Compiled);

    private static void KitListsAll()
    {
        int before = _run, failedBefore = _failed;
        KitListsTax();
        KitListsOverview();
        KitListsSources();
        KitListsHire();
        Console.WriteLine("  the kit's lists, a delivery's typed amount, the lowest price and the hire button: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static bool KitHasText(UiScene s, string text)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Text == text)
            {
                return true;
            }
        }
        return false;
    }

    private static int KitMoreLines(UiScene s)
    {
        int n = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            n += s.Items[i].Text != null && AndMore.IsMatch(s.Items[i].Text) ? 1 : 0;
        }
        return n;
    }

    private static bool KitHasMoreLine(UiScene s)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Text != null && AndMore.IsMatch(s.Items[i].Text))
            {
                return true;
            }
        }
        return false;
    }

    // The region a slot of the layout holds, or -1.
    private static int KitClip(CompanyLayout lay, int slot) => lay.Clip[slot];

    private static void KitListsTax()
    {
        UiWords en = UiWords("en");
        CompanyFacts f = UiSamples.CompanyFactsOf(en, "tax_big");
        CompanyView view = UiSamples.CompanyViewOf("tax_big");
        CompanyWords w = UiSamples.CompanyWordsOf(en);
        int own = f.Tax.Licences.Length;
        Check(own > 0, "kit lists: the tax sample holds licences to copy");
        var many = new CompanyLicence[23];
        for (int i = 0; i < many.Length; i++)
        {
            many[i] = f.Tax.Licences[i % Math.Max(1, own)];
            many[i].Id = "kit:" + i;
            many[i].Name = "Licence number " + i;
        }
        f.Tax.Licences = many;
        f.Tax.LicencesCount = "23";
        var scene = new UiScene();
        var lay = new CompanyLayout();
        CompanyFace.Lay(scene, f, view, w, lay, UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        int clip = KitClip(lay, CompanyLayout.SlotLicences);
        Check(clip >= 0 && scene.Clips[clip].ContentHeight > scene.Clips[clip].H + 0.5f && scene.Lists[lay.List[CompanyLayout.SlotLicences]].Count == 23,
            "kit lists: 23 licences are one scrolling list of 23 rows, not a cut list");
        Check(!KitHasMoreLine(scene), "kit lists: the licences' list does not end in a line that says how many it leaves out");
        Check(clip >= 0 && lay.SlotOfClip(clip) == CompanyLayout.SlotLicences, "kit lists: the layout maps the licences' region to its slot, so the wheel and the screen's drag move it");
        UiClip c = scene.Clips[Math.Max(0, clip)];
        Check(clip >= 0 && UiPanRules.ClipAt(scene, c.X + 8f, c.Y + 8f, -4f) == clip, "kit lists: a mouse drag up on the licences takes their region (UiPanRules), which a page with no region never gave");
        Check(!KitHasText(scene, "Licence number 22"), "kit lists: at the top the last licence is not in the scene (only the rows in view are laid out)");
        lay.Offset[CompanyLayout.SlotLicences] = 1000000f;
        CompanyFace.Lay(scene, f, view, w, lay, UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        Check(KitHasText(scene, "Licence number 22") && !KitHasText(scene, "Licence number 0"), "kit lists: scrolled to the end the last licence is there and the first is not");
        // The pad: the node of the last row names its list and index, so that going down past the rows in view brings the rest into view.
        bool listed = false;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            listed |= scene.Nodes[i].List == lay.List[CompanyLayout.SlotLicences] && scene.Nodes[i].Index == 22;
        }
        Check(listed, "kit lists: a licence's row is a row of its list, so the pad reaches rows that are out of view");
    }

    private static void KitListsOverview()
    {
        UiWords en = UiWords("en");
        CompanyFacts f = UiSamples.CompanyFactsOf(en, "overview_big");
        CompanyView view = UiSamples.CompanyViewOf("overview_big");
        CompanyWords w = UiSamples.CompanyWordsOf(en);
        CompanyOverviewFacts o = f.Overview;
        int own = o.Businesses.Length, waits = o.Waiting.Length;
        Check(own > 0, "kit lists: the overview sample holds businesses to copy");
        var shops = new CompanyBusiness[23];
        for (int i = 0; i < shops.Length; i++)
        {
            shops[i] = o.Businesses[i % Math.Max(1, own)];
            shops[i].Name = "Shop number " + i;
        }
        o.Businesses = shops;
        if (waits > 0)
        {
            var wait = new CompanyWaiting[12];
            for (int i = 0; i < wait.Length; i++)
            {
                wait[i] = o.Waiting[i % waits];
                wait[i].Name = "Waiting number " + i;
            }
            o.Waiting = wait;
        }
        var scene = new UiScene();
        var lay = new CompanyLayout();
        CompanyFace.Lay(scene, f, view, w, lay, UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        int clip = KitClip(lay, CompanyLayout.SlotBusinesses);
        Check(clip >= 0 && scene.Clips[clip].ContentHeight > scene.Clips[clip].H + 0.5f && scene.Lists[lay.List[CompanyLayout.SlotBusinesses]].Count == 23,
            "kit lists: 23 businesses are one scrolling table of 23 rows, which the head's badges (they run out at the width) are not");
        Check(KitMoreLines(scene) <= 1, "kit lists: the day table does not end in a line that says how many businesses it leaves out (the one line that may remain is the managers', beside its 'All' link)");
        lay.Offset[CompanyLayout.SlotBusinesses] = 1000000f;
        CompanyFace.Lay(scene, f, view, w, lay, UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        Check(KitHasText(scene, "Shop number 22"), "kit lists: scrolled to the end of the table the last business is there");
        if (waits > 0 && o.Lead == null || o.Waiting.Length >= 12)
        {
            int wc = KitClip(lay, CompanyLayout.SlotWaiting);
            Check(wc < 0 || scene.Lists[lay.List[CompanyLayout.SlotWaiting]].Count == 12, "kit lists: what waits for the owner is one scrolling list holding every row, when the column has no room for them all");
        }
    }

    private static void KitListsSources()
    {
        string tax = ModSource(@"Ui\Company\CompanyFace.Tax.cs"), rota = ModSource(@"Ui\Company\CompanyFace.Rota.cs"), overview = ModSource(@"Ui\Company\CompanyFace.Overview.cs");
        Check(!tax.Contains("reallife_co_and_more") && !rota.Contains("reallife_co_and_more") && tax.Contains("BeginRows(s, at, lay, CompanyLayout.SlotLicences,")
              && tax.Contains("BeginRows(s, at, lay, CompanyLayout.SlotPage,") && rota.Contains("BeginRows(s, at, lay, CompanyLayout.SlotGrid,") && rota.Contains("CompanyLayout.SlotLog,"),
            "kit lists: the licences, the tools, the rota's people and its folk scroll through BeginRows, and none of those pages says how many it leaves out");
        Check(overview.Split(new[] { "reallife_co_and_more" }, StringSplitOptions.None).Length == 2 && overview.Contains("CompanyLayout.SlotBusinesses") && overview.Contains("CompanyLayout.SlotWaiting"),
            "kit lists: the overview's businesses and waiting rows scroll; its one count that remains is the managers' (the 'All' link beside it opens the rest)");
        string pages = ModSource(@"Ui\Company\CompanyFace.Pages.cs");
        Check(MemberText(pages, "private static float ItemsBlock(").Contains("FreeSlot(lay)") && !MemberText(pages, "private static float ItemsBlock(").Contains("heights[i] + ListGap > bottom"),
            "kit lists: a card's rows past its room scroll in a region the page has not given away (a page with none left still says how many)");

        // A delivery's amount typed in BizMan is taken by the game's cell when its field is left, so each figure typed is that end.
        string screen = ModSource(@"Ui\BizMan\BizManScreen.cs");
        string typed = MemberText(screen, "public void Typed(string text)");
        Check(typed.Contains("_typeCommand == BizManPages.Goods + UiDataTable.Type && _shown == BizManRules.Deliveries") && typed.Contains("f.onEndEdit?.Invoke(f.text);")
              && typed.Contains("int.TryParse(text,"), "bizman: a figure typed into a delivery's amount ends the game's field edit, as the game's own cell takes it (it listens to the end, not the change)");

        // The lowest market price on the pricing page is the game's, read again when the table is laid out, and part of what makes it be laid out again.
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs"), page = ModSource(@"Ui\BizMan\BizManScreen.Page.cs");
        string good = MemberText(parts, "private UiDataRow Good(");
        Check(good.Contains("Lowest(g, district)") && !good.Contains("g.MarketPrice") && MemberText(parts, "private static float Lowest(").Contains("ItemHelper.GetLowestMarketPrice(g.Item.itemName, district)"),
            "bizman: the pricing page's lowest price and the mark on a dear price read the game's lowest price, not the figure the model kept when the table was built");
        Check(MemberText(page, "private long PageSignature(").Contains("Lowest(g, district).GetHashCode()"), "bizman: the lowest price is in what makes the pricing page be laid out again (read at the screen's own look, four a second at most, never per frame)");
    }

    private static void KitListsHire()
    {
        var a = new Applicant();
        int[] stages = { (int)ApplyStage.New, (int)ApplyStage.Shortlisted, (int)ApplyStage.Interviewed, (int)ApplyStage.Offered, (int)ApplyStage.Countered, (int)ApplyStage.Accepted,
            (int)ApplyStage.Started, (int)ApplyStage.Declined, (int)ApplyStage.Rejected, (int)ApplyStage.Withdrawn, (int)ApplyStage.Lapsed };
        int[] want = { HireOffers.CanOffer, HireOffers.CanOffer, HireOffers.CanOffer, HireOffers.OfferWaiting, HireOffers.OfferCountered, HireOffers.OfferTaken, HireOffers.OfferTaken,
            HireOffers.OfferEnded, HireOffers.OfferEnded, HireOffers.OfferEnded, HireOffers.OfferEnded };
        bool right = true;
        for (int i = 0; i < stages.Length; i++)
        {
            a.Stage = stages[i];
            right &= HireOffers.OfferState(a) == want[i];
        }
        Check(right, "hire: an offer can be made only before one is made; made, countered, taken and over are each told apart");
        string people = ModSource(@"Ui\Work\RolesLive.People.cs"), director = ModSource(@"Tech\TechDirector.Hiring.cs");
        Check(people.Split(new[] { "OfferWhy(a, w, dollars)" }, StringSplitOptions.None).Length == 3 && !people.Contains("done ? w.T(\"reallife_work_already_hired\""),
            "hire: both hire buttons of the kit (the list's detail and the person's contract) are held with a reason in every state the director refuses an offer in");
        Check(people.Contains("return d.MakeOffer(act.Arg,") && MemberText(director, "internal bool MakeOffer(").Contains("return false;") && MemberText(director, "internal bool MakeOffer(").Contains("reallife_hire_note_offered"),
            "hire: a press the director refuses is not taken as done, and an offer made says so in a notice");
    }
}
