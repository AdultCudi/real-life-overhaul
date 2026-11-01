using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's entry in the mod's feature list: its switch, its motion and each screen's own switch, its tests
    /// and its Guide page.
    /// Called once when the feature list is built.
    /// </summary>
    public static class UiGuide
    {
        public static void Register()
        {
            Features.Add("ui", "reallife_meta_title_ui")
                .About("reallife_guide_ui_about").Help("reallife_guide_ui")
                .Owns(Settings.UiInterfaceId, Settings.UiMotionId)
                .Owns(UiScreenSwitches.OptionIds())
                .Test("reallife_ui_test_gallery", "reallife_ui_test_parts", "reallife_ui_test_contacts", "reallife_ui_test_population", "reallife_ui_test_flare", "reallife_ui_test_firm")
                .Test("reallife_ui_test_all_apps", "reallife_ui_test_question", "reallife_ui_test_question_negative", "reallife_ui_test_acknowledge",
                    "reallife_ui_test_text_box", "reallife_ui_test_mod_box", "reallife_ui_test_notices", "reallife_ui_test_contacts_switch")
                .Test("reallife_phone_test_second", "reallife_phone_test_kit_folder", "reallife_phone_test_classic")
                .Test("reallife_ui_test_employees", "reallife_ui_test_employee_page", "reallife_ui_test_employees_switch")
                .Cleanup(() => UiDirector.RemoveAll("cleaned up"), 0);
        }
    }
}
