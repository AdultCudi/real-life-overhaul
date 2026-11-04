using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the city map openable whatever happened to a tree. The game's map handler (TreeController.ToggleMapLod) asks every
    /// tree in TreeController.AllTrees for its game object, and the game never takes a destroyed tree out of that list, so one
    /// destroyed tree made the handler throw inside the map's open coroutine: M only tinted the screen and the map never
    /// opened (0.60.0). This puts a handler of its own FIRST in the map's event (<see cref="EventHold.First"/>: the game's
    /// handlers are added at the end, and the game empties the event on every load, so it is looked at again whenever the field
    /// changed) that runs <see cref="SceneryTrees.Prune"/> before the game's handler does. That covers a tree destroyed by
    /// the mod's paths that missed <see cref="SceneryTrees.Forget"/>, by another mod, or by the game. Being first, it also tells
    /// the session record that the map opens or shuts before anything is changed for it (<see cref="SessionWatch.MapToggled"/>).
    /// Also Options > Tests > Check the city map with a destroyed tree. COST: a reference comparison a frame; on each opening or
    /// closing of the map one backward walk of the tree list (about eight thousand Unity null tests, no allocation); with
    /// Developer diagnostics on, its check (a second walk) and line the frame after an opening, out of the frame the map switches in.
    /// Its frame also runs the slices of the parts' work for the map's last change that wait for the frames after it (Core/MapParts.After).
    /// </summary>
    public sealed class MapTreeGuard : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "MapTreeGuard", typeof(MapTreeGuard));
        private static readonly int HotUpdate = HotSpots.Slot("Core: MapTreeGuard.Update");

        public static IModLogger Logger;

        private const float TestWait = 2.5f;      // real seconds the map is given to open or shut in the test (the map pauses the game)
        private const float MenuWait = 60f;       // real seconds the test waits for the menu to be closed

        private static MapTreeGuard _instance;
        private static Action<bool> _onMap;
        private static Action<bool> _seen;
        private static bool _testing;

        private void Awake()
        {
            _instance = this;
            _onMap = _onMap ?? Guard.Wrap<bool>(FrameGuard, OnMapToggled);
            _seen = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap);
                _seen = null;
            }
        }

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
                // the game empties its events on every load: first again when the field is not as this last saw it
                EventHold.First(ref GlobalEvents.onCityMapToggle, _onMap, ref _seen);
                if (MapParts.Waiting > 0)
                {
                    MapParts.RunLater();   // the parts' work for the map's last change, a slice a frame after it
                }
                if (_taken >= 0 && Time.frameCount > _takenIn)
                {
                    Said();
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // Runs before the game's own handler on every opening and closing of the map: the one pass that keeps the map
        // openable. Its check and line wait for the next frame, out of the frame the map switches in.
        private static void OnMapToggled(bool open)
        {
            SessionWatch.MapToggled(open);   // the player's camera as it was, and the frame (nothing while the record is off)
            int taken = SceneryTrees.Prune();
            if (open && Meta.Dev.Verbose)
            {
                _taken = taken;
                _takenIn = Time.frameCount;
            }
        }

        private static int _taken = -1;   // dead trees the last opening of the map took out, until its line is said; -1 for none to say
        private static int _takenIn;      // ...and the frame it opened in

        // The frame after the map opened, with Developer diagnostics on: a second pass to see that none is left, and the line.
        private static void Said()
        {
            int taken = _taken;
            _taken = -1;
            int left = SceneryTrees.Dead();
            Logger?.Info("Map check (trees): " + taken + " dead tree(s) taken out of the game's list of " + TreeController.AllTrees.Count
                + " before the map opened; " + SceneryTrees.Forgotten + " more taken out as the mod destroyed their objects since the start; "
                + left + " dead left in it a frame after" + (left == 0 ? ": PASS" : ": FAIL"));
        }

        /// <summary>Options > Tests: copies a tree, destroys the copy the plain way, then opens and shuts the map and says in Player.log whether it opened.</summary>
        public static void RunTest()
        {
            if (_instance == null || SaveGameManager.Current == null || InstanceBehavior<CityManager>.Instance == null)
            {
                Say(NotificationType.Warning, "reallife_maptrees_test_none");
                return;
            }
            if (_testing)
            {
                return;
            }
            _instance.StartCoroutine(Guard.Routine(_instance.Test()));
        }

        private IEnumerator Test()
        {
            _testing = true;
            try
            {
                CityMap map = InstanceBehavior<CityManager>.Instance.cityMap;
                TreeController template = null;
                List<TreeController> all = TreeController.AllTrees;
                for (int i = 0; i < all.Count && template == null; i++)
                {
                    if (all[i] != null && all[i].gameObject.activeInHierarchy)
                    {
                        template = all[i];
                    }
                }
                if (map == null || template == null)
                {
                    Line("no tree or no map to test with", false);
                    Say(NotificationType.Warning, "reallife_maptrees_test_none");
                    yield break;
                }
                Say(NotificationType.Info, "reallife_maptrees_test_start");
                for (float waited = 0f; !CityMap.CanOpenMap() && waited < MenuWait; waited += Time.unscaledDeltaTime)
                {
                    yield return null;   // the menu is open: the map cannot be
                }
                if (!CityMap.CanOpenMap() || CityMap.IsOpen)
                {
                    Line("the map could not be opened (a menu or the map itself was open)", false);
                    Say(NotificationType.Warning, "reallife_maptrees_test_none");
                    yield break;
                }
                // A copy of the tree, woken, so the game lists it; then destroyed the plain way, as a path that missed Forget would
                // (its tree left in the tree list on purpose; out of the culling lists first, as the test is about the tree list).
                GameObject copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                copy.name = "RealLife map tree test";
                yield return null;
                yield return null;
                bool listed = all.Contains(copy.GetComponent<TreeController>());
                SceneryCulling.Forget(copy);
                UnityEngine.Object.Destroy(copy);
                yield return null;
                yield return null;
                int dead = SceneryTrees.Dead();
                int prunedBefore = SceneryTrees.Pruned;
                // Open the map the way M does and see whether the game's open coroutine ran to its end (it sets the camera's mask last of what matters here).
                bool opened = false;
                bool shut = false;
                try
                {
                    map.Toggle(Vector3.zero, false, true);
                    yield return new WaitForSecondsRealtime(TestWait);
                    Camera main = GameManager.GetMainCamera();
                    int expected = (PlayerPrefSettings.LowDetailCityMap ? map.cityMapMaskLowDetail : map.cityMapMask).value;
                    opened = CityMap.IsOpen && main != null && main.cullingMask == expected;
                    map.Close();
                    yield return new WaitForSecondsRealtime(TestWait);
                    shut = !CityMap.IsOpen;
                }
                finally
                {
                    if (CityMap.IsOpen)
                    {
                        map.Close();
                    }
                }
                int pruned = SceneryTrees.Pruned - prunedBefore;
                int left = SceneryTrees.Dead();
                bool pass = listed && dead >= 1 && opened && shut && left == 0;
                Line("a copy of a tree " + (listed ? "listed" : "NOT listed") + " by the game, destroyed the plain way (" + dead + " dead in its list); the map "
                    + (opened ? "opened" : "did NOT open") + " and " + (shut ? "shut" : "did NOT shut") + " again, "
                    + pruned + " dead tree(s) pruned before it, " + left + " left", pass);
                Say(pass ? NotificationType.Success : NotificationType.Error, pass ? "reallife_maptrees_test_pass" : "reallife_maptrees_test_fail");
            }
            finally
            {
                _testing = false;
            }
        }

        private static void Line(string said, bool pass)
        {
            Meta.Dev.Always(Logger, "Map check (trees, test): " + said + ": " + (pass ? "PASS" : "FAIL"));
        }

        private static void Say(NotificationType type, string key)
        {
            Notifications.Show(type, key, null, 8f, null, null, true, false);
        }
    }
}
