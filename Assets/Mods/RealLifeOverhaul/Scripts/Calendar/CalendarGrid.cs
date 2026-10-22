using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// The Month tab: seven columns, Monday to Sunday, where the table stands on the Day tab. Each column is a heading
    /// for its weekday and a cell for each week of the month, every one of them a copy of the game's own table row
    /// (MyEmployees' row, with only its background, its click and its first text): the game's grey rounded row, its hover
    /// and its click sound, its pale colour when chosen. A cell says its day of the month, today in the game's yellow,
    /// the money that day brings in (green) and takes out (red), and how many other things are on it. A click on a cell
    /// chooses its day.
    ///
    /// Every cell is made once, asleep, when the app is built (seven headings and six weeks of cells); showing a month only
    /// writes their words and colours and switches them on or off. Nothing runs per frame.
    /// </summary>
    public sealed class CalendarGrid
    {
        private const float HeadHeight = 64f;     // the weekday's heading
        private const float CellHeight = 148f;    // three lines of the smaller words below
        private const float FontSize = 26f;       // the game's table rows write 30: a column is a seventh of the table
        private const float Spacing = 10f;        // between two cells of a column
        private const float ColumnGap = 15f;      // between two columns: what the game's scroller leaves between rows
        private const float PastAlpha = 0.55f;    // a day gone by is written fainter

        private sealed class Cell
        {
            public GameObject Object;
            public Image Background;
            public TMP_Text Words;
            public CanvasGroup Group;
            public Button Button;
            public int Day;   // 0 for a heading or a cell outside the month
        }

        private GameObject _root;
        private readonly Cell[] _heads = new Cell[MonthViewRules.Columns];
        private readonly Cell[,] _cells = new Cell[MonthViewRules.MostWeeks, MonthViewRules.Columns];
        private Color _plainBackground = new Color(0.349f, 0.412f, 0.459f, 0.635f);   // the game's row (EmployeeCellView's Background), read from the copy below
        private Color _chosenBackground = new Color(0.741f, 0.78f, 0.812f, 1f);

        /// <summary>Told the day of a cell that was clicked.</summary>
        public Action<int> DayClicked;

        /// <summary>
        /// The grid beside the app's table (the same place and size), asleep until shown, made of copies of the game's row
        /// as AppShell hands it over, before the table's own script goes on it. Null, with one line in Player.log, if the row
        /// is not built as expected.
        /// </summary>
        public static CalendarGrid Make(GameObject row, Transform list, Color chosen)
        {
            if (row == null || list == null || list.parent == null || row.transform.Find("EmployeeName") == null || row.transform.Find("Background") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Calendar has no month grid");
                return null;
            }
            var grid = new CalendarGrid { _chosenBackground = chosen };
            var root = new GameObject("Month", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            var at = (RectTransform)list;
            rect.SetParent(list.parent, false);
            rect.SetSiblingIndex(list.GetSiblingIndex() + 1);
            rect.anchorMin = at.anchorMin;
            rect.anchorMax = at.anchorMax;
            rect.pivot = at.pivot;
            rect.anchoredPosition = at.anchoredPosition;
            rect.sizeDelta = new Vector2(at.sizeDelta.x - 30f, at.sizeDelta.y);   // the table's rows stop short of its scroll bar too
            var columns = root.GetComponent<HorizontalLayoutGroup>();
            columns.spacing = ColumnGap;
            columns.childControlWidth = columns.childControlHeight = true;
            columns.childForceExpandWidth = columns.childForceExpandHeight = true;
            columns.childAlignment = TextAnchor.UpperLeft;
            grid._root = root;

            for (int c = 0; c < MonthViewRules.Columns; c++)
            {
                var column = new GameObject("Weekday" + c, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                column.transform.SetParent(root.transform, false);
                var stack = column.GetComponent<VerticalLayoutGroup>();
                stack.spacing = Spacing;
                stack.childControlWidth = stack.childControlHeight = true;
                stack.childForceExpandWidth = true;
                stack.childForceExpandHeight = false;
                stack.childAlignment = TextAnchor.UpperCenter;
                column.GetComponent<LayoutElement>().flexibleWidth = 1f;
                grid._heads[c] = grid.Copy(row, column.transform, HeadHeight, null);
                if (grid._heads[c].Button != null)
                {
                    grid._heads[c].Button.interactable = false;   // a heading is no day
                }
                for (int w = 0; w < MonthViewRules.MostWeeks; w++)
                {
                    int week = w, weekday = c;
                    grid._cells[w, c] = grid.Copy(row, column.transform, CellHeight, () => grid.Clicked(week, weekday));
                }
            }
            return grid;
        }

        // A copy of the game's row with only its background, its click and its first text, which holds the words.
        private Cell Copy(GameObject row, Transform column, float height, Action click)
        {
            GameObject copy = UnityEngine.Object.Instantiate(row, column, false);
            copy.name = "Cell";
            UiClone.KeepOnly(copy.transform, "Background", "RaycastTarget", "EmployeeName");
            var cell = new Cell { Object = copy };
            Transform background = copy.transform.Find("Background");
            cell.Background = background != null ? background.GetComponent<Image>() : null;
            if (cell.Background != null)
            {
                _plainBackground = cell.Background.color;
            }
            Transform words = copy.transform.Find("EmployeeName");
            cell.Words = words != null ? words.GetComponent<TMP_Text>() : null;
            if (cell.Words != null)
            {
                cell.Words.fontSize = FontSize;
                cell.Words.richText = true;
                cell.Words.textWrappingMode = TextWrappingModes.Normal;
                cell.Words.overflowMode = TextOverflowModes.Ellipsis;
                cell.Words.alignment = TextAlignmentOptions.TopLeft;
                AppShell.Width(words, 100f, true);
            }
            var layout = copy.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(18, 18, 10, 8);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
            }
            LayoutElement size = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            size.flexibleHeight = 0f;
            cell.Group = copy.GetComponent<CanvasGroup>() ?? copy.AddComponent<CanvasGroup>();
            cell.Button = UiClone.Rewire(copy.transform, click);
            copy.SetActive(true);
            return cell;
        }

        public bool Showing => _root != null && _root.activeSelf;

        public void Show(bool shown)
        {
            if (_root != null && _root.activeSelf != shown)
            {
                _root.SetActive(shown);
            }
        }

        private void Clicked(int week, int weekday)
        {
            int day = _cells[week, weekday].Day;
            if (day > 0 && DayClicked != null)
            {
                DayClicked(day);
            }
        }

        // ---------------------------------------------------------------- writing it

        /// <summary>
        /// Writes the month: the weekdays' headings, and each cell of its weeks with its day, the money and how many other
        /// things the list has on it; the day chosen as the game draws the chosen row of a table, today in its yellow.
        /// </summary>
        public void Fill(int firstDay, int length, int today, int chosenDay, List<CalendarEntry> items)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            for (int c = 0; c < MonthViewRules.Columns; c++)
            {
                Write(_heads[c], 0, "<b>" + CalendarText.WeekdayOfColumn(c) + "</b>", false, 1f, references);
            }
            int weeks = MonthViewRules.Weeks(firstDay, length);
            for (int w = 0; w < MonthViewRules.MostWeeks; w++)
            {
                for (int c = 0; c < MonthViewRules.Columns; c++)
                {
                    Cell cell = _cells[w, c];
                    if (w >= weeks)
                    {
                        cell.Day = 0;
                        if (cell.Object.activeSelf)
                        {
                            cell.Object.SetActive(false);
                        }
                        continue;
                    }
                    if (!cell.Object.activeSelf)
                    {
                        cell.Object.SetActive(true);
                    }
                    int day = MonthViewRules.DayAt(firstDay, length, w, c);
                    if (day <= 0)
                    {
                        Write(cell, 0, string.Empty, false, 0f, references);   // a cell before or after the month keeps its place, unseen
                        continue;
                    }
                    bool chosen = day == chosenDay;
                    Write(cell, day, Words(day, firstDay, today, chosen, MonthViewRules.Tally(items, day), references), chosen, day < today ? PastAlpha : 1f, references);
                }
            }
        }

        // "12  Today" over "+$1.2K  $340" over "3 planned": the day of the month, the money with a known amount, the rest.
        private static string Words(int day, int firstDay, int today, bool chosen, DayTally tally, GlobalReferences references)
        {
            string words = "<b>" + (day - firstDay + 1) + "</b>";
            if (day == today)
            {
                words += "  " + Tinted(CalendarText.T("common_today"), CellTone.Yellow, chosen, references);
            }
            string money = string.Empty;
            if (tally.In > 0f)
            {
                money = Tinted("+" + CalendarText.Short(tally.In), CellTone.Green, chosen, references);
            }
            if (tally.Out > 0f)
            {
                money += (money.Length > 0 ? "  " : string.Empty) + Tinted(CalendarText.Short(tally.Out), CellTone.Red, chosen, references);
            }
            if (money.Length == 0 && tally.Money > 0)
            {
                money = Tinted(CalendarText.T("reallife_calendar_cell_money", "count", tally.Money.ToString()), CellTone.Green, chosen, references);
            }
            words += "\n" + money;
            int other = tally.Time + tally.City;
            if (other > 0)
            {
                words += "\n" + CalendarText.T("reallife_calendar_cell_other", "count", other.ToString());
            }
            return words;
        }

        // Words in one of the mod's tones: the game's own yellow, red and green (GlobalReferences.colors), as its tables write
        // them; on the chosen (pale) cell its yellow is orange, as the game's tables have it (BaTableCellView.VisualizeSelected).
        private static string Tinted(string words, CellTone tone, bool chosen, GlobalReferences references)
        {
            if (tone == CellTone.Plain || references == null)
            {
                return words;
            }
            Color colour = tone == CellTone.Red ? references.colors.red : tone == CellTone.Green ? references.colors.green : chosen ? references.colors.orange : references.colors.yellow;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">" + words + "</color>";
        }

        private void Write(Cell cell, int day, string words, bool chosen, float alpha, GlobalReferences references)
        {
            cell.Day = day;
            if (cell.Words != null)
            {
                cell.Words.text = words ?? string.Empty;
                if (references != null)
                {
                    cell.Words.color = chosen ? (Color)references.colors.midnight : (Color)references.colors.white;
                }
            }
            if (cell.Background != null)
            {
                cell.Background.color = chosen ? _chosenBackground : _plainBackground;
            }
            if (cell.Group != null)
            {
                cell.Group.alpha = alpha;
                cell.Group.blocksRaycasts = cell.Group.interactable = alpha > 0f;
            }
        }
    }
}
