using System.Collections.Generic;
using BaTable;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One row of the Finance app's table: four cells, or one line that only says something.</summary>
    public sealed class FinanceRow
    {
        public string Id;
        public string A, B, C, D;
        public bool Info;       // a line of words: no background, not to be selected
        public bool Pick;       // a click on it means something (a lender)
        public int Index;       // what it stands for in its tab's list
        public int Tone;        // of the last cell: 0 plain, 1 the game's yellow, 2 the game's red
    }

    /// <summary>
    /// The Finance app's table is the game's own (BaTable), as the Planner's is: the game's scroller makes only the
    /// rows in view and reuses them. The header names are not fields of FinanceRow, so no column sorts.
    /// </summary>
    public sealed class FinanceTable : BaTable<FinanceCellView, FinanceRow>
    {
        private bool _started;

        public override void Start()
        {
            base.Start();
            _started = true;
            scroller.ReloadData();
        }

        public override float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            return 100f;   // what every table of the game's phone uses
        }

        protected override string GetDataId(FinanceRow model)
        {
            return model.Id;
        }

        /// <summary>Shows these rows. The first time the app opens this runs before the scroller has woken up: the rows are kept and Start shows them.</summary>
        public void Show(List<FinanceRow> rows, string selectedId)
        {
            data = rows;
            SelectRow(null, selectedId);
            if (_started && scroller != null)
            {
                scroller.ReloadData();
            }
        }

        public void ClearSelection()
        {
            SelectRow(null, null);
        }
    }

    /// <summary>
    /// A row: the game's MyEmployees row with four of its cells left (FinanceApp.PrepareRow), written from a
    /// FinanceRow. The game colours the selected row and leaves its own red and yellow alone
    /// (BaTableCellView.VisualizeSelected), so the last cell's emphasis is written in exactly those colours.
    /// </summary>
    public sealed class FinanceCellView : BaTableCellView<FinanceRow>
    {
        public static Color SelectedColour = new Color(0.741f, 0.78f, 0.812f, 1f);

        private TMP_Text _a, _b, _c, _d;
        private FinanceRow _row;
        private bool _bound;

        protected override void Awake()
        {
            Bind();
            base.Awake();
        }

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
            _a = Text("A");
            _c = Text("C");
            _d = Text("D");
            Transform b = transform.Find("B");
            Transform label = b != null ? b.Find("Task") : null;
            _b = label != null ? label.GetComponent<TMP_Text>() : null;
        }

        private TMP_Text Text(string cell)
        {
            Transform found = transform.Find(cell);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        public override void SetData(FinanceRow row)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);   // a new row is a copy of a template that sleeps
            }
            Bind();
            _row = row;
            Write(_a, row.A);
            Write(_b, row.B);
            Write(_c, row.C);
            Write(_d, row.D);
            Look(false);
        }

        private static void Write(TMP_Text text, string words)
        {
            if (text != null)
            {
                text.text = words ?? string.Empty;
            }
        }

        public override void ResetVisuals()
        {
            Look(false);
        }

        public override void VisualizeSelected(bool selected)
        {
            if (_row == null || !_row.Pick || backgroundImage == null)
            {
                Look(false);   // only a lender is something to select
                return;
            }
            base.VisualizeSelected(selected);
            Look(selected);
        }

        private void Look(bool selected)
        {
            if (_row == null)
            {
                return;
            }
            if (backgroundImage != null && _row.Info)
            {
                Color clear = defaultBackgroundColor;
                clear.a = 0f;
                backgroundImage.color = clear;
            }
            else if (backgroundImage != null && !selected)
            {
                backgroundImage.color = defaultBackgroundColor;
            }
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null)
            {
                return;
            }
            Color plain = selected ? (Color)references.colors.midnight : (Color)references.colors.white;
            Colour(_a, plain);
            Colour(_b, plain);
            Colour(_c, plain);
            Colour(_d, _row.Tone == 2 ? (Color)references.colors.red : _row.Tone == 1 ? (Color)(selected ? references.colors.orange : references.colors.yellow) : plain);
        }

        private static void Colour(TMP_Text text, Color colour)
        {
            if (text != null)
            {
                text.color = colour;
            }
        }
    }
}
