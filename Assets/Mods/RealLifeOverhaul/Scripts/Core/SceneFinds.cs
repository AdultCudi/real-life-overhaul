using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every search of the whole scene the mod makes goes through here. The engine goes through every object in memory,
    /// well over a million in this game, so one search is a frame of about 110 ms: what it finds is found once per city
    /// behind the loading screen (<see cref="ScenePrewarm"/>) and kept. One made in play is named and timed for the
    /// session record and said in the log with FAIL. Cost: the search itself; nothing runs unless called.
    /// </summary>
    public static class SceneFinds
    {
        /// <summary>Searches made since the mod started, and how many of them while the game was being played.</summary>
        public static int Made { get; private set; }
        public static int InPlay { get; private set; }

        /// <summary>True while a long frame is not seen: the loading screen, a pause, or the game's black screen at a door.</summary>
        public static bool Hidden => Looks.Hidden || UI.UiFader.isFading || ScenePrewarm.InLate;

        /// <summary>
        /// True while the screen is dark: the loading screen or the game's black screen, its fades included. Not a pause,
        /// whose menu would show a hitch: work that comes back after every door keeps its stretch by this, never
        /// <see cref="Hidden"/>, and runs only on a frame that is <see cref="Black"/>.
        /// </summary>
        public static bool Dark => UI.Load.LoadScene.isLoading || UI.UiFader.isFading;

        /// <summary>True while the screen shows black; through a door's 0.2 s fades the picture shows, so heavy work waits for this.</summary>
        public static bool Black => UI.Load.LoadScene.isLoading || Covered;

        /// <summary>
        /// True while the game's black overlay is at full strength, its alpha read only while it fades; false for good
        /// where it cannot be read (one log line).
        /// </summary>
        public static bool Covered
        {
            get
            {
                if (!UI.UiFader.isFading || _overlayUnread)
                {
                    return false;
                }
                try
                {
                    return OverlayAlpha() >= Opaque;
                }
                catch (Exception e)
                {
                    _overlayUnread = true;
                    SessionWatch.Logger?.Warn("Scene searches: the game's black screen cannot be read in this build (" + e.GetType().Name
                                              + "); the searches that wait for it wait for the loading screen.");
                    return false;
                }
            }
        }

        private const float Opaque = 0.99f;   // the overlay's alpha from which nothing shows through (the game's fade ends at 1)
        private static UnityEngine.UI.Image _overlay;   // from the game's register of singletons, no search
        private static bool _overlayUnread;

        // In a method of its own: a build without the overlay fails here alone (Mono compiles a method whole).
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static float OverlayAlpha()
        {
            if (_overlay == null)
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.IsInitialized ? InstanceBehavior<UI.UIs>.Instance : null;
                _overlay = uis != null ? uis.blackOverlay : null;
                if (_overlay == null)
                {
                    return 0f;
                }
            }
            return _overlay.color.a;
        }

        /// <summary>Every object of a kind in the loaded scenes, in no order. <paramref name="what"/> is a constant text.</summary>
        public static T[] All<T>(string what, FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T : UnityEngine.Object
        {
            int mark = SessionWatch.Mark(what);
            long began = Stopwatch.GetTimestamp();
            T[] found = UnityEngine.Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None);
            Said(what, mark, began, found.Length);
            return found;
        }

        /// <summary>Any one active object of a type known only at run time, or null. <paramref name="what"/> is a constant text.</summary>
        public static UnityEngine.Object Any(Type type, string what)
        {
            int mark = SessionWatch.Mark(what);
            long began = Stopwatch.GetTimestamp();
            UnityEngine.Object found = UnityEngine.Object.FindAnyObjectByType(type);
            Said(what, mark, began, found != null ? 1 : 0);
            return found;
        }

        /// <summary>Every object of a kind in memory, assets too. <paramref name="what"/> is a constant text.</summary>
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
            // The game's black screen counts only at full strength: through its fade the picture shows.
            bool hidden = Looks.Hidden || ScenePrewarm.InLate || Covered;
            bool fading = !hidden && UI.UiFader.isFading;
            Made++;
            InPlay += hidden ? 0 : 1;
            if (Meta.Dev.Verbose)
            {
                double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                SessionWatch.Logger?.Info("Scene search check (" + what + "): " + found + " found in " + ms.ToString("F1", CultureInfo.InvariantCulture) + " ms, "
                                          + (hidden ? "behind the loading screen, a pause or the game's black screen: PASS."
                                              : fading ? "during the game's fade, with the picture showing through: FAIL (a stutter the player sees; "
                                                         + "it belongs behind the black screen at full strength)."
                                              : "while the game was being played: FAIL (a stutter of that length; it belongs behind the loading screen)."));
            }
        }
    }

    /// <summary>
    /// One-off work done while the loading screen is still up instead of when first needed in play. Features hand pieces
    /// over in Awake (<see cref="Add"/>); the entry calls <see cref="Begin"/> once all are there. A piece that returns
    /// false (it needs the city shown and the save was made indoors) is tried again once each time the player is outdoors
    /// behind the black screen. Late pieces (<see cref="AddLate"/>) need the game's own start of the city (its traffic
    /// system, stations, buildings) and run in its late "game loaded" call, the loading screen still up. Per frame:
    /// nothing (disabled) unless a piece is waiting; then two of the game's flags read.
    /// </summary>
    public sealed class ScenePrewarm : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ScenePrewarm", typeof(ScenePrewarm));

        private static readonly List<Func<bool>> Work = new List<Func<bool>>(8);
        private static readonly List<string> Names = new List<string>(8);
        private static readonly List<int> TriedIn = new List<int>(8);   // the black screen each piece was last tried behind
        private static readonly List<Func<bool>> Late = new List<Func<bool>>(4);
        private static readonly List<string> LateNames = new List<string>(4);
        private static Action _lateCall;   // made once: the game's late "game loaded" call, through the guard
        private static ScenePrewarm _instance;
        private bool _tried;

        // Behind a black screen the pieces go one a frame, never all in one (RLO-PERF-024: beta 6 had a frame of 13 ms and
        // beta 4 one of 45 ms from every waiting piece at once): a piece is tried once behind each black screen, and a piece
        // of several steps (a dozen coats, each a mesh merge) does one step a frame. A frame is one piece's call at most.
        private const int MostSteps = 64;            // steps of one piece behind the loading screen at most (a stuck piece is let go)
        private static int _blacks;                  // black screens with pieces waiting, counted: a piece is tried once in each
        private static bool _stepAgain;              // the piece just called did one step of several (NextFrame)
        private static readonly SpreadWatch Spread = new SpreadWatch();
        private static string _slowest;              // the piece of the slowest frame behind this black screen, for its line
        private static bool _spreading;              // a black screen's pieces are being tried: its line is still to be written

        /// <summary>True while the late pieces run: the loading screen is up though the game's load is done.</summary>
        public static bool InLate { get; private set; }

        /// <summary>Hands over a piece of one-off work: true when done (or never to be), false to be tried later. A constant text.</summary>
        public static void Add(string what, Func<bool> work)
        {
            if (work != null)
            {
                Work.Add(work);
                Names.Add(what);
                TriedIn.Add(0);
            }
        }

        /// <summary>
        /// Said by a piece that did one step of work that has several (a look merged of a dozen), as it returns false: it is
        /// called again in the next frame while the screen is black, and at once behind the loading screen.
        /// </summary>
        public static void NextFrame()
        {
            _stepAgain = true;
        }

        /// <summary>
        /// Hands a piece over again in play, for a feature switched back on whose work the loading screen already passed:
        /// it is done the next time the player goes through a door to the street, behind the black screen.
        /// </summary>
        public static void Again(string what, Func<bool> work)
        {
            Add(what, work);
            if (_instance != null && work != null)
            {
                _instance.enabled = true;
            }
        }

        /// <summary>Hands over a piece run in the game's late "game loaded" call, in order; false there, and it waits. A constant text.</summary>
        public static void AddLate(string what, Func<bool> work)
        {
            if (work != null)
            {
                Late.Add(work);
                LateNames.Add(what);
            }
        }

        /// <summary>Does every piece now; once per city, while the loading screen is up.</summary>
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
            if (Late.Count > 0)
            {
                // At once if the game has already made its call: the pieces then find what they need or wait.
                GlobalEvents.RegisterOnGameLoadedLateCallback(_lateCall ??= Guard.Wrap(RunLate));
            }
        }

        // The game's late "game loaded" call: its traffic system and stations are there, the loading screen still up.
        private static void RunLate()
        {
            int before = Late.Count;
            int done = 0;
            long began = Stopwatch.GetTimestamp();
            InLate = true;
            try
            {
                for (int i = 0; i < Late.Count; i++)
                {
                    if (Steps(Late[i], LateNames[i]))
                    {
                        done++;
                    }
                    else
                    {
                        Work.Insert(0, Late[i]);   // with the others, last in their round (they run last first)
                        Names.Insert(0, LateNames[i]);
                        TriedIn.Insert(0, 0);
                    }
                }
            }
            finally
            {
                InLate = false;
                Late.Clear();
                LateNames.Clear();
            }
            if (Meta.Dev.Verbose)
            {
                SessionWatch.Logger?.Info("Prewarm check (after the game's own start of the city): " + done + " of " + before + " piece(s) done behind the loading screen in "
                                          + ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency).ToString("F0") + " ms"
                                          + (done < before ? "; " + (before - done) + " wait for the player to be outdoors (the first: " + Names[0] + ")." : ": PASS."));
            }
            if (_instance != null)
            {
                _instance.enabled = Work.Count > 0;
            }
        }

        // Behind the loading screen: every piece whole, each of its steps at once.
        private static void Run()
        {
            for (int i = Work.Count - 1; i >= 0; i--)
            {
                if (Steps(Work[i], Names[i]) && i < Work.Count)
                {
                    Work.RemoveAt(i);
                    Names.RemoveAt(i);
                    TriedIn.RemoveAt(i);
                }
            }
        }

        // A piece with all its steps, where a long frame is not seen.
        private static bool Steps(Func<bool> work, string what)
        {
            bool done = Call(work, what);
            for (int n = 1; !done && _stepAgain && n < MostSteps; n++)
            {
                done = Call(work, what);
            }
            return done;
        }

        // One call of a piece: true when it is done (or failed: the feature then does it on first need).
        private static bool Call(Func<bool> work, string what)
        {
            _stepAgain = false;
            bool done = true;
            int mark = SessionWatch.Mark(what);
            try
            {
                done = work();
            }
            catch (Exception e)
            {
                _stepAgain = false;
                if (!Guard.DoesWithout(e, "the work behind the loading screen: " + what, "the feature does it when it first needs it"))
                {
                    SessionWatch.Logger?.Error(e);   // the feature does it on first need instead
                }
            }
            SessionWatch.Done(mark);
            return done;
        }

        // Behind a black screen: the next piece not yet tried behind it, one a frame. False when every piece has been.
        private static bool RunOne()
        {
            int i = Work.Count - 1;
            while (i >= 0 && TriedIn[i] == _blacks)
            {
                i--;
            }
            if (i < 0)
            {
                return false;
            }
            string what = Names[i];
            long began = Stopwatch.GetTimestamp();
            bool done = Call(Work[i], what);
            if (Spread.Item(0, began))
            {
                _slowest = what;
            }
            if (i < Work.Count && ReferenceEquals(Names[i], what))
            {
                if (done)
                {
                    Work.RemoveAt(i);
                    Names.RemoveAt(i);
                    TriedIn.RemoveAt(i);
                }
                else if (!_stepAgain)
                {
                    TriedIn[i] = _blacks;   // tried behind this black screen: the next one tries it again
                }
            }
            return true;
        }

        // The self-check line of one black screen's pieces, with Developer diagnostics on: written once it is over.
        private static void SayDoor()
        {
            if (!_spreading)
            {
                return;
            }
            _spreading = false;
            if (Meta.Dev.Verbose && Spread.Items > 0)
            {
                SessionWatch.Logger?.Info("Prewarm check (behind a black screen, a piece a frame): " + Spread.Said(_slowest ?? "-") + "; " + Work.Count
                                          + " piece(s) wait for the next: " + Spread.Verdict(true) + ".");
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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Work.Count == 0)
            {
                SayDoor();
                enabled = false;
                return;
            }
            if (BuildingManager.IsInsideBuilding || !SceneFinds.Dark)
            {
                SayDoor();
                _tried = false;   // a plain pause is not the walk out of a door: its menu would show the hitch
                return;
            }
            if (!SceneFinds.Black)
            {
                return;   // the game's fade, the street showing through: wait for the screen fully black
            }
            if (!_tried)
            {
                _tried = true;   // a new black screen: every piece is tried once behind it, not once a frame while it is
                _blacks++;
                _slowest = null;
                _spreading = true;
                Spread.Begin();
            }
            if (_spreading)
            {
                long began = Stopwatch.GetTimestamp();
                if (RunOne())
                {
                    Spread.Frame(began);
                }
                else
                {
                    SayDoor();   // every piece tried behind this black screen; the rest wait for the next
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                Work.Clear();   // the pieces belonged to the city that is going
                Names.Clear();
                TriedIn.Clear();
                _spreading = false;
                Late.Clear();
                LateNames.Clear();
            }
        }
    }
}
