# The game's own interface, and how the mod uses it

Everything the mod shows should be the game's interface: a copy of a live object of the game, made while the game
runs, never a shipped copy and never a look-alike. This page records how the game's phone, tables, dialogs and click
actions are built, what the mod copies and how, and what to watch for. `Scripts/Core/GameUi` is the code that goes
with it; the Planner (`Scripts/Planner`) is the first feature built on it.

Sources for every statement here: the decompiled game (`decompiled/BigAmbitions/...`, named per item) and the game's
scene data read with UnityPy (`evidence/uitools/uitree.py`; the phone and the menus are in `level4`). Nothing below
has been seen running: the game cannot be started from the build machine. "Proven" means the member or object is in
the decompiled code or the scene data; "inferred" means it follows from them but depends on run-time behaviour.

## 1. The two phones

| | HUD phone | Full menu |
| --- | --- | --- |
| Class | `SmartphoneUI` (`UIs.Instance.smartphoneUI`) | `UI.Smartphone.FullMenu` (`UIs.Instance.fullMenu`) |
| Scene path | `Canvases/Smartphone` | `Canvases/FullMenu/Canvas` |
| Canvas | overlay, order 2, scaler 3840x2160, match height | overlay, order 3, same scaler |
| App buttons | `.../Phone/AppButtons` (GridLayoutGroup, cell 250x225, box 558x868: two columns, four rows) | `Topbar/AppButtons` (HorizontalLayoutGroup, spacing 20, buttons 220x245) |
| Button template | `AppButtonTemplate`: Button, ButtonEffects, `SmartphoneAppButton`; children `Title` (TMP 28 + TextLocalizationComponent), `Icon` (160x160), `Badge` (inactive; `UI.Elements.Badge`) | `AppButtonTemplate`: `FullMenuAppButton`, Button, ButtonEffects; children `Title` (TMP 26.5), `Logo` (160x160), `SelectedIcon` (12x12 sprite `circle`, inactive) |
| Built in | `SmartphoneUI.Awake`: one `template.CreateElement()` per `SmartphoneApps.appList` entry, kept in the public `appButtons` dictionary by `AppName` | `FullMenu.Start`: the same for apps with `showInFullMenu`, kept in private `_appButtons` |

- `AppName` is an enum of eight (Persona, Contacts, MyEmployees, BizMan, EconoView, VoogleMaps, MarketInsider,
  Rivals). The eight HUD buttons fill the grid exactly; Voogle Maps is not in the full menu, so its bar has seven.
- An app is a child of `FullMenu.appsContainer` whose GameObject name is the `AppName`. `FullMenu.SelectApp` switches
  every child off and the one of that name on (fading its CanvasGroup over `transitionDuration`, 0.1 s, if the menu
  is already open), writes `appNameLabel.Key`, moves the dot (`HideSelectedIcon`/`ShowSelectedIcon`) and calls
  `Toggle(true)` if the menu was closed. `Toggle(true)` pauses the game and hides the HUD; Esc closes the menu
  (`CloseFullMenu`). The game is therefore paused for as long as any app is open.
- `SmartphoneUI.OpenApp` first clears the gamepad outline and closes `UIs.buildingResume`; `FullMenu.ShowApp` first
  asks about unsaved BizMan settings (`bizMan.business.bizManSettings.HasUnsavedChanges`) through `HudConfirm`.
- The gamepad walks the HUD buttons by casting an index to `AppName` and looking it up in `appButtons`
  (`OutlineApp`). **Never add to that dictionary**: a ninth entry would be reached with a cast that is no `AppName`.
- `Transform.ResetTemplate()` (HGExtensions) destroys every sibling of a template tagged `UiTemplate`. A copy of a
  template is untagged at once (`UiClone.Asleep`, `PhoneApp.Install`).

### Adding an app (`GameUi.PhoneApp.Install`)

One call puts a button on both phones and a panel among the game's. It copies the two templates (found through the
private `appButtonTemplate` fields; failing that the object of that name; failing that a live button, since clicks
added in code are not copied by `Instantiate`), sets the title through the copy's own `TextLocalizationComponent`,
the icon through `AppButton.SetIcon`, and the click to `PhoneApp.Open`, which repeats `OpenApp`/`ShowApp`/`SelectApp`
for a panel the game does not know by name. When the game opens one of its own apps its loop switches the mod's panel
off like any other; the panel's `PhoneAppRoot` then takes the mod's dot away. The badge is the copy's own
(`SmartphoneAppButton.UpdateBadgeCount`), as Contacts uses it.

Proven: every member named above, and both templates' structure. Inferred: that a copy made after `Awake`/`Start`
lays out as the ninth child (it is an ordinary child of a layout group).

### Pages on the HUD phone (`GameUi.PhoneGridPager`)

A ninth button has no room, so the home screen gets pages of eight: the wheel over the grid or a dot under it turns
the page; a dot turns the badge's red when a page that is not showing has something new. It runs on `OnEnable`,
`OnTransformChildrenChanged` and the scroll event only.

Other mods page this grid as well (studied, not copied: BizSuite's `PhoneAppsPager` puts a component on the grid and
shows or hides every child but the template; AlunaPhonePages pages every child that has a `SmartphoneAppButton`,
and re-reads the grid when its child count changes). Two pagers would undo each other, so this one **steps aside
for good** as soon as it sees a component on the grid from an assembly that is neither the game's nor the mod's, or
finds a button it had hidden shown again. It then shows everything it hid and logs one line. For that to work the
mod's button stays what those pagers look for: a real `SmartphoneAppButton`, a direct child of the grid, with no
"Template" in its name. The full menu's bar is not paged: seven buttons and one more fit.

## 2. The frame of an app (`GameUi.AppShell`, copied from MyEmployees)

`FullMenu.myEmployees` (public) is `AppsContainer/MyEmployees`, a "Layout 30-70":

```
MyEmployees [CanvasGroup, MyEmployees]
  Layout 30-70
    Header            TMP 50, white: the page's headline, top left
    Left (1200 wide)
      WhiteBox        VerticalLayoutGroup (padding 80/80/30/30, spacing 30), sprite white-round-corner-drop
        EmployeePersonalData/EmployeeName   TMP 50, dark (0.067, 0.082, 0.098)   -> InfoBox title
        EmployeePersonalData/EmployeeInfoRow1/Age   TMP 28.7, "Label: <b>value</b>" -> InfoBox line
        CurrentBusinessHeadline             a word on a grey rule                  -> InfoBox headline
        Demands/DemandTemplate              MainInfo: Name, ..., Toggle            -> InfoBox switch
        AssignedAddress                     the game's Dropdown: Header/LeftButton, MiddleBox, RightButton -> InfoBox selector
        Buttons                             PositiveActionButton, NegativeActionButton, AdditionalActionButton -> InfoBox buttons
    Splitter
    Right
      TableTabs       EmployeesTab (sprite white-box-top-left-corner), CandidatesTab (...-top-right-corner), each with "Label"
      EmployeesList   [EmployeeScrollerController]
        Headers       HorizontalLayoutGroup (padding left 216, right 30, spacing 20), one cell per column: Button, Title, ArrowUp/Down
        EnhancedScroller  ScrollRect + EnhancedScroller (spacing 15, padding left 127 right 30) + "Scrollbar Vertical"
```

`AppShell.Make` copies it asleep, takes off every script of the game's assembly except the ones that only draw or
sound (ButtonEffects, ToggleClickSound, Badge, HoverEffect and the two scroll bar helpers), destroys what is not
listed above, and hands the parts out. The selected tab is marked as `MyEmployees.ChangeTab` does it: not
interactable, label black, the other dark grey (`GlobalReferences.colors`).

**Asleep** is the rule that makes this safe: `Instantiate(original, parent)` under an inactive parent
(`UiClone.Dormant`) runs no `Awake` or `OnEnable`, so a script can be destroyed before it has done anything. A
script another script on the same object requires (`[RequireComponent]`) is destroyed after it.

### Tables: `BaTable`

Every list in the phone is a `BaTable.BaTable<TCellView, TModel>` (MonoBehaviour, `IEnhancedScrollerDelegate`) over
the EnhancedScroller in `ExternalPlugins.dll`, which makes only the rows in view and reuses them. It is generic and
public, so a mod's table is a subclass:

```csharp
public sealed class PlannerTable : BaTable<PlannerCellView, PlannerRow> { GetCellViewSize => 100; GetDataId => row.Id; }
public sealed class PlannerCellView : BaTableCellView<PlannerRow> { SetData(row) ... }
```

- `Start` sets `scroller.Delegate`, and makes a header cell sort its column if the cell's **name** is a public field
  or property of the model. Name header cells otherwise if nothing should sort.
- `GetCellView` calls `SetData`, then (with `allowRowSelection`) wires the row's Button to `SelectRow`, calls
  `VisualizeSelected(false)` and `ResetVisuals()`. `VisualizeSelected` sets the row background to
  `selectedBackgroundColor` and every text to midnight or white, **except** texts that are exactly the game's red
  or yellow (yellow becomes orange on the selected row). So: put a row's own look on in `ResetVisuals`, and use
  those two colours for emphasis.
