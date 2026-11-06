# Map places

How a feature puts its places on the game's city map: a row in the map's filter list, a marker with a name for every place, and "set destination". The code is `Scripts/Core/MapPlaces.cs` and `Scripts/World/MapLinks.cs`.

## The rule

**Every place a feature adds to the city must be registered here**, and nothing else in the mod touches the map's markers or filters.
One statement makes a kind of place, one more gives it its places:

```csharp
MapPlaces.Category stations = MapPlaces.Register("police", "reallife_law_map_stations", blue, MapIcon.Shield, onByDefault: true);
stations.Set(places);   // IReadOnlyList<MapPlace>; or Add / Remove / Clear / Changed(place) whenever they change
```

A `MapPlace` is a world position and a name, and optionally the `CityBuildingController` it is (and two locale keys for that building's card).
`Register` is called once per city load from the feature's own start-up; the same id gives the same category back.

Optional: `custom:` a sprite of the feature's own (with `MapIcon.Custom`), `inStreet: true` to show the markers in the street within 100 m as well (as the game does for the player's own buildings; police stations use it), and `category.Available = () => Settings.X` to hide the row and the markers while the feature's option is off (call `MapPlaces.Refresh()` when that option changes).

Built-in glyphs (`MapIcon`, drawn in code, 64 px, white on nothing: the marker supplies the colour): `Shield`, `Target`, `Bus`, `Bed`, `Scales`, `Wheel`, `Bicycle`, `Wrench`, `Briefcase`, `Ballot`, `Cone`, `Cross`, `Star`, `Flag`.

