using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul.Ui;

// A player's choice of apps (14 Nov 2026, the suggestion "Favourite apps"): which apps stand on the RLO interface's bar and in
// what order, and which are favourites in All apps. The rules are game-free (UiAppDeck); the screen, the bar and the stored
// line are read from the sources and laid out at the standard frames by the kit's own screen checks.
internal static partial class Program
{
    private static void AppDeckChecks()
    {
        List<UiShellApp> apps = UiSamples.ShellApps((key, english) => english, true);
        var row = new List<int>();
        string[] home = apps.Where(a => a.InRow).Select(a => a.Id).ToArray();

        // The default: a player who never touches it sees today's row exactly.
        var deck = new UiAppDeck();
        deck.Apply(apps, row);
        Check(!deck.Custom && row.Select(i => apps[i].Id).SequenceEqual(home) && apps.Where(a => a.InRow).Select(a => a.Id).SequenceEqual(home) && home.Length == PagerRulesTiles,
            "app deck: with nothing chosen the row is the phone's first page exactly as before (" + string.Join(", ", home) + ")");

        // Add, remove, move, reset.
        Check(deck.TakeOffBar("Persona", apps) && deck.Custom && !deck.InBar("Persona") && deck.BarCount == home.Length - 1, "app deck: taking an app off starts from the row the player sees, minus it");
        deck.Apply(apps, row);
        Check(row.Count == home.Length - 1 && !apps[apps.FindIndex(a => a.Id == "Persona")].InRow, "app deck: the row shows one app fewer and the app's flag is off");
        Check(deck.PutOnBar("Persona", apps) && deck.BarAt(deck.BarCount - 1) == "Persona" && !deck.PutOnBar("Persona", apps), "app deck: an app put on the bar stands last, and not twice");
        string first = deck.BarAt(0), second = deck.BarAt(1);
        Check(deck.MoveBar(first, 1, apps) && deck.BarAt(0) == second && deck.BarAt(1) == first, "app deck: an app moved right swaps with the next");
        Check(!deck.MoveBar(second, -1, apps) && deck.MoveBar(first, -1, apps) && deck.BarAt(0) == first, "app deck: the first app cannot move left, and a move left undoes a move right");
        Check(deck.MoveBarTo("Persona", 0, apps) && deck.BarAt(0) == "Persona" && deck.MoveBarTo("Persona", 3, apps) && deck.BarAt(3) == "Persona" && !deck.MoveBarTo("Persona", 3, apps),
            "app deck: a drag's drop puts an app in the place asked, and not moving is not a change");
        Check(deck.ResetBar() && !deck.Custom && !deck.ResetBar(), "app deck: reset puts the default row back");
        deck.Apply(apps, row);
        Check(row.Select(i => apps[i].Id).SequenceEqual(home), "app deck: after a reset the row is today's again");

        // The row's own cap and the strip's room: sixteen chosen, as many as the room allows, never fewer than four.
        var long16 = new UiAppDeck();
        int put = 0;
        foreach (UiShellApp a in apps.Where(a => !a.InRow))
        {
            put += long16.PutOnBar(a.Id, apps) ? 1 : 0;
        }
        long16.Apply(apps, row);
        Check(row.Count == Math.Min(UiAppDeck.BarMost, home.Length + put) && row.Count == UiAppDeck.BarMost && !long16.PutOnBar(apps.First(a => !long16.InBar(a.Id)).Id, apps),
            "app deck: the bar holds at most " + UiAppDeck.BarMost + " apps and refuses another");
        UiStatusModel model = RowModel(apps, row);
        int wide = UiDraw.AppsShown(model, 1920f), narrow = UiDraw.AppsShown(model, 960f), tiny = UiDraw.AppsShown(model, 640f);
        Check(wide > home.Length && wide <= UiAppDeck.BarMost && narrow < wide && narrow >= 4 && tiny >= 1,
            "app deck: a long bar shows as many apps as the strip's room allows (" + wide + " at 1920, " + narrow + " at 960, " + tiny + " at 640) and the rest wait in All apps");
        UiStatusModel plain = RowModel(apps, apps.Select((a, i) => i).Where(i => apps[i].InRow).ToList());
        int plainShown = UiDraw.AppsShown(plain, 1920f);
        Check(plainShown == home.Length && plain.AppsMost == 0, "app deck: the default row is laid as it was, ten tiles at most (" + plainShown + " of " + home.Length + ")");

        // An app that is not here keeps its place and is not drawn.
        var cut = apps.Where(a => a.Id != "Contacts").ToList();
        UiAppDeck keep = UiAppDeck.Read("1|bar=Persona,Contacts,BizMan,Rivals|fav=Contacts,Persona");
        keep.Apply(cut, row);
        Check(row.Select(i => cut[i].Id).SequenceEqual(new[] { "Persona", "BizMan", "Rivals" }) && keep.BarCount == 4 && keep.FavCount == 2,
            "app deck: an app that is not here is skipped on the row and keeps its place in the deck");
        Check(keep.MoveBar("BizMan", -1, cut) && keep.BarAt(0) == "BizMan" && keep.BarAt(1) == "Persona" && keep.BarAt(2) == "Contacts",
            "app deck: a move steps over an app that is not here (Contacts kept between Persona and Rivals)");
        keep.Apply(apps, row);
        Check(row.Select(i => apps[i].Id).SequenceEqual(new[] { "BizMan", "Persona", "Contacts", "Rivals" }), "app deck: the app comes back to the place it kept");
        Check(!keep.MoveFav("Persona", -1, cut) && keep.MoveFav("Persona", -1, apps) && keep.FavAt(0) == "Persona", "app deck: favourites step over an app that is not here too");

        // The stored line: a round trip, and an older or damaged line is the default.
        var made = new UiAppDeck();
        made.TakeOffBar("Persona", apps);
        made.MoveBar("BizMan", -1, apps);
        made.ToggleFav("RealLifePlanner");
        made.ToggleFav("Contacts");
        string line = made.Write();
        UiAppDeck again = UiAppDeck.Read(line);
        Check(again.Write() == line && again.Custom && again.BarCount == made.BarCount && again.FavCount == 2 && again.FavAt(0) == "RealLifePlanner" && !again.InBar("Persona"),
            "app deck: the stored line reads back as written (" + line + ")");
        var emptied = new UiAppDeck();
        foreach (string id in home)
        {
            emptied.TakeOffBar(id, apps);
        }
        Check(UiAppDeck.Read(emptied.Write()).Custom && UiAppDeck.Read(emptied.Write()).BarCount == 0, "app deck: a bar the player emptied stays empty, not the default");
        foreach (string bad in new[] { null, string.Empty, "0|bar=Persona", "2|bar=Persona|fav=A", "garbage", "1|bar", "1|=x", "1|bar=,,,|fav=,", "|||", "1|bar=Persona,Persona,|" })
        {
            UiAppDeck d = UiAppDeck.Read(bad);
            bool fine = bad != null && bad.StartsWith("1|bar=,,,") ? d.Custom && d.BarCount == 0 : bad == "1|bar=Persona,Persona,|" ? d.Custom && d.BarCount == 1 : !d.Custom && d.FavCount == 0;
            Check(fine, "app deck: a damaged or older stored line reads as the default or as its clean part (" + (bad ?? "null") + ")");
        }
        string bigLine = "1|bar=" + string.Join(",", Enumerable.Range(0, 100).Select(i => "App" + i).ToArray()) + "|fav=" + string.Join(",", Enumerable.Range(0, 200).Select(i => "F" + i).ToArray());
        UiAppDeck big = UiAppDeck.Read(bigLine);
        Check(big.BarCount <= 32 && big.FavCount <= 64 && UiAppDeck.Read(new string('x', 70)).FavCount == 0 && !UiAppDeck.Read("1|fav=" + new string('x', 70)).IsFav(new string('x', 70)),
            "app deck: a stored line is cut to its limits and an id too long is dropped");
        Check(!made.ToggleFav("a|b") && !made.ToggleFav("a,b") && !made.ToggleFav("a=b") && !made.ToggleFav(string.Empty) && !made.PutOnBar("a|b", apps), "app deck: an id the line cannot carry is refused");

        // The store: written on a commit, read once, a store that throws is the default.
        string kept = null;
        Func<string> load = UiAppDeck.Load;
        Action<string> save = UiAppDeck.Save;
        try
        {
            UiAppDeck.Save = l => kept = l;
            UiAppDeck.Load = () => kept;
            UiAppDeck.Forget();
            made.Commit();
            UiAppDeck.Forget();
            Check(kept == line && UiAppDeck.Current.Write() == line, "app deck: committed to the store and read from it once");
            UiAppDeck.Load = () => throw new InvalidOperationException("no store");
            UiAppDeck.Save = l => throw new InvalidOperationException("no store");
            UiAppDeck.Forget();
            made.Commit();
            Check(!UiAppDeck.Current.Custom, "app deck: a store that fails is the default deck, never a fault");
        }
        finally
        {
            UiAppDeck.Load = load;
            UiAppDeck.Save = save;
            UiAppDeck.Forget();
        }
        int was = made.Revision;
        made.Restore(line);
        Check(made.Revision > was && made.Write() == line, "app deck: a cancelled drag puts the deck back as it was");

        // Favourites: toggle, order, the tab's list.
        var fav = new UiAppDeck();
        Check(fav.ToggleFav("BizMan") && fav.ToggleFav("Contacts") && fav.ToggleFav("Persona") && fav.IsFav("Contacts"), "app deck: an app marked a favourite stands last");
        var ids = new List<int>();
        fav.Favourites(apps, ids);
        Check(ids.Select(i => apps[i].Id).SequenceEqual(new[] { "BizMan", "Contacts", "Persona" }), "app deck: the favourites list is in the player's order");
        Check(fav.MoveFav("Persona", -1, apps) && fav.MoveFav("Persona", -1, apps) && fav.FavAt(0) == "Persona" && !fav.MoveFav("Persona", -1, apps) && fav.MoveFavTo("Persona", 2, apps) && fav.FavAt(2) == "Persona",
            "app deck: a favourite moves up and down and by a drag");
        Check(fav.ToggleFav("Contacts") && !fav.IsFav("Contacts") && fav.FavCount == 2 && !fav.Custom, "app deck: the star again unmarks it, and favourites leave the bar alone");
        fav.Apply(apps, row);
        Check(apps.Where(a => a.Favourite).Select(a => a.Id).OrderBy(x => x).SequenceEqual(new[] { "BizMan", "Persona" }) && row.Select(i => apps[i].Id).SequenceEqual(home),
            "app deck: the flags follow the deck and the favourites do not move the row");
        Check(fav.ResetBar() == false && fav.FavCount == 2, "app deck: a bar reset keeps the favourites");
        UiAppDeck none = UiAppDeck.Read(string.Empty);
        none.Favourites(apps, ids);
        Check(ids.Count == 0, "app deck: no favourites is an empty list, not a fault");

        // The frame's list: refreshed from a source it takes the deck, and a change moves the version and tells the row.
        Func<List<UiShellApp>, int> source = UiShellApps.Source;
        try
        {
            UiAppDeck.Load = null;
            UiAppDeck.Save = null;
            UiAppDeck.Forget();
            UiShellApps.Source = into =>
            {
                into.AddRange(UiSamples.ShellApps((key, english) => english, true));
                return 7;
            };
            UiShellApps.Refresh();
            Check(UiShellApps.Row.Select(i => UiShellApps.Items[i].Id).SequenceEqual(home) && UiShellApps.Items.Where(a => a.InRow).Count() == home.Length, "app deck: the frame's list with nothing chosen has the phone's row");
            int version = UiShellApps.Version, told = 0;
            Action tell = () => told++;
            UiShellApps.Rearranged += tell;
            UiAppDeck.Current.TakeOffBar(home[0], UiShellApps.Items);
            UiShellApps.Rearrange();
            Check(told == 1 && UiShellApps.Version != version && UiShellApps.Row.Count == home.Length - 1 && !UiShellApps.Items.First(a => a.Id == home[0]).InRow, "app deck: a change moves the frame's version and tells whatever draws the row");
            UiShellApps.Rearrange(false);
            Check(told == 1, "app deck: a drag in progress does not tell the row until it is dropped");
            UiShellApps.Rearranged -= tell;
            UiAppDeck.Current.ResetBar();
            UiShellApps.Rearrange(false);
            Check(UiShellApps.Row.Select(i => UiShellApps.Items[i].Id).SequenceEqual(home), "app deck: after a reset the frame's list has the default row again without asking the source");
            UiShellApps.Refresh();
            Check(UiShellApps.Refresh() == false, "app deck: a second refresh with nothing changed does not move the version");
        }
        finally
        {
            UiShellApps.Source = source;
            UiAppDeck.Load = load;
            UiAppDeck.Save = save;
            UiAppDeck.Forget();
            UiShellApps.Items.Clear();
            UiShellApps.Row.Clear();
        }

        AppDeckSources();
    }

