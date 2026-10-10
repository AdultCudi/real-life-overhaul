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
                UiClone.StripGameScripts(root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);

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
                    rect.sizeDelta = new Vector2(900f, 80f);
                    var row = topButtons.GetComponent<HorizontalLayoutGroup>();
                    if (row != null)
                    {
                        row.childAlignment = TextAnchor.MiddleRight;
                        row.childForceExpandWidth = false;
                    }
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

        /// <summary>
        /// Makes the table's columns: the header cells are the game's own, the first so-many of them, renamed (so the
        /// table does not take them for sortable columns) and given their words and widths. One column may be flexible.
        /// </summary>
        public bool SetColumns(string[] names, string[] titleKeys, float[] widths, int flexible)
        {
            var layout = Headers.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                UiClone.Fallback("the table's header row has no layout");
                return false;
            }
            RowLayout(layout);
            int made = 0;
            for (int i = 0; i < Headers.childCount; i++)
            {
                Transform cell = Headers.GetChild(i);
                var element = cell.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout)
                {
                    continue;   // the background
                }
                if (made >= names.Length)
                {
                    cell.gameObject.SetActive(false);
                    continue;
                }
                cell.name = names[made];
                // A header that sorts nothing is not a button. The hover effect goes first: it may require the button.
                foreach (MonoBehaviour script in cell.GetComponents<MonoBehaviour>())
                {
                    if (script != null && script.GetType().Name == "ButtonEffects")
                    {
                        UnityEngine.Object.DestroyImmediate(script);
                    }
                }
                var button = cell.GetComponent<Button>();
                if (button != null)
                {
                    UnityEngine.Object.DestroyImmediate(button);
                }
                Transform title = cell.Find("Title");
                if (title != null)
                {
                    UiClone.Key(title, titleKeys[made]);
                }
                Width(cell, widths[made], made == flexible);
                made++;
            }
            if (made < names.Length)
            {
                UiClone.Fallback("the game's table has " + made + " header cells, " + names.Length + " were needed");
                return false;
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