One option is over all of it: "The mod's places on the map" (`Settings.MapPlacesShown`, with the camera options; off hides every row and marker, Law's police stations included, which also keep their own option).

**The other features' places: `World/MapLinks.cs`.** Apart from Law (which registers its precincts itself) no feature calls `MapPlaces`: one component, added last at city load, reads each feature's places through members the feature already has (or a read-only accessor) and registers them.
No feature knows of it, so one whose places cannot be read is left off the map with one log line ("Map links: the places of '...' could not be read") and nothing else changes.

A new kind of place is one `Link` in `MapLinks.Declare`: an id, a title key, a colour, a glyph, `Enabled` (the feature's options), `Stamp` (a few integer operations that change when the places do: a count, ids, a position) and `Fill` (the places, read only when the stamp has changed).

| row (id) | places | glyph | ticked by default | in the street | changes |
|---|---|---|---|---|---|
| Bus stops (`bus_stops`) | every stop of the network (`TransitMap.Stops`), "Bus stop M1 to (end): (address)" | Bus | only with 40 stops or fewer (usually over a hundred: unticked) | no | once, when the network is laid out |
| Hotels (`hotels`) | `Hotels.Sites`; a hotel that is a building has the game's card under its name | Bed | yes | yes | once |
| Courthouse (`courthouse`) | `Courthouse.Front`, if hearings can be attended there | Scales | yes | yes | once |
| Driving schools (`driving_schools`) | the learner-plate boards (`LicenceDesks`) | Wheel | yes | yes | as they are found |
| Bike hire and two-wheeler shops (`two_wheelers`) | docks ("Bike hire"), racks ("Bicycle shop"), stands ("Motorbike dealer") | Bicycle | yes | no | once |
| Custom shops (`custom_shops`) | one per repair station (bays within 45 m of one another are one station), at its first bay | Wrench | yes | no | once |
| Help wanted and employers (`help_wanted`) | the help-wanted boards; one that is an employer's office is named after the employer | Briefcase | yes | no | as boards are found, and when the employers are matched to them |
| Polling places (`polling_places`) | one per district, from the start of a campaign until the count | Ballot | yes | no | with the campaign |
| Construction sites (`construction`) | the sites not yet finished, "(project): (address)" | Cone | yes | no | with the programme |
| Sights and fishing spots (`sights`) | the landmarks: "(seen)" once ticked off, "(fishing nearby)" at the waterside ones | Star | yes | no | when one is seen, or either option changes |
| What's on (`whats_on`) | the street event that is on (never a fire), a rally while it is held, the chess player's table while it is out, the place of a plan until the outing begins (the game's guider marks it from then) | Flag | yes | no | as they come and go |

A row is made the first time its kind has a place, and is offered only while it has one and the feature's option is on (so "Polling places" and "What's on" are in the list only while there is something to show; with the option off the markers are removed, not hidden).
It looks once every three seconds of real time; markers are made six at a time a fifth of a second apart.

Grouped to keep the list to a dozen rows with Law's: docks, racks and stands in one row; boards and employers in one (they are the same places); sights and fishing spots in one (the fishing spots are the waterside landmarks); the four passing things in "What's on".

Not registered, because they are the game's own buildings with the game's own map filters: law firms, cinemas, theatres, nightclubs, clothing stores, travel agencies, filling stations and repair garages as such.
Not registered because the game's own destination guider already marks them when they matter: Family's appointments and wedding venue.
A stop that is dressed and a hotel no longer add a marker of their own (`BusStopDressing`, `Hotels` did until 0.39.0).

Unverified (the game could not be run): everything in the table as seen on the map; that a hundred-odd unticked markers cost nothing while the map is shut (they are inactive objects); how the list reads with twelve rows.

What the game's map allowed, and what a category therefore gets:

- **A real filter row.** The map's filter list is not data: it is built in code at start-up from one template row (`CityMapFilters.InitializeFilters` -> private `CreateFilter(name, icon, data, category, ...)`).
  That method is called by reflection, so the row is the game's own: icon, label (the title key), tick box, found by the search box, switched by its category's switch and by "toggle all", and its arrow button flies the map to the category's place nearest the player.

  Rows go under the game's "Special buildings" (with the subway stations, the bank, the hospital), moved up from the end of the list to the end of that category; if that category cannot be found they get a category line of their own (`reallife_map_category`).

- **Nothing in the save.** A game row writes its name into the save's `SelectedCitymapFilters` when ticked.
  The mod's rows are taken out of the game's table of rows and set up again with the game's own "not in the selection" flag, so the save never sees them; whether a row is ticked is kept in PlayerPrefs (`rlo_map_<id>`), per machine, and put back each time the map opens.
- **Markers** are the game's own (`CityMap.AddPoi`), shown on the open map while the row is ticked.
  The game's filters only ever switch *buildings* on and off (by type, business or address), so the markers are switched by the facility itself, on the map's own events (opened, closed, a row ticked): nothing per frame.
- **Buildings lit.** A place that is a building (`MapPlace.Building`), or stands at one that keeps its own card (`MapPlace.Lit`: a sign on a school's wall, a help-wanted board, a polling place, a construction site), has the building lit on the open map while its row is ticked.

  It is lit exactly as the game's own filters light theirs (`CityMapFilters.ApplyFilters`): `SetHighlight` in the row's colour and the building's own marker with the row's icon, which stands for the place there instead of the mod's marker (in the street the mod's marker stays).

  The game looks at every building again whenever a filter changes, a few a frame from `GameManager.Update`, and puts out every light that is not its own; a lit building of ours that it put out has its marker switched off, so the driver's `LateUpdate` lights it again in the same frame, before it is drawn.

  A building whose marker the game always shows (the player's own) is lit again when each look is over (`CityMapFilters.Work.HasPendingWork` falls).
  Nothing is lit where the game puts out its own lights: its choice of a subway station, the phone's full menu over the map, a picture of a building being taken (`buildingOutdoorsCameraCaptureController` active), nor while something is picked on the map (`MapPlaces.Quiet`).

  The cost: a look at some forty buildings a frame while the map is open.
  Not lit: the custom shop's repair stations (the game's filling stations are not buildings it can light), bus stops, bike places, sights and what is on (no building).
  The courthouse's card names it with its hours.

- **Names.** A game marker has a name plate that the game shows only for directions (destination, delivery).
  It is switched on for the mod's places (its "dismiss" button hidden), so every marker carries its name.
- **Set destination.** The game offers "set destination" only on a *building's* card.
  A place that is a building gets exactly that, with the place's name on the card instead of "for rent" (the card is rewritten the moment the game writes it: one reference comparison per frame, and only while some place is a building).

  Every marker, building or not, can also be clicked on the open map: the place becomes the destination (the game's own destination marker, with name and distance) and the map closes.

- Works however the map is opened (phone or key: both end in `CityMap.Toggle`, which raises the event used).
  Everything is removed when the city unloads.
  If the game's code is not as expected (a private member renamed) it is logged once and that part is left out: no row means the markers are always on the map; no name plate means markers without names.

Unverified (the game could not be run): how the name plate looks beside a marker; that a click on a marker's blob reaches the button added to it rather than the building under it; the row's place in the list.

## Rows added since the table above was written

`World/MapLinks.cs` has these rows as well. Each is ticked by default. With them and Law's police stations the list has nineteen rows of the mod's, and the district view adds its ten layers through `MapPlaces.RegisterChoice` ([Districts](../features/districts.md)). The glyphs `Paper`, `Tower`, `Cloud`, `Coin`, `House`, `Heart` and `Mortarboard` were added to `MapIcon` for these rows and for the notifications.

| row (id) | places | glyph | in the street |
|---|---|---|---|
| City Utilities (`utilities_office`) | the office where power and water are applied for in person | Flag | yes |
| Hudson City University (`university`) | the university (`CareersDirector.University`), in a row of its own | Mortarboard | yes |
| Places to study (`study`) | the game's school and the hospitals' wards | Star | yes |
| Tech companies (`tech_hq`) | the city's tech companies' headquarters (`Tech/Sites`) | Tower | yes |
| Your cloud (`tech_cloud`) | the player's data centres: the cloud's regions | Cloud | no |
| Employers' offices (`job_offices`) | each employer's office (its help-wanted board) by the employer's name, where reviews are held and shifts clocked on to | Briefcase | no |
| Newspaper kiosks (`newspaper_kiosks`) | the City Ledger's kiosks near the home (`LedgerDirector`) | Paper | yes |
| City Offices (`floors`) | a building taken by the floors core for places that found no spare floor ([Floors](../features/floors.md)), registered by `Floors/FloorsDirector.Book`, there only while one is taken | Briefcase | yes |

Every building the mod claims as its own (`Core/SpecialBuildings`: the university, City Utilities, the courthouse, the tech companies' towers, the hotels, the police stations, a building taken for places on floors) has a row of its own, as the game's special buildings have theirs. NavCheck's `MapRowCheck.cs` checks that every claimed building has its row.

## The game's own letting leaves them alone

The mod refuses its buildings to the player in the interface only (`Core/ClaimedOffers`), and it takes empty units, so the game's own night of letting (`CompetitionHelper.RunDaily`: a new business in an empty unit, the residential and warehouse swaps) used to fill them, and a police station or a tech tower then moved.
Every address the mod holds or reserves is held by `Core/HeldUnits`, counted by claim (`HeldLedger`, game-free in `HeldUnitsRules.cs`):

- **Where the claims come from.** The book of special buildings (`Core/SpecialBuildings`) tells it of every change at once: each `Set`, `SetNamed`, `Remove`, `Reserve`, `Unreserve`, `Clear` and an option switched (`LookShown`) calls `HeldUnits.BookChanged`, which reads what the book holds (`SpecialNameBook.Holding`: every entry whose option is on, every reservation that is on). So a feature that claims its building the usual way is covered without knowing, a feature added later included, and a claim made while the city loads is taken in once the city's `HeldUnits` starts (a second at most, before any night). The empty shops made offices hold theirs with `HeldUnits.Hold` and `Release`.
- **Out of the night's letting.** The game takes its units from `BuildingHelper.AllNeighbourhoodBuildings`, which nothing else of the game reads. At the night's first hour event (before the real estate and the letting) each held unit that stands empty, is no player's and is not a special service is taken out of its district's list, and at the day event (after the letting) every list is put back whole, in the game's order (`HeldUnitsGame.Out`, `Back`; at the next second if a step of the night threw). A held unit with a business in it stays in, so the game may still close that business.
- **The ignore list.** `RivalsHelper.IgnoredAddresses` keeps the game's new businesses and rivals off an address too, but the rivals' step (`RivalsHelper.RefreshRivals`, every game hour) blanks a rival's landlord and business at every address on it, and the save would keep that. Nearly every building has a rival landlord (`CityGenerator.DistributeBuildingsToRivals` gives one to every building at a new game; only a building for sale has none), so a held address goes on the list only while it has neither and is not for sale, and never on a Going Public guest (`HeldUnitsRules.MayList`). Only the mod's own entry is ever taken off (`HeldUnitsRules.IgnoreChange`); the game's own (23 Third Street) and another mod's stay.
- **Not covered.** A special rival's defence (`RivalDefenseHelper.ActivateLowDemand`) opens its businesses in units to let read straight from the save's registrations, ignore list or not; only a Harmony patch could stop it. Nothing of the above is saved or sent.

With Developer diagnostics on, a "Held buildings check" line after a load and after each change counts the held addresses by feature and runs the game's own choice of empty units (`CompetitionHelper.GetEmptyBuildings`) as at night: PASS when it would offer none of them. Options > Tests > "Run the game's choice of empty units to let against the mod's buildings" does the same at a press.
