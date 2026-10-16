# After a game update

When the game updates, the gate fails on purpose until the mod has been checked against the new build. This page says why, what the running mod does on a game it was not made for, and the steps to bring it up to date. The short version:

1. Build the mod against the new game and fix every compile error.
2. Run ContractCheck and fix every line it prints except the build line.
3. Set `GameBuild.MadeFor` in `Scripts/Core/GameBuild.cs` to the new build, and run the whole gate again.

## Why the mod is made for one build

The mod is compiled against one build of the game, `GameBuild.MadeFor` in `Scripts/Core/GameBuild.cs` (3682): the mod's one build number.
Under Mono a method that names a type, field or method the running game does not have cannot be compiled: it throws every time it is called, and everything else it does is lost.

A player on Build 3675 had no mod app open (`PhoneApp.Open` named the newer eight-parameter `HudConfirm.Show`) while the camera and careers threw every frame, some 110,000 errors a session.
The game contract and its guard are the mod's one answer to a game that is not the one it was made for, newer or older.

## What the running mod does

- **As the game starts, before any feature** (`Core/Guard.Start`):
  - the game contract (`Scripts/Core/GameContract.cs`) is resolved once: every piece of the game the mod reaches by name (a private field, a method read by name), and the members the mod names in its code where an older game lacks them (the five Build 3675 lacked, the game's version, the question box, an app's first steps).

    A piece that is missing or has changed switches off the parts that stand on it; an optional one (the code has another way) is done without.
    Core/GameMembers and Core/GameConfirm take their answers without a missing member from the first question, so the methods that name it are never called;

  - the game's build is read (`GameVersion.buildNumber`) and compared with `MadeFor`: one line for an older, a newer or an unreadable build;
  - every type and member of the game's and Unity's assemblies the mod's compiled code names is asked of the running game (`Core/ModImage` reads the mod's own assembly as a file, `Core/ModScan` asks; some 4,000 questions, about twenty milliseconds).
    Each one missing gets one line naming it, the mod's classes that name it and the features switched off for it; those features are off before they run (their components are not even added, `Guard.Add`).

    A member named only in a small method of the mod's own that is never inlined, whose caller catches its absence and does without it (`Core/GameMembers`, `Core/GameConfirm`, an app's first steps in `Core/GameUi/PhoneApps`), switches nothing off.

- **One notice**, the game's own question box (`Core/GameBuild.Notice.cs`): for an older game, in the main menu and once a session when a save is loaded, that the mod needs the current game, how to update it in Steam (Betas set to None, then Verify integrity of game files) and which parts are off or work without what it lacks.

  For any other game on which a part is off, once a session in the city, that the mod waits for an update and which parts.
  The options page says it under Troubleshooting, Diagnostics, "What a game update switched off", and after the name of each feature hit.

- **A member found missing only when it is first used**: every entry point the engine or the game calls runs inside a try with a `FeatureGuard` (Update, LateUpdate, FixedUpdate and OnGUI through each class's FrameGuard, coroutines through `Guard.Routine`, handlers on the game's events through `Guard.Wrap`, clicks through `UiClone.Safe`, the economy's ticks through `Guard.Each`, the steps of the start through `Guard.Step`).

  A fault that says the game lacks a member stops that entry point's class at once, with one line, instead of failing every frame.
  A part inside an entry point that can stop alone uses `Guard.StandsDown(e, part)`; a step that is done without, `Guard.DoesWithout`.
  Any other fault is written in full; five within thirty seconds switch the feature off for the session.

- **The game's own calls into the mod** (`Core/GuardCalls.cs`), past those entry points, are behind the same guard:
  - what the game asks of an object of the mod's through an interface or a base class of its own (an activity of the game's work panel, an overlay, a call to action, a pointer, a table and its cells, a mesh, a drag or a scroll, a row of the options page): the method's body is a try whose catch is the guard's, and the game is given an answer instead (`Guard.Failed`).

    An activity whose feature is off ends as its own Finish ends it, so the panel closes (`Guard.Ended`);

  - the delegates of the mod's the game keeps and calls later: a UnityEvent's listener (`Guard.Listener`), a button of the work panel, the walk's arrival, the end of a dialog, an asset bundle's load done, the interface's late pass, the scene's events, the save event and the notification event (`Guard.Wrap`, or `Guard.Way` for a way out, which is never skipped), a question the city map asks (`Guard.Asked`);
  - every page of the mod's dialogs as it goes to the game (`Guard.Entry`) and every dialog the game makes for a contact of the mod's (`Guard.Maker`): an answer that fails, or whose feature is off, ends the conversation with the game's own closing, so no dialog is left that cannot go on or be left;
  - the options page as it is registered (`Guard.Rows`): every row's callback caught and never skipped, since the game calls a row's callback while it draws the page too.
