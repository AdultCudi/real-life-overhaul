using System;
using System.Collections.Generic;
using Extensions;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Managers tab: every place and district with its manager, a place's kinds of decision with a switch each in the
    /// manager's own panel and its log, a decision with its undo, and the staff to appoint from. The table and the white box as the other tabs have them; a page is
    /// written when the tab opens, a row is picked or something changes. The picks:
    ///   "mp:place" a place; "mk:place*kind" one of its kinds; "md:id" a decision; "ma:place" its staff to appoint from;
    ///   "mc:place*id" one of them; "rg:district" a district's regional manager and who could be; "rc:district*id" one of them.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private const int DecisionsShown = 40;   // a place's decisions in its table, the newest first

        private static readonly string[] TaskKeys =
        {
            "reallife_mgr_task_raises", "reallife_mgr_task_offers", "reallife_mgr_task_overtime", "reallife_mgr_task_claims",
            "reallife_mgr_task_stock", "reallife_mgr_task_prices", "reallife_mgr_task_rota", "reallife_mgr_task_hiring"
        };
        private static readonly string[] TaskWhatKeys =
        {
            "reallife_mgr_task_raises_what", "reallife_mgr_task_offers_what", "reallife_mgr_task_overtime_what", "reallife_mgr_task_claims_what",
            "reallife_mgr_task_stock_what", "reallife_mgr_task_prices_what", "reallife_mgr_task_rota_what", "reallife_mgr_task_hiring_what"
        };
        // Why a kind given waits, by kind; none for the two nothing else stops.
        private static readonly string[] TaskWaitKeys =
        {
            "reallife_mgr_task_staff_waits", "reallife_mgr_task_staff_waits", "reallife_mgr_task_staff_waits", "reallife_mgr_task_claims_waits",
            null, "reallife_mgr_task_prices_waits", "reallife_mgr_task_rota_waits", null
        };
        private static readonly string[] KindKeys = { "reallife_mgr_kind_shop", "reallife_mgr_kind_works", "reallife_mgr_kind_warehouse" };
        private static readonly string[] StateKeys = { "reallife_mgr_state_done", "reallife_mgr_state_undone", "reallife_mgr_state_overridden", "reallife_mgr_state_closed" };

        private readonly List<ManagerHand> _hands = new List<ManagerHand>(16);
        private readonly List<ItemChange> _shownItems = new List<ItemChange>(8);

        partial void WriteManagers(InfoBox box, ref string selected)
        {
            ManagerState ms = _director.ManagerData;
            if (!_director.ManagersOn || ms == null)
            {
                _spec = CompanyTables.Off;
                box.Title(CompanyText.T("reallife_mgr_tab"));
                box.Line(CompanyText.T("reallife_mgr_off"), CellTone.Yellow);
                Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
                Say("off", CompanyText.T("reallife_mgr_off_row"));
                return;
            }
            string place, rest;
            if (Split("mk:", out place, out rest) || Split("mc:", out place, out rest))
            {
                bool appointing = _pick.StartsWith("mc:", StringComparison.Ordinal);
                ManagedPlace mp = _director.ManagedAt(place);
                if (mp != null && appointing)
                {
                    WriteStaffToAppoint(mp, ref selected);
                    WriteHand(box, mp, rest);
                    return;
                }
                int task;
                if (mp != null && int.TryParse(rest, out task) && task >= 0 && task < (int)ManagerTask.Count)
                {
                    WritePlaceRows(ms, mp, ref selected);
                    WriteTask(box, mp, (ManagerTask)task);
                    return;
                }
            }
            string id = Picked("md:");
            int number;
            Decision d = id != null && int.TryParse(id, out number) ? ms.Find(number) : null;
            ManagedPlace at = d != null ? _director.ManagedAt(d.Place) : null;
            if (at != null)
            {
                WritePlaceRows(ms, at, ref selected);
                WriteDecision(box, d, at);
                return;
            }
            place = Picked("ma:");
            at = place != null ? _director.ManagedAt(place) : null;
            if (at != null)
            {
                WriteStaffToAppoint(at, ref selected);
                WriteAppoint(box, at);
                return;
            }
            place = Picked("mp:");
            at = place != null ? _director.ManagedAt(place) : null;
            if (at != null)
            {
                WritePlaceRows(ms, at, ref selected);
                WritePlace(box, at);
                return;
            }
            string district;
            if (Split("rc:", out district, out rest) && _director.HasDistrict(district))
            {
                WriteOffices(district, ref selected);
                WriteRegionHand(box, district, rest);
                return;
            }
            district = Picked("rg:");
            if (district != null && _director.HasDistrict(district))
            {
                WriteOffices(district, ref selected);
                WriteRegion(box, district);
                return;
            }
            WriteManagerList(box, ms, ref selected);
        }

        // A pick of "prefix" + a + "*" + b.
        private bool Split(string prefix, out string a, out string b)
        {
            a = b = null;
            string all = Picked(prefix);
            int at = all != null ? all.LastIndexOf('*') : -1;
            if (at <= 0 || at >= all.Length - 1)
            {
                return false;
            }
            a = all.Substring(0, at);
            b = all.Substring(at + 1);
            return true;
        }

        private void Go(string pick)
        {
            _pick = pick;
            _table.ClearSelection();
            Refresh();
        }

        private static string TaskName(int task)
        {
            return CompanyText.T(TaskKeys[CompanyCatalogue.Clamp(task, 0, TaskKeys.Length - 1)]);
        }

        // --- The list ---

        private void WriteManagerList(InfoBox box, ManagerState ms, ref string selected)
        {
            _spec = CompanyTables.Managers;
            IReadOnlyList<string> districts = _director.ManagerDistricts;
            for (int i = 0; i < districts.Count; i++)
            {
                ManagerFile r = _director.RegionOf(districts[i]);
                Pick("rg:" + districts[i], CompanyText.T("reallife_mgr_row_region", "district", CompanyText.District(districts[i])),
                    r != null ? r.Name : CompanyText.T("reallife_mgr_none"), r != null ? r.Skill.ToString() : string.Empty,
                    r != null ? r.Decided.ToString() : string.Empty, r != null ? 0 : 1, ref selected);
                Last.Pinned = true;
                Last.Set(4, r != null ? CompanyText.T("reallife_mgr_in_post") : CompanyText.T("reallife_mgr_empty_post"));
            }
            IReadOnlyList<ManagedPlace> places = _director.Managed;
            int managed = 0, decided = 0, mistakes = 0, undone = 0, allowance = 0, last = 0, lastMistakes = 0;
            for (int i = 0; i < places.Count; i++)
            {
                ManagedPlace mp = places[i];
                ManagerFile m = mp.Own;
                string state = m == null ? CompanyText.T("reallife_mgr_empty_post")
                    : ManagerGame.Absent(ManagerGame.Employee(m.Id)) ? CompanyText.T("reallife_mgr_off_sick") : CompanyText.T("reallife_mgr_in_post");
                Pick("mp:" + mp.Place.Place, mp.Place.Name, m != null ? m.Name : CompanyText.T("reallife_mgr_none"), m != null ? m.Skill.ToString() : string.Empty,
                    m != null ? m.Decided.ToString() : string.Empty, m == null ? 0 : m.Mistakes > 0 ? 1 : 0, ref selected);
                TableRow row = Last;
                row.Key[2] = m != null ? m.Skill : -1;
                row.Key[3] = m != null ? m.Decided : -1;
                row.Set(4, state);
            }
            for (int i = 0; i < ms.Managers.Count; i++)
            {
                ManagerFile m = ms.Managers[i];
                managed++;
                decided += m.Decided;
                mistakes += m.Mistakes;
                undone += m.Undone;
                allowance += _director.AllowanceOf(m);
                last += m.LastDecided;
                lastMistakes += m.LastMistakes;
            }
            if (places.Count == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_places"));
            }
            box.Title(CompanyText.T("reallife_mgr_tab"));
            box.Chip(CompanyText.T("reallife_mgr_chip_count", "count", managed.ToString()), managed > 0 ? CellTone.Green : CellTone.Plain);
            box.Note(CompanyText.T("reallife_mgr_list_what"));
            box.Section("reallife_mgr_sec_month");
            box.RowPair("reallife_mgr_decided", decided.ToString(), "reallife_mgr_mistakes", mistakes.ToString());
            box.Row("reallife_mgr_undone", undone.ToString());
            box.Row("reallife_mgr_allowances", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(allowance)));
            box.Row("reallife_mgr_month_from", CompanyText.Day(ms.MonthFrom));
            if (last > 0)
            {
                box.Section("reallife_mgr_sec_last");
                box.RowPair("reallife_mgr_decided", last.ToString(), "reallife_mgr_mistakes", lastMistakes.ToString());
            }
            Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
        }

        // --- A place ---

        private void WritePlaceRows(ManagerState ms, ManagedPlace mp, ref string selected)
        {
            _spec = CompanyTables.ManagerPlace;
            ManagerFile m = mp.Own;
            int kinds = ManagerRules.Kinds(mp.Kind), blocked = _director.ManagerBlocked(mp);
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                if (!ManagerRules.Has(kinds, (ManagerTask)t))
                {
                    continue;
                }
                bool on = m != null && ManagerRules.Has(m.Tasks, (ManagerTask)t);
                string state = m == null ? CompanyText.T("reallife_mgr_none") : !on ? CompanyText.T("reallife_mgr_task_off")
                    : (blocked & (1 << t)) != 0 ? CompanyText.T("reallife_mgr_task_waits") : CompanyText.T("reallife_mgr_task_on");
                Pick("mk:" + mp.Place.Place + "*" + t, TaskName(t), m != null ? LimitText(m, (ManagerTask)t) : string.Empty, string.Empty, state,
                    on && (blocked & (1 << t)) != 0 ? 1 : 0, ref selected);
                Last.Pinned = true;
            }
            int shown = 0;
            for (int i = ms.Decisions.Count - 1; i >= 0 && shown < DecisionsShown; i--)
            {
                Decision d = ms.Decisions[i];
                if (d.Place != mp.Place.Place)
                {
                    continue;
                }
                shown++;
                Pick("md:" + d.Id, TaskName(d.Task), Summary(d), CompanyText.T("reallife_mgr_row_day", "day", d.Day.ToString()),
                    CompanyText.T(StateKeys[(int)d.State]) + (d.Mistake ? CompanyText.T("reallife_mgr_row_mistake") : string.Empty),
                    d.Mistake && d.Open ? 1 : 0, ref selected);
            }
            if (shown == 0 && m != null)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_decisions"));
            }
        }

        private static string LimitText(ManagerFile m, ManagerTask task)
        {
            switch (task)
            {
                case ManagerTask.Raises:
                case ManagerTask.Offers:
                    return CompanyText.T("reallife_mgr_limit_wage_value", "share", CompanyText.Share(ManagerCatalogue.Step(ManagerCatalogue.WageBands, m.WageBand)));
                case ManagerTask.Overtime:
                    return CompanyText.T("reallife_mgr_limit_budget_value", "amount", CompanyText.Money(CompanyDirector.Instance != null ? CompanyDirector.Instance.BudgetOf(m) : 0));
                case ManagerTask.Hiring:
                    return CompanyText.T("reallife_mgr_limit_keep_value", "count", Math.Max(0, m.Keep).ToString());
                case ManagerTask.Stock:
                    return CompanyText.T("reallife_mgr_limit_stock_value", "days", ManagerCatalogue.Step(ManagerCatalogue.StockDays, m.StockDays).ToString());
                case ManagerTask.Prices:
                    return CompanyText.T("reallife_mgr_limit_price_value", "low", CompanyText.Share(ManagerCatalogue.Step(ManagerCatalogue.PriceLows, m.PriceLow)),
                        "high", CompanyText.Share(ManagerCatalogue.Step(ManagerCatalogue.PriceHighs, m.PriceHigh)));
                default:
                    return string.Empty;
            }
        }

        private void WritePlace(InfoBox box, ManagedPlace mp)
        {
            ManagerFile m = mp.Own;
            string place = mp.Place.Place;
            box.Title(mp.Place.Name);
            box.Subtitle(CompanyText.T(KindKeys[(int)mp.Kind]) + ", " + CompanyText.District(mp.District));
            if (m == null)
            {
                box.Chip(CompanyText.T("reallife_mgr_empty_post"));
                box.Note(CompanyText.T("reallife_mgr_place_none"));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_mgr_appoint", () => Go("ma:" + place));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
                Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
                return;
            }
            bool sick = ManagerGame.Absent(ManagerGame.Employee(m.Id));
            box.Chip(sick ? CompanyText.T("reallife_mgr_off_sick") : CompanyText.T("reallife_mgr_in_post"), sick ? CellTone.Yellow : CellTone.Green);
            Transform top = box.ButtonRow();
            InfoBox.Button(top, InfoBox.Additional, "reallife_mgr_replace", () => Go("ma:" + place));
            InfoBox.Button(top, InfoBox.Negative, "reallife_mgr_stand_down", () => _director.ManagerStandDown(place, false));
            InfoBox.Button(top, InfoBox.Additional, "reallife_co_back", Back);
            box.Section("reallife_mgr_sec_manager");
            box.Row("reallife_mgr_name", m.Name);
            box.RowPair("reallife_mgr_skill", m.Skill.ToString(), "reallife_mgr_mistake_chance", CompanyText.Share(ManagerRules.MistakeChance(m.Skill, mp.Region != null ? mp.Region.Skill : 0)));
            box.Row("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(_director.AllowanceOf(m))));
            if (mp.Region != null)
            {
                box.Line(CompanyText.T(sick ? "reallife_mgr_place_stands_in" : "reallife_mgr_place_reviewed", "name", mp.Region.Name), sick ? CellTone.Yellow : CellTone.Plain);
            }
            else if (sick)
            {
                box.Line(CompanyText.T("reallife_mgr_place_waits"), CellTone.Yellow);
            }
            // A switch for every kind the place has, as the Mod settings app has them: off, the manager never does it.
            box.Section("reallife_mgr_sec_tasks");
            int kinds = ManagerRules.Kinds(mp.Kind), blocked = _director.ManagerBlocked(mp);
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                ManagerTask task = (ManagerTask)t;
                if (ManagerRules.Has(kinds, task))
                {
                    bool on = ManagerRules.Has(m.Tasks, task);
                    TaskSwitch(box, place, task, TaskKeys[t], on, on && (blocked & (1 << t)) != 0 && TaskWaitKeys[t] != null ? CompanyText.T(TaskWaitKeys[t]) : TaskNote(m, task));
                }
            }
            box.Note(CompanyText.T("reallife_mgr_tasks_what"));
            box.Section("reallife_mgr_sec_month");
            box.RowPair("reallife_mgr_decided", m.Decided.ToString(), "reallife_mgr_mistakes", m.Mistakes.ToString());
            box.RowPair("reallife_mgr_undone", m.Undone.ToString(), "reallife_mgr_spent",
                CompanyText.T("reallife_mgr_spent_value", "spent", CompanyText.Money(m.Spent), "budget", CompanyText.Money(_director.BudgetOf(m))));
            if (m.LastDecided > 0 || m.LastPaid > 0)
            {
                box.Line(CompanyText.Fill("reallife_mgr_place_last", "decisions", m.LastDecided.ToString(), "mistakes", m.LastMistakes.ToString(),
                    "undone", m.LastUndone.ToString(), "amount", CompanyText.Money(m.LastPaid)));
            }
            if (m.Held.Count > 0)
            {
                box.Line(CompanyText.T("reallife_mgr_place_held", "count", m.Held.Count.ToString()), CellTone.Yellow);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_mgr_hand_back", () => _director.ManagerHandBack(place));
            }
            box.Note(CompanyText.T("reallife_mgr_place_pick"));
        }

        private void WriteTask(InfoBox box, ManagedPlace mp, ManagerTask task)
        {
            ManagerFile m = mp.Own;
            string place = mp.Place.Place;
            box.Title(TaskName((int)task));
            box.Subtitle(mp.Place.Name);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("mp:" + place));
            box.Note(CompanyText.T(TaskWhatKeys[(int)task]));
            if (m == null)
            {
                box.Line(CompanyText.T("reallife_mgr_task_no_manager"), CellTone.Yellow);
                return;
            }
            if ((_director.ManagerBlocked(mp) & ManagerRules.Bit(task)) != 0 && TaskWaitKeys[(int)task] != null)
            {
                box.Line(CompanyText.T(TaskWaitKeys[(int)task]), CellTone.Yellow);
            }
            TaskSwitch(box, place, task, "reallife_mgr_task_given", ManagerRules.Has(m.Tasks, task), TaskNote(m, task));
            switch (task)
            {
                case ManagerTask.Raises:
                case ManagerTask.Offers:
                    Limit(box, "reallife_mgr_limit_wage", place, CompanyDirector.LimitWage, ManagerCatalogue.WageBands.Length, m.WageBand,
                        i => CompanyText.T("reallife_mgr_limit_wage_value", "share", CompanyText.Share(ManagerCatalogue.WageBands[i])));
                    break;
                case ManagerTask.Overtime:
                    BudgetLimit(box, place, m);
                    break;
                case ManagerTask.Hiring:
                    BudgetLimit(box, place, m);
                    Limit(box, "reallife_mgr_limit_keep", place, CompanyDirector.LimitKeep, ManagerCatalogue.KeepMost + 1, Math.Max(0, m.Keep),
                        i => CompanyText.T("reallife_mgr_limit_keep_value", "count", i.ToString()));
                    break;
                case ManagerTask.Stock:
                    Limit(box, "reallife_mgr_limit_stock", place, CompanyDirector.LimitStock, ManagerCatalogue.StockDays.Length, m.StockDays,
                        i => CompanyText.T("reallife_mgr_limit_stock_value", "days", ManagerCatalogue.StockDays[i].ToString()));
                    break;
                case ManagerTask.Prices:
                    Limit(box, "reallife_mgr_limit_price_low", place, CompanyDirector.LimitPriceLow, ManagerCatalogue.PriceLows.Length, m.PriceLow,
                        i => CompanyText.Share(ManagerCatalogue.PriceLows[i]));
                    Limit(box, "reallife_mgr_limit_price_high", place, CompanyDirector.LimitPriceHigh, ManagerCatalogue.PriceHighs.Length, m.PriceHigh,
                        i => CompanyText.Share(ManagerCatalogue.PriceHighs[i]));
                    break;
            }
        }

        // One kind's switch: the Mod settings app's own (InfoBox.Toggle), else the box's tick switch, else a button that does the same.
        private void TaskSwitch(InfoBox box, string place, ManagerTask task, string labelKey, bool on, string note)
        {
            Action<bool> set = v => _director.ManagerTaskSet(place, task, v);
            if (box.Toggle(labelKey, note, on, set) || box.Switch(labelKey, on, set))
            {
                return;
            }
            InfoBox.Button(box.ButtonRow(), on ? InfoBox.Negative : InfoBox.Positive, on ? "reallife_mgr_task_take" : "reallife_mgr_task_give", () => set(!on));
        }

        // The line under a kind's switch: its limit, or for the rota when it is written.
        private static string TaskNote(ManagerFile m, ManagerTask task)
        {
            return task == ManagerTask.Rota ? CompanyText.T("reallife_mgr_task_rota_when", "hour", ManagerCatalogue.RotaHour.ToString("00")) : LimitText(m, task);
        }

        private void BudgetLimit(InfoBox box, string place, ManagerFile m)
        {
            int index = _director.Index;
            Limit(box, "reallife_mgr_limit_budget", place, CompanyDirector.LimitBudget, ManagerCatalogue.Budgets.Length, m.Budget,
                i => CompanyText.T("reallife_mgr_limit_budget_value", "amount", CompanyText.Money(CompanyCatalogue.AtPrices(ManagerCatalogue.Budgets[i], index))));
        }

        private void Limit(InfoBox box, string key, string place, int limit, int count, int index, Func<int, string> text)
        {
            box.Headline(key);
            if (!box.HasSelector || !box.Selector(count, index, text, i => _director.ManagerLimit(place, limit, i)))
            {
                box.Line(text(CompanyCatalogue.Clamp(index, 0, count - 1)));
            }
        }

        // --- A decision ---

        private void WriteDecision(InfoBox box, Decision d, ManagedPlace mp)
        {
            string place = mp.Place.Place;
            int id = d.Id;
            box.Title(TaskName(d.Task));
            box.Subtitle(CompanyText.Fill("reallife_mgr_dec_when", "place", mp.Place.Name, "day", d.Day.ToString(), "hour", d.Hour.ToString("00"), "name", d.Who));
            box.Chip(CompanyText.T(StateKeys[(int)d.State]), d.State == DecisionState.Done ? CellTone.Green : d.State == DecisionState.Closed ? CellTone.Plain : CellTone.Yellow);
            if (d.Mistake)
            {
                box.Chip(CompanyText.T("reallife_mgr_chip_mistake"), CellTone.Red);
            }
            string why;
            int state = _director.UndoState(d, _director.Today, out why);
            Transform buttons = box.ButtonRow();
            if (state == 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_mgr_undo", () => _director.ManagerUndo(id));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go("mp:" + place));
            box.Line(Summary(d));
            if (d.Task == (int)ManagerTask.Stock || d.Task == (int)ManagerTask.Prices)
            {
                ManagerStore.Items(d.Detail, _shownItems);
                for (int i = 0; i < _shownItems.Count && i < 6; i++)
                {
                    bool money = d.Task == (int)ManagerTask.Prices;
                    box.RowWords(ProductName(_shownItems[i].Item), CompanyText.T("reallife_mgr_dec_from_to",
                        "before", money ? CompanyText.Money(_shownItems[i].Before / 100f) : _shownItems[i].Before.ToString(),
                        "after", money ? CompanyText.Money(_shownItems[i].After / 100f) : _shownItems[i].After.ToString()));
                }
                if (_shownItems.Count > 6)
                {
                    box.Note(CompanyText.T("reallife_co_over_more", "count", (_shownItems.Count - 6).ToString()));
                }
            }
            if (d.Mistake)
            {
                box.Note(CompanyText.T("reallife_mgr_dec_mistake"));
            }
            if (state != 0 && why != null && d.State != DecisionState.Undone)
            {
                box.Note(CompanyText.T(why));
            }
            else if (state == 0)
            {
                box.Note(CompanyText.T("reallife_mgr_dec_undo_what"));
            }
        }

        private static string ProductName(string item)
        {
            return LocalizorManager.IsLocalizedKey(item) ? item.GetLocalization() : item;
        }

        // The decision in one line, as the log and the table say it.
        private static string Summary(Decision d)
        {
            string name = d.Detail, rival = string.Empty;
            int star = d.Detail.IndexOf('*');
            if (star >= 0)
            {
                name = d.Detail.Substring(0, star);
                rival = d.Detail.Substring(star + 1);
            }
            switch ((ManagerTask)d.Task)
            {
                case ManagerTask.Raises:
                    return CompanyText.T(d.F == (int)RaiseAnswer.Agree ? "reallife_mgr_did_raise_agree" : d.F == (int)RaiseAnswer.Less ? "reallife_mgr_did_raise_less"
                        : "reallife_mgr_did_raise_refuse", "name", name, "raise", CompanyText.Wage(d.A));
                case ManagerTask.Offers:
                    return CompanyText.T(d.F == 1 ? "reallife_mgr_did_offer_match" : "reallife_mgr_did_offer_go", "name", name, "rival", rival, "wage", CompanyText.Wage(d.D));
                case ManagerTask.Overtime:
                    return CompanyText.T(d.A == 1 ? "reallife_mgr_did_overtime_agree" : "reallife_mgr_did_overtime_refuse", "amount", CompanyText.Money(d.B));
                case ManagerTask.Claims:
                    return CompanyText.T(d.D == 1 ? "reallife_mgr_did_claim_put" : "reallife_mgr_did_claim_left", "cover", name, "amount", CompanyText.Money(d.C),
                        "adds", CompanyText.Money(d.E));
                case ManagerTask.Stock:
                    return CompanyText.T("reallife_mgr_did_stock", "count", d.B.ToString(), "day", d.A.ToString());
                case ManagerTask.Prices:
                    return CompanyText.T("reallife_mgr_did_prices", "count", d.A.ToString());
                case ManagerTask.Rota:
                    return d.A == -1 ? CompanyText.T("reallife_mgr_did_rota_missed") : d.A == -2 ? CompanyText.T("reallife_mgr_did_rota_unwritten")
                        : d.A == -3 ? CompanyText.T("reallife_mgr_did_rota_kept_out", "count", d.B.ToString())
                        : d.A == -4 ? CompanyText.T("reallife_mgr_did_rota_kept_less", "after", d.B.ToString(), "wanted", d.C.ToString(), "now", d.D.ToString())
                        : CompanyText.T("reallife_mgr_did_rota", "shifts", d.A.ToString(), "covered", d.B.ToString(), "wanted", d.C.ToString());
                case ManagerTask.Hiring:
                    return CompanyText.T("reallife_mgr_did_hire", "name", name, "skill", d.B.ToString(), "wage", CompanyText.Wage(d.A));
                default:
                    return string.Empty;
            }
        }

        // --- Appointing ---

        private void WriteStaffToAppoint(ManagedPlace mp, ref string selected)
        {
            _spec = CompanyTables.ManagerHands;
            ManagerGame.StaffAt(mp.Place, _director.Index, _hands);
            for (int i = 0; i < _hands.Count; i++)
            {
                ManagerHand h = _hands[i];
                bool now = mp.Own != null && mp.Own.Id == h.Id;
                Pick("mc:" + mp.Place.Place + "*" + h.Id, h.Name, CompanyGame.SkillName(h.Id), h.Skill.ToString(), CompanyText.Wage(h.Wage), 0, ref selected);
                Last.Key[2] = h.Skill;
                Last.Key[3] = h.Wage / 100.0;
                Last.Set(4, now ? CompanyText.T("reallife_mgr_in_post") : string.Empty);
            }
            if (_hands.Count == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_staff"));
            }
        }

        private void WriteAppoint(InfoBox box, ManagedPlace mp)
        {
            string place = mp.Place.Place;
            box.Title(CompanyText.T("reallife_mgr_appoint_title"));
            box.Subtitle(mp.Place.Name);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("mp:" + place));
            box.Note(CompanyText.T("reallife_mgr_appoint_what"));
            box.Note(CompanyText.T("reallife_mgr_appoint_hire"));
        }

        private void WriteHand(InfoBox box, ManagedPlace mp, string id)
        {
            string place = mp.Place.Place;
            ManagerHand h = default(ManagerHand);
            bool found = false;
            for (int i = 0; i < _hands.Count && !found; i++)
            {
                found = _hands[i].Id == id;
                h = found ? _hands[i] : h;
            }
            if (!found)
            {
                WriteAppoint(box, mp);
                return;
            }
            box.Title(h.Name);
            box.Subtitle(CompanyText.T("reallife_co_emp_works", "business", mp.Place.Name, "skill", CompanyGame.SkillName(h.Id)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_mgr_appoint", () =>
            {
                _director.ManagerAppoint(place, id);
                Go("mp:" + place);
            });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go("ma:" + place));
            box.Section("reallife_mgr_sec_manager");
            box.RowPair("reallife_mgr_skill", h.Skill.ToString(), "reallife_mgr_mistake_chance", CompanyText.Share(ManagerRules.MistakeChance(h.Skill, mp.Region != null ? mp.Region.Skill : 0)));
            box.Row("reallife_co_emp_wage", CompanyText.Wage(h.Wage));
            box.Row("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount",
                CompanyText.Money(ManagerRules.Allowance(ManagerCatalogue.RolePlace, h.Skill, _director.Index))));
            if (mp.Own != null && mp.Own.Id != h.Id)
            {
                box.Note(CompanyText.T("reallife_mgr_appoint_replaces", "name", mp.Own.Name));
            }
        }

        // --- Districts ---

        private void WriteOffices(string district, ref string selected)
        {
            _spec = CompanyTables.ManagerHands;
            ManagerGame.Offices(_director.ManagerPlaces, _director.Index, _hands);
            ManagerFile r = _director.RegionOf(district);
            for (int i = 0; i < _hands.Count; i++)
            {
                ManagerHand h = _hands[i];
                Pick("rc:" + district + "*" + h.Id, h.Name, h.PlaceName, h.Skill.ToString(), CompanyText.Wage(h.Wage), 0, ref selected);
                Last.Key[2] = h.Skill;
                Last.Key[3] = h.Wage / 100.0;
                Last.Set(4, r != null && r.Id == h.Id ? CompanyText.T("reallife_mgr_in_post") : string.Empty);
            }
            if (_hands.Count == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_office"));
            }
        }

        private void WriteRegion(InfoBox box, string district)
        {
            ManagerFile r = _director.RegionOf(district);
            box.Title(CompanyText.T("reallife_mgr_row_region", "district", CompanyText.District(district)));
            Transform buttons = box.ButtonRow();
            if (r != null)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_mgr_stand_down", () => _director.ManagerStandDown(district, true));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            box.Note(CompanyText.T("reallife_mgr_region_what"));
            if (r == null)
            {
                box.Chip(CompanyText.T("reallife_mgr_empty_post"));
                box.Note(CompanyText.T("reallife_mgr_region_pick"));
                return;
            }
            box.Chip(CompanyText.T("reallife_mgr_in_post"), CellTone.Green);
            box.Section("reallife_mgr_sec_manager");
            box.Row("reallife_mgr_name", r.Name);
            box.Row("reallife_mgr_skill", r.Skill.ToString());
            box.Row("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(_director.AllowanceOf(r))));
            int places = 0;
            IReadOnlyList<ManagedPlace> all = _director.Managed;
            for (int i = 0; i < all.Count; i++)
            {
                places += all[i].District == district && all[i].Own != null ? 1 : 0;
            }
            box.Row("reallife_mgr_region_places", places.ToString());
            box.Note(CompanyText.T("reallife_mgr_region_pick"));
        }

        private void WriteRegionHand(InfoBox box, string district, string id)
        {
            ManagerHand h = default(ManagerHand);
            bool found = false;
            for (int i = 0; i < _hands.Count && !found; i++)
            {
                found = _hands[i].Id == id;
                h = found ? _hands[i] : h;
            }
            if (!found)
            {
                WriteRegion(box, district);
                return;
            }
            box.Title(h.Name);
            box.Subtitle(CompanyText.T("reallife_co_emp_works", "business", h.PlaceName, "skill", CompanyGame.SkillName(h.Id)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_mgr_appoint", () =>
            {
                _director.ManagerAppointRegion(district, id);
                Go("rg:" + district);
            });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go("rg:" + district));
            box.Section("reallife_mgr_sec_manager");
            box.Row("reallife_mgr_skill", h.Skill.ToString());
            box.Row("reallife_co_emp_wage", CompanyText.Wage(h.Wage));
            box.Row("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount",
                CompanyText.Money(ManagerRules.Allowance(ManagerCatalogue.RoleRegion, h.Skill, _director.Index))));
            box.Note(CompanyText.T("reallife_mgr_region_what"));
        }
    }
}
