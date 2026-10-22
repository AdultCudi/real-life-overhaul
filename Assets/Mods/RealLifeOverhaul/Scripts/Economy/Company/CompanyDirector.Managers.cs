using System;
using System.Collections.Generic;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One of the player's places as the managers see it today: what it is, its manager and the district's.</summary>
    internal sealed class ManagedPlace
    {
        public RotaPlace Place;
        public ManagedKind Kind;
        public CompanyShop Shop;        // the company's business there; null for a warehouse, a factory or a place it does not run
        public string District;
        public ManagerFile Own;         // the place's manager; null for none
        public ManagerFile Region;      // the district's regional manager; null for none
    }

    /// <summary>
    /// The managers (docs/features/economy-company.md, "Managers"): a manager per place, appointed from its own staff, and
    /// a regional manager per district from a headquarters, making the kinds of minor decision the player gives them
    /// inside the player's limits, each one written down with what it changed so it can be undone. They act on the game's
    /// hours (ManagerCatalogue: prices at 8, answers at 9, hiring at 11, a Sunday's orders at 12 and its rota at 21) and
    /// on its day (allowances, the month's report); nothing per frame. Switched off, or a manager stood down, nothing is
    /// decided and the company's own notices ask the player again; nothing of the game's is left changed by the switch.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        public const int TabManagers = 6;
        public const int LimitWage = 0, LimitStock = 1, LimitPriceLow = 2, LimitPriceHigh = 3, LimitBudget = 4, LimitKeep = 5;

        private ManagerState _mgr;
        private bool _mgrBroken;
        private readonly List<RotaPlace> _mgrPlaces = new List<RotaPlace>(16), _mgrPlacePool = new List<RotaPlace>(16);
        private readonly List<ManagedPlace> _managed = new List<ManagedPlace>(16), _managedPool = new List<ManagedPlace>(16);
        private readonly List<ManagerFile> _gone = new List<ManagerFile>(2);
        private readonly List<string> _districts = new List<string>(8);

        /// <summary>Whether the managers decide: the company is running, their option is on and no fault has stopped them.</summary>
        public bool ManagersOn => _running && !_broken && !_mgrBroken && Settings.EconomyManagers;

        internal ManagerState ManagerData => _running ? _mgr : null;
        internal IReadOnlyList<ManagedPlace> Managed => _managed;
        internal IReadOnlyList<RotaPlace> ManagerPlaces => _mgrPlaces;

        /// <summary>The districts the player has places in, each once, in the order first met.</summary>
        internal IReadOnlyList<string> ManagerDistricts => _districts;

        internal bool HasDistrict(string district) => district != null && _districts.Contains(district);

        private void ManagerFault(string where, Exception e)
        {
            _mgrBroken = true;
            Logger?.Error(e);
            Logger?.Warn("Company: the managers switched off for this session after an error in " + where
                         + ". Nothing is decided by them any more and the company's notices ask you again; what they decided before stays in their log.");
        }

        partial void ManagerLoad(GameInstance save)
        {
            int version;
            _mgr = ManagerStore.Load(save.modData, out version);
            bool fresh = _mgr == null;
            _mgr = _mgr ?? new ManagerState { MonthFrom = save.Day };
            ManagerRead();
            if (!fresh || Verbose)
            {
                Logger?.Info("Company: managers " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + ": " + _mgr.Managers.Count
                             + " appointed, " + _mgr.Decisions.Count + " decision(s) in the log, the month from day " + _mgr.MonthFrom + ".");
            }
        }

        partial void ManagerSave(GameInstance save)
        {
            if (_mgr != null)
            {
                save.modData[ManagerStore.Key] = ManagerStore.Write(_mgr);
            }
        }

        partial void ManagerStop()
        {
            for (int i = 0; i < _managed.Count; i++)
            {
                _managed[i].Place = null;
                _managed[i].Shop = null;
                _managedPool.Add(_managed[i]);
            }
            _managed.Clear();
            for (int i = 0; i < _mgrPlaces.Count; i++)
            {
                _mgrPlaces[i].Reg = null;
                _mgrPlacePool.Add(_mgrPlaces[i]);
            }
            _mgrPlaces.Clear();
            _districts.Clear();
        }

        /// <summary>
        /// The places and the managers read again: one pass over the save's buildings and staff (RotaGame.ReadPlaces), and a
        /// look-up of each manager. A manager no longer where they work (they left, or were moved) is stood down and said so.
        /// </summary>
        internal void ManagerRead()
        {
            if (_mgr == null || Save == null || !_running)
            {
                return;
            }
            ManagerStop();
            RotaGame.Logger = Logger;
            RotaGame.ReadPlaces(_mgrPlaces, _mgrPlacePool);
            for (int i = 0; i < _mgrPlaces.Count; i++)
            {
                RotaPlace p = _mgrPlaces[i];
                if (p.Type == RotaType.Headquarters || p.Reg == null)
                {
                    continue;
                }
                ManagedPlace mp = _managedPool.Count > 0 ? _managedPool[_managedPool.Count - 1] : new ManagedPlace();
                if (_managedPool.Count > 0)
                {
                    _managedPool.RemoveAt(_managedPool.Count - 1);
                }
                mp.Place = p;
                mp.Shop = ShopAt(p.Place);
                mp.Kind = p.Type == RotaType.Warehouse ? ManagedKind.Warehouse : p.Type == RotaType.Customers && mp.Shop != null ? ManagedKind.Shop : ManagedKind.Works;
                mp.District = ManagerGame.District(p.Reg);
                mp.Own = OwnOf(p.Place);
                mp.Region = null;
                _managed.Add(mp);
                if (!string.IsNullOrEmpty(mp.District) && !_districts.Contains(mp.District))
                {
                    _districts.Add(mp.District);
                }
            }
            _gone.Clear();
            for (int i = 0; i < _mgr.Managers.Count; i++)
            {
                ManagerFile m = _mgr.Managers[i];
                RotaPlace where = PlaceOf(m.Region ? m.Base : m.Place);
                EmployeeInstance e = ManagerGame.Employee(m.Id);
                m.Seen = where != null && e != null && ManagerGame.At(e, where.Reg) && (!m.Region || _districts.Contains(m.Place));
                if (!m.Seen)
                {
                    if (ManagersOn)
                    {
                        _gone.Add(m);   // off, the post is kept as it is and looked at again when they are on
                    }
                    continue;
                }
                m.Name = ManagerGame.NameOf(e);
                m.Skill = ManagerGame.SkillOf(e);
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                ManagerFile m = _gone[i];
                _mgr.Managers.Remove(m);
                string where = m.Region ? CompanyText.District(m.Place) : ManagedName(m.Place);
                Logger?.Info("Company: the " + (m.Region ? "regional manager of " : "manager of ") + where + " (" + m.Name
                             + ") is no longer there, so the post is empty and the decisions are yours again there.");
                if (!string.IsNullOrEmpty(m.Name))
                {
                    Tell(NotificationType.Warning, "reallife_mgr_note_gone", new Dictionary<string, string> { { "name", m.Name }, { "place", where } }, TabManagers, null);
                }
            }
            _gone.Clear();
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace mp = _managed[i];
                mp.Own = OwnOf(mp.Place.Place);
                mp.Region = RegionOf(mp.District);
            }
        }

        private RotaPlace PlaceOf(string place)
        {
            for (int i = 0; i < _mgrPlaces.Count; i++)
            {
                if (string.Equals(_mgrPlaces[i].Place, place, StringComparison.Ordinal))
                {
                    return _mgrPlaces[i];
                }
            }
            return null;
        }

        internal ManagedPlace ManagedAt(string place)
        {
            for (int i = 0; i < _managed.Count; i++)
            {
                if (string.Equals(_managed[i].Place.Place, place, StringComparison.Ordinal))
                {
                    return _managed[i];
                }
            }
            return null;
        }

        internal string ManagedName(string place)
        {
            RotaPlace p = PlaceOf(place);
            return p != null ? p.Name : "?";
        }

        private ManagerFile OwnOf(string place)
        {
            for (int i = 0; _mgr != null && i < _mgr.Managers.Count; i++)
            {
                ManagerFile m = _mgr.Managers[i];
                if (!m.Region && string.Equals(m.Place, place, StringComparison.Ordinal))
                {
                    return m;
                }
            }
            return null;
        }

        internal ManagerFile RegionOf(string district)
        {
            for (int i = 0; _mgr != null && !string.IsNullOrEmpty(district) && i < _mgr.Managers.Count; i++)
            {
                ManagerFile m = _mgr.Managers[i];
                if (m.Region && string.Equals(m.Place, district, StringComparison.Ordinal))
                {
                    return m;
                }
            }
            return null;
        }

        /// <summary>
        /// True where a manager answers this kind of thing: the company then sends no notice asking the player. Never for
        /// the manager's own request or offer: that stays the player's to answer.
        /// </summary>
        internal bool Manages(string place, ManagerTask task, string employee = null)
        {
            if (!ManagersOn || _mgr == null || place == null)
            {
                return false;
            }
            return ManagerRules.Answers(ManagersOn, OwnOf(place), task, employee);
        }

        // Who decides at a place today: its manager, or the district's regional manager standing in for one off sick.
        private bool Decider(ManagedPlace mp, out int skill, out int review, out string who)
        {
            ManagerFile own = mp.Own != null && mp.Own.Seen ? mp.Own : null, region = mp.Region != null && mp.Region.Seen ? mp.Region : null;
            int by = ManagerRules.Decider(own != null, own != null && ManagerGame.Absent(ManagerGame.Employee(own.Id)), own != null ? own.Skill : 0,
                region != null, region != null && ManagerGame.Absent(ManagerGame.Employee(region.Id)), region != null ? region.Skill : 0, out skill, out review);
            who = by == 1 ? own.Name : by == 2 ? region.Name : null;
            return by != 0;   // 0: off sick and nobody to stand in, so the decisions wait a day
        }

        private bool Slips(int skill, int review, int day, string place, ManagerTask task, string subject, out int roll)
        {
            roll = ManagerRules.Roll(_state.Seed, day, place, (int)task, subject);
            return roll < ManagerRules.MistakeChance(skill, review);
        }

        private Decision Record(ManagedPlace mp, ManagerTask task, string subject, string who, int day, int hour, bool mistake)
        {
            Decision d = _mgr.Add(day, hour, mp.Place.Place, task, ManagerStore.Clean(subject), ManagerStore.Clean(who));
            d.Mistake = mistake;
            mp.Own.Decided++;
            mp.Own.Mistakes += mistake ? 1 : 0;
            return d;
        }

        /// <summary>The month's budget of a manager, at today's prices.</summary>
        internal int BudgetOf(ManagerFile m)
        {
            return CompanyCatalogue.AtPrices(ManagerCatalogue.Step(ManagerCatalogue.Budgets, m.Budget), _index);
        }

        /// <summary>A manager's allowance a day, at today's prices.</summary>
        internal int AllowanceOf(ManagerFile m)
        {
            return ManagerRules.Allowance(m.Role, m.Skill, _index);
        }

        // Once a game hour, after the company's own: each kind at its hour (ManagerRules.Due).
        partial void ManagerHour(int day, int hour)
        {
            if (!ManagersOn || _mgr == null || _managed.Count == 0)
            {
                return;
            }
            try
            {
                Hour(day, hour);
            }
            catch (Exception e)
            {
                ManagerFault("their hour", e);
            }
        }

        private void Hour(int day, int hour)
        {
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                if (ManagerRules.Due(day, hour, (ManagerTask)t))
                {
                    Round(day, hour, (ManagerTask)t);
                }
            }
            if (hour == ManagerCatalogue.DigestHour && _mgr.Today > 0 && !_mgrBroken)
            {
                Tell(NotificationType.Info, "reallife_mgr_note_today", new Dictionary<string, string> { { "count", _mgr.Today.ToString() } }, TabManagers, null);
                _mgr.Today = 0;
            }
        }

        private void Round(int day, int hour, ManagerTask task)
        {
            if (_mgrBroken)
            {
                return;
            }
            int before = _mgr.Decisions.Count > 0 ? _mgr.Decisions[_mgr.Decisions.Count - 1].Id : 0;
            try
            {
                for (int i = 0; i < _managed.Count; i++)
                {
                    ManagedPlace mp = _managed[i];
                    int skill, review;
                    string who;
                    if (mp.Own == null || !ManagerRules.Runs(mp.Own.Tasks, mp.Kind, task) || mp.Place.Reg == null
                        || !GoingPublic.Mine(mp.Place.Reg) || !Decider(mp, out skill, out review, out who))
                    {
                        continue;
                    }
                    Decide(task, mp, skill, review, who, day, hour);
                }
            }
            catch (Exception e)
            {
                ManagerFault("their " + task + " decisions", e);
                return;
            }
            int after = _mgr.Decisions.Count > 0 ? _mgr.Decisions[_mgr.Decisions.Count - 1].Id : 0;
            if (after != before)
            {
                Changed();
            }
        }

        private void Decide(ManagerTask task, ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            switch (task)
            {
                case ManagerTask.Raises: DecideRaises(mp, skill, review, who, day, hour); break;
                case ManagerTask.Offers: DecideOffers(mp, skill, review, who, day, hour); break;
                case ManagerTask.Overtime: DecideOvertime(mp, skill, review, who, day, hour); break;
                case ManagerTask.Claims: DecideClaims(mp, skill, review, who, day, hour); break;
                case ManagerTask.Stock: DecideStock(mp, skill, review, who, day, hour); break;
                case ManagerTask.Prices: DecidePrices(mp, skill, review, who, day, hour); break;
                case ManagerTask.Rota: DecideRota(mp, skill, review, who, day, hour); break;
                case ManagerTask.Hiring: DecideHiring(mp, skill, review, who, day, hour); break;
            }
        }

        // The game's day: the places read again, yesterday's allowances paid, the month's report, and decisions too late to undo closed.
        partial void ManagerDay(int day)
        {
            if (_mgr == null)
            {
                return;
            }
            if (!ManagersOn)
            {
                return;   // off: nothing is read, paid or said, as without the feature
            }
            try
            {
                ManagerRead();
                int paid = 0;
                for (int i = 0; i < _mgr.Managers.Count; i++)
                {
                    ManagerFile m = _mgr.Managers[i];
                    int amount = AllowanceOf(m);
                    CompanyShop shop = m.Region ? null : ShopAt(m.Place);
                    Cost(amount, CompanyGame.ManagerKey, shop, m.Name, CostKind.Wages, day - 1,
                        (m.Region ? "the regional manager's allowance, " : "the manager's allowance, ") + "skill " + m.Skill + ", for day " + (day - 1), true);
                    m.Paid += amount;
                    paid += amount;
                }
                if (ManagerRules.MonthOver(_mgr.MonthFrom, day, MonthDays))
                {
                    MonthReport(day);
                }
                int closed = CloseOld(day);
                if (Verbose)
                {
                    Logger?.Info("Company check (managers, day " + day + "): " + _mgr.Managers.Count + " manager(s) over " + _managed.Count + " place(s), allowances "
                                 + paid + " for day " + (day - 1) + ", " + closed + " decision(s) now too late to undo, " + _mgr.Decisions.Count
                                 + " in the log (kept " + ManagerCatalogue.DecisionsKept + "): PASS");
                }
            }
            catch (Exception e)
            {
                ManagerFault("their day", e);
            }
        }

        private void MonthReport(int day)
        {
            int decided = 0, mistakes = 0, undone = 0, paid = 0, spent = 0;
            for (int i = 0; i < _mgr.Managers.Count; i++)
            {
                ManagerFile m = _mgr.Managers[i];
                decided += m.Decided;
                mistakes += m.Mistakes;
                undone += m.Undone;
                paid += m.Paid;
                spent += m.Spent;
                m.EndMonth();
            }
            Logger?.Info("Company: the managers' month from day " + _mgr.MonthFrom + " to day " + day + ": " + decided + " decision(s), " + mistakes + " mistake(s), "
                         + undone + " undone, allowances " + paid + ", " + spent + " of their budgets spent.");
            _mgr.MonthFrom = day;
            if (_mgr.Managers.Count > 0)
            {
                Tell(NotificationType.Info, "reallife_mgr_note_month", new Dictionary<string, string>
                {
                    { "decisions", decided.ToString() }, { "mistakes", mistakes.ToString() }, { "undone", undone.ToString() }, { "amount", CompanyText.Money(paid) }
                }, TabManagers, null);
            }
        }

        // Decisions that can no longer be undone are closed, so the log says so and nothing is tried on them again.
        private int CloseOld(int day)
        {
            int closed = 0;
            string why;
            for (int i = 0; i < _mgr.Decisions.Count; i++)
            {
                Decision d = _mgr.Decisions[i];
                if (d.Open && UndoState(d, day, out why) == UndoNever)
                {
                    d.State = DecisionState.Closed;
                    closed++;
                }
            }
            return closed;
        }

        // --- What the player does on the Managers tab ---

        /// <summary>One of a place's staff made its manager: a new post, or the one there replaced. Limits and kinds stay with the post.</summary>
        public void ManagerAppoint(string place, string id)
        {
            if (!ManagersOn || _mgr == null)
            {
                return;
            }
            ManagedPlace mp = ManagedAt(place);
            EmployeeInstance e = ManagerGame.Employee(id);
            if (mp == null || e == null || !ManagerGame.At(e, mp.Place.Reg) || !GoingPublic.Mine(mp.Place.Reg))
            {
                Notify(NotificationType.Warning, "reallife_mgr_note_cannot", null, 7f);
                return;
            }
            ManagerFile other = _mgr.Of(id);
            if (other != null && other != mp.Own)
            {
                _mgr.Managers.Remove(other);   // somebody manages one place at a time: moved here, the other post is empty
            }
            ManagerFile m = mp.Own;
            bool fresh = m == null;
            if (fresh)
            {
                if (_mgr.Managers.Count >= ManagerCatalogue.ManagersMost)
                {
                    Notify(NotificationType.Warning, "reallife_mgr_note_most", new Dictionary<string, string> { { "count", ManagerCatalogue.ManagersMost.ToString() } }, 7f);
                    return;
                }
                m = new ManagerFile { Place = place, Role = ManagerCatalogue.RolePlace, Tasks = ManagerRules.Kinds(mp.Kind), Keep = mp.Place.Staff };
                _mgr.Managers.Add(m);
            }
            m.Id = id;
            m.From = Save.Day;
            m.Name = ManagerGame.NameOf(e);
            m.Skill = ManagerGame.SkillOf(e);
            m.Seen = true;
            mp.Own = m;
            SnapNow(mp, Save.Day, Save.Hour);
            Logger?.Info("Company: " + m.Name + " (skill " + m.Skill + ") " + (fresh ? "appointed manager of " : "now manages ") + mp.Place.Name
                         + ", allowance " + AllowanceOf(m) + " a day; kinds " + m.Tasks + ", limits: wage band step " + m.WageBand + ", stock " + m.StockDays
                         + ", prices " + m.PriceLow + " to " + m.PriceHigh + ", budget " + m.Budget + ", keeps " + m.Keep + " staff.");
            Notify(NotificationType.Success, "reallife_mgr_note_appointed", new Dictionary<string, string> { { "name", m.Name }, { "place", mp.Place.Name } }, 8f);
            ManagerRead();
            Changed();
        }

        /// <summary>Somebody at a headquarters made the regional manager of a district.</summary>
        public void ManagerAppointRegion(string district, string id)
        {
            if (!ManagersOn || _mgr == null || string.IsNullOrEmpty(district) || !_districts.Contains(district))
            {
                return;
            }
            EmployeeInstance e = ManagerGame.Employee(id);
            RotaPlace office = null;
            for (int i = 0; e != null && i < _mgrPlaces.Count && office == null; i++)
            {
                office = _mgrPlaces[i].Type == RotaType.Headquarters && ManagerGame.At(e, _mgrPlaces[i].Reg) ? _mgrPlaces[i] : null;
            }
            if (office == null || ManagerGame.OnAPlan(id))
            {
                Notify(NotificationType.Warning, "reallife_mgr_note_cannot", null, 7f);
                return;
            }
            ManagerFile other = _mgr.Of(id);
            ManagerFile m = RegionOf(district);
            if (other != null && other != m)
            {
                _mgr.Managers.Remove(other);
            }
            if (m == null)
            {
                if (_mgr.Managers.Count >= ManagerCatalogue.ManagersMost)
                {
                    Notify(NotificationType.Warning, "reallife_mgr_note_most", new Dictionary<string, string> { { "count", ManagerCatalogue.ManagersMost.ToString() } }, 7f);
                    return;
                }
                m = new ManagerFile { Place = district, Role = ManagerCatalogue.RoleRegion };
                _mgr.Managers.Add(m);
            }
            m.Id = id;
            m.Base = office.Place;
            m.From = Save.Day;
            m.Name = ManagerGame.NameOf(e);
            m.Skill = ManagerGame.SkillOf(e);
            m.Seen = true;
            Logger?.Info("Company: " + m.Name + " (skill " + m.Skill + ", at " + office.Name + ") appointed regional manager of " + CompanyText.District(district)
                         + ", allowance " + AllowanceOf(m) + " a day.");
            Notify(NotificationType.Success, "reallife_mgr_note_appointed", new Dictionary<string, string> { { "name", m.Name }, { "place", CompanyText.District(district) } }, 8f);
            ManagerRead();
            Changed();
        }

        /// <summary>A post emptied: nothing more is decided there, and the company's notices ask the player again.</summary>
        public void ManagerStandDown(string place, bool region)
        {
            if (_mgr == null)
            {
                return;
            }
            ManagerFile m = region ? RegionOf(place) : OwnOf(place);
            if (m == null)
            {
                return;
            }
            _mgr.Managers.Remove(m);
            string where = region ? CompanyText.District(place) : ManagedName(place);
            Logger?.Info("Company: " + m.Name + " stood down as " + (region ? "regional manager of " : "manager of ") + where
                         + ". Their decisions stay in the log and can still be undone where it is not too late; the products they left to you are let go with the post.");
            Notify(NotificationType.Info, "reallife_mgr_note_stood_down", new Dictionary<string, string> { { "name", m.Name }, { "place", where } }, 8f);
            ManagerRead();
            Changed();
        }

        /// <summary>A kind of decision given to a place's manager, or taken back.</summary>
        public void ManagerTaskSet(string place, ManagerTask task, bool on)
        {
            ManagedPlace mp = ManagedAt(place);
            ManagerFile m = mp != null ? mp.Own : null;
            if (m == null || !ManagerRules.Has(ManagerRules.Kinds(mp.Kind), task))
            {
                return;
            }
            m.Tasks = on ? m.Tasks | ManagerRules.Bit(task) : m.Tasks & ~ManagerRules.Bit(task);
            if (on && task == ManagerTask.Hiring && m.Keep < 0)
            {
                m.Keep = mp.Place.Staff;
            }
            if (on && task == ManagerTask.Stock)
            {
                SnapNow(mp, Save.Day, Save.Hour);
            }
            if (!on && task == ManagerTask.Rota)
            {
                ManagerRotaDropped(place);   // a week of theirs still waiting is not written (one under way is stopped before it writes)
            }
            Logger?.Info("Company: the manager of " + mp.Place.Name + " " + (on ? "now decides " : "no longer decides ") + task + ".");
            Changed();
        }

        /// <summary>One of a place's limits set to a step of its list (or, for the staff kept, a number).</summary>
        public void ManagerLimit(string place, int limit, int step)
        {
            ManagerFile m = OwnOf(place);
            if (m == null)
            {
                return;
            }
            switch (limit)
            {
                case LimitWage: m.WageBand = CompanyCatalogue.Clamp(step, 0, ManagerCatalogue.WageBands.Length - 1); break;
                case LimitStock: m.StockDays = CompanyCatalogue.Clamp(step, 0, ManagerCatalogue.StockDays.Length - 1); break;
                case LimitPriceLow: m.PriceLow = CompanyCatalogue.Clamp(step, 0, ManagerCatalogue.PriceLows.Length - 1); break;
                case LimitPriceHigh: m.PriceHigh = CompanyCatalogue.Clamp(step, 0, ManagerCatalogue.PriceHighs.Length - 1); break;
                case LimitBudget: m.Budget = CompanyCatalogue.Clamp(step, 0, ManagerCatalogue.Budgets.Length - 1); break;
                case LimitKeep: m.Keep = CompanyCatalogue.Clamp(step, 0, ManagerCatalogue.KeepMost); break;
            }
            Logger?.Info("Company: a limit of the manager of " + ManagedName(place) + " set: " + limit + " to " + step + ".");
            Changed();
        }

        /// <summary>The products a place's manager left to the player given back to the manager.</summary>
        public void ManagerHandBack(string place)
        {
            ManagerFile m = OwnOf(place);
            if (m == null || m.Held.Count == 0)
            {
                return;
            }
            Logger?.Info("Company: " + m.Held.Count + " product(s) at " + ManagedName(place) + " handed back to its manager.");
            m.Held.Clear();
            Changed();
        }

        // A product the player has changed or taken back: the manager leaves it alone from now on.
        private static void Hold(ManagerFile m, string item)
        {
            if (m != null && !m.Holds(item) && m.Held.Count < ManagerCatalogue.HeldMost)
            {
                m.Held.Add(item);
            }
        }

        /// <summary>The kinds a place has now that need a part of the company that is off; a bit each.</summary>
        internal int ManagerBlocked(ManagedPlace mp)
        {
            int blocked = 0;
            if (!On(CompanyPart.Staff))
            {
                blocked |= ManagerRules.Bit(ManagerTask.Raises) | ManagerRules.Bit(ManagerTask.Offers) | ManagerRules.Bit(ManagerTask.Overtime);
            }
            if (!On(CompanyPart.Cover))
            {
                blocked |= ManagerRules.Bit(ManagerTask.Claims);
            }
            if (!RotaOn)
            {
                blocked |= ManagerRules.Bit(ManagerTask.Rota);
            }
            if (mp != null && mp.Kind == ManagedKind.Shop && (ManagerGame.PricingManagerCovers(mp.District) || CompanyBridges.PricesKeptAt(mp.Place.Place)))
            {
                blocked |= ManagerRules.Bit(ManagerTask.Prices);
            }
            return blocked;
        }

        /// <summary>For the save tools: the managers' line is gone with the rest.</summary>
        private void ManagerForget()
        {
            _mgr = null;
            ManagerStop();
        }
    }
}
