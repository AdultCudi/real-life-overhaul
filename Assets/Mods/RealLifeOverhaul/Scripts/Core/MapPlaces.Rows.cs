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
    /// <summary>The map places' rows in the game's filter list, made by its own private method and ticked by the player.</summary>
    public static partial class MapPlaces
    {
        private static void AddRows(CityMapFilters filters)
        {
            try
            {
                // Null when a game update took one away.
                FieldInfo togglesField = GameContract.FilterToggles.Field;
                FieldInfo entriesField = GameContract.FilterEntries.Field;
                var toggles = togglesField != null ? togglesField.GetValue(filters) as Dictionary<string, Toggle> : null;
                var entries = entriesField != null ? entriesField.GetValue(filters) as Dictionary<string, CityMapFilter> : null;
                if (toggles == null || entries == null)
                {
                    Fail("its table of rows is not where it was");
                    return;
                }
                if (toggles.Count == 0)
                {
                    return;   // the game's list is not built yet: looked at again in a second
                }
                MethodInfo create = GameContract.FilterCreate.Method;
                ParameterInfo[] parameters = create != null ? create.GetParameters() : null;
                if (parameters == null || parameters.Length != 7 || parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(Sprite)
                    || parameters[2].ParameterType != typeof(CityMapFilterData) || parameters[3].ParameterType != typeof(CityMapFilterCategory)
                    || !parameters[4].ParameterType.IsValueType || parameters[6].ParameterType != typeof(Func<Vector3?>))
                {
                    Fail("the method that makes a row has changed");
                    return;
                }
                CityMapFilterCategory under = filters.GetCategory(SpecialBuildings);
                bool own = under == null;
                if (own)
                {
                    MethodInfo line = GameContract.FilterCreateLine.Method;
                    under = line != null ? line.Invoke(filters, new object[] { OwnCategory }) as CityMapFilterCategory : null;
                }
                if (under == null)
                {
                    Fail("no category to put the rows under");
                    return;
                }
                _filters = filters;
                int added = 0;
                for (int i = 0; i < Categories.Count; i++)
                {
                    Category category = Categories[i];
                    if (category.Toggle != null)
                    {
                        continue;
                    }
                    string name = Pref + category.Id;
                    var data = new CityMapFilterData { isAvailable = Guard.Asked(() => category.Offered, false) };
                    Func<Vector3?> focus = Guard.Asked<Vector3?>(() => Nearest(category), null);
                    object languageData = Activator.CreateInstance(parameters[4].ParameterType);
                    var row = create.Invoke(filters, new object[] { name, category.Icon, data, under, languageData, null, focus }) as CityMapFilter;
                    if (row == null || row.Toggle == null)
                    {
                        Fail("a row came back empty");
                        return;
                    }
                    // Out of the game's table, so "toggle all" never writes its name into the save, and set up again not
                    // to write it there itself.
                    toggles.Remove(name);
                    entries.Remove(name);
                    row.SetUp(category.TitleKey, Guard.Wrap<bool>(on => Ticked(category, on)), category.Icon, data, true, default, null, focus);
                    row.name = name;
                    row.Toggle.SetIsOnWithoutNotify(category.On);
                    category.Row = row;
                    category.Toggle = row.Toggle;
                    Transform list = row.transform.parent;
                    Transform next = !own && list != null ? list.Find(NextCategory) : null;
                    if (next != null)
                    {
                        row.transform.SetSiblingIndex(next.GetSiblingIndex());   // from the end of the list to the end of its category
                    }
                    added++;
                }
                if (added > 0)
                {
                    // The two switches that tick rows without telling them: the category's own, and "toggle all" at the top.
                    _sweptHeard = _sweptHeard ?? Guard.Listener<bool>(Swept);   // made once
                    _sweptAllHeard = _sweptAllHeard ?? Guard.Listener<bool>(SweptAll);
                    under.ToggleAll.onValueChanged.RemoveListener(_sweptHeard);
                    under.ToggleAll.onValueChanged.AddListener(_sweptHeard);
                    var all = GameContract.FilterToggleAll.Field?.GetValue(filters) as Toggle;
                    if (all != null)
                    {
                        all.onValueChanged.RemoveListener(_sweptAllHeard);
                        all.onValueChanged.AddListener(_sweptAllHeard);
                    }
                    under.UpdateToggleAllState();
                    Logger?.Info("Map places: " + added + " row(s) added to the city map's filters" + (own ? " (under a category of their own)." : " (under special buildings)."));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Fail("an error (above)");
            }
        }

        private static void Fail(string why)
        {
            if (_filterFailed)
            {
                return;
            }
            _filterFailed = true;
            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i].Toggle == null)
                {
                    Categories[i].On = true;
                }
            }
            Logger?.Warn("Map places: the city map's filter list could not be given the mod's rows (" + why + "); the mod's places are always shown on the map instead.");
            // Told once: without rows only the option still turns the places off.
            try
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, "reallife_map_rows_failed", null, 14f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Refresh();
        }

        private static void Ticked(Category category, bool on)
        {
            if (Said.Add("clicked " + category.Id))
            {
                Logger?.Info("Map places: the row '" + category.Id + "' " + (on ? "ticked" : "unticked") + " by the player (the first time this session); "
                             + category.Places.Count + " place(s), " + (category.Offered ? "offered" : "not offered") + ".");
            }
            Remember(category, on);
            Apply(category);
            Refilter(_filters);   // as the game's own rows do when ticked
        }

        private static UnityAction<bool> _sweptHeard, _sweptAllHeard;

        // The category's switch has gone over every row under it, the mod's included, without telling them.
        private static void Swept(bool on)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Category category = Categories[i];
                if (category.Toggle != null && category.Toggle.isOn != category.On)
                {
                    Remember(category, category.Toggle.isOn);
                    Apply(category);
                }
            }
        }

        // "Toggle all" goes over the game's table of rows, which the mod's rows are not in: they are ticked to match.
        private static void SweptAll(bool on)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Category category = Categories[i];
                if (category.Toggle != null && category.Offered)
                {
                    category.Toggle.SetIsOnWithoutNotify(on);
                    Remember(category, on);
                    Apply(category);
                }
            }
        }

        // While the map is open the tick the player sees counts: a tick changed without telling the row (another mod's
        // switch) is followed. Logged once a session per row.
        private static void Follow(Category category, bool open)
        {
            Toggle toggle = category.Toggle;
            if (!open || toggle == null || toggle.isOn == category.On)
            {
                return;
            }
            if (Said.Add(category.Id))
            {
                Logger?.Info("Map places: the row '" + category.Id + "' was " + (toggle.isOn ? "ticked" : "unticked")
                             + " without telling it; its places follow the tick from here.");
            }
            Remember(category, toggle.isOn);
        }

        private static void Remember(Category category, bool on)
        {
            if (category.On != on)
            {
                category.On = on;
                UnityEngine.PlayerPrefs.SetInt(Pref + category.Id, on ? 1 : 0);
            }
        }

        // Where a row's arrow button flies the map: the category's place nearest the player.
        private static Vector3? Nearest(Category category)
        {
            if (Helpers.PlayerHelper.PlayerController == null)
            {
                return null;
            }
            Vector3 from = Helpers.PlayerHelper.GetCityPosition();
            float best = float.MaxValue;
            Vector3? found = null;
            IReadOnlyList<MapPlace> places = category.Places;
            for (int i = 0; i < places.Count; i++)
            {
                float sqr = (places[i].Position - from).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    found = places[i].Position;
                }
            }
            return found;
        }
    }
}
