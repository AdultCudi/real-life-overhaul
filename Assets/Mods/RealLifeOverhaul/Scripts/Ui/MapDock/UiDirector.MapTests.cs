using System;
using System.Collections;
using City.CityMap;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the city map (Options, Tests): the map opened, as the M key opens it, and said to be on the kit or the game's own; and the map's
    /// screens switched to the game's own and back. Nothing is saved: the switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] MapScreens = { MapKits.FiltersId, MapKits.SubwayId, MapKits.PickerId, MapKits.DistrictId };

        /// <summary>Options, Tests: the city map, opened as the M key opens it.</summary>
        public void TestMapOpen()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city == null || city.cityMap == null || (!CityMap.IsOpen && !CityMap.CanOpenMap()))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_map_none", null, 8f);
                    return;
                }
                if (!CityMap.IsOpen)
                {
                    city.cityMap.Toggle();
                }
                StartCoroutine(Guard.Routine(MapSoon()));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the city map's screens switched to the game's own, and back on the next press.</summary>
        public void TestMapSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(MapKits.FiltersId);
                for (int i = 0; i < MapScreens.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(MapScreens[i]), on);
                }
                Slow();
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_map_kit" : "reallife_ui_test_map_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The map shows a frame or two after it is asked for: then says whether the kit draws its filters.
        private IEnumerator MapSoon()
        {
            yield return null;
            yield return null;
            if (!CityMap.IsOpen)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_map_none", null, 8f);
                yield break;
            }
            Notifications.Show(NotificationType.Info, MapKits.FiltersShowing ? "reallife_ui_test_buy_on_kit" : "reallife_ui_test_buy_on_game", null, 8f);
        }
    }
}
