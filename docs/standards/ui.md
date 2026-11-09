# The RLO interface

The rules for every screen the mod draws on its own interface kit (`Scripts/Ui`), in the look the owner chose on
1 November 2026, after Cities: Skylines II. The owner's words are the brief:

- "The most important thing in the overhaul is the UI and UX. It must look absolutely beautiful at the same time, not cluttered."
- "Every single UI that is being built, must be the same and looks absolutely exquisite. No slop like, no weird spacing etc."
- "It must be like a game UI, not a web UI."

The kit was first drawn as the City Ledger, a newspaper's look, and the owner turned it down the same day: "it looks
like AI slop, especially the brown red color theme... like I'm looking at a dashboard instead of a game UI. Something
is missing and something feels incomplete". He chose Cities: Skylines II as the base: "Cs2 is good ... mix and match".
A prototype of six screens in that look followed, and he approved it on 1 November 2026: "Yes this works. This looks
very good."

The kit's tokens, components, templates and checks are in [`Scripts/Ui`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/), and
its README holds the way to build a screen and the review checklist. [ADR-0007](decisions/0007-rlo-interface.md) records
why the mod has an interface of its own, and [ADR-0008](decisions/0008-cs2-interface-look.md) why it looks as it does.
The direction itself, with its references, is `not-in-repo/design/DIRECTION_CS2.md`. EconomyCheck's `UiChecks.cs` lays
out every registered screen and the prototype's screens at every frame the game's canvas gives it (16:9, 16:10, 4:3,
21:9), in all eleven languages, with the keyboard and a pad leading and with the mouse, and checks each rule below that
a machine can check.

### RLO-UI-001: One source for every look

**MUST.** Take every colour, size, space, radius, stroke, duration and text style from `UiTokens`, by role and named
style. A screen never writes a colour, a font, a font size or a pixel figure of its own.

Why: one change of the tokens changes every screen, and nothing drifts. Checked by: EconomyCheck (`ui sources`), review.

### RLO-UI-002: Cities: Skylines II's slate

*Replaced 1 November 2026: this was "The City Ledger".*

**MUST.** Draw in Cities: Skylines II's slate:

- Overpass alone, in Regular, SemiBold and Bold, with tabular figures. Never a serif.
- One dark look. A screen has no background of its own: the game's world is the ground, and slate panels stand over it.
- A thin title strip on every panel and window: its icon at the left, its title centred in cyan capitals, a close
  button at the right.
- Compact rows: the label at the left in the secondary grey-blue, the value at the right in soft white, or a cyan link
  with its magnifier. A section stands on a darker band under its label in capitals, with no rule between its rows.
- One cyan accent for titles, links, the focus ring and what is chosen. Chosen is a cyan fill, never a border or a bar
  beside it: a tab or a tile filled cyan with dark words, a row at a fifth of the cyan.
- Green, amber and red for good, a warning and bad. Each icon has one colour and each role one glyph, the same
  everywhere (`UiIconTints`), on a neutral slate tile. A state is told by a meter, a chip, a portrait's ring or the
  warning's own glyph, never by recolouring an icon; only a trend arrow takes the colour of good or bad for the player
  (RLO-UI-011). A portrait's ring is always the person's mood, and presence is a dot or a chip. A business family's
  colour goes on its icons and its chart slices, never on a large fill.
- A chart takes the eight chart hues in order (`UiTokens.Chart`), a graded figure or an attribute bar takes the five
  grades from red to green (`UiTokens.Grade`), and a map coloured by a figure takes the five steps of the ramp
  (`UiTokens.Ramp`): one violet whose colour strengthens with the figure and never runs to white.
- Corners of 4 px (2 px on small parts), hairlines only under a title strip and between sections, and no pure white
  anywhere.
- At most three sizes of type on a screen, the frame's own words aside.

Why: the owner's choice of 1 November, after he turned the City Ledger down ([ADR-0008](decisions/0008-cs2-interface-look.md)).
Checked by: EconomyCheck (`ui tokens`, `ui fonts`, `the chart's hues stay apart`, `three sizes of type at most`).

### RLO-UI-003: The 4 px grid

**MUST.** Place every box on the 4 px grid relative to its parent, with sizes on the grid. Only text centred on a box,
a share of a width (a segment, a knob) and decoration are exempt.

