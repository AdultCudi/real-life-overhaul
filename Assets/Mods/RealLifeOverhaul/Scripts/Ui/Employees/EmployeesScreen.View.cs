using System;
using System.Collections.Generic;
using BigAmbitions.Characters.Skills;
using Buildings.Office.Headquarters;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Enums;
using Extensions;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI.Smartphone.Apps.MyEmployees;
using UI.Smartphone.Apps.Shared;
using UnityEngine;
using UnityEngine.UI;
using Dropdown = UI.Elements.Dropdown;

namespace RealLifeOverhaul.Ui
{
    // What My Employees shows, read from the game's own tables, box, filters and panels after the game and other mods
    // filled them; the view model is built from it (StaffViews), game-free.
    public sealed partial class EmployeesScreen
    {
        private enum Slot : byte { Business, Skill, Demand, Status }

        private static readonly Slot[] EmployeeSlots = { Slot.Business, Slot.Skill, Slot.Demand, Slot.Status };
        private static readonly Slot[] CandidateSlots = { Slot.Skill, Slot.Demand };

        // The game's header of each column, which sorts its table (BaTable: a header's name is the model's field); null: none.
        private static readonly string[] EmployeeHeaders = { "EmployeeName", "CurrentBusinessName", "PrimarySkill", "HourlyWage", "Satisfaction", "Task" };
        private static readonly string[] CandidateHeaders = { "EmployeeName", "PrimarySkill", "HourlyWage", "Schedule", null, "ExpiresIn" };

        private const string TrainingTask = "myemployees_task_training";   // the game's task words while they train (EmployeeInstance.GetCurrentTask)
        private const string AbsentTask = "myemployees_task_absent";

        private readonly StaffPersonView _person = new StaffPersonView();
        private readonly StaffPreviewView _preview = new StaffPreviewView();
        private readonly List<EmployeeInstance> _mates = new List<EmployeeInstance>(8);   // the people beside the person open, as listed
        private const int MostMates = 8;   // the people listed beside a person: more than a page's room holds
        private readonly List<Component> _toggles = new List<Component>(64);   // the shown tab's filter toggles, the game's own
        private readonly List<string> _toggleKeys = new List<string>(64);
        private readonly List<int> _optionToggle = new List<int>(48);
        private readonly List<string> _words = new List<string>(48);
        private readonly List<Transform> _clones = new List<Transform>(8);
        private bool _togglesOfCandidates;

        // ------------------------------------------------------------------------------------------------- the signature

        // What changing would change the screen: the tab, the game's rows and their figures, what is picked, the person open
        // and their box, the panel of choices, the candidates' count and the filters.
        private long Signature()
        {
            bool candidates = Candidates();
            long sig = candidates ? 7 : 3;
            if (candidates)
            {
                List<CandidateModel> data = _app.candidateScrollerController.data;
                sig = sig * 31 + (data != null ? data.Count : -1);
                for (int i = 0; data != null && i < data.Count; i++)
                {
                    CandidateModel c = data[i];
                    sig = sig * 31 + (c != null ? c.GetHashCode() + c.hoursUntilExpiring : 0);
                }
                var hide = GameContract.StaffHideJobBoard.Field?.GetValue(_app.candidateScrollerController) as Toggle;
                sig = sig * 31 + (hide != null && hide.isOn ? 1 : 0);
            }
            else
            {
                List<EmployeeModel> data = _app.employeeScrollerController.data;
                sig = sig * 31 + (data != null ? data.Count : -1);
                for (int i = 0; data != null && i < data.Count; i++)
                {
                    EmployeeModel e = data[i];
                    sig = sig * 31 + (e != null ? e.GetHashCode() + (int)e.satisfaction + e.hoursPerWeek * 101 : 0);
                }
            }
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            sig = sig * 31 + (picked != null ? picked.Count : 0);
            sig = sig * 31 + CandidateScrollerController.CandidateCount;
            sig = sig * 31 + (candidates ? _app.candidateScrollerController.ActiveFilterCount : _app.employeeScrollerController.ActiveFilterCount);
            var panel = GameContract.StaffMassPanel.Field?.GetValue(_app.massActionsUI) as Transform;
            var choices = GameContract.StaffMassOptions.Field?.GetValue(_app.massActionsUI) as Dropdown;
            sig = sig * 31 + (panel != null && panel.gameObject.activeSelf ? 1 + (choices != null ? choices.SelectedOptionIndex + 1 : 0) : 0);
            EmployeeInstance p = _app.SelectedEmployeeInstance;
            if (p != null)
            {
                sig = sig * 31 + p.GetHashCode();
                sig = sig * 31 + (int)p.satisfaction + (p.IsTraining ? 1000 : 0) + (p.isAbsent ? 2000 : 0) + (UiWidgets.Shows(_bonus) ? 4000 : 0);
                sig = sig * 31 + (p.assignedAddress != null ? p.assignedAddress.GetHashCode() : 0) + p.assignedWeeklyHours * 7 + p.workedHoursThisWeek;
                for (int i = 0; p.characterData != null && i < p.characterData.skills.Count; i++)
                {
                    sig = sig * 31 + p.characterData.skills[i].GetRoundedValue();
                }
                var assign = GameContract.StaffAssign.Field?.GetValue(_app) as Dropdown;
                sig = sig * 31 + (assign != null ? assign.SelectedOptionIndex : 0);
            }
            return sig;
        }

