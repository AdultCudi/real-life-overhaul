using System;
using System.Collections;
using UI;
using UI.InGameUI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The map places' driver: the look a second and the frame's work.</summary>
    public static partial class MapPlaces
    {
        /// <summary>
        /// The one object behind the map places: a look a real second (the map pauses the game) that keeps the map's event
        /// hooked, adds the rows once the filter list is there, and names new markers. COST: a reference comparison a
        /// frame for the building card, and Light while the map is open; switched off while no place is a building.
        /// </summary>
        private sealed class Driver : MonoBehaviour
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "MapPlaces.Driver", typeof(MapPlaces.Driver));

            private readonly WaitForSecondsRealtime _second = new WaitForSecondsRealtime(1f);
            private BuildingResume _card;
            private EntityController _carded;
            private bool _cardOff;   // the card's lines stood down: this game lacks something they name
            private int _hookIn;

            private void Start()
            {
                StartCoroutine(Guard.Routine(Look()));
            }

            private void OnDestroy()
            {
                if (_driver == this)
                {
                    Dispose();
                }
            }

            private IEnumerator Look()
            {
                while (true)
                {
                    bool lacking = false;
                    try
                    {
                        Tick();
                    }
                    catch (Exception e)
                    {
                        // A game that lacks something the look names would fail every time: said once, and the look stops.
                        lacking = Guard.StandsDown(e, "the mod's places on the city map");
                        if (!lacking)
                        {
                            Logger?.Error(e);
                        }
                    }
                    if (lacking)
                    {
                        yield break;
                    }
                    yield return _second;
                }
            }

            private void Tick()
            {
                if (SaveGameManager.Current == null)
                {
                    return;
                }
                // The game clears its event hub on every load.
                if (--_hookIn <= 0)
                {
                    _hookIn = 3;
                    if (_onMap == null)
                    {
                        _onMap = Guard.Wrap<bool>(OnMap);
                    }
                    EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                _card = uis != null ? uis.buildingResume : null;
                enabled = ByBuilding.Count > 0 || Lights.Count > 0;
                if (!_filterFailed && uis != null && uis.mapFilters != null)
                {
                    bool missing = !ReferenceEquals(_filters, uis.mapFilters);
                    for (int i = 0; i < Categories.Count && !missing; i++)
                    {
                        missing = Categories[i].Toggle == null;
                    }
                    if (missing)
                    {
                        if (!ReferenceEquals(_filters, uis.mapFilters))
                        {
                            for (int i = 0; i < Categories.Count; i++)
                            {
                                Categories[i].Toggle = null;
                                Categories[i].Row = null;
                            }
                        }
                        AddRows(uis.mapFilters);
                    }
                }
                if (!_choiceFailed && uis != null && uis.mapFilters != null && Choices.Count > 0)
                {
                    bool stale = !ReferenceEquals(_choiceFilters, uis.mapFilters);
                    if (stale)
                    {
                        for (int i = 0; i < Choices.Count; i++)
                        {
                            Choices[i].Forget();   // a new list after a load: lines and rows made again
                        }
                        _choiceFilters = uis.mapFilters;
                    }
                    bool missing = false;
                    for (int i = 0; i < Choices.Count && !missing; i++)
                    {
                        missing = !Choices[i].HasRows;
                    }
                    if (missing)
                    {
                        AddChoiceRows(uis.mapFilters);
                    }
                }
                // Markers that could not be made when their places were given, and their names.
                if (CityMap.IsOpen != _mapOpen)
                {
                    OnMap(CityMap.IsOpen);   // the event was missed before the hook was back
                }
                else
                {
                    Refresh();
                }
                Label();
                CheckDropped(InstanceBehavior<CityManager>.Instance);
                if (!ReferenceEquals(_card, null))
                {
                    _carded = null;   // written again once a second: the game rewrites the card on the hour
                }
            }

            public void LabelSoon()
            {
                StartCoroutine(Guard.Routine(Soon()));
            }

            private static IEnumerator Soon()
            {
                yield return null;
                yield return null;
                try
                {
                    Label();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }

            private static readonly int HotLateUpdate = HotSpots.Slot("Core: Driver.LateUpdate");
            // After the game's own work of the frame: its look at the buildings, and the card it writes.
            private void LateUpdate()
            {
                using var hotSpot = HotSpots.Time(HotLateUpdate);
                if (FrameGuard.Off)
                {
                    enabled = false;   // switched off after its faults
                    return;
                }
                try
                {
                    OnLateUpdate();
                }
                catch (System.Exception e)
                {
                    Guard.Fail(FrameGuard, e, this);
                }
            }

            private void OnLateUpdate()
            {
                if (_mapOpen)
                {
                    try
                    {
                        Light();
                    }
                    catch (Exception e)
                    {
                        _lightFailed = true;
                        Logger?.Error(e);
                    }
                }
                if (ReferenceEquals(_card, null) || _cardOff)
                {
                    return;
                }
                EntityController target = GameMembers.TargetOf(_card);
                if (ReferenceEquals(target, _carded))
                {
                    return;
                }
                _carded = target;
                if (!ReferenceEquals(target, null))
                {
                    try
                    {
                        Card(_card, target);
                    }
                    catch (Exception e) when (Guard.StandsDown(e, "the mod's lines on the game's building card"))
                    {
                        _cardOff = true;
                    }
                }
            }
        }
    }
}
