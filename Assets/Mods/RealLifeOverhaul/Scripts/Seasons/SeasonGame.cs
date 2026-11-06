using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using Helpers;
using Localizor;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The only part of the seasons that touches the game: its clock, the sales ratio of each seasonal good (an item of the
    /// game's in memory, never in a save, written once a season and put back exactly), the place by the player where the
    /// rain falls, and the paces the city's people are given as they set off (narrowed on snow and put back).
    /// </summary>
    public static partial class SeasonGame
    {
        private const float WalkLeast = 0.6f;   // m/s: no walker is held slower than this, however deep the snow

        // Each good's own ratio as the game had it, and what was written over it; both by the good's place in SeasonRules.Goods.
        private static readonly Item[] Items = new Item[SeasonRules.GoodCount];
        private static readonly float[] Own = new float[SeasonRules.GoodCount];
        private static readonly float[] Written = new float[SeasonRules.GoodCount];
        private static readonly bool[] Held = new bool[SeasonRules.GoodCount];

        // The walkers' pace ranges as the game had them, while they are narrowed.
        private static List<PedestrianPool.AnimatorOverride> _paces;
        private static Vector2[] _pacesOwn;
        private static float _walkApplied = 1f;
        private static bool _walkFailed;

        private static readonly Collider[] Near = new Collider[128];   // the test's sample of the cars and people round the player

        public static int Today => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        /// <summary>The hour of the day with the minutes as a fraction, 0 to 24.</summary>
        public static float Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour + SaveGameManager.Current.Minute / 60f : 0f;

        public static int WholeHour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;

        /// <summary>No city to read: no save, a load under way, or the mod's state not read yet.</summary>
        public static bool Loading()
        {
            return SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded;
        }

        /// <summary>The player is out of doors, where snow is seen.</summary>
        public static bool Outdoors => !BuildingManager.IsInsideBuilding;

        public static Vector3 Player => PlayerHelper.GetPosition();

        /// <summary>A good's name in the player's language, as the game writes it.</summary>
        public static string Label(int good)
        {
            return good >= 0 && good < SeasonRules.GoodCount ? SeasonRules.Goods[good].GetLocalization() : string.Empty;
        }

        /// <summary>
        /// Writes each seasonal good's sales ratio for a season: the game's own times the season's figure. A ratio someone
        /// else changed since it was last written is taken as the good's own from then on. Returns the goods written.
        /// </summary>
        public static int ApplyDemand(int season)
        {
            int written = 0;
            for (int g = 0; g < SeasonRules.GoodCount; g++)
            {
                Item item = Items[g] != null ? Items[g] : Items[g] = ItemsGetter.GetByName(SeasonRules.Goods[g], true);
                if (item == null)
                {
                    continue;   // a good this build of the game does not have
                }
                if (!Held[g] || !Mathf.Approximately(item.productSalesRatio, Written[g]))
                {
                    Own[g] = item.productSalesRatio;
                    Held[g] = true;
                }
                Written[g] = SeasonRules.Ratio(Own[g], g, season);
                item.productSalesRatio = Written[g];
                written++;
            }
            return written;
        }

        /// <summary>Every good's own ratio back, where it is still the one written. Safe at any time.</summary>
        public static void RestoreDemand()
        {
            for (int g = 0; g < SeasonRules.GoodCount; g++)
            {
                if (Held[g] && Items[g] != null && Mathf.Approximately(Items[g].productSalesRatio, Written[g]))
                {
                    Items[g].productSalesRatio = Own[g];
                }
                Held[g] = false;
                Items[g] = null;
            }
        }

        /// <summary>A good's ratio as the game had it and as it is now; false for a good not held.</summary>
        public static bool Ratios(int good, out float own, out float now)
        {
            own = now = 0f;
            if (good < 0 || good >= SeasonRules.GoodCount || !Held[good] || Items[good] == null)
            {
                return false;
            }
            own = Own[good];
            now = Items[good].productSalesRatio;
            return true;
        }

        /// <summary>
        /// The renderers of the cars and people within <paramref name="radius"/> of the player, and the player's own, into
        /// <paramref name="into"/>; returns how many cars and people they belong to. One physics query: for a test, once.
        /// </summary>
        public static int Bodies(float radius, List<Renderer> into)
        {
            into.Clear();
            var roots = new HashSet<Transform>();
            var some = new List<Renderer>(64);
            PlayerController player = PlayerHelper.PlayerController;
            if (player != null && roots.Add(player.transform))
            {
                player.GetComponentsInChildren(true, some);
                into.AddRange(some);
            }
            int mask = LayerHelper.vehicleAndHumanConflictMask | LayerHelper.freeParkingSpotDetectionMask | (LayerHelper.PlayerLayerIndex >= 0 ? 1 << LayerHelper.PlayerLayerIndex : 0);
            int count = Physics.OverlapSphereNonAlloc(Player, radius, Near, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Transform root = Near[i].attachedRigidbody != null ? Near[i].attachedRigidbody.transform : Near[i].transform;
                if (roots.Add(root))
                {
                    root.GetComponentsInChildren(true, some);
                    into.AddRange(some);
                }
            }
            return roots.Count;
        }

        /// <summary>The place the game's rain falls around the player; null if it cannot be read (snow does not fall then).</summary>
        public static Transform RainPlace()
        {
            FieldInfo field = GameContract.RainPlace.Field;
            var rain = field != null ? field.GetValue(null) as Component : null;
            return rain != null ? rain.transform : null;
        }

        /// <summary>
        /// The city's people set off at <paramref name="factor"/> of the paces the game gives them (1: its own). Those already
        /// walking keep theirs. False while there is no city to slow, so the caller asks again.
        /// </summary>
        public static bool Walkers(float factor)
        {
            return _walkFailed || Mathf.Approximately(factor, _walkApplied) || Narrow(factor);
        }

        // The paces put back and narrowed again for a new factor: a few times a snow day.
        private static bool Narrow(float factor)
        {
            try
            {
                RestoreWalkers();
                if (Mathf.Approximately(factor, 1f))
                {
                    return true;
                }
                var city = InstanceBehavior<CityManager>.Instance;
                PedestrianPool pool = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.pedestrianPool : null;
                FieldInfo field = GameContract.WalkerPaces.Field;
                var paces = pool != null && field != null ? field.GetValue(pool) as List<PedestrianPool.AnimatorOverride> : null;
                if (paces == null)
                {
                    return false;
                }
                _paces = paces;
                _pacesOwn = new Vector2[paces.Count];
                for (int i = 0; i < paces.Count; i++)
                {
                    if (paces[i] == null)
                    {
                        continue;
                    }
                    Vector2 own = paces[i].moveSpeed;
                    _pacesOwn[i] = own;
                    paces[i].moveSpeed = new Vector2(Mathf.Max(WalkLeast, own.x * factor), Mathf.Max(WalkLeast, own.y * factor));
                }
                _walkApplied = factor;
                return true;
            }
            catch (Exception e)
            {
                _walkFailed = true;
                SeasonDirector.Logger?.Error(e);
                SeasonDirector.Logger?.Warn("Seasons: the city's people's paces could not be read; snow no longer slows them this session.");
                RestoreWalkers();
                return true;
            }
        }

        /// <summary>The paces narrowed so far, as a factor (1: the game's own).</summary>
        public static float WalkApplied => _walkApplied;

        /// <summary>The walkers' paces back as the game had them. Safe at any time.</summary>
        public static void RestoreWalkers()
        {
            List<PedestrianPool.AnimatorOverride> paces = _paces;
            for (int i = 0; paces != null && _pacesOwn != null && i < paces.Count && i < _pacesOwn.Length; i++)
            {
                if (paces[i] != null)
                {
                    paces[i].moveSpeed = _pacesOwn[i];
                }
            }
            _paces = null;
            _pacesOwn = null;
            _walkApplied = 1f;
        }

        /// <summary>The city is unloading: everything back, and the items let go (the next city has its own).</summary>
        public static void Reset()
        {
            RestoreDemand();
            RestoreWalkers();
            RestoreLooks();
            UnhushRain();
            _walkFailed = false;
        }
    }
}
