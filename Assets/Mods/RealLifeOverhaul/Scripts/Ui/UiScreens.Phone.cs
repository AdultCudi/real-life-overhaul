using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The phone, drawn on the kit over the game's own phone (Phone/PhoneKit).
        static partial void AddPhone(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("phone", UiTemplate.Phone, "PhoneScreen", new[]
            {
                "reallife_phone_next", "reallife_phone_next_none", "reallife_phone_folder_money", "reallife_phone_folder_work", "reallife_phone_folder_life",
                "reallife_phone_folder_business", "reallife_phone_folder_setup", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_page",
                "reallife_ui_prompt_back", "reallife_phone_other_app", "reallife_phone_driver", "reallife_phone_other_mods",
                "reallife_phone_tool_note", "reallife_phone_put_away"
            }).Switch(UiScreenSwitches.Apps));
        }
    }
}
