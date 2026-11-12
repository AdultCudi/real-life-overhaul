using BigAmbitions.Mods;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's switch for each screen on the options page, one row each from the screens' entries
    /// (UiScreenSwitches), so a job that converts a screen adds its switch with the one word in its entry. The page puts
    /// them in order by Meta/OptionsTree, which places them the same way. Called once when the page is built.
    /// </summary>
    public static class UiSwitchOptions
    {
        public static ModOptions Add(ModOptions options)
        {
            foreach (UiScreenDef def in UiScreenSwitches.All)
            {
                string option = UiScreenSwitches.OptionOf(def.Id);
                options = options.AddToggle(option, UiScreenSwitches.LabelOf(def.Id), UiScreenSwitches.Value(option), on => UiScreenSwitches.Set(option, on));
            }
            return options;
        }
    }
}
