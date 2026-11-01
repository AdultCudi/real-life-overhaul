# RLO interface

## In a sentence

Under the living economy's master option **Living population**, the RLO interface (`ui_interface`, on by default) draws
the game's Contacts and the overhaul's screens in the mod's own design, after Cities: Skylines II. Slate panels stand
over the city with thin title strips, colourful icons, faces and charts, apps open as windows with the city round them,
and a status bar runs along the bottom. The mouse leads, and the keyboard and a pad reach every target.

## Where the code is

- Folder: [`Scripts/Ui`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/). Its README explains how to build a screen and
  holds the review checklist. The rules are in [docs/standards/ui.md](../standards/ui.md), and the reasons for them in
  [ADR-0007](../standards/decisions/0007-rlo-interface.md) (an interface of its own) and
  [ADR-0008](../standards/decisions/0008-cs2-interface-look.md) (its look).
- Options: "RLO interface" (`ui_interface`) and "Interface motion" (`ui_motion`: full, calm, off). The kit has one dark
  look, so Flare's look option (`flare_theme`) no longer changes it. With either switch off, every screen is the game's
  own.
- Tests: "Open the interface gallery", "Open Contacts on the RLO interface", "Open the city's people on the RLO
  interface", "Open Flare in the city on the RLO interface" and "Open your first tech firm on the RLO interface".

```
Ui/                         game-free unless named: the offline checks and the previews draw exactly what ships
  UiTokens, UiColour          the one dark look's colours, type, spaces, radii, strokes, motion
  UiFontMetrics(.Data)        Overpass's metrics, written by UiAssets/scripts/fonts.py
  UiTextRules                 widths, wrapping and fit, by those metrics
  UiFrame                     where a screen stands over the world (a window, a docked panel, a card, the phone),
                              and what the status bar says
  UiScene, UiDraw*            a scene of placed items and targets, and the components that place them
  UiDraw.Chrome               the window, the title strip, the status bar, icon tiles, rows and links, the donut,
                              legend, scale, meters, graded bars, stars, portraits and chips
  UiIcon, UiIcons,            the icons: Phosphor's outlines as code (UiIconPaths.Data, written by
  UiIconRaster,               UiAssets/scripts/icons.py), filled into textures at runtime, the game's own icons
  UiIconPaths.Data            standing in where it has them
  UiPortraits                 a face painted from a person's name
  UiInfoPanel, UiInfoView,    the templates: a person's or a place's panel, an info view with its donut, an app's
  UiAppWindow, UiDecision,    window, a decision card, the phone's home
  UiPhone
  UiListDetail, UiDashboard,  the four older shapes, drawn in the window until their screens are converted
  UiSessionSteps, UiForm
  UiGallery(Screen)           the kit's gallery
  UiFocusMode, UiFocusRules,  who leads (the mouse or the keys), the d-pad's focus, and the rules EconomyCheck holds
  UiCheckRules                every screen to
  UiScreens, IUiScreen        the one list of screens, and what a screen answers
  UiView*, UiHost, UiTweens,  the renderer (pooled slots, only what changed), the host (input, focus, sounds,
  UiSprites, UiFonts          typing), the tween pool, shapes and painted faces, the font bundle (game-facing, still
                              game-free)
  UiGame, UiText              the one file that meets the game (its input, sounds, money, time and icons), and the
                              words (game files)
  UiDirector(.Tests), UiGuide the fonts behind the loading screen, the switch, Contacts, the tests, the feature's entry
  Pages/                      the population's, Flare in the city's and the tech firms' screens, and a desk session's
                              pages over the game's own panel (SessionKit)
  Contacts/ContactsKit,       the game's Contacts app under the kit: hidden, read, its buttons pressed, given back on
  Contacts/ContactsScreen     a fault, for a call, an offer or the game's question box
```

## What the player sees

- **Windows over the city**: an app opens as a window centred over the world with the city round it, under a thin
  title strip (the app's icon, its name in cyan capitals, a close button). The status bar along the bottom shows the
  time, the day and the money. The needs, the season and the apps' tiles come with the HUD's conversion.
- **Mouse and keys**: a hover is a still fill. Key caps, or the pad's glyphs, show on the status bar only while the
  keys or a pad lead, and a focus ring then marks the target. With the mouse, the window's close button goes back.
- **Contacts**: in its window, groups (the game's categories) at the left, the list (unread first, then the newest)
  with a face or an icon on each row in the middle, and the open contact at the right. The contact shows its messages
  under the line where they start, its details and the game's own buttons as actions. Call, an offer with buttons and
  a tax table hand over to the game's own view until the app closes.
- **The overhaul's pages**: the population and Flare in the city as dashboards, and a desk session's steps as a card
  with its choices. A tech firm's whole page is a dashboard too. A studio's shows its games out, copies and last 30
  days' takings with their line, its games and the games market's newest, its team by group with the groups it lacks,
  and its sales and support. Another firm's shows its products or work, the rivals in its markets, its team and its
  markets' shares. They draw inside the window on the older page shapes until they are converted to the new templates.
- **The gallery**: every component on seven pages, the last an icon style sheet.

## Save and Going Public

Nothing is saved, and nothing is shared: the kit only draws.
