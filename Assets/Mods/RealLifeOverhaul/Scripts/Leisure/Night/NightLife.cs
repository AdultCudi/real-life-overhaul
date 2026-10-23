using BigAmbitions.DayNightCycle;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The city after dark: from 20:00 to 03:00 two people of the living city's pool talk outside an open nightclub,
    /// cinema, theatre or casino near the player, put out of view; never while the living city has its own door scene.
    /// </summary>
    public sealed class NightLife
    {
        private const float StayMinutes = 5f;   // real minutes

        private CityPerson _first, _second;
        private Vector3 _at;
        private float _nextLook;
        private float _since;

        public bool Out => _first != null || _second != null;

        private static bool Evening => TimeHelper.CurrentHour >= 20 || TimeHelper.CurrentHour < 3;

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
                bool gone = LeisureReach.Beyond(CitySpots.Flat(_at - player), LeisureReach.NightKept);
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

        /// <summary>Options-page button: the nearest open venue gets its two people now, at any hour.</summary>
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
                TestRefusals.Tell(LeisureDirector.Logger, "Night life", TestRefusal.NothingFits, 0,
                    "no open nightclub, cinema, theatre or casino door within " + LeisureReach.NightNear + " m");
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

        // A pass over the city's buildings, at most once every twelve seconds.
        private static CityBuildingController OpenVenueNear(Vector3 player)
        {
            CityManager cityManager = InstanceBehavior<CityManager>.Instance;
            if (cityManager == null || cityManager.cityBuildingControllers == null)
            {
                return null;
            }
            CityBuildingController best = null;
            float bestSqr = LeisureReach.NightNear * LeisureReach.NightNear;
            CityBuildingController[] all = GameDlc.Open(cityManager);
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
                if (!CitySpots.Stand(a, 0.35f, out a) || !CitySpots.Stand(b, 0.35f, out b) || LeisureReach.Within(CitySpots.Flat(a - b), LeisureReach.NightApart)
                    || (!inView && (CitySpots.InView(a) || CitySpots.InView(b))))
                {
                    continue;
                }
                CityPerson first = city.People.Take(CityLook.Civilian, a, b);
                CityPerson second = first != null ? city.People.Take(CityLook.Civilian, b, a) : null;
                if (first == null || second == null || !first.Work("talk_a", city.Props) || !second.Work("talk_b", city.Props))
                {
                    // Both talk or neither stands there.
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
    /// Drink at the wheel: an uneven pull to either side, stronger the more was drunk. On only while the player drives
    /// tipsy, adding one torque per physics step; off it costs nothing a frame. Nothing on the car is changed.
    /// </summary>
    public sealed class TipsyDriving : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("leisure", "TipsyDriving", typeof(TipsyDriving));

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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
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
