using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Population;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// Health: illnesses and injuries that can turn serious, the doctor's appointment, stays in hospital, health insurance, medical debt and sick leave, for the player, the
    /// player's staff, the rivals and the city's people (docs/features/health.md). It owns the state and its timing. A second at a time it looks whether a save is ready, whether
    /// the game put the player in hospital and whether a slice of the people's day is waiting; a game hour it follows Home's illness into the case; a game day it goes through
    /// once (the premium, the debt, the case, the day's draws, the staff, the rivals). Money moves only through <see cref="HealthGame"/>, which books it through the mod's
    /// one call. No Update. COST: nothing per frame; a few comparisons a second, and a slice of the people's pass a second while a day's pass runs.
    /// </summary>
    public sealed partial class HealthDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static HealthDirector Instance { get; private set; }
        public const string TestKey = "reallife:health_test";

        private static readonly int HotDay = HotSpots.Slot("Health: the day's pass");
        private static readonly int HotPeople = HotSpots.Slot("Health: a slice of the people's day");

        private HealthState _state = new HealthState();
        private bool _read, _on, _broken;
        private Action _onSave;
        private int _revision;
        private int _illAtHome, _wave, _pendingNights;
        private int _homeDay = int.MinValue;   // the day _illAtHome was worked out for
        private readonly Func<long, bool> _payPremium, _payBill, _payDebt;
        private readonly Func<long, BillResult> _settleStay, _settleNight;
        private readonly Func<int, int> _file;

        public HealthDirector()
        {
            _payPremium = amount => HealthGame.Pay(amount, HealthGame.PremiumKey);
            _payBill = amount => HealthGame.Pay(amount, HealthGame.BillKey);
            _payDebt = amount => HealthGame.Pay(amount, HealthGame.DebtKey);
            _file = amount => HealthBridges.FileClaim != null ? HealthBridges.FileClaim(amount) : 0;
            _settleStay = bill => SettleBill(Care.Hospital, bill, false);
            _settleNight = bill => SettleBill(Care.Hospital, bill, true);
        }

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            HealthGame.Logger = logger;
            root.AddComponent<HealthDirector>();
        }

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => _read && _on && !_broken && Settings.Health;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public HealthState State => _state;

        /// <summary>Moves whenever something a page shows may have changed.</summary>
        public int Revision => _revision;

        /// <summary>True while a serious illness is held on: Home's illness does not clear up by itself until the hospital has seen it.</summary>
        public bool Holding => Running && _state.Case.Open && _state.Case.Serious && !HealthCatalogue.Is(HealthCatalogue.Injury, _state.Case.Kind);

        /// <summary>True while the player's case rules out hard exercise: anything serious, and a broken arm or a concussion while it heals.</summary>
        public bool NoTraining => Running && _state.Case.Open && (_state.Case.Serious || _state.Case.Kind == Condition.BrokenArm || _state.Case.Kind == Condition.Concussion);

        /// <summary>A line for where the player's needs show: the injury or the serious case, or null (Home says its own illness itself).</summary>
        public string StatusLine()
        {
            if (!Running || !_state.Case.Open)
            {
                return null;
            }
            HealthCase c = _state.Case;
            if (c.Serious)
            {
                return HealthText.Fill("reallife_hc_status_serious", "illness", HealthText.Name(c.Kind));
            }
            return HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind)
                ? HealthText.Fill("reallife_hc_status_injury", "illness", HealthText.Name(c.Kind), "days", Math.Max(1, c.Until - HealthGame.Day).ToString())
                : null;
        }

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            HealthGame.Reset();
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            EndStay();
            HealthGame.DropDrain();   // the game keeps its energy spenders until it is closed: a drain left on would follow the player into another save
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Look();
                }
                catch (Exception e)
                {
                    _broken = true;
                    Logger?.Error(e);
                    Logger?.Warn("Health: switched off for this session after the error above. The save keeps the policy, the debt and the leave as they were.");
                }
            }
        }

        private void Look()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!_read)
            {
                Read();
            }
            SaveHook.Keep(_onSave);
            int day = HealthGame.Day;
            if (!Settings.Health)
            {
                OptionOff(day);   // what is owed stays owed, option or no option: nobody hides from a debt behind a switch
                return;
            }
            if (!_on)
            {
                TurnOn(day);
            }
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            GameNights();
            if (_state.Stay.Active && !_stayRunning)
            {
                ResumeStay();
            }
            PeopleSlice();
            RefreshHome(day);
            if (day != _state.LastDay)
            {
                Pass(day);
            }
            int hour = HealthGame.Hour;
            if (hour != _state.LastHour)
            {
                _state.LastHour = hour;
                HourPass(day);
            }
        }

        // The save's line, read once whatever the option says: the debt in it is collected either way.
        private void Read()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = HealthStore.Load(save.modData, out int version) ?? new HealthState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: every draw comes from it
            }
            save.modData.Remove(TestKey);   // (an older version kept a snapshot of the state here for the test that puts health back; it is not used any more)
            _read = true;
            _on = false;
            _homeDay = int.MinValue;
            HealthGame.DropDrain();   // (the drain of whatever save was before this one; a serious case here puts its own on again)
            _revision++;
            Logger?.Info("Health: " + (version > 0 ? "read the save's line (version " + version + "): plan " + _state.Policy.Plan + ", " + _state.Debt.Owed + " owed, case "
                         + _state.Case.Kind + (_state.Stay.Active ? ", in hospital" : string.Empty) : "nothing yet") + ".");
        }

        private void TurnOn(int day)
        {
            _on = true;
            if (HealthDay.BackOn(_state, day, HealthGame.GameStays, HealthGame.MonthDays))
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_note_lapsed_away", null, OpenAppClick);
            }
            HealthBridges.Credit?.Invoke(-1, 0);   // the medical debt's account on the credit file again
            // What a day's pass works out and the game does not keep: the wave, who is ill at home (when the city's people are there), the drain of a serious case and the
            // injury's mood.
            _wave = HealthRules.Wave(_state.Seed, day, HealthGame.DaysPerYear);
            _homeDay = int.MinValue;
            RefreshHome(day);
            MirrorHome(day);
            _revision++;
        }

        // Who is ill at the player's own home today: worked out once the city's people are there (a load does not wait for them), and nothing while they are away.
        private void RefreshHome(int day)
        {
            if (_homeDay == day)
            {
                return;
            }
            HealthPeople.Mode mode = HealthGame.PeopleMode();
            if (mode == HealthPeople.Mode.NotReady)
            {
                return;   // asked again at the next look
            }
            PopulationState people = mode == HealthPeople.Mode.Ready ? HealthGame.People() : null;
            _illAtHome = people != null ? HealthPeople.IllAtHome(people, _state.Seed, day) : 0;
            _homeDay = day;
            _revision++;
        }

        private void OnSave()
        {
            try
            {
                if (_read)
                {
                    Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[HealthStore.Key] = HealthStore.Write(_state);
            save.modData.Remove(TestKey);
        }

        // The option was switched off: an illness under way ends as far as health goes (Home's own goes on by Home's rules), a stay is cut short and its days billed, the
        // appointment is dropped, the app leaves the phone. The policy's record and what is owed stay in the line, and the debt is still collected.
        private void OptionOff(int day)
        {
            bool was = _on;
            DebtResult r = HealthDay.WhileOff(_state, was, HealthGame.Minutes, day, HealthGame.Level, _settleStay, _payDebt, _file, out long amount);
            if (was)
            {
                EndStay();
                HealthGame.DropDrain();
                Write();
                _on = false;
                RemoveApp();
                _revision++;
                Logger?.Info("Health: switched off; the policy's record, the leave and what is owed are kept in the save, and the debt is still collected.");
            }
            DebtOutcome(r, amount);
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(HealthStore.Key);
                save.modData.Remove(TestKey);
            }
            EndStay();
            HealthGame.DropDrain();
            _state = new HealthState();
            ClearTests();
            _homeDay = int.MinValue;
            _read = false;
            _on = false;
            RemoveApp();
            _revision++;
        }

        // ---- the day

        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            int yearDays = HealthGame.DaysPerYear, monthDays = HealthGame.MonthDays;
            _state.LastDay = day;
            HealthCover.TurnYear(_state, day, yearDays);
            HealthLeave.TurnYear(_state.Leave, day, yearDays);
            _wave = HealthRules.Wave(_state.Seed, day, yearDays);
            PremiumDay(day, yearDays, monthDays);
            DebtDay(day);
            _illAtHome = 0;
            _homeDay = int.MinValue;
            RefreshHome(day);
            ObserveCase(day - 1);   // the case as it was through yesterday, before today's day can clear it: yesterday's working day is settled by it
            CaseDay(day);
            AppointmentLook(day);
            StaffDay(day);
            RivalsDay(day);
            PeopleBegin(day, monthDays);
            ExpireNotes(day);
            _revision++;
        }

        private void HourPass(int day)
        {
            MirrorHome(day);
            ObserveCase(day);
            AppointmentLook(day);
            _revision++;
        }

        private void PremiumDay(int day, int yearDays, int monthDays)
        {
            Plan was = _state.Policy.Plan;
            PremiumResult r = HealthDay.Premium(_state, day, HealthGame.Age, yearDays, monthDays, HealthGame.Level, _payPremium, out int amount);
            if (r == PremiumResult.Missed || r == PremiumResult.Lapsed)
            {
                HealthGame.Tell(NotificationType.Warning, r == PremiumResult.Lapsed ? "reallife_hc_note_lapsed" : "reallife_hc_note_premium_missed",
                    HealthGame.Data("amount", HealthGame.Money(amount), "plan", HealthText.PlanName(was)), OpenAppClick);
            }
            if (r != PremiumResult.None && Verbose)
            {
                Logger?.Info("Health check (the premium on day " + day + "): " + amount + " for " + was + ": " + r + ", next due day " + _state.Policy.NextDue + ": PASS");
            }
        }

        // The debt's day, with the option on or off: an instalment from the cash that is there, a late day otherwise, and the court after too many.
        private void DebtDay(int day)
        {
            if (!_read || _state.Debt.Owed <= 0)
            {
                return;
            }
            DebtResult r = HealthDay.Debt(_state, day, _payDebt, _file, out long amount);
            DebtOutcome(r, amount);
        }

        // What the debt's day came to: the credit file hears of it, and the player is told of the big ones.
        private void DebtOutcome(DebtResult r, long amount)
        {
            if (r == DebtResult.None)
            {
                return;
            }
            HealthBridges.Credit?.Invoke(r == DebtResult.Paid ? 1 : r == DebtResult.Missed ? 2 : r == DebtResult.PaidOff ? 3 : 4, (int)Math.Min(int.MaxValue / 2, amount));
            if (r == DebtResult.PaidOff)
            {
                HealthGame.Tell(NotificationType.Success, "reallife_hc_note_debt_paid_off", null);
            }
            else if (r == DebtResult.Court)
            {
                HealthGame.Tell(NotificationType.Error, "reallife_hc_note_debt_court", HealthGame.Data("amount", HealthGame.Money(amount)));
            }
            else if (r == DebtResult.Missed && _state.Debt.Late == 1)
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_note_debt_missed",
                    HealthGame.Data("amount", HealthGame.Money(amount), "days", HealthCatalogue.DebtClaimAfter.ToString()));
            }
            _revision++;
        }

        private void ExpireNotes(int day)
        {
            for (int i = _state.Notes.Count - 1; i >= 0; i--)
            {
                if (day - _state.Notes[i].Day > HealthCatalogue.PaperKept)
                {
                    _state.Notes.RemoveAt(i);
                }
            }
        }

        // ---- the rivals

        // Each tycoon's day: a rival off sick makes no move and grows at half the pace until they are back; one in hospital pays its bill from the worth over their floor.
        private void RivalsDay(int day)
        {
            int began = HealthRivals.Day(_state, day, _wave, HealthGame.Level, HealthBridges.RivalWorth, HealthBridges.RivalPay, HealthBridges.RivalRest);
            for (int i = 0; began > 0 && Verbose && i < _state.RivalCases.Length; i++)
            {
                RivalHealth r = _state.RivalCases[i];
                if (r.Kind != Condition.None && r.Day == day)
                {
                    Logger?.Info("Health check (rival " + i + " on day " + day + "): " + r.Kind + (r.Serious ? ", in hospital, bill " + r.Bill : string.Empty) + ", off until day "
                                 + r.Until + ": PASS");
                }
            }
        }

        /// <summary>A rival's line for the rich list and their panel while they are off, or empty.</summary>
        public string RivalLine(int tycoon)
        {
            if (!Running || tycoon < 0 || tycoon >= _state.RivalCases.Length)
            {
                return string.Empty;
            }
            RivalHealth r = _state.RivalCases[tycoon];
            return r.Kind != Condition.None && HealthGame.Day <= r.Until ? HealthText.RivalLine(tycoon, r) : string.Empty;
        }

        /// <summary>The rivals who went into hospital yesterday, for the morning's paper.</summary>
        public void Stories(int day, List<HealthStory> into)
        {
            into.Clear();
            if (!Running)
            {
                return;
            }
            for (int i = 0; i < _state.Notes.Count; i++)
            {
                if (_state.Notes[i].Day == day - 1)
                {
                    into.Add(HealthText.Story(_state.Notes[i]));
                }
            }
        }

        // ---- the people and the staff

        // The day turned: its pass of the city's people begins, or waits for the one under way (always in slices, a time skip never makes it a spike). Away (a session, a
        // guest, the living population off) the people are not health's to touch; not ready yet is only a wait, and the pass keeps its place.
        private void PeopleBegin(int day, int monthDays)
        {
            PopHealth o = _state.Pop;
            if (HealthGame.PeopleMode() == HealthPeople.Mode.Away)
            {
                HealthPeople.StandDown(o);
                return;
            }
            HealthPeople.Turn(o, day, HealthRules.MonthOf(day, monthDays));
        }

        private void PeopleSlice()
        {
            PopHealth o = _state.Pop;
            HealthPeople.Mode mode = HealthGame.PeopleMode();
            if (mode == HealthPeople.Mode.Away)
            {
                if (o.Cursor >= 0 || o.Pending > 0)
                {
                    HealthPeople.StandDown(o);
                }
                return;
            }
            if (o.Cursor < 0 && !HealthPeople.Resume(o, HealthGame.MonthDays))
            {
                return;
            }
            PopulationState people = mode == HealthPeople.Mode.Ready ? HealthGame.People() : null;
            if (people == null)
            {
                return;   // not there yet: the cursor stays where it is
            }
            using var hotSpot = HotSpots.Time(HotPeople);
            if (HealthPeople.Slice(people, o, _state.Seed, HealthGame.Level, HealthCatalogue.PassSlice))
            {
                _revision++;
                if (Verbose)
                {
                    Logger?.Info("Health check (the people's day " + o.Day + "): " + o.IllNow + " ill of " + people.People.Live + ", the month's cases " + o.Cases + ", bills "
                                 + o.Bills + ", onto debts " + o.Debt + ": " + (o.IllNow * 1000L <= (long)people.People.Live * HealthCatalogue.WaveShareMost ? "PASS" : "FAIL, over the bound"));
                }
                HealthPeople.Resume(o, HealthGame.MonthDays);   // the day that waited for this one, if any, begins now and is sliced from the next second
            }
        }

        private void StaffDay(int day)
        {
            if (_state.StaffDay == day)
            {
                return;
            }
            _state.StaffDay = day;
            int policy = HealthBridges.SickPolicy != null ? HealthBridges.SickPolicy() : 1;
            int off = HealthGame.StaffDay(_state, day, _wave, policy);
            if (off > 0 && Verbose)
            {
                Logger?.Info("Health check (the staff on day " + day + "): the wave at " + _wave + ", policy " + policy + ": " + off + " off tomorrow by the game's own sick day, "
                             + _state.Staff.Count + " case(s) kept: " + (_state.Staff.Count <= HealthCatalogue.StaffCasesMost ? "PASS" : "FAIL, over the most"));
            }
        }

        // The staff off tomorrow with a case of health's.
        private int StaffOff(int day)
        {
            int off = 0;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                off += day + 1 >= _state.Staff[i].From && day + 1 <= _state.Staff[i].Until ? 1 : 0;
            }
            return off;
        }

        private Action _openApp;

        private Action OpenAppClick => _openApp ?? (_openApp = () => OpenApp());
    }
}
