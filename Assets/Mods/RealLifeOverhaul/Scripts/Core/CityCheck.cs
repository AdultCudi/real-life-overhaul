using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// THE CITY CHECK: at every city load, whether the loaded city is the New York the mod knows (Core/CityRules decides,
    /// from the game's own data). In another city (a DLC's) every feature the city table calls New York's
    /// (Meta/FeatureCities) stands down for that city with one log line: its components are not started
    /// (<see cref="Guard.Add{T}"/>), every guard of it is away (<see cref="FeatureGuards.SetAway"/>), so its entry points,
    /// handlers, routines and save lines do nothing; the rest of the mod runs. Back in New York they run again.
    /// HOW A CITY LOADS: the game loads a city's scenes behind its loading screen (UI.Load.LoadingScreen), then the mods'
    /// city scope, then calls its "game loaded" event, at which CityManager lists the city's buildings. Every city load of
    /// today unloads and loads the mod's city part, so the check runs:
    ///   1. in the city part's start (<see cref="Begin"/>), before any feature: the scenes loaded, and the city's buildings
    ///      if CityManager has them already;
    ///   2. in the game's late "game loaded" call, the loading screen still up and no feature's first tick yet: the
    ///      districts of the buildings CityManager has just listed, which decide;
    ///   3. should the game ever load or unload a scene while the city part runs (a city changed without the mods' city
    ///      scope being loaded again), once the loading is over (<see cref="CityCheck"/>'s watch).
    /// A verdict that changes stands the New York features down (their clean-up, their components off) or brings them back.
    /// The districts of the loaded city are kept for the tables by district (<see cref="Here"/>). Nothing is read of DLC
    /// ownership and nothing is sent: in a Going Public session every machine in a city finds the same.
    /// COST: per city load, a walk over the loaded scenes and the city's building list; per frame nothing (the watch is
    /// disabled unless a scene changed under the running city part, then one flag test a frame until the loading is over).
    /// </summary>
    public sealed class CityCheck : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "CityCheck", typeof(CityCheck));   // its Update

        public static IModLogger Logger;

        private static readonly CityCensus Census = new CityCensus();
        private static readonly CityVerdict Verdict = new CityVerdict();
        private static readonly List<string> Away = new List<string>(24);
        private static readonly List<string> Went = new List<string>(24);
        private static readonly List<string> Back = new List<string>(24);
        private static readonly List<Behaviour> Stood = new List<Behaviour>(32);   // components it switched off in this city, to switch on again
        private static readonly HashSet<string> NotStarted = new HashSet<string>(StringComparer.Ordinal);   // New York features whose parts this start left out

        private static GameObject _root;
        private static CityCheck _watch;
        private static bool _settled;    // the game's late "game loaded" call has come for this city part
        private static bool _running;    // between Begin and End
        private static string _lastLine;
        private static Action _late;
        private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> _loaded;
        private static UnityEngine.Events.UnityAction<Scene> _unloaded;

        /// <summary>What the loaded city is; Unknown before the first check.</summary>
        public static CityKind Kind => Verdict.Kind;

        /// <summary>True while the New York features run: in New York, a New York with new districts, or a city not read.</summary>
        public static bool NewYork => Verdict.NewYorkFeatures;

        /// <summary>
        /// True for a district with a building in the loaded city; for every district while the city's buildings are not
        /// listed. A table by district reads it to leave out a district of the game's data that is in another city.
        /// </summary>
        public static bool Here(string district) => Census.Here(district);

        /// <summary>Called by Core/Guard when it leaves out a component of a New York feature in this city.</summary>
        internal static void LeftOut(string feature)
        {
            if (feature != null)
            {
                NotStarted.Add(feature);
            }
        }

        /// <summary>
        /// Once in the city part's start, before any feature is added: the first verdict, from the scenes (and the city's
        /// buildings if they are listed already); then the game's late "game loaded" call, and the engine's scene events.
        /// </summary>
        public static void Begin(GameObject root, IModLogger logger)
        {
            Logger = logger;
            _root = root;
            _settled = false;
            _running = true;
            _lastLine = null;
            Stood.Clear();
            NotStarted.Clear();
            Decide(false);
            _late = _late ?? Guard.Wrap(OnGameLoaded);
            GlobalEvents.RegisterOnGameLoadedLateCallback(_late);   // at once if the game has made its call already
            _loaded = _loaded ?? Guard.Listener<Scene, LoadSceneMode>(OnSceneLoaded);
            _unloaded = _unloaded ?? Guard.Listener<Scene>(OnSceneUnloaded);
            SceneManager.sceneLoaded -= _loaded;
            SceneManager.sceneLoaded += _loaded;
            SceneManager.sceneUnloaded -= _unloaded;
            SceneManager.sceneUnloaded += _unloaded;
            _watch = root != null ? Guard.Add<CityCheck>(root) : null;
            if (_watch != null)
            {
                _watch.enabled = false;
            }
        }

        /// <summary>The city part unloads: the scene events let go, every feature back for the menu and the next city.</summary>
        public static void End()
        {
            _running = false;
            if (_loaded != null)
            {
                SceneManager.sceneLoaded -= _loaded;
            }
            if (_unloaded != null)
            {
                SceneManager.sceneUnloaded -= _unloaded;
            }
            FeatureGuards.SetAway(null, null, null);
            Stood.Clear();
            _root = null;
            _watch = null;
        }

        private static void OnGameLoaded()
        {
            if (!_running)
            {
                return;
            }
            _settled = true;
            Decide(true);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Changed();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Changed();
        }

        // A scene changed under the running city part: asked again once the loading is over. Before the game's late call it
        // is that call that decides.
        private static void Changed()
        {
            if (_running && _settled && _watch != null)
            {
                _watch.enabled = true;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: CityCheck.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (UI.Load.LoadScene.isLoading)
            {
                return;   // the scenes are still changing
            }
            enabled = false;
            Decide(true);
        }

        // Reads the city, decides, and stands the New York features down or brings them back; before the features are added
        // (started false) only their guards are set, as nothing of theirs is out yet.
        private static void Decide(bool started)
        {
            Census.Clear();
            try
            {
                ReadScenes();
                ReadBuildings();
                ReadDistricts();
            }
            catch (Exception e)
            {
                if (!Guard.DoesWithout(e, "the city check's reading of the city", "the mod runs as in New York"))
                {
                    Logger?.Error(e);
                }
            }
            CityRules.Decide(Census, Verdict);
            Apply(started);
            string line = CityRules.Line(Verdict, Census);
            if (line == _lastLine)
            {
                return;
            }
            _lastLine = line;
            if (Verdict.Kind == CityKind.NewYork)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        private static void ReadScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    Census.Scenes.Add(scene.name);
                }
            }
        }

        private static void ReadBuildings()
        {
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            CityBuildingController[] all = GameDlc.Open(city);   // what a DLC keeps is not counted: the same on every machine
            if (all == null)
            {
                return;
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].building != null)
                {
                    Census.Add(all[i].building.Neighbourhood);   // a building of a city unloaded since is gone, and not counted
                }
            }
        }

        private static void ReadDistricts()
        {
            List<string> known = NeighborhoodHelper.Neighborhoods;
            if (known == null)
            {
                return;
            }
            for (int i = 0; i < known.Count; i++)
            {
                Census.GameDistricts.Add(known[i]);
            }
        }

        private static void Apply(bool started)
        {
            Away.Clear();
            if (!Verdict.NewYorkFeatures)
            {
                Meta.FeatureCities.NewYork(Away);
            }
            Went.Clear();
            Back.Clear();
            FeatureGuards.SetAway(Away, Went, Back);
            if (Went.Count > 0 && started)
            {
                StandDown();
            }
            if (Back.Count > 0)
            {
                ComeBack();
            }
        }

        // The New York features that went away after they started: what they have out is put away, their components off.
        private static void StandDown()
        {
            Went.Sort(ByCleanupOrder);
            for (int i = 0; i < Went.Count; i++)
            {
                Meta.Feature feature = Meta.Features.Find(Went[i]);
                if (feature == null || feature.CleanupAction == null)
                {
                    continue;
                }
                try
                {
                    feature.CleanupAction();
                }
                catch (Exception e)
                {
                    Logger?.Warn("City check: the clean-up of '" + Went[i] + "' failed as it stood down; the rest stood down.");
                    Logger?.Error(e);
                }
            }
            if (_root == null)
            {
                return;
            }
            Behaviour[] parts = _root.GetComponents<Behaviour>();
            for (int i = 0; i < parts.Length; i++)
            {
                Behaviour part = parts[i];
                FeatureGuard guard = part != null ? Guard.OfOwner(part.GetType()) : null;
                if (guard != null && guard.Feature != null && Went.Contains(guard.Feature) && part.enabled)
                {
                    part.enabled = false;
                    Stood.Add(part);
                }
            }
        }

        // The New York features back: the components this city switched off on again, unless switched off since for faults.
        private static void ComeBack()
        {
            for (int i = Stood.Count - 1; i >= 0; i--)
            {
                Behaviour part = Stood[i];
                FeatureGuard guard = part != null ? Guard.OfOwner(part.GetType()) : null;
                if (part == null || guard == null || guard.Feature == null || !Back.Contains(guard.Feature))
                {
                    continue;
                }
                Stood.RemoveAt(i);
                if (!guard.Off)
                {
                    part.enabled = true;
                }
            }
            var later = new List<string>(4);
            for (int i = 0; i < Back.Count; i++)
            {
                if (NotStarted.Remove(Back[i]))
                {
                    later.Add(Back[i]);
                }
            }
            if (later.Count > 0)
            {
                Logger?.Warn("City check: back in New York without a new start of the mod's city part; " + string.Join(", ", later)
                             + " were not started in the other city and start when the save is next loaded.");
            }
        }

        private static readonly Comparison<string> ByCleanupOrder = (a, b) =>
        {
            Meta.Feature fa = Meta.Features.Find(a), fb = Meta.Features.Find(b);
            int order = (fa != null ? fa.CleanupOrder : 0).CompareTo(fb != null ? fb.CleanupOrder : 0);
            return order != 0 ? order : string.CompareOrdinal(a, b);
        };
    }
}
