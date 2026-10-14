using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using RealLifeOverhaul;
using RealLifeOverhaul.Districts;
using RealLifeOverhaul.Interiors;
using RealLifeOverhaul.Permadeath;
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

        // Two copies switched on (the Workshop item and a ModsLocal folder) are one assembly started twice: the second
        // to start says so and registers nothing, so there is one options page and one set of saved choices in use.
        // (Told apart by the id the game gives each copy: the same copy started again is not a second one.)
        private static string _started;
        private bool _second;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            if (_started != null && _started != context.ModId)
            {
                _second = true;
                context.Logger.Warn("Real Life Overhaul is switched on twice (the Workshop item and a copy in the ModsLocal folder?). This copy does nothing; switch one of them off under Mods in the main menu.");
                return Task.CompletedTask;
            }
            _started = context.ModId;
            IModLogger log = context.Logger;
            context = Meta.Dev.Gated(context);   // Info lines only while "Developer diagnostics" is on (read below; OptionsLayout.Organise applies it)
            _context = context;
            Keys.Logger = context.Logger;
            Settings.Load(context.ModId);
            Economy.Dealership.DealerType.Logger = log;
            Economy.Dealership.DealerType.Register();   // the car dealership's business type, before any save is read (Scripts/Economy/Dealer)
            Meta.Dev.Gated(context);   // (the option has been read now)
            GameMembers.Logger = context.Logger;
            GameConfirm.Logger = context.Logger;
            GameBuild.Start(context.Logger);   // the game's build against the one this version is made for: a line in Player.log, and the notice for an older game (Core/GameBuild)
            Tech.TechTypes.Logger = context.Logger;
            Tech.TechTypes.Register();   // the tech sector's business types, before any save that holds one is loaded (Scripts/Tech/Firms/TechTypes)
            Careers.AcademyType.Logger = context.Logger;
            Careers.AcademyType.Register();   // the player's own training academy, a business type, before any save that holds one is loaded (Scripts/Careers/AcademyType)

            var options = new ModOptions()
                .AddHeader("reallife_camera_header")
                .AddToggle(Settings.CameraEnabledId, "reallife_camera_enabled", Settings.CameraEnabled,
                    v => Settings.CameraEnabled = v)
                .AddToggle(Settings.MouseLookId, "reallife_camera_mouselook_keys", Settings.MouseLook,
                    v => Settings.MouseLook = v)
                .AddSlider(Settings.MouseSensitivityId, "reallife_camera_sensitivity", 1, 20, Settings.MouseSensitivity,
                    v => Settings.MouseSensitivity = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.EnterWithKeyId, "reallife_enter_with_keys", Settings.EnterWithKey,
                    v => Settings.EnterWithKey = v)
                .AddToggle(Settings.CameraIndoorsId, "reallife_camera_indoors", Settings.CameraIndoors,
                    v => Settings.CameraIndoors = v)
                .AddSlider(Settings.CameraPitchId, "reallife_camera_pitch", 5, 70, Settings.CameraPitch,
                    v => Settings.CameraPitch = v, Meta.SettingsTable.SliderValue)
                .AddSlider(Settings.CameraDistanceId, "reallife_camera_distance", 3, 20, Settings.CameraDistance,
                    v => Settings.CameraDistance = v, Meta.SettingsTable.SliderValue)
                .AddSlider(Settings.CameraFovId, "reallife_camera_fov", 30, 90, Settings.CameraFov,
                    v => Settings.CameraFov = v, Meta.SettingsTable.SliderValue)
                .AddSlider(Settings.CameraHeightId, "reallife_camera_height", 5, 25, Settings.CameraHeight,
                    v => Settings.CameraHeight = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.CarCameraId, "reallife_car_camera", Settings.CarCamera,
                    v => Settings.CarCamera = v)
                .AddSlider(Settings.CarCameraDistanceId, "reallife_car_camera_distance", 4, 20, Settings.CarCameraDistance,
                    v => Settings.CarCameraDistance = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.CarTuningId, "reallife_car_tuning", Settings.CarTuning,
                    v => Settings.CarTuning = v)
                .AddSlider(Settings.CarTopSpeedId, "reallife_car_top_speed", 70, 150, Settings.CarTopSpeed,
                    v => Settings.CarTopSpeed = v, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.CarAccelerationId, "reallife_car_acceleration", 70, 150, Settings.CarAcceleration,
                    v => Settings.CarAcceleration = v, Meta.SettingsTable.SliderPercent)
                .AddToggle(Settings.SmoothThrottleId, "reallife_car_smooth_throttle", Settings.SmoothThrottle,
                    v => Settings.SmoothThrottle = v)
                .AddButton("reallife_car_measure", CarMeasure.Arm)
                .AddButton("reallife_camera_test_pretend", CameraYield.TestPretend)
                .AddToggle(Settings.SpeedometerId, "reallife_speedometer", Settings.Speedometer,
                    v => Settings.Speedometer = v)
                .AddSlider(Settings.CameraFollowId, "reallife_camera_follow", 0, 10, Settings.CameraFollow,
                    v => Settings.CameraFollow = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.MapPlacesShownId, "reallife_map_places_shown", Settings.MapPlacesShown,
                    v => Settings.MapPlacesShown = v)
                .AddToggle(Settings.SpecialNamesId, "reallife_special_names", Settings.SpecialNames,
                    v => { Settings.SpecialNames = v; SpecialNames.Changed(); })
                .AddSplitter()
                .AddHeader("reallife_visuals_header")
                .AddToggle(Settings.VisualsEnabledId, "reallife_visuals_enabled", Settings.VisualsEnabled,
                    v => Settings.VisualsEnabled = v)
                .AddSlider(Settings.SaturationId, "reallife_visuals_saturation", -50, 50, Settings.Saturation,
                    v => Settings.Saturation = v, Meta.SettingsTable.SliderValue)
                .AddSlider(Settings.ContrastId, "reallife_visuals_contrast", -50, 50, Settings.Contrast,
                    v => Settings.Contrast = v, Meta.SettingsTable.SliderValue)
                .AddSlider(Settings.BloomId, "reallife_visuals_bloom", 0, 100, Settings.Bloom,
                    v => Settings.Bloom = v, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.DetailId, "reallife_visuals_detail", 100, 300, Settings.Detail,
                    v => Settings.Detail = v, Meta.SettingsTable.SliderPercent)
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
                    v => Settings.EventFrequency = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.CrashConsequencesId, "reallife_crash_consequences", Settings.CrashConsequences,
                    v => Settings.CrashConsequences = v)
                .AddToggle(Settings.TrafficAccidentsId, "reallife_traffic_accidents", Settings.TrafficAccidents,
                    v => Settings.TrafficAccidents = v)
                .AddSlider(Settings.AccidentFrequencyId, "reallife_accident_frequency", 1, 10, Settings.AccidentFrequency,
                    v => Settings.AccidentFrequency = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.AccidentNoticesId, "reallife_accident_notices", Settings.AccidentNotices,
                    v => Settings.AccidentNotices = v)
                .AddToggle(Settings.AccidentMarkerId, "reallife_accident_waypoint", Settings.AccidentMarker,
                    v => Settings.AccidentMarker = v)
                .AddToggle(Settings.TowTrucksId, "reallife_tow_trucks", Settings.TowTrucks,
                    v => Settings.TowTrucks = v)
                .AddToggle(Settings.PoliceId, "reallife_police", Settings.Police,
                    v => Settings.Police = v)
                .AddSlider(Settings.PolicePresenceId, "reallife_police_presence", 0, 10, Settings.PolicePresence,
                    v => Settings.PolicePresence = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.PolicePursuitId, "reallife_police_pursuit", Settings.PolicePursuit,
                    v => Settings.PolicePursuit = v)
                .AddToggle(Settings.SirenId, "reallife_siren", Settings.Siren,
                    v => Settings.Siren = v)
                .AddToggle(Settings.PeopleId, "reallife_people", Settings.People,
                    v => Settings.People = v)
                .AddToggle(Settings.OfficersOnPatrolId, "reallife_patrol_officers", Settings.OfficersOnPatrol,
                    v => Settings.OfficersOnPatrol = v)
                .AddSlider(Settings.PatrolFrequencyId, "reallife_patrol_frequency", 1, 10, Settings.PatrolFrequency,
                    v => Settings.PatrolFrequency = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.RagdollsId, "reallife_ragdolls", Settings.Ragdolls,
                    v => Settings.Ragdolls = v)
                .AddToggle(Settings.PlayerKnockdownId, "reallife_player_knockdown", Settings.PlayerKnockdown,
                    v => Settings.PlayerKnockdown = v)
                .AddToggle(Settings.PedestrianIncidentsId, "reallife_ped_incidents", Settings.PedestrianIncidents,
                    v => Settings.PedestrianIncidents = v)
                .AddToggle(Settings.HornReactionsId, "reallife_horn_reactions", Settings.HornReactions,
                    v => Settings.HornReactions = v)
                .AddToggle(Settings.NaturalAccidentsId, "reallife_natural_accidents", Settings.NaturalAccidents,
                    v => Settings.NaturalAccidents = v)
                .AddToggle(Settings.SolidStreetsId, "reallife_solid_streets", Settings.SolidStreets,
                    v => Settings.SolidStreets = v)
                .AddToggle(Settings.PhoneContactsId, "reallife_phone_contacts", Settings.PhoneContacts,
                    v => Settings.PhoneContacts = v)
                .AddButton("reallife_contacts_test_pictures", ContactPictures.TestReport)
                .AddButton("reallife_contacts_test_redraw", ContactPictures.TestRedraw)
                .AddToggle(Settings.PhoneSkinId, "reallife_phone_skin", Settings.PhoneSkin,
                    v => Settings.PhoneSkin = v)
                .AddToggle(Settings.NoticeLookId, "reallife_notice_look", Settings.NoticeLook,
                    v => Settings.NoticeLook = v)
                .AddButton("reallife_test_notice_look", GameUi.NoticeLook.Test)
                .AddButton("reallife_test_event", () => LifeEvents.Instance?.TriggerEventNow())
                .AddButton("reallife_test_accident", () => TrafficAccidents.Instance?.StageAccidentNow())
                .AddButton("reallife_test_accident_minor", () => TrafficAccidents.Instance?.StageAccidentNow(TrafficAccidents.Severity.Minor))
                .AddButton("reallife_test_accident_moderate", () => TrafficAccidents.Instance?.StageAccidentNow(TrafficAccidents.Severity.Moderate))
                .AddButton("reallife_test_accident_severe", () => TrafficAccidents.Instance?.StageAccidentNow(TrafficAccidents.Severity.Severe))
                .AddButton("reallife_test_tow_truck", () => TowService.Instance?.Test())
                .AddButton("reallife_test_ragdoll", () => PedestrianIncidents.Instance?.TestNearest())
                .AddButton("reallife_test_ped_incident", () => PedestrianIncidents.Instance?.StageNow())
                .AddButton("reallife_test_streets_speeder", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.Speeder))
                .AddButton("reallife_test_streets_redlight", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.RedLight))
                .AddButton("reallife_test_streets_drunk", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.Drunk))
                .AddButton("reallife_test_streets_jaywalker", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.Jaywalker))
                .AddButton("reallife_test_streets_hit", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.HitSomeone))
                .AddButton("reallife_test_streets_crash", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.Crash))
                .AddButton("reallife_test_streets_patrol", () => RecklessDrivers.Instance?.Test(RecklessDrivers.TestKind.Patrol))
                .AddButton("reallife_test_solid_room", () => SolidStreets.Instance?.Test(SolidStreets.TestKind.Room))
                .AddButton("reallife_test_solid_close", () => SolidStreets.Instance?.Test(SolidStreets.TestKind.Close))
                .AddButton("reallife_test_solid_dawdle", () => SolidStreets.Instance?.Test(SolidStreets.TestKind.Dawdle))
                .AddButton("reallife_test_solid_late", () => SolidStreets.Instance?.Test(SolidStreets.TestKind.Late))
                .AddButton("reallife_test_solid_crowd", () => SolidStreets.Instance?.Test(SolidStreets.TestKind.Crowd))
                .AddButton("reallife_test_solid_senses", () => SolidStreets.Instance?.Test(SolidStreets.TestKind.Senses))
                .AddSplitter()
                .AddHeader("reallife_crime_header")
                .AddToggle(Settings.StreetCrimeId, "reallife_crime_enabled", Settings.StreetCrime,
                    v => Settings.StreetCrime = v)
                .AddSlider(Settings.CrimeFrequencyId, "reallife_crime_frequency_all", 1, 10, Settings.CrimeFrequency,
                    v => Settings.CrimeFrequency = v, Meta.SettingsTable.SliderValue)
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
                    v => Settings.CityDensity = v, Meta.SettingsTable.SliderValue)
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
                    v => Settings.JobPay = v, Meta.SettingsTable.SliderPercent)
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
                .AddToggle(Settings.CareersStudyId, "reallife_study_enabled", Settings.CareersStudy,
                    v => Settings.CareersStudy = v)
                .AddButton("reallife_study_test_class", () => Careers.CareersDirector.Instance?.TestClass())
                .AddButton("reallife_study_test_exam", () => Careers.CareersDirector.Instance?.TestExam())
                .AddButton("reallife_study_test_skills", () => Careers.CareersDirector.Instance?.TestSkills())
                .AddToggle(Settings.CareersDegreesId, "reallife_degrees_enabled", Settings.CareersDegrees,
                    v => Settings.CareersDegrees = v)
                .AddButton("reallife_degrees_test_graduate", () => Careers.CareersDirector.Instance?.TestGraduate())
                .AddToggle(Settings.CareersFamilyId, "reallife_family_study_enabled", Settings.CareersFamily,
                    v => Settings.CareersFamily = v)
                .AddButton("reallife_family_test_tutor", () => Careers.CareersDirector.Instance?.TestTutor())
                .AddToggle(Settings.CareersJobsId, "reallife_work_enabled", Settings.CareersJobs,
                    v => Settings.CareersJobs = v)
                .AddButton("reallife_work_test_offer", () => Careers.CareersDirector.Instance?.TestOffer())
                .AddButton("reallife_work_test_day", () => Careers.CareersDirector.Instance?.TestWorkDay())
                .AddButton("reallife_work_test_vacancies", () => Careers.CareersDirector.Instance?.TestVacancies())
                .AddToggle(Settings.CareersLifeId, "reallife_life_enabled", Settings.CareersLife,
                    v => Settings.CareersLife = v)
                .AddButton("reallife_life_test_review", () => Careers.CareersDirector.Instance?.TestReview())
                .AddButton("reallife_life_test_matter", () => Careers.CareersDirector.Instance?.TestMatter())
                .AddButton("reallife_life_test_approach", () => Careers.CareersDirector.Instance?.TestApproach())
                .AddToggle(Settings.CareersOwnId, "reallife_own_enabled", Settings.CareersOwn,
                    v => Settings.CareersOwn = v)
                .AddButton("reallife_own_test_contract", () => Careers.CareersDirector.Instance?.TestContract())
                .AddButton("reallife_own_test_found", () => Careers.CareersDirector.Instance?.TestFound())
                .AddSplitter()
                .AddHeader("reallife_business_header")
                .AddToggle(Settings.BusinessId, "reallife_business_enabled", Settings.Business,
                    v => Settings.Business = v)
                .AddSlider(Settings.BusinessFrequencyId, "reallife_business_frequency", 1, 10, Settings.BusinessFrequency,
                    v => Settings.BusinessFrequency = v, Meta.SettingsTable.SliderValue)
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
                .AddInteriors()   // interiors and decor that matter (Scripts/Interiors)
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
                .AddButton("reallife_home_test_cook_panel", () => HomeDirector.Instance?.TestRoutine(HomeWish.Cook))
                .AddButton("reallife_test_home_cook", () => HomeDirector.Instance?.TestRoutine(HomeWish.CookFirst))
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
                .AddToggle(Settings.LawStationFurnitureId, "reallife_law_station_furniture", Settings.LawStationFurniture,
                    v => Settings.LawStationFurniture = v)
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
                .AddButton("reallife_panel_test_beside", GameUi.WorldPanel.TestBeside)
                .AddToggle(Settings.BusesId, "reallife_transport_buses", Settings.Buses,
                    v => Settings.Buses = v)
                .AddToggle(Settings.TransitPeopleId, "reallife_transport_people", Settings.TransitPeople,
                    v => Settings.TransitPeople = v)
                .AddToggle(Settings.SubwayJourneyId, "reallife_transport_subway", Settings.SubwayJourney,
                    v => Settings.SubwayJourney = v)
                .AddToggle(Settings.TaxiCallId, "reallife_transport_taxi", Settings.TaxiCall,
                    v => { Settings.TaxiCall = v; TaxiDirector.OptionChanged(); })
                .AddToggle(Settings.CarOwnershipId, "reallife_transport_cars", Settings.CarOwnership,
                    v => Settings.CarOwnership = v)
                .AddToggle(Settings.CarEasyId, "reallife_transport_cars_easy", Settings.CarEasy,
                    v => Settings.CarEasy = v)
                .AddButton("reallife_test_transit_stop", () => TransitDirector.Instance?.TestDress())
                .AddButton("reallife_test_transit_bus", () => TransitDirector.Instance?.TestBus())
                .AddButton("reallife_test_transit_ride", () => TransitDirector.Instance?.TestRide())
                .AddButton("reallife_test_transit_subway", () => TransitDirector.Instance?.TestSubway())
                .AddButton("reallife_test_taxi_call", () => TaxiDirector.Instance?.TestCall())
                .AddButton("reallife_test_taxi_bring", () => TaxiDirector.Instance?.TestBring())
                .AddButton("reallife_test_taxi_cancel", () => TaxiDirector.Instance?.TestCancel())
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
                    v => Settings.AmbientBirds = v, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.AmbientNightId, "reallife_leisure_opt_ambient_night", 0, 100, Settings.AmbientNight,
                    v => Settings.AmbientNight = v, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.AmbientCityId, "reallife_leisure_opt_ambient_city", 0, 100, Settings.AmbientCity,
                    v => Settings.AmbientCity = v, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.AmbientCrowdId, "reallife_leisure_opt_ambient_crowd", 0, 100, Settings.AmbientCrowd,
                    v => Settings.AmbientCrowd = v, Meta.SettingsTable.SliderPercent)
                .AddToggle(Settings.RadioHudId, "reallife_leisure_opt_radio_hud", Settings.RadioHud,
                    v => Settings.RadioHud = v)
                .AddToggle(Settings.PhotoModeId, "reallife_leisure_opt_photo", Settings.PhotoMode,
                    v => Settings.PhotoMode = v)
                .AddButton("reallife_leisure_test_chess", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Chess))
                .AddButton("reallife_leisure_test_fishing", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Fishing))
                .AddButton("reallife_leisure_test_fishing_near", () => Leisure.LeisureDirector.Instance?.TestFishNearest())
                .AddButton("reallife_leisure_test_dance", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Dance))
                .AddButton("reallife_leisure_test_drinks", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Drinks))
                .AddButton("reallife_leisure_test_sights", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Sightseeing))
                .AddButton("reallife_leisure_test_rack", () => Leisure.LeisureDirector.Instance?.TestStart(Leisure.LeisureKind.Rack))
                .AddButton("reallife_leisure_test_wardrobe", () => Leisure.LeisureDirector.Instance?.TestWardrobe())
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
                    v => Settings.FireFrequency = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.WeatherMattersId, "reallife_events_weather", Settings.WeatherMatters,
                    v => Settings.WeatherMatters = v)
                .AddSlider(Settings.WeatherFrequencyId, "reallife_events_weather_frequency", 1, 10, Settings.WeatherFrequency,
                    v => Settings.WeatherFrequency = v, Meta.SettingsTable.SliderValue)
                .AddToggle(Settings.StreetEventsId, "reallife_events_street", Settings.StreetEvents,
                    v => Settings.StreetEvents = v)
                .AddSlider(Settings.StreetEventFrequencyId, "reallife_events_street_frequency", 1, 10, Settings.StreetEventFrequency,
                    v => Settings.StreetEventFrequency = v, Meta.SettingsTable.SliderValue)
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
                    v => Settings.SocialTextsMax = v, Meta.SettingsTable.SliderValue)
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
                .AddButton("reallife_social_test_stop", () => SocialDirector.Instance?.TestStop())
                .AddSplitter()
                .AddHeader("reallife_ledger_header")
                .AddToggle(Settings.SocialNewspaperId, "reallife_social_newspaper", Settings.SocialNewspaper,
                    v => Settings.SocialNewspaper = v)
                .AddToggle(Settings.LedgerKiosksId, "reallife_ledger_opt_kiosks", Settings.LedgerKiosks,
                    v => Settings.LedgerKiosks = v)
                .AddToggle(Settings.LedgerOnlineId, "reallife_ledger_opt_online", Settings.LedgerOnline,
                    v => Settings.LedgerOnline = v)
                .AddToggle(Settings.LedgerOnlineNotesId, "reallife_ledger_opt_online_notes", Settings.LedgerOnlineNotes,
                    v => Settings.LedgerOnlineNotes = v)
                .AddButton("reallife_ledger_test_open", () => Ledger.LedgerDirector.Instance?.TestOpen())
                .AddButton("reallife_ledger_test_online", () => Ledger.LedgerDirector.Instance?.TestOnline())
                .AddButton("reallife_social_test_paper", () =>
                {
                    SocialDirector.Instance?.TestPaper();   // the city's paper written again from its log...
                    Ledger.LedgerDirector.Instance?.TestDeliver();   // ...printed as the City Ledger, a copy at the door
                })
                .AddButton("reallife_ledger_test_kiosk", () => Ledger.LedgerDirector.Instance?.TestKiosk())
                .AddButton("reallife_ledger_test_find", () => Ledger.LedgerDirector.Instance?.TestFindKiosks())
                .AddButton("reallife_ledger_test_back", () => Ledger.LedgerDirector.Instance?.TestBack())
                .AddSplitter()
                .AddHeader("reallife_flare_header")
                .AddToggle(Settings.SocialMediaId, "reallife_flare_enabled", Settings.SocialMedia,
                    v => Settings.SocialMedia = v)
                .AddToggle(Settings.SocialMediaNoticesId, "reallife_flare_notices", Settings.SocialMediaNotices,
                    v => Settings.SocialMediaNotices = v)
                .AddButton("reallife_flare_test_drafts", () => Flare.FlareDirector.Instance?.TestDrafts())
                .AddButton("reallife_flare_test_day", () => Flare.FlareDirector.Instance?.TestDay())
                .AddButton("reallife_flare_test_followers", () => Flare.FlareDirector.Instance?.TestFollowers())
                .AddButton("reallife_flare_test_viral", () => Flare.FlareDirector.Instance?.TestViral())
                .AddButton("reallife_flare_test_month", () => Flare.FlareDirector.Instance?.TestMonth())
                .AddButton("reallife_flare_test_arrival", () => Flare.FlareDirector.Instance?.TestArrival())
                .AddButton("reallife_flare_test_inf_scandal", () => Flare.FlareDirector.Instance?.TestInfluencerScandal())
                .AddButton("reallife_flare_test_online", () => Flare.FlareDirector.Instance?.TestOnline())
                .AddButton("reallife_flare_test_city", () => Flare.FlareDirector.Instance?.TestCity())
                .AddButton("reallife_flare_test_back", () => Flare.FlareDirector.Instance?.TestBack())
                .AddSplitter()
                .AddHeader("reallife_fame_header")
                .AddToggle(Settings.StandingId, "reallife_fame_enabled", Settings.Standing,
                    v => Settings.Standing = v)
                .AddToggle(Settings.StandingStreetId, "reallife_fame_street", Settings.StandingStreet,
                    v => Settings.StandingStreet = v)
                .AddToggle(Settings.StandingPressId, "reallife_fame_press", Settings.StandingPress,
                    v => Settings.StandingPress = v)
                .AddToggle(Settings.StandingCorruptionId, "reallife_fame_corruption", Settings.StandingCorruption,
                    v => Settings.StandingCorruption = v)
                .AddButton("reallife_fame_test_day", () => Fame.FameDirector.Instance?.TestDay())
                .AddButton("reallife_fame_test_street", () => Fame.FameDirector.Instance?.TestStreet())
                .AddButton("reallife_fame_test_known", () => Fame.FameDirector.Instance?.TestKnown())
                .AddButton("reallife_fame_test_reporter", () => Fame.FameDirector.Instance?.TestReporter())
                .AddButton("reallife_fame_test_show", () => Fame.FameDirector.Instance?.TestShow())
                .AddButton("reallife_fame_test_promises", () => Fame.FameDirector.Instance?.TestPromises())
                .AddButton("reallife_fame_test_request", () => Fame.FameDirector.Instance?.TestRequest())
                .AddButton("reallife_fame_test_evening", () => Fame.FameDirector.Instance?.TestEvening())
                .AddButton("reallife_fame_test_bribe", () => Fame.FameDirector.Instance?.TestBribe())
                .AddButton("reallife_fame_test_council", () => Fame.FameDirector.Instance?.TestCouncil())
                .AddButton("reallife_fame_test_back", () => Fame.FameDirector.Instance?.TestBack())
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
                .AddButton("reallife_test_lic_motorcycle", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Motorcycle))
                .AddButton("reallife_test_lic_theory", () => LicenceDirector.Instance?.TestTheory())
                .AddButton("reallife_test_lic_practical", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Car))
                .AddButton("reallife_test_lic_practical_van", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Commercial))
                .AddButton("reallife_test_lic_check", () => LicenceDirector.Instance?.TestCheck())
                .AddButton("reallife_test_lic_points", () => LicenceDirector.Instance?.TestPoints())
                .AddSplitter()
                .AddHeader("reallife_planner_header")
                .AddToggle(Settings.PlannerId, "reallife_planner_enabled", Settings.Planner,
                    v => Settings.Planner = v)
                .AddToggle(Settings.PlannerRemindersId, "reallife_planner_reminders", Settings.PlannerReminders,
                    v => Settings.PlannerReminders = v)
                .AddButton("reallife_planner_test_fill", () => Planner.PlannerDirector.Instance?.TestFill())
                .AddButton("reallife_planner_test_clear", () => Planner.PlannerDirector.Instance?.TestClear())
                .AddSplitter()
                .AddHeader("reallife_econ_header")
                .AddToggle(Settings.EconomyId, "reallife_econ_enabled", Settings.Economy,
                    v => Settings.Economy = v)
                .AddToggle(Settings.EconomyProductsId, "reallife_econ_products", Settings.EconomyProducts,
                    v => Settings.EconomyProducts = v)
                .AddToggle(Settings.EconomySharkId, "reallife_econ_shark", Settings.EconomyShark,
                    v => Settings.EconomyShark = v)
                .AddToggle(Settings.EconomyConsequencesId, "reallife_econ_consequences", Settings.EconomyConsequences,
                    v => Settings.EconomyConsequences = v)
                .AddDropdown(Settings.EconomyLevelId, "reallife_econ_level",
                    new[] { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" }, Settings.EconomyLevel,
                    v => Settings.EconomyLevel = v)
                .AddButton("reallife_econ_test_report", () => Economy.EconomyDirector.Instance?.TestReport())
                .AddButton("reallife_econ_test_missed", () => Economy.EconomyDirector.Instance?.TestMissed())
                .AddButton("reallife_econ_test_on_time", () => Economy.EconomyDirector.Instance?.TestOnTime())
                .AddButton("reallife_econ_test_set_560", () => Economy.EconomyDirector.Instance?.TestSet(560))
                .AddButton("reallife_econ_test_set_600", () => Economy.EconomyDirector.Instance?.TestSet(600))
                .AddButton("reallife_econ_test_set_760", () => Economy.EconomyDirector.Instance?.TestSet(760))
                .AddToggle(Settings.EconomyBanksId, "reallife_bank_enabled", Settings.EconomyBanks,
                    v => Settings.EconomyBanks = v)
                .AddButton("reallife_bank_test_check", () => Economy.BanksDirector.Instance?.TestCheck())
                .AddButton("reallife_bank_test_open", () => Economy.BanksDirector.Instance?.TestOpen())
                .AddButton("reallife_econ_test_clear", () => Economy.EconomyDirector.Instance?.TestClear())
                .AddButton("reallife_econ_test_restore", () => Economy.EconomyDirector.Instance?.TestRestore())
                .AddButton("reallife_econ_test_open", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestOpen()))
                .AddButton("reallife_econ_test_miss_1", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestMiss(1)))
                .AddButton("reallife_econ_test_miss_3", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestMiss(3)))
                .AddButton("reallife_econ_test_collections", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestCollections()))
                .AddButton("reallife_econ_test_repossess", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestRepossess()))
                .AddButton("reallife_econ_test_lender", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestLenderText()))
                .AddButton("reallife_econ_test_ways", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestWaysOut()))
                .AddButton("reallife_econ_test_back", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestBack()))
                .AddHeader("reallife_macro_header")
                .AddToggle(Settings.EconomyCityId, "reallife_macro_enabled", Settings.EconomyCity,
                    v => Settings.EconomyCity = v)
                .AddDropdown(Settings.EconomyCityLevelId, "reallife_macro_level",
                    new[] { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" }, Settings.EconomyCityLevel,
                    v => Settings.EconomyCityLevel = v)
                .AddButton("reallife_macro_test_show", () => Economy.CityEconomyDirector.Instance?.TestShow())
                .AddButton("reallife_macro_test_month", () => Economy.CityEconomyDirector.Instance?.TestMonth())
                .AddButton("reallife_macro_test_boom", () => Economy.CityEconomyDirector.Instance?.TestCycle(Economy.CycleState.Boom))
                .AddButton("reallife_macro_test_steady", () => Economy.CityEconomyDirector.Instance?.TestCycle(Economy.CycleState.Steady))
                .AddButton("reallife_macro_test_slowdown", () => Economy.CityEconomyDirector.Instance?.TestCycle(Economy.CycleState.Slowdown))
                .AddButton("reallife_macro_test_recession", () => Economy.CityEconomyDirector.Instance?.TestCycle(Economy.CycleState.Recession))
                .AddButton("reallife_macro_test_shortage", () => Economy.CityEconomyDirector.Instance?.TestShock(true))
                .AddButton("reallife_macro_test_glut", () => Economy.CityEconomyDirector.Instance?.TestShock(false))
                .AddButton("reallife_macro_test_rate_up", () => Economy.CityEconomyDirector.Instance?.TestRate(Economy.MacroCatalogue.QuartersPerPoint))
                .AddButton("reallife_macro_test_rate_down", () => Economy.CityEconomyDirector.Instance?.TestRate(-Economy.MacroCatalogue.QuartersPerPoint))
                .AddButton("reallife_macro_test_district_up", () => Economy.CityEconomyDirector.Instance?.TestDistrict(100))
                .AddButton("reallife_macro_test_district_down", () => Economy.CityEconomyDirector.Instance?.TestDistrict(-100))
                .AddButton("reallife_macro_test_back", () => Economy.CityEconomyDirector.Instance?.TestBack())
                .AddToggle(Settings.VenturesInvestorsId, "reallife_ven_investors", Settings.VenturesInvestors,
                    v => Settings.VenturesInvestors = v)
                .AddDropdown(Settings.VenturesLevelId, "reallife_ven_level",
                    new[] { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" }, Settings.VenturesLevel,
                    v => Settings.VenturesLevel = v)
                .AddButton("reallife_ven_test_offer", () => Economy.Ventures.VenturesDirector.Instance?.TestOffer())
                .AddButton("reallife_ven_test_pitch_angel", () => Economy.Ventures.VenturesDirector.Instance?.TestPitch((int)Economy.Ventures.InvestorKind.Angel))
                .AddButton("reallife_ven_test_pitch_tycoon", () => Economy.Ventures.VenturesDirector.Instance?.TestPitch((int)Economy.Ventures.InvestorKind.Tycoon))
                .AddButton("reallife_ven_test_pitch_firm", () => Economy.Ventures.VenturesDirector.Instance?.TestPitch((int)Economy.Ventures.InvestorKind.Firm))
                .AddButton("reallife_ven_test_list", () => Economy.Ventures.VenturesDirector.Instance?.TestList())
                .AddButton("reallife_ven_test_call", () => Economy.Ventures.VenturesDirector.Instance?.TestCall())
                .AddButton("reallife_ven_test_back", () => Economy.Ventures.VenturesDirector.Instance?.TestBack())
                .AddHeader("reallife_mkt_header")
                .AddToggle(Settings.MarketsId, "reallife_mkt_enabled", Settings.Markets,
                    v => Settings.Markets = v)
                .AddToggle(Settings.MarketsMarginId, "reallife_mkt_opt_margin", Settings.MarketsMargin,
                    v => Settings.MarketsMargin = v)
                .AddToggle(Settings.MarketsListingId, "reallife_lst_opt", Settings.MarketsListing,
                    v => Settings.MarketsListing = v)
                .AddButton("reallife_mkt_test_open", () => Markets.MarketDirector.Instance?.TestOpen())
                .AddButton("reallife_mkt_test_crisis", () => Markets.MarketDirector.Instance?.TestNews(true))
                .AddButton("reallife_mkt_test_windfall", () => Markets.MarketDirector.Instance?.TestNews(false))
                .AddButton("reallife_mkt_test_bid", () => Markets.MarketDirector.Instance?.TestBid())
                .AddButton("reallife_lst_test_list", () => Markets.MarketDirector.Instance?.TestListing())
                .AddButton("reallife_lst_test_results", () => Markets.MarketDirector.Instance?.TestOwnResults())
                .AddButton("reallife_lst_test_meeting", () => Markets.MarketDirector.Instance?.TestOwnMeeting())
                .AddButton("reallife_lst_test_activist", () => Markets.MarketDirector.Instance?.TestOwnActivist())
                .AddButton("reallife_lst_test_bid", () => Markets.MarketDirector.Instance?.TestOwnBid())
                .AddButton("reallife_lst_test_off", () => Markets.MarketDirector.Instance?.TestOwnOff())
                .AddHeader("reallife_co_header")
                .AddToggle(Settings.EconomyCompanyId, "reallife_co_enabled", Settings.EconomyCompany,
                    v => Settings.EconomyCompany = v)
                .AddToggle(Settings.EconomyCompanyStaffId, "reallife_co_opt_staff", Settings.EconomyCompanyStaff,
                    v => Settings.EconomyCompanyStaff = v)
                .AddToggle(Settings.EconomyCompanyCoverId, "reallife_co_opt_cover", Settings.EconomyCompanyCover,
                    v => Settings.EconomyCompanyCover = v)
                .AddToggle(Settings.EconomyCompanyMarketingId, "reallife_co_opt_marketing", Settings.EconomyCompanyMarketing,
                    v => Settings.EconomyCompanyMarketing = v)
                .AddToggle(Settings.EconomyCompanyTaxId, "reallife_co_opt_tax", Settings.EconomyCompanyTax,
                    v => Settings.EconomyCompanyTax = v)
                .AddToggle(Settings.EconomyCompanyHideId, "reallife_co_opt_hide", Settings.EconomyCompanyHide,
                    v => Settings.EconomyCompanyHide = v)
                .AddToggle(Settings.EconomyCompanyLicencesId, "reallife_co_opt_licences", Settings.EconomyCompanyLicences,
                    v => Settings.EconomyCompanyLicences = v)
                .AddToggle(Settings.EconomyRotaId, "reallife_rota_enabled", Settings.EconomyRota,
                    v => Settings.EconomyRota = v)
                .AddToggle(Settings.EconomyCleaningId, "reallife_svc_cleaning_enabled", Settings.EconomyCleaning,
                    v => Settings.EconomyCleaning = v)
                .AddToggle(Settings.EconomySaleId, "reallife_svc_sale_enabled", Settings.EconomySale,
                    v => Settings.EconomySale = v)
                .AddToggle(Settings.EconomyBankId, "reallife_ob_enabled", Settings.EconomyBank,
                    v => Settings.EconomyBank = v)
                .AddDropdown(Settings.EconomyCompanyLevelId, "reallife_co_level",
                    new[] { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" }, Settings.EconomyCompanyLevel,
                    v => Settings.EconomyCompanyLevel = v)
                .AddButton("reallife_co_test_raise", () => Economy.CompanyDirector.Instance?.TestRaise())
                .AddButton("reallife_co_test_offer", () => Economy.CompanyDirector.Instance?.TestOffer())
                .AddButton("reallife_co_test_strike", () => Economy.CompanyDirector.Instance?.TestStrike())
                .AddButton("reallife_co_test_claim", () => Economy.CompanyDirector.Instance?.TestFireClaim())
                .AddButton("reallife_co_test_campaign", () => Economy.CompanyDirector.Instance?.TestCampaign())
                .AddButton("reallife_co_test_return", () => Economy.CompanyDirector.Instance?.TestReturn())
                .AddButton("reallife_co_test_audit", () => Economy.CompanyDirector.Instance?.TestAudit())
                .AddButton("reallife_co_test_licence", () => Economy.CompanyDirector.Instance?.TestLicence())
                .AddButton("reallife_co_test_back", () => Economy.CompanyDirector.Instance?.TestBack())
                .AddButton("reallife_rota_test_open", () => Economy.CompanyDirector.Instance?.TestRotaOpen())
                .AddButton("reallife_rota_test_plans", () => Economy.CompanyDirector.Instance?.TestRotaPlans())
                .AddButton("reallife_rota_test_fill", () => Economy.CompanyDirector.Instance?.TestRotaFillAll())
                .AddButton("reallife_rota_test_back", () => Economy.CompanyDirector.Instance?.TestRotaBackAll())
                .AddButton("reallife_ob_test_found", () => Economy.OwnBankDirector.Instance?.TestFound())
                .AddButton("reallife_ob_test_month", () => Economy.OwnBankDirector.Instance?.TestMonth())
                .AddButton("reallife_ob_test_hard", () => Economy.OwnBankDirector.Instance?.TestHardMonth())
                .AddButton("reallife_ob_test_run", () => Economy.OwnBankDirector.Instance?.TestRun())
                .AddButton("reallife_ob_test_deadline", () => Economy.OwnBankDirector.Instance?.TestDeadline())
                .AddButton("reallife_ob_test_fund", () => Economy.OwnBankDirector.Instance?.TestFund())
                .AddButton("reallife_ob_test_back", () => Economy.OwnBankDirector.Instance?.TestBack())
                .AddHeader("reallife_estate_header")
                .AddToggle(Settings.EstateId, "reallife_estate_enabled", Settings.Estate,
                    v => Settings.Estate = v)
                .AddToggle(Settings.EstateHotelId, "reallife_estate_opt_hotel", Settings.EstateHotel,
                    v => Settings.EstateHotel = v)
                .AddToggle(Settings.EstateHireId, "reallife_estate_opt_hire", Settings.EstateHire,
                    v => Settings.EstateHire = v)
                .AddSlider(Settings.EstateRentId, "reallife_estate_opt_rent", 50, 300, Settings.EstateRent, v => Settings.EstateRent = v, Meta.SettingsTable.SliderPercent)
                .AddButton("reallife_estate_test_applicant", () => Economy.EstateDirector.Instance?.TestApplicant())
                .AddButton("reallife_estate_test_arrears", () => Economy.EstateDirector.Instance?.TestArrears())
                .AddButton("reallife_estate_test_works", () => Economy.EstateDirector.Instance?.TestWorks())
                .AddButton("reallife_estate_test_offer", () => Economy.EstateDirector.Instance?.TestOffer())
                .AddButton("reallife_estate_test_instruction", () => Economy.EstateDirector.Instance?.TestInstruction())
                .AddButton("reallife_estate_test_night", () => Economy.EstateDirector.Instance?.TestHotelNight())
                .AddButton("reallife_estate_test_group", () => Economy.EstateDirector.Instance?.TestGroup())
                .AddButton("reallife_estate_test_hire", () => Economy.EstateDirector.Instance?.TestHire())
                .AddButton("reallife_estate_test_month", () => Economy.EstateDirector.Instance?.TestMonth())
                .AddButton("reallife_estate_test_back", () => Economy.EstateDirector.Instance?.TestBack())
                .AddHeader("reallife_talent_header")
                .AddToggle(Settings.TechPeopleId, "reallife_talent_enabled", Settings.TechPeople,
                    v => Settings.TechPeople = v)
                .AddButton("reallife_talent_test_review", () => Tech.TechDirector.Instance?.TestReview())
                .AddButton("reallife_talent_test_offer", () => Tech.TechDirector.Instance?.TestOffer())
                .AddButton("reallife_talent_test_course", () => Tech.TechDirector.Instance?.TestCourse())
                .AddToggle(Settings.TechJobsId, "reallife_hire_enabled", Settings.TechJobs,
                    v => Settings.TechJobs = v)
                .AddButton("reallife_hire_test_applicants", () => Tech.TechDirector.Instance?.TestApplicants())
                .AddButton("reallife_hire_test_market", () => Tech.TechDirector.Instance?.TestMarketDay())
                .AddButton("reallife_hire_test_search", () => Tech.TechDirector.Instance?.TestSearch())
                .AddToggle(Settings.TechSectorId, "reallife_tech_enabled", Settings.TechSector,
                    v => Settings.TechSector = v)
                .AddButton("reallife_tech_test_offer", () => Tech.TechDirector.Instance?.TestContractOffer())
                .AddButton("reallife_tech_test_outage", () => Tech.TechDirector.Instance?.TestOutage())
                .AddButton("reallife_tech_test_breach", () => Tech.TechDirector.Instance?.TestBreach())
                .AddButton("reallife_infra_test_down", () => Tech.TechDirector.Instance?.TestCentreDown())
                .AddButton("reallife_infra_test_price_war", () => Tech.TechDirector.Instance?.TestPriceWar())
                .AddToggle(Settings.TechStartupsId, "reallife_vc_enabled", Settings.TechStartups,
                    v => Settings.TechStartups = v)
                .AddButton("reallife_vc_test_pitch", () => Tech.TechDirector.Instance?.TestPitch())
                .AddButton("reallife_vc_test_sale", () => Tech.TechDirector.Instance?.TestSale())
                .AddButton("reallife_vc_test_board", () => Tech.TechDirector.Instance?.TestBoard())
                .AddToggle(Settings.TechSiteRoomsId, "reallife_sites_rooms", Settings.TechSiteRooms,
                    v => Settings.TechSiteRooms = v)
                .AddToggle(Settings.TechSiteHqId, "reallife_sites_hq", Settings.TechSiteHq,
                    v => Settings.TechSiteHq = v)
                .AddToggle(Settings.TechSiteCloudId, "reallife_sites_cloud", Settings.TechSiteCloud,
                    v => Settings.TechSiteCloud = v)
                .AddToggle(Settings.TechSiteOfficesId, "reallife_sites_offices", Settings.TechSiteOffices,
                    v => Settings.TechSiteOffices = v)
                .AddButton("reallife_sites_test_room", () => Tech.TechSites.Instance?.TestRoom())
                .AddButton("reallife_sites_test_hq", () => Tech.TechSites.Instance?.TestHeadquarters())
                .AddButton("reallife_sites_test_floor", () => Tech.TechSites.Instance?.TestFloor())
                .AddToggle(Settings.EconomyOnlineId, "reallife_online_enabled", Settings.EconomyOnline,
                    v => Settings.EconomyOnline = v)
                .AddButton("reallife_online_test_open", () => Economy.OnlineDirector.Instance?.TestOpen())
                .AddButton("reallife_online_test_orders", () => Economy.OnlineDirector.Instance?.TestOrders())
                .AddButton("reallife_online_test_late", () => Economy.OnlineDirector.Instance?.TestLate())
                .AddButton("reallife_online_test_return", () => Economy.OnlineDirector.Instance?.TestReturn())
                .AddButton("reallife_online_test_day", () => Economy.OnlineDirector.Instance?.TestDay())
                .AddButton("reallife_online_test_back", () => Economy.OnlineDirector.Instance?.TestBack())
                .AddToggle(Settings.EconomyDealerId, "reallife_dealer_enabled", Settings.EconomyDealer,
                    v => Settings.EconomyDealer = v)
                .AddButton("reallife_dealer_test_stock", () => Economy.Dealership.DealerDirector.Instance?.TestStock())
                .AddButton("reallife_dealer_test_customers", () => Economy.Dealership.DealerDirector.Instance?.TestCustomers())
                .AddButton("reallife_dealer_test_auction", () => Economy.Dealership.DealerDirector.Instance?.TestAuction())
                .AddButton("reallife_dealer_test_week", () => Economy.Dealership.DealerDirector.Instance?.TestWeek())
                .AddButton("reallife_dealer_test_back", () => Economy.Dealership.DealerDirector.Instance?.TestBack())
                .AddButton("reallife_svc_test_round", () => Economy.ServicesDirector.Instance?.TestRound())
                .AddButton("reallife_svc_test_contract", () => Economy.ServicesDirector.Instance?.TestContractOffer())
                .AddButton("reallife_svc_test_week", () => Economy.ServicesDirector.Instance?.TestContractWeek())
                .AddButton("reallife_svc_test_offer", () => Economy.ServicesDirector.Instance?.TestSaleOffer())
                .AddButton("reallife_svc_test_sale_paper", () => Economy.ServicesDirector.Instance?.TestSalePaper())
                .AddButton("reallife_svc_test_part", () => Economy.ServicesDirector.Instance?.TestPart())
                .AddHeader("reallife_brand_header")
                .AddToggle(Settings.EconomyBrandsId, "reallife_brand_enabled", Settings.EconomyBrands,
                    v => Settings.EconomyBrands = v)
                .AddButton("reallife_brand_test_make", () => Economy.Brands.BrandsDirector.Instance?.TestMake())
                .AddButton("reallife_brand_test_applicant", () => Economy.Brands.BrandsDirector.Instance?.TestApplicant())
                .AddButton("reallife_brand_test_sign", () => Economy.Brands.BrandsDirector.Instance?.TestSign())
                .AddButton("reallife_brand_test_trouble", () => Economy.Brands.BrandsDirector.Instance?.TestTrouble())
                .AddButton("reallife_brand_test_scandal", () => Economy.Brands.BrandsDirector.Instance?.TestScandal())
                .AddButton("reallife_brand_test_year", () => Economy.Brands.BrandsDirector.Instance?.TestYear())
                .AddButton("reallife_brand_test_back", () => Economy.Brands.BrandsDirector.Instance?.TestBack())
                .AddSplitter()
                .AddHeader("reallife_nav_header")
                .AddToggle(Settings.MinimapId, "reallife_nav_minimap", Settings.Minimap,
                    v => Settings.Minimap = v)
                .AddToggle(Settings.GpsId, "reallife_nav_gps", Settings.Gps,
                    v => Settings.Gps = v)
                .AddToggle(Settings.NavOtherRouteId, "reallife_nav_other_route", Settings.NavOtherRoute,
                    v => Settings.NavOtherRoute = v)
                .AddToggle(Settings.MinimapRotateId, "reallife_nav_rotate", Settings.MinimapRotate,
                    v => Settings.MinimapRotate = v)
                .AddSlider(Settings.MinimapSizeId, "reallife_nav_size", 60, 100, Settings.MinimapSize,
                    v => Settings.MinimapSize = v, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.MinimapZoomId, "reallife_nav_zoom", 50, 200, Settings.MinimapZoom,
                    v => Settings.MinimapZoom = v, Meta.SettingsTable.SliderPercent)
                .AddToggle(Settings.MinimapBaseId, "reallife_nav_base", Settings.MinimapBase,
                    v => Settings.MinimapBase = v)
                .AddButton("reallife_nav_test_station", () => Nav.NavDirector.Instance?.TestStation())
                .AddButton("reallife_nav_test_far", () => Nav.NavDirector.Instance?.TestFar())
                .AddButton("reallife_nav_test_clear", () => Nav.NavDirector.Instance?.TestClear())
                .AddButton("reallife_nav_test_base_alone", () => Nav.NavDirector.Instance?.TestBaseAlone())
                .AddButton("reallife_nav_test_base_marks", () => Nav.NavDirector.Instance?.TestBaseMarks())
                .AddButton("reallife_nav_test_base_again", () => Nav.NavDirector.Instance?.TestBaseAgain())
                .AddSplitter()
                .AddDistrictView()   // the district view on the city map (Scripts/Districts)
                .AddHeader("reallife_civic_header")
                .AddToggle(Settings.CivicId, "reallife_civic_enabled", Settings.Civic,
                    v => Settings.Civic = v)
                .AddToggle(Settings.CivicElectionsId, "reallife_civic_elections", Settings.CivicElections,
                    v => Settings.CivicElections = v)
                .AddToggle(Settings.CivicConstructionId, "reallife_civic_construction", Settings.CivicConstruction,
                    v => Settings.CivicConstruction = v)
                .AddToggle(Settings.CivicOfficeId, "reallife_civic_office_option", Settings.CivicOffice,
                    v => Settings.CivicOffice = v)
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
                .AddButton("reallife_civic_test_office_council", () => CivicDirector.Instance?.TestStand(OfficeRace.Council))
                .AddButton("reallife_civic_test_office_mayor", () => CivicDirector.Instance?.TestStand(OfficeRace.Mayor))
                .AddButton("reallife_civic_test_office_seat", () => CivicDirector.Instance?.TestOffice(OfficeRace.Council))
                .AddButton("reallife_civic_test_office_mayoralty", () => CivicDirector.Instance?.TestOffice(OfficeRace.Mayor))
                .AddButton("reallife_civic_test_office_scene", () => CivicDirector.Instance?.TestScene())
                .AddSplitter()
                .AddHeader("reallife_util_header")
                .AddToggle(Settings.UtilitiesId, "reallife_util_enabled", Settings.Utilities,
                    v => Settings.Utilities = v)
                .AddButton("reallife_util_test_bill", () => Utilities.UtilitiesDirector.Instance?.TestBill())
                .AddButton("reallife_util_test_due", () => Utilities.UtilitiesDirector.Instance?.TestDue())
                .AddButton("reallife_util_test_cut", () => Utilities.UtilitiesDirector.Instance?.TestCutHome())
                .AddButton("reallife_util_test_restore", () => Utilities.UtilitiesDirector.Instance?.TestRestore())
                .AddButton("reallife_util_test_outage", () => Utilities.UtilitiesDirector.Instance?.TestOutage())
                .AddButton("reallife_util_test_network_month", () => Utilities.UtilitiesDirector.Instance?.TestNetworkMonth())
                .AddButton("reallife_util_test_network_built", () => Utilities.UtilitiesDirector.Instance?.TestNetworkBuilt())
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
                    v => Settings.FamilyYearWeeks = v, Meta.SettingsTable.SliderValue)
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
                .AddPermadeath()   // an ending the player chooses, off unless switched on (Scripts/Permadeath)
                .AddRoommates()
                .AddControls()
                .AddPerformance();

            // Meta puts the finished page in order (sections, presets, guide, test tools); rows are still added above.
            OptionsService.Register(context.ModId, Meta.OptionsLayout.Organise(context, options));
            log.Info("Real Life Overhaul " + Meta.Changelog.Version + " initialised.");
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_second)
            {
                _second = false;
                return Task.CompletedTask;   // this copy started nothing
            }
            _started = null;
            if (_context == null)
            {
                return Task.CompletedTask;   // the load did not get as far as registering anything
            }
            Meta.OptionsLayout.Dispose();
            OptionsService.RemoveModOptions(_context.ModId);
            Keys.Dispose();
            EmpireView.Release();   // Empire Motors' view from the driver's seat as it was without this mod (Camera/EmpireView)
            Economy.Dealership.DealerType.Unregister();   // the dealership's business type off the game's list (Scripts/Economy/Dealer)
            return Task.CompletedTask;
        }
    }

    /// <summary>Loaded each time the city scene loads: gameplay features hook in here.</summary>
    [ModEntryOnCityLoad]
    public class RealLifeOverhaulCity : IModBigAmbitions
    {
        private const string RootName = "RealLifeOverhaul";
        private GameObject _root;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        // One set of city features per city, whatever loaded them. The game looks for two mods with one assembly name
        // among the Workshop items and among the ModsLocal folders separately (ModDiscoveryRegistry), so the Workshop
        // copy and a local build are both started when both are switched on: this class is then made twice and every
        // feature would run twice. What is looked for is the one thing both would make: the root object, by its
        // name (that also holds for a copy built under another assembly name, which has statics of its own).
        // A root that went with its scene is not found, so a load that was never followed by an unload cannot lock the mod out.
        private static string _running;   // the id of the copy whose features are running (null in a copy that is another assembly)
        private bool _second;

        public Task OnLoadAsync(ModContext context)
        {
            if (_running != context.ModId && GameObject.Find(RootName) != null)
            {
                _second = true;
                context.Logger.Warn("Real Life Overhaul is already running in this city (two copies of the mod are switched on). This copy does nothing; switch one of them off under Mods in the main menu.");
                return Task.CompletedTask;
            }
            _running = context.ModId;
            context = Meta.Dev.Gated(context);   // Info lines only while "Developer diagnostics" is on; warnings and errors always
            GameMembers.Logger = context.Logger;   // what an older game lacks is said once (Core/GameMembers)
            GameConfirm.Logger = context.Logger;
            Tech.TechTypes.Register();   // (furnished as the game's web development agency once its type is known)
            Careers.AcademyType.Register();   // (furnished as the game's recruitment office once its type is known)
            OtherMods.Look(context.Logger);   // other authors' mods this one stands aside for: one walk over the loaded assemblies (Core/OtherMods)
            GoingPublic.Logger = context.Logger;
            GoingPublicLink.Logger = context.Logger;
            SharedCity.Logger = context.Logger;
            GoingPublic.Reset();
            GoingPublic.Look();   // a Going Public session is known before any feature starts (Core/GoingPublic)
            CameraYield.Logger = context.Logger;
            EmpireView.Logger = context.Logger;
            GameUi.WorldPanel.Logger = context.Logger;
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
            TowService.Logger = context.Logger;
            TrafficTools.Logger = context.Logger;
            TrafficEffects.Logger = context.Logger;
            ResponseUnit.Logger = context.Logger;
            SceneCordon.Logger = context.Logger;
            AccidentCast.Logger = context.Logger;
            Shapes.Logger = context.Logger;
            PropLibrary.Logger = context.Logger;
            MapPlaces.Logger = context.Logger;
            SpecialBuildings.Logger = context.Logger;
            MapLinks.Logger = context.Logger;
            Districts.DistrictView.Logger = context.Logger;
            Districts.DistrictPanel.Logger = context.Logger;
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
            HornReactions.Logger = context.Logger;
            Crossings.Logger = context.Logger;
            RecklessDrivers.Logger = context.Logger;
            SolidStreets.Logger = context.Logger;
            SolidGame.Logger = context.Logger;
            Jaywalker.Logger = context.Logger;
            PlayerKnockdown.Logger = context.Logger;
            PhoneContacts.Logger = context.Logger;
            GameFurniture.Logger = context.Logger;
            ContactPictures.Logger = context.Logger;
            ContactSearch.Logger = context.Logger;
            CityLifeDirector.Logger = context.Logger;
            ClipLibrary.Logger = context.Logger;
            ClipLibrary.Load(context);   // the mod's own animation clips, read in the background
            JobDirector.Logger = context.Logger;
            Law.LawDirector.Logger = context.Logger;
            TransitDirector.Logger = context.Logger;
            TaxiDirector.Logger = context.Logger;
            CarOwnership.Logger = context.Logger;
            Events.EventDirector.Logger = context.Logger;
            SocialDirector.Logger = context.Logger;
            BusinessDirector.Logger = context.Logger;
            JusticeDirector.Logger = context.Logger;
            Licences.Logger = context.Logger;
            LicenceDirector.Logger = context.Logger;
            CivicDirector.Logger = context.Logger;
            Family.FamilyDirector.Logger = context.Logger;
            Tech.TechDirector.Logger = context.Logger;
            Tech.RolesApp.Logger = context.Logger;
            Tech.HiringGame.Logger = context.Logger;
            Tech.FirmsGame.Logger = context.Logger;
            Tech.TechApp.Logger = context.Logger;
            Careers.CareersDirector.Logger = context.Logger;
            Careers.CampusApp.Logger = context.Logger;

            _root = new GameObject(RootName);
            SessionWatch.Logger = context.Logger;
            _root.AddComponent<SessionWatch>();   // the record of the session for "it stutters after a while": disabled unless "Developer diagnostics" is on
            _root.AddComponent<GoingPublicWatch>();   // a look at Going Public's session once a second of real time: one comparison a frame otherwise
            _root.AddComponent<ScenePrewarm>();   // one-off searches of the scene done behind the loading screen (Core/SceneFinds): disabled unless a piece is waiting
            ScenePrewarm.Add("roads: the traffic system's road data being found for the city", RoadData.Prewarm);
            ScenePrewarm.Add("traffic: the game's smoke effect being found for accidents and fires", TrafficEffects.Prewarm);
            ScenePrewarm.Add("game UI: the game's font being found", FindFont);
            ScenePrewarm.Add("transit: the game's navigation obstacles being listed for the bus stops", BusZones.Prewarm);
            GameUi.NoticeLook.Logger = context.Logger;
            _root.AddComponent<GameUi.NoticeLook>();   // the mod's notifications told from the game's by a picture of their kind on the mod's colour (Core/GameUi/NoticeLook): nothing per frame, a few writes per notification shown
            ScenePrewarm.Add("game UI: the game's notification toast and list being found, and the notifications' pictures", GameUi.NoticeLook.Prewarm);
            ClickWatch.Logger = context.Logger;
            OverlayGuard.Logger = context.Logger;
            _root.AddComponent<OverlayGuard>();   // the game's card over a thing is put away once the thing is gone (removed while the time machine hid it): two null tests a frame
            _root.AddComponent<SpecialNames>();   // the special buildings' own names on the game's card, guiders, headline and map search (Core/SpecialBuildings): one reference comparison a frame while some building is named
            _root.AddComponent<ClickWatch>();     // with "Developer diagnostics" on, what a left click in the city met: one flag test a frame otherwise
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
            _root.AddComponent<TowService>();   // the tow truck that takes a cleared scene's wrecks away (Traffic/Tow): disabled while there is nothing to tow
            _root.AddComponent<PolicePursuit>();
            _root.AddComponent<StreetCrime>();
            _root.AddComponent<CrimeDirector>();   // the scenes StreetCrime now forwards to
            _root.AddComponent<PoliceAttention>();
            _root.AddComponent<BeatPatrol>();
            _root.AddComponent<PolicePresence>();
            _root.AddComponent<PedestrianIncidents>();
            _root.AddComponent<CarStrikes>();
            _root.AddComponent<HornReactions>();     // people answer the player's horn (People/HornReactions)
            _root.AddComponent<RecklessDrivers>();   // reckless drivers and the accidents that come of them (Traffic/Reckless); after CarStrikes and TrafficAccidents
            _root.AddComponent<SolidStreets>();      // traffic sees people in the road (Traffic/Solid); after HornReactions, CarStrikes and RecklessDrivers
            _root.AddComponent<PlayerKnockdown>();
            _root.AddComponent<CityLifeDirector>();   // people at work and about their day: its own pool of eight
            _root.AddComponent<JobDirector>();   // jobs the player takes themselves (Scripts/Jobs)
            HomeDirector.Logger = context.Logger;
            _root.AddComponent<HomeDirector>();   // home life and health (Scripts/Home); after LifeEvents and the city life director, whose clip player it shares
            _root.AddComponent<Law.LawDirector>();   // precincts, custody, the record and reputation (Scripts/Law)
            _root.AddComponent<TransitDirector>();   // bus stops, buses and the subway journey (Scripts/Transport)
            _root.AddComponent<TaxiDirector>();   // a taxi called from the phone: a real taxi of the game driven to the player's street (Scripts/Transport/Taxi)
            _root.AddComponent<CarOwnership>();   // servicing, wear, breakdowns, insurance, the word in the game's vehicle panel for what a car needs (Scripts/Transport)
            Leisure.LeisureDirector.Logger = context.Logger;
            _root.AddComponent<Leisure.LeisureDirector>();   // things to do for fun, the clothes rack, night life, the sound bed, photo mode (Scripts/Leisure)
            _root.AddComponent<Events.EventDirector>();   // fires, weather that matters, street events (Scripts/Events): after CityLifeDirector, whose clip player it uses
            _root.AddComponent<SocialDirector>();   // the people in the player's life, outings, the morning paper (Scripts/Social)
            TravelDirector.Logger = context.Logger;
            _root.AddComponent<TravelDirector>();   // hotels and holidays (Scripts/Travel)
            _root.AddComponent<BusinessDirector>();   // pressure on the player's businesses (Scripts/Business)
            _root.AddComponent<JusticeDirector>();   // courts, lawyers, sentences and prison (Scripts/Justice)
            _root.AddComponent<LicenceDirector>();   // driving licences: the school, the tests, points and what a stop finds (Scripts/Licences)
            GameUi.UiClone.Logger = context.Logger;
            GameUi.GameSprites.Logger = context.Logger;
            GameUi.AppIconPainter.Logger = context.Logger;
            GameUi.PhoneApp.Logger = context.Logger;
            GameUi.PhoneGridPager.Logger = context.Logger;
            GameUi.AppShell.Logger = context.Logger;
            GameUi.InfoBox.Logger = context.Logger;
            Planner.PlannerApp.Logger = context.Logger;
            Planner.PlannerDirector.Logger = context.Logger;
            _root.AddComponent<Planner.PlannerDirector>();   // the Planner: an app on the game's phone with everything to do, reminders and notes (Scripts/Planner)
            Economy.EconomyDirector.Logger = context.Logger;
            Economy.FinanceApp.Logger = context.Logger;
            _root.AddComponent<Economy.EconomyDirector>();   // the credit rating, the banks' terms by it, loans from the phone and the Finance app (Scripts/Economy)
            Economy.BanksDirector.Logger = context.Logger;
            _root.AddComponent<Economy.BanksDirector>();   // the bank apps: one for each of the city's three largest banks, a front on the same credit desk (Scripts/Economy/Banks)
            Economy.CityEconomyDirector.Logger = context.Logger;
            _root.AddComponent<Economy.CityEconomyDirector>();   // the city's economy: a central bank, prices, a cycle, districts and supply (Scripts/Economy/City)
            Economy.Ventures.VenturesDirector.Logger = context.Logger;
            Economy.Ventures.VenturesApp.Logger = context.Logger;
            _root.AddComponent<Economy.Ventures.VenturesDirector>();   // investors, the pitch and the Ventures app (Scripts/Economy/Ventures)
            Markets.MarketDirector.Logger = context.Logger;
            Markets.MarketTapeFile.Logger = context.Logger;
            Markets.MarketApp.Logger = context.Logger;
            _root.AddComponent<Markets.MarketDirector>();   // the city's stock market and the Stocks app (Scripts/Markets): after Ventures, whose old market it takes over
            Ledger.LedgerDirector.Logger = context.Logger;
            _root.AddComponent<Ledger.LedgerDirector>();   // the City Ledger: the morning paper delivered or bought at a kiosk, and read as a newspaper (Scripts/Ledger)
            Economy.OnlineDirector.Logger = context.Logger;
            Economy.OnlineApp.Logger = context.Logger;
            Economy.Dealership.DealerDirector.Logger = context.Logger;
            Economy.Dealership.DealerApp.Logger = context.Logger;
            _root.AddComponent<Economy.Dealership.DealerDirector>();   // the car dealership: its stock, market, customers, finance and warranties, and the Dealership app (Scripts/Economy/Dealer); before the company, so that its day comes first
            _root.AddComponent<Economy.OnlineDirector>();   // online shops: orders from the whole city, picked from real stock and delivered, and the Web shop app (Scripts/Economy/Online); before the company, so that its day comes first
            Economy.CompanyDirector.Logger = context.Logger;
            Economy.CompanyApp.Logger = context.Logger;
            Economy.ServicesDirector.Logger = context.Logger;
            Economy.ServicesApp.Logger = context.Logger;
            _root.AddComponent<Economy.ServicesDirector>();   // the cleaning service and selling a business, and the Services app (Scripts/Economy/Services)
            _root.AddComponent<Economy.CompanyDirector>();   // running a business: staff, insurance, marketing, tax and licences, and the Company app (Scripts/Economy/Company)
            _root.AddComponent<Tech.TechDirector>();   // the professions and the tech sector: roles, skills and the Open Roles app (Scripts/Tech); after the company, so that its day follows the company's
            _root.AddComponent<Careers.CareersDirector>();   // education and careers: the city's courses, the player's skills and the Campus app (Scripts/Careers); after the tech sector, whose roles it reads
            FitOut.FitOutDirector.Logger = context.Logger;
            _root.AddComponent<FitOut.FitOutDirector>();   // the mod's buildings fitted out across their whole floor while the player is in one, a little a frame (Scripts/FitOut): before the features whose floors it builds
            Tech.TechSites.Logger = context.Logger;
            _root.AddComponent<Tech.TechSites>();   // the tech sector's buildings: server rooms, the tech companies' headquarters, data centre frontages, tech offices (Scripts/Tech/Sites): a look once a second, nothing per frame
            Economy.EstateDirector.Logger = context.Logger;
            Economy.EstateApp.Logger = context.Logger;
            _root.AddComponent<Economy.EstateDirector>();   // property: the market, letting, works, selling, an agency, a hotel and car hire, and the Property app (Scripts/Economy/Estate)
            Economy.OwnBankDirector.Logger = context.Logger;
            _root.AddComponent<Economy.OwnBankDirector>();   // the player's own bank: a licence, savers, loans, a fund and a regulator, and its app (Scripts/Economy/OwnBank); after the company, so that its day's books come after the company's
            Economy.Brands.BrandsDirector.Logger = context.Logger;
            Economy.Brands.BrandsApp.Logger = context.Logger;
            _root.AddComponent<Economy.Brands.BrandsDirector>();   // brands and franchises: recognition, standards, franchised shops, city chains and the Brands app (Scripts/Economy/Brands; after the company: its day follows the company's)
            Nav.NavGraph.Logger = context.Logger;
            Nav.NavMarks.Logger = context.Logger;
            Nav.NavHud.Logger = context.Logger;
            Nav.NavBase.Logger = context.Logger;
            Nav.NavCityMap.Logger = context.Logger;
            Nav.NavDirector.Logger = context.Logger;
            _root.AddComponent<Nav.NavDirector>();   // the minimap on the HUD and the GPS: a route along the roads to the game's destination (Scripts/Navigation)
            _root.AddComponent<CivicDirector>();   // city government, elections and construction sites (Scripts/Civic): its own cast of five
            Utilities.UtilitiesDirector.Logger = context.Logger;
            Utilities.UtilitiesApp.Logger = context.Logger;
            _root.AddComponent<Utilities.UtilitiesDirector>();   // power and water: the utilities office, the bills, a cut and the Utilities app (Scripts/Utilities)
            _root.AddComponent<Family.FamilyDirector>();   // a partner at home, engagement and wedding, children, relatives (Scripts/Family)
            Roommates.RoommateDirector.Attach(_root, context.Logger);   // a roommate who shares the flat and the rent (Scripts/Home/Roommates)
            Flare.FlareDirector.Logger = context.Logger;
            Flare.FlareApp.Logger = context.Logger;
            _root.AddComponent<Flare.FlareDirector>();   // Flare, the city's social media app (Scripts/Social/Flare): after the company and the brands, so that its day follows theirs
            Fame.FameDirector.Logger = context.Logger;
            _root.AddComponent<Fame.FameDirector>();   // Standing, a city that reacts to the player's success (Scripts/Fame): after Flare and the company, so that its day reads theirs
            PermadeathDirector.Logger = context.Logger;
            _root.AddComponent<PermadeathDirector>();   // an ending the player chooses: a death after warnings, the obituary, the save ended (Scripts/Permadeath)
            CarTuning.Logger = context.Logger;
            SpeedReading.Logger = context.Logger;
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
            _root.AddComponent<Districts.DistrictView>();   // the district view on the city map: ten layers, the key and a card per district (Scripts/Districts)
            Interiors.InteriorDirector.Logger = context.Logger;
            CustomerDraw.Logger = context.Logger;
            _root.AddComponent<Interiors.InteriorDirector>();   // interiors and decor that matter: the scores once a game day, the BizMan tab (Scripts/Interiors)
            Wiring.Connect(context.Logger, _root);   // every director is there: the bridges between the features are filled in (Scripts/Wiring)
            // The loading screen is still up: the people of the two street pools are made and dressed now, where the time
            // it takes is not seen (People/Looks). Each catches its own faults and falls back to making them as they are needed.
            Looks.Reset();
            CityLifeDirector.Instance?.Prewarm();
            ActorPool.Instance?.Prewarm();
            // And every search of the whole scene a feature would otherwise make the first time it needs its answer,
            // a frame of over 100 ms each in the middle of play (Core/SceneFinds).
            ScenePrewarm.Begin();
            Meta.MetaCity.Begin(context, _root);   // the check for leftovers, the guide's contact, the welcome panel
            context.Logger.Info("City features started.");
            return Task.CompletedTask;
        }

        // The game's font is found by a search of the scene for any text of the interface (Core/GameUi/GameFont) and kept
        // for the session: asked for here, so that the search is not made when the first bus stop or sign is lettered.
        private static bool FindFont()
        {
            _ = GameUi.GameFont.Asset;
            return true;
        }

        public Task OnUnloadAsync()
        {
            if (_second)
            {
                _second = false;
                return Task.CompletedTask;   // this copy started nothing
            }
            _running = null;
            Meta.MetaCity.End();
            Wiring.Disconnect();   // every bridge back to its stand-alone default
            if (_root != null)
            {
                _root.name = RootName + " (closing)";   // it is gone at the end of the frame: a city loaded before then must not take it for a second copy
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            MapPlaces.Shutdown();
            PropLibrary.Dispose();
            return Task.CompletedTask;
        }
    }
}
