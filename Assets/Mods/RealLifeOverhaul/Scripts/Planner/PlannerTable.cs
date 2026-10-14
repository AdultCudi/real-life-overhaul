using System.Collections.Generic;
using BaTable;
using EnhancedUI.EnhancedScroller;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Planner
{
    /// <summary>One row of the Planner's table: an entry, a day's heading, or a line that only says something.</summary>
    public sealed class PlannerRow
    {
        public string Id;
        public RowKind Kind;
        public PlannerEntry Entry;
        public int Day;     // the day of its entry, or of a heading: a click on a heading shows that day
        public string When, What, Where, Status;
        public Sprite Icon;
        public int Tone;    // of the status: 0 plain, 1 the game's yellow (now, next), 2 the game's red (missed, a clash)
    }

    /// <summary>
    /// The Planner's table is the game's own (BaTable): the game's scroller makes only the rows in view and reuses
    /// them, selects a row on a click and colours it (BaTable.GetCellView, SelectRow). This class says how tall a row
    /// is and who a row is. The header names are not fields of PlannerRow, so the game's own sort takes no part: the
    /// order and the filter are worked out by GameUi.TableModel and the rows are handed over in that order (PlannerApp.ShowRows).
    /// </summary>
    public sealed class PlannerTable : BaTable<PlannerCellView, PlannerRow>
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("planner", "PlannerTable", typeof(PlannerTable));   // the game's calls of it (Core/Guard)

        private bool _started;

        public override void Start()
        {
            base.Start();      // gives the scroller its delegate and looks for sortable headers (there are none)
            _started = true;
            scroller.ReloadData();
        }

        public override float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            return 100f;       // what every table of the game's phone uses (BaseFilteredScrollerController)
        }

        protected override string GetDataId(PlannerRow model)
        {
            if (FrameGuard.Off)
            {
                return string.Empty;
            }
            try
            {
                return model.Id;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, string.Empty);
            }
        }

        /// <summary>
        /// Shows these rows, with the one of that id selected if it is among them. The first time the app opens this
        /// runs before the scroller has woken up: the rows are kept and Start shows them.
        /// </summary>
        public void Show(List<PlannerRow> rows, string selectedId)
        {
            data = rows;
            SelectRow(null, selectedId);   // remembers who is selected; the row colours itself when the scroller makes it
            if (_started && scroller != null)
            {
                scroller.ReloadData();
            }
        }

        public void ClearSelection()
        {
            SelectRow(null, null);
        }

        /// <summary>Brings the row of an id into the middle of what shows (after the order or the filter has changed).</summary>
        public void ScrollTo(string id)
        {
            for (int i = 0; id != null && _started && scroller != null && i < data.Count; i++)
            {
                if (data[i].Id == id)
                {
                    scroller.JumpToDataIndex(i, 0.5f, 0.5f);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// A row: the game's MyEmployees row with four of its cells left (see PlannerApp.PrepareRow for which), filled
    /// from a PlannerRow. The game colours the selected row's background and turns its texts dark, leaving its own red
    /// and yellow alone (BaTableCellView.VisualizeSelected), so the status is written in exactly those colours.
    /// </summary>
    public sealed class PlannerCellView : BaTableCellView<PlannerRow>
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("planner", "PlannerCellView", typeof(PlannerCellView));   // the game's calls of it (Core/Guard)

        public static Color SelectedColour = new Color(0.741f, 0.78f, 0.812f, 1f);

        private TMP_Text _when, _what, _where, _status;
        private Image _icon;
        private GameObject _iconHolder;
        private PlannerRow _row;
        private bool _bound;

        protected override void Awake()
        {
            Bind();
            base.Awake();   // remembers the row's own background colour
        }

        // Found by name each time a row is made: a copy does not have to carry references for this to work.
        private void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            Transform background = transform.Find("Background");
            backgroundImage = background != null ? background.GetComponent<Image>() : null;
            selectedBackgroundColor = SelectedColour;
            _when = Text("When");
            _where = Text("Where");
            _status = Text("Status");
            Transform what = transform.Find("What");
            if (what != null)
            {
                Transform label = what.Find("Task");
                _what = label != null ? label.GetComponent<TMP_Text>() : null;
                Transform holder = what.Find("WarningIcon");
                _iconHolder = holder != null ? holder.gameObject : null;
                _icon = holder != null ? holder.GetComponentInChildren<Image>(true) : null;
            }
        }

        private TMP_Text Text(string cell)
        {
            Transform found = transform.Find(cell);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        public override void SetData(PlannerRow row)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);   // a new row is a copy of a template that sleeps
                }
                Bind();
                _row = row;
                Write(_when, row.When);
                Write(_what, row.What);
                Write(_where, row.Where);
                Write(_status, row.Status);
                if (_what != null)
                {
                    _what.fontStyle = row.Kind == RowKind.DayHeader ? FontStyles.Bold : FontStyles.Normal;
                }
                bool picture = row.Icon != null && row.Kind == RowKind.Entry;
                if (_iconHolder != null && _iconHolder.activeSelf != picture)
                {
                    _iconHolder.SetActive(picture);
                }
                if (picture && _icon != null)
                {
                    _icon.sprite = row.Icon;
                }
                Look(false);
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private static void Write(TMP_Text text, string words)
        {
            if (text != null)
            {
                text.text = words ?? string.Empty;
            }
        }

        // The table calls this after it has reset a reused row's colours: the row's own look goes on last.
        public override void ResetVisuals()
        {
            try
            {
                Look(false);
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public override void VisualizeSelected(bool selected)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (_row == null || _row.Kind != RowKind.Entry || backgroundImage == null)
                {
                    Look(false);   // a heading is not something to select
                    return;
                }
                base.VisualizeSelected(selected);
                Look(selected);
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private void Look(bool selected)
        {
            if (_row == null)
            {
                return;
            }
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (backgroundImage != null && _row.Kind != RowKind.Entry)
            {
                Color clear = defaultBackgroundColor;
                clear.a = 0f;
                backgroundImage.color = clear;
            }
            else if (backgroundImage != null && !selected)
            {
                backgroundImage.color = defaultBackgroundColor;
            }
            if (references == null)
            {
                return;
            }
            if (_status != null)
            {
                // Selected, the game shows its yellow as orange (so it reads on the pale row) and keeps its red.
                _status.color = _row.Tone == 2 ? references.colors.red
                    : _row.Tone == 1 ? (selected ? references.colors.orange : references.colors.yellow)
                    : selected ? references.colors.midnight : references.colors.white;
            }
            if (_row.Kind != RowKind.Entry)
            {
                if (_what != null)
                {
                    _what.color = references.colors.white;
                }
                if (_when != null)
                {
                    _when.color = references.colors.white;
                }
            }
            if (_icon != null)
            {
                _icon.color = selected ? (Color)references.colors.midnight : Color.white;
            }
        }
    }
}
