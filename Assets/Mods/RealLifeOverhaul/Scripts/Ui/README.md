# The RLO interface kit

Every screen of the overhaul, and every game app the mod redraws, is drawn here in the look of Cities: Skylines II,
mixed with a few other games: the owner's choice of 1 November 2026. The rules are in
[docs/standards/ui.md](../../../../../docs/standards/ui.md) (RLO-UI-001 to 018), the reasons in
[ADR-0007](../../../../../docs/standards/decisions/0007-rlo-interface.md) and
[ADR-0008](../../../../../docs/standards/decisions/0008-cs2-interface-look.md), and the feature in
[docs/features/rlo-interface.md](../../../../../docs/features/rlo-interface.md).

## How to build a screen

1. **A view model**: plain data, no colour or position. Give the feature a view of its own (as `PopulationViews` or
   `StudioViews` do) and translate it into a template's model. Words come from locale keys through `UiWords`.
2. **A template**: every screen stands on one (RLO-UI-018). Pick by what the screen is about:
   - **Info panel** (`UiInfoPanel`, `UiInfoModel`): one thing in the world, a person, a household, a business, a
     building or a vehicle. A narrow panel (440 px) docked at the top left with the world beside it: round icon tabs
     down its left for the things it joins (household, person, home, work), the title strip, a portrait in its status
     ring with the mood and one line of facts, sections of rows on darker bands with cyan links that open the next
     panel or move the camera, and footer icon buttons (locate, follow, call).
   - **Info view** (`UiInfoView`, `UiInfoViewModel`): the city by one measure, its people, its demand or its
     districts. A menu of the views as tinted icons, and beside it a panel of headline rows, a donut with its legend and
     a map legend. The mod tints the city map's districts by a figure today (the Districts view does,
     `Districts/DistrictGame.Tint`), and build mode lays a coloured mesh of its own in a copy of the map's zone material,
     which can colour the city block by block. `ScaleSteps` gives the legend the colours the game file tints with.
   - **App window** (`UiAppWindow`, `UiAppModel`): a phone app, such as Tech, Economy, Stocks, Bank, a BizMan-like
     screen or Calendar. A window over the city with its title strip, the app's sections as a sidebar of tinted icon
     entries (Football Manager's left menu) or, for an app of few sections, icon tabs across the top (`TabsAcross`: a
     translation too long for the tabs takes the sidebar). Then a page of blocks, each left out when empty: a person's
     head (`UiProfileModel`: the face in its ring, the role, the firm behind its badge and a status chip, key facts and
     stars on one rhythm) or the next step, the
     actions, a band of figures, graded attributes, tables whose rows lead with an icon tile that says what differs
     between them (left out when every row would show the same one; one column may take the room left with
     `UiColumn.Grow`, so the first keeps to its words) with a graded figure and a status chip, and people with portraits
     and two graded attribute bars, as many as fit and then
     a link to the rest (`PeopleMore`). A page about one thing puts it first as a hero (`UiHeroModel`: its picture on a
     large tile, its name, a progress bar and when the next step comes) with the figures beside it. The blocks are
     public (`Shell`, `Profile`, `Lead`, `Hero`, `Figures`, `Attributes`, `Table`, `People`), so a page shaped
     otherwise is laid out from the same parts. Focus starts on the section in view, never on the close.
   - **Decision card** (`UiDecision`, `UiDecisionModel`): a desk session, a life event or a question box. Frostpunk 2's
     card, centred over the world dimmed to about half: the faces of the people it is about in a grid at the left (or an
     emblem for a decision about no one), the title in capitals, what is happening, numbered choices each with its
     consequences in coloured lines, and one or two buttons. A consequence's arrow follows the change (`Change`) and
     its colour says good or bad (`Tone`), so burnout easing is a green arrow down.
   - **Phone** (`UiPhone`, `UiPhoneModel`): the phone's home in the kit's tokens: a close at its head for the mouse,
     apps in their icons' colours on slate tiles, an "Up next" list and the page controls fixed in a bar at its foot,
     the arrow with no page that way dimmed.

   The four older shapes draw inside the app window and stay until their screens are converted. Start no new screen on
   them:
   - **List and detail** (`UiListDetail`): groups, a list and the selected record. Contacts uses it, in the window.
   - **Dashboard** (`UiDashboard`, with its page shapes): figures, tables and a side panel. The Economy app's views,
     Flare in the city and the tech firms' pages.
   - **Session steps** (`UiSessionSteps`): a desk session's steps, its question and its choices.
   - **Form** (`UiForm`): settings before an action, or a page of lines and actions.

   When a screen needs something no template has, add it to the kit as a reusable piece (a block, a component), with
   its sample and its checks, never as a one-off drawn in the screen.
