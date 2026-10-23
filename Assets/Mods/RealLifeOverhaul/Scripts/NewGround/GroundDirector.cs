using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyUrbanAssets;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// New ground's one component: the developer's tests of the walking core, the roads and the parking, and everything
    /// each lends until it is given back: a test patch of walking mesh with its links and a pedestrian zone, the city's own
    /// walking meshes grown by a tile, a copied parking lane, and the test road appended at a load (which stays until the
    /// next, as the traffic is never started again). Given back when its minute of play is up, indoors, at a load and when
    /// the city goes. Nothing is saved or sent. COST: disabled while nothing of its own runs; then four looks a second.
    /// </summary>
    public sealed partial class GroundDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("newground", "GroundDirector", typeof(GroundDirector));
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const float Look = 0.25f;            // s of real time between two looks while something runs
        private const string NoticeId = "RealLifeOverhaul new ground";   // one notice at a time: a new one replaces it
        private const float NoticeSeconds = 10f;     // s

        public static IModLogger Logger;
        public static GroundDirector Instance { get; private set; }

        private float _nextLook;
        private float _lastLook;

        // A grow of one city mesh by a tile (the grow test), from its pavement point to the new tile beside.
        private sealed class GrowRun
        {
            public CityMesh Mesh;
            public GroundJob Job;
            public Vector3 From;           // on the old mesh: where the new ground begins
            public Vector3 Far;            // on the new tile
            public GroundBox Strip;
            public string Alone;           // the path before the old tile was rebuilt
            public string Joined;          // ...and after
            public int Phase;              // 0 the new tile, 1 the old tile, 2 kept, 3 giving back, 4 done
        }

        private GrowRun _growPlayer;
        private GrowRun _growNpc;
        private float _growLeft;           // s of play the grown tiles stay

        private void Awake()
        {
            Instance = this;
            enabled = false;
            _trafficStarted = Guard.Wrap(TrafficStarted);
            ArmRoad();   // the test road is appended now, in the city's load, before the traffic starts
        }

        private void OnDestroy()
        {
            Leave("the city is going");
            WalkGame.Forget();
            PeopleGame.Forget();
            ParkingGame.Forget();
            RoadsGame.Forget();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("NewGround: GroundDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                Leave("its guard stopped it");
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _nextLook)
            {
                return;
            }
            float since = Mathf.Min(now - _lastLook, 1f);
            _lastLook = now;
            _nextLook = now + Look;
            float played = Time.timeScale > 0f ? since : 0f;   // a test's minute is a minute of play, not of a menu
            if ((BuildingManager.IsInsideBuilding || UI.Load.LoadScene.isLoading) && Lending)
            {
                GiveBackAll(BuildingManager.IsInsideBuilding ? "the player went indoors" : "a load began");
            }
            bool busy = TickPatch(played);
            busy |= TickGrow(played);
            busy |= TickLane(played);
            busy |= TickRoad();
            if (!busy)
            {
                enabled = false;
            }
        }

        // Something of the city's or the game's is lent out now.
        private bool Lending => _hasPatch || _growPlayer != null || _growNpc != null || _lane != null;

        // Starts the looks for something just begun.
        private void Wake()
        {
            _lastLook = Time.unscaledTime;
            _nextLook = 0f;
            enabled = true;
        }

        /// <summary>Gives back everything the tests have lent; what is given back in the background finishes on the looks.</summary>
        public void GiveBackAll(string why)
        {
            if (!Lending)
            {
                return;
            }
            Logger?.Info("New ground: the test ground is given back (" + why + ").");
            EndPatch();
            EndLane();
            BackGrow(_growPlayer);
            BackGrow(_growNpc);
            Wake();
        }

        // The city is going: what is the world's (the patch, its links, the zone, the lane) is taken away now; the city's
        // own walking data goes with its scene, so a grow is only let go.
        private void Leave(string why)
        {
            if (_hasPatch || _lane != null)
            {
                Logger?.Info("New ground: the test ground is taken away (" + why + ").");
            }
            EndPatch();
            EndLane();
            if (_growPlayer != null)
            {
                GroundBuilder.Free(_growPlayer.Job);
                _growPlayer = null;
            }
            if (_growNpc != null)
            {
                GroundBuilder.Free(_growNpc.Job);
                _growNpc = null;
            }
        }

        // ---- the grow, a look at a time

        private bool TickGrow(float played)
        {
            if (_growPlayer == null && _growNpc == null)
            {
                return false;
            }
            bool kept = (_growPlayer == null || _growPlayer.Phase == 2) && (_growNpc == null || _growNpc.Phase == 2);
            if (kept)
            {
                _growLeft -= played;
                if (_growLeft <= 0f)
                {
                    BackGrow(_growPlayer);
                    BackGrow(_growNpc);
                }
            }
            StepGrow(ref _growPlayer);
            StepGrow(ref _growNpc);
            return _growPlayer != null || _growNpc != null;
        }

        private void StepGrow(ref GrowRun run)
        {
            if (run == null || run.Phase == 2)
            {
                return;
            }
            GroundJob job = run.Job;
            if (!GroundBuilder.Step(job))
            {
                return;
            }
            switch (run.Phase)
            {
                case 0:
                    run.Alone = PathOnto(run);
                    if (job.Problem != null)
                    {
                        Logger?.Info("Walk check (grow one tile, " + run.Mesh.Label + "): FAIL, " + job.Problem);
                        BackGrow(run);
                        return;
                    }
                    GroundBuilder.Begin(job, GroundPass.Join);
                    run.Phase = 1;
                    return;
                case 1:
                    run.Joined = job.Problem == null ? PathOnto(run) : "INVALID";
                    int alike = GroundBuilder.RoundAlike(job);
                    if (Meta.Dev.Verbose)
                    {
                        string from = job.Joining.Count > 0 ? "the old mesh's own " + job.OldTriangles + " triangles" : "nothing (it had no old mesh)";
                        Logger?.Info(GroundRules.GrowLine(run.Mesh.Label, run.Alone, run.Joined, from, alike, job.RoundBefore.Length / 3)
                                     + (job.Problem != null ? " (" + job.Problem + ")" : string.Empty) + "; " + job.Fresh.Count + " new tile(s), "
                                     + job.Joining.Count + " rebuilt, in " + job.Ms.ToString("0", Inv) + " ms of building");
                    }
                    if (job.Problem != null || alike * 3 != job.RoundBefore.Length)
                    {
                        BackGrow(run);   // something else moved: given back at once
                        return;
                    }
                    run.Phase = 2;
                    if ((_growPlayer == null || _growPlayer.Phase >= 2) && (_growNpc == null || _growNpc.Phase >= 2))
                    {
                        Notice(NewGroundText.Grow, new Dictionary<string, string>(2)
                        {
                            { "player", Said(_growPlayer, 0) },
                            { "people", Said(_growNpc, 1) }
                        });
                    }
                    return;
                case 3:
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info(GroundRules.BackLine(run.Mesh.Label, GroundBuilder.CoveredAlike(job), job.CoveredBefore.Length / 3,
                            GroundBuilder.RoundAlike(job), job.RoundBefore.Length / 3));
                    }
                    GroundBuilder.Free(job);
                    run.Phase = 4;
                    run = null;
                    return;
            }
        }

        // What a mesh's grow came to, for the notice: its path onto the new tile, or that it was not grown.
        private string Said(GrowRun run, int slot)
        {
            return run != null ? run.Joined ?? "-" : _skipped[slot] != null ? "not grown" : "-";
        }

        // The way from the pavement onto the new tile, its point found on the mesh as it is now.
        private static string PathOnto(GrowRun run)
        {
            if (!WalkGame.Sample(run.Mesh, run.Far, 1.5f, out Vector3 onto))
            {
                return "PARTIAL (no walking mesh on the new tile)";
            }
            return WalkGame.Path(run.Mesh, run.From, onto);
        }

        // Starts giving a grown mesh back: the player is first put back on the pavement if standing on the new ground.
        private void BackGrow(GrowRun run)
        {
            if (run == null || run.Phase >= 3)
            {
                return;
            }
            if (run.Strip.Covers(WalkGame.At.x, WalkGame.At.z, 1f) && WalkGame.OnFoot())
            {
                WalkGame.Warp(run.From);
                Notice(NewGroundText.Back, null);
            }
            WalkGame.ReleaseWalkersOn(run.Strip);
            GroundBuilder.Begin(run.Job, GroundPass.Back);
            run.Phase = 3;
        }

        // ---- shared

        private static void Notice(string key, Dictionary<string, string> data)
        {
            Notifications.Show(NotificationType.Info, key, data, NoticeSeconds, NoticeId, null, notificationSound: false, trackOnSaveGame: false);
        }

        private static string Metres(float value)
        {
            return value.ToString("0", Inv);
        }
    }
}