    private const int PagerRulesTiles = 8;

    // The status bar's model for a row: the apps in the order given, as UiGame.Row fills it.
    private static UiStatusModel RowModel(List<UiShellApp> apps, List<int> row)
    {
        UiStatusModel m = UiSamples.ShellBar(-1, false);
        m.Apps = row.Select(i => apps[i].Icon).ToArray();
        m.AppTints = row.Select(i => apps[i].Tint).ToArray();
        m.AppBadges = row.Select(i => apps[i].Badge).ToArray();
        m.AppPictures = row.Select(i => apps[i].Picture).ToArray();
        m.AppsMost = row.Count > 10 ? UiAppDeck.BarMost : 0;
        m.Time = m.Day = m.Season = m.Place = m.Money = m.Temperature = null;
        m.Energy = m.Hunger = m.Happiness = -1;
        m.Alerts = 0;
        return m;
    }

    // What the sources must hold: every control reachable by the keys and a pad, the choice kept with the settings on this
    // computer and not with the save, the old interface left to Aluna's Phone Pages and the mod's own phone folders.
    private static void AppDeckSources()
    {
        string screen = ModSource(@"Ui\Shell\AllAppsScreen.cs");
        string prefs = ModSource(@"Ui\UiGameDeckPrefs.cs");
        string layout = ModSource(@"Ui\UiAllApps.cs");
        Check(screen.Contains("IUiDragging") && MemberText(screen, "public bool Act(UiAction action)").Contains("UiAction.Alt") && MemberText(screen, "public bool Act(UiAction action)").Contains("UiAction.Option"),
            "app deck: the screen takes a drag, F for the star and P for the bar, so no control needs the mouse");
        Check(layout.Contains("EarlierCommand, index") && layout.Contains("LaterCommand, index") && layout.Contains("PinCommand, index") && layout.Contains("StarCommand, index") && layout.Contains("EditCommand")
              && layout.Contains("ResetCommand") && layout.Contains("Chain(s, row, left"), "app deck: each row's controls are targets chained to the row, so Left and Right (the d-pad) walk to them");
        Check(prefs.Contains("PlayerPrefs.SetString") && prefs.Contains("reallife.apps.deck") && !prefs.Contains("SaveGame") && !prefs.Contains("Save.Current"),
            "app deck: kept with the game's settings on this computer (as the windows' places and the phone's layout), never in the save");
        Check(!prefs.Contains("Network") && !prefs.Contains("GoingPublic") && !prefs.Contains("GP."), "app deck: nothing is sent and nothing of another player's is touched");
        string shell = ModSource(@"Ui\Shell\UiShell.cs");
        Check(shell.Contains("UiShellApps.Rearranged += RowChanged") && shell.Contains("UiGameDeckPrefs.Start()"), "app deck: the full menu's frame lays the row again when the deck changes, and the store is wired when the interface starts");
        Check(ModSource(@"Ui\Hud\HudHost.cs").Contains("UiShellApps.Rearranged += Rearranged") && ModSource(@"Ui\Hud\HudHost.cs").Contains("UiShellApps.Rearranged -= Rearranged"),
            "app deck: the HUD's bar lays the row again when the deck changes, and lets go of the event when it goes");
        string game = ModSource(@"Ui\UiGame.cs");
        Check(MemberText(game, "private static void Row(UiStatusModel m)").Contains("UiShellApps.Row[r]") && ModSource(@"Ui\Hud\HudScreen.cs").Contains("UiShellApps.Row[index]")
              && shell.Contains("UiShellApps.Row[index]"), "app deck: the row's tiles, a press on the full menu's row and a press on the HUD's row all follow the one order");
        Check(!ModSource(@"Core\GameUi\PhoneApps.cs").Contains("UiAppDeck") && !ModSource(@"Core\GameUi\PhoneGridPager.cs").Contains("UiAppDeck"), "app deck: the old interface's phone and menu are not touched (Phone Pages orders and hides apps there)");
    }
}
