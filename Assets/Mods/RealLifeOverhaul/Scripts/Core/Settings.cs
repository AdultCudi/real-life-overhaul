using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Live option values. The game only fires option callbacks once the options
    /// screen has been opened, so saved values are read from PlayerPrefs at load.
    /// </summary>
    public static class Settings
    {
        public const string CameraEnabledId = "camera_enabled";
        public const string CameraIndoorsId = "camera_indoors";
        public const string CameraPitchId = "camera_pitch";
        public const string CameraDistanceId = "camera_distance";
        public const string CameraFollowId = "camera_follow";
        public const string MapPlacesShownId = "map_places_shown";
        public const string MouseLookId = "camera_mouselook";
        public const string MouseSensitivityId = "camera_sensitivity";
        public const string EnterWithKeyId = "enter_with_key";
        public const string CameraFovId = "camera_fov";
        public const string CameraHeightId = "camera_height";
        public const string CarCameraId = "car_camera";
        public const string CarCameraDistanceId = "car_camera_distance";
        public const string LifeEventsId = "life_events";
        public const string EventFrequencyId = "life_frequency";
        public const string CrashConsequencesId = "crash_consequences";
        public const string TrafficAccidentsId = "traffic_accidents";
        public const string AccidentFrequencyId = "accident_frequency";
        public const string SirenId = "siren";
        public const string PoliceId = "police";
        public const string PolicePursuitId = "police_pursuit";
        public const string PeopleId = "people";
        public const string OfficersOnPatrolId = "patrol_officers";
        public const string PatrolFrequencyId = "patrol_frequency";
        public const string StreetCrimeId = "street_crime";
        public const string CrimeFrequencyId = "crime_frequency";
        public const string RagdollsId = "ragdolls";
        public const string PlayerKnockdownId = "player_knockdown";
        public const string PedestrianIncidentsId = "ped_incidents";
        public const string HornReactionsId = "horn_reactions";
        public const string NaturalAccidentsId = "natural_accidents";
        public const string SolidStreetsId = "solid_streets";
        public const string PhoneContactsId = "phone_contacts";
        public const string PhoneSkinId = "phone_skin";
        public const string CarSpeedId = "car_speed";           // the old single "car speed %" slider: dropped at load
        public const string CarTuningId = "car_tuning";
        public const string CarTopSpeedId = "car_top_speed";
        public const string CarAccelerationId = "car_acceleration";
        public const string SmoothThrottleId = "smooth_throttle";
        public const string SpeedometerId = "speedometer";
        public const string VisualsEnabledId = "visuals_enabled";
        public const string SaturationId = "visuals_saturation";
        public const string ContrastId = "visuals_contrast";
        public const string BloomId = "visuals_bloom";
        public const string DetailId = "visuals_detail";
        public const string CharacterHighDetailId = "character_high_detail";
        public const string CharacterHairId = "character_hair";
        public const string CharacterLightingId = "character_lighting";
        public const string CharacterNewLooksId = "character_new_looks";
        public const string CharacterSkinId = "character_skin";
        public const string CrimeAgainstPlayerId = "crime_against_player";
        public const string PlayerOffencesId = "player_offences";
        public const string PolicePresenceId = "police_presence";
        public const string CityLifeId = "city_life";
        public const string CityDensityId = "city_density";
        public const string CityWorkersId = "city_workers";
        public const string CityEverydayId = "city_everyday";
        public const string CityInteractionsId = "city_interactions";
        public const string JobsId = "jobs";
        public const string JobPayId = "job_pay";
        public const string FamilyId = "family";
        public const string UtilitiesId = "utilities";
        public const string FamilyAtHomeId = "family_at_home";
        public const string FamilyWeddingId = "family_wedding";
        public const string FamilyChildrenId = "family_children";
        public const string FamilyRelativesId = "family_relatives";
        public const string FamilyFrictionId = "family_friction";
        public const string FamilyInterestId = "family_interest";
        public const string FamilyYearWeeksId = "family_year_weeks";
        public const string CivicId = "civic";
        public const string CivicElectionsId = "civic_elections";
        public const string CivicConstructionId = "civic_construction";
        public const string DrivingLicenceId = "driving_licence";
        public const string StartWithLicenceId = "licence_start_with";
        public const string DrivingSchoolId = "driving_school";
        public const string LicencePointsId = "licence_points";
        public const string PlannerId = "planner";
        public const string PlannerRemindersId = "planner_reminders";
        public const string EconomyId = "economy";
        public const string EconomyProductsId = "economy_products";
        public const string EconomySharkId = "economy_shark";
        public const string EconomyConsequencesId = "economy_consequences";
        public const string EconomyLevelId = "economy_level";
        public const string EconomyCityId = "economy_city";
        public const string EconomyCityLevelId = "economy_city_level";
        public const string VenturesInvestorsId = "ventures_investors";
        public const string MarketsId = "ventures_market";        // (the old market's option: kept, so that a player's choice carries over to the city's stock market)
        public const string MarketsMarginId = "markets_margin";
        public const string VenturesLevelId = "ventures_level";
        public const string EconomyCompanyId = "economy_company";
        public const string EconomyCompanyLevelId = "economy_company_level";
        public const string EconomyCompanyStaffId = "economy_company_staff";
        public const string EconomyCompanyCoverId = "economy_company_cover";
        public const string EconomyCompanyMarketingId = "economy_company_marketing";
        public const string EconomyCompanyTaxId = "economy_company_tax";
        public const string EconomyCompanyHideId = "economy_company_hide";
        public const string EconomyCompanyLicencesId = "economy_company_licences";
        public const string EconomyRotaId = "economy_rota";
        public const string EstateId = "economy_estate";
        public const string EstateHotelId = "economy_estate_hotel";
        public const string EstateHireId = "economy_estate_hire";
        public const string EconomyOnlineId = "economy_online";
        public const string EconomyCleaningId = "economy_cleaning";
        public const string EconomySaleId = "economy_sale";
        public const string EconomyBrandsId = "economy_brands";
        public const string EconomyBanksId = "economy_banks";
        public const string MinimapId = "nav_minimap";
        public const string GpsId = "nav_gps";
        public const string MinimapRotateId = "nav_rotate";
        public const string NavOtherRouteId = "nav_other_route";
        public const string MinimapSizeId = "nav_size";
        public const string MinimapZoomId = "nav_zoom";
        public const string MinimapBaseId = "nav_base";
        public const string RoadsideChecksId = "roadside_checks";
        public const string ShowTestToolsId = "show_test_tools";       // Meta: the "Test tools" section of the options page
        public const string DevDiagnosticsId = "dev_diagnostics";      // Meta: the diagnostic keys (F7, Shift+F7, F8, F9) and the Info lines in the log
        public const string GarageId = "garage";
        public const string GarageCosmeticsId = "garage_cosmetics";
        public const string GarageTuningId = "garage_tuning";
        public const string GarageUnderglowId = "garage_underglow";
        public const string GaragePoliceId = "garage_police";
        public const string TwoWheelersId = "two_wheelers";
        public const string HomeLifeId = "home_life";
        public const string HomeNeedsId = "home_needs";
        public const string HomeRoutinesId = "home_routines";
        public const string HomeSleepId = "home_sleep";
        public const string HomeIllnessId = "home_illness";
        public const string HomeFitnessId = "home_fitness";
        public const string HomeBodyId = "home_body";
        public const string HomeJogId = "home_jog";
        public const string HomeMorningId = "home_morning";
        public const string HomeStripId = "home_strip";
        public const string HomeEasyId = "home_easy";
        public const string LawPrecinctsId = "law_precincts";
        public const string LawCustodyId = "law_custody";
        public const string LawArrestsId = "law_arrests";
        public const string LawReputationId = "law_reputation";
        public const string LawStationId = "law_station";
        public const string LawStationFurnitureId = "law_station_furniture";
        public const string LawMapMarkersId = "law_map_markers";
        public const string BusesId = "transit_buses";
        public const string TransitPeopleId = "transit_people";
        public const string SubwayJourneyId = "transit_subway";
        public const string TaxiCallId = "transit_taxi_call";
        public const string CarOwnershipId = "car_ownership";
        public const string CarEasyId = "car_easy";
        public const string LeisureId = "leisure";
        public const string LeisureChessId = "leisure_chess";
        public const string LeisureFishingId = "leisure_fishing";
        public const string LeisureNightOutId = "leisure_night_out";
        public const string LeisureSightsId = "leisure_sights";
        public const string LeisureCinemaId = "leisure_cinema";
        public const string ClothesShopsId = "clothes_shops";
        public const string ClothesMustBeBoughtId = "clothes_must_be_bought";
        public const string NightLifeId = "night_life";
        public const string AmbientSoundId = "ambient_sound";
        public const string AmbientBirdsId = "ambient_birds";
        public const string AmbientNightId = "ambient_night";
        public const string AmbientCityId = "ambient_city";
        public const string AmbientCrowdId = "ambient_crowd";
        public const string RadioHudId = "radio_hud";
        public const string PhotoModeId = "photo_mode";
        public const string CityFiresId = "city_fires";
        public const string FireFrequencyId = "fire_frequency";
        public const string WeatherMattersId = "weather_matters";
        public const string WeatherFrequencyId = "weather_frequency";
        public const string StreetEventsId = "street_events";
        public const string StreetEventFrequencyId = "street_event_frequency";
        public const string SocialId = "social";
        public const string SocialMeetingsId = "social_meetings";
        public const string SocialRelationshipsId = "social_relationships";
        public const string SocialOutingsId = "social_outings";
        public const string SocialContactId = "social_contact";
        public const string SocialTextsId = "social_texts";
        public const string SocialTextsMaxId = "social_texts_max";
        public const string SocialNewspaperId = "social_newspaper";
        public const string LedgerKiosksId = "ledger_kiosks";
        public const string HotelsId = "travel_hotels";
        public const string HolidaysId = "travel_holidays";
        public const string TravelExtrasId = "travel_extras";
        public const string CareersId = "careers";
        public const string BusinessId = "business_pressure";
        public const string BusinessFrequencyId = "business_frequency";
        public const string BusinessEasyId = "business_easy";
        public const string BusinessRivalsId = "business_rivals";
        public const string BusinessRacketId = "business_racket";
        public const string BusinessInspectionsId = "business_inspections";
        public const string BusinessStaffId = "business_staff";
        public const string BusinessSuppliersId = "business_suppliers";
        public const string BusinessCustomersId = "business_customers";
        public const string BusinessAuditId = "business_audit";
        public const string BusinessOpportunitiesId = "business_opportunities";

        public static bool CameraEnabled = true;
        public static bool CameraIndoors = false;
        public static int CameraPitch = 18;      // degrees above the horizon
        public static int CameraDistance = 6;    // metres behind the player
        public static int CameraFollow = 5;      // 0 = never swing behind the player (only without mouse look)
        public static bool MapPlacesShown = true;   // the mod's places on the city map: their markers and their rows in the map's filter list (Core/MapPlaces; off hides all of them)
        public static bool MouseLook = true;     // GTA-style: mouse steers the camera, cursor locked
        public static int MouseSensitivity = 6;
        public static bool EnterWithKey = true;  // E/F near a door enters the building
        public static int CameraFov = 55;        // degrees, vertical
        public static bool LifeEvents = true;
        public static int EventFrequency = 5;        // about this many random events per game week
        public static bool CrashConsequences = true;
        public static bool TrafficAccidents = true;
        public static int AccidentFrequency = 2;     // 1 = rare, 10 = constant
        public static bool Siren = true;
        public static bool Police = true;            // patrol cars attend accidents and chase hit-and-run drivers
        public static bool PolicePursuit = true;     // the player is chased after hitting a car and driving off
        public static bool People = true;            // drivers, paramedics and officers on foot at accidents and traffic stops
        public static bool OfficersOnPatrol = true;  // one or two officers walking a beat near the player now and then
        public static int PatrolFrequency = 3;       // 1 = rare, 10 = nearly always one about
        public static bool StreetCrime = true;       // foot chases, stops and disturbances on the pavement
        public static int CrimeFrequency = 2;        // 1 = rare, 10 = constant
        public static bool Ragdolls = true;          // people hit by a car fall as ragdolls and can be hurt or killed
        public static bool PlayerKnockdown = true;   // traffic can knock the player down when on foot
        public static bool PedestrianIncidents = true;   // now and then a traffic car hits someone near the player
        public static bool HornReactions = true;         // people hurry, glare or look round when the player sounds the horn (People/HornReactions)
        public static bool NaturalAccidents = true;      // accidents come from reckless drivers on the road, in view (Traffic/Reckless); off: the old roll that stages them out of view
        public static bool SolidStreets = true;          // traffic brakes for people in the road, sounds its horn and waits (Traffic/Solid); off: the game's cars drive through people
        public static bool PhoneContacts = true;
        public static bool CarTuning = true;         // each car gets the acceleration and top speed of its kind (Driving/CarCatalogue)
        public static int CarTopSpeed = 100;         // percent of the car's own target top speed
        public static int CarAcceleration = 100;     // percent: at 150 a car reaches 100 km/h in two thirds of its own target time
        public static bool SmoothThrottle = true;    // the throttle key eases the pedal down instead of flooring it
        public static bool Speedometer = true;
        public static bool PhoneSkin = true;         // modern phone body, wallpaper and status bar
        public static bool CarCamera = true;     // chase camera while driving
        public static int CarCameraDistance = 8; // metres behind the car
        public static int CameraHeight = 15;     // look-at height above the player's feet, in tenths of a metre
        public static bool VisualsEnabled = true;
        public static int Saturation = 12;
        public static int Contrast = 10;
        public static int Bloom = 0;             // percent; 0 leaves the game's bloom alone (bloom costs performance)
        public static int Detail = 100;          // LOD distance, percent of vanilla (above 100 costs performance)
        public static bool CharacterHighDetail = true;   // the player is drawn with the game's detailed character set in the city
        public static bool CharacterHair = true;         // solid base layer and calmer highlights for detailed hair and beards
        public static bool CharacterLighting = true;     // fill and rim light in character creation and the shop previews
        public static bool CharacterNewLooks = true;     // the mod's own hair, beards and clothes on the player (picked on the options page)
        public static bool CharacterSkin = false;        // skin finish on the player: fine relief, calmer gloss on dark tones (off: a small change, close up only)
        public static bool CrimeAgainstPlayer = true;    // pickpockets, muggers, break-ins, scam calls: all capped, all with days of rest between
        public static bool PlayerOffences = true;        // red lights, speeding and reckless driving can get the player pulled over
        public static int PolicePresence = 5;            // 0 = no police standing or walking about; 10 = somebody nearly always in sight
        public static bool CityLife = true;              // people at work and going about their day on the pavements near the player
        public static int CityDensity = 5;               // 1 = one site now and then, 10 = three sites nearly always (never more than eight people)
        public static bool CityWorkers = true;           // sweepers, road crews, window cleaners, leaflets, the coffee cart
        public static bool CityEveryday = true;          // friends talking, smokers, phone calls, tourists, joggers, a busker's audience
        public static bool CityInteractions = true;      // the player can buy a coffee at the cart
        public static bool Jobs = true;                  // jobs the player can take themselves, from the Gig Line contact on the phone
        public static int JobPay = 100;                  // percent of what the job catalogue pays
        public static bool Family = true;                // a partner who moves in, engagement and wedding, children, relatives (Scripts/Family)
        public static bool Utilities = true;             // power and water bills, the utilities office and the Utilities app (Scripts/Utilities)
        public static bool FamilyAtHome = true;          // the partner is shown as a character in the home (off: they still live there, as notices and conversations)
        public static bool FamilyWedding = true;         // rings, the proposal, planning and holding a wedding
        public static bool FamilyChildren = true;        // deciding to have children, pregnancy and adoption, growing up
        public static bool FamilyRelatives = true;       // Mom and the in-laws react, visit, invite to holiday dinners
        public static bool FamilyFriction = true;        // chores, arguments, separation and divorce (off: the home only ever gets warmer)
        public static int FamilyInterest = 0;            // who the default "Someone special" introduces: 0 anyone, 1 women, 2 men
        public static int FamilyYearWeeks = 4;           // in-game weeks per year of a child's age
        public static bool Civic = true;                 // civic life: a city government and a construction programme (Scripts/Civic)
        public static bool CivicElections = true;        // elections, the mayor's policies and their effects, the campaign in the street
        public static bool CivicConstruction = true;     // construction sites that progress over days
        public static bool DrivingLicence = true;        // driving needs a licence for the class of vehicle; unlicensed driving is punished when police stop the player
        public static bool StartWithLicence = false;     // a save that owns no vehicle the first time the rule runs is given a car licence anyway
        public static bool DrivingSchool = true;         // the desks at the school buildings, the written test, lessons and the driven test
        public static bool LicencePoints = true;         // booked offences put penalty points on the licence; twelve suspend it
        public static bool Planner = true;               // the Planner app on the phone: everything to do in one agenda (Scripts/Planner)
        public static bool PlannerReminders = true;      // ...and its reminders, sent as the game's own notifications
        public static bool Economy = true;               // the credit rating, the banks' terms by it, loans from the phone and the Finance app (Scripts/Economy)
        public static bool EconomyProducts = true;       // more kinds of credit: an overdraft, a card, a business loan, car finance, a mortgage, consolidation (off: nothing new is opened or drawn; what is owed stays owed)
        public static bool EconomyShark = true;          // the lender of last resort (off: it lends nothing and does nothing; a debt to it can still be paid)
        public static bool EconomyConsequences = true;   // what lenders do when payments are missed (off: only the rating is affected)
        public static int EconomyLevel = 1;              // how hard not paying hits: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore (Economy/Products/ProductCatalogue)
        public static bool EconomyCity = true;           // the city's economy: a central bank, prices, a cycle, districts and supply (off: every figure is the game's own and the model stands still) (Scripts/Economy/City)
        public static int EconomyCityLevel = 1;          // how far it swings: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore (Economy/City/MacroCatalogue)
        public static bool VenturesInvestors = true;     // investors: offers for a business that does well, the pitch, deals (off: no new offers or meetings; deals that exist carry on) (Scripts/Economy/Ventures)
        public static bool Markets = true;               // the city's stock market and the Stocks app (off: the market stands still and nothing is traded; shares held are kept) (Scripts/Markets)
        public static bool MarketsMargin = true;         // a margin account may be opened: borrowing against shares and selling short, with margin calls (off: no new account; one that is open can only be wound down)
        public static int VenturesLevel = 1;             // how demanding investors are: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore (Economy/Ventures/VenturesCatalogue)
        public static bool EconomyCompany = true;        // running a business: staff, insurance, marketing, tax and licences, with the Company app (off: none of it runs and nothing is charged) (Scripts/Economy/Company)
        public static int EconomyCompanyLevel = 1;       // how strict it is: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore (Economy/Company/CompanyCatalogue)
        public static bool EconomyCompanyStaff = true;   // ...the wage market, raises, rivals' offers, strikes, overtime and sick pay
        public static bool EconomyCompanyCover = true;   // ...business insurance: policies, premiums and claims
        public static bool EconomyCompanyMarketing = true;   // ...campaigns and their reports
        public static bool EconomyCompanyTax = true;     // ...the quarterly return and the accountant
        public static bool EconomyCompanyHide = true;    // ...cash sales kept off the books (off: every share is nothing and none can be set)
        public static bool EconomyCompanyLicences = true;    // ...licences and permits
        public static bool EconomyRota = true;           // the staff rota planner: a customer forecast, the hours worth opening, the people they take and a rota at a press, on the Company app's Rota tab (off: the tab says so and nothing is worked out) (Scripts/Economy/Company/CompanyDirector.Rota.cs)
        public static bool Estate = true;                // property: a market that moves, letting a building yourself, works, selling and an estate agency, with the Property app (off: every value and rent is the game's own) (Scripts/Economy/Estate)
        public static bool EstateHotel = true;           // owning a hotel of the city's as a business (off: a hotel that is held stands still and nothing is taken or paid)
        public static bool EstateHire = true;            // car hire: the player's own vehicles go out to customers from a lot (off: no car goes out, and one that is out is called back)
        public static bool EconomyOnline = true;         // online shops: a business or a warehouse sells what it stocks to the whole city, picked by its staff and delivered by van or courier, with the Web shop app (off: no orders come, nothing is charged or paid) (Scripts/Economy/Online)
        public static bool EconomyCleaning = true;       // the cleaning service: cleaners shared between businesses on a daily round, and cleaning contracts with other businesses, in the Services app (off: no round, no contracts, nothing cleaned or charged by it) (Scripts/Economy/Services)
        public static bool EconomySale = true;           // selling a business: its value, the market, offers and a completed sale, in the Services app (off: nothing is offered, paid or charged by it) (Scripts/Economy/Services)
        public static bool EconomyBrands = true;         // brands and franchises: a brand made from a shop, its recognition and standards, franchising it, and a shop under a city chain, with the Brands app (off: none of it runs, nothing is paid and footfall is without it) (Scripts/Economy/Brands)
        public static bool EconomyBanks = true;          // the bank apps: one app on the phone for each of the city's three largest banks, that bank's slice of the accounts, loans, card, offers and rating (off: no bank app; every account and loan stays as it is, and the Finance app still has everything) (Scripts/Economy/Banks)
        public static bool Minimap = true;               // the minimap on the HUD while out in the city (Scripts/Navigation)
        public static bool Gps = true;                   // a route along the roads to the game's destination, with the next turn
        public static bool MinimapRotate = true;         // the minimap turns with the player (off: north is up)
        public static bool NavOtherRoute = false;        // with the mod Voogle Route switched on: its route line only, and none of this mod's (the row is there only then)
        public static int MinimapSize = 80;              // per cent of the width of the game's left column
        public static int MinimapZoom = 100;             // per cent: more is closer
        public static bool MinimapBase = true;           // buildings and water on the minimap, under the roads (off: roads only, as before) (Scripts/Navigation/NavBase.cs)
        public static bool RoadsideChecks = true;        // now and then a patrol car pulls the player over only to see the licence
        public static bool ShowTestTools = false;        // the test buttons are on the options page
        public static bool DevDiagnostics = false;       // the diagnostic keys work and Info lines go to the log

        /// <summary>Raised after the options were read again (a preset, an import, a reset): anything that caches a value looks again.</summary>
        public static event System.Action Changed;

        /// <summary>The mod's id once the options were read; null before.</summary>
        public static string ModId => _modId;

        /// <summary>The PlayerPrefs key of one of the mod's options, in the game's own format.</summary>
        public static string PrefsKey(string id) => _modId == null ? null : Key(_modId, id);

        /// <summary>Reads every option again from PlayerPrefs and tells whoever listens. Never called per frame.</summary>
        public static void Reload()
        {
            if (_modId != null)
            {
                Load(_modId);
                Changed?.Invoke();
            }
        }
        public static bool Garage = true;                // the custom shop at repair bays, and everything it puts on a car
        public static bool GarageCosmetics = true;       // paint, finish, lamp tint, underglow and fitted parts are shown on the player's cars
        public static bool GarageTuning = true;          // performance parts take effect
        public static bool GarageUnderglow = true;       // underglow kits light up after dark
        public static bool GaragePolice = true;          // a straight-through exhaust or riding without a helmet can be fined
        public static bool TwoWheelers = true;           // bicycles and motorbikes: docks, racks, stands, and the ones already out
        public static bool HomeLife = true;              // home life and health as a whole (Scripts/Home)
        public static bool HomeNeeds = true;             // hygiene, stress and how tidy the flat is
        public static bool HomeRoutines = true;          // cooking, coffee, tidying up and floor exercises as activities
        public static bool HomeSleep = true;             // sleep quality, sleep debt, the morning's mood
        public static bool HomeIllness = true;           // colds, flu, food poisoning, back pain, burnout; the doctor at the hospital
        public static bool HomeFitness = true;           // fitness from exercise, diet from what is eaten
        public static bool HomeBody = true;              // the body slowly changes with exercise and food (on waking)
        public static bool HomeJog = true;               // going for a run out of doors
        public static bool HomeMorning = true;           // the morning summary
        public static bool HomeStrip = true;             // the small needs strip on screen
        public static bool HomeEasy = false;             // needs wear down half as fast, illness is half as likely
        public static bool LawPrecincts = true;          // a police precinct in each district: sign, patrol cars, officers at the door, a marker on the map
        public static bool LawCustody = true;            // an arrest ends in a cell at the nearest precinct (off: the old time skip where caught)
        public static bool LawArrests = true;            // arrested for a death even when stopped, and for 911 calls about nothing; wanted after an escape
        public static bool LawReputation = true;         // the record and reputation have effects: scrutiny, thanks from passers-by
        public static bool LawStationFurniture = true;   // ...furnished with the game's own furniture: lockers, desks with computers, a water cooler, plants, a clock, lamps, a toilet in the cell (off: the counter, bench, desks and cell only) (Scripts/Law/StationInterior.Furnish.cs)
        public static bool LawStation = true;            // a precinct can be walked into: front desk, waiting area, holding cell (off: the front desk answers at its door)
        public static bool LawMapMarkers = true;         // police stations on the city map and its filter list, and their markers in the street
        public static bool Buses = true;                 // bus stops near the player, buses that call at them, riding one
        public static bool TransitPeople = true;         // people waiting at a stop, getting on and off
        public static bool SubwayJourney = true;         // the game's own subway ride takes the time of a journey: the wait for a train, the stops, any delay
        public static bool TaxiCall = true;              // the City Cabs contact: a taxi called from the phone comes to the player's street (Scripts/Transport/Taxi)
        public static bool CarOwnership = true;          // distance, servicing, wear, breakdowns, insurance, registration, the fuel gauge
        public static bool CarEasy = false;              // easy car ownership: no fuel use, no breakdowns, no wear
        public static bool Leisure = true;               // everything under Scripts/Leisure
        public static bool LeisureChess = true;          // a chess player sets up on the pavement by day; the player can sit down to a game
        public static bool LeisureFishing = true;        // a line in the water at the waterfront
        public static bool LeisureNightOut = true;       // the dance floor and the bar of a nightclub; drink makes the car wander
        public static bool LeisureSights = true;         // landmarks to take in and tick off
        public static bool LeisureCinema = true;         // cinemas and theatres name what is on, and a show watched is remembered
        public static bool ClothesShops = true;          // the rack in clothing stores and the wardrobe at home
        public static bool ClothesMustBeBought = true;   // what the options page shows; the rule itself is kept per save (Leisure/LeisureState.MustBuy)
        public static bool NightLife = true;             // people outside nightclubs, cinemas and theatres in the evening
        public static bool AmbientSound = true;          // birds, crickets, a far hum of traffic, a murmur where people are
        public static int AmbientBirds = 60;             // percent
        public static int AmbientNight = 60;
        public static int AmbientCity = 60;
        public static int AmbientCrowd = 60;
        public static bool RadioHud = true;              // the station and song named on screen when the car radio changes
        public static bool PhotoMode = true;
        public static bool CityFires = true;             // car, bin and shopfront fires with a fire engine and crew (Scripts/Events)
        public static int FireFrequency = 2;             // 1 = rare, 10 = one every few minutes
        public static bool WeatherMatters = true;        // wet roads, mist, storms, heat and cold change grip, traffic, accidents, crime and energy
        public static int WeatherFrequency = 5;          // how often a summer day is a heat wave and a winter day a cold snap: 1 = hardly ever, 10 = nearly half of them
        public static bool StreetEvents = true;          // markets, parades, protests, fun runs, film shoots, fireworks: on the calendar, announced the day before
        public static int StreetEventFrequency = 4;      // 1 = one day in twelve has one, 10 = four in five (the Saturday market and the fireworks nights are fixed)
        public static bool Social = true;                // the people in the player's life: Mom, Alex and whoever is met; their contacts and the People page
        public static bool SocialMeetings = true;        // new people walk up to the player (after the gym, a shift, a coffee, a good turn)
        public static bool SocialRelationships = true;   // relationships wear down with neglect and can be damaged; off: nothing is ever lost
        public static bool SocialOutings = true;         // plans can be made and kept: coffee, a meal, drinks, the cinema, a walk, a drive, a workout, the shop
        public static bool SocialContact = true;         // they get in touch themselves: invitations, favours, news, a visit at the shop
        public static bool SocialTexts = true;           // text messages from people (off: only what must be said is shown, as a notification)
        public static int SocialTextsMax = 3;            // texts a day from everybody together, 1 to 6
        public static bool SocialNewspaper = true;       // the City Ledger: a morning newspaper, delivered to subscribers' homes and sold at kiosks (Scripts/Ledger)
        public static bool LedgerKiosks = true;          // newspaper kiosks near the player's home sell the paper (off: subscribers only)
        public static bool Hotels = true;                // eight places to stay: reception at the door, the room as a panel (Scripts/Travel)
        public static bool Holidays = true;              // trips bought at the game's travel agencies; burnout after weeks without a break
        public static bool TravelExtras = true;          // suitcase, travel insurance, a companion, trade fair invitations
        public static bool Careers = true;               // employers behind the help-wanted boards: rota, reviews, ranks (Jobs/Career*)
        public static bool Business = true;              // rival moves, inspections, staff and supplier trouble, opportunities for the player's businesses
        public static int BusinessFrequency = 4;         // 1 = something every couple of weeks, 10 = most days (never more than two open at once)
        public static bool BusinessEasy = false;         // "easy business": only the opportunities are left
        public static bool BusinessRivals = true;
        public static bool BusinessRacket = true;
        public static bool BusinessInspections = true;
        public static bool BusinessStaff = true;
        public static bool BusinessSuppliers = true;
        public static bool BusinessCustomers = true;
        public static bool BusinessAudit = true;
        public static bool BusinessOpportunities = true;

        private static string _modId;

        /// <summary>Flips the third-person camera on or off and saves it, so the options screen stays in step.</summary>
        public static void ToggleCamera()
        {
            CameraEnabled = !CameraEnabled;
            if (_modId != null)
            {
                UnityEngine.PlayerPrefs.SetInt(Key(_modId, CameraEnabledId), CameraEnabled ? 1 : 0);
            }
        }

        public static void Load(string modId)
        {
            Meta.SettingsTable.CaptureDefaults();   // the shipped values, before the first saved one replaces them
            _modId = modId;
            CameraEnabled = Bool(modId, CameraEnabledId, CameraEnabled);
            CameraIndoors = Bool(modId, CameraIndoorsId, CameraIndoors);
            CameraPitch = Int(modId, CameraPitchId, CameraPitch);
            CameraDistance = Int(modId, CameraDistanceId, CameraDistance);
            CameraFollow = Int(modId, CameraFollowId, CameraFollow);
            MapPlacesShown = Bool(modId, MapPlacesShownId, MapPlacesShown);
            MouseLook = Bool(modId, MouseLookId, MouseLook);
            MouseSensitivity = Int(modId, MouseSensitivityId, MouseSensitivity);
            EnterWithKey = Bool(modId, EnterWithKeyId, EnterWithKey);
            CameraFov = Int(modId, CameraFovId, CameraFov);
            CameraHeight = Int(modId, CameraHeightId, CameraHeight);
            CarCamera = Bool(modId, CarCameraId, CarCamera);
            CarCameraDistance = Int(modId, CarCameraDistanceId, CarCameraDistance);
            LifeEvents = Bool(modId, LifeEventsId, LifeEvents);
            EventFrequency = Int(modId, EventFrequencyId, EventFrequency);
            CrashConsequences = Bool(modId, CrashConsequencesId, CrashConsequences);
            TrafficAccidents = Bool(modId, TrafficAccidentsId, TrafficAccidents);
            AccidentFrequency = Int(modId, AccidentFrequencyId, AccidentFrequency);
            Siren = Bool(modId, SirenId, Siren);
            Police = Bool(modId, PoliceId, Police);
            PolicePursuit = Bool(modId, PolicePursuitId, PolicePursuit);
            People = Bool(modId, PeopleId, People);
            OfficersOnPatrol = Bool(modId, OfficersOnPatrolId, OfficersOnPatrol);
            PatrolFrequency = Int(modId, PatrolFrequencyId, PatrolFrequency);
            StreetCrime = Bool(modId, StreetCrimeId, StreetCrime);
            CrimeFrequency = Int(modId, CrimeFrequencyId, CrimeFrequency);
            Ragdolls = Bool(modId, RagdollsId, Ragdolls);
            PlayerKnockdown = Bool(modId, PlayerKnockdownId, PlayerKnockdown);
            PedestrianIncidents = Bool(modId, PedestrianIncidentsId, PedestrianIncidents);
            HornReactions = Bool(modId, HornReactionsId, HornReactions);
            NaturalAccidents = Bool(modId, NaturalAccidentsId, NaturalAccidents);
            SolidStreets = Bool(modId, SolidStreetsId, SolidStreets);
            PhoneContacts = Bool(modId, PhoneContactsId, PhoneContacts);
            PhoneSkin = Bool(modId, PhoneSkinId, PhoneSkin);
            CarTuning = Bool(modId, CarTuningId, CarTuning);
            CarTopSpeed = Int(modId, CarTopSpeedId, CarTopSpeed);
            CarAcceleration = Int(modId, CarAccelerationId, CarAcceleration);
            SmoothThrottle = Bool(modId, SmoothThrottleId, SmoothThrottle);
            // The old slider was a percentage of the game's own car; the new two are percentages of each car's own
            // targets, so its saved value means nothing to them. It is dropped and the new sliders start at 100.
            UnityEngine.PlayerPrefs.DeleteKey(Key(modId, CarSpeedId));
            Keys.Load(modId);
            Speedometer = Bool(modId, SpeedometerId, Speedometer);
            VisualsEnabled = Bool(modId, VisualsEnabledId, VisualsEnabled);
            Saturation = Int(modId, SaturationId, Saturation);
            Contrast = Int(modId, ContrastId, Contrast);
            Bloom = Int(modId, BloomId, Bloom);
            Detail = Int(modId, DetailId, Detail);
            CharacterHighDetail = Bool(modId, CharacterHighDetailId, CharacterHighDetail);
            CharacterHair = Bool(modId, CharacterHairId, CharacterHair);
            CharacterLighting = Bool(modId, CharacterLightingId, CharacterLighting);
            CharacterNewLooks = Bool(modId, CharacterNewLooksId, CharacterNewLooks);
            CharacterSkin = Bool(modId, CharacterSkinId, CharacterSkin);
            CharacterCatalogue.Load(modId);
            CrimeAgainstPlayer = Bool(modId, CrimeAgainstPlayerId, CrimeAgainstPlayer);
            PlayerOffences = Bool(modId, PlayerOffencesId, PlayerOffences);
            PolicePresence = Int(modId, PolicePresenceId, PolicePresence);
            CityLife = Bool(modId, CityLifeId, CityLife);
            CityDensity = Int(modId, CityDensityId, CityDensity);
            CityWorkers = Bool(modId, CityWorkersId, CityWorkers);
            CityEveryday = Bool(modId, CityEverydayId, CityEveryday);
            CityInteractions = Bool(modId, CityInteractionsId, CityInteractions);
            PerfTweaks.Load(modId);
            JusticeSettings.Load(modId);   // courts and prison (Scripts/Justice)
            RoommateOptions.Load(modId);   // sharing the flat (Scripts/Home/Roommates)
            Jobs = Bool(modId, JobsId, Jobs);
            JobPay = Int(modId, JobPayId, JobPay);
            Family = Bool(modId, FamilyId, Family);
            Utilities = Bool(modId, UtilitiesId, Utilities);
            FamilyAtHome = Bool(modId, FamilyAtHomeId, FamilyAtHome);
            FamilyWedding = Bool(modId, FamilyWeddingId, FamilyWedding);
            FamilyChildren = Bool(modId, FamilyChildrenId, FamilyChildren);
            FamilyRelatives = Bool(modId, FamilyRelativesId, FamilyRelatives);
            FamilyFriction = Bool(modId, FamilyFrictionId, FamilyFriction);
            FamilyInterest = Int(modId, FamilyInterestId, FamilyInterest);
            FamilyYearWeeks = Int(modId, FamilyYearWeeksId, FamilyYearWeeks);
            Civic = Bool(modId, CivicId, Civic);
            CivicElections = Bool(modId, CivicElectionsId, CivicElections);
            CivicConstruction = Bool(modId, CivicConstructionId, CivicConstruction);
            DrivingLicence = Bool(modId, DrivingLicenceId, DrivingLicence);
            StartWithLicence = Bool(modId, StartWithLicenceId, StartWithLicence);
            DrivingSchool = Bool(modId, DrivingSchoolId, DrivingSchool);
            LicencePoints = Bool(modId, LicencePointsId, LicencePoints);
            Planner = Bool(modId, PlannerId, Planner);
            PlannerReminders = Bool(modId, PlannerRemindersId, PlannerReminders);
            Economy = Bool(modId, EconomyId, Economy);
            EconomyProducts = Bool(modId, EconomyProductsId, EconomyProducts);
            EconomyShark = Bool(modId, EconomySharkId, EconomyShark);
            EconomyConsequences = Bool(modId, EconomyConsequencesId, EconomyConsequences);
            EconomyLevel = Int(modId, EconomyLevelId, EconomyLevel);
            EconomyCity = Bool(modId, EconomyCityId, EconomyCity);
            EconomyCityLevel = Int(modId, EconomyCityLevelId, EconomyCityLevel);
            VenturesInvestors = Bool(modId, VenturesInvestorsId, VenturesInvestors);
            Markets = Bool(modId, MarketsId, Markets);
            MarketsMargin = Bool(modId, MarketsMarginId, MarketsMargin);
            VenturesLevel = Int(modId, VenturesLevelId, VenturesLevel);
            EconomyCompany = Bool(modId, EconomyCompanyId, EconomyCompany);
            EconomyCompanyLevel = Int(modId, EconomyCompanyLevelId, EconomyCompanyLevel);
            EconomyCompanyStaff = Bool(modId, EconomyCompanyStaffId, EconomyCompanyStaff);
            EconomyCompanyCover = Bool(modId, EconomyCompanyCoverId, EconomyCompanyCover);
            EconomyCompanyMarketing = Bool(modId, EconomyCompanyMarketingId, EconomyCompanyMarketing);
            EconomyCompanyTax = Bool(modId, EconomyCompanyTaxId, EconomyCompanyTax);
            EconomyCompanyHide = Bool(modId, EconomyCompanyHideId, EconomyCompanyHide);
            EconomyCompanyLicences = Bool(modId, EconomyCompanyLicencesId, EconomyCompanyLicences);
            EconomyRota = Bool(modId, EconomyRotaId, EconomyRota);
            Estate = Bool(modId, EstateId, Estate);
            EstateHotel = Bool(modId, EstateHotelId, EstateHotel);
            EstateHire = Bool(modId, EstateHireId, EstateHire);
            EconomyOnline = Bool(modId, EconomyOnlineId, EconomyOnline);
            EconomyCleaning = Bool(modId, EconomyCleaningId, EconomyCleaning);
            EconomySale = Bool(modId, EconomySaleId, EconomySale);
            EconomyBrands = Bool(modId, EconomyBrandsId, EconomyBrands);
            EconomyBanks = Bool(modId, EconomyBanksId, EconomyBanks);
            Minimap = Bool(modId, MinimapId, Minimap);
            Gps = Bool(modId, GpsId, Gps);
            MinimapRotate = Bool(modId, MinimapRotateId, MinimapRotate);
            NavOtherRoute = Bool(modId, NavOtherRouteId, NavOtherRoute);
            MinimapSize = Int(modId, MinimapSizeId, MinimapSize);
            MinimapZoom = Int(modId, MinimapZoomId, MinimapZoom);
            MinimapBase = Bool(modId, MinimapBaseId, MinimapBase);
            RoadsideChecks = Bool(modId, RoadsideChecksId, RoadsideChecks);
            ShowTestTools = Bool(modId, ShowTestToolsId, ShowTestTools);
            DevDiagnostics = Bool(modId, DevDiagnosticsId, DevDiagnostics);
            Garage = Bool(modId, GarageId, Garage);
            GarageCosmetics = Bool(modId, GarageCosmeticsId, GarageCosmetics);
            GarageTuning = Bool(modId, GarageTuningId, GarageTuning);
            GarageUnderglow = Bool(modId, GarageUnderglowId, GarageUnderglow);
            GaragePolice = Bool(modId, GaragePoliceId, GaragePolice);
            TwoWheelers = Bool(modId, TwoWheelersId, TwoWheelers);
            HomeLife = Bool(modId, HomeLifeId, HomeLife);
            HomeNeeds = Bool(modId, HomeNeedsId, HomeNeeds);
            HomeRoutines = Bool(modId, HomeRoutinesId, HomeRoutines);
            HomeSleep = Bool(modId, HomeSleepId, HomeSleep);
            HomeIllness = Bool(modId, HomeIllnessId, HomeIllness);
            HomeFitness = Bool(modId, HomeFitnessId, HomeFitness);
            HomeBody = Bool(modId, HomeBodyId, HomeBody);
            HomeJog = Bool(modId, HomeJogId, HomeJog);
            HomeMorning = Bool(modId, HomeMorningId, HomeMorning);
            HomeStrip = Bool(modId, HomeStripId, HomeStrip);
            HomeEasy = Bool(modId, HomeEasyId, HomeEasy);
            LawPrecincts = Bool(modId, LawPrecinctsId, LawPrecincts);
            LawCustody = Bool(modId, LawCustodyId, LawCustody);
            LawArrests = Bool(modId, LawArrestsId, LawArrests);
            LawReputation = Bool(modId, LawReputationId, LawReputation);
            LawStation = Bool(modId, LawStationId, LawStation);
            LawStationFurniture = Bool(modId, LawStationFurnitureId, LawStationFurniture);
            LawMapMarkers = Bool(modId, LawMapMarkersId, LawMapMarkers);
            Buses = Bool(modId, BusesId, Buses);
            TransitPeople = Bool(modId, TransitPeopleId, TransitPeople);
            SubwayJourney = Bool(modId, SubwayJourneyId, SubwayJourney);
            TaxiCall = Bool(modId, TaxiCallId, TaxiCall);
            CarOwnership = Bool(modId, CarOwnershipId, CarOwnership);
            CarEasy = Bool(modId, CarEasyId, CarEasy);
            Leisure = Bool(modId, LeisureId, Leisure);
            LeisureChess = Bool(modId, LeisureChessId, LeisureChess);
            LeisureFishing = Bool(modId, LeisureFishingId, LeisureFishing);
            LeisureNightOut = Bool(modId, LeisureNightOutId, LeisureNightOut);
            LeisureSights = Bool(modId, LeisureSightsId, LeisureSights);
            LeisureCinema = Bool(modId, LeisureCinemaId, LeisureCinema);
            ClothesShops = Bool(modId, ClothesShopsId, ClothesShops);
            ClothesMustBeBought = Bool(modId, ClothesMustBeBoughtId, ClothesMustBeBought);
            NightLife = Bool(modId, NightLifeId, NightLife);
            AmbientSound = Bool(modId, AmbientSoundId, AmbientSound);
            AmbientBirds = Int(modId, AmbientBirdsId, AmbientBirds);
            AmbientNight = Int(modId, AmbientNightId, AmbientNight);
            AmbientCity = Int(modId, AmbientCityId, AmbientCity);
            AmbientCrowd = Int(modId, AmbientCrowdId, AmbientCrowd);
            RadioHud = Bool(modId, RadioHudId, RadioHud);
            PhotoMode = Bool(modId, PhotoModeId, PhotoMode);
            CityFires = Bool(modId, CityFiresId, CityFires);
            FireFrequency = Int(modId, FireFrequencyId, FireFrequency);
            WeatherMatters = Bool(modId, WeatherMattersId, WeatherMatters);
            WeatherFrequency = Int(modId, WeatherFrequencyId, WeatherFrequency);
            StreetEvents = Bool(modId, StreetEventsId, StreetEvents);
            StreetEventFrequency = Int(modId, StreetEventFrequencyId, StreetEventFrequency);
            Social = Bool(modId, SocialId, Social);
            SocialMeetings = Bool(modId, SocialMeetingsId, SocialMeetings);
            SocialRelationships = Bool(modId, SocialRelationshipsId, SocialRelationships);
            SocialOutings = Bool(modId, SocialOutingsId, SocialOutings);
            SocialContact = Bool(modId, SocialContactId, SocialContact);
            SocialTexts = Bool(modId, SocialTextsId, SocialTexts);
            SocialTextsMax = Int(modId, SocialTextsMaxId, SocialTextsMax);
            SocialNewspaper = Bool(modId, SocialNewspaperId, SocialNewspaper);
            LedgerKiosks = Bool(modId, LedgerKiosksId, LedgerKiosks);
            Hotels = Bool(modId, HotelsId, Hotels);
            Holidays = Bool(modId, HolidaysId, Holidays);
            TravelExtras = Bool(modId, TravelExtrasId, TravelExtras);
            Careers = Bool(modId, CareersId, Careers);
            Business = Bool(modId, BusinessId, Business);
            BusinessFrequency = Int(modId, BusinessFrequencyId, BusinessFrequency);
            BusinessEasy = Bool(modId, BusinessEasyId, BusinessEasy);
            BusinessRivals = Bool(modId, BusinessRivalsId, BusinessRivals);
            BusinessRacket = Bool(modId, BusinessRacketId, BusinessRacket);
            BusinessInspections = Bool(modId, BusinessInspectionsId, BusinessInspections);
            BusinessStaff = Bool(modId, BusinessStaffId, BusinessStaff);
            BusinessSuppliers = Bool(modId, BusinessSuppliersId, BusinessSuppliers);
            BusinessCustomers = Bool(modId, BusinessCustomersId, BusinessCustomers);
            BusinessAudit = Bool(modId, BusinessAuditId, BusinessAudit);
            BusinessOpportunities = Bool(modId, BusinessOpportunitiesId, BusinessOpportunities);
        }

        /// <summary>
        /// "Clothes must be bought" is kept per save (Leisure/LeisureState); when a save is loaded its value is written
        /// here and to PlayerPrefs, so the options page shows what holds for the game being played.
        /// </summary>
        public static void SetClothesMustBeBought(bool on)
        {
            ClothesMustBeBought = on;
            if (_modId != null)
            {
                UnityEngine.PlayerPrefs.SetInt(Key(_modId, ClothesMustBeBoughtId), on ? 1 : 0);
            }
        }

        // Same key format the game uses when it saves mod options.
        private static string Key(string modId, string id) => "m:" + modId + ":" + id;

        private static bool Bool(string modId, string id, bool fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback ? 1 : 0) != 0;

        private static int Int(string modId, string id, int fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback);
    }
}
