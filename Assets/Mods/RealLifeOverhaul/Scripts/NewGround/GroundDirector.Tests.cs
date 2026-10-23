using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// New ground's tests under Options > Tests: each works when pressed or says why not in a notice, writes one
    /// "... check (...)" line per event with Developer diagnostics on, and gives back what it lent (the walking tests
    /// after a minute of play, indoors, at a load and when the city goes).
    /// </summary>
    public sealed partial class GroundDirector
    {
        private static readonly float[] RingX = new float[GroundRules.RingPoints];
        private static readonly float[] RingZ = new float[GroundRules.RingPoints];

        public static void TestUnder()
        {
            if (!Ready("Walk: what is under me", false))
            {
                return;
            }
            Instance.Under();
        }

        public static void TestPatch()
        {
            if (!Ready("Walk: build a test patch", true))
            {
                return;
            }
            if (Instance.Growing)
            {
                TestRefusals.TellKey(Logger, "Walk: build a test patch", NewGroundText.Busy);
                return;
            }
            if (!Instance.LayPatch(out string refusal, out string detail))
            {
                TestRefusals.TellKey(Logger, "Walk: build a test patch", refusal, detail);
            }
        }

        public static void TestGrow()
        {
            if (!Ready("Walk: grow the city's own mesh by one tile", true))
            {
                return;
            }
            if (Instance.Growing)
            {
                TestRefusals.TellKey(Logger, "Walk: grow the city's own mesh by one tile", NewGroundText.Busy);
                return;
            }
            Instance.Grow();
        }

        public static void TestLink()
        {
            if (!Ready("Walk: link the patch", true))
            {
                return;
            }
            if (Instance.Growing)
            {
                TestRefusals.TellKey(Logger, "Walk: link the patch", NewGroundText.Busy);
                return;
            }
            if (!Instance.LinkPatch(out string refusal, out string detail))
            {
                TestRefusals.TellKey(Logger, "Walk: link the patch", refusal, detail);
            }
        }

        public static void TestCensus()
        {
            if (!Ready("Roads: census", false))
            {
                return;
            }
            Instance.Census();
        }

        public static void TestParking()
        {
            if (!Ready("Parking: copy a lane", false))
            {
                return;
            }
            Instance.CopyLane();
        }

        // The save is loaded, the player out of doors (and on foot, for a walking test that lays ground beside them).
        private static bool Ready(string test, bool onFoot)
        {
            if (FeatureGuards.FeatureAway("newground"))
            {
                TestRefusals.TellKey(Logger, test, NewGroundText.OtherCity);   // the director is not started in a city the mod does not know
                return false;
            }
            if (Instance == null || !ModState.Loaded || PlayerHelper.PlayerController == null || UI.Load.LoadScene.isLoading)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.Indoors);
                return false;
            }
            if (onFoot && VehicleHelper.IsInsideVehicle())
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotOnFoot);
                return false;
            }
            return true;
        }

        // A grow's passes are still under way, or being given back.
        private bool Growing => (_growPlayer != null && _growPlayer.Phase != 2) || (_growNpc != null && _growNpc.Phase != 2);

        // "What is under me": both walking meshes sampled at the player and at 8 points 10 m round.
        private void Under()
        {
            CityMesh player = WalkGame.Player, npc = WalkGame.Npc;
            if (player == null || npc == null)
            {
                TestRefusals.TellKey(Logger, "Walk: what is under me", NewGroundText.NoMeshes, WalkGame.Missing);
                return;
            }
            Vector3 at = WalkGame.At;
            GroundRules.Ring(at.x, at.z, RingX, RingZ);
            int onPlayer = 0, onNpc = 0;
            for (int i = 0; i < GroundRules.RingPoints; i++)
            {
                WalkGame.Ground(RingX[i], RingZ[i], at.y, out float y);
                var point = new Vector3(RingX[i], y, RingZ[i]);
                onPlayer += WalkGame.Sample(player, point, GroundRules.SampleReach, out _) ? 1 : 0;
                onNpc += WalkGame.Sample(npc, point, GroundRules.SampleReach, out _) ? 1 : 0;
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(GroundRules.UnderLine(player.Label, onPlayer, npc.Label, onNpc, at.x, at.z) + " (meshes " + player.Name + ", agent " + player.Agent
                             + ", and " + npc.Name + ", agent " + npc.Agent + ")");
            }
            Notice(NewGroundText.Under, new Dictionary<string, string>(2) { { "player", onPlayer.ToString(Inv) }, { "people", onNpc.ToString(Inv) } });
        }

        // "Grow the city's own mesh by one tile": for each walking mesh, a strip from the pavement into the empty tile beside.
        private void Grow()
        {
            CityMesh player = WalkGame.Player, npc = WalkGame.Npc;
            if (player == null || npc == null)
            {
                TestRefusals.TellKey(Logger, "Walk: grow the city's own mesh by one tile", NewGroundText.NoMeshes, WalkGame.Missing);
                return;
            }
            GiveBackAll("a new grow test");
            EndPatch();   // a patch of its own would be read as the old mesh
            if (Growing)
            {
                TestRefusals.TellKey(Logger, "Walk: grow the city's own mesh by one tile", NewGroundText.Busy);
                return;
            }
            _growPlayer = Start(player, out string playerWhy);
            _growNpc = Start(npc, out string npcWhy);
            if (_growPlayer == null && _growNpc == null)
            {
                TestRefusals.TellKey(Logger, "Walk: grow the city's own mesh by one tile", playerWhy == NewGroundText.Builder || npcWhy == NewGroundText.Builder
                    ? NewGroundText.Builder : NewGroundText.NoTile, "Player: " + _skipped[0] + "; NPC: " + _skipped[1]);
                return;
            }
            _growLeft = GroundRules.KeepSeconds;
            Wake();
        }

        private readonly string[] _skipped = new string[2];
        private static readonly List<float> TileX = new List<float>(GroundRules.TileSamples * GroundRules.TileSamples);
        private static readonly List<float> TileZ = new List<float>(GroundRules.TileSamples * GroundRules.TileSamples);
        private static readonly int[] Headings = { 0, 90, 180, 270 };

        // One mesh's grow begun, or null with why (in _skipped) and the refusal's key.
        private GrowRun Start(CityMesh mesh, out string refusal)
        {
            int slot = mesh == WalkGame.Player ? 0 : 1;
            refusal = NewGroundText.NoTile;
            _skipped[slot] = null;
            Vector3 at = WalkGame.At;
            if (!WalkGame.Sample(mesh, at, 2f, out Vector3 from))
            {
                _skipped[slot] = "you are not on the " + mesh.Label + " walking mesh";
                return null;
            }
            GroundBox chosen = default;
            float nearest = float.MaxValue;
            Vector3 far = from;
            for (int h = 0; h < Headings.Length; h++)
            {
                if (!GroundRules.Strip(from.x, from.z, from.y, Headings[h], mesh.Grid, out GroundBox strip, out float toBorder) || toBorder >= nearest)
                {
                    continue;
                }
                long beside = GroundRules.Beside(from.x, from.z, Headings[h], mesh.Grid);
                TileX.Clear();
                TileZ.Clear();
                GroundRules.Spread(beside, mesh.Grid, GroundRules.TileSamples, TileX, TileZ);
                if (WalkGame.Has(WalkGame.Print(mesh, TileX, TileZ, from.y, GroundRules.SpreadReach(mesh.Grid, GroundRules.TileSamples)), beside, mesh.Grid))
                {
                    continue;   // the tile beside has this walking mesh already
                }
                if (!WalkGame.Clear(strip, out _))
                {
                    continue;
                }
                float into = toBorder + GroundRules.StripInto * 0.5f;
                float angle = Headings[h] * Mathf.Deg2Rad;
                var end = new Vector3(from.x + Mathf.Sin(angle) * into, from.y, from.z + Mathf.Cos(angle) * into);
                if (!WalkGame.Ground(end.x, end.z, from.y, out float ground) || Mathf.Abs(ground - from.y) > GroundRules.StepUp + GroundRules.FlatEnough)
                {
                    continue;   // no ground there: the new tile would be walking on air
                }
                chosen = strip;
                nearest = toBorder;
                far = end;
            }
            if (nearest == float.MaxValue)
            {
                _skipped[slot] = "no empty tile of the " + mesh.Label + " mesh beside you with clear ground into it";
                return null;
            }
            string dry = GroundBuilder.DryRun(mesh, from, out double dryMs, out float flat);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(GroundRules.DryLine(mesh.Label, dry, flat, dryMs));
            }
            if (dry != null)
            {
                refusal = NewGroundText.Builder;
                _skipped[slot] = dry;
                return null;
            }
            GroundJob job = GroundBuilder.Plan(mesh, new List<GroundBox>(1) { chosen }, from.y, flat);
            if (job.Problem != null)
            {
                refusal = NewGroundText.Builder;
                _skipped[slot] = job.Problem;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Walk check (grow one tile, " + mesh.Label + "): FAIL, " + job.Problem);
                }
                GroundBuilder.Free(job);
                return null;
            }
            GroundBuilder.Begin(job, GroundPass.Fresh);
            WalkGame.Sample(mesh, far, 2f, out Vector3 farOnGround);
            return new GrowRun { Mesh = mesh, Job = job, From = from, Far = farOnGround, Strip = chosen, Phase = 0 };
        }
    }
}
