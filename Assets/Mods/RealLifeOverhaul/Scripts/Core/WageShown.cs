using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BaTable;
using EnhancedUI.EnhancedScroller;
using Entities;
using Helpers;
using UI;
using UI.Load;
using UI.Smartphone;
using UI.Smartphone.Apps.BizMan.HRManagers;
using UI.Smartphone.Apps.BizMan.Schedule;
using UI.Smartphone.Apps.MyEmployees;
using UI.Smartphone.Apps.Shared;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own employee screens show what an employee is really paid (Core/PaidWage): the game charges its
    /// hourlyWage as it is, but the city's economy adds its prices to every wage each night and the Company app adds any
    /// agreed raise, so My Employees, the Contacts card, BizMan's HR manager plans and its schedule showed less than the
    /// player paid. The game's saved wage is never written (Going Public sends it to the other players, and the game's
    /// own rules read it): only what its screens draw. The tables' rows are written at the paid wage as the game's
    /// scroller asks for them (<see cref="WageTable{TCell, TModel}"/>, which stands in as the scroller's delegate and
    /// hands every call on to the game's table), and the few single labels are written again when the game writes them
    /// (<see cref="WageShownWatch"/>). Going Public: another player's employee shown here keeps the game's figure.
    /// COST: no Update; a one-second real-time timer (a reference comparison per table). A row costs one look-up when the
    /// game's scroller draws it.
    /// </summary>
    public sealed class WageShown : MonoBehaviour
    {
        internal static readonly FeatureGuard Shown = Guard.For(null, "WageShown", typeof(WageShown));

        public static IModLogger Logger;

        private readonly List<IWageTable> _tables = new List<IWageTable>(6);
        private readonly List<WageShownWatch> _watches = new List<WageShownWatch>(3);
        private FullMenu _menu;          // the city's phone the tables and watches were found on
        private bool _gaveBack;
        private static GameInstance _toldIn;
        private static readonly HashSet<string> Told = new HashSet<string>();   // negotiations whose price was told, this save

        private static GameInstance Save => SaveGameManager.Current;

        private void Awake()
        {
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GiveBack();
        }

        // Once a second of real time: the tables and labels found on a new city's phone, and a table whose scroller the
        // game handed back to itself taken again. Switched off by the guard, everything is given back as it was.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                try
                {
                    Step();
                }
                catch (Exception e)
                {
                    Guard.Fail(Shown, e, this);   // the timer goes on: five faults in a row switch it all off, and it is given back
                }
            }
        }

        private void Step()
        {
            if (Shown.Off)
            {
                GiveBack();
                return;
            }
            if (Save == null || LoadScene.isLoading)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            FullMenu menu = uis != null ? uis.fullMenu : null;
            if (menu == null)
            {
                return;
            }
            if (!ReferenceEquals(menu, _menu))
            {
                _menu = menu;
                Find(menu);
            }
            for (int i = 0; i < _tables.Count; i++)
            {
                _tables[i].Keep();
            }
            for (int i = 0; _gaveBack && i < _watches.Count; i++)
            {
                if (_watches[i] != null)
                {
                    _watches[i].enabled = true;   // back on after the guard let it go
                }
            }
            _gaveBack = false;
        }

        // Once per city: the game's tables of employees and the labels that name one wage. A screen the game lacks is left out.
        private void Find(FullMenu menu)
        {
            _tables.Clear();
            _watches.Clear();
            MyEmployees employees = menu.myEmployees;
            if (employees != null)
            {
                Add(new WageTable<EmployeeCellView, EmployeeModel>(employees.employeeScrollerController, m => m.employeeInstance, "My Employees' employees"));
                Add(new WageTable<CandidateCellView, CandidateModel>(employees.candidateScrollerController, m => m.employeeInstance, "My Employees' candidates"));
                Watch(WageShownWatch.On(employees, WageLabel.EmployeeBox));
            }
            if (menu.contactsApp != null)
            {
                Watch(WageShownWatch.On(menu.contactsApp, WageLabel.ContactCard));
            }
            HrManagerPlanUI plan = menu.bizMan != null && menu.bizMan.business != null ? menu.bizMan.business.hrHrManagerPlanUI : null;
            if (plan != null)
            {
                foreach (EmployeesScrollerController table in plan.GetComponentsInChildren<EmployeesScrollerController>(true))
                {
                    Add(new WageTable<UI.Smartphone.Apps.BizMan.HRManagers.EmployeeCellView, UI.Smartphone.Apps.BizMan.HrManagers.HrManagerEmployeeModel>(
                        table, m => ById(m.employeeId), "an HR manager plan's employees"));
                }
                Watch(WageShownWatch.On(plan, WageLabel.PlanAverage));
            }
            if (menu.schedule != null)
            {
                foreach (ScheduleEmployeeScrollerController table in menu.schedule.GetComponentsInChildren<ScheduleEmployeeScrollerController>(true))
                {
                    Add(new WageTable<ScheduleEmployeeCellView, ScheduleEmployeeModel>(table, m => ById(m.employeeId), "the schedule's employees"));
                }
            }
            Logger?.Info("Wages shown: " + _tables.Count + " of the game's employee tables and " + _watches.Count
                         + " of its wage labels show what is paid (the city's prices and agreed raises).");
        }

        private void Add(IWageTable table)
        {
            if (table.Found)
            {
                _tables.Add(table);
            }
        }

        private void Watch(WageShownWatch watch)
        {
            if (watch != null)
            {
                _watches.Add(watch);
            }
        }

        private void GiveBack()
        {
            if (_gaveBack)
            {
                return;
            }
            _gaveBack = true;
            for (int i = 0; i < _tables.Count; i++)
            {
                _tables[i].GiveBack();
            }
            for (int i = 0; i < _watches.Count; i++)
            {
                if (_watches[i] != null)
                {
                    _watches[i].GiveBack();
                }
            }
        }

        internal static EmployeeInstance ById(string id)
        {
            return string.IsNullOrEmpty(id) ? null : EmployeeHelper.GetEmployeeById(id, false);
        }

        /// <summary>
        /// The game's figure for an employee and what is paid, hundredths an hour; false where the game's own figure stands:
        /// nobody, the same figure, or (Going Public) another player's employee, whose wage this machine neither pays nor knows.
        /// </summary>
        internal static bool Paid(EmployeeInstance e, out int baseCents, out int paidCents)
        {
            baseCents = paidCents = 0;
            if (!Ours(e))
            {
                return false;
            }
            baseCents = PaidWage.Base(e.hourlyWage);
            paidCents = PaidWage.Now(e.hourlyWage, e.id);
            return PaidWage.Differs(baseCents, paidCents);
        }

        /// <summary>An employee with a wage whose screens this machine may write: not (Going Public) another player's.</summary>
        internal static bool Ours(EmployeeInstance e)
        {
            return e != null && e.hourlyWage > 0f
                   && !(GoingPublic.InSession && e.assignedAddress != null && GoingPublic.Foreign(BuildingHelper.GetBuildingRegistration(e.assignedAddress)));
        }

        /// <summary>True the first time in this save that a negotiation is told what its wage costs.</summary>
        internal static bool FirstTell(string negotiation)
        {
            if (!ReferenceEquals(_toldIn, Save))
            {
                _toldIn = Save;
                Told.Clear();
            }
            return !string.IsNullOrEmpty(negotiation) && Told.Add(negotiation);
        }
    }

    /// <summary>One of the game's tables of employees as the mod stands in for its scroller's delegate.</summary>
    internal interface IWageTable
    {
        bool Found { get; }

        void Keep();

        void GiveBack();
    }

    /// <summary>
    /// The game's scroller asks this for its rows instead of the game's table, and this hands every call on to the table
    /// after writing the row's wage at what is paid: the row's model is the game's own copy for the screen (BaseEmployeeModel),
    /// made afresh each time the table loads, so nothing is written twice and nothing of the employee is touched. The sizes
    /// are asked for every row as the table reloads, so all rows are written before the game sorts by wage again.
    /// COST: one look-up a row the scroller asks for.
    /// </summary>
    internal sealed class WageTable<TCell, TModel> : IWageTable, IEnhancedScrollerDelegate
        where TCell : BaTableCellView<TModel>
        where TModel : BaseEmployeeModel
    {
        private readonly BaTable<TCell, TModel> _table;
        private readonly Func<TModel, EmployeeInstance> _who;
        private readonly string _name;
        private bool _toldHeld;

        internal WageTable(BaTable<TCell, TModel> table, Func<TModel, EmployeeInstance> who, string name)
        {
            _table = table;
            _who = who;
            _name = name;
        }

        public bool Found => _table != null && _table.scroller != null;

        // The game's table makes itself the scroller's delegate in its Start: taken once it has, and again should it ever do so.
        public void Keep()
        {
            if (_table == null || _table.scroller == null)
            {
                return;
            }
            EnhancedScroller scroller = _table.scroller;
            IEnhancedScrollerDelegate now = scroller.Delegate;
            if (ReferenceEquals(now, _table))
            {
                scroller.Delegate = this;   // the scroller reloads at its next frame, so the rows showing are written again at the paid wage
            }
            else if (now != null && !ReferenceEquals(now, this) && !_toldHeld)
            {
                _toldHeld = true;
                WageShown.Logger?.Info("Wages shown: " + _name + " are drawn by something else's hand; left as they are, at the game's figures.");
            }
        }

        public void GiveBack()
        {
            if (_table != null && _table.scroller != null && ReferenceEquals(_table.scroller.Delegate, this))
            {
                _table.scroller.Delegate = _table;
            }
        }

        public int GetNumberOfCells(EnhancedScroller scroller)
        {
            try
            {
                return _table.GetNumberOfCells(scroller);
            }
            catch (Exception e)
            {
                return Guard.Failed(WageShown.Shown, e, 0);
            }
        }

        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            try
            {
                Write(dataIndex);
            }
            catch (Exception e)
            {
                Guard.Fail(WageShown.Shown, e);
            }
            return _table.GetCellViewSize(scroller, dataIndex);
        }

        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            try
            {
                Write(dataIndex);
            }
            catch (Exception e)
            {
                Guard.Fail(WageShown.Shown, e);
            }
            return _table.GetCellView(scroller, dataIndex, cellIndex);
        }

        // Always from the employee's own wage, never the row's, so a row written already comes out the same.
        private void Write(int index)
        {
            List<TModel> data = _table.data;
            if (WageShown.Shown.Off || data == null || index < 0 || index >= data.Count || data[index] == null)
            {
                return;
            }
            TModel row = data[index];
            EmployeeInstance e = _who(row);
            if (!WageShown.Ours(e))
            {
                return;
            }
            int paid = PaidWage.Now(e.hourlyWage, e.id);
            if (PaidWage.Base(row.hourlyWage) != paid)
            {
                row.hourlyWage = paid / 100f;
            }
        }
    }
}
