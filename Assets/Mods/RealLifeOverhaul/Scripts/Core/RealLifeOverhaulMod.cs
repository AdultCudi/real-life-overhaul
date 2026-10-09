using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using RealLifeOverhaul;
using UnityEngine;

[assembly: RegisterModClass(typeof(RealLifeOverhaulInit))]
[assembly: RegisterModClass(typeof(RealLifeOverhaulCity))]

namespace RealLifeOverhaul
{
    /// <summary>Loaded once at game start: registers the options page.</summary>
    [ModEntryOnInitializationLoad]
    public class RealLifeOverhaulInit : IModBigAmbitions
    {
        private ModContext _context;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            IModLogger log = context.Logger;
            context = Meta.Dev.Gated(context);   // Info lines only while "Developer diagnostics" is on (read below; OptionsLayout.Organise applies it)
            _context = context;
            Keys.Logger = context.Logger;
            Settings.Load(context.ModId);
            Meta.Dev.Gated(context);   // (the option has been read now)

            var options = new ModOptions()
                .AddHeader("reallife_camera_header")
                .AddToggle(Settings.CameraEnabledId, "reallife_camera_enabled", Settings.CameraEnabled,
                    v => Settings.CameraEnabled = v)
                .AddToggle(Settings.MouseLookId, "reallife_camera_mouselook_keys", Settings.MouseLook,
                    v => Settings.MouseLook = v)
                .AddSlider(Settings.MouseSensitivityId, "reallife_camera_sensitivity", 1, 20, Settings.MouseSensitivity,
                    v => Settings.MouseSensitivity = v)
                .AddToggle(Settings.EnterWithKeyId, "reallife_enter_with_keys", Settings.EnterWithKey,
                    v => Settings.EnterWithKey = v)
                .AddToggle(Settings.CameraIndoorsId, "reallife_camera_indoors", Settings.CameraIndoors,
                    v => Settings.CameraIndoors = v)
                .AddSlider(Settings.CameraPitchId, "reallife_camera_pitch", 5, 70, Settings.CameraPitch,
                    v => Settings.CameraPitch = v)
                .AddSlider(Settings.CameraDistanceId, "reallife_camera_distance", 3, 20, Settings.CameraDistance,
                    v => Settings.CameraDistance = v)
                .AddSlider(Settings.CameraFovId, "reallife_camera_fov", 30, 90, Settings.CameraFov,
                    v => Settings.CameraFov = v)
                .AddSlider(Settings.CameraHeightId, "reallife_camera_height", 5, 25, Settings.CameraHeight,
                    v => Settings.CameraHeight = v)
                .AddToggle(Settings.CarCameraId, "reallife_car_camera", Settings.CarCamera,
                    v => Settings.CarCamera = v)
                .AddSlider(Settings.CarCameraDistanceId, "reallife_car_camera_distance", 4, 20, Settings.CarCameraDistance,
                    v => Settings.CarCameraDistance = v)
                .AddToggle(Settings.CarTuningId, "reallife_car_tuning", Settings.CarTuning,
                    v => Settings.CarTuning = v)
                .AddSlider(Settings.CarTopSpeedId, "reallife_car_top_speed", 70, 150, Settings.CarTopSpeed,
                    v => Settings.CarTopSpeed = v)
                .AddSlider(Settings.CarAccelerationId, "reallife_car_acceleration", 70, 150, Settings.CarAcceleration,
                    v => Settings.CarAcceleration = v)
                .AddToggle(Settings.SmoothThrottleId, "reallife_car_smooth_throttle", Settings.SmoothThrottle,
                    v => Settings.SmoothThrottle = v)
                .AddButton("reallife_car_measure", CarMeasure.Arm)
                .AddToggle(Settings.SpeedometerId, "reallife_speedometer", Settings.Speedometer,
                    v => Settings.Speedometer = v)
                .AddSlider(Settings.CameraFollowId, "reallife_camera_follow", 0, 10, Settings.CameraFollow,
                    v => Settings.CameraFollow = v)
                .AddToggle(Settings.MapPlacesShownId, "reallife_map_places_shown", Settings.MapPlacesShown,
                    v => Settings.MapPlacesShown = v)
                .AddSplitter()
                .AddHeader("reallife_visuals_header")
                .AddToggle(Settings.VisualsEnabledId, "reallife_visuals_enabled", Settings.VisualsEnabled,
                    v => Settings.VisualsEnabled = v)
                .AddSlider(Settings.SaturationId, "reallife_visuals_saturation", -50, 50, Settings.Saturation,
                    v => Settings.Saturation = v)
                .AddSlider(Settings.ContrastId, "reallife_visuals_contrast", -50, 50, Settings.Contrast,
                    v => Settings.Contrast = v)
                .AddSlider(Settings.BloomId, "reallife_visuals_bloom", 0, 100, Settings.Bloom,
                    v => Settings.Bloom = v)
                .AddSlider(Settings.DetailId, "reallife_visuals_detail", 100, 300, Settings.Detail,
                    v => Settings.Detail = v)
                .AddToggle(Settings.CharacterHighDetailId, "reallife_character_high_detail", Settings.CharacterHighDetail,
                    v => Settings.CharacterHighDetail = v)
                .AddToggle(Settings.CharacterHairId, "reallife_character_hair", Settings.CharacterHair,
                    v => Settings.CharacterHair = v)
                .AddToggle(Settings.CharacterLightingId, "reallife_character_lighting", Settings.CharacterLighting,
                    v => Settings.CharacterLighting = v)
                .AddToggle(Settings.CharacterSkinId, "reallife_character_skin", Settings.CharacterSkin,
                    v => Settings.CharacterSkin = v)
                .AddSplitter()
                .AddHeader("reallife_looks_header")
                .AddToggle(Settings.CharacterNewLooksId, "reallife_character_new_looks", Settings.CharacterNewLooks,
                    v => Settings.CharacterNewLooks = v)
                .AddLooks()
                .AddSplitter()
                .AddHeader("reallife_life_header")
                .AddToggle(Settings.LifeEventsId, "reallife_life_events", Settings.LifeEvents,
                    v => Settings.LifeEvents = v)
                .AddSlider(Settings.EventFrequencyId, "reallife_life_frequency", 1, 20, Settings.EventFrequency,
                    v => Settings.EventFrequency = v)
                .AddToggle(Settings.CrashConsequencesId, "reallife_crash_consequences", Settings.CrashConsequences,
                    v => Settings.CrashConsequences = v)
                .AddToggle(Settings.TrafficAccidentsId, "reallife_traffic_accidents", Settings.TrafficAccidents,
                    v => Settings.TrafficAccidents = v)
                .AddSlider(Settings.AccidentFrequencyId, "reallife_accident_frequency", 1, 10, Settings.AccidentFrequency,
                    v => Settings.AccidentFrequency = v)
                .AddToggle(Settings.PoliceId, "reallife_police", Settings.Police,
                    v => Settings.Police = v)
                .AddSlider(Settings.PolicePresenceId, "reallife_police_presence", 0, 10, Settings.PolicePresence,
                    v => Settings.PolicePresence = v)
                .AddToggle(Settings.PolicePursuitId, "reallife_police_pursuit", Settings.PolicePursuit,
                    v => Settings.PolicePursuit = v)
                .AddToggle(Settings.SirenId, "reallife_siren", Settings.Siren,
                    v => Settings.Siren = v)
                .AddToggle(Settings.PeopleId, "reallife_people", Settings.People,
                    v => Settings.People = v)
                .AddToggle(Settings.OfficersOnPatrolId, "reallife_patrol_officers", Settings.OfficersOnPatrol,
                    v => Settings.OfficersOnPatrol = v)
                .AddSlider(Settings.PatrolFrequencyId, "reallife_patrol_frequency", 1, 10, Settings.PatrolFrequency,
                    v => Settings.PatrolFrequency = v)
                .AddToggle(Settings.RagdollsId, "reallife_ragdolls", Settings.Ragdolls,
                    v => Settings.Ragdolls = v)
                .AddToggle(Settings.PlayerKnockdownId, "reallife_player_knockdown", Settings.PlayerKnockdown,
                    v => Settings.PlayerKnockdown = v)
                .AddToggle(Settings.PedestrianIncidentsId, "reallife_ped_incidents", Settings.PedestrianIncidents,
                    v => Settings.PedestrianIncidents = v)
                .AddToggle(Settings.PhoneContactsId, "reallife_phone_contacts", Settings.PhoneContacts,
                    v => Settings.PhoneContacts = v)
                .AddToggle(Settings.PhoneSkinId, "reallife_phone_skin", Settings.PhoneSkin,
                    v => Settings.PhoneSkin = v)
                .AddButton("reallife_test_event", () => LifeEvents.Instance?.TriggerEventNow())
                .AddButton("reallife_test_accident", () => TrafficAccidents.Instance?.StageAccidentNow())
                .AddButton("reallife_test_accident_minor", () => TrafficAccidents.Instance?.StageAccidentNow(TrafficAccidents.Severity.Minor))
                .AddButton("reallife_test_accident_moderate", () => TrafficAccidents.Instance?.StageAccidentNow(TrafficAccidents.Severity.Moderate))
                .AddButton("reallife_test_accident_severe", () => TrafficAccidents.Instance?.StageAccidentNow(TrafficAccidents.Severity.Severe))
                .AddButton("reallife_test_ragdoll", () => PedestrianIncidents.Instance?.TestNearest())
                .AddButton("reallife_test_ped_incident", () => PedestrianIncidents.Instance?.StageNow())
                .AddSplitter()
                .AddHeader("reallife_crime_header")
                .AddToggle(Settings.StreetCrimeId, "reallife_crime_enabled", Settings.StreetCrime,
                    v => Settings.StreetCrime = v)
                .AddSlider(Settings.CrimeFrequencyId, "reallife_crime_frequency_all", 1, 10, Settings.CrimeFrequency,
                    v => Settings.CrimeFrequency = v)
                .AddToggle(Settings.CrimeAgainstPlayerId, "reallife_crime_against_player", Settings.CrimeAgainstPlayer,
                    v => Settings.CrimeAgainstPlayer = v)
                .AddToggle(Settings.PlayerOffencesId, "reallife_player_offences", Settings.PlayerOffences,
                    v => Settings.PlayerOffences = v)
                .AddButton("reallife_test_crime", () => CrimeDirector.Instance?.StageNow(0))
                .AddButton("reallife_test_crime_traffic", () => CrimeDirector.Instance?.StageNow(1))
                .AddButton("reallife_test_crime_shop", () => CrimeDirector.Instance?.StageNow(2))
                .AddButton("reallife_test_crime_player", () => CrimeDirector.Instance?.StageNow(3))
                .AddButton("reallife_test_crime_offscreen", () => CrimeDirector.Instance?.TestOffscreen())
                .AddButton("reallife_test_crime_stop", () => PoliceAttention.Instance?.TestStop())
                .AddSplitter()
                .AddHeader("reallife_city_header")
                .AddToggle(Settings.CityLifeId, "reallife_city_enabled", Settings.CityLife,
                    v => Settings.CityLife = v)
                .AddSlider(Settings.CityDensityId, "reallife_city_density", 1, 10, Settings.CityDensity,
                    v => Settings.CityDensity = v)
                .AddToggle(Settings.CityWorkersId, "reallife_city_workers", Settings.CityWorkers,
                    v => Settings.CityWorkers = v)
                .AddToggle(Settings.CityEverydayId, "reallife_city_everyday", Settings.CityEveryday,
                    v => Settings.CityEveryday = v)
                .AddToggle(Settings.CityInteractionsId, "reallife_city_interactions", Settings.CityInteractions,
                    v => Settings.CityInteractions = v)
                .AddButton("reallife_city_test_workers", () => CityLifeDirector.Instance?.Test(CityFamily.Workers))
                .AddButton("reallife_city_test_everyday", () => CityLifeDirector.Instance?.Test(CityFamily.Everyday))
                .AddButton("reallife_city_test_stalls", () => CityLifeDirector.Instance?.Test(CityFamily.Stalls))
                .AddButton("reallife_city_test_deliveries", () => CityLifeDirector.Instance?.Test(CityFamily.Deliveries))
                .AddButton("reallife_city_test_leisure", () => CityLifeDirector.Instance?.Test(CityFamily.Leisure))
                .AddButton("reallife_city_test_clear", () => CityLifeDirector.Instance?.TestClear())
                .AddSplitter()
                .AddHeader("reallife_jobs_header")
                .AddToggle(Settings.JobsId, "reallife_jobs_enabled", Settings.Jobs,
                    v => Settings.Jobs = v)
                .AddSlider(Settings.JobPayId, "reallife_jobs_pay", 50, 200, Settings.JobPay,
                    v => Settings.JobPay = v)
                .AddButton("reallife_test_job_flyers", () => JobDirector.Instance?.TestStart(JobCatalogue.Flyers))
                .AddButton("reallife_test_job_courier", () => JobDirector.Instance?.TestStart(JobCatalogue.Courier))
                .AddButton("reallife_test_job_taxi",() => JobDirector.Instance?.TestStart(JobCatalogue.Taxi))
                .AddButton("reallife_test_job_cleaner", () => JobDirector.Instance?.TestStart(JobCatalogue.Cleaner))
                .AddButton("reallife_test_job_stocker", () => JobDirector.Instance?.TestStart(JobCatalogue.Stocker))
                .AddButton("reallife_test_job_vendor", () => JobDirector.Instance?.TestStart(JobCatalogue.Vendor))
                .AddButton("reallife_test_job_busker", () => JobDirector.Instance?.TestStart(JobCatalogue.Busker))
                .AddButton("reallife_test_job_stop", () => JobDirector.Instance?.Quit())
                .AddToggle(Settings.CareersId, "reallife_careers_enabled", Settings.Careers,
                    v => Settings.Careers = v)
                .AddButton("reallife_test_career_sign_on", () => JobDirector.Instance?.TestSignOn())
                .AddButton("reallife_test_career_shift", () => JobDirector.Instance?.TestShift(CareerWork.Gig, true))
                .AddButton("reallife_test_career_review", () => JobDirector.Instance?.TestReview())
                .AddButton("reallife_test_career_promote", () => JobDirector.Instance?.TestPromote())
                .AddButton("reallife_test_career_dispatch", () => JobDirector.Instance?.TestShift(CareerWork.Dispatch, false))
                .AddButton("reallife_test_career_lead", () => JobDirector.Instance?.TestShift(CareerWork.Lead, false))
                .AddButton("reallife_test_career_gig", () => JobDirector.Instance?.TestGig())
                .AddSplitter()
                .AddHeader("reallife_business_header")
                .AddToggle(Settings.BusinessId, "reallife_business_enabled", Settings.Business,
                    v => Settings.Business = v)
                .AddSlider(Settings.BusinessFrequencyId, "reallife_business_frequency", 1, 10, Settings.BusinessFrequency,
                    v => Settings.BusinessFrequency = v)
                .AddToggle(Settings.BusinessEasyId, "reallife_business_easy", Settings.BusinessEasy,
                    v => Settings.BusinessEasy = v)
                .AddToggle(Settings.BusinessRivalsId, "reallife_business_rivals", Settings.BusinessRivals,
                    v => Settings.BusinessRivals = v)
                .AddToggle(Settings.BusinessRacketId, "reallife_business_racket", Settings.BusinessRacket,
                    v => Settings.BusinessRacket = v)
                .AddToggle(Settings.BusinessInspectionsId, "reallife_business_inspections", Settings.BusinessInspections,
                    v => Settings.BusinessInspections = v)
                .AddToggle(Settings.BusinessStaffId, "reallife_business_staff", Settings.BusinessStaff,
                    v => Settings.BusinessStaff = v)
                .AddToggle(Settings.BusinessSuppliersId, "reallife_business_suppliers", Settings.BusinessSuppliers,
                    v => Settings.BusinessSuppliers = v)
                .AddToggle(Settings.BusinessCustomersId, "reallife_business_customers", Settings.BusinessCustomers,
                    v => Settings.BusinessCustomers = v)
                .AddToggle(Settings.BusinessAuditId, "reallife_business_audit", Settings.BusinessAudit,
                    v => Settings.BusinessAudit = v)
                .AddToggle(Settings.BusinessOpportunitiesId, "reallife_business_opportunities", Settings.BusinessOpportunities,
                    v => Settings.BusinessOpportunities = v)
                .AddButton("reallife_test_business_rivals", () => BusinessDirector.Instance?.Test(PressureGroup.Rivals))
                .AddButton("reallife_test_business_racket", () => BusinessDirector.Instance?.Test(PressureGroup.Racket))
                .AddButton("reallife_test_business_inspections", () => BusinessDirector.Instance?.Test(PressureGroup.Inspections))
                .AddButton("reallife_test_business_staff", () => BusinessDirector.Instance?.Test(PressureGroup.Staff))
                .AddButton("reallife_test_business_suppliers", () => BusinessDirector.Instance?.Test(PressureGroup.Suppliers))
                .AddButton("reallife_test_business_customers", () => BusinessDirector.Instance?.Test(PressureGroup.Customers))
                .AddButton("reallife_test_business_audit", () => BusinessDirector.Instance?.Test(PressureGroup.Audit))
                .AddButton("reallife_test_business_opportunities", () => BusinessDirector.Instance?.Test(PressureGroup.Opportunities))
                .AddButton("reallife_test_business_desk", () => BusinessDirector.Instance?.TestDesk())
                .AddButton("reallife_test_business_resolve", () => BusinessDirector.Instance?.TestResolveAll())
                .AddSplitter()
                .AddHeader("reallife_home_header")
                .AddToggle(Settings.HomeLifeId, "reallife_home_enabled", Settings.HomeLife,
                    v => Settings.HomeLife = v)
                .AddToggle(Settings.HomeNeedsId, "reallife_home_needs", Settings.HomeNeeds,
                    v => Settings.HomeNeeds = v)
                .AddToggle(Settings.HomeStripId, "reallife_home_strip", Settings.HomeStrip,
                    v => Settings.HomeStrip = v)
                .AddToggle(Settings.HomeEasyId, "reallife_home_easy", Settings.HomeEasy,
                    v => Settings.HomeEasy = v)
                .AddToggle(Settings.HomeRoutinesId, "reallife_home_routines", Settings.HomeRoutines,
                    v => Settings.HomeRoutines = v)
                .AddToggle(Settings.HomeSleepId, "reallife_home_sleep", Settings.HomeSleep,
                    v => Settings.HomeSleep = v)
                .AddToggle(Settings.HomeMorningId, "reallife_home_morning_option", Settings.HomeMorning,
                    v => Settings.HomeMorning = v)
                .AddToggle(Settings.HomeIllnessId, "reallife_home_illness", Settings.HomeIllness,
                    v => Settings.HomeIllness = v)
                .AddToggle(Settings.HomeFitnessId, "reallife_home_fitness", Settings.HomeFitness,
                    v => Settings.HomeFitness = v)
                .AddToggle(Settings.HomeBodyId, "reallife_home_body", Settings.HomeBody,
                    v => Settings.HomeBody = v)
                .AddToggle(Settings.HomeJogId, "reallife_home_jog", Settings.HomeJog,
                    v => Settings.HomeJog = v)
                .AddButton("reallife_test_home_page", () => HomeDirector.Instance?.TestPage())
                .AddButton("reallife_test_home_cook", () => HomeDirector.Instance?.TestRoutine(HomeWish.Cook))
                .AddButton("reallife_test_home_coffee", () => HomeDirector.Instance?.TestRoutine(HomeWish.Coffee))
                .AddButton("reallife_test_home_tidy", () => HomeDirector.Instance?.TestRoutine(HomeWish.Tidy))
                .AddButton("reallife_test_home_stretch", () => HomeDirector.Instance?.TestRoutine(HomeWish.Stretch))
                .AddButton("reallife_test_home_pushups", () => HomeDirector.Instance?.TestRoutine(HomeWish.PushUps))
                .AddButton("reallife_test_home_situps", () => HomeDirector.Instance?.TestRoutine(HomeWish.SitUps))
                .AddButton("reallife_test_home_run", () => HomeDirector.Instance?.TestRoutine(HomeWish.Run))
                .AddButton("reallife_test_home_run_stop", () => HomeDirector.Instance?.TestRoutine(HomeWish.StopRun))
                .AddButton("reallife_test_home_doctor", () => HomeDirector.Instance?.TestRoutine(HomeWish.Doctor))
                .AddButton("reallife_test_home_cold", () => HomeDirector.Instance?.TestSick(Illness.Cold))
                .AddButton("reallife_test_home_flu", () => HomeDirector.Instance?.TestSick(Illness.Flu))
                .AddButton("reallife_test_home_food", () => HomeDirector.Instance?.TestSick(Illness.FoodPoisoning))
                .AddButton("reallife_test_home_back", () => HomeDirector.Instance?.TestSick(Illness.BackPain))
                .AddButton("reallife_test_home_burnout", () => HomeDirector.Instance?.TestSick(Illness.Burnout))
                .AddButton("reallife_test_home_cure", () => HomeDirector.Instance?.TestCure())
                .AddButton("reallife_test_home_fitter", () => HomeDirector.Instance?.TestFitness(10f))
                .AddButton("reallife_test_home_unfit", () => HomeDirector.Instance?.TestFitness(-10f))
                .AddButton("reallife_test_home_needs_low", () => HomeDirector.Instance?.TestNeeds(true))
                .AddButton("reallife_test_home_needs_full", () => HomeDirector.Instance?.TestNeeds(false))
                .AddSplitter()
                .AddHeader("reallife_law_header")
                .AddToggle(Settings.LawPrecinctsId, "reallife_law_precincts", Settings.LawPrecincts,
                    v => Settings.LawPrecincts = v)
                .AddToggle(Settings.LawCustodyId, "reallife_law_custody", Settings.LawCustody,
                    v => Settings.LawCustody = v)
                .AddToggle(Settings.LawArrestsId, "reallife_law_arrests", Settings.LawArrests,
                    v => Settings.LawArrests = v)
                .AddToggle(Settings.LawReputationId, "reallife_law_reputation", Settings.LawReputation,
                    v => Settings.LawReputation = v)
                .AddToggle(Settings.LawStationId, "reallife_law_station", Settings.LawStation,
                    v => Settings.LawStation = v)
                .AddToggle(Settings.LawMapMarkersId, "reallife_law_map_markers", Settings.LawMapMarkers,
                    v => Settings.LawMapMarkers = v)
                .AddButton("reallife_law_test_arrest_minor", () => Law.LawDirector.Instance?.TestArrest(0))
                .AddButton("reallife_law_test_arrest_serious", () => Law.LawDirector.Instance?.TestArrest(1))
                .AddButton("reallife_law_test_arrest_fatal", () => Law.LawDirector.Instance?.TestArrest(2))
                .AddButton("reallife_law_test_nearest", () => Law.LawDirector.Instance?.TestNearest())
                .AddButton("reallife_law_test_desk", () => Law.LawDirector.Instance?.TestDesk())
                .AddButton("reallife_law_test_dress", () => Law.LawDirector.Instance?.TestDress())
                .AddButton("reallife_law_test_clear", () => Law.LawDirector.Instance?.TestClearRecord())
                .AddSplitter()
                .AddHeader("reallife_transport_header")
                .AddToggle(Settings.BusesId, "reallife_transport_buses", Settings.Buses,
                    v => Settings.Buses = v)
                .AddToggle(Settings.TransitPeopleId, "reallife_transport_people", Settings.TransitPeople,
                    v => Settings.TransitPeople = v)
                .AddToggle(Settings.SubwayJourneyId, "reallife_transport_subway", Settings.SubwayJourney,
                    v => Settings.SubwayJourney = v)
                .AddToggle(Settings.CarOwnershipId, "reallife_transport_cars", Settings.CarOwnership,
                    v => Settings.CarOwnership = v)
                .AddToggle(Settings.CarEasyId, "reallife_transport_cars_easy", Settings.CarEasy,
                    v => Settings.CarEasy = v)
                .AddButton("reallife_test_transit_stop", () => TransitDirector.Instance?.TestDress())
                .AddButton("reallife_test_transit_bus", () => TransitDirector.Instance?.TestBus())
                .AddButton("reallife_test_transit_ride", () => TransitDirector.Instance?.TestRide())
                .AddButton("reallife_test_transit_subway", () => TransitDirector.Instance?.TestSubway())
                .AddButton("reallife_test_car_fuel", () => CarOwnership.Instance?.TestFuel())
                .AddButton("reallife_test_car_breakdown", () => CarOwnership.Instance?.TestBreakdown())
                .AddButton("reallife_test_car_garage", () => CarOwnership.Instance?.TestGarage())
                .AddSplitter()
                .AddHeader("reallife_leisure_header")
                .AddToggle(Settings.LeisureId, "reallife_leisure_enabled", Settings.Leisure,
                    v => Settings.Leisure = v)
                .AddToggle(Settings.LeisureChessId, "reallife_leisure_opt_chess", Settings.LeisureChess,
                    v => Settings.LeisureChess = v)
                .AddToggle(Settings.LeisureFishingId, "reallife_leisure_opt_fishing", Settings.LeisureFishing,
                    v => Settings.LeisureFishing = v)
                .AddToggle(Settings.LeisureNightOutId, "reallife_leisure_opt_night_out", Settings.LeisureNightOut,
                    v => Settings.LeisureNightOut = v)
                .AddToggle(Settings.LeisureSightsId, "reallife_leisure_opt_sights", Settings.LeisureSights,
                    v => Settings.LeisureSights = v)
                .AddToggle(Settings.LeisureCinemaId, "reallife_leisure_opt_cinema", Settings.LeisureCinema,
                    v => Settings.LeisureCinema = v)
                .AddToggle(Settings.ClothesShopsId, "reallife_leisure_opt_clothes", Settings.ClothesShops,
                    v => Settings.ClothesShops = v)
                .AddToggle(Settings.ClothesMustBeBoughtId, "reallife_leisure_opt_must_buy", Settings.ClothesMustBeBought,
                    v => { Settings.ClothesMustBeBought = v; Leisure.ClothesShop.OptionChanged(v); })
                .AddToggle(Settings.NightLifeId, "reallife_leisure_opt_night_life", Settings.NightLife,
                    v => Settings.NightLife = v)
                .AddToggle(Settings.AmbientSoundId, "reallife_leisure_opt_ambient", Settings.AmbientSound,
                    v => Settings.AmbientSound = v)
                .AddSlider(Settings.AmbientBirdsId, "reallife_leisure_opt_ambient_birds", 0, 100, Settings.AmbientBirds,
                    v => Settings.AmbientBirds = v)
                .AddSlider(Settings.AmbientNightId, "reallife_leisure_opt_ambient_night", 0, 100, Settings.AmbientNight,
                    v => Settings.AmbientNight = v)
                .AddSlider(Settings.AmbientCityId, "reallife_leisure_opt_ambient_city", 0, 100, Settings.AmbientCity,
                    v => Settings.AmbientCity = v)
                .AddSlider(Settings.AmbientCrowdId, "reallife_leisure_opt_ambient_crowd", 0, 100, Settings.AmbientCrowd,
                    v => Settings.AmbientCrowd = v)
                .AddToggle(Settings.RadioHudId, "reallife_leisure_opt_radio_hud", Settings.RadioHud,
                    v => Settings.RadioHud = v)
                .AddToggle(Settings.PhotoModeId, "reallife_leisure_opt_photo", Settings.PhotoMode,
                    v => Settings.PhotoMode = v)
                .AddButton("reallife_leisure_test_chess", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Chess))
                .AddButton("reallife_leisure_test_fishing", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Fishing))
                .AddButton("reallife_leisure_test_dance", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Dance))
                .AddButton("reallife_leisure_test_drinks", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Drinks))
                .AddButton("reallife_leisure_test_sights", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Sightseeing))
                .AddButton("reallife_leisure_test_rack", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Rack))
                .AddButton("reallife_leisure_test_wardrobe", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Wardrobe))
                .AddButton("reallife_leisure_test_night", () => Leisure.LeisureDirector.Instance?.TestNight())
                .AddButton("reallife_leisure_test_sound", Leisure.LeisureSound.Test)
                .AddButton("reallife_leisure_test_photo", () => Leisure.PhotoMode.Instance?.Test())
                .AddButton("reallife_leisure_test_records", () => Leisure.LeisureDirector.Instance?.ShowRecords())
                .AddButton("reallife_leisure_test_stop", () => Leisure.LeisureDirector.Instance?.Quit())
                .AddSplitter()
                .AddHeader("reallife_events_header")
                .AddToggle(Settings.CityFiresId, "reallife_events_fires", Settings.CityFires,
                    v => Settings.CityFires = v)
                .AddSlider(Settings.FireFrequencyId, "reallife_events_fire_frequency", 1, 10, Settings.FireFrequency,
                    v => Settings.FireFrequency = v)
                .AddToggle(Settings.WeatherMattersId, "reallife_events_weather", Settings.WeatherMatters,
                    v => Settings.WeatherMatters = v)
                .AddSlider(Settings.WeatherFrequencyId, "reallife_events_weather_frequency", 1, 10, Settings.WeatherFrequency,
                    v => Settings.WeatherFrequency = v)
                .AddToggle(Settings.StreetEventsId, "reallife_events_street", Settings.StreetEvents,
                    v => Settings.StreetEvents = v)
                .AddSlider(Settings.StreetEventFrequencyId, "reallife_events_street_frequency", 1, 10, Settings.StreetEventFrequency,
                    v => Settings.StreetEventFrequency = v)
                .AddButton("reallife_events_test_fire_car", () => Events.EventDirector.Instance?.TestFire(Events.FireKind.Car))
                .AddButton("reallife_events_test_fire_bin", () => Events.EventDirector.Instance?.TestFire(Events.FireKind.Bin))
                .AddButton("reallife_events_test_fire_building", () => Events.EventDirector.Instance?.TestFire(Events.FireKind.Building))
                .AddButton("reallife_events_test_weather_rain", () => Events.EventDirector.Instance?.TestWeather(Events.Sky.Rain, Events.Spell.None))
                .AddButton("reallife_events_test_weather_mist", () => Events.EventDirector.Instance?.TestWeather(Events.Sky.Mist, Events.Spell.None))
                .AddButton("reallife_events_test_weather_storm", () => Events.EventDirector.Instance?.TestWeather(Events.Sky.Storm, Events.Spell.None))
                .AddButton("reallife_events_test_weather_heat", () => Events.EventDirector.Instance?.TestWeather(Events.Sky.Dry, Events.Spell.HeatWave))
                .AddButton("reallife_events_test_weather_cold", () => Events.EventDirector.Instance?.TestWeather(Events.Sky.Dry, Events.Spell.ColdSnap))
                .AddButton("reallife_events_test_weather_ice", () => Events.EventDirector.Instance?.TestWeather(Events.Sky.Dry, Events.Spell.Ice))
                .AddButton("reallife_events_test_market", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.Market))
                .AddButton("reallife_events_test_farmers", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.FarmersMarket))
                .AddButton("reallife_events_test_parade", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.Parade))
                .AddButton("reallife_events_test_protest", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.Protest))
                .AddButton("reallife_events_test_run", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.FunRun))
                .AddButton("reallife_events_test_film", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.FilmShoot))
                .AddButton("reallife_events_test_fireworks", () => Events.EventDirector.Instance?.TestEvent(Events.EventKind.Fireworks))
                .AddButton("reallife_events_test_clear", () => Events.EventDirector.Instance?.TestClear())
                .AddSplitter()
                .AddHeader("reallife_social_header")
                .AddToggle(Settings.SocialId, "reallife_social_enabled", Settings.Social,
                    v => Settings.Social = v)
                .AddToggle(Settings.SocialMeetingsId, "reallife_social_meetings", Settings.SocialMeetings,
                    v => Settings.SocialMeetings = v)
                .AddToggle(Settings.SocialRelationshipsId, "reallife_social_relationships", Settings.SocialRelationships,
                    v => Settings.SocialRelationships = v)
                .AddToggle(Settings.SocialOutingsId, "reallife_social_outings", Settings.SocialOutings,
                    v => Settings.SocialOutings = v)
                .AddToggle(Settings.SocialContactId, "reallife_social_contact", Settings.SocialContact,
                    v => Settings.SocialContact = v)
                .AddToggle(Settings.SocialTextsId, "reallife_social_texts", Settings.SocialTexts,
                    v => Settings.SocialTexts = v)
                .AddSlider(Settings.SocialTextsMaxId, "reallife_social_texts_max", 1, 6, Settings.SocialTextsMax,
                    v => Settings.SocialTextsMax = v)
                .AddToggle(Settings.SocialNewspaperId, "reallife_social_newspaper", Settings.SocialNewspaper,
                    v => Settings.SocialNewspaper = v)
                .AddButton("reallife_social_test_meet", () => SocialDirector.Instance?.TestMeet())
                .AddButton("reallife_social_test_person", () => SocialDirector.Instance?.TestPerson())
                .AddButton("reallife_social_test_advance", () => SocialDirector.Instance?.TestAdvance())
                .AddButton("reallife_social_test_call", () => SocialDirector.Instance?.TestCall())
                .AddButton("reallife_social_test_text", () => SocialDirector.Instance?.TestText())
                .AddButton("reallife_social_test_favour", () => SocialDirector.Instance?.TestFavour())
                .AddButton("reallife_social_test_dropby", () => SocialDirector.Instance?.TestDropBy())
                .AddButton("reallife_social_test_mom", () => SocialDirector.Instance?.TestOutingWithMom())
                .AddButton("reallife_social_test_coffee", () => SocialDirector.Instance?.TestOuting(PlanKind.Coffee))
                .AddButton("reallife_social_test_meal", () => SocialDirector.Instance?.TestOuting(PlanKind.Meal))
                .AddButton("reallife_social_test_drinks", () => SocialDirector.Instance?.TestOuting(PlanKind.Drinks))
                .AddButton("reallife_social_test_cinema", () => SocialDirector.Instance?.TestOuting(PlanKind.Cinema))
                .AddButton("reallife_social_test_walk", () => SocialDirector.Instance?.TestOuting(PlanKind.Walk))
                .AddButton("reallife_social_test_drive", () => SocialDirector.Instance?.TestOuting(PlanKind.Drive))
                .AddButton("reallife_social_test_workout", () => SocialDirector.Instance?.TestOuting(PlanKind.Workout))
                .AddButton("reallife_social_test_business", () => SocialDirector.Instance?.TestOuting(PlanKind.Business))
                .AddButton("reallife_social_test_lift", () => SocialDirector.Instance?.TestOuting(PlanKind.Lift))
                .AddButton("reallife_social_test_moving", () => SocialDirector.Instance?.TestOuting(PlanKind.Moving))
                .AddButton("reallife_social_test_paper", () => SocialDirector.Instance?.TestPaper())
                .AddButton("reallife_social_test_stop", () => SocialDirector.Instance?.TestStop())
                .AddSplitter()
                .AddHeader("reallife_travel_header")
                .AddToggle(Settings.HotelsId, "reallife_travel_hotels", Settings.Hotels,
                    v => Settings.Hotels = v)
                .AddToggle(Settings.HolidaysId, "reallife_travel_holidays", Settings.Holidays,
                    v => Settings.Holidays = v)
                .AddToggle(Settings.TravelExtrasId, "reallife_travel_extras", Settings.TravelExtras,
                    v => Settings.TravelExtras = v)
                .AddButton("reallife_travel_test_checkin_budget", () => TravelDirector.Instance?.TestCheckIn(HotelTier.Budget))
                .AddButton("reallife_travel_test_checkin_mid", () => TravelDirector.Instance?.TestCheckIn(HotelTier.MidRange))
                .AddButton("reallife_travel_test_checkin_luxury", () => TravelDirector.Instance?.TestCheckIn(HotelTier.Luxury))
                .AddButton("reallife_travel_test_room", () => TravelDirector.Instance?.TestOpenRoom())
                .AddButton("reallife_travel_test_trip_hamptons", () => TravelDirector.Instance?.TestTrip("hamptons"))
                .AddButton("reallife_travel_test_trip_cruise", () => TravelDirector.Instance?.TestTrip("cruise"))
                .AddButton("reallife_travel_test_trip_ski", () => TravelDirector.Instance?.TestTrip("ski"))
                .AddButton("reallife_travel_test_trip_miami", () => TravelDirector.Instance?.TestTrip("miami"))
                .AddButton("reallife_travel_test_trip_europe", () => TravelDirector.Instance?.TestTrip("europe"))
                .AddButton("reallife_travel_test_trip_tradefair", () => TravelDirector.Instance?.TestTrip("tradefair"))
                .AddButton("reallife_travel_test_return", () => TravelDirector.Instance?.TestReturn())
                .AddButton("reallife_travel_test_clear", () => TravelDirector.Instance?.TestClear())
                .AddSplitter()
                .AddHeader("reallife_garage_header")
                .AddToggle(Settings.GarageId, "reallife_garage_enabled", Settings.Garage,
                    v => Settings.Garage = v)
                .AddToggle(Settings.GarageCosmeticsId, "reallife_garage_cosmetics", Settings.GarageCosmetics,
                    v => Settings.GarageCosmetics = v)
                .AddToggle(Settings.GarageTuningId, "reallife_garage_tuning", Settings.GarageTuning,
                    v => Settings.GarageTuning = v)
                .AddToggle(Settings.GarageUnderglowId, "reallife_garage_underglow", Settings.GarageUnderglow,
                    v => Settings.GarageUnderglow = v)
                .AddToggle(Settings.GaragePoliceId, "reallife_garage_police", Settings.GaragePolice,
                    v => Settings.GaragePolice = v)
                .AddToggle(Settings.TwoWheelersId, "reallife_garage_two_wheelers", Settings.TwoWheelers,
                    v => Settings.TwoWheelers = v)
                .AddButton("reallife_garage_test_shop", () => GarageDirector.Instance?.TestShop())
                .AddButton("reallife_garage_test_random", () => GarageDirector.Instance?.TestRandomBuild())
                .AddButton("reallife_garage_test_restore", () => GarageDirector.Instance?.TestStock())
                .AddButton("reallife_garage_test_vehicles", () => { if (GarageDirector.Instance != null) { GarageSummary.Toggle(); } })
                .AddButton("reallife_garage_test_bicycle", () => GarageDirector.Instance?.TestBike(BikeKind.CityBicycle))
                .AddButton("reallife_garage_test_road_bicycle", () => GarageDirector.Instance?.TestBike(BikeKind.RoadBicycle))
                .AddButton("reallife_garage_test_motorbike", () => GarageDirector.Instance?.TestBike(BikeKind.StandardMotorbike))
                .AddButton("reallife_garage_test_sport_motorbike", () => GarageDirector.Instance?.TestBike(BikeKind.SportMotorbike))
                .AddSplitter()
                .AddJustice()
                .AddSplitter()
                .AddHeader("reallife_licences_header")
                .AddToggle(Settings.DrivingLicenceId, "reallife_lic_opt_required", Settings.DrivingLicence,
                    v => Settings.DrivingLicence = v)
                .AddToggle(Settings.StartWithLicenceId, "reallife_lic_opt_start_with", Settings.StartWithLicence,
                    v => Settings.StartWithLicence = v)
                .AddToggle(Settings.DrivingSchoolId, "reallife_lic_opt_school", Settings.DrivingSchool,
                    v => Settings.DrivingSchool = v)
                .AddToggle(Settings.LicencePointsId, "reallife_lic_opt_points", Settings.LicencePoints,
                    v => Settings.LicencePoints = v)
                .AddToggle(Settings.RoadsideChecksId, "reallife_lic_opt_checks", Settings.RoadsideChecks,
                    v => Settings.RoadsideChecks = v)
                .AddButton("reallife_test_lic_card", () => LicenceDirector.Instance?.ShowCard())
                .AddButton("reallife_test_lic_car", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Car))
                .AddButton("reallife_test_lic_commercial", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Commercial))
                .AddButton("reallife_test_lic_taxi", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Taxi))
                .AddButton("reallife_test_lic_theory", () => LicenceDirector.Instance?.TestTheory())
                .AddButton("reallife_test_lic_practical", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Car))
                .AddButton("reallife_test_lic_practical_van", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Commercial))
                .AddButton("reallife_test_lic_check", () => LicenceDirector.Instance?.TestCheck())
                .AddButton("reallife_test_lic_points", () => LicenceDirector.Instance?.TestPoints())
                .AddSplitter()
                .AddHeader("reallife_civic_header")
                .AddToggle(Settings.CivicId, "reallife_civic_enabled", Settings.Civic,
                    v => Settings.Civic = v)
                .AddToggle(Settings.CivicElectionsId, "reallife_civic_elections", Settings.CivicElections,
                    v => Settings.CivicElections = v)
                .AddToggle(Settings.CivicConstructionId, "reallife_civic_construction", Settings.CivicConstruction,
                    v => Settings.CivicConstruction = v)
                .AddButton("reallife_civic_test_campaign", () => CivicDirector.Instance?.TestCampaign())
                .AddButton("reallife_civic_test_rally", () => CivicDirector.Instance?.TestRally())
                .AddButton("reallife_civic_test_election", () => CivicDirector.Instance?.TestElectionDay())
                .AddButton("reallife_civic_test_policy", () => CivicDirector.Instance?.TestPolicy())
                .AddButton("reallife_civic_test_revert", () => CivicDirector.Instance?.TestRevert())
                .AddButton("reallife_civic_test_roadworks", () => CivicDirector.Instance?.TestSite(SiteKind.Roadworks))
                .AddButton("reallife_civic_test_scaffold", () => CivicDirector.Instance?.TestSite(SiteKind.Scaffold))
                .AddButton("reallife_civic_test_refit", () => CivicDirector.Instance?.TestSite(SiteKind.Refit))
                .AddButton("reallife_civic_test_utility", () => CivicDirector.Instance?.TestSite(SiteKind.Utility))
                .AddButton("reallife_civic_test_advance", () => CivicDirector.Instance?.TestAdvance())
                .AddButton("reallife_civic_test_clear", () => CivicDirector.Instance?.TestClear())
                .AddSplitter()
                .AddHeader("reallife_family_header")
                .AddToggle(Settings.FamilyId, "reallife_family_enabled", Settings.Family,
                    v => Settings.Family = v)
                .AddToggle(Settings.FamilyAtHomeId, "reallife_family_at_home", Settings.FamilyAtHome,
                    v => Settings.FamilyAtHome = v)
                .AddToggle(Settings.FamilyWeddingId, "reallife_family_wedding_opt", Settings.FamilyWedding,
                    v => Settings.FamilyWedding = v)
                .AddToggle(Settings.FamilyChildrenId, "reallife_family_children", Settings.FamilyChildren,
                    v => Settings.FamilyChildren = v)
                .AddToggle(Settings.FamilyRelativesId, "reallife_family_relatives", Settings.FamilyRelatives,
                    v => Settings.FamilyRelatives = v)
                .AddToggle(Settings.FamilyFrictionId, "reallife_family_friction", Settings.FamilyFriction,
                    v => Settings.FamilyFriction = v)
                .AddDropdown(Settings.FamilyInterestId, "reallife_family_interest",
                    new[] { "reallife_family_interest_anyone", "reallife_family_interest_women", "reallife_family_interest_men" }, Settings.FamilyInterest,
                    v => Settings.FamilyInterest = v)
                .AddSlider(Settings.FamilyYearWeeksId, "reallife_family_year_weeks", 1, 12, Settings.FamilyYearWeeks,
                    v => Settings.FamilyYearWeeks = v)
                .AddButton("reallife_family_test_move_in", () => Family.FamilyDirector.Instance?.TestMoveIn())
                .AddButton("reallife_family_test_propose", () => Family.FamilyDirector.Instance?.TestPropose())
                .AddButton("reallife_family_test_plan", () => Family.FamilyDirector.Instance?.TestPlanWedding())
                .AddButton("reallife_family_test_wedding", () => Family.FamilyDirector.Instance?.TestWedding())
                .AddButton("reallife_family_test_child", () => Family.FamilyDirector.Instance?.TestChild(false))
                .AddButton("reallife_family_test_adopt", () => Family.FamilyDirector.Instance?.TestChild(true))
                .AddButton("reallife_family_test_year", () => Family.FamilyDirector.Instance?.TestYear())
                .AddButton("reallife_family_test_walk", () => Family.FamilyDirector.Instance?.TestWalk())
                .AddButton("reallife_family_test_page", () => Family.FamilyDirector.Instance?.TestPage())
                .AddButton("reallife_family_test_clear", () => Family.FamilyDirector.Instance?.TestClear())
                .AddSplitter()
                .AddControls()
                .AddPerformance();

            // Meta puts the finished page in order (sections, presets, guide, test tools); rows are still added above.
            OptionsService.Register(context.ModId, Meta.OptionsLayout.Organise(context, options));
            log.Info("Real Life Overhaul " + Meta.Changelog.Version + " initialised.");
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            Meta.OptionsLayout.Dispose();
            OptionsService.RemoveModOptions(_context.ModId);
            Keys.Dispose();
            return Task.CompletedTask;
        }
    }

    /// <summary>Loaded each time the city scene loads: gameplay features hook in here.</summary>
    [ModEntryOnCityLoad]
    public class RealLifeOverhaulCity : IModBigAmbitions
    {
        private GameObject _root;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            context = Meta.Dev.Gated(context);   // Info lines only while "Developer diagnostics" is on; warnings and errors always
            ThirdPersonCamera.Logger = context.Logger;
            VisualEnhancer.Logger = context.Logger;
            NearbyInteraction.Logger = context.Logger;
            ChaseCamera.Logger = context.Logger;
            PoiDirection.Logger = context.Logger;
            ModState.Logger = context.Logger;
            LifeEvents.Logger = context.Logger;
            VehicleTow.Logger = context.Logger;
            CrashConsequences.Logger = context.Logger;
            TrafficAccidents.Logger = context.Logger;
            TrafficTools.Logger = context.Logger;
            TrafficEffects.Logger = context.Logger;
            ResponseUnit.Logger = context.Logger;
            SceneCordon.Logger = context.Logger;
            AccidentCast.Logger = context.Logger;
            Shapes.Logger = context.Logger;
            PropLibrary.Logger = context.Logger;
            MapPlaces.Logger = context.Logger;
            MapLinks.Logger = context.Logger;
            PropLibrary.Load(context);   // the modelled props, read in the background; plain shapes until they are there
            PolicePursuit.Logger = context.Logger;
            ActorPool.Logger = context.Logger;
            StreetCrime.Logger = context.Logger;
            CrimeDirector.Logger = context.Logger;
            PoliceAttention.Logger = context.Logger;
            BeatPatrol.Logger = context.Logger;
            PolicePresence.Logger = context.Logger;
            RagdollSystem.Logger = context.Logger;
            PedestrianIncidents.Logger = context.Logger;
            CarStrikes.Logger = context.Logger;
            PlayerKnockdown.Logger = context.Logger;
            PhoneContacts.Logger = context.Logger;
            CityLifeDirector.Logger = context.Logger;
            ClipLibrary.Logger = context.Logger;
            ClipLibrary.Load(context);   // the mod's own animation clips, read in the background
            JobDirector.Logger = context.Logger;
            Law.LawDirector.Logger = context.Logger;
            TransitDirector.Logger = context.Logger;
            CarOwnership.Logger = context.Logger;
            Events.EventDirector.Logger = context.Logger;
            SocialDirector.Logger = context.Logger;
            BusinessDirector.Logger = context.Logger;
            JusticeDirector.Logger = context.Logger;
            Licences.Logger = context.Logger;
            LicenceDirector.Logger = context.Logger;
            CivicDirector.Logger = context.Logger;
            Family.FamilyDirector.Logger = context.Logger;

            _root = new GameObject("RealLifeOverhaul");
            _root.AddComponent<ThirdPersonCamera>();
            _root.AddComponent<VisualEnhancer>();
            _root.AddComponent<NearbyInteraction>();
            _root.AddComponent<ChaseCamera>();
            _root.AddComponent<PoiDirection>();   // the game's place markers, when what they mark is behind the camera
            _root.AddComponent<LifeEvents>();
            _root.AddComponent<CrashConsequences>();
            _root.AddComponent<RagdollSystem>();   // before anyone who throws people down
            _root.AddComponent<ActorPool>();   // before the scenes that put people on the street
            _root.AddComponent<TrafficAccidents>();
            _root.AddComponent<PolicePursuit>();
            _root.AddComponent<StreetCrime>();
            _root.AddComponent<CrimeDirector>();   // the scenes StreetCrime now forwards to
            _root.AddComponent<PoliceAttention>();
            _root.AddComponent<BeatPatrol>();
            _root.AddComponent<PolicePresence>();
            _root.AddComponent<PedestrianIncidents>();
            _root.AddComponent<CarStrikes>();
            _root.AddComponent<PlayerKnockdown>();
            _root.AddComponent<CityLifeDirector>();   // people at work and about their day: its own pool of eight
            _root.AddComponent<JobDirector>();   // jobs the player takes themselves (Scripts/Jobs)
            HomeDirector.Logger = context.Logger;
            _root.AddComponent<HomeDirector>();   // home life and health (Scripts/Home); after LifeEvents and the city life director, whose clip player it shares
            _root.AddComponent<Law.LawDirector>();   // precincts, custody, the record and reputation (Scripts/Law)
            _root.AddComponent<TransitDirector>();   // bus stops, buses and the subway journey (Scripts/Transport)
            _root.AddComponent<CarOwnership>();   // servicing, wear, breakdowns, insurance, the fuel gauge (Scripts/Transport)
            Leisure.LeisureDirector.Logger = context.Logger;
            _root.AddComponent<Leisure.LeisureDirector>();   // things to do for fun, the clothes rack, night life, the sound bed, photo mode (Scripts/Leisure)
            _root.AddComponent<Events.EventDirector>();   // fires, weather that matters, street events (Scripts/Events): after CityLifeDirector, whose clip player it uses
            _root.AddComponent<SocialDirector>();   // the people in the player's life, outings, the morning paper (Scripts/Social)
            TravelDirector.Logger = context.Logger;
            _root.AddComponent<TravelDirector>();   // hotels and holidays (Scripts/Travel)
            _root.AddComponent<BusinessDirector>();   // pressure on the player's businesses (Scripts/Business)
            _root.AddComponent<JusticeDirector>();   // courts, lawyers, sentences and prison (Scripts/Justice)
            _root.AddComponent<LicenceDirector>();   // driving licences: the school, the tests, points and what a stop finds (Scripts/Licences)
            _root.AddComponent<CivicDirector>();   // city government, elections and construction sites (Scripts/Civic): its own cast of five
            _root.AddComponent<Family.FamilyDirector>();   // a partner at home, engagement and wedding, children, relatives (Scripts/Family)
            CarTuning.Logger = context.Logger;
            CarMeasure.Logger = context.Logger;
            _root.AddComponent<CarTuning>();
            _root.AddComponent<Speedometer>();
            GarageDirector.Logger = context.Logger;
            GarageShop.Logger = context.Logger;
            GarageCar.Logger = context.Logger;
            GarageTuning.Logger = context.Logger;
            TwoWheelers.Logger = context.Logger;
            BikeMount.Logger = context.Logger;
            BikePlaces.Logger = context.Logger;
            _root.AddComponent<GarageDirector>();   // the custom shop, builds on the player's cars, bicycles and motorbikes (Scripts/Garage)
            PhoneSkin.Logger = context.Logger;
            _root.AddComponent<PhoneSkin>();
            PhoneUiDump.Logger = context.Logger;
            _root.AddComponent<PhoneUiDump>();   // a development aid: its key does nothing unless "Developer diagnostics" is on
            PerfDirector.Logger = context.Logger;
            _root.AddComponent<PerfDirector>();   // performance tweaks for the base game, and the two measurements (their keys are development aids)
            _root.AddComponent<MapLinks>();   // every feature's places on the city map (World/MapLinks reads them; no feature knows of it)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Immersion")
                {
                    context.Logger.Warn("The 'Immersion - RPG Camera' mod is also loaded. Two camera mods fight over the camera: disable one of them.");
                }
            }
            Wiring.Connect(context.Logger, _root);   // every director is there: the bridges between the features are filled in (Scripts/Wiring)
            Meta.MetaCity.Begin(context, _root);   // the check for leftovers, the guide's contact, the welcome panel
            context.Logger.Info("City features started.");
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            Meta.MetaCity.End();
            Wiring.Disconnect();   // every bridge back to its stand-alone default
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            MapPlaces.Shutdown();
            PropLibrary.Dispose();
            return Task.CompletedTask;
        }
    }
}