Why: alignment is what reads as care. Checked by: EconomyCheck (`every box on the 4 px grid`).

### RLO-UI-004: Text fits in every language

*Amended 1 November 2026: the smallest text was 15 px.*

**MUST.** Make every translated label fit whole in all eleven languages, wrapping onto two lines where the component
allows it. Player data (names, messages) may end in an ellipsis. Every style is at least 13 px at 1080p
(`UiTokens.MinText`).

Why: a cut label looks broken, and 13 px is the smallest Cities: Skylines II sets, on its section labels in capitals.
Checked by: EconomyCheck (`every label fits whole`, `no text under the smallest size`).

### RLO-UI-005: Every screen is registered

**MUST.** Register every screen in `UiScreens` with its template, the class that runs it and the keys of its labels.
Every key a screen uses is named in its entry and is in en, pt and zh-cn.

Why: the checks can only hold what they can find. Checked by: EconomyCheck (`ui sources`, `every key ... is named in its entry`, `ui texts`).

### RLO-UI-006: Contrast over the world

*Replaced 1 November 2026: this was "Contrast in both looks".*

**MUST.** Give every text and icon at least 4.5:1 against what is under it (3:1 for large text and the focus ring), in
the one look. Where a surface lets any of the world through, measure it over a bright street (`UiCheckRules.World`), as
the world is the ground under every panel.

Why: WCAG 2.2 AA, at play distance, over any street a panel stands on. Checked by: EconomyCheck (`stands out`, `ui tokens`).

### RLO-UI-007: Mouse first, every target on the keys

*Replaced 1 November 2026: this was "Focus first".*

**MUST.** Let the mouse lead: the target under the pointer takes a still fill, and the focus ring shows only while the
keys or a pad lead. A screen opened with a pad has its ring from the start. Make every target reachable by the d-pad
and the arrows from the screen's first target, and let nothing depend on hover.

Why: a city game is played with the mouse first, and a ring that follows the mouse jumps on every hover. A pad and the
keys must still reach everything. Checked by: EconomyCheck (`the d-pad reaches every target`, `ui input`).

### RLO-UI-008: Prompts while the keys lead

*Amended 1 November 2026: this was "A prompt bar on every screen", shown whatever input led.*

**MUST.** While the keys or a pad lead, show the keys bound now on the status bar (key caps, or the pad's glyphs while
a pad is used), and give the way back among them. While the mouse leads, show no prompts: the close button on the
window's title strip is the way back (the host takes it as Back), and on a decision card the button that answers Back.
The prompts stand as chips in the lane over the status bar (`UiFrame.PromptLane`), never on its strip: the app row and the
pills do not move or give way to them, and a window ends above the lane.

Why: the player never has to guess a key, and a mouse player is not asked for keys they do not use. Checked by:
EconomyCheck (`a prompt bar that says how to go back`, `with the mouse leading, the status bar shows no prompts`, and the
`kit prompts` checks: no chip over the bar, a window or another chip, and no piece of the bar moved by the prompts).

### RLO-UI-009: One key does one thing

**MUST.** Let a key do one thing on a screen:

- A target owns the key whose cap it shows, and that key presses it from anywhere on its layer.
- Enter always presses whatever has focus, so no target shows it.
- Choices are on 1, 2, 3 and so on, F is the screen's main action and P its second.

Why: a key that means two things is a trap. Checked by: EconomyCheck (`one key does one thing`).

### RLO-UI-010: No dead space

*Amended 1 November 2026: the window's and the phone's own body are chrome now, not frames.*

**MUST.** Make a frame fit what it holds. No frame may hold more than 48 px that nothing covers, above, between or
under its lines. A list or a thread is a frame of its own, and an empty state fills the room it is given. An app
window's and the phone's own body are chrome, fixed in size, and not frames: the panels, lists and cards they hold are
checked.

Why: a hole in a frame reads as unfinished. Checked by: EconomyCheck (`no frame holds dead space`).

### RLO-UI-011: Indicators agree with their values

**MUST.** Point a trend arrow the way its change's sign goes, and run a sparkline the same way. Colour shows whether
the change is good or bad for the player (rent up is red, arrow up).

Why: an arrow against its number tells the player two things at once. Checked by: EconomyCheck (`no indicator contradicts its value`).

### RLO-UI-012: One primary action, a calm screen

