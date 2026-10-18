# Meta: the Guide, presets, the options page and the save tools

## In a sentence

The mod's part about itself: the feature registry, the in-game **Guide** app and the welcome notice, the realism presets, the options page in sections with its search, and the tools that clean a save for removing the mod.

## Where the code is

- Folder: [`Scripts/Meta`](../../Assets/Mods/RealLifeOverhaul/Scripts/Meta/). The files are listed in the code block below.
- Read first: `Features.cs` (the registry), `FeatureList.cs` (every feature, one statement each), `OptionsLayout.cs` and `SaveTools.cs`.

## How it talks to other features

- It names no feature outside `FeatureList.cs` and its parts: everything else reads the registry, so a feature that registers appears in the Guide, the presets, the options page and the save tools. How to register one is in [Adding a feature](../contributing/adding-things.md#adding-a-feature-with-its-guide-page-and-presets).
- `Meta.Dev` is the **Developer diagnostics** switch every logger in the mod goes through; see [Logs and debugging](../reference/logs-and-debugging.md).

`Scripts/Meta` is the mod's part about itself.
It names no feature outside `FeatureList.cs` and its parts: everything else reads the registry they fill, so a feature that registers appears in all of it.

```
Meta/
  Features        the registry: Feature (fluent), Features.Add / Find / OwnerOf...; [DescribesFeature] for self-registration
  FeatureList     every feature, one statement each: gameplay and tools here, .Life and .Economy the rest; a new one at the end of RegisterAll
  SettingsTable   every saved option, found by itself: Settings' "XId" + "X" fields (id, kind, shipped default,
                  captured at the top of the first Settings.Load), the rows of the options page (label, range),
                  and what a feature declares for options kept elsewhere (performance tweaks, looks)
  Presets         Relaxed / Standard / Realistic / Hardcore / Custom, reset everything, the settings file
  OptionsLayout   puts the finished options page in order: sections, test tools, the buttons at the top; the set-up check
  OptionsLayout.Drawing, .Buttons, .Book  its drawing (greyed rows, counts, open and closed), its own buttons and the set-up check, and the Settings app's view of its rows
  OptionsLayout.Search    the search box under the page's first header
  OptionsLayout.Captions  the word on each button of the page ("Open", "Close", "Run", "Apply")
  MetaUi          the mod's own canvas (above the menus, off unless a panel is open) for the options page's messages and questions only
  Guide           the guide's pages and texts (topics, facts, mark-up) and its phone contact
  GuideApp        the Guide as an app on the game's phone (GameUi.PhoneApp), asked for from the options page, the contact and the welcome notice
  GuideView       the app's panel in the game's frame (GameUi.AppShell): a table of pages, a table of the lines of a page
  Welcome         the first-run / after-update notification, with a click that opens the Guide on "What changed"
  Changelog       what changed per version, for the Guide's What's new page
  SaveTools       prepare a save for removing the mod (.Steps: the backup and the purge), reset and repair (.Reset), the check after each load (.Load)
  MetaCity        Meta's part of a city load; Dev: the "Developer diagnostics" switch
```

## What the mod API allows on the options page

One flat page for all mods (`ModOptionsViewController`), built from `ModOptions`: header, toggle, slider, dropdown, button, splitter, and `AddCustom` (a row that draws itself under the page's content object).
There is no text block, no sub-page and no way to scroll to a row.
Toggles, sliders and dropdowns call their callback with the saved value every time the page is drawn, which is why the presets are buttons and not a dropdown.

`OptionsLayout` therefore rearranges the finished list: sections are a button followed by the feature's rows, and a row that draws nothing (`RowTap`) on either side tells it which of the game's row objects are that section's, to switch on and off.
Open/closed is kept per section in PlayerPrefs (`m:<mod>:ui_open_<feature id>`).
Every row is re-made with the shipped value as its default, so the game's own "reset" resets (the chain passes the loaded values).

"Test tools" is a section of sections: one group per feature that has test buttons, in the order of the sections, each a button ("Tests:  Crime and police stops   (14)") that opens and closes like a section and is closed until asked for (`m:<mod>:ui_open_tests_<feature id>`).
A feature that is not registered is named by its header without "Real Life Overhaul - " in front.
"Open or close every section" leaves the groups as they are.

A section's button says what is in it after the name, through the label's `Suffix` (written when the page is drawn and when it changes, never per frame): "Garage   (9 options, on)". "on"/"off" is there only for a section with a row that switches the whole feature, which is the toggle whose label key ends in `_enabled`.

The game calls a toggle's callback with the saved value when it draws the row and on every click, and that is where the button hears of it.

The game's button row has two texts: the label beside the button, which `ModOptionsButtonControl.Initialize` fills from the row, and one on the button itself, which it never fills, so the button shows what its template was saved with ("Your text here").
`OptionsLayout.Captions.cs` writes that second text.

A row that draws nothing stands after the mod's last row and goes once through the rows of this drawing:

- a section's button says "Open" or "Close" (and "Found" while a search is on, when clicking it does nothing);
- "Open or close every section" says "Open all" or "Close all";
- a row that changes a key says "Change";
- every other button says "Run" unless the table `CaptionByLabel` names another word for its label key ("Apply" for a preset, "Reset", "Export", "Import", "Check").

The text is the first `TMP_Text` under the row's `Button` that is not the label: if it has a `TextLocalizationComponent` its `Key` is set, and if not the translated word is written and written again when the language changes.
If no such text is found the buttons are left as the game draws them and one line is logged.

A slider shows its number only if it was given a `valueLabelKey` (`ModOptionsSliderControl.Initialize` hides the value otherwise; the text gets the number as `{value}`).
Every `AddSlider` passes `SettingsTable.SliderValue` ("{value}") or `SliderPercent` ("{value}%"), and a slider re-made without one gets `SliderValue`.
If the page cannot be put in order it is registered as built, with the same numbers and words (`OptionsLayout.AsBuilt`).

## Search

The row under the page's first header is a text field (`OptionsLayout.Search.cs`).
The page has no text field of the game's to copy, so it is a `TMP_InputField` built in code that takes its width from the header above it and its height, font, letter size and button shape from the "Open the Guide" button below it.

- **Matching.** From two letters on.
  Not case-sensitive; the words typed may be in any order and each must be somewhere in the row's label, or in its section's text: the section's name, the feature's `About` sentence, and the other words for it in the `SearchWords` table.

  So a word that names a feature shows all of its rows, and a word only in a label shows that row ("bus" shows the whole of the transport section and its tests; "timetable" shows the rows that say timetable).

- **What is shown.** While a search is on, sections count as open, rows that do not match are switched off, a section's button is there only if one of its rows is, and the test tools are searched whether or not "Show test tools" is on or their section is open.
  The buttons at the top (guide, presets, export) are found by their labels; the headers between the parts are hidden.

  A line under the box says how many rows were found.
  Clicking a section's button does nothing meanwhile.
  Emptying the box (the x, Backspace, or Esc while typing) puts everything back as it was left: the search never writes what is open and closed.

- **Cost.** No `Update`.
  The field's value-changed event does the work: the lower-case texts are made on the first letters typed after each drawing of the page (the page is drawn again after a preset, and the language may have changed), a keystroke is one pass of `IndexOf` over them and `SetActive` only on the rows whose state changes, and the page lays itself out once.

  The scroll view is put to the top of the mod's part by a component that is enabled for two frames after a new search.

- **Keys.** While the field is selected `Keys.Suspended` is on (released on deselect and when the row is disabled or destroyed), and the game leaves its own shortcuts alone because a UI object is selected (`GameManager.HasInputSelected`).
  In the city Esc is the game's `CancelButtonHandler`: with the field selected it only takes the selection away, and the box is emptied in the same press; the next Esc closes the page.

  On the main menu `Options.Update` closes the page on Esc whatever is selected, so there one press does both.

- **Failing.** Anything thrown hides the box for the session, logs once ("Meta: the search box ... is hidden") and shows the page as the sections have it.
- **Not kept.** The box starts empty every time the page is drawn.

Other words for a feature are one line of `SearchWords` at the top of `OptionsLayout.Search.cs`: who (the feature's id, or the header key of its group while it is not registered; both may be given) and the words, lower case:

```csharp
new[] { "fuel reallife_fuel_header", "petrol gas station tank litre" },
```

Only words that mean the whole feature: each one shows every row of it.

The game's own help (F1) draws any page whose text is the locale key `help_<slug>_content` (`HelpPageRenderer.RenderPage(slug)`), but lists only the pages of its `helpstructure.json`, and that key cannot carry the mod's prefix; the guide is therefore a panel of the mod's own.

## Presets

Standard is always an option's default; the other three are declared with `Feature.Preset`.
Today:

| option | Relaxed | Standard | Realistic | Hardcore |
|---|---|---|---|---|
| `crime_against_player` | off | on | on | on |
| `player_offences` | off | on | on | on |
| `crime_frequency` | 1 | 2 | 3 | 5 |
| `job_pay` | 125 | 100 | 100 | 80 |
| `economy_estate_rent` | 150 | 100 | 100 | 80 |
| `life_frequency` | 4 | 5 | 6 | 8 |
| `police_presence` | 4 | 5 | 6 | 8 |
| `patrol_frequency` | 2 | 3 | 4 | 6 |
| `player_knockdown` | off | on | on | on |
| `ped_incidents` | off | on | on | on |
| `accident_frequency` | 1 | 2 | 3 | 4 |
| `crash_consequences` | off | on | on | on |
| `police_pursuit` | off | on | on | on |

A preset writes the PlayerPrefs keys the options page writes, calls `Settings.Reload()` (which is `Load` and then the `Settings.Changed` event) and has the page drawn again.
The settings as they were go to `<persistentDataPath>/RealLifeOverhaul/settings_before_preset.txt` first; "Export" writes `settings.txt` there and "Import" reads it (`id=value` lines, key bindings included).

## Save tools: what is removed, and how that was checked against the game

Read in the decompiled game: `GameInstance` (the save), `SaveGameManager.Save` (fires `GlobalEvents.onSaveGame`, then serialises on the calling thread), `SaveGamePathHelper` (`SaveGames/<version>/<characterId>/<name>.hsg|.json`, `.meta`, `.jpg`; the list of saves reads the files of each character folder and never a folder inside one), `Contact` (`GameInstance.Contacts`; a contact's messages are its own `messagesQueue`), `HappinessHelper`.

| in the save | the mod's | prepare for removal | reset data |
|---|---|---|---|
| `modData` | keys starting `reallife:` (and any a feature declares) | removed | removed |
| `happinessModifiers` | types starting `reallife_happinessmodifier_` | removed (also stripped by `ModState` on every save) | removed |
| `Contacts` | ids starting `reallife-` | removed with their messages | kept |
| `notifications` | keys starting `reallife_` | removed | kept |
| `Transactions` | types starting `reallife_transaction_` | kept and counted | kept |
| `VehicleInstances` | the hired job van | taken back by Jobs' own clean-up; reported if still there | same |
| `PlayerDefaults.contactsLastName` | the game's note of the contact last opened | set to none if it names a removed contact (the Contacts app lists nothing otherwise) | untouched |

A mood type the game does not know breaks the load of a save without the mod, and shows as nothing with it. That is why the mod's moods are stripped from the save on every save, and why a mood type this version no longer registers is taken out of the list a save was loaded with (`SaveTools`).

"Prepare" does this, in order:

1. It backs up first, twice: `RealLifeOverhaul_backup_<time>` inside the character folder, and `<persistentDataPath>/RealLifeOverhaul/backups/<character>/<time>`, because the load menu deletes a character folder whole with the character or its last save. Either copy failing means no change.
2. It runs every `Cleanup` and destroys the mod's root object and the objects features name (their `OnDestroy` restores what they changed, as on unload).
3. It removes the above, and attaches a handler at the end of `onSaveGame` that removes it again inside the save event, so nothing a feature writes there reaches the file. The moods come out and the handler goes on in the very frame the features are destroyed, before any autosave can run.
4. It saves under the same name, and shows and writes a report.

It refuses in a vehicle and during a hospital trip, custody, sleep or an activity.
There is no "all saves" variant.

## The Guide and the welcome in the game's own interface

- The Guide is an app called Guide on the game's phone, built as the Planner is (`GameUi.PhoneApp.Install`, a panel from `GameUi.AppShell`, an icon from `GameUi.AppIconPainter`, one more tile on the phone's pages).
  Tab "Pages" lists the pages (the features, Presets, Known limits, Credits, What changed); a click opens tab "Reading", which has the page's text a line to a row in the game's table, headings in the game's yellow and bullets as "-".

  The left box says which page and has "All pages".
  The texts, the `{facts}` and `Changelog` are as they were (`Guide.Fill`, `Guide.Check`).

- Asked for from the options page, the contact's call or a click on the welcome notice: the app opens at once if no menu of the game's is up; under one a notification says it opens when the menu is closed, and it does (a quarter-second timer that lasts only while the request waits).
  In the main menu there is no phone: a notification says so.
- The welcome is the game's notification ("Real Life Overhaul 0.1.5: open the Guide on your phone to see what changed"), once per version; a click opens the Guide on "What changed".
  The four presets it used to offer are on the options page.
- Removed: the guide's keyboard search, the welcome panel, and their canvas panels.
  `MetaUi` stays for the options page's messages and questions (reset, import, preset questions, the set-up check): that page is also in the main menu, where there is no phone and no HUD, so `MetaUi.AnyOpen` stays and the camera still counts it.
- If the phone is not built as expected, the app is left off with one line in Player.log; the texts remain in the mod.
- Log lines: "Meta check (guide app): the Guide is on the phone: PASS", "opened on the list of 40 pages, 40 rows ...: PASS", "Meta check (welcome notice): ...: PASS".
- Not checked in the game: the app's look (row heights are a guess at the line length: a screenshot of a long page settles it), the icon, the notice's click, and the wait for the menus.

## Test paths

- Guide: options page "Open the Guide on your phone" (the app opens when the menu is closed); the phone contact "Real Life Overhaul Guide"; "Check the mod's set-up" renders every page and lists unfilled `{facts}` and missing texts.
- Welcome: "Show what changed" (opens the Guide on that page); or delete `m:<mod>:meta_seen_version` from PlayerPrefs.
- Presets: the four buttons, then the guide's Presets page (the "Now" column) and the header "Preset in use".
- Options page: open and close sections; "Show test tools" off hides the last section.
  Every button has a word on it ("Open", "Close", "Run", "Apply"), never "Your text here", and every slider its number beside it.
  Each section's label ends in its count ("(5 options, on)"); switch a feature's first toggle and the "on" becomes "off" at once.

  Open "Test tools": one closed group per feature; open one, close and reopen the page: it is still open.

- Search (main menu and in a city, both): with "Test tools" closed, click the box under "Real Life Overhaul: Start here" and type `bus`: the page jumps to the top, only the transport section and its group of tests are left, the line under the box counts them.
  Type on to `bus stop`: the rows that say so are left, and pressing a button among them works.
  `stop bus` (two words, any order) finds them too.

  `zzzz` says "Nothing found for 'zzzz'".
  Try `police`, `fps`, `bike`.
  A toggle and a slider among the results still work.
  While typing `v`, `f`, `b`, `x`, the camera, interaction and garage keys must do nothing, and nor must the game's own (Space, M, Tab).
  Empty the box with the x: every section is as it was before.

  In the city, Esc while typing empties the box and leaves the page open; on the main menu it also closes the page.
  Switch "Show test tools" off and search `test`: the tests are still found.
  `Player.log` must have no "Meta: the search box" line.

- Save tools: the three buttons on a save you do not need; `Player.log` lines start `Meta:`; the report is also in `<persistentDataPath>/RealLifeOverhaul/`.
- Diagnostics: switch "Developer diagnostics" off: F8/F9 do nothing and only warnings and errors of the features reach the log.
