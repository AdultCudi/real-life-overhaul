# The RLO interface kit

Every screen of the overhaul, and every game app the mod redraws, is drawn here in the City Ledger, the owner's choice.
The rules are in [docs/standards/ui.md](../../../../../docs/standards/ui.md) (RLO-UI-001 to 016) and the feature in
[docs/features/rlo-interface.md](../../../../../docs/features/rlo-interface.md).

## How to build a screen

1. **A view model**: plain data, no colour or position. Give the feature a view of its own (as `PopulationViews` or
   `StudioViews` do) and translate it into a template's model (`UiListDetailModel`, `UiDashboardModel`,
   `UiSessionModel`, `UiFormModel`). Words come from locale keys through `UiWords`.
2. **A template**: pick the one that fits.
   - **List and detail** (`UiListDetail`): groups, a list and the selected record. Used by Contacts, and by
     anything with records and a detail.
   - **Dashboard** (`UiDashboard`): up to four places of figures with their change and line (a wide one draws its line
     beside the number), a table of whole rows with an optional second under it that takes the rest in whole rows, a
     side panel of bars and pairs that keeps to its room, and the page's actions at the head. Used by the Economy app's
     views, Flare in the city, and the tech firms' whole pages.
   - **Session steps** (`UiSessionSteps`): one card across the page, as tall as what it holds, with the steps, the
     question and the choices on 1, 2 and 3 (side by side when they fit one row), and its summary as a panel at the
     side. Used by desk sessions.
   - **Form** (`UiForm`): settings before an action, or a page of lines and actions. What to do next stands in a band
     across the page with the actions, and the sections run in two columns.
3. **A runner**: a class implementing `IUiScreen` (and `IUiTyping` for a search) that owns the model and answers the
   presses. Register it in `UiScreens` with its template and every key it labels with.
4. **Samples**: add the screen's sample to `RealLifeOverhaul.Build/UiPreview/UiSamples.cs`, so EconomyCheck lays it out
   in every language, look and frame, and `preview.ps1` renders it.
5. **A host**: the game file that shows it creates a `UiHost` over the full menu (as `ContactsKit` does). It binds it
   to the game's input (`UiGame`), the look (`UiDirector.Dark`) and the motion setting.
   The game keeps its full menu and apps switched off until they open, so the host binds over them switched off: every
   search of the scene passes `true` or `false` for switched off objects, the canvases are walked with
   `GetComponentsInParent<Canvas>(true)` and `UiBindRules`, and a screen not built yet is looked at again
   (`UiBindRules.Wait`), never given up for the session. EconomyCheck fails a search that does not say.

## Review checklist

Before a screen is done, check each line in the previews (dark and light, 1080p and 720p, and the focus path) from couch distance:

- [ ] **Game UI, not web UI**: framed panels with square edges and the column rule, bars for tabs and selection, density
      for a management game. No cards floating on a page, and no page scroll.
- [ ] **Flat**: no gradient, glow, blur, soft shadow or glass (RLO-UI-015).
- [ ] **One primary action** to a layer, and nothing on screen that is not needed.
- [ ] **Focus first**: the focus path reaches every target, the d-pad stays in its column, and nothing is hover-only.
- [ ] **Keys**: the prompt bar shows the bound keys and the way back, one key does one thing, Enter presses the focus,
      choices are on numbers, F is the main action and P the second.
- [ ] **Alignment**: everything is on the 4 px grid and spacing comes from the tokens' steps, with no hand-picked gaps.
- [ ] **No dead space**: frames fit what they hold, tables show whole rows and a short thread sits at the foot under its start line.
- [ ] **The page uses its screen**: it starts under the masthead and runs across the width, a form of several sections
      runs in two columns, a short table's rows breathe, and nothing floats in the middle (RLO-UI-016).
- [ ] **Indicators** agree with their values: the arrow follows the sign, and the colour says good or bad for the player.
- [ ] **Words**: every label fits in all eleven languages, empty states are in words, and nothing is under 15 px.
- [ ] **Contrast**: AA in both looks.
- [ ] **Motion**: 80 to 200 ms feedback, counters tick, bars fill, selections snap, Calm and Off are respected.
- [ ] **Sound**: the game's own UI sounds on a move, a press, a tab and an error.
- [ ] **The game's logic**: a redrawn game screen presses the game's own buttons, steps aside for its question box,
      and falls back on a fault.

Every line a machine can check is checked by EconomyCheck (`UiChecks.cs`); the rest is the review.

## The pieces

- **Look**: `UiTokens`, `UiColour`, `UiFontMetrics(.Data)`, `UiTextRules`.
- **Layout**: `UiScene`, `UiDraw*`, the templates, `UiFrame` (the whole full menu, its masthead and band painted by the kit).
- **Rules**: `UiFocusRules`, `UiCheckRules`, `UiMotionRules`.
- **Registry**: `UiScreens`, `IUiScreen`.
- **Drawing**: `UiView*`, `UiHost`, `UiTweens`, `UiSprites`, `UiIcons`, `UiFonts`, `UiSpark`, `UiHit`.
- **The game**: `UiGame`, `UiText`, `UiDirector`, `UiGuide`, `Contacts/`.

Everything except the game files is game-free, so the offline preview draws exactly what ships.
