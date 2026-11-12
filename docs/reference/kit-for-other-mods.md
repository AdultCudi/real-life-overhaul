# The interface kit and other authors' mods

Real Life Overhaul draws many of the game's own screens on its own interface (the kit, `Scripts/Ui`): the phone and its apps, the full menu's row and All apps, BizMan, Contacts, My Employees, Persona, EconoView, Rivals, Market Insider, the HUD and its bar, the pause menu, the notices and the boxes, and more. Other authors' mods add to the same screens through the game's mod API. This page says what the kit does with what they add, and the small API a mod can call to fit the kit on purpose.

Nothing here needs another mod to change. A mod that never calls the API is shown and held by the kit as described under "What the kit does on its own".

## What the kit does on its own

| What a mod adds | What the kit does |
|---|---|
| A phone app (a copy of a game button on the HUD phone's grid; the game has no call for an app) | Its tile is in the kit's phone, the full menu's app row when it is among the first, and All apps under Other mods. It shows its own picture, or a mark the kit makes: its name's first letter in one of eight hues chosen from its id. It opens its own panel in the game's own chrome, above the kit's frame, with its own keys. |
| An app on the full menu's own row alone (a copy of the row's template in `Topbar/AppButtons`, with no button on the phone) | The kit hides that row, so the button is an app of the phone's model like any other (`Ui/Phone/PhoneExtras`): a tile on the kit phone's home screen and a line of All apps under Other mods, with its own picture, and the first two stand on the kit's app row after the phone's first page, where the game's row had them. Pressed away from the full menu, the menu is opened first, as the game opens it, since the app's page stands in it. A button with the same words as one the mod also put on the grid is one tile, not two. |
| A button hung on the HUD phone outside its grid | The kit phone covers the phone, so the button is a tile of the kit phone and a line of All apps under Other mods, pressed through itself. Told by who listens to it: code of another mod's and none of the game's. |
| A tab or a button on a game panel | BizMan: the tab is an entry in the sidebar and its page opens in the game's own view; a piece outside the tab bar, or a button a mod put inside the page the game shows, is listed under Other mods. The building's panel of the HUD: up to two buttons of another mod's are actions of the kit's panel under the game's own, pressed through themselves. Any other replaced screen: a piece the game's panel does not have (found by what the game's own children are, never by a list of mods) makes the screen show the game's own view until it closes. Said once in Player.log with the mod's name and where. |
| A button or a piece on the HUD's bar | The kit hides only the game's own leaves of the top bar, so the mod's piece stays where the game puts it, over the world, and works as it did. |
| Its own canvas or window, opened while a kit screen is up | Lifted just over the kit's canvases (put back when it closes), the cursor freed, and every kit screen holds its keys while it stands, so Escape and the rest are the window's first. The kit never takes the game's Escape from a window above it. |
| A changed label or picture on a game button | The kit reads the button as the game shows it, so the mod's words and picture are shown. |
| Code of its own that throws when the kit reads or presses its piece | Caught and said once in Player.log with the mod's name. A label that throws is left empty and the screen goes on; a press that throws makes that screen the game's own for the session. No other screen is touched, nothing reaches the game's loop, and it is never counted as a fault of the interface. |

Every screen the kit draws over has its own switch in Mod options (the RLO interface's sections: Apps, Business, Boxes, Hud and the others). A player whose other mod needs the classic screen turns that one switch off and keeps the rest.

Nothing of another mod's is ever moved, re-parented or switched: its button stays where its mod put it and looks for it again, and the kit presses it through itself. The kit looks for such buttons when the phone or the full menu opens and about once a second while it shows, by the objects alone, so an app a mod adds late is found whenever it comes and nothing is searched a frame.

At load Player.log has one line, "UI: other mods' pieces on the RLO interface", that lists each piece found, its mod, where, and how the screen holds it, and a second that says whether windows over the kit are watched and how many screens are the game's own after a fault. A third counts other mods' entries by where the player finds them, each by name, and is written again whenever it changes: "Other mods' apps: 2 on the phone (Empire Motors, Empire Bank), 0 on the HUD, 1 in BizMan (...)." A player who cannot find another mod's app sends that line.

## The API

`RealLifeOverhaul.Ui.KitForOtherMods`, in `RealLifeOverhaul.dll`. A mod references the DLL (or finds the type by reflection and calls it only when it is there). Every call is safe at any time: before the city, twice, with a null. A call the kit cannot serve returns `false` and does nothing. `ApiVersion` only grows.

```csharp
public const int ApiVersion = 1;
public static bool IsOn { get; }
public static bool IsScreenOn(string screenId);
public static bool RegisterApp(string id, string name, Sprite icon, Func<Transform, GameObject> panel, Func<IUiScreen> screen = null);
public static bool AddPage(string appId, string pageId, string title, Action open);
public static bool AddSetting(string id, string label, Func<bool> get, Action<bool> set);
```

- **`IsOn`**: the RLO interface is drawing the game's screens now (its switch and Living population are on).
- **`IsScreenOn("bizman")`**: the kit draws that screen now. The ids are the ones in Mod options ("contacts", "bizman", "phone", "hud", "pause" and so on); false while its switch is off or after a fault.
- **`RegisterApp`**: puts an app on the phone. `id` names its objects and must be unique and no game app's name. `name` is its title (a locale key of the game's, or the words themselves). `icon` is its picture, or null for a tile the kit paints in a hue of its id. `panel` builds a plain Unity panel under the holder it is given (the game fades it as it fades any app); give null when `screen` is given. `screen` is a screen of the kit's own (`IUiScreen`, built from the kit's parts: `UiAppWindow`, `UiDraw`), which the kit draws over an empty panel while the interface is on. The app is installed once the phone is built, in each city.
- **`AddPage`**: adds a page to BizMan's sidebar (`appId` is `"bizman"`). `open` runs when the player chooses it: the mod opens its own window or panel.
- **`AddSetting`**: adds an on and off row to Mod options under Other mods, in the RLO interface's section. `get` is read when the page shows, `set` runs when the player flips it.

