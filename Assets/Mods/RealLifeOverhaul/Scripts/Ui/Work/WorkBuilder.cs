using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Builds a work app's page from its parts: the buttons with what each does, the chips, the rows with what each opens. Every button is drawn
    /// from a WorkDo: one that cannot work now is drawn disabled and its reason is said (never a dead button), so the checks can walk every
    /// action of every state and know each one acts or says why not. Game-free.
    /// </summary>
    public sealed class WorkBuilder
    {
        public readonly WorkPage Page = new WorkPage();
        private readonly List<UiActionModel> _buttons = new List<UiActionModel>(8);
        private readonly List<WorkDo> _acts = new List<WorkDo>(8);
        private readonly List<string> _whys = new List<string>(2);
        public readonly UtilitiesWords W;

        public WorkBuilder(UtilitiesWords words)
        {
            W = words;
        }

        public UiAppModel M => Page.Model;

        /// <summary>How many buttons the page has so far: the place the next one takes.</summary>
        public int Count => _buttons.Count;

        /// <summary>The model of a button already added, to draw it in a place of the page's own.</summary>
        public UiActionModel ModelAt(int i) => _buttons[i];

        /// <summary>The reasons the disabled buttons gave, said once under the page's own buttons.</summary>
        public string Why => _whys.Count == 0 ? null : string.Join(" ", _whys);

        /// <summary>A button: its label, kind, glyph, key and what it does; returns its place. With a reason in the action it is drawn disabled and the reason said.</summary>
        public int Button(string label, UiButtonKind kind, UiIcon icon, UiAction key, WorkDo act)
        {
            bool off = act.Why != null || !act.Any;
            _buttons.Add(new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = UiAppWindow.ActionCommand,
                Arg = _buttons.Count, Disabled = off });
            _acts.Add(act);
            if (act.Why != null && !_whys.Contains(act.Why))
            {
                _whys.Add(act.Why);
            }
            return _buttons.Count - 1;
        }

        /// <summary>A button of a detail or a page from its act; the first of a row's is the main one (F) and the second P.</summary>
        public int Act(WorkAct a, int rowId, int place)
        {
            return Button(a.Label, a.Style, a.Icon, place == 0 && a.Style == UiButtonKind.Primary ? UiAction.Alt : place <= 1 && a.Style == UiButtonKind.Secondary ? UiAction.Option
                : UiAction.Count, Do(a.Kind, a.Arg != 0 ? a.Arg : rowId, a.Why, a.Opens));
        }

        /// <summary>A filter chip: a choice of the page, chosen in the accent.</summary>
        public void Chip(string label, bool chosen, WorkDo act)
        {
            _buttons.Add(new UiActionModel { Label = label, Kind = UiButtonKind.Chip, Icon = chosen ? UiIcon.Check : UiIcon.None,
                Key = UiAction.Count, Command = UiAppWindow.ActionCommand, Arg = _buttons.Count });
            _acts.Add(act);
        }

        public WorkPage Done()
        {
            M.Actions = _buttons.ToArray();
            Page.Acts = _acts.ToArray();
            Page.Why = Why;
            return Page;
        }

        // ------------------------------------------------------------------------------------------------- small helpers

        public static WorkDo Do(int kind, int arg = 0, string why = null, bool opens = false) => new WorkDo { Kind = kind, Arg = arg, Why = why,
            Opens = opens };

        /// <summary>The rows a player with no job could take, best pay first: by each row's own figure (<see cref="WorkRow.Order"/>), never by the text of a cell.</summary>
        public static void BestPaid(List<WorkRow> rows, int mask)
        {
            rows.RemoveAll(r => (r.Mask & mask) == 0);
            rows.Sort((a, b) => b.Order.CompareTo(a.Order));
        }

        public static string Percent(int value) => (value >= 0 ? "+" : string.Empty) + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

        public static string Number(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        public static UiAppRow RowOf(UiIcon icon, UiRole tint, string[] cells, string status = null, UiRole tone = UiRole.Ok, int grade = -1)
        {
            return new UiAppRow { Icon = icon, Tint = tint, Cells = cells, Status = status, StatusTone = tone, Grade = grade };
        }

        public static UiDataColumn Col(string title, UiCellKind kind, float width = 0f, bool graded = false) => new UiDataColumn { Title = title, Kind = kind, Width = width, Graded = graded };
    }
}
