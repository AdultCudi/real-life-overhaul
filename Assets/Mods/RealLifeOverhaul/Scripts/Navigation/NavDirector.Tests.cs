using System;
using Helpers;
using Parking.UndergroundParking;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>The director's test buttons for the GPS.</summary>
    public sealed partial class NavDirector
    {
        private bool Refused(string test)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null || !_graph.Ready)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return true;
            }
            if (_broken || !Settings.Gps)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return true;
            }
            if (RouteLeft)
            {
                TestRefusals.TellKey(Logger, test, "reallife_nav_test_other_mod");
                return true;
            }
            if (BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.Indoors);
                return true;
            }
            return false;
        }

        /// <summary>"Test: route to the nearest police station".</summary>
        public void TestStation()
        {
            try
            {
                if (Refused("Route to a police station"))
                {
                    return;
                }
                Func<bool> guide = NavBridges.GuideToStation;
                if (guide == null || !guide())
                {
                    TestRefusals.Tell(Logger, "Route to a police station", TestRefusal.NoPrecinct);
                    return;
                }
                _next = 0f;
                Logger?.Info("Navigation test: the nearest police station is the destination.");
                Notifications.Show(NotificationType.Info, "reallife_nav_test_set", null, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Test: route to the far end of the city": the lane point furthest from the player, the longest search from here.</summary>
        public void TestFar()
        {
            try
            {
                Far();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Far()
        {
            if (Refused("Route across the city"))
            {
                return;
            }
            GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
            RouteSearch drive = _graph.Drive;
            int furthest = -1;
            float most = 0f;
            for (int i = 0; i < drive.Count; i++)
            {
                if (!_graph.Usable[i] || _graph.Junction[i])
                {
                    continue;
                }
                float dx = drive.X[i] - _px;
                float dz = drive.Z[i] - _pz;
                float sqr = dx * dx + dz * dz;
                if (sqr > most)
                {
                    most = sqr;
                    furthest = i;
                }
            }
            if (furthest < 0 || guiders == null || guiders.destinationGuider == null)
            {
                TestRefusals.Tell(Logger, "Route across the city", TestRefusal.NotLoaded);
                return;
            }
            SaveGameManager.Current.customDestination = null;
            GuidersManager.SetGuiderTarget(new Vector3(drive.X[furthest], drive.Y[furthest] + 1.5f, drive.Z[furthest]), NavText.T("reallife_nav_test_far_name"),
                                           guiders.destinationGuider.poiICon, guiders.destinationGuider.poiBackgroundColor, DirectionGuiderType.Destination);
            _next = 0f;
            Logger?.Info("Navigation test: destination set " + Mathf.Sqrt(most).ToString("F0") + " m away in a straight line, at the lane point furthest from the player.");
            Notifications.Show(NotificationType.Info, "reallife_nav_test_set", null, 8f);
        }

        /// <summary>"Test: clear the destination".</summary>
        public void TestClear()
        {
            try
            {
                if (SaveGameManager.Current == null || InstanceBehavior<GuidersManager>.Instance == null)
                {
                    TestRefusals.Tell(Logger, "Clear the destination", TestRefusal.NotLoaded);
                    return;
                }
                SaveGameManager.Current.customDestination = null;
                GuidersManager.ResetGuider(DirectionGuiderType.Destination);
                _next = 0f;
                Notifications.Show(NotificationType.Info, "reallife_nav_test_cleared", null, 6f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
