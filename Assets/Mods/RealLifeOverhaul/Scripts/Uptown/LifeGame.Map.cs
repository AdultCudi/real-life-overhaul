using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Neighborhoods;
using City.CityMap;
using Localizor;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The band's districts on the game's city map, as a district of the game is there: each one's quad among the map's district
    /// zones (a copy of Midtown's, scaled over the district's ground and given its own colour), which the game shows when its row
    /// is ticked and asks for that row's focus (CityMapNeighborhoodZones.First throws for a district without one), and its row in
    /// the map's filter list among the game's district rows, made by the game's own method as it makes them and stood where the
    /// game stands its own: the game builds the list once, when its UI scene first starts (the filters' object is active in
    /// that scene, its Start builds the list), before the city's step gives the districts their zones, and a row its
    /// CreateFilter makes afterwards is put at the end of the whole list, under the last category; so the row is moved up to the
    /// end of the districts' own rows. A list the game builds after the zone is in makes the row itself (the record says it has
    /// one). Each district has a zone and a row of its own, cut along 5th Street (DistrictSplit.Bounds). Nothing here runs a
    /// frame: once at a load, and once when the band stands down.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string NeighborhoodsCategory = "common_neighborhoods";   // the game's category of its district rows (CityMapFilters.CreateNeighborhoodsCategory)
        private static readonly int ZoneColour = Shader.PropertyToID("_UnlitColor");   // the property the game colours its zones by (CityMapNeighborhoodZones)

        private static readonly GameObject[] Zones = new GameObject[DistrictSplit.Count];     // each district's quad, destroyed when the city goes
        private static readonly CityMapFilter[] RowsMade = new CityMapFilter[DistrictSplit.Count];   // each row, if this load made it (the game's own list keeps it)
        private static MaterialPropertyBlock _block;
        private static readonly Sprite[] Icons = new Sprite[DistrictSplit.Count];   // each district's toggle disc, in its colour, destroyed when the city goes
        private const int IconSize = 64;                                             // pixels

        // The disc on a district's row in the map's list, drawn once a load in the district's own colour (the game's rows show a disc of theirs).
        private static Sprite Icon(int district)
        {
            if (Icons[district] != null)
            {
                return Icons[district];
            }
            float[] colour = DistrictValues.ColourOf(district);
            var tint = new Color(colour[0], colour[1], colour[2], 1f);
            var disc = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color[IconSize * IconSize];
            float radius = IconSize / 2f - 2f;
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    Color pixel = tint;
                    pixel.a = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(IconSize / 2f, IconSize / 2f)));
                    pixels[y * IconSize + x] = pixel;
                }
            }
            disc.SetPixels(pixels);
            disc.Apply();
            Sprite icon = Sprite.Create(disc, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f));
            icon.name = DistrictSplit.IdOf(district);
            Icons[district] = icon;
            return icon;
        }

        /// <summary>
        /// Every district's zone colour, the game's seven and the band's two as the game holds them now, as one line for the log, and null when no
        /// two are nearer than DistrictValues.ColourLeast; else which two are. Asked once a load.
        /// </summary>
        public static string ColourWhy(out string line)
        {
            var names = new List<string>(12);
            var colours = new List<float[]>(12);
            NeighborhoodData[] all = NeighborhoodHelper.NeighborhoodsData;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null && all[i].hasCityMapFilter)
                {
                    names.Add(all[i].neighbourhood.Substring(all[i].neighbourhood.LastIndexOf('_') + 1));
                    colours.Add(new[] { all[i].cityMapZoneColor.r, all[i].cityMapZoneColor.g, all[i].cityMapZoneColor.b });
                }
            }
            return DistrictValues.ColourWhy(names, colours, DistrictValues.ColourLeast, out line);
        }

        /// <summary>True when every district has its quad among the map's district zones at this load.</summary>
        public static bool Zoned
        {
            get
            {
                for (int i = 0; i < Zones.Length; i++)
                {
                    if (Zones[i] == null)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>True when the map's filter list has a district's row (made by the game, or at this load).</summary>
        public static bool Rowed(int district)
        {
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var toggles = uis != null && uis.mapFilters != null ? GameContract.FilterToggles.Field?.GetValue(uis.mapFilters) as Dictionary<string, Toggle> : null;
            return toggles != null && toggles.ContainsKey(DistrictSplit.IdOf(district));
        }

        /// <summary>
        /// True when a district's row stands among the game's district rows: under the districts' category, right after its line
        /// with nothing but district rows before it, and named by the district's own text; else why not.
        /// </summary>
        public static bool RowPlaced(int district, out string why)
        {
            why = null;
            string id = DistrictSplit.IdOf(district);
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            CityMapFilters filters = uis != null ? uis.mapFilters : null;
            var entries = filters != null ? GameContract.FilterEntries.Field?.GetValue(filters) as Dictionary<string, CityMapFilter> : null;
            CityMapFilterCategory under = filters != null ? filters.GetCategory(NeighborhoodsCategory) : null;
            if (entries == null || under == null || !entries.TryGetValue(id, out CityMapFilter row) || row == null)
            {
                why = "the map's list has no row of it, or no category of districts";
                return false;
            }
            Transform t = row.transform, line = under.transform, list = line.parent;
            if (row.category != under || t.parent != list)
            {
                why = "its row is under another category";
                return false;
            }
            int at = t.GetSiblingIndex(), from = line.GetSiblingIndex();
            if (at <= from)
            {
                why = "its row stands above the districts' line";
                return false;
            }
            for (int i = from + 1; i < at; i++)
            {
                CityMapFilter other = list.GetChild(i).GetComponent<CityMapFilter>();
                if (other == null || other.category != under)
                {
                    why = "its row stands below " + list.GetChild(i).name + ", outside the districts' rows";
                    return false;
                }
            }
            string name = id.GetLocalization();
            if (string.IsNullOrEmpty(name) || name == id)
            {
                why = "the district's name has no text in this language";
                return false;
            }
            return true;
        }

        // A row made after the list was built stands at the list's very end (CityMapFilters.CreateFilter instantiates its template
        // under the list): moved to the end of its category's own rows, where the game stands each of its district rows.
        private static void Place(CityMapFilter row, CityMapFilterCategory under)
        {
            Transform t = row != null ? row.transform : null, line = under != null ? under.transform : null;
            Transform list = line != null ? line.parent : null;
            if (t == null || list == null || t.parent != list)
            {
                return;
            }
            int at = line.GetSiblingIndex() + 1;
            for (int i = at; i < list.childCount; i++)
            {
                Transform child = list.GetChild(i);
                if (child == t)
                {
                    continue;
                }
                CityMapFilter other = child.GetComponent<CityMapFilter>();
                if (other == null || other.category != under)
                {
                    break;   // the next category's line, or a row of another
                }
                at = i + 1;
            }
            int now = t.GetSiblingIndex();
            t.SetSiblingIndex(now < at ? at - 1 : at);
        }

        /// <summary>
        /// The districts' zones over their halves of the band's ground (DistrictSplit.Bounds), then their rows once the map's
        /// filter list is built. True when the zones are there (their rows are made then or by the game); false with why.
        /// </summary>
        public static bool Map(IReadOnlyList<GroundRect> ground, out string why)
        {
            why = null;
            if (!AnyRecord)
            {
                why = "the district is not among the game's districts";
                return false;
            }
            bool all = true;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                if (!DistrictSplit.Bounds(d, ground, out float x1, out float z1, out float x2, out float z2))
                {
                    why = "the ground has nothing in " + DistrictSplit.IdOf(d);
                    all = false;
                    continue;
                }
                if (!MapOne(d, x1, z1, x2, z2, out string one))
                {
                    why = one;
                    all = false;
                }
            }
            return all;
        }

        private static bool MapOne(int district, float x1, float z1, float x2, float z2, out string why)
        {
            why = null;
            string id = DistrictSplit.IdOf(district);
            NeighborhoodData record = Record(district);
            if (record == null)
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
                if (all[i].neighbourhood == id)
                {
                    Row(district, x1, z1, x2, z2, out why);   // the zone there from earlier in this load: its row, if the list was not built then
                    return true;
                }
                if (DistrictSplit.Is(all[i].neighbourhood))
                {
                    continue;   // the other half of the band: its quad is no template and no ground of the game's to keep clear of
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
            GameObject made = UnityEngine.Object.Instantiate(from.gameObject, from.parent, false);   // the game's zones sleep until their row is ticked: so does the copy
            made.name = district == DistrictSplit.CedarHeights ? "Cedar Heights" : "Uptown";
            Zones[district] = made;
            Transform t = made.transform;
            Vector3 parent = from.parent != null ? from.parent.lossyScale : Vector3.one;
            t.position = new Vector3(x, from.position.y, z);
            t.rotation = from.rotation;
            t.localScale = new Vector3(scaleX / Mathf.Max(parent.x, 1e-4f), from.localScale.y, scaleZ / Mathf.Max(parent.z, 1e-4f));
            MeshRenderer look = made.GetComponent<MeshRenderer>();
            _block = _block ?? new MaterialPropertyBlock();
            look.GetPropertyBlock(_block);
            _block.SetColor(ZoneColour, record.cityMapZoneColor);   // as the game colours its own at their Start
            look.SetPropertyBlock(_block);
            var now = new CityMapNeighborhoodZones.CityMapNeighborhoodZone[all.Length + 1];
            Array.Copy(all, now, all.Length);
            now[all.Length] = new CityMapNeighborhoodZones.CityMapNeighborhoodZone { neighbourhood = id, zoneRenderer = look };
            field.SetValue(zones, now);
            record.hasCityMapFilter = true;   // a list built from here on makes its row as for any district
            Row(district, x1, z1, x2, z2, out why);
            return true;
        }

        // Its row among the game's district rows, once the list is built (the game builds it when the map's filters first start).
        private static bool Row(int district, float x1, float z1, float x2, float z2, out string why)
        {
            why = null;
            string id = DistrictSplit.IdOf(district);
            NeighborhoodData record = Record(district);
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            CityMapFilters filters = uis != null ? uis.mapFilters : null;
            var toggles = filters != null ? GameContract.FilterToggles.Field?.GetValue(filters) as Dictionary<string, Toggle> : null;
            MethodInfo create = GameContract.FilterCreate.Method;
            if (filters == null || toggles == null || create == null || record == null)
            {
                why = "the city map's filter list could not be reached (" + (GameContract.MapPlaces.Why ?? "not in the scene") + ")";
                return false;
            }
            if (toggles.Count == 0)
            {
                return true;   // not built yet: the game makes the district's row with the rest, in its place
            }
            CityMapFilterCategory under = filters.GetCategory(NeighborhoodsCategory);
            if (toggles.ContainsKey(id))
            {
                var entries = GameContract.FilterEntries.Field?.GetValue(filters) as Dictionary<string, CityMapFilter>;
                if (entries != null && entries.TryGetValue(id, out CityMapFilter made))
                {
                    Place(made, under);   // made at an earlier step of this load
                }
                return true;
            }
            ParameterInfo[] parameters = create.GetParameters();
            if (under == null || parameters.Length != 7 || !parameters[4].ParameterType.IsValueType)
            {
                why = "the city map has no category of district rows, or makes its rows another way";
                return false;
            }
            object languageData = Activator.CreateInstance(parameters[4].ParameterType);   // default, as the game's own district rows: the label is the district's id's text
            Vector3? focus = new Vector3((x1 + x2) * 0.5f, UptownLots.GroundY, (z1 + z2) * 0.5f);   // the middle of the district's ground
            CityMapFilter row = create.Invoke(filters, new object[] { id, record.cityMapIcon, new CityMapFilterData { neighbourhood = id }, under, languageData, focus, null })
                as CityMapFilter;
            RowsMade[district] = row;
            if (row == null)
            {
                why = "the game's own row came back empty";
                return false;
            }
            Place(row, under);
            under.UpdateToggleAllState();
            return true;
        }

        /// <summary>The band stood down in play: its rows unticked and put away, its zones hidden; the records stay for what still holds an id.</summary>
        public static void Unmap()
        {
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var toggles = uis != null && uis.mapFilters != null ? GameContract.FilterToggles.Field?.GetValue(uis.mapFilters) as Dictionary<string, Toggle> : null;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                NeighborhoodData record = Record(d);
                if (record != null)
                {
                    record.hasCityMapFilter = false;
                }
                if (toggles != null && toggles.TryGetValue(DistrictSplit.Ids[d], out Toggle toggle) && toggle != null)
                {
                    toggle.isOn = false;   // through the game's own handler, so its zone and its buildings' outlines go and the save's list loses its name
                    toggle.gameObject.SetActive(false);
                }
                if (Zones[d] != null)
                {
                    Zones[d].SetActive(false);
                }
            }
        }

        // The city goes: the zones are the mod's own copies (the game's array goes with the scene).
        private static void UnloadMap()
        {
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                if (Zones[d] != null)
                {
                    UnityEngine.Object.Destroy(Zones[d]);
                }
                Zones[d] = null;
                RowsMade[d] = null;
                Sprite icon = Icons[d];
                CodeAssets.DropSprite(icon);
                Icons[d] = null;
            }
        }
    }
}
