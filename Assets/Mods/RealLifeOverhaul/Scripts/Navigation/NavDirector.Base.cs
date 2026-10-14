using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The director's part for the city under the minimap's roads (NavBase: buildings and water): when it is made,
    /// when the panel is given it, its two checks and its three tests.
    ///
    /// Cost: nothing of this runs per frame (the one test of NavBase.Busy is in Update). On the slow tick, four times
    /// a second: three comparisons while nothing has changed. With the option off the picture is let go and the
    /// panel is exactly what it is without it.
    /// </summary>
    public sealed partial class NavDirector
    {
        private const float AloneSeconds = 20f;     // the test "show the base alone" puts the roads back after this long
        private const float TrialSeconds = 600f;    // the test's three markers go after this long
        private const float MisfitMost = 1f;        // metres the picture and the markers may disagree by
        private const float DoorMost = 8f;          // metres a building's door may lie outside its own outline (a porch, steps)

        private readonly NavBase _base = new NavBase();
        private readonly int[] _three = new int[3];
        private bool _baseGiven, _baseChecked, _baseAgain;
        private float _aloneUntil, _trialUntil;

        // On the slow tick, loading screen or not: the picture is begun as soon as the roads are there (from the mod's
        // folder that is done under the loading screen), and let go when its option or the minimap is switched off.
        private void BaseWanted(bool wantMap)
        {
            if (!wantMap || !Settings.MinimapBase)
            {
                if (!_base.Idle)
                {
                    _base.Drop(true);   // switched off: its memory on the graphics card goes too
                    BaseOff();
                }
                return;
            }
            if (_base.Idle && _graph.Finished)
            {
                _base.Begin(_graph, _baseAgain);
                _baseAgain = _baseAgain && _base.Idle;   // (still idle: the game's palette was not there yet, asked again on the next tick)
            }
        }

        private void BaseOff()
        {
            _baseGiven = false;
            _baseChecked = false;
            _aloneUntil = 0f;
            if (_hud != null)
            {
                _hud.SetBase(null, 0f, 0f, 0f, 0f);
                _hud.Alone(false);
            }
        }

        // On the slow tick with a save loaded: the panel gets the picture once, the checks are written once, and what a test put out is taken back.
        private void BaseShown(float now)
        {
            if (_base.Picture != null)
            {
                if (_hud != null && !_baseGiven)
                {
                    _baseGiven = true;
                    _hud.SetBase(_base.Picture, _base.OriginX, _base.OriginZ, _base.WorldWidth, _base.WorldHeight);
                }
                if (!_baseChecked && Meta.Dev.Verbose)
                {
                    _baseChecked = true;
                    BaseCheck();
                }
            }
            if (_aloneUntil > 0f && now >= _aloneUntil)
            {
                _aloneUntil = 0f;
                if (_hud != null)
                {
                    _hud.Alone(false);
                }
            }
            if (_trialUntil > 0f && now >= _trialUntil)
            {
                _trialUntil = 0f;
                NavMarks.Trial.Clear();
                _nextMarks = 0f;
            }
        }

        // Two lines, once per picture: what it is, and whether three buildings far from one another (and the player)
        // are drawn where the markers' layer puts them.
        private void BaseCheck()
        {
            NavBaseData data = _base.Data;
            if (data == null)
            {
                return;
            }
            CultureInfo plain = CultureInfo.InvariantCulture;
            int water = 0;
            for (int i = 0; i < data.Cells.Length; i++)
            {
                water += data.Cells[i] == NavBaseWater.Water ? 1 : 0;
            }
            bool sized = _base.Width <= NavBaseMath.LongestSide && _base.Height <= NavBaseMath.LongestSide && data.Hulls > 0;
            Logger?.Info("Minimap check (base): drawn from the game's own buildings and ground (the game's city map is the city under a camera, not a picture): "
                         + data.Hulls + " buildings, " + water + " squares of water of " + NavBaseWater.CellMetres.ToString("F0", plain) + " m, " + _base.Width + " x " + _base.Height
                         + ", world rectangle (" + _base.OriginX.ToString("F1", plain) + ", " + _base.OriginZ.ToString("F1", plain) + ") to ("
                         + (_base.OriginX + _base.WorldWidth).ToString("F1", plain) + ", " + (_base.OriginZ + _base.WorldHeight).ToString("F1", plain) + "), shown at 1 pixel = "
                         + (1f / _base.PerMetre).ToString("F2", plain) + " m, " + (_base.Width * _base.Height * 4 / 1048576f).ToString("F1", plain) + " MB, "
                         + (_base.FromFile ? "read from the mod's folder" : "measured in this session") + ": "
                         + (sized ? "PASS" : "FAIL (wanted at most " + NavBaseMath.LongestSide + " pixels a side and at least one building)"));

            CityBuildingController[] buildings = NavBaseGame.Buildings();
            if (buildings == null)
            {
                return;
            }
            var doorX = new float[data.Hulls];
            var doorZ = new float[data.Hulls];
            for (int h = 0; h < data.Hulls; h++)
            {
                int index = data.HullOf[h];
                NavBaseGame.Door(index >= 0 && index < buildings.Length ? buildings[index] : null, out doorX[h], out doorZ[h]);
            }
            int picked = NavBaseMath.FarApart(doorX, doorZ, data.Hulls, _three);
            float metres = NavView.MetresAcross(Settings.MinimapZoom, 0f) * 1.5f;   // the turned square that holds the view
            float worstFit = NavBaseMath.Misfit(_px, _pz, _px, _pz, _base.OriginX, _base.OriginZ, _base.WorldWidth, _base.WorldHeight, metres);
            float worstDoor = 0f;
            string names = string.Empty;
            for (int i = 0; i < picked; i++)
            {
                int h = _three[i];
                // Seen by a player standing 60 m from the door: both are then in the view.
                worstFit = Mathf.Max(worstFit, NavBaseMath.Misfit(doorX[h], doorZ[h], doorX[h] + 40f, doorZ[h] - 45f, _base.OriginX, _base.OriginZ,
                                                                 _base.WorldWidth, _base.WorldHeight, metres));
                worstDoor = Mathf.Max(worstDoor, NavBaseMath.Outside(doorX[h], doorZ[h], data.HullX, data.HullZ, data.HullStart[h], data.HullStart[h + 1] - data.HullStart[h]));
                int index = data.HullOf[h];
                names += (i > 0 ? ", " : string.Empty) + "'" + NavBaseGame.NameOf(index >= 0 && index < buildings.Length ? buildings[index] : null) + "' at pixel "
                         + NavBaseMath.Texel(NavBaseMath.ToPixel(doorX[h], _base.OriginX, _base.PerMetre), _base.Width) + ", "
                         + NavBaseMath.Texel(NavBaseMath.ToPixel(doorZ[h], _base.OriginZ, _base.PerMetre), _base.Height);
            }
            bool fits = picked > 0 && worstFit <= MisfitMost && worstDoor <= DoorMost;
            Logger?.Info("Minimap check (alignment): the player and the doors of " + picked + " buildings far apart (" + names + "): the picture and the markers agree to "
                         + worstFit.ToString("F4", plain) + " m at worst (wanted " + MisfitMost.ToString("F0", plain) + " m or less); the door furthest from its own building's outline is "
                         + worstDoor.ToString("F1", plain) + " m outside it (wanted " + DoorMost.ToString("F0", plain) + " m or less): " + (fits ? "PASS" : "FAIL"));
        }

        // ---------------------------------------------------------------- tests

        private bool BaseRefused(string test, bool needsPicture)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return true;
            }
            if (_broken || !Settings.Minimap || !Settings.MinimapBase)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return true;
            }
            if (!needsPicture)
            {
                return false;
            }
            if (_base.Failed)
            {
                TestRefusals.TellKey(Logger, test, "reallife_nav_test_base_failed");
                return true;
            }
            if (_base.Picture == null || _base.Data == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_nav_test_base_wait");
                return true;
            }
            return false;
        }

        /// <summary>"Test: minimap, buildings and water alone": the roads, the route and the markers are left off the minimap for 20 seconds.</summary>
        public void TestBaseAlone()
        {
            try
            {
                const string test = "Minimap base alone";
                if (BaseRefused(test, true))
                {
                    return;
                }
                if (_hud == null || !_showing || !_baseGiven)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_nav_test_base_hidden");
                    return;
                }
                _hud.Alone(true);
                _aloneUntil = Time.unscaledTime + AloneSeconds;
                Logger?.Info("Navigation test: the minimap shows its buildings and water alone for " + AloneSeconds.ToString("F0") + " s.");
                Notifications.Show(NotificationType.Info, "reallife_nav_test_base_alone_on",
                                   new Dictionary<string, string>(1) { { "seconds", AloneSeconds.ToString("F0", CultureInfo.InvariantCulture) } }, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// "Test: minimap, mark three buildings": a marker for ten minutes on the door of the building nearest the
        /// player (in view at once) and of two buildings far from each other, so that it can be seen whether a
        /// marker stands on its building's front.
        /// </summary>
        public void TestBaseMarks()
        {
            try
            {
                const string test = "Minimap marks on three buildings";
                if (BaseRefused(test, true))
                {
                    return;
                }
                NavBaseData data = _base.Data;
                CityBuildingController[] buildings = NavBaseGame.Buildings();
                if (buildings == null || data.Hulls < 3)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits);
                    return;
                }
                var doorX = new float[data.Hulls];
                var doorZ = new float[data.Hulls];
                int nearest = 0;
                float least = float.MaxValue;
                for (int h = 0; h < data.Hulls; h++)
                {
                    int index = data.HullOf[h];
                    NavBaseGame.Door(index >= 0 && index < buildings.Length ? buildings[index] : null, out doorX[h], out doorZ[h]);
                    float sqr = (doorX[h] - _px) * (doorX[h] - _px) + (doorZ[h] - _pz) * (doorZ[h] - _pz);
                    if (sqr < least)
                    {
                        least = sqr;
                        nearest = h;
                    }
                }
                NavBaseMath.FarApart(doorX, doorZ, data.Hulls, _three);
                _three[2] = nearest;
                GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
                DirectionGuider like = guiders != null ? guiders.destinationGuider : null;
                Color32 colour = like != null ? (Color32)like.poiBackgroundColor : new Color32(255, 255, 255, 255);
                colour.a = 255;
                NavMarks.Trial.Clear();
                string names = string.Empty;
                for (int i = 2; i >= 0; i--)   // the nearest first
                {
                    int h = _three[i];
                    int index = data.HullOf[h];
                    NavMarks.Trial.Add(new NavMark { X = doorX[h], Z = doorZ[h], Icon = like != null ? like.poiICon : null, Colour = colour });
                    names += (names.Length > 0 ? "; " : string.Empty) + NavBaseGame.NameOf(index >= 0 && index < buildings.Length ? buildings[index] : null);
                }
                _trialUntil = Time.unscaledTime + TrialSeconds;
                _nextMarks = 0f;
                _next = 0f;
                Logger?.Info("Navigation test: markers for " + (TrialSeconds / 60f).ToString("F0") + " minutes on the doors of three buildings: " + names
                             + ". The first is the nearest to the player, " + Mathf.Sqrt(least).ToString("F0") + " m away.");
                Notifications.Show(NotificationType.Info, "reallife_nav_test_base_marks_on", new Dictionary<string, string>(1) { { "places", names } }, 12f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Test: minimap, measure the city again": the kept file is deleted and the buildings and water are measured and drawn anew.</summary>
        public void TestBaseAgain()
        {
            try
            {
                const string test = "Minimap measured again";
                if (BaseRefused(test, false))
                {
                    return;
                }
                if (_base.Busy || !_graph.Finished)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_nav_test_base_wait");
                    return;
                }
                _base.Drop(true);
                BaseOff();
                _baseAgain = true;
                _next = 0f;
                Logger?.Info("Navigation test: the minimap's buildings and water are measured again; the kept file is deleted.");
                Notifications.Show(NotificationType.Info, "reallife_nav_test_base_again_on", null, 10f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
