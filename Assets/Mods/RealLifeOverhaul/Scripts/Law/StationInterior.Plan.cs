using System;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>The station's own pieces planned on the measured room: the counter, the bench, the cell, the office desks, the board.</summary>
    internal sealed partial class StationInterior
    {
        private bool PlanDesk()
        {
            float half = _deskSize.x * 0.5f;
            float deep = _deskSize.z * 0.5f;
            float depth = Mathf.Clamp(_length * 0.42f, 3.2f, 6.5f);
            if (_length < depth + deep + 1.3f)
            {
                depth = _length - deep - 1.3f;
            }
            _sergeantAt = At(Mathf.Min(_length - 0.5f, 2.8f), 0f);
            ServiceSpot = At(Mathf.Min(_length - 0.5f, 2.8f) - 1.3f, 0f);
            ServiceFace = _sergeantAt;
            if (depth < 2.6f)
            {
                return false;
            }
            float left = Run(At(depth, 0f), -_right, 30f);
            float right = Run(At(depth, 0f), _right, 30f);
            if (left < 0f || right < 0f)
            {
                return false;
            }
            float aside = left + right >= half * 2f + 2.2f ? (right - left) * 0.5f : 0f;
            if (Mathf.Max(left + aside - half, right - aside - half) < 0.9f)
            {
                if (left + right < half * 2f + 0.95f)
                {
                    return false;   // no wider than the counter: nobody could get round it
                }
                aside = right - half - 0.05f;
            }
            Vector3 middle = At(depth, aside);
            if (!Free(middle, _right, half + 0.05f, _in, deep + 0.05f) || !Free(At(depth + deep + 0.5f, aside), _right, 0.3f, _in, 0.3f)
                || !Free(At(depth - deep - 0.75f, aside), _right, 0.3f, _in, 0.3f))
            {
                return false;
            }
            _deskDepth = depth;
            _deskAside = aside;
            middle.y = _floor;
            _desk.SetPositionAndRotation(middle, Quaternion.LookRotation(-_in, Vector3.up));
            if (_deskName != null)
            {
                _deskName.text = Precinct.Name.ToUpperInvariant();
            }
            _sergeantAt = At(depth + deep + 0.5f, aside);
            ServiceSpot = At(depth - deep - 0.75f, aside);
            ServiceFace = _sergeantAt;
            return true;
        }

        private bool PlanWaiting(out int benchSide)
        {
            benchSide = 0;
            const float depth = 2.3f;
            float half = _benchSize.x * 0.5f;
            float deep = _benchSize.z;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? 1 : -1;
                if (!SideWall(depth, side, half, out float edge) || edge < 1.5f)
                {
                    continue;
                }
                // Clear of the counter if that runs up to this wall.
                if (HasDesk && depth + half + 0.7f > _deskDepth - _deskSize.z * 0.5f && side * _deskAside + _deskSize.x * 0.5f > edge - deep - 0.6f)
                {
                    continue;
                }
                if (!Free(At(depth, side * (edge - 0.45f)), _in, half, _right, 0.4f))
                {
                    continue;
                }
                Vector3 at = At(depth, side * (edge + _radius - deep * 0.5f - FromWall));
                at.y = _floor;
                _bench.SetPositionAndRotation(at, Quaternion.LookRotation(_right * -side, Vector3.up));
                _visitorAt = At(depth + 0.2f, side * (edge - 0.7f));
                _visitorFace = HasDesk ? At(_deskDepth, _deskAside) : _visitorAt + _right * -side * 4f;
                _hasVisitor = true;
                benchSide = side;
                return true;
            }
            return false;
        }

        // The holding cell at the back: a corner closed with two barred panels, or the whole back of a room one panel wide.
        private bool PlanCell(out int cornerSide)
        {
            cornerSide = 0;
            _cellBenchFits = false;
            float panel = Mathf.Clamp(_barsSize.x, 2.2f, 3.8f);
            float back = _length + _radius;   // the back wall's surface, as near as the floor's edge tells
            float served = HasDesk ? _deskDepth + _deskSize.z * 0.5f : 2.8f;
            float deep = Mathf.Min(panel, back - (served + 2.3f));
            if (deep < 2.3f)
            {
                _fitted.Append("cell: the room is not deep enough behind the desk; ");
                return false;
            }
            float left = Run(At(_length - 0.5f, 0f), -_right, 30f);
            float right = Run(At(_length - 0.5f, 0f), _right, 30f);
            if (left < 0f || right < 0f)
            {
                return false;
            }
            float wide = left + right + _radius * 2f;
            if (Mathf.Abs(wide - panel) <= 0.35f)
            {
                // The whole back of the room: one panel from wall to wall, stretched a little to meet them.
                float aside = (right - left) * 0.5f;
                if (!SideWall(_length - deep * 0.5f, -1, deep * 0.4f, out float a) || !SideWall(_length - deep * 0.5f, 1, deep * 0.4f, out float b)
                    || Mathf.Abs(a - left) > Straight || Mathf.Abs(b - right) > Straight
                    || !Free(At(back - deep * 0.5f, aside), _right, wide * 0.5f - _radius - 0.08f, _in, deep * 0.5f - _radius - 0.08f))
                {
                    _fitted.Append("cell: the back of the room is not a plain alcove; ");
                    return false;
                }
                PutBars(_barsFront, At(back - deep, aside), -_in, wide / _barsSize.x);
                SetCell(back, deep, aside);
                _cellPanel = wide;
                _cellSide = 0;
                return true;
            }
            if (wide < panel + 0.8f)
            {
                _fitted.Append(FormattableString.Invariant($"cell: the back of the room is {wide:F1} m wide, neither one panel nor room for a corner; "));
                return false;
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? -1 : 1;
                float edge = side < 0 ? left : right;
                float wall = edge + _radius;                      // the side wall's surface, measured from the way in
                float aside = side * (wall - panel * 0.5f);       // the middle of the cell
                // The side wall straight for the cell's depth, the back wall for its width, and all of it floor.
                if (!SideWall(_length - deep * 0.5f, side, deep * 0.4f, out float mid) || Mathf.Abs(mid - edge) > Straight)
                {
                    continue;
                }
                bool square = true;
                for (int probe = 0; probe < 3 && square; probe++)
                {
                    float run = Run(At(_length - 1.2f, side * (edge - 0.25f - probe * (panel - 0.7f) * 0.5f)), _in, 6f);
                    square = run >= 0f && Mathf.Abs(run - 1.2f) < Straight;
                }
                if (!square || !Free(At(back - deep * 0.5f, aside), _right, panel * 0.5f - _radius - 0.08f, _in, deep * 0.5f - _radius - 0.08f))
                {
                    continue;
                }
                PutBars(_barsFront, At(back - deep, aside), -_in, panel / _barsSize.x);
                PutBars(_barsSide, At(back - deep * 0.5f, side * (wall - panel)), _right * -side, deep / _barsSize.x);
                SetCell(back, deep, aside);
                _cellPanel = panel;
                _cellSide = side;
                cornerSide = side;
                PlanEvidence(back, side * (wall - panel), -side, side < 0 ? right : left);
                return true;
            }
            _fitted.Append("cell: neither back corner is square and clear; ");
            return false;
        }

        private void PutBars(Transform bars, Vector3 at, Vector3 facing, float stretch)
        {
            at.y = _floor;
            bars.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Vector3.up));
            bars.localScale = new Vector3(stretch, 1f, 1f);
        }

        private void SetCell(float back, float deep, float aside)
        {
            _cellBack = back;
            _cellDeep = deep;
            _cellAside = aside;
            Vector3 middle = At(back - deep * 0.5f, aside);
            CellSpot = NavMesh.SamplePosition(middle, out NavMeshHit hit, 0.6f, _filter) ? hit.position : middle;
            CellFace = CellSpot - _in * 4f;
            // A bench against the back wall, if what is left in front of it is still room to stand.
            if (deep - _benchSize.z >= 1.5f)
            {
                Vector3 at = At(back - _benchSize.z * 0.5f - FromWall, aside);
                at.y = _floor;
                _cellBench.SetPositionAndRotation(at, Quaternion.LookRotation(-_in, Vector3.up));
                _cellBenchFits = true;
                CellSpot = NavMesh.SamplePosition(At(back - _benchSize.z - 0.75f, aside), out NavMeshHit front, 0.5f, _filter) ? front.position : CellSpot;
            }
        }

        private void PlanEvidence(float back, float from, int toward, float edge)
        {
            float to = toward * (edge + _radius);
            if (Mathf.Abs(to - from) < 1.7f)
            {
                return;
            }
            float aside = (from + to) * 0.5f;
            float run = Run(At(_length - 0.6f, aside), _in, 4f);
            if (run < 0f || Mathf.Abs(run - 0.6f) > Straight)
            {
                return;
            }
            if (!Surface(At(_length, aside), _in, out Vector3 surface))
            {
                _fitted.Append("evidence door: the back wall's surface was not found; ");
                return;
            }
            _evidence.SetPositionAndRotation(surface - _in * 0.004f, Quaternion.LookRotation(-_in, Vector3.up));
            Show(_evidence, true);
            _fitted.Append("evidence-room door; ");
        }

        private void PlanOffice(bool cell)
        {
            _staffed[0] = false;
            _staffed[1] = false;
            if (!HasDesk)
            {
                return;
            }
            float from = _deskDepth + _deskSize.z * 0.5f + 1.7f;
            float to = cell ? Vector3.Dot(_barsFront.position - _origin, _in) - 1.4f : _length - 1.0f;
            if (to - from < 1.5f)
            {
                return;
            }
            float depth = (from + to) * 0.5f;
            int placed = 0;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? 1 : -1;
                if (!SideWall(depth, side, 0.7f, out float edge) || edge < 1.6f)
                {
                    continue;
                }
                // Not where the sergeant stands, and room for whoever works at it.
                if (side * _deskAside + 0.6f > edge - 1.6f && depth < _deskDepth + _deskSize.z * 0.5f + 2.2f)
                {
                    continue;
                }
                Vector3 stand = At(depth, side * (edge + _radius - FromWall - 0.70f - 0.50f));
                if (!Free(stand, _in, 0.7f, _right, 0.3f))
                {
                    continue;
                }
                Vector3 at = At(depth, side * (edge + _radius - FromWall - 0.35f));
                at.y = _floor;
                _desks[placed].SetPositionAndRotation(at, Quaternion.LookRotation(_right * -side, Vector3.up));
                _staffAt[placed] = stand;
                _staffFace[placed] = at;
                _staffed[placed] = true;
                placed++;
            }
            if (placed > 0)
            {
                _fitted.Append(placed).Append(placed == 1 ? " office desk; " : " office desks; ");
            }
        }

        private void PlanBoard(int preferred)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? preferred : -preferred;
                for (int place = 0; place < 2; place++)
                {
                    float depth = place == 0 ? 2.3f : 4.4f;
                    if (depth > _length - 1f || !SideWall(depth, side, 0.7f, out float edge) || !Surface(At(depth, side * edge), _right * side, out Vector3 surface))
                    {
                        continue;
                    }
                    // Not behind the bench, nor behind an officer's desk.
                    if (_hasVisitor && Mathf.Abs(Vector3.Dot(_bench.position - surface, _in)) < 1.7f && CitySpots.Flat(_bench.position - surface) < 2.2f * 2.2f)
                    {
                        continue;
                    }
                    bool taken = false;
                    for (int i = 0; i < _desks.Length; i++)
                    {
                        taken |= _staffed[i] && CitySpots.Flat(_desks[i].position - surface) < 1.9f * 1.9f;
                    }
                    if (taken || (HasDesk && CitySpots.Flat(_desk.position - surface) < 1.6f * 1.6f))
                    {
                        continue;
                    }
                    _board.SetPositionAndRotation(surface + Vector3.up * 1.55f - _right * side * 0.004f, Quaternion.LookRotation(_right * -side, Vector3.up));
                    Show(_board, true);
                    _fitted.Append("notice board; ");
                    return;
                }
            }
            _fitted.Append("notice board: no side wall whose surface was found; ");
        }
    }
}
