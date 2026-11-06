using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;

// When the whole scene may be searched behind the game's black screen (Core/SearchPace.cs), on a model of the game's own
// fades frame by frame, written as the game's own coroutines are: BuildingManager.EnterBuildingCoroutine (UiFader.Fade(0.2f),
// a frame, the building's load, the frame it is put in, DelayedEnterBuildingActions with its call
// GlobalEvents.onEnterBuildingDelayed, WarmUpBehaviourManager's four frames, UiFader.UnFade(0.2f)), ExitFromBuildingCoroutine
// (Fade(0.2f), a frame, the frame the city is switched back on, UnFade(0.2f)) and a taxi's ride (TaxiSystem: Fade(1f), the
// ride, UnFade()). The overlay's alpha moves as the game's tween moves it (DOFade on the real clock, a step a frame by the
// frame before's time), stepped before the mod's Update or after it. Up to 0.47.0 the search was made half a second into any
// fade: at a door that is its fade back in, and the session of 21 Oct 2026 had a frame of 80 to 110 ms there on every walk
// into a building. Over the sources: the hair finish asks with the black screen and the game's call, the one-off work of
// the first minute waits for the black screen too, and the log says FAIL for a search made during a fade.
internal static partial class Program
{
    private const float FadeFrame = 0.0096f;   // seconds: a frame of 9.6 ms, as in the city (Session 5 min)

    // One frame of the model: what the mod's Update reads in it, and what the game did after it.
    private struct FadeFrameState
    {
        public float At;          // real seconds at the frame's start
        public bool Dark;         // UI.UiFader.isFading as the mod's Update reads it
        public float Shown;       // the overlay's alpha in the picture on the screen while the frame runs (the frame before's)
        public float Read;        // the overlay's alpha the mod's Update reads
        public bool Settled;      // the game's "entered, and settled" call, in its coroutines after the Update
        public bool InView;       // the game's fade back in had begun before the frame (UnFade called)
    }

    // The game's side of the model: its fade's flag, its tween and the length of the frame its coroutine makes.
    private sealed class FadeGame
    {
        public bool Fading;       // UI.UiFader.isFading
        public float Alpha;       // UIs.blackOverlay's alpha
        public bool Running;      // the tween is under way
        public bool InCalled;     // UnFade has been called
        public float FrameLength; // the frame the coroutine is in, seconds
        public bool Settled;
        public float Clock;
        private int _dir;
        private float _elapsed, _length;

        public void Tween(int dir, float length)
        {
            _dir = dir;
            _length = length;
            _elapsed = 0f;
            Running = true;
            InCalled |= dir < 0;
        }

        public void Step(float dt)
        {
            if (!Running)
            {
                return;
            }
            _elapsed += dt;
            Alpha = _dir > 0 ? Math.Min(1f, _elapsed / _length) : Math.Max(0f, 1f - _elapsed / _length);
            Running = _elapsed < _length;
        }
    }

    private static IEnumerable<object> GameEnter(FadeGame g, int load)
    {
        g.Fading = true;            // UiFader.Fade(0.2f)
        g.Tween(1, 0.2f);
        while (g.Running)
        {
            yield return null;      // WaitForCompletion
        }
        yield return null;
        for (int i = 0; i < load; i++)
        {
            yield return null;      // BuildingSizeResolver.LoadBuildingAsync
        }
        g.FrameLength = 0.150f;     // LoadBuilding, LoadItems, onEnterBuilding: some 150 ms of the game's own
        yield return null;          // DelayedEnterBuildingActions: yield return null
        g.Settled = true;           // SpawnPlayerVehicles, the items, onEnterBuildingDelayed
        for (int i = 0; i < 4; i++)
        {
            yield return null;      // WarmUpBehaviourManager
        }
        g.Tween(-1, 0.2f);          // UiFader.UnFade(0.2f)
        while (g.Running)
        {
            yield return null;
        }
        g.Fading = false;
    }

    private static IEnumerable<object> GameExit(FadeGame g)
    {
        g.Fading = true;
        g.Tween(1, 0.2f);
        while (g.Running)
        {
            yield return null;
        }
        yield return null;
        g.FrameLength = 0.230f;     // ResetIndoors, onExitBuilding: every CityMapHider switched back on in the one frame
        g.Tween(-1, 0.2f);
        while (g.Running)
        {
            yield return null;
        }
        g.Fading = false;
    }

