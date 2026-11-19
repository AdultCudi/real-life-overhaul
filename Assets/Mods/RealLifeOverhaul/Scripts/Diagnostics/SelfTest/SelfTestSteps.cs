namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// The step table: a class for every kind of SelfTestKinds, made by name when a plan reaches it. Every name in
    /// SelfTestKinds has a case here and every case a name there; an offline check (EconomyCheck --selftest) reads the two
    /// from the sources and holds them together. A developer diagnostic: nothing here runs without a plan.
    /// </summary>
    internal static partial class SelfTestSteps
    {
        public static SelfTestStep Make(string kind)
        {
            switch (kind)
            {
                case "wait_menu": return new WaitMenuStep();
                case "wait_city": return new WaitCityStep();
                case "wait_seconds": return new WaitSecondsStep();
                case "wait_minutes": return new WaitMinutesStep();
                case "skip_time": return new SkipTimeStep();
                case "set_clock": return new SetClockStep();
                case "load_save": return new LoadSaveStep();
                case "new_game": return new NewGameStep();
                case "save_game": return new SaveGameStep();
                case "return_menu": return new ReturnMenuStep();
                case "set_option": return new SetOptionStep();
                case "assert_option": return new AssertOptionStep();
                case "open_phone": return new OpenPhoneStep();
                case "close_screen": return new CloseScreenStep();
                case "open_app": return new OpenAppStep();
                case "open_all_apps": return new OpenAllAppsStep();
                case "open_screen": return new OpenScreenStep();
                case "open_every_screen": return new OpenEveryScreenStep();
                case "kit_press": return new KitPressStep();
                case "kit_type": return new KitTypeStep();
                case "kit_scroll": return new KitScrollStep();
                case "press_button": return new PressButtonStep();
                case "list_buttons": return new ListButtonsStep();
                case "console": return new ConsoleStep();
                case "set_money": return new SetMoneyStep();
                case "teleport": return new TeleportStep();
                case "found_business": return new FoundBusinessStep();
                case "screenshot": return new ScreenshotStep();
                case "assert_no_faults": return new AssertNoFaultsStep();
                case "assert_no_log_errors": return new AssertNoLogErrorsStep();
                case "assert_kit_showing": return new AssertKitShowingStep();
                case "assert_text": return new AssertTextStep();
                case "assert_mod_loaded": return new AssertModLoadedStep();
                case "assert_log_has": return new AssertLogHasStep();
                case "measure": return new MeasureStep();
                case "monologue": return new MonologueStep();
                case "game_open": return new GameOpenStep();
                case "assert_money": return new AssertMoneyStep();
                case "assert_owns": return new AssertOwnsStep();
                case "assert_bank_line": return new AssertBankLineStep();
                case "assert_notice": return new AssertNoticeStep();
                case "write_wav": return new WriteWavStep();
                case "set_year": return new SetYearStep();
                case "kit_commands": return new KitCommandsStep();
                case "confirm": return new ConfirmStep();
                case "enter_building": return new EnterBuildingStep();
                case "leave_building": return new LeaveBuildingStep();
                case "set_day": return new SetDayStep();
                case "find_place": return new FindPlaceStep();
                case "assert_home": return new AssertHomeStep();
                case "call": return new CallStep();
                case "read": return new ReadStep();
                case "note": return new NoteStep();
            }
            return null;
        }
    }
}
