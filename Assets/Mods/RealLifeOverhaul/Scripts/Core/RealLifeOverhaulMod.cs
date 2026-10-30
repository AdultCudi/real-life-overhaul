using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using RealLifeOverhaul;
using RealLifeOverhaul.Districts;
using RealLifeOverhaul.Faults;
using RealLifeOverhaul.Interiors;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Photos;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;
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

        // Two copies switched on (the Workshop item and a ModsLocal folder) are one assembly started twice: the second,
        // told apart by the id the game gives each copy, says so and registers nothing.
        private static string _started;
        private bool _second;

        /// <summary>
        /// True once a second copy was told to stand down, in the game's start or in a city. The copy that runs is the first one the game
        /// discovered: the Workshop items come before ModsLocal, so a beta from the Workshop runs over a public copy in ModsLocal.
        /// (Two Workshop items with this assembly's name never get this far: the game refuses both, "Duplicate mod assembly name".)
        /// The beta's welcome notice tells the player to switch the public copy off (Meta/Welcome).
        /// </summary>
        public static bool TwoCopies;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            if (_started != null && _started != context.ModId)
            {
                _second = true;
                TwoCopies = true;
                context.Logger.Warn("Real Life Overhaul is switched on twice (the Workshop item and a copy in the ModsLocal folder?). This copy does nothing; "
                                    + "switch one of them off under Mods in the main menu.");
                return Task.CompletedTask;
            }
            _started = context.ModId;
            IModLogger log = context.Logger;
            Guard.Logger = log;   // every step below is a Guard.Step: one that fails costs the mod only itself
            Guard.Step("the bug report's record of the mod's warnings", Meta.BugReportGame.Listen);   // from the first line on, for Report a bug
            Guard.Step("the traffic mend's count of the game's failed density passes", TrafficMend.Listen);
            context = Meta.Dev.Gated(context);   // Info lines only while Developer diagnostics is on (read below)
            _context = context;
            Keys.Logger = context.Logger;
            Settings.Start(context.ModId);   // the copy's id only says where older versions kept the options
            Economy.Dealership.DealerType.Logger = log;
            Guard.Step("the car dealership's business type", () => Economy.Dealership.DealerType.Register());   // before any save is read
            Meta.Dev.Gated(context);   // the option has been read now
            Guard.Start(context.Logger);   // the game contract, the game's build and the notice for an older game
            Tech.TechTypes.Logger = context.Logger;
            Guard.Step("the tech sector's business types", () => Tech.TechTypes.Register());   // before any save that holds one is loaded
            Careers.AcademyType.Logger = context.Logger;
            Guard.Step("the training academy's business type", () => Careers.AcademyType.Register());   // before any save that holds one is loaded
            Economy.BranchType.Logger = context.Logger;
            Guard.Step("the bank branch's business type", () => Economy.BranchType.Register());   // before any save that holds one is loaded
            Construction.ConstructionType.Logger = context.Logger;
            Guard.Step("the construction company's business type", () => Construction.ConstructionType.Register());   // before any save that holds one is loaded
            Shops.ShopsDirector.Logger = context.Logger;
            Guard.Step("the new kinds of shop", Shops.ShopsDirector.Start);   // before any save that holds one is loaded
            Designs.DesignsDirector.Logger = context.Logger;
            // The installation firms' designs for the mod's business types, put in place or taken away on a worker.
            Guard.Step("the ready-made designs", () => Designs.DesignsDirector.Ask(Settings.Designs));
            Guard.Step("Uptown's places for the district save", () => Uptown.LifeDirector.Register(context.Logger));   // before any save is read: its early step runs then
            // After Uptown's: the lift is asked as the district's buildings are declared.
            Guard.Step("the landmarks' places and their lift of Uptown's traffic", () => Landmarks.LandmarksDirector.Register(context.Logger));
            // After the landmarks': the campus's name lifts Uptown's traffic after theirs.
            Guard.Step("the schools' street and their lift of Uptown's traffic", () => Schools.SchoolsDirector.Register(context.Logger));
            // After Uptown's: its finished buildings that wear a corner building's model ask for it while the city loads.
            Guard.Step("the corner buildings over Uptown's finished buildings", () => Corners.CornerLook.Register(context.Logger));
            Guard.Step("the city expansion's test run, if one waits for its load", () => TestRun.RunDirector.Begin(context.Logger));   // before any save is read: it hears the load's own lines

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
                .AddDropdown(Settings.HandlingId, "reallife_car_handling",
                    new[] { "reallife_car_handling_0", "reallife_car_handling_1", "reallife_car_handling_2", "reallife_car_handling_3" }, Settings.Handling,
                    v => Settings.Handling = v)
                .AddSlider(Settings.HandlingSoftnessId, "reallife_car_handling_softness", HandlingTuning.Least, HandlingTuning.Most, Settings.HandlingSoftness,
                    v => { Settings.HandlingSoftness = v; CarTuning.TuningChanged(); }, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.HandlingBounceId, "reallife_car_handling_bounce", HandlingTuning.Least, HandlingTuning.Most, Settings.HandlingBounce,
                    v => { Settings.HandlingBounce = v; CarTuning.TuningChanged(); }, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.HandlingRollId, "reallife_car_handling_roll", HandlingTuning.Least, HandlingTuning.Most, Settings.HandlingRoll,
                    v => { Settings.HandlingRoll = v; CarTuning.TuningChanged(); }, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.HandlingGripId, "reallife_car_handling_grip", HandlingTuning.Least, HandlingTuning.Most, Settings.HandlingGrip,
                    v => { Settings.HandlingGrip = v; CarTuning.TuningChanged(); }, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.HandlingSteeringId, "reallife_car_handling_steering", HandlingTuning.Least, HandlingTuning.Most, Settings.HandlingSteering,
                    v => { Settings.HandlingSteering = v; CarTuning.TuningChanged(); }, Meta.SettingsTable.SliderPercent)
                .AddSlider(Settings.HandlingBrakesId, "reallife_car_handling_brakes", HandlingTuning.Least, HandlingTuning.Most, Settings.HandlingBrakes,
                    v => { Settings.HandlingBrakes = v; CarTuning.TuningChanged(); }, Meta.SettingsTable.SliderPercent)
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
                .Guarded(o => o.AddLooks())
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
                .AddToggle(Settings.CityDriversId, "reallife_city_drivers", Settings.CityDrivers,
                    v => Settings.CityDrivers = v)
                .AddToggle(Settings.PhoneContactsId, "reallife_phone_contacts", Settings.PhoneContacts,
                    v => Settings.PhoneContacts = v)
                .AddToggle(Settings.PhoneContactGroupsId, "reallife_phone_contact_groups", Settings.PhoneContactGroups,
                    v =>
                    {
                        Settings.PhoneContactGroups = v;
                        ContactGroupsGame.Apply();   // the buttons and the list follow at once
                    })
                .AddButton("reallife_contacts_test_pictures", ContactPictures.TestReport)
                .AddButton("reallife_contacts_test_redraw", ContactPictures.TestRedraw)
                .AddButton("reallife_contacts_test_groups", ContactGroupsTest.Run)
                .AddToggle(Settings.PhoneSkinId, "reallife_phone_skin", Settings.PhoneSkin,
                    v => Settings.PhoneSkin = v)
                .AddToggle(Settings.PhoneFoldersId, "reallife_phone_folders", Settings.PhoneFolders,
                    v =>
                    {
                        Settings.PhoneFolders = v;
                        GameUi.PhoneLayoutPrefs.Tell();   // the phones lay themselves out again at once
                    })
                .AddButton("reallife_phone_layout_reset", GameUi.PhoneMove.ResetAll)
                .AddButton("reallife_phone_test_check", GameUi.PhoneMove.TestCheck)
                .AddButton("reallife_phone_test_folder", GameUi.PhoneMove.TestFolder)
                .AddButton("reallife_leftpane_test", Meta.LeftPaneTest.Run)
                .AddToggle(Settings.NoticeLookId, "reallife_notice_look", Settings.NoticeLook,
                    v => Settings.NoticeLook = v)
                .AddButton("reallife_test_notice_look", GameUi.NoticeLook.Test)
                .AddButton("reallife_test_event", () => LifeEvents.Instance?.TriggerEventNow())
                .AddButton("reallife_moods_test", MoodsTest.Run)
                .AddButton("reallife_maptrees_test", MapTreeGuard.RunTest)
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
                .AddButton("reallife_test_drivers_park", () => CityDrivers.TestNow(CityDrivers.TestKind.Park))
                .AddButton("reallife_test_drivers_letout", () => CityDrivers.TestNow(CityDrivers.TestKind.LetOut))
                .AddButton("reallife_test_wave_through", TrafficAccidents.TestWaveThrough)
                .AddButton("reallife_test_traffic_mend", TrafficMend.Test)
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
                .AddToggle(Settings.JobTaxiEasyId, "reallife_jobs_taxi_easy", Settings.JobTaxiEasy,
                    v => Settings.JobTaxiEasy = v)
                .AddButton("reallife_test_job_flyers", () => JobDirector.Instance?.TestStart(JobCatalogue.Flyers))
                .AddButton("reallife_test_job_courier", () => JobDirector.Instance?.TestStart(JobCatalogue.Courier))
                .AddButton("reallife_test_job_taxi",() => JobDirector.Instance?.TestStart(JobCatalogue.Taxi))
                .AddButton("reallife_test_job_taxi_table", () => JobDirector.Instance?.TestTaxiTable())
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
                .AddButton("reallife_study_test_computer", () => Careers.CareersDirector.Instance?.TestComputer())
                .AddButton("reallife_study_test_session_desk", () => Careers.CareersDirector.Instance?.TestSessionDesk())
                .AddButton("reallife_study_test_outage", () => Careers.CareersDirector.Instance?.TestOutage())
                .AddButton("reallife_study_test_exam", () => Careers.CareersDirector.Instance?.TestExam())
                .AddButton("reallife_study_test_skills", () => Careers.CareersDirector.Instance?.TestSkills())
                .AddToggle(Settings.CareersClassStartId, "reallife_study_opt_class_start", Settings.CareersClassStart,
                    v => Settings.CareersClassStart = v)
                .AddToggle(Settings.CareersDegreesId, "reallife_degrees_enabled", Settings.CareersDegrees,
                    v => Settings.CareersDegrees = v)
                .AddButton("reallife_degrees_test_graduate", () => Careers.CareersDirector.Instance?.TestGraduate())
                .AddButton("reallife_degrees_test_away", () => Careers.CareersDirector.Instance?.TestAway())
                .AddToggle(Settings.CareersFamilyId, "reallife_family_study_enabled", Settings.CareersFamily,
                    v => Settings.CareersFamily = v)
                .AddButton("reallife_family_test_tutor", () => Careers.CareersDirector.Instance?.TestTutor())
                .AddToggle(Settings.CareersJobsId, "reallife_work_enabled", Settings.CareersJobs,
                    v => Settings.CareersJobs = v)
                .AddButton("reallife_work_test_offer", () => Careers.CareersDirector.Instance?.TestOffer())
                .AddButton("reallife_work_test_reject", () => Careers.CareersDirector.Instance?.TestReject())
                .AddButton("reallife_work_test_day", () => Careers.CareersDirector.Instance?.TestWorkDay())
                .AddButton("reallife_work_test_desk", () => Careers.CareersDirector.Instance?.TestDesk())
                .AddButton("reallife_work_test_computer", () => Careers.CareersDirector.Instance?.TestComputerWork())
                .AddButton("reallife_work_test_interview_place", () => Careers.CareersDirector.Instance?.TestInterviewPlace())
                .AddButton("reallife_work_test_interview_home", () => Careers.CareersDirector.Instance?.TestInterviewHome())
                .AddButton("reallife_work_test_vacancies", () => Careers.CareersDirector.Instance?.TestVacancies())
                .AddButton("reallife_work_test_shift_hours", () => Careers.CareersDirector.Instance?.TestShiftHours())
                .AddButton("reallife_work_test_forklift", () => Careers.CareersDirector.Instance?.TestForklift())
                .AddButton("reallife_work_test_closed", () => Careers.CareersDirector.Instance?.TestClosed())
                .AddButton("reallife_work_test_moved", () => Careers.CareersDirector.Instance?.TestMoved())
                .AddToggle(Settings.CareersAgencyNoteId, "reallife_work_opt_agency_note", Settings.CareersAgencyNote,
                    v => Settings.CareersAgencyNote = v)
                .AddToggle(Settings.CareersRejectReasonId, "reallife_work_opt_reject_reason", Settings.CareersRejectReason,
                    v => Settings.CareersRejectReason = v)
                .AddToggle(Settings.CareersLifeId, "reallife_life_enabled", Settings.CareersLife,
                    v => Settings.CareersLife = v)
                .AddButton("reallife_life_test_review", () => Careers.CareersDirector.Instance?.TestReview())
                .AddButton("reallife_life_test_matter", () => Careers.CareersDirector.Instance?.TestMatter())
                .AddButton("reallife_life_test_approach", () => Careers.CareersDirector.Instance?.TestApproach())
                .AddToggle(Settings.CareersLadderId, "reallife_ladder_enabled", Settings.CareersLadder,
                    v => Settings.CareersLadder = v)
                .AddButton("reallife_ladder_test_job", () => Careers.CareersDirector.Instance?.TestLadderJob())
                .AddButton("reallife_ladder_test_promote", () => Careers.CareersDirector.Instance?.TestLadderPromote())
                .AddButton("reallife_ladder_test_tasks", () => Careers.CareersDirector.Instance?.TestLadderTasks())
                .AddButton("reallife_ladder_test_decide", () => Careers.CareersDirector.Instance?.TestLadderDecide())
                .AddButton("reallife_ladder_test_ask", () => Careers.CareersDirector.Instance?.TestLadderAsk())
                .AddToggle(Settings.CareersOwnId, "reallife_own_enabled", Settings.CareersOwn,
                    v => Settings.CareersOwn = v)
                .AddToggle(Settings.CareersOwnOfferNotesId, "reallife_own_opt_offer_notes", Settings.CareersOwnOfferNotes,
                    v => Settings.CareersOwnOfferNotes = v)
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
                .Guarded(o => o.AddInteriors())   // interiors and decor that matter (Scripts/Interiors)
                .Guarded(o => Designs.DesignsOptions.AddDesigns(o))   // the installation firms' designs for the mod's types (Scripts/Designs)
                .Guarded(o => Shops.ShopsOptions.AddShops(o))   // the ice cream parlour, tea bar and tobacconist (Scripts/Shops)
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
                .AddToggle(Settings.HomeMorningNewsId, "reallife_home_morning_news_option", Settings.HomeMorningNews,
                    v => Settings.HomeMorningNews = v)
                .AddToggle(Settings.HomeIllnessId, "reallife_home_illness", Settings.HomeIllness,
                    v => Settings.HomeIllness = v)
                .AddToggle(Settings.HomeFitnessId, "reallife_home_fitness", Settings.HomeFitness,
                    v => Settings.HomeFitness = v)
                .AddToggle(Settings.HomeBodyId, "reallife_home_body", Settings.HomeBody,
                    v => Settings.HomeBody = v)
                .AddToggle(Settings.HomeJogId, "reallife_home_jog", Settings.HomeJog,
                    v => Settings.HomeJog = v)
                .AddButton("reallife_test_home_page", () => HomeDirector.Instance?.TestPage())
                .AddButton("reallife_test_home_morning", () => HomeDirector.Instance?.TestMorning())
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
                .AddButton("reallife_test_vehicle_costs", () => CarOwnership.Instance?.TestCosts())
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
                .AddToggle(Settings.StreetProtestNotesId, "reallife_events_protest_notes", Settings.StreetProtestNotes,
                    v => Settings.StreetProtestNotes = v)
                .AddButton("reallife_events_test_fire_car", () => Events.EventDirector.Instance?.TestFire(Events.FireKind.Car))
                .AddButton("reallife_events_test_fire_bin", () => Events.EventDirector.Instance?.TestFire(Events.FireKind.Bin))
                .AddButton("reallife_events_test_fire_building", () => Events.EventDirector.Instance?.TestFire(Events.FireKind.Building))
                .AddButton("reallife_events_test_fire_blocked", () => Events.EventDirector.Instance?.TestFireBlocked())
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
                .AddToggle(Settings.SocialComposerId, "reallife_social_composer", Settings.SocialComposer,
                    v => Settings.SocialComposer = v)
                .AddToggle(Settings.SocialNetworkingId, "reallife_social_networking", Settings.SocialNetworking,
                    v => Settings.SocialNetworking = v)
                .AddButton("reallife_social_net_test_intro", Networking.NetworkDirector.TestIntro)
                .AddButton("reallife_social_net_test_favour", Networking.NetworkDirector.TestFavour)
                .AddButton("reallife_social_net_test_each", Networking.NetworkDirector.TestEach)
                .AddButton("reallife_social_test_meet", () => SocialDirector.Instance?.TestMeet())
                .AddButton("reallife_social_test_person", () => SocialDirector.Instance?.TestPerson())
                .AddButton("reallife_social_test_advance", () => SocialDirector.Instance?.TestAdvance())
                .AddButton("reallife_social_test_call", () => SocialDirector.Instance?.TestCall())
                .AddButton("reallife_social_test_composer", () => SocialDirector.Instance?.TestComposer())
                .AddButton("reallife_social_test_text", () => SocialDirector.Instance?.TestText())
                .AddButton("reallife_social_test_followup", () => SocialDirector.Instance?.TestFollowUp())
                .AddButton("reallife_social_test_nextday", () => SocialDirector.Instance?.TestNextDay())
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
                .AddButton("reallife_social_test_lose", () => SocialDirector.Instance?.TestLoseTouch())
                .AddButton("reallife_social_test_looks", () => SocialDirector.Instance?.TestOneLook())
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
                .AddDropdown(Settings.FlareBusyId, "reallife_flare_opt_busy",
                    new[] { "reallife_flare_busy_0", "reallife_flare_busy_1", "reallife_flare_busy_2" }, Settings.FlareBusy,
                    v => Settings.FlareBusy = v)
                .AddToggle(Settings.FlareCityOfficeId, "reallife_flare_opt_city_office", Settings.FlareCityOffice,
                    v => Settings.FlareCityOffice = v)
                .AddToggle(Settings.FlareSharePostsId, "reallife_flare_opt_share_posts", Settings.FlareSharePosts,
                    v => Settings.FlareSharePosts = v)
                .AddToggle(Settings.FlareSharePicturesId, "reallife_flare_opt_share_pictures", Settings.FlareSharePictures,
                    v => Settings.FlareSharePictures = v)
                .AddDropdown(Settings.FlareFilterId, "reallife_flare_opt_filter",
                    new[] { "reallife_flare_filter_0", "reallife_flare_filter_1" }, Settings.FlareFilter,
                    v => Settings.FlareFilter = v)
                .AddDropdown(Settings.FlareThemeId, "reallife_flare_opt_theme",
                    new[] { "reallife_flare_theme_0", "reallife_flare_theme_1", "reallife_flare_theme_2" }, Settings.FlareTheme,
                    v => Settings.FlareTheme = v)
                .AddToggle(Settings.FlarePicturesId, "reallife_flare_opt_pictures", Settings.FlarePictures,
                    v => Settings.FlarePictures = v)
                .AddDropdown(Settings.FlareCityPhotosId, "reallife_flare_opt_city_photos",
                    new[] { "reallife_flare_city_photos_0", "reallife_flare_city_photos_1", "reallife_flare_city_photos_2", "reallife_flare_city_photos_3" }, Settings.FlareCityPhotos,
                    v => Settings.FlareCityPhotos = v)
                .AddButton("reallife_flare_test_drafts", () => Flare.FlareDirector.Instance?.TestDrafts())
                .AddButton("reallife_flare_test_day", () => Flare.FlareDirector.Instance?.TestDay())
                .AddButton("reallife_flare_test_followers", () => Flare.FlareDirector.Instance?.TestFollowers())
                .AddButton("reallife_flare_test_viral", () => Flare.FlareDirector.Instance?.TestViral())
                .AddButton("reallife_flare_test_month", () => Flare.FlareDirector.Instance?.TestMonth())
                .AddButton("reallife_flare_test_arrival", () => Flare.FlareDirector.Instance?.TestArrival())
                .AddButton("reallife_flare_test_inf_scandal", () => Flare.FlareDirector.Instance?.TestInfluencerScandal())
                .AddButton("reallife_flare_test_online", () => Flare.FlareDirector.Instance?.TestOnline())
                .AddButton("reallife_flare_test_city", () => Flare.FlareDirector.Instance?.TestCity())
                .AddButton("reallife_flare_test_pictures", () => Flare.FlareDirector.Instance?.TestPictures())
                .AddButton("reallife_flare_test_report", () => Flare.FlareDirector.Instance?.TestReport())
                .AddButton("reallife_flare_test_cityday", () => Flare.FlareDirector.Instance?.TestCityDay())
                .AddButton("reallife_flare_test_review_now", () => Flare.FlareDirector.Instance?.TestReviewNow())
                .AddButton("reallife_flare_test_reactions", () => Flare.FlareDirector.Instance?.TestReactions())
                .AddButton("reallife_flare_test_pileon", () => Flare.FlareDirector.Instance?.TestPileOn())
                .AddButton("reallife_flare_test_dm", () => Flare.FlareDirector.Instance?.TestDm())
                .AddButton("reallife_flare_test_invitee", () => Flare.FlareDirector.Instance?.TestInvitee())
                .AddButton("reallife_flare_test_invite", () => Flare.FlareDirector.Instance?.TestInvite())
                .AddButton("reallife_flare_test_escalate", () => Flare.FlareDirector.Instance?.TestEscalate())
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
                .AddToggle(Settings.StandingRequestNotesId, "reallife_fame_request_notes", Settings.StandingRequestNotes,
                    v => Settings.StandingRequestNotes = v)
                .AddButton("reallife_fame_test_day", () => Fame.FameDirector.Instance?.TestDay())
                .AddButton("reallife_fame_test_street", () => Fame.FameDirector.Instance?.TestStreet())
                .AddButton("reallife_fame_test_known", () => Fame.FameDirector.Instance?.TestKnown())
                .AddButton("reallife_fame_test_reporter", () => Fame.FameDirector.Instance?.TestReporter())
                .AddButton("reallife_fame_test_show", () => Fame.FameDirector.Instance?.TestShow())
                .AddButton("reallife_fame_test_promises", () => Fame.FameDirector.Instance?.TestPromises())
                .AddButton("reallife_fame_test_request", () => Fame.FameDirector.Instance?.TestRequest())
                .AddButton("reallife_fame_test_evening", () => Fame.FameDirector.Instance?.TestEvening())
                .AddButton("reallife_fame_test_meetings", () => Fame.FameDirector.Instance?.TestMeetings())
                .AddButton("reallife_fame_test_bribe", () => Fame.FameDirector.Instance?.TestBribe())
                .AddButton("reallife_fame_test_council", () => Fame.FameDirector.Instance?.TestCouncil())
                .AddButton("reallife_fame_test_back", () => Fame.FameDirector.Instance?.TestBack())
                .AddSplitter()
                .AddHeader("reallife_rival_header")
                .AddToggle(Settings.RivalsId, "reallife_rival_enabled", Settings.Rivals,
                    v => Settings.Rivals = v)
                .AddDropdown(Settings.RivalsStrengthId, "reallife_rival_strength",
                    new[] { "reallife_rival_strength_gentle", "reallife_rival_strength_normal", "reallife_rival_strength_hard", "reallife_rival_strength_ruthless" }, Settings.RivalsStrength,
                    v => Settings.RivalsStrength = v)
                .AddDropdown(Settings.RivalsFirstId, "reallife_rival_first",
                    new[] { "reallife_rival_first_5", "reallife_rival_first_10", "reallife_rival_first_20", "reallife_rival_first_30", "reallife_rival_first_50" }, Settings.RivalsFirst,
                    v => Settings.RivalsFirst = v)
                .AddButton("reallife_rival_test_property", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Property))
                .AddButton("reallife_rival_test_outlet", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Outlet))
                .AddButton("reallife_rival_test_poach", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Poach))
                .AddButton("reallife_rival_test_stake", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Stake))
                .AddButton("reallife_rival_test_bid", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Bid))
                .AddButton("reallife_rival_test_lobby", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Lobby))
                .AddButton("reallife_rival_test_suit", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Suit))
                .AddButton("reallife_rival_test_press", () => Rivals.RivalDirector.Instance?.TestMove(Rivals.RivalMoveKind.Press))
                .AddButton("reallife_rival_test_next", () => Rivals.RivalDirector.Instance?.TestAnnounce())
                .AddButton("reallife_rival_test_forward", () => Rivals.RivalDirector.Instance?.TestForward())
                .AddButton("reallife_rival_test_first", () => Rivals.RivalDirector.Instance?.TestFirst())
                .AddButton("reallife_rival_test_list", () => Rivals.RivalDirector.Instance?.TestList())
                .AddButton("reallife_rival_test_takeover", () => Rivals.RivalDirector.Instance?.TestTakeover())
                .AddButton("reallife_rival_test_back", () => Rivals.RivalDirector.Instance?.TestBack())
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
                .AddToggle(Settings.GarageSoundsId, "reallife_garage_sounds", Settings.GarageSounds,
                    v => Settings.GarageSounds = v)
                .AddToggle(Settings.GarageResaleId, "reallife_garage_resale", Settings.GarageResale,
                    v => Settings.GarageResale = v)
                .AddToggle(Settings.TwoWheelersId, "reallife_garage_two_wheelers", Settings.TwoWheelers,
                    v => Settings.TwoWheelers = v)
                .AddButton("reallife_garage_test_shop", () => GarageDirector.Instance?.TestShop())
                .AddButton("reallife_garage_test_random", () => GarageDirector.Instance?.TestRandomBuild())
                .AddButton("reallife_garage_test_restore", () => GarageDirector.Instance?.TestStock())
                .AddButton("reallife_garage_test_sounds", () => CarSoundDirector.Instance?.TestReport())
                .AddButton("reallife_tuning_test_figures", () => GarageDirector.Instance?.TestFigures())
                .AddButton("reallife_tuning_test_car", () => GarageDirector.Instance?.TestTuning())
                .AddButton("reallife_tuning_test_sale", () => GarageDirector.Instance?.TestSale())

                .AddButton("reallife_garage_test_vehicles", () => { if (GarageDirector.Instance != null) { GarageSummary.Toggle(); } })
                .AddButton("reallife_garage_test_bicycle", () => GarageDirector.Instance?.TestBike(BikeKind.CityBicycle))
                .AddButton("reallife_garage_test_road_bicycle", () => GarageDirector.Instance?.TestBike(BikeKind.RoadBicycle))
                .AddButton("reallife_garage_test_motorbike", () => GarageDirector.Instance?.TestBike(BikeKind.StandardMotorbike))
                .AddButton("reallife_garage_test_sport_motorbike", () => GarageDirector.Instance?.TestBike(BikeKind.SportMotorbike))
                .AddSplitter()
                .Guarded(o => o.AddJustice())
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
                .AddToggle(Settings.HeavyLicenceId, "reallife_lic_opt_heavy", Settings.HeavyLicence,
                    v => Settings.HeavyLicence = v)
                .AddButton("reallife_test_lic_card", () => LicenceDirector.Instance?.ShowCard())
                .AddButton("reallife_test_lic_car", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Car))
                .AddButton("reallife_test_lic_commercial", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Commercial))
                .AddButton("reallife_test_lic_taxi", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Taxi))
                .AddButton("reallife_test_lic_motorcycle", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Motorcycle))
                .AddButton("reallife_test_lic_heavy", () => LicenceDirector.Instance?.TestToggle(LicenceClass.Heavy))
                .AddButton("reallife_test_lic_theory", () => LicenceDirector.Instance?.TestTheory())
                .AddButton("reallife_test_lic_practical", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Car))
                .AddButton("reallife_test_lic_practical_van", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Commercial))
                .AddButton("reallife_test_lic_theory_heavy", () => LicenceDirector.Instance?.TestHeavyTheory())
                .AddButton("reallife_test_lic_practical_heavy", () => LicenceDirector.Instance?.TestPractical(LicenceClass.Heavy))
                .AddButton("reallife_test_job_heavy_load", () => JobDirector.Instance?.TestHeavyLoad())
                .AddButton("reallife_test_lic_check", () => LicenceDirector.Instance?.TestCheck())
                .AddButton("reallife_test_lic_points", () => LicenceDirector.Instance?.TestPoints())
                .AddSplitter()
                .Guarded(o => Seasons.SeasonOptions.AddSeasons(o))   // what sells in each season, and the snow (Scripts/Seasons)
                .AddHeader("reallife_planner_header")
                .AddToggle(Settings.PlannerId, "reallife_planner_enabled", Settings.Planner,
                    v => Settings.Planner = v)
                .AddToggle(Settings.PlannerRemindersId, "reallife_planner_reminders", Settings.PlannerReminders,
                    v => Settings.PlannerReminders = v)
                .AddToggle(Settings.PlannerMorningId, "reallife_planner_opt_morning", Settings.PlannerMorning,
                    v => Settings.PlannerMorning = v)
                .AddButton("reallife_planner_test_fill", () => Planner.PlannerDirector.Instance?.TestFill())
                .AddButton("reallife_planner_test_clear", () => Planner.PlannerDirector.Instance?.TestClear())
                .AddSplitter()
                .Guarded(o => Calendar.CalendarOptions.AddCalendar(o))   // the month on the phone (Scripts/Calendar)
                .Guarded(o => Hustles.HustleOptions.AddHustles(o))   // the Marketplace and freelance visits (Scripts/Hustles)
                .AddHeader("reallife_econ_header")
                .AddToggle(Settings.EconomyId, "reallife_econ_enabled", Settings.Economy,
                    v => Settings.Economy = v)
                .AddToggle(Settings.EconomyGradeUpNotesId, "reallife_econ_opt_grade_up_notes", Settings.EconomyGradeUpNotes,
                    v => Settings.EconomyGradeUpNotes = v)
                .AddToggle(Settings.EconomyDayResultId, "reallife_econ_opt_day_result", Settings.EconomyDayResult,
                    v => Settings.EconomyDayResult = v)
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
                .AddToggle(Settings.EconomyRateAheadId, "reallife_macro_opt_eve", Settings.EconomyRateAhead,
                    v => Settings.EconomyRateAhead = v)
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
                .Guarded(o => Outlook.OutlookOptions.AddOutlook(o))   // the Economy app (Scripts/Outlook)
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
                .AddToggle(Settings.MarketsBotsId, "reallife_mkt_bot_opt", Settings.MarketsBots,
                    v => Settings.MarketsBots = v)
                .AddToggle(Settings.MarketsDividendNotesId, "reallife_mkt_opt_dividend_notes", Settings.MarketsDividendNotes,
                    v => Settings.MarketsDividendNotes = v)
                .AddToggle(Settings.MarketsBondsId, "reallife_mkt_bond_opt", Settings.MarketsBonds,
                    v => Settings.MarketsBonds = v)
                .AddToggle(Settings.MarketsBondSoFarId, "reallife_mkt_bond_opt_so_far", Settings.MarketsBondSoFar,
                    v => Settings.MarketsBondSoFar = v)
                .AddToggle(Settings.MarketsLargeId, "reallife_mkt_inv_opt_large", Settings.MarketsLarge,
                    v => Settings.MarketsLarge = v)
                .AddToggle(Settings.MarketsReinvestId, "reallife_mkt_inv_opt_reinvest", Settings.MarketsReinvest,
                    v => Settings.MarketsReinvest = v)
                .AddToggle(Settings.MarketsDerivativesId, "reallife_mkt_drv_opt", Settings.MarketsDerivatives,
                    v => Settings.MarketsDerivatives = v)
                .AddToggle(Settings.MarketsBuildersId, "reallife_mkt_builders_opt", Settings.MarketsBuilders,
                    v => Settings.MarketsBuilders = v)
                .AddDropdown(Settings.MarketsHoldingId, "reallife_mkt_opt_holding",
                    new[] { "reallife_mkt_opt_holding_tenth", "reallife_mkt_opt_holding_quarter", "reallife_mkt_opt_holding_half", "reallife_mkt_opt_holding_all" }, Settings.MarketsHolding,
                    v => Settings.MarketsHolding = v)
                .AddButton("reallife_mkt_test_open", () => Markets.MarketDirector.Instance?.TestOpen())
                .AddButton("reallife_mkt_test_crisis", () => Markets.MarketDirector.Instance?.TestNews(true))
                .AddButton("reallife_mkt_test_windfall", () => Markets.MarketDirector.Instance?.TestNews(false))
                .AddButton("reallife_mkt_test_bid", () => Markets.MarketDirector.Instance?.TestBid())
                .AddButton("reallife_mkt_bot_test_month", () => Markets.MarketDirector.Instance?.TestBotMonth())
                .AddButton("reallife_mkt_bond_test_open", () => Markets.MarketDirector.Instance?.TestBonds())
                .AddButton("reallife_mkt_bond_test_pay", () => Markets.MarketDirector.Instance?.TestBondPay())
                .AddButton("reallife_mkt_drv_test_open", () => Markets.MarketDirector.Instance?.TestFutures())
                .AddButton("reallife_mkt_drv_test_settle", () => Markets.MarketDirector.Instance?.TestSettle())
                .AddButton("reallife_mkt_drv_test_call", () => Markets.MarketDirector.Instance?.TestCall())
                .AddButton("reallife_mkt_inv_test_work", () => Markets.MarketDirector.Instance?.TestWorkedDay())
                .AddButton("reallife_mkt_inv_test_back", () => Markets.MarketDirector.Instance?.TestPutBack())
                .AddButton("reallife_mkt_test_holding", () => Markets.MarketDirector.Instance?.TestHolding())
                .AddButton("reallife_mkt_inv_test_roll", () => Markets.MarketDirector.Instance?.TestRollover())
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
                .AddToggle(Settings.EconomyManagersId, "reallife_mgr_enabled", Settings.EconomyManagers,
                    v => Settings.EconomyManagers = v)
                .AddToggle(Settings.EconomyHierarchyId, "reallife_hier_enabled", Settings.EconomyHierarchy,
                    v => Settings.EconomyHierarchy = v)
                .AddToggle(Settings.EconomyCleaningId, "reallife_svc_cleaning_enabled", Settings.EconomyCleaning,
                    v => Settings.EconomyCleaning = v)
                .AddToggle(Settings.EconomySaleId, "reallife_svc_sale_enabled", Settings.EconomySale,
                    v => Settings.EconomySale = v)
                .AddToggle(Settings.OfficeConversionId, "reallife_offices_enabled", Settings.OfficeConversion,
                    v => Settings.OfficeConversion = v)
                .AddButton("reallife_offices_test_list", () => Offices.Conversions.Instance?.TestList())
                .AddButton("reallife_held_test", HeldUnits.Test)
                .AddToggle(Settings.EconomyBankId, "reallife_ob_enabled", Settings.EconomyBank,
                    v => Settings.EconomyBank = v)
                .AddToggle(Settings.EconomyBranchId, "reallife_branch_enabled", Settings.EconomyBranch,
                    v => Settings.EconomyBranch = v)
                .AddToggle(Settings.EndScaleId, "reallife_endscale_enabled", Settings.EndScale,
                    v => Settings.EndScale = v)
                .AddButton("reallife_endscale_test_phase", () => EndScaleTests.Phase(false))
                .AddButton("reallife_endscale_test_full", () => EndScaleTests.Phase(true))
                .AddDropdown(Settings.EconomyCompanyLevelId, "reallife_co_level",
                    new[] { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" }, Settings.EconomyCompanyLevel,
                    v => Settings.EconomyCompanyLevel = v)
                .AddButton("reallife_co_test_raise", () => Economy.CompanyDirector.Instance?.TestRaise())
                .AddButton("reallife_co_test_picked_raise", () => Economy.CompanyDirector.Instance?.TestPickedRaise())
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
                .AddButton("reallife_mgr_test_appoint", () => Economy.CompanyDirector.Instance?.TestManagerAppoint())
                .AddButton("reallife_mgr_test_fill", () => Economy.CompanyDirector.Instance?.TestManagerFill())
                .AddButton("reallife_mgr_test_decide", () => Economy.CompanyDirector.Instance?.TestManagerDecide())
                .AddButton("reallife_mgr_test_undo", () => Economy.CompanyDirector.Instance?.TestManagerUndoAll())
                .AddButton("reallife_hier_test_store", () => Economy.CompanyDirector.Instance?.TestHierarchyStore())
                .AddButton("reallife_hier_test_region", () => Economy.CompanyDirector.Instance?.TestHierarchyRegion())
                .AddButton("reallife_hier_test_executives", () => Economy.CompanyDirector.Instance?.TestHierarchyExecutives())
                .AddButton("reallife_hier_test_owner", () => Economy.CompanyDirector.Instance?.TestHierarchyOwner())
                .AddButton("reallife_hier_test_gap", () => Economy.CompanyDirector.Instance?.TestHierarchyGap())
                .AddButton("reallife_hier_test_budget", () => Economy.CompanyDirector.Instance?.TestHierarchyBudget())
                .AddButton("reallife_hier_test_demand", () => Economy.CompanyDirector.Instance?.TestHierarchyDemand())
                .AddButton("reallife_hier_test_mood", () => Economy.CompanyDirector.Instance?.TestHierarchyMood())
                .AddButton("reallife_hier_test_wave", () => Economy.CompanyDirector.Instance?.TestHierarchyWave())
                .AddButton("reallife_hier_test_kpi", () => Economy.CompanyDirector.Instance?.TestHierarchyKpi())
                .AddButton("reallife_hier_test_exception", () => Economy.CompanyDirector.Instance?.TestHierarchyException())
                .AddButton("reallife_ob_test_found", () => Economy.OwnBankDirector.Instance?.TestFound())
                .AddButton("reallife_ob_test_month", () => Economy.OwnBankDirector.Instance?.TestMonth())
                .AddButton("reallife_ob_test_hard", () => Economy.OwnBankDirector.Instance?.TestHardMonth())
                .AddButton("reallife_ob_test_run", () => Economy.OwnBankDirector.Instance?.TestRun())
                .AddButton("reallife_ob_test_deadline", () => Economy.OwnBankDirector.Instance?.TestDeadline())
                .AddButton("reallife_ob_test_fund", () => Economy.OwnBankDirector.Instance?.TestFund())
                .AddButton("reallife_ob_test_level", () => Economy.OwnBankDirector.Instance?.TestTierReady())
                .AddButton("reallife_ob_test_back", () => Economy.OwnBankDirector.Instance?.TestBack())
                .AddButton("reallife_branch_test_hour", () => Economy.OwnBankDirector.Instance?.TestBranchHour())
                .AddButton("reallife_branch_test_floor", () => Economy.BranchFloor.Instance?.TestFloor())
                .AddHeader("reallife_estate_header")
                .AddToggle(Settings.EstateId, "reallife_estate_enabled", Settings.Estate,
                    v => Settings.Estate = v)
                .AddToggle(Settings.EstateHotelId, "reallife_estate_opt_hotel", Settings.EstateHotel,
                    v => Settings.EstateHotel = v)
                .AddToggle(Settings.EstateHireId, "reallife_estate_opt_hire", Settings.EstateHire,
                    v => Settings.EstateHire = v)
                .AddSlider(Settings.EstateRentId, "reallife_estate_opt_rent", 50, 300, Settings.EstateRent, v => Settings.EstateRent = v, Meta.SettingsTable.SliderPercent)
                .AddToggle(Settings.EstateDutiesId, "reallife_estate_opt_duties", Settings.EstateDuties,
                    v => Settings.EstateDuties = v)
                .AddButton("reallife_estate_test_applicant", () => Economy.EstateDirector.Instance?.TestApplicant())
                .AddButton("reallife_estate_test_arrears", () => Economy.EstateDirector.Instance?.TestArrears())
                .AddButton("reallife_estate_test_works", () => Economy.EstateDirector.Instance?.TestWorks())
                .AddButton("reallife_estate_test_offer", () => Economy.EstateDirector.Instance?.TestOffer())
                .AddButton("reallife_estate_test_instruction", () => Economy.EstateDirector.Instance?.TestInstruction())
                .AddButton("reallife_estate_test_night", () => Economy.EstateDirector.Instance?.TestHotelNight())
                .AddButton("reallife_estate_test_group", () => Economy.EstateDirector.Instance?.TestGroup())
                .AddButton("reallife_estate_test_hire", () => Economy.EstateDirector.Instance?.TestHire())
                .AddButton("reallife_estate_test_month", () => Economy.EstateDirector.Instance?.TestMonth())
                .AddButton("reallife_estate_test_call", () => Economy.EstateDirector.Instance?.TestCall())
                .AddButton("reallife_estate_test_complaint", () => Economy.EstateDirector.Instance?.TestComplaint())
                .AddButton("reallife_estate_test_inspection", () => Economy.EstateDirector.Instance?.TestInspection())
                .AddButton("reallife_estate_test_manager", () => Economy.EstateDirector.Instance?.TestManager())
                .AddButton("reallife_estate_test_back", () => Economy.EstateDirector.Instance?.TestBack())
                .AddHeader("reallife_talent_header")
                .AddToggle(Settings.TechPeopleId, "reallife_talent_enabled", Settings.TechPeople,
                    v => Settings.TechPeople = v)
                .AddButton("reallife_talent_test_review", () => Tech.TechDirector.Instance?.TestReview())
                .AddButton("reallife_talent_test_offer", () => Tech.TechDirector.Instance?.TestOffer())
                .AddButton("reallife_talent_test_course", () => Tech.TechDirector.Instance?.TestCourse())
                .AddButton("reallife_names_test", () => Tech.TechDirector.Instance?.TestNames())
                .AddToggle(Settings.TechJobsId, "reallife_hire_enabled", Settings.TechJobs,
                    v => Settings.TechJobs = v)
                .AddButton("reallife_hire_test_applicants", () => Tech.TechDirector.Instance?.TestApplicants())
                .AddButton("reallife_hire_test_market", () => Tech.TechDirector.Instance?.TestMarketDay())
                .AddButton("reallife_hire_test_search", () => Tech.TechDirector.Instance?.TestSearch())
                .AddButton("reallife_crew_test", () => Tech.TechDirector.Instance?.TestCrewSearch())
                .AddButton("reallife_dataeng_test", () => Tech.TechDirector.Instance?.TestDataEngineers())
                .AddButton("reallife_hire_test_orphan", () => Tech.TechDirector.Instance?.TestOrphanRegistration())
                .AddToggle(Settings.TechSectorId, "reallife_tech_enabled", Settings.TechSector,
                    v => Settings.TechSector = v)
                .AddToggle(Settings.TechOfferNotesId, "reallife_tech_opt_offer_notes", Settings.TechOfferNotes,
                    v => Settings.TechOfferNotes = v)
                .AddToggle(Settings.TechInfraOfferId, "reallife_infra_opt_offer", Settings.TechInfraOffer,
                    v => Settings.TechInfraOffer = v)
                .AddButton("reallife_tech_test_offer", () => Tech.TechDirector.Instance?.TestContractOffer())
                .AddButton("reallife_tech_test_tender", () => Tech.TechDirector.Instance?.TestTender())
                .AddButton("reallife_tech_test_move", () => Tech.TechDirector.Instance?.TestMove())
                .AddButton("reallife_tech_test_outage", () => Tech.TechDirector.Instance?.TestOutage())
                .AddButton("reallife_tech_test_breach", () => Tech.TechDirector.Instance?.TestBreach())
                .AddButton("reallife_infra_test_down", () => Tech.TechDirector.Instance?.TestCentreDown())
                .AddButton("reallife_infra_test_price_war", () => Tech.TechDirector.Instance?.TestPriceWar())
                .AddButton("reallife_infra_test_offer", () => Tech.TechDirector.Instance?.TestCentreOffer())
                .AddButton("reallife_tech_test_net", () => Tech.TechDirector.Instance?.TestNet())
                .AddToggle(Settings.TechStartupsId, "reallife_vc_enabled", Settings.TechStartups,
                    v => Settings.TechStartups = v)
                .AddButton("reallife_vc_test_pitch", () => Tech.TechDirector.Instance?.TestPitch())
                .AddButton("reallife_vc_test_sale", () => Tech.TechDirector.Instance?.TestSale())
                .AddButton("reallife_vc_test_round", () => Tech.TechDirector.Instance?.TestRound())
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
                .AddToggle(Settings.EconomyOnlineWaitNotesId, "reallife_online_opt_wait_notes", Settings.EconomyOnlineWaitNotes,
                    v => Settings.EconomyOnlineWaitNotes = v)
                .AddButton("reallife_online_test_open", () => Economy.OnlineDirector.Instance?.TestOpen())
                .AddButton("reallife_online_test_orders", () => Economy.OnlineDirector.Instance?.TestOrders())
                .AddButton("reallife_online_test_late", () => Economy.OnlineDirector.Instance?.TestLate())
                .AddButton("reallife_online_test_return", () => Economy.OnlineDirector.Instance?.TestReturn())
                .AddButton("reallife_online_test_day", () => Economy.OnlineDirector.Instance?.TestDay())
                .AddButton("reallife_online_test_back", () => Economy.OnlineDirector.Instance?.TestBack())
                .AddButton("reallife_online_test_net", () => Economy.OnlineDirector.Instance?.TestNet())
                .AddToggle(Settings.EconomyDealerId, "reallife_dealer_enabled", Settings.EconomyDealer,
                    v => Settings.EconomyDealer = v)
                .AddToggle(Settings.EconomyDealerFloorId, "reallife_showroom_floor_enabled", Settings.EconomyDealerFloor,
                    v => Settings.EconomyDealerFloor = v)
                .AddButton("reallife_dealer_test_stock", () => Economy.Dealership.DealerDirector.Instance?.TestStock())
                .AddButton("reallife_dealer_test_customers", () => Economy.Dealership.DealerDirector.Instance?.TestCustomers())
                .AddButton("reallife_dealer_test_auction", () => Economy.Dealership.DealerDirector.Instance?.TestAuction())
                .AddButton("reallife_dealer_test_week", () => Economy.Dealership.DealerDirector.Instance?.TestWeek())
                .AddButton("reallife_dealer_test_event", () => Economy.Dealership.DealerDirector.Instance?.TestEvent())
                .AddButton("reallife_dealer_test_arrive", () => Economy.Dealership.DealerDirector.Instance?.TestArrive())
                .AddButton("reallife_dealer_test_mission", () => Economy.Dealership.DealerDirector.Instance?.TestMission())
                .AddButton("reallife_dealer_test_back", () => Economy.Dealership.DealerDirector.Instance?.TestBack())
                .AddButton("reallife_showroom_test_floor", () => Economy.Dealership.ShowroomFloor.Instance?.TestFloor())
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
                .Guarded(o => o.AddDistrictView())   // the district view on the city map (Scripts/Districts)
                .Guarded(o => o.AddCityFaults())   // real faults in the city, and Flare's reports checked against them (Scripts/Faults)
                .Guarded(o => o.AddTexts())   // RLO Texts: a small text model on the player's PC words the city's messages (Scripts/Texts)
                .Guarded(o => o.AddOwnAi())   // Your own AI: RLO Texts' cards sent to an AI service the player chose, off unless chosen (Scripts/Texts)
                .Guarded(o => o.AddStreetTalk())   // talking to anybody in the street, in the game's own dialogue panel (Scripts/Street)
                .Guarded(o => o.AddPhotos())   // the Camera and Gallery apps on the phone (Scripts/Photos)
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
                .AddButton("reallife_construction_test_wrap", () => CivicDirector.Instance?.TestWrap())
                .AddButton("reallife_construction_test_rebuild", () => CivicDirector.Instance?.TestRebuild())
                .AddButton("reallife_construction_test_stage", () => CivicDirector.Instance?.TestStage())
                .AddButton("reallife_construction_test_storey", () => CivicDirector.Instance?.TestStorey())
                .AddButton("reallife_construction_test_crane", () => CivicDirector.Instance?.TestCrane())
                .AddSplitter()
                .AddHeader("reallife_util_header")
                .AddToggle(Settings.UtilitiesId, "reallife_util_enabled", Settings.Utilities,
                    v => Settings.Utilities = v)
                .AddToggle(Settings.UtilitiesNetSpeedId, "reallife_util_net_speed", Settings.UtilitiesNetSpeed,
                    v => Settings.UtilitiesNetSpeed = v)
                .AddButton("reallife_util_test_bill", () => Utilities.UtilitiesDirector.Instance?.TestBill())
                .AddButton("reallife_util_test_due", () => Utilities.UtilitiesDirector.Instance?.TestDue())
                .AddButton("reallife_util_test_cut", () => Utilities.UtilitiesDirector.Instance?.TestCutHome())
                .AddButton("reallife_util_test_restore", () => Utilities.UtilitiesDirector.Instance?.TestRestore())
                .AddButton("reallife_util_test_phone", () => Utilities.UtilitiesDirector.Instance?.TestByPhone())
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
                .AddToggle(Settings.FamilyWorkId, "reallife_family_work_opt", Settings.FamilyWork,
                    v => Settings.FamilyWork = v)
                .AddToggle(Settings.FamilyWealthMoodId, "reallife_family_wealth_opt", Settings.FamilyWealthMood,
                    v => Settings.FamilyWealthMood = v)
                .AddToggle(Settings.FamilyExpectId, "reallife_family_expect_opt", Settings.FamilyExpect,
                    v => Settings.FamilyExpect = v)
                .AddDropdown(Settings.FamilyInterestId, "reallife_family_interest",
                    new[] { "reallife_family_interest_anyone", "reallife_family_interest_women", "reallife_family_interest_men" }, Settings.FamilyInterest,
                    v => Settings.FamilyInterest = v)
                .AddSlider(Settings.FamilyYearWeeksId, "reallife_family_year_weeks", 1, 12, Settings.FamilyYearWeeks,
                    v => Settings.FamilyYearWeeks = v, Meta.SettingsTable.SliderValue)
                .AddButton("reallife_family_test_move_in", () => Family.FamilyDirector.Instance?.TestMoveIn())
                .AddButton("reallife_family_test_work", () => Family.FamilyDirector.Instance?.TestWork())
                .AddButton("reallife_family_test_wealth", () => Family.FamilyDirector.Instance?.TestWealth())
                .AddButton("reallife_family_test_wish", () => Family.FamilyDirector.Instance?.TestWish())
                .AddButton("reallife_family_test_expect", () => Family.FamilyDirector.Instance?.TestExpect())
                .AddButton("reallife_family_test_propose", () => Family.FamilyDirector.Instance?.TestPropose())
                .AddButton("reallife_family_test_plan", () => Family.FamilyDirector.Instance?.TestPlanWedding())
                .AddButton("reallife_family_test_wedding", () => Family.FamilyDirector.Instance?.TestWedding())
                .AddButton("reallife_family_test_child", () => Family.FamilyDirector.Instance?.TestChild(false))
                .AddButton("reallife_family_test_adopt", () => Family.FamilyDirector.Instance?.TestChild(true))
                .AddButton("reallife_family_test_year", () => Family.FamilyDirector.Instance?.TestYear())
                .AddButton("reallife_family_test_walk", () => Family.FamilyDirector.Instance?.TestWalk())
                .AddButton("reallife_family_test_argue", () => Family.FamilyDirector.Instance?.TestArgue())
                .AddButton("reallife_family_test_page", () => Family.FamilyDirector.Instance?.TestPage())
                .AddButton("reallife_family_test_clear", () => Family.FamilyDirector.Instance?.TestClear())
                .AddSplitter()
                .Guarded(o => o.AddPermadeath())   // an ending the player chooses, off unless switched on (Scripts/Permadeath)
                .Guarded(o => o.AddRoommates())
                .Guarded(o => Household.HouseholdOptions.AddHousehold(o))
                .Guarded(o => Walking.WalkOptions.AddWalking(o))
                .Guarded(o => Meals.MealsOptions.AddMeals(o))
                .Guarded(o => NewBuildings.NewBuildingsOptions.AddNewBuildings(o))   // the mod's own buildings (Scripts/NewBuildings)
                .Guarded(o => BuildTrades.TradesOptions.AddTrades(o))   // the building trades (Scripts/BuildTrades)
                .Guarded(o => Construction.ConstructionOptions.AddConstruction(o))   // the construction game (Scripts/Construction)
                .Guarded(o => o.AddControls())
                .Guarded(o => o.AddPerformance());

            if (OptionKeys.Finish(options, log))
            {
                Settings.Reload();   // a row's value was found only now: everything reads it before the page is put in order
            }
            // Meta puts the finished page in order (sections, presets, guide, test tools). Under the mod's own id, so the game's own
            // controls save where the mod reads, whatever the game calls this copy.
            OptionsService.Register(OptionKeysRules.FixedId, Guard.Rows(Guard.Organised(context, options)));   // the page as added, if putting it in order fails
            log.Info("Real Life Overhaul " + Meta.Changelog.Shown + " initialised.");
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
            OptionsService.RemoveModOptions(OptionKeysRules.FixedId);
            Keys.Dispose();
            EmpireView.Release();   // Empire Motors' driver's seat view as it was without this mod
            Economy.Dealership.DealerType.Unregister();
            Shops.ShopsDirector.Stop();   // the help pages' handler off the game's language event
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

        // One set of city features per city: the game starts a Workshop copy and a local build both when both are on
        // (ModDiscoveryRegistry looks at each place apart), so the second finds the first's root object by name. A root
        // gone with its scene is not found, so a load never followed by an unload cannot lock the mod out.
        private static string _running;   // the id of the copy whose features run (null in a copy that is another assembly)
        private bool _second;

        public Task OnLoadAsync(ModContext context)
        {
            if (_running != context.ModId && GameObject.Find(RootName) != null)
            {
                _second = true;
                RealLifeOverhaulInit.TwoCopies = true;
                context.Logger.Warn("Real Life Overhaul is already running in this city (two copies of the mod are switched on). This copy does nothing; "
                                    + "switch one of them off under Mods in the main menu.");
                return Task.CompletedTask;
            }
            _running = context.ModId;
            context = Meta.Dev.Gated(context);   // Info lines only while Developer diagnostics is on
            Guard.Step("the tech sector's business types", () => Tech.TechTypes.Register());   // furnished as the game's web development agency once its type is known
            Guard.Step("the training academy's business type", () => Careers.AcademyType.Register());   // furnished as the game's recruitment office once its type is known
            Guard.Step("the bank branch's business type", () => Economy.BranchType.Register());   // dressed as the city's banks once their type is known
            Guard.Step("the construction company's business type", () => Construction.ConstructionType.Register());   // furnished as the game's design studio once its type is known
            Guard.Step("the new kinds of shop", Shops.ShopsDirector.CityLoaded);   // trading as the game's shops they are made like once those are loaded
            Guard.Step("other authors' mods", () => OtherMods.Look(context.Logger));   // one walk over the loaded assemblies
            Guard.Step("GoingPublic", () => GoingPublic.Logger = context.Logger);
            Guard.Step("GoingPublicLink", () => GoingPublicLink.Logger = context.Logger);
            Guard.Step("SharedCity", () => SharedCity.Logger = context.Logger);
            Guard.Step("Going Public", () => GoingPublic.Reset());
            Guard.Step("Going Public", () => GoingPublic.Look());   // a session is known before any feature starts
            Guard.Step("CameraYield", () => CameraYield.Logger = context.Logger);
            Guard.Step("EmpireView", () => EmpireView.Logger = context.Logger);
            Guard.Step("GameUi.WorldPanel", () => GameUi.WorldPanel.Logger = context.Logger);
            Guard.Step("ThirdPersonCamera", () => ThirdPersonCamera.Logger = context.Logger);
            Guard.Step("VisualEnhancer", () => VisualEnhancer.Logger = context.Logger);
            Guard.Step("NearbyInteraction", () => NearbyInteraction.Logger = context.Logger);
            Guard.Step("ChaseCamera", () => ChaseCamera.Logger = context.Logger);
            Guard.Step("PoiDirection", () => PoiDirection.Logger = context.Logger);
            Guard.Step("ModState", () => ModState.Logger = context.Logger);
            Guard.Step("LifeEvents", () => LifeEvents.Logger = context.Logger);
            Guard.Step("VehicleTow", () => VehicleTow.Logger = context.Logger);
            Guard.Step("CrashConsequences", () => CrashConsequences.Logger = context.Logger);
            Guard.Step("TrafficAccidents", () => TrafficAccidents.Logger = context.Logger);
            Guard.Step("TowService", () => TowService.Logger = context.Logger);
            Guard.Step("TrafficTools", () => TrafficTools.Logger = context.Logger);
            Guard.Step("TrafficEffects", () => TrafficEffects.Logger = context.Logger);
            Guard.Step("ResponseUnit", () => ResponseUnit.Logger = context.Logger);
            Guard.Step("SceneCordon", () => SceneCordon.Logger = context.Logger);
            Guard.Step("AccidentCast", () => AccidentCast.Logger = context.Logger);
            Guard.Step("Shapes", () => Shapes.Logger = context.Logger);
            Guard.Step("PropLibrary", () => PropLibrary.Logger = context.Logger);
            Guard.Step("MapPlaces", () => MapPlaces.Logger = context.Logger);
            Guard.Step("SpecialBuildings", () => SpecialBuildings.Logger = context.Logger);
            Guard.Step("HeldUnits", () => HeldUnits.Logger = context.Logger);
            Guard.Step("MapLinks", () => MapLinks.Logger = context.Logger);
            Guard.Step("Districts.DistrictView", () => Districts.DistrictView.Logger = context.Logger);
            Guard.Step("Districts.DistrictPanel", () => Districts.DistrictPanel.Logger = context.Logger);
            Guard.Step("the modelled props", () => PropLibrary.Load(context));   // read in the background; plain shapes until then
            Guard.Step("PolicePursuit", () => PolicePursuit.Logger = context.Logger);
            Guard.Step("ActorPool", () => ActorPool.Logger = context.Logger);
            Guard.Step("CrimeDirector", () => CrimeDirector.Logger = context.Logger);
            Guard.Step("PoliceAttention", () => PoliceAttention.Logger = context.Logger);
            Guard.Step("BeatPatrol", () => BeatPatrol.Logger = context.Logger);
            Guard.Step("PolicePresence", () => PolicePresence.Logger = context.Logger);
            Guard.Step("RagdollSystem", () => RagdollSystem.Logger = context.Logger);
            Guard.Step("PedestrianIncidents", () => PedestrianIncidents.Logger = context.Logger);
            Guard.Step("CarStrikes", () => CarStrikes.Logger = context.Logger);
            Guard.Step("HornReactions", () => HornReactions.Logger = context.Logger);
            Guard.Step("Crossings", () => Crossings.Logger = context.Logger);
            Guard.Step("RecklessDrivers", () => RecklessDrivers.Logger = context.Logger);
            Guard.Step("SolidStreets", () => SolidStreets.Logger = context.Logger);
            Guard.Step("SolidGame", () => SolidGame.Logger = context.Logger);
            Guard.Step("CityDrivers", () => CityDrivers.Logger = context.Logger);
            Guard.Step("Walking.WalkDirector", () => Walking.WalkDirector.Logger = context.Logger);
            Guard.Step("Meals.MealsDirector", () => Meals.MealsDirector.Logger = context.Logger);
            Guard.Step("Networking.NetworkDirector", () => Networking.NetworkDirector.Logger = context.Logger);
            Guard.Step("DriverGame", () => DriverGame.Logger = context.Logger);
            Guard.Step("Jaywalker", () => Jaywalker.Logger = context.Logger);
            Guard.Step("PlayerKnockdown", () => PlayerKnockdown.Logger = context.Logger);
            Guard.Step("PhoneContacts", () => PhoneContacts.Logger = context.Logger);
            Guard.Step("ContactGroups", () => ContactGroupsGame.Logger = context.Logger);
            Guard.Step("GameFurniture", () => GameFurniture.Logger = context.Logger);
            Guard.Step("GameRegistrations", () => GameRegistrations.Logger = context.Logger);
            Guard.Step("ContactPictures", () => ContactPictures.Logger = context.Logger);
            Guard.Step("ContactSearch", () => ContactSearch.Logger = context.Logger);
            Guard.Step("CityLifeDirector", () => CityLifeDirector.Logger = context.Logger);
            Guard.Step("ClipLibrary", () => ClipLibrary.Logger = context.Logger);
            Guard.Step("the animation clips", () => ClipLibrary.Load(context));   // read in the background
            Guard.Step("JobDirector", () => JobDirector.Logger = context.Logger);
            Guard.Step("Law.LawDirector", () => Law.LawDirector.Logger = context.Logger);
            Guard.Step("TransitDirector", () => TransitDirector.Logger = context.Logger);
            Guard.Step("TaxiDirector", () => TaxiDirector.Logger = context.Logger);
            Guard.Step("CarOwnership", () => { CarOwnership.Logger = context.Logger; CarGarage.Logger = context.Logger; });
            Guard.Step("Events.EventDirector", () => Events.EventDirector.Logger = context.Logger);
            Guard.Step("SocialDirector", () => SocialDirector.Logger = context.Logger);
            Guard.Step("BusinessDirector", () => BusinessDirector.Logger = context.Logger);
            Guard.Step("JusticeDirector", () => JusticeDirector.Logger = context.Logger);
            Guard.Step("Licences", () => Licences.Logger = context.Logger);
            Guard.Step("LicenceDirector", () => LicenceDirector.Logger = context.Logger);
            Guard.Step("CivicDirector", () => CivicDirector.Logger = context.Logger);
            Guard.Step("Family.FamilyDirector", () => Family.FamilyDirector.Logger = context.Logger);
            Guard.Step("Tech.TechDirector", () => Tech.TechDirector.Logger = context.Logger);
            Guard.Step("Tech.HiringGame", () => Tech.HiringGame.Logger = context.Logger);
            Guard.Step("Tech.FirmsGame", () => Tech.FirmsGame.Logger = context.Logger);
            Guard.Step("Careers.CareersDirector", () => Careers.CareersDirector.Logger = context.Logger);
            Guard.Step("Floors.FloorsDirector", () => Floors.FloorsDirector.Logger = context.Logger);

            _root = new GameObject(RootName);
            Guard.Step("the city check", () => CityCheck.Begin(_root, context.Logger));   // before any feature: New York's features stand down in another city
            Guard.Step("the start line", () => StartLine.Logger = context.Logger);
            Guard.Step("the start line", () => StartLine.Begin());   // before any director's timer starts
            Guard.Step("the guard's notices", () => Guard.Add<GuardNotices>(_root));   // once the HUD is there
            Guard.Step("SessionWatch", () => SessionWatch.Logger = context.Logger);
            Guard.Step("SessionWatch", () => Guard.Add<SessionWatch>(_root));   // disabled unless Developer diagnostics is on
            Guard.Step("DayWarm", () => DayWarm.Logger = context.Logger);
            Guard.Step("DayWarm", () => Guard.Add<DayWarm>(_root));   // a slice a frame for some seconds after a load, then disabled
            Guard.Step("DayPump", () => DayPump.Logger = context.Logger);
            Guard.Step("DayPump", () => Guard.Add<DayPump>(_root));   // plays the midnight's later steps and held lines; disabled while nothing waits
            Guard.Step("GoingPublicWatch", () => Guard.Add<GoingPublicWatch>(_root));   // once a second of real time
            Guard.Step("ScenePrewarm", () => Guard.Add<ScenePrewarm>(_root));   // disabled unless a piece is waiting
            Guard.Step("LoadHold", () => Guard.Add<LoadHold>(_root));   // disabled unless a piece is waiting
            Guard.Step("NewGround.GroundDirector", () => NewGround.GroundDirector.Logger = context.Logger);
            Guard.Step("NewGround.GroundDirector", () => Guard.Add<NewGround.GroundDirector>(_root));   // before the traffic starts: a test road is appended here; disabled while idle
            // In the game's late call, after the road data below and once its traffic system is up (3.8 ms that waited for the
            // first black screen; RLO-PERF-024).
            Guard.Step("the lane grid's prewarm", () => ScenePrewarm.AddLate("roads: the lane points put in a grid for the nearest lane point", TrafficTools.PrewarmLaneGrid));
            Guard.Step("the roads' prewarm", () => ScenePrewarm.Add("roads: the traffic system's road data being found for the city", RoadData.Prewarm));
            Guard.Step("the smoke's prewarm", () => ScenePrewarm.Add("traffic: the game's smoke effect being found for accidents and fires", TrafficEffects.Prewarm));
            Guard.Step("the sirens' prewarm", () => ScenePrewarm.Add("traffic: the sirens' sounds being made", TrafficEffects.PrewarmSirens));
            Guard.Step("the font's prewarm", () => ScenePrewarm.Add("game UI: the game's font being found", FindFont));
            Guard.Step("the bus stops' prewarm", () => ScenePrewarm.Add("transit: the game's navigation obstacles being listed for the bus stops", BusZones.Prewarm));
            Guard.Step("GameUi.NoticeLook", () => GameUi.NoticeLook.Logger = context.Logger);
            Guard.Step("GameUi.NoticeRoom", () => GameUi.NoticeRoom.Logger = context.Logger);
            Guard.Step("GameUi.NoticeLook", () => Guard.Add<GameUi.NoticeLook>(_root));   // nothing per frame
            Guard.Step("the notices' prewarm", () => ScenePrewarm.Add("game UI: the game's notification toast and list being found, and the notifications' pictures", GameUi.NoticeLook.Prewarm));
            // Run first (pieces run last added first): every picture the features named in their Awake, in one look.
            Guard.Step("the pictures' prewarm", () => ScenePrewarm.Add("game UI: the game's interface pictures being found by name (everything in memory gone through)", GameUi.GameSprites.Prewarm));
            Guard.Step("ClickWatch", () => ClickWatch.Logger = context.Logger);
            Guard.Step("OverlayGuard", () => OverlayGuard.Logger = context.Logger);
            Guard.Step("OverlayGuard", () => Guard.Add<OverlayGuard>(_root));   // two null tests a frame
            Guard.Step("MapTreeGuard", () => MapTreeGuard.Logger = context.Logger);
            Guard.Step("MapTreeGuard", () => Guard.Add<MapTreeGuard>(_root));   // a reference comparison a frame; the tree list pruned when the map opens
            Guard.Step("SceneryCulling", () => SceneryCulling.Logger = context.Logger);   // the culling check's line as the city unloads
            Guard.Step("GameUi.TextBox", () => GameUi.TextBox.Logger = context.Logger);
            // A conversation ended another way: the text box closed and the dialogue panel's handle given back; one time
            // comparison a frame.
            Guard.Step("GameUi.ConversationWatch", () => Guard.Add<GameUi.ConversationWatch>(_root));
            Guard.Step("GameUi.GuideMark", () => GameUi.GuideMark.Logger = context.Logger);
            Guard.Step("GameUi.GuideMarkWatch", () => Guard.Add<GameUi.GuideMarkWatch>(_root));   // one time comparison a frame
            Guard.Step("DayRollGuard", () => DayRollGuard.Logger = context.Logger);
            Guard.Step("DayRollGuard", () => Guard.Add<DayRollGuard>(_root));   // no Update, one comparison a game hour
            Guard.Step("RivalOfferGuard", () => RivalOfferGuard.Logger = context.Logger);
            Guard.Step("RivalOfferGuard", () => Guard.Add<RivalOfferGuard>(_root));   // no Update
            Guard.Step("StaffLeave", () => StaffLeave.Logger = context.Logger);
            Guard.Step("SpecialNames", () => Guard.Add<SpecialNames>(_root));   // one reference comparison a frame while a building is named
            Guard.Step("HeldUnits", () => Guard.Add<HeldUnits>(_root));   // no Update; a look once a second, two at the game's night
            Guard.Step("ClickWatch", () => Guard.Add<ClickWatch>(_root));   // one flag test a frame unless Developer diagnostics is on
            Guard.Step("ThirdPersonCamera", () => Guard.Add<ThirdPersonCamera>(_root));
            Guard.Step("VisualEnhancer", () => Guard.Add<VisualEnhancer>(_root));
            Guard.Step("NearbyInteraction", () => Guard.Add<NearbyInteraction>(_root));
            Guard.Step("Walking.WalkDirector", () => Guard.Add<Walking.WalkDirector>(_root));   // one key read a frame
            Guard.Step("ChaseCamera", () => Guard.Add<ChaseCamera>(_root));
            Guard.Step("PoiDirection", () => Guard.Add<PoiDirection>(_root));
            Guard.Step("LifeEvents", () => Guard.Add<LifeEvents>(_root));
            Guard.Step("CrashConsequences", () => Guard.Add<CrashConsequences>(_root));
            Guard.Step("RagdollSystem", () => Guard.Add<RagdollSystem>(_root));   // before anyone who throws people down
            Guard.Step("ActorPool", () => Guard.Add<ActorPool>(_root));   // before the scenes that put people on the street
            Guard.Step("TrafficAccidents", () => Guard.Add<TrafficAccidents>(_root));
            Guard.Step("TowService", () => Guard.Add<TowService>(_root));   // disabled while there is nothing to tow
            Guard.Step("PolicePursuit", () => Guard.Add<PolicePursuit>(_root));
            Guard.Step("StreetCrime", () => Guard.Add<StreetCrime>(_root));
            Guard.Step("CrimeDirector", () => Guard.Add<CrimeDirector>(_root));   // the scenes StreetCrime forwards to
            Guard.Step("PoliceAttention", () => Guard.Add<PoliceAttention>(_root));
            Guard.Step("BeatPatrol", () => Guard.Add<BeatPatrol>(_root));
            Guard.Step("PolicePresence", () => Guard.Add<PolicePresence>(_root));
            Guard.Step("PedestrianIncidents", () => Guard.Add<PedestrianIncidents>(_root));
            Guard.Step("CarStrikes", () => Guard.Add<CarStrikes>(_root));
            Guard.Step("HornReactions", () => Guard.Add<HornReactions>(_root));
            Guard.Step("RecklessDrivers", () => Guard.Add<RecklessDrivers>(_root));   // after CarStrikes and TrafficAccidents
            Guard.Step("SolidStreets", () => Guard.Add<SolidStreets>(_root));   // after HornReactions, CarStrikes and RecklessDrivers
            Guard.Step("CityDrivers", () => Guard.Add<CityDrivers>(_root));   // after SolidStreets, which it leaves people to
            Guard.Step("PlayerKnockdown", () => Guard.Add<PlayerKnockdown>(_root));
            Guard.Step("CityLifeDirector", () => Guard.Add<CityLifeDirector>(_root));   // its own pool of eight
            Guard.Step("JobDirector", () => Guard.Add<JobDirector>(_root));
            Guard.Step("HomeDirector", () => HomeDirector.Logger = context.Logger);
            Guard.Step("Meals.MealsDirector", () => Guard.Add<Meals.MealsDirector>(_root));   // meals at a seat and dancing to the music
            Guard.Step("HomeDirector", () => Guard.Add<HomeDirector>(_root));   // after LifeEvents and CityLifeDirector, whose clip player it shares
            Guard.Step("Law.LawDirector", () => Guard.Add<Law.LawDirector>(_root));
            Guard.Step("TransitDirector", () => Guard.Add<TransitDirector>(_root));
            Guard.Step("TaxiDirector", () => Guard.Add<TaxiDirector>(_root));
            Guard.Step("CarOwnership", () => Guard.Add<CarOwnership>(_root));
            Guard.Step("Leisure.LeisureDirector", () => Leisure.LeisureDirector.Logger = context.Logger);
            Guard.Step("Leisure.LeisureDirector", () => Guard.Add<Leisure.LeisureDirector>(_root));
            Guard.Step("Events.EventDirector", () => Guard.Add<Events.EventDirector>(_root));   // after CityLifeDirector, whose clip player it uses
            Guard.Step("SocialDirector", () => Guard.Add<SocialDirector>(_root));
            Guard.Step("TravelDirector", () => TravelDirector.Logger = context.Logger);
            Guard.Step("TravelDirector", () => Guard.Add<TravelDirector>(_root));
            Guard.Step("BusinessDirector", () => Guard.Add<BusinessDirector>(_root));
            Guard.Step("JusticeDirector", () => Guard.Add<JusticeDirector>(_root));
            Guard.Step("LicenceDirector", () => Guard.Add<LicenceDirector>(_root));
            Guard.Step("GameUi.UiClone", () => GameUi.UiClone.Logger = context.Logger);
            Guard.Step("GameUi.GameSprites", () => GameUi.GameSprites.Logger = context.Logger);
            Guard.Step("GameUi.GameFont", () => GameUi.GameFont.Logger = context.Logger);
            Guard.Step("GameUi.AppIconPainter", () => GameUi.AppIconPainter.Logger = context.Logger);
            Guard.Step("GameUi.PhoneApp", () => GameUi.PhoneApp.Logger = context.Logger);
            Guard.Step("GameUi.PhoneGridPager", () => GameUi.PhoneGridPager.Logger = context.Logger);
            Guard.Step("GameUi.PhoneLayoutPrefs", () => GameUi.PhoneLayoutPrefs.Logger = context.Logger);
            Guard.Step("GameUi.PhoneMove", () => GameUi.PhoneMove.Logger = context.Logger);
            Guard.Step("GameUi.AppShell", () => GameUi.AppShell.Logger = context.Logger);
            Guard.Step("GameUi.InfoBox", () => GameUi.InfoBox.Logger = context.Logger);
            Guard.Step("GameUi.HelpFrame", () => GameUi.HelpFrame.Logger = context.Logger);
            Guard.Step("GameUi.OptionRows", () => GameUi.OptionRows.Logger = context.Logger);
            Guard.Step("Planner.PlannerApp", () => Planner.PlannerApp.Logger = context.Logger);
            Guard.Step("Planner.PlannerDirector", () => Planner.PlannerDirector.Logger = context.Logger);
            Guard.Step("Calendar.CalendarDirector", () => Calendar.CalendarDirector.Logger = context.Logger);
            Guard.Step("Calendar.CalendarApp", () => Calendar.CalendarApp.Logger = context.Logger);
            Guard.Step("Calendar.CalendarDirector", () => Guard.Add<Calendar.CalendarDirector>(_root));
            Guard.Step("Hustles.HustleDirector", () => Hustles.HustleDirector.Logger = context.Logger);
            Guard.Step("Hustles.HustleApp", () => Hustles.HustleApp.Logger = context.Logger);
            Guard.Step("Hustles.HustleDirector", () => Guard.Add<Hustles.HustleDirector>(_root));   // the Marketplace: used goods and cars, freelance visits (Scripts/Hustles)
            Guard.Step("Seasons.SeasonDirector", () => Seasons.SeasonDirector.Logger = context.Logger);
            Guard.Step("Seasons.SeasonDirector", () => Guard.Add<Seasons.SeasonDirector>(_root));
            Guard.Step("Planner.PlannerDirector", () => Guard.Add<Planner.PlannerDirector>(_root));
            Guard.Step("Economy.EconomyDirector", () => Economy.EconomyDirector.Logger = context.Logger);
            Guard.Step("Economy.FinanceApp", () => Economy.FinanceApp.Logger = context.Logger);
            Guard.Step("Economy.EconomyDirector", () => Guard.Add<Economy.EconomyDirector>(_root));
            Guard.Step("Economy.BanksDirector", () => Economy.BanksDirector.Logger = context.Logger);
            Guard.Step("Economy.BanksDirector", () => Guard.Add<Economy.BanksDirector>(_root));
            Guard.Step("Economy.CityEconomyDirector", () => Economy.CityEconomyDirector.Logger = context.Logger);
            Guard.Step("Economy.CityEconomyDirector", () => Guard.Add<Economy.CityEconomyDirector>(_root));
            Guard.Step("Economy.Ventures.VenturesDirector", () => Economy.Ventures.VenturesDirector.Logger = context.Logger);
            Guard.Step("Economy.Ventures.VenturesApp", () => Economy.Ventures.VenturesApp.Logger = context.Logger);
            Guard.Step("Economy.Ventures.VenturesDirector", () => Guard.Add<Economy.Ventures.VenturesDirector>(_root));
            Guard.Step("Markets.MarketDirector", () => Markets.MarketDirector.Logger = context.Logger);
            Guard.Step("Markets.MarketTapeFile", () => Markets.MarketTapeFile.Logger = context.Logger);
            Guard.Step("Markets.MarketApp", () => Markets.MarketApp.Logger = context.Logger);
            Guard.Step("Markets.MarketDirector", () => Guard.Add<Markets.MarketDirector>(_root));   // after Ventures, whose old market it takes over
            Guard.Step("Ledger.LedgerDirector", () => Ledger.LedgerDirector.Logger = context.Logger);
            Guard.Step("Ledger.LedgerDirector", () => Guard.Add<Ledger.LedgerDirector>(_root));
            Guard.Step("Economy.OnlineDirector", () => Economy.OnlineDirector.Logger = context.Logger);
            Guard.Step("Economy.OnlineApp", () => Economy.OnlineApp.Logger = context.Logger);
            Guard.Step("Economy.Dealership.DealerDirector", () => Economy.Dealership.DealerDirector.Logger = context.Logger);
            Guard.Step("Economy.Dealership.DealerApp", () => Economy.Dealership.DealerApp.Logger = context.Logger);
            Guard.Step("Economy.Dealership.DealerDirector", () => Guard.Add<Economy.Dealership.DealerDirector>(_root));   // before the company, so that its day comes first
            Guard.Step("Economy.Dealership.ShowroomFloor", () => Economy.Dealership.ShowroomFloor.Logger = context.Logger);
            Guard.Step("Economy.Dealership.ShowroomView", () => Economy.Dealership.ShowroomView.Logger = context.Logger);
            Guard.Step("Economy.Dealership.ShowroomFloor", () => Guard.Add<Economy.Dealership.ShowroomFloor>(_root));
            Guard.Step("Economy.Dealership.ShowroomView", () => Guard.Add<Economy.Dealership.ShowroomView>(_root));
            Guard.Step("Economy.OnlineDirector", () => Guard.Add<Economy.OnlineDirector>(_root));   // before the company, so that its day comes first
            Guard.Step("Economy.CompanyDirector", () => Economy.CompanyDirector.Logger = context.Logger);
            Guard.Step("Economy.CompanyApp", () => Economy.CompanyApp.Logger = context.Logger);
            Guard.Step("Economy.ServicesDirector", () => Economy.ServicesDirector.Logger = context.Logger);
            Guard.Step("Economy.ServicesApp", () => Economy.ServicesApp.Logger = context.Logger);
            Guard.Step("Economy.ServicesDirector", () => Guard.Add<Economy.ServicesDirector>(_root));
            Guard.Step("Economy.CompanyDirector", () => Guard.Add<Economy.CompanyDirector>(_root));
            Guard.Step("Tech.TechDirector", () => Guard.Add<Tech.TechDirector>(_root));   // after the company, so that its day follows the company's
            Guard.Step("Careers.CareersDirector", () => Guard.Add<Careers.CareersDirector>(_root));   // after the tech sector, whose roles it reads
            Guard.Step("FitOut.FitOutDirector", () => FitOut.FitOutDirector.Logger = context.Logger);
            Guard.Step("FitOut.FitOutDirector", () => Guard.Add<FitOut.FitOutDirector>(_root));   // before the features whose floors it builds
            Guard.Step("Floors.FloorsDirector", () => Guard.Add<Floors.FloorsDirector>(_root));   // a look once a second: the places on floors of the mod's buildings
            Guard.Step("Tech.TechSites", () => Tech.TechSites.Logger = context.Logger);
            Guard.Step("Offices.Conversions", () => Offices.Conversions.Logger = context.Logger);
            Guard.Step("Offices.Conversions", () => Guard.Add<Offices.Conversions>(_root));   // a look once a second
            Guard.Step("Tech.TechSites", () => Guard.Add<Tech.TechSites>(_root));   // a look once a second
            Guard.Step("Economy.EstateDirector", () => Economy.EstateDirector.Logger = context.Logger);
            Guard.Step("Economy.EstateApp", () => Economy.EstateApp.Logger = context.Logger);
            Guard.Step("Economy.EstateDirector", () => Guard.Add<Economy.EstateDirector>(_root));
            Guard.Step("Economy.OwnBankDirector", () => Economy.OwnBankDirector.Logger = context.Logger);
            Guard.Step("Economy.BranchFloor", () => Economy.BranchFloor.Logger = context.Logger);
            Guard.Step("Economy.BranchFloor", () => Guard.Add<Economy.BranchFloor>(_root));
            Guard.Step("Economy.OwnBankDirector", () => Guard.Add<Economy.OwnBankDirector>(_root));   // after the company, so that its day's books come after the company's
            Guard.Step("Economy.Brands.BrandsDirector", () => Economy.Brands.BrandsDirector.Logger = context.Logger);
            Guard.Step("Economy.Brands.BrandsApp", () => Economy.Brands.BrandsApp.Logger = context.Logger);
            Guard.Step("Economy.Brands.BrandsDirector", () => Guard.Add<Economy.Brands.BrandsDirector>(_root));   // after the company: its day follows the company's
            Guard.Step("Nav.NavGraph", () => Nav.NavGraph.Logger = context.Logger);
            Guard.Step("Nav.NavMarks", () => Nav.NavMarks.Logger = context.Logger);
            Guard.Step("Nav.NavHud", () => Nav.NavHud.Logger = context.Logger);
            Guard.Step("Nav.NavBase", () => Nav.NavBase.Logger = context.Logger);
            Guard.Step("Nav.NavCityMap", () => Nav.NavCityMap.Logger = context.Logger);
            Guard.Step("Nav.NavDirector", () => Nav.NavDirector.Logger = context.Logger);
            Guard.Step("Nav.NavDirector", () => Guard.Add<Nav.NavDirector>(_root));
            Guard.Step("CivicDirector", () => Guard.Add<CivicDirector>(_root));   // its own cast of five
            Guard.Step("Faults.FaultDirector", () => Faults.FaultDirector.Logger = context.Logger);
            Guard.Step("Faults.FaultDirector", () => Guard.Add<Faults.FaultDirector>(_root));
            Guard.Step("Texts.TextsDirector", () => Texts.TextsDirector.Logger = context.Logger);
            Guard.Step("Texts.TextsDirector", () => Guard.Add<Texts.TextsDirector>(_root));   // twice a second a look
            Guard.Step("Street.StreetDirector", () => Street.StreetDirector.Logger = context.Logger);
            Guard.Step("Street.StreetDirector", () => Guard.Add<Street.StreetDirector>(_root));   // four looks a second on foot
            Guard.Step("Utilities.UtilitiesDirector", () => Utilities.UtilitiesDirector.Logger = context.Logger);
            Guard.Step("Utilities.BroadbandCompany", () => Utilities.BroadbandCompany.Logger = context.Logger);
            Guard.Step("Utilities.UtilitiesDirector", () => Guard.Add<Utilities.UtilitiesDirector>(_root));
            Guard.Step("Family.FamilyDirector", () => Guard.Add<Family.FamilyDirector>(_root));
            Guard.Step("the roommate", () => Roommates.RoommateDirector.Attach(_root, context.Logger));
            Guard.Step("household staff", () => Household.HouseholdDirector.Attach(_root, context.Logger));
            Guard.Step("Flare.FlareDirector", () => Flare.FlareDirector.Logger = context.Logger);
            Guard.Step("Flare.FlareApp", () => Flare.FlareApp.Logger = context.Logger);
            Guard.Step("Flare.FlareDirector", () => Guard.Add<Flare.FlareDirector>(_root));   // after the company and the brands, so that its day follows theirs
            Guard.Step("Fame.FameDirector", () => Fame.FameDirector.Logger = context.Logger);
            Guard.Step("Fame.FameDirector", () => Guard.Add<Fame.FameDirector>(_root));   // after Flare and the company, so that its day reads theirs
            Guard.Step("Rivals.RivalDirector", () => Rivals.RivalDirector.Logger = context.Logger);
            Guard.Step("Rivals.RivalDirector", () => Guard.Add<Rivals.RivalDirector>(_root));   // after Standing
            Guard.Step("PermadeathDirector", () => PermadeathDirector.Logger = context.Logger);
            Guard.Step("PermadeathDirector", () => Guard.Add<PermadeathDirector>(_root));
            Guard.Step("CarTuning", () => CarTuning.Logger = context.Logger);
            Guard.Step("SpeedReading", () => SpeedReading.Logger = context.Logger);
            Guard.Step("CarMeasure", () => CarMeasure.Logger = context.Logger);
            Guard.Step("CarTuning", () => Guard.Add<CarTuning>(_root));
            Guard.Step("Speedometer", () => Guard.Add<Speedometer>(_root));
            Guard.Step("GarageDirector", () => GarageDirector.Logger = context.Logger);
            Guard.Step("GarageShop", () => GarageShop.Logger = context.Logger);
            Guard.Step("GarageCar", () => GarageCar.Logger = context.Logger);
            Guard.Step("GarageTuning", () => GarageTuning.Logger = context.Logger);
            Guard.Step("TwoWheelers", () => TwoWheelers.Logger = context.Logger);
            Guard.Step("BikeMount", () => BikeMount.Logger = context.Logger);
            Guard.Step("BikePlaces", () => BikePlaces.Logger = context.Logger);
            Guard.Step("GarageDirector", () => Guard.Add<GarageDirector>(_root));
            Guard.Step("CarSoundDirector", () => CarSoundDirector.Logger = context.Logger);
            Guard.Step("CarSoundBank", () => CarSoundBank.Logger = context.Logger);
            Guard.Step("the recorded car sounds", () => CarSoundBank.Load(context));   // read in the background, once a session
            Guard.Step("CarSoundDirector", () => Guard.Add<CarSoundDirector>(_root));
            Guard.Step("PhoneSkin", () => PhoneSkin.Logger = context.Logger);
            Guard.Step("PhoneSkin", () => Guard.Add<PhoneSkin>(_root));
            Guard.Step("Photos.PhotosDirector", () => Photos.PhotosDirector.Logger = context.Logger);
            Guard.Step("Photos.PhotosDirector", () => Guard.Add<Photos.PhotosDirector>(_root));   // nothing per frame while shut
            Guard.Step("PerfDirector", () => PerfDirector.Logger = context.Logger);
            Guard.Step("PerfDirector", () => Guard.Add<PerfDirector>(_root));
            Guard.Step("MapLinks", () => Guard.Add<MapLinks>(_root));   // every feature's places on the city map; no feature knows of it
            Guard.Step("Districts.DistrictView", () => Guard.Add<Districts.DistrictView>(_root));
            Guard.Step("Interiors.InteriorDirector", () => Interiors.InteriorDirector.Logger = context.Logger);
            Guard.Step("CustomerDraw", () => CustomerDraw.Logger = context.Logger);
            Guard.Step("BizPace", () => BizPace.BizPaceWatch.Logger = context.Logger);   // what the mod adds to BizMan's open, switch and tab change, timed with Developer diagnostics on
            // With Developer diagnostics on, the installation firms' lists checked against the designs written.
            Guard.Step("Designs.DesignsDirector", () => Designs.DesignsDirector.CityLoaded());
            Guard.Step("Interiors.InteriorDirector", () => Guard.Add<Interiors.InteriorDirector>(_root));
            Guard.Step("Uptown.GroundDirector", () => Uptown.GroundDirector.Logger = context.Logger);
            // Uptown's ground is laid in its Awake, before the traffic starts and before the city's buildings are made on its lots.
            Guard.Step("Uptown.GroundDirector", () => Guard.Add<Uptown.GroundDirector>(_root));
            Guard.Step("Uptown.LifeDirector", () => Uptown.LifeDirector.Logger = context.Logger);
            Guard.Step("Uptown.LifeDirector", () => Guard.Add<Uptown.LifeDirector>(_root));   // after Uptown's ground's director: its buildings declared for the made buildings below
            Guard.Step("Schools.SchoolsDirector", () => Schools.SchoolsDirector.Logger = context.Logger);
            // Birchwood School: its main school declared for the made buildings below, its other buildings asked of the copy engine.
            Guard.Step("Schools.SchoolsDirector", () => Guard.Add<Schools.SchoolsDirector>(_root));
            Guard.Step("Outlook.OutlookDirector", () => Outlook.OutlookDirector.Logger = context.Logger);
            Guard.Step("Outlook.OutlookApp", () => Outlook.OutlookApp.Logger = context.Logger);
            // The Economy app: after the city's economy, the market and the council, whose figures it reads; its day follows the city's. No Update.
            Guard.Step("Outlook.OutlookDirector", () => Guard.Add<Outlook.OutlookDirector>(_root));
            Guard.Step("NewBuildings.NewBuildingsDirector", () => NewBuildings.NewBuildingsDirector.Logger = context.Logger);
            // Its buildings are made behind the loading screen, once every director has declared its own.
            Guard.Step("NewBuildings.NewBuildingsDirector", () => Guard.Add<NewBuildings.NewBuildingsDirector>(_root));
            Guard.Step("BuildTrades.TradesDirector", () => BuildTrades.TradesDirector.Logger = context.Logger);
            Guard.Step("BuildTrades.TradesDirector", () => Guard.Add<BuildTrades.TradesDirector>(_root));   // the practice's floor, its panel, the trades' line; no Update
            Guard.Step("Construction.ConstructionDirector", () => Construction.ConstructionDirector.Logger = context.Logger);
            Guard.Step("Construction.ConstructionApp", () => Construction.ConstructionApp.Logger = context.Logger);
            // The company, its projects and the city's builders: a time comparison a frame.
            Guard.Step("Construction.ConstructionDirector", () => Guard.Add<Construction.ConstructionDirector>(_root));
            Guard.Step("Landmarks.LandmarksDirector", () => Landmarks.LandmarksDirector.Logger = context.Logger);
            // Uptown's landmarks: a time comparison a frame; their pieces stood in the game's late call.
            Guard.Step("Landmarks.LandmarksDirector", () => Guard.Add<Landmarks.LandmarksDirector>(_root));
            Guard.Step("the wiring", () => Wiring.Connect(context.Logger, _root));   // every director is there: the bridges are filled in
            // Behind the loading screen: the two street pools' people made and dressed where the time is not seen; each
            // falls back to making them as needed.
            Guard.Step("the looks", () => Looks.Reset());
            Guard.Step("the city life prewarm", () => CityLifeDirector.Instance?.Prewarm());
            Guard.Step("the street pool prewarm", () => ActorPool.Instance?.Prewarm());
            Guard.Step("the game's pictures", WantPictures);   // the helpers' names; each director named its own in its Awake
            // Every whole-scene search a feature would otherwise make in play, at over 100 ms each.
            Guard.Step("the scene prewarm", () => ScenePrewarm.Begin());
            Guard.Step("the options page in the city", () => Meta.MetaCity.Begin(context, _root));   // the check for leftovers, the guide's contact, the welcome panel
            Guard.Step("DistrictSave.DistrictSaveDirector", () => DistrictSave.DistrictSaveDirector.Logger = context.Logger);
            Guard.Step("Uptown's buildings", () => Uptown.LifeDirector.Settle());   // the made buildings stand now: Uptown's into the game's lists, before the district save puts back theirs
            Guard.Step("the district save", () => DistrictSave.DistrictSaveDirector.City(_root));   // after every step that makes places of the mod's
            context.Logger.Info("City features started.");
            // The loading screen stays up while pieces that need frames of their own are done, two seconds at most.
            return LoadHold.Wait();
        }

        // The game's pictures that the helpers and the shell ask for (the round of an InfoBox and of the districts' panel, More
        // apps and the folders, the Planner's place), named before the one look behind the loading screen.
        private static void WantPictures()
        {
            GameUi.GameSprites.Want("Circle-20", "circle", GameUi.PhoneGridPager.MoreGlyph, "Icon-SetDestination");
            GameUi.GameSprites.Want(ContactGroups.Glyphs);   // the Contacts app's group buttons
            GameUi.NoticeLook.WantPictures();
            Meta.GuideApp.WantPictures();
            Meta.SettingsApp.WantPictures();
            Meta.StaffApp.WantPictures();
            Ledger.NewsstandApp.WantPictures();
            SpeedReading.WantPictures();
            CarDash.WantPictures();
            HomeNeedsBar.WantPictures();
        }

        // The game's font is found by a scene search and kept: asked for here, not when the first sign is lettered.
        private static bool FindFont()
        {
            return GameUi.GameFont.Refind();
        }

        public Task OnUnloadAsync()
        {
            if (_second)
            {
                _second = false;
                return Task.CompletedTask;   // this copy started nothing
            }
            _running = null;
            Guard.Step("the options page in the city", () => Meta.MetaCity.End());
            Guard.Step("the wiring", () => Wiring.Disconnect());
            Guard.Step("the guide marks", () => GameUi.GuideMark.HideAll("the city is unloading"));   // while the game's destination guider is still there
            Guard.Step("the culling check", SceneryCulling.WatchUnload);   // the game's culling lists counted once the menu's scene is in, the city's still there
            if (_root != null)
            {
                _root.name = RootName + " (closing)";   // gone at the end of the frame: a city loaded before then must not take it for a second copy
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            Guard.Step("the map places", () => MapPlaces.Shutdown());
            Guard.Step("the modelled props", () => PropLibrary.Dispose());
            Guard.Step("the city check", CityCheck.End);   // every feature back for the menu and the next city
            return Task.CompletedTask;
        }
    }
}
