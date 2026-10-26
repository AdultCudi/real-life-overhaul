using System;
using System.Collections;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's tests of its leased offices, its row on the city map and the frame its buildings cost (Options, Test tools,
    /// and the city expansion's test run). Each works when pressed or says why not in a notice, and writes one
    /// "Uptown check (...)" line. Nothing here runs a frame unless the frame cost test is measuring.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const float CostSettle = 1f;   // s of real time after the buildings are hidden or shown before a window opens

        private PerfSampler _sampler;
        private static readonly List<MadeBuilding> Hidden = new List<MadeBuilding>(512);

        // ---------------------------------------------------------------- the city's builders' offices

        /// <summary>
        /// "Uptown: the city's builders' offices": each leased office's business there (its type, not to let), laid
        /// out, staffed and valued as the game's own office businesses are, on the map's row of its type and held off the
        /// market; the nearest made the game's destination.
        /// </summary>
        public static void TestOffices()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                int leases = 0, up = 0, sound = 0;
                string firstWhy = null;
                MadeBuilding nearest = null;
                float near = float.MaxValue;
                Vector3 at = MadeGame.PlayerAt();
                var names = new System.Text.StringBuilder(256);
                for (int i = 0; i < LeasedUp.Length; i++)
                {
                    DistrictLease lease = Lessee(i);
                    if (lease == null)
                    {
                        continue;
                    }
                    leases++;
                    MadeBuilding made = LeasedUp[i];
                    if (made == null)
                    {
                        firstWhy = firstWhy ?? lease.Name + " has no office standing (lot " + LeasedLot(i) + ")";
                        continue;
                    }
                    up++;
                    string why = LifeGame.OfficeWhy(made, lease);
                    if (why == null)
                    {
                        sound++;
                    }
                    else
                    {
                        firstWhy = firstWhy ?? lease.Name + " at " + made.Address + ": " + why;
                    }
                    names.Append(names.Length > 0 ? "; " : string.Empty).Append(lease.Name).Append(" at ").Append(made.Address);
                    float dx = made.DoorX - at.x, dz = made.DoorZ - at.z;
                    if (dx * dx + dz * dz < near)
                    {
                        near = dx * dx + dz * dz;
                        nearest = made;
                    }
                }
                bool pass = leases > 0 && sound == leases;
                string line = "Uptown check (builders' offices): " + up + " of " + leases + " leased offices with their businesses, " + sound
                              + " laid out, staffed and valued as the game's office businesses, on the map's row of their type and held off the market"
                              + (names.Length > 0 ? " (" + names + ")" : string.Empty) + (firstWhy != null ? "; first wrong: " + firstWhy : string.Empty) + ": " + (pass ? "PASS" : "FAIL");
                if (nearest != null)
                {
                    LifeGame.Destination(nearest);
                }
                if (pass)
                {
                    Logger?.Info(line);
                }
                else
                {
                    Logger?.Warn(line);
                }
                if (nearest != null)
                {
                    Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.OfficesDone, new Dictionary<string, string>
                    {
                        { "count", up.ToString(Plain) }, { "all", leases.ToString(Plain) }, { "address", nearest.Address }
                    }, 14f);
                }
                else
                {
                    Notifications.Show(NotificationType.Warning, LifeText.OfficesNone, null, 12f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- its row on the city map

        /// <summary>
        /// "Uptown: its row on the city map": its row among the game's district rows (under their category, after the game's
        /// own, named in the player's language), its zone in the game's zones for the row to show, and the row's data naming the
        /// district; ticking it shows the zone through the game's own filters, which only run with the map open.
        /// </summary>
        public static void TestRow()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                bool row = LifeGame.Rowed(), placed = LifeGame.RowPlaced(out string why), zone = LifeGame.ZoneListed(), data = LifeGame.RowNamesDistrict();
                bool pass = row && placed && zone && data;
                string line = "Uptown check (map row): its row in the map's filters " + Yes(row) + ", among the game's district rows " + Yes(placed)
                              + (why != null ? " (" + why + ")" : string.Empty) + ", named " + LifeGame.RowName() + ", its zone among the game's zones " + Yes(zone)
                              + ", the row's data naming the district " + Yes(data) + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(line);
                    Notifications.Show(NotificationType.Info, LifeText.RowDone, null, 12f);
                }
                else
                {
                    Logger?.Warn(line);
                    Notifications.Show(NotificationType.Warning, LifeText.RowWrong, null, 12f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the frame its buildings cost

        /// <summary>
        /// "Uptown: frame cost of its buildings": from where the player stands (the test run stands them at an open lot of the
        /// first block, among the district's buildings), the frame measured over a window with the district's buildings as
        /// they are, then over one with each of their looks hidden (the copy engine's own way: their doors and solids stay),
        /// then shown again; what they add to the frame, and for each building in view, against the copy engine's allowance for
        /// a copy in view (ten under half a millisecond).
        /// </summary>
        public static void TestCost()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                if (LifeGame.Indoors)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                    return;
                }
                if (Instance == null || !Instance.isActiveAndEnabled)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                    return;
                }
                _testing = true;
                Instance.StartCoroutine(Guard.Routine(Instance.Cost()));
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        private IEnumerator Cost()
        {
            try
            {
                Notifications.Show(NotificationType.Info, LifeText.CostStart, null, 8f);
                int inView = 0, renderers = 0;
                for (int i = 0; i < Standing.Count; i++)
                {
                    if (LifeGame.InView(Standing[i], out int drawn))
                    {
                        inView++;
                        renderers += drawn;
                    }
                }
                _sampler = _sampler != null ? _sampler : gameObject.AddComponent<PerfSampler>();
                float until = Time.unscaledTime + CostSettle;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                _sampler.Open();
                until = Time.unscaledTime + NewBuildingsRules.CostWindow;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                PerfWindow with = _sampler.Close();
                Hidden.Clear();
                for (int i = 0; i < Standing.Count; i++)
                {
                    if (MadeBuildings.HideLook(Standing[i], true))
                    {
                        Hidden.Add(Standing[i]);
                    }
                }
                until = Time.unscaledTime + CostSettle;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                _sampler.Open();
                until = Time.unscaledTime + NewBuildingsRules.CostWindow;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                PerfWindow without = _sampler.Close();
                ShowHidden();
                float added = with.AverageMs - without.AverageMs;
                float each = inView > 0 ? added / inView : added;
                float allowed = NewBuildingsRules.MostAddedMs / NewBuildingsRules.CostCopies;
                bool pass = inView > 0 && each <= allowed;
                string line = "Uptown check (frame cost): " + inView + " of the district's " + Standing.Count + " buildings in view (" + renderers + " of their parts drawn): "
                              + "the frame took " + with.AverageMs.ToString("F2", Plain) + " ms with them against " + without.AverageMs.ToString("F2", Plain) + " ms with their looks hidden "
                              + "(main thread " + Ms(with.MainMs) + " against " + Ms(without.MainMs) + ", graphics card " + Ms(with.GpuMs) + " against " + Ms(without.GpuMs) + "; "
                              + with.Frames + " and " + without.Frames + " frames), " + added.ToString("+0.00;-0.00", Plain) + " ms, " + each.ToString("0.000", Plain)
                              + " ms a building in view (wanted at most " + allowed.ToString("0.000", Plain) + " ms, the copy engine's allowance for a copy in view)"
                              + (inView == 0 ? "; no building of the district was in view" : string.Empty) + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(line);
                }
                else
                {
                    Logger?.Warn(line);
                }
                if (inView > 0)
                {
                    Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.CostDone, new Dictionary<string, string>
                    {
                        { "count", inView.ToString(Plain) }, { "ms", added.ToString("0.00", Plain) }
                    }, 12f);
                }
                else
                {
                    Notifications.Show(NotificationType.Warning, LifeText.CostNone, null, 10f);
                }
            }
            finally
            {
                ShowHidden();
                if (_sampler != null && _sampler.IsOpen)
                {
                    _sampler.Close();
                }
                _testing = false;
            }
        }

        // Every look the frame cost test hid shown again: on its end, a fault, or the city going.
        private static void ShowHidden()
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                MadeBuildings.HideLook(Hidden[i], false);
            }
            Hidden.Clear();
        }

        private static string Ms(float ms) => ms >= 0f ? ms.ToString("F2", Plain) + " ms" : "not read";
    }
}
