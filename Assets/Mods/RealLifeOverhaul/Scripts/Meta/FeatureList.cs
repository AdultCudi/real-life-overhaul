using System.Globalization;
using System.Text;
using BigAmbitions.Mods;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Every feature of the mod, one statement each: all that the options page, the presets, the Guide, the save tools and
    /// the diagnostics switch know of a feature; nothing else in Meta names one. A new feature adds its statement at the end
    /// of <see cref="RegisterAll"/> (or in a static DescribeFeature() of its own class marked [DescribesFeature]); the
    /// statement and its calls are in docs/contributing/adding-things.md.
    /// </summary>
    internal static partial class FeatureList
    {
        public static void RegisterAll()
        {
            RegisterGameplay();
            RegisterTools();
            RegisterLife();
            RegisterEconomy();
            Roommates.RoommateGuide.Register();
            Permadeath.PermadeathGuide.Register();
            Faults.FaultGuide.Register();
            Texts.TextsGuide.Register();
            Street.StreetGuide.Register();
            Districts.DistrictGuide.Register();
            Economy.OwnBankGuide.Register();
            EndScaleGuide.Register();
            Rivals.RivalGuide.Register();
            Photos.PhotosGuide.Register();
        }

        private static void RegisterGameplay()
        {
            Features.Add("camera", "reallife_meta_title_camera")
                .Group("reallife_camera_header")
                .About("reallife_guide_camera_about").Help("reallife_guide_camera")
                .Owns(Settings.CameraEnabledId, Settings.MouseLookId, Settings.MouseSensitivityId, Settings.EnterWithKeyId, Settings.CameraIndoorsId,
                    Settings.CameraPitchId, Settings.CameraDistanceId, Settings.CameraFovId, Settings.CameraHeightId, Settings.CarCameraId,
                    Settings.CarCameraDistanceId, Settings.CameraFollowId)
                .StandsAside(CameraYield.Sentence, Settings.CameraEnabledId, Settings.MouseLookId, Settings.MouseSensitivityId, Settings.CameraIndoorsId,
                    Settings.CameraPitchId, Settings.CameraDistanceId, Settings.CameraFovId, Settings.CameraHeightId, Settings.CarCameraId,
                    Settings.CarCameraDistanceId, Settings.CameraFollowId)
                .Cleanup(() =>
                {
                    ThirdPersonCamera.Focus = null;
                    ThirdPersonCamera.StandAside();   // the figures, the pointer and the bridge back to the game
                    ChaseCamera.LetGo();
                });

            Features.Add("character", "reallife_meta_title_character")
                .Group("reallife_looks_header")
                .About("reallife_guide_character_about").Help("reallife_guide_character")
                .Fact("clothes_least", () => Guide.Money(CharacterCatalogue.CheapestPiece))
                .Fact("clothes_most", () => Guide.Money(CharacterCatalogue.DearestPiece))
                .Owns(Settings.CharacterHighDetailId, Settings.CharacterHairId, Settings.CharacterLightingId, Settings.CharacterNewLooksId)
                .Test("reallife_test_hair_say", HairTests.Say)
                .Test("reallife_test_hair_lamp", HairTests.Lamp)
                .Test("reallife_test_hair_streetlamp", HairTests.StreetLamp)
                .Test("reallife_test_hair_switch", HairTests.Switch)
                .Cleanup(HairTests.Forget)
                .Data("reallife:look_*")
                .Objects("RealLifeOverhaul Character");
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                SettingsTable.Declare(slot.OptionId, OptionType.Choice, 0);   // "the game's own"
            }

            Features.Add("citylife", "reallife_meta_title_citylife")
                .Group("reallife_city_header")
                .About("reallife_guide_citylife_about").Help("reallife_guide_citylife")
                .Owns(Settings.CityLifeId, Settings.CityDensityId, Settings.CityWorkersId, Settings.CityEverydayId, Settings.CityInteractionsId)
                .Fact("coffee_price", () => Guide.Money(StallActivity.Price))
                .Fact("city_people", () => CityPeople.MaxPeople.ToString(CultureInfo.InvariantCulture))
                .Fact("city_sites", () => CityLifeDirector.MaxSites.ToString(CultureInfo.InvariantCulture))
                .Fact("city_count", () => CityCatalogue.All.Length.ToString(CultureInfo.InvariantCulture))
                .Cleanup(() => CityLifeDirector.Instance?.EndAll());

            Features.Add("crime", "reallife_meta_title_crime")
                .Group("reallife_crime_header")
                .About("reallife_guide_crime_about").Help("reallife_guide_crime")
                .Fact("pick_least", () => Guide.Money(Prices.At(PlayerPickpocketScene.Least)))
                .Fact("pick_most", () => Guide.Money(Prices.At(PlayerPickpocketScene.Most)))
                .Fact("mug_least", () => Guide.Money(Prices.At(PlayerMuggingScene.Least)))
                .Fact("mug_most", () => Guide.Money(Prices.At(PlayerMuggingScene.Most)))
                .Fact("mug_refused_most", () => Guide.Money(Prices.At(PlayerMuggingScene.Most / 2)))
                .Fact("crime_cash_least", () => Guide.Money(Prices.At(CrimeDirector.CashLeast)))
                .Fact("holdup_least", () => Guide.Money(Prices.At(ShopIncidentScene.HoldUpLeast)))
                .Fact("holdup_most", () => Guide.Money(Prices.At(ShopIncidentScene.HoldUpMost)))
                .Fact("breakin_least", () => Guide.Money(Prices.At(OffscreenCrimes.RepairLeast)))
                .Fact("breakin_most", () => Guide.Money(Prices.At(OffscreenCrimes.RepairMost)))
                .Fact("car_repair_most", () => Guide.Money(Prices.At(OffscreenCrimes.CarRepairCap)))
                .Fact("scam_most", () => Guide.Money(Prices.At(OffscreenCrimes.ScamLossMost)))
                .Fact("fine_drunk", () => Guide.Money(CrimeLaw.DrunkDrivingFine))
                .Owns(Settings.StreetCrimeId)
                .Preset(Settings.CrimeAgainstPlayerId, 0, 1, 1)
                .Preset(Settings.PlayerOffencesId, 0, 1, 1)
                .Preset(Settings.CrimeFrequencyId, 1, 3, 5)
                .Fact("crime_count", () => CrimeCatalogue.All.Length.ToString(CultureInfo.InvariantCulture))
                .Fact("fine_redlight", () => Guide.Money(CrimeLaw.RedLightFine))
                .Fact("fine_speeding", () => Guide.Money(CrimeLaw.SpeedingFine))
                .Fact("fine_reckless", () => Guide.Money(CrimeLaw.RecklessFine))
                .Fact("fine_jaywalk", () => Guide.Money(CrimeLaw.JaywalkFine))
                .Fact("fine_fleeing", () => Guide.Money(CrimeLaw.FleeingFine))
                .Fact("fine_fleeing_step", () => Guide.Money(CrimeLaw.FleeingPerQuarterMinute))
                .Fact("fine_fleeing_most", () => Guide.Money(CrimeLaw.FleeingMost))
                .Fact("fine_wasted_call", () => Guide.Money(CrimeLaw.WastedCallFine))
                .Fact("custody_min", () => CrimeLaw.CustodyHoursMin.ToString(CultureInfo.InvariantCulture))
                .Fact("custody_max", () => CrimeLaw.CustodyHoursMax.ToString(CultureInfo.InvariantCulture))
                .Fact("speed_limit", () => Guide.Number(CrimeLaw.SpeedLimitKmh))
                .Fact("speed_margin", () => Guide.Number(CrimeLaw.SpeedingMarginKmh))
                .Fact("speed_seconds", () => Guide.Number(CrimeLaw.SpeedingSeconds))
                .Fact("speed_gross", () => Guide.Number(CrimeLaw.SpeedingGrossKmh))
                .Fact("seen_redlight", () => Guide.Percent(CrimeLaw.RedLightSeen))
                .Fact("seen_speeding", () => Guide.Percent(CrimeLaw.SpeedingSeen))
                .Fact("seen_reckless", () => Guide.Percent(CrimeLaw.RecklessSeen))
                .Cleanup(CrimeDirector.EndActive);

            Features.Add("driving", "reallife_meta_title_driving")
                .About("reallife_guide_driving_about").Help("reallife_guide_driving")
                .Owns(Settings.CarTuningId, Settings.CarTopSpeedId, Settings.CarAccelerationId, Settings.SmoothThrottleId, Settings.SpeedometerId)
                .Rows("reallife_car_measure")
                .Fact("throttle_rise", () => Guide.Number(CarModel.ThrottleRise))
                .Fact("throttle_fall", () => Guide.Number(CarModel.ThrottleFall))
                .Fact("gentle_throttle", () => Guide.Percent(CarModel.GentleThrottle))
                .Preset(Settings.HandlingId, (int)HandlingLevel.Light, (int)HandlingLevel.Full, (int)HandlingLevel.Heavy)
                .Fact("handling_mass_least", () => Guide.Number(HandlingRules.Target(CarKind.Economy, HandlingLevel.Full, HandlingTuning.Neutral).Mass))
                .Fact("handling_mass_most", () => Guide.Number(HandlingRules.Target(CarKind.Truck, HandlingLevel.Full, HandlingTuning.Neutral).Mass))
                .Fact("handling_brake_rise", () => Guide.Number(HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, HandlingTuning.Neutral).BrakeRise))
                .Fact("handling_kerb", () => Guide.Number(HandlingModel.Kerb * 100f))
                .Fact("handling_stop", () => Guide.Number(HandlingModel.KindStop(CarKind.Saloon, CarTuning.BrakeOnset)))
                .Fact("handling_pedal", () => Guide.Number(HandlingModel.KindPedalStop(CarKind.Saloon)))
                .Test("reallife_handling_test_say", CarTuning.TestHandling)
                .Cleanup(CarTuning.PutBack);

            Features.Add("jobs", "reallife_meta_title_jobs")
                .Group("reallife_jobs_header")
                .About("reallife_guide_jobs_about").Help("reallife_guide_jobs")
                .Owns(Settings.JobsId)
                .Preset(Settings.JobPayId, 125, 100, 80)
                .Data("reallife:jobs", "reallife:job_rental", "reallife:job_live")
                .Contact(JobPhone.ContactId)
                .Objects("RealLifeOverhaul Shift Card")
                .Fact("vendor_quick", () => Guide.Number(VendorJob.QuickTip))
                .Fact("vendor_wrong", () => Guide.Number(VendorJob.WrongCost))
                .Fact("vendor_patience", () => Guide.Number(VendorJob.Patience))
                .Fact("vendor_abandon", () => Guide.Number(VendorJob.AbandonAfter))
                .Fact("vendor_minutes_each", () => ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Vendor)).ToString(CultureInfo.InvariantCulture))
                .Fact("vendor_skip_share", () => Guide.Percent(ShiftRates.Share))
                .Fact("skip_share", () => Guide.Percent(ShiftRates.Share))
                .Fact("skip_minutes_flyers", () => ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Flyers)).ToString(CultureInfo.InvariantCulture))
                .Fact("skip_minutes_cleaner", () => ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Cleaner)).ToString(CultureInfo.InvariantCulture))
                .Fact("skip_minutes_stocker", () => ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Stocker)).ToString(CultureInfo.InvariantCulture))
                .Fact("skip_minutes_busker", () => ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Busker)).ToString(CultureInfo.InvariantCulture))
                .Fact("busker_abandon", () => Guide.Number(BuskerJob.AbandonAfter))
                .Fact("busker_average", () => Guide.Percent(BuskerJob.AveragePlay))
                .Fact("taxi_misses", () => TaxiJob.MissesAllowed.ToString(CultureInfo.InvariantCulture))
                .Fact("vendor_hawk_within", () => Guide.Number(VendorJob.HawkWithin))
                .Fact("vendor_hawk_extra", () => VendorJob.HawkExtraMost.ToString(CultureInfo.InvariantCulture))
                .Fact("vendor_hawk_cooldown", () => Guide.Number(VendorJob.HawkCooldown))
                .Fact("vendor_hawk_fade", () => Guide.Number(VendorJob.HawkHeatFade))
                .Fact("job_table", JobTable)
                .Fact("job_van_fee", () => Guide.Money(JobRental.DailyFee))
                .Fact("job_levels", () => JobCatalogue.MaxLevel.ToString(CultureInfo.InvariantCulture))
                .Fact("job_level_bonus", () => Guide.Percent(JobCatalogue.LevelBonus))
                .Fact("job_reference_level", () => JobCatalogue.ReferenceLevel.ToString(CultureInfo.InvariantCulture))
                .Fact("job_strikes", () => JobCatalogue.StrikesToSack.ToString(CultureInfo.InvariantCulture))
                .Fact("job_sacked_days", () => JobCatalogue.SackedDays.ToString(CultureInfo.InvariantCulture))
                .Fact("job_clean_days", () => JobCatalogue.CleanDays.ToString(CultureInfo.InvariantCulture))
                .Fact("job_min_energy", () => Guide.Number(JobCatalogue.MinEnergy))
                .Fact("job_stop_cut", () => Guide.Percent(JobCatalogue.PoliceStopCut))
                .Fact("job_crash_cut", () => Guide.Percent(JobCatalogue.CrashCut))
                .Fact("gig_client_low", () => Guide.Percent(GigOffers.ClientLow))
                .Fact("gig_client_high", () => Guide.Percent(GigOffers.ClientHigh))
                .Fact("gig_busy_fee", () => Guide.Percent(GigOffers.BusyFee))
                .Fact("gig_normal_fee", () => Guide.Percent(GigOffers.NormalFee(JobCatalogue.All.Length)))
                .Fact("gig_busy_per_day", () => Guide.Number(GigOffers.BusyPerDay))
                .Fact("gig_closed_per_day", () => Guide.Number(GigOffers.ClosedPerDay))
                .Fact("gig_quiet", () => Guide.Percent(GigOffers.QuietCrowd))
                .Fact("gig_rush", () => Guide.Percent(GigOffers.RushCrowd))
                .Fact("gig_tipper_chance", () => Guide.Percent(GigOffers.TipperChance))
                .Fact("gig_tipper_low", () => Guide.Number(GigOffers.TipperLow))
                .Fact("gig_tipper_high", () => Guide.Number(GigOffers.TipperHigh))
                .Fact("gig_speed_chance", () => Guide.Percent(GigOffers.SpeedChance))
                .Fact("gig_speed_low", () => Guide.Number(GigOffers.SpeedLow))
                .Fact("gig_speed_high", () => Guide.Number(GigOffers.SpeedHigh))
                .Fact("gig_speed_time", () => Guide.Percent(GigOffers.SpeedTime))
                .Fact("gig_redo_chance", () => Guide.Percent(GigOffers.RedoChance))
                .Fact("gig_rain_pay", () => Guide.Percent(GigOffers.RainPay - 1f))
                .Fact("gig_storm_pay", () => Guide.Percent(GigOffers.StormPay - 1f))
                .Fact("gig_spell_pay", () => Guide.Percent(GigOffers.HeatPay - 1f))
                .Fact("gig_ice_pay", () => Guide.Percent(GigOffers.IcePay - 1f))
                .Cleanup(() =>
                {
                    JobDirector.Instance?.Quit();   // the shift ends, its people and props go back, a hired van is handed in
                })
                // Read from the (now empty) save here, not through the director: its own reload would forget a van still out.
                .ResetData(() => JobState.Load(SaveGameManager.Current.modData, TimeHelper.CurrentDay))
                .Repair(JobPhone.Setup);

            Features.Add("life", "reallife_meta_title_life")
                .Group("reallife_life_header")
                .About("reallife_guide_life_about").Help("reallife_guide_life")
                .Owns(Settings.LifeEventsId)
                .Preset(Settings.EventFrequencyId, 4, 6, 8)
                .Test("reallife_test_event")
                .Data("reallife:sick_until", "reallife:friend_day", "reallife:mom_day", "reallife:mom_gift_day", "reallife:modifiers",
                    "reallife:seen_events", "reallife:pending_fine", "reallife:pending_fine_due")
                .Objects("RealLifeOverhaul Crosshair")
                .Fact("life_count", () => LifeEventCatalogue.All.Length.ToString(CultureInfo.InvariantCulture))
                .Fact("doctor_fee", () => Guide.Money(LifeEvents.DoctorFee))
                .Fact("ambulance_fee", () => Guide.Money(LifeEvents.AmbulanceFee))
                .Fact("tow_fee", () => Guide.Money(VehicleTow.Fee))
                .Cleanup(() =>
                {
                    LifeEvents.Instance?.Cure(null);   // the cold and its drain on energy end; no toast
                })
                .ResetData(() =>
                {
                    ModState.ClearPendingFine();
                    ModState.Reset();   // LifeEvents reads the (now empty) save again on its next frame
                })
                .Repair(() => ModState.RepairModifiers());

            Features.Add("people", "reallife_meta_title_people")
                .About("reallife_guide_people_about").Help("reallife_guide_people")
                .Owns(Settings.PeopleId, Settings.OfficersOnPatrolId)
                .StandsAside(GoingPublic.TrafficSentence, Settings.OfficersOnPatrolId, Settings.PolicePresenceId, Settings.PatrolFrequencyId)   // their patrol cars are the traffic's
                .Preset(Settings.PolicePresenceId, 4, 6, 8)
                .Preset(Settings.PatrolFrequencyId, 2, 4, 6)
                .Fact("pool_people", () => ActorPool.MaxActors.ToString(CultureInfo.InvariantCulture))
                .Fact("pool_presence", () => ActorPool.MaxPresence.ToString(CultureInfo.InvariantCulture))
                .Cleanup(() =>
                {
                    // Last: by now every scene has given its people back, and whoever is still out is sent in.
                    Stretcher.PutAwayShared();
                    ActorPool.Instance?.ReleaseAll();
                }, 100);

            Features.Add("phone", "reallife_meta_title_phone")
                .About("reallife_guide_phone_about").Help("reallife_guide_phone")
                .Owns(Settings.PhoneContactsId, Settings.PhoneSkinId, Settings.NoticeLookId)
                .NeedsReload(Settings.PhoneContactsId)
                .Contact("reallife-911", "reallife-doctor", "reallife-friend", "reallife-mom")
                .Test("reallife_contacts_test_pictures", "reallife_contacts_test_redraw", "reallife_test_notice_look")
                .Repair(PhoneContacts.Setup);

            Features.Add("ragdoll", "reallife_meta_title_ragdoll")
                .About("reallife_guide_ragdoll_about").Help("reallife_guide_ragdoll")
                .Fact("roadside_least", () => Guide.Money(JusticeLaw.FineFloor))
                .Owns(Settings.RagdollsId)
                .Preset(Settings.PlayerKnockdownId, 0, 1, 1)
                .Preset(Settings.PedestrianIncidentsId, 0, 1, 1)
                .Test("reallife_test_ragdoll", "reallife_test_ped_incident")
                .Fact("ragdoll_max", () => RagdollSystem.MaxActive.ToString(CultureInfo.InvariantCulture))
                .Fact("ragdoll_still", () => RagdollSystem.MaxStill.ToString(CultureInfo.InvariantCulture));

            Features.Add("traffic", "reallife_meta_title_traffic")
                .About("reallife_guide_traffic_about").Help("reallife_guide_traffic")
                .Fact("crash_least", () => Guide.Money(Prices.At(CrashConsequences.LiabilityLeast)))
                .Fact("crash_most", () => Guide.Money(Prices.At(CrashConsequences.LiabilityMost)))
                .Owns(Settings.TrafficAccidentsId, Settings.AccidentNoticesId, Settings.AccidentMarkerId, Settings.TowTrucksId, Settings.SirenId, Settings.PoliceId)
                .StandsAside(GoingPublic.TrafficSentence, Settings.TrafficAccidentsId, Settings.AccidentFrequencyId, Settings.TowTrucksId,
                    Settings.PoliceId, Settings.PolicePursuitId, Settings.SirenId)
                .Preset(Settings.AccidentFrequencyId, 1, 3, 4)
                .Fact("accident_hours", () => AccidentHours(StreetRules.DefaultFrequency))
                .Fact("accident_hours_now", () => AccidentHours(Settings.AccidentFrequency))
                .Fact("accident_hours_least", () => AccidentHours(1))
                .Fact("accident_hours_most", () => AccidentHours(10))
                .Preset(Settings.CrashConsequencesId, 0, 1, 1)
                .Preset(Settings.PolicePursuitId, 0, 1, 1)
                .Test("reallife_test_accident", "reallife_test_accident_minor", "reallife_test_accident_moderate", "reallife_test_accident_severe", "reallife_test_tow_truck")
                .Fact("fine_caught", () => Guide.Money(PolicePursuit.CaughtFine))
                .Fact("fine_escaped", () => Guide.Money(PolicePursuit.EscapedFine))
                .Fact("fine_injury", () => Guide.Money(PolicePursuit.InjuryFine))
                .Fact("fine_death", () => Guide.Money(PolicePursuit.DeathFine))
                .Fact("fine_injury_run", () => Guide.Money(PolicePursuit.InjuryRunFine))
                .Fact("fine_death_run", () => Guide.Money(PolicePursuit.DeathRunFine))
                .Fact("fine_injury_escaped", () => Guide.Money(PolicePursuit.InjuryEscapedFine))
                .Fact("fine_death_escaped", () => Guide.Money(PolicePursuit.DeathEscapedFine))
                .Fact("pursuit_least", () => Guide.Number(PursuitPace.LeastKmh))
                .Fact("pursuit_most", () => Guide.Number(PursuitPace.MostKmh))
                .Fact("pursuit_backup", () => Guide.Number(PursuitPace.BackupAfter))
                .Cleanup(() =>
                {
                    TrafficAccidents.EndActive();   // roads open again, wrecks and units back to traffic, the cordon put away
                    SceneObstacles.Clear();
                }, 10);

            Features.Add("streets", "reallife_meta_title_streets")
                .About("reallife_guide_streets_about").Help("reallife_guide_streets")
                .Owns(Settings.HornReactionsId, Settings.NaturalAccidentsId, Settings.SolidStreetsId)
                .StandsAside(GoingPublic.TrafficSentence, Settings.HornReactionsId, Settings.NaturalAccidentsId, Settings.SolidStreetsId)
                .Test("reallife_test_streets_speeder", "reallife_test_streets_redlight", "reallife_test_streets_drunk", "reallife_test_streets_jaywalker",
                    "reallife_test_streets_hit", "reallife_test_streets_crash", "reallife_test_streets_patrol",
                    "reallife_test_solid_room", "reallife_test_solid_close", "reallife_test_solid_dawdle", "reallife_test_solid_late",
                    "reallife_test_solid_crowd", "reallife_test_solid_senses")
                .Fact("horn_reach", () => Guide.Number(StreetRules.HornReach))
                .Fact("horn_most", () => Guide.Number(StreetRules.HornMost))
                .Fact("horn_officer", () => Guide.Number(StreetRules.OfficerWithin))
                .Fact("horn_fine", () => Guide.Money(CrimeLaw.HornFine))
                .Fact("reckless_least", () => Guide.Number(StreetRules.LastsLeast))
                .Fact("reckless_most", () => Guide.Number(StreetRules.LastsMost))
                .Fact("crash_minor", () => Guide.Number((float)System.Math.Round(StreetRules.MinorBelow * 3.6f)))
                .Fact("crash_severe", () => Guide.Number((float)System.Math.Round(StreetRules.SevereFrom * 3.6f)))
                .Fact("solid_horn", () => Guide.Number(SolidRules.HornEvery))
                .Fact("solid_hold", () => Guide.Number(SolidRules.HoldMost))
                .Cleanup(() =>
                {
                    // Before the accident scene's own cleanup: the car, the patrol car and whoever was in the road are given back first.
                    SolidStreets.Instance?.ReleaseAll("the options page's cleanup");
                    RecklessDrivers.Instance?.EndNow("the options page's cleanup");
                    HornReactions.Instance?.ReleaseAll("the options page's cleanup");
                }, 5);

            Features.Add("visuals", "reallife_meta_title_visuals")
                .Group("reallife_visuals_header")
                .About("reallife_guide_visuals_about").Help("reallife_guide_visuals")
                .Owns(Settings.VisualsEnabledId, Settings.SaturationId, Settings.ContrastId, Settings.BloomId, Settings.DetailId);
        }

        private static void RegisterTools()
        {
            Features.Add("keys", "reallife_meta_title_keys")
                .Group("reallife_keys_header")
                .About("reallife_guide_keys_about").Help("reallife_guide_keys");

            Features.Add("performance", "reallife_meta_title_performance")
                .Group("reallife_perf_header")
                .About("reallife_guide_performance_about").Help("reallife_guide_performance")
                .Test("reallife_perf_test_session", SessionWatch.TestSay);
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                if (!tweak.Trial)
                {
                    SettingsTable.Declare("perf_" + tweak.Id, OptionType.Toggle, tweak.DefaultOn ? 1 : 0);
                }
            }
            SettingsTable.Declare("perf_step_timing", OptionType.Toggle, 1);

            Features.Add("savetools", "reallife_meta_title_savetools")
                .About("reallife_guide_savetools_about").Help("reallife_guide_savetools")
                .Row(new ButtonOption("reallife_meta_remove_button", SaveTools.AskPrepare))
                .Row(new ButtonOption("reallife_meta_resetdata_button", SaveTools.AskReset))
                .Row(new ButtonOption("reallife_meta_repair_button", SaveTools.Repair))
                .Data(SaveTools.VersionKey, SaveTools.DevelopmentVersionKey)
                .Contact(GuideContact.ContactId)
                .Repair(() => GuideContact.Setup());

            Features.Add("diagnostics", "reallife_meta_title_diagnostics")
                .About("reallife_guide_diagnostics_about").Help("reallife_guide_diagnostics")
                .Row(new ToggleOption(Settings.DevDiagnosticsId, "reallife_meta_dev_diagnostics", false, on =>
                {
                    Settings.DevDiagnostics = on;
                    Dev.Apply();
                }))
                .Row(new ToggleOption(Settings.ShowTestToolsId, "reallife_meta_show_tests", false, on =>
                {
                    Settings.ShowTestTools = on;
                    OptionsLayout.ApplyAll();
                }))
                .Row(new ButtonOption("reallife_meta_check_button", OptionsLayout.CheckSetup))
                .Test("reallife_test_block_say", BlockTests.Say)
                .Test("reallife_test_guard_fault", Guard.TestFault)
                .Test("reallife_test_build_notice", GameBuild.TestNotice)
                .Test("reallife_test_build_box", GameConfirm.TestOwnBox)
                .Diagnostic(KeyAction.PerfProbe, KeyAction.PerfBench, KeyAction.PhoneDump, KeyAction.CharacterReport);

            Features.Add("multiplayer", "reallife_meta_title_multiplayer")
                .About("reallife_guide_multiplayer_about").Help("reallife_guide_multiplayer")
                .Fact("session", GoingPublic.Words)
                .Fact("city", SharedCity.Words)
                .Row(new ButtonOption("reallife_gp_tell_button", GoingPublic.Tell))
                .Row(new ButtonOption("reallife_gp_city_button", SharedCity.Tell))
                .Test("reallife_test_gp_pretend", GoingPublic.TestPretend)
                .Test("reallife_test_gp_record", SharedCity.TestRecord)
                .Test("reallife_test_gp_apply", SharedCity.TestApply);

            Features.Add("tests", "reallife_meta_title_tests")
                .Owns(Settings.HandlingSoftnessId, Settings.HandlingBounceId, Settings.HandlingRollId, Settings.HandlingGripId, Settings.HandlingSteeringId,
                    Settings.HandlingBrakesId)   // the driven car's handling tuned live, its figures written to the log (Driving/CarTuning.TuningChanged)
                .Present(Settings.HandlingSoftnessId, () => Settings.ShowTestTools)
                .Present(Settings.HandlingBounceId, () => Settings.ShowTestTools)
                .Present(Settings.HandlingRollId, () => Settings.ShowTestTools)
                .Present(Settings.HandlingGripId, () => Settings.ShowTestTools)
                .Present(Settings.HandlingSteeringId, () => Settings.ShowTestTools)
                .Present(Settings.HandlingBrakesId, () => Settings.ShowTestTools);
        }

        // Game hours of street time between two accidents near the player at a setting of the option, at the day's average hour in fair weather.
        private static string AccidentHours(int frequency)
        {
            float perDay = StreetRules.PerDay(frequency < 1 ? 1 : frequency);
            float hours = perDay > 0f ? 24f / perDay : 0f;
            return Guide.Number(hours >= 10f ? (float)System.Math.Round(hours) : (float)System.Math.Round(hours * 10f) / 10f);
        }

        private static string Whole(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string MarketsTime(int minute)
        {
            return (minute / 60).GetFormattedTime(minute % 60);
        }

        // One line per job, from the catalogue the jobs themselves pay by.
        private static string JobTable()
        {
            var text = new StringBuilder();
            foreach (JobDef job in JobCatalogue.All)
            {
                // The busker's "pay" is only what the tips are worked out from.
                text.Append("- ").Append((job.Id == JobCatalogue.Busker ? "reallife_guide_job_row_tips" : "reallife_guide_job_row").GetLocalization()
                    .Replace("{name}", job.NameKey.GetLocalization())
                    .Replace("{pay}", Guide.Money(job.UnitPay))
                    .Replace("{metre}", job.PayPerMetre > 0f
                        ? "reallife_guide_job_metre".GetLocalization().Replace("{amount}", Guide.Money(job.PayPerMetre * 100f))
                        : string.Empty)
                    .Replace("{size}", job.ShiftUnits <= 0 ? string.Empty : "reallife_guide_job_size".GetLocalization()
                        .Replace("{low}", (job.ShiftUnits - job.UnitsSpread).ToString(CultureInfo.InvariantCulture))
                        .Replace("{high}", (job.ShiftUnits + job.UnitsSpread).ToString(CultureInfo.InvariantCulture)))
                    .Replace("{daily}", job.DailyUnits.ToString(CultureInfo.InvariantCulture))
                    .Replace("{level}", job.UnitsPerLevel.ToString(CultureInfo.InvariantCulture)));
                if ((job.Needs & JobNeed.Car) != 0)
                {
                    text.Append("reallife_guide_job_need_car".GetLocalization());
                }
                if ((job.Needs & JobNeed.Reference) != 0)
                {
                    text.Append("reallife_guide_job_need_reference".GetLocalization());
                }
                if ((job.Needs & JobNeed.CleanRecord) != 0)
                {
                    text.Append("reallife_guide_job_need_clean".GetLocalization());
                }
                text.Append('\n');
            }
            return text.ToString();
        }
    }
}
