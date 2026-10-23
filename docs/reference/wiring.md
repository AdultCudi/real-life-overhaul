# Wiring: how the features meet

`Scripts/Wiring` fills in the bridges between features. This page says how it works and lists every file in it, then follows an arrest through four features, explains one street scene at a time, and lists every bridge with who answers it.

## How the wiring works

Every feature works alone and talks to the others only through optional bridges with a default.
`Scripts/Wiring` is the one folder that fills those bridges in: a `partial static class Wiring` with one nested class per pairing, one small file each.
`Wiring.Connect(logger, root)` runs once per city load after every director has been added (`RealLifeOverhaulCity.OnLoadAsync`), `Wiring.Disconnect()` on unload puts every bridge back to its default.

Nothing in the folder has an `Update`: an adapter runs when the feature that owns the bridge asks.
An adapter never throws into its caller: a fault is logged once per place (`Wiring.Fault`) and the default answer is given.

## Every file in `Scripts/Wiring`

| file | what it connects |
|---|---|
| `Wiring.cs` | `Connect` / `Disconnect`, the list of pairings, `Fault` |
| `Standing.cs` | `Wiring.Standing`: the one answer to "has the player a record" (`HasRecord`, `Arrested(days)`, `Convictions`, `Charged`) and to what the city thinks of them (`Public`, `PublicSigned`), from Law's police record and Justice's convictions together, and how well the city knows them and what it thinks of them as an owner (Fame) |
| `LawJustice.cs` | `Wiring.Arrests`: Law's cell and Justice's case for the same arrest; `Justice.Bridges.Law` |
| `LicenceLinks.cs` | `Licences.Law`, `Justice.Bridges.Licences`, and what Licences reports to the courts instead of fining |
| `Scenes.cs` | `Wiring.Scenes`: the one question every street scene asks before it starts; `PlayerHeld` for panels |
| `CivicLinks.cs` | `CivicBridges` (record, reputation, scenes, weather, news, city people; for running for office the economy's cycle, Standing's influence, recognition and integrity, a conviction or a case, a let building, Standing's notice, a summons); `Wiring.Paper.Print`: a finished story for the City Ledger |
| `LifeLinks.cs` | `SocialLinks`, `CareerLinks`, `TravelBridges`, `Justice.Bridges.Social`, `GarageBridges.RoadIsWet`, `LifeAccounts.Home` |
| `Wiring.DrivingLinks.cs` | `DrivingBridges`: the custom shop's parts lifted off the driven car and laid back on top of its handling |
| `EconomyLinks.cs` | `Economy.EconomyBridges` (the record the credit rating reads, the fines and court debts the Finance app lists), the Finance rows of the Planner, and the banks' own terms put back on unload |
| `FamilyLinks.cs` | `FamilyBridges.Social`, `Home`, `Law` and `SocialLinks.PartnerChanged` |
| `RoommateLinks.cs` | `RoommateBridges`: whether the partner lives in, the friends who could move in |
| `DebtLinks.cs` | the credit desk and the private lender: a lender's claim as one of Justice's civil suits, whether the player is held, a free stretch of kerb |
| `CityLinks.cs` | `CityBridges`: the season and weather, business pressure's footfall, copycat rivals and morning roll |
| `CompanyLinks.cs` | `CompanyBridges`: business pressure's footfall, closures, disputes, rivals, resignations (once, `Core/StaffLeave`), audits and inspections; a tax case with Justice; one rival's offer to an employee at a time: `CompanyBridges.Held` (the tech sector's offers and notices) and `BusinessDirector.Held` (the company's and the tech sector's, for business pressure's poaching) |
| `BrandLinks.cs` | `BrandsBridges`: business pressure's footfall |
| `EstateLinks.cs` | `EstateBridges`: the council's rent policy |
| `OnlineLinks.cs` | `OnlineBridges`: the company's campaigns |
| `ServicesLinks.cs` | `ServicesBridges`: what buying every investor out of a business costs |
| `VenturesLinks.cs` | `VenturesBridges`: the city's economy, the employers on the CV, whether the player is held |
| `MarketLinks.cs` | `MarketBridges`: the city on a market day, events that move prices, the game's rivals, the banks, the Planner's rows |
| `ListingLinks.cs` | the player's listed company: their books, their businesses, a sale's hand-over |
| `OwnBankLinks.cs` | the player's own bank: Standing's integrity, the council's levers, the courts, the market's quotes |
| `UtilityLinks.cs` | `UtilitiesBridges`: the credit grade, the household, closures, the home's taps and screens, the Finance app |
| `CareersLinks.cs` | `CareersBridges`: home internet, a driving refresher, Standing, the economy's vacancies, the record, the student loan, the council's grants, restraint claims, the tech labour market |
| `TechLinks.cs` | `TechBridges`: Standing as an employer, the game's rivals, the company's own offers and notices (`TechBridges.Held`); the tech sector's own for the others (`TechLinks.Holds`) |
| `EndScaleLinks.cs` | the end game at scale's two tests, reaching the ventures it scales |
| `DistrictLinks.cs` | `DistrictBridges`: one reading per district from the economy, Standing, Utilities and Civic |
| `InteriorLinks.cs` | interiors and decor: footfall, cleaning, the district's income, and every reader of its scores |
| `StandingLinks.cs` | `FameBridges`: Flare's followers and what else Standing reads |
| `RivalLinks.cs` | `RivalBridges`: every move of the rival tycoons, through the feature that owns what it touches |
| `FaultLinks.cs` | `FaultBridges`: Flare's City Office and the crews, crime and litter in the street |
| `PermadeathLinks.cs` | `PermadeathBridges`: the life an obituary tells, the businesses, the City Ledger's page |
| `LedgerLinks.cs` | `LedgerBridges`: what the City Ledger prints, read once a morning |
| `FlareLinks.cs`, `FlareLiveLinks.cs`, `FlareInviteLinks.cs` | `FlareBridges`: the social life's people, the city's facts, invitations that book outings |
| `PhotosLinks.cs` | `PhotosBridges`: a photo shared to Flare |
| `NavLinks.cs` | `NavBridges.HudHeld`, `GuideToStation` |
| `TaxiLinks.cs` | `TaxiBridges` and the minimap's mark for the called taxi |
| `OfficeLinks.cs` | empty shops made offices, once the other features' buildings are chosen |
| `StreetLinks.cs`, `StreetTexts.cs` | street talk with the rest of the mod, and its words from RLO Texts |
| `TextsLinks.cs`, `TextsPhone.cs`, `Wiring.TextsCalls.cs` | RLO Texts with Flare, the phone's texts and the answers to what the player types on a call; a text worded by RLO Texts keeps its written line's key beside the words (`t_key`), so a call back knows what it was about |
| `Wiring.TextsMemory.cs`, `Wiring.TextsMemoryFacts.cs` | RLO Texts data v6: what people remember of the player, from street talk, texts, calls, Flare messages, the social life's own memories and what a person last texted about, read from their thread in the game's phone (the game-free part: `TextsMemoryFacts`) |
| `Wiring.CompanionLinks.cs` | `CompanionBridges`: the companion report's capabilities (settings, businesses, ordinary hourly pay, credit accounts, the statements' source), each read from the feature that owns it (contracts/companion/v1) |
| `Wiring.TextsCity.cs` | RLO Texts: the city's new talk with the features it reads (the staff's files, family life, a graduation, the court, the stock market, the books, the social life's people and texts); the people's last text is the newest in their thread, from any feature |

