using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One car running from one patrol car, from the moment it bolts to the moment it is either
    /// stopped with the patrol car behind it (and its driver dealt with at the roadside) or lost.
    /// It was part of TrafficAccidents (the hit-and-run) and is here so that a reckless driver a
    /// patrol car has seen is chased by the same rules: both owners keep the two cars and the
    /// roadside stop, start this with them and tick it; it drives nothing itself and decides only
    /// when the suspect brakes, when the two are held, and when it is over.
    /// The owner does what its own scene needs at the end (Tick says how it ended).
    /// A few comparisons a tick while a chase is on; nothing while there is none.
    /// </summary>
    public sealed class RoadChase
    {
        public enum Step { None, Fleeing, Stopping, Held }

        private const float StopAllowance = 7f;      // seconds the suspect is given to stop and the patrol car to pull up behind it
        private const float PulledUpWithin = 30f;    // metres from the suspect at which a standing patrol car has pulled up
        private const float ArrestAllowance = 45f;   // extra seconds a stopped suspect is held while the roadside stop is played out

        private ResponseUnit _suspect;
        private ResponseUnit _patrol;
        private RoadsideStop _stop;
        private bool _caught;
        private bool _arrest;
        private string _stoppedNotice;
        private float _end;
        private float _holdMin;

        public Step Now { get; private set; }

        /// <summary>True from the start of a chase to its end.</summary>
        public bool Running => Now != Step.None;

        /// <summary>Whether this chase ends with the suspect stopped (decided when it began).</summary>
        public bool WillBeCaught => _caught;

        /// <summary>
        /// Starts it. Both cars are being driven by their owner already: the suspect along its road, the
        /// patrol car following it.
        /// </summary>
        /// <param name="caught">Whether the suspect is stopped at the end of its run, or gets away.</param>
        /// <param name="until">When the run ends, unless <see cref="EndRun"/> is called sooner.</param>
        /// <param name="arrest">Stopped, the driver is arrested (hands up, cuffed, into the patrol car); otherwise an officer walks up to the window and back.</param>
        /// <param name="stoppedNotice">The notice shown when the two are held, or null for none.</param>
        public void Begin(ResponseUnit suspect, ResponseUnit patrol, RoadsideStop stop, bool caught, float until, bool arrest, string stoppedNotice)
        {
            _suspect = suspect;
            _patrol = patrol;
            _stop = stop;
            _caught = caught;
            _arrest = arrest;
            _stoppedNotice = stoppedNotice;
            _end = until;
            Now = Step.Fleeing;
        }

        /// <summary>The run ends now (the patrol car has closed up): the suspect brakes at the next tick, or is lost.</summary>
        public void EndRun(float now)
        {
            if (Now == Step.Fleeing)
            {
                _end = now;
            }
        }

        /// <summary>Forgets the chase. The cars and the people are the owner's to let go.</summary>
        public void Reset()
        {
            Now = Step.None;
            _suspect = null;
            _patrol = null;
            _stop = null;
        }

        /// <summary>
        /// One look at the chase. True when it is over, with how: the owner then lets the cars go
        /// (a suspect that was caught is taken off the road, one that got away goes back to traffic).
        /// </summary>
        public bool Tick(float now, out bool caught)
        {
            caught = false;
            if (Now == Step.None || _suspect == null || _patrol == null)
            {
                return false;
            }
            if (_suspect.IsLost || _patrol.IsLost)
            {
                // One of the two cars was taken off the road under us: nothing left to chase.
                caught = Now == Step.Held;
                return true;
            }

            switch (Now)
            {
                case Step.Fleeing:
                    // Out of time, or the suspect ran out of road.
                    if (now >= _end || !_suspect.Driver.Active)
                    {
                        if (!_caught)
                        {
                            return true;
                        }
                        _suspect.Driver.Brake();
                        Now = Step.Stopping;
                        _end = now + StopAllowance;
                    }
                    break;

                case Step.Stopping:
                    if (!_suspect.Driver.Active)
                    {
                        _suspect.Park();   // stopped: held still from now, not left standing on its springs until the patrol car is up
                    }
                    // Wait for the suspect to stop and the patrol car to pull up behind it.
                    bool pulledUp = _patrol.Driver.Speed < 0.5f
                                    && (_patrol.Car.transform.position - _suspect.Car.transform.position).sqrMagnitude < PulledUpWithin * PulledUpWithin;
                    if ((!_suspect.Driver.Active && pulledUp) || now >= _end)
                    {
                        _suspect.Park();
                        _patrol.Park();
                        Now = Step.Held;
                        _holdMin = now + Random.Range(8f, 14f);
                        _end = _holdMin;
                        OnStopped(now);
                    }
                    break;

                case Step.Held:
                    // Held for the usual few seconds, and beyond them until the roadside stop has been played out.
                    if (now >= _end || (now >= _holdMin && (_stop == null || _stop.Finished)))
                    {
                        caught = true;
                        return true;
                    }
                    break;
            }
            return false;
        }

        // The fleeing car has been stopped and the patrol car is parked behind it. This is the one place that
        // knows it, so the roadside stop starts here: an officer walking up from the patrol car, and (an arrest)
        // the driver out with their hands up.
        private void OnStopped(float now)
        {
            if (_stoppedNotice != null)
            {
                Notifications.Show(NotificationType.Info, _stoppedNotice, null, 8f, "reallife_accident");
            }
            if (_stop != null && StreetSpots.BesideCar(_suspect.Car, StreetSpots.Left, 1f, out Vector3 door)
                && _stop.Begin(_patrol.Car, door, _arrest, now))
            {
                // Both cars wait for the people; the owner's own deadline still ends everything.
                _end = _holdMin + ArrestAllowance;
            }
        }
    }
}
