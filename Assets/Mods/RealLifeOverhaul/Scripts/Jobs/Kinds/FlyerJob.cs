using System.Collections.Generic;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Handing out flyers, the job that needs nothing: a bundle, a pitch and a few minutes. Offered on the panel over a
    /// passer-by (<see cref="JobPanel"/>), each stops and turns, takes one or waves it away, and walks on; each is asked once.
    /// Paid per flyer taken, more on a busy street. The flyer changes hands as the offering hand opens; with nobody in reach
    /// the rest can be left to the game's clock (<see cref="ShiftClock"/>). In a canvasser's outfit.
    /// </summary>
    public sealed partial class FlyerJob : Job, IClockedShift
    {
        private const float Pitch = 70f;            // metres from where the job was taken that still count as the pitch
        private const float Reach = 3.5f;           // metres: how near someone must be to be offered a flyer
        private const float Grace = 1.5f;           // metres further a press reaches: whoever was in reach a moment ago and is walking off
        private const float Debounce = 0.7f;        // seconds after an offer ends in which Let time pass does nothing (a press meant for the next flyer)
        private const float PanelOver = 2.1f;       // metres over the player the panel stands with nobody in reach
        private const float Seconds = 420f;         // seconds for the bundle
        private const float TakeChance = 0.55f;     // a passer-by's chance of taking one, at level 1
        private const float TakePerLevel = 0.04f;   // added for each level above 1
        private const float BusyWithin = 35f;       // metres: people this near make the street busy...
        private const int BusyPeople = 6;           // ...when there are this many
        private const float BusyBonus = 0.4f;       // share of a flyer's pay added for one handed out on a busy street
        private const float Beat = 1.5f;            // seconds a passer-by is held for
        private const float AskAgain = 100f;        // seconds before the same pedestrian object counts as a new passer-by
        private const int FairTry = 10;             // offers made that show the player worked the pitch, whatever was taken
        private const float GripWait = 4f;          // seconds the clip bundle is given to load before the plain stack is used

        // Clips whose tool is the thing held, best first; each brings its own prop and grip.
        private static readonly string[] StackGrips = { "job_flyer_hold", "leaflet_idle" };
        private static readonly string[] OfferGrips = { "job_hand_over", "leaflet_offer" };
        private static readonly string[] TakerGrips = { "job_receive", "job_hand_over", "leaflet_offer" };
        private const string FlyerProp = "flyer";   // the one sheet that changes hands: an A5 flyer, as the stack's are (the leaflet where the bundle has none)
        private const float HoldAgain = 0.45f;      // seconds before the end of the offering gesture at which the left arm takes the stack up again

        private static readonly string[] Took = { "reallife_job_flyers_took_1", "reallife_job_flyers_took_2", "reallife_job_flyers_took_3", "reallife_job_flyers_took_4" };
        private static readonly string[] Refused = { "reallife_job_flyers_no_1", "reallife_job_flyers_no_2", "reallife_job_flyers_no_3", "reallife_job_flyers_no_4" };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Flyers);
        private readonly PedestrianTakeover _held = new PedestrianTakeover();
        private readonly HandProp _stack = new HandProp();          // the last resort, plain shapes, when no clip says how a stack is held
        private readonly HeldProp _stackHeld = new HeldProp();      // the stack, left hand
        private readonly HeldProp _offered = new HeldProp();        // the one flyer being held out, right hand
        private readonly HeldProp _given = new HeldProp();          // the same flyer in the taker's hand
        private float _begun;
        private float _nextGrip;
        // Who was offered one and when: the game reuses a pedestrian's object, so after a while it is somebody new.
        private readonly List<int> _asked = new List<int>(64);
        private readonly List<float> _askedAt = new List<float>(64);

        private Vector3 _centre;
        private float _endsAt;
        private int _bundle;
        private int _taken;
        private int _busy;              // of those, how many on a busy street
        private int _refusals;
        private BaseHuman _near;        // who the prompt is about...
        private Pedestrian _nearWalker; // ...and the pedestrian they are, if they are one of the game's walkers
        private BaseHuman _with;        // who a flyer is being offered to right now
        private bool _withWalker;       // stopped on their walk (PedestrianTakeover) and handed back walking
        private bool _turned;           // someone standing was turned to face the player...
        private Quaternion _turnedFrom; // ...from this, and is turned back
        private float _heldUntil;
        private bool _heldTakes;
        private bool _receiveDue;
        private float _receiveAt;
        private bool _receivePlayed;    // the taker's reach started with the offer, so their hand closes as the player's opens
        private bool _armed;            // the left arm holds the stack to the chest: a masked clip over the game's walk
        private float _armAgainAt = -1f;   // ...and takes it up again at this time, after a gesture that had the upper body
        private bool _away;
        private bool _paid;
        private int _line = -1;
        private string _markerText;

        private readonly JobPanel _panel;
        private ShiftFigures _figures;
        private ShiftClock _clock;
        private int _clocked;           // flyers the game's clock handed out
        private int _clockPay;          // ...and what it was paid for them
        private float _offerOver = -10f;
        private bool _dressed;
        private string _objWalk;
        private string _objNear;
        private string _objClock;
        private string _objBack;
        private string _panelNear;
        private string _panelNone;
        private string _panelLeft;
        private int _shownSeconds = int.MinValue;

        private const string ButtonOffer = "reallife_job_flyers_button_offer";
        private const string ButtonSkip = "reallife_job_button_skip";

        public FlyerJob()
        {
            _panel = new JobPanel("flyer panel", Act, OfferSkip);
        }

        public override JobDef Def => _def;

        public override bool PassingTime => _clock != null && _clock.Working;

        public override bool OwnsInteractKey => _panel.Up;

        public override bool QuitIsFailure => DoneCount * 3 < _bundle && DoneCount + _refusals < FairTry;

        private int DoneCount => _taken + _clocked;

        int IClockedShift.ClockLeft => Mathf.Max(0, _bundle - DoneCount);

        float IClockedShift.ClockPay => Def.UnitPay * Ctx.Rate;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_flyers_on_foot";
                return false;
            }
            if (!StreetSpots.OnPavement(Ctx.PlayerPosition, 12f, out _centre))
            {
                Refusal = "reallife_job_no_here";
                return false;
            }
            _bundle = ShiftSize();
            if (_bundle <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            _endsAt = now + Seconds;
            _begun = now;
            BeginSpeed(now, Seconds);
            _markerText = JobText.T("reallife_job_flyers_marker");
            _objWalk = JobText.T("reallife_job_flyers_obj_walk");
            _objNear = JobText.T("reallife_job_flyers_obj_near");
            _objClock = JobText.T("reallife_job_flyers_obj_clock");
            _objBack = JobText.T("reallife_job_flyers_obj_back");
            _panelNear = JobText.T("reallife_job_flyers_panel_near");
            _panelNone = JobText.T("reallife_job_flyers_panel_none");
            _panelLeft = JobText.T("reallife_job_flyers_panel_left");
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_flyers_card_done");
            _clock = new ShiftClock(Ctx, this, this, "flyers", "reallife_job_flyers_unit", "reallife_job_flyers_units", "reallife_job_flyers_slip");
            Show(_objWalk);
            Figures(now);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: flyers: a bundle of " + _bundle + ", the pitch is " + Pitch.ToString("F0") + " m round the player, reach " + Reach.ToString("F1", CultureInfo.InvariantCulture)
                                 + " m; shown in the game's own interface: job panel " + (_figures.Shown ? "copied" : "NOT copied (the panel over the passer-by is all there is)")
                                 + ", " + ShiftRates.Minutes(Def) + " game minutes a flyer by the clock at " + ShiftRates.Percent + "%.");
            }
            return true;
        }

        public override void Tick(float now)
        {
            if (_clock.Hold())
            {
                Figures(now);   // the clock is working, or its offer is up with the game paused
                return;
            }
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            if (!_dressed && onFoot && Ctx.Player.Character.visible)
            {
                _dressed = true;
                Ctx.Uniform.Wear(Ctx.Player, CityLook.Canvasser);   // once: the director takes it off however the shift ends
            }
            ShowStack(now, onFoot && Ctx.Player.Character.visible);

            if (_with != null)
            {
                Holding(now);
            }
            if (DoneCount >= _bundle)
            {
                if (_with == null)
                {
                    _panel.Hide();
                    if (_clocked == 0)
                    {
                        EndSpeed(now);   // the whole bundle by hand
                    }
                    Finish(JobEnd.Done);
                }
                return;
            }
            if (now >= _endsAt)
            {
                if (_with == null)
                {
                    Ctx.Say("reallife_job_flyers_time", 8f);
                    // A quiet street is not the player's fault: only a bundle barely offered is a bad shift.
                    _panel.Hide();
                    Finish(DoneCount * 3 < _bundle && DoneCount + _refusals < FairTry ? JobEnd.Failed : JobEnd.Done);
                }
                return;
            }

            bool away = JobSpots.Flat(Ctx.PlayerPosition, _centre) > Pitch;
            if (away != _away)
            {
                _away = away;
                if (away)
                {
                    Mark(_centre, _markerText);
                    Show(_objBack);
                }
                else
                {
                    Unmark();
                }
            }
            if (away || !onFoot || _with != null)
            {
                Point(null, null);
            }
            else
            {
                Find(Reach, out BaseHuman person, out Pedestrian walker);
                Point(person, walker);
            }
            if (!away)
            {
                Show(_with != null || _near != null ? _objNear : _objWalk);
            }
            Figures(now);
            Panel(onFoot && !away);
        }

        private void Figures(float now)
        {
            bool clock = _clock.Working;
            if (clock)
            {
                Show(_objClock);
            }
            _figures.Objective(Objective);
            _figures.Units(DoneCount + _clock.Done, _bundle);
            int seconds = clock ? int.MaxValue : Mathf.Max(0, Mathf.CeilToInt(_endsAt - now));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _figures.Limit(clock ? null : JobText.T("reallife_job_card_time_left", "time", ShiftFigures.Clock(seconds)), seconds <= 60 ? WorldPanel.Yellow() : WorldPanel.White());
            }
            _figures.Money(Mathf.RoundToInt(_taken * Def.UnitPay * Ctx.Rate) + _clockPay, 0);
            _figures.Time(Mathf.Max(0, _bundle - DoneCount - _clock.Done) * _clock.MinutesEach);
        }

        // Over whoever is in reach with Offer a flyer; over the player with Let time pass when nobody is.
        private void Panel(bool active)
        {
            if (!active || _clock.Offered)
            {
                _panel.Hide();
                return;
            }
            int left = Mathf.Max(0, _bundle - DoneCount);
            BaseHuman who = _with != null ? _with : _near;
            bool someone = who != null;
            _panel.Offer(someone ? _panelNear : _panelNone, _panelLeft, JobText.Number(left), _panel.White,
                someone ? ButtonOffer : null, _with == null, someone || left <= 0 ? null : ButtonSkip, true);
            if (someone)
            {
                _panel.Show(who.head != null ? who.head : who.transform, who.head != null ? 0.42f : 2.05f);
            }
            else
            {
                _panel.Show(Ctx.Player.transform, PanelOver);
            }
        }

        private void Act(string action)
        {
            if (!Over && ReferenceEquals(action, ButtonOffer))
            {
                Offer(Time.time);
            }
        }

        private void OfferSkip()
        {
            if (Over || _with != null || Time.time - _offerOver < Debounce || !_clock.Offer())
            {
                return;
            }
            _panel.Hide();   // the game's activity panel has the keys while it is up
        }

        void IClockedShift.ClockStarted()
        {
            Point(null, null);
            _panel.Hide();
        }

        void IClockedShift.ClockEnded(int count, int pay, float away)
        {
            _clocked += count;
            _clockPay += pay;
            _endsAt += away;   // the minutes the clock took are not the bundle's own time
            _offerOver = Time.time;
            _shownSeconds = int.MinValue;
        }

        // The stack in the left hand: at a measured grip if the clip bundle has one, else as plain shapes.
        private void ShowStack(float now, bool show)
        {
            if (!_stackHeld.Held && !_stack.Fitted && show && now >= _nextGrip)
            {
                _nextGrip = now + 1f;
                _stackHeld.Give(Ctx.Player.Character, Ctx.Props, StackGrips);
            }
            if (_stackHeld.Held)
            {
                _stackHeld.Show(show);
                Carry(now, show);
            }
            else if (now - _begun > GripWait)
            {
                _stack.Show(Ctx.Player.Character, show);
            }
        }

        // The left arm holds the stack by its clip over whatever the game does with the body, again after an offer's gesture.
        private void Carry(float now, bool on)
        {
            if (on && _armed && _armAgainAt >= 0f && now >= _armAgainAt)
            {
                _armed = false;
            }
            if (on == _armed)
            {
                return;
            }
            if (!on)
            {
                _armed = false;
                _armAgainAt = -1f;
                Ctx.Self.StopOver();
                return;
            }
            if (_armAgainAt >= 0f && now < _armAgainAt)
            {
                return;   // the offering gesture has the arms
            }
            _armAgainAt = -1f;
            CityClip hold = HeldProp.GripOf(StackGrips);
            _armed = hold != null && hold.Loop && hold.Grip.Hand == HumanBodyBones.LeftHand && Ctx.Self.Over(hold.Name, BodyMask.LeftArm);
        }

        public override void End(JobEnd why)
        {
            LetGo();
            Drop();
            _stack.Remove();
            _stackHeld.Take();
            _armed = false;
            _armAgainAt = -1f;
            Ctx.Self.StopOver();
            _near = null;
            _nearWalker = null;
            _panel.Hide();
            _clock?.Close();   // the game's activity panel goes; nothing more is settled through it
            if (why == JobEnd.Fault || !Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_job_flyers_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || _taken <= 0)
            {
                return false;
            }
            float each = Def.UnitPay * Ctx.Rate;   // a flyer pays less than a whole unit of money: rounded on the total
            slip = new PaySlip
            {
                Units = _taken,
                Base = Mathf.RoundToInt(_taken * each),
                Bonus = Mathf.RoundToInt(_busy * each * BusyBonus + (DoneCount >= _bundle ? _taken * each * 0.1f : 0f))   // a busy street; the whole bundle gone (the clock's flyers earn no bonus)
            };
            return true;
        }

        protected override ShiftClock Clocked => _clock;
    }
}
