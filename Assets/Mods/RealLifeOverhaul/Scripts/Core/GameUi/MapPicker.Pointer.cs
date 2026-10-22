using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.InputSystem;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>The choice's pointer on entries and markers, its look once a frame, and the scripts on its objects.</summary>
    public sealed partial class MapPicker
    {
        internal void EntryEnter(int index)
        {
            if (!IsOpen || index < 0 || index >= _used || _rows[index].Note)
            {
                return;
            }
            Unpoint();
            Row row = _rows[index];
            if (row.Background != null)
            {
                row.Background.enabled = true;
            }
            _pointedRow = index;
            _glideAt = Time.unscaledTime + _previewDelay;
            _glided = false;
        }

        internal void EntryExit(int index)
        {
            if (index == _pointedRow)
            {
                Unpoint();
            }
        }

        internal void EntryClick(int index)
        {
            Picked(index);
        }

        internal void MarkEnter(int index)
        {
            if (IsOpen && index >= 0 && index < _marksUsed)
            {
                Plate(index);
            }
        }

        internal void MarkExit(int index)
        {
            if (index == _plate && _pointedRow < 0)
            {
                Plate(-1);
            }
        }

        internal void MarkClick(int index)
        {
            if (IsOpen && index >= 0 && index < _marksUsed && _marks[index].Row >= 0)
            {
                Picked(_marks[index].Row);
            }
        }

        private void Unpoint()
        {
            if (_pointedRow >= 0 && _pointedRow < _rows.Count && _rows[_pointedRow].Background != null)
            {
                _rows[_pointedRow].Background.enabled = false;
            }
            if (_glided)
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city != null && city.cityMap != null && city.cityMap.cityMapCam != null)
                {
                    city.cityMap.cityMapCam.MoveCameraToTarget(Vector3.zero);   // stops the glide where it is, as the game's entries do
                }
            }
            _pointedRow = -1;
            _glided = false;
            Plate(-1);
        }

        private void Plate(int index)
        {
            if (_plate == index)
            {
                return;
            }
            if (_plate >= 0 && _plate < _marks.Count && _marks[_plate].Poi != null)
            {
                _marks[_plate].Poi.SetContainerActive(false);
            }
            _plate = index;
            if (index >= 0 && _marks[index].Poi != null)
            {
                _marks[index].Poi.SetContainerActive(true);
            }
        }

        /// <summary>Once a frame while open: the map still open, the glide and the name plate.</summary>
        internal void Frame()
        {
            if (!IsOpen)
            {
                return;
            }
            if (!CityMap.IsOpen)
            {
                Closed();   // the cancel key, or anything else that closed the map
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.mapFilters != null && uis.mapFilters.gameObject.activeSelf)
            {
                uis.mapFilters.gameObject.SetActive(false);   // the game's key that hides and shows the interface puts them back
            }
            if (_pointedRow >= 0 && !_glided && Time.unscaledTime >= _glideAt)
            {
                _glided = true;
                Row row = _rows[_pointedRow];
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (row.HasPlace && city != null && city.cityMap != null && city.cityMap.cityMapCam != null)
                {
                    city.cityMap.cityMapCam.MoveCameraToTarget(row.Place);
                    Plate(row.Mark);
                }
            }
            if (_plate >= 0 && _plate < _marks.Count && _marks[_plate].Poi != null)
            {
                _marks[_plate].Poi.SetContainerActive(true);   // the game's marker puts its plate away the first time it is shown
            }
        }

        /// <summary>The window's object went with the game's interface.</summary>
        internal void Lost()
        {
            if (IsOpen)
            {
                IsOpen = false;
                if (_shown == this)
                {
                    _shown = null;
                }
                MapPlaces.Quiet = false;
            }
            _root = null;
            _onPick = null;
            _onClosed = null;
        }
    }

    /// <summary>On the window's own object, so it runs only while the window is open: <see cref="MapPicker.Frame"/> once a frame.</summary>
    public sealed class MapPickerWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "MapPickerWatch", typeof(MapPickerWatch));   // its Update

        public MapPicker Owner;

        private static readonly int HotUpdate = HotSpots.Slot("Core: MapPickerWatch.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Owner == null)
            {
                return;
            }
            try
            {
                Owner.Frame();
            }
            catch (Exception e)
            {
                MapPicker.Logger?.Error(e);
                enabled = false;   // the entries and Cancel still work
            }
        }

        private void OnDestroy()
        {
            Owner?.Lost();
        }
    }

    /// <summary>An entry: pointed at, it glides the map to its place; clicked anywhere, it picks it. Costs nothing a frame.</summary>
    public sealed class MapPickerEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public MapPicker Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.EntryEnter(Index));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.EntryExit(Index));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.EntryClick(Index));
        }
    }

    /// <summary>A marker on the map: pointed at, it shows its name; clicked, it picks its entry. Costs nothing a frame.</summary>
    public sealed class MapPickerMark : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public MapPicker Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.MarkEnter(Index));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.MarkExit(Index));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.MarkClick(Index));
        }
    }
}
