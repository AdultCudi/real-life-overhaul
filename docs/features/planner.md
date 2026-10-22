# The Planner

## In a sentence

An app on the game's phone with what the player has to do today and in the next seven days, gathered from the mod's features and the game, with reminders and the player's own notes.

## Where the code is

- Folder: [`Scripts/Planner`](../../Assets/Mods/RealLifeOverhaul/Scripts/Planner/), one class per source in [`Sources`](../../Assets/Mods/RealLifeOverhaul/Scripts/Planner/Sources/). The files are listed in the code block below.
- Read first: `PlannerDirector.cs`, `PlannerModel.cs` (game-free) and one source in `Sources/`.
- The interface it is built from is `Scripts/Core/GameUi`; see [The game's own interface](../reference/game-interface.md).

## How it talks to other features

- A source reads its feature's state and changes nothing (`IPlannerSource`). A feature the Planner is not to know of gives it a source from Wiring with `PlannerDirector.AddSource` (the Finance rows: `Wiring/EconomyLinks.cs`).

The Planner is an app on the game's phone: what the player has to do today and in the next seven days, from the mod's features and from the game, with reminders and the player's own notes.
Every object it shows is a copy of one of the game's, made while the game runs.

How the game's phone, tables, dialogs and click actions are built, which objects are copied, and what is proven and what inferred is in [`RealLifeOverhaul.Build/docs/game-ui.md`](../../RealLifeOverhaul.Build/docs/game-ui.md): read it before adding anything to the interface.
**New interface is built this way, not on a canvas of the mod's own.**

```

Planner/
  PlannerModel    game-free: the clock as the game counts it, entries, order, clashes (overlaps, and with a pace ways
                  too long between two places), states, "next", the badge
  PlannerRules    game-free: the filter's four groups, the way between two places and when to leave, the player's
                  entries that repeat, the day's free time and money due, the Week tab's seven columns (WeekGrid)
  PlannerStore    game-free: the player's entries (once, every day, every week), reminders and settings; the save's
                  three lines ("reallife:planner", "reallife:planner_notes", "reallife:planner_reminders")
  PlannerText     IPlannerSource; words, days and times as the game writes them; icons, tags and tones by kind
  PlannerTable    the table (the game's BaTable) and its row
  PlannerWeek     the Week tab: seven columns of copies of the table's row, made once, written when shown
  PlannerGame     the only reads of the game for the way: where the player is, an address's door, a car of their own
  PlannerApp      the three tabs and the filter (PlannerApp.cs), the white box's day at a glance and entry
                  (PlannerApp.Box.cs, one method a section), the player's own entries and settings (PlannerApp.Notes.cs)
  PlannerDirector installs the app, loads and saves, asks the sources, finds the entries' places, sends reminders
  Sources/        one class per source: notes, samples (test), the game's tax bill, careers, justice, custody,
                  licences, family, social plans, hotel checkout, city events, elections, business deadlines, sick note

```

- **Cost.** The director's `Update` is a timer comparison.
  Once a second it holds its two event handlers, compares the clock with the soonest reminder (`PlannerStore.NextFire`) and, until the app is on the phone, tries to put it there.
  Everything else happens on `GlobalEvents.onNewHour` (the badge: integers only, no text), on the save event, when a reminder's minute comes, and when the app is opened or clicked.

  The app's panel is inactive while closed, and the game is paused while it is open.
  The Week tab's rows are made once with the app and only written when it is shown.

- **Sources.** `IPlannerSource`: an id (kept in the save with a reminder, never changed), `Available`, and `Collect(fromDay, toDay, list)`.
  A source reads its feature's state and changes nothing.
  A feature that is off or not loaded is not asked.
  A source that throws is logged once and left out for the session.

  To add one: a class in `Sources/` and one entry in `PlannerDirector._sources`; or, for a feature the Planner is not to know of, a class in the pairing's file in `Scripts/Wiring` given to `PlannerDirector.AddSource` (the Finance rows: `Wiring/EconomyLinks`).

- **Entries.** Who an entry is (`EntryId`: source, two numbers) must be the same every time it is collected, or its reminder is dropped as "gone".
  Texts are filled only when the list asks (`WantText`).
- **Reminders.** One per entry: 15, 30 or 60 minutes before, at the time, 18:00 the day before, or when to leave (its hour less the way, kept with it).
  It goes out at its minute (the clock is compared once a second).

  It is taken out of the list before it is shown, checked against its source (gone, done, missed or over: not sent; moved later: moved with it), and one that went out more than half an hour after its moment (a time skip, a load) is marked late.
  One of the player's entries that repeats keeps its reminder on the entry, and the next time's is set when one goes.

- **The way.** There is no route for every entry (a route is a search spread over frames): the way is the straight line from the player (indoors, the door they went in by) to the entry's door, made 30% longer by the streets, at 3.5 m a game minute on foot and 8 by car or taxi (the cleaners' figures), and 5 minutes to set off.

  The leave by time uses the player's own way (by car if they have a car of their own); a clash by the way uses car or taxi.

- **Missed.** An entry the player has to attend is missed once it can no longer be begun (`PlannerEntry.JoinBy`): a class an hour after it starts, a rota shift three hours after, a working day two hours after, an interview an hour after, each from its feature's own rule.
- **The badge** counts what begins within two hours and is still to do, and the player's own all-day notes on their day (a source's all-day entries, such as a market day, never count). What the player has seen is kept as a stamp made of a hash that is the same in every run.
  It goes when the app is opened and comes back only when what is due has changed (`PlannerLogic.DueStamp`).
- **If the game changes.** Every object the app is made of is looked up by name; if one is missing, one line says which, the app is left off the phone (or the part is left out: a switch, a selector, an icon), and reminders already set still go out.
  Nothing is thrown into the game's interface.
- **Checks.** `RealLifeOverhaul.Build/PlannerCheck` compiles `PlannerModel.cs`, `PlannerStore.cs` and `PlannerRules.cs` as they are and checks the calendar, order, clashes by overlap and by the way, states and the missed, the badge, reminders across a save and a time skip, when to leave, repeats over the week's end, deleting, the filter's groups, the Week tab's order, free time and money, round trips, old and damaged saves, and the new texts in every language: `dotnet build RealLifeOverhaul.Build/PlannerCheck -c Release`, then `dotnet RealLifeOverhaul.Build/PlannerCheck/bin/Release/net10.0/PlannerCheck.dll` ends with `ALL CHECKS PASSED`.
- **Not run in the game.** The build machine cannot start the game: the first play session should follow the list in the pull request, with "Developer diagnostics" on.