Everything the mod hands over runs inside the kit's guard. A fault is blamed on the mod by name in Player.log, once, and the screen it came through is the game's own for the session.

## A minimal example

```csharp
using RealLifeOverhaul.Ui;
using UnityEngine;

public static class FitTheKit
{
    // Call once, any time after the mod's own start.
    public static void Register(Sprite icon)
    {
        KitForOtherMods.RegisterApp("MyMod_Notes", "My notes", icon, holder =>
        {
            var panel = new GameObject("MyMod Notes panel", typeof(RectTransform));
            panel.transform.SetParent(holder, false);
            // ... build the mod's own panel under it ...
            return panel;
        });
        KitForOtherMods.AddPage("bizman", "mymod_page", "My notes", () => MyWindow.Open());
        bool shown = true;
        KitForOtherMods.AddSetting("mymod_shown", "Show my notes", () => shown, on => shown = on);
    }
}
```

A mod that must also run without RLO keeps this class apart and loads it only when an assembly named RealLifeOverhaul is among the loaded ones (`AppDomain.CurrentDomain.GetAssemblies()`).

The app appears with its icon in the kit's phone and All apps, opens its panel like any game app and closes back to the phone. The page appears in BizMan's sidebar. The row appears in Mod options.

## What the kit checks

EconomyCheck (`UiForeignChecks.cs`, alone with `--other-mods`) runs a second mod that exists only in the checks (`RealLifeOverhaul.Build/UiPreview/UiSamples.FakeMod.cs`): apps with and without a picture, three apps on the full menu's row alone and a button hung on the phone, a BizMan tab and a loose button, a button on the building's panel, a piece on the HUD's bar, a window over a kit screen, a button and a text that throw. It checks each case through the kit's rules, the public surface against this page, that every screen the host draws over has a switch, and what the guards cost. The previews `modcompat_phone` and `modcompat_bizman` draw the phone and BizMan with the fake mod's pieces on them.
