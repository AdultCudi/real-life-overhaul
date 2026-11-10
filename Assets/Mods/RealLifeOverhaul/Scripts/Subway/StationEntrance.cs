using System;
using System.Runtime.CompilerServices;
using Buildings;
using Controllers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Subway
{
    /// <summary>
    /// A subway station of the districts' own: the game's own <see cref="SubwayStation"/> on a copy of one of its entrances,
    /// so the game's travel from and to it, its fare, its map's marker, outline and filter, and its list of stations are
    /// all the game's code. The game names a station from a fixed list (EnumHelpers.ToStringFast throws for any other
    /// value, and SubwayStation.GetNameKey is not virtual), so this one keeps the copied entrance's value and is named
    /// another way everywhere the game shows a name: over the street and on the map by the entity's own header key, in the
    /// list by its entry's label, and as a taxi's destination by a stop of its own (<see cref="StationTaxiStop"/>). The
    /// game's Start hangs a call on the map's toggle that it never takes off; this one's is its own and goes with it.
    /// COST: nothing a frame; the game asks only on pointing, clicking and the map's toggle.
    /// </summary>
    public sealed class StationEntrance : SubwayStation
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(StationsDirector.FeatureId, "StationEntrance", typeof(StationEntrance));   // the game's calls of it

        private Action<bool> _onMap;   // made once, added and taken off by this field
        private bool _hung;

        /// <summary>Which of the plan's stations it is.</summary>
        public StationSite Site { get; private set; }

        /// <summary>What a taxi is sent to in its place: an entity that is no station, named by its own key.</summary>
        public StationTaxiStop TaxiStop { get; private set; }

        /// <summary>Where a rider comes up.</summary>
        public Transform Arrival { get; private set; }

        /// <summary>Fitted while the copy is asleep: everything the game's Start and its travel read.</summary>
        public void Fit(StationSite site, SubwayStation copied, Renderer[] pieces, Transform arrival, StationTaxiStop taxiStop, string district)
        {
            Site = site;
            Arrival = arrival;
            TaxiStop = taxiStop;
            Take(copied);
            neighbourhood = district;
            primaryInteractionEnabled = true;
            customOverlayHeaderKey = site.NameKey;   // the game's own header over an entity reads this before it asks a station for its name
            renderers = pieces ?? new Renderer[0];
            navMeshTargets = new[] { arrival };
        }

        // What it keeps of the entrance copied: the overlays it shows and the name's value, which is never read for its name.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Take(SubwayStation copied)
        {
            simpleOverlayType = copied.simpleOverlayType;
            detailedOverlayType = copied.detailedOverlayType;
            itemWarningIconOffset = copied.itemWarningIconOffset;
            stationName = copied.stationName;
        }

        public override void Start()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                // The game's two Starts, but for two things: the travel point is not moved (it was put on ground both walking meshes hold, and
                // checked), and the map's call is one that is taken off again.
                randomAvailableNavMeshPositionGetter = new RandomAvailableNavMeshPositionGetter(navMeshTargets != null ? navMeshTargets.Length : 0);
                _onMap = _onMap ?? Guard.Wrap<bool>(FrameGuard, OnMap);
                if (!_hung)
                {
                    GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Combine(GlobalEvents.onCityMapToggle, _onMap);
                    _hung = true;
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault, this);
            }
        }

        // The travel point stays where it was checked: the game's own validation would move it to the nearest point of any mesh within 3 m.
        public override void UpdateNavMeshTargets()
        {
        }

        // As the game's stations do when its map shuts: the outline back to white and off.
        private void OnMap(bool open)
        {
            if (open || !this)
            {
                return;
            }
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            if (references != null)
            {
                SetOutlineColor(references.colors.white);
            }
            OnIoExit();
        }

        /// <summary>
        /// A click: on the game's map in its taxi mode the taxi's destination is this station's stop, which the game's card names by its own key
        /// (a station it would name by the copied entrance's); every other click is the game's own (its travel in subway mode, its walk over).
        /// </summary>
        public override bool OnIoLeftClick()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (city != null && city.cityMap != null && !city.cityMap.isSubwayMode && CityMap.IsOpen && city.cityMap.IsTaxiMode && TaxiStop != null && uis != null
                    && uis.buildingResume != null)
                {
                    uis.buildingResume.SelectTarget(TaxiStop);
                    return false;   // as the game's own click answers with its map open
                }
                return base.OnIoLeftClick();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }

        public override void OnDestroy()
        {
            try
            {
                if (_hung && _onMap != null)
                {
                    GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap);
                    _hung = false;
                }
                base.OnDestroy();
            }
            catch (Exception fault)
            {
                Guard.Failed(FrameGuard, fault, false);
            }
        }
    }

    /// <summary>
    /// What a taxi is sent to at a station of the districts' own: an entity of the game's that is not a station, with the
    /// game's own destination data beside it (Buildings.TaxiDestinationData: a name's key and a district), so the game's
    /// card names the place by that key in every path that fills it. It has no collider and nothing points at it: the
    /// station's click hands it over. COST: nothing a frame.
    /// </summary>
    public sealed class StationTaxiStop : EntityController
    {
        /// <summary>Fitted while asleep: where the taxi puts its rider down, and the data the game's card reads.</summary>
        public void Fit(Transform arrival, string nameKey, string district)
        {
            primaryInteractionEnabled = false;
            renderers = new Renderer[0];
            navMeshTargets = new[] { arrival };
            TaxiDestinationData data = gameObject.AddComponent<TaxiDestinationData>();
            data.nameKey = nameKey;
            data.neighbourhood = district;
            data.mapFiltersKey = string.Empty;        // no line of the map's filters, no marker of its own: the station has the game's
            data.mapColliders = new Collider[0];      // the game's toggle walks this list
        }

        // The game's Awake and Start set up objects the game places; neither is wanted here.
        public override void Awake()
        {
        }

        public override void Start()
        {
        }

        public override bool Interact()
        {
            return false;
        }

        public override bool ShouldReactToIoEnter()
        {
            return false;
        }

        public override void UpdateNavMeshTargets()
        {
        }
    }
}
