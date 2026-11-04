using System;
using Localizor;
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
            if (_scene.State != NavScene.Stage.Idle)
            {
                _scene.Dispose();
            }
        }

        private string _districtId, _districtText;

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
            }
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
