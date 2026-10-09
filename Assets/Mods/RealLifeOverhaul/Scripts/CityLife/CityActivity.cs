using System.Collections.Generic;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Why an activity could not be put where it was asked for. A test button says exactly this
    /// (one sentence each in en.json); never one sentence for several causes.
    /// </summary>
    public enum CityRefusal
    {
        None,
        OffPavement,    // no pavement, or not enough of it, at the places tried
        Blocked,        // something solid is where a person or a prop would stand
        NearDoor,       // too close to something the player can use
        NoWall,         // no building wall within reach
        UnevenWall,     // walls, but none flat with pavement in front
        NoPeople,       // the pool has not enough people free
        NoProp,         // a clip or a prop it needs is not in the bundles
        NoParkedCar,    // it needs a parked car beside the pavement
        NoTree,         // it needs a street tree
        NoPerformer,    // it needs one of the game's street performers
        NoWayIn,        // nowhere out of view to walk in from, or no path from there
        NotRaining      // it only happens in the rain
    }

    public static class CityRefusals
    {
        public static string Key(CityRefusal why)
        {
            switch (why)
            {
                case CityRefusal.Blocked: return "reallife_city_refused_blocked";
                case CityRefusal.NearDoor: return "reallife_city_refused_door";
                case CityRefusal.NoWall: return "reallife_city_refused_no_wall";
                case CityRefusal.UnevenWall: return "reallife_city_refused_uneven_wall";
                case CityRefusal.NoPeople: return "reallife_city_refused_no_people";
                case CityRefusal.NoProp: return "reallife_city_refused_no_prop";
                case CityRefusal.NoParkedCar: return "reallife_city_refused_no_car";
                case CityRefusal.NoTree: return "reallife_city_refused_no_tree";
                case CityRefusal.NoPerformer: return "reallife_city_refused_no_performer";
                case CityRefusal.NoWayIn: return "reallife_city_refused_no_way_in";
                case CityRefusal.NotRaining: return "reallife_city_refused_not_raining";
                default: return "reallife_city_refused_pavement";
            }
        }
    }

    /// <summary>What the director hands an activity when it asks it to find room and begin.</summary>
    public sealed class CitySite
    {
        /// <summary>A point of pavement the director proposes: out of view, a walk from the player (or, for a test, just ahead of them).</summary>
        public Vector3 Anchor;

        /// <summary>Asked for by a test button: appearing in view is allowed, and the usual hours and weather do not apply.</summary>
        public bool Test;

        public int Hour;
        public bool Raining;
        public Vector3 Player;
        public CityPeople People;
        public CityProps Props;

        /// <summary>Set by an activity that turns a place down for a reason of its own (no parked car, no tree): what a test then says.</summary>
        public CityRefusal Refusal;
    }

    /// <summary>
    /// One thing happening on the street. The four calls every activity answers:
    /// find room, begin, tick, end.
    /// </summary>
    public interface ICityActivity
    {
        /// <summary>
        /// Looks for a place for every person and every prop near the site's anchor and remembers
        /// them. Puts nothing on the street. False if there is no room here: the activity then
        /// does not happen here.
        /// </summary>
        bool Find(CitySite site);

        /// <summary>Puts people and props where Find found room. False, having given everything back, if it could not.</summary>
        bool Begin(CitySite site);

        /// <summary>Once a second while the activity is out. False when it has nothing more to do.</summary>
        bool Tick(float now);

        /// <summary>Everything given back: people to the pool, props to the shelf. Safe in any state, any number of times.</summary>
        void End();

        /// <summary>Where it is, for the director's distance and view tests.</summary>
        Vector3 Centre { get; }

        /// <summary>True if this person is one of the activity's.</summary>
        bool Has(CityPerson person);

        /// <summary>One of its people was knocked down by a car. The rest carry on until nobody is looking, then it ends.</summary>
        void Disturbed();

        /// <summary>A moment it asked for with CityLifeDirector.After has come (the instant inside a clip when something changes hands).</summary>
        void Moment(int what, CityPerson who, float now);
    }

    /// <summary>
    /// What every activity shares: its people, the props it stood on the ground, giving all of
    /// it back, and the small questions most of them ask (is somebody walking past, how long
    /// until the next change). An activity derives from this, fills in Find, Begin and Tick, and
    /// has no other duties.
    /// </summary>
    public abstract class CityActivity : ICityActivity
    {
        protected readonly List<CityPerson> Cast = new List<CityPerson>(4);
        protected readonly List<Transform> Dressing = new List<Transform>(8);
        protected CityPeople People;
        protected CityProps Props;
        protected bool Over;   // set when it should end as soon as nobody is looking

        public Vector3 Centre { get; protected set; }

        public abstract bool Find(CitySite site);

        public abstract bool Begin(CitySite site);

        public abstract bool Tick(float now);

        public virtual void End()
        {
            for (int i = 0; i < Cast.Count; i++)
            {
                People?.Release(Cast[i]);
            }
            Cast.Clear();
            _leaving.Clear();
            _leavingTries.Clear();
            for (int i = 0; i < Dressing.Count; i++)
            {
                Props?.Return(Dressing[i]);
            }
            Dressing.Clear();
            // Tools dropped here by people who were knocked down.
            if (People != null && Props != null)
            {
                List<Transform> fallen = People.Fallen;
                for (int i = fallen.Count - 1; i >= 0; i--)
                {
                    if (fallen[i] == null || CitySpots.Flat(fallen[i].position - Centre) < 12f * 12f)
                    {
                        Props.Return(fallen[i]);
                        fallen.RemoveAt(i);
                    }
                }
            }
            Over = false;
        }

        public bool Has(CityPerson person)
        {
            return Cast.Contains(person);
        }

        public virtual void Disturbed()
        {
            Over = true;
            // What they dropped is this activity's to clear up, wherever it fell (somebody walking in may be far from the site).
            if (People != null)
            {
                List<Transform> fallen = People.Fallen;
                for (int i = 0; i < fallen.Count; i++)
                {
                    if (fallen[i] != null && !Dressing.Contains(fallen[i]))
                    {
                        Dressing.Add(fallen[i]);
                    }
                }
                fallen.Clear();
            }
        }

        public virtual void Moment(int what, CityPerson who, float now)
        {
        }

        // ---------------------------------------------------------------- coming and going on foot

        private const float ArriveNear = 24f;      // people who walk in are put on the pavement between these distances, out of view
        private const float ArriveFar = 46f;
        private const float DepartNear = 38f;
        private const float DepartFar = 70f;
        private const int DepartTries = 4;

        private readonly List<CityPerson> _leaving = new List<CityPerson>(4);
        private readonly List<int> _leavingTries = new List<int>(4);

        /// <summary>
        /// One more person, put on the pavement out of view a short walk away and already walking
        /// to a place. Null if the pool has nobody, there is nowhere out of view to come from, or
        /// no path from there (the site's Refusal says which).
        /// </summary>
        protected CityPerson Arrive(CitySite site, CityLook look, Vector3 goal)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                // Out of view, clear, and with a whole path to where they are going: all asked before anybody is taken from the pool.
                if (!CitySpots.PavementRing(goal, ArriveNear, ArriveFar, true, out Vector3 from) || !CitySpots.ClearQuietly(from, 0.4f) || !CitySpots.Reachable(from, goal))
                {
                    continue;
                }
                CityPerson person = (site != null ? site.People : People).Take(look, from, goal, false);
                if (person == null)
                {
                    if (site != null)
                    {
                        site.Refusal = CityRefusal.NoPeople;
                    }
                    return null;
                }
                if (person.WalkTo(goal))
                {
                    Cast.Add(person);
                    return person;
                }
                (site != null ? site.People : People).Release(person);
            }
            if (site != null && site.Refusal == CityRefusal.None)
            {
                site.Refusal = CityRefusal.NoWayIn;
            }
            return null;
        }

        /// <summary>
        /// Somebody walks off: to a point of pavement out of view, where they are taken back into
        /// the pool. They stay one of the cast (and are given back with it) until then.
        /// </summary>
        protected void Depart(CityPerson person)
        {
            if (person == null || person.Lost || _leaving.Contains(person))
            {
                return;
            }
            _leaving.Add(person);
            _leavingTries.Add(0);
            SendOff(person);
        }

        protected bool Leaving(CityPerson person)
        {
            return _leaving.Contains(person);
        }

        /// <summary>Call from Tick: takes back whoever has walked out of view, and sends on anyone who arrived where they can still be seen.</summary>
        protected void TickDepartures()
        {
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                CityPerson person = _leaving[i];
                bool gone = person.Lost;
                if (!gone && !CitySpots.InView(person.Position) && (person.Arrived || CitySpots.Flat(person.Position - Centre) > DepartNear * DepartNear))
                {
                    Cast.Remove(person);
                    People.Release(person);
                    gone = true;
                }
                else if (!gone && person.Arrived)
                {
                    if (++_leavingTries[i] > DepartTries)
                    {
                        continue;   // still in view after several walks: they stand until the site itself goes
                    }
                    SendOff(person);
                }
                if (gone)
                {
                    _leaving.RemoveAt(i);
                    _leavingTries.RemoveAt(i);
                }
            }
        }

        private void SendOff(CityPerson person)
        {
            if (CitySpots.PavementRing(person.Position, DepartNear, DepartFar, true, out Vector3 away) || CitySpots.PavementRing(person.Position, DepartNear, DepartFar, false, out away))
            {
                person.WalkTo(away);
            }
        }

        /// <summary>Remembers the pool and the shelf; every Begin starts with this.</summary>
        protected void Start(CitySite site)
        {
            People = site.People;
            Props = site.Props;
            Over = false;
        }

        /// <summary>One more person at a spot, facing a point. Null if the pool has nobody or the spot will not take them.</summary>
        protected CityPerson Add(CityLook look, Vector3 spot, Vector3 face)
        {
            CityPerson person = People.Take(look, spot, face);
            if (person != null)
            {
                Cast.Add(person);
            }
            return person;
        }

        /// <summary>Stands a prop on the ground. False if the prop library cannot supply it.</summary>
        protected bool Put(string id, Vector3 position, float heading)
        {
            Transform prop = Props.Stand(id, position, heading);
            if (prop == null)
            {
                return false;
            }
            Dressing.Add(prop);
            return true;
        }

        /// <summary>
        /// What Tick returns for an activity whose people simply carry on: true until one of
        /// them is lost or it has been told to end, and after that only while someone is looking.
        /// </summary>
        protected bool Lasting()
        {
            for (int i = 0; i < Cast.Count; i++)
            {
                if (Cast[i].Lost)
                {
                    Over = true;
                }
            }
            return !Over || CitySpots.InView(Centre);
        }

        /// <summary>
        /// True if someone on foot (one of the game's pedestrians, or the player) is in front of
        /// a person, within a distance and a cone: somebody to hold a leaflet out to.
        /// </summary>
        protected static bool SomeoneAhead(CityPerson person, float distance, float cosine)
        {
            Vector3 from = person.Position;
            Vector3 forward = person.Forward;
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (player != null && !Helpers.PlayerHelper.IsUsingVehicle && Ahead(player.transform.position - from, forward, distance, cosine))
            {
                return true;
            }
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return false;
            }
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker != null && walker.isActiveAndEnabled && Ahead(walker.transform.position - from, forward, distance, cosine))
                {
                    return true;
                }
            }
            return false;
        }

        protected static float In(float min, float max)
        {
            return Time.time + Random.Range(min, max);
        }

        private static bool Ahead(Vector3 offset, Vector3 forward, float distance, float cosine)
        {
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            return sqr < distance * distance && sqr > 0.25f && Vector3.Dot(offset, forward) > cosine * Mathf.Sqrt(sqr);
        }
    }
}
