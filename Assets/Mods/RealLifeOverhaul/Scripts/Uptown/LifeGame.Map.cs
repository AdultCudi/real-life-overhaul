using System;
using System.Collections.Generic;
using System.Reflection;
using City.CityMap;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown on the game's city map, as a district of the game is there: its quad among the map's district zones (a copy of
    /// Midtown's, scaled over the district's ground and given its own colour), which the game shows when its row is ticked
    /// and asks for that row's focus (CityMapNeighborhoodZones.First throws for a district without one), and its row in the
    /// map's filter list among the game's district rows, made by the game's own method as it makes them. A list the game
    /// builds after the zone is in makes the row itself (the record says it has one). Nothing here runs a frame: once at a
    /// load, and once when Uptown stands down.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string NeighborhoodsCategory = "common_neighborhoods";   // the game's category of its district rows (CityMapFilters.CreateNeighborhoodsCategory)
        private static readonly int ZoneColour = Shader.PropertyToID("_UnlitColor");   // the property the game colours its zones by (CityMapNeighborhoodZones)

        private static GameObject _zone;       // Uptown's quad, destroyed when the city goes
        private static CityMapFilter _row;     // its row, if this load made it (the game's own list keeps it)
        private static MaterialPropertyBlock _block;

        /// <summary>True when Uptown has its quad among the map's district zones at this load.</summary>
        public static bool Zoned => _zone != null;

        /// <summary>True when the map's filter list has Uptown's row (made by the game, or at this load).</summary>
        public static bool Rowed()
        {
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var toggles = uis != null && uis.mapFilters != null ? GameContract.FilterToggles.Field?.GetValue(uis.mapFilters) as Dictionary<string, Toggle> : null;
            return toggles != null && toggles.ContainsKey(DistrictValues.Id);
        }

        /// <summary>
        /// Uptown's zone over a rectangle of the world (its ground's bounds), then its row once the map's filter list is
        /// built. True when the zone is there (its row is made then or by the game); false with why.
        /// </summary>
        public static bool Map(float x1, float z1, float x2, float z2, out string why)
        {
            why = null;
            if (_record == null)
            {
                why = "the district is not among the game's districts";
                return false;
            }
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            CityMapNeighborhoodZones zones = city != null && city.cityMap != null ? city.cityMap.cityMapNeighborhoodZones : null;
            FieldInfo field = GameContract.DistrictZones.Field;
            var all = zones != null && field != null ? field.GetValue(zones) as CityMapNeighborhoodZones.CityMapNeighborhoodZone[] : null;
            if (all == null)
            {
                why = "the city map's district zones could not be reached (" + (GameContract.UptownDistrict.Why ?? "not in the scene") + ")";
                return false;
            }
            CityMapNeighborhoodZones.CityMapNeighborhoodZone template = null;
            float south = Mathf.Min(z1, z2), north = Mathf.Max(z1, z2);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].zoneRenderer == null)
                {
                    continue;
                }
                if (all[i].neighbourhood == DistrictValues.Id)
                {
                    Row(x1, z1, x2, z2, out why);   // the zone there from earlier in this load: its row, if the list was not built then
                    return true;
                }
                if (template == null || all[i].neighbourhood == DistrictValues.Midtown)
                {
                    template = all[i];   // Midtown's, its southern neighbour, else the first
                }
                MeshFilter shape = all[i].zoneRenderer.GetComponent<MeshFilter>();
                if (shape != null && shape.sharedMesh != null)
                {
                    Bounds b = shape.sharedMesh.bounds;
                    Vector3 a = all[i].zoneRenderer.transform.TransformPoint(b.min), c = all[i].zoneRenderer.transform.TransformPoint(b.max);
                    south = DistrictValues.SouthClear(x1, south, x2, north, Mathf.Min(a.x, c.x), Mathf.Max(a.x, c.x), Mathf.Min(a.z, c.z), Mathf.Max(a.z, c.z));
                }
            }
            z1 = south;   // clear of the game's zones south of it, so no stretch of the map is tinted twice
            z2 = north;
            MeshFilter mesh = template != null ? template.zoneRenderer.GetComponent<MeshFilter>() : null;
            if (mesh == null || mesh.sharedMesh == null)
            {
                why = "the game has no district zone to copy";
                return false;
            }
            Bounds local = mesh.sharedMesh.bounds;
            Transform from = template.zoneRenderer.transform;
            if (!DistrictValues.ZoneOver(x1, z1, x2, z2, local.min.x, local.max.x, local.min.z, local.max.z, out float x, out float z, out float scaleX, out float scaleZ))
            {
                why = "the game's district zones have no extent";
                return false;
            }
            _zone = UnityEngine.Object.Instantiate(from.gameObject, from.parent, false);   // the game's zones sleep until their row is ticked: so does the copy
            _zone.name = "Uptown";
            Transform t = _zone.transform;
            Vector3 parent = from.parent != null ? from.parent.lossyScale : Vector3.one;
            t.position = new Vector3(x, from.position.y, z);
            t.rotation = from.rotation;
            t.localScale = new Vector3(scaleX / Mathf.Max(parent.x, 1e-4f), from.localScale.y, scaleZ / Mathf.Max(parent.z, 1e-4f));
            MeshRenderer look = _zone.GetComponent<MeshRenderer>();
            _block = _block ?? new MaterialPropertyBlock();
            look.GetPropertyBlock(_block);
            _block.SetColor(ZoneColour, _record.cityMapZoneColor);   // as the game colours its own at their Start
            look.SetPropertyBlock(_block);
            var now = new CityMapNeighborhoodZones.CityMapNeighborhoodZone[all.Length + 1];
            Array.Copy(all, now, all.Length);
            now[all.Length] = new CityMapNeighborhoodZones.CityMapNeighborhoodZone { neighbourhood = DistrictValues.Id, zoneRenderer = look };
            field.SetValue(zones, now);
            _record.hasCityMapFilter = true;   // a list built from here on makes its row as for any district
            Row(x1, z1, x2, z2, out why);
            return true;
        }

        // Its row among the game's district rows, once the list is built (the game builds it when the map's filters first start).
        private static bool Row(float x1, float z1, float x2, float z2, out string why)
        {
            why = null;
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            CityMapFilters filters = uis != null ? uis.mapFilters : null;
            var toggles = filters != null ? GameContract.FilterToggles.Field?.GetValue(filters) as Dictionary<string, Toggle> : null;
            MethodInfo create = GameContract.FilterCreate.Method;
            if (filters == null || toggles == null || create == null)
            {
                why = "the city map's filter list could not be reached (" + (GameContract.MapPlaces.Why ?? "not in the scene") + ")";
                return false;
            }
            if (toggles.Count == 0 || toggles.ContainsKey(DistrictValues.Id))
            {
                return true;   // not built yet (the game makes Uptown's row with the rest), or made already
            }
            CityMapFilterCategory under = filters.GetCategory(NeighborhoodsCategory);
            ParameterInfo[] parameters = create.GetParameters();
            if (under == null || parameters.Length != 7 || !parameters[4].ParameterType.IsValueType)
            {
                why = "the city map has no category of district rows, or makes its rows another way";
                return false;
            }
            object languageData = Activator.CreateInstance(parameters[4].ParameterType);
            Vector3? focus = new Vector3((x1 + x2) * 0.5f, UptownLots.GroundY, (z1 + z2) * 0.5f);   // the middle of the district's ground
            _row = create.Invoke(filters, new object[] { DistrictValues.Id, _record.cityMapIcon, new CityMapFilterData { neighbourhood = DistrictValues.Id }, under, languageData, focus, null })
                as CityMapFilter;
            if (_row == null)
            {
                why = "the game's own row came back empty";
                return false;
            }
            under.UpdateToggleAllState();
            return true;
        }

        /// <summary>Uptown stood down in play: its row unticked and put away, its zone hidden; its record stays for what still holds its id.</summary>
        public static void Unmap()
        {
            if (_record != null)
            {
                _record.hasCityMapFilter = false;
            }
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var toggles = uis != null && uis.mapFilters != null ? GameContract.FilterToggles.Field?.GetValue(uis.mapFilters) as Dictionary<string, Toggle> : null;
            if (toggles != null && toggles.TryGetValue(DistrictValues.Id, out Toggle toggle) && toggle != null)
            {
                toggle.isOn = false;   // through the game's own handler, so its zone and its buildings' outlines go and the save's list loses its name
                toggle.gameObject.SetActive(false);
            }
            if (_zone != null)
            {
                _zone.SetActive(false);
            }
        }

        // The city goes: the zone is the mod's own copy (the game's array goes with the scene).
        private static void UnloadMap()
        {
            if (_zone != null)
            {
                UnityEngine.Object.Destroy(_zone);
            }
            _zone = null;
            _row = null;
        }
    }
}