## Who does what at an arrest

One path, each step done by one feature, nothing charged or served twice:

| step | who | what |
|---|---|---|
| 1. caught | `Crime/PoliceAttention` (`Bust`, `Ticket`), `Traffic/PolicePursuit` (`Catch`, `Settle`), `Crime/CrimeDirector` (wasted 911 calls), `Licences/Driving` (a stop without a licence) | detects it and charges the **roadside fines once** (traffic fines, damages, the fines for people hurt, the licence fines) |
| 2. held | `Law.Arrests.*` -> `Law.Custody` | cuffed where they stand, the cell at the nearest precinct (never more than 48 h), the line on the **police record**, both reputations. With "Custody" off: the old 2 to 5 h roadside time skip (`PoliceAttention.BeginCustody`) |
| 3. charged | `Justice.Arrest` / `Justice.Summon` | the **case**: charges, evidence, court date, verdict, sentence. The roadside fine is credited (`PaidAtScene`); the hours in the cell are **time served** (`HeldHours`) and come off any custody the court gives, shown on the sentence page |
| 4. told | `Wiring.Arrests` (listens to `Law.Record.Changed`, nothing polled) | cell door shut: the case opened that day gets the precinct's real hours, the paper and the player's friends hear of it. Cell door open: the hours really served replace them; **bail paid at the precinct is the case's bail** (returned on appearing, forfeit on a warrant) and the court asks for no second one; sat it out: straight to a court date. Only a case in which bail would be refused still goes before the magistrate (remand) |
| 5. licence | `Licences` from the same police events (`PoliceAttention.Stopped`, `PolicePursuit.CaughtAfterFleeing`, `PeopleCharged`) | points, and the suspension at twelve. A court's suspension goes the other way through `Justice.Bridges.Licences` into the same date, so "suspended" has one answer (`Licences.Driving.Suspended` = `Justice.DrivingSuspended`) |
| 6. the record | `Wiring.Standing` | everybody who asks "a record?" (city hall, employers, friends, a licensing desk) asks here: Law's police record and Justice's convictions, read together |

