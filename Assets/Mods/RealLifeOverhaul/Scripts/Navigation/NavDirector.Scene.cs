using System;
using Localizor;
using RealLifeOverhaul.Map25;
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
        private bool _namesMade;

        // Whether the minimap's base is wanted for the 2.5D map, though the flat picture is not.
        private bool SceneWantsBase => (Settings.Map25 && !_voogle) || (Settings.Minimap25 && Settings.Minimap);

        // On the slow tick, after the base: the scene begun when something wants it and its data is there, dropped when nothing does.
        private void SceneWanted(bool wantMinimap)
        {
            bool full = Settings.Map25 && !_voogle;
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
            if (full && _scene.Ready && _map25 == null && !_map25GaveUp)
            {
                _map25 = NavMap25.Make(_scene);
                _map25GaveUp = _map25 == null;
                if (_map25GaveUp)
                {
                    Logger?.Warn("Navigation: the city map's marker layer was not found, so the city map stays the game's own (no 2.5D picture).");
                }
                else if (_mapOpen)
                {
                    OpenPicture(true);
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
                _map25.Player(_px, _py, _pz, _follow != null ? _follow.eulerAngles.y : _heading);
            }
            _map25.Open(open);
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

        // Once a frame while the scene's meshes are made.
        private void SceneStep()
        {
            if (_scene.Busy)
            {
                _scene.Step();
            }
        }
    }
}
