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

## 7. An action on an object in the world (`Home/HomeCookOffer`)

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
(`HomeCookOffer.Remove`). `ShouldShow` is asked for whatever the pointer is on: answer with a type test and a
look-up, nothing dearer.

Do not take over an object whose click already opens a panel of the game's. The fridge is one: its call to action is
"Click to use" with no action, and the click opens `DecorativeItemHolderOverlay` (what is in it, "empty", "add").
An action there belongs in that panel as a button (its `emptyFridgeButton` is the template to copy; the panel's
`UpdateOverlay` rewrites its buttons each time it shows). That is not built yet, which is why a home with a fridge
and no cooker still gets the mod's key prompt for cooking.

The game shows no key hints on objects (`ActionKeyTooltip` is a tooltip on HUD tools), so a mod's key prompt has no
native form: prefer the click.

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

## 9. Diagnostics

With "Developer diagnostics" on, one session's Player.log shows whether everything was found:

- `Game UI: app 'RealLifePlanner' is on the phone. Button copied from ...; place 9 of 9 apps, 2 page(s); icon AppIcon-Planner.`
- `Game UI: 12 of 13 of the game's pictures found among N (first one missing: ...).`
- `Planner: opened. note 2, sample off, tax 1, work 3, ...; 9 rows; 1.8 ms.`
- `Planner: reminder sent for work 14 ('Shift: ...', Tomorrow, 09:00 to 13:00).` and `... not sent: its entry is Done.`
- `Home: 'Click to cook' added to the game's click actions (12 in the list).`
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
