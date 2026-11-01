# RLO interface

## In a sentence

Under the living economy's master option **Living population**, the RLO interface (`ui_interface`, on by default) draws
the game's Contacts and the overhaul's screens in the mod's own design, the City Ledger. It has serif heads, the paper's
labels in capitals, square ruled panels and the salmon of a markets paper. It works with the keyboard and the pad
first, and its own masthead and prompt bar cover the whole full menu.

## Where the code is

- Folder: [`Scripts/Ui`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/). Its README explains how to build a screen and
  holds the review checklist. The rules are in [docs/standards/ui.md](../standards/ui.md), and the reason for them in
  [ADR-0007](../standards/decisions/0007-rlo-interface.md).
- Options: "RLO interface" (`ui_interface`) and "Interface motion" (`ui_motion`: full, calm, off). Dark or light follows
  Flare's look option (`flare_theme`). With either switch off, every screen is the game's own.
- Tests: "Open the interface gallery" and "Open Contacts on the RLO interface".

```
Ui/                         game-free unless named: the offline checks and the previews draw exactly what ships
  UiTokens, UiColour          the City Ledger's colours (dark and light), type, spaces, radii, strokes, motion
  UiFontMetrics(.Data)        the bundle's faces' metrics, written by UiAssets/scripts/fonts.py
  UiTextRules                 widths, wrapping and fit, by those metrics
  UiScene, UiDraw*            a scene of placed items and targets; the components that place them
  UiListDetail, UiDashboard,  the templates: groups, list and detail; figures and a table; a desk session's steps;
  UiSessionSteps, UiForm,     a form; the kit's gallery
  UiGallery
  UiFocusRules, UiCheckRules  the d-pad's focus; the rules EconomyCheck holds every screen to
  UiScreens, IUiScreen        the one list of screens; what a screen answers
  UiView*, UiHost, UiTweens,  the renderer (pooled slots, only what changed), the host (input, focus, sounds,
  UiSprites, UiIcons, UiFonts typing), the tween pool, shapes, icons, the font bundle (game-facing, still game-free)
  UiGame, UiText              the one file that meets the game's input, sounds, money and time; the words (game files)
  UiDirector(.Tests), UiGuide the fonts behind the loading screen, the switch, Contacts, the tests, the feature's entry
  Contacts/ContactsKit,       the game's Contacts app under the kit: hidden, read, its buttons pressed, given back on
  Contacts/ContactsScreen     a fault, for a call, an offer or the game's question box
```

## What the player sees

- **Contacts**: groups (the game's categories) at the left, the list (unread first, then the newest) in the middle,
  and the open contact at the right. The contact shows its messages under the line where they start, its details and
  the game's own buttons as actions. Call, an offer with buttons and a tax table hand over to the game's own view
  until the app closes.
- **The overhaul's pages**: the population and Flare in the city as dashboards, a tech firm's page as a form, and a
  desk session's steps as a card with its choices. Every page starts under a slim masthead and runs across the screen.
- **The gallery**: every component on six pages, in both looks (Option turns the look).

## Save and Going Public

Nothing is saved, and nothing is shared: the kit only draws.
