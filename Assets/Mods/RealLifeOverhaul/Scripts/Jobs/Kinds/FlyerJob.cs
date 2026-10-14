using System.Collections.Generic;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Handing out flyers: the job that needs nothing. A bundle of flyers, a pitch (the stretch
    /// of street the player was standing on when they took it) and a few minutes. Walk up to a
    /// passer-by and offer one: they stop and turn; some take it, some wave it away; then they
    /// walk on. Each person is asked once. Paid per flyer taken, with a bonus for each one
    /// handed out while the street was busy.
    ///
    /// The passers-by are the game's own pedestrians, borrowed one at a time for the two
    /// seconds it takes (PedestrianTakeover) and handed back walking. Uses JobClip.HandOver,
    /// Receive and Refuse. The stack is in the player's left hand at the grip of the clip it was
    /// made for; one flyer comes out in the right hand as it is offered, and is in the taker's
    /// hand from the moment the offering hand opens (the clip's "release", to the frame) until they
    /// walk on (HeldProp). The stack is held to the chest by the left arm alone (job_flyer_hold on
    /// the masked layer) while the game walks the player; the offer is an upper-body gesture, and
    /// the player stands still for the second and a half it takes.
    ///
    /// IN THE GAME'S OWN INTERFACE. The player is dressed as a canvasser (<see cref="JobUniform"/>, taken off by the
    /// director however the shift ends). The game's panel over a place (<see cref="JobPanel"/>) stands over whoever is in
    /// reach with an "Offer a flyer" button on the game's interact key; with nobody in reach it stands over the player
    /// with "Let time pass", which hands the rest of the shift to the game's clock (<see cref="ShiftClock"/>):
    /// <see cref="ShiftRates"/> minutes a flyer, at <see cref="ShiftRates.Share"/> of a flyer's pay, no bonus. The
    /// shift's figures are on the HUD in a copy of the game's job panel (<see cref="ShiftFigures"/>).
    /// </summary>
    public sealed class FlyerJob : Job, IClockedShift
    {
        private const float Pitch = 70f;            // metres from where the job was taken that still count as the pitch
        private const float Reach = 3.5f;           // how near someone must be to be offered a flyer
        private const float Grace = 1.5f;           // a press reaches this much further: whoever was in reach a moment ago and is walking off
        private const float Debounce = 0.7f;        // seconds after an offer ends in which the panel's "let time pass" does nothing (a press meant for the next flyer)
        private const float PanelOver = 2.1f;       // metres over the player the panel stands with nobody in reach
        private const float Seconds = 420f;         // for the bundle
        private const float TakeChance = 0.55f;
        private const float TakePerLevel = 0.04f;
        private const float BusyWithin = 35f;       // people within this distance make the street "busy"...
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
        private readonly HandProp _stack = new HandProp();          // the last resort: plain shapes, when no clip can say how a stack is held
        private readonly HeldProp _stackHeld = new HeldProp();      // the stack, left hand
        private readonly HeldProp _offered = new HeldProp();        // the one flyer being held out, right hand
        private readonly HeldProp _given = new HeldProp();          // the same flyer in the taker's hand
        private float _begun;
        private float _nextGrip;
        // Who was offered one, and when. The game reuses a pedestrian's object for the next walker, so nobody is
        // struck off for the whole shift: after a while the same object is somebody new.
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
        private bool _withWalker;       // they were stopped on their walk (PedestrianTakeover) and are handed back walking
        private bool _turned;           // someone standing was turned to face the player...
        private Quaternion _turnedFrom; // ...from this, and is turned back
        private float _heldUntil;
        private bool _heldTakes;
        private bool _receiveDue;
        private float _receiveAt;
        private bool _receivePlayed;    // the taker's reach was started with the offer, so that their hand closes as the player's opens
        private bool _armed;            // the left arm holds the stack to the chest (a masked clip over the game's own walk)
        private float _armAgainAt = -1f;   // ...and takes it up again at this time, after a gesture that had the upper body
        private bool _away;
        private bool _paid;
        private int _line = -1;
        private string _markerText;

        // The game's own interface.
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

        /// <summary>While the panel is up its first button is on the game's interact key: the mod's own use of that key (a door, a car) stands aside.</summary>
        public override bool OwnsInteractKey => _panel.Up;

        /// <summary>Walking off with most of the bundle still in hand is a bad shift.</summary>
        public override bool QuitIsFailure => DoneCount > 0 && DoneCount * 3 < _bundle && DoneCount + _refusals < FairTry;

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
            _bundle = ShiftSize();   // the offer's bundle
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
                Ctx.Logger?.Info("Jobs: flyers: a bundle of " + _bundle + ", the pitch is " + Pitch.ToString("F0") + " m round the player, reach " + Reach.ToString("F1")
                                 + " m; shown in the game's own interface: job panel " + (_figures.Shown ? "copied" : "NOT copied (the panel over the passer-by is all there is)")
                                 + ", " + ShiftRates.Minutes(Def) + " game minutes a flyer by the clock at " + ShiftRates.Percent + "%.");
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
                    // A quiet street is not the player's fault: only a bundle that was barely offered is a bad shift.
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

        // ---------------------------------------------------------------- the game's own interface

        // The shift's figures on the card: each line is written only when what it says changes.
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

        // Over whoever is in reach, with "Offer a flyer"; over the player with "Let time pass" when nobody is.
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
                _panel.Show(who.head != null ? who.head : who.transform, who.head != null ? 0.42f : 2.05f, Ctx.Player);
            }
            else
            {
                _panel.Show(Ctx.Player.transform, PanelOver, Ctx.Player);
            }
        }

        // The panel's first button was pressed (the game ran it on its key or on a click).
        private void Act(string action)
        {
            if (Ctx != null && !Over && ReferenceEquals(action, ButtonOffer))
            {
                Offer(Time.time);
            }
        }

        private void OfferSkip()
        {
            if (Ctx == null || Over || _with != null || Time.time - _offerOver < Debounce || !_clock.Offer())
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

        // The stack in the left hand: at a measured grip if the clip bundle can give one, as plain shapes if it cannot.
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

        // The arm that has the stack holds it to the chest: the clip the stack's grip was measured in, on the left arm
        // alone, over whatever the game is doing with the rest of the body (walking included: nothing of the legs is
        // ours). Asked for when it changes, and again after an offer, whose gesture had the arms for a moment. Where
        // it cannot be had the stack hangs in the hand at the same grip, as it did before there was such a layer.
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

        // ---------------------------------------------------------------- offering one

        // Who the panel is about. Set only when it changes.
        private void Point(BaseHuman person, Pedestrian walker)
        {
            if (ReferenceEquals(person, _near))
            {
                return;
            }
            _near = person;
            _nearWalker = walker;
        }

        // The panel's "Offer a flyer": to whoever the panel stands over, or failing that whoever is nearest a little further out.
        private void Offer(float now)
        {
            if (_with != null || Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                return;   // a flyer is changing hands already
            }
            BaseHuman person = _near;
            Pedestrian walker = _nearWalker;
            // Whoever the prompt was about; failing that whoever is nearest a little further out, so that someone
            // who walked out of reach as the key went down is stopped all the same.
            if (person == null || !Eligible(person))
            {
                Find(Reach + Grace, out person, out walker);
            }
            Point(null, null);
            if (person == null || (walker != null && !_held.Take(walker)))
            {
                return;   // nobody in reach after all (the panel is made again on the next tick)
            }
            Remember(person.GetInstanceID(), now);
            _with = person;
            _withWalker = walker != null;
            _turned = false;
            Vector3 player = Ctx.Player.transform.position;
            var body = person as ThirdPersonCharacter;

            if (_withWalker)
            {
                // They stop where they are and turn to the player.
                NavMeshAgent agent = body.navmeshAgent;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;   // the game turns a character to its look target only while it is not being steered
                body.LookTarget = player;
            }
            else if (person.isSittingOn == null && (body == null || body.visible) && Ctx.Director != null && Ctx.Director.isActiveAndEnabled)
            {
                // Someone standing about stays where they are and is the game's throughout: they only turn, and turn back after.
                _turned = true;
                _turnedFrom = person.transform.rotation;
                Ctx.Director.StartCoroutine(Guard.Routine(Turn(person.transform, Facing(person.transform.position, player), 0.25f)));
            }
            Ctx.TurnPlayer(person.transform.position);

            _heldTakes = Random.value < (TakeChance + TakePerLevel * (Ctx.Level - 1)) * Ctx.Offer.CrowdFactor;   // a quiet street or a rush, as the offer said
            _heldUntil = now + Beat;
            Ctx.Self.Hold(Beat - 0.2f);   // on the spot while the arm is out; let go again by the director's tick
            _offered.Give(Ctx.Player.Character, Ctx.Props, OfferGrips, FlyerProp);
            float reach = Ctx.Animations.Play(Ctx.Player.Character, JobClip.HandOver);
            // With the mod's clip the flyer leaves the hand at the moment the clip opens it ("release"), to the frame.
            float release = Ctx.Animations.Moment(JobClip.HandOver, "release");
            if (release >= 0f)
            {
                _armAgainAt = now + Mathf.Max(release + 0.1f, reach - HoldAgain);   // (the gesture took the arm the stack is in)
            }
            _receivePlayed = false;
            if (_heldTakes)
            {
                _receiveDue = true;       // a moment later, as the flyer reaches them
                if (release >= 0f)
                {
                    _receiveAt = now + release;
                    float grip = Ctx.Animations.Moment(JobClip.Receive, "grip");
                    if (grip >= 0f && release - grip < 0.15f)
                    {
                        // Their hand closes (0.50 s into reaching) as the player's opens (0.55 s): both start together.
                        _receivePlayed = true;
                        Ctx.Animations.Play(person, JobClip.Receive);
                    }
                    Ctx.At(_receiveAt);
                }
                else
                {
                    _receiveAt = now + Mathf.Clamp(reach * 0.45f, 0.45f, 0.75f);
                }
            }
            else
            {
                _receiveDue = false;
                Ctx.Animations.Play(person, JobClip.Refuse);
                _refusals++;
                if (Random.value < 0.35f)
                {
                    Ctx.Cast.Emoji(person, Random.value < 0.3f ? CharacterEmojiName.CommonRedAngry : CharacterEmojiName.CommonYellowNeutral, 1.6f);
                }
                if (_refusals % 3 == 1)
                {
                    Ctx.Say(JobText.Pick(Refused, ref _line), 4f);
                }
            }
        }

        private void Holding(float now)
        {
            // The game took them back meanwhile (the view moved on, a pool was emptied).
            if (_withWalker ? _held.PutAway || !_held.Held : !_with.gameObject.activeInHierarchy)
            {
                Drop();
                return;
            }
            if (_receiveDue && now >= _receiveAt)
            {
                Pass();
            }
            if (now < _heldUntil)
            {
                return;
            }
            bool busy = false;
            if (_heldTakes)
            {
                _taken++;
                busy = Busy();
                if (busy)
                {
                    _busy++;
                }
                if (_taken % 4 == 1)
                {
                    Ctx.Say(JobText.Pick(Took, ref _line), 4f);
                }
            }
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: flyers: a flyer was " + (_heldTakes ? "taken" + (busy ? " on a busy street" : string.Empty) : "waved away") + "; taken " + _taken + " of " + _bundle
                                 + ", waved away " + _refusals + ", " + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the bundle's time left.");
            }
            _offerOver = now;
            LetGo();
        }

        /// <summary>The frame the offering hand opens: the flyer is the taker's.</summary>
        public override void Moment(float now)
        {
            if (_receiveDue && _with != null && _with.gameObject.activeInHierarchy && (!_withWalker || (_held.Held && !_held.PutAway)))
            {
                Pass();
            }
        }

        private void Pass()
        {
            _receiveDue = false;
            _offered.Take();                                              // out of the player's hand...
            _given.Give(_with, Ctx.Props, TakerGrips, FlyerProp);         // ...and into theirs, where it stays until they go
            if (!_receivePlayed)
            {
                Ctx.Animations.Play(_with, JobClip.Receive);
            }
            Ctx.Cast.Emoji(_with, CharacterEmojiName.CommonGreenHappy, 1.6f);
        }

        // Turned on the spot over a moment; they may be put away by the game meanwhile.
        private static System.Collections.IEnumerator Turn(Transform who, Quaternion to, float seconds)
        {
            Quaternion from = who != null ? who.rotation : to;
            for (float t = 0f; t < seconds && who != null && who.gameObject.activeInHierarchy; t += Time.deltaTime)
            {
                who.rotation = Quaternion.Slerp(from, to, t / seconds);
                yield return null;
            }
            if (who != null && who.gameObject.activeInHierarchy)
            {
                who.rotation = to;
            }
        }

        private static Quaternion Facing(Vector3 from, Vector3 towards)
        {
            Vector3 flat = towards - from;
            flat.y = 0f;
            return flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized) : Quaternion.identity;
        }

        // Nothing more is done to them: whoever it was is the game's again (or gone).
        private void Drop()
        {
            _offered.Take();
            _given.Take();
            if (_held.Held)
            {
                if (_held.Character != null)
                {
                    _held.Character.LookTarget = Vector3.zero;   // a pooled pedestrian keeps what it was left with
                }
                _held.Forget();
            }
            _with = null;
            _withWalker = false;
            _turned = false;
        }

        // A walker on their way again, away from the player, as one of the game's own; someone standing turned back as they were.
        private void LetGo()
        {
            BaseHuman person = _with;
            bool walker = _withWalker;
            bool turned = _turned;
            _offered.Take();   // a flyer that was waved away goes back on the stack
            _given.Take();     // nothing of ours goes with them
            _with = null;
            _withWalker = false;
            _turned = false;
            if (walker)
            {
                if (!_held.Held)
                {
                    return;
                }
                ThirdPersonCharacter body = _held.Character;
                if (body == null || _held.PutAway)
                {
                    if (body != null)
                    {
                        body.LookTarget = Vector3.zero;
                    }
                    _held.Forget();
                    return;
                }
                body.LookTarget = Vector3.zero;
                Vector3 from = body.transform.position;
                if (!StreetSpots.AwayFrom(from, Ctx != null ? Ctx.PlayerPosition : from, 30f, false, out Vector3 to))
                {
                    to = from;
                }
                _held.HandBack(to);
            }
            else if (turned && person != null && person.gameObject.activeInHierarchy)
            {
                if (Ctx != null && Ctx.Director != null && Ctx.Director.isActiveAndEnabled)
                {
                    Ctx.Director.StartCoroutine(Guard.Routine(Turn(person.transform, _turnedFrom, 0.3f)));
                }
                else
                {
                    person.transform.rotation = _turnedFrom;
                }
            }
        }

        // ---------------------------------------------------------------- who is in reach

        // The nearest person on foot who has not been asked: the walkers (the game keeps that list) and everyone
        // standing or sitting about (StreetPeople). Someone the player is facing counts as nearer than they are.
        private void Find(float reach, out BaseHuman best, out Pedestrian bestWalker)
        {
            best = null;
            bestWalker = null;
            Vector3 player = Ctx.PlayerPosition;
            Vector3 facing = Ctx.Player.transform.forward;
            float bestScore = float.MaxValue;
            float reachSqr = reach * reach;

            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers != null)
            {
                for (int i = 0; i < walkers.Count; i++)
                {
                    Pedestrian walker = walkers[i];
                    if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null)
                    {
                        continue;
                    }
                    float score = Score(walker.tpc.transform.position - player, facing, reachSqr);
                    if (score < bestScore && Eligible(walker.tpc))
                    {
                        bestScore = score;
                        best = walker.tpc;
                        bestWalker = walker;
                    }
                }
            }
            List<BaseHuman> standing = StreetPeople.Standing(Ctx.Logger);
            for (int i = 0; i < standing.Count; i++)
            {
                BaseHuman person = standing[i];
                if (person == null)
                {
                    continue;
                }
                float score = Score(person.transform.position - player, facing, reachSqr);
                if (score < bestScore && Eligible(person))
                {
                    bestScore = score;
                    best = person;
                    bestWalker = null;
                }
            }
        }

        // Squared distance on the flat, shortened for someone in front of the player; float.MaxValue out of reach.
        private static float Score(Vector3 offset, Vector3 facing, float reachSqr)
        {
            if (offset.y > 1.5f || offset.y < -1.5f)
            {
                return float.MaxValue;
            }
            float sqr = offset.x * offset.x + offset.z * offset.z;
            if (sqr >= reachSqr)
            {
                return float.MaxValue;
            }
            float ahead = offset.x * facing.x + offset.z * facing.z;   // metres in front
            return ahead > 0f && ahead * ahead > sqr * 0.5f ? sqr * 0.35f : ahead > 0f ? sqr * 0.7f : sqr;
        }

        // Out on the street, drawn, on their feet or their seat (not lying in the road), and not asked a moment ago.
        private bool Eligible(BaseHuman person)
        {
            if (person == null || !person.gameObject.activeInHierarchy || AskedLately(person.GetInstanceID()))
            {
                return false;
            }
            return !(person is ThirdPersonCharacter body) || (body.visible && !RagdollSystem.Holds(body));
        }

        private bool AskedLately(int id)
        {
            int index = _asked.IndexOf(id);
            return index >= 0 && Time.time - _askedAt[index] < AskAgain;
        }

        private void Remember(int id, float now)
        {
            int index = _asked.IndexOf(id);
            if (index >= 0)
            {
                _askedAt[index] = now;
            }
            else
            {
                _asked.Add(id);
                _askedAt.Add(now);
            }
        }

        // A busy street: enough people about, counted at the moment a flyer changes hands.
        private bool Busy()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return false;
            }
            Vector3 player = Ctx.PlayerPosition;
            int people = 0;
            for (int i = 0; i < walkers.Count && people < BusyPeople; i++)
            {
                if (walkers[i] != null && (walkers[i].transform.position - player).sqrMagnitude < BusyWithin * BusyWithin)
                {
                    people++;
                }
            }
            return people >= BusyPeople;
        }

        // ---------------------------------------------------------------- the end

        public override void End(JobEnd why)
        {
            LetGo();
            Drop();
            _stack.Remove();
            _stackHeld.Take();
            _armed = false;
            _armAgainAt = -1f;
            if (Ctx != null)
            {
                Ctx.Self.StopOver();
            }
            _near = null;
            _nearWalker = null;
            if (Ctx != null)
            {
                _panel.Hide();
                _clock?.Close();   // the game's activity panel goes; nothing more is settled through it
            }
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
            if (Ctx == null || _paid || _taken <= 0)
            {
                return false;
            }
            float each = Def.UnitPay * Ctx.Rate;   // a flyer pays less than a whole unit of money: rounded on the total, not each
            slip = new PaySlip
            {
                Units = _taken,
                Base = Mathf.RoundToInt(_taken * each),
                Bonus = Mathf.RoundToInt(_busy * each * BusyBonus + (DoneCount >= _bundle ? _taken * each * 0.1f : 0f))   // busy street; the whole bundle gone (the clock's flyers earn no bonus)
            };
            return true;
        }

        protected override ShiftClock Clocked => _clock;
    }
}
