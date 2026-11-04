using System;
using System.Text;
using Localizor;
using RealLifeOverhaul.Map25;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The director's part for the 2.5D map: when its city is made (once the minimap's base is, from that base's measured buildings,
    /// and again when the mod's own buildings change), the full map's picture in the game's pin layer, and letting all of it go when
    /// both options are off. Three comparisons on the slow tick while nothing changes; a step of at most 0.6 ms a frame while the
    /// meshes are made; nothing while the map is shut and the minimap is flat.
    /// </summary>
    public sealed partial class NavDirector
    {
        private readonly NavScene _scene = new NavScene();
        private NavMap25 _map25;
        private bool _map25GaveUp;
        private string[] _roadName;
        private int _roadAt;
        private bool _namesMade, _routeFed;
        private const float LongFrame = 0.05f;      // seconds: a frame this long gets no slice of the scene's meshes

        // The city map's own picture is wanted: its option is on, no other mod owns the map, and no picture of it has come out black on this machine.
        private bool FullWanted => Settings.Map25 && !_voogle && !NavMapLatch.Drew(NavMapLatch.Full);

        // Whether the minimap's base is wanted for the 2.5D map, though the flat picture is not.
        private bool SceneWantsBase => FullWanted || (Settings.Minimap25 && Settings.Minimap);

        // On the slow tick, after the base: the scene begun when something wants it and its data is there, dropped when nothing does.
        private void SceneWanted(bool wantMinimap)
        {
            bool full = FullWanted;
            if (!full && !(wantMinimap && Settings.Minimap25))
            {
                SceneRemove();
                return;
            }
            NavBaseData data = _base.Drawn;
            if (_scene.State == NavScene.Stage.Failed)
            {
                return;
            }
            if (data != null && _scene.Ready && !ReferenceEquals(data, _scene.Data))
            {
                SceneRemove();   // the mod's own buildings changed: the city is made again with them
            }
            if (_scene.State == NavScene.Stage.Idle && data != null && _graph.Finished)
            {
                _scene.Begin(data, _graph);
            }
            if (_scene.Ready && !_routeFed)
            {
                _routeFed = true;
                FeedRoute();   // the route as it is, into the strips of the scene just made
            }
            if (full && _scene.Ready && _map25 == null && !_map25GaveUp)
            {
                _map25 = NavMap25.Make(_scene);
                _map25GaveUp = _map25 == null;
                if (_map25GaveUp)
                {
                    Logger?.Warn("Navigation: the city map's marker layer was not found, so the city map stays the game's own (no 2.5D picture).");
                }
                else
                {
                    ArrangeMap();
                    if (_mapOpen)
                    {
                        OpenPicture(true);
                    }
                }
            }
            if (!full && _map25 != null)
            {
                UnityEngine.Object.Destroy(_map25.gameObject);
                _map25 = null;
            }
            if (full && _map25 != null && !_namesMade)
            {
                NameRoads();
            }
        }

        // The streets' names for the full map, two roads a slow tick (the game's own are painted on the roads the picture covers). Each road's
        // name is the graph's (a vote of the doors beside it), found once and kept; the places of the labels are made when the last is in.
        private void NameRoads()
        {
            if (!_graph.Finished || _graph.Drive == null)
            {
                return;
            }
            if (_roadName == null)
            {
                _roadName = new string[_graph.Roads];
                _roadAt = 0;
            }
            for (int n = 0; n < 2 && _roadAt < _roadName.Length; n++)
            {
                _roadName[_roadAt] = _graph.StreetOf(_roadAt);
                _roadAt++;
            }
            if (_roadAt >= _roadName.Length)
            {
                RouteSearch drive = _graph.Drive;
                _map25.Names(Map25Names.Anchors(_graph.Road, drive.X, drive.Z, _graph.Usable, _graph.Junction, drive.Count, _roadName));
                _roadName = null;
                _namesMade = true;
            }
        }

        // The map opened or shut: the 2.5D picture with it (the game's own camera has come to rest only later; the picture waits for it).
        private void OpenPicture(bool open)
        {
            if (_map25 == null)
            {
                return;
            }
            if (open)
            {
                Player();   // where the player stands now, for the cone and the marker (the slow tick may not have run with the minimap and the GPS off)
                _map25.Player(_px, _py, _pz, _follow != null ? _follow.eulerAngles.y : _heading);
            }
            _map25.Open(open);
        }

        // The one place that says where the game's marker layer's children stand: the 2.5D picture first, the mod's own route line over it, every pin of the game's over both.
        // Called whenever either is made and whenever the map opens, so which of the two was made last, and whether the game added a pin meanwhile, never matters.
        private void ArrangeMap()
        {
            NavMapLayers.Indices(_map25 != null, _map != null, out int picture, out int route);
            if (picture >= 0)
            {
                _map25.Rect.SetSiblingIndex(picture);
            }
            if (route >= 0)
            {
                _map.Rect.SetSiblingIndex(route);
            }
        }

        // The route into the scene's own strips (the full map's picture and the minimap's both draw it, each in a few of its own pixels), and both told to draw again.
        private void FeedRoute()
        {
            if (_scene.Ready)
            {
                _scene.Scene.SetRoute(_lineCount < 2 ? null : _lineX, _lineY, _lineZ, _lineCount, _scene.Y0);
            }
            if (_hud != null)
            {
                _hud.SceneRouteChanged();
            }
            if (_map25 != null)
            {
                _map25.RouteChanged();
            }
        }

        // The district view changed what a zone looks like: the open map's picture looks at the zones and the highlights again.
        private void PokeMap()
        {
            if (_map25 != null && _mapOpen)
            {
                _map25.Poke();
            }
        }

        // The map check after the map shut (MapParts, RLO-PERF-022): the picture's object, texture, camera mask and tilt, and tints given back.
        private int Map25Look(StringBuilder line)
        {
            if (_map25 == null)
            {
                line?.Append("2.5D city map: none made");
                return 0;
            }
            return _map25.Held(line);
        }

        // Everything of the 2.5D map goes: the picture, the meshes and the city.
        private void SceneRemove()
        {
            if (_map25 != null)
            {
                UnityEngine.Object.Destroy(_map25.gameObject);
                _map25 = null;
            }
            _map25GaveUp = false;
            _namesMade = false;
            _routeFed = false;
            _roadName = null;
            _roadAt = 0;
            if (_scene.State != NavScene.Stage.Idle)
            {
                _scene.Dispose();
            }
        }

        private string _districtId, _districtText;
        private float _stripX = float.MaxValue, _stripZ;

        // On the slow tick while the minimap shows its 2.5D picture: the district's name (the game's, in capitals) and the street under the player.
        private void SceneStrip()
        {
            if (!_hud.SceneMode)
            {
                return;
            }
            string id = NeighborhoodHelper.CurrentNeighborhood;
            if (id != _districtId)
            {
                _districtId = id;
                _districtText = string.IsNullOrEmpty(id) ? null : id.GetLocalization().ToUpperInvariant();
                _stripX = float.MaxValue;   // the district changed: the strip is written again
            }
            float dx = _px - _stripX, dz = _pz - _stripZ;
            if (dx * dx + dz * dz < 36f)
            {
                return;   // the street is looked for again after six metres, not on every tick
            }
            _stripX = _px;
            _stripZ = _pz;
            _hud.SceneStrip(_districtText, _graph.Ready ? _graph.StreetNear(_px, _py, _pz) : null);
        }

        // Once a frame while the scene's meshes are made: not behind the loading screen (the base's own step waits too) and not in a frame that is already long.
        private void SceneStep()
        {
            if (_scene.Busy && !LoadScene.isLoading && Time.unscaledDeltaTime < LongFrame)
            {
                _scene.Step();
            }
        }
    }
}
