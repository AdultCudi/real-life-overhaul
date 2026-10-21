using System;
using System.Collections.Generic;
using System.Globalization;
using Controllers;
using Entities;
using RealLifeOverhaul.Careers;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's branches (Economy/OwnBank/Branch): each game hour every branch's customers come, are seen to at its counter
    /// and are booked in the bank's own books (BranchRules.Hour); their visits of the last week are the bank's reach
    /// (<see cref="Reach"/>, read by OwnBankGame.City). Inside one of them, a free till is the counter the player may work in
    /// the game's own work panel (Jobs/ShiftSkip), clicked as a job's desk is (Careers/DeskGame). Cost: nothing per frame; once
    /// an hour a pass over the save's buildings and staff and a few dozen sums a branch; once a second inside a branch a
    /// look at its tills.
    /// </summary>
    public sealed partial class OwnBankDirector : ISkippableShift
    {
        public const int TabBranches = 4;
        private const int CatchUpHours = 3;      // hours missed that are still worked through one by one
        private const int CounterMinutes = 240;  // a stint at the counter: up to four hours, stopped at any time

        private readonly List<BuildingRegistration> _branchRegs = new List<BuildingRegistration>(4);
        private readonly List<CashRegisterController> _tills = new List<CashRegisterController>(4);
        private Action _onHour;
        private Action<EntityController> _onCounter;
        private string _insidePlace;             // the branch the player stands in, once a second; null outside
        private ShiftSkip _counter;              // the player's stint at a counter, while it is offered or worked
        private CashRegisterController _counterTill;
        private string _counterPlace;            // the branch whose counter the player works, while the clock runs
        private string _counterName;
        private int _counterServed, _counterHours;
        private int _toldLostDay = -1;

        /// <summary>The reach of the bank's branches today, thousandths (BranchRules.Reach); 0 with the option off or no bank running.</summary>
        public int Reach()
        {
            return Running && Settings.EconomyBranch && _bank != null && Save != null ? BranchRules.Reach(_bank, Save.Day) : 0;
        }

        /// <summary>The bank's figures of a branch the player stands in, while its floor is to be shown; null otherwise.</summary>
        public BankBranch BranchInside()
        {
            return Running && Settings.EconomyBranch && _bank != null && _bank.Running && _insidePlace != null ? _bank.Branch(_insidePlace) : null;
        }

        /// <summary>True while the player works the counter of a branch (the floor shows them as its teller).</summary>
        public bool AtCounter(string place) => _counterPlace != null && _counterPlace == place && _counter != null && _counter.Working;

        private int HourNow => Save.Day * 24 + Save.Hour;

        // The branches' hour: every hour since the last, a few at most, each branch's visitors seen to and booked.
        private void OnHour()
        {
            if (!Running || Save == null || _bank == null)
            {
                return;
            }
            try
            {
                int now = HourNow;
                if (!Settings.EconomyBranch || !_bank.Running)
                {
                    _bank.CounterHour = now;   // nothing is owed for the hours a branch could not open
                    return;
                }
                if (now <= _bank.CounterHour)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                int from = _bank.CounterHour == int.MinValue || now - _bank.CounterHour > CatchUpHours ? now : _bank.CounterHour + 1;
                ReadBranches();
                int visitors = 0, served = 0, lost = 0, loans = 0, full = 0;
                long fees = 0;
                int level = Prices.Level, month = GameCalendar.MonthDays, share = ScaleNow();
                for (int hour = from; hour <= now && _bank.Running; hour++)
                {
                    for (int i = 0; i < _branchRegs.Count; i++)
                    {
                        BuildingRegistration reg = _branchRegs[i];
                        string place = BranchGame.Place(reg);
                        BankBranch br = _bank.Branch(place);
                        if (br == null)
                        {
                            continue;
                        }
                        bool player = AtCounter(place);
                        BranchHourIn h = BranchGame.Hour(reg, player);
                        BranchHourOut o = BranchRules.Hour(_bank, br, h, hour, level, month, share);
                        visitors += o.Visitors;
                        served += o.Served;
                        lost += o.Lost;
                        loans += o.Loans;
                        full += o.Full;
                        fees += o.Fees;
                        if (player && o.Served > 0)
                        {
                            int all = BranchRules.Capacity(h);
                            _counterServed += all > 0 ? o.Served * BranchRules.Capacity(new BranchHourIn { Open = true, Player = true }) / all : 0;
                            _counterHours++;
                        }
                        if (place == _insidePlace)
                        {
                            BranchFloor.Instance?.NewHour(br);
                        }
                    }
                }
                _bank.CounterHour = now;
                TellLost();
                _view?.Changed();
                if (Verbose && _branchRegs.Count > 0)
                {
                    bool balanced = !_bank.Running || OwnBankRules.Balanced(_bank);
                    Logger?.Info("Bank branch check (hour " + Save.Hour + " of day " + Save.Day + "): " + _bank.Branches.Count + " of " + _branchRegs.Count + " branch(es) taken in, "
                                 + visitors + " visitor(s), " + served + " seen to, " + lost + " left unseen, " + loans + " loan(s) arranged, " + full
                                 + " turned away as the bank's level takes no more deposits, fees " + fees + " cents waiting for the day; the reach " + Reach() + " thousandths; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms; the books "
                                 + (balanced ? "balance: PASS" : "DO NOT BALANCE: FAIL") + ".");
                }
            }
            catch (Exception e)
            {
                Fault("the branches' hour", e);
            }
        }

        // The player's branches now, each with its figures in the bank: the figures of a branch no longer there are let go
        // first, then a new branch is taken in while the bank's level has room for one (OwnBankRules.BranchRoom). A branch
        // past the level's count is not the bank's: it sees nobody until the bank moves up, and the player is told once a
        // day. The branches taken in before stay, however many the level allows.
        private void ReadBranches()
        {
            BranchGame.Branches(_branchRegs);
            for (int b = _bank.Branches.Count - 1; b >= 0; b--)
            {
                if (!Listed(_bank.Branches[b].Place))
                {
                    Logger?.Info("Bank branch: '" + _bank.Branches[b].Name + "' is no longer one of the player's branches; its figures are let go.");
                    _bank.Branches.RemoveAt(b);
                }
            }
            int today = Save.Day;
            string waiting = null;
            for (int i = 0; i < _branchRegs.Count; i++)
            {
                string place = BranchGame.Place(_branchRegs[i]);
                BankBranch br = _bank.Branch(place);
                if (br == null)
                {
                    if (!_bank.Running || !OwnBankRules.BranchRoom(_bank))
                    {
                        waiting = waiting ?? BranchGame.Name(_branchRegs[i]);
                        continue;
                    }
                    br = new BankBranch { Place = place, MonthFrom = today };
                    _bank.Branches.Add(br);
                    Logger?.Info("Bank branch: '" + BranchGame.Name(_branchRegs[i]) + "' opened for " + _bank.Name + " on day " + today + ".");
                }
                br.Name = BranchGame.Name(_branchRegs[i]);
            }
            if (waiting != null && _bank.Running)
            {
                TellWaiting(waiting);
            }
        }

        private bool Listed(string place)
        {
            for (int i = 0; i < _branchRegs.Count; i++)
            {
                if (BranchGame.Place(_branchRegs[i]) == place)
                {
                    return true;
                }
            }
            return false;
        }

        // Once a day at most: a branch that turned more people away than it saw to today, with nobody at its counter enough.
        private void TellLost()
        {
            int today = Save.Day;
            if (_toldLostDay == today || Save.Hour < 12)
            {
                return;
            }
            for (int i = 0; i < _bank.Branches.Count; i++)
            {
                BankBranch br = _bank.Branches[i];
                if (br.HourIndex / 24 == today && br.HourVisitors > 0 && br.HourServed * 2 < br.HourVisitors && br.HourVisitors >= 3)
                {
                    _toldLostDay = today;
                    Tell(NotificationType.Warning, "reallife_branch_toast_queue", TabBranches, "branch", br.Name,
                        "lost", (br.HourVisitors - br.HourServed).ToString(CultureInfo.InvariantCulture));
                    return;
                }
            }
        }

        // Once a second: the branch the player stands in, its floor, and its counter to be clicked or worked.
        private void BranchSecond()
        {
            BuildingRegistration reg = Settings.EconomyBranch && _bank != null && _bank.Running ? BranchGame.Inside() : null;
            string place = reg != null ? BranchGame.Place(reg) : null;
            if (place != _insidePlace)
            {
                _insidePlace = place;
                if (place != null && _bank.Branch(place) == null)
                {
                    ReadBranches();
                }
                if (place != null)
                {
                    BranchGame.Tills(_tills);
                }
                else
                {
                    _tills.Clear();
                }
            }
            BranchFloor.Instance?.Stand(BranchInside());
            WatchCounter(reg);
            OfferCounter(place != null && _bank.Branch(place) != null ? reg : null);   // a branch waiting for a larger licence has no counter to work
        }

        // A free till of the branch the player is in is the counter they may click; nothing to click elsewhere, or while they work.
        private void OfferCounter(BuildingRegistration reg)
        {
            CashRegisterController free = null;
            if (reg != null && _counter == null)
            {
                for (int i = 0; i < _tills.Count; i++)
                {
                    if (_tills[i] != null && _tills[i].employee == null)
                    {
                        free = _tills[i];
                        break;
                    }
                }
            }
            if (free == null)
            {
                if (DeskGame.OtherTarget != null && ReferenceEquals(DeskGame.OtherClicked, _onCounter))
                {
                    DeskGame.OtherTarget = null;
                    DeskGame.OtherClicked = null;
                }
                return;
            }
            if (!DeskGame.Clickable && !DeskGame.Install())
            {
                return;
            }
            DeskGame.OtherTarget = free;
            DeskGame.OtherClicked = _onCounter;
        }

        // A click on the counter: the player walks to the till, and the game's work panel offers a stint.
        private void CounterClicked(EntityController e)
        {
            BuildingRegistration reg = BranchGame.Inside();
            CashRegisterController till = e as CashRegisterController;
            if (reg == null || till == null || _counter != null || !Running || !_bank.Running)
            {
                return;
            }
            DeskGame.WalkTo(till, () =>
            {
                try
                {
                    OpenCounter(reg, till);
                }
                catch (Exception ex)
                {
                    Fault("the counter", ex);
                }
            });
        }

        private void OpenCounter(BuildingRegistration reg, CashRegisterController till)
        {
            if (_counter != null || till == null || till.employee != null)
            {
                return;
            }
            _counterTill = till;
            _counterName = BranchGame.Name(reg);
            _counterPlace = null;
            _counterServed = _counterHours = 0;
            _counter = ShiftSkip.Offer(this, this);
            if (_counter == null)
            {
                _counterTill = null;
            }
        }

        // While a stint is on: over if the player has left the branch, or the game has put somebody else at the till.
        private void WatchCounter(BuildingRegistration reg)
        {
            if (_counter == null)
            {
                return;
            }
            if (!_counter.Open)
            {
                _counter = null;
                return;
            }
            bool gone = reg == null || BranchGame.Place(reg) != (_counterPlace ?? BranchGame.Place(reg)) || (_counterPlace != null && !BranchGame.PlayerAt(_counterTill));
            if (gone)
            {
                _counter.Close();
                EndCounter(false);
            }
        }

        string ISkippableShift.SkipTitle => OwnBankText.Fill("reallife_branch_counter_title", "branch", _counterName ?? string.Empty);

        int ISkippableShift.SkipMinutes => CounterMinutes;

        LabelInfo[] ISkippableShift.SkipLabels()
        {
            int each = BranchRules.Capacity(new BranchHourIn { Open = true, Player = true });
            BuildingRegistration reg = BranchGame.Inside();
            BranchHourIn h = BranchGame.Hour(reg, false);
            return new[]
            {
                DeskGame.Label(OwnBankText.Fill("reallife_branch_counter_line", "count", each.ToString(CultureInfo.InvariantCulture)), 0),
                DeskGame.Label(OwnBankText.Fill("reallife_branch_counter_staff", "count", h.Tellers.ToString(CultureInfo.InvariantCulture)), 0),
                h.Open ? DeskGame.Label(OwnBankText.T("reallife_branch_counter_hours"), 0) : DeskGame.Closed() ?? DeskGame.Label(OwnBankText.T("reallife_branch_counter_closed"), 0)
            };
        }

        void ISkippableShift.SkipStarted()
        {
            BuildingRegistration reg = BranchGame.Inside();
            if (reg == null || _counterTill == null || _counterTill.employee != null)
            {
                _counter?.Close();
                EndCounter(false);
                return;
            }
            DeskGame.OtherTarget = null;
            DeskGame.SitAt(_counterTill);
            _counterPlace = BranchGame.Place(reg);
            Logger?.Info("Bank branch: the player works the counter of '" + _counterName + "' from hour " + Save.Hour + " of day " + Save.Day + ".");
        }

        void ISkippableShift.SkipWorked(int minutes)
        {
        }

        void ISkippableShift.SkipEnded(int minutes, bool whole)
        {
            EndCounter(true);
        }

        // The stint over, whichever way: the player stands up from the till and is told what they saw to.
        private void EndCounter(bool told)
        {
            CashRegisterController till = _counterTill;
            string name = _counterName, place = _counterPlace;
            _counterTill = null;
            _counterPlace = null;
            _counter = null;
            if (place == null)
            {
                return;   // the stint never began
            }
            if (BranchGame.PlayerAt(till))
            {
                DeskGame.StandFrom(till);
            }
            if (told)
            {
                Tell(NotificationType.Info, "reallife_branch_toast_counter", TabBranches, "branch", name ?? string.Empty,
                    "served", _counterServed.ToString(CultureInfo.InvariantCulture), "hours", _counterHours.ToString(CultureInfo.InvariantCulture));
            }
            Logger?.Info("Bank branch: the player's stint at the counter of '" + name + "' is over after " + _counterHours + " hour(s); about " + _counterServed + " customer(s) seen to by them.");
        }

        // The branches stand down with the bank: a stint is called off, the click taken back, the floor cleared.
        private void BranchesDown()
        {
            if (_counter != null)
            {
                _counter.Close();
                EndCounter(false);
            }
            if (ReferenceEquals(DeskGame.OtherClicked, _onCounter))
            {
                DeskGame.OtherTarget = null;
                DeskGame.OtherClicked = null;
            }
            _insidePlace = null;
            _tills.Clear();
            BranchFloor.Instance?.Stand(null);
        }

        /// <summary>For Options > Tests: an hour of the branch the player stands in (or the first) worked now, and its page shown.</summary>
        public void TestBranchHour()
        {
            const string test = "An hour at my branch";
            if (!TestReady(test, true))
            {
                return;
            }
            if (!Settings.EconomyBranch)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            TestGuard("branch hour", () =>
            {
                ReadBranches();
                BuildingRegistration reg = BranchGame.Inside();
                if (reg == null && _branchRegs.Count > 0)
                {
                    reg = _branchRegs[0];
                }
                BankBranch br = reg != null ? _bank.Branch(BranchGame.Place(reg)) : null;
                if (br == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_branch_test_no_branch");
                    return;
                }
                Keep();
                BranchHourIn h = BranchGame.Hour(reg, AtCounter(br.Place));
                h.Open = true;
                h.Tellers = Math.Max(1, h.Tellers);
                int busiest = 12;   // the lunchtime peak, whatever the hour now
                BranchHourOut o = BranchRules.Hour(_bank, br, h, Save.Day * 24 + busiest, Prices.Level, GameCalendar.MonthDays, ScaleNow());
                if (br.Place == _insidePlace)
                {
                    BranchFloor.Instance?.NewHour(br);
                }
                TestDone("a busy hour at '" + br.Name + "' worked now: " + o.Visitors + " visitor(s), " + o.Served + " seen to, " + o.Opened + " account(s) opened, "
                         + o.Loans + " loan(s) arranged, " + o.Refused + " turned down, fees " + o.Fees + " cents waiting for the day", TabBranches);
            });
        }
    }
}
