using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The traffic system's road data (every lane point of the city, its junctions and lights), found once per city
    /// and kept for everyone: the GPS, the bus network, the taxi, the road map the police use and the driving school.
    ///
    /// The traffic system's own way to it, CurrentSceneData.GetSceneInstance(), searches the whole scene on every
    /// call (Object.FindObjectsOfType), and each of the five used to call it for itself: the road map the first time
    /// it was wanted in a city and the driving school at the start of every lesson and every test, both in the
    /// middle of play, a frame of over 100 ms each time. Now there is one search per city, made behind the loading
    /// screen (Core/SceneFinds, ScenePrewarm), and every later question is one comparison.
    /// </summary>
    public static class RoadData
    {
        private const float RetryAfter = 5f;   // seconds before a scene that had no road data is searched again (a search is a long frame)

        private static CurrentSceneData _scene;
        private static float _retryAt;

        /// <summary>The road data, or null where the scene has none (yet). The object goes with the city's scene and is then looked for again.</summary>
        public static CurrentSceneData Scene
        {
            get
            {
                if (_scene == null && Time.unscaledTime >= _retryAt)
                {
                    CurrentSceneData[] found = SceneFinds.All<CurrentSceneData>("roads: the traffic system's road data being looked for (a search of the whole scene, once a city)", FindObjectsInactive.Include);
                    // The one the traffic system itself takes is the one that is switched on; a spare without lane points is no use to anyone.
                    _scene = null;
                    for (int i = 0; i < found.Length; i++)
                    {
                        CurrentSceneData one = found[i];
                        if (one == null || one.allWaypoints == null || one.allWaypoints.Length == 0)
                        {
                            continue;
                        }
                        if (_scene == null || (one.gameObject.activeInHierarchy && !_scene.gameObject.activeInHierarchy))
                        {
                            _scene = one;
                        }
                    }
                    _retryAt = _scene == null ? Time.unscaledTime + RetryAfter : 0f;
                }
                return _scene;
            }
        }

        /// <summary>Every lane point of the city, or null where there is no road data.</summary>
        public static Waypoint[] Points
        {
            get
            {
                CurrentSceneData scene = Scene;
                return scene != null ? scene.allWaypoints : null;
            }
        }

        /// <summary>Behind the loading screen: the one search of this city. Always done with (a later question asks again if nothing was there).</summary>
        public static bool Prewarm()
        {
            _retryAt = 0f;
            _scene = null;
            _ = Scene;
            return true;
        }
    }
}
