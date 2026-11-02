# The host: how a game screen goes onto the kit

Every screen of the game's that the RLO interface draws over goes through one host, `UiGameHost`, so the binding, the
hiding and the way back are written once and tested once. The game's object stays alive and active underneath; the kit
reads the game's state after the game's (and other mods') code ran and presses the game's own widgets. The game's rules,
saves and events stay the game's. Contacts is the worked example (`Scripts/Ui/Contacts/ContactsKit.cs`).

## The five steps

1. **Describe the screen** in a small class implementing `IUiGameScreen` (see `ContactsKit.ContactsGameScreen`):
   - `Id`: the screen's id in `UiScreens`, which its own switch is keyed by.
   - `Name`: what Player.log calls it ("the Contacts app").
   - `Title`, `Icon`: the window's strip.
   - `Kind`: `App` (an app of the full menu: fades with the menu, holds its keys under a box, the frame owns the menu's
     chrome), `Box` (a box over everything: its keys are its own), `Overlay` (over the world: takes no key) or `Panel` (a
     panel of the HUD over the world, the dialogue: its keys its own, held under a box or the phone, put away while the
     game's pause menu stands as the game hides its HUD; it takes no click outside its own targets, so the rest of the
     HUD stays the player's).
   - `Find()`: the game's object, switched off or on, or null while the game has not built it. Never search the scene for
     it: read it from `UIs`, a field of the game's (through `GameContract`), or a child by its path.
   - `Check(found)`: why it is not built as this version expects, or null.
   - `Conceal(found, conceal)`: the game's own layout leaves to hide (`conceal.Add(leaf)`), and any graphic standing on a
     shared parent (`conceal.Add(graphic)`). Never a shared parent (`UiHostRules.SharedParents`, or one you pass to
     `conceal.Shared`): other mods fill them. Never switch anything off.
   - `Make(found)`: the kit screen (`IUiScreen`) that mirrors it.
2. **Mirror, never compute.** In the screen, read the game's widgets through `UiWidgets` (`Shows`, `There`, `Label`,
   `Text`, `Tint`, `On`, `Options`, `Field`) and press them through it (`Press`, `Set`, `Pick`, `Type`, `Slide`, or
   `Calling(root, "MethodName")` for a button the game wired in the Inspector). Going Public hides tabs, disables buttons
   and tints cells: what the vanilla object says is the truth. Look at a signature of the game's state a few times a
   second (`Every = 0.25f`), never every frame, and lay out only when it changed. A mod's addition inside the screen is
   offered as an entry that calls `host.Classic()`: the game's own view until the screen closes.
3. **Make one host** for the screen, once: `new UiGameHost(new MyGameScreen())`, kept in a static field. Call its
   `Install()` from `UiDirector.Slow` while `UiScreenSwitches.On(id)`, `Remove(why)` when the switch is off, and
   `Rearm()` with the others when the city goes. A call or anything the game must run in its own view:
   `host.StepAside(() => over)`; the kit comes back the moment `over` says so.
4. **Register and switch**: the screen's entry in `UiScreens.<Area>.cs` with its template, runner and keys, and
   `.Switch(UiScreenSwitches.Apps)` (or `Boxes`, or a section of your own in `UiScreenSwitches.Sections`). Its switch's
   name is `reallife_ui_screen_<id>` and its tip `reallife_opt_tip_ui_screen_<id>`, in en, pt and zh-cn. That is all:
   the options page, the settings and the tree pick it up from the entry.
5. **Declare the game's members** you read or press in `Scripts/Core/GameContract.<Area>.cs`, a part file of
   `GameContract` holding your screen's `GamePart` and its lines, self-contained (lean on nothing in another part file).

## What the host does for you

- Binds over switched off objects (the canvases walked with `GetComponentsInParent<Canvas>(true)`), looks again a few
  times a city while the screen is not built (`UiBindRules.Wait`), and stays the game's own until the next city after
  `UiBindRules.Stops`, with one Player.log line.
- Puts a watcher (`UiGameWatch`) on the game's object, so the kit shows the moment the game opens it and goes the moment
  it closes, the leaves given back exactly as found (`UiConceal`: alpha, interactable and blocksRaycasts as they were).
- Stands its canvas just above the screen it covers (`UiHostRules.Above`), a box one more so it covers the frame, all
  below the game's own boxes and notices and other mods' canvases; and holds the clicks no kit target takes.
- For an app: follows the full menu's fade, sleeps as the menu closes, steps aside for a box of the game's own, holds its
  keys under a box or All apps the kit draws, and tells the frame (`UiShell`) to take the menu's chrome.
- A fault in your screen gives the game's screen back for the session, with one warning in Player.log.

## The mod's own apps

An app of the mod's goes onto the kit through `PhoneAppDef.Kit` (its `IUiScreen` factory), `KitScreen` (its id in
`UiScreens`) and `KitIcon`; after `PhoneApp.Install`, hand the app and its definition to `UiShell.Face(app, def)`. The
frame then draws that screen over the app's panel whenever the interface and the screen's switch are on, the panel
built and kept unseen under it; otherwise the panel shows as it always has.

## Checked offline

EconomyCheck (`UiHostChecks.cs`): no file outside `Scripts/Ui/Host` binds over the game or hides its pieces its own way,
the shared parents are never given a group, another mod's objects are told from the game's and the mod's, every runner
has its switch with its words in three languages, and the status bar's app row keeps to the frame's commands.