    private static IEnumerable<object> GameTaxi(FadeGame g)
    {
        g.Fading = true;            // TaxiSystem: UiFader.Fade(1f)
        g.Tween(1, 1f);
        while (g.Running)
        {
            yield return null;
        }
        float until = g.Clock + 3f; // the ride
        while (g.Clock < until)
        {
            yield return null;
        }
        g.Tween(-1, 0.4f);          // UiFader.UnFade()
        while (g.Running)
        {
            yield return null;
        }
        g.Fading = false;
    }

    // Runs one of the game's coroutines frame by frame from real second t, in the engine's order: the Updates (the
    // game's tween first or the mod's first), then the coroutines. Ends with the first frame after the fade.
    private static List<FadeFrameState> FadeModel(Func<FadeGame, IEnumerable<object>> game, bool tweenFirst, float t)
    {
        var frames = new List<FadeFrameState>();
        var g = new FadeGame { Clock = t, FrameLength = FadeFrame };
        IEnumerator<object> run = game(g).GetEnumerator();
        bool alive = run.MoveNext();   // started in the frame before (a click, a key): its fade is called there
        float before = FadeFrame;
        while (true)
        {
            var f = new FadeFrameState { At = t, Dark = g.Fading, Shown = g.Alpha, InView = g.InCalled };
            g.FrameLength = FadeFrame;
            g.Settled = false;
            if (tweenFirst)
            {
                g.Step(before);
            }
            f.Read = g.Alpha;
            if (!tweenFirst)
            {
                g.Step(before);
            }
            if (!alive)
            {
                frames.Add(f);
                return frames;
            }
            alive = run.MoveNext();
            f.Settled = g.Settled;
            frames.Add(f);
            before = g.FrameLength;
            t += g.FrameLength;
            g.Clock = t;
        }
    }

    // The pace over the frames, and the old rule beside it: each search, with what was on the screen while it ran.
    private static void RunPace(List<FadeFrameState> frames, SearchPace pace, List<FadeFrameState> made, List<FadeFrameState> madeBefore, ref float oldSince, ref int oldMade)
    {
        for (int i = 0; i < frames.Count; i++)
        {
            FadeFrameState f = frames[i];
            bool black = f.Dark && f.Read >= 0.99f;   // SceneFinds.Black: the overlay at full strength
            if (pace.Due(f.Dark, black, f.At))
            {
                made.Add(f);
            }
            // Before (up to 0.47.0): half a second into the stretch, and two seconds after that, whatever was on the screen.
            if (!f.Dark)
            {
                oldSince = -1f;
                oldMade = 0;
            }
            else
            {
                if (oldSince < 0f)
                {
                    oldSince = f.At;
                }
                float into = f.At - oldSince;
                if ((oldMade == 0 && into >= SearchPace.First) || (oldMade == 1 && into >= SearchPace.Second))
                {
                    oldMade++;
                    madeBefore.Add(f);
                }
            }
            if (f.Settled)
            {
                pace.Arrived();   // CharacterQuality.Arrived, on the game's onEnterBuildingDelayed
            }
        }
    }

    private static void DoorFadeChecks()
    {
        foreach (bool tweenFirst in new[] { false, true })
        {
            string order = tweenFirst ? " (the game's tween stepped before the mod's Update)" : " (the mod's Update first)";
            FadeEntry(tweenFirst, order);
            FadeExit(tweenFirst, order);
            FadeTaxi(tweenFirst, order);
        }
        FadePlay();
        FadeSession();
        FadeSources();
        FaceFadeChecks();
    }

    // A walk into a building, its load one frame to forty: every search with the screen black, one of them after the game
    // says the building is in, none once its fade back in has begun; the old rule seen through the fade back in.
    private static void FadeEntry(bool tweenFirst, string order)
    {
        foreach (int load in new[] { 1, 3, 12, 40 })
        {
            List<FadeFrameState> frames = FadeModel(g => GameEnter(g, load), tweenFirst, 100f);
            var made = new List<FadeFrameState>();
            var before = new List<FadeFrameState>();
            float oldSince = -1f;
            int oldMade = 0;
            RunPace(frames, new SearchPace(), made, before, ref oldSince, ref oldMade);
            float settledAt = float.MaxValue;
            foreach (FadeFrameState f in frames)
            {
                if (f.Settled)
                {
                    settledAt = f.At;
                }
            }
            bool allBlack = true, afterSettled = false, beforeFadeIn = true;
            foreach (FadeFrameState f in made)
            {
                allBlack &= f.Shown >= 0.99f && f.Read >= 0.99f;
                afterSettled |= f.At > settledAt;
                beforeFadeIn &= !f.InView;
            }
            string what = "door pace: a walk into a building, its load " + load + " frame(s)" + order;
            Check(made.Count >= 1 && made.Count <= SearchPace.Most && allBlack, what + ": every search with the screen fully black (" + made.Count + ")");
            Check(afterSettled && beforeFadeIn, what + ": one after the game says the building is in, none once its fade back in has begun");
            if (load <= 3)
            {
                bool seen = false;
                foreach (FadeFrameState f in before)
                {
                    seen |= f.Shown < 0.99f;
                }
                Check(seen, what + ": the old rule searched with the room showing through the fade back in");
            }
        }
    }

