# Uptown

## The district and its buildings

North of Manhattan, on the ground the lots lay, Uptown is a district of the game with an id of the mod's own, `reallife_neighborhood_uptown`. A quarter of its first block stands from the first day as buildings of the game's own; the rest of its lots wait for the construction game, and every building finished there joins the district at the next load. Its buildings take part in the game's own simulation as its districts' do: the game's businesses open there, rents and real estate are priced there, BizMan, the Market Insider and the city map's rows and search know it, the city's people shop and work there, and the player rents and buys there. Everything that names it leaves every save for the mod's own section and comes back at a load ([The district save](district-save.md)), so a save loads without the mod at any time: Uptown and what was in it are then simply gone, and come back with the mod.

### Where the code is

- Folder: [`Scripts/Uptown`](../../Assets/Mods/RealLifeOverhaul/Scripts/Uptown/), the files below (the lots, the ground and the streets are the ground's own files in the same folder).
- Read first: `DistrictValues.cs` and `BuildingsRules.cs` (every rule, game-free, run by EconomyCheck's `UptownLifeChecks.cs`), then `LifeDirector.cs` (the load, step by step).

```
DistrictValues       game-free: Uptown's figures from Midtown's and Hell's Kitchen's, Build 3682's written down, its people's
                     class table and price index as the game makes them, its quad on the city map, its land's price
BuildingsRules       game-free: the first day's buildings, a lot's traffic, the game's structure a lot is built with, what
                     refuses a finished building
LifeStore            game-free: the district's line in the save (the buildings the construction game finished)
LifeText             the locale keys
LifeGame             the one part that touches the game: the district in the game's lookups and tables (.Buildings its
                     buildings in the game's lists, its first life; .Map the city map; .Tests and .Player what the tests and
                     the stand-down read and ask)
LifeDirector         the load, the stand-down, the district save's view of it (.Lots the construction game's side, .Tests)
DistrictBuildings    the construction game's way in
LifeGuide            the tests in the feature's entry (the ground's entry owns the option and the clean-up)
```

### A district of the game

The game keeps its districts in `NeighborhoodHelper` (a private lookup that `GetData` reads and throws without, a list and an array), which it makes again from its data every time a save is read. Uptown's record is a `NeighborhoodData` the mod makes with `ScriptableObject.CreateInstance`, and goes in three steps:

1. **When a save has been read and before its city's scenes are built** (the district save's early step): the record into the lookup, the list and the array; an empty list of its buildings in `BuildingHelper.AllNeighbourhoodBuildings` (the game's daily pass reads one for every district with stats); its row in the people's class table and price index (`CitizenHelper`, only once the game has made them, as its own start throws on a row put there before it) and in the wholesalers' table; and its share of offices and shops the game keeps empty to let, in the two building types' targets (given back as the game had them when the city goes). Every list the game builds as the scenes load then has Uptown as one of its own: BizMan's districts, the Market Insider's, the people's tables.
2. **In the city's load**: its buildings declared to the made buildings' engine as buildings of the game's own (below), made behind the loading screen; then each that stands into the game's list of all buildings and the district's list, a carving box over it on the walking meshes, and the district's quad and row on the city map. This step comes before the district save's, which then puts back what the save kept of them.
3. **In the game's late "game loaded" call**: where the save never had Uptown, its stats (its first days from the save's seed) and the game's own generator for it alone (`CityGenerator.InitializeCity` and `DistributeBuildingsToRivals`, the engine's chance seeded from the save and put back after), as the game sets up a district it adds to an older save; a new city is set up by the game's own generator with the rest. Then the first load of each building the construction game finished.

