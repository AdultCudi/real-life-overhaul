using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every search of the whole scene the mod makes goes through here. The engine answers "every object of this
    /// kind" by going through every object it has in memory, and this game has well over a million: one such search
    /// is a frame of about 110 ms (Player.log, three sessions: the line "Crime: road map built" was followed each time
    /// by a frame of 112 to 117 ms, and every driving lesson began with one of 126 to 146 ms; both asked the traffic
    /// system for its road data, which it finds by such a search). So a search is never to be made while the game is
    /// being played: what it finds is found once per city, behind the loading screen (<see cref="ScenePrewarm"/>), and
    /// kept. Where one is made in play after all, it is named and timed for the session record's long-frame line,
    /// counted for its line a minute, and said in the log with FAIL.
    ///
    /// Cost of a call: the search itself. Nothing here runs unless something calls it.
    /// </summary>
    public static class SceneFinds
    {
        /// <summary>Searches made since the mod started, and how many of them while the game was being played.</summary>
        public static int Made { get; private set; }
        public static int InPlay { get; private set; }

        /// <summary>True while a long frame is not seen as one: the loading screen, a pause, or the game's own black screen at a door.</summary>
        public static bool Hidden => Looks.Hidden || UI.UiFader.isFading;

        /// <summary>Every object of a kind in the loaded scenes, in no particular order. <paramref name="what"/> is a constant text.</summary>
        public static T[] All<T>(string what, FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T : UnityEngine.Object
        {
            int mark = SessionWatch.Mark(what);
            long began = Stopwatch.GetTimestamp();
            T[] found = UnityEngine.Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None);
            Said(what, mark, began, found.Length);
            return found;
        }

        /// <summary>Every object of a kind the engine has in memory, in a scene or not (assets too). <paramref name="what"/> is a constant text.</summary>
        public static T[] InMemory<T>(string what) where T : UnityEngine.Object
        {
            int mark = SessionWatch.Mark(what);
            long began = Stopwatch.GetTimestamp();
            T[] found = Resources.FindObjectsOfTypeAll<T>();
            Said(what, mark, began, found.Length);
            return found;
        }

        private static void Said(string what, int mark, long began, int found)
        {
            SessionWatch.Done(mark);
            bool hidden = Hidden;
            Made++;
            InPlay += hidden ? 0 : 1;
            if (Meta.Dev.Verbose)
            {
                double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                SessionWatch.Logger?.Info("Scene search check (" + what + "): " + found + " found in " + ms.ToString("F1") + " ms, "
                                          + (hidden ? "behind the loading screen, a pause or the game's black screen: PASS."
                                              : "while the game was being played: FAIL (a stutter of that length; it belongs behind the loading screen)."));
            }
        }
    }

    /// <summary>
    /// The one-off work that would otherwise be done the first time it is needed, in the middle of play: done instead
    /// while the loading screen is still up. A feature hands its piece over in its Awake (<see cref="Add"/>); the mod's
    /// entry calls <see cref="Begin"/> once every feature is there, which the game does after the city's scenes are
    /// loaded and before its loading screen goes (UI.Load.LoadingScreen.LoadAsyncScenes). A piece that cannot be done
    /// yet (it needs the city shown, and the save was made inside a building) says so by returning false and is tried
    /// again, once, each time the player is outdoors with the game's black screen up, which is the walk out of a door.
    ///
    /// Per frame: nothing (the component is disabled) unless a piece is waiting; then two of the game's flags read.
    /// </summary>
    public sealed class ScenePrewarm : MonoBehaviour
    {
        private static readonly List<Func<bool>> Work = new List<Func<bool>>(8);
        private static readonly List<string> Names = new List<string>(8);
        private static ScenePrewarm _instance;
        private bool _tried;

        /// <summary>Hands over a piece of one-off work. It returns true when done (or never to be done), false to be tried again later. A constant text.</summary>
        public static void Add(string what, Func<bool> work)
        {
            if (work != null)
            {
                Work.Add(work);
                Names.Add(what);
            }
        }

        /// <summary>Does every piece now. Called once per city, while the loading screen is up.</summary>
        public static void Begin()
        {
            int before = Work.Count;
            long began = Stopwatch.GetTimestamp();
            Run();
            if (Meta.Dev.Verbose)
            {
                SessionWatch.Logger?.Info("Prewarm check: " + (before - Work.Count) + " of " + before + " piece(s) of one-off work done behind the loading screen in "
                                          + ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency).ToString("F0") + " ms"
                                          + (Work.Count > 0 ? "; " + Work.Count + " wait for the player to be outdoors (the first: " + Names[0] + ")." : ": PASS."));
            }
            if (_instance != null)
            {
                _instance.enabled = Work.Count > 0;
            }
        }

        private static void Run()
        {
            for (int i = Work.Count - 1; i >= 0; i--)
            {
                bool done = true;
                int mark = SessionWatch.Mark(Names[i]);
                try
                {
                    done = Work[i]();
                }
                catch (Exception e)
                {
                    SessionWatch.Logger?.Error(e);   // the feature does it on first need instead, as it did before
                }
                SessionWatch.Done(mark);
                if (done)
                {
                    Work.RemoveAt(i);
                    Names.RemoveAt(i);
                }
            }
        }

        private void Awake()
        {
            _instance = this;
            enabled = false;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: ScenePrewarm.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (Work.Count == 0)
            {
                enabled = false;
                return;
            }
            if (BuildingManager.IsInsideBuilding || !SceneFinds.Hidden)
            {
                _tried = false;
                return;
            }
            if (!_tried)
            {
                _tried = true;   // once each time the screen goes black, not once a frame while it is
                Run();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                Work.Clear();   // the pieces belonged to the city that is going
                Names.Clear();
            }
        }
    }
}
