using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using Localizor;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The living city: people at work and about their day on the pavements near the player. This decides what is out and
    /// where; each activity plays itself (<see cref="CityActivity"/>) and the catalogue says what exists. The budget, kept
    /// here and in CityPeople: three sites and eight people at most, 45 to 95 metres away, set up and taken away only out
    /// of view (beyond 150 metres at once), and people who come and go walk in and off out of view. Cost: a tick once a
    /// second and a new site tried every six (a few physics queries); per frame only somebody turning to face a point or a
    /// moment due in a clip. Nothing is made or destroyed as sites come and go, and nothing is saved; indoors, a time skip
    /// or a fault clears the street.
    /// </summary>
    public sealed partial class CityLifeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("citylife", "CityLifeDirector", typeof(CityLifeDirector));

        public const int MaxSites = 3;

        private const float TickInterval = 1f;       // seconds
        private const float SpawnInterval = 6f;      // seconds between tries for a new site
        private const float WarmInterval = 2.5f;     // seconds between making two of the pool's characters, after the city loads
        private const float NearRing = 45f;          // metres from the player that a site is set up within
        private const float FarRing = 95f;
        private const float TooFar = 150f;           // a site this far from the player is taken away whether or not it is in view
        private const float SiteGap = 28f;           // two sites are never closer together than this
        private const int RecentMemory = 2;        // an activity is not dealt again until this many others have been

        private struct Running
        {
            public CityEntry Entry;
            public float EndsAt;
            public bool Test;   // put out by a test button: the weather and the family switches do not take it in
        }

        // Something to do at a moment inside a clip: put a cup in a hand, stand a box on the ground.
        private struct Moment
        {
            public float At;
            public ICityActivity Owner;
            public int What;
            public CityPerson Who;
        }

        private struct Facing
        {
            public CityPerson Who;
            public Quaternion To;
        }

        private const float TurnRate = 300f;   // degrees a second somebody turns on the spot at

        public static IModLogger Logger;
        public static CityLifeDirector Instance { get; private set; }

        private readonly List<Running> _running = new List<Running>(MaxSites);
        private readonly List<CityEntry> _possible = new List<CityEntry>(16);
        private readonly List<string> _recent = new List<string>(4);
        private readonly CitySite _site = new CitySite();
        private readonly int[] _testNext = new int[CityCatalogue.Families];
        private readonly List<Moment> _moments = new List<Moment>(8);
        private readonly List<Facing> _facing = new List<Facing>(8);
        private CityPeople _people;
        private CityProps _props;
        private CityEntry _retry;      // the site that last could not start for want of a look: tried again first
        private StallActivity _cart;   // the cart that is out, while the player stands at its counter; else null
        private float _nextTick;
        private float _nextSpawn;
        private float _nextWarm;
        private bool _warm;
        private bool _failed;
        private bool _toldNotReady;

        // For features that put one of its people out themselves (Scripts/Leisure): the same pool and props, never past them.
        public CityPeople People => _people;
        public CityProps Props => _props;

        /// <summary>True while the catalogue entry of this id is out on the street.</summary>
        public bool IsOut(string entryId)
        {
            for (int i = 0; i < _running.Count; i++)
            {
                if (_running[i].Entry != null && _running[i].Entry.Id == entryId)
                {
                    return true;
                }
            }
            return false;
        }

        private void Awake()
        {
            Instance = this;
            _props = new CityProps(transform);
            _people = gameObject.AddComponent<CityPeople>();
            _people.Setup(_props);
            _people.Thrifty = true;   // no look is built in the frame a site is set up
            gameObject.AddComponent<ActivityAnimators>();
            ActivityAnimators.Fault = error =>
            {
                Logger?.Error(error);
                Logger?.Warn("City life: the clip player faulted and is switched off for this session; people keep to the game's own animations.");
            };
            _nextSpawn = Time.time + 20f;   // the city settles first
            _nextWarm = Time.time + 8f;
        }

        /// <summary>
        /// Called once behind the loading screen: the pool's eight are made now, so none of it is paid for in play; failing
        /// that they are made a person at a time once the city runs.
        /// </summary>
        public void Prewarm()
        {
            if (!Settings.CityLife || _failed)
            {
                return;
            }
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                Vector3 near = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;
                while (_people.Warm(near + Vector3.down * 40f))
                {
                }
                _warm = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void OnDestroy()
        {
            EndAll();
            ActivityAnimators.ReleaseAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("CityLife: CityLifeDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            float now = Time.time;
            try
            {
                if (_cart != null && Keys.Down(KeyAction.Interact))
                {
                    _cart.Buy(now);
                }
                if (_facing.Count > 0)
                {
                    Turn(Time.deltaTime);
                }
                if (_moments.Count > 0)
                {
                    Moments(now);
                }
                if (now < _nextTick)
                {
                    return;
                }
                _nextTick = now + TickInterval;
                Tick(now);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>One of the living city's people was knocked down: their site is told.</summary>
        public static void Disturb(CityPerson person)
        {
            CityLifeDirector director = Instance;
            if (director == null)
            {
                return;
            }
            for (int i = 0; i < director._running.Count; i++)
            {
                if (director._running[i].Entry.Activity.Has(person))
                {
                    director._running[i].Entry.Activity.Disturbed();
                }
            }
        }

        /// <summary>
        /// Asks for <see cref="ICityActivity.Moment"/> on an activity in so many seconds, for the instant in a clip when
        /// something changes hands; dropped if the activity has ended by then.
        /// </summary>
        public static void After(float seconds, ICityActivity owner, int what, CityPerson who)
        {
            CityLifeDirector director = Instance;
            if (director != null && !director._failed && owner != null)
            {
                director._moments.Add(new Moment { At = Time.time + seconds, Owner = owner, What = what, Who = who });
            }
        }

        /// <summary>Somebody turns on the spot to face a point over a moment. They must be standing.</summary>
        public static void Face(CityPerson person, Vector3 point)
        {
            CityLifeDirector director = Instance;
            if (director == null || director._failed || person == null || person.Lost)
            {
                return;
            }
            Vector3 flat = point - person.Position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
            {
                return;
            }
            StopFacing(person);
            director._facing.Add(new Facing { Who = person, To = Quaternion.LookRotation(flat) });
        }

        public static void StopFacing(CityPerson person)
        {
            CityLifeDirector director = Instance;
            if (director == null)
            {
                return;
            }
            for (int i = director._facing.Count - 1; i >= 0; i--)
            {
                if (director._facing[i].Who == person)
                {
                    director._facing.RemoveAt(i);
                }
            }
        }

        private void Turn(float delta)
        {
            for (int i = _facing.Count - 1; i >= 0; i--)
            {
                Facing facing = _facing[i];
                if (facing.Who.Lost)
                {
                    _facing.RemoveAt(i);
                    continue;
                }
                Transform body = facing.Who.Character.transform;
                body.rotation = Quaternion.RotateTowards(body.rotation, facing.To, TurnRate * delta);
                if (Quaternion.Angle(body.rotation, facing.To) < 0.5f)
                {
                    _facing.RemoveAt(i);
                }
            }
        }

        private void Moments(float now)
        {
            // In the order they were asked for: a long frame can bring several due at once, and a cup must be
            // picked up before it is put down.
            while (true)
            {
                int first = -1;
                for (int i = 0; i < _moments.Count; i++)
                {
                    if (now >= _moments[i].At && (first < 0 || _moments[i].At < _moments[first].At))
                    {
                        first = i;
                    }
                }
                if (first < 0)
                {
                    return;
                }
                Moment moment = _moments[first];
                _moments.RemoveAt(first);
                if (Out(moment.Owner))
                {
                    moment.Owner.Moment(moment.What, moment.Who, now);
                }
            }
        }

        private bool Out(ICityActivity activity)
        {
            for (int i = 0; i < _running.Count; i++)
            {
                if (_running[i].Entry.Activity == activity)
                {
                    return true;
                }
            }
            return false;
        }

        private void Tick(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (!Settings.CityLife || !ModState.Loaded || player == null || uis == null || LoadScene.isLoading || BuildingManager.IsInsideBuilding || uis.timeMachine.isRunning)
            {
                EndAll();   // the street is gone, or is about to jump hours ahead, or the player does not want this
                return;
            }
            if (uis.gameSpeed.Paused)
            {
                return;
            }
            Vector3 origin = player.transform.position;

            if (!_warm && now >= _nextWarm)
            {
                _nextWarm = now + WarmInterval;
                _warm = !_people.Warm(origin + Vector3.down * 40f);
                if (_warm)
                {
                    Logger?.Info($"City life: its {_people.Count} people are ready (a pool of its own; the accident and crime scenes keep their six).");
                }
            }

            bool raining = RainHelper.isRaining;
            _cart = null;
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                Running running = _running[i];
                ICityActivity activity = running.Entry.Activity;
                bool alive = activity.Tick(now);
                float sqr = CitySpots.Flat(activity.Centre - origin);
                bool weather = !running.Test && ((raining && !running.Entry.Rain) || (!raining && running.Entry.RainOnly));
                bool stale = now >= running.EndsAt || weather || (!running.Test && !CityCatalogue.Enabled(running.Entry.Family));
                if (!alive || sqr > TooFar * TooFar || (stale && !CitySpots.InView(activity.Centre)))
                {
                    string ended = !alive ? "finished" : sqr > TooFar * TooFar ? "left behind" : weather ? (raining ? "rain" : "the rain stopped") : "time up";
                    Logger?.Info($"City life: {running.Entry.Id} cleared ({ended}, {Mathf.Sqrt(sqr):F0} m away).");
                    activity.End();
                    _running.RemoveAt(i);
                    for (int k = _moments.Count - 1; k >= 0; k--)
                    {
                        if (_moments[k].Owner == activity)
                        {
                            _moments.RemoveAt(k);   // what it was waiting for will not happen now
                        }
                    }
                }
                else if (activity is StallActivity cart && cart.PlayerAtCounter)
                {
                    _cart = cart;
                }
            }

            if (now >= _nextSpawn)
            {
                _nextSpawn = now + SpawnInterval;
                if (Ready() && _running.Count < Wanted(TimeHelper.CurrentHour) && !TrafficAccidents.IsActive && !StreetCrime.IsActive
                    && !Events.Weather.TakeCover)   // nobody new comes out in a thunderstorm
                {
                    TryStart(origin, now, raining);
                }
            }
        }

        // How many sites the hour and the density setting call for.
        private static int Wanted(int hour)
        {
            if (hour >= 2 && hour < 5)
            {
                return 0;   // the dead of night
            }
            int byDensity = Settings.CityDensity <= 3 ? 1 : Settings.CityDensity <= 7 ? 2 : MaxSites;
            bool quiet = hour < 7 || hour >= 22;
            return quiet ? Mathf.Min(1, byDensity) : byDensity;
        }

        private bool Ready()
        {
            if (ClipLibrary.Ready && PropLibrary.Ready && ActivityAnimators.Working && _people.Count > 0)
            {
                return true;
            }
            if (!_toldNotReady && Time.time > 90f && _people.Count > 0)
            {
                _toldNotReady = true;
                Logger?.Warn($"City life: nothing can start: clips ready {ClipLibrary.Ready}, props ready {PropLibrary.Ready}, clip player working {ActivityAnimators.Working}. (Logged once.)");
            }
            return false;
        }

        private void TryStart(Vector3 origin, float now, bool raining)
        {
            // A higher density also means the gap between two sites closing sooner.
            if (Random.value > 0.25f + Settings.CityDensity * 0.075f)
            {
                return;
            }
            // A site that last failed for want of a ready look is tried again first: the look is ready by now.
            CityEntry entry = _retry;
            _retry = null;
            int hour = TimeHelper.CurrentHour;
            if (entry == null || entry.Cast > _people.Free || !entry.OpenAt(hour) || (raining ? !entry.Rain : entry.RainOnly) || !CityCatalogue.Enabled(entry.Family) || Out(entry))
            {
                entry = Deal(hour, raining);
            }
            if (entry == null)
            {
                return;
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (CitySpots.PavementRing(origin, NearRing, FarRing, true, out Vector3 anchor) && Open(entry, anchor, origin, now, raining, false))
                {
                    return;
                }
            }
        }

        // One of what is possible now, by weight, and not one of the last two dealt.
        private CityEntry Deal(int hour, bool raining)
        {
            _possible.Clear();
            int total = 0;
            int free = _people.Free;
            foreach (CityEntry entry in CityCatalogue.All)
            {
                if (entry.Cast <= free && entry.OpenAt(hour) && (raining ? entry.Rain : !entry.RainOnly) && CityCatalogue.Enabled(entry.Family) && !_recent.Contains(entry.Id) && !Out(entry))
                {
                    _possible.Add(entry);
                    total += entry.Weight;
                }
            }
            if (total <= 0)
            {
                _recent.Clear();   // everything possible was dealt lately: start over next time
                return null;
            }
            int roll = Random.Range(0, total);
            for (int i = 0; i < _possible.Count; i++)
            {
                roll -= _possible[i].Weight;
                if (roll < 0)
                {
                    return _possible[i];
                }
            }
            return _possible[_possible.Count - 1];
        }

        private bool Open(CityEntry entry, Vector3 anchor, Vector3 origin, float now, bool raining, bool test)
        {
            using var piece = SessionWatch.Piece("city life: a site being set up");
            ICityActivity activity = entry.Activity;
            _site.Anchor = anchor;
            _site.Test = test;
            _site.Hour = TimeHelper.CurrentHour;
            _site.Raining = raining;
            _site.Player = origin;
            _site.People = _people;
            _site.Props = _props;
            _site.Refusal = CityRefusal.None;
            if (!activity.Find(_site))
            {
                return false;
            }
            Vector3 centre = activity.Centre;
            if (!test && CitySpots.InView(centre))
            {
                return false;   // room was found, but somewhere the camera can see
            }
            for (int i = 0; i < _running.Count; i++)
            {
                if (CitySpots.Flat(_running[i].Entry.Activity.Centre - centre) < SiteGap * SiteGap)
                {
                    return false;
                }
            }
            _people.AtNeed = test;   // a test button cannot wait for a look to be made ready
            _people.Refused = false;
            bool begun = activity.Begin(_site);
            _people.AtNeed = false;
            if (!begun)
            {
                if (_people.Refused && !test)
                {
                    _retry = entry;
                }
                if (_site.Refusal == CityRefusal.None)
                {
                    _site.Refusal = _people.Free < entry.Cast ? CityRefusal.NoPeople : CityRefusal.NoProp;
                }
                return false;
            }
            _running.Add(new Running { Entry = entry, Test = test, EndsAt = now + Random.Range(entry.MinMinutes, entry.MaxMinutes) * 60f });
            _recent.Add(entry.Id);
            while (_recent.Count > RecentMemory)
            {
                _recent.RemoveAt(0);
            }
            Logger?.Info($"City life: {entry.Id} started {Mathf.Sqrt(CitySpots.Flat(centre - origin)):F0} m away{(test ? " (test)" : "")}; "
                         + $"{_running.Count} of {MaxSites} sites out, {_people.Count - _people.Free} of {_people.Count} people.");
            return true;
        }

        private bool Out(CityEntry entry)
        {
            for (int i = 0; i < _running.Count; i++)
            {
                if (_running[i].Entry == entry)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Everybody and everything off the street, now.</summary>
        public void EndAll()
        {
            _cart = null;
            _moments.Clear();
            _facing.Clear();
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                try
                {
                    _running[i].Entry.Activity.End();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _running.Clear();
            if (_people != null)
            {
                _people.ReleaseAll();
            }
        }

        private void Fail(Exception error)
        {
            _failed = true;
            Logger?.Error(error);
            Logger?.Warn("City life: switched off for this session after the error above; the street has been cleared.");
            try
            {
                EndAll();
                ActivityAnimators.ReleaseAll();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
