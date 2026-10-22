using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every search of the whole scene the mod makes goes through here. The engine answers "every object of this
    /// kind" by going through every object it has in memory, and this game has well over a million: one such search
    /// is a frame of about 110 ms (the line "Crime: road map built" was followed each time
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

        /// <summary>
        /// True while a long frame is not seen as one: the loading screen (also in the frame the game ends its own start of the
        /// city in, while the late prewarm runs: <see cref="ScenePrewarm.InLate"/>), a pause, or the game's own black screen at a door.
        /// </summary>
        public static bool Hidden => Looks.Hidden || UI.UiFader.isFading || ScenePrewarm.InLate;

        /// <summary>
        /// True while the screen is dark: the loading screen, or the game's own black screen (a door, sleep, fast travel),
        /// its fades out and back in included. Not a plain pause: its menu stays on the screen, and a search of some 80 ms
        /// there is a hitch the player sees in the menu. Work that comes back again and again (a search after every door)
        /// keeps its stretch by this, never <see cref="Hidden"/>, and is made only on a frame that is <see cref="Black"/>.
        /// </summary>
        public static bool Dark => UI.Load.LoadScene.isLoading || UI.UiFader.isFading;

        /// <summary>
        /// True while the screen shows black: the loading screen, or the game's own black screen drawn at full strength
        /// (<see cref="Covered"/>). <see cref="Dark"/> is true through the game's fades as well, and at a door they take
        /// 0.2 s each way (BuildingManager: UiFader.Fade(0.2f), UnFade(0.2f)) with the picture showing through: a long frame
        /// there stops the picture, and the player sees it. Heavy work that waits for the dark makes itself on this.
        /// </summary>
        public static bool Black => UI.Load.LoadScene.isLoading || Covered;

        /// <summary>
        /// True while the game's black screen (UI.UiFader, its overlay UIs.blackOverlay) is drawn at full strength: from the
        /// end of its fade out to the first step of its fade back in. Read only while it fades (one flag otherwise): the
        /// overlay's own alpha, which the game's fade moves from 0 to 1 and back (DOFade). A build in which the overlay
        /// cannot be read has no fully black screen here, with one line, and the work that waits for one waits for the
        /// loading screen.
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

        private const float Opaque = 0.99f;   // the overlay's alpha from which nothing shows through it (the game's fade ends at exactly 1)
        private static UnityEngine.UI.Image _overlay;   // the game's black overlay, taken from its register of singletons (no search), again only once it is gone
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

        /// <summary>Every object of a kind in the loaded scenes, in no particular order. <paramref name="what"/> is a constant text.</summary>
        public static T[] All<T>(string what, FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T : UnityEngine.Object
        {
            int mark = SessionWatch.Mark(what);
            long began = Stopwatch.GetTimestamp();
            T[] found = UnityEngine.Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None);
            Said(what, mark, began, found.Length);
            return found;
        }

        /// <summary>Any one active object of a kind known only at run time (another author's type), or null. <paramref name="what"/> is a constant text.</summary>
        public static UnityEngine.Object Any(Type type, string what)
        {
            int mark = SessionWatch.Mark(what);
            long began = Stopwatch.GetTimestamp();
            UnityEngine.Object found = UnityEngine.Object.FindAnyObjectByType(type);
            Said(what, mark, began, found != null ? 1 : 0);
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
            // The game's black screen counts only drawn at full strength: through its fade the picture shows, and stops.
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
    /// The one-off work that would otherwise be done the first time it is needed, in the middle of play: done instead
    /// while the loading screen is still up. A feature hands its piece over in its Awake (<see cref="Add"/>); the mod's
    /// entry calls <see cref="Begin"/> once every feature is there, which the game does after the city's scenes are
    /// loaded and before its loading screen goes (UI.Load.LoadingScreen.LoadAsyncScenes). A piece that cannot be done
    /// yet (it needs the city shown, and the save was made inside a building) says so by returning false and is tried
    /// again, once, each time the player is outdoors with the game's black screen up, which is the walk out of a door.
    ///
    /// LATE PIECES (<see cref="AddLate"/>). Some work needs what the game itself makes of the city only after the mods are
    /// loaded: its traffic system started, which marks the lane points at junctions (the road graph's changes of lane depend
    /// on those marks), its list of subway stations and of buildings (CityManager.OnScenesLoaded). The game makes them in its
    /// "game loaded" call, in the frame after which its loading screen begins to fade (LoadingScreen.LoadAsyncScenes:
    /// GlobalEvents.InvokeOnGameLoaded, then the fade). Late pieces run in that same frame, in the game's own late call
    /// (GlobalEvents.RegisterOnGameLoadedLateCallback, after its own calls), with the loading screen still fully up; a piece
    /// that cannot be done there joins the others that wait for the black screen out of doors.
    ///
    /// Per frame: nothing (the component is disabled) unless a piece is waiting; then two of the game's flags read.
    /// </summary>
    public sealed class ScenePrewarm : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ScenePrewarm", typeof(ScenePrewarm));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private static readonly List<Func<bool>> Work = new List<Func<bool>>(8);
        private static readonly List<string> Names = new List<string>(8);
        private static readonly List<Func<bool>> Late = new List<Func<bool>>(4);
        private static readonly List<string> LateNames = new List<string>(4);
        private static Action _lateCall;   // made once: the game's late "game loaded" call, through the guard (Core/Guard)
        private static ScenePrewarm _instance;
        private bool _tried;

        /// <summary>True while the late pieces run: the loading screen is up, though the game has said its load is done.</summary>
        public static bool InLate { get; private set; }

        /// <summary>Hands over a piece of one-off work. It returns true when done (or never to be done), false to be tried again later. A constant text.</summary>
        public static void Add(string what, Func<bool> work)
        {
            if (work != null)
            {
                Work.Add(work);
                Names.Add(what);
            }
        }

        /// <summary>
        /// Hands over a piece of one-off work that needs the game's own start of the city done (its traffic system, its stations):
        /// done in the game's late "game loaded" call, the loading screen still up; false there, and it waits with the others for
        /// the black screen out of doors. Pieces run in the order they were handed over. A constant text.
        /// </summary>
        public static void AddLate(string what, Func<bool> work)
        {
            if (work != null)
            {
                Late.Add(work);
                LateNames.Add(what);
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
            if (Late.Count > 0)
            {
                // At once if the game has already made its call (a mod loaded after it): the pieces then find what they need or wait.
                GlobalEvents.RegisterOnGameLoadedLateCallback(_lateCall ??= Guard.Wrap(RunLate));
            }
        }

        // The game's late "game loaded" call: its traffic system and stations are there, its loading screen still fully up.
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
                    bool finished = true;
                    int mark = SessionWatch.Mark(LateNames[i]);
                    try
                    {
                        finished = Late[i]();
                    }
                    catch (Exception e)
                    {
                        if (!Guard.DoesWithout(e, "the work behind the loading screen: " + LateNames[i], "the feature does it when it first needs it"))
                        {
                            SessionWatch.Logger?.Error(e);   // the feature does it on first need instead, as it did before
                        }
                    }
                    SessionWatch.Done(mark);
                    if (finished)
                    {
                        done++;
                    }
                    else
                    {
                        Work.Insert(0, Late[i]);   // with the others, at the end of the round they are run in (last first)
                        Names.Insert(0, LateNames[i]);
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
                    if (!Guard.DoesWithout(e, "the work behind the loading screen: " + Names[i], "the feature does it when it first needs it"))
                    {
                        SessionWatch.Logger?.Error(e);   // the feature does it on first need instead, as it did before
                    }
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
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                enabled = false;
                return;
            }
            if (BuildingManager.IsInsideBuilding || !SceneFinds.Dark)
            {
                _tried = false;   // (a plain pause is not the walk out of a door: its menu would show the work as a hitch)
                return;
            }
            if (!SceneFinds.Black)
            {
                return;   // the game's fade, with the street showing through it: the work waits for the screen fully black
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
                Late.Clear();
                LateNames.Clear();
            }
        }
    }
}
