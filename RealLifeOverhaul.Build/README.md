# Real Life Overhaul

A mod for Big Ambitions: third-person cameras, traffic accidents with emergency response,
people on the street (drivers, paramedics, officers, street incidents), crash consequences,
random life events, and extra phone contacts.

## Layout

```
Assets/Mods/RealLifeOverhaul/      the mod (single source of truth)
  ModManifest.asset                id, version, what the Mod Builder packages
  RealLifeOverhaul.asmdef          assembly definition and references
  Locales/en.json                  every player-facing string, flat keys prefixed reallife_
  Bundles/reallife_character       meshes and textures for the player's new looks (built by CharacterAssets, see below)
  Bundles/reallife_props           the modelled street props and their texture atlas (built by PropAssets, see below)
  Bundles/reallife_anim            the mod's own Humanoid animation clips and how each tool sits in the hand (built by CityAssets, see below)
  Scripts/
    Core/      entry points, settings, per-save state, shared HUD, shared shapes for props (Shapes), modelled props by id (PropLibrary; plain shapes are the fallback)
    Core/      entry points, settings, per-save state, shared HUD, the modelled props (PropLibrary) and the plain shapes they fall back to (Shapes)
    Camera/    on-foot third-person camera, driving chase camera; the game's place markers put right when their target is behind the camera (PoiDirection)
    World/     enter buildings and vehicles with a key, visual grading; every feature's places on the city map (MapLinks)
    Life/      random life events (catalogue, no-repeat deck, runner), sickness, hospital
    Traffic/   staged traffic accidents with ambulance and police response, hit-and-run chases, player crash consequences
      TrafficAccidents  the orchestrator: what happens and when; owns the scene and clears it on every exit
      ResponseUnit      one ambulance or patrol car taken from traffic (always the real ambulance, always the police body)
      EmergencyDriver   drives a unit along the road points: closes up and stops short of vehicles and people, never leaves the road or forces through; says what holds it up
      RouteGates        red lights and closed lane points for a vehicle the mod drives like anybody else (the bus's rules, for the called taxi)
      SceneParking      the free-space tests (what a pose touches; a unit's whole body against every solid layer; a strip of road), and where a unit pulls up when a scene has no lane for it to stand in
      SceneCordon       closes the road round a scene (the traffic system's own switch) with an apron for the response vehicles, says where they stand; tape and barriers with a wall along each, an opening wherever the ambulance crew's way crosses it and nothing standing on that way (SetCorridor), the cone taper, the gate (its hold line 24 m before the scene; held shut for a leaving unit, and for the crew), debris and evidence markers at the point of impact
      TrafficWatch      self-check: stops a vehicle that bounces on the spot, and reports and removes a response vehicle left standing with its crew aboard or after its scene
      SceneObstacles    cuts the vehicles standing at a scene out of the navigation mesh, so people on foot go round them
      TrafficEffects    lights, sirens, the police body, fire, smoke, debris
      PursuitSight      whether a patrol car still has the player in sight: shared by both pursuits
      PursuitPace       how fast a patrol car after the player may go (0.95 of their car's top speed, 130 to 200 km/h) and how long it keeps at it
      PursuitBackup     the second patrol car that joins a pursuit of the player after 45 s, if traffic has a car to give
      TrafficTools      what all of these need from the traffic system; a vehicle's real footprint from its colliders
    People/    the mod's street characters and the scenes they play
      ActorPool      the twelve characters every scene shares: created once, reused in the same outfit, hidden when out of view; four may be out only to be seen and give way to any scene
      Uniforms       the one outfit every officer wears and the one every paramedic wears: named garments, fixed colours; the hi-vis shirt
      ServiceKit     what is worn with a uniform, on the bones: badge, belt, radio, cap, POLICE / PARAMEDIC panels, the medic bag; fitted to the measured body, the cap to the measured head (band above the brow, peak above the eyes)
      Stature        how tall a character stands, from its joints and measured crown; the scale that makes officers 1.86 m (women 1.76 m) and no paramedic short
      BonePose       aim a bone, reach a hand to a place: shared by KneelPose, SupinePose and Crime/CuffPose
      StreetActor    one character: walk, stand, pose, kneel, gesture
      KneelPose      down on one knee, set on the bones by hand (the game has no such animation); costs nothing to hold
      SupinePose     flat on the back along a bed, for an animated character or a ragdoll at rest: what the stretcher lays its passenger in
      ActorRole      one person's part in a scene; safe to use when nobody could be had
      StreetSpots    where people can stand: beside car doors, on stretches of pavement
      StreetPeople   who counts as a person on the street besides the walking pedestrians: the game's standing and sitting people, from its pools' parents (once a second, only while asked)
      AccidentCast   drivers, paramedics and officers at a traffic accident (run by Traffic/TrafficAccidents)
      CrewWay        the ambulance crew's one way from the rear doors to the casualty and back, planned and tested pose by pose before anyone sets off, and the fixed layout beside the casualty (stretcher, who kneels where)
      CrewPath       that way as plain geometry (no engine types; checked outside the game): a line with rounded corners laid out by distance, the crew's pose at any distance along it, what it sweeps over, how near that comes to a point, a box or a length of tape
      SceneWitness   an unhurt driver or bystander at a scene: checks the casualty, phones, looks for help; out to the kerb the moment the tape is up or the paramedics are out, and gives their account there
      SceneOfficer   one officer's jobs at a scene: one on traffic at the barrier (stepping out of the open lane to wave the ambulance through when it leaves that way); the other at the casualty, setting out evidence markers, taking the witness's account, at the cordon's opening
      Stretcher      the ambulance crew's wheeled stretcher: one, built once; wheeled along a CrewPath, moving its crew with it frame by frame (no navigation agent finds a way), swung where a corner will not do; its passenger laid straight (SupinePose) under a blanket, or a covered form
      PeopleAhead    the people a mod-driven vehicle must brake for whom physics cannot see
      RoadsideStop   the crew of a patrol car at a stopped car: a ticket at the window, or an arrest (Crime/ArrestSequence)
      StreetCrime    the old name for street incidents; forwards to Crime/CrimeDirector
      BeatPatrol     a pair of officers walking a beat near the player, most of the time
      PolicePresence a patrol car parked at the kerb with its officers by it, and an officer at a junction
      ICasualty      someone lying at a scene who is not one of its cast (a person hit by a car)
    Crime/     crime near the player, by the player and against the player
      CrimeCatalogue   every crime: where and when it can happen, weight, endings with odds, its lines; fines and odds (CrimeLaw)
      CrimeDeck        deals crimes, endings and lines without repeats (game-free, like LifeEventDeck)
      CrimeDirector    one scene at a time: when, which, ticking it, the marker, 911, the test buttons
      CrimeScene       ICrimeScene and the base every scene shares: cast of four, steps, arrests, clearing up
      CrimeScenes      which scene plays which catalogue entry: one line per crime
      Scenes/          the scenes: FootChase, Questioning, Disturbance, Snatch, Mugging, Assault, DrugDeal,
                       LoneOffender (graffiti, car break-in, drunk, vendor, panhandler, fare dodger),
                       CarPursuit (traffic stop, reckless driver, car theft), ParkingTicket,
                       OwnShopTheft, ShopIncident (shoplifter, hold-up), PlayerPickpocket, PlayerMugging
      ArrestSequence   the one arrest every scene uses: hands up, cuffed, to the kerb, into a patrol car, away
      CuffPose         hands behind the back, re-aimed after the animator each frame while (and only while) someone is cuffed
      Crime            money, toasts and small helpers shared by all of the above
      CrimeCars        finds a parked car to stand beside or an ordinary traffic car to take over
      Knockdown        one of a scene's people knocked off their feet (RagdollSystem) and up again
      ShopFloor        what indoor scenes read from the shop: doors, shelves, camera cover, the guard
      OffscreenCrimes  overnight break-in, parked car broken into, scam call: rolled once a day, all capped
      PoliceAttention  the player's own offences: seen or not, pulled over, ticket, pursuit, escape, custody
      PlayerOffences   notices red lights, speeding, pavement and wrong-side driving, jaywalking (timed, player only)
      RoadMap          stop lines and lanes from the traffic system in a grid, built once per city load
    Ragdoll/   people hit by cars: ragdoll physics, pedestrian collisions, the player knocked down
      RagdollRig          builds a ragdoll on a character's bones (bodies, colliders, joints) and strips it again
      Ragdoll             one character as a ragdoll: animated to physical, frozen at rest, restored completely
      RagdollSystem       the six ragdolls there are, for full characters and the game's plain standing figures alike, and up to eight bodies that lie on without physics when a seventh person goes down (removed out of view); the only per-frame work, and only while someone is down
      PedestrianTakeover  borrows one of the game's pooled pedestrians and gives it back (walking, or through the pool)
      Casualty            one person who was hit: lies, gets up angry and walks off, or stays until taken away
      PedestrianIncidents rolls what a hit does by speed, looks after the casualties, stages traffic hitting someone
      CarStrikes          notices the player's car running into people (positions, not collisions): walkers, standing figures (StreetPeople) and the mod's own, everyone in the sweep, one log line per batch
      PlayerKnockdown     the player on foot hit by traffic: brief ragdoll, then shaken, injured or hospital
    Phone/     callable contacts and their conversations
    Character/ character quality: detailed player in the city, a scalp under its hair, character-screen lighting
      CharacterQuality  entry points (city, character creation); hair, looks and textures on every change of look, options
      CharacterCatalogue the new looks on offer (one row each); the picks, which belong to one character (the one being created, or the loaded save's: never PlayerPrefs) and give way, per kind, to a piece picked in the game's own screens
      GridLooks         the mod's garments as entries of the game's own grids (Torso, Legs, Feet) with their colourways in its colour row: the creator's starter garments, the clothes screen's wardrobe and shop
      ModAssets         the asset bundle: read once in the background, bind poses taken from the game's head mesh
      ModLooks          puts the scalp underlay (and any picked look) on the characters that show the player; undoes itself
      PlayerDouble      the player drawn with the game's detailed character set, posed from the player's skeleton
      HairLook          puts the hair finish on the hair and beard of the characters that show the player; undoes itself
      HairFinish        what a finished hair layer is (depth prepass, strand maps, gloss from the colour); game-free, the HDRP preview compiles it too
      CharacterLighting fill and rim light for character creation and the shop preview; undoes itself
      CharacterTools    telling the two character sets apart, copying one inertly, sharper textures
    CityLife/  the living city: people at work and about their day on the pavements near the player
      CityLifeDirector  what is out and where: three sites and eight people at most, set up and taken away only out of view; the test buttons and why each refused
      CityCatalogue     every activity: who, when, how often, in the rain or not, the activity that plays it (one entry each)
      CityActivity      ICityActivity (find room, begin, tick, end) and the base every activity shares: people who walk in (Arrive) and walk off (Depart)
      Activities/       SoloActivity (one person, data only: sweeper, window cleaner, painter on a ladder, tai chi, newspaper, doorman...),
                        RoadCrewActivity, ChatActivity, BuskerAudienceActivity, JoggerActivity,
                        StallActivity (coffee and hot-dog carts with a queue that walks up, is served and walks off),
                        CourierActivity (sack truck, parcel to a shopfront), WalkingActivities (the mail round; pushchair; a traveller with a suitcase; umbrellas in the rain),
                        StandingScenes (chess table, a driver leaning on a parked car, a door with a queue, a gardener at a street tree)
      CityPeople        the pool of eight (not the accident pool's six): made a person at a time, hidden when out of view, knocked down by the player's car
      CityPerson        one of them: stand, walk, play a clip with its tool, drop it when hit
      CityOutfits       work clothes from the game's own garments by name, in hi-vis and council colours
      CitySpots         where people may stand: pavement at street level, clear, away from doors; a shopfront at a measured distance
      CityProps         the prop objects, made once and reused
      ClipLibrary       the mod's animation clips by name, from Bundles/reallife_anim
      Animation/        the clip player any feature can use (no game types in here: the asset pipeline tests these very files)
        ActivityAnimator   plays a clip on one character over the game's controller: cross-fade in, hold, hand back; a masked layer (arms, one arm, upper body) over walking; speed matched to the ground
        ActivityAnimators  steps the players that are fading and the hands on tools; switched off when there are none
        HeldTool           a tool on a hand bone at the grip its clip was made with
        TwoBoneReach       puts the second hand on the tool after the animation
    Jobs/      jobs the player takes themselves, from the Gig Line contact on the phone
      JobCatalogue   every job: needs, pay per unit and per metre, energy, skill, daily amount, levels (game-free)
      JobState       per-save experience, strikes, sackings, today's and this week's totals: one line in modData (game-free)
      JobDirector    the one job that is on: start, 0.2 s tick, interruptions (indoors only in the one building a job names), pay, the test buttons
      Job            the base every job derives from, the pay slip, what a job is handed each tick, text helpers
      JobStand       a place of work taken by walking on to it: the last step, the turn, the hold, the working pose, stepping away
      JobUniform     work clothes over the player's own (the game's UpdateVisuals(list)); the director takes them off on every way out
      ShiftSkip      the rest of a shift left to the game's clock: one of the game's activities on the game's own panel, as its employer job
      ShiftCard      the shift's figures on the HUD: a copy of the game's job panel, five lines written only when they change
      CareerClock    a shift's time as start to end in the game's clock format: the one formatter every text asks
      JobKeys        every key test and key name the jobs use: the two lines to change for rebindable keys; fills {key} in texts
      JobPhone       the Gig Line contact and its conversation, on the phone or face to face at a board
      JobBoards      help-wanted boards outside a few service buildings: the same list as the Gig Line
      JobCast        the two characters jobs put on the street (never the accident pool's)
      JobSpots       door fronts and the ground beside the player's car, tested before anyone is put there
      JobAnimations  IJobAnimations and JobClip: the one door to a person's body; today played with the game's gestures
      JobProps       what a job puts in the player's hand that is not a game item (the flyer stack)
      Kinds/         the jobs: FlyerJob, CourierJob, TaxiJob
    Driving/   the car the player drives: performance by kind of car, the eased throttle, speedometer (a reading in the game's vehicle panel, SpeedReading), towing
      CarCatalogue   every vehicle type's targets (0-100 km/h time, top speed) and kind, and the rule for unknown types (game-free)
      CarModel       a car in a straight line as the game's physics moves it; finds the power, gearing and drag that meet a target (game-free)
      CarTuning      reads the driven car, applies what CarModel found, puts every value back; the eased pedal
      CarDrag        air drag on the driven car, one force per physics step, only while a tuned car is driven
      CarMeasure     the "Measure this car" button: times a full-throttle run, off otherwise
    Garage/    the custom shop for cars, and bicycles and motorbikes (see "Garage" below)
      GarageDirector   the one component that ticks: builds onto spawned cars, underglow after dark, the timers of the lists and panels, the test buttons; GarageSummary ("My vehicles", in the game's list)
      GarageBay        the game's own panel in a repair bay with one more line and a "Garage" button (RepairBayOverlay); the workshop's list; GarageList (the one list window the Garage's screens share)
      GarageCatalogue  paint chart, finishes, performance parts with effects and trade-offs, every price; GarageBuild (one car's build, one line of text) (game-free)
      GarageState      the save's part: builds by vehicle id, which scooters are two-wheelers; GarageBridges (what other features may set or ask)
      GarageFit        every car body as measured (roof, boot deck, nose) and which fitted part goes on which car
      GarageCar        one car wearing its build: paint through the game's own property blocks, lamps, underglow, fitted parts, brakes, tyres, springs, weight; puts all of it back
      GarageTuning     power, gearing and drag for a build through CarModel; the hook for Driving/CarTuning; the shop's before-and-after figures
      GarageShop       the repair bays, the turntable camera, the shop's pages in the game's list, the game's colour picker, the bill; GarageDragArea (the invisible drag surface of the turntable view)
      GarageText       a locale key in, the line out
      TwoWheelers      bicycles and motorbikes as re-bodied scooters: making, removing, hiring, buying, energy, helmet, falls; the game's panel at a dock, rack or stand, the stand's list, the game's marker on one
      BikeMount        one scooter as a bicycle or motorbike: body, numbers, the rider's seat, hands and feet; LegIk
      BikeBody         the four models (BikeCatalogue) and their stand-in bodies built from tubes and rings (MeshMaker)
      BikePlaces       bike-share docks, bicycle racks, motorbike stands: where they go and what stands there
      JobSites       a client's premises: which businesses will do, clear floor and shelves inside that the player can walk to
      JobAnimations  IJobAnimations and JobClip: the one door to another person's body; the mod's clip where the bundle has one, the game's gesture where not
      JobBody        the player's own body at work: held on the spot, a clip with its tool, or the game's own animation and hand object
      JobProps       the jobs' props, pooled (modelled, or the prop that stands in, or a plain shape); HeldProp: one in a hand at its clip's grip
      JobCarry       a box in the player's arms with the game's own carry
      JobRental      a van hired for the day: the game's own job van, made and removed the game's own way
      Kinds/         the jobs: FlyerJob, CourierJob, TaxiJob, CleanerJob, StockerJob, VendorJob, BuskerJob
    Law/       police precincts, being arrested and held, the criminal record and reputation
      LawBook        the offence table and its sums: hours per charge, the two-day cap, repeat offenders, bail, the lawyer (game-free)
      Record         Record (the criminal record), Reputation (police and public, scrutiny) and LawState: their four lines in modData
      Precincts      which buildings are the precincts (one per district, fixed by address), their map markers, the dressed frontage
      Custody        one arrest: the scene on the street, the ride, the cell as a time skip, bail and lawyer, release, resume after a load
      CustodyList    the holding cell on screen: the game's list window with the charge sheet, release clock, three entries
      StationDesk    the front desk: the game's panel at the counter and the game's list window (DeskList), a second page for the record
      LawDirector    the one tick a second: load and save, dressing, the daily step, thanks from passers-by, the watchdog, test buttons;
                     Arrests: the one call each for whoever catches the player
      RecordPhone    the Police Records Desk contact: record, reputation, time since the last offence, nearest precinct
    Transport/ bus stops and buses, the subway as a journey, car ownership (see "Transport" below)
      TransitMap       the bus network: six routes between subway stations along the road graph, stops by rule; built once per city load, in memory only
      TransitDirector  what of it is on the street: three dressed stops, the stop that is served, its people, the bus, the player's ride; the test buttons
      BusStopDressing  one stop's pole, flag, shelter and bench (modelled props when the bundle has them, plain shapes to the same size until then); on the city map every stop is a place of the "Bus stops" row (World/MapLinks)
      TransitBus       one bus: a van from the traffic pool with a route board, driven by the traffic system on a scripted route, stopped "in point" at the stop
      BusBay           the geometry of a bus bay and nothing else (game-free: NavCheck drives a model bus through it): where the bus stands, the curves in and out
      TransitBays      a bay on the street as it is now: whether its length of the strip is free; the game's parked cars standing in it out of view are taken in
      TransitRide      the ride itself (fade, the game's time machine, put down at the far end), for bus and subway; TransitState: day pass and ride counts in modData
      SubwayJourney    the game's own subway taking a journey's time: the panel at each entrance, service by the clock (frequency, delays), the day pass, the City Transit contact
      CarDash          what a car needs as one more reading in the game's own vehicle panel (a copy of its "Condition" reading)
      CarGarage        CarRecord (per owned car, in modData by vehicle id), CarRules (every price, interval and chance; game-free)
      CarOwnership     the driven car: distance, wear felt through grip and brake torque, thirst, breakdowns, what is due in a repair bay (for Garage/GarageBay), the game's panel over a broken-down car, insurance payouts, the daily round
      CarRoadside      a mechanic who walks up, kneels at the car and walks off: breakdowns and fuel brought to the roadside
      CarPhone         the Garage page and Roadside Assistance contacts
      Taxi/TaxiRules   the taxi called from the phone, in plain numbers (game-free: NavCheck runs them): the call-out, a cancel, nobody came, the start bands, the quoted wait, the refusals, the watchdog
      Taxi/TaxiRoads   which lane is the caller's, where out of view a taxi can be put, and the way between (one search over the shared road graph); TaxiBridges; TaxiPlan
      Taxi/TaxiCab     the taxi itself: one of the game's own, taken from the traffic pool, driven to the caller by the mod's driver, stood there for them, let go in every ending
      Taxi/TaxiDirector  one call at a time: quote, send, watch, arrival, the wait, and how a call ends (the game's ride, nobody came, a cancel, it cannot come); the test buttons
      Taxi/TaxiPhone   the City Cabs contact and its conversation
    Leisure/   things to do for fun at real places, the mod's clothes as something bought, night life, the sound bed, photo mode (see "Leisure")
    Events/    city events: fires and the fire service, weather that matters, street events (see "City events" below)
      EventDirector     the one scene that is on (a fire or a street event), yielding to accidents and crimes; calendar announcements, the City Bulletin contact, 911, the test buttons, one line in modData
      FireScene         one fire from first smoke to the engine leaving (car, bin, shopfront); HoseHold (hands on the nozzle); FireDamage (the player's own business: closure and repair bill)
      EventRoad         the adapter over Scripts/Traffic (public surface only): lanes, the stretch to close, bringing a pooled vehicle; EngineLivery and FireEngine
      FireEffects       flames, smoke, steam, water jet and firelight: seven pooled particle systems copied from the game's smoke; Fireworks (four pooled mesh-particle systems)
      Hose              sixteen pooled segments laid along the ground from the engine to the nozzle, clear of vehicles and street furniture
      EventPeople       the event pool of eight (a second CityPeople): people on a closed carriageway, things worn on a bone, a staff in the hand, walking in and off
      EventProps        the props by id: the modelled one if the bundle has it, else a plain-shape stand-in to the wish-list's size and pivot
      FireOutfit        turnout gear from the game's garments by name
      Weather           the static factors other features read (accidents, crime, sight, street life, grip, traffic speed); WeatherEffects applies tyres, traffic pace, energy, forecast
      EventCalendar     what is on which day: fixed days placed by how far through the year they fall, the rest by a roll that depends on the day and the save
      StreetEventScene  market, farmers' market, parade, protest, fun run, film shoot, fireworks: one class, one closed stretch of road
    Social/    the people in the player's life, outings with them, and the morning paper
      SocialModel     the cast's records, the plan, the rules (stages, what things are worth, neglect), the save: game-free
      SocialCatalogue names, jobs, traits, outings, every line anybody says: game-free
      SocialLife      what happens between the player and a person, with its consequences in the game; the optional links (ISocialLaw, ISocialWellbeing)
      SocialDirector  load and save, the hour and the day, who gets in touch, the plan, the one scene, the test buttons; SocialHooks
      SocialScene     the base of a scene (talking face to face, walking off) and MeetScene (somebody walks up)
      OutingScene     a plan kept: the eight outings, a lift, helping them move
      SocialDialogs   the call to a person, the People page, the Ledger read out, the conversation in the street; SocialTalk (topics from what happened)
      SocialPhone     contacts, icons, the paper delivered, ringing
      SocialCast      the two characters, wearing the look of whoever they stand for
      SocialBody      the player's own body in a scene: held, a gesture, a cup, a box
      SocialPlaces    venues by business type, streets, places to wait, walk in from and walk off to
      (SocialHud removed: the plan is a ShiftCard, the prompt a DoorPanel, the marker a GuideMark)
      News            the log any feature reports to (News.Report), and the desk that writes the paper
    Travel/    hotels to stay in and holidays to take: see "Travel and lodging" below
      Kinds/         the jobs: FlyerJob, CourierJob, TaxiJob, CleanerJob, StockerJob, VendorJob, BuskerJob;
                     and the two an employer's rank gives: DispatchJob (the planning desk), LeadJob (a shift in charge)
      CareerCatalogue  employers, ranks, rotas and every figure of the career ladder (game-free)
      CareerState      per-save employer, rank, rota, marks, review figures, employment history: one line in modData (game-free)
      CareerDirector   where each employer is (a help-wanted board), its boss by the board, the rota, clocking on, payslip, reviews, booked sets
      CareerPhone      the Work contact and the same conversation face to face with a boss: sign on, rota, clock on, sick, notice, CV
      CareerLinks      the optional doors to other features: acquaintances (offered), law record and sick notes (asked for)
      DispatchList     the dispatcher's planning board: the game's list window, written only when a parcel is placed or taken back
    Business/  pressure on the businesses the player runs: rivals, a racket, inspections, staff, suppliers, customers, audit, opportunities
      BusinessCatalogue  every kind of issue and every number that bounds it (game-free)
      BusinessState      open issues, running footfall changes and closures with their end, the racket: one line in modData (game-free)
      BusinessGame       the only code that reads or changes the game's business fields; each change bounded and logged
      BusinessIssues     what each kind is: when it can start, what it says, the choices, what each does, the default at the deadline
      BusinessDirector   the hourly clock, the one roll a morning, the off switch that puts everything back, the test buttons
      BusinessDesk       the phone contact that lists open issues with deadlines and choices; its messages are the record
      BusinessVisit      a collector, an inspector or a police sting played in the player's own shop while they stand in it
    Justice/   courts and prison: cases, bail, lawyers, the courthouse, hearings, sentences, the Legal file (see "Justice" below)
      JusticeLaw       every charge, cap, fee and formula: evidence, verdict, bail, sentence, appeals (game-free)
      JusticeState     what a save remembers of it: one line in modData (game-free)
      JusticeBridges   Justice (the door for the rest of the mod), Justice.Bridges (Law, Licences, Social, Transport; each with a default), JusticeReports
      JusticeDirector  the clock and the switchboard: hourly business, what waits for the player, the watchdog, the test buttons
      JusticePanel     the hearing, bail, cell, prison and Legal file as pages of the game's list window (DeskList)
      Courthouse       which building it is, the sign on its frontage, where one stands to go in
      LegalCounsel     the law firms, rating and fee, motions and plea deals; the Legal contact and its conversation
      CourtHearing     one hearing: charges, plea, the rounds of a trial, the verdict and its sum, the sentence and its reasons
      PrisonStay       custody (hours) and prison (days) as time passing, day by day; JusticeHold: what is changed on the game meanwhile
      CommunityService a played litter-picking shift
      LegalFile        the Legal file page and what can be done from it
    Licences/  driving licences (school, theory and driven tests, points, enforcement): see "Driving licences" below
    Civic/     a city government with elections and policies, and construction sites that progress over days (see "Civic life")
    Family/    a partner who moves in, engagement and wedding, children, relatives, hard times (see "Family" below)
      FamilyDirector  the one component: a tick a second, the key prompt, questions, sequences (fade, time skip), scenes, the test buttons; FamilyModifiers
      FamilyState     per-save state as one JSON string in modData; FamilyBalance: every cost, threshold and time scale
      FamilyBridges   ISocialBridge, IHomeBridge, ITravelBridge, ILeisureBridge, ILawBridge and FamilyApi: what other features implement, call and may read
      Partner         who the partner is: Social's person when its bridge is set, else the one record the default keeps
      FamilyCast      the family's own three characters (seeded looks, formal or day wear) and FamilyProps (wish-list props with plain-shape stand-ins)
      FamilyPlaces    the city's own buildings by kind of business (jeweller, planner, florist, hospital, school, theatre, restaurant...), the home, floor spots indoors
      HomeLife        the timetable, the partner shown at home, moving in, routines, their share of the rent, chores, support, evenings out
      Errands         somewhere to be on a day (a scan, a dinner, the wedding, a school play) or to shop: marker, objective line, prompt at the door, what a miss does
      Courtship       the ring at the jeweller, the answer and its odds; ProposalScene
      Wedding         WeddingPlanner (venue, size, extras, date, deposit, invitations, postponing, cold feet), WeddingDay, WeddingScene
      Children        deciding, pregnancy and adoption, the nursery, birth, growing up, school, birthdays, costs; WalkScene (the pushchair walk)
      Relatives       Mom and the in-laws, the four family dinners of the game's year, next of kin
      HardTimes       arguments (a no-repeat pool with two answers), counselling, time apart, coming home, divorce and custody
      FamilyPhone     the Family, Someone special and in-laws contacts, everything that can be done from a conversation; FamilyPage (the five pages)
      FamilyTalk / FamilyText / FamilyCalendar  conversations and menus, text and its fillers, what happens by the hour and the day
    Diagnostics/ temporary F9 character report (remove before release)
      SessionWatch   the session record: with "Developer diagnostics" on, one line a minute (frame times, memory, the mod's objects) and one for every frame over 80 ms, naming what the mod was at (see "The session record")
      FrameSteps     where the frame before this one went, by engine step (scripts' Update, coroutines, physics, navigation, rendering...), for the long-frame line; in the engine's loop only while the record runs
    Performance/ fixes for the base game's frame rate, and the measuring that decides them
      PerfDirector   switches the tweaks as the options ask, gives each its timer tick; F7 probe, Shift+F7 benchmark
      PerfTweak      one optimisation: Apply records what it changes, Restore puts it back; a fault switches it off
      PerfTweaks     the list of tweaks (options page, saved choices, benchmark rows all follow from it)
      Tweaks/        the tweaks themselves, one small class each
      PerfSampler    frame times, main/render/GPU split, engine counters over a window; off unless measuring
      PerfLoop       where the main thread's frame goes: a timestamp in front of each of the engine's frame steps, and per camera; off unless measuring
      PerfProbe      F7: ten seconds measured, then RealLifeOverhaul_perf_HHmmss.txt with the step table and a census of the scene
      PerfCensus     what is in the scene and how the engine is set up (report time only)
      PerfBench      Shift+F7: every tweak, trial (one render-pipeline feature off) and reference measured off/ON/off/ON in one-second windows, paired so drift cancels; RealLifeOverhaul_bench_HHmmss.txt
      PerfGame       what the above need from the game (pedestrians, traffic, lamps, trees)
    Meta/      the mod about itself: the feature registry, the in-game guide and welcome panel, realism presets,
               the options page in sections, the save tools (see "Meta" below)
    Navigation/ the minimap on the HUD and the GPS: a route along the roads to the game's destination (see "Minimap and GPS" below)
    Economy/   the credit rating, the banks' terms by it, loans from the phone and the Finance app (see "Economy" below)
CharacterAssets/                   asset pipeline for the new looks (outside Assets, Unity ignores it)
  scripts/                         Blender scripts: make the meshes and textures, write them to build/
  PreviewProject/, preview.ps1     the game's character rebuilt under HDRP: a look is judged here before it ships
  UnityProject/                    small Unity project that only turns build/ into the asset bundle
  build_bundle.ps1                 runs it in batch mode: build/ -> Assets/Mods/RealLifeOverhaul/Bundles, then a load check
PropAssets/                        asset pipeline for the street props (outside Assets, Unity ignores it)
  scripts/                         Blender scripts: paint the atlas, model every prop, render previews
  UnityProject/                    small Unity project that only turns build/ into the asset bundle
  build.ps1                        everything: atlas, props, previews, Bundles/reallife_props, load check
CityAssets/                        asset pipeline for the animation clips (outside Assets, Unity ignores it)
  scripts/                         Blender scripts: the authoring rig, THE CLIPS, contact sheets, FBX
  UnityProject/                    small Unity project: Humanoid import, the bundle, and the tests that prove the clips play
  build.ps1                        everything: clips, Bundles/reallife_anim, every clip measured on the game's skeleton, play-mode proof
GarageAssets/                      read-only extraction of the game's vehicle data (outside Assets, Unity ignores it)
  scripts/extract_vehicles.py      every vehicle prefab: hierarchy, meshes, materials, shader properties (how cars are painted)
  scripts/extract_fit.py           every car body measured from its triangles: the rows of Scripts/Garage/GarageFit.cs
  scripts/check_locale.py          the by-hand locale check, for when the CatalogueCheck executable is blocked
RealLifeOverhaul.Build/            command-line build and checks (outside Assets, Unity ignores it)
  RealLifeOverhaul.csproj          compiles the same scripts with dotnet and installs to ModsLocal
  CatalogueCheck/                  verifies the event and crime catalogues, their dealing, and locale keys
  PlannerCheck/                    checks the Planner's game-free parts (see "The Planner")
  NavCheck/                        checks the game-free parts of the minimap and the GPS, the shared route search (see "Minimap and GPS"), the bus bays (see "Transport", "Bays") and the taxi called from the phone (see "Transport", "A taxi by phone")
  NavCheck/                        checks the game-free parts of the minimap and the GPS, the shared route search (see "Minimap and GPS") and the bus bays (see "Transport", "Bays")
  EconomyCheck/                    checks the economy's game-free parts: the rating, the terms, the fee, the credit file and its line in the save, the loans (see "Economy")
```

## Setup on a new machine

1. Install Big Ambitions through Steam, Unity 2022.3.62f2 (must match the game) and the .NET SDK.
2. Open this repository's root folder in Unity. In **Big Ambitions > Welcome**, import the game DLLs.
3. If the game is not at `G:\Steam\steamapps\common\Big Ambitions`, pass
   `-p:GameManaged="<game>\Big Ambitions_Data\Managed"` to the dotnet commands below.

## Build

Two ways, same source:

- **Fast iteration (code only):**
  `dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release`
  compiles the mod and copies the DLL, the locales and `Bundles/` to the game's `ModsLocal/RealLifeOverhaul`.
- **Asset bundle (only after changing the looks):** `CharacterAssets/build.ps1` (or only the last step, `build_bundle.ps1`)
  rebuilds `Assets/Mods/RealLifeOverhaul/Bundles/reallife_character` from `CharacterAssets/build`
  with Unity in batch mode and checks that it loads; the bundle is committed, so a code-only
  build needs neither Unity nor Blender. See `CharacterAssets/README.md`.
- **Prop bundle (only after changing a prop):** `PropAssets/build.ps1` rebuilds
  `Assets/Mods/RealLifeOverhaul/Bundles/reallife_props` the same way; it is committed too.
  See `PropAssets/README.md`, which also lists every prop with its size, pivot and how to use it.
- **Clip bundle (only after changing a clip):** `CityAssets/build.ps1` rebuilds
  `Assets/Mods/RealLifeOverhaul/Bundles/reallife_anim`, measures every clip on a stand-in with the game's
  skeleton and proves in play mode that clips cross-fade over an Animator Controller; it is committed too.
  See `CityAssets/README.md`, which lists every clip, what it is for and how to use one from any feature.
What the dotnet build installs is the whole package, exactly as it is uploaded: `RealLifeOverhaul.dll`, `Locales/en.json`,
the three files in `Bundles/` and `thumbnail.jpg` (the Workshop picture). The Unity **Mod Builder** is not used for this
mod: its manifest (`ModManifest.asset`) names no asset bundle, because the bundles are built by the three scripts above.

Restart the game to load a new build. Publishing to the Workshop is done from inside the game
(Mods > Mod Creator); the texts for its form, the thumbnail script, the gallery pictures and the steps are in
`RealLifeOverhaul.Build/Workshop` (start with `UPLOAD.md`).

### Version

The version is written in three places and the build stops if they disagree (the `CheckVersion` target):
`<Version>` in `RealLifeOverhaul.csproj` (the assembly version, which is what the running mod reports),
`Changelog.Latest` in `Scripts/Meta/Changelog.cs`, and `Version` in `ModManifest.asset`. A new version also gets one
row at the top of `Changelog.All` and its two texts in `en.json`. 0.1.0 is the first public release; the numbers the
development builds carried (0.13 to 0.46) appear in this file's history notes and in the git log only. A save or an
options store written by one of those builds is treated as written before 0.1.0 (`SaveTools.DevelopmentVersionKey`,
`Welcome.DevelopmentSeenId`).

### Release defaults

"Show test tools" and "Developer diagnostics" are off as shipped. With diagnostics off the mod writes only warnings and
errors to `Player.log` (every logger in the mod is the one gate, `Meta.Dev.Gated`), and the four diagnostic keys (F7,
Shift+F7, F8, F9) do nothing. To work on the mod, switch both on under Diagnostics on the options page.

The gate drops the line, not the work of making it: the text handed to `Info` is put together before the logger sees
it. That is fine for a line written when something happens (a scene starts, a bus is sent). A line on a timer, or one
that needs a question asked of the game first (a self-check on the navigation mesh), tests `Meta.Dev.Verbose` and does
neither while diagnostics are off.

### When characters' looks are built (`People/Looks`)

Building a look is the one costly thing the mod's people need: `AppearanceSetter.UpdateVisuals` merges the character's
meshes (several `Mesh.CombineMeshes` and new arrays of bones and bind poses each time). The game's own pedestrians are
dressed once and kept, so the game does none of this while the city is played; in 0.1.0 the mod did it whenever a
living-city site was set up (a merge per person, in that frame), after every scene (every civilian re-dressed) and the
first time a scene needed each of its twelve people. The rule since 0.1.1:

- **Where it cannot be seen.** `RealLifeOverhaulCity.OnLoadAsync` runs behind the loading screen: there the living
  city's eight (`CityLifeDirector.Prewarm`) and six civilians of the street pool (`ActorPool.Prewarm`) are made and
  dressed. Four officers and two paramedics follow as soon as the prop bundle has loaded (their kit is made of it; 25 s
  at most), one a frame while the loading screen or a pause is still up (`Looks.Hidden`), else one every `Looks.Gap`
  (5 s). So all twenty exist before the first scene can start.
- **One at a time afterwards.** Whoever prepares a look ahead of need asks `Looks.Free`: one a frame while hidden, else
  one every 5 s over all pools together. Each pool has a coroutine for it that runs only while its shelf has work.
- **A look is kept.** A civilian's look is "worn" after three outings or ten minutes (`Looks.Worn`) and is then changed
  on the shelf through the slow path. Whoever came back longest ago goes out first, so with six (or eight) on the shelf
  the same face is not handed out twice running, and one character is never two people at once.
- **Dressed for a part, kept for it.** `ActorPool.Take` hands out somebody already dressed for the part; only when
  nobody is (more officers wanted than exist, say) is a look built on the spot, and then on one of the part there are
  most of on the shelf. The living city's pool goes further (`CityPeople.Thrifty`): when nobody on its shelf is dressed
  for a part, `Take` says no, as it does when nobody is free, and the look is made ready on the shelf (`Want`); the
  director tries that site again first on a later look (`_retry`). A site with two parts nobody has takes two or three
  tries, six seconds apart, out of view. People dressed for a job stay dressed for it, so the shelf comes to hold what
  the street's sites use. A test button builds on the spot (`AtNeed`).
- **Not changed:** the pools of fires and street events and of the civic crew (the same class, not thrifty: their
  scenes are few and dress their people as they are set up), and the casts whose people are somebody in particular
  (`FamilyCast`, `SocialCast`, `JobCast`, the hotel doorman): a relative's or a friend's look is that person, and is
  built when they are first put out. Every one of them is counted.
- **Counted.** Everything that builds a look calls `Looks.Count` first; the session record prints the totals.

Weighed and not taken: `AppearanceSetter.skipMeshMerging` (no merge at all; `ModLooks` uses it for the player's one
high-detail double). Every garment then stays a skinned renderer of its own, eight to eleven a character where there
is one now, drawn, culled and skinned in every frame the character is in view, for up to twenty-odd people; and the
pools' hiding of people out of view, the cuffs and the kit all work on the one merged renderer. That would trade a
hitch now and then for a cost in every frame.

### What cuts the navigation mesh, and when

A carving `NavMeshObstacle` makes the engine cut the piece of mesh it stands on again whenever it appears, goes or
moves. All of the mod's are `carveOnlyStationary`, placed while switched off and then switched on, so each is cut once.

- **Bus stops** (`BusStopDressing`, five boxes a stop, three stops dressed). In 0.1.0 the boxes cut the mesh whenever a
  stop was dressed, which driving through the city did every few seconds (dressed within 135 m, taken in beyond 175 m; 190 m and 230 m from 0.1.4).
  Now a dressed stop is a thing to look at until the player is on foot within 60 m of it (`Solid`, kept until 90 m, and
  never on the bus): only then are its boxes enabled. Nothing is cut while driving. What is lost: while a stop is not
  solid the game's pedestrians (who walk on this mesh) may pass through its pole and panels, which can be seen from a
  passing car. A stop's five boxes change in the same frame on purpose: the engine cuts the tile once for the lot, and
  one box a frame would have it cut five times. The self-check ("stop dressed", diagnostics only) is made when a stop
  becomes solid.
- **Carts** (`Core/SolidFootprint`: the player's own job cart, and the living city's coffee and hot-dog carts). One
  box on the cart object, its own length (1.5 m) and as deep as leaves the vendor's and the customer's places
  (0.68 m and 0.72 m from the cart's middle) on the mesh: half a depth of 0.68 less the walker's radius less 0.05 m,
  0.40 m at most. While it does not cut it is the obstacle walkers steer round that the carts had before (0.56 m
  deep). The living city's carts cut only while the player is on foot within 60 m (kept to 90 m), as the bus stops;
  the job's cart cuts from the moment it is put down, because the player is on foot beside it for as long as it
  stands. 1.6 s after a cut `Verify` asks the mesh: the cart's middle off it, both places on it, and a way on foot
  from one to the other. If a place was lost the cut is taken back for that placing and the cart is steered round
  only. One line: `Jobs check (cart solid): ...` or `City life check (cart solid): ...`. If the walker's radius is
  0.58 m or more there is no room for a box and the carts are steered round, not solid; the line says so.
- **Scenes** (`SceneObstacles`, eight vehicle boxes; the cordon's walls; a parked patrol car's box; a precinct's parked
  cars; the wedding's): put out once when the scene is set and taken in once when it ends, each group in one frame for
  the same reason. The cordon switches a wall off and on once more if its cut did not take. None is toggled on a timer.

### The session record

For reports of the kind "it stutters after a few minutes, or hours". `Diagnostics/SessionWatch` is a component on the
mod's root object that is disabled unless "Developer diagnostics" is on (`Meta.Dev.Apply` switches it), so it costs
nothing as shipped. On, it costs a dozen reads of numbers the engine and the game already keep and as many comparisons
a frame, about thirty reads of the clock for the step timing (`Diagnostics/FrameSteps`, under two microseconds), and,
once a minute, a walk over the mod's own objects. Nothing is allocated except for a line that is written.

One line for every minute of play (loading screens, pauses and time with the window in the background are left out):

```
Session 12 min: frames 5210, average 11.5 ms, worst 61 ms, over 33 ms: 4, over 50 ms: 1; managed heap 412 MB of 610 MB,
collections in the minute 3, grown by 31.0 MB in the minute (517 KB a second), the collector at work 212 ms in the minute
and 3.1 ms at most in a frame; engine memory 2950 MB; mod objects 345 (118 shown) under 6 roots; people 2 out, 12 made of
12; city people 3 out, 8 made of 8, city props made 14; looks built in the minute 1 (0 while playing), since the city
loaded 23 (2 while playing); frames over 80 ms: 2, of them 2 behind the game's black screen (into or out of a building)
and 0 seen; the mod's navigation obstacles changed in the minute 0; handlers put back on the game's events 0 (changed
events looked into 3); searches of the whole scene 0 (0 while playing); on foot in the street, game hour 9.
```

| part | what it is | what to look for over a session |
|---|---|---|
| frames, average, worst | the minute's frame times | the average is the frame rate; the worst is the minute's longest stall |
| over 33 ms, over 50 ms | frames longer than that in the minute | these are the stutters: how many a minute, and whether the number grows |
| managed heap A of B | `GC.GetTotalMemory(false)` and the size the heap has grown to (`Profiler.GetMonoHeapSizeLong`) | B only ever grows; if it grows minute after minute something allocates on a timer (the game's code and every mod share this heap) |
| collections in the minute | `GC.CollectionCount(0)` since the last line | more collections a minute at the same place and speed means more garbage a second |
| grown by N MB in the minute | the allocation budget: `GC.GetTotalMemory(false)` read every frame, and every rise added up (what a collection gives back in the same frame is not seen, so it is a little under what was allocated). The game's share and every mod's together | about 35 MB a minute in a busy city with the mod as it is now. A version of the mod that raises it by megabytes allocates on a timer somewhere |
| the collector at work, and at most in a frame | the engine's own `GC.Collect` time (a profiler counter this build offers), summed over the minute and its largest frame | the game's collector works in slices of 3 ms (`gc-max-time-slice=3` in boot.config): "at most" near 3 ms is the collector as intended; tens of milliseconds is a collection that could not be sliced |
| engine memory | `Profiler.GetTotalAllocatedMemoryLong`: meshes, textures, materials, audio | a steady climb between two midnights is normal for the game (it frees unused assets once a game day, at midnight); a climb across days is a leak |
| mod objects (shown) under N roots | every object under a root object named `RealLifeOverhaul...`, and how many of them are switched on | everything of the mod's is pooled: the count rises while each kind of scene is met for the first time and then stands still. If it climbs all session, something is made per use and never put away |
| people, city people, city props | the two pools of characters (12 and 8) and the living city's prop shelf | "made" stops at 12 and 8; "out" goes up and down |
| looks built (while playing) | characters' looks built, a mesh merge each (`People/Looks`): in the minute and since the city loaded; in brackets those built while the game was neither loading nor paused | about twenty at the load, none of them while playing. After that a few an hour is the pools changing worn looks, one at a time. Several "while playing" in one minute, minute after minute, means scenes are asking for looks nobody on the shelf has |
| frames over 80 ms: N, behind the game's black screen, seen | the long frames of the minute; "behind the black screen" are those with the game's fade up (`UI.UiFader.isFading`), which is its walk into or out of a building | every walk out of a building is a frame of about 225 ms and every walk in one of 85 to 630 ms, all the game's own and all behind its black screen (see below). "Seen" is the number that matters, and it should be 0 |
| the mod's navigation obstacles changed | carving obstacles of the mod's made, moved or switched in the minute (`Core/MeshCuts`) | a handful when a bus stop, a cart or a cordon comes or goes while the player is on foot near it; never a steady number minute after minute |
| handlers put back on the game's events | `Core/EventHold`: times a handler had to be put on one of the game's event fields, and times a changed field was looked into | a few after a load, then 0. Sixty a minute is a holder putting itself back every second |
| searches of the whole scene (while playing) | `Core/SceneFinds`: every `FindObjectsByType` and `Resources.FindObjectsOfTypeAll` the mod makes | each is a frame of about 110 ms in this game. "While playing" must be 0: what a search finds is found behind the loading screen (`ScenePrewarm`) |

One line for a frame longer than 80 ms (12 a minute at most; the rest are in the minute's count):

```
Session: a frame of 231 ms; none of the mod's large pieces of work was under way; where it went: coroutines and timers
204 ms, rendering 14 ms, the rest 13 ms; the collector: no collection (41 so far), at work 0.0 ms, heap 731 MB before and
733 MB after; the game: its screen was black or fading (its own walk into or out of a building: not seen as a stutter),
the player came out of a building in it, not saving, no scene loaded or unloaded; the mod's navigation obstacles changed
in the second before it and in it: 0; the mod's last pieces of work: "people: a character being made" (3.1 ms) 41.0 s
before, "transit: a bus stop being put out" (not timed) 80.2 s before; on foot in the street, game hour 7.
```

How to read it:

- **where it went** is the engine's own steps of that frame, timed by `Diagnostics/FrameSteps`. "navigation" is the step
  in which the engine cuts the walkable mesh: a long frame with its time there is a cut, asked for half a second
  earlier by a carving obstacle. "coroutines and timers" with the screen black is the game taking the player through a
  door. "scripts' Update" is some script's Update, the game's or a mod's: the mod's share is the time beside the pieces
  of work named "in it". "rendering" and "the rest" are the engine's.
- **the collector** says whether a collection finished between the record's two looks, how long the engine's collector
  worked in that frame, and the heap on both sides of it.
- **the game** says whether its black screen was up, whether the player had just gone through a door, whether it was
  saving (`SaveGameManager.SavingGameInProgress`) and whether a scene had just been loaded or unloaded.
- **the mod's last pieces of work** are the last three named with `SessionWatch.Mark`, with how long each took where the
  place that named it also called `SessionWatch.Done`, and when it was, seen from the long frame.

The test "Test: write the session record's lines now" (Options, Tests, under Performance) writes both lines at once, so
every part can be read without waiting for a stutter.

"The mod was at" is the last piece of work named with `SessionWatch.Mark("...")` in the three frames before. `Mark` is
a handful of writes into a fixed ring with a constant text and one read of the clock (the same cost with diagnostics on or off). It is called where the mod
does something large in one go: a character made or given a look (`ActorPool.Dress`, `CityPeople.Dress`, the casts), a
living-city site set up, a bus stop, a precinct front or a two-wheeler stand put out, an accident, a crime, a fire or a
street event starting, the game saving (the mod's own share of the save is timed and written as its own `Session:`
line), the performance tweaks listing the loaded materials. New code that builds something in one frame names it the
same way; `Mark` is never called per frame and never with a text put together for the call.

What the lines cannot say: which script a long frame was spent in when its time is under "scripts' Update" or
"coroutines and timers" and no work was named, and anything about the graphics card.

### Long frames: what was found, and the rules that follow

Three sessions of Player.log with the record on (83 frames over 80 ms) sort like this:

- **The game's own, behind its black screen: about three in four.** `BuildingManager.ExitFromBuildingCoroutine` fades to black,
  then calls `GlobalEvents.onExitBuilding`, on which every `CityMapHider` switches its part of the city back on in the
  one frame (`CityMapHider.OnExitBuilding`; on the way in the same work is spread over frames): 215 to 280 ms every
  time. `EnterBuildingCoroutine` loads the interior and its items under the same fade: 85 to 630 ms. Neither is the
  mod's and neither is seen. The record now counts them apart.
- **A search of the whole scene, in play: the mod's, about 110 ms each.** `Object.FindObjectsOfType` and its relations go
  through every object the engine has in memory. The traffic system's `CurrentSceneData.GetSceneInstance()` is such a
  search on every call, and five features called it: the line "Crime: road map built" was followed by a frame of 112 to
  117 ms in all three sessions, and every driving lesson and test began with one of 126 to 146 ms. **Rule: no search of
  the scene while the game is played.** `Core/RoadData` finds the road data once per city; `Core/SceneFinds` is the
  only place that searches, names and times each search and says FAIL in the log for one made in play;
  `ScenePrewarm` runs each feature's one-off search behind the loading screen (the game loads its mods after the city's
  scenes and before its loading screen goes: `UI.Load.LoadingScreen.LoadAsyncScenes`).
- **The collector: not a cause of a frame that is seen.** Four of those frames had a collection finish in them, all
  four at a door (the game makes and destroys a whole interior there, and a collection that cannot keep up is finished
  in one go: it made those frames an estimated 40 to 100 ms longer, behind the black screen). No long frame outside a
  door had one: in ordinary play the collector works in slices of 3 ms, and the probe measured 3.09 ms as its longest
  share of a frame.
- **Cuts of the navigation mesh: not shown to be a cause.** Every long frame within 2.5 s of a cut asked for by the mod
  was a walk through a door, or the frame the cordon itself was set out in. The step timing now proves or refutes it
  per frame ("navigation"). The rules stand: a carving obstacle is moved only while switched off, everything of one
  scene changes in one frame, and every change is noted in `Core/MeshCuts`.
- **Handlers on the game's events.** `EventHold.Keep` with several holders on one event had each put itself back every
  second for ever, because each one's putting back changed the event for the others (`Core/EventHold.cs` says how it is
  kept now). It was a few kilobytes a second, not a long frame.

## Checks

`dotnet run --project RealLifeOverhaul.Build/CatalogueCheck -c Release`
fails if an event id is duplicated, a locale key is missing, or the no-repeat deck repeats early.
If Windows blocks the unsigned check executable ("An Application Control policy has blocked this
file"), build it and run the DLL through the dotnet host instead:
`dotnet build RealLifeOverhaul.Build/CatalogueCheck -c Release`, then
`dotnet RealLifeOverhaul.Build/CatalogueCheck/bin/Release/net10.0/CatalogueCheck.dll`.
Run it after any change to the event catalogue or `en.json`.

Three more check programs compile the mod's own game-free files and run them on made-up data: `PlannerCheck` (see
"The Planner"), `NavCheck` (see "Minimap and GPS"; it also covers `Core/RouteSearch.cs`, which the bus network uses,
and in `HitchCheck.cs` the keeper of handlers on the game's events, `Core/EventHold.cs`, and the session record's
rings, `Core/WorkRings.cs`) and `EconomyCheck` (see "Economy"). Each ends with `ALL CHECKS PASSED`.

## Adding a crime

1. **Catalogue entry** in `Scripts/Crime/CrimeCatalogue.cs`: an id, its family, where it can happen
   (`Place`), what it needs (`Needs`: night, traffic, patrol cars...), how many people it must be able
   to take from the pool (`Cast`), its `Weight` in the deal, what a 911 call about it is called
   (`Report`), and its lines: `Starts`, `Near` (the player walks up), `Extra`, and one `CrimeEnding`
   per way it can end, with odds. An ending with odds 0 is never rolled; a scene reaches it by what
   happens in it. Every key is written out in full and added at the end of `Locales/en.json`.
2. **Scene**: either one more line in `Scripts/Crime/CrimeScenes.cs` reusing an existing class (a lone
   offender on the pavement is `new LoneOffenderScene("my_id", OffenderStyle.Pitch)`: its endings are
   picked from the ones that class knows, listed at the top of the file), or a new class in
   `Scripts/Crime/Scenes/` deriving from `CrimeScene`:
   - `Id` returns the catalogue id;
   - `Stage` puts the cast out through the four `ActorRole`s (`First`, `Second`, `Third`, `Cop`) and
     sets the first step with `Go(step, now, seconds)`; return false if it cannot be staged here;
   - `Run` is a switch over the scene's own steps, called four times a second. Ask `Is("ending")`
     for what was rolled, `Conclude()` to announce it, `Arrest(role, now)` to hand someone to the
     officer (cuffs, kerb, patrol car and driving away follow by themselves), `RunFor(role, from)`
     when somebody runs for it (the player can then stop them by getting within reach: the base
     class plays the citizen's arrest, the wait for an officer and the thanks), `Leave(now, player)`
     to wind down;
   - `Release` gives back anything that is not one of the four roles or the two shared cars.
   The base class ends the scene after two minutes, when the player leaves, and on every fault.
3. `dotnet run --project RealLifeOverhaul.Build/CatalogueCheck -c Release` checks that every entry has
   a scene, every line exists, every ending a scene names is in the catalogue, and the deal never
   repeats early.

Fines, the odds of being seen and custody hours are constants in `CrimeLaw` at the top of the catalogue.

## Adding an activity to the living city

Somebody working alone on the pavement is **one catalogue entry and nothing else**. In
`Scripts/CityLife/CityCatalogue.cs`:

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

plus its name at the end of `Locales/en.json` (written out in full). The clip brings its tool; `Carry` puts a second
tool in the other hand, `Greet` is a one-off played when someone on foot comes by in front, `Now` a one-off every
so often, `UsesGamePose` one of the game's own poses instead. If it needs a new motion, that is one clip
(`CityAssets/README.md`, "Add a clip") and, for a new tool, one prop (`PropAssets/README.md`, "Add a prop").

More than one person, or anything a table cannot say, is a class in `Scripts/CityLife/Activities/` deriving from
`CityActivity` (copy `ChatActivity`, 70 lines) and an entry that names it:

- `Find(site)` looks for a place for every person and prop near `site.Anchor` with `CitySpots` (`Stand` = on the
  pavement with room, `Shopfront` = at a measured distance from a real wall, `Along` = which way the pavement
  runs) and remembers them. It puts nothing on the street, and returns false if anything has no room.
- `Begin(site)` calls `Start(site)`, then `Add(look, spot, face)` for each person, `Put(prop, spot, heading)` for
  each prop and `person.Work(clip, Props)` for what they do. If any of it fails it calls `End()` and returns false.
- `Tick(now)`, once a second, changes clips on timers and returns `Lasting()`.
- `End()` is inherited: everyone and everything goes back.

- **People who come and go** are never put down in view. `Arrive(site, look, goal)` takes somebody from the pool,
  puts them on the pavement out of view 24 to 46 m away and sets them walking to `goal` (null, with the reason in
  `site.Refusal`, if nobody is free or there is no way in); test `person.Arrived && person.Near(goal, 0.5f)` in
  `Tick`, then `person.Stand()` and `person.Face(point)` (a turn over a third of a second). `Depart(person)` walks
  them off to pavement out of view, and `TickDepartures()` at the top of `Tick` takes them back into the pool once
  they are there.
- **Carrying while walking** is a masked clip over the game's own walk: `person.WorkOver("carry_box_hold", Props)`
  (arms), `"cup_hold"` with `BodyMask.RightArm`, `"umbrella_hold"`; `person.Bring(prop, null, position, rotation)`
  for what is pushed in front (pushchair, sack truck) or worn on a bone (satchel). Nothing of the legs is ours, so
  nothing slides.
- **The instant something changes hands** is asked for with `CityLifeDirector.After(clip.Mark("release"), this,
  what, person)` and arrives in `Moment(what, who, now)`: there `person.Release(true)` gives the prop back to the
  activity (stand it where it is and add it to `Dressing`), or `person.Take(prop, clip, Props)` puts a prop that is
  already out into a hand. A clip whose tool is picked up part of the way through is started with `tool: false`.
- An activity that turns a place down for a reason of its own says so: `site.Refusal = CityRefusal.NoParkedCar`.

The director does the rest: when and where, out of view, never the same thing twice running, taken away out of
view, the test button (each press of a family's button puts its next activity just ahead of the player, and a
press that starts nothing says which activity and exactly why: one sentence per `CityRefusal`). In the rain
everything is taken in except entries marked `Rain`; `RainOnly` entries (people under umbrellas) start only then.
### Clips nothing played

`not-in-repo/tools/medium/clip_users.py` lists every clip the bundle's builder declares (`CityAssets/scripts/clips*.py`,
160) and the ones no script names. There were 25. Six now have a home in the living city, each where the clip's own
note says it belongs:

| clip | where | how |
|---|---|---|
| `sit_chat_a`, `sit_chat_b` | new activity `bench_chat` (`BenchChatActivity`, Leisure, two people, 9:00 to 21:00) | a `precinct_bench` with its back to a wall (`CitySpots.Shopfront`, 0.45 m off it, pavement under both ends and in front); the two put exactly on the seat 0.55 m apart, as the chess players are on their stools; the talker on the right, since `_a` is turned to its left |
| `eat_standing` (with `takeaway_box`) | new entry `street_lunch` (Everyday, 11:00 to 15:00) | `SoloSpec.HoldNow`: the one-off is played, its last pose (the carton on the palm) is held, and it is played again every 4 to 8 s |
| `drink_stand` (with `mug`) | new entry `mug_break` (Everyday, 7:00 to 17:00) | the same, every 7 to 14 s |
| `phone_answer` | the existing `phone_call` | `SoloSpec.Ring`: after 14 to 26 s the call ends (the game's pose and phone go), 5 to 9 s later the clip takes the phone to the ear, its phone is put away and the game's `TalkingPhone` takes over, as the clip's note asks |
| `point_show` | the existing `doorman` | `Greet`: somebody on foot in front of him is shown the way in |

`HoldNow` and `Ring` are two fields of `SoloSpec`, so another such person is still one catalogue entry. The budget is
the director's and is unchanged (three sites, eight people); the Guide's count is `CityCatalogue.All.Length` (33 now);
each start writes the director's own line, `City life: <id> started N m away; ...`. A clip or prop missing from the
bundle leaves `bench_chat`, `street_lunch` and `mug_break` unstarted and the other two as they were.

A second pass wired eight more, each where its clip's own note says it belongs:

| clip | where | how it starts, how it ends, what it falls back to |
|---|---|---|
| `carry_suitcase_walk` (with `suitcase`) | new entry `traveller` (Leisure, 6:00 to 23:00), `StrollActivity` with `StrollKind.Suitcase`: three or four stretches of pavement, 16 to 30 m each | the clip is a whole-body walk made for 1.3 m/s, so it is played only while the agent is really moving and its speed is set from the agent's (`CityPerson.KeepStep`, `ActivityAnimator.MatchSpeed`, as the crews are kept in step) once a second on the director's tick; held up (under 0.2 m/s, two seconds after setting off) they go back to the game's own walk with the case in one hand. No landmark is known to the catalogue, so it is placed like the pushchair. Without the clip or the case the entry does not start |
| `cheer_once` | the listener at the busker (`BuskerJob`) | the song just played was good (the quality of the "good" toast); when the listener is about to leave, instead of leaving at once they cheer once (the game's pedestrian, the mod's clip, 1.8 s), and the clip is taken off with `ActivityAnimators.Forget` before they are handed back, on every way (the pedestrian is pooled). If the clip cannot be played they leave as before |
| `cheer_once` | an onlooker at the street chess table (`ChessCorner`) | a third person from the pool, to the side of the table (`watch_nod`), only where the pool has three free after the opponent; when the player wins (7 of 10) they cheer once and go back to watching on the corner's own second tick. Taken in with the table |
| `hold_baby` (with `baby_bundle`) | the partner at home (`HomeLife`), with a baby under a year old (`FamilyBalance.BabyUntil`, the age the cot shows its swaddled baby to) | one of the ways they stand at a place: every other day at the living-room places, always at a clear-floor place. The cot's bundle is hidden while they hold it and lies in the cot again when they put it down (`HomeLife.Cradle`), walk, are spoken to, or leave. A child of one or two is not held: there is no toddler shape, and a held empty bundle would be wrong |
| `rock_cot` | the partner at home, a place of its own beside the cot (`CotPlace`, found when the cot is put down) | the feet 0.68 m to the cot's side (the clip's figure), within 8 cm of the mesh; the partner is brought to it and then set on it exactly (`NavMeshAgent.Warp`, a few centimetres), facing the cot. **The cot does not move with the push** (the clip says it should; moving it needs a per-frame step the mod does not take), so the hands go 2.8 cm either side of the rail |
| `slow_dance_a`, `_b` | the wedding, after the photograph (`WeddingScene`, `Step.Dance`) | the partner takes the step to 0.45 m (`CityPerson.StepTo`), the gap really reached is measured and the pair is played only within 0.10 m of it, both in the same call, for 8 s; both are released on every way out (`Abandon`, `End`). If the gap is not reached, or either clip is missing, the ceremony goes on to the reception as before |

Eleven stay unplayed. Each was looked at for a place among the features that exist:

| clip | why not |
|---|---|
| `strap_hang` | made for standing in a moving bus: there is no bus interior |
| `bonnet_check` | needs a car with its bonnet open: the game's cars do not open theirs, and no scene is a breakdown |
| `refuel_hold` | emptying a `jerry_can` into a filler: the mod has no fuel and no scene of running out |
| `oath_hand` | the raised hand of an oath: a hearing is pages in a list window and nobody is played in it |
| `podium_speak` | needs a lectern 1.10 m up: none is modelled, and a rally's speaker has a placard |
| `placard_hold_arm` | not needed by design: the code masks `placard_hold` itself to the right arm |
| `throw_ball` | a free throw: no ball and no hoop are modelled |
| `eat_sit` | seated at a table 0.75 m high whose edge is 0.20 m ahead: no seat in the game or the mod has a table at a known place in front of it |
| `box_lift_with`, `_b` | two people lifting a box 1.20 m long: no such prop (the move's boxes are 0.45 m and carried one each) |
| `collector_lean` | the collector of "Business pressure" would lean on a counter 0.975 m up whose near edge is 0.32 to 0.36 m ahead, the player 1.1 m beyond it. A visit is played on open floor 1.7 m from the player and the game's shop furniture gives no measured counter (`BusinessVisit` says so); played anywhere else the clip reads as a man bent forward with a hand in the air. Left listed; should a shop ever offer a measured counter it is one line in `BusinessVisit` |

Not checked in the game: any of it. A screenshot of the bench with the two on it settles whether they sit on the
seat and not in it (the seat is 0.46 m, the clips are made for 0.45) and whether the bench stands clear of the wall.
For the second pass: the hands on the rack's rail and on the cot's rail, the baby in the partner's arm, the traveller's
feet on the pavement, the clasped hands of the first dance. The log (developer diagnostics on) says the first time
each clip is played, `Clips: the '<clip>' clip is played by <who> (said the first time only).`, and the dance says the gap it measured.

## Jobs

The game has its own work: employer jobs (stand at a station in someone's shop while time is skipped,
paid by the hour), the delivery van (a company van, a hand truck, boxes of stock to three shops, 100 a
drop) and food delivery (accepted at a restaurant's till, 25 plus 0.05 a metre). The jobs here sit beside
those and never touch them: a job cannot start while one of the game's missions is on, and ends if one
is taken. They are found on the phone (the Gig Line) and at the help-wanted boards.

| job | needs | a unit is | pays | a day |
|---|---|---|---|---|
| Flyers | nothing | a flyer taken | 7, +40 % on a busy street, +10 % for the whole bundle | 48 (two bundles of 24) |
| Cleaner | a business near that is open | an eighth of the client's floor cleaned (the game's own dirt, to 100 %) | 20, by the share of the dirt taken off | 24 (three floors of 8) |
| Shelf stocker | a shop near that is open | a box carried in from the kerb and shelved | 33, +10 % for the whole delivery | 18 (three deliveries of 6) |
| Drinks cart | a pavement with room for the cart | a customer served the drink they asked for | 18, tips of 2.75 to 9.50 for quick, +10 % if nobody walked off | 36 (three shifts of 12) |
| Busker | a pavement | a song | tips only: about 17 a song, times 0.5 to 1.5 for how many are about, 0.4 to 1.4 for the hour, 0.85 to 1.25 for the song, by how it was played; 2.75 to 8.25 from each listener | 16 (two sets of 8) |
| Courier | a car (own, or a van hired for 125 a day) | a parcel delivered | 50 + 0.085 a metre of the round, +15 % for beating the clock, tips of 5.50 to 16.50 | 15 (three rounds of 5) |
| Ride-hail | a car (own or hired), a reference (level 2 in any job), no police stop on a job for 2 days | a fare | 22 + 0.195 a metre, tip up to 30 % by comfort | 14 |

Every figure is in `JobCatalogue`, set against what the game itself pays (read from its data files by
`JobAssets/scripts/extract_wages.py`: the one employer job, cashier, is 25 an hour; the game's own staff
start at 12 an hour for cleaning, 16 for customer service, 18 for delivery driving; the van job is 100 a
drop). Pay is multiplied by 1 + 0.08 per level above the first (a level every 40 flyers, 30 patches, 20
boxes, 30 customers, 20 songs, 15 parcels, 12 fares; four levels) and by the "Job pay" option. Deductions only ever reduce what
a job pays; a slip never goes below zero. Three failed shifts in a job and it lets the player go for three
days; a good shift takes a strike off. Work spends energy and trains a skill through the game's own
systems, and never takes energy below 12.

### Pay against the game's

What the game pays and charges, read from its data files (`JobAssets/scripts/extract_wages.py`) and its code, and
what the gigs were set against. One game hour is one minute of play; a gig shift is four game hours (the hours an
employer pays basic wage for).

| in the game | figure | where |
|---|---|---|
| the player's own job: cashier at El Gato Food Market | 25 an hour, 8 to 16, six days: 100 for a four-hour shift | `PlayerActivity/WorkActivity` pays `floor(hours x hourlySalary)` |
| its staff at no skill | cleaning 12 an hour, customer service 16, delivery driver 18 | `SkillData.baseHourlyWage` |
| its staff with skill | those times `1 + skill^1.05 / 100`: 19 to 29 at skill 50 | `EmployeeHelper.CalculateHourlyWageForSkill` |
| the delivery van job | 100 a drop (150 at one depot), three drops, about three game hours: about 100 an hour | `Vehicles.DeliveryDriverJob` |
| food delivery | 25 + 0.05 a metre within 500 m | `Player.FoodDeliveryJob` |
| a taxi ride (the player pays) | 0.15 a metre: 90 for 600 m | `TaxiSystem.GetPrice` |

The first release's gigs paid 14 to 22 an hour on foot (under the cashier's 25) and 30 to 55 with a car, which
is what the owner found far too little against 48,500 in hand and 187 a day in costs. The new table raises every
gig's unit pay by about 2.8 (unskilled 2.75 to 2.9, courier and ride-hail 2.8, the two desk shifts 3.0), which
puts a shift played well at 40 to 55 an hour on foot (1.6 to 2.2 times the cashier) and about 95 to 105 an hour of
play with a car (the van job's 100). Skilled work (courier, ride-hail, the desk shifts) pays more an hour than
unskilled. Nothing is said to cost more energy or to level faster.

| gig | unit pay before | unit pay now | whole shift before | whole shift now | an hour of the shift now |
|---|---|---|---|---|---|
| Flyers (24) | 2.50 | 7 | 60 | 168 | 42 |
| Cleaner (8) | 7 | 20 | 56 | 160 | 40 |
| Shelf stocker (6) | 12 | 33 | 72 | 198 | 50 |
| Drinks cart (12) | 6.50 | 18 | 78 | 216 | 54 (and tips) |
| Busker (8 songs, tips) | 6 | 17 | 4 to 16 a song | 11 to 45 a song | by the street and the hour |
| Courier (5) | 18 + 0.03 a metre | 50 + 0.085 a metre | 110 to 130 | 310 to 340 | 78 to 85 (about 100 of play) |
| Ride-hail (a 600 m fare) | 8 + 0.07 a metre = 50 | 22 + 0.195 a metre = 139 | 50 a fare | 139 a fare | about 100 of play |
| Dispatch desk (3 plans) | 14 a plan | 42 a plan | up to 42 | up to 126 | plus the rank's salary |
| Shift in charge (4 calls) | 12 a call | 36 a call | up to 48 | up to 144 | plus the rank's salary |

Everything else that is money moved with it, so no ratio changed: the tips in the courier's, cart's and busker's
code; the employers' basic wage by rank (gig ranks 4 to 9 an hour became 11 to 25, the salaried desk and lead
ranks 18 to 30 became 36 to 60), the raise step (1 to 3), the cart rent (60 to 165) and the cart's takings and
stock (11 and 3.5 to 30 and 9.5), the booked set's fee (45 to 125), the hired van's daily fee (45 to 125) and what a
review counts a tip for (a point a dollar to a point for 3, twenty at most). The 85 % rate of the game's clock
(`ShiftRates.Share`), the crash and police-stop cuts, the level bonus (8 % a level) and the "Job pay" option are
shares or factors and needed no change. Every figure the Guide quotes (`job_table`, `job_van_fee`, and the pitches'
`{pay}`, `{km}` and `{shift}`) is read from these constants.

### No two shifts alike (`Jobs/GigOffers`, `Jobs/GigWords`)

Everything that makes one shift of a gig differ from another is drawn in one place, `GigOffers.Draw`, once, from a
seed made of the day, the hour and the gig (`Mix`, and a small xorshift `GigDice` that is the same on every machine).
Asking again in the same hour gives the same offer, so the Gig Line's quote is the shift that is worked. The jobs roll
none of this themselves; they read `JobContext.Offer`. The Gig Line (`JobPhone.Offer`, also read out at the boards) says
the fee, the size and the reasons in one sentence; the first line of the shift's panel (`ShiftFigures`) repeats the
reasons ("Gig Line, a generous client, a busy day"); each event is a notification when it happens. Employers' shifts,
booked sets and the test buttons are flat: `GigOffer.Table`, the table's figures, none of this.

| what | range | average | shown |
|---|---|---|---|
| the client | tight 80 to 95 % of the fee (40 % of offers), fair 95 to 107.5 % (40 %), generous 107.5 to 137.5 % (20 %) | exactly 100 % | the quote, the panel |
| the day (which gigs are busy) | each day 2 or 3 of the 7 gigs are busy and pay 120 %; about one day in two one gig has nothing on and cannot be taken ("Nothing in that today"); every other offered shift pays 87.5 % | busy 2.5 and shut 0.5 a day; the fee over offered shifts exactly 100 % | the quote, the panel |
| the size | flyers 18 to 30, cleaning 6 to 10 units of floor, stocker 4 to 8 boxes, cart 9 to 15 customers, busker 6 to 10 songs, courier 3 to 7 parcels (ride-hail has no set size) | the table's number | the pitch, the panel |
| the street (flyers, cart, busker) | quiet 70 %, normal 100 %, rush 130 % (25 %, 50 %, 25 %): how many take a flyer, how soon customers come, what the hat holds | exactly 100 % | the panel |
| a big tipper (cart, busker, courier, ride-hail, shelf stocker) | 25 % of shifts, at one unit of the shift, 1 to 2 units of pay (a share of the fare for ride-hail) | 0.375 units a shift | a notification, the slip's tips |
| a speed bonus (flyers, cleaning, shelves) | 30 % of shifts, 0.5 to 1.5 units of pay if the work is done by hand in 60 % of the time allowed | 0.3 units a shift, taken as won | a notification at the start and at the end, the slip's bonus |
| a difficult customer (cart, cleaning) | 20 % of shifts: the cart's customer sends the first right drink back (3 seconds off their patience, a new order); the client finds a patch missed and 30 % of the dirtied cells are dirty again at 80 % of their dirt | none in money; the pay follows the floor | a notification |
| weather pay (outdoor gigs, weather option on) | rain +15 %, storm +25 %, heat wave or cold snap +10 %, ice +20 % | extra, only in bad weather | the quote, the panel |

Fair means: for a shift played well in fair weather the pay averages the table's figure. The client's factor, the day's
factor and the street's factor each average exactly 1, the size is symmetrical round the table's number, and the money
the events are expected to add (a tip's and a speed bonus' chances times the middle of their ranges, as a share of the
shift) is taken off every fee (`GigOffers.Calibration`, 0.94 to 1 by gig), so adding them back gives 1. That is why the
quoted fee of a gig with events averages a little under 100 % (stocker 90 %). Weather pay is not taken off: it is a
bonus for bad weather. The pay slip is the game's notification as before; tips and the speed bonus are on its lines.

Checked offline by `CatalogueCheck` (`GigCheck.cs`), which draws about 5,500 seeded offers for each gig: the same
day and hour give the same offer; the ranges above are kept; the client, day and crowd factors average 1 within 2 %;
the size averages the table's within 3 %; a whole shift, events included, averages the table's within 2.5 %
(measured 0.993 to 1.002 of the table, 0.998 over all gigs); the day's board has 2 or 3 busy and at most 1 shut gig a
day and averages 2.49 and 0.51 over 3,000 days; a career shift gets the flat table; weather pay applies out of doors
only. It also checks the pay table: every gig on foot pays at least 1.5 times the game's cashier an hour (the busker,
whose pay is tips, at least the cashier's), courier and ride-hail more than the best unskilled gig and no more than
the van job's 100 an hour, both desk units more than any unskilled gig's, and the own cart nets about the cart gig's pay.

Log lines with "Developer diagnostics" on: `Jobs: offer for 'cleaner' (day 14, hour 9, seed ...): Generous client x1.2 ...`
when a gig starts; `Jobs 'x' event: a big tipper at unit ...`, `... the client adds ... if the shift is done within ...`,
`... speed bonus kept ...`, `Jobs: vendor event: a difficult customer sends the drink back ...`, `Jobs: cleaner event:
a difficult client points out a missed patch ...`.

### The cleaner works on the game's own dirt (`Kinds/CleanerJob`, `JobDirt`, `JobMop`)

How the game cleans: every interior has a floor of `DirtSpotObject`s, each reading its dirtiness (0 to 100) from its
place in `BuildingRegistration.dirtSpots`; `BuildingCleanliness.GetCleanliness()` is 100 minus the mean dirtiness
minus the mean over spots of 5 or more (100 when none shows). The player takes a mop from a cleaning station
(`CleaningStationController.OnCleaningStationClick`: `PlayerHelper.ItemInstanceInHands = ItemHelper.InitializeNewInstance("ba:itemname_mop")`),
which shows the dirt highlighting (`MopController.AssignToPlayer`, `BuildingCleanlinessHelper.ShowDirtinessHighlighting`);
clicking a dirt spot (`DirtSpotObject.Interact`, `BuildingManager.onFloorCellClick`) starts `MopController.FloorCellClick`,
which cleans the clicked cell and those round it at 25 dirt a second shared among them, with the game's cleaning stance
and a minimum of 0.2 s, and ends with the item panel's bar refreshed. The game's tasks list stops flagging dirty floors at
90 %; the job asks 100 %, which is the game's own "no dirt shows" figure.

In a business that is not the player's the objects exist but `dirtSpots` is empty or missing (`BuildingHelper.ResetRegistrationOnRent`
makes it only for the player's), the click is hooked only in the player's own business, the item panel reads 100 % and the
station refuses anyone but the owner. So `JobDirt` makes its own list for the client's floor (the same cells and order),
puts dirt in it with `BuildingCleanliness.SetDirtiness` (patches, not single cells: some centres at random and the cells
nearest them, at most six cells a unit, 55 plus 4 a unit above eight dirt each, 75 to 125 % of that), and hangs it on the
client's registration while the player is inside; the game's floor objects, its mop and its percentage then work as in the
player's shop. It is put back whenever the player leaves, when the job ends and while a save is written
(`SaveHook`; the save is made on the main thread right after the event), so no job dirt is ever saved, and a business the
player rents is refused outright. `JobMop` gives and takes the mop with the game's own calls, hooks the mop's
click method to the floor click event for the length of the mop, and sets the item panel's bar to the real figure.
If the game's dirt cannot be had (no floor cells, no floor object under the player, any exception), one line says why
and the job uses the mod's own spills as before (`CleanerJob.Spills.cs`). Pay is the fee (units times the unit's pay) times
the share of the dirt taken off, the whole fee at 100 %, half or more is a shift done; the clock cleans the rest at 85 %
and sets that share of the dirt to nothing through the game's call. Log lines: `Jobs: cleaner: the game's own dirt is
down at ...`, `Jobs: cleaner: the floor reads 62% clean (was 55%) ...`, `Jobs: cleaner: pay slip: ...`.

### The drinks cart: the model of a shift in the game's own interface (`Kinds/VendorJob`)

The cart shift (an employee's rota shift at Uncle Lou's, or the gig) shows nothing of the mod's own. `Job.NativeUi`
is true for it, and the director then reads no key for it. What the
player sees and does is built from four shared pieces of `Scripts/Jobs`, each usable by any other job:

- **`JobStand`: the place is taken by walking on to it.** `Place(post, facing, reach, idleClip)` names a zone;
  the job calls `Tick` on its own tick. A player on foot inside the zone is held (`JobBody.Hold`, the game's
  navigation blocker), drawn the last step to the exact place and turned (a coroutine of the director's, per
  frame for the half second it takes and never otherwise), and given the looping clip as the working pose
  (`counter_wipe`, the living city's vendor's; the game's own standing if the bundle lacks it). A one-off motion
  goes through `Busy`, after which the pose comes back by itself. `StepAway` frees them and the zone will not
  take them again until they have left it by 0.6 m. The director calls `Clear` on every way a job can stop.
- **`JobUniform`: work clothes over the player's own.** The game dresses the player for a while with
  `AppearanceSetter.UpdateVisuals(list)` (its swimwear, the uniform at its own tills): the pieces of the list are
  drawn where there is one and the character's own data is not written. `Wear(player, CityLook.Vendor)` builds
  that list from the living city's outfit (`CityOutfits.Wear` into a list of the caller's own: white shirt with
  red trim, charcoal trousers, red hat) and calls it; `Off` puts back what was recoloured and calls
  `UpdateVisuals()`. Nothing reaches the save: a save made in uniform holds the player's own clothes and loads
  the same with or without the mod. Where the player is drawn by the detailed double (`Character/PlayerDouble`),
  the double is dressed through `PlayerDouble.Workwear`, a delegate it calls with the copy of the data it is about
  to dress from. The director calls `Off` in `EndJob` (every ending, a fault and the feature switched off
  included) and in `OnDestroy`. Each change is one rebuild of the player's look, counted in `Looks`.
- **`ShiftSkip`: the rest of the shift by the game's clock.** The game's employer job is
  `PlayerActivity.WorkActivity` on `PlayerActivityUI`: Start and Cancel with the game paused; started, the player
  is kept at the till, the panel shows a progress bar with the time remaining, the game's skip button on its
  special key (it runs `TimeMachine.StartTimeMachine` to the end of the shift) and Stop; `Finish` pays the hourly
  wage for the minutes worked. `ShiftSkip` is an `IPlayerActivity` on the same panel with the same buttons and
  keys, for a job that implements `ISkippableShift` (how many minutes are left, what the offer says, what to do
  when the clock starts, as minutes pass and when it ends). No walk order is given (the player is already held at
  the stand), so the panel's cancel-on-walk-order trap (see Home) cannot fire; the activity is "on its way" for
  one frame because that is the step in which the game's panel lets the clock run. While `Job.PassingTime` is
  true the director does not take the running time machine for an interruption. Differences from the game's job:
  the game's fast-forward switch is offered by the game only indoors, so on the street the panel has the skip
  button alone; and no customers are shown while the clock runs.
- **`ShiftCard`: the figures, for the whole shift.** A copy of `PlayerHUD/CurrentJobUI/Panel` made asleep
  (`UiClone`), its two buttons and every raycast target taken off, three more lines copied from its own, a canvas
  of its own, 336 high. It hangs on the game's `CurrentJobUI`, in the game's place for it, and under the game's own
  panel while the player also has the game's job. `Title` and `Line(n, words, colour)` write only on change.

For the cart: the rule of the clock is `VendorJob.MinutesEach` (20 game minutes a customer: twelve are the four
hours an employer pays basic wage for) and `VendorJob.SkipShare` (85 % of a customer's pay, no tips, no "nobody
walked off" bonus). Units, energy, the level, the employer's hours, lateness, marks and the review are settled by
the same code as a shift by hand (`Ctx.Pay`, `CareerDirector.ShiftEnded`); tips count towards a review, so a
shift left to the clock scores lower. The order and the three drinks are the game's panel over a place
(`GameUi.WorldPanel`, now with four places: the game's interact, secondary, special and confirm keys) over the
cart: three buttons, one per drink, because the game writes each button's key itself, runs it on the key or a
click, and the three keys are the three the game sends to that panel. Calling out is on the confirm key in both
states of the panel; "Let time pass" and "Step away" are on the special and secondary keys while nobody is
ordering, and do nothing for 0.7 s after an order ends (a key meant for a drink). The pay slip is one
notification (`reallife_job_slip_note`, or `reallife_career_slip_note` with the basic wage).

**Calling out** (`VendorJob.Hawk`). Each of the game's pedestrians within 14 m (six at most) rolls once: annoyed
with 0.10 + 0.13 for each call in the last 25 s; else tempted with 0.32 x the hour (0.5 at night, 0.8 before nine,
1.2 from eleven to two, 1.3 from four to nine) x the pavement (0.8 to 1.2 by how many are within 30 m) x the
player's customer service skill (1 to 1.5) x the queue (0.6 with one at the cart, 0.3 with two), less a fifth for
each recent call. Tempted: the game's green face, up to two are borrowed (`PedestrianTakeover`, as the busker's
listener) and look at the cart for four to seven seconds, and a customer more is added to the shift (four at most,
never past a gig's daily amount) who arrives within a couple of seconds. Annoyed: the red face and
`JobClip.Annoyed`, and they are not asked again this shift. Ten seconds between calls. With three recent calls
and police presence above zero, a warning by chance (0.12 + 0.03 x the presence option); the second ends calling
out for the shift. No officer is put on the street for it. The player's motion is `JobClip.Hail` over the working
pose; no sound is played (the game's sound list was not read for one that fits).

**The cart is solid** (`Core/SolidFootprint`, shared with the living city's two carts): see "What cuts the
navigation mesh".

Log lines with "Developer diagnostics" on: `Jobs: vendor cart put down ...`, `Jobs check (cart solid): ... PASS`,
`Jobs: stand entered ...`, `Jobs: uniform on (Vendor, ...; worn before: ...)`, `Jobs check (cart panel): ... PASS`,
`Jobs: vendor: customer served by hand (...) in 2.8 s, tip 3 ...`, `Jobs: vendor: called out. Chances: ...`,
`Jobs: vendor: the rest of the shift is left to the clock: ...`, `Jobs: vendor: shift skipped by the clock: 3.0 game
hours ..., pay ..., energy ... -> ...`, `Jobs: stand left (...)`, `Jobs: uniform off ...`,
`Jobs: pay slip shown as one of the game's notifications ...` or `Careers: payslip shown as ...`.

Not verified (the game could not be run): everything that is seen. In particular how the copied job panel lays
out with five lines, where the game's panel over the cart stands on screen with four buttons, whether the hat and
shirt exist under the same names in the detailed character set (if not, the double keeps the player's own piece
there and the outfit's log line says so), and that the walker's radius leaves room for a cut (the check line
says).

**The other jobs on the same pieces.** Every job is now in the game's own interface: `NativeUi` is gone (all jobs are),
and with it the mod's own objective plate, status text, number-key choices, key prompt and pay slip. Shared by all:
`ShiftFigures` (the card laid out as the cart lays it out, each line written only when a figure changes), `JobPanel`
(the game's panel over a place: one or two buttons by locale key, or the job's own four), `ShiftClock` (the offer,
texts, rate, pay slip and log of "Let time pass" for the jobs other than the cart) and `ShiftRates` (the 85% and the
game minutes a unit: four game hours over the units of the shift; the cart's twenty minutes is the same sum).
`WorldPanel` gained `Follow`/`FollowLift` so that the box stays on a person who walks.

- **Flyers.** Canvasser uniform; the panel follows the passer-by in reach with "Offer a flyer", and stands over the
  player with "Let time pass" when nobody is in reach (10 game minutes a flyer, 85%, no busy bonus, no bundle bonus).
- **Cleaner.** Cleaner uniform; "Go in" over the door, "Mop it up" and "Let time pass" over each spill in reach
  (30 game minutes a spill, 85%).
- **Shelf stocker.** No uniform (the game has no shop assistant's outfit); "Pick up" and "Let time pass" over the pile,
  "Go in" over the door, "Put on the shelf" over the shelf (40 game minutes a box, 85%).
- **Busker.** `JobStand` on the pitch (the guitar pose when the clip bundle has the strumming clip); the three songs, "Strum"
  and "More" (which has "Step away" and "Let time pass") are the panel's buttons; 30 game minutes a song at 85% of
  `BuskerJob.AverageTip`, the song's own tip formula for the crowd and hour now, the middle song and
  `AveragePlay` of the strums in time; no uniform.
- **Courier.** Card and panel buttons (collect, load, take, hand over, leave). No clock: the car would have to be moved.
- **Ride-hail.** Card only; nothing in it is done on foot with a key. No clock.
- **Dispatch desk and shift in charge.** Card and panel buttons for the choices (a button for each van and "Take one
  back"; "Check on them", what to say, what to do about an incident). No clock: every call is a decision. The dispatch
  desk's board is the game's list window (`DispatchList`).

`JobHud` is deleted: its status, countdown, choices, target diamond, pay slip, plate, marker and prompt were moved to `ShiftCard`, `WorldPanel` and `GuideMark`, and the director's drawing of them is gone. `JobKeys` is unchanged: every
member still has a caller outside the jobs.

Not checked in the game (nothing here has been run): any of the above. In particular where each panel stands and how
it reads, that the box follows a walking passer-by, that the canvasser and cleaner outfits fit, that the clock runs in
a shop and on the open street without the player being moved, that the busker's idle pose and the stand sit well with
the game's guitar performance, and the amounts the clock pays.


### A shift across a save (`JobSave`, `PaySlip`)

The game saves by itself every few minutes and at midnight (`GameManager.CheckAutoSave`), so a save in the middle of a
shift is ordinary. What a shift had paid slip by slip was always in the save's money; what was lost on loading such a
save was everything still to be settled at the end: the work done by hand since the last slip (most gigs pay it in one
slip when they end), tips and bonuses its events had earned, the units of a clock run that was still going, and for an
employer's shift the whole basic wage and the shift on the review's count.

**The rule: every job is settled, none is resumed.** A shift cannot be taken up where it was: all nine kinds find
their place, people and props on the street when they begin (a pitch, a client's shop, a pile of boxes, passengers,
a round's doors), and none has a state that is only counts and a place. So:

- **At a save** (`JobDirector.OnSaveGame` -> `LiveLine`) one line goes under `reallife:job_live`: the job, the day and
  minute, what the shift has been paid (`job.Shift`), and what it is owed now (`Job.Unpaid()`), with the employer, the
  hourly wage as it is being paid, the late flag and the clock-on time for a rota shift. Nothing of the shift changes
  and it goes on. With no shift on, a test button's shift or a test rota shift, the line is written empty, so one is
  never left over from an earlier save.
- **What is owed** is the job's own word. `Job.Owed(out slip)` is the slip its settlement would pay if the shift
  stopped now through nobody's fault; each kind's `Settle` / `Pay` is now `if (!Owed(out slip)) return; _paid = true;
  Ctx.Pay(...)`, so the save's figure and the live pay are one piece of code. A round or a delivery stopped early has
  not beaten the clock or shelved the whole delivery, so those bonuses are not in it (`CourierJob`, `StockerJob`).
  `Unpaid()` adds the events' money to that slip, bounds it at nothing (`PaySlip.Bounded`, as `Director.Pay` bounds a
  slip), and adds what a clock run that is still going has done (`ShiftClock.Owed`; the drinks cart's own clock in
  `VendorJob.ClockOwed`). Ride-hail pays each fare as it ends and owes nothing; a fare in progress pays nothing, as on
  any other way out.
- **At a load** (`JobDirector.Slow`, once the jobs' state is loaded -> `SettleSaved`) the line is emptied first and then
  settled (`JobSave.Settle`), as a shift that ended early through nobody's fault: the owed slip is paid through
  `PayOut` (the game's transaction under the job's name, the energy, the skill, `JobState.Worked` for the level and the
  day's amount: exactly what `Pay` does); a rota shift gets `CareerDirector.SettleSaved` (the basic wage for the hours
  from clock-on to the save by `CareerCatalogue.PaidHours`, the rule `ShiftEnded` uses, and `CareerState.ShiftDone`
  with no complaint, only if work was done and the player still works there); a gig counts as a shift worked
  (`JobState.ShiftCut`). One notification says so (`reallife_job_saved_paid`, `_nothing`, `_rota`).

What that rules out:

| | how |
|---|---|
| lost earnings | the owed slip and the wage are paid on loading |
| double pay | only `Owed` is ever paid; `Paid` is carried for the review's totals and the log. The line is emptied before the money moves, and every later save writes it afresh |
| a free second shift | the units count towards the day's amount, and the rota day was handled when it was clocked on to (`CareerState.ClockOn`), so it is neither missed nor open again |
| a strike or a mark for saving | none: it is an interruption, as going indoors or to hospital is |
| a strike taken off for saving | none either: `ShiftCut` leaves the strikes alone (a shift that ends on the street by an interruption does take one off: that was left as it is) |
| a shift with no work done | pays nothing and counts as nothing (no wage, no shift, no complaint) |
| a line that does not add up | refused whole (`JobSave.Read`: a missing field, a figure below zero, not a number), logged, dropped |

Nothing of a job is left in a save or stuck after a load, and that was so before: the uniform is drawn over the
player's own clothes and never saved (`JobUniform`), the cleaner's dirt is taken off the client's registration for
the moment of the save and its mop out of the hands (`CleanerJob.OnSaveGame`, `JobDirt.Suspend`), a sealed box in the
hands or in a car is taken away after the load (`CourierJob.ClearLeftovers`: the courier's parcel and the stocker's
box are the same item), a hired van is in its own line and is seen back (`JobRental`), and the hold on the player, the
marker, the shift card, the panels and the people are objects of the session that a load does not bring back.

"Prepare this save for removing the mod" and "Reset the mod's data" end the shift first (`JobDirector.Quit`: paid on
its own terms) and then remove the key with the feature's others (`Features`: `.Data(..., "reallife:job_live")`).
Without the mod the key is ignored by the game.

**Home routines and leisure activities** carry nothing across a save and leave nothing behind: nothing of them is in
modData while they run, they put no item in the player's hands, and their hold, clip, props and cards are objects of
the session. After a load they are simply over, and what each gives when it ends is not given: cooking has taken
its ingredients from the fridges when it began and gives no meal (`CookActivity.Prepare`, as "stopped cooking" does);
coffee, tidying and exercise give nothing for the minutes spent (their effect is worked out in `End`); the waiting
room does not open the doctor's visit; a chess game's stake of 10 is not returned (`ChessGame`: only a game that never
got going refunds it); fishing has paid for its bait and the fish in the bucket are not sold (`Fishing`: sold when it
ends); a drink at the bar is paid as it is bought and nothing more is lost. The dance floor, sightseeing and the jog
were looked at only for what they keep (nothing in the save) and what they take at the start (nothing).

Checked offline (`CatalogueCheck/SavedShiftCheck.cs`): 920 shifts (every gig, every rank of every employer, at random
moments, some with nothing done, some sunk by a penalty) are written, read back field for field and settled; the
work, the hours and the wage are compared with the live rules written out a second time; what had been paid before
the save does not change what the load pays; `PaidHours` is compared hour by hour with the formula `ShiftEnded` had;
a bounded slip plus the clock's pays what the two pay apart; fifteen bad lines are refused; the key is registered and
the three sentences have their blanks.

Log lines: `Jobs check (saved shift): '<job>' is on at the save: paid so far N for U unit(s), owed M for V ...: PASS`
(diagnostics on), `Jobs check (saved shift): settled '<job>' saved on day D: paid M for V unit(s) owed ...: PASS`
(always), `Careers: a shift at <employer> that was on at a save was settled: ...`.

Not checked in the game: any of it; in particular that the game's save event reaches the hook during each kind of
shift (the cleaner's own hook shows it does for one), and what the figures are at a real midnight autosave.

### Adding a job

1. A row in `JobCatalogue.All` (and its id as a constant): needs, pay, energy, skill, daily units.
2. A class in `Scripts/Jobs/Kinds` deriving from `Job`:
   - `OnBegin` sets it up where the player stands and returns false with `Refusal` (a locale key the
     phone can say) if it cannot be done here;
   - `Tick` runs five times a second and is a switch over the job's own steps. It says what to show by
     setting `Objective` (through `Show`), `Status` or `ClockSeconds`, the marker (`Mark` / `Unmark`) and
     `Prompt`, and only when they change: these are strings, and nothing may build one per tick;
   - `Interact` is the interaction key (`JobKeys`), called only while `Prompt` is set (or always, for a
     job that `OwnsInteractKey`); `Choices` (three labels) puts a row of three on screen and `Choose`
     is called with 0, 1 or 2. Texts never name a key: they carry `{key}`, which `JobText.T` fills in;
   - `WorksInside(building)` says yes for the one building the job goes on in (a client's shop, picked
     with `JobSites`); the director then lets the shift continue in there and nowhere else indoors;
   - the player's own body is moved only through `Ctx.Self` (`JobBody`): `Hold` first, then `Clip` or
     `Pose`; the director undoes all of it on every way out;
   - `OnCrash` and `OnPoliceStop` if driving matters to it;
   - `End` is called exactly once on every way out. It gives back every person (`Ctx.Cast.Release`),
     anything in the player's hands, and pays for work done and not yet paid (`Ctx.Pay`);
   - `Finish(JobEnd.Done / Failed)` when the shift is over.
3. One line in `JobDirector.Create`, a test button in `Core/RealLifeOverhaulMod.cs`, its strings at the
   end of `Locales/en.json` (several lines for anything said more than once, picked with `JobText.Pick`).

People come from `Ctx.Cast` (two at most; `Put` only at a point that came from `JobSpots`, `StreetSpots` or
`CitySpots`, out of the player's sight where the job can choose, and cope with null), or are the game's own
pedestrians borrowed through `Ragdoll/PedestrianTakeover` and handed back with nothing of ours on them. They
move only by walking. Nobody gets into or out of a car except at a spot `JobSpots.BesideCar` found clear:
with `job_car_enter` / `job_car_exit` in the bundle they are seen to, otherwise it is the sound of the door
(the game has no such animation, and none is faked). The cast is not used indoors.

### The hired van

A driving job taken with no car of the player's own within 60 m is given a hired van (`JobRental`, 45 once a
day). It is the game's own job van made exactly as its delivery job makes one (`DeliveryVehicleInstance`,
`VehicleHelper.CreateAndSpawnVehicle`), with no mission. The van's prefab carries the game's
`DeliveryJobVehicle`, which takes the van off the save's vehicle list when the object goes and, once an
hour, destroys any van that has no mission, is not driven and is not in view. A save made in the middle of
a hire therefore holds only a vehicle the base game made and removes by itself; with the mod loaded the van
is also remembered by id in modData and taken back once the player is out of it. During a hire that hourly
check is unhooked for the one van. If the prefab ever comes without the component the hire is refused.

For the rest of the mod: `JobDirector.ActiveJobId`, `LastJobId` and `EarnedToday` are safe to read at any
time (a life event can ask whether the player drove a taxi today). A good shift adds `ModState.MoodUp`,
a failed one `MoodDown`, a sacking `MoodDownBig`.

### Job clips

Job code never names one of the game's animations. It asks `IJobAnimations.Play(character, JobClip.X)`,
which returns how long the beat lasts, or 0 if that character cannot do it. `ClipJobAnimations` plays the
mod's own clip for it when the bundle has one (its table lists the clips that will do, best first: `job_*`
when they arrive, and until then the living city's `leaflet_offer`, `wave_hello` and `serve` where they fit)
and otherwise hands over to `GameJobAnimations`, the nearest gesture the game's characters have. A gesture
(hail, hand over, receive, refuse, thank, knock, annoyed) is played on the masked layer with `BodyMask.Upper`:
the legs stay the game's, so whoever makes it may be walking. What moves the whole body by its nature (into and
out of a car, to a boot, a shelf, a counter) is played whole, and the player is held still for it (`JobBody.Hold`).
`Moment(JobClip, "release" / "grip")` says when in the beat a hand lets go or closes; a job asks for that instant
with `Ctx.At(time)` and gets it in `Job.Moment`, to the frame (the tick is five a second): that is when a flyer, a
parcel or a cup changes hands. `job_hail` is a loop and goes on until `Stop(person)`.
Also asked for by JobClip: `HandOverLeft` (`job_hand_over_l`), `ReceiveBox` (`job_receive_box`), `CarEnter`,
`CarExit`, `LoadBoot`, `Stock` (`stock_shelf`), `Serve` (`serve_counter`). On the player, by name through
`JobBody.Clip`: `mop_floor` (else the game's `Cleaning` with its `Mop`), `guitar_strum` (else the game's own
guitar performance from `StreetPerformerDataHelper`, with its music). Whoever takes a parcel carries it in
`carry_box_hold` (arms only, over the game's own walk and idle); only a bundle without it falls back to the
whole-body `carry_box_walk`, kept in step with `MatchSpeed`. `job_flyer_hold` is on the player's left arm for the
whole flyer shift (`JobBody.Over`, `BodyMask.LeftArm`), walking included, and is taken up again after each offer.
`CarEnterLeft` / `CarExitLeft` are the `_l` clips for a door on the car's left: `JobSpots.AtDoor` gives the place
the four are made for (square to the car, 0.75 m out from the middle of the rear seat, which is an estimate: the
game's vehicles say nothing of their seats) and the side; the clip is played only within 0.35 m of that place.
The clips first asked for, as designed (Humanoid, in place, starting and ending in the standing idle):

| JobClip | played today by | wanted clip | length | hand | prop socket |
|---|---|---|---|---|---|
| `Hail` | `DJEncouraging` | `job_hail`: right arm up and out towards the road, hand open, two small waves, lean forward on the toes | 1.6 s, loops cleanly | right | none |
| `HandOver` | `PressAction` | `job_hand_over`: holds an object out at chest height to someone 0.8 m in front, releases, arm returns | 1.2 s, release at 0.55 s | right (a mirrored `job_hand_over_l` for the flyer hand) | right hand: the object leaves the hand at the release |
| `Receive` | `PressAction` | `job_receive`: reaches forward, closes the hand on an object at chest height, draws it in to the body, small nod | 1.3 s, grip at 0.5 s | right; a two-handed `job_receive_box` for a parcel 0.4 m wide | right hand (or both, 0.4 m apart) from the grip on |
| `Refuse` | `IDontThinkSo` | `job_refuse`: palm up and out at shoulder height, head turns away, a half step back | 1.2 s | right | none |
| `Thank` | `DJEncouraging` | `job_thank`: a nod and a short open-hand wave at head height | 1.2 s | right | none |
| `Knock` | `PressAction` | `job_knock`: three raps with the knuckles at head height, 0.5 m in front, then a step back | 1.6 s | right | none |
| `Annoyed` | `IDontThinkSo` | `job_annoyed`: both hands thrown up, head shake, turns away | 1.4 s | both | none |

**All of these are now in `Bundles/reallife_anim`** under exactly those ids, with `job_car_enter` / `job_car_exit`
(and `_l` for a door on the car's left side), `job_load_boot`, `job_flyer_hold`, `carry_box_hold`, and for jobs to
come `mop_floor`, `stock_shelf`, `serve_counter`, `till_tap`, `carry_box_walk`, `guitar_strum`. The table in
`CityAssets/README.md` ("Jobs") has each one's length, the moment of its grip or release (`CityClip.Mark`), the hand,
the prop it holds and where the character must stand. They are made for the upper body and are meant to be played
with `ActivityAnimator.PlayOver(clip, BodyMask.Upper)` (the legs stay the game's own) and `job_flyer_hold` with
`BodyMask.LeftArm` over walking; `ClipLibrary.Has(id)` says whether a clip is there, so an `IJobAnimations` that
plays them can fall back to `GameJobAnimations` beat by beat. `ClipJobAnimations` and the jobs use them so.

### Job props

The parcel in the player's arms is the game's own sealed cardboard box, carried with the game's own
two-handed carry (`JobCarry`). Everything else comes from `JobProps.Take(id)`: the modelled prop of that id;
failing that the one it is listed against in `JobProps.Alias` (`flyer` -> `leaflet`, `flyer_stack` ->
`leaflet_stack`, `parcel_s/m/l` and `shelf_box` -> `parcel`, `mop_bucket` -> `bucket`, `vendor_cart` ->
`coffee_cart`); failing that a plain shape for the few that pass as one (parcels, `taxi_sign`, `guitar_case`,
`mop_bucket`, the cleaner's `floor_patch`, which is only ever a shape). No cart and no help-wanted board is
made of plain shapes: without the prop there is none. A prop goes in a hand only through `HeldProp`, at the
grip of a clip made for it and with the prop that clip names (`ClipLibrary` carries both); the last resort for
the flyer stack alone, when no clip can be had at all, is `HandProp`'s plain shapes fitted from the forearm.

## Leisure

Everything under `Scripts/Leisure` (namespace `RealLifeOverhaul.Leisure`): things the player does for fun at real
places in the game's city, the mod's clothes as something bought, a little night life, a sound bed and photo mode.
One component, `LeisureDirector`, owns all of it except sound and photo mode, which are two small components it adds.

```
Leisure/
  LeisureDirector   the one activity that is on, what is on offer where the player stands (one look a second),
                    interruptions, the result card, the test buttons, the film programme, drink at the wheel
  LeisureActivity   the base every activity derives from; Sweep, the one timing interaction they share
  LeisureApi        Activities.Start / Stop / Finished for the rest of the mod; LeisureMoney
  LeisureState      this save's records, landmarks, catch, clothes bought, outfits, drinks: plain strings in modData
  LeisureScreen     the game's panels, job panel, list window, marker and notifications for all of it
  LeisureBody       the player's body at play: held, a clip with its tool (HeldTool), an arm loop, one of the game's dances
  LeisureProps      pooled props: from PropLibrary by id, else a stand-in of plain shapes (see the wish-list)
  LeisureVenues     what the game's city has: the items of the building the player is in, landmarks, the water's edge
  Activities/       ChessGame (+ ChessCorner, the chess player on the pavement), Fishing, NightOut (DanceFloor, BarDrinks, Sightseeing)
  Shops/            ClothesShop (prices, what is owned, the rule), ClothesRack (the prop in a store), ClothesScreen (the game's clothes screen: the shop at the rack, the wardrobe at home)
  Night/            NightLife (two people outside an open venue in the evening), TipsyDriving
  Sound/            LeisureSound (the four ambient loops, the cue tones, the radio's name on the HUD)
  Photo/            PhotoMode
```

### What the game's city has, and what is built on it

Found by reading the game's code (`BuildingRegistration.businessTypeName`, the item names of an interior, the
scene's MonoBehaviours); nothing is guessed at run time beyond what is listed under "Unverified".

| the game has | leisure uses it for |
|---|---|
| nightclubs (`ba:businesstype_nightclub`) with dance floor items, cocktail bar items (`ba:itemname_*cocktailbar*`) and nine player dances (`BaseHuman.SetDance`) | the dance floor (a rhythm game on the game's own dances) and drinks at the bar |
| clothing stores (`ba:businesstype_clothingstore`) with `ba:itemname_clothingrack*`; no way to buy clothes to wear (clothing is retail stock; the wardrobe changes outfits for free) | the Real Life Overhaul rack: the game's clothes screen as a shop of the mod's garments, tried on and bought |
| the clothes screen of wardrobe and dresser furniture in homes (`ChangeCharacterClothesUI`) | the mod's clothes the player owns, in its grids after the game's pieces |
| cinemas and theatres where the game itself seats the player and passes the time ("watch show", with its own mood) | only what was missing: a programme (what is on today) and a verdict on what was watched |
| the pier's big wheel and rides, the casino boat's pier, golf course, tennis courts, swimming pool, bridges, twenty named subway stations, the hospital | landmarks to see and tick off; the waterside ones also say where a line can be cast |
| pavements (through `CitySpots`) and the mod's own `chess_table` prop and `chess_sit` clip | the chess player on the pavement |
| a car radio with four stations (`RadioPlayer`), footsteps by surface (`StepTrigger`), rain loops (`RainAudio`) | left alone; the radio's station and song are named on the HUD |

**Not in the game, so not built** (the brief said to skip these if the city has no such place): basketball hoops,
bars as a business of their own (hence no pool or darts: a nightclub's bar is the only bar), bowling. The game's own
arcade machines, golf, tennis, gym machines, swimming, carnival rides and casino are already playable by the
player through the game and are not touched.

### The activities

Each is started by the prompt that appears where it can be played (the interaction key), by its test button, or by
`Leisure.Activities.Start(kind, place, companion)`. One at a time, never during a job, a police stop, a mission
screen or in a vehicle. Every way out (played to the end, walked away, a load, hospital, time skipped, knocked
down, the option switched off, an error) goes through `LeisureDirector.EndActivity`: the body is released first
(`LeisureBody.Release`: clip disposed, hands emptied, dance stopped, the hold let go), then the activity's `End`
gives back whatever else it put out. A best score is kept per save for chess, fishing, dancing and sightseeing.

| activity | where | how it plays | costs | pays / mood | record |
|---|---|---|---|---|---|
| Street chess | at the mod's chess table, where a chess player sits by day in the dry (put out of view 30 to 70 m off, one at a time, never while the living city has its own chess table out) | walk to the free stool (marked), sit (`chess_sit`); five positions, three moves each on a 22 s clock: 2 points for the move, 1 for a sound one, 0 for a blunder; the hand makes the move chosen (`chess_move_player`, from and back to the sitting loop); he answers each in a line; an onlooker to the side of the table, where the pool can spare one, cheers once (`cheer_once`) on a win | stake 10 | 7+ of 10 wins the stake (MoodUp), 5 or 6 draws it (Social), under 5 loses it | points |
| Fishing | standing still at the water's edge within 320 m of a waterside landmark, facing open water | the rod in the hand at the grip the four rod clips share: `fish_wait` between casts and while the float is out, `cast_rod` for the cast (the float and line leave the tip at its `release` mark, 0.9 s), `reel_in` while a fish is on, `fish_land` when it comes out; they cross-fade with the rod in the hand and turn it themselves. Five casts: stop the cursor in the zone to cast (a better cast: shorter wait, better fish), wait for the bite, strike inside the fish's window (a strike at nothing scares it; three and the cast is over), then three timed pulls (the zone narrower the heavier the fish; two misses and it is off). Key 3 packs up early | bait 6 | the catch is sold by weight (0 to about 110 a fish); MoodUp for a catch, MoodUpBig for a new kind or 3 kg | heaviest fish; kinds caught (8) |
| Dancing | in a nightclub, on or beside a dance floor item | the player dances one of the game's nine dances; a cursor crosses the meter once a beat (0.86 s), 24 beats: the key in the zone scores 1, in the bright middle 3; every sixth in a row changes the dance | free (the club's own entrance fee is the game's) | MoodUp from 12 on the beat, MoodUpBig from 18 | points + 2 x longest run |
| Drinks | in a nightclub, within 2.8 m of a cocktail bar item | beer 8, cocktail 14, soft drink 4; the glass is stood on the bar and the player steps up to it and drinks with `drink_bar` (the glass on the bar before the clip's `grip` mark and after its `release`, in the right hand between) where the bar measures as one that clip fits; anywhere else drunk where they stand (`sip` with a glass). See "The bar" below | the price | MoodUp; from two drinks in the blood the Tipsy mood (+3, 3 h) instead, and the car wanders | none |
| Sightseeing | within a landmark's radius (35 to 70 m), on foot | the player stops and takes a picture (`take_photo` with its phone); the place is ticked off | free | MoodUp for a first sight, MoodUpBig for the last one on the list | landmarks seen |
| Film programme | inside a cinema or theatre | nothing to play: the house names today's film on the way in; a show watched (the game's own seat) for 45 game minutes or more gets a verdict | the game's own | MoodUpBig, MoodUp or nothing, by the film | shows watched |

Why chess is a reading game and not a board: a whole game of chess with an engine and a board interface would be a
mod of its own, and is not what anybody stops on the pavement for. Five positions with three candidate moves keep
what is chess about it (seeing the fork, not moving the pinned piece, swapping when ahead) and take two minutes.
The clock is only there so that it is not a quiz. The eight positions are dealt in a different order each game.

Drink: each drink is 0.8 (beer) or 1.2 (cocktail) and wears off at one per ninety game minutes
(`LeisureState.DrinksAt`). The bar stops serving alcohol at four. From two, `LeisureDirector.Tipsy` is true and
`TipsyDriving` (switched on by the director only while the player is at the wheel and tipsy) adds a slow uneven
turning pull to the driven car, stronger the more was drunk: one `AddTorque` per physics step, nothing changed on
the car. **It is an offence**: `LeisureDirector.Wander` calls `PoliceAttention.Report(Offence.DrunkDriving)` while the
player drives tipsy, and any stop finds it (see "Wiring": fine 900, 6 points, a court case the second time).

The bar (`BarDrinks.Serve`): `drink_bar` is made for a bar top 1.05 m up with the glass on it 0.35 m ahead of the
drinker and 0.06 m to their right. The game's bar is an item of unknown shape, so it is measured when a drink is
ordered, on the bar item's own colliders: a ray from the player at 1.02 m finds the face they would stand at (it
must be square to them within 60 degrees), a ray down where the glass would stand must find a level top 1.05 m up
(5 cm either way), and the place to stand (toes 0.25 m from the edge) must be on the floor's navigation mesh. If
all of that holds the glass is stood on the bar (`drink_glass`), the place is marked, and once the player is within
half a metre of it (nine seconds are allowed) they are held, brought the last step (`LeisureBody.Settle`), turned
square to the bar and the clip is played with empty hands: `LeisureBody.PickUp` puts the glass in the hand at the
clip's grip at its `grip` mark, `SetDown` stands it back on the bar at `release` (two comparisons a frame while a
drink is being drunk, nothing otherwise). If any of it fails, or the player does not step up, the drink is drunk
where they stand with `sip`, as before.

### For the rest of the mod

```csharp
bool started = Leisure.Activities.Start(Leisure.LeisureKind.Chess, null, friend);       // or with: out string refusalKey
Leisure.Activities.Finished += (kind, score, played) => { ... };                         // once per activity
Leisure.LeisureKind now = Leisure.Activities.Current;  bool tipsy = Leisure.LeisureDirector.Tipsy;
```

`Start` plays the activity where the player stands if it can be played there (the same test the test buttons
make) and answers false with a locale key otherwise; it never throws. `place`, when given, must be within 6 m of
the player: bring them there first. `companion` is somebody who came along: the player gets the Social mood at
the end; the companion is never moved, animated or released by leisure (whoever brought them keeps them).
Chess needs the chess player to be out; `Start` does not conjure a table.

`Leisure.Shopping.Buy(itemId)` (also with `out string refusalKey` and `free: true` for a gift), `Price(itemId)` and
`Owns(itemId)` sell the one thing there is to sell today: the mod's garments and shoes, by their catalogue id.
Groceries and gifts were to be added only when another feature needs them; none does yet, and there is no
second inventory.

### Clothes

The mod's garments and shoes (`Character/CharacterCatalogue`: tees, jeans, bomber, summer dress, three shoes, in
their colourways) are bought at the **Real Life Overhaul rack** in any clothing store: a prop of the mod's own
stood on a tested piece of shop floor while the player is inside (clear of the walkable area's edges by 1.1 m,
nothing of the shop's inside its footprint, walkable to from the door; a carving obstacle on the prop itself, gone
with it), or, in a store with no such floor, the nearest of the store's own racks. The game has no shoe shop:
shoes are on the same rack.

The rack's key opens the **game's own clothes screen** (`ChangeCharacterClothesUI`, what a wardrobe's "Change
clothes" opens: the player's preview, the categories, a grid of pieces, a row of colours and Save) as a shop
(`Leisure/Shops/ClothesScreen`): the Torso, Legs and Feet grids hold the mod's garments in every colour, each tile
with its price or "Owned" (`Character/GridLooks`, the tiles the character creator has too), and the game's own
pieces of those three kinds are put away. A click tries a garment on the preview; Save reads "Buy (price)" for what
is tried and not owned, greyed while the money is not there. Buying pays (`LeisureMoney`) and keeps it on; closing
the screen without buying takes off what was tried, back to what they came in wearing. A Save is told from a
closing by the save's list of the player's pieces: the game's SaveClothes puts a new one in its place. The price
goes by the garment:

| garment | tier | price |
|---|---|---|
| T-shirt | Everyday | 35 |
| Jeans | Everyday | 60 |
| Court shoes | Everyday | 70 |
| Runners | Smart | 95 |
| Summer dress | Smart | 120 |
| Bomber jacket | Smart | 140 |
| Derby shoes | Premium | 220 |
| anything new without a line in `ClothesShop.Price` | by its tier | 50 / 110 / 200 |

**"Clothes must be bought"** is kept per save (`LeisureState.MustBuy`, with the ids owned): on for a game begun
with this version (which owns whatever was picked during character creation: the creator offers the Everyday tier
only, `CharacterCatalogue.Starter`, as entries of its own grids; see `CharacterAssets/README.md`, "Whose picks"),
off for a save that was already going, so nobody loses what they wear. The tier of a garment is the catalogue's
(`CharacterCatalogue.TierOf`); `ClothesShop.Tier` only passes it on. The options toggle speaks for the save that is loaded. While it is on,
anything picked on the options page that is not owned is taken off again with a toast, and the dropdowns name
unowned entries "(not bought yet)": the game builds a dropdown from the array it was given at start, so entries
cannot be removed from it, only renamed.

At home the mod asks nothing of a **wardrobe or dresser**: its own "Change clothes" opens the same screen, and what
the player owns of the mod's is in its Torso, Legs and Feet grids after the game's pieces, in the colours bought.
Save keeps what was picked; closing without saving puts back what was worn (the game asks first, as it does for its
own pieces). The screen dresses employees in uniforms too: then nothing of the mod's is in it. (Until 0.3.0 a key at
the furniture opened a list of the mod's own with three saved outfits; the game's panel was often showing another
place there, so the key did nothing. The outfits saved then are still in the save; the game's screen has none.)

### Night life

Between 20:00 and 03:00, in the dry, two people of the living city's pool stand talking outside the nearest open
nightclub, cinema, theatre or casino door within 75 m (`talk_a` / `talk_b`, both or neither), put there and taken
away only out of view, for five minutes or until the player is 120 m off. Never while the living city has its own
door scene out (`door_queue`, `doorman`) or fewer than four of its eight people free.

Deliberately not done, and why: **brighter signs** (each sign has its own material instance that the game colours
and switches itself, `BuildingSignController.ToggleLight`; a property block over that cannot be checked without
running the game), **more taxis at closing time** (the traffic pool is filled once at start and has no knob for
one kind of car; the hourly group percentages are overwritten by the game), **fewer walkers late** (the game sets
pedestrian numbers by the hour itself), **street musicians** (the game spawns its own performers; the living city
already gives them an audience).

### Sound

Checked first, and left to the game: footsteps already differ by surface, rain has its own loops, the car has a
radio, and the mod's sirens are already positional (`TrafficEffects.AddSiren`: 3D, logarithmic roll-off from 4 to
95 m, Doppler 0.5, pitched low), so there was nothing to fix there. The game has no birds, crickets, crowd or
far-traffic clips in its code, so the four loops are made from arithmetic once per session (one per quarter
second after the city loads, a few milliseconds each): birds (06:00 to 19:00, more before 09:00), crickets
(21:00 to 05:00), a low hum (always out of doors, less at night) and a murmur that follows how many of the game's
pedestrians are within 18 m (counted every two seconds). Four looping 2D sources and one for cues; volumes are
moved on a quarter-second timer, a layer at nothing is stopped, indoors and in the rain they fade out, in a
vehicle they drop to 40 %. All are routed to the game's city mixer group (master and effects volume apply) and
stop with the game's pause. One slider per layer.

### Photo mode

`KeyAction.PhotoMode` (F10: the game binds nothing to it). The game is paused through its own
`GameSpeedController.SetPause` (the frame goes on being drawn) and its pause key is locked out; the Cinemachine
brain is switched off and the main camera moved directly; the game's interface is hidden with the game's own
`ScreenshotController.ToggleUIVisibility`, the mod puts nothing of its own on screen. Mouse: look. W A S D: fly.
X / Z: up / down. Shift / Ctrl: fast / slow. Wheel: zoom (field of view 8 to 100). J / L: roll. , / .: exposure.
K: filter (none, warm, cool, black and white, vivid, faded). G: depth of field, Y / U: its focus distance.
1: hide the player. 2: hide or show the game's interface (the controls and settings come in a notification). 3: reset. Enter: save a PNG at the screen's resolution to
`<persistentDataPath>/RealLifeOverhaul/Shots`. Exposure, filters and depth of field are a Volume (priority 1100)
that exists only while the mode is on. Everything is recorded on the way in and put back on every way out (the
key, a menu opening, the game unpaused by anything else, the city unloading, an error). The camera stays within
150 m of the player. The fixed keys live in `Core/Keys.cs` (`PhotoKeys`) and were chosen from keys the game does
nothing with while paused (not Space, Q, E or F).

### On screen: the game's own interface (Leisure)

The mod draws nothing of its own for leisure (`LeisureHud` is gone; everything is `LeisureScreen`, which owns the pieces below). None of this has been seen in the game.

| screen | what it is now | if it cannot be made |
|---|---|---|
| a place where chess, fishing, a dance, a drink, the rack or a view is on offer | the game's panel over the place (`GameUi.DoorPanel`): the offer as the first line, "Go ahead" on the button | tried again once a second while the game's panel shows another place |
| the objective and the figures of an activity | the headline and lines of a copy of the game's job panel at the top right (`ShiftCard`): the activity's name, what to do, the status | what is asked is sent as a notification each time it changes |
| the prompt ("cast", "strike", "pull in the green", "on the beat") and the choices (the chess moves, the drinks, "pack up") | buttons of the game's panel over the player: the prompt on the first, each choice on the next ones, the game writing each key and running it on that key or a click. A button's words are a locale key filled from the activity's text (`reallife_leisure_button`) | as above |
| the marker | the game's destination marker (`GameUi.GuideMark`) | not shown while a job or an outing has the marker |
| the timing meter (fishing: cast and pull; the dance beat) and the chess clock | two lines of the job panel: what it asks with the zone as percentages, and a gauge of twenty cells in plain letters (`=` good, `#` best, `|` the cursor; for the clock `#` is what is left). The action is the panel's button. Timings, windows and judging are the activities' own and untouched | no gauge without the job panel; the button and the words still work through the notification |
| the rack's shop and the wardrobe | the game's own clothes screen (`ChangeCharacterClothesUI`): the mod's garments as tiles of its grids (copies of its own entries) and colours in its colour row; in the shop its Save reads "Buy (price)" | the screen is left as the game made it (`Clothes: the mod's clothes are left out of the game's clothes screen ...`); the options page still has the clothes |
| what a game or a visit adds up to; the records; the radio's station and song | one of the game's notifications | none needed |

Why the meter is not the game's activity panel. `PlayerActivityUI` was read: showing an activity pauses the game (`gameSpeed.SetPause(true)`), and its progress bar is written only by `SetUpProgressBar`, which runs when a whole minute of game time has passed (`OnActivityRunning`) or inside `UpdateActivityDisplay`, which also makes its labels and buttons again. A cursor that crosses in about a second cannot be a progress bar there and could not be moved without remaking the panel every frame. The job panel's line with a text gauge was used instead, as the fallback allowed.

Cost: per frame only `LeisureScreen.Gauge`, which compares one cell number and writes a line only when the cursor is in another cell (about twenty times a pass, only while a meter is up). Everything else is told on the tick (five a second) or on a button press, and only when it changed.
Log lines with diagnostics on: `Leisure check (prompt panel)`, `Leisure check (activity card)`, `Leisure check (clothes list)`, each ending PASS or FAIL.
Behaviour that changed: the keys 1 to 3 and the interact key are the panel's buttons now (the game shows the player's own keys); the browser's up and down are the list's scroll; photo mode starts with the game's interface showing and the controls in a notification (the "2" key hides or shows the game's interface, and a picture is always taken with it away); the mod's own help and settings strips, the dimming of the screen (nothing used it) and the corner strip for the radio are gone.

### Performance

Idle: `LeisureDirector` one timer test a frame and a look round once a second (the offers are a handful of
distance tests; the pass over a building's items happens once per building entered, the scene search for
landmarks once per city load; the water test, three rays, only while the player stands still near the water);
`LeisureSound` one timer test a frame and four volume comparisons a quarter second; `PhotoMode` one key test a
frame; `TipsyDriving` switched off. Per frame only while an activity is on: the meter's cursor (one cell number compared), for fishing the float and the line (the rod is turned by its clips; only the stand-in for a bundle without them aims it), and for a drink at the bar two timed moments. Strings are built when something
changes (a key press, a new step), never on a timer. Each of the three components catches what its `Update`
throws, logs once, puts back what it changed and stays off for the session.

### Unverified (the game could not be run)

- **The water's edge** is measured, not known: the game has no water object, layer or height. Fishing is offered
  where the walkable ground ends within 3 m and 5.75 and 7.5 m ahead there is something named water, nothing at
  all under the sky, or a surface 0.8 m or more below that nobody can walk or drive on, and only within 320 m of a
  waterside landmark. It may be offered at a drop that is not water, or refused at water the rays read as ground.
  The float is shown only where a surface was hit.
- **The fishing clips** (`fish_wait`, `cast_rod`, `reel_in`, `fish_land`) are played whole-body on the held player with
  `fishing_rod` at their shared grip, each held on its last pose (which is `fish_wait`'s first) until the next: seen
  only on the clip pipeline's sheets, not in the game. `fish_wait` holds the rod 22 degrees to the left of straight
  ahead, so the tip is not over the float's line; the line is drawn from wherever the tip really is. A bundle that
  lacks any of the four gets the old stand-in (`umbrella_hold` on the right arm and the rod aimed each frame).
- **The bar** is measured, not known (see "The bar"): whether the game's cocktail bar item has colliders that give a
  level top at 1.05 m has not been seen; if it does not, every drink is the standing `sip`.
- **Landmarks**: found by type in the scene; which of several bridges (the most westerly) and whether every
  listed subway station exists in the scene is not known. A missing one is simply not on the list.
- **The rack's floor test** may find no clear floor in a store whose interior has a large collider round it; the
  store's own racks are then used and no prop is stood.
- **The film verdict** needs the game's temporary mood for watching a show to be named `...watching_show` (it is
  in the game's legacy map under that name); if it is called something else, the programme is named but no
  verdict follows.
- **Depth of field** in photo mode needs the camera's frame settings to allow it; if they do not, G does nothing.
  Hiding the game's interface relies on `ScreenshotController` (the game's own F7 debug toggle).
- **The dance** relies on `BaseHuman.SetDance` holding while the player is under a navigation blocker.
- **Mixer routing**: whether the city mixer group is silenced by the game's pause snapshot is asset data; the
  layers are also paused directly whenever time stands still.
- The sound of the four synthesised loops has not been heard by anybody.

### Wish-list for the asset pass

All drop in by id with no code change (`LeisureProps.Take` prefers `PropLibrary`; `ClipLibrary.Has` gates each
clip), except where a line says what to change. **Delivered and played:** `cast_rod`, `fish_wait`, `reel_in`,
`fish_land`, `drink_bar`, `chess_move_player`, and the props `fishing_rod`, `fishing_float`, `fishing_line`,
`drink_glass`, `clothes_rack`. **Delivered, not played yet:** `throw_ball`,
`cocktail_glass`, `beer_bottle` and `shopping_bag`; and `browse_rack` and `try_on_turn`, played at the rack until it
opened the game's clothes screen (which pauses the game and shows the preview, not the player).

| clip | length | loop | what it is |
|---|---|---|---|
| `cast_rod` | 1.6 s | once | right arm back over the shoulder and forward, `fishing_rod` in the right hand; mark `release` at 0.9 s (the line goes out) |
| `fish_wait` | 8 s | loop | standing, rod held out low in both hands (right at the grip, left 0.3 m up), a small shift of weight; replaces `umbrella_hold` in `Fishing.Begin` |
| `reel_in` | 1.2 s | loop | rod tip up, right hand winding at the reel (0.40 m up the rod, below it), leaning back a little |
| `fish_land` | 2.0 s | once | rod lifted high, a step back, a look at the catch |
| `dance_loop` | not needed | | the game's nine dances are used |
| `drink_bar` | 3.6 s | once | at a bar 1.05 m high: lifts a glass from 0.35 m ahead, drinks, sets it down; mark `grip` 0.5, `release` 3.0; replaces `sip` in `BarDrinks` |
| `browse_rack` | 4.0 s | loop | upper body: right hand moves along a rail 1.55 m up, 0.5 m ahead, pushing hangers apart |
| `try_on_turn` | 3.0 s | once | a slow quarter turn each way with the arms a little out, looking down at oneself |
| `sit_cinema` | not needed | | the game seats the player itself |
| `chess_move_player` | 2.4 s | once | seated as `chess_sit`: reaches to the board 0.45 m ahead, moves a piece, sits back (played when a move is chosen) |
| `throw_ball` | 1.4 s | once | only if a basketball court is ever added to the city: a free throw, release at 0.8 s |

| prop | size (m) | pivot | notes |
|---|---|---|---|
| `fishing_rod` | 0.07 x 0.10 x 2.10 | the butt of the handle; **the rod runs along +Z**, reel under it at z 0.40 | stand-in exists (three round lengths and a reel) |
| `fishing_float` | 0.045 x 0.11 x 0.045 | the waterline (red above, white below) | stand-in exists |
| `fishing_line` | unit length along +Z | one end | scaled along Z to the span each frame; stand-in exists (a 4 mm thread) |
| `drink_glass` | 0.075 x 0.12 x 0.075 | ground centre | held at the `coffee_cup` grip; stand-in exists. Also wanted: `beer_bottle` (0.065 x 0.23), `cocktail_glass` (0.11 x 0.16) |
| `clothes_rack` | 1.44 x 1.76 x 0.56 | ground centre; rail along X at 1.55 m, **browsed from +Z**; a small sign on top | stand-in exists (rail, two feet, seven hanging blocks of colour, a green sign) |
| `shopping_bag` | 0.32 x 0.38 x 0.12 | top centre of the handles | not used yet: for carrying out what was bought |
| `basketball`, `pool_cue`, `dartboard` | | | only if the city ever gets a court or a bar |
| `radio` | | | not needed: the car radio is the game's |

| sound | what it should be (a seamless loop unless said) |
|---|---|
| `RealLifeOverhaul Ambience Birds` | 20 to 30 s of small city birds (sparrows, a pigeon), sparse, no wind |
| `RealLifeOverhaul Ambience Crickets` | 10 s of night insects, even, quiet |
| `RealLifeOverhaul Ambience City` | 20 s of far traffic: a low rumble, no horns, no close cars |
| `RealLifeOverhaul Ambience Crowd` | 20 s of a pavement crowd's murmur, no words that can be made out |
| cue tones (`Cue Tick`, `Good`, `Bad`, `Bite`, `Shutter`) | one-shots: a soft click, a rising two-note, a low two-note, a small bell, a camera shutter |

The loops are made in `LeisureSound.Build`; to use recorded clips, load them by those names there instead of
calling the four generator functions.
## Social life

People the player knows, things to do with them, and a morning paper (`Scripts/Social`). Everything is on the
street or on the phone: the cast is never put indoors (the game loads an interior as a place of its own), so an
outing at a business is played to its door and from its door, and the time inside passes with the game's own
time machine.

### The cast

Eight people per save, rolled once from a seed and then stored whole (name, look, traits, job, district, free
hours, birthday), so a later change to the name lists renames nobody. One per way of meeting:

| who | met | how |
|---|---|---|
| Mom | known from the start (the contact `reallife-mom` the mod always had) | family: never less than a friend |
| Alex | known from the start (`reallife-friend`) | an old friend |
| six others | walking up to the player in the street | after leaving a gym (35 %), after a job shift that paid (30 %), after the second coffee bought at a cart (50 %), outside the player's home (15 %) or own shop (20 %), after a `ModState.Helped` moment (60 %); or brought along by a friend |

A new face comes at most every other day. Whoever comes walks in from pavement out of view, stops in front of the
player, waves and waits to be spoken to (45 s; they follow a little if the player moves on, then give up). The
conversation is the game's own dialogue panel with both of them facing and talking (`talk_a` / `talk_b` in turn);
"Swap numbers" puts them in the phone as a contact whose id is their name. A look is the game's own appearance
system run from the person's seed (`AppearanceSetter.RandomizeElements(gender, skin, casual, seed)`), so Dana
looks like Dana every time; `SocialCast` has two bodies at most and rebuilds one only when it is needed for
somebody else.

### The relationship model (`SocialRules`)

Per person: familiarity, affection, trust (0 to 100), today's mood, last contact, up to six memories, what they
would like to do. The People page (`reallife-people`, a contact that is called) shows all of it.

| stage | reached when | lost when |
|---|---|---|
| acquaintance | numbers were swapped | (drifts: familiarity under 8 and ten days without a word; a call brings them back) |
| friend | familiarity 30, affection 30, trust 20, two things done together | any of the three 10 below that |
| close friend | 60 / 60 / 55, five things done together, known ten days | any of the three 10 below that |
| dating | asked on the phone; they say yes at affection 60 and trust 45, only people marked compatible (half of the six, never fewer than two), one at a time | affection under 35 or trust under 25: they end it |
| partner | asked; yes at affection 80, trust 70, fourteen days of dating and four dates | as above |
| living together | asked; yes at trust 85 and twenty-one days as partners. Text and state only: a daily mood, their texts | with the break-up |

Worth: a first chat of the day +3 familiarity, +2 affection (half as much again face to face; +1 more if they texted since yesterday and the player is getting back to them); a warm answer +1.5
affection, a curt one -2; an outing +6 to +8 familiarity, affection by how much it is their kind of thing (0.7 to
1.4), +2 to +5 trust; on time +2 trust; over 20 game minutes late -3 trust, -2 affection (twice that for somebody
punctual, nothing for somebody who runs late themselves); not turning up within 75 minutes -12 trust, -10
affection and a text; calling a plan off -1 / -1, at under an hour's notice -4 / -3; a favour done +8 trust, +4
affection; a birthday remembered +8 affection, +3 trust, forgotten -4; being arrested -4 trust with everybody
(family -2, the supportive -1); a favour asked -2 trust, a third within a week -5 and a refusal; borrowed money not
returned in ten days -10 trust. Neglect: after 3 (partner), 4, 7 (friend), 10 (close friend) or 6 (Mom) days
without a word, -1 familiarity, -1 affection, -0.5 trust a day. With "Relationships fade..." off nothing is ever
lost. The Law feature is asked only through `ISocialLaw` (`SocialLinks.Law`, null until somebody sets it): with a
record and a reputation under 60, people who are punctual, careful or reliable lose 0.3 trust a day. The Home and
health feature is told through `ISocialWellbeing` (`SocialLinks.Wellbeing`, null likewise): stress down after a
good outing, a meal eaten.

### Things to do together (`OutingScene`)

A plan is agreed on the phone (they offer what they would like first, then the rest; a place within 1.8 km is
found then and named; two times are offered from their free hours and the place's): one plan at a time, saved.
An hour before, a reminder and the plan on screen with a marker (the game's own guider, so it is on the city map
too). They may cancel by text beforehand (5 % plus traits, mood and rain; never the reliable) and would still like
to another day. They arrive on their own time (the punctual early, the late late: `SocialPlan.Arrives`, a time of
its own a few minutes before or after the one agreed) and, if the player is there first or would see them appear,
walk in from out of view; otherwise they are waiting. See "Arriving, and what holds a scene up" below.

| outing | where (the game's business types) | a head | inside | needs |
|---|---|---|---|---|
| coffee | coffee shop | 6 | 25 min; both come out with a cup (`cup_hold`, `sip`) | |
| a meal | fast food restaurant | 24 | 70 min | |
| drinks | nightclub | 32 | 110 min; outside again, if it went well, a toast (`cheers`, a `wine_glass` each) | friend |
| the cinema | cinema, theater | 18 | 130 min | |
| a workout | gym | 15 | 60 min, -14 energy | |
| a look round your business | a business the player rents | | 30 min | friend |
| a walk | outside any business | | 160 m walked with them beside the player; twice on the way they talk as they walk (`walk_talk` / `walk_talk_l` by the side they are on, upper body over the game's walk); by one of the game's street benches the panel offers "Sit down", once a walk, and a rest counts as 40 m (Family, "Sitting") | |
| a drive | outside any business | | 650 m driven with them in the back, then pull over | friend, a car |
| a lift (favour) | from one address to another | | to the door they name; petrol money | |
| helping them move (favour) | between two doors 22 to 75 m apart | | three boxes carried (`carry_box_hold`; the box is `moving_box` when the bundle has it, else `parcel_m`) | friend |

Greeting: the greeting clips are made for two people facing 0.80 m apart, so on hello the player is held where they
stand and whoever is met walks the last steps up to them (`CityPerson.StepTo`: to 0.80 m on the line between the two,
the agent's own stopping distance allowed for; four and a half seconds at most), both turn to each other, and then
both halves start at the same moment: `social_cheek_kiss` for a partner (dating or married), a real hug for close
friends and family (`social_hug_a` on the player, `social_hug_b` on the other, whole body, its full 2.6 s: they finish
0.35 m apart with their arms round each other), `social_handshake` for everybody else (upper body). A whole-body
greeting is played only if the two really ended 0.70 to 0.90 m apart; otherwise the upper body's `social_hug` /
`social_handshake` at 0.65 to 1.0 m as before, and failing that `wave_hello` on both. Then the dialogue panel: a chat about something
that really happened (see below) and who pays (my treat, split, or theirs if it was their idea or they are
generous). A place found shut becomes a walk. Afterwards: what they thought, a card with what it cost and did,
a word and a wave, and they walk off out of view (after a good outing with a wave over the shoulder as they go:
`goodbye_wave_walk`, the right arm over the game's walk). The toast after drinks (`OutingScene.RaiseToast`): `cheers`
is made for two facing 1.05 m apart (at that distance the two bowls meet at its `clink` mark, 0.8 s; at 0.80 m they
pass each other), so the player is held, the other steps to 1.05 m, both turn, and both start it together with the
glass at the clip's grip; only if they really stand 0.97 to 1.13 m apart, otherwise a word (`talk_a`) as after any
other outing. The glasses stay in the hands (the clip ends holding one) until the goodbye. Walking out on it costs; being taken away from it (a time
skip, hospital) does not.

#### Arriving, and what holds a scene up

Whoever is met is put out when the player is within 130 m of the place and their own time has come. Where they
stand is found and tested (`SocialPlaces.Standing`: pavement within 1.5 m of the front of the door with room, else
a ring of 1.2 to 5 m; from the third try any pavement within 6 m). If the player would see them appear (there
first, or looking that way from under 60 m) they walk in instead: from pavement out of view 14 to 40 m off
(`SocialPlaces.WalkIn`), by a route that was measured (`NavMesh.CalculatePath`, complete, 45 m at most: a point 30 m
away across the road can be a block's walk) and then confirmed by their own agent (`SocialCast.Walk`). "Here comes
Mom" is shown only once she exists and is walking. The walk is bounded: if the route ends short, or 40 s have gone,
they are put at the place (nobody looking at them or at it), or put nearer out of view and walk again (twice at
most, 25 s each), or stop where they are. Wherever they stand, that is where they are met: the mod's marker
(`SocialScene.MarkPerson`: their first name and the distance) is on them from the moment they are out until hello
is said, they wave at 11 m and the prompt comes at 3.4 m. With no way in to be found in four tries they appear by
the door as soon as the player looks another way (after six seconds more, regardless). One log line per person put
out: position, distance to the player, distance to the navigation mesh, the route's length and whether it is whole.

The director asks once a tick what, if anything, has the player (`SceneBreak`, one value per condition); every
ending is logged with that value and what the scene was doing, and whatever the player had been told about is
called off with one notification saying why and what becomes of the plan.

| what happens | before hello | after hello |
|---|---|---|
| the player goes into the venue, or any other building (`Indoors`) | held, not ended: they wait where it was agreed. If they are due and the player is within 130 m (by the door of the building they are in) they are put by the venue's door facing the street while nobody can see, and a notification says "Mom is here, waiting outside Five Cups"; the marker is on them when the player comes out | held: they wait outside. A text after 40 s ("I'm still outside"), gone after 100 s (as being left behind: -4 affection, -3 trust). Timers do not run meanwhile |
| the street is not running for 6 s (`StreetNotReady`: an activity fast-forwarding) | held, as above (nobody is put out meanwhile) | held, as above |
| time is skipped by something else (`TimeSkip`) | held; on the way out the usual clock applies (stood up after 75 game minutes) | ended: cut short, no blame, part of the credit, said with the reason |
| a shift of the mod's jobs (`JobShift`) | ended, the plan kept and said so ("still on: outside Five Cups at 09:00"); the scene comes back when the shift is over | ended: cut short, said with the reason |
| hospital, death (`Hospital`, `PlayerDead`) | ended, the plan kept and said so | ended: cut short, said with the reason |
| a load, the game going (`Loading`, `NoGame`, `Shutdown`) | ended, logged; the plan is in the save | ended, logged |
| the option switched off (`OutingsOff`, `SocialOff`) | the plan is dropped and said so, nobody is stood up | ended: cut short |
| a delivery job of the game's own (`currentPlayerMission`) | nothing: the player is free on the street. Only the game's destination guider is left to the job; the mod's marker shows the way | nothing |
| the game's standing objectives ("pay off your bank loans") | nothing: they are not missions (`currentPlayerMission` is only ever a delivery or food-delivery job) | nothing |
| driving up | they wave at 11 m; "Park up and walk over" within 40 m (a drive or a lift: pull up beside them) | |
| arriving early | "You are early. Mom will be here about 08:57"; she comes at her own time, walking in | |
| arriving late | a text 20 game minutes after the time ("I'm outside Five Cups. Are you far?"), over 20 late costs trust, stood up at 75 with a text | |
| rain | may make them cancel beforehand (by text; a walk more than the rest); once somebody is out the scene plays as it is | a walk goes on |
| met a little way from the door (they had stopped short, or the player went to meet them) | hello is said there | they walk to the door from there (up to 30 s), or the walk starts there |

Somebody walking up to the player (`MeetScene`: a stranger, an introduction, a friend dropping by) is put out the
same way (measured route, one log line, the marker on them, sent on again if their route ends short of the player),
waits outside a door the player went through for as long as they would have waited to be spoken to (45 s), and
whenever they give up or are cut off the player is told ("Dana saw you were busy and went on their way").

There is no "bar", "restaurant" or "park" in the game's business types: drinks are at a nightclub, a meal at a
fast food restaurant, and a walk is along the pavement wherever the player leads. Indoors nobody sits: the game's
chairs are there, where the cast does not go. On a walk the two can sit on a street bench (Family, "Sitting"), in the
game's own sitting pose; `sit_chat_a` / `_b` are made for a seat of the mod's own and are played by the living city's
two friends on a bench (`BenchChatActivity`).

### Talking, texts and calls

A chat (`SocialTalk`) picks one of twenty topics that fit right now, never the one that person talked about last:
the player's crash, arrest or knock-down and the street it was on, an accident or crime of the last day (from the
news log), the best shop's takings or loss yesterday (the game's own `FinancialSummary`), the shift worked today
(`JobDirector.EarnedToday`), being ill (`ModState.IsSick`), a break-in, the weather, their own day, job and
district, the last thing done together, their birthday coming. Each has two answers and a reaction to each.

They get in touch themselves (`SocialDirector.Hourly`: each waking hour a chance of 9 % plus 2 % per person known): an invitation, a
favour (a lift, a loan repaid or excused in five to nine days, help moving), news of their day, an introduction,
or a reaction to the player's crash, break-in, arrest or wealth (10 k, 50 k, 250 k, 1 M, 5 M: the supportive are
pleased, the envious are not). Texts are the game's own: a `TextMessage` in that contact's thread, with the game's
"new message" notification. Never more than "Most texts in a day" (3) from everybody together, one a person a day,
none from 22:00 to 08:00, none at all with the switch off (what must be said, such as a plan called off, is then a
notification). Somebody close may ring instead: a prompt for 22 s, the interaction key answers, and the call opens
in the contacts app with what they rang about. An invitation or favour is answered by calling them.

### The City Ledger (`News`)

`News.Report(NewsKind kind, Vector3 where, string detail = null, int amount = 0)` from any feature: a slot in a
fixed array of 48, no allocation, no lookup, a return at once when neither the paper nor the social life is on.
Call sites today: `TrafficAccidents` (accident staged with its severity, pedestrian down, hit-and-run),
`CrimeDirector` (a crime staged, by catalogue id), `CrimeScene.Conclude` (an arrest), `OffscreenCrimes` (break-in,
car break-in), `CrashConsequences` (the player's crash), `PlayerKnockdown`, `PoliceAttention.BeginCustody`.
Each morning after six the paper is written once (`News.Compose`): the worst two things of the last day with the
street (the nearest building's address) and hour (on a day of four or more, the worst one and a count of the rest), the weather, a business piece from
the player's own books, a piece about somebody the player knows, and one or two of forty fillers that do not
repeat until all have run. It arrives as messages from the `reallife-ledger` contact (one notification) and is
read out when that contact is called. The log, the paper and the fillers seen are in modData.

### Rules kept

- Nothing in the save the game cannot load: `reallife:social*` strings in modData, and contacts made exactly as
  `Phone/PhoneContacts` makes its own (id, description key, the call dialog set again on every load).
- Idle cost: three comparisons a frame; once a second a few more. A scene ticks five times a second; per frame
  only its marker, its key and (via the living city's service) a turn on the spot. No per-frame allocation.
- Two characters at most, the social life's own; placed only where `SocialPlaces.Standing` / `JobSpots` /
  `CitySpots` found and tested room; they move only by their navigation agent; everything is given back in
  `End`, which the director calls once on every way out, and again by `Cast.ReleaseAll` / `Body.Release`.
- A fault is logged once, the scene is ended with everything given back, and the social life is off for the session.
- A scene that is held up costs one question a tick (`Holding`); the street is asked about only every second and a
  half, and only before hello with the player indoors.

### Test buttons (Social)

All say why when they cannot (indoors, the street not running, a scene on, a shift, nothing near). The outing ones
play the real thing (walk-in, marker, hello, the activity) and record nothing. `SocialDirector.TestOutingWithMom()`
is "an outing with Mom, here, now": a walk agreed for the pavement where the player stands (`SocialPlaces.Here`, any
pavement within 8 m, no business needed), on foot or from a car. Its label is `reallife_social_test_mom`; its row
is still to be added to the chain in `RealLifeOverhaulMod.cs` (outside this folder):
`.AddButton("reallife_social_test_mom", () => SocialDirector.Instance?.TestOutingWithMom())`. Until then "Test: a
walk with the chosen person, here" does the same thing (the chosen person is Mom until another is chosen): it is
agreed for the pavement where the player stands, and looks for a business only if there is no pavement within 8 m.

### On screen: the game's own interface (Social)

The mod draws nothing of its own for the social life (`SocialHud` is gone). None of this has been seen in the game.

| screen | what it is now | if it cannot be made |
|---|---|---|
| the plan ("Coffee with Mom", the line and the time) | the headline and two lines of a copy of the game's job panel at the top right (`ShiftCard`), shown while a scene is on, from an hour before the plan | the line goes out as a notification each time it changes |
| the place, or the person to meet | the game's destination marker (`GameUi.GuideMark`), moved when the place moves by more than a metre. It was the game's marker already; the mod's own plate for a person is gone | not shown while a delivery job of the game's or a job of the mod's has the marker; tried again every tick |
| "say hello", "talk", "pick up a box", "put it down", and the phone ringing | the game's panel over the person (`GameUi.DoorPanel`, following them), or over the player, with one button the game labels with the key (`reallife_social_button_*`) | tried again every tick while the game's panel shows another place |
| what an outing cost and did | one of the game's notifications, 11 seconds | none needed |

Cost: no per-frame work of the mod's. The scene still ticks five times a second; what is on screen is brought up to date on that tick and the panels are told only what changed. The key is no longer read by the mod: the game runs the button on its key or a click.
Log lines with diagnostics on: `Social check (prompt panel)`, `Social check (plan card)`, `Social check (marker)`, each ending PASS or FAIL.

### Not verified in the game (Social)

The game could not be run for the changes of 0.36: that a character can be put on the street and walks while the
player is inside a building; what `GlobalReferences.walkingSpeedWalk` is (the 40 s and 45 m bounds assume about
1.2 m/s); that the navigation agent's own route agrees with the pedestrians' filter (it is asked, and refused if
not); how the marker on somebody looks beside an incident's marker (they are separate plates; the social one keeps
85 px lower at the top edge); the wording and length of the new notifications on screen.

### Wish-list (drop in by id, no code change)

**Delivered and played:** `social_handshake`, `social_hug`, `social_hug_a` / `_b`, `social_cheek_kiss`, `cheers`
(with `wine_glass`), `walk_talk` / `walk_talk_l`, `goodbye_wave_walk`, and the prop `moving_box`. **Delivered, not
played by Social** (each needs a scene or a placement that does not exist here): `box_lift_with` / `_b` (the boxes of
a move are carried one each), and the props `beer_bottle`, `popcorn_tub`, `gift_box`, `flowers_bunch`, `gym_bag`,
`newspaper_ledger`. **Played by the living city instead** (see "Clips nothing played"): `sit_chat_a` / `_b`,
`eat_standing` with `takeaway_box`, `point_show`, `phone_answer`.

Clips (Humanoid, in place, from and back to the standing idle unless said):

| id | length | loop | what |
|---|---|---|---|
| `social_handshake` | 1.8 s | once | right hand out to a hand 0.80 m ahead at 1.05 m, two shakes, back. Made for two people facing 0.80 m apart; marks `grip` 0.5, `release` 1.3. Played on both, upper body |
| `social_hug` | 2.6 s | once | a step in, arms round somebody 0.80 m ahead (finishing 0.35 m apart), a pat, a step back. Played on both, upper body today: as a whole-body clip it needs the root to stay put |
| `social_cheek_kiss` | 1.6 s | once | for a partner: lean in from 0.80 m, back |
| `sit_chat_a`, `sit_chat_b` | 6 s | loop | seated on a seat 0.45 m up, talking / listening: for when benches can be used |
| `cheers` | 1.8 s | once | right hand lifts a glass from 1.05 m to shoulder height, a small forward tap, back; prop `wine_glass`; mark `clink` 0.8 |
| `eat_standing` | 3.4 s | once | a bite from something held in the right hand; prop `takeaway_box` in the left |
| `walk_talk` | 5 s | loop, upper body | head turned 25 degrees to the left, small right-hand gestures: over the game's walk, for the one on the right (a mirrored `walk_talk_l`) |
| `point_show` | 2.2 s | once, upper body | an open hand sweeping from the left to straight ahead: "this is my place" |
| `phone_answer` | 1.6 s | once, upper body | phone from the pocket to the right ear; then the game's `TalkingPhone` |
| `box_lift_with` | 2.2 s | once | one of two lifting a long box together, mirrored pair |
| `goodbye_wave_walk` | 1.4 s | once, right arm | a wave over the shoulder while walking away |

Props (pivot at the grip unless said): `wine_glass` 0.07 x 0.20 x 0.07, pivot the middle of the stem;
`beer_bottle` 0.07 x 0.24 x 0.07, pivot 0.08 m up; `takeaway_box` 0.14 x 0.09 x 0.11, pivot the middle of the
underside; `popcorn_tub` 0.16 x 0.20 x 0.16, pivot the middle of the underside; `gift_box` 0.20 x 0.14 x 0.20 with a
ribbon, ground centre; `flowers_bunch` 0.22 x 0.42 x 0.22, pivot the wrapped stems 0.10 m up; `gym_bag` 0.50 x
0.26 x 0.24, pivot the middle of the handles; `moving_box` 0.45 x 0.35 x 0.35 printed "FRAGILE" (until then
`parcel_m`), ground centre; `newspaper_ledger` (the `newspaper` prop with the Ledger's masthead), as `newspaper`.
### Careers

Six employers stand behind the help-wanted boards: each board's building is one employer's office (matched by
the kind of service building, best fit first: `CareerCatalogue.Premises`; the boards are the same in every game,
so the employers are too). Walking up to a board that is an office, the key speaks to its boss, who stands by it
(the careers' own cast of two: `JobCast` with a seeded look, so the same face every time; put out only when it
cannot be seen from close by, shelved when the player is 100 m off). On a day the player is on the rota there,
whoever is on the same shift waits on the other side of the board and waves when the player walks up. With no
board in the bundle, or fewer than six, the employers without an office are spoken to through the **Work**
contact on the phone.

A shift's time is never said as a bare hour. `Jobs/CareerClock` is the one formatter: `Span(hour)` gives the start
and the end in the game's clock format ("13:00 to 17:00", or with AM and PM under the game's 12 hour option), and
`SpanWithWindow` adds when clocking on opens (`EarlyHours` before) and from when it is late (`OnTimeHours` after).
The boss's rota offer, the Work contact's "next shift", its too-early refusal, the reminders and the clocked-on
line all pass it as `{time}`; the shift's panel shows it beside the employer's name; the Planner's rows carry the
same two times as minutes and format them with its own `PlannerText.Time`. At an office board the game's panel
over a place offers "Speak to the office" on the game's interact key (`JobDirector.Boards`); the mod reads no key
there and draws no prompt.

| employer | grows out of | references needed | ranks (what the shift is) |
|---|---|---|---|
| Brightline Promotions (Tasha Greene) | flyers | 0 | Leafleter (the flyer round) -> Street team lead (a shift in charge) |
| Brightside Contract Cleaning (Dolores Whitlock) | cleaner | 0 | Cleaner -> Crew lead (in charge) |
| Uncle Lou's Street Eats (Lou Ferrante) | drinks cart | 0 | Cart vendor -> Licensed cart owner (the cart is rented by the day, 60, and the takings are kept: 11 a customer less 3.50 stock) |
| Fairway Markets (Anand Rao) | shelf stocker | 1 | Stockroom assistant -> Shift lead -> Assistant manager (both in charge) |
| Hudson Parcel Co. (Marge Kowalski) | courier | 1 | Courier -> Senior courier (7 parcels, the firm's van free, +10 %) -> Dispatcher (the planning desk) -> Depot supervisor (in charge) |
| Five Borough Cabs (Sal DeMarco) | ride-hail | 2 | Cab driver (4 fares a shift) -> Premium driver (a Luxury-kind car by `Driving/CarCatalogue`, passengers mind 30 % sooner, fares +25 %, tips x2) -> Owner-operator (fares +45 %) |

The game has no taxi business type (taxis are transport the player buys), so the cab ladder ends at
owner-operator and has no hook into a business. The game's one employer job (the cashier) is left exactly as it
is: an hour worked at it is counted, read-only, once an hour (`JobHelper.IsPlayerWorking` outside the player's
own shops), shown on the CV, and every eight such hours count as one shift of experience when signing on with
the retail chain.

- **Signing on** is face to face at the office or by phone (Work contact, "Apply for a job": the six employers in turn, the same page as the boss at the office, so the same checks and sign-on). The boss says what the work is in their own manner, then offers the three rotas
  in turn (Mon/Wed/Fri 08:00, Tue/Thu/Sat 13:00, Sat/Sun 10:00). References are counted as: +1 for the Gig Line's
  reference letter, +1 for each employer left on good terms, -1 for each left with a marked-down reference.
  Experience carries over: levels in the employer's own gig (two shifts each above the first) and in a related
  gig (one each) are credited towards the first review, never the whole of it.
- **The rota.** A shift is one run of the rank's work, clocked on to from the phone (Work) or at the office, from
  two hours before its hour. Up to an hour after is on time; up to three hours after is late (a mark, and an
  hour's basic docked); after that it is a no-show (a mark). The phone reminds three hours before. Three marks
  and the employer lets the player go with the reference marked down and a week's ban. Desk and lead shifts are
  clocked on to within 45 m of the office.
- **Sick.** "I'm not well today" is accepted, and a missed shift is excused, if `CareerLinks.Health` says a sick
  note covers the day, or with no health feature if the mod's own sickness (`ModState.IsSick`) is on.
- **Pay.** What the work itself pays (piece pay, by the rank's factor) plus the rank's basic wage for each game
  hour of the shift, four at most, paid when the shift ends well. The payslip lists both and a tax line that
  reads zero: the game takes no tax from wages (its cashier job pays hours x rate in full; `TaxHelper` taxes
  business sales and property only).
- **Reviews** are due every three rota shifts and are held with the boss. The numbers read out are the ones the
  outcome is decided from (`CareerCatalogue.ReviewScore`): 20 + 60 x the share of shifts on time (late counts
  0.4), -12 a complaint (a failed shift), -6 a crash, -10 a police stop on the job, plus a point per dollar of
  tips a shift up to 20. 70 or more is a promotion, 55 a raise (+1 an hour, three at most per rank), under 35 a
  mark.
- **Leaving.** Notice is two more shifts (or seven days) and ends with a good reference if three shifts were
  worked and fewer than two marks stand; walking out marks the reference down and bans for a week.
- **The CV** is two pages of the Work contact's call: employer and rank, employment history (eight spells kept)
  with references, shifts and earnings; then gig levels, the reference letter, cashier hours and the record
  (police stops on a job; a law feature's line if `CareerLinks.Law` is set).
- **Booked sets.** From busker level 2 the Work contact books one set a day at an open business nearby: it is
  marked on the map, begins when the player walks up to its front, pays 45 from the venue on top of the hat for
  a set at least half played, and tips are 30 % better.

| rank | basic / h | the work pays | about, per game hour |
|---|---|---|---|
| Leafleter, Cleaner | 4 | as the gig | 14 to 19 |
| Stockroom assistant, Cart vendor | 5 | as the gig | 21 to 24 |
| Courier | 6 | as the gig | 50 to 66 |
| Senior courier | 9 | 7 parcels, firm's van, +10 % | 65 to 80 |
| Dispatcher | 20 | 14 a plan x (0.25 + 0.75 x quality), 3 plans | 27 to 31 |
| Street team lead, Crew lead | 18 | 12 a call made well, 4 calls | 24 to 30 |
| Shift lead | 20 | the same | 26 to 32 |
| Assistant manager | 26 | the same, +20 % | 32 to 38 |
| Depot supervisor | 30 | the same, +20 % | 36 to 42 |
| Cab driver | 5 | 4 fares, as the gig | 55 to 85 |
| Premium driver | 8 | fares +25 %, tips x2 | 80 to 120 (needs a Luxury car) |
| Owner-operator | 0 | fares +45 %, tips x2 | 90 to 130 |
| Licensed cart owner | 0 | 7.50 a customer, less 60 a day for the cart | 30 to 45 |

Set against the game: cashier 25 an hour, its own staff 12 to 18, the van job about 100. Everything but the two
top cab ranks (which need a car costing tens of thousands) stays under the van job. The per-hour column is worked
out from the catalogue's figures, not measured in play. **Owners are paid less:** every job's pay, freelance or
employed, is multiplied by `CareerCatalogue.OwnerFactor`: 1 with no business, 0.85 with one, 0.70 with two, 0.55
from three on.

**The dispatcher's desk** (`DispatchJob`, `DispatchList`): three batches of six parcels round the depot; each
parcel goes on one of three vans (three a van) with the choice keys, 55 s a batch, the interaction key takes one
back. The board is the game's list window (`GameUi.DeskList`): the parcels still waiting as a note at the top (how far and which way from the depot), then an entry for each van with what it carries and how long its round is, and a button to send the next parcel on it, and an entry to take one back. The same choices stay on the game's panel over the desk (the keys). The window opens with each batch; the cancel key closes it for that batch. If the game's list window cannot be copied the panel is all there is (`Jobs check (dispatch list) ... FAIL`). The mod's own map is gone; nothing in this has been seen in the game. A plan's cost is the driving (nearest parcel first, then
home) plus a fixed amount per van sent out; the best and worst of the 729 possible loadings are found by trying
them all, and the plan is paid by where it falls between them. Two poor plans (under 0.4) are a bad shift.

**A shift in charge** (`LeadJob`): two of the team stand near the office doing what they are doing: writing up
(`take_notes`), smoking (`smoke`) or stuck (`arms_crossed`). The lead walks up to each and makes the call that
fits (praise, a word, a hand), then decides two incidents from a deck of six, each a choice of three with one
good, one passable and one bad answer. Calls made well are the units paid and counted.

**Optional doors** (`Jobs/CareerLinks.cs`; each a static that is null until somebody sets it, every use wrapped):

| the career offers / asks | interface | used for |
|---|---|---|
| offers | `ICareerAcquaintances.Met(personId, nameKey, employerNameKey, isBoss)` via `CareerLinks.Social` | a social feature picking up the boss and the two colleagues as acquaintances |
| asks | `ICareerRecord { bool Clean; string Summary; }` via `CareerLinks.Law` | the record line on the CV |
| asks | `ICareerSickNote.Covers(day)` via `CareerLinks.Health` | sick notes; without it `ModState.IsSick` |
| offers | `CareerLinks.EmployerId`, `TitleKey`, `ShiftDueToday(day)` | anybody who wants to know where the player works |

State: one line, `reallife:career`, in modData (`CareerState`). A shift in progress is carried by the jobs' own line
(`reallife:job_live`, see "A shift across a save"): a load pays the basic wage for the hours clocked and counts the shift.

Test buttons (options, Jobs): sign on with the next employer (cycles all six, on a rota that works tomorrow);
work a shift at my current rank now; my review is due now (opens the review face to face); promote me one rank;
start a dispatcher shift; start a shift in charge; book a set near here. Test shifts go on no record.

Wish-list for the asset agent. The clips are already asked for by id, before today's stand-in, so they drop in with no code change; the props would each need a line of placement code and are listed as wishes only:

| id | what | used by |
|---|---|---|
| clip `desk_plan` | standing at a waist-high desk, moving markers on a board, 6 s loop, upper body | `DispatchJob` (today `clipboard_write` over the upper body) |

| clip `staff_struggle` | hands on hips, a shake of the head, looking down at something, 6 s loop | `LeadJob`'s "stuck" (today `arms_crossed`) |
| prop `dispatch_desk` | a lectern-height desk with a wall map, 1.2 x 1.4 x 0.5 m, read from +Z | (needs placement code) stood by the board on a dispatcher shift |
| prop `employer_sign` | a small wall plaque (0.6 x 0.3 m), six variants, one per employer name | (needs placement code) above each office's board |

## Business pressure

**What the game's rivals already do.** `BigAmbitions.Rivals` is a real simulation: 19 rivals own businesses and
buildings; a "special rival" per neighbourhood sends a message once the player has one revenue business there
and becomes active at three, after which its timeline fires three attacks (`RivalDefenseHelper`): price cuts of
45 to 65 % on the player's best sellers for 5 to 8 days, 3 to 7 competitor businesses opened to push demand
down, and 10 to 24 of the player's best employees poached at once (`EmployeeInstance.PoachByRival`: the
employee texts the offer with accept and decline buttons). `CompetitionHelper` opens and shuts AI businesses
daily, and `RealEstateHelper` has competitors buying buildings. Rent never changes after signing and there is no
landlord behaviour, so "a rival buys your building" is not added. The mod's rival moves therefore fill the
early game: they are not started in a neighbourhood whose special rival is active, and the mod's one poaching
*is* the game's own offer, made to a single employee at +12 %.

Everything is in `Scripts/Business`: `BusinessCatalogue` (every kind, every number; game-free), `BusinessState`
(one line, `reallife:business`, in modData; game-free), `BusinessGame` (the only code that touches the game's
business fields), `BusinessIssues` (what each kind does), `BusinessDirector` (the clock, the morning roll, the
off switch, the tests), `BusinessDesk` (the phone contact), `BusinessVisit` (somebody who comes into the shop).

**Rules for all of it** (`BusinessCatalogue`):
- nothing in the first 7 days after the mod first sees a save; never on a business known for less than 7 days;
- one open issue per business, two overall; a business is left alone for 3 days after one ends; each kind has
  its own rest (8 to 30 days); one roll a morning at 09:00: 8 % at frequency 1 to 30.5 % at 10;
- a charge is a share of what that business itself took a day (the game's order history, last seven days)
  between a floor and a ceiling. One the player did not choose (a fine, a repair, an adjustment) is also never
  more than 6 % of the cash in hand; nothing is ever forced, and a charge that cannot be paid falls back to the
  issue's other outcome;
- footfall changes are -12 % to +10 % of customers, one per business, with an end day, and only on businesses whose
  promotion figure the game itself recomputes and can carry the change in full (not within a few points of 0 or 100);
- a closure is 12 game hours at most;
- an employee's satisfaction is never pushed below 20 (the game makes them resign under 10); a wage only goes up;
- **the off switch** closes every open issue without its outcome, puts every promotion figure back and reopens
  every business the mod closed; each group has its own switch; "easy business" leaves the opportunities only.

**How the effects reach the game** (all in `BusinessGame`, each logged with before and after):
- *Money*: `GameManager.ChangeMoneySafe` at the business's address, never forced. Marketing answers are booked
  under the game's marketing category, fines and licences under licensing fees, bonuses and the lunch under
  salary expenses, so they appear in that business's own income statement.
- *Footfall*: the game computes customers from `0.5 + 0.75 x promotion / 100`. The mod recomputes the game's own
  figure (`BusinessHelper.UpdatePromotion`), sets `promotion.total` to the value that gives the wanted percent
  (`BusinessCatalogue.PromotionFor`), and has the game redraw the customers still to come today
  (`CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness`, which the game itself calls when a business
  opens or closes). It is put back on top each game hour and redrawn after midnight. The game recomputes the
  figure daily, on entering the building and when marketing changes, so a save loaded without the mod heals by
  itself the next day. The player sees it: the promotion figure on the business's page moves.
- *Closure*: `BuildingRegistration.TemporarilyClose`, the switch on the business's own page; the reopening hour
  is remembered and done by the mod. Removing the mod mid-closure leaves the business closed until the player
  unticks it.
- *Theft by an employee*: added to `stolenItemsCost`, which the game books as Theft in the statement and clears
  each night. No cash moves.
- *Wages*: `EmployeeInstance.hourlyWage`, as the game's own poach counter-offer writes it. *Leaving*: the
  game's own resignation path (what an ignored complaint does). *Deliveries*: `DeliveryContract.nextDeliveryDay`.

| kind | when | what it does | choices | caps |
|---|---|---|---|---|
| Competitor undercuts | has a promotion figure; no active game rival | -10 % customers, 5 days | marketing push (0.6 of a day, 80 to 1200: +4 % for 3 days) / match prices (0.3, 40 to 600: dip halved) / quality (cleanliness 80 and satisfaction 60: over in 2 days) / ride it out | -10 % |
| A rival wants your staff | a poachable employee, a rival with businesses | the game's own offer, +12 %, 3 days | answered in the employee's messages: match or let go | one employee |
| Smear in the paper | has a promotion figure | -6 %, 4 days | right of reply (0.4, 60 to 800: ends) / lawyer (0.25, 50 to 500: 55 % ends it and +3 % for 2 days) / ignore | -6 % |
| Protection racket | takes 150 a day or more | see below | pay / refuse / police / security | weekly 60 to 600; repair 80 to 900; closed 4 h |
| Health inspection | food business | tomorrow 10:00, on the game's cleanliness figure | book a contract clean (0.2, 40 to 400: dirt to a third) or clean it yourself | 75 passes (+4 % for 3 days); 50 to 74 a warning and a second visit in 2 days; under 50 a fine (0.5, 100 to 1500); under 30 or unpaid: closed 6 h |
| Fire-safety inspection | any | tomorrow 10:00: cleanliness 55 and the facility score 40 | book a contractor's check (0.25, 50 to 500: certain pass) | fine 0.4, 80 to 1000; both failed or unpaid: closed 4 h; pass +4 % for 3 days |
| Licence renewal | any | a fee (0.5, 60 to 900) due in 3 days | pay now | late: x1.5; unpayable: closed 6 h |
| Strike threat | 2+ staff, wages 12 % under the game's formula on average | 3 days to answer | agree (each wage to 95 % of the formula, +15 % at most) / bonus of 8 hours' wages each / refuse (half the time a walkout) | closed 8 h; satisfaction -8 |
| Theft by an employee | stock to steal, security level 10+, staff | a loss (0.15 of a day, 20 to 300) booked as theft | dismiss / final warning (30 % repaid) / let it go | 300 |
| Argument | 2+ staff | two fall out | side with either (+5 / -12 satisfaction) / lunch (0.05, 20 to 120: +6 both) | unanswered: -6 both |
| Late delivery | a wholesale delivery within 2 days | a day late unless | express (15 % of the delivery, 20 to 400) / take the delay | one day |
| Supplier's surcharge | the same | 8 % of the delivery, 15 to 500 | pay / haggle (30 % + half the negotiation skill, to 80 %; trains it) / switch haulier (a third, a day late) | 500 |
| Complaint that spreads | satisfaction under 50 or cleanliness under 55 | -6 %, 3 days, naming the weakest of the game's four figures | apologise and voucher (0.15, 20 to 300: ends) / ignore | -6 % |
| Praise that spreads | satisfaction 70+ | +6 %, 3 days | share it (5 days) | +6 % |
| Tax audit | a week of books | 3 days to answer | accountant (0.07 of a day, 100 to 800: 70 % clean, else a third of the adjustment) / alone (40 % clean, 45 % an adjustment of 0.14 of a day, 100 to 1500, 15 % a refund to 500) | adjustment also at most 6 % of cash |
| Bulk order | takes 100 a day | 0.4 of a day (120 to 2000) if the shop is open tomorrow at 12:00 | take it / decline | one every 8 days |
| Feature in the paper | has a promotion figure | | give the interview: +8 % for 3 days | +8 % |
| Award | satisfaction 75+, cleanliness 70+ | a prize (0.3, 100 to 1000), +5 % for 4 days | accept | every 30 days |

**The racket.** A collector asks for a weekly sum; if the player is standing in that shop he walks in and says so
himself, and the desk opens on it when he reaches them. *Pay*: collected every seven days, creeping 10 % a week
to 1.5 times the first demand; the desk can stop it or go to the police at any time. *Refuse* (also the default
after 3 days): in 2 to 4 days, with odds of 0.7 x (1 - security / 150), a brick through the window (a repair,
and the shop shut 4 hours); then nothing for 14 days. *Police*: three days, then odds of 0.35 + security / 200
(0.85 at most) of a case; with one, the collector is caught at the shop next day at 14:00 (played in the shop
through `Crime/ArrestSequence` if the player is in it, otherwise told) and **the racket is over for good in that
save**; without one it counts as a refusal. *Security*: three days to get the game's security level to 50 (the
game's own guards and cameras); then he gives up for 28 days, otherwise it counts as a refusal.

The crime system has no public entry for staging a scene by name (`CrimeDirector.StageNow` takes a family, for
its test buttons), so the vandalism is told, not played; the arrest uses `ArrestSequence.BeginIndoors` directly.

**In the shop** (`BusinessVisit`): the collector, an inspector (walks in, looks the place over with a clipboard
at two spots, comes to the player, leaves; the verdict follows an hour later) and the sting are played by the
street scenes' own pool through `ActorRole`: in at the real door, on the shop's navigation mesh, out by the door,
110 s at most, ended with everybody back in the pool the moment the player leaves or a crime scene starts.
Nothing of the shop is touched. Nobody free in the pool: no visit, and the issue goes on by phone.

**The Business desk** is a phone contact (the game gives a mod no way to add a phone page): its call goes
through the open issues with their deadlines, reads each choice out with its cost before it is taken, and its
messages are the record of everything that happened.

Test buttons (options, Business): one per group (each press starts the next kind of that group on the business
the player is standing in, or their first; the grace periods, rests and odds are waived, and a kind that cannot
exist here says exactly why), open the Business desk here, and resolve all (also forgets the racket, ended or
not).

Wish-list for the asset agent (the two clips are already asked for by id before today's stand-in; the props need placement code):

| id | what | used by |
|---|---|---|
| clip `collector_lean` | leaning in, one hand flat on a counter, the other gesturing low, 5 s loop | **delivered, deliberately not played by `BusinessVisit`**: it is made for a counter 0.975 m up with its near edge 0.32 to 0.36 m ahead and reads as a man bent over nothing anywhere else. A visit is played 1.7 m from wherever the player stands, and the game's shop furniture says nothing of a counter top's height or edge, so no such place can be found. The collector (and the man of a sting) talks with `talk_a` and, once the desk is open on his issue, waits with `arms_crossed`; the game's `IDontThinkSo` gesture only without the clip bundle. One line in `BusinessVisit` (`TalkClip`) if a measured counter ever exists |
| clip `inspect_look` | clipboard in the left hand, looking up at a ceiling and down at a floor, 6 s loop | the inspector between spots (today `clipboard_write`) |

| prop `window_boarded` | a plywood sheet 1.2 x 1.5 m to stand against a shopfront | (needs placement code) after the vandalism, for the hours the shop is shut |
| prop `closed_notice` | an A3 notice on a stand, "CLOSED BY ORDER" | (needs placement code) a business closed by an inspector |
## Justice

Courts and prison (`Scripts/Justice`). Tickets stay tickets; an arrest, or a charge for something serious, opens a
**case**. A case has charges (each with its own evidence strength), a bail decision, a court date two to four days
ahead, a hearing that has to be attended, and an outcome. Everything is one line in the save's `modData`
(`reallife:justice`); a save made at any moment loads without the mod with the player standing in the street,
because the player is never put anywhere the base game does not know.

| file | what it is |
|---|---|
| `JusticeLaw` | every charge, cap, fee and formula: evidence, the case against the accused, bail, the sentence, appeals (game-free) |
| `JusticeState` | what a save remembers, and its one line of text (game-free) |
| `JusticeBridges` | `Justice` (the door for the rest of the mod), `Justice.Bridges` (Law, Licences, Social, Transport, each with a default), `JusticeReports` |
| `JusticeDirector` | the clock and the switchboard: hourly business, what is waiting for the player, the watchdog, the test buttons |
| `JusticePanel` | the screen: courtroom, cell and Legal file are pages of it, in the game's own list window (`GameUi.DeskList`), three answers at most, answered by a click |
| `Courthouse` | which building it is, the sign on its frontage, where one stands to go in |
| `LegalCounsel` | the law firms, their rating and fee, what counsel can do before the hearing; the Legal contact and its conversation |
| `CourtHearing` | one hearing, page by page |
| `PrisonStay` | custody (hours) and prison (days) as time passing; `JusticeHold`: everything changed on the game while the player is held |
| `CommunityService` | a played litter-picking shift |
| `LegalFile` | the Legal file page and what can be done from it |

### On screen: the game's own interface

Nothing in the justice system is drawn by the mod any more (`JusticeHud` and the full-screen panel are gone). Not seen in the game.

| screen | what it is now | if it cannot be made |
|---|---|---|
| the hearing, bail, the cell, the prison mornings, the summary, the Legal file | pages of `JusticePanel`, which is a `GameUi.DeskList` (a copy of the subway station window): the title and who is present under the headline, what is said as a note with the speaker's name over it, and an entry with a button for each answer (at most three) with its hint under its name. Answered by a click. Locked (no cancel key) while the player is held; the Legal file, which holds nobody, can be put away with the cancel key. While the screen is faded to black by the game the list stands on the canvas of the game's black cover, and goes back to the HUD's when the cover is gone | the justice system switches itself off for the session with one line (`Justice check (page list) ... FAIL`); the player is let go and cases and sentences stay in the save |
| the courthouse door ("Attend the hearing", "Hand yourself in", "Report for service") and the lawyer's desk ("Speak to a lawyer") | the game's panel over a place (`GameUi.DoorPanel`), told once a second | tried again the next second while the game's panel shows another place |
| the court's marker | the game's destination marker (`GameUi.GuideMark`) | not shown while a job or an outing has the marker |
| community service: the litter, the pieces picked and the hours owed | the panel over the litter with "Pick it up"; the marker as above; the figures on a copy of the game's job panel (`ShiftCard`) | the pieces picked are sent as notifications |

Cost: nothing per frame. A page is written once when it is shown; the shift's tick (five a second) tells the panel and the marker only what changed.
Log lines with diagnostics on: `Justice check (page list)`, `Justice check (door panel)`, `Justice check (litter panel)`, `Justice check (community card)`, each ending PASS or FAIL.
Behaviour that changed: the keys 1 to 3 and the interact key no longer answer a page (a click does); the portraits and the drawn courtroom are gone (the names of those present are under the headline), and the portrait sprites in the bundle are unused.

### What happens, in order

1. **A case is opened** by `Justice.Arrest(report)` (taken in) or `Justice.Summon(report)` (charged by post). What is
   reported on the same day joins the same case; never more than three cases are open. Evidence, 0.05 to 0.97:
   0.30 for the accusation alone, +0.12 a witness (three at most), +0.22 filmed, +0.25 caught at it, +0.06 for having
   run, and a few hundredths either way per charge. A case opened on probation carries a breach charge as well.
2. **Bail**, put to the player on the panel as soon as nothing else has them: released on their own promise (a
   misdemeanour, no record, did not run); bail of a quarter of the charges' fines, at least 200 and at most a fifth of
   cash, returned on appearing (or a bondsman for a tenth, not returned; or stay in custody and be heard when the
   court next sits); or refused (a grave charge with flight or two convictions, or a warrant) and held until then.
3. **Review**, the day after: the prosecutor drops any charge whose evidence is under 0.34. A case left with nothing
   is dismissed: an apology, and compensation of 400 + 150 for each hour held + any roadside fine (the wrongful
   arrest).
4. **A lawyer** (the Legal contact, or the key at a law firm's desk): the public defender (free, quality 0.25),
   yourself (0.10 to 0.60 by the game's own Lawyer skill), or one of the game's law firm businesses
   (`ba:businesstype_lawfirm`, read from the save's building registrations; the player's own firm acts for free).
   The game keeps a satisfaction score per business but leaves it at 50 for those it runs itself, so a firm's rating
   is two to five stars fixed by its address, moved a star for every 25 points of that score from 50; quality 0.35
   to 0.83, 0.05 more for a case prepared at the office. A city with no law firm gets three sets of chambers in its
   office buildings. Fee: 600 / 1 800 / 3 500 by the gravest open charge, times 0.8 (one star) to 2.0 (five), never
   more than 30 % of cash; a quarter down, the rest over six days. Counsel says what the odds really are, can make
   one motion to dismiss (every charge under 0.34 + 0.12 x quality goes) and can put a plea deal on the table.
5. **The court date**: a text when it is set, another the evening before, another and a toast that morning (and the
   courthouse becomes the map destination if the player had none), a toast in the last hour. The court sits 9:00 to
   17:00. Missing it: a bench warrant, bail forfeit, a charge of failure to appear; the next police stop
   (`Justice.PoliceContact`) takes the player in, as do officers at their door after three days; or they hand
   themselves in at the courthouse. Someone who could not have come (in prison, in hospital) has the date moved.
6. **The hearing** (`CourtHearing`): called; charges read; plea. Guilty: a quarter off. The deal: weak side charges
   dropped, the main charge reduced where it has a lesser form, 40 % off and no prison unless what is left is
   grave. Not guilty: three or four rounds (the officer, a witness, the footage, the flight; the closing), each
   with a choice: testify (once: -8 if the evidence is thin, +6 if it is strong, and the hint says which), stay
   silent and let counsel cross-examine (-6 x quality), object (sustained as often as counsel is good: -4, else
   +1), call a friend as character witness (Social bridge: -8), put in your own business's footage and receipts
   (a business of the player's with security: -10, nothing if caught at it); at the close speak (remorse: -2 and a
   tenth off the sentence if the evidence is strong, +3 if it is thin), let counsel close (-8 x quality) or say
   nothing. **Verdict**, per charge: case against = evidence + 4 a prior conviction (12 at most) - (quality - 0.2)
   x 30 + the choices; guilty if that is more than the court's doubt, a number fixed by the case's seed when it
   was opened. Every term of that sum is on the verdict page. Nothing in a hearing is a fresh roll of dice: a
   reload replays it the same way for the same choices.
7. **The sentence**, with the reason for each part on the page (table below).
8. **Afterwards**: one appeal within three days (fee a tenth of the fine, at least 300, at most 15 % of cash;
   chance 5 to 60 % by how thin the evidence was and how good counsel was, shown before paying; won: the fine back
   or struck off, the conviction off the record, 600 a prison day and 150 a custody hour paid). Civil suits (a
   crash that hurt someone: one in two; damages of 800 or more: one in five; one suit at a time, a week apart;
   claim at most a fifth of cash): settle for 60 %, contest (10 to 70 % by counsel; lost: claim + 15 %), or
   ignore (the claim, by default). The record is sealed after 30 days with no new conviction, no open case and
   no probation.

### Charges and sentences

Nominal values, for a first offence convicted after a trial. `Lesser` is what a deal can reduce it to.

| id (`JusticeLaw.`) | charge | class | fine | community | custody | prison (cap 7) | probation | licence | lesser |
|---|---|---|---|---|---|---|---|---|---|
| `Evading` | Evading the police | misdemeanour | 1 500 | 8 h | 12 h | | 5 d | 3 d | |
| `Reckless` | Dangerous driving | misdemeanour | 1 200 | 6 h | | | 4 d | 3 d | |
| `HitRun` | Leaving the scene of a crash | misdemeanour | 2 500 | 8 h | 24 h | | 6 d | 5 d | |
| `HitRunInjury` | ...with injury | felony | 8 000 | 16 h | 48 h | 3 d | 10 d | 10 d | `HitRun` |
| `HitRunDeath` | Leaving the scene of a fatal crash | grave | 25 000 | | 48 h | 7 d | 14 d | 21 d | `Manslaughter` |
| `InjuryDriving` | Causing injury by dangerous driving | felony | 4 000 | 12 h | 24 h | | 7 d | 7 d | `Reckless` |
| `Manslaughter` | Causing death by driving | grave | 15 000 | | 48 h | 5 d | 14 d | 14 d | `InjuryDriving` |
| `Unlicensed` | Driving while suspended | misdemeanour | 1 500 | 8 h | | | 5 d | 5 d | |
| `Assault` | Assault | felony | 3 000 | 12 h | 24 h | 2 d | 7 d | | `Resisting` |
| `Theft` | Theft | misdemeanour | 1 000 | 8 h | | | 5 d | | |
| `Robbery` | Robbery | felony | 8 000 | | 48 h | 4 d | 10 d | | `Theft` |
| `Vandalism` | Criminal damage | misdemeanour | 800 | 6 h | | | 3 d | | |
| `Resisting` | Resisting arrest | misdemeanour | 1 200 | 6 h | 12 h | | 4 d | | |
| `NoShow` | Failure to appear | misdemeanour | 1 000 | 4 h | 12 h | | 3 d | | |
| `Violation` | Breach of probation | misdemeanour | 1 500 | | 24 h | | 5 d | | |

How a sentence is put together (`JusticeLaw.Sentence`):

- **Fine**: the charges' fines added, times the plea (1, 0.75 guilty, 0.6 deal), less 15 % for having complied with
  the police and 10 % for remorse the court believed, plus a quarter for each prior conviction (three at most),
  half as much again on probation. Then **bounded**: never more than 35 % of the cash in hand (but never under 250)
  and never more than 60 000; what was fined at the roadside for the same thing is taken off. Up to 15 % of cash
  it is paid at once; above that it is seven daily instalments.
- **Prison**: the gravest charge's term, a day more for each further felony, a quarter more per prior, scaled to the
  cap option (default 7, 3 to 14) and never over it, never under 2. A repeat offender (two convictions) gets 2 days
  for any felony. A deal on anything short of grave turns it into custody. With "No prison: fines and custody
  only" it becomes 48 hours of custody and 500 a day on the fine.
- **Custody** (no prison): the longest among the charges, 12 h more per prior, never over 48 (the station's cap).
  A first misdemeanour conviction has it turned into community service, an hour for every three.
- **Community service**: the charges' hours added, 24 at most, due in 7 days. A shift is 8 pieces of litter for 4
  hours (half an hour a piece if given up). Not done in time: 100 an hour as a fine (at most a tenth of cash) and two
  days more probation. With the option off the hours are 60 each on the fine instead.
- **Probation**: the longest among the charges + 2 days per prior, 21 at most; after prison at least 5, from the
  release, plus twice any days not served on parole. On probation a new conviction costs half as much again and
  carries a breach charge.
- **Licence**: the longest among the charges, half as much again per prior, 30 days at most. Driving in that time and
  being stopped is a new charge.

**Money never spirals.** Every amount is bounded against cash when it is set (fines 35 %, fees 30 %, bail and civil
claims 20 %, appeals 15 %, impound storage 10 %). Instalments carry no interest and no penalty; one is only taken if
it leaves the player 50; a debt that could not be collected five days running is written off (a fine into community
service, an hour per 150, twelve at most; anything else simply ends).

### The courthouse

One of the game's own buildings, the same in every game: the tax office (the one government building the game has),
else a bank, else a school, the largest of its kind, ties by address; a city with none of those: its largest office.
One sign on its frontage, against the wall 4.6 or 6.2 m from the door (the help-wanted boards stand at 2.4), where
`CitySpots.Shopfront` found wall, pavement and room: the modelled `courthouse_sign` when the prop bundle has it,
otherwise a navy board on two posts lettered in the game's font. **Localisation:** the model carries its lettering in
the mesh (COURTHOUSE, "EQUAL JUSTICE UNDER LAW": English only) and the code adds none to it (`Courthouse.Build`
returns as soon as the model is in place); only the stand-in is lettered from `reallife_justice_sign`, so a
translated game shows the translated word only without the prop bundle. A translated sign needs the model's board
left plain (as `precinct_sign`'s name strip is) and the code's lettering put on it at (0, 1.52, 0.027). On a court day a brass marker points at it; the
interaction key in front of the sign goes in. No room for a sign: the pavement at the door serves. No pavement
either, or the option off: hearings are held by video link from the Legal file.

### Prison

**Custody in hours is not prison.** Everything a court orders in hours (remand until it sits, a custody sentence, a
hold on an arrest by Licences: `JusticeState.JailHoursPending`, never more than `JusticeLaw.CustodyCapHours` = 48) is
handed to the Law bridge (`JusticeDirector.BeginCustody`), which serves it in the nearest police station's holding
cell (`Law.Custody.Hold`, see "A court's hold" under Law). Only when the bridge says no (custody or the stations
switched off, no station, somebody already held) is it `PrisonStay.Custody`: the fade and the time skip below, where
the player stands. Prison, in days, is always what follows here. `Justice check (custody): ... PASS` says which of
the two served it.

Presented as time passing, with the game's own time skip (the one its hospital uses): fade, the player out of their
car and out of any building and put down on the pavement outside the courthouse (or wherever the Law bridge says),
and then a day at a time. Each morning the panel: the **parole board** on the morning after two thirds are served
(behaviour - prior convictions + what is said to it, against the days served), then **how to spend the day**: the
work detail (40 on the prison account, behaviour +1), the yard (released with full energy and a mood lift; the game
has no fitness figure for the player, so that is what fitness is), or the library (+1 to the game's own
`ba:skill_lawyer`, which is also what representing oneself runs on); then **association**: keep to yourself
(behaviour +1) or mix (55 % a favour and +1, 25 % nothing, 20 % trouble and -2); then, seven days in ten,
**something happens**, dealt from twelve without a repeat in a term, with two answers each. A friend (Social
bridge) visits on day 2 and rings every third day after. **Good behaviour**: a day off for every four points, a
third of the term at most. Energy and hunger are held at 55 or more while the clock runs and set to at least
75 / 85 at the gate. On release: the summary, what was earned, probation from that day, and the car the player was
taken from brought to them (Transport bridge, else `VehicleTow` with 25 a day storage, 300 and a tenth of cash at
most).

Businesses: **no efficiency penalty.** The game runs them through `GameManager.RunMainGameTick` during the skip
exactly as during any other; no safe hook for an owner's absence was found in it, so none is applied. Rent, loans
and wages are the game's own and carry on.

### Safety

- The player is held only by the panel (game paused with the game's own `GameSpeedController`, the game's own
  navigation blocker) or by a time skip behind the game's own fade. All four are remembered by `JusticeHold` and put
  back together. The director's watchdog, once a second: a panel open with nothing behind it, a routine that has not
  shown a sign of life for 25 seconds of running game, or a hold with neither panel nor routine, and the player is
  released (a prison term cut short that way is treated as served). A fault anywhere does the same; three and the
  feature stays off for the session, with the cases kept.
- Idle cost: one time comparison a frame; once a second a handful of integer tests; the day's business once per
  in-game hour. Per frame work only while the panel is open (two key tests), a shift of community service is on, a
  prompt is in reach, or it is a court day (one marker). Nothing allocates per frame.

### For other features

They call us (all safe at any time, with the feature off or no save loaded):

```csharp
// arrested for something serious (police / custody code); tickets are never reported
Justice.Arrest(new JusticeReport(JusticeLaw.HitRun) { Witnesses = 1, InTheAct = true, Complied = true, PaidAtScene = fine, HeldHours = hours });
// charged without being taken in
Justice.Summon(new JusticeReport(JusticeLaw.HitRunInjury) { Cameras = true, Fled = true });
// the police have the player in front of them (a stop, a station desk): a bench warrant is served, a suspended licence charged
Justice.PoliceContact(driving: true);
// somebody else paid for the player's driving: now and then they sue
Justice.CrashVictim(damages, injured, killed);
```

and may read `Justice.Wanted`, `Justice.OnProbation`, `Justice.DrivingSuspended`, `Justice.InPrison`,
`Justice.Priors`. We call them through `Justice.Bridges`, each with a default that works alone:

| bridge | set by | what it is asked | default |
|---|---|---|---|
| `Law` (`ILawBridge`) | `Scripts/Law` | `PriorConvictions`; told `Convicted`, `Cleared`, `RecordSealed`; `Custody(hours, reasonKey, released)` (remand and custody sentences: served in the station's cell, see "A court's hold" under Law); `Holding` (in a cell of the station's, or a save says so: never counted absent from court); `ReleaseSpot(out spot, out name)` (the spot, and the station's name that the release text uses: "at Midtown Precinct", else "at the courthouse") | the justice system's own record; a plain time skip; not held; the courthouse front |
| `Licences` (`ILicenceBridge`) | `Scripts/Licences` | `SuspendDriving(untilDay)` (0 lifts it); `DrivingSuspended` | the date kept in `JusticeState` |
| `Social` (`ISocialBridge`) | `Scripts/Social` | `CloseFriend(out name)`; told `Stood(name, visit)` | the mod's friend contact, if rung within the last 7 days |
| `Transport` (`ITransportBridge`) | whoever keeps the cars | `ReturnCar(car, near, daysAway)` | `VehicleTow.BringNear` and a storage charge |

```csharp
Justice.Bridges.Law = new MyLawBridge();   // once, when the feature starts
Justice.Bridges.Law = null;                // when it is destroyed: the default comes back
```

Hooks in the mod's own code today (one line each): `PoliceAttention.Bust` and `PolicePursuit.Catch` arrest;
`PolicePursuit.ChargeForPeople` and `BillLater` summon (a death, or leaving the scene); `PoliceAttention.Ticket`
is a police contact; `CrashConsequences.ChargeLiability` and the two pursuit settlements report a crash victim;
`NearbyInteraction` leaves the interaction key alone while `Justice.TakesInteractKey`.

### Wish-list (assets; nothing waits on them: a prop or a clip of these ids is picked up with no code change, a sound or a sprite needs one line)

| kind | id | what | used |
|---|---|---|---|
| prop | `courthouse_sign` | free-standing sign about 1.4 x 1.9 x 0.45 m, ground centre, **read from +Z**, "COURTHOUSE" on a dark board between two posts or a stone plinth | `Courthouse`: replaces the plain board with no code change |
| prop | `litter_pile` | a small heap of street litter (a can, a carton, crumpled paper) within 0.4 m, ground centre | `CommunityService`: replaces the three plain shapes |
| prop | `litter_grabber`, `litter_bag` | a pick-up tool for the right hand (0.9 m) and a refuse sack for the left | not made: `litter_pick` as delivered picks with the bare hand |
| clip | `litter_pick` | **delivered**: 1.8 s, a step and a squat, the right hand closing on the ground 0.55 m ahead and 0.05 m right of the middle at its "grip" mark (1.1 s) | `CommunityService`: on the key the game walks the player (`PlayerController.SetGoal`) to the place the clip is measured from, they are turned so that the pile is under that hand, held, and the clip is played; the litter is switched off at the clip's own "grip" mark and they are let go at its end. A step that has not arrived within 0.10 m after 2.5 s gives the prompt back; after two such in a shift the litter is picked from where they stand (one log line). `kneel_work` from where they stand without the clip |
| clip | `court_stand`, `court_swear` | standing at a rail, hands on it; right hand raised | a 3D courtroom, if one is ever built. `oath_hand` (delivered) is the raised hand: **not wired**, a hearing is a dialogue panel and nobody is played in it |
| sound | `gavel` | two raps | the first and the verdict page of a hearing |
| sound | `cell_door` | a steel door closing | the prison fade and lights out |
| sprite | portraits | judge, prosecutor, counsel, officer, inmate, visitor, clerk: 178 x 196 busts | nothing now: the list has no pictures, so these are unused |

### Not verified in the game

Nothing here has been run in the game. Verified against the decompiled code and by the compiler: the time skip,
the pause, the navigation blocker, the fade, leaving a vehicle and a building, the warp of the player's navigation
agent, the skill increase, contacts and messages, the dialogue panel, building registrations, the destination
guider. Not verified, and to be looked at first in a play test: the panel's place in the draw order (its canvas is
at sorting order 30 000) and whether the cursor is free while it is up (it keeps an element of its own selected,
which `GameManager.ShouldBlockKeyboardShortcuts` and the mod's cameras take for an open screen; keys 1 to 3 work
either way); whether the city has law firm businesses at all (else the chambers stand in); which building the
courthouse turns out to be and how the sign sits on it; warping the player across the city out of doors; the look
of the portraits and scenes; the length of a 24-hour time skip in real seconds.

## Cars

The game's cars are all 1400 kg (the Vord Pony 1200), share one four-speed gearbox and one engine curve, have
no air drag, and are stopped by a speed limiter at the "max speed" of their vehicle type (50 to 100 km/h).
`Driving/CarTuning` gives the car being driven (never traffic) the performance of its kind instead: each
vehicle type has a target in `CarCatalogue`, and `CarModel` works out from the car's own mass, wheels, grip,
clutch and power curve the engine power, gear ratios and drag that meet it. Top speed is where the engine's
power in top gear equals drag plus rolling resistance (the limiter is switched off), so a car pulls hard to
50, steadily to 100 and builds slowly to its top speed. Everything changed is recorded and put back when the
player gets out, changes car, switches the option off or the mod unloads.

| vehicle type (`ba:vehicletype_`) | in game | kind | game: kW / final drive / limiter | target 0-100, top | applied: kW / final drive / drag (N per (m/s)²) | first gear ends | model: 0-50, 0-100, after 20 s |
|---|---|---|---|---|---|---|---|
| `honzamimic` | Honza Mimic | Economy | 45 / 8.51 / 60 | 12.5 s, 170 | 76 / 3.05 / 0.541 | 45 km/h | 4.2 s, 12.5 s, 124 km/h |
| `vordtiaravic` | Vord Tiara Vic | Saloon | 80 / 6.42 / 70 | 10.0 s, 190 | 107 / 2.73 / 0.587 | 45 | 4.4, 10.0, 143 |
| `bima320` | BIMA 320 | Saloon | 100 / 7.08 / 70 | 8.5 s, 210 | 125 / 2.75 / 0.525 | 45 | 3.8, 8.5, 158 |
| `mersaidis500` | Mersaidi S500 | Luxury | 175 / 6.76 / 80 | 6.5 s, 240 | 169 / 2.61 / 0.497 | 48 | 3.0, 6.5, 185 |
| `missamvillian` | Lytte L6 | Luxury | 175 / 8.51 / 80 | 6.2 s, 240 | 152 / 2.61 / 0.438 | 48 | 2.3, 6.2, 180 |
| `petrollsfanton` | Petrolls Fanton | Luxury | 175 / 7.61 / 80 | 6.8 s, 240 | 160 / 2.83 / 0.473 | 48 | 3.2, 6.8, 182 |
| `limo` | Petrolls Executive Limousine | Luxury | 75 / 8.00 / 80 | 10.5 s, 180 | 102 / 3.41 / 0.685 | 45 | 4.6, 10.5, 139 |
| `vordpony` | Vord Pony | Sports | 100 / 6.33 / 80 | 5.8 s, 240 | 163 / 2.36 / 0.468 | 48 | 2.8, 5.8, 194 |
| `ferdinand112` | Ferdinand 112 | Sports | 150 / 5.15 / 90 | 5.0 s, 265 | 217 / 2.14 / 0.477 | 53 | 2.3, 5.0, 212 |
| `mersaidimgagt` | Mersaidi MGA GT | Sports | 200 / 4.80 / 100 | 4.7 s, 280 | 234 / 2.02 / 0.438 | 56 | 2.2, 4.7, 222 |
| `anselmoaf90` | Anselmo AF90 | Sports | 200 / 4.80 / 100 | 4.5 s, 290 | 247 / 2.05 / 0.419 | 58 | 2.2, 4.5, 229 |
| `umcnunavut` | UMC Nunavut | Utility | 150 / 7.92 / 80 | 9.0 s, 190 | 119 / 3.60 / 0.705 | 45 | 4.0, 9.0, 150 |
| `vordv150` | Vord V150 | Utility | 130 / 7.62 / 80 | 10.0 s, 180 | 107 / 3.80 / 0.740 | 45 | 4.4, 10.0, 142 |
| `umcdesert`, `umcdesertdeliveryjob` | UMC Desert, Delivery Van | Van | 55 / 11.14 / 50 | 16.0 s, 140 | 66 / 4.89 / 0.924 | 45 | 4.9, 16.0, 110 |
| `mersaididash` | Mersaidi Dash | Van | 55 / 11.14 / 65 | 14.5 s, 150 | 71 / 4.56 / 0.804 | 45 | 4.6, 14.5, 116 |
| `deliverytruck` | Vord Courier D500 | Truck | 46 / 15.25 / 55 | 18.0 s, 135 | 57 / 5.41 / 1.106 | 45 | 6.7, 18.0, 105 |
| `freighttruckt1` | Freight Truck T1 | Truck | 35 / 15.25 / 50 | 20.0 s, 130 | 52 / 7.09 / 1.146 | 45 | 7.3, 20.0, 100 |
| `electricscooter`, `handtruck`, `flatbed` | | Untouched | | as the game made them | | | |

The "applied" and "model" columns are what `CarModel` gives for the game's own vehicle data (read from the
game's files with a small command-line check, at both sliders 100 % and with the eased throttle); the mod
works the same sums out again from the live car each time the player gets in, and writes them to the log
once per car ("Car tuning: ..."). They are a model of the physics, not a measurement: the options button
"Measure this car" times a real run for comparison.

- **A type the table does not know** (a car from another mod) is placed by `CarCatalogue.Guess`: no fuel
  tank, carried in the hands or under 600 kg: untouched; the game's own max speed up to 55: truck (18 s, 135);
  up to 65: van (15 s, 145) with cargo room for 14 or more, else economy (12 s, 170); cargo 16 or more: utility
  (10 s, 180); max speed 95 or more: sports (4.8 s, 270), 85 or more: sports (5.5 s, 250); otherwise luxury
  (6.5 s, 240) if flagged so, else saloon (9 s, 200). To give it its own figures, add one `case` to `CarCatalogue.For`.
- **The two sliders** (70 to 150 %) scale the top speed and divide the 0-100 time. Top speed is never taken
  past 350 km/h; with a top speed turned down below 125 km/h the power is found as if for 125 and the car is
  then geared and dragged down to what was asked.
- **Gearing**: top gear turns the engine at 95 % of its limit at top speed; first gear runs out at a fifth of
  top speed (45 to 58 km/h), never so short that the engine's best torque spins the tyres; the gears between
  are evenly spaced. Driven gently the box changes up at 45 to 58 % of the rev limit by kind, with the pedal
  down at 95 %; a change takes 0.12 s (sports) to 0.45 s (trucks).
- **The eased throttle** ("Smooth throttle"): the car's own input handler calls `CarTuning.EasePedal` once a
  frame, which moves the pedal towards what the key asks: 0 to full in 0.7 s, slowly at first, and back in
  0.15 s. A tap gives a gentle pull-away; an analogue trigger keeps its own feel. The brakes come on over
  0.25 s through the physics' own `brakes.actuationTime`. In reverse the game swaps the pedals, and the one
  that drives is the one eased.
- **Brakes** are left alone: with the game's tyres they stop a car from 100 km/h in 19 to 56 m and from its
  new top speed in 66 to 122 m.
- A car the sums cannot be done for (no combustion engine, no automatic gearbox, missing wheels or curve) is
  left exactly as the game made it, with one line in the log saying why.

## Law

Police precincts, custody that is served at one, and a record that follows the player. Options group "Law":
six toggles (precincts, custody, arrests for serious offences, effects of record and reputation, walk-in
stations, police stations on the map) and seven test buttons.

### Precincts

The game has no police station: no building type, no model, only the parked police car prop
(`Vehicles/ParkedVehicles/PoliceCar`). So one existing building in each of the six city districts is
designated (never the Hamptons, never one of the game's own service buildings, never a home, never the
player's own premises). Since players walk in, it has to be **a unit that stands empty and to let**
(`AvailableForRent`, business type empty, no layout, no items, its DLC owned): the game loads the bare interior
of such a unit through its own door, and nothing of the game's lives in it. Of the district's empty offices,
large shop units, warehouses and shops, the likeliest fourteen (by kind, then nearest the middle of the
district) have their frontage measured once per city load, and the best score wins (`Precincts.Measure`):
a sign band of its own over the shopfront 25, flat wall either side of the door at chest height 3 a place
(eight places) and above head height 2 a place (four), pavement depth 5 a metre (three), a kerb with the road
beyond it 10, and a little for the kind of building. The result is written into the save by address
(`reallife:law_precincts`) and stays, with two exceptions that are both said to the player:

- a save from before stations could be entered is looked over once (`reallife:law_station`, first field): a
  precinct in a unit that cannot be walked into moves to one that can, and one whose frontage scores 30 less
  than the best gives way to it;
- a precinct whose unit has since been let (to the player or to a rival) moves to another empty one in its
  district (`Precincts.Keep`, every five seconds; never while the player is inside it or being held).

A district with no empty unit keeps a precinct that cannot be walked into: its front desk answers at the door
(the same panel, with the front office drawn behind it). The log names all six once per load, with each
one's score and whether it is walk-in.

- **Map**: the precincts are the first user of `Core/MapPlaces` (see "Map places" under the rules): a
  "Police stations" row in the map's filter list under Special buildings, a shield marker per precinct with its
  name on it (on the map while the row is ticked, and in the street within 100 m), a click on a marker for a
  destination, and the game's own card for the building reading the precinct's name, "Police station" and
  "Open day and night", with the game's own "set destination" button. The option "Police stations on the map"
  takes all of it away.
- **Frontage** (`PrecinctFrontage`), for the one precinct the player is within 260 m of (one set of objects,
  built once and moved; taken in beyond 330 m). Every piece is measured against that building before it goes
  up; one that does not fit is left off and the log says why (one line per visit, `Law: frontage: ...`):
  - the *fascia*: the building's own wide sign over the shopfront (which would read "for rent") is switched
    off, with its hanging logo signs, and a police-blue board of exactly its measured box put in its place,
    lettered POLICE and the precinct's name. The game's own `UpdateSign` puts the signs back when the frontage
    is taken in;
  - the standing `precinct_sign` with the precinct's name, against a flat stretch of wall beside the door
    (`CitySpots.Shopfront`, then the board and both posts tested against everything solid);
  - a `station_awning` over the door, only on flat wall above it, with its whole volume clear, pavement
    under it, and never across the fascia;
  - twin `blue_lamp`s on brackets either side of the door, 2.2 m up, the same distance out on both sides
    where the wall allows (their glass changes to a brighter paint when the street lights come on);
  - a `police_shield_emblem` and a `notice_board` on flat wall (a list of places either side of the door,
    tried in turn);
  - POLICE lettered on the door, where rays find the door's own face flat;
  - up to six `bollard`s on the kerb strip either side of the way in (never straight in front of the door)
    and a `flag_pole` further along: just beyond the edge of where people walk, only where the road is the
    other side of that edge, on level ground, touching nothing; each cut out of the navigation mesh;
  - up to three patrol cars at the kerb within 48 m (the game's police car prop, placed by the tests
    `PolicePresence` parks by, and clear of the bollards);
  - two officers either side of the door (`guard_stand`, or `officer_guard_idle` once it exists), and one
    thing at a time on a slow timer: an officer comes out of the door, walks to the nearest patrol car, talks
    on the radio and goes back in; an officer walks in off the street from out of view; and now and then,
    with the player within 75 m, a suspect in handcuffs (`cuffed_walk`, `CuffPose`) is walked in with an
    officer a pace behind. Whoever goes in is gone at the door, as the game's own shoppers are. All but the
    suspect are people the pool may take back at any moment.
  A wall piece needs a flat patch its own size (five rays square on to the wall that agree to 6 cm), a clear
  box in front of it, and no other piece on that patch. Nothing is put out or taken in where the camera is
  looking, except under a dark screen. **Not done**: tinted windows (the game clears every property block on a
  building's renderers whenever its map outline changes, so a tint could not be kept or safely put back).

### Inside a station

How the player gets in: the precinct's doors are given the game's own `alwaysOpen` flag (remembered and put
back), so clicking the building and walking to the door enters it as any open business; and within 5 m of the
door the interaction key is the station's (`LawDirector.TakesInteractKey`; the prompt says so) and calls the
game's own `BuildingManager.EnterBuilding`. Nothing about the unit is changed: it is still to let, its
registration, rent and ownership are untouched, and a save made inside one loads without the mod as the
player standing in an empty unit. If the game does not get the player in within seven seconds, its half-done
entry is put right and that precinct's front desk answers at the door for the rest of the session.

The room (`StationInterior`) is furnished the moment the game's "entered building" event fires, with the
screen still dark, and cleared on its "left building" event. Every unit is a different size and shape, so the
room is **measured**, on the navigation mesh the game walks its customers on: how far the floor runs straight
in from the door, how far to either side at each depth, whether a wall runs straight where something stands
against it. Each piece needs its whole footprint walkable; wall pieces go up only where a ray finds the
wall's real surface; what does not fit is left out and named in the log (`Law: <precinct>: inside: ...`):

- the **front desk** (`precinct_desk`) across the room 3 to 6.5 m in, in the middle where the room is wide,
  always with a way round one end; the precinct's name on its face; the desk sergeant behind it;
- a **waiting bench** (`precinct_bench`) along a side wall by the entrance with somebody waiting
  (`queue_wait`), and a `notice_board` on the wall opposite;
- one or two **office desks** along the side walls behind the counter, an officer at each (`take_notes`);
- the **holding cell** at the back: a square, clear back corner closed with two `holding_cell_bars` panels
  (the side one shortened to the depth there is), or the whole back of a room no wider than one panel; a bench
  inside; and a closed **evidence-room door** in the back wall beside it.

With **Furnished police stations** on (`law_station_furniture`, `StationInterior.Furnish.cs`), the game's own
furniture then goes round all of it. Each piece is a copy of one of the game's item prefabs
(`PrefabHelper.LoadPrefabAssetByName`), made under a switched-off holder so that none of its scripts runs, and
stripped while asleep of what makes it an item: the ground and direction indicators, attachment points and their
decals, navigation targets, the "for sale" sign, a counter's waiting line, and every script, collider and obstacle
(`Core/GameFurniture.cs`). What is left is the game's meshes and materials; a lamp keeps its light with its shadows
off. Every item prefab stands on the floor at its pivot with its front towards +Z (read from the game's own
prefabs); sizes are measured from the copy's meshes. Placed in the room's frame, only where the floor and a straight
stretch of wall have room, and never on floor already taken: the way from the door to the counter, round both its
ends, to the cell and in front of the bars, the sergeant's and the visitor's places and the officers' desks are
taken first.

- on the counter (sergeant's side) a `ComputerMonitor`, an `OfficePhone` and a `StackOfBooks`; on each officer's
  desk a monitor, a phone and a `DeskPlant01`;
- along the side walls, in front of the counter: a `WaterCooler`, a `GroundPlant03`, a `TrashBin`; behind it:
  up to two `OfficeLockers`, a `Printer`, a `StorageShelf`, a `CheapCoffeeMachine`, up to two `UniformLocker`, a
  plant; each with its back to the wall and 0.55 m kept free in front of it;
- on the walls, where a ray finds the surface: an `ExitSign` over the door, a `LargeWallClock` in the public part,
  two `FramedCertificate` behind the counter (never where the notice board hangs);
- up to three flush `CeilingLampSquare` down the middle where a ray finds a ceiling; a `CarpetBigBasic` in the
  waiting part where all of it is floor;
- in a corner cell big enough for it beside its bench: a `ToiletClassic` against the back wall and a
  `SinkWallModern` on its side wall.

One pass on the way in, under the game's fade: a few thousand walkable-floor samples, and the first copy of each
piece (kept for every later visit, moved to the next station's own places). The log line names how many pieces went
in. Off, the station is the counter, bench, desks and cell only. Not seen in the game yet: how the pieces look in the
units' rooms, whether every piece's front is the side the game's own placing uses (read from the prefabs' direction
indicators and seat points), and whether a unit has a ceiling a ray can find.

Furniture and bars are cut out of the navigation mesh. The sergeant is a pool character of the scene's own;
the other three are presence characters. A room too small for any of it keeps the front desk service where
the player stands.

**The front desk** (`StationDesk`; within 2.3 m of the counter the game's panel over a place (`GameUi.DoorPanel`) offers "Speak to the desk officer" on the interact key, at a door that can be walked into "Go into the station"): the list has an entry for each thing, with the cost or state under its name and the button off, with the reason, when it cannot be had; answers are notifications. Report a crime or an
accident (exactly what a 911 call would do about what is going on nearby, +2 police reputation for coming
in, and never a "wasted call"); pay outstanding fines (`ModState.PendingFine`, +1); the record (a second page: both
reputations, then a note for each line, newest first, and Back); collect a car from the pound (see custody; 250, brought to the kerb when the
player steps outside); apply for a licence (only if `Law.Bridges.Licensing` is set); a police report after a
break-in or a car break-in in the last seven days (15; reference `PR-<precinct>-<day>-<number>`; handed to
`Law.Bridges.PoliceReport` if set); hand yourself in (see below); visiting hours.

```csharp
Law.Bridges.Licensing = () => ...;                    // the desk then offers "Apply for a licence or permit"
Law.Bridges.LicensingLabelKey = "...";                // optional: its button's text
Law.Bridges.PoliceReport = (kind, reference) => ...;  // kind: Bridges.BreakIn / CarBreakIn / Accident
Law.Bridges.NoteIncident(Law.Bridges.Accident);       // something a report can be asked for has happened
Law.LawDirector.Inside                                // the precinct the player is standing in, or null
```

### Arrest and custody

| charge (`Law.Charge`) | when | hours | bail |
|---|---|---|---|
| `WastedCalls` wasting police time | the fifth 911 call about nothing in one day | 2 to 4 | 300 |
| `FledStop` fleeing a traffic stop | told to pull over, drove on, caught (`PoliceAttention`) | 4 to 8 | 1,500 |
| `HitAndRun` reckless driving causing a crash, leaving the scene | hit a car and drove off, caught (`PolicePursuit`); or crashed while fleeing a stop | 8 to 16 | 3,000 |
| `InjuryFled` injuring a pedestrian and leaving the scene | caught after driving off | 18 to 30 | 8,000 |
| `DeathStayed` causing the death of a pedestrian | stopped and waited: arrested on the spot | 24 to 36 | none |
| `DeathFled` causing a death and leaving the scene | caught after driving off | 36 to 48 | none |

Several charges: the longest in full plus a quarter of each of the others. A repeat offender (held before
in the last 14 game days) serves 15 % longer per time, at most 60 %; bail goes up the same way. **Nothing
is ever longer than 48 hours.** The fines are not in this table: whoever stops the player charges them,
once, exactly as before (`CrimeLaw`, `PolicePursuit`'s constants) and passes the sum to the charge sheet,
which shows it and charges nothing. Every figure is in `LawBook`.

What happens, in order (`Custody`):

1. The charge, the sentence and the precinct are announced, and the record gets its line.
2. Out of doors, with the car stopped: the player is taken out of it and held where they stand; an officer
   walks up behind them (from the patrol car that made the stop, otherwise from pavement out of view), the
   player's hands go up (`hands_up`, `hands_up_hold`), then the handcuffs: the officer comes to stand 0.9 m
   from them, the player is turned on the spot to have their back to the officer, and the pair of clips made
   for exactly that is started in one frame (`arrest_cuffed` on the player, `officer_cuff` whole-body on the
   officer with its last pose held; the officer's half only from 0.9 m give or take 0.25, the reach otherwise).
   At their `click` the handcuffs leave the officer's hand and `CuffPose`'s pair is on the wrists: one pair is
   ever seen. Then `cuffed_stand` (on the player's own body the way `JobBody` plays clips on it). Indoors, still rolling, knocked down, or with
   nobody to be had from the pool, this step is skipped: nothing is faked.
3. The screen goes dark. The player is taken out of any vehicle, building or car park and put on the
   pavement in front of the nearest precinct's door, facing the street, and that precinct is dressed.
4. The cell: a panel over the dark screen with the precinct's name, the charge sheet, the fines already
   paid and a clock that counts game time, while the game's own time skip runs to the release time (the
   same call its hospital and taxi use, so businesses, rent and everything else go on). Energy comes back
   as on the game's own benches and hunger is topped up when low. Three choices, by button or number key:
   sit it out; **pay bail** (not for a death), which ends it at once; **call a lawyer** (750, or 600 for
   somebody the public thinks well of), once, which strikes off a third of what is left.
5. Release outside the door: energy at least 45, hunger at least 50, a summary, a bad mood for a while,
   and the car that was left at the scene brought to a kerb space near the precinct for the city pound's fee (300, `LawBook.ImpoundFee`, the same for a car taken for driving without a licence)
   (`VehicleTow.BringNear`; a repair shop if there is no space; a message if it could not be moved).

**At a precinct that can be walked into** (and with "Walk-in police stations" on), steps 3 to 5 go on
inside: from the pavement the player is taken through the door by the game's own way in and put in the
holding cell before the screen clears, facing the bars and kept there. The cell's list is then at the
right of the screen with the cell in view beside it, and the time skip runs without the game's
blur. On release the screen goes dark for a moment, the player is at the front desk, is signed out by the
sergeant and walks to the door, which the game takes them out of in the usual way: release is from the front
door. The car left at the scene is then **at the pound** (`reallife:law_station`): any front desk has it
brought to the kerb for the impound fee. A room with no cell (too small, an odd shape) holds the player at
the desk behind the full-screen panel; if the game's way in fails, everything is as it was (the pavement, the
full-screen panel, the car brought round at once).

**Handing oneself in**: after getting away from hurting somebody the player is wanted
(`LawState.WantedCharges`: the charge they ran from). For seven game days the front desk offers "hand yourself
in": that charge for half the hours and half the bail, served in the cell like any other stay. Any arrest
settles it.

Safety, inside: the bars are solid, so on every way out of custody (release, the watchdog, a fault) the
player is put at the front desk, and if that cannot be done the bars are taken away; the room is cleared when
the player leaves it for any reason. A save made in the cell loads, without the mod, as the player standing
in an empty unit; with the mod they walk out and the rest of the stay is resumed (the precincts are only
designated once the player is out of doors).

Safety: the stay is one line in modData (`reallife:law_custody`). A save made in the middle has the
player standing on the pavement outside the precinct, which is all the base game sees if the mod is
removed. With the mod, a load resumes what is left, or releases at once if the time has passed, the option
was switched off or anything about it does not add up. `LawDirector` ends custody (everything put back,
the player free where they stand) if the routine shows no sign of life for 25 real seconds, if the save
says held and nothing is running, or on any fault; a stay never takes more than seven real minutes on
screen. If the player cannot be moved (the hospital has them, no way to the door) the arrest is dropped:
the fines stand, the record keeps the line.

#### A court's hold (`Custody.Hold`)

```csharp
// False, with nothing done, if custody cannot take them now (off, no precinct, somebody already held, the courts off).
bool Law.Custody.Hold(float hours, string reasonKey, int bail, Action released);
Law.Custody.HoldBailed            // read in "released": the hold ended because its bail was paid
Law.Bridges.CourtOn = () => ...;  // asked whether whoever orders holds still stands behind them (Wiring: Justice.Active)
```

Hours ordered by a court are served exactly where an arrest's are, by the same code: the wait for a free screen, the
dark screen, out of any car or building (`Transport`), the pavement outside the nearest precinct, through its door
into the holding cell (`GoInside`, `PutInCell`), the cell's list, the game's own time skip to the release time, the
front desk and the door (`Release`, `ReleaseInside`). A player who is already standing in a station is put in that
station's cell without going out. What differs, all of it decided by `CustodyState.Court`:

| | an arrest | a court's hold |
|---|---|---|
| in the street | the officer, hands up, cuffs | nothing is played: one notification, then the dark screen |
| the note in the cell | CHARGE SHEET: the charges and what was paid | COURT ORDER: the caller's sentence (`reasonKey`: `JusticeLaw.HoldRemand`, `HoldSentence`) |
| entries | sit it out, bail, a lawyer | sit it out; bail only if the caller set one (`CustodyList.Shape`); never a lawyer |
| hours | `LawBook.Sentence` | the caller's, bounded to 1 to `LawBook.MostHours` (48) |
| the police record | a line, closed with the hours served | no line: the conviction is the courts' own, and a second line would move the reputation twice for one offence. So it is not counted by `Record.TimesHeld` either |
| mood | `MoodDownBig` | `MoodDown`, as the courts' own time skip gave |
| could not be moved (at the wheel of a car that will not stop, no way to the door) | the arrest is dropped | the hours pass where the player is, behind the full-screen list: a court's hours are never let off |
| the courts go (switched off, the feature at fault) | | the hold ends at the next look (eight a second, with the clock line) and at a load |

A car the player was sitting in is remembered and treated as an arrest's: at the pound if they are let out inside,
brought to a kerb space for the impound fee otherwise. `released` is called exactly once, last thing in `Cleanup`, on
every way out (served, bail, the watchdog, a fault, the component destroyed); it is not kept in the save, so a hold
resumed after a load ends without it, and `ILawBridge.Holding` is what the courts ask instead.

Every guarantee of an arrest's stay holds for a court's because it is the same routine and the same state: the
watchdog in `LawDirector` (25 s without a sign of life, "the save says held, nothing is running", any fault),
`HeldMost` (seven real minutes on screen), `LeaveCell` and `OpenCell` for the bars, `Cleanup` on destroy.

**The save.** `reallife:law_custody` has two fields more at its end: `|1|<reason key>` for a court's hold (`|0|` for
an arrest). A line with nine fields, from before, is an arrest. `Active` on load is "has charges, or is a court's,
and has a release time". A save made in the cell loads without the mod as the player standing in an empty unit (the
base game ignores the key); with the mod they walk out and what is left is served, or they are let out if the time
has passed, the option is off, or the courts are off. A build of the mod from before this reads the nine fields it
knows, finds no charges and holds nobody.

One thing can come out of order: a save made during **remand** and loaded has its hearing waiting too
(`JusticeState.HearNow`). The courts' panel waits for `Custody.Active`, which is false from the load until the stay is
taken up again (once the player is out of doors and the precincts are designated), so the hearing can be held first
and the rest of the remand served after it. Neither is lost and nothing is stuck: the hearing pauses the game, and
the stay waits for `LifeEvents.IsSafeForEvent`.

Log lines with diagnostics on: `Law check (court hold): taken to <precinct>: in the holding cell: PASS` (or "no cell in
this room, held at the desk", "held outside the door"; "could not be moved, held where they are" is the FAIL),
`... signed out at the front desk of <precinct> after N h of M, then the door: PASS`, `... let out at the door of ...`,
`... a save made during the hold was loaded with N h left: resumed`, `... ended by the watchdog (...)`.

Not checked in the game: any of it. A screenshot of the cell with the COURT ORDER note and a single "Sit it out"
entry settles the list; the log lines above settle the path.

With "Custody" off, the two old routes fall back to what they did before (a 2 to 5 hour time skip where
caught). With "Arrests for serious offences" off, a death is only an arrest if the player drove off, and
911 calls about nothing only cost the fine.

Not wired, because the player cannot do them: stealing and violence on foot. `Law.Arrests` and
`Law.Custody.Arrest(charges, paid, patrol)` are there for whoever adds them: one row in `LawBook`, one call.

### Record and reputation

- **Record** (`reallife:law_record`, at most 40 lines): every ticket, every stay in a cell after an arrest (with its
  hours, fines and how it ended) and every time the player got away after hurting somebody. Hours a court ordered
  are not a line here (they are in the Legal file with the conviction).
- **Police reputation** (0 to 100, starts at 55): falls with each line by the amount in `LawBook` (a
  ticket 3, a fatal hit-and-run 25), rises 2 for every game day with nothing new on the record (up to 75)
  and with calls that led to an arrest.
- **Scrutiny**: two lines in the last five days and traffic offences are 15 % more likely to be seen;
  three, 30 %, and an officer on foot no longer warns before a jaywalking ticket; four or more, 50 %. A
  message says when it starts and when a clean spell has ended it.
- **Wanted**: after getting away from a hit-and-run the car is known for the rest of the day (the flag
  `PoliceAttention` already kept for fleeing a stop: every other offence is seen, fines half as much again).
- **Public reputation** (0 to 100, starts at 55): rises 4 with everything that earns the "helped" mood
  (staying with a casualty, reporting an accident, stopping a thief; three a day count), falls when the
  player hurts somebody, drifts back up to 55. From 70 a passer-by says thanks about once a day (a real
  pedestrian within six metres, a smile over their head) and the lawyer charges less; from 85 they press
  a few dollars into the player's hand.
- **The Record page**: the Police Records Desk contact on the phone reads out both reputations, how long
  since the last offence, whether patrols are watching, then the record line by line, newest first.

For other features (all safe to read at any time; empty or neutral until the save has loaded):

```csharp
Law.Reputation.Police            // 0..100
Law.Reputation.Public            // 0..100
Law.Reputation.Liked             // Public >= 70
Law.Reputation.Scrutiny          // 1 .. 1.5, a field read
Law.Reputation.GoodDeed()        // what ModState.AddModifier(ModState.Helped) already calls
Law.Record.Clean                 // nothing at all on it
Law.Record.Has(Law.Charge.DeathFled)            // ever
Law.Record.Has(Law.Charge.FledStop, 7)          // in the last 7 game days
Law.Record.CleanFor(14)          // nothing new in 14 game days (a job that wants a clean record)
Law.Record.DaysSinceLastOffence  // -1 if none
Law.Record.TimesHeld(30)         // stays in a cell in the last 30 game days
Law.Record.Count, Law.Record.Entry(i)           // day, charges (bits), hours, fines, outcome
Law.Custody.Active               // being arrested or held right now (an arrest's stay or a court's)
Law.Custody.Hold(hours, reasonKey, bail, released)   // a court's order served in the cell; false if it cannot be taken
Law.LawDirector.NearestPrecinct(position)       // its Name, Door, Building; null before the city is ready
Law.Arrests.FledStop / HitAndRun / StayedAtScene / WastedCall / Escaped / Cited   // the ways in
```

### The desk and the cell in the game's own interface

- The panel at a station (door or counter) is `GameUi.WorldPanel` through `GameUi.DoorPanel`; the desk's menu is a
  `GameUi.TravelList` made by a `GameUi.DeskList` ("RealLifeOverhaul Law"); the cell is a second `DeskList`
  ("RealLifeOverhaul Custody") that is `Locked` (no Cancel button, the cancel key left to the game).
- The game's time skip switches the HUD off, so the cell's window is moved onto the skip's own canvas
  (`TravelList.MoveTo`) once the skip has started and back to the HUD's when it closes. The clock line is written only
  when the minute changes. Number keys 1 to 3 no longer choose in the cell: the entries are clicked.
- Fallbacks: if the game's list cannot be copied, the desk's panel has the report, the fines, the car and the police
  report on its own four keys; the cell serves the sentence with the charges told once in a notification and no
  bail or lawyer.
- The licence entry shows only when `Law.Bridges.Licensing` is set; nothing in this build sets it.
- Log lines: "Law check (desk panel): ...: PASS", "Law check (desk list): the game's list is open: ...: PASS",
  "Law check (choice): ...", "Law check (cell list): ...".
- Not checked in the game: any of it. In particular whether the list is drawn on the skip's canvas at the right size
  and place (a screenshot settles it), and whether the cancel key is left alone by the locked list.

### Test buttons

"Arrest me now" (minor: fled a stop; serious: hit-and-run with an injury; fatal hit-and-run) runs everything
from where the player stands, in a car or on foot, indoors too; it charges no fines (in play those are charged
by whoever stops the player), so the charge sheet has no "paid" line. The arrest begins when the options
menu is closed (it waits up to 40 seconds for whatever has the screen). "Show the nearest precinct" marks it as the destination and says how far it is. "Open the front desk here" opens the nearest precinct's desk wherever the player stands (the one they are in, if they are in one). "Dress the nearest precinct again and log what fitted" takes the frontage in and puts it out at once, in view, and writes the frontage line (every piece: up, or left off and why) and the room's line to the log; it wants the precinct within 250 m and the player out of doors. "Clear my record"
wipes the record and resets both reputations. Each says why if it cannot run (`TestRefusal`, with
`NoPrecinct` and `ScreenBusy` added for these).

### Wish-list for the asset pipelines

Everything below drops in by id with no code change: the code already asks `PropLibrary` / `ClipLibrary` for each and uses its stand-in (or does without) until it is there. The one exception is marked.

| prop id | size (m) | pivot and layout |
|---|---|---|
| `precinct_sign` | 1.80 x 2.90 x 0.08 | ground centre, read from +Z, nothing behind z -0.04 (it stands 0.12 m off a wall). Two posts 0.07 square at x +-0.86. A police-blue board 1.65 wide from 2.05 to 2.85 m up (over everybody's head) with POLICE in white, centred 2.56 m up; a white rule at 2.27 m; **a blank blue strip 1.50 x 0.17 centred 2.15 m up** where the code letters the precinct's name (z +0.027). The right-hand post top (0.86, 2.90, 0) carries the lamp |
| `blue_lamp` | 0.20 x 0.27 x 0.20 | centre of its base (it sits on a post top, and on the wall brackets either side of the door); glow material: a blue glass cylinder 0.17 across and 0.24 high under a dark cap |

The pieces below are placed by their **bounds**, not their pivot (`StationProps.Holder`): a floor piece is stood on the
lowest point of its mesh with its footprint centred, a wall piece is hung by the middle of its back face (its lowest z).
So only the facing matters, and the size is read from the mesh (the cell is made as wide as the panel really is).

| prop id | size (m) | facing and layout |
|---|---|---|
| `precinct_desk` | 2.4 x 1.1 x 0.8 | floor. The public stand at +Z, staff at -Z. The code letters the precinct's name on the +Z face, centred 0.55 m up, in a strip 1.9 x 0.14: leave it plain |
| `precinct_bench` | 1.8 x 0.85 x 0.5 | floor. Back at -Z (against a wall), sat on from +Z; seat 0.45 m up |
| `holding_cell_bars` | 3.0 x 2.4 x 0.06 | floor. A flat panel in the XY plane with a door in it; seen from both sides. It is stretched along X by up to +-12 % to meet a wall, and the side panel of a corner cell shortened to as little as 77 % |
| `notice_board` | 1.2 x 0.9 x 0.05 | wall. Read from +Z; hung with its middle 1.55 m up, indoors and out |
| `police_shield_emblem` | 0.8 x 0.8 x 0.05 | wall. Read from +Z; hung 2.0 m up beside the door (or higher, over it) |
| `station_awning` | 2.4 x 0.5 x 1.1 | wall. Projects towards +Z from its back face; hung with its middle 2.75 m up (underside 2.5 m) over the door |
| `flag_pole` | 1.3 x 5.1 x 0.3 | placed by its own pivot: the foot of the pole at the origin, on the ground. The flag flies towards +X from the top (the code clears a box 1.4 long there) |
| `bollard` | 0.16 x 0.9 x 0.16 | floor |
| `office_desk` | 1.4 x 0.75 x 0.7 | floor. Whoever works at it stands at +Z; its back (-Z) goes against a wall. **New id**: a plain desk with a screen, built from shapes until it exists |
| `evidence_door` | 1.04 x 2.12 x 0.05 | wall, standing on the floor: a closed steel door in its frame, read from +Z. **Delivered.** The model has EVIDENCE on its plate in the mesh (English only) and the code letters only the stand-in (`reallife_law_station_evidence`): **for localisation** the model's plate would have to be left plain and lettered by the code at (0, 1.62) as the stand-in is. Likewise `precinct_desk` carries POLICE on its fascia in the mesh: the code's precinct name now goes in the plain strip under that word (0.34 m up, on the panel), where it used to be lettered across it |

| clip id | length, loop | what |
|---|---|---|
| `arrest_cuffed` | 2.4 s, once | **delivered and played** on whoever is arrested (`Crime/ArrestSequence`, so every crime scene, the roadside stop and the sting; `Law/Custody` for the player): straight out of `hands_up_hold` and on into `cuffed_stand` (`ActorRole.LeadInto`); mark `click` at 1.5 s |
| `officer_cuff` | 2.4 s, once | **delivered and played**, whole body (it steps in), its last pose held (`ActorRole.Act(clip, ClipEnd.Hold)`) until the suspect is walked off. The officer walks to the place it is measured from, 0.9 m STRAIGHT BEHIND the suspect and facing the same way (`ActorRole.GoExactly`; the suspect has turned their back first), and both clips start in the same tick; the last centimetres (0.25 m at most) are closed under the cross-fade, and from further off it is the old reach and the cut. The right hand holds `handcuffs` until the `click`, when they are put away and `CuffPose` shows its pair on the wrists: one pair is ever seen |
| `officer_guard_idle` | 8 s, loops | standing easy by a door: thumbs on the belt, weight from one foot to the other, a slow look left and right. For the officers at a precinct door, who only stand today |
| `cuffed_car_enter`, `cuffed_car_enter_l` | 2.0 s, once | **delivered and played** by `Crime/ArrestSequence` at the patrol car: the suspect walks to the place `job_car_enter` is made for (`StreetSpots.AtRearDoor`: square to the car through the seat, feet 0.75 m out from its middle; the `_l` clip on the car's left), turns square, and gets in in the clip, taken off the street at its `seated` mark. Only where their own agent has a whole route to that place and stops within 0.2 m of it; otherwise gone with the sound of the door as before. The door does not open (the game's traffic cars have none that does), as for the taxi's passenger. The player's own ride to the precinct is still a fade |
## Transport

What the game already has, and is left exactly as it is: **the subway** (twenty `SubwayStation` entrances in the
street; click one, pay 3, the map opens in subway mode, pick a station, a camera flies there in seconds with no
time passing), **taxis** (0.15 a metre, 0.04 game minutes a metre, a fade and the time machine), a private driver,
**fuel** (a tank per vehicle type, 1.30 a litre at a filling station, a 10-litre can for 30, a to-do at 10 %, the
engine stops when dry), **bodywork damage** (repaired in a station's repair bay for damage x half the car's price;
at 100 % the car will not drive), **dirt** (washed in the same bay for 50), **parking tickets and towing** (to
`ba:towdestination_autorepairshop`). The game has no bus, no bus stop and no vehicle interiors.

### Buses

**Why the bus of 0.43.0 never came, read from the traffic system's code.** It was a van handed to the traffic AI
with a scripted route (`VehicleComponent.presetPath`), "the way the game drives its private-driver car". Three
things in that:

- *A scripted route does not obey lights or give-way points, and is crippled by the first one it meets.* A vehicle
  with a scripted route is given its next point as soon as it is within 5 m of the last (`TrafficManager.FixedUpdate`
  calls `DrivingAI.WaypointRequested`, which returns right after `SetNextWaypoint(presetPath[0])`), before the code
  that holds a vehicle at a red light (`activeActions` has `StopInPoint`: return) or asks a junction whether it may
  enter (`GiveWay`) is ever reached. The stop order a red light puts on a vehicle is lifted only for vehicles whose
  target is still the light's own point (`WaypointManager.TrafficLightChanged`); the scripted vehicle's target has
  moved on, so its order is never lifted, and a give-way order is lifted only in the code it never reaches. From its
  first red light or unsignalled junction on it drives in `DriveJob.StopInPoint`: braking for every lane point in
  turn, and the first time it is slower than one brake step within 10 m of its next point (behind a queue, say) it
  is stopped for good. After 100 s the mod let it go as traffic. (The game's own private driver has the same flaw;
  its routes are at most fifty points long.)
- *It needed an idle van by luck.* Only a van of the pool that was switched off would do, and the place it started
  from had to be free of traffic two car lengths each way, at one fixed lane point; every refusal was answered by
  trying exactly the same again ten seconds later.
- *The figure jumped because it was not a measurement.* It was a countdown to "due", put back up to 34 s at every
  refusal (and the refusals were logged only when a counter happened to line up: one chance in eight), counted down
  to nothing while the bus crept, and turned into game minutes with a clock rate measured over two seconds.

What cannot be told from the one log there is: its last transit line is the only bus that was ever put on the road
that session, and the log ends before either "stands at its stop" or the 100 s "did not reach its stop" line, so
that one bus was either still crawling when the game was closed or its vehicle was taken (which was not logged).

- **The network** (`TransitMap`, `RouteSearch`) is laid out by rule in the first seconds out of doors after a city
  load and kept in memory only. The ends of the routes are the game's subway stations in the order of
  `SubwayStationName`: up to six times over, the first station not yet used, and the unused station furthest from it
  (500 m to 2.8 km) that can be driven to **and back from**. A line's end is any plain lane point within 150 m of
  its station (0.43.0 took the one nearest point, which is often on a lane that only leads away: that, and never
  changing lane, is why six directions had "no way along the road graph"); the way is the shortest along the lanes
  (A* from all of one end's points to any of the other's), with a change of lane allowed where the road graph offers
  one between two plain lanes running the same way, at the price of 30 m. A failed search says how far it got and
  whether it ran out of road or hit its limit, and every partner it never got near is dropped without a search of
  its own. Only a station with no partner both ways gets a line one way. `Transit check (network)` is the summary.
  A stop is looked for every 260 m in the 140 m after that, at the first lane point that is on a road proper, has no
  junction, give-way or lane beside it on the kerb side within 26 m of line either way, is straight (under 9 degrees
  over six points), at street level, and has pavement 1.3 to 5.6 m to its right that is 1.5 m deep with nothing
  static and no door within 2.4 m where the pole and the waiting people stand. Where the pavement is also bare and
  deep 3.2 m further on there is room for a shelter. A stop is named after the nearest building's address.
  Of the places that will do, one with a **bay** (below) is taken before one without: first in the 140 m, then up
  to 60 m further on, then up to 50 m before the place wanted (so two stops are never nearer than 210 m of line),
  and only then the first place without one, which is the place that was always taken. `Transit check (network)`
  counts the stops with a bay and gives every stop's kerb distance, with a word for why a stop has none.
- **Bays** (`BusBay`, `TransitBays`; the owner's report: "Busses are blocking traffic"). The road and the pavement
  are the game's meshes and cannot be cut, so a bay is made of room that is there: the strip between a driving lane
  and the kerb where the game parks cars.
  - *Where there is one.* A stop has a bay where the kerb is at least 4.10 m from the lane's line (the lane's half
    of 1.65, 0.15 m clear of its edge, the 2.10 m body, 0.20 m to the kerb: `BusBay.KerbNeeded`), measured at the
    kerb that is there (the first step up of the road surface, where that comes before the navigation mesh's edge);
    the lane runs straight (its points within 0.35 m of its line) and plain, with no other lane between it and the
    kerb, for the whole run; and the strip is road at the lane's level, with the kerb keeping its line and nothing
    fixed standing in it, from about 17 m behind the pole to 16 m ahead of it (more where the strip is wider: the
    bus crosses further). A stop records where the bus stands (`Bay`, `BayOffset`), how much strip it takes
    (`BayBehind`, `BayAhead`) and how far back the strip will do for a vehicle with a longer nose (`BayRoom`).
  - *Why a bay is that long.* The driver turns a vehicle about its own origin (under the middle of a van), so the
    3.3 m of bus ahead of that point swings towards the kerb as it comes in at an angle and the 3.3 m behind
    swings towards it as it turns out. A body that stands 0.2 m from the kerb has room for neither unless the angle
    falls away as fast as the gap closes: the last metres in and the first metres out are an exponential whose
    length constant is about that lever. The rest is a straight run across the strip at 16 degrees (up to 22
    where the strip is wide) and three metres of easing off the lane's line. A vehicle whose curve will not fit
    the stop's bay (a long nose ahead of a rear axle on a wide strip) is given the lane's line instead, and the
    log says so.
  - *In.* The end of the bus's route is the curve in (a point of the mod's own every metre), the standing place
    and the point beyond it that the driver stops 1.2 m short of. From 6 m before the curve the driver is told to
    take a point as reached at 2 m, not 4.5 (`EmergencyDriver.Reach`: on the open lane it cuts every corner by
    4.5 m, which here would be the kerb), and where the kerb is (`EmergencyDriver.Kerb`): its look ahead runs
    along its nose, which points at the pavement while it turns in, and without that the people waiting at the
    stop, or the player standing at the kerb, would stop it half way across. With the kerb known, what the sweep
    finds beyond it does not count and nobody of the mod's people beyond it counts (`PeopleAhead`); everything on
    the carriageway counts as ever. It comes down to 4.5 m/s by where the curve begins (2.5 m/s2). Held up on
    the last 9 m by something that is in the bay after all, it stops there and that is its stand.
  - *Standing, and why traffic passes.* The game's drivers do not overtake a standing vehicle (nothing in the
    traffic system's code does). Each sees what is ahead with one trigger box on its
    nose, as wide as the vehicle itself (`VehicleComponent.OnTriggerEnter`, `colliderWidth` from that box; the
    box is turned with the steering, `UpdateTriggerJob`), and stops for any solid collider of a vehicle that the
    box touches. A bus in its bay has its nearest side 1.80 m or more from the lane's line; the widest thing the
    game drives on that line is taken as 2.60 m (`BusBay.WidestHalf`), which reaches 1.30 m. So the box of whatever passes never touches the bus, and
    nothing is switched off or excluded to make that so: the bus keeps its colliders, and a car that leaves its
    lane still stops for it.
  - *Out.* Doors closed, it stays where it is, frozen, and gives way: it goes when the lane is clear from 30 m
    behind its tail to 5 m beyond its nose (one box, `SceneParking.InStrip`, four times a second while it waits
    and never otherwise); after 15 s, when it is clear from 12 m behind (it edges out, and what comes up behind
    it sees it in its box and slows, as for any vehicle ahead); never while something is beside it. No gap in
    45 s and it is let go where it stands, out of the lane. Nothing is ever taken off the road to make a gap.
    The curve out is laid from where it really stands, so a bus that stopped short and askew gets out too.
  - *Kept free.* The game fills the strip by chance (`ParkingLaneGenerator`: a spot in four or so, again each day
    and when a business opens or shuts, never within 80 m of the player). When a stop is dressed and when a bus
    is put on the road for it, the game's parked cars for show that stand in the bay **out of view** are handed
    back to the game's own store of them, the way the game takes them in (the lane's `onReleaseVehicle`, then
    `ParkingSimulator.ReleaseParkedVehicle`). Nothing in view is touched, and nothing else at all: a vehicle of
    the player's, a delivery van, a wreck or a cart leaves the bay taken, the bus stops on the lane's line as it
    used to, and the bay is looked at again for the next bus. It is looked at once more as the bus comes up, 16 m
    before its curve; taken by then, the end of its route is put back on the lane's line.
  - *Marked.* A dressed stop with a bay has a yellow box on the road round where the bus stands and the words
    BUS STOP (`reallife_transit_bay_marking`), read the way the traffic runs: four painted strips, as the
    driving test's bay, laid to the road's own fall, made once per dressing and gone with it. Nothing solid.
  - *In the lane.* Where there is no bay, or it is taken, the bus stands on the lane's line as before, and for
    less time: 4 s where nobody is waiting or walking up, nobody gets out and the player is not within 35 m
    (`DwellEmpty`), 9 s otherwise, and up to 34 s only while the player is on foot within 35 m.
  - *Checked outside the game* (`NavCheck`, `BayCheck.cs`): the arithmetic, the two curves, and a bus driven in,
    stood and driven out by a model of the driver's steering and pacing, for the body on its van, on a van whose
    origin is 0.4 m behind its middle and on a vehicle turned about its rear axle, at four kerb distances, with
    the physics step halved and doubled, coming up off the line and askew, slower and faster, and standing askew
    part of the way across. `NavCheck.dll --bays` prints what each run measured. In the model the body on its
    van stands within 3 cm of the bay's line and 1.3 degrees of it, its nearest corner 0.06 m clear of the lane's
    edge at the narrowest bay, never nearer the kerb than 0.13 m coming in and 0.06 m pulling out. The model is
    not the game's physics: `Transit check (arrival)` says what the real vehicle did.
- **A stop is solid** (`BusStopDressing`). From 0.2 every stop of the city is put up once, nearest first, two a second, when the network is ready, and stays up while the city is loaded (no waiting for the player to look away, nothing taken in behind them; the navigation mesh is still cut only near a player on foot). Before that: The three nearest stops within 190 m are dressed, out of view if a wait of three seconds allows it and at once after a load or an arrival (230 m to
  be taken in), from the modelled `bus_stop_sign` and `bus_shelter` (plain shapes of the same size without the
  bundle). Everyone on foot is moved on the navigation mesh, so the pole, the shelter's back, its end panel, the
  screen at its other end and the bench each have a carving `NavMeshObstacle` (the cordon's mechanism): children of
  the dressing, laid out while it is switched off, cut once when it is switched on, gone with it. A cut is wider
  than its box by the walker's radius (asked of the player's navigation agent type), so the boxes are the solid
  things themselves and the bench's is that much smaller than the bench: the open side stays open, the place in
  front of each seat stays on the mesh, and the 1.69 m of pavement between the shelter's posts and the kerb stays a
  way past. That way past is tested on the mesh before the shelter is shown; where it is not one strip, the stop is
  a pole only. `Transit check (stop dressed)` says, 1.6 s later, what the mesh says was really cut, and what the
  stop's bay is: its length, where the bus stands in it, free or taken by what, how many parked cars were taken
  in out of view, and that the marking was made.
  (This is the one place the mod cuts the mesh on a pavement: the rule against it below is about closing a
  pavement, and this leaves it open.)
- **The bench** has two seats (never one for two), the sitter's feet 0.30 m in front of the seat's middle line,
  facing the road: what `sit_bench`, `sit_wait` and `sit_bench_up` are made for (seat 0.46 m up, as the model's).
  The player is offered "Sit down" within 1.5 m of a free seat, as a button of the stop's panel, and "Click to sit
  down" on the shelter from anywhere in view (the game walks them to the seat first): `TransitSeat`, held by the game's own navigation
  blocker, moved the last step and turned over a third of a second, then the three clips on the animator the way
  leisure plays its clips; any key or click gets them up, and so does the bus arriving. The game's own seating is
  not used: it exists only inside its rest and entertainment activities, which run the clock. Somebody waiting may
  take the other seat (walks to it, sits, gets up when the bus stands); those standing wait by the pole, outside
  the shelter.
- **Service**: the nearest dressed stop within 80 m is served (a line's last stop too: the bus that ends there
  takes riders on, see Riding), and a dressed stop the player walks up to on foot takes the service over from
  another at once: up to two people walk up from
  out of view, and a bus comes every 150 s by day (from 0.1.4 never later than 20 minutes of the game clock, the drive in counted: `Wait`; and it stands up to 34 s for a player within 35 m), 100 s in the weekday rush (7 to 9, 16 to 19), 210 s in the
  evening, 220 s early, 360 s at night, a third longer in the rain. In the rush one bus in four is full.
- **The bus** (`TransitBus`) is a vehicle of the traffic pool wearing the modelled `minibus` (hidden under it
  wholly; without the bundle, a van with a lit route board). The body is made to the Mersaidi Dash (6.66 m, axles
  4.02 m apart) and is put with its axles on the vehicle's own, read from its wheel meshes (`TrafficTools.Axles`),
  so on a Dash its wheels are exactly where the van's are; a Dash is taken before a van with another wheelbase, and
  that before any other vehicle (the log's `chassis` says which, and how far the axles are off). It is held the way a response unit is (ignored by the
  traffic AI, an empty scripted route as the mark that keeps the game from taking it off the road) and **driven by
  the mod** with `EmergencyDriver` along the line's own lane points: 40 km/h, 25 through junctions, stopping behind
  whatever is ahead, never out of its lane (`PullOver` with next to nothing: a bus passes nobody), to within a
  metre of the end of its route. What that driver does not know of this class adds: it pulls up at a red light
  (the light's own lane point, `Waypoint.stop`, within 170 m of the player; the game only runs the lights near the
  player, so a light further off, or one out of view that has not changed in 45 s, is not waited at) and two points
  before any lane point a scene has closed (`temporaryDisabled`: the cordon, a lane closed for roadworks), frozen
  while it stands, and goes on when they open. Which vehicle is not luck: an idle van; else a van driving out of
  sight (taken off the road there); else, under the modelled body only, any ordinary car or truck of 4.2 to 7.5 m
  in the same two ways; never an ambulance, a taxi, a limousine, a private driver or a vehicle with a scripted
  route or held by a scene. Where it starts: a plain lane point of its line about 170 m before the stop and out of
  view, then others further back (to 330 m) and nearer (to 60 m), 14 m apart, up to eight tried for one free of
  traffic, none before a closed road.
- **It comes** (`TransitDirector`). When the timetable's wait is down to 30 s a bus is put on the road; a refusal is
  tried again every 2 s (from the third on it may start in view, more than 60 m from the player), logged at the
  first and every fifteenth, and the panel says DELAYED after six seconds of it. On the road it is watched: with no
  metre of progress for 40 s while out of view and more than 110 m off it is moved up its line, past what holds it
  (`Transit check (re-plan)`); with none for 150 s, or after 300 s out, it is given up with a notice and the next
  is sent; if the game takes its vehicle another is sent at once.
- **The figure** (`TransitEta`, run in a console test) only goes down. Before a bus is on the road it is the
  timetable's wait ("next bus in about N min"); with one on the road it is that bus's metres of route still to
  drive over the speed it has made while moving ("the bus is 120 m away, about 1 min"), never more than was shown
  before. When that cannot be kept (it stands more than 12 s, is 12 s behind for 6 s, was lost, none could set out)
  the panel says DELAYED and why, with no figure; when the bus has been moving again for two seconds one new
  figure is given from where it is. The game minutes shown are that figure at the measured rate of the game's
  clock, and are not allowed to rise for the rate's wobble either.
- **At the stop** it stands with the middle of its body (its door) beside the pole, door side to the kerb, frozen:
  in the stop's bay where there is one and it is free (0.2 m from the kerb, out of the lane), else on the lane's
  line; for 9 to 34 s (4 s in the lane where nobody has business with it). `Transit check (arrival)` gives metres
  along and aside and degrees off and, in a bay, the metres between the body's nearest corner and the kerb and
  between its nearest corner and the lane's edge (PASS: within 2.5 m of the pole, 3 degrees, nothing of it in the
  lane or over the kerb; on the lane's line, as before: 2.5 m, 1.2 m aside, 12 degrees); `(doors)`
  the place somebody steps in from (0.30 m outside the body at the door 0.76 m ahead of its middle, as the model
  documents and `board_step` wants: the step's edge 0.30 to 0.35 m ahead of the feet; up to 0.9 m further out if the mesh does not come that close) and whether anything stands between it
  and the kerb. Somebody may get out and walk off; those waiting get up, walk to the door one at a time and step
  in (`board_step`; the van: `job_car_enter`); `(departure)` counts them and, for a bus in its bay, says how many
  seconds it waited for the lane to be clear (the line is written when it goes). It follows the next 240 m of its
  line and is then taken off the road if nobody is looking, or handed back to the traffic AI as it is. The bus a
  rider sees pull away after a ride is put on the lane just past the bay's end, where one that has pulled out is.
- **Riding** (`TransitDirector.Player.cs`): everything the player is shown is the game's own interface (see
  `docs/game-ui.md`, sections 9 to 11), and nothing of it runs per frame.
  - *Pointing.* The pole, the shelter and the standing bus each have a `TransitPointer`: an `EntityController` of
    the mod's on a trigger box on the game's layer for interactive items, with one entry at the front of the game's
    list of calls to action (`TransitCta`, as `HomeOffers`). The game outlines the stop, names it and offers
    "Click to choose a stop", "Click to sit down" or "Click to board" from anywhere in view, as it does for its own
    objects; a click from further off than reach walks the player over with the game's `PlayerController.SetGoal`
    and acts on arrival. The bus's box is the director's own, put where the bus stands (`TrafficTools.Footprint`)
    and switched off when it goes: nothing is added to the pooled vehicle.
  - *At the stop.* Within 3 m (`Reach`: the game's `EntityController.MaxReachDistance`) of the pole, the waiting
    place, the shelter's middle or the standing bus's body, the game's panel over a place (`GameUi.WorldPanel`:
    the box of a petrol pump) shows "Bus stop: name", the line (or "Your stop: name" once one is picked, or that the
    line ends here) and, right, the minutes, "At the stop", "Full" or "Delayed". Its buttons carry the game's own
    keys: "Choose a stop" on Interact ("Board: choose a stop" while a bus with room stands, "Change your stop" once
    one is picked), "Sit down" on SecondaryInteract while a free seat is within 1.5 m, "Get up" while seated. The
    earlier figures (panel at 13 m, boarding at 16 m, then 6 m from the bus) are gone.
  - *Where to.* On the city map, as the game offers its subway stations from an entrance (`GameUi.MapPicker`): the
    map opens over the stop, its filters are put away as the game puts them away for its stations, and so are the
    mod's own places (`MapPlaces.Quiet`). A copy of the game's window for its stations stands where the game's does,
    on the map's canvas, with the game's search field (it narrows the entries by name). Every stop offered is also
    one of the game's markers on the map, in the bus stops' colour, named while pointed at and picked by a click; the
    stop the player is at is marked in the game's red. Pointing at an entry glides the map to its stop after the
    game's own delay (0.2 s), as the game's entries do. The map holds the game's clock still, so a pick closes the map
    and is taken up on the director's next tick, once the game runs again. The window offers
    every stop of the network (`TransitTrips`), as the subway offers every station: first the stops further along
    this line in the order the bus calls at them (the same bus: 0.06 game minutes a metre of line plus a minute a
    stop, times the game-speed setting), then every other stop, nearest first, reached with a change of bus that
    is not played out (the straight line times 1.35 at the same rate, a minute a stop at one per 330 m, and 6
    minutes for the change). Not offered: the stop itself and a stop of another line within 150 m, which is
    walked to. Each entry has the stop's name, its line and where that goes, the minutes and the fare; the first
    entry sells the day pass (7.50, buses and subway until midnight). One fare (2.75) covers a ride, the change
    included, as the subway's 3 covers any station.
  - *Boarding.* Picked while a bus with room stands, the fare is paid (`pay_tap`) and the ride begins. Picked while
    waiting, the stop is kept (`_booked`, a notice says so) and the player boards by themselves when a bus with
    room has stood for a second and they are at the stop or the bus; it is forgotten when the service at the stop
    ends (105 m away, or another stop walked up to). The bus waits up to 30 s under an open list, and for as long
    as the map is open (the map stops the game's clock).
  - *The ride* is unchanged: a fade with the game's time machine exactly as the game's taxi does it
    (`TaxiSystem.TravelCoroutine`), the put-down at the far stop's waiting place, a bus pulling away there.
    `Transit check (ride)` names the stops called at, or says "with a change of bus" with the metres of road it
    counted; `(put-down)` how far from the waiting place the player stands and whether that is pavement.
  - *If the game's parts are not there* (one `Game UI:` or `Transit:` warning each): without the list of calls to
    action the stops cannot be clicked and the panel still works; without the subway's window on the map (or when
    the game will not open its map now: placement mode, say) the same list stands in the middle of the HUD
    (`GameUi.TravelList`), and without that the game's own yes/no question offers the one ride to the end of the
    line; if the game's panel is showing another place the
    stop's is tried again every quarter second and `(stop panel)` says FAIL meanwhile.
  `strap_hang` is not used: there is no interior to stand in.
- **Test buttons**: "dress the nearest bus stop" (in view; its check line follows), "send a bus to the nearest stop
  now" (a stop within 100 m; out of view if it can, else in view far off; says exactly what stopped it otherwise),
  the ride, the subway journey.

**Lines to judge a run by** (`Player.log`, one each): `Transit check (network)`, `(stop dressed)`, `(seat)`,
`(bus assigned)`, `(en route)` each minute, `(re-plan)`, `(arrival)`, `(doors)`, `(departure)`, `(ride)`,
`(put-down)`; for the player's side `(pointer)` (once when the click actions are added, then at every click on a
stop, a bench or the bus), `(stop panel)` each time the panel comes up, `(stop map)` (or `(stop list)`) at every
opening and closing, `(choice)` at every pick and for the day pass, `(subway journey)` when a ride of the game's
comes up; and
`Transit: no bus could be put on the road ... yet (try N): <why>`.

**Not verified in the game** (it could not be run): everything about how the game's interface looks with the
stop's content in it (where the panel stands over the pole, how wide its second line makes the box, the list in the
middle of the HUD, two lines in an entry made for one; the copy of the map's window beside the game's own, the
markers' name plates while pointed at, the glide); that the game's pointer reaches a trigger box under the
mod's camera (the game's physics has queries hit triggers, and the cursor is locked to the middle of the screen, not
hidden, so the game's own ray is cast from the crosshair); that the outline pass draws the mod's props when they
are put on the game's outlined layer; that the cancel key taken by the list does not also open the game's menu
(the list's script runs first and resets the action, as the game does); that a cut in the mesh is wider than its box by the walker's
radius (if it is not, the bench's box is smaller than the bench by that much: `(stop dressed)` says "bench no");
that the navigation blocker also holds the player against the mod's own direct controls (any movement key gets
them up anyway); where on the vehicle its origin is (the body is stood on the lowest point of what the vehicle
shows; `(bus assigned)` gives the height and how far the body reaches beyond the vehicle's colliders); the van's
own size (the model's notes assume 6.8 x 2.26 m with a 4.02 m wheelbase; the log line gives the real figures);
whether the lanes the road graph offers as "other lanes" are always same-way lanes (they are only used where
their directions agree within 45 degrees); how long the game's lights stay red; whether Industry City can be
reached by lanes at all (if not, its stations simply get no line, and the pairing lines say so).

### A taxi by phone (`Scripts/Transport/Taxi`)

The contact **City Cabs** sends one of the game's own taxis to the street the caller is in. The game's click, map,
fare and ride are untouched: the mod brings the taxi and charges a call-out, and that is all.

```
TaxiRules      every number and rule, game-free                     NavCheck/TaxiCheck.cs runs them
TaxiRoads      the caller's lane, the places a taxi may start, the way between
TaxiCab        the vehicle: taken, driven, stood, let go
TaxiDirector   one call: quote, send, watch, arrival, wait, ending   0.25 s timer while a taxi is out; switched off otherwise
TaxiPhone      the contact and the game's dialogue panel
Wiring/TaxiLinks   the holds, the weather, the GPS's road graph, the minimap
Traffic/RouteGates red lights and closed roads for a vehicle the mod drives (the bus's rules, written out for anybody)
```

**What the game has, and what was taken from it.** A taxi is a pooled traffic vehicle whose prefab carries
`TaxiController` (an `EntityController` and `ITaxi`): its click stops the vehicle (`StopNow`, 60), walks the player to
it and opens the map in taxi mode (`CityMap.SetTaxiMode`); `TaxiSystem.TravelTo` charges 0.15 a metre, hides the
player, runs the time machine and puts the taxi back with `TaxiController.InstantiateVehicle`
(`Manager.RemoveVehicle`, then `TrafficManager.LoadVehicle(prefab, waypoint)`). The game also has a car that comes
when called, the private driver (`PrivateDriverHelpers.SummonPrivateDriverVehicle`, `PrivateDriverVehicle`). From it:

- *taken:* how the vehicle is put on the road (`LoadVehicle` on a lane point that is no junction's and that the
  camera does not see); that it is the game's own object the player then clicks; that one may be out at a time;
  that the game's `AiCarRescueCheck` (which removes a car standing 20 s unseen, 40 s seen, and leaves only the
  private driver alone) must be kept off it;
- *not taken:* the driving. The private driver is given a scripted route (`VehicleComponent.presetPath`) and stopped
  by `PrivateDriverVehicle.Update` every frame. A scripted route drives through its first red light and stalls
  behind the first queue (see "Buses" above), and a per-frame `Update` on the vehicle is not this mod's way. Nor its
  route finder (a breadth-first walk of at most fifty lane points that allocates a list per branch), its guider
  (`DirectionGuiderType.PrivateDriver`, which the game's own private driver resets), or its component added to the
  pooled vehicle.

**Which vehicle, and why it is a real taxi when it arrives** (`TaxiCab.Pick`, `Send`). One pass over the traffic pool
for a vehicle with `TaxiController` that nobody holds (`presetPath == null`): an idle one, else one that is driving
out of sight (not ignored by the traffic AI, the game's own visibility script says unseen, neither of the mod's two
view tests sees it, and it is 70 m or more from the player), which is taken off the road there. It is put on the
plan's start with `TrafficManager.LoadVehicle`, only ever while an idle vehicle of that prefab exists (with none,
that call would take any vehicle of the prefab's name, seen or not). Nothing is added to the vehicle and nothing on
it is changed, so the object that stands for the caller is the same `TaxiController` the game's click, map and ride
expect.

**The way** (`TaxiRoads.Find`). One pass over the lane points gives the caller's lane (the plain lane points on
their level, within 60 m, that are no further off than the nearest plus 5 m) and the places a taxi may start (one
plain lane point in four between 250 and 450 m as the crow flies, 400 to 650 m in the wet; the camera's 110 m is
well inside that, and both view tests are still asked for any nearer the camera than 115 m). One `RouteSearch.Find`
from all of the starts to the lane: the answer is the start nearest by road and the way from it. The graph is the
GPS's when it has one (`NavDirector.Roads`: places and edges shared, four working arrays of the taxi's own), else
`RoadGraph.Build` once per city on the first call (named for the session record). A route more than 2.2 times the
band's far end is refused. A start traffic stands on (`LoadVehicle` answers null) is left out and the search run
again, four times at most.

**The drive** (`TaxiCab`). As a bus: ignored by the traffic AI, an empty `presetPath` as the mark that keeps the game
from taking it off the road, the rescue check switched off, `EmergencyDriver.BeginRoute` along the route with
`MayPass` off (it keeps its lane), 11 m/s and 7 through a junction. `RouteGates` (the bus's rules for red lights and
closed lane points, written out as two static functions; `TransitBus` keeps its own copy, which was proven in the
game and is not touched) says where it pulls up and when it may go on; standing at a gate it is kinematic. Held up
with 14 m or less of its route left (the caller standing in the lane for it), it stops there and has arrived.

**Where it stops.** The last place of the route is the caller's lane point. With 17 m left, once, and only if it is
within 12 degrees of the lane's line, `SceneParking.KerbRoom` is asked (the test a car stopped by the police gets:
the strip beside it is carriageway, no other lane, nothing standing in it): if there is room it pulls in by that
much (`EmergencyDriver.PullOver`) and is braked with 4.5 m left; else it stops on the lane's line, 1.2 m short of
the route's end as every route of the driver's does.

**Standing, and the hand-over.** The taxi stays held and kinematic: nothing drives it away, the game does not
remove it, traffic queues behind it or the kerb keeps it out of the way. It is not handed back to the
traffic AI with a `StopNow` issued again and again (what the game's click does, once, for a hailed taxi): a vehicle in
the AI's hands is removed by `AiCarRescueCheck` after 20 s unseen and by the pool once it is out of range, and a caller
80 m away causes both. On a held vehicle the game's click still works as on any taxi: its own `StopNow` and `DriveAway` only
write the traffic manager's two state arrays (`TrafficManager.UpdateDrivingState`), which nothing acts on for an
ignored vehicle.

**What tells the ride's start.** `TaxiSystem.IsTraveling` turning true while the last taxi the game's map had was
this one. `CityMap.Taxi` cannot be read at that moment: `TravelCoroutine` closes the map in its first synchronous
stretch and the map forgets its taxi as it closes, before `_travelCoroutine` is even assigned. So the map's taxi is
noted from `GlobalEvents.onCityMapToggle` (kept with `EventHold.Keep` while a taxi is out, taken off when the call
ends): the event is raised on opening with `Taxi` already set (`SetTaxiMode` sets it, then toggles), and on closing
before it is cleared. A map opened plainly has no taxi and changes nothing. The ride check is the first thing in a
tick, before the vehicle is looked at, because the time machine that follows shuts traffic down.

**How a call ends** (`TaxiDirector`), each with one `Taxi check` line and everything released by `Stand`:

| ending | what tells it | money | the taxi |
|---|---|---|---|
| the ride | `IsTraveling`, the map's taxi was this one | the call-out | handed back to the traffic AI at once (`ReturnToTraffic`): the game then removes and re-instantiates it after the ride, as it does any taxi |
| another taxi | `IsTraveling`, the map's taxi was another (hailed, the private driver) | a cancel's | traffic if seen, else off the road |
| nobody came | 60 real seconds standing (20 more, once, for a caller within 15 m) | half the call-out | the same |
| cancelled | "Cancel it" on a second call, or the test | free above 100 m of road to drive, else half | the same |
| went another way | in a vehicle, indoors or in a car park, on the subway, the boat or a bus | a cancel's | the same |
| held | `TaxiBridges.Held` | none | the same |
| cannot come | got nowhere for 40 s out of view with over 110 m to go: one new plan, nearer; 150 s without a metre or 300 s out: given up. Or the game took the vehicle (a building, fast-forward, the pool) | none | off the road |
| option off, clean-up, a fault | the tick; `Features.Cleanup`; `Fail` | none | traffic if seen, else off the road |
| the city closes, a save is loaded | `OnDestroy` | none | off the road |

No charge is forced: `TaxiRules.Payable` takes whole money and never more than the save holds, through
`GameManager.ChangeMoneySafe` with `force: false`, under `reallife_transaction_taxi_callout`, `_cancel` and
`_noshow`. Nothing is in the save but the contact.

**Cost.** Idle: nothing (the component is disabled; it runs for the first moments of a city to add its contact). A
call: one pass over the lane points, one over the pool, one search (figures in the `(route)` line). While a taxi is
out: the 0.25 s tick (a few flag reads, the gate walk over at most 45 m of route) and the driver's own physics step
for its one vehicle. The minimap asks for the taxi's place when it gathers its marks, every two seconds.

**Log lines** (with "Developer diagnostics" on): `Taxi check (call)`, `(route)`, `(taxi found)`, `(en route)` once a
minute, `(re-plan)`, `(arrival)`, `(wait)`, `(ride)`, `(no show)`, `(cancel)`, `(released)`.

**Never run in the game.** Read from the decompiled code and checked by the compiler and `NavCheck` only: that the
city's traffic pool has a taxi idle or out of sight often enough (if not, the call is refused with "No taxi is free
right now"); that the taxi's `VehicleTypes` is allowed on the lanes it is routed over (only lanes that allow it are
taken as starts and ends; the graph's edges are the car graph's); how the game's hover outline and click behave on a
kinematic vehicle; that the player's walk to a taxi pulled in to the kerb ends at its door as it does in the lane;
how the taken taxi looks driving off in the second the screen fades; whether `KerbRoom` finds room often on this
city's streets, most of which have parked cars at the kerb (then it stops in the lane, as a hailed taxi does).

### Subway

The game's own subway is left as it is: click an entrance, its map opens in subway mode, pick a station, pay its 3
(`ba:transaction_subwayride`), and a camera flies there. Standing within 7.5 m of an entrance on foot, the game's
panel over a place (`GameUi.WorldPanel`) shows the station, how often trains run and the state of the service. It has
no button: an earlier version put a list of stations of its own on the interact key there, and players met that
instead of the game's map. Service follows the game's clock, the same on every load of the same day: a train every 3
minutes in the weekday rush, 5 by day, 7 from 20:00, 8 from 05:00, 12 at night; in one three-hour window in eight
there are delays of 6 to 14 minutes, which arrive as texts from the City Transit contact (it can also be called for
the state of the service and a day pass). The journey is the wait (a part of the interval, plus delays) and 2 minutes
a stop (one per 650 m, at least one). When the game's ride is over (`SubwaySystem.IsRiding` falls; its two stations
are read as it rises), what the journey takes beyond the minutes the game's ride took is run under the bus ride's
fade with the time machine (`TransitRide`, without the card's tap), and a note says where the player came up and how
long the journey took; the game's own ride gives its subway mood. A day pass gives the game's fare back as the ride
begins (`reallife_transaction_subway_pass`); civic life counts such a ride as not paid for, and counts no ride while a
train is under way. The night closure of a station for track work went with the mod's own list: the game's map offers
every station and cannot be told that one is shut. With "Subway journeys take time" off the game's subway is as it is,
and a day pass still gives its fare back. No late-night crime is tied in: the crime system has no call for staging
something on the player other than its test button.

### Car ownership

Per car in the save's own list of the player's vehicles, a record in modData (`reallife:garage`, by vehicle
instance id; dropped at load, at save and daily for cars the save no longer has). All figures are in `CarRules`.
Prices are shares of the car's list price (`VehicleType.price`) between a floor and a ceiling. The game's own
figures, read from its data files: cars cost 2,500 (Honza Mimic), 8,000 (UMC Desert), 11,000 (Vord Tiara Vic),
44,000 to 65,000 (V150, BIMA 320, Mersaidi Dash, Nunavut), 72,500 to 98,000 (the trucks, S500), 130,000 to 250,000
(Ferdinand, Lytte, Pony, MGA GT, limousine), 520,000 (Anselmo) and 720,000 (Petrolls Fanton); tanks hold 55 to 80
litres (cars), 110 to 185 (SUVs, vans) and 250 to 378 (trucks) at 1.30 a litre; repairing a wreck is half the
list price. So a Honza is 120 to service, 180 for tyres, 90 for brakes, 40 a period to insure and 65 to register;
a BIMA 320 is 575, 770, 385, 290 and 155; anything from 120,000 up is 900, 1,400, 700 and 300, with insurance
from 780 (Ferdinand) to the ceiling of 2,500 (above 417,000).

| what | rule | cost |
|---|---|---|
| service | every 40 km (sports), 50 (luxury, trucks), 60 (others), 70 (economy) | 1.2 % of list price, 120 to 900 |
| tyres | last 120 km (sports), 200 (vans, trucks), 180 (others); below 30 % grip falls, to 88 % when bald | 1.6 %, 180 to 1,400 |
| brakes | last 110 km (sports), 120 (trucks), 150 (others); below 30 % brake torque falls, to 80 % | 0.8 %, 90 to 700 |
| breakdown | none until the service is overdue; then 0.4 % a km rising to 3 % a km a whole interval late; +1 % a km with 70 % damage | mechanic 95 (half if insured), or a tow 180 |
| fuel | the game's, used at 0.8 (economy) to 1.6 (truck) of its one rate; ten litres brought to the roadside | 34 + 10 x 2.60 = 60 |
| insurance | per car, 28 days, renews itself; pays 80 % of a repair bill over the first 100 | 0.6 % of list price, 40 to 2,500, +25 % per claim (a claim is forgotten after 56 days) |
| registration and inspection | once a game year (`daysPerYear`), renewable from 30 days before | 60 + 0.2 % (300 at most), 75 more when late |

A service is asked for standing in a filling station's repair bay (the game's own bay, trucks in the truck bay). The
game shows its own panel there ("Repair [F]", "Wash [P]": `GasStationOverlay` in the game's box over a place);
`Garage/GarageBay` gives that panel a second line (what is due and what it comes to, `CarOwnership.BayLine`) and a
third button, "Garage", on the game's confirm key. Its list (`GarageList`) has the service as its first entry with
every part of the bill and the time; "Order" has it all done behind a fade while 40 to 85 minutes pass, and tyres and
brakes under 35 % are changed with it. Worn tyres and brakes act through the wheels' own grip figures and the brakes' own
torque; thirst through the fuel system's own multiplier; a breakdown through the car's own input hook (no pedal;
the brakes work; hazard lights on). Each is recorded before it is touched and put back when the player gets out,
changes car, switches the option off, or the mod unloads. "Easy car ownership" stops fuel use, wear and breakdowns.
The tank is the game's to show (its vehicle panel has the figure, orange from 50 % and red from 30 %), so the mod
draws no gauge. What the game does not know is one more reading in that panel, after "Condition" (`CarDash`, a copy
of that reading made once per city): SERVICE DUE, TYRES WORN, BRAKES WORN in the game's orange, BREAKDOWN in its red,
switched off while nothing needs attention. Broken down or out of fuel and standing still anywhere but in a bay, the
game's panel over a place stands over the car (`GameUi.WorldPanel`) with roadside assistance or a can of fuel on the
game's interact key; `CarOwnership.TakesInteractKey` is true while it is up.

**How the game's bay panel is extended.** The game keeps one `GasStationOverlay` in a public field
(`OverlayUI.gasStation`) and shows it through the interface `UI.Overlays.IOverlay`. `RepairBayOverlay` derives from
the game's class and implements the interface again, so the box's questions (second line, buttons) are answered by
the mod while everything the game does with its own object (which station, repair, wash, refuelling) stays the
game's code. `GarageBay` puts one in the field the first time the box is down and puts the game's own back when the
Garage goes. In a bay the game's first button is named `RepairVehicle`: that is how the mod knows it is one, and of
the vehicle's kind. The game's interact keys are its own repair and wash and its special key also parks the car, so
the workshop is on the confirm key; the game writes the key on the button, as on its own.

With "Developer diagnostics" on: `Garage check (bay)` when the panel is extended and when the button is first
offered in a bay, `(workshop)` when the list opens (with what is due) and closes (with why), `(choice)` for each
entry used, `Car check (dash)` when the reading changes, `(roadside panel)` when the panel over the car comes up or
goes. Not verified in the game: how the box looks with a second line and three buttons; that the confirm key is not
taken by anything else while seated in a bay.

### Asset wish-list (drops in by id, no code change)

Props (`PropAssets`, metres, standing on their origin):

| id | size | pivot and facing | notes |
|---|---|---|---|
| `bus_stop_sign` | 0.60 x 2.85 x 0.07 | ground centre of the pole; the flag hangs to +X (away from the road), its faces to +Z and -Z | pole 0.07, flag 0.50 x 0.62 with its middle 2.42 up, a timetable case at 1.45; the route number is lettered by code at (0.30, 2.53) on both faces in a box 0.44 x 0.34 (the model's plain blue field is from 2.33 m up): leave the upper half of the flag plain |
| `bus_shelter` | 1.70 x 2.50 x 3.76 | ground centre of the floor (1.4 deep, 3.6 long); length along Z, open to -X, back at +X | bench seat 0.46 up along the back from z -1.3 to +0.6, end panel (route map, advert) at +Z; glass may be opaque |
| `city_bus` and its parts | 2.40 x 3.05 x 10.90 | ground centre under the middle of the bus, nose to +Z | **delivered**: the mod's own bus (`Transport/BusVehicle`), its body with the wheels, the door leaves and the lit lamps as props of their own that the code puts together (`PropAssets\README.md`, "Service vehicles"); the shuttle body `minibus` it replaced is gone |
| `turnstile` | 0.30 x 1.00 x 1.20 | ground centre, reader on top at 0.975 | for a subway entrance, if entrances are ever dressed |
| `fuel_pump`, `jerry_can` | 0.5 x 1.6 x 0.4; 0.35 x 0.45 x 0.18 | ground centre; can: handle on top | the mechanic's can for fuel brought to the roadside |
| `warning_triangle` | 0.45 x 0.42 x 0.05 | ground centre, faces +Z, glow | stood behind a broken-down car |
| `car_wash_arch` | 3.6 x 3.2 x 1.0 | ground centre, drive through along Z | the game already washes cars in its repair bays |

Clips (`CityAssets`, Humanoid, in place, starting and ending in the standing idle):

| id | length | loop | what |
|---|---|---|---|
| `board_step` | 1.4 s | once | a step up through a bus door 0.35 m ahead, hand to a rail at shoulder height; for the modelled `city_bus` (`BusVehicle.Modelled`) |
| `strap_hang` | 6 s | loop | standing, one hand up on a strap, swaying: only if a ride is ever shown |
| `sit_bench` / `sit_bench_up` | 1.6 s each | once | onto and off a seat 0.46 m up, 0.30 m behind the feet; with a seated loop `sit_wait` (8 s) for the shelter's bench |
| `refuel_hold` | 5 s | loop | bent at the waist holding a jerry can to a filler 0.9 m up, 0.4 m ahead |
| `bonnet_check` | 6 s | loop | leaning over an open bonnet 0.9 m up, hands working 0.5 m ahead |

Also wanted: a mechanic's overalls in `CityOutfits`/`Uniforms` (the roadside mechanic is in ordinary clothes today).
## City events

`Scripts/Events`, namespace `RealLifeOverhaul.Events`, one component (`EventDirector`). Options group "City events":
a toggle and a frequency slider each for fires, weather and street events, and the test buttons.

**One large scene at a time.** The director runs one fire or one street event, and starts none while
`TrafficAccidents.IsActive`, `CrimeDirector.IsActive` or `PoliceAttention.IsBusy`. Others ask
`Events.EventDirector.IsActive` (the crime director does; see "Hooks" for the accident director).

**Budgets.** People: 8 of the event pool's own (made one every two seconds, and only once a scene or a due event
needs them; none exist before that) and at most 2 officers from `People/ActorPool`. Vehicles: at most 3 (fire
engine, patrol car, the burning car). Particles: 418 for a fire (3 flames x 36, 2 smoke columns x 96, steam 48,
water jet 70) in seven systems and one unshadowed point light; 240 for fireworks (4 x 60) in four systems; never
both. Idle cost: one comparison a frame; once a second two flags and the hour; every twenty seconds a roll.
With a scene on: the marker per frame, the scene four times a second (most of it once a second), and while a
nozzle is held two two-bone reaches in LateUpdate. Savegame: one line of plain text in `modData`
(`reallife:events`): the calendar's number, the days already announced, and the business a fire closed.

### Fires

A car (a private car in traffic pulls in, stops, its driver gets out and away), a bin (one of the street's, or
the mod's own `street_bin` stood on clear pavement), or a shopfront (`CitySpots.Shopfront`: flames at the
frontage and at the wall 4.3 m up, if there is wall there). The lane beside it is closed through a `SceneCordon`
of the director's own. Twenty seconds after the flames, or at once on a 911 call, a fire engine is sent: a
pooled truck dressed by `EngineLivery` (with the modelled `fire_engine` in the bundle a Delivery Truck, which the
body is made to: the same 7.43 m, the same axles, so it wears the body with its wheels on the truck's own; any other
truck, or a van, and every truck without the bundle, is repainted red with a light bar instead), driven by
`EmergencyDriver` to `SceneCordon.Stand` 12 m short of the fire in its lane. Three firefighters get out one a
second: the first takes the nozzle to 3.6 to 5.5 m from the fire, the hose is laid behind them and the jet
arcs on to the fire; the second works the pump panel (on the modelled engine: 0.50 m off the panel of the fire's side, square to it, which is where `pump_panel` has its hands; the first gets out at the front door, the third at the crew door); the third stands between the fire and the street. The
fire is knocked down in three stages of eight seconds (flames to 60 %, to 25 % with white smoke and steam,
out), damped down for eleven, the hose is rolled up, the crew walk back and board, tape and cones come in,
the burnt car is taken away out of view, the road opens and the engine leaves. With the police option on a
patrol car stands behind the engine and its officer works the traffic. Up to three bystanders walk in to a
line 13 to 19 m away and film. If no engine can come the fire burns out by itself. Hard limit 250 s.

*The player's own business.* Only through the game's own mechanisms: `BuildingRegistration.TemporarilyClose`
(the switch the player has on the business screen) for 2 to 10 hours, reopened by the same switch; and a repair
bill of 350 to 1 800 as an ordinary transaction. Both scale with the seconds from first smoke to water (30 s is
best, 120 s worst) and are reduced by the business's security level (up to 40 %), by warning the occupants at
the door (15 %) and by calling 911 (10 %). Stock is never touched. A save's only business is spared in its
first ten days. A business the player had already closed is not reopened by the mod.

*Not done:* the extinguisher (the game has no such item); a fire engine attending a crashed car (see Hooks:
`CrashFire` gives the wreck flames that die down by themselves).

### Weather

The game has rain and nothing else: no snow, fog, wind, temperature or forecast. `Weather` derives:

| | from | accidents | crime | sight | street life | grip | traffic speed |
|---|---|---|---|---|---|---|---|
| mist | the game's dry spell before and after a shower (its own murk) | x1.3 | | x0.6 | | x0.95 | x0.92 |
| rain | drops falling | x1.5 | x0.85 | x0.85 | x0.6 | x0.82 | x0.88 |
| storm | a shower whose top intensity is 0.8 or more (read by reflection; if unreadable, never) | x1.9 | x0.7 | x0.7 | 0, `TakeCover` | x0.72 | x0.8 |
| heat wave | a summer day, by the day's roll | | x1.3 | | x0.85 | | |
| cold snap | a winter day, by the day's roll | | x0.85 | | x0.8 | | |
| ice | one cold-snap day in three | x1.4 | | | x0.7 | x0.75 | x0.9 |

Seasons are quarters of the game's year (`gameVariables.daysPerYear`, 60 by default), starting in midwinter.
`WeatherEffects` applies grip to the driven car's `LateralFrictionGrip` and `LongitudinalFrictionGrip` (the
values `Driving/CarTuning` reads; recorded per wheel and written back exactly), narrows each pooled traffic
vehicle's `minPossibleSpeed`/`maxPossibleSpeed` (the traffic system has no speed factor; it takes effect as
cars are put on the road; originals written back), spends 1 to 2 energy an hour out of doors in heat (11 to
19 h) and cold (never below 15; `Weather.Wardrobe`, if anybody sets it, says whether a coat is worn), gives the
morning forecast (toast and City Bulletin) and a warning when the weather turns. *Not done, by design:* slipping
on ice (nothing tasteful to be had from the knockdown system without editing it); a power cut is a notification
only (the game has no safe way to switch a district's shops off); sight is exposed, not applied (see Hooks).

### Street events

On the calendar (`EventCalendar`): fireworks 21 to 24 h on the last day of the year and on the day 50.7 %
through it; a farmers' market 8 to 13 h every Saturday; otherwise a day has an event if its roll is under
0.08 x the slider: market 10 to 16, parade 12 to 15, protest 11 to 15, fun run 8 to 11, film shoot 9 to 17.
Announced at 18 h the day before and at 7 h on the day (toast and City Bulletin). It is held once, on a plain
stretch of road 55 to 110 m from wherever the player is when it is due and they are out of doors, out of view;
20 to 32 m of road is closed with the cordon's tape and barriers; staff walk in, then the crowd. At closing time
(or in a thunderstorm) the crowd leaves, the staff pack and walk off, the tape comes down, and the props go once
nobody is looking.

| event | on the closed road | the player can |
|---|---|---|
| market, farmers' market | up to three stalls (`vendor_cart`, `fruit_stand`, `coffee_cart`, a parasol), a vendor behind each, up to four customers who walk in, wait, pay and leave with a cup, a marshal | buy a snack for 6 (energy +6, mood) |
| parade | four marchers with banners and placards who form up and march end to end and back, two marshals, two onlookers on the pavement who clap as it passes | cheer (mood) |
| protest | five with placards, an organiser with a clipboard, two officers watching from one end | sign the petition (mood). Always peaceful: the crime system has no public way to arrest a bystander |
| fun run | runners with numbers who jog in, through and off, two marshals, onlookers | cheer (mood; the nearest runner waves) |
| film shoot | camera, two lamps, a chair; operator, director, assistant on the radio, two actors who play a take of about twenty seconds and wait between takes, a marshal | watch a take (mood); "Quiet on set" within 32 m |
| fireworks | a rack, a marshal, up to six people who walk in and watch; bursts 58 m up, 85 m off to one side | watch for twenty seconds (mood) |

Footfall for nearby businesses is not changed: the game rebuilds a business's promotion every day and its
traffic index is not saved, so there is no safe mechanism; attending gives a mood instead.

### Hooks other features need (one line each)

- `Traffic/TrafficAccidents.Tick` (read-only to this work, so not made): the roll becomes
  `Random.value < Settings.AccidentFrequency * 0.02f * Events.Weather.AccidentFactor`, and its start condition
  gains `&& !Events.EventDirector.IsActive`.
- `Traffic/TrafficAccidents`, where a severe crash is staged: `Events.EventDirector.CrashFire(wreck)` sets that
  wreck alight for 50 s (flames, smoke, firelight; no engine).
- `Traffic/PursuitSight` and `Crime/PoliceAttention`: multiply their sight distances by `Events.Weather.SightFactor`.
- A wardrobe feature: `Events.Weather.Wardrobe = <IWardrobe>`.
- Made already: `Crime/CrimeDirector` (`* Events.Weather.CrimeFactor`, `&& !Events.EventDirector.IsActive`),
  `CityLife/CityLifeDirector` (`&& !Events.Weather.TakeCover`), `Phone/PhoneContacts` (the 911 dialog offers
  "Report a fire" and calls `Events.EventDirector.Report911()`).

### Asset wish-list

Everything below is asked for by id through `ClipLibrary.Has/TryGet` or `PropLibrary.TryGet`; when the bundle
has it, it is used with no code change. Until then: the fallback named.

Clips (Humanoid, in place, starting and ending in the standing idle unless said):

| id | length, loop | what | until then |
|---|---|---|---|
| `hose_hold` | 4 s loop, whole body | braced: feet apart, left foot forward, knees bent, leaning into it; right hand at the right hip, left hand 0.24 m ahead of it, both on a bar pointing forward and 12 degrees up, 1.02 m above the ground and 0.17 m right of centre; small recoil sway | **delivered** (palms on the nozzle 0.06 and 0.30 m along it). `HoseHold` keeps the wrists where the clip's own grip has them on the nozzle (`CityClip.Grip`: the nozzle on the right hand bone, the left wrist on the nozzle), so at the clip's aim nothing is corrected and at any other (3 to 45 degrees up) the palms stay on the grips; it used to put the wrists on the palms' points, 0.08 m too far along. Without the clip: `carry_box_hold` on the arms, both wrists put on those two points by `TwoBoneReach` |
| `pump_panel` | 4 s loop | standing square to a panel 0.5 m ahead: turning a valve at 1.1 m, reading a gauge at 1.4 m | `counter_wipe` |
| `keep_back` | 3 s loop | both palms out at chest height, a step forward, a look over the shoulder | `direct_traffic` |
| `crowd_look_up` | 6 s loop | head back, looking 50 degrees up, a hand shading the eyes now and then, pointing once | `watch_nod`, `take_photo`, `clap` |
| `placard_hold` | 4 s loop, right arm mask and a whole-body variant | a staff held upright in the right fist at 1.10 m, pumped up 0.15 m twice a loop | `paddle_hold` (its grip; the stop paddle's) |
| `camera_operate` | 6 s loop | at a tripod camera: right eye to an eyepiece 1.50 m up and 0.16 m ahead, right hand on a pan bar, left on the lens | **delivered**, for somebody 0.75 m behind `film_camera`'s pivot on the lens's line. `StreetEventScene.FilmSet` takes that place from where the camera really stands, `Arrivals` walks the operator's last steps on to it (within 0.08 m, the agent sent as far past it as it stops short) and the clip is played only there; anywhere else, and without the clip: `guard_stand` with `till_tap` |

Props (metres, X x Y x Z, pivot as said; all "lit" unless said):

| id | size | pivot and facing | until then |
|---|---|---|---|
| `fire_engine` | 2.72 x 3.20 x 7.43 | ground centre under the middle of the body, nose at +Z; axles at z +2.756 and -1.362 | **delivered, and rebuilt to the Delivery Truck** (2.72 x 3.01 x 7.43, wheelbase 4.118; the Freight Truck is 10.98 m, too long for a pumper): `EngineLivery` puts its axles on the truck's (the game's trucks have their origin under the rear axle, not their middle). On any other vehicle: the truck repainted red through property blocks, a light bar on its roof |
| `fire_bar_lens` | 0.60 x 0.13 x 0.30, glow | the middle of the underside of the engine's light bar; the lens at +X | **delivered**: a lit copy of one end of the modelled engine's light bar, shown over each end in turn by `BarFlash` in step with the two flashing lights (only while an engine is out) |
| `fire_light_bar` | 1.30 x 0.17 x 0.28, glow | centre of its underside; lies along X | shapes |
| `fire_helmet` | 0.30 x 0.19 x 0.40 | centre of the rim's lower edge, peak at +Z: put 0.085 m above the head bone | shapes |
| `fire_air_pack` | 0.34 x 0.75 x 0.40 | the upper-chest bone: cylinder on the back, harness, reflective bands front and back (as `mail_satchel` is modelled as worn) | shapes |
| `hose_nozzle` | 0.07 x 0.09 x 0.47 | the coupling at its back end; points along +Z; gripped 0.06 and 0.30 m along it | shapes |
| `street_bin` | 0.54 x 0.90 x 0.54 | ground centre | shapes |
| `placard` | 0.56 x 2.19 x 0.03 | the stop paddle's: the hand is 1.10 m above the pivot; the staff runs from 0.70 to 1.75 m (it is carried clear of the ground), the board above it faces +Z | shapes |
| `banner_pole` | 0.72 x 2.20 x 0.03 | as `placard`; a cloth 0.70 x 0.48 to +X of the staff | shapes |
| `runner_bib` | 0.22 x 0.17 x 0.01 | the upper-chest bone; on the chest 0.134 m ahead of it, 0.07 m below | shapes |
| `film_camera` | 0.9 x 1.7 x 0.9 | ground centre of the tripod; the lens looks along +Z, 1.47 m up | shapes |
| `light_stand` | 0.9 x 2.5 x 0.9, lamp glow | ground centre; the lamp 2.32 m up shines along +Z | shapes |
| `directors_chair` | 0.55 x 1.10 x 0.45 | ground centre; faces +Z | shapes |
| `firework_rack` | 1.2 x 0.55 x 0.5 | ground centre | shapes |
## Garage

Car customisation and two-wheelers, in `Scripts/Garage`. Everything is kept in the save's modData, keyed by the
vehicle's own id; the vehicles in the save stay exactly what the base game made, so a save loaded without the mod
shows stock cars and ordinary scooters.

### How the game paints a car (read from its files with `GarageAssets/scripts/extract_vehicles.py`)

- Every player vehicle is **one body mesh on one material** of the shader `Shader Graphs/SH_Vehicle` (18 materials,
  one per model; the scooter too). Paint, glass, lamps, trim, number plate and the wheels all share that material's
  one texture; a mask in the texture says where the paint is. The wheels are separate renderers on the same material.
- `CarFeatures.SetColor(VehicleColor)` writes three properties into a `MaterialPropertyBlock` on each of
  `CarFeatures.bodyMeshes`: **Tint** (`Color_3d0f0cdbe6b74be28a1a5be5bab71dea`), **FresnelColor**
  (`Color_f78fac473bac467092fb27521e9f71ea`, the colour the paint turns at a glancing angle) and **FresnelPower**
  (`Vector1_481fa2a8a5e94165a039319bfd512b76`). It is called once, from `VehicleController.SetVehicleInstance`, with
  the `VehicleColor` whose name is in `VehicleInstance.vehicleColorName` (chosen at the dealership). Dirt is a
  fourth property, `_Dirtiness`, written the same way (get block, set, set block), so ours survive it.
- The shader has **no metallic, clear-coat or per-part property**. What else it has: `SmoothnessBoost`
  (`Vector1_41991b689ccf4932ad504975ac34399a`, one gloss factor for the whole material), `_EmissionColor` (the
  colour of every lamp), and the lamp and indicator strengths the game drives itself.

So: **paint** is Tint in any colour, a **finish** is FresnelColor + FresnelPower + SmoothnessBoost, **lamp tint**
is `_EmissionColor` and the colour of the car's own headlamp `Light`. All through the same property blocks, once
per change. And these cannot be done on the game's cars, and are not offered: **window tint** (the glass has no
material or property of its own), **wheel colour** (the rims are in the shared texture, outside the paint mask),
**number plate text** (the plate is painted into the body texture), **two-tone roofs, stripes and liveries** (one
material and one UV atlas per car; a flat stripe laid over a curved bonnet either floats or cuts into it, on every
car differently). A matte finish dulls the glass and trim too, because SmoothnessBoost is material-wide; the shop
says so.

### The custom shop

At every repair bay the game has (the `GasStationTrigger`s its `GasStationController`s list as repair stations:
auto repair shops and truck garages). Drive a car of your own into a bay, stop, and use the "Garage" button of
the game's panel there (see "Car ownership"); "Custom shop" is the second entry of its list. The car is held where
it stands (its pedals taken off the keys, its body frozen the way the car wash freezes it), the mod's own camera goes
onto a turntable round it (drag on the free part of the screen to turn, wheel to move in and out), and the game's
list window stands left of the middle, clear of the game's task panel, phone and item panel, with the car seen
to its right (the camera stands a fifth of its distance to the left of its line to the car) (`GarageList`: a copy of the window the game lists its subway stations
in, 1,100 wide with entries of 150 so that an entry can say what a part does). Its first page has the bill ("Pay"),
one entry per page below, and "All stock"; a page has "Back" and one entry per choice, whose button reads "Chosen"
and is off for the one in the cart. Every choice is on the car at once; the lines under the headline show the money,
the bill and the car's figures before and after. A colour of one's own is mixed in the game's own colour picker
(`UIs.customColorPicker`, the one its interior designer uses: the car is repainted as the colour moves; its button
keeps the colour, and also adds it to the game's list of the player's colours, which is what that picker does;
closed any other way, the paint is as it was). **Pay** keeps the build; **Cancel** (the list's yellow button or the game's cancel key, or
getting out, or walking away) puts the car back as it came in; **All stock** takes everything off. Work is instant: the game has no notion of
a car that is away being worked on, and a car hidden or taken out of the save's vehicle list for a few hours
would be a save the base game cannot account for.

| page | what | price |
|---|---|---|
| Paint | 42 chart colours in six families of seven shades (an entry per family, written in the shade chosen, whose button steps to the next shade), the colour it was bought in, or any colour mixed in the game's colour picker | 600 chart, 900 mixed, times the body factor |
| Finish | gloss, satin, matte, metallic, pearl | 150 / 300 / 500 / 700 / 1 200, times the body factor |
| Lights | lamp tint: standard, warm white, ice blue, rally yellow; underglow kit in seven colours | 150; 400 (another colour later 100) |
| Body | boot spoiler, roof bars, bull bar: each only on the bodies it fits; other wheels when the prop bundle has them | 450, 350, 600; wheels 1 200 |
| Parts | one part in each of eight slots (below); an entry's button steps to the slot's next part | a multiple of the part base |

Body factor: economy 1, saloon 1.2, utility 1.4, van 1.5, luxury, sports and trucks 2. Taking a fitted thing off
costs 50. Half of everything spent on a car counts as value added (`GarageBridges.ValueAdded`).

**Fitted parts are placed from each body's measured shape**, not its bounding box:
`GarageAssets/scripts/extract_fit.py` reads every triangle of every car body and writes one row per car into
`GarageFit.cs` (roof: where it is level, its height at the bars, its half width, the height under the feet; boot
deck: height, slope, half width; nose: its low and high edge, half width, bonnet height). A **spoiler** is offered
where the boot deck slopes under 25 degrees and is at least 0.25 m below the roof (AF90, 112, Mimic, limousine,
MGA GT, S500, Fanton, Pony), **roof bars** where the roof is level for 0.9 m (everything but the sports cars and the trucks), a **bull bar** on SUVs, pickups and vans (Nunavut, V150, Desert), as tall as the upright part of the nose allows. The
Mersaidi Dash has no measured body (its prefab is built differently) and gets none; neither does a car from
another mod. Stand-ins are plain black shapes from `Core/Shapes`; the modelled parts replace them without a code
change when the prop bundle has them (wish-list below).

### Performance parts

Layered on the per-car model of "Cars" above: a build **divides the 0-100 km/h time and multiplies the top
speed**, and `CarModel.Solve` finds the power, gearing and drag for the new targets. Brakes, tyres, springs,
weight and the engine's note are changed on the car itself (NWH: `brakes.maxTorque`, the wheels' friction grip,
spring force and length, damper rates, the axles' anti-roll force, the body's mass, the engine sound's pitch
and volume), recorded first and put back exactly.

Part base by kind: economy 1 000, saloon 1 800, utility 2 500, van 2 000, truck 3 500, luxury 4 500, sports 6 000.

| slot | part | x base | effect | cost in use |
|---|---|---|---|---|
| Engine | Stage 1 / 2 / 3 | 1.0 / 2.2 / 4.0 | acceleration +6 / +12 / +20 %, top speed +2 / +4 / +7 % | wears 5 / 12 / 25 % faster; stage 3 insurance +10 % |
| Exhaust | Sports / Straight-through | 0.5 / 0.9 | acceleration +3 / +5 %, top +1 / +2 %, higher and louder note | straight-through is not street legal |
| Turbo | Turbocharger / Big turbo | 2.5 / 4.5 | acceleration +10 / +18 %, top +5 / +8 % | wears 10 / 22 % faster; big turbo insurance +10 % |
| Gearing | Short / Long final drive | 1.2 | acceleration +7 %, top -8 % / acceleration -6 %, top +6 % | |
| Brakes | Sports / Race | 0.8 / 1.8 | brake torque +25 / +50 % | race: wears 5 % faster |
| Tyres | All-weather / Sports / Semi-slick | 0.7 / 0.6 / 1.2 | wet grip +15 % / dry +10 %, wet -10 % / dry +20 %, wet -25 % | semi-slick: wears 8 % faster |
| Suspension | Sports springs / Coilovers / Lift kit (SUVs, pickups, vans) | 0.9 / 1.8 / 1.0 | stiffness and anti-roll +25 % and 2 cm lower / +50 % and 4 cm lower / +10 % and 4 cm higher | |
| Weight | Lightened interior / Stripped out | 1.0 / 2.5 | weight -5 / -10 % (as much quicker) | stripped out: insurance +5 % |

However the parts stack, a build is never asked for more than 1.6 times the acceleration or 1.25 times the top
speed. A car is lowered only inside the travel its own suspension already uses (at most a quarter of what is
left of the spring at rest, never more than 5 cm). "Wet" is the game's rain unless a Weather feature sets
`GarageBridges.RoadIsWet`. The shop shows 0-100 and top speed from the model (when the player sits in the car
and car tuning is on) and the stopping distance from 100 km/h from the brakes, tyres and weight; what is applied
is logged once per car and build ("Garage: ..."), and "Measure this car" times the real thing.

**The hook for `Driving/CarTuning`** (two lines; until they are there, `GarageTuning` does the same work itself
after CarTuning has run: an engine power modifier, the gear ratios, and the difference in drag as its own force,
each put back exactly; with car tuning off it falls back to the power modifier and the game's speed limiter):

```csharp
// CarTuning.Update, the end of the "options" expression: a build that changes is a change of options
    | (Mathf.Clamp(Settings.CarAcceleration, 70, 150) << 10) | (GarageTuning.Stamp << 18);
// CarTuning.Apply, after "float topKmh = ..." and before CarModel.Solve
GarageModifiers garage = GarageTuning.Claim(car.vehicleInstance); seconds /= garage.Acceleration; topKmh = Mathf.Min(topKmh * garage.TopSpeed, MaxTopKmh);
```

The first call of `Claim` makes `GarageTuning` stand down for good, so the two never both apply.

A straight-through exhaust driven above 36 km/h is noticed by the police now and then (a look every 30 s, 8 %,
once a day at most): `GarageBridges.PoliceNotice("exhaust", 120)` if a Crime feature has set it, otherwise a fine
of 120 through `Crime.Fine`. For a Transport feature: `GarageBridges.WearOf(id)`, `InsuranceOf(id)`,
`ValueAdded(id)`, `LoudnessOf(id)`, `IsTwoWheeler(id)`, and `GarageSummary.Lines` for a garage page of its own.

### Two-wheelers

The game's electric scooter is an NWH vehicle with a motorcycle module (lean, steering), a 45 kW engine, one
forward gear, 250 kg, a 45 km/h limiter, and a rider the game attaches to it in a standing pose with the hands
on its bar. **A bicycle or motorbike here is one of those scooters**: made with
`VehicleHelper.CreateAndSpawnVehicle(new VehicleInstance("ba:vehicletype_electricscooter"), ...)`, ridden with
`ScooterController` untouched, saved in the save's own vehicle list. modData says which ids are what
(`reallife:garage_bikes`). `BikeMount` then, on the spawned object only:

- **body**: the scooter's renderers are set not to draw (`forceRenderingOff`; nothing is removed) and a body is
  built under it (`BikeBody.Build`): the modelled bike when the bundle has it, else a stand-in of tubes, rings and
  boxes in three shared meshes per part (a diamond frame, spoked wheels, bars, saddle, cranks and pedals; or tank,
  seat, engine, forks, exhaust, fat tyres). Either way wheels turn with the speed, the fork and front wheel steer,
  the cranks go round with level pedals while the rider pedals. The modelled bike is put together from its parts
  (`<id>_frame`, `_fork`, `_wheel` or `_wheel_front` / `_wheel_rear`, `_crank`, `_pedal`: at most nine renderers,
  all shared meshes), each on the object `BikeMount` turns; nothing turns while nobody rides.
- **colour**: what is painted (frame tubes, mudguards, tank, tail, fairing) is a part of its own on a neutral
  swatch (`<id>_frame_paint`, `<id>_fork_paint`) drawn with `PropLibrary.Tinted(colour)`: the atlas material with
  its base colour multiplied, ONE shared instanced material per colour (never per bike, no property blocks), gone
  with the library's other materials when the city unloads. The colour is `BikeRecord.Colour`, else
  `BikeDef.Colour`, which is the colour the whole model is painted in the atlas (city 0x124D70, road and sport
  0x9E090A, roadster 0x090F2B). A bike that only stands on show in that colour (`Build(..., still: true)`: the
  shops' racks) is the whole model as one mesh, one draw call; the docks' bicycles are in the share scheme's
  blue, so they are parts.
- **grips**: `BikeDef.Grip` is per model and measured on its bar by `PropAssets\scripts\checks_garage.py`: the town
  bar's rubber grips, the TOPS of the road bicycle's drop bar (0.13 either side of the stem: the one part of a drop
  bar that lies along X, which is what the game's hand rig holds), the roadster's bar, the sport bike's clip-ons
  under the top yoke.
- **numbers**: a bicycle gets a small "engine" behind the pedals (1.2 or 1.6 kW for the scooter's 250 kg), almost
  no engine braking, a bicycle's rolling resistance, air drag sized so that steady pedalling holds 38 or 50 km/h (24 and 32 before 0.1.4, which felt slow against the city),
  and no engine sound. A motorbike gets power, final drive and drag from `CarModel.Solve` for its targets, the
  limiter off and 1.6 times the brakes. Logged once per kind ("Two-wheelers: ...").
- **rider**: the game's own riding pose, moved so that the hips are over and just above the seat (fitted to the
  character's own hip height), the hands on our grips through the game's own hand rig, and after the animation
  each frame the legs put on the pedals (following the cranks) or the footpegs (`LegIk`, knee forwards). A helmet
  follows the head on a motorbike once the player owns one, turning with it (`BikeMount.HeadSquare` reads from
  the avatar's T-pose how the Head bone's own axes stand to the character; if the avatar does not say, the helmet
  keeps the player's heading as before). With the clip bundle the mod's own loops are played instead, chosen by
  what the rider is doing (`BikeDef.Ride`, `Coast`, `Stand`; `BikeMount.Pose`: three comparisons a frame while
  ridden, the engine told only on a change that has lasted 0.3 s, 0.1 s to set off):

  | | riding (a bicycle: pedalling) | freewheeling | stopped (under 0.35 m/s, no throttle; riding again over 0.8 m/s) |
  |---|---|---|---|
  | city bicycle | `cycle_ride` | `cycle_coast` | `cycle_stand` |
  | road bicycle | `cycle_ride_road` | `cycle_coast_road` | `cycle_stand_road` |
  | roadster | `moto_ride` | | `moto_stand` |
  | sport bike | `moto_ride_sport` | | `moto_stand_sport` |

  A model's own clip missing, the town bicycle's or the roadster's is used; none at all, the game's pose. On a
  bicycle the feet stay on the pedals through `LegIk` while riding and freewheeling; stopped, the clip has the
  left foot on the ground, so `LegIk` is faded out over 0.3 s and the cranks are brought round (backwards, as on
  a freewheel) to the right pedal forward, where the clip's right foot is. The bike itself stays upright (the
  game's scooter physics): the stopped clips lean the rider, not the machine.

| model | at | price | goes |
|---|---|---|---|
| City bicycle | bike-share docks (hire 15, back to any dock by the end of the next day, else collected with a 25 late fee); racks outside supermarkets | 350 | about 38 km/h, 0.06 energy a second of pedalling |
| Road bicycle | racks outside supermarkets | 900 | about 50 km/h, 0.08 energy a second |
| Motorbike | stands outside car dealerships (at repair garages if the city has no dealership open) | 7 500 | 0-100 in 6.0 s, 150 km/h |
| Sport motorbike | the same stands | 18 000 | 0-100 in 4.5 s, 190 km/h |
| Helmet | the same stands | 120 | riding without one: a look every 30 s above 18 km/h, 10 %, once a day, fine 60 |

Docks stand beside every other one of the game's scooter stands (eight at most), racks outside up to five
supermarkets, stands outside up to four dealerships: found once per city load, in a fixed order, each on pavement
with room for the whole thing. Within 3.5 m of one the game's panel over a place (`GameUi.WorldPanel`) names it and
has one button on the game's interact key: "Hire" (with the fee), "Return" (seated on a hired bicycle that stands),
or "Browse", which opens the game's list with what the rack or stand sells (buy, a helmet, sell an owned one standing
by for half its price). `GarageDirector.TakesInteractKey` is true while the panel is up. Holding the throttle is pedalling: it costs energy through the game's own
`EnergyHelper`, never below 14; a worn-out rider has half the push; letting go coasts. A motorbike asks
`GarageBridges.HasMotorcycleLicence` (null: yes) only at the sale: the dealer warns in a sentence if it is no and sells all the same. Riding unlicensed is policed by Licences (the warning on getting on, the roadside check: fine, tow, arrest the third time). Coming off (most of the speed lost in one physics step, or
the bike on its side while moving) calls `GarageBridges.RiderFalls(speed)` if a Ragdoll feature has set it,
otherwise the bike stops and the rider is on their feet. Traffic sees a ridden two-wheeler as it sees a ridden
scooter: the game puts its collider on the vehicles layer.

**Where one is put, and finding it.** A place faces along its pavement, so "beside it" is the shopfront on one
side and the kerb on the other. A bike handed over is therefore stood **along the pavement** from the place
(2.6 m, 3.4 m, then the corners, the sides last), and a spot must pass four tests: ground under it
(`BikePlaces.StandHeight`: the ground ray, or the pavement's own surface where that is higher), pavement
(only if no pavement spot is free is level ground in sight accepted), room for the whole bike (`Fits`), and
**a clear line from the place to it** (`BikePlaces.Seen`). The last one matters: `Fits` asks whether a box
touches anything, a wall is a skin of triangles, and a box wholly behind a shopfront touches nothing, so a spot
indoors "fits" (v0.35.0 tried the sides first, without the line: a motorbike bought at a stand a step out from
a dealership wall could be made behind that wall, the likeliest reason the first one bought was never seen). The test buttons and "Bring it here" use
`BikePlaces.SpotAhead`: 2.2 m in front of the player on the ground under that point, side on, in their sight.

- The game marks the player's cars on the map and, within 100 m, on the street (`CityMap.AddPoi`, set on
  `VehicleController.poi`); it leaves scooters out on purpose. `BikeMount.Mark` gives ours that same marker
  under their own name; the game hides it while it is ridden and destroys it with the vehicle.
- On buying, hiring, a test button, "Show me" and "Bring it here" the game's own destination marker
  (`GameUi.GuideMark`: the guider the game marks a job's destination with) is on the bike for 20 s (30 s for
  "Show me"), until the player is within 3 m, on it, or indoors; the toast says how far and which way. A job
  or an outing that has that marker keeps it, and the bike's is asked for again four times a second. Nothing
  per frame.
- "My vehicles" gives every two-wheeler its address, district and distance, with **Show me** and **Bring it
  here** (on foot, out of doors; free from within 25 m, otherwise 20; one the game no longer has in the world
  is made again, the same sort and colour).
- An owned one found behind a building's wall when a save loads (`BikePlaces.Walled`: pavement near it, a wall
  between) is said once, with the way out.
- Parking: a two-wheeler's `parkingState` is `Undefined`, so the game's hourly public-parking charge and its
  parking tickets ("electric scooter") skip it.
- The log says everything about one that is made, and again two seconds on ("Two-wheelers: made ...", "two
  seconds on ..."): position, heading, tilt, the ground found under it, pavement or not, how many of the
  body's renderers are drawn, forced off or without a material, their bounds, how many of the scooter's own
  are hidden or still drawn, active state, layer, scale, collider layer, rigidbody, physics, map marker. One
  that is no longer where it was put (fallen, flown, on its side) is stood back once and held as the game
  holds a parked scooter.

**Saving.** The game removes a scooter four hours after it was last looked at (`AutoDestroyVehicle`); on ours that
component is switched off, and they are saved with `lastSeen` empty, so with the mod they stay. Without the mod
they are scooters, and the game clears them away by its own rule once they have been seen and left. An owned one
that went that way is stood back where it was last left, the next time the save is loaded with the mod. A build
is dropped from modData when its car is no longer in the save (sold).

Not done: carrying a bicycle; a phone entry of its own for "My vehicles" (it is one of the Garage's lists in the
game's window, from the workshop's list and the options page, and `GarageSummary.Lines` for another feature's page; a
car is an entry to read with its button off, a two-wheeler has an entry for "Show me" and one for "Bring it here"). The item panel still calls
a two-wheeler an electric scooter, because to the game it is one. Not verified in the game (it could not be
run): that the v0.35.0 motorbike was behind the wall (the code allows it and nothing else found does; the log
line above settles it), whether the pavement's ground collider is on a layer the ground ray sees (hence
`StandHeight`), and how the scooter's physics settles one that is made with nobody on it.

### Wish-list (assets that replace a stand-in with no code change)

Props, in `Bundles/reallife_props` under these ids (metres; +Z forwards, +Y up):

| id | size | pivot | notes |
|---|---|---|---|
| `car_spoiler` | 1.40 wide x 0.18 high x 0.26 deep | on the boot deck under the middle of its two feet (feet at x = +-0.42) | scaled in X to the deck; dark, satin |
| `car_roof_bar` | 1.20 wide x 0.11 high x 0.06 deep | on the roof under the middle, feet at x = +-0.52 | one bar; two are fitted; scaled in X |
| `car_bull_bar` | 1.00 wide x 0.60 high x 0.14 deep | bottom centre of the hoop; the mounts reach 0.12 m behind it | scaled in X and Y |
| `wheel_5spoke`, `wheel_mesh`, `wheel_steel` | radius 0.34, 0.22 wide | centre of the wheel, axle along X, outer face at -X | a whole wheel, tyre included: it replaces the car's own; scaled to each car's wheel |
| `bicycle_city`, `bicycle_road` | 1.78 long x 1.14 high x 0.56 wide | ground under the middle; wheels (radius 0.34) at z = -0.52 / +0.58 (road -0.49 / +0.55); saddle top 0.93 at z -0.30 (road 0.95, -0.29); grips at (+-0.26, 1.14, 0.16) (road: the tops of its drop bar, +-0.13, 1.00, 0.42); bottom bracket (0.29, -0.08) (road 0.28, -0.07) | delivered whole and in parts (`_frame`, `_frame_paint`, `_fork`, `_fork_paint`, `_wheel`, `_crank`, `_pedal`): see `PropAssets\README.md`, "Garage" |
| `motorbike_standard`, `motorbike_sport` | 2.05 long x 1.10 high x 0.75 wide | ground under the middle; wheels (radius 0.33) at z = -0.651 / +0.723 (the scooter's own); seat top 0.83 at z -0.20 (sport 0.84, -0.22); grips (+-0.31, 1.075, 0.30) (sport: clip-ons, +-0.27, 0.915, 0.42); pegs (+-0.24, 0.34, -0.10) (sport +-0.24, 0.38, -0.16) | whole and in parts (`_frame`, `_frame_paint`, `_fork`, `_fork_paint`, `_wheel_front`, `_wheel_rear`) |
| `helmet` | 0.27 x 0.29 x 0.30 | the base of the skull (the Head bone), +Z the face | full-face |
| `bike_dock` | 2.6 wide x 1.25 high x 0.2 deep | ground centre, 0.80 m behind the hoops' line (the bikes stand at the pivot, front wheels in the hoops) | three hoops 0.8 m apart, a pay post at +X |

Clips, in `Bundles/reallife_anim` (Humanoid, in place, loops; the table under "rider" above, the numbers in
`CityAssets\README.md`, "Garage"): `cycle_ride` (seated upright, the ground of the clip 0.93 m below the saddle
surface, hands forward at 1.14 m, legs anything: they are placed on the pedals afterwards), `moto_ride` (seated,
seat 0.83 m up, feet on pegs 0.34 m up and 0.24 m out, hands on bars at 1.075 m and 0.50 m ahead of the hips),
and for each model its freewheeling and stopped loops and the road bicycle's and sport bike's own riding loops. Sounds: a freewheel tick and a bell for bicycles, an air tool for the shop.

### Testing

Options, "Custom shop and two-wheelers": open the custom shop on the current or nearest car wherever it stands;
a random full build (free); restore to stock; "My vehicles"; a city bicycle, road bicycle, motorbike or sport
motorbike 2.2 m ahead, side on, with the marker on it. A button that cannot do it says why (`Core/TestRefusal`: not loaded, option off, no
car of yours within 20 m, not a car, a job's or hire company's vehicle, shop already open, in a vehicle, no clear
ground, indoors, traffic not running). `Player.log` has one line for each thing applied. The shop and "My vehicles"
asked for on the options page open when the menus are closed (the game's list stands on the HUD, which is not shown
under a menu), and a notification says so.

With "Developer diagnostics" on: `Garage check (custom shop list)` when the shop's list opens, on each page and
when it closes (with why); `(choice)` for every choice with the bill; `(place panel)` when the game's panel comes up
or goes at a dock, rack or stand; `(stand list)`, `(my vehicles)` and `(marker)` likewise. Not verified in the game:
how the list reads at 1,100 wide with entries of three lines; that a name written in a dark colour is readable (a
colour darker than 22 % grey is lightened for the name only); that the game's colour picker draws over the HUD
outside its interior designer; that the turntable's drag surface, the first object of the HUD's canvas, takes the
drag where nothing of the HUD is in front of it.
## Meta: the guide, presets, the options page, save tools

`Scripts/Meta` is the mod's part about itself. It names no feature outside one file, `FeatureList.cs`: everything
else reads the registry that file fills, so a feature that registers appears in all of it.

```
Meta/
  Features        the registry: Feature (fluent), Features.Add / Find / OwnerOf...; [DescribesFeature] for self-registration
  FeatureList     every feature, one statement each; the place for features merged later
  SettingsTable   every saved option, found by itself: Settings' "XId" + "X" fields (id, kind, shipped default,
                  captured at the top of the first Settings.Load), the rows of the options page (label, range),
                  and what a feature declares for options kept elsewhere (performance tweaks, looks)
  Presets         Relaxed / Standard / Realistic / Hardcore / Custom, reset everything, the settings file
  OptionsLayout   puts the finished options page in order: sections, test tools, the buttons at the top; the set-up check
  OptionsSearch   the other half of that class: the search box under the page's first header, and the words features are also found by
  OptionsCaptions a third part of it: the word on each button of the page ("Open", "Close", "Run", "Apply")
  MetaUi          the mod's own canvas (above the menus, off unless a panel is open) for the options page's messages and questions only
  Guide           the guide's pages and texts (topics, facts, mark-up) and its phone contact
  GuideApp        the Guide as an app on the game's phone (GameUi.PhoneApp), asked for from the options page, the contact and the welcome notice
  GuideView       the app's panel in the game's frame (GameUi.AppShell): a table of pages, a table of the lines of a page
  Welcome         the first-run / after-update notification, with a click that opens the Guide on "What changed"
  Changelog       what changed per version (guide page, "since last time")
  SaveTools       prepare a save for removing the mod, reset the mod's data, repair, the check after each load
  MetaCity        Meta's part of a city load; Dev: the "Developer diagnostics" switch
```

### Registering a feature (one statement)

At the marked place at the end of `FeatureList.RegisterAll` (or the same statement in a `static void DescribeFeature()`
of a class of the feature's own marked `[Meta.DescribesFeature]`):

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

Only `Add` is required. The feature's rows stay where they are added today (the chain in `RealLifeOverhaulMod.cs`),
its options stay fields of `Settings` with a `Settings.Load` line each. What happens without any registration: its
header becomes a section of its own, its options are in the settings file and "reset everything", its buttons with
`_test_` in the label move to "Test tools", its `reallife:` modData keys and `reallife-` contacts are still removed
by the save tools, and its `Logger` is reached by the diagnostics switch. What it does not get: a guide page, a
place in the presets, a clean-up call. "Check the mod's set-up" (options page, Diagnostics) lists every such gap.

Rules that make this work: option ids are `public const string XId` beside `public static bool|int X` in `Settings`;
modData keys start with `reallife:`; contact ids with `reallife-`; a class that logs has a static `IModLogger Logger`.

### What the mod API allows on the options page

One flat page for all mods (`ModOptionsViewController`), built from `ModOptions`: header, toggle, slider, dropdown,
button, splitter, and `AddCustom` (a row that draws itself under the page's content object). There is no text
block, no sub-page and no way to scroll to a row. Toggles, sliders and dropdowns call their callback with the saved
value every time the page is drawn, which is why the presets are buttons and not a dropdown. `OptionsLayout`
therefore rearranges the finished list: sections are a button followed by the feature's rows, and a row that draws
nothing (`RowTap`) on either side tells it which of the game's row objects are that section's, to switch on and
off. Open/closed is kept per section in PlayerPrefs (`m:<mod>:ui_open_<feature id>`). Every row is re-made with the
shipped value as its default, so the game's own "reset" resets (the chain passes the loaded values).

"Test tools" is a section of sections: one group per feature that has test buttons, in the order of the sections,
each a button ("Tests:  Crime and police stops   (14)") that opens and closes like a section and is closed until
asked for (`m:<mod>:ui_open_tests_<feature id>`). A feature that is not registered is named by its header without
"Real Life Overhaul - " in front. "Open or close every section" leaves the groups as they are.

A section's button says what is in it after the name, through the label's `Suffix` (written when the page is drawn
and when it changes, never per frame): "Garage   (9 options, on)". "on"/"off" is there only for a section
with a row that switches the whole feature, which is the toggle whose label key ends in `_enabled`; the game calls
a toggle's callback with the saved value when it draws the row and on every click, and that is where the button
hears of it.

The game's button row has two texts: the label beside the button, which `ModOptionsButtonControl.Initialize` fills
from the row, and one on the button itself, which it never fills, so the button shows what its template was saved
with ("Your text here"). `OptionsCaptions.cs` writes that second text. A row that draws nothing stands after the
mod's last row and goes once through the rows of this drawing: a section's button says "Open" or "Close" (and
"Found" while a search is on, when clicking it does nothing), "Open or close every section" says "Open all" or
"Close all", a row that changes a key says "Change", and every other button says "Run" unless the table
`CaptionByLabel` names another word for its label key ("Apply" for a preset, "Reset", "Export", "Import", "Check").
The text is the first `TMP_Text` under the row's `Button` that is not the label: if it has a
`TextLocalizationComponent` its `Key` is set, and if not the translated word is written and written again when the
language changes. If no such text is found the buttons are left as the game draws them and one line is logged.

A slider shows its number only if it was given a `valueLabelKey` (`ModOptionsSliderControl.Initialize` hides the
value otherwise; the text gets the number as `{value}`). Every `AddSlider` passes `SettingsTable.SliderValue`
("{value}") or `SliderPercent` ("{value}%"), and a slider re-made without one gets `SliderValue`. If the page cannot
be put in order it is registered as built, with the same numbers and words (`OptionsLayout.AsBuilt`).

### Search

The row under the page's first header is a text field (`OptionsSearch.cs`). The page has no text field of the
game's to copy, so it is a `TMP_InputField` built in code that takes its width from the header above it and its
height, font, letter size and button shape from the "Open the Guide" button below it.

- **Matching.** From two letters on. Not case-sensitive; the words typed may be in any order and each must be
  somewhere in the row's label, or in its section's text: the section's name, the feature's `About` sentence, and
  the other words for it in the `SearchWords` table. So a word that names a feature shows all of its rows, and a
  word only in a label shows that row ("bus" shows the whole of the transport section and its tests; "timetable"
  shows the rows that say timetable).
- **What is shown.** While a search is on, sections count as open, rows that do not match are switched off,
  a section's button is there only if one of its rows is, and the test tools are searched whether or not "Show
  test tools" is on or their section is open. The buttons at the top (guide, presets, export) are found by their
  labels; the headers between the parts are hidden. A line under the box says how many rows were found. Clicking a
  section's button does nothing meanwhile. Emptying the box (the x, Backspace, or Esc while typing) puts everything
  back as it was left: the search never writes what is open and closed.
- **Cost.** No `Update`. The field's value-changed event does the work: the lower-case texts are made on the first
  letters typed after each drawing of the page (the page is drawn again after a preset, and the language may have
  changed), a keystroke is one pass of `IndexOf` over them and `SetActive` only on the rows whose state changes,
  and the page lays itself out once. The scroll view is put to the top of the mod's part by a component that is
  enabled for two frames after a new search.
- **Keys.** While the field is selected `Keys.Suspended` is on (released on deselect and when the row is disabled
  or destroyed), and the game leaves its own shortcuts alone because a UI object is selected
  (`GameManager.HasInputSelected`). In the city Esc is the game's `CancelButtonHandler`: with the field selected it
  only takes the selection away, and the box is emptied in the same press; the next Esc closes the page. On the
  main menu `Options.Update` closes the page on Esc whatever is selected, so there one press does both.
- **Failing.** Anything thrown hides the box for the session, logs once ("Meta: the search box ... is hidden") and
  shows the page as the sections have it.
- **Not kept.** The box starts empty every time the page is drawn.

Other words for a feature are one line of `SearchWords` at the top of `OptionsSearch.cs`: who (the feature's id, or
the header key of its group while it is not registered; both may be given) and the words, lower case:

```csharp
new[] { "fuel reallife_fuel_header", "petrol gas station tank litre" },
```

Only words that mean the whole feature: each one shows every row of it.

The game's own help (F1) draws any page whose text is the locale key `help_<slug>_content`
(`HelpPageRenderer.RenderPage(slug)`), but lists only the pages of its `helpstructure.json`, and that key cannot
carry the mod's prefix; the guide is therefore a panel of the mod's own.

### Presets

Standard is always an option's default; the other three are declared with `Feature.Preset`. Today:

| option | Relaxed | Standard | Realistic | Hardcore |
|---|---|---|---|---|
| `crime_against_player` | off | on | on | on |
| `player_offences` | off | on | on | on |
| `crime_frequency` | 1 | 2 | 3 | 5 |
| `job_pay` | 125 | 100 | 100 | 80 |
| `life_frequency` | 4 | 5 | 6 | 8 |
| `police_presence` | 4 | 5 | 6 | 8 |
| `patrol_frequency` | 2 | 3 | 4 | 6 |
| `player_knockdown` | off | on | on | on |
| `ped_incidents` | off | on | on | on |
| `accident_frequency` | 1 | 2 | 3 | 4 |
| `crash_consequences` | off | on | on | on |
| `police_pursuit` | off | on | on | on |

A preset writes the PlayerPrefs keys the options page writes, calls `Settings.Reload()` (which is `Load` and then the
`Settings.Changed` event) and has the page drawn again. The settings as they were go to
`<persistentDataPath>/RealLifeOverhaul/settings_before_preset.txt` first; "Export" writes `settings.txt` there and
"Import" reads it (`id=value` lines, key bindings included).

### Save tools: what is removed, and how that was checked against the game

Read in the decompiled game: `GameInstance` (the save), `SaveGameManager.Save` (fires `GlobalEvents.onSaveGame`, then
serialises on the calling thread), `SaveGamePathHelper` (`SaveGames/<version>/<characterId>/<name>.hsg|.json`, `.meta`,
`.jpg`; the list of saves reads the files of each character folder and never a folder inside one), `Contact`
(`GameInstance.Contacts`; a contact's messages are its own `messagesQueue`), `HappinessHelper`.

| in the save | the mod's | prepare for removal | reset data |
|---|---|---|---|
| `modData` | keys starting `reallife:` (and any a feature declares) | removed | removed |
| `happinessModifiers` | types starting `reallife_happinessmodifier_` | removed (also stripped by `ModState` on every save) | removed |
| `Contacts` | ids starting `reallife-` | removed with their messages | kept |
| `notifications` | keys starting `reallife_` | removed | kept |
| `Transactions` | types starting `reallife_transaction_` | kept and counted | kept |
| `VehicleInstances` | the hired job van | taken back by Jobs' own clean-up; reported if still there | same |
| `PlayerDefaults.contactsLastName` | the game's note of the contact last opened | set to none if it names a removed contact (the Contacts app lists nothing otherwise) | untouched |

"Prepare" backs up first, twice (`RealLifeOverhaul_backup_<time>` inside the character folder, and
`<persistentDataPath>/RealLifeOverhaul/backups/<character>/<time>`, because the load menu deletes a character folder
whole with the character or its last save; either copy failing means no change), runs
every `Cleanup`, destroys the mod's root object and the objects features name (their `OnDestroy` restores what
they changed, as on unload), removes the above, attaches a handler at the end of `onSaveGame` that removes it again
inside the save event (so nothing a feature writes there reaches the file; the moods come out and the handler goes
on in the very frame the features are destroyed, before any autosave can run), saves under the same name and shows and
writes a report. It refuses in a vehicle and during a hospital trip, custody, sleep or an activity. There is no
"all saves" variant.


### The Guide and the welcome in the game's own interface

- The Guide is an app called Guide on the game's phone, built as the Planner is (`GameUi.PhoneApp.Install`, a panel
  from `GameUi.AppShell`, an icon from `GameUi.AppIconPainter`, one more tile on the phone's pages). Tab "Pages" lists
  the pages (the features, Presets, Known limits, Credits, What changed); a click opens tab "Reading", which has the
  page's text a line to a row in the game's table, headings in the game's yellow and bullets as "-". The left box says
  which page and has "All pages". The texts, the `{facts}` and `Changelog` are as they were (`Guide.Fill`, `Guide.Check`).
- Asked for from the options page, the contact's call or a click on the welcome notice: the app opens at once if no
  menu of the game's is up; under one a notification says it opens when the menu is closed, and it does (a quarter-second
  timer that lasts only while the request waits). In the main menu there is no phone: a notification says so.
- The welcome is the game's notification ("Real Life Overhaul 0.1.5: open the Guide on your phone to see what changed"),
  once per version; a click opens the Guide on "What changed". The four presets it used to offer are on the options page.
- Removed: the guide's keyboard search, the welcome panel, and their canvas panels. `MetaUi` stays for the options
  page's messages and questions (reset, import, preset questions, the set-up check): that page is also in the main menu,
  where there is no phone and no HUD, so `MetaUi.AnyOpen` stays and the camera still counts it.
- If the phone is not built as expected, the app is left off with one line in Player.log; the texts remain in the mod.
- Log lines: "Meta check (guide app): the Guide is on the phone: PASS", "opened on the list of 40 pages, 40 rows ...: PASS",
  "Meta check (welcome notice): ...: PASS".
- Not checked in the game: the app's look (row heights are a guess at the line length: a screenshot of a long page
  settles it), the icon, the notice's click, and the wait for the menus.
### Test paths

- Guide: options page "Open the Guide on your phone" (the app opens when the menu is closed); the phone contact "Real Life Overhaul Guide"; "Check the mod's set-up" renders every page and lists unfilled `{facts}` and missing texts.
- Welcome: "Show what changed" (opens the Guide on that page); or delete `m:<mod>:meta_seen_version` from PlayerPrefs.
- Presets: the four buttons, then the guide's Presets page (the "Now" column) and the header "Preset in use".
- Options page: open and close sections; "Show test tools" off hides the last section. Every button has a word on it ("Open", "Close", "Run", "Apply"), never "Your text here", and every slider its number beside it. Each section's label ends in its count ("(5 options, on)"); switch a feature's first toggle and the "on" becomes "off" at once. Open "Test tools": one closed group per feature; open one, close and reopen the page: it is still open.
- Search (main menu and in a city, both): with "Test tools" closed, click the box under "Real Life Overhaul: Start here" and type `bus`: the page jumps to the top, only the transport section and its group of tests are left, the line under the box counts them; type on to `bus stop`: the rows that say so are left, and pressing a button among them works. `stop bus` (two words, any order) finds them too. `zzzz` says "Nothing found for 'zzzz'". Try `police`, `fps`, `bike`. A toggle and a slider among the results still work. While typing `v`, `f`, `b`, `x`, the camera, interaction and garage keys must do nothing, and nor must the game's own (Space, M, Tab). Empty the box with the x: every section is as it was before. In the city, Esc while typing empties the box and leaves the page open; on the main menu it also closes the page. Switch "Show test tools" off and search `test`: the tests are still found. `Player.log` must have no "Meta: the search box" line.
- Save tools: the three buttons on a save you do not need; `Player.log` lines start `Meta:`; the report is also in `<persistentDataPath>/RealLifeOverhaul/`.
- Diagnostics: switch "Developer diagnostics" off: F8/F9 do nothing and only warnings and errors of the features reach the log.

When the version is bumped: `Changelog.Latest`, and the row `Changelog.Unnumbered` takes the new number.
## Civic life

`Scripts/Civic/`: a city government with elections, and a construction programme. Everything is data that
moves once per in-game hour; the street only shows what is near the player. Options group "Civic life":
a master switch, "Elections and the mayor" and "Construction sites", and eleven test buttons.

```
CivicDirector     the clock, the save (two lines in modData), the toggles, faults, key prompts, the test buttons
CivicCatalogue    the thirteen levers and every number a policy changes; name pools; election and site constants (game-free)
CivicState        GovState, SiteState, WorksState and their one-line text form (game-free)
CivicGovernment   mayor, council, campaign, polls, the count, policies phased in, approval, petitions, council votes, recall
CivicEffects      what policies do: multipliers the mod owns, labelled transactions, moods; all undone by Revert
CivicBridges      CivicPolicy (what other features read) and CivicBridges (what civic life asks of features not in this tree)
CivicCity         districts (the game's neighbourhoods), electorates, polling places, the player's buildings
CivicWorks        the construction programme as data: six sites, working hours, rain, complaints, inspections, handover
SiteDresser       the three sites nearest the player as props, a closed lane, one crew
RoadClosure       one lane closed for roadworks through the traffic system's switch (a small copy of SceneCordon's part)
CampaignWorld     posters, boards, the canvasser, rallies, the polling place's sign; VolunteerShift (the player canvassing)
CivicCast         civic life's own five people (a second instance of CityPeople), CivicCrew (walk in, work, walk off); CivicScreen: the game's panel and job panel for its prompts and shift figures
CivicProps        pooled props by id: the bundle's model if it has one, else a plain stand-in built to the same size
CityHallPhone     the City Hall contact (four topics), the ballot and the site notice in the game's conversation panel
```

**On screen (Civic).** The mod draws nothing of its own for civic life (`CivicHud` is gone; `CivicScreen` takes its place). The polling station, the campaign canvasser, the site notice and the canvassing shift's "offer a leaflet" are the game's panel over the player (`GameUi.DoorPanel`, one button the game labels with the player's key); the shift's figures (leaflets taken, seconds left) are the headline and first line of a copy of the game's job panel (`ShiftCard`), or a notification each time they change if that copy cannot be made. The council's meetings and ballots were already the game's phone dialogue (`CityHallPhone`). Cost: nothing per frame; the prompt is offered once a second and told to the panel only when it changed. Log lines with diagnostics on: `Civic check (prompt panel)`, `Civic check (shift card)`. Not seen in the game.

### What the game lets a mod change, and what is done instead

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

Nothing the game saves is written by a policy. `CivicEffects.Revert` (option off, city unload, fault) puts
back every value it changed.

### The policy catalogue

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

Money by the week is one settlement every seven days, a transaction per line; the phone shows the account
as it stands. Every amount above is at the start's prices and is charged and shown at today's (`CivicCatalogue.*Now`, `Economy/Prices`): the levies, grants and licence fee, the ticket and fare changes, the floor, donations, the fast-track threshold and the refit bids. The `_fx` strings take them as {levy}, {grant}, {ticket}, {fare} and {floor} (`CivicText.Effect`).

### Elections

A mayor and a council of five. An election every 8 weeks (56 days); the first 14 days after the mod first
sees a save. Three or four candidates drawn from pools (20 x 20 names, 8 invented parties, 18 slogans), each
leading on one of six issues with a platform of three or four policies on different levers. The campaign is
the 7 days before: polls move a third of the way each day towards where the city stands (platform fit weighted
by what matters now: crime scenes and accidents in this save, dirty streets, rent rules; a sitting mayor carries
their approval), plus the player's part: donations (500 a time, 2,500 at most; if that candidate wins, a
fast-tracked permit per 1,000, two at most, with one chance in four of a "pay to play" story when called in),
one endorsement (worth more with every business they run; a scandal with a criminal record, through the Law
bridge), an afternoon's canvassing, standing through a rally. The debate is 3 days before at 19:00. On the day
the polls are open 07:00 to 20:00 at one designated building per district (a school if there is one, else a
bank, an agency, a clinic, else the first building by address); the count is at 21:00, by district, from an
electorate derived from the district's floor space (the game keeps no population). The winner takes office the
next morning and their policies come in one a day at 09:00, each with a notice. Between elections: approval
follows crime, accidents, late sites and promises kept; petitions (2 % of the electorate in 14 days force a
council vote; the city starts some, the player can start or sign one); a council vote on one policy about every
12 days; a recall election after 7 days under 25 %.

Standing for a council seat is not implemented.

### Construction sites

Six sites in progress at once, two started within 60 to 320 m of wherever the player was that day. Working
hours needed: roadworks 50, scaffolding 120, refit 90, utility 30 (x0.8 to 1.3); ten a weekday, 07:00 to 17:00,
as the city came. Five stages each. Rain stops outdoor work for the hour. Thirty in a hundred are left unsafe
for a stretch (missing cones or netting, a barrier flat in the road): reported, they stop for a day.
The game's city has no empty lots and nothing is ever built or demolished, so there is no new building:
the refit of an empty retail unit (hoarding, "opening soon", hoarding down) takes its place.

Placement and budgets are at the top of `SiteDresser`. Nothing is cut out of the navigation mesh. A lane is
closed only by `RoadClosure`, only for a stretch with no junction, and only if every way into it can be shut
where traffic has another way to go; it is opened on every path out.

### For other features

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
```

### Wish-list (assets)

Each is drawn today as a plain stand-in of the same size and pivot, built in `CivicProps.CivicStandIns`. A
model added to the prop bundle under the same id is used with no code change.

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

`tower_crane`, `structure_floor` and `structure_cladding` are not wanted: the city has no empty lot to build on.

## Wiring: how the features meet (`Scripts/Wiring`)

Every feature works alone and talks to the others only through optional bridges with a default. `Scripts/Wiring` is
the one folder that fills those bridges in: a `partial static class Wiring` with one nested class per pairing, one
small file each. `Wiring.Connect(logger, root)` runs once per city load after every director has been added
(`RealLifeOverhaulCity.OnLoadAsync`), `Wiring.Disconnect()` on unload puts every bridge back to its default. Nothing in
the folder has an `Update`: an adapter runs when the feature that owns the bridge asks. An adapter never throws into
its caller: a fault is logged once per place (`Wiring.Fault`) and the default answer is given.

| file | what it connects |
|---|---|
| `Wiring.cs` | `Connect` / `Disconnect`, the list of pairings, `Fault` |
| `Standing.cs` | `Wiring.Standing`: the one answer to "has the player a record" (`HasRecord`, `Arrested(days)`, `Convictions`, `Charged`) and to what the city thinks of them (`Public`, `PublicSigned`), from Law's police record and Justice's convictions together |
| `LawJustice.cs` | `Wiring.Arrests`: Law's cell and Justice's case for the same arrest; `Justice.Bridges.Law` |
| `LicenceLinks.cs` | `Licences.Law`, `Justice.Bridges.Licences`, and what Licences reports to the courts instead of fining |
| `Scenes.cs` | `Wiring.Scenes`: the one question every street scene asks before it starts; `PlayerHeld` for panels |
| `CivicLinks.cs` | `CivicBridges` (record, reputation, scenes, weather, news, city people); `Wiring.Paper.Print`: a finished story for the City Ledger |
| `LifeLinks.cs` | `SocialLinks`, `CareerLinks`, `TravelBridges`, `Justice.Bridges.Social`, `GarageBridges.RoadIsWet` |
| `EconomyLinks.cs` | `Economy.EconomyBridges` (the record the credit rating reads, the fines and court debts the Finance app lists), the Finance rows of the Planner, and the banks' own terms put back on unload |

### Who does what at an arrest

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

- **Custody twice** (the precinct's hours, then the court's custody for the same thing): the cell is time served
  (`CourtHearing.Sentence`, one line on the sentence page).
- **Bail twice** (the precinct's, then the court's): the precinct's is the case's (`Justice.ReleasedByPolice`).
- **The wrong hours** told to the court (`Bust` passed the old roadside custody's 2 to 5 h even when a precinct held
  the player for 4 to 48; `Catch` passed 0): `Bust` passes 0 when a precinct will hold them, and `Wiring.Arrests`
  adds the precinct's own figure.
- **Driving while suspended fined twice** (1 500 at the window by Licences, and the same charge in court by
  `Justice.PoliceContact`): what was paid at the window is credited to that case (`LicenceLinks.SuspendedAtStop`).
- **An arrest by Licences** (third time without a licence, second time suspended): served through the courts' custody
  when they are on (`Justice.Hold`: in the nearest precinct's cell like every hold of the courts', counted as time
  served), through the old roadside time skip when they are off. Never both.
- A death on the way to a ticket (`PoliceAttention.Ticket`) arrested but never charged: it now opens the same case a
  death after a pursuit does.
- With a precinct holding the player, nobody told the paper or the player's friends (`News`, `SocialHooks.PlayerArrested`
  were only in the old roadside custody): `Wiring.Arrests` tells them.

Panels queue, they never stack: the precinct's cell runs behind the game's time skip, and everything else waits for
`LifeEvents.IsSafeForEvent` (no dialog, no time skip, not paused: the courts' panel pauses the game, so nothing starts
behind it). The courts' own panel also waits for `Wiring.Scenes.PlayerHeld` (a test paper or driven test, a panel of the mod's own that needs the cursor).

**The cell is Law's for the courts too.** `Wiring.Arrests.LawBridge.Custody` hands a court's hours (remand, a custody
sentence, an arrest by Licences) to `Law.Custody.Hold(hours, reasonKey, 0, released)`: no bail, because a court that
holds somebody has refused it or the player chose to stay. `Law.Bridges.CourtOn` is `Justice.Active`, so a hold ends
when the courts are switched off. `Holding` answers from `Custody.Active` and the saved stay.

**For Scripts/Law (two calls it would need for more):** `Record.Convict(day, name, caseNumber)` and
`Record.Remove(caseNumber)` so that a conviction and an acquittal can be lines of the police record (today
`Convicted` / `Cleared` / `RecordSealed` do nothing there: a second line for the conviction would move the reputation
twice for one offence, and there is no way to take one line off). The custody path does not need them.

### One street scene at a time (`Wiring.Scenes`)

Every large scene is staged around the player, so two of them would always stand beside each other. Each director
keeps its own flag; `Wiring.Scenes` puts them side by side and every start test asks the one question:

| scene (`Wiring.Scene`) | its flag | its start test asks |
|---|---|---|
| `Accident` | `TrafficAccidents.IsActive` | `!Scenes.Near(player, Scenes.Beside, Scene.Accident)` (also: no building site within 90 m) |
| `Crime` | `CrimeDirector.IsActive` | `!Scenes.AnyBut(Scene.Crime)` in `CrimeDirector.CanStage` |
| `Event` (fire, street event) | `Events.EventDirector.IsActive` | `!Scenes.AnyBut(Scene.Event)` in `EventDirector.CanStage` |
| `Rally` | `CivicDirector.RallyOn` | `CivicBridges.LargeScene = () => Scenes.AnyBut(Scene.Rally)`: rallies and site crews (`CampaignWorld`, `SiteDresser`) |
| a pedestrian hit by traffic | part of `Accident` once it has happened | `!Scenes.AnyLargeSceneActive` in `PedestrianIncidents` |
| the routine roadside check | `PoliceAttention.IsBusy` | `!Scenes.AnyLargeSceneActive` in `DrivingEnforcement.Wanted` |

A building site stands for days where it stands, so it is asked about by place (`CivicDirector.SiteNear`). A scene
added later is a value in `Wiring.Scene`, a line in `Scenes.Active`, and `AnyBut(its own value)` in its start test.
`Scenes.PlayerHeld` is the same idea for panels (see above).

**Road closures have one owner per road point.** Two things switch `temporaryDisabled`: `SceneCordon` (accidents,
and the Events director's own instance) and `Civic/RoadClosure`. Each shuts only points that are open
when it looks, remembers exactly those, and opens exactly those again; none opens a point it did not shut. With one
scene at a time and no accident or event beside a building site, two owners can no longer want the same stretch. The
one theoretical overlap left is `SceneCordon.Recheck`, which looks at its own list once more on the tick after a scene
has cleared: a roadworks crew would have to close one of those very points in that tick, and its start test
(`CivicBridges.SceneOn`) is false only from the tick after.

### Every bridge, and who answers it

| bridge | answered by | with |
|---|---|---|
| `Justice.Bridges.Law` | `Wiring.Arrests` | `ReleaseSpot`: the pavement outside the nearest precinct, and its name. `PriorConvictions` -1, `Custody` false, the three record calls empty (see "For Scripts/Law") |
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
| the Planner's Finance rows | `Wiring.EconomyLinks` (`PlannerDirector.AddSource`) | tomorrow's payments over every credit account, a missed payment, a change of grade: read from `CreditLedger` |
| `News` (the City Ledger) | `Wiring.Paper.Print(key)` | a finished story: verdicts (`JusticeDirector`), fires (`EventDirector.Begin`), election results and scandals (civic life) |

**Drink at the wheel** is now an offence of the police's own (`Offence.DrunkDriving`, `CrimeLaw.DrunkDrivingFine` 900):
`PoliceAttention.Report(Offence.DrunkDriving)` is called by `LeisureDirector.Wander` while the player drives tipsy
(one look every 25 s at most, seen with the odds of driving on the pavement), and at any stop the officer at the
window books it with whatever the stop was for (`Breathalyse`). Licences adds 6 points (`PointCode.DrunkDriving`); the
second time inside the three weeks points count for it is a case as well (`JusticeLaw.Reckless`, the roadside fine
credited), unless the player was caught after running, which is before the court already.

**Clothes prices after wardrobe batch two.** The shop priced by text in the id; the catalogue now has garments the
text did not know (chinos, suit trousers, pencil skirt, evening dress, boots, loafers) and a tier of its own.
`ClothesShop.Tier` now reads `CharacterCatalogue.Garments` (shoes: derby Premium, runner, boot and loafer Smart, court
Everyday), so the tier on the rack is the tier in the label. The evening dress was caught by the summer dress's
`dress_` price (120); it now goes by its tier (Premium, 200). The seven first prices are unchanged.

### On screen, everywhere: no canvas of the mod's but the options page

The mod's shared layer (`Core/Hud`, `Jobs/JobHud` and each feature's own copy) is gone. What replaced it, and what is left:

| was | now |
|---|---|
| the crosshair dot | `Camera/CrosshairDot`: one `Image`, a child of the game's own HUD canvas, made the first time mouse look is on |
| the incident marker with its distance (street crime, events, accidents) | each feature's own `GameUi.GuideMark` (the game's destination marker). Only one feature can have the marker at a time: the one that asked first keeps it, the others ask again on their next tick |
| the speed readout | `Driving/SpeedReading`: one more reading in the game's vehicle panel (a copy of its condition reading, after it, as `CarDash` does), written when the whole number changes. The game's panel has no speed of its own |
| the font asset others read | `GameUi.GameFont.Asset` (the font of the first text the game's interface has, looked up once). `Core/Hud.cs` is now only a four-line class that forwards `Hud.Font` to it, because `Scripts/Transport` (not touched in this piece of work) still reads `Hud.Font`; delete the file when that reads `GameFont` |
| the job panel's plate, marker and prompt | `ShiftCard`, `WorldPanel` and `GuideMark` |

Canvases the mod still makes: `Meta/MetaUi` (the options page's own messages, also used in the main menu where there is no HUD). `ShiftCard` and `NavHud` each add one `Canvas` component to a copy of a game panel so that it batches by itself; that is not a canvas of the mod's own. None of this has been seen in the game.

### What the merged whole costs per frame

Read through after the merge: 67 `Update` / `LateUpdate` / `FixedUpdate` methods, no `OnGUI`, no `InvokeRepeating`, and no
`Find*` or allocation on any idle per-frame path. "Idle" is in the city with no scene and no panel, walking or driving.

| component | idle, per frame | changed here |
|---|---|---|
| the thirteen feature directors (Jobs, Home, Transport x2, Leisure, Events, Social, Travel, Business, Justice, Licences, Civic, Garage) and `LifeEvents` | a timer comparison and two to eight flag tests; their work is on a 1 s (some 0.25 s) tick | **before: each took itself off the game's save event and put itself back every 1 to 2 s (a dozen features, two to four new delegates each, every second). After: one relay for the whole mod (`Core/SaveHook`), re-hooked only when the game's event is not the one last seen: nothing allocated while idle.** Home's game-event hook is made once |
| `ThirdPersonCamera.Update` | the heaviest always-on method: both of the game's cameras are written every frame | **before: a sphere cast every frame for the indoor camera even out of doors. After: only while that camera is live** (`CinemachineCore.IsLive`) |
| `ChaseCamera.LateUpdate` (driving) | pose of the chase camera | **before: the lens written every frame. After: only when the field-of-view option has changed** |
| `CityPeople.FixedUpdate` (people out, driving) | every other step: the car against eight people | **before: `GetComponent<Rigidbody>` twice a second. After: when the car changes** |
| `CrimeDirector`, `EventDirector`, `BusinessDirector`, `SocialDirector`, `CarOwnership`, `LeisureSound`, `PoliceAttention`, `PolicePursuit`, `BeatPatrol`, `PolicePresence`, `PedestrianIncidents`, `PlayerKnockdown`, `Law.LawDirector` | one to three comparisons | nothing to do per frame. **On their timers, found later: `Law.LawDirector` (the save and the two building events, every three seconds), `HomeDirector` (the game's event, every second), `Family.FamilyDirector` (the save, every two seconds) and `MapPlaces` (the map event, every three seconds) still took themselves off the game's events and put themselves back. Now `SaveHook.Keep` and `EventHold.Keep`: nothing allocated while the game's event has not changed** |
| `PerfDirector` and the tweaks that are on by default | one time comparison; the tweaks tick every 2 to 5 s | **before: every scene loaded or unloaded made `RefractionOff` look through every material in memory in that frame (`Resources.FindObjectsOfTypeAll<Material>`, seven engine calls a material) and, on the next tick, `StaleLampLights` through all 3,038 street lamps (three reflected fields each). After: unloads are ignored, a load asks for one look four seconds after the last scene arrived, and both looks go through their list 300 and 250 at a time, 20 times a second. The first look after the city loads is sliced the same way; refraction stays as the game has it until that look is finished** |
| `TransitDirector` (4 times a second) | the stops in reach, the bus | **before: every second the label of each dressed stop was put together for a self-check that had already been made; and the self-check itself (a dozen questions to the navigation mesh, once per dressing) ran with diagnostics off, when nobody reads its line. After: neither** |
| `MetaUi`, `CuffPose`, `HoseHold`, `CarMeasure`, `CarDrag`, `ActivityAnimators`, `RagdollSystem`, `Stretcher`, `EmergencyDriver`, `BikeMount`, `TipsyDriving`, `PerfSampler`, `PerfLoop` | disabled while idle | nothing to do |
| `CityPeople.Update`, `ActorPool.Update` | off with nobody out; with people out, a distance and a dot product each to hide those out of view | left: it is what keeps unseen characters from being drawn |
| `PlayerDouble.LateUpdate` | off by default; on: every bone copied | left: an option that says what it costs |
| `CarStrikes.FixedUpdate` (driving above 5 km/h) | every other step: the car against walkers, standing figures and the mod's people | left: it is the hit test itself |
| `NearbyInteraction.Update` | the option, thirteen `TakesInteractKey` flags, two key tests | left for whoever next touches that line: test the two keys first and the ten flags only on a press |
| `TrafficAccidents.Update` -> `TrafficWatch` | 5 times a second: every pooled traffic car's body looked at | left: the bounce watchdog's whole purpose; it is the largest timed cost while idle |
| `Wiring` | nothing: no component, no tick | adapters run when asked; `Wiring.Scenes` is four flag reads on a director's own roll timer |
| `HomeDirector`, `CarOwnership`, `GarageDirector` (their interface moved to the game's own after this read-through) | a time comparison each; the Garage also compares what is ridden (`TwoWheelers.Frame`, one reference) | **before: a key test per frame while a prompt was up (Home, car ownership, Garage), the run's marker and the two-wheeler's marker drawn per frame, the custom shop's money text written four times a second. After: no key is read (the game runs its panels' buttons), the markers are the game's guider, set when they change, and a text is written only when what it says has changed.** The needs bars are three `fillAmount` writes a second at most, each only when its whole number changed; the bay panel's line and button are built when the game shows or refreshes its panel |
| `Economy.EconomyDirector` (added after this read-through) | no `Update`: a coroutine that waits one second on the real clock, then four flag tests, three handlers held (`SaveHook.Keep`, `EventHold.Keep`) and, only while nobody lends, one static field read | see "Economy": its work is on the game's hour and day events (a handful of loans and banks compared; the score worked out when something changed) and when the app is opened |
| `Nav.NavDirector` (added after this read-through) | a time comparison and two flag tests; while the minimap shows, the player's place and heading compared with what was last written, and written (one quad's texture window, one position, up to 34 rotations) only when they differ | see "Minimap and GPS": its work is on a 0.25 s tick, and a route search takes 0.4 ms of a frame while one is under way |

Not shared, on purpose: every director reads the player, the hour and "in a vehicle" for itself, but on its own slow
tick, so a common ticker would save a few property reads a second and couple thirteen features to one clock.

## Keys

Every key the mod acts on is an action in `Core/Keys.cs` (`KeyAction`): an id, a default key and an optional
Shift/Ctrl/Alt. Saved bindings are read at game start from PlayerPrefs (`m:<mod id>:key_<action id>`), and
changed under "Controls" on the options page: a button per action that takes the next key pressed (hold a
modifier with it for a combination; Esc cancels; the left mouse button and gamepads are not offered), and a
reset button. A key the game itself has bound is allowed and named beside the button (read from the game's
live bindings, its defaults if those cannot be read).

| action | default | where it is used |
|---|---|---|
| `CameraToggle` | V | `Camera/ThirdPersonCamera` |
| `Interact` | F | `World/NearbyInteraction`, `Jobs/JobDirector`, `Crime/CrimeDirector`, `CityLife/CityLifeDirector`, `Civic/CivicDirector` (vote, take a leaflet, read a site notice, offer a leaflet) |
| `InteractAlt` | E | the same, and only while the game shows no interaction of its own |
| `FreeCursor` | Left Alt | `Camera/ThirdPersonCamera` |
| `LookBehind` | Right mouse (held) | `Camera/ThirdPersonCamera` |
| `GentleThrottle` | G (held) | `Driving/CarTuning`: the pedal goes no further than half |
| `PerfProbe`, `PerfBench` | F7, Shift+F7 | `Performance/PerfDirector` |
| `PhoneDump`, `CharacterReport` | F8, F9 | the two development aids |
| `PhotoMode` | F10 | `Leisure/Photo/PhotoMode`: into and out of photo mode (its other keys are fixed: `PhotoKeys`) |

## Family

A partner who moves in, an engagement and a wedding, children, the wider family, and the hard times
(`Scripts/Family`). Everything is optional: the group "Family" on the options page has a switch for the
whole of it and one each for the partner being shown at home, engagement and wedding, children,
relatives, and hard times (chores, arguments, separation, divorce: off, and none of them ever happen).
Nothing depends on who anybody is: the partner is whoever the player is seeing, in any pairing, and the
default introduction follows the "introduces you to" choice (anyone / women / men).

**What family life puts on screen.** Everything is the game's own interface; the mod draws nothing of its own. A prompt
("buy a pushchair", "talk to Sam", "go in to the scan") is the game's panel over a place (`GameUi.DoorPanel`, a `WorldPanel`
with one button): over the partner when the prompt is about them (`Follow`), otherwise over the player. The first line says
what it is, the button says the verb (`reallife_family_button_*`, one for each prompt key) and the game writes the player's key
after it and runs it on that key or a click. The objective (where to be and until when, the pushchair walk, the aisle) is
the headline and first line of a copy of the game's job panel at the top right (`ShiftCard`); one notification stands in if
that copy cannot be made. The place markers were already the game's own destination marker (`FamilyPlaces.Guide`) and are
unchanged. Cost: nothing per frame; the director's one-second tick (five a second in a scene) tells the panel only what changed,
and the texts are rebuilt only when the prompt or the objective changes. A fault in the button's action switches family life
off for the session with one log line, as before. If the game's panel is showing another place the prompt waits and is tried
again on the next tick; there is no other fallback for a prompt, because the same errands are listed on the Family page and the
objective still shows. Log lines with diagnostics on: `Family check (prompt panel): <prompt key>: the game's own panel is up: PASS`
and `Family check (objective card): ... PASS`. None of this has been seen in the game.

State is one JSON string in the save's modData (`reallife:family`, `FamilyState`); the moods are happiness
modifiers with the mod's prefix, so `Core/ModState` takes them out of the save like the rest. A save made
with the mod loads without it. What the save proper does keep is what the mod's other contacts already
keep: three phone contacts (Family, Someone special, The in-laws) and transaction lines.

### What can be shown, and what is state

**Children are never shown as walking characters.** The game's people are adults: `AppearanceAgeHelper`
clamps age to 18..80 and age only fades the wrinkles of the head material in (`_Fade`, 18 to 60); there is
no height or proportion value in `CharacterData` (gender, colour, strength, fatness, age, elements), no child
mesh under either gender model, and the only way to make somebody small is to scale the whole rig, which
gives a small adult (adult head-to-body proportions, adult face, adult clothes) and looks wrong. So:

- a baby or toddler is a **pushchair** on a walk (the modelled prop, pushed by the player with `push_cart`
  on the arms over the game's own walk), a **cot** and a **high chair** standing at home (modelled; clean
  plain-shape stand-ins without the bundle), with the swaddled `baby_bundle` lying in the modelled cot while a
  child is under one (`FamilyProps.Tuck`: a piece of the cot object, pooled with it), and a **sound** now and
  then as a notice;
- a school-age child is a name and an age on the Family page, school reports, drawings on the fridge
  (messages), a school play at a real theatre and a parents' evening at a real school to go to, birthdays;
- no scaled-down adult is used anywhere.

**The partner is shown** at home (while the player is in that home and the timetable says they are in) and
in three scenes: the proposal, the wedding, the pushchair walk. They stand and walk; they never sit or lie:
the game's chairs are driven by its customers' behaviour trees and its beds by its own sleep activity,
neither of which one of our characters can use reliably, so "asleep" and "in bed" are state and a notice.

The family's own pool is **three characters** (`FamilyCast`: the partner, and for a wedding the officiant
and one guest of the family's). A wedding borrows up to four more from the living city's pool when it has
them free, as guests in the clothes they stand in. Everybody is the living city's kind of person
(`CityPerson`): moved only by the navigation agent, standing only on spots found and tested for room
(`CitySpots` out of doors; `FamilyPlaces.Floor` on the home's own navigation mesh, with room to the nearest
edge and a complete path from the player). Out of doors people walk in from out of view and are taken away
out of view; a wedding is set out and struck under a cut to black. At home they are there when the player
walks in, or come in at the door when it is not being looked at.

### Living together

- **Moving in** needs the two of you close enough for long enough (default bridge: affection 60 and seven
  days together; Social: its own "partner" stage) and a home of the player's own (a rented residential
  building) with floor for the household: 30 m2 for two and 15 more per child (`Building.totalSqm`; a home
  whose size the game does not give counts as big enough).
- **Timetable** by what the partner does for a living (`HomeLife.Where`): office hours for most, earlies
  and lates for a nurse or a paramedic, evenings for a chef, mornings for a barista; an evening out of
  their own about one day in five; asleep 23:00 to 06:00; at home for three days after a child arrives;
  short days if they are the one looking after small children.
- **At home**: they stand by what the game calls a cooker, fridge, coffee machine (a cup held, a sip now
  and then), a sofa, television, armchair or bookshelf (reading the paper, or watching), or failing all
  furniture on clear floor; every minute or so they walk to another of those places. With a baby under a year old they hold it (`hold_baby`) at some of them, and a place beside the cot, where they rock it (`rock_cot`), is one of the places. Up to two of the places
  are beside something to sit on, and there they sit down (see "Sitting" below). They wave and greet
  the player at the door (lines by the hour and by how the player is), and within reach, facing them, the
  interaction key opens a conversation: both stand, face each other and animate (`talk_a` / `talk_b`).
- **Conversation**: a chat about what has really happened (the player ill, a shift worked today and what
  it paid, the baby, rain, a low mood, the wedding or the birth being days away), cooking together and an
  evening in front of the television (Home bridge; default a few lines, a meal and a mood), an evening out.
- **Money**: every evening at 18:00 their share comes in as its own income line: the smaller of half the
  day's rent and 35 % of their day's pay, never more than 600 (in a home with no rent: a fifth of their
  pay, at most 60). Halved while they work short days for the children.
- **Chores** (about three days in ten, a question with two answers), **neglect** (four days with not a
  word and the home cools, with a text every third day), **broken promises** (standing them up, missing an
  appointment), and **arrests** (`FamilyApi.PlayerArrested`, told by `Wiring.Arrests.FamilyHears` once as it happens; only with a partner) all move
  "harmony", the one figure for how things are at home (0..100, shown on the Family page in words).
- **Support**: ill and at home in the evening, they look after the player (Home bridge; default eight
  hours off the illness and a mood); after a bad day (happiness under 35) they say something that helps.
- **Evenings out**: Leisure bridge, else Social's outing, else dinner at the nearest real restaurant: the
  game's destination marker, a key prompt at its door, the bill (90), two hours skipped, and a line.
  Not turning up is noticed.

### Sitting (`People/Seating.cs`): the partner at home, a bench on a walk

**What the game has to sit on, and how it seats its own people.** Looked for in the decompiled game:

- every chair, armchair, sofa, stool and indoor bench a player can place is a `Controllers.SeatController`
  (an `ItemController`), with one or more sitting positions (`SittingPosition`: one that is free, or null) and its own
  record of which are taken (`Occupied`, `OnSittingChanged`);
- the city's street benches are `OutsideBenchController`, each with `sittingPosition` (the player's) and
  `aiSittingPosition` (where the game itself seats a pedestrian while the bench is near, `_npc`);
- a person is seated by `ThirdPersonCharacter.SitOnChair(seat)` (the game's own sitting pose, the navigation agent
  off, the body on the seat's position and turned as it is, the seat told it is taken) and taken off it by
  `ThirdPersonCharacter.Reset()`. Its customers, its employees and the player in its own activities all go through
  those two.
- beds are the game's sleep activity's and are not seats: nobody of the mod's lies down.

So nobody of the mod's is posed on a seat by the mod. `Seating` uses the game's two calls, and adds what they leave
open: where the person stood (they stand up there, on the mesh, and not wherever the agent lands), and what "free" is.

```csharp
bool   CityPerson.Sit(Transform seat)     // anybody from a cast: the family's partner, a companion, a city person
void   CityPerson.Rise()                  // safe at any time; also done by WalkTo, Work, Reset and Surrender
bool   CityPerson.Seated
Seating.IsSeat(item)                      // an ItemController that is a SeatController
Seating.Free(item)                        // a sitting position of it with nobody on ANY of its places, or null
Seating.InReach(from, seat)               // within 2.4 m: near enough to sit down without being seen to jump
Seating.Bench(near, within)               // the nearest OutsideBenchController (one physics query: on a slow timer only)
Seating.SecondPlaceFree(bench)            // its pedestrian's place is empty (the game's own field, read when asked)
new PlayerSeat().Sit(player, seat) / .Stand() / .Seated   // the player, held by the game's own RestActivity blocker
```

Rules kept: a whole-body clip is never played on somebody seated (`CityPerson.Work` stands them up first), nobody
walks off a seat with the agent off (`WalkTo` stands them up first), and a person given back to a pool or knocked
down is stood up first (`Reset`, `Surrender`), so every existing way out of every scene already gets people off seats.

**The partner at home** (`HomeLife`). `Scan` makes up to two of the partner's six places "beside a seat": any
`SeatController` in the home with a clear piece of floor by it that is not in the doorway (`Usable`: 1.6 m from the
entry, 1.5 m from the other places, on the same mesh). When they have walked to such a place (`Settle`) they sit down
if `Seating.Free` gives a position (so never on a seat the player, or anybody, is on) within reach, for 70 to 160
seconds (`Mode.Sitting`); otherwise they stand there as in the living room. They get up (`GetUp`: back where they
stood) when that time is over and the player is not right beside them, when it is time to leave or to go to bed,
when the seat is switched off or taken away, when a scene takes them over (`Hand`), and when the player presses Talk:
the panel is offered over them seated, and they stand up to talk. While seated they have the game's sitting pose and
nothing in their hands.

**A bench on a walk** (`OutingScene`). On a walk, every second and a half while both are on foot and within 6 m of
each other, `Seating.Bench` looks 4.5 m round the player for a street bench; with one in reach the game's panel
offers "Sit down" (`reallife_social_button_sit`), once a walk. On the button: the player is seated on the bench's own
place (`PlayerSeat`); the companion on its second place if the game has no pedestrian there and it is in reach,
otherwise they stand by, facing; the bench's `spawnPedestrians` is switched off meanwhile and put back, so the game
seats nobody on top of them. After four seconds the companion says something and the panel offers "Get up"; after 90
seconds they get up by themselves. A rest counts as 40 m of the walk's 160. They are got up at once by anything that
wants the player (`Wiring.Scenes.PlayerHeld`: an arrest, a panel of the mod's; the hospital), when the scene is cut
(a time skip, a door) and when it ends in any way (`LeaveBench` in `Cut` and `End`).

Outings that go indoors are as they were: the door, the fade, the time passing. Nobody sits at a table, and the
Guide says so.

Cost: nothing per frame. The home is scanned once on entering, as before; the partner's tick is the family's
once-a-second one; the bench query is one `OverlapSphereNonAlloc` every 1.5 s of a walk and nothing otherwise.

Log lines with diagnostics on: `People check (seat): this home has N place(s) to sit among M ...`, `... the partner
sat down on <item>, 0.00 m from where they stood, for N s: PASS`, `... the partner stays standing by <item> (in use, or
out of reach)`, `... the partner got up after N s (<why>): PASS`, `... a street bench is N m from the player on a walk:
sitting down is offered`, `... the player sat down on a street bench; the companion sat down beside them`, `... up from
the street bench (<why>): the player is free to walk: PASS`.

Not checked in the game: any of it. To look at first: that one of the mod's characters takes the game's sitting pose
at all (they are the game's own character type, but from the mod's pool) and sits at the right height on each kind of
seat; that the player seated outside the game's own activity keeps the camera and can click the panel's button; that
standing up puts both back on the pavement; whether the game's bench panel ("rest") still opens on a click while the
player sits there through the mod. A screenshot of the partner on a sofa and one of the two on a bench settle the look.

### Engagement and wedding

- **The ring** is bought inside a real jeweller (`ba:businesstype_jewelrystore`): a key prompt opens the
  counter conversation with four rings (1,200 / 4,500 / 12,000 / 30,000).
- **The proposal** is wherever the player chooses: "ask them to come" from the Family contact brings the
  partner walking in from out of view to the player; at home they are already there. The player goes down
  on one knee when they choose to: `propose_kneel_down` once (2.8 s, held on its last pose), then the
  `propose_kneel` loop the moment that is over (`JobBody.Chain`, stepped by the director's tick of the body),
  with `ring_box` open on the right palm at the clips' grip from the first frame; the question is asked when
  the box has come up (the clip's `present` mark). After the answer they get up with `propose_kneel_up`,
  the box still in the hand, and it is put away when they stand. (A bundle without the way down fades into
  the loop from standing; one without the loop uses the mod's `kneel_work`, empty-handed.) The partner steps
  round to 1.25 m in front of them first (where the clips look for a face) and has stopped before the knee
  goes down, so the two face each other; their answer is `propose_react_yes` or `propose_react_unsure`. The answer is theirs:
  base odds from how close the two are and how things are at home, plus the ring (up to +0.22), plus the
  moment: an evening +0.08, a weekend +0.04, a dry sky out of doors +0.04 (rain -0.08), the end of a night
  out the same day +0.10, the player dressed in clothes the game calls formal +0.05, the small hours
  -0.15, an argument in the last two days -0.20. Too soon (under 14 days, or not close enough) is always
  "not yet". A "not yet" says what would have helped and costs nothing but a week before asking again.
- **Planning** at a real event planning agency's counter (they add 15 %, send the invitations and order
  the flowers) or by the two of you from the phone (the invitations are then yours to send from the Family
  contact, the flowers yours to order inside a real florist). Venue: a registry wedding at the city offices
  (the tax office stands in for a town hall, which the city does not have) 500, an open-air wedding on the
  spot where the player stands when they choose it 1,500, a restaurant 6,000, a theatre 12,000. Size:
  simple x1, classic x1.8, grand x3.2. Flowers 800, music 1,200, catering 85 a guest. A day 7, 14 or 21
  days ahead. A quarter is paid as a deposit, the rest on the day.
- **Guests**: Social's close people, or by default Mom, the old friend (Alex) and the in-laws, plus a crowd
  by the size (4 / 14 / 30). Without invitations only the family comes.
- **Outfits**: the partner, the officiant and the family's guest wear what the game tags Formal (their own
  face and hair, from the same seed as every day). The mod's wardrobe (`Character/CharacterCatalogue`) has
  no formal wear yet; the player wears what they choose, and being dressed formally counts at the proposal.
- **The day** (11:00 to 20:00): the destination marker, and at the venue a key prompt. Under a cut to black
  the ceremony is set out on the pavement outside the venue's doors (every place tested for room; if the
  essentials do not fit it says so, and after two such failures the next press holds a small ceremony told
  rather than shown): officiant at the front, guests in rows, and by what was paid for an arch (classic and up), flower
  stands (flowers), a runner down the aisle (grand). The partner walks up the aisle; so does the player;
  vows with a choice of two, their vow, rings (`ring_present` on the player and `ring_receive` on the partner,
  started together: they are made for a pair facing 1.10 m apart, so during the officiant's welcome the partner
  takes the step that makes it so, `CityPerson.StepTo`; if the two do not end 0.98 to 1.22 m apart the player's
  half plays alone, as before), the pronouncement, applause (the officiant and the family's guest clap, the
  borrowed guests clap and `cheer_loop` by turns, each starting a little later than the last; the partner's
  `cheer`), a photograph (the HUD hidden
  for two frames; saved under `RealLifeOverhaul_shots` in the game's data folder), the first dance (`slow_dance_a` on the
  player and `slow_dance_b` on the partner, 0.45 m apart and facing, started together for 8 s; the partner takes the step in
  and the pair is played only if the gap really reached is within 0.10 m of that, else it is left out), then the screen goes
  dark for the reception (four hours) and everybody is taken away unseen. Then the bill is settled, the
  gifts counted (about 140 a guest times the root of the size), and the story of the party told.
- **Married**: a lasting mood (+10, renewed daily while home is not unhappy), next of kin, and the offer
  of a honeymoon: Travel bridge, else three days skipped (a day at a time) for 2,500 and a four-day mood. Put off, it stays in the Family contact's "Things to do" until it is taken or `HoneymoonWindowDays` (60) days after the wedding (`HoneymoonOpen`); `HoneymoonTaken` is set when it is paid for and given back if the trip is called off, and is cleared by the next wedding. A save from before the flag counts as taken if its memories hold the trip.
- **No dead ends**: the day can be put back a week (10 %), called off (the deposit is kept; still engaged),
  the engagement broken off (still together, badly hurt). A missed day moves itself three days once and is
  forgiven; missed twice, the booking is lost. Cold feet the night before: the player's own (talk it
  through, or sleep on it) or theirs (reassure them at the venue, or give them a week at no charge).
  Walking away mid-ceremony strikes the set under a cut; the day can be begun again while it lasts.
- A kind of place the city does not have at the time (its businesses change hands) never blocks anything:
  rings and flowers are then ordered by telephone from the Family contact, an appointment whose building
  cannot be found is kept from home, and a wedding whose venue has gone is held wherever the player stands.
- The casino boat is not offered as a venue: boarding it is a sequence of the game's own with its own
  time skip, and nothing of ours can be set out on it.

### Children

- **Deciding** is a talk at home: both must want it. The partner may not be sure (about one in five); they
  need time, not persuading, and have come round by a second talk ten days later in a happy home. Then: a
  baby (carried by either of you: it is asked) or adoption (a baby, a toddler, or a child of school age).
- **Pregnancy**: three quarters of a child's year (21 days at the default scale). A scan at a fifth and at
  half way (120 each), a birth class at three quarters, each an appointment at a real hospital or clinic
  with the marker, a prompt and an hour or two skipped. **The nursery**: a cot (450, inside a furniture
  store), a pushchair (380, a gift or clothing shop), supplies (220, a supermarket), each a prompt inside
  a real shop of that kind. **The birth** (1,800): "it is time", seven hours to reach the hospital, a long
  night there, then a name (as many second thoughts as wanted). Missed, the child is born all the same and
  the player is told to come home; if the player is the one carrying, it cannot be missed.
- **Adoption**: papers filed in person (1,500; a law firm, else the tax office), a home visit a third of
  the way through (be at home that day), the call, the day to meet. A missed day is simply moved.
- **Then**: three days at home (a mood, and the partner there all day); broken nights for the first half
  year unless there is a nanny, every other night when the partner lives in (Home bridge; default a mood);
  who looks after children under five: you (energy every day, closest to them), the partner (short days,
  half their share of the rent), a nursery (900 a month a child), a nanny (2,200 a month); check-ups at a
  quarter, one, two and four years (80); pushchair walks until three; milestones kept as dated memories
  (first smile, sat up, first word, first steps, first sentence, out of the pushchair, first day at school,
  lost tooth, rode a bike, ten, thirteen, a first job); school from five: public (free) or private (1,500
  a month, better reports); the school run some weekday mornings for whoever wants it; a report every third
  of a year; drawings on the fridge between three and nine; a school play a year from six to eleven and a
  parents' evening a year, both to attend; birthdays (cake at home, a party at home for 150, or at a
  restaurant for 600) with a present from Grandma; pocket money from six (0 / 10 / 25 / 50 a week); a
  college fund (500 a time; 40,000 pays for all of it); grown up at eighteen. Monthly figures are charged
  a quarter each week. Up to three children.
- **A pushchair walk** (from the Family contact, out of doors): 220 m wherever the player likes, the
  pushchair in front of them, the partner alongside when they are home and free; small sights on the way;
  the key ends it early (half way counts). Good for the mood, the child and the two of you.
- **Time scale**: one year of a child's age per four in-game weeks by default (options: 1 to 12 weeks).
  The game's own year (`gameVariables.daysPerYear`, 60 days) is used only for the holiday calendar.

### Wider family, hard times

- Mom texts at every milestone, sends small presents (250 for a new grandchild, 40 a birthday), and drops
  by on some Sundays. The in-laws arrive with the engagement as a contact of their own: a chat, advice that
  reads the household as it is, and now and then a favour asked (a lift, a hand with a move, a loan of 300
  repaid in a week).
- Four family dinners a year on the game's own calendar (at 22, 47, 72 and 97 % of its year): Mom writes
  the day before; with a household it is at home (120 for the food, three hours), otherwise at Mom's (four
  hours, from the Family contact). Missing one costs nothing but the dinner.
- Next of kin is a line on the Family page (the partner once married, Mom otherwise; it can be changed)
  and nothing else.
- Arguments are questions with two answers from a pool of ten that never repeats until all have come up;
  which answer is the better one differs. Counselling (180 a session at a clinic, an hour) mends the most
  the first times. Harmony under 15 for five days and they move out; time apart can also be asked for. A
  call a day, counselling and a little time bring harmony back, and at 55 they can be asked home. After a
  week apart either may end it: married, that is a divorce with a settlement of a fifth of the cash in
  hand, at most 250,000, never a business; unmarried, a parting with nothing to settle. The children stay
  with the player and spend every other weekend, or every weekend, with the other parent, who then pays
  half of what the children cost. Nothing in any of this harms a child.

### The phone

- **Family**: five short pages (household, the family, coming up, memories, budget) and "things to do",
  one at a time with "do it", "something else" and leaving, which is every decision above.
- **Someone special** (only while Social does not keep the partner): ask a friend to introduce you to
  someone, and from then on ring them: a chat a day, an evening out, and the same things to do.
- **The in-laws**: a chat, advice, or the favour they asked.
With the phone contacts option off, "Test: open the Family page" opens the same page face to face.

### The bridges (`FamilyBridges`, `FamilyApi`)

Each is null until its owner sets it (`FamilyBridges.Social = mine;` when its own state is loaded, back to
null when it unloads); until then Family uses its own default. All calls are from the main thread.

| bridge | its owner implements | Family's default without it |
|---|---|---|
| `ISocialBridge` | `Active`, `Ready`, `TryGetPartner(out PersonInfo)`: the one person the player is dating or partnered with (Stage 4 or 5: name, look seed, sex, affection and trust 0..100, days together); `Changed(FamilyEvent, affection, trust)`: what happened to the two (moved in, engaged, married, argued, shared time, broken promise, separated, divorced...), with a suggested change in points; `StartOuting(kind, done)`: "dinner" (true if it started; `done(true)` when it went well); `ClosePeople(list, most)`: who would come to a wedding. It calls `FamilyApi.PartnerChanged()` when the partner changes outside Family's flows and may call `FamilyApi.SharedTime(points)` | one partner record in the family state (a name, a look seed, affection), met and rung through "Someone special" |
| `IHomeBridge` | `CookTogether`, `WatchTogether`, `CareWhenSick(partner)`, `BrokenNight(wakings)`, `HealthVisit(reasonKey)`: true if Home did it its own way | a few lines, a meal, a mood; eight hours off the illness; the "up in the night" mood |
| `ITravelBridge` | `Honeymoon(partner, days, done)` | 2,500, three days skipped, a mood |
| `ILeisureBridge` | `DateNight(partner, done)`, `FamilyOuting("school_play" or "birthday", child, done)` | dinner at a restaurant's door; the play and the party as an errand and a skip |
| `ILawBridge` | `InCustody` (a cell or a prison term; family life waits) | nothing is known; arrests are told with `FamilyApi.PlayerArrested(severity 1..3)`, which `Wiring.Arrests.FamilyHears` calls once for each arrest (1 a night in the cells, 2 when a case is open for it) and each prison sentence (3), only with a partner |

`Wiring/FamilyLinks.cs` fills in Social, Home and Law. Social: the partner is Social's person who is dating or with the player; `Active` is Social's option, `Ready` is "its people are read" (Family waits for that before reading who the partner is); `StartOuting` answers false, so Family runs its own evening, because Social's outings are planned with a venue and a time by its people. Home: `CareWhenSick` shortens Home's illness by eight hours, `BrokenNight` adds half an hour of sleep debt for each waking; Home has no routine for cooking or television for two, or for recording a health visit, so those keep Family's default. `ITravelBridge` and `ILeisureBridge` have no matching door (a trip is bought at the travel agent's desk, and a leisure activity is the player's own at a venue), so they stay at their defaults. A partner who is engaged, married or living in and is not Social's person stays on Family's own record (`Partner.Kept`); a dating stand-in with nobody from Social to replace them ends when Social is on, and the "Someone special" contact is not made again (an existing one answers and says where the partner is).

`FamilyApi` is safe to read at any time: `HasPartner`, `LivesTogether`, `Engaged`, `Married`, `Children`,
`PartnerName`, `WeddingToday`. `FamilyDirector.TakesInteractKey` is true while one of its key prompts is up
(the mod's own E/F on doors stands aside, as it does for a job's prompt).

### Cost

Nothing per frame while nobody of the family's is out and no prompt is up beyond a few comparisons. One
tick a second (five a second during a staged scene): the hour and the day compared, the home looked at only
while the player is inside a building, the errand list only while it has entries. The home's furniture is
read once on walking in. Looking for the nearest building of a kind is one pass over the city's buildings
when a conversation asks or an errand becomes due. A fault in any tick is logged once and switches family
life off for the session; the save is left as it was.

### Test buttons

Partner moves in now (needs a rented home; with Social absent a partner is made up on the spot) - propose
here (a ring is supplied; the answer is a real roll at 90 % or better) - start wedding planning (engaged at
once if need be) - hold the wedding here, now (a classic wedding with everything, out of doors; free: no bill is charged and no presents come in, `WeddingScene.Free`) - start a
pregnancy / move it on a step (scan, scan, class, birth) - start an adoption / move it on a step - add a year
to the children's ages - pushchair walk now (needs a child under three) - open the Family page - clear all
family state. A button that does nothing says why.

### Wish-list for the asset pipeline (no code change needed)

Clips (played by name when the bundle has them, the fallback in brackets until then): `propose_kneel`
(the mod's `kneel_work`): down on the right knee, right hand held up and forward at chest height with a
small box, head up, held as a loop; `propose_react_yes` (`clap`): hands to the mouth, a step back, a nod,
2.2 s once; `propose_react_unsure` (`talk_a`): a hand to the chest, a small shake of the head, 2.2 s once;
`ring_present` (`job_hand_over`): takes the other's left hand and slides a ring on at 1.05 m, 3 s once;
`cheer` (`wave_hello`): both arms up, once. **All five are delivered and played, and so are**
`propose_kneel_down`, `propose_kneel_up`, `ring_receive` and `cheer_loop` (see "Engagement and wedding").
**Played since:** `slow_dance_a` / `_b` (the first dance, after the wedding photograph), `hold_baby` and `rock_cot` (the
partner at home), `cheer_once` (a busker's listener, the chess onlooker). Still wanted: `sit_sofa` (only if seats are solved).

Props (drawn from the prop bundle by these ids when present; plain shapes from `FamilyProps` until then,
origin on the floor, facing +z, metres): `wedding_arch` (2.1 wide, 2.4 high, 0.3 deep, flowers along the
top), `flower_stand` (a pedestal 0.95 high with an arrangement, 0.4 across), `aisle_runner` (a strip 1.1
wide by 1.0 long, laid end to end), `cot` (0.72 x 0.9 x 1.26), `high_chair` (0.48 x 1.05 x 0.5). Not used
yet: `bouquet` (no clip holds it), `birthday_cake` (a birthday is told, not shown). `ring_box` comes with
the kneeling clips and `baby_bundle` lies in the cot (both delivered and used). Still wanted: `toy_box`.

### Not verified in the game

Written against the decompiled code and never run: whether a home's interior has a navigation mesh for the
game's NPC agent type (the player's is tried second; with neither the partner is not shown and it says so
in the log); how the game's Formal clothes look on a seeded face; that the sizes of real apartments make 30
m2 for two a sensible bar; the look of every clip on the partner indoors; turning the player's body to face
the partner at the altar (set once on the character's transform while they are held); the three-day time
skip of the default honeymoon; the photograph with the game's HUD objects switched off for two frames; the
plain-shape stand-ins' materials indoors; which of the game's business types a given save actually has (a
kind the city lacks is said so, and the conversation offers what there is).
### Clashes looked for, and what was done

The game's own bindings are the JSON in its `BigAmbitions.InputSystem.PlayerInput` (one asset, always enabled; which
code polls an action decides where it counts) plus a few hard-coded keys. Its driving keys beyond these (throttle,
brake, gears, handbrake) are in an NWH asset that is not in the decompiled code, so that column is not complete.

| key | the game | the mod | verdict |
|---|---|---|---|
| Enter | Confirm: a question's yes, the game's panels | the "Garage" button of the game's panel in a repair bay | the game's own routing (`OverlayUI.OnPlayerActionPressed`); the mod reads no key for it. The Garage key (K, earlier B) is gone |
| F | Interact | `Interact` | by design: the mod's prompts stand aside while the game shows its own (`TakesInteractKey`, and the game's overlay) |
| E | rotate the camera right (the game's own pedestrian camera) | `InteractAlt` | only while the game shows no interaction of its own (`JobKeys.InteractDown`); the Events prompt lacked that test and has it now |
| V, G | nothing | `CameraToggle`, `GentleThrottle` | free |
| Left Alt | snap in placement and the interior designer | `FreeCursor` | different screens |
| Right mouse | right click | `LookBehind` (driving) | the mod feeds the game's own right-button state, as before |
| F7, F8 | hide the interface / free camera, in the game's debug builds or with `-console` only | `PerfProbe`, `PerfBench` (Shift), `PhoneDump` | no clash in a normal game |
| F9, F10 | nothing | `CharacterReport`, `PhotoMode` | free |
| 1, 2, 3 | tools in the interior designer | a row of three choices (`JobKeys.ChoiceDown`: jobs, leisure, transit, the theory paper, the cell, the courts' panel); photo mode | one at a time: a choice row is on screen only while its owner has the player, and none of them with the designer open |
| Space, Enter, Tab, Esc, N, M, T, P, O, I, Q, C, H, [ ], F1, F2, F5, F12, Backspace, Left Shift | the game's | nothing of the mod's outside photo mode (paused) | free of the mod |

Free in both for a future action: F3, F4, F6, F11, the digits 4 to 0, and ; ' / \ - =. The letters L, U, Y, Z, J and X
are used only inside photo mode, R only in the game's placement.

## Rules for new code

Performance is a requirement, not a nice-to-have.

- No `KeyCode` outside `Core/Keys.cs`: test a key with `Keys.Down(action)` or `Keys.Held(action)` (one engine
  call each), and build any text that names a key with `Keys.Label(action)` (cache it; `Keys.Version` goes up
  when a binding changes). A new key is one `KeyAction`, one row in `Keys.All` and its name in `en.json`.

- Nothing allocates per frame. No `OnGUI`, no LINQ or string building in `Update`, no
  `FindObjectsOfType` outside a one-off setup. Put anything on screen through the game's own interface (see the next point); there is no shared canvas of the mod's. The one font asset others read is `GameUi.GameFont.Asset`.
- Something offered at a place in the world is offered in the game's own interface, and the mod draws no key prompt
  or panel for it: a click on the object (`docs/game-ui.md`, sections 7 and 11), the game's panel over a place with
  its keys (`GameUi.WorldPanel`, section 9), a list in the game's own window (`GameUi.TravelList`, section 10), a
  button in the panel the game opens on the object (section 13), a button more in the game's own panel at the place
  (section 16), the game's destination marker (`GameUi.GuideMark`, section 17). The buses and the subway journey are
  the model (`Transport/TransitDirector.Player.cs`, `SubwayJourney`); Home and the Garage use every one of these.
- A handler on one of the game's event fields is kept there with `SaveHook.Keep` (the save) or `EventHold.Keep`
  (any other field: entering and leaving a building, the map, the game's events), never by taking it off and putting it
  back on a timer: each of those builds a new list of the game's handlers.
- A look over everything of a kind in the scene (every material, every lamp, every building) is never done in the
  frame that asked for it once the city is running: the list is taken once and gone through a few hundred at a time
  on a timer (`RefractionOff`, `StaleLampLights`). A performance tweak is told of a scene loaded or unloaded with
  `WorldChanged(bool loaded)` and only notes it.
- Anything large done in one frame is named with `SessionWatch.Mark` (see "The session record").
- Push values to the engine only when they change; poll game state at the slowest rate that works
  (per in-game hour, or a timer), not every frame.
- Visual options that cost frames are off by default and say so in their label.
- A feature is one component under `Scripts/<Feature>/`, registered in `Core/RealLifeOverhaulMod.cs`,
  with a toggle in `Core/Settings.cs` and its strings in `Locales/en.json`.
- Anything added to a pooled game object (traffic cars) must be removed again.
- People come from `People/ActorPool` only, through an `ActorRole`, and never more than twelve at
  once, of whom at most four are only there to be seen (`ActorRole.EnterPresence`: officers on the
  beat, by a parked car, at a junction). Those are taken back without warning the moment a scene
  needs a character, so whoever owns one must cope with finding nobody behind the role. A scene must work when the pool gives it nobody, tick its people on a timer (not per
  frame), and release every one of them in every way it can end.
- Anything changed on a game object (materials, renderer flags, lights, textures) is recorded and put
  back when the option is switched off and when the scene ends. A feature that meets something
  unexpected logs once, restores the game's own state and stays off; it never throws per frame.
- The stretcher and its crew never go anywhere by a navigation agent. It is wheeled along a line worked out
  beforehand (`People/CrewPath`), and it is the stretcher that moves its crew (`Stretcher.Wheel`): every frame
  it puts whoever pushes on the line, itself an arm's length ahead pointing from them to its own place on the
  line, the second of a pair beside its leading end (the near hand on that rail), all on the ground height the
  line was given. Agents are only told the speed, for the walk to play to (`StreetActor.Convey`). So nothing on
  the navigation mesh (a wall's cut, a vehicle's, an edge) can turn the crew aside, hold it up or stop it short,
  and there is no "own route" to fall back on. The few steps between the places they work at beside it are
  walked the same way (`Stretcher.Walk`: round the bed's corner, round the casualty's head or feet). With
  `stretcher_push_bar` in the bundle the hands are on its grips (0.225 m either side, 0.95 m up, 1.11 m from the
  middle: where `push_cart` holds a rail for someone 1.55 m from the middle), and the crew kneel to lift 0.10 m
  further out than that, clear of the bar. Nobody at the stretcher has anything else in a hand: the medic bag
  rides on the bed (`ServiceKit.Stow`) and is in a hand only while its owner walks without the stretcher.
- The ambulance crew has one way out and back (`People/CrewWay`), planned once when the ambulance has stopped,
  and everything else agrees with it. Corners are rounded to 1.2 m and the last stretch is straight for the
  stretcher's own reach, so it ends exactly on its place pointing exactly along it. Every pose on the way (the
  bed, whoever pushes, whoever walks beside it) is tested before anyone sets off against vehicles and
  fixtures (0.3 m to spare), the tape (never run along, crossed in one opening), and the casualty's own body
  (0.2 m). It tries both sides of the ambulance and of the casualty, straight, square through an end of the
  tape, or by a place to one side; on the carriageway and out of any lane traffic is let past in first, and
  only then over a pavement or through that lane (held shut meanwhile: `SceneCordon.HoldForCrew`). Two last
  moves: a run-in round a corner onto the body's own line; or, where the casualty lies across a road too
  narrow for that, a swing-in (straight at their side to 2.1 m off their line, then a quarter turn into place
  with the crew at the bed's ends). Back by the same places, round behind the ambulance and up to the rear
  doors square, leading end 0.25 m from them (swung square on the spot where there is no room to turn). The
  layout at the casualty is fixed from the body's axis (`CrewWay.Lay`): the stretcher parallel, head to head,
  0.55 m away; whoever treats at the chest on the other side; the second at the head or hips.
- The cordon stands clear of that way (`SceneCordon.SetCorridor`): no post, cone or barrier within 0.3 m of
  anything of the crew on it, no tape across it, an opening at least 2.6 m wide wherever it crosses a side, the
  near end's own opening where it crosses that end. Tape that already stands clear is not touched. When tape
  is run again, a place is never judged by a navigation mesh that the tape's own walls are still cut out of
  (that moved every post in off its line, onto the crew). Traffic let past a scene waits at a hold line 24 m
  before it, behind where the ambulance stands, never beside its rear doors.
- A crew's step ends when it has been done, never on a clock: the stretcher at the end of its line, both at
  their places, the lift played. The ambulance does not drive off from under its crew (only the scene's hard
  limit ends that). A step that is well overdue may be finished at once only when both of the crew are out of
  the player's sight. Each step writes one `People: crew check (...)` line with what was measured, what is
  allowed, and PASS or FAIL: set off, through the opening, parked beside, lifted, set off back, back through
  the opening, loaded; the cordon adds its own `Cordon: run round the ambulance crew's way ... PASS/FAIL`.
- Nobody but the crews and the casualty is inside the tape: a witness or passer-by goes out to the kerb the
  moment the tape is up or the paramedics are out (`SceneWitness`), and gives their account there; the place
  they wait is never on the crew's way.
- A fine for people struck is taken at the roadside only up to the share of the cash in hand a court's fines
  are bounded to (`PolicePursuit.Roadside`: 35 %, never bounded below 250, `JusticeLaw.Bound`); the rest is
  left to the court case, which credits what was paid, and the toast says so.
- Ragdolls come from `Ragdoll/RagdollSystem` only (six at once: 11 bodies each while falling, nothing simulated
  at rest; when all are in use the one who has lain longest gives up their place) and are ended through it on
  every path. A character must never be moved, shown, hidden or animated while it is one
  (`StreetActor.Limp`, `RagdollSystem.Holds`), and a pedestrian borrowed from the game goes back
  through `PedestrianTakeover` with nothing of ours left on it.
- A look is accepted in the HDRP preview (`CharacterAssets/preview.ps1`), next to the game's own, at play
  distance and close up, never in Blender; code that decides how a look is drawn is kept game-free and listed in
  `CharacterAssets/PreviewProject/shared_sources.txt`, so the preview runs it too.
- New looks are meshes and textures only, in the one asset bundle, loaded once per session in the
  background. Materials are always copies of materials the game is already drawing with (never
  change shader keywords on them), only for the characters that show the player, and with a row
  in `Character/CharacterCatalogue`. The player's picks are kept by id in the save's modData, per save; a pick
  never outranks a piece the player then picks in one of the game's own screens (`CharacterCatalogue.Observe`).
- Never leave data in the savegame that the base game cannot load without this mod
  (see the comment on `Core/ModState.cs`).
- Props the mod builds itself (cordon, stretcher) are made once, kept switched off and reused.
  Nothing is built or destroyed per scene. Their meshes and materials come from
  `Core/PropLibrary.TryGet` (modelled props from the bundle: shared meshes, two shared instanced
  materials on one atlas, no colliders) with `Core/Shapes` as the fallback when it says no (shared
  plain meshes, one material per colour). Never a material or a property block per instance.
- A vehicle driven by the mod (`EmergencyDriver`) never touches anything: it brakes for vehicles and
  for people (`PeopleAhead` covers those without a collider), moves over only into another lane of
  the road graph, never steers off its road points and never forces through. The one exception is a
  car being stopped at the roadside, which pulls in to the kerb side of its own lane first
  (`EmergencyDriver.PullOver`), and only where `SceneParking.KerbRoom` found that strip to be
  carriageway, nobody's lane and free of everything. Held up near its goal
  it stops in lane and its crew walks. A response vehicle parks on its own route, where
  `SceneCordon.Stand` puts it: in a lane the wrecks block, on the scene's own carriageway, the
  ambulance nearest the casualty and outside the tape, nose to the scene. Never at a kerb, on a parking
  strip or across a crossing: the place is tested before it sets off with its whole body and 0.3 m all
  round against every solid layer (`SceneParking.Standing`: vehicles, sign posts, hydrants, poles, a sign
  overhanging the road), and one that fails is moved back along the unit's own lane only
  (`TrafficAccidents.SettleStand`). Distances to a place are measured along the
  route, never as the crow flies. Traffic standing in a unit's way is what gets moved
  (`TrafficAccidents.ClearTheWay`), not the place.
- A unit with its crew aboard leaves, forwards, within seconds. The loaded ambulance
  goes at once, siren on: the tape is taken in (`SceneCordon.Lower`), bystanders are sent to the kerb, the
  officers are called back, and it drives on along its own lane: straight through where the scene is if
  that lane is clear, round the wrecks by a lane beside them if that is (the gate held shut for it:
  `SceneCordon.HoldGateShut`, given back on every path), and otherwise the wrecks standing in its lane
  are taken off the road first (out of view at once, in view after five seconds). The patrol car is the
  last to go: when the ambulance is past the scene and the patrol car's own lane through it is clear, it
  drives off while the road is still closed, and the road is given back when both are away; with a wreck
  still in its lane it waits for the scene to clear and drives off through it. No unit is ever handed back
  to the traffic system standing: it is driven until it is clear of the scene and moving along a lane (which
  may be after the scene has ended: `TrafficAccidents.TickDepartures`), or taken off the road out of view.
- A departure is a plan, not a hope, and the two units never wait on each other. Where the patrol car
  stands is settled against the ambulance's way out when the units are scheduled
  (`TrafficAccidents.PlanDeparture`): at the far end only if it stands clear of that line by both half
  widths and 0.4 m, else behind the ambulance in its own lane. A unit leaving through the scene by a lane
  checked for it does not pull out to pass (`EmergencyDriver.MayPass`): the lane beside is the other
  unit's. A driver that is out of its lane and held up goes back to its own lane when that is clear. A
  leaving ambulance held up by the patrol car has it moved at once (`GiveWay`): officers called aboard,
  driven off ahead of it, or backed along its own lane when the two are nose to nose (the one case in
  which a unit reverses). "Could not be moved" does not exist for the mod's own vehicles.
- No response vehicle is taken off the road where somebody may see it (in the camera's view, or within
  45 m of it): one that cannot get away stays, says so once (`departure check (... held up): FAIL`), keeps
  trying to drive off, and is removed only out of sight. `TrafficWatch` (`Guard`) still reports any
  response vehicle that does not move for 25 s. An ordinary ambulance from the traffic pool that comes
  within 90 m of a scene is taken off the road out of view, so that only the scene's own stands there.
  Each step of a departure writes one `Accident: departure check (...)` line with what was measured and
  PASS or FAIL: planned, units parked, ambulance set off, ambulance past the scene, patrol car set off,
  patrol car past the scene, road reopened.
- The traffic system's springs have no damper: what steadies a car is the traffic system replacing its velocity every
  physics step, which stops the moment the car is ignored (`SetVehicleIgnored`). A vehicle the mod holds is therefore
  always either driven (`EmergencyDriver` bleeds off its vertical speed) or kinematic (`Pin`, `ResponseUnit.Park`),
  never left standing unparked; it goes back only through `TrafficTools.ReturnToTraffic` / `Remove`, which put its
  body right and log its state. Nothing solid and immovable is left where traffic can end up on top of it (a body at
  rest gives way to a vehicle over it: `Ragdoll.YieldToVehicles`). `TrafficWatch` reports and stops whatever bounces anyway.
- The game moves everyone on foot (the player included) on the navigation mesh; colliders stop
  nobody. Anything that must be solid for people is cut out of the mesh with a carving
  `NavMeshObstacle` on an object of the mod's own (`SceneObstacles` for vehicles, the cordon's
  walls), pooled, switched off on every exit path, never put on a pooled car, never where the
  game's pedestrians walk (pavements, crossings), and always with a way out left open. A cordon wall
  runs exactly along tape that went up (and takes the barriers of an end in with it), so what looks
  closed is closed; the cordon says in the log whether each wall really cut the mesh.
- Tape, posts, barriers and cones stand on the carriageway only: never on a pavement or a kerb, never
  through anything solid (vehicles, street furniture, buildings, steps). Cones are a taper upstream
  of an end, beyond the response vehicles, laid when those are in and taken in when one leaves.
- A vehicle that shows its driver in the cab (the delivery truck) has that figure hidden while the
  driver is on the road as one of the scene's people, and shown again when the car is let go
  (`TrafficAccidents.EmptySeat` / `Unpin`). So has any further vehicle of a crash the road was closed
  for (the third car, whose driver nobody plays): nobody sits at the wheel of a wreck through the scene. Only cars and SUVs are rolled; a rolled car is frozen flat
  on its side, roof or wheels, never leaning.
- Nobody is laid down where the whole length of a body is not clear of every vehicle
  (`AccidentCast.BodyClear`); a casualty on the stretcher lies along it on their back (`SupinePose`).
- A scene says in the log, once per transition and never per frame, what each unit and crew is
  doing (dispatched, held up, parked at N m, crew out, stretcher up, loaded, left), so that a run can
  be diagnosed from `Player.log` alone. A scene is planned to clear inside three minutes; only a
  casualty still being seen to can take it past that, and never past five.
- A test button that cannot do what was asked says why: `Core/TestRefusal` (one enum value, one sentence in
  `en.json`, one log line with what was measured). Never one sentence for several causes.
- A scene leaves the road and the traffic as it found them on every way out, traffic shut down included: a car
  the mod holds is handed back or taken off the road (the game's own clearing skips cars with a scripted route),
  and `TrafficAccidents.Recheck` looks again on the next tick out of doors and says so if anything was left.
- Nobody gestures at nobody: conversational gestures play only while the person spoken to is
  standing with them. A scene is worded as what it is (one car is never "two drivers").
- Roads are closed only through `SceneCordon`, which remembers exactly which road points it
  switched off and opens those again on every exit path (`TrafficAccidents.Clear`). Nothing else
  may touch `temporaryDisabled`, with one exception: `Civic/RoadClosure` closes one lane beside
  roadworks by the same switch and the same discipline (only points that were open, never one
  somebody else has shut, only a stretch with no junction and with every way in shuttable where
  traffic has another way to go; exactly those points opened again on every path out).
- Civic life (`Scripts/Civic`) has a cast of five of its own (`CivicCast`: a second instance of
  `CityPeople`, made a person at a time when first needed). At most one construction crew of three
  and one canvasser are out together, or one rally of four; they walk in from out of view and walk
  off, like city life's people. It cuts nothing out of the navigation mesh.
- Officers and paramedics wear `People/Uniforms` and the equipment of `People/ServiceKit` (pieces on
  bones: no component, collider or shadow, hidden with the body, removed when the character is
  dressed as anyone else) and nothing else; civilians stay random. An
  ambulance is only ever a response vehicle (`TrafficTools.IsAmbulance`): never a wreck, a
  runner or a suspect, and no other vehicle stands in for one.
- The living city (`Scripts/CityLife`) has its own pool of eight (`CityPeople`) and never takes from
  `People/ActorPool`. Its people are placed only where `CitySpots` found and tested room, only out of view
  (the test buttons are the one exception; and even then whoever comes later walks in from out of view), and
  move only by their navigation agent with the game's walk. Its budget is fixed: eight people, three sites, one
  tick a second per site, a path per walker per stretch; per frame only a turn on the spot or a timed moment,
  and only while there is one. A prop is always exactly one thing's: in a hand, brought along, standing in an
  activity's dressing, or fallen; `End` gives all of them back in every state. A
  tool is in a hand only at the grip the clip being played was made with (`CityPerson.Work`); an activity whose
  clip or prop cannot be had does not start. Nobody gestures at nobody: a greeting plays only when someone on
  foot is in front, and a conversation always has both people animating at each other.
- New motion is a Humanoid clip from `CityAssets`, played through `ActivityAnimator`; never a pose set on
  bones by hand. Somebody who is walking keeps the game's own walk and carries with a masked clip (`PlayOver`);
  a whole-body walking clip is only for where the game's walk will not do, and then its speed is matched to the
  ground (`MatchSpeed`) from a timer, never per frame. A character that is about to become a ragdoll has its clip player disposed first.
- People stand and walk at street level: positions come from `StreetSpots` (which refuses any
  walkable point that is not at the height of the ground beneath it) and they travel only by
  their navigation agent. Poses use animations the characters have (`StreetActor.Knows`) or
  `KneelPose`; a pose the animator lacks is skipped, never substituted blindly.

### Map places (`Core/MapPlaces.cs`)

**Every place a feature adds to the city must be registered here**, and nothing else in the mod touches the
map's markers or filters. One statement makes a kind of place, one more gives it its places:

```csharp
MapPlaces.Category stations = MapPlaces.Register("police", "reallife_law_map_stations", blue, MapIcon.Shield, onByDefault: true);
stations.Set(places);   // IReadOnlyList<MapPlace>; or Add / Remove / Clear / Changed(place) whenever they change
```

A `MapPlace` is a world position and a name, and optionally the `CityBuildingController` it is (and two locale
keys for that building's card). `Register` is called once per city load from the feature's own start-up; the same
id gives the same category back. Optional: `custom:` a sprite of the feature's own (with `MapIcon.Custom`),
`inStreet: true` to show the markers in the street within 100 m as well (as the game does for the player's own
buildings; police stations use it), and `category.Available = () => Settings.X` to hide the row and the markers
while the feature's option is off (call `MapPlaces.Refresh()` when that option changes).

Built-in glyphs (`MapIcon`, drawn in code, 64 px, white on nothing: the marker supplies the colour): `Shield`,
`Target`, `Bus`, `Bed`, `Scales`, `Wheel`, `Bicycle`, `Wrench`, `Briefcase`, `Ballot`, `Cone`, `Cross`, `Star`, `Flag`.

One option is over all of it: "The mod's places on the map" (`Settings.MapPlacesShown`, with the camera options;
off hides every row and marker, Law's police stations included, which also keep their own option).

**The other features' places: `World/MapLinks.cs`.** Apart from Law (which registers its precincts itself) no
feature calls `MapPlaces`: one component, added last at city load, reads each feature's places through members
the feature already has (or a read-only accessor) and registers them. No feature knows of it, so one whose
places cannot be read is left off the map with one log line ("Map links: the places of '...' could not be
read") and nothing else changes. A new kind of place is one `Link` in `MapLinks.Declare`: an id, a title key,
a colour, a glyph, `Enabled` (the feature's options), `Stamp` (a few integer operations that change when the
places do: a count, ids, a position) and `Fill` (the places, read only when the stamp has changed).

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

A row is made the first time its kind has a place, and is offered only while it has one and the feature's option
is on (so "Polling places" and "What's on" are in the list only while there is something to show; with the option
off the markers are removed, not hidden). It looks once every three seconds of real time; markers are made six at
a time a fifth of a second apart. Grouped to keep the list to a dozen rows with Law's: docks, racks and stands in
one row; boards and employers in one (they are the same places); sights and fishing spots in one (the fishing
spots are the waterside landmarks); the four passing things in "What's on". Not registered, because they are the
game's own buildings with the game's own map filters: law firms, cinemas, theatres, nightclubs, clothing stores,
travel agencies, filling stations and repair garages as such. Not registered because the game's own destination
guider already marks them when they matter: Family's appointments and wedding venue. A stop that is dressed and a
hotel no longer add a marker of their own (`BusStopDressing`, `Hotels` did until 0.39.0).

Unverified (the game could not be run): everything in the table as seen on the map; that a hundred-odd unticked
markers cost nothing while the map is shut (they are inactive objects); how the list reads with twelve rows.

What the game's map allowed, and what a category therefore gets:

- **A real filter row.** The map's filter list is not data: it is built in code at start-up from one template
  row (`CityMapFilters.InitializeFilters` -> private `CreateFilter(name, icon, data, category, ...)`). That
  method is called by reflection, so the row is the game's own: icon, label (the title key), tick box, found by
  the search box, switched by its category's switch and by "toggle all", and its arrow button flies the map to
  the category's place nearest the player. Rows go under the game's "Special buildings" (with the subway
  stations, the bank, the hospital), moved up from the end of the list to the end of that category; if that
  category cannot be found they get a category line of their own (`reallife_map_category`).
- **Nothing in the save.** A game row writes its name into the save's `SelectedCitymapFilters` when ticked. The
  mod's rows are taken out of the game's table of rows and set up again with the game's own "not in the
  selection" flag, so the save never sees them; whether a row is ticked is kept in PlayerPrefs
  (`rlo_map_<id>`), per machine, and put back each time the map opens.
- **Markers** are the game's own (`CityMap.AddPoi`), shown on the open map while the row is ticked. The game's
  filters only ever switch *buildings* on and off (by type, business or address), so the markers are switched by
  the facility itself, on the map's own events (opened, closed, a row ticked): nothing per frame.
- **Buildings lit.** A place that is a building (`MapPlace.Building`), or stands at one that keeps its own card
  (`MapPlace.Lit`: a sign on a school's wall, a help-wanted board, a polling place, a construction site), has the
  building lit on the open map while its row is ticked, exactly as the game's own filters light theirs
  (`CityMapFilters.ApplyFilters`): `SetHighlight` in the row's colour and the building's own marker with the row's
  icon, which stands for the place there instead of the mod's marker (in the street the mod's marker stays). The game
  looks at every building again whenever a filter changes, a few a frame from `GameManager.Update`, and puts out
  every light that is not its own; a lit building of ours that it put out has its marker switched off, so the
  driver's `LateUpdate` lights it again in the same frame, before it is drawn. A building whose marker the game
  always shows (the player's own) is lit again when each look is over (`CityMapFilters.Work.HasPendingWork` falls).
  Nothing is lit where the game puts out its own lights: its choice of a subway station, the phone's full menu over
  the map, a picture of a building being taken (`buildingOutdoorsCameraCaptureController` active), nor while
  something is picked on the map (`MapPlaces.Quiet`). The cost: a look at some forty buildings a frame while the map
  is open. Not lit: the custom shop's repair stations (the game's filling stations are not buildings it can light),
  bus stops, bike places, sights and what is on (no building). The courthouse's card names it with its hours.
- **Names.** A game marker has a name plate that the game shows only for directions (destination, delivery).
  It is switched on for the mod's places (its "dismiss" button hidden), so every marker carries its name.
- **Set destination.** The game offers "set destination" only on a *building's* card. A place that is a building
  gets exactly that, with the place's name on the card instead of "for rent" (the card is rewritten the moment
  the game writes it: one reference comparison per frame, and only while some place is a building). Every
  marker, building or not, can also be clicked on the open map: the place becomes the destination (the game's
  own destination marker, with name and distance) and the map closes.
- Works however the map is opened (phone or key: both end in `CityMap.Toggle`, which raises the event used).
  Everything is removed when the city unloads. If the game's code is not as expected (a private member renamed)
  it is logged once and that part is left out: no row means the markers are always on the map; no name plate
  means markers without names.

Unverified (the game could not be run): how the name plate looks beside a marker; that a click on a marker's
blob reaches the button added to it rather than the building under it; the row's place in the list.

## The calendar and the price level (`Core/CalendarRules.cs`, `Core/GameCalendar.cs`, `Core/Prices.cs`)

The game counts days and has a year of so many of them, chosen when a game is started (60 to 365;
`gameVariables.daysPerYear`, 60 in a save that says nothing), but no months, seasons or dates. Every part of the mod
reads the calendar from `GameCalendar` (the rules, game-free and checked by EconomyCheck, are `CalendarRules`):

- **The year**: the save's own, or 60 when it says less than 8 days.
- **The month**: a twelfth of the year, three days at least (`MacroCatalogue.MonthDays`: 5 days in a 60-day year, 30
  in a 365-day one), the city economy's month. Something paid by the month is priced by the day and paid a month's
  worth at a time (`GameCalendar.Monthly`), so the year's length changes nothing by the day, as the game's own daily
  rent and wages are not. Car insurance runs a month (`CarRules.PolicyDays`, its premium in proportion to the four
  weeks its figures are written for); childcare and private school are by the day (32, 79, 54), paid by the week and
  named by the month.
- **The seasons**: the weather's (the year begins in midwinter; winter to a sixth, spring to five twelfths, summer to
  two thirds, autumn to eleven twelfths, winter again), counted from spring where travel and property count theirs
  (`SeasonFromSpring`). `EventCalendar` and `Weather.SeasonOf` read them from here.

Money at the city's prices is `Prices.At(amount)` (`Core/Prices.cs`: the mod's, not the economy's, as every feature
reads it): an amount written at the start's prices (the city's price index at 100), at today's, rounded as prices are
named (five cents under 10, fifty cents under 100, 5 under 1,000, 10 under 10,000, 50 above; at the start's level an
amount is itself). The level is the very factor the city adjusts the game's own rents, wages, shops and services by
(`CityModel.Costs`, handed in by `Wiring/EconomyLinks` as `Prices.Factor`), so a price of the mod's and a game charge
adjusted by the city are always at one level; within 1% of the start (`PriceRules.Quiet`, the city's own
`AdjustQuiet`) it is the start's. While the city's economy is off the level stays at the start's.

Every fixed amount of the mod's goes through it: fines, fees, fares, bail, lawyers and court costs, garage and car
costs, bikes, clothes, drinks, family costs, hotels and trips, the banks' floors and caps, job and gig pay, the bounds
of what a crime or an event can take, and the cash an event or a crime needs on the player. The features with an
index of their own (Brands, Company, Estate, Online, Services) read `Prices.Level`. Texts name no amounts: the Guide's
figures are `.Fact`s read from the code; a life event's prompt fills `{cost}` (its price, `LifeEvent.CostNow`) and
`{sum}` (the first fixed payout of its yes answer, `LifeEvent.Quote`) in its body, its answers being plain keys of the
game's box (CatalogueCheck fails an event text that names a `$` figure); prompts and toasts are filled by the code
that shows them; the licence quiz fills today's fines (`TheoryTest.Figures`).

## The game's daily statement (`Economy/GameBooks.cs`)

The game builds each day's statement (`GameInstance.financialSummaries`) first thing on the next day, before
`onNewDay`: for each business, four lines of costs summed from that day's transactions by the business's address and
the game's categories (salaries, rent, marketing, licensing fees), with goods, sales and theft from its own records.
The game's bank sizes a loan by it (`PlayerHelper.CalculateDailyIncome`: the last seven days' profit), and EconoView
and the daily summary show it. A cost of the mod's that is one of those lines carries the category and the address:
Company's wages (raises, overtime, sick and strike pay), marketing and licences (`CompanyGame.Take`), and the
business issues' marketing, licence and staff costs (`BusinessGame.Charge`). What is booked after midnight for the day
that ended is put into that day's statement when the mod's day is done (`EconomyDirector.OnDay`, after `AfterDay`):
the four lines summed again as the game sums them, and the totals with them (`GameBooks.Refresh`). Insurance,
accountants, hosting and the city's weekly account have no line there and stay in the mod's own books (Finance). The
city's cost of living adjusts the game's own rent and wages, and on a line of its own the game's shops and services
at prices of its own (`CityGame.LivingTypes`: tickets, rides, the hospital and the doctor, gyms and entrance fees,
shopping, fuel and stalls, fit-outs, training and hiring). A charge under rent or salaries stays on those lines; a
purchase at a wholesaler's is left out, as the city writes those goods' prices at today's; and the mod's refunds of a
game charge (`EconomyBridges.GivenBack`: a ride the day pass covered, a lent car's ticket) count against it.
`CityGame.DayFigures` passes over the mod's own transactions, which are at today's prices already. Each of the city's
lines is booked where the game's own figures were (`DayLine`: by address, by the category the statement keeps them
under, and deductible where the game's tax deducts them), shared out by those figures in whole money that always sums
to the line (`CityAdjust.Split`; a carried amount goes with the day it is taken). `GameBooks.Refresh`, called by the
city once its lines are booked and again when the mod's day is done, sums the day's statement as the game sums it:
a business's four lines by category, its takings as the game's sales and the city's line on them (the goods sold
scaled row by row so that they still add up; where the game booked no takings of its own, the sales of its order book:
a factory's exports) and the mod's own takings there (online orders less refunds, `OnlineGame` putting each product's
sales among the rows itself; the estate agency's commissions at its office; the broadband company's bills at the
headquarters, `EconomyBridges.Takings`, with its network as a row among the goods, `EconomyBridges.CostRow`), every sum made afresh from what the save keeps and never
from the statement itself, so that a second refresh counts nothing twice and loses nothing entered between the two;
a rented home's rent as its daily rent and the city's line on it, and the day's own figures (loans, health insurance, the headhunter, parking,
salary income, and the wages of no business with the city's line on them). So the game's taxes (sales of the period
from the statements, deductions as booked), its bank (`PlayerHelper.CalculateDailyIncome`) and EconoView see the
city's lines. Readers of a statement take its takings as today's: an insurance premium's base is at today's prices and
its share of the takings is not raised again (`CoverRules.Premium`); the rota planner, which reads the order history
(the game's own prices), raises a customer's worth by the city's takings factor (`CityEconomyDirector.TakingsFactor`).
The property business's hotels and car hire are in no statement (a hotel is held in the mod's books, the hire lot is
no business of the game's), so they pay their own tax when the game's tax year turns, on the same day as the game's:
its rate on the year's takings less their running costs, nothing on a loss, and nothing while the year's business
income in all is under the game's threshold (`EstateRules.TradingTax`, `EstateDirector.TradingYear`). Their running
costs are counted there and are not marked deductible, so nothing is deducted twice.

## Power and water (`Scripts/Utilities`)

Every place the player rents or owns (`BuildingRegistration.RentedByPlayer`, a home or a business) has an account for
power (City Light and Power) and one for water (Riverside Water). The rules are game-free and checked by EconomyCheck
(`UtilitiesCatalogue`, `UtilitiesRules`, `UtilitiesStore`); the director reads the game and does what they say.

- **Accounts.** A rented place has the landlord's, supplied at once: the player just pays. A bought place
  (`BuildingOwnedByPlayer`) is supplied for three days at half as much again (the temporary supply) and then cut,
  unless an account is applied for in person at the office's desk; it connects the next morning. A poor credit rating
  (Economy's grade, through `UtilitiesBridges.Grade`) asks for a deposit of one or two months' estimate, never a
  refusal; it comes back after six bills paid by their due day in a row, or with the last bill. A place given up has its
  last bill at once, the deposit set against it.
- **Use.** Each morning yesterday's use is added to the month's, at that day's prices (`Prices.Level`): a home by its
  floor (`Building.totalSqm`) and the people in it (the player, a partner who moved in, children at home, a roommate:
  `UtilitiesBridges.Household`); a business by its floor and its kind (the game's business types, from offices to
  industry), a quarter of it on a day it does not open (its week of opening hours); both by the season (the weather's).
- **Bills.** At the turn of the month (a twelfth of the year) each account is billed, due in half a month (two to
  fourteen days). Auto debit (the Utilities app) pays on the due day. A bill a day overdue has the late fee added once;
  three days after its due day the supply is cut, and reconnecting at the desk costs a fee as well.
- **A cut.** A business without power closes (Business's own switch, `UtilitiesBridges.CloseFor`, hour by hour), as one
  without water does where it needs water (food and drink, salons, gyms). At home the shower and the sink give nothing
  without water, screens and the mod's cooking nothing without power (`HomeTidy.HasPower`, `HasWater`), and a mood
  modifier stands for each (`ModState.NoPower`, `NoWater`); hygiene and its colds follow by themselves.
- **The office.** The largest office building of the city that no other feature has made its own (not the
  courthouse), with a lettered sign by its door; the desk is the game's own list (`DeskList`, `UtilitiesDesk`): pay a
  bill, apply for an account or a reconnection, the accounts that connect tomorrow. It lights up on the city map.
- **The app.** Every account with where it stands, the month so far, what is owed and when, and auto debit; the white
  box has the picked account's use, bills and deposit, the auto debit switch and "Pay now".

- **The internet.** Four rival providers (`InternetCatalogue`: cable for the whole city but The Hamptons, fibre in
  three districts only, fixed wireless and a copper line everywhere), each with its plans, reliability and a price that
  walks month by month within bounds (a promotion now and then: `InternetRules.NextPromo`). A plan is bought in the app
  (no office: a private provider sells online) and installed two days later; on a contract of twelve months its price is
  held and the installation free, month by month it costs a fifth more; leaving a contract early costs half of each month
  left. It is billed with power and water, a day's share of the month at a time, and cut the same way. Outages are drawn
  from the save's own seed, the day and the provider (`InternetRules.Draw`), so a reload changes nothing; a day with one is
  not charged, and a business plan is repaired in half the time. Web design and software companies and the headquarters
  (`InternetCatalogue.Needs`) cannot open without a connection that works.

- **Your own broadband company** (`BroadbandCatalogue`, `BroadbandRules`, `BroadbandCompany`). Late in a game and very
  costly: the city's operator's licence (with a headquarters: its office, and where it bills from), then a fibre network
  dug district by district, each a fortune and two of the game's months. Where it is open the company is a fifth
  provider (`InternetCatalogue.Own`, set by `BroadbandRules.Apply`), and the player's own places connect free. Each
  district's homes and businesses (`BroadbandCatalogue.Market`) choose among the providers there month by month: each
  has a weight (the rivals' holds as the market stands when the company is founded, so that nothing moves without a
  newcomer; the company's grown with the district's trust over nine months), by its price against its usual squared,
  and the company's also by its reliability (the maintenance spent: outages on twenty days in a thousand as usual) and
  its marketing (each dollar winning less than the last); each share moves a fifth of the way to its weight's share a
  month. The rivals answer: a rival's price is held under its highest by the company's share of its markets
  (`BroadbandRules.AnswerCap`). Each morning the day is booked at the headquarters (`UtilitiesGame.Book`): the customers'
  bills as takings (none on a day the network failed, nor without a headquarters), engineers and the call centre as
  salaries, the network (backbone, boxes and upkeep) as the cost of what it sells, the marketing as marketing; at the
  turn of the month the new connections' installations. `GameBooks.Refresh` enters the takings and the network's row in
  the headquarters' statement, so the game's tax and its bank count them; the running costs are deductible, the capital
  (the licence, the networks dug) is not, and the sale is not taxed. A buyer (Harbour Fibre) pays four years of the last
  twelve months' profit, or three tenths of what the network cost if that is more, and runs the network on at the usual
  prices; the player's places on it pay like anybody's, and the licence goes with it. The checks hold that a district
  pays back its network in about two years, that the market keeps still without a newcomer, that nobody is won in a
  day, and that one squeezed month cannot raise the price of a sale by more than four times its gain.
- **Bills at a business** (power, water, the internet, their fees) are deducted by the game's tax as its own charges
  are (`UtilitiesGame.Pay`, deductible); a home's, and a deposit, are not.

The save line (`reallife:utilities`, version 2): the days added up, the seed, the months and each rival's price, each
account's place (escaped) and figures, and the broadband company (`~n:`, none in a line of version 1). Nothing runs per
frame but the office's panel check once a second; the effects once an hour, the books, outages, prices and the
company's day once a morning.

## Home life and health (`Scripts/Home`)

```
Scripts/Home/
  HomeState      per-save state: the three needs, fitness, sleep debt, five days of meals, the illness, a few dates: one line in modData (game-free)
  HomeBalance    every number (rates per game hour, thresholds, fees, odds), the game's food items by kind, the dishes (game-free)
  HomeCooking    what can be made from a list of what the fridges hold: a plan per dish, what is one unit short (game-free)
  HomeCookList   the cooking list, in the game's list window: the size, the leftovers, the dishes that can be made now, what is one ingredient short
  HomeOffers     "Click to cook" on cookers and "Click to make a coffee" on coffee machines: one entry in the game's list of click actions
  HomeFridgeButton  "Cook a meal" as a button in the game's own panel of a fridge
  HomeDirector   the one component: reads what the player is doing once a second, moves the needs, offers the routines, the test buttons
  HomeHealth     the rules: needs by activity, mood modifiers, fitness, sleep quality, the body, falling ill and getting over it
  HomePlace      HomeTidy (the flat's tidiness = the game's own floor dirt) and HomePlace (cookers, coffee machines, fridges and the food in them, a clear piece of floor)
  HomeActivity   the mod's routines as activities on the game's own panel: a dish from the cooking list, coffee, tidy, floor exercises, the doctor's waiting room
  JogRun         going for a run: a loop of six checkpoints
  HomePhone      the "Wellbeing" contact and its page (HomeReport), the doctor's consultation (DoctorVisit, ClinicDialog)
  HomeNeedsBar   hygiene, stress and home as three more of the game's bars, in a tab under its top bar
```

### What the game already does (and the mod leaves alone)

The base game has a whole activity system (`PlayerActivity`): a panel with a start button, a duration slider, a progress
bar, fast-forward and a time skip, and behind it

- **sleep** in a bed, a car or a boat (`SleepActivity`: energy by the hour, more in a better bed, a happiness modifier);
- **shower, bath, sink and toilet** (`HygieneActivity`: only a happiness modifier, `ba:happinessmodifier_hygiene`; there is no hygiene need);
- **television, computer games, reading a book, a DJ booth** (`EntertainActivity`, seated on the nearest sofa or chair);
- **resting** on chairs, benches, loungers (`RestActivity`);
- **workouts** on fifteen kinds of machine, at home or in a gym (`WorkoutActivity`: treadmill, bench, squat station,
  pull-up bar, boxing bag, kettlebells, gym mat, fit ball, trampoline, outdoor calisthenics), with the sport-clothes
  rule in other people's gyms, a trainer's workout plan, and the gym's entrance fee (there is no membership);
- **swimming, tennis, golf, studying, employer jobs**;
- eating and drinking any item (from the hands, a bag or a fridge: `ItemHelper.TryToConsume`), energy, hunger, happiness
  with timed modifiers, a "wet" modifier in the rain, a "walk in the park" modifier;
- floor dirt and mopping, for businesses (`BuildingCleanliness`, `MopController`);
- the hospital only as where the player wakes after collapsing (2000), and plastic surgery.

It has **no** hygiene, stress or tidiness need, no illness, no fitness or diet, no cooking, no sleep quality, no doctor,
no running, no washing machine and no medicine items. Working out changes nothing on the body.
The player's animator has: `Sitting`, `Laying`, `Showering`, `Bathing`, `UsingSink`, `ReadingABook`, `ConsumingFoodSitting`,
`Cleaning`/`CleaningIdle`, `Squats`, `Crunches`, `CrunchesOnFitBall`, `Boxing`, `Press`, `Running`, `PushUpIdle`,
`KettlebellPull`, `Deadlift`, the calisthenics set (loops); `ConsumeFood`, `ConsumeDrink`, `Drink`, `UsingProducer`,
`StretchArms`, `SweatRemove`, `SquatAction`, `PushUpJump`, `PressAction`, `BoxingPunch`, `Faint` (one-offs).

### What the mod adds

Nothing above is replaced. The director looks at the game's own panel once a second (`ReadDoing`) and takes what the
player is doing from the activity that is on it; the game minutes that went by (the time skip included) move the needs.

- **Needs** (0 to 100, per game hour): *hygiene* -3.3 awake, -1.2 asleep, -30 on a workout machine, -28 running; a
  shower or bath restores it in ten minutes, a sink as far as 70. *Stress* +0.5 awake, +3 on one of the mod's jobs, +2.5
  at an employer job, +1.5 studying, more when ill, starving, short of sleep or in a messy flat; -14 at the television,
  -12 reading, -10 in the shower, -6 resting, -5 asleep, -8 to -14 exercising. *Home*: the tidiness of the player's home,
  which is **the game's own floor dirt** where the home has floor cells (living there puts a little on a third of the
  cells, never more than 60 a cell; the game's own mop takes it off; tidying up takes it off everywhere) and a figure in
  the mod's state where it has none. -1.6 an hour at home awake. The three are shown at home, while hygiene is
  under 35 or stress over 65, and while ill, as three more of the game's own bars (`HomeNeedsBar`, below). "Easy needs" halves what wears a need down and every chance of illness.
- **Consequences** are mood only, each one of the mod's happiness modifiers held on while its cause lasts: Unwashed -6
  (under 25), Stressed -8 (over 75), Relaxed +4 (under 15), The flat is a mess -5 / A tidy home +4 (at home), Short of
  sleep -8 (more than 8 h owed), In good shape +4 (fitness 70), Eating well +4 / Living on junk food -5. Under hygiene
  12, in somebody else's premises, people are said to keep their distance (once in six hours, a small mood loss).
- **Sleep**: quality 0 to 100 from the bed's quality, a car (-25), going to bed between 1 and 6 (-15) or in the daytime
  (-10), a messy flat, stress, hunger, illness, and the hours (7 or more +10, under 5 -20). 68 and up is Well rested
  (+6, 10 h), under 45 Groggy (-5, 6 h). Sleep debt: 7.5 h a day is owed, an hour asleep pays off 0.7 to 1.3 h by quality.
  A morning summary once a day after three hours or more.
- **Routines on the game's panel** (`HomeActivity`: the player walks there, a short watched sequence, then the game's
  own time skip indoors for anything longer than 14 minutes; the stop button, running out of energy and a click
  elsewhere on the way all end it cleanly): *a dish* chosen on the cooking list, from the food in the home's
  fridges (see "Cooking" below); *coffee* at a coffee machine, by a click on it ("Click to make a coffee"; 10 min, 8 energy inside the game's daily limit
  for food and drink, three a day); *tidy up* (10 to 90 min, 1.1 points a minute); *stretching, push-ups, sit-ups*
  anywhere with 0.9 m of clear floor all round (1.0 m for push-ups; 10 to 60 min). The mod's floor clips are centred
  on the character and lie along the way they face (`pushup`: toes 0.79 m behind, crown 0.90 m ahead; `situp`: head
  0.70 m behind, feet 0.60 m ahead), so the player is walked to the middle of that circle of floor and whichever way
  they face the body stays inside it.
- **Diet**: every meal of the last five days is cooked at home, from the fridge at home, bought and eaten out, or
  skipped (fewer than two meals in a day). What a meal was is told from where it was eaten: the game says only that
  something was.
- **Fitness** (0 to 100): 9 an hour on a machine, 11 running, 6 for floor exercises and sports, with the second hour
  of a day worth half the first; -0.7 a day from the third day without exercise. It is stamina: an energy spender of
  +0.006 a game minute at 0 to -0.010 at 100 (the game's own "living" drain is 0.04). **The body** follows on waking,
  0.02 a night at most and 0.35 in all from the body the player made: strength +0.012 an hour of weights or floor
  exercise, fatness -0.008 an active hour, +0.004 a day of bad food and no exercise, -0.003 a day of good food.
- **Illness** (one at a time, none for three days after any; `HomeHealth.Catch`): a cold (36 to 60 h: an hour in the
  rain without an umbrella 30 %, short of sleep 6 % a day, unwashed 5 % a day), the flu (72 to 108 h: 1.5 % a day,
  doubled by a poor diet and again by sleep debt), food poisoning (16 to 30 h: a burnt meal 12 %, cooking in a filthy
  flat 8 %), back pain (40 to 64 h: an hour's exercise in a day at fitness under 20, 15 %; a night in the car 12 %),
  burnout (72 to 120 h: stress at 90 for thirty hours on end). The first three are Life's own sickness
  (`LifeEvents.MakeSick`: its modifier, its drain, its doctor on the phone, the hospital) with a name and a course, and a
  sickness a life event gives is taken up as a cold. A night of six hours or more takes six hours off; stretching eases
  a bad back; a prescription halves what is left and the extra drain.
- **The doctor**: in a hospital or clinic, within 7 m of the desk of the game's own doctor (a
  `SpecialEmployeeController`; where the building has none, of the place the player came in), the game's panel over a
  place (`GameUi.WorldPanel`) offers a consultation (80) on the game's interact key: a wait of 20 to 35 minutes
  on the game's panel, then the conversation: the diagnosis, a prescription (45), a sick note (20: for 48 h a failed job
  shift is no strike, `HomeHealth.SickNoteActive`), or a check-up with advice for someone well.
- **A run**: from the Wellbeing contact, out of doors on foot: six checkpoints 55 m apart on the pavement in a loop
  back to the start (34 m and a quarter more gain where the game marks a park), the game's own destination marker
  (`GameUi.GuideMark`, named "Run: checkpoint 2 of 6") and a ring at the next one.
  Only the minutes spent actually running count. Rain: a fifth less, and soaked.
- **The Wellbeing contact**: "How am I doing" (needs, illness, fitness and its trend, diet, sleep, one tip), and from
  it tidying, the exercises and the run.

Left out on purpose: laundry (the game has no machine), pharmacy items (it has none), a gym membership (the game
charges an entrance fee), a timing game on the game's workout machines (the game's own activity runs them).

### Cooking

**Why a routine "offered" in 0.1.0 and 0.1.1 never ran.** A routine is one of the game's activities on the game's own
panel. When its start button is pressed the routine is "moving towards the activity", and the game's panel then
listens for any new walk order: `PlayerActivityUI.CancelActivityMovement` hangs on
`PlayerController.PlayerChangedNavigation` and, once `_allowCancelMoveTowards` is set, takes a walk order for the
player clicking somewhere else: it stops the walk, puts the activity back to "not started", forgets it and closes,
without a message. That flag is set at the end of the frame in which the panel first sees the routine on its way
(`CoroutineUtility.RunAfterFrameDelay(..., 1)` is one `WaitForEndOfFrame`), which is the frame of the press or the one
after. The game's own activities give their walk order at the end of the frame of the press, before the flag. The
mod gave its own (`player.SetGoal`, which ends in `SetNewDestination` and `PlayerChangedNavigation.Invoke()`) two
frames after the press, so its own first step was taken for a click elsewhere and every routine with a step to walk
called itself off: cooking and coffee always (the furniture's standing place is never where the player stands),
tidying and the floor exercises whenever the clear floor was not under their feet. In `Player.log` that is
"Home: CookActivity offered." and then nothing.

Now (`HomeActivity.Start` / `SetOff`): the walk order is given at the end of the frame of the start, as the game's
are. If the panel drops the routine all the same, the routine is put on the panel once more and its walk order is
given at once, before the panel can have seen it on its way; a second failure is a toast. Every other way a start
can come to nothing says so in one sentence: no way there on foot (the game's own "cannot reach"), the game would
not take a walk order, the walk never began (a movement key held down), the furniture in use, the food gone from
the fridge meanwhile, the game's panel busy, 20 seconds without the player being free.

**The game's food** (read from its item bundle, `defaultlocalgroup_assets_items`: 791 items; every goods item has
`saturation`, 30 unless set, batteries and glass included, and the fridge takes anything with a saturation, so
food is a list of ids, `HomeBalance.Foods`; anything else in a fridge is ignored):

| kind | the game's items (`ba:itemname_...`), saturation |
|---|---|
| meat | `groundbeef` 30, `chickenbreast` 30, `rawsausage` 30 |
| vegetables | `carrot` 20, `lettuce` 15, `tomato` 20, `onion` 30, `slicedonions` 30, `russetpotatoes` 30 |
| dairy | `milk` 30, `butter` 30, `cheese` 30 |
| grain | `dough` 30, `bakingmix` 30, `barley` 30 |
| fruit | `apple` 30, `banana` 40, `pear` 30, `grapes` 30 |
| fresh | `freshfood` 100 |
| frozen | `frozenfood` 25 |
| ready meals | `burger` 70, `hotdog` 60, `kabob` 90, `pizza` 100, `frenchfries` 50, `salad` 60, `cheeseplatter` 30 |
| snacks (never cooked) | `croissant` 25, `cupcake` 25, `donut` 25, `cottoncandy` 30, `popcorn` 30, `icecream` 50 |
| drinks (never cooked) | `beer`, `bottleofwine`, `margarita`, `martini`, `whisky` 25; `cupofcoffee`, `cupoftea`, `slushi` 10; `energydrink` 10; `sodacan` 5 |

**The dishes** (`HomeBalance.Dishes`; one batch; a portion fills the saturation of the items used, times the yield,
shared between the portions, 100 at most):

| dish | takes | cooker | portions | minutes | yield | burns |
|---|---|---|---|---|---|---|
| Fruit bowl | 2 fruit | no | 1 | 5 | 1.2 | never |
| Salad | 2 vegetables | no | 1 | 10 | 1.4 | never |
| Cheese and fruit plate | 1 dairy, 1 fruit | no | 1 | 5 | 1.3 | never |
| Heated-up meal | 1 ready meal | yes | 1 | 10 | 1.25 | 3 % |
| Frozen meal, cooked | 1 frozen | yes | 1 | 20 | 2.6 | 5 % |
| Fry-up | 1 meat, 1 vegetable | yes | 1 | 20 | 1.5 | 6 % |
| Pancakes | 1 grain, 1 dairy | yes | 1 | 15 | 1.4 | 7 % |
| Home-cooked dinner | 1 fresh | yes | 2 | 45 | 1.5 | 8 % |
| Vegetable soup | 3 vegetables | yes | 2 | 35 | 1.7 | 5 % |
| Home-made pizza | 1 grain, 1 dairy, 1 vegetable | yes | 2 | 40 | 1.8 | 9 % |
| Roast dinner | 1 fresh, 1 meat, 1 vegetable | yes | 3 | 70 | 1.5 | 10 % |
| Stew | 2 meat, 3 vegetables | yes | 3 | 80 | 1.6 | 12 % |

Sizes: normal, double, family size are 1, 2 and 4 batches; the time is 1, 1.25 and 1.6 times (to the nearest 5
minutes), the chance of burning +0, +1 and +3 points, the mess 1, 1.2 and 1.5 times. Stressed (over 75) or
exhausted (energy under 20) adds 8 points to the chance of burning; "easy needs" halves it. Burnt: the portion
eaten fills 0.6 as much, nothing is put by, a mood loss, and food poisoning 12 %. Leftovers: at most 8 portions,
one lot that carries the name of the dish cooked last and the average of what its portions fill
(`HomeState.PutBy`; `gro`, `grd`, `grf` in the save's line); reheating one takes 8 minutes at a cooker and never burns.

**What the player does.** At home, a click on a cooker (an item whose name has `stove`, `pizzaoven` or `grill` in
it: the game offers "Click to cook", `HomeOffers`) or the button "Cook a meal" in the game's own panel of a fridge
(`HomeFridgeButton`) opens the cooking list (`HomeCookList`: the window the game lists its subway stations in,
`GameUi.TravelList`, 1,100 wide with entries of 150; the mod's camera frees the cursor while it is open, and the
game's cancel key or the yellow button closes it). The fridge's own use (the game's panel on a click) is not
touched. The list reads the fridges once (`HomePlace.ReadPantry`) and has: an entry for the size, whose button steps
through normal, double and family size; the leftovers; one entry per dish in the largest size up to the chosen one
that the fridges allow, with its items by the game's own names and counts (the most filling of a kind first); an
entry with its button off for a dish of two or more items that lacks one unit, saying which; and, under the
headline, what the fridges hold and a note when there is no fridge, nothing in the fridges, nothing that makes a
dish, or no cooker (then only what needs no cooking is listed). Choosing a dish closes the list; as soon as the player is free the dish
goes on the game's activity panel and is started for them two frames later (the game's panel has to see it waiting
once), they walk to the nearest cooker (to the nearest fridge for what needs no cooking), the items leave the
fridges on arrival (`HomePlace.Take`, all or nothing), `cook_stir` with `pan` plays at a cooker (the game's
`UsingProducer` otherwise), the game's time skip takes anything over 14 minutes, and at the end one portion is eaten
through `GameManager.ChangeHunger` and the rest are put by. With hunger at 100 nothing is eaten and every portion
is put by. Stopping early wastes the food.

`IHomeBridge.CookTogether` has no implementation in Home (`FamilyBridges.Home` is never set), so Family's "cook
together" is its own short scene, as before.

**The fridge's button.** The game's panel for a fridge is the object `FridgeDetailedOverlay`
(`DecorativeItemHolderOverlay`): a vertical list with one button per kind of item, `EmptyFridgeButton`,
`ChangeClothesButton` (a wardrobe's) and `AddItemButton`. The mod's button is a copy of `ChangeClothesButton` (the
blue one), put under the items once per city. A component of the mod's on that list is enabled each time the game
switches the panel on (it fills the panel first), and shows the button if the panel's `linkedController` is a
fridge of the home the player is in. If the game's window for the list cannot be copied, a click cooks the first
dish the fridges allow.

**The needs bars** (`HomeNeedsBar`). The game's energy, hunger and happiness are three objects of 412 x 56 in
`Topbar/Container/Left Bar/PlayerStats`, each the sprite `StatBarBackground` with a filled copy of it over it and a
picture of 32; under the same bar hangs the accessories tab (`AccessoriesPanel`, 100 high). The mod's three are a
copy of that tab holding a copy of `PlayerStats`, hung under the game's bars (beside the accessories tab while that
is open: its place is measured), with their own colours, pictures and a word each (the game's bars have no word;
three new ones without would have to be guessed). The copy is a child of the game's top bar and goes with it. A bar
is written when its whole number changes, at most once a second; the illness and how long it has to run is a line
beside the bars in the game's yellow.

Test buttons: "Test: open the cooking list" and "Test: cook the first dish the fridges allow". Pressed on the
options page, both wait up to 20 seconds for the menus to be closed. `Player.log` gets
"Home: cooking list opened: 1 fridge(s), 1 cooker(s); food by kind: Meat 2 Vegetable 3; size 1; 3 dish(es) possible,
1 one short, 0 wanting a cooker, leftovers 0." and a line when a dish is done. With "Developer diagnostics" on:
`Home check (offer)` when the click actions and the fridge's button are in and for each piece of furniture marked,
`(cooking list)` when the list opens (what it holds, the time it took) and closes (with why), `(choice)` for a
click on a cooker, a coffee machine or the fridge's button, for the size and for the dish, `(needs bars)` when the
bars are built, shown and hidden, `(doctor panel)` when the game's panel comes up or goes, `(run)` per checkpoint.

Not verified in the game (it could not be run): how the list reads at 1,100 wide with entries of three lines; how
the tab of three bars looks under the top bar and beside the accessories tab, and which of the game's pictures the
bars got; that the fridge's button lays out under the items; that a
routine now always sets off (the order of two end-of-frame coroutines started in the same frame is assumed to be
the order they were started in, which the game's own activities rely on too; the second attempt does not depend on
it); the look of `cook_stir` at each kind of cooker; that a fridge's standing place suits a routine; that raw
ingredients (meat, dough, milk) can be bought by the player in every save, since they are wholesale goods.

### Asset wish-list

`cook_stir` (with `pan`), `pushup`, `situp`, `stretch` and `run_checkpoint` are **delivered and played** by id; without the bundle the game's own animation (or the plain shape) is used. `eat_sit` and `drink_stand` are not asked for yet: eating and drinking are the game's own gestures, played standing once the routine has let go of the player.

| clip id | length | loop | what | played today by |
|---|---|---|---|---|
| `cook_stir` | 3.0 s | loop | standing at a hob 0.45 m ahead, 0.92 m up: left hand on a pan handle, right stirring; holds `pan` (right + other hand) | the game's `UsingProducer` every 4 s |
| `pushup` | 2.0 s | loop | push-ups, hands under the shoulders, body straight; **as delivered the body is centred on the root** (toes 0.79 m behind, crown 0.90 m ahead), which is what the clear floor is measured round | the game's `PushUpIdle` + `PushUpJump` |
| `situp` | 2.4 s | loop | sit-ups, knees bent, hands at the temples | the game's `Crunches` |
| `stretch` | 10 s | loop | standing stretches: arms overhead, side bends, a forward fold; needs 0.8 m either side | `tai_chi`, else the game's `StretchArms` |
| `eat_sit` | 4.0 s | loop | seated at a table 0.75 m high, fork to mouth from a `plate` 0.35 m ahead | the game's `ConsumeFood` standing |
| `drink_stand` | 3.0 s | once | a mug lifted to the mouth and lowered; holds `mug` (right) | the game's `ConsumeDrink` |
| `shower` , `sofa_sit_tv`, `read_sit`, `treadmill_run`, `weights_curl` | | | not needed: the game's own `Showering`, `Sitting`, `ReadingABook`, `Running`, `KettlebellPull` are what its activities play | |

| prop id | size | pivot | for |
|---|---|---|---|
| `pan` | 0.26 across, 0.05 deep, handle 0.18 | centre of the base; handle along +X | `cook_stir` |
| `plate` | 0.26 across | centre of the base | `eat_sit` |
| `mug` | 0.08 x 0.10 | centre of the base; handle at +X | `drink_stand` |
| `run_checkpoint` | 1.6 across, 1.4 high | ground centre | the run's marker (a plain disc and post today) |
## Travel and lodging

`Scripts/Travel/`: hotels the player can stay in, and holidays bought from the game's travel agencies.

```
TravelCatalogue  every number and name: rooms, the eight hotel slots, trips, events, prices (game-free)
TravelState      what a save remembers, one line in modData under "reallife:travel"; the no-repeat dealing (game-free)
TravelBridges    the doors to Home, Social, Law, Transport and Leisure, each with a working default
TravelEffects    money, hunger, energy and travel's own happiness modifiers, through the game's systems
TravelText       lines from en.json with their {names} filled
Hotels           which buildings are hotels; sign, awning, luggage cart, the one doorman, the city-map icons
HotelStay        the room as a list, sleep through the game's time skip, checkout; HotelReception is the desk's list
Holidays         agencies, the agent, tickets, the trip as a list, the Hamptons weekend, burnout, the trade fair
TravelPhone      the "Trips" contact (the page) and the agency's own number
TravelDirector   the component: save and load, ticks, the game's panel at a door, the watchdog, the test buttons
```

### What the game has

- **No hotels.** Residential buildings cannot be entered unless rented (`cantenterunlessrented`); pressing the door of
  one opens the rent screen or says "private property".
- **Travel agencies** are an office business type (`ba:businesstype_travelagency`, skill `ba:skill_travelagent`) that
  the player or the AI can run. Its one product is `ba:itemname_hourlytravelconsultantfee`, sold to simulated customers
  only (`spawnCustomers` is off): the player can never buy anything there. The mod's agent is met at such a building's
  door, whoever owns it, while it is open.
- **The Hamptons** (`ba:neighborhood_thehamptons`) is part of the one city scene, reached by road and by its own
  subway station (`TheHamptonsStation`). Its houses are size `t` plots that rent for 4,768 a day; its shops are on
  its own Seventh Avenue; there is a theater at Ocean Crest Road 1 and a cinema at Harbor Street 1. There is an
  Airport Avenue (one warehouse) but no airport building, no flights and no terminal anywhere.
- **The casino boat** (`CasinoBoatManager`, the building at Pier 5) is in harbour on Fridays from 18:00 to 22:00. A
  ticket from the ticket house plays the game's own boarding sequence, skips to midnight and puts the player in the
  casino interior; at 05:00 on Saturday they are put ashore. So the player already rides it, on the game's terms
  and timetable only. Travel does not touch it: the mod's cruise is a different ship, played in cards.
- **Being away.** Nothing in the game needs the player present except their own two hands: businesses trade through
  their employees' shifts, and every tick of the time skip (`TimeMachine` runs `GameManager.RunMainGameTick`) is a
  full tick of the simulation. Stock that runs out stays out unless a delivery contract or a logistics manager
  refills it, to-do tasks pile up (`BuildingRegistration.Alerts`), rent and wages are charged daily. The daily
  accounts are `GameInstance.financialSummaries` (`totalBusinessProfit` and one statement per business).
- **Sleep** is `EnergyHelper.SetCurrentEnergyRegen` (bed 0.35 energy a minute, car 0.30, bench 0.25) times
  `energyRegenMultiplier` (the default plus a bed's quality / 100), run by the time skip.

### Prices

Set against the game's own figures, read from its data files: the smallest apartment (54 m2) rents for 31 a day in
Industry City, 34-39 in Lower Manhattan, 40-48 in the Garment District, 55-63 in Murray Hill, 79-95 in Hell's
Kitchen and 134-138 in Midtown; 96 m2 for 54-261; 204 m2 for 115-529; 589 m2 for 364-1,455. The cashier job pays 25
an hour; staff start at 12 to 18.

| room | a night | sleep | rough night | refund of unused prepaid nights |
|---|---|---|---|---|
| hostel: bed in a dorm | 45 | bench rate, multiplier -0.1 | 45 % | none |
| hostel: private single | 75 | car-seat rate | 25 % | none |
| mid-range: standard double | 150 | bed rate | 5 % | 50 % |
| mid-range: deluxe | 230 | bed rate, +0.2 | never | 50 % |
| luxury: king room | 480 | bed rate, +0.4, comfort modifier (+6 for 24 h) | never | 70 % |
| luxury: corner suite | 900 | bed rate, +0.6, comfort modifier | never | 70 % |
| cottages: bedroom / whole cottage | 210 / 380 | bed rate, +0.15 / +0.35 | never | 50 % |

Times the hotel's own factor (Industry City and Lower Manhattan 0.85, Murray Hill 0.95, Midtown 1.2), times 1.2 on
Friday and Saturday nights, times the season (the game counts days only, so travel gives the year four seasons of 28
days starting in spring: city hotels 1.15 in summer and 0.9 in winter, the cottages 1.4 and 0.75), times the Leisure
bridge's demand for the day. Seven nights or more cost 0.8 of that every night, fourteen or more 0.72. On deposit the
first night is paid at the desk and the rest, 5 % dearer, at checkout. Late checkout (16:00 instead of 11:00) is 30 %
of a night, free in a luxury room. Eight stamps (one a night) are a free night worth up to 250. A hostel bed costs
what the cheapest apartment rents for and sleeps worse, so it is a stopgap, never the better deal.

Extras: vending sandwich 8 (+25 hunger), room service 32 (+60) or 65 in a luxury room (full), cottage hamper 28,
minibar 12 or 18 (+12 hunger, +3 energy, four a day), laundry 8 / 25 / free, dinner through the concierge 85, dinner
in the village 60. Nothing pays the player anything, and every effect is capped (hunger at full, one mood a day).

| trip | days | economy | first class | splurge a day | dear / cheap season |
|---|---|---|---|---|---|
| Weekend in the Hamptons (real) | 2 nights | 520 | 910 | - | summer / winter |
| Coast cruise | 3 | 1,500 | 2,850 | 160 | summer / autumn |
| Ski trip | 4 | 2,900 | 5,220 | 220 | winter / summer |
| Miami beach week | 5 | 2,400 | 4,320 | 180 | winter / summer |
| Europe tour (jet lag) | 7 | 5,600 | 9,800 | 300 | summer / winter |
| Trade fair (by invitation) | 2 | 900 | 1,440 | 140 | autumn / summer |

Dear season x1.25, cheap x0.85. A companion adds 70 %, insurance 8 %. A suitcase is 120. A trip falls through at the
gate 6 % of the time: all of it back with insurance, 40 % without. A ticket is good for seven days; handed back or
expired it returns everything with insurance and half without.

### Hotels

Eight places (`TravelCatalogue.Hotels`). A city hotel is the residential building of its district, of the sizes its
slot names, with the highest traffic index (ties by street name, then number) that the player neither rents nor owns:
static data only, so it is the same building in every session, and a booking remembers the slot, not the address.
With the game's data as shipped: the Bobbin Hostel at Fourth Avenue 1 (Garment District), Dockside Bunks at
Twenty-first Street 23 (Industry City), Hudson Court Hotel at Second Avenue 8 (Hell's Kitchen), the Battery Row at
Fourth Avenue 45 (Lower Manhattan), Murray Hill Arms at Sixth Avenue 18, the Broadway Grand at Broadway 13 (Midtown),
the Meridian Tower at Second Avenue 14 (Hell's Kitchen). Seabreeze Cottages is a board by the Hamptons theater
(Ocean Crest Road 1; failing that the cinema, then Seventh Avenue 5). The log line "Travel: hotels:" lists what was
picked.

At a hotel's door the game's own panel over a place (`GameUi.WorldPanel`, through `GameUi.DoorPanel`) names the hotel and
says what is on offer: "Reception" on the interact key (off while a room is held), and "Go up to your room" on the
secondary key when the room is booked there. The mod's own door key stands aside while it is up. Reception is a
`GameUi.TravelList` (`HotelReception`): page one has an entry per room type with tonight's rate, the week's rate and
how many are left; a room goes to page two (Back, an entry that steps the nights, Pay now, Leave a deposit). The result
is a notification, and a booked room is gone up to at once. The room is a list as well (`HotelStay`): an entry per
choice with its cost or effect under the name and the button off, with the reason, when it cannot be done (sleep, nap,
wake-up call, shower, room service or the vending machine, minibar, laundry, rest, messages, the concierge (luxury),
a beach walk and dinner in the village (cottages), extend, late checkout, check out, go out). Sleep, nap and rest
keep the game's own time skip; that hides the HUD, so the list is put away while it runs and the skip's own Cancel button is the way to get up. What a
choice did is a notification. The player never moves: they stand at the door, held by a navigation block of the
game's own. The list is one window shared by reception, the room and a trip (`TravelDirector.Window`, a `DeskList`).
The mark that points to a hotel is the game's destination marker (`GameUi.GuideMark`).

If the game's list cannot be copied (one "Game UI:" line says so): reception is the game's dialogue panel as before,
the room sleeps the night at once and puts the player outside, and a trip is played through taking it easy each day.

Log lines (with Developer diagnostics on): "Travel check (door panel): ...: PASS", "Travel check (reception list): the
game's list is open: ...: PASS", "Travel check (room list): ...", "Travel check (trip list): ...", "Travel check (choice): ...".

Not checked in the game: any of the above. The wording of the panel and the list, how the game's list looks with 20
entries of two lines, whether its tall entries (a trip's text) fit, whether Cancel closes the room list as meant, and
that the marker takes the guider when no job has it.

### Holidays

At an open travel agency's door, or by phone once one has been visited (from the start in a city with no agency).
The Hamptons weekend is real: setting off books the cottage from that night and the player travels there themselves;
a night slept there makes it a holiday. The others are played in the same list: departure, a page a day (take it easy,
explore, splurge; each with an event from a pool of 72 that does not repeat), a day of game time skipped after each,
and a return page that reads what the businesses made from the game's accounts. Before booking the agent says what
is being left: how many businesses, what they made a day over the last week, open problems, the rent that carries on.

Burnout: every day with a business to run or a job taken counts; after 18 a friend says so, after 24 a mild
modifier (-6) stays until a holiday is taken (an afternoon's rest in a hotel room takes three days off the count).

### Bridges

Everything other features do for travel goes through `TravelBridges`; each is taken over with one line and handed back
by setting null:

```csharp
TravelBridges.Home = new MyHome();            // ITravelHome: Freshen, Laundry, Rest, Holiday, HighStressDays, HasBed
TravelBridges.Social = new MySocial();        // ITravelSocial: Companions, TripTogether, Souvenirs, TableBooked, MissedWhileAway
TravelBridges.Justice = new MyJustice();      // ITravelJustice: MayLeaveTown, MissedWhileAway
TravelBridges.Transport = new MyTransport();  // ITravelTransport: CoachToHamptons, RideCoachToHamptons, CallCar
TravelBridges.Leisure = new MyLeisure();      // ITravelLeisure: PlayGolf, DinnerOut, DemandToday
TravelDirector.SuggestHotel("reallife_travel_suggest_locked_out");   // locked out, evicted: where the nearest bed is
```

Without them: freshening up and laundry are a small mood once a day, rest is the relaxed modifier, the one companion
is the mod's Friend contact, souvenirs go to Friend and Mom (a thank-you text and a mood each), nobody is refused
travel, there is no coach, and the concierge's car and the golf button are not shown.

### Wish-list for the asset pipeline

Plain shapes stand in until these ids are in `Bundles/reallife_props`; the code asks `PropLibrary` first, so no code
changes when they arrive.

| id | size (w x h x d) | pivot | notes |
|---|---|---|---|
| `hotel_sign` | 0.70 x 1.90 x 0.16 | ground centre, face to +Z | pavement sign against a wall; leave a plain plate 0.60 x 0.34 centred 1.50 m up on the +Z face (0.085 m in front of the pivot plane): the hotel's name is lettered there in code. As built the plate's face is at z 0.080 with nothing in front of it, so the lettering at 0.085 is 5 mm proud and does not flicker; its box is 0.56 x 0.30, 2 cm inside the plate all round |
| `hotel_awning` | 2.40 x 0.45 x 1.40 | the middle of its wall edge, at its underside | reaches out along +Z; hung 2.75 m up on a wall measured flat; one neutral colour (four tiers share it) or light canvas |
| `luggage_cart` | 1.10 x 1.75 x 0.60 | ground centre, long side along X | brass birdcage trolley with two cases and a bag on it |
| `suitcase` | 0.45 x 0.65 x 0.25 | ground centre, handle up | hard-shell case; and a grip for `carry_bag_walk` (right hand) so it can be carried: not used by code yet |

### Not done

- The suitcase is bought and changes what trips bring home, but is never seen: no prop in the hand on the way to the
  cab (`HeldTool` with `carry_bag_walk` needs the modelled case and its grip).
- No postcards or photographs are saved.
- The trade fair gives a mood only (+4 for 48 h): no supplier discount or marketing boost, because no mechanism of the
  game's was found that could be given one safely.
- Golf in the Hamptons and a coach there exist only through the Leisure and Transport bridges.
- The Trips page is a phone contact with pages, not an app icon on the home screen.
## Driving licences

Everything is under `Scripts/Licences/` (and its `Driving/`). Nothing unknown to the base game goes into a
save: licences and points live in the save's `modData` as one plain line (`reallife:licences`), and the driving
school's car is taken off the save's vehicle list for the length of each serialisation (`TestCar.BeforeSave`).

```
Licences/
  Licences          the one door for the rest of the mod: Licences.Driving.Has(LicenceClass), Licences.Law (ILawBridge), listeners
  LicenceState      classes, validity, points, suspension, retests: game-free rules and the modData line (LicenceLaw has every figure)
  LicenceDirector   loads/saves, the first-run grant, the desks' panel, the one test that is on, enforcement's tick, the test buttons
  SchoolDesk        the game's panel at a desk and the school's list (services, the licence card as a second page)
  SchoolScreens     the school's one list window (a GameUi.DeskList) and the plain report page
  LicenceProps      the things stood in the world, by id: the modelled prop if the bundle has it, else a plain-shape stand-in
  TheoryTest        a written test as a question card in the list: no repeats, a question from every subject, three answers and a way to give up
  Driving/
    DrivingSchool      what the school offers and when (DrivingSchool), the desks at the school buildings (LicenceDesks),
                       the Licensing Office contact (LicencePhone) and the conversation both use (DrivingSchoolDialog)
    DrivingQuestions   the 66 theory questions in seven subjects
    PracticalTest      a lesson or test, driven: examiner, checkpoints, what is marked, the bay, the result page
    TestRoute          five to seven checkpoints along real lanes and a marked bay at the kerb
    TestCar            the school's car: made the game's way, never in a save, the player seen out of it when the drive ends (and again if they get back in), taken back with the game's own delete
    DrivingEnforcement penalty points, the licence asked for at a stop, impound, arrest, the routine roadside check
```

### Driving licence: the rules

| class | covers | how it is got |
|---|---|---|
| Car | every car, SUV and pickup (`CarKind` Economy, Saloon, Luxury, Sports, Utility) | theory test (60; 10 of 66 questions, 8 to pass, a pass counts 30 days), one lesson (90), the driven test (220) |
| Commercial | vans and trucks (`CarKind.Van`, `CarKind.Truck`) | a valid car licence, one van lesson (90), the driven test in the school's van with the bay reversed into (380) |
| Taxi endorsement | carrying passengers for hire | a car licence held 7 days with at most 5 points, 180 |
| Motorcycle | every motorbike of the Garage (`Licences.Driving.TwoWheelerOf` says what a vehicle of the save is; a bicycle is none) | stands alone: the theory test, then 180 at the desk for the ride assessment. The school has no motorbike to lend, so nothing is driven: the assessment is recorded as passed at the desk (`SchoolService.Motorcycle`) |
| none needed | bicycle, electric scooter, hand truck, flatbed trolley, anything without an engine (`CarKind.Untouched`) | |

A licence runs 120 days; it can be renewed (90 each, at a desk or by phone) from 14 days before it runs out until 30
days after, and after that it is the tests again. A failed driven test can be sat again from the next day for 120.
The first time the rules run on a save, a player who owns a car is given a car licence (and a commercial one if
they own a van or truck, and a motorcycle one if the Garage lists a motorbike they own: `MotorcycleDecided` is saved with the line `moto=issued,expires,decided`, absent in an older save, and asked of `Licences.Driving.OwnsMotorbike` once a second until the Garage has read its list); a save that owns nothing starts without one unless "Start with a driving licence" is on.
With "Driving needs a licence" off, `Licences.Driving.Has` is always true and nothing is policed.

Driving unlicensed is possible and is never discovered by magic: only when police actually stop the player
(`PoliceAttention.Stopped`, `PolicePursuit.CaughtAfterFleeing`, or the routine roadside check: about once in forty
minutes at the wheel, never within two days of the last, by a patrol car that comes up behind with its lights on;
driving on from one is not chased but costs 400 and 3 points at the next stop).

| found at a stop | fine | vehicle | further |
|---|---|---|---|
| no licence | 800 | impounded: towed to a repair-shop bay by the game's own tow, the city pound's fee (300, `LawBook.ImpoundFee`) | the third time in 30 days: arrest (6 h, `ILawBridge.Custody`) |
| a licence for another class (a car licence on a motorbike or a van, a motorcycle licence in a car) | 500 | impounded | as above |
| expired, still renewable | 250 | stays | |
| suspended | 1500 | impounded | the second time: arrest |

A hired or job van is not impounded (it is not the player's): it "goes back to its owner". The school's car on a
lesson or test is lawful and is never checked.

| booked offence | points | | booked offence | points |
|---|---|---|---|---|
| red light | 3 | | failing to stop (fleeing) | 6 |
| speeding | 3 | | caught after leaving a collision | 8 |
| driving on the pavement | 4 | | a person injured (each, up to three) | 4 |
| against the traffic | 4 | | a death | 9 |
| drove on from a roadside check | 3 | | | |

Points count for 21 days. Twelve suspend every licence for 7 days, after which the record starts from zero.


### The school in the game's own interface

- Desk: the game's panel over a place (`GameUi.WorldPanel` through `GameUi.DoorPanel`) says whether the school is open
  and has "Speak to the instructor" on the interact key. The list (`SchoolDesk`, a `GameUi.TravelList` on the shared
  `SchoolScreens.Window`) has an entry per service with the fee and what is needed under its name and its button off,
  with the reason, when it cannot be had (closed hours, the fee, a test still to pass, a wait after a failure). The
  texts are the dialogue's own pitch lines. Renewals, the taxi endorsement and the motorcycle assessment are done at
  once and the list stays; a test or a lesson closes it and starts as before. The licence card is a second page of
  the same list (a note per licence, one for the points, one for a suspension, Back): a card is short facts with
  names, which is what the list's entries are.
- Written test: a question card. The headline is the test, the first info line the question, the second "Question 4
  of 12"; three entries "Answer 1..3" carry the answers; "Give up the test" counts as a failed paper (new). The list
  is `Locked`. An answer is a notification; the end is a page with the score and Close.
- Driven test and lesson: a copy of the game's job panel (`Jobs/ShiftCard`, reused as it is) with the instruction,
  the checkpoints passed, the faults and the time left, written five times a second only when a line changes; the
  next checkpoint is the game's destination marker (`GameUi.GuideMark`); at the bay the panel over a place has "I have
  parked" on the interact key; the result is a report page with Close. The school's car logic is unchanged.
- Fallbacks: no window, no written test (the fee is returned), the desk speaks through the dialogue panel, results are
  notifications. No job panel: each instruction is a notification.
- Log lines: "Licences check (desk panel)", "(desk list)", "(theory): question 4 of 12 ...: PASS", "(report)", "(choice)".
- Not checked in the game: any of it.
### The driven test

The school's car (a Honza Mimic; a Mersaidi Dash for the commercial class) is put at the roadside near the desk, the
examiner walks out from the desk and gets in when the player is at the wheel, and the route is driven to a marked bay.
Marked ten times a second from the car's body against `Crime/RoadMap`: crossing a stop line on red (serious),
stopping past it (minor), more than 10 km/h over the lane's limit for two seconds (minor) or 30 over (serious),
out of the lane for three seconds (minor), on the pavement or against the traffic (serious), a stop harder than
9 m/s² from speed (minor), a touch (minor) or a collision of 2.5 m/s or more (serious), and the parking: inside
the box and within 12 degrees (else minor; more than 0.6 m over or 25 degrees off is serious), reversed into for
the commercial class. Pass: no serious fault and at most six minor ones. A lesson is the same drive with each
fault said as it happens. Where no kerb within 150 m has room for a bay the test ends with a stop at a marker.
Signalling and stop signs are not modelled (the road data has traffic lights only).

### Bridges and hooks

- `Licences.Law` (`ILicenceLawBridge`): `Record(offenceId, arrested)`, `Custody(hours)`. `DefaultLaw` records
  nothing and uses `PoliceAttention.TakeIntoCustody`. A law feature sets
  `Licences.Law = itsBridge`.
- **For Jobs (not applied, Jobs is not ours to edit):** in `JobDirector.Available`, for the ride-hail job,
  `if (!Licences.Driving.Has(LicenceClass.Taxi)) { reasonKey = "reallife_job_no_taxi_licence"; return false; }`
  and for any driving job `Licences.Driving.MayDrive(car)`.
- Added elsewhere, all additive: `PoliceAttention.Stopped` (event, raised in `Ticket` and `Bust`) and
  `PoliceAttention.TakeIntoCustody(hours)`; `PolicePursuit.PeopleCharged` (event, raised in `ChargeForPeople`);
  `NearbyInteraction` stands aside while `LicenceDirector.TakesInteractKey`; two `TestRefusal` values (`OnJob`,
  `NotOnFoot`); settings, options and registration in `Core`.

### Asset wish-list (drops in by id, no code change)

| id | what | size, pivot |
|---|---|---|
| prop `driving_school_sign` | a sandwich board with a learner plate (white square, red L) on both faces | 0.62 wide, 1.05 tall, 0.30 deep; origin on the ground at its middle, faces along +Z |
| prop `test_checkpoint_beacon` | a slim marker post with a small flag | 0.22 across, 2.9 tall; origin on the ground |

Both are in the prop bundle now, and the code fits them as built:

- `driving_school_sign` came out 0.52 deep (feet at z +-0.26), not 0.30. Its middle stands 0.55 m from the wall
  (`DrivingSchool.FromWall`), so it reaches from 0.29 to 0.81 m out; the spot is tested clear to 1.6 m out, and
  the desk's place is 0.6 m beyond that. Nothing to change.
- `test_checkpoint_beacon` flies its flag to its own +X and shows it along +Z and -Z. `PracticalTest.Beacon` turns
  it so that a face looks back down the lane at the driver and the flag points in at the checkpoint (it stood
  unturned, the flag to wherever the world's +X was).
- `parking_box_marker` and `parking_box_posts` are in the bundle and deliberately not used: they are one fixed bay
  of 2.40 x 5.50 m, and the bay that is judged is sized to each route and vehicle (`TestRoute.BayHalfWidth`,
  `BayHalfLength`; `LicenceProps.SizeBay`). Drawing a box of another size than the one that is marked would be a
  lie, and scaling the posts' mesh makes their poles oval. The code-drawn bay stays.

### Firearms (removed in 0.37)

Firearms (licences, gun shops, a range, carrying, shootouts) were part of the mod until 0.37 and were removed.
What remains is `LicenceDirector.ForgetFirearms`, which once, on loading an old save, clears its firearms line
(`reallife:firearms` in `modData`) and the two phone contacts `reallife-firearms-unit` and `reallife-gunshop`,
and the two locale keys of those names that it needs; plus the three `reallife_transaction_gun*` labels, kept so
that old bank entries still have a name.

## The Planner (`Scripts/Planner`) and the game's own interface (`Scripts/Core/GameUi`)

The Planner is an app on the game's phone: what the player has to do today and in the next seven days, from the
mod's features and from the game, with reminders and the player's own notes. Every object it shows is a copy of one
of the game's, made while the game runs. How the game's phone, tables, dialogs and click actions are built, which
objects are copied, and what is proven and what inferred is in [docs/game-ui.md](docs/game-ui.md): read it before
adding anything to the interface. **New interface is built this way, not on a canvas of the mod's own.**

```
Core/GameUi/
  UiClone         copies of live game objects made asleep (no script of the game's wakes in a copy), the game's
                  scripts taken off, texts written through the game's own component; one log line per fallback
  PhoneApps       PhoneApp.Install: a button on the HUD phone, a button in the full menu's bar and a panel among the
                  game's app panels; opening as the game opens its own; the badge
  PhoneGridPager  pages of eight on the HUD phone's home screen (the game's eight apps fill it); steps aside for
                  good when another mod pages the grid
  AppShell        the frame of an app, copied from the game's MyEmployees: headline, white box, two tabs, table
  InfoBox         the white box as a page: title, lines, headlines, switches, previous/next selectors, buttons
  GameSprites     the game's pictures by name, found in memory
  AppIconPainter  an app icon in the game's manner, painted from one of the game's own
  WorldPanel      the game's panel over a place, with buttons on the game's own keys (an IOverlay in the game's box)
  MapPicker       a choice of places on the open city map, in a copy of the game's window for its stations there:
                  its search field, a marker of the game's per place (named while pointed at), the glide to an entry's place
  TravelList      a list in the window the game lists its subway stations in: a name, lines and a button per entry;
                  Window (its rectangle, for an owner that stands it aside or widens it) and EntryShape (higher
                  entries whose lines wrap) for lists that say more than a station's name
  GuideMark       a mark on a place that is no building, on the game's own destination guider (the job's), taken
                  only while it is free and taken off only by whoever put it there
Planner/
  PlannerModel    game-free: the clock as the game counts it, entries, order, clashes, states, "next", the badge
  PlannerStore    game-free: notes, reminders and settings; the save's three lines ("reallife:planner",
                  "reallife:planner_notes", "reallife:planner_reminders")
  PlannerText     IPlannerSource; words, days and times as the game writes them; icons by category
  PlannerTable    the table (the game's BaTable) and its row
  PlannerApp      the two tabs and the white box's four pages: the day at a glance, an entry, a note, settings
  PlannerDirector installs the app, loads and saves, asks the sources, sends reminders on the game's hour event
  Sources/        one class per source: notes, samples (test), the game's tax bill, careers, justice, custody,
                  licences, family, social plans, hotel checkout, city events, elections, business deadlines, sick note
```

- **Cost.** The director's `Update` is a timer comparison. Once a second it holds its two event handlers and, until
  the app is on the phone, tries to put it there. Everything else happens on `GlobalEvents.onNewHour` (reminders and
  the badge: integers only, no text unless a reminder is due), on the save event, and when the app is opened. The
  app's panel is inactive while closed, and the game is paused while it is open.
- **Sources.** `IPlannerSource`: an id (kept in the save with a reminder, never changed), `Available`, and
  `Collect(fromDay, toDay, list)`. A source reads its feature's state and changes nothing. A feature that is off or
  not loaded is not asked. A source that throws is logged once and left out for the session. To add one: a class in
  `Sources/` and one entry in `PlannerDirector._sources`; or, for a feature the Planner is not to know of, a class in
  the pairing's file in `Scripts/Wiring` given to `PlannerDirector.AddSource` (the Finance rows: `Wiring/EconomyLinks`).
- **Entries.** Who an entry is (`EntryId`: source, two numbers) must be the same every time it is collected, or its
  reminder is dropped as "gone". Texts are filled only when the list asks (`WantText`).
- **Reminders.** One per entry: at the time, 1 hour before, or 18:00 the day before. The clock is looked at once a
  game hour, so a reminder goes out at the start of the hour it falls in (never late in ordinary play, at most 59
  minutes early). It is taken out of the list before it is shown, checked against its source (gone, done, missed or
  over: not sent; moved later: moved with it), and one that fell inside a time skip or before a load goes out once,
  as a warning marked late.
- **The badge** counts what begins within two hours and is still to do. It goes when the app is opened and comes
  back only when what is due has changed (`PlannerLogic.DueStamp`).
- **If the game changes.** Every object the app is made of is looked up by name; if one is missing, one line says
  which, the app is left off the phone (or the part is left out: a switch, a selector, an icon), and reminders
  already set still go out. Nothing is thrown into the game's interface.
- **Checks.** `RealLifeOverhaul.Build/PlannerCheck` compiles `PlannerModel.cs` and `PlannerStore.cs` as they are and
  checks the calendar, order, clashes, states, the badge, reminders across a save and a time skip, round trips and
  damaged saves: `dotnet build RealLifeOverhaul.Build/PlannerCheck -c Release`, then
  `dotnet RealLifeOverhaul.Build/PlannerCheck/bin/Release/net10.0/PlannerCheck.dll` ends with `ALL CHECKS PASSED`.
- **Not run in the game.** The build machine cannot start the game: the first play session should follow the list
  in the pull request, with "Developer diagnostics" on.

Home, the garage and car ownership are offered the same native way (their sections say how): a click on a cooker
or a coffee machine and a button in the fridge's panel (`Home/HomeOffers`, `HomeFridgeButton`; sections 7 and 13 of
game-ui.md), the cooking list, the workshop's list, the custom shop, "My vehicles" and a stand's list in the game's
list window, three more bars under the game's needs (section 14), a reading in the game's vehicle panel (section
15), the game's own repair-bay panel with a button more (section 16), its panel over a place at a dock, a stand, a
broken-down car and the doctor's desk, and its destination marker (`GameUi.GuideMark`). Neither feature has a
canvas, a prompt or a key of its own left.

## Minimap and GPS (`Scripts/Navigation`)

A minimap on the game's HUD while the player is out in the city, and a GPS: a route along the roads to the game's
destination, drawn on the minimap and on the game's city map, with the next turn, the distance left and the time on
the game's clock at which the player should arrive. The base game has neither: its only navigation is the marker
that points straight at a destination (`UI.Guiders`).

```
Core/
  RouteSearch     the A* search over the road graph (moved here from Transport: the bus network and the GPS both use
                  it). New: Begin / Advance, so a search can be done a slice at a time; Find is Begin + Advance and
                  gives what it gave before
  RoadGraph       the game's lane points as RouteSearch wants them (moved here from TransitMap, unchanged)
Navigation/       (namespace RealLifeOverhaul.Nav: "Navigation" is a type of the engine's interface)
  NavModel        game-free: the route as a line, turns, where the player is on it, leaving it and arriving, the
                  view's arithmetic, the grid for "which lane points are near", the walking graph, the map picture
  NavGraph        the roads once per city: the two graphs, the grid, the map picture (drawn on a pool thread),
                  street names from the buildings' addresses
  NavHud          the panel on the HUD: a copy of the game's task panel with the map, the markers and two lines in it
  NavRouteLine    a line as one ribbon of the interface (a Graphic), made when the route changes
  NavMarks        the places that matter, read from the game's markers and from Core/MapPlaces
  NavCityMap      the route on the game's city map, and the double click that sets a destination
  NavBaseRules    game-free: the city under the roads (buildings and water): a place on the picture and back, a
                  building's outline, the water grid and its settling, the drawing, the file the measured city is kept in
  NavBaseGame     the only place of it that reads the game: a building's outline, one look down for water, the palette
  NavBase         makes that picture once per city load: from the mod's folder, or by measuring the city in slices
  NavDirector.Base  when it is made and shown, its two checks and its three tests
  NavBridges      what it asks of the rest of the mod (Wiring/NavLinks answers); NavText, the feature's words
  NavDirector     the one component: what shows, the destination, the search, following the route, the tests
```

### What the game has, and what was built on it

Read in the decompiled game and in its scene data (`evidence/uitools`, and a study script that reads the road graph
out of `level5`); nothing was seen running.

- **The destination.** Everything that sets a destination ends in `GuidersManager.SetGuiderTarget(..., Destination)`:
  a building's card, a task's button, this mod's "set destination" buttons and map markers (`Core/MapPlaces`, Law's
  nearest station, the Planner). The GPS reads `GuidersManager.destinationGuider.target` four times a second, and a
  job's (`jobDestinationGuider`) when no destination is set. So nothing that sets a destination knows of the GPS.
  Quest guiders and the private driver's are not routed to; they are markers on the minimap.
- **The game never clears a destination on arrival**; only the marker's own dismiss button does. On arrival the GPS
  says so (the game's notification), clears its route and leaves the marker, as the game does. It guides to the same
  place again only if the player has gone more than 150 m from it while the marker still stands.
- **The roads.** `CurrentSceneData.allWaypoints`: 18,791 lane points in the city of this version (17,282 lane edges,
  97 km of lanes, 3.8 by 2.2 km), each named `Road_25-Lane_3-Waypoint_7`, `...-In`, `...-Out`, or, for a junction's
  own points, `...-Connector2` / `...-CConnect2`. A junction's points are named after the road left, the road
  entered, or another one, so turns are found from the geometry (below), never from which road a point claims.
  `roadIds` holds something else and is not used. The data has 322 ways through a junction that turn right, 112 that
  turn left and 432 straight on: routes in this city turn right more often than left because the roads do.
- **No street names in the road data.** Buildings have addresses. A road is named after the street most of the
  buildings along it give as their address (`NavGraph.StreetOf`: up to seven places along the road each ask for the
  nearest door within 45 m; two votes make a name). Where that fails the turn is said without a name.
- **The HUD** is several canvases, all laid out for 3840 x 2160 and scaled by height, and all re-scaled by the
  game's "interface zoom" option (`UIs.SetUIZooming`). Top: the bar, 183 high. Left column, 740 wide: the task panel
  from 320 below the top (as high as its list, 1,000 at most, foldable), the control hints at the bottom (as high as
  their content, absent when switched off). Bottom right: the phone, 628 x 1,230. Bottom middle: the address labels,
  and the item panel (up to 1,834 wide) when something is held or driven. Top right: the job panel and the
  notifications. There is no corner the game always leaves free; see "Where the panel stands".
- **The city map** is the city seen by a camera; its markers are interface objects the game moves to their places on
  the screen every frame the map is open. A click selects a building (whose card has "set destination"); a drag
  moves the map; nothing sets a destination at a spot.

### The minimap

- **The picture is made once per city, from data, not rendered.** Every lane of the road graph is drawn as a
  soft-edged band 4.6 m wide into one byte per pixel (`NavRaster`), on a thread of the pool, from arrays nothing
  else writes; the main thread then hands it to the graphics card (`Texture2D`, Alpha8, no mip maps, not readable).
  A second camera was not used: one HDRP render of the whole city costs an unknown amount under the loading screen,
  comes out as the hour and the weather have it (dark at night), and cannot be checked without the game. A drawn
  street map is the same every time, and reads like the map of a satnav.
- **Size:** as many pixels as fit in 4,096 on the longer side, at most 1.25 a metre. This city: 4,096 x 2,308 at 1.13
  a metre, 9.0 MB on the graphics card. Its array of the same size in main memory is let go once it is uploaded.
- **Moving it costs one quad.** The picture is shown by one `RawImage` as wide as the view's diagonal; which part it
  shows is its texture window (`uvRect`), and turning with the player is the image's own rotation. Route and markers
  are children of an object turned and scaled the same way and moved so that the player is at its middle: their
  places are in metres and are never written again while they show. The panel has a canvas of its own, so a frame in
  which the player moved re-batches these few pictures and not the game's HUD.
- **Per frame** (`NavHud.Frame`, only while the panel shows): the player's place, heading and speed are compared
  with what was last written; standing still writes nothing. Moving writes the texture window (four vertices), one
  position and, when the heading changed by more than 0.1 degree, two rotations and the markers in view (32 at most)
  turned back upright. Nothing is allocated.
- **The view:** 260 m across at 100% zoom; from 8 m/s it widens smoothly to 1.9 times that at 28 m/s
  (`NavView.MetresAcross`), eased at 1.6 a second.
- **Markers** (`NavMarks`, read every 2 s; which are in view, 4 times a second; a marker is written only when what it
  shows changes): the game's guiders other than the one routed to; the game's permanent markers (the player's own
  buildings); the subway stations; every kind of place of this mod whose row on the city map is ticked. A marker
  is the game's own: the round base of its map markers (read once from the map's marker template) in the place's
  colour with the place's picture on it. The player is the map's own player icon and colour
  (`CityMapFilters.playerIcon`), turned by the game's own rule for it. The destination's marker is held at the view's
  edge while the place is outside it.
- **Where the panel stands** (`NavHud.Place`, 4 times a second, writes only on change). First choice: the game's
  left column, between the control hints and the task panel, the map as high as the room between them (it is square
  when there is room). Each of the game's panels is measured where it is on the screen, so the interface zoom and
  the screen's shape need no table. When a long task list leaves less than 240 units for the map there, the panel
  stands at the bottom beside the column instead, lifted over the address labels or the item panel if it would
  reach them (a 4:3 screen). With no room in either place it is hidden and one line says so.
- **Hidden** indoors, in the underground car park, on the subway and the casino boat, while the city map, the phone's
  full menu, the pause menu, the options, a building preview, the interior designer, a mini game or the time skip is
  on, at a funeral and on the way to hospital, whenever the game has switched its phone off (which it does with the
  rest of its HUD), and during custody, a bus ride and photo mode (`NavBridges.HudHeld`). As a child of the game's
  HUD it also goes with the game's own "hide the interface".

### The GPS

- **Driving** the route is searched on the road graph as the bus network uses it: lanes in their direction, a change
  of lane for 30 m extra. It begins on a lane within 14 m that runs the way the car points and ends on any lane
  within 30 m of the place (the single nearest within 400 m if there is none). If there is no way with the traffic
  (in this city 20% of random pairs: a lane that leads away and nowhere back, a stretch nothing drives to), the same
  streets are searched without the one-way rule and that way is shown; the log says so.
- **On foot** the same streets are searched with every lane usable both ways (`NavEdges.TwoWay`). That is what the
  route is: the way along the streets. Lanes are where cars drive, so the line runs down the carriageway while the
  player walks on the pavement beside it, and it knows no path through a park, a square or a building. A straight
  line would be wrong in a city of blocks and the game's marker already gives the direction; a search on the
  navigation mesh was not used because one call over the whole city cannot be sliced.
- **The search is sliced.** `RouteSearch.Advance` is called once a frame for 0.4 ms at most (the clock is read every
  64 steps), until it finds a way or runs out of road. Measured outside the game on this city's graph (NavCheck,
  .NET 10 on the build machine, not the game's Mono): 3,400 steps on average, 14,105 at most (the whole city, on
  foot); a whole search 0.25 ms on average and 1.1 ms at most. Mono is several times slower, so the worst search
  is expected to take a handful of frames, each with 0.4 ms of it. Not measured in the game: the log line of every
  route gives the steps, the slices, the time in all and the longest slice.
- **Turns** (`NavRoute.FindTurns`): at every junction the route passes, the heading over the last 14 m of plain lane
  before the junction's own points against the heading over the first 14 m after them. Under 28 degrees nothing is
  said; to 55 "keep left/right"; to 150 "turn left/right"; above, a U-turn. Two junctions within 18 m, or two curves
  of one junction with a plain point between them, are one turn.
- **Following** (4 times a second): the nearest place on the route, looked for around the segment the player was on
  last. More than 32 m from the line in a car (22 m on foot) for 2 s asks for a new route, and never twice within
  4 s. Arrived: within 28 m of the place in a car (14 m on foot), or at the end of the road in a car when the place
  is not on one.
- **The two lines** are put together again only when a figure in them changes (distances are rounded to 10, 50 or
  100 m). The time is the game's clock: the lanes' speed limits at 75% for a drive, the player's own walking speed
  on foot, turned into minutes at the rate the game's clock has been running.
- **On the city map** the route is one line in the markers' layer, under the markers, worked out again only in a
  frame in which the map's camera moved (the game moves every marker in every frame the map is open, and is paused
  meanwhile). **Two clicks on one spot within 0.4 s** make that spot the destination (a building: the same call as
  its card's button). One click is not used: it drags the map and selects buildings, and a destination must not be
  replaced by accident.
- **Voogle Route** (another author's GPS mod, found by `Core/OtherMods`; see [docs/other-mods.md](docs/other-mods.md)):
  if it is switched on, the city map is left to it. It draws its route there as lines in the world on the map's own
  layer and has its own click, panels and markers on the map, so nothing of this mod's is added to the map: no route
  object, no double click. This mod's route is then on its own minimap only, with its two lines, where that mod draws
  nothing; the arrival notice and the end of the destination are that mod's. The option "Use the other mod's route line" (a row that is on the options page only while that mod is on;
  off by default) leaves the route to it altogether: only the minimap is shown. One warning says which.

### Costs

| when | what | cost |
|---|---|---|
| once per city | the driving graph | one pass over the lane points twice (as the bus network does); not measured in the game |
| once per city | the walking graph and the grid | 0.5 ms and 1.3 ms outside the game |
| once per city | the map picture | 34 to 50 ms outside the game, on a pool thread; then one upload of 9 MB on the main thread (not measured) |
| once per city | the panel | one copy of the game's task panel with its entries, cut down asleep; not measured |
| per route | the search | 0.4 ms a frame until done; see above |
| per route | the route's line, turns and the two meshes | under 0.1 ms outside the game |
| 4 times a second | visibility, placement, the destination, the place on the route, markers in view | a few dozen comparisons; text only when a rounded figure changes |
| every 2 s | the places read again | one pass over the game's markers and the mod's places |
| per frame, panel showing | `NavHud.Frame` | comparisons; when moving, one quad's texture window, one position, up to 34 rotations |
| per frame, panel hidden or options off | `NavDirector.Update` | one time comparison and two flag tests |

Each one-off step is named for the session record (`SessionWatch.Mark`: "the GPS's road graph", "the GPS's walking
graph", "the minimap's picture", "the minimap's panel"), and its time is in its log line. The steps are taken one a
tick from the moment the city's roads exist, so they fall under the loading screen where the game is quick enough
to have the roads there.

### Safety

Nothing is written to the save. Every object made lives under the game's HUD or its map's marker layer and is
destroyed when the director is (city unload, "prepare this save for removing the mod"). An error in the director
switches the feature off for the session with one line and takes its objects off the screen; an error in the city
map's part switches only that part off. The game's map event is held with `EventHold.Keep`. If the task panel, its
headline or its mask is not there, the minimap is left off with one `Game UI:` line and the route is still drawn on
the city map.

### Checks

`RealLifeOverhaul.Build/NavCheck` compiles `Core/RouteSearch.cs`, `Navigation/NavModel.cs` and `Transport/BusBay.cs` as they are:
`dotnet build RealLifeOverhaul.Build/NavCheck -c Release`, then
`dotnet RealLifeOverhaul.Build/NavCheck/bin/Release/net10.0/NavCheck.dll` ends with `ALL CHECKS PASSED`. It checks the
search whole against the search in slices, routes on a made-up grid of streets named the three ways the game names
junction points, a one-way street driven and walked, the turn list, following, leaving the route, arriving, the
line that is drawn, the view's arithmetic and the picture. Given a file of lane points as its argument it also
prints the timings quoted above (the file is made from the game's own data by a study script and is not in the
repository). The bus bays are checked in the same run (see "Transport", "Bays"); `--bays` prints each run of the
model bus.
The taxi called from the phone is checked in the same run too (`TaxiCheck.cs` with `Transport/Taxi/TaxiRules.cs`; see
"Transport", "A taxi by phone").

### Log lines (with "Developer diagnostics" on)

- `Navigation: road graph made: 18791 lane points, ... edges (... of them changes of lane), 273 roads, ... ms.`
- `Navigation: walking graph and index made: ... edges, ... lane points in the index, ... ms.`
- `Navigation: map made: 4096 x 2308 pixels (1.13 a metre, 9.0 MB), ... lanes drawn in ... ms off the main thread, sent to the graphics card in ... ms.`
- `Navigation: minimap on the HUD, a copy of the game's task panel, in the left column; view 568 x 568 units; ...`
- `Navigation: route found to '...' (driving; a new destination): ... lane points, ... m, ... turns, ... search steps in ... slices, ... ms in all (longest slice ... ms).`
- `Navigation: 41 m off the route for 2 s (driving, 310 m along it): looking for a new route.`
- `Navigation: arrived at '...' (22 m from it, driving). ...`
- `Navigation: destination set by a double click on the city map: ...`
- `Navigation check (other mod): Voogle Route is switched on: this mod's route is drawn on its own minimap only, and nothing of this mod's is on the city map (no object there: PASS).`
- Always logged: the "Other mods:" warning for Voogle Route, and every part left off because the game is not built as expected.

### Not verified (the game could not be run)

Everything on screen. In particular: that the copy of the task panel looks right at the sizes it is given; that an
Alpha8 picture is drawn in the image's colour by the interface's standard shader (if not, the roads are black);
that the mask's rounded corners cut the map; the player icon's direction (taken from the game's own rule); how the
markers read at 46 units; that the game's phone object is active whenever its HUD is in use (the panel is hidden
when it is not); the panel's place beside the column on a narrow screen; that a double click on the map reaches
this code and not only the map's drag; the names streets are given; how far the walking line lies from the
pavement; every time quoted from outside the game.

### The city under the roads (`NavBase`)

The option "Buildings and water on the minimap" (on by default; off, the minimap is what it was: roads, route,
markers, and nothing of this is made or kept).

- **The game has no map picture.** Its city map is the city itself under a camera: `CityMap.Toggle` sets the main
  camera's culling mask and switches to the map's Cinemachine camera (`CityMap.cs` 221 to 284), `CityMapCam` moves
  that camera, and each building is drawn by its simple `lodVersion` mesh (`CityBuildingController.ToggleLodMode`,
  230 to 244). No texture, sprite or render texture of the city exists in the game's code. So the base is drawn.
- **Buildings**: `CityManager.cityBuildingControllers`. An outline is the convex outline, seen from above, of the
  `lodVersion` renderer's box: the mesh's own box turned as the building is, or the engine's upright box when the
  renderer is part of a static batch (its mesh is then the whole batch's); all the building's renderers if it has
  no `lodVersion`.
- **Water**: the game has no water object, layer or list (Leisure's fishing found the same). The city is asked on a
  grid of 8 m squares, one look straight down each (`Physics.Raycast`), by the fishing rule: nothing there, or
  something more than 0.8 m below the nearest road that is neither road, building nor walkable (the navigation
  mesh), is open. Water is the open squares that reach the picture's edge without crossing ground, so a courtyard
  or a lawn stays ground. A square within 8 m of a lane point or inside a building is ground without asking.
- **Colours**: the game's interface palette (`GlobalReferences.colors`): buildings the roads' `lightGrey` at a third
  of their strength, water `darkBlue`. The game's map has no flat colours of its own to read.
- **The picture**: at most 2048 pixels on its long side, RGBA32, no smaller copies: 2048 x 1154 and 9.0 MB on the
  graphics card for the game's city (3.6 km by 2 km), 1.77 m a pixel. It covers the road picture's piece of the
  world from the same origin: pixel = (world - origin) x pixels a metre. One `RawImage` under the roads shows it,
  as large and turned as the roads' square; when the player has moved its window is written (four divisions).
- **Cost**: measuring takes 0.8 ms of a frame at most, for some seconds, once per version of the game; the result
  (outlines and grid, about 150 KB) is kept in `RealLifeOverhaul/minimap_base_<build>.bin` beside the saves. Every
  load after that: one read, one drawing on a pool thread, one upload.
- **Not seen running**: whether every piece of ground the city stands on has something solid under it far from
  the player, and what the palette's dark blue looks like on the panel. The tests "minimap, buildings and water
  alone" and "minimap, mark three buildings" are for judging both, and the two `Minimap check` lines say what was made.

## Economy: credit rating, lending and the Finance app

The economy core (`Scripts/Economy`, option "Credit rating and phone loans", id `economy`): one credit rating worked
out from how the player pays, the banks' terms moved by it (under the threshold nobody lends), loans from the phone,
and a Finance app. It is the foundation the later economy work stands on: more kinds of credit and what a missed
payment leads to (built: see "Economy: credit products, missed payments, the private lender and the ways out" below),
a city economy with interest rates, investors, tax. No method of the game's is patched; everything is done by
reading and writing the game's public state, on its events, with copies of its interface.

```
Economy/
  Credit/           game-free: compiled as they are by RealLifeOverhaul.Build/EconomyCheck
    CreditCatalogue   EVERY NUMBER: the scale, the bands, the threshold, the weights, the fades, the terms, the fee
    CreditModel       CreditFile (what the save holds), CreditEvent, PayDay, LoanMark, LoanView, LegalStanding, CreditInputs
    CreditScoring     the arithmetic: Compute, Tips (what would raise it most), Open (the starting estimate)
    CreditLedger      THE PUBLIC FACE: the score and grade, CanBorrow, Post, GradeChanged, ICreditAccount and its register
    CreditStore       the save's one line, "reallife:economy"
    LoanBook          the game's loans told apart (Sync) and the daily run read (Settle)
  Lending/
    LendingTerms      game-free: the terms table, the phone's fee, Decide, MostNow
    BankTerms         the city's banks; their own terms kept, the rating's written, the own put back
    GameLoans         the game's loans as an ICreditAccount; the game's own means test read from its public members
    PhoneLending      a loan made from the phone exactly as the bank's dialogue makes one, plus the fee
  Finance/
    FinanceApp        the tabs (five since the second part) and the white box's pages
    FinanceTable      the table (the game's BaTable) and its row
    FinanceText       the words; money as the game's loan pages write it
    CashFlow          one pass over the game's transaction history for a period
  EconomyBridges    what the economy asks of the rest of the mod (filled in by Wiring/EconomyLinks); Finance.Open
  EconomyDirector   keeps it going: the save's line, the game's hour and day events, the banks' terms, the app, the tests
```

### The rating

One score, 300 to 850, with a grade. It is computed, never random: the same file, figures and day give the same score.

| | |
|---|---|
| bands (`GradeFrom`) | 300 very poor, 500 poor, 580 fair, 670 good, 740 very good, 800 excellent |
| `LendingThreshold` | 580: below it nobody lends, on every preset (no preset touches the economy) |
| `CleanStart` | 640: a save with no debts and no record |
| `Weights` | payments 40, owed 25, length 10, legal 15, applications 10 (per cent; they add up to 100) |

`score = 300 + 550 x (sum of weight x factor) / 100`, each factor from 0 to 1 (`CreditScoring.Compute`):

- **Payments.** Every instalment of the last `PaymentFadeDays` (60) weighs `1 - age / 60`. Factor =
  `(prior x neutral + good + lasting) / (prior + good + lasting + MissWeight x bad)` with `PaymentPriorWeight` 4,
  `PaymentNeutral` 13/44 (what makes an empty file come out at 640), `MissWeight` 8, an instalment paid in part
  counting as `PartPaidShare` 0.5 of a miss, and `lasting` = payments on time older than the fade, each worth
  `LastingPaymentShare` 0.05, at most `LastingPaymentsMost` 200 of them. So a miss costs a thin file more than a long
  one, and is gone from the factor after 60 days (it stays in the file's totals).
- **Owed.** `OwedAgainstLimitShare` 0.8 of it is `1 - owed / limit` (the limit is what every bank would lend on its
  **own** terms, so the rating does not feed on itself); the rest is `1 - (daily payments / daily income) / 0.5`
  (`ServiceShareWorst`), with no income counting as the worst. Income is the game's `PlayerHelper.CalculateDailyIncome`.
- **Length.** Days since credit was first held, in full after `HistoryFullDays` 120.
- **Legal.** 1 less `ConvictionCost` 0.35 a conviction, `JudgmentCost` 0.25 a civil judgment being paid off,
  `LateDayCost` 0.08 for each day a fine or court debt could not be taken (at most 0.40), `OutstandingCost` 0.05 while
  any is owed, `OffenceCost` 0.05 a line on the police record in the last `OffenceDays` 30 (at most 0.20). Read through
  `EconomyBridges.Legal`, never from Justice or Law directly.
- **Applications.** Each takes `ApplicationDent` 0.15 off, fading over `ApplicationFadeDays` 10. One application is
  about 8 points for a day. A loan taken at a bank is an application (seen when the loan appears), and so is one by phone.

**The starting estimate** (`CreditScoring.Open`, logged as `Economy: a credit file is opened ...`): each loan in the
save is taken as paid on time for `paid-off part / daily payment` days (what the game's daily run takes each day),
which gives the length of the history and the lasting payments. Debts and the record need no estimate: the scoring
reads them live.

**The file** (`CreditFile`) keeps the instalments of the last 60 days counted per day, the last `LedgerMost` 120
events with what each did to the score, folded totals per kind for older ones, and the loans as last seen. An event is
posted with `CreditLedger.Post(kind, amount, day, sourceKey, count)`; the result is the effect in points.

### How the daily payment is read

`GameManager.NewDay` raises `GlobalEvents.onNewHour`, then runs the day (`BusinessHelper.RunDaily` calls
`LoanHelper.RunDaily`: an instalment is taken, or only the interest when the money is short and the balance is left as
it was), then raises `GlobalEvents.onNewDay`. So:

- on the hour event `LoanBook.Sync` brings the file's marks up to date: after it, each mark is exactly its loan as it
  stood **before** the run. What the player did since the last hour is seen here (a new loan, one paid off early, part
  paid off, a changed daily payment) and posted;
- on the day event `LoanBook.Settle` compares after the run: a balance that fell was paid, a loan that is gone was
  paid off, a balance that did not move while a payment was due was missed.

Nothing the player does can fall between the two (they are in one call of the game's), so the reading is exact. If
the day event ever came without the hour event before it, the comparison is against the last hour's reading and the
day's line says so and ends FAIL. A loan is known by the object itself while the save is loaded and, after a load, by
its fingerprint (the bank's address and the amount borrowed); two loans of one print are told apart by their balance
(`LoanBook`, top comment). `LoanEntryUi` clamps a daily payment between the bank's minimum and the balance, so a
payment under the minimum cannot be set; a bank without settings has no minimum, and a payment of 0 is then neither
paid nor missed.

### The banks' terms

A bank's terms are one `BankSettings` object in memory. `BankDialog.LoanRequest` reads `maxTotalLoanAmount` when an
amount is confirmed and `BankLoanSettings` reads `annualInterestRate` and `yearsToPayLoan` for the figures shown
while it is typed. `BankTerms.Apply(score)` writes `LendingTerms.For(score, own cap, own rate)` on each:

| rating | cap | annual rate |
|---|---|---|
| under 580 | 0: the game itself refuses every amount | (not offered) |
| 580 | `CapShareAtThreshold` 40% of the bank's own | its own plus `RatePointsAtThreshold` 6 |
| 800 and over (`BestTermsFrom`) | `CapShareAtBest` 130% | its own less 2 (`RatePointsAtBest` -2), never under `RateFloor` 1 |
| between | a straight line | a straight line, rounded to a whole point |

- **When.** At the start, each game hour, each day, after any event that moves the score, when the app opens and
  before a quote. Only a value that differs is written.
- **The game's quest loans are left alone** (`BankTerms.QuestHold`): while the tutorial's first-business loan is open
  (`tutorialEnabled` and `tutorial_quest_establish_first_business_objective_1` not completed: the game's own test in
  `BankDialog.GetEconomyMaxLoanAmount`) every bank keeps its own terms; while the side quest `SQBankruptcy` is active a
  bank with `allowSideQuestEmergencyLoan` keeps its own (that loan ignores the cap anyway).
- **Put back.** The first values seen on an object are its own and are kept for as long as the game runs.
  `BankTerms.Restore` writes them back exactly: when the option goes off, on a fault, when the city unloads or the mod
  is switched off (`Wiring.EconomyLinks.Disconnect`, and again in the director's `OnDestroy`), and from the test
  button. Nothing of this is in a save: the objects are the game's assets.
- **The refusal.** With a cap of 0 the game says "the most we can lend you is $0". So while nobody lends, the
  one-second timer looks whether a bank's dialogue is open (`DialogController.current.dialog is BankDialog`, at the
  counter or on a call) and says why once, through the game's notification, with what would raise the rating most.
- **Known.** The game recalculates every loan's daily interest and payment from the bank's settings when
  `bankInterestMultiplier` is changed in the game's options (`LoanHelper.RecalculateLoanPayments`): with the economy
  on that uses the rate for the rating at that moment, not the rate each loan was taken at.

### Loans from the phone

`PhoneLending.Take` makes one of the game's own loans as `BankDialog.LoanRequest` does: `new Loan(amount, interest,
payment, the bank's address)` with the figures from `LoanHelper.CalculateDailyInterestPayment` and
`CalculateDailyPayment`, added to `SaveGameManager.Current.Loans`, paid out with the game's `ba:transaction_loanpayout`,
and `ba:gameevent_newloan` raised. EconoView lists it, the game's daily run collects it, and it is there with the mod
removed. The most a lender lends now is `LendingTerms.MostNow`: its cap for the rating less what is owed to it, and no
more than the game's own means test (`BankDialog.GetEconomyMaxLoanAmount` is private; `GameLoans.MeansLimit` reads the
same public members). The fee is `PhoneFeeShare` 1.5% of the amount, rounded up, at least `PhoneFeeLeast` 25
(`reallife_transaction_loan_fee`, in the game's loan-expenses category). The smallest loan is the game's 500. The
bankruptcy quest's emergency loan is offered in person only.

### The Finance app

Built as the Planner is (`AppShell`, with `SetTabCount(4)`), id `RealLifeFinance`. Nothing runs while it is closed;
rows and figures are made when it opens, when a tab is picked and when the player changes something in it.

| tab | what it shows | from |
|---|---|---|
| Overview | cash; owed in all; loan payments, fines and rent tomorrow; wages paid yesterday; net worth; the rating; every debt as a row | `GameInstance.Money`, `Loans`; `EconomyBridges.OtherDebts`; rent by the rule of `BusinessHelper.RunDaily` over `BuildingRegistrations`; yesterday's transactions in the category `ba:transactioncategory_salaryexpenses`; `PlayerHelper.GetPersonalWealth().CurrentWealth` |
| Credit | score, grade, the threshold, the one or two things that would raise it most, each factor in a plain word; the last 30 events | `CreditLedger.Score`, `CreditScoring.Tips`, `CreditFile.Events` |
| Borrow | each bank: rate offered, most it lends now, what is owed to it; an amount (steps, or typed into the game's text box); payment, interest, days, fee, cost; the game's question box | `BankTerms.Lenders`, `PhoneLending.Quote` (the game's `LoanHelper`), `HudConfirm` |
| Cash flow | money in and out by the game's kinds of transaction for yesterday, 7 days or 30, and the five largest | `GameInstance.Transactions` (`CashFlow`: no second history is kept) |

- The game has no forecast of wages (`EmployeeInstance.PayWage` works them out as it pays), so the app shows what was
  paid yesterday and says so.
- The game gives a category to eleven kinds of transaction only, and a type to every one, so the cash flow is grouped
  by type, named by the game's own `<type>_label` (the grouping EconoView's filter offers).
- The game keeps at least seven days of transactions and drops older ones past 1,000; when the period reaches further
  back than the history, the app says from which day the history runs.
- Repaying and changing a daily payment stay in EconoView; "Repay in EconoView" opens it (`FullMenu.ShowApp`).
- The badge on the HUD button counts payments missed and a change of grade since the app was last opened.
- **EconoView's loans page has no button of the mod's.** Its layout is a prefab the UI study has not read, and a
  button placed blind in a page of the game's is not clean. The Planner's row, the notifications and the app are the
  ways in.

### The save

One line of the save's modData, `reallife:economy` (`CreditStore`; version 1 as below, version 2 since the second
part, which adds to it: see "The save: version 2"), registered with the feature, so the self-check and "prepare this
save for removing the mod" know it:

```
v=1~h:...~d:...~e:...~f:...~l:...
  h  header: start day|first credit day|old on time|old missed|old part|test offset|last score|last grade|settled day|
     unseen missed|unseen grade|last missed day|count|amount|last grade day|grade it was|record as last seen (5)
  d  per day of the last sixty: day|on time|missed|part|test on time|test missed
  e  the last 120 events: kind|day|amount|count|effect|test|source
  f  folded totals, one per kind: count|amount
  l  the loans as last seen: street|number|total|first day|remaining|payment|interest
```

A later version adds fields at the end of a record or a part with a new letter; this reader takes what it knows. A
full file is under 8,000 characters (`EconomyCheck` measures one). A damaged line opens a fresh file from what the save shows. The loans are the
game's own records: a save loads in the plain game with its loans as they are.

The line is written when the game saves (`SaveHook`), and when the option is switched off or the save tools stop the
feature in the middle of play. It is **not** written when the city unloads: by then the game's current save may be
another one. The director's first look is a second after the city's objects are made, and one that is destroyed after
a newer one has started leaves the file, the ledger and the banks to that one.

### For the later phases

```csharp
CreditLedger.Value / Grade / Score          // the rating; Score has every factor and the counts behind it
CreditLedger.CanBorrow(out CreditRefusal)    // THE GATE: every lender asks it first
CreditLedger.Post(kind, amount, day, sourceKey, count)   // one line on the file; returns the effect in points
CreditLedger.GradeChanged                    // (was, is)
CreditLedger.Posted                          // (event, score before, score after)
ICreditAccount + CreditLedger.Register       // Key, NameKey, Balance, Limit, PaymentDue: counted in "owed" and in the app
LendingTerms.For / MostNow / PhoneFee / Decide, BankTerms.Lenders / Wanted / QuestHold
EconomyBridges.Legal / LegalReady / OtherDebts, Finance.Open(tab)
```

A new kind of credit is a class implementing `ICreditAccount`, registered while the economy runs, that posts
`PaidOnTime`, `Missed` or `PartPaid` for its instalments (they are counted on their day) and `NewCredit` when it
lends. A new kind of event is a value at the end of `CreditKind` and a text in `FinanceText.EventKeys`. Every figure
is a constant of `CreditCatalogue`; the Guide's page prints them through `{facts}`.

### Cost

No `Update`. A coroutine waits one second on the real clock (the game is paused while the phone or a call is open)
and then: tests four flags, holds three handlers (`SaveHook.Keep`, `EventHold.Keep`), and reads one static field while
nobody lends. Per game hour: the loans compared with their marks (a handful), the banks' terms compared (a handful).
Per game day: the same, the score worked out once or twice (one pass over at most 60 days and 120 events), the record
read. The score is cached and worked out again only after something changed. The app allocates only when a page is
written.

### Faults

Every handler is wrapped. A fault logs one line (`Economy: switched off for this session after an error in ...`), puts
the banks' own terms back, takes the app off the phone and leaves the game, its loans and its daily run alone.

### Test buttons

"Test: show my credit report in the log" (written whether or not diagnostics are on), "record a missed payment",
"record 30 days of payments on time", "set the rating to 560 / 600 / 760" (each a test entry), "clear test entries"
(the score, the days, the events and the totals are exactly as before; `EconomyCheck` proves it), "restore the banks'
own terms" (until the next game hour).

### Log lines (with "Developer diagnostics" on)

- `Economy: a credit file is opened for this save on day 12 at 640 (Fair). Factors: ...` (once per save)
- `Economy check (start): 3 bank(s) found, 1 loan(s), file version 1, rating 655 (Fair), 3 bank(s) given the terms for it. <bank>: own 100000 at 8%, in memory 70682 at 11%; ...: PASS`
- `Economy check (day 13): 2 instalment(s) read against the loans as they stood before the run. <street> <number>, 10000, left 9500 before the run: paid 112; ...: missed 100. Rating 655 to 611: PASS`
- `Economy: event Missed (day 12, amount 100, count 1, from 'loans'): rating 655 to 611. Factors: payments 0.31 (4 on time, 1 missed), owed ..., length ..., record ..., applications ...`
- `Economy check (terms, the day): rating 611, 3 bank(s) changed. ...: PASS`
- `Economy check (application): 5000 from <bank> by phone at rating 655, the most it lends now 61000: approved at 11% a year, 41 a day and 4 interest for 122 days, fee 75: PASS`
- `Economy: a bank's dialogue opened with the rating at 560, under 580: the reason was said ...`
- `Economy check (terms restored, the option is off): 3 bank(s) put back. <bank>: own 100000 at 8%, in memory 100000 at 8%; ...: PASS`
- `Finance: tab 1 written, 14 row(s); 2.3 ms.`

### Checks

`RealLifeOverhaul.Build/EconomyCheck` compiles the game-free files as they are (seven for this part): the bands and the weights, the
start at 640, a clean history rising, misses and their recovery over 60 days, applications, owed and the record, the
score always inside 300 to 850 (400 random files), the gate at 580, the terms table at every score, the fee, the cap
and the folding, the test entries, the save's line with a later version and with damage, loan identity across list
changes and a load, and the daily run. `dotnet build RealLifeOverhaul.Build/EconomyCheck -c Release`, then
`dotnet RealLifeOverhaul.Build/EconomyCheck/bin/Release/net10.0/EconomyCheck.dll` ends with `ALL CHECKS PASSED`.

### Not verified (the game could not be run)

Everything on screen: the four tabs (the two in the middle are copies with no picture, expected to draw as plain
white tabs), whether the white box holds the longest pages (Credit with two tips, an offer), the app's place on the
phone's second page and in the full menu's bar with ten buttons, the icon's glyph. That `Money` in the game's loan
format reads well in the table. That the bank's dialogue, opened under the threshold, answers every amount with its
"maximum exceeded" text and that the notification is seen beside it. The text the game's localiser gives a
transaction type that has no `_label`. That the hour event of `NewDay` reaches the mod's handler before the run on
every path (it is the game's own order; the day's check line says so each day).

## Economy: credit products, missed payments, the private lender and the ways out

The second part of the economy (`Scripts/Economy/Products`, `Scripts/Economy/Debt`; options "More kinds of credit",
"The private lender", "Consequences of not paying" and "How hard not paying hits"). It stands on the credit rating
above and changes none of its rules: every product asks `CreditLedger.CanBorrow`, so under the threshold (580)
nothing is opened and nothing is drawn, on every preset; what is already owed stays owed. No method of the game's is
patched. The game's own loans stay the game's own records.

```
Economy/
  Products/         the first five are game-free and compiled by EconomyCheck
    ProductCatalogue  EVERY NUMBER of this part: limits, rates, fees, the steps' counts by level, the private lender, the ways out
    ProductTerms      the terms by rating (limits, rates, deposits, quotes), and consolidation's before and after
    CreditAccount     ONE model for every account of the mod's (ProductKind), LadderState, IWallet, AccountRules (the arithmetic)
    Ladder            LadderRules: what a lender does as payments go on being missed, for accounts and the game's loans alike
    LoanHolds         what the mod writes on one of the game's loans (penalty rate, arrangement) and how it is put back exactly
    Wallet            IWallet over GameManager.ChangeMoneySafe (the game's transaction history has every movement)
    CreditDesk        THE GAME SIDE: the day and the hour, the steps, repossession, claims (.Offers: products and purchases;
                      .Tests: leaving a save and the test tools; Debt/CreditDesk.Relief: collections, lender, arrangement, bankruptcy)
  Debt/
    Shark             game-free: the private lender's one debt and its terms (SharkDebt, SharkRules)
    Relief            game-free: arrangement, bankruptcy, the rating's ceilings, an approval for finance (Relief, ReliefRules)
    DebtPhone         three contacts and their conversations: Collections, Private lender, Debt advice
  Finance/
    Books             the game's figures the economy reads: what leaves tomorrow, businesses and their statements, sales
    FinanceApp.Accounts  the Accounts tab and the product pages of Borrow
Justice/DebtClaims  NEW FILE in Justice: file, ask about and withdraw a lender's civil claim (one of Justice's own suits)
Wiring/DebtLinks    the bridges: courts, holds, the injury of a mugging, what the camera sees, a kerb for the test car, contact pictures
```

### The products

All figures are constants of `ProductCatalogue`; "the bank's rate" is the house bank's own rate moved by the rating as
`LendingTerms` moves it, times the game's interest multiplier. The house bank (`BankTerms.House`) is the bank with the
largest cap of its own. Interest is whole money, worked out in hundredths of a per cent over the game's days in a
year with the remainder carried (`CreditAccount.Carry`): a long run neither gains nor loses a unit.

| Product | What it is | How it is collected |
| --- | --- | --- |
| Overdraft (`ProductKind.Overdraft`) | A limit of 2% (at 580) to 8% (from 800) of what the city's banks lend at the rating, rounded to 50, at least 200. Free to hold. Rate: the bank's plus 8 points. | On the first hour event of a new day, before the game's run, if cash is short of what leaves tonight (`Books.Tomorrow`, the figure the Overview shows) it pays the shortfall into cash up to its free limit. Interest is added to the balance each day. Cash above 500 pays it back at each day's end. A balance over the limit is a missed payment. |
| Credit card (`CreditCard`) | A limit of 3% to 12% of the same, rounded to 50, at least 300. Cash is drawn in the app for a fee of 3%, at least 5. Rate: the bank's plus 12 points. | A statement every 7 days. 3 days later the minimum (5% of the statement, at least 25, or all of a smaller balance) is taken, and what is then still unpaid of the statement bears 7 days' interest. A statement paid in full bears none. A minimum that could not be taken is tried again each day: each day is a miss. |
| Business loan (a game `Loan`, `LoanMark.Purpose = Business`) | Offered when the player runs a business with 7 daily statements on record (`GameInstance.financialSummaries`, what EconoView's business page reads): up to 30 times its average daily profit, at the bank's rate less 1 point, plus the phone's arrangement fee. The mark keeps the business as `Security`. The bank's cap in memory is raised by what is owed on it (`BankTerms.Secured`), so it does not use up the personal cap. | By the game, like any loan. |
| Car finance (`CarFinance`) | See "A purchase on finance". Deposit 10% to 50%, 60 to 240 days, the bank's rate plus 2 points, up to what the bank lends unsecured at the rating. The vehicle (`VehicleInstance.id`) is the security. | A daily instalment (the amount over the term, rounded up) and the day's interest, taken together on the game's day event, after the game's own loans. If the cash is not there nothing is taken, the interest is added to the balance, and it is a miss. |
| Mortgage (`Mortgage`) | The same for a building bought in BizMan: deposit 20% to 50%, 360 to 1,440 days, the bank's rate less 1 point, up to 5 times what the bank lends unsecured. The building (its address) is the security. | As car finance. |
| Late fees (`Arrears`) | Late fees on a game loan that were not in cash on their day. No interest. | Taken in full as soon as the cash is there. |

Early repayment of any of the mod's accounts is free, at any time, in Finance under Accounts. A game loan is repaid
in EconoView, as before.

**The mortgage: the game does sell property.** BizMan's presentation buys a building outright
(`BizManPresentation`, transaction `ba:transaction_buildingbought`, a `RealEstate` entry in `GameInstance.realEstate`
with its `purchasePrice`); `RealEstateHelper` collects its rents daily and sells it to a competitor when it is put up
for sale. So the mortgage exists. A building is never taken for one: the game has no safe way to take a building
back (`RealEstateHelper.SellBuildingForCompat` removes the entry without restoring the owner or the rent), and the
player may live or trade in it. At the fifth step the lender takes the building's rents instead.

### A purchase on finance (where the game forced a change)

The design was: the player arms finance, buys in the game's own way, and the lender pays the financed part back into
cash after the game has charged the full price. The game refuses a purchase unless the WHOLE price is in cash
(`ShowcaseVehicleController.Purchase` and `VehicleDeliveryHelper` call `ChangeMoneySafe` without force; BizMan tests
`Money < price`), so that would only have financed people who did not need it. What is built instead keeps the game's
purchase untouched and turns the order round:

1. **Approval** (`CreditDesk.Approve`): amount, deposit share and term are chosen in Finance. The buyer must hold
   the deposit the amount implies (`ProductTerms.DepositFor`). The approved amount is paid into cash at once
   (`reallife_transaction_finance_advance`), the application goes on the credit file, and the ids of the vehicles (or
   the addresses of the buildings) owned now are remembered.
2. **Purchase**: made in the game's own way. While an approval stands the desk compares one count a second
   (`VehicleInstances.Count` or `realEstate.Count`); when it changes it looks for what is new. A vehicle's price is
   the newest `ba:transaction_vehiclebought` for that type since the approval (else the type's list price); a
   building's is its `RealEstate.purchasePrice`. Lent vehicles (`zeroSellingPrice`) and hand vehicles are passed over.
3. **The account** opens for `ProductTerms.Financed`: the price less the buyer's deposit, never more than was
   approved. What was approved over that is taken back at once (`..._finance_returned`).
4. **No purchase** by the end of the day after the approval: all of it is taken back, by force if it has been
   spent (the game lets cash go under nothing, as its own loan interest does). The same on "Cancel the approval".

**Selling or losing the security** (`CreditDesk.Securities`, each game hour): when the vehicle or building is no
longer the player's, the newest matching sale in the game's transactions since yesterday (`ba:transaction_itemsold`
with the vehicle's type, `ba:transaction_buildingsold` with the address) is taken against the balance as far as it is
still in cash; the rest stays owed on the same payments, unsecured, and the app says so.

### When payments are missed

One state machine (`LadderRules`, `LadderState`) for every debt: each account of the mod's and each of the game's
loans (its state is on the loan's `LoanMark`, fed by `LoanBook.Settle`'s findings). It counts due payments missed in
a row; a payment made in full puts the count back to nothing (the misses stay on the credit file).

| Step | Standard, Realistic | Hardcore | What happens |
| --- | --- | --- | --- |
| 1 Reminder | 1 | 1 | The notice that the payment was missed (it names the amount and the fall in the rating), and the Planner's row. |
| 2 Late fee | 2 | 2 | 2% of the payment, at least 15 (halved on Relaxed), added to the account. For a game loan: taken from cash, else put on the `Arrears` account. |
| 3 Penalty rate | 3 | 3 | 5 points a year more until 3 payments in a row are made. |
| 4 Collections | 5 | 4 | The call: "Pay what is overdue now", "Agree a payment plan" (the overdue money over 10 days on top of the normal payment; one per debt; broken, it goes straight to the next step), "I can't pay". |
| 5 Repossession | 8 | 6 | Secured debts only. Car finance: the vehicle is taken and sold for 60% of its price; the rest stays owed. Business loan: 25% of that business's daily takings go to the lender until the arrears are paid. Mortgage: the building's rents do. |
| 6 Claim | 12 | 9 | A civil claim for the balance and costs (10%, at least 50). Also when a balance left by a repossession still stands 10 days later. |

Relaxed stops after step 4, Standard after step 5, Realistic and Hardcore go to step 6
(`ProductCatalogue.LastStep`, option `economy_level`, set by the presets: Relaxed 0, Standard 1, Realistic 2,
Hardcore 3). With "Consequences of not paying" off no step is taken at all, any penalty rate is lifted, and only the
rating is affected, as in the first part.

**The penalty rate on a game loan** (`LoanHolds`, `GameLoanMath`). `Loan.dailyInterest` is a public number the game
takes each night. The loan's own interest and payment are kept on its mark (`OwnInterest`, `OwnPayment`) from the
moment anything is held; the penalty is the game's formula (`LoanHelper.CalculateDailyInterestPayment`) for 5 points
on the amount borrowed, added to the loan's own interest (the game keeps no rate, so this is the formula at the
loan's rate plus 5 to within one unit of rounding). The own figures are written back exactly: when three payments in a
row are made; when "Consequences of not paying" is switched off; when the economy stops (the option, a fault, the
city unloading); by the save tools; and **every time the game writes a save** (`CreditDesk.BeforeSave` on the game's
save event, which runs before the game serialises): a save file therefore never holds anything but the game's own
figures and loads in the plain game as it would without the mod, with or without "prepare this save". What is held
stays noted in the credit file and is written again within a second and on the next hour event, which is the first
thing the game's new day does, before its run. If the game recalculates a held loan (its interest multiplier was
changed) or the player changes the payment in EconoView, the new figure is taken as the loan's own. A debt
arrangement uses the same mechanism (interest and payment are nothing while it runs).

**Repossession of a vehicle** (`CreditDesk.TakeVehicles`): never while the player is in it
(`GameInstance.ActiveVehicleId`, `controlledByPlayer`), within 30 m of it, or looking at it
(`StreetSpots.IsInView` through `EconomyBridges.InView`); then with the game's own `VehicleInstance.Delete`. Its cargo
goes with it, and the notice says so beforehand.

**The claim** (`CreditDesk.Claim`): `EconomyBridges.FileClaim` (Wiring/DebtLinks) calls `DebtClaims.File` in
`Scripts/Justice/DebtClaims.cs`, a new file that adds a `JusticeSuit` to `JusticeState.Suits` with Justice's own
numbering and a text from the Legal contact. From then on it is Justice's: listed and answered in the Legal file
(settled for 60%, or contested with Justice's own chance), judged on the day after its date, and what is owed becomes
one of Justice's civil debts, collected as Justice collects all its debts. The account is closed here (a game loan
is removed from `GameInstance.Loans`; nothing else in the game refers to a loan). A judgment shows on the rating
through `EconomyLinks`, which already counts Justice's civil debts and posts `CreditKind.Judgment`. With Justice
off there is no court: the debt stays as it is. Justice's suit can be contested and won; then the lender gets nothing.

### Consolidation and early repayment

`CreditDesk.Consolidation` / `Consolidate`: every game loan, the card's balance and the overdraft's are paid off by
ONE new game loan at the chosen bank at today's rate for the rating, for what is owed plus the fee (1%, at least 25:
the fee is in the loan, so no cash is needed). Offered at or over the threshold, if the new loan fits what that bank
lends at the rating, and not while one of the game's quest loans is open. Finance shows before and after: owed,
daily cost, interest still to pay, days (`ProductTerms.Before` / `After`; a card or overdraft has no end, so its
interest is counted over the new loan's days). It posts one `Application` and one `Consolidated` line, not early
pay-offs. The card and the overdraft stay open with nothing owed.

### The private lender

`SharkDebt` / `SharkRules`, contact "Private lender" (`reallife-lender`). It makes itself known by a text the first
time a bank refuses the player (`EconomyDirector.TellRefused`) or the rating is under the threshold at a day's end.
It lends 500 to 5,000, or up to 10 days of average daily income if that is more; 3% of the sum is added each day; all
of it is due in 10 days; one extension by 10 days costs 15% of what is owed; what is owed never passes 3 times the
sum. It is not an `ICreditAccount` and posts nothing: it is not in the rating and paying it builds nothing. Past the
due day: day 1 a text; day 3 the player is set upon in the street if on foot and out of doors, and the cash on them is
taken against the debt; day 6 one of their businesses is damaged (a repair bill of 10% of the debt, 150 to 1,500);
then one or the other every 3 days. On Relaxed every act is a text. An act that cannot happen yet waits and is tried
each game hour; nothing happens while `EconomyBridges.Held` says the player is held (the game's dialogs, time skips,
hospital and pause through `LifeEvents.IsSafeForEvent`, a panel or test of the mod's, a large street scene, the police,
prison, a bus ride).

**Bridges to Crime and Business.** Neither has a public way in: `CrimeDirector` stages its scenes from its own deck
and has only the test entry `StageNow`; the business-pressure feature deals its issues from its own deck and has
only `Test`. So nobody walks up to the player: the assault is the injury a mugging gives
(`LifeEvents.Injure`, 12 hours hurt, 8 energy, never enough to make a tired player faint) with a notice, and the
damage is a repair bill with a notice. A staged scene would need an entry added to `Scripts/Crime`.

### Debt advice, a debt arrangement, bankruptcy

Debt advice (contact "Debt advice", free) is offered by a notice when something has been overdue 10 days running or
a claim has been filed. The call explains the three ways with the caller's own figures and changes nothing. The two
that change anything are in Finance, Accounts, "Ways out of debt", where the game's question box confirms them (the
red one for bankruptcy).

- **Sell and settle**: the game's own figures (`PlayerHelper.GetPersonalWealth`). Nothing is sold by the mod.
- **A debt arrangement** (`CreditDesk.Arrange`): every game loan and every unsecured account of the mod's stands still
  (no interest, no fees, no steps; a game loan through `LoanHold.Frozen`) and is paid by one daily payment of 20% of
  the average daily income, at least 25, shared out in a fixed order. The rating has a ceiling of 560 while it runs
  and for 20 days after (`ReliefRules.Ceiling`, applied in `CreditScoring.Compute`). Three missed payments end it and
  everything runs again. The private lender and secured finance are not part of it.
- **Bankruptcy** (`CreditDesk.Bankrupt`): every game loan (removed from `GameInstance.Loans`), every account but a
  mortgage that still has its building, and the private lender's debt are written off; open claims are withdrawn
  (`DebtClaims.Withdraw`); a vehicle on finance goes back (taken out of sight as above, its proceeds the creditors');
  cash above 500 goes to the creditors; fines and court debts stay. The rating's ceiling is 300 for 60 days, during
  which `CreditLedger.CanBorrow` answers `CreditRefusal.Bankrupt` whatever the score (a flag of its own:
  `ReliefRules.NoCredit`), then 450 rising by 2 a day. Once in 180 days. Not offered while the game's own bankruptcy
  quest (`SQBankruptcy`) is active (the page points to the game's) or while the tutorial's first-business loan is open.

### The Finance app

Five tabs (`AppShell.SetTabCount(5)`). The rule that keeps the white box short: a list is rows of the table, which
scrolls; the box shows only the row that is picked.

- **Overview**: "owed" and "leaves tomorrow" include every account; the table lists the mod's accounts and the
  private lender's debt under the loans.
- **Credit**: a line names the debt that hurts most now (the one furthest behind, else a limit over half used), and
  says so when the rating is being held under a ceiling.
- **Borrow**: under the banks, one row per kind of credit with "On offer", "Open" or "Not offered now"; picking one
  shows its page (what it is, the figures, the selectors, one confirm) or the sentence that says why not.
- **Accounts** (new): every account and loan with lender, owed, and how it stands (in order, payments behind, penalty
  rate, on a plan, in collections, to be repossessed, in an arrangement); the private lender's debt; an approval;
  "Ways out of debt". Picking a row shows its page and its buttons: pay an amount (the game's text box), pay it all
  off, draw cash, close it, pay what is overdue, agree a plan.

### The save: version 2

```
v=2~h:...~d:...~e:...~f:...~l:...~a:...~k:...~r:...
  h  as version 1, and at the end: the next account's number
  l  as version 1, and at the end: standing (12: misses|overdue|paid run|step|penalty|plan left|plan a day|plan used|
     call due|repossession day|repossession pending|claim number)|held (bits: 1 penalty, 2 frozen)|own interest|
     own payment|purpose (0 plain, 1 business, 2 consolidation)|security ("street#number")
  a  the mod's open accounts: id|kind|owed|agreed|rate (hundredths of a per cent)|carry|opened|term|instalment|next
     statement day|due day|statement left|minimum left|charged|secured|security value|bank's number|frozen|closed|test|
     standing (12)|security|security's name|bank's street
  k  the private lender: known|borrowed|owed|taken day|due day|grown day|extended|acted day|waiting|test
  r  the ways out and an approval: arranged|day|a day|total|paid|misses|ended day|bankrupt day|overdue since|advice
     told|advice known|collector known|approved kind|amount|deposit|term|rate|day|bank's number|bank's street|claims (n,n)
```

**Migration from version 1**: nothing is made up. A version 1 line has the first five parts and seven fields to a
loan; it is read as it is, the new parts are empty, and each loan starts in order with its lender. It is written
back as version 2. Closed accounts are never written; at most 24 accounts are. `EconomyCheck` round-trips a full
version 2 file, loads a literal version 1 line, and damages the new parts.

**With the mod removed.** A save always holds the game's own loan figures (see the penalty rate). The mod's own
accounts are in `reallife:economy` and do not exist in the plain game. "Prepare this save for removing the mod" (and
"reset the mod's data") call `CreditDesk.Leave` through the feature's cleanup: an approval's money goes back, every
hold on a game loan is ended for good, each account is paid from cash as far as the cash goes, and what is left is
forgiven; one `Economy: leaving this save. ...` line in Player.log lists each account with what was paid and what was
forgiven. A financed vehicle or building stays the player's. Three contacts are registered with the feature so the
save tools remove them.

### For the later phases

```csharp
ProductCatalogue.*                             // every figure; ProductKind, LadderStep, ProductAnswer
ProductTerms.RateBp(score, ownRate, points, multiplier) / OverdraftLimit / CardLimit / FinanceMost / Quote
CreditFile.Accounts / Account(kind) / AccountById / Shark / Relief          // the save's state (CreditLedger.File)
CreditAccount : ICreditAccount                 // Owed, Agreed, RateBp, RateNow, Ladder, Secured, Frozen, Test
AccountRules.RunDay / Pay / Draw / Cover / Finance / Repossess / AgreePlan / DueTomorrow / Interest
LadderRules.Missed / Paid / Takes / AgreePlan, LoanHolds.Begin / Apply / Restore / End
ReliefRules.Ceiling / NoCredit                 // a ceiling on the rating, and the hard flag CanBorrow asks
EconomyDirector.Instance.Desk                  // CreditDesk: Gate, Offer, OpenAccount, Pay, Draw, Approve, Business,
                                               // Consolidation, Arrange, Bankrupt, Owed, Level, ApplyHolds / RestoreHolds
BankTerms.House / At(street, number) / Secured // the bank the accounts are with; room a secured loan adds to a cap
Books.Tomorrow(file) / Businesses / AverageProfit / SalesYesterday
EconomyBridges.FileClaim / ClaimOpen / WithdrawClaim / Held / Assault / InView / KerbSpot / ContactIcon
DebtClaims.File / Open / Withdraw              // (Justice) a civil claim from outside the justice system
```

A new product is a value at the END of `ProductKind` (before `Count`), a name and a source in `ProductCatalogue`,
its terms in `ProductTerms`, and a case in `AccountRules.RunDay`; the model, the save, the steps, the app's list and
the ways out take it as it is. An interest rate that moves with a city economy (phase 3) goes in through
`ProductTerms.RateBp` and `LendingTerms.For`: an account keeps the rate it was agreed at (`RateBp`).

### Cost

Still no `Update`. Per second: the desk tests three flags (holds to write again after a save, an approval standing,
a vehicle waiting to be taken) and, only while one is set, compares one count or looks at one vehicle. Per game
hour: what is held on the loans compared (a handful), each secured account's security looked up in the game's list,
the private lender's waiting act. Per game day: each account's arithmetic (integers), the steps, one or two lines on
the credit file. Transactions are searched only when a purchase or a sale has just been noticed. The app builds a
page when a tab or a row is picked. No per-frame work and no per-frame allocation.

### Faults

Everything the director asks of the desk goes through `EconomyDirector.DeskDo`. A fault there logs one line
(`Economy: the credit desk ... is switched off for this session after an error in ...`), writes every loan's own
interest and payment back, and leaves the rating, the banks' terms, the app and the game's daily run going. The
accounts stay in the save. A fault in the first part still switches the whole economy off, and that stops the desk
first.

### Test buttons

"open a test overdraft, a test card and a test car on finance" (accounts marked as tests: never part of the rating,
never taking or paying money; the car is a lent `DeliveryVehicleInstance` at a free stretch of kerb), "the test
accounts miss 1 / 3 payments" (each step is taken and logged), "the collections call about a test account",
"repossess the test car" (taken once the player is 30 m away and not looking), "send the private lender's first
text", "work out a debt arrangement and a bankruptcy in the log" (on the test accounts and on the real figures;
nothing is changed), "put everything back" (test accounts and the test car removed; what is held on the game's loans
checked). Lines the tests put on the credit file are test entries: "clear test entries" takes them out.

### Log lines (with "Developer diagnostics" on)

- `Economy check (accounts at load, day 40): 3 account(s), 1 loan(s) with something held (0 without its own figures: cleared), 0 business loan(s) whose business has gone ..., 0 account(s) whose security has gone ...: PASS`
- `Economy check (account opened): CreditCard #2 with <bank>, 0 owed of 1200 at 26% a year: PASS`
- `Economy check (overdraft, day 41): 950 leaves tonight (loans 300, accounts 118, fines 0, rent 400, wages 132) against 610 in cash: the overdraft paid 340, 340 of 800 drawn: PASS`
- `Economy check (account, day 41): CarFinance #3: 9000 owed, interest 18, paid 118, now 8900 owed: PASS` (or `a payment of 100 missed (2 in a row)`)
- `Economy check (step, day 43): CarFinance #3: 3 payment(s) missed in a row on level 1 (secured), 300 overdue: PenaltyRate: PASS`
- `Economy check (rate, the penalty rate): the loan of 10000 at <street> 12: daily interest 13 to 21, daily payment 27 to 27; its own 13 and 27 are remembered, held at the penalty rate: PASS` (and `(rate, the game is saving)`, `(rate, after the save)`, `(rate, three payments in a row)`)
- `Economy check (approval)`, `(purchase on finance)`, `(approval ended)`, `(security gone, day n)`, `(repossession, day n)`, `(takings, day n)`, `(claim, day n)`
- `Economy check (collections): ...`, `(business loan)`, `(consolidation): 3 debt(s) of 7500 in all (...) replaced by one loan of 7575 ...: PASS`
- `Economy check (private lender, day n): 60 added, 2600 owed of at most 6000, due day 20, 3 day(s) past: Assault: PASS`
- `Economy check (debt advice, day n)`, `(debt arrangement, day n): 80 paid, 4920 left of 5000: PASS`, `(bankruptcy, day n): before ... after ...: PASS`
- `Economy: leaving this save. ...` (always written)

### Checks

`EconomyCheck` now compiles fourteen game-free files and runs 293 checks (`Checks2.cs` has the new ones): the terms
of every product at every score (nothing under the threshold, no limit falling, no rate rising), interest over 600
days to the unit, a vehicle and a mortgage paid off on their last day to the unit, missed days, early repayment, the
overdraft's cover and repayment, the card's statement cycle, the steps for each level from a scripted history, the
payment plan kept and broken, repossession sums and the claim after 10 days, consolidation's totals, the private
lender's growth, cap and acts, the arrangement, bankruptcy's totals, the 60-day flag and the rebuild, the save's
version 2 round trip, a version 1 file, damage in the new parts, and the penalty rate written and put back on a
model of the game's loan formula.

### Not verified (the game could not be run)

Everything on screen: that five tabs fit the bar (it sizes each tab to its label; five short labels are expected to
fit); that the product pages with three selectors and a row of buttons fit the white box, which does not scroll;
that a second row of buttons draws under the first. That the game's hour event reaches the desk before the run on
the day's first hour (the overdraft's log line shows it). That `GameEvent`-free detection of a purchase by count
catches every way of buying a vehicle (showroom, contract, delivery). The sale transactions' data keys
(`itemSoldInfo`, `address`) are read from the decompiled game and not seen in a save. That a vehicle deleted while
its scene is not loaded leaves nothing behind. The three contacts' pictures and their place in the phone's list.
That a conversation opened from a notification (`dialogUI.ShowDialog`) behaves as one opened from a call.

## Economy: bank apps

`Scripts/Economy/Banks/`. One app on the game's phone for each of the city's three largest banks, under the game's
own name for the bank, each that bank's slice of the player's money. The Finance app stays the one page for every
lender at once. Option: Money > Bank apps (`economy_banks`, on by default). No save data.

### Which banks, and how they differ

The banks are the game's own (`BankTerms.Lenders`: every special-service building whose settings are a
`BankSettings` and that has a registration). Branches with the same name are one bank. `BankRules.Rank` orders them
by the most each lends on its own terms (`BankSettings.maxTotalLoanAmount` as first read, before the rating writes
it), the largest first, by name among equals; `BankTerms.Banks` holds one branch of each in that order. The first is
`BankTerms.House`, the bank the mod's accounts have always been held with. The game's New York has two banks
(Vantander Bank, own cap 2,000,000 at 12%; Jensen Capital, 40,000 at 20%), so it has two bank apps; the log says so
once per city ("Banks: 2 bank(s) in the city, 2 bank app(s) on the phone (the city has fewer than 3 banks)").

| Rank | Rates | Card, overdraft, car finance, mortgage, business loan | Fee of a loan by phone |
|---|---|---|---|
| 0 (the largest) | as before | as before | as before (1.5%, at least 25) |
| 1 | one point more | 125% | half the usual share |
| 2 | one point less | 80% | three quarters of the usual share |
| 3 and on | as rank 0 | as rank 0 | as rank 0 |

The rule moves only the mod's own terms, and only inside the one desk that decides them: `CreditDesk.Offer(kind,
bank)`, `Business(shop, bank, ...)`, `BusinessQuote`, `AgreedAt`, `RateAt`, `LimitAt` and `PhoneLending.Quote` (the
fee). The Finance app asks the same desk, so its offers from a bank are that bank's in its app too; rank 0 keeps every
figure exactly as before (checked for every rating). The game's own loan at the bank counter keeps the game's terms by
the rating alone (`BankTerms.Apply`): the banks already differ there by their own settings. Under the lending
threshold (580) every limit is 0 at every bank whatever its share, and the rating's gate (`CreditDesk.Gate`,
`CreditLedger.CanBorrow`) is asked again at the moment anything is done: every app refuses in the bank's own words
and shows no button that could lend.

One overdraft and one card are held in all, as before, with the bank that opened them (the address on the account,
`CreditAccount.Street` and `Number`, which every account has always carried: an older save's accounts are at the
house bank already, so nothing is converted). A car or home approval records its bank (`Relief.ApprovedStreet`), and
the account it becomes is held there. A game loan belongs to the bank at its own `Loan.bankAddress`.

### The tabs

- **Accounts**: the current account (the cash, held with the house bank; another bank's app says where it is), an
  overdraft held here with pay, pay off and close, and the statement: every transaction of the game's history
  (`CashFlow`) that is this bank's, over a day, a week or a month. The house bank's statement is every movement of
  cash; another bank's is what names it (`businessName` on the game's loan transactions and the fees) or names an
  account it holds (`name` on the accounts' transactions).
- **Loans**: every loan owed to the bank, the game's own and the mod's (business, consolidation, car finance,
  mortgage, late fees). Pick one: its figures, how it stands, the next step if payments are missed, and pay an
  amount or pay it off. A game loan is paid as EconoView pays it (`LoanEntryUi.PayOffLoan`: the game's
  `ba:transaction_loanpayoff` under the bank's name, the amount off `remainingAmount`, the loan out of the list
  with `ba:gameevent_loanpaid` when nothing is left); the credit file then reads it as it reads a payment made in
  EconoView (`EconomyDirector.BringUpToDate`). A collections call that waits shows its three answers here as well.
  Changing a game loan's daily payment stays in EconoView (a button opens it).
- **Card**: a card held here: limit, balance, the share used, the rate, the statement, the minimum and its due day;
  pay the minimum, an amount or all of it; draw cash; close it at nothing owed. The table is the card's own
  movements over 30 days.
- **Apply**: the bank's loan, overdraft, card, business loan, car finance, mortgage and consolidation, each with
  its figures today; a page for each with the same question boxes as the Finance app.
- **Rating**: the bank's terms at five ratings from just under the threshold to the best terms, and the player's
  own: a loan up to (its cap before income and wealth), its rate, the card, the overdraft and car finance; what the
  next 50 points would bring at this bank; what would raise the rating.

### The game's parts

The panel is `GameUi.AppShell` (copied from MyEmployees) like the Finance app's; tables are `GameUi.DataTable` (a cell's
tone is a type of its own, `GameUi.CellTone`, so that a figure passed to `TableRow.Set` is always what the column is
ordered and summed by, never taken for a colour) with
one set of `TableSpec`s per app (`BankTables`), so an order or a filter is that app's. The app's name on its button
and in the menu's top bar is one text key, `reallife_bank_app` ("{bank}"), filled through the game's own
`TextLocalizationComponent.Arguments` (`PhoneAppDef.TitleArguments`, game-ui.md section 33): it still follows a
change of language. The icon is the game's app icon painted in one of three plain colours by rank; no bank's real
name, logo or colours are used.

### Cost

No Update. Once a second (the credit side's timer) `BanksDirector` compares the banks wanted with the apps on the
phone: a few comparisons while nothing changes, and at most one app put on a second (each copies a few hundred of
the game's objects, so they never land in one frame together). The badges (debts behind or with a call waiting) are
counted after the credit side's hour and day: one pass over the file's accounts and loans per bank. A page costs one
pass over the accounts and loans; the statement one pass over the game's transaction history (at most about a
thousand). Nothing runs while an app is closed.

### Checks

EconomyCheck (`BankChecks.cs`): the three profiles differ pairwise in rate, limit and fee; rank 0 gives every limit,
rate and fee exactly as before at every rating; under the threshold every bank's every limit is 0 and at or over it
every bank lends; limits never fall and rates never rise as the rating rises, at every bank; no rate under the floor;
the fees; the ranking of branches and banks; its cost. In the game: Options > Tests > "Check the bank apps against
Finance" writes `Bank check (figures)`: what every bank's Loans tab adds up with what no bank holds, against the
Finance app's total, and that every bank refuses under the threshold. "Open the first bank's app" opens it on Apply.
With diagnostics on, every page writes `Banks: <bank>, tab N written` with its height against the white box
(`InfoBox.Fit`), and a game loan paid from an app writes `Bank check (loan payment)`.

### Not verified (the game could not be run)

That five tabs fit the bar and the eight columns of the Loans table fit (the Finance app has seven); that the white
box holds the Card page with two rows of buttons; that the top bar shows the bank's name when an app opens from the
full menu and from the HUD phone, and the game's own app's name again after it; that `Icon-Coin` is among the
phone's sprites (the next glyph is used if not). The statement's matching by name relies on the data keys of the
game's loan transactions (`businessName`, read from the decompiled `LoanHelper` and `LoanEntryUi`).

## Economy: the city

The third part of the economy (`Scripts/Economy/City`; options "The city's economy" and "How far the economy
swings", ids `economy_city` and `economy_city_level`). The city gets an economy of its own: a central bank that sets
a rate each month, a price index, a cycle of good and bad times, districts with an income level, and wholesale prices
that move. It stands on the credit side above and runs only while that runs. No method of the game's is patched and
nothing of the mod's is ever written into saved game data.

```
Economy/City/       the first four are game-free and compiled by EconomyCheck
  MacroCatalogue      EVERY NUMBER of this part (the name: Scripts/CityLife already has a CityCatalogue)
  CityModel           CityState (what the save holds), CityDistrict, SupplyLine; CityModel (one day, one month, the central
                      bank's rule, and what the state means for footfall, prices and worth); CityAdjust (the adjustments' arithmetic)
  CityStore           the save's one line, "reallife:economy_city"
  CityEconomy         THE PUBLIC FACE for other features: State, BasePoints, PriceIndex, DistrictIncome(name), MonthTurned
  CityGame            THE GAME SIDE: districts from the game's neighbourhoods, the goods and their wholesale prices, the
                      player's businesses and their prices, the day's rent, wages and takings; CityBridges; CityShop
  CityText            the words: one way of writing each figure for the app, the paper, the Planner and a notification
  CityEconomyDirector keeps it going and applies it: the day, the hour, the save, the tests
Economy/Finance/FinanceApp.City.cs   three lines on the Overview and the page "The city"
Wiring/CityLinks.cs                  the calendar and the weather, business pressure (footfall, rivals), the paper, the Planner's rows
```

### The model

Everything is a whole number, so a run is exact and a save holds exactly what was in memory: the rate in quarter
points, inflation in hundredths of a per cent a year, the index in thousandths, an income in tenths of a point, a
price factor in thousandths. Nothing is random but `CityModel.Draw(seed, purpose, count, range)`: the seed is drawn
once when the economy is opened for a save and kept in its line.

| what | figures (`MacroCatalogue`) |
| --- | --- |
| a month | `daysPerYear / 12` game days, 3 at least (5 with the game's 60-day year). The model counts its own days (`Age`): a month turns when `Age` is a multiple of the month's days. Days that pass while the option is off are not counted. |
| the central bank | the rate in points on top of the banks' own: starts at 0, -2 to +6, in quarters. Decided on the last day of each month for the next (`CityModel.Rule`): +0.25 for each whole point inflation is over the target's band (one quarter at least), the same downwards when it is under; a slowdown takes a quarter off the decision and a recession a half; with inflation in its band and no bad times, a quarter back towards 0. One point at most a decision. |
| prices | inflation starts at the target, 2% a year, band of half a point, held between -1% and +12%. Each month it moves half-way towards: the target, plus the cycle's push (boom +1.2, slowdown -0.8, recession -1.6), plus a slow pressure (-1.5 to +3.5, moving by up to 0.6 a month and losing a tenth of itself), plus the month's own surprise (-1 to +1.5), less 0.6 for each point of the rate of the month before last (the lag). The index starts at 100 and rises each month by a twelfth of inflation; held between 50 and 400. |
| the cycle | boom, steady, slowdown, recession. A state lasts 2 to 6 months; the first 3 months are steady. What follows what, in per cent (each save moves each row's two chances by up to 8): boom to steady 70, slowdown 30; steady to boom 45, steady 20, slowdown 35; slowdown to steady 65, recession 35; recession to steady 65, slowdown 35. So a recession only ever follows a slowdown. |
| a state's numbers | footfall +6 / 0 / -5 / -12 per cent; what a customer spends x1.04 / 1 / 0.97 / 0.93; the chance of a business trouble x0.85 / 1 / 1.15 / 1.35; property x1.10 / 1 / 0.95 / 0.85; how often a rival undercuts x1 / 1 / 1.5 / 2. |
| districts | one for each of the game's neighbourhoods (`NeighborhoodHelper.NeighborhoodsData`, not the global one). The start is the mean of 70, 100 and 135 by the game's own shares of working, middle and upper class (`NeighborhoodData`), 100 where it gives none. Each month: the cycle's push (boom +1, slowdown -0.8, recession -1.6), up to 1.5 of its own either way, and 15% of the way back to its start. Always 60 to 140 and within 20 points of its start. |
| supply | six kinds of goods (`KindOfType`: fresh produce and flowers; groceries and drink; clothing and gifts; electronics and appliances; home and office goods; general goods). An item's kind is that of the first business type in the list that sells it (`BusinessType.businessProducts`), so a specialist's goods are its own even if the supermarket has them too. A kind's factor is 1 plus the season's push (`SeasonPush`), the weather's (`WeatherPush`), the city event's (`EventPush`) and a running shortage or glut. A shortage (+15% to +40%) or a glut (-8% to -24%) is drawn with a chance of 7% a day, 55% of them shortages, for 3 to 8 days from the next day; one drawn a day at most, two running or announced at most. The factor is held between 0.7 and 1.5. |
| strength | Relaxed 50%, Standard 100%, Realistic 110%, Hardcore 125% of every swing: the cycle's numbers, the pushes on inflation, a district's monthly move, supply's pushes and shocks. Relaxed never enters a recession (where the table says one, the slowdown goes on), and a recession running when Relaxed is chosen ends at the next day. |

### What it does to the player, and how each is done and undone

The rule for every effect: (a) through a mechanism the mod already has and restores; else (b) a figure of the game's
in memory that it reads when it computes, with its own value kept and put back when the feature stops, on a fault,
on unload and before every save; else (c) a dated transaction of the mod's own.

| effect | way | how |
| --- | --- | --- |
| **loan rates** | (a) | `LendingTerms.BasePoints` is added to the rating's points before the rate is rounded and held to its floor, so `BankTerms` writes it on the banks' settings with everything else and the bank's dialogue and the Borrow tab show it. A bank's rate is a whole per cent in the game, so quarters count once they add up with the rating's points. The mod's own accounts count in hundredths: an overdraft and a card are opened at the rating's rate and charged `AccountRules.BaseBp` on top day by day (`CreditAccount.RateAgreedNow`); car finance and a mortgage take it once, when approved (`ProductTerms.FixedRateBp`). A loan or account already made keeps its rate. Both numbers are the mod's own and are 0 while this is off. |
| **wholesale and import prices** | (b) | The game works a wholesale price out each time it is asked (`ItemHelper.GetWholesalePrice`: the item's `wholesalePrice`, the market's import index, the game's price multiplier), on the wholesaler's shelf, on a delivery contract and at the importer. The item is an asset in memory, not part of a save. `CityGame.ApplyWholesale` writes `own x the kind's factor x the index / 100` on every retail product with a wholesale price, once a day and only where the price is not already that; `RestoreWholesale` puts every item's own price back exactly. Nothing to restore before a save: the items are not in it. |
| **footfall** | (a) | For each business the player runs: the cycle's per cent, a tenth of a per cent for each point its district is away from 100, and its prices against what the district bears; together -25% to +15%, and nothing under 2% either way. Written through business pressure's own promotion figure: `BusinessGame.Footfall` asks `BusinessGame.Ambient` each time it writes a business's figure, so one writer puts an issue's change and the city's on it together, each inside its own bounds. Applied each morning with the day's customers drawn again; kept each game hour; put at the game's own before every save and until the next game hour; the game itself writes its own figure every night. |
| **price tolerance** | (a) | The game keeps each business's prices (`BuildingRegistration.retailPrices`). They are judged against the game's own price for the neighbourhood (`Item.DefaultMarketPrice` x `CitizenHelper.averagePriceIndicesInNeighborhoods`, plus the 0.3 the game itself allows a monopoly), as a mean over the price list, and that against what the district bears: half a per cent more or less for each point its income is over or under its start, times what customers spend in the cycle. Up to 2% over costs nothing; then 0.8% of customers for each per cent over, 10% at most; at or under, +2%. It is part of the footfall above. The game's own table is read and never written (its figures reach rivals' saved prices). |
| **rents and wages** | (c) | After the game's daily run, its own rent and wage transactions of the day that ended are added up (`CityGame.DayFigures`: by the game's categories) and one transaction each adjusts them by `index / 100 - 1`: "Rent, cost of living adjustment", "Wages, cost of living adjustment". None within 1% of 100. |
| **takings** | (c) | The same for the day's revenue transactions, by `index / 100 x what customers spend - 1`: "Takings, prices and the economy". The game's shelf prices cannot follow the index (its customers judge them against fixed prices), so the index reaches takings this way; without it rents, wages and wholesale would rise for ever against takings that could not. |
| **property** | shown only | "Worth today" on the city's page: the game's market value (`Building.GetMarketValue`) times the cycle, the district against its start (0.4% a point) and the index. The game's sale price is not changed: its inputs also set rents the game saves. |
| **rivals** | (a) | Business pressure's morning roll is multiplied by the cycle's trouble number and its "undercut" kind weighs 1.5 or 2 times as much in a slowdown or a recession (`BusinessDirector.RollScale`, `WeightOf`). A business with a profit on each of the last 14 daily statements may draw a rival: each day, with a chance of 6% times the trouble number, the one that made the most gets business pressure's own "a competitor undercuts you" through its own rules (`BusinessDirector.StartIssue`), 21 days apart at least. Its notification, its answers and its end are that feature's. |
| **credit risk** | none added | The rating's "owed against income" already falls with income. Nothing hidden is added. |

An adjustment that would take money is never taken beyond the cash in hand (`CityAdjust.Take`): what cannot be taken is
carried to the next day as it is, per stream, in the state. The credit side settles its day first (it calls the city's
economy when its own work on the day event is done), so an adjustment never takes the cash an account's payment was
waiting for. "Prepare this save for removing the mod" writes what
was carried off. The adjustments carry no category of the game's, so its daily statements, its tax and the
Overview's rent and wages lines stay the game's own figures.

### What the player sees

- **Finance, Overview**: "Bank rate: banks' own +1.25", "Prices: 104.2, up 0.8% this month", "The economy: slowdown,
  month 2", and a button "The city".
- **Finance, The city** (a page of the Overview, not a sixth tab): in the box the rate, the next decision (or what was
  announced for tomorrow), prices, inflation, the economy, what rents, wages and takings come to against the game's
  figures, what is carried; in the table the districts (income, the month's change), the six kinds of goods (today's
  price against the usual, what runs or starts tomorrow, "you stock it"), each business (district, customers, prices
  over or suiting the district) and each building owned (the game's value and today's).
- **The paper** (`News.Extra`): two pieces a day at most, in this order: a shortage or a glut from tomorrow, a rival,
  the rate decision (the day before it takes effect, with its reason), a change of state, the month's prices, a
  district that moved 2 points or more.
- **The Planner** (rows of the Finance source): the rate decision on the day it takes effect, once announced; a
  shortage or a glut from tomorrow in goods the player's businesses stock.
- **Notifications**: the rate moved while something is owed on an overdraft or a card; a shortage or a glut from
  tomorrow in goods the player's businesses stock. "Stock" is read from their price lists and delivery contracts:
  the game's purchase transactions do not name what was bought.

### The save

One line, `reallife:economy_city`: `v=1~h:...~d:...~s:...~n:...` (`CityStore`): the header's 22 numbers, the
districts (`name|start|income|a month ago`), supply (`factor|shock|from|days|size` per kind) and the pieces for the
paper that are not out of date. A second line, `reallife:economy_city_before_tests`, holds the first line as it was
before a test tool changed it, while test changes are in place. Both are registered with the feature. A reader takes
the fields it knows and skips parts it does not; a figure out of bounds is put inside them; a line that cannot be
read is replaced by a new economy.

### For other features: `CityEconomy`

Read-only and safe at any time. While the city's economy is off, not started or stopped by a fault every member
answers as a city with no economy of the mod's would.

| member | while it runs | otherwise |
| --- | --- | --- |
| `CityEconomy.Running` | true | false |
| `CityEconomy.State` | the `CycleState` | `Steady` |
| `CityEconomy.BasePoints` | the rate in points on the banks' own, -2 to +6 | 0 |
| `CityEconomy.PriceIndex` | the index, 100 at the start | 100 |
| `CityEconomy.InflationPercent` | per cent a year | 2 |
| `CityEconomy.MonthOfState` | the month of the state, from 1 | 1 |
| `CityEconomy.DistrictIncome(name)` | 60 to 140, by the game's own name for the neighbourhood (`ba:neighborhood_...`); 100 for one it does not know | 100 |
| `CityEconomy.MonthTurned` | an event, raised once on the game's day event after the month's figures are in place; a handler that throws is logged and stops nobody | never raised |

### Added to other features (all additive)

- `Economy/Lending/LendingTerms`: `BasePoints`, and `For(score, cap, rate, basePoints)`. `ProductTerms`: `BaseBp`,
  `WithBase`, `FixedRateBp`; `RateBp` is by the rating alone. `AccountRules`: `BaseBp`, `BaseMultiplier`.
  `CreditAccount.RateAgreedNow`. `ProductOffer.AgreedBp`. `EconomyDirector`: `Second`, `AfterHour`, `AfterDay` (its
  one-second timer and its handlers of the game's hour and day events call the city's economy, which has no timer
  and no handler of its own in the game's events), `OpenCity`. `Finance.OpenCity`.
- `Business/BusinessCatalogue`: `AmbientMostDip`, `AmbientMostBoost`, `ClampAmbient`, `PromotionFor` with an ambient
  part. `BusinessGame`: `Ambient`, `LastOwn`, `LastWritten`. `BusinessDirector`: `RollScale`, `WeightOf`,
  `StartIssue(kind, reg)`, `Footfall(reg, redraw, newDay, out own, out written)`.
- `Social/News`: `Extra`, called once when the paper is written, after the business piece.
- `Wiring/EconomyLinks`: one call to `CityLinks.Rows` in the Finance source of the Planner.

### Cost

No Update, no timer and no handler of its own in the game's events, nothing per frame and nothing allocated per
second. Once a second (on the credit side's timer): four comparisons.
Each game hour: two comparisons for the rate, and one promotion figure written per business that carries a change.
Each game day: one tick of the model (a few dozen integer operations), one comparison for each good with a wholesale price and a write where it differs, one pass over the player's businesses and their price lists, one
pass over the game's transaction history (the last thousand entries) for the adjustments, and the day's customers
drawn again for each business whose footfall is changed. That last one is the only work of any size: it is the
game's own draw (`CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness`), done once more for those
businesses at midnight, exactly as business pressure does for a business with an issue. With diagnostics on the
"applied" line gives the time of the whole day's application in milliseconds. A business whose three parts come to
under 2% is left alone.

### Options, tests, diagnostics

- "The city's economy" off: the rate is the banks' own, every wholesale price and footfall figure is the game's, no
  adjustment is made and the model stands still (its state stays in the save).
- "How far the economy swings": the four presets (Relaxed, Standard, Realistic, Hardcore).
- Test buttons: show the city's economy in the log; step a month; put the economy in a boom, steady, a slowdown, a
  recession; a shortage, a glut from tomorrow; bank rate up, down one point; raise, lower this district's income by
  10; put the city's economy back (the state from before the first test change).
- Always written: `City economy: opened for this save on day n. ...` or `state loaded (version 1) ...`;
  `City economy: switched off for this session after an error in ...`.
- With "Developer diagnostics" on:
  - `Economy check (city, self-check at load): 312 good(s) with a wholesale price, 0 not as expected, the bank rate at the banks' own: PASS`
  - `Economy check (city, applied, the day, day 41): the rate +0.5, 104 wholesale price(s) written (fresh 1.134, ...), 3 footfall figure(s) written, 3 with the day's customers drawn again: <business> -7% (cycle -5, district -2, prices 0 at 99.1% of what the district bears, promotion 62 to 55); ...; 1.8 ms: PASS`
  - `Economy check (city, decision on day 40): inflation 3.10% against a target of 2.00%, the cycle Steady, the rate +0.25: moved by +0.25 from tomorrow (PricesHigh): PASS`
  - `Economy check (city, month 8 on day 41): the rate +0.25 to +0.5 (PricesHigh), inflation 3.10% to 2.95% a year (...), the index 103.9 to 104.2, the cycle Steady to Steady (month 3, 2 left): PASS`
  - `Economy check (city, adjustment for day 40): rent 1200 by the game, factor 1.042, so 50 more to pay; carried from before 0; 50 taken, 0 carried to tomorrow: PASS`
  - `Economy check (city, rival): ...`, `Economy check (city, before a save): 3 footfall figure(s) put at the game's own until the next game hour ...: PASS`
  - `Economy check (city, restored, the option is off): 104 wholesale price(s) and 3 footfall figure(s) put back, the bank rate back at the banks' own. ...: PASS`
- A fault anywhere in it switches the city's economy off for the session with one line, puts every figure back and
  leaves the game's daily run and the credit side alone.

### Checks

`EconomyCheck` compiles four more game-free files and runs 424 checks (`Checks3.cs` has the new ones): twenty years
of the model from 60 seeds at each strength, with a 60-day and a 365-day year (every figure inside its bounds every
day, the first three months steady, no recession on Relaxed, a decision announced on the last day of every month and
exactly what takes effect, every shortage and glut announced the day before unchanged), the central bank's rule case
by case and over its whole range, the footfall and tolerance arithmetic, a district's way back to its start, the
adjustments (rounding, the 1% band, carrying when cash is short, nothing lost over thirty days), the rate hook
(nothing when off, added before the rounding and the floor, what follows it and what does not), the save's line
(round trip at any point, a run carried on from a save, a later version, damage) and determinism.

### Not verified (the game could not be run)

That the game's neighbourhood data is loaded when the economy starts and what class shares each neighbourhood has.
That every wholesale path reads `Item.wholesalePrice` at purchase (read from the decompiled game: the shelf sign,
`PlayerItemPurchaser`, `DeliveryContract.TotalPricePerDelivery`, the importer). That the game's item assets keep a
written price between a city unloading and the next one loading (the restore on unload is there for that). How the
kinds of goods divide the game's items (the "show" test button counts them). That the day event follows the game's
rent and wage transactions as the decompiled `GameManager.NewDay` says. That three more lines and a second button
fit the Overview's white box, which does not scroll, and how the city's page reads with every tab unmarked. The
time the second draw of customers takes with many businesses.
## Economy: investors and the market

Phase 4 of the economy (`Scripts/Economy/Ventures`, options "Investors", "The stock market" and "How demanding
investors are", ids `ventures_investors`, `ventures_market`, `ventures_level`): investors who put money into a
business of the player's for a share of its daily profit, a pitch that wins or loses that money, a stock market of
the city's own companies moved by the news the mod already makes, and a page that reads the game's own lettings as
an investment. It has its own director, its own line in the save and its own app on the phone ("Ventures"). No
method of the game's is patched and nothing is ever written into the game's business or property records: money
moves through the game's own transaction call under the mod's own keys, and everything else is the save line.

| File | What it is |
| --- | --- |
| `VenturesCatalogue.cs` | every figure, the four levels, the names; `VenturesRandom` (seeded rolls that are a hash of what they are about) |
| `VenturesModel.cs` | `Investor`, `Offer`, `Pledge`, `Deal`, `Company`, `Holding`, `BusinessFigures`, `VenturesFile`; `IVenturesClimate` with `SteadyClimate` and `ClimateFrom` |
| `Cast.cs` | the save's investors and companies from its seed |
| `Deals.cs` | `OfferRules` (an offer, a counter, taking it up, money promised), `DealRules` (a deal's day, the ladder, buy-out), the transaction keys |
| `Pitch.cs` | the bank of 41 questions, which are asked, what an answer is worth, the check of a figure, the decision |
| `Market.cs` | a market day, the news tables, trading, dividends |
| `VenturesStore.cs` | the save line |
| `VenturesBridges.cs` | what Ventures asks of the rest of the mod; `VenturesPhone.Open(tab)`; `VenturesText` |
| `VenturesGame.cs` | what is read from the game (a business's figures, the true answers, rivals, kinds of business, rents) and `VenturesWallet` |
| `VenturesDirector.cs`, `.Tests.cs` | the component: start, the day, the hour, the save, what the app asks for, the test tools, the save tools |
| `PitchMeeting.cs` | the meeting in the game's list window |
| `VenturesApp.cs`, `.Investors.cs`, `.Market.cs`, `.Property.cs` | the app |
| `Wiring/VenturesLinks.cs` | the bridges filled in, and the Planner's rows |

The first seven are game-free: the offline checks compile them as they are.

### How it runs (no per-frame work)

`VenturesDirector` has no `Update`. A coroutine on the real clock looks once a second: it starts when the save is
ready, stands down when both options are off, keeps its handlers on `GlobalEvents.onNewDay`, `onNewHour` and the
save relay (two reference comparisons while nothing has changed), puts the app on the phone, and asks a meeting in
progress whether its window is still there. Everything else is done on an event:

- **the day** (after the game's daily run, so yesterday's statements exist): offers and promises that have lapsed;
  each deal's share of yesterday's profit and its target; every seventh day the look at the businesses for an
  offer; the market's day and, every fifteenth, its dividends. Transactions are dated on the day that ended, as
  the game dates its own run.
- **the hour**: only while money is promised for a business not yet open: has one been opened.
- **the save**: the line is written.
- **the app** being opened or clicked: pages are written again; nothing runs while it is closed.

A fault anywhere logs one line (`Ventures: switched off for this session after an error in ...`) and Ventures does
nothing more that session; what the save holds is left as it is. Days on which Ventures did not run (an option off,
a fault) are not made up afterwards: no share is taken for them and the market does not move.

### The cast (`Cast.Make`, once per save from its seed)

- Two or three tycoons (the first patient, the second demanding), three angels (patient or even), two venture firms
  (demanding). Names are invented: a first and a last name from two lists of sixteen, a firm from a list of six.
- Smallest sum: angel 4,000 / 5,000 / 7,500; tycoon 25,000 / 40,000 / 60,000; firm 75,000 / 100,000 / 150,000.
  Largest: angel 30,000 / 45,000 / 60,000; tycoon 400,000 / 750,000 / 1,500,000; firm 1,000,000 / 2,500,000 /
  5,000,000. Diligence (how hard they check a figure): angel 25 to 45, tycoon 50 to 75, firm 78 to 95. Share of the
  profit they take: angel 8% to 20%, tycoon 15% to 35%, firm 20% to 40%. Each likes two of five sectors (shops, food
  and drink, offices and services, leisure, trade and industry; `VenturesCatalogue.SectorOf` sorts the game's
  business types by the words in their ids, "ba:businesstype_webdevelopmentagency" being an office).
- Twelve companies: one invented firm in each of eight sectors (transport, property, retail, food, security,
  utilities, banking, entertainment), then the game's own named rivals (`RivalsHelper.GetSpecialRivals`, four at
  most, listed as "<name> Holdings" in retail, food, entertainment and property in turn), then second invented
  firms to make twelve. Each starts at 20 to 240 a share, with an ordinary daily move of 0.8% to 2% and a dividend
  of 0.4% to 1.1% of the price each payout.

### An offer that comes to the player (`OfferRules`, `VenturesDirector.Look`)

Every 7 days each business is looked at. One that made a profit on at least 10 of its last 14 days and whose mean
profit over the last 7 is at least 1,500 a day gets an offer with a chance of 25% (40% if a free tycoon likes its
sector; half of that in a slowdown, a quarter more in a boom: see the climate below). The tycoon values the business
at 60 days of that profit and asks a share a quarter (patient), a half (even) or three quarters (demanding) of the
way up their range; the sum is that share of the value, cut to their largest sum (the share shrinks with it) and to
the room left on the business (all investors together hold 60% of one business's profit at most, two deals to a
business, eight deals in all, four offers open). Under their smallest sum there is no offer. The offer names a
growth target (10% in 30 days patient, 15% in 24 even, 25% in 18 demanding, scaled by the level) and waits 5 days.

Accept (the money is paid in at once), refuse, or counter once: 25% more money for the same share, or a share a
quarter smaller for the same money. The investor agrees with a chance of 40% / 30% / 15% by temperament plus 5
points for each profitable day over ten, meets half way (half the step) with 40% / 40% / 35%, and otherwise holds to
the first terms. The roll is a hash of the save's seed and the offer, so loading and trying again changes nothing.

### The pitch (`Pitch`, `PitchMeeting`)

"Raise money" in the app: a business the player runs, or a kind of business not yet open with a budget (10,000 to
500,000 in nine steps); then an investor (one approach to each in 10 days; a tycoon backs no business that is not
open). The phone is put away and the meeting is held in the game's list window, locked, the player standing still.

- **Questions**: 6 (angel), 7 (tycoon) or 8 (firm), drawn from the 41 that fit: facts / plan / founder in the mix
  2/2/2, 3/2/2, 4/3/1. Where too few facts can be asked (no debts, no record, no credit file) the plan fills up.
- **A fact** (14 of them: yesterday's and the week's takings, yesterday's and the week's daily profit, staff, age,
  profitable days of 14, cash, credit rating, debts, convictions, businesses run, missed payments, what loans cost
  a day) is offered as the true figure (money in two figures that count) and one or two that sound better (money
  30% and 80% more, a count likewise and by one and two at least, a rating 40 and 100 higher; a debt halved and none). A
  true figure scores 4 to 10 by how strong it is. An untrue one scores as if true unless caught: the chance is the
  investor's diligence (moved by the level and, under a name of 70, by half the shortfall) times 45% for the
  nearer figure and 90% for the further, between 3% and 95%. Caught: the meeting ends, that investor sees nobody
  for 60 days and the founder's name with all investors falls by 25 (of 100; it starts at 70).
- **The plan** (18 questions, three answers each): points for each kind of investor, moved by their temperament
  (patient: careful +2, bold -1, personal +1, reckless -2; even: +1, +1, 0, -2; demanding: -1, +2, -1, -1) and for
  two questions by the business's sector (+2 or -2). The best answers differ by investor.
- **The founder** (9 questions): each answer says something the save shows or not (businesses run and how long,
  jobs held, missed payments and bankruptcy on the credit file, convictions, other backers, a debt in order). Said
  without the record behind it, it scores 1.
- **Score** = points x 100 / (questions x 10), +5 for a sector the investor likes, + (name - 70) / 5. Needed: angel
  45, tycoon 55, firm 62 (the level adds -5, 0, +3, +6). At or over it, with "over" the part of the way from needed
  to 100: the business is valued at 40 to 90 days of its true recent profit and the investor takes from the most to
  the least of their range; the sum is their share of that value inside their smallest and largest sums. A business
  not yet open gets 30% to 70% of its budget (from an angel or a firm), paid when the player runs one more business
  of that kind than on the day of the promise, within 14 days (the deal starts on the one of that kind with no deal
  and the fewest daily statements: the one just opened); else the promise lapses and nothing is owed. An angel
  backs a business under 30 days old with their smallest sum whatever its profit.
- **Worked example**: an even-tempered tycoon (sums 25,000 to 750,000) asks 7 questions and the answers score
  8, 10, 7, 8, 6, 9, 8 = 56 of 70, a score of 80. They need 55, so "over" is 25 / 45 = 0.56: the business, making
  4,000 a day, is valued at 40 + 0.56 x 50 = 68 days = 272,000, and their share is 35% - 0.56 x 20 = 23.89%. The
  offer is 64,900 for 23.89% of each day's profit. They say what helped most (the answer worth 10) and what counted
  against the founder (the one worth 6). `EconomyCheck` has this case.
- The decision is said in two or three sentences and, where there is an offer, the page has "Accept the terms",
  "Think it over" (the offer waits 5 days in the app, where it can also be countered) and "Decline".

### A deal (`DealRules`)

- Each day the investor is paid their share of yesterday's profit of that business from the game's own statement
  (`GameInstance.financialSummaries`), never of a loss, by a transaction ("Investor's share: <name>"). Cash that is
  short is carried over and taken first the next day.
- **Target**: the recent daily profit grown by the investor's figure by a day. Met: mood +20, the name +3, a second
  round is offered at the next weekly look (70 days of profit; its money and share are added to the same deal), and
  a new target is set from the new profit, unless the investor has had their money back: then they set no more.
  Missed once: a warning and the same days again. Twice: a demand, to be answered in 5 days: a seat on the board
  (10 points more of the profit for 30 days; a firm already has one) or a promise to cut the mean daily costs of
  the last 14 days by 10% within 14 days (tested from the statements; broken, it counts as the next miss).
  Unanswered, the investor takes the seat (a firm: the promise). Three times: a demanding investor calls their
  money back (what they put in less 50% of what they were paid, 25% of what they put in at least) over 30 / 30 /
  20 / 14 days by level; the share stops. After 5 daily payments missed running the rest is filed as a civil claim
  through `EconomyBridges.FileClaim` (Justice judges and collects it; with no court it stays owed). Anybody else,
  and everybody on Relaxed, loses interest: they keep their share and set no more targets, and one who has not had
  their money back will not back the founder again and costs 10 of the name.
- **Buy-out** at any time: the investor's share of 75 days of the mean profit of the last 14, never less than 110%
  of what they put in; for a sum called back, what is still called.
- **The business sold or closed**: the investor is owed what they put in less what they have been paid, by the day
  over the level's days (nothing if they have had it back).
- **Levels** (the presets set `ventures_level`): growth asked 50% / 100% / 125% / 150% of the investor's figure;
  diligence -15 / 0 / +10 / +20; score needed -5 / 0 / +3 / +6; nobody calls money back on Relaxed.
- What cannot be made safe by a rule is bounded: money for a business not yet open rests on a budget nobody can
  check, so a tycoon gives none, a firm's is called back when growth stops, and an angel's (60,000 at most) is lost
  to the angel once, with the angel and 10 of the name.

### The stock market (`Market`)

One step a day, at the day event:

    move (in hundredths of a per cent) = 4 + the company's ordinary move x a seeded bell-shaped number
                                         + 0.004 x the gap to what the company is worth + the day's news + climate

What a company is worth rises by 0.04% a day, so prices follow it over the years. The news of one day is capped at
1.5% for a company, less than the 2% it costs to sell and buy back, so reading the news before the market does is
worth nothing. The climate adds or takes 0.02% a day in a boom or a slowdown. A price never goes under 1.00.

| News | Where it is read (public state only) | Sectors moved (hundredths of a per cent) |
| --- | --- | --- |
| storm, rain | `Events.Weather.Sky`, the game's `RainHelper.isRaining` | storm: transport -60, retail -40, entertainment -40, food -20, utilities +40, security +10; rain: retail -15, entertainment -15, transport +10 |
| heat wave, cold snap, ice | `Events.Weather.SpellOf(the day that ended)` | utilities +40 / +50 / +20; food +20 (heat); transport -10 / -50 (cold, ice); retail -20 / -15 |
| street market, parade or run or fireworks, protest, film crew | `Events.EventCalendar.For(the day that ended)` | food and retail up for a market; entertainment +40 for a parade or a film crew; retail -30 and security +30 for a protest |
| crime, break-in, road accident | `News.Recent(kind, 1 day)` (whether there was one) | security +40 / +50; retail -20 / -25; property -15 / -10; transport -30 (accident) |
| a council policy moved | `CivicPolicy.Position(lever)` against what Ventures last saw | by lever: policing, business tax, trade levy, grants, rents, transit, night life, permits, events, wages |
| an election counted | `CivicDirector.Instance.Government.State.Number` against the number last seen | every company +15 |
| a rival's move | `BusinessState.Issues`: a price war or a smear begun yesterday | the named rival's own company +60, other retail -10 |
| the bank rate | `IVenturesClimate.BankRate` against the rate last seen | banking +40 and property -30 for each point up |

Not linked, because nothing usable is public: how many crimes or accidents a day had, the paper's own items by
kind (`News.Today` holds finished texts), a campaign's polls.

Trading: whole shares at the last price; a purchase is rounded up to whole money and a sale down; commission 1%
of each, 5 at least; 5,000 shares of one company at most. Dividends every 15 market days on the price of that day.
The last 30 closing prices of each company are kept; the app shows the move over them as a figure and their low
and high (no chart is drawn).

**It is no money printer** (`EconomyCheck`, written out on every run): over ten game years of 60 days from eighty
seeds, with news made up at about the city's rate, buying every company on the first day and holding returns
5.05% a year after commission, and the unluckiest and luckiest holders end with 1.50 and 1.81 times their money.
Seven rules of thumb all do worse than holding: buy after a fall and sell after a rise (-11.96% a year against
holding), the opposite (-11.19%), buy under the thirty-day average and sell over it (-3.83%), buy for the dividend
(-9.24%), buy on the day's good news (-8.15%), sell on the day's bad news (-1.22%), hold but sell what has run a
tenth over its average (-0.72%). The check fails if holding leaves 3% to 7% a year or any rule beats it by 1%.

The game's own investment funds (EconoView) are left alone: a fund follows its yearly rate; a share is one company.

### Property

The game already lets every building the player owns (`Helpers.RealEstateHelper.RunDaily`): tenants come and go
by the rent set in BizMan against the market rent, the rent is paid in each night (`ba:transaction_rentrevenue`),
a building for sale is sold to a competitor, and its market value follows its district. So no second system was
built. The Property tab only reads: each building's price and day of purchase (`RealEstate`), its market value now
(`Building.GetMarketValue`), how much is let and what that brings a day (`OccupancyPercentage`, `DailyIncome`),
the rent of the last 7 and 30 days from the game's daily statements (`FinancialSummary.realEstateStatements`, kept
for a year; the transaction list is cut after six days), the yield of a year's rent on the price paid, and the
mortgage on it if Phase 2 has one. Repairs, empty months and tenants of the mod's own were left out on purpose.

### The save line

`modData["reallife:economy_ventures"]`, version 1, a few kilobytes at its fullest (`VenturesStore`):

    v=1~h:...~i:...~o:...~p:...~d:...~c:...~s:...

Parts are divided by `~`, records by `;`, fields by `|`. `h` the header (seed, start day, name with investors,
next id, meetings held, the day of the last weekly look, the last day settled, market days, the last market day,
totals, the council's policies, elections and bank rate as last seen, what the app has not shown); `i` the investors
(11 numbers each); `o` the open offers (4 at most: 13 numbers, the business's key, its name, a new business's
type); `p` money promised (3 at most: 11 numbers, the type); `d` the deals (8 at most: 24 numbers, key, name); `c`
the companies (12: 9 numbers, what it is worth, a rival's listed name, 30 closing prices divided by commas); `s`
the shares held. A later version adds fields at the end of a record or a part with a new letter; this reader takes
what it knows and skips the rest. A line that cannot be read is replaced by a new file. While the test tools have
moved the market, the save keeps the market as it stood before them.

### Bridges

`Wiring/VenturesLinks.cs` fills `VenturesBridges`: `DayNews`, `Lever`, `Elections` (the table above), `JobsHeld`
(`CareerState.History` and the present employer), `Held` (custody, prison, a hearing, the driving school, the
police, a bus ride), and adds the Planner's source (an offer's last day, a promise's last day, a target's day, a
demand's last day, tomorrow's payment of a sum called back, the next dividend day). The legal record, the courts
and the other debts are the economy's own bridges (`EconomyBridges.Legal`, `FileClaim`, `OtherDebts`), used as they
are. Members added to other features: none.

**The climate.** `VenturesBridges.Climate` is an `IVenturesClimate` (a phase of -1, 0 or 1 and a bank rate) and is
left at `SteadyClimate`. Phase 3 (`Scripts/Economy/City`) is not in this tree. When both are merged, one line in
`VenturesLinks.Connect` joins it on (with whatever `CityEconomy` calls its three states and its rate):

    VenturesBridges.Climate = new ClimateFrom(() => (int)CityEconomy.State - 1, () => CityEconomy.BankRate);

### Test tools (options page, "Show test tools")

A tycoon's offer for the best business; a meeting with an angel, a tycoon, a venture firm (about the best business,
or with none about a business not yet open); the investors and companies in the log; the market stepped 30 days
(no dividend; the market as it stood is kept and is what a save keeps); a dividend paid now (with no shares, ten
test shares of the first company are given first); an investor calls their money (a deal there is, or a test deal);
"Test: put ventures back" (test offers, deals, promises and holdings removed; the market, the name with investors
and the investors' days as before the first test of the session; money a test paid stays).

### Log lines (with "Developer diagnostics" on)

- `Ventures: a file is opened for this save on day 12 (seed ...): 8 investors, 12 listed companies.` (once per save, always)
- `Ventures check (start): file version 1, 2 deal(s), ... Cast: ... Companies: ...: PASS`
- `Ventures check (the weekly look, day 21): <business> made a profit on 8 of its last 14 days and 900 a day lately (needs 10 and 1500): no offer.`
- `Ventures check (offer, day 21): <name> (Tycoon, Patient), a business with 12 profitable days of 14 and 3200 a day lately, chance 40%, roll 17: 38400 for 2000 bp (60 days of profit), growth 10% in 30 days, answer by day 26.`
- `Ventures check (pitch 3, day 22): <name> (Firm, Demanding, diligence 88) about <business> (...): 8 questions. True figures: ...`
- `Ventures check (pitch 3, question 2 of 8): reallife_ven_q_f03 (Fact), answer 2 = 1200 against the true 900: untrue, chance 39%, roll 71, not caught, 8 point(s), 15 of 20 so far.`
- `Ventures check (pitch 3, decision): points 7,8,...; liked sector True (+5), name with investors 70; score 71 against 62; recent profit 900 a day valued at 50 days, sums 100000 to 2500000: TooSmall 0 for 0 bp; best answer 2, worst 6.`
- `Ventures: pitch 3 to <name> about <business>: 49 of 80 points, score 66 (needs 62): TooSmall.` (always)
- `Ventures check (day 23): deal 4 with <name> on <business>: Paid 640 (yesterday's profit 3200, share 2000 bp, ...).`
- `Ventures check (market day 11, game day 23): 2 piece(s) of news (Storm, Policy 7 by 100), climate 0. <company> 43.20 to 42.71 (news -60 bp, Storm); ...`
- `Ventures check (dividend, day 27): market day 15, 3 holding(s) paid 412 in all.`
- `Ventures check (trade): buy 100 of <company> at 43.21: Done; cash 100000 to 95635, now holding 100 at a cost of 4365.`
- `Ventures: tab 1 written, 12 row(s); 1.9 ms.`
- `Ventures: leaving this save. Shares sold for ... in all, with no commission. ...` (always, from the save tools)

### Checks

`EconomyCheck` compiles the seven game-free files too and `VenturesChecks.cs` adds 195 checks (488 in all): the
cast for 300 seeds; the offer rule, the counter over 600 offers, a second round, money promised; a deal over 400
days of profits and losses to the unit, arrears, buy-out, a business sold; the ladder day by day for a demanding
tycoon, a patient one, Relaxed, a firm's promise kept and broken, the claim; the save line (round trip, a later
version's line, damage, the caps, the test tools' market); the bank and the draw of questions, and the words of
every question in `en.json`; truth never fatal, a caught figure always ending the meeting; the best answers
differing by investor; the founder's answers against the record; the decision's sums, the worked example; the
market's determinism, news, cap, commission, limit, dividends, and the ten-year runs above.

### Not verified (the game could not be run)

Everything on screen: that four tabs fit the bar; how many lines the white box holds (the deal's page with a
demand is the longest: about twelve lines and two rows of buttons); that the list window opened in the same frame
the phone is put away shows on the HUD; how long answers wrap in the list's entries. That
`BuildingRegistration.GetDisplayName`, `creationDay` and the daily statements read as the decompiled code says for
a real save. That a transaction with no address and no category shows in EconoView under its `_label`. The
rivals' names as the game gives them (`SpecialRival.rivalData.rivalName`). The app icon's glyph (`Icon-Building`).
Whether a business opened in a building rented before the promise is found (it is counted by its kind, so it
should be). Every figure's feel in play: the sums against what businesses really earn, how often an offer comes.

## Economy: running a business

The fifth part of the economy (`Scripts/Economy/Company`; options under "Real Life Overhaul: Running a business",
ids `economy_company`, `economy_company_level` and one switch for each part: `economy_company_staff`, `_cover`,
`_marketing`, `_tax`, `_hide`, `_licences`). It adds what running a business has and the game leaves out: staff who
know the going rate, insurance, campaigns that are measured, a quarterly return with an accountant and cash kept off
the books, licences that lapse, and rows in the Planner for all of it. It has its own director, its own line in the
save and its own app on the phone, "Company". It runs only while the credit side does, as the city's economy does.

```
Economy/Company/        the first eight are game-free and compiled by EconomyCheck
  CompanyCatalogue        every number, and the enums that are saved as numbers
  CompanyState            what is kept for a save: businesses, staff, claims, reports, tax
  StaffRules              the going rate, raises, a rival's offer, a strike, overtime, sick pay
  CoverRules              premiums, excess, claims, renewal
  CampaignRules           cost, effect, the report's arithmetic
  TaxRules                the quarter and its return, the accountant, cash off the books
  LicenceRules            which licences, terms, the free first term, renewal
  CompanyStore            the save's one line, "reallife:economy_company"
  CompanyGame             everything read from or done to the game, and CompanyBridges (what it asks of the mod)
  CompanyDirector         the timer's look, the hour, the day, the save, footfall, money, the Planner's rows
  CompanyDirector.Staff / .Cover / .Marketing / .Tax / .Licences    one part each: its day, its answers, its rows
  CompanyDirector.Tests   the test tools
  CompanyApp              the frame, the tabs, the Overview
  CompanyApp.Staff / .Cover / .Marketing / .Tax / .Licences         one tab's page each
  CompanyText             the words; CompanyGuide: the Guide page's figures, all from the catalogue
Wiring/CompanyLinks.cs  business pressure, fires, break-ins, the courts, the paper, the Planner
```

**Adding a part** is a rules file (game-free, with checks), a `CompanyDirector.X.cs` that fills in the partial
methods `XDay` and `XAlerts`, a `CompanyApp.X.cs` that fills in `WriteX`, a value of `CompanyPart`, a switch in the
options and the names in the catalogue. The core calls a part's day inside its own `try`: a fault there switches that
part off for the session with one line and the others go on.

### What the game already had, and what was therefore built

| Part | The game's own, left as it is | Built here |
| --- | --- | --- |
| Staff | A wage per employee from a formula by skill (`EmployeeHelper.CalculateHourlyWageForSkill`), satisfaction from job demands (`Entities.Employee.JobDemands`: a clean workplace, hours and days, health insurance, equipment), complaints, a bonus, training that raises the wage, sick days without pay (`EmployeeHelper.RunDaily` sets `isAbsent`), poaching of teams by its special rivals (`EmployeeInstance.PoachByRival`) | A going rate that moves, raises asked for, a rival's offer to one employee, a strike, overtime, sick pay for whoever the game has off sick. No sick list of the mod's own and no flu spell: the game already decides who is sick |
| Insurance | Health insurance for staff only | Fire, theft and liability policies for a business |
| Marketing | A daily marketing budget through an agency (`BuildingRegistration.marketingCampaigns`), which raises the promotion figure for as long as it is paid, with no end and no report | Campaigns with a price, an end and a report, on top of it |
| Tax | A yearly tax on the businesses' sales less deductible expenses, above a threshold, with a late fee and seizure (`TaxHelper`, `TaxCalculationHelper`); property tax | A quarterly return to file, an accountant, cash off the books. Deductible costs are marked the game's own way, so nothing is worked out twice |
| Licences | "Licensing fees" for films and plays (`LicensingFeesHelper`): a cost of what is on, not a permit | Four permits to trade |

### Every effect, and which of the three safe ways it uses

The rule is the city's economy's: no game method is patched, and no figure of the mod's is written into the game's
saved data. (a) is a mechanism the mod already has and restores, (b) a figure of the game's in memory that is not
saved, (c) a dated transaction of the mod's own. (b) is not used here at all.

| Effect | Way | How it is undone |
| --- | --- | --- |
| A campaign's, overtime's and a strike's customers | (a) `BusinessGame.Ambient`: `Wiring/CompanyLinks` answers with the city's economy's and the company's together, and business pressure's own writer puts the sum in the promotion figure inside its own bounds (25 fewer to 15 more) | The figure is put at the game's own before every save, when the feature or the part stops for any reason, and by the game itself every night. `CompanyDirector.FootfallOf` is a function of the files and the day, so it is right whoever asks |
| A strike's shut doors, a closure for a missing food or alcohol licence | (a) `BusinessDirector.CloseFor`, business pressure's own closure | Business pressure reopens it on its hour, and the player can at any time. Only while business pressure is on |
| An audit after two late returns, an inspection where a licence is missing | (a) `BusinessDirector.StartIssue`, by business pressure's own rules: it may say no | It is business pressure's own issue from then on |
| An employee who takes a rival's offer leaves | The game's own resignation, as business pressure calls it (`BusinessGame.Leave`) | Not undone: they have left |
| An agreed raise, sick pay, overtime, a strike's payment, premiums, a claim paid, a campaign's cost, a discount day's takings, the accountant's fee, a rebate, a penalty, back-tax and its fine, the tax kept back, a licence's fee and fine | (c) sixteen transaction keys, `reallife_transaction_co_...`, each with a `_label` | Money that has moved has moved |
| A court case for tax kept back | `Justice.Summon` through `Justice/TaxCases` | It is one of Justice's own cases |

**Wages are never written.** `EmployeeInstance.hourlyWage` is saved, so a raise agreed here is `StaffFile.Raise`,
hundredths an hour, kept by the game's own id for the employee (`EmployeeInstance.id`). Each night it is paid for the
hours the game counted that day (`workedHoursToday`, read on the hour event that opens the new day, before the game's
run clears it; a delivery driver's fixed day as the game pays it) as one transaction per business, "Wages, agreed
raise: {business}", taken whatever the cash is, as the game takes its own wages. An employee who is no longer at a
business of the player's is no longer read, and their file is dropped on the next day's pass with the raise in it.

**Sick pay follows the game's own absences.** `GameManager.NewDay` raises the day event before `EmployeeHelper.RunDaily`
picks the new day's absences, so on the day's pass `isAbsent` still says who was off on the day that has ended:
sick pay is paid for that day and dated on it. Nobody is made sick by the mod and `nextSickDay` is not written.

**Deductible costs** are marked with `TransactionInfo.SetTaxDeductibleName`, which is how the game's tax reads its
own. The name is the cost's label as words ("Business insurance"), the way the game itself passes a business's name
there, so the game's tax notice reads the same in a save the mod has left. The game keeps those sums in its own
list for its tax year; that list is the one thing of the game's saved data this feature adds to, through the game's
own call and by the game's own rule.

**Cash off the books** does not touch the game's count of sales. The tax the hidden share would bear at
`gameVariables.taxPercentage` is paid back each morning, and only while the last year's income passes the game's own
threshold (`TaxHelper.MeetsIncomeTaxThreshold`), since under it the game takes nothing.

### The numbers (`CompanyCatalogue`)

Strictness is Relaxed, Standard, Realistic, Hardcore; a figure with four values is in that order. Money is at a
price index of 100 where "at prices" is said, and follows `CityEconomy.PriceIndex`.

| What | Value |
| --- | --- |
| Going rate against the game's wage formula | boom +4%, slowdown -2%, recession -5%; 0.2% for each point of district income from 100; between 80% and 130%; then at today's prices (the index's whole move). What somebody is paid is counted the same way: the game's wage at today's prices, as the city's cost of living pays it (`StaffFile.Wage`), and the raise on top, so prices moving make nobody underpaid by themselves |
| Underpaid from, and for how long before asking | 15, 10, 8, 6% under; 10, 7, 6, 5 days |
| A request | 4 days to answer; "offer less" gives half; 14 days before the same employee asks again |
| Standing | starts 60, calm level 65; a day underpaid -1, a day paid fairly +1 up to calm; agree +10, less -8, refuse -18, an offer matched +5 |
| Sick pay | none, 50% or 100% of a day's pay; standing -3, 0, +2 a sick day; statutory is the default |
| A rival's offer | chance 0, 1.5, 2.5, 4% a day; to a skill of 50 or more, 8% or more under; the going rate and 5%; 3 days to answer, 2 days' notice; 21 days before the same employee, 7 before anybody |
| A strike | when 60% of two or more stand at 30 or lower; called a day ahead; 0, 2, 3, 4 days; 25% fewer customers, shut 8 hours from 9:00; 21 days' rest; the payment is 16 hours' pay each (+25 standing); a raise for all leaves nobody under 60; sat out, 40 |
| Overtime | offered on 30% of busy days (a campaign running or a boom); 2 days to answer; 3 days of 2 extra hours at 150%; 4% more customers; 10 days' rest; +2 standing |
| Insurers (three) | price 85, 100, 125% of list; excess 30, 15, 5% of the loss and 200, 100, 50 at least; pay 6, 4, 2 days after a claim; decline at 2, 3, 4 claims in a term |
| Cover (fire, theft, liability) | 12, 10, 8 a day and 0.4, 0.5, 0.3% of an average day's takings, at prices; counts a loss up to 20,000, 8,000, 5,000; district 0.2, -0.3, 0.2% a point of income, between 85 and 115% |
| Claims | 5 days to put in; each adds 25% to the next term; a term is a game year, 20 days at least; a policy lapses after 3 premiums missed running; 24 claims kept |
| Campaigns (flyers, radio, discount day, refit) | 150, 1,200, 0, 2,500 at prices; 3, 6, 1, 20 days; 5, 8, 12, 3% more customers; 12% at most |
| Campaign factors | local kinds: 0.5% for each point of district income on the business's side, between 80 and 120%; boom 110, slowdown 85, recession 70%; the same kind again loses 50% straight away, nothing after 10 days; a discount day gives away 15% of takings; 8 reports kept |
| The return | a quarter is three months of the city's (a twelfth of the year each, three days at least); 5 days to file; penalty 0.5% of the quarter's takings, 200 to 5,000, again after each 5 days, 3 at most; 2 late running bring an audit |
| Accountants (three) | 300, 700, 1,500 a month at prices; a rebate of 2, 4, 7% of the quarter's deductible costs; audit risk at 100, 80, 60%; the cheapest errs on 20% of returns (half a late penalty) and charges 50% more with cash off the books |
| Cash off the books | 0, 5, 10, 20, 30% of takings, 10% at most on Relaxed; risk +0.02 points a day for each per cent, -0.4 a day when clean, 25% a day at most; fine 25, 50, 75, 100% of the back-tax; court from 5,000, never on Relaxed |
| Licences (food hygiene, alcohol, late opening, signage) | 400, 900, 300, 150 at prices; 30, 30, 20, 40 days; reminder 3 days before; 5 days' grace for a new business; the fine is twice the fee; shut 6 hours for the first two; an inspection is asked for on 10% of days one is missing; late is open past 22:00 or before 5:00 |
| Kept | 64 businesses, 400 staff, 24 claims, 8 reports, 40 Planner rows; the game's statements are read 45 days back at most; a loss three days running is said |

Which business types sell food or alcohol, and which sell to the well-off or on price (for how well a district
suits a local campaign), are four short lists of the game's type names in `CompanyGame`. A business needs the late
opening permit by its own opening hours in the game (`BuildingRegistration.scheduleDays`).

### In the save

One line, `reallife:economy_company`: `v=1^h:...^t:...^s:...^e:...^c:...^r:...` (`CompanyStore`): the header's seven
numbers, tax's twenty, a record per business (its place, twenty numbers, three policies of seven and four licences of
three), a record per employee (the game's id, the place, ten numbers, a rival's name), the claims and the reports.
Parts are divided by `^` because a place, written as business pressure writes one, contains `~`. Every figure is a
whole number. A newer version adds fields at the end of a record or a part with a new letter; this reader takes the
fields it knows and skips the rest, puts a figure out of its bounds inside them and caps every list. A second line,
`reallife:economy_company_before_tests`, holds the first as it was before the first test tool changed it. Both are
registered in the feature list, so the self-check and "prepare this save for removing the mod" know them. What is
read from the game each day (a wage, a going rate, the hours, a business's name and books) is never saved.

### Public entries added to other features

Each is a new small file and one line that raises it; nothing else in those features was changed.

- `Events/FireLosses.cs`: `FireLosses.Billed(business, bill)`, raised in `FireDamage.Apply` after the repair bill.
- `Crime/BreakIns.cs`: `BreakIns.Happened(business, stock, repair)`, raised in `OffscreenCrimes.BreakIn`.
- `Business/BusinessEvents.cs`: `BusinessEvents.Closed(kind, place, outcome)`, raised where an issue closes
  (`BusinessIssues.Done`), and `BusinessEvents.Fined(kind, place, amount)`, raised where an inspection fines.
- `Justice/TaxCases.cs`: `TaxCases.File(amount)` and `TaxCases.Open(number)`, and one row at the end of
  `JusticeLaw.All`, the charge `tax_evasion` (a misdemeanour: fine 3,000, 12 hours of community service, 7 days of
  probation, 24 hours of custody, before the court's own bounds).
- `Wiring/Wiring.cs`: one line each in `Connect` and `Disconnect`. `Wiring/CompanyLinks` sets
  `BusinessGame.Ambient` to the city's economy's and the company's together and `News.Extra` to its own after the
  one that was there, and puts both back on disconnect.
- `Core/Settings.cs`, `Core/RealLifeOverhaulMod.cs`, `Meta/FeatureList.cs`: the options, the director and the
  feature, each directly after the city's economy's.
- `Economy/Finance`: nothing added. The Company app uses `FinanceTable`, `FinanceRow`, `FinanceCellView` and
  `FinanceText` as they are.

### Cost

Nothing runs per frame and nothing has an `Update`. Once a second (the credit side's timer): a few comparisons.

- **The day's pass**, once a game day, on the game's day event. The player's businesses are found by one walk over
  the save's buildings (as the city's economy does) and each one's week of opening hours; the game's statements are
  walked back eight days at most (45 as a bound); every employee is read once, with one comparison of addresses per
  business each. With 30 businesses and 300 staff that is some ten thousand comparisons and a few hundred small
  strings: about a millisecond. The rules are one pass over the staff and one over the businesses. The footfall
  figure is written only where the company adds something, and the day's customers are not drawn again where the
  city's economy has just done so. A campaign's report walks the statements once for each of its days, on the day
  it ends. With "Developer diagnostics" on the pass prints what it took: "Company check (day N): ... reading the game
  x ms, the rules y ms, footfall and rows z ms".
- **The hour**: one multiplication per business, and a write of the figure where the company adds something. On
  the hour that opens a day, one look at each employee who has an agreed raise.
- **The app** is built once per city from the game's MyEmployees (as Finance is) and written when it opens, when a
  tab or a row is picked and when something has changed. Opening it reads the staff once.
- **Cash off the books** asks the game once a day, only while something is hidden, whether its tax falls due
  (`TaxCalculationHelper.GetBusinessSales`, one walk over the game's statements).

### Diagnostics

With "Developer diagnostics" on, lines that begin "Company check (" say: each day's pass with its milliseconds; each
decision with its inputs ("staff, {name}", "staff, a rival's offer", "staff, a strike at", "cover, a loss at",
"cover, {business}", "marketing, the report for", "tax, the audit risk", "tax, quarter N closed", "licences,
{business}"); each transaction with how it was worked out ("transaction, {key}, {name}"); each footfall figure
applied and restored ("footfall, {business}"); "before a save"; "restored, {why}"; and "options". What the player
does in the app is written whether or not diagnostics are on, as lines that begin "Company: ".

### Test tools

Nine buttons under the options. Each puts one thing in place at the first business or employee it fits and opens
the app on it: a raise request, a poaching offer, a strike tomorrow, a fire claim on a test policy (no fire and no
bill), a campaign that ends at once with its report (flyers as if run over the last three days: nothing charged, no
customers added, the report from the game's real figures), a return due now, an audit of cash off the books (it puts
6,000 in the audit's way if nothing was kept back, and really charges what it finds and may really open a case), a
licence that lapses now, and "put the company back", which restores the state from before the first test. Money
that has moved since stays moved.

### Checks

`EconomyCheck` compiles eight more game-free files and runs 649 checks in all (`Checks5.cs` has the 225 new ones):
the going rate's bounds and direction over every index, state and district; raises, a rival's offer and a strike from
scripted histories at each strictness (200 days of underpaid staff refused every raise: no strike on Relaxed, one
within sixty days otherwise); overtime and sick pay; premiums, excess, claims, the cap and the insurer that will not
renew; every campaign's bounds, repeats, and the report's arithmetic against a scripted series; the return, its
penalties and the run of late ones; the accountants; the risk curve, back-tax, the fine and the court's threshold;
licence terms, the free first term, renewing by itself; the save's line across a version bump, with figures out of
bounds and names with dividers in them; and that the same seed gives the same history at every strictness.

### Not checked in the game (the game could not be run)

That the Company app's five tabs and its pages fit the white box as Finance's do (the longest page is a strike's or
an accountant's, eight or nine lines and one row of buttons). That the row of three buttons in a request for a raise
reads well. That the icon's glyph (`Icon-Shop`) is in memory on the phone. That `workedHoursToday` still holds the
ended day's hours on the hour event that opens the new day, as the decompiled `GameManager.NewDay` says. That the
game's tax notice shows the deductible labels as words. That business pressure's closure and the company's strike
read well together when both features are on, and what the player sees when a campaign's customers are cut by
business pressure's bounds in a boom. That a tax case opened by a summons is heard as Justice's other summonses are.
The milliseconds of the day's pass in a late save.

## Economy: the staff rota planner

A tab of the Company app, "Rota" (`Scripts/Economy/Company/Rota*.cs`, `CompanyDirector.Rota.cs`, `CompanyApp.Rota.cs`;
option "Staff rota planner", id `economy_rota`, under "Real Life Overhaul: Running a business", on by default). For
every place the player runs it shows the game's own customer forecast for each hour of the week, the opening hours
that pay for their staff, the people those hours take against the people there are, and it writes a week's rota when
the player presses a button. It keeps nothing in a save and has no Harmony patch.

```
Economy/Company/        the first six are game-free and compiled by EconomyCheck
  RotaModel               the planner's numbers (the game's limits among them) and its plain data: problem, plan, demand
  RotaSolver              the rota itself: limits and goals, the start, placing an hour, a demand met or not
  RotaSolver.Shifts       whole-week stations, shifts for those with a least number of hours or days, shifts by value
  RotaSolver.Improve      the improving pass under a fixed budget of moves, and the rating of a rota
  RotaAdvice              an hour's crew, the hours worth opening, what each hour at each station is worth, a week in
                          money, the people a week takes, a station that is missing
  RotaLedger              what a fill may touch in a week that is there, what putting the week back restores, the stamp
  RotaGame                the places, what a business was worked out from, names; everything from the game is here
  RotaGame.Read           one business into plain numbers: stations, skills, the week it keeps, customers, worth, fees
  RotaGame.Demands        the game's rota demands, each asked of the demand itself
  RotaGame.Write          the rota and hours written, the week as it was kept and put back, as RotaLedger decides
  CompanyDirector.Rota    the list of places, the jobs that work a business out a few steps a frame, the buttons, tests
  CompanyApp.Rota         the tab: the places, one business's week, one day's hours
  RotaGuide               the Guide page's figures, from RotaModel
```

### What the game already had, and what was therefore built

The game has a schedule screen and, with an HR manager at a headquarters, an auto fill (`ScheduleAutoFiller`, Google
OR-Tools on a worker thread) that puts the employees of a business on its stations for the hours it is open and keeps
their demands. It is left as it is. It does not say which hours are worth opening, how many customers will come, how
many people that takes or which stations are worth staffing. Those are what was built, with a rota of the mod's own
making (no OR-Tools: the mod references nothing new).

### How each kind of place is planned

| Place | Forecast | Hours | Rota |
| --- | --- | --- | --- |
| A shop of any type on a retail building, a gym, a hairdresser, a nightclub, a casino, a cinema, a theatre | the game's calculator for it (`CustomerEntriesCalculatorFactory`): `GetCustomersByHour` for each of 168 hours | advised | service stations by the customers each adds, support stations while open, cleaning by the dirt |
| An office that takes customers (a law firm, an agency) | the game's office calculator without its cap at the desks staffed now | advised | one customer a staffed desk an hour, as `OfficeBusinessSimulator` serves them |
| A factory, or any type the game sends no customers | none | the hours it keeps | every work station every open hour, cleaning by the stations at work |
| A headquarters | none | the game's office hours (its screen refuses to change them) | one person a desk for the whole week, the game's own rule there |
| A warehouse | none | none | none: the page says the game gives its drivers their vehicles |

A place with no assignable station, or with nobody assigned, says so in a sentence. A business the game's figures
cannot be read for is left out for the session with one line in Player.log, and its page says so.

### What is read, and why nothing of the mod's is counted twice

- **Customers**: `GetInitialCustomers` and `GetCustomersByHour`, which hold capacity, the promotion figure and the
  type's day and hour factors. The city's economy, a campaign, overtime and a strike of the mod's change customers by
  writing that promotion figure (`BusinessGame.Footfall`, through `Wiring/CompanyLinks`), so the game's figure already
  has them for today and they are not added again. A weekday on which a campaign, overtime, a strike or one of business
  pressure's issues will have ended or begun is asked of the game's calculator with the promotion figure the mod will
  have written by then. That figure comes from `BusinessCatalogue.Written`, the one function `BusinessGame.Footfall`
  writes with (the issue's change and the ambient one each inside their own bounds, the result a promotion of 0 to
  100), so a business already at 100 is forecast no gain from a campaign. The figure stands in the business's record
  only for the length of the asking (`RotaGame.ReadCustomers`); `CompanyDirector.RotaDays` works it out.
- **What a customer brings**: the business's `orderHistory` of the last 7 days (revenue, wholesale cost, customers);
  with fewer than 20 customers on it, the products on sale at their prices by the arithmetic of
  `CustomerEntriesCalculatorRetail`. The page says which.
- **Stations**: `BuildingRegistration.GetAssignableItems`; what one serves an hour by
  `ItemHelper.GetItemsSortedByCapacity` (the rule of `RetailBusinessSimulator.GetMaxCustomerCapacityThisHour`). A
  station that lacks one of its furniture requirements is passed over, as the game's simulation passes over it.
- **Skills**: one of the employee's skills among the item's `suitableSkills`; how well by skill and satisfaction.
- **Demands**: `HoursWorkingPerWeek` and `WorksOnItem` by their public members; `DaysWorkingPerWeek`, `FreeOnDays`,
  `HasNoShiftBetweenHours` and `NoSpecificShift` by asking the demand's own `Fulfilled` or `AcceptsWorkStation` about
  an employee who is nobody with a made-up week. No private member of the game is read.
- **Fees**: the cinema's and the theatre's licensing fee for a day it opens. **Cleaning**: the game's dirt figures.

### The rota (`RotaSolver`)

Never broken: a skill for the station, one place at a time, a shift of at most 12 hours, a day of at most 14, one
person for the week at a headquarters desk. Wanted, in this order: the most value covered, the most demands met
(weighed 2, 3 and 5 as `JobDemand.Impact`), the lowest wages, few and long shifts. Demands that set an upper end are
limits; a least number of hours, a number of days and a station asked for are goals. If hours stay uncovered, demands
that are not of high priority stop being limits (the game's own `skipOptional` step) and give way only where that
covers an hour; those of high priority never do. Two starts are made and the better kept; the improving pass counts
its moves against a fixed budget, so the same business always gets the same rota.

### What is written, and how it is undone

Only on the player's button, after the game's own confirm dialogue where a rota is written over or hours change:
`ScheduleDay.workShifts`, and with "Use these hours and fill the rota" `isOpen` and `openingHourSlots`. `RotaLedger`
decides, and is checked offline:

- **Left alone**: every shift of somebody the planner did not read (a delivery driver), and everybody who is "fixed":
  whoever holds a headquarters desk, and whoever has a shift at a station that was not read (one that lacks a
  furniture requirement). A fill only ever fills desks that are empty.
- **Taken out**: every other shift of the people it read. **Written**: the plan's shifts, none for a station that is
  not in the building at that moment, none for somebody not assigned there at that moment, none on a closed day.
- **Afterwards** what the game does after its own auto fill (`ScheduleAutoFiller.ApplyResult`,
  `ScheduleAutoFillerHelper.OnCompleted`) and when its schedule screen closes (`BizManSchedule.OnDisable`), with two
  things left out on purpose: the game's `UpdateHQPlans` is never called, and `UnAssignWork` with the "idle" task is
  taken only for somebody who had a shift and has none now and who holds no headquarters plan. The game has no call
  that gives a headhunter's, a logistics manager's or a purchasing agent's plan back, so the planner never takes one.
- **Put back**: the week kept (each day's switch, its opening hours, every shift) is restored where the person still
  works there and the station still stands; shifts of those left alone stay as they are today. As a fill changes no
  plan, that is everything a fill changed; the "idle" task of somebody who has a shift again is closed by the game.
- **Never from a stale reading**: what was worked out carries a stamp of everything it was worked out from (every
  assignable item by id and whether it lacks a requirement, prices, hours, shifts, everybody assigned with wage,
  satisfaction, skills and demands). The stamp is compared again in the frame that writes; a business that changed
  is read again, and after three such readings nothing is written and the player is told.
- **Never in part**: a business with more than 64 stations (one bit each in the solver) or 120 employees is refused
  with a sentence; nothing of it is read in part and nothing of its rota is touched. The game's building size data
  is asset data and is not in the decompiled code, so the largest business the game allows is not known here.
- **The late opening permit**: while licences are on and the business holds no permit, hours from 22:00 and before
  05:00 are barred from the advice, so "Use these hours" can never put it in breach. The page says what the permit
  would add (hours and money), and says so when the business opens late without one as it stands.

### The ladder of plans (`RotaLadder.cs`, `RotaLadder.Steps.cs`)

The Rota page of a business no longer shows two extremes ("with the staff", "with all it takes"). It shows a ladder,
every rung a set of opening hours with a rota, all valued the same way (`RotaLadder.Evaluate`):

| Rung | What it is |
| --- | --- |
| As it is | the hours and the rota the business has |
| Best with the people you have | the better of: the week as it is; the hours kept with a new rota; those hours less the ones nobody can keep the smallest crew for; the hours worth opening, cut to what the people can keep. Never worse than the week as it is, by construction |
| Hire 1, Hire 2, ... | one more person at the going wage (`StaffRules.Going` of the game's wage formula for the station's skill at a middling value), for the group of stations where that adds most (up to three groups are tried), counted for a full week of 40 hours and no more. Kept only if it adds at least 5 per cent of the week before and at least 50.00 a week. The ladder ends at the first hire that does not, and at four hires |

A week is: what the customers served leave (at the cleanliness they find, and for goods only while there are some:
see "Goods" below), less wages, licensing fees, the game's hourly theft, the risk of a break-in on a night shut, and
what unmet demands cost. So a week open round the clock is shown only if every step to it pays.

**Staff first or lean.** Staff first: every demand is a limit (the solver is given them all at the game's high
priority, which never gives way), and the least hours people ask for are wanted at stations that would otherwise
stand empty, as the game's own auto fill gives everybody their least hours. Lean: only hours that pay are wanted, the
solver lets demands below high priority give way to an hour, and what that costs is counted. Left alone the planner
makes the hours kept in both and takes whichever comes to more; the player's choice for a business is kept in the save
under `reallife:economy_rota` (`RotaStore`, version 1; a line of another version is not read and is left untouched
until a choice is made). For a small shop the default comes out as staff first: losing its only cashier costs more
than the hours asked for.

**Cleaning hours** are chosen one move at a time by what each adds to what the week's customers leave, less the
cleaner's wage: one hour more, or the same hour more on every day (a single hour changes nothing where the floor is
dirtier than one hour cleans). Staff first, the hours a cleaner asks for beyond those are added from closing time
backwards, spread evenly over the days. Where the mod's cleaning service cleans a business
(`CompanyBridges.Cleaned`, set in `Wiring/ServicesLinks.cs` from `CleaningService.Covers`) no cleaning hour is wanted
and the floor is taken to stay as it is.

**Goods** (`RotaStock.cs`, read by `RotaGame.Stock.cs`). In a shop of the game's retail simulators, every product
sold off a shelf (and, where the business type needs paper bags, the bags at the tills) is counted one by one: what
its shelves hold (`CargoInstance.GetMaxStockCapacity`), what is on site (the shelves, the stock containers and any
vehicle parked there, as `ReStockingHelper.CountAvailableStock` counts them), the box places of the business's
storage less those other goods take, what a customer takes of it and what a unit leaves (by the same source as what a
customer is worth: the books of the last days, else the game's own arithmetic for a customer's order), what the
delivery contracts that repeat bring on a Monday (`DeliveryContractItem.amount`, with the weekly limit
`Item.maxWholesaleOrderAmount`), and what the logistics managers' delivery plans top it up to each morning
(`ItemAmountTarget.targetAmount`, read as `LogisticsManagerPlan.GetPlannedDeliveries` would run it, without its side
effects). Each week of a plan is run hour by hour (`RotaStockRun`): the delivery plan at 2, the contract at 8 on a
Monday (onto the shelves, then into storage a box at a time; what finds no room goes back, as
`ItemHelper.DeliverCargoToBuilding` refunds it), then the hour's customers buy what is there. The shelves are filled
again from storage the moment they run empty (`ItemHelper.SubtractFromStock`, `RefillSingleItemWithLimit`), so what
sells out is everything on site. Without a bag nobody buys goods (`GetIdealPointOfSale`). The goods run two weeks, the
first from what is there now, and the second is counted, so a plan comes to what it comes to week after week with the
deliveries there are. An Urgent Order is a delivery once (the contract goes back to Mondays after it, and the game
lowers its amount to what is left of the week's limit), so it is not counted; neither is a contract that does not
repeat, a backorder or a shortage at the wholesaler (the page says those). A product nothing brings is not capped: it
is taken to be restocked by hand, and the page says how long what is there lasts. A shelf whose item takes nothing on
a sale (`producerSettings.ResourcesRequired` off) is no limit. With no product capped the week is valued exactly as
before. The page lists each product that runs out in the advised plan with the hour, what its customers would have
spent and what would lift it (`RotaLift`): room for the deliveries, a higher target for the delivery plan, a larger
contract or an Urgent Order, or past the weekly limit a second wholesaler or a delivery plan from a warehouse. Where
the next hire adds too little because the goods run out first, the ladder ends with that reason (`RotaEnd.Stock`).

**What is written.** "Fill the rota" writes the rota for the hours kept (the ladder's `Fill`). "Use this plan" is on
the page of the best week with the people when it differs from the week as it is; the confirm dialogue says the hours
open now and after and what the plan is worth. A rung that takes a hire is never written (`RotaLadder.Writable`,
checked again by the director before the job and before the write); its page shows a button that cannot be pressed,
who to hire, at what wage, and where. Every hour a plan opens or closes is listed with its reason and what it comes to
now and in the plan.

### Hours a week are compulsory, and shifts follow the customers (`RotaFit.cs`)

**The range.** The game's hours demand is `Entities.Employee.JobDemands.Requirements/HoursWorkingPerWeek.cs`: `MinHours`
(:20) and `MaxHours` (:22) are the two ends of a slider from 0 to 50 (:17) kept in each demand's own asset
(`jobdemand_parttime`, `jobdemand_fulltime`), and `Fulfilled` (:24) asks for the assigned hours to be inside both. The
figures themselves are in the game's data, not its code, so they could not be read offline; the planner reads them
from the game for every employee (`RotaGame.Demands.cs:89`) and uses whatever they are. The game's own auto fill
gives exactly the least (:98).

**The rule.** In every rota the planner makes (the one "Fill the rota" writes and every rung above the week as it
is), whoever asks for hours a week works no fewer than the least and no more than the most, or is given no hours at
all and is named with the reason. It holds in staff first and in lean, for every skill. How:
- the most: every hours demand is given to the solver at the game's high priority, which never gives way
  (`RotaLadder.Steps.cs`, `Build`);
- the least: the hours people ask for are wanted at their stations, the busiest hours first, also where the station
  would otherwise stand empty (`Lay`); cleaning hours are chosen by what they bring, lengthened to the cleaners'
  least and made one shift a day around the busy hours (`CleanOne`, `CleanBlocks`); after the solver, `RotaFit.Reach`
  gives whoever is still short an empty hour beside a shift they have (a wanted one first, then the busiest), or an
  hour a colleague with more than their own least can spare at an end of a shift;
- whoever still has some hours but fewer than their least is left out of that week, one person at a time, and the
  week is made again without them (`Fitted`); nobody is ever written short.
A rung says who it leaves without hours and why (`RotaLeft`): their own limits against the opening hours (with the
hours open to them), the others at the same stations (what they ask for between them against what the stations
hold), or the cleaning service. A rung may be used (`RotaRung.Usable`, `RotaLadder.Writable`) only if nobody in it
works outside their range.

**When the hours kept are too few.** The plan with the people then has one more candidate: the hours kept and more,
the best paying hour beside an open one added one at a time (never an hour barred for want of a permit) until the
opening hours hold the least hours of everybody who was left out (`Extend`). Its page lists those hours as "opened so
that ... gets the hours they ask for" with what they earn or cost. Candidates are ranked first by how many people keep
the rule, then by what the week comes to, so that plan may come to less than the week as it is: the page shows the
difference. "Fill the rota" on hours that cannot hold somebody says so in its dialogue with the figures and offers
that plan in its place.

**Shifts follow the customers.** What an hour at a till is worth is its customers, so the busiest hours are covered
first; hours given beyond need are valued by the customers of the hour. After the solver, `RotaFit` mends shifts of a
single hour (lengthened, or given up where the holder stays in range) and then moves shifts towards the customers
until no move is left: a shift an hour later or earlier, an end hour of a long shift to a busier hour beside another
shift of the same person, and a break that a 12 hour shift forces moved to the quietest hour. So within a day a
busier hour never has fewer people at the tills than a quieter one unless a rule forbids the move: skill, one place
at a time, the 12 hour shift, the 14 hour day, the person's own demands, or whole shifts (no further piece of work on
a day, none of a single hour).

### What the game does, as read from its code (decompiled `BigAmbitions`)

- **An unmanned till sells nothing (away).** `Buildings.Retail.Simulation/RetailBusinessSimulator.cs:106`
  (`LoadPointsOfSaleWithCustomerSatisfactionThisHour`) keeps only tills whose employee gives a customer satisfaction
  above nothing; `ProcessCustomer` (:229) with no till leaves every retail entry unpaid (`ProcessItemInOrder`, :296)
  and completes the order. `Helpers/BusinessHelper.cs:234` (`CreateDailyOrderHistory`) counts only orders with a paid
  entry as customers and revenue. **With the player inside** customers queue at an employee station
  (`CustomerJoinQueue.cs:38`) and complain where none is available; an order is only added to the day's books when it
  completes (`Customer.cs:383`). The planner therefore values an open hour without the smallest crew at no sales.
  Not verified in the running game.
- **A till's customers an hour are a hard limit, and skill does not change it.** `RetailBusinessSimulator.cs:214`
  (`GetMaxCustomerCapacityThisHour`) sums `Item.addedCustomersPerHour` of the manned stations of each group and takes
  the smallest group; customers beyond it are marked completed and lost (:176). Skill enters only as the order's
  service quality: `Entities/EmployeeInstance.cs:1037` (skill x satisfaction x 0.01). The forecast itself stops at what
  all stations serve (`registration.customerCapacity`), so the page shows the uncapped figure beside it ("36 come, the
  stations serve 20") and what one more station would serve.
- **Satisfaction and sales.** `Helpers/BusinessHelper.cs:822` (`UpdateSatisfaction`): overall = (service + pricing +
  cleanliness + facility) / 4, the last three over orders with a paid entry.
  `AI.Customers.CustomerEntries/CustomerEntriesCalculator.cs:43`: what customers buy goes with (overall - 50) / 100 + 1.
  One point of cleanliness is therefore a quarter of a point of satisfaction, 1 / (4 x (50 + satisfaction)) of sales.
- **Nights.** The game's forecast gives every hour its own figure; the self-check line prints the week's customers by
  the hour of the day. Nothing in `BusinessHelper.RunHourly` (:454) charges an open hour beyond wages and, for a cinema
  or theatre, the day's licensing fee. Theft (`Helpers/BusinessSecurityHelper.cs:133`, `SimulateTheft`) follows the
  customers of the last hour, not the time of day: thieves a shelf = customers x 0.04 x (1 - security); under one it is
  the chance that one unit goes from a stocked shelf, and from one on the code takes nothing (`num2` is only set in
  the branch under one). The security level counts guards only while they are on a shift (:82, `IsWorking`). The mod's
  break-ins (`Scripts/Crime/OffscreenCrimes.cs:77`, `BreakIn` :120) are rolled once a night in the small hours and only
  hit a shop that is shut then, so an hour open at 01:00 removes that risk; the planner counts the expected loss of each
  night shut (chance 4 per cent x the frequency option x exposure, shared among the player's shops, times a quarter of
  the shelves up to 2,500 and the repair). The late opening permit bars the hours from 22:00 and before 05:00 as
  before. `HasNoShiftBetweenHours` demands are read as limits as before.
- **Cleaning.** `Buildings.BuildingTypes.Shared.Dirtiness/BuildingCleanliness.cs:35` (`ApplyDirt`): each dirtying adds
  1.2 to two thirds of the spots an item touches; :112 (`SimulateCleaning`, hourly from `BuildingCleanlinessHelper.cs:22`
  on a day the business opens): every spot loses the cleaners' skill x satisfaction / 100, divided by the cleaners the
  floor needs; :64 (`GetCleanliness`) = 100 - mean dirt - mean visible dirt (five points and more), which an order
  carries into the satisfaction above. `CleaningStationController.cs` only places the cleaner in the room. The planner
  runs the week hour by hour with the mean dirt (twice, so that it starts as dirty as it ends).
- **An unmet demand.** `Entities/EmployeeInstance.cs:744` (`UpdateSatisfaction`, hourly): plus the weight of each demand
  met and minus that of each unmet (`JobDemand.Impact`, `Entities.Employee.JobDemands/JobDemand.cs:43`: 0.2, 0.3, 0.5),
  no more than 0.6 an hour; with a third or fewer met it cannot rise above 60, with two thirds or fewer above 80.
  `AI.Employees/LowSatisfactionComplaint.cs:13` and `EmployeeInstance.cs:1305`: at nothing the employee resigns at once.
  `AI.Employees/UnfulfilledDemandsComplaint.cs:19`: a complaint about an unmet demand gives 72 hours to meet it or pay
  a bonus (not counted: it comes at a random time). `Helpers/EmployeeHelper.cs:154`: more than 14 hours worked in a day
  gives a chance of a sick day (the solver never rosters more than 14). The planner counts: hours to the resignation =
  satisfaction x 10 / (weights unmet - weights met); the sales the person's customers leave less their wages (or their
  wages, if more) for the 7 days the post is taken to stand empty, spread over the weeks to the resignation; and the
  worse work on the way (service is skill x satisfaction). **The 7 days are the planner's own figure**, not the game's.
- **Hiring.** Candidates come from the MyEmployees app and job boards
  (`UI.Smartphone.Apps.MyEmployees/JobBoardCandidateGenerator.cs:22`); a signing bonus is optional
  (`AI.Employees.SalaryNegotiation/CandidateSalaryNegotiation.cs:70` starts it at nothing). The planner counts no
  one-off cost for a hire.
- **Stock.** `RetailBusinessSimulator.cs:296`: an entry is sold only from a shelf that holds it, and shelves are
  refilled from storage at the hour the last work shift of the day ends (:424). **Not counted**: the plans take the
  shelves to be full. A plan that sells more needs deliveries to match; the Guide says so.

### A worked example (printed by `EconomyCheck --rota-example`)

A gift shop: one till (20 customers an hour), one cleaning station; 786 customers a week (112 a day, 12 in the busiest
hour, a few through the night); a customer leaves 20.86 after the goods; a cashier at 12.00 who asks for 20 to 30
hours, a cleaner at 9.00 who asks for 35 to 45; open 68 hours with the cashier rostered for 30 and the cleaner for 4.
Going wages 12.50 and 9.50; satisfaction 70, cleanliness 80; theft 0.23 a customer; a night shut 3.24.

| Rung | Open | With the cashier | Staff hours | Served of those who come | Sales | Wages | Theft | Night | Demands | Week | Gain |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| As it is | 68 | 30 | 34 | 307 of 572 | 6,209.79 | 396.00 | 131.56 | 22.68 | 36.16 | 5,623.39 | |
| Best with the people | 68 | 30 | 65 | 314 of 572 | 6,818.42 | 675.00 | 131.56 | 22.68 | 0 | 5,989.18 | 365.79 |
| Hire 1 cashier | 70 | 69 | 104 | 587 of 592 | 12,598.60 | 1,162.50 | 136.16 | 22.68 | 0 | 11,277.26 | 5,288.08 |
| Hire 2 cashiers | 109 | 108 | 143 | 725 of 726 | 15,380.07 | 1,650.00 | 166.98 | 16.20 | 0 | 13,546.89 | 2,269.63 |
| Hire 3 cashiers | 145 | 143 | 178 | 782 of 784 | 16,512.57 | 2,088.00 | 180.32 | 3.24 | 0 | 14,241.01 | 694.12 |

The best week with the people, as written: the cashier (asks 20 to 30, gets 30) stands at the till 11:00 to 14:00
on Monday and Tuesday, 10:00 to 14:00 on Wednesday and 10:00 to 15:00 from Thursday to Sunday, the 30 busiest hours
of the 68; the cleaner (asks 35 to 45, gets 35) works 10:00 to 15:00 every day, one shift around the busy hours. In
every plan everybody is inside their range. As it is, 265 customers a week find nobody at the till in the 38 open
hours without the cashier; nothing more can be sold without somebody at the till, so each further rung is a cashier
of 40 hours. The third still adds 694.12, over the 5 per cent (677.34) a rung must add, because in this example a
customer or two an hour through the night leave more than a cashier's wage. With no customers at night no plan opens
those hours (checked).

**Where this contradicts the earlier figures.** The earlier page said for this shop: now 6,008.02; "with the staff"
13,021.36 at 90 hours; "with all it takes" 14,631.96 at 147 hours. The first left out theft, the night risk, the dirty
floor and the cleaner's unmet demand. The second had the one cashier work 90 hours a week against a demand for at most
30, and counted nothing for it; the game would take 0.3 of satisfaction an hour and the cashier would resign within
two weeks. The earlier advice also gave the cleaner the fewest hours that kept up with the dirt at closing time (four
to six a week), which the hour by hour run shows to be the wrong place and too few. The third assumed four people
appeared at once; the ladder shows what each one adds.

### Cost

Nothing runs per frame, per second or per hour while nothing is asked. The list of places is read on the day event,
when the app opens and after a rota is written: one walk over the save's buildings and one over its employees. A
business is worked out when it is picked on the tab or a button is pressed, as a job: the game is read in one frame,
then the arithmetic and the rota are made a few steps a frame, for 2 ms of each frame at most, by a coroutine that
exists only while a job runs. `RotaSolver.Start`/`Step` and `RotaAdvisor.Begin`/`Step` do the same steps in the same
order as their one-go forms, so the result is the same (checked on 100 generated businesses). Measured by
`EconomyCheck` on the largest business it is asked to bear (40 stations, 60 employees, 7 days, 4,480 wanted hours): a
rota is about 1,800 steps and 1.5 to 2 ms in all, its longest single step under 0.1 ms, the longest step of the
advice under 0.1 ms; nothing is allocated once the arrays have grown. Those are figures of the .NET runtime the
checker runs on. On a runtime eight times slower that is some seven frames of 2 ms for a rota, where one go would
have held a frame for 15 ms or more. With "Developer diagnostics" on, "Company check (rota, ...)" and "Company: rota
written for ..." say the real milliseconds, the frames and the longest share of a frame.

The ladder of plans is made by the same job, after the advice: `RotaLadder.Begin`/`Step`, the same steps as its
one-go `Make` (checked on the example and on the largest business). A step is one piece of a candidate week (the
worth of its hours, a day's worth of cleaning moves, one step of its rota, its valuation). For the largest business
(40 stations, 60 employees) that is about 16,000 steps and 13 ms in all on the checker's runtime, the longest step
about 0.2 to 0.5 ms (the check takes the best of five runs, since one run alone can be held up by the machine); for
the example shop about 2,300 steps. A small shop's ladder makes up to some fifteen candidate rotas,
so on a runtime eight times slower its page may take a second or so of frames at 2 ms each to appear; the page says
the week is being worked out meanwhile. With "Developer diagnostics" on, "Company check (rota plans, ...)" gives the
ladder in short form, the customers by the hour of the day and every rate it was valued with.

### Test tools

Four buttons: open the rota of the busiest business, show that business's plans in the log (the ladder in one line,
then every rung with what its week is made of), fill every business's rota (without the dialogue; the weeks as they
were are kept), and put every rota back.

### Checks

`EconomyCheck` compiles the ten game-free files (and `Business/BusinessCatalogue.cs`, for the promotion figure) and
runs 434 checks of them (`RotaChecks.cs`, `RotaLedgerChecks.cs`, 91 in `RotaPlanChecks.cs` for the ladder and 90 in
`RotaRangeChecks.cs` for the hours rule: over 330 generated shops of one to four stations with part-time, full-time
and no demand, short and long hours, everybody in every rota made is inside their range or without hours; no rota
breaks a limit to get there; where the open hours hold every cashier's least hours none is left out (existence by
counting, exact for one till and days of at most twelve open hours); whoever is left out has the true reason and no
hours; no busier hour of a day has fewer people at the tills than a quieter one where a legal move could change it;
no shift of a single hour; lengthened hours are the best paying ones; stepped is one-shot. The ladder's checks: an
unmanned hour sells nothing and still costs its theft; a till's limit and a second till; that skill changes no
count; theft, night risk, fees; cleanliness and the hour cleaned; the cleaning service's rule; what an unmet demand
costs, figure by figure, and against the wages that meet it in both modes; the best week never worse than the week
as it is; each gain as shown and each hire rung paying; a hire rung never writable; every changed hour with its
reason; no hour opened that nobody comes in; barred hours in no plan; the same ladder every time and a step at a
time; the choices' line in the save; the time of the longest step): every kind of demand; the limits on
a shift and a day as behaviour at each limit; one place at a time; skills; the headquarters rule; what gives way and
what never does, with the reason told; no employees, no stations, closed all week, no customers; an hour's crew; the
hours advised (never an hour that loses money, at most two blocks of two hours or more, a day's fee, no late block
without the permit); cleaning; the people a week takes; a missing station; a week in money; who is left alone and
what a fill takes out and writes (never a station or a person that is not there); that a week filled and put back
is the week before, also when filled twice, and what is not restored when somebody left or a station was sold; that
a headquarters desk holder whom no rota would give a desk keeps theirs; the stamp (a station swapped for another
changes it); the promotion figure of a day ahead against the function that writes it, with its bounds and its stop
at 100; that a rota and an advice made in steps are the ones made in one go; the time of the largest business and of
its longest step; and 240 generated businesses against a plain rota filled hour by hour in the order of the week.
Against that plain rota it never covers less value. With every demand of high priority it never leaves more demands
unmet, and covers at least as many hours in 116 of 120 businesses (in the other four it covers fewer hours that are
worth more). With demands of every priority it covers more hours in 51 of 120 and leaves more demands unmet in 13 of
them, each time for more value covered: optional demands give way to cover an hour, as they are meant to.

### Not checked in the game (the game could not be run)

That six tabs fit the Company app's bar. That the week's box, with its lines and up to three rows of buttons, fits
the white box, and that "Use these hours and fill the rota" fits a button. That the game's confirm dialogue shows over
the phone. That each of the game's demands answers the made-up week as its decompiled code says. That the game's
schedule screen shows a written rota as it shows its own. What the forecast is against a day really played. The
milliseconds on the game's Mono, and above all those of the one frame that reads a large business from the game (its
stations' capacities, its staff's demands and 168 hours of the calculator), which is not cut into steps. That the
coroutine runs while the phone is open (it waits a frame, not game time).

Of the ladder of plans: that six columns and the people's rows read well in the plans table, that the titles of
columns that do not sort are readable (the kit shows them as the game shows a button that cannot be pressed) and that
a page opens at its first row (a different table is shown from the top: `DataTable.Show`). That the game's selector
row shows the three choices in the white box, and that a button set not to be pressed looks so. That an unmanned
till sells nothing with the player inside as it does away. That the dirt and cleaning rates read from a real
building give a believable week (the self-check line prints them). That theft in a played week is near the figure
counted. The seven days a post is taken to stand empty are the planner's own figure.

Of the goods: that what a real shop's shelves, storage, contracts and delivery plans are read as matches what the
game shows in BizMan; that a product is seen to run out when the forecast says (the books undercount what a customer
would take of a product that ran out in the days they cover, so the cap may come late rather than early); that the
box places counted for storage match what a delivery really fits; and how the goods rows read in the plans table.
