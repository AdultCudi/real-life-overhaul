using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Show on map: a lot, a project, a tender's works or a building of the player's made the game's own destination, one way
    /// for all (ConstructionGame.Destination, the game's Set destination). A standing building is marked by its address; an
    /// empty lot or a site with no building yet by its front on its street, so the guider leads to the pavement before it.
    /// A notice of the game's says where: the street and number, and the district. On a press only.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const float FrontOut = 0.8f;   // m past a lot's front edge: on the pavement before it, where the ribbon is cut

        /// <summary>Where a press of Show on map leads: a standing building's address, or a spot on the street.</summary>
        internal struct MapPlace
        {
            public string Street;
            public int Number;
            public bool Building;   // a standing building, marked by its address
            public Vector3 Spot;    // the front on the street, for a place with no building
            public string District; // in words
        }

        /// <summary>Show on map for a lot of the district (for sale, the player's, a landmark's site). The answer's text key, or null when it worked.</summary>
        public string ShowLot(string lotId)
        {
            return Show(PlaceOfLot(lotId, out MapPlace place), place);
        }

        /// <summary>Show on map for a project: its lot's front or its building. The answer's text key, or null when it worked.</summary>
        public string ShowProject(ProjectFile p)
        {
            return Show(PlaceOf(p, out MapPlace place), place);
        }

        /// <summary>Show on map for a tender's works: the lot, the building or the city's site. The answer's text key, or null when it worked.</summary>
        public string ShowTender(TenderFile t)
        {
            MapPlace place = default(MapPlace);
            return Show(t != null && PlaceOf(t.Lot, t.Street, t.Number, t.SiteId, null, out place), place);
        }

        /// <summary>Show on map for one of the city's buildings (the player's own, a business of theirs). The answer's text key, or null when it worked.</summary>
        public string ShowBuilding(string street, int number)
        {
            return Show(PlaceOf(null, street, number, 0, null, out MapPlace place), place);
        }

        /// <summary>True when a project has a place the map can show now: the button is offered only then.</summary>
        public bool HasPlace(ProjectFile p)
        {
            return PlaceOf(p, out _);
        }

        /// <summary>True when a tender's works have a place the map can show now.</summary>
        public bool HasPlace(TenderFile t)
        {
            return t != null && PlaceOf(t.Lot, t.Street, t.Number, t.SiteId, null, out _);
        }

        internal bool PlaceOf(ProjectFile p, out MapPlace place)
        {
            place = default(MapPlace);
            return p != null && PlaceOf(p.Lot, p.Street, p.Number, p.SiteId, p.District, out place);
        }

        private bool PlaceOfLot(string lotId, out MapPlace place)
        {
            return PlaceOf(lotId, null, 0, 0, null, out place);
        }

        // A lot first (its building once one stands there, else its front on its street), then a building's address, then
        // the city's site where the works have neither.
        private bool PlaceOf(string lotId, string street, int number, int siteId, string district, out MapPlace place)
        {
            place = default(MapPlace);
            if (!string.IsNullOrEmpty(lotId) && Lot(lotId, out LotView lot))
            {
                place.Street = lot.Street;
                place.Number = lot.Number;
                place.Building = !string.IsNullOrEmpty(lot.Street) && ConstructionGame.Stands(lot.Street, lot.Number);
                place.Spot = LotFront(lot);
                place.District = ConstructionText.T(string.IsNullOrEmpty(lot.DistrictKey) ? BuildCatalogue.DistrictKeys[0] : lot.DistrictKey);
                return true;
            }
            if (!string.IsNullOrEmpty(street))
            {
                place.Street = street;
                place.Number = number;
                place.Building = ConstructionGame.Stands(street, number);
                place.District = DistrictOf(street, number, district);
                if (place.Building)
                {
                    return true;
                }
            }
            SiteFacts site = siteId > 0 ? SiteOf(siteId) : default(SiteFacts);
            if (site.Known)
            {
                place.Spot = site.At;
                place.Street = place.Street ?? site.Street;
                place.Number = place.Street == site.Street ? site.Number : place.Number;
                place.District = place.District ?? DistrictOf(site.Street, site.Number, null);
                return true;
            }
            return false;
        }

        private static string DistrictOf(string street, int number, string district)
        {
            if (string.IsNullOrEmpty(district) && ConstructionGame.Building(street, number, out BuildingView b))
            {
                district = b.District;
            }
            return string.IsNullOrEmpty(district) ? null : ConstructionGame.DistrictWords(district);
        }

        /// <summary>A lot's front on its street: its middle moved out along the way its front looks, past its edge onto the pavement.</summary>
        internal static Vector3 LotFront(LotView lot)
        {
            double a = lot.Yaw * Math.PI / 180.0;
            float half = lot.Depth * 0.5f + FrontOut;
            return new Vector3(lot.X - (float)Math.Sin(a) * half, lot.Y, lot.Z - (float)Math.Cos(a) * half);
        }

        // The one way: the game's destination at the place, and its notice saying where.
        private string Show(bool found, MapPlace place)
        {
            if (!found)
            {
                return "reallife_bld_map_none";
            }
            string words = ConstructionGame.AddressText(place.Street, place.Number);
            if (!ConstructionGame.Destination(place.Street, place.Number, place.Spot, words, place.Building))
            {
                Logger?.Info("Construction: Show on map for " + words + " was not taken by the game's destination guider.");
                return "reallife_bld_map_refused";
            }
            var data = new Dictionary<string, string>(2) { { "place", words }, { "district", place.District ?? string.Empty } };
            Notifications.Show(NotificationType.Success, string.IsNullOrEmpty(place.District) ? "reallife_bld_map_set_plain" : "reallife_bld_map_set", data, 6f);
            if (Verbose)
            {
                Logger?.Info("Construction: Show on map made " + words + " the game's destination, " + (place.Building ? "its building by address." : "a spot at "
                             + place.Spot.x.ToString("F0") + ", " + place.Spot.z.ToString("F0") + " on its street."));
            }
            return null;
        }
    }
}
