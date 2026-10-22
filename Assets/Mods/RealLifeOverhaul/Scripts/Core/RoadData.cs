using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The traffic system's road data (every lane point, junction and light), found once per city and kept for every
    /// feature. The system's own CurrentSceneData.GetSceneInstance() searches the whole scene on every call (over
    /// 100 ms), so the one search is made behind the loading screen and later questions are one comparison. In play the
    /// scene is never searched: a city without road data is asked again only behind the next black screen or pause.
    /// </summary>
    public static class RoadData
    {
        private const float RetryAfter = 5f;   // seconds before a scene that had no road data is searched again

        private static CurrentSceneData _scene;
        private static float _retryAt;

        /// <summary>The road data, or null where the scene has none yet; looked for again once it goes with its scene.</summary>
        public static CurrentSceneData Scene
        {
            get
            {
                if (_scene == null && SceneFinds.Hidden && Time.unscaledTime >= _retryAt)
                {
                    CurrentSceneData[] found = SceneFinds.All<CurrentSceneData>("roads: the traffic system's road data being looked for (a search of the whole scene, once a city)", FindObjectsInactive.Include);
                    // The traffic system takes the one switched on; a spare without lane points is of no use.
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

        /// <summary>Every lane point of the city, or null with no road data.</summary>
        public static Waypoint[] Points
        {
            get
            {
                CurrentSceneData scene = Scene;
                return scene != null ? scene.allWaypoints : null;
            }
        }

        /// <summary>
        /// Behind the loading screen: the city's one search, unless an earlier piece made it (the last city's object went
        /// with its scene). Always done.
        /// </summary>
        public static bool Prewarm()
        {
            if (_scene == null)
            {
                _retryAt = 0f;
                _ = Scene;
            }
            return true;
        }
    }
}
