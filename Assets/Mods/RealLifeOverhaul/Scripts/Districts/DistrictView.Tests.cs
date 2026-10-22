using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Districts
{
    /// <summary>The district view, part: the options page's tests.</summary>
    public sealed partial class DistrictView
    {
        /// <summary>Options-page test: opens the city map with the district view on (the wealth layer if none was ticked).</summary>
        public void TestOpen()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (_broken || _choice == null || city == null || city.cityMap == null || !ModState.Loaded)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_refused", null, 8f);
                return;
            }
            if (!Settings.DistrictView)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_off", null, 8f);
                return;
            }
            if (_choice.Picked < 0)
            {
                _choice.Pick((int)DistrictLayer.Wealth);
            }
            if (!CityMap.IsOpen && CityMap.CanOpenMap())
            {
                city.cityMap.Toggle();
            }
            Logger?.Info("District view: test, the map opened with the layer '" + DistrictRules.Ids[Math.Max(0, _choice.Picked)] + "'.");
        }

        /// <summary>Options-page test: a crime report where the player stands, as if one had been reported there.</summary>
        public void TestCrime()
        {
            if (_broken || !_logLoaded || Helpers.PlayerHelper.PlayerController == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_refused", null, 8f);
                return;
            }
            string district = DistrictGame.PlayerDistrict();
            if (string.IsNullOrEmpty(district))
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_refused", null, 8f);
                return;
            }
            Note(district);
            var data = new Dictionary<string, string>(1) { { "district", DistrictGame.Name(district) } };
            Notifications.Show(NotificationType.Info, "reallife_dv_test_crime_done", data, 8f);
            Logger?.Info("District view: test, a crime report noted in " + district + ".");
        }
    }
}
