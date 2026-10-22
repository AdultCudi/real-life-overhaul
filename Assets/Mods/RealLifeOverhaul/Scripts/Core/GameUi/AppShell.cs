using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The frame of a phone app, copied asleep from the game's MyEmployees app: a headline, the white box on the left, two
    /// tabs over a table with headers and the game's scroller on the right, without the scripts that make it MyEmployees.
    /// It has every part an agenda needs, and its BaTable can be derived from (docs/game-ui.md).
    /// </summary>
    public sealed class AppShell
    {
        public static IModLogger Logger;

        // What only draws or sounds stays on the copy.
        internal static readonly string[] Harmless =
        {
            "ButtonEffects", "ToggleClickSound", "Badge", "HoverEffect", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter"
        };

        public GameObject Root { get; private set; }
        public Component Header { get; private set; }
        public InfoBox Box { get; private set; }
        public Button[] Tabs { get; private set; }
        public TMP_Text[] TabLabels { get; private set; }
        public Transform TopButtons { get; private set; }
        public Transform List { get; private set; }
        public Transform Headers { get; private set; }
        public EnhancedScroller Scroller { get; private set; }
        public GameObject RowPrefab { get; private set; }
        /// <summary>The colour the game gives a selected row, read from its row.</summary>
        public Color RowSelected { get; private set; } = new Color(0.741f, 0.78f, 0.812f, 1f);
        /// <summary>The game's drop-down over MyEmployees' table, asleep and whole: what a table's filter copies. Null if the game lacks it.</summary>
        public GameObject FilterTemplate { get; private set; }
        /// <summary>The header cells are the game's buttons with their two arrows; without them no column sorts.</summary>
        public bool CanSort { get; private set; }
        /// <summary>Told which column's header was clicked (only one that sorts can be).</summary>
        public Action<int> HeaderClicked;

        // The header cells, read once: the cell, its button, its title and the two arrows of the order.
        private readonly Transform[] _headCells = new Transform[TableSpec.MaxColumns];
        private readonly Button[] _headButtons = new Button[TableSpec.MaxColumns];
        private readonly Transform[] _headTitles = new Transform[TableSpec.MaxColumns];
        private readonly GameObject[] _arrowsUp = new GameObject[TableSpec.MaxColumns], _arrowsDown = new GameObject[TableSpec.MaxColumns];
        private int _headCount;
        private TableSpec _columnsOf;   // the table whose columns the header cells show now

        /// <summary>The copy, asleep under <paramref name="dormant"/>. Null, with one line in Player.log, if the game's app is not built as expected.</summary>
        public static AppShell Make(FullMenu menu, Transform dormant, string name)
        {
            try
            {
                if (menu == null || menu.myEmployees == null)
                {
                    UiClone.Fallback("the game's MyEmployees app is not there to copy, so '" + name + "' is not added");
                    return null;
                }
                var shell = new AppShell();
                GameObject source = menu.myEmployees.gameObject;
                GameObject rowSource = RowOf(source);
                GameObject root = UiClone.Asleep(source, dormant, name);
                shell.Root = root;
                // The drop-down over the table keeps its scripts: it is the game's own control, whole (TakeFilter).
                Transform filter = root.transform.Find("Layout 30-70/Right/MassActionDropdown");
                UiClone.StripGameScripts(root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0 || (filter != null && script.transform.IsChildOf(filter)));

                Transform layout = UiClone.Need(root.transform, "Layout 30-70", name);
                Transform left = UiClone.Need(layout, "Left", name);
                Transform right = UiClone.Need(layout, "Right", name);
                Transform header = UiClone.Need(layout, "Header", name);
                Transform whiteBox = UiClone.Need(left, "WhiteBox", name);
                Transform tabs = UiClone.Need(right, "TableTabs", name);
                Transform list = UiClone.Need(right, "EmployeesList", name);
                Transform headers = UiClone.Need(list, "Headers", name);
                if (layout == null || left == null || right == null || header == null || whiteBox == null || tabs == null || list == null || headers == null || rowSource == null)
                {
                    if (rowSource == null)
                    {
                        UiClone.Fallback(name + ": the game's table row was not found");
                    }
                    UnityEngine.Object.DestroyImmediate(root);
                    return null;
                }
                shell.Scroller = list.GetComponentInChildren<EnhancedScroller>(true);
                shell.Box = InfoBox.From(whiteBox, UiClone.Dormant(root.transform, "Templates"));
                if (shell.Scroller == null || shell.Box == null)
                {
                    UiClone.Fallback(name + ": the game's scroller or its white box is not built as expected");
                    UnityEngine.Object.DestroyImmediate(root);
                    return null;
                }

                shell.Box.SetAccent(LookRules.Accent(name));   // the app icon's colour, over each page's title
                shell.TakeFilter(filter, root.transform.Find("Templates"));

                // The headline over the left column: wide enough for a sentence, cut with dots past that.
                shell.Header = header;
                var headline = header.GetComponent<TMP_Text>();
                if (headline != null)
                {
                    var rect = (RectTransform)header;
                    rect.sizeDelta = new Vector2(1060f, rect.sizeDelta.y);
                    headline.textWrappingMode = TextWrappingModes.NoWrap;
                    headline.overflowMode = TextOverflowModes.Ellipsis;
                }

                // The top right buttons stand where MyEmployees has its mass action selector, level with the tabs.
                Transform topButtons = shell.Box.ButtonRow(right);
                if (topButtons != null)
                {
                    var rect = (RectTransform)topButtons;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                    rect.anchoredPosition = new Vector2(-127.5f, -224.1f);
                    rect.sizeDelta = new Vector2(760f, 80f);   // room for two buttons side by side
                    topButtons.name = "TopButtons";
                    shell.TopButtons = topButtons;
                }
                UiClone.KeepOnly(right, "TableTabs", "EmployeesList", "TopButtons");
                UiClone.KeepOnly(left, "WhiteBox");
                left.gameObject.SetActive(true);
                whiteBox.gameObject.SetActive(true);
                list.gameObject.SetActive(true);
                tabs.gameObject.SetActive(true);
                layout.gameObject.SetActive(true);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.transform.GetChild(i);
                    if (child != layout && child.name != "Templates")
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                // Two tabs, as the game has them: the first with the rounded left corner, the second with the right.
                shell.Tabs = new Button[2];
                shell.TabLabels = new TMP_Text[2];
                for (int i = 0; i < tabs.childCount && i < 2; i++)
                {
                    Transform tab = tabs.GetChild(i);
                    shell.Tabs[i] = UiClone.Rewire(tab, null);
                    Transform label = tab.Find("Label");
                    shell.TabLabels[i] = label != null ? label.GetComponent<TMP_Text>() : null;
                    var badge = tab.GetComponentInChildren<global::UI.Elements.Badge>(true);
                    if (badge != null)
                    {
                        badge.gameObject.SetActive(false);
                    }
                }
                for (int i = tabs.childCount - 1; i >= 2; i--)
                {
                    UnityEngine.Object.DestroyImmediate(tabs.GetChild(i).gameObject);
                }
                if (shell.Tabs[0] == null || shell.Tabs[1] == null || shell.TabLabels[0] == null || shell.TabLabels[1] == null)
                {
                    UiClone.Fallback(name + ": the game's two table tabs are not built as expected");
                    UnityEngine.Object.DestroyImmediate(root);
                    return null;
                }

                shell.List = list;
                shell.Headers = headers;
                shell.ReadHeaders();
                UiClone.KeepOnly(list, "Headers", "ListBackground", shell.Scroller.name);
                // Rows start at the list's left edge (MyEmployees leaves 127 for its tick boxes) and end 30 short of the right, where
                // the scroll bar is; the headers are 30 narrower than the list for the same reason.
                shell.Scroller.padding = new RectOffset(0, 30, 0, 0);

                shell.RowPrefab = UiClone.Asleep(rowSource, dormant, name + " Row");
                shell.ReadRowColours();
                UiClone.StripGameScripts(shell.RowPrefab, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);
                return shell;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("'" + name + "' could not be built from the game's MyEmployees app");
                return null;
            }
        }

        // The row MyEmployees fills its table with (BaTable.prefab), through the game contract, so that a change to those
        // classes costs this app its rows and not the mod its start.
        private static GameObject RowOf(GameObject myEmployees)
        {
            Type table = GameContract.EmployeeTable.Type;
            FieldInfo field = GameContract.EmployeeTableRow.Field;
            if (table == null || field == null)
            {
                return null;
            }
            MonoBehaviour[] scripts = myEmployees.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                if (scripts[i] == null || scripts[i].GetType() != table)
                {
                    continue;
                }
                var prefab = field.GetValue(scripts[i]) as Component;
                return prefab != null ? prefab.gameObject : null;
            }
            return null;
        }

        // A row script's colour field, looked up once per kind of row (BaTableCellView<T>; null for one without).
        private static readonly Dictionary<Type, FieldInfo> RowColourFields = new Dictionary<Type, FieldInfo>(4);

        private static FieldInfo RowColourOf(Type type)
        {
            if (!RowColourFields.TryGetValue(type, out FieldInfo field))
            {
                field = GameContract.RowChosenColour.Ok ? type.GetField(GameContract.RowChosenColour.Name, BindingFlags.Instance | BindingFlags.Public) : null;
                RowColourFields[type] = field;
            }
            return field;
        }

        private void ReadRowColours()
        {
            MonoBehaviour[] scripts = RowPrefab.GetComponents<MonoBehaviour>();
            for (int i = 0; i < scripts.Length; i++)
            {
                FieldInfo field = scripts[i] != null ? RowColourOf(scripts[i].GetType()) : null;
                if (field != null && field.FieldType == typeof(Color))
                {
                    RowSelected = (Color)field.GetValue(scripts[i]);
                    return;
                }
            }
        }

        // The game's drop-down goes among the templates, asleep: a table that filters copies it when first needed. Without
        // it tables have no filter.
        private void TakeFilter(Transform filter, Transform templates)
        {
            bool found = false;
            try
            {
                found = filter != null && templates != null && FilterDrop.Is(filter.gameObject);
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the game's drop-down class is not what it was
            }
            if (!found)
            {
                UiClone.Fallback("the drop-down over the game's MyEmployees table is not there to copy, so the tables have no filter");
                if (filter != null)
                {
                    UnityEngine.Object.DestroyImmediate(filter.gameObject);
                }
                return;
            }
            filter.SetParent(templates, false);
            filter.name = "Filter";
            FilterTemplate = filter.gameObject;
        }

        // The game's table makes a header sort by giving its Button a click (BaTable.Start) and shows the order with the
        // cell's ArrowUp or ArrowDown (BaTable.ChangeSortOrder). Cells stay as they are; the click is the mod's (TableModel).
        // The title is white and darkened by the button's tint, so every cell keeps its button: a column that does not sort
        // has it not interactable, the game's own look for that.
        private void ReadHeaders()
        {
            _headCount = 0;
            bool whole = true;
            for (int i = 0; i < Headers.childCount && _headCount < TableSpec.MaxColumns; i++)
            {
                Transform cell = Headers.GetChild(i);
                var element = cell.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout)
                {
                    continue;   // the background
                }
                int column = _headCount++;
                Transform up = cell.Find("ArrowUp"), down = cell.Find("ArrowDown");
                _headCells[column] = cell;
                _headTitles[column] = cell.Find("Title");
                _arrowsUp[column] = up != null ? up.gameObject : null;
                _arrowsDown[column] = down != null ? down.gameObject : null;
                _headButtons[column] = UiClone.Rewire(cell, () =>
                {
                    if (HeaderClicked != null)
                    {
                        HeaderClicked(column);
                    }
                });
                whole &= _headButtons[column] != null && up != null && down != null;
                cell.name = "Column" + column;   // no field of any row class: the game's own sort by reflection stays out of it
            }
            CanSort = whole && _headCount > 0;
            if (!CanSort)
            {
                UiClone.Fallback("the header cells of the game's table are no longer buttons with two arrows, so the tables' columns do not sort");
            }
        }

        /// <summary>The table's columns from a specification on the game's header cells, the button off where one does not sort; again for another table.</summary>
        public bool SetColumns(TableSpec spec)
        {
            var layout = Headers.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                UiClone.Fallback("the table's header row has no layout");
                return false;
            }
            if (ReferenceEquals(spec, _columnsOf))
            {
                return true;   // the same columns: the header cells are as they should be
            }
            _columnsOf = spec;
            RowLayout(layout);
            for (int i = 0; i < _headCount; i++)
            {
                Transform cell = _headCells[i];
                bool used = i < spec.Count;
                if (cell.gameObject.activeSelf != used)
                {
                    cell.gameObject.SetActive(used);
                }
                if (!used)
                {
                    continue;
                }
                TableColumn column = spec.Columns[i];
                if (_headTitles[i] != null)
                {
                    UiClone.Key(_headTitles[i], column.TitleKey);
                }
                Width(cell, column.Width, column.Flexible);
                var group = cell.GetComponent<HorizontalLayoutGroup>();
                if (group != null)
                {
                    group.childAlignment = column.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                }
                if (_headButtons[i] != null)
                {
                    _headButtons[i].interactable = CanSort && column.Sort != TableSort.None;
                }
            }
            if (_headCount < spec.Count)
            {
                UiClone.Fallback("the game's table has " + _headCount + " header cells, " + spec.Count + " were needed");
                _columnsOf = null;
                return false;
            }
            return true;
        }

        /// <summary>Columns that do not sort, from plain lists.</summary>
        public bool SetColumns(string[] names, string[] titleKeys, float[] widths, int flexible)
        {
            var spec = new TableSpec("Plain", names.Length > 0 ? names[0] : string.Empty);
            for (int i = 0; i < names.Length; i++)
            {
                spec.Column(titleKeys[i], widths[i], TableSort.None, i == flexible);
            }
            bool made = SetColumns(spec);
            ShowSort(-1, true);
            return made;
        }

        /// <summary>Shows the order as the game's table does: the column's arrow, up for ascending; -1 for none.</summary>
        public void ShowSort(int column, bool ascending)
        {
            for (int i = 0; i < _headCount; i++)
            {
                Arrow(_arrowsUp[i], CanSort && i == column && ascending);
                Arrow(_arrowsDown[i], CanSort && i == column && !ascending);
            }
        }

        private static void Arrow(GameObject arrow, bool shown)
        {
            if (arrow != null && arrow.activeSelf != shown)
            {
                arrow.SetActive(shown);
            }
        }

        /// <summary>The header row has what a specification asks for: its cells, and arrows where a column sorts.</summary>
        public bool HeadersFit(TableSpec spec)
        {
            return _headCount >= spec.Count && (CanSort || spec.Sortable == 0);
        }

        /// <summary>The game's employee row made plain text cells, "Cell0", "Cell1"..., copies of its name; false, with one log line, if it is not as expected.</summary>
        public static bool PrepareRow(GameObject row, int cells, string app)
        {
            Transform t = row != null ? row.transform : null;
            var layout = row != null ? row.GetComponent<HorizontalLayoutGroup>() : null;
            Transform first = t != null ? t.Find("EmployeeName") : null;
            if (layout == null || first == null || first.GetComponent<TMP_Text>() == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the " + app + " app is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName");
            first.name = "Cell0";
            for (int i = 1; i < cells; i++)
            {
                GameObject copy = UnityEngine.Object.Instantiate(first.gameObject, t, false);
                copy.name = "Cell" + i;
            }
            RowLayout(layout);
            for (int i = 0; i < cells; i++)
            {
                Transform cell = t.Find("Cell" + i);
                Width(cell, 200f, i == 0);
                OneLine(cell.GetComponent<TMP_Text>());
            }
            return true;
        }

        /// <summary>The layout a row and the header row share, so that a cell stands under its header.</summary>
        public static void RowLayout(HorizontalLayoutGroup layout)
        {
            layout.padding = new RectOffset(40, 30, layout.padding.top, layout.padding.bottom);
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
        }

        /// <summary>A cell's width in a row laid out by RowLayout.</summary>
        public static void Width(Transform cell, float width, bool flexible)
        {
            LayoutElement element = cell.GetComponent<LayoutElement>() ?? cell.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.minWidth = flexible ? 120f : width;
            element.preferredWidth = width;
            element.flexibleWidth = flexible ? 1f : 0f;
        }

        /// <summary>One line, cut with dots where it does not fit.</summary>
        public static void OneLine(TMP_Text text)
        {
            if (text != null)
            {
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>More tabs than two: middle ones are pictureless copies of the first (square corners). Call while asleep; false, with a log line, on failure.</summary>
        public bool SetTabCount(int count)
        {
            if (count <= 2 || Tabs == null || Tabs.Length != 2)
            {
                return count == 2;
            }
            Transform bar = Tabs[0].transform.parent;
            var tabs = new Button[count];
            var labels = new TMP_Text[count];
            tabs[0] = Tabs[0];
            labels[0] = TabLabels[0];
            tabs[count - 1] = Tabs[1];
            labels[count - 1] = TabLabels[1];
            for (int i = 1; i < count - 1; i++)
            {
                GameObject copy = UnityEngine.Object.Instantiate(Tabs[0].gameObject, bar, false);
                copy.name = "Tab" + i;
                copy.transform.SetSiblingIndex(i);
                var picture = copy.GetComponent<Image>();
                if (picture != null)
                {
                    picture.sprite = null;
                }
                tabs[i] = UiClone.Rewire(copy.transform, null);
                Transform label = copy.transform.Find("Label");
                labels[i] = label != null ? label.GetComponent<TMP_Text>() : null;
                if (tabs[i] == null || labels[i] == null)
                {
                    UiClone.Fallback("a copy of the game's table tab is not a tab, so the app cannot have " + count + " of them");
                    return false;
                }
            }
            Tabs = tabs;
            TabLabels = labels;
            return true;
        }

        /// <summary>Marks the tab showing, as MyEmployees.ChangeTab does: not clickable, its label black.</summary>
        public void ShowTab(int index)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            for (int i = 0; i < Tabs.Length; i++)
            {
                Tabs[i].interactable = i != index;
                if (references != null)
                {
                    TabLabels[i].color = i == index ? references.colors.black : references.colors.darkGrey;
                }
            }
        }
    }
}
