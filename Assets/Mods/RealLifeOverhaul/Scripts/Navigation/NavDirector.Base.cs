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
    /// The director's part for the buildings and water under the minimap's roads (<see cref="NavBase"/>): when it is made
    /// and given to the panel, its two checks and its three tests. Three comparisons on the slow tick while nothing changes.
    /// </summary>
    public sealed partial class NavDirector
    {
        private const float AloneSeconds = 20f;     // seconds the test "show the base alone" leaves the roads off
        private const float TrialSeconds = 600f;    // seconds the test's three markers stay
        private const float MisfitMost = 1f;        // metres the picture and the markers may disagree by
        private const float DoorMost = 8f;          // metres a building's door may lie outside its own outline (a porch, steps)

        private readonly NavBase _base = new NavBase();
        private readonly int[] _three = new int[3];
        private bool _baseGiven, _baseChecked, _baseAgain;
        private Texture2D _baseShown;   // the picture the panel was given: drawn again, the new one is given
        private float _aloneUntil, _trialUntil;

        // On the slow tick: begun once the roads are there, let go when its option or the minimap is switched off.
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
                _baseAgain = _baseAgain && _base.Idle;   // still idle: the game's palette was not there yet, asked again next tick
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

        // On the slow tick with a save loaded: the picture given and the checks written once, and a test's doings taken back.
        private void BaseShown(float now)
        {
            if (_base.Stale && !_base.Busy)
            {
                _base.Redraw();   // the mod's own buildings changed (Scripts/NewBuildings): the same city drawn again with them
            }
            if (_base.Picture != null)
            {
                if (_hud != null && (!_baseGiven || _baseShown != _base.Picture))
                {
                    _baseGiven = true;
                    _baseShown = _base.Picture;
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

        // Two lines once per picture: what it is, and whether it and the markers' layer agree on three buildings far apart.
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
            // Each outline's building is found by its address: the game's list is in no order it keeps from one load to the next.
            var found = new CityBuildingController[data.Hulls];
            int matched = NavBaseGame.Find(buildings, data, found);
            var doorX = new float[data.Hulls];
            var doorZ = new float[data.Hulls];
            var known = new bool[data.Hulls];
            for (int h = 0; h < data.Hulls; h++)
            {
                known[h] = NavBaseGame.Door(found[h], out doorX[h], out doorZ[h]);
            }
            int picked = NavBaseMath.FarApartKnown(doorX, doorZ, known, data.Hulls, _three);
            float metres = NavView.MetresAcross(Settings.MinimapZoom, 0f) * 1.5f;   // the turned square that holds the view
            float worstFit = NavBaseMath.Misfit(_px, _pz, _px, _pz, _base.OriginX, _base.OriginZ, _base.WorldWidth, _base.WorldHeight, metres);
            string names = string.Empty;
            for (int i = 0; i < picked; i++)
            {
                int h = _three[i];
                // Seen by a player standing 60 m from the door: both are then in the view.
                worstFit = Mathf.Max(worstFit, NavBaseMath.Misfit(doorX[h], doorZ[h], doorX[h] + 40f, doorZ[h] - 45f, _base.OriginX, _base.OriginZ,
                                                                 _base.WorldWidth, _base.WorldHeight, metres));
                names += (i > 0 ? ", " : string.Empty) + "'" + NavBaseGame.NameOf(found[h]) + "' at pixel "
                         + NavBaseMath.Texel(NavBaseMath.ToPixel(doorX[h], _base.OriginX, _base.PerMetre), _base.Width) + ", "
                         + NavBaseMath.Texel(NavBaseMath.ToPixel(doorZ[h], _base.OriginZ, _base.PerMetre), _base.Height);
            }
            float worstDoor = NavBaseMath.WorstOutside(doorX, doorZ, data, _three, picked, out int worst);
            string offender = string.Empty;
            if (worst >= 0)
            {
                // The building and its door that lie furthest from their outline: where the door is and where its outline's middle is.
                int h = _three[worst];
                float middleX = 0f, middleZ = 0f;
                int count = data.HullStart[h + 1] - data.HullStart[h];
                for (int k = data.HullStart[h]; k < data.HullStart[h + 1]; k++)
                {
                    middleX += data.HullX[k] / count;
                    middleZ += data.HullZ[k] / count;
                }
                offender = ", that of '" + NavBaseGame.NameOf(found[h]) + "', its door at (" + doorX[h].ToString("F1", plain) + ", " + doorZ[h].ToString("F1", plain)
                           + ") and its outline's middle at (" + middleX.ToString("F1", plain) + ", " + middleZ.ToString("F1", plain) + ")";
            }
            bool found3 = matched * 4 >= data.Hulls * 3;   // a few outlines may have lost their building (one the mod's own stands in its place, or an address shared); most may not
            bool fits = picked > 0 && found3 && worstFit <= MisfitMost && worstDoor <= DoorMost;
            Logger?.Info("Minimap check (alignment): the player and the doors of " + picked + " buildings far apart (" + names + "): the picture and the markers agree to "
                         + worstFit.ToString("F4", plain) + " m at worst (wanted " + MisfitMost.ToString("F0", plain) + " m or less); the door furthest from its own building's outline is "
                         + worstDoor.ToString("F1", plain) + " m outside it" + offender + " (wanted " + DoorMost.ToString("F0", plain) + " m or less); " + matched + " of " + data.Hulls
                         + " outlines found their building by its address" + (found3 ? string.Empty : " (wanted three in four)") + ": " + (fits ? "PASS" : "FAIL"));
        }

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

        /// <summary>"Test: minimap, buildings and water alone": the roads, the route and the markers left off for a while.</summary>
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

        /// <summary>"Test: minimap, mark three buildings": markers on the nearest building's door and two far apart.</summary>
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
                var found = new CityBuildingController[data.Hulls];
                NavBaseGame.Find(buildings, data, found);
                var doorX = new float[data.Hulls];
                var doorZ = new float[data.Hulls];
                var known = new bool[data.Hulls];
                int nearest = -1;
                int knownCount = 0;
                float least = float.MaxValue;
                for (int h = 0; h < data.Hulls; h++)
                {
                    known[h] = NavBaseGame.Door(found[h], out doorX[h], out doorZ[h]);
                    if (!known[h])
                    {
                        continue;
                    }
                    knownCount++;
                    float sqr = (doorX[h] - _px) * (doorX[h] - _px) + (doorZ[h] - _pz) * (doorZ[h] - _pz);
                    if (sqr < least)
                    {
                        least = sqr;
                        nearest = h;
                    }
                }
                if (knownCount < 3)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits);
                    return;
                }
                NavBaseMath.FarApartKnown(doorX, doorZ, known, data.Hulls, _three);
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
                    NavMarks.Trial.Add(new NavMark { X = doorX[h], Z = doorZ[h], Icon = like != null ? like.poiICon : null, Colour = colour });
                    names += (names.Length > 0 ? "; " : string.Empty) + NavBaseGame.NameOf(found[h]);
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

        /// <summary>"Test: minimap, measure the city again": the kept file deleted, the buildings and water measured anew.</summary>
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