        // ------------------------------------------------------------------------------------------------- the view

        private void Build()
        {
            StaffView v = _v;
            bool candidates = Candidates();
            v.Candidates = candidates;
            v.EmployeeCount = EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { excludeBeingReplaced = true }).Count;
            v.CandidateCount = CandidateScrollerController.CandidateCount;
            var hide = GameContract.StaffHideJobBoard.Field?.GetValue(_app.candidateScrollerController) as Toggle;
            v.HideJobBoard = hide != null && hide.isOn;
            EmployeeInstance open = _app.SelectedEmployeeInstance;
            if (open != null)
            {
                _picking = false;   // the game's box is up: the picking bar belongs to the table
                Person(open);
                v.Person = _person;
            }
            else
            {
                v.Person = null;
                _train.Clear();
                Rows(v, candidates);
            }
            Options(v);
        }

        private void Rows(StaffView v, bool candidates)
        {
            _rows.Clear();
            _dataIndex.Clear();
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            int pickedCount = 0;
            if (candidates)
            {
                List<CandidateModel> data = _app.candidateScrollerController.data ?? new List<CandidateModel>();
                var rows = new CandidateRow[data.Count];
                int n = 0;
                for (int i = 0; i < data.Count; i++)
                {
                    CandidateModel c = data[i];
                    if (c == null || c.employeeInstance == null)
                    {
                        continue;
                    }
                    bool on = picked != null && picked.Contains(c.employeeInstance);
                    pickedCount += on ? 1 : 0;
                    rows[n++] = new CandidateRow
                    {
                        Name = c.employeeName, Role = c.primarySkillName.GetLocalization(), Skill = c.primarySkillPercentage, Wage = Wage(c.employeeInstance), Schedule = c.schedule,
                        Source = c.recruitmentSource, Expires = Expires(c.hoursUntilExpiring), Soon = c.hoursUntilExpiring < 24, Picked = on
                    };
                    _rows.Add(c.employeeInstance);
                    _dataIndex.Add(i);
                }
                Array.Resize(ref rows, n);
                v.CandidateRows = rows;
                v.Employees = new StaffRow[0];
                v.Listed = data.Count;
                v.TotalWage = null;
            }
            else
            {
                List<EmployeeModel> data = _app.employeeScrollerController.data ?? new List<EmployeeModel>();
                var rows = new StaffRow[data.Count];
                int n = 0;
                float wages = 0f;
                for (int i = 0; i < data.Count; i++)
                {
                    EmployeeModel e = data[i];
                    if (e == null || e.employeeInstance == null || !OfBusiness(e.currentBusinessName))
                    {
                        continue;
                    }
                    bool on = picked != null && picked.Contains(e.employeeInstance);
                    pickedCount += on ? 1 : 0;
                    float paid = PaidMoney(e.employeeInstance);
                    wages += paid;
                    rows[n++] = new StaffRow
                    {
                        Name = e.employeeName, Role = e.primarySkillName.GetLocalization(), Business = e.currentBusinessName,
                        Skill = e.primarySkillPercentage, Wage = paid.ToCurrencyFormat(), Satisfaction = Mathf.RoundToInt(e.satisfaction),
                        Task = TaskWords(e.task), TaskTone = TaskTone(e.task, e.isAbsent), Picked = on
                    };
                    _rows.Add(e.employeeInstance);
                    _dataIndex.Add(i);
                }
                Array.Resize(ref rows, n);
                v.Employees = rows;
                v.CandidateRows = new CandidateRow[0];
                v.Listed = data.Count;
                v.TotalWage = wages.ToCurrencyFormat();
            }
            v.PickedCount = pickedCount;
            v.AllPicked = _rows.Count > 0 && pickedCount == _rows.Count;
            v.Picking = _picking;
            if (_lastPerson != null && _rows.IndexOf(_lastPerson) >= 0)
            {
                _previewRow = _rows.IndexOf(_lastPerson);   // back from a page, the row it opened from is the one in view
                _lastPerson = null;
            }
            _previewRow = _rows.Count == 0 ? 0 : Math.Max(0, Math.Min(_previewRow, _rows.Count - 1));
            v.Selected = _rows.Count > 0 && !_picking ? _previewRow : -1;
            v.Preview = _rows.Count > 0 ? Preview(_rows[_previewRow], candidates) : null;
            v.Offset = _offset;
            v.Reveal = _reveal;
            Sorted(v, candidates);
            Filters(v, candidates);
            Mass(v);
        }