3. **A runner**: a class implementing `IUiScreen` (and `IUiTyping` for a search) that owns the model and answers the
   presses. Register it in `UiScreens` with its template (`UiTemplate` names each one) and every key it labels with,
   in its area's file, `UiScreens.<Area>.cs` (`UiScreens.Contacts.cs`, `UiScreens.Kit.cs`, `UiScreens.Pages.cs`).
4. **Samples**: add the screen's sample to its area's `RealLifeOverhaul.Build/UiPreview/UiSamples.<Area>.cs` (the
   first screens' are in `UiSamples.cs`, the prototype's in `UiSamples.Cs2.cs`), so EconomyCheck lays it out in every
   language and frame, with the keys, a pad and the mouse leading. EconomyCheck picks a screen's sample by its template
   (`TemplateCases` in `UiChecks.cs`, where each template's own sample stands as a `template_` entry), so a screen
   whose page differs adds its own cases in its area's `UiChecks.<Area>.cs`, as the studio's and the population's do in
   `UiChecks.Pages.cs`. `preview.ps1` renders the entries of each area's `UiPreviewRunner.<Area>.cs`, each over the
   game's picture it stands on (`Backdrop`; an entry without one stops the run).
   Each area is a partial method in the three lists (`UiScreens.Gather`, `UiPreviewRunner.Screens`, `UiCases`), so
   jobs on different areas never edit one file; a new area adds its line to the three lists. The registry parts and the
   samples are linked into EconomyCheck by a pattern, so a new area's files need no line in its project.
5. **A host**: the game file that shows it creates a `UiHost` over the full menu (as `ContactsKit` does). It binds it
   to the game's input (`UiGame`) and the motion setting. The look it still passes (`UiDirector.Dark`) changes nothing,
   as the kit has one look.
   The game keeps its full menu and apps switched off until they open, so the host binds over them switched off: every
   search of the scene passes `true` or `false` for switched off objects, the canvases are walked with
   `GetComponentsInParent<Canvas>(true)` and `UiBindRules`, and a screen not built yet is looked at again
   (`UiBindRules.Wait`), never given up for the session. EconomyCheck fails a search that does not say.

## The parts a screen is made of

Use these, and never draw a part of your own. All are in `UiDraw.Chrome.cs` unless named.

- **Window** (`Window`): an app's slate window over the world at about 86% by 84% of the screen, with its title strip.
  It returns the panel the page goes in. Its close button runs `UiDraw.CloseCommand`, which the host takes as Back, so
  a screen with pages steps back before it closes.
- **Title strip** (`TitleStrip`): the icon at the left, the title centred in cyan capitals, a close button at the
  right and a hairline under it. Every panel wears one.
- **Status bar** (`StatusBar`): along the foot, a row of app tiles (the open one filled cyan) and a row of pills: the
  time and the game's speed, the season, the place, the money, the needs as meters and the alerts, from `UiChrome.Bar`
  (`UiStatusModel`). A field left empty leaves its pill out. The keys' prompts stand at the right of the app row while
  the keys lead. A template's last call, `UiDraw.PromptBar`, draws it. In the game it shows the time, the day and the
  money (`UiGame.Bar`). The needs, the season and the apps wait for the HUD wave.
- **Icon tiles and buttons** (`Tile`, `IconButton`): an icon in its own colour on a neutral slate tile, as Cities:
  Skylines II's tool buttons (a warm colour faded on slate turns brown), or the cyan fill with dark ink when it is the
  one chosen. On a tile, draw the icon's filled cut (the last argument of `UiDraw.Icon`).
- **Icon colours** (`UiIconTints.Of`): each icon has one colour, set once, the same on every screen and on the style
  sheet: an app or a section its category's, a genre its own, a state the state's. Pass `UiRole.None` as an icon's tint
  to get it, and give each role one glyph. Never recolour a glyph to say a state: tell it with a meter, a chip, a ring
  or the warning's own glyph. Only a trend arrow takes good or bad's colour. A portrait's ring is the person's mood.
- **Rows and links** (`InfoRow`, `SectionRow`, `Band`): compact rows, the label in grey-blue at the left and the value
  in soft white at the right, or a cyan link with its magnifier that opens the thing or moves the camera. A section
  stands on a darker band under its label in capitals. Every link's mark stands `LinkGap` (a grid step) from its words
  as drawn, cut to their room first (`UiDraw.Links.cs`: `LinkValue`, `LinkRow`); a row that is a link is a pair row
  high among an info panel's rows and a control high under a block of a window, and a person among those rows is
  `PersonRow`, two pair rows with the face's left edge on the labels. A list of people or threads keeps `ListRow`.
- **Pills and chips** (`Pill`, `Chip`): a status pill with its icon, and inZOI's soft chip with a dot in its tone.
- **Portraits** (`Portrait`): a round face painted from the person's name (`UiPortraits`, kept by
  `UiSprites.Portrait`, 160 at most), with a status ring in a tone. No portrait art ships.
