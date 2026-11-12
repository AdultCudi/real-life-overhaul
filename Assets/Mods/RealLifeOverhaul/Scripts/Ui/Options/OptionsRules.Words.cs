namespace RealLifeOverhaul.Ui
{
    // The actions that have a sentence of their own: game-free and icon-free, so the key checks can read them alone.
    public static partial class OptionsRules
    {
        private const string Mod = "reallife";
        /// <summary>The start of each action's sentence key; built from parts so the key list does not read it as a key.</summary>
        public const string ActPrefix = Mod + "_ui_opt_act_";

        /// <summary>
        /// The actions that have a sentence of their own under their name, each a key reallife_ui_opt_act_ and the action's
        /// name in lower case. The designer's tools (an action named Open and the tool's name) share one.
        /// </summary>
        public static readonly string[] Described =
        {
            "move", "rotateleft", "rotateright", "rotate", "zoom", "togglerunning", "autorun", "reversecarcam", "fastmappan", "pause", "sleep",
            "interact", "secondaryinteract", "specialinteract", "cancel", "confirm", "nextoption", "previousoption", "click", "rightclick", "sell",
            "performactionwithoutconfirm", "sliderleft", "sliderright", "selectmultipleelements", "snapfreeplacement", "invertitemrotation", "rotateitem",
            "menu", "opennotifications", "openhelp", "openbugreport", "quicksave", "openmap", "openbizman", "skipsong", "showallitemoverlays",
            "steering", "throttle", "brakes", "handbrake", "vehicleleftblinker", "vehiclerightblinker", "vehiclehorn",
            "undo", "redo", "togglewalls", "showupperfloor", "showlowerfloor", "specialbehavior", "apply", "exit", "openblueprintpanel",
            "interiordesignerundo", "interiordesignerredo", "interiordesignerpicker", "tool"
        };
    }
}