**MUST.** Keep one primary action to a layer and at most 48 targets on a screen. Every list scrolls inside a fixed
frame, and a screen never scrolls as a page.

Why: management density without clutter. Checked by: EconomyCheck (`one primary action a layer, and no crowd`).

### RLO-UI-013: Game timing

**MUST.** Keep feedback, focus, selection and arrivals to 60 to 240 ms, and a fill to at most 600 ms. The Calm
setting is slower and shorter, and Off is still. Nothing allocates per frame, and tweens come from one fixed pool.

Why: a game answers at once. Checked by: EconomyCheck (`ui tokens`, `ui motion`, `ui timings`).

### RLO-UI-014: The game's logic stays the game's

**MUST.** When the kit redraws a game screen, keep the game's own object alive underneath. Read its data and press its
own buttons. Fall back to the game's screen on any fault, and step aside for its question boxes, calls and anything it
alone can show.

Why: the game's rules and saves stay correct, and a game update costs a screen, never a save. Checked by: review, ContractCheck (the game's members the kit names).

### RLO-UI-015: Flat surfaces, no effects

*Amended 1 November 2026: it allowed no translucency at all.*

**MUST NOT.** Use a gradient, glow, blur or soft shadow anywhere in the kit. Draw flat surfaces with crisp hairline
edges, and use colour sparingly and on purpose. A surface over the world is flat slate, solid enough that the street
does not read through it: a flat alpha at most, never a blur or a frost.

Why: the owner, 1 November: "Gradients don't look nice. I want a proper themed thing." The game blends in linear light,
where a street's crossings and signs read through a thinner panel and break up its words. Checked by: EconomyCheck
(`no gradient, glow, blur or soft shadow in the kit`), review.

### RLO-UI-016: The window uses its room

*Replaced 1 November 2026: this was "The page uses its screen", a full-screen page under the masthead.*

**MUST.** Draw no masthead and no full-screen page. Open an app as a window at about 86% by 84% of the screen, centred
over the world above the status bar, with the city visible round it (`UiFrame.Window`). Its page runs across the
window's content, leaving no more than 64 px of nothing at the left, top or right, and at 16:9 its frames and words
cover at least a quarter of that room. A form of several sections runs in two columns and a short table's rows breathe.
A short page may end where its content ends, but it never floats in the middle. The status bar along the foot is only
as tall as its row of app tiles and its row of pills. An info panel or info view docked at the top left, a decision
card and the phone stand over the world on purpose, so the window's room and its fill do not apply to them.

Why: the owner, 1 November, of the City Ledger: "like I'm looking at a dashboard instead of a game UI". Cities:
Skylines II keeps the city in view round even its largest windows. Earlier the same day the full-screen pages wasted
the screen, the studio's form floating in the middle, so a window's page still uses its room. Checked by: EconomyCheck
(`the page uses its screen`).

### RLO-UI-017: Pictures carry the meaning

*Added 1 November 2026.*

**MUST.** Give every section, tab, category and status its icon, in its own colour on a slate tile. Give every person a
portrait. Give a share or a spread a chart where a picture helps: a donut with its legend, a scale, a meter, graded
bars or stars. Keep the world in view round every window. A table of words alone, or a page that hides the city, is
the failure this rule is for.

Why: the owner, 1 November, of the City Ledger: "Something is missing and something feels incomplete". What it missed
is what makes Cities: Skylines II read as a game: icons that carry meaning, real charts and the world on screen, with
faces for people. Checked by: EconomyCheck (`ui icons`, `the chart's hues stay apart`), review.

### RLO-UI-018: One kit of templates

*Added 1 November 2026.*

**MUST.** Draw every screen on the kit's templates: the info panel (`UiInfoPanel`), the info view (`UiInfoView`), the
app window (`UiAppWindow`), the decision card (`UiDecision`) and the phone (`UiPhone`). Until its screen is converted, a
screen may stay on one of the three older shapes, drawn in the window (`UiListDetail`, `UiSessionSteps`,
`UiForm`). When a conversion needs something the kit lacks, add it to the kit as a reusable piece with its sample and
its checks, never as a one-off screen.

Why: the owner: "Every single UI that is being built, must be the same". A piece built and checked once looks the same
on every screen that uses it. Checked by: EconomyCheck (`ui sources`), review.
