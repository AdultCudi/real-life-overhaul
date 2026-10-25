using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The test of Show on map (Options > Tests; also in the city expansion's test run): the player's nearest project with a
    /// place, else a lot of theirs, else the nearest lot for sale, made the game's destination and the guider looked at. It
    /// writes one "Construction check (show on map)" line ending PASS or FAIL, and a notice, and is a public static method,
    /// so a runner can call it with no press. Nothing of the state is changed; the player's destination is moved.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string TrackCheck = "Construction check (";   // each line goes on with its test's name and ")"
        private const float BuildingNear = 40f;    // m on the ground: a building's marker stands over its roof, near its door
        private const float SpotNear = 1f;         // m on the ground: a spot's marker stands where it was put

        /// <summary>Test: the player's nearest project with a place (else a lot of theirs, else the nearest lot for sale) shown on the map.</summary>
        public static bool TestMapNow()
        {
            return TrackTest("show on map", d => d.MapTest("show on map"));
        }

        private static bool TrackTest(string test, Func<ConstructionDirector, bool> work)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, TrackCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(TrackCheck + test + "): the construction game has not read this save yet: FAIL.");
                return false;
            }
            if (!d.Running)
            {
                TestRefusals.Tell(Logger, TrackCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(TrackCheck + test + "): the construction game is off in the options: FAIL.");
                return false;
            }
            try
            {
                return work(d);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool MapTest(string test)
        {
            Vector3 player = ConstructionGame.Player;
            ProjectFile project = null;
            string lotId = null;
            MapPlace place = default(MapPlace), best = default(MapPlace);
            float near = float.MaxValue;
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage != ProjectStage.Abandoned && PlaceOf(p, out place) && Flat(Where(place), player) < near)
                {
                    near = Flat(Where(place), player);
                    project = p;
                    best = place;
                }
            }
            for (int i = 0; project == null && i < _state.Lots.Count; i++)
            {
                if (PlaceOfLot(_state.Lots[i].Id, out place))
                {
                    lotId = _state.Lots[i].Id;
                    best = place;
                    break;
                }
            }
            if (project == null && lotId == null)
            {
                lotId = NearestOpenLot(player);
                if (lotId == null || !PlaceOfLot(lotId, out best))
                {
                    lotId = null;
                }
            }
            if (project == null && lotId == null)
            {
                return Failed(TrackCheck, test, "reallife_bld_test_map_none", "no project or lot of the player's has a place, and no lot is for sale at this load");
            }
            string what = project != null ? "project " + project.Id + " (" + PlaceWords(project) + ")" : "lot " + lotId;
            string why = project != null ? ShowProject(project) : ShowLot(lotId);
            bool set = why == null && ConstructionGame.DestinationAt(out Vector3 at) && Flat(at, Where(best)) <= (best.Building ? BuildingNear : SpotNear);
            ConstructionGame.DestinationAt(out Vector3 marked);
            Logger?.Warn(TrackCheck + test + "): " + what + " at " + ConstructionGame.AddressText(best.Street, best.Number) + ", " + (best.Building ? "a standing building, by its address"
                : "no building, by its front on its street") + "; the game's destination " + (why != null ? "refused (" + why + ")" : "marks a point " + Flat(marked, Where(best)).ToString("0.0")
                + " m from it") + (set ? ": PASS." : ": FAIL."));
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 6f);
            }
            return set;
        }

        // The nearest lot for sale now, by its front; null for none.
        private string NearestOpenLot(Vector3 player)
        {
            IReadOnlyList<LotView> lots = Lots;
            string best = null;
            float near = float.MaxValue;
            for (int i = 0; i < lots.Count; i++)
            {
                LotView lot = lots[i];
                float d = lot.Open && !LotTaken(lot.Id) ? Flat(LotFront(lot), player) : float.MaxValue;
                if (d < near)
                {
                    near = d;
                    best = lot.Id;
                }
            }
            return best;
        }

        // Where a place's marker should stand: a building's door, else the spot.
        private static Vector3 Where(MapPlace place)
        {
            return place.Building && ConstructionGame.Door(place.Street, place.Number, out Vector3 door, out _) ? door : place.Spot;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
