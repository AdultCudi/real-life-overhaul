using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The fixes of the buying, items and overlays pass: every button of a card is reachable (a page of them when a fridge holds many kinds), the
// steering assist is on the vehicle panel, the surgeon is the game's own screen, a card keeps its security line and says how many requirements
// are left, a person's panel lists every skill and demand, the game's fields that are switched off are not read, a piece the card cannot say
// hands the box back, and a rebuilt button is held hidden at once.
internal static partial class Program
{
    private static void ItemsFixChecks()
    {
        ItemsActionPages();
        ItemsSources();
    }

    private static void ItemsActionPages()
    {
        const int fit = UiOverlayModel.MostActions;
        Check(OverlayRules.ActionPages(0, fit) == 1 && OverlayRules.ActionPages(fit, fit) == 1 && OverlayRules.ActionPages(fit + 1, fit) == 2 && OverlayRules.ActionPages(22, fit) == 4,
            "items: a card's buttons are one page while eight fit, and pages of seven with the pager's row beyond that");
        var reached = new HashSet<int>();
        for (int page = 0; page < OverlayRules.ActionPages(22, fit); page++)
        {
            OverlayRules.ActionWindow(22, page, fit, out int first, out int end);
            Check(end - first <= fit - 1 && end > first, "items: a page of a card's buttons leaves the pager its row: page " + page + " holds " + (end - first));
            for (int i = first; i < end; i++)
            {
                reached.Add(i);
            }
        }
        Check(reached.Count == 22, "items: every one of 22 buttons of a fridge is on some page: " + reached.Count);
        OverlayRules.ActionWindow(22, 99, fit, out int lastFirst, out int lastEnd);
        Check(lastEnd == 22 && lastFirst >= 0, "items: a page beyond the last shows the last");

        // A fridge with a take button for every kind and the game's and the mod's own buttons after them.
        var m = new UiOverlayModel { Detail = true, Title = "Fridge" };
        for (int i = 0; i < 22; i++)
        {
            m.AddAction(new UiOverlayAction { Label = i < 18 ? "Take " + i : i == 18 ? "Empty" : i == 19 ? "Add item" : i == 20 ? "Cook" : "Family" });
        }
        Check(m.ActionCount == 22 && m.Actions.Length >= 22, "items: the card's list of buttons grows with the box: a button past the eighth is no longer dropped");
        var seen = new HashSet<int>();
        bool pagerEveryPage = true;
        for (int page = 0; page < OverlayRules.ActionPages(m.ActionCount, fit); page++)
        {
            m.ActionPage = page;
            var s = new UiScene();
            s.Begin("overlay", UiOverlayCard.Width(m), UiOverlayCard.Height(m), true);
            UiOverlayCard.Lay(s, UiAt.Screen, m, 0f, 0f);
            int buttons = 0, pager = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                UiNode n = s.Nodes[i];
                if (n.Command == UiOverlayCard.ActionCommand)
                {
                    buttons++;
                    seen.Add(n.Arg);
                    Check(OverlayRules.Route(m, n.Command, n.Arg, -1) == OverlayAct.Action, "items: a button of page " + page + " is routed to its game button");
                }
                else if (n.Command == UiOverlayCard.ActionPageCommand)
                {
                    pager++;
                    Check(OverlayRules.Route(m, n.Command, n.Arg, -1) == OverlayAct.ActionPage, "items: the pager's buttons are routed");
                }
            }
            pagerEveryPage &= pager == 2;
            Check(buttons <= fit - 1 && UiOverlayCard.Height(m) < 640f, "items: a page of the card is no taller than the screen allows: " + UiOverlayCard.Height(m));
        }
        Check(seen.Count == 22, "items: pressing through the pages reaches every button of the fridge, Empty, Add item, Cook and Family among them: " + seen.Count);
        Check(pagerEveryPage, "items: every page of a long card has its back and next buttons");
        int digestFirst = m.Digest();
        m.ActionPage = 1;
        Check(digestFirst != m.Digest(), "items: turning the card's button page changes what is laid out");
        var few = new UiOverlayModel { Detail = true, Title = "Cart" };
        for (int i = 0; i < fit; i++)
        {
            few.AddAction(new UiOverlayAction { Label = "B" + i });
        }
        var sf = new UiScene();
        sf.Begin("overlay", UiOverlayCard.Width(few), UiOverlayCard.Height(few), true);
        UiOverlayCard.Lay(sf, UiAt.Screen, few, 0f, 0f);
        Check(Enumerable.Range(0, sf.NodeCount).All(i => sf.Nodes[i].Command == UiOverlayCard.ActionCommand) && sf.NodeCount == fit,
            "items: a card of eight buttons shows them all with no pager");
        Check(OverlayRules.Route(few, UiOverlayCard.ActionPageCommand, 1, -1) == OverlayAct.None, "items: no pager is answered while the buttons fit");
        Check(UiOverlayCard.ActionPageCommand >= 9620 && UiOverlayCard.ActionPageCommand <= 9659, "items: the pager's command is in the card's own block of numbers");

