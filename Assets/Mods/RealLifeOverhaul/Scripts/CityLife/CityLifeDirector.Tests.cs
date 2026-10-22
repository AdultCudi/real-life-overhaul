using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The living city's test buttons on the options page.</summary>
    public sealed partial class CityLifeDirector
    {
        /// <summary>Options-page test: the family's next activity just ahead of the player, in view, in place of whatever is out.</summary>
        public void Test(CityFamily family)
        {
            if (_failed)
            {
                Notifications.Show(NotificationType.Warning, "reallife_city_test_failed", null, 8f);
                return;
            }
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (player == null || !ModState.Loaded || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_city_test_outdoors", null, 8f);
                    return;
                }
                while (_people.Warm(player.transform.position + Vector3.down * 40f))
                {
                    // A test cannot wait for the pool to fill a person at a time.
                }
                _warm = true;
                if (!Ready())
                {
                    Notifications.Show(NotificationType.Warning, "reallife_city_test_loading", null, 8f);
                    return;
                }
                EndAll();
                Vector3 origin = player.transform.position;
                Camera camera = GameManager.GetMainCamera();
                Vector3 ahead = camera != null ? camera.transform.forward : player.transform.forward;
                ahead.y = 0f;
                ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;

                int count = 0;
                foreach (CityEntry entry in CityCatalogue.All)
                {
                    count += entry.Family == family ? 1 : 0;
                }
                CityRefusal last = CityRefusal.OffPavement;
                string lastName = null;
                // Each activity of the family in turn, starting with the next, until one finds room near here.
                for (int step = 0; step < count; step++)
                {
                    CityEntry entry = Nth(family, (_testNext[(int)family] + step) % count);
                    CitySpots.OffPavement = CitySpots.Blocked = CitySpots.NearDoor = CitySpots.NoWall = CitySpots.Uneven = 0;
                    CityRefusal said = CityRefusal.None;
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        Vector3 place = origin + Quaternion.Euler(0f, (attempt % 5 - 2) * 22f, 0f) * ahead * (5f + (attempt / 5) * 4f);
                        _site.Refusal = CityRefusal.None;
                        bool opened = CitySpots.Pavement(place, 4f, out Vector3 anchor) && Open(entry, anchor, origin, Time.time, RainHelper.isRaining, true);
                        said = _site.Refusal != CityRefusal.None ? _site.Refusal : said;
                        if (opened)
                        {
                            _testNext[(int)family] = (_testNext[(int)family] + step + 1) % count;
                            Notifications.Show(NotificationType.Info, "reallife_city_test_started", new Dictionary<string, string> { { "what", entry.Name.GetLocalization() } }, 8f);
                            return;
                        }
                    }
                    // Why: what the activity itself said if it said anything, else whatever turned most places down.
                    if (said == CityRefusal.None)
                    {
                        int most = Mathf.Max(Mathf.Max(CitySpots.OffPavement, CitySpots.Blocked), Mathf.Max(CitySpots.NearDoor, Mathf.Max(CitySpots.NoWall, CitySpots.Uneven)));
                        said = most == 0 ? CityRefusal.OffPavement : most == CitySpots.NoWall ? CityRefusal.NoWall : most == CitySpots.Uneven ? CityRefusal.UnevenWall
                            : most == CitySpots.NearDoor ? CityRefusal.NearDoor : most == CitySpots.Blocked ? CityRefusal.Blocked : CityRefusal.OffPavement;
                    }
                    last = said;
                    lastName = entry.Name;
                    Logger?.Info($"City life test: {entry.Id} not started ({said}) in ten tries near the player: places refused as off the pavement {CitySpots.OffPavement}, "
                                 + $"blocked by something solid {CitySpots.Blocked}, near something the player can use {CitySpots.NearDoor}, no building wall within reach {CitySpots.NoWall}, "
                                 + $"walls seen but none flat with pavement in front {CitySpots.Uneven}.");
                }
                Notifications.Show(NotificationType.Warning, CityRefusals.Key(last), new Dictionary<string, string> { { "what", lastName != null ? lastName.GetLocalization() : string.Empty } }, 9f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Options-page test: clears the street of the living city's people.</summary>
        public void TestClear()
        {
            EndAll();
            Notifications.Show(NotificationType.Info, "reallife_city_test_cleared", null, 5f);
        }

        private static CityEntry Nth(CityFamily family, int index)
        {
            foreach (CityEntry entry in CityCatalogue.All)
            {
                if (entry.Family == family && index-- == 0)
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
