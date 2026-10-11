using System;
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
    /// The frame of a phone app, copied from the game's MyEmployees app (FullMenu.myEmployees, "Layout 30-70"): a
    /// headline, on the left the white box that app shows an employee in, on the right two tabs over a table with
    /// column headers, the game's virtual scroller and its scroll bar. The copy is made asleep, the scripts that make
    /// it MyEmployees are taken off, what the new app has no use for is destroyed, and the rest is handed out by name.
    /// Rows come from the same app: its table's row prefab, copied asleep (RowPrefab).
    ///
    /// Why this app: it is the one with every part an agenda needs (tabs, a table with headers, a detail box with
    /// buttons, switches and a previous/next selector), and its table is the game's own BaTable, which any class can
    /// derive from (see docs/game-ui.md).
    /// </summary>
    public sealed class AppShell
    {
        public static IModLogger Logger;

        // What only draws or sounds stays on the copy; every other script of the game's is taken off.
        private static readonly string[] Harmless =
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
        /// <summary>The colour the game gives the row that is selected, read from its row.</summary>
        public Color RowSelected { get; private set; } = new Color(0.741f, 0.78f, 0.812f, 1f);
        /// <summary>The game's drop-down that stands over MyEmployees' table, asleep and whole: what a table's filter is a copy of. Null if the game no longer has it.</summary>
        public GameObject FilterTemplate { get; private set; }
        /// <summary>Whether the header cells are the game's buttons with their two arrows: without them no column sorts.</summary>
        public bool CanSort { get; private set; }
        /// <summary>Told which column's header was clicked (only the header of a column that sorts can be).</summary>
        public Action<int> HeaderClicked;

        // The header cells, read once: the cell, its button, its title and the two arrows the game shows the order with.
        private readonly Transform[] _headCells = new Transform[TableSpec.MaxColumns];
        private readonly Button[] _headButtons = new Button[TableSpec.MaxColumns];
        private readonly Transform[] _headTitles = new Transform[TableSpec.MaxColumns];
        private readonly GameObject[] _arrowsUp = new GameObject[TableSpec.MaxColumns], _arrowsDown = new GameObject[TableSpec.MaxColumns];
        private int _headCount;
        private TableSpec _columnsOf;   // the table whose columns the header cells show now

        /// <summary>The copy, asleep under 'dormant'. Null, with one line in Player.log, if the game's app is not built as expected.</summary>
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
                // The drop-down over the table keeps its scripts: it is the game's own control, whole (see TakeFilter).
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

                shell.TakeFilter(filter, root.transform.Find("Templates"));

                // The headline over the left column: wide enough for a sentence, cut with dots if it is longer still.
                shell.Header = header;
                var headline = header.GetComponent<TMP_Text>();
                if (headline != null)
                {
                    var rect = (RectTransform)header;
                    rect.sizeDelta = new Vector2(1060f, rect.sizeDelta.y);
                    headline.textWrappingMode = TextWrappingModes.NoWrap;
                    headline.overflowMode = TextOverflowModes.Ellipsis;
                }

                // The buttons at the top right stand where MyEmployees has its "mass action" selector, level with the tabs.
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

                // Two tabs, as the game has them: the first with the rounded left corner, the second with the right one.
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
                // Rows start at the list's left edge (MyEmployees leaves 127 for its tick boxes) and end 30 short of
                // the right one, where the scroll bar is: the headers are 30 narrower than the list for the same reason.
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

        // The row MyEmployees fills its table with: the "prefab" of the table's controller (BaTable.prefab, public).
        // Read by name, so that a change to those classes costs this app its rows and not the mod its start.
        private static GameObject RowOf(GameObject myEmployees)
        {
            MonoBehaviour[] scripts = myEmployees.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                if (scripts[i] == null || scripts[i].GetType().Name != "EmployeeScrollerController")
                {
                    continue;
                }
                FieldInfo field = scripts[i].GetType().GetField("prefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                var prefab = field != null ? field.GetValue(scripts[i]) as Component : null;
                return prefab != null ? prefab.gameObject : null;
            }
            return null;
        }

        private void ReadRowColours()
        {
            MonoBehaviour[] scripts = RowPrefab.GetComponents<MonoBehaviour>();
            for (int i = 0; i < scripts.Length; i++)
            {
                FieldInfo field = scripts[i] != null ? scripts[i].GetType().GetField("selectedBackgroundColor", BindingFlags.Instance | BindingFlags.Public) : null;
                if (field != null && field.FieldType == typeof(Color))
                {
                    RowSelected = (Color)field.GetValue(scripts[i]);
                    return;
                }
            }
        }

        // The game's drop-down goes among the templates, asleep, as the white box's parts do: a table that filters
        // copies it when it first needs one. Without it (a game update took it away) tables have no filter.
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

        // The game's table makes a header cell sort by giving its Button a click (BaTable.Start) and shows the order
        // by switching the cell's ArrowUp or ArrowDown on (BaTable.ChangeSortOrder). The cells are kept as they are,
        // button, hover effect and arrows; the click is the mod's (the order is worked out by GameUi.TableModel).
        // The title is white and gets its dark colour from the button's tint, so the button stays on every cell: a
        // column that does not sort has it switched to "not interactable", the game's own look for that.
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
                cell.name = "Column" + column;   // not a field of any row class: the game's own sort by reflection stays out of it
            }
            CanSort = whole && _headCount > 0;
            if (!CanSort)
            {
                UiClone.Fallback("the header cells of the game's table are no longer buttons with two arrows, so the tables' columns do not sort");
            }
        }

        /// <summary>
        /// Makes the table's columns from a specification: the first so-many header cells of the game's, each with
        /// its title and width; a column that sorts keeps its button, one that does not has it switched off. May be
        /// called again for another table of the same app (a tab, a page).
        /// </summary>
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
                return true;   // written again with the same columns: the header cells are as they should be
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

        /// <summary>Columns that do not sort, from plain lists (a table with no specification of its own).</summary>
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

        /// <summary>Shows the order as the game's table does: the arrow of that column, up for ascending; -1 for none.</summary>
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

        /// <summary>Whether the header row has what a specification asks for: its cells, and buttons with arrows where a column sorts.</summary>
        public bool HeadersFit(TableSpec spec)
        {
            return _headCount >= spec.Count && (CanSort || spec.Sortable == 0);
        }

        /// <summary>
        /// The game's employee row made into a row of so many plain cells of text. The row has a tick box and ten
        /// cells of different kinds; the first, the employee's name, is a text with a width of its own, so the cells
        /// are copies of it ("Cell0", "Cell1"...) and everything else but the background goes. Which of them show,
        /// and how wide, is the table's specification (DataCellView). False, with one line in Player.log, if the row
        /// is not built as expected.
        /// </summary>
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

        /// <summary>The layout a row and the header row share, so that a column's cell stands under its header.</summary>
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

        /// <summary>One line, cut with dots where it does not fit: what a table cell's text does with a long name.</summary>
        public static void OneLine(TMP_Text text)
        {
            if (text != null)
            {
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>
        /// More tabs than the two MyEmployees has. The first keeps the rounded left corner and the last the rounded
        /// right one; each tab between them is a copy of the first with no picture, which a sliced image draws as a
        /// plain white box: a tab with square corners. The bar lays them out itself. Call it while the app is still
        /// asleep (in Build), before the tabs are given their words and clicks. False, with one line in Player.log, if
        /// a copy is not a tab.
        /// </summary>
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

        /// <summary>Marks a tab as the one showing, as MyEmployees.ChangeTab does: it cannot be clicked and its label is black.</summary>
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
