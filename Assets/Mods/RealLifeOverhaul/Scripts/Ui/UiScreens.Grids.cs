using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The app window's page shapes, each on its gallery page in the app it is made for: samples the waves build on, run
        // by no screen of their own yet (the Calendar, BizMan's schedule, the Guide, the mod's settings, Contacts, Flare and
        // the City Ledger take them up in their waves).
        static partial void AddGrids(List<UiScreenDef> screens)
        {
            string[] prompts = { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back" };
            screens.AddRange(new[]
            {
                new UiScreenDef("shape_diary", UiTemplate.AppWindow, null, new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_page", "reallife_ui_prompt_back", "reallife_calendar_app", "reallife_calendar_tab_day",
                    "reallife_calendar_tab_month", "reallife_calendar_month_3", "reallife_calendar_kind_in", "reallife_calendar_kind_out"
                }),
                new UiScreenDef("shape_board", UiTemplate.AppWindow, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_page", "reallife_ui_prompt_back" }),
                new UiScreenDef("shape_reader", UiTemplate.AppWindow, null, prompts),
                new UiScreenDef("shape_settings", UiTemplate.AppWindow, null, prompts),
                new UiScreenDef("shape_conversation", UiTemplate.AppWindow, null, new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "contactcategoryname_rivals"
                }),
                new UiScreenDef("shape_conversation_docked", UiTemplate.AppWindow, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "reallife_ui_prompt_back" }),
                new UiScreenDef("shape_messages", UiTemplate.AppWindow, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_contacts_send" }),
                new UiScreenDef("shape_feed", UiTemplate.AppWindow, null, prompts),
                new UiScreenDef("shape_edition", UiTemplate.AppWindow, null, prompts),
            });
        }
    }
}
