using System;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Scene control: the ambulance crew's way in and out, kept clear of everything of the cordon.</summary>
    public sealed partial class SceneCordon
    {
        /// <summary>The ambulance crew's way out and back: nothing of the cordon stands on it, and the tape is run again round it if it must be.</summary>
        public void SetCorridor(CrewPath there, CrewPath back)
        {
            _wayOut = there != null && there.Built ? there : null;
            _wayHome = back != null && back.Built ? back : null;
            CorridorClear = -1f;
            if (!Surveyed || _failed || !HasWay)
            {
                return;
            }
            if (!Up || Lowered || _props == null || !Shapes.Available)
            {
                PlaceDoor();   // the tape goes up round it when the police arrive
                return;
            }
            try
            {
                // Cones first: one on the way is simply taken in.
                int cones = 0;
                for (int i = 0; i < _cones.Length; i++)
                {
                    if (_cones[i].gameObject.activeSelf && WayClearance(_cones[i].position) - 0.15f < CrewClear - 0.02f)
                    {
                        _cones[i].gameObject.SetActive(false);
                        cones++;
                    }
                }
                if (cones > 0)
                {
                    Logger?.Info($"Cordon: {cones} cone(s) stood on the ambulance crew's way and are taken in.");
                }
                float clear = MeasureWay(false, out _, out string nearest);
                if (clear >= CrewClear - 0.02f)
                {
                    CorridorClear = clear;
                    Logger?.Info(FormattableString.Invariant(
                        $"Cordon: the tape stands clear of the crew's way as it is: the nearest thing ({nearest}) is {clear:F2} m from anything of the crew ({CrewClear:F2} m wanted): PASS."));
                    return;
                }
                Logger?.Info(FormattableString.Invariant($"Cordon: the crew's way comes within {clear:F2} m of the cordon as it stands ({nearest}); the tape is run again round it."));
                _oldCount = Mathf.Min(_wallCount, Walls);
                for (int i = 0; i < _oldCount; i++)
                {
                    _oldFrom[i] = _wallFrom[i];
                    _oldTo[i] = _wallTo[i];
                }
                PlaceDoor();
                _redress = true;
                Dress(Time.time);
            }
            catch (Exception e)
            {
                Fail(e);
            }
            finally
            {
                _redress = false;
                _oldCount = 0;
            }
        }

        private bool HasWay => _wayOut != null || _wayHome != null;

        private bool _redress;   // the tape is being run again round the crew's way: the cones are left as they stand

        private float WayClearance(Vector3 point)
        {
            float clear = float.MaxValue;
            if (_wayOut != null)
            {
                clear = Mathf.Min(clear, _wayOut.Clearance(point.x, point.z));
            }
            if (_wayHome != null)
            {
                clear = Mathf.Min(clear, _wayHome.Clearance(point.x, point.z));
            }
            return clear;
        }

        // The part of a straight line that the crew comes within a distance of, out or back, as fractions of it. False if none.
        private bool WayCrossing(Vector3 from, Vector3 to, float within, out float low, out float high)
        {
            low = 1f;
            high = 0f;
            bool any = false;
            for (int i = 0; i < 2; i++)
            {
                CrewPath way = i == 0 ? _wayOut : _wayHome;
                if (way != null && way.Crossing(from.x, from.z, to.x, to.z, within, out float first, out float last))
                {
                    low = Mathf.Min(low, first);
                    high = Mathf.Max(high, last);
                    any = true;
                }
            }
            return any;
        }

        // The near end's opening goes where the crew's way crosses that end, wide enough for it; else it stays where it was.
        private void PlaceDoor()
        {
            if (!WayCrossing(Point(_back, _left), Point(_back, _rightEdge), CrewClear, out float low, out float high))
            {
                return;
            }
            float span = _rightEdge - _left;
            float wide = Mathf.Min(Mathf.Max((high - low) * span, CrewGap), span);
            _doorWidth = wide;
            _doorAcross = Mathf.Clamp(_left + span * (low + high) * 0.5f, _left + wide * 0.5f, _rightEdge - wide * 0.5f);
        }

        // How near the crew's way comes to every post, tape, cone and barrier; with takeIn, what is too near is switched off.
        private float MeasureWay(bool takeIn, out int taken, out string nearest)
        {
            float least = float.MaxValue;
            float limit = CrewClear - 0.12f;
            taken = 0;
            nearest = "nothing";
            for (int i = 0; i < _placed && i < _posts.Length; i++)
            {
                Tally(_posts[i], WayClearance(_posts[i].position), "a post", takeIn, limit, ref least, ref taken, ref nearest);
            }
            for (int i = 0; i < _taped && i < _tapes.Length; i++)
            {
                if (!_tapes[i].gameObject.activeSelf)
                {
                    continue;
                }
                float clear = float.MaxValue;
                int pieces = Mathf.Max(1, Mathf.CeilToInt((_tapeTo[i] - _tapeFrom[i]).magnitude / 0.25f));
                for (int j = 0; j <= pieces; j++)
                {
                    clear = Mathf.Min(clear, WayClearance(Vector3.Lerp(_tapeFrom[i], _tapeTo[i], (float)j / pieces)));
                }
                Tally(_tapes[i], clear, "a length of tape", takeIn, limit, ref least, ref taken, ref nearest);
            }
            for (int i = 0; i < _cones.Length; i++)
            {
                Tally(_cones[i], WayClearance(_cones[i].position) - 0.15f, "a cone", takeIn, limit, ref least, ref taken, ref nearest);
            }
            for (int i = 0; i < _barriers.Length; i++)
            {
                Transform barrier = _barriers[i];
                Vector3 reach = barrier.right * (BarrierWidth * 0.5f - 0.2f);
                float clear = Mathf.Min(WayClearance(barrier.position), Mathf.Min(WayClearance(barrier.position + reach), WayClearance(barrier.position - reach))) - 0.2f;
                Tally(barrier, clear, "a barrier", takeIn, limit, ref least, ref taken, ref nearest);
            }
            return least;
        }

        private static void Tally(Transform prop, float clear, string what, bool takeIn, float limit, ref float least, ref int taken, ref string nearest)
        {
            if (!prop.gameObject.activeSelf)
            {
                return;
            }
            if (takeIn && clear < limit)
            {
                prop.gameObject.SetActive(false);
                taken++;
                return;
            }
            if (clear < least)
            {
                least = clear;
                nearest = what;
            }
        }

        // The proof for the log: what stands of the cordon measured against the crew's way, anything still on it taken in.
        private void CheckWay(int barriers)
        {
            CorridorClear = -1f;
            if (!HasWay || !Up || Lowered)
            {
                return;
            }
            float clear = MeasureWay(true, out int taken, out string nearest);
            CorridorClear = clear;
            bool crossesNear = WayCrossing(Point(_back, _left), Point(_back, _rightEdge), CrewClear, out _, out _);
            Logger?.Info("Cordon: run round the ambulance crew's way: "
                         + (crossesNear ? FormattableString.Invariant(
                             $"it comes in by the near end's opening, {_doorWidth:F1} m wide ({CrewGap - 0.2f:F1} m wanted)") : "it does not come in by the near end")
                         + (_wayOpenings > 0 ? $", {_wayOpenings} more opening(s) where it crosses a side or the far end" : "")
                         + $"; {_wayRefused} place(s) for a post, cone or barrier on it left empty, {taken} thing(s) on it taken in; the nearest left ({nearest}) is "
                         + FormattableString.Invariant(
                             $"{(clear == float.MaxValue ? 99f : clear):F2} m from anything of the crew ({CrewClear - 0.12f:F2} m at least, {CrewClear:F2} m wanted), {barriers} barrier(s) standing: ")
                         + (clear >= CrewClear - 0.12f && (!crossesNear || _doorWidth >= CrewGap - 0.2f) ? "PASS." : "FAIL."));
        }
    }
}
