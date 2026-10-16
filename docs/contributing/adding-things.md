# Adding things

Step-by-step recipes for the things contributors add most: a crime, an activity of the living city, a job, and a whole feature with its Guide page and presets. Before you add a feature, read [Architecture](../reference/architecture.md) and copy the layout of `Scripts/Economy/Company`.

## Adding a feature with its Guide page and presets

A feature is one component under `Scripts/<Feature>/`, registered in `Core/RealLifeOverhaulMod.cs`, with a toggle in `Core/Settings.cs` and its strings in the locales. In order:

1. Make the folder with the layers of [the director pattern](../reference/architecture.md#the-director-pattern), and a `README.md` saying what it is for and which files to read first.
2. Add its option to `Core/Settings.cs` (`public const string XId` beside `public static bool X`) and its row to the options chain in `Core/RealLifeOverhaulMod.cs`. With the option off, the game must behave exactly as without the feature.
3. Register it in the feature registry with one statement (below). `.Help` is its Guide page: a locale key whose text uses `# ` for a heading, `- ` for a bullet and `{fact}` for a figure filled from the code by `.Fact`. `.Preset` gives its value in Relaxed, Realistic and Hardcore; Standard is always its default.
4. Keep its state under its own `reallife:<feature>` key, declared with `.Data`, and its contacts with `.Contact`, so the save tools find them.
5. Add its texts to the locales ([Texts and translation](../contributing/texts-and-translation.md)), its game-free rules to a checker ([Checks](checks.md)), and its line to What's new.
6. If it has places in the city, give them a row on the map ([Map places](../reference/map-places.md)). If it talks to other features, give it a bridges file and fill it in from `Scripts/Wiring` ([Wiring](../reference/wiring.md)).

### The registry statement

At the marked place at the end of `FeatureList.RegisterAll` (or the same statement in a `static void DescribeFeature()` of a class of the feature's own marked `[Meta.DescribesFeature]`):

```csharp
Features.Add("fuel", "reallife_fuel_title")              // id (plain English: it sorts the page), title key
    .Group("reallife_fuel_header")                       // the AddHeader key of its group in RealLifeOverhaulMod.cs
    .About("reallife_fuel_about")                        // one sentence
    .Help("reallife_fuel_help")                          // its guide page: "# " heading, "- " bullet, {fact}
    .Preset(Settings.FuelId, 0, 1, 1)                    // toggle at Relaxed, Realistic, Hardcore (Standard = its default)
    .Preset(Settings.FuelPriceId, 50, 120, 160)          // slider likewise
    .NeedsReload(Settings.FuelId)                        // only if it is read once per load
    .Data("reallife:fuel")                               // modData keys ("reallife:fuel_*" = prefix)
    .Contact("reallife-garage")                          // phone contacts it adds
    .Objects("RealLifeOverhaul Fuel HUD")                // objects of its own outside the mod's root object
    .Fact("fuel_price", () => Guide.Money(FuelLaw.PricePerLitre))
    .Cleanup(() => FuelDirector.Instance?.StopAll())     // off the street and stop; safe when idle
    .ResetData(() => FuelState.Reset())                  // forget this save (its modData was just removed)
    .Repair(() => FuelPhone.Setup());                    // put back what it adds to a save
```

Only `Add` is required.
The feature's rows stay where they are added today (the chain in `RealLifeOverhaulMod.cs`), its options stay fields of `Settings` with a `Settings.Load` line each.

What happens without any registration: its header becomes a section of its own, its options are in the settings file and "reset everything", its buttons with `_test_` in the label move to "Test tools", its `reallife:` modData keys and `reallife-` contacts are still removed by the save tools, and its `Logger` is reached by the diagnostics switch.
What it does not get: a guide page, a place in the presets, a clean-up call.

"Check the mod's set-up" (options page, Diagnostics) lists every such gap.

Rules that make this work: option ids are `public const string XId` beside `public static bool|int X` in `Settings`; modData keys start with `reallife:`; contact ids with `reallife-`; a class that logs has a static `IModLogger Logger`.

## Adding a crime

1. **Catalogue entry** in `Scripts/Crime/CrimeCatalogue.cs`: an id, its family, where it can happen (`Place`), what it needs (`Needs`: night, traffic, patrol cars...), how many people it must be able to take from the pool (`Cast`), its `Weight` in the deal, what a 911 call about it is called (`Report`), and its lines: `Starts`, `Near` (the player walks up), `Extra`, and one `CrimeEnding` per way it can end, with odds.

   An ending with odds 0 is never rolled; a scene reaches it by what happens in it.
   Every key is written out in full and added at the end of `Locales/en.json`.

2. **Scene**: either one more line in `Scripts/Crime/CrimeScenes.cs` reusing an existing class (a lone offender on the pavement is `new LoneOffenderScene("my_id", OffenderStyle.Pitch)`: its endings are picked from the ones that class knows, listed at the top of the file), or a new class in `Scripts/Crime/Scenes/` deriving from `CrimeScene`:
   - `Id` returns the catalogue id;
   - `Stage` puts the cast out through the four `ActorRole`s (`First`, `Second`, `Third`, `Cop`) and sets the first step with `Go(step, now, seconds)`; return false if it cannot be staged here;
   - `Run` is a switch over the scene's own steps, called four times a second.

     Ask `Is("ending")` for what was rolled, `Conclude()` to announce it, `Arrest(role, now)` to hand someone to the officer (cuffs, kerb, patrol car and driving away follow by themselves), `RunFor(role, from)` when somebody runs for it (the player can then stop them by getting within reach: the base class plays the citizen's arrest, the wait for an officer and the thanks), `Leave(now, player)` to wind down;

   - `Release` gives back anything that is not one of the four roles or the two shared cars.
     The base class ends the scene after two minutes, when the player leaves, and on every fault.
3. `dotnet run --project RealLifeOverhaul.Build/CatalogueCheck -c Release` checks that every entry has a scene, every line exists, every ending a scene names is in the catalogue, and the deal never repeats early.

Fines, the odds of being seen and custody hours are constants in `CrimeLaw` at the top of the catalogue.

## Adding an activity to the living city

Somebody working alone on the pavement is **one catalogue entry and nothing else**.
In `Scripts/CityLife/CityCatalogue.cs`:

```csharp
new CityEntry
{
    Id = "window_cleaner", Name = "reallife_city_name_window_cleaner", Family = CityFamily.Workers, Weight = 12, From = 6, To = 12,
    Activity = new SoloActivity(new SoloSpec
    {
        Look = CityLook.Cleaner, Place = SoloPlace.Shopfront, Wall = 0.50f, Room = 0.45f,       // facing a real wall, half a metre from it
        Work = "squeegee_wipe", Rest = "squeegee_rest",                                           // two clips made for the same tool
        Props = new[] { new SoloProp("bucket", 0.62f, 0.05f, 0f) }                                // stood on the ground beside them
    })
},
```

plus its name at the end of `Locales/en.json` (written out in full).
The clip brings its tool; `Carry` puts a second tool in the other hand, `Greet` is a one-off played when someone on foot comes by in front, `Now` a one-off every so often, `UsesGamePose` one of the game's own poses instead.

If it needs a new motion, that is one clip ([`CityAssets/README.md`](../../CityAssets/README.md), "Add a clip") and, for a new tool, one prop ([`PropAssets/README.md`](../../PropAssets/README.md), "Add a prop").

More than one person, or anything a table cannot say, is a class in `Scripts/CityLife/Activities/` deriving from `CityActivity` (copy `ChatActivity`, 70 lines) and an entry that names it:

- `Find(site)` looks for a place for every person and prop near `site.Anchor` with `CitySpots` (`Stand` = on the pavement with room, `Shopfront` = at a measured distance from a real wall, `Along` = which way the pavement runs) and remembers them.
  It puts nothing on the street, and returns false if anything has no room.
- `Begin(site)` calls `Start(site)`, then `Add(look, spot, face)` for each person, `Put(prop, spot, heading)` for each prop and `person.Work(clip, Props)` for what they do.
  If any of it fails it calls `End()` and returns false.
- `Tick(now)`, once a second, changes clips on timers and returns `Lasting()`.
- `End()` is inherited: everyone and everything goes back.

- **People who come and go** are never put down in view.
  `Arrive(site, look, goal)` takes somebody from the pool, puts them on the pavement out of view 24 to 46 m away and sets them walking to `goal` (null, with the reason in `site.Refusal`, if nobody is free or there is no way in); test `person.Arrived && person.Near(goal, 0.5f)` in `Tick`, then `person.Stand()` and `person.Face(point)` (a turn over a third of a second).

  `Depart(person)` walks them off to pavement out of view, and `TickDepartures()` at the top of `Tick` takes them back into the pool once they are there.

- **Carrying while walking** is a masked clip over the game's own walk: `person.WorkOver("carry_box_hold", Props)` (arms), `"cup_hold"` with `BodyMask.RightArm`, `"umbrella_hold"`; `person.Bring(prop, null, position, rotation)` for what is pushed in front (pushchair, sack truck) or worn on a bone (satchel).
  Nothing of the legs is ours, so nothing slides.
- **The instant something changes hands** is asked for with `CityLifeDirector.After(clip.Mark("release"), this, what, person)` and arrives in `Moment(what, who, now)`: there `person.Release(true)` gives the prop back to the activity (stand it where it is and add it to `Dressing`), or `person.Take(prop, clip, Props)` puts a prop that is already out into a hand.
  A clip whose tool is picked up part of the way through is started with `tool: false`.
- An activity that turns a place down for a reason of its own says so: `site.Refusal = CityRefusal.NoParkedCar`.

The director does the rest: when and where, out of view, never the same thing twice running, taken away out of view, the test button (each press of a family's button puts its next activity just ahead of the player, and a press that starts nothing says which activity and exactly why: one sentence per `CityRefusal`).
In the rain everything is taken in except entries marked `Rain`; `RainOnly` entries (people under umbrellas) start only then.

## Adding a job

1. A row in `JobCatalogue.All` (and its id as a constant): needs, pay, energy, skill, daily units.
2. A class in `Scripts/Jobs/Kinds` deriving from `Job`:
   - `OnBegin` sets it up where the player stands and returns false with `Refusal` (a locale key the phone can say) if it cannot be done here;
   - `Tick` runs five times a second and is a switch over the job's own steps.
     It says what to show by setting `Objective` (through `Show`), `Status` or `ClockSeconds`, the marker (`Mark` / `Unmark`) and `Prompt`, and only when they change: these are strings, and nothing may build one per tick;
   - `Interact` is the interaction key (`JobKeys`), called only while `Prompt` is set (or always, for a job that `OwnsInteractKey`); `Choices` (three labels) puts a row of three on screen and `Choose` is called with 0, 1 or 2.
     Texts never name a key: they carry `{key}`, which `JobText.T` fills in;
   - `WorksInside(building)` says yes for the one building the job goes on in (a client's shop, picked with `JobSites`); the director then lets the shift continue in there and nowhere else indoors;
   - the player's own body is moved only through `Ctx.Self` (`JobBody`): `Hold` first, then `Clip` or `Pose`; the director undoes all of it on every way out;
   - `OnCrash` and `OnPoliceStop` if driving matters to it;
   - `End` is called exactly once on every way out.
     It gives back every person (`Ctx.Cast.Release`), anything in the player's hands, and pays for work done and not yet paid (`Ctx.Pay`);
   - `Finish(JobEnd.Done / Failed)` when the shift is over.
3. One line in `JobDirector.Create`, a test button in `Core/RealLifeOverhaulMod.cs`, its strings at the end of `Locales/en.json` (several lines for anything said more than once, picked with `JobText.Pick`).

People come from `Ctx.Cast` (two at most; `Put` only at a point that came from `JobSpots`, `StreetSpots` or `CitySpots`, out of the player's sight where the job can choose, and cope with null), or are the game's own pedestrians borrowed through `Ragdoll/PedestrianTakeover` and handed back with nothing of ours on them.
They move only by walking.

Nobody gets into or out of a car except at a spot `JobSpots.BesideCar` found clear: with `job_car_enter` / `job_car_exit` in the bundle they are seen to, otherwise it is the sound of the door (the game has no such animation, and none is faked).
The cast is not used indoors.