What was doubled before the wiring, and how each is now single:

- **Custody twice** (the precinct's hours, then the court's custody for the same thing): the cell is time served (`CourtHearing.Sentence`, one line on the sentence page).
- **Bail twice** (the precinct's, then the court's): the precinct's is the case's (`Justice.ReleasedByPolice`).
- **The wrong hours** told to the court (`Bust` passed the old roadside custody's 2 to 5 h even when a precinct held the player for 4 to 48; `Catch` passed 0): `Bust` passes 0 when a precinct will hold them, and `Wiring.Arrests` adds the precinct's own figure.
- **Driving while suspended fined twice** (1 500 at the window by Licences, and the same charge in court by `Justice.PoliceContact`): what was paid at the window is credited to that case (`LicenceLinks.SuspendedAtStop`).
- **An arrest by Licences** (third time without a licence, second time suspended): served through the courts' custody when they are on (`Justice.Hold`: in the nearest precinct's cell like every hold of the courts', counted as time served), through the old roadside time skip when they are off.
  Never both.
- A death on the way to a ticket (`PoliceAttention.Ticket`) arrested but never charged: it now opens the same case a death after a pursuit does.
- With a precinct holding the player, nobody told the paper or the player's friends (`News`, `SocialHooks.PlayerArrested` were only in the old roadside custody): `Wiring.Arrests` tells them.

Panels queue, they never stack: the precinct's cell runs behind the game's time skip, and everything else waits for `LifeEvents.IsSafeForEvent` (no dialog, no time skip, not paused: the courts' panel pauses the game, so nothing starts behind it).
The courts' own panel also waits for `Wiring.Scenes.PlayerHeld` (a test paper or driven test, a panel of the mod's own that needs the cursor).

**The cell is Law's for the courts too.** `Wiring.Arrests.LawBridge.Custody` hands a court's hours (remand, a custody sentence, an arrest by Licences) to `Law.Custody.Hold(hours, reasonKey, 0, released)`: no bail, because a court that holds somebody has refused it or the player chose to stay.
`Law.Bridges.CourtOn` is `Justice.Active`, so a hold ends when the courts are switched off.
`Holding` answers from `Custody.Active` and the saved stay.

**For Scripts/Law (two calls it would need for more):** `Record.Convict(day, name, caseNumber)` and `Record.Remove(caseNumber)` so that a conviction and an acquittal can be lines of the police record (today `Convicted` / `Cleared` / `RecordSealed` do nothing there: a second line for the conviction would move the reputation twice for one offence, and there is no way to take one line off).
The custody path does not need them.

## One street scene at a time (`Wiring.Scenes`)

Every large scene is staged around the player, so two of them would always stand beside each other.
Each director keeps its own flag; `Wiring.Scenes` puts them side by side and every start test asks the one question:

| scene (`Wiring.Scene`) | its flag | its start test asks |
|---|---|---|
| `Accident` | `TrafficAccidents.IsActive` | `!Scenes.Near(player, Scenes.Beside, Scene.Accident)` (also: no building site within 90 m) |
| `Crime` | `CrimeDirector.IsActive` | `!Scenes.AnyBut(Scene.Crime)` in `CrimeDirector.CanStage` |
| `Event` (fire, street event) | `Events.EventDirector.IsActive` | `!Scenes.AnyBut(Scene.Event)` in `EventDirector.CanStage` |
| `Rally` | `CivicDirector.RallyOn` | `CivicBridges.LargeScene = () => Scenes.AnyBut(Scene.Rally)`: rallies and site crews (`CampaignWorld`, `SiteDresser`) |
| a pedestrian hit by traffic | part of `Accident` once it has happened | `!Scenes.AnyLargeSceneActive` in `PedestrianIncidents` |
| the routine roadside check | `PoliceAttention.IsBusy` | `!Scenes.AnyLargeSceneActive` in `DrivingEnforcement.Wanted` |

A building site stands for days where it stands, so it is asked about by place (`CivicDirector.SiteNear`).
A scene added later is a value in `Wiring.Scene`, a line in `Scenes.Active`, and `AnyBut(its own value)` in its start test.
`Scenes.PlayerHeld` is the same idea for panels (see above).

**Road closures have one owner per road point.** Two things switch `temporaryDisabled`: `SceneCordon` (accidents, and the Events director's own instance) and `Civic/RoadClosure`.
Each shuts only points that are open when it looks, remembers exactly those, and opens exactly those again; none opens a point it did not shut.
With one scene at a time and no accident or event beside a building site, two owners can no longer want the same stretch.

The one theoretical overlap left is `SceneCordon.Recheck`, which looks at its own list once more on the tick after a scene has cleared: a roadworks crew would have to close one of those very points in that tick, and its start test (`CivicBridges.SceneOn`) is false only from the tick after.

## Every bridge, and who answers it

| bridge | answered by | with |
|---|---|---|
| `Justice.Bridges.Law` | `Wiring.Arrests` | `ReleaseSpot`: the pavement outside the nearest precinct, and its name. `PriorConvictions` -1, `Custody` false, the three record calls empty (see [For Scripts/Law](#who-does-what-at-an-arrest)) |
| `Justice.Bridges.Licences` | `Wiring.LicenceLinks` | the licence's own suspension date |
| `Nav.NavBridges.HudHeld` | `Wiring.NavLinks` | true during custody (`Law.Custody.Active`), a bus ride (`TransitDirector.Riding`) and photo mode: the minimap is hidden |
| `Nav.NavBridges.MoreMarks` | `Wiring.TaxiLinks` | adds the called taxi to the minimap's marks where it is, with the game's vehicle picture in the colour the game gives its private driver |
| `TaxiBridges.Held` | `Wiring.TaxiLinks` | the police have business with the player (`PoliceAttention.IsBusy`, which includes custody), a driving test is on, or they are in prison: no taxi is sent, one that is out is let go free |
| `TaxiBridges.Riding` | `Wiring.TaxiLinks` | `TransitDirector.Riding`: no taxi is sent; one that is out counts as cancelled |
| `TaxiBridges.Wet` | `Wiring.TaxiLinks` | "Weather that matters" is on and `Events.Weather.Sky` is rain or a storm |
| `TaxiBridges.Roads` | `Wiring.TaxiLinks` | `Nav.NavDirector.Roads`: the GPS's road graph, searched with a search of the taxi's own (null while the minimap and the GPS are both off: the taxi then makes its own) |
| `Nav.NavBridges.GuideToStation` | `Wiring.NavLinks` | `LawDirector.GuideToNearest`: the nearest police station becomes the game's destination (the GPS's test button) |
| `Justice.Bridges.Social` | `Wiring.LifeLinks` | the closest person at "close friend" or above; standing by the player is remembered. The Friend contact with the social life off |
| `Justice.Bridges.Transport` | nobody | Transport keeps no pound for cars; the default tow and storage charge stand |
| `Licences.Law` | `Wiring.LicenceLinks` | `Custody`: the courts' custody, else the old time skip; `Record`: nothing (Law's record has no line for a licence offence) |
| `CivicBridges.CriminalRecord`, `Reputation` | `Wiring.CivicLinks` | `Standing.HasRecord`, `Standing.PublicSigned` |
| `CivicBridges.LargeScene`, `WeatherStopsWork` | `Wiring.CivicLinks` | `Scenes.AnyBut(Rally)`; the game's rain or a thunderstorm |
| `CivicBridges.News` | `Wiring.CivicLinks` | the headline at once, the story in tomorrow's paper (`Wiring.Paper.Print`) |
| `CivicBridges.CityPeopleOut` | `Wiring.CivicLinks` | the living city's people that are out |
| `CivicBridges.NoteCrime` | `Crime/OffscreenCrimes` | break-ins out of the player's sight (city hall counts staged crimes and accidents itself) |
| `CivicBridges.Labouring` | nobody | Jobs has no labouring shift |
| `CivicBridges.FinesScaledAtSource`, `FaresScaledAtSource` | left false | civic life applies both policies itself after each charge; nothing is scaled twice |
| `CivicPolicy.EnforcementScale`, `ServiceScale`, `EventScale` | applied at the source | `PoliceAttention.Seen`, `TransitDirector.Headway`, `EventCalendar` (one line each; 1 with civic life off) |
| `CivicPolicy.ClosingHour` | nobody | civic life's stock hour is 2, the bar's is 4 and night life's 3: applying it would change Leisure with no policy enacted. To be decided, not wired |
| `SocialLinks.Law`, `Wellbeing` | `Wiring.LifeLinks` | `Standing`; Home's stress and meals |
| `CareerLinks.Law`, `Health` | `Wiring.LifeLinks` | the CV line from `Standing`; Home's sick note or the mod's own sickness |
| `CareerLinks.Social` | nobody | the social cast is a fixed eight: there is no way to add a colleague to it |
| `FamilyBridges.Social`, `Home`, `Law`; `SocialLinks.PartnerChanged` | `Wiring.FamilyLinks` | the partner is Social's person; sleep debt and illness; whether the player is locked up; a break-up in Social reaches Family at once (`Wiring.Arrests.FamilyHears` tells it of arrests) |
| `FamilyBridges.Travel`, `Leisure` | nobody | a trip is bought at the travel agent's desk, and a leisure activity is the player's own at a venue; neither can be run for a named partner |
| `TravelBridges.Justice` | `Wiring.LifeLinks` | no trip with a warrant out, an undecided bail or a court date inside it |
| `TravelBridges.Home` | `Wiring.LifeLinks` | a hotel shower is hygiene, rest and holidays take stress off (`Laundry`: nothing to act on) |
| `TravelBridges.Social` | `Wiring.LifeLinks`, only with the social life on at city load | partner and close friends as companions, a trip together, souvenirs for friends |
| `TravelBridges.Transport`, `Leisure` | nobody | there is no coach and no staged golf or dinner |
| `GarageBridges.RoadIsWet` | `Wiring.LifeLinks` | the game's rain or `Events.Weather.Sky` |
| `DrivingBridges.LiftParts`, `LayParts` | `Wiring.DrivingLinks` | `GarageCar.LiftHandling` and `LayHandling`: the parts off before the handling changes a car, back on top after (nothing with no build) |
| `HomeBridges.Sheltered` | `Wiring.LifeLinks` | up in a hotel room (`TravelState.InRoom`) counts as indoors for the rain and the cold it brings |
| `HomeBridges.SleepingAway` | `Wiring.LifeLinks` | a night or a nap in a hotel room (`HotelStay.SleepingBed`) counts as sleep, in a bed of 50 plus the room's sleep bonus |
| `LifeAccounts.Home` | `Wiring.LifeLinks` | `HomeTidy.HasHome`: a residential building rented or owned by the player; a hotel or hostel booking does not count. No home when unwired or on a fault |
| `GarageBridges.WearOf`, `InsuranceOf` | `Transport/CarOwnership`, `CarPhone` | tyres and brakes wear `1 + WearOf` times as fast; the premium is `1 + InsuranceOf` times `CarRules.Premium`, on the bill and on the Garage page |
| `GarageBridges.ValueAdded`, `GarageSummary.Lines` | nobody | Transport has no resale; `Lines` lists every car at once, the Garage page shows one (it needs a one-car form) |
| `GarageBridges.HasMotorcycleLicence`; `Licences.Driving.TwoWheelerOf`, `OwnsMotorbike` | `Wiring.LicenceLinks` | the motorcycle class: held or not at the sale; a motorbike is the class and a bicycle none; a save that owns one is given the licence once |
| `GarageBridges.PoliceNotice` | nobody | the Garage's own fine works alone; a stop for it would be new police behaviour |
| `GarageBridges.RiderFalls` | nobody | `PlayerKnockdown` has no door for "down now", and a ragdoll on a rider leaving a scooter in the same frame cannot be judged without a run |
| `Events.EventDirector.CrashFire` | nobody, on purpose | a severe crash already burns with the accident's own fire (`TrafficEffects.CreateFire`, 6 in 10 severe crashes, put out with the scene); `CrashFire` would be a second fire on the same bonnet |
| `Events.Weather.SightFactor` | `Traffic/PursuitSight.Lost`, `Crime/PoliceAttention.Seen` | how far a patrol car keeps sight (150 m in fair weather) and how far an officer on foot notices driving (60 m) |
| `Events.Weather.Wardrobe` | nobody | the wardrobe catalogue keeps what is worn private and has no coat |
| `Economy.EconomyBridges.Legal`, `LegalReady` | `Wiring.EconomyLinks` | convictions that count (`Standing.Convictions`); from `JusticeState.Debts`: civil judgments being paid off, what is owed, and the days running on which a debt could not be taken (`JusticeDebt.Missed`); `ModState.PendingFine`; `Law.Record.Recent(30)`. Ready when Justice has read the save |
| `Economy.EconomyBridges.OtherDebts` | `Wiring.EconomyLinks` | the same debts as lines for the Finance app: a court fine, legal fees, a civil judgment, a fine waiting at the police station |
| `Economy.EconomyBridges.OtherAccounts` | `Wiring.CareersLinks` | the student loan as a credit account, registered on the rating each time the credit side starts (its ledger forgets every account when it stands down) |
| the Planner's Finance rows | `Wiring.EconomyLinks` (`PlannerDirector.AddSource`) | tomorrow's payments over every credit account, a missed payment, a change of grade: read from `CreditLedger` |
| `News` (the City Ledger) | `Wiring.Paper.Print(key)` | a finished story: verdicts (`JusticeDirector`), fires (`EventDirector.Begin`), election results and scandals (civic life) |
| `Texts.TextsBridges.Staff`, `Life`, `People`, `PersonSay`, `BigFloor` | `Wiring.TextsCity` | the player's staff from running a business, the player's life from the features' public state, the social life's people, a text through the social life's own (its switch, daily cap and quiet hours), the least big win or loss at today's prices. Read once a game hour with RLO Texts on: the player's life and people for the texts after the player's news with a pack or without one, the staff only with a pack of data 6 |
| `Flare.FlareBridges.TextReview` | `Wiring.TextsLinks` | a review of the player's own business: its satisfaction figures in words and how busy it was, from the game's insight page |
| `Street.StreetDirector.Spoken` | `Wiring.StreetTexts` | each line a stranger says, however its words came: what they will remember of it (data v6) |