        // The rows and the requirement lines have the room the game's lists need, and the sweep also fires when a button was destroyed.
        Check(UiOverlayModel.MostRows >= 8 && UiOverlayModel.MostChecks >= 12, "items: a hover card holds the stock, price, margin, delivery, customers, security and queue rows together");
        Check(OverlayRules.SweepDue(7, 7, false, true) && !OverlayRules.SweepDue(7, 7, false, false) && OverlayRules.SweepDue(7, 8, false, false),
            "items: the box is looked through again when a button seen last time is gone, though the count of its transforms is the same");
    }

    private static void ItemsSources()
    {
        string folder = System.IO.Path.Combine(ModScripts(), "Ui", "Overlays");
        string hover = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Hover.cs"));
        string click = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Click.cs"));
        string person = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Person.cs"));
        string hide = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Hide.cs"));
        string cards = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.cs"));
        string stock = OverlayBetween(hover, "// A vehicle's hold, or anything else", "// A person at a station");
        Check(stock.Contains("Shown(Text(GameContract.OverlayStockAmount, _stock))") && stock.Contains("Shown(Text(GameContract.OverlayStockContent, _stock))"),
            "items: the stock's fallback reads its amount and contents only while the game has those fields switched on, never the words of the last shelf");
        Check(hover.Contains("private static TMP_Text Shown(TMP_Text t) => t != null && t.gameObject.activeInHierarchy ? t : null;"), "items: a label is read only while it is on");
        string reqs = OverlayBetween(hover, "private void Requirements(", "// A recipe machine");
        Check(reqs.Contains("reallife_ui_overlay_more") && reqs.Contains("total > UiOverlayModel.MostChecks") && !reqs.Contains("m.CheckCount < UiOverlayModel.MostChecks"),
            "items: a requirement list longer than the card holds ends in a line that says how many are left");
        Check(person.Contains("skills[i].GetRoundedValue()") && !person.Contains("TopSkill(") && !person.Contains("Math.Min(4, who.demands.Count)") && person.Contains("fulfilled"),
            "items: a person's panel lists every skill with its percent and every demand, with how many are met of how many");
        Check(click.Contains("private void Put(UiOverlayModel m") && !click.Contains("m.ActionCount < UiOverlayModel.MostActions") && !click.Contains("_buttons[m.ActionCount] ="),
            "items: every button of a box is added through the growing list, none stops at a count");
        Check(cards.Contains("case OverlayAct.ActionPage:") && cards.Contains("_actionPage = 0;"), "items: the button page turns on a press and starts again for another thing");
        Check(hover.Contains("Unknown(_form == Form.Hover ? _hoverParts : _parts)") && hover.Contains("Skip(entity);") && cards.Contains("want == _skipForm") && cards.Contains("_skipId = 0;"),
            "items: a box with a piece of another mod's the card cannot say, or one whose read threw, is left to the game until the pointer is off every box, and is not read again each frame");
        Check(hover.Contains("t != typeof(IOverlay)") && hover.Contains("OverlayKinds.All"), "items: a piece is known by the table of kinds, whatever its base");
        Check(hide.Contains("last == null &&") && hide.Contains("OverlayRules.SweepDue(") && hide.Contains("_simpleLast") && hide.Contains("_detailLast"),
            "items: the box's last button is remembered, and the box is looked through again at once when the game destroyed it");

        string panel = ModSource(@"Ui\Buy\ItemPanelKit.cs"), buyKits = ModSource(@"Ui\Buy\BuyKits.cs"), wardrobe = ModSource(@"Ui\Buy\WardrobeScreen.cs") + ModSource(@"Ui\Buy\WardrobeGame.cs");
        string steer = MemberText(panel, "private void Steering()");
        Check(steer.Contains("GameContract.InfoSteering") && steer.Contains("ToggleSteeringAssist") && steer.Contains("_buttons.Add(_steer)") && steer.Contains("PlayerPrefSettings.SteeringAssist")
              && panel.Contains("Steering();"), "items: the vehicle panel offers the steering assist, pressed through the game's own button, saying whether it is on");
        Check(!buyKits.Contains("WardrobeKind.Surgery") && !wardrobe.Contains("Surgery") && buyKits.Contains("the surgeon (the game's own)"),
            "items: the surgeon is not hosted by the kit, so the game's own screen with its face sliders, colours and zoom stays whole");
        string cargo = ModSource(@"Ui\Buy\CargoScreen.cs");
        Check(cargo.Contains("nestedCargoInstances") && cargo.Contains("reallife_ui_buy_contains") && cargo.Contains("_detailNested"), "items: manage cargo lists what a bundle holds, kind by kind, as the game's tooltip does");
        string overlayKeys = ModSource(@"Ui\UiScreens.Overlays.cs"), buyKeys = ModSource(@"Ui\UiScreens.Buy.cs");
        Check(overlayKeys.Contains("reallife_ui_overlay_more") && buyKeys.Contains("reallife_ui_buy_steering_on") && buyKeys.Contains("reallife_ui_buy_steering_off") && buyKeys.Contains("reallife_ui_buy_contains"),
            "items: each new text is listed with its screen's keys");
    }
}
