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

The phone takes apps once `FullMenu.Start` has made the game's buttons, at the load, whether the menu shows or not
(`PhoneApp.PhoneUp`). `Install` itself only checks the name against the names of both phones' buttons (read in one
search per load, `PhoneNames`) and puts the app in a line (`InstallQueue`); the full menu's `PhoneAppQueue` puts one on a
frame, its buttons hidden and the pagers held (`PhoneGridPager.Holding`), and after the line's last shows them all and
has each pager lay its grid out once. The app it hands back counts as installed at once; opening it before its turn puts
it on there and then. With Developer diagnostics on, "Phone apps check (paced install): ..." gives the frames and the
slowest install.

Proven: every member named above, and both templates' structure. Inferred: that a copy made after `Awake`/`Start`
lays out as the ninth child (it is an ordinary child of a layout group).

### Pages in the full menu's top bar (`PhoneGridPager.OnStrip`)

The full menu's row of app buttons (`Topbar/AppButtons`, a HorizontalLayoutGroup of spacing 20, upper centre, in a
box 1400 wide at x 1650 of the 3840 canvas) is centred for the game's own seven apps, which already run a little past
both ends of the box. Every further button grew it both ways, under the money, the date and the time
(`Topbar/RightData`) and off the screen. The same pager pages that row. A page has a set number of tiles
(`Core/GameUi/PagerRules`): as many as fit from where the game's own row begins (x 820) to the left edge of RightData's
own box (1350 wide, ending 100 from the bar's right edge: as wide as the money, the date and the time can ever be),
between two and the game's own seven. It is worked out at the first opening and again only when the bar's width
changes (`RowBarWatch` on `Topbar`): 6 tiles on 16:9 at 100%, 5 on 16:10, 2 on 4:3. (Until 0.26.x the room was
measured to the money's words at every opening, so the count and the row's length changed.) The game's own apps come
first in the game's order, then the mod's in one fixed order (`PagerRules.ModOrder`: the day, work and study, money,
business, then the Guide and the options; one Banks app for the three banks), then other mods' in the order
they stand (`PagerRules.Arrange`; buttons standing among the game's, or the mod's out of their order, are moved after
them in it). Each feature puts its app on when it is ready, so the mod's apps once stood in a different order after
each load; ContractCheck fails if an app of the mod's has no place in the table. A full page stands centred in that room (`PagerRules.PageStart`, worked out with the
count): 75 either side at 16:9, 3 at 16:10, 75 at 4:3. The pages fill in order, the apps and the More apps button on the last tile, only the
last page shorter; every page starts where the game's own row starts (the row's left padding, alignment upper left), so
the button never moves. The button is a copy of the template app button with the game's scripts that make it an app taken off (only
`ButtonEffects` and the badge kept), a light grey tile painted by `AppIconPainter` with the game's `Icon-Arrows` in dark ink
(a code-drawn double chevron if that sprite is not in memory), the name "More apps" and the page "2 / 3" under the
picture; it stands beside the row under `Topbar`, placed on the tile by hand, so neither the layout nor other mods'
pagers count it. It goes to the next page and from the last to the first; the wheel over the row or the button turns
the page and stops at the ends; opening an app shows its page. With one page it is hidden. The row's alignment and
padding are put back when only the game's own apps are left, when the pager goes or steps aside. With Developer
diagnostics on, each opening writes one line: where the row ends against where the money begins. Checked offline
(ContractCheck, PagerChecks); not seen in the game yet.

### Pages on the HUD phone (`GameUi.PhoneGridPager`)

A ninth button has no room, so the home screen gets pages: the game's eight fill one page; with more, seven apps and
the More apps button on the eighth tile (lower right) a page. A short page keeps the full page's left edge (the grid's
alignment set to upper left with the full page's padding while there are pages, put back after). The button's badge
(the game's own) counts the buttons on pages not showing whose own badge is up, the game's Contacts and MyEmployees
too (it listens to `SmartphoneUI.OnUpdatedBadgeCount`). Every child the layout places is paged, other mods' buttons too
(Going Public's Business, Chat and Hub), so none is laid out under the button. It runs on `OnEnable`,
`OnTransformChildrenChanged`, the button, the box's change of size and the scroll event only.

Other mods page this grid as well (studied, not copied: BizSuite's `PhoneAppsPager` puts a component on the grid and
shows or hides every child but the template; AlunaPhonePages pages every child that has a `SmartphoneAppButton`,
and re-reads the grid when its child count changes). Two pagers would undo each other, so this one **steps aside
for good** as soon as it sees a component on the grid from an assembly that is neither the game's nor the mod's, or
finds a button it had hidden shown again. It then shows everything it hid and logs one line. For that to work the
mod's button stays what those pagers look for: a real `SmartphoneAppButton`, a direct child of the grid, with no
"Template" in its name. The More apps button is not in the grid, so those pagers never see it; when this pager
steps aside it takes the button away.

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
  or property of the model (it then orders the rows by that field through reflection and `OrderBy`). The mod's
  header cells are named `Column0`, `Column1`... so that this takes no part: its tables are ordered by their own
  model (section 30).
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
- The game's cameras look down from far off (`PedestrianCam`: on foot 35 m from the player, indoors 13 m with a lens of
  49, turned only round the player; indoors that is the floor from about 5 m behind the player to 10 m ahead), and
  indoors the game hides its guiders (`GuidersManager.ShouldGuidersBeHidden`), so a box over a place further off is
  drawn off the screen and nothing says where to go; the box stands up from its point (pivot 0.5, 0), so one over a
  place 5 m ahead is already mostly above the top. A `WorldPanel` with `KeepInView` (every job's panel) is handed the
  point at the picture's edge toward such a place, low enough for the whole box, worked out in the camera's own space so
  a place behind the lens is not mirrored (`GameUi/PanelEdgeRules.cs`, the rule; `GameUi/WorldPanel.View.cs`, the part
  that asks the camera; a few dot products a frame while the box is up).
- Each button's text gets the suffix `PlayerAction.AsSuffix()` (" [F]": the player's own binding), and
  `GameManager` sends Interact, SecondaryInteract, SpecialInteract and Confirm to
  `OverlayUI.OnPlayerActionPressed`, which resets the action and runs the button's `onClick` (in a vehicle
  SpecialInteract also parks the car: section 16). A button is also a
  real `Button`: a click runs it too. So the mod reads no key and draws no prompt.
