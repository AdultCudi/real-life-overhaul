using System;
using System.Collections.Generic;
using City.CityMap;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What each press on the live dock does: every one presses the game's own widget or calls the public method the game's button calls, so the game's checks, Going Public's
    /// patches on them and the mod's rows all run as a click on the game's own panel would run them (UiWidgets). The dock mirrors afterwards, at the next look.
    /// </summary>
    internal sealed partial class MapLive : IMapActions
    {
        // ------------------------------------------------------------------------------------------------- the filters

        public bool Rail(int index)
        {
            if (index < 0 || index >= _railCats.Count)
            {
                return false;
            }
            Cat cat = _railCats[index];
            if (cat.Collapsed)
            {
                using (HotSpots.Game())
                {
                    cat.Obj.OnCollapseClick();
                }
            }
            _revealGroup = cat.Key;
            return true;
        }

        public bool ShowAll()
        {
            Toggle master = _filters != null ? MasterSwitch(_filters) : null;
            return master != null && UiWidgets.Set(master, !master.isOn);
        }

        public bool Search() => true;   // the host opens its field on the target; the words come back through Typed

        public bool Result(int index)
        {
            if (index < 0 || index >= _resultBuildings.Count || _resultBuildings[index] == null)
            {
                return false;
            }
            CityMap map = InstanceBehavior<CityManager>.Instance.cityMap;
            map.FocusOnBuilding(_resultBuildings[index]);
            Typed(string.Empty);
            return true;
        }

        public bool Chip(int index)
        {
            if (index < 0 || index >= _chipKinds.Count)
            {
                return false;
            }
            MapChipRules.Lines((MapChipKind)_chipKinds[index], _keys, _groupKeys, _avail, _chipLines);
            if (_chipLines.Count == 0)
            {
                return false;
            }
            bool target = MapChipRules.Target(_chipLines, _on);
            bool any = false;
            for (int i = 0; i < _chipLines.Count; i++)
            {
                LineInfo line = _lines[_chipLines[i]];
                if (line.On != target && line.Obj.Toggle != null)
                {
                    any |= UiWidgets.Set(line.Obj.Toggle, target);
                }
            }
            return any;
        }

        private Cat CatOfRow(int row) => row >= 0 && row < _rowCats.Count ? _rowCats[row] : null;

        private LineInfo LineOfRow(int row) => row >= 0 && row < _rowLines.Count ? _rowLines[row] : null;

        public bool Group(int row)
        {
            Cat cat = CatOfRow(row);
            if (cat == null || LineOfRow(row) != null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                cat.Obj.OnCollapseClick();
            }
            return true;
        }

        public bool GroupAll(int row)
        {
            Cat cat = CatOfRow(row);
            return cat != null && LineOfRow(row) == null && cat.Obj.ToggleAll != null && UiWidgets.Set(cat.Obj.ToggleAll, !(cat.Total > 0 && cat.On == cat.Total));
        }

        public bool Line(int row)
        {
            LineInfo line = LineOfRow(row);
            return line != null && line.Obj.Toggle != null && UiWidgets.Set(line.Obj.Toggle, !line.On);
        }

        public bool Locate(int row)
        {
            LineInfo line = LineOfRow(row);
            Button button = line != null ? GameContract.MapLineFocus.Field?.GetValue(line.Obj) as Button : null;
            return button != null && UiWidgets.Press(button);
        }

        public bool Station(int row)
        {
            if (row < 0 || row >= _stationRows.Count || _stationRows[row] == null)
            {
                return false;
            }
            return UiWidgets.Press(_stationRows[row].travelButton);
        }

        // A place of the choice: the entry's own pick, whatever its owner makes of it.
        public bool Choice(int row)
        {
            if (_picker == null || row < 0 || row >= _pickerRows.Count)
            {
                return false;
            }
            _picker.KitPick(_pickerRows[row]);
            return true;
        }

        // The choice's Cancel: the choice and the map closed, the game's own way out.
        public bool Footer()
        {
            if (_picker == null)
            {
                return false;
            }
            _picker.KitClose();
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the map's buttons

        public bool North()
        {
            CityMapCam cam = CityMapCamera();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            Transform eye = game != null && game.citymapCamera != null ? game.citymapCamera.transform : null;
            if (cam == null || eye == null || city == null)
            {
                return false;
            }
            Vector3 flat = cam.transform.position - eye.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
            {
                return false;
            }
            float delta = Vector3.SignedAngle(flat, Vector3.forward, Vector3.up);
            if (Mathf.Abs(delta) < 0.5f)
            {
                return true;
            }
            // The camera's own turn, so its 90 degree steps keep counting from where it stands now.
            using (HotSpots.Game())
            {
                GameContract.MapCamRotate.Method?.Invoke(cam, new object[] { delta });
            }
            return GameContract.MapCamRotate.Method != null;
        }

        public bool Centre()
        {
            CityMapCam cam = CityMapCamera();
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (cam == null || game == null || game.playerController == null)
            {
                return false;
            }
            cam.MoveCameraToTarget(game.playerController.transform.position);
            return true;
        }

        public bool ZoomIn() => Zoom(-1f);

        public bool ZoomOut() => Zoom(1f);

        // The camera's own distance, stepped inside its range; the camera is put at it as the game's own zoom puts it.
        private bool Zoom(float direction)
        {
            CityMapCam cam = CityMapCamera();
            if (cam == null)
            {
                return false;
            }
            float next = Mathf.Clamp(cam.distance + direction * ZoomStep, cam.minMaxDistance.x, cam.minMaxDistance.y);
            if (Mathf.Approximately(next, cam.distance))
            {
                return false;
            }
            cam.distance = next;
            cam.ForceUpdateCameraPosition();
            return true;
        }

        public bool Tilt()
        {
            Nav.NavTilt.StepMap();
            return true;
        }

        // The Layers panel's rows: the game's own lines and groups by name, each pressed through its switch.
        public bool Layer(int index)
        {
            if (index < 0 || index >= _layerLines.Count)
            {
                return false;
            }
            LayerRef layer = _layerLines[index];
            if (layer.Line != null)
            {
                return layer.Line.Obj.Toggle != null && UiWidgets.Set(layer.Line.Obj.Toggle, !layer.Line.On);
            }
            return layer.Cat != null && layer.Cat.Obj.ToggleAll != null && UiWidgets.Set(layer.Cat.Obj.ToggleAll, !(layer.Cat.Total > 0 && layer.Cat.On == layer.Cat.Total));
        }

        public bool HideAll()
        {
            Toggle master = _filters != null ? MasterSwitch(_filters) : null;
            return master != null && UiWidgets.Set(master, false);
        }

        private struct LayerRef
        {
            public LineInfo Line;
            public Cat Cat;
        }

        private readonly List<LayerRef> _layerLines = new List<LayerRef>(8);
    }
}
