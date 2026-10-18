using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Who counts as a person on the street besides the walking pedestrians: the game's standing and sitting figures, listed
    /// once a second, while somebody asks, from the few pool parents they live under. No scene search and no physics.
    /// </summary>
    public static class StreetPeople
    {
        private const float RefreshEvery = 1f;   // seconds

        private static readonly List<BaseHuman> People = new List<BaseHuman>(64);
        private static readonly List<Transform> Pools = new List<Transform>(8);
        private static readonly Dictionary<int, BaseHuman> Known = new Dictionary<int, BaseHuman>(128);
        private static Transform _root;      // the parent of every pool's parent
        private static int _classified;      // how many pools there were when they were last told apart; -1 to do it again
        private static float _next;
        private static bool _logged;

        /// <summary>
        /// The standing and sitting street people out now. The list is the registry's own: read it, never keep or change
        /// it, and check activeInHierarchy before use, as the game can switch an entry off at any moment.
        /// </summary>
        public static List<BaseHuman> Standing(IModLogger logger)
        {
            float now = Time.time;
            if (now >= _next || now < _next - RefreshEvery)
            {
                _next = now + RefreshEvery;
                Refresh(logger);
            }
            return People;
        }

        private static void Refresh(IModLogger logger)
        {
            People.Clear();
            if (_root == null && !FindRoot())
            {
                return;
            }
            if (_classified != _root.childCount)
            {
                Classify(logger);
            }
            int live = 0;
            for (int p = 0; p < Pools.Count; p++)
            {
                live += Pools[p] != null ? Pools[p].childCount : 0;
            }
            if (CacheRules.Stale(Known.Count, live))
            {
                Known.Clear();   // the pools destroyed people since: what is still out is looked up again below
            }
            for (int p = 0; p < Pools.Count; p++)
            {
                Transform pool = Pools[p];
                if (pool == null)
                {
                    _classified = -1;
                    continue;
                }
                for (int i = pool.childCount - 1; i >= 0; i--)
                {
                    Transform child = pool.GetChild(i);
                    if (!child.gameObject.activeSelf)
                    {
                        continue;
                    }
                    int id = child.GetInstanceID();
                    if (!Known.TryGetValue(id, out BaseHuman human))
                    {
                        human = Human(child);
                        Known[id] = human;
                    }
                    if (human != null)
                    {
                        People.Add(human);
                    }
                }
            }
        }

        // The pools' common parent, reached from any pedestrian the game has out: no search of the scene.
        private static bool FindRoot()
        {
            Pools.Clear();
            Known.Clear();
            _classified = -1;
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return false;
            }
            for (int i = 0; i < walkers.Count; i++)
            {
                Transform pool = walkers[i] != null ? walkers[i].transform.parent : null;
                if (pool != null && pool.parent != null)
                {
                    _root = pool.parent;
                    return true;
                }
            }
            return false;
        }

        // Which pools hold street people. A pool with nothing in it yet is looked at again next time.
        private static void Classify(IModLogger logger)
        {
            Pools.Clear();
            bool allKnown = true;
            for (int i = 0; i < _root.childCount; i++)
            {
                Transform pool = _root.GetChild(i);
                if (pool.childCount == 0)
                {
                    allKnown = false;
                    continue;
                }
                if (IsStreetPerson(pool.GetChild(0)))
                {
                    Pools.Add(pool);
                }
            }
            _classified = allKnown ? _root.childCount : -1;
            if (!_logged && Pools.Count > 0)
            {
                _logged = true;
                var names = new StringBuilder(96);
                for (int i = 0; i < Pools.Count; i++)
                {
                    names.Append(i > 0 ? ", " : string.Empty).Append(Pools[i].name);
                }
                logger?.Info("Street people: besides the walking pedestrians, the standing and sitting people of " + Pools.Count
                             + " of the game's pools count as people on the street (" + names + ").");
            }
        }

        // A hangout-spot figure, a performer, someone homeless, or the plain figure the game sits on benches and at tables.
        private static bool IsStreetPerson(Transform member)
        {
            if (member.GetComponent<Pedestrian>() != null)
            {
                return false;   // the walkers: the game's own list has them
            }
            if (member.GetComponent<StationaryAiBehavior>() != null || member.GetComponent<Homeless>() != null || member.GetComponent<StreetPerformer>() != null)
            {
                return true;
            }
            BaseHuman human = member.GetComponent<BaseHuman>();
            return human != null && human.GetType() == typeof(BaseHuman);
        }

        private static BaseHuman Human(Transform member)
        {
            BaseHuman human = member.GetComponent<BaseHuman>();
            return human != null ? human : member.GetComponentInChildren<BaseHuman>(true);
        }
    }
}
