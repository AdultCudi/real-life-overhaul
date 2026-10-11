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
                .Data("reallife:jobs", "reallife:job_rental")
                .Contact(JobPhone.ContactId)
                .Objects("RealLifeOverhaul Job HUD", "RealLifeOverhaul Shift Card")
                .Fact("vendor_quick", () => Guide.Number(VendorJob.QuickTip))
                .Fact("vendor_wrong", () => Guide.Number(VendorJob.WrongCost))
                .Fact("vendor_patience", () => Guide.Number(VendorJob.Patience))
                .Fact("vendor_abandon", () => Guide.Number(VendorJob.AbandonAfter))
                .Fact("vendor_minutes_each", () => ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Vendor)).ToString(CultureInfo.InvariantCulture))
                .Fact("vendor_skip_share", () => Guide.Percent(ShiftRates.Share))
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
                .Cleanup(() =>
                {
                    JobDirector.Instance?.Quit();   // the shift ends, its people and props go back, a hired van is handed in
                    if (JobHud.Exists)
                    {
                        JobHud.Instance.HideAll(true);
                    }
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
                .Objects("RealLifeOverhaul HUD")
                .Fact("life_count", () => LifeEventCatalogue.All.Length.ToString(CultureInfo.InvariantCulture))
                .Fact("doctor_fee", () => Guide.Money(LifeEvents.DoctorFee))
                .Fact("ambulance_fee", () => Guide.Money(LifeEvents.AmbulanceFee))
                .Fact("tow_fee", () => Guide.Money(VehicleTow.Fee))
                .Cleanup(() =>
                {
                    LifeEvents.Instance?.Cure(null);   // the cold and its drain on energy end; no toast
                    if (Hud.Exists)
                    {
                        Hud.Instance.HideMarker();
                    }
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
                .About("reallife_guide_performance_about").Help("reallife_guide_performance");
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
                .Objects("RealLifeOverhaul Home HUD")
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
                .Objects("RealLifeOverhaul Transport HUD")
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

            // Leisure's records live in ModState's part of the save: "life" resets them (ModState.Reset).
            Features.Add("leisure", "reallife_meta_title_leisure")
                .Group("reallife_leisure_header")
                .About("reallife_guide_leisure_about").Help("reallife_guide_leisure")
                .Owns(Settings.LeisureId, Settings.ClothesShopsId, Settings.NightLifeId, Settings.AmbientSoundId, Settings.PhotoModeId)
                .Data("reallife:leisure_*")
                .Objects("RealLifeOverhaul Leisure HUD")
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
                .Objects("RealLifeOverhaul Social HUD")
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
                .Objects("RealLifeOverhaul Dispatch Board")
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
                .Rows("reallife_garage_test_vehicles")   // "My vehicles": a player's panel (a two-wheeler that cannot be reached is fetched from it), not a test
                .Data("reallife:garage_*")
                .Objects("RealLifeOverhaul Garage UI")
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
                .Objects("RealLifeOverhaul Justice HUD", "RealLifeOverhaul Justice Panel", "RealLifeOverhaul Courthouse Sign", "RealLifeOverhaul Litter")
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
                .Owns(Settings.MinimapId, Settings.GpsId, Settings.MinimapRotateId, Settings.MinimapSizeId, Settings.MinimapZoomId)
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
                .Objects("RealLifeOverhaul Family HUD")
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
