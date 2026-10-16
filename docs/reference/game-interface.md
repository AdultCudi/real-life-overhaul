# The game's own interface

The mod draws no canvas, panel, control or prompt of its own. Everything the player sees is the game's own interface, copied or driven through `Scripts/Core/GameUi`, so the mod looks like part of the game.

Before you build anything on screen, read [`RealLifeOverhaul.Build/docs/game-ui.md`](../../RealLifeOverhaul.Build/docs/game-ui.md): how the game's phone, tables, dialogs and click actions are built, which objects are copied, and what is proven and what inferred. The sections named below (7, 9, 10 and so on) are that file's.

## The rule

- Something offered at a place in the world is offered in the game's own interface, and the mod draws no key prompt or panel for it. The ways, with the sections of [`RealLifeOverhaul.Build/docs/game-ui.md`](../../RealLifeOverhaul.Build/docs/game-ui.md) that describe them:
  - a click on the object (sections 7 and 11);
  - the game's panel over a place with its keys (`GameUi.WorldPanel`, section 9);
  - a list in the game's own window (`GameUi.TravelList`, section 10);
  - a button in the panel the game opens on the object (section 13);
  - a button more in the game's own panel at the place (section 16);
  - the game's destination marker (`GameUi.GuideMark`, section 17).

  The buses and the subway journey are the model (`Transport/TransitDirector.Player.cs`, `SubwayJourney`); Home and the Garage use every one of these.

## The building blocks (`Scripts/Core/GameUi`)

```

Core/GameUi/
  UiClone         copies of live game objects made asleep (no script of the game's wakes in a copy), the game's
                  scripts taken off, texts written through the game's own component; one log line per fallback
  PhoneApps       PhoneApp.Install: a button on the HUD phone, a button in the full menu's bar and a panel among the
                  game's app panels; opening as the game opens its own; the badge
  PhoneGridPager  pages of a set length on the HUD phone's home screen and the full menu's row of apps, turned by a
                  More apps button on each page's last tile (PagerRules); steps aside for good when another mod pages them
  AppShell        the frame of an app, copied from the game's MyEmployees: headline, white box, two tabs, table
  InfoBox         the white box as a page: title, lines, headlines, switches, previous/next selectors, buttons
  GameSprites     the game's pictures by name, found in memory
  AppIconPainter  an app icon in the game's manner, painted from one of the game's own
  WorldPanel      the game's panel over a place, with buttons on the game's own keys (an IOverlay in the game's box)
  MapPicker       a choice of places on the open city map, in a copy of the game's window for its stations there:
                  its search field, a marker of the game's per place (named while pointed at), the glide to an entry's place
  TravelList      a list in the window the game lists its subway stations in: a name, lines and a button per entry;
                  Window (its rectangle, for an owner that stands it aside or widens it) and EntryShape (higher
                  entries whose lines wrap) for lists that say more than a station's name
  GuideMark       a mark on a place that is no building, on the game's own destination guider (the job's), taken
                  only while it is free and taken off only by whoever put it there
  NoticeKinds     game-free (CatalogueCheck/NoticeCheck): which kind a notification of the mod's is, from its key alone
  NoticeLook      the mod's notifications told from the game's: the kind's picture on a disc of the mod's colour, in the
                  game's own toast and the game's own notification list; the options page's test of one of each kind

```

Home, the garage and car ownership are offered the same native way (their sections say how; the numbers are the sections of [game-ui.md](../../RealLifeOverhaul.Build/docs/game-ui.md)):

- a click on a cooker or a coffee machine and a button in the fridge's panel (`Home/HomeOffers`, `HomeFridgeButton`; sections 7 and 13);
- the cooking list, the workshop's list, the custom shop, "My vehicles" and a stand's list in the game's list window;
- three more bars under the game's needs (section 14);
- a reading in the game's vehicle panel (section 15);
- the game's own repair-bay panel with a button more (section 16);
- its panel over a place at a dock, a stand, a broken-down car and the doctor's desk, and its destination marker (`GameUi.GuideMark`).

Neither feature has a canvas, a prompt or a key of its own left.

