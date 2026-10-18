using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One car running from one patrol car, from the moment it bolts until it is stopped (its driver dealt with at the
    /// roadside) or lost: the accident's hit-and-run, and a reckless driver the police saw. It drives nothing; it decides when
    /// the suspect brakes, when the two are held, and when it is over. A few comparisons a tick while on.
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

        /// <summary>Starts it; both cars are driven by their owner already, the suspect along its road, the patrol car following.</summary>
        /// <param name="caught">Whether the suspect is stopped at the end of its run, or gets away.</param>
        /// <param name="until">When the run ends, unless <see cref="EndRun"/> is called sooner.</param>
        /// <param name="arrest">Stopped, the driver is arrested; otherwise an officer walks up to the window and back.</param>
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

        /// <summary>One look at the chase; true when it is over, with how. The owner lets the cars go.</summary>
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

        // Stopped with the patrol car behind: the roadside stop starts, an officer walking up (for an arrest, the driver hands up).
        private void OnStopped(float now)
        {
            if (_stoppedNotice != null)
            {
                TrafficAccidents.Notice(NotificationType.Info, _stoppedNotice, 8f);
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
