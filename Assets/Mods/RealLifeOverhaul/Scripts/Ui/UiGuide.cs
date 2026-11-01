using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's entry in the mod's feature list: its switch and its motion, its two tests and its Guide page.
    /// Called once when the feature list is built.
    /// </summary>
    public static class UiGuide
    {
        public static void Register()
        {
            Features.Add("ui", "reallife_meta_title_ui")
                .About("reallife_guide_ui_about").Help("reallife_guide_ui")
                .Owns(Settings.UiInterfaceId, Settings.UiMotionId)
                .Test("reallife_ui_test_gallery", "reallife_ui_test_contacts", "reallife_ui_test_population")
                .Cleanup(() => ContactsKit.Remove("cleaned up"), 0);
        }
    }
}
