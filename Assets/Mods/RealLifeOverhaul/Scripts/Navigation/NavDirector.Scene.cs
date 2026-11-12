using System;
using System.Text;
using Localizor;
using RealLifeOverhaul.Map25;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The director's part for the minimap's 2.5D picture: when its city is made (once the minimap's base is, from that base's measured buildings,
    /// and again when the mod's own buildings change), and letting all of it go when the option is off. Three comparisons on the slow tick while nothing
    /// changes; a step of at most 0.6 ms a frame while the meshes are made; nothing while the minimap is flat.
    /// </summary>
    public sealed partial class NavDirector
    {
        private readonly NavScene _scene = new NavScene();
        private NavMapCamera _mapCamera;
        private bool _routeFed;
        private const float LongFrame = 0.05f;      // seconds: a frame this long gets no slice of the scene's meshes

        // Whether the minimap's base is wanted for the 2.5D minimap, though the flat picture is not.
        private bool SceneWantsBase => Settings.Minimap25 && Settings.Minimap;

        // On the slow tick, after the base: the scene begun when something wants it and its data is there, dropped when nothing does.
        private void SceneWanted(bool wantMinimap)
        {
            if (!(wantMinimap && Settings.Minimap25))
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
        }

        // The map opened or shut: the button's tilt of the game's own map camera goes on or off with it.
        private void OpenCamera(bool open)
        {
            if (_mapCamera == null)
            {
                if (!open)
                {
                    return;
                }
                _mapCamera = gameObject.AddComponent<NavMapCamera>();
            }
            _mapCamera.Open(open);
        }

        // The route into the scene's own strips (the minimap's picture draws it in a few of its own pixels), and told to draw again.
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
        }

        // The map check after the map shut (MapParts, RLO-PERF-022): the camera not held at a tilt of the mod's.
        private int CameraLook(StringBuilder line)
        {
            bool held = _mapCamera != null && _mapCamera.Pitched;
            line?.Append("city map camera: ").Append(held ? "still tilted by the mod" : "the game's own");
            return held ? 1 : 0;
        }

        // Everything of the minimap's 2.5D picture goes: the meshes and the city.
        private void SceneRemove()
        {
            _routeFed = false;
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
