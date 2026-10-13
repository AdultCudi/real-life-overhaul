using BigAmbitions.DayNightCycle;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The city after dark, cheaply: at the door of a nightclub, cinema, theatre or the casino
    /// that is open near the player between eight in the evening and three in the morning, two
    /// people of the living city's pool stand outside talking, a step along the pavement from
    /// the door. One venue at a time, two people, asked once a second by the director; they are
    /// put there and taken away only out of the player's view, and never while the living city
    /// has its own door scene out (the queue with its doorman, the doorman alone) or is short of
    /// people: the larger share of its pool stays its own.
    ///
    /// What is deliberately not done here: brighter shop signs (the game colours and switches
    /// each sign's own material instance itself, and a property block laid over that could not
    /// be verified without running the game), more taxis at closing time (the traffic system
    /// has no knob for one kind of car: its pool is filled once at start) and fewer walkers on
    /// quiet streets (the game sets pedestrian numbers by the hour itself).
    /// </summary>
    public sealed class NightLife
    {
        private const float Near = 75f, Far = 120f;
        private const float StayMinutes = 5f;

        private CityPerson _first, _second;
        private Vector3 _at;
        private float _nextLook;
        private float _since;

        public bool Out => _first != null || _second != null;

        private static bool Evening => TimeHelper.CurrentHour >= 20 || TimeHelper.CurrentHour < 3;

        /// <summary>Once a second while the player is out of doors.</summary>
        public void Tick(Vector3 player, float now)
        {
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city == null)
            {
                Clear();
                return;
            }
            bool wanted = Settings.Leisure && Settings.NightLife && Settings.CityLife && Evening && !RainHelper.isRaining;
            if (Out)
            {
                bool lost = _first == null || _second == null || _first.Lost || _second.Lost;
                bool gone = CitySpots.Flat(_at - player) > Far;
                bool done = !wanted || now - _since > StayMinutes * 60f;
                if (lost || gone || (done && !CitySpots.InView(_at)))
                {
                    Clear();
                    _nextLook = now + 40f;
                }
                return;
            }
            if (!wanted || now < _nextLook)
            {
                return;
            }
            _nextLook = now + 12f;
            if (city.People.Free < 4 || city.IsOut("door_queue") || city.IsOut("doorman") || !ClipLibrary.Has("talk_a") || !ClipLibrary.Has("talk_b"))
            {
                return;
            }
            CityBuildingController venue = OpenVenueNear(player);
            if (venue != null)
            {
                Stage(city, venue, now);
            }
        }

        /// <summary>Options-page button: the nearest venue within reach gets its two people now, whatever the hour. Says why not.</summary>
        public void Test(Vector3 player, float now)
        {
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city == null || !Settings.Leisure || !Settings.NightLife)
            {
                TestRefusals.Tell(LeisureDirector.Logger, "Night life", TestRefusal.FeatureOff);
                return;
            }
            Clear();
            CityBuildingController venue = OpenVenueNear(player);
            if (venue == null)
            {
                TestRefusals.Tell(LeisureDirector.Logger, "Night life", TestRefusal.NothingFits, 0, "no open nightclub, cinema, theatre or casino door within " + Near + " m");
                return;
            }
            if (city.People.Free < 2)
            {
                TestRefusals.Tell(LeisureDirector.Logger, "Night life", TestRefusal.PoolBusy);
                return;
            }
            if (!Stage(city, venue, now, true))
            {
                TestRefusals.Tell(LeisureDirector.Logger, "Night life", TestRefusal.NothingFits, 0, "no clear pavement beside that door");
            }
        }

        // The nearest door of a place people go at night that is open now. A pass over the city's buildings: once every twelve seconds at most.
        private static CityBuildingController OpenVenueNear(Vector3 player)
        {
            CityManager cityManager = InstanceBehavior<CityManager>.Instance;
            if (cityManager == null || cityManager.cityBuildingControllers == null)
            {
                return null;
            }
            CityBuildingController best = null;
            float bestSqr = Near * Near;
            CityBuildingController[] all = cityManager.cityBuildingControllers;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController building = all[i];
                if (building == null || building.buildingRegistration == null)
                {
                    continue;
                }
                float sqr = (building.transform.position - player).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }
                string type = building.buildingRegistration.businessTypeName;
                if (type != LeisureVenues.Nightclub && type != LeisureVenues.Cinema && type != LeisureVenues.Theater && type != "ba:businesstype_casino")
                {
                    continue;
                }
                if (!BusinessHelper.IsBusinessOpen(building.buildingRegistration))
                {
                    continue;
                }
                bestSqr = sqr;
                best = building;
            }
            return best;
        }

        private bool Stage(CityLifeDirector city, CityBuildingController venue, float now, bool inView = false)
        {
            if (!JobSpots.Front(venue, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = front + along * (side * 2.0f);
                Vector3 b = front + along * (side * 2.9f);
                if (!CitySpots.Stand(a, 0.35f, out a) || !CitySpots.Stand(b, 0.35f, out b) || CitySpots.Flat(a - b) < 0.75f
                    || (!inView && (CitySpots.InView(a) || CitySpots.InView(b))))
                {
                    continue;
                }
                CityPerson first = city.People.Take(CityLook.Civilian, a, b);
                CityPerson second = first != null ? city.People.Take(CityLook.Civilian, b, a) : null;
                if (first == null || second == null || !first.Work("talk_a", city.Props) || !second.Work("talk_b", city.Props))
                {
                    // Both talk or neither stands there: nobody gestures at nobody.
                    if (first != null)
                    {
                        city.People.Release(first);
                    }
                    if (second != null)
                    {
                        city.People.Release(second);
                    }
                    return false;
                }
                _first = first;
                _second = second;
                _at = (a + b) * 0.5f;
                _since = now;
                LeisureDirector.Logger?.Info("Leisure: two people outside '" + venue.name + "' for the evening.");
                return true;
            }
            return false;
        }

        public void Clear()
        {
            CityLifeDirector city = CityLifeDirector.Instance;
            try
            {
                if (city != null)
                {
                    if (_first != null)
                    {
                        city.People.Release(_first);
                    }
                    if (_second != null)
                    {
                        city.People.Release(_second);
                    }
                }
            }
            catch (System.Exception e)
            {
                LeisureDirector.Logger?.Error(e);
            }
            _first = null;
            _second = null;
        }
    }

    /// <summary>
    /// What drink does to driving: with two or more drinks in the blood the car the player drives
    /// wanders, a slow uneven pull to one side and the other that has to be steered against, the
    /// stronger the more was drunk. One small torque per physics step, and only while this
    /// component is switched on, which the director does once a second and only while the player
    /// is at the wheel and tipsy. Nothing is changed on the car: there is nothing to put back.
    /// </summary>
    public sealed class TipsyDriving : MonoBehaviour
    {
        private Rigidbody _body;
        private float _strength;
        private float _seed;

        public void Drive(Rigidbody body, float drinks)
        {
            _body = body;
            _strength = Mathf.Clamp(0.35f + 0.3f * (drinks - BarDrinks.TipsyFrom), 0.35f, 1.3f);
            if (!enabled)
            {
                _seed = Random.value * 20f;
                enabled = true;
            }
        }

        public void Stop()
        {
            _body = null;
            if (enabled)
            {
                enabled = false;
            }
        }

        private void Awake()
        {
            enabled = false;
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Leisure: TipsyDriving.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (_body == null)
            {
                enabled = false;
                return;
            }
            float speed = _body.velocity.magnitude;
            if (speed < 4f)
            {
                return;
            }
            float t = Time.time + _seed;
            float wander = Mathf.Sin(t * 0.83f) + 0.6f * Mathf.Sin(t * 2.1f + 1.3f);
            _body.AddTorque(Vector3.up * (wander * _strength * Mathf.Min(1f, speed / 12f)), ForceMode.Acceleration);
        }
    }
}
