using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BAModAPI;
using Buildings.Outdoors;
using Culling;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the game's culling lists (Culling.CullingManager's two CullingGroupControllers) free of what the mod destroys
    /// (docs/standards/performance.md, RLO-PERF-023). The game's street lamps, trees, benches, building signs, people's
    /// zones and the like register in a list when they start, and the game makes its culling group from that list again,
    /// asking each entry for its sphere, whenever one unregisters. While a city unloads their own OnDestroy leaves them in
    /// the list, and some kinds never take themselves out at all. So a copy of the game's scenery the mod destroyed stayed
    /// listed: at the city's unload the mod's own unregistering of a people's zone asked for a rebuild, which met Uptown's
    /// destroyed lamp posts and threw every frame until the city's scene went (0.61.0 beta 6, and 0.60.0), and pieces the
    /// mod took away in the frame the game first made its group shrank the list under it, so its first cull read past the
    /// end (ArgumentOutOfRangeException in OnCullingGroupStateChanged at every load with Uptown's garage mouths).
    /// <see cref="Forget"/> takes every cullable at or under an object out of both lists before the mod destroys it
    /// (<see cref="SceneryTrees.Forget"/>, <see cref="SceneryTrees.Destroy"/> and <see cref="SceneryTrees.DestroyNow"/> call
    /// it, so every path of the mod that destroys a copy of the game's scenery goes through it): one pass over the list that
    /// moves the group's spheres in step and sets its count at once, so no rebuild is asked for and the group never reads an
    /// entry the list no longer has. If this game keeps those fields elsewhere, the game's own UnregisterCullable is used
    /// instead, never while the city unloads. With Developer diagnostics on, the check "Culling check (unload)" counts the
    /// dead entries as the next scene comes in after the city's part of the mod unloaded, the city's scenes still there.
    /// COST: nothing per frame. Forget walks the scripts under the object once, and the lists (some ten thousand entries)
    /// only when one of them is a cullable; nothing allocated after the first use. The check: one walk of the lists, once.
    /// </summary>
    public static class SceneryCulling
    {
        public static IModLogger Logger;

        private static readonly FieldInfo ListField = GameContract.CullList.Field;
        private static readonly FieldInfo SpheresField = GameContract.CullSpheres.Field;
        private static readonly FieldInfo GroupField = GameContract.CullGroup.Field;
        private static readonly FieldInfo ReadyField = GameContract.CullReady.Field;
        private static readonly FieldInfo PendingField = GameContract.CullPending.Field;
        private static readonly FieldInfo RebuildField = GameContract.CullRebuild.Field;
        private static readonly FieldInfo MusicField = GameContract.BuildingMusic.Field;
        private static readonly FieldInfo HangoutField = GameContract.BuildingHangout.Field;
        private static readonly FieldInfo MusicOwnerField = GameContract.MusicOwner.Field;
        private static readonly FieldInfo HangoutOwnerField = GameContract.HangoutOwner.Field;

        private static readonly List<MonoBehaviour> Scripts = new List<MonoBehaviour>(64);
        private static readonly HashSet<ICullable> Going = new HashSet<ICullable>(SameObject.Instance);
        private static readonly Predicate<ICullable> IsGoing = Going.Contains;
        private static readonly Action<ICullable> Dropped = DropPending;
        private static Dictionary<ICullable, int> _pending;   // the list's pending bands while it is compacted

        private static CullingManager _city;   // the unloading city's culling, held for the check
        private static int _unloadFrame;
        private static UnityAction<Scene, LoadSceneMode> _loaded;

        /// <summary>Entries taken out of the game's culling lists since the mod started.</summary>
        public static int Forgotten { get; private set; }

        /// <summary>Of them, taken out through the game's own UnregisterCullable (this game keeps the fields elsewhere).</summary>
        public static int ByGame { get; private set; }

        /// <summary>Takes the cullables at or under <paramref name="root"/> out of the game's culling lists. Call before destroying it; null is safe. Returns how many.</summary>
        public static int Forget(GameObject root)
        {
            if (ReferenceEquals(root, null) || root == null)
            {
                return 0;
            }
            CullingManager culling = InstanceBehavior<CullingManager>.IsInitialized ? InstanceBehavior<CullingManager>.Instance : null;
            if (culling == null)
            {
                return 0;   // no city: nothing is listed
            }
            Going.Clear();
            Scripts.Clear();
            root.GetComponentsInChildren(true, Scripts);
            for (int i = 0; i < Scripts.Count; i++)
            {
                MonoBehaviour script = Scripts[i];
                if (script is ICullable cullable)
                {
                    Going.Add(cullable);
                }
                if (script is CityBuildingController building)
                {
                    AddOwned(building, MusicField);   // a building's music and crowd outside: cullables that are no component
                    AddOwned(building, HangoutField);
                }
            }
            Scripts.Clear();
            if (Going.Count == 0)
            {
                return 0;   // the common case: nothing of the lists' under it, and they are not walked
            }
            int taken = Take(culling.generalCullingGroupController) + Take(culling.hamptonsHousesCullingGroupController);
            Going.Clear();
            Forgotten += taken;
            return taken;
        }

        private static void AddOwned(CityBuildingController building, FieldInfo field)
        {
            if (field != null && field.GetValue(building) is ICullable owned)
            {
                Going.Add(owned);
            }
        }

        // One list: compacted with the group's spheres in step and the group's count set, or the game's own way.
        private static int Take(CullingGroupController controller)
        {
            if (controller == null)
            {
                return 0;
            }
            if (ListField == null || ReadyField == null || PendingField == null || !(ListField.GetValue(controller) is List<ICullable> list)
                || !(PendingField.GetValue(controller) is Dictionary<ICullable, int> pending))
            {
                return TheGamesWay(controller);
            }
            if (list.Count == 0)
            {
                return 0;
            }
            bool ready = ReadyField.GetValue(controller) is bool made && made;
            BoundingSphere[] spheres = null;
            CullingGroup group = null;
            if (ready)
            {
                spheres = SpheresField?.GetValue(controller) as BoundingSphere[];
                group = GroupField?.GetValue(controller) as CullingGroup;
                if (spheres == null || group == null)
                {
                    return TheGamesWay(controller);   // its group made, but not where the mod can keep it in step
                }
            }
            _pending = pending;
            int taken;
            try
            {
                taken = SceneryCullRules.Compact(list, spheres, IsGoing, Dropped);   // a band the game was about to hand out is dropped with its entry
            }
            finally
            {
                _pending = null;
            }
            if (taken > 0 && group != null)
            {
                group.SetBoundingSphereCount(list.Count);
            }
            return taken;
        }

        private static void DropPending(ICullable cullable)
        {
            _pending?.Remove(cullable);
        }

        // The game's own UnregisterCullable, one at a time: it asks for a rebuild of the whole group, which while the city
        // unloads would walk the objects the unload has destroyed, so not then (the game's own objects do not either).
        private static int TheGamesWay(CullingGroupController controller)
        {
            if (GameManager.isCitySceneBeingUnloaded)
            {
                return 0;
            }
            foreach (ICullable cullable in Going)
            {
                controller.UnregisterCullable(cullable);
            }
            ByGame += Going.Count;
            return Going.Count;
        }

        /// <summary>
        /// Called as the city's part of the mod unloads: the check counts the dead entries of this city's culling lists as
        /// the next scene comes in (the game loads the menu first and unloads the city's scenes after it), when everything
        /// the mod destroyed is gone and the game's own objects are still there.
        /// </summary>
        public static void WatchUnload()
        {
            _city = InstanceBehavior<CullingManager>.IsInitialized ? InstanceBehavior<CullingManager>.Instance : null;
            _unloadFrame = Time.frameCount;
            _loaded = _loaded ?? Guard.Listener<Scene, LoadSceneMode>(OnSceneLoaded);
            SceneManager.sceneLoaded -= _loaded;
            SceneManager.sceneLoaded += _loaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Time.frameCount <= _unloadFrame + 1)
            {
                return;   // too soon: what the mod destroyed as it unloaded is gone only at the end of that frame
            }
            SceneManager.sceneLoaded -= _loaded;
            CullingManager city = _city;
            _city = null;
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            int dead = -1, listed = 0;
            bool rebuild = false;
            if (city != null && ListField != null)
            {
                dead = DeadIn(city.generalCullingGroupController, ref listed, ref rebuild) + DeadIn(city.hamptonsHousesCullingGroupController, ref listed, ref rebuild);
            }
            Logger?.Info(SceneryCullRules.UnloadLine(dead, listed, rebuild, Forgotten, ByGame));
        }

        private static int DeadIn(CullingGroupController controller, ref int listed, ref bool rebuild)
        {
            if (controller == null || ListField == null || !(ListField.GetValue(controller) is List<ICullable> list))
            {
                return 0;
            }
            listed += list.Count;
            rebuild |= RebuildField != null && RebuildField.GetValue(controller) is bool asked && asked;
            int dead = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (IsDead(list[i]))
                {
                    dead++;
                }
            }
            return dead;
        }

        // A component destroyed, or a building's music or crowd outside whose building is.
        private static bool IsDead(ICullable cullable)
        {
            switch (cullable)
            {
                case null:
                    return true;
                case UnityEngine.Object component:
                    return component == null;
                case BuildingOutsideMusicController music:
                    return MusicOwnerField != null && (MusicOwnerField.GetValue(music) as UnityEngine.Object) == null;
                case BuildingOutsideHangoutZoneController crowd:
                    return HangoutOwnerField != null && (HangoutOwnerField.GetValue(crowd) as UnityEngine.Object) == null;
                default:
                    return false;
            }
        }

        // The game's entries by the object itself: a destroyed component is still the one in the list.
        private sealed class SameObject : IEqualityComparer<ICullable>
        {
            public static readonly SameObject Instance = new SameObject();

            public bool Equals(ICullable x, ICullable y) => ReferenceEquals(x, y);

            public int GetHashCode(ICullable obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
