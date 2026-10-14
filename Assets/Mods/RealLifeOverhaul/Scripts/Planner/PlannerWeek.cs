using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Planner
{
    /// <summary>
    /// The Week tab: seven columns, one for each day from today, where the table stands on the other tabs. Each column
    /// is a heading for its day and the day's entries in time order, every one of them a copy of the game's own table
    /// row (MyEmployees' row, as PlannerApp.PrepareRow leaves it, with only its background, its click and one text):
    /// the game's grey rounded row, its hover and its click sound, its pale colour when chosen. A click on a heading
    /// shows that day in the white box, a click on an entry shows the entry.
    ///
    /// Every row is made once, asleep, when the app is built (seven headings and nine rows a day); showing the tab or a
    /// change of its entries only writes their words and colours and switches them on or off. Nothing runs per frame.
    /// </summary>
    public sealed class PlannerWeek
    {
        public const int Shown = 8;               // entries a column shows; the row after them says how many more there are
        private const float HeadHeight = 100f;    // the height of a row of the game's phone tables (PlannerTable)
        private const float RowHeight = 112f;     // three lines of the smaller words below
        private const float FontSize = 26f;       // the game's table rows write 30: a column is a seventh of the table
        private const float Spacing = 12f;        // between two rows of a column
        private const float ColumnGap = 15f;      // between two columns: what the game's scroller leaves between rows

        private sealed class Chip
        {
            public GameObject Object;
            public Image Background;
            public TMP_Text Words;
            public PlannerEntry Entry;   // null for a heading, the row of how many more, or an empty day
        }

        private GameObject _root;
        private readonly Chip[] _heads = new Chip[WeekGrid.Days];
        private readonly Chip[,] _chips = new Chip[WeekGrid.Days, Shown + 1];
        private readonly int[] _days = new int[WeekGrid.Days];
        private Color _plainBackground = new Color(0.349f, 0.412f, 0.459f, 0.635f);   // the game's row (EmployeeCellView's Background), read from the copy below

        /// <summary>Told the day of a heading (or the row of how many more) that was clicked.</summary>
        public Action<int> DayClicked;
        /// <summary>Told the entry that was clicked.</summary>
        public Action<PlannerEntry> EntryClicked;

        /// <summary>
        /// The grid beside the app's table (the same place and size), asleep until shown, made of copies of a row of the
        /// table. Null, with one line in Player.log, if the row is not as PlannerApp.PrepareRow leaves it.
        /// </summary>
        public static PlannerWeek Make(GameObject row, Transform list)
        {
            if (row == null || list == null || list.parent == null || row.transform.Find("When") == null || row.transform.Find("Background") == null)
            {
                UiClone.Fallback("the Planner's row is not as expected, so the Planner has no Week tab");
                return null;
            }
            var week = new PlannerWeek();
            var root = new GameObject("Week", typeof(RectTransform), typeof(HorizontalLayoutGroup));
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
            week._root = root;

            for (int d = 0; d < WeekGrid.Days; d++)
            {
                var column = new GameObject("Day" + d, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                column.transform.SetParent(root.transform, false);
                var stack = column.GetComponent<VerticalLayoutGroup>();
                stack.spacing = Spacing;
                stack.childControlWidth = stack.childControlHeight = true;
                stack.childForceExpandWidth = true;
                stack.childForceExpandHeight = false;
                stack.childAlignment = TextAnchor.UpperCenter;
                column.GetComponent<LayoutElement>().flexibleWidth = 1f;
                int index = d;
                week._heads[d] = week.Copy(row, column.transform, HeadHeight, () => week.Day(index));
                for (int c = 0; c <= Shown; c++)
                {
                    int place = c;
                    week._chips[d, c] = week.Copy(row, column.transform, RowHeight, () => week.Clicked(index, place));
                }
            }
            return week;
        }

        // A copy of the table's row with only its background, its click and its first text, which holds the words.
        private Chip Copy(GameObject row, Transform column, float height, Action click)
        {
            GameObject copy = UnityEngine.Object.Instantiate(row, column, false);
            copy.name = "Row";
            UiClone.KeepOnly(copy.transform, "Background", "RaycastTarget", "When");
            var chip = new Chip { Object = copy };
            Transform background = copy.transform.Find("Background");
            chip.Background = background != null ? background.GetComponent<Image>() : null;
            if (chip.Background != null)
            {
                _plainBackground = chip.Background.color;
            }
            Transform words = copy.transform.Find("When");
            chip.Words = words != null ? words.GetComponent<TMP_Text>() : null;
            if (chip.Words != null)
            {
                chip.Words.fontSize = FontSize;
                chip.Words.richText = true;
                chip.Words.textWrappingMode = TextWrappingModes.Normal;
                chip.Words.overflowMode = TextOverflowModes.Ellipsis;
                chip.Words.alignment = TextAlignmentOptions.MidlineLeft;
                AppShell.Width(words, 100f, true);
            }
            var layout = copy.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(18, 18, 6, 6);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
            }
            LayoutElement size = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            size.flexibleHeight = 0f;
            UiClone.Rewire(copy.transform, click);
            copy.SetActive(false);
            return chip;
        }

        public bool Showing => _root != null && _root.activeSelf;

        public void Show(bool shown)
        {
            if (_root != null && _root.activeSelf != shown)
            {
                _root.SetActive(shown);
            }
        }

        private void Day(int column)
        {
            if (DayClicked != null)
            {
                DayClicked(_days[column]);
            }
        }

        private void Clicked(int column, int place)
        {
            PlannerEntry entry = _chips[column, place].Entry;
            if (entry != null && EntryClicked != null)
            {
                EntryClicked(entry);
            }
            else
            {
                Day(column);   // the row of how many more, or an empty day: the day itself
            }
        }

        // ---------------------------------------------------------------- writing it

        /// <summary>
        /// Writes the seven columns from the grid: each day's heading, its first entries and how many more. The day
        /// chosen and the entry chosen are drawn as the game draws the chosen row of a table.
        /// </summary>
        public void Fill(WeekGrid grid, int today, PlannerEntry chosen, int chosenDay)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            for (int d = 0; d < WeekGrid.Days; d++)
            {
                int day = grid.FirstDay + d;
                _days[d] = day;
                var entries = grid.Columns[d];
                string head = "<b>" + (day == today ? PlannerText.T("common_today") : day == today + 1 ? PlannerText.T("common_tomorrow") : PlannerText.Weekday(day)) + "</b>\n"
                              + PlannerText.T("reallife_planner_week_day", "day", day.ToString());
                Write(_heads[d], null, head, chosen == null && day == chosenDay, references);
                int shown = entries.Count > Shown + 1 ? Shown : entries.Count;
                for (int c = 0; c <= Shown; c++)
                {
                    Chip chip = _chips[d, c];
                    if (c < shown)
                    {
                        PlannerEntry e = entries[c];
                        Write(chip, e, Words(e, e == chosen, references), e == chosen, references);
                    }
                    else if (c == shown && entries.Count > shown)
                    {
                        Write(chip, null, PlannerText.T("reallife_planner_week_more", "count", (entries.Count - shown).ToString()), false, references);
                    }
                    else if (c == 0)
                    {
                        Write(chip, null, PlannerText.T("reallife_planner_week_none"), false, references);
                    }
                    else if (chip.Object.activeSelf)
                    {
                        chip.Object.SetActive(false);
                    }
                }
            }
        }

        // "WORK 09:00 Missed" over the title: the kind's short word in its tone, the hour, the state where it matters.
        private static string Words(PlannerEntry e, bool chosen, GlobalReferences references)
        {
            string tag = PlannerText.T(PlannerText.TagKey(e.Category)).ToUpperInvariant();
            string first = Tinted(tag, PlannerText.TagTone(e.Category), chosen, references) + "  " + PlannerText.When(e);
            string status = PlannerText.StatusKey(e);
            if (status != null)
            {
                bool trouble = e.State == PlannerState.Missed || (e.Clash && e.State != PlannerState.Done);
                bool soon = e.State == PlannerState.Now || e.Next;
                first += "  " + Tinted(PlannerText.T(status), trouble ? CellTone.Red : soon ? CellTone.Yellow : CellTone.Plain, chosen, references);
            }
            return first + "\n" + (e.Title ?? string.Empty);
        }

        // Words in one of the mod's tones: the game's own yellow, red and green (GlobalReferences.colors), as the tables write
        // them; on the chosen (pale) row its yellow is orange, as the game's tables have it (BaTableCellView.VisualizeSelected).
        private static string Tinted(string words, CellTone tone, bool chosen, GlobalReferences references)
        {
            if (tone == CellTone.Plain || references == null)
            {
                return words;
            }
            Color colour = tone == CellTone.Red ? references.colors.red : tone == CellTone.Green ? references.colors.green : chosen ? references.colors.orange : references.colors.yellow;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">" + words + "</color>";
        }

        private void Write(Chip chip, PlannerEntry entry, string words, bool chosen, GlobalReferences references)
        {
            chip.Entry = entry;
            if (!chip.Object.activeSelf)
            {
                chip.Object.SetActive(true);
            }
            if (chip.Words != null)
            {
                chip.Words.text = words ?? string.Empty;
                if (references != null)
                {
                    chip.Words.color = chosen ? (Color)references.colors.midnight : (Color)references.colors.white;
                }
            }
            if (chip.Background != null)
            {
                chip.Background.color = chosen ? PlannerCellView.SelectedColour : _plainBackground;
            }
        }
    }
}
