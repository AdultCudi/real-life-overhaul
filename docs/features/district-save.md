# The district save

## In a sentence

Everything in the save that names one of the mod's own places (its district, its streets, the buildings it makes) is moved into the mod's own section of the save at every save and put back right after, and at every load, so the game's own part of the save never names them and a save loads without the mod at any time: the district and what was in it are then simply gone, and come back with the mod.

## Where the code is

- Folder: [`Scripts/DistrictSave`](../../Assets/Mods/RealLifeOverhaul/Scripts/DistrictSave/).
- Read first: `DistrictPlaces.cs` (how a feature names its places), `DistrictSaveDirector.cs` (the timing), `DistrictSaveGame.cs` (every field of the save that can name a place).

| File | What it is |
| --- | --- |
| `DistrictPlaces` | the features' places: whole streets, addresses, districts, ground and the ways out to the game's streets (game-free: EconomyCheck runs it) |
| `DistrictSaveRules` | whose a place is, what each kind of record does, the moves that take records out of a list or a queue and put them back where they stood, the game's line that a save is written (game-free) |
| `DistrictSaveStore` | the line in modData and its round trip (game-free) |
| `DistrictSaveCodec` | the records to text and back with the game's own serializer |
| `DistrictSaveGame` | the one part that touches the game's records: `.Records` (whole records), `.Elements` (elements of the game's records), `.StandIns` (fields), `.Restore` (the load) |
| `DistrictSaveScan` | the self-check of what is about to be written |
| `DistrictSaveDirector` | the save's step, the put back, the two load passes; `.Tests` the test entry |
| `DistrictSaveWatch` | the frame: a put back the game's line did not bring, the waiting log lines, the save hook kept last |
| `DistrictSaveEntry` | the main menu's entry, for the unload that comes after a save is read and before its city is built |

## How a feature names its places

A feature that makes buildings, streets or a district of the mod's registers once, at game start or in its static set-up:

```csharp
DistrictPlaces.Add("new buildings", set =>
{
    set.Street("reallife:street_airport_road");       // every number on a street of the mod's
    set.Address("ba:street_thirteenthstreet", 905);    // or one address: a number no game building uses on a game street
    set.District("reallife:district_airport");        // a district of the mod's
    set.Ground(-2600f, -1700f, -2300f, -1400f);        // its own ground, for the player and the cars on it
    set.Exit(-2290f, 0.1f, -1550f, 270f, "ba:neighborhood_hamptons");   // a spot on a game street outside, and the game's district it is in
}, early: PutMyBuildingsIntoTheGamesLookup);
```

- **Ids.** The mod's street and district ids start with `reallife` and a separator (`DistrictSaveRules.IsModId`). No game id has that prefix, and no future update's or DLC's can, so a place of the game's own (the Midtown Mall, the JFK area, a second city) is never taken for the mod's. A record naming a `reallife` street or district leaves the game's part of the save even if no feature named it.
- **The fill** names what the feature makes, from its own data alone: the same on every machine, nothing of the game's asked, no DLC asked. It is called when a save has been read (before its city loads) and at every city load; `DistrictPlaces.Changed()` builds the set afresh in play (a feature switched off or on). A feature that is switched off, or stands down in a city it does not know, names nothing: its records then stay in the mod's section, written again at every save, until a load where it makes its places.
- **The early step** (optional) runs when a save has been read and before its city's scenes are built. A feature whose buildings hold the player's things (a rented shop, a home) puts its buildings' assets into the game's address lookup there, so that their records go back before the game builds its own lookups from the save's lists (its staff by id, each business's customers, the rivals' lists, the to-do list). Records whose buildings are made only at the city load go back then, with those lookups brought up to date (the to-do list's HUD shows their tasks after its next change).
- **Ground and ways out.** The player and the player's cars out of doors on the mod's ground, or in a building of the mod's, are written at the way out nearest; without a way out, at the first one named; with none, the position stays and the self-check says so.

## At a save

`SaveGameManager.Save` raises the game's save event, then writes the whole save synchronously, then compresses it on a thread. The mod's one handler in that event (Core/SaveHook) runs after the game's own handlers (it is put last again within a frame of anything that joins the event) and, as its last step after every feature has written its lines, calls the district save:

