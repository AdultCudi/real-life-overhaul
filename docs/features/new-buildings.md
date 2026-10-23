# New buildings

## In a sentence

The mod puts up buildings of its own, copied at every load from the city's own building designs: a building a feature declares stands at an address on a street of the mod's own, with the game's own door, interior, map look and see-through fade, and nothing of it is ever saved; decorative copies give another feature's place a look of the city's own, a construction site's finished building first of all.

A player sees nothing of it yet: no feature declares a building today. The test entries put up one on open ground by the airport.

## Where the code is

- Folder: [`Scripts/NewBuildings`](../../Assets/Mods/RealLifeOverhaul/Scripts/NewBuildings/).
- Read first: `NewBuildingsRules.cs` (every rule, game-free, run by EconomyCheck's `NewBuildingsChecks.cs`), `MadeBuildings.cs` (declaring a building), `BuildingCopies.cs` (decorative copies) and `NewBuildingsDirector.cs` (the load, step by step).

```
NewBuildingsRules        game-free: a design's name and the building it is measured on, the pick, where a design stands on its
                         lot, the ground's sums, the lot taken when the one asked for is not, what refuses a declaration
NewBuildingsTestPlot     the test building and its six lots north of the airport
MadeBuildings            declare a building of the mod's own, find it after the load, list them and their addresses
BuildingCopies           decorative copies for other features (CopyHandle), and a building's own look hidden and shown
CopyGame                 the copy engine's Game file: the catalogue, the templates asleep, working and decorative copies
CopyGame.Ground          the ground check at every load
CopyLook                 a decorative copy's fade and map look, a ViewBlockingEntity with no controller of the game's; over a
                         building, its hovers and clicks lent to that building
MadeGame                 the made buildings' Game file: record, registration, the game's lookups, the streets, the claim
MadeGame.Save            the made buildings named to the district save, the save guard where it is not present, and the save
                         test's writing and reading back
MadeGame.Tests           what the tests read of the game
NewBuildingsDirector     made at the load, shown and hidden, the made buildings' check, the places named, the save guard's timing
  .Copies                the decorative copies: one change a frame, out of view, or all behind the loading screen
  .Tests                 the six test entries
NewBuildingsGuide        the feature's entry and its block on the options page
NewBuildingsText         the locale keys
Wiring/Wiring.NewBuildingsLinks   the minimap's three bridges
```

## How it talks to other features

- **A feature with a place** declares its building with `MadeBuildings.Declare` and finds it with `MadeBuildings.Get` once the city has loaded (below).
- **The special buildings' book** (`Core/SpecialBuildings`): every made building is claimed there (`SpecialKind.Other`) once its record, its registration and its copy are all in place, so `Core/HeldUnits` keeps the game's own letting off it. The game's ignore list is HeldUnits' business, never this folder's.
- **Construction sites** ask for decorative copies through `BuildingCopies` (below).
- **The floors core** (`Scripts/Floors`) can hold a made building as a host of floors, as it holds the university's building and the towers (below).
- **The district save** ([The district save](district-save.md)) is told every made building standing, with the spot outside its door (`DistrictPlaces.Add` in the director's `Awake`, `MadeGame.NamePlaces`), and alone takes out and puts back whatever of a save names a made address (below). Every address on a street of the mod's own is a made one, standing or not: its id carries the mod's prefix, which the district save keeps out of every save whatever names it.
- **The far districts** (`Performance/Tweaks/FarDistricts`) are handed every copy (`FarDistricts.Include`), so a copy far off is not drawn either, and leave alone a look this folder hides itself (`FarDistricts.LeaveAlone`).
- **The minimap** (`Wiring.NewBuildingsLinks`): `NavBridges.NotMeasured` leaves the made buildings out when the city is measured, so the kept file is the game's alone; `NavBridges.MoreBuildings` gives the box of every made building shown and every copy standing, drawn as hulls of their own; `NavBridges.MoreBuildingsStamp` changes when one is made, hidden, shown or taken away, and the base is drawn again.

## Declaring a building of the mod's own

```csharp
MadeBuildings.Declare(new MadeSpec
{
    Id = "city_hall",                                   // the feature's own name for it, and its claim in the book
    Street = MadeStreets.Prefix + "provinglane", Number = 2,
    Size = "ba:buildingsize_k", Version = 1,            // the game's structure inside: its size and version
    Type = "ba:buildingtype_office",
    District = "ba:neighborhood_midtown",
    Design = null,                                      // or a design's name (DesignEntry.Name)
    Lots = lots, Preferred = 0,                         // where it may stand, in order
    Traffic = 40,                                       // its traffic index, 0 to 100
    Wanted = () => MyFeatureOn,                         // the feature's own option; null for always
});

MadeBuilding hall = MadeBuildings.Get("city_hall");    // after the load; null if it stood down (Player.log says why)
```

- **When.** In the feature's director's `Awake`, or earlier. Every building is made behind the loading screen, once every director is there and before the game's own start of the city lists its buildings, and again at every load.
- **The address** is the feature's own, on a street of the mod's own (`MadeStreets.All`). A new street is one line there, with a three-letter short name and its name in the three languages. The mod's prefix (`reallife:street_`) is one no street of the game's can have, so a free update that fills a street the game left empty never takes a made building's address. The address does not move when the building has to take another lot.
- **What is refused,** with one log line each (`NewBuildingsRules.Refusal`): a street that is not the mod's own; a house number under 1; the game's special building type (its towing reads a special service of the game's); a size, version and type the game has no structure for (`BuildingSizeHelper`, read at the load, never assumed; a structure kept for the game's special buildings does not count); a district the game does not have, or one not in the loaded city (`Core/CityCheck.Here`); a home, unless the game's food deliveries can be told to leave it alone; no lots; a traffic index outside 0 to 100; storeys (`Floors`) outside 1 to 12; an address the game, or another mod, has already.
- **The design.** One asked for by name is given if it can be copied and fits the lot; otherwise one that buildings of its size wear, those of its type first if any, picked by its id. A design drawn in a static batch, of a DLC or of a kind never copied (a special building, a Hamptons house, a taxi destination, a gas station) is never picked.
- **What the game does with it.** The copy keeps the game's own `CityBuildingController`: its door takes the player in through the game's own `EnterBuilding` to the game's structure of its size and version; its card names its address; the closest-building label and the city map know it. Its registration is empty, not to let and not for sale, so its door gives the game's own "business closed", and no delivery picks it.

### A building of a district's own

A district of the mod's whose buildings take part in the game's simulation ([Uptown](uptown.md)) declares them with `Life = true`:

- **Its registration is the save's own:** the one already in the save's list at its address, else the one the district save keeps for it (`DistrictSaveDirector.KeptRegistration`), else a new one put in the save's list. The district save is the one owner of what names it, and the district names its places itself (`MadeGame.NamePlaces` passes it by).
- **Never claimed in the special buildings' book**, so the game lets it, starts businesses in it and lists it as its own; a home may be one.
- **Its floor area** is that of the game's building its design copies when `DesignArea` is set (`Floors` storeys of its size's area otherwise).
- **Its own switch:** the new buildings' option leaves it alone; its district's `Wanted` decides.

### A made building as a host of floors

A made building can hold floors of the floors core ([Floors](floors.md)) as one of the city's does: the core finds a host's registration by address through `BuildingHelper.GetBuildingRegistration`, which reads the game's private lookup first, where the made building's is. What it needs:

- **Declared with its storeys.** `MadeSpec.Floors` (1 to 12, `NewBuildingsRules.MostFloors`, the core's own most) gives its record that many storeys of floor area (`Building.totalSqm`: its size's area times the storeys), which the core divides by one storey's to give its floors; with 1 it holds only its holder's own floors. Inside, it is still the game's one structure of its size and version.
- **Handed over once it stands.** After the load (`MadeBuildings.Get(id)` is not null): a `FloorHost` with `Building = made.Controller`, its `Door` a step out from the door (`made.DoorX`, `DoorY`, `DoorZ`, out along `made.DoorFacing`), its name and its own places, then `FloorsDirector.Hold(host)`. A made building is never one a DLC keeps, and its entrance door is the design's own.
- **Let go while hidden.** A made building switched off, or hidden in a Going Public session that does not agree, has `made.Shown` false and stands switched off: let the host go then (`FloorsDirector.Let`) and hold it again once it is shown. `MadeBuildings.Stamp` changes whenever one is hidden or shown, so a look once a second is one comparison.
- While held, the core opens its doors (the game's "always open"), so its own "business closed" no longer stands in the way.

## At every load

1. The mod's streets go among the game's own (`Streets.AddressHelper`'s list, which the game empties at every load), so it shows their names.
2. The city's designs are measured, from a search of the scene that finds switched-off buildings too: each design once per size, measured on the same building on every machine (one not in a static batch first, then the lowest address), in one order (by name, then size).
3. The ground the game keeps: every building of the game's (a DLC's switched-off ones too), the game's fixed obstacles and every piece of DLC content, shown or hidden.
4. For each declaration, in the order of their ids: the refusals above, then the lots in their order from the one asked for, round to the start. A lot is taken when a design fits it and its ground is clear:
   - inside the city map's bounds and the traffic system's grid (both read now), 40 m in;
   - no traffic lane on it (3.5 m kept from a lane's middle), and one within 60 m for it to front;
   - none of the game's buildings, obstacles or DLC content on it, and none of the mod's own made earlier, 1.5 m kept round each;
   - the player's walking mesh under its middle and corners, level within 1.5 m, and at its door;
   - nothing solid standing there (one physics query from 0.4 m above the ground).

   A lot refused gives its reason in the log line; none free, and the building stands down for this load with one line. So a free update or a DLC that puts something on a lot makes the building take the next one, never cover the game's content.
5. The game's record of it (`Building`, made with `ScriptableObject.CreateInstance`) and its registration go into the game's private lookups (`BuildingHelper.AllBuildingDictionary` and `AllBuildingRegistrationDictionary`, contract lines of their own), the registration first: the game's own lookup of a registration would add one to the save if it found the record alone. Never into the save's list of registrations, never into the city's list of all buildings, so the city's generator, the rivals, the contacts and the taxes never see it.
6. The working copy stands, given its record and registration before it wakes; then the claim in the book.
7. In the game's late call after its own start of the city: each made building is looked for in the city's list, and the option and the session are applied.

## Decorative copies

The API is `BuildingCopies` (`Scripts/NewBuildings/BuildingCopies.cs`); a copy is a `CopyHandle`.

```csharp
// A rebuilt building's new look: a design that fits its place (or one by name), its own look swapped out of view.
CopyHandle look = BuildingCopies.PlaceOver(building, null, null, "civic", true);
BuildingCopies.RemoveOutOfView(look);   // its own look back again, out of view, the copy gone in the same frame

// A plot's finished building, on the ground as CivicWorks.StartPlot has it (middle, yaw, width, depth).
CopyHandle built = BuildingCopies.PlaceOnPlot(null, null, middle, yaw, width, depth, "civic");

// A design at a place of the feature's own choosing.
CopyHandle copy = BuildingCopies.Place("SM_FlatironAreaBuilding_02", null, at, yaw, "civic");

// A building's own look alone: now, or once out of view.
BuildingCopies.HideLook(building, true);
BuildingCopies.ShowLook(building, true);

BuildingCopies.Remove(copy);            // now, whatever is in view
```

| Call | What it does |
| --- | --- |
| `PlaceOver(building, design, size, owner, hideOwn)` | A copy where one of the game's buildings stands: its front on the building's front line, its door where the building's own door is (as far as its footprint may move; a design may be up to 1.5 m wider and deeper on each side than the building), at the height of the ground at that door. A design by name, or null for one that fits the building's place, other than its own, picked by the building's address. With `hideOwn` the building's own look is hidden in the frame the copy stands. |
| `PlaceOnPlot(design, size, middle, yaw, width, depth, owner)` | A finished building on given ground, the arguments as `CivicWorks.StartPlot` takes them: the middle on the ground, the land's turn (its front at -Z, as `PlotRules` has it), width and depth in metres. The design's front stands on the land's front, in the middle of its width. Pass `PlotRules.Frame`'s footprint for a building where the frame stood. A design by name, or null for one that fits, picked by the plot's place. |
| `Place(design, size, at, yaw, owner)` | A design by name at a place and turn of the feature's choosing. |
| `RemoveOutOfView(handle)` | Takes the copy away once out of view; a look it hid comes back in the same frame. |
| `Remove(handle)` | Takes it away now, whatever is in view (a look it hid comes back at once). |
| `HideLook(building, outOfView)`, `ShowLook(building, outOfView)` | A building's own look hidden or shown, now or once out of view. |
| `LookHidden(building)`, `Over(building)` | Whether a building's look is hidden; the copy standing (or waiting to) over it. |
| `handle.Ready`, `Root`, `Wears`, `Gone`, `Why`, `Building` | It stands; its root (and colliders); the design it wears; it is gone or never made, and why. |

- **Out of view.** In play every change a player could see (a copy standing or going, a look hidden or shown) is made only while its place is out of the main camera's view and the city map is shut, as the construction sites' scaffold is set out. A copy asked for during a load, and a look asked to change then, changes behind the loading screen, in the game's late call after its own start of the city. One change a frame; the first copy asked for in play waits while the city's designs are measured, a millisecond a frame.
- **A building's own look hidden** is every renderer under it kept from drawing (`forceRenderingOff`, which the game's own switches leave as it is), its box among them (the building's shadow in the street and its whole look on the city map), and the lights of its signs off (the game switches a sign's light by its object, never the light itself). Its controller, colliders, door, card, address and map marker keep working; the far districts leave its renderers alone while it is hidden. Shown again, every renderer and light is given back as it was.
- **A copy over a building** fades by itself when it stands between the camera and the player (the game's fade finds a hit's parent, which is the copy), and a hover or a click on it is one on the building (its parts are lent to the building), so the player still clicks the building to go in. It is not in the building's fade or city map lists, so a wrap of the construction sites and a copy never share a list. On the city map it shows its own box as the game's buildings do. The minimap draws its box beside the building's own measured footprint.
- A copy never keeps the game's controller, so it can be taken away at any time. A design drawn in a static batch, of a DLC or of a kind never copied is never stood, and the handle says why.
- Switching the option off takes every copy away and shows every hidden look again at once.
- The copies are what this machine shows: a feature shared in Going Public asks for the same copy on every machine, and a design picked to fit is picked the same on every one (by the building's address or the plot's place).

### With the construction sites

The construction sites (`Scripts/Civic`, [Construction](construction.md)) end a rebuild at "wrap down" without changing the building's look, and a plot with its frame clad and wrapped; both are for the integration to finish, not this feature:

- **A rebuild handed over:** once the wrap is down, `PlaceOver(building, null, null, "civic", true)`, and at every load after it again for every finished rebuild (nothing of a copy is saved: the site record is the civic feature's). Asked for during the load, it stands behind the loading screen.
- **A plot handed over:** take the frame and the yard away, and `PlaceOnPlot(null, null, middle, yaw, width, depth, "civic")` with the plot's own middle, yaw and the frame's footprint, again at every load.
- **A wrap round a building with a copy over it** is laid out from the building's own colliders, which no longer match what is seen: leave such a building out of a new rebuild (`BuildingCopies.Over(building) != null`), or lay the wrap out from the copy's colliders (`handle.Root`).
- **The wrap's pieces** join the building's fade lists and the city map's set (`SiteWrap`, part `site_wrap_fade`); hiding the building's own look touches only the renderers under the building, so a wrap still standing stays as it is.
- With Developer diagnostics on, every copy that stands says so: `Construction check (look): <design> for civic over <building> (its door within 0.0 m of the building's, the building's own look hidden) at (x, z), out of view, N part(s) the game's fade finds: PASS`.
- Known limits: the hover outline the game draws round a building shows on the hidden building, so it does not show on the copy over it; the minimap keeps the old footprint under the copy's box.

## Shown, hidden, never destroyed

A made building's copy is never destroyed while the city is loaded. The game's controller puts handlers on the game's hourly, map and registration events in its `Start` that its `OnDestroy` never takes off, and the game empties those events only at the next load. Switching the option off, the feature's clean-up (the options page, or a city that is not New York) and a Going Public session whose machines are not all known to run the mod hide it instead: the whole copy is switched off, so nothing of it is drawn, solid or clicked. Off, nothing is made at the next load.

The copy stands in the scene of the building it copies, not under the mod's own objects, so it goes with the city. Preparing a save for removal (Meta's save tools) destroys the mod's objects in a city that stays: the made buildings stay hidden where they stand until the city goes, and every record that names a made address is taken out of the save in memory for good by the district save, which the tools ask after the features have stopped and before their save, and kept in the mod's part of the save to come back with the mod. The records and the streets the mod made by code are destroyed when the city goes, and only then.

## Saves and loads

Nothing is saved: every made building is made again at every load, and a building no longer declared is simply not there.

What names a made address in a save has one owner, the district save ([The district save](district-save.md)), whatever is switched off. At every save, as the last of the mod's steps in the game's save event, it takes out everything that names one and puts it back the moment the game has written its file, exactly where it was:

- **inside a made building:** the save is written with no current address and the player outside its door, facing out (`MadeGame.NamePlaces` gives it the spot, 1.2 m in front of the door), so it loads with or without the mod with the player there; nothing of it is kept;
- the player's own destination, a mission under way that names one (a delivery round, a food delivery), the day's delivery addresses and the food delivery offers that name one, any registration on a street of the mod's own, and every other record of the save that names one: written as nothing in the game's part, and kept in the mod's part of the save, to come back at a load where the building stands.

The save guard (`MadeGame.Save`, the director's `OnSave`) steps aside while the district save is present (`DistrictSaveDirector.Present`, true once the district save's city step is done), so no record is moved twice. It takes out the first four kinds only in a city whose district save step failed, and puts them back a frame later; each of the two takes out only what is still there and puts back only what it took, the later taker first, so even then nothing is moved twice. EconomyCheck's `DistrictSaveChecks.cs` and ContractCheck's district save section hold both.

## Going Public

Every machine with the same game and the same mod gets the same buildings in the same places: the catalogue's order, the building each design is measured on, the pick by the id's FNV-1a hash, the lots tried in one fixed order and the ground read from the scenes' own data, switched-off DLC content included. Nothing is sent. The buildings stand only while every machine is known to run the mod: a guest following its host's city (`SharedCity.Following`), a host its guests follow (`SharedCity.Leading`). A guest with no mod at all is not seen by the host; such a player simply does not see them.

## Another city, DLCs and free updates

- The city table (`Meta/FeatureCities`) calls the feature New York's: its lots are measured from New York's map. In another city it is not started.
- Nothing the game keeps behind a DLC is copied (`Core/GameDlc.Gated`), nor a building with a DLC's content in it or round it (`ToggleObjectDLC`), and whether a DLC is owned is never read. The city's list is read only through `GameDlc.Open`.
- The test plot is very likely where a free update puts its JFK Airport and Mall. It is the tests' alone, and like every lot it is checked at every load: the test building stands down, or moves along, when the game's own content is there.

## Traps in the game, and how they are kept clear of

- The game's special building type needs a special service of the game's: refused.
- The game's food deliveries send orders to the city's homes, and an order names its address in the save: a made home goes on the food deliveries' own list of addresses they leave out (put back as the game had it when the city goes), or is refused.
- `BuildingHelper.GetBuildingRegistration` adds a registration to the save when it finds a record with none: the registration is in the lookup first.
- The controller's handlers on the game's events (above): never destroyed, hidden.
- The game's closest-building job is sized from the city's list when it is made: a made building is in that list because it stands before the game's start of the city lists its buildings.
- A building drawn in a static batch would draw at the original's place: never copied.
- A copy of a scene object woken where it is made runs the game's start for it there: templates are made under a holder that is switched off, and a copy is switched on only where it stands.
- A copy of a building's car park entrance or drive-in would be a second one of the original's number: taken off a working copy.
- The Hamptons' houses are one prefab outside and in: never copied.
- A feature that picks from the city's list may find a made building there: leave out one for which `MadeBuildings.Of(controller)` is not null, or whose street is the mod's own (`MadeBuildings.IsMadeStreet`).

## Cost

Idle: one time comparison a frame and a look once a second. A load with buildings declared: three searches of the scene and the measuring behind the loading screen, then for each lot tried a pass over the traffic lanes and the kept boxes, a few samples of the walking mesh and one physics query; a load with none declared searches nothing. Copies: while any change waits, the camera's planes once a frame and a box test for each change waiting, and one change a frame once its place is out of view; a millisecond a frame while the catalogue is measured in play. A copy shares the game's meshes, materials and textures, so it costs no memory on the graphics card; what ten cost to draw is the frame cost test's.

## Tests

The six entries are on the options page with the test tools shown. The test building is made only with Developer diagnostics on (under Troubleshooting), so the entries that use it need that too; the two copy tests do not. Each says why not in a notice when it cannot run, and writes its check lines in Player.log.

1. **Make the test building at the next load.** Load a save: `Made buildings check (test building, 1 Proving Lane): in the game's building lookup YES, in the city's list YES, in the save's registrations NO, the closest-building job sized to the list: PASS`.
2. **Walk into the test building and out.** `Made buildings check (way in): entered on structure C1 (...), exit zone found: PASS`, then `Made buildings check (way out): the player is at its door: PASS`.
3. **Save inside the test building and read the file back.** A recovery save is written and read back by the game's own reader: `Save check: the written save names no made address (registrations 0, current address empty, mission none ...): PASS`, and with Developer diagnostics on the district save's own lines, `District save check: the written save names none of the mod's ... (... 1 stand-in(s) ...): PASS` and `District save: put back after the write (the game's line that the file is written): ... PASS`. Loading that save with the mod switched off puts the player outside, at the door, and so does loading it with the mod.
4. **Copy the nearest building.** A copy of it stands 40 m ahead until the next load: `Construction check (copy): <design>, 0 in a static batch, fade linked: PASS`. A building drawn in a static batch is refused with its reason.
5. **Frame cost with ten copies in view.** The frame is measured for 4 s without copies, then for 4 s with ten made one a frame: `Copy check (frame cost): ... (wanted under +0.5 ms): PASS`.
6. **Switch them off.** Every made building hidden, none destroyed, every copy taken away: `Made buildings check (switched off): ...: PASS`, and at the next game hour `Made buildings check (hidden, the next hour): the hidden building's hourly handlers ran without an error: PASS`.

Offline, EconomyCheck's `NewBuildingsChecks.cs` (`--new-buildings` runs it alone): the design's name and the building it is measured on in any order of the scene, the pick (by size and type, and for a plot or a new look the design that fills the ground), where a design stands on its lot whichever way lot and door face, a copy's door brought to the building's, the ground's sums, the lot taken, every refusal, the test plot, the load's cost, and the game's side read as text: nothing of a made building added to the save's lists, the registration before the record and the claim after both, no made building's copy destroyed and none under the mod's own objects, what the mod makes by code destroyed when the city goes, every member of the game's through `Core/GameContract`, the made buildings named to the district save with their doors and the save guard stepping aside while it is present, the save guard and the save tools' removal, nothing sent, the DLC rule and the city table, every copy's change out of view and a swap in one frame, the plot as the construction sites give it, and the texts in en, pt and zh-cn.