- `GetDataId` reads a field called `Id` by reflection unless overridden.
- The scroller instantiates the row prefab itself. A prefab kept asleep in the scene gives inactive copies:
  `SetData` switches a new row on.
- A row is 100 high in every phone table (`BaseFilteredScrollerController.GetCellViewSize`).
- The first time an app is shown, the panel's `OnEnable` runs before the scroller's `Awake`: do not call
  `scroller.ReloadData()` before the table's `Start` (`PlannerTable.Show` keeps the rows until then).

The row itself is MyEmployees' (`EmployeeScrollerController.prefab`, an asset: `EmployeeCellView`, 1320x360 as
authored): a HorizontalLayoutGroup with `Background` (sprite white-round, 0.349/0.412/0.459/0.635), `RaycastTarget`,
a tick box and ten cells, each with a LayoutElement. The Planner keeps `EmployeeName`, `TaskInfo` (an icon holder
`WarningIcon` and the text `Task`), `BusinessName` and `Satisfaction` (right-aligned). Header row and rows get the
same layout (`AppShell.RowLayout`, `AppShell.Width`) so that cells stand under their headers.

## 3. Text

- `Localizor.LanguageChangeEvent.TextLocalizationComponent` (HGPlugins.dll) sits on almost every text. `Key` (and
  `Arguments`, `Prefix`, `Suffix`) write the text when they change, `OnEnable` writes it again, and a change of
  language rewrites every enabled one. `SetValue(text, clearKey: true)` writes final words and stops that.
  `UiClone.Key` and `UiClone.Words` are the two ways the mod writes a text. The mod's `reallife_` keys work as keys.
- Dates: `"topbar_date_format"` is `"{DayOfWeek} (Day {CurrentNumberDay})"`; weekdays are `common_monday`...; the
  game's `common_today`, `common_tomorrow`, `common_setdestination`, `common_cancel`, `common_close`,
  `common_confirm`, `menu_save`, `filebrowser_delete`, `bizman_status`, `bizman_menu_settings` are reused, so those
  words follow the game's language.
- Times: `hour.GetFormattedTime(minute)` (TimeHelper) follows the game's 12/24 hour option (`TimeHelper.use12h`).
- Money: `amount.ToCurrencyFormat()`.

## 4. Pictures

- App icons are 160x160 sprites (`AppIcon-Persona`...): a 112 px rounded tile with a two-colour gradient, a soft
  black shadow below it, a white glyph. `GameUi.AppIconPainter` reads one back through a render texture, repaints
  the tile in the app's own colours and stamps one of the game's white glyphs on it. If the read-back gives nothing
  (it uses `Graphics.DrawTexture` outside the render loop: inferred to work under HDRP, not proven), the glyph alone
  is the icon.
- `GameUi.GameSprites.Get(name)` finds one of the game's sprites in memory (one pass of
  `Resources.FindObjectsOfTypeAll<Sprite>` when a menu opens). **A sprite is certainly in memory only if something
  loaded uses it.** Used by an Image of the UI scene (`evidence/uitools/usedsprites.py`): Icon-Building, Icon-Cargo,
  Icon-Coin, Icon-Condition, Icon-Edit, Icon-Info, Icon-Notifications, Icon-OpeningHours, Icon-Parking,
  Icon-PersonManage, Icon-Route, Icon-Security, Icon-SetDestination, Icon-Shop, circle and the eight AppIcon-*.
  Not used by any Image of that scene (they may still be loaded by a prefab or a script): Icon-Calendar, Icon-Chat,
  Icon-Clock, Icon-Money, Icon-Neighborhood, Icon-People, Icon-PersonEngineer, Icon-Traffic. Always give a second
  name from the first list.

## 5. Notifications, questions, text entry, destinations, sound

| What | Call | Notes |
| --- | --- | --- |
| Notification | `Notifications.Show(type, headerKey, Dictionary data, seconds, duplicateIdentifier, onClick, sound, trackOnSaveGame)` | `NotificationType` Success, Warning, Error, Info. `{name}` in the text is filled from the dictionary. `onClick` runs when it is clicked. `Notifications.ShowError(key)` for a refusal. |
| Yes/no | `HudConfirm.Show(headerKey, bodyKey, onConfirm, onCancel, confirmKey, cancelKey, allowConfirmationSkip, isNegative)` | Works over the full menu (the game uses it there). `isNegative` gives the red look. |
| A line of text | `UIs.Instance.textInputUi.Show(placeholderKey, buttonKey, onConfirm, onClose, initialText, allowEmpty, characterLimit)` | `Canvases/TextInputUI`, its own canvas at order 21, above the menus. |
| Destination | `SaveGameManager.Current.customDestination = address; GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination); Notifications.Show(Success, "notification_destination_set", {address}); fullMenu.Toggle(false)` | Exactly what `BizManBusiness.SetDestination` does. For a point that is no building: `SetGuiderTarget(position, name, sprite, colour, type)`. An address is `new Address(streetName, streetNumber)`. |
| Sounds | hover and click sounds come with `ButtonEffects` and `ToggleClickSound` on the copied objects | nothing to call |

## 6. Time

- `GlobalEvents.onNewHour` is raised once per game hour, every hour, also at high speed, and by `NewDay` as well
  (`GameManager`); `GlobalEvents.onNewDay` once per day. Hold a handler on them with `EventHold.Keep`.
- Day 1 is a Monday, a week has 7 days (`TimeHelper.GetDayOfWeek`), a year has
  `SaveGameManager.Current.gameVariables.daysPerYear` days (`TimeHelper.GetYearsByDays`: day / daysPerYear).
  Game minutes are `(day * 24 + hour) * 60 + minute` (`Timestamp.GetTotalMinutes`).
- Base game dates read by the Planner: the tax bill (`TaxHelper.HasCurrentTaxesToPay`, `GetCurrentTaxesDueDay`,
  `GetCurrentTaxesToPay`; `TaxHelper.RunDaily` takes it by force the day after).

## 7. An action on an object in the world (`Home/HomeOffers`)

The game offers actions on objects through `Player.HUD.ItemInfoOverlays`:

1. Pointing at an `EntityController` asks `CtaManager.UpdateCta`, which goes through the private static list
   `CtaBehaviors` and takes the first `ICtaBehavior` whose `ShouldShow(entity)` is true. Its `GetCta` returns a
   text key and an action.
2. The object is outlined and the key is shown in the game's blue under the object's name (`CtaOverlay`).
3. A left click runs the action (`EntityController.OnIoLeftClick`); with no action it opens the object's detailed
   panel or walks over and interacts.
4. An object reacts to the pointer only if its public `simpleOverlayType` has a flag set
   (`EntityController.ShouldReactToIoEnter`). The game sets `SimpleOverlayType.ClickToAction` in code where it adds
   an action (`PrivateDriverVehicle`).

So a feature adds an action with one class deriving `ICtaBehavior`, inserted at the front of `CtaBehaviors` (by
reflection), and `simpleOverlayType |= ClickToAction` on its objects; both are taken back when the feature goes
(`HomeOffers.Remove`). `ShouldShow` is asked for whatever the pointer is on: answer with a type test and a
look-up, nothing dearer.

Do not take over an object whose click already opens a panel of the game's. The fridge is one: its call to action is
"Click to use" with no action, and the click opens `DecorativeItemHolderOverlay` (what is in it, "empty", "add").
An action there belongs in that panel as a button: section 13 (`Home/HomeFridgeButton`).

The game shows no key hints on objects themselves (`ActionKeyTooltip` is a tooltip on HUD tools): prefer the click.
Where a key is wanted at a place, the game's form for it is the panel of section 9.

An object of the mod's own can be pointed at as well (`Transport/TransitPointer`, section 11).

## 8. Pitfalls

- A destroyed object is not `null` to `?.`: test Unity objects with `== null`.
- The interface is rebuilt with the city scene. Keep no reference across a city load; `PhoneApp.Installed` tests its
  objects each time.
- Do not copy an app by `Instantiate` while it is active under an active parent: its scripts start and subscribe to
  the game's events.
- Persistent click calls saved on a button survive the copy and point at the copy's (now removed) scripts:
  `UiClone.Rewire` replaces the whole click event.
- A layout that should match another (rows and headers) must be set on both; the game's values differ between them.
- Nothing in an app runs while it is closed: its panel is inactive, and so is everything under it, the scroller included.

## 9. A panel over a place, with keys (`GameUi.WorldPanel`)

The dark box with an arrow that stands over a petrol pump or a lift is `UI.Overlays.OverlayUI`
(`UIs.Instance.overlayUI`, scene path `Canvases/PlayerHUD/OverlayUI`):

```
OverlayUI [VerticalLayoutGroup (padding 30/30/55/40, spacing 20), ContentSizeFitter, CanvasGroup]   pivot (0.5, 0)
  Background      sprite Box-Dark-Wide-Arrow, alpha 0.86
  Header
    FirstLine     TMP 36, white
    Splitter
    SecondLine    LeftLabel TMP 32, white; RightLabel TMP 32, capitals, right-aligned (the game's "Open" in green)
  Buttons
    ButtonTemplate  min height 80; Container (sprite Gradient-Blue-Round) / Text (TMP 32, capitals)
```