1. Every record that names one of the mod's places leaves the game's lists (each with its index), or its field is written as a stand-in:
   - **A business's records leave with it**: the registration (with its items, rota and shifts, designs, layout, rival owners, poached staff), its staff and candidates, its tasks, transactions, contracts, deliveries, licence fees, deeds, the district's stats and market events, and the plans of an office there (with their insurance offers). What leaves because something else left (a plan whose manager worked there, a task about an employee there, a message whose button acts on them) carries that one's places, so it goes back only with it.
   - **Elements leave the game's own records, which stay**: a campaign by an agency there, a game business's staff from there, shifts of someone who left, a day's accounts' lines for a business there, a product's demand in the mod's district, a staying plan's entries for a store, a destination or a warehouse there, an HR manager's people who left, a rival's moves against them.
   - **The player and the player's cars, contacts and job stand in**: written at the way out (out of doors, the player's current address empty as a new game starts), parked in the way out's game district, a contact with no address, no map destination, no delivery job under way; the real values go back after the write and at a load where their place is made. The save's cars are this player's alone (another Going Public player's car is not in the save).
2. The mod's section is written: `reallife:district_save` (below).
3. The self-check reads what is about to be written (below).
4. The records go back where they stood, the same objects in the same order, the moment the game logs that its file is written ("Game … saved in N ms", or that it could not be): while the save call is still running, so nothing of the game's runs on the shortened lists (the midnight save is made inside the game's hour, before the next hour's work). If that line never comes, the frame after, or at the next save at the latest.

With no places of the mod's and nothing kept from the load, nothing at all is done: one test (`DistrictSaveRules.Idle`, checked to allocate nothing).

## At a load

A save chosen from the main menu is read, the menu's mods are unloaded, and only then are the city's scenes built (UI.Load/LoadingScreen). The district save has two passes:

1. **At the main menu's unload** (`DistrictSaveEntry`): the features' early steps run, then every kept record whose places the game knows (its building in the game's address lookup, its district in the game's list) goes back where it stood. GameManager.Awake, the controllers' Start and TasksUI.Start then see it as the game's own.
2. **At the city load** (the last of the mod's city steps, after every feature has made its places): the rest whose places are made now go back, the game's lookups built from the lists are brought up to date (the staff by id, each business's customers through Core/CustomerDraw, the rivals' lists), and the save hook joins the game's save event after the game's own handlers, before the clock runs. A load that did not pass the main menu (a funeral's backup) does everything here.

A record whose places are still not made stays kept in the mod's section, written again at every save; a stand-in whose place is not made stands (the player and the cars are where the save put them). A record of a kind this version does not know, and one the game's part of the save holds already (the same address, the same id), never goes back. A line this version cannot read (a later version's, or damaged) is kept as it was read under `reallife:district_save_unread` for the version that can.

## The mod's section

One line under `reallife:district_save`: `v=1^r:records^p:payload`. Each record is its kind's code (`DistrictSaveRules`; it never changes), its index, the game's record it stood in (for an element), the mod's addresses and districts it names, its place in the payload, and a stand-in's real values. The payload is the records themselves, written by the game's own serializer exactly as `SaveGameSerializationHelper` writes a save (Odin's binary format under the game's save policy) and read back through the game's own reader of older saves (its migrations of renamed types and enums), then gzip and base64.

Why not the mod's line store: a registration is some fifty fields deep (items with their cargo, the rota, 300 interior designs in a large shop, orders, staff), and any field a game update adds would be lost. Measured under the game's own Mono with the game's assemblies, on a real save of the owner's: its largest shop (a gym with 188 items and 306 interior designs) is 299 KB of Odin, 37 KB gzipped, 50,000 base64 characters, about 5 ms to write and 26 ms to read; ten shops 52 KB gzipped, 7 ms to write. The self-check line gives the size and time of every save.

The whole way through, on a copy of that save (885 registrations) with the gym named a place of the mod's: the save step took out 30 records (the registration, 20 transactions, 7 days' lines of the accounts, a task, a delivery contract) and stood in for a car parked there, in 18 ms; the self-check over the whole save took 9 ms (17 ms the first time in a session) and found the gym named nowhere; the mod's section was 50,036 characters, 27 ms to write the first time in a session and 21 ms to read. The file written by the game's own writer and read by its own reader, as a game without the mod reads it, named the gym nowhere; the load put all 31 back, and the registrations in order with their items and designs, the transactions, the tasks, the deliveries, each day's lines and the cars were the same as before the save.

## The guards

- **Offline, every gate (ContractCheck, "the save's fields that can name a place of the mod's").** It walks every type reachable from `GameInstance` in the game's assemblies on disk (lists, dictionaries, tuples, every subclass of a field's type), by the game's own rule of what is written, and lists every field that can hold an address, a street and its number, a district, a position (the player's, a car's), or an id of someone or a plan that leaves with a business. The list must be exactly [`ContractCheck/SavePlaces.txt`](../../RealLifeOverhaul.Build/ContractCheck/SavePlaces.txt), where each line says what the district save does with the field. A field a game update or a DLC adds (a new list of businesses, a second city's records) fails the gate by name. It also fails when a field the list says is handled has no `// save path:` line in `Scripts/DistrictSave`, when the game starts to read or write `GameInstance.modData` (today it only makes it, in its own initializer), when SaveHook's last step is no longer the district save's, when the save tools no longer leave its key, and when anything of the mod's writes a save other than through the game's `SaveGameManager.Save`.
- **At every save (the self-check).** After the records have left, every value the game's serializer is about to write is gone through by reflection, along a plan made once a session per type by the same rule (a type that cannot hold a place below it, such as a shop's items, is never entered): every address, every street and district by its field's name, every reference to someone or a plan that left. A field a game update added is gone through like the others, so a place of the mod's in it is found before the mod knows the field. With Developer diagnostics on: `District save check: the written save names none of the mod's N addresses (S whole streets) and M districts (…): PASS`; anything still named is always logged, as `… still names the mod's … at <path> = <value>: FAIL`.

