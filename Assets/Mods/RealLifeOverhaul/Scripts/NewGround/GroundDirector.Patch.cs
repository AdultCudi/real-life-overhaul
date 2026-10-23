using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// The test patch: a square of walking mesh of its own beside the player for both agent types (the fallback's
    /// separate data), its two-way links from the pavement, and a pedestrian zone of the game's own copied onto it.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const float ZoneSeconds = 20f;   // s of play the pedestrian zone on the patch is watched before its line

        private bool _hasPatch;
        private GroundBox _patch;
        private Vector3 _patchFrom;              // where the player stood: the pavement the links start on
        private Vector3 _patchMiddle;
        private NavMeshData _playerPatch;
        private NavMeshData _npcPatch;
        private NavMeshDataInstance _playerPatchOn;
        private NavMeshDataInstance _npcPatchOn;
        private NavMeshLinkInstance _playerLink;
        private NavMeshLinkInstance _npcLink;
        private StationaryAiSpawner _zone;
        private float _patchLeft;                // s of play the patch stays
        private float _zoneWatch;                // s of play the zone has been watched
        private int _zoneMost;
        private bool _zoneTold;

        private static readonly float[] PatchX = new float[9];
        private static readonly float[] PatchZ = new float[9];

        private bool TickPatch(float played)
        {
            if (!_hasPatch)
            {
                return false;
            }
            _patchLeft -= played;
            if (_zone != null && !_zoneTold)
            {
                _zoneWatch += played;
                _zoneMost = Mathf.Max(_zoneMost, PeopleGame.StandingOn(_patchMiddle, _patch.Yaw, new Vector3(_patch.HalfX * 2f, 2f, _patch.HalfZ * 2f)));
                if (_zoneWatch >= ZoneSeconds)
                {
                    _zoneTold = true;
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info(GroundRules.PeopleLine(true, _zoneMost, _zoneWatch));
                    }
                }
            }
            if (_patchLeft <= 0f)
            {
                EndPatch();
                Notice(NewGroundText.Back, null);
            }
            return _hasPatch;
        }

        /// <summary>
        /// Lays the patch: the clear, level square beside the player with the least of the city's walking mesh under it,
        /// built for both agent types on the city meshes' own grids and settings, timed. False with the refusal's key.
        /// </summary>
        private bool LayPatch(out string refusal, out string detail)
        {
            refusal = null;
            detail = null;
            CityMesh player = WalkGame.Player, npc = WalkGame.Npc;
            if (player == null || npc == null)
            {
                refusal = NewGroundText.NoMeshes;
                detail = WalkGame.Missing;
                return false;
            }
            Vector3 at = WalkGame.At;
            int least = int.MaxValue;
            GroundBox best = default;
            string blocked = "no level ground found round you";
            for (int i = 0; i < 8; i++)
            {
                float heading = GroundRules.Heading(i);
                GroundBox probe = GroundRules.Patch(at.x, at.z, at.y, heading);
                if (!Level(probe, at.y, out float top))
                {
                    continue;
                }
                GroundBox box = GroundRules.Patch(at.x, at.z, top + 0.02f, heading);
                if (!WalkGame.Clear(box, out string blocker))
                {
                    blocked = blocker;
                    continue;
                }
                int under = Under(player, box) + Under(npc, box);
                if (under < least)
                {
                    least = under;
                    best = box;
                }
            }
            if (least == int.MaxValue)
            {
                refusal = NewGroundText.NoRoom;
                detail = blocked;
                return false;
            }
            EndPatch();
            _patch = best;
            _patchFrom = at;
            _patch.World(0f, 0f, out float mx, out float mz);
            _patchMiddle = new Vector3(mx, best.Top, mz);
            int playerTiles = Lay(player, best, out _playerPatch, out _playerPatchOn, out double playerMs);
            int npcTiles = Lay(npc, best, out _npcPatch, out _npcPatchOn, out double npcMs);
            _hasPatch = true;
            _patchLeft = GroundRules.KeepSeconds;
            bool sampled = OnPatch(player) && OnPatch(npc);
            string path = PathTo(player, _patchMiddle);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(GroundRules.BuildLine(player.Label, playerTiles, playerMs, npc.Label, npcTiles, npcMs, sampled, path + " (pedestrians "
                                                   + PathTo(npc, _patchMiddle) + ")") + "; the city's mesh under it at " + least + " of 18 points");
                WalkGame.CountSources(player, at, out int meshes, out int readable, out int batched, out int other);
                Logger?.Info(GroundRules.SourcesLine(meshes, readable, batched, other, WalkGame.SourcesReach));
            }
            Notice(NewGroundText.Patch, new Dictionary<string, string>(5)
            {
                { "ptiles", playerTiles.ToString(Inv) },
                { "pms", playerMs.ToString("0", Inv) },
                { "ntiles", npcTiles.ToString(Inv) },
                { "nms", npcMs.ToString("0", Inv) },
                { "path", path }
            });
            Wake();
            return true;
        }

        // Ground under a patch's middle and corners, near enough level: the height of its top.
        private static bool Level(GroundBox box, float near, out float top)
        {
            box.World(0f, 0f, out float x, out float z);
            if (!WalkGame.Ground(x, z, near, out top))
            {
                return false;
            }
            for (int c = 0; c < 4; c++)
            {
                box.World((c & 1) == 0 ? -box.HalfX : box.HalfX, (c & 2) == 0 ? -box.HalfZ : box.HalfZ, out float cx, out float cz);
                if (!WalkGame.Ground(cx, cz, near, out float y) || Mathf.Abs(y - top) > GroundRules.FlatEnough)
                {
                    return false;
                }
                top = Mathf.Max(top, y);
            }
            return true;
        }

        // How many of nine points over a box already have this mesh: the patch is laid where there is least.
        private static int Under(CityMesh mesh, GroundBox box)
        {
            int found = 0;
            for (int i = 0; i < 9; i++)
            {
                box.World((i % 3 - 1) * box.HalfX * 0.8f, (i / 3 - 1) * box.HalfZ * 0.8f, out PatchX[i], out PatchZ[i]);
                found += WalkGame.Sample(mesh, new Vector3(PatchX[i], box.Top, PatchZ[i]), 0.5f, out _) ? 1 : 0;
            }
            return found;
        }

        // One agent type's patch, built and added; its size in tiles of the city mesh's grid.
        private static int Lay(CityMesh mesh, GroundBox box, out NavMeshData data, out NavMeshDataInstance instance, out double ms)
        {
            var sources = new List<NavMeshBuildSource>(1) { WalkGame.Box(box, mesh.Area) };
            box.Extent(out float minX, out float minZ, out float maxX, out float maxZ);
            Bounds local = WalkGame.Local(mesh, minX - 1f, minZ - 1f, maxX + 1f, maxZ + 1f, box.Top - box.Depth - 3f, box.Top + 4f);
            data = WalkGame.Build(mesh, mesh.Settings, sources, local, out ms);
            instance = data != null ? WalkGame.Add(data) : default;
            return GroundRules.TilesOf(minX, minZ, maxX, maxZ, mesh.Grid);
        }

        private bool OnPatch(CityMesh mesh)
        {
            return WalkGame.Sample(mesh, _patchMiddle, 1f, out Vector3 hit) && _patch.Covers(hit.x, hit.z, 0.5f);
        }

        // The way from where the player stood to a point, on one agent type's mesh.
        private string PathTo(CityMesh mesh, Vector3 to)
        {
            if (!WalkGame.Sample(mesh, _patchFrom, 2f, out Vector3 from))
            {
                return "INVALID (no " + mesh.Label + " mesh where you stood)";
            }
            if (!WalkGame.Sample(mesh, to, 1f, out Vector3 end))
            {
                return "INVALID (no " + mesh.Label + " mesh there)";
            }
            return WalkGame.Path(mesh, from, end);
        }

        /// <summary>The fallback join: a two-way link of each agent type from where the player stood to the patch's near side, and a zone of pedestrians on it.</summary>
        private bool LinkPatch(out string refusal, out string detail)
        {
            refusal = null;
            detail = null;
            if (!_hasPatch && !LayPatch(out refusal, out detail))
            {
                return false;
            }
            CityMesh player = WalkGame.Player, npc = WalkGame.Npc;
            string playerPath = Join(player, ref _playerLink);
            string npcPath = Join(npc, ref _npcLink);
            if (_zone == null)
            {
                _zone = PeopleGame.Zone(_patchMiddle, _patch.Yaw, new Vector3(_patch.HalfX * 1.6f, 0f, _patch.HalfZ * 1.6f), out string why);
                _zoneWatch = 0f;
                _zoneMost = 0;
                _zoneTold = _zone == null;
                if (_zone == null && Meta.Dev.Verbose)
                {
                    Logger?.Info(GroundRules.PeopleLine(false, 0, 0f) + " (" + why + ")");
                }
            }
            _patchLeft = GroundRules.KeepSeconds;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(GroundRules.LinkLine(player.Label, playerPath, npc.Label, npcPath));
            }
            Notice(NewGroundText.Link, new Dictionary<string, string>(2) { { "player", playerPath }, { "people", npcPath } });
            Wake();
            return true;
        }

        // One agent type's link: from its mesh where the player stood to its mesh on the patch's near side; the path then.
        private string Join(CityMesh mesh, ref NavMeshLinkInstance link)
        {
            WalkGame.Unlink(ref link);
            GroundRules.NearSide(_patch, out float x, out float z);
            if (!WalkGame.Sample(mesh, _patchFrom, 2f, out Vector3 from))
            {
                return "INVALID (no " + mesh.Label + " mesh where you stood)";
            }
            if (!WalkGame.Sample(mesh, new Vector3(x, _patch.Top, z), 1f, out Vector3 to))
            {
                return "INVALID (no patch for " + mesh.Label + ")";
            }
            link = WalkGame.Link(mesh, from, to, GroundRules.LinkWidth);
            if (!link.valid)
            {
                return "INVALID (the link was not taken)";
            }
            return PathTo(mesh, _patchMiddle);
        }

        // The patch goes: the player is put back where they stood if on it, the people on it go back to the game's pool.
        private void EndPatch()
        {
            if (!_hasPatch)
            {
                return;
            }
            Vector3 at = WalkGame.At;
            if (WalkGame.OnFoot() && _patch.Covers(at.x, at.z, 1f))
            {
                WalkGame.Warp(_patchFrom);
            }
            WalkGame.ReleaseWalkersOn(_patch);
            PeopleGame.Unzone(ref _zone);
            WalkGame.Unlink(ref _playerLink);
            WalkGame.Unlink(ref _npcLink);
            WalkGame.Remove(ref _playerPatchOn, ref _playerPatch);
            WalkGame.Remove(ref _npcPatchOn, ref _npcPatch);
            _hasPatch = false;
        }
    }
}
