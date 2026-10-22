using System;
using System.Collections.Generic;
using System.Reflection;
using City.CityMap;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The map places' choices: switches in the filter list that mark no place.</summary>
    public static partial class MapPlaces
    {
        /// <summary>One row of a <see cref="Choice"/>: its label, its icon, and where its arrow button flies the map (null: no arrow).</summary>
        public sealed class ChoiceRow
        {
            public string Id;
            public string TitleKey;
            public Sprite Icon;
            public Func<Vector3?> Focus;

            internal CityMapFilter Row;
        }

        /// <summary>
        /// Rows in the map's filter list that mark no place: switches under a line of their own, at most one ticked at a
        /// time, remembered in PlayerPrefs; <see cref="Changed"/> is told.
        /// </summary>
        public sealed class Choice
        {
            private readonly ChoiceRow[] _rows;

            internal Choice(string id, string categoryKey, ChoiceRow[] rows)
            {
                Id = id;
                CategoryKey = categoryKey;
                _rows = rows;
                string remembered = UnityEngine.PlayerPrefs.GetString(Pref + id, string.Empty);
                Picked = -1;
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] != null && rows[i].Id == remembered)
                    {
                        Picked = i;
                    }
                }
            }

            public string Id { get; }
            public string CategoryKey { get; }
            public IReadOnlyList<ChoiceRow> Rows => _rows;
            /// <summary>The row ticked as remembered, or -1, whether or not the choice is offered now.</summary>
            public int Picked { get; private set; }
            /// <summary>Optional: false hides the line and its rows, when the feature's own option is off.</summary>
            public Func<bool> Available;
            /// <summary>Told when the row ticked changes, with the new one or -1.</summary>
            public Action<int> Changed;

            internal CityMapFilterCategory Line;
            internal bool Offered => Available == null || Available();
            internal bool HasRows => Line != null;

            /// <summary>Ticks a row, or none with -1, as the player would.</summary>
            public void Pick(int index)
            {
                if (index < -1 || index >= _rows.Length)
                {
                    index = -1;
                }
                ShowTicks(index);
                if (Picked == index)
                {
                    return;
                }
                Picked = index;
                UnityEngine.PlayerPrefs.SetString(Pref + Id, index >= 0 ? _rows[index].Id : string.Empty);
                try
                {
                    Changed?.Invoke(index);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }

            internal void ShowTicks(int index)
            {
                for (int i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i].Row != null)
                    {
                        _rows[i].Row.SetIsOnWithoutNotify(i == index);
                    }
                }
            }

            internal void Ticked(int index, bool on)
            {
                Pick(on ? index : Picked == index ? -1 : Picked);
            }

            internal void Forget()
            {
                Line = null;
                for (int i = 0; i < _rows.Length; i++)
                {
                    _rows[i].Row = null;
                }
            }
        }

        /// <summary>
        /// Adds a <see cref="Choice"/> to the filter list, its line just before the game's neighbourhoods. Call once per
        /// city load; the same id gives the same choice.
        /// </summary>
        public static Choice RegisterChoice(string id, string categoryKey, ChoiceRow[] rows)
        {
            for (int i = 0; i < Choices.Count; i++)
            {
                if (Choices[i].Id == id)
                {
                    return Choices[i];
                }
            }
            var choice = new Choice(id, categoryKey, rows ?? new ChoiceRow[0]);
            Choices.Add(choice);
            if (_driver == null)
            {
                var go = new GameObject("RealLifeOverhaul Map Places");
                _driver = go.AddComponent<Driver>();
            }
            return choice;
        }

        // A choice not offered keeps its line, folded to nothing: the game switches every line on when it applies a search
        // (CityMapFilterCategory.UpdateFilterVisibility); its rows hide by their own "is available" test.
        private static void Offer(Choice choice)
        {
            CityMapFilterCategory line = choice.Line;
            if (line == null)
            {
                return;
            }
            bool offered = choice.Offered;
            var layout = line.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = line.gameObject.AddComponent<LayoutElement>();
            }
            if (layout.ignoreLayout == offered)
            {
                layout.ignoreLayout = !offered;
                line.transform.localScale = offered ? Vector3.one : Vector3.zero;
                line.ApplySearch(null);   // its rows shown or hidden by their test
            }
        }

        // Made by the game's own two methods, as the categories' rows are, and taken out of its table at once. The line's
        // "toggle all" and fold are put away: at most one row is ticked. If the list is not as expected, no rows.
        private static void AddChoiceRows(CityMapFilters filters)
        {
            if (_choiceFailed)
            {
                return;
            }
            try
            {
                var toggles = GameContract.FilterToggles.Field?.GetValue(filters) as Dictionary<string, Toggle>;
                var entries = GameContract.FilterEntries.Field?.GetValue(filters) as Dictionary<string, CityMapFilter>;
                MethodInfo create = GameContract.FilterCreate.Method;
                MethodInfo makeLine = GameContract.FilterCreateLine.Method;
                ParameterInfo[] parameters = create != null ? create.GetParameters() : null;
                if (toggles == null || entries == null || makeLine == null || parameters == null || parameters.Length != 7
                    || parameters[3].ParameterType != typeof(CityMapFilterCategory) || !parameters[4].ParameterType.IsValueType
                    || parameters[6].ParameterType != typeof(Func<Vector3?>))
                {
                    ChoiceFail("the methods that make a line and a row have changed");
                    return;
                }
                if (toggles.Count == 0)
                {
                    return;   // the game's list is not built yet: looked at again in a second
                }
                for (int c = 0; c < Choices.Count; c++)
                {
                    Choice choice = Choices[c];
                    if (choice.HasRows)
                    {
                        continue;
                    }
                    var line = makeLine.Invoke(filters, new object[] { choice.CategoryKey }) as CityMapFilterCategory;
                    if (line == null || line.ToggleAll == null)
                    {
                        ChoiceFail("a line came back empty");
                        return;
                    }
                    line.ToggleAll.gameObject.SetActive(false);
                    Transform fold = line.transform.Find("Collapse");
                    if (fold != null)
                    {
                        fold.gameObject.SetActive(false);   // folding would write the line's name into the save
                    }
                    Transform list = line.transform.parent;
                    Transform before = list != null ? list.Find(Neighbourhoods) : null;
                    int at = before != null ? before.GetSiblingIndex() : -1;
                    if (at >= 0)
                    {
                        line.transform.SetSiblingIndex(at);
                    }
                    choice.Line = line;
                    var data = new CityMapFilterData { isAvailable = Guard.Asked(() => choice.Offered, false) };
                    for (int i = 0; i < choice.Rows.Count; i++)
                    {
                        ChoiceRow entry = choice.Rows[i];
                        int index = i;
                        string name = Pref + choice.Id + "_" + entry.Id;
                        object languageData = Activator.CreateInstance(parameters[4].ParameterType);
                        Func<Vector3?> focus = Guard.Asked(entry.Focus, null);
                        var row = create.Invoke(filters, new object[] { name, entry.Icon, data, line, languageData, null, focus }) as CityMapFilter;
                        if (row == null || row.Toggle == null)
                        {
                            ChoiceFail("a row came back empty");
                            return;
                        }
                        toggles.Remove(name);
                        entries.Remove(name);
                        row.SetUp(entry.TitleKey, Guard.Wrap<bool>(on => choice.Ticked(index, on)), entry.Icon, data, true, default, null, focus);
                        row.name = name;
                        entry.Row = row;
                        if (at >= 0)
                        {
                            row.transform.SetSiblingIndex(line.transform.GetSiblingIndex() + 1 + i);
                        }
                    }
                    choice.ShowTicks(choice.Picked);
                    line.UpdateToggleAllState();
                    Offer(choice);
                    Logger?.Info("Map places: the choice '" + choice.Id + "' added to the city map's filters with " + choice.Rows.Count + " row(s).");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ChoiceFail("an error (above)");
            }
        }

        private static void ChoiceFail(string why)
        {
            _choiceFailed = true;
            Logger?.Warn("Map places: the city map's filter list could not be given the mod's switches (" + why + "); they are left off the map.");
        }
    }
}
