# The RLO interface

The rules for every screen the mod draws on its own interface kit (`Scripts/Ui`), in the City Ledger the owner chose on
1 November 2026. The owner's words are the brief:

- "The most important thing in the overhaul is the UI and UX. It must look absolutely beautiful at the same time, not cluttered."
- "Every single UI that is being built, must be the same and looks absolutely exquisite. No slop like, no weird spacing etc."
- "It must be like a game UI, not a web UI."

The kit's tokens, components, templates and checks are in [`Scripts/Ui`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/), and
its README holds the way to build a screen and the review checklist. [ADR-0007](decisions/0007-rlo-interface.md) records
why the mod has an interface of its own. EconomyCheck's `UiChecks.cs` lays out every registered screen at every frame
the game's canvas gives it (16:9, 16:10, 4:3, 21:9), in all eleven languages, in both looks, with the keyboard and a
pad, and checks each rule below that a machine can check.

### RLO-UI-001: One source for every look

**MUST.** Take every colour, size, space, radius, stroke, duration and text style from `UiTokens`, by role and named
style. A screen never writes a colour, a font, a font size or a pixel figure of its own.

Why: one change of the tokens changes every screen, and nothing drifts. Checked by: EconomyCheck (`ui sources`), review.

### RLO-UI-002: The City Ledger

**MUST.** Draw in the City Ledger:

- Source Serif 4 for heads and messages, and Libre Franklin in tracked capitals for labels, buttons, prompts and figures.
- Dark is the late edition (ink-black with paper-white text). Light is the salmon paper with its ink.
- One accent: the markets paper's salmon in the dark, the paper's claret in the light.
- Square print, hairline rules, and a 3 px column rule on each framed panel's head and under the masthead.

Why: the owner chose the paper's look, and the mod's City Ledger is its world. Checked by: EconomyCheck (`ui tokens`, `ui fonts`).

### RLO-UI-003: The 4 px grid

**MUST.** Place every box on the 4 px grid relative to its parent, with sizes on the grid. Only text centred on a box,
a share of a width (a segment, a knob) and decoration are exempt.

Why: alignment is what reads as care. Checked by: EconomyCheck (`every box on the 4 px grid`).

### RLO-UI-004: Text fits in every language

**MUST.** Make every translated label fit whole in all eleven languages, wrapping onto two lines where the component
allows it. Player data (names, messages) may end in an ellipsis. Every style is at least 15 px at 1080p.

Why: a cut label looks broken, and a game is read from the couch. Checked by: EconomyCheck (`every label fits whole`, `no text under the smallest size`).

### RLO-UI-005: Every screen is registered

**MUST.** Register every screen in `UiScreens` with its template, the class that runs it and the keys of its labels.
Every key a screen uses is named in its entry and is in en, pt and zh-cn.

Why: the checks can only hold what they can find. Checked by: EconomyCheck (`ui sources`, `every key ... is named in its entry`, `ui texts`).

### RLO-UI-006: Contrast in both looks

**MUST.** Give every text and icon at least 4.5:1 against what is under it (3:1 for large text and the focus ring), in
the dark and the light look.

Why: WCAG 2.2 AA, at play distance. Checked by: EconomyCheck (`stands out`, `ui tokens`).

### RLO-UI-007: Focus first

**MUST.** Make every target reachable by the d-pad and the arrows from the screen's first target. Focus is the main
state, and nothing depends on hover.

Why: a game is played with a pad and a keyboard as much as a mouse. Checked by: EconomyCheck (`the d-pad reaches every target`).

### RLO-UI-008: A prompt bar on every screen

**MUST.** Show the prompt bar with the keys bound now (key caps, or the pad's glyphs while a pad is used), and give
the way back on it.

Why: the player never has to guess a key. Checked by: EconomyCheck (`a prompt bar that says how to go back`).

### RLO-UI-009: One key does one thing

**MUST.** Let a key do one thing on a screen:

- A target owns the key whose cap it shows, and that key presses it from anywhere on its layer.
- Enter always presses whatever has focus, so no target shows it.
- Choices are on 1, 2, 3 and so on, F is the screen's main action and P its second.

Why: a key that means two things is a trap. Checked by: EconomyCheck (`one key does one thing`).

### RLO-UI-010: No dead space

**MUST.** Make a frame fit what it holds. No frame may hold more than 48 px that nothing covers, above, between or
under its lines. A list or a thread is a frame of its own, and an empty state fills the room it is given.

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

**MUST NOT.** Use a gradient, glow, blur, soft shadow or translucent glass anywhere in the kit. Draw flat solid
surfaces with crisp hairline edges, and use colour sparingly and on purpose.

Why: the owner, 1 November: "Gradients don't look nice. I want a proper themed thing." Checked by: EconomyCheck (`no gradient, glow, blur or soft shadow in the kit`).
