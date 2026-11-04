using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The rota plans of each business (RotaPlanRules): named plans with a date range, the weeks they repeat on, the most hours each person may be placed, overtime
    /// and a floor of crew by block of hours. The plan in use for a week is put on what the planner reads (RotaGame.PutPlan), so the page, "Fill the gaps" and the
    /// managers' Sunday write all plan by it. Nothing per frame: the plans are read from the save once and changed by the player's presses.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly Dictionary<string, RotaPlanSet> _rotaPlans = new Dictionary<string, RotaPlanSet>(StringComparer.Ordinal);
        private bool _rotaPlansUnread;

        partial void RotaPlansLoad(GameInstance save)
        {
            string text = null;
            _rotaPlansUnread = save != null && save.modData != null && save.modData.TryGetValue(RotaPlanStore.Key, out text) && !RotaPlanStore.Read(text, _rotaPlans);
            if (_rotaPlansUnread)
            {
                SaveLines.Unreadable(save.modData, RotaPlanStore.Key, "its version is not one this version of the mod reads");
                Logger?.Warn("Company: the rota plans in this save are not of a version this one reads; no plan is in use this session, and the line is kept as it is.");
            }
            else if (Verbose)
            {
                Logger?.Info("Company check (rota plans): " + _rotaPlans.Count + " business(es) with plans read from the save (version " + RotaPlanStore.Version + "): PASS");
            }
        }

        partial void RotaPlansSave(GameInstance save)
        {
            if (_rotaPlansUnread || save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            // A business that is no longer the player's lets its plans go.
            var gone = new List<string>(2);
            foreach (string place in _rotaPlans.Keys)
            {
                if (ShopAt(place) == null && _shops.Count > 0)
                {
                    gone.Add(place);
                }
            }
            for (int i = 0; i < gone.Count; i++)
            {
                _rotaPlans.Remove(gone[i]);
            }
            string line = RotaPlanStore.Write(_rotaPlans);
            if (line == "v=" + RotaPlanStore.Version)
            {
                save.modData.Remove(RotaPlanStore.Key);
            }
            else
            {
                save.modData[RotaPlanStore.Key] = line;
            }
        }

        /// <summary>A business's plans; null while it has none of its own (the planner's rules stand), or one made with the starting plan with <paramref name="make"/>.</summary>
        internal RotaPlanSet RotaPlansOf(string place, bool make)
        {
            RotaPlanSet set;
            if (_rotaPlans.TryGetValue(place, out set) || !make || _rotaPlansUnread || _rotaPlans.Count >= RotaPlanStore.PlacesMost)
            {
                return set;
            }
            set = new RotaPlanSet { Place = place };
            RotaPlanFile standard = RotaPlanRules.Starting(set.NextId++, CompanyText.T("reallife_rota_plan_standard"));
            set.Plans.Add(standard);
            set.Active = standard.Id;
            _rotaPlans[place] = set;
            return set;
        }

        /// <summary>The day a rota written now plans for: today, or the coming Monday from Sunday's rota hour on (the managers' Sunday write is for that week).</summary>
        internal int RotaPlanDay()
        {
            int today = Save.Day;
            return ManagerRules.Weekday(today) == ManagerCatalogue.Sunday && Save.Hour >= ManagerCatalogue.RotaHour ? today + 1 : today;
        }

        /// <summary>The plan in use at a business for the week a day is in; null for none.</summary>
        internal RotaPlanFile RotaPlanFor(string place, int day)
        {
            RotaPlanSet set;
            return place != null && _rotaPlans.TryGetValue(place, out set) ? RotaPlanRules.InUse(set, day) : null;
        }

        // A number that changes when the plan in use, or any plan's figures, change: folded into the sheet's stamp so that a changed plan is read again.
        private int RotaPlanStamp(string place)
        {
            RotaPlanFile p = RotaPlanFor(place, RotaPlanDay());
            return p == null ? 0 : unchecked(p.Id * 31 + p.Version * 7919 + 1);
        }

        private void RotaPlansChanged(string place)
        {
            _rotaSheetGood = false;   // what was worked out under the old plan is not reused
            Changed();
        }

        /// <summary>A new plan, as the starting one, named for the player; false when the business keeps the most.</summary>
        public bool RotaPlanNew(string place, string name)
        {
            RotaPlanSet set = RotaPlansOf(place, true);
            if (set == null || RotaPlanRules.Add(set, name) == null)
            {
                return false;
            }
            RotaPlansChanged(place);
            return true;
        }

        /// <summary>A copy of a plan under a name; false when the business keeps the most.</summary>
        public bool RotaPlanCopy(string place, int id, string name)
        {
            RotaPlanSet set = RotaPlansOf(place, false);
            if (set == null || RotaPlanRules.Copy(set, id, name) == null)
            {
                return false;
            }
            RotaPlansChanged(place);
            return true;
        }

        /// <summary>The plan the business falls back on when no dated plan holds for the week.</summary>
        public void RotaPlanUse(string place, int id)
        {
            RotaPlanSet set = RotaPlansOf(place, true);
            RotaPlanFile p = set != null ? set.Find(id) : null;
            if (p == null || p.Dated)
            {
                return;   // a dated plan is in use by its days, not by a choice
            }
            set.Active = id;
            p.Version++;
            RotaPlansChanged(place);
        }

        /// <summary>A plan taken away; the last undated plan stays, so a business always has something to fall back on.</summary>
        public bool RotaPlanDelete(string place, int id)
        {
            RotaPlanSet set = RotaPlansOf(place, false);
            RotaPlanFile p = set != null ? set.Find(id) : null;
            if (p == null)
            {
                return false;
            }
            int undated = 0;
            for (int i = 0; i < set.Plans.Count; i++)
            {
                undated += set.Plans[i].Dated ? 0 : 1;
            }
            if (!p.Dated && undated <= 1)
            {
                return false;
            }
            set.Plans.Remove(p);
            if (set.Active == id)
            {
                set.Active = 0;
            }
            RotaPlansChanged(place);
            return true;
        }

        public const int PlanName = 0, PlanFrom = 1, PlanTo = 2, PlanParity = 3, PlanHours = 4, PlanOvertime = 5, PlanBlock = 6, PlanBlockCrew = 7, PlanBlockDelete = 8;

        /// <summary>One figure of a plan changed (name by <see cref="RotaPlanRename"/>); the plan is put right and read again. For a block, <paramref name="at"/> is its place.</summary>
        public void RotaPlanChange(string place, int id, int field, int value, int at = 0)
        {
            RotaPlanSet set = RotaPlansOf(place, false);
            RotaPlanFile p = set != null ? set.Find(id) : null;
            if (p == null)
            {
                return;
            }
            switch (field)
            {
                case PlanFrom: p.From = value; break;
                case PlanTo: p.To = value; break;
                case PlanParity: p.Parity = value; break;
                case PlanHours: p.Hours = value; break;
                case PlanOvertime: p.Overtime = value != 0; break;
                case PlanBlockCrew:
                    if (at >= 0 && at < p.Blocks.Count)
                    {
                        RotaBlock b = p.Blocks[at];
                        b.Crew = value;
                        p.Blocks[at] = b;
                    }
                    break;
                case PlanBlockDelete:
                    if (at >= 0 && at < p.Blocks.Count)
                    {
                        p.Blocks.RemoveAt(at);
                    }
                    break;
                case PlanBlock:   // a block added: its hours packed as from * 100 + to; the crew starts at two
                    if (p.Blocks.Count < RotaPlanRules.BlocksMost)
                    {
                        p.Blocks.Add(new RotaBlock { From = value / 100, To = value % 100, Crew = 2 });
                    }
                    break;
            }
            RotaPlanRules.Tidy(p);
            RotaPlansChanged(place);
        }

        public void RotaPlanRename(string place, int id, string name)
        {
            RotaPlanSet set = RotaPlansOf(place, false);
            RotaPlanFile p = set != null ? set.Find(id) : null;
            if (p == null)
            {
                return;
            }
            p.Name = RotaPlanRules.Name(name);
            RotaPlanRules.Tidy(p);
            RotaPlansChanged(place);
        }
    }
}