## The mod's notifications (`Core/GameUi/NoticeKinds`, `NoticeLook`)

Every notification of the mod's goes through the game's own `Notifications.Show`, so it pops up in the game's toast and stays in the game's list, saved with the game as its type, key, names and time (`UI.Notification/Notification`).

To be told from the game's at a glance, each carries a picture of its kind on a disc in the mod's indigo, where the game's carry their white one; the game's own notifications, and other mods', are never touched.

- **Which kind.** Read from the key each time it is drawn (`NoticeKinds.Of`: the prefix after `reallife_`, a table read longest first), so nothing new is saved and notifications in an older save get their picture too.
  A new feature's keys need a row in the table only if its prefix is new; a key with no row is drawn as "anything else".

  NoticeCheck fails a key raised by a call that is not taken for the mod's, a toast text over 240 characters, a row that can never match, and more than half of the notifications under "anything else".

- **Where it is set.** The toast: after the game's maker on `Notifications.onShow` (the copy it has just made: its `Text/Image` becomes the disc, the picture a child of it).
  The list: the scroller's `cellViewVisibilityChanged`, raised right after the game's `NotificationListCellView.SetData`; rows are reused, so every row is set either way (the mod's: the disc and its picture; the game's: its white disc back, the game having put its own picture on).

  No Harmony patch, nothing per frame.

- **The pictures.** The game's own where one fits, found once behind the loading screen (`GameSprites`), else one of the mod's glyphs drawn in code like the map's (`MapPlaces.Glyph`, which gained Coin, House and Heart for this):

  | kind | the game's picture | else the mod's glyph |
  | --- | --- | --- |
  | accidents and the road | Icon-Traffic | Cone |
  | police and crime | Icon-Security | Shield |
  | health and the hospital | BusinessIcon-Hospital | Cross |
  | money and bills | Icon-Coin | Coin |
  | property and rent | Icon-Building | House |
  | work and careers | Icon-Briefcase | Briefcase |
  | the player's businesses | Icon-Shop | Tower |
  | the tech sector | Icon-Laptop | Cloud |
  | news and the city | (none) | Paper |
  | family and friends | Icon-People | Heart |
  | anything else | (none) | Star |

Icon-Coin, Icon-Building, Icon-Security, Icon-Shop and the disc's Circle are used by Images of the UI scene, so in memory; the others may not be, and then the glyph is drawn.
With "Developer diagnostics" on, `Notice check (attach)` says how many kinds got the game's picture.

- **Option and test.** "The mod's notifications carry their own picture and colour" (on by default; off, they look like the game's).
  Options > Tests: "One notification of each kind" shows the eleven, 1.2 s apart, kept in the list.

## On screen, everywhere: no canvas of the mod's but the options page

The mod's shared layer (`Core/Hud`, `Jobs/JobHud` and each feature's own copy) is gone.
What replaced it, and what is left:

| was | now |
|---|---|
| the crosshair dot | `Camera/CrosshairDot`: one `Image`, a child of the game's own HUD canvas, made the first time mouse look is on |
| the incident marker with its distance (street crime, events, accidents) | each feature's own `GameUi.GuideMark` (the game's destination marker). Only one feature can have the marker at a time: the one that asked first keeps it, the others ask again on their next tick |
| the speed readout | `Driving/SpeedReading`: one more reading in the game's vehicle panel (a copy of its condition reading, after it, as `CarDash` does), written when the whole number changes. The game's panel has no speed of its own |
| the font asset others read | `GameUi.GameFont.Asset` (the font of the first text the game's interface has, looked up once). `Core/Hud.cs`, which once forwarded `Hud.Font` to it, is gone: nothing reads `Hud.Font` any more |
| the job panel's plate, marker and prompt | `ShiftCard`, `WorldPanel` and `GuideMark` |

Canvases the mod still makes: `Meta/MetaUi` (the options page's own messages, also used in the main menu where there is no HUD).
`ShiftCard` and `NavHud` each add one `Canvas` component to a copy of a game panel so that it batches by itself; that is not a canvas of the mod's own.
None of this has been seen in the game.
