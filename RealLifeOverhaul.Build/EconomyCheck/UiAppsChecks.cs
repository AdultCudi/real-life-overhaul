using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The money, work and life apps on the two interfaces (run with --ventures-ui and --work-ui): what the kit's Web shop, Campus, Career, Open Roles, Persona and money apps carry over from the
// old pages, and what the old Web shop app carries over from the kit's. Pages are checked on their sample facts; the live readers, which need the game, by what their source calls.
internal static partial class Program
{
    private static bool _uiAppsChecked;

    private static void UiAppsParity()
    {
        if (_uiAppsChecked)
        {
            return;
        }
        _uiAppsChecked = true;
        ShopOpenPage();
        ShopOldAppParity();
        WorkAppsParity();
        MoneyAppsParity();
    }

    private static UiActionModel? FindShopAct(VentModel m, string label)
    {
        foreach (VentBlock b in m.Left.Concat(m.Right))
        {
            if (b.Kind == VentBlockKind.Actions)
            {
                foreach (UiActionModel a in b.Actions)
                {
                    if (a.Label == label)
                    {
                        return a;
                    }
                }
            }
        }
        foreach (UiActionModel a in m.HeadActions)
        {
            if (a.Label == label)
            {
                return a;
            }
        }
        return null;
    }

    private static VentBlock FindShopBlock(VentModel m, VentBlockKind kind, string label) => m.Left.Concat(m.Right).FirstOrDefault(b => b.Kind == kind && b.Text == label);

    // A player with no web site can open one for a shop or warehouse of theirs: the page, its quality, way and promise choices, what it costs, and a press that reaches the director.
    private static void ShopOpenPage()
    {
        var stub = new UiSamples.ShopStub();
        var view = new VentView();
        VentModel open = UiSamples.Shop((k, e) => e, "open", view, stub);
        UiActionModel? button = FindShopAct(open, "Open an online shop");
        Check(open.Empty == null && button != null && !button.Value.Disabled && FindShopBlock(open, VentBlockKind.Choice, "Quality") != null && FindShopBlock(open, VentBlockKind.Choice, "Delivered by") != null
              && FindShopBlock(open, VentBlockKind.Choice, "Delivery promise") != null, "web shop: with no site yet and a place to run one from, the page opens one: quality, way and promise, and the one button");
        open.Runs[button.Value.Arg]();
        Check(stub.Last == "open_site" && stub.Count == 1, "web shop: the open button reaches the director's OpenShop through the question box");
        VentBlock quality = FindShopBlock(open, VentBlockKind.Choice, "Quality");
        int was = view.A;
        open.Runs[quality.Run]();
        Check(view.A != was, "web shop: the quality choice steps the view and costs nothing until the question is answered");
        VentModel again = UiSamples.Shop((k, e) => e, "open", view, stub);
        Check(again.HeroFigures.Length == 3 && again.HeroFigures[0].Value != open.HeroFigures[0].Value, "web shop: the set-up, hosting and orders follow the quality chosen");
        // with sites already: the head offers it, and the page steps back
        VentView more = new VentView();
        VentModel hub = UiSamples.Shop((k, e) => e, "open_more", more, stub);
        Check(more.Pick == ShopPages.OpenKey && hub.Sub && hub.HeadActions.Any(a => a.Arg == VentFace.BackArg), "web shop: a player with sites opens another from a page that can step back");
        VentModel hubOnly = UiSamples.Shop((k, e) => e, "hub", new VentView(), stub);
        UiActionModel? better = FindShopAct(hubOnly, "Build a better site");
        Check(better != null && FindShopBlock(hubOnly, VentBlockKind.Choice, "Delivered by") != null && FindShopBlock(hubOnly, VentBlockKind.Choice, "Delivery promise") != null,
            "web shop: a site's page carries the delivery way, the promise and the better site");
        hubOnly.Runs[better.Value.Arg]();
        VentBlock way = FindShopBlock(hubOnly, VentBlockKind.Choice, "Delivered by");
        hubOnly.Runs[way.Run]();
        Check(stub.Last == "way", "web shop: the delivery way is set through SetWay");
        VentModel none = UiSamples.Shop((k, e) => e, "empty", new VentView(), stub);
        Check(none.Empty != null && none.Empty.IndexOf("Open one", StringComparison.Ordinal) < 0, "web shop: with no place to run a site from the page does not promise a button it has not got");
        string live = ModSource("Ui/Ventures/ShopLive.cs");
        string open2 = MemberText(live, "public void OpenSite(");
        Check(open2.Contains("d.OpenShop(place, quality, way, promise, false)") && open2.Contains("GameConfirm.Show") && MemberText(live, "public void Better(").Contains("d.Upgrade(place, next)")
              && live.Contains("SetWay(place, way)") && live.Contains("SetPromise(place, promise)") && live.Contains("d.Candidates(regs)"), "web shop: the kit's open, better, way and promise call the director's own methods, asking as the old page asked");
    }