        private bool OfBusiness(string business) => _business == null || (_business.Length == 0 ? string.IsNullOrEmpty(business) : business == _business);

        // The sort the game's table shows: the header whose arrow is up.
        private void Sorted(StaffView v, bool candidates)
        {
            string[] headers = candidates ? CandidateHeaders : EmployeeHeaders;
            Transform root = (candidates ? (Component)_app.candidateScrollerController : _app.employeeScrollerController).transform.Find("Headers");
            v.SortColumn = -1;
            for (int c = 0; root != null && c < headers.Length; c++)
            {
                Transform head = headers[c] != null ? root.Find(headers[c]) : null;
                Transform up = head != null ? head.Find("ArrowUp") : null, down = head != null ? head.Find("ArrowDown") : null;
                if ((up != null && up.gameObject.activeSelf) || (down != null && down.gameObject.activeSelf))
                {
                    v.SortColumn = c;
                    v.SortDown = down != null && down.gameObject.activeSelf;
                }
            }
        }

        private string Header(int column)
        {
            string[] headers = Candidates() ? CandidateHeaders : EmployeeHeaders;
            return column >= 0 && column < headers.Length ? headers[column] : null;
        }

        // The game's actions for several picked, in its own order (Going Public narrows them on another player's staff), and
        // the panel of choices one has opened.
        private void Mass(StaffView v)
        {
            var types = GameContract.StaffMassTypes.Field?.GetValue(_app.massActionsUI) as string[];
            int n = types?.Length ?? 0;
            if (v.Mass.Length != n)
            {
                v.Mass = new StaffMass[n];
            }
            for (int i = 0; i < n; i++)
            {
                v.Mass[i] = new StaffMass { Type = types[i], Label = types[i].GetLocalization() };
            }
            var panel = GameContract.StaffMassPanel.Field?.GetValue(_app.massActionsUI) as Transform;
            var choices = GameContract.StaffMassOptions.Field?.GetValue(_app.massActionsUI) as Dropdown;
            if (panel == null || !panel.gameObject.activeSelf || choices == null)
            {
                v.AskValue = null;
                v.AskTitle = null;
                if (_open == UiStaff.ListMass)
                {
                    _open = -1;
                }
                return;
            }
            _picking = true;
            v.Picking = true;
            var title = GameContract.StaffMassTitle.Field?.GetValue(_app.massActionsUI) as TextLocalizationComponent;
            v.AskTitle = title != null ? UiWidgets.Text(title.TextContainer) : null;
            int chosen = choices.SelectedOptionIndex;
            Words(choices, _words);
            v.AskValue = chosen >= 0 && chosen < _words.Count ? _words[chosen] : UiText.T("common_select_option");
        }

        // ------------------------------------------------------------------------------------------------- filters

        private Slot[] Slots() => Candidates() ? CandidateSlots : EmployeeSlots;

        // The shown tab's filter toggles, read once a tab (and again should the game build them anew), each with its key.
        private void Toggles()
        {
            bool candidates = Candidates();
            if (_toggles.Count > 0 && _togglesOfCandidates == candidates && _toggles[0] != null)
            {
                return;
            }
            _toggles.Clear();
            _toggleKeys.Clear();
            _togglesOfCandidates = candidates;
            var panel = (candidates ? GameContract.StaffCandidateFilters : GameContract.StaffEmployeeFilters).Field?.GetValue(_app) as Component;
            if (panel == null)
            {
                return;
            }
            // The panel stays shut until the player opens it, so its toggles are read switched off or on.
            if (candidates)
            {
                foreach (CandidateFilterToggle t in panel.GetComponentsInChildren<CandidateFilterToggle>(true))
                {
                    AddToggle(t);
                }
            }
            else
            {
                foreach (EmployeeFilterToggle t in panel.GetComponentsInChildren<EmployeeFilterToggle>(true))
                {
                    AddToggle(t);
                }
            }
        }

        private void AddToggle(Component t)
        {
            var label = t.GetComponentInChildren<TextLocalizationComponent>(true);
            _toggles.Add(t);
            _toggleKeys.Add(label != null ? label.Key : null);
        }

        private static bool IsOn(Component t) => t is EmployeeFilterToggle e ? e.IsOn : t is CandidateFilterToggle c && c.IsOn;

