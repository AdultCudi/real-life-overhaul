using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    // A rule of the policy centre is the Economy's, not the root namespace's of the same name.
    using PolicyDef = RealLifeOverhaul.Economy.PolicyDef;
    using PolicyField = RealLifeOverhaul.Economy.PolicyField;

    internal sealed partial class CompanyLive
    {
        private const int ListShown = 40, StepsAsChoice = 5;

        private readonly List<ManagerHand> _hands = new List<ManagerHand>(16);

        // The pages inside the Managers tab: who to appoint, a manager's log and its decisions, a kind's limits, the Deep Mode inbox, policy centre and templates, the
        // dashboards of a store and a region, and the seats. Each is a list at the left and a card at the right, from the subject the tree had picked. False when the
        // subject is gone (a manager stood down, a seat emptied): the tab falls back to its own page.
        private bool ReadManagersSub(CompanyManagersFacts m, CompanyDirector d, CompanyView v, Words w)
        {
            int sub = v.Sub[CompanyView.Managers];
            string pick = v.Pick[CompanyView.Managers] ?? string.Empty;
            var page = new CompanyPage();
            bool ok;
            switch (sub)
            {
                case CompanyView.SubAppoint: ok = AppointPage(page, d, v, w, pick); break;
                case CompanyView.SubDecision: ok = DecisionsPage(page, d, v, w, pick); break;
                case CompanyView.SubTask: ok = TaskPage(page, d, v, w, pick); break;
                case CompanyView.SubInbox: ok = d.HierarchyOn && InboxPage(page, d, v, w); break;
                case CompanyView.SubPolicy: ok = d.HierarchyOn && PolicyPage(page, d, v, w, pick, m); break;
                case CompanyView.SubTemplates: ok = d.HierarchyOn && TemplatesPage(page, d, v, w, pick); break;
                case CompanyView.SubDashboard: ok = d.HierarchyOn && DashboardPage(page, d, v, w, pick); break;
                case CompanyView.SubRegion: ok = d.HierarchyOn && RegionPage(page, d, v, w, pick); break;
                case CompanyView.SubSeat: ok = d.HierarchyOn && SeatPage(page, d, v, w, pick); break;
                default: ok = false; break;
            }
            if (!ok)
            {
                v.Sub[CompanyView.Managers] = CompanyView.SubMain;
                v.SubPick[CompanyView.Managers] = string.Empty;
                return false;
            }
            m.Sub = page;
            return true;
        }

        private static CompanyButton[] BackTo(Words w, string pick, int sub = CompanyView.SubMain)
        {
            return new[] { Btn(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, pick, CompanyView.Managers, sub), UiAction.Count, UiIcon.Back) };
        }

        private static int ChosenRow(CompanyRow[] rows, string id)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i].Caption && rows[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        // ------------------------------------------------------------------------------------------------- appoint

        private bool AppointPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            bool region = pick.StartsWith("reg:", StringComparison.Ordinal);
            string key = pick.IndexOf(':') >= 0 ? pick.Substring(pick.IndexOf(':') + 1) : string.Empty;
            ManagedPlace mp = region ? null : d.ManagedAt(key);
            if (region ? !d.HasArea(key) : mp == null)
            {
                return false;
            }
            string back = region ? pick : (mp.Own != null ? "mgr:" + key : "post:" + key);
            if (region)
            {
                ManagerGame.Offices(d.ManagerPlaces, d.Index, _hands);
            }
            else
            {
                ManagerGame.StaffAt(mp.Place, d.Index, _hands);
            }
            ManagerFile now = region ? d.RegionOf(key) : mp.Own;
            var rows = new CompanyRow[_hands.Count];
            for (int i = 0; i < _hands.Count; i++)
            {
                ManagerHand h = _hands[i];
                Face(h.Name, h.Id);
                string holds = region ? d.RegionHeldBy(h.Id) : null;
                rows[i] = new CompanyRow
                {
                    Id = "mc:" + h.Id, Title = h.Name, Sub = region ? h.PlaceName : CompanyGame.SkillName(h.Id), Face = true, Hue = UiColour.HueOf(h.Name), Ring = UiRole.None,
                    Figure = CompanyText.Wage(h.Wage), Note = w.F("reallife_co_skill_n", "skill {n}", "n", h.Skill.ToString()),
                    Chip = now != null && now.Id == h.Id ? CompanyText.T("reallife_mgr_in_post") : holds != null ? CompanyText.T("reallife_mgr_row_region", "district", CompanyDirector.AreaName(holds)) : null,
                    ChipTone = UiRole.Ok
                };
            }
            string title = region ? CompanyText.T("reallife_mgr_row_region", "district", CompanyDirector.AreaName(key)) : mp.Place.Name;
            page.List = new CompanyList
            {
                Title = w.T("reallife_co_candidates", "Who could do it"), Count = rows.Length.ToString(), Rows = rows, Empty = CompanyText.T(region ? "reallife_mgr_row_no_office" : "reallife_mgr_row_no_staff")
            };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(rows, chosen);
            ManagerHand? hand = null;
            for (int i = 0; i < _hands.Count; i++)
            {
                if (page.List.Chosen == i)
                {
                    hand = _hands[i];
                }
            }
            var card = new CompanyCard { Kicker = w.T("reallife_mgr_appoint_title", "Appoint a manager"), Title = title, Back = BackTo(w, back) };
            var blocks = new List<CompanyBlock>(10);
            // What can be done with the post as it stands: stand the manager down, and hand back what they hold.
            var post = new List<CompanyButton>(2);
            if (now != null)
            {
                post.Add(Btn(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.StandDown, key, region ? 1 : 0), UiAction.Count, UiIcon.Close, true));
                if (!region && now.Held.Count > 0)
                {
                    post.Add(Btn(w.T("reallife_mgr_hand_back", "Hand back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.HandBack, key), UiAction.Count, UiIcon.Back));
                }
            }
            if (hand == null)
            {
                card.Icon = region ? UiIcon.MapTrifold : UiIcon.Store;
                if (now != null)
                {
                    card.Face = true;
                    card.Hue = UiColour.HueOf(now.Name);
                    card.Sub = w.F("reallife_co_now_in_post", "{name} is in post", "name", now.Name);
                    card.Chips = new[] { CompanyText.T(CompanyDirector.Sick(now) ? "reallife_mgr_off_sick" : "reallife_mgr_in_post") };
                    card.ChipTones = new[] { CompanyDirector.Sick(now) ? UiRole.Warn : UiRole.Ok };
                    Face(now.Name, now.Id);
                }
                blocks.Add(Note(CompanyText.T(region ? "reallife_mgr_region_what" : "reallife_mgr_appoint_what")));
                blocks.Add(Note(CompanyText.T(region ? "reallife_mgr_region_pick" : "reallife_mgr_appoint_hire")));
                if (post.Count > 0)
                {
                    blocks.Add(Buttons(post.ToArray()));
                }
            }
            else
            {
                ManagerHand h = hand.Value;
                card = new CompanyCard
                {
                    Kicker = title, Title = h.Name, Sub = CompanyText.T("reallife_co_emp_works", "business", h.PlaceName, "skill", CompanyGame.SkillName(h.Id)), Face = true, Hue = UiColour.HueOf(h.Name),
                    Ring = UiRole.None, Back = BackTo(w, back)
                };
                int review = !region && mp.Region != null && !CompanyDirector.Sick(mp.Region) ? mp.Region.Skill : 0;
                int allowance = ManagerRules.Allowance(region ? ManagerCatalogue.RoleRegion : ManagerCatalogue.RolePlace, h.Skill, d.Index);
                card.FactLabels = new[] { w.T("reallife_mgr_skill_label", "Skill"), w.T("reallife_co_f_paid", "Paid"), w.T("reallife_mgr_allowance_label", "Allowance") };
                card.FactValues = new[] { h.Skill.ToString(), CompanyText.Wage(h.Wage), CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(allowance)) };
                if (!region)
                {
                    blocks.Add(Row(CompanyText.T("reallife_mgr_mistake_chance"), CompanyText.Share(ManagerRules.MistakeChance(h.Skill, review))));
                }
                string holds = region ? d.RegionHeldBy(h.Id) : null;
                if (holds != null && holds != key)
                {
                    blocks.Add(Note(CompanyText.T("reallife_mgr_region_moves", "district", CompanyDirector.AreaName(holds)), UiRole.WarnText));
                }
                if (now != null && now.Id != h.Id)
                {
                    blocks.Add(Note(CompanyText.T("reallife_mgr_appoint_replaces", "name", now.Name)));
                }
                var buttons = new List<CompanyButton>(3)
                {
                    Btn(CompanyText.T("reallife_mgr_appoint"), UiButtonKind.Primary, CompanyAct.Of(region ? CompanyDo.RegionAppoint : CompanyDo.Appoint, region ? key : h.Id, 0, 0, 0, region ? h.Id : key),
                        UiAction.Alt, UiIcon.Check, false, now != null && now.Id == h.Id, CompanyText.T("reallife_mgr_in_post"))
                };
                buttons.AddRange(post);
                blocks.Add(Buttons(buttons.ToArray()));
            }
            card.Blocks = blocks.ToArray();
            page.Card = card;
            return true;
        }

        // ------------------------------------------------------------------------------------------------- decisions

        private bool DecisionsPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            string place = pick.StartsWith("mgr:", StringComparison.Ordinal) ? pick.Substring(4) : null;
            ManagedPlace mp = place != null ? d.ManagedAt(place) : null;
            if (mp == null || mp.Own == null)
            {
                return false;
            }
            ManagerState ms = d.ManagerData;
            var rows = new List<CompanyRow>(ListShown);
            for (int i = ms.Decisions.Count - 1; i >= 0 && rows.Count < ListShown; i--)
            {
                Decision dec = ms.Decisions[i];
                if (dec.Place != place)
                {
                    continue;
                }
                rows.Add(new CompanyRow
                {
                    Id = "md:" + dec.Id, Title = CompanyApp.TaskTitle(dec.Task), Sub = CompanyApp.DecisionWords(dec), Icon = TaskIcons[Math.Min(dec.Task, TaskIcons.Length - 1)],
                    Figure = CompanyText.T("reallife_mgr_row_day", "day", dec.Day.ToString()), Note = dec.Who,
                    Chip = CompanyApp.DecisionStateName((int)dec.State) + (dec.Mistake ? CompanyText.T("reallife_mgr_row_mistake") : string.Empty),
                    ChipTone = dec.Mistake && dec.Open ? UiRole.Warn : dec.State == DecisionState.Done ? UiRole.Ok : UiRole.Muted
                });
            }
            page.List = new CompanyList
            {
                Title = w.F("reallife_co_decisions_of", "{name}'s decisions", "name", FirstName(mp.Own.Name)), Count = rows.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_mgr_row_no_decisions")
            };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, chosen);
            Decision picked = null;
            if (chosen.StartsWith("md:", StringComparison.Ordinal) && int.TryParse(chosen.Substring(3), out int id))
            {
                for (int i = 0; i < ms.Decisions.Count && picked == null; i++)
                {
                    picked = ms.Decisions[i].Id == id ? ms.Decisions[i] : null;
                }
            }
            var card = new CompanyCard { Kicker = mp.Place.Name, Title = w.T("reallife_co_log_title", "Decision log"), Icon = UiIcon.Clock, Back = BackTo(w, "mgr:" + place) };
            if (picked == null)
            {
                card.Empty = w.T("reallife_co_dec_pick", "Pick a decision to see what was done, what it cost and whether it can be undone.");
                card.Title = null;
                page.Card = card;
                return true;
            }
            page.Card = DecisionCard(d, picked, mp, w, "mgr:" + place);
            return true;
        }

        private CompanyCard DecisionCard(CompanyDirector d, Decision dec, ManagedPlace mp, Words w, string back)
        {
            var card = new CompanyCard
            {
                Kicker = mp.Place.Name, Title = CompanyApp.TaskTitle(dec.Task), Icon = TaskIcons[Math.Min(dec.Task, TaskIcons.Length - 1)],
                Sub = CompanyText.Fill("reallife_mgr_dec_when", "place", mp.Place.Name, "day", dec.Day.ToString(), "hour", dec.Hour.ToString("00"), "name", dec.Who), Back = BackTo(w, back)
            };
            var chips = new List<string> { CompanyApp.DecisionStateName((int)dec.State) };
            var tones = new List<UiRole> { dec.State == DecisionState.Done ? UiRole.Ok : UiRole.Muted };
            if (dec.Mistake)
            {
                chips.Add(CompanyText.T("reallife_mgr_chip_mistake"));
                tones.Add(UiRole.Danger);
            }
            card.Chips = chips.ToArray();
            card.ChipTones = tones.ToArray();
            string why;
            int state = d.UndoState(dec, d.Today, out why);
            var blocks = new List<CompanyBlock>(10) { Note(CompanyApp.DecisionWords(dec)) };
            if (dec.Task == (int)ManagerTask.Stock || dec.Task == (int)ManagerTask.Prices)
            {
                var items = new List<ItemChange>(8);
                ManagerStore.Items(dec.Detail, items);
                bool money = dec.Task == (int)ManagerTask.Prices;
                for (int i = 0; i < items.Count && i < 6; i++)
                {
                    blocks.Add(Row(CompanyApp.ProductWords(items[i].Item), CompanyText.T("reallife_mgr_dec_from_to", "before", money ? CompanyText.Money(items[i].Before / 100f) : items[i].Before.ToString(),
                        "after", money ? CompanyText.Money(items[i].After / 100f) : items[i].After.ToString())));
                }
                if (items.Count > 6)
                {
                    blocks.Add(Note(CompanyText.T("reallife_co_over_more", "count", (items.Count - 6).ToString())));
                }
            }
            if (dec.Mistake)
            {
                blocks.Add(Note(CompanyText.T("reallife_mgr_dec_mistake"), UiRole.WarnText));
            }
            if (state != 0 && why != null && dec.State != DecisionState.Undone)
            {
                blocks.Add(Note(CompanyText.T(why)));
            }
            else if (state == 0)
            {
                blocks.Add(Note(CompanyText.T("reallife_mgr_dec_undo_what")));
            }
            blocks.Add(Buttons(Btn(CompanyText.T("reallife_mgr_undo"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Undo, null, dec.Id), UiAction.Count, UiIcon.Back, true, state != 0,
                state != 0 && why != null ? CompanyText.T(why) : null)));
            card.Blocks = blocks.ToArray();
            return card;
        }

        // ------------------------------------------------------------------------------------------------- a kind of decision

        private bool TaskPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            string place = pick.StartsWith("mgr:", StringComparison.Ordinal) ? pick.Substring(4) : null;
            ManagedPlace mp = place != null ? d.ManagedAt(place) : null;
            if (mp == null || mp.Own == null)
            {
                return false;
            }
            ManagerFile m = mp.Own;
            // Every kind of decision the place has, each with whether the manager owns it; the one chosen (or the one the page was opened on) has its switch and limits beside.
            int kinds = ManagerRules.Kinds(mp.Kind), blocked = d.ManagerBlocked(mp);
            var rows = new List<CompanyRow>((int)ManagerTask.Count);
            for (int k = 0; k < (int)ManagerTask.Count; k++)
            {
                if (!ManagerRules.Has(kinds, (ManagerTask)k))
                {
                    continue;
                }
                bool owns = ManagerRules.Has(m.Tasks, (ManagerTask)k);
                rows.Add(new CompanyRow
                {
                    Id = "mk:" + k, Title = CompanyApp.TaskTitle(k), Sub = CompanyApp.TaskLine(m, (ManagerTask)k), Icon = TaskIcons[Math.Min(k, TaskIcons.Length - 1)],
                    Chip = owns ? FirstName(m.Name) : w.T("reallife_hier_you", "You"), ChipTone = owns ? ((blocked & (1 << k)) != 0 ? UiRole.Warn : UiRole.Ok) : UiRole.Muted
                });
            }
            page.List = new CompanyList { Title = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", FirstName(m.Name)), Count = rows.Count.ToString(), Rows = rows.ToArray() };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            int chosenRow = ChosenRow(page.List.Rows, chosen);
            if (chosenRow < 0)
            {
                chosenRow = ChosenRow(page.List.Rows, "mk:" + v.SubArg[CompanyView.Managers]);
                chosenRow = chosenRow < 0 && rows.Count > 0 ? 0 : chosenRow;
            }
            page.List.Chosen = chosenRow;
            if (chosenRow < 0)
            {
                return true;
            }
            int t = int.Parse(page.List.Rows[chosenRow].Id.Substring(3));
            ManagerTask task = (ManagerTask)t;
            var card = new CompanyCard { Kicker = mp.Place.Name, Title = CompanyApp.TaskTitle(t), Icon = TaskIcons[Math.Min(t, TaskIcons.Length - 1)], Back = BackTo(w, "mgr:" + place) };
            var blocks = new List<CompanyBlock>(16) { Note(CompanyApp.TaskWhat(t)) };
            if ((d.ManagerBlocked(mp) & ManagerRules.Bit(task)) != 0 && CompanyApp.TaskWaits(t) != null)
            {
                blocks.Add(Note(CompanyApp.TaskWaits(t), UiRole.WarnText));
            }
            bool on = ManagerRules.Has(m.Tasks, task);
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Switch, Text = CompanyText.T("reallife_mgr_task_given"), Sub = CompanyApp.TaskLine(m, task), On = on,
                Value = on ? FirstName(m.Name) : w.T("reallife_hier_you", "You"), Act = CompanyAct.Of(CompanyDo.TaskSet, place, t, on ? 0 : 1)
            });
            switch (task)
            {
                case ManagerTask.Raises:
                case ManagerTask.Offers:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitWage, w));
                    break;
                case ManagerTask.Overtime:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitBudget, w));
                    break;
                case ManagerTask.Hiring:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitBudget, w));
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitKeep, w));
                    break;
                case ManagerTask.Marketing:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitMarketing, w));
                    break;
                case ManagerTask.Leases:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitLease, w));
                    break;
                case ManagerTask.Stock:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitStock, w));
                    break;
                case ManagerTask.Prices:
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitPriceLow, w));
                    blocks.Add(Stepper(d, m, place, CompanyDirector.LimitPriceHigh, w));
                    PriceStrategyBlocks(blocks, m, place);
                    break;
            }
            card.Blocks = blocks.ToArray();
            page.Card = card;
            return true;
        }

        // A limit as a stepper block: the same words and steps as on the manager's own page.
        private CompanyBlock Stepper(CompanyDirector d, ManagerFile m, string place, int limit, Words w)
        {
            CompanyLimit l = LimitOf(d, place, limit, w);
            return new CompanyBlock
            {
                Kind = CompanyBlockKind.Stepper, Text = l.Title, Sub = l.Sub, Value = l.Value, CanLess = l.CanLess, CanMore = l.CanMore, Act = l.Less, Act2 = l.More, Act3 = l.Typed
            };
        }

        // The place's price strategy: the same one a pricing plan on BizMan's pricing page keeps.
        private static void PriceStrategyBlocks(List<CompanyBlock> blocks, ManagerFile m, string place)
        {
            blocks.Add(ChoiceOf(CompanyText.T("reallife_ui_bizops_strategy_stance"), place, CompanyDirector.LimitPriceStance, PriceStrategyRules.StanceKeys.Length, (int)m.Strategy.Stance,
                i => CompanyText.T(PriceStrategyRules.StanceKeys[i])));
            blocks.Add(ChoiceOf(CompanyText.T("reallife_ui_bizops_strategy_margin"), place, CompanyDirector.LimitPriceMargin, PriceStrategyRules.Margins.Length,
                Math.Max(0, Array.IndexOf(PriceStrategyRules.Margins, m.Strategy.Margin)), i => PriceStrategyRules.Margins[i] == 0 ? CompanyText.T("reallife_ui_bizops_margin_none")
                    : CompanyText.T("reallife_ui_bizops_percent", "count", PriceStrategyRules.Margins[i].ToString(System.Globalization.CultureInfo.InvariantCulture))));
            blocks.Add(ChoiceOf(CompanyText.T("reallife_ui_bizops_strategy_ninety"), place, CompanyDirector.LimitPriceNinety, 2, m.Strategy.Ninety ? 1 : 0,
                i => CompanyText.T(i == 1 ? "reallife_ui_bizops_on" : "reallife_ui_bizops_off")));
            blocks.Add(ChoiceOf(CompanyText.T("reallife_ui_bizops_strategy_cost"), place, CompanyDirector.LimitPriceCost, 2, m.Strategy.OverCost ? 1 : 0,
                i => CompanyText.T(i == 1 ? "reallife_ui_bizops_on" : "reallife_ui_bizops_off")));
            blocks.Add(ChoiceOf(CompanyText.T("reallife_ui_bizops_strategy_step"), place, CompanyDirector.LimitPriceStep, PriceStrategyRules.Steps.Length,
                Math.Max(0, Array.IndexOf(PriceStrategyRules.Steps, m.Strategy.Step)), i => PriceStrategyRules.Steps[i] == 0 ? CompanyText.T("reallife_ui_bizops_step_none")
                    : CompanyText.T("reallife_ui_bizops_percent", "count", PriceStrategyRules.Steps[i].ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        private static CompanyBlock ChoiceOf(string title, string place, int limit, int count, int chosen, Func<int, string> words)
        {
            var options = new string[count];
            var acts = new CompanyAct[count];
            for (int i = 0; i < count; i++)
            {
                options[i] = words(i);
                acts[i] = CompanyAct.Of(CompanyDo.LimitSet, place, limit, i);
            }
            return new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = title, Options = options, Chosen = chosen, Acts = acts };
        }

        // ------------------------------------------------------------------------------------------------- the inbox

        private bool InboxPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w)
        {
            HierarchyState h = d.HierarchyData;
            var rows = new List<CompanyRow>(ListShown);
            int yours = 0, others = 0;
            var kinds = new int[(int)EscalationKind.Lease + 2];
            for (int i = h.Escalations.Count - 1; i >= 0; i--)
            {
                Escalation e = h.Escalations[i];
                if (e.Open)
                {
                    yours += e.Level >= EscalationRules.LevelOwner ? 1 : 0;
                    others += e.Level < EscalationRules.LevelOwner ? 1 : 0;
                    kinds[CompanyCatalogue.Clamp(e.Kind, 0, kinds.Length - 1)]++;
                }
                if (rows.Count < ListShown)
                {
                    rows.Add(new CompanyRow
                    {
                        Id = "he:" + e.Id, Title = CompanyApp.KindWords(e.Kind), Sub = CompanyApp.PlaceWords(e), Icon = UiIcon.Envelope, Figure = CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()),
                        Note = CompanyApp.HolderWords(e), Chip = CompanyApp.EscalationStateName(e.State), ChipTone = e.Open && e.Level >= EscalationRules.LevelOwner ? UiRole.Warn : e.Open ? UiRole.None : UiRole.Muted
                    });
                }
            }
            page.List = new CompanyList { Title = w.T("reallife_hier_dir_inbox", "Questions inbox"), Count = rows.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_hier_inbox_none") };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, chosen);
            Escalation picked = null;
            if (chosen.StartsWith("he:", StringComparison.Ordinal) && int.TryParse(chosen.Substring(3), out int id))
            {
                picked = h.Find(id);
            }
            if (picked == null)
            {
                var top = new CompanyCard { Kicker = w.T("reallife_co_tab_managers", "Managers"), Title = w.T("reallife_hier_dir_inbox", "Questions inbox"), Icon = UiIcon.Envelope, Back = BackTo(w, null) };
                top.Chips = new[] { CompanyText.T("reallife_hier_chip_yours", "count", yours.ToString()) };
                top.ChipTones = new[] { yours > 0 ? UiRole.Warn : UiRole.Ok };
                var blocks = new List<CompanyBlock>(8)
                {
                    Sec(w.T("reallife_hier_sec_waiting", "What waits")),
                    Row(CompanyText.T("reallife_hier_held_you"), yours.ToString(), yours > 0 ? UiRole.WarnText : UiRole.None),
                    Row(CompanyText.T("reallife_hier_with_others"), others.ToString())
                };
                for (int k = 0; k < kinds.Length; k++)
                {
                    if (kinds[k] > 0)
                    {
                        blocks.Add(Row(CompanyApp.KindWords(k), kinds[k].ToString()));
                    }
                }
                blocks.Add(Note(CompanyText.T("reallife_hier_inbox_what")));
                top.Blocks = blocks.ToArray();
                page.Card = top;
                return true;
            }
            page.Card = EscalationCard(d, h, picked, w);
            return true;
        }

        // An escalation's page: what it is and who holds it, the owner's way in to answer it (the question's own card), Deep Mode's Try now and Pass, and its story from the log.
        private CompanyCard EscalationCard(CompanyDirector d, HierarchyState h, Escalation e, Words w)
        {
            bool yours = e.Open && e.Level >= EscalationRules.LevelOwner;
            ManagedPlace mp = d.ManagedAt(e.Place);
            var card = new CompanyCard
            {
                Kicker = w.T("reallife_hier_dir_inbox", "Questions inbox"), Title = CompanyApp.KindWords(e.Kind), Sub = CompanyApp.PlaceWords(e), Icon = UiIcon.Envelope, Back = BackTo(w, null)
            };
            var chips = new List<string> { CompanyApp.EscalationStateName(e.State) };
            var tones = new List<UiRole> { e.Open ? UiRole.Warn : UiRole.Ok };
            if (e.Open)
            {
                chips.Add(CompanyText.T("reallife_hier_chip_with", "who", CompanyApp.HolderWords(e)));
                tones.Add(UiRole.None);
            }
            card.Chips = chips.ToArray();
            card.ChipTones = tones.ToArray();
            var blocks = new List<CompanyBlock>(14) { Note(CompanyApp.EscalationSays(d, e)) };
            var buttons = new List<CompanyButton>(4);
            if (yours)
            {
                buttons.Add(Btn(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.Go, "card", e.Id), UiAction.Alt, UiIcon.Next));
            }
            else if (e.Open && h.Deep)
            {
                buttons.Add(Btn(CompanyText.T("reallife_hier_do_try"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.TryNow, null, e.Id), UiAction.Alt, UiIcon.Check));
                buttons.Add(Btn(CompanyText.T("reallife_hier_do_pass"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Pass, null, e.Id), UiAction.Option));
            }
            if (buttons.Count > 0)
            {
                blocks.Add(Buttons(buttons.ToArray()));
            }
            if (mp != null)
            {
                var links = new List<CompanyButton>(3);
                if (e.Kind == (int)EscalationKind.Demand || e.Kind == (int)EscalationKind.Mood || e.Kind == (int)EscalationKind.Wave || e.Kind == (int)EscalationKind.Exception)
                {
                    links.Add(Btn(CompanyText.T("reallife_hier_open_staff"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, e.Kind == (int)EscalationKind.Exception ? "emp:" + e.Subject : "", CompanyView.Staff), UiAction.Count, UiIcon.People));
                }
                links.Add(Btn(CompanyText.T("reallife_hier_open_dashboard"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "mgr:" + e.Place, CompanyView.Managers, CompanyView.SubDashboard), UiAction.Count, UiIcon.ChartPie));
                links.Add(Btn(CompanyText.T("reallife_hier_open_policy"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "mgr:" + e.Place, CompanyView.Managers, CompanyView.SubPolicy), UiAction.Count, UiIcon.Sliders));
                blocks.Add(Buttons(links.ToArray()));
            }
            blocks.Add(Sec(w.T("reallife_hier_sec_story", "The story")));
            blocks.Add(Row(CompanyText.T("reallife_hier_raised"), CompanyText.Day(e.Day)));
            blocks.Add(Row(CompanyText.T("reallife_hier_moved"), CompanyText.Day(e.Moved)));
            if (e.By.Length > 0)
            {
                blocks.Add(Row(CompanyText.T(e.Open ? "reallife_hier_last_by" : "reallife_hier_settled_by"), e.By));
            }
            var lines = new List<CompanyDone>(5);
            for (int i = h.Entries.Count - 1; i >= 0 && lines.Count < LogShown; i--)
            {
                HierarchyEntry en = h.Entries[i];
                if ((en.Kind == (int)HierarchyLog.Raised || en.Kind == (int)HierarchyLog.Passed || en.Kind == (int)HierarchyLog.Settled) && en.A == e.Id)
                {
                    lines.Add(new CompanyDone { When = CompanyText.T("reallife_mgr_row_day", "day", en.Day.ToString()), Text = CompanyApp.EntryWhat(en) + ": " + CompanyApp.EntryDetail(en), Money = en.Who });
                }
            }
            if (lines.Count > 0)
            {
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = lines.ToArray() });
            }
            else
            {
                blocks.Add(Note(CompanyText.T("reallife_hier_story_none")));
            }
            blocks.Add(Note(CompanyText.T(yours ? "reallife_hier_what_yours" : e.Open ? "reallife_hier_what_others" : "reallife_hier_what_closed")));
            card.Blocks = blocks.ToArray();
            return card;
        }

        // ------------------------------------------------------------------------------------------------- the policy centre

        // The scope the centre shows: the company (the owner's node), a region (its node) or a store (its manager's or open post's node).
        private static bool ScopeOf(CompanyDirector d, string pick, out int kind, out string scope)
        {
            kind = CompanyDirector.ScopeCompany;
            scope = null;
            if (pick.StartsWith("reg:", StringComparison.Ordinal) && d.HasArea(pick.Substring(4)))
            {
                kind = CompanyDirector.ScopeRegion;
                scope = pick.Substring(4);
            }
            else if ((pick.StartsWith("mgr:", StringComparison.Ordinal) || pick.StartsWith("post:", StringComparison.Ordinal)) && d.ManagedAt(pick.Substring(pick.IndexOf(':') + 1)) != null)
            {
                kind = CompanyDirector.ScopeStore;
                scope = pick.Substring(pick.IndexOf(':') + 1);
            }
            return true;
        }

        private static string ScopeName(CompanyDirector d, int kind, string scope)
        {
            return kind == CompanyDirector.ScopeCompany ? CompanyText.T("reallife_pol_scope_company")
                : kind == CompanyDirector.ScopeRegion ? CompanyText.T("reallife_pol_scope_region", "region", CompanyDirector.AreaName(scope))
                : CompanyText.T("reallife_pol_scope_store", "store", d.ManagedName(scope));
        }

        // The node a scope is picked by, so that the page keeps the tree's pick across the sub pages.
        private static string ScopeNode(int kind, string scope)
        {
            return kind == CompanyDirector.ScopeCompany ? "own" : (kind == CompanyDirector.ScopeRegion ? "reg:" : "mgr:") + scope;
        }

        private bool PolicyPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick, CompanyManagersFacts m)
        {
            int kind;
            string scope;
            ScopeOf(d, pick, out kind, out scope);
            HierarchyState hs = d.HierarchyData;
            string area = kind == CompanyDirector.ScopeRegion ? scope : kind == CompanyDirector.ScopeStore && d.ManagedAt(scope) != null ? d.AreaOf(d.ManagedAt(scope)) : null;
            PolicySet region = area != null ? hs.Region(area) : null, store = kind == CompanyDirector.ScopeStore ? hs.Store(scope) : null;
            var rows = new List<CompanyRow>(40);
            for (int c = 0; c < (int)PolicyCategory.Count; c++)
            {
                rows.Add(new CompanyRow { Caption = true, Title = CompanyText.T(PolicyCatalogue.CategoryKeys[c]) });
                for (int f = 0; f < PolicyCatalogue.Defs.Length; f++)
                {
                    PolicyDef def = PolicyCatalogue.Defs[f];
                    if ((int)def.Category != c)
                    {
                        continue;
                    }
                    bool held = RuleHeld(hs, def, region, store);
                    PolicySet own = kind == CompanyDirector.ScopeCompany ? hs.Company : kind == CompanyDirector.ScopeRegion ? region : store;
                    bool sets = own != null && (kind == CompanyDirector.ScopeCompany || own.Sets(def.Field));
                    rows.Add(new CompanyRow
                    {
                        Id = "pf:" + f, Title = CompanyApp.RuleName(def.Field), Icon = UiIcon.Sliders,
                        Sub = kind == CompanyDirector.ScopeCompany ? null : sets ? CompanyText.T("reallife_pol_set_here_row") : def.Way == PolicyWay.CompanyOnly ? CompanyText.T("reallife_pol_company_only") : CompanyText.T("reallife_pol_as_above"),
                        Figure = CompanyApp.PolicyStepName(def.Field, d.ScopeRule(kind, scope, def.Field)), Chip = held ? CompanyText.T("reallife_co_held") : null, ChipTone = UiRole.Warn
                    });
                }
            }
            page.List = new CompanyList { Title = w.T("reallife_hier_dir_policy", "Policy centre"), Count = ScopeName(d, kind, scope), Rows = rows.ToArray() };
            // The scope chips: the company, the regions and, when a store is the scope, that store.
            var names = new List<string> { CompanyText.T("reallife_pol_scope_company") };
            var ids = new List<string> { "own" };
            IReadOnlyList<string> areas = d.ManagerDistricts;
            for (int i = 0; i < areas.Count && names.Count < 5; i++)
            {
                names.Add(CompanyDirector.AreaName(areas[i]));
                ids.Add("reg:" + areas[i]);
            }
            if (kind == CompanyDirector.ScopeStore)
            {
                names.Add(d.ManagedName(scope));
                ids.Add(ScopeNode(kind, scope));
            }
            page.List.Filters = names.ToArray();
            m.FilterIds = ids.ToArray();
            page.List.Filter = Math.Max(0, ids.IndexOf(ScopeNode(kind, scope)));
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, chosen);
            string back = ScopeNode(kind, scope);
            if (chosen.StartsWith("pf:", StringComparison.Ordinal) && int.TryParse(chosen.Substring(3), out int fi) && fi >= 0 && fi < PolicyCatalogue.Defs.Length)
            {
                page.Card = RuleCard(d, hs, PolicyCatalogue.Defs[fi], kind, scope, area, w, back);
                return true;
            }
            var top = new CompanyCard { Kicker = w.T("reallife_hier_dir_policy", "Policy centre"), Title = ScopeName(d, kind, scope), Icon = UiIcon.Sliders, Back = BackTo(w, back) };
            PolicySet mine = kind == CompanyDirector.ScopeCompany ? null : kind == CompanyDirector.ScopeRegion ? region : store;
            var blocks = new List<CompanyBlock>(8)
            {
                Sec(w.T("reallife_pol_sec_scope", "This scope")),
                Row(CompanyText.T("reallife_pol_own"), kind == CompanyDirector.ScopeCompany ? CompanyText.T("reallife_pol_own_all") : (mine != null ? mine.Count : 0).ToString()),
                Note(CompanyText.T("reallife_pol_what")),
                Note(CompanyText.T("reallife_pol_held_what")),
                Buttons(
                    Btn(CompanyText.T("reallife_pol_templates"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, back, CompanyView.Managers, CompanyView.SubTemplates), UiAction.Count, UiIcon.ClipboardText),
                    Btn(CompanyText.T("reallife_pol_save_template"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.TemplateSave, scope, 0, 0, kind), UiAction.Count, UiIcon.FloppyDisk))
            };
            if (mine != null && mine.Count > 0)
            {
                blocks.Add(Buttons(Btn(CompanyText.T("reallife_pol_follow"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RuleClear, scope, 0, 0, kind), UiAction.Count, UiIcon.Back, true)));
            }
            top.Blocks = blocks.ToArray();
            page.Card = top;
            return true;
        }

        // Whether a lower scope's own step is past the one above it and so held to it.
        private static bool RuleHeld(HierarchyState hs, PolicyDef def, PolicySet region, PolicySet store)
        {
            int company = PolicyRules.Effective(hs.Company, null, null, def.Field);
            if (region != null && region.Sets(def.Field) && PolicyRules.Past(def.Way, company, region.Of(def.Field)))
            {
                return true;
            }
            int above = PolicyRules.Effective(hs.Company, region, null, def.Field);
            return store != null && store.Sets(def.Field) && PolicyRules.Past(def.Way, above, store.Of(def.Field));
        }

        // A rule: what it does, then its steps (a lower scope's first choice is to follow the scope above) as chips for a few steps, a stepper for many, and how it holds.
        private CompanyCard RuleCard(CompanyDirector d, HierarchyState hs, PolicyDef def, int kind, string scope, string area, Words w, string back)
        {
            PolicyField field = def.Field;
            var card = new CompanyCard
            {
                Kicker = CompanyText.T(PolicyCatalogue.CategoryKeys[(int)def.Category]) + ", " + ScopeName(d, kind, scope), Title = CompanyApp.RuleName(field), Icon = UiIcon.Sliders,
                Back = BackTo(w, back, CompanyView.SubPolicy)
            };
            var blocks = new List<CompanyBlock>(8) { Note(CompanyText.T(def.TextKey + "_what", "score", RegionCatalogue.ReplaceScore.ToString())) };
            bool company = kind == CompanyDirector.ScopeCompany;
            if (!company && def.Way == PolicyWay.CompanyOnly)
            {
                blocks.Add(Note(CompanyText.T("reallife_pol_company_only_line", "value", CompanyApp.PolicyStepName(field, hs.Company.Of(field))), UiRole.WarnText));
                card.Blocks = blocks.ToArray();
                return card;
            }
            PolicySet own = company ? hs.Company : kind == CompanyDirector.ScopeRegion ? hs.Region(scope) : hs.Store(scope);
            int above = company ? -1 : kind == CompanyDirector.ScopeRegion ? d.ScopeRule(CompanyDirector.ScopeCompany, null, field)
                : area != null ? d.ScopeRule(CompanyDirector.ScopeRegion, area, field) : d.ScopeRule(CompanyDirector.ScopeCompany, null, field);
            int offset = company ? 0 : 1;
            int current = own != null && own.Sets(field) ? own.Of(field) + offset : 0;
            int count = def.Steps.Length + offset;
            Func<int, string> words = i => !company && i == 0 ? CompanyText.T("reallife_pol_follow_one", "value", CompanyApp.PolicyStepName(field, above))
                : !company && PolicyRules.Past(def.Way, above, i - offset) ? CompanyText.T("reallife_pol_held_mark", "value", CompanyApp.PolicyStepName(field, i - offset)) : CompanyApp.PolicyStepName(field, i - offset);
            blocks.Add(Sec(w.T("reallife_pol_set_here", "Set here")));
            if (count <= StepsAsChoice)
            {
                var options = new string[count];
                var acts = new CompanyAct[count];
                for (int i = 0; i < count; i++)
                {
                    options[i] = words(i);
                    acts[i] = CompanyAct.Of(CompanyDo.RuleSet, scope, (int)field, i - offset, kind);
                }
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = CompanyApp.RuleName(field), Options = options, Chosen = current, Acts = acts });
            }
            else
            {
                blocks.Add(new CompanyBlock
                {
                    Kind = CompanyBlockKind.Stepper, Text = CompanyApp.RuleName(field), Value = words(current), CanLess = current > 0, CanMore = current < count - 1,
                    Act = CompanyAct.Of(CompanyDo.RuleSet, scope, (int)field, current - 1 - offset, kind), Act2 = CompanyAct.Of(CompanyDo.RuleSet, scope, (int)field, current + 1 - offset, kind)
                });
            }
            blocks.Add(Sec(w.T("reallife_pol_sec_force", "In force")));
            blocks.Add(Row(CompanyText.T("reallife_pol_scope_company"), CompanyApp.PolicyStepName(field, hs.Company.Of(field))));
            blocks.Add(Row(CompanyText.T("reallife_pol_col_force"), CompanyApp.PolicyStepName(field, d.ScopeRule(kind, scope, field))));
            if (!company && own != null && own.Sets(field) && PolicyRules.Past(def.Way, above, own.Of(field)))
            {
                blocks.Add(Note(CompanyText.T("reallife_pol_held_line", "value", CompanyApp.PolicyStepName(field, above)), UiRole.WarnText));
            }
            blocks.Add(Note(CompanyText.T(def.Way == PolicyWay.LooseUp ? "reallife_pol_way_up" : def.Way == PolicyWay.LooseDown ? "reallife_pol_way_down" : "reallife_pol_way_company")));
            card.Blocks = blocks.ToArray();
            return card;
        }

        // ------------------------------------------------------------------------------------------------- templates

        private bool TemplatesPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            int kind;
            string scope;
            ScopeOf(d, pick, out kind, out scope);
            HierarchyState hs = d.HierarchyData;
            var rows = new List<CompanyRow>(8);
            for (int i = 0; i < PolicyCatalogue.Templates.Length; i++)
            {
                rows.Add(new CompanyRow { Id = "pk:b" + i, Title = CompanyText.T(PolicyCatalogue.TemplateKeys[i]), Sub = CompanyText.T("reallife_pol_tpl_built"), Icon = UiIcon.ClipboardText });
            }
            for (int i = 0; i < hs.Templates.Count; i++)
            {
                PolicyTemplate t = hs.Templates[i];
                rows.Add(new CompanyRow
                {
                    Id = "pk:o" + t.Number, Title = CompanyText.T("reallife_pol_tpl_own", "number", t.Number.ToString()), Sub = CompanyText.T("reallife_pol_tpl_yours"), Icon = UiIcon.ClipboardText,
                    Figure = CompanyText.T("reallife_mgr_row_day", "day", t.Day.ToString())
                });
            }
            string back = ScopeNode(kind, scope);
            page.List = new CompanyList { Title = w.T("reallife_pol_templates", "Templates"), Count = rows.Count.ToString(), Rows = rows.ToArray() };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, chosen);
            var card = new CompanyCard { Kicker = ScopeName(d, kind, scope), Title = w.T("reallife_pol_templates", "Templates"), Icon = UiIcon.ClipboardText, Back = BackTo(w, back, CompanyView.SubPolicy) };
            bool own = chosen.StartsWith("pk:o", StringComparison.Ordinal);
            int number = -1;
            if ((own || chosen.StartsWith("pk:b", StringComparison.Ordinal)) && int.TryParse(chosen.Substring(4), out number))
            {
                int[] steps = null;
                for (int i = 0; own && i < hs.Templates.Count && steps == null; i++)
                {
                    steps = hs.Templates[i].Number == number ? hs.Templates[i].Set.Steps : null;
                }
                bool known = own ? steps != null : number >= 0 && number < PolicyCatalogue.Templates.Length;
                if (known)
                {
                    steps = own ? steps : PolicyCatalogue.Templates[number];
                    card.Title = own ? CompanyText.T("reallife_pol_tpl_own", "number", number.ToString()) : CompanyText.T(PolicyCatalogue.TemplateKeys[number]);
                    card.Sub = own ? CompanyText.T("reallife_pol_tpl_yours") : CompanyText.T("reallife_pol_tpl_built");
                    var blocks = new List<CompanyBlock>(12);
                    if (!own)
                    {
                        blocks.Add(Note(CompanyText.T(PolicyCatalogue.TemplateKeys[number] + "_what")));
                    }
                    blocks.Add(Sec(w.T("reallife_pol_sec_template", "What it sets")));
                    PolicyField[] shown = { PolicyField.PayBand, PolicyField.SkillFloor, PolicyField.Approver, PolicyField.Budget, PolicyField.GapDays, PolicyField.Reports };
                    for (int i = 0; i < shown.Length; i++)
                    {
                        int f = (int)shown[i];
                        blocks.Add(Row(CompanyApp.RuleName(shown[i]), CompanyApp.PolicyStepName(shown[i], steps != null && steps[f] >= 0 ? steps[f] : PolicyCatalogue.Defs[f].Default)));
                    }
                    blocks.Add(Note(CompanyText.T("reallife_pol_tpl_onto", "scope", ScopeName(d, kind, scope))));
                    var buttons = new List<CompanyButton>(2)
                    {
                        Btn(CompanyText.T("reallife_pol_tpl_apply"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.TemplateApply, scope, own ? 0 : number, own ? number : 0, kind), UiAction.Alt, UiIcon.Check)
                    };
                    if (own)
                    {
                        buttons.Add(Btn(CompanyText.T("reallife_pol_tpl_delete"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.TemplateDelete, null, number), UiAction.Count, UiIcon.Close, true));
                    }
                    blocks.Add(Buttons(buttons.ToArray()));
                    card.Blocks = blocks.ToArray();
                    page.Card = card;
                    return true;
                }
            }
            card.Title = null;
            card.Empty = CompanyText.T("reallife_pol_templates_what");
            page.Card = card;
            return true;
        }

        // ------------------------------------------------------------------------------------------------- dashboards

        private bool DashboardPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            string place = pick.StartsWith("mgr:", StringComparison.Ordinal) ? pick.Substring(4) : null;
            ManagedPlace mp = place != null ? d.ManagedAt(place) : null;
            if (mp == null || mp.Own == null)
            {
                return false;
            }
            HierarchyState hs = d.HierarchyData;
            ManagerFile m = mp.Own;
            var rows = new List<CompanyRow>(ListShown);
            int open = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = hs.Escalations.Count - 1; i >= 0 && rows.Count < ListShown; i--)
                {
                    Escalation e = hs.Escalations[i];
                    if (e.Open != (pass == 0) || !string.Equals(e.Place, place, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    open += e.Open ? 1 : 0;
                    rows.Add(new CompanyRow
                    {
                        Id = "he:" + e.Id, Title = CompanyApp.KindWords(e.Kind), Sub = CompanyApp.HolderWords(e), Icon = UiIcon.Envelope, Figure = CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()),
                        Chip = CompanyApp.EscalationStateName(e.State), ChipTone = e.Open ? UiRole.Warn : UiRole.Muted
                    });
                }
            }
            page.List = new CompanyList { Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = rows.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_hier_dash_none") };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, chosen);
            int count, standing = d.StandingAt(place, out count), budget = d.BudgetOf(m);
            float used = budget > 0 ? Math.Min(1f, Math.Max(0f, m.Spent / (float)budget)) : 0f;
            var card = new CompanyCard
            {
                Kicker = mp.Place.Name + ", " + m.Name, Title = CompanyText.T("reallife_hier_dash_title"), Icon = UiIcon.ChartPie, Back = BackTo(w, "mgr:" + place),
                Chips = new[] { CompanyText.T("reallife_hier_level_chip", "level", ExecutiveHiring.Code(Rung.StoreManager)) }, ChipTones = new[] { UiRole.None },
                FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_score", "Score"), w.T("reallife_co_f_pending", "Pending") },
                FactValues = new[] { CompanyText.T("reallife_hier_fig_staff_value", "staff", mp.Place.Staff.ToString(), "keep", Math.Max(0, m.Keep).ToString()),
                    count > 0 ? standing.ToString() : CompanyText.T("reallife_mgr_none"), d.ScoreOf(mp).ToString(), open.ToString() }
            };
            var blocks = new List<CompanyBlock>(12)
            {
                Sec(w.T("reallife_hier_sec_figures", "Figures")),
                Row(CompanyText.T("reallife_hier_fig_budget"), CompanyText.T("reallife_mgr_spent_value", "spent", CompanyText.Money(m.Spent), "budget", CompanyText.Money(budget))),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = CompanyText.T("reallife_hier_fig_budget_used"), Value = CompanyText.Share((int)(used * 1000f)), Number = (int)(used * 100f), Tone = used >= 0.8f ? UiRole.Warn : UiRole.Ok }
            };
            if (mp.Shop != null && mp.Shop.HasBooks)
            {
                blocks.Add(Row(CompanyText.T("reallife_ibox_profit"), CompanyText.Money(mp.Shop.Profit), mp.Shop.Profit < 0 ? UiRole.DangerText : UiRole.None));
            }
            blocks.Add(Sec(w.T("reallife_hier_sec_quick", "Do now")));
            blocks.Add(Buttons(
                Btn(CompanyText.T("reallife_hier_quick_fill"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.StoreFill, place), UiAction.Alt, UiIcon.People),
                Btn(CompanyText.T("reallife_hier_quick_raise"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.StoreRaise, place), UiAction.Option, UiIcon.Coin),
                Btn(CompanyText.T("reallife_hier_open_policy"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiAction.Count, UiIcon.Sliders)));
            // The store's region: its district's, or one the player makes (A to H).
            int now = HierarchyRules.CustomNumber(mp.Area);
            var options = new string[HierarchyRules.CustomMost + 1];
            var acts = new CompanyAct[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = i == 0 ? CompanyText.T("reallife_hier_region_own", "district", CompanyText.District(mp.District)) : HierarchyRules.Letter(i);
                acts[i] = CompanyAct.Of(CompanyDo.StoreArea, place, i);
            }
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = CompanyText.T("reallife_hier_region_pick"), Options = options, Chosen = now, Acts = acts });
            blocks.Add(Note(CompanyText.T("reallife_hier_dash_what")));
            card.Blocks = blocks.ToArray();
            page.Card = card;
            return true;
        }

        private bool RegionPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            string area = pick.StartsWith("reg:", StringComparison.Ordinal) ? pick.Substring(4) : null;
            if (area == null || !d.HasArea(area))
            {
                return false;
            }
            HierarchyState hs = d.HierarchyData;
            ManagerFile r = d.RegionOf(area);
            var rows = new List<CompanyRow>(ListShown);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = hs.Escalations.Count - 1; i >= 0 && rows.Count < ListShown; i--)
                {
                    Escalation e = hs.Escalations[i];
                    ManagedPlace at = d.ManagedAt(e.Place);
                    bool ours = string.Equals(e.Place, area, StringComparison.Ordinal) || (at != null && string.Equals(d.AreaOf(at), area, StringComparison.Ordinal));
                    if (e.Open != (pass == 0) || !ours)
                    {
                        continue;
                    }
                    rows.Add(new CompanyRow
                    {
                        Id = "he:" + e.Id, Title = CompanyApp.KindWords(e.Kind), Sub = CompanyApp.PlaceWords(e), Icon = UiIcon.Envelope, Figure = CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()),
                        Chip = CompanyApp.EscalationStateName(e.State), ChipTone = e.Open ? UiRole.Warn : UiRole.Muted
                    });
                }
            }
            page.List = new CompanyList { Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = rows.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_hier_dash_none") };
            page.List.Chosen = ChosenRow(page.List.Rows, v.SubPick[CompanyView.Managers] ?? string.Empty);
            int places, managed, gaps, standing, profit, score, pending;
            d.RegionFigures(area, out places, out managed, out gaps, out standing, out profit, out score, out pending);
            int ruleStep = d.RegionRule(area, PolicyField.ProfitTarget);
            int target = PolicyCatalogue.Value(PolicyField.ProfitTarget, ruleStep);
            var card = new CompanyCard
            {
                Kicker = CompanyText.T("reallife_mgr_row_region", "district", CompanyDirector.AreaName(area)) + (r != null ? ", " + r.Name : string.Empty), Title = CompanyText.T("reallife_hier_dash_title"),
                Icon = UiIcon.ChartPie, Back = BackTo(w, "reg:" + area), Chips = new[] { CompanyText.T("reallife_hier_level_chip", "level", ExecutiveHiring.Code(Rung.Regional)) }, ChipTones = new[] { UiRole.None },
                FactLabels = new[] { w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_gaps", "Gaps"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_pending", "Pending") },
                FactValues = new[] { managed + "/" + places, gaps.ToString(), standing.ToString(), pending.ToString() }
            };
            var blocks = new List<CompanyBlock>(10)
            {
                Sec(w.T("reallife_hier_sec_figures", "Figures")),
                Row(CompanyText.T("reallife_hier_fig_score"), score.ToString()),
                Row(CompanyText.T("reallife_hier_fig_profit"), CompanyText.Money(profit), target != PolicyCatalogue.ProfitNone && managed > 0 && profit < CompanyCatalogue.AtPrices(target, d.Index) * managed ? UiRole.WarnText : UiRole.None),
                Row(CompanyText.T("reallife_pol_field_profit_target"), CompanyApp.PolicyStepName(PolicyField.ProfitTarget, ruleStep)),
                Sec(w.T("reallife_hier_sec_quick", "Do now")),
                Buttons(
                    Btn(CompanyText.T("reallife_hier_quick_fill"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RegionFill, area), UiAction.Alt, UiIcon.People),
                    Btn(CompanyText.T("reallife_hier_quick_raise"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RegionRaise, area), UiAction.Option, UiIcon.Coin)),
                Buttons(
                    Btn(CompanyText.T("reallife_hier_quick_move"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RegionMove, area), UiAction.Count, UiIcon.Next),
                    Btn(CompanyText.T("reallife_hier_quick_appoint"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RegionAppoint, area), UiAction.Count, UiIcon.People),
                    Btn(CompanyText.T("reallife_hier_open_policy"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "reg:" + area, CompanyView.Managers, CompanyView.SubPolicy), UiAction.Count, UiIcon.Sliders)),
                Note(CompanyText.T("reallife_hier_region_dash_what"))
            };
            card.Blocks = blocks.ToArray();
            page.Card = card;
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the seats

        private bool SeatPage(CompanyPage page, CompanyDirector d, CompanyView v, Words w, string pick)
        {
            int seatNo;
            if (!(pick.StartsWith("exe:", StringComparison.Ordinal) || pick.StartsWith("seat:", StringComparison.Ordinal)) || !int.TryParse(pick.Substring(pick.IndexOf(':') + 1), out seatNo)
                || !ExecutiveHiring.Real(seatNo))
            {
                return false;
            }
            ExecSeat seat = (ExecSeat)seatNo;
            HierarchyState hs = d.HierarchyData;
            ExecutiveFile x = hs.Executive(seat);
            IReadOnlyList<ProfessionalHand> pros = d.Professionals;
            var rows = new List<CompanyRow>(ListShown);
            for (int i = 0; i < pros.Count && rows.Count < ListShown; i++)
            {
                ProfessionalHand p = pros[i];
                if (p.Leaving || !ExecutiveHiring.Fits(seat, p.Role, p.Level))
                {
                    continue;
                }
                Face(p.Name, p.Id);
                rows.Add(new CompanyRow
                {
                    Id = "xc:" + p.Id, Title = p.Name, Sub = TechText.Role(p.Role) + ", " + TechText.Level(p.Level), Face = true, Hue = UiColour.HueOf(p.Name), Ring = UiRole.None,
                    Figure = CompanyText.Wage(p.Going), Note = p.PlaceName, Chip = x != null && x.Id == p.Id ? CompanyText.T("reallife_mgr_in_post") : null, ChipTone = UiRole.Ok
                });
            }
            page.List = new CompanyList { Title = w.T("reallife_co_candidates", "Who could do it"), Count = rows.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_hier_exec_nobody") };
            string chosen = v.SubPick[CompanyView.Managers] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, chosen);
            string back = x != null ? "exe:" + seatNo : "seat:" + seatNo;
            string name = CompanyDirector.SeatName(seat);
            ProfessionalHand? hand = null;
            for (int i = 0; i < pros.Count && chosen.StartsWith("xc:", StringComparison.Ordinal); i++)
            {
                hand = pros[i].Id == chosen.Substring(3) ? pros[i] : hand;
            }
            CompanyCard card;
            if (hand != null)
            {
                ProfessionalHand p = hand.Value;
                card = new CompanyCard
                {
                    Kicker = name, Title = p.Name, Sub = TechText.Role(p.Role) + ", " + TechText.Level(p.Level) + (p.PlaceName.Length > 0 ? ", " + p.PlaceName : string.Empty), Face = true,
                    Hue = UiColour.HueOf(p.Name), Ring = UiRole.None, Back = BackTo(w, back)
                };
                Face(p.Name, p.Id);
                var blocks = new List<CompanyBlock>(6) { Row(CompanyText.T("reallife_hier_going"), CompanyText.Wage(p.Going)) };
                if (x != null && x.Id != p.Id)
                {
                    blocks.Add(Note(CompanyText.T("reallife_mgr_appoint_replaces", "name", x.Name)));
                }
                blocks.Add(Note(CompanyText.T(PolicyCatalogue.Mod + "_hier_seat_what_" + seat.ToString().ToLowerInvariant())));
                blocks.Add(Buttons(Btn(CompanyText.T("reallife_mgr_appoint"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.SeatAppoint, p.Id, seatNo), UiAction.Alt, UiIcon.Check, false, x != null && x.Id == p.Id,
                    CompanyText.T("reallife_mgr_in_post"))));
                card.Blocks = blocks.ToArray();
            }
            else
            {
                card = new CompanyCard { Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = name, Icon = UiIcon.IdBadge, Back = BackTo(w, back) };
                var blocks = new List<CompanyBlock>(12) { Note(CompanyText.T(PolicyCatalogue.Mod + "_hier_seat_what_" + seat.ToString().ToLowerInvariant())) };
                if (x == null)
                {
                    card.Chips = new[] { CompanyText.T("reallife_mgr_empty_post") };
                    card.ChipTones = new[] { UiRole.Muted };
                    blocks.Add(Note(CompanyText.T("reallife_hier_seat_hire", "roles", SeatRolesOf(seat))));
                    blocks.Add(Note(CompanyText.T("reallife_hier_exec_pick")));
                }
                else
                {
                    card.Face = true;
                    card.Hue = UiColour.HueOf(x.Name);
                    card.Ring = x.Seen ? UiRole.Ok : UiRole.Warn;
                    card.Title = x.Name;
                    card.Sub = name;
                    Face(x.Name, x.Id);
                    card.Chips = new[] { CompanyText.T(x.Seen ? "reallife_mgr_in_post" : "reallife_hier_seat_waits") };
                    card.ChipTones = new[] { x.Seen ? UiRole.Ok : UiRole.Warn };
                    blocks.Add(Row(CompanyText.T("reallife_mgr_since"), CompanyText.Day(x.From)));
                    blocks.Add(Row(CompanyText.T("reallife_hier_col_role"), TechText.Role(x.Role) + ", " + TechText.Level(x.Level)));
                    blocks.Add(Row(CompanyText.T("reallife_hier_going"), CompanyText.Wage(x.Going)));
                    blocks.Add(Row(CompanyText.T("reallife_mgr_allowance"), CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(CompanyDirector.AllowanceOf(x)))));
                    blocks.Add(Row(CompanyText.T("reallife_hier_paid_month"), CompanyText.Money(x.Paid)));
                    blocks.Add(Row(CompanyText.T("reallife_hier_area_open"), d.OpenOfSeat(seat).ToString()));
                    blocks.Add(Row(CompanyText.T("reallife_hier_area_settled"), d.SettledSince(null, x.Name, d.Today - 7).ToString()));
                    blocks.Add(Row(CompanyText.T("reallife_mgr_decided"), x.Decided.ToString() + " / " + x.LastDecided));
                    if (!x.Seen)
                    {
                        blocks.Add(Note(CompanyText.T("reallife_hier_seat_waits_line"), UiRole.WarnText));
                    }
                    blocks.Add(Buttons(Btn(CompanyText.T("reallife_mgr_stand_down"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.SeatStandDown, null, seatNo), UiAction.Count, UiIcon.Close, true)));
                    var lines = new List<CompanyDone>(5);
                    for (int i = hs.Entries.Count - 1; i >= 0 && lines.Count < LogShown; i--)
                    {
                        HierarchyEntry en = hs.Entries[i];
                        if (en.Who == x.Name)
                        {
                            lines.Add(new CompanyDone { When = CompanyText.T("reallife_mgr_row_day", "day", en.Day.ToString()), Text = CompanyApp.EntryWhat(en) + ": " + CompanyApp.EntryDetail(en) });
                        }
                    }
                    if (lines.Count > 0)
                    {
                        blocks.Add(Sec(w.T("reallife_co_log", "Log")));
                        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = lines.ToArray() });
                    }
                }
                card.Blocks = blocks.ToArray();
            }
            page.Card = card;
            return true;
        }

        private static string SeatRolesOf(ExecSeat seat)
        {
            int[] roles = ExecutiveHiring.SeatRoles[(int)seat];
            string list = string.Empty;
            for (int i = 0; i < roles.Length; i++)
            {
                list += (i > 0 ? ", " : string.Empty) + TechText.Role(roles[i]);
            }
            return list;
        }
    }
}
