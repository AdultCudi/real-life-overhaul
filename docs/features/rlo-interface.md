# RLO interface

## In a sentence

Under the living economy's master option **Living population**, the RLO interface (`ui_interface`, off by default) draws
the game's Contacts and the overhaul's screens in the mod's own design, after Cities: Skylines II. Slate panels stand
over the city with thin title strips, colourful icons, faces and charts, apps open as windows with the city round them,
and a status bar runs along the bottom. The mouse leads, and the keyboard and a pad reach every target.

It is off by default since 1.0.3, while faults in its screens are mended (eating at a counter, picking items when
ordering by phone, the vehicle's fuel and cargo, Korean and Chinese text): the game's own screens show, and a player
switches it on under "RLO interface" on the options page. At the first start on 1.0.3 a switch saved as on is written
off once (`Core/UiOffRules.SwitchOffOnce`, from `Settings.Load` before the switch is read; the mark `ui_interface_reset`,
103, keeps every later start away), and that player is told once in the game's own notification at the next city
loaded (`Meta/Welcome`; it pops up whatever the notification switches say). A switch set on afterwards stays on. No
preset touches it, and "reset everything" writes it off.

## Where the code is

- Folder: [`Scripts/Ui`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/). Its README explains how to build a screen and
  holds the review checklist. The rules are in [docs/standards/ui.md](../standards/ui.md), and the reasons for them in
  [ADR-0007](../standards/decisions/0007-rlo-interface.md) (an interface of its own) and
  [ADR-0008](../standards/decisions/0008-cs2-interface-look.md) (its look).
- Options: "RLO interface" (`ui_interface`) and "Interface motion" (`ui_motion`: full, calm, off). The kit has one dark
  look, so Flare's look option (`flare_theme`) no longer changes it. With either switch off, every screen is the game's
  own.
- Tests: "Open the interface gallery", "Open Contacts on the RLO interface", "Open the city's people on the RLO
  interface", "Open Flare in the city on the RLO interface" and "Open your first tech firm on the RLO interface";
  "Open BizMan on the RLO interface", "BizMan for a business you do not own", "BizMan for a building to rent" and
  "Switch BizMan back to the game's own".

```
Ui/                         game-free unless named: the offline checks and the previews draw exactly what ships
  UiTokens, UiColour          the one dark look's colours, type, spaces, radii, strokes, motion
  UiFontMetrics(.Data)        Overpass's metrics, written by UiAssets/scripts/fonts.py
  UiTextRules                 widths, wrapping and fit, by those metrics, and the room of a text's box
  UiFallbackRules(.Data)      the characters the face lacks: which of the game's fonts draws them, in what order for
                              a language, how wide they are measured, and the fonts check's line (the table of the
                              game's Western fallback is written by RealLifeOverhaul.Build/Census/game_fonts.py)
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
  UiListDetail, UiSessionSteps,  the three older shapes, drawn in the window until their screens are converted
  UiForm
  UiGallery(Screen)           the kit's gallery
  UiFocusMode, UiFocusRules,  who leads (the mouse or the keys), the d-pad's focus, and the rules EconomyCheck holds
  UiCheckRules                every screen to
  UiScreens, IUiScreen        the one list of screens, and what a screen answers
  UiView*, UiHost, UiTweens,  the renderer (pooled slots, only what changed), the host (input, focus, sounds,
  UiSprites, UiFonts          typing), the tween pool, shapes and painted faces, the font bundle (game-facing, still
                              game-free)
  UiFonts.Fallbacks,          the mod's own copies of the game's fallback fonts, their line cut to the face's, on
  UiFontProbe                 every face in the language's order, and the line the kit draws to check them
  UiShaderRules               which text shader of the running game the faces are given as the bundle is read (the one
                              it was built with draws on Windows alone: pink blocks on a Mac), and the check's one line
  UiGame, UiText              the one file that meets the game (its input, sounds, money, time and icons), and the
                              words (game files)
  UiDirector(.Tests, .Fonts), the fonts behind the loading screen, the language's characters and the fonts check's
  UiGuide                     line, the switch, Contacts, the tests, the feature's entry
  Pages/                      the population's, Flare in the city's and the tech firms' screens, and a desk session's
                              pages over the game's own panel (SessionKit)
  Contacts/ContactsKit,       the game's Contacts app under the kit: hidden, read, its buttons pressed, given back on
  Contacts/ContactsScreen     a fault, for a call, an offer or the game's question box
  BizManModel, BizManPages,   BizMan's model and its pages on the app window (game-free), the tab and glyph rules
  BizManRules, UiFirmHead     checked offline, and the firm's head (a block of the kit)
  BizMan/BizManKit,           the game's BizMan under the kit: its list and its business page read, its buttons
  BizMan/BizManScreen(.*)     pressed, a page only the game's view shows handed over to it until the app closes
  UiPageFence                 the d-pad kept on a page of many blocks (a block of the kit)
  Interiors/InteriorLines     the Interior tab's lines, written into the game's boxes or the kit's page alike
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
- **BizMan**: a manager's office. The portfolio's kinds of place (businesses, homes, warehouses, headquarters) stand
  down the sidebar with their counts; the page holds how many are open now, how satisfied their customers are and which
  earns the most on one line, then the businesses as the big table with a Set destination on each row, sorted and
  filtered by the game's own heads and filters. A place opens with its
  head (its logo, its name with the list of the others, its kind, its address as a link that sets the destination, its
  security, what it still needs, its open switch and EconoView), then the shown tab as its job needs it: the building
  as its photo beside its facts with the offers under them; insight as the period's customers over their chart beside
  the capacity and satisfaction, promotion under the chart; prices and deliveries as tables with each price or amount to
  step, a price marked where the game marks it; the mod's Interior tab as its scores beside its four pages;
  marketing as campaigns with their switches beside the agencies to call; settings and real estate as two columns of
  rows and controls; starting a business as a catalogue of the kinds. The schedule, the managers, the factory, the
  warehouse's pages and another mod's tab open in the game's own view from their entries, until the app closes.
- **The overhaul's pages**: the population and Flare in the city as dashboards, and a desk session's steps as a card
  with its choices. A tech firm's whole page is a dashboard too. A studio's shows its games out, copies and last 30
  days' takings with their line, its games and the games market's newest, its team by group with the groups it lacks,
  and its sales and support. Another firm's shows its products or work, the rivals in its markets, its team and its
  markets' shares. They draw inside the window on the older page shapes until they are converted to the new templates.
- **The gallery**: every component on seven pages, the last an icon style sheet.
- **Every script**: the face, Overpass, holds Latin and Cyrillic letters. Korean, Chinese, Japanese and Greek, and a
  name typed in any script the game can write, are drawn by the game's own fonts at the face's line height, each
  language in its own font first. A character no font has shows as the game's question mark, never as nothing.

## The fonts of other scripts

Overpass has no Hangul, no Chinese characters, no kana and no Greek. TextMeshPro draws a character the text's font
lacks with a fallback font, and lays a line out by the font of each of its characters. The game's fallbacks for
Chinese, Japanese and Korean (its Noto fonts, in TMP Settings' general list) stand 1.448 em from ascender to descender,
Overpass 1.266, and the text engine ends a text that may be cut at its first character when a line is taller than its
box. A box one line of the face tall, as every label of the kit had, therefore drew nothing of such a line: a player
reported all Korean text gone (12 November 2026). No glyph was missing.

- **Copies with the face's line** (`UiFonts.Fallbacks`): for each of the game's fallback fonts that has a font file
  (found where TextMeshPro asks them: the general fallbacks, the default font, the game's interface font) the mod makes
  a font asset of its own over the same file, with its ascender, descender, line and capitals cut to the face's, and
  puts the copies on every face in the order of `UiFallbackRules.Rank`: the Western font, the language's own, the other
  regions', then the rest as the game lists them. So Chinese is written by the simplified font before the Japanese one.
  Nothing of the game's own fonts or of TextMeshPro's settings is written: the game's screens are laid out by them.
- **Room** (`UiTextRules.Room`, `UiView.PlaceText`): every text's box has room no line needs, a line under a text laid
  from its top, a line over and a line under a centred one, half a line under a wrapped block. A font that is not a
  copy cannot drop a line either, and no word moves.
- **Measure** (`UiFallbackRules.AdvanceEm`): a character the face lacks is measured as the font that draws it draws
  it: the Western fallback's own advance, 0.92 em for a Hangul letter, a square for a Chinese character or a kana.
- **The check** (`UiDirector.Fonts`): behind the loading screen, and again when the game's language changes, the
  characters of the language's texts (the mod's file and the game's own) are read on a worker thread and put into the
  copies' atlases, and one line of Player.log says what they came to, whatever Developer diagnostics says:
  `Fonts check (ko): 1370 of the language's distinct characters in the mod's texts, 1273 missing from Overpass, 1273
  covered by fallbacks (Noto Sans KR 1273), 0 left: PASS.` It fails, as a warning, when a character is left without a
  font (the first twenty are named, and the game's font then writes the whole language) or when a line of twelve of
  them, drawn through the kit's own text, does not come out whole.
- **Outside the kit** (`Core/GameUi/TextRoom`, `TextRoomRules`): the mod's older screens write in the game's font, in
  boxes sized by its line, and lost a Korean or Chinese line the same way wherever a box was under 1.448 em. Every text
  they have the text engine cut carries its room: its margins give a box that is too short what it lacks for one line
  of the tallest font, on the side where its alignment leaves the words where they were. A text that sizes itself to
  its box needs none. EconomyCheck lists every such text by file, so a new one is given its room or fails the check.

Offline: EconomyCheck `--fonts` (`FontFallbackChecks.cs`) holds the rules against the census of the game's fonts
(`RealLifeOverhaul.Build/Census/fonts-NNNN.json`, written by `game_fonts.py`) for every character of the eleven
locale files and of the game's own words in each language it offers. `preview.ps1 -Method UiPreviewRunner.FontsCheck`
asks the text engine itself, in the game's Unity: the fault made again, then the kit and the room as they are now, then
whole pages in Korean and Chinese. Only the game can show the look of the copies' letters and the `Fonts check` line
of a Korean and a Chinese session.

## Save and Going Public

Nothing is saved, and nothing is shared: the kit only draws.
