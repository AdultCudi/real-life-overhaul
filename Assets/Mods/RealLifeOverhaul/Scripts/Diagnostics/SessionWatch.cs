using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A record of the session for "it stutters after a while": with "Developer diagnostics" on, one line a
    /// minute in Player.log and one line for every frame that took longer than 80 ms. Off (as the mod ships)
    /// this component is disabled: the engine does not call it, and nothing is measured.
    ///
    /// The line a minute:
    ///
    ///   Session 12 min: frames 5210, average 11.5 ms, worst 61 ms, over 33 ms: 4, over 50 ms: 1; managed heap
    ///   412 MB of 610 MB, collections in the minute 3; engine memory 2950 MB; mod objects 345 (118 shown) under 6
    ///   roots; people 2 out, 12 made of 12; city people 3 out, 8 made of 8, city props made 14; looks built in
    ///   the minute 1 (1 while playing), since the city loaded 23 (3 while playing); on foot in the street, game hour 9.
    ///
    /// How to read it over a session: a managed heap that climbs minute after minute with collections coming
    /// more often is memory being used up on a timer; engine memory that climbs is meshes, textures or materials
    /// made and never destroyed; mod objects that climb is something of the mod's that is never put away (they
    /// are pooled: the count should rise while each kind of scene is seen for the first time and then stand
    /// still); "over 33 ms" and "over 50 ms" are the stutters themselves.
    ///
    /// The line for a long frame says what the mod was doing, if one of its large one-off pieces of work was
    /// under way: the places that do such work name it with <see cref="Mark"/> (a character being built, a bus
    /// stop or a precinct front being put out, a scene starting, the save being written). Mark is two writes to
    /// static fields with a constant text: it costs the same whether diagnostics are on or off, and it is never
    /// to be called per frame or with a text that is put together for the call.
    /// </summary>
    public sealed class SessionWatch : MonoBehaviour
    {
        private const float Every = 60f;           // seconds between two lines
        private const float LongFrame = 0.080f;    // seconds: a frame this long gets a line of its own
        private const int LongFramesPerMinute = 12;   // lines for long frames in one minute; the rest are only counted
        private const int MarkLastsFrames = 3;     // a piece of work named this many frames back still explains a long frame (loading finishes a frame or two later)
        private const string RootPrefix = "RealLifeOverhaul";

        public static IModLogger Logger;

        private static SessionWatch _instance;
        private static string _mark;
        private static int _markFrame = -1000;

        private readonly List<GameObject> _roots = new List<GameObject>(64);
        private readonly List<Transform> _under = new List<Transform>(512);
        private readonly StringBuilder _line = new StringBuilder(512);

        private float _since;
        private float _started;
        private int _frames;
        private float _worst;
        private int _over33;
        private int _over50;
        private int _longSaid;
        private int _collections;
        private int _collectionsLastFrame;
        private int _looks;
        private int _looksInPlay;
        private bool _resumed = true;   // the next frame's time is not a frame of play (the record has just started, or a loading screen or a pause has just ended)

        /// <summary>
        /// Names the large piece of work the mod is starting in this frame, for the long-frame line. A constant
        /// text only; never per frame.
        /// </summary>
        public static void Mark(string what)
        {
            _mark = what;
            _markFrame = Time.frameCount;
        }

        /// <summary>Starts or stops the record with the "Developer diagnostics" switch (Meta.Dev.Apply).</summary>
        public static void Apply(bool on)
        {
            if (_instance != null && _instance.enabled != on)
            {
                _instance.enabled = on;
            }
        }

        private void Awake()
        {
            _instance = this;
            enabled = false;   // Meta.Dev.Apply switches it on when the option is
        }

        private void OnEnable()
        {
            _since = Time.unscaledTime;
            _resumed = true;
            if (_started <= 0f)
            {
                _started = Time.unscaledTime;
            }
            Clear();
            _collections = GC.CollectionCount(0);
            _collectionsLastFrame = _collections;
            _looks = Looks.Built;
            _looksInPlay = Looks.BuiltInPlay;
            Logger?.Info("Session: recording. One line a minute, and one for every frame over " + (LongFrame * 1000f).ToString("F0") + " ms.");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Clear()
        {
            _frames = 0;
            _worst = 0f;
            _over33 = 0;
            _over50 = 0;
            _longSaid = 0;
        }

        private void Update()
        {
            // The time the frame before this one took, loading screens and pauses left out: they are not stutter.
            float took = Time.unscaledDeltaTime;
            int collections = GC.CollectionCount(0);
            bool collected = collections != _collectionsLastFrame;
            _collectionsLastFrame = collections;
            if (UI.Load.LoadScene.isLoading || Time.timeScale <= 0f || !Application.isFocused)
            {
                _since += took;   // the minute is a minute of play
                _resumed = true;
                return;
            }
            if (_resumed)
            {
                _resumed = false;   // the time in hand is the last frame of the loading screen or the pause, not a frame of play
                return;
            }
            _frames++;
            if (took > _worst)
            {
                _worst = took;
            }
            if (took > 0.033f)
            {
                _over33++;
                if (took > 0.050f)
                {
                    _over50++;
                }
            }
            if (took > LongFrame)
            {
                Long(took, collected);
            }
            if (Time.unscaledTime - _since >= Every)
            {
                try
                {
                    Minute(collections);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    enabled = false;   // a record must never be the fault
                }
            }
        }

        private void Long(float took, bool collected)
        {
            if (++_longSaid > LongFramesPerMinute)
            {
                return;   // counted in the minute's line; a bad minute does not fill the log
            }
            // The time is that of the frame before this one: work named in this very frame is not what made it long.
            int ago = Time.frameCount - _markFrame;
            bool marked = _mark != null && ago >= 1 && ago <= MarkLastsFrames;
            _line.Length = 0;
            _line.Append("Session: a frame of ").Append((took * 1000f).ToString("F0")).Append(" ms; ");
            _line.Append(marked ? "the mod was at: " : "none of the mod's large pieces of work was under way");
            float cutAgo = Time.unscaledTime - took - MeshCuts.At;
            string cut = cutAgo >= 0f && cutAgo < 2.5f ? MeshCuts.Last : null;
            if (marked)
            {
                _line.Append(_mark);
            }
            if (collected)
            {
                _line.Append("; a garbage collection ran in it");
            }
            if (cut != null)
            {
                _line.Append("; ").Append(cutAgo.ToString("F1")).Append(" s before it the navigation mesh was asked to be cut for ").Append(cut).Append(" (the engine cuts about half a second after)");
            }
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
        }

        private void Minute(int collections)
        {
            float seconds = Time.unscaledTime - _since;
            _since = Time.unscaledTime;
            int objects = 0, shown = 0, roots = 0;
            CountObjects(ref objects, ref shown, ref roots);
            _line.Length = 0;
            _line.Append("Session ").Append(Mathf.RoundToInt((Time.unscaledTime - _started) / 60f)).Append(" min: frames ").Append(_frames)
                .Append(", average ").Append((_frames > 0 ? seconds * 1000f / _frames : 0f).ToString("F1"))
                .Append(" ms, worst ").Append((_worst * 1000f).ToString("F0"))
                .Append(" ms, over 33 ms: ").Append(_over33).Append(", over 50 ms: ").Append(_over50)
                .Append("; managed heap ").Append(Megabytes(GC.GetTotalMemory(false))).Append(" MB of ").Append(Megabytes(Profiler.GetMonoHeapSizeLong()))
                .Append(" MB, collections in the minute ").Append(collections - _collections)
                .Append("; engine memory ").Append(Megabytes(Profiler.GetTotalAllocatedMemoryLong()))
                .Append(" MB; mod objects ").Append(objects).Append(" (").Append(shown).Append(" shown) under ").Append(roots).Append(" roots");
            People(_line);
            // Characters' looks built (a mesh merge each: People/Looks): "while playing" are the ones that could be felt.
            _line.Append("; looks built in the minute ").Append(Looks.Built - _looks).Append(" (").Append(Looks.BuiltInPlay - _looksInPlay)
                .Append(" while playing), since the city loaded ").Append(Looks.Built).Append(" (").Append(Looks.BuiltInPlay).Append(" while playing)");
            _looks = Looks.Built;
            _looksInPlay = Looks.BuiltInPlay;
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
            _collections = collections;
            Clear();
        }

        private static long Megabytes(long bytes) => bytes / (1024L * 1024L);

        // Every object under a root object of the mod's (they are all named "RealLifeOverhaul ..."), in every loaded
        // scene. What the mod hangs on the game's own objects (a prop in a hand, a part on a car) is not counted.
        private void CountObjects(ref int objects, ref int shown, ref int roots)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                scene.GetRootGameObjects(_roots);
                for (int r = 0; r < _roots.Count; r++)
                {
                    GameObject root = _roots[r];
                    if (root == null || !root.name.StartsWith(RootPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    roots++;
                    root.GetComponentsInChildren(true, _under);   // fills the list it is given
                    objects += _under.Count;
                    for (int i = 0; i < _under.Count; i++)
                    {
                        if (_under[i].gameObject.activeInHierarchy)
                        {
                            shown++;
                        }
                    }
                    _under.Clear();
                }
                _roots.Clear();
            }
        }

        // The two pools of characters: how many exist and how many are out.
        private static void People(StringBuilder line)
        {
            ActorPool actors = ActorPool.Instance;
            if (actors != null)
            {
                int made = actors.ActorCount, used = 0;
                for (int i = 0; i < made; i++)
                {
                    used += actors.ActorAt(i).InUse ? 1 : 0;
                }
                line.Append("; people ").Append(used).Append(" out, ").Append(made).Append(" made of ").Append(ActorPool.MaxActors);
            }
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city != null && city.People != null && city.Props != null)
            {
                int made = city.People.Count;
                line.Append("; city people ").Append(made - city.People.Free).Append(" out, ").Append(made).Append(" made of ").Append(CityPeople.MaxPeople)
                    .Append(", city props made ").Append(city.Props.Made);
            }
        }

        private static string Where()
        {
            try
            {
                return (BuildingManager.IsInsideBuilding ? "inside a building" : PerfGame.InVehicle() ? "in a vehicle" : "on foot in the street")
                       + ", game hour " + TimeHelper.CurrentHour;
            }
            catch (Exception)
            {
                return "place unknown";
            }
        }
    }
}