    // A walk out: after the city's frame of 230 ms the fade back in is over in a step; no search with the street showing.
    private static void FadeExit(bool tweenFirst, string order)
    {
        List<FadeFrameState> frames = FadeModel(GameExit, tweenFirst, 200f);
        var made = new List<FadeFrameState>();
        var before = new List<FadeFrameState>();
        float oldSince = -1f;
        int oldMade = 0;
        RunPace(frames, new SearchPace(), made, before, ref oldSince, ref oldMade);
        bool allBlack = true;
        foreach (FadeFrameState f in made)
        {
            allBlack &= f.Shown >= 0.99f && f.Read >= 0.99f;
        }
        Check(allBlack && made.Count <= 1, "door pace: a walk out of a building" + order + ": no search with the street showing through the fade (" + made.Count + " made)");
    }

    // A taxi: a fade of a second, three seconds of black, a fade back of 0.4 s: two searches, both behind the black screen.
    private static void FadeTaxi(bool tweenFirst, string order)
    {
        List<FadeFrameState> frames = FadeModel(GameTaxi, tweenFirst, 300f);
        var made = new List<FadeFrameState>();
        var before = new List<FadeFrameState>();
        float oldSince = -1f;
        int oldMade = 0;
        RunPace(frames, new SearchPace(), made, before, ref oldSince, ref oldMade);
        bool allBlack = true;
        foreach (FadeFrameState f in made)
        {
            allBlack &= f.Shown >= 0.95f && f.Read >= 0.99f;   // (a step of 9.6 ms of a fade of a second: the picture at 1% at the most)
        }
        bool seen = false;
        foreach (FadeFrameState f in before)
        {
            seen |= f.Shown < 0.95f;
        }
        Check(made.Count == SearchPace.Most && allBlack, "door pace: a taxi's ride behind the black screen" + order + ": two searches, both with the screen black (" + made.Count + ")");
        Check(seen, "door pace: the old rule searched a taxi's ride half a second in, the street still showing through its fade" + order);
    }

    // Play, a plain pause, and a call of the game's with no fade (the game goes in without one on its way to the hospital).
    private static void FadePlay()
    {
        var pace = new SearchPace();
        bool any = false;
        for (int f = 0; f < 6000; f++)
        {
            any |= pace.Due(false, false, f * 0.01f);
        }
        Check(!any && pace.Made == 0, "door pace: a minute of play, no search");
        pace.Arrived();
        any |= pace.Due(false, false, 61f);
        bool later = false;
        for (float t = 62f; t < 62.4f; t += 0.01f)
        {
            later |= pace.Due(true, t >= 62.2f, t);   // a fade of 0.2 s later on: nothing before the time
        }
        Check(!any && !later, "door pace: the game's call with no fade is forgotten, and asks for nothing at the next black screen");
        // Black at full strength for a long time (a loading screen of twenty seconds), after play: two searches, not forty.
        pace.Due(false, false, 99f);
        int made = 0;
        float second = -1f;
        for (float t = 100f; t < 120f; t += 0.01f)
        {
            if (pace.Due(true, true, t))
            {
                made++;
                second = t - 100f;
            }
        }
        Check(made == SearchPace.Most && second >= SearchPace.Second && second < SearchPace.Second + 0.02f,
            "door pace: a long black screen, two searches at the most (" + made + ", the second at " + second.ToString("F2") + " s)");
        // The game's call after both: no third.
        pace.Arrived();
        Check(!pace.Due(true, true, 120.01f), "door pace: a building in after two searches in one stretch makes no third");
    }

