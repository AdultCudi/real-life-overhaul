# Walking

## In a sentence

On foot, the walk key (F3 unless it is changed under Controls) puts the player on the game's own walk, the pace the city's people walk at; a press again gives back the game's own pace.

## Where the code is

- Folder: [`Scripts/Walking`](../../Assets/Mods/RealLifeOverhaul/Scripts/Walking/).
- Read first: `WalkRules.cs` (every rule, game-free, run by NavCheck's `WalkCheck.cs`) and `WalkDirector.cs` (the key, and the speed kept while walking).
- The key is an action of `Core/Keys.cs` (`KeyAction.Walk`); `Core/KeyRules.cs` names keys by the input system's paths for the options page and for ContractCheck's `KeyClashChecks.cs`.

## How it talks to other features

It asks the camera whether a window is open (`ThirdPersonCamera.IsUiOpen`, which counts typing in a box, the phone, the map and every panel of the game's and the mod's) and nothing else. No bridge, no save line, no contact.

```
WalkRules        the toggle, the refusals, the walk speed, Decide, the walk cycle's rate   NavCheck/WalkCheck.cs runs them
WalkGame         the local player's body and agent, the game's figures, what stops a walk, the notice
WalkDirector     the key a frame; while walking, the agent's speed against the last looked at
WalkDirector.Tests  "Say how you are moving now"
WalkGuide        the feature's entry, the options block, the Guide page
WalkText         the locale keys
```

## What the game does, and what walking does with it

The game picks the player's pace every frame: `PlayerController.Update` calls `ThirdPersonCharacter.ToggleRunning`, which chooses `Run` outdoors (6 m/s), `Jog` indoors (4 m/s, unless "Run by default indoor" is on) and the other one of the two while **Toggle Running** (Left Shift) is held, `Jog` with no energy, the zombie walk with neither energy nor food, and the scooter's pace on one.
It writes that pace's speed into the navigation agent (`SetWalkingSpeed`) only when the pace it chooses changes. WASD walking (`HandleWasdNavigation`) and click-to-go both move at the agent's speed. It never puts the player on its own `Walk` pace (1.2 m/s), which its people use.

Walking writes the game's walk speed (`GlobalReferences.walkingSpeedWalk`, held between 0.8 and 1.6 m/s) into the agent over the game's run or jog and leaves the pace itself alone. The game's choice still stands: when it changes its pace it writes its own speed again, and walking puts the walk back the same frame.

- **The legs.** The game's `Move` gives the animator's `Forward` the speed the player moves at and advances `MotionTime` at a rate read from the same figures. At the walk speed that is the game's walk, at its walk rate (0.75), the walk its own people use: nothing is scaled or faked.
- **Toggle Running.** While it is held the game's own pace comes back (outdoors its jog, indoors its run); let go and the walk is back the next frame. With no energy the game jogs whatever the key, and walking walks over it.
- **Never written over:** a speed neither the game's for its pace nor walking's own. A job of the game's (`Employee.SetAgentProperties`) and Going Public's time skip (`SkipPaceBodies`, which paces the local body only while it works a shift) put speeds of their own on the agent; walking leaves them and comes back when the game writes its own again.
- **Given back** only when the walk is on the agent, through the game's own `SetWalkingSpeed(walkingSpeed)`: on a press, the option off, the mod closing, and while the player is in a vehicle, seated, held still or holding Toggle Running.

## The key

- Read through `Keys.Down(KeyAction.Walk)`, Unity's `Input.GetKeyDown`: it takes nothing from any binding of the game's or the mod's.
- It does nothing, and says nothing, while typing in any box, in a menu, the phone or the map, in a vehicle (`selectedVehicle`, `PlayerHelper.IsUsingVehicle`), seated, working, studying or held still (an activity of the game's, `NavigationDisabled`, which the mod's seats and desks set too). Walking already on carries on afterwards.
- **F3**, because nothing else has it. ContractCheck reads the installed game and the built mod and fails if the default is anybody's: every binding of every input asset in the game's assemblies (the player's, the interior designer's, the vehicle's and the scene's maps), every key the game's code reads by itself (a KeyCode passed to any method or stored in a KeyCode field, a `Keyboard` property, `Keyboard[Key]`), the same of the mod's code and every other binding's default, and `KeyRules.Taken`: Windows (the Windows keys, Print Screen, the lock keys, Pause, Alt+F4, Alt+Tab), Steam (F12, Shift+Tab), Discord (Shift+\`), Going Public and Empire Motors.
  It prints the free keys it looked at: F3, F11, Insert, Delete, - = \ / ', 0 on the top row, and the number pad's keys. F3 is on every keyboard in the same place whatever the layout, by the game's F1 and F2. NVIDIA's Alt+F3 is not counted: nothing in play holds Alt down.

## Saves and loads

Nothing is saved. The game keeps its pace on the player's body, which every load builds anew at its own pace, and its auto-run lasts a session; walking likewise ends with a load (`WalkRules.AfterLoad`), and every save starts at the usual pace.

## Going Public

Only the local player's body is touched (`GameManager.playerController.Character`; another player's avatar is Going Public's own object). Nothing is sent. Going Public sends the local body's place, rotation and every animator value already (`RemotePlayerManager`); the others' copies follow them (`RemotePlayerMover`), so they see the walk. Its time skip's speeds are never written over (above).

## Cost

Running: one key read a frame. Walking: also the agent's speed (one engine call) compared with the last looked at, and one time comparison while what stops the walk lasts. A look (a press, or the speed changed) is a few reads of the game. Nothing is allocated but the notice of a press.

## Tests

- NavCheck `WalkCheck.cs`: the toggle and its 32 cases of what stops it; the walk speed and its clamp; `Decide` case by case and over every pace, speed and reason; frame by frame on a model of the game's own loop (a press, Toggle Running held and let go outdoors and in, a door, a vehicle left, a job's pace, Going Public's skip, no energy, a press again, a load, a seat); the walk cycle's rates against the game's `Move`; the sources (one key read, nothing sent or saved, the game touched only through `WalkGame`, nothing allocated a frame) and the texts in en, pt and zh-cn.
- ContractCheck `KeyClashChecks.cs`: the walk key's default against every key that is someone's (above), and the same question asked of the game's interact key, the mod's photo mode key, Caps Lock, Right Shift and V, which must be found taken.
- Options, Test tools: "Say how you are moving now" says whether walking is on and whether the walk or the game's own pace is on the player now; with Developer diagnostics on it writes the "Walk check (pace)" line: the speed wanted, the walk cycle's rate, what the agent has over which pace of the game's, and PASS or why not.