- **Charts and meters**: `Donut` (slices in the chart hues) with its `Legend`, `Scale` (a map legend's steps), `Meter`
  (a thin bar in a tone), `GradeBar` (an attribute graded red to green; a block of them shares one label column from
  `LabelColumn`, so their tracks start at one edge), `StatBar` (the same thin graded bar under its label), `Stars` (a
  rating by halves, gold and grey) and the spark line (`UiSpark`).
- **Colours for data**: `UiTokens.Chart(i)` for a slice, `UiTokens.Grade(value)` for a graded figure, `UiTokens.Ramp(step)`
  for a map coloured by a figure and its legend (one violet strengthening with the figure, never the chart's hues and
  never white), and the Cat roles
  (`CatFood`, `CatRetail`, `CatOffice`, `CatIndustry`, `CatTech`, `CatServices`) for a business family's icons and
  slices, never for a large fill.
- **The game's own icons**: `UiIcons.Own` lets the game's picture stand in for an icon where the game has one. In the
  game `UiGame.OwnIcon` sets it, for info, notifications, warning, set destination and calendar today. To map another,
  add it to `UiGame.OwnIconNames` and `OwnIcon`. Offline the Phosphor icon draws, so the checks and the previews see
  the kit's own.

## The page shapes

A page that is not "figures and a table" takes the shape its job has. Each is a block of the app window's page: Shell
draws the window and the app's sections, the shape fills the room it gives (`Lay(s, at, model, x, y, w, h, keys)`).
Their samples stand in `UiSamples.Grids.cs`, each in the app it is made for, with previews in
not-in-repo/previews/ui/kitgrids.

- **Diary** (`UiDiary`): a month as a diary page, seven columns in the game's order, a day's things as short lines
  behind their kinds' glyphs and "+n more", today on the cyan, the chosen day's money and things beside it as rows that
  lead where each is dealt with; Q and E turn the month. For the Calendar and a campus term. Not for a list of dates
  with nothing to see across a week: use a table.
- **Board** (`UiBoard`, `UiBoardRules`): a week's or a day's shifts on an hour ruler, lanes led by a face or a tile,
  bars with the lane's colour at their edge, the closed hours darker, the now line. It only draws and reports: a
  screen implementing `IUiDragging` takes the pointer's drags (`UiBoardRules.Drag`, `Paint`, `EdgeScroll`) and
  `IUiSteering` the keys' steps (`Nudge`; Shift or a shoulder resizes), and sends each edit to the game's schedule or
  the mod's rota. For BizMan's schedule, the Company rota, the Planner's week. Not for a timeline that cannot be edited
  by the hour: a track or a table.
- **Reader** (`UiReader`, `UiReaderRules`): a long document at a reading measure with its bold names in the strong cut,
  notes, pictures and links, its sections numbered down the sidebar's place (`LayWithContents`), the page's picture,
  facts and pages it leads to beside it; the wheel, Page Up and Down (`IUiPaging`) and the d-pad's steps through its
  anchors scroll it. `UiReaderRules.Parse` reads the Guide's "# " and "- " and a help page's markdown. For the Guide,
  the Newsstand's articles and the game's help. Not for a few lines: a note or an empty state says them.
- **Settings list** (`UiSettingsList`): every section of a settings page as bands of rows, a name, its sentence and
  its control, a main switch greying its section, a search marking its letters, the nearest preset and the way back
  and forward, the groups down the sidebar's place (`LayWithGroups`, a long group name on two lines). Hundreds of rows
  at a screenful's cost. For the mod's settings and the game's Options in the city. Not for a form before an action:
  that is a decision card or a short page of rows.
- **Conversation** (`UiConversation`): who is talking, the thread at its foot under its start line, a form slot, the
  choices numbered with their consequences (the game's dialog buttons) or the composer; in a window, or docked over
  the world at the right third (`LayDocked`) for talk face to face. For Contacts, Flare's messages and the game's and
  the mod's dialogs. Not for one question with no one behind it: the decision card.
- **Message list** (`UiMessageList`): threads under their bands with a face or a tile, the last words, the time, an
  unread badge and presence, and the search. Beside a conversation in Contacts and Flare.
- **Feed** (`UiFeed`): posts at a reading width beside the chosen account or tag as a short profile, never empty. For
  Flare.
- **Edition** (`UiEdition`): the lead story as a hero beside the markets' rows, the stories in balanced columns under
  their sections' bands, each whole in one column, and the notices. For the City Ledger and the Newsstand.

The settings list's controls are drawn by stand-ins in `UiSettingsList` (`ControlSwitch`, `ControlSlider`,
`ControlChoice`, `ControlText`, `ControlKey`, `ControlStepper`) with the parameter lists of the kit's own controls, which
take their places.

## Previews

`RealLifeOverhaul.Build/UiPreview/preview.ps1` renders the samples with the real fonts. It needs the font bundle built
(`UiAssets/build.ps1`) and Unity 2022.3.62f2. `-Set cs2 -Backdrops <folder>` renders the prototype's screens over the
game's own street screenshots in that folder, which stay local and are never committed, at 1080 and 720, with a mouse
hover shot and a keys shot. `-Out` chooses where the shots go: the prototype's are in `not-in-repo/previews/ui/cs2/`.

## Review checklist

Before a screen is done, check each line in the previews at play distance: over the game's streets, at 1080 and 720,
with the mouse resting on a target and with the keys leading.

- [ ] **Game UI, not web UI**: panels stand over the world with the city round them, every panel wears its title
      strip, rows are compact, a section stands on its band, and nothing reads as a dashboard. No page scroll.
- [ ] **Pictures carry the meaning** (RLO-UI-017): every section, tab, category and status has its icon on a tile in
      its colour, every person has a portrait, and a share or a spread has a chart where a picture helps. No table of
      words alone.
- [ ] **The look** (RLO-UI-002): Overpass only, never a serif. No pure white, and no brown, red or cream surface. The
      cyan only for titles, links, the focus ring and what is chosen, and a category's colour only on its icons and
      slices.
- [ ] **Over the world**: the street never reads through a panel, and every word reads over the brightest street
      behind it.
- [ ] **Flat**: no gradient, glow, blur, soft shadow or frost (RLO-UI-015).
- [ ] **One kit** (RLO-UI-018): the screen stands on a template, and anything new it needed went into the kit as a
      reusable piece.
- [ ] **One primary action** to a layer, and nothing on screen that is not needed.
- [ ] **Mouse first**: a hover is a still fill with no ring and no sound, the focus path reaches every target from
      the first, the d-pad stays in its column, and nothing is hover-only.
- [ ] **Keys**: while the keys lead, the status bar shows the bound keys and the way back. With the mouse no prompt
      shows, and the close button goes back. One key does one thing, Enter presses the focus, choices are on numbers,
      F is the main action and P the second.
- [ ] **Alignment**: everything is on the 4 px grid and spacing comes from the tokens' steps, with no hand-picked gaps.
- [ ] **No dead space**: frames fit what they hold, tables show whole rows and a short thread sits at the foot under its start line.
- [ ] **The window uses its room** (RLO-UI-016): its page runs across the window, a form of several sections runs in
      two columns, a short table's rows breathe, and nothing floats in the middle.
- [ ] **Indicators** agree with their values: the arrow follows the sign, and the colour says good or bad for the player.
- [ ] **Words**: every label fits in all eleven languages, empty states are in words, nothing is under 13 px, and a
      screen keeps to three sizes of type.
- [ ] **Contrast**: AA over the world.
- [ ] **Motion**: 80 to 200 ms feedback, counters tick, bars fill, selections snap, Calm and Off are respected.
- [ ] **Sound**: the game's own UI sounds on a move, a press, a tab and an error.
- [ ] **The game's logic**: a redrawn game screen presses the game's own buttons, steps aside for its question box,
      and falls back on a fault.

Every line a machine can check is checked by EconomyCheck (`UiChecks.cs`, `UiPageChecks.cs`, `UiIconChecks.cs`). The
rest is the review.

## The pieces

- **Look**: `UiTokens`, `UiColour`, `UiFontMetrics(.Data)`, `UiTextRules`.
- **Icons and faces**: `UiIcon`, `UiIcons`, `UiIconRaster`, `UiIconPaths.Data` (written by `UiAssets/scripts/icons.py`,
  never by hand), `UiPortraits`.
- **Layout**: `UiScene`, `UiDraw*` (`UiDraw.Chrome` holds the window, the status bar and the parts above), the
  templates, `UiFrame` (where a screen stands over the world, and what the status bar says).
- **Rules**: `UiFocusMode`, `UiFocusRules`, `UiCheckRules`, `UiMotionRules`.
- **Registry**: `UiScreens`, `IUiScreen`.
- **Drawing**: `UiView*`, `UiHost`, `UiTweens`, `UiSprites`, `UiFonts`, `UiSpark`, `UiHit`.
- **Screens**: `Pages/`, the overhaul's pages (`*Pages`, game-free) and the screens that run them with their features'
  directors, and `SessionKit` over the game's own desk panel.
- **The game**: `UiGame`, `UiText`, `UiDirector`, `UiGuide`, `Contacts/`.

Everything except the game files is game-free, so the offline preview draws exactly what ships.
