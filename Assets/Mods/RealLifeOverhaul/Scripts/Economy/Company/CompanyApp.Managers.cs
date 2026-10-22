using System;
using System.Collections.Generic;
using Extensions;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Managers tab: a directory of the managers (CompanyApp.ManagerGroups: the groups, the regional managers by
    /// district, each kind of place's managers, the places with none and what needs the player), a manager's own panel with
    /// a switch for each kind of decision and its log, a decision with its undo, a district's regional manager with the
    /// places they look over, and the staff to appoint from. The table and the white box as the other tabs have them; a page
    /// is written when the tab opens, a row is picked or something changes, never per frame. The picks:
    ///   "mg:group" a group of the directory; "mp:place" a place; "mk:place*kind" one of its kinds; "md:id" a decision;
    ///   "ma:place" its staff to appoint from; "mc:place*id" one of them; "rg:district" a district's regional manager and
    ///   its places; "rh:district" their decisions standing in; "ra:district" who could be; "rc:district*id" one of them.
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
            _director.ManagerLook(_lines, _linePool, _counts);   // every place's post as it stands, read once for this page
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
                // Only a kind the place has now: one that became a factory keeps no business's kinds to switch.
                if (mp != null && int.TryParse(rest, out task) && task >= 0 && task < (int)ManagerTask.Count
                    && ManagerRules.Has(ManagerRules.Kinds(mp.Kind), (ManagerTask)task))
                {
                    WritePlaceRows(ms, mp, ref selected);
                    WriteTask(box, mp, (ManagerTask)task);
                    return;
                }
                _pick = mp != null ? "mp:" + mp.Place.Place : null;
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
            district = Picked("ra:");
            if (district != null && _director.HasDistrict(district))
            {
                WriteOffices(district, ref selected);
                WriteAppointRegion(box, district);
                return;
            }
            district = Picked("rh:");
            if (district != null && _director.HasDistrict(district))
            {
                _mgrRegion = "rg:" + district;
                WriteRegionLog(ms, district, ref selected);
                WriteRegion(box, district);
                return;
            }
            district = Picked("rg:");
            if (district != null && _director.HasDistrict(district))
            {
                _mgrRegion = _pick;
                WriteRegionPlaces(district, ref selected);
                WriteRegion(box, district);
                return;
            }
            ManagerGroup group;
            if (GroupPicked(out group))
            {
                WriteGroup(box, ms, group, ref selected);
                return;
            }
            WriteDirectory(box, ms, ref selected);
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

        private static string KindName(ManagedKind kind)
        {
            return CompanyText.T(KindKeys[CompanyCatalogue.Clamp((int)kind, 0, KindKeys.Length - 1)]);
        }

        // --- A place ---

        private void WritePlaceRows(ManagerState ms, ManagedPlace mp, ref string selected)
        {
            _spec = CompanyTables.ManagerPlace;
            ManagerFile m = mp.Own;
            if (!string.IsNullOrEmpty(mp.District))
            {
                // The district's regional manager, a click away; their page lists this place and comes back to it.
                ManagerFile r = mp.Region;
                Pick("rg:" + mp.District, CompanyText.T("reallife_mgr_row_region", "district", CompanyText.District(mp.District)), r != null ? r.Name : CompanyText.T("reallife_mgr_none"),
                    string.Empty, RegionState(r), CompanyDirector.Sick(r) ? 1 : 0, ref selected);
                Last.Pinned = true;
            }
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
                Last.Set(4, d.Who);
            }
            if (shown == 0 && m != null)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_decisions"));
            }
        }

        // A district's regional post in a cell: who holds it and whether they are at work today.
        private static string RegionState(ManagerFile r)
        {
            return r == null ? CompanyText.T("reallife_mgr_empty_post") : CompanyDirector.Sick(r) ? CompanyText.T("reallife_mgr_off_sick") : CompanyText.T("reallife_mgr_in_post");
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
            box.Subtitle(KindName(mp.Kind) + ", " + CompanyText.District(mp.District));
            if (m == null)
            {
                box.Chip(CompanyText.T("reallife_mgr_empty_post"));
                box.Note(CompanyText.T("reallife_mgr_place_none"));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_mgr_appoint", () => Go("ma:" + place));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go(PlaceBack(mp)));
                Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
                return;
            }
            // How the post stands today, as the decisions are made (ManagerRules.Decider): who decides, and how well.
            PostState post = _director.PostOf(mp);
            box.Chip(post == PostState.InPost ? CompanyText.T("reallife_mgr_in_post") : CompanyText.T("reallife_mgr_off_sick"), post == PostState.InPost ? CellTone.Green : CellTone.Yellow);
            Transform top = box.ButtonRow();
            InfoBox.Button(top, InfoBox.Additional, "reallife_mgr_replace", () => Go("ma:" + place));
            InfoBox.Button(top, InfoBox.Negative, "reallife_mgr_stand_down", () => _director.ManagerStandDown(place, false));
            InfoBox.Button(top, InfoBox.Additional, "reallife_co_back", () => Go(PlaceBack(mp)));
            box.Section("reallife_mgr_sec_manager");
            box.RowPair("reallife_mgr_name", m.Name, "reallife_mgr_since", CompanyText.Day(m.From));
            int chance = _director.ChanceToday(mp);
            box.RowPair("reallife_mgr_skill", m.Skill.ToString(), "reallife_mgr_mistake_chance", chance >= 0 ? CompanyText.Share(chance) : CompanyText.T("reallife_mgr_chance_waits"));
            box.RowPair("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(_director.AllowanceOf(m))), "reallife_mgr_pay",
                CompanyText.Wage(_director.PayOf(m)));
            if (post == PostState.Covered)
            {
                box.Line(CompanyText.T("reallife_mgr_place_stands_in", "name", mp.Region.Name), CellTone.Yellow);
            }
            else if (post == PostState.Uncovered)
            {
                box.Line(CompanyText.T("reallife_mgr_place_waits"), CellTone.Yellow);
            }
            else if (mp.Region != null)
            {
                bool away = CompanyDirector.Sick(mp.Region);   // off sick, they look over nothing today (ManagerRules.Decider)
                box.Line(CompanyText.T(away ? "reallife_mgr_place_review_off" : "reallife_mgr_place_reviewed", "name", mp.Region.Name), away ? CellTone.Yellow : CellTone.Plain);
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
            int review = mp.Region != null && !CompanyDirector.Sick(mp.Region) ? mp.Region.Skill : 0;   // a regional manager off sick reviews nothing
            box.RowPair("reallife_mgr_skill", h.Skill.ToString(), "reallife_mgr_mistake_chance", CompanyText.Share(ManagerRules.MistakeChance(h.Skill, review)));
            box.Row("reallife_co_emp_wage", CompanyText.Wage(h.Wage));
            box.Row("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount",
                CompanyText.Money(ManagerRules.Allowance(ManagerCatalogue.RolePlace, h.Skill, _director.Index))));
            if (mp.Own != null && mp.Own.Id != h.Id)
            {
                box.Note(CompanyText.T("reallife_mgr_appoint_replaces", "name", mp.Own.Name));
            }
        }

        // --- Districts ---

        // Who at a headquarters could be a district's regional manager; one who already is of another district says which.
        private void WriteOffices(string district, ref string selected)
        {
            _spec = CompanyTables.ManagerHands;
            ManagerGame.Offices(_director.ManagerPlaces, _director.Index, _hands);
            ManagerFile r = _director.RegionOf(district);
            for (int i = 0; i < _hands.Count; i++)
            {
                ManagerHand h = _hands[i];
                string holds = _director.RegionHeldBy(h.Id);
                Pick("rc:" + district + "*" + h.Id, h.Name, h.PlaceName, h.Skill.ToString(), CompanyText.Wage(h.Wage), 0, ref selected);
                Last.Key[2] = h.Skill;
                Last.Key[3] = h.Wage / 100.0;
                Last.Set(4, r != null && r.Id == h.Id ? CompanyText.T("reallife_mgr_in_post")
                    : holds != null ? CompanyText.T("reallife_mgr_row_region", "district", CompanyText.District(holds)) : string.Empty);
            }
            if (_hands.Count == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_office"));
            }
        }

        // A district's places, each with its manager and who stands in for one off sick today; a click opens the place.
        private void WriteRegionPlaces(string district, ref string selected)
        {
            _spec = CompanyTables.ManagerRegion;
            ManagerFile r = _director.RegionOf(district);
            int rows = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                ManagerLine line = _lines[i];
                ManagedPlace mp = line.Mp;
                if (!string.Equals(mp.District, district, StringComparison.Ordinal))
                {
                    continue;
                }
                rows++;
                Pick("mp:" + mp.Place.Place, mp.Place.Name, mp.Own != null ? mp.Own.Name : CompanyText.T("reallife_mgr_none"), KindName(mp.Kind),
                    PostWords(line.State, r), line.State == PostState.Uncovered ? 1 : 0, ref selected);
            }
            if (rows == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_places"));
            }
        }

        // A post's state in words, naming who stands in.
        private static string PostWords(PostState state, ManagerFile region)
        {
            switch (state)
            {
                case PostState.InPost: return CompanyText.T("reallife_mgr_in_post");
                case PostState.Covered: return CompanyText.T("reallife_mgr_post_covered", "name", region != null ? region.Name : "?");
                case PostState.Uncovered: return CompanyText.T("reallife_mgr_post_uncovered");
                default: return CompanyText.T("reallife_mgr_empty_post");
            }
        }

        // A regional manager's own decisions, made standing in at the district's places, newest first.
        private void WriteRegionLog(ManagerState ms, string district, ref string selected)
        {
            _spec = CompanyTables.ManagerLog;
            ManagerFile r = _director.RegionOf(district);
            int shown = 0;
            for (int i = ms.Decisions.Count - 1; r != null && i >= 0 && shown < DecisionsShown; i--)
            {
                Decision d = ms.Decisions[i];
                ManagedPlace mp = _director.ManagedAt(d.Place);
                if (mp == null || !string.Equals(mp.District, district, StringComparison.Ordinal)
                    || !ManagerDirectoryRules.ByRegion(d.Who, mp.Own != null ? ManagerStore.Clean(mp.Own.Name) : null, ManagerStore.Clean(r.Name)))
                {
                    continue;
                }
                shown++;
                Pick("md:" + d.Id, TaskName(d.Task), Summary(d), CompanyText.T("reallife_mgr_row_day", "day", d.Day.ToString()),
                    CompanyText.T(StateKeys[(int)d.State]) + (d.Mistake ? CompanyText.T("reallife_mgr_row_mistake") : string.Empty),
                    d.Mistake && d.Open ? 1 : 0, ref selected);
                Last.Set(4, mp.Place.Name);
            }
            if (shown == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_region_no_log"));
            }
        }

        private void WriteRegion(InfoBox box, string district)
        {
            ManagerFile r = _director.RegionOf(district);
            bool sick = CompanyDirector.Sick(r);
            box.Title(CompanyText.T("reallife_mgr_row_region", "district", CompanyText.District(district)));
            box.Chip(RegionState(r), r == null ? CellTone.Plain : sick ? CellTone.Yellow : CellTone.Green);
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, r == null ? InfoBox.Positive : InfoBox.Additional, r == null ? "reallife_mgr_appoint" : "reallife_mgr_replace", () => Go("ra:" + district));
            if (r != null)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_mgr_stand_down", () => _director.ManagerStandDown(district, true));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go(_mgrGroup ?? GroupPick(ManagerGroup.Regional)));
            if (r == null)
            {
                box.Note(CompanyText.T("reallife_mgr_region_empty"));
                box.Note(CompanyText.T("reallife_mgr_region_what"));
                Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
                return;
            }
            box.Section("reallife_mgr_sec_manager");
            box.RowPair("reallife_mgr_name", r.Name, "reallife_mgr_since", CompanyText.Day(r.From));
            box.RowPair("reallife_mgr_skill", r.Skill.ToString(), "reallife_mgr_standing_skill", ManagerRules.FromAfar(r.Skill).ToString());
            box.RowPair("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(_director.AllowanceOf(r))), "reallife_mgr_pay",
                CompanyText.Wage(_director.PayOf(r)));
            int places = 0;
            IReadOnlyList<ManagedPlace> all = _director.Managed;
            for (int i = 0; i < all.Count; i++)
            {
                places += all[i].District == district && all[i].Own != null ? 1 : 0;
            }
            box.Section("reallife_mgr_sec_district");
            box.RowPair("reallife_mgr_region_places", places.ToString(), "reallife_mgr_standing_in", _director.StandingIn(district).ToString());
            box.RowPair("reallife_mgr_decided_standing", r.Decided.ToString(), "reallife_mgr_mistakes", r.Mistakes.ToString());
            if (sick)
            {
                box.Line(CompanyText.T("reallife_mgr_region_sick"), CellTone.Yellow);
            }
            bool log = _pick != null && _pick.StartsWith("rh:", StringComparison.Ordinal);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, log ? "reallife_mgr_region_their_places" : "reallife_mgr_region_their_log",
                () => Go((log ? "rg:" : "rh:") + district));
            box.Note(CompanyText.T("reallife_mgr_region_what"));
        }

        private void WriteAppointRegion(InfoBox box, string district)
        {
            box.Title(CompanyText.T("reallife_mgr_appoint_title"));
            box.Subtitle(CompanyText.T("reallife_mgr_row_region", "district", CompanyText.District(district)));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("rg:" + district));
            box.Note(CompanyText.T("reallife_mgr_region_what"));
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
                WriteAppointRegion(box, district);
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
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go("ra:" + district));
            box.Section("reallife_mgr_sec_manager");
            box.Row("reallife_mgr_skill", h.Skill.ToString());
            box.Row("reallife_co_emp_wage", CompanyText.Wage(h.Wage));
            box.Row("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount",
                CompanyText.Money(ManagerRules.Allowance(ManagerCatalogue.RoleRegion, h.Skill, _director.Index))));
            string holds = _director.RegionHeldBy(h.Id);
            if (holds != null && holds != district)
            {
                box.Line(CompanyText.T("reallife_mgr_region_moves", "district", CompanyText.District(holds)), CellTone.Yellow);
            }
            ManagerFile r = _director.RegionOf(district);
            if (r != null && r.Id != h.Id)
            {
                box.Note(CompanyText.T("reallife_mgr_appoint_replaces", "name", r.Name));
            }
            box.Note(CompanyText.T("reallife_mgr_region_what"));
        }
    }
}
