using System;
using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Scene control: the walls and bars cut out of the navigation mesh, and the check that each really cut it.</summary>
    public sealed partial class SceneCordon
    {
        private const float BarReach = 1.1f;      // metres off the tape's line a wall may stand and still cover it (a post is moved in 1 m at most)
        private const float WalkwayReach = 0.45f; // metres from a point of the line to the pedestrians' own mesh that make it a walkway
        private const float CarveWait = 0.8f;     // seconds after the walls go up before the navigation mesh is asked about them
        private const float WallSame = 0.02f;     // metres: a wall asked to stand this close to where it stands is not moved (no second cut of the mesh)

        private static readonly bool[] BarOpen = new bool[BarPoints];
        private static readonly float[] BarFrom = new float[Bars];
        private static readonly float[] BarTo = new float[Bars];
        private static readonly System.Text.StringBuilder Report = new System.Text.StringBuilder(160);

        // Every line of the rectangle, a point every half metre: where no wall stands and the pedestrians' mesh is, the open
        // points are joined into runs (CordonRules.Gaps), each barred, cut from the mesh as a wall is. Where that mesh is, is
        // asked once as the tape first goes up (before any cut) and kept for the tape run again round the crew's way.
        private int Bar()
        {
            if (!_redress)
            {
                FindWalkways();
            }
            int used = 0;
            for (int line = 0; line < 4 && used < Bars; line++)
            {
                // 0 the near end and 1 the far end (across the road), 2 the left side and 3 the right side (along it).
                Vector3 from = Point(line == 1 ? _front : _back, line == 3 ? _rightEdge : _left);
                Vector3 to = Point(line == 0 ? _back : _front, line == 2 ? _left : _rightEdge);
                Vector3 run = to - from;
                run.y = 0f;
                float length = run.magnitude;
                if (length < 0.5f)
                {
                    continue;
                }
                Vector3 way = run / length;
                int count = Mathf.Min(CordonRules.Points(length), BarPoints);
                for (int k = 0; k < count; k++)
                {
                    float at = CordonRules.At(k, count, length);
                    BarOpen[k] = _walkway[line * BarPoints + k] && !Covered(from, way, at);
                }
                int runs = CordonRules.Gaps(BarOpen, count, length, BarFrom, BarTo, Bars - used);
                for (int r = 0; r < runs; r++)
                {
                    Vector3 centre = from + way * ((BarFrom[r] + BarTo[r]) * 0.5f);
                    centre.y = StreetSpots.Ground(centre, out float ground) ? ground : _scene.y;
                    var size = new Vector3(WallThickness, 3f, Mathf.Max(0.5f, BarTo[r] - BarFrom[r]));
                    if (Stand(_bars[used], centre, Quaternion.LookRotation(way, Vector3.up), size))
                    {
                        _wallsChanged++;
                    }
                    _bars[used].gameObject.SetActive(true);
                    used++;
                }
            }
            return used;
        }

        // True if a standing wall covers that point of a line: running with it, within reach (a post moves in a metre at most).
        private bool Covered(Vector3 from, Vector3 way, float at)
        {
            for (int i = 0; i < _wallCount && i < Walls; i++)
            {
                if (!_walls[i].gameObject.activeSelf)
                {
                    continue;
                }
                Vector3 a = _wallFrom[i] - from;
                Vector3 b = _wallTo[i] - from;
                float alongA = a.x * way.x + a.z * way.z;
                float alongB = b.x * way.x + b.z * way.z;
                float offA = Mathf.Abs(a.x * way.z - a.z * way.x);
                float offB = Mathf.Abs(b.x * way.z - b.z * way.x);
                if (offA < BarReach && offB < BarReach && at >= Mathf.Min(alongA, alongB) - WallThickness && at <= Mathf.Max(alongA, alongB) + WallThickness)
                {
                    return true;
                }
            }
            return false;
        }

        // Where along each line the pedestrians' mesh is, a point every half metre (about 120 samples, once).
        private void FindWalkways()
        {
            for (int line = 0; line < 4; line++)
            {
                Vector3 from = Point(line == 1 ? _front : _back, line == 3 ? _rightEdge : _left);
                Vector3 to = Point(line == 0 ? _back : _front, line == 2 ? _left : _rightEdge);
                from.y = StreetSpots.Ground(from, out float low) ? low : _scene.y;
                to.y = StreetSpots.Ground(to, out float high) ? high : _scene.y;
                Vector3 run = to - from;
                run.y = 0f;
                float length = run.magnitude;
                int count = Mathf.Min(CordonRules.Points(length), BarPoints);
                for (int k = 0; k < BarPoints; k++)
                {
                    _walkway[line * BarPoints + k] = k < count && length >= 0.5f
                                                     && Walkway(Vector3.Lerp(from, to, length > 0f ? CordonRules.At(k, count, length) / length : 0f));
                }
            }
        }

        private static bool Walkway(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, WalkwayReach, StreetSpots.Pavement))
            {
                return false;
            }
            Vector3 off = hit.position - point;
            off.y = 0f;
            return off.sqrMagnitude < WalkwayReach * WalkwayReach;
        }

        // One wall along a run of tape; an end's covers its barriers too. False if the run is too short to matter.
        private bool SetWall(int index)
        {
            Vector3 run = _wallTo[index] - _wallFrom[index];
            float length = run.magnitude;
            if (length < 0.4f)
            {
                return false;
            }
            Vector3 centre = (_wallFrom[index] + _wallTo[index]) * 0.5f;
            float depth = WallThickness;
            if (_wallOut[index] != 0f)
            {
                depth = BarrierWall;
                centre += _forward * (_wallOut[index] * (BarrierWall - WallThickness) * 0.5f);
            }
            // From a metre under the road to two above it: the mesh need not lie exactly where the road's collider does.
            var size = new Vector3(depth, 3f, length + (_wallOut[index] != 0f ? 0f : WallThickness));
            if (Stand(_walls[index], centre, Quaternion.LookRotation(run / length, Vector3.up), size))
            {
                _wallsChanged++;
            }
            return true;
        }

        // Puts a wall or bar in place; true if it was moved, switched off for that (the caller switches it on). One already in
        // place is left alone, so the engine does not cut the mesh again; a carving obstacle moved while on is cut twice.
        private static bool Stand(NavMeshObstacle wall, Vector3 centre, Quaternion facing, Vector3 size)
        {
            GameObject box = wall.gameObject;
            Transform stands = wall.transform;
            if (box.activeSelf && (stands.position - centre).sqrMagnitude < WallSame * WallSame && Quaternion.Angle(stands.rotation, facing) < 0.5f
                && (wall.size - size).sqrMagnitude < WallSame * WallSame)
            {
                return false;
            }
            box.SetActive(false);
            wall.carveOnlyStationary = true;
            stands.SetPositionAndRotation(centre, facing);
            wall.center = new Vector3(0f, 0.5f, 0f);
            wall.size = size;
            return true;
        }

        // A moment after the walls went up: is the mesh under each gone? One not cut is set to cut at once and asked again.
        private void CheckWalls(float now)
        {
            _carveAt = 0f;
            try
            {
                int standing = 0;
                int cut = 0;
                Report.Length = 0;
                for (int i = 0; i < _wallCount && i < Walls; i++)
                {
                    Probe(_walls[i], _wallOut[i] != 0f ? "end " : "side ", ref standing, ref cut);
                }
                for (int i = 0; i < _barCount && i < Bars; i++)
                {
                    Probe(_bars[i], "bar ", ref standing, ref cut);
                }
                if (standing == 0)
                {
                    return;
                }
                if (cut < standing && _carveTries == 0)
                {
                    _carveTries = 1;
                    _carveAt = now + CarveWait;
                    Logger?.Info($"Cordon: {cut} of {standing} wall(s) cut out of the navigation mesh so far ({Report}); the rest are told to cut at once and are looked at again.");
                }
                else
                {
                    Logger?.Info($"Cordon: {cut} of {standing} wall(s) cut out of the navigation mesh ({Report})"
                                 + (cut < standing ? "; people on foot can cross the tape where one is not." : "; nobody on foot can cross the tape."));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Probe(NavMeshObstacle wall, string kind, ref int standing, ref int cut)
        {
            if (!wall.gameObject.activeInHierarchy)
            {
                return;
            }
            standing++;
            Vector3 at = wall.transform.position;
            bool whole = false;
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 0.6f, StreetActor.RoadFilter))
            {
                Vector3 off = hit.position - at;
                off.y = 0f;
                whole = off.sqrMagnitude < 0.12f * 0.12f;
            }
            if (!whole)
            {
                cut++;
            }
            else if (_carveTries == 0)
            {
                wall.carveOnlyStationary = false;
                wall.gameObject.SetActive(false);
                wall.gameObject.SetActive(true);
                MeshCuts.Note("a cordon's wall told to cut at once");
            }
            Report.Append(Report.Length > 0 ? ", " : "").Append(kind).Append(wall.size.z.ToString("F1", CultureInfo.InvariantCulture)).Append(whole ? " m NOT cut" : " m cut");
        }
    }
}
