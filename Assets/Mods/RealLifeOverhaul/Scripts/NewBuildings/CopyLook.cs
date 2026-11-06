using System;
using System.Collections.Generic;
using System.Text;
using Helpers;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A decorative copy of one of the game's buildings (a look for a construction site, scenery): the game's own
    /// see-through fade, as a plain ViewBlockingEntity whose lists are the building's own, and its look on the city map,
    /// the box drawn and the detail hidden as CityBuildingController.ToggleLodMode does it; nothing else of the game's.
    /// The building's own controller is taken off in the sleeping template: its Start puts handlers on the game's hourly,
    /// map and registration events that its OnDestroy never takes off, and the game empties those only at the next load,
    /// so a copy with it could never be taken away again. This one takes its own handler off when it goes. It is not
    /// hovered or clicked: the game's pointer finds it and does nothing, unless it wears the look of one of the game's
    /// buildings (<see cref="Lend"/>): then a hover or a click on it is one on that building. Cost: nothing a frame; the
    /// game's fade runs it only while it stands between the camera and the player; the map's opening and closing switch
    /// its renderers once.
    /// </summary>
    public sealed class CopyLook : ViewBlockingEntity
    {
        [SerializeField] private MeshRenderer _box;                               // the building's box: its shadow, and its look on the map
        [SerializeField] private List<Renderer> _detail = new List<Renderer>(16);   // what the map leaves out: everything drawn but the box

        private int _layer;
        private Action<bool> _onMap;
        private bool _mapLook;   // drawn as for the city map now
        private static int _inMapLook;   // copies drawn as for the map now (the map check, RLO-PERF-022)
        private static readonly MapParts.Look MapLookCall = MapLook;   // made once
        private ViewBlockingEntity _lentTo;   // the building whose look it wears, which takes its hovers and clicks; null for none

        protected override int DefaultLayer => _layer;

        /// <summary>The building's box: what the minimap draws of the copy.</summary>
        internal MeshRenderer Box => _box;

        /// <summary>
        /// Fills the template asleep, from the building's own controller before it is taken off: the fade's lists, the parts
        /// the game finds it by, what the pointer outlines (the parts' meshes alone, all on the buildings' layer) and the box.
        /// </summary>
        internal void Fill(MeshRenderer box, List<Renderer> fade, List<Renderer> hide, List<ViewBlockingEntityPart> parts, List<GameObject> disable)
        {
            _box = box;
            renderersToFade = new List<Renderer>(fade);
            renderersToHide = new List<Renderer>(hide);
            cityBuildingParts = new List<ViewBlockingEntityPart>(parts);
            objectsToDisable = new List<GameObject>(disable);
            var outline = new List<Renderer>(parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                Renderer drawn = parts[i] != null ? parts[i].GetComponent<Renderer>() : null;
                if (drawn != null)
                {
                    outline.Add(drawn);
                }
            }
            renderers = outline.ToArray();
            navMeshTargets = new Transform[0];
            _detail.Clear();
            AddDetail(renderers);
            AddDetail(renderersToFade);
            AddDetail(renderersToHide);
        }

        private void AddDetail(IList<Renderer> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Renderer r = list[i];
                if (r != null && (_box == null || r.gameObject != _box.gameObject) && !_detail.Contains(r))
                {
                    _detail.Add(r);
                }
            }
        }

        public override void Awake()
        {
            _layer = LayerHelper.BuildingLayerIndex;
            primaryInteractionEnabled = false;
            base.Awake();
            for (int i = 0; i < cityBuildingParts.Count; i++)
            {
                if (cityBuildingParts[i] != null)
                {
                    cityBuildingParts[i].gameObject.layer = _layer;   // as the game's controller puts them in its Awake
                }
            }
        }

        public override void Start()
        {
            base.Start();
            MapParts.Add("copied buildings", MapLookCall);   // asked after the map shut, with Developer diagnostics on (Diagnostics/SessionWatch.Map)
            _onMap = Guard.Wrap<bool>(OnMap);
            GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Combine(GlobalEvents.onCityMapToggle, _onMap);
            if (CityMap.IsOpen)
            {
                OnMap(true);   // made while the map was open
            }
        }

        public override void OnDestroy()
        {
            if (_onMap != null)
            {
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap);
            }
            if (_mapLook)
            {
                _mapLook = false;
                _inMapLook--;
            }
            base.OnDestroy();
        }

        // As CityBuildingController.ToggleLodMode: on the map the box is drawn with its own shadow and the detail is not.
        private void OnMap(bool open)
        {
            if (_box == null)
            {
                return;
            }
            _box.shadowCastingMode = open ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            for (int i = 0; i < _detail.Count; i++)
            {
                if (_detail[i] != null)
                {
                    _detail[i].enabled = !open;
                }
            }
            if (_mapLook != open)
            {
                _mapLook = open;
                _inMapLook += open ? 1 : -1;
            }
        }

        // The map check after the map shut (Diagnostics/SessionWatch.Map): no copy drawn as for the map. A count.
        private static int MapLook(StringBuilder line)
        {
            line?.Append("copied buildings: ").Append(_inMapLook).Append(" still drawn as for the map");
            return _inMapLook > 0 ? 1 : 0;
        }

        /// <summary>
        /// Hovers and clicks on the copy go to <paramref name="owner"/>, the building whose look it wears (its own colliders
        /// still fade the copy itself, as the game's fade finds the hit's parent); null gives them back to the copy, which
        /// takes none.
        /// </summary>
        internal void Lend(ViewBlockingEntity owner)
        {
            _lentTo = owner;
            for (int i = 0; i < cityBuildingParts.Count; i++)
            {
                if (cityBuildingParts[i] != null)
                {
                    cityBuildingParts[i].cityBuildingController = owner != null ? owner : this;
                }
            }
        }

        /// <summary>True when the game's fade finds this copy from each of its parts' colliders, as its manager looks (the hit or its parent), and has something to fade.</summary>
        internal bool FadeLinked(out int parts)
        {
            parts = 0;
            for (int i = 0; i < cityBuildingParts.Count; i++)
            {
                ViewBlockingEntityPart part = cityBuildingParts[i];
                if (part == null || part.GetComponent<Collider>() == null)
                {
                    continue;
                }
                Transform hit = part.transform;
                bool found = hit.GetComponent<ViewBlockingEntity>() == this || (hit.parent != null && hit.parent.GetComponent<ViewBlockingEntity>() == this);
                if (!found || (part.cityBuildingController != this && (_lentTo == null || part.cityBuildingController != _lentTo)))
                {
                    return false;
                }
                parts++;
            }
            return parts > 0 && renderersToFade.Count > 0;
        }
    }
}
