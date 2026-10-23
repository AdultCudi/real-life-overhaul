# Floors

## In a sentence

The mod's places on floors of the buildings it already holds, so a new place (City Hall, a job centre, a licensing office, another firm) takes a floor and not another of the city's buildings: one core for the university's classrooms, the tech companies' towers and every place to come.

## Where the code is

[`Scripts/Floors`](../../Assets/Mods/RealLifeOverhaul/Scripts/Floors/). Read `FloorsDirector.cs` and `FloorPlace.cs` first.

| File | What it is |
| --- | --- |
| `FloorsRules` | how many floors a building holds, which place goes on which spare floor and when a building is taken for places, the panel's pages, the door's floor (game-free: EconomyCheck runs it) |
| `FloorLoan` | what the floors hold of the game's while the player goes in, moves and leaves, and what each of the game's steps gives back (game-free: EconomyCheck drives it through every way in and out) |
| `FloorHost` | a building the mod holds, as its holder hands it over: the building, the place outside its door, its name, its own places from floor 1, and its holder's words |
| `FloorPlace` | a place on a floor: what a feature declares (an id, a name, the structure wanted, its furniture, its door's words) and the hooks a richer place overrides |
| `FloorVisit` | the player on a floor: which, whose place, where they came in (the lift is there), what is on show |
| `FloorsDirector` | the one component (no Update): a one-second timer, the game's enter, exit and save events, the book of buildings and places (.Book), the panels at the door and the lift (.Panels), going in and every way out (.Entering), the lift (.Lift), the test (.Tests) |
| `FloorsGame` (and .Layout) | the only file touching the game's API: a building's size and version, the manager's registration, its dirt spots, the doors' "always open", going in and out, the warp and the fade, the structures the game has, a layout laid in |
| `FloorsText` | the words |

## How it talks to other features

- **Holders** hand their buildings over and take them back: the university (`Careers/CampusInterior`: its building, four floors of classrooms) and the tech companies' towers (`Tech/Sites/TowerFloors`: a building a tower, a company a floor). Their door's and lift's words, their hours, their default floor and their furniture are theirs, through `FloorHost` and `FloorPlace`.
- **Places** that need a floor ask for one (`FloorsDirector.Ask`): the architects' practice of the building trades (`BuildTrades/TradeOffice`, [Building trades](trades.md)) and the test.
- **FitOut** furnishes a place's floor (`FitOut/FitView`), told the structure on show when the floor borrows one, so the game's designs are that structure's.
- **Core/SpecialBuildings** takes a building for places that found no spare floor (`Set` and `Remove`, its existing API); the book of special buildings tells `Core/HeldUnits` of it, which keeps the game's letting off it. The floors never touch the ignore list.
- **Core/MapPlaces** shows a building taken for places under a row of its own ("City Offices").
- **GameUi/DoorPanel** and `WorldPanel` are the game's own panel at the door and the lift.
- **Core/CityCheck** and the city table (`Meta/FeatureCities`): the floors have no line of their own there; they follow their hosts. Each building and place names the feature that holds it (`FloorHost.Feature`, `FloorPlace.Feature`: the university's `careers_study`, the towers' `tech_sites`, the test's `diagnostics`). While that feature stands down in another city (or under the Diagnostics test "Stand New York's features down, as in another city"), or is switched off after its faults, its holder's code no longer runs to let it go, so the core puts the building or the place away itself, as a `Let` or a `Release` would, and takes it up again once the feature is back. A building or place held or asked for meanwhile waits the same way.
- **Core/GameDlc**: a building a DLC keeps is never held (`Hold` refuses it with a line) and never taken for places (`FloorsGame.PickClaim` walks the city's buildings through `GameDlc.Open` and tests each with `GameDlc.Gated`). The floors read no DLC themselves.

## A building the mod holds (a host)

A holder sets the building, the place outside its door, its name and its own places, then calls `FloorsDirector.Hold(host)`; again whenever its places change; `FloorsDirector.Let(host, why)` gives it back. A host names the feature that holds it (its constructor's second argument) and needs a `CityBuildingController` with its `Building` (address, size, version, floor area) and an entrance door, never one a DLC keeps; its registration is found by address (`BuildingHelper.GetBuildingRegistration`, the game's private lookup first), never in the save's list or the city's list of buildings, so a building made at run time serves as well as one of the city's. While held, its doors let the player in (the game's "always open" on each, put back as found when it closes or is let go).

## Which place is on which floor

- **A building's floors:** its floor area over one storey's (the game's interior for its size, `BuildingSizeData.squareMeters`), never fewer than its holder's own floors and at most 12 (`FloorsRules.MostFloors`: four pages of the panel). The university's building (5,971 square metres, a 225 square metre storey) holds 12; a tower holds its storeys, never fewer than its four companies.
- **A holder's own places** stand from floor 1: the university's four floors, a tower's companies.
- **Spare floors** go to the places that asked: the towers' first, then the university's building, then a building taken for places (`FloorsRules.Rank*`); buildings of a turn by address, places by their id, each place the first run of free floors that holds it (`FloorsRules.Assign`). The answer depends on the buildings and places alone, never on the order they came in: every machine of a Going Public session gives the same.
- **No spare floor anywhere:** one building is taken for all the places that found none, never one a place (`FloorsRules.ClaimsFor`): the largest office of the city's own that stands empty and that nothing holds (no feature, no reservation, no conversion, not the player's or another player's, never one a DLC keeps), ties by address. At most four are ever taken. One that no place needs any more is given back once the player is not in it; one that is let meanwhile goes back and another is taken. With none to take, the places wait, and the city is looked at again once a minute.
- **Released:** a place's floor goes when its feature asks (`FloorsDirector.Release`); places after it by id move down; a holder's own floors never move.
- **Nothing is saved.** It is worked out again at each load from what holds and asks.

## The door and the lift

- **At the door,** within 3.2 metres of the place outside it, out of a vehicle and with no screen of the game's up: the game's own panel (`WorldPanel` through `DoorPanel`) with the building's name, its holder's line (the university's hours, a tower's companies) and a button a floor named after its place. A place may mark its floor in blue (the university's class now, the player's employer's floor) and grey it while shut (the university's hours). The university's panel stands while it is closed, its floors greyed; a tower's only while it stands empty.
- **At the lift,** within 2.6 metres of where the player came in: the other floors.
- **Pages:** with more than four floors to offer, three a page and "More floors" on the fourth button, which turns to the next page at once, on the press, and from the last back to the first (`FloorsRules.Page`, `NextPage`).
- **The game's own door** (a click on the building) takes the player to the holder's door floor: the university's class now, else the first course's; a tower's the employer's, else the first. Where that floor stands on another structure, the click is watched for while the player is outdoors within 12 metres of the open door, so the building reads as that structure in time.

## A floor on another of the game's structures

Every building of a size and version shares one interior of the game's (`BuildingSizeResolver`), loaded by `BuildingManager.EnterBuilding` from the building's size and version. A place may want another structure than its building's own (the university's classrooms stand on the game's school, D1; the test's office on another office's):

- **Borrowed while the game loads it.** From the moment a going in is asked (or the game's own click is seen) the building's `BuildingSize` and `BuildingVersion` read as the floor's structure; the moment the game has entered (its `onEnterBuilding`), they are the building's own again. Its registration's dirt spots, which the game rewrites for the structure it loads, are put back as they were.
- **The registration of the place's own.** On a borrowed structure the manager's registration is the place's own while the player is on the floor (made from the building's as the game has just loaded it, so its dirt spots are the structure's), in no list of the save. The university's is the school's, with its hours; a plain place's is an empty unit with its name.
- **The lift to another structure** goes through the game's own door again, the building read as the next floor's structure: the game leaves the building first and comes back in behind its own fade. To a floor of the same structure the lift is the core's own: a quarter-second fade, the floor's furniture taken down, the player where they came in, the next floor's furniture up, light again once it stands (six seconds at most).
- **Any structure the game has:** the test picks the largest office structure that is neither D1 nor the building's own (a shop's if there is none), from the game's own list of structures by building type. A Hamptons house and the car park are never a floor.

## Every way out gives back what was borrowed

Each of the game's steps goes through `FloorLoan`, which says what to give back; EconomyCheck drives it through every way in and out and counts each thing given back exactly once.

| Way | What the floors do |
| --- | --- |
| In by a floor's button, the Campus app, the game's own door, the way back after a load, a test | the size and version borrowed until the game has entered, then back, with the dirt spots |
| The game refuses, fails, or goes into another building | given back when its going in is over (the settle's look), or at once |
| The game takes longer than 15 seconds | given back regardless, and the floor not waited for |
| Out by the game's door, sleep elsewhere, fast travel, a taxi, a faint to hospital | the visit ends; the game drops the registration of the place's own with the building |
| The lift on the same structure | only the floor changes; nothing is borrowed |
| The lift to another structure | borrowed again for the game's going in; the game's exit first ends the floor; back once it has entered |
| A save on a floor | nothing borrowed is written: a building's size is never saved, and the place's own registration is in no list of the save |
| A save loaded on a floor | the game loads the building's own structure; found inside on no floor, the player is taken to the door's floor through the game's door again (or out while shut); on a tower's own structure at once, the lift where the game's door brings them in |
| A place released, its holder's option switched off midway | a going in under way gets the building's own structure; on its floor the building's registration is put back and, on a borrowed structure, the player is taken out to the street |
| The floors stand down after a fault | as a release, for every floor |
| The holder's feature stands down in another city, or is switched off after its faults | as a release, for every floor of its building or for its place's; taken up again once the feature is back |
| The scene unloading | the size and version given back at once; nobody is walked out |

## Furniture

A place where something is worked or studied has the furniture for it, the game's own first:

- **The game's layout** (`FloorPlace.Layout`): a business type's layout for the floor's structure, laid in the way the game lays one, a quarter a frame over the four frames the game keeps the screen dark (the university's school, its classroom doors last).
- **FitOut's fit-out** (`FloorPlace.Fit`, `Designs`): a kind of place (`FitKind`: a city office, a tech office...) round the door, the game's designs of those kinds for the structure on show first.
- **The place's own** (its hooks): a tech company's lobby and the rest of its floor (`Tech/Sites`).

## Putting a place on a floor

```csharp
_office = new FloorPlace("cityhall", "civic", "reallife-cityhall") { Structure = new FloorStructure("ba:buildingsize_c", 1), Fit = FitKind.CivicOffice };
_office.Designs.Add("LawFirm");
FloorsDirector.Ask(_office);                        // a floor, now or as soon as a building has one
if (FloorsDirector.Where(_office, out FloorWhere at))   // its building, address, floor and door: for the map, the guider, the Guide
{
    ...
}
FloorsDirector.Release(_office, "its option is off");
```

The rules a place keeps:

- **An id of its own,** the same on every machine: spare floors go by it.
- **Its feature,** the one of `Meta/FeatureList` that declares it (here `civic`): its floor is put away while that feature stands down in another city or is switched off, and comes back with it.
- **A name** (a locale key in the three languages, or a proper name never translated) and its button's words (`Button`: by default "Floor {floor}: {place}").
- **A structure the game has,** or none for the building's own.
- **Its furniture for what is done there,** the game's own first (`Layout`, else `Fit` with `Designs`, else its own hooks).
- **Released when its option is switched off** and asked again when it is on: with the option off the game behaves as without it.
- **Nothing of it saved.** What it needs is worked out again at each load.
- **Its hooks** (`Arrive`, `Laid`, `Moved`, `Shown`, `Leave`, `Ready`, `Tick`, `Busy`, `Open`, `Pressed`, `MakeShadow`) are called on the main thread from the timer or the game's events, never per frame; a hook never moves the player or asks the game to go in itself (`FloorsDirector.Enter` and `Lift` do).

## Going Public

- **What the indoor tag is.** Going Public shows another player indoors only while their building tag equals this player's (`RemotePlayerManager`: `p.Bldg == MPRegisterSync.CurrentShopAddress`). Both are made of the manager's registration's number and street, `"{StreetNumber} {StreetName}"` (`GameStateReader.AddressKey`): the one sent is read live every tenth of a second while inside (`MPCanvasUI`, the player's position message), this player's own is set when entering finishes (a prefix on `BuildingManager.DelayedEnterBuildingActions`, `MPPatches`) and healed every half second (`MPRegisterSync.TickContextHeal`).
- **What that means for floors.** The registration of the place's own carries the building's street and number, and a tower's floors keep the game's registration: every floor of a building gives the same tag. Two players on different floors of one building are shown to each other, in one room (the same structure is at the same place on both machines).
- **Why the core does not keep them apart.** The only thing the tag is made of is the registration's number and street. A registration of the place's own with another street or number per floor would give each floor its own tag (the game reads the building through `BuildingRegistration.BuildingCached`, which can be set), but Going Public would then send, on every machine, an address no machine's game has: its interior requests, its shop context, its customer streaming and its rosters all go by that same string. That is a change to what is sent, which the mod does not make. So the floors send nothing new and change nothing of another player's; two players on different floors of a building see each other.
- **Deterministic.** Which place is on which floor, and the building taken for places, are the same on every machine with the same options and the same mod, in the same city (a feature switched off after its faults on one machine is there as its option off), and nothing a DLC keeps is held or taken, so a player without a DLC is never sent to a door the game refuses them.
- **The two-machine test.** Two machines in a session, the same mod, Developer diagnostics on: player A on the university's floor 1, player B on floor 2 (the lift). Expected now: each Player.log has Going Public's "[InteriorMask] '<the other>' shown" line and the floors' "Floors: in this Going Public session another player is shown indoors by the building's address alone" line; A sees B standing in A's floor's rooms. Then B leaves by the door: A's log says "hidden".

## The cost

No Update. A one-second timer: the doors' flags of each building held, the book when something changed, the panels within reach, a place's tick on its floor. While the player is outdoors within twelve metres of an open door whose door floor stands on another structure, one flag a frame (the game's own click is seen before it loads). The lift and the panels' buttons work on their presses only. The book is worked out again only on a change.

## Diagnostics

With Developer diagnostics on, one line per event, never per frame:

- `<place's check>: asked by <how>: the building read as D1 until the game has loaded it (was ba:buildingsize_c 2): PASS` and `<place's check>: the building's own size put back (the game has loaded <the structure>): PASS`; for the university its own `Careers check (university way in)`, for the test `Floors check (way in)`.
- `Floors check: floor N of <address> on structure <K1>, the building's own size and version put back on the way out: PASS`, on every way off a floor of another structure.
- `<place's check of its layout>: frame N, n of m pieces, x ms: PASS` while a layout is laid (the university's `Careers check (university floor)`).
- `Floors check (save): on floor N of <address>: the building reads as its own C2; the manager's registration is the place's own, in no list of the save: PASS`.
- The panels' own lines, named by their holder: `Careers check (university door)`, `Careers check (university lift)`, `Tech sites check (tower door)`, `Tech sites check (tower lift)`.
- `Floors:` lines for each going in, arrival, lift, way off, building taken and given back.
- `Floors: <building> (<id>) is put away while its feature '<feature>' stands down in this city; it comes back with its feature.` and `Floors: <building> (<id>) is held again, its feature '<feature>' back.` (a place's: `the floor of <place> is put away`, `<place> asks for a floor again`).

## The test

Options > Tests > Diagnostics > "Floors: put a test place on a floor" (with "Show test tools" on): a test office (FitOut's city office, the game's office designs first) on a spare floor of a building the mod holds, on the game's largest office structure that is neither D1 nor the building's own, and the player taken there by the game's own door (from inside another building the game leaves it first). It stands until the button is pressed again or the city goes; pressing again takes it away (and the player out, if they are on it). With no spare floor, no empty office to take, or the player in a vehicle or busy, a notice says why.

## Offline checks

EconomyCheck's `FloorsChecks.cs`: the floors a building holds, the door's floor, the structure rule, the panel's pages for one to twelve floors from the door and every lift, the spare floors in the towers' and the university's turn and the same in 300 orders, a release, one building for many places and never more than four, every way in and out through the loan with each borrow given back once, and over the sources: the university and the towers on the core, each building and place going with its feature in another city (the university's and the towers' New York's in the city table, the test's any city's) and put away while it does not run, nothing a DLC keeps held or taken and no DLC read, a building's size and version, the manager's registration, the doors' flags, the going in and the warp written in `FloorsGame` alone, every answer of the loan acted on, every borrow followed by the settle, nothing saved, no Update, the self-check lines and the test, and the words in every language. NavCheck's `TowerCheck` checks the towers' pages on `FloorsRules.Page`, and `MapRowCheck` the row of a building taken for places.

## Not verified without the game

That the size borrowed for a structure other than D1 loads that structure with an exit zone the player is put at (the test proves it: the "Floors check" lines and the office around the player); how a fit-out looks on a borrowed office structure; the lift through the game's door on a borrowed structure (its own fade, the exit first, the player at the new structure's door); the "More floors" page turned on the press; and the shared room in a Going Public session (the two-machine test above).
