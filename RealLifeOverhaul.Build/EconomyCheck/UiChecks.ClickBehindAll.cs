using System;
using System.Reflection;
using RealLifeOverhaul.Ui;

// Every click one behind, in nearly every app (the owner, 5 Nov evening, with another mod installed that hooks the phone's apps:
// the first click only lit its target, the next did what the first asked). The windows watch (UiForeignWindows) took the full
// menu's own canvas, which the kit draws its apps on, for that mod's window, since the mod hangs a script on each app inside it.
// Every app host then held its keys, and a held host was switched off, so a press acted and was laid out only by the next click's
// focus. The watch, the game host and the kit host need the engine, so this drives the game-free rules they decide by, in the
// order a session meets them, and reads those files for the calls; where a rule is missing it runs the decision the file made
// without it. Run: --click-behind-all; the lines read "click behind all: ...".
internal static partial class Program
{
    private static void UiClickBehindAllChecks()
    {
        string host = ModSource(@"Ui\UiHost.cs"), gameHost = ModSource(@"Ui\Host\UiGameHost.cs"), watch = ModSource(@"Ui\Compat\UiForeignWindows.cs"),
            overCanvas = ModSource(@"Ui\Host\UiOverCanvas.cs");
        Check(host.Length > 0 && gameHost.Length > 0 && watch.Length > 0 && overCanvas.Length > 0, "click behind all: the kit's host files are read");
        bool holdSwitchesOff = BehindAllHoldSwitchesOff(host);

        // The notices stand from the load, so the first kit screen opens with the city: the HUD's canvas is up then, the background.
        bool hud = BehindAllWindow(true, true, true, true, true, true, 1f, true);
        // The phone's full menu opens: the game keeps its canvas switched off until then, the kit's apps stand on it, and the
        // other mod's watcher sits on every app inside it. The canvas has its own group, seen and blocking while the menu is up.
        bool menu = BehindAllWindow(false, true, true, true, true, true, 1f, true);
        Check(!hud, "click behind all: a canvas up when the first kit screen opened is never a window over the kit");
        Check(!menu, "click behind all: the full menu's own canvas, which the kit draws its apps on, is no window of the mod that hooks the apps inside it");
        Check(UiForeignRules.KitHasKeys(menu, false, false), "click behind all: with only the full menu open, an app of it has its own keys");

        // Two clicks, A then B, on each kind of host the watch holds.
        BehindAllKinds(menu, false, false, true, holdSwitchesOff, "with the full menu open and another mod's watcher in its apps");

        // A real window of another mod's over the kit (its own canvas, outside the game's interface) still holds every kit screen's
        // keys, so Escape is the window's; a click beside it on the kit is drawn at once all the same.
        bool window = BehindAllWindow(false, false, false, false, true, false, 1f, true);
        Check(window && !UiForeignRules.KitHasKeys(window, false, false) && !UiForeignRules.KitHasOverlayKeys(false, false, false, window),
            "click behind all: a window of another mod's own over the kit still holds the kit's keys");
        BehindAllKinds(window, false, false, true, holdSwitchesOff, "under another mod's window");
        BehindAllKinds(false, true, false, true, holdSwitchesOff, "under a box of the game's");
        BehindAllKinds(false, false, true, true, holdSwitchesOff, "under All apps");
        BehindAllKinds(false, false, false, false, holdSwitchesOff, "on the street, nothing over it");

        // A mod's canvas inside the game's interface that the kit does not stand on is still its window; a game canvas with nothing
        // of a mod's in it is not; a window faded out or letting clicks through, but left switched on, holds nothing.
        Check(BehindAllWindow(false, false, true, true, true, false, 1f, true), "click behind all: a mod's own canvas put inside the game's interface is still its window");
        Check(!BehindAllWindow(false, false, true, false, true, false, 1f, true), "click behind all: a canvas of the game's with nothing of a mod's in it is no window");
        Check(!BehindAllWindow(false, false, false, false, true, true, 0f, true) && !BehindAllWindow(false, false, false, false, true, true, 1f, false)
              && !BehindAllWindow(false, false, false, false, false, false, 1f, true),
            "click behind all: a mod's window faded out, letting clicks through or with its raycaster off holds no kit screen's keys");

        // A press that sends its screen away (View on map closing the full menu) is still not laid out for nobody.
        BehindAllClicks(false, holdSwitchesOff, true, out int leftA, out _);
        Check(leftA == -1, "click behind all: a press on a screen leaving (the full menu closing on the way to the map) is not laid out");

        // The pause menu's own kit is the menu that is open: its being open never holds its keys.
        Check(gameHost.Contains("bool pause = global::UI.MiniMenu.MiniMenu.IsOpen && _screen.Id != PauseKits.PauseId;")
              && gameHost.Contains("Hold(!UiForeignRules.KitHasOverlayKeys(pause ||"),
            "click behind all: the pause menu's kit has its keys, and its clicks drawn, while the pause menu is open");

        // The game files make those calls.
        Check(watch.Contains("UiForeignRules.Foreign(ground, inGame,") && watch.Contains("bool ground = KitGround(c.transform);")
              && watch.Contains("UiForeignRules.TakesMouse(") && watch.Contains("Up = Open[i] != null && TakesMouse(Open[i]);"),
            "click behind all: the windows watch asks the rules, never takes the canvas the kit stands on, and a window that stops taking the mouse holds no keys");
        Check(gameHost.Contains("UiForeignWindows.Forget(root);") && overCanvas.Contains("UiForeignWindows.Forget(root);"),
            "click behind all: a canvas the kit binds to is forgotten by the watch, however early it was first judged");
        Check(!holdSwitchesOff && host.Contains("if (PointerOnly || _held)") && host.Contains("UiHostRules.Gone(gameObject.activeInHierarchy, enabled,"),
            "click behind all: a held host keeps running and is laid out as it changes; only hidden, asleep or leaving is gone");
        Check(gameHost.Contains("_host.Hold(hold);   // a screen put away keeps it too"),
            "click behind all: a hold reaches the host even while its screen is put away, so it never shows again with a stale hold");
        Check(host.Contains("\", gone \" + (GoneWhy() ?? \"no\")") && host.Contains("HeldBy?.Invoke()") && gameHost.Contains("_host.HeldBy = HeldBy;"),
            "click behind all: the press line says what kept a press from being drawn and what held the keys");
    }