- The game's bindings (`PlayerInput`): Interact F, SecondaryInteract P, SpecialInteract O, Confirm Enter, Cancel
  Esc; the Player map has keyboard and mouse bindings only. The UI map has a gamepad's south button on Click and
  its east button on Cancel, so with a gamepad a button of this panel is clicked, not pressed by its key.
- There is one box. The game uses it at filling stations, the private driver's garage and lifts, each shown by a
  trigger the player or the player's car has entered (`BuildingEntranceOverlay` exists but nothing shows it); a subway
  entrance, a door, a cash machine or a stand puts nothing in it. `OverlayUI.Show` simply replaces what is there, and
  the game's keys go to whatever is in it. Whoever owns a `WorldPanel` calls `Show` from its timer while it wants it
  up, and gives the panel its place with `For` (or `Follow` for a person); `Meet` marks a person the player came to meet.
- **Who has the box** (`GameUi/PanelPickRules.cs`, the rule; `GameUi/WorldPanel.Pick.cs`, the part that measures):
  a `Show` that finds another of the mod's panels in the box measures both places from the player. The nearer place
  the player faces has it (a place straight behind counts twice its distance); a person met counts nearest of all
  within 3.5 m; the one asking takes the box only when it counts nearer by 0.75 m or a fifth, whichever is more, so
  the box never goes to and fro. The one put out asks again at its owner's next look and takes the box back as soon
  as the player turns or steps to its place. The game's own panels keep the box: the player stands in their place,
  and the game has no way to be asked for it back. One line, `Panel check (who has the box)`, says each new outcome.
- The places where the mod's panels over a place are offered (subway entrances, stops, bike places, kiosks) are named
  to `GameUi/PanelPlaces.cs`, and somebody put out to wait for the player stands clear of their reaches where the
  street allows (`SocialPlaces.StandingClear`).
- `OverlayUI.IsVisible` is static. It must not be left true when the city unloads (`WorldPanel.Hide` sees to it):
  the mod's second interact key (E) stands aside while it is true.
- Making the buttons again destroys and instantiates them (`SetUpButtons`), so a change of a line alone goes
  through the private `SetUpHeader` (a delegate made once) and only a change of the buttons through
  `RefreshButtons`.
- While a `WorldPanel` with a button on Interact is up, the mod's own interact key must do nothing else:
  `TransitDirector.TakesInteractKey` is true while it shows and in the frame its button ran, and the mod's own door
  and car key (`NearbyInteraction`) asks `WorldPanel.InteractFree()`, false while the box shows any panel's button on
  Interact, the game's own included.
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
- Its buttons (`SetButton`) get the `ButtonInfo`'s key alone: neither its `arguments` nor its `interactable` is read,
  so a key with a `{placeholder}` shows the placeholder, and a button is never dimmed and its key always runs it. Give
  an activity's button a key with no placeholder, and leave out a button that cannot be pressed now, as the game's
  own `StudyActivity` does once its course is done (`Careers/SchoolDesk.GetButtons`). Labels do read their arguments.
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

The game's notifications (`NotificationsUI/Container`, 237 below the top) stand in the same corner as its job panel
(200 below the top): the game has no rule for the two. While a copy shows, the notifications start 16 units under its
foot (`GameUi.NoticeRoom`, the arithmetic in `NoticeRoomRules`, checked in NavCheck's PolishCheck), and they are back at
the game's own place as soon as none shows. A `NoticeRoomMark` on each copy says when it comes on, goes off (its own
`SetActive`, the HUD hidden with the time machine or the map) or steps under the game's panel; nothing runs per frame.
With Developer diagnostics on, each move writes one `Notice check (room)` line.

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
  first values seen and puts them back (`Economy/Banking/Loans/BankTerms`).
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

The white box (`InfoBox`, section 2) is a card of a header, a scrolling body and a foot (section 36), so a page of any length is
reached by the wheel; the table beside it is the game's scroller and takes any number of rows. The
table beside it is the game's scroller and takes any number of rows. So a tab that has a list of things with actions
puts the list in the table and shows in the box only the row that is picked:

- A row that can be picked sets `TableRow.Pick`; `BaTable.allowRowSelection` and `onRowSelected` do the rest
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

