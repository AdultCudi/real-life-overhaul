using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the game's list of trees (TreeController.AllTrees) free of trees that no longer exist. The game adds every tree to
    /// that list when it wakes and never takes one out (its OnDestroy does not), and its city map walks the list and asks each
    /// tree for its game object: one destroyed tree makes the map's open coroutine throw, and the map stays shut with only its
    /// tint on the screen, for the rest of the session (0.60.0: the garage doors' mouths on Uptown's pavements destroyed the
    /// trees standing in them). The game never destroys a tree during a session; the mod does. Two layers:
    /// <see cref="Forget"/>, <see cref="Destroy"/> and <see cref="DestroyNow"/> take the trees of an object out of the list
    /// before the mod destroys it (every path of the mod that destroys a copy of the game's scenery goes through them,
    /// held by StandardCheck RLO-CITY-001), and <see cref="Prune"/> takes out any dead entry, whoever destroyed it (the map's
    /// guard, <see cref="MapTreeGuard"/>, runs it before the game's map handler). COST: nothing per frame. Forget looks for one
    /// tree under the object and walks the list only if there is one; Prune is one backward walk of the list, no allocation.
    /// </summary>
    public static class SceneryTrees
    {
        /// <summary>Trees taken out of the game's list by <see cref="Forget"/> since the mod started.</summary>
        public static int Forgotten { get; private set; }

        /// <summary>Dead entries <see cref="Prune"/> found in the game's list since the mod started (not the mod's own: those are forgotten first).</summary>
        public static int Pruned { get; private set; }

        private static Transform _under;   // the root Forget is taking the trees of
        private static readonly Predicate<TreeController> Beneath = IsBeneath;

        /// <summary>Takes the trees at or under <paramref name="root"/> out of the game's list. Call before destroying it; null is safe. Returns how many.</summary>
        public static int Forget(GameObject root)
        {
            if (ReferenceEquals(root, null) || root == null)
            {
                return 0;
            }
            if (root.GetComponentInChildren<TreeController>(true) == null)
            {
                return 0;   // the common case: none, and nothing is walked
            }
            _under = root.transform;
            int taken = TreeController.AllTrees.RemoveAll(Beneath);
            _under = null;
            Forgotten += taken;
            return taken;
        }

        /// <summary>Forgets the trees of <paramref name="root"/>, then destroys it at the end of the frame.</summary>
        public static void Destroy(GameObject root)
        {
            if (ReferenceEquals(root, null))
            {
                return;
            }
            Forget(root);
            UnityEngine.Object.Destroy(root);
        }

        /// <summary>Forgets the trees of <paramref name="root"/>, then destroys it now.</summary>
        public static void DestroyNow(GameObject root)
        {
            if (ReferenceEquals(root, null))
            {
                return;
            }
            Forget(root);
            UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Takes every destroyed tree out of the game's list, whoever destroyed it; how many. One backward walk, no
        /// allocation (a Unity null test per tree).
        /// </summary>
        public static int Prune()
        {
            List<TreeController> all = TreeController.AllTrees;
            int taken = 0;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i] == null)
                {
                    all.RemoveAt(i);
                    taken++;
                }
            }
            Pruned += taken;
            return taken;
        }

        /// <summary>How many destroyed trees the game's list holds now.</summary>
        public static int Dead()
        {
            List<TreeController> all = TreeController.AllTrees;
            int dead = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null)
                {
                    dead++;
                }
            }
            return dead;
        }

        private static bool IsBeneath(TreeController tree)
        {
            return tree == null || (_under != null && tree.transform.IsChildOf(_under));
        }
    }
}
