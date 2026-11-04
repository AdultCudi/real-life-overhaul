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
    /// its renderers once, by one handler for every copy: the copies within 200 m of the player in the frame the map
    /// switches in, the rest half a millisecond a frame after it (Core/MapParts.After), while the map's camera flies.
    /// </summary>
    public sealed class CopyLook : ViewBlockingEntity
    {
        [SerializeField] private MeshRenderer _box;                               // the building's box: its shadow, and its look on the map
        [SerializeField] private List<Renderer> _detail = new List<Renderer>(16);   // what the map leaves out: everything drawn but the box

        private int _layer;
        private bool _mapLook;   // drawn as for the city map now
        private bool _state;     // switched for the open map (a copy with no box too, which draws nothing for it)
        private int _standingAt = -1;   // its place among the copies standing; -1 before its Start and once it went
        private Vector3 _at;            // where it stands (a copy stays where it was put): read once, at its Start
        private static int _inMapLook;   // copies drawn as for the map now (the map check, RLO-PERF-022)
        private static readonly MapParts.Look MapLookCall = MapLook;   // made once
        private ViewBlockingEntity _lentTo;   // the building whose look it wears, which takes its hovers and clicks; null for none

        // Every copy standing, switched for the map by one handler on the game's map event, on while any copy stands.
        private static readonly List<CopyLook> Standing = new List<CopyLook>(256);
        private static Action<bool> _onMap;                           // made once
        private static readonly Func<bool> LaterCall = SwitchLater;   // made once
        private static bool _wanted;          // what the copies are switched for: the open map, or the street
        private static int _cursor;           // where the slices after the map's change go on from
        private static int _changedIn = -1;   // the frame the map switched in: its slices begin in the next
        private const float NearNow = 200f;   // metres from the player within which a copy is switched in the frame the map switches in
        private const double SliceMs = 0.5;   // of each frame after it, for the rest

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
            _at = transform.position;
            _standingAt = Standing.Count;
            Standing.Add(this);
            if (Standing.Count == 1)
            {
                // The first copy standing: the one handler on the game's map event (the game empties its events at a load, when every copy goes).
                _onMap = _onMap ?? Guard.Wrap<bool>(OnMap);
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Combine(GlobalEvents.onCityMapToggle, _onMap);
            }
            if (CityMap.IsOpen)
            {
                Switch(true);   // made while the map was open
            }
        }

        public override void OnDestroy()
        {
            if (_standingAt >= 0)
            {
                int at = _standingAt;
                int last = Standing.Count - 1;
                if (at <= last && ReferenceEquals(Standing[at], this))
                {
                    Standing[at] = Standing[last];
                    Standing[at]._standingAt = at;
                    Standing.RemoveAt(last);
                    if (at < _cursor)
                    {
                        _cursor = at;   // the copy moved into its place is looked at by the slices still to come
                    }
                }
                _standingAt = -1;
                if (Standing.Count == 0 && _onMap != null)
                {
                    GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap);
                }
            }
            if (_mapLook)
            {
                _mapLook = false;
                _inMapLook--;
            }
            base.OnDestroy();
        }

        // The one handler of the map's event for every copy. The copies within NearNow of the player are switched in this
        // frame, the rest a slice a frame from the next (the map's camera takes a second or more to fly up or down, and they
        // are far below it); one that is already as wanted is passed by.
        private static void OnMap(bool open)
        {
            _wanted = open;
            _cursor = 0;
            _changedIn = Time.frameCount;
            Vector3 player = PlayerHelper.GetPosition();
            const float near = NearNow * NearNow;
            bool later = false;
            for (int i = 0; i < Standing.Count; i++)
            {
                CopyLook copy = Standing[i];
                if (copy._state == open)
                {
                    continue;
                }
                Vector3 at = copy._at;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz <= near)
                {
                    copy.Switch(open);
                }
                else
                {
                    later = true;
                }
            }
            if (later)
            {
                MapParts.After(LaterCall);
            }
        }

        // A slice of the copies left after the map's change, once a frame from the frame after it; false once none is left.
        private static bool SwitchLater()
        {
            if (Time.frameCount <= _changedIn)
            {
                return true;   // the frame the map switched in has had its share
            }
            try
            {
                WorkSlice slice = WorkSlice.Of(SliceMs, 4);
                for (; _cursor < Standing.Count; _cursor++)
                {
                    CopyLook copy = Standing[_cursor];
                    if (copy._state == _wanted)
                    {
                        continue;
                    }
                    if (!slice.More())
                    {
                        return true;
                    }
                    copy.Switch(_wanted);
                }
            }
            catch (Exception e)
            {
                CopyGame.Logger?.Error(e);
            }
            return false;
        }

        // As CityBuildingController.ToggleLodMode: on the map the box is drawn with its own shadow and the detail is not.
        private void Switch(bool open)
        {
            _state = open;
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