    // The old Web shop app carries every control the kit's has: fee, free over, packing, night orders, matching, stock, areas, refunds, couriers, titles and the shop as a customer sees it.
    private static void ShopOldAppParity()
    {
        string app = ModSource("Economy/Online/OnlineApp.Extra.cs");
        string[] calls = { "SetFee(key", "SetFreeOver(key", "SetPacking(key", "Toggle(key, 0)", "Toggle(key, 1)", "Toggle(key, 2)", "ToggleDistrict(key", "RefundOrder(key", "SendCourier(key", "SetTitle(key", "SetDescription(key",
            "_director.Reply(" };
        string missing = string.Join(", ", calls.Where(c => !app.Contains(c)));
        Check(missing.Length == 0, "web shop (old app): every control of the kit's is on the old pages too (" + (missing.Length == 0 ? calls.Length + " calls" : "missing " + missing) + ")");
        string shops = ModSource("Economy/Online/OnlineApp.Shops.cs"), lists = ModSource("Economy/Online/OnlineApp.Lists.cs");
        Check(shops.Contains("case PageStore: WriteStore(box, picked)") && shops.Contains("WriteExtras(box, place, state)") && lists.Contains("WriteOrder(box, orderPlace, orderPicked, state, day)") && lists.Contains("WriteTitles(box, place, item)"),
            "web shop (old app): the settings page, a picked order, a product and the customer's page are written by the new parts");
        Check(System.Text.RegularExpressions.Regex.IsMatch(lists, @"TableRow row = Row\(OrderRow \+ order\.Id\);\s+row\.Kind = overdue"), "web shop (old app): an open order can be picked, for its refund and its courier");
        string facts = ModSource("Ui/Ventures/ShopLive.cs");
        Check(facts.Contains("public static ShopSiteFact Site(string place)") && app.Contains("ShopLive.Site(shop.Place)"), "web shop (old app): the customer's page reads the same facts the kit's draws");
    }