    // Ten minutes like the session of 21 Oct 2026: a walk into a building every 40 seconds, the load two frames, in play
    // between. Before: the search seen on every walk in. After: none seen, one behind each.
    private static void FadeSession()
    {
        var pace = new SearchPace();
        float oldSince = -1f;
        int oldMade = 0;
        int seenBefore = 0, seenAfter = 0, madeAfter = 0, entries = 0;
        float t = 0f;
        var play = new List<FadeFrameState>(1) { default };
        var none = new List<FadeFrameState>();
        var noneBefore = new List<FadeFrameState>();
        while (t < 600f)
        {
            for (float until = t + 30f; t < until; t += FadeFrame)
            {
                play[0] = new FadeFrameState { At = t };
                RunPace(play, pace, none, noneBefore, ref oldSince, ref oldMade);
            }
            seenAfter += none.Count;
            seenBefore += noneBefore.Count;
            none.Clear();
            noneBefore.Clear();
            List<FadeFrameState> walkIn = FadeModel(g => GameEnter(g, 2), entries % 2 == 0, t);
            entries++;
            var made = new List<FadeFrameState>();
            var before = new List<FadeFrameState>();
            RunPace(walkIn, pace, made, before, ref oldSince, ref oldMade);
            foreach (FadeFrameState f in made)
            {
                madeAfter++;
                seenAfter += f.Shown < 0.99f ? 1 : 0;
            }
            foreach (FadeFrameState f in before)
            {
                seenBefore += f.Shown < 0.99f ? 1 : 0;
            }
            t = walkIn[walkIn.Count - 1].At + 10f;
        }
        Check(seenAfter == 0 && madeAfter >= entries && madeAfter <= entries * SearchPace.Most,
            "door pace: ten minutes with " + entries + " walks into a building, no search seen (" + seenAfter + "), one or two behind each (" + madeAfter + ")");
        Check(seenBefore >= entries, "door pace: the old rule was seen on every walk in (" + seenBefore + " of " + entries + ")");
        Console.WriteLine("door pace: the hair finish's search of the whole scene (some 90 ms in the city) on " + entries + " walks into a building: before "
                          + seenBefore + " with the room showing through the fade back in, after " + seenAfter + " (" + madeAfter + " behind the black screen at full strength)");
    }

    // Over the sources: who asks the pace and with what, and the black screen read only while the game fades.
    private static void FadeSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string quality = Source(scripts, @"Character\CharacterQuality.cs");
        Check(quality.Contains("_othersPace.Due(SceneFinds.Dark, SceneFinds.Black, Time.unscaledTime)"),
            "door pace: the hair finish searches the whole scene only on a frame the screen shows black (SceneFinds.Black)");
        Check(Body(quality, "private void HoldArrival()").Contains("EventHold.Keep(ref GlobalEvents.onEnterBuildingDelayed, _onArrived, ref _arrivedSeen)")
              && Body(quality, "private void Arrived(Address _)").Contains("_othersPace.Arrived()")
              && quality.Contains("_onArrived = Guard.Wrap<Address>(Arrived)")
              && Body(quality, "private void OnDestroy()").Contains("GlobalEvents.onEnterBuildingDelayed -= _onArrived"),
            "door pace: the game's settled call is held through the guard, made once, taken off when the city goes, and tells the pace");
        int searches = 0;
        foreach (string line in quality.Split('\n'))
        {
            searches += line.Contains("SceneFinds.All<AppearanceSetter>(") ? 1 : 0;
        }
        Check(searches == 1, "door pace: the hair finish has one search of the whole scene, the paced one (" + searches + ")");
        string finds = Source(scripts, @"Core\SceneFinds.cs");
        string covered = Body(finds, "public static bool Covered");
        int flag = covered.IndexOf("!UI.UiFader.isFading", StringComparison.Ordinal);
        Check(flag >= 0 && flag < covered.IndexOf("OverlayAlpha()", StringComparison.Ordinal) && covered.Contains("catch"),
            "door pace: the black screen's strength is read only while the game fades, in a method a build without it cannot break");
        string alpha = Body(finds, "private static float OverlayAlpha()");
        Check(alpha.Contains("InstanceBehavior<UI.UIs>.IsInitialized") && !alpha.Contains("FindObject") && !alpha.Contains("FindAny"),
            "door pace: the game's overlay is taken from its register of singletons, never searched for");
        Check(finds.Contains("public static bool Black => UI.Load.LoadScene.isLoading || Covered;"), "door pace: black is the loading screen or the black screen at full strength");
        string prewarm = Body(finds, "private void OnUpdate()");
        int wait = prewarm.IndexOf("!SceneFinds.Black", StringComparison.Ordinal);
        Check(wait >= 0 && wait < prewarm.IndexOf("RunOne()", StringComparison.Ordinal),
            "door pace: the first minute's one-off work left for the street waits for the screen fully black, not its fade (a piece a frame, RLO-PERF-024)");
        string said = Body(finds, "private static void Said(");
        Check(said.Contains("Covered") && said.Contains("with the picture showing through: FAIL") && !said.Contains("bool hidden = Hidden;"),
            "door pace: a search during the game's fade says FAIL in the log, not PASS");
    }
}
