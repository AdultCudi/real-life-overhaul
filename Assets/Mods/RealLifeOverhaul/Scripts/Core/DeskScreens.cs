using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The screens at a desk, lit while somebody is seated there to work and dark again when they stand up. It is the game's own
    /// component: ScreenVideoController plays the "Work" video of the game's desk computers on a monitor, as the game's
    /// WorkstationController has it do for its employee (ItemController.PlayVideoOnScreen) and as the game's study and
    /// entertainment activities do. A desk the mod furnishes is a stripped copy (<see cref="GameFurniture"/>), which keeps that
    /// component switched off, so it is switched on here only while somebody sits at the desk. Nothing is drawn here and
    /// nothing is sent over the network: the screen is local and visual. COST: nothing per frame of its own; a seat or a stand
    /// is one walk down the desk's few children, and the game's component updates its picture only while it plays.
    /// </summary>
    internal static class DeskScreens
    {
        private static readonly List<ScreenVideoController> Found = new List<ScreenVideoController>(8);
        private static bool _said;

        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>The game's own item (a desk computer of its building): its desk's screens by the game's call, which a pose without its employee never makes.</summary>
        public static void OnItem(ItemController item)
        {
            try
            {
                if (item != null)
                {
                    item.PlayVideoOnScreen(VideoClipData.VideoType.Work);
                }
            }
            catch (Exception e)
            {
                Failed(e);
            }
        }

        /// <summary>The game's own item: its desk's screens dark again.</summary>
        public static void OffItem(ItemController item)
        {
            try
            {
                if (item != null)
                {
                    item.StopVideoOnScreen();
                }
            }
            catch (Exception e)
            {
                Failed(e);
            }
        }

        /// <summary>Every screen of a desk the mod furnished and what stands on it, lit. The number lit; never throws.</summary>
        public static int OnDesk(Transform desk, string who)
        {
            int lit = 0;
            try
            {
                if (desk != null)
                {
                    desk.GetComponentsInChildren(false, Found);
                    for (int i = 0; i < Found.Count; i++)
                    {
                        lit += OnScreen(Found[i]) ? 1 : 0;
                    }
                    Found.Clear();
                }
            }
            catch (Exception e)
            {
                Found.Clear();
                Failed(e);
            }
            Seen("lit", desk, who, lit);
            return lit;
        }

        /// <summary>Every screen of a desk the mod furnished, dark again. Safe twice, and with a desk that is gone. The number put out; never throws.</summary>
        public static int OffDesk(Transform desk, string who)
        {
            int dark = 0;
            try
            {
                if (desk != null)
                {
                    desk.GetComponentsInChildren(true, Found);
                    for (int i = 0; i < Found.Count; i++)
                    {
                        dark += OffScreen(Found[i]) ? 1 : 0;
                    }
                    Found.Clear();
                }
            }
            catch (Exception e)
            {
                Found.Clear();
                Failed(e);
            }
            Seen("dark", desk, who, dark);
            return dark;
        }

        /// <summary>The nearest standing screen under a root to a point within a radius, flat; null if none. Looked for once, as somebody sits.</summary>
        public static ScreenVideoController Near(Transform root, Vector3 at, float radius)
        {
            ScreenVideoController best = null;
            try
            {
                if (root != null)
                {
                    root.GetComponentsInChildren(false, Found);
                    float least = radius * radius;
                    for (int i = 0; i < Found.Count; i++)
                    {
                        Vector3 d = Found[i].transform.position - at;
                        float sqr = d.x * d.x + d.z * d.z;
                        if (sqr <= least)
                        {
                            least = sqr;
                            best = Found[i];
                        }
                    }
                    Found.Clear();
                }
            }
            catch (Exception e)
            {
                Found.Clear();
                best = null;
                Failed(e);
            }
            return best;
        }

        /// <summary>One screen of a copy lit: its component switched on for the game's picture changes, then the game's Work video. False if it could not be.</summary>
        public static bool OnScreen(ScreenVideoController screen)
        {
            try
            {
                if (screen == null)
                {
                    return false;
                }
                screen.enabled = true;   // the game updates its picture only while it plays
                screen.Play(VideoClipData.VideoType.Work);
                return true;
            }
            catch (Exception e)
            {
                Failed(e);
                return false;
            }
        }

        /// <summary>One screen of a copy dark: the game's Stop, then its component switched off again.</summary>
        public static bool OffScreen(ScreenVideoController screen)
        {
            try
            {
                if (screen == null)
                {
                    return false;
                }
                screen.Stop();
                screen.enabled = false;
                return true;
            }
            catch (Exception e)
            {
                Failed(e);
                return false;
            }
        }

        // With Developer diagnostics on, one line a seat or a stand: how many screens, or that the desk has none.
        private static void Seen(string what, Transform desk, string who, int count)
        {
            if (!Verbose || desk == null)
            {
                return;
            }
            GameFurniture.Logger?.Info("Desk check (screens): " + count + " screen(s) " + what + " at " + desk.name + " for " + who + ": " + (count > 0 ? "PASS" : "no screen of the game's on it"));
        }

        // A fault here never reaches the seat: the screens stay as they were, said once.
        private static void Failed(Exception e)
        {
            if (_said)
            {
                return;
            }
            _said = true;
            GameFurniture.Logger?.Warn("Desk screens: a screen could not be switched (" + e.Message + "); the desks are seated as they were. Said once.");
        }
    }
}
