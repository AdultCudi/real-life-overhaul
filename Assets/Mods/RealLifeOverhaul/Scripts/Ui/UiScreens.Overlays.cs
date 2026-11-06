using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The cards over the world's items (Ui/Overlays): the small card under the pointer over a shelf, a machine, a till, a table or
        // a person; the large card a click opens over a shelf, a fridge, a machine, a radio, a vehicle and the rest; and a person's
        // info panel. Each has its switch under "RLO interface: items in the world".
        static partial void AddOverlays(List<UiScreenDef> screens)
        {
            string[] common =
            {
                "reallife_ui_overlay_stock", "reallife_ui_overlay_price", "reallife_ui_overlay_item", "reallife_ui_overlay_margin", "reallife_ui_overlay_margin_of",
                "reallife_ui_overlay_delivery", "reallife_ui_overlay_today", "reallife_ui_overlay_tomorrow", "reallife_ui_overlay_of", "reallife_ui_overlay_customers",
                "reallife_ui_overlay_security", "reallife_ui_overlay_needs", "reallife_ui_overlay_recipe", "reallife_ui_overlay_per_hour", "reallife_ui_overlay_nobody",
                "reallife_ui_overlay_pallets", "reallife_ui_overlay_seats", "reallife_ui_overlay_taken", "reallife_ui_overlay_queue", "reallife_ui_overlay_mood",
                "reallife_ui_overlay_mood_low", "reallife_ui_overlay_mood_middle", "reallife_ui_overlay_mood_high", "reallife_ui_overlay_until", "reallife_ui_overlay_off_shift",
                "reallife_ui_overlay_no_shift"
            };
            screens.AddRange(new[]
            {
                new UiScreenDef(UiOverlays.HoverId, UiTemplate.Hud, null, common).Switch(UiScreenSwitches.Overlays),
                new UiScreenDef(UiOverlays.ClickId, UiTemplate.Hud, null, new[]
                {
                    "reallife_ui_overlay_holds", "reallife_ui_overlay_choose", "reallife_ui_overlay_workstation", "reallife_ui_overlay_makes", "reallife_ui_overlay_short",
                    "reallife_ui_overlay_station", "reallife_ui_overlay_song", "reallife_ui_overlay_volume", "reallife_ui_overlay_next_station", "reallife_ui_overlay_mute",
                    "reallife_ui_overlay_unmute", "reallife_ui_overlay_rename", "reallife_ui_overlay_radio", "reallife_ui_overlay_employee", "reallife_ui_overlay_stock"
                }).Switch(UiScreenSwitches.Overlays),
                new UiScreenDef(UiOverlays.PersonId, UiTemplate.InfoPanel, null, new[]
                {
                    "reallife_ui_overlay_employee", "reallife_ui_overlay_absent", "reallife_ui_overlay_on_shift", "reallife_ui_overlay_off_shift", "reallife_ui_overlay_hours",
                    "reallife_ui_overlay_next_shift", "reallife_ui_overlay_range", "reallife_ui_overlay_worked", "reallife_ui_overlay_h_min", "reallife_ui_overlay_min",
                    "reallife_ui_overlay_today_shift", "reallife_ui_overlay_how", "reallife_ui_overlay_pay", "reallife_ui_overlay_wage", "reallife_ui_overlay_per_hour_money",
                    "reallife_ui_overlay_earned", "reallife_ui_overlay_page", "reallife_ui_overlay_open_page", "reallife_ui_overlay_open", "reallife_ui_overlay_wants",
                    "reallife_ui_overlay_met", "reallife_ui_overlay_unmet", "reallife_ui_overlay_no_shift", "reallife_ui_overlay_mood", "reallife_ui_overlay_mood_low",
                    "reallife_ui_overlay_mood_middle", "reallife_ui_overlay_mood_high"
                }).Switch(UiScreenSwitches.Overlays),
                new UiScreenDef(UiOverlays.PointerId, UiTemplate.Hud, null, new string[0]).Switch(UiScreenSwitches.Overlays),
            });
        }
    }
}
