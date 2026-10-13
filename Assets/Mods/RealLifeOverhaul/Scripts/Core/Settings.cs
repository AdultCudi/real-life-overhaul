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
        public const string SpecialNamesId = "special_names";
        public const string DistrictViewId = "district_view";
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
        public const string AccidentNoticesId = "accident_notices";
        public const string AccidentMarkerId = "accident_marker";
        public const string TowTrucksId = "tow_trucks";
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
        public const string CivicOfficeId = "civic_office";
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
        public const string MarketsListingId = "markets_listing";
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
        public const string EstateRentId = "economy_estate_rent";
        public const string EconomyOnlineId = "economy_online";
        public const string TechPeopleId = "tech_people";
        public const string TechJobsId = "tech_jobs";
        public const string TechSectorId = "tech_sector";
        public const string TechStartupsId = "tech_startups";
        public const string EconomyDealerId = "economy_dealer";
        public const string EconomyCleaningId = "economy_cleaning";
        public const string EconomySaleId = "economy_sale";
        public const string EconomyBankId = "economy_bank";
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
        public const string SocialMediaId = "social_media";
        public const string SocialMediaNoticesId = "social_media_notices";
        public const string StandingId = "standing";
        public const string StandingStreetId = "standing_street";
        public const string StandingPressId = "standing_press";
        public const string StandingCorruptionId = "standing_corruption";
        public const string LedgerKiosksId = "ledger_kiosks";
        public const string LedgerOnlineId = "ledger_online";
        public const string LedgerOnlineNotesId = "ledger_online_notes";
        public const string HotelsId = "travel_hotels";
        public const string HolidaysId = "travel_holidays";
        public const string TravelExtrasId = "travel_extras";
        public const string CareersId = "careers";
        public const string CareersStudyId = "careers_study";
        public const string CareersDegreesId = "careers_degrees";
        public const string CareersJobsId = "careers_jobs";
        public const string CareersLifeId = "careers_life";
        public const string CareersOwnId = "careers_own";
        public const string CareersFamilyId = "careers_family";
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
        public const string InteriorsId = "interiors";
        public const string InteriorsStandardLevelId = "interiors_standard_level";
        public const string InteriorsRewardLevelId = "interiors_reward_level";
        public const string InteriorsDecorLevelId = "interiors_decor_level";
        public const string InteriorsPricesLevelId = "interiors_prices_level";
        public const string InteriorsWearLevelId = "interiors_wear_level";
        public const string InteriorsBasketId = "interiors_basket";
        // The switches the five strengths above replaced (0.8.0): read once, when a strength has no saved choice yet, so
        // that a part switched off stays off. They have no row of their own any more.
        private const string StandardSwitchWas = "interiors_standard", DecorSwitchWas = "interiors_decor", PricesSwitchWas = "interiors_prices", WearSwitchWas = "interiors_wear";
        public const string InteriorsStandingId = "interiors_standing";
        public const string InteriorsStaffId = "interiors_staff";
        public const string PermadeathId = "permadeath";

        public static bool CameraEnabled = true;
        public static bool CameraIndoors = false;
        public static int CameraPitch = 18;      // degrees above the horizon
        public static int CameraDistance = 6;    // metres behind the player
        public static int CameraFollow = 5;      // 0 = never swing behind the player (only without mouse look)
        public static bool DistrictView = true;     // the district view on the city map: its rows in the map's filter list, the tint and the card (Districts/DistrictView)
        public static bool MapPlacesShown = true;   // the mod's places on the city map: their markers and their rows in the map's filter list (Core/MapPlaces; off hides all of them)
        public static bool SpecialNames = true;     // the mod's special buildings carry their own names wherever the game names a building (Core/SpecialBuildings; off: the game's own names)
        public static bool MouseLook = true;     // GTA-style: mouse steers the camera, cursor locked
        public static int MouseSensitivity = 6;
        public static bool EnterWithKey = true;  // E/F near a door enters the building
        public static int CameraFov = 55;        // degrees, vertical
        public static bool LifeEvents = true;
        public static int EventFrequency = 5;        // about this many random events per game week
        public static bool CrashConsequences = true;
        public static bool TrafficAccidents = true;
        public static bool AccidentNotices = true;   // the notifications (and their sound) as an accident of other people's unfolds; off, it still happens
        public static bool AccidentMarker = true;    // the game's destination marker on an accident, and so the route to it
        public static bool TowTrucks = true;         // a tow truck takes a wreck away once its scene has cleared (Traffic/Tow); off, the wreck simply goes
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
        public static bool CivicOffice = true;           // the player may stand for the council or for mayor (Civic/CivicOffice)
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
        public static bool MarketsListing = true;        // the player's own company may come to the market (off: no new listing is begun; a company on the market stays there until taken private) (Scripts/Markets/Listing)
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
        public static int EstateRent = 100;              // rental income: percent of the market's rent of a unit the player lets himself, and so of what is asked, offered and renewed at (50 to 300) (Scripts/Economy/Estate/EstateRules.MarketRent)
        public static bool EstateHire = true;            // car hire: the player's own vehicles go out to customers from a lot (off: no car goes out, and one that is out is called back)
        public static bool TechPeople = true;            // professionals: roles, levels and skills of their own for the player's professional staff, reviews, promotions, courses, notice and severance, rivals' offers, in the Open Roles app (off: the game's own staff alone; files are kept) (Scripts/Tech/People)
        public static bool TechJobs = true;              // the jobs board and the labour market: ads in the Open Roles app, applicants from the city's job seekers, interviews, offers, executive search, and more candidates from the recruitment office (off: the game's own hiring alone) (Scripts/Tech/Hiring)
        public static bool TechSector = true;            // the tech sector: software studios, game studios, IT consultancies, cybersecurity firms and AI labs of the player's, products with customers and recurring revenue, client contracts and tenders, outages and breaches, in the Tech app (off: those businesses earn nothing of their own; their files are kept) (Scripts/Tech/Firms)
        public static bool TechStartups = true;          // startups and venture capital: the city's startups that grow, raise, fail or are bought, their pitches to the player, an angel portfolio and a fund, and the player's own tech firms raising from venture firms with a board and share options (off: no startup is founded or pitches; holdings are kept) (Scripts/Tech/Venture)
        public static bool EconomyOnline = true;         // online shops: a business or a warehouse sells what it stocks to the whole city, picked by its staff and delivered by van or courier, with the Web shop app (off: no orders come, nothing is charged or paid) (Scripts/Economy/Online)
        public static bool EconomyDealer = true;         // the car dealership: a business type of the mod's own that buys cars new, used and at auction and sells them to customers, with its own finance and warranties and the Dealership app (off: its dealerships do not trade; their cars stay on the lots) (Scripts/Economy/Dealer)
        public static bool EconomyCleaning = true;       // the cleaning service: cleaners shared between businesses on a daily round, and cleaning contracts with other businesses, in the Services app (off: no round, no contracts, nothing cleaned or charged by it) (Scripts/Economy/Services)
        public static bool EconomySale = true;           // selling a business: its value, the market, offers and a completed sale, in the Services app (off: nothing is offered, paid or charged by it) (Scripts/Economy/Services)
        public static bool EconomyBank = true;           // the player's own bank: a licence, savers, loans, a fund and a regulator, in its own app (off: it stands still, nothing is run, booked or charged) (Scripts/Economy/OwnBank)
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
        public static bool LawStationFurniture = true;   // ...furnished with the game's own furniture and the walls and floors of its office design for the unit: waiting chairs and a queue, desks with computers, lockers, a briefing row, a break table, a toilet in the cell, and the officers and visitors who use them (off: the counter, bench, desks and cell only, and the people as before) (Scripts/Law/StationInterior.Furnish.cs, .Rooms.cs, .Design.cs, .Cast.cs)
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
        public static bool SocialMedia = true;           // Flare, the city's social media app: an account, posts about what really happened, friends who follow (Scripts/Social/Flare)
        public static bool SocialMediaNotices = true;    // Flare's few notifications (off: none at all; the app's badge still counts)
        public static bool Standing = true;              // Standing: the city knows the player's wealth, name and reputation, and reacts (Scripts/Fame)
        public static bool StandingStreet = true;        // ...people in the street and customers in the player's shops know them
        public static bool StandingPress = true;         // ...reporters, the night show, magazines and the papers' pieces about the player
        public static bool StandingCorruption = true;    // ...officials who offer to speed things up for a bribe, and what comes of it
        public static bool SocialNewspaper = true;       // the City Ledger: a morning newspaper, delivered to subscribers' homes and sold at kiosks (Scripts/Ledger)
        public static bool LedgerKiosks = true;          // newspaper kiosks near the player's home sell the paper (off: subscribers only)
        public static bool LedgerOnline = true;          // Newsstand: the City Ledger and three other titles read on the phone by subscription, over the home's internet
        public static bool LedgerOnlineNotes = true;     // a notification each morning the online editions are in
        public static bool Hotels = true;                // eight places to stay: reception at the door, the room as a panel (Scripts/Travel)
        public static bool Holidays = true;              // trips bought at the game's travel agencies; burnout after weeks without a break
        public static bool TravelExtras = true;          // suitcase, travel insurance, a companion, trade fair invitations
        public static bool Careers = true;               // employers behind the help-wanted boards: rota, reviews, ranks (Jobs/Career*)
        public static bool CareersStudy = true;          // the city's courses: short courses and diplomas at the school with a timetable, homework and an exam, and the player's own skills, in the Campus app (off: the game's own courses alone; records are kept) (Scripts/Careers)
        public static bool CareersDegrees = true;        // the university: degrees at Hudson City University and on the wards, professional certificates studied at home, the student loan, grants and graduation day (off: those courses are not offered; results are kept) (Scripts/Careers)
        public static bool CareersJobs = true;           // professional jobs: the city's businesses, the rivals', its tech companies and the public sector take the player on by their qualifications and skills, with applications, interviews, offers and working days, in the Career app (off: no vacancies; a job held is kept) (Scripts/Careers)
        public static bool CareersLife = true;           // working life in a profession: reviews, rises, bonuses and promotions, office matters, overtime and burnout, search firms, redundancy and benefit, share options, the pension and the top seats of a rival's company (off: a job is its days and pay alone) (Scripts/Careers)
        public static bool CareersFamily = true;         // the family's children at university, tutoring from home, and the player's own training academy as a business type (off: none of them; records are kept) (Scripts/Careers)
        public static bool CareersOwn = true;            // working for oneself: leaving a profession to found a company or open a practice, former colleagues, restraint clauses and claims, freelance contracts and side work (off: no contracts come; a practice is kept) (Scripts/Careers)
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
        public static bool Interiors = true;                // how a business looks counts: the game's interior design score, a decor score from the placed items, wear, the district's standard, and a BizMan tab (off: none of the parts below, and the tab is gone) (Scripts/Interiors)
        public static int InteriorsStandardLevel = 2;   // (InteriorRules.StandardDefault) each district expects a standard: what falling short of it costs (customers, complaints): 0 Off, 1 Gentle, 2 Standard, 3 Strict (with Meaningful Interiors on: nothing) (Scripts/Interiors)
        public static int InteriorsRewardLevel = 3;       // (InteriorRules.RewardDefault) what being over it brings (customers, the basket's chance): 0 Off, 1 Low, 2 Conservative, 3 Standard, 4 Generous (with Meaningful Interiors on: nothing) (Scripts/Interiors)
        public static int InteriorsDecorLevel = 1;         // (InteriorRules.DecorDefault) the customers decor brings, and an office's clients: 0 Off, 1 5%, 2 10%, 3 15%, 4 20% at a decor score of 100 (with Meaningful Ambience on: nothing) (Scripts/Interiors)
        public static int InteriorsPricesLevel = 3;        // (InteriorRules.PriceDefault) how much more (or less) of the price the district bears: 0 Off, 1 Low, 2 Conservative, 3 Standard, 4 Generous (Scripts/Interiors)
        public static int InteriorsWearLevel = 2;           // (InteriorRules.WearDefault) how fast finishes wear and get dirty: 0 Off, 1 Slow, 2 Standard, 3 Fast (Scripts/Interiors)
        public static bool InteriorsBasket = false;         // over the standard a customer may take one more product the shop has in stock; off by default: it adds to the game's own list of the hour's customers (with Meaningful Interiors on: nothing) (Scripts/Interiors)
        public static bool InteriorsStanding = true;        // a well kept place lifts its brand's standards and Standing's customers, and makes the news; a shabby one the other way (Scripts/Interiors)
        public static bool InteriorsStaff = true;           // plants, good light and a rest corner lift staff; a good office helps hiring and a firm's bids (Scripts/Interiors)
        public static bool Permadeath = false;           // what the options page shows; the switch itself is kept per save (Permadeath/PermadeathState.Armed), off unless switched on

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
            bool starting = _modId == null;   // the first read in this run of the game: no save is loaded yet
            _modId = modId;
            if (starting)
            {
                // The game starts in the main menu, where the switch is no save's: off, until moved there for the next game begun.
                UnityEngine.PlayerPrefs.SetInt(Key(modId, PermadeathId), 0);
            }
            Permadeath = Bool(modId, PermadeathId, false);
            CameraEnabled = Bool(modId, CameraEnabledId, CameraEnabled);
            CameraIndoors = Bool(modId, CameraIndoorsId, CameraIndoors);
            CameraPitch = Int(modId, CameraPitchId, CameraPitch);
            CameraDistance = Int(modId, CameraDistanceId, CameraDistance);
            CameraFollow = Int(modId, CameraFollowId, CameraFollow);
            MapPlacesShown = Bool(modId, MapPlacesShownId, MapPlacesShown);
            SpecialNames = Bool(modId, SpecialNamesId, SpecialNames);
            DistrictView = Bool(modId, DistrictViewId, DistrictView);
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
            AccidentNotices = Bool(modId, AccidentNoticesId, AccidentNotices);
            AccidentMarker = Bool(modId, AccidentMarkerId, AccidentMarker);
            TowTrucks = Bool(modId, TowTrucksId, TowTrucks);
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
            CivicOffice = Bool(modId, CivicOfficeId, CivicOffice);
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
            MarketsListing = Bool(modId, MarketsListingId, MarketsListing);
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
            EstateRent = Int(modId, EstateRentId, EstateRent);
            EconomyOnline = Bool(modId, EconomyOnlineId, EconomyOnline);
            TechPeople = Bool(modId, TechPeopleId, TechPeople);
            TechJobs = Bool(modId, TechJobsId, TechJobs);
            TechSector = Bool(modId, TechSectorId, TechSector);
            TechStartups = Bool(modId, TechStartupsId, TechStartups);
            EconomyDealer = Bool(modId, EconomyDealerId, EconomyDealer);
            EconomyCleaning = Bool(modId, EconomyCleaningId, EconomyCleaning);
            EconomySale = Bool(modId, EconomySaleId, EconomySale);
            EconomyBank = Bool(modId, EconomyBankId, EconomyBank);
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
            SocialMedia = Bool(modId, SocialMediaId, SocialMedia);
            SocialMediaNotices = Bool(modId, SocialMediaNoticesId, SocialMediaNotices);
            Standing = Bool(modId, StandingId, Standing);
            StandingStreet = Bool(modId, StandingStreetId, StandingStreet);
            StandingPress = Bool(modId, StandingPressId, StandingPress);
            StandingCorruption = Bool(modId, StandingCorruptionId, StandingCorruption);
            LedgerKiosks = Bool(modId, LedgerKiosksId, LedgerKiosks);
            LedgerOnline = Bool(modId, LedgerOnlineId, LedgerOnline);
            LedgerOnlineNotes = Bool(modId, LedgerOnlineNotesId, LedgerOnlineNotes);
            Hotels = Bool(modId, HotelsId, Hotels);
            Holidays = Bool(modId, HolidaysId, Holidays);
            TravelExtras = Bool(modId, TravelExtrasId, TravelExtras);
            Careers = Bool(modId, CareersId, Careers);
            CareersStudy = Bool(modId, CareersStudyId, CareersStudy);
            CareersDegrees = Bool(modId, CareersDegreesId, CareersDegrees);
            CareersJobs = Bool(modId, CareersJobsId, CareersJobs);
            CareersLife = Bool(modId, CareersLifeId, CareersLife);
            CareersOwn = Bool(modId, CareersOwnId, CareersOwn);
            CareersFamily = Bool(modId, CareersFamilyId, CareersFamily);
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
            Interiors = Bool(modId, InteriorsId, Interiors);
            InteriorsStandardLevel = Int(modId, InteriorsStandardLevelId, Bool(modId, StandardSwitchWas, true) ? InteriorsStandardLevel : 0);
            InteriorsRewardLevel = Int(modId, InteriorsRewardLevelId, Bool(modId, StandardSwitchWas, true) ? InteriorsRewardLevel : 0);
            InteriorsDecorLevel = Int(modId, InteriorsDecorLevelId, Bool(modId, DecorSwitchWas, true) ? InteriorsDecorLevel : 0);
            InteriorsPricesLevel = Int(modId, InteriorsPricesLevelId, Bool(modId, PricesSwitchWas, true) ? InteriorsPricesLevel : 0);
            InteriorsWearLevel = Int(modId, InteriorsWearLevelId, Bool(modId, WearSwitchWas, true) ? InteriorsWearLevel : 0);
            InteriorsBasket = Bool(modId, InteriorsBasketId, InteriorsBasket);
            InteriorsStanding = Bool(modId, InteriorsStandingId, InteriorsStanding);
            InteriorsStaff = Bool(modId, InteriorsStaffId, InteriorsStaff);
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

        /// <summary>
        /// "Permadeath" is kept per save (Permadeath/PermadeathState); the save being played writes it here and to PlayerPrefs,
        /// so that the options page shows what holds for it, and the main menu writes it off. True if it changed.
        /// </summary>
        public static bool SetPermadeath(bool on)
        {
            bool changed = Permadeath != on;
            Permadeath = on;
            if (_modId != null)
            {
                string key = Key(_modId, PermadeathId);
                changed |= UnityEngine.PlayerPrefs.GetInt(key, 0) != (on ? 1 : 0);
                UnityEngine.PlayerPrefs.SetInt(key, on ? 1 : 0);
            }
            return changed;
        }

        // Same key format the game uses when it saves mod options.
        private static string Key(string modId, string id) => "m:" + modId + ":" + id;

        private static bool Bool(string modId, string id, bool fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback ? 1 : 0) != 0;

        private static int Int(string modId, string id, int fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback);
    }
}
