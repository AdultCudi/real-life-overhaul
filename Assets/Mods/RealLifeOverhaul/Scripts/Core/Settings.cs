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
        public const string CityFaultsId = "city_faults";
        public const string CityFaultsCountId = "city_faults_count";
        public const string CityFaultsFalseCostId = "city_faults_false_cost";
        public const string TextsId = "texts";
        public const string TextsDeviceId = "texts_device";
        public const string TextsThreadsId = "texts_threads";
        public const string TextsRepliesId = "texts_replies";
        public const string TextsPostsId = "texts_posts";
        public const string TextsPeopleId = "texts_people";
        public const string TextsOfficialId = "texts_official";
        public const string TextsRivalsId = "texts_rivals";
        public const string TextsLedgerId = "texts_ledger";
        public const string TextsServiceId = "texts_service";
        public const string TextsCityId = "texts_city";
        public const string PhoneCameraId = "phone_camera";
        public const string PhoneCameraGridId = "phone_camera_grid";
        public const string MouseLookId = "camera_mouselook";
        public const string MouseSensitivityId = "camera_sensitivity";
        public const string EnterWithKeyId = "enter_with_key";
        public const string WalkToggleId = "walk_toggle";
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
        public const string CityDriversId = "city_drivers";
        public const string PhoneContactsId = "phone_contacts";
        public const string PhoneSkinId = "phone_skin";
        public const string PhoneFoldersId = "phone_folders";
        public const string NoticeLookId = "notice_look";
        public const string CarSpeedId = "car_speed";           // the old single "car speed %" slider: dropped at load
        public const string CarTuningId = "car_tuning";
        public const string CarTopSpeedId = "car_top_speed";
        public const string CarAccelerationId = "car_acceleration";
        public const string SmoothThrottleId = "smooth_throttle";
        public const string SpeedometerId = "speedometer";
        public const string HandlingId = "car_handling";
        public const string HandlingSoftnessId = "car_handling_softness";
        public const string HandlingBounceId = "car_handling_bounce";
        public const string HandlingRollId = "car_handling_roll";
        public const string HandlingGripId = "car_handling_grip";
        public const string HandlingSteeringId = "car_handling_steering";
        public const string HandlingBrakesId = "car_handling_brakes";
        public const string VisualsEnabledId = "visuals_enabled";
        public const string SaturationId = "visuals_saturation";
        public const string ContrastId = "visuals_contrast";
        public const string BloomId = "visuals_bloom";
        public const string DetailId = "visuals_detail";
        public const string ShadowReachId = "render_shadow_reach";
        public const string ShadowCascadesId = "render_shadow_cascades";
        public const string LodReachId = "render_lod_reach";
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
        public const string FamilyWorkId = "family_work";
        public const string FamilyWealthMoodId = "family_wealth_mood";
        public const string FamilyExpectId = "family_expect";
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
        public const string PlannerMorningId = "planner_morning";
        public const string CalendarId = "calendar";
        public const string EconomyId = "economy";
        public const string EconomyGradeUpNotesId = "economy_grade_up_notes";
        public const string EconomyProductsId = "economy_products";
        public const string EconomySharkId = "economy_shark";
        public const string EconomyConsequencesId = "economy_consequences";
        public const string EconomyLevelId = "economy_level";
        public const string EconomyCityId = "economy_city";
        public const string EconomyCityLevelId = "economy_city_level";
        public const string EconomyRateAheadId = "economy_rate_ahead";
        public const string VenturesInvestorsId = "ventures_investors";
        public const string MarketsId = "ventures_market";        // (the old market's option: kept, so that a player's choice carries over to the city's stock market)
        public const string MarketsMarginId = "markets_margin";
        public const string MarketsListingId = "markets_listing";
        public const string MarketsBotsId = "markets_bots";
        public const string MarketsDividendNotesId = "markets_dividend_notes";
        public const string MarketsBondsId = "markets_bonds";
        public const string MarketsBondSoFarId = "markets_bond_so_far";
        public const string MarketsLargeId = "markets_large";
        public const string MarketsReinvestId = "markets_reinvest";
        public const string MarketsDerivativesId = "markets_derivatives";
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
        public const string EconomyManagersId = "economy_managers";
        public const string EstateId = "economy_estate";
        public const string EstateHotelId = "economy_estate_hotel";
        public const string EstateHireId = "economy_estate_hire";
        public const string EstateRentId = "economy_estate_rent";
        public const string EconomyOnlineId = "economy_online";
        public const string EconomyOnlineWaitNotesId = "economy_online_wait_notes";
        public const string TechPeopleId = "tech_people";
        public const string TechJobsId = "tech_jobs";
        public const string TechSectorId = "tech_sector";
        public const string TechOfferNotesId = "tech_offer_notes";
        public const string TechStartupsId = "tech_startups";
        public const string TechSiteRoomsId = "tech_site_rooms";
        public const string TechSiteHqId = "tech_site_hq";
        public const string TechSiteCloudId = "tech_site_cloud";
        public const string TechSiteOfficesId = "tech_site_offices";
        public const string EconomyDealerId = "economy_dealer";
        public const string EconomyDealerFloorId = "economy_dealer_floor";
        public const string EconomyCleaningId = "economy_cleaning";
        public const string EconomySaleId = "economy_sale";
        public const string OfficeConversionId = "office_conversion";
        public const string EconomyBankId = "economy_bank";
        public const string EconomyBranchId = "economy_bank_branch";
        public const string EndScaleId = "economy_endscale";
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
        public const string GarageSoundsId = "garage_sounds";
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
        public const string HomeMorningNewsId = "home_morning_news";
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
        public const string StreetProtestNotesId = "street_protest_notes";
        public const string SocialId = "social";
        public const string SocialMeetingsId = "social_meetings";
        public const string SocialRelationshipsId = "social_relationships";
        public const string SocialOutingsId = "social_outings";
        public const string SocialContactId = "social_contact";
        public const string SocialTextsId = "social_texts";
        public const string SocialTextsMaxId = "social_texts_max";
        public const string SocialComposerId = "social_composer";
        public const string StreetTalkId = "street_talk";
        public const string StreetBrushId = "street_brush";
        public const string SocialNewspaperId = "social_newspaper";
        public const string SocialMediaId = "social_media";
        public const string SocialMediaNoticesId = "social_media_notices";
        public const string FlareBusyId = "flare_busy";
        public const string FlareCityOfficeId = "flare_city_office";
        public const string FlareSharePostsId = "flare_share_posts";
        public const string FlareSharePicturesId = "flare_share_pictures";
        public const string FlareFilterId = "flare_filter";
        public const string FlareThemeId = "flare_theme";
        public const string FlarePicturesId = "flare_pictures";
        public const string FlareCityPhotosId = "flare_city_photos";
        public const string StandingId = "standing";
        public const string StandingStreetId = "standing_street";
        public const string StandingPressId = "standing_press";
        public const string StandingCorruptionId = "standing_corruption";
        public const string StandingRequestNotesId = "standing_request_notes";
        public const string RivalsId = "rivals";
        public const string RivalsStrengthId = "rivals_strength";
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
        public const string CareersOwnOfferNotesId = "careers_own_offer_notes";
        public const string CareersFamilyId = "careers_family";
        public const string CareersAgencyNoteId = "careers_agency_note";
        public const string CareersClassStartId = "careers_class_start";
        public const string CareersRejectReasonId = "careers_reject_reason";
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
        public const string InteriorsStandardStrengthId = "interiors_standard_strength";
        public const string InteriorsDecorStrengthId = "interiors_decor_strength";
        public const string InteriorsPricesStrengthId = "interiors_prices_strength";
        public const string InteriorsWearStrengthId = "interiors_wear_strength";
        public const string InteriorsBasketId = "interiors_basket";
        // The drop-downs the four sliders above replaced (0.8.2 to 0.9.10), read while a slider has no saved value yet so
        // that a choice made there is kept (InteriorRules' "Was" tables); the reward's had no slider of its own to go to.
        private const string StandardLevelWas = "interiors_standard_level", DecorLevelWas = "interiors_decor_level", PricesLevelWas = "interiors_prices_level", WearLevelWas = "interiors_wear_level";
        // The switches those drop-downs replaced (0.8.0): read when neither has a saved choice, so that a part switched
        // off stays off. They have no row of their own any more.
        private const string StandardSwitchWas = "interiors_standard", DecorSwitchWas = "interiors_decor", PricesSwitchWas = "interiors_prices", WearSwitchWas = "interiors_wear";
        // Set once the basket's and the decor's old defaults (off, and 5%: level 1) were moved to the new ones (MoveInteriorDefaults). No row of its own.
        private const string InteriorDefaultsMoved = "interiors_defaults_moved";
        private const int DecorChoiceWas = 1, DecorChoiceNow = 2;   // (the decor drop-down's 5% and 10%)
        public const string InteriorsStandingId = "interiors_standing";
        public const string InteriorsStaffId = "interiors_staff";
        public const string DesignsId = "designs";
        public const string PermadeathId = "permadeath";

        public static bool CameraEnabled = true;
        public static bool CameraIndoors = false;
        public static int CameraPitch = 18;      // degrees above the horizon
        public static int CameraDistance = 6;    // metres behind the player
        public static int CameraFollow = 5;      // 0 = never swing behind the player (only without mouse look)
        public static bool CityFaults = true;       // real faults in the city, rolled at random as the game runs: dark lamps, potholes, litter, crime hot spots, unsafe places; Flare's reports are checked against them (Scripts/Faults)
        public static int CityFaultsCount = 1;      // ...how many at once: 0 a few, 1 some, 2 many (Faults/FaultRules.Most)
        public static bool CityFaultsFalseCost = true;   // ...a report of nothing costs a little Flare reputation, and many stop the crews for a few days
        public static bool Texts = true;            // RLO Texts: a small text model on the player's own PC words the city's messages (Scripts/Texts); does nothing without a pack installed
        public static int TextsDevice = 0;          // ...where it runs: 0 Auto (the processor unless a GPU has ample free memory), 1 the processor, 2 the GPU
        public static int TextsThreads = 0;         // ...processor threads: 0 Auto (by the cores, Texts/TextSpeed), 1 two, 2 three, 3 four
        public static bool TextsReplies = true;     // ...Flare comments and replies
        public static bool TextsPosts = true;       // ...Flare posts of the city's people and businesses
        public static bool TextsPeople = true;      // ...messages from people: texts, Flare messages, greetings face to face
        public static bool TextsOfficial = true;    // ...the city's accounts and their answers
        public static bool TextsRivals = true;      // ...the rival tycoons
        public static bool TextsLedger = true;      // ...the City Ledger's briefs
        public static int TextsService = 0;         // ...Your own AI: the AI service the cards go to (Texts/TextProviders: 0 off, the default)
        public static bool TextsCity = true;        // ...with a pack of data 6: texts from the player's staff, from people after the player's own events, and gossip (Texts/TextsDirector.City)
        public static bool DistrictView = true;     // the district view on the city map: its rows in the map's filter list, the tint and the card (Districts/DistrictView)
        public static bool PhoneCamera = true;      // the Camera and Gallery apps on the phone: rear camera and selfie, photos kept per save (Scripts/Photos)
        public static bool PhoneCameraGrid = true;  // ...the viewfinder opens with the rule of thirds' grid showing
        public static bool MapPlacesShown = true;   // the mod's places on the city map: their markers and their rows in the map's filter list (Core/MapPlaces; off hides all of them)
        public static bool SpecialNames = true;     // the mod's special buildings carry their own names wherever the game names a building (Core/SpecialBuildings; off: the game's own names)
        public static bool MouseLook = true;     // GTA-style: mouse steers the camera, cursor locked
        public static int MouseSensitivity = 6;
        public static bool EnterWithKey = true;  // E/F near a door enters the building
        public static bool WalkToggle = true;    // on foot, the walk key walks and gives back the game's own pace (Walking/WalkDirector)
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
        public static bool CityDrivers = true;           // the city's cars slow in good time, leave room to park and let the player out (Traffic/Drivers)
        public static bool PhoneContacts = true;
        public static bool CarTuning = true;         // each car gets the acceleration and top speed of its kind (Driving/CarCatalogue)
        public static int CarTopSpeed = 100;         // percent of the car's own target top speed
        public static int CarAcceleration = 100;     // percent: at 150 a car reaches 100 km/h in two thirds of its own target time
        public static bool SmoothThrottle = true;    // the throttle key eases the pedal down instead of flooring it
        public static bool Speedometer = true;
        public static int Handling = 2;              // the driven car's weight, soft ride, roll and sliding (Driving/CarHandling): 0 off, 1 light, 2 full, 3 heavy
        public static int HandlingSoftness = 100;    // the test tools' tuning sliders, per cent of the design (Driving/HandlingRules): the springs' softness...
        public static int HandlingBounce = 100;      // ...how lightly the bounce is damped
        public static int HandlingRoll = 100;        // ...the body roll
        public static int HandlingGrip = 100;        // ...the tyres' grip
        public static int HandlingSteering = 100;    // ...the steering's reach at speed
        public static int HandlingBrakes = 100;      // ...how quickly the brakes build
        public static bool PhoneSkin = true;         // modern phone body, wallpaper and status bar
        public static bool PhoneFolders = true;      // the mod's apps in folders on the home screen, and a dock (GameUi/PhoneLayoutRules); off, the plain pages of before
        public static bool NoticeLook = true;        // the mod's notifications carry a picture of their kind on a disc of the mod's colour (Core/GameUi/NoticeLook); off: they look like the game's
        public static bool CarCamera = true;     // chase camera while driving
        public static int CarCameraDistance = 8; // metres behind the car
        public static int CameraHeight = 15;     // look-at height above the player's feet, in tenths of a metre
        public static bool VisualsEnabled = true;
        public static int Saturation = 12;
        public static int Contrast = 10;
        public static int Bloom = 0;             // percent; 0 leaves the game's bloom alone (bloom costs performance)
        public static int Detail = 100;          // LOD distance, percent of vanilla (above 100 costs performance)
        public static int ShadowReach = RenderLevelRules.Game;       // the sun's shadows, percent of the game's distance
        public static int ShadowCascades = RenderLevelRules.GameCascades;   // the sun's shadow cascades: 0 the game's own, else 1 to 4
        public static int LodReach = RenderLevelRules.Game;          // level-of-detail distances, percent of the game's
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
        public static bool FamilyWealthMood = true;      // the partner reacts to the player's wealth by how they lean, proud or envious (off: it is never mentioned)
        public static bool FamilyExpect = true;          // the partner expects a bigger home and more evenings out as the player gets richer (off: nothing is expected)
        public static bool FamilyWork = true;            // the partner asks to cut back to half days once the player is well off (off: always full days)
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
        public static bool Calendar = true;              // the Calendar app: the month as the chosen year divides it, with the days money moves and the player's own time (Scripts/Calendar)
        public static bool PlannerMorning = true;        // ...and one line of the day on waking: how many entries, the first and when to leave for it (off: none)
        public static bool Economy = true;               // the credit rating, the banks' terms by it, loans from the phone and the Finance app (Scripts/Economy)
        public static bool EconomyGradeUpNotes = true;   // ...a notification when the rating rises a grade (off: only a fall is told; the Finance app shows the rating either way)
        public static bool EconomyProducts = true;       // more kinds of credit: an overdraft, a card, a business loan, car finance, a mortgage, consolidation (off: nothing new is opened or drawn; what is owed stays owed)
        public static bool EconomyShark = true;          // the lender of last resort (off: it lends nothing and does nothing; a debt to it can still be paid)
        public static bool EconomyConsequences = true;   // what lenders do when payments are missed (off: only the rating is affected)
        public static int EconomyLevel = 1;              // how hard not paying hits: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore (Economy/Banking/Terms/ProductCatalogue)
        public static bool EconomyCity = true;           // the city's economy: a central bank, prices, a cycle, districts and supply (off: every figure is the game's own and the model stands still) (Scripts/Economy/City)
        public static bool EconomyRateAhead = true;      // the evening before a rate decision takes effect, one notice of what the market expects (off: none; the decision comes all the same)
        public static int EconomyCityLevel = 1;          // how far it swings: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore (Economy/City/MacroCatalogue)
        public static bool VenturesInvestors = true;     // investors: offers for a business that does well, the pitch, deals (off: no new offers or meetings; deals that exist carry on) (Scripts/Economy/Ventures)
        public static bool Markets = true;               // the city's stock market and the Stocks app (off: the market stands still and nothing is traded; shares held are kept) (Scripts/Markets)
        public static bool MarketsMargin = true;         // a margin account may be opened: borrowing against shares and selling short, with margin calls (off: no new account; one that is open can only be wound down)
        public static bool MarketsListing = true;        // the player's own company may come to the market (off: no new listing is begun; a company on the market stays there until taken private) (Scripts/Markets/Listing)
        public static bool MarketsBots = true;           // trading bots may be hired in the Stocks app (off: none is hired, and every bot hired is let go, its shares the player's own) (Scripts/Markets/MarketBots)
        public static bool MarketsDividendNotes = true;  // a notification when dividends are paid to the player or their bots, one for all that land at once (off: paid all the same, each still a line of the market news)
        public static bool MarketsBondSoFar = true;      // a bond's interest notice adds what it has paid so far (off: the payment alone)
        public static bool MarketsBonds = true;          // bonds and bank deposits may be bought in the Stocks app (off: none is bought; those held still pay, end and can be sold) (Scripts/Markets/Bonds)
        public static bool MarketsLarge = true;          // an order over one order's limit is worked a slice of each step at a time, and one bond purchase may be up to a share of what the player is worth (off: both limits as before; worked orders open are cancelled) (Scripts/Markets/InvestRules)
        public static bool MarketsReinvest = true;       // the share of each dividend a holding is set to put back is bought into the same company at the next open (off: every dividend is paid in cash) (Scripts/Markets/InvestRules)
        public static bool MarketsDerivatives = true;    // the exchange prices raw materials; futures and options on the Futures tab (off: prices as before, nothing new) (Markets/Derivatives)
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
        public static bool EconomyManagers = true;       // managers deciding inside the player's limits (CompanyDirector.Managers)
        public static bool Estate = true;                // property: a market that moves, letting a building yourself, works, selling and an estate agency, with the Property app (off: every value and rent is the game's own) (Scripts/Economy/Estate)
        public static bool EstateHotel = true;           // owning a hotel of the city's as a business (off: a hotel that is held stands still and nothing is taken or paid)
        public static int EstateRent = 100;              // rental income: percent of the market's rent of a unit the player lets himself, and so of what is asked, offered and renewed at (50 to 300) (Scripts/Economy/Estate/EstateRules.MarketRent)
        public static bool EstateHire = true;            // car hire: the player's own vehicles go out to customers from a lot (off: no car goes out, and one that is out is called back)
        public static bool TechPeople = true;            // professionals: roles, levels and skills of their own for the player's professional staff, reviews, promotions, courses, notice and severance, rivals' offers, in the Open Roles app (off: the game's own staff alone; files are kept) (Scripts/Tech/People)
        public static bool TechJobs = true;              // the jobs board and the labour market: ads in the Open Roles app, applicants from the city's job seekers, interviews, offers, executive search, and more candidates from the recruitment office (off: the game's own hiring alone) (Scripts/Tech/Hiring)
        public static bool TechSector = true;            // the tech sector: software studios, game studios, IT consultancies, cybersecurity firms and AI labs of the player's, products with customers and recurring revenue, client contracts and tenders, outages and breaches, in the Tech app (off: those businesses earn nothing of their own; their files are kept) (Scripts/Tech/Firms)
        public static bool TechOfferNotes = true;        // ...a notification for each client's contract offer and each council tender (off: they still come, on the Tech app's Contracts tab)
        public static bool TechSiteRooms = true;         // a data centre's server room furnished while the player is inside: rows of racks by its tier and the racks bought, cooling, power, trays, a raised floor, status lights, technicians; amber and dim in an outage (Scripts/Tech/Sites)
        public static bool TechSiteHq = true;            // the city's tech companies each given an empty office building as their headquarters: its name on the map, the GPS and the card, a furnished lobby and the rest of its floor fitted out (Scripts/FitOut), and the jobs there (Scripts/Tech/Sites)
        public static bool TechSiteCloud = true;         // the player's data centres signed outside with the cloud's name, with a security booth and a barrier where there is room, and on the map as "Your cloud" (Scripts/Tech/Sites)
        public static bool TechSiteOffices = true;       // a few tech fittings (a server closet, a developer's desk, a whiteboard) in an office a tech firm of the player's runs in, where there is room (Scripts/Tech/Sites)
        public static bool TechStartups = true;          // startups and venture capital: the city's startups that grow, raise, fail or are bought, their pitches to the player, an angel portfolio and a fund, and the player's own tech firms raising from venture firms with a board and share options (off: no startup is founded or pitches; holdings are kept) (Scripts/Tech/Venture)
        public static bool EconomyOnline = true;         // online shops: a business or a warehouse sells what it stocks to the whole city, picked by its staff and delivered by van or courier, with the Web shop app (off: no orders come, nothing is charged or paid) (Scripts/Economy/Online)
        public static bool EconomyOnlineWaitNotes = true; // ...a notification after three opening hours of orders nobody is on shift to pick (off: the Planner still says so)
        public static bool EconomyDealer = true;         // the car dealership: a business type of the mod's own that buys cars new, used and at auction and sells them to customers, with its own finance and warranties and the Dealership app (off: its dealerships do not trade; their cars stay on the lots) (Scripts/Economy/Dealer)
        public static bool EconomyDealerFloor = true;    // visitors on the Car Showroom's floor: the hour's customers seen walking round the showroom the player stands in, looking at the cars, a salesperson going to some (off: the floor is empty; the hour is worked as before) (Scripts/Economy/Dealer/Floor)
        public static bool EconomyCleaning = true;       // the cleaning service: cleaners shared between businesses on a daily round, and cleaning contracts with other businesses, in the Services app (off: no round, no contracts, nothing cleaned or charged by it) (Scripts/Economy/Services)
        public static bool OfficeConversion = true;      // some empty shops in the districts with offices are offices to rent while the mod runs: on the map, in BizMan, at an office's rent, for any office business; never written into the save (Scripts/Offices)
        public static bool EconomySale = true;           // selling a business: its value, the market, offers and a completed sale, in the Services app (off: nothing is offered, paid or charged by it) (Scripts/Economy/Services)
        public static bool EconomyBank = true;           // the player's own bank: a licence, savers, loans, a fund and a regulator, in its own app (off: it stands still, nothing is run, booked or charged) (Scripts/Economy/OwnBank)
        // bank branches: a business type whose customers bank with the player's own bank and bring it more of the city;
        // the player may work the counter (off: branches do nothing, the bank is as without them) (Economy/OwnBank/Branch)
        public static bool EconomyBranch = true;
        public static bool EndScale = true;              // the end game at scale: data centres, the cloud and the hyperscaler, the broadband company and the player's own bank cost and earn at the size of the real thing, with upkeep, crews of staff by kind and income that grows with what is built; a save that already held one is brought to it over four game weeks (off: every figure as before) (Scripts/Core/EndScale)
        public static bool EconomyBrands = true;         // brands and franchises: a brand made from a shop, its recognition and standards, franchising it, and a shop under a city chain, with the Brands app (off: none of it runs, nothing is paid and footfall is without it) (Scripts/Economy/Brands)
        public static bool EconomyBanks = true;          // the Banks app: the city's three largest banks in one app on the phone, each bank's slice of the accounts, loans, card, offers and rating (off: no Banks app; every account and loan stays as it is, and the Finance app still has everything) (Scripts/Economy/Banks)
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
        public static bool GarageSounds = true;          // recorded engine, exhaust, turbo and tyre sounds on the car the player drives (Driving/Sound)
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
        public static bool HomeMorningNews = true;       // ...with the city's news held while the player slept in it, one notification or few (Core/MorningNews; off: each notice comes on its own)
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
        public static bool StreetProtestNotes = true;    // the notifications of a protest: planned, begun, over (off: it still happens and closes its street; the City Bulletin still lists it)
        public static bool Social = true;                // the people in the player's life: Mom, Alex and whoever is met; their contacts and the People page
        public static bool SocialMeetings = true;        // new people walk up to the player (after the gym, a shift, a coffee, a good turn)
        public static bool SocialRelationships = true;   // relationships wear down with neglect and can be damaged; off: nothing is ever lost
        public static bool SocialOutings = true;         // plans can be made and kept: coffee, a meal, drinks, the cinema, a walk, a drive, a workout, the shop
        public static bool SocialContact = true;         // they get in touch themselves: invitations, favours, news, a visit at the shop
        public static bool SocialTexts = true;           // text messages from people (off: only what must be said is shown, as a notification)
        public static int SocialTextsMax = 3;            // texts a day from everybody together, 1 to 6
        public static bool SocialComposer = true;        // with an RLO Texts pack, a call's chat is answered in the player's own typed words (off: the two answers)
        public static bool StreetTalk = true;            // talk to anybody in the street: the game's panel over a passer-by offers Talk (Scripts/Street)
        public static int StreetBrush = 1;               // how often people in the street have no time: 0 rarely, 1 sometimes, 2 often (StreetRules.BrushFactor)
        public static bool SocialMedia = true;           // Flare, the city's social media app: an account, posts about what really happened, friends who follow (Scripts/Social/Flare)
        public static bool SocialMediaNotices = true;    // Flare's few notifications (off: none at all; the app's badge still counts)
        public static int FlareBusy = 1;                 // how busy the city's feed is: 0 quiet, 1 normal, 2 busy (Social/Flare/FlareLiveRules)
        public static bool FlareCityOffice = true;       // the City Office acts on reports (works, patrols, cleaning, inspections); off: it answers only
        public static bool FlareSharePosts = true;       // in a Going Public session the player's posts go to the session's shared feed
        public static bool FlareSharePictures = true;    // ...and their pictures, as small copies
        public static int FlareFilter = 0;               // what other players type: 0 standard (Steam's filter, else the mod's list), 1 strict (both, and addresses starred)
        public static int FlareTheme = 0;                // Flare's look: 0 follows the phone (dark from 19:00 to 07:00), 1 light, 2 dark
        public static bool FlarePictures = true;         // every account shows its picture: faces from the game's portrait camera, the game's logos, emblems (Social/Flare/FlareAvatars); off: the initials
        public static int FlareCityPhotos = 2;           // how many of the city's posts carry a photo from the bank of city photos: 0 none, 1 a few, 2 some (about three in ten of the city's posts, over half of those with something to see), 3 many (Social/Flare/FlareCityPhotoRules)
        public static bool Standing = true;              // Standing: the city knows the player's wealth, name and reputation, and reacts (Scripts/Fame)
        public static bool StandingStreet = true;        // ...people in the street and customers in the player's shops know them
        public static bool StandingPress = true;         // ...reporters, the night show, magazines and the papers' pieces about the player
        public static bool Rivals = true;                // rival tycoons: two or three named fortunes with holdings on the stock market who grow with the player and move against them in the late game (off: they stand still and do nothing; the save keeps them) (Scripts/Rivals)
        public static int RivalsStrength = 1;            // how hard they push: 0 Gentle, 1 Normal, 2 Hard, 3 Ruthless (Rivals/RivalCatalogue)
        public static bool StandingCorruption = true;    // ...officials who offer to speed things up for a bribe, and what comes of it
        public static bool StandingRequestNotes = true;  // ...a notification when the city asks something of the player (off: the requests still come, on the Standing page)
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
        public static bool CareersClassStart = true;     // "your class starts now" at a class's hour while the player is not inside the place that holds it, once a class (off: the older notice of a class begun elsewhere)
        public static bool CareersRejectReason = true;   // an employer's no says how many points of score the application was short, or that it was close (off: the no alone)
        public static bool CareersAgencyNote = true;     // a notification on the last day of a registration with the recruitment office (off: none; the registration ends all the same)
        public static bool CareersOwn = true;            // working for oneself: leaving a profession to found a company or open a practice, former colleagues, restraint clauses and claims, freelance contracts and side work (off: no contracts come; a practice is kept) (Scripts/Careers)
        public static bool CareersOwnOfferNotes = true;  // ...a notification for each contract a client offers (off: the offers still come, on the Career app's Own work tab)
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
        public static int InteriorsStandardStrength = 100;  // (InteriorRules.StrengthDefault) per cent, 0 off to 200 double: each district's standard, what falling short of it costs (customers, complaints: 6% at most at 100) and being over it brings (with Meaningful Interiors on: nothing) (Scripts/Interiors)
        public static int InteriorsDecorStrength = 100;     // per cent, 0 off to 200 double: the customers decor brings (10% at a decor score of 100 at 100), and an office's clients (with Meaningful Ambience on: nothing) (Scripts/Interiors)
        public static int InteriorsPricesStrength = 100;    // per cent, 0 off to 200 double: what a nicer place lets a shop charge: the price the district bears over the standard (3% at most at 100) and the basket's share (Scripts/Interiors)
        public static int InteriorsWearStrength = 100;      // per cent, 0 off to 200 double: how fast finishes wear and get dirty (Scripts/Interiors)
        public static bool InteriorsBasket = true;          // over the standard each customer takes more products the shop has in stock, by the size of their order (it adds to the game's own list of the hour's customers) (with Meaningful Interiors on: nothing) (Scripts/Interiors)
        public static bool InteriorsStanding = true;        // a well kept place lifts its brand's standards and Standing's customers, and makes the news; a shabby one the other way (Scripts/Interiors)
        public static bool Designs = true;                  // the interior installation firms offer the mod's business types ready-made designs (Scripts/Designs)
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
            CityFaults = Bool(modId, CityFaultsId, CityFaults);
            CityFaultsCount = Int(modId, CityFaultsCountId, CityFaultsCount);
            CityFaultsFalseCost = Bool(modId, CityFaultsFalseCostId, CityFaultsFalseCost);
            Texts = Bool(modId, TextsId, Texts);
            TextsDevice = Int(modId, TextsDeviceId, TextsDevice);
            TextsThreads = Int(modId, TextsThreadsId, TextsThreads);
            TextsReplies = Bool(modId, TextsRepliesId, TextsReplies);
            TextsPosts = Bool(modId, TextsPostsId, TextsPosts);
            TextsPeople = Bool(modId, TextsPeopleId, TextsPeople);
            TextsOfficial = Bool(modId, TextsOfficialId, TextsOfficial);
            TextsRivals = Bool(modId, TextsRivalsId, TextsRivals);
            TextsLedger = Bool(modId, TextsLedgerId, TextsLedger);
            TextsService = Int(modId, TextsServiceId, TextsService);
            TextsCity = Bool(modId, TextsCityId, TextsCity);
            PhoneCamera = Bool(modId, PhoneCameraId, PhoneCamera);
            PhoneCameraGrid = Bool(modId, PhoneCameraGridId, PhoneCameraGrid);
            MouseLook = Bool(modId, MouseLookId, MouseLook);
            MouseSensitivity = Int(modId, MouseSensitivityId, MouseSensitivity);
            EnterWithKey = Bool(modId, EnterWithKeyId, EnterWithKey);
            WalkToggle = Bool(modId, WalkToggleId, WalkToggle);
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
            CityDrivers = Bool(modId, CityDriversId, CityDrivers);
            PhoneContacts = Bool(modId, PhoneContactsId, PhoneContacts);
            PhoneSkin = Bool(modId, PhoneSkinId, PhoneSkin);
            PhoneFolders = Bool(modId, PhoneFoldersId, PhoneFolders);
            NoticeLook = Bool(modId, NoticeLookId, NoticeLook);
            CarTuning = Bool(modId, CarTuningId, CarTuning);
            CarTopSpeed = Int(modId, CarTopSpeedId, CarTopSpeed);
            CarAcceleration = Int(modId, CarAccelerationId, CarAcceleration);
            SmoothThrottle = Bool(modId, SmoothThrottleId, SmoothThrottle);
            // The old slider was a percentage of the game's own car; the new two are percentages of each car's own
            // targets, so its saved value means nothing to them. It is dropped and the new sliders start at 100.
            UnityEngine.PlayerPrefs.DeleteKey(Key(modId, CarSpeedId));
            Keys.Load(modId);
            Speedometer = Bool(modId, SpeedometerId, Speedometer);
            Handling = Int(modId, HandlingId, Handling);
            HandlingSoftness = Int(modId, HandlingSoftnessId, HandlingSoftness);
            HandlingBounce = Int(modId, HandlingBounceId, HandlingBounce);
            HandlingRoll = Int(modId, HandlingRollId, HandlingRoll);
            HandlingGrip = Int(modId, HandlingGripId, HandlingGrip);
            HandlingSteering = Int(modId, HandlingSteeringId, HandlingSteering);
            HandlingBrakes = Int(modId, HandlingBrakesId, HandlingBrakes);
            VisualsEnabled = Bool(modId, VisualsEnabledId, VisualsEnabled);
            Saturation = Int(modId, SaturationId, Saturation);
            Contrast = Int(modId, ContrastId, Contrast);
            Bloom = Int(modId, BloomId, Bloom);
            Detail = Int(modId, DetailId, Detail);
            ShadowReach = RenderLevelRules.Percent(Int(modId, ShadowReachId, ShadowReach));
            ShadowCascades = RenderLevelRules.Cascades(Int(modId, ShadowCascadesId, ShadowCascades));
            LodReach = RenderLevelRules.Percent(Int(modId, LodReachId, LodReach));
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
            FamilyWork = Bool(modId, FamilyWorkId, FamilyWork);
            FamilyWealthMood = Bool(modId, FamilyWealthMoodId, FamilyWealthMood);
            FamilyExpect = Bool(modId, FamilyExpectId, FamilyExpect);
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
            PlannerMorning = Bool(modId, PlannerMorningId, PlannerMorning);
            Calendar = Bool(modId, CalendarId, Calendar);
            Economy = Bool(modId, EconomyId, Economy);
            EconomyGradeUpNotes = Bool(modId, EconomyGradeUpNotesId, EconomyGradeUpNotes);
            EconomyProducts = Bool(modId, EconomyProductsId, EconomyProducts);
            EconomyShark = Bool(modId, EconomySharkId, EconomyShark);
            EconomyConsequences = Bool(modId, EconomyConsequencesId, EconomyConsequences);
            EconomyLevel = Int(modId, EconomyLevelId, EconomyLevel);
            EconomyCity = Bool(modId, EconomyCityId, EconomyCity);
            EconomyCityLevel = Int(modId, EconomyCityLevelId, EconomyCityLevel);
            EconomyRateAhead = Bool(modId, EconomyRateAheadId, EconomyRateAhead);
            VenturesInvestors = Bool(modId, VenturesInvestorsId, VenturesInvestors);
            Markets = Bool(modId, MarketsId, Markets);
            MarketsMargin = Bool(modId, MarketsMarginId, MarketsMargin);
            MarketsListing = Bool(modId, MarketsListingId, MarketsListing);
            MarketsBots = Bool(modId, MarketsBotsId, MarketsBots);
            MarketsDividendNotes = Bool(modId, MarketsDividendNotesId, MarketsDividendNotes);
            MarketsBonds = Bool(modId, MarketsBondsId, MarketsBonds);
            MarketsBondSoFar = Bool(modId, MarketsBondSoFarId, MarketsBondSoFar);
            MarketsLarge = Bool(modId, MarketsLargeId, MarketsLarge);
            MarketsReinvest = Bool(modId, MarketsReinvestId, MarketsReinvest);
            MarketsDerivatives = Bool(modId, MarketsDerivativesId, MarketsDerivatives);
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
            EconomyManagers = Bool(modId, EconomyManagersId, EconomyManagers);
            Estate = Bool(modId, EstateId, Estate);
            EstateHotel = Bool(modId, EstateHotelId, EstateHotel);
            EstateHire = Bool(modId, EstateHireId, EstateHire);
            EstateRent = Int(modId, EstateRentId, EstateRent);
            EconomyOnline = Bool(modId, EconomyOnlineId, EconomyOnline);
            EconomyOnlineWaitNotes = Bool(modId, EconomyOnlineWaitNotesId, EconomyOnlineWaitNotes);
            TechPeople = Bool(modId, TechPeopleId, TechPeople);
            TechJobs = Bool(modId, TechJobsId, TechJobs);
            TechSector = Bool(modId, TechSectorId, TechSector);
            TechOfferNotes = Bool(modId, TechOfferNotesId, TechOfferNotes);
            TechStartups = Bool(modId, TechStartupsId, TechStartups);
            TechSiteRooms = Bool(modId, TechSiteRoomsId, TechSiteRooms);
            TechSiteHq = Bool(modId, TechSiteHqId, TechSiteHq);
            TechSiteCloud = Bool(modId, TechSiteCloudId, TechSiteCloud);
            TechSiteOffices = Bool(modId, TechSiteOfficesId, TechSiteOffices);
            EconomyDealer = Bool(modId, EconomyDealerId, EconomyDealer);
            EconomyDealerFloor = Bool(modId, EconomyDealerFloorId, EconomyDealerFloor);
            EconomyCleaning = Bool(modId, EconomyCleaningId, EconomyCleaning);
            EconomySale = Bool(modId, EconomySaleId, EconomySale);
            OfficeConversion = Bool(modId, OfficeConversionId, OfficeConversion);
            EconomyBank = Bool(modId, EconomyBankId, EconomyBank);
            EconomyBranch = Bool(modId, EconomyBranchId, EconomyBranch);
            EndScale = Bool(modId, EndScaleId, EndScale);
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
            GarageSounds = Bool(modId, GarageSoundsId, GarageSounds);
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
            HomeMorningNews = Bool(modId, HomeMorningNewsId, HomeMorningNews);
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
            StreetProtestNotes = Bool(modId, StreetProtestNotesId, StreetProtestNotes);
            Social = Bool(modId, SocialId, Social);
            SocialMeetings = Bool(modId, SocialMeetingsId, SocialMeetings);
            SocialRelationships = Bool(modId, SocialRelationshipsId, SocialRelationships);
            SocialOutings = Bool(modId, SocialOutingsId, SocialOutings);
            SocialContact = Bool(modId, SocialContactId, SocialContact);
            SocialTexts = Bool(modId, SocialTextsId, SocialTexts);
            SocialTextsMax = Int(modId, SocialTextsMaxId, SocialTextsMax);
            SocialComposer = Bool(modId, SocialComposerId, SocialComposer);
            StreetTalk = Bool(modId, StreetTalkId, StreetTalk);
            StreetBrush = Int(modId, StreetBrushId, StreetBrush);
            SocialNewspaper = Bool(modId, SocialNewspaperId, SocialNewspaper);
            SocialMedia = Bool(modId, SocialMediaId, SocialMedia);
            SocialMediaNotices = Bool(modId, SocialMediaNoticesId, SocialMediaNotices);
            FlareBusy = Int(modId, FlareBusyId, FlareBusy);
            FlareCityOffice = Bool(modId, FlareCityOfficeId, FlareCityOffice);
            FlareSharePosts = Bool(modId, FlareSharePostsId, FlareSharePosts);
            FlareSharePictures = Bool(modId, FlareSharePicturesId, FlareSharePictures);
            FlareFilter = Int(modId, FlareFilterId, FlareFilter);
            FlareTheme = Int(modId, FlareThemeId, FlareTheme);
            FlarePictures = Bool(modId, FlarePicturesId, FlarePictures);
            FlareCityPhotos = Int(modId, FlareCityPhotosId, FlareCityPhotos);
            Standing = Bool(modId, StandingId, Standing);
            StandingStreet = Bool(modId, StandingStreetId, StandingStreet);
            StandingPress = Bool(modId, StandingPressId, StandingPress);
            StandingCorruption = Bool(modId, StandingCorruptionId, StandingCorruption);
            StandingRequestNotes = Bool(modId, StandingRequestNotesId, StandingRequestNotes);
            Rivals = Bool(modId, RivalsId, Rivals);
            RivalsStrength = Int(modId, RivalsStrengthId, RivalsStrength);
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
            CareersOwnOfferNotes = Bool(modId, CareersOwnOfferNotesId, CareersOwnOfferNotes);
            CareersFamily = Bool(modId, CareersFamilyId, CareersFamily);
            CareersAgencyNote = Bool(modId, CareersAgencyNoteId, CareersAgencyNote);
            CareersClassStart = Bool(modId, CareersClassStartId, CareersClassStart);
            CareersRejectReason = Bool(modId, CareersRejectReasonId, CareersRejectReason);
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
            MoveInteriorDefaults(modId);
            InteriorsStandardStrength = Strength(modId, InteriorsStandardStrengthId, StandardLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.StandardWas, StandardSwitchWas, InteriorsStandardStrength);
            InteriorsDecorStrength = Strength(modId, InteriorsDecorStrengthId, DecorLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.DecorWas, DecorSwitchWas, InteriorsDecorStrength);
            InteriorsPricesStrength = Strength(modId, InteriorsPricesStrengthId, PricesLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.PricesWas, PricesSwitchWas, InteriorsPricesStrength);
            InteriorsWearStrength = Strength(modId, InteriorsWearStrengthId, WearLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.WearWas, WearSwitchWas, InteriorsWearStrength);
            InteriorsBasket = Bool(modId, InteriorsBasketId, InteriorsBasket);
            InteriorsStanding = Bool(modId, InteriorsStandingId, InteriorsStanding);
            InteriorsStaff = Bool(modId, InteriorsStaffId, InteriorsStaff);
            Designs = Bool(modId, DesignsId, Designs);
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

        /// <summary>
        /// Once only (the marker below): the basket came on by default and the customers decor brings went from 5% to 10%,
        /// as the other authors' Meaningful Interiors and Meaningful Ambience have them. A saved basket still at its old
        /// default (off) is switched on, and a saved decor choice still at its old default (5%) moves to 10%. Any other
        /// value stays as it is, and whatever is chosen afterwards is never touched again.
        /// </summary>
        private static void MoveInteriorDefaults(string modId)
        {
            string done = Key(modId, InteriorDefaultsMoved);
            if (UnityEngine.PlayerPrefs.GetInt(done, 0) != 0)
            {
                return;
            }
            string basket = Key(modId, InteriorsBasketId), decor = Key(modId, DecorLevelWas);
            if (UnityEngine.PlayerPrefs.HasKey(basket) && UnityEngine.PlayerPrefs.GetInt(basket, 0) == 0)
            {
                UnityEngine.PlayerPrefs.SetInt(basket, 1);
            }
            if (UnityEngine.PlayerPrefs.HasKey(decor) && UnityEngine.PlayerPrefs.GetInt(decor, 0) == DecorChoiceWas)
            {
                UnityEngine.PlayerPrefs.SetInt(decor, DecorChoiceNow);
            }
            UnityEngine.PlayerPrefs.SetInt(done, 1);
        }

        /// <summary>
        /// A strength slider of the interiors (per cent, 0 to 200): its saved value; while it has none, the drop-down it
        /// replaced as its slider figure (a choice made there is kept); while neither has one, off if the switch before
        /// them was switched off, and otherwise the default. Nothing is written: the old rows stay as they were.
        /// </summary>
        private static int Strength(string modId, string id, string levelWas, int[] was, string switchWas, int fallback)
        {
            string key = Key(modId, id), old = Key(modId, levelWas);
            if (UnityEngine.PlayerPrefs.HasKey(key))
            {
                return global::RealLifeOverhaul.Interiors.InteriorRules.Clamp(UnityEngine.PlayerPrefs.GetInt(key, fallback), global::RealLifeOverhaul.Interiors.InteriorRules.StrengthLeast, global::RealLifeOverhaul.Interiors.InteriorRules.StrengthMost);
            }
            if (UnityEngine.PlayerPrefs.HasKey(old))
            {
                return global::RealLifeOverhaul.Interiors.InteriorRules.Was(was, UnityEngine.PlayerPrefs.GetInt(old, 0));
            }
            return Bool(modId, switchWas, true) ? fallback : 0;
        }

        // Same key format the game uses when it saves mod options.
        private static string Key(string modId, string id) => "m:" + modId + ":" + id;

        private static bool Bool(string modId, string id, bool fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback ? 1 : 0) != 0;

        private static int Int(string modId, string id, int fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback);
    }
}
