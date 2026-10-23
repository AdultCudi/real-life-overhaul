namespace RealLifeOverhaul
{
    /// <summary>
    /// Live option values. The game fires option callbacks only once the options screen has been opened, so saved
    /// values are read from PlayerPrefs at load.
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
        public const string MealsSeatedId = "meals_seated";
        public const string MealsCookedId = "meals_cooked";
        public const string MealsDanceId = "meals_dance";
        public const string NewBuildingsId = "new_buildings";
        public const string TradesId = "build_trades";              // the building trades (Scripts/BuildTrades)
        public const string TradesOfficeId = "build_trades_office";   // ...and their architects' practice
        public const string ConstructionId = "construction";                      // the construction game (Scripts/Construction)
        public const string ConstructionDevelopersId = "construction_developers";  // ...the city's developers and the tenders
        public const string ConstructionLandTaxId = "construction_land_tax";       // ...land tax on the lots owned
        public const string ConstructionRisksId = "construction_risks";            // ...theft, storms and faults on the sites
        public const string ConstructionShowFlatsId = "construction_show_flats";   // ...show flats for a block's off plan buyers
        public const string ConstructionRenovationsId = "construction_renovations"; // ...refits of the player's own businesses, and businesses' refits to tender
        public const string ConstructionCityId = "construction_city";              // ...the city around it: rigged tenders, unpaid bills, the building of the year
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
        public const string MarketsBuildersId = "markets_builders";              // the city's builders on the market, building materials and a project's hedge
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
        public const string DevDiagnosticsId = "dev_diagnostics";      // Meta: the diagnostic keys (F7, Shift+F7, F9) and the Info lines in the log
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
        // The drop-downs the four sliders above replaced, read while a slider has no saved value, so a choice made there
        // is kept (InteriorRules' "Was" tables).
        private const string StandardLevelWas = "interiors_standard_level", DecorLevelWas = "interiors_decor_level", PricesLevelWas = "interiors_prices_level", WearLevelWas = "interiors_wear_level";
        // The switches those drop-downs replaced: read when neither has a saved choice, so a part switched off stays off.
        private const string StandardSwitchWas = "interiors_standard", DecorSwitchWas = "interiors_decor", PricesSwitchWas = "interiors_prices", WearSwitchWas = "interiors_wear";
        // Set once the basket's and the decor's old defaults were moved to the new ones (MoveInteriorDefaults).
        private const string InteriorDefaultsMoved = "interiors_defaults_moved";
        private const int DecorChoiceWas = 1, DecorChoiceNow = 2;   // the decor drop-down's 5% and 10%
        public const string InteriorsStandingId = "interiors_standing";
        public const string InteriorsStaffId = "interiors_staff";
        public const string DesignsId = "designs";
        public const string PermadeathId = "permadeath";
        public const string NewGroundTestRoadId = "newground_test_road";   // NewGround: a developer's test road, read at each load
        public const string UptownId = "uptown";   // Uptown, north of Manhattan: its ground laid at each load

        public static bool CameraEnabled = true;
        public static bool CameraIndoors = false;
        public static int CameraPitch = 18;      // degrees above the horizon
        public static int CameraDistance = 6;    // metres behind the player
        public static int CameraFollow = 5;      // 0 = never swing behind the player (only without mouse look)
        public static bool CityFaults = true;       // real faults in the city, rolled at random as the game runs
        public static int CityFaultsCount = 1;      // ...how many at once: 0 a few, 1 some, 2 many
        public static bool CityFaultsFalseCost = true;   // ...a report of nothing costs a little Flare reputation, and many stop the crews for a few days
        public static bool Texts = true;            // RLO Texts
        public static int TextsDevice = 0;          // ...where it runs: 0 Auto (the processor unless a GPU has ample free memory), 1 the processor, 2 the GPU
        public static int TextsThreads = 0;         // ...processor threads: 0 Auto (by the cores, Texts/TextSpeed), 1 two, 2 three, 3 four
        public static bool TextsReplies = true;     // ...Flare comments and replies
        public static bool TextsPosts = true;       // ...Flare posts of the city's people and businesses
        public static bool TextsPeople = true;      // ...messages from people: texts, Flare messages, greetings face to face
        public static bool TextsOfficial = true;    // ...the city's accounts and their answers
        public static bool TextsRivals = true;      // ...the rival tycoons
        public static bool TextsLedger = true;      // ...the City Ledger's briefs
        public static int TextsService = 0;         // ...Your own AI: the AI service the cards go to
        public static bool TextsCity = true;        // ...people's texts after the player's news (a pack or not); with a pack of data 6, the staff's and gossip
        public static bool DistrictView = true;     // the district view on the city map: its rows in the map's filter list, the tint and the card
        public static bool PhoneCamera = true;      // the Camera and Gallery apps on the phone: rear camera and selfie, photos kept per save
        public static bool PhoneCameraGrid = true;  // ...the viewfinder opens with the rule of thirds' grid showing
        public static bool MapPlacesShown = true;   // the mod's places on the city map: their markers and their rows in the map's filter list
        public static bool SpecialNames = true;     // the mod's special buildings carry their own names wherever the game names a building
        public static bool MouseLook = true;     // GTA-style: mouse steers the camera, cursor locked
        public static int MouseSensitivity = 6;
        public static bool EnterWithKey = true;  // E/F near a door enters the building
        public static bool WalkToggle = true;    // on foot, the walk key walks and gives back the game's own pace
        public static bool MealsSeated = true;   // a meal is eaten at a free seat nearby, with the food in front
        public static bool MealsCooked = true;   // a meal cooked at home is carried to a seat and eaten there
        public static bool MealsDance = true;    // "Dance" in the radio panel over a loudspeaker playing music
        public static bool NewBuildings = true;  // the mod's own buildings, made at each load from the city's own designs
        public static bool Trades = true, TradesOffice = true;   // the building trades at the works sites, and the architects' practice on a floor
        public static bool Construction = true;            // a construction company of the player's: land, projects, tenders and the rival builders
        public static bool ConstructionDevelopers = true;  // the city's developers build on the district's lots, and works are put out to tender
        public static bool ConstructionLandTax = true;     // the lots the player owns pay land tax, and more kept empty
        public static bool ConstructionRisks = true;       // theft, storm damage and faults coming to light on the company's works
        public static bool ConstructionShowFlats = true;   // a show flat dressed in one of the show flats' designs for a block sold off plan, with its viewings
        public static bool ConstructionRenovations = true; // the player's businesses refitted by their company or a contractor, and businesses' refits out to tender
        public static bool ConstructionCity = true;        // rigged tenders, unpaid bills taken to court, and the City Ledger's building of the year
        public static int CameraFov = 55;        // degrees, vertical
        public static bool LifeEvents = true;
        public static int EventFrequency = 5;        // about this many random events per game week
        public static bool CrashConsequences = true;
        public static bool TrafficAccidents = true;
        public static bool AccidentNotices = true;   // the notifications (and their sound) as an accident of other people's unfolds
        public static bool AccidentMarker = true;    // the game's destination marker on an accident, and so the route to it
        public static bool TowTrucks = true;         // a tow truck takes a wreck away once its scene has cleared; off, the wreck simply goes
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
        public static bool HornReactions = true;         // people hurry, glare or look round when the player sounds the horn
        public static bool NaturalAccidents = true;      // accidents come from reckless drivers on the road, in view
        public static bool SolidStreets = true;          // traffic brakes for people in the road, sounds its horn and waits
        public static bool CityDrivers = true;           // the city's cars slow in good time, leave room to park and let the player out
        public static bool PhoneContacts = true;
        public static bool CarTuning = true;         // each car gets the acceleration and top speed of its kind
        public static int CarTopSpeed = 100;         // percent of the car's own target top speed
        public static int CarAcceleration = 100;     // percent: at 150 a car reaches 100 km/h in two thirds of its own target time
        public static bool SmoothThrottle = true;    // the throttle key eases the pedal down instead of flooring it
        public static bool Speedometer = true;
        public static int Handling = 2;              // the driven car's weight, soft ride, roll and sliding: 0 off, 1 light, 2 full, 3 heavy
        public static int HandlingSoftness = 100;    // the test tools' tuning sliders, per cent of the design: the springs' softness...
        public static int HandlingBounce = 100;      // ...how lightly the bounce is damped
        public static int HandlingRoll = 100;        // ...the body roll
        public static int HandlingGrip = 100;        // ...the tyres' grip
        public static int HandlingSteering = 100;    // ...the steering's reach at speed
        public static int HandlingBrakes = 100;      // ...how quickly the brakes build
        public static bool PhoneSkin = true;         // modern phone body, wallpaper and status bar
        public static bool PhoneFolders = true;      // the mod's apps in folders on the home screen, and a dock; off, the plain pages of before
        public static bool NoticeLook = true;        // the mod's notifications carry a picture of their kind on a disc of the mod's colour
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
        public static bool CharacterSkin = false;        // skin finish on the player (off: a small change, close up only)
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
        public static bool Family = true;                // a partner who moves in, engagement and wedding, children, relatives
        public static bool Utilities = true;             // power and water bills, the utilities office and the Utilities app
        public static bool FamilyAtHome = true;          // the partner is shown as a character in the home
        public static bool FamilyWedding = true;         // rings, the proposal, planning and holding a wedding
        public static bool FamilyChildren = true;        // deciding to have children, pregnancy and adoption, growing up
        public static bool FamilyRelatives = true;       // Mom and the in-laws react, visit, invite to holiday dinners
        public static bool FamilyFriction = true;        // chores, arguments, separation and divorce (off: the home only ever gets warmer)
        public static bool FamilyWealthMood = true;      // the partner reacts to the player's wealth by how they lean, proud or envious
        public static bool FamilyExpect = true;          // the partner expects a bigger home and more evenings out as the player gets richer
        public static bool FamilyWork = true;            // the partner asks to cut back to half days once the player is well off
        public static int FamilyInterest = 0;            // who the default "Someone special" introduces: 0 anyone, 1 women, 2 men
        public static int FamilyYearWeeks = 4;           // in-game weeks per year of a child's age
        public static bool Civic = true;                 // civic life: a city government and a construction programme
        public static bool CivicElections = true;        // elections, the mayor's policies and their effects, the campaign in the street
        public static bool CivicConstruction = true;     // construction sites that progress over days
        public static bool CivicOffice = true;           // the player may stand for the council or for mayor
        public static bool DrivingLicence = true;        // driving needs a licence for the class of vehicle; unlicensed driving is punished when police stop the player
        public static bool StartWithLicence = false;     // a save that owns no vehicle the first time the rule runs is given a car licence anyway
        public static bool DrivingSchool = true;         // the desks at the school buildings, the written test, lessons and the driven test
        public static bool LicencePoints = true;         // booked offences put penalty points on the licence; twelve suspend it
        public static bool Planner = true;               // the Planner app on the phone: everything to do in one agenda
        public static bool PlannerReminders = true;      // ...and its reminders, sent as the game's own notifications
        public static bool Calendar = true;              // the Calendar app
        public static bool PlannerMorning = true;        // ...and one line of the day on waking (off: none)
        public static bool Economy = true;               // the credit rating, the banks' terms by it, loans from the phone and the Finance app
        public static bool EconomyGradeUpNotes = true;   // ...a notification when the rating rises a grade
        public static bool EconomyProducts = true;       // more kinds of credit (off: nothing new is opened or drawn; what is owed stays owed)
        public static bool EconomyShark = true;          // the lender of last resort
        public static bool EconomyConsequences = true;   // what lenders do when payments are missed (off: only the rating is affected)
        public static int EconomyLevel = 1;              // how hard not paying hits: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore
        public static bool EconomyCity = true;           // the city's economy (off: every figure is the game's own and the model stands still)
        public static bool EconomyRateAhead = true;      // the evening before a rate decision takes effect, one notice of what the market expects
        public static int EconomyCityLevel = 1;          // how far it swings: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore
        public static bool VenturesInvestors = true;     // investors (off: no new offers or meetings; deals that exist carry on)
        public static bool Markets = true;               // the city's stock market and the Stocks app
        public static bool MarketsMargin = true;         // a margin account may be opened
        public static bool MarketsListing = true;        // the player's own company may come to the market
        public static bool MarketsBots = true;           // trading bots may be hired in the Stocks app
        public static bool MarketsDividendNotes = true;  // a notification when dividends are paid to the player or their bots, one for all that land at once
        public static bool MarketsBondSoFar = true;      // a bond's interest notice adds what it has paid so far (off: the payment alone)
        public static bool MarketsBonds = true;          // bonds and bank deposits may be bought in the Stocks app
        public static bool MarketsLarge = true;          // a large order is worked a slice a step, and a bond purchase may be a share of what the player is worth
        public static bool MarketsReinvest = true;       // the share of each dividend a holding is set to put back is bought into the same company at the next open
        public static bool MarketsDerivatives = true;    // the exchange prices raw materials; futures and options on the Futures tab
        public static bool MarketsBuilders = true;       // the rival builders listed and moved by the construction game's news; a project's materials hedged
        public static int VenturesLevel = 1;             // how demanding investors are: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore
        public static bool EconomyCompany = true;        // running a business (off: none of it runs and nothing is charged)
        public static int EconomyCompanyLevel = 1;       // how strict it is: 0 Relaxed, 1 Standard, 2 Realistic, 3 Hardcore
        public static bool EconomyCompanyStaff = true;   // ...the wage market, raises, rivals' offers, strikes, overtime and sick pay
        public static bool EconomyCompanyCover = true;   // ...business insurance: policies, premiums and claims
        public static bool EconomyCompanyMarketing = true;   // ...campaigns and their reports
        public static bool EconomyCompanyTax = true;     // ...the quarterly return and the accountant
        public static bool EconomyCompanyHide = true;    // ...cash sales kept off the books (off: every share is nothing and none can be set)
        public static bool EconomyCompanyLicences = true;    // ...licences and permits
        public static bool EconomyRota = true;           // the staff rota planner (off: the tab says so and nothing is worked out)
        public static bool EconomyManagers = true;       // managers deciding inside the player's limits (CompanyDirector.Managers)
        public static bool Estate = true;                // property (off: every value and rent is the game's own)
        public static bool EstateHotel = true;           // owning a hotel of the city's as a business
        public static int EstateRent = 100;              // rental income: percent of the market's rent of a unit the player lets himself, and so of what is asked, offered and renewed at (50 to 300)
        public static bool EstateHire = true;            // car hire (off: no car goes out, and one that is out is called back)
        public static bool TechPeople = true;            // professionals (off: the game's own staff alone; files are kept)
        public static bool TechJobs = true;              // the jobs board and the labour market (off: the game's own hiring alone)
        public static bool TechSector = true;            // the tech sector (off: those businesses earn nothing of their own; their files are kept)
        public static bool TechOfferNotes = true;        // ...a notification for each client's contract offer and each council tender
        public static bool TechSiteRooms = true;         // a data centre's server room furnished while the player is inside
        public static bool TechSiteHq = true;            // the city's tech companies each given an empty office building as their headquarters
        public static bool TechSiteCloud = true;         // the player's data centres signed with the cloud's name, a booth and a barrier, "Your cloud" on the map
        public static bool TechSiteOffices = true;       // a few tech fittings in an office a tech firm of the player's runs in, where there is room
        public static bool TechStartups = true;          // startups and venture capital (off: no startup is founded or pitches; holdings are kept)
        public static bool EconomyOnline = true;         // online shops (off: no orders come, nothing is charged or paid)
        public static bool EconomyOnlineWaitNotes = true; // ...a notification after three opening hours of orders nobody is on shift to pick
        public static bool EconomyDealer = true;         // the car dealership (off: its dealerships do not trade; their cars stay on the lots)
        public static bool EconomyDealerFloor = true;    // visitors on the Car Showroom's floor
        public static bool EconomyCleaning = true;       // the cleaning service (off: no round, no contracts, nothing cleaned or charged by it)
        public static bool OfficeConversion = true;      // some empty shops in the districts with offices are offices to rent while the mod runs
        public static bool EconomySale = true;           // selling a business (off: nothing is offered, paid or charged by it)
        public static bool EconomyBank = true;           // the player's own bank (off: it stands still, nothing is run, booked or charged)
        public static bool EconomyBranch = true;         // bank branches of the player's own bank (off: branches do nothing)
        public static bool EndScale = true;              // the end game at scale (off: every figure as before)
        public static bool EconomyBrands = true;         // brands and franchises (off: none of it runs, nothing is paid and footfall is without it)
        public static bool EconomyBanks = true;          // the Banks app
        public static bool Minimap = true;               // the minimap on the HUD while out in the city
        public static bool Gps = true;                   // a route along the roads to the game's destination, with the next turn
        public static bool MinimapRotate = true;         // the minimap turns with the player (off: north is up)
        public static bool NavOtherRoute = false;        // with the mod Voogle Route switched on
        public static int MinimapSize = 80;              // per cent of the width of the game's left column
        public static int MinimapZoom = 100;             // per cent: more is closer
        public static bool MinimapBase = true;           // buildings and water on the minimap, under the roads (off: roads only, as before)
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
        public static bool GarageSounds = true;          // recorded engine, exhaust, turbo and tyre sounds on the car the player drives
        public static bool TwoWheelers = true;           // bicycles and motorbikes: docks, racks, stands, and the ones already out
        public static bool HomeLife = true;              // home life and health as a whole
        public static bool HomeNeeds = true;             // hygiene, stress and how tidy the flat is
        public static bool HomeRoutines = true;          // cooking, coffee, tidying up and floor exercises as activities
        public static bool HomeSleep = true;             // sleep quality, sleep debt, the morning's mood
        public static bool HomeIllness = true;           // colds, flu, food poisoning, back pain, burnout; the doctor at the hospital
        public static bool HomeFitness = true;           // fitness from exercise, diet from what is eaten
        public static bool HomeBody = true;              // the body slowly changes with exercise and food (on waking)
        public static bool HomeJog = true;               // going for a run out of doors
        public static bool HomeMorning = true;           // the morning summary
        public static bool HomeMorningNews = true;       // ...with the city's news held while the player slept in it, one notification or few
        public static bool HomeStrip = true;             // the small needs strip on screen
        public static bool HomeEasy = false;             // needs wear down half as fast, illness is half as likely
        public static bool LawPrecincts = true;          // a police precinct in each district
        public static bool LawCustody = true;            // an arrest ends in a cell at the nearest precinct (off: the old time skip where caught)
        public static bool LawArrests = true;            // arrested for a death even when stopped, and for 911 calls about nothing; wanted after an escape
        public static bool LawReputation = true;         // the record and reputation have effects: scrutiny, thanks from passers-by
        public static bool LawStationFurniture = true;   // ...furnished with the game's own furniture and the walls and floors of its office design for the unit
        public static bool LawStation = true;            // a precinct can be walked into (off: the front desk answers at its door)
        public static bool LawMapMarkers = true;         // police stations on the city map and its filter list, and their markers in the street
        public static bool Buses = true;                 // bus stops near the player, buses that call at them, riding one
        public static bool TransitPeople = true;         // people waiting at a stop, getting on and off
        public static bool SubwayJourney = true;         // the game's own subway ride takes the time of a journey
        public static bool TaxiCall = true;              // the City Cabs contact: a taxi called from the phone comes to the player's street
        public static bool CarOwnership = true;          // distance, servicing, wear, breakdowns, insurance, registration, the fuel gauge
        public static bool CarEasy = false;              // easy car ownership: no fuel use, no breakdowns, no wear
        public static bool Leisure = true;               // everything under Scripts/Leisure
        public static bool LeisureChess = true;          // a chess player sets up on the pavement by day; the player can sit down to a game
        public static bool LeisureFishing = true;        // a line in the water at the waterfront
        public static bool LeisureNightOut = true;       // the dance floor and the bar of a nightclub; drink makes the car wander
        public static bool LeisureSights = true;         // landmarks to take in and tick off
        public static bool LeisureCinema = true;         // cinemas and theatres name what is on, and a show watched is remembered
        public static bool ClothesShops = true;          // the rack in clothing stores and the wardrobe at home
        public static bool ClothesMustBeBought = true;   // what the options page shows; the rule itself is kept per save
        public static bool NightLife = true;             // people outside nightclubs, cinemas and theatres in the evening
        public static bool AmbientSound = true;          // birds, crickets, a far hum of traffic, a murmur where people are
        public static int AmbientBirds = 60;             // percent
        public static int AmbientNight = 60;
        public static int AmbientCity = 60;
        public static int AmbientCrowd = 60;
        public static bool RadioHud = true;              // the station and song named on screen when the car radio changes
        public static bool PhotoMode = true;
        public static bool CityFires = true;             // car, bin and shopfront fires with a fire engine and crew
        public static int FireFrequency = 2;             // 1 = rare, 10 = one every few minutes
        public static bool WeatherMatters = true;        // wet roads, mist, storms, heat and cold change grip, traffic, accidents, crime and energy
        public static int WeatherFrequency = 5;          // how often a summer day is a heat wave and a winter day a cold snap: 1 = hardly ever, 10 = nearly half of them
        public static bool StreetEvents = true;          // markets, parades, protests, fun runs, film shoots, fireworks
        public static int StreetEventFrequency = 4;      // 1 = one day in twelve has one, 10 = four in five (the Saturday market and the fireworks nights are fixed)
        public static bool StreetProtestNotes = true;    // the notifications of a protest
        public static bool Social = true;                // the people in the player's life
        public static bool SocialMeetings = true;        // new people walk up to the player (after the gym, a shift, a coffee, a good turn)
        public static bool SocialRelationships = true;   // relationships wear down with neglect and can be damaged; off: nothing is ever lost
        public static bool SocialOutings = true;         // plans can be made and kept
        public static bool SocialContact = true;         // they get in touch themselves: invitations, favours, news, a visit at the shop
        public static bool SocialTexts = true;           // text messages from people (off: only what must be said is shown, as a notification)
        public static int SocialTextsMax = 3;            // texts a day from everybody together, 1 to 6
        public static bool SocialComposer = true;        // with an RLO Texts pack, a call's chat is answered in the player's own typed words
        public static bool StreetTalk = true;            // talk to anybody in the street: the game's panel over a passer-by offers Talk
        public static int StreetBrush = 1;               // how often people in the street have no time: 0 rarely, 1 sometimes, 2 often (StreetRules.BrushFactor)
        public static bool SocialMedia = true;           // Flare, the city's social media app
        public static bool SocialMediaNotices = true;    // Flare's few notifications (off: none at all; the app's badge still counts)
        public static int FlareBusy = 1;                 // how busy the city's feed is: 0 quiet, 1 normal, 2 busy
        public static bool FlareCityOffice = true;       // the City Office acts on reports (works, patrols, cleaning, inspections)
        public static bool FlareSharePosts = true;       // in a Going Public session the player's posts go to the session's shared feed
        public static bool FlareSharePictures = true;    // ...and their pictures, as small copies
        public static int FlareFilter = 0;               // what other players type: 0 standard (Steam's filter, else the mod's list), 1 strict (both, and addresses starred)
        public static int FlareTheme = 0;                // Flare's look: 0 follows the phone (dark from 19:00 to 07:00), 1 light, 2 dark
        public static bool FlarePictures = true;         // every account shows its picture (off: the initials)
        public static int FlareCityPhotos = 2;           // the city's posts with a photo: 0 none, 1 a few, 2 some (about three in ten), 3 many
        public static bool Standing = true;              // Standing: the city knows the player's wealth, name and reputation, and reacts
        public static bool StandingStreet = true;        // ...people in the street and customers in the player's shops know them
        public static bool StandingPress = true;         // ...reporters, the night show, magazines and the papers' pieces about the player
        public static bool Rivals = true;                // rival tycoons (off: they stand still and do nothing; the save keeps them)
        public static int RivalsStrength = 1;            // how hard they push: 0 Gentle, 1 Normal, 2 Hard, 3 Ruthless
        public static bool StandingCorruption = true;    // ...officials who offer to speed things up for a bribe, and what comes of it
        public static bool StandingRequestNotes = true;  // ...a notification when the city asks something of the player
        public static bool SocialNewspaper = true;       // the City Ledger: a morning newspaper, delivered to subscribers' homes and sold at kiosks
        public static bool LedgerKiosks = true;          // newspaper kiosks near the player's home sell the paper (off: subscribers only)
        public static bool LedgerOnline = true;          // Newsstand
        public static bool LedgerOnlineNotes = true;     // a notification each morning the online editions are in
        public static bool Hotels = true;                // eight places to stay: reception at the door, the room as a panel
        public static bool Holidays = true;              // trips bought at the game's travel agencies; burnout after weeks without a break
        public static bool TravelExtras = true;          // suitcase, travel insurance, a companion, trade fair invitations
        public static bool Careers = true;               // employers behind the help-wanted boards: rota, reviews, ranks
        public static bool CareersStudy = true;          // the city's courses (off: the game's own courses alone; records are kept)
        public static bool CareersDegrees = true;        // the university (off: those courses are not offered; results are kept)
        public static bool CareersJobs = true;           // professional jobs (off: no vacancies; a job held is kept)
        public static bool CareersLife = true;           // working life in a profession (off: a job is its days and pay alone)
        public static bool CareersFamily = true;         // the family's children at university, tutoring from home, and the player's own training academy as a business type
        public static bool CareersClassStart = true;     // "your class starts now" at a class's hour while the player is not inside the place that holds it, once a class
        public static bool CareersRejectReason = true;   // an employer's no says how many points of score the application was short, or that it was close
        public static bool CareersAgencyNote = true;     // a notification on the last day of a registration with the recruitment office
        public static bool CareersOwn = true;            // working for oneself (off: no contracts come; a practice is kept)
        public static bool CareersOwnOfferNotes = true;  // ...a notification for each contract a client offers
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
        public static bool Interiors = true;                // how a business looks counts (off: none of the parts below, and the tab is gone)
        public static int InteriorsStandardStrength = 100;  // per cent, 0 off to 200 double: each district's standard (6% at most at 100)
        public static int InteriorsDecorStrength = 100;     // per cent, 0 off to 200 double: the customers decor brings (10% at a score of 100)
        public static int InteriorsPricesStrength = 100;    // per cent, 0 off to 200 double: the price a nicer place may charge (3% at most at 100)
        public static int InteriorsWearStrength = 100;      // per cent, 0 off to 200 double: how fast finishes wear and get dirty
        public static bool InteriorsBasket = true;          // over the standard each customer takes more of the shop's stock (off with Meaningful Interiors)
        public static bool InteriorsStanding = true;        // a well kept place lifts its brand's standards and Standing's customers, and makes the news; a shabby one the other way
        public static bool Designs = true;                  // the interior installation firms offer the mod's business types ready-made designs
        public static bool InteriorsStaff = true;           // plants, good light and a rest corner lift staff; a good office helps hiring and a firm's bids
        public static bool Permadeath = false;           // what the options page shows; the switch itself is kept per save, off unless switched on
        public static bool NewGroundTestRoad = false;    // a copy of a short stretch of road added to the traffic at each load, for the developer's test
        public static bool Uptown = true;                // the district north of Manhattan laid at each load; read only then

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
                // The game starts in the main menu, where the switch is no save's: off.
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
            MealsSeated = Bool(modId, MealsSeatedId, MealsSeated);
            MealsCooked = Bool(modId, MealsCookedId, MealsCooked);
            MealsDance = Bool(modId, MealsDanceId, MealsDance);
            NewBuildings = Bool(modId, NewBuildingsId, NewBuildings);
            Trades = Bool(modId, TradesId, Trades);
            TradesOffice = Bool(modId, TradesOfficeId, TradesOffice);
            Construction = Bool(modId, ConstructionId, Construction);
            ConstructionDevelopers = Bool(modId, ConstructionDevelopersId, ConstructionDevelopers);
            ConstructionLandTax = Bool(modId, ConstructionLandTaxId, ConstructionLandTax);
            ConstructionRisks = Bool(modId, ConstructionRisksId, ConstructionRisks);
            ConstructionShowFlats = Bool(modId, ConstructionShowFlatsId, ConstructionShowFlats);
            ConstructionRenovations = Bool(modId, ConstructionRenovationsId, ConstructionRenovations);
            ConstructionCity = Bool(modId, ConstructionCityId, ConstructionCity);
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
            // The old single slider's value means nothing to the two per-car sliders: dropped, so they start at 100.
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
            MarketsBuilders = Bool(modId, MarketsBuildersId, MarketsBuilders);
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
            InteriorsStandardStrength = Strength(modId, InteriorsStandardStrengthId, StandardLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.StandardWas,
                StandardSwitchWas, InteriorsStandardStrength);
            InteriorsDecorStrength = Strength(modId, InteriorsDecorStrengthId, DecorLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.DecorWas, DecorSwitchWas, InteriorsDecorStrength);
            InteriorsPricesStrength = Strength(modId, InteriorsPricesStrengthId, PricesLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.PricesWas, PricesSwitchWas, InteriorsPricesStrength);
            InteriorsWearStrength = Strength(modId, InteriorsWearStrengthId, WearLevelWas, global::RealLifeOverhaul.Interiors.InteriorRules.WearWas, WearSwitchWas, InteriorsWearStrength);
            InteriorsBasket = Bool(modId, InteriorsBasketId, InteriorsBasket);
            InteriorsStanding = Bool(modId, InteriorsStandingId, InteriorsStanding);
            InteriorsStaff = Bool(modId, InteriorsStaffId, InteriorsStaff);
            Designs = Bool(modId, DesignsId, Designs);
            NewGroundTestRoad = Bool(modId, NewGroundTestRoadId, NewGroundTestRoad);
            Uptown = Bool(modId, UptownId, Uptown);
        }

        /// <summary>
        /// "Clothes must be bought" is kept per save: a loaded save's value is written here and to PlayerPrefs, so the
        /// options page shows what holds.
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
        /// "Permadeath" is kept per save: the save played writes it here and to PlayerPrefs, the main menu writes it off.
        /// True if it changed.
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
        /// Once only: a saved basket still at its old default (off) is switched on, and a saved decor choice still at 5%
        /// moves to 10%, the new defaults. Any other value, and anything chosen afterwards, is never touched.
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
        /// An interiors strength slider (per cent, 0 to 200): its saved value, else the drop-down it replaced, else off if
        /// the older switch was off, else the default. Nothing is written.
        /// </summary>
        private static int Strength(string modId, string id, string levelWas, int[] was, string switchWas, int fallback)
        {
            string key = Key(modId, id), old = Key(modId, levelWas);
            if (UnityEngine.PlayerPrefs.HasKey(key))
            {
                return global::RealLifeOverhaul.Interiors.InteriorRules.Clamp(UnityEngine.PlayerPrefs.GetInt(key, fallback),
                    global::RealLifeOverhaul.Interiors.InteriorRules.StrengthLeast, global::RealLifeOverhaul.Interiors.InteriorRules.StrengthMost);
            }
            if (UnityEngine.PlayerPrefs.HasKey(old))
            {
                return global::RealLifeOverhaul.Interiors.InteriorRules.Was(was, UnityEngine.PlayerPrefs.GetInt(old, 0));
            }
            return Bool(modId, switchWas, true) ? fallback : 0;
        }

        // The key format the game uses when it saves mod options.
        private static string Key(string modId, string id) => "m:" + modId + ":" + id;

        private static bool Bool(string modId, string id, bool fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback ? 1 : 0) != 0;

        private static int Int(string modId, string id, int fallback) =>
            UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback);
    }
}