    // Two clicks on each host kind the watch can hold, with what stands over the kit: each is drawn in its own press.
    private static void BehindAllKinds(bool foreignWindow, bool gameBox, bool allApps, bool fullMenu, bool holdSwitchesOff, string where)
    {
        string[] kinds = { "an app of the full menu", "a panel of the HUD", "a box", "an overlay with keys" };
        bool[] held =
        {
            !UiForeignRules.KitHasKeys(foreignWindow, gameBox, allApps),
            !UiForeignRules.KitHasKeys(foreignWindow, gameBox || allApps || fullMenu, false),
            foreignWindow,
            !UiForeignRules.KitHasOverlayKeys(false, fullMenu, gameBox || allApps, foreignWindow),
        };
        for (int k = 0; k < kinds.Length; k++)
        {
            BehindAllClicks(held[k], holdSwitchesOff, false, out int afterA, out int afterB);
            Check(afterA == 0 && afterB == 1, "click behind all: " + kinds[k] + " " + where + (held[k] ? " (its keys held)" : string.Empty)
                + " draws click A as A is pressed and B as B is (drawn " + afterA + ", then " + afterB + "; 0 is A, 1 is B, -1 nothing yet)");
        }
    }

    // Clicks A (0) then B (1) on a screen as the kit host runs them: a press acts, and is laid out in the press unless the host reads
    // as gone; B's focus first lays out a change still waiting (UiHost.FocusNode). What is drawn after each press, -1 for nothing yet.
    private static void BehindAllClicks(bool held, bool holdSwitchesOff, bool leaving, out int afterA, out int afterB)
    {
        bool running = !(held && holdSwitchesOff);
        bool laysOut = BehindAllGone(true, running, leaving) == null;
        int drawn = -1;
        int state = 0;
        bool dirty = true;
        if (laysOut)
        {
            drawn = state;
            dirty = false;
        }
        afterA = drawn;
        if (dirty)
        {
            drawn = state;
        }
        state = 1;
        if (laysOut)
        {
            drawn = state;
        }
        afterB = drawn;
    }

    // Whether the watch takes a canvas for another mod's window, by the rules the watch asks.
    private static bool BehindAllWindow(bool wasThere, bool kitGround, bool inGame, bool modInside, bool raycaster, bool grouped, float alpha, bool blocks)
    {
        return UiForeignRules.IsWindow(wasThere, false, !BehindAllForeign(kitGround, inGame, modInside), true, BehindAllMouse(raycaster, grouped, alpha, blocks));
    }

    private static bool BehindAllForeign(bool kitGround, bool inGame, bool modInside)
    {
        MethodInfo rule = typeof(UiForeignRules).GetMethod("Foreign", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
        // Without the rule the watch judged: outside the game's interface, or a mod's script or listener anywhere under it.
        return rule != null ? (bool)rule.Invoke(null, new object[] { kitGround, inGame, modInside }) : !inGame || modInside;
    }

    private static bool BehindAllMouse(bool raycaster, bool grouped, float alpha, bool blocks)
    {
        MethodInfo rule = typeof(UiForeignRules).GetMethod("TakesMouse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool), typeof(bool), typeof(float), typeof(bool) }, null);
        // Without the rule a raycaster on the canvas was enough.
        return rule != null ? (bool)rule.Invoke(null, new object[] { raycaster, grouped, alpha, blocks }) : raycaster;
    }

    private static string BehindAllGone(bool active, bool running, bool leaving)
    {
        MethodInfo rule = typeof(UiHostRules).GetMethod("Gone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
        // Without the rule the host asked isActiveAndEnabled and its owner's Leaving.
        return rule != null ? (string)rule.Invoke(null, new object[] { active, running, leaving }) : !active || !running ? "switched off" : leaving ? "leaving" : null;
    }

    // True when the kit host's Hold switches the host off, as it did: its frame stopped, and a press read it as gone.
    private static bool BehindAllHoldSwitchesOff(string host)
    {
        int from = host.IndexOf("public void Hold(bool hold)", StringComparison.Ordinal);
        int to = from >= 0 ? host.IndexOf("private void Update()", from, StringComparison.Ordinal) : -1;
        return from < 0 || to < 0 || host.Substring(from, to - from).Contains("enabled =");
    }
}