        private static FilterToggleGroup GroupOf(Component t) => t is EmployeeFilterToggle e ? e.Group : t is CandidateFilterToggle c ? c.Group : FilterToggleGroup.Other;

        private static void SetOn(Component t, bool on)
        {
            if (t is EmployeeFilterToggle e)
            {
                e.SetToggleWithoutNotify(on);
            }
            else if (t is CandidateFilterToggle c)
            {
                c.SetToggleWithoutNotify(on);
            }
        }

        private static bool InSlot(FilterToggleGroup g, Slot slot)
        {
            switch (slot)
            {
                case Slot.Skill: return g == FilterToggleGroup.Skill;
                case Slot.Status: return g == FilterToggleGroup.Status;
                case Slot.Demand:
                    return g == FilterToggleGroup.Type || g == FilterToggleGroup.Equipment || g == FilterToggleGroup.Schedule || g == FilterToggleGroup.HealthInsurance
                           || g == FilterToggleGroup.Environment;
                default: return false;
            }
        }

        // Each filter's value in words: everyone, the one chosen, or how many are chosen (in the game's panel, more can be).
        private void Filters(StaffView v, bool candidates)
        {
            Toggles();
            Slot[] slots = Slots();
            if (v.Filters.Length != slots.Length)
            {
                v.Filters = new string[slots.Length];
            }
            bool any = _business != null;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == Slot.Business)
                {
                    v.Filters[i] = _business == null ? UiText.T("reallife_ui_staff_all_businesses") : _business.Length == 0 ? UiText.T("common_unassigned") : _business;
                    continue;
                }
                int on = 0, last = -1;
                for (int k = 0; k < _toggles.Count; k++)
                {
                    if (_toggles[k] != null && InSlot(GroupOf(_toggles[k]), slots[i]) && IsOn(_toggles[k]))
                    {
                        on++;
                        last = k;
                    }
                }
                any |= on > 0;
                v.Filters[i] = on == 0 ? UiText.T(AllKey(slots[i])) : on == 1 ? (_toggleKeys[last] ?? string.Empty).GetLocalization()
                    : StaffViews.Fill(UiText.T("reallife_ui_staff_chosen"), "count", UiText.Whole(on));
            }
            TMP_InputField search = Search();
            v.Query = search != null ? search.text : null;
            v.Filtered = any || !string.IsNullOrEmpty(v.Query);
        }

        private static string AllKey(Slot slot) => slot == Slot.Skill ? "reallife_ui_staff_all_skills" : slot == Slot.Demand ? "reallife_ui_staff_all_demands" : "reallife_ui_staff_any_status";

        // The open list's options: a filter's, the businesses to assign to, or a mass action's choices.
        private void Options(StaffView v)
        {
            _optionKeys.Clear();
            _optionToggle.Clear();
            v.OpenList = _open;
            v.OptionChosen = -1;
            v.OptionOffset = _optionOffset;
            if (_open < 0)
            {
                v.Options = new string[0];
                return;
            }
            if (_open == UiStaff.ListAssign || _open == UiStaff.ListMass)
            {
                Dropdown drop = _open == UiStaff.ListAssign ? GameContract.StaffAssign.Field?.GetValue(_app) as Dropdown
                    : GameContract.StaffMassOptions.Field?.GetValue(_app.massActionsUI) as Dropdown;
                if (drop == null || (_open == UiStaff.ListAssign && v.Person == null) || (_open == UiStaff.ListMass && v.AskValue == null))
                {
                    _open = -1;
                    v.OpenList = -1;
                    v.Options = new string[0];
                    return;
                }
                Words(drop, _words);
                _optionKeys.AddRange(_words);
                v.Options = _words.ToArray();
                v.OptionChosen = drop.SelectedOptionIndex;
                return;
            }
            Slot[] slots = Slots();
            if (v.Person != null || _open >= slots.Length)
            {
                _open = -1;
                v.OpenList = -1;
                v.Options = new string[0];
                return;
            }
            var words = new List<string>(24);
            Slot slot = slots[_open];
            if (slot == Slot.Business)
            {
                words.Add(UiText.T("reallife_ui_staff_all_businesses"));
                _optionKeys.Add(null);
                words.Add(UiText.T("common_unassigned"));
                _optionKeys.Add(string.Empty);
                foreach (string name in Businesses())
                {
                    words.Add(name);
                    _optionKeys.Add(name);
                }
                v.OptionChosen = _business == null ? 0 : _optionKeys.IndexOf(_business);
            }
            else
            {
                words.Add(UiText.T(AllKey(slot)));
                _optionKeys.Add(null);
                _optionToggle.Add(-1);
                int on = 0, chosen = 0;
                for (int k = 0; k < _toggles.Count; k++)
                {
                    if (_toggles[k] == null || !InSlot(GroupOf(_toggles[k]), slot))
                    {
                        continue;
                    }
                    if (IsOn(_toggles[k]))
                    {
                        on++;
                        chosen = words.Count;
                    }
                    words.Add((_toggleKeys[k] ?? string.Empty).GetLocalization());
                    _optionKeys.Add(_toggleKeys[k]);
                    _optionToggle.Add(k);
                }
                v.OptionChosen = on == 0 ? 0 : on == 1 ? chosen : -1;
            }
            v.Options = words.ToArray();
        }

        // Every business the player's staff work at, by name, in the order of their names.
        private List<string> Businesses()
        {
            var names = new List<string>(8);
            foreach (EmployeeInstance e in EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { excludeBeingReplaced = true }))
            {
                string name = e != null && !e.IsCandidate ? BuildingHelper.GetBuildingRegistration(e.assignedAddress)?.BusinessName : null;
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }
            names.Sort(StringComparer.CurrentCulture);
            return names;
        }

        // A filter set through the game's own toggles: the group's others off, the chosen on, then the game's table filters
        // itself again and puts back the sort its header shows, as it does after loading.
        private bool SetFilter(int list, int option)
        {
            Slot[] slots = Slots();
            if (list < 0 || list >= slots.Length || option >= _optionToggle.Count)
            {
                return false;
            }
            Slot slot = slots[list];
            for (int k = 0; k < _toggles.Count; k++)
            {
                if (_toggles[k] != null && InSlot(GroupOf(_toggles[k]), slot))
                {
                    SetOn(_toggles[k], false);
                }
            }
            int chosen = _optionToggle[option];
            if (chosen >= 0 && _toggles[chosen] != null)
            {
                SetOn(_toggles[chosen], true);
            }
            Refilter();
            _offset = 0f;
            return true;
        }

        private bool ClearFilters()
        {
            _business = null;
            if (Candidates())
            {
                _app.candidateScrollerController.ClearFilters();
                _app.candidateScrollerController.ChangeSortOrder(null, false);
            }
            else
            {
                _app.employeeScrollerController.ClearFilters();
                _app.employeeScrollerController.ChangeSortOrder(null, false);
            }
            _offset = 0f;
            return true;
        }

        private void Refilter()
        {
            if (Candidates())
            {
                _app.candidateScrollerController.RefreshFilters();
                _app.candidateScrollerController.ChangeSortOrder(null, false);
            }
            else
            {
                _app.employeeScrollerController.RefreshFilters();
                _app.employeeScrollerController.ChangeSortOrder(null, false);
            }
        }

        // The shown tab's search field: the game's own, inside its filter panel.
        private TMP_InputField Search()
        {
            var panel = (Candidates() ? GameContract.StaffCandidateFilters : GameContract.StaffEmployeeFilters).Field?.GetValue(_app) as Component;
            return panel != null ? panel.GetComponentInChildren<TMP_InputField>(true) : null;
        }

        // The row under the cursor for the preview beside the table, from the person's own record as the game's row holds it.
        private StaffPreviewView Preview(EmployeeInstance e, bool candidate)
        {
            StaffPreviewView p = _preview;
            string primary = e.GetPrimarySkill();
            p.Candidate = candidate;
            p.Name = e.characterData != null ? e.characterData.name : string.Empty;
            p.Role = string.IsNullOrEmpty(primary) ? null : primary.GetLocalization();
            p.RoleIcon = StaffViews.SkillIcon(primary);
            p.Business = BuildingHelper.GetBuildingRegistration(e.assignedAddress)?.BusinessName;
            p.Skill = string.IsNullOrEmpty(primary) ? 0 : Mathf.RoundToInt(e.GetSkillValue(primary));
            p.Satisfaction = Mathf.RoundToInt(e.satisfaction);
            p.Age = e.characterData != null ? TimeHelper.GetYearsByDays(e.characterData.ageInDays) : 0;
            p.Hours = e.assignedWeeklyHours;
            p.Wage = Wage(e);
            if (candidate)
            {
                int hours = e.candidateInfo != null ? e.candidateInfo.hoursUntilExpiring : 0;
                p.Expires = Expires(hours);
                p.Status = StaffViews.Fill(UiText.T("reallife_ui_staff_expires"), "time", p.Expires);
                p.StatusTone = hours < 24 ? UiRole.Warn : UiRole.Muted;
                p.Schedule = e.GetDemandOfTypeLocalized<HoursWorkingPerWeek>();
                p.Source = e.candidateInfo != null ? e.candidateInfo.GetSource() : null;
                p.Demands = 0;
                p.DemandsMet = 0;
                return p;
            }
            string task = e.GetCurrentTask();
            p.Status = TaskWords(task);
            p.StatusTone = TaskTone(task, e.isAbsent);
            p.Demands = e.demands != null ? e.demands.Count : 0;
            p.DemandsMet = 0;
            for (int i = 0; i < p.Demands; i++)
            {
                JobDemand d = JobDemandHelper.GetByName(e.demands[i]);
                p.DemandsMet += d != null && d.Fulfilled(e, null) ? 1 : 0;   // the game's own rule its box asks
            }
            return p;
        }

        // ------------------------------------------------------------------------------------------------- the person

        // One person's page from the game's box as its ShowEmployee wrote it, and the person's own record behind it.
        private void Person(EmployeeInstance e)
        {
            StaffPersonView p = _person;
            bool candidate = e.IsCandidate;
            string primary = e.GetPrimarySkill();
            p.Candidate = candidate;
            p.Name = e.characterData != null ? e.characterData.name : string.Empty;
            p.Role = string.IsNullOrEmpty(primary) ? null : primary.GetLocalization();
            p.RoleIcon = StaffViews.SkillIcon(primary);
            p.Business = BuildingHelper.GetBuildingRegistration(e.assignedAddress)?.BusinessName;
            p.Satisfaction = Mathf.RoundToInt(e.satisfaction);
            p.Level = string.IsNullOrEmpty(primary) ? 0 : Mathf.RoundToInt(e.GetSkillValue(primary));
            p.Age = e.characterData != null ? TimeHelper.GetYearsByDays(e.characterData.ageInDays) : 0;
            p.Wage = PaidMoney(e).ToCurrencyFormat();
            p.Positive = Shown(_positive) ? UiWidgets.Label(_positive) : null;
            p.PositiveOn = UiWidgets.Shows(_positive);
            p.Negative = Shown(_negative) ? UiWidgets.Label(_negative) : null;
            p.NegativeOn = UiWidgets.Shows(_negative);
            p.NegativeArmed = _armed;
            p.Additional = Shown(_additional) ? UiWidgets.Label(_additional) : null;
            p.AdditionalOn = UiWidgets.Shows(_additional);
            if (candidate)
            {
                int hours = e.candidateInfo != null ? e.candidateInfo.hoursUntilExpiring : 0;
                p.Expires = Expires(hours);
                p.Soon = hours < 24;
                p.Schedule = e.GetDemandOfTypeLocalized<HoursWorkingPerWeek>();
                p.Source = e.candidateInfo != null ? e.candidateInfo.GetSource() : null;
                p.Status = StaffViews.Fill(UiText.T("reallife_ui_staff_expires"), "time", p.Expires);
                p.StatusTone = p.Soon ? UiRole.Warn : UiRole.Muted;
                p.Bonus = null;
                p.Cooldown = null;
                p.Sick = false;
                p.HrManager = null;
            }
            else
            {
                string task = e.GetCurrentTask();
                p.DaysHired = Math.Max(0, SaveGameManager.Current.Day - e.dayHired);
                p.WeeklyHours = e.assignedWeeklyHours;
                p.WorkedHours = e.workedHoursThisWeek;
                p.Task = TaskWords(task);
                p.Status = p.Task;
                p.StatusTone = TaskTone(task, e.isAbsent);
                p.Bonus = Shown(_bonus) ? UiWidgets.Label(_bonus) : null;
                p.BonusOn = UiWidgets.Shows(_bonus);
                var cooldown = GameContract.StaffCooldown.Field?.GetValue(_app) as TextLocalizationComponent;
                p.Cooldown = cooldown != null && cooldown.gameObject.activeSelf ? UiWidgets.Text(cooldown.TextContainer) : null;
                p.Sick = e.isAbsent;
                p.SickWords = e.isAbsent ? "myemployees_called_in_sick".Localize(new { employeeName = p.Name }).ToString() : null;
                HrManagerPlan plan = string.IsNullOrEmpty(e.assignedHrManagerPlanId) ? null : HrManagerHelper.GetPlanFromId(e.assignedHrManagerPlanId);
                p.HrManager = plan != null && plan.HrManagerInstance != null && plan.HrManagerInstance.characterData != null ? plan.HrManagerInstance.characterData.name : null;
            }
            Assignment(p);
            Training(e, p);
            Skills(e, p, candidate);
            Demands(e, p, candidate);
            Mates(e, p, candidate, primary);
        }

        // The people beside a person: who works at the same business, or (for a candidate) the staff with the same skill, the
        // most skilled first, as many as a page lists.
        private void Mates(EmployeeInstance e, StaffPersonView p, bool candidate, string primary)
        {
            _mates.Clear();
            int total = 0;
            if (candidate ? !string.IsNullOrEmpty(primary) : e.assignedAddress != null && !string.IsNullOrEmpty(p.Business))
            {
                foreach (EmployeeInstance other in EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { excludeBeingReplaced = true }))
                {
                    if (other == null || other == e || other.IsCandidate || (candidate ? other.GetPrimarySkill() != primary : other.assignedAddress != e.assignedAddress))
                    {
                        continue;
                    }
                    total++;
                    _mates.Add(other);
                }
            }
            _mates.Sort((a, b) => b.GetSkillValue(b.GetPrimarySkill()).CompareTo(a.GetSkillValue(a.GetPrimarySkill())));
            if (_mates.Count > MostMates)
            {
                _mates.RemoveRange(MostMates, _mates.Count - MostMates);
            }
            if (p.Mates.Length != _mates.Count)
            {
                p.Mates = new StaffMate[_mates.Count];
            }
            for (int i = 0; i < _mates.Count; i++)
            {
                EmployeeInstance m = _mates[i];
                string skill = m.GetPrimarySkill();
                p.Mates[i] = new StaffMate
                {
                    Name = m.characterData != null ? m.characterData.name : string.Empty, Role = string.IsNullOrEmpty(skill) ? null : skill.GetLocalization(),
                    RoleIcon = StaffViews.SkillIcon(skill), Skill = string.IsNullOrEmpty(skill) ? 0 : Mathf.RoundToInt(m.GetSkillValue(skill)), Satisfaction = Mathf.RoundToInt(m.satisfaction)
                };
            }
            p.MatesTotal = total;
            p.MatesAt = candidate ? p.Role : p.Business;
        }

        // The game's choice of business, as its box shows it; held while they train, as the game hides it then.
        private void Assignment(StaffPersonView p)
        {
            var assign = GameContract.StaffAssign.Field?.GetValue(_app) as Dropdown;
            p.AssignOn = assign != null && assign.gameObject.activeSelf;
            Words(assign, _words);
            int chosen = assign != null ? assign.SelectedOptionIndex : -1;
            p.Assign = chosen >= 0 && chosen < _words.Count ? _words[chosen] : UiText.T("common_unassigned");
        }

        // The training under way, from the game's own overlay over its box: the skill and the hours left as it wrote them.
        private void Training(EmployeeInstance e, StaffPersonView p)
        {
            var overlay = GameContract.StaffTraining.Field?.GetValue(_app) as Transform;
            bool training = e.IsTraining && overlay != null && overlay.gameObject.activeSelf;
            p.Training = training ? Words(overlay.Find("SkillName")) : null;
            p.TrainingLeft = training ? Words(overlay.Find("HoursLeft")) : null;
            p.TrainingCard = training && _cardPutAway != e.id;
        }

        // The skills from the person's record, each with the train button of the copy the game made for it in its box: the
        // copies of the last ShowEmployee stand last, while the ones before it wait to be destroyed at the frame's end.
        private void Skills(EmployeeInstance e, StaffPersonView p, bool candidate)
        {
            _train.Clear();
            List<Skill> skills = e.characterData != null ? e.characterData.skills : null;
            int n = skills != null ? skills.Count : 0;
            Clones(GameContract.StaffSkillTemplate.Field?.GetValue(_app) as Transform, n);
            if (p.Skills.Length != n)
            {
                p.Skills = new UiStaffSkill[n];
            }
            string train = UiText.T("myemployees_train_skill_button");
            for (int i = 0; i < n; i++)
            {
                Transform clone = _clones.Count == n ? _clones[i] : null;
                Transform child = clone != null ? clone.Find("TrainButton") : null;
                Button button = child != null ? child.GetComponent<Button>() : null;
                bool can = !candidate && button != null && button.gameObject.activeSelf && button.interactable;
                _train.Add(can ? button : null);
                p.Skills[i] = new UiStaffSkill { Label = skills[i].name.GetLocalization(), Value = skills[i].GetRoundedValue(), Train = can ? train : null };
            }
        }

        // The wants and demands from the person's record, each with how much it matters and, for the hired, whether it is
        // met, by the game's own rule its box asks (JobDemand.Fulfilled), and the hours or days scheduled and worked.
        private void Demands(EmployeeInstance e, StaffPersonView p, bool candidate)
        {
            List<string> demands = e.demands;
            int n = demands != null ? demands.Count : 0;
            if (p.Demands.Length != n)
            {
                p.Demands = new UiStaffDemand[n];
            }
            string met = UiText.T("reallife_ui_staff_met"), notMet = UiText.T("reallife_ui_staff_not_met");
            for (int i = 0; i < n; i++)
            {
                string key = demands[i];
                JobDemand d = JobDemandHelper.GetByName(key);
                bool fulfilled = !candidate && d != null && d.Fulfilled(e, null);
                string note = null;
                if (!candidate && d is HoursWorkingPerWeek)
                {
                    note = "myemployees_employee_hours".Localize(new { e.assignedWeeklyHours, e.workedHoursThisWeek }).ToString();
                }
                else if (!candidate && d is DaysWorkingPerWeek)
                {
                    note = "myemployees_employee_days".Localize(new { assignedWeeklyDays = e.assignedWeeklyDays.Count, workedDays = e.workedDays }).ToString();
                }
                p.Demands[i] = new UiStaffDemand
                {
                    Icon = DemandIcon(key), Name = key.GetLocalization(), Note = OneLine(note),
                    Priority = d != null ? d.PriorityLocalizeKey.GetLocalization() : null, PriorityTone = d != null ? PriorityTone(d.priority) : UiRole.Muted,
                    Met = candidate || d == null ? -1 : fulfilled ? 1 : 0, MetWords = fulfilled ? met : notMet
                };
            }
        }

        // A demand's glyph by the game's group for it: hours, schedule, equipment, health insurance, the workplace.
        private static UiIcon DemandIcon(string key)
        {
            if (JobDemandHelper.TypeDemands.Contains(key))
            {
                return UiIcon.Clock;
            }
            if (JobDemandHelper.ScheduleDemands.Contains(key))
            {
                return UiIcon.Calendar;
            }
            if (JobDemandHelper.EquipmentDemands.Contains(key))
            {
                return UiIcon.Package;
            }
            if (JobDemandHelper.HealthInsuranceDemands.Contains(key))
            {
                return UiIcon.Heart;
            }
            return JobDemandHelper.EnvironmentDemands.Contains(key) ? UiIcon.Sofa : UiIcon.Briefcase;
        }

        // How much a demand matters, as the game colours its word: critical red, important amber, nice to have quiet.
        private static UiRole PriorityTone(Priority priority) => priority == Priority.High ? UiRole.Danger : priority == Priority.Medium ? UiRole.Warn : UiRole.Muted;

        // The last copies of a template in its parent, one for each the game just made.
        private void Clones(Transform template, int count)
        {
            _clones.Clear();
            Transform parent = template != null ? template.parent : null;
            if (parent == null)
            {
                return;
            }
            for (int i = parent.childCount - 1; i >= 0 && _clones.Count < count; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != template && child.gameObject.activeSelf)
                {
                    _clones.Insert(0, child);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- words

        private static bool Shown(Button b) => b != null && b.gameObject.activeInHierarchy;

        // What a person is paid an hour: the game's wage at the city's prices and any agreed raise (Core/PaidWage), or the
        // game's own figure for another player's employee, whose pay this machine neither pays nor knows (Going Public).
        private static float PaidMoney(EmployeeInstance e) => WageShown.Ours(e) ? PaidWage.NowMoney(e.hourlyWage, e.id) : e.hourlyWage;

        private static string Wage(EmployeeInstance e) => PaidMoney(e).ToCurrencyFormat();

        // A candidate's time left as the game's own box writes it: hours under a day, else whole days.
        private static string Expires(int hours) => hours < 24 ? "common_hours".Localize(new { hours }).ToString()
            : "common_days".Localize(new { value = Mathf.RoundToInt(hours / 24f) }).ToString();

        private static string TaskWords(string task) => (task ?? "myemployees_no_task").GetLocalization();

        // What they do now, as the game's table colours it: no task red, sick amber, training the cyan, at work quiet.
        private static UiRole TaskTone(string task, bool absent) => task == null ? UiRole.Danger : task == AbsentTask || absent ? UiRole.Warn : task == TrainingTask ? UiRole.Accent : UiRole.Muted;

        // The game's two lines ("Scheduled 36 hours" over "Worked 12 hours") on the one line under a demand's name.
        private static string OneLine(string text) => string.IsNullOrEmpty(text) ? null : text.Replace("\r", string.Empty).Replace("\n", ", ");

        private static string Words(Transform t)
        {
            TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
            return text != null ? UiWidgets.Text(text) : null;
        }

        // A drop-down's options as it shows them: its words, or the keys it puts into words.
        private static void Words(Dropdown drop, List<string> into)
        {
            into.Clear();
            var options = drop != null ? GameContract.StaffDropdownOptions.Field?.GetValue(drop) as List<string> : null;
            if (options == null)
            {
                return;
            }
            bool localized = GameContract.StaffDropdownWords.Field?.GetValue(drop) is bool b && b;
            for (int i = 0; i < options.Count; i++)
            {
                into.Add(localized ? options[i].GetLocalization() : options[i]);
            }
        }
    }
}