- It shows whatever implements the public interface `UI.Overlays.IOverlay`: a world position, three `LabelInfo`
  (a key or final words, a colour) and an array of `ButtonInfo` (name, text key, sprite name "blue", "gray" or
  "yellow" looked up in `GlobalReferences.wsButtons`, action, `PlayerAction`, interactable).
  `OverlayUI.Show(overlay)` and `OverlayUI.Hide(overlay)` are public and static.
- `OverlayUI.Update` puts the box at `WorldToScreenPoint(GetTargetPosition())` every frame. Give it a point in front
  of the camera: a point behind it comes out mirrored.
- Each button's text gets the suffix `PlayerAction.AsSuffix()` (" [F]": the player's own binding), and
  `GameManager` sends Interact, SecondaryInteract, SpecialInteract and Confirm to
  `OverlayUI.OnPlayerActionPressed`, which resets the action and runs the button's `onClick` (in a vehicle
  SpecialInteract also parks the car: section 16). A button is also a
  real `Button`: a click runs it too. So the mod reads no key and draws no prompt.
- The game's bindings (`PlayerInput`): Interact F, SecondaryInteract P, SpecialInteract O, Confirm Enter, Cancel
  Esc; the Player map has keyboard and mouse bindings only. The UI map has a gamepad's south button on Click and
  its east button on Cancel, so with a gamepad a button of this panel is clicked, not pressed by its key.
- There is one box. The game uses it at filling stations, the private driver's garage and lifts
  (`BuildingEntranceOverlay` exists but nothing shows it). `WorldPanel.Show` returns false while another panel has
  the box and takes it when it is free; whoever owns a `WorldPanel` calls `Show` from its timer while it wants it up.
- `OverlayUI.IsVisible` is static. It must not be left true when the city unloads (`WorldPanel.Hide` sees to it):
  the mod's second interact key (E) stands aside while it is true.
- Making the buttons again destroys and instantiates them (`SetUpButtons`), so a change of a line alone goes
  through the private `SetUpHeader` (a delegate made once) and only a change of the buttons through
  `RefreshButtons`.
- While a `WorldPanel` with a button on Interact is up, the mod's own interact key must do nothing else:
  `TransitDirector.TakesInteractKey` is true while it shows and in the frame its button ran.
- A panel has four places for buttons, one for each key the game sends to its box, in this order: Interact,
  SecondaryInteract, SpecialInteract, Confirm (`WorldPanel.Button(place, key, action, enabled, blue)`; the older
  `Buttons` sets the first two). A button that is not `interactable` is drawn dimmed and ignores its key. The drinks
  cart uses all four (`Jobs/Kinds/VendorJob`): three drinks and "Call out" while a customer orders, "Step away",
  "Let time pass" and "Call out" otherwise. Four buttons make the box about 320 units higher than its header.
- The game's Cancel key is not sent to this box. A panel that must be left by a key needs a button for it.
- **The building box.** The card that comes up over a shop's door while the pointer is on the shop is another object,
  `UI.InGameUI.BuildingResume` (`UIs.Instance.buildingResume`, an overlay canvas of its own). Its `LateUpdate` puts its
  private `panelRectTransform` over `entranceDoors[0]` every frame and switches it on while it has a target; nothing
  announces it. The game has no rule between that box and this one (its own panels stand at filling stations and lifts,
  where no building box comes up), so the kit has one, for every `WorldPanel` and `DoorPanel`
  (`GameUi/PanelPlacement.cs`, the rule; `GameUi/WorldPanel.Beside.cs`, the part that touches the game): the building
  box never moves; the panel steps to the side of it that has room on the screen, left or right first (the shorter
  step), then above or below, 14 pixels away; with no side free it leaves the box and `Show` answers false with
  `Waiting` true until the building box has gone or moved, as the game's small item card waits for its large one.
  Once on a side it stays there while that side has room, and it goes home only when home is clear by 42 pixels.
- **How the step is given.** `OverlayUI.Update` writes the box's place from `GetTargetPosition` every frame, so the step
  is handed to the game as a step in the world: the answer is the place plus a vector across the camera's view that
  is exactly the wanted pixels at the place's depth. The game then keeps the box beside the building box by itself.
  The two rectangles are read (`GetWorldCorners`, into an array made once) only when the building box comes or goes,
  when either has moved more than 6 pixels on the screen, or when the panel's words or buttons changed; the look is
  on `Canvas.willRenderCanvases`, which runs after every `LateUpdate` and the interface's layout and before the
  frame is drawn, so the two are never drawn over each other. The watch is on that event only while a panel of the
  mod's is up. `WorldPanel.Name` ("stop") is the word in the log line.

Proven: every member named above and the object's structure. Inferred: how the box looks with other content than
the game's (the second line makes it wider; capitals on the right-hand word and the buttons).

## 10. A list to travel from (`GameUi.TravelList`, copied from the subway's)

`UIs.Instance.cityMapSubwayStations` is `Canvases/CityMap/SubwayStations` (775 wide, down the right of the map):

```
SubwayStations [CanvasGroup, CityMapSubwayStations, DraggableWindow]
  Background        sprite grey-round-bordered
  Headline          sprite darkgreybox-header@2x; Header TMP 32, dark, capitals, key "subwaystations"
  Top Controls      VerticalLayoutGroup; InputField (the search field)
  Scroll View       ScrollRect; Viewport/Content (VerticalLayoutGroup spacing 10, ContentSizeFitter)
    Entry           tagged UiTemplate, 88.8 high: Image (off unless hovered), CityMapSubwayStationEntry
      Label         TMP 26, white, capitals
      DefaultButton Button, ButtonEffects; Container (Gradient-Blue-Round) / Text (TMP 26, key "common_travel")
    Scrollbar Vertical
  Cancel            Button; Container (Gradient-Yellow-Round) / Text (key "common_cancel", suffix " [ESC]")
```

- The game fills it in `CityManager` (`LoadStations`: one copy of `Entry` per station, named after it) and shows it
  with the map in subway mode. A click on an entry travels (`SubwaySystem.TravelTo`).
