using System;
using System.Collections.Generic;
using System.Globalization;
using Buildings.BuildingTypes.Shared;
using EnhancedUI.EnhancedScroller;
using Entities;
using Extensions;
using Localizor.LanguageChangeEvent;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A rival's staff (the game's RivalEmployeesUi, opened from BizMan) as a screen of the kit: the same squad table as My
    /// Employees, read only (a face, the skill graded, the hours they look for, whether talks are under way), the game's
    /// title over it, and Negotiate for the one chosen, which presses that row's own button in the game's table so the
    /// game's diploma check and its poaching run as a click runs them. Back and the close shut the game's window through
    /// its own Toggle. Cost: a look at the table's count and talks four times a second while it shows; a layout only when
    /// they change.
    /// </summary>
    public sealed class RivalStaffScreen : IUiScreen
    {
        private const float Every = 0.25f;   // seconds between looks at the game's table

        private readonly RivalEmployeesUi _ui;
        private readonly UiStaffModel _m = new UiStaffModel();
        private int _chosen;
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private string _offerOf;           // whose offer is held: the game makes it afresh from their record, once a choice
        private RivalChosenView _offer;
        private readonly Dictionary<string, Ask> _asks = new Dictionary<string, Ask>(16);   // each listed person's age and ask, made once a window

        public RivalStaffScreen(RivalEmployeesUi ui)
        {
            _ui = ui;
        }

        public string Id => EmployeesKit.RivalId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            UiStaff.Lay(s, _m, frame, dark, keys);
            _m.Table.Reveal = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            List<RivalEmployeeModel> data = Data();
            switch (command)
            {
                case UiDataTable.First + UiDataTable.Row:
                    if (data == null || arg < 0 || arg >= data.Count)
                    {
                        return false;
                    }
                    _chosen = arg;
                    return true;
                case UiStaff.PreviewCommand:
                    return arg == 0 && Negotiate(data);
                default:
                    return false;
            }
        }

        public void Focused(int command, int arg)
        {
            if (command == UiDataTable.First + UiDataTable.Row && arg != _chosen)
            {
                _chosen = arg;   // the cursor chooses, as a squad screen follows it: Negotiate names the one under it
                _dirty = true;
            }
        }

        public bool Back()
        {
            _ui.Toggle(false);   // the game's own close (its close button and its dimmed backdrop call the same)
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;   // Negotiate carries F on its own button

        public void Reveal(int list, int index)
        {
            _m.Table.Reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _m.Table.Offset = Math.Max(0f, _m.Table.Offset - notches * UiTokens.S16);
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- the game's state

        private List<RivalEmployeeModel> Data()
        {
            var table = GameContract.RivalStaffTable.Field?.GetValue(_ui) as RivalEmployeeScrollerController;
            return table != null ? table.data : null;
        }

        // What would change the screen: the rows the game loaded (a frame after it opened) and the talks under way.
        private long Signature()
        {
            List<RivalEmployeeModel> data = Data();
            long sig = data != null ? data.Count : -1;
            for (int i = 0; data != null && i < data.Count; i++)
            {
                AiBusinessEmployeeData e = data[i] != null ? data[i].EmployeeData : null;
                sig = sig * 31 + (e != null ? (e.HasActiveSalaryNegotiation ? 2 : 1) + (e.isPoached ? 4 : 0) : 0);
            }
            return sig * 31 + _chosen;
        }

        private void Build()
        {
            List<RivalEmployeeModel> data = Data() ?? new List<RivalEmployeeModel>();
            _chosen = data.Count == 0 ? 0 : Math.Max(0, Math.Min(_chosen, data.Count - 1));
            var title = GameContract.RivalStaffTitle.Field?.GetValue(_ui) as TextLocalizationComponent;
            string titled = title != null ? UiWidgets.Text(title.TextContainer) : null;
            var rows = new RivalRow[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                RivalEmployeeModel row = data[i];
                AiBusinessEmployeeData e = row != null ? row.EmployeeData : null;
                rows[i] = new RivalRow
                {
                    Name = row != null ? row.EmployeeName : string.Empty, Skill = row != null ? row.PrimarySkill.Item1 : string.Empty,
                    Level = row != null ? Mathf.RoundToInt(row.PrimarySkill.Item2) : 0, Hours = e != null ? e.GetExpectedHoursPerWeek() : 0,
                    Talking = e != null && e.HasActiveSalaryNegotiation, Poached = e != null && e.isPoached, SkillKey = e != null ? e.primarySkillName : null
                };
                Offered(e, ref rows[i]);
            }
            RivalChosenView offer = data.Count > 0 ? Offer(data[_chosen]) : null;
            StaffViews.Rival(_m, UiText.Words, string.IsNullOrEmpty(titled) ? UiText.T("smartphone_rivals") : titled, rows, _chosen, offer);
        }

        // A row's age and the wage they would ask at the city's prices, from the person the game would offer, made once each.
        private void Offered(AiBusinessEmployeeData data, ref RivalRow row)
        {
            if (data == null)
            {
                return;
            }
            if (!_asks.TryGetValue(data.id, out Ask ask))
            {
                EmployeeInstance e = data.GetEmployeeInstance();
                string face = FaceSources.Key(row.Name, e);
                FaceSources.Employee(row.Name, face, e);   // made from a name, a sex and an age: painted from those, since there is no look to draw
                ask = new Ask
                {
                    Face = face,
                    Age = e != null && e.characterData != null ? TimeHelper.GetYearsByDays(e.characterData.ageInDays) : 0,
                    Wage = e != null ? PaidWage.NowMoney(e.hourlyWage, e.id).ToCurrencyFormat() : null
                };
                _asks[data.id] = ask;
            }
            row.Face = ask.Face;
            row.Age = ask.Age;
            row.Wage = ask.Wage;
        }

        private struct Ask
        {
            public string Face;
            public int Age;
            public string Wage;
        }

        // The chosen one as the game would offer them: the game makes the person it negotiates with from its own record
        // (AiBusinessEmployeeData.GetEmployeeInstance, seeded by their id, so the same each time), read once a choice, never
        // added anywhere: their wants and demands, as a candidate's.
        private RivalChosenView Offer(RivalEmployeeModel row)
        {
            AiBusinessEmployeeData data = row != null ? row.EmployeeData : null;
            if (data == null)
            {
                return null;
            }
            if (data.id == _offerOf && _offer != null)
            {
                return _offer;
            }
            EmployeeInstance e = data.GetEmployeeInstance();
            _offerOf = data.id;
            _offer = new RivalChosenView();
            if (e != null)
            {
                _offer.Demands = EmployeesScreen.DemandsOf(e, true, null);
            }
            return _offer;
        }

        // The chosen row's own Negotiate in the game's table: its cell brought into the game's (unseen) view and pressed, so
        // the game asks for its diploma, refuses one too soon and opens the talks in Contacts as a click does.
        private bool Negotiate(List<RivalEmployeeModel> data)
        {
            var table = GameContract.RivalStaffTable.Field?.GetValue(_ui) as RivalEmployeeScrollerController;
            if (table == null || table.scroller == null || data == null || _chosen < 0 || _chosen >= data.Count)
            {
                return Done(false, "negotiate: no one chosen");
            }
            EnhancedScroller scroller = table.scroller;
            scroller.JumpToDataIndex(_chosen);
            var cell = scroller.GetCellViewAtDataIndex(_chosen) as RivalEmployeeCellView;
            var button = cell != null ? GameContract.RivalStaffNegotiate.Field?.GetValue(cell) as Button : null;
            return Done(UiWidgets.Press(button), "negotiate with row " + _chosen.ToString(CultureInfo.InvariantCulture) + ": handed to the game");
        }

        // One line a press in Player.log, so a report shows each button reaching the game.
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("A rival's staff (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }
    }
}
