using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using UI;
using UI.Guiders;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>One place the minimap can mark: where it is, and the picture and colour the game's own map gives it.</summary>
    public struct NavMark
    {
        public float X, Z;
        public Sprite Icon;
        public Color32 Colour;
    }

    /// <summary>
    /// The places the minimap marks: the game's guiders but the one routed to, its permanent markers, the subway
    /// stations and the mod's places ticked on the city map, gathered every two seconds while the minimap shows. A
    /// marker's picture and colour are private to the game, read through fields found once.
    /// </summary>
    public static class NavMarks
    {
        private const int Most = 512;

        public static IModLogger Logger;

        public static readonly List<NavMark> All = new List<NavMark>(128);

        /// <summary>Marks the "mark three buildings" test has put out for a while; empty otherwise.</summary>
        public static readonly List<NavMark> Trial = new List<NavMark>(3);

        private static FieldInfo _blob, _icon, _iconImage, _words;
        private static bool _looked, _missing;

        /// <summary>The round picture every marker of the game stands on; null if it could not be read.</summary>
        public static Sprite Blob { get; private set; }

        public static void Reset()
        {
            All.Clear();
            Trial.Clear();
            Blob = null;
            _looked = false;
            _missing = false;
        }

        private static void Look(CityMap map)
        {
            _looked = true;
            try
            {
                _blob = GameContract.PoiBlob.Field;
                _icon = GameContract.PoiIcon.Field;
                _iconImage = GameContract.BuildingIconImage.Field;
                _words = GameContract.PoiText.Field;
                var blob = _blob != null && map.poiTemplate != null ? _blob.GetValue(map.poiTemplate) as Image : null;
                Blob = blob != null ? blob.sprite : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _missing = _blob == null || _icon == null || _iconImage == null || Blob == null;
            if (_missing)
            {
                Logger?.Warn("Navigation: the game's map markers are not built as this version expects, so the minimap leaves the player's own buildings off"
                             + " and marks the other places on a plain circle.");
            }
        }

        /// <summary>The words on a marker's name plate, without the distance the game adds; null if unknown.</summary>
        public static string Words(PointOfInterest marker)
        {
            Prepare();
            return _words != null && marker != null ? _words.GetValue(marker) as string : null;
        }

        /// <summary>Finds the private fields and the markers' round base, once per city.</summary>
        public static void Prepare()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (!_looked && city != null && city.cityMap != null)
            {
                Look(city.cityMap);
            }
        }

        /// <summary>The picture and colour of one of the game's markers; false if they cannot be read.</summary>
        public static bool Read(PointOfInterest marker, out Sprite icon, out Color32 colour)
        {
            icon = null;
            colour = new Color32(255, 255, 255, 255);
            if (_missing || marker == null)
            {
                return false;
            }
            var blob = _blob.GetValue(marker) as Image;
            var holder = _icon.GetValue(marker) as BuildingIcon;
            var image = holder != null ? _iconImage.GetValue(holder) as Image : null;
            if (blob == null)
            {
                return false;
            }
            colour = blob.color;
            colour.a = 255;
            icon = image != null ? image.sprite : null;
            return true;
        }

        /// <summary>Reads every place again; <paramref name="routed"/>, the guider routed to, has a marker of its own.</summary>
        public static void Gather(DirectionGuider routed)
        {
            All.Clear();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (city == null || city.cityMap == null)
            {
                return;
            }
            if (!_looked)
            {
                Look(city.cityMap);
            }
            List<DirectionGuider> guiders = GuidersManager.AllGuiders;
            for (int i = 0; i < guiders.Count; i++)
            {
                DirectionGuider guider = guiders[i];
                if (guider == null || guider == routed || guider.target == null)
                {
                    continue;
                }
                Read(guider.poi, out Sprite icon, out Color32 colour);
                Add(guider.target.position, icon != null ? icon : guider.poiICon, colour);
            }
            List<PointOfInterest> markers = city.cityMap.pois;
            for (int i = 0; i < markers.Count; i++)
            {
                PointOfInterest marker = markers[i];
                if (marker == null || !marker.Permanent || marker.isGuider || marker.hidden || marker.target == null)
                {
                    continue;
                }
                if (Read(marker, out Sprite icon, out Color32 colour))
                {
                    Add(marker.target.position, icon, colour);
                }
            }
            if (uis != null && uis.mapFilters != null && city.subwayStations != null)
            {
                Color32 colour = uis.mapFilters.subwayStationFilterColor;
                colour.a = 255;
                for (int i = 0; i < city.subwayStations.Count; i++)
                {
                    SubwayStation station = city.subwayStations[i];
                    if (station != null)
                    {
                        Add(station.transform.position, uis.mapFilters.subwayStationIcon, colour);
                    }
                }
            }
            IReadOnlyList<MapPlaces.Category> kinds = MapPlaces.All;
            for (int k = 0; k < kinds.Count; k++)
            {
                MapPlaces.Category kind = kinds[k];
                if (!kind.Shown)
                {
                    continue;
                }
                Color32 colour = kind.Colour;
                colour.a = 255;
                IReadOnlyList<MapPlace> places = kind.Places;
                for (int i = 0; i < places.Count; i++)
                {
                    if (places[i] != null)
                    {
                        Add(places[i].Position, kind.Icon, colour);
                    }
                }
            }
            for (int i = 0; i < Trial.Count; i++)
            {
                All.Insert(i, Trial[i]);   // first, so a full view cannot crowd them out
            }
            NavBridges.MoreMarks?.Invoke(All);
        }

        private static void Add(Vector3 at, Sprite icon, Color32 colour)
        {
            if (All.Count < Most)
            {
                All.Add(new NavMark { X = at.x, Z = at.z, Icon = icon, Colour = colour });
            }
        }
    }
}
