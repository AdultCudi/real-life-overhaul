# Civic life

## In a sentence

A city government with a mayor, a council, elections and policies, the player standing for office, and construction sites that progress over days, seen in the street as posters, rallies and site crews.

## Where the code is

- Folder: [`Scripts/Civic`](../../Assets/Mods/RealLifeOverhaul/Scripts/Civic/). The files are listed in the code block below.
- Read first: `CivicDirector.cs` (the clock and the save), `CivicCatalogue.cs` (the levers and every number, game-free) and `CivicGovernment.cs`.

## How it talks to other features

- `Civic/CivicBridges` is filled in by `Wiring/CivicLinks.cs`: the record and reputation (`Wiring.Standing`), one street scene at a time, the weather, the paper, the living city's people, the city's economy, the player's lettings, Standing, court cases and Flare.
- The policies' effects are applied at their source: `PoliceAttention.Seen`, `TransitDirector.Headway` and `EventCalendar`; see [Every bridge, and who answers it](../reference/wiring.md#every-bridge-and-who-answers-it).

`Scripts/Civic/`: a city government with elections, and a construction programme.
Everything is data that moves once per in-game hour; the street only shows what is near the player.
Options group "Civic life": a master switch, "Elections and the mayor", "Standing for office" (needs the elections) and "Construction sites", and twenty-one test buttons (five of them construction's: [Construction](construction.md)).

```
CivicDirector     the clock, the save (two lines in modData), the toggles, faults, key prompts, the test buttons
CivicCatalogue    the thirteen levers and every number a policy changes; name pools; election and site constants (game-free)
CivicState        GovState, SiteState, WorksState and their one-line text form (game-free)
CivicGovernment   mayor, council, campaign, polls, the count, policies phased in, approval, petitions, council votes, recall
CivicEffects      what policies do: multipliers the mod owns, labelled transactions, moods; all undone by Revert
CivicBridges      CivicPolicy (what other features read) and CivicBridges (what civic life asks of features outside this folder)
CivicCity         districts (the game's neighbourhoods), electorates, polling places, the player's buildings
CivicWorks        the construction programme as data: six sites, working hours, rain, complaints, inspections, handover
SiteDresser       the three sites nearest the player as props, a closed lane, one crew; a rebuild's wrap, a plot's stages (.Rebuild, .Plot, .Wraps)
SiteWrap          a building wrapped in the game's own scaffolding, a few pieces a frame out of view, linked to its fade (WrapRules: the layout, game-free)
BuildingShell     a building's solid boxes read from its colliders, in its own axes: the walls scaffold, hoarding and wrap stand against
SiteKit           the game's scaffold pieces, concrete, metal and fence wire, found once a city behind the loading screen
PlotRules         a new building on given ground, game-free: its record, its stages, its frame; PlotMeshes, CraneSlew
RoadClosure       one lane closed for roadworks through the traffic system's switch (a small copy of SceneCordon's part)
CampaignWorld     posters, boards, the canvasser, rallies, the polling place's sign; VolunteerShift (the player canvassing)
CivicCast         civic life's own five people (a second instance of CityPeople), CivicCrew (walk in, work, walk off); CivicScreen: the game's panel and job panel for its prompts and shift figures
CivicProps        pooled props by id: the bundle's model if it has one, else a plain stand-in built to the same size
CityHallPhone     the City Hall contact (five topics), the ballot and the site notice in the game's conversation panel
OfficeRules       running for office by rule: nominations, who may stand, donors and the law, the vote model by district, doors, rallies, the debate, attacks, endorsements, the council's odds, conflicts of interest, the city's budget, approval, recall (game-free)
OfficeState       the player's run, result and office, and the city's budget, as one line (game-free)
CivicOffice       the player's run and office on the government's clock: its hooks (PlayerStands, PlayerTerm, PlayerLocal, Counted, DayBegins, OfficeSits, ToAgenda, ApprovalExtra, DepartmentLevel)
OfficePhone       City Hall's fifth topic: standing, the papers, the platform, the fund, spending, endorsements, attacks; the agenda, the budget, orders, the register
OfficeScenes      a door, the player's rally, the debate, result night and the council's meetings, in the game's list window (OfficePanel)
```

**On screen (Civic).** The mod draws nothing of its own for civic life (`CivicHud` is gone; `CivicScreen` takes its place).
The polling station, the campaign canvasser, the site notice and the canvassing shift's "offer a leaflet" are the game's panel over the player (`GameUi.DoorPanel`, one button the game labels with the player's key).

The shift's figures (leaflets taken, seconds left) are the headline and first line of a copy of the game's job panel (`ShiftCard`), or a notification each time they change if that copy cannot be made.
The council's meetings and ballots were already the game's phone dialogue (`CityHallPhone`).
Cost: nothing per frame; the prompt is offered once a second and told to the panel only when it changed.
Log lines with diagnostics on: `Civic check (prompt panel)`, `Civic check (shift card)`.

Not seen in the game.

**The night's toasts.** Every city toast (`CivicText.Toast`, and a guest's copy of the host's in `CivicText.Show`) goes through `Core/MorningNews`: while the player sleeps it is held for the morning summary. The director runs on game time, which the game's sleep pauses, so the hours a night skips (a policy ending at midnight, a site handed over in the afternoon) catch up in the minute the player wakes; held, they come with the morning line instead. Going Public's relay of the host's toasts to its guests (`CivicText.Heard`) is as before.

## What the game lets a mod change, and what is done instead

| | in the game | what a policy does |
|---|---|---|
| income tax | `gameVariables.taxPercentage`: a whole-percent int that is part of the save, read once a year when the bill is made, and reported by the options screen as a changed game rule | never touched. Each midnight `rate x yesterday's sales x 20 %` runs up on the weekly account, charged or paid back as "City business tax adjustment (policy)" |
| sales tax | none exists | "trade levy": 0.5 % of sales charged, or 0.3 % paid back, weekly |
| rent | `BuildingRegistration.RentPerDay`: fixed per lease, saved; no global multiplier; the formula's inputs also move sale prices and property tax | never touched. 6 % of the week's rents charged or returned as "City rent rules adjustment"; landlords get or lose 4 % of rent collected |
| wages | `EmployeeInstance.hourlyWage`: per employee, saved. `employeeHourlySalaryMultiplier` is saved and only affects new candidates | never touched. A floor of 16 an hour (at today's prices) is billed as the difference between it and what they are paid (the game's wage at today's prices, as the city's cost of living pays it, and any raise agreed in Company: `CivicBridges.Raise`) x rota hours; or 5 % of that wage bill is paid back |
| footfall | `baseCustomerPromotionMultiplier` is saved; `Building.trafficIndex` also feeds rent and price; `promotion` is rebuilt nightly | not changed. A site next door is a notification and a mood, not fewer customers |
| street parking | `NeighborhoodData.parkingPrice`: a field of a data asset, in memory only | scaled x1.5 / x0.6 while the policy stands, the value found put back exactly |
| parking tickets, subway fare | constants compiled into the call sites (125, 3) | each ticket or ride found in the day's transactions is topped up or part-refunded under its own label (a ride whose fare a day pass gave back is not counted, nor any while a train is under way) |
| this mod's fines | `CrimeLaw` constants | each fine found in the day's transactions is topped up 50 % or refunded 30 % ("Traffic fine adjustment"); or Law multiplies by `CivicPolicy.FineScale` and sets `CivicBridges.FinesScaledAtSource` |
| police, crime, accidents | the mod's own options (`Settings.PolicePresence`, `PatrolFrequency`, `CrimeFrequency`, `AccidentFrequency`) | scaled in memory (dithered by the hour so that 2 x 1.25 is 2.5 on average), never written to the options file, put back on revert; if the player moves the slider meanwhile, that becomes the new base |
| sweepers | `CityCatalogue` entry `sweeper`, public `Weight` | x2.5 / x0.3, put back |

Nothing the game saves is written by a policy.
`CivicEffects.Revert` (option off, city unload, fault) puts back every value it changed.

## The policy catalogue

| lever | +1 | -1 |
|---|---|---|
| Policing | officers and patrols x1.4, street crime x0.8, levy +70 a week | officers x0.6, crime x1.25, 50 a week returned |
| Traffic enforcement | fines x1.5, traffic accidents x0.7 | fines x0.7, accidents x1.3 |
| Business tax | +20 % of the income tax sales run up, only where the game charges income tax (the tax year so far, projected, at 150,000 or more: `TaxHelper.MeetsIncomeTaxThreshold`) | 20 % of it paid back, the same |
| Trade levy | 0.5 % of sales charged; 40 a week off the levy | 0.3 % of sales paid back |
| Grants | 150 a week per business (three at most) | licence fee 60 a week per business (three at most) |
| Rent | +6 % on rent paid; +4 % on rent collected | 6 % of rent paid returned; -4 % on rent collected |
| Parking | street parking x1.5, each ticket +60 | x0.6, each ticket -40 |
| Transit | subway +1 a ride; buses x1.3 as often (bus fares do not change) | subway -1; buses x0.75 as often |
| Cleaning | sweepers x2.5, mood +3, levy +30 | sweepers x0.3, mood -3, 25 returned |
| Night life | licensed until 04:00 (bridge), crime x1.1 | until midnight (bridge), crime x0.92 |
| Permits | sites x1.35 and until 19:00; complaints only noted | sites x0.75, 08:00 to 16:00; one complaint brings quiet hours |
| Events | street events x1.5 (bridge), mood +2, levy +40 | x0.5, 30 returned |
| Wages | floor of 16 an hour, against the wage at today's prices with any raise, billed as a top-up | the city pays 5 % of the wage bill |

Money by the week is one settlement every seven days, a transaction per line; the phone shows the account as it stands.
Every amount above is at the start's prices and is charged and shown at today's (`CivicCatalogue.*Now`, `Core/Prices`): the levies, grants and licence fee, the ticket and fare changes, the floor, donations, the fast-track threshold and the refit bids.
The `_fx` strings take them as {levy}, {grant}, {ticket}, {fare} and {floor} (`CivicText.Effect`).

## Elections

A mayor and a council of five.
An election every 8 weeks (56 days); the first 14 days after the mod first sees a save.
Three or four candidates drawn from pools (20 x 20 names, 8 invented parties, 18 slogans), each leading on one of six issues with a platform of three or four policies on different levers.

The campaign is the 7 days before.
Polls move a third of the way each day towards where the city stands: platform fit weighted by what matters now (crime scenes and accidents in this save, dirty streets, rent rules), and a sitting mayor carries their approval.
The player has a part too:

- donations (500 a time, 2,500 at most; if that candidate wins, a fast-tracked permit per 1,000, two at most, with one chance in four of a "pay to play" story when called in);
- one endorsement (worth more with every business they run; a scandal with a criminal record, through the Law bridge);
- an afternoon's canvassing;
- standing through a rally.

The debate is 3 days before at 19:00.
On the day the polls are open 07:00 to 20:00 at one designated building per district (a school if there is one, else a bank, an agency, a clinic, else the first building by address).

The count is at 21:00, by district, from an electorate derived from the district's floor space (the game keeps no population): each district's shares are the city's final poll leaned by how each platform fits what that district cares about against the city, and by each candidate's local appeal there (for the player: their home, businesses, doors and posters in it), kept in the save with the count (`GovState.DistrictShares`).

A sitting mayor carries the economy too (OfficeRules.EconomyTerm: the cycle of Economy/City, a boom helps them, a recession their challengers), in the polls and in their approval.
The winner takes office the next morning and their policies come in one a day at 09:00, each with a notice.

Between elections: approval follows crime, accidents, late sites and promises kept; petitions (2 % of the electorate in 14 days force a council vote; the city starts some, the player can start or sign one); a council vote on one policy about every 12 days; a recall election after 7 days under 25 %.

## Running for office (`CivicOffice`, `OfficeRules`, `OfficeState`)

The player can stand for the council seat of the district their home is in, or for mayor.
Nominations open 14 days before election day and close at the end of the campaign's second day (papers filed in the campaign join it at once).

To stand: a home in the city, no conviction that still counts, no case before the court, a platform of one to four policies (one a lever), and either signatures (council 60, mayor 300: the businesses' sheets once, 8 a business, ten at most, half the player's recognition, and doors) or the deposit (council 1,000, mayor 5,000; back with a tenth of the vote).

A party (each stands for issue `party % 6`) gives a grant (1,500 / 4,000) and wants a plank on its issue; an independent gets the outsider's lift while the mayor's approval is under 40.
Not offered in a Going Public session.

The mayor's race is the city's own: the player's candidate takes a slot of `GovState.Candidates` (their party's candidate stands down, else a free slot, else the last challenger's; a mayor standing again takes slot 0) and is `Candidate.Player` (named by the game; `GovState.PlayerSlot`, `MayorIsPlayer`).

Their score adds `PlayerTerm`: Standing (recognition, public opinion, integrity: 0.4 at most either way), what the fund has spent against the others (diminishing, 0.3 at most), a party's machine (0.08), the outsider's lift, the campaign's own support and the district's (doors and posters), and the economy for a challenger.

The council race is the district's alone: two rivals from the city's pools (one of the mayor's party holds the seat unless the player does), the same polls a third of the way a day, counted at 21:00 with the mayor's, and the seat is `Council[4]`.

The fund: the player's own money (500 a step), the party's grant, donors (an offer a day at most, likelier the better known and placed; at most 500 or 1,000 from one donor).

Over the limit, cash from nobody named and straw donors are against the law: taken, the money can come out on any day for 28 days after it was taken or after the election (2 % a day, up to 10 more by how much, up to 2 more for somebody well known, 12 % at most), when it is forfeited, a summons for breach of campaign finance law is filed (`TaxCases.FileCampaignFinance`), support or approval falls and the City Ledger prints it.

Spending: posters (300 for 12, put up by the player on a shop wall: the five rays of the campaign's own posters, from where they stand), a City Ledger or a social media advert (800 / 500, less each time), a rally (600: `CampaignWorld.OwnRally`, a box and a crowd and no speaker; the speech is the player's, `OfficeScenes.Rally`), canvassers (400: 30 doors in the district).

Doors: between 10:00 and 20:00 at the street door of a home that is not the player's (`CivicDirector.OfficePrompt`), once a building a day; the person's worry is drawn from the district's issues.
Endorsements: seven bodies, each for an issue (the City Ledger's editorial for the platform and integrity); asked once (odds by fit and influence), or deciding for whoever fits best four days out.

Attacks: about one day in three (one in two in the last three days), on what is true of the player if anything is (a record, money taken, meetings or the debate missed, integrity under 45, promises not kept, never having won), else on something thin; answered within a day or it stands.
The debate is the player's scene at 19:00 three days before; an empty chair costs 0.05.

Result night is the count's pages, district by district, and a speech.

In office: a councillor is paid 600 a week, a mayor 1,800.
The council meets every 7 days at 18:00 (not on election day) while the player sits (`OfficeSits`: the government's own council votes and petitions go to the agenda instead).

A councillor votes on each motion and may table one; a mayor sets the agenda (two of their own, petitions, the budget), speaks for or against each (0.15 on their own party's members) and orders the levers that are how the city is kept (cleaning, events, permits, transit: once a fortnight, without the council).
The city's budget: four departments at -2 to 2 steps, 12 % of a department's share a step.

Police and streets take 8 % a step off the crime and accidents the city counts, every department a point of approval a step, by how much its issue matters.
A deficit runs the reserve down and costs up to ten points (with the option off the budget is kept as data and does nothing).

A vote on a lever that touches the player's businesses or property is a conflict of interest: declared, the player stands aside.

Not declared, it can come out within 21 days (4 % a day, 7 % if the vote decided it, more for somebody well known, twice as likely while no register of interests is filed; the register is due in the first 7 days): a scandal, and a summons for misconduct in public office if it decided the motion (`TaxCases.FileMisconduct`).

A councillor's approval is their own (`OfficeRules.CouncillorTarget`); under 25 for 7 days brings a recall vote in 7 days, kept by approval and the campaign against it.
A mayor's is the city's.
Standing again needs neither signatures nor deposit.

The player's moments are a draft in Flare (`FlareSource.Campaign`; a post of it adds a little support) and pieces in the next morning's City Ledger (`OfficeState.Stories`, through `LedgerBridges.Extra`: filing, the race, attacks, endorsements, adverts, the debate, the result, scandals, recalls, orders, the budget).
Standing hears of it (`FameCause.Office`: integrity and the papers' notice).

The Planner has the last day to file, the debate, result night, an attack to answer, the meetings, the register, the budget and a recall vote.

While the option is off nothing new begins and nothing is offered; papers already filed stay on the ballot and are counted, and an office held lasts to the next count with the council voting by itself.

## Construction sites

Six sites in progress at once, two started within 60 to 320 m of wherever the player was that day.
Working hours needed: roadworks 50, scaffolding 120, refit 90, utility 30, rebuild 160, a new plot 320 (x0.8 to 1.3); ten a weekday, 07:00 to 17:00, as the city came.
A rebuild wraps a whole building in the game's own scaffolding to its roof (one at a time in the city); a plot is a new building on ground given to it, started only by whoever has the ground (`CivicWorks.StartPlot`). Both are described in [Construction](construction.md).
Five stages each.
Rain stops outdoor work for the hour.

Thirty in a hundred are left unsafe for a stretch (missing cones or netting, a barrier flat in the road): reported, they stop for a day.
The game's city has no empty lots: a new building's stages stand on ground given to them (the mod's own district, once the owner has chosen where it is), and the test button "raise a frame storey here" gives the ground in front of the player.

Placement and budgets are at the top of `SiteDresser`.
A scaffold's or hoarding's wall is the building's own box nearest its door (`BuildingShell`), never looked for between the pavement and the door: a door marker can stand on the walkable mesh (36 5th Avenue's is 0.30 m out), which left that line no length.
Nothing is cut out of the navigation mesh.
A lane is closed only by `RoadClosure`, only for a stretch with no junction, and only if every way into it can be shut where traffic has another way to go; it is opened on every path out.

## For other features

```csharp
// read at any time (1, or the stock value, when civic life is off):
CivicPolicy.FineScale  CivicPolicy.EnforcementScale  CivicPolicy.PoliceScale  CivicPolicy.CrimeScale
CivicPolicy.AccidentScale  CivicPolicy.ServiceScale  CivicPolicy.EventScale
CivicPolicy.ClosingHour  CivicPolicy.SiteSpeed  CivicPolicy.Position(Lever.X)  CivicPolicy.Changed += ...

// set once at start-up by the feature that has the answer:
CivicBridges.CriminalRecord = () => ...;      // Law: an endorsement becomes a scandal
CivicBridges.Reputation = () => ...;          // Law: -100 to 100, weighs an endorsement
CivicBridges.LargeScene = () => ...;          // Events: no rally or site crew beside a street event
CivicBridges.WeatherStopsWork = () => ...;    // Events: storms as well as the game's rain
CivicBridges.News = (headlineKey, bodyKey) => ...;   // Social: stories (result, recall, scandal, pay to play)
CivicBridges.Labouring = site => ...;         // Jobs: starts a labouring shift at a place; offered on a site's page only when set
CivicBridges.CityPeopleOut = () => ...;       // City life: civic life makes no more people while seven of the eight are out
CivicBridges.FinesScaledAtSource = true;      // Law, if it multiplies its own fines by CivicPolicy.FineScale
CivicBridges.FaresScaledAtSource = true;      // Transport, likewise for fares
CivicBridges.NoteCrime();  CivicBridges.NoteAccident();   // anything that happens out of the player's sight
CivicBridges.Economy, Influence, Recognition, Integrity, Convicted, CaseOpen, Landlord   // running for office: what it reads
CivicBridges.StandingMoved, FileCase, CampaignPost                                     // ...and what it tells
```

## Wish-list (assets)

Each is drawn today as a plain stand-in of the same size and pivot, built in `CivicProps.CivicStandIns`.
A model added to the prop bundle under the same id is used with no code change.

| id | size (m) and pivot | note |
|---|---|---|
| `campaign_poster_0` .. `_3` | 0.60 x 0.85, 0.012 thick; pivot the centre of its back; face +Z | four colourways (amber, teal, plum, olive): a portrait block, a name band, a slogan band |
| `campaign_board_0` .. `_3` | as `sandwich_board` (0.60 x 0.93 x 0.46) | the same four colourways |
| `polling_sign` | as `sandwich_board` | white, "POLLING PLACE" and an arrow |
| `soapbox` | 0.60 x 0.30 x 0.45; ground centre | a crate to stand on; the top is 0.30 up |
| `scaffold_bay` | 2.40 (X) x 2.40 x 1.80 (Z); pivot the ground at the middle of the wall side, standing out towards +Z | first lift: four standards (outer row 1.74 from the pivot), ledgers, boarded deck with its top 2.40 up; open underneath |
| `scaffold_bay_upper` | 2.40 x 2.00 x 1.80; same pivot | a lift above: toe board and guard rail on the outer face |
| `scaffold_net` | 2.38 x 1.96 at z 1.80; same pivot | debris netting for one upper bay |
| `scaffold_fan` | 2.40 wide, 0.90 out from z 1.75 at 2.6 up, sloping up 28 degrees; same pivot | the protection fan over the walkway |
| `skip_bin` | 3.40 (X) x 1.25 x 1.70; ground centre | builder's skip |
| `site_hoarding`, `site_hoarding_notice` | 2.40 x 2.40 x 0.10; ground centre; public face +Z | painted timber panel; the second carries an "opening soon" board 1.2 x 0.8 centred 1.5 up |
| `site_notice_board` | 0.90 x 1.80 x 0.45; ground centre; read from +Z | board 0.9 x 0.65 centred 1.45 up, on two posts |
| `road_sign_works` | 0.75 x 1.55 x 0.60; ground centre; face +Z | "road work ahead" on an A-frame |
| `trench_cover`, `asphalt_patch` | 1.00 / 1.20 x 0.02 x 2.00, lying along Z | broken surface with an open trench; fresh surfacing. Tiled four in a row |
| `road_marking` | 0.10 x 0.004 x 2.00 along Z | sprayed setting-out line |
| `spoil_heap` | 0.9 x 0.35 x 0.9; ground centre | |
| `works_tent` | 1.5 x 1.9 x 1.5; ground centre; open at +Z | frame shelter over an open cover |
| `manhole_open`, `manhole_lid` | 0.9 x 0.06 x 0.9; 0.66 x 0.05 x 0.66; ground centre | |
| clips | `speech_gesture` (a speaker to a crowd, standing), `hammer_nail` and `carry_plank_hold` (scaffolders), `rake_asphalt` | `speech_gesture` is **delivered and played** by the rally's speaker once somebody is listening (`CampaignWorld.SpeechClip`; `talk_a` without it), on `soapbox`: the stage is the one the clip is made for (feet within 0.36 x 0.26 m on a box 0.30 m high, listeners 2.3 m ahead and 1.15 m apart). The others today: `kneel_work`, `shovel_dig`. `podium_speak` (delivered) is not wired: it rests its hands on a lectern 1.10 m high that nobody has made |
| vehicle | a works van or flatbed that can be parked at the kerb | none is placed: nothing in the traffic or vehicle code is touched |

`tower_crane` is in the second props bundle split at its slewing ring (`tower_crane_mast`, `tower_crane_jib`). The frame storeys and cladding are boxes in the game's own concrete and metal (`PlotRules`, `PlotMeshes`), not props. The building sites' props are listed in [Construction](construction.md).

## Civic life in the street

- Civic life (`Scripts/Civic`) has a cast of five of its own (`CivicCast`: a second instance of `CityPeople`, made a person at a time when first needed).
  At most one construction crew of three and one canvasser are out together, or one rally of four; they walk in from out of view and walk off, like city life's people.
  It cuts nothing out of the navigation mesh.
