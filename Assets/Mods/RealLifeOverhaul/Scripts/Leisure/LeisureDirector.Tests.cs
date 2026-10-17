using System;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>The options page's test buttons: each plays what it names or says in one sentence why not.</summary>
    public sealed partial class LeisureDirector
    {
        public void TestStart(LeisureKind kind)
        {
            if (!ModState.Loaded || !LeisureState.Loaded || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, "Leisure " + kind, TestRefusal.NotLoaded);
                return;
            }
            if (_broken || !Enabled(kind))
            {
                TestRefusals.Tell(Logger, "Leisure " + kind, TestRefusal.FeatureOff);
                return;
            }
            Vector3 position = PlayerHelper.PlayerController.transform.position;
            if (kind == LeisureKind.Chess && _activity == null && (!_corner.Out || CitySpots.Flat(_corner.Stool - position) > 4f))
            {
                // No chess player here: a test may set one up just ahead, in view, for the player to walk up to.
                TestRefusal street = TestRefusals.Street();
                if (street != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Leisure Chess", street);
                    return;
                }
                _corner.TakeIn();
                Vector3 ahead = position + PlayerHelper.PlayerController.transform.forward * 3.5f;
                if (CitySpots.Pavement(ahead, 5f, out Vector3 anchor) && !_corner.PutOut(anchor, position, out TestRefusal why))
                {
                    TestRefusals.Tell(Logger, "Leisure Chess", why, 0, "table at " + anchor.ToString("F0"));
                    return;
                }
                if (!_corner.Out)
                {
                    TestRefusals.Tell(Logger, "Leisure Chess", TestRefusal.NothingFits, 0, "no pavement within 5 m ahead");
                    return;
                }
                Notifications.Show(NotificationType.Info, "reallife_leisure_chess_set_up", null, 8f);
                return;
            }
            if (kind == LeisureKind.Rack)
            {
                if (!ClothesScreen.OpenShop(out string why))
                {
                    TestRefusals.TellKey(Logger, "Leisure " + kind, why);
                }
                return;
            }
            if (!StartActivity(kind, null, true, out string refusal))
            {
                TestRefusals.TellKey(Logger, "Leisure " + kind, refusal);
            }
        }

        public void TestWardrobe()
        {
            if (!ModState.Loaded || !LeisureState.Loaded || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, "Leisure wardrobe", TestRefusal.NotLoaded);
                return;
            }
            if (_broken || !Settings.Leisure || !Settings.ClothesShops)
            {
                TestRefusals.Tell(Logger, "Leisure wardrobe", TestRefusal.FeatureOff);
                return;
            }
            if (!ClothesScreen.OpenWardrobe(out string why))
            {
                TestRefusals.TellKey(Logger, "Leisure wardrobe", why);
            }
        }

        /// <summary>Fishing without the walk: the player is put at the nearest water's edge, facing it.</summary>
        public void TestFishNearest()
        {
            const string test = "Fishing at the nearest water";
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (!ModState.Loaded || !LeisureState.Loaded || player == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                    return;
                }
                if (_broken || !Enabled(LeisureKind.Fishing))
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                    return;
                }
                if (_activity != null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.SceneActive);
                    return;
                }
                if (BuildingManager.IsInsideBuilding)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.Indoors);
                    return;
                }
                if (PlayerHelper.IsUsingVehicle)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.InVehicle);
                    return;
                }
                if (!Free(LeisureKind.Fishing, out string busy))
                {
                    TestRefusals.TellKey(Logger, test, busy);
                    return;
                }
                Vector3 position = player.transform.position;
                LeisureVenues.FindLandmarks();
                bool here = LeisureVenues.WatersideDistance(position) < 320f
                            && LeisureVenues.WaterAhead(position, player.transform.forward, out _, out _);
                if (!here)
                {
                    UnityEngine.AI.NavMeshAgent feet = player.Character != null ? player.Character.navmeshAgent : null;
                    if (feet == null || !feet.isOnNavMesh)
                    {
                        TestRefusals.Tell(Logger, test, TestRefusal.NoPavement);
                        return;
                    }
                    if (!LeisureVenues.WaterEdgeNear(position, out Vector3 stand, out Vector3 facing, out float metres))
                    {
                        TestRefusals.Tell(Logger, test, TestRefusal.NothingFits, 0, "no waterside landmark has a walkable edge with open water in front of it");
                        return;
                    }
                    player.ResetNavigation();
                    if (!feet.Warp(stand))
                    {
                        TestRefusals.Tell(Logger, test, TestRefusal.NoRoom, 0, "the player could not be put at " + stand.ToString("F0"));
                        return;
                    }
                    player.transform.rotation = Quaternion.LookRotation(facing);
                    Logger?.Info("Leisure test: the player is put at the water's edge " + metres.ToString("F0") + " m away, at " + stand.ToString("F0")
                                 + ", facing the water.");
                }
                if (!StartActivity(LeisureKind.Fishing, null, true, out string refusal))
                {
                    TestRefusals.TellKey(Logger, test, refusal);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public void TestNight()
        {
            if (PlayerHelper.PlayerController == null || TestRefusals.Street() != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Night life", PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded : TestRefusals.Street());
                return;
            }
            _night.Test(PlayerHelper.PlayerController.transform.position, Time.unscaledTime);
        }

        public void ShowRecords()
        {
            if (!LeisureState.Loaded)
            {
                TestRefusals.Tell(Logger, "Leisure records", TestRefusal.NotLoaded);
                return;
            }
            LeisureVenues.FindLandmarks();
            string fishing = (LeisureState.BestOf("fishing") / 1000f).ToString("0.0", CultureInfo.InvariantCulture);
            string body = JobText.T("reallife_leisure_records_chess", "best", LeisureState.BestOf("chess").ToString(), "plays", LeisureState.PlaysOf("chess").ToString())
                          + "\n" + JobText.T("reallife_leisure_records_fishing", "best", fishing, "kinds", LeisureState.FishKinds.ToString())
                          + "\n" + JobText.T("reallife_leisure_records_dance", "best", LeisureState.BestOf("dance").ToString(), "plays", LeisureState.PlaysOf("dance").ToString())
                          + "\n" + JobText.T("reallife_leisure_records_sights", "seen", LeisureState.SeenCount.ToString(), "all", LeisureVenues.Landmarks.Count.ToString())
                          + "\n" + JobText.T("reallife_leisure_records_films", "films", LeisureState.PlaysOf("cinema").ToString(), "clothes", LeisureState.OwnedCount.ToString())
                          + "\n" + JobText.T("reallife_leisure_records_spent", "amount", JobText.Money(LeisureState.Spent));
            for (int i = 0; i < LeisureVenues.Landmarks.Count; i++)
            {
                if (!LeisureState.HasSeen(LeisureVenues.Landmarks[i].Id))
                {
                    body += "\n" + JobText.T("reallife_leisure_records_next", "place", JobText.T(LeisureVenues.Landmarks[i].NameKey));
                    break;
                }
            }
            LeisureScreen.Card(JobText.T("reallife_leisure_records_title"), body, 16f);
        }
    }
}