**A page that is no tab** (the Finance app's "The city"). The white box's body scrolls but the tab bar's width is
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

**An app whose table is another app's** (the Company app). `DataTable`, `TableRow` and `DataCellView` (the kit's:
section 30) are the game's own `BaTable` with up to nine cells; a second app adds them to its own copy of MyEmployees
with one call (`DataTable.On`), exactly as Finance does. Two apps can share the row class
because each has its own copy of the row and its own list. A row's id is also what is "picked": the page in the white
box is chosen by it ("emp:" and an employee's id, "pol:" with a place and a kind), so a notification can open the app
on a tab with one thing picked (`CompanyApp.Want`). A tab with two subjects (Tax and licences) is two writers of the
same table, one after the other, and one page in the box.

**Three answers** are the three kinds of button of one row (`InfoBox.Positive`, `Additional`, `Negative`): agree,
offer less, refuse. A row has no fourth, so a page with three answers puts its Back in a second row.

**A switch in the box** is `InfoBox.Switch` (MyEmployees' own switch row); where the game's row could not be had it
returns false and the page shows a button that does the same.
`InfoBox.Toggle` is the Mod settings app's switch in the box: the game's settings menu switch row (`OptionRows.SwitchRow`,
`switch-off@2x` and `switch-on@2x`) with a name and a short line under it in the box's words and colours, the control's
column the switch's own width. It is made the first time a page asks and kept as the box's other parts are; where the
menu cannot be copied it returns false and the page uses `InfoBox.Switch` (the managers' task switches do so).

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

## 30. Tables that sort, filter and add up (`GameUi.TableSpec`, `TableModel`, `TableView`, `DataTable`)

Every table of Finance, Ventures, Company and the Planner is declared as data and gets its sorting, its filter, its
totals line and its "nothing here" sentence from the kit. A new table is a specification and the rows.

**How the game does each, and what the mod copies**

- **Sorting.** `BaTable.Start` gives the `Button` of a header cell a click if the cell's name is a field of the row
  class; `ChangeSortOrder` then orders the list (reflection, `OrderBy`), a second click on the same header turns the
  order round, and the cell's child `ArrowUp` (ascending) or `ArrowDown` is switched on. A header cell of MyEmployees
  is `[HorizontalLayoutGroup, Button, Image, ButtonEffects, LayoutElement]` with the children `ArrowDown`, `ArrowUp`
  (both inactive, sprite `DropdownArrow`, black) and `Title`. The title is white: its dark colour is the button's
  tint (the button's target graphic is `Title`, normal colour 0.282/0.327/0.424), so **a header cell without its
  button has a white title on the white header**. The mod keeps every cell as it is and gives the button its own
  click (`AppShell.ReadHeaders`); a column that does not sort has its button "not interactable" (the game's grey).
  The arrows are switched exactly as the game switches them (`AppShell.ShowSort`).
- **Filter.** EconoView filters its list of transactions with the game's drop-down (`UI.Elements.Dropdown`, "All
  types" first, no counts: `EconoViewFullTransactions.SetUpTypesDropdown`), and MyEmployees has one over its table
  (`Right/MassActionDropdown`, 581.9 x 80 at the top right, level with the tabs). That object is copied **with its
  scripts** (`Dropdown`, `DropdownOptionScrollerController`, `DropdownOptionCellView`): its references are all
  inside itself, it is driven through `SetOptions(list, localize, selected)` and `onOptionSelected`, and the game
  closes an open one on a click elsewhere (`GameManager` asks `Dropdown.currentDropdown`). `AppShell.TakeFilter`
  keeps it asleep among the templates; `FilterDrop.Make` copies it under the app's panel, one row above the tabs
  (the mod's apps have up to seven tabs, which fill the row MyEmployees has it in). MyEmployees' own filter panel
  (the funnel button, `EmployeeFilterPanel`) is a window of switches tied to `EmployeeModel`: not used.
- **Totals.** EconoView's income statement ends in a total (`TotalEntryPositive`, `TotalEntryNegative`, the last
  entries of its list): the mod's totals line is the last row of the table, in bold, and scrolls with it.
- **Nothing here.** EconoView writes a sentence in the list's place (`NoLoansLabel`, `NoInvestmentsLabel`): the mod
  writes one row with no background.

**Declaring a table**

```csharp
static readonly TableSpec Accounts = new TableSpec("Finance", "Accounts")       // app and tab: its name in Player.log
    .Column("reallife_econ_col_account", 480f, TableSort.Text, flexible: true)  // title key, width, how it sorts
    .Column("reallife_econ_col_owed", 230f, TableSort.Money, sums: true)        // summed in the totals line
    .Column("reallife_tab_col_due", 180f, TableSort.Date)
    .Column("reallife_econ_col_state", 330f, TableSort.Custom, right: true)     // a number where there is one, else the words
    .Filter("reallife_tab_all_kinds", KindKeys)                                 // "all" first, then the kinds (locale keys)
    .Order(2, true)                                                             // opens by column 2, ascending
    .Empty("reallife_tab_empty_debts");                                         // the sentence when a filter leaves nothing
```

- `TableSort`: `None` (the header is no button), `Text`, `Number`, `Money`, `Date`, `Custom`. A cell with nothing to
  compare comes last in both directions; equal rows stay in the order they were written.
- Up to nine columns (the header row has nine cells) and two filters. `.Filter(allKey)` without kinds is a filter
  whose kinds the app names each time it writes the table (`spec.Filters[n].Names`: districts, businesses).
- Widths: the table is about 2,345 units wide at 16:9, less 20 for each gap; one column is `flexible`.
- The specification object also keeps what the player last chose there (column, direction, kinds) until the game is
  closed. One static object per table; a tab and its pages each have their own.

**Writing the rows** (an app of the Finance family: `DataTable`)

```csharp
_table = DataTable.On(_shell, "Finance");          // in Build: the game's row made into nine plain cells
_table.Money = amount => FinanceText.Money((float)amount);
_table.SelectionLost = ClosePick;                   // the filter took the selected row out and left none

TableRow row = Row("acct:" + id);                   // (the app's pool; row.Reset(id))
row.Pick = true;                                    // a click means something
row.Kind = kind;                                    // for the first filter; Kind2 for the second
row.Set(0, name);                                   // words: ordered by them
row.Set(2, Money(owed), owed);                      // words and the figure the column is ordered and summed by
row.Set(6, state, gravity, TableRow.Red);           // a tone: Yellow, Red, Green (the game's)
_table.Show(spec, _rows, selectedId);
```

- `row.Info` is a line of words; `row.Pinned` a row that stays first whatever the order and the filter and is in no
  total; `row.NoTotal` a row that is ordered and filtered but not summed.
- A specification with no sortable column and no filter is a page laid out by hand: its rows stay exactly as written
  (the Company app's rota pages).
- A click on a header or a choice in the drop-down re-orders the same rows (`TableModel`: a stable merge sort on an
  array of row numbers kept for the purpose, no allocation after the first). The selected row stays selected and the
  scroller jumps to it; if the filter took it out, the first row is selected as a click on it would be
  (`onRowSelected`), and `SelectionLost` is called when none is left. `Filtered` is for an app whose box says
  something about the rows shown.
- An app with a row class of its own uses `TableView` directly, as the Planner does (`PlannerApp.ShowRows`): `Begin`,
  `Model.Set` / `Kind` / `Pin` for each row, `Apply`, then its rows in the order of `Model.RowAt`.

**If the game changes.** No header buttons or arrows: `AppShell.CanSort` is false, the titles stay and nothing
sorts. No drop-down, or an exception from it: no filter, everything is shown. Each is one `Game UI: ...` warning.

**Diagnostics** (with "Developer diagnostics" on)

- `Table check (Finance, Accounts): 7 columns, 6 sortable, filter with 11 kinds, totals: PASS.` once per table and
  session, the first time it is written.
- `Table (Finance, Accounts): sorted by column 3 (Money), descending, 6 row(s) shown; 0.21 ms.` and
  `Table (Finance, Accounts): filter 1 set to kind 4, 2 of 6 row(s) shown; 0.18 ms.` for each click.

The model has no Unity type in it and is checked offline (`EconomyCheck/TableChecks.cs`).

Proven: every member and object named, from the game's code and its scene data. Inferred: that the copy of the
drop-down works away from MyEmployees as it does there, where the filter stands on screen, and how seven or eight
columns of text fit their widths.

## 31. A bar (`GameUi.GameBar`)

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

## 32. Options that do nothing for a while, and a row that is not always there (`Meta/Features`)

The mod API has no way to grey a row out or to leave one off. The rows are the game's own objects, though, made
again each time the page is drawn, and `OptionsLayout` is told as each is made (its `RowTap`):

- `Feature.StandsAside(why, optionIds...)`: while `why()` answers a sentence, every `Selectable` of those rows (the
  game's toggle and slider) gets `interactable = false`, which is the game's own look for a control that cannot be
  used, and each of those rows says the sentence after its label: "Third-person camera   (unavailable: the camera
  is with ...)". A row that needs another option on (`Meta/OptionsTree.cs`) is drawn the same way.
- `Feature.Present(optionId, present)`: while `present()` is false the row is switched off and kept off for that
  drawing, whatever section opens or search finds it, and is not counted in "5 of 6 on". The option keeps its value.

Both are asked when the page is drawn, never per frame. See [other-mods.md](other-mods.md) for what uses them.

## 33. An app whose name has a name in it (`PhoneAppDef.TitleArguments`)

The game names an app by a text key on a `TextLocalizationComponent` (its button's `AppButton.appTitle`, and
`FullMenu.appNameLabel` in the top bar). That component also has `Arguments`: an object whose fields, or a
dictionary whose entries, fill the text's names (`LocalizorManager.GetLocalization(key, arguments)`, in
HGPlugins.dll), and it writes its text again when either changes and on a change of language. So an app named after
something of the game's (a bank's name) is one key, "{bank}", and `TitleArguments` is a `Dictionary<string, string>`
(read without reflection). `PhoneApp` sets the arguments after the key on both buttons and, when the app shows, on the
top bar before its key (two apps with one key differ only by their arguments); when the app is hidden it takes its
arguments off the top bar if they are still there, because the game's own apps set only the key. Used by the bank
apps (Economy/Banking/BankApps).

### What a row of the mod options page can do, and how the page is arranged (`Meta/OptionsTree`, `OptionsLayout`)

Read from the decompiled game (`BigAmbitions.ModsInternal`):

- `ModOptionsViewController.Rebuild` (lines 43 to 58) destroys every row and makes each again from its prefab, for all mods on one page; a row that is none of the six kinds is asked to draw itself (`SpawnOption`, lines 84 to 96).
- A row has a label and its control, nothing else: `ModOptionsToggleControl`, `ModOptionsSliderControl` (a second text for the number), `ModOptionsDropdownControl`, `ModOptionsButtonControl` (a text on the button that the game never fills), `ModOptionsHeaderControl`. No description, no tooltip, no disabled state, no indent, no sub-page.
- Each control calls its handler with the saved value while it is made, and again on every change. That is how the page knows which switches are on without reading anything itself.

What the mod adds, all with the game's own parts:

- A sentence per option: the game's `BasicTooltip` (`TooltipTarget.OnPointerEnter`, a 0.1 s delay, then `TooltipSystem`) is added to the row with `descriptionKey` set to the key `reallife_opt_tip_<option id>`. It is added the first time the row's section is shown in a drawing, not for every row of the page.
- Greyed rows: `Selectable.interactable = false` on the row's controls, the label at the control's own disabled alpha, and the reason in the label's `Suffix` ("needs: Credit rating and phone loans"). What a row needs is declared in `Meta/OptionsTree.cs`.
- Groups and sections are button rows that switch the rows after them on and off; their label's `Suffix` carries "5 of 6 on", written when the page is drawn and on a click.
- Questions go through `HudConfirm.Show` (the game uses it over its own options screen, `Scenes.MainMenu/Options.cs` line 530) and answers through `Notifications.Show`; with no HUD (the main menu) the answer is shown in the confirm dialog (`Meta/OptionsNotice`).
- Test buttons are on the page only while "Show test tools" is on: the page is registered again with or without them (`OptionsService.Register`), so a player's page has about 300 rows fewer to make.

A new option: add its row in `RealLifeOverhaulMod.cs` as before, name its id in a section of `Meta/OptionsTree.cs`, and give it a sentence in en.json. `CatalogueCheck` fails until all three are there.

## 34. A business type of the mod's own, and cars that stand in a building (`Economy/Dealer`)

- **Registering it.** `BAModAPI.ModdingAPI.RegisterModBusinessType(BusinessType)` takes a `ScriptableObject` made in code:
  `businessTypeName` (also its text key, which the game finds in the mod's own `Locales`), `suitableBuildingType`
  (`ba:buildingtype_retail`), `employeePrimarySkills` (what the job board and the agencies offer: customer service and
  cleaning), the tags `ba:businesstag_allowplayercreation` and `ba:businesstag_generatesrevenue` (`AddTag`), and empty
  lists for products, demand sets and the day and hour factors. The game keeps the mod's types in a dictionary of its own
  and lays them over its own on every load (`BusinessTypeHelper.OnBusinessTypesLoaded`, and the building types'
  `availableBusinessTypes`), so one call when the mod starts is enough; a vanilla name wins over a mod's.
- **What it then is.** BizMan offers it in a retail building; the staff's shifts point at a station item whose
  `suitableSkills` fit (a cash register for customer service). With no day or hour factors the game's customer count
  (`CustomerEntriesCalculator.GetCustomersByHour`) is 0 for it, so no shopper of the game's walks in; the building still
  has a capacity (`GetInitialCustomers`) and a promotion the game and the mod's features write. Its name splits at the
  first underscore for the game's layout folders (`GetIdWithoutType`): `reallife_businesstype_...` finds none, as wanted.
- **What it only gets once a city is up.** The icon (`GlobalReferences.vehiclePOIIcon`), and from a retail type of the
  game's its logo shapes, uniforms and appearance tags: copied onto the type when the city loads.
- **Cars in a building.** A `VehicleInstance` whose street data is a building's address is spawned by the game itself at
  its saved world position whenever the player walks in (`BuildingManager.DelayedEnterBuildingActions` calls
  `GameManager.SpawnPlayerVehicles`, then raises `GlobalEvents.onEnterBuildingDelayed`), with a map pin. The floor the
  game measured on entering is `BuildingManager.interiorBounds`. A car made while the player is inside is spawned with
  `VehicleHelper.CreateAndSpawnVehicle` (which adds it to the save itself); one made while away is only added to
  `GameInstance.VehicleInstances`. `VehicleController.SetFreeze`, `EntityController.primaryInteractionEnabled` and
  `poi.SetHidden` hold a car on show. `VehicleInstance.Delete(controller)` removes one the game's way. Such cars are the
  player's to the game (BizMan's list, the wealth figure); the mod's own features ask `Core/Dealerships.Stock`.
- **Money.** A car sold is `ba:transaction_itemsold` with `itemSoldInfo`, a car bought `ba:transaction_vehiclebought` with
  `vehicleName`, as the game books its own; both with the business's address. The game's statement takes a business's
  sales from its order book only, so `Economy/GameBooks` puts them in the day's statement itself.

Proven: every member named, from the game's code. Inferred (not seen running): that a cash register takes a customer
service shift at a business with no products, and how cars stand on a shop's floor.

## 35. The mod's settings on the phone (`Meta/SettingsApp`, `SettingsView`, `OptionsLayout.Book`, `GameUi.HelpFrame`, `GameUi.OptionRows`)

The Mods tab is one flat list for every mod: `ModOptionsViewController.Rebuild` destroys and makes every row of it each
time the tab is shown (`OnEnable`) and each time a mod registers its page (`OptionsService.OnChanged`), about 470 of the
mod's rows and buttons, and about 500 more with the test tools on. The Settings app ("Mod settings") is the same options
laid out as the game's help screen (F1), in the mod's dark colours, inside the full menu like every app of the mod:

- **The window** is a copy of `HelpSystem.container` (`Canvases/HelpSystem/Container`, 2400 x 1600 at the middle; F1 and
  `HelpSystem.Toggle` stay the game's): the frame (sprite `helpoverlay`), the top bar's headline and red close button, the
  round back and forward buttons, the search box (`InputField (Search)`, a `TMP_InputField` with the magnifier and the
  clear icon), the list on the left (`Left (Categories)`: a `Category` of button, arrow, `PagesBackground` and `Pages` of
  links) and the page on the right (`Help Page`, a ScrollRect with its scroll bar, the `Topic` header and the help's own
  text, `TMProTemplate`). `GameUi.HelpFrame` copies it asleep, takes off `HelpSystem`, `HelpCategory`, `HelpPageLink`,
  `HelpPageRenderer`, the game's `InputField` and the mark-down renderer, and keeps what only draws or sounds. It is
  painted with `GameUi.DarkLook` (Flare's night colours of 0.40.5, the game's blue for the page shown): the help's white
  sprites are tinted, so their shapes and shading stay the game's. The game's own help is not touched.
- **The list**: one category per group of `OptionsTree` (Start here first, Troubleshooting last) and one more, Test tools,
  while "Show test tools" is on; a link per section (`SettingsRules.Pages`). A category opens and closes as the help's
  does (`HelpCategory.SetOpenState`: pages and their ground shown, the arrow turned); the page shown is in the blue.
- **The rows** are copies of the game's own settings menu (`UIs.options`, `Canvases/Options`): the button row with a
  description under its name (the menu's "Reset windows": `Labels` with `Label` and `Tip`), its switch row
  (`ModOptionsToggleControl`: `switch-off@2x`, `switch-on@2x`), its slider row (`ModOptionsSliderControl`, the number in
  its `Suffix`) and its drop-down row (`ModOptionsDropdownControl`: the game's `UI.Elements.Dropdown`, kept whole as
  `GameUi.FilterDrop` keeps it). `GameUi.OptionRows` gives every row the two lines of words, the control at the right in
  the menu's own column of 700. The menu is white on dark already, so its rows stand on the dark window as they are.
- **Controls by kind** (`SettingsRules.ControlOf`): an option that is on or off is the switch; a number is the slider,
  between the option's own `Min` and `Max` in whole numbers (`wholeNumbers`, as `ModOptionsSliderControl`), its value
  written beside it (`valueLabelKey`); a choice is the drop-down with its arrows; a button of the player's or a test tool
  is the blue button with its word (`OptionsLayout.Captions`); a key is a button with the key on it, which waits for the
  next key (`Keys.Rebind`, the Mods tab's own capture; Esc keeps the key).
- **Words**: each row has its label and, under it, the option's sentence (`reallife_opt_tip_<id>`), what keeps it from
  working (in orange), "takes effect when the save is loaded again" where so, and its default when it is not at it. A
  preset's row says what it does and how far the choices are from it; the top bar says the preset in use.
- **One source.** The lines are `SettingsRules.Tabs` over the very plan the Mods tab is placed by (`OptionsPlan.Place`),
  and each line holds the row the page registers. A value is read as the game's controls read it (`ModOptionPrefs.Load`:
  the key `m:<mod>:<id>`, the option's default) or from its field in `Settings`; a change is saved under that key and
  then the row's own handler is called with it (`OptionsLayout.AppSet`), as `ModOptionsToggleControl`,
  `ModOptionsSliderControl` and `ModOptionsDropdownControl` do. A switch's handler is the page's wrapper, so the Mods tab
  hears of it. The app calls `PlayerPrefs.Save` when it is closed after a change.
- **Search** is the help's own box: from two letters the page shows every row with each word typed (`OptionsFind`, as
  the Mods tab's box), under its group and section, at most 60, each with its control; the list keeps only the pages
  with a match, open, as the help's list does while searched. While the box has the keyboard the mod's keys are off.
- **Cost**: the window and the list are built the first time the app is opened (a few hundred objects, the game paused
  under the menu); a page takes rows from pools of each kind, so a page shown again makes nothing; a change writes the
  rows of the page that hang on it. Nothing runs a frame of its own.
- **Opening it** from the Mods tab waits for the options to close (`GuideApp.Blocked`), as the Guide does. Another app's
  page can add a button that opens it on a section (`SettingsApp.Link`; BizMan's Interior tab).
- **If the game changes**: a part of the help or of the settings menu not where it was leaves the app off the phone,
  with one line in Player.log; the options stay on the Mods tab.

Proven: the game's members and objects named, from the decompiled game and the scene data (`evidence/uitools/uitree.py
script:HelpSystem`, `script:ModOptionsToggleControl`, `name:ResetWindows`). Not seen running: the window inside the full
menu, the drop-down's list over it, the rows' heights with long descriptions.

## 36. The box's own look (`InfoBox.Frame.cs`, `InfoBox.Look.cs`, `InfoBox.Parts.cs`, `LookRules`, `LeftPaneRules`)

The white box writes every app's page in one look of the mod's own, made from the game's box itself when it is taken:
its line of text, its title, its headline's font and grey, its rule's grey, the round sprite and green of its
satisfaction bar, the tick of its switch, and the game's red, orange and green (`GlobalReferences.colors`).

**Why it was rebuilt (the left pane, 26 Oct 2026).** The game's white box is a fixed card with a `VerticalLayoutGroup` of
no scroll. Given more than it holds, that layout takes every child's height down from its preferred towards its minimum,
and a text's minimum is nothing: so each page that was too long for the card had every text squeezed, section heads were
drawn over the text above them, wrapped lines over the next row, and the foot ran into the buttons. A page that fitted
showed none of it. The fault was the card's, not any app's, and it was in all of them (130 source files write a page).

**The frame (`InfoBox.Frame.cs`)**, all Unity layout with no position or height of the mod's own:
- the **head** (title, subtitle, chips) and the **foot** (the rows of buttons that close the page, then the Guide's page) ask for
  the height their parts ask for; the **body** between them has a flexible height and none preferred, so the card's layout
  never squeezes the head or the foot;
- the body is a `ScrollRect` (the game's own table's step, movement and scroll bar, copied) over a masked viewport
  that catches the wheel, whose content is a vertical layout under a `ContentSizeFitter`: as tall as its parts ask, so a page
  of any length in any language overlaps nothing and scrolls;
- the rows of buttons after the page's last heavy part stand in the foot (`LeftPaneRules.FootRows`), two to a line and
  full width for an odd one (`ButtonRules`); a row in the middle of a page stays where it is;
- the scroll goes to the top for another page (another title) and is kept for the same page written again (a press that
  changed a figure), `LeftPaneRules.KeepOffset`.

**The parts (`InfoBox.Look.cs`, `InfoBox.Parts.cs`)**:
- **A header:** the title in a horizontal layout with its chips, so a long title wraps in the room the chips leave it, under a
  short bar of the app's colour (`LookRules.Accent`); chips wider than 42% of the line go on a line under the title; a muted
  line under it (`Subtitle`); chips in a `CellTone` (`Chip`).
- **Sections:** a card in a tint of the rule's grey with its heading in small capitals over a hairline (`Section`); what
  follows stands in the card until the next section, a row of buttons or the foot. `Headline` is a section.
- **Facts:** the label at the left in grey and the value at the right (`Row`, `RowWords`, `RowPair`), two texts of one size
  in a horizontal layout so that they share a baseline. The label is given the width its words measure
  (`LeftPaneRules.LabelWidth`, from the text's own preferred width, so it holds for any language), the value takes the rest,
  right aligned, wrapping under itself. A pair of facts is two such halves, or two facts one under the other when either does not
  fit its half. An old "Label <b>value</b>" line is read as a fact.
- **List entries (`Entry`):** a rung of a ladder: its name and figure on one line, what it asks under them in small muted
  words; the current one on a tint of the app's colour with a mark at its left. The career app's professions use it.
- **Meters and needs:** a fact with a bar of the game's round sprite under it, the card's whole width (`Meter`); a line with
  the game's tick, or a red dot (`Check`).
- **Notes and the foot:** smaller muted sentences (`Note`); the Guide's page in the foot under a hairline (`GuideLink`).

Each part is one copy of a template, kept asleep between pages and reused (`InfoBox.Keep.cs`, a section card laid aside after
its parts). Nothing runs a frame: a page is written on a click, and Unity lays it out once. With Developer diagnostics on, each
page writes its parts and time to Player.log ("Info box check").

**Checks.** ContractCheck (`LeftPaneChecks.cs`, `LookChecks.cs`): the rules above on made-up widths (the label's width always
leaves the value its room; the foot takes the right rows; the scroll's place; when two boxes cross); and the sources: the
frame is Unity's layout, the code that writes a page sets no position, size, offset or margin, no caller reaches into a part's
transform or puts a label and a value on one line by mark-up, every caller writes with the card's own parts. In the game,
Options > Tests > "Check the apps' left panes" (`Meta/LeftPaneTest.cs`, `LeftPaneFixture.cs`) opens every app of the mod on the
phone and measures its card (`InfoBox.Audit`): no text taller than its place, no two texts' boxes crossing, nothing past the
edge, the scroll reaching the page's end, for the page the app wrote, for Career's profession with the most rungs and Campus'
longest courses, and for a page of every kind of part in the longest words of each of the mod's nine languages; one log line a
page ("Left pane check (app, page): PASS"), and a last line that says PASS only if every page did.

## 37. Folders and a dock on the phone's home screen (`PhoneLayoutRules`, `PhoneFolders`, `PhoneGridPager`)

The game's phone has no folders and no dock: two buttons wide and four rows on the HUD phone, one row in the full
menu, every app a button. A folder of the mod's is made of the game's own parts only:

- **The tile** is a copy of that phone's app button template (as the More apps button is: `UiClone.Asleep`, the game's
  scripts that make it an app taken off, `ButtonEffects` and `Badge` kept, the open app's dot taken away), its title
  the folder's name, its picture a tile painted as the game's icons are (`AppIconPainter.Paint` with no glyph) with
  nine `Image` children over it, the icons of the folder's first nine apps (`AppButton`'s own `icon` field read back),
  three rows of three. It stands in the grid, with a `PhoneTile` on it so that no pager counts it as an app.
- **A folder's page** puts the folder's apps on the same grid, after a Back tile (the More apps button's light tile
  with the game's `Icon-Arrows` mirrored: `AppIconPainter.Paint(..., mirrored: true)`). Every other button of the grid is
  switched off with a `PageMark`, as a button on another page is, so another mod's pager is seen at once.
- **The dock** is the grid's last row on the home screen. The grid lays out what shows in the order it stands, so a
  short page is filled before the dock with empty tiles (a bare `RectTransform` each), and the pager puts every child
  it counts in the view's order whatever the page (`PhoneGridPager.Order`).
- **The badge** of a folder is the game's `Badge.UpdateBadge` with the numbers of its apps added up, read back from
  each button's own badge text (whoever set it).
- **The cancel key** is `PlayerAction.Cancel`, which `GameManager` sends to `CancelButtonHandler.HandleEscapeClick`; with
  only the HUD up the game opens its menu. A component on the grid, on only while a folder is open and run before the
  game (`DefaultExecutionOrder(-31000)`), takes the press (`PlayerAction.Cancel.Reset`) unless something the game closes
  first is up (`HudConfirm.isOpen`, `MiniMenu.IsOpen`, `FullMenu.IsOpen`, `CityMap.IsOpen`, `Options.IsVisible`,
  `PlayerActivityUI.IsPanelOpen`, `BuildingPreview.isPreviewing`, `InteriorDesignerUI.IsOpen`, the dialogue, the
  building card, `GameManager.HasInputSelected`, a list window of the mod's).
- **Moving an app** is a right click (`IPointerClickHandler` on the button: `Button` itself answers the left one only),
  which opens the subway's list window (`DeskList`, section 10) with the places to go.

See [the phone](../../docs/features/phone.md) for what the player sees and what is checked.

## 38. Lines of the mod's in the game's objectives window (`GameUi.TaskGroup`; the construction objectives)

`UIs.Instance.tasksUI` (`UI.Tasks.TasksUI`, `Canvases/Tasks`, the panel of section 10) holds groups under its
`groupTemplate`'s parent: the business alerts (`bizman_alerts`, made in its `Start`), a job's mission group
(`MissionTasksUI.CreateTasksGroup`, first in order), the workout plan (`WorkoutPlanUI`) and each side quest
(`SideQuestUI`). Every one is made by the public `SetUpTasksGroup(headerKey)`, a copy of the template with
`Headline/Label`'s key set, and filled with copies of the public `taskEntryTemplate`: a `Button` on the entry, `Label`
(TMP 30 with its `TextLocalizationComponent`), `Checkmark` (a `Toggle`), `DestinationButton` (a `Button`) and
`CloseButton`. The game's own entries set the label's colour to `GlobalReferences.colors.yellow` or `.red` for its
medium and high priorities, hide the destination button when there is no address and wire it to the Set destination
sequence of section 5. After a change the owner calls `ScheduleUpdateObjectivesHeight()`: the game sizes the window,
folds it away while no group has a shown entry (`HasVisibleObjectiveEntries`, which skips entries named as its
templates and `Headline`) and opens it again when one has. The whole window's alpha follows the full menu
(`GlobalEvents.onFullMenuToggle` -> `ChangeVisibility`, also off in a mini game), and it goes with the HUD in the time
machine and with "hide the interface".

`TaskGroup` makes one group by that call, its entries by instancing the template (named `reallife_task_N`, never a
template's name), hides `Checkmark` and `CloseButton` (as the game's own time entry does), rewires the entry's button
and its destination button to the owner's presses (`UiClone.Rewire`), and folds on a press of its headline (a `Button`
added to `Headline` if it has none, its label the raycast target). Words go through `UiClone.Words`; a line whose words
did not change is not written again. No line hides the group; the feature off destroys it. A load destroys the window
with the scene: the next write makes the group again. Nothing runs a frame.

Proven: every member named here (decompiled `TasksUI.cs`, `MissionTasksUI.cs`, `SideQuestUI.cs`, `WorkoutPlanUI.cs`).
Inferred: how a long line wraps in the entry and how the added headline button takes a press.

## 39. A toolbar over the map, the game's tooltips, and a tour (`Construction/BuildModeKit`, `BuildModeBar`, `BuildModeTip`, `BuildModeTour`)

Build mode's interface over the city map is made of the interior designer's own parts (`UIs.interiorDesignerUI`,
`Canvases/InteriorDesignerUI`, a canvas of order 3 scaled to 3840 by 2160): its toolbar's `Topbar/Section` (the box, sprite
`Box-Darker-Small-125` at 0.949, a horizontal layout and a content size fitter), one of its tool buttons (a `Button` with the
game's `ButtonEffects`, its child `SelectedBackground` in `Button-Blue` and its `Icon` of 64), a `Divider`
(`Square-With-Padding-8`) and a label of its panels (`ActionPanels/QueuePanel/NoPointOfSaleItemsLabel`, TextMeshPro 32 in
the game's font). `BuildModeKit` copies them asleep and takes every script of the game's off them but `ButtonEffects` and
the text component (the designer's `ToolButton`, `ActionKeyTooltip` and `ConfigurableControlsHintsToggler` are in
`BigAmbitions.InteriorDesigner.dll` and other assemblies, so `UiClone.StripGameScripts`, which takes the main assembly's,
would leave them). The icons are the designer's toolbar's (read from it: `Icon-Palette`, `Icon-Coin`,
`Icon-CopyOutline-64`, `Icon-Close`, `Icon-Walls`...) or others by name through `GameSprites`. A coloured box is the game's
`white-rounded-box` (else `round-corner-no-drop`) tinted, never the colour picker's `white-round`: that sprite is packed
tight, 50 of its 232 pixels each side cut away as see-through, so a box of it draws smaller than its rectangle (a row
116 high drew as a thin line, a 60 square as a dot; `UnityEngine.Sprites.DataUtility.GetPadding` says it, and the interface
test fails on any sliced picture with such a margin). A plain fill is `1pixelwhite`. Buttons that must read as buttons on
the dark box get a face inside a thin edge (`BuildModeKit.Frame`). The hint bar stands under the game's top bar and
everything hung under it, measured at each writing (`BuildModeGame.Hud`), clear of the objectives panel; the game's FPS,
address and neighbourhood labels at the foot (PlayerHUD's) are switched off while build mode is open and given back. The parts stand on the help's canvas (order 20, over the HUD and under
the question box and the tooltips, both 21).

The game's tooltip (`Tooltip.TooltipSystem`, its own canvas of order 21, following the pointer) shows for any
`TooltipTarget` a tenth of a second after the pointer rests on it, and a disabled button still gets the pointer, so a
greyed tool's tooltip can say why. `BuildModeTip` is a `TooltipTarget` whose `ShowTooltip` writes a header (a text that is
not a key is written as it is, `TooltipSystem.SetHeader`) and a line in final words. The same system can be driven
without a target (`TooltipSystem.Show`, `AddHeader`, `AddSplitter`, `AddLabel`, `Hide`): build mode says the square under
the pointer that way, written only when the square changes, taken away as the pointer goes onto the interface.

Proven: every object, size, sprite and member named here (the scene data, `uitree.py name:InteriorDesignerUI 5`; decompiled
`TooltipSystem.cs`, `TooltipTarget.cs`, `BasicTooltip.cs`; `ilspycmd -t BigAmbitions.InteriorDesigner.ToolButton`). Inferred:
how the copies look at the foot of the map, the tooltip beside the pointer over the map, and the layout's sizes in a
language with longer words.

