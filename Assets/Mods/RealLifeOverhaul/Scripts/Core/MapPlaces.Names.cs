using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using City.CityMap;
using Streets;
using UI;
using UI.Guiders;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The map places' markers named and clicked for a destination, and the building card.</summary>
    public static partial class MapPlaces
    {
        // A marker's name plate is off for all but directions: for a place it is switched on, without the "dismiss"
        // button, and the blob takes a click. Once per marker, after it first woke (it hides its plate as it does).
        private static void Label()
        {
            if (_labelFailed)
            {
                return;
            }
            for (int c = 0; c < Categories.Count; c++)
            {
                IReadOnlyList<MapPlace> places = Categories[c].Places;
                for (int i = 0; i < places.Count; i++)
                {
                    MapPlace place = places[i];
                    PointOfInterest marker = place.Marker;
                    if (marker == null || !marker.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (place.Labelled)
                    {
                        marker.SetContainerActive(true);   // nothing if it is on
                        continue;
                    }
                    try
                    {
                        if (_buttonField == null)
                        {
                            _buttonField = GameContract.PoiButton.Field;
                            _blobField = GameContract.PoiBlob.Field;
                        }
                        var dismiss = _buttonField != null ? _buttonField.GetValue(marker) as Button : null;
                        if (dismiss == null)
                        {
                            _labelFailed = true;
                            Logger?.Info("Map places: the game's marker has no name plate that can be shown without its button; the mod's markers stay without names.");
                            return;
                        }
                        dismiss.gameObject.SetActive(false);
                        marker.SetText(place.Name ?? string.Empty);
                        marker.SetContainerActive(true);
                        var blob = _blobField != null ? _blobField.GetValue(marker) as Image : null;
                        if (blob != null && blob.GetComponent<Button>() == null)
                        {
                            blob.raycastTarget = true;
                            var click = blob.gameObject.AddComponent<Button>();
                            click.targetGraphic = blob;
                            click.transition = Selectable.Transition.None;
                            click.onClick.AddListener(Guard.Listener(() => GoTo(place)));
                        }
                        place.Labelled = true;
                    }
                    catch (Exception e)
                    {
                        _labelFailed = true;
                        Logger?.Error(e);
                        return;
                    }
                }
            }
        }

        // A marker clicked on the open map: the place is the destination, and the map closes, as "set destination" does.
        private static void GoTo(MapPlace place)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (!CityMap.IsOpen || city == null || city.cityMap == null || city.cityMap.IsTaxiMode || city.cityMap.isSubwayMode
                    || place == null || place.Owner == null || InstanceBehavior<GuidersManager>.Instance == null)
                {
                    return;
                }
                GuidersManager.SetGuiderTarget(place.Position + Vector3.up * MarkerHeight, place.Name ?? string.Empty, place.Owner.Icon, place.Owner.Colour, DirectionGuiderType.Destination);
                city.cityMap.Toggle();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The card of a building that is a place: its name (a special building's own) and, if given, what it is and its
        // state. The game writes all three again whenever it shows the card, so nothing has to be put back.
        private static void Card(BuildingResume card, EntityController target)
        {
            var building = target as CityBuildingController;
            MapPlace place = Of(building);
            if (place == null || place.Owner == null || !place.Owner.Offered)
            {
                return;
            }
            try
            {
                string name = RealLifeOverhaul.SpecialBuildings.NameFor(building) ?? place.Name;
                if (card.businessNameLabel != null && !string.IsNullOrEmpty(name))
                {
                    card.businessNameLabel.gameObject.SetActive(true);
                    card.businessNameLabel.text = name;
                }
                if (card.businessTypeLabel != null && !string.IsNullOrEmpty(place.CardTypeKey))
                {
                    card.businessTypeLabel.Key = place.CardTypeKey;
                }
                if (card.openStateLabel != null && !string.IsNullOrEmpty(place.CardStateKey))
                {
                    card.openStateLabel.Key = place.CardStateKey;
                    card.openStateLabel.Prefix = string.Empty;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ByBuilding.Clear();   // the card is not as expected: left alone from here on
            }
        }
    }
}