It lives only where the ground's own verdict says (`GroundSession.Agrees`: the option, and a Going Public session only when every machine runs this mod in this version), in a save of New York's (a city set up whose districts' stats name none of New York's is another city's), and with its buildings: at a load where none stands (the ground not laid, the feature away in another city), it leaves the game's districts again at the city's step, and what names it is kept in the mod's section.

**Its figures** are Midtown's and Hell's Kitchen's, half and half, read from the game's own data at every load (Build 3682's are written down for when they cannot be read, and the census says which changed):

| Figure | Midtown | Hell's Kitchen | Uptown |
| --- | --- | --- | --- |
| working, middle and upper class | 0, 6, 94 % | 6, 82, 12 % | 3, 44, 53 % |
| real estate multiplier | 2.2 | 1.3 | 1.75 (land 35,969.5 a square metre) |
| parking, an hour | $15 | $7.50 | $11.25 |
| customers' weight | 0.9 | 0.7 | 0.8 |
| cars on its streets | 100 % | 90 % | 95 % |
| interior score asked for | 50 | 0 | 25 |
| marketing's strength | 0.5 | 0.7 | 0.6 |
| a business closes under, a day | $850 | $700 | $775 |
| offices and shops kept empty to let | 15 and 6 % | 20 and 10 % | 18 and 8 % |

The base price of a square metre (20,554) and the rent's multiplier (0.00056) are the two's own, and offices' goods sell there. Its people's price index is 1.553, worked out as the game works out every district's. On the city map its quad is a copy of Midtown's laid over its ground, its south edge where Manhattan's zones end (they reach past the old wall), in a green blue no district of the game's has, at the game's own alpha, with Midtown's icon. Its name is its id's text in the locales, as the game names its own districts.

### Its buildings

- **From the first day:** a quarter of the first block's lots (21 of 83), halves up, those nearest Manhattan, a lot on an avenue counted 30 m nearer; in the order of their ids. Each is built with one of the game's structures for its class's sizes (never one kept for the game's special buildings), picked by its id's hash among them in one order, in a design the copy engine picks for its size, and with the floor area of the game's building its design copies.
- **A lot's traffic index:** 38 on an avenue, 22 on a street, 2 less for each 25 m further from Manhattan than the plan's nearest lot (five steps at most), then up to 3 more or less by its id's hash, kept from 10 to 60.
- **The rest** of the lots are empty, for the construction game. A building it finishes stands from the next load (below).
- **Its registrations** are the save's own while the city runs: the one the district save keeps for its address (`DistrictSaveDirector.KeptRegistration`), or a new one in the save's list. Nothing asks the game for them before the district save's city step (`GetBuildingRegistration` would make an empty one and the kept one would be let go).
- **Never claimed in the special buildings' book**, so the game lets them as its own: its daily pass starts businesses there, the rivals own and let them, and BizMan lists them as it lists the game's.

### The mod's tables by district

| Table | Uptown | Why |
| --- | --- | --- |
| Precincts | left out | six stations in New York's districts; its streets are served by the nearest |
| Hotels | left out | every hotel slot is in one of New York's districts |
| Broadband (the player's own network) | left out | the network is built district by district over New York's seven |
| Internet | takes part | wireless and copper reach every district, so they reach its businesses |
| Civic wards | takes part | an eighth ward after the seven, whose saved indices stay |
| The city economy | takes part | its income starts from its own class shares (1,176, between its neighbours') |
| Brands | takes part | its units are sites like any district's; in the brands' order it comes after the game's |
| Flare | takes part | a home district of the city's people after the seven; another city's Flare keeps the seven |
| The district view | takes part | its crimes are counted by its id |
| Online | takes part | after the districts known, whose orders keep their indices |
| Faults, Hiring, Estate | take part | they read the game's own id on its buildings and registrations |
| Fame | takes part | its people's opinion of the player, by its id |
| Social | takes part | a person's home district keeps its name, as Uptown comes after the game's |

### Saves

Every record that names its district, its streets or its buildings leaves the game's part of every save and comes back right after: its registrations with everything in them, its stats, its market events, its products' demand, a ticked row of the city map's filters, and every record that names one of its addresses; the player inside one of its buildings is written outside its door, the player and the cars on its ground at the way out on 1st Avenue, a car inside one parked in Midtown. The district's own line (`reallife:uptown`) holds the buildings the construction game finished, and the save tools leave it with the district's records.

### Going Public

Uptown is the same on every machine with the same mod: its figures, its lots, its first buildings, their structures and traffic, and its first life are worked out from fixed data and the save's seed alone, and nothing is sent. It lives only in a session the ground's verdict agrees to. When a session stops being one, it stands down for the rest of the session, and the player is told. A building of another player's is never set up, and a finished building is the city's in a session, never a deed made on one machine.

### Standing down

At a load with no building of it, when the ground's feature cleans up (another city, its faults, the save tools), or when a session's verdict turns: the player is taken out of a building of it by the game's own way out; then every record that names it leaves the save in memory for the rest of the city, kept in the mod's section (`DistrictSaveDirector.KeepOut`); its buildings leave the game's lists and are hidden, never destroyed; the district leaves the game's list and array of districts and the city map. Its record stays in the game's lookup for anything that still holds its id. It comes back at the next load where it may live.

### For the construction game

```csharp
if (DistrictBuildings.Lives && !DistrictBuildings.Taken(lotId))   // UptownLots.Get(lotId) for its place and its sizes
{
    int traffic = DistrictBuildings.TrafficOf(lotId);              // what its building's traffic index will be, 10 to 60
    float land = DistrictBuildings.LandPricePerSqm;                // the district's land, a square metre
    bool raised = DistrictBuildings.Finish(lotId, design, size, version, type, storeys, FinishOwner.Player, price, out string why);
}
```

- `Lives`: Uptown is a district of the game with its buildings standing at this load. `Stamp` changes whenever a lot's state does, so a look once a second is one comparison.
- `Taken(lotId)`: a building stands there, from the first day or finished, or one finished there waits for the next load: not for sale. `Finished(lotId)`: a district building stands there at this load, so a look of the construction game's own is not wanted.
- `Finish(...)`: from the next load the building stands there as one of the district's, in the design named (a `DesignEntry`'s name, or null for the copy engine's pick, the same on every machine), with the game's size, version and type, and `storeys` of floor area (1 to 12; 0 for the design's own). `FinishOwner.Player`: at its first load the game's own deed is written for the player as BizMan's purchase writes one, with `price`; `FinishOwner.City`: let to the city's businesses. Refused, with why: Uptown not living, a lot unknown or taken, the game's special type, a size the lot's class does not take, a structure the game lacks, storeys outside 0 to 12, a player's deed in a Going Public session, a price that is no sum of money.

### Tests

- Offline: EconomyCheck's `UptownLifeChecks.cs` (its figures, the price index and class order, its quad, the first day's buildings and their traffic in any order of the lots, the structures, every refusal, its line's round trip and bounds, the district save's view, the city check with it, every table above with it and without, and the sources: only its game files write the game's tables of districts, the load's order, the stand-down's order, nothing left to chance, nothing a frame); ContractCheck (every member of the game's read by name, and the save's fields that can name a district).
- In the game, Options > Tests, each with one "Uptown check (...)" line in Player.log:
  - "Uptown: district census": the district found by the game's own `GetData`, in its list, its zone and filter row, its buildings in the game's lists and their registrations in the save's.
  - "Uptown: a business opens there": the game's own daily pass for a new business, run for Uptown alone.
  - "Uptown: rent a unit there": the nearest unit to let made the destination; renting it in BizMan is the player's, with the game's own button.
  - "Uptown: save and read back": a recovery save written and read back by the game's own reader, gone through for anything of Uptown, the save in memory as before; the save is kept, to load with the mod switched off.
