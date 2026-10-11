using BigAmbitions.Characters;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Contract cleaner. A business near the player that is not theirs, and is open, has spills
    /// on its floor: walk to its door, go in, and mop each patch in turn. A patch is one of the
    /// mod's own (a flat mark on a clear piece of the shop's real floor, with a bucket beside
    /// it), not one of the game's dirt spots, which belong to the owner's own cleaning. Stand on
    /// a patch and mop: the patch shrinks under the mop and is gone in a few seconds. Paid per
    /// patch, a little more for the whole floor.
    ///
    /// The mopping is played on the player, who is held on the spot for it (the game's own
    /// blocker), in this order of preference:
    ///  - the mod's <c>mop_floor</c> clip with the modelled <c>mop</c> at the clip's grip;
    ///  - the game's own cleaning animation with the game's own mop in the hand, exactly as its
    ///    cleaners and the player's own mop use them.
    /// If the player's animator has neither, the job is not offered here.
    ///
    /// The shift goes on inside that one building (<see cref="WorksInside"/>); going into any
    /// other ends it, and the player may step out and come back.
    ///
    /// IN THE GAME'S OWN INTERFACE. The player works in the cleaner's uniform (<see cref="JobUniform"/>). The game's panel
    /// over a place (<see cref="JobPanel"/>) stands over the client's door with "Go in" and over each spill in reach
    /// with "Mop it up" on the game's interact key and "Let time pass" beside it, which hands the rest of the floor to the
    /// game's clock (<see cref="ShiftClock"/>): <see cref="ShiftRates"/> minutes a spill, at
    /// <see cref="ShiftRates.Share"/> of a spill's pay, no bonus. The figures are on the HUD in a copy of the game's job
    /// panel (<see cref="ShiftFigures"/>).
    /// </summary>
    public sealed class CleanerJob : Job, IClockedShift
    {
        private enum Step { ToClient, Floor, Mopping }

        private const float Debounce = 0.7f;           // seconds after a spill is mopped in which the panel's "let time pass" does nothing
        private const float PanelUp = 1.6f;            // metres over the door or spill the panel stands
        private const string ButtonEnter = "reallife_job_button_enter";
        private const string ButtonMop = "reallife_job_cleaner_button_mop";
        private const string ButtonSkip = "reallife_job_button_skip";

        private const float ClientMin = 15f;
        private const float ClientMax = 260f;
        private const float DoorReach = 3.2f;
        private const float PatchReach = 1.1f;
        private const float PatchesApart = 2.4f;
        private const float MopSeconds = 5.5f;
        private const float Seconds = 330f;            // for the whole floor, the walk there included
        private const float SecondsPerMetre = 0.5f;    // more for a client further off
        private const int MostTries = 4;               // looks for the next clear piece of floor before calling the floor done
        private const string MopClip = "mop_floor";
        private const string GameMop = "Mop";
        private const string PatchProp = "floor_patch";
        private const string BucketProp = "mop_bucket";

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Cleaner);
        private readonly Vector3[] _done = new Vector3[16];

        private Step _step;
        private DoorSpot _client;
        private string _clientName;
        private int _patches;          // how many the shift is
        private int _mopped;           // by hand and by the clock
        private int _hand;             // ...of which by hand
        private int _clockPay;         // what the clock's patches were paid
        private float _endsAt;
        private float _mopOver = -10f;
        private bool _dressed;
        private readonly JobPanel _panel;
        private ShiftFigures _figures;
        private ShiftClock _clock;
        private string _panelDoor;
        private string _panelSpill;
        private string _panelHands;
        private string _panelLeft;
        private string _objClock;
        private int _shownSeconds = int.MinValue;
        private Vector3 _patchAt;
        private bool _hasPatch;
        private int _tries;
        private float _nextTry;
        private Transform _patch;
        private Transform _bucket;
        private float _mopFrom;
        private float _mopShown = -1f;
        private bool _wasInside;
        private bool _paid;
        private int _hint = -1;
        private string _doorLabel;
        private string _patchLabel;

        public CleanerJob()
        {
            _panel = new JobPanel("cleaner panel", Act, OfferSkip);
        }

        public override JobDef Def => _def;

        public override bool PassingTime => _clock != null && _clock.Working;

        /// <summary>While the panel is up its first button is on the game's interact key: the mod's own use of that key stands aside.</summary>
        public override bool OwnsInteractKey => _panel.Up;

        int IClockedShift.ClockLeft => Mathf.Max(0, _patches - _mopped);

        float IClockedShift.ClockPay => UnitPay();

        /// <summary>Walking out on a floor that is mostly still dirty is a bad shift.</summary>
        public override bool QuitIsFailure => _mopped > 0 && _mopped * 2 < _patches;   // (by hand or by the clock)

        public override bool WorksInside(Buildings.Building building)
        {
            return _client.Building != null && ReferenceEquals(_client.Building.building, building);
        }

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
            _patches = Ctx.FullShift ? Def.ShiftUnits : Mathf.Min(Def.ShiftUnits, JobState.LeftToday(Def));
            if (_patches <= 0)
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
            _patchLabel = JobText.T("reallife_job_cleaner_marker_patch");
            _panelDoor = JobText.T("reallife_job_cleaner_panel_door", "place", _clientName);
            _panelSpill = JobText.T("reallife_job_cleaner_panel_spill");
            _panelHands = JobText.T("reallife_job_obj_hands");
            _panelLeft = JobText.T("reallife_job_cleaner_panel_left");
            _objClock = JobText.T("reallife_job_cleaner_obj_clock");
            _endsAt = now + Seconds + JobSpots.Flat(Ctx.PlayerPosition, _client.Front) * SecondsPerMetre;
            _step = Step.ToClient;
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_cleaner_card_done");
            _clock = new ShiftClock(Ctx, this, this, "cleaner", "reallife_job_cleaner_unit", "reallife_job_cleaner_units", "reallife_job_cleaner_slip");
            Mark(_client.Front, _doorLabel);
            Hint(0);
            Figures(now);
            Ctx.Logger?.Info("Jobs: cleaning at " + _clientName + " (" + _client.Name + "), " + _patches + " patches.");
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: " + JobSpots.Flat(Ctx.PlayerPosition, _client.Front).ToString("F0") + " m to the client's door; shown in the game's own interface: job panel "
                                 + (_figures.Shown ? "copied" : "NOT copied (the panels over the door and the spill are all there is)") + ", " + ShiftRates.Minutes(Def)
                                 + " game minutes a patch by the clock at " + ShiftRates.Percent + "%.");
            }
            return true;
        }

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
                if (!inside)
                {
                    StopMopping();
                    ClearPatch();   // the floor they left is not the street's: found again when they are back in
                    _hasPatch = false;
                    _step = Step.ToClient;
                }
                else
                {
                    _step = Step.Floor;
                    _nextTry = now + 0.6f;   // a moment for the shop to finish arriving round them
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

        // The shift's figures on the card: each line is written only when what it says changes.
        private void Figures(float now)
        {
            bool clock = _clock.Working;
            if (clock)
            {
                Show(_objClock);
            }
            _figures.Objective(Objective);
            _figures.Units(_mopped + _clock.Done, _patches);
            int seconds = clock ? int.MaxValue : Mathf.Max(0, Mathf.CeilToInt(_endsAt - now));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _figures.Limit(clock ? null : JobText.T("reallife_job_card_time_left", "time", ShiftFigures.Clock(seconds)), seconds <= 60 ? WorldPanel.Yellow() : _panel.White);
            }
            _figures.Money(Mathf.RoundToInt(_hand * UnitPay()) + _clockPay, 0);
            _figures.Time(Mathf.Max(0, _patches - _mopped - _clock.Done) * _clock.MinutesEach);
        }

        private void ToClient()
        {
            Mark(_client.Front, _doorLabel);
            Hint(_mopped > 0 ? 1 : 0);
            if (!Ctx.Driving && !PlayerHelper.IsUsingVehicle && JobSpots.Flat(Ctx.PlayerPosition, _client.Front) < DoorReach)
            {
                _panel.Offer(_panelDoor, _panelLeft, JobText.Number(_patches - _mopped), _panel.White, ButtonEnter, true, null, false);
                _panel.Show(_client.Front + Vector3.up * PanelUp, Ctx.Player);
            }
            else
            {
                _panel.Hide();
            }
        }

        private void Floor(float now)
        {
            if (_mopped >= _patches)
            {
                Ctx.Say("reallife_job_cleaner_done", 7f);   // (the last of them was done by the clock)
                _panel.Hide();
                Conclude();
                return;
            }
            if (!_hasPatch)
            {
                _panel.Hide();
                Unmark();
                if (now < _nextTry)
                {
                    return;
                }
                _nextTry = now + 1f;
                if (!NextPatch())
                {
                    if (++_tries >= MostTries)
                    {
                        // No clear floor left to work on in a shop this size: what was done is the job.
                        Ctx.Say("reallife_job_cleaner_small", 7f);
                        Conclude();
                    }
                    return;
                }
                _tries = 0;
                Hint(2);
            }
            Mark(_patchAt, _patchLabel);
            bool onIt = JobSpots.Flat(Ctx.Player.transform.position, _patchAt) < PatchReach;
            if (onIt)
            {
                bool hands = PlayerHelper.IsHoldingItem;
                _panel.Offer(hands ? _panelHands : _panelSpill, _panelLeft, JobText.Number(_patches - _mopped), _panel.White, ButtonMop, !hands, hands ? null : ButtonSkip, true);
                _panel.Show(_patchAt + Vector3.up * PanelUp, Ctx.Player);
                Hint(hands ? 4 : 2);
            }
            else
            {
                _panel.Hide();
                Hint(2);
            }
        }

        // A clear piece of the shop's floor the player can walk to, away from the patches already done.
        private bool NextPatch()
        {
            if (!JobSites.FloorSpot(Ctx.Player.transform.position, _done, Mathf.Min(_hand, _done.Length), PatchesApart, out _patchAt))
            {
                return false;
            }
            ClearPatch();
            _patch = Ctx.Props.Take(PatchProp);
            if (_patch != null)
            {
                _patch.SetPositionAndRotation(_patchAt + Vector3.up * 0.004f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                _mopShown = -1f;
            }
            // The bucket stands at the edge of the patch, if there is floor for it there.
            Vector3 beside = _patchAt + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 0.75f;
            if (ShopFloor.Standable(beside, 0.3f, out Vector3 stood) && JobSpots.Flat(stood, _patchAt) > 0.55f)
            {
                _bucket = Ctx.Props.Take(BucketProp);
                if (_bucket != null)
                {
                    _bucket.SetPositionAndRotation(stood, Quaternion.identity);
                }
            }
            _hasPatch = true;
            return true;
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
            if (!ReferenceEquals(action, ButtonMop) || _step != Step.Floor || !_hasPatch || PlayerHelper.IsHoldingItem)
            {
                return;
            }
            _panel.Hide();
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: mopping spill " + (_mopped + 1) + " of " + _patches + ", " + JobSpots.Flat(Ctx.Player.transform.position, _patchAt).ToString("F1")
                                 + " m from its middle; " + Mathf.Max(0, Mathf.CeilToInt(_endsAt - Time.time)) + " s of the floor's time left.");
            }
            float now = Time.time;
            // Held on the patch, facing across it, and mopping: the mod's clip and mop if the bundle has them, else the game's own.
            Ctx.Self.Hold();
            bool mopping = Ctx.Self.Clip(MopClip);
            if (!mopping && Ctx.Self.Pose(PermanentAnimationType.Cleaning, PermanentAnimationType.CleaningIdle))
            {
                mopping = true;
                Ctx.Self.GameProp(GameMop, Ctx.Player.Character.rightHand);
            }
            if (!mopping)
            {
                Ctx.Self.Free();
                Ctx.Say("reallife_job_cleaner_no_mop", 7f);
                Conclude();
                return;
            }
            _mopFrom = now;
            _step = Step.Mopping;
            Unmark();
            Hint(3);
        }

        private void Mopping(float now)
        {
            float share = Mathf.Clamp01((now - _mopFrom) / MopSeconds);
            // The patch goes as it is mopped: pushed only when it has shrunk by a visible step.
            if (_patch != null && share - _mopShown >= 0.1f)
            {
                _mopShown = share;
                float left = Mathf.Max(0.05f, 1f - share);
                _patch.localScale = new Vector3(left, 1f, left);
            }
            if (share < 1f)
            {
                return;
            }
            if (_hand < _done.Length)
            {
                _done[_hand] = _patchAt;
            }
            _hand++;
            _mopped++;
            _mopOver = now;
            _hint = -1;   // the count in the line has changed
            StopMopping();
            ClearPatch();
            _hasPatch = false;
            _step = Step.Floor;
            _nextTry = now + 0.4f;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: spill mopped by hand in " + MopSeconds.ToString("F1") + " s; mopped " + _mopped + " of " + _patches + ", "
                                 + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the floor's time left.");
            }
            if (_mopped >= _patches)
            {
                Ctx.Say("reallife_job_cleaner_done", 7f);
                Conclude();
            }
        }

        private void StopMopping()
        {
            if (_step == Step.Mopping || Ctx.Self.Holding)
            {
                Ctx.Self.Rest();
                Ctx.Self.Free();
            }
        }

        private void ClearPatch()
        {
            if (_patch != null)
            {
                Ctx.Props.Return(_patch);
                _patch = null;
            }
            if (_bucket != null)
            {
                Ctx.Props.Return(_bucket);
                _bucket = null;
            }
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
                case 2: Show(JobText.T("reallife_job_cleaner_obj_spill")); break;
                case 3: Show(JobText.T("reallife_job_cleaner_obj_mopping")); break;
                default: Show(JobText.T("reallife_job_obj_hands")); break;
            }
        }

        private void Conclude()
        {
            Settle();
            // A floor with no room to work on is nobody's fault; half the patches or more is a shift done.
            Finish(_mopped * 2 >= _patches || _tries >= MostTries ? JobEnd.Done : JobEnd.Failed);
        }

        private void Settle()
        {
            if (_paid || _hand <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            int each = UnitPay();
            var slip = new PaySlip
            {
                Units = _hand,
                Base = _hand * each,
                Bonus = _mopped >= _patches ? Mathf.RoundToInt(_hand * each * 0.1f) : 0   // the whole floor (the clock's patches earn no bonus)
            };
            Ctx.Pay(this, slip, "reallife_job_cleaner_slip");
        }

        // ---------------------------------------------------------------- the rest of the floor by the clock

        private void OfferSkip()
        {
            if (Ctx == null || Over || _step != Step.Floor || PlayerHelper.IsHoldingItem || Time.time - _mopOver < Debounce || !_clock.Offer())
            {
                return;
            }
            _panel.Hide();   // the game's activity panel has the keys while it is up
        }

        void IClockedShift.ClockStarted()
        {
            // The floor is cleared: the patch and the bucket go back, and the next patch is found when the clock is over.
            ClearPatch();
            _hasPatch = false;
            Unmark();
            _panel.Hide();
        }

        void IClockedShift.ClockEnded(int count, int pay, float away)
        {
            _mopped += count;
            _clockPay += pay;
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
            _panel.Hide();
            _clock?.Close();   // the game's activity panel goes; nothing more is settled through it
            Ctx.Self.Rest();
            Ctx.Self.Free();
            ClearPatch();
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
