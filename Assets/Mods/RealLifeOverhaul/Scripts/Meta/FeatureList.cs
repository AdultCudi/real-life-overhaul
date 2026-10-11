using System.Globalization;
using System.Text;
using BigAmbitions.Mods;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Every feature of the mod, one statement each. This is all the options page, the presets, the guide, the
    /// save tools and the diagnostics switch know about a feature; nothing else in Meta names one.
    ///
    /// TO ADD A FEATURE: one statement at the marked place at the end of <see cref="RegisterAll"/> (or the same
    /// statement in a static DescribeFeature() of a class of the feature's own marked [DescribesFeature]):
    ///
    ///     Features.Add("fuel", "<reallife_fuel_title>")            // id (plain English, it sorts the page), title key
    ///         .Group("<reallife_fuel_header>")                     // the AddHeader key of its group in RealLifeOverhaulMod.cs
    ///         .About("<reallife_fuel_about>")                      // one sentence, en.json
    ///         .Help("<reallife_fuel_help>")                        // its guide page, en.json: "# " heading, "- " bullet, {fact}
    ///         .Preset(Settings.FuelId, 0, 1, 1)                    // toggle: Relaxed off, Realistic on, Hardcore on (Standard = its default)
    ///         .Preset(Settings.FuelPriceId, 50, 120, 160)          // slider: Relaxed, Realistic, Hardcore
    ///         .NeedsReload(Settings.FuelId)                        // only if it is read once per load
    ///         .Data("reallife:fuel")                               // modData keys it writes ("reallife:fuel_*" for a prefix)
    ///         .Contact("<reallife-garage>")                        // phone contacts it adds
    ///         .Objects("RealLifeOverhaul Fuel HUD")                // objects of its own outside the mod's root object
    ///         .Fact("fuel_price", () => Guide.Money(FuelLaw.PricePerLitre))   // {fuel_price} in its help text
    ///         .Cleanup(() => FuelDirector.Instance?.StopAll())     // off the street, stop; safe when idle
    ///         .ResetData(() => FuelState.Reset())                  // forget this save (its modData was removed)
    ///         .Repair(() => FuelPhone.Setup());                    // put back contacts if missing
    ///
    /// (The keys are written here inside angle brackets only so that the locale check does not take them for real ones.)
    /// Only Add is required; every other call is optional. Its option rows, test buttons (any button whose label
    /// contains "_test_") and options stay where they are added today, in RealLifeOverhaulMod.cs.
    /// </summary>
    internal static class FeatureList
    {
        public static void RegisterAll()
        {
            // ------------------------------------------------------------ gameplay (alphabetical on the page, by id)

            Features.Add("camera", "reallife_meta_title_camera")
                .Group("reallife_camera_header")
                .About("reallife_guide_camera_about").Help("reallife_guide_camera")
                .Owns(Settings.CameraEnabledId, Settings.MouseLookId, Settings.MouseSensitivityId, Settings.EnterWithKeyId, Settings.CameraIndoorsId,
                    Settings.CameraPitchId, Settings.CameraDistanceId, Settings.CameraFovId, Settings.CameraHeightId, Settings.CarCameraId,
                    Settings.CarCameraDistanceId, Settings.CameraFollowId)
                // While another author's mod drives the camera these do nothing, and the page says so (Camera/CameraYield).
                .StandsAside(CameraYield.Sentence, Settings.CameraEnabledId, Settings.MouseLookId, Settings.MouseSensitivityId, Settings.CameraIndoorsId,
                    Settings.CameraPitchId, Settings.CameraDistanceId, Settings.CameraFovId, Settings.CameraHeightId, Settings.CarCameraId,
                    Settings.CarCameraDistanceId, Settings.CameraFollowId)
                .Cleanup(() => ThirdPersonCamera.Focus = null);

            Features.Add("character", "reallife_meta_title_character")
                .Group("reallife_looks_header")
                .About("reallife_guide_character_about").Help("reallife_guide_character")
                .Owns(Settings.CharacterHighDetailId, Settings.CharacterHairId, Settings.CharacterLightingId, Settings.CharacterNewLooksId)
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
                .Fact("gentle_throttle", () => Guide.Percent(CarModel.GentleThrottle));

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
                .Owns(Settings.PhoneContactsId, Settings.PhoneSkinId)
                .NeedsReload(Settings.PhoneContactsId)
                .Contact("reallife-911", "reallife-doctor", "reallife-friend", "reallife-mom")
                .Repair(PhoneContacts.Setup);

            Features.Add("ragdoll", "reallife_meta_title_ragdoll")
                .About("reallife_guide_ragdoll_about").Help("reallife_guide_ragdoll")
                .Owns(Settings.RagdollsId)
                .Preset(Settings.PlayerKnockdownId, 0, 1, 1)
                .Preset(Settings.PedestrianIncidentsId, 0, 1, 1)
                .Test("reallife_test_ragdoll", "reallife_test_ped_incident")
                .Fact("ragdoll_max", () => RagdollSystem.MaxActive.ToString(CultureInfo.InvariantCulture))
                .Fact("ragdoll_still", () => RagdollSystem.MaxStill.ToString(CultureInfo.InvariantCulture));

            Features.Add("traffic", "reallife_meta_title_traffic")
                .About("reallife_guide_traffic_about").Help("reallife_guide_traffic")
                .Owns(Settings.TrafficAccidentsId, Settings.SirenId, Settings.PoliceId)
                .Preset(Settings.AccidentFrequencyId, 1, 3, 4)
                .Preset(Settings.CrashConsequencesId, 0, 1, 1)
                .Preset(Settings.PolicePursuitId, 0, 1, 1)
                .Test("reallife_test_accident", "reallife_test_accident_minor", "reallife_test_accident_moderate", "reallife_test_accident_severe")
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
                .Owns(Settings.HornReactionsId, Settings.NaturalAccidentsId)
                .Test("reallife_test_streets_speeder", "reallife_test_streets_redlight", "reallife_test_streets_drunk", "reallife_test_streets_jaywalker",
                    "reallife_test_streets_hit", "reallife_test_streets_crash", "reallife_test_streets_patrol")
                .Fact("horn_reach", () => Guide.Number(StreetRules.HornReach))
                .Fact("horn_most", () => Guide.Number(StreetRules.HornMost))
                .Fact("horn_officer", () => Guide.Number(StreetRules.OfficerWithin))
                .Fact("horn_fine", () => Guide.Money(CrimeLaw.HornFine))
                .Fact("reckless_least", () => Guide.Number(StreetRules.LastsLeast))
                .Fact("reckless_most", () => Guide.Number(StreetRules.LastsMost))
                .Fact("crash_minor", () => Guide.Number((float)System.Math.Round(StreetRules.MinorBelow * 3.6f)))
                .Fact("crash_severe", () => Guide.Number((float)System.Math.Round(StreetRules.SevereFrom * 3.6f)))
                .Cleanup(() =>
                {
                    // Before the accident scene's own cleanup: the car, the patrol car and whoever was in the road are given back first.
                    RecklessDrivers.Instance?.EndNow("the options page's cleanup");
                    HornReactions.Instance?.ReleaseAll("the options page's cleanup");
                }, 5);

            Features.Add("visuals", "reallife_meta_title_visuals")
                .Group("reallife_visuals_header")
                .About("reallife_guide_visuals_about").Help("reallife_guide_visuals")
                .Owns(Settings.VisualsEnabledId, Settings.SaturationId, Settings.ContrastId, Settings.BloomId, Settings.DetailId);

            // ------------------------------------------------------------ controls, performance, tools

            Features.Add("keys", "reallife_meta_title_keys").As(FeatureKind.Controls)
                .Group("reallife_keys_header")
                .About("reallife_guide_keys_about").Help("reallife_guide_keys");

            Features.Add("performance", "reallife_meta_title_performance").As(FeatureKind.Performance)
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

            Features.Add("savetools", "reallife_meta_title_savetools").As(FeatureKind.Tools)
                .About("reallife_guide_savetools_about").Help("reallife_guide_savetools")
                .Row(new ButtonOption("reallife_meta_remove_button", SaveTools.AskPrepare))
                .Row(new ButtonOption("reallife_meta_resetdata_button", SaveTools.AskReset))
                .Row(new ButtonOption("reallife_meta_repair_button", SaveTools.Repair))
                .Data(SaveTools.VersionKey, SaveTools.DevelopmentVersionKey)
                .Contact(GuideContact.ContactId)
                .Repair(() => GuideContact.Setup());

            Features.Add("diagnostics", "reallife_meta_title_diagnostics").As(FeatureKind.Tools)
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
                .Diagnostic(KeyAction.PerfProbe, KeyAction.PerfBench, KeyAction.PhoneDump, KeyAction.CharacterReport);

            Features.Add("tests", "reallife_meta_title_tests").As(FeatureKind.Tools).OpenAtFirst();

            // ------------------------------------------------------------ features merged later: one statement each, below this line

            Features.Add("home", "reallife_meta_title_home")
                .Group("reallife_home_header")
                .About("reallife_guide_home_about").Help("reallife_guide_home")
                .Owns(Settings.HomeLifeId)
                .Preset(Settings.HomeEasyId, 1, 0, 0)
                .OnFrom(Settings.HomeIllnessId, Intensity.Standard)
                .Data(HomeState.Key)
                .Contact(HomePhone.ContactId)
                .ResetData(() =>
                {
                    HomeState.Reset();   // the director reads the (now empty) save again on its next second
                    HomeHealth.Reset();
                })
                .Repair(HomePhone.Setup);

            Features.Add("law", "reallife_meta_title_law")
                .Group("reallife_law_header")
                .About("reallife_guide_law_about").Help("reallife_guide_law")
                .Owns(Settings.LawPrecinctsId, Settings.LawCustodyId)
                .OnFrom(Settings.LawArrestsId, Intensity.Standard)
                .OnFrom(Settings.LawReputationId, Intensity.Standard)
                .Data("reallife:law_record", "reallife:law_reputation", "reallife:law_custody", "reallife:law_precincts")
                .Contact(Law.RecordPhone.ContactId)
                .Fact("law_most_hours", () => Law.LawBook.MostHours.ToString(CultureInfo.InvariantCulture))
                .Fact("law_lawyer_fee", () => Guide.Money(Law.LawBook.LawyerFee))
                .Fact("law_impound_fee", () => Guide.Money(Law.LawBook.ImpoundFee))
                .ResetData(Law.LawState.Reset);   // (the save tools refuse while somebody is held: PoliceAttention.IsBusy)

            Features.Add("transport", "reallife_meta_title_transport")
                .Group("reallife_transport_header")
                .About("reallife_guide_transport_about").Help("reallife_guide_transport")
                .Owns(Settings.BusesId, Settings.TransitPeopleId, Settings.SubwayJourneyId, Settings.CarOwnershipId)
                .Preset(Settings.CarEasyId, 1, 0, 0)
                .Data("reallife:transit", "reallife:garage")
                .Contact(TransitPhone.ContactId, CarPhone.GarageId, CarPhone.RoadsideId)
                .Fact("car_policy_days", () => CarRules.PolicyDays.ToString(CultureInfo.InvariantCulture))
                .ResetData(() =>
                {
                    TransitState.Reset();
                    CarGarage.Reset();
                })
                .Repair(() =>
                {
                    TransitPhone.Setup();
                    CarPhone.Setup();
                });

            // The taxi by phone shares the transport header. Nothing of it is in the save but its contact: a taxi that was coming is let go.
            Features.Add("taxi", "reallife_meta_title_taxi")
                .About("reallife_guide_taxi_about").Help("reallife_guide_taxi")
                .Owns(Settings.TaxiCallId)
                .Test("reallife_test_taxi_call", "reallife_test_taxi_bring", "reallife_test_taxi_cancel")
                .Contact(TaxiPhone.ContactId)
                .Fact("taxi_callout", () => Guide.Money(TaxiRules.CallOut))
                .Fact("taxi_callout_night", () => Guide.Money(TaxiRules.NightCallOut))
                .Fact("taxi_night_from", () => TaxiRules.NightFrom.ToString(CultureInfo.InvariantCulture) + ":00")
                .Fact("taxi_night_until", () => TaxiRules.NightUntil.ToString("00", CultureInfo.InvariantCulture) + ":00")
                .Fact("taxi_wet_extra", () => Guide.Percent(TaxiRules.WetExtra))
                .Fact("taxi_start_least", () => Guide.Number(TaxiRules.StartLeast))
                .Fact("taxi_start_most", () => Guide.Number(TaxiRules.StartMost))
                .Fact("taxi_wet_start_least", () => Guide.Number(TaxiRules.WetStartLeast))
                .Fact("taxi_wet_start_most", () => Guide.Number(TaxiRules.WetStartMost))
                .Fact("taxi_road_within", () => Guide.Number(TaxiRules.RoadWithin))
                .Fact("taxi_cancel_free", () => Guide.Number(TaxiRules.CancelFreeBeyond))
                .Fact("taxi_cancel_share", () => Guide.Percent(TaxiRules.CancelShare))
                .Fact("taxi_noshow_share", () => Guide.Percent(TaxiRules.NoShowShare))
                .Fact("taxi_wait", () => Guide.Number(TaxiRules.WaitSeconds))
                .Fact("taxi_grace", () => Guide.Number(TaxiRules.GraceSeconds))
                .Fact("taxi_grace_within", () => Guide.Number(TaxiRules.GraceWithin))
                .Fact("taxi_far", () => Guide.Number(TaxiRules.FarFromStop))
                .Cleanup(() => TaxiDirector.Instance?.StopAll())
                .Repair(TaxiPhone.Setup);

            // Leisure's records live in ModState's part of the save: "life" resets them (ModState.Reset).
            Features.Add("leisure", "reallife_meta_title_leisure")
                .Group("reallife_leisure_header")
                .About("reallife_guide_leisure_about").Help("reallife_guide_leisure")
                .Owns(Settings.LeisureId, Settings.ClothesShopsId, Settings.NightLifeId, Settings.AmbientSoundId, Settings.PhotoModeId)
                .Data("reallife:leisure_*")
                .Objects("RealLifeOverhaul Shift Card", "RealLifeOverhaul Clothes")
                .Rows("reallife_leisure_test_records")   // the records and landmarks card: the player's, not a test
                .Fact("leisure_tipsy_from", () => Guide.Number(Leisure.BarDrinks.TipsyFrom))
                .Cleanup(() =>
                {
                    Leisure.PhotoMode.Instance?.Leave();
                    Leisure.Activities.Stop();   // whatever is being played ends, the body and the props go back
                });

            Features.Add("events", "reallife_meta_title_events")
                .Group("reallife_events_header")
                .About("reallife_guide_events_about").Help("reallife_guide_events")
                .Owns(Settings.CityFiresId, Settings.StreetEventsId)
                .Preset(Settings.FireFrequencyId, 1, 3, 4)
                .OnFrom(Settings.WeatherMattersId, Intensity.Standard)
                .Preset(Settings.WeatherFrequencyId, 3, 5, 7)
                .Data("reallife:events")
                .Contact("reallife-bulletin")
                .Cleanup(Events.EventDirector.EndActive);

            Features.Add("social", "reallife_meta_title_social")
                .Group("reallife_social_header")
                .About("reallife_guide_social_about").Help("reallife_guide_social")
                .Owns(Settings.SocialId, Settings.SocialMeetingsId, Settings.SocialOutingsId, Settings.SocialNewspaperId)
                .OnFrom(Settings.SocialRelationshipsId, Intensity.Standard)
                .Preset(Settings.SocialTextsMaxId, 2, 3, 4)
                .Data("reallife:social", "reallife:social_*")
                .Contact(SocialPhone.PeopleId, SocialPhone.LedgerId)
                .Objects("RealLifeOverhaul Shift Card")
                .Fact("social_cast", () => SocialRules.CastSize.ToString(CultureInfo.InvariantCulture))
                .ResetData(() =>
                {
                    SocialState.Reset();   // the director reads the (now empty) save again on its next second
                    News.Reset();
                })
                .Repair(() => SocialPhone.Setup(SocialDirector.Logger));

            Features.Add("travel", "reallife_meta_title_travel")
                .Group("reallife_travel_header")
                .About("reallife_guide_travel_about").Help("reallife_guide_travel")
                .Owns(Settings.HotelsId, Settings.HolidaysId, Settings.TravelExtrasId)
                .Data(TravelState.SaveKey)
                .Contact(TravelPhone.TripsId, TravelPhone.AgentId)
                .Objects("RealLifeOverhaul Travel UI")
                .Cleanup(HotelStay.EndSleepEffects)
                .ResetData(TravelState.Reset)
                .Repair(TravelPhone.Setup);

            // Careers share the jobs header; their tests stay with "jobs", which also ends a shift (JobDirector.Quit).
            Features.Add("careers", "reallife_meta_title_careers")
                .About("reallife_guide_careers_about").Help("reallife_guide_careers")
                .Owns(Settings.CareersId)
                .Data("reallife:career")
                .Contact(CareerPhone.ContactId)
                .Objects("RealLifeOverhaul Dispatch")
                .ResetData(() => CareerState.Load(SaveGameManager.Current.modData))
                .Repair(CareerPhone.Setup);

            Features.Add("business", "reallife_meta_title_business")
                .Group("reallife_business_header")
                .About("reallife_guide_business_about").Help("reallife_guide_business")
                .Owns(Settings.BusinessId)
                .Preset(Settings.BusinessEasyId, 1, 0, 0)
                .Preset(Settings.BusinessFrequencyId, 2, 5, 7)
                .OnFrom(Settings.BusinessRacketId, Intensity.Standard)
                .Data("reallife:business")
                .Contact(BusinessDesk.ContactId)
                // Read from the (now empty) save: the director's own stand-down (footfall and closures put back) runs when the option goes off or the city unloads.
                .ResetData(() => BusinessState.Load(SaveGameManager.Current.modData, TimeHelper.CurrentDay))
                .Repair(BusinessDesk.Setup);

            Features.Add("garage", "reallife_meta_title_garage")
                .Group("reallife_garage_header")
                .About("reallife_guide_garage_about").Help("reallife_guide_garage")
                .Owns(Settings.GarageId, Settings.GarageCosmeticsId, Settings.GarageTuningId, Settings.GarageUnderglowId, Settings.TwoWheelersId)
                .OnFrom(Settings.GaragePoliceId, Intensity.Standard)
                .Rows("reallife_garage_test_vehicles")   // "My vehicles": a list for the player (a two-wheeler that cannot be reached is fetched from it), not a test
                .Data("reallife:garage_*")
                .Fact("garage_resale", () => Guide.Percent(GarageCatalogue.Resale))
                .Cleanup(GarageSummary.Close)
                .ResetData(() => GarageState.Load(SaveGameManager.Current.modData));   // no builds in the save: the director takes them off the cars

            SettingsTable.Declare(JusticeSettings.NoPrisonId, OptionType.Toggle, 0);
            SettingsTable.Declare(JusticeSettings.PrisonCapId, OptionType.Slider, JusticeLaw.PrisonCapDefault);
            Features.Add("justice", "reallife_meta_title_justice")
                .Group("reallife_justice_header")
                .About("reallife_guide_justice_about").Help("reallife_guide_justice")
                .Owns(JusticeSettings.EnabledId)
                .Preset(JusticeSettings.NoPrisonId, 1, 0, 0)
                .Preset(JusticeSettings.PrisonCapId, JusticeLaw.PrisonCapMin, JusticeLaw.PrisonCapDefault, 10)
                .Data(JusticeState.Key)
                .Contact(LegalPhone.ContactId)
                .Objects("RealLifeOverhaul Justice", "RealLifeOverhaul Shift Card", "RealLifeOverhaul Courthouse Sign", "RealLifeOverhaul Litter")
                .Fact("justice_fine_cap", () => Guide.Money(JusticeLaw.FineCap))
                .Fact("justice_community_cap", () => JusticeLaw.CommunityCapHours.ToString(CultureInfo.InvariantCulture))
                .Fact("justice_custody_cap", () => JusticeLaw.CustodyCapHours.ToString(CultureInfo.InvariantCulture))
                .Fact("justice_appeal_days", () => JusticeLaw.AppealWindowDays.ToString(CultureInfo.InvariantCulture))
                .Fact("justice_seal_days", () => JusticeLaw.SealAfterCleanDays.ToString(CultureInfo.InvariantCulture))
                .ResetData(JusticeState.Reset)   // the director reads the (now empty) save again on its next second
                .Repair(() => LegalPhone.Setup(JusticeDirector.Logger));

            Features.Add("licences", "reallife_meta_title_licences")
                .Group("reallife_licences_header")
                .About("reallife_guide_licences_about").Help("reallife_guide_licences")
                .Owns(Settings.DrivingSchoolId)
                .OnFrom(Settings.DrivingLicenceId, Intensity.Standard)
                .Preset(Settings.StartWithLicenceId, 1, 0, 0)
                .OnFrom(Settings.LicencePointsId, Intensity.Standard)
                .OnFrom(Settings.RoadsideChecksId, Intensity.Standard)
                .Data(LicenceState.Key, TestCar.Key)
                .Contact(LicencePhone.ContactId)
                .Objects("RealLifeOverhaul Licence HUD")
                .Fact("lic_theory_fee", () => Guide.Money(LicenceLaw.TheoryFee))
                .Fact("lic_lesson_fee", () => Guide.Money(LicenceLaw.LessonFee))
                .Fact("lic_test_fee", () => Guide.Money(LicenceLaw.CarTestFee))
                .Fact("lic_motorcycle_fee", () => Guide.Money(LicenceLaw.MotorcycleFee))
                .Fact("lic_wrong_class_fine", () => Guide.Money(LicenceLaw.WrongClassFine))
                .Fact("lic_valid_days", () => LicenceLaw.ValidDays.ToString(CultureInfo.InvariantCulture))
                .Fact("lic_points_limit", () => LicenceLaw.PointsLimit.ToString(CultureInfo.InvariantCulture))
                .Fact("lic_suspension_days", () => LicenceLaw.SuspensionDays.ToString(CultureInfo.InvariantCulture))
                .Fact("lic_no_licence_fine", () => Guide.Money(LicenceLaw.NoLicenceFine))
                .ResetData(LicenceState.Reset)   // the director reads the (now empty) save again on its next second
                .Repair(LicencePhone.Setup);

            // The Planner reads the other features and changes none: off, its app is not on the phone and no reminder goes out.
            Features.Add("planner", "reallife_meta_title_planner")
                .Group("reallife_planner_header")
                .About("reallife_guide_planner_about").Help("reallife_guide_planner")
                .Owns(Settings.PlannerId, Settings.PlannerRemindersId)
                .Data(Planner.PlannerStore.Key, Planner.PlannerStore.NotesKey, Planner.PlannerStore.RemindersKey)
                .Fact("planner_days", () => Planner.PlannerApp.UpcomingDays.ToString(CultureInfo.InvariantCulture))
                .Fact("planner_notes", () => Planner.PlannerStore.MostNotes.ToString(CultureInfo.InvariantCulture))
                .Fact("planner_evening", () => Planner.PlannerStore.EveningHour.ToString(CultureInfo.InvariantCulture) + ":00")
                .Cleanup(() => Planner.PlannerDirector.Instance?.StopAll())
                .ResetData(Planner.PlannerStore.Reset);   // the director reads the (now empty) save again on its next second

            // The minimap and the GPS read the game's destination and the other features' places and change neither; nothing of theirs is in the save.
            Features.Add("navigation", "reallife_meta_title_nav")
                .Group("reallife_nav_header")
                .About("reallife_guide_nav_about").Help("reallife_guide_nav")
                .Owns(Settings.MinimapId, Settings.GpsId, Settings.MinimapRotateId, Settings.MinimapSizeId, Settings.MinimapZoomId, Settings.MinimapBaseId, Settings.NavOtherRouteId)
                .Present(Settings.NavOtherRouteId, () => OtherMods.VoogleRoute.Loaded)   // a row that is there only with that mod switched on
                .Objects(Nav.NavHud.PanelName, Nav.NavCityMap.ObjectName)
                .Fact("nav_arrive_foot", () => Nav.NavTracker.ArriveOnFoot.ToString("F0", CultureInfo.InvariantCulture))
                .Fact("nav_arrive_car", () => Nav.NavTracker.ArriveDriving.ToString("F0", CultureInfo.InvariantCulture))
                .Fact("nav_off_foot", () => Nav.NavTracker.OffOnFoot.ToString("F0", CultureInfo.InvariantCulture))
                .Fact("nav_off_car", () => Nav.NavTracker.OffDriving.ToString("F0", CultureInfo.InvariantCulture))
                .Fact("nav_view", () => Nav.NavView.BaseMetres.ToString("F0", CultureInfo.InvariantCulture))
                .Cleanup(() => Nav.NavDirector.Instance?.StopAll());

            // Civic life keeps its state on its director and puts its policies back itself when it is switched off or the city unloads.
            Features.Add("civic", "reallife_meta_title_civic")
                .Group("reallife_civic_header")
                .About("reallife_guide_civic_about").Help("reallife_guide_civic")
                .Owns(Settings.CivicId, Settings.CivicElectionsId, Settings.CivicConstructionId)
                .Data("reallife:civic_gov", "reallife:civic_works")
                .Contact(CityHallPhone.ContactId)
                .Fact("civic_levers", () => CivicCatalogue.Levers.ToString(CultureInfo.InvariantCulture))
                .Repair(CityHallPhone.Setup);

            // Its clean-up runs before the living city's: a wedding borrows up to four of the city's people and gives them back.
            Features.Add("family", "reallife_meta_title_family")
                .Group("reallife_family_header")
                .About("reallife_guide_family_about").Help("reallife_guide_family")
                .Owns(Settings.FamilyId, Settings.FamilyAtHomeId, Settings.FamilyWeddingId, Settings.FamilyChildrenId, Settings.FamilyRelativesId,
                    Settings.FamilyInterestId, Settings.FamilyYearWeeksId)
                .Preset(Settings.FamilyFrictionId, 0, 1, 1)
                .Rows("reallife_family_test_page")   // the Family page: with the phone contacts off it is the only way in, so it is the player's, not a test
                .Data("reallife:family")
                .Contact(Family.FamilyPhone.FamilyId, Family.FamilyPhone.PartnerId, Family.FamilyPhone.InLawId)
                .Objects("RealLifeOverhaul Shift Card")
                .Fact("family_dinner", () => Guide.Money(Family.FamilyBalance.DinnerCost))
                .Fact("family_move_in_days", () => Family.FamilyBalance.MoveInDaysTogether.ToString(CultureInfo.InvariantCulture))
                .Fact("family_sqm", () => Family.FamilyBalance.SqmForTwo.ToString(CultureInfo.InvariantCulture))
                .Fact("family_sqm_child", () => Family.FamilyBalance.SqmPerChild.ToString(CultureInfo.InvariantCulture))
                .Fact("family_ring_least", () => Guide.Money(Family.FamilyBalance.RingPrice[1]))
                .Fact("family_ring_most", () => Guide.Money(Family.FamilyBalance.RingPrice[Family.FamilyBalance.RingPrice.Length - 1]))
                .Fact("family_propose_days", () => Family.FamilyBalance.ProposeDaysTogether.ToString(CultureInfo.InvariantCulture))
                .Fact("family_honeymoon", () => Guide.Money(Family.FamilyBalance.HoneymoonCost))
                .Fact("family_honeymoon_days", () => Family.FamilyBalance.HoneymoonWindowDays.ToString(CultureInfo.InvariantCulture))
                .Fact("family_planner_fee", () => Guide.Percent(Family.FamilyBalance.PlannerFee))
                .Fact("family_venue_least", () => Guide.Money(Family.FamilyBalance.VenuePrice[0]))
                .Fact("family_venue_most", () => Guide.Money(Family.FamilyBalance.VenuePrice[Family.FamilyBalance.VenuePrice.Length - 1]))
                .Fact("family_deposit", () => Guide.Percent(Family.FamilyBalance.Deposit))
                .Fact("family_wedding_from", () => Family.FamilyBalance.WeddingFromHour.ToString(CultureInfo.InvariantCulture))
                .Fact("family_wedding_to", () => Family.FamilyBalance.WeddingToHour.ToString(CultureInfo.InvariantCulture))
                .Fact("family_adoption_fee", () => Guide.Money(Family.FamilyBalance.AdoptionFee))
                .Fact("family_children_most", () => Family.FamilyBalance.MaxChildren.ToString(CultureInfo.InvariantCulture))
                .Fact("family_scan", () => Guide.Money(Family.FamilyBalance.ScanPrice))
                .Fact("family_birth", () => Guide.Money(Family.FamilyBalance.BirthPrice))
                .Fact("family_nursery", () => Guide.Money(Family.FamilyBalance.NurseryPerMonth))
                .Fact("family_nanny", () => Guide.Money(Family.FamilyBalance.NannyPerMonth))
                .Fact("family_school", () => Guide.Money(Family.FamilyBalance.PrivateSchoolPerMonth))
                .Fact("family_year_weeks", () => Settings.FamilyYearWeeks.ToString(CultureInfo.InvariantCulture))
                .Fact("family_counselling", () => Guide.Money(Family.FamilyBalance.CounsellingCost))
                .Fact("family_settlement", () => Guide.Percent(Family.FamilyBalance.SettlementShare))
                .Fact("family_settlement_most", () => Guide.Money(Family.FamilyBalance.SettlementCap))
                .Cleanup(() => Family.FamilyDirector.Instance?.Halt(), -10)
                .ResetData(() =>
                {
                    Family.FamilyState.Reset();   // the director reads the (now empty) save again on its next frame
                    Family.Errands.Reset();
                })
                .Repair(Family.FamilyPhone.Setup);

            // The economy core. Off, the banks have their own terms, the app is not on the phone and nothing is recorded.
            // The game's loans are the game's own records: nothing of them is this feature's to remove.
            Features.Add("economy", "reallife_meta_title_economy")
                .Group("reallife_econ_header")
                .About("reallife_guide_economy_about").Help("reallife_guide_economy")
                .Owns(Settings.EconomyId, Settings.EconomyProductsId, Settings.EconomySharkId, Settings.EconomyConsequencesId, Settings.EconomyLevelId)
                .Preset(Settings.EconomyLevelId, Economy.ProductCatalogue.Relaxed, Economy.ProductCatalogue.Realistic, Economy.ProductCatalogue.Hardcore)   // (Standard is its default)
                .Data(Economy.CreditStore.Key)
                .Contact(Economy.DebtPhone.ContactIds)
                .Fact("econ_min", () => Whole(Economy.CreditCatalogue.ScoreMin))
                .Fact("econ_max", () => Whole(Economy.CreditCatalogue.ScoreMax))
                .Fact("econ_threshold", () => Whole(Economy.CreditCatalogue.LendingThreshold))
                .Fact("econ_start", () => Whole(Economy.CreditCatalogue.CleanStart))
                .Fact("econ_bands", EconomyBands)
                .Fact("econ_w_payments", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Payments]) + "%")
                .Fact("econ_w_owed", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Owed]) + "%")
                .Fact("econ_w_length", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Length]) + "%")
                .Fact("econ_w_legal", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Legal]) + "%")
                .Fact("econ_w_applications", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Applications]) + "%")
                .Fact("econ_fade_days", () => Whole(Economy.CreditCatalogue.PaymentFadeDays))
                .Fact("econ_miss_weight", () => Guide.Number(Economy.CreditCatalogue.MissWeight))
                .Fact("econ_history_days", () => Whole(Economy.CreditCatalogue.HistoryFullDays))
                .Fact("econ_application_days", () => Whole(Economy.CreditCatalogue.ApplicationFadeDays))
                .Fact("econ_best_from", () => Whole(Economy.CreditCatalogue.BestTermsFrom))
                .Fact("econ_cap_low", () => Guide.Percent(Economy.CreditCatalogue.CapShareAtThreshold))
                .Fact("econ_cap_high", () => Guide.Percent(Economy.CreditCatalogue.CapShareAtBest))
                .Fact("econ_rate_low", () => Whole(Economy.CreditCatalogue.RatePointsAtThreshold))
                .Fact("econ_rate_high", () => Whole(-Economy.CreditCatalogue.RatePointsAtBest))
                .Fact("econ_rate_floor", () => Whole(Economy.CreditCatalogue.RateFloor) + "%")
                .Fact("econ_fee", () => Guide.Percent(Economy.CreditCatalogue.PhoneFeeShare))
                .Fact("econ_fee_least", () => Guide.Money(Economy.CreditCatalogue.PhoneFeeLeast))
                .Fact("econ_loan_least", () => Guide.Money(Economy.CreditCatalogue.LoanLeast))
                .Fact("econ_events_shown", () => Whole(Economy.CreditCatalogue.LedgerShown))
                .Fact("econ_week", () => Whole(Economy.CashFlow.PeriodDays[Economy.CashFlow.Week]))
                .Fact("econ_month", () => Whole(Economy.CashFlow.PeriodDays[Economy.CashFlow.Month]))
                .Fact("econ_top", () => Whole(Economy.CashFlow.TopMost))
                // The credit products, what happens when payments are missed, the private lender and the ways out (Economy/Products/ProductCatalogue).
                .Fact("econ_od_low", () => Guide.Percent(Economy.ProductCatalogue.OverdraftShareAtThreshold))
                .Fact("econ_od_high", () => Guide.Percent(Economy.ProductCatalogue.OverdraftShareAtBest))
                .Fact("econ_od_least", () => Guide.Money(Economy.ProductCatalogue.OverdraftLeast))
                .Fact("econ_od_points", () => Whole(Economy.ProductCatalogue.OverdraftRatePoints))
                .Fact("econ_od_float", () => Guide.Money(Economy.ProductCatalogue.OverdraftFloat))
                .Fact("econ_card_low", () => Guide.Percent(Economy.ProductCatalogue.CardShareAtThreshold))
                .Fact("econ_card_high", () => Guide.Percent(Economy.ProductCatalogue.CardShareAtBest))
                .Fact("econ_card_least", () => Guide.Money(Economy.ProductCatalogue.CardLeast))
                .Fact("econ_card_points", () => Whole(Economy.ProductCatalogue.CardRatePoints))
                .Fact("econ_card_fee", () => Guide.Percent(Economy.ProductCatalogue.CardCashFeeShare))
                .Fact("econ_card_fee_least", () => Guide.Money(Economy.ProductCatalogue.CardCashFeeLeast))
                .Fact("econ_card_days", () => Whole(Economy.ProductCatalogue.CardStatementDays))
                .Fact("econ_card_grace", () => Whole(Economy.ProductCatalogue.CardGraceDays))
                .Fact("econ_card_min", () => Guide.Percent(Economy.ProductCatalogue.CardMinimumShare))
                .Fact("econ_card_min_least", () => Guide.Money(Economy.ProductCatalogue.CardMinimumLeast))
                .Fact("econ_biz_days", () => Whole(Economy.ProductCatalogue.BusinessDaysOnRecord))
                .Fact("econ_biz_multiple", () => Whole(Economy.ProductCatalogue.BusinessProfitMultiple))
                .Fact("econ_biz_points", () => Whole(-Economy.ProductCatalogue.BusinessRatePoints))
                .Fact("econ_car_dep_low", () => Whole(Economy.ProductCatalogue.CarDepositLeast) + "%")
                .Fact("econ_car_dep_high", () => Whole(Economy.ProductCatalogue.CarDepositMost) + "%")
                .Fact("econ_car_term_low", () => Whole(Economy.ProductCatalogue.CarTermLeast))
                .Fact("econ_car_term_high", () => Whole(Economy.ProductCatalogue.CarTermMost))
                .Fact("econ_car_points", () => Whole(Economy.ProductCatalogue.CarRatePoints))
                .Fact("econ_home_dep_low", () => Whole(Economy.ProductCatalogue.HomeDepositLeast) + "%")
                .Fact("econ_home_dep_high", () => Whole(Economy.ProductCatalogue.HomeDepositMost) + "%")
                .Fact("econ_home_term_low", () => Whole(Economy.ProductCatalogue.HomeTermLeast))
                .Fact("econ_home_term_high", () => Whole(Economy.ProductCatalogue.HomeTermMost))
                .Fact("econ_home_points", () => Whole(-Economy.ProductCatalogue.HomeRatePoints))
                .Fact("econ_home_multiple", () => Whole(Economy.ProductCatalogue.HomeCapMultiple))
                .Fact("econ_approval_days", () => Whole(Economy.ProductCatalogue.ApprovalDays))
                .Fact("econ_cons_fee", () => Guide.Percent(Economy.ProductCatalogue.ConsolidateFeeShare))
                .Fact("econ_cons_fee_least", () => Guide.Money(Economy.ProductCatalogue.ConsolidateFeeLeast))
                .Fact("econ_ladder", EconomyLadder)
                .Fact("econ_late_share", () => Guide.Percent(Economy.ProductCatalogue.LateFeeShare))
                .Fact("econ_late_least", () => Guide.Money(Economy.ProductCatalogue.LateFeeLeast))
                .Fact("econ_penalty_points", () => Whole(Economy.ProductCatalogue.PenaltyPoints))
                .Fact("econ_penalty_run", () => Whole(Economy.ProductCatalogue.PenaltyClearRun))
                .Fact("econ_plan_days", () => Whole(Economy.ProductCatalogue.PlanDays))
                .Fact("econ_repo_share", () => Guide.Percent(Economy.ProductCatalogue.RepossessionValueShare))
                .Fact("econ_divert", () => Guide.Percent(Economy.ProductCatalogue.BusinessDivertShare))
                .Fact("econ_unsecured_days", () => Whole(Economy.ProductCatalogue.UnsecuredClaimDays))
                .Fact("econ_claim_costs", () => Guide.Percent(Economy.ProductCatalogue.ClaimCostsShare))
                .Fact("econ_claim_costs_least", () => Guide.Money(Economy.ProductCatalogue.ClaimCostsLeast))
                .Fact("econ_shark_least", () => Guide.Money(Economy.ProductCatalogue.SharkLeast))
                .Fact("econ_shark_most", () => Guide.Money(Economy.ProductCatalogue.SharkMost))
                .Fact("econ_shark_days", () => Whole(Economy.ProductCatalogue.SharkIncomeDays))
                .Fact("econ_shark_rate", () => Guide.Percent(Economy.ProductCatalogue.SharkDailyShare))
                .Fact("econ_shark_due", () => Whole(Economy.ProductCatalogue.SharkDueDays))
                .Fact("econ_shark_extend", () => Guide.Percent(Economy.ProductCatalogue.SharkExtendFeeShare))
                .Fact("econ_shark_cap", () => Whole(Economy.ProductCatalogue.SharkCapMultiple))
                .Fact("econ_shark_warn", () => Whole(Economy.ProductCatalogue.SharkWarnDay))
                .Fact("econ_shark_assault", () => Whole(Economy.ProductCatalogue.SharkAssaultDay))
                .Fact("econ_shark_damage", () => Whole(Economy.ProductCatalogue.SharkDamageDay))
                .Fact("econ_shark_repeat", () => Whole(Economy.ProductCatalogue.SharkRepeatDays))
                .Fact("econ_shark_bill_low", () => Guide.Money(Economy.ProductCatalogue.SharkDamageLeast))
                .Fact("econ_shark_bill_high", () => Guide.Money(Economy.ProductCatalogue.SharkDamageMost))
                .Fact("econ_advice_days", () => Whole(Economy.ProductCatalogue.AdviceOverdueDays))
                .Fact("econ_arr_share", () => Guide.Percent(Economy.ProductCatalogue.ArrangementShare))
                .Fact("econ_arr_least", () => Guide.Money(Economy.ProductCatalogue.ArrangementLeast))
                .Fact("econ_arr_cap", () => Whole(Economy.ProductCatalogue.ArrangementScoreCap))
                .Fact("econ_arr_after", () => Whole(Economy.ProductCatalogue.ArrangementCapDaysAfter))
                .Fact("econ_arr_misses", () => Whole(Economy.ProductCatalogue.ArrangementMissesMost))
                .Fact("econ_bank_keep", () => Guide.Money(Economy.ProductCatalogue.BankruptKeepCash))
                .Fact("econ_bank_score", () => Whole(Economy.ProductCatalogue.BankruptScore))
                .Fact("econ_bank_days", () => Whole(Economy.ProductCatalogue.BankruptNoCreditDays))
                .Fact("econ_bank_from", () => Whole(Economy.ProductCatalogue.BankruptRebuildFrom))
                .Fact("econ_bank_per_day", () => Whole(Economy.ProductCatalogue.BankruptRebuildPerDay))
                .Fact("econ_bank_every", () => Whole(Economy.ProductCatalogue.BankruptEveryDays))
                .Cleanup(() => Economy.EconomyDirector.Instance?.StopAll())
                .ResetData(() => Economy.EconomyDirector.Instance?.Forget());   // the director opens a new file from the (now empty) save on its next second

            // The city's economy. Off, the banks' rates, the wholesale prices and every business's footfall are the game's
            // own and nothing is adjusted. It runs only while the economy core does. (Cleaned up before the core: order -1.)
            Features.Add("economy_city", "reallife_meta_title_economy_city")
                .Group("reallife_macro_header")
                .About("reallife_guide_economy_city_about").Help("reallife_guide_economy_city")
                .Owns(Settings.EconomyCityId)
                .Preset(Settings.EconomyCityLevelId, Economy.MacroCatalogue.Relaxed, Economy.MacroCatalogue.Realistic, Economy.MacroCatalogue.Hardcore)   // (Standard is its default)
                .Data(Economy.CityStore.Key, Economy.CityStore.TestKey)
                .Fact("macro_month_least", () => Whole(Economy.MacroCatalogue.MonthDaysLeast))
                .Fact("macro_base_low", () => CityPoints(-Economy.MacroCatalogue.BaseLeast))
                .Fact("macro_base_high", () => CityPoints(Economy.MacroCatalogue.BaseMost))
                .Fact("macro_move_most", () => CityPoints(Economy.MacroCatalogue.MoveMost))
                .Fact("macro_target", () => CityHundredths(Economy.MacroCatalogue.InflationTarget))
                .Fact("macro_inflation_low", () => CityHundredths(-Economy.MacroCatalogue.InflationLeast))
                .Fact("macro_inflation_high", () => CityHundredths(Economy.MacroCatalogue.InflationMost))
                .Fact("macro_state_least", () => Whole(Economy.MacroCatalogue.StateMonthsLeast))
                .Fact("macro_state_most", () => Whole(Economy.MacroCatalogue.StateMonthsMost))
                .Fact("macro_opening", () => Whole(Economy.MacroCatalogue.OpeningSteadyMonths))
                .Fact("macro_states", CityStates)
                .Fact("macro_income_low", () => Whole(Economy.MacroCatalogue.IncomeLeast / 10))
                .Fact("macro_income_high", () => Whole(Economy.MacroCatalogue.IncomeMost / 10))
                .Fact("macro_income_band", () => Whole(Economy.MacroCatalogue.IncomeBand / 10))
                .Fact("macro_over_band", () => Whole(Economy.MacroCatalogue.OverBand / 10))
                .Fact("macro_over_loss", () => Guide.Number(Economy.MacroCatalogue.OverLossPerPercent / 1000f))
                .Fact("macro_over_most", () => Whole(Economy.MacroCatalogue.OverLossMost))
                .Fact("macro_under_gain", () => Whole(Economy.MacroCatalogue.UnderGain))
                .Fact("macro_footfall_low", () => Whole(-Economy.MacroCatalogue.FootfallLeast))
                .Fact("macro_footfall_high", () => Whole(Economy.MacroCatalogue.FootfallMost))
                .Fact("macro_factor_low", () => Whole(100 - Economy.MacroCatalogue.FactorLeast / 10))
                .Fact("macro_factor_high", () => Whole(Economy.MacroCatalogue.FactorMost / 10 - 100))
                .Fact("macro_shock_days_low", () => Whole(Economy.MacroCatalogue.ShockDaysLeast))
                .Fact("macro_shock_days_high", () => Whole(Economy.MacroCatalogue.ShockDaysMost))
                .Fact("macro_adjust_band", () => Whole(Economy.MacroCatalogue.AdjustQuiet / 10))
                .Fact("macro_copycat_days", () => Whole(Economy.MacroCatalogue.CopycatDays))
                .Fact("macro_copycat_rest", () => Whole(Economy.MacroCatalogue.CopycatRestDays))
                .Fact("macro_swing_relaxed", () => Whole(Economy.MacroCatalogue.SwingPercent[Economy.MacroCatalogue.Relaxed]) + "%")
                .Fact("macro_swing_realistic", () => Whole(Economy.MacroCatalogue.SwingPercent[Economy.MacroCatalogue.Realistic]) + "%")
                .Fact("macro_swing_hardcore", () => Whole(Economy.MacroCatalogue.SwingPercent[Economy.MacroCatalogue.Hardcore]) + "%")
                .Cleanup(() => Economy.CityEconomyDirector.Instance?.StopAll(), -1)
                .ResetData(() => Economy.CityEconomyDirector.Instance?.Forget());   // the director opens a new economy from the (now empty) save on its next second
            // Investors and the market. Its rows stand under the economy's header on the options page; its file is its own
            // line in the save. Nothing of it is in the game's business or property records: removing the line removes it.
            Features.Add("ventures", "reallife_meta_title_ventures")
                .About("reallife_guide_ventures_about").Help("reallife_guide_ventures")
                .Owns(Settings.VenturesInvestorsId, Settings.VenturesMarketId, Settings.VenturesLevelId)
                .Preset(Settings.VenturesLevelId, Economy.Ventures.VenturesCatalogue.Relaxed, Economy.Ventures.VenturesCatalogue.Realistic, Economy.Ventures.VenturesCatalogue.Hardcore)   // (Standard is its default)
                .Data(Economy.Ventures.VenturesStore.Key)
                .Test("reallife_ven_test_offer", "reallife_ven_test_pitch_angel", "reallife_ven_test_pitch_tycoon", "reallife_ven_test_pitch_firm", "reallife_ven_test_list",
                    "reallife_ven_test_market", "reallife_ven_test_dividend", "reallife_ven_test_call", "reallife_ven_test_back")
                .Fact("ven_profit_days", () => Whole(Economy.Ventures.VenturesCatalogue.ProfitDaysNeeded))
                .Fact("ven_window", () => Whole(Economy.Ventures.VenturesCatalogue.ProfitWindowDays))
                .Fact("ven_average_days", () => Whole(Economy.Ventures.VenturesCatalogue.AverageDays))
                .Fact("ven_profit_floor", () => Guide.Money(Economy.Ventures.VenturesCatalogue.OfferProfitFloor))
                .Fact("ven_offer_every", () => Whole(Economy.Ventures.VenturesCatalogue.OfferEveryDays))
                .Fact("ven_offer_chance", () => Whole(Economy.Ventures.VenturesCatalogue.OfferChancePct) + "%")
                .Fact("ven_offer_liked", () => Whole(Economy.Ventures.VenturesCatalogue.OfferChancePct + Economy.Ventures.VenturesCatalogue.LikedBonusPct) + "%")
                .Fact("ven_value_days", () => Whole(Economy.Ventures.VenturesCatalogue.OfferValueDays))
                .Fact("ven_lapse_days", () => Whole(Economy.Ventures.VenturesCatalogue.OfferLapseDays))
                .Fact("ven_counter_money", () => Whole(Economy.Ventures.VenturesCatalogue.CounterMoneyPct) + "%")
                .Fact("ven_counter_share", () => Whole(Economy.Ventures.VenturesCatalogue.CounterSharePct) + "%")
                .Fact("ven_questions_least", () => Whole(Economy.Ventures.VenturesCatalogue.Questions[0]))
                .Fact("ven_questions_most", () => Whole(Economy.Ventures.VenturesCatalogue.Questions[2]))
                .Fact("ven_bank", () => Whole(Economy.Ventures.Pitch.Bank.Length))
                .Fact("ven_truth_base", () => Whole(Economy.Ventures.VenturesCatalogue.TruthBase))
                .Fact("ven_points", () => Whole(Economy.Ventures.VenturesCatalogue.PointsMost))
                .Fact("ven_catch_little", () => Whole(Economy.Ventures.VenturesCatalogue.StretchCatchPct) + "%")
                .Fact("ven_catch_much", () => Whole(Economy.Ventures.VenturesCatalogue.BoldCatchPct) + "%")
                .Fact("ven_caught_days", () => Whole(Economy.Ventures.VenturesCatalogue.CaughtCloseDays))
                .Fact("ven_caught_name", () => Whole(-Economy.Ventures.VenturesCatalogue.ReputationCaught))
                .Fact("ven_cold_name", () => Whole(-Economy.Ventures.VenturesCatalogue.ReputationCold))
                .Fact("ven_cooldown", () => Whole(Economy.Ventures.VenturesCatalogue.PitchCooldownDays))
                .Fact("ven_need_angel", () => Whole(Economy.Ventures.VenturesCatalogue.ScoreNeeded[0]))
                .Fact("ven_need_tycoon", () => Whole(Economy.Ventures.VenturesCatalogue.ScoreNeeded[1]))
                .Fact("ven_need_firm", () => Whole(Economy.Ventures.VenturesCatalogue.ScoreNeeded[2]))
                .Fact("ven_pitch_days_least", () => Whole(Economy.Ventures.VenturesCatalogue.ValueDaysLeast))
                .Fact("ven_pitch_days_most", () => Whole(Economy.Ventures.VenturesCatalogue.ValueDaysMost))
                .Fact("ven_budget_least", () => Whole(Economy.Ventures.VenturesCatalogue.BudgetShareLeastPct) + "%")
                .Fact("ven_budget_most", () => Whole(Economy.Ventures.VenturesCatalogue.BudgetShareMostPct) + "%")
                .Fact("ven_pledge_days", () => Whole(Economy.Ventures.VenturesCatalogue.PledgeDays))
                .Fact("ven_young_days", () => Whole(Economy.Ventures.VenturesCatalogue.YoungDays))
                .Fact("ven_share_cap", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.ShareCapBp / 10000f))
                .Fact("ven_demand_days", () => Whole(Economy.Ventures.VenturesCatalogue.DemandDays))
                .Fact("ven_board_extra", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.BoardExtraBp / 10000f))
                .Fact("ven_board_days", () => Whole(Economy.Ventures.VenturesCatalogue.BoardDays))
                .Fact("ven_cut", () => Whole(Economy.Ventures.VenturesCatalogue.CostCutPct) + "%")
                .Fact("ven_cut_days", () => Whole(Economy.Ventures.VenturesCatalogue.CostCutDays))
                .Fact("ven_call_kept", () => Whole(Economy.Ventures.VenturesCatalogue.CallKeptPct) + "%")
                .Fact("ven_call_least", () => Whole(Economy.Ventures.VenturesCatalogue.CallLeastPct) + "%")
                .Fact("ven_call_misses", () => Whole(Economy.Ventures.VenturesCatalogue.CallMissesMost))
                .Fact("ven_levels", VenturesLevels)
                .Fact("ven_buy_out_days", () => Whole(Economy.Ventures.VenturesCatalogue.BuyOutDays))
                .Fact("ven_buy_out_floor", () => Whole(Economy.Ventures.VenturesCatalogue.BuyOutFloorPct) + "%")
                .Fact("ven_companies", () => Whole(Economy.Ventures.VenturesCatalogue.Companies))
                .Fact("ven_commission", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.CommissionBp / 10000f))
                .Fact("ven_commission_least", () => Guide.Money(Economy.Ventures.VenturesCatalogue.CommissionLeast))
                .Fact("ven_holding", () => Economy.Ventures.VenturesCatalogue.HoldingMost.ToString("N0", CultureInfo.InvariantCulture))
                .Fact("ven_dividend_days", () => Whole(Economy.Ventures.VenturesCatalogue.DividendEveryDays))
                .Fact("ven_dividend_least", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.DividendLeastBp / 10000f))
                .Fact("ven_dividend_most", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.DividendMostBp / 10000f))
                .Fact("ven_news_cap", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.NewsCapBp / 10000f))
                .Fact("ven_history", () => Whole(Economy.Ventures.VenturesCatalogue.HistoryDays))
                .Cleanup(() => Economy.Ventures.VenturesDirector.Instance?.StopAll())
                .ResetData(() => Economy.Ventures.VenturesDirector.Instance?.Forget());   // the director opens a new file from the (now empty) save on its next second

            // Running a business. Off, none of it runs, nothing is charged or paid and every business's footfall is
            // without it. It runs only while the economy core does. (Cleaned up before the city's economy and the core: order -2.)
            Economy.CompanyGuide.AddFacts(Features.Add("economy_company", "reallife_meta_title_economy_company")
                .Group("reallife_co_header")
                .About("reallife_guide_economy_company_about").Help("reallife_guide_economy_company")
                .Owns(Settings.EconomyCompanyId, Settings.EconomyCompanyStaffId, Settings.EconomyCompanyCoverId, Settings.EconomyCompanyMarketingId, Settings.EconomyCompanyTaxId, Settings.EconomyCompanyHideId, Settings.EconomyCompanyLicencesId)
                .Preset(Settings.EconomyCompanyLevelId, Economy.CompanyCatalogue.Relaxed, Economy.CompanyCatalogue.Realistic, Economy.CompanyCatalogue.Hardcore)   // (Standard is its default)
                .Data(Economy.CompanyStore.Key, Economy.CompanyStore.TestKey)
                .Cleanup(() => Economy.CompanyDirector.Instance?.StopAll(), -2)
                .ResetData(() => Economy.CompanyDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second

            // The staff rota planner. Its rows stand under the company's header on the options page and its page is a tab of
            // the Company app. It keeps nothing in a save and leaves nothing behind: off, the tab says so and no rota is written.
            Economy.RotaGuide.AddFacts(Features.Add("economy_rota", "reallife_meta_title_economy_rota")
                .About("reallife_guide_economy_rota_about").Help("reallife_guide_economy_rota")
                .Owns(Settings.EconomyRotaId)
                .Test("reallife_rota_test_open", "reallife_rota_test_fill", "reallife_rota_test_back"));

            // The cleaning service and selling a business. Their rows stand under the company's header on the options page
            // and their pages are the Services app. They share one line of the save. Off, the cleaning service plans no
            // round, shows no cleaner and charges nothing; selling a business offers, pays and charges nothing.
            Economy.ServicesGuide.AddCleaningFacts(Features.Add("economy_cleaning", "reallife_meta_title_economy_cleaning")
                .About("reallife_guide_economy_cleaning_about").Help("reallife_guide_economy_cleaning")
                .Owns(Settings.EconomyCleaningId)
                .Test("reallife_svc_test_round", "reallife_svc_test_contract", "reallife_svc_test_week")
                .Data(Economy.ServicesStore.Key)
                .Cleanup(() => Economy.ServicesDirector.Instance?.StopAll(), -3)
                .ResetData(() => Economy.ServicesDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second
            Economy.ServicesGuide.AddSaleFacts(Features.Add("economy_sale", "reallife_meta_title_economy_sale")
                .About("reallife_guide_economy_sale_about").Help("reallife_guide_economy_sale")
                .Owns(Settings.EconomySaleId)
                .Test("reallife_svc_test_offer", "reallife_svc_test_sale_paper", "reallife_svc_test_part"));
            Roommates.RoommateGuide.Register();   // sharing the flat: the feature, its option, its save line, its tests and its Guide figures (Scripts/Home/Roommates)
        }

        private static string Whole(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // Quarter points as points, and hundredths of a per cent as per cent, for the city's economy's page.
        private static string CityPoints(int quarters)
        {
            return (quarters / (float)Economy.MacroCatalogue.QuartersPerPoint).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string CityHundredths(int hundredths)
        {
            return (hundredths / 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        // One line per state of the cycle, from the table the model itself reads.
        private static string CityStates()
        {
            var text = new StringBuilder();
            for (int s = 0; s < Economy.MacroCatalogue.StateCount; s++)
            {
                if (s == (int)Economy.CycleState.Steady)
                {
                    continue;   // the state everything is measured against
                }
                text.Append("- ").Append("reallife_guide_economy_city_state".GetLocalization()
                    .Replace("{state}", Economy.CityText.StateKeys[s].GetLocalization())
                    .Replace("{footfall}", Economy.CityText.Percent(Economy.MacroCatalogue.StateFootfall[s]))
                    .Replace("{spend}", Economy.CityText.Change(Economy.MacroCatalogue.StateSpend[s]))
                    .Replace("{property}", Economy.CityText.Change(Economy.MacroCatalogue.StateProperty[s]))).Append('\n');
            }
            return text.ToString();
        }

        // One line per level of "How demanding investors are", from the tables the deals themselves count by.
        private static string VenturesLevels()
        {
            var text = new StringBuilder();
            string[] names = { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" };
            for (int level = 0; level < names.Length; level++)
            {
                int misses = Economy.Ventures.VenturesCatalogue.MissesToCall[level];
                text.Append("- ").Append((misses > 0 ? "reallife_guide_ventures_level" : "reallife_guide_ventures_level_never").GetLocalization()
                    .Replace("{level}", names[level].GetLocalization())
                    .Replace("{growth}", Whole(Economy.Ventures.VenturesCatalogue.GrowthScalePct[level]) + "%")
                    .Replace("{misses}", Whole(misses))
                    .Replace("{days}", Whole(Economy.Ventures.VenturesCatalogue.CallDays[level]))).Append('\n');
            }
            return text.ToString();
        }

        // One line per level of "How hard not paying hits", from the table the lenders themselves count by.
        private static string EconomyLadder()
        {
            var text = new StringBuilder();
            string[] names = { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" };
            for (int level = 0; level < names.Length; level++)
            {
                int[] counts = Economy.ProductCatalogue.Counts(level);
                var last = Economy.ProductCatalogue.LastStep[level];
                text.Append("- ").Append("reallife_guide_economy_ladder".GetLocalization()
                    .Replace("{level}", names[level].GetLocalization())
                    .Replace("{reminder}", Whole(counts[0])).Replace("{fee}", Whole(counts[1])).Replace("{rate}", Whole(counts[2])).Replace("{call}", Whole(counts[3])));
                if (last >= Economy.LadderStep.Repossession)
                {
                    text.Append("reallife_guide_economy_ladder_security".GetLocalization().Replace("{count}", Whole(counts[4])));
                }
                text.Append((last >= Economy.LadderStep.Claim ? "reallife_guide_economy_ladder_claim" : "reallife_guide_economy_ladder_stop").GetLocalization().Replace("{count}", Whole(counts[5])));
                if (level == Economy.ProductCatalogue.Relaxed)
                {
                    text.Append(' ').Append("reallife_guide_economy_ladder_half".GetLocalization());
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        // One line per grade, from the catalogue the rating itself is graded by.
        private static string EconomyBands()
        {
            var text = new StringBuilder();
            for (int g = 0; g < Economy.CreditCatalogue.GradeFrom.Length; g++)
            {
                text.Append("- ").Append("reallife_guide_economy_band".GetLocalization()
                    .Replace("{from}", Whole(Economy.CreditCatalogue.GradeFrom[g]))
                    .Replace("{to}", Whole(Economy.CreditCatalogue.GradeTo((Economy.CreditGrade)g)))
                    .Replace("{grade}", Economy.CreditCatalogue.GradeKeys[g].GetLocalization())).Append('\n');
            }
            return text.ToString();
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