- **Saves**: a line no store can read is kept in the save as it was read (`Core/SaveLines`), however the feature starts.
- **Going Public**: a session refuses a player whose game build differs from the host's, so every machine meets the same game, finds the same pieces missing and switches off the same parts; nothing of this is sent over the network.

In Player.log, read the lines that begin `Game build:` and `Game contract:` (warnings: written whether or not the developer diagnostics are on).
Options > Tests > Diagnostics shows the guard's five faults, the notice an older game gets, and a question asked the way the mod asks on an older game.

## What to do after a game update

1. Build the mod: it compiles against the game installed in Steam, so a member that is gone or has another signature is a compile error to fix first.
2. Run the gate, which runs `ContractCheck`, or run it alone: `dotnet build RealLifeOverhaul.Build/ContractCheck -c Release`, then `dotnet RealLifeOverhaul.Build/ContractCheck/bin/Release/net10.0/ContractCheck.dll ["<game>/Big Ambitions_Data/Managed"]` (the Steam folder on G: when no folder is given, or the one in `BA_MANAGED`).
   Nothing of the game is loaded: it reads the game's files on disk and the built mod, and ends with `ALL CHECKS PASSED` or one `FAIL` line for each thing to fix.
3. Read its sections:
   - **the game's build on disk**: fails while the installed game is not `GameBuild.MadeFor`.
     It is meant to: the gate stops after every game update until the rest below has been read and fixed.
   - **the game contract**: each piece missing or changed, what changed ("the field is missing", "the field's type is now X", "the method now takes (...)") and the parts and features the running mod switches off for it.
     Fix the line in `GameContract.cs` and the code that uses it, or mark the piece `Optional` when the code has another way.
   - **the mod's compiled references**: every type, field and method of the game's and Unity's assemblies the built mod names that the game no longer has, or has with another signature, and the classes that name it.
   - **the mod's code against this game**: what the running mod would find missing as it starts, and for each piece the classes that name it and the features it would switch off: what the update touches, at a glance.
   - **a game that lacks what a player's Build 3675 lacked**: the five pieces of that player's Player.log, in Mono's own words; each must be found, named only where the mod does without it, a line of the contract, and the features each touches are printed.
   - **isolated members, guarded entry points, guarded coroutines**: a never-inlined method that names a game member called outside a try, an Update without the guard, a coroutine started outside `Guard.Routine`.
   - **the game's calls into the mod**: a method the game calls through its interfaces or base classes whose body is not behind the guard (and cannot be shown not to fail).
     A delegate of the mod's handed to the game, straight, kept in a field first or passed on, that the guard did not make.

     A page of a dialog, a maker of a dialog or an options page given to the game outside `Guard.Entry`, `Guard.Maker` or `Guard.Rows`.
     The few left as they are on purpose are listed with the reason (the characters' own, the options page's own catches, the frame's timestamps).

   - **the game's keys** (`ContractCheck/GameKeys.txt`): keys of the game's own texts and data (business types, items, skills, districts, transactions, vehicles) the mod's code names, against the game's `StreamingAssets/locale/en.json`, and that the list holds every one the code names.
4. Fix each line.
   A member that has changed: point the code at what replaced it.
   One that an older game may lack, or that may go: name it only in a small method of `Core/GameMembers` (never inlined, called inside a try, with an answer without it), add its line to the contract, and let the contract tell `GameMembers.Known` it is missing.

   Run the check again until only the build line fails, then set `GameBuild.MadeFor` to the new build.

The game's layers the mod names (`GameContract.Layers`) are the game's project settings, not in its assemblies: the running mod checks them as it starts and names any that is gone.