    private static void WorkAppsParity()
    {
        string campus = ModSource("Ui/Work/CampusLive.cs");
        Check(campus.Contains("d.CanAttend(e, out int _)") && campus.Contains("d.CanHomework(e)") && campus.Contains("d.CanSit(e)") && campus.Contains("case Attend: why = d.Attend(act.Arg)")
              && campus.Contains("case Homework: why = d.Homework(act.Arg)") && campus.Contains("case Sit: why = d.Sit(act.Arg)") && campus.Contains("d.UniversityLine()"),
            "campus: a course on the cards can be attended, its homework done and its exam sat, each dimmed with the director's reason");
        WorkScreen studying = LaidWork("campus", "studying");
        UiActionModel[] acts = studying.Page.Model.Actions;
        Check(acts.Any(a => a.Label == "Attend the class") && acts.Any(a => a.Label == "Do homework") && acts.Any(a => a.Label == "Sit the exam"),
            "campus: the studying page draws the class, homework and exam buttons on its cards");
        string career = ModSource("Ui/Work/CareerLive.Manage.cs") + ModSource("Ui/Work/CareerLive.cs") + ModSource("Ui/Work/CareerPages.cs");
        Check(career.Contains("case CareerAct.StartWork: return Said(d.StartWorkDay(), d)") && career.Contains("case CareerAct.WorkLate: return Said(d.WorkLate(), d)") && career.Contains("d.DeskFallbackWhy(j)")
              && career.Contains("d.CanWorkLate()") && career.Contains("cj.DeskLine = d.DeskLine(j)"), "career: My job can start the working day and work late where no desk could be had, and says where the desk is");
        WorkScreen job = LaidWork("career", "my_job_desk");
        UiActionModel[] jobActs = job.Page.Model.Actions;
        Check(jobActs.Any(a => a.Label == "Start the working day" && !a.Disabled) && jobActs.Any(a => a.Label == "Work late" && a.Disabled) && job.Page.Why != null && job.Page.Why.Contains("Work today"),
            "career: with no desk the page has Start the working day, and Work late dimmed with its reason said");
        WorkScreen deskful = LaidWork("career", "my_job");
        Check(!deskful.Page.Model.Actions.Any(a => a.Label == "Start the working day"), "career: with a desk there is no way to begin the day from the page");
        string manage = ModSource("Ui/Work/CareerLive.Manage.cs");
        Check(manage.Contains("PageOf(t, decide, view, TaskPageSlot") && manage.Contains("PageOf(t, staff, view, StaffPageSlot") && manage.Contains("PageOf(t, shops, view, StorePageSlot")
              && manage.Contains("CareerLadderRules.InUnit(f, c.People[i].Store)") && manage.Contains("case CareerAct.Move: return Said(d.LadderMove(arg >> 4, arg & 15), d)")
              && manage.Contains("case CareerAct.AimSeat: return Said(d.LadderAimSeat((ExecSeat)arg), d)") && manage.Contains("d.LadderWhyNot(f)"),
            "career: Management pages every task, person of the unit and store instead of cutting them, and can move a person and choose the executive seat");
        Check(!manage.Contains("i < TasksShown;") && !manage.Contains("people < StaffShown") && !manage.Contains("i < c.Stores.Count && i < 4"), "career: no list of the Management page is cut without a way to the rest");
        string live = ModSource("Ui/Work/CareerLive.cs"), boards = ModSource("Ui/Work/CareerLive.Boards.cs");
        Check(live.Contains("director.InterviewPlace(a)") && live.Contains("director.CanInterview(a, out int _)") && live.Contains("case CareerAct.Register: return Said(d.Register(), d)")
              && live.Contains("and {n} more") && boards.Contains("d.ShowBoard(") && boards.Contains("CareersBridges.Boards") && live.Contains("d.FirstStepLine(prof, course)"),
            "career: the interview says where and why not, Register is a button, hiring lists are never cut short, the boards show on the map and the first step is said");
        string roles = ModSource("Ui/Work/RolesLive.People.cs");
        Check(roles.Contains("case SetRole: d.SetRole(key, act.Arg)") && roles.Contains("CrewChoice(d, view, out DataCentre centre") && roles.Contains("d.StartCrewSearch(centre.Place, role, level, count)")
              && roles.Contains("InfraRules.CrewLacking(centre)"), "open roles: a person can be moved to another role, and a crew is briefed for a data centre of the infrastructure with its role, level and count");
        Check(!roles.Contains("_places[Math.Max(0, Math.Min(_places.Count - 1, v.Get(2, 0)))]), TalentCatalogue.Technician"), "open roles: the crew search no longer asks a business where a data centre is meant");
    }

    private static void MoneyAppsParity()
    {
        foreach (string live in new[] { "Ui/Money/FinanceLive.cs", "Ui/Money/BanksLive.cs", "Ui/Money/OwnBankLive.cs" })
        {
            Check(ModSource(live).Contains("MoneyStamp.Now"), "money apps: " + live.Substring(live.LastIndexOf('/') + 1) + " is written again when the cash moves, not only each hour");
        }
        string stamp = ModSource("Ui/Money/MoneyStamp.cs");
        Check(stamp.Contains("Every = 0.25f") && stamp.Contains("Time.unscaledTime") && stamp.Contains("save.Loans"), "money apps: the cash is looked at four times a second, never every frame");
        string persona = ModSource("Ui/Persona/PersonaScreen.cs"), views = ModSource("Ui/PersonaViews.cs");
        Check(persona.Contains("g.GetTier(t)") && persona.Contains("tier.GetTitle()") && views.Contains("TierTitles") && views.Contains("TierSubs(columns.Count, g.TierTitles)"), "persona: each goal's tiers say what they ask, as the game's tooltip on a tier does");
        string brands = ModSource("Ui/Ventures/BrandsLive.Facts.cs") + ModSource("Ui/Ventures/BraPages.cs");
        Check(brands.Contains("d.KeptFactor(shop.Place)") && brands.Contains("d.ManagerWatches(shop)") && brands.Contains("s.KeptNote") && brands.Contains("s.ManagerNote"), "brands: a kept price says the band it is kept at and that a manager of the game's watches the district");
    }
}