- `TravelList.Make` copies the window asleep, takes off every script of the game's except the ones that only draw
  or sound, destroys the station entries and the search field, and moves the copy to the HUD's canvas
  (`UIs.playerHUD`: the same scaler as the map's, so the same size), in the middle of the screen. The line of text
  under the headline is a copy of an entry's label. An entry is made 104 high and its label writes the name in
  capitals by a tag, with a smaller line under it.
- Rows are made once and used again (`Begin`, `Add`, `Open`). After a click the clicked button is unselected: the
  rows are used again for other places, and the interface's confirm key presses whatever is selected.
- Cancel: the button, and the game's cancel key through a script on the window (`TravelListKeys`, order -31000),
  which resets the action so that the game's own handler (`CancelButtonHandler.HandleEscapeClick`, which would
  open the game's menu) does not see the press. Inferred, not proven: that the reset hides the press from a
  handler later in the same frame.
- While it is open `TravelList.AnyOpen` is true, which `ThirdPersonCamera.IsUiOpen` counts: the mouse is freed.
  The game is not paused. Whoever opened it closes it when the player walks away or another screen opens.

## 11. Making an object of the mod's own clickable (`Transport/TransitPointer`)

Section 7 puts an action on an object of the game's. For an object the mod built (a bus stop), the object needs an
`EntityController` of its own for the game's pointer to find:

- `MouseController.Run` casts one ray a frame from the cursor on the layers InteractiveItems,
  InteractiveItemsOutlined, Buildings, BuildingsOutlined, Vehicles, PlayerVehicles and Ground, up to 600 m, and
  calls `OnIoEnter`, `OnIoExit` and `OnIoLeftClick` on the `EntityController` of the transform it hits. Traffic
  (layer AiVehicles) is not among them. Under the mod's camera the cursor is locked to the middle of the screen
  and still visible to the game, so the ray is the crosshair's.
- The game's physics has queries hit triggers, and the layer InteractiveItems does not meet AiVehicles in the
  collision matrix. So the collider is a **trigger** box on InteractiveItems: the pointer finds it, and it moves
  nothing and stops nothing (people walk on the navigation mesh; the game's own trigger handlers test tags).
- The component derives `EntityController` and overrides `Awake` and `Start` (the game's read places to walk to
  that only its own objects have), `Interact`, `ShouldReactToIoEnter`, `OnIoExit` (as the game's, then the outlined
  pieces go back to their own layers: the game's would leave them on InteractiveItems) and `OnDestroy`. It sets
  `simpleOverlayType = ClickToAction`, `customOverlayHeaderKey` (a locale key: the name shown), `renderers` (what
  is outlined: the game moves them to the outlined layer while the pointer is on the object) and `navMeshTargets`.
- The action comes from an `ICtaBehavior` at the front of `CtaManager.CtaBehaviors`, as in section 7. `GetCta` is
  asked about three times when the pointer comes onto the object and once per click: answer from fields.
- The game offers a click from any distance and then walks the player over (`EntityController.WalkOverAndInteract`
  with `PlayerController.SetGoal`, acting within 0.4 m of the place). `EntityController.MaxReachDistance` is 3 m.
  A click action of the mod's does the same: act if the player is within reach, else `SetGoal(place, onReached)`.
- Something added to a pooled object must come off again, so the bus has no component: a box of the director's is
  put where the bus stands.

## 12. Diagnostics

With "Developer diagnostics" on, one session's Player.log shows whether everything was found:

- `Game UI: app 'RealLifePlanner' is on the phone. Button copied from ...; place 9 of 9 apps, 2 page(s); icon AppIcon-Planner.`
- `Game UI: 12 of 13 of the game's pictures found among N (first one missing: ...).`
- `Planner: opened. note 2, sample off, tax 1, work 3, ...; 9 rows; 1.8 ms.`
- `Planner: reminder sent for work 14 ('Shift: ...', Tomorrow, 09:00 to 13:00).` and `... not sent: its entry is Done.`
- `Home: 'Click to cook' added to the game's click actions (12 in the list).`
- `Transit check (pointer): the bus stops' click actions are in the game's list (12 entries): PASS.`
- `Transit check (stop panel): at the M1 stop at ..., 2.1 m from the pole (...): the game's own panel is up, bus: 95 s (Timetable): PASS.`
- `Transit check (stop list): the game's list is open at ...: 31 stops, 2 further along this line and 29 with a change of bus ...; 4.2 ms: PASS.`
- `Home check (offer): at home with 1 cooker(s), 1 coffee machine(s) and 1 fridge(s): click actions on, the fridge panel's button in: PASS.`
- `Home check (cooking list): the game's list is open: 1 fridge(s), 1 cooker(s); food by kind: ...; 3 dish(es) possible, ... 6 entries made so far; 3.1 ms: PASS.`
- `Home check (needs bars): built from the game's top bar: a copy of its accessories tab holding a copy of its three bars (1266 wide, ...): PASS.`
- `Garage check (bay): in a repair bay with ...: the game's panel has its 2 button(s) and 'Garage' on the game's confirm key, second line 'Due: service $120.00': PASS.`
- `Garage check (custom shop list): the game's list is open left of the middle with 7 entries (...); 2.8 ms: PASS.`
- `Car check (dash): 'SERVICE DUE' is in the game's vehicle panel, after its condition reading, in the game's orange: PASS.`
- `Panel check (stop, beside a building box): moved to the left of it by 212 pixels: PASS.` and `... back over its own place: PASS.`
- `Gauge check (fishing): the game's bar, 0.42 (a marker, the zone about 0.72): PASS.`
- Every fallback is one `Game UI: ...` warning, logged whether or not diagnostics are on.

## 10. The HUD, and a panel on it (`Navigation/NavHud`)

The HUD is not one canvas. `Canvases` (the object with `UIs`) holds, among others, `Topbar`, `Tasks`, `PlayerHUD`,
`Smartphone` and `ControlsHintUIController`: each an overlay canvas of order 2 with a scaler of 3840 x 2160 matched to
height (`NotificationsUI` and `BuildingResume` match width). The game's "interface zoom" divides every scaler's
reference size (`UIs.SetUIZooming`), so sizes below are in reference units and everything scales together.

| what | where | size | notes |
|---|---|---|---|
| top bar | `Topbar/Container`, top, full width | 183 high | |
| task panel | `Tasks/Container`, top left, 320 below the top | 740 wide; `Panel` as high as its list, 1,000 at most | `TasksUI.IsCollapsed`: folded away, its button stays at the left edge |
| control hints | `ControlsHintUIController/Container`, bottom left | 740 wide; `Panel` as high as its content | the object is inactive when hints are off, in the pause menu and in the options |
| phone | `Smartphone/Container`, bottom right | 628 x 1,230 | the game switches the whole object off with its HUD (`UIs.HideUiMiniMenu`, `HideUiInteriorDesigner`) |
| address and neighbourhood | `PlayerHUD/CurrentAddressLabel`, `CurrentNeighborhoodLabel`, bottom middle | 600 x 100 each | |
| item panel | `PlayerHUD/ItemPanelUI/Panel`, bottom middle, from 140 up | up to 1,834 wide, as high as its content | while something is held or driven |
| job panel | `PlayerHUD/CurrentJobUI/Panel`, top right, 200 below the top | 715 x 460 | during one of the game's jobs |
| notifications | `NotificationsUI/Container`, top right, 237 below the top | 737 wide at scale 1.089 | a different scaler (matched to width) |

**The task panel is the model of a HUD panel**: `Tasks/Container/Panel` is an Image (sprite `darkgreybox@2x`, alpha
0.729), a `Headline` (sprite `darkgreybox-header@2x`, 100 high, with `Label`: TMP 32, bold capitals, dark, and `Icon`,
the arrows that fold it), a `Viewport` (Image `UIMask` with a `Mask`: the rounded window its list scrolls in), and a
scroll bar. A task entry's `Label` (`TasksUI.taskEntryTemplate`, public) is TMP 30, white; a group's label is the grey
(0.792, 0.804, 0.808). The control hints' panel is the same box.

`NavHud.Build` copies that panel asleep (section 2), destroys everything but `Headline` and `Viewport` and every
child of the viewport (the copy brings the live task entries with it), takes the game's scripts off, takes off the
`ScrollRect`, the headline's `Button` and every raycast target (the copy is looked at, never clicked: a HUD panel that
takes clicks would take them from the world under it), and adds a `Canvas` to the copy so that it is batched by
itself. The two text lines are copies of the task entry's `Label` with its `TextLocalizationComponent` taken off
(the words are final when written). The copy is switched off before it leaves the sleeping holder, so nothing wakes
until it is first shown. It is made a child of `PlayerHUD`, first in order (the game's dialogs draw over it), and so
goes with the game's own "hide the interface" (`ScreenshotController.ToggleUIVisibility` sets the alpha of the group
above it).

**Placing it.** No corner is always free, so the panel is placed by measuring: `RectTransform.GetWorldCorners` of the
control hints' `Panel`, the task `Panel`, the address label and the item `Panel`, brought into the HUD canvas's own
units. That needs no table of resolutions and follows the interface zoom. See `NavHud.Place`.

**The map's markers** (`PointOfInterest`, made by `CityMap.AddPoi` from `poiTemplate`; its parent is the object
`Pois`, an overlay layer in which a marker's world position is its place on the screen). A marker's round base and
its picture are private (`blobImage`; `buildingIcon` -> `BuildingIcon.image`), read by reflection once
(`NavMarks`). Public and useful: `CityMapFilters.playerIcon` / `playerFilterColor` (the player's own marker; the game
turns that icon by 45 degrees plus its direction on the screen, `PlayerController.UpdateCityMapPlayerIconRotation`),
`subwayStationIcon` / `subwayStationFilterColor`, `DirectionGuider.poiICon` / `poiBackgroundColor`,
`BuildingRegistration.GetPOIIcon()` / `GetPOIBackgroundColor()`, and the palette `GlobalReferences.colors`
(blue, midnight, lightGrey...).

**Drawing on the city map** is done as the game does it for its markers: an interface object under `Pois` given
screen positions from `Camera.WorldToScreenPoint`, again whenever the camera has moved (`NavCityMap`). The game is
paused while the map is open, and `GlobalEvents.onCityMapToggle` says when it opens and shuts.

**An Alpha8 texture in a `RawImage`** is drawn in the image's colour (the interface's shader adds white to an
alpha-only texture: `_TextureSampleAdd`), so one byte a pixel is enough for a picture of one colour. Inferred from
how the engine draws its own font textures; not seen in this game.

Proven: every object, component, size and member named here. Inferred: everything about how the copy looks and
where it ends up on screen.

## 13. The game's activity panel, for time that passes (`Jobs/ShiftSkip`, `Home/HomeActivity`)

`UIs.Instance.playerActivityUI` is `Canvases/PlayerHUD/PlayerActivityUI` (a draggable window in the middle of the
screen, 1,322 wide as authored):

```
PlayerActivityUI [PlayerActivityUI, DraggableWindow, VerticalLayoutGroup, ContentSizeFitter, CanvasGroup]
  Background        sprite Box-Dark
  Headline/Title    TMP 32, white
  Content
    ProgressionRow  ProgressBar (800 x 20, green fill) and ProgressionLabel (TMP 30)
    LabelsRow       LabelTemplate (TMP 28, bold), one copy per label
    SliderRow       the slider, its two key labels and SliderLabel
    SpeedOptionsRow TimeMachineButton (Button-Blue; its Label gets the suffix of SpecialInteract) and the fast-forward switch
    ButtonsRow      ButtonTemplate (button-gray@2x, Label TMP 38 capitals), one copy per button
```

- It shows whatever implements `PlayerActivity.IPlayerActivity`: a headline, labels, buttons (`ButtonInfo`, as
  section 9), whether there is a progress bar, a slider, the time machine and the fast-forward switch, and a state.
  `PlayerActivityUI.Show(activity)` opens it paused; the panel's `Update` reads the state every frame:
  MovingTowardsActivity (it hides itself and lets the clock run), Started (it makes it Running), Running (it calls
  `Perform(minutes)` as game minutes pass), Finished (it closes).
- `GameManager` sends Confirm, Interact, SecondaryInteract, Sell and SpecialInteract to it while it is open, and
  `CancelButtonHandler` sends Cancel: a button on any of those is run by its key. SpecialInteract is the time
  machine while the activity has one; SecondaryInteract is the fast-forward switch while it has one and the player
  is indoors (the switch is not shown out of doors).
- The time machine (`TimeMachine.StartTimeMachine(goal)`) clears the traffic, hides the HUD, blurs the view and runs
  the clock to `GetTimeMachineGoal()`; when it ends the panel calls `Perform` with the minutes that passed, then
  `Finish`. The player running out of energy, or the panel's Stop, also ends in `Finish`: settle there.
- The game's own employer job is `PlayerActivity.WorkActivity`: this is the model `ShiftSkip` follows.
- While the panel is open `PlayerActivityUI.IsPanelOpen` is true, which the mod's camera counts as a screen being
  open: the pointer is free and its buttons can be clicked.

## 14. The game's job panel on the HUD (`Jobs/ShiftCard`)

`UIs.Instance.playerHUD.currentJobUI` is `Canvases/PlayerHUD/CurrentJobUI` (class `UI.CurrentJob.CurrentJob`,
anchored top right at (-35, -200)); its public `panel` is the child `Panel` (714.5 x 460, inactive unless the player
has the game's employer job):

```
Panel [Image darkgreybox@2x, alpha 0.859]
  Headline            sprite darkgreybox-header@2x, 100 high; Text (TMP) 32, bold capitals, dark
  BusinessName        TMP 30, capitals, at y -132
  Address             TMP 30, capitals, at y -174
  TodayWorkStatus     TMP 30, bold capitals, at y -217
  TomorrowWorkStatus  TMP 30, bold capitals, at y -257
  SetDestinationButton, QuitJobButton
```

`ShiftCard.Make` copies the panel asleep, keeps `Headline`, `BusinessName` and `Address`, copies `Address` three
more times for five lines 42 apart, lets a long line shrink to 18 before it would be cut, takes every raycast
target off (it is looked at, never clicked), gives the copy a canvas of its own and hangs it on `CurrentJobUI`
itself, so it stands where the game's panel stands; while the game's own panel is showing the copy moves 476 down.
The lines are in capitals because the game's are. The game hides `playerHUD` during its time machine, and the copy
with it.

Proven: every object, size and member named in sections 13 and 14. Inferred: how the copies look.
## 15. A button in the panel the game opens on an object (`Home/HomeFridgeButton`)

A click on an object with no action opens the game's detailed panel over it (`Player.HUD.ItemInfoOverlays.DetailedOverlay`,
scene path `Canvases/PlayerHUD/ItemOverlayManager/DetailedItemOverlay`). It is one object with a part per kind of
content, each a script deriving `ItemInfoOverlays.IOverlay`; the part for a fridge or a wardrobe is:

```
FridgeDetailedOverlay [DecorativeItemHolderOverlay, VerticalLayoutGroup (spacing 10)]
  ConsumeButtons        VerticalLayoutGroup; ConsumeButtonTemplate (tagged UiTemplate; sprite Gradient-Blue-Round; Text TMP 24, capitals)
  EmptyFridgeButton     sprite Gradient-Yellow-Round; a saved click to OnEmptyFridgeClicked
  ChangeClothesButton   tagged UiTemplate; sprite Gradient-Blue-Round
  AddItemButton         tagged UiTemplate; sprite Gradient-Gray-Round
```

- `OverlayBase.UpdateOverlay` fills each part (`linkedController` is set, then the part's `UpdateOverlay`, then
  `SetActive`), and only then does `OverlayManager.ShowDetailedOverlay` switch the whole panel on. The part's own
  `UpdateOverlay` switches its own three buttons on and off and makes the item buttons again
  (`ConsumeButtonTemplate.ResetTemplate()`, which destroys tagged siblings of the template only: nothing at the level
  above it).
- So a button of the mod's is a copy of `ChangeClothesButton`, made asleep and untagged (`UiClone.Asleep`), its click
  replaced (`UiClone.Rewire`: the saved click points at the game's script), its words set through its own
  `TextLocalizationComponent`, put in the list under `ConsumeButtons`. A small component of the mod's on the list's
  object is enabled every time the panel is switched on, after it was filled: its `OnEnable` reads
  `linkedController` and shows or hides the button. Nothing runs between two openings.
- The game's own buttons of this panel close it first (`OverlayManager.HideDetailedOverlay`); so does the mod's.
- `OverlayManager.blockPlayerNavigation` is true while the panel is up and for a moment after. It stops a click on the
  floor from walking the player; it is not `PlayerController.NavigationDisabled`, and a walk order with a goal
  (`SetGoal`) is not stopped by it.

Proven: every object and member named. Inferred: that the copy lays out as one more row of the list.

## 16. More needs bars (`Home/HomeNeedsBar`)

`Canvases/Topbar/Container/Left Bar` (sprite `darkgreybox@2x`, 3,191 x 168) holds, left to right: `Avatar`,
`PlayerStats`, `Money`, `Date`, `Time`, `TimeControls`, with `Splitter`s between. `PlayerStats` (1,314 wide) has no
layout group: `Energy`, `Hunger` and `Happiness` are placed by hand at 233, 661 and 1,087, each 412 x 56:

```
Energy [Image StatBarBackground, black at 30 %; Button (opens Persona); ButtonEffects]
  EnergyFiller   Image StatBarBackground, type Filled; the colour is the bar's (1, 0.63, 0.32)
  EnergyIcon     32 x 32, 12 from the left
```

- `StatBarBackground` is 412 x 56 with no border: a bar of another size is a squeezed pill. Keep copies at that size.
- `Topbar.UpdateStatsBars` writes `fillAmount` each frame only when it differs by more than 0.01.
- The bars have no words and no tooltip. Hunger is (1, 0.33, 0.40), happiness (0.45, 0.86, 0.44).
- There is no room in the row for more. Under the bar hangs `AccessoriesPanel` (anchored to the bar's bottom left,
  521 x 100 at x 267, centre 38 below the bar's edge; the game shows it from the backpack button and keeps that in
  the save): the game's own form for something more under the top bar.
- So three more bars are a copy of `AccessoriesPanel`, emptied, holding a copy of `PlayerStats` (same spacing, same
  size), hung under the game's bars, and beside the accessories tab while that is open. The copies lose their
  `Button` and every raycast target. As children of the top bar they follow it: `Topbar` switches its own object off
  while the full menu is open.
- Texts in the top bar are TMP 36, white (`Date/Label` has a `TextLocalizationComponent`: the one copied for a word).

Proven: the objects, sizes and members. Inferred: how the second tab looks, and which pictures are in memory
(`Icon-Person`, `Icon-Building`, `Icon-Info-32` and `Icon-Address` are used by the UI scene; `Icon-Heart` is not).

## 17. A reading in the vehicle panel (`Transport/CarDash`)

While something is driven the game shows `Canvases/PlayerHUD/ItemPanelUI/Panel`; in its headline the object
`VehicleInfo` (`UI.ItemPanel.VehicleInfoPanel`, public as `ItemPanelUI.vehicleInfo`) is a horizontal layout (spacing
30) of `SteeringAssist`, `ParkingZone`, `Cargo`, `Fuel` and `Condition`. A reading is a horizontal layout (spacing 10)
of `Icon` (32 x 32, a `LayoutElement`) and `Value` (TMP 28 in the headline's dark colour, no localisation component).

- **The game shows the fuel**: `UpdateFuel` writes the tank as a whole percentage, orange from 50 and red from 30
  (`GlobalReferences.colors.orange`, `red`), and tints the picture with it. A gauge of the mod's would say it twice.
- `Initialize` switches `Fuel` and `Condition` on by the save's options and leaves other children alone.
- One more reading is a copy of `Condition` put after it: it is laid out by the game's group and comes and goes with
  the panel.

## 18. One more line and button in a panel the game shows itself (`Garage/GarageBay`)

At a filling station, a repair bay and the private driver's garage the game shows `UI.Overlays.GasStationOverlay` in
its box over a place (section 9). There is one such object, in the public field `OverlayUI.gasStation`, and the box
is taken while it is up, so a `WorldPanel` cannot stand there.

- The class is not sealed and the box talks to it only through `UI.Overlays.IOverlay`. A class that derives from it
  **and lists the interface again** (`class RepairBayOverlay : GasStationOverlay, IOverlay`) answers the interface's
  questions with its own `new` methods and can call the game's (`base.GetButtons()`); everything the game calls on
  its own object by name (`SetStationTrigger`, the repair, the wash) stays the game's code. Put in the field, it is
  what the game's static `GasStationOverlay.Show` shows.
- Change the field only while `OverlayUI.IsVisible` is false: the box hides an overlay by comparing references.
  Put the game's own object back when the feature goes.
- In a bay of the vehicle's kind, seated, the game's buttons are `RepairVehicle` (Interact) and `WashVehicle`
  (SecondaryInteract); at a pump `FillUpVehicle` and `BuyJerryCan`. The first button's `name` says where the car is.
- `GetButtons` is called when the panel is shown or refreshed and on every key the box is sent; the game's own
  makes new arrays each time. The mod adds one array of three and reuses its two `ButtonInfo`.
- The box is sent Interact, SecondaryInteract, SpecialInteract and Confirm (`GameManager`). **SpecialInteract also
  parks the car** (`ItemPanelUI.ClickAutoPark`, when that button is active), so a third button in a vehicle goes on
  Confirm.
- The header is written only when the box is shown or one of its events fires (entering or leaving a vehicle, a
  binding, vehicle variables): after changing what the second line says, show the overlay again.

Proven: every member named, and that the compiler accepts the re-implemented interface. Inferred: that the game's
runtime (Mono) dispatches the interface to the derived class's methods, as the C# language defines it.

## 19. A destination marker, and a colour (`GameUi.GuideMark`; the Garage's paint)

- `UI.Guiders.GuidersManager` has five guiders (Destination, MainQuest, SideQuest, JobDestination, PrivateDriver),
  each a `DirectionGuider` with a marker over the place, on the map, and an arrow while it is out of view.
  `SetGuiderTarget(position, name, sprite, colour, type)` marks a place that is no building; `ResetGuider(type)`
  takes it off; `DirectionGuider.target` is null while it marks nothing. Guiders are hidden indoors.
- Destination is the player's own and is saved: never take it for a moment's marker. The mod's jobs and outings use
  JobDestination; `GuideMark` takes it only while its `target` is null (or is this mark's) and resets it only if it
  still marks this mark's place.
- A colour is picked in `UIs.customColorPicker` (`Canvases/CustomColorPicker`, its own canvas of order 11):
  `Open(onColorChanged, initial)`, `onColorPick` for its button, `CustomColorPicker.isOpen`. Its Cancel calls back
  with the first colour; the cancel key closes it with no call; its button also adds the colour to the player's
  saved colours (`PlayerSettingsHelper.AddPlayerColor`).

## 20. More than two tabs over an app's table (`AppShell.SetTabCount`; the Finance app)

MyEmployees' tab bar (`Right/TableTabs`, a HorizontalLayoutGroup that sizes each tab to its label, padding 25 and 50)
has two tabs: the first with the sprite `white-box-top-left-corner`, the second with `white-box-top-right-corner`,
both sliced Images in white with a `Button` (colour tint) and a `Label` (TMP 28).

- A tab between them is a copy of the first with its Image's sprite set to none: a sliced Image without a sprite
  draws a plain rectangle in its colour, which is a tab with square corners. The copy is put between the two with
  `SetSiblingIndex`, and its click is replaced (`UiClone.Rewire`) like the others'.
- `AppShell.ShowTab` marks whichever is showing as MyEmployees does, over however many there are.
- The header cells of the table keep their objects for the app's life: `AppShell.SetColumns` can be called again to
  give a tab its own column titles (the widths stay, so the rows stay under their headers).

Proven: the bar's structure and both sprites. Inferred: how a tab without a sprite looks beside the two with one.

## 21. The bank: its terms, its dialogue, and what a mod can do without patching

- A bank is a building whose `SpecialService.settings` is a `SpecialServices.Bank.BankSettings` (a ScriptableObject):
  `maxTotalLoanAmount`, `annualInterestRate`, `yearsToPayLoan`, `allowSideQuestEmergencyLoan`. The city's banks are
  the entries of `BuildingHelper.SpecialServiceBuildings` with such settings; a bank's name is its
  `BuildingRegistration.BusinessName`. Branches may share one settings object.
- `Dialogs.BankDialog.LoanRequest` (the amount confirmed): under 500 is refused; the cap is `maxTotalLoanAmount` less
  what is owed **to that address**; `GetEconomyMaxLoanAmount` (private) caps it by means: the greater of wealth before
  loans (`PlayerHelper.GetPersonalWealth().WealthBeforeLoans`) and a quarter of `PlayerHelper.CalculateDailyIncome()`
  over the payback days, 15,500 at least while the tutorial's first-business loan is open, less every loan. Over the
  cap the answer is `dialog_bank_loan_maximum_exceeded` with the amount that is left, and no loan. With the side
  quest `SQBankruptcy` active at a bank that allows it, the cap is ignored and the bank lends up to what brings the money in hand to 15,000.
- `UI.Dialog.BankLoanSettings` (the input in the dialogue) reads the settings in `Start` and works out
  `dailyInterest` and `dailyPayment` with `LoanHelper` each time the amount changes; the loan is made with those.
- So the terms can be changed by writing the two fields on the settings object, and a cap of 0 makes the game
  refuse. The object is an asset: it outlives a city load and is not in the save, so whoever writes it keeps the
  first values seen and puts them back (`Economy/Lending/BankTerms`).
- A dialogue has no event when it opens. `DialogController.current` is the open dialogue's controller (the counter's
  `DialogUI`, or `ContactsApp` for a call) and its public `dialog` is the `Dialog` object, a `BankDialog` at a bank.
  The game is paused during a call from the phone, so a timer that looks for it must run on the real clock.
- `LoanHelper.RunDaily` reads `BuildingHelper.GetBuildingRegistration(loan.bankAddress).BusinessName` for every loan:
  a loan whose address is no building throws in the game's daily run. A loan made by a mod must carry a real bank's
  address.
- `LoanHelper.RecalculateLoanPayments` rewrites every loan's daily interest and payment from its bank's settings when
  `gameVariables.bankInterestMultiplier` is changed (`SaveGameManager`, where the game's variables are applied).
- `LoanEntryUi` (EconoView) clamps a daily payment between `LoanHelper.CalculateMinimumDailyPayment` (half the
  bank's standard payment, 5 at least; 0 for a bank without settings) and the balance, and pays off through
  `HudConfirm`; a loan paid off there raises `ba:gameevent_loanpaid`, a new loan at the bank `ba:gameevent_newloan`.

## 22. The day's order, and the game's money figures

- `GameManager.NewDay`: the day number goes up, **`GlobalEvents.onNewHour`**, real estate, wages, the daily
  run (`BusinessHelper.RunDaily`: rent, licensing fees, `LoanHelper.RunDaily`, marketing, the day's financial
  summary), then **`GlobalEvents.onNewDay`**, then tax and the investment funds. A handler on the hour event
  therefore sees the state just before the daily run and one on the day event just after it. The daily run books its
  transactions on the day that ended (`Day - 1`).
- Every change of money goes through `GameManager.ChangeMoneySafe(amount, TransactionInfo, day, address, force)` and
  is kept as a `Transaction` in `SaveGameManager.Current.Transactions` (a queue): type (a locale key), categories
  (a list, often empty), amount, `timestamp.Day`, the balance after it, and `transactionData` (the arguments of the
  type's text). The queue is cut when it has more than 1,000 entries, and then only of entries over six days old.
- EconoView names a type by `<type>_label` in its filter and writes a row with the type's own text filled from the
  data. The game has eleven categories (`ba:transactioncategory_rent`, `_salaryexpenses`, `_loanexpenses`,
  `_marketing`, `_licensingfees`, `_parkingfees`, `_salaryincome`, `_healthinsuranceexpenses`,
  `_headhunterreplacementfees`, `_negativeinterestrates`, `_casino`); most transactions have none.
- `SaveGameManager.Current.financialSummaries` has one `FinancialSummary` per day for a year (made in the daily
  run): business statements, loan expenses, wages of unassigned staff, `totalProfit`.
  `PlayerHelper.CalculateDailyIncome()` is the mean `totalProfit` of the last seven.
- `PlayerHelper.GetPersonalWealth()` (Persona's figure): `cash`, `totalInvestments`, `totalLoans`, `totalAssets`
  (vehicles and boats at list price, real estate at its purchase price); `CurrentWealth` and `WealthBeforeLoans`.
- Money is written `amount.ToShortCurrencyFormat()` on the loan pages (whole units in the game's currency format;
  `true` abbreviates from 10,000) and `amount.ToCurrencyFormat()` elsewhere (with decimals).

## 23. Typing an amount

`UIs.textInputUi.Show` (section 5) is a line of text: it has no mode for numbers, and its field is private. The game's
own amount fields (`UI.Components.InputField` with an integer content type, as in the bank's dialogue and on a loan's
row in EconoView) are parts of those screens, each wired to its screen's script. The Finance app therefore offers a
previous/next selector over round amounts (section 2's selector) and, for any other amount, the game's text box, with
what was typed read as digits and checked against the least and the most.

## 24. Lists in the table, one page in the box (the Finance app's Accounts and Borrow)

The white box (`InfoBox`, section 2) is a vertical layout with no scroll view: what does not fit is cut off. The
table beside it is the game's scroller and takes any number of rows. So a tab that has a list of things with actions
puts the list in the table and shows in the box only the row that is picked:

- A row that can be picked sets `FinanceRow.Pick`; `BaTable.allowRowSelection` and `onRowSelected` do the rest
  (section 2), and `BaTableCellView.VisualizeSelected` colours it. Rows of different kinds share one table by their
  id's prefix (`acct:`, `loan:`, `product:`, `lender:`).
- The box's page for a row: a title, a few lines, at most three selectors, and one or two rows of buttons. A row of
  buttons has the game's three kinds and no more (`InfoBox.Positive`, `Negative`, `Additional`): a fourth action is a
  second `ButtonRow()`. Lines added after a `ButtonRow()` come under it, so the row is asked for last.
- A fifth tab is `AppShell.SetTabCount(5)` (section 20): three copies of the first tab between the two the game has.
- An amount that is not on a selector is typed in the game's text box (section 23). Anything that costs or cannot be
  undone goes through `HudConfirm.Show`; its last argument makes the confirm button the game's red one.

Proven: the members. Inferred: that five tabs fit the bar, how many lines the box holds before it cuts off.

## 25. A conversation with three answers, and one opened without a call

- A `DialogEntry` has three buttons at most: `OnConfirm` (its text `ConfirmTextOverride`, a localised holder, so it
  can carry a figure), `OnSecondOption` (`SecondOptionTextOverride`, a key: no figure) and `OnCancel`
  (`CancelTextOverride`, a key). Leaving `OnSecondOption` unset hides the second button. More than three choices are
  a chain of entries (the private lender offers its sums one at a time; debt advice has a second page).
- `OnCancel` is an `Action`, not an entry: a cancel that has to do something does it and then calls
  `DialogController.current.CancelDialog()`.
- A conversation is opened without a call by `UIs.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog)`,
  as the game's counters open one (the taxi and the legal contact do the same). A notification's click can call it,
  so a feature's conversation still works with "Phone contacts" off. It is not opened while another one is on
  (`DialogController.current != null`) or the game's question box is up.
- A contact made later in a save is made the same way as one made at load (`Contact.GetContact(..., create: true)`),
  and its `callDialogTypeOverride` is set again on every load: the game does not save it.

Proven: the members. Inferred: how the game's panel lays out three long button texts.

## 26. What the game sells, and what a mod can tell of a purchase

- **Vehicles**: `ShowcaseVehicleController.Purchase`, `ContractVehicleForSale.Purchase` and
  `VehicleDeliveryHelper` charge the whole price with `ChangeMoneySafe` (no force: no cash, no sale), make a
  `VehicleInstance` with a new `id` and book `ba:transaction_vehiclebought` with `vehicleName` (the type's name) in
  its data. Only two of the three raise `ba:gameevent_purchasecompleted`, so a mod that has to know of every
  purchase compares `GameInstance.VehicleInstances` with what it knew.
- A vehicle is sold from its panel (`ItemPanelUI`): `ba:transaction_itemsold` with `itemSoldInfo` (the type's name),
  then `VehicleInstance.Delete(controller)`, the same call that removes any vehicle of the player's.
- **Buildings**: BizMan's presentation buys one outright if the cash is there (`Money < price` refuses):
  `ba:transaction_buildingbought`, a `RealEstate` in `GameInstance.realEstate` (`address`, `purchasePrice`,
  `purchaseDay`, `DailyIncome`). `RealEstateHelper.RunDaily` pays its rents in and, for a building put up for sale,
  sells it to a competitor (`ba:transaction_buildingsold`, `address` in its data).
- Cash may go under nothing: `ChangeMoneySafe(..., force: true)` does not test it (the game's own loan interest is
  taken that way).
- A business's daily figures are in `GameInstance.financialSummaries` (one `FinancialSummary` per day, each with a
  `BusinessIncomeStatement` per address: `TotalSales`, `TotalProfit`), which is what EconoView's business page reads.

Proven: all of it, from the game's code. Not seen in a running game.

## 27. A page inside a tab, and what the game computes about prices, customers and districts

**A page that is no tab** (the Finance app's "The city"). The white box does not scroll and the tab bar's width is
not known, so a sixth subject is a page of an existing tab, opened by a button in that tab's box: the same table and
box are written again with other rows and other column titles (`AppShell.SetColumns` again, section 20), and
`AppShell.ShowTab(-1)` leaves every tab clickable and unmarked, so the tab the page belongs to takes the player back.
A button in the page's own box does the same.

**What the game computes, and where a mod can reach it without patching**

- **Districts.** `NeighborhoodHelper.NeighborhoodsData` is every `NeighborhoodData` (a ScriptableObject):
  `neighbourhood` (the id, also a locale key), `workingClassPercentage`, `middleClassPercentage`,
  `upperClassClassPercentage`, `realEstateMultiplier`, `baseBuildingPricePerSqm`, `customerDemandsWeight`,
  `marketingStrength`. `ba:neighborhood_global` is the city as a whole. A business's district is
  `BuildingRegistration.Neighborhood`; the one the player stands in is `NeighborhoodHelper.CurrentNeighborhood`.
- **What customers pay.** `CitizenHelper.averagePriceIndicesInNeighborhoods` (a public dictionary, filled once) is
  1.2, 1.4 and 1.7 weighted by the class shares. `ItemHelper.CalculateOptimalPriceByNeighborhood` is the item's
  `DefaultMarketPrice` times it (plus 0.3 for a product only the player sells there); a customer of a class accepts up to
  `CalculateMaxAcceptablePrice`. The dictionary also sets the prices rival shops are given
  (`CompetitionHelper`), which are saved: read it, do not write it.
- **A business's prices** are `BuildingRegistration.retailPrices` (`itemName`, `price`); an item with no entry sells
  at its default market price (`ItemHelper.GetPrice`).
- **How many customers come.** `CustomerEntriesCalculator.GetCustomersByHour` is the building's capacity times
  `baseCustomerPromotionMultiplier + 0.75 x promotion / 100` times the business type's day and hour factors, where
  the promotion is `BuildingRegistration.promotion.total` for the player (the building's traffic index plus
  marketing, 0 to 100). `BusinessHelper.RunDaily` writes that figure afresh (`UpdatePromotion`) and then draws the
  day's customers for every business of the player's, both before `GlobalEvents.onNewDay`. So a change to the figure
  made on the day event needs the customers drawn again (`CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness`,
  which throws away those still to come and draws them from the figure as it stands).
- **Wholesale and import prices.** `ItemHelper.GetWholesalePrice(item)` is `item.wholesalePrice` (a public field of
  the item's asset) times the market's `importPriceIndex` for it and `gameVariables.marketPriceMultiplier`; the shelf
  sign, a purchase, `DeliveryContract.TotalPricePerDelivery` and the importer all call it when they need a price, and
  multiply by the seller's own price index and by the game's own shortage or backorder multiplier
  (`ProductMarketHelper.GetProductMarketEventMultiplier`). Items have no category: what kind of goods one is can only
  be told from the business types that list it (`BusinessType.businessProducts`).
- **The day's money.** Rent is `ba:transaction_rent` with the category `ba:transactioncategory_rent`, wages carry
  `ba:transactioncategory_salaryexpenses`, a business's takings are `ba:transaction_revenue`; all three are dated on
  the day that ended and are in the transaction queue when the day event comes (section 22). A purchase
  (`ba:transaction_itempurchase`, `_deliverycontract`, `_importdelivery`) names the seller and never the goods.
- **Property.** `Building.GetMarketValue()` is the size times the neighbourhood's price per square metre, the
  building type's multiplier and the traffic index; the same inputs set the market rent the game saves on a building.

Proven: all of it, from the game's code. Not seen in a running game.
## 28. Starting something from an app, and what the Ventures app read from the game

- **The phone pauses the game.** While any app is open `uis.gameSpeed.Paused` is true, so `LifeEvents.IsSafeForEvent`
  (and `EconomyBridges.Held`, which asks it) says "not now", and `ThirdPersonCamera.IsUiOpen` says a screen is up. A
  feature that starts something from an app's button must not ask those: Ventures asks the game's own holds one by
  one without the pause (`VenturesGame.Busy`: a conversation, the time machine, an activity's panel, the subway, the
  casino boat, the way to hospital, the wheel of a vehicle: `GameManager.selectedVehicle`) and the mod's through a
  bridge of its own.
- **A list window from an app.** The list (section 10) stands on the HUD's canvas, which `FullMenu.Toggle(true)`
  hides. So the menu is closed first (`FullMenu.IsOpen` is static; `uis.fullMenu.Toggle(false)` is what
  `BizManBusiness.SetDestination` calls), then the list is taken, filled and opened (`Ventures/PitchMeeting`).
  `Toggle` does nothing but close the question box while `HudConfirm.isOpen`, so it is not called from a page that
  has one up.
- **One button of a kind to a row.** `InfoBox.Button(row, kind, ...)` shows the row's own button of that kind: two
  calls with the same kind on one row are one button with the second's word and click. A second action of a kind
  is a second `ButtonRow()`. Lines added after a row come under it.
- **Pages in a tab.** A tab that needs more than the white box holds keeps a page number beside the picked row and
  writes the box and the table from it; "Back" is an Additional button (Ventures' Raise money: the business, its
  kind and budget, the investor).
- **A business's day.** `GameInstance.financialSummaries[n].businessIncomeStatements` has `TotalSales` and
  `TotalProfit` per address and day; a business's costs of a day are the difference. A day the business has no
  statement for (it did not exist, or the summary is older than a year) is simply absent.
- **`BuildingRegistration.creationDay`** is written by `AddToPlayer`: the day the player took the building, not the
  day a business got its type. "The player has opened a business of this kind" is therefore told by counting the
  businesses of the kind (`Books.Businesses`), not by that day. `GetDisplayName()` is the business's name or, with
  none, its address.
- **The kinds of business the player may start**: `BusinessTypeHelper.GetAllPlayerAvailableBusinesses()` (types
  tagged `allowplayercreation`). A type's name is its id localised: `"ba:businesstype_webdevelopmentagency"`,
  `_graphicdesigner`, `_marketingagency`, `_lawfirm` are the office kinds.
- **The game's rivals**: `RivalsHelper.GetSpecialRivals()` (the list may be null before the rivals have loaded) with
  `rivalData.rivalName`, a plain name; `RivalsHelper.IsFeatureEnabled` is false in a save with rivals off.
- **The game's lettings.** `RealEstateHelper.RunDaily` moves each owned building's `occupancy` by the rent set in
  BizMan against the market rent and pays `RealEstate.DailyIncome` in (`ba:transaction_rentrevenue`, `address` in
  its data, dated the day that ended). `FinancialSummary.realEstateStatements` keeps that amount per building and
  day for a year, where the transaction list is cut after six days. `Building.GetMarketValue()` is the value BizMan
  shows. A Hamptons house is skipped by the run: it earns nothing.
- **The game's investment funds** (`EconoViewInvestments`, `InvestmentFundHelper.RunDaily`): each fund grows by its
  own yearly percentage spread over `daysPerYear`, with deposits and withdrawals in EconoView. Nothing of the mod's
  touches them.

Proven: every member named, from the game's code. Inferred: that a list opened in the same frame the menu is closed
shows at once (the HUD's alpha is set back by `Toggle` itself; the menu fades out over a tenth of a second).

## 29. A second app on Finance's table, and what the game keeps about staff, tax, marketing and opening hours

**An app whose table is another app's** (the Company app). `FinanceTable`, `FinanceRow` and `FinanceCellView` are the
game's own `BaTable` with four cells (section 24); a second app adds them to its own copy of MyEmployees
(`AppShell.Make`) exactly as Finance does and prepares its own row the same way. Two apps can share the row class
because each has its own copy of the row and its own list. A row's id is also what is "picked": the page in the white
box is chosen by it ("emp:" and an employee's id, "pol:" with a place and a kind), so a notification can open the app
on a tab with one thing picked (`CompanyApp.Want`). A tab with two subjects (Tax and licences) is two writers of the
same table, one after the other, and one page in the box.

**Three answers** are the three kinds of button of one row (`InfoBox.Positive`, `Additional`, `Negative`): agree,
offer less, refuse. A row has no fourth, so a page with three answers puts its Back in a second row.

**A switch in the box** is `InfoBox.Switch` (MyEmployees' own switch row); where the game's row could not be had it
returns false and the page shows a button that does the same.

**What the game keeps, and where a mod can read it without patching**

- **Staff.** `SaveGameManager.Current.EmployeeInstances` is every employee. `id` is a stable text id
  (`EmployeeHelper.GetEmployeeById`). `hourlyWage`, `satisfaction` (0 to 100), `characterData.skills` (the first is
  the main one: `name`, `value`), `assignedAddress`, `assignedWeeklyHours`, `assignedWeeklyDays`, `isAbsent` (off
  sick today: `EmployeeHelper.RunDaily` decides it each night from `nextSickDay`, and somebody absent is paid
  nothing), `workedHoursToday` (counted each hour, cleared in the daily run after wages are paid). The wage the game
  itself would offer is `EmployeeHelper.CalculateHourlyWageForSkill(skill)`. All of it is saved: read it, do not
  write it. The player's own employee instance is `PlayerHelper.GetPlayerEmployeeInstance()`.
- **What staff ask of the player** is `demands` (`Entities.Employee.JobDemands`: a clean workplace, days and hours a
  week, free days, health insurance, an item in the building, no shift between certain hours, the player's
  happiness). Satisfaction moves towards how many are met; under 10 they resign. There is no demand about pay
  against a market, and no overtime pay.
- **Poaching.** `EmployeeInstance.PoachByRival(rivalId, days, percent)` is the game's own offer, answered in the
  employee's messages; accepting writes the wage. A rival's name is `rivalId.GetRivalName()`.
- **Tax.** `TaxHelper.RunDaily` bills once a year, on the day the year turns: `gameVariables.taxPercentage` of the
  businesses' `TotalSales` (from `financialSummaries`) and gambling winnings, less
  `currentTaxPeriodDeductibleExpenses`, if the total passes `TaxHelper.MinimumIncomeForTaxes`. A transaction made
  with `TransactionInfo.SetTaxDeductibleName(name)` is added to that list by `GameManager.ChangeMoney` itself
  (`TaxHelper.TrackTransaction`); a name that is no locale key is shown as it is, which is how the game passes a
  business's name. `TaxHelper.MeetsIncomeTaxThreshold` and `TaxCalculationHelper.GetBusinessSales(from, to)` are
  public.
- **A day's figures for one business.** `financialSummaries[i].businessIncomeStatements` has `TotalSales`,
  `TotalProfit` and `Address` for each business and day; it is made from transactions by their category, so a
  transaction with no category is in neither. Customers by day are `BuildingRegistration.orderHistory`
  (`dayNumber`, `totalCustomers`, `totalRevenue`).
- **The game's own marketing** is `BuildingRegistration.marketingCampaigns`, paid daily
  (`MarketingHelper.RunDaily`, category `ba:transactioncategory_marketing`); it sets `promotion.marketing`, and
  `promotion.total` is the traffic index plus that times the neighbourhood's `marketingStrength`, 100 at most.
- **Opening hours** are `BuildingRegistration.scheduleDays`: for each day `isOpen` and `openingHourSlots`
  (`startingHour`, `endingHour`).
- **Business types** are told by name (`businessTypeName`, "ba:businesstype_fastfoodrestaurant"); there is no tag
  for food or alcohol.

Proven: all of it, from the game's code. Not seen in a running game.

## 30. A bar (`GameUi.GameBar`)

The game's bar is the needs bar of its top bar (section 16): `Canvases/Topbar/Container/Left Bar/PlayerStats/Energy`,
412 x 56, an Image with the pill `StatBarBackground` at 30 % black and a child `EnergyFiller`, the same pill as a
filled Image. The game has no other bar that is written while the game runs: the activity panel's progress bar is
written once a game minute and the panel pauses the game (section 13).

`GameBar.Make(parent, name)` copies `Energy` asleep, takes its `Button`, its hover script and its picture off, sets
it to the game's size in the middle of `parent`, takes every raycast target off and gives it a canvas of its own. It
shows one of two things:

- **A quantity**: `Fill(value)` writes the filler's `fillAmount`, as the game does for its needs (a clock running down).
- **A timing**: `Zone(centre, width, bestWidth)` marks a zone on the empty bar and `Mark(value)` puts a marker on it.
  The zone, its better part and the marker are the filler's own picture three more times, whole (`Image.Type.Simple`),
  placed in the filler's rectangle by shares of its width: the zone in the game's green at 45 %, the better part in
  full green, the marker white, 12 wide and 8 higher than the bar above and below. A stretch of the pill is a rounded
  band, which is why nothing has to be drawn.

- **When it is written.** `Topbar.UpdateStatsBars` writes a bar only when it differs by more than 0.005 (energy) or
  0.01, and always when it runs empty. `GameUi/BarRule.cs` is that rule with the finer step, for the filler and for
  the marker: `Fill` and `Mark` may be called every frame and write one number to one object on a visible step, and
  nothing at all while the value stands still. No text is made and nothing is laid out.
- **Where it stands.** `ShiftCard.Bar()` gives the card's own bar, in the place of its last two lines (measured from
  the lines), made the first time it is asked for. The leisure meters use it (`Leisure/LeisureScreen`): a cast and a
  pull on the line while fishing, the beat on a dance floor, the chess clock.
- Keep it at 412 x 56: the sprite has no border.
- With "Developer diagnostics" on: `Gauge check (fishing): the game's bar, 0.42 (a marker, the zone about 0.72): PASS.`

Proven: the objects, sizes and members. Inferred: how the zone and the marker look on the bar, and where the bar ends
up on the card.

## 31. Options that do nothing for a while, and a row that is not always there (`Meta/Features`)

The mod API has no way to grey a row out or to leave one off. The rows are the game's own objects, though, made
again each time the page is drawn, and `OptionsLayout` is told as each is made (its `RowTap`):

- `Feature.StandsAside(why, optionIds...)`: while `why()` answers a sentence, every `Selectable` of those rows (the
  game's toggle and slider) gets `interactable = false`, which is the game's own look for a control that cannot be
  used, and the section's button says the sentence after its name: "Camera   (12 options, on, unavailable: the camera
  is with ...)".
- `Feature.Present(optionId, present)`: while `present()` is false the row is switched off and kept off for that
  drawing, whatever section opens or search finds it, and is not counted in "5 options". The option keeps its value.

Both are asked when the page is drawn, never per frame. See [other-mods.md](other-mods.md) for what uses them.
