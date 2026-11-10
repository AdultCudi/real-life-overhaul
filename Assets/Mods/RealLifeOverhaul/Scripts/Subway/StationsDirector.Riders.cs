using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Subway
{
    /// <summary>
    /// The stations' director's part for the city's people on the line: at the entrance the player is near, a train of the
    /// day brings somebody up, who walks off along the pavement, and somebody walks in from along the pavement and goes
    /// down to it. Which trains, who and which way are mixed from the day, the train and the station
    /// (<see cref="StationsRules.ComesUp"/>, <see cref="StationsRules.GoesDown"/>), so they are the same on every machine
    /// and never chance; the trains are the service's own (SubwayJourney.Frequency, the clock the player's journey waits
    /// on). They are people beside the bus stops' own, whose pace is untouched. Two people at the most, made behind the
    /// loading screen; under the option for people at the stops. COST: with nobody out, one look a second (a distance to
    /// each of three stations, the clock read); four a second while somebody walks.
    /// </summary>
    public sealed partial class StationsDirector
    {
        private enum RiderStep
        {
            None = 0,
            WalkingOff = 1,   // came up, walks away along the pavement
            WalkingIn = 2,    // walks to the entrance's mouth
            GoingDown = 3     // at the mouth, a moment before the stairs have them
        }

        private struct Rider
        {
            public StreetActor Person;
            public RiderStep Step;
            public float Until;
        }

        private const float WalkMost = 90f;        // s somebody is given to walk their way before they are taken in all the same
        private const float AtMouth = 0.8f;        // s somebody stands at the mouth before the stairs have them
        private const float PavementFind = 8f;     // m round a place a point of pavement is looked for

        private JobCast _cast;
        private Rider _up, _down;
        private int _near = -1;          // the station whose people are shown, as StationsGame counts them
        private int _train = -1;         // the last train of the day seen there
        private int _cameUp, _wentDown;  // counted for the check's line

        // Behind the loading screen: the two people are made and shelved now, so none is made in play.
        private void PrewarmRiders()
        {
            if (!Settings.TransitPeople || !Settings.UptownSubway)
            {
                return;
            }
            _cast = _cast ?? new JobCast(this, Logger);
            _cast.Prewarm(JobCast.Size);
        }

        // True while somebody is out: the looks then come four a second.
        private bool RunRiders(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (!Standing || !Settings.TransitPeople || player == null || BuildingManager.IsInsideBuilding || SubwaySystem.IsRiding || CityMap.IsOpen)
            {
                if (_near >= 0)
                {
                    ReleaseRiders();
                    _near = -1;
                }
                return false;
            }
            int near = Nearest(player.transform.position, _near >= 0 ? StationsRules.RidersKeep : StationsRules.RidersNear);
            if (near != _near)
            {
                ReleaseRiders();
                _near = near;
                _train = near >= 0 ? TrainNow() : -1;   // the train under way as the player comes near is not shown: nobody appears at once
            }
            if (near < 0)
            {
                return false;
            }
            Step(ref _up, now);
            Step(ref _down, now);
            int train = TrainNow();
            if (train != _train)
            {
                _train = train;
                NewTrain(near, train, now);
            }
            bool anyOut = _up.Step != RiderStep.None || _down.Step != RiderStep.None;
            if (anyOut)
            {
                _cast?.Cull();
            }
            return anyOut;
        }

        private static int TrainNow()
        {
            return StationsRules.Train(TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute, SubwayJourney.Frequency());
        }

        // The standing station nearest a place within a reach, or -1.
        private static int Nearest(Vector3 at, float within)
        {
            int best = -1;
            float bestAt = within * within;
            for (int i = 0; i < StationsGame.Count; i++)
            {
                StationEntrance station = StationsGame.StationAt(i);
                if (station == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(station.transform.position - at);
                if (sqr < bestAt)
                {
                    bestAt = sqr;
                    best = i;
                }
            }
            return best;
        }

        private void NewTrain(int station, int train, float now)
        {
            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            bool weekend = TimeHelper.IsWeekend;
            StationSite site = StationsGame.SiteAt(station);
            int place = site != null ? IndexOf(site) : station;   // the plan's place, so a station missing at this load changes nobody else's
            if (_up.Step == RiderStep.None && StationsRules.ComesUp(day, train, place, hour, weekend))
            {
                ComeUp(station, place, day, train, now);
            }
            if (_down.Step == RiderStep.None && StationsRules.GoesDown(day, train, place, hour, weekend))
            {
                GoDown(station, place, day, train, now);
            }
        }

        private static int IndexOf(StationSite site)
        {
            for (int i = 0; i < StationsRules.Sites.Count; i++)
            {
                if (ReferenceEquals(StationsRules.Sites[i], site))
                {
                    return i;
                }
            }
            return 0;
        }

        // Somebody comes up at the mouth and walks off along the pavement.
        private bool ComeUp(int station, int place, int day, int train, float now)
        {
            StationEntrance entrance = StationsGame.StationAt(station);
            if (entrance == null || entrance.Arrival == null)
            {
                return false;
            }
            Vector3 mouth = entrance.Arrival.position;
            if (!Far(mouth, day, train, place, false, out Vector3 away))
            {
                return false;   // nowhere to walk off to just now
            }
            int look = StationsRules.RiderLook(day, train, place, false, out bool female);
            _cast = _cast ?? new JobCast(this, Logger);
            StreetActor person = _cast.Put(mouth, false, look, female, false);
            if (person == null)
            {
                return false;
            }
            person.StandAt(mouth, mouth + entrance.transform.forward * 3f);
            if (!person.MoveTo(away, Pace.Walk))
            {
                _cast.Release(person);
                return false;
            }
            _up = new Rider { Person = person, Step = RiderStep.WalkingOff, Until = now + WalkMost };
            _cameUp++;
            return true;
        }

        // Somebody walks in from along the pavement and goes down.
        private bool GoDown(int station, int place, int day, int train, float now)
        {
            StationEntrance entrance = StationsGame.StationAt(station);
            if (entrance == null || entrance.Arrival == null)
            {
                return false;
            }
            Vector3 mouth = entrance.Arrival.position;
            if (!Far(mouth, day, train, place, true, out Vector3 from))
            {
                return false;
            }
            int look = StationsRules.RiderLook(day, train, place, true, out bool female);
            _cast = _cast ?? new JobCast(this, Logger);
            StreetActor person = _cast.Put(from, false, look, female, false);
            if (person == null)
            {
                return false;
            }
            if (!person.MoveTo(mouth, Pace.Walk))
            {
                _cast.Release(person);
                return false;
            }
            _down = new Rider { Person = person, Step = RiderStep.WalkingIn, Until = now + WalkMost };
            return true;
        }

        // A point of pavement along one of eight ways round the mouth that can be walked to: one out of view first, else the first found.
        private static bool Far(Vector3 mouth, int day, int train, int place, bool down, out Vector3 found)
        {
            found = default;
            bool any = false;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                StationsRules.Along(mouth.x, mouth.z, StationsRules.RiderBearing(day, train, place, down, attempt), StationsRules.RiderWalk, out float x, out float z);
                if (!CitySpots.Pavement(new Vector3(x, mouth.y, z), PavementFind, out Vector3 spot)
                    || CitySpots.Flat(spot - mouth) < StationsRules.RiderWalkLeast * StationsRules.RiderWalkLeast || !CitySpots.Reachable(mouth, spot))
                {
                    continue;
                }
                if (!CitySpots.InView(spot))
                {
                    found = spot;
                    return true;
                }
                if (!any)
                {
                    any = true;
                    found = spot;
                }
            }
            return any;
        }

        private void Step(ref Rider rider, float now)
        {
            if (rider.Step == RiderStep.None)
            {
                return;
            }
            StreetActor person = rider.Person;
            if (person == null || !person.IsValid || person.Limp || !person.InUse)
            {
                Drop(ref rider);   // knocked down, or gone with the scene: no longer ours to move
                return;
            }
            switch (rider.Step)
            {
                case RiderStep.WalkingOff:
                    if (person.Within(1.2f) || now >= rider.Until)
                    {
                        Drop(ref rider);
                    }
                    break;
                case RiderStep.WalkingIn:
                    if (person.Within(0.5f))
                    {
                        StationEntrance entrance = StationsGame.StationAt(_near);
                        Vector3 here = person.Position;
                        person.StandAt(here, entrance != null ? entrance.transform.position : here + Vector3.forward);
                        rider.Step = RiderStep.GoingDown;
                        rider.Until = now + AtMouth;
                        _wentDown++;
                    }
                    else if (now >= rider.Until)
                    {
                        Drop(ref rider);
                    }
                    break;
                case RiderStep.GoingDown:
                    if (now >= rider.Until)
                    {
                        Drop(ref rider);
                    }
                    break;
            }
        }

        private void Drop(ref Rider rider)
        {
            if (rider.Person != null)
            {
                _cast?.Release(rider.Person);
            }
            rider = default;
        }

        // Everybody of the line taken in: the station went, the player left, a ride began, the city goes.
        private void ReleaseRiders()
        {
            Drop(ref _up);
            Drop(ref _down);
            _cast?.ReleaseAll();
        }
    }
}
