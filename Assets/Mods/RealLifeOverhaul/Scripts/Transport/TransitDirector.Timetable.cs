using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's part for the place served: which one it is, and when each of its lines next calls there.</summary>
    public sealed partial class TransitDirector
    {
        // Which place has people waiting and buses coming: the nearest dressed one, kept until the player is well away.
        private void Serve(Vector3 at, float now)
        {
            if (_journey)
            {
                return;   // the stop the player's bus drives to is the one served (Journey)
            }
            if (_served != null && _player != PlayerStep.Choosing && !_ride.Riding)
            {
                // Well away from it, or on foot at another dressed stop (across the street, say): that one is served instead. On a
                // journey's change the place of the next bus is kept while the player walks to it.
                TransitStop stood = PlayerHelper.IsUsingVehicle || AtStop(_served, at) ? null : StoodAt(at);
                if (_changing && _hasPlan && stood == _plan.ChangeOff.At)
                {
                    stood = null;   // just off the first bus, at the stop they are walking from
                }
                float keep = _changing ? ChangeKeep : ServeKeep;
                if (CitySpots.Flat(_served.Sign - at) > keep * keep || !Dressed(_served) || (stood != null && stood != _served))
                {
                    if (_changing)
                    {
                        Logger?.Info("Transit check (journey): the player left the change at " + StopLabel(_served) + " before the " + LineNumber(_plan.ChangeOn)
                                     + " came: the journey ends there: PASS.");
                        DropPlan();
                    }
                    EndService();
                }
            }
            if (_served != null)
            {
                return;
            }
            TransitStop nearest = null;
            float best = ServeRange * ServeRange;
            for (int i = 0; i < Dressings; i++)
            {
                TransitStop stop = _dressings[i] != null ? _dressings[i].Stop : null;
                if (stop == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(stop.Sign - at);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = stop;
                }
            }
            if (nearest == null)
            {
                return;
            }
            BeginService(nearest, now);
        }

        // A place becomes the one that has people waiting and buses coming: its lines' timetables from now.
        private void BeginService(TransitStop stop, float now)
        {
            _served = stop.At;
            _calls = Mathf.Min(_served.Calls.Count, MostCalls);
            for (int i = 0; i < MostCalls; i++)
            {
                _pinned[i] = false;
                _doneAt[i] = double.MinValue;
                _callAt[i] = double.MaxValue;
            }
            _lastCall = double.MinValue;
            _frontCall = -1;
            _nextCall = -1;
            _refusals = 0;
            _nextRefusals = 0;
            _nextTry = 0f;
            _nextWaiter = now + 2f;
            _panelKey = int.MinValue;
            Schedule(NowMinutes());
        }

        private void EndService()
        {
            ReleaseRiders();
            HandOff(ref _bus);
            HandOff(ref _next);
            _bus.TakeLost();
            _next.TakeLost();
            _served = null;
            _frontCall = -1;
            _nextCall = -1;
            _calls = 0;
            _wasStanding = false;
            _doorFound = false;
            _refusals = 0;
            _player = PlayerStep.None;
            HideHud();
        }

        // The game's clock in minutes: its total, the day and the time of day.
        private static double NowMinutes()
        {
            return TimeHelper.NowInMinutes();
        }

        // Real seconds from now to a time on the game's clock, at the rate it is running at.
        private float SecondsTo(double minutes)
        {
            if (minutes >= double.MaxValue)
            {
                return float.MaxValue;
            }
            return (float)((minutes - NowMinutes()) / Mathf.Max(0.1f, _minutesPerSecond));
        }

        // When each line next calls at the place served by its timetable, no two nearer than SpacingSeconds: what the panel shows.
        // A call that is due or has a bus on its way is kept as it is.
        private void Schedule(double now)
        {
            if (_served == null)
            {
                return;
            }
            List<TransitStop> calls = _served.Calls;
            bool rain = Raining();
            float scale = CivicPolicy.ServiceScale;
            double spacing = TransitPlan.SpacingSeconds * Mathf.Max(0.1f, _minutesPerSecond);
            double anchor = _lastCall;
            int n = 0;
            for (int k = 0; k < _calls; k++)
            {
                if (_pinned[k])
                {
                    if (_callAt[k] < double.MaxValue)
                    {
                        anchor = Math.Max(anchor, _callAt[k]);
                    }
                    continue;
                }
                TransitStop call = calls[k];
                TransitLine line = TransitMap.Lines[call.Line];
                _sortAt[n] = TransitPlan.NextCall(Math.Max(now, _doneAt[k] + 1.0), line.Main, line.OffsetAt(call.Order), rain, scale);
                _sortWho[n] = k;
                n++;
            }
            TransitPlan.Space(_sortAt, _sortWho, n, spacing, anchor);
            for (int i = 0; i < n; i++)
            {
                _callAt[_sortWho[i]] = _sortAt[i];
            }
        }

        // The call due soonest that no bus is on its way for; -1 if none runs.
        private int Soonest()
        {
            int best = -1;
            double at = double.MaxValue;
            for (int k = 0; k < _calls; k++)
            {
                if ((k == _frontCall && _bus.State != TransitBus.Phase.Idle) || (k == _nextCall && _next.State != TransitBus.Phase.Idle))
                {
                    continue;
                }
                if (_callAt[k] < at)
                {
                    at = _callAt[k];
                    best = k;
                }
            }
            return best;
        }

        // When a line next calls at a stop of its: as the panel has it at the place served, else by the timetable. For the planner.
        private double CallAt(int line, int order, double at)
        {
            if (_served != null && line >= 0 && line < TransitMap.Lines.Count)
            {
                for (int k = 0; k < _calls; k++)
                {
                    TransitStop call = _served.Calls[k];
                    if (call.Line == line && call.Order == order && _callAt[k] >= at && _callAt[k] < double.MaxValue)
                    {
                        return _callAt[k];
                    }
                }
            }
            TransitLine l = TransitMap.Lines[line];
            return TransitPlan.NextCall(at, l.Main, l.OffsetAt(order), Raining(), CivicPolicy.ServiceScale);
        }

        private Func<int, int, double, double> _callAtFn;

        /// <summary>The planner's question of when a line calls at a stop, made once.</summary>
        internal Func<int, int, double, double> Calls => _callAtFn ?? (_callAtFn = CallAt);

        // Seconds before its call at which a bus is put on the road: what the longest start out of sight takes to drive.
        private float Lead => TransitPlan.LeadSeconds(BusCruise, _pace, LightsPerMetre, StartMost);

        /// <summary>Whether it is raining, if the game's weather can be asked at all.</summary>
        public static bool Raining()
        {
            try
            {
                return RainHelper.AreRainDropsFalling;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
