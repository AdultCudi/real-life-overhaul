using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Every canvas of the kit's own that takes the pointer, registered where it is made: the canvases over the game's pieces
    /// (UiOverCanvas), a host's over a game screen (UiGameHost), the phone's, All apps', a modal piece's, and the cards on canvases of
    /// their own (the bug report's, the mod's messages, the viewfinder, a page opened over the screen). Many of them carry a clear
    /// picture over the whole screen so that no click reaches what lies under, which is right only while something answers them.
    /// When the interface's part is switched off or stops, <see cref="HideAll"/> switches each one off, so none can lie unseen over
    /// the screen taking every click (1.0.4: the bug report's card did). EconomyCheck reads the sources: a canvas of the kit's that
    /// takes the pointer and is not registered here fails the gate. COST: a list entry a canvas made; nothing a frame.
    /// </summary>
    internal static class UiRoots
    {
        private const int PruneAt = 48;   // canvases kept before those gone with their city are dropped from the list

        private static readonly List<GameObject> Roots = new List<GameObject>(64);

        /// <summary>Registers a canvas of the kit's that takes the pointer; returns it.</summary>
        internal static GameObject Take(GameObject root)
        {
            if (root == null)
            {
                return null;
            }
            if (Roots.Count >= PruneAt)
            {
                for (int i = Roots.Count - 1; i >= 0; i--)
                {
                    if (Roots[i] == null)
                    {
                        Roots.RemoveAt(i);
                    }
                }
            }
            Roots.Add(root);
            return root;
        }

        /// <summary>True while any of them is switched on.</summary>
        internal static bool AnyStanding()
        {
            for (int i = 0; i < Roots.Count; i++)
            {
                if (Roots[i] != null && Roots[i].activeSelf)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Switches every one of them off; returns how many were on. Each owner shows its own again when it next draws.</summary>
        internal static int HideAll()
        {
            int hidden = 0;
            for (int i = Roots.Count - 1; i >= 0; i--)
            {
                GameObject root = Roots[i];
                if (root == null)
                {
                    Roots.RemoveAt(i);
                    continue;
                }
                if (root.activeSelf)
                {
                    root.SetActive(false);
                    hidden++;
                }
            }
            return hidden;
        }
    }
}
