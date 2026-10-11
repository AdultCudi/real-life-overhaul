using System;
using BigAmbitions.Characters;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Contract cleaner. A business near the player that is not theirs, and is open, is dirty: walk to its door, go
    /// in, and clean its floor to 100 %, the way the player cleans their own shop.
    ///
    /// It is the game's own cleaning, not a copy of it. On going in, the client's floor is given the game's own dirt
    /// (<see cref="JobDirt"/>: the game's floor objects, its dirtiness values, its cleanliness percentage), the
    /// player is handed the game's own mop (<see cref="JobMop"/>) and the game's dirt highlighting comes on with it.
    /// The player clicks the dirty patches; the game walks them there, plays its cleaning stance and takes the dirt
    /// off at its own pace. The job reads the game's percentage and pays by how much of the dirt the player took off:
    /// the whole fee at 100 %, the share done at any point before. Half or more is a shift done; less is a failed one.
    ///
    /// The shift goes on inside that one building (<see cref="WorksInside"/>); going into any other ends it, and the
    /// player may step out and come back (the dirt they left is kept, off the client's registration while they are out).
    /// If the game's dirt cannot be had on a floor, the job falls back to the mod's own spills (CleanerJob.Spills.cs).
    ///
    /// IN THE GAME'S OWN INTERFACE. The player works in the cleaner's uniform (<see cref="JobUniform"/>). The game's
    /// panel over a place (<see cref="JobPanel"/>) stands over the client's door with "Go in", and over the player
    /// while they are not mopping with "Take the mop" (if they have put it away) and "Let time pass", which hands the rest of the
    /// floor to the game's clock (<see cref="ShiftClock"/>): <see cref="ShiftRates"/> minutes an eighth of the floor, at
    /// <see cref="ShiftRates.Share"/> of that share's pay. The figures are on the HUD in a copy of the game's job panel
    /// (<see cref="ShiftFigures"/>): the cleanliness and its target.
    /// </summary>
    public sealed partial class CleanerJob : Job, IClockedShift
    {
        private enum Step { ToClient, Floor, Mopping }
        private enum Mode { Undecided, Game, Spills }

        private const float Debounce = 0.7f;           // seconds after a mopping in which the panel's "let time pass" does nothing
        private const float PanelUp = 1.6f;            // metres over the door the panel stands
        private const float PanelOver = 2.1f;          // ...and over the player
        private const float Settle = 0.6f;             // seconds after going in for the shop to finish arriving round the player
        private const string ButtonEnter = "reallife_job_button_enter";
        private const string ButtonMop = "reallife_job_cleaner_button_mop";
        private const string ButtonTake = "reallife_job_cleaner_button_take";
        private const string ButtonSkip = "reallife_job_button_skip";

        private const float ClientMin = 15f;
        private const float ClientMax = 260f;
        private const float DoorReach = 3.2f;
        private const float Seconds = 330f;            // for the whole floor, the walk there included
        private const float SecondsPerMetre = 0.5f;    // more for a client further off
        private const float Spotless = 100f;           // the game's cleanliness at which a floor shows no dirt
        private const float RedoShare = 0.3f;          // share of the dirtied cells a difficult client has put back
        private const float RedoStrength = 0.8f;       // ...at this share of the dirt they had
        private const float Half = 0.5f;               // share of the floor at which a shift is done rather than failed
        private const string MopClip = "mop_floor";

        private static readonly string[] PercentText = new string[101];

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Cleaner);
        private readonly JobPanel _panel;
        private readonly JobDirt _dirt = new JobDirt();
        private JobMop _mop;
        private ShiftFigures _figures;
        private ShiftClock _clock;
        private Action _onSave;

        private Step _step;
        private Mode _mode;
        private DoorSpot _client;
        private string _clientName;
        private int _units;            // how big the shift is: the fee is this many times a unit's pay at 100 %
        private float _endsAt;
        private System.Random _dice;
        private float _start;          // the game's cleanliness when the dirt was laid
        private float _clean;          // ...and the last reading of it
        private int _pct = -1;         // ...as the whole number on the panel
        private int _clockUnits;       // units of the floor the clock did
        private int _leftAtClock;      // units that were left when the clock began
        private int _clockPay;         // what the clock was paid
        private float _mopOver = -10f;
        private float _arriveAt;
        private bool _arrived;
        private bool _autoMop;        // hand the mop over as soon as the hands are empty
        private bool _wasCleaning;
        private bool _wasInside;
        private bool _paid;
        private bool _redone;          // the difficult client has had their say
        private bool _dressed;
        private int _hint = -1;
        private int _shownSeconds = int.MinValue;
        private string _panelDoor;
        private string _panelHands;
        private string _panelClean;
        private string _panelTarget;
        private string _objClock;
        private string _doorLabel;

        public CleanerJob()
        {
            _panel = new JobPanel("cleaner panel", Act, OfferSkip);
        }

        public override JobDef Def => _def;

        public override bool PassingTime => _clock != null && _clock.Working;

        /// <summary>While the panel is up its first button is on the game's interact key: the mod's own use of that key stands aside.</summary>
        public override bool OwnsInteractKey => _panel.Up;

        int IClockedShift.ClockLeft => LeftUnits();

        float IClockedShift.ClockPay => UnitPay();

        /// <summary>Walking out on a floor that is mostly still dirty is a bad shift.</summary>
        public override bool QuitIsFailure => TotalShare > 0f && TotalShare < Half;

        public override bool WorksInside(Buildings.Building building)
        {
            return _client.Building != null && ReferenceEquals(_client.Building.building, building);
        }

        // ---------------------------------------------------------------- how much of the floor is done

        private float GameShare => _start >= Spotless ? 1f : Mathf.Clamp01((_clean - _start) / (Spotless - _start));

        /// <summary>The share of the dirt that has been taken off, by hand and by the clock: 0 to 1.</summary>
        private float TotalShare => _mode == Mode.Game ? GameShare : Mathf.Clamp01(_mopped / (float)_units);

        /// <summary>...of which by hand: what the player is paid for in the end (the clock was paid as it went).</summary>
        private float HandShare => Mathf.Max(0f, TotalShare - _clockUnits / (float)_units);

        /// <summary>Units of the floor still to do (a part of a unit counts as a whole one).</summary>
        private int LeftUnits()
        {
            return Mathf.Clamp(_units - Mathf.FloorToInt(TotalShare * _units + 0.001f), 0, _units);
        }

        private static string Percent(int value)
        {
            return PercentText[value] ?? (PercentText[value] = value.ToString() + "%");
        }

        // The game's own cleanliness is a whole number; this reads it while the dirt is on the registration.
        private void ReadClean()
        {
            if (_dirt.InPlace)
            {
                _clean = _dirt.Cleanliness;
            }
        }

        // ---------------------------------------------------------------- begin

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            if (!(ClipLibrary.Ready && ClipLibrary.TryGet(MopClip, out _)) && !Ctx.Self.Knows(PermanentAnimationType.Cleaning))
            {
                Refusal = "reallife_job_no_here";
                Ctx.Logger?.Info("Jobs: no cleaning work: neither the mop clip nor the game's own cleaning animation can be played on the player.");
                return false;
            }
            _units = ShiftSize();   // the offer's floor: the bigger it is, the dirtier and the better paid
            if (_units <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            if (!JobSpots.RandomDoor(Ctx.PlayerPosition, ClientMin, ClientMax, null, 0f, out _client, JobSites.AnyClient, 96))
            {
                Refusal = "reallife_job_no_client";
                return false;
            }
            _clientName = JobSites.NameOf(_client);
            _doorLabel = JobText.T("reallife_job_cleaner_marker_door");
            _panelDoor = JobText.T("reallife_job_cleaner_panel_door", "place", _clientName);
            _panelHands = JobText.T("reallife_job_obj_hands");
            _panelClean = JobText.T("reallife_job_cleaner_panel_clean");
            _panelTarget = JobText.T("reallife_job_cleaner_panel_target");
            _objClock = JobText.T("reallife_job_cleaner_obj_clock");
            _endsAt = now + Seconds + JobSpots.Flat(Ctx.PlayerPosition, _client.Front) * SecondsPerMetre;
            _dice = new System.Random(Ctx.Offer.Seed != 0u ? unchecked((int)Ctx.Offer.Seed) : unchecked(Environment.TickCount * 397 + _units));   // where the dirt lies is part of the offer's seed
            BeginSpeed(now, _endsAt - now);
            _mop = new JobMop(Ctx.Logger);
            _step = Step.ToClient;
            _mode = Mode.Undecided;
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_cleaner_card_done");
            _clock = new ShiftClock(Ctx, this, this, "cleaner", "reallife_job_cleaner_unit", "reallife_job_cleaner_units", "reallife_job_cleaner_slip");
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // a save never carries the job's dirt or its mop
            Mark(_client.Front, _doorLabel);
            Hint(0);
            Figures(now);
            Ctx.Logger?.Info("Jobs: cleaning at " + _clientName + " (" + _client.Name + "), a floor of " + _units + " units.");
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: " + JobSpots.Flat(Ctx.PlayerPosition, _client.Front).ToString("F0") + " m to the client's door; shown in the game's own interface: job panel "
                                 + (_figures.Shown ? "copied" : "NOT copied (the panels over the door and the player are all there is)") + ", " + ShiftRates.Minutes(Def)
                                 + " game minutes a unit by the clock at " + ShiftRates.Percent + "%.");
            }
            return true;
        }

        // The game is about to write a save: the client's registration and the player's hands are as the game had them while it does.
        private void OnSaveGame()
        {
            if (Ctx == null || Over)
            {
                return;
            }
            ReadClean();
            _dirt.Suspend();
            if (JobMop.Holding)
            {
                _mop.Take();
                _autoMop = true;   // handed back on the next tick
            }
        }

        // ---------------------------------------------------------------- tick

        public override void Tick(float now)
        {
            if (_clock.Hold())
            {
                Figures(now);   // the clock is doing the work (or the offer is up, with the game paused)
                return;
            }
            if (!_dressed && !Ctx.Driving && !PlayerHelper.IsUsingVehicle && Ctx.Player.Character.visible)
            {
                _dressed = true;
                Ctx.Uniform.Wear(Ctx.Player, CityLook.Cleaner);   // once: the director takes it off however the shift ends
            }
            if (now >= _endsAt && _step != Step.Mopping)
            {
                Ctx.Say("reallife_job_cleaner_time", 8f);
                _panel.Hide();
                Conclude();
                return;
            }
            bool inside = JobSites.Inside(_client);
            if (inside != _wasInside)
            {
                _wasInside = inside;
                _hint = -1;
                if (inside)
                {
                    _step = Step.Floor;
                    _arrived = false;
                    _arriveAt = now + Settle;   // a moment for the shop to finish arriving round them
                }
                else
                {
                    LeftPremises();
                }
            }
            switch (_step)
            {
                case Step.ToClient: ToClient(); break;
                case Step.Floor: Floor(now); break;
                case Step.Mopping: Mopping(now); break;
            }
            if (!Over)
            {
                Figures(now);
            }
        }

        // The player is out of the shop: its registration is the game's again, the mop is back, and the dirt waits for them.
        private void LeftPremises()
        {
            ReadClean();
            _mop.Take();
            _dirt.Release();
            StopMopping();
            ClearPatch();
            _hasPatch = false;
            _panel.Hide();
            _step = Step.ToClient;
        }

        // The shift's figures on the card: each line is written only when what it says changes.
        private void Figures(float now)
        {
            bool clock = _clock.Working;
            if (clock)
            {
                Show(_objClock);
            }
            _figures.Objective(Objective);
            if (_mode == Mode.Game)
            {
                _figures.Units(Mathf.Max(0, _pct), 100);
            }
            else if (_mode == Mode.Spills)
            {
                _figures.Units(Mathf.RoundToInt((_mopped + _clock.Done) * 100f / _units), 100);   // the same line: how much of the dirt is off
            }
            int seconds = clock ? int.MaxValue : Mathf.Max(0, Mathf.CeilToInt(_endsAt - now));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _figures.Limit(clock ? null : JobText.T("reallife_job_card_time_left", "time", ShiftFigures.Clock(seconds)), seconds <= 60 ? WorldPanel.Yellow() : _panel.White);
            }
            _figures.Money(Mathf.RoundToInt(HandShare * _units * UnitPay()) + _clockPay, 0);
            _figures.Time(Mathf.Max(0, LeftUnits() - (clock ? _clock.Done : 0)) * _clock.MinutesEach);
        }

        private void ToClient()
        {
            Mark(_client.Front, _doorLabel);
            Hint(TotalShare > 0f ? 1 : 0);
            if (!Ctx.Driving && !PlayerHelper.IsUsingVehicle && JobSpots.Flat(Ctx.PlayerPosition, _client.Front) < DoorReach)
            {
                _panel.Offer(_panelDoor, _panelTarget, Percent(100), _panel.White, ButtonEnter, true, null, false);
                _panel.Show(_client.Front + Vector3.up * PanelUp, Ctx.Player);
            }
            else
            {
                _panel.Hide();
            }
        }

        // ---------------------------------------------------------------- on the floor

        private void Floor(float now)
        {
            if (!_arrived)
            {
                if (now < _arriveAt)
                {
                    return;
                }
                _arrived = true;
                Unmark();
                if (_mode == Mode.Undecided)
                {
                    Decide();
                }
                if (_mode == Mode.Game)
                {
                    _dirt.Adopt();
                    _autoMop = true;
                    ReadClean();
                }
            }
            if (_mode == Mode.Game)
            {
                FloorGame(now);
            }
            else
            {
                FloorSpills(now);
            }
        }

        // The game's dirt if the floor can have it; the mod's own spills if not (said once in the log).
        private void Decide()
        {
            string why = "the game's cleaning animation cannot be played on the player";
            bool laid = false;
            if (Ctx.Self.Knows(PermanentAnimationType.Cleaning))
            {
                try
                {
                    laid = _dirt.Lay(_client.Building != null ? _client.Building.buildingRegistration : null, _units, _dice, out why);
                }
                catch (Exception e)
                {
                    Ctx.Logger?.Error(e);
                    why = e.GetType().Name;
                    _dirt.Discard();
                }
            }
            if (!laid)
            {
                _mode = Mode.Spills;
                Ctx.Logger?.Info("Jobs: cleaner: the game's own dirt could not be had at " + _clientName + " (" + why + "); this job uses the mod's own spills instead.");
                return;
            }
            _mode = Mode.Game;
            _clean = _dirt.Cleanliness;
            _start = _clean;
            _pct = Mathf.FloorToInt(_clean + 0.001f);
            Ctx.Logger?.Info("Jobs: cleaner: the game's own dirt is down at " + _clientName + ": " + _dirt.Dirtied + " of " + _dirt.Cells + " floor cells, " + _dirt.Mean.ToString("F0")
                             + " dirty on average; the game reads the floor " + _pct + "% clean, the job wants 100%.");
        }

        private void FloorGame(float now)
        {
            _dirt.Resume();   // after a save
            ReadClean();
            bool cleaning = JobMop.Cleaning;
            if (cleaning != _wasCleaning)
            {
                _wasCleaning = cleaning;
                _hint = -1;
                if (!cleaning)
                {
                    _mopOver = now;
                    _mop.Show(_clean);   // the game sets its bar back to 100 when a spot is done: this client's floor is not the player's
                }
            }
            int pct = Mathf.FloorToInt(_clean + 0.001f);
            if (pct != _pct)
            {
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs: cleaner: the floor reads " + pct + "% clean (was " + _pct + "%); " + Mathf.RoundToInt(GameShare * 100f) + "% of the dirt is off, "
                                     + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the floor's time left.");
                }
                _pct = pct;
                _mop.Show(_clean);
            }
            if (_clean >= Spotless - 0.001f && Ctx.Offer.RedoAt > 0 && !_redone)
            {
                // The difficult client, if the offer drew one, finds a patch they say was missed: some of the dirt is put back, and the pay follows the floor.
                _redone = true;
                int back = _dirt.Redo(RedoShare, RedoStrength);
                ReadClean();
                Ctx.Say("reallife_gig_redo_clean", 7f);
                Ctx.Logger?.Info("Jobs: cleaner event: a difficult client points out a missed patch: " + back + " cells are dirty again, the floor reads " + Mathf.FloorToInt(_clean + 0.001f) + "% clean.");
                return;
            }
            if (_clean >= Spotless - 0.001f)
            {
                if (_clockUnits == 0)
                {
                    EndSpeed(now);   // the whole floor by hand
                }
                Ctx.Say("reallife_job_cleaner_done", 7f);
                _panel.Hide();
                Conclude();
                return;
            }
            bool holding = JobMop.Holding;
            bool full = JobMop.HandsFull;
            if (_autoMop && !holding && !full)
            {
                _autoMop = false;
                holding = _mop.Give();
                if (holding)
                {
                    _mop.Show(_clean);
                    if (Meta.Dev.Verbose)
                    {
                        Ctx.Logger?.Info("Jobs: cleaner: the game's mop is in the player's hands (the highlighting is the game's, with it).");
                    }
                }
            }
            if (cleaning)
            {
                _panel.Hide();   // the game's own stance and cursor are on; nothing of the mod's stands over them
                Hint(3);
                return;
            }
            Hint(full ? 4 : holding ? 2 : 5);
            _panel.Offer(full ? _panelHands : _panelDoor, _panelClean, Percent(pct), _panel.White, holding ? null : ButtonTake, !full, ButtonSkip, !full);
            _panel.Show(Ctx.Player.transform, PanelOver, Ctx.Player);
        }

        // The panel's first button was pressed (the game ran it on its key or on a click).
        private void Act(string action)
        {
            if (Ctx == null || Over)
            {
                return;
            }
            if (ReferenceEquals(action, ButtonEnter))
            {
                if (_client.Building != null)
                {
                    _client.Building.Interact();   // the game's own way in: it says so itself if the business has just shut
                }
                return;
            }
            if (ReferenceEquals(action, ButtonTake) && _step == Step.Floor && _mode == Mode.Game)
            {
                if (_mop.Give())
                {
                    _panel.Hide();
                    _hint = -1;
                }
                return;
            }
            if (ReferenceEquals(action, ButtonMop))
            {
                ActMop();
            }
        }

        // ---------------------------------------------------------------- the end

        private void Conclude()
        {
            Pay();
            // A floor with no room to work on is nobody's fault; half the floor or more is a shift done.
            Finish(TotalShare >= Half || _tries >= MostTries ? JobEnd.Done : JobEnd.Failed);
        }

        private void Pay()
        {
            if (!Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: pay slip: " + Mathf.RoundToInt(HandShare * 100f) + "% of the floor by hand (" + _units + " units at " + UnitPay() + ") = " + slip.Base
                                 + "; the clock's " + _clockUnits + " units were paid " + _clockPay + " as they went.");
            }
            Ctx.Pay(this, slip, "reallife_job_cleaner_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || Ctx == null)
            {
                return false;
            }
            float share = HandShare;
            if (share <= 0.0001f)
            {
                return false;
            }
            slip = new PaySlip { Units = Mathf.Max(1, Mathf.RoundToInt(share * _units)), Base = Mathf.RoundToInt(share * _units * UnitPay()) };
            return true;
        }

        protected override ShiftClock Clocked => _clock;

        // ---------------------------------------------------------------- the rest of the floor by the clock

        private void OfferSkip()
        {
            if (Ctx == null || Over || _step != Step.Floor || JobMop.HandsFull || JobMop.Cleaning || Time.time - _mopOver < Debounce || LeftUnits() <= 0)
            {
                return;
            }
            bool hadMop = JobMop.Holding;
            _mop.Take();   // the game's activity panel is for empty hands
            if (!_clock.Offer())
            {
                if (hadMop)
                {
                    _autoMop = true;
                }
                return;
            }
            _panel.Hide();   // the game's activity panel has the keys while it is up
        }

        void IClockedShift.ClockStarted()
        {
            // The floor is left to the clock: the mop is put away, and the spills, if there are any, go back.
            _mop.Take();
            _leftAtClock = LeftUnits();
            ClearPatch();
            _hasPatch = false;
            Unmark();
            _panel.Hide();
        }

        void IClockedShift.ClockEnded(int count, int pay, float away)
        {
            _clockUnits += count;
            _clockPay += pay;
            if (_mode == Mode.Game)
            {
                // The clock cleaned that many of the units that were left: the floor really is that much cleaner afterwards.
                _dirt.Clean(_leftAtClock > 0 ? Mathf.Min(1f, count / (float)_leftAtClock) : 1f);
                ReadClean();
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs: cleaner: the clock cleaned " + count + " of " + _leftAtClock + " units; the floor now reads " + Mathf.FloorToInt(_clean + 0.001f) + "% clean.");
                }
            }
            else
            {
                _mopped += count;
            }
            _autoMop = true;
            _endsAt += away;   // the minutes the clock took are not the floor's own time
            _mopOver = Time.time;
            _nextTry = Time.time + 0.4f;
            _hint = -1;
            _shownSeconds = int.MinValue;
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            SaveHook.Drop(_onSave);
            _panel.Hide();
            _clock?.Close();   // the game's activity panel goes; nothing more is settled through it
            _mop?.Take();      // never a mop in the hands once the job is over, whatever ended it
            Ctx.Self.Rest();
            Ctx.Self.Free();
            ClearPatch();
            if (why != JobEnd.Fault)
            {
                ReadClean();
                Pay();
            }
            _dirt.Discard();   // the client's registration is the game's again
        }

        // The line under the job's name, rebuilt only when what it says changes.
        private void Hint(int which)
        {
            if (which == _hint)
            {
                return;
            }
            _hint = which;
            switch (which)
            {
                case 0: Show(JobText.T("reallife_job_cleaner_obj_go", "place", _clientName)); break;
                case 1: Show(JobText.T("reallife_job_cleaner_obj_back", "place", _clientName)); break;
                case 2: Show(JobText.T(_mode == Mode.Game ? "reallife_job_cleaner_obj_dirt" : "reallife_job_cleaner_obj_spill")); break;
                case 3: Show(JobText.T("reallife_job_cleaner_obj_mopping")); break;
                case 5: Show(JobText.T("reallife_job_cleaner_obj_take")); break;
                default: Show(JobText.T("reallife_job_obj_hands")); break;
            }
        }
    }
}