## Going Public

- Each machine writes its own save with `SaveGameManager.Save`, and the host serves it back at a join as the file's bytes: the mod's section travels in the file, and each machine puts back its own player's records. Nothing new is sent over the network.
- Going Public strips its own records around every save (a Harmony prefix and finalizer on `Save`, and around its own session saves): it takes them out before the save event and puts them back after `Save` returns, appending what is missing. The district save takes its records out inside the save event and puts them back on the game's line, before `Save` returns: the two are nested, neither sees the other's records, and Going Public's put back appends, so neither undoes the other in either order.
- Another player's business mirrored into this world (a rival-owned registration of a session player's) that stands at a place of the mod's is taken out for the write and put back after it, never kept: Going Public mirrors it again at a join.
- Deterministic: no randomness; the places are the features' fixed data; nothing depends on the DLCs a player owns.

## In another city, and when its feature is away

The district save has no feature of its own and never stands down: every save in any city goes through it. A New York feature that makes places stands down in another city (Core/CityCheck), names nothing there, and its records stay in the mod's section until the player is back in New York.

## The save tools

"Prepare this save for removing the mod" and "Reset the mod's data" leave the district's keys: they hold the player's own things, and a save loads without the mod with them there. The removal tool's report says how many records stay. Its save goes through the district save like any other.

## After a game update

Run the gate. When "the save's fields that can name a place of the mod's" fails on a new field: read what it holds and what writes it; handle it in `Scripts/DistrictSave` (a whole record that leaves, an element of a game record, or a stand-in, with a `// save path:` line); give it a line in `SavePlaces.txt` with its kind's code; run the gate again. A field gone: take its line out and its handling. The same list is the thing to read when a DLC adds a city: its records are the game's own and never the mod's (no `reallife` id), and they need nothing unless they can name a place of the mod's.

## Tests

- Offline: EconomyCheck (`DistrictSaveChecks.cs`: ids, the place set, the features' registrations, the moves on lists and queues to the object and the order, a large list's time, text escaping, the store's round trip, a later version's and a damaged line, the kinds' codes and what each does, the game's line, the idle save allocating nothing); ContractCheck (the census, the list, modData, every save path).
- In the game, Options > Tests, "District save: keep a test record out of the save": a building of the test's own at a `reallife` address (never in the game's lookup), a registration there, a task and a transaction naming it, a district of the test's with its stats, and the map's destination at it; a save written by the game into a folder of the test's own; the game's lists then without the test's things and compared with before; the file read back with the game's own reader and gone through for anything of the test's; the mod's section read for the five records; the folder removed. Refused in a Going Public session and while a save is being written.

## What the parts that make places connect at the merge

- Register with `DistrictPlaces.Add` (a fill, and an early step if its buildings are to hold the player's things); call `DistrictPlaces.Changed()` when its places change in play.
- Make the buildings' city objects in a city load step before the district save's (its step is the last of `RealLifeOverhaulCity.OnLoadAsync`).
- A made building whose address has a registration in the save's list after the load (the district save put it back) uses that one, through the game's own `BuildingHelper.GetBuildingRegistration`; one with none keeps its own out of the list, as before.
- A save guard of its own for the player's address inside a made building is the district save's stand-in for the player: name the building's address in the fill and the guard can go.
